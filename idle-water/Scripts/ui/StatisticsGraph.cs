using Godot;
using System;
using System.Collections.Generic;

public partial class StatisticsGraph : Control
{
	private readonly List<GraphSample> samples = new List<GraphSample>(4096);
	private readonly List<RainEnergySample> rainEnergySamples = new List<RainEnergySample>();

	private const int MaxRainEnergySamples = 120;
	private const float GraphSpacing = 15.0f;
	private const float GraphMarginLeft = 55.0f;
	private const float GraphMarginRight = 35.0f;
	private const float GraphMarginTop = 35.0f;
	private const float GraphMarginBottom = 60.0f;
	private const float SecondGraphMarginLeft = 55.0f;
	private const float SecondGraphMarginRight = 35.0f;
	private const float SecondGraphMarginTop = 25.0f;
	private const float SecondGraphMarginBottom = 60.0f;
	private const float GraphBorderWidth = UiSettings.BorderSize;
	private const float GridLineWidth = 1.0f;
	private const float BottomPointRadius = 1.8f;

	private const float MaxTopGraphSeconds = 600.0f;
	private const int AssumedSamplesPerSecond = 60;
	private const int MaxTopGraphSamples = (int)(MaxTopGraphSeconds * AssumedSamplesPerSecond);

	private const string ExistingGraphTitle = "Statistics";
	private const string ExistingGraphYAxis = "Value";
	private const string RainEnergyGraphTitle = "Energy Per Particle";
	private const string RainEnergyGraphYAxis = "Energy";

	private Color particlesColor = new Color(0.05f, 0.40f, 0.75f);
	private Color energyColor = new Color(1.0f, 0.75f, 0.25f);
	private Color fpsColor = new Color(0.4f, 0.4f, 0.4f);

	private float existingMaxParticles = 100.0f;
	private float existingMaxEnergy = 1.0f;
	private float existingMaxFps = 60.0f;
	private float particleEnergyMaxParticles = 100.0f;
	private float particleEnergyMaxEnergy = 1.0f;

	private Vector2[] cachedParticlesPoints = Array.Empty<Vector2>();
	private Vector2[] cachedEnergyPoints = Array.Empty<Vector2>();
	private Vector2[] cachedFpsPoints = Array.Empty<Vector2>();
	private bool topGraphCacheDirty = true;
	private int lastCachedWidth = -1;
	private int samplesSinceCacheBuild;
	private const int CacheRebuildInterval = 30;

	private struct GraphSample
	{
		public float Particles;
		public float EnergyPerSecond;
		public float Fps;

		public GraphSample(float particles, float energyPerSecond, float fps)
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

		public RainEnergySample(float averageRain, float averageEnergy, float averageParticles)
		{
			AverageRain = averageRain;
			AverageEnergy = averageEnergy;
			AverageParticles = averageParticles;
		}
	}

	public override void _Ready()
	{
		MouseFilter = Control.MouseFilterEnum.Ignore;
		QueueRedraw();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
		{
			topGraphCacheDirty = true;
			QueueRedraw();
		}
	}

	public void AddSample(int activeParticles, double energyPerSecond, float fps, float delta)
	{
		samples.Add(new GraphSample(activeParticles, (float)energyPerSecond, fps));
		while (samples.Count > MaxTopGraphSamples)
			samples.RemoveAt(0);

		if (activeParticles > existingMaxParticles) existingMaxParticles = activeParticles;
		if (energyPerSecond > existingMaxEnergy) existingMaxEnergy = (float)energyPerSecond;
		if (fps > existingMaxFps) existingMaxFps = fps;

		existingMaxParticles = Mathf.Max(existingMaxParticles, 1.0f);
		existingMaxEnergy = Mathf.Max(existingMaxEnergy, 0.001f);
		existingMaxFps = Mathf.Max(existingMaxFps, 1.0f);

		samplesSinceCacheBuild++;
		if (samplesSinceCacheBuild >= CacheRebuildInterval)
		{
			topGraphCacheDirty = true;
			samplesSinceCacheBuild = 0;
		}

		QueueRedraw();
	}

	public void AddRainEnergySample(float averageRain, float averageEnergy, float averageParticles)
	{
		rainEnergySamples.Add(new RainEnergySample(averageRain, averageEnergy, averageParticles));
		while (rainEnergySamples.Count > MaxRainEnergySamples)
			rainEnergySamples.RemoveAt(0);

		RecalculateParticleEnergyGraphScale();
		QueueRedraw();

		GD.Print("Particle/Energy Graph Point: Particles=" + averageParticles.ToString("F1") +
			" Energy=" + averageEnergy.ToString("F4") + " Rain=" + averageRain.ToString("F1") + "%");
	}

