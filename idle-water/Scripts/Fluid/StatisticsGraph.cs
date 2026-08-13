
using System;
using System.Collections.Generic;
using Godot;

public partial class StatisticsGraph : Control
{
	private const float GraphWidth = 720.0f;
	private const float GraphHeight = 330.0f;
	private const float GraphGap = 20.0f;
	private const float TotalHeight = GraphHeight * 2.0f + GraphGap;

	private const float PlotLeft = 58.0f;
	private const float PlotTop = 48.0f;
	private const float PlotRight = 660.0f;
	private const float PlotBottom = 278.0f;

	private const int SampleFrames = 60;
	private const int MaxSamples = 601;
	private const float FpsGraphMax = 60.0f;

	// Second graph: one completed measurement every 600 physics frames.
	// The first 600 frames are deliberately discarded as warm-up,
	// so the first point is produced at frame 1200.
	private const int EnergyParticleWindowFrames = 600;

	private readonly List<int> particleHistory = new List<int>();
	private readonly List<double> energyHistory = new List<double>();
	private readonly List<float> fpsHistory = new List<float>();

	private readonly List<float> rainEnergyRainHistory = new List<float>();
	private readonly List<double> rainEnergyPerParticleHistory = new List<double>();

	private int sampleFrameCounter = 0;
	private double maxEnergy = 1.0;

	private int rainWindowFrameCounter = 0;
	private bool rainEnergyWarmupComplete = false;
	private double rainWindowEnergyPerSecondSum = 0.0;
	private double rainWindowActiveParticleSum = 0.0;
	private double rainWindowRainSum = 0.0;

	private float lastRainPercent = 0.0f;

	private float measuredRainMin = 0.0f;
	private float measuredRainMax = 1.0f;
	private double highestEnergyPerActiveParticle = 1.0;

	private Label titleLabel;
	private Label particleLabel;
	private Label energyLabel;
	private Label fpsLabel;

	private Label rainEnergyTitleLabel;
	private Label rainEnergyStatusLabel;

	public override void _Ready()
	{
		CustomMinimumSize = new Vector2(GraphWidth, TotalHeight);

		MouseFilter = Control.MouseFilterEnum.Ignore;
		ZIndex = 100;

		titleLabel = CreateLabel(
			"Particles & Energy",
			new Vector2(16.0f, 8.0f),
			22
		);

		particleLabel = CreateLabel(
			"Particles  0",
			new Vector2(350.0f, 10.0f),
			16
		);
		particleLabel.AddThemeColorOverride(
			"font_color",
			new Color(0.35f, 0.75f, 1.0f, 1.0f)
		);

		energyLabel = CreateLabel(
			"Energy  0.00/s",
			new Vector2(350.0f, 30.0f),
			16
		);
		energyLabel.AddThemeColorOverride(
			"font_color",
			new Color(1.0f, 0.75f, 0.25f, 1.0f)
		);

		fpsLabel = CreateLabel(
			"FPS  0    History 600s",
			new Vector2(16.0f, 302.0f),
			16
		);
		fpsLabel.AddThemeColorOverride(
			"font_color",
			new Color(0.0f, 0.0f, 0.0f, 1.0f)
		);

		rainEnergyTitleLabel = CreateLabel(
			"Rain vs Energy / Active Particle",
			new Vector2(16.0f, GraphHeight + GraphGap + 8.0f),
			22
		);

		rainEnergyStatusLabel = CreateLabel(
			"First measurement in 20s",
			new Vector2(350.0f, GraphHeight + GraphGap + 10.0f),
			16
		);
		rainEnergyStatusLabel.AddThemeColorOverride(
			"font_color",
			new Color(1.0f, 1.0f, 1.0f, 1.0f)
		);

		QueueRedraw();
	}

	private Label CreateLabel(
		string text,
		Vector2 position,
		int fontSize)
	{
		Label label = new Label();

		label.Text = text;
		label.Position = position;

		label.AddThemeFontSizeOverride(
			"font_size",
			fontSize
		);

		label.AddThemeColorOverride(
			"font_shadow_color",
			new Color(0.0f, 0.0f, 0.0f, 0.8f)
		);

		label.AddThemeConstantOverride(
			"shadow_offset_x",
			2
		);

		label.AddThemeConstantOverride(
			"shadow_offset_y",
			2
		);

		label.MouseFilter = Control.MouseFilterEnum.Ignore;

		AddChild(label);

		return label;
	}

