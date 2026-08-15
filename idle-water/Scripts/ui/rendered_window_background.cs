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

	private int _borderWidth = 3;

	private Color _fillColor = new Color(
		0.10f,
		0.13f,
		0.18f,
		0.95f
	);

	private Color _borderColor = new Color(
		0.50f,
		0.65f,
		0.80f,
		1.0f
	);

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
			FillColor,
			true
		);

		// Border.
		if (BorderWidth > 0)
		{
			float w = BorderWidth;

			// Top
			DrawRect(
				new Rect2(
					0,
					0,
					Size.X,
					w
				),
				BorderColor,
				true
			);

			// Bottom
			DrawRect(
				new Rect2(
					0,
					Size.Y - w,
					Size.X,
					w
				),
				BorderColor,
				true
			);

			// Left
			DrawRect(
				new Rect2(
					0,
					0,
					w,
					Size.Y
				),
				BorderColor,
				true
			);

			// Right
			DrawRect(
				new Rect2(
					Size.X - w,
					0,
					w,
					Size.Y
				),
				BorderColor,
				true
			);
		}
	}
}
