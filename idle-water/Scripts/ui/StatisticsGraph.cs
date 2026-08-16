using Godot;
using System;
using System.Collections.Generic;

public partial class StatisticsGraph : Control
{
	// ============================================================
	// TOP GRAPH DATA
	// ============================================================

	private readonly List<GraphSample> samples =
		new List<GraphSample>(4096);

	// ============================================================
	// BOTTOM GRAPH DATA
	// ============================================================

	private readonly List<RainEnergySample> rainEnergySamples =
		new List<RainEnergySample>();

	// ============================================================
	// GENERAL SETTINGS
	// ============================================================

	private const int MaxRainEnergySamples = 120;

	private const float GraphSpacing = 5.0f;

	private const float GraphMarginLeft = 55.0f;
	private const float GraphMarginRight = 35.0f;

	// Increased by 10 pixels.
	private const float GraphMarginTop = 35.0f;

	private const float GraphMarginBottom = 5.0f;

	private const float SecondGraphMarginLeft = 55.0f;
	private const float SecondGraphMarginRight = 35.0f;

	private const float SecondGraphMarginTop = 25.0f;

	// Do not reserve unnecessary space underneath
	// the second graph.
	private const float SecondGraphMarginBottom = 10.0f;

	private const float GraphBorderWidth = 2.0f;
	private const float GridLineWidth = 1.0f;

	// Bottom graph dot radius.
	private const float BottomPointRadius = 1.8f;

	// ============================================================
	// TOP GRAPH HISTORY
	// ============================================================

	private const float MaxTopGraphSeconds = 600.0f;

	private const int AssumedSamplesPerSecond = 60;

	private const int MaxTopGraphSamples =
		(int)(MaxTopGraphSeconds * AssumedSamplesPerSecond);

	// ============================================================
	// GRAPH TITLES
	// ============================================================

	private const string ExistingGraphTitle =
		"Statistics";

	private const string ExistingGraphXAxis =
		"";

	private const string ExistingGraphYAxis =
		"Value";

	private const string RainEnergyGraphTitle =
		"Energy Per Particle";

	private const string RainEnergyGraphXAxis =
		"Particles";

	private const string RainEnergyGraphYAxis =
		"Energy";

	// ============================================================
	// TOP GRAPH COLORS
	// ============================================================

	private readonly Color particlesColor =
		new Color(0.05f, 0.40f, 0.75f);

	private readonly Color energyColor =
		new Color(1.0f, 0.75f, 0.25f);

	private readonly Color fpsColor =
		new Color(0.4f, 0.4f, 0.4f);

	// ============================================================
	// DATA STRUCTURES
	// ============================================================

	private struct GraphSample
	{
		public float Particles;
		public float EnergyPerSecond;
		public float Fps;

		public GraphSample(
			float particles,
			float energyPerSecond,
			float fps)
		{
			Particles = particles;
			EnergyPerSecond = energyPerSecond;
			Fps = fps;
		}
	}

	private struct RainEnergySample
	{
		public float AverageRain;
		public float AverageEnergy;
		public float AverageParticles;

		public RainEnergySample(
			float averageRain,
			float averageEnergy,
			float averageParticles)
		{
			AverageRain = averageRain;
			AverageEnergy = averageEnergy;
			AverageParticles = averageParticles;
		}
	}

	// ============================================================
	// TOP GRAPH SCALING
	// ============================================================

	private float existingMaxParticles = 100.0f;
	private float existingMaxEnergy = 1.0f;
	private float existingMaxFps = 60.0f;

	// ============================================================
	// BOTTOM GRAPH SCALING
	// ============================================================

	private float particleEnergyMaxParticles = 100.0f;
	private float particleEnergyMaxEnergy = 1.0f;

	// ============================================================
	// TOP GRAPH CACHE
	// ============================================================

	private Vector2[] cachedParticlesPoints =
		Array.Empty<Vector2>();

	private Vector2[] cachedEnergyPoints =
		Array.Empty<Vector2>();

	private Vector2[] cachedFpsPoints =
		Array.Empty<Vector2>();

	private bool topGraphCacheDirty = true;

