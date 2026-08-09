using Godot;
using System;

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
	// Density kernel
	// ------------------------------------------------------------

	private const float Radius = 18.0f;

	private readonly int kernelRadius;
	private readonly int kernelSize;

	// Only store non-zero kernel entries.
	private readonly KernelEntry[] kernelEntries;

	private struct KernelEntry
	{
		public int OffsetX;
		public int OffsetY;
		public float Weight;

		public KernelEntry(
			int offsetX,
			int offsetY,
			float weight)
		{
			OffsetX = offsetX;
			OffsetY = offsetY;
			Weight = weight;
		}
	}

	// ------------------------------------------------------------
	// Active bounds
	//
	// This allows FluidRenderer to avoid scanning the entire
	// density field every frame.
	// ------------------------------------------------------------

	private int activeMinX;
	private int activeMinY;
	private int activeMaxX;
	private int activeMaxY;

	public int ActiveMinX => activeMinX;
	public int ActiveMinY => activeMinY;
	public int ActiveMaxX => activeMaxX;
	public int ActiveMaxY => activeMaxY;

	public bool HasDensity =>
		activeMaxX >= activeMinX &&
		activeMaxY >= activeMinY;

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

		values =
			new float[
				width * height
			];

		kernelRadius =
			Mathf.CeilToInt(
				Radius * invCellSize
			);

		kernelSize =
			kernelRadius * 2 + 1;

		kernelEntries =
			BuildKernel();

		ResetBounds();
	}

	// ------------------------------------------------------------
	// Build sparse kernel once.
	// ------------------------------------------------------------

	private KernelEntry[] BuildKernel()
	{
		// Maximum possible number of entries.
		KernelEntry[] temporary =
			new KernelEntry[
				kernelSize * kernelSize
			];

		int count = 0;

		float radiusSquared =
			Radius * Radius;

		for (int y = -kernelRadius;
			 y <= kernelRadius;
			 y++)
		{
			for (int x = -kernelRadius;
				 x <= kernelRadius;
				 x++)
			{
				float dx =
					x * cellSize;

				float dy =
					y * cellSize;

				float distanceSquared =
					dx * dx +
					dy * dy;

				if (distanceSquared >= radiusSquared)
					continue;

				float distance =
					Mathf.Sqrt(
						distanceSquared
					);

				float q =
					1.0f -
					distance / Radius;

				float weight =
					q * q * q;

				if (weight <= 0.0f)
					continue;

				temporary[count++] =
					new KernelEntry(
						x,
						y,
						weight
					);
			}
		}

		KernelEntry[] result =
			new KernelEntry[count];

		Array.Copy(
			temporary,
			result,
			count
		);

		return result;
	}

	// ------------------------------------------------------------
	// AddDensity
	//
	// Compatibility method used by PbfSolver.
	//
	// This adds density to the single grid cell containing the
	// supplied world position.
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

		int index =
			y * width + x;

		values[index] += density;

		UpdateActiveBounds(
			x,
			y
		);
	}

	// ------------------------------------------------------------
	// AddParticle
	//
	// Adds the precomputed density kernel around a particle.
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

		// Particle completely outside influence area.
		if (centerX < -kernelRadius ||
			centerX >= width + kernelRadius ||
			centerY < -kernelRadius ||
			centerY >= height + kernelRadius)
		{
			return;
		}

		// Instead of iterating a square and checking whether
		// every kernel entry is zero, only iterate valid entries.
		for (int i = 0;
			 i < kernelEntries.Length;
			 i++)
		{
			KernelEntry entry =
				kernelEntries[i];

			int x =
				centerX +
				entry.OffsetX;

			int y =
				centerY +
				entry.OffsetY;

			if (x < 0 ||
				y < 0 ||
				x >= width ||
				y >= height)
			{
				continue;
			}

			values[
				y * width + x
			] += entry.Weight;

			UpdateActiveBounds(
				x,
				y
			);
		}
	}

	// ------------------------------------------------------------
	// Clear
	// ------------------------------------------------------------

	public void Clear()
	{
		Array.Clear(
			values,
			0,
			values.Length
		);

		ResetBounds();
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
	// Bounds
	// ------------------------------------------------------------

	private void ResetBounds()
	{
		activeMinX = width;
		activeMinY = height;
		activeMaxX = -1;
		activeMaxY = -1;
	}

	private void UpdateActiveBounds(
		int x,
		int y)
	{
		if (x < activeMinX)
			activeMinX = x;

		if (y < activeMinY)
			activeMinY = y;

		if (x > activeMaxX)
			activeMaxX = x;

		if (y > activeMaxY)
			activeMaxY = y;
	}
}
