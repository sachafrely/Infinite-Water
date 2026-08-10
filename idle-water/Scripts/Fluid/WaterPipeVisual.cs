
using Godot;

public partial class WaterPipeVisual : Node2D
{
	// ============================================================
	// Configuration
	// ============================================================

	public float Width = 96.0f;
	public float Length = 144.0f;

	public float PipeAngle = 90.0f;

	// ============================================================
	// Pixel-art appearance
	// ============================================================

	private const float Pixel = 4.0f;

	private readonly Color PipeDark =
		new Color(0.04f, 0.16f, 0.08f, 1.0f);

	private readonly Color PipeShadow =
		new Color(0.02f, 0.09f, 0.04f, 1.0f);

	private readonly Color PipeMid =
		new Color(0.08f, 0.32f, 0.14f, 1.0f);

	private readonly Color PipeGreen =
		new Color(0.12f, 0.48f, 0.20f, 1.0f);

	private readonly Color PipeLight =
		new Color(0.25f, 0.65f, 0.30f, 1.0f);

	private readonly Color OpeningDark =
		new Color(0.015f, 0.035f, 0.02f, 1.0f);

	// ============================================================
	// Ready
	// ============================================================

	public override void _Ready()
	{
		Rotation =
			Mathf.DegToRad(PipeAngle);

		QueueRedraw();
	}

	// ============================================================
	// Set angle
	// ============================================================

	public void SetPipeAngle(
		float angle)
	{
		PipeAngle =
			angle;

		Rotation =
			Mathf.DegToRad(angle);

		QueueRedraw();
	}

	// ============================================================
	// Draw
	// ============================================================

	public override void _Draw()
	{
		float halfWidth =
			Width * 0.5f;

		// ========================================================
		// OUTER SHADOW
		// ========================================================

		DrawRect(
			new Rect2(
				-4.0f,
				-halfWidth - Pixel,
				Length + 8.0f,
				Width + Pixel * 2.0f
			),
			PipeShadow
		);

		// ========================================================
		// MAIN PIPE BODY
		// ========================================================

		DrawRect(
			new Rect2(
				0.0f,
				-halfWidth,
				Length,
				Width
			),
			PipeDark
		);

		DrawRect(
			new Rect2(
				Pixel,
				-halfWidth + Pixel,
				Length - Pixel * 2.0f,
				Width - Pixel * 2.0f
			),
			PipeMid
		);

		// ========================================================
		// LARGE TOP HIGHLIGHT
		// ========================================================

		DrawRect(
			new Rect2(
				Pixel * 2.0f,
				-halfWidth + Pixel * 2.0f,
				Length - Pixel * 4.0f,
				Pixel * 3.0f
			),
			PipeLight
		);

		// ========================================================
		// LOWER SHADOW
		// ========================================================

		DrawRect(
			new Rect2(
				Pixel * 2.0f,
				halfWidth - Pixel * 4.0f,
				Length - Pixel * 4.0f,
				Pixel * 3.0f
			),
			PipeShadow
		);

		// ========================================================
		// PIXEL PIPE SEGMENTS
		// ========================================================

		float segmentSize =
			32.0f;

		for (
			float x = 20.0f;
			x < Length - 12.0f;
			x += segmentSize)
		{
			DrawRect(
				new Rect2(
					x,
					-halfWidth + Pixel * 2.0f,
					Pixel * 2.0f,
					Width - Pixel * 4.0f
				),
				PipeDark
			);
		}

		// ========================================================
		// REINFORCED COLLAR
		// ========================================================

		float collarLength =
			30.0f;

		DrawRect(
			new Rect2(
				Length - collarLength,
				-halfWidth - Pixel * 2.0f,
				collarLength,
				Width + Pixel * 4.0f
			),
			PipeDark
		);

		DrawRect(
			new Rect2(
				Length - collarLength + Pixel * 2.0f,
				-halfWidth,
				collarLength - Pixel * 4.0f,
				Width
			),
			PipeGreen
		);

		// ========================================================
		// COLLAR HIGHLIGHT
		// ========================================================

		DrawRect(
			new Rect2(
				Length - collarLength + Pixel * 3.0f,
				-halfWidth + Pixel * 2.0f,
				Pixel * 3.0f,
				Width - Pixel * 4.0f
			),
			PipeLight
		);

		// ========================================================
		// PIPE OPENING
		// ========================================================

		float openingWidth =
			Width - Pixel * 10.0f;

		DrawRect(
			new Rect2(
				Length - Pixel * 2.0f,
				-openingWidth * 0.5f,
				Pixel * 4.0f,
				openingWidth
			),
			OpeningDark
		);

		// ========================================================
		// OPENING TOP EDGE
		// ========================================================

		DrawRect(
			new Rect2(
				Length - Pixel * 3.0f,
				-halfWidth - Pixel * 2.0f,
				Pixel * 4.0f,
				Pixel * 3.0f
			),
			PipeLight
		);

		// ========================================================
		// OPENING BOTTOM EDGE
		// ========================================================

		DrawRect(
			new Rect2(
				Length - Pixel * 3.0f,
				halfWidth - Pixel,
				Pixel * 4.0f,
				Pixel * 3.0f
			),
			PipeShadow
		);

		// ========================================================
		// MOUNTING BLOCKS
		// ========================================================

		DrawRect(
			new Rect2(
				12.0f,
				-halfWidth - Pixel * 2.0f,
				32.0f,
				Pixel * 3.0f
			),
			PipeDark
		);

		DrawRect(
			new Rect2(
				12.0f,
				halfWidth - Pixel,
				32.0f,
				Pixel * 3.0f
			),
			PipeDark
		);

		// ========================================================
		// SMALL GREEN HIGHLIGHT PIXELS
		// ========================================================

		DrawRect(
			new Rect2(
				20.0f,
				-halfWidth + Pixel * 5.0f,
				16.0f,
				Pixel * 2.0f
			),
			PipeGreen
		);

		DrawRect(
			new Rect2(
				Length * 0.45f,
				-halfWidth + Pixel * 5.0f,
				20.0f,
				Pixel * 2.0f
			),
			PipeGreen
		);
	}
}
