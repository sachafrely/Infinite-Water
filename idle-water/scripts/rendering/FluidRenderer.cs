
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
	
	private float worldMinX;
	private float worldMinY;

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
	private const float SurfaceMaskStrength = 1.0f;

	// ============================================================
	// Render throttle
	// ============================================================

	private const int RenderEveryNFrames = 2;
	private const int ProfilerSampleWindow = 600;

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
	// Active pixel region
	// ============================================================

	private int activePixelMinX;
	private int activePixelMaxX;
	private int activePixelMinY;
	private int activePixelMaxY;

	private int previousPixelMinX;
	private int previousPixelMaxX;
	private int previousPixelMinY;
	private int previousPixelMaxY;

	private bool hasPreviousPixelRegion;

	// ============================================================
	// Profiling
	// ============================================================

	private int profilerFrameCount;
	private double profilerTotalMs;
	private double profilerImageMs;
	private double profilerBuildPixelsMs;
	private double profilerSurfaceGlowMs;
	private double profilerFillBytesMs;
	private double profilerTextureUploadMs;

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
		float densityCellSize,
	float worldMinX,
	float worldMinY)
	{
		width = densityWidth;
		height = densityHeight;
		cellSize = densityCellSize;

	this.worldMinX = worldMinX;
	this.worldMinY = worldMinY;

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

		hasPreviousPixelRegion = false;

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

		waterSprite.Position =
	new Vector2(
		worldMinX,
		worldMinY
	);
	
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
vec3(0.025, 0.25, 0.68);

uniform vec3 middle_color : source_color =
vec3(0.08, 0.45, 0.78);

uniform vec3 shallow_color : source_color =
vec3(0.2, 0.58, 0.95);

uniform vec3 surface_color : source_color =
vec3(0.75, 0.92, 1.00);

// ============================================================
// Appearance
// ============================================================

uniform float water_alpha = 0.70;

uniform float surface_glow_strength = 0.40;

uniform float surface_brightness = 0.14;

// ============================================================
// LOCAL SURFACE HIGHLIGHT
// ============================================================

uniform float local_highlight_strength = 0.45;

uniform float local_highlight_brightness = 0.14;

// ============================================================
// Shimmer
// ============================================================

uniform float shimmer_strength = 0.67;

uniform float shimmer_speed = 0.25;

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
		if (ShouldSkipRenderFrame())
		{
			return;
		}

		ResetLastFrameMetrics();

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

		AccumulateProfilerMetrics();
		TryFlushProfiler(particles);
	}

	private bool ShouldSkipRenderFrame()
	{
		renderFrameCounter++;

		if (
			renderFrameCounter <
			RenderEveryNFrames
		)
		{
			ResetLastFrameMetrics();
			return true;
		}

		renderFrameCounter = 0;
		return false;
	}

	private void ResetLastFrameMetrics()
	{
		LastTotalMs = 0.0;
		LastBuildPixelsMs = 0.0;
		LastSurfaceGlowMs = 0.0;
		LastFillBytesMs = 0.0;
		LastTextureUploadMs = 0.0;
	}

	private void AccumulateProfilerMetrics()
	{
		profilerTotalMs +=
			LastTotalMs;

		profilerBuildPixelsMs +=
			LastBuildPixelsMs;

		profilerSurfaceGlowMs +=
			LastSurfaceGlowMs;

		profilerFillBytesMs +=
			LastFillBytesMs;

		profilerTextureUploadMs +=
			LastTextureUploadMs;

		profilerFrameCount++;
	}

	private void TryFlushProfiler(
		ParticleData particles)
	{
		if (
			profilerFrameCount <
			ProfilerSampleWindow
		)
		{
			return;
		}

		const double profilerSamples =
			ProfilerSampleWindow;

		GD.Print(
			"Pixel Water profiler " +
			$"(avg ms over {ProfilerSampleWindow} render updates): " +
			"Particles=" +
			particles.Count +
			" BuildPixels=" +
			(profilerBuildPixelsMs / profilerSamples)
				.ToString("F3") +
			"ms SurfaceGlow=" +
			(profilerSurfaceGlowMs / profilerSamples)
				.ToString("F3") +
			"ms FillBytes=" +
			(profilerFillBytesMs / profilerSamples)
				.ToString("F3") +
			"ms TextureUpload=" +
			(profilerTextureUploadMs / profilerSamples)
				.ToString("F3") +
			"ms Total=" +
			(profilerTotalMs / profilerSamples)
				.ToString("F3") +
			"ms PixelCount=" +
			(pixelWidth * pixelHeight) +
			" PixelSize=" +
			PixelSize
		);

		ResetProfilerMetrics();
	}

	private void ResetProfilerMetrics()
	{
		profilerTotalMs = 0.0;
		profilerImageMs = 0.0;
		profilerBuildPixelsMs = 0.0;
		profilerSurfaceGlowMs = 0.0;
		profilerFillBytesMs = 0.0;
		profilerTextureUploadMs = 0.0;
		profilerFrameCount = 0;
	}

	// ============================================================
	// Build pixel texture
	//
	// PHASE 5 OPTIMIZATION
	//
	// PixelScale is permanently 1.
	//
	// Therefore every output pixel maps to exactly one density
	// cell. The previous implementation still performed:
	//
	// - sourceStartX/sourceEndX calculations
	// - sourceStartY/sourceEndY calculations
	// - nested X/Y sampling loops
	// - Mathf.Min() calls per pixel
	// - sample counting
	//
	// None of that is necessary with PixelScale = 1.
	//
	// This version performs exactly one density read per pixel.
	// Visual calculations are unchanged.
	// ============================================================

	private void BuildPixelTexture(
		DensityField densityField)
	{
		float[] values =
			densityField.GetValues();

		UpdateActivePixelRegion(
			densityField
		);

		// --------------------------------------------------------
		// Build timer starts before previous-region clearing,
		// matching the previous profiler behavior.
		// --------------------------------------------------------

		Stopwatch buildPixelsTimer =
			Stopwatch.StartNew();

		if (hasPreviousPixelRegion)
		{
			ClearPreviousPixelRegion();
		}

		BuildPixelsFromDensity(
			values
		);
		CacheCurrentAsPreviousPixelRegion();

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

		// ========================================================
		// Convert to RGBA8
		// ========================================================

		Stopwatch imageTimer =
			Stopwatch.StartNew();

		Stopwatch fillBytesTimer =
			Stopwatch.StartNew();

		FillPixelBytes();

		fillBytesTimer.Stop();

		LastFillBytesMs =
			fillBytesTimer.Elapsed.TotalMilliseconds;

		// ========================================================
		// Upload texture
		// ========================================================

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

	private void UpdateActivePixelRegion(
		DensityField densityField)
	{
		int minX =
			densityField.ActiveMinX;

		if (minX < 0)
			minX = 0;

		int maxX =
			densityField.ActiveMaxX;

		if (maxX >= width)
			maxX = width - 1;

		int minY =
			densityField.ActiveMinY;

		if (minY < 0)
			minY = 0;

		int maxY =
			densityField.ActiveMaxY;

		if (maxY >= height)
			maxY = height - 1;

		activePixelMinX =
			minX;
		activePixelMaxX =
			maxX;
		activePixelMinY =
			minY;
		activePixelMaxY =
			maxY;
	}

	private void BuildPixelsFromDensity(
		float[] values)
	{
		int firstPixelX =
			activePixelMinX;

		int lastPixelX =
			activePixelMaxX;

		int firstPixelY =
			activePixelMinY;

		int lastPixelY =
			activePixelMaxY;

		int localWidth =
			lastPixelX -
			firstPixelX +
			1;

		for (
			int py = firstPixelY;
			py <= lastPixelY;
			py++
		)
		{
			int row =
				py * width;

			int pixelIndex =
				py * pixelWidth +
				firstPixelX;

			int sourceIndex =
				row +
				firstPixelX;

			int endIndex =
				pixelIndex +
				localWidth;

			for (
				;
				pixelIndex < endIndex;
				pixelIndex++,
				sourceIndex++
			)
			{
				float density =
					values[sourceIndex];

				if (
					density <
					SurfaceThreshold
				)
				{
					continue;
				}

				pixelWater[pixelIndex] =
					true;

				float depth =
					density /
					1.5f;

				if (depth > 1.0f)
					depth = 1.0f;

				if (depth < 0.0f)
					depth = 0.0f;

				float surface =
					1.0f -
					(
						density -
						SurfaceThreshold
					) /
					0.45f;

				if (surface > 1.0f)
					surface = 1.0f;

				if (surface < 0.0f)
					surface = 0.0f;

				surface *=
					surface;

				pixelDepth[pixelIndex] =
					depth;

				pixelSurface[pixelIndex] =
					surface;
			}
		}
	}

	private void CacheCurrentAsPreviousPixelRegion()
	{
		previousPixelMinX =
			activePixelMinX;

		previousPixelMaxX =
			activePixelMaxX;

		previousPixelMinY =
			activePixelMinY;

		previousPixelMaxY =
			activePixelMaxY;

		hasPreviousPixelRegion =
			true;
	}

	// ============================================================
	// Clear Previous Active Region
	// ============================================================

	private void ClearPreviousPixelRegion()
	{
		int minX =
			previousPixelMinX;

		int maxX =
			previousPixelMaxX;

		int minY =
			previousPixelMinY;

		int maxY =
			previousPixelMaxY;

		if (
			minX < 0 ||
			maxX < minX ||
			minY < 0 ||
			maxY < minY
		)
		{
			return;
		}

		int rowWidth =
			maxX -
			minX +
			1;

		for (
			int y = minY;
			y <= maxY;
			y++
		)
		{
			int index =
				y * pixelWidth +
				minX;

			Array.Clear(
				pixelWater,
				index,
				rowWidth
			);

			Array.Clear(
				pixelDepth,
				index,
				rowWidth
			);

			Array.Clear(
				pixelSurface,
				index,
				rowWidth
			);

			Array.Clear(
				pixelGlow,
				index,
				rowWidth
			);

			Array.Clear(
				pixelBytes,
				index * 4,
				rowWidth * 4
			);
		}
	}

	// ============================================================
	// Local Surface Mask
	// ============================================================

	private void BuildSurfaceGlow()
	{
		int w =
			pixelWidth;

		int minX =
			activePixelMinX;

		int maxX =
			activePixelMaxX;

		int minY =
			activePixelMinY;

		int maxY =
			activePixelMaxY;

		for (
			int y = minY;
			y <= maxY;
			y++
		)
		{
			int row =
				y * w;

			int index =
				row + minX;

			int aboveIndex =
				index - w;

			bool firstRow =
				y == 0;

			for (
				int x = minX;
				x <= maxX;
				x++,
				index++,
				aboveIndex++
			)
			{
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

				if (firstRow)
				{
					exposedAbove =
						true;
				}
				else
				{
					exposedAbove =
						!pixelWater[aboveIndex];
				}

				if (!exposedAbove)
				{
					continue;
				}

				float glow =
					surface *
					SurfaceMaskStrength;

				if (glow < 0.0f)
					glow = 0.0f;

				if (glow > 1.0f)
					glow = 1.0f;

				pixelGlow[index] =
					glow;
			}
		}
	}

	// ============================================================
	// RGBA8 conversion
	// ============================================================

	private void FillPixelBytes()
	{
		int minX =
			activePixelMinX;

		int maxX =
			activePixelMaxX;

		int minY =
			activePixelMinY;

		int maxY =
			activePixelMaxY;

		int w =
			pixelWidth;

		// --------------------------------------------------------
		// Empty pixels are NOT rewritten here.
		//
		// Their bytes are already zero because the previous active
		// region is cleared before building the new region.
		// --------------------------------------------------------

		for (
			int y = minY;
			y <= maxY;
			y++
		)
		{
			int index =
				y * w +
				minX;

			int end =
				y * w +
				maxX;

			for (
				;
				index <= end;
				index++
			)
			{
				if (!pixelWater[index])
				{
					continue;
				}

				int byteIndex =
					index * 4;

				// R = depth.
				pixelBytes[byteIndex] =
					(byte)(
						pixelDepth[index] *
						255.0f
					);

				// G = surface.
				pixelBytes[byteIndex + 1] =
					(byte)(
						pixelSurface[index] *
						255.0f
					);

				// B = exposed surface mask.
				pixelBytes[byteIndex + 2] =
					(byte)(
						pixelGlow[index] *
						255.0f
					);

				// A = water.
				pixelBytes[byteIndex + 3] =
					255;
			}
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

		hasPreviousPixelRegion =
			false;

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
