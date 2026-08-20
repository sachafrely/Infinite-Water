using Godot;

/// <summary>
/// Graphical rain indicator displayed in the existing TopUI/RainDisplay space.
/// Ten small segments represent 0-100% rain in 10% steps.
/// </summary>
public partial class GraphicalRainDisplay : Control
{
	private const int SegmentCount = 10;
	private const float SegmentWidth = 12.0f;
	private const float SegmentHeight = 16.0f;
	private const float SegmentGap = 3.0f;
	private const float RightMargin = 16.0f;
	private const float TopMargin = 36.0f;

	private readonly ColorRect[] segments = new ColorRect[SegmentCount];

	public override void _Ready()
	{
		MouseFilter = Control.MouseFilterEnum.Ignore;
		CreateSegments();
		UpdateRain(0.0f);
	}

	public void UpdateRain(float rainPercent)
	{
		int activeCount = Mathf.Clamp(
			Mathf.RoundToInt(rainPercent / 10.0f),
			0,
			SegmentCount
		);

		for (int i = 0; i < SegmentCount; i++)
		{
			if (segments[i] == null)
				continue;

			segments[i].Color = i < activeCount
				? UiSettings.FontColorWater
				: new Color(UiSettings.ButtonColor, 0.85f);
		}
	}

	private void CreateSegments()
	{
		for (int i = 0; i < SegmentCount; i++)
		{
			ColorRect segment = new ColorRect();
			segment.MouseFilter = Control.MouseFilterEnum.Ignore;
			segment.Size = new Vector2(SegmentWidth, SegmentHeight);
			segment.Position = new Vector2(
				i * (SegmentWidth + SegmentGap),
				0.0f
			);
			AddChild(segment);
			segments[i] = segment;
		}

		Size = new Vector2(
			SegmentCount * SegmentWidth + (SegmentCount - 1) * SegmentGap,
			SegmentHeight
		);

		Position = new Vector2(
			GetViewportRect().Size.X - Size.X - RightMargin,
			TopMargin
		);
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized && IsInsideTree())
		{
			Position = new Vector2(
				GetViewportRect().Size.X - Size.X - RightMargin,
				TopMargin
			);
		}
	}
}
