using Godot;

public partial class Graph3 : Control
{
	private const int WheelCount = 6;
	private const int InitialDelayFrames = 1200;
	private const int SampleIntervalFrames = 600;

	private readonly double[] energySum = new double[WheelCount];
	private readonly double[] displayedEnergyPerSecond = new double[WheelCount];
	private int totalFrames;
	private int windowFrames;
	private float maxEnergy = 0.001f;
	private FluidSimulator fluidSimulator;

	private const float MarginLeft = 55.0f;
	private const float MarginRight = 35.0f;
	private const float MarginTop = 35.0f;
	private const float MarginBottom = 60.0f;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		QueueRedraw();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (fluidSimulator == null || !IsInstanceValid(fluidSimulator))
		{
			Node currentScene = GetTree().CurrentScene;
			if (currentScene != null)
				fluidSimulator = currentScene.FindChild("FluidSimulation", true, false) as FluidSimulator;
		}

		if (fluidSimulator == null)
			return;

		totalFrames++;
		if (totalFrames <= InitialDelayFrames - SampleIntervalFrames)
			return;

		windowFrames++;
		int wheelCount = Mathf.Min(fluidSimulator.GetStatisticsWheelCount(), WheelCount);
		float dt = (float)delta;
		if (dt <= 0.000001f)
			return;

		for (int i = 0; i < wheelCount; i++)
			energySum[i] += fluidSimulator.GetStatisticsWheelEnergyThisFrame(i) / dt;

		if (windowFrames < SampleIntervalFrames)
			return;

		maxEnergy = 0.001f;
		for (int i = 0; i < WheelCount; i++)
		{
			displayedEnergyPerSecond[i] = energySum[i] / SampleIntervalFrames;
			maxEnergy = Mathf.Max(maxEnergy, (float)displayedEnergyPerSecond[i]);
			energySum[i] = 0.0;
		}

		maxEnergy = Mathf.Max(maxEnergy * 1.15f, 0.001f);
		windowFrames = 0;
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
		DrawString(ThemeDB.FallbackFont, new Vector2(rect.Position.X, rect.Position.Y - 7.0f), "Energy Generation per Wheel", HorizontalAlignment.Left, -1, UiSettings.FontSizeBig, UiSettings.FontColorEnabled);
		DrawString(ThemeDB.FallbackFont, new Vector2(rect.Position.X - 50.0f, rect.Position.Y + rect.Size.Y * 0.5f + 7.0f), "Energy / s", HorizontalAlignment.Right, 45.0f, UiSettings.FontSizeSmall, UiSettings.FontColorEnabled);

		if (totalFrames < InitialDelayFrames)
		{
			DrawString(ThemeDB.FallbackFont, new Vector2(rect.Position.X + rect.Size.X * 0.5f - 55.0f, rect.Position.Y + rect.Size.Y * 0.5f), "Collecting data...", HorizontalAlignment.Left, -1, UiSettings.FontSizeMedium, new Color(0.55f, 0.55f, 0.55f));
			return;
		}

		float barWidth = rect.Size.X / (WheelCount * 1.5f);
		float gap = barWidth * 0.5f;
		float baseline = rect.End.Y;
		float availableHeight = rect.Size.Y - 10.0f;

		for (int i = 0; i < WheelCount; i++)
		{
			float normalized = Mathf.Clamp((float)displayedEnergyPerSecond[i] / maxEnergy, 0.0f, 1.0f);
			float height = normalized * availableHeight;
			float x = rect.Position.X + gap * 0.5f + i * (barWidth + gap);
			Rect2 bar = new Rect2(x, baseline - height, barWidth, height);
			DrawRect(bar, new Color(0.15f, 0.65f, 0.85f), true);
			DrawString(ThemeDB.FallbackFont, new Vector2(x + barWidth * 0.5f - 5.0f, baseline + 20.0f), (i + 1).ToString(), HorizontalAlignment.Left, -1, UiSettings.FontSizeSmall, UiSettings.FontColorEnabled);
		}
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

	public int SampleCount => totalFrames;
}
