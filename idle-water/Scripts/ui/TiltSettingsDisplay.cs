using Godot;

/// <summary>
/// Visual container for the tilt setting.
/// Owns the TILT section label and places its controls below it.
/// </summary>
public partial class TiltSettingsDisplay : Control
{
	private const float LeftMargin = 20.0f;
	private const float SectionTop = 65.0f;

	private HBoxContainer _controls;

	public override void _Ready()
	{
		_controls = GetNodeOrNull<HBoxContainer>("HBoxContainer");
		LayoutControls();
		QueueRedraw();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
		{
			LayoutControls();
			QueueRedraw();
		}
	}

	private void LayoutControls()
	{
		if (_controls == null)
			return;

		_controls.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_controls.OffsetLeft = 0.0f;
		_controls.OffsetTop = SectionTop;
		_controls.OffsetRight = 0.0f;
		_controls.OffsetBottom = 0.0f;
	}

	public override void _Draw()
	{
		if (Size.X <= 0.0f || Size.Y <= 0.0f)
			return;

		Font font = ThemeDB.FallbackFont;
		DrawString(
			font,
			new Vector2(LeftMargin, 35.0f),
			"TILT",
			HorizontalAlignment.Left,
			80.0f,
			UiSettings.FontSizeMedium,
			UiSettings.FontColorBasic
		);
	}
}
