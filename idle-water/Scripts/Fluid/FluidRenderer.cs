using System.Diagnostics;
using Godot;

public partial class FluidRenderer : Node2D
{
	private Sprite2D waterSprite;
	private Image waterImage;
	private ImageTexture waterTexture;
	private ShaderMaterial waterMaterial;

	private int width;
	private int height;
	private float cellSize;

	// ============================================================
	// Pixel Art
	// ============================================================

	// DensityField cell = 4 screen pixels.
	//
	// 1 = 4x4 screen pixels
	// 2 = 8x8 screen pixels
	// 3 = 12x12 screen pixels
	// 4 = 16x16 screen pixels

	private const int PixelScale = 1;

	private int pixelWidth;
	private int pixelHeight;

	private float PixelSize => cellSize * PixelScale;

	// ============================================================
	// Density
	// ============================================================

	private const float SurfaceThreshold = 0.28f;

	// ============================================================
	// Surface Glow
	// ============================================================

	// Number of pixels into the water that the surface glow
	// can extend.
	private const int SurfaceGlowWidth = 8;

	// ============================================================
	// Render throttle
	// ============================================================

	private const int RenderEveryNFrames = 2;

	private int renderFrameCounter;

	// ============================================================
	// Pixel buffers
	// ============================================================

	private bool[] pixelWater;

	private float[] pixelDepth;
	private float[] pixelSurface;

	// NEW:
	// Actual glow strength for every water pixel.
	private float[] pixelGlow;

	private byte[] pixelBytes;

	// ============================================================
	// Profiling
	// ============================================================

	private int profilerFrameCount;

	private double profilerTotalMs;
	private double profilerImageMs;

	// Last measured renderer stages.
	// These are read by FluidSimulator so the full-frame profiler
	// can show exactly where renderer time is going.
	public double LastTotalMs { get; private set; }
	public double LastBuildPixelsMs { get; private set; }
	public double LastSurfaceGlowMs { get; private set; }
	public double LastFillBytesMs { get; private set; }
	public double LastTextureUploadMs { get; private set; }

	// ============================================================
	// Initialization
	// ============================================================

	public void Initialize(
		int densityWidth,
		int densityHeight,
		float densityCellSize)
	{
		width = densityWidth;
		height = densityHeight;
		cellSize = densityCellSize;

		pixelWidth = Mathf.CeilToInt(
			width / (float)PixelScale
		);

		pixelHeight = Mathf.CeilToInt(
			height / (float)PixelScale
		);

		int pixelCount =
			pixelWidth *
			pixelHeight;

		pixelWater =
			new bool[pixelCount];

		pixelDepth =
			new float[pixelCount];

		pixelSurface =
			new float[pixelCount];

		pixelGlow =
			new float[pixelCount];

		pixelBytes =
			new byte[pixelCount * 4];

		// --------------------------------------------------------
		// Image
		// --------------------------------------------------------

		waterImage =
			Image.Create(
				pixelWidth,
				pixelHeight,
				false,
				Image.Format.Rgba8
			);

		waterImage.Fill(
			new Color(
				0,
				0,
				0,
				0
			)
		);

		waterTexture =
			ImageTexture.CreateFromImage(
				waterImage
			);

		// --------------------------------------------------------
		// Sprite
		// --------------------------------------------------------

		waterSprite =
			new Sprite2D();

		waterSprite.Texture =
			waterTexture;

		waterSprite.Scale =
			new Vector2(
				PixelSize,
				PixelSize
			);

		waterSprite.TextureFilter =
			CanvasItem.TextureFilterEnum.Nearest;

		waterSprite.Centered =
			false;

		// --------------------------------------------------------
		// Shader
		// --------------------------------------------------------

		CreateWaterMaterial();

		waterSprite.Material =
			waterMaterial;

		AddChild(
			waterSprite
		);
	}

	// ============================================================
	// Water shader
	// ============================================================

