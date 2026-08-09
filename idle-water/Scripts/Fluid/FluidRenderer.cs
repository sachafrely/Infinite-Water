
using System.Collections.Generic;
using System.Diagnostics;
using Godot;

public partial class FluidRenderer : Node2D
{
	private MeshInstance2D waterMesh;
	private ArrayMesh mesh;

	private int width;
	private int height;
	private float cellSize;

	private const float SurfaceThreshold = 0.28f;

	// ============================================================
	// RENDER UPDATE THROTTLE
	//
	// Simulation can continue every physics frame.
	// The expensive mesh is rebuilt only every Nth update.
	// ============================================================

	private const int RenderEveryNFrames = 2;

	private int renderFrameCounter = 0;

	// ============================================================
	// Render connectivity
	// ============================================================

	private bool[] renderWater;
	private bool[] renderWaterScratch;

	// ============================================================
	// Visual data
	//
	// R = depth
	// G = surface influence
	// B = edge lighting
	// A = 1
	// ============================================================

	private float[] visualDepth;
	private float[] visualSurface;
	private int[] visualGeneration;

	private int visualGenerationId = 0;

	// ============================================================
	// Vertex cache generations
	// ============================================================

	private int[] cornerCacheGeneration;
	private int[] horizontalCacheGeneration;
	private int[] verticalCacheGeneration;

	private int[] cornerIndices;
	private int[] horizontalEdgeIndices;
	private int[] verticalEdgeIndices;

	private int cacheGeneration = 0;

	// ============================================================
	// Mesh buffers
	// ============================================================

	private readonly List<Vector2> vertices =
		new List<Vector2>(16384);

	private readonly List<int> indices =
		new List<int>(24576);

	private readonly List<Color> vertexColors =
		new List<Color>(16384);

	// ============================================================
	// Profiler
	// ============================================================

	private int profilerFrameCount = 0;

	private double profilerMeshTime = 0.0;
	private double profilerTotalTime = 0.0;

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

		waterMesh =
			new MeshInstance2D();

		mesh =
			new ArrayMesh();

		waterMesh.Mesh =
			mesh;

		int gridSize =
			width * height;

		int cornerGridSize =
			(width + 1) *
			(height + 1);

		int horizontalSize =
			Mathf.Max(
				1,
				(width - 1) * height
			);

		int verticalSize =
			Mathf.Max(
				1,
				width * (height - 1)
			);

		// --------------------------------------------------------
		// Vertex caches
		// --------------------------------------------------------

		cornerIndices =
			new int[cornerGridSize];

		horizontalEdgeIndices =
			new int[horizontalSize];

		verticalEdgeIndices =
			new int[verticalSize];

		cornerCacheGeneration =
			new int[cornerGridSize];

		horizontalCacheGeneration =
			new int[horizontalSize];

		verticalCacheGeneration =
			new int[verticalSize];

		// --------------------------------------------------------
		// Render masks
		// --------------------------------------------------------

		renderWater =
			new bool[gridSize];

		renderWaterScratch =
			new bool[gridSize];

		// --------------------------------------------------------
		// Visual data
		// --------------------------------------------------------

		visualDepth =
			new float[gridSize];

		visualSurface =
			new float[gridSize];

		visualGeneration =
			new int[gridSize];

		CreateWaterMaterial();

