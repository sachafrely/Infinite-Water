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

	private const float TiltLabelWidth = 80.0f;
	// Move the complete Tilt settings area higher to use the free space above it.
	private const float SliderTopOffset = 28.0f;
	private const float SliderBarHeight = 8.0f;
	private const float SliderHandleWidth = 24.0f;
	private const float SliderHandleHeight = 44.0f;
	private const float SliderLeft = 115.0f;
	// Leave an additional 60px of space on the right for the GravityIndicator.
	private const float SliderRightMargin = 165.0f;
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
		if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left)
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

		DrawString(font, new Vector2(LeftMargin, TopMargin), "SETTINGS", HorizontalAlignment.Left, -1, UiSettings.FontSizeBig, UiSettings.FontColorBasic);

		float separatorY = TopMargin + 15.0f;
		DrawLine(new Vector2(LeftMargin, separatorY), new Vector2(Size.X - LeftMargin, separatorY), UiSettings.BorderColor.Darkened(0.5f), SeparatorWidth);

		float sectionY = separatorY + 35.0f;
		float sliderY = sectionY + SliderTopOffset;
		float sliderLeft = GetSliderLeft();
		float sliderRight = GetSliderRight();
		float barY = sliderY + SliderHandleHeight * 0.5f;

		// TILT is intentionally placed to the left of the shortened slider.
		// The old right-side "TILT INFLUENCE ..." text is removed.
		DrawString(
			font,
			new Vector2(LeftMargin, barY + UiSettings.FontSizeMedium * 0.35f),
			"TILT",
			HorizontalAlignment.Left,
			TiltLabelWidth,
			UiSettings.FontSizeMedium,
			UiSettings.FontColorBasic
		);

		DrawTiltInfluenceSlider(sliderY, sliderLeft, sliderRight);
	}

	private float GetSliderLeft()
	{
		return SliderLeft;
	}

	private float GetSliderRight()
	{
		return Mathf.Max(GetSliderLeft() + 120.0f, Size.X - SliderRightMargin);
	}

	private void DrawTiltInfluenceSlider(float sliderY, float sliderLeft, float sliderRight)
	{
		float sliderWidth = sliderRight - sliderLeft;
		if (sliderWidth <= 0.0f)
			return;

		float barY = sliderY + SliderHandleHeight * 0.5f;
		float handleX = GetTiltInfluenceSliderX(sliderLeft, sliderWidth);

		Rect2 barRect = new Rect2(sliderLeft, barY - SliderBarHeight * 0.5f, sliderWidth, SliderBarHeight);
		DrawRect(barRect, UiSettings.ButtonColor, true);
		DrawRect(barRect, UiSettings.BorderColor.Darkened(0.55f), false, 1.0f);

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
		DrawLine(handleRect.Position, new Vector2(handleRect.End.X, handleRect.Position.Y), handleHighlight, 2.0f);
		DrawLine(handleRect.Position, new Vector2(handleRect.Position.X, handleRect.End.Y), handleHighlight, 2.0f);
		DrawLine(new Vector2(handleRect.Position.X, handleRect.End.Y), handleRect.End, handleShadow, 2.0f);
		DrawLine(new Vector2(handleRect.End.X, handleRect.Position.Y), handleRect.End, handleShadow, 2.0f);
	}

	private bool IsMouseOverTiltSlider(Vector2 mousePosition)
	{
		float separatorY = TopMargin + 15.0f;
		float sectionY = separatorY + 35.0f;
		float sliderY = sectionY + SliderTopOffset;

		float sliderLeft = GetSliderLeft() - SliderHitPadding;
		float sliderRight = GetSliderRight() + SliderHitPadding;
		float sliderTop = sliderY - SliderHitPadding;
		float sliderBottom = sliderY + SliderHandleHeight + SliderHitPadding;

		return mousePosition.X >= sliderLeft && mousePosition.X <= sliderRight && mousePosition.Y >= sliderTop && mousePosition.Y <= sliderBottom;
	}

	private void SetTiltInfluenceFromMousePosition(float mouseX)
	{
		float sliderLeft = GetSliderLeft();
		float sliderRight = GetSliderRight();
		float sliderWidth = sliderRight - sliderLeft;
		if (sliderWidth <= 0.0f)
			return;

		float normalized = Mathf.Clamp((mouseX - sliderLeft) / sliderWidth, 0.0f, 1.0f);
		SetTiltInfluenceRatio(Mathf.Lerp(MinimumTiltInfluenceRatio, MaximumTiltInfluenceRatio, normalized));
	}

	private float GetTiltInfluenceSliderX(float sliderLeft, float sliderWidth)
	{
		return sliderLeft + sliderWidth * TiltSettings.TiltInfluenceRatio;
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