	// Called by FluidSimulator once per physics frame.
	public void SetRainAmount(float rainPercent)
	{
		lastRainPercent = Mathf.Clamp(
			rainPercent,
			0.0f,
			100.0f
		);
	}

	public void AddSample(
		int activeParticleCount,
		double totalEnergy,
		float fps,
		float delta)
	{
		int safeParticles = Math.Max(
			0,
			activeParticleCount
		);

		double safeEnergy = Math.Max(
			0.0,
			totalEnergy
		);

		float safeDelta = Mathf.Max(
			delta,
			0.0f
		);

		sampleFrameCounter++;

		UpdateCurrentValues(
			safeParticles,
			safeEnergy,
			fps
		);

		// --------------------------------------------------------
		// Existing 60-frame graph
		// --------------------------------------------------------

		if (sampleFrameCounter >= SampleFrames)
		{
			sampleFrameCounter -= SampleFrames;

			particleHistory.Add(safeParticles);

			energyHistory.Add(safeEnergy);

			fpsHistory.Add(
				Mathf.Clamp(
					fps,
					0.0f,
					FpsGraphMax
				)
			);

			while (particleHistory.Count > MaxSamples)
			{
				particleHistory.RemoveAt(0);
			}

			while (energyHistory.Count > MaxSamples)
			{
				energyHistory.RemoveAt(0);
			}

			while (fpsHistory.Count > MaxSamples)
			{
				fpsHistory.RemoveAt(0);
			}

			RecalculateEnergyScale();
		}

		// --------------------------------------------------------
		// New 600-frame rain/energy measurement
		// --------------------------------------------------------
		//
		// totalEnergy is the existing graph's energy-per-second
		// value. Multiplying by delta gives the actual energy
		// generated during this physics frame.
		//
		// We accumulate the raw frame values for the entire
		// measurement window and only calculate the statistic
		// when the 600-frame window is complete.
		// --------------------------------------------------------

		if (rainEnergyWarmupComplete)
		{
			rainWindowEnergyPerSecondSum += safeEnergy;
			rainWindowActiveParticleSum += safeParticles;
			rainWindowRainSum += lastRainPercent;

			rainWindowFrameCounter++;

			if (
				rainWindowFrameCounter >=
				EnergyParticleWindowFrames)
			{
				FinishRainEnergyMeasurement();

				rainWindowFrameCounter = 0;
				rainWindowEnergyPerSecondSum = 0.0;
				rainWindowActiveParticleSum = 0.0;
				rainWindowRainSum = 0.0;
			}
		}
		else
		{
			rainWindowFrameCounter++;

			if (
				rainWindowFrameCounter >=
				EnergyParticleWindowFrames)
			{
				// Frames 1-600 are warm-up only.
				// The next 600 frames are the first real
				// measurement window, ending at frame 1200.
				rainEnergyWarmupComplete = true;
				rainWindowFrameCounter = 0;
				rainWindowEnergyPerSecondSum = 0.0;
				rainWindowActiveParticleSum = 0.0;
				rainWindowRainSum = 0.0;
			}
		}

		UpdateRainEnergyStatus();

		QueueRedraw();
	}

	private void FinishRainEnergyMeasurement()
	{
		double averageEnergyPerSecond =
			rainWindowEnergyPerSecondSum /
			EnergyParticleWindowFrames;

		double averageActiveParticles =
			rainWindowActiveParticleSum /
			EnergyParticleWindowFrames;

		float averageRain =
			(float)(
				rainWindowRainSum /
				EnergyParticleWindowFrames
			);

		double energyPerActiveParticle = 0.0;

		if (averageActiveParticles > 0.000001)
		{
			energyPerActiveParticle =
				averageEnergyPerSecond /
				averageActiveParticles;
		}

		energyPerActiveParticle =
			Math.Max(
				0.0,
				energyPerActiveParticle
			);

		rainEnergyRainHistory.Add(
			Mathf.Clamp(
				averageRain,
				0.0f,
				100.0f
			)
		);

		rainEnergyPerParticleHistory.Add(
			energyPerActiveParticle
		);

		while (
			rainEnergyRainHistory.Count >
			MaxSamples)
		{
			rainEnergyRainHistory.RemoveAt(0);
		}

		while (
			rainEnergyPerParticleHistory.Count >
			MaxSamples)
		{
			rainEnergyPerParticleHistory.RemoveAt(0);
		}

		RecalculateRainEnergyScale();
	}