	private int lastCachedWidth = -1;

	private int samplesSinceCacheBuild = 0;

	private const int CacheRebuildInterval = 30;

	// ============================================================
	// INITIALIZATION
	// ============================================================

	public override void _Ready()
	{
		MouseFilter =
			Control.MouseFilterEnum.Ignore;

		QueueRedraw();
	}

	// ============================================================
	// TOP GRAPH API
	// ============================================================

	public void AddSample(
		int activeParticles,
		double energyPerSecond,
		float fps,
		float delta)
	{
		samples.Add(
			new GraphSample(
				activeParticles,
				(float)energyPerSecond,
				fps
			)
		);

		while (
			samples.Count >
			MaxTopGraphSamples)
		{
			samples.RemoveAt(0);
		}

		UpdateExistingGraphScale(
			activeParticles,
			(float)energyPerSecond,
			fps
		);

		samplesSinceCacheBuild++;

		if (
			samplesSinceCacheBuild >=
			CacheRebuildInterval)
		{
			topGraphCacheDirty = true;
			samplesSinceCacheBuild = 0;
		}

		QueueRedraw();
	}

	// ============================================================
	// BOTTOM GRAPH API
	// ============================================================

	public void AddRainEnergySample(
		float averageRain,
		float averageEnergy,
		float averageParticles)
	{
		RainEnergySample sample =
			new RainEnergySample(
				averageRain,
				averageEnergy,
				averageParticles
			);

		rainEnergySamples.Add(sample);

		while (
			rainEnergySamples.Count >
			MaxRainEnergySamples)
		{
			rainEnergySamples.RemoveAt(0);
		}

		RecalculateParticleEnergyGraphScale();

		QueueRedraw();

		GD.Print(
			"Particle/Energy Graph Point: " +
			"Particles=" +
			averageParticles.ToString("F1") +
			" Energy=" +
			averageEnergy.ToString("F4") +
			" Rain=" +
			averageRain.ToString("F1") +
			"%"
		);
	}

	// ============================================================
	// TOP GRAPH SCALE
	// ============================================================

	private void UpdateExistingGraphScale(
		float particles,
		float energy,
		float fps)
	{
		bool scaleChanged = false;

		if (
			particles >
			existingMaxParticles)
		{
			existingMaxParticles =
				particles;

			scaleChanged = true;
		}

		if (
			energy >
			existingMaxEnergy)
		{
			existingMaxEnergy =
				energy;

			scaleChanged = true;
		}

		if (
			fps >
			existingMaxFps)
		{
			existingMaxFps =
				fps;

			scaleChanged = true;
		}

		if (scaleChanged)
		{
			topGraphCacheDirty = true;
		}

		existingMaxParticles =
			Mathf.Max(
				existingMaxParticles,
				1.0f
			);

		existingMaxEnergy =
			Mathf.Max(
				existingMaxEnergy,
				0.001f
			);

		existingMaxFps =
			Mathf.Max(
				existingMaxFps,
				1.0f
			);
	}

	// ============================================================
	// BOTTOM GRAPH SCALE
	// ============================================================

	private void RecalculateParticleEnergyGraphScale()
	{
		particleEnergyMaxParticles =
			1.0f;

		particleEnergyMaxEnergy =
			0.001f;

		for (
			int i = 0;
			i < rainEnergySamples.Count;
			i++)
		{
			RainEnergySample sample =
				rainEnergySamples[i];

			if (
				sample.AverageParticles >
				particleEnergyMaxParticles)
			{
				particleEnergyMaxParticles =
					sample.AverageParticles;
			}

			if (
				sample.AverageEnergy >
				particleEnergyMaxEnergy)
			{
				particleEnergyMaxEnergy =
					sample.AverageEnergy;
			}
		}

		particleEnergyMaxParticles =
			Mathf.Max(
				particleEnergyMaxParticles * 1.15f,
				1.0f
			);

		particleEnergyMaxEnergy =
			Mathf.Max(
				particleEnergyMaxEnergy * 1.15f,
				0.001f
			);
	}

	// ============================================================
	// DRAW
	// ============================================================

