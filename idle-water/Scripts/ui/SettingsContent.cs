using Godot;

/// <summary>
/// Content container for SettingsWindow.
///
/// Individual settings sections own their own rendering and interaction.
/// </summary>
public partial class SettingsContent : VBoxContainer
{
	public override void _Ready()
	{
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

		Font font = ThemeDB.FallbackFont;
		const float leftMargin = 20.0f;
		const float topMargin = 30.0f;
		float separatorY = topMargin + 15.0f;

		DrawString(
			font,
			new Vector2(leftMargin, topMargin),
			"SETTINGS",
			HorizontalAlignment.Left,
			-1,
			UiSettings.FontSizeBig,
			UiSettings.FontColorBasic
		);

		DrawLine(
			new Vector2(leftMargin, separatorY),
			new Vector2(Size.X - leftMargin, separatorY),
			UiSettings.BorderColor.Darkened(0.5f),
			UiSettings.BorderSize
		);
	}
}