	private void CreateWaterMaterial()
	{
		Shader shader =
			new Shader();

		shader.Code = @"
shader_type canvas_item;
render_mode unshaded;

// ============================================================
// Water colors
// ============================================================

uniform vec3 deep_color : source_color =
vec3(0.005, 0.16, 0.48);

uniform vec3 middle_color : source_color =
vec3(0.005, 0.16, 0.48);

uniform vec3 shallow_color : source_color =
vec3(0.01, 0.38, 0.78);

uniform vec3 surface_color : source_color =
vec3(0.55, 0.78, 0.95);

// ============================================================
// Appearance
// ============================================================

uniform float water_alpha = 0.50;

// Main surface glow.
uniform float surface_glow_strength = 0.25;

// Direct brightness.
uniform float surface_brightness = 0.12;

// Shimmer.
uniform float shimmer_strength = 0.20;
uniform float shimmer_speed = 0.20;
uniform float shimmer_scale = 0.045;

// ============================================================
// Fragment
// ============================================================

void fragment()
{
	// --------------------------------------------------------
	// Read texture.
	//
	// R = depth
	// G = surface
	// B = glow
	// A = water mask
	// --------------------------------------------------------

	vec4 tex =
		texture(
			TEXTURE,
			UV
		);

	// Empty pixels stay transparent.
	if (tex.a < 0.01)
	{
		discard;
	}

	float depth =
		clamp(
			tex.r,
			0.0,
			1.0
		);

	float surface =
		clamp(
			tex.g,
			0.0,
			1.0
		);

	// NEW:
	// Actual four-pixel glow generated by C#.
	float glow =
		clamp(
			tex.b,
			0.0,
			1.0
		);

	// ========================================================
	// Depth color
	// ========================================================

	vec3 water;

	if (depth < 0.5)
	{
		float t =
			depth / 0.5;

		water =
			mix(
				shallow_color,
				middle_color,
				t
			);
	}
	else
	{
		float t =
			(depth - 0.5) / 0.5;

		water =
			mix(
				middle_color,
				deep_color,
				t
			);
	}

	// ========================================================
	// SURFACE
	// ========================================================

	float surfaceGlow =
		smoothstep(
			0.0,
			0.75,
			surface
		);

	water =
		mix(
			water,
			surface_color,
			surfaceGlow *
			surface_glow_strength
		);

	water +=
		surface_color *
		surfaceGlow *
		surface_brightness;

	// ========================================================
	// FOUR-PIXEL WIDE GLOW
	// ========================================================

	// This is the important part.
	//
	// glow is strongest at the surface and gradually fades
	// as it travels into the water.

	water =
		mix(
			water,
			surface_color,
			glow * 0.55
		);

	water +=
		surface_color *
		glow *
		0.20;

	// ========================================================
	// Shimmer
	// ========================================================

	float wave1 =
		sin(
			UV.x *
			300.0 *
			shimmer_scale +

			UV.y *
			300.0 *
			shimmer_scale *
			0.35 +

			TIME *
			shimmer_speed
		);

	float wave2 =
		sin(
			UV.x *
			300.0 *
			shimmer_scale *
			1.73 -

			UV.y *
			300.0 *
			shimmer_scale *
			0.55 -

			TIME *
			shimmer_speed *
			0.73
		);

	float wave =
		(wave1 + wave2) *
		0.5;

	wave =
		wave *
		0.5 +
		0.5;

	float shimmerMask =
		0.20 +
		surface *
		0.80;

	float shimmer =
		wave *
		shimmer_strength *
		shimmerMask;

	water +=
		vec3(
			shimmer,
			shimmer,
			shimmer
		);

	// ========================================================
	// Surface highlight
	// ========================================================

	float highlight =
		pow(
			surface,
			3.0
		);

	water +=
		surface_color *
		highlight *
		0.18;

	// ========================================================
	// Final
	// ========================================================

	water =
		clamp(
			water,
			vec3(0.0),
			vec3(1.0)
		);

	COLOR =
		vec4(
			water,
			water_alpha
		);
}
";

		waterMaterial =
			new ShaderMaterial();

		waterMaterial.Shader =
			shader;
	}

