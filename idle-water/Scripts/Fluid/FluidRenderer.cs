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

	private float surfaceThreshold = 0.3f;

	// Profiler
	private int profilerFrameCount = 0;

	private double profilerMeshTime = 0.0;
	private double profilerTotalTime = 0.0;

	// Reused every frame.
	private readonly List<Vector2> vertices =
		new List<Vector2>(16384);

	private readonly List<int> indices =
		new List<int>(24576);

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

		waterMesh = new MeshInstance2D();

		mesh = new ArrayMesh();

		waterMesh.Mesh = mesh;

		CreateWaterMaterial();

		AddChild(waterMesh);
	}

	// ------------------------------------------------------------
	// Water material
	// ------------------------------------------------------------

	private void CreateWaterMaterial()
	{
		Shader shader = new Shader();

		shader.Code = @"
shader_type canvas_item;

uniform vec3 water_color : source_color =
vec3(0.02, 0.7, 1.0);

uniform float water_alpha = 0.50;

void fragment()
{
	COLOR = vec4(
		water_color,
		water_alpha
	);
}
";

		ShaderMaterial material =
			new ShaderMaterial();

		material.Shader = shader;

		waterMesh.Material = material;
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

		// --------------------------------------------------------
		// Find active density bounds.
		// --------------------------------------------------------

		int minX = width;
		int minY = height;
		int maxX = -1;
		int maxY = -1;

		for (int y = 0; y < height; y++)
		{
			int rowStart =
				y * width;

			for (int x = 0; x < width; x++)
			{
				if (values[rowStart + x] >=
					surfaceThreshold)
				{
					if (x < minX)
						minX = x;

					if (x > maxX)
						maxX = x;

					if (y < minY)
						minY = y;

					if (y > maxY)
						maxY = y;
				}
			}
		}

		// No fluid.
		if (maxX < 0)
		{
			mesh.ClearSurfaces();
			return;
		}

		// --------------------------------------------------------
		// Expand by one cell.
		//
		// Marching Squares needs the neighboring corners around
		// the active region.
		// --------------------------------------------------------

		minX =
			Mathf.Max(
				0,
				minX - 1
			);

		minY =
			Mathf.Max(
				0,
				minY - 1
			);

		maxX =
			Mathf.Min(
				width - 1,
				maxX + 1
			);

		maxY =
			Mathf.Min(
				height - 1,
				maxY + 1
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

				// ------------------------------------------------
				// Intersections.
				// ------------------------------------------------

				float topT =
					Interpolate(
						a,
						b
					);

				float rightT =
					Interpolate(
						b,
						c
					);

				float bottomT =
					Interpolate(
						d,
						c
					);

				float leftT =
					Interpolate(
						d,
						a
					);

				float x0 =
					x * cellSize;

				float y0 =
					y * cellSize;

				float x1 =
					(x + 1) * cellSize;

				float y1 =
					(y + 1) * cellSize;

				Vector2 top =
					new Vector2(
						x0 +
						(x1 - x0) * topT,
						y0
					);

				Vector2 right =
					new Vector2(
						x1,
						y0 +
						(y1 - y0) * rightT
					);

				Vector2 bottom =
					new Vector2(
						x0 +
						(x1 - x0) * bottomT,
						y1
					);

				Vector2 left =
					new Vector2(
						x0,
						y0 +
						(y1 - y0) * leftT
					);

				// ------------------------------------------------
				// Cell corners.
				// ------------------------------------------------

				Vector2 cornerA =
					new Vector2(
						x0,
						y0
					);

				Vector2 cornerB =
					new Vector2(
						x1,
						y0
					);

				Vector2 cornerC =
					new Vector2(
						x1,
						y1
					);

				Vector2 cornerD =
					new Vector2(
						x0,
						y1
					);

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
		// Upload generated geometry.
		// --------------------------------------------------------

		Stopwatch meshTimer =
			Stopwatch.StartNew();

		BuildMesh();

		meshTimer.Stop();

		profilerMeshTime +=
			meshTimer.Elapsed.TotalMilliseconds;
	}

	// ============================================================
	// Build Godot mesh
	// ============================================================

	private void BuildMesh()
	{
		mesh.ClearSurfaces();

		if (vertices.Count == 0)
			return;

		Vector2[] vertexArray =
			vertices.ToArray();

		int[] indexArray =
			indices.ToArray();

		Vector2[] uvs =
			new Vector2[
				vertexArray.Length
			];

		float worldWidth =
			width * cellSize;

		float worldHeight =
			height * cellSize;

		for (int i = 0;
			 i < vertexArray.Length;
			 i++)
		{
			uvs[i] =
				new Vector2(
					vertexArray[i].X /
						worldWidth,
					vertexArray[i].Y /
						worldHeight
				);
		}

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
			(int)Mesh.ArrayType.TexUV
		] =
			uvs;

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
		Vector2 a,
		Vector2 b,
		Vector2 c,
		Vector2 d,
		Vector2 top,
		Vector2 right,
		Vector2 bottom,
		Vector2 left)
	{
		switch (caseIndex)
		{
			// ----------------------------------------------------
			// Single-corner cases
			// ----------------------------------------------------

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

			// ----------------------------------------------------
			// Two adjacent corners
			// ----------------------------------------------------

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

			// ----------------------------------------------------
			// TWO OPPOSITE CORNERS
			//
			// Cases 5 and 10 are intentionally two separate
			// triangles. These represent two disconnected
			// regions inside the cell.
			// ----------------------------------------------------

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

			// ----------------------------------------------------
			// Three corners inside
			// ----------------------------------------------------

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

			// ----------------------------------------------------
			// IMPORTANT: CASE 9
			//
			// A and D are inside.
			//
			// The filled region is:
			//
			//       A ----- top
			//       |        |
			//       D ----- bottom
			//
			// This is a QUAD.
			//
			// The old implementation added 'left' as a fifth
			// vertex. That point lies on the A-D edge and created
			// a degenerate extra triangle.
			// ----------------------------------------------------

			case 9:
				AddQuad(
					a,
					top,
					bottom,
					d
				);
				break;

			// ----------------------------------------------------
			// Completely inside
			// ----------------------------------------------------

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
		Vector2 a,
		Vector2 b,
		Vector2 c)
	{
		// --------------------------------------------------------
		// Reject zero-area triangles.
		//
		// This protects the generated mesh from degenerate
		// triangles caused by interpolation values landing on
		// the same point.
		// --------------------------------------------------------

		float area =
			(b.X - a.X) *
			(c.Y - a.Y) -
			(b.Y - a.Y) *
			(c.X - a.X);

		if (Mathf.Abs(area) <
			0.000001f)
		{
			return;
		}

		int start =
			vertices.Count;

		vertices.Add(a);
		vertices.Add(b);
		vertices.Add(c);

		indices.Add(start);
		indices.Add(start + 1);
		indices.Add(start + 2);
	}

	// ============================================================
	// Quad
	// ============================================================

	private void AddQuad(
		Vector2 a,
		Vector2 b,
		Vector2 c,
		Vector2 d)
	{
		// --------------------------------------------------------
		// Triangle 1
		// --------------------------------------------------------

		AddTriangle(
			a,
			b,
			c
		);

		// --------------------------------------------------------
		// Triangle 2
		// --------------------------------------------------------

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
		Vector2 a,
		Vector2 b,
		Vector2 c,
		Vector2 d,
		Vector2 e)
	{
		// These five-point polygons are convex for the standard
		// three-corners-inside Marching Squares cases.
		//
		// Fan triangulation is therefore safe.

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
