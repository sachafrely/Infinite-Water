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

	// ------------------------------------------------------------
	// Profiler
	// ------------------------------------------------------------

	private int profilerFrameCount = 0;

	private double profilerMeshTime = 0.0;
	private double profilerTotalTime = 0.0;

	// ------------------------------------------------------------
	// Mesh buffers
	// ------------------------------------------------------------

	private readonly List<Vector2> vertices =
		new List<Vector2>(16384);

	private readonly List<int> indices =
		new List<int>(24576);

	// One color per vertex.
	//
	// R = normalized water depth
	// G = surface influence
	// B = unused
	// A = water alpha
	//
	private readonly List<Color> vertexColors =
		new List<Color>(16384);

	// ------------------------------------------------------------
	// Vertex caches
	// ------------------------------------------------------------

	private int[] cornerIndices;

	private int[] horizontalEdgeIndices;

	private int[] verticalEdgeIndices;

	// ------------------------------------------------------------
	// Surface information
	//
	// Stores the first/uppermost density position for every
	// horizontal column.
	// ------------------------------------------------------------

	private float[] surfaceY;

	// ------------------------------------------------------------
	// Initialization
	// ------------------------------------------------------------

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

		// --------------------------------------------------------
		// Allocate vertex caches.
		// --------------------------------------------------------

		cornerIndices =
			new int[
				width * height
			];

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

		surfaceY =
			new float[
				width
			];

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

// ------------------------------------------------------------
// Water colors
// ------------------------------------------------------------

uniform vec3 deep_color : source_color =
	vec3(0.005, 0.16, 0.48);

uniform vec3 middle_color : source_color =
	vec3(0.01, 0.38, 0.78);

uniform vec3 shallow_color : source_color =
	vec3(0.04, 0.68, 0.95);

uniform vec3 surface_color : source_color =
	vec3(0.72, 0.94, 1.0);

// ------------------------------------------------------------
// Transparency
// ------------------------------------------------------------

uniform float water_alpha = 0.76;

// ------------------------------------------------------------
// Surface
// ------------------------------------------------------------

uniform float surface_glow_strength = 0.85;
uniform float surface_glow_width = 0.75;

// ------------------------------------------------------------
// Shimmer
// ------------------------------------------------------------

uniform float shimmer_strength = 0.055;
uniform float shimmer_speed = 0.7;
uniform float shimmer_scale = 0.045;

// ------------------------------------------------------------
// Varyings
//
// These are explicitly passed from the vertex color.
// This avoids relying on COLOR in the fragment stage for
// intermediate calculations.
// ------------------------------------------------------------

varying float water_depth;
varying float surface_factor;
varying vec2 water_position;

void vertex()
{
	// R = actual depth from the CPU.
	// G = actual distance from the water surface.
	water_depth = COLOR.r;
	surface_factor = COLOR.g;

	// Position in the water mesh.
	water_position = VERTEX;
}