	// ============================================================
	// Update
	// ============================================================

	public void Update(
		ParticleData particles,
		DensityField densityField)
	{
		renderFrameCounter++;

		if (
			renderFrameCounter <
			RenderEveryNFrames
		)
		{
			LastTotalMs = 0.0;
			LastBuildPixelsMs = 0.0;
			LastSurfaceGlowMs = 0.0;
			LastFillBytesMs = 0.0;
			LastTextureUploadMs = 0.0;
			return;
		}

		renderFrameCounter = 0;

		LastTotalMs = 0.0;
		LastBuildPixelsMs = 0.0;
		LastSurfaceGlowMs = 0.0;
		LastFillBytesMs = 0.0;
		LastTextureUploadMs = 0.0;

		if (!densityField.HasDensity)
		{
			ClearTexture();
			return;
		}

		Stopwatch totalTimer =
			Stopwatch.StartNew();

		BuildPixelTexture(
			densityField
		);

		totalTimer.Stop();

		LastTotalMs =
			totalTimer.Elapsed.TotalMilliseconds;

		profilerTotalMs +=
			LastTotalMs;

		profilerFrameCount++;

		if (
			profilerFrameCount >=
			60
		)
		{
			GD.Print(
				"Pixel Water profiler " +
				"(avg ms over 60 render updates): " +
				"Particles=" +
				particles.Count +
				" Total=" +
				(profilerTotalMs / 60.0)
					.ToString("F2") +
				"ms Image=" +
				(profilerImageMs / 60.0)
					.ToString("F2") +
				"ms PixelCount=" +
				(pixelWidth * pixelHeight) +
				" PixelSize=" +
				PixelSize
			);

			profilerTotalMs = 0.0;
			profilerImageMs = 0.0;
			profilerFrameCount = 0;
		}
	}

	// ============================================================
	// Build pixel texture
	// ============================================================

