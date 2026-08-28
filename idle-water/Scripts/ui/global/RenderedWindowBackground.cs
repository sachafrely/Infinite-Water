using Godot;

[Tool]
public partial class RenderedWindowBackground : Control
{
	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
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

		float w = UiSettings.BorderSize;
		if (w <= 0.0f)
			return;

		DrawRect(new Rect2(0, 0, Size.X, w), UiSettings.BorderColor, true);
		DrawRect(new Rect2(0, Size.Y - w, Size.X, w), UiSettings.BorderColor, true);
		DrawRect(new Rect2(0, 0, w, Size.Y), UiSettings.BorderColor, true);
		DrawRect(new Rect2(Size.X - w, 0, w, Size.Y), UiSettings.BorderColor, true);
	}
}
