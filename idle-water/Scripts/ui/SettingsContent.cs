using Godot;

/// <summary>
/// Content displayed inside the SettingsWindow.
///
/// This script renders the settings UI. Tilt itself comes from the device
/// accelerometer; this UI only controls its influence ratio.
/// Shared visual configuration comes from UiSettings.
/// </summary>
public partial class SettingsContent : Control
{
	private const float LeftMargin = 20.0f;
	private const float TopMargin = 30.0f;
	private const float SeparatorWidth = UiSettings.BorderSize;

	private const float SliderTopOffset = 58.0f;
	private const float SliderBarHeight = 8.0f;
	private const float SliderHandleWidth = 24.0f;
	private const float SliderHandleHeight = 22.0f;
	private const float SliderSideMargin = 10.0f;
	private const float SliderHitPadding = 10.0f;

	private const float MinimumTiltInfluenceRatio = 0.0f;
	private const float MaximumTiltInfluenceRatio = 1.0f;
	private const float TiltInfluenceStep = 0.01f;

	private bool _isDraggingTiltSlider;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Stop;
		QueueRedraw();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
			QueueRedraw();
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

	public override void _Draw()
	{
		if (Size.X <= 0.0f || Size.Y <= 0.0f)
			return;

		Font font = ThemeDB.FallbackFont;

		DrawString(
			font,
			new Vector2(LeftMargin, TopMargin),
			"SETTINGS",
			HorizontalAlignment.Left,
			-1,
			UiSettings.FontSizeBig,
			UiSettings.FontColorBasic
		);

		float separatorY = TopMargin + 15.0f;

		DrawLine(
			new Vector2(LeftMargin, separatorY),
			new Vector2(Size.X - LeftMargin, separatorY),
			UiSettings.BorderColor.Darkened(0.5f),
			SeparatorWidth
		);

		float sectionY = separatorY + 35.0f;

		DrawString(
			font,
			new Vector2(LeftMargin, sectionY),
			"TILT",
			HorizontalAlignment.Left,
			-1,
			UiSettings.FontSizeMedium,
			UiSettings.FontColorBasic
		);

		DrawString(
			font,
			new Vector2(Size.X - LeftMargin, sectionY),
			$"TILT INFLUENCE {GetDisplayTiltInfluence()}%",
			HorizontalAlignment.Right,
			200.0f,
			UiSettings.FontSizeSmall,
			UiSettings.FontColorBasic
		);

		DrawTiltInfluenceSlider(sectionY + SliderTopOffset);
	}

	private void DrawTiltInfluenceSlider(float sliderY)
	{
		float sliderLeft = LeftMargin + SliderSideMargin;
		float sliderRight = Size.X - LeftMargin - SliderSideMargin;
		float sliderWidth = sliderRight - sliderLeft;

		if (sliderWidth <= 0.0f)
			return;

		float barY = sliderY + SliderHandleHeight * 0.5f;
		float handleX = GetTiltInfluenceSliderX(sliderLeft, sliderWidth);

		DrawRect(
			new Rect2(
				sliderLeft,
				barY - SliderBarHeight * 0.5f,
				sliderWidth,
				SliderBarHeight
			),
			UiSettings.ButtonColor,
			true
		);

		DrawRect(
			new Rect2(
				sliderLeft,
				barY - SliderBarHeight * 0.5f,
				sliderWidth,
				SliderBarHeight
			),
			UiSettings.BorderColor.Darkened(0.55f),
			false,
			1.0f
		);

		Rect2 handleRect = new Rect2(
			handleX - SliderHandleWidth * 0.5f,
			barY - SliderHandleHeight * 0.5f,
			SliderHandleWidth,
			SliderHandleHeight
		);

		Color handleColor = UiSettings.FontColorBasic.Darkened(0.28f);
		Color handleHighlight = UiSettings.FontColorBasic.Darkened(0.10f);
		Color handleShadow = UiSettings.FontColorBasic.Darkened(0.65f);

		DrawRect(handleRect, handleColor, true);

		DrawLine(
			handleRect.Position,
			new Vector2(handleRect.End.X, handleRect.Position.Y),
			handleHighlight,
			2.0f
		);

		DrawLine(
			handleRect.Position,
			new Vector2(handleRect.Position.X, handleRect.End.Y),
			handleHighlight,
			2.0f
		);

		DrawLine(
			new Vector2(handleRect.Position.X, handleRect.End.Y),
			handleRect.End,
			handleShadow,
			2.0f
		);

		DrawLine(
			new Vector2(handleRect.End.X, handleRect.Position.Y),
			handleRect.End,
			handleShadow,
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
			return;

		float normalized = Mathf.Clamp(
			(mouseX - sliderLeft) / sliderWidth,
			0.0f,
			1.0f
		);

		SetTiltInfluenceRatio(
			Mathf.Lerp(
				MinimumTiltInfluenceRatio,
				MaximumTiltInfluenceRatio,
				normalized
			)
		);
	}

	private float GetTiltInfluenceSliderX(float sliderLeft, float sliderWidth)
	{
		return sliderLeft + sliderWidth * TiltSettings.TiltInfluenceRatio;
	}

	private int GetDisplayTiltInfluence()
	{
		return Mathf.RoundToInt(TiltSettings.TiltInfluenceRatio * 100.0f);
	}

	public float GetTiltInfluenceRatio()
	{
		return TiltSettings.TiltInfluenceRatio;
	}

	public void SetTiltInfluenceRatio(float ratio)
	{
		TiltSettings.TiltInfluenceRatio = Mathf.Clamp(
			Mathf.Snapped(ratio, TiltInfluenceStep),
			MinimumTiltInfluenceRatio,
			MaximumTiltInfluenceRatio
		);

		QueueRedraw();
	}

	public void ResetTiltInfluence()
	{
		SetTiltInfluenceRatio(0.0f);
	}
}
