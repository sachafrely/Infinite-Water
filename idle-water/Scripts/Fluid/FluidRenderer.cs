
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

	private float surfaceThreshold = 0.28f;

	// ============================================================
	// Render connectivity
	// ============================================================

	private bool[] renderWater;
	private bool[] renderWaterScratch;

	// ============================================================
	// Profiler
	// ============================================================

	private int profilerFrameCount = 0;

	private double profilerMeshTime = 0.0;
	private double profilerTotalTime = 0.0;

	// ============================================================
	// Mesh buffers
	// ============================================================

	private readonly List<Vector2> vertices =
		new List<Vector2>(16384);

	private readonly List<int> indices =
		new List<int>(24576);

	// R = depth
	// G = surface influence
	// B = edge lighting
	// A = 1
	private readonly List<Color> vertexColors =
		new List<Color>(16384);

	// ============================================================
	// Vertex caches
	// ============================================================

	private int[] cornerIndices;

	private int[] horizontalEdgeIndices;

	private int[] verticalEdgeIndices;

	// ============================================================
	// Local water-body information
	// ============================================================

	private float[] bodyTopY;
	private float[] bodyBottomY;
	private bool[] bodyValid;

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

		waterMesh = new MeshInstance2D();
		mesh = new ArrayMesh();

		waterMesh.Mesh = mesh;

		cornerIndices =
			new int[width * height];

		horizontalEdgeIndices =
			new int[
				Mathf.Max(
					1,
					(width - 1) * height
				)
			];

		verticalEdgeIndices =
			new int[
				Mathf.Max(
					1,
					width * (height - 1)
				)
			];

		bodyTopY =
			new float[
				width * height
			];

		bodyBottomY =
			new float[
				width * height
			];

		bodyValid =
			new bool[
				width * height
			];

		renderWater =
			new bool[
				width * height
			];

		renderWaterScratch =
			new bool[
				width * height
			];

		CreateWaterMaterial();

		AddChild(waterMesh);
	}

	// ============================================================
	// Water material
	// ============================================================

	private void CreateWaterMaterial()
	{
		Shader shader = new Shader();

		shader.Code = @"
shader_type canvas_item;
render_mode unshaded;

// ------------------------------------------------------------
// Water palette
// ------------------------------------------------------------

uniform vec3 deep_color : source_color =
vec3(0.005, 0.16, 0.48);

uniform vec3 middle_color : source_color =
vec3(0.005, 0.16, 0.48);

uniform vec3 shallow_color : source_color =
vec3(0.01, 0.38, 0.78);

uniform vec3 surface_color : source_color =
vec3(0.55, 0.78, 0.95);

// ------------------------------------------------------------
// Alpha
// ------------------------------------------------------------

uniform float water_alpha = 0.5;

// ------------------------------------------------------------
// Surface glow
// ------------------------------------------------------------

uniform float surface_glow_strength = 0.5;

// ------------------------------------------------------------
// Edge lighting
// ------------------------------------------------------------

uniform float edge_light_strength = 0.33;

// ------------------------------------------------------------
// Shimmer
// ------------------------------------------------------------

uniform float shimmer_strength = 0.1;
uniform float shimmer_speed = 0.2;
uniform float shimmer_scale = 0.045;

// ------------------------------------------------------------
// Data passed from vertex color
// ------------------------------------------------------------

varying float water_depth;
varying float surface_factor;
varying float edge_factor;

varying vec2 water_position;

void vertex()
{
	// IMPORTANT:
	// COLOR is ONLY used as a data container here.
	// We never use it directly as the final water color.

	water_depth = clamp(COLOR.r, 0.0, 1.0);
	surface_factor = clamp(COLOR.g, 0.0, 1.0);
	edge_factor = clamp(COLOR.b, 0.0, 1.0);

	water_position = VERTEX;
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

	// --------------------------------------------------------
	// Depth gradient
	//
	// IMPORTANT:
	// This is the ONLY base water color.
	// It cannot become red/green from vertex data.
	// --------------------------------------------------------

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

	// --------------------------------------------------------
	// Surface glow
	// --------------------------------------------------------

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

	// Subtle surface highlight.

	water +=
		surface_color *
		glow *
		0.035;

	// --------------------------------------------------------
	// Animated shimmer
	// --------------------------------------------------------

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

	// Neutral white shimmer.
	// It cannot turn the water green.

	water +=
		vec3(
			shimmer,
			shimmer,
			shimmer
		);

	// --------------------------------------------------------
	// Edge lighting
	// --------------------------------------------------------

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

	// --------------------------------------------------------
	// Final surface highlight
	// --------------------------------------------------------

	float highlight =
		pow(
			surface,
			5.0
		);

	water +=
		surface_color *
		highlight *
		0.10;

	// --------------------------------------------------------
	// FINAL OUTPUT
	//
	// Explicitly construct the displayed water color.
	// Vertex COLOR is NEVER used directly.
	// --------------------------------------------------------

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
				"(avg ms over 60 frames): " +
				"Particles=" +
				particles.Count +
				" " +
				"Total=" +
				(profilerTotalTime / 60.0)
					.ToString("F2") +
				"ms " +
				"Mesh=" +
				(profilerMeshTime / 60.0)
					.ToString("F2") +
				"ms " +
				"Vertices=" +
				vertices.Count +
				" " +
				"Indices=" +
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

		System.Array.Fill(
			cornerIndices,
			-1
		);

		System.Array.Fill(
			horizontalEdgeIndices,
			-1
		);

		System.Array.Fill(
			verticalEdgeIndices,
			-1
		);

		if (!densityField.HasDensity)
		{
			mesh.ClearSurfaces();
			return;
		}

		BuildRenderWaterMask(
			values
		);

		CalculateWaterBodyProfiles(
			values
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

		for (int y = minY;
			 y < maxY;
			 y++)
		{
			int row =
				y * width;

			int nextRow =
				(y + 1) * width;

			for (int x = minX;
				 x < maxX;
				 x++)
			{
				int indexA =
					row + x;

				int indexB =
					row + x + 1;

				int indexC =
					nextRow + x + 1;

				int indexD =
					nextRow + x;

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

				float y0 =
					y * cellSize;

				float x1 =
					(x + 1) * cellSize;

				float y1 =
					(y + 1) * cellSize;

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
							Interpolate(
								a,
								b
							),
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
							Interpolate(
								b,
								c
							),
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
							Interpolate(
								d,
								c
							),
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
							Interpolate(
								d,
								a
							),
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
	// Render-only connectivity mask
	// ============================================================

	private void BuildRenderWaterMask(
		float[] values)
	{
		for (int i = 0;
			 i < renderWater.Length;
			 i++)
		{
			renderWater[i] =
				values[i] >=
				surfaceThreshold;
		}

		System.Array.Copy(
			renderWater,
			renderWaterScratch,
			renderWater.Length
		);

		// Horizontal hole filling.

		for (int y = 0;
			 y < height;
			 y++)
		{
			int row =
				y * width;

			for (int x = 1;
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

		// Vertical hole filling.

		for (int y = 1;
			 y < height - 1;
			 y++)
		{
			int row =
				y * width;

			for (int x = 0;
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
			renderWater.Length
		);
	}

	// ============================================================
	// Calculate independent vertical water bodies
	// ============================================================

	private void CalculateWaterBodyProfiles(
		float[] values)
	{
		System.Array.Fill(
			bodyTopY,
			0.0f
		);

		System.Array.Fill(
			bodyBottomY,
			0.0f
		);

		System.Array.Fill(
			bodyValid,
			false
		);

		for (int x = 0;
			 x < width;
			 x++)
		{
			int y = 0;

			while (y < height)
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
					y + 1 < height &&
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

				for (int bodyY = startY;
					 bodyY <= endY;
					 bodyY++)
				{
					int bodyIndex =
						bodyY * width + x;

					bodyTopY[bodyIndex] =
						top;

					bodyBottomY[bodyIndex] =
						bottom;

					bodyValid[bodyIndex] =
						true;
				}

				y++;
			}
		}
	}

	// ============================================================
	// Find local water body
	// ============================================================

	private bool TryGetLocalWaterBody(
		Vector2 position,
		out float top,
		out float bottom)
	{
		top = 0.0f;
		bottom = 0.0f;

		int centerX =
			Mathf.Clamp(
				Mathf.FloorToInt(
					position.X / cellSize
				),
				0,
				width - 1
			);

		int centerY =
			Mathf.Clamp(
				Mathf.FloorToInt(
					position.Y / cellSize
				),
				0,
				height - 1
			);

		float bestDistance =
			float.MaxValue;

		bool found =
			false;

		const int SearchRadius = 3;

		int minSampleX =
			Mathf.Max(
				0,
				centerX - SearchRadius
			);

		int maxSampleX =
			Mathf.Min(
				width - 1,
				centerX + SearchRadius
			);

		int minSampleY =
			Mathf.Max(
				0,
				centerY - SearchRadius
			);

		int maxSampleY =
			Mathf.Min(
				height - 1,
				centerY + SearchRadius
			);

		for (int sampleY = minSampleY;
			 sampleY <= maxSampleY;
			 sampleY++)
		{
			for (int sampleX = minSampleX;
				 sampleX <= maxSampleX;
				 sampleX++)
			{
				int sampleIndex =
					sampleY * width +
					sampleX;

				if (!bodyValid[sampleIndex])
					continue;

				float sampleWorldX =
					sampleX * cellSize;

				float sampleWorldY =
					sampleY * cellSize;

				float dx =
					position.X -
					sampleWorldX;

				float dy =
					position.Y -
					sampleWorldY;

				float distance =
					dx * dx +
					dy * dy;

				if (distance <
					bestDistance)
				{
					bestDistance =
						distance;

					top =
						bodyTopY[
							sampleIndex
						];

					bottom =
						bodyBottomY[
							sampleIndex
						];

					found =
						true;
				}
			}
		}

		return found;
	}

	// ============================================================
	// Vertex visual information
	// ============================================================

	private Color GetVertexColor(
		Vector2 position)
	{
		float top;
		float bottom;

		bool hasBody =
			TryGetLocalWaterBody(
				position,
				out top,
				out bottom
			);

		if (!hasBody)
		{
			// IMPORTANT:
			// Never return green/red fallback data.
			// Use neutral data instead.
			return new Color(
				0.0f,
				0.0f,
				0.0f,
				1.0f
			);
		}

		float bodyHeight =
			bottom - top;

		bodyHeight =
			Mathf.Max(
				bodyHeight,
				48.0f
			);

		float depth =
			(position.Y - top) /
			bodyHeight;

		depth =
			Mathf.Clamp(
				depth,
				0.0f,
				1.0f
			);

		float surfaceDistance =
			Mathf.Abs(
				position.Y - top
			);

		float surface =
			1.0f -
			Mathf.Clamp(
				surfaceDistance / 28.0f,
				0.0f,
				1.0f
			);

		surface =
			surface * surface;

		return new Color(
			depth,
			surface,
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
			y * width + x;

		int existing =
			cornerIndices[cacheIndex];

		if (existing >= 0)
			return existing;

		int index =
			vertices.Count;

		vertices.Add(
			position
		);

		vertexColors.Add(
			GetVertexColor(
				position
			)
		);

		cornerIndices[cacheIndex] =
			index;

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

		int existing =
			horizontalEdgeIndices[
				edgeIndex
			];

		if (existing >= 0)
			return existing;

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
			GetVertexColor(
				position
			);

		color.B =
			1.0f;

		vertexColors.Add(
			color
		);

		horizontalEdgeIndices[
			edgeIndex
		] =
			index;

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

		int existing =
			verticalEdgeIndices[
				edgeIndex
			];

		if (existing >= 0)
			return existing;

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
			GetVertexColor(
				position
			);

		color.B =
			1.0f;

		vertexColors.Add(
			color
		);

		verticalEdgeIndices[
			edgeIndex
		] =
			index;

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
			(surfaceThreshold - valueA) /
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
				AddTriangle(
					a,
					top,
					left
				);
				break;

			case 2:
				AddTriangle(
					b,
					right,
					top
				);
				break;

			case 4:
				AddTriangle(
					c,
					bottom,
					right
				);
				break;

			case 8:
				AddTriangle(
					d,
					left,
					bottom
				);
				break;

			case 3:
				AddQuad(
					a,
					b,
					right,
					left
				);
				break;

			case 6:
				AddQuad(
					b,
					c,
					bottom,
					top
				);
				break;

			case 12:
				AddQuad(
					c,
					d,
					left,
					right
				);
				break;

			case 5:
				AddTriangle(
					a,
					top,
					left
				);

				AddTriangle(
					c,
					right,
					bottom
				);
				break;

			case 10:
				AddTriangle(
					b,
					right,
					top
				);

				AddTriangle(
					d,
					left,
					bottom
				);
				break;

			case 7:
				AddPolygon(
					a,
					b,
					c,
					bottom,
					left
				);
				break;

			case 11:
				AddPolygon(
					a,
					b,
					right,
					bottom,
					d
				);
				break;

			case 13:
				AddPolygon(
					a,
					top,
					right,
					c,
					d
				);
				break;

			case 14:
				AddPolygon(
					b,
					c,
					d,
					left,
					top
				);
				break;

			case 9:
				AddQuad(
					a,
					top,
					bottom,
					d
				);
				break;

			case 15:
				AddQuad(
					a,
					b,
					c,
					d
				);
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
		AddTriangle(
			a,
			b,
			c
		);

		AddTriangle(
			a,
			c,
			d
		);
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
		AddTriangle(
			a,
			b,
			c
		);

		AddTriangle(
			a,
			c,
			d
		);

		AddTriangle(
			a,
			d,
			e
		);
	}
}
