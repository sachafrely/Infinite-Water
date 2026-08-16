using Godot;

/// <summary>
/// Content displayed inside the SettingsWindow.
///
/// This script is responsible only for the actual settings UI.
/// The window background and border are handled by SettingsWindow.
/// </summary>
public partial class SettingsContent : Control
{
	// ============================================================
	// LAYOUT
	// ============================================================

	private const float LeftMargin = 20.0f;
	private const float TopMargin = 30.0f;

	private const int TitleFontSize = 28;
	private const int SectionFontSize = 22;

	private const float SeparatorWidth = 2.0f;


	// ============================================================
	// COLORS
	// ============================================================

	private static readonly Color TitleColor =
		new Color(
			1.0f,
			1.0f,
			1.0f,
			1.0f
		);

	private static readonly Color SeparatorColor =
		new Color(
			0.35f,
			0.35f,
			0.35f,
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
		if (Size.X <= 0.0f || Size.Y <= 0.0f)
		{
			return;
		}

		Font font = ThemeDB.FallbackFont;


		// --------------------------------------------------------
		// SETTINGS TITLE
		// --------------------------------------------------------

		DrawString(
			font,
			new Vector2(
				LeftMargin,
				TopMargin
			),
			"SETTINGS",
			HorizontalAlignment.Left,
			-1,
			TitleFontSize,
			TitleColor
		);


		// --------------------------------------------------------
		// TITLE SEPARATOR
		// --------------------------------------------------------

		float separatorY =
			TopMargin + 15.0f;

		DrawLine(
			new Vector2(
				LeftMargin,
				separatorY
			),
			new Vector2(
				Size.X - LeftMargin,
				separatorY
			),
			SeparatorColor,
			SeparatorWidth
		);


		// --------------------------------------------------------
		// TILT SECTION
		// --------------------------------------------------------

		float sectionY =
			separatorY + 35.0f;

		DrawString(
			font,
			new Vector2(
				LeftMargin,
				sectionY
			),
			"TILT",
			HorizontalAlignment.Left,
			-1,
			SectionFontSize,
			TitleColor
		);
	}
}
