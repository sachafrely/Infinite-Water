
using System;
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

	private const int PixelScale = 1;

	private int pixelWidth;
	private int pixelHeight;

	private float PixelSize =>
		cellSize * PixelScale;

	// ============================================================
	// Density
	// ============================================================

	private const float SurfaceThreshold = 0.28f;

	// ============================================================
	// Surface Highlight
	// ============================================================

	private const float SurfaceMaskThreshold = 0.05f;

	// Increased from 0.75.
	//
	// This makes the exposed surface mask significantly stronger
	// without propagating it into the water.
	private const float SurfaceMaskStrength = 1.0f;

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
	private float[] pixelGlow;

	private byte[] pixelBytes;

	// ============================================================
	// Profiling
	// ============================================================

	private int profilerFrameCount;

	private double profilerTotalMs;
	private double profilerImageMs;

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
		width =
			densityWidth;

		height =
			densityHeight;

		cellSize =
			densityCellSize;

		pixelWidth =
			Mathf.CeilToInt(
				width /
				(float)PixelScale
			);

		pixelHeight =
			Mathf.CeilToInt(
				height /
				(float)PixelScale
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
vec3(0.11, 0.48, 0.88);

uniform vec3 surface_color : source_color =
vec3(0.55, 0.78, 0.95);

// ============================================================
// Appearance
// ============================================================

uniform float water_alpha = 0.70;

// Main surface contribution.
uniform float surface_glow_strength = 0.40;

// Direct surface brightness.
uniform float surface_brightness = 0.14;

// ============================================================
// LOCAL SURFACE HIGHLIGHT
// ============================================================
//
// These two values were increased so the exposed surface
// is visibly thicker/brighter than the previous version.
//
// IMPORTANT:
// This still only uses the exposed-surface mask from the
// B channel. It does not create deep-water glow.
//
// ============================================================

uniform float local_highlight_strength = 0.45;

uniform float local_highlight_brightness = 0.14;

// ============================================================
// Shimmer
// ============================================================

uniform float shimmer_strength = 0.25;
uniform float shimmer_speed = 0.20;
uniform float shimmer_scale = 0.045;

// ============================================================
// Fragment
// ============================================================

void fragment()
{
vec4 tex =
	texture(
		TEXTURE,
		UV
	);

if (tex.a < 0.01)
{
	discard;
}

// ========================================================
// Read channels
// ========================================================

// R = depth.
float depth =
	clamp(
		tex.r,
		0.0,
		1.0
	);

// G = density-derived surface.
float surface =
	clamp(
		tex.g,
		0.0,
		1.0
	);

// B = exposed surface mask.
//
// This is ONLY present on actual exposed top pixels.
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
// THICKER LOCAL SURFACE HIGHLIGHT
// ========================================================

float localHighlight =
	smoothstep(
		0.0,
		1.0,
		glow
	);

water =
	mix(
		water,
		surface_color,
		localHighlight *
		local_highlight_strength
	);

water +=
	surface_color *
	localHighlight *
	local_highlight_brightness;

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

		Array.Clear(
			pixelWater,
			0,
			pixelWater.Length
		);

		Array.Clear(
			pixelDepth,
			0,
			pixelDepth.Length
		);

		Array.Clear(
			pixelSurface,
			0,
			pixelSurface.Length
		);

		Array.Clear(
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
		// Local surface mask
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
	// Local Surface Mask
	// ============================================================

	private void BuildSurfaceGlow()
	{
		int w =
			pixelWidth;

		int h =
			pixelHeight;

		// --------------------------------------------------------
		// NO propagation.
		//
		// NO neighborhood search.
		//
		// NO bottom glow.
		//
		// Only exposed top-surface pixels receive the mask.
		// --------------------------------------------------------

		for (
			int y = 0;
			y < h;
			y++)
		{
			int row =
				y * w;

			for (
				int x = 0;
				x < w;
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

				if (
					surface <=
					SurfaceMaskThreshold
				)
				{
					continue;
				}

				bool exposedAbove;

				if (y == 0)
				{
					exposedAbove = true;
				}
				else
				{
					exposedAbove =
						!pixelWater[index - w];
				}

				if (!exposedAbove)
				{
					continue;
				}

				float glow =
					surface *
					SurfaceMaskStrength;

				pixelGlow[index] =
					Mathf.Clamp(
						glow,
						0.0f,
						1.0f
					);
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
			i++
		)
		{
			int byteIndex =
				i * 4;

			if (!pixelWater[i])
			{
				pixelBytes[byteIndex] = 0;
				pixelBytes[byteIndex + 1] = 0;
				pixelBytes[byteIndex + 2] = 0;
				pixelBytes[byteIndex + 3] = 0;

				continue;
			}

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

			// B = exposed surface mask.
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
		Array.Clear(
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