	public override void _Draw()
	{
		DrawExistingGraph();

		DrawRainEnergyGraph();
	}

	// ============================================================
	// TOP GRAPH
	// ============================================================

	private void DrawExistingGraph()
	{
		Rect2 graphRect =
			GetExistingGraphRect();

		if (
			graphRect.Size.X <= 0.0f ||
			graphRect.Size.Y <= 0.0f)
		{
			return;
		}

		DrawRect(
			graphRect,
			new Color(
				0.04f,
				0.04f,
				0.05f,
				0.92f
			),
			true
		);

		DrawExistingGrid(graphRect);

		DrawRect(
			graphRect,
			new Color(
				0.75f,
				0.75f,
				0.75f,
				0.9f
			),
			false,
			GraphBorderWidth
		);

		DrawString(
			ThemeDB.FallbackFont,
			new Vector2(
				graphRect.Position.X,
				graphRect.Position.Y - 7.0f
			),
			ExistingGraphTitle,
			HorizontalAlignment.Left,
			-1,
			22,
			new Color(
				0.9f,
				0.9f,
				0.9f
			)
		);

		DrawTopGraphLegend(graphRect);

		DrawString(
			ThemeDB.FallbackFont,
			new Vector2(
				graphRect.Position.X +
				graphRect.Size.X * 0.5f -
				15.0f,
				graphRect.Position.Y +
				graphRect.Size.Y +
				27.0f
			),
			ExistingGraphXAxis,
			HorizontalAlignment.Left,
			-1,
			18,
			new Color(
				0.75f,
				0.75f,
				0.75f
			)
		);

		DrawString(
			ThemeDB.FallbackFont,
			new Vector2(
				graphRect.Position.X - 45.0f,
				graphRect.Position.Y +
				graphRect.Size.Y * 0.5f
			),
			ExistingGraphYAxis,
			HorizontalAlignment.Left,
			-1,
			20,
			new Color(
				0.75f,
				0.75f,
				0.75f
			)
		);

		DrawTopTimeLabels(graphRect);

		if (samples.Count == 0)
		{
			DrawString(
				ThemeDB.FallbackFont,
				new Vector2(
					graphRect.Position.X +
					graphRect.Size.X * 0.5f -
					55.0f,
					graphRect.Position.Y +
					graphRect.Size.Y * 0.5f
				),
				"Collecting data...",
				HorizontalAlignment.Left,
				-1,
				22,
				new Color(
					0.55f,
					0.55f,
					0.55f
				)
			);

			return;
		}

		int graphWidth =
			Mathf.Max(
				(int)graphRect.Size.X,
				1
			);

		if (
			lastCachedWidth !=
			graphWidth)
		{
			topGraphCacheDirty = true;
		}

		if (topGraphCacheDirty)
		{
			BuildTopGraphCache(graphRect);
		}

		if (
			cachedParticlesPoints.Length >= 2)
		{
			DrawPolyline(
				cachedParticlesPoints,
				particlesColor,
				2.0f,
				true
			);
		}

		if (
			cachedEnergyPoints.Length >= 2)
		{
			DrawPolyline(
				cachedEnergyPoints,
				energyColor,
				2.0f,
				true
			);
		}

		if (
			cachedFpsPoints.Length >= 2)
		{
			DrawPolyline(
				cachedFpsPoints,
				fpsColor,
				2.0f,
				true
			);
		}
	}

	// ============================================================
	// TOP GRAPH LEGEND
	// ============================================================