	private void RecalculateParticleEnergyGraphScale()
	{
		particleEnergyMaxParticles = 1.0f;
		particleEnergyMaxEnergy = 0.001f;

		foreach (RainEnergySample sample in rainEnergySamples)
		{
			particleEnergyMaxParticles = Mathf.Max(particleEnergyMaxParticles, sample.AverageParticles);
			particleEnergyMaxEnergy = Mathf.Max(particleEnergyMaxEnergy, sample.AverageEnergy);
		}

		particleEnergyMaxParticles = Mathf.Max(particleEnergyMaxParticles * 1.15f, 1.0f);
		particleEnergyMaxEnergy = Mathf.Max(particleEnergyMaxEnergy * 1.15f, 0.001f);
	}

	public override void _Draw()
	{
		DrawExistingGraph();
		DrawRainEnergyGraph();
	}

	private void DrawExistingGraph()
	{
		Rect2 graphRect = GetExistingGraphRect();
		if (graphRect.Size.X <= 0.0f || graphRect.Size.Y <= 0.0f) return;

		DrawGraphBackground(graphRect);
		DrawGrid(graphRect);
		DrawRect(graphRect, UiSettings.BorderColor, false, GraphBorderWidth);
		DrawString(ThemeDB.FallbackFont, new Vector2(graphRect.Position.X, graphRect.Position.Y - 7.0f), ExistingGraphTitle, HorizontalAlignment.Left, -1, 22, new Color(0.9f, 0.9f, 0.9f));
		DrawTopGraphLegend(graphRect);

		// Value label restored to the left side of the graph.
		DrawString(ThemeDB.FallbackFont, new Vector2(graphRect.Position.X - 50.0f, graphRect.Position.Y + graphRect.Size.Y * 0.5f + 7.0f), ExistingGraphYAxis, HorizontalAlignment.Right, 45.0f, UiSettings.FontSizeSmall, new Color(0.75f, 0.75f, 0.75f));
		DrawTopTimeLabels(graphRect);

		if (samples.Count == 0)
		{
			DrawString(ThemeDB.FallbackFont, new Vector2(graphRect.Position.X + graphRect.Size.X * 0.5f - 55.0f, graphRect.Position.Y + graphRect.Size.Y * 0.5f), "Collecting data...", HorizontalAlignment.Left, -1, 22, new Color(0.55f, 0.55f, 0.55f));
			return;
		}

		int graphWidth = Mathf.Max((int)graphRect.Size.X, 1);
		if (lastCachedWidth != graphWidth) topGraphCacheDirty = true;
		if (topGraphCacheDirty) BuildTopGraphCache(graphRect);

		if (cachedParticlesPoints.Length >= 2) DrawPolyline(cachedParticlesPoints, particlesColor, 2.0f, true);
		if (cachedEnergyPoints.Length >= 2) DrawPolyline(cachedEnergyPoints, energyColor, 2.0f, true);
		if (cachedFpsPoints.Length >= 2) DrawPolyline(cachedFpsPoints, fpsColor, 2.0f, true);
	}

	private void DrawTopGraphLegend(Rect2 graphRect)
	{
		// Extra spacing keeps Particles, Energy and FPS from overlapping.
		float x = graphRect.End.X - 285.0f;
		float y = graphRect.Position.Y - 7.0f;

		DrawCircle(new Vector2(x, y - 4.0f), 3.0f, particlesColor);
		DrawString(ThemeDB.FallbackFont, new Vector2(x + 10.0f, y), "Particles", HorizontalAlignment.Left, -1, UiSettings.FontSizeSmall, particlesColor);

		x += 105.0f;
		DrawCircle(new Vector2(x, y - 4.0f), 3.0f, energyColor);
		DrawString(ThemeDB.FallbackFont, new Vector2(x + 10.0f, y), "Energy", HorizontalAlignment.Left, -1, UiSettings.FontSizeSmall, energyColor);

		x += 85.0f;
		DrawCircle(new Vector2(x, y - 4.0f), 3.0f, fpsColor);
		DrawString(ThemeDB.FallbackFont, new Vector2(x + 10.0f, y), "FPS", HorizontalAlignment.Left, -1, UiSettings.FontSizeSmall, fpsColor);
	}