		AddChild(waterMesh);
	}

	// ============================================================
	// Water material
	// ============================================================

	private void CreateWaterMaterial()
	{
		Shader shader =
			new Shader();

		shader.Code = @"
shader_type canvas_item;
render_mode unshaded;

uniform vec3 deep_color : source_color =
vec3(0.005, 0.16, 0.48);

uniform vec3 middle_color : source_color =
vec3(0.005, 0.16, 0.48);

uniform vec3 shallow_color : source_color =
vec3(0.01, 0.38, 0.78);

uniform vec3 surface_color : source_color =
vec3(0.55, 0.78, 0.95);

uniform float water_alpha = 0.5;

uniform float surface_glow_strength = 0.5;

uniform float edge_light_strength = 0.33;

uniform float shimmer_strength = 0.1;

uniform float shimmer_speed = 0.2;

uniform float shimmer_scale = 0.045;

varying float water_depth;
varying float surface_factor;
varying float edge_factor;
varying vec2 water_position;

void vertex()
{
	water_depth =
		clamp(
			COLOR.r,
			0.0,
			1.0
		);

	surface_factor =
		clamp(
			COLOR.g,
			0.0,
			1.0
		);

	edge_factor =
		clamp(
			COLOR.b,
			0.0,
			1.0
		);

	water_position =
		VERTEX;
}

void fragment()
{
	float depth =
		clamp(
			water_depth,
			0.0,
			1.0
		);

	float surface =
		clamp(
			surface_factor,
			0.0,
			1.0
		);

	float edge =
		clamp(
			edge_factor,
			0.0,
			1.0
		);

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

	float glow =
		surface * surface;

	float glowAmount =
		glow *
		surface_glow_strength *
		0.45;

	water =
		mix(
			water,
			surface_color,
			glowAmount
		);

	water +=
		surface_color *
		glow *
		0.035;

	float wave1 =
		sin(
			water_position.x *
			shimmer_scale +

			water_position.y *
			shimmer_scale *
			0.35 +

			TIME *
			shimmer_speed
		);

	float wave2 =
		sin(
			water_position.x *
			shimmer_scale *
			1.73 -

			water_position.y *
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
		wave * 0.5 +
		0.5;

	float shimmerMask =
		0.25 +
		surface * 0.75;

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

	float edgeGlow =
		edge *
		edge *
		edge_light_strength;

	water =
		mix(
			water,
			surface_color,
			edgeGlow
		);

	float highlight =
		pow(
			surface,
			5.0
		);

	water +=
		surface_color *
		highlight *
		0.10;

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

		ShaderMaterial material =
			new ShaderMaterial();

		material.Shader =
			shader;

		waterMesh.Material =
			material;
	}

	// ============================================================
	// Update
	// ============================================================

	public void Update(
		ParticleData particles,
		DensityField densityField)
	{
		renderFrameCounter++;

		// --------------------------------------------------------
		// Do not rebuild the expensive mesh every physics frame.
		// --------------------------------------------------------

		if (
			renderFrameCounter <
			RenderEveryNFrames
		)
		{
			return;
		}

		renderFrameCounter = 0;

		Stopwatch totalTimer =
			Stopwatch.StartNew();

		BuildMarchingSquaresMesh(
			densityField
		);

		totalTimer.Stop();

		profilerTotalTime +=
			totalTimer.Elapsed.TotalMilliseconds;

		profilerFrameCount++;

		if (profilerFrameCount >= 60)
		{
			GD.Print(
				"Marching Squares profiler " +
				"(avg ms over 60 render updates): " +
				"Particles=" +
				particles.Count +
				" Total=" +
				(profilerTotalTime / 60.0)
					.ToString("F2") +
				"ms Mesh=" +
				(profilerMeshTime / 60.0)
					.ToString("F2") +
				"ms Vertices=" +
				vertices.Count +
				" Indices=" +
				indices.Count
			);

			profilerMeshTime = 0.0;
			profilerFrameCount = 0;
			profilerTotalTime = 0.0;
		}
	}

	// ============================================================
	// Build Marching Squares mesh
	// ============================================================

	private void BuildMarchingSquaresMesh(
		DensityField densityField)
	{
		float[] values =
			densityField.GetValues();

		vertices.Clear();
		indices.Clear();
		vertexColors.Clear();

		cacheGeneration++;

		if (cacheGeneration == int.MaxValue)
		{
			System.Array.Clear(
				cornerCacheGeneration,
				0,
				cornerCacheGeneration.Length
			);

			System.Array.Clear(
				horizontalCacheGeneration,
				0,
				horizontalCacheGeneration.Length
			);

			System.Array.Clear(
				verticalCacheGeneration,
				0,
				verticalCacheGeneration.Length
			);

			cacheGeneration = 1;
		}

		if (!densityField.HasDensity)
		{
			mesh.ClearSurfaces();
			return;
		}

		BuildRenderWaterMask(values);

		CalculateVisualData(
			densityField
		);

		int minX =
			Mathf.Max(
				0,
				densityField.ActiveMinX - 1
			);

		int minY =
			Mathf.Max(
				0,
				densityField.ActiveMinY - 1
			);

		int maxX =
			Mathf.Min(
				width - 1,
				densityField.ActiveMaxX + 1
			);

		int maxY =
			Mathf.Min(
				height - 1,
				densityField.ActiveMaxY + 1
			);

		for (
			int y = minY;
			y < maxY;
			y++)
		{
			int row =
				y * width;

			int nextRow =
				row + width;

			float y0 =
				y * cellSize;

			float y1 =
				y0 + cellSize;

			for (
				int x = minX;
				x < maxX;
				x++)
			{
				int indexA =
					row + x;

				int indexB =
					indexA + 1;

				int indexD =
					nextRow + x;

				int indexC =
					indexD + 1;

				int caseIndex = 0;

				if (renderWater[indexA])
					caseIndex |= 1;

				if (renderWater[indexB])
					caseIndex |= 2;

				if (renderWater[indexC])
					caseIndex |= 4;

				if (renderWater[indexD])
					caseIndex |= 8;

				if (caseIndex == 0)
					continue;

				float a =
					values[indexA];

				float b =
					values[indexB];

				float c =
					values[indexC];

				float d =
					values[indexD];

				float x0 =
					x * cellSize;

				float x1 =
					x0 + cellSize;

				int cornerA =
					GetCornerVertex(
						x,
						y,
						new Vector2(
							x0,
							y0
						)
					);

				int cornerB =
					GetCornerVertex(
						x + 1,
						y,
						new Vector2(
							x1,
							y0
						)
					);

				int cornerC =
					GetCornerVertex(
						x + 1,
						y + 1,
						new Vector2(
							x1,
							y1
						)
					);

				int cornerD =
					GetCornerVertex(
						x,
						y + 1,
						new Vector2(
							x0,
							y1
						)
					);

				int top = -1;
				int right = -1;
				int bottom = -1;
				int left = -1;

				if (
					renderWater[indexA] !=
					renderWater[indexB]
				)
				{
					top =
						GetHorizontalEdgeVertex(
							x,
							y,
							Interpolate(a, b),
							x0,
							x1,
							y0
						);
				}

				if (
					renderWater[indexB] !=
					renderWater[indexC]
				)
				{
					right =
						GetVerticalEdgeVertex(
							x + 1,
							y,
							Interpolate(b, c),
							x1,
							y0,
							y1
						);
				}

				if (
					renderWater[indexD] !=
					renderWater[indexC]
				)
				{
					bottom =
						GetHorizontalEdgeVertex(
							x,
							y + 1,
							Interpolate(d, c),
							x0,
							x1,
							y1
						);
				}

				if (
					renderWater[indexD] !=
					renderWater[indexA]
				)
				{
					left =
						GetVerticalEdgeVertex(
							x,
							y,
							Interpolate(d, a),
							x0,
							y0,
							y1
						);
				}

				AddCell(
					caseIndex,
					cornerA,
					cornerB,
					cornerC,
					cornerD,
					top,
					right,
					bottom,
					left
				);
			}
		}

		Stopwatch meshTimer =
			Stopwatch.StartNew();

		BuildMesh();

		meshTimer.Stop();

		profilerMeshTime +=
			meshTimer.Elapsed.TotalMilliseconds;
	}

	// ============================================================
	// Render connectivity mask
	// ============================================================

	private void BuildRenderWaterMask(
		float[] values)
	{
		int count =
			renderWater.Length;

		for (
			int i = 0;
			i < count;
			i++)
		{
			renderWater[i] =
				values[i] >=
				SurfaceThreshold;
		}

		System.Array.Copy(
			renderWater,
			renderWaterScratch,
			count
		);

		for (
			int y = 0;
			y < height;
			y++)
		{
			int row =
				y * width;

			for (
				int x = 1;
				x < width - 1;
				x++)
			{
				int index =
					row + x;

				if (renderWater[index])
					continue;

				if (
					renderWater[index - 1] &&
					renderWater[index + 1]
				)
				{
					renderWaterScratch[index] =
						true;
				}
			}
		}

		for (
			int y = 1;
			y < height - 1;
			y++)
		{
			int row =
				y * width;

			for (
				int x = 0;
				x < width;
				x++)
			{
				int index =
					row + x;

				if (renderWaterScratch[index])
					continue;

				int above =
					index - width;

				int below =
					index + width;

				if (
					renderWater[above] &&
					renderWater[below]
				)
				{
					renderWaterScratch[index] =
						true;
				}
			}
		}

		System.Array.Copy(
			renderWaterScratch,
			renderWater,
			count
		);
	}

	// ============================================================
	// Calculate visual data
	// ============================================================

	private void CalculateVisualData(
		DensityField densityField)
	{
		visualGenerationId++;

		if (visualGenerationId == int.MaxValue)
		{
			System.Array.Clear(
				visualGeneration,
				0,
				visualGeneration.Length
			);

			visualGenerationId = 1;
		}

		int minX =
			Mathf.Max(
				0,
				densityField.ActiveMinX - 1
			);

		int maxX =
			Mathf.Min(
				width - 1,
				densityField.ActiveMaxX + 1
			);

		int minY =
			Mathf.Max(
				0,
				densityField.ActiveMinY - 1
			);

		int maxY =
			Mathf.Min(
				height - 1,
				densityField.ActiveMaxY + 1
			);

		for (
			int x = minX;
			x <= maxX;
			x++)
		{
			int y = minY;

			while (y <= maxY)
			{
				int index =
					y * width + x;

				if (!renderWater[index])
				{
					y++;
					continue;
				}

				int startY =
					y;

				while (
					y < maxY &&
					renderWater[
						(y + 1) * width + x
					]
				)
				{
					y++;
				}

				int endY =
					y;

				float top =
					startY * cellSize;

				float bottom =
					(endY + 1) * cellSize;

				float bodyHeight =
					bottom - top;

				if (bodyHeight < 48.0f)
					bodyHeight = 48.0f;

				for (
					int bodyY = startY;
					bodyY <= endY;
					bodyY++)
				{
					int bodyIndex =
						bodyY * width + x;

					float worldY =
						bodyY * cellSize;

					float depth =
						(worldY - top) /
						bodyHeight;

					depth =
						Mathf.Clamp(
							depth,
							0.0f,
							1.0f
						);

					float surfaceDistance =
						Mathf.Abs(
							worldY - top
						);

					float surface =
						1.0f -
						Mathf.Clamp(
							surfaceDistance / 28.0f,
							0.0f,
							1.0f
						);

					surface *= surface;

					visualDepth[bodyIndex] =
						depth;

					visualSurface[bodyIndex] =
						surface;

					visualGeneration[bodyIndex] =
						visualGenerationId;
				}

				y++;
			}
		}
	}

	// ============================================================
	// Fast visual lookup
	// ============================================================

	private Color GetVertexColor(
		Vector2 position)
	{
		int x =
			Mathf.Clamp(
				Mathf.FloorToInt(
					position.X / cellSize
				),
				0,
				width - 1
			);

		int y =
			Mathf.Clamp(
				Mathf.FloorToInt(
					position.Y / cellSize
				),
				0,
				height - 1
			);

		int index =
			y * width + x;

		if (
			visualGeneration[index] ==
			visualGenerationId
		)
		{
			return new Color(
				visualDepth[index],
				visualSurface[index],
				0.0f,
				1.0f
			);
		}

		// --------------------------------------------------------
		// Instead of searching neighboring cells, simply use the
		// nearest valid vertical sample.
		//
		// This fallback is rarely used and is deliberately cheap.
		// --------------------------------------------------------

		for (
			int offsetY = 1;
			offsetY <= 2;
			offsetY++)
		{
			int sampleY =
				y - offsetY;

			if (sampleY < 0)
				break;

			int sampleIndex =
				sampleY * width + x;

			if (
				visualGeneration[sampleIndex] ==
				visualGenerationId
			)
			{
				return new Color(
					visualDepth[sampleIndex],
					visualSurface[sampleIndex],
					0.0f,
					1.0f
				);
			}
		}

		return new Color(
			0.0f,
			0.0f,
			0.0f,
			1.0f
		);
	}

	// ============================================================
	// Corner vertex
	// ============================================================

	private int GetCornerVertex(
		int x,
		int y,
		Vector2 position)
	{
		int cacheIndex =
			y * (width + 1) +
			x;

		if (
			cornerCacheGeneration[cacheIndex] ==
			cacheGeneration
		)
		{
			return cornerIndices[cacheIndex];
		}

		int index =
			vertices.Count;

		vertices.Add(
			position
		);

		vertexColors.Add(
			GetVertexColor(position)
		);

		cornerIndices[cacheIndex] =
			index;

		cornerCacheGeneration[cacheIndex] =
			cacheGeneration;

		return index;
	}

	// ============================================================
	// Horizontal edge vertex
	// ============================================================

	private int GetHorizontalEdgeVertex(
		int edgeX,
		int edgeY,
		float t,
		float x0,
		float x1,
		float worldY)
	{
		int edgeIndex =
			edgeY * (width - 1) +
			edgeX;

		if (
			horizontalCacheGeneration[edgeIndex] ==
			cacheGeneration
		)
		{
			return horizontalEdgeIndices[edgeIndex];
		}

		Vector2 position =
			new Vector2(
				x0 +
				(x1 - x0) * t,
				worldY
			);

		int index =
			vertices.Count;

		vertices.Add(
			position
		);

		Color color =
			GetVertexColor(position);

		color.B = 1.0f;

		vertexColors.Add(
			color
		);

		horizontalEdgeIndices[edgeIndex] =
			index;

		horizontalCacheGeneration[edgeIndex] =
			cacheGeneration;

		return index;
	}

	// ============================================================
	// Vertical edge vertex
	// ============================================================

	private int GetVerticalEdgeVertex(
		int edgeX,
		int edgeY,
		float t,
		float worldX,
		float y0,
		float y1)
	{
		int edgeIndex =
			edgeY * width +
			edgeX;

		if (
			verticalCacheGeneration[edgeIndex] ==
			cacheGeneration
		)
		{
			return verticalEdgeIndices[edgeIndex];
		}

		Vector2 position =
			new Vector2(
				worldX,
				y0 +
				(y1 - y0) * t
			);

		int index =
			vertices.Count;

		vertices.Add(
			position
		);

		Color color =
			GetVertexColor(position);

		color.B = 1.0f;

		vertexColors.Add(
			color
		);

		verticalEdgeIndices[edgeIndex] =
			index;

		verticalCacheGeneration[edgeIndex] =
			cacheGeneration;

		return index;
	}

	// ============================================================
	// Build Godot mesh
	// ============================================================

	private void BuildMesh()
	{
		mesh.ClearSurfaces();

		if (
			vertices.Count == 0 ||
			indices.Count == 0
		)
		{
			return;
		}

		Vector2[] vertexArray =
			vertices.ToArray();

		int[] indexArray =
			indices.ToArray();

		Color[] colorArray =
			vertexColors.ToArray();

		Godot.Collections.Array arrays =
			new Godot.Collections.Array();

		arrays.Resize(
			(int)Mesh.ArrayType.Max
		);

		arrays[
			(int)Mesh.ArrayType.Vertex
		] =
			vertexArray;

		arrays[
			(int)Mesh.ArrayType.Color
		] =
			colorArray;

		arrays[
			(int)Mesh.ArrayType.Index
		] =
			indexArray;

		mesh.AddSurfaceFromArrays(
			Mesh.PrimitiveType.Triangles,
			arrays
		);
	}

	// ============================================================
	// Density interpolation
	// ============================================================

	private float Interpolate(
		float valueA,
		float valueB)
	{
		float difference =
			valueB - valueA;

		if (
			Mathf.Abs(difference) <
			0.0001f
		)
		{
			return 0.5f;
		}

		float t =
			(SurfaceThreshold - valueA) /
			difference;

		return Mathf.Clamp(
			t,
			0.0f,
			1.0f
		);
	}

	// ============================================================
	// Marching Squares case table
	// ============================================================

	private void AddCell(
		int caseIndex,
		int a,
		int b,
		int c,
		int d,
		int top,
		int right,
		int bottom,
		int left)
	{
		switch (caseIndex)
		{
			case 1:
				AddTriangle(a, top, left);
				break;

			case 2:
				AddTriangle(b, right, top);
				break;

			case 4:
				AddTriangle(c, bottom, right);
				break;

			case 8:
				AddTriangle(d, left, bottom);
				break;

			case 3:
				AddQuad(a, b, right, left);
				break;

			case 6:
				AddQuad(b, c, bottom, top);
				break;

			case 12:
				AddQuad(c, d, left, right);
				break;

			case 5:
				AddTriangle(a, top, left);
				AddTriangle(c, right, bottom);
				break;

			case 10:
				AddTriangle(b, right, top);
				AddTriangle(d, left, bottom);
				break;

			case 7:
				AddPolygon(a, b, c, bottom, left);
				break;

			case 11:
				AddPolygon(a, b, right, bottom, d);
				break;

			case 13:
				AddPolygon(a, top, right, c, d);
				break;

			case 14:
				AddPolygon(b, c, d, left, top);
				break;

			case 9:
				AddQuad(a, top, bottom, d);
				break;

			case 15:
				AddQuad(a, b, c, d);
				break;
		}
	}

	// ============================================================
	// Triangle
	// ============================================================

	private void AddTriangle(
		int a,
		int b,
		int c)
	{
		if (
			a < 0 ||
			b < 0 ||
			c < 0
		)
		{
			return;
		}

		Vector2 va =
			vertices[a];

		Vector2 vb =
			vertices[b];

		Vector2 vc =
			vertices[c];

		float area =
			(vb.X - va.X) *
			(vc.Y - va.Y) -
			(vb.Y - va.Y) *
			(vc.X - va.X);

		if (
			Mathf.Abs(area) <
			0.000001f
		)
		{
			return;
		}

		indices.Add(a);
		indices.Add(b);
		indices.Add(c);
	}

	// ============================================================
	// Quad
	// ============================================================

	private void AddQuad(
		int a,
		int b,
		int c,
		int d)
	{
		AddTriangle(a, b, c);
		AddTriangle(a, c, d);
	}

	// ============================================================
	// Five-point polygon
	// ============================================================

	private void AddPolygon(
		int a,
		int b,
		int c,
		int d,
		int e)
	{
		AddTriangle(a, b, c);
		AddTriangle(a, c, d);
		AddTriangle(a, d, e);
	}
}