	private void UpdateCurrentValues(
		int activeParticleCount,
		double totalEnergy,
		float fps)
	{
		if (particleLabel != null)
		{
			particleLabel.Text =
				"Particles  " +
				activeParticleCount.ToString();
		}

		if (energyLabel != null)
		{
			energyLabel.Text =
				"Energy  " +
				totalEnergy.ToString("F2") +
				"/s";
		}

		if (fpsLabel != null)
		{
			fpsLabel.Text =
				"FPS  " +
				fps.ToString("F0") +
				"    History 600s";
		}
	}

	private void UpdateRainEnergyStatus()
	{
		if (rainEnergyStatusLabel == null)
		{
			return;
		}

		if (rainEnergyPerParticleHistory.Count <= 0)
		{
			int remaining =
				EnergyParticleWindowFrames -
				rainWindowFrameCounter;

			int totalRemaining =
				rainEnergyWarmupComplete
					? remaining
					: EnergyParticleWindowFrames +
						remaining;

			float seconds =
				totalRemaining /
				60.0f;

			rainEnergyStatusLabel.Text =
				"First measurement in " +
				seconds.ToString("F0") +
				"s";
			return;
		}

		double latest =
			rainEnergyPerParticleHistory[
				rainEnergyPerParticleHistory.Count - 1
			];

		rainEnergyStatusLabel.Text =
			"Latest  " +
			latest.ToString("F5") +
			" / active particle";
	}

	private void RecalculateEnergyScale()
	{
		double highest = 1.0;

		for (
			int i = 0;
			i < energyHistory.Count;
			i++)
		{
			if (energyHistory[i] > highest)
			{
				highest = energyHistory[i];
			}
		}

		maxEnergy = highest * 1.10;

		if (maxEnergy < 1.0)
		{
			maxEnergy = 1.0;
		}
	}

	private void RecalculateRainEnergyScale()
	{
		if (rainEnergyRainHistory.Count <= 0)
		{
			measuredRainMin = 0.0f;
			measuredRainMax = 1.0f;
			highestEnergyPerActiveParticle = 1.0;
			return;
		}

		float minRain = float.MaxValue;
		float maxRain = float.MinValue;
		double maxEnergyPerParticle = 0.0;

		int count = Math.Min(
			rainEnergyRainHistory.Count,
			rainEnergyPerParticleHistory.Count
		);

		for (
			int i = 0;
			i < count;
			i++)
		{
			float rain =
				rainEnergyRainHistory[i];

			double energy =
				rainEnergyPerParticleHistory[i];

			if (rain < minRain)
			{
				minRain = rain;
			}

			if (rain > maxRain)
			{
				maxRain = rain;
			}

			if (energy > maxEnergyPerParticle)
			{
				maxEnergyPerParticle = energy;
			}
		}

		measuredRainMin = minRain;
		measuredRainMax = maxRain;

		if (
			Mathf.Abs(
				measuredRainMax -
				measuredRainMin
			) < 0.0001f)
		{
			measuredRainMax =
				measuredRainMin + 1.0f;
		}

		highestEnergyPerActiveParticle =
			Math.Max(
				maxEnergyPerParticle,
				0.000000001
			);
	}

	public override void _Draw()
	{
		DrawExistingGraph();
		DrawRainEnergyGraph();
	}

	private void DrawExistingGraph()
	{
		DrawGraphBackground(0.0f);
		DrawGraphGrid(0.0f);
		DrawHistoryLines(0.0f);
	}

	private void DrawRainEnergyGraph()
	{
		float offsetY =
			GraphHeight +
			GraphGap;

		DrawGraphBackground(offsetY);
		DrawGraphGrid(offsetY);
		DrawRainEnergyPoints(offsetY);

		DrawRainEnergyAxisLabels(offsetY);
	}