	private void BuildPixelTexture(
		DensityField densityField)
	{
		float[] values =
			densityField.GetValues();

		// --------------------------------------------------------
		// Active density region
		// --------------------------------------------------------

		int minX =
			Mathf.Max(
				0,
				densityField.ActiveMinX
			);

		int maxX =
			Mathf.Min(
				width - 1,
				densityField.ActiveMaxX
			);

		int minY =
			Mathf.Max(
				0,
				densityField.ActiveMinY
			);

		int maxY =
			Mathf.Min(
				height - 1,
				densityField.ActiveMaxY
			);

		// --------------------------------------------------------
		// Clear previous frame
		// --------------------------------------------------------

		System.Array.Clear(
			pixelWater,
			0,
			pixelWater.Length
		);

		System.Array.Clear(
			pixelDepth,
			0,
			pixelDepth.Length
		);

		System.Array.Clear(
			pixelSurface,
			0,
			pixelSurface.Length
		);

		System.Array.Clear(
			pixelGlow,
			0,
			pixelGlow.Length
		);

		// --------------------------------------------------------
		// Convert density cells to pixels
		// --------------------------------------------------------

		Stopwatch buildPixelsTimer =
			Stopwatch.StartNew();

		int firstPixelX =
			Mathf.Clamp(
				minX / PixelScale,
				0,
				pixelWidth - 1
			);

		int lastPixelX =
			Mathf.Clamp(
				maxX / PixelScale,
				0,
				pixelWidth - 1
			);

		int firstPixelY =
			Mathf.Clamp(
				minY / PixelScale,
				0,
				pixelHeight - 1
			);

		int lastPixelY =
			Mathf.Clamp(
				maxY / PixelScale,
				0,
				pixelHeight - 1
			);

		for (
			int py = firstPixelY;
			py <= lastPixelY;
			py++)
		{
			int sourceStartY =
				py * PixelScale;

			int sourceEndY =
				Mathf.Min(
					height - 1,
					sourceStartY +
					PixelScale -
					1
				);

			int pixelRow =
				py * pixelWidth;

			for (
				int px = firstPixelX;
				px <= lastPixelX;
				px++)
			{
				int sourceStartX =
					px * PixelScale;

				int sourceEndX =
					Mathf.Min(
						width - 1,
						sourceStartX +
						PixelScale -
						1
					);

				float densitySum =
					0.0f;

				float maximumDensity =
					0.0f;

				int samples =
					0;

				for (
					int y = sourceStartY;
					y <= sourceEndY;
					y++)
				{
					int row =
						y * width;

					for (
						int x = sourceStartX;
						x <= sourceEndX;
						x++)
					{
						float density =
							values[row + x];

						if (
							density >=
							SurfaceThreshold
						)
						{
							if (
								density >
								maximumDensity
							)
							{
								maximumDensity =
									density;
							}

							densitySum +=
								density;

							samples++;
						}
					}
				}

				if (samples == 0)
				{
					continue;
				}

				int pixelIndex =
					pixelRow + px;

				pixelWater[pixelIndex] =
					true;

				float averageDensity =
					densitySum /
					samples;

				// ------------------------------------------------
				// Depth
				// ------------------------------------------------

				float depth =
					Mathf.Clamp(
						averageDensity /
						1.5f,
						0.0f,
						1.0f
					);

				// ------------------------------------------------
				// Surface
				// ------------------------------------------------

				float surface =
					Mathf.Clamp(
						1.0f -
						(
							maximumDensity -
							SurfaceThreshold
						) /
						0.45f,
						0.0f,
						1.0f
					);

				surface *= surface;

				pixelDepth[pixelIndex] =
					depth;

				pixelSurface[pixelIndex] =
					surface;
			}
		}

		buildPixelsTimer.Stop();
		LastBuildPixelsMs =
			buildPixelsTimer.Elapsed.TotalMilliseconds;

		// ========================================================
		// Build 4-pixel surface glow
		// ========================================================

		Stopwatch glowTimer =
			Stopwatch.StartNew();

		BuildSurfaceGlow();

		glowTimer.Stop();
		LastSurfaceGlowMs =
			glowTimer.Elapsed.TotalMilliseconds;

		// --------------------------------------------------------
		// Convert to RGBA8
		// --------------------------------------------------------

		Stopwatch imageTimer =
			Stopwatch.StartNew();

		Stopwatch fillBytesTimer =
			Stopwatch.StartNew();

		FillPixelBytes();

		fillBytesTimer.Stop();
		LastFillBytesMs =
			fillBytesTimer.Elapsed.TotalMilliseconds;

		// --------------------------------------------------------
		// Upload texture
		// --------------------------------------------------------

		Stopwatch uploadTimer =
			Stopwatch.StartNew();

		waterImage.SetData(
			pixelWidth,
			pixelHeight,
			false,
			Image.Format.Rgba8,
			pixelBytes
		);

		waterTexture.Update(
			waterImage
		);

		uploadTimer.Stop();
		LastTextureUploadMs =
			uploadTimer.Elapsed.TotalMilliseconds;

		imageTimer.Stop();

		profilerImageMs +=
			imageTimer.Elapsed.TotalMilliseconds;
	}

	// ============================================================
	// Build Surface Glow
	// ============================================================

