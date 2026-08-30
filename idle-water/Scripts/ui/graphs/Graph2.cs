using Godot;
using System.Collections.Generic;

public partial class Graph2 : Control
{
	private readonly List<Sample> samples = new List<Sample>(600);
	private const int MaxSamples = 600;
	private const float MarginLeft = 55.0f;
	private const float MarginRight = 35.0f;
	private const float MarginTop = 35.0f;
	private const float MarginBottom = 60.0f;

	private float maxEnergy = 1.0f;
	private float maxFps = 60.0f;

	private struct Sample
	{
		public float Energy;
		public float Fps;

		public Sample(float energy, float fps)
		{
			Energy = energy;
			Fps = fps;
		}
	}

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		QueueRedraw();
	}

	public void AddSample(double energyPerSecond, float fps)
	{
		samples.Add(new Sample((float)energyPerSecond, fps));
		while (samples.Count > MaxSamples)
			samples.RemoveAt(0);

		maxEnergy = Mathf.Max(maxEnergy, (float)energyPerSecond);
		maxFps = Mathf.Max(maxFps, fps);
		QueueRedraw();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
			QueueRedraw();
	}

	public override void _Draw()
	{
		Rect2 rect = GetGraphRect();
		if (rect.Size.X <= 0.0f || rect.Size.Y <= 0.0f)
			return;

		DrawRect(rect, new Color(0.04f, 0.04f, 0.05f, 0.92f), true);
		DrawGrid(rect);
		DrawRect(rect, UiSettings.BorderColor, false, UiSettings.BorderSize);

		DrawString(ThemeDB.FallbackFont, new Vector2(rect.Position.X, rect.Position.Y - 7.0f), "Energy / FPS", HorizontalAlignment.Left, -1, UiSettings.FontSizeBig, UiSettings.FontColorEnabled);

		if (samples.Count == 0)
		{
			DrawString(ThemeDB.FallbackFont, new Vector2(rect.Position.X + rect.Size.X * 0.5f - 55.0f, rect.Position.Y + rect.Size.Y * 0.5f), "Collecting data...", HorizontalAlignment.Left, -1, UiSettings.FontSizeMedium, new Color(0.55f, 0.55f, 0.55f));
			return;
		}

		Vector2[] energyPoints = new Vector2[samples.Count];
		Vector2[] fpsPoints = new Vector2[samples.Count];

		for (int i = 0; i < samples.Count; i++)
		{
			float x = samples.Count == 1 ? rect.Position.X + rect.Size.X * 0.5f : rect.Position.X + i / (float)(samples.Count - 1) * rect.Size.X;
			energyPoints[i] = new Vector2(x, rect.End.Y - Mathf.Clamp(samples[i].Energy / maxEnergy, 0.0f, 1.0f) * rect.Size.Y);
			fpsPoints[i] = new Vector2(x, rect.End.Y - Mathf.Clamp(samples[i].Fps / maxFps, 0.0f, 1.0f) * rect.Size.Y);
		}

		if (energyPoints.Length >= 2)
			DrawPolyline(energyPoints, new Color(1.0f, 0.75f, 0.25f), 2.0f, true);
		if (fpsPoints.Length >= 2)
			DrawPolyline(fpsPoints, new Color(0.4f, 0.4f, 0.4f), 2.0f, true);

		DrawString(ThemeDB.FallbackFont, new Vector2(rect.End.X - 145.0f, rect.Position.Y - 7.0f), "Energy", HorizontalAlignment.Left, -1, UiSettings.FontSizeSmall, new Color(1.0f, 0.75f, 0.25f));
		DrawString(ThemeDB.FallbackFont, new Vector2(rect.End.X - 75.0f, rect.Position.Y - 7.0f), "FPS", HorizontalAlignment.Left, -1, UiSettings.FontSizeSmall, new Color(0.4f, 0.4f, 0.4f));
	}

	private Rect2 GetGraphRect()
	{
		return new Rect2(
			MarginLeft,
			MarginTop,
			Mathf.Max(Size.X - MarginLeft - MarginRight, 100.0f),
			Mathf.Max(Size.Y - MarginTop - MarginBottom, 80.0f));
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
