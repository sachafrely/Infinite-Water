using Godot;

public class DensityField
{
private readonly int width;
private readonly int height;


private readonly float cellSize;
private readonly float invCellSize;

private readonly float[] values;

public int Width => width;
public int Height => height;
public float CellSize => cellSize;

public DensityField(
	int width,
	int height,
	float cellSize)
{
	this.width = width;
	this.height = height;

	this.cellSize = cellSize;
	this.invCellSize = 1.0f / cellSize;

	values = new float[width * height];
}

public void AddDensity(
	float worldX,
	float worldY,
	float density)
{
	int x = (int)(worldX * invCellSize);
	int y = (int)(worldY * invCellSize);

	if (x < 0 ||
		y < 0 ||
		x >= width ||
		y >= height)
	{
		return;
	}

	values[y * width + x] += density;
}

public void Clear()
{
	System.Array.Clear(
		values,
		0,
		values.Length
	);
}

public float Get(int x, int y)
{
	if (x < 0 ||
		y < 0 ||
		x >= width ||
		y >= height)
	{
		return 0.0f;
	}

	return values[y * width + x];
}

public float[] GetValues()
{
	return values;
}

// Adds a smooth particle influence to the grid.
public void AddParticle(
	float worldX,
	float worldY)
{
	const float Radius = 18.0f;

	float gridX = worldX * invCellSize;
	float gridY = worldY * invCellSize;

	int minX = Mathf.FloorToInt(
		gridX - Radius * invCellSize
	);

	int maxX = Mathf.CeilToInt(
		gridX + Radius * invCellSize
	);

	int minY = Mathf.FloorToInt(
		gridY - Radius * invCellSize
	);

	int maxY = Mathf.CeilToInt(
		gridY + Radius * invCellSize
	);

	for (int y = minY; y <= maxY; y++)
	{
		if (y < 0 || y >= height)
			continue;

		float cellWorldY =
			(y + 0.5f) * cellSize;

		float dy =
			cellWorldY - worldY;

		for (int x = minX; x <= maxX; x++)
		{
			if (x < 0 || x >= width)
				continue;

			float cellWorldX =
				(x + 0.5f) * cellSize;

			float dx =
				cellWorldX - worldX;

			float distance =
				Mathf.Sqrt(dx * dx + dy * dy);

			if (distance >= Radius)
				continue;

			// Smooth radial kernel.
			float q =
				1.0f - distance / Radius;

			// Cubic falloff.
			float weight =
				q * q * q;

			values[y * width + x] += weight;
		}
	}
}


}