	private void DrawTopGraphLegend(
		Rect2 graphRect)
	{
		float x =
			graphRect.End.X -
			230.0f;

		float y =
			graphRect.Position.Y -
			7.0f;

		DrawCircle(
			new Vector2(x, y - 4.0f),
			3.0f,
			particlesColor
		);

		DrawString(
			ThemeDB.FallbackFont,
			new Vector2(
				x + 30.0f,
				y
			),
			"Particles",
			HorizontalAlignment.Left,
			-1,
			22,
			particlesColor
		);

		x += 75.0f;

		DrawCircle(
			new Vector2(x, y - 4.0f),
			3.0f,
			energyColor
		);

		DrawString(
			ThemeDB.FallbackFont,
			new Vector2(
				x + 8.0f,
				y
			),
			"Energy",
			HorizontalAlignment.Left,
			-1,
			20,
			energyColor
		);

		x += 65.0f;

		DrawCircle(
			new Vector2(x, y - 4.0f),
			3.0f,
			fpsColor
		);

		DrawString(
			ThemeDB.FallbackFont,
			new Vector2(
				x + 8.0f,
				y
			),
			"FPS",
			HorizontalAlignment.Left,
			-1,
			20,
			fpsColor
		);
	}

	// ============================================================
	// TOP TIME LABELS
	// ============================================================

	private void DrawTopTimeLabels(
		Rect2 graphRect)
	{
		float totalSeconds =
			Mathf.Min(
				GetTotalRunSeconds(),
				MaxTopGraphSeconds
			);

		if (totalSeconds <= 0.0f)
		{
			return;
		}

		DrawString(
			ThemeDB.FallbackFont,
			new Vector2(
				graphRect.Position.X - 5.0f,
				graphRect.End.Y + 14.0f
			),
			"",
			HorizontalAlignment.Left,
			-1,
			22,
			new Color(
				0.65f,
				0.65f,
				0.65f
			)
		);

		DrawString(
			ThemeDB.FallbackFont,
			new Vector2(
				graphRect.Position.X +
				graphRect.Size.X * 0.5f -
				10.0f,
				graphRect.End.Y + 14.0f
			),
			FormatSeconds(
				totalSeconds * 0.5f
			),
			HorizontalAlignment.Left,
			-1,
			22,
			new Color(
				0.65f,
				0.65f,
				0.65f
			)
		);

		string endText =
			FormatSeconds(totalSeconds);

		DrawString(
			ThemeDB.FallbackFont,
			new Vector2(
				graphRect.End.X - 30.0f,
				graphRect.End.Y + 14.0f
			),
			endText,
			HorizontalAlignment.Left,
			-1,
			22,
			new Color(
				0.65f,
				0.65f,
				0.65f
			)
		);
	}

	// ============================================================
	// TOP GRAPH CACHE
	// ============================================================

	private void BuildTopGraphCache(
		Rect2 graphRect)
	{
		if (samples.Count == 0)
		{
			cachedParticlesPoints =
				Array.Empty<Vector2>();

			cachedEnergyPoints =
				Array.Empty<Vector2>();

			cachedFpsPoints =
				Array.Empty<Vector2>();

			topGraphCacheDirty = false;

			return;
		}

		int width =
			Mathf.Max(
				(int)graphRect.Size.X,
				2
			);

		int sampleCount =
			samples.Count;

		cachedParticlesPoints =
			BuildCompressedSeries(
				graphRect,
				width,
				sampleCount,
				0
			);

		cachedEnergyPoints =
			BuildCompressedSeries(
				graphRect,
				width,
				sampleCount,
				1
			);

		cachedFpsPoints =
			BuildCompressedSeries(
				graphRect,
				width,
				sampleCount,
				2
			);

		lastCachedWidth =
			width;

		topGraphCacheDirty = false;
	}

	// ============================================================
	// COMPRESSED TOP SERIES
	// ============================================================

