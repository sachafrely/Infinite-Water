using Godot;

public partial class WaterWheelVisual : Node2D
{
// ============================================================
// Configuration
// ============================================================


public float OuterRadius = 115.0f;
public float InnerRadius = 55.0f;
public int BladeCount = 10;
public float BladeWidth = 18.0f;

public float WheelAngle
{
	get;
	set;
}

// ============================================================
// Pixel-art configuration
// ============================================================

private const float PixelSize = 4.0f;

// ============================================================
// Visual smoothing
// ============================================================

private const float VisualSmoothing = 20.0f;

private float targetAngle = 0.0f;
private float visualAngle = 0.0f;

// ============================================================
// Colors
// ============================================================

private static readonly Color Outline =
	new Color(
		0.035f,
		0.055f,
		0.055f,
		1.0f
	);

private static readonly Color DarkMetal =
	new Color(
		0.08f,
		0.12f,
		0.12f,
		1.0f
	);

private static readonly Color Metal =
	new Color(
		0.16f,
		0.22f,
		0.21f,
		1.0f
	);

private static readonly Color MetalLight =
	new Color(
		0.24f,
		0.30f,
		0.28f,
		1.0f
	);

private static readonly Color Wood =
	new Color(
		0.27f,
		0.20f,
		0.13f,
		1.0f
	);

private static readonly Color WoodLight =
	new Color(
		0.38f,
		0.28f,
		0.17f,
		1.0f
	);

private static readonly Color HubDark =
	new Color(
		0.07f,
		0.12f,
		0.10f,
		1.0f
	);

private static readonly Color Hole =
	new Color(
		0.02f,
		0.035f,
		0.035f,
		1.0f
	);

// ============================================================
// Ready
// ============================================================

public override void _Ready()
{
	QueueRedraw();
}

// ============================================================
// Visual update
//
// IMPORTANT:
//
// This only smooths the visual wheel.
// The physics wheel remains controlled by the solver.
// ============================================================

public override void _Process(double delta)
{
	float dt =
		(float)delta;

	float difference =
		Mathf.Wrap(
			targetAngle -
			visualAngle,
			-Mathf.Pi,
			Mathf.Pi
		);

	visualAngle +=
		difference *
		(
			1.0f -
			Mathf.Exp(
				-VisualSmoothing *
				dt
			)
		);

	QueueRedraw();
}

// ============================================================
// Physics update
// ============================================================

public void SetWheelAngle(
	float angle)
{
	targetAngle =
		angle;

	WheelAngle =
		angle;
}

// ============================================================
// Pixel snapping
// ============================================================

private Vector2 Snap(
	Vector2 position)
{
	return new Vector2(
		Mathf.Round(
			position.X /
			PixelSize
		) *
		PixelSize,

		Mathf.Round(
			position.Y /
			PixelSize
		) *
		PixelSize
	);
}

private float Snap(
	float value)
{
	return Mathf.Round(
		value /
		PixelSize
	) *
	PixelSize;
}

// ============================================================
// Draw
// ============================================================

public override void _Draw()
{
	float outer =
		Snap(
			OuterRadius
		);

	float inner =
		Snap(
			InnerRadius
		);

	// --------------------------------------------------------
	// Outer wheel silhouette
	// --------------------------------------------------------

	DrawCircle(
		Vector2.Zero,
		outer + 6.0f,
		Outline
	);

	DrawCircle(
		Vector2.Zero,
		outer,
		DarkMetal
	);

	// --------------------------------------------------------
	// Outer rim highlight
	// --------------------------------------------------------

	DrawArc(
		Vector2.Zero,
		outer - 2.0f,
		0.0f,
		Mathf.Tau,
		32,
		Metal,
		8.0f,
		false
	);

	DrawArc(
		Vector2.Zero,
		outer - 8.0f,
		0.0f,
		Mathf.Tau,
		32,
		Outline,
		5.0f,
		false
	);

	// --------------------------------------------------------
	// Blades
	// --------------------------------------------------------

	for (
		int i = 0;
		i < BladeCount;
		i++)
	{
		float angle =
			visualAngle +
			Mathf.Tau *
			i /
			BladeCount;

		DrawPixelBlade(
			angle,
			inner,
			outer
		);
	}

	// --------------------------------------------------------
	// Inner wheel ring
	// --------------------------------------------------------

	DrawCircle(
		Vector2.Zero,
		inner + 5.0f,
		Outline
	);

	DrawCircle(
		Vector2.Zero,
		inner,
		Wood
	);

	// --------------------------------------------------------
	// Wooden ring highlight
	// --------------------------------------------------------

	DrawArc(
		Vector2.Zero,
		inner - 4.0f,
		0.0f,
		Mathf.Tau,
		24,
		WoodLight,
		5.0f,
		false
	);

	// --------------------------------------------------------
	// Hub
	// --------------------------------------------------------

	DrawCircle(
		Vector2.Zero,
		23.0f,
		Outline
	);

	DrawCircle(
		Vector2.Zero,
		19.0f,
		HubDark
	);

	// --------------------------------------------------------
	// Hub highlight
	// --------------------------------------------------------

	DrawRect(
		new Rect2(
			-9.0f,
			-9.0f,
			18.0f,
			6.0f
		),
		Metal
	);

	// --------------------------------------------------------
	// Center hole
	// --------------------------------------------------------

	DrawCircle(
		Vector2.Zero,
		11.0f,
		Hole
	);

	DrawCircle(
		Vector2.Zero,
		11.0f,
		Outline,
		false,
		4.0f
	);

	// --------------------------------------------------------
	// Center highlight
	// --------------------------------------------------------

	DrawRect(
		new Rect2(
			-5.0f,
			-5.0f,
			10.0f,
			5.0f
		),
		MetalLight
	);
}

// ============================================================
// Pixel blade
// ============================================================

private void DrawPixelBlade(
	float angle,
	float innerRadius,
	float outerRadius)
{
	Vector2 direction =
		new Vector2(
			Mathf.Cos(angle),
			Mathf.Sin(angle)
		);

	Vector2 tangent =
		new Vector2(
			-direction.Y,
			direction.X
		);

	float width =
		Snap(
			BladeWidth
		);

	// --------------------------------------------------------
	// Blade endpoints
	// --------------------------------------------------------

	Vector2 inner =
		direction *
		(
			innerRadius -
			2.0f
		);

	Vector2 outer =
		direction *
		(
			outerRadius -
			7.0f
		);

	// --------------------------------------------------------
	// Outline
	// --------------------------------------------------------

	Vector2 p1 =
		Snap(
			inner +
			tangent *
			width
		);

	Vector2 p2 =
		Snap(
			outer +
			tangent *
			width
		);

	Vector2 p3 =
		Snap(
			outer -
			tangent *
			width
		);

	Vector2 p4 =
		Snap(
			inner -
			tangent *
			width
		);

	Vector2[] outline =
	{
		Snap(
			p1 +
			direction *
			3.0f
		),

		Snap(
			p2 +
			direction *
			3.0f
		),

		Snap(
			p3 -
			direction *
			3.0f
		),

		Snap(
			p4 -
			direction *
			3.0f
		)
	};

	DrawColoredPolygon(
		outline,
		Outline
	);

	// --------------------------------------------------------
	// Main blade
	// --------------------------------------------------------

	Vector2[] blade =
	{
		p1,
		p2,
		p3,
		p4
	};

	DrawColoredPolygon(
		blade,
		Metal
	);

	// --------------------------------------------------------
	// Highlight edge
	// --------------------------------------------------------

	Vector2 highlightStart =
		Snap(
			inner +
			tangent *
			(
				width *
				0.55f
			)
		);

	Vector2 highlightEnd =
		Snap(
			outer +
			tangent *
			(
				width *
				0.55f
			)
		);

	DrawLine(
		highlightStart,
		highlightEnd,
		MetalLight,
		4.0f,
		false
	);

	// --------------------------------------------------------
	// Dark edge
	// --------------------------------------------------------

	Vector2 darkStart =
		Snap(
			inner -
			tangent *
			(
				width *
				0.55f
			)
		);

	Vector2 darkEnd =
		Snap(
			outer -
			tangent *
			(
				width *
				0.55f
			)
		);

	DrawLine(
		darkStart,
		darkEnd,
		DarkMetal,
		4.0f,
		false
	);
}


}
