using Godot;

/// <summary>
/// Statistics window container.
/// The scene owns the graphs; this script only handles window state and
/// shared window rendering.
/// </summary>
public partial class StatisticsWindow : Control
{
	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Stop;
		Hide();
		QueueRedraw();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
			QueueRedraw();
	}

	public override void _Draw()
	{
		if (Size.X <= 0.0f || Size.Y <= 0.0f)
			return;

		DrawRect(new Rect2(Vector2.Zero, Size), UiSettings.WindowBackgroundColor, true);
		DrawRect(new Rect2(Vector2.Zero, Size), UiSettings.BorderColor, false, UiSettings.BorderSize);
	}

	public void Open()
	{
		Show();
		QueueRedraw();
	}

	public void Close()
	{
		Hide();
	}

	public bool IsOpen() => Visible;
}