	private Vector2[] BuildCompressedSeries(
		Rect2 graphRect,
		int width,
		int sampleCount,
		int series)
	{
		if (sampleCount <= 0)
		{
			return Array.Empty<Vector2>();
		}

		int pointCount =
			Math.Min(
				width,
				sampleCount
			);

		Vector2[] points =
			new Vector2[pointCount];

		for (
			int pixel = 0;
			pixel < pointCount;
			pixel++)
		{
			int startIndex =
				(int)(
					(double)pixel *
					sampleCount /
					pointCount
				);

			int endIndex =
				(int)(
					(double)(pixel + 1) *
					sampleCount /
					pointCount
				) - 1;

			if (endIndex < startIndex)
			{
				endIndex =
					startIndex;
			}

			if (endIndex >= sampleCount)
			{
				endIndex =
					sampleCount - 1;
			}

			double sum = 0.0;

			int count =
				endIndex -
				startIndex +
				1;

			for (
				int i = startIndex;
				i <= endIndex;
				i++)
			{
				GraphSample sample =
					samples[i];

				if (series == 0)
				{
					sum += sample.Particles;
				}
				else if (series == 1)
				{
					sum += sample.EnergyPerSecond;
				}
				else
				{
					sum += sample.Fps;
				}
			}

			float value =
				(float)(
					sum /
					Math.Max(
						count,
						1
					)
				);

			float maxValue;

			if (series == 0)
			{
				maxValue =
					existingMaxParticles;
			}
			else if (series == 1)
			{
				maxValue =
					existingMaxEnergy;
			}
			else
			{
				maxValue =
					existingMaxFps;
			}

			float normalized =
				Mathf.Clamp(
					value / maxValue,
					0.0f,
					1.0f
				);

			float x;

			if (pointCount == 1)
			{
				x =
					graphRect.Position.X +
					graphRect.Size.X * 0.5f;
			}
			else
			{
				x =
					graphRect.Position.X +
					(float)pixel /
					(pointCount - 1) *
					graphRect.Size.X;
			}

			float y =
				graphRect.Position.Y +
				graphRect.Size.Y -
				normalized *
				graphRect.Size.Y;

			points[pixel] =
				new Vector2(
					x,
					y
				);
		}

		return points;
	}

	// ============================================================
	// TOTAL RUN TIME
	// ============================================================

	private float GetTotalRunSeconds()
	{
		return samples.Count /
			(float)AssumedSamplesPerSecond;
	}

	// ============================================================
	// FORMAT TIME
	// ============================================================

	private string FormatSeconds(
		float seconds)
	{
		if (seconds < 60.0f)
		{
			return Mathf.RoundToInt(seconds) + "s";
		}

		int total =
			Mathf.RoundToInt(seconds);

		int minutes =
			total / 60;

		int remainingSeconds =
			total % 60;

		return minutes +
			":" +
			remainingSeconds.ToString("00");
	}

	// ============================================================
	// TOP GRID
	// ============================================================

	private void DrawExistingGrid(
		Rect2 graphRect)
	{
		const int horizontalLines = 5;
		const int verticalLines = 5;

		for (
			int i = 1;
			i < horizontalLines;
			i++)
		{
			float y =
				graphRect.Position.Y +
				graphRect.Size.Y *
				i /
				horizontalLines;

			DrawLine(
				new Vector2(
					graphRect.Position.X,
					y
				),
				new Vector2(
					graphRect.End.X,
					y
				),
				new Color(
					0.18f,
					0.18f,
					0.20f,
					0.8f
				),
				GridLineWidth
			);
		}

		for (
			int i = 1;
			i < verticalLines;
			i++)
		{
			float x =
				graphRect.Position.X +
				graphRect.Size.X *
				i /
				verticalLines;

			DrawLine(
				new Vector2(
					x,
					graphRect.Position.Y
				),
				new Vector2(
					x,
					graphRect.End.Y
				),
				new Color(
					0.18f,
					0.18f,
					0.20f,
					0.8f
				),
				GridLineWidth
			);
		}
	}

	// ============================================================
	// BOTTOM GRAPH
	// ============================================================