	private void DrawGraphBackground(
		float offsetY)
	{
		DrawRect(
			new Rect2(
				0.0f,
				offsetY,
				GraphWidth,
				GraphHeight
			),
			new Color(
				0.32f,
				0.34f,
				0.37f,
				1.0f
			),
			true
		);

		DrawRect(
			new Rect2(
				0.0f,
				offsetY,
				GraphWidth,
				GraphHeight
			),
			new Color(
				0.55f,
				0.58f,
				0.62f,
				1.0f
			),
			false,
			2.0f
		);
	}

	private void DrawGraphGrid(
		float offsetY)
	{
		Color gridColor =
			new Color(
				0.75f,
				0.78f,
				0.82f,
				0.25f
			);

		Color axisColor =
			new Color(
				0.9f,
				0.92f,
				0.95f,
				0.65f
			);

		float plotWidth =
			PlotRight -
			PlotLeft;

		float plotHeight =
			PlotBottom -
			PlotTop;

		for (
			int i = 0;
			i <= 4;
			i++)
		{
			float y =
				offsetY +
				PlotTop +
				plotHeight *
				i /
				4.0f;

			DrawLine(
				new Vector2(
					PlotLeft,
					y
				),
				new Vector2(
					PlotRight,
					y
				),
				gridColor,
				1.0f
			);
		}

		for (
			int i = 0;
			i <= 6;
			i++)
		{
			float x =
				PlotLeft +
				plotWidth *
				i /
				6.0f;

			DrawLine(
				new Vector2(
					x,
					offsetY + PlotTop
				),
				new Vector2(
					x,
					offsetY + PlotBottom
				),
				gridColor,
				1.0f
			);
		}

		DrawLine(
			new Vector2(
				PlotLeft,
				offsetY + PlotTop
			),
			new Vector2(
				PlotLeft,
				offsetY + PlotBottom
			),
			axisColor,
			1.5f
		);

		DrawLine(
			new Vector2(
				PlotRight,
				offsetY + PlotTop
			),
			new Vector2(
				PlotRight,
				offsetY + PlotBottom
			),
			axisColor,
			1.5f
		);

		DrawLine(
			new Vector2(
				PlotLeft,
				offsetY + PlotBottom
			),
			new Vector2(
				PlotRight,
				offsetY + PlotBottom
			),
			axisColor,
			1.5f
		);
	}

	private void DrawHistoryLines(
		float offsetY)
	{
		int count =
			Math.Min(
				particleHistory.Count,
				energyHistory.Count
			);

		if (count < 2)
		{
			return;
		}

		float plotWidth =
			PlotRight -
			PlotLeft;

		float plotHeight =
			PlotBottom -
			PlotTop;

		Vector2[] particlePoints =
			new Vector2[count];

		int maxParticles = 1;

		for (
			int i = 0;
			i < count;
			i++)
		{
			if (
				particleHistory[i] >
				maxParticles)
			{
				maxParticles =
					particleHistory[i];
			}
		}

		for (
			int i = 0;
			i < count;
			i++)
		{
			float x =
				PlotLeft +
				plotWidth *
				i /
				Mathf.Max(
					count - 1.0f,
					1.0f
				);

			float normalized =
				Mathf.Clamp(
					(float)particleHistory[i] /
					maxParticles,
					0.0f,
					1.0f
				);

			float y =
				offsetY +
				PlotBottom -
				normalized *
				plotHeight;

			particlePoints[i] =
				new Vector2(
					x,
					y
				);
		}

		DrawPolyline(
			particlePoints,
			new Color(
				0.35f,
				0.75f,
				1.0f,
				1.0f
			),
			3.0f,
			true
		);

		Vector2[] energyPoints =
			new Vector2[count];

		for (
			int i = 0;
			i < count;
			i++)
		{
			float x =
				PlotLeft +
				plotWidth *
				i /
				Mathf.Max(
					count - 1.0f,
					1.0f
				);

			float normalized =
				Mathf.Clamp(
					(float)(
						energyHistory[i] /
						maxEnergy
					),
					0.0f,
					1.0f
				);

			float y =
				offsetY +
				PlotBottom -
				normalized *
				plotHeight;

			energyPoints[i] =
				new Vector2(
					x,
					y
				);
		}

		DrawPolyline(
			energyPoints,
			new Color(
				1.0f,
				0.75f,
				0.25f,
				1.0f
			),
			3.0f,
			true
		);

		int fpsCount =
			Math.Min(
				count,
				fpsHistory.Count
			);

		if (fpsCount < 2)
		{
			return;
		}

		Vector2[] fpsPoints =
			new Vector2[fpsCount];

		for (
			int i = 0;
			i < fpsCount;
			i++)
		{
			float x =
				PlotLeft +
				plotWidth *
				i /
				Mathf.Max(
					count - 1.0f,
					1.0f
				);

			float normalized =
				Mathf.Clamp(
					fpsHistory[i] /
					FpsGraphMax,
					0.0f,
					1.0f
				);

			float y =
				offsetY +
				PlotBottom -
				normalized *
				plotHeight;

			fpsPoints[i] =
				new Vector2(
					x,
					y
				);
		}

		DrawPolyline(
			fpsPoints,
			new Color(
				0.0f,
				0.0f,
				0.0f,
				1.0f
			),
			3.0f,
			true
		);
	}