	private void DrawTopTimeLabels(Rect2 graphRect)
	{
		float totalSeconds = Mathf.Min(GetTotalRunSeconds(), MaxTopGraphSeconds);
		if (totalSeconds <= 0.0f) return;

		Color textColor = new Color(0.65f, 0.65f, 0.65f);
		DrawString(ThemeDB.FallbackFont, new Vector2(graphRect.Position.X + graphRect.Size.X * 0.5f - 10.0f, graphRect.End.Y + 24.0f), FormatSeconds(totalSeconds * 0.5f), HorizontalAlignment.Left, -1, UiSettings.FontSizeSmall, textColor);
		DrawString(ThemeDB.FallbackFont, new Vector2(graphRect.End.X - 30.0f, graphRect.End.Y + 24.0f), FormatSeconds(totalSeconds), HorizontalAlignment.Left, -1, UiSettings.FontSizeSmall, textColor);
	}

	private void BuildTopGraphCache(Rect2 graphRect)
	{
		if (samples.Count == 0)
		{
			cachedParticlesPoints = Array.Empty<Vector2>();
			cachedEnergyPoints = Array.Empty<Vector2>();
			cachedFpsPoints = Array.Empty<Vector2>();
			topGraphCacheDirty = false;
			return;
		}

		int width = Mathf.Max((int)graphRect.Size.X, 2);
		cachedParticlesPoints = BuildCompressedSeries(graphRect, width, 0);
		cachedEnergyPoints = BuildCompressedSeries(graphRect, width, 1);
		cachedFpsPoints = BuildCompressedSeries(graphRect, width, 2);
		lastCachedWidth = width;
		topGraphCacheDirty = false;
	}

	private Vector2[] BuildCompressedSeries(Rect2 graphRect, int width, int series)
	{
		int pointCount = Math.Min(width, samples.Count);
		Vector2[] points = new Vector2[pointCount];

		for (int pixel = 0; pixel < pointCount; pixel++)
		{
			int startIndex = (int)((double)pixel * samples.Count / pointCount);
			int endIndex = (int)((double)(pixel + 1) * samples.Count / pointCount) - 1;
			if (endIndex < startIndex) endIndex = startIndex;
			if (endIndex >= samples.Count) endIndex = samples.Count - 1;

			double sum = 0.0;
			int count = endIndex - startIndex + 1;
			for (int i = startIndex; i <= endIndex; i++)
			{
				GraphSample sample = samples[i];
				if (series == 0) sum += sample.Particles;
				else if (series == 1) sum += sample.EnergyPerSecond;
				else sum += sample.Fps;
			}

			float value = (float)(sum / Math.Max(count, 1));
			float maxValue = series == 0 ? existingMaxParticles : series == 1 ? existingMaxEnergy : existingMaxFps;
			float normalized = Mathf.Clamp(value / maxValue, 0.0f, 1.0f);
			float x = pointCount == 1 ? graphRect.Position.X + graphRect.Size.X * 0.5f : graphRect.Position.X + (float)pixel / (pointCount - 1) * graphRect.Size.X;
			float y = graphRect.Position.Y + graphRect.Size.Y - normalized * graphRect.Size.Y;
			points[pixel] = new Vector2(x, y);
		}

		return points;
	}

	private void DrawRainEnergyGraph()
	{
		Rect2 graphRect = GetRainEnergyGraphRect();
		if (graphRect.Size.X <= 0.0f || graphRect.Size.Y <= 0.0f) return;

		DrawGraphBackground(graphRect);
		DrawGrid(graphRect);
		DrawRect(graphRect, UiSettings.BorderColor, false, GraphBorderWidth);
		DrawString(ThemeDB.FallbackFont, new Vector2(graphRect.Position.X, graphRect.Position.Y - 7.0f), RainEnergyGraphTitle, HorizontalAlignment.Left, -1, 20, new Color(0.9f, 0.9f, 0.9f));

		// Energy label restored to the left side of the graph.
		DrawString(ThemeDB.FallbackFont, new Vector2(graphRect.Position.X - 50.0f, graphRect.Position.Y + graphRect.Size.Y * 0.5f + 7.0f), RainEnergyGraphYAxis, HorizontalAlignment.Right, 45.0f, UiSettings.FontSizeSmall, new Color(0.75f, 0.75f, 0.75f));

		Color textColor = new Color(0.65f, 0.65f, 0.65f);
		DrawString(ThemeDB.FallbackFont, new Vector2(graphRect.Position.X - 5.0f, graphRect.End.Y + 24.0f), "0", HorizontalAlignment.Left, -1, UiSettings.FontSizeSmall, textColor);
		DrawString(ThemeDB.FallbackFont, new Vector2(graphRect.Position.X + graphRect.Size.X * 0.5f - 15.0f, graphRect.End.Y + 24.0f), FormatParticleCount(particleEnergyMaxParticles * 0.5f), HorizontalAlignment.Left, -1, UiSettings.FontSizeSmall, textColor);
		DrawString(ThemeDB.FallbackFont, new Vector2(graphRect.End.X - 30.0f, graphRect.End.Y + 24.0f), FormatParticleCount(particleEnergyMaxParticles), HorizontalAlignment.Left, -1, UiSettings.FontSizeSmall, textColor);

		if (rainEnergySamples.Count == 0)
		{
			DrawString(ThemeDB.FallbackFont, new Vector2(graphRect.Position.X + graphRect.Size.X * 0.5f - 55.0f, graphRect.Position.Y + graphRect.Size.Y * 0.5f), "Collecting data...", HorizontalAlignment.Left, -1, 22, new Color(0.55f, 0.55f, 0.55f));
			return;
		}

		foreach (RainEnergySample sample in rainEnergySamples)
		{
			float normalizedX = Mathf.Clamp(sample.AverageParticles / particleEnergyMaxParticles, 0.0f, 1.0f);
			float normalizedY = Mathf.Clamp(sample.AverageEnergy / particleEnergyMaxEnergy, 0.0f, 1.0f);
			Vector2 point = new Vector2(
				graphRect.Position.X + normalizedX * graphRect.Size.X,
				graphRect.Position.Y + graphRect.Size.Y - normalizedY * graphRect.Size.Y
			);
			DrawCircle(point, BottomPointRadius, new Color(1.0f, 0.75f, 0.25f));
		}
	}

