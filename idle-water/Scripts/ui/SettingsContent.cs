using Godot;

/// <summary>
/// Content displayed inside the SettingsWindow.
///
/// This script is responsible only for the actual settings UI.
/// The window background and border are handled by SettingsWindow.
///
/// Tilt itself comes from the device accelerometer. This UI only
/// controls how strongly the accelerometer tilt influences the simulation.
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

	private const float SliderTopOffset = 58.0f;
	private const float SliderBarHeight = 8.0f;
	private const float SliderHandleWidth = 24.0f;
	private const float SliderHandleHeight = 22.0f;
	private const float SliderSideMargin = 10.0f;
	private const float SliderHitPadding = 10.0f;

	// ============================================================
	// TILT SETTINGS
	// ============================================================

	// 0% means the accelerometer has no influence on the simulation.
	// 100% means the accelerometer is applied at full configured strength.
	private const float MinimumTiltInfluenceRatio = 0.0f;
	private const float MaximumTiltInfluenceRatio = 1.0f;
	private const float TiltInfluenceStep = 0.01f;

	private float _tiltInfluenceRatio = 0.0f;
	private bool _isDraggingTiltSlider;

	// ============================================================
	// COLORS
	// ============================================================

	private static readonly Color TitleColor =
		new Color(1.0f, 1.0f, 1.0f, 1.0f);

	private static readonly Color SeparatorColor =
		new Color(0.35f, 0.35f, 0.35f, 1.0f);

	// Old-school dark slider bar.
	private static readonly Color SliderBarColor =
		new Color(0.12f, 0.12f, 0.13f, 1.0f);

	// Slightly lighter edge so the rendered bar remains readable.
	private static readonly Color SliderBarEdgeColor =
		new Color(0.28f, 0.28f, 0.29f, 1.0f);

	// Rectangular push button / slider handle.
	private static readonly Color SliderHandleColor =
		new Color(0.72f, 0.72f, 0.72f, 1.0f);

	private static readonly Color SliderHandleHighlightColor =
		new Color(0.90f, 0.90f, 0.90f, 1.0f);

	private static readonly Color SliderHandleShadowColor =
		new Color(0.35f, 0.35f, 0.36f, 1.0f);

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
				SetTiltInfluenceFromMousePosition(mouseButton.Position.X);
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
			SetTiltInfluenceFromMousePosition(mouseMotion.Position.X);
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
			new Vector2(LeftMargin, TopMargin),
			"SETTINGS",
			HorizontalAlignment.Left,
			-1,
			TitleFontSize,
			TitleColor
		);

		// --------------------------------------------------------
		// TITLE SEPARATOR
		// --------------------------------------------------------

		float separatorY = TopMargin + 15.0f;

		DrawLine(
			new Vector2(LeftMargin, separatorY),
			new Vector2(Size.X - LeftMargin, separatorY),
			SeparatorColor,
			SeparatorWidth
		);

		// --------------------------------------------------------
		// TILT SECTION
		// --------------------------------------------------------

		float sectionY = separatorY + 35.0f;

		DrawString(
			font,
			new Vector2(LeftMargin, sectionY),
			"TILT",
			HorizontalAlignment.Left,
			-1,
			SectionFontSize,
			TitleColor
		);

		// --------------------------------------------------------
		// TILT INFLUENCE RATIO
		// --------------------------------------------------------

		DrawString(
			font,
			new Vector2(Size.X - LeftMargin, sectionY),
			$"TILT INFLUENCE {GetDisplayTiltInfluence()}%",
			HorizontalAlignment.Right,
			200.0f,
			SettingFontSize,
			TitleColor
		);

		DrawTiltInfluenceSlider(sectionY + SliderTopOffset);
	}

	// ============================================================
	// TILT INFLUENCE SLIDER
	// ============================================================

	private void DrawTiltInfluenceSlider(float sliderY)
	{
		float sliderLeft = LeftMargin + SliderSideMargin;
		float sliderRight = Size.X - LeftMargin - SliderSideMargin;
		float sliderWidth = sliderRight - sliderLeft;

		if (sliderWidth <= 0.0f)
		{
			return;
		}

		float barY = sliderY + SliderHandleHeight * 0.5f;
		float handleX = GetTiltInfluenceSliderX(sliderLeft, sliderWidth);

		// --------------------------------------------------------
		// DARK BAR
		// --------------------------------------------------------

		DrawRect(
			new Rect2(
				sliderLeft,
				barY - SliderBarHeight * 0.5f,
				sliderWidth,
				SliderBarHeight
			),
			SliderBarColor,
			true
		);

		DrawRect(
			new Rect2(
				sliderLeft,
				barY - SliderBarHeight * 0.5f,
				sliderWidth,
				SliderBarHeight
			),
			SliderBarEdgeColor,
			false,
			1.0f
		);

		// --------------------------------------------------------
		// RECTANGULAR PUSH BUTTON
		// --------------------------------------------------------

		Rect2 handleRect = new Rect2(
			handleX - SliderHandleWidth * 0.5f,
			barY - SliderHandleHeight * 0.5f,
			SliderHandleWidth,
			SliderHandleHeight
		);

		// Main face.
		DrawRect(handleRect, SliderHandleColor, true);

		// Bright top/left edge.
		DrawLine(
			handleRect.Position,
			new Vector2(handleRect.End.X, handleRect.Position.Y),
			SliderHandleHighlightColor,
			2.0f
		);

		DrawLine(
			handleRect.Position,
			new Vector2(handleRect.Position.X, handleRect.End.Y),
			SliderHandleHighlightColor,
			2.0f
		);

		// Dark bottom/right edge gives the button its old-school depth.
		DrawLine(
			new Vector2(handleRect.Position.X, handleRect.End.Y),
			handleRect.End,
			SliderHandleShadowColor,
			2.0f
		);

		DrawLine(
			new Vector2(handleRect.End.X, handleRect.Position.Y),
			handleRect.End,
			SliderHandleShadowColor,
			2.0f
		);
	}

	private bool IsMouseOverTiltSlider(Vector2 mousePosition)
	{
		float separatorY = TopMargin + 15.0f;
		float sectionY = separatorY + 35.0f;
		float sliderY = sectionY + SliderTopOffset;

		float sliderLeft = LeftMargin;
		float sliderRight = Size.X - LeftMargin;
		float sliderTop = sliderY - SliderHitPadding;
		float sliderBottom = sliderY + SliderHandleHeight + SliderHitPadding;

		return mousePosition.X >= sliderLeft &&
			mousePosition.X <= sliderRight &&
			mousePosition.Y >= sliderTop &&
			mousePosition.Y <= sliderBottom;
	}

	private void SetTiltInfluenceFromMousePosition(float mouseX)
	{
		float sliderLeft = LeftMargin + SliderSideMargin;
		float sliderRight = Size.X - LeftMargin - SliderSideMargin;
		float sliderWidth = sliderRight - sliderLeft;

		if (sliderWidth <= 0.0f)
		{
			return;
		}

		float normalized = Mathf.Clamp(
			(mouseX - sliderLeft) / sliderWidth,
			0.0f,
			1.0f
		);

		_tiltInfluenceRatio = Mathf.Snapped(
			Mathf.Lerp(
				MinimumTiltInfluenceRatio,
				MaximumTiltInfluenceRatio,
				normalized
			),
			TiltInfluenceStep
		);

		QueueRedraw();
	}

	private float GetTiltInfluenceSliderX(float sliderLeft, float sliderWidth)
	{
		return sliderLeft + sliderWidth * _tiltInfluenceRatio;
	}

	private int GetDisplayTiltInfluence()
	{
		return Mathf.RoundToInt(_tiltInfluenceRatio * 100.0f);
	}

	// ============================================================
	// PUBLIC API
	// ============================================================

	/// <summary>
	/// Returns the accelerometer influence ratio from 0.0 to 1.0.
	/// This does not represent an angle.
	/// </summary>
	public float GetTiltInfluenceRatio()
	{
		return _tiltInfluenceRatio;
	}

	/// <summary>
	/// Sets the accelerometer influence ratio from 0.0 to 1.0.
	/// </summary>
	public void SetTiltInfluenceRatio(float ratio)
	{
		_tiltInfluenceRatio = Mathf.Clamp(
			Mathf.Snapped(ratio, TiltInfluenceStep),
			MinimumTiltInfluenceRatio,
			MaximumTiltInfluenceRatio
		);

		QueueRedraw();
	}

	/// <summary>
	/// Restores the tilt influence to 0%.
	/// </summary>
	public void ResetTiltInfluence()
	{
		SetTiltInfluenceRatio(0.0f);
	}
}
