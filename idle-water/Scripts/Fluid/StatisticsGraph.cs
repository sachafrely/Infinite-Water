using System;
using System.Collections.Generic;
using Godot;

public partial class StatisticsGraph : Node2D
{
private const float GraphWidth = 560.0f;
private const float GraphHeight = 330.0f;


private const float PlotLeft = 58.0f;
private const float PlotTop = 48.0f;
private const float PlotRight = 530.0f;
private const float PlotBottom = 278.0f;

private const float SampleInterval = 0.10f;
private const int MaxSamples = 601;

private readonly List<float> rainHistory =
	new List<float>();

private readonly List<double> energyHistory =
	new List<double>();

private float sampleAccumulator = 0.0f;
private double maxEnergy = 1.0;

private Label titleLabel;
private Label rainLabel;
private Label energyLabel;
private Label fpsLabel;

public override void _Ready()
{
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

	fpsLabel =
		CreateLabel(
			"FPS  0",
			new Vector2(16.0f, 302.0f),
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

	AddChild(
		label
	);

	return label;
}

public void AddSample(
	float rainAmount,
	double energyPerSecond,
	float fps,
	float delta)
{
	sampleAccumulator +=
		Mathf.Max(
			delta,
			0.0f
		);

	if (
		sampleAccumulator <
		SampleInterval)
	{
		UpdateCurrentValues(
			rainAmount,
			energyPerSecond,
			fps
		);

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
			energyPerSecond
		)
	);

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

	UpdateCurrentValues(
		rainAmount,
		energyPerSecond,
		fps
	);

	QueueRedraw();
}

private void UpdateCurrentValues(
	float rainAmount,
	double energyPerSecond,
	float fps)
{
	if (
		rainLabel != null)
	{
		rainLabel.Text =
			"Rain  " +
			rainAmount.ToString("F0") +
			"%";
	}

	if (
		energyLabel != null)
	{
		energyLabel.Text =
			"Energy  " +
			energyPerSecond.ToString("F2") +
			"/s";
	}

	if (
		fpsLabel != null)
	{
		fpsLabel.Text =
			"FPS  " +
			fps.ToString("F0") +
			"    History 60s";
	}
}

private void RecalculateEnergyScale()
{
	double highest =
		1.0;

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

	if (
		maxEnergy <
		1.0)
	{
		maxEnergy =
			1.0;
	}
}

public override void _Draw()
{
	DrawRect(
		new Rect2(
			0.0f,
			0.0f,
			GraphWidth,
			GraphHeight
		),
		//BACKGROUND COLOR
		new Color(
			0.18f,
			0.18f,
			0.18f,
			0.92f
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
			0.35f,
			0.4f,
			0.5f,
			0.9f
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
			0.35f,
			0.4f,
			0.5f,
			0.22f
		);

	Color axisColor =
		new Color(
			0.65f,
			0.7f,
			0.78f,
			0.65f
		);

	float plotWidth =
		PlotRight - PlotLeft;

	float plotHeight =
		PlotBottom - PlotTop;

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
	if (
		rainHistory.Count <
		2)
	{
		return;
	}

	float plotWidth =
		PlotRight - PlotLeft;

	float plotHeight =
		PlotBottom - PlotTop;

	int count =
		Math.Min(
			rainHistory.Count,
			energyHistory.Count
		);

	if (
		count < 2)
	{
		return;
	}

	// Normal C# arrays are used here instead of
	// PackedVector2Array for compatibility with the
	// current Godot C# environment.

	Vector2[] rainPoints =
		new Vector2[count];

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

		float rainNormalized =
			Mathf.Clamp(
				rainHistory[i] /
				100.0f,
				0.0f,
				1.0f
			);

		float energyNormalized =
			Mathf.Clamp(
				(float)(
					energyHistory[i] /
					maxEnergy
				),
				0.0f,
				1.0f
			);

		float rainY =
			PlotBottom -
			rainNormalized *
			plotHeight;

		float energyY =
			PlotBottom -
			energyNormalized *
			plotHeight;

		rainPoints[i] =
			new Vector2(
				x,
				rainY
			);

		energyPoints[i] =
			new Vector2(
				x,
				energyY
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
}


}