	private void BuildSurfaceGlow()
	{
		// We search outward from every surface pixel.
		//
		// The glow is strongest at the surface:
		//
		// Distance 0 = 1.00
		// Distance 1 = 0.80
		// Distance 2 = 0.55
		// Distance 3 = 0.30
		// Distance 4 = 0.12
		//
		// This creates a visible 4-pixel bright band.

		for (
			int y = 0;
			y < pixelHeight;
			y++)
		{
			int row =
				y * pixelWidth;

			for (
				int x = 0;
				x < pixelWidth;
				x++)
			{
				int index =
					row + x;

				if (!pixelWater[index])
				{
					continue;
				}

				float surface =
					pixelSurface[index];

				// Only meaningful surface pixels create
				// the extended glow.
				if (surface <= 0.05f)
				{
					continue;
				}

				// ------------------------------------------------
				// Check the 4-pixel neighborhood.
				// ------------------------------------------------

				for (
					int dy = -SurfaceGlowWidth;
					dy <= SurfaceGlowWidth;
					dy++)
				{
					int targetY =
						y + dy;

					if (
						targetY < 0 ||
						targetY >= pixelHeight
					)
					{
						continue;
					}

					for (
						int dx = -SurfaceGlowWidth;
						dx <= SurfaceGlowWidth;
						dx++)
					{
						int targetX =
							x + dx;

						if (
							targetX < 0 ||
							targetX >= pixelWidth
						)
						{
							continue;
						}

						// Manhattan distance produces a
						// pixel-art style glow.
						int distance =
							Mathf.Abs(dx) +
							Mathf.Abs(dy);

						if (
							distance >
							SurfaceGlowWidth
						)
						{
							continue;
						}

						int targetIndex =
							targetY *
							pixelWidth +
							targetX;

						// Glow should only travel through
						// actual water.
						if (!pixelWater[targetIndex])
						{
							continue;
						}

						float strength;

						if (distance == 0)
						{
							strength = 1.0f;
						}
						else if (distance == 1)
						{
							strength = 0.80f;
						}
						else if (distance == 2)
						{
							strength = 0.55f;
						}
						else if (distance == 3)
						{
							strength = 0.30f;
						}
						else
						{
							strength = 0.12f;
						}

						float glow =
							surface *
							strength;

						if (
							glow >
							pixelGlow[targetIndex]
						)
						{
							pixelGlow[targetIndex] =
								glow;
						}
					}
				}
			}
		}
	}

	// ============================================================
	// RGBA8 conversion
	// ============================================================

	private void FillPixelBytes()
	{
		int count =
			pixelWater.Length;

		for (
			int i = 0;
			i < count;
			i++)
		{
			int byteIndex =
				i * 4;

			// ----------------------------------------------------
			// Empty pixel
			// ----------------------------------------------------

			if (!pixelWater[i])
			{
				pixelBytes[byteIndex] = 0;
				pixelBytes[byteIndex + 1] = 0;

				// Blue = glow.
				pixelBytes[byteIndex + 2] = 0;

				pixelBytes[byteIndex + 3] = 0;

				continue;
			}

			// ----------------------------------------------------
			// Water pixel
			// ----------------------------------------------------

			float depth =
				Mathf.Clamp(
					pixelDepth[i],
					0.0f,
					1.0f
				);

			float surface =
				Mathf.Clamp(
					pixelSurface[i],
					0.0f,
					1.0f
				);

			float glow =
				Mathf.Clamp(
					pixelGlow[i],
					0.0f,
					1.0f
				);

			// R = depth.
			pixelBytes[byteIndex] =
				(byte)(
					depth *
					255.0f
				);

			// G = surface.
			pixelBytes[byteIndex + 1] =
				(byte)(
					surface *
					255.0f
				);

			// B = four-pixel glow.
			pixelBytes[byteIndex + 2] =
				(byte)(
					glow *
					255.0f
				);

			// A = water.
			pixelBytes[byteIndex + 3] =
				255;
		}
	}

	// ============================================================
	// Clear
	// ============================================================

	private void ClearTexture()
	{
		System.Array.Clear(
			pixelBytes,
			0,
			pixelBytes.Length
		);

		waterImage.SetData(
			pixelWidth,
			pixelHeight,
			false,
			Image.Format.Rgba8,
			pixelBytes
		);

		waterTexture.Update(
			waterImage
		);
	}
}
