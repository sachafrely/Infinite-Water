
using Godot;

/// <summary>
/// Pixel-art water wheel visual.
///
/// The wheel is rendered into a low-resolution SubViewport and then
/// enlarged with nearest-neighbour filtering.
///
/// IMPORTANT:
/// The WaterWheelVisual itself never rotates.
/// Only the wheel painter inside the low-resolution viewport rotates.
/// This keeps the pixel grid screen-aligned while the wheel rotates.
///
/// Physics is completely separate and only uses WheelAngle.
/// </summary>
public partial class WaterWheelVisual : Node2D
{
	// ============================================================
	// Configuration
	// ============================================================

	public float OuterRadius = 145.0f;
	public float InnerRadius = 75.0f;
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

	// Must match the water pixel size.
	private const int PixelSize = 4;

	// Extra margin around the wheel so the outline never gets clipped.
	private const int ViewportMargin = 8;

	// ============================================================
	// Visual smoothing
	// ============================================================

	private const float VisualSmoothing = 20.0f;

	private float targetAngle = 0.0f;
	private float visualAngle = 0.0f;

	// ============================================================
	// Rendering nodes
	// ============================================================

	private SubViewport wheelViewport;
	private WheelPainter wheelPainter;
	private Sprite2D wheelSprite;

	private int viewportSize;

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
			// Render the wheel in front of the water.
			ZIndex = 10;