	private void DrawRainEnergyGraph()
	{
		Rect2 graphRect =
			GetRainEnergyGraphRect();

		if (
			graphRect.Size.X <= 0.0f ||
			graphRect.Size.Y <= 0.0f)
		{
			return;
		}

		DrawRect(
			graphRect,
			new Color(
				0.04f,
				0.04f,
				0.05f,
				0.92f
			),
			true
		);

		DrawRainEnergyGrid(graphRect);

		DrawRect(
			graphRect,
			new Color(
				0.75f,
				0.75f,
				0.75f,
				0.9f
			),
			false,
			GraphBorderWidth
		);

		DrawString(
			ThemeDB.FallbackFont,
			new Vector2(
				graphRect.Position.X,
				graphRect.Position.Y - 7.0f
			),
			RainEnergyGraphTitle,
			HorizontalAlignment.Left,
			-1,
			20,
			new Color(
				0.9f,
				0.9f,
				0.9f
			)
		);

		DrawString(
			ThemeDB.FallbackFont,
			new Vector2(
				graphRect.Position.X +
				graphRect.Size.X * 0.5f -
				45.0f,
				graphRect.Position.Y +
				graphRect.Size.Y +
				27.0f
			),
			RainEnergyGraphXAxis,
			HorizontalAlignment.Left,
			-1,
			20,
			new Color(
				0.75f,
				0.75f,
				0.75f
			)
		);

		DrawString(
			ThemeDB.FallbackFont,
			new Vector2(
				graphRect.Position.X - 45.0f,
				graphRect.Position.Y +
				graphRect.Size.Y * 0.5f
			),
			RainEnergyGraphYAxis,
			HorizontalAlignment.Left,
			-1,
			22,
			new Color(
				0.75f,
				0.75f,
				0.75f
			)
		);

		DrawString(
			ThemeDB.FallbackFont,
			new Vector2(
				graphRect.Position.X - 5.0f,
				graphRect.End.Y + 14.0f
			),
			"0",
			HorizontalAlignment.Left,
			-1,
			18,
			new Color(
				0.65f,
				0.65f,
				0.65f
			)
		);

		DrawString(
			ThemeDB.FallbackFont,
			new Vector2(
				graphRect.Position.X +
				graphRect.Size.X * 0.5f -
				15.0f,
				graphRect.End.Y + 14.0f
			),
			FormatParticleCount(
				particleEnergyMaxParticles * 0.5f
			),
			HorizontalAlignment.Left,
			-1,
			18,
			new Color(
				0.65f,
				0.65f,
				0.65f
			)
		);

		DrawString(
			ThemeDB.FallbackFont,
			new Vector2(
				graphRect.End.X - 30.0f,
				graphRect.End.Y + 14.0f
			),
			FormatParticleCount(
				particleEnergyMaxParticles
			),
			HorizontalAlignment.Left,
			-1,
			18,
			new Color(
				0.65f,
				0.65f,
				0.65f
			)
		);

		if (rainEnergySamples.Count < 1)
		{
			DrawString(
				ThemeDB.FallbackFont,
				new Vector2(
					graphRect.Position.X +
					graphRect.Size.X * 0.5f -
					55.0f,
					graphRect.Position.Y +
					graphRect.Size.Y * 0.5f
				),
				"Collecting data...",
				HorizontalAlignment.Left,
				-1,
				22,
				new Color(
					0.55f,
					0.55f,
					0.55f
				)
			);

			return;
		}

		for (
			int i = 0;
			i < rainEnergySamples.Count;
			i++)
		{
			RainEnergySample sample =
				rainEnergySamples[i];

			float normalizedX =
				Mathf.Clamp(
					sample.AverageParticles /
					particleEnergyMaxParticles,
					0.0f,
					1.0f
				);

			float normalizedY =
				Mathf.Clamp(
					sample.AverageEnergy /
					particleEnergyMaxEnergy,
					0.0f,
					1.0f
				);

			Vector2 point =
				new Vector2(
					graphRect.Position.X +
					normalizedX *
					graphRect.Size.X,

					graphRect.Position.Y +
					graphRect.Size.Y -
					normalizedY *
					graphRect.Size.Y
				);

			DrawCircle(
				point,
				BottomPointRadius,
				new Color(
					1.0f,
					0.75f,
					0.25f
				)
			);
		}
	}

	// ============================================================
	// BOTTOM GRID
	// ============================================================