	private void DrawRainEnergyPoints(
		float offsetY)
	{
		int count =
			Math.Min(
				rainEnergyRainHistory.Count,
				rainEnergyPerParticleHistory.Count
			);

		if (count <= 0)
		{
			return;
		}

		float plotWidth =
			PlotRight -
			PlotLeft;

		float plotHeight =
			PlotBottom -
			PlotTop;

		Vector2[] curvePoints =
			new Vector2[count];

		float rainRange =
			measuredRainMax -
			measuredRainMin;

		if (rainRange <= 0.000001f)
		{
			rainRange = 1.0f;
		}

		double maxEnergyValue =
			Math.Max(
				highestEnergyPerActiveParticle,
				0.000000001
			);

		for (
			int i = 0;
			i < count;
			i++)
		{
			float rain =
				rainEnergyRainHistory[i];

			double energy =
				rainEnergyPerParticleHistory[i];

			float normalizedRain =
				Mathf.Clamp(
					(rain - measuredRainMin) /
					rainRange,
					0.0f,
					1.0f
				);

			float normalizedEnergy =
				Mathf.Clamp(
					(float)(
						energy /
						maxEnergyValue
					),
					0.0f,
					1.0f
				);

			float x =
				PlotLeft +
				normalizedRain *
				plotWidth;

			float y =
				offsetY +
				PlotBottom -
				normalizedEnergy *
				plotHeight;

			curvePoints[i] =
				new Vector2(
					x,
					y
				);
		}

		if (count >= 2)
		{
			DrawPolyline(
				curvePoints,
				new Color(
					1.0f,
					1.0f,
					1.0f,
					1.0f
				),
				1.5f,
				true
			);
		}

		const float pointSize = 4.0f;

		for (
			int i = 0;
			i < count;
			i++)
		{
			Vector2 point =
				curvePoints[i];

			DrawRect(
				new Rect2(
					point.X -
						pointSize * 0.5f,
					point.Y -
						pointSize * 0.5f,
					pointSize,
					pointSize
				),
				new Color(
					1.0f,
					1.0f,
					1.0f,
					1.0f
				),
				true
			);
		}
	}

	private void DrawRainEnergyAxisLabels(
		float offsetY)
	{
		if (
			rainEnergyRainHistory.Count <= 0)
		{
			return;
		}

		DrawString(
			ThemeDB.FallbackFont,
			new Vector2(
				PlotLeft - 8.0f,
				offsetY + PlotBottom + 20.0f
			),
			measuredRainMin.ToString("F0") + "%",
			HorizontalAlignment.Right,
			48.0f,
			12,
			new Color(
				1.0f,
				1.0f,
				1.0f,
				0.85f
			)
		);

		DrawString(
			ThemeDB.FallbackFont,
			new Vector2(
				PlotRight - 48.0f,
				offsetY + PlotBottom + 20.0f
			),
			measuredRainMax.ToString("F0") + "%",
			HorizontalAlignment.Right,
			48.0f,
			12,
			new Color(
				1.0f,
				1.0f,
				1.0f,
				0.85f
			)
		);

		DrawString(
			ThemeDB.FallbackFont,
			new Vector2(
				PlotLeft - 8.0f,
				offsetY + PlotTop + 12.0f
			),
			highestEnergyPerActiveParticle.ToString("F5"),
			HorizontalAlignment.Right,
			48.0f,
			12,
			new Color(
				1.0f,
				1.0f,
				1.0f,
				0.85f
			)
		);
	}
}
