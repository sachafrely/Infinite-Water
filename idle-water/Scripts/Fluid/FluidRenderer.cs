using System;
using System.Collections.Generic;
using Godot;

public partial class FluidRenderer : Node2D
{
private MeshInstance2D waterMesh;
private ArrayMesh mesh;


private int width;
private int height;
private float cellSize;

private float surfaceThreshold = 0.12f;

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

private void CreateWaterMaterial()
{
	Shader shader = new Shader();

	shader.Code = @"
```

shader_type canvas_item;

uniform vec3 shallow_color : source_color = vec3(0.05, 0.45, 0.90);
uniform vec3 deep_color : source_color = vec3(0.01, 0.12, 0.45);

void fragment()
{
float depth = UV.y;


vec3 water_color =
	mix(
		shallow_color,
		deep_color,
		depth
	);

// Simple directional lighting.
float light =
	0.85 +
	0.15 * sin(UV.x * 12.0);

water_color *= light;

COLOR =
	vec4(
		water_color,
		0.95
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

public void Update(
	ParticleData particles,
	DensityField densityField)
{
	BuildMarchingSquaresMesh(
		densityField
	);
}

private void BuildMarchingSquaresMesh(
	DensityField densityField)
{
	List<Vector2> vertices =
		new List<Vector2>();

	List<int> indices =
		new List<int>();

	for (int y = 0; y < height - 1; y++)
	{
		for (int x = 0; x < width - 1; x++)
		{
			float a =
				densityField.Get(x, y);

			float b =
				densityField.Get(x + 1, y);

			float c =
				densityField.Get(x + 1, y + 1);

			float d =
				densityField.Get(x, y + 1);

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

			float leftT =
				Interpolate(
					d,
					a
				);

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

			Vector2 top =
				new Vector2(
					(x + topT) * cellSize,
					y * cellSize
				);

			Vector2 right =
				new Vector2(
					(x + 1) * cellSize,
					(y + rightT) * cellSize
				);

			Vector2 bottom =
				new Vector2(
					(x + bottomT) * cellSize,
					(y + 1) * cellSize
				);

			Vector2 left =
				new Vector2(
					x * cellSize,
					(y + leftT) * cellSize
				);

			Vector2 center =
				new Vector2(
					(x + 0.5f) * cellSize,
					(y + 0.5f) * cellSize
				);

			AddCell(
				caseIndex,
				top,
				right,
				bottom,
				left,
				center,
				vertices,
				indices
			);
		}
	}

	if (vertices.Count == 0)
	{
		mesh.ClearSurfaces();
		return;
	}

	ArrayMesh newMesh =
		new ArrayMesh();

	ArrayMesh surfaceMesh =
		CreateMesh(
			vertices,
			indices
		);

	mesh.ClearSurfaces();

	if (surfaceMesh.GetSurfaceCount() > 0)
	{
		Array arrays =
			surfaceMesh.SurfaceGetArrays(0);

		mesh.AddSurfaceFromArrays(
			Mesh.PrimitiveType.Triangles,
			arrays
		);
	}
}

private ArrayMesh CreateMesh(
	List<Vector2> vertices,
	List<int> indices)
{
	ArrayMesh result =
		new ArrayMesh();

	Vector2[] vertexArray =
		vertices.ToArray();

	int[] indexArray =
		indices.ToArray();

	Vector2[] uvs =
		new Vector2[vertexArray.Length];

	float worldWidth =
		width * cellSize;

	float worldHeight =
		height * cellSize;

	for (int i = 0; i < vertexArray.Length; i++)
	{
		uvs[i] =
			new Vector2(
				vertexArray[i].X / worldWidth,
				vertexArray[i].Y / worldHeight
			);
	}

	Array arrays =
		new Array();

	arrays.Resize(
		(int)Mesh.ArrayType.Max
	);

	arrays[(int)Mesh.ArrayType.Vertex] =
		vertexArray;

	arrays[(int)Mesh.ArrayType.TexUV] =
		uvs;

	arrays[(int)Mesh.ArrayType.Index] =
		indexArray;

	result.AddSurfaceFromArrays(
		Mesh.PrimitiveType.Triangles,
		arrays
	);

	return result;
}

private float Interpolate(
	float valueA,
	float valueB)
{
	float difference =
		valueB - valueA;

	if (Mathf.Abs(difference) < 0.0001f)
		return 0.5f;

	float t =
		(surfaceThreshold - valueA)
		/ difference;

	return Mathf.Clamp(
		t,
		0.0f,
		1.0f
	);
}

private void AddTriangle(
	Vector2 a,
	Vector2 b,
	Vector2 c,
	List<Vector2> vertices,
	List<int> indices)
{
	int start =
		vertices.Count;

	vertices.Add(a);
	vertices.Add(b);
	vertices.Add(c);

	indices.Add(start);
	indices.Add(start + 1);
	indices.Add(start + 2);
}

private void AddQuad(
	Vector2 a,
	Vector2 b,
	Vector2 c,
	Vector2 d,
	List<Vector2> vertices,
	List<int> indices)
{
	AddTriangle(
		a,
		b,
		c,
		vertices,
		indices
	);

	AddTriangle(
		a,
		c,
		d,
		vertices,
		indices
	);
}

private void AddCell(
	int caseIndex,
	Vector2 top,
	Vector2 right,
	Vector2 bottom,
	Vector2 left,
	Vector2 center,
	List<Vector2> vertices,
	List<int> indices)
{
	switch (caseIndex)
	{
		case 1:
			AddTriangle(
				top,
				left,
				center,
				vertices,
				indices
			);
			break;

		case 2:
			AddTriangle(
				top,
				right,
				center,
				vertices,
				indices
			);
			break;

		case 3:
			AddQuad(
				top,
				right,
				left,
				left,
				vertices,
				indices
			);
			break;

		case 4:
			AddTriangle(
				right,
				bottom,
				center,
				vertices,
				indices
			);
			break;

		case 5:
			AddTriangle(
				top,
				right,
				center,
				vertices,
				indices
			);

			AddTriangle(
				bottom,
				left,
				center,
				vertices,
				indices
			);
			break;

		case 6:
			AddQuad(
				top,
				right,
				bottom,
				center,
				vertices,
				indices
			);
			break;

		case 7:
			AddQuad(
				top,
				right,
				bottom,
				left,
				vertices,
				indices
			);
			break;

		case 8:
			AddTriangle(
				bottom,
				left,
				center,
				vertices,
				indices
			);
			break;

		case 9:
			AddQuad(
				top,
				bottom,
				left,
				center,
				vertices,
				indices
			);
			break;

		case 10:
			AddTriangle(
				top,
				left,
				center,
				vertices,
				indices
			);

			AddTriangle(
				right,
				bottom,
				center,
				vertices,
				indices
			);
			break;

		case 11:
			AddQuad(
				top,
				right,
				bottom,
				left,
				vertices,
				indices
			);
			break;

		case 12:
			AddQuad(
				right,
				bottom,
				left,
				center,
				vertices,
				indices
			);
			break;

		case 13:
			AddQuad(
				top,
				right,
				bottom,
				left,
				vertices,
				indices
			);
			break;

		case 14:
			AddQuad(
				top,
				right,
				bottom,
				left,
				vertices,
				indices
			);
			break;

		case 15:
			AddQuad(
				top,
				right,
				bottom,
				left,
				vertices,
				indices
			);
			break;
	}
}


}
