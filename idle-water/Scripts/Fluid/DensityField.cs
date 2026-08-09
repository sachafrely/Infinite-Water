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

	// ------------------------------------------------------------
	// Density kernel settings
	// ------------------------------------------------------------

	private const float Radius = 18.0f;

	private readonly int kernelRadius;
	private readonly float[] kernelWeights;

	// ------------------------------------------------------------
	// Constructor
	// ------------------------------------------------------------

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

		// Number of grid cells covered by the radius.
		kernelRadius =
			Mathf.CeilToInt(
				Radius * invCellSize
			);

		int kernelSize =
			kernelRadius * 2 + 1;

		kernelWeights =
			new float[
				kernelSize *
				kernelSize
			];

		BuildKernel();
	}

	// ------------------------------------------------------------
	// Precompute the density kernel.
	//
	// This runs ONLY ONCE.
	// No sqrt is performed during simulation anymore.
	// ------------------------------------------------------------

	private void BuildKernel()
	{
		int kernelSize =
			kernelRadius * 2 + 1;

		for (int ky = -kernelRadius;
			ky <= kernelRadius;
			ky++)
		{
			for (int kx = -kernelRadius;
				kx <= kernelRadius;
				kx++)
			{
				float dx =
					kx * cellSize;

				float dy =
					ky * cellSize;

				float distanceSquared =
					dx * dx +
					dy * dy;

				int index =
					(ky + kernelRadius) *
					kernelSize +
					(kx + kernelRadius);

				if (distanceSquared >=
					Radius * Radius)
				{
					kernelWeights[index] = 0.0f;
					continue;
				}

				float distance =
					Mathf.Sqrt(
						distanceSquared
					);

				float q =
					1.0f -
					distance / Radius;

				kernelWeights[index] =
					q * q * q;
			}
		}
	}

	// ------------------------------------------------------------
	// AddDensity
	// ------------------------------------------------------------

	public void AddDensity(
		float worldX,
		float worldY,
		float density)
	{
		int x =
			(int)(
				worldX *
				invCellSize
			);

		int y =
			(int)(
				worldY *
				invCellSize
			);

		if (x < 0 ||
			y < 0 ||
			x >= width ||
			y >= height)
		{
			return;
		}

		values[
			y * width + x
		] += density;
	}

	// ------------------------------------------------------------
	// Clear
	// ------------------------------------------------------------

	public void Clear()
	{
		System.Array.Clear(
			values,
			0,
			values.Length
		);
	}

	// ------------------------------------------------------------
	// Get
	// ------------------------------------------------------------

	public float Get(
		int x,
		int y)
	{
		if (x < 0 ||
			y < 0 ||
			x >= width ||
			y >= height)
		{
			return 0.0f;
		}

		return values[
			y * width + x
		];
	}

	// ------------------------------------------------------------
	// GetValues
	// ------------------------------------------------------------

	public float[] GetValues()
	{
		return values;
	}

	// ------------------------------------------------------------
	// AddParticle
	//
	// IMPORTANT:
	// No sqrt.
	// No distance calculation.
	// No q calculation.
	// No multiplication for the kernel weight.
	//
	// Everything expensive was precomputed.
	// ------------------------------------------------------------

	public void AddParticle(
		float worldX,
		float worldY)
	{
		int centerX =
			(int)(
				worldX *
				invCellSize
			);

		int centerY =
			(int)(
				worldY *
				invCellSize
			);

		// Completely outside the density field.
		if (centerX < -kernelRadius ||
			centerX >= width + kernelRadius ||
			centerY < -kernelRadius ||
			centerY >= height + kernelRadius)
		{
			return;
		}

		int minX =
			Mathf.Max(
				0,
				centerX - kernelRadius
			);

		int maxX =
			Mathf.Min(
				width - 1,
				centerX + kernelRadius
			);

		int minY =
			Mathf.Max(
				0,
				centerY - kernelRadius
			);

		int maxY =
			Mathf.Min(
				height - 1,
				centerY + kernelRadius
			);

		int kernelSize =
			kernelRadius * 2 + 1;

		for (int y = minY;
			y <= maxY;
			y++)
		{
			int kernelY =
				y -
				centerY +
				kernelRadius;

			int rowStart =
				y * width;

			int kernelRowStart =
				kernelY *
				kernelSize;

			for (int x = minX;
				x <= maxX;
				x++)
			{
				int kernelX =
					x -
					centerX +
					kernelRadius;

				float weight =
					kernelWeights[
						kernelRowStart +
						kernelX
					];

				if (weight <= 0.0f)
					continue;

				values[
					rowStart + x
				] += weight;
			}
		}
	}
}
