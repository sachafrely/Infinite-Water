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

	public void Clear()
	{
		System.Array.Clear(values, 0, values.Length);
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

	public void AddDensity(
		float worldX,
		float worldY,
		float density)
	{
		int x =
			(int)(worldX * invCellSize);

		int y =
			(int)(worldY * invCellSize);

		if (x < 0 ||
			y < 0 ||
			x >= width ||
			y >= height)
		{
			return;
		}

		values[y * width + x] += density;
	}
}
