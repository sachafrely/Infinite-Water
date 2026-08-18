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
	private const int SettingFontSize = 18;

	private const float SeparatorWidth = 2.0f;

	private const float SliderTopOffset = 78.0f;
	private const float SliderHeight = 16.0f;
	private const float SliderHandleSize = 12.0f;
	private const float SliderSideMargin = 10.0f;

	// ============================================================
	// TILT SETTINGS
	// ============================================================

	private const float MinimumTiltAngle = -45.0f;
	private const float MaximumTiltAngle = 45.0f;
	private const float TiltStep = 1.0f;

	private float _tiltAngle = 0.0f;
	private bool _isDraggingTiltSlider;

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

	private static readonly Color SliderTrackColor =
		new Color(
			0.25f,
			0.25f,
			0.27f,
			1.0f
		);

	private static readonly Color SliderFillColor =
		new Color(
			0.75f,
			0.75f,
			0.75f,
			1.0f
		);

	private static readonly Color SliderCenterColor =
		new Color(
			0.45f,
			0.45f,
			0.45f,
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

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseButton &&
			mouseButton.ButtonIndex == MouseButton.Left)
		{
			if (mouseButton.Pressed && IsMouseOverTiltSlider(mouseButton.Position))
			{
				_isDraggingTiltSlider = true;
				SetTiltFromMousePosition(mouseButton.Position.X);
				AcceptEvent();
				return;
			}

			if (!mouseButton.Pressed && _isDraggingTiltSlider)
			{
				_isDraggingTiltSlider = false;
				AcceptEvent();
				return;
			}
		}

		if (@event is InputEventMouseMotion mouseMotion && _isDraggingTiltSlider)
		{
			SetTiltFromMousePosition(mouseMotion.Position.X);
			AcceptEvent();
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

		// --------------------------------------------------------
		// TILT VALUE
		// --------------------------------------------------------

		DrawString(
			font,
			new Vector2(
				Size.X - LeftMargin,
				sectionY
			),
			$"{GetDisplayTiltAngle():+0;-0;0}°",
			HorizontalAlignment.Right,
			140.0f,
			SettingFontSize,
			TitleColor
		);

		// --------------------------------------------------------
		// TILT SLIDER
		// --------------------------------------------------------

		DrawTiltSlider(font, sectionY + SliderTopOffset);
	}

	// ============================================================
	// TILT SLIDER
	// ============================================================

	private void DrawTiltSlider(Font font, float sliderY)
	{
		float sliderLeft = LeftMargin + SliderSideMargin;
		float sliderRight = Size.X - LeftMargin - SliderSideMargin;
		float sliderWidth = sliderRight - sliderLeft;

		if (sliderWidth <= 0.0f)
		{
			return;
		}

		float centerX = sliderLeft + sliderWidth * 0.5f;
		float handleX = GetTiltSliderX(sliderLeft, sliderWidth);
		float trackY = sliderY + SliderHeight * 0.5f;

		// Full slider track.
		DrawRect(
			new Rect2(
				sliderLeft,
				trackY - SliderHeight * 0.5f,
				sliderWidth,
				SliderHeight
			),
			SliderTrackColor,
			true
		);

		// Center marker represents neutral tilt.
		DrawRect(
			new Rect2(
				centerX - 1.0f,
				trackY - SliderHeight * 0.5f - 3.0f,
				2.0f,
				SliderHeight + 6.0f
			),
			SliderCenterColor,
			true
		);

		// Filled portion from neutral to the current angle.
		float fillLeft = Mathf.Min(centerX, handleX);
		float fillRight = Mathf.Max(centerX, handleX);

		DrawRect(
			new Rect2(
				fillLeft,
				trackY - SliderHeight * 0.5f,
				fillRight - fillLeft,
				SliderHeight
			),
			SliderFillColor,
			true
		);

		// Slider handle.
		DrawRect(
			new Rect2(
				handleX - SliderHandleSize * 0.5f,
				trackY - SliderHandleSize * 0.5f,
				SliderHandleSize,
				SliderHandleSize
			),
			TitleColor,
			true
		);

		// End labels.
		DrawString(
			font,
			new Vector2(sliderLeft, trackY + 28.0f),
			"-45°",
			HorizontalAlignment.Left,
			-1,
			SettingFontSize,
			TitleColor
		);

		DrawString(
			font,
			new Vector2(sliderRight - 45.0f, trackY + 28.0f),
			"+45°",
			HorizontalAlignment.Right,
			45.0f,
			SettingFontSize,
			TitleColor
		);
	}

	private bool IsMouseOverTiltSlider(Vector2 mousePosition)
	{
		float separatorY = TopMargin + 15.0f;
		float sectionY = separatorY + 35.0f;
		float sliderY = sectionY + SliderTopOffset;

		float sliderLeft = LeftMargin;
		float sliderRight = Size.X - LeftMargin;
		float sliderTop = sliderY - 10.0f;
		float sliderBottom = sliderY + SliderHeight + 30.0f;

		return mousePosition.X >= sliderLeft &&
			mousePosition.X <= sliderRight &&
			mousePosition.Y >= sliderTop &&
			mousePosition.Y <= sliderBottom;
	}

	private void SetTiltFromMousePosition(float mouseX)
	{
		float sliderLeft = LeftMargin + SliderSideMargin;
		float sliderRight = Size.X - LeftMargin - SliderSideMargin;
		float sliderWidth = sliderRight - sliderLeft;

		if (sliderWidth <= 0.0f)
		{
			return;
		}

		float normalized =
			Mathf.Clamp(
				(mouseX - sliderLeft) / sliderWidth,
				0.0f,
				1.0f
			);

		float angle =
			Mathf.Lerp(
				MinimumTiltAngle,
				MaximumTiltAngle,
				normalized
			);

		_tiltAngle = Mathf.Snapped(angle, TiltStep);
		QueueRedraw();
	}

	private float GetTiltSliderX(float sliderLeft, float sliderWidth)
	{
		float normalized =
			Mathf.InverseLerp(
				MinimumTiltAngle,
				MaximumTiltAngle,
				_tiltAngle
			);

		return sliderLeft + sliderWidth * normalized;
	}

	private int GetDisplayTiltAngle()
	{
		return Mathf.RoundToInt(_tiltAngle);
	}

	// ============================================================
	// PUBLIC API
	// ============================================================

	/// <summary>
	/// Returns the currently selected tilt angle in degrees.
	/// </summary>
	public float GetTiltAngle()
	{
		return _tiltAngle;
	}

	/// <summary>
	/// Sets the tilt angle and updates the settings UI.
	/// </summary>
	public void SetTiltAngle(float angle)
	{
		_tiltAngle =
			Mathf.Clamp(
				Mathf.Snapped(angle, TiltStep),
				MinimumTiltAngle,
				MaximumTiltAngle
			);

		QueueRedraw();
	}

	/// <summary>
	/// Restores neutral tilt.
	/// </summary>
	public void ResetTilt()
	{
		SetTiltAngle(0.0f);
	}
}
