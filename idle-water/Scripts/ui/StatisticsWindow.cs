using Godot;

public partial class StatisticsWindow : Control
{
	public override void _Ready()
	{
		// The Statistics window is modal while open. It must consume touches so
		// controls underneath (including wheel Buy/Upgrade controls) cannot be
		// activated through the window.
		MouseFilter = Control.MouseFilterEnum.Stop;
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

	public bool IsOpen()
	{
		return Visible;
	}
}
