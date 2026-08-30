using Godot;
using System.Collections.Generic;

public partial class Graph1 : Control
{
	private readonly List<float> values = new List<float>(600);
	private const int MaxSamples = 600;
	private const float MarginLeft = 55.0f;
	private const float MarginRight = 35.0f;
	private const float MarginTop = 35.0f;
	private const float MarginBottom = 60.0f;

	private float maxValue = 1.0f;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		QueueRedraw();
	}

	public void AddSample(int activeParticles)
	{
		values.Add(activeParticles);
		while (values.Count > MaxSamples)
			values.RemoveAt(0);

		maxValue = Mathf.Max(maxValue, activeParticles);
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

		DrawGraphBackground(rect);
		DrawGrid(rect);
		DrawRect(rect, UiSettings.BorderColor, false, UiSettings.BorderSize);
		DrawString(ThemeDB.FallbackFont, new Vector2(rect.Position.X, rect.Position.Y - 7.0f), "Active Particles", HorizontalAlignment.Left, -1, UiSettings.FontSizeBig, UiSettings.FontColorEnabled);
		DrawString(ThemeDB.FallbackFont, new Vector2(rect.Position.X - 50.0f, rect.Position.Y + rect.Size.Y * 0.5f + 7.0f), "Particles", HorizontalAlignment.Right, 45.0f, UiSettings.FontSizeSmall, UiSettings.FontColorEnabled);

		if (values.Count == 0)
		{
			DrawString(ThemeDB.FallbackFont, new Vector2(rect.Position.X + rect.Size.X * 0.5f - 55.0f, rect.Position.Y + rect.Size.Y * 0.5f), "Collecting data...", HorizontalAlignment.Left, -1, UiSettings.FontSizeMedium, new Color(0.55f, 0.55f, 0.55f));
			return;
		}

		Vector2[] points = new Vector2[values.Count];
		for (int i = 0; i < values.Count; i++)
		{
			float x = values.Count == 1 ? rect.Position.X + rect.Size.X * 0.5f : rect.Position.X + i / (float)(values.Count - 1) * rect.Size.X;
			float normalized = Mathf.Clamp(values[i] / maxValue, 0.0f, 1.0f);
			float y = rect.End.Y - normalized * rect.Size.Y;
			points[i] = new Vector2(x, y);
		}

		if (points.Length >= 2)
			DrawPolyline(points, new Color(0.05f, 0.40f, 0.75f), 2.0f, true);
	}

	private Rect2 GetGraphRect()
	{
		return new Rect2(
			MarginLeft,
			MarginTop,
			Mathf.Max(Size.X - MarginLeft - MarginRight, 100.0f),
			Mathf.Max(Size.Y - MarginTop - MarginBottom, 80.0f));
	}

	private void DrawGraphBackground(Rect2 rect)
	{
		DrawRect(rect, new Color(0.04f, 0.04f, 0.05f, 0.92f), true);
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

	public int SampleCount => values.Count;
}