void fragment()
{
	// --------------------------------------------------------
	// Read water information.
	// --------------------------------------------------------

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

	// --------------------------------------------------------
	// DEPTH COLOR
	//
	// 0.0 = surface
	// 1.0 = deep water
	//
	// This is completely independent of screen position.
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
	// SURFACE GLOW
	//
	// Only the upper portion of the water receives this.
	// --------------------------------------------------------

	float glow =
		pow(
			surface,
			2.5
		);

	water =
		mix(
			water,
			surface_color,
			glow * surface_glow_strength
		);

	// --------------------------------------------------------
	// SUBTLE WATER SHIMMER
	//
	// Important:
	// This is a brightness variation, NOT a green color.
	// --------------------------------------------------------

	float wave1 =
		sin(
			water_position.x * shimmer_scale +
			water_position.y * shimmer_scale * 0.35 +
			TIME * shimmer_speed
		);

	float wave2 =
		sin(
			water_position.x * shimmer_scale * 1.73 -
			water_position.y * shimmer_scale * 0.55 -
			TIME * shimmer_speed * 0.73
		);

	float wave =
		(wave1 + wave2) * 0.5;

	// Convert from [-1,1] to [0,1].
	wave =
		wave * 0.5 + 0.5;

	// Shimmer is strongest near the surface.
	float shimmer_mask =
		0.25 +
		surface * 0.75;

	float shimmer =
		wave *
		shimmer_strength *
		shimmer_mask;

	// Add neutral light instead of green.
	water +=
		vec3(
			shimmer,
			shimmer,
			shimmer
		);

	// --------------------------------------------------------
	// Very subtle surface reflection.
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
	// Final output.
	// --------------------------------------------------------

	COLOR =
		vec4(
			clamp(
				water,
				0.0,
				1.0
			),
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
	// Marching Squares
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

		// --------------------------------------------------------
		// Calculate actual surface height for every X column.
		//
		// This is the important part:
		// depth is measured from the water surface.
		// --------------------------------------------------------

		CalculateSurfaceProfile(
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

		// --------------------------------------------------------
		// Marching Squares.
		// --------------------------------------------------------

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
				float a =
					values[row + x];

				float b =
					values[row + x + 1];

				float c =
					values[nextRow + x + 1];

				float d =
					values[nextRow + x];

				int caseIndex = 0;

				if (a >= surfaceThreshold)
					caseIndex |= 1;

				if (b >= surfaceThreshold)
					caseIndex |= 2;

				if (c >= surfaceThreshold)
					caseIndex |= 4;

				if (d >= surfaceThreshold)
					caseIndex |= 8;

				if (caseIndex == 0)
					continue;

				float x0 =
					x * cellSize;

				float y0 =
					y * cellSize;

				float x1 =
					(x + 1) * cellSize;

				float y1 =
					(y + 1) * cellSize;

				// ------------------------------------------------
				// Corner vertices.
				// ------------------------------------------------

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

				// ------------------------------------------------
				// Edge vertices.
				// ------------------------------------------------

				int top = -1;
				int right = -1;
				int bottom = -1;
				int left = -1;

				if (
					(a >= surfaceThreshold) !=
					(b >= surfaceThreshold)
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
					(b >= surfaceThreshold) !=
					(c >= surfaceThreshold)
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
					(d >= surfaceThreshold) !=
					(c >= surfaceThreshold)
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
					(d >= surfaceThreshold) !=
					(a >= surfaceThreshold)
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

		// --------------------------------------------------------
		// Upload geometry.
		// --------------------------------------------------------

		Stopwatch meshTimer =
			Stopwatch.StartNew();

		BuildMesh();

		meshTimer.Stop();

		profilerMeshTime +=
			meshTimer.Elapsed.TotalMilliseconds;
	}

	// ============================================================
	// Calculate surface profile
	// ============================================================

	private void CalculateSurfaceProfile(
		float[] values)
	{
		for (int x = 0;
			 x < width;
			 x++)
		{
			float foundY =
				-1.0f;

			for (int y = 0;
				 y < height;
				 y++)
			{
				float value =
					values[
						y * width + x
					];

				if (value >= surfaceThreshold)
				{
					foundY =
						y * cellSize;

					break;
				}
			}

			surfaceY[x] =
				foundY;
		}

		// --------------------------------------------------------
		// Fill gaps in the surface profile.
		//
		// This prevents isolated empty columns from producing
		// incorrect depth values.
		// --------------------------------------------------------

		for (int x = 0;
			 x < width;
			 x++)
		{
			if (surfaceY[x] >= 0.0f)
				continue;

			float left =
				FindNearestSurface(
					x,
					-1
				);

			float right =
				FindNearestSurface(
					x,
					1
				);

			if (left >= 0.0f &&
				right >= 0.0f)
			{
				surfaceY[x] =
					(left + right) * 0.5f;
			}
			else if (left >= 0.0f)
			{
				surfaceY[x] =
					left;
			}
			else if (right >= 0.0f)
			{
				surfaceY[x] =
					right;
			}
			else
			{
				surfaceY[x] =
					0.0f;
			}
		}
	}

	// ============================================================
	// Find nearest valid surface
	// ============================================================

	private float FindNearestSurface(
		int startX,
		int direction)
	{
		int x =
			startX + direction;

		while (x >= 0 &&
			   x < width)
		{
			if (surfaceY[x] >= 0.0f)
				return surfaceY[x];

			x += direction;
		}

		return -1.0f;
	}

	// ============================================================
	// Calculate vertex visual information
	// ============================================================

	private Color GetVertexColor(
		Vector2 position)
	{
		int column =
			Mathf.Clamp(
				Mathf.RoundToInt(
					position.X / cellSize
				),
				0,
				width - 1
			);

		float top =
			surfaceY[column];

		if (top < 0.0f)
			top = position.Y;

		// --------------------------------------------------------
		// Approximate water depth.
		//
		// We use the active water height as normalization so
		// deeper parts of the same body become progressively
		// darker.
		// --------------------------------------------------------

		float bottom =
			height * cellSize;

		float totalDepth =
			Mathf.Max(
				32.0f,
				bottom - top
			);

		float depth =
			(position.Y - top) /
			totalDepth;

		depth =
			Mathf.Clamp(
				depth,
				0.0f,
				1.0f
			);

		// --------------------------------------------------------
		// Surface glow.
		//
		// 1 at the surface.
		// Quickly fades away underneath it.
		// --------------------------------------------------------

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
			edgeY * (width - 1) + edgeX;

		int existing =
			horizontalEdgeIndices[edgeIndex];

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

		vertexColors.Add(
			GetVertexColor(
				position
			)
		);

		horizontalEdgeIndices[edgeIndex] =
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
			edgeY * width + edgeX;

		int existing =
			verticalEdgeIndices[edgeIndex];

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

		vertexColors.Add(
			GetVertexColor(
				position
			)
		);

		verticalEdgeIndices[edgeIndex] =
			index;

		return index;
	}

	// ============================================================
	// Build Godot mesh
	// ============================================================

	private void BuildMesh()
	{
		mesh.ClearSurfaces();

		if (vertices.Count == 0 ||
			indices.Count == 0)
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

		if (Mathf.Abs(difference) <
			0.0001f)
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
		if (a < 0 ||
			b < 0 ||
			c < 0)
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

		if (Mathf.Abs(area) <
			0.000001f)
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
