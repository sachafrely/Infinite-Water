using Godot;

[Tool]
public partial class RenderedWindowBackground : Control
{
	[Export]
	public int BorderWidth
	{
		get => _borderWidth;
		set
		{
			_borderWidth = Mathf.Max(0, value);
			QueueRedraw();
		}
	}

	[Export]
	public Color FillColor
	{
		get => _fillColor;
		set
		{
			_fillColor = value;
			QueueRedraw();
		}
	}

	[Export]
	public Color BorderColor
	{
		get => _borderColor;
		set
		{
			_borderColor = value;
			QueueRedraw();
		}
	}

	private int _borderWidth = (int)UiSettings.BorderSize;

	private Color _fillColor = UiSettings.WindowColor;

	private Color _borderColor = UiSettings.BorderColor;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
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

		// Main background.
		DrawRect(
			new Rect2(Vector2.Zero, Size),
			UiSettings.WindowColor,
			true
		);

		// Border.
		float w = UiSettings.BorderSize;

		if (w > 0.0f)
		{
			DrawRect(
				new Rect2(0, 0, Size.X, w),
				UiSettings.BorderColor,
				true
			);

			DrawRect(
				new Rect2(0, Size.Y - w, Size.X, w),
				UiSettings.BorderColor,
				true
			);

			DrawRect(
				new Rect2(0, 0, w, Size.Y),
				UiSettings.BorderColor,
				true
			);

			DrawRect(
				new Rect2(Size.X - w, 0, w, Size.Y),
				UiSettings.BorderColor,
				true
			);
		}
	}
}
