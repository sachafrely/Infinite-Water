using System;
using System.Collections.Generic;
using Godot;

public partial class StatisticsGraph : Control
{
	private const float GraphWidth = 720.0f;
	private const float GraphHeight = 330.0f;

	private const float PlotLeft = 58.0f;
	private const float PlotTop = 48.0f;
	private const float PlotRight = 660.0f;
	private const float PlotBottom = 278.0f;

	// One graph sample every 60 physics frames.
	private const int SampleFrames = 60;

	// 601 samples = approximately 600 seconds
	// when sampling once every 60 frames at 60 physics FPS.
	private const int MaxSamples = 601;

	// ============================================================
	// FPS graph
	// ============================================================

	// Fixed FPS scale:
	//
	// 60 FPS = top
	// 30 FPS  = middle
	// 0 FPS   = bottom
	//
	// Keeping this fixed makes FPS visually meaningful and
	// prevents the graph from constantly rescaling.
	private const float FpsGraphMax = 60.0f;

	private readonly List<int> particleHistory =
		new List<int>();

	private readonly List<double> energyHistory =
		new List<double>();

	private readonly List<float> fpsHistory =
		new List<float>();

	private int sampleFrameCounter = 0;
	private double maxEnergy = 1.0;

	private Label titleLabel;
	private Label particleLabel;
	private Label energyLabel;
	private Label fpsLabel;


	public override void _Ready()
	{
		CustomMinimumSize =
			new Vector2(
				GraphWidth,
				GraphHeight
			);

		MouseFilter =
			Control.MouseFilterEnum.Ignore;

		ZIndex = 100;

		titleLabel =
			CreateLabel(
				"Particles & Energy",
				new Vector2(16.0f, 8.0f),
				22
			);

		particleLabel =
			CreateLabel(
				"Particles  0",
				new Vector2(350.0f, 10.0f),
				16
			);

		particleLabel.AddThemeColorOverride(
			"font_color",
			new Color(
				0.35f,
				0.75f,
				1.0f,
				1.0f
			)
		);

		energyLabel =
			CreateLabel(
				"Energy  0.00/s",
				new Vector2(350.0f, 30.0f),
				16
			);

		energyLabel.AddThemeColorOverride(
			"font_color",
			new Color(
				1.0f,
				0.75f,
				0.25f,
				1.0f
			)
		);


		fpsLabel =
			CreateLabel(
				"FPS  0    History 600s",
				new Vector2(
					16.0f,
					302.0f
				),
				16
			);

		// FPS text is black as well.
		fpsLabel.AddThemeColorOverride(
			"font_color",
			new Color(
				0.0f,
				0.0f,
				0.0f,
				1.0f
			)
		);

		QueueRedraw();
	}

	private Label CreateLabel(
		string text,
		Vector2 position,
		int fontSize)
	{
		Label label =
			new Label();

		label.Text =
			text;

		label.Position =
			position;

		label.AddThemeFontSizeOverride(
			"font_size",
			fontSize
		);

		label.AddThemeColorOverride(
			"font_shadow_color",
			new Color(
				0.0f,
				0.0f,
				0.0f,
				0.8f
			)
		);

		label.AddThemeConstantOverride(
			"shadow_offset_x",
			2
		);

		label.AddThemeConstantOverride(
			"shadow_offset_y",
			2
		);

		label.MouseFilter =
			Control.MouseFilterEnum.Ignore;

		AddChild(label);

		return label;
	}

	public void AddSample(
		int activeParticleCount,
		double totalEnergy,
		float fps,
		float delta)
	{
		sampleFrameCounter++;

		UpdateCurrentValues(
			activeParticleCount,
			totalEnergy,
			fps
		);

		if (
			sampleFrameCounter <
			SampleFrames)
		{
			return;
		}

		sampleFrameCounter -=
			SampleFrames;

		// --------------------------------------------------------
		// Particles
		// --------------------------------------------------------

		particleHistory.Add(
			Math.Max(
				0,
				activeParticleCount
			)
		);

		// --------------------------------------------------------
		// Energy
		// --------------------------------------------------------

		energyHistory.Add(
			Math.Max(
				0.0,
				totalEnergy
			)
		);

		// --------------------------------------------------------
		// FPS
		// --------------------------------------------------------

		fpsHistory.Add(
			Mathf.Clamp(
				fps,
				0.0f,
				FpsGraphMax
			)
		);

		// --------------------------------------------------------
		// Limit history
		// --------------------------------------------------------

		while (
			particleHistory.Count >
			MaxSamples)
		{
			particleHistory.RemoveAt(0);
		}

		while (
			energyHistory.Count >
			MaxSamples)
		{
			energyHistory.RemoveAt(0);
		}

		while (
			fpsHistory.Count >
			MaxSamples)
		{
			fpsHistory.RemoveAt(0);
		}

		RecalculateEnergyScale();

		QueueRedraw();
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

	private void RecalculateEnergyScale()
	{
		double highest = 1.0;

		for (
			int i = 0;
			i < energyHistory.Count;
			i++)
		{
			if (
				energyHistory[i] >
				highest)
			{
				highest =
					energyHistory[i];
			}
		}


		maxEnergy =
			highest * 1.10;

		if (maxEnergy < 1.0)
		{
			maxEnergy = 1.0;
		}
	}

	public override void _Draw()
	{
		// --------------------------------------------------------
		// Background
		// --------------------------------------------------------

		DrawRect(
			new Rect2(
				0.0f,
				0.0f,
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
				0.0f,
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

		DrawGrid();
		DrawHistoryLines();
	}

	private void DrawGrid()
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
					PlotTop
				),
				new Vector2(
					x,
					PlotBottom
				),
				gridColor,
				1.0f
			);
		}

		DrawLine(
			new Vector2(
				PlotLeft,
				PlotTop
			),
			new Vector2(
				PlotLeft,
				PlotBottom
			),
			axisColor,
			1.5f
		);

		DrawLine(
			new Vector2(
				PlotRight,
				PlotTop
			),
			new Vector2(
				PlotRight,
				PlotBottom
			),
			axisColor,
			1.5f
		);

		DrawLine(
			new Vector2(
				PlotLeft,
				PlotBottom
			),
			new Vector2(
				PlotRight,
				PlotBottom
			),
			axisColor,
			1.5f
		);
	}

	private void DrawHistoryLines()
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

		// --------------------------------------------------------
		// Active particles
		// --------------------------------------------------------

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

		// --------------------------------------------------------
		// Total energy
		// --------------------------------------------------------

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

		// --------------------------------------------------------
		// FPS
		// --------------------------------------------------------
		//
		// Fixed 0-120 FPS scale:
		//
		// 120 FPS = top
		//  60 FPS = middle
		//   0 FPS = bottom
		//
		// Black line.
		// --------------------------------------------------------

		int fpsCount =
			Math.Min(
				count,
				fpsHistory.Count
			);

		if (fpsCount >= 2)
		{
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

	}
}
