using Godot;

public partial class WaterPipeVisual : Node2D
{
// ============================================================
// Configuration
// ============================================================


public float Width = 48.0f;
public float Length = 96.0f;

public float PipeAngle = 0.0f;

// ============================================================
// Pixel-art
// ============================================================

private const float Pixel = 4.0f;

// ============================================================
// Colors
// ============================================================

private static readonly Color Outline =
	new Color(0.025f, 0.045f, 0.035f, 1.0f);

private static readonly Color PipeDark =
	new Color(0.045f, 0.14f, 0.065f, 1.0f);

private static readonly Color PipeMid =
	new Color(0.08f, 0.30f, 0.13f, 1.0f);

private static readonly Color PipeGreen =
	new Color(0.12f, 0.43f, 0.17f, 1.0f);

private static readonly Color PipeLight =
	new Color(0.28f, 0.62f, 0.28f, 1.0f);

private static readonly Color Opening =
	new Color(0.008f, 0.018f, 0.012f, 1.0f);

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

public void SetPipeAngle(float angle)
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
	// MAIN PIPE OUTLINE
	// ========================================================

	DrawRect(
		new Rect2(
			0.0f,
			-halfWidth - Pixel,
			Length,
			Width + Pixel * 2.0f
		),
		Outline
	);

	// ========================================================
	// MAIN PIPE BODY
	// ========================================================

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
	// TOP LIGHT
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
			halfWidth - Pixel * 5.0f,
			Length - Pixel * 4.0f,
			Pixel * 3.0f
		),
		PipeDark
	);

	// ========================================================
	// FRONT COLLAR
	// ========================================================

	float collarWidth =
		16.0f;

	float collarX =
		Length - collarWidth;

	DrawRect(
		new Rect2(
			collarX - Pixel,
			-halfWidth - Pixel * 2.0f,
			collarWidth + Pixel * 2.0f,
			Width + Pixel * 4.0f
		),
		Outline
	);

	DrawRect(
		new Rect2(
			collarX,
			-halfWidth,
			collarWidth,
			Width
		),
		PipeGreen
	);

	// Collar highlight

	DrawRect(
		new Rect2(
			collarX + Pixel,
			-halfWidth + Pixel,
			Pixel * 2.0f,
			Width - Pixel * 2.0f
		),
		PipeLight
	);

	// ========================================================
	// SIDE OPENING
	//
	// The opening is on the RIGHT end of the pipe.
	// ========================================================

	float openingWidth =
		Width - Pixel * 10.0f;

	float openingX =
		Length - Pixel * 2.0f;

	// Dark rectangular opening

	DrawRect(
		new Rect2(
			openingX,
			-openingWidth * 0.5f,
			Pixel * 4.0f,
			openingWidth
		),
		Opening
	);

	// ========================================================
	// OPENING TOP LIP
	// ========================================================

	DrawRect(
		new Rect2(
			Length - Pixel * 3.0f,
			-halfWidth - Pixel,
			Pixel * 4.0f,
			Pixel * 3.0f
		),
		PipeLight
	);

	// ========================================================
	// OPENING BOTTOM LIP
	// ========================================================

	DrawRect(
		new Rect2(
			Length - Pixel * 3.0f,
			halfWidth - Pixel * 2.0f,
			Pixel * 4.0f,
			Pixel * 3.0f
		),
		PipeDark
	);

	// ========================================================
	// LEFT REAR CAP
	// ========================================================

	DrawRect(
		new Rect2(
			0.0f,
			-halfWidth,
			Pixel * 2.0f,
			Width
		),
		PipeDark
	);

	DrawRect(
		new Rect2(
			Pixel * 2.0f,
			-halfWidth + Pixel * 2.0f,
			Pixel,
			Width - Pixel * 4.0f
		),
		PipeGreen
	);

	// ========================================================
	// SIMPLE PIXEL DETAILS
	// ========================================================

	DrawRect(
		new Rect2(
			Length * 0.30f,
			-halfWidth + Pixel * 5.0f,
			20.0f,
			Pixel * 2.0f
		),
		PipeGreen
	);

	DrawRect(
		new Rect2(
			Length * 0.55f,
			halfWidth - Pixel * 5.0f,
			16.0f,
			Pixel
		),
		PipeDark
	);
}


}
