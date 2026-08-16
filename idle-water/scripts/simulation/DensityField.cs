using Godot;
using System;

public class DensityField
{
	private readonly int width;
	private readonly int height;

	private readonly float cellSize;
	private readonly float invCellSize;

	// ------------------------------------------------------------
	// World origin
	//
	// Density cell (0, 0) corresponds to:
	//
	// world = (worldMinX, worldMinY)
	// ------------------------------------------------------------

	private readonly float worldMinX;
	private readonly float worldMinY;

	private readonly float[] values;

	public int Width => width;
	public int Height => height;
	public float CellSize => cellSize;

	public float WorldMinX => worldMinX;
	public float WorldMinY => worldMinY;

	// ------------------------------------------------------------
	// Density kernel
	// ------------------------------------------------------------

	private const float Radius = 18.0f;

	private readonly int kernelRadius;
	private readonly int kernelSize;

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
		float cellSize,
		float worldMinX,
		float worldMinY)
	{
		this.width = width;
		this.height = height;
		this.cellSize = cellSize;
		this.invCellSize = 1.0f / cellSize;

		this.worldMinX = worldMinX;
		this.worldMinY = worldMinY;

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
		KernelEntry[] temporary =
			new KernelEntry[
				kernelSize * kernelSize
			];

		int count = 0;

		float radiusSquared =
			Radius * Radius;

		for (
			int y = -kernelRadius;
			y <= kernelRadius;
			y++)
		{
			for (
				int x = -kernelRadius;
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

				if (
					distanceSquared >=
					radiusSquared)
				{
					continue;
				}

				float distance =
					Mathf.Sqrt(
						distanceSquared
					);

				float q =
					1.0f -
					distance / Radius;

				float weight =
					q * q * q * q;

				if (weight <= 0.0f)
				{
					continue;
				}

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
	// World → density coordinates
	// ------------------------------------------------------------

	private int WorldToCellX(
		float worldX)
	{
		return Mathf.FloorToInt(
			(worldX - worldMinX) *
			invCellSize
		);
	}

	private int WorldToCellY(
		float worldY)
	{
		return Mathf.FloorToInt(
			(worldY - worldMinY) *
			invCellSize
		);
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
			WorldToCellX(
				worldX
			);

		int y =
			WorldToCellY(
				worldY
			);

		if (
			x < 0 ||
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
	// ------------------------------------------------------------

	public void AddParticle(
		float worldX,
		float worldY)
	{
		int centerX =
			WorldToCellX(
				worldX
			);

		int centerY =
			WorldToCellY(
				worldY
			);

		if (
			centerX < -kernelRadius ||
			centerX >= width + kernelRadius ||
			centerY < -kernelRadius ||
			centerY >= height + kernelRadius)
		{
			return;
		}

		for (
			int i = 0;
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

			if (
				x < 0 ||
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
		if (
			x < 0 ||
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
		{
			activeMinX = x;
		}

		if (y < activeMinY)
		{
			activeMinY = y;
		}

		if (x > activeMaxX)
		{
			activeMaxX = x;
		}

		if (y > activeMaxY)
		{
			activeMaxY = y;
		}
	}
}