	private void DrawGraphBackground(Rect2 graphRect)
	{
		DrawRect(graphRect, new Color(0.04f, 0.04f, 0.05f, 0.92f), true);
	}

	private void DrawGrid(Rect2 graphRect)
	{
		const int horizontalLines = 5;
		const int verticalLines = 5;
		Color gridColor = new Color(0.18f, 0.18f, 0.20f, 0.8f);

		for (int i = 1; i < horizontalLines; i++)
		{
			float y = graphRect.Position.Y + graphRect.Size.Y * i / horizontalLines;
			DrawLine(new Vector2(graphRect.Position.X, y), new Vector2(graphRect.End.X, y), gridColor, GridLineWidth);
		}

		for (int i = 1; i < verticalLines; i++)
		{
			float x = graphRect.Position.X + graphRect.Size.X * i / verticalLines;
			DrawLine(new Vector2(x, graphRect.Position.Y), new Vector2(x, graphRect.End.Y), gridColor, GridLineWidth);
		}
	}

	private Rect2 GetExistingGraphRect()
	{
		float width = Mathf.Max(Size.X - GraphMarginLeft - GraphMarginRight, 100.0f);
		return new Rect2(GraphMarginLeft, GraphMarginTop, width, GetEqualGraphHeight());
	}

	private Rect2 GetRainEnergyGraphRect()
	{
		float width = Mathf.Max(Size.X - SecondGraphMarginLeft - SecondGraphMarginRight, 100.0f);
		Rect2 firstGraph = GetExistingGraphRect();
		float top = firstGraph.End.Y + GraphSpacing + SecondGraphMarginTop;
		return new Rect2(SecondGraphMarginLeft, top, width, GetEqualGraphHeight());
	}

	private float GetEqualGraphHeight()
	{
		float availableHeight = Size.Y - GraphMarginTop - GraphMarginBottom - GraphSpacing - SecondGraphMarginTop - SecondGraphMarginBottom;
		return Mathf.Max(availableHeight * 0.5f, 80.0f);
	}

	private float GetTotalRunSeconds()
	{
		return samples.Count / (float)AssumedSamplesPerSecond;
	}

	private string FormatSeconds(float seconds)
	{
		if (seconds < 60.0f) return Mathf.RoundToInt(seconds) + "s";
		int total = Mathf.RoundToInt(seconds);
		return (total / 60) + ":" + (total % 60).ToString("00");
	}

	private string FormatParticleCount(float particles)
	{
		int rounded = Mathf.RoundToInt(particles);
		if (rounded >= 1000)
		{
			float thousands = rounded / 1000.0f;
			return thousands >= 10.0f ? thousands.ToString("F0") + "k" : thousands.ToString("F1") + "k";
		}
		return rounded.ToString();
	}

	public int SampleCount => samples.Count;
	public int RainEnergySampleCount => rainEnergySamples.Count;
	public float LatestAverageRain => rainEnergySamples.Count == 0 ? 0.0f : rainEnergySamples[rainEnergySamples.Count - 1].AverageRain;
	public float LatestAverageEnergy => rainEnergySamples.Count == 0 ? 0.0f : rainEnergySamples[rainEnergySamples.Count - 1].AverageEnergy;
	public float LatestAverageParticles => rainEnergySamples.Count == 0 ? 0.0f : rainEnergySamples[rainEnergySamples.Count - 1].AverageParticles;
}
