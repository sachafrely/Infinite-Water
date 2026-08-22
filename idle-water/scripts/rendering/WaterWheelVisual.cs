using Godot;

public partial class WaterWheelVisual : Node2D
{
	public float OuterRadius = 145.0f;
	public float InnerRadius = 75.0f;
	public float PaddleInnerRadius = 75.0f;
	public int BladeCount = 10;
	public float BladeWidth = 18.0f;
	public float WheelAngle { get; set; }

	private const int PixelSize = 4;
	private const int ViewportMargin = 8;
	private const float VisualSmoothing = 20.0f;
	private float targetAngle;
	private float visualAngle;
	private SubViewport wheelViewport;
	private WheelPainter wheelPainter;
	private Sprite2D wheelSprite;
	private int viewportSize;

	private static readonly Color Outline = new Color(0.035f, 0.055f, 0.055f, 1.0f);
	private static readonly Color DarkMetal = new Color(0.08f, 0.12f, 0.12f, 1.0f);
	private static readonly Color Metal = new Color(0.16f, 0.22f, 0.21f, 1.0f);
	private static readonly Color MetalLight = new Color(0.24f, 0.30f, 0.28f, 1.0f);
	private static readonly Color Wood = new Color(0.27f, 0.20f, 0.13f, 1.0f);
	private static readonly Color WoodLight = new Color(0.38f, 0.28f, 0.17f, 1.0f);
	private static readonly Color HubDark = new Color(0.07f, 0.12f, 0.10f, 1.0f);
	private static readonly Color Hole = new Color(0.02f, 0.035f, 0.035f, 1.0f);

	public override void _Ready()
	{
		ZIndex = 10;
		SetupPixelViewport();
		UpdatePainter();
	}

	public void SetGeometry(float outerRadius, float paddleInnerRadius, float bladeWidth)
	{
		OuterRadius = outerRadius;
		PaddleInnerRadius = paddleInnerRadius;
		BladeWidth = bladeWidth;

		if (wheelViewport == null || wheelPainter == null)
			return;

		ResizePixelViewport();
		UpdatePainter();
	}

	private void SetupPixelViewport()
	{
		ResizePixelViewport();
		wheelViewport = new SubViewport
		{
			Name = "WheelPixelViewport",
			TransparentBg = true,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
			CanvasItemDefaultTextureFilter = Viewport.DefaultCanvasItemTextureFilter.Nearest
		};
		AddChild(wheelViewport);

		wheelPainter = new WheelPainter { Name = "WheelPainter", Wheel = this };
		wheelViewport.AddChild(wheelPainter);
		ResizePixelViewport();

		wheelSprite = new Sprite2D
		{
			Name = "WheelPixelSprite",
			Texture = wheelViewport.GetTexture(),
			Centered = true,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			Scale = new Vector2(PixelSize, PixelSize),
			Position = Vector2.Zero
		};
		AddChild(wheelSprite);
	}

	private void ResizePixelViewport()
	{
		float worldDiameter = OuterRadius * 2.0f + 12.0f;
		viewportSize = Mathf.Max(Mathf.CeilToInt(worldDiameter / PixelSize) + ViewportMargin * 2, 16);

		if (wheelViewport != null)
			wheelViewport.Size = new Vector2I(viewportSize, viewportSize);

		if (wheelPainter != null)
			wheelPainter.Position = new Vector2(viewportSize * 0.5f, viewportSize * 0.5f);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		float difference = Mathf.Wrap(targetAngle - visualAngle, -Mathf.Pi, Mathf.Pi);
		visualAngle += difference * (1.0f - Mathf.Exp(-VisualSmoothing * dt));
		UpdatePainter();
	}

	public void SetWheelAngle(float angle)
	{
		targetAngle = angle;
		WheelAngle = angle;
	}

	private void UpdatePainter()
	{
		if (wheelPainter == null) return;
		wheelPainter.Angle = visualAngle;
		wheelPainter.QueueRedraw();
	}

	private sealed partial class WheelPainter : Node2D
	{
		public WaterWheelVisual Wheel;
		public float Angle;

		public override void _Draw()
		{
			if (Wheel == null) return;
			float outer = Wheel.OuterRadius / PixelSize;
			float inner = Wheel.InnerRadius / PixelSize;
			float paddleInner = Wheel.PaddleInnerRadius / PixelSize;
			float bladeWidth = Wheel.BladeWidth / PixelSize;

			DrawCircle(Vector2.Zero, outer + 1.5f, Outline);
			DrawCircle(Vector2.Zero, outer, DarkMetal);
			DrawArc(Vector2.Zero, outer - 0.5f, 0.0f, Mathf.Tau, 32, Metal, 2.0f, false);
			DrawArc(Vector2.Zero, outer - 2.0f, 0.0f, Mathf.Tau, 32, Outline, 1.25f, false);

			for (int i = 0; i < Wheel.BladeCount; i++)
			{
				float angle = Angle + Mathf.Tau * i / Wheel.BladeCount;
				DrawPixelBlade(angle, paddleInner, outer, bladeWidth);
			}

			DrawCircle(Vector2.Zero, inner + 1.25f, Outline);
			DrawCircle(Vector2.Zero, inner, Wood);
			DrawArc(Vector2.Zero, inner - 1.0f, 0.0f, Mathf.Tau, 24, WoodLight, 1.25f, false);
			DrawCircle(Vector2.Zero, 23.0f / PixelSize, Outline);
			DrawCircle(Vector2.Zero, 19.0f / PixelSize, HubDark);
			DrawRect(new Rect2(-9.0f / PixelSize, -9.0f / PixelSize, 18.0f / PixelSize, 6.0f / PixelSize), Metal);
			DrawCircle(Vector2.Zero, 11.0f / PixelSize, Hole);
			DrawCircle(Vector2.Zero, 11.0f / PixelSize, Outline, false, 1.0f);
			DrawRect(new Rect2(-5.0f / PixelSize, -5.0f / PixelSize, 10.0f / PixelSize, 5.0f / PixelSize), MetalLight);
		}

		private void DrawPixelBlade(float angle, float innerRadius, float outerRadius, float width)
		{
			Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
			Vector2 tangent = new Vector2(-direction.Y, direction.X);
			Vector2 inner = direction * (innerRadius - 0.5f);
			Vector2 outer = direction * (outerRadius - 1.75f);
			Vector2 p1 = inner + tangent * width;
			Vector2 p2 = outer + tangent * width;
			Vector2 p3 = outer - tangent * width;
			Vector2 p4 = inner - tangent * width;
			Vector2[] outline = { p1 + direction * 0.75f, p2 + direction * 0.75f, p3 - direction * 0.75f, p4 - direction * 0.75f };
			DrawColoredPolygon(outline, Outline);
			Vector2[] blade = { p1, p2, p3, p4 };
			DrawColoredPolygon(blade, Metal);
			DrawLine(inner + tangent * (width * 0.55f), outer + tangent * (width * 0.55f), MetalLight, 1.0f, false);
			DrawLine(inner - tangent * (width * 0.55f), outer - tangent * (width * 0.55f), DarkMetal, 1.0f, false);
		}
	}
}