	private void DrawRainEnergyGrid(
		Rect2 graphRect)
	{
		const int horizontalLines = 5;
		const int verticalLines = 5;

		for (
			int i = 1;
			i < horizontalLines;
			i++)
		{
			float y =
				graphRect.Position.Y +
				graphRect.Size.Y *
				i /
				horizontalLines;

			DrawLine(
				new Vector2(
					graphRect.Position.X,
					y
				),
				new Vector2(
					graphRect.End.X,
					y
				),
				new Color(
					0.18f,
					0.18f,
					0.20f,
					0.8f
				),
				GridLineWidth
			);
		}

		for (
			int i = 1;
			i < verticalLines;
			i++)
		{
			float x =
				graphRect.Position.X +
				graphRect.Size.X *
				i /
				verticalLines;

			DrawLine(
				new Vector2(
					x,
					graphRect.Position.Y
				),
				new Vector2(
					x,
					graphRect.End.Y
				),
				new Color(
					0.18f,
					0.18f,
					0.20f,
					0.8f
				),
				GridLineWidth
			);
		}
	}

	// ============================================================
	// GRAPH RECTANGLES
	// ============================================================

	private Rect2 GetExistingGraphRect()
	{
		float width =
			Mathf.Max(
				Size.X -
				GraphMarginLeft -
				GraphMarginRight,
				100.0f
			);

		// Allocate roughly half of the available vertical space
		// to the top graph.
		//
		// The important difference is that the total layout is
		// calculated from the actual Control height, rather than
		// leaving a hidden fixed portion at the bottom.
		float availableHeight =
			Mathf.Max(
				Size.Y -
				GraphMarginTop -
				GraphMarginBottom -
				GraphSpacing -
				SecondGraphMarginTop,
				160.0f
			);

		float height =
			Mathf.Max(
				availableHeight * 0.5f,
				80.0f
			);

		return new Rect2(
			GraphMarginLeft,
			GraphMarginTop,
			width,
			height
		);
	}

	private Rect2 GetRainEnergyGraphRect()
	{
		float width =
			Mathf.Max(
				Size.X -
				SecondGraphMarginLeft -
				SecondGraphMarginRight,
				100.0f
			);

		Rect2 firstGraph =
			GetExistingGraphRect();

		float top =
			firstGraph.End.Y +
			GraphSpacing +
			SecondGraphMarginTop;

		// Use EVERYTHING remaining below the second graph's top.
		//
		// There is deliberately no bottom reservation here.
		// This makes the second graph extend all the way to the
		// bottom edge of the StatisticsGraph Control.
		float bottom =
			Size.Y -
			SecondGraphMarginBottom;

		float height =
			Mathf.Max(
				bottom - top,
				80.0f
			);

		return new Rect2(
			SecondGraphMarginLeft,
			top,
			width,
			height
		);
	}

	// ============================================================
	// PARTICLE COUNT FORMATTING
	// ============================================================

	private string FormatParticleCount(
		float particles)
	{
		int rounded =
			Mathf.RoundToInt(
				particles
			);

		if (rounded >= 1000)
		{
			float thousands =
				rounded / 1000.0f;

			if (thousands >= 10.0f)
			{
				return thousands.ToString("F0") + "k";
			}

			return thousands.ToString("F1") + "k";
		}

		return rounded.ToString();
	}

	// ============================================================
	// RESIZE
	// ============================================================

	public override void _Notification(
		int what)
	{
		if (
			what ==
			NotificationResized)
		{
			topGraphCacheDirty = true;

			QueueRedraw();
		}
	}

	// ============================================================
	// PUBLIC DATA ACCESS
	// ============================================================

	public int SampleCount =>
		samples.Count;

	public int RainEnergySampleCount =>
		rainEnergySamples.Count;

	public float LatestAverageRain
	{
		get
		{
			if (
				rainEnergySamples.Count ==
				0)
			{
				return 0.0f;
			}

			return rainEnergySamples[
				rainEnergySamples.Count - 1
			].AverageRain;
		}
	}

	public float LatestAverageEnergy
	{
		get
		{
			if (
				rainEnergySamples.Count ==
				0)
			{
				return 0.0f;
			}

			return rainEnergySamples[
				rainEnergySamples.Count - 1
			].AverageEnergy;
		}
	}

	public float LatestAverageParticles
	{
		get
		{
			if (
				rainEnergySamples.Count ==
				0)
			{
				return 0.0f;
			}

			return rainEnergySamples[
				rainEnergySamples.Count - 1
			].AverageParticles;
		}
	}
}
