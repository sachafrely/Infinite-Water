using Godot;

/// <summary>
/// Main settings window.
///
/// This Control is the visual container for the settings panel.
/// Shared UI appearance is provided by UiSettings.
/// </summary>
public partial class SettingsWindow : Control
{
	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Stop;
		QueueRedraw();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
		{
			QueueRedraw();
		}
	}

	public override void _Draw()
	{
		if (Size.X <= 0.0f || Size.Y <= 0.0f)
		{
			return;
		}

		DrawRect(
			new Rect2(Vector2.Zero, Size),
			UiSettings.WindowColor,
			true
		);

		DrawRect(
			new Rect2(Vector2.Zero, Size),
			UiSettings.BorderColor,
			false,
			UiSettings.BorderSize
		);
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
