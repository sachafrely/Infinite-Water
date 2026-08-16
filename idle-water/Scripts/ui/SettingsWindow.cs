using Godot;

/// <summary>
/// Main settings window.
///
/// This Control is the visual container for the settings panel.
/// It fills the complete area assigned to it and draws a simple
/// background and border.
///
/// Individual settings controls can be added here later.
/// </summary>
public partial class SettingsWindow : Control
{
	// ============================================================
	// APPEARANCE
	// ============================================================

	private const float BorderWidth = 2.0f;

	private static readonly Color BackgroundColor =
		new Color(
			0.04f,
			0.04f,
			0.05f,
			0.97f
		);

	private static readonly Color BorderColor =
		new Color(
			0.75f,
			0.75f,
			0.75f,
			1.0f
		);


	// ============================================================
	// GODOT
	// ============================================================

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


	// ============================================================
	// DRAWING
	// ============================================================

	public override void _Draw()
	{
		// Nothing to draw if the Control has no size.
		if (Size.X <= 0.0f || Size.Y <= 0.0f)
		{
			return;
		}


		// --------------------------------------------------------
		// Full window background
		// --------------------------------------------------------

		DrawRect(
			new Rect2(
				Vector2.Zero,
				Size
			),
			BackgroundColor,
			true
		);


		// --------------------------------------------------------
		// Full window border
		// --------------------------------------------------------

		DrawRect(
			new Rect2(
				Vector2.Zero,
				Size
			),
			BorderColor,
			false,
			BorderWidth
		);
	}


	// ============================================================
	// WINDOW API
	// ============================================================

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
