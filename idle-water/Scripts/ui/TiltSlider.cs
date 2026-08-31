using Godot;

/// <summary>
/// Renders and controls the accelerometer tilt influence slider.
/// All coordinates are local to this Control.
/// </summary>
public partial class TiltSlider : Control
{
	private const float SliderBarHeight = 8.0f;
	private const float SliderHandleWidth = 24.0f;
	private const float SliderHandleHeight = 44.0f;
	private const float SliderHitPadding = 10.0f;
	private const float MinimumTiltInfluenceRatio = 0.0f;
	private const float MaximumTiltInfluenceRatio = 1.0f;
	private const float TiltInfluenceStep = 0.01f;

	private bool _isDragging;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Stop;
		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		SizeFlagsVertical = SizeFlags.ShrinkCenter;
		CustomMinimumSize = new Vector2(280.0f, SliderHandleHeight);
		QueueRedraw();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
			QueueRedraw();
	}

	public override void _Input(InputEvent @event)
	{
		if (!Visible || !IsInsideTree())
			return;

		if (@event is InputEventMouseButton mouseButton &&
			mouseButton.ButtonIndex == MouseButton.Left)
		{
			Vector2 localPosition = GetGlobalTransform().AffineInverse() * mouseButton.GlobalPosition;

			if (mouseButton.Pressed && IsOverSlider(localPosition))
			{
				_isDragging = true;
				SetFromPosition(localPosition.X);
				GetViewport().SetInputAsHandled();
				return;
			}

			if (!mouseButton.Pressed && _isDragging)
			{
				_isDragging = false;
				GetViewport().SetInputAsHandled();
				return;
			}
		}

		if (@event is InputEventMouseMotion mouseMotion && _isDragging)
		{
			Vector2 localPosition = GetGlobalTransform().AffineInverse() * mouseMotion.GlobalPosition;
			SetFromPosition(localPosition.X);
			GetViewport().SetInputAsHandled();
		}
	}

	public override void _Draw()
	{
		if (Size.X <= 0.0f || Size.Y <= 0.0f)
			return;

		float left = SliderHandleWidth * 0.5f;
		float right = Mathf.Max(left + 1.0f, Size.X - SliderHandleWidth * 0.5f);
		float width = right - left;
		float barY = Size.Y * 0.5f;
		float handleX = left + width * TiltSettings.TiltInfluenceRatio;

		Rect2 barRect = new Rect2(left, barY - SliderBarHeight * 0.5f, width, SliderBarHeight);
		DrawRect(barRect, UiSettings.ButtonColor, true);
		DrawRect(barRect, UiSettings.BorderColor.Darkened(0.55f), false, 1.0f);

		Rect2 handleRect = new Rect2(handleX - SliderHandleWidth * 0.5f, barY - SliderHandleHeight * 0.5f, SliderHandleWidth, SliderHandleHeight);
		Color handleColor = UiSettings.FontColorBasic.Darkened(0.28f);
		Color handleHighlight = UiSettings.FontColorBasic.Darkened(0.10f);
		Color handleShadow = UiSettings.FontColorBasic.Darkened(0.65f);

		DrawRect(handleRect, handleColor, true);
		DrawLine(handleRect.Position, new Vector2(handleRect.End.X, handleRect.Position.Y), handleHighlight, 2.0f);
		DrawLine(handleRect.Position, new Vector2(handleRect.Position.X, handleRect.End.Y), handleHighlight, 2.0f);
		DrawLine(new Vector2(handleRect.Position.X, handleRect.End.Y), handleRect.End, handleShadow, 2.0f);
		DrawLine(new Vector2(handleRect.End.X, handleRect.Position.Y), handleRect.End, handleShadow, 2.0f);
	}

	private bool IsOverSlider(Vector2 position)
	{
		return position.X >= -SliderHitPadding && position.X <= Size.X + SliderHitPadding &&
			position.Y >= -SliderHitPadding && position.Y <= Size.Y + SliderHitPadding;
	}

	private void SetFromPosition(float mouseX)
	{
		float left = SliderHandleWidth * 0.5f;
		float right = Mathf.Max(left + 1.0f, Size.X - SliderHandleWidth * 0.5f);
		float width = right - left;
		float normalized = Mathf.Clamp((mouseX - left) / width, 0.0f, 1.0f);

		TiltSettings.TiltInfluenceRatio = Mathf.Clamp(
			Mathf.Snapped(Mathf.Lerp(MinimumTiltInfluenceRatio, MaximumTiltInfluenceRatio, normalized), TiltInfluenceStep),
			MinimumTiltInfluenceRatio,
			MaximumTiltInfluenceRatio
		);

		QueueRedraw();
	}
}
