using Godot;

[Tool]
public partial class RenderedWindowBackground : Control
{
	[Export] public int BorderWidth { get => _borderWidth; set { _borderWidth = Mathf.Max(0, value); QueueRedraw(); } }
	[Export] public Color FillColor { get => _fillColor; set { _fillColor = value; QueueRedraw(); } }
	[Export] public Color BorderColor { get => _borderColor; set { _borderColor = value; QueueRedraw(); } }

	[Export] public bool UseGradient { get => _useGradient; set { _useGradient = value; QueueRedraw(); } }
	[Export] public Color GradientTop { get => _gradientTop; set { _gradientTop = value; QueueRedraw(); } }
	[Export] public Color GradientBottom { get => _gradientBottom; set { _gradientBottom = value; QueueRedraw(); } }
	[Export] public bool GradientVertical { get => _gradientVertical; set { _gradientVertical = value; QueueRedraw(); } }

	[Export] public bool DrawShadow { get => _drawShadow; set { _drawShadow = value; QueueRedraw(); } }
	[Export] public Vector2 ShadowOffset { get => _shadowOffset; set { _shadowOffset = value; QueueRedraw(); } }
	[Export] public Color ShadowColor { get => _shadowColor; set { _shadowColor = value; QueueRedraw(); } }

	private int _borderWidth = 2;
	private Color _fillColor = new("1a2230d9");
	private Color _borderColor = new("7fa6c9ff");
	private bool _useGradient = true;
	private Color _gradientTop = new("243247dd");
	private Color _gradientBottom = new("121a26dd");
	private bool _gradientVertical = true;
	private bool _drawShadow = true;
	private Vector2 _shadowOffset = new(2, 2);
	private Color _shadowColor = new(0, 0, 0, 0.35f);

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint())
			QueueRedraw(); // live in editor
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
			QueueRedraw();
	}

	public override void _Draw()
	{
		if (Size.X < 1 || Size.Y < 1) return;

		var r = new Rect2(Vector2.Zero, Size);

		if (DrawShadow)
			DrawRect(new Rect2(r.Position + ShadowOffset, r.Size), ShadowColor, true);

		if (UseGradient) DrawGradientRect(r, GradientTop, GradientBottom, GradientVertical);
		else DrawRect(r, FillColor, true);

		if (BorderWidth > 0) DrawBorder(r, BorderColor, BorderWidth);
	}

	private void DrawGradientRect(Rect2 r, Color c1, Color c2, bool vertical)
	{
		var points = new[] {
			r.Position,
			r.Position + new Vector2(r.Size.X, 0),
			r.Position + r.Size,
			r.Position + new Vector2(0, r.Size.Y)
		};

		var colors = vertical
			? new[] { c1, c1, c2, c2 }
			: new[] { c1, c2, c2, c1 };

		DrawPolygon(points, colors);
	}

	private void DrawBorder(Rect2 r, Color c, int w)
	{
		DrawRect(new Rect2(r.Position, new Vector2(r.Size.X, w)), c, true); // top
		DrawRect(new Rect2(r.Position + new Vector2(0, r.Size.Y - w), new Vector2(r.Size.X, w)), c, true); // bottom
		DrawRect(new Rect2(r.Position, new Vector2(w, r.Size.Y)), c, true); // left
		DrawRect(new Rect2(r.Position + new Vector2(r.Size.X - w, 0), new Vector2(w, r.Size.Y)), c, true); // right
	}
}
