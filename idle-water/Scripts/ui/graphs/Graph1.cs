using Godot;
using System;
using System.Collections.Generic;

public partial class Graph1 : Control
{
	private readonly List<Sample> samples = new List<Sample>(36000);
	private const int MaxSamples = 600 * 60;
	private const float MarginLeft = 55.0f;
	private const float MarginRight = 35.0f;
	private const float MarginTop = 35.0f;
	private const float MarginBottom = 60.0f;
	private const int CacheRebuildInterval = 30;

	private float maxParticles = 1.0f;
	private float maxEnergy = 0.001f;
	private float maxFps = 60.0f;
	private bool cacheDirty = true;
	private int cachedWidth = -1;
	private int samplesSinceCache;
	private Vector2[] particlePoints = Array.Empty<Vector2>();
	private Vector2[] energyPoints = Array.Empty<Vector2>();
	private Vector2[] fpsPoints = Array.Empty<Vector2>();

	private struct Sample
	{
		public float Particles;
		public float EnergyPerSecond;
		public float Fps;

		public Sample(float particles, float energyPerSecond, float fps)
		{
			Particles = particles;
			EnergyPerSecond = energyPerSecond;
			Fps = fps;
		}
	}

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		QueueRedraw();
	}

	public void AddSample(int activeParticles, double energyPerSecond, float fps)
	{
		samples.Add(new Sample(activeParticles, (float)energyPerSecond, fps));
		while (samples.Count > MaxSamples)
			samples.RemoveAt(0);

		maxParticles = Mathf.Max(maxParticles, activeParticles);
		maxEnergy = Mathf.Max(maxEnergy, (float)energyPerSecond);
		maxFps = Mathf.Max(maxFps, fps);
		samplesSinceCache++;
		if (samplesSinceCache >= CacheRebuildInterval)
		{
			cacheDirty = true;
			samplesSinceCache = 0;
		}
		QueueRedraw();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
		{
			cacheDirty = true;
			QueueRedraw();
		}
	}

	public override void _Draw()
	{
		Rect2 rect = GetGraphRect();
		if (rect.Size.X <= 0.0f || rect.Size.Y <= 0.0f)
			return;

		DrawRect(rect, new Color(0.04f, 0.04f, 0.05f, 0.92f), true);
		DrawGrid(rect);
		DrawRect(rect, UiSettings.BorderColor, false, UiSettings.BorderSize);
		DrawString(ThemeDB.FallbackFont, new Vector2(rect.Position.X, rect.Position.Y - 7.0f), "Statistics - 10 Minutes", HorizontalAlignment.Left, -1, UiSettings.FontSizeBig, UiSettings.FontColorEnabled);

		DrawLegend(rect);

		if (samples.Count == 0)
		{
			DrawString(ThemeDB.FallbackFont, new Vector2(rect.Position.X + rect.Size.X * 0.5f - 55.0f, rect.Position.Y + rect.Size.Y * 0.5f), "Collecting data...", HorizontalAlignment.Left, -1, UiSettings.FontSizeMedium, new Color(0.55f, 0.55f, 0.55f));
			return;
		}

		int width = Mathf.Max((int)rect.Size.X, 2);
		if (cachedWidth != width)
			cacheDirty = true;
		if (cacheDirty)
			BuildCache(rect, width);

		if (particlePoints.Length >= 2)
			DrawPolyline(particlePoints, new Color(0.05f, 0.40f, 0.75f), 2.0f, true);
		if (energyPoints.Length >= 2)
			DrawPolyline(energyPoints, new Color(1.0f, 0.75f, 0.25f), 2.0f, true);
		if (fpsPoints.Length >= 2)
			DrawPolyline(fpsPoints, new Color(0.4f, 0.4f, 0.4f), 2.0f, true);

		DrawString(ThemeDB.FallbackFont, new Vector2(rect.Position.X - 50.0f, rect.Position.Y + rect.Size.Y * 0.5f + 7.0f), "Value", HorizontalAlignment.Right, 45.0f, UiSettings.FontSizeSmall, UiSettings.FontColorEnabled);
		DrawString(ThemeDB.FallbackFont, new Vector2(rect.Position.X, rect.End.Y + 24.0f), "10m ago", HorizontalAlignment.Left, -1, UiSettings.FontSizeSmall, new Color(0.65f, 0.65f, 0.65f));
		DrawString(ThemeDB.FallbackFont, new Vector2(rect.End.X - 30.0f, rect.End.Y + 24.0f), "Now", HorizontalAlignment.Left, -1, UiSettings.FontSizeSmall, new Color(0.65f, 0.65f, 0.65f));
	}

	private void DrawLegend(Rect2 rect)
	{
		float x = rect.End.X - 260.0f;
		float y = rect.Position.Y - 7.0f;
		DrawCircle(new Vector2(x, y - 4.0f), 3.0f, new Color(0.05f, 0.40f, 0.75f));
		DrawString(ThemeDB.FallbackFont, new Vector2(x + 10.0f, y), "Particles", HorizontalAlignment.Left, -1, UiSettings.FontSizeSmall, new Color(0.05f, 0.40f, 0.75f));
		x += 105.0f;
		DrawCircle(new Vector2(x, y - 4.0f), 3.0f, new Color(1.0f, 0.75f, 0.25f));
		DrawString(ThemeDB.FallbackFont, new Vector2(x + 10.0f, y), "Energy / s", HorizontalAlignment.Left, -1, UiSettings.FontSizeSmall, new Color(1.0f, 0.75f, 0.25f));
		x += 105.0f;
		DrawCircle(new Vector2(x, y - 4.0f), 3.0f, new Color(0.4f, 0.4f, 0.4f));
		DrawString(ThemeDB.FallbackFont, new Vector2(x + 10.0f, y), "FPS", HorizontalAlignment.Left, -1, UiSettings.FontSizeSmall, new Color(0.4f, 0.4f, 0.4f));
	}

	private void BuildCache(Rect2 rect, int width)
	{
		particlePoints = BuildSeries(rect, width, 0);
		energyPoints = BuildSeries(rect, width, 1);
		fpsPoints = BuildSeries(rect, width, 2);
		cachedWidth = width;
		cacheDirty = false;
	}

	private Vector2[] BuildSeries(Rect2 rect, int width, int series)
	{
		int pointCount = Math.Min(width, samples.Count);
		Vector2[] points = new Vector2[pointCount];
		for (int pixel = 0; pixel < pointCount; pixel++)
		{
			int start = (int)((double)pixel * samples.Count / pointCount);
			int end = (int)((double)(pixel + 1) * samples.Count / pointCount) - 1;
			if (end < start) end = start;
			if (end >= samples.Count) end = samples.Count - 1;

			double sum = 0.0;
			for (int i = start; i <= end; i++)
			{
				if (series == 0) sum += samples[i].Particles;
				else if (series == 1) sum += samples[i].EnergyPerSecond;
				else sum += samples[i].Fps;
			}

			float value = (float)(sum / Math.Max(1, end - start + 1));
			float max = series == 0 ? maxParticles : series == 1 ? maxEnergy : maxFps;
			float normalized = Mathf.Clamp(value / Mathf.Max(max, 0.001f), 0.0f, 1.0f);
			float x = pointCount == 1 ? rect.Position.X + rect.Size.X * 0.5f : rect.Position.X + pixel / (float)(pointCount - 1) * rect.Size.X;
			float y = rect.End.Y - normalized * rect.Size.Y;
			points[pixel] = new Vector2(x, y);
		}
		return points;
	}

	private Rect2 GetGraphRect()
	{
		return new Rect2(MarginLeft, MarginTop, Mathf.Max(Size.X - MarginLeft - MarginRight, 100.0f), Mathf.Max(Size.Y - MarginTop - MarginBottom, 80.0f));
	}

	private void DrawGrid(Rect2 rect)
	{
		Color gridColor = new Color(0.18f, 0.18f, 0.20f, 0.8f);
		for (int i = 1; i < 5; i++)
		{
			float y = rect.Position.Y + rect.Size.Y * i / 5.0f;
			DrawLine(new Vector2(rect.Position.X, y), new Vector2(rect.End.X, y), gridColor, 1.0f);
			float x = rect.Position.X + rect.Size.X * i / 5.0f;
			DrawLine(new Vector2(x, rect.Position.Y), new Vector2(x, rect.End.Y), gridColor, 1.0f);
		}
	}

	public int SampleCount => samples.Count;
}
