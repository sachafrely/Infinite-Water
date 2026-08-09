
using Godot;
using System;

public class SpatialHash
{
	private readonly float cellSize;
	private readonly float invCellSize;

	private readonly int[] head;
	private readonly int[] next;

	private readonly int width;
	private readonly int height;

	public SpatialHash(
		int maxParticles,
		float cellSize,
		int width,
		int height)
	{
		this.cellSize = cellSize;
		this.invCellSize = 1.0f / cellSize;

		this.width = width;
		this.height = height;

		head = new int[width * height];
		next = new int[maxParticles];

		Clear();
	}

	public void Clear()
	{
		Array.Fill(head, -1);
	}

	// ============================================================
	// Insert
	// ============================================================

	public void Insert(
		int id,
		Vector2 position)
	{
		Insert(
			id,
			position.X,
			position.Y
		);
	}

	public void Insert(
		int id,
		float posX,
		float posY)
	{
		int x =
			(int)(posX * invCellSize);

		int y =
			(int)(posY * invCellSize);

		if (x < 0 ||
			y < 0 ||
			x >= width ||
			y >= height)
		{
			next[id] = -1;
			return;
		}

		int cell =
			y * width + x;

		next[id] =
			head[cell];

		head[cell] =
			id;
	}

	// ============================================================
	// PBF Query
	//
	// Finds ONLY particles whose actual distance is <= radius.
	//
	// This is the important version used by PbfSolver.
	//
	// The hash first finds candidate cells, then performs an
	// actual squared-distance test before writing to output.
	// ============================================================

	public int Query(
		float posX,
		float posY,
		float radius,
		float[] positionsX,
		float[] positionsY,
		int[] output,
		int outputStart,
		int maxResults)
	{
		float radiusSquared =
			radius * radius;

		int minX =
			(int)((posX - radius) * invCellSize);

		int maxX =
			(int)((posX + radius) * invCellSize);

		int minY =
			(int)((posY - radius) * invCellSize);

		int maxY =
			(int)((posY + radius) * invCellSize);

		if (minX < 0)
			minX = 0;

		if (minY < 0)
			minY = 0;

		if (maxX >= width)
			maxX = width - 1;

		if (maxY >= height)
			maxY = height - 1;

		int count = 0;

		if (maxResults <= 0)
			return 0;

		int capacity =
			output.Length -
			outputStart;

		if (capacity <= 0)
			return 0;

		if (maxResults > capacity)
			maxResults = capacity;

		for (int y = minY; y <= maxY; y++)
		{
			int rowStart =
				y * width;

			for (int x = minX; x <= maxX; x++)
			{
				int cell =
					rowStart + x;

				int particle =
					head[cell];

				while (particle != -1)
				{
					float dx =
						posX -
						positionsX[particle];

					float dy =
						posY -
						positionsY[particle];

					float distanceSquared =
						dx * dx +
						dy * dy;

					if (distanceSquared <
						radiusSquared)
					{
						output[
							outputStart + count
						] = particle;

						count++;

						if (count >= maxResults)
							return count;
					}

					particle =
						next[particle];
				}
			}
		}

		return count;
	}

	// ============================================================
	// Convenience PBF overload
	//
	// Writes into a normal output array starting at zero.
	// ============================================================

	public int Query(
		float posX,
		float posY,
		float radius,
		float[] positionsX,
		float[] positionsY,
		int[] output)
	{
		return Query(
			posX,
			posY,
			radius,
			positionsX,
			positionsY,
			output,
			0,
			output.Length
		);
	}

	// ============================================================
	// Compatibility API
	//
	// These versions are retained for other project files.
	// ============================================================

	private int[] compatibilityResults =
		new int[32];

	private int compatibilityResultCount;

	public int Query(
		Vector2 position,
		float radius)
	{
		return Query(
			position.X,
			position.Y,
			radius
		);
	}

	public int Query(
		float posX,
		float posY,
		float radius)
	{
		compatibilityResultCount = 0;

		int minX =
			(int)((posX - radius) * invCellSize);

		int maxX =
			(int)((posX + radius) * invCellSize);

		int minY =
			(int)((posY - radius) * invCellSize);

		int maxY =
			(int)((posY + radius) * invCellSize);

		if (minX < 0)
			minX = 0;

		if (minY < 0)
			minY = 0;

		if (maxX >= width)
			maxX = width - 1;

		if (maxY >= height)
			maxY = height - 1;

		for (int y = minY; y <= maxY; y++)
		{
			int rowStart =
				y * width;

			for (int x = minX; x <= maxX; x++)
			{
				int particle =
					head[rowStart + x];

				while (particle != -1)
				{
					if (compatibilityResultCount >=
						compatibilityResults.Length)
					{
						Array.Resize(
							ref compatibilityResults,
							compatibilityResults.Length * 2
						);
					}

					compatibilityResults[
						compatibilityResultCount++
					] = particle;

					particle =
						next[particle];
				}
			}
		}

		return compatibilityResultCount;
	}

	public int GetResult(
		int index)
	{
		return compatibilityResults[index];
	}
}