		SetupPixelViewport();
		UpdatePainter();
	}

	// ============================================================
	// Pixel viewport setup
	// ============================================================

	private void SetupPixelViewport()
	{
		// The wheel diameter is approximately:
		//
		// 2 * (OuterRadius + outline)
		//
		// Convert the world-space size to the low-resolution
		// pixel-art render size.

		float worldDiameter =
			OuterRadius * 2.0f +
			12.0f;

		viewportSize =
			Mathf.CeilToInt(
				worldDiameter /
				PixelSize
			) +
			ViewportMargin * 2;

		viewportSize =
			Mathf.Max(
				viewportSize,
				16
			);

		// --------------------------------------------------------
		// Low-resolution viewport
		// --------------------------------------------------------

		wheelViewport =
			new SubViewport();

		wheelViewport.Name =
			"WheelPixelViewport";

		wheelViewport.Size =
			new Vector2I(
				viewportSize,
				viewportSize
			);

		wheelViewport.TransparentBg =
			true;

		wheelViewport.RenderTargetUpdateMode =
			SubViewport.UpdateMode.Always;

		wheelViewport.CanvasItemDefaultTextureFilter =
	Viewport.DefaultCanvasItemTextureFilter.Nearest;

		AddChild(
			wheelViewport
		);

		// --------------------------------------------------------
		// Painter
		//
		// This node draws the wheel at 1/4 resolution.
		// It is the ONLY thing that rotates.
		// --------------------------------------------------------

		wheelPainter =
			new WheelPainter();

		wheelPainter.Name =
			"WheelPainter";

		wheelPainter.Wheel =
			this;

		wheelViewport.AddChild(
			wheelPainter
		);

		wheelPainter.Position =
			new Vector2(
				viewportSize * 0.5f,
				viewportSize * 0.5f
			);

		// --------------------------------------------------------
		// Display sprite
		//
		// The low-resolution texture is enlarged exactly 4x.
		// Nearest filtering preserves hard pixel edges.
		// --------------------------------------------------------

		wheelSprite =
			new Sprite2D();

		wheelSprite.Name =
			"WheelPixelSprite";

		AddChild(
			wheelSprite
		);

		wheelSprite.Texture =
			wheelViewport.GetTexture();

		wheelSprite.TextureFilter =
			CanvasItem.TextureFilterEnum.Nearest;

		wheelSprite.Centered =
			true;

		wheelSprite.Scale =
			new Vector2(
				PixelSize,
				PixelSize
			);

		// The SubViewport texture is centered around its own
		// center, so the Sprite2D is already centered on this node.
		wheelSprite.Position =
			Vector2.Zero;

		// The viewport itself is a child only for rendering.
		// Keep it completely out of the visible scene.

	}

	// ============================================================
	// Process
	// ============================================================

	public override void _Process(
		double delta)
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

		UpdatePainter();
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
	// Update low-resolution painter
	// ============================================================

	private void UpdatePainter()
	{
		if (wheelPainter == null)
		{
			return;
		}

		wheelPainter.Angle =
			visualAngle;

		wheelPainter.QueueRedraw();
	}

	// ============================================================
	// Wheel painter
	// ============================================================

	private sealed partial class WheelPainter : Node2D
	{
		public WaterWheelVisual Wheel;

		public float Angle;

		private const float PixelSize = 4.0f;

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

		private Vector2 Snap(
			Vector2 position)
		{
			return new Vector2(
				Mathf.Round(
					position.X
				) * 1.0f,

				Mathf.Round(
					position.Y
				) * 1.0f
			);
		}

		private float Snap(
			float value)
		{
			return Mathf.Round(
				value
			);
		}

		public override void _Draw()
		{
			if (Wheel == null)
			{
				return;
			}

			// ----------------------------------------------------
			// IMPORTANT:
			//
			// Coordinates are expressed in LOW-RESOLUTION pixels.
			//
			// A 4x4 block in the final game corresponds to exactly
			// one pixel here.
			// ----------------------------------------------------

			float outer =
				Wheel.OuterRadius /
				PixelSize;

			float inner =
				Wheel.InnerRadius /
				PixelSize;

			float bladeWidth =
				Wheel.BladeWidth /
				PixelSize;

			// ----------------------------------------------------
			// Outer wheel silhouette
			// ----------------------------------------------------

			DrawCircle(
				Vector2.Zero,
				outer + 1.5f,
				Outline
			);

			DrawCircle(
				Vector2.Zero,
				outer,
				DarkMetal
			);

			// ----------------------------------------------------
			// Outer rim
			// ----------------------------------------------------

			DrawArc(
				Vector2.Zero,
				outer - 0.5f,
				0.0f,
				Mathf.Tau,
				32,
				Metal,
				2.0f,
				false
			);

			DrawArc(
				Vector2.Zero,
				outer - 2.0f,
				0.0f,
				Mathf.Tau,
				32,
				Outline,
				1.25f,
				false
			);

			// ----------------------------------------------------
			// Blades
			// ----------------------------------------------------

			for (
				int i = 0;
				i < Wheel.BladeCount;
				i++)
			{
				float angle =
					Angle +
					Mathf.Tau *
					i /
					Wheel.BladeCount;

				DrawPixelBlade(
					angle,
					inner,
					outer,
					bladeWidth
				);
			}

			// ----------------------------------------------------
			// Inner wooden ring
			// ----------------------------------------------------

			DrawCircle(
				Vector2.Zero,
				inner + 1.25f,
				Outline
			);

			DrawCircle(
				Vector2.Zero,
				inner,
				Wood
			);

			DrawArc(
				Vector2.Zero,
				inner - 1.0f,
				0.0f,
				Mathf.Tau,
				24,
				WoodLight,
				1.25f,
				false
			);

			// ----------------------------------------------------
			// Hub
			// ----------------------------------------------------

			DrawCircle(
				Vector2.Zero,
				23.0f / PixelSize,
				Outline
			);

			DrawCircle(
				Vector2.Zero,
				19.0f / PixelSize,
				HubDark
			);

			DrawRect(
				new Rect2(
					-9.0f / PixelSize,
					-9.0f / PixelSize,
					18.0f / PixelSize,
					6.0f / PixelSize
				),
				Metal
			);

			// ----------------------------------------------------
			// Center hole
			// ----------------------------------------------------

			DrawCircle(
				Vector2.Zero,
				11.0f / PixelSize,
				Hole
			);

			DrawCircle(
				Vector2.Zero,
				11.0f / PixelSize,
				Outline,
				false,
				1.0f
			);

			DrawRect(
				new Rect2(
					-5.0f / PixelSize,
					-5.0f / PixelSize,
					10.0f / PixelSize,
					5.0f / PixelSize
				),
				MetalLight
			);
		}

		private void DrawPixelBlade(
			float angle,
			float innerRadius,
			float outerRadius,
			float width)
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

			Vector2 inner =
				direction *
				(
					innerRadius -
					0.5f
				);

			Vector2 outer =
				direction *
				(
					outerRadius -
					1.75f
				);

			Vector2 p1 =
				inner +
				tangent *
				width;

			Vector2 p2 =
				outer +
				tangent *
				width;

			Vector2 p3 =
				outer -
				tangent *
				width;

			Vector2 p4 =
				inner -
				tangent *
				width;

			Vector2[] outline =
			{
				p1 +
				direction *
				0.75f,

				p2 +
				direction *
				0.75f,

				p3 -
				direction *
				0.75f,

				p4 -
				direction *
				0.75f
			};

			DrawColoredPolygon(
				outline,
				Outline
			);

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

			Vector2 highlightStart =
				inner +
				tangent *
				(
					width *
					0.55f
				);

			Vector2 highlightEnd =
				outer +
				tangent *
				(
					width *
					0.55f
				);

			DrawLine(
				highlightStart,
				highlightEnd,
				MetalLight,
				1.0f,
				false
			);

			Vector2 darkStart =
				inner -
				tangent *
				(
					width *
					0.55f
				);

			Vector2 darkEnd =
				outer -
				tangent *
				(
					width *
					0.55f
				);

			DrawLine(
				darkStart,
				darkEnd,
				DarkMetal,
				1.0f,
				false
			);
		}
	}
}
