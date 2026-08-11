using System;
using System.Collections.Generic;
using Godot;

public partial class StatisticsGraph : Control
{
	private const float GraphWidth = 560.0f;
	private const float GraphHeight = 330.0f;

	private const float PlotLeft = 58.0f;
	private const float PlotTop = 48.0f;
	private const float PlotRight = 530.0f;
	private const float PlotBottom = 278.0f;

	private const float SampleInterval = 0.10f;
	private const int MaxSamples = 1201;
	private const int MaxWheels = 4;

	private readonly List<float> rainHistory =
		new List<float>();

	private readonly List<double> energyHistory =
		new List<double>();

	private readonly List<double>[] wheelEnergyHistory =
	{
		new List<double>(),
		new List<double>(),
		new List<double>(),
		new List<double>()
	};

	private float sampleAccumulator = 0.0f;
	private double maxEnergy = 1.0;

	private Label titleLabel;
	private Label rainLabel;
	private Label energyLabel;
	private Label fpsLabel;

	private Label[] wheelLabels =
		new Label[MaxWheels];

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
				"Rain & Energy",
				new Vector2(16.0f, 8.0f),
				22
			);

		rainLabel =
			CreateLabel(
				"Rain  0%",
				new Vector2(350.0f, 10.0f),
				16
			);

		rainLabel.AddThemeColorOverride(
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

		for (
			int i = 0;
			i < MaxWheels;
			i++)
		{
			float y =
				8.0f +
				i * 20.0f;

			wheelLabels[i] =
				CreateLabel(
					"Wheel " +
					(i + 1) +
					"  0.00/s",
					new Vector2(
						150.0f,
						y
					),
					14
				);

			wheelLabels[i].Visible =
				false;

			wheelLabels[i].AddThemeColorOverride(
				"font_color",
				new Color(
					1.0f,
					1.0f,
					1.0f,
					1.0f
				)
			);
		}

		fpsLabel =
			CreateLabel(
				"FPS  0    History 60s",
				new Vector2(
					16.0f,
					302.0f
				),
				16
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
		float rainAmount,
		double totalEnergy,
		float fps,
		float delta,
		double[] wheelEnergyPerSecond)
	{
		sampleAccumulator +=
			Mathf.Max(
				delta,
				0.0f
			);

		UpdateCurrentValues(
			rainAmount,
			totalEnergy,
			fps,
			wheelEnergyPerSecond
		);

		if (
			sampleAccumulator <
			SampleInterval)
		{
			return;
		}

		sampleAccumulator -=
			SampleInterval;

		rainHistory.Add(
			Mathf.Clamp(
				rainAmount,
				0.0f,
				100.0f
			)
		);

		energyHistory.Add(
			Math.Max(
				0.0,
				totalEnergy
			)
		);

		for (
			int wheel = 0;
			wheel < MaxWheels;
			wheel++)
		{
			double value = 0.0;

			if (
				wheelEnergyPerSecond != null &&
				wheel < wheelEnergyPerSecond.Length)
			{
				value =
					Math.Max(
						0.0,
						wheelEnergyPerSecond[wheel]
					);
			}

			wheelEnergyHistory[wheel].Add(
				value
			);

			while (
				wheelEnergyHistory[wheel].Count >
				MaxSamples)
			{
				wheelEnergyHistory[wheel].RemoveAt(0);
			}
		}

		while (
			rainHistory.Count >
			MaxSamples)
		{
			rainHistory.RemoveAt(0);
		}

		while (
			energyHistory.Count >
			MaxSamples)
		{
			energyHistory.RemoveAt(0);
		}

		RecalculateEnergyScale();

		QueueRedraw();
	}

	private void UpdateCurrentValues(
		float rainAmount,
		double totalEnergy,
		float fps,
		double[] wheelEnergyPerSecond)
	{
		if (rainLabel != null)
		{
			rainLabel.Text =
				"Rain  " +
				rainAmount.ToString("F0") +
				"%";
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
				"    History 60s";
		}

		for (
			int i = 0;
			i < MaxWheels;
			i++)
		{
			if (wheelLabels[i] == null)
			{
				continue;
			}

			bool exists =
				wheelEnergyPerSecond != null &&
				i < wheelEnergyPerSecond.Length;

			wheelLabels[i].Visible =
				exists;

			if (exists)
			{
				wheelLabels[i].Text =
					"Wheel " +
					(i + 1) +
					"  " +
					wheelEnergyPerSecond[i].ToString("F2") +
					"/s";
			}
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

		for (
			int wheel = 0;
			wheel < MaxWheels;
			wheel++)
		{
			for (
				int i = 0;
				i < wheelEnergyHistory[wheel].Count;
				i++)
			{
				if (
					wheelEnergyHistory[wheel][i] >
					highest)
				{
					highest =
						wheelEnergyHistory[wheel][i];
				}
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
				rainHistory.Count,
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
		// Rain
		// --------------------------------------------------------

		Vector2[] rainPoints =
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
					rainHistory[i] /
					100.0f,
					0.0f,
					1.0f
				);

			float y =
				PlotBottom -
				normalized *
				plotHeight;

			rainPoints[i] =
				new Vector2(
					x,
					y
				);
		}

		DrawPolyline(
			rainPoints,
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
		// Individual wheels
		// --------------------------------------------------------

		for (
			int wheel = 0;
			wheel < MaxWheels;
			wheel++)
		{
			int wheelCount =
				Math.Min(
					count,
					wheelEnergyHistory[wheel].Count
				);

			if (wheelCount < 2)
			{
				continue;
			}

			Vector2[] wheelPoints =
				new Vector2[wheelCount];

			for (
				int i = 0;
				i < wheelCount;
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
							wheelEnergyHistory[wheel][i] /
							maxEnergy
						),
						0.0f,
						1.0f
					);

				float y =
					PlotBottom -
					normalized *
					plotHeight;

				wheelPoints[i] =
					new Vector2(
						x,
						y
					);
			}

			DrawPolyline(
				wheelPoints,
				new Color(
					1.0f,
					1.0f,
					1.0f,
					0.85f
				),
				1.5f,
				true
			);
		}
	}
}
