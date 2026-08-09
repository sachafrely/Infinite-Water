
using Godot;
using System;

public class SpatialHash
{
	// ============================================================
	// Configuration
	// ============================================================

	private readonly float cellSize;
	private readonly float invCellSize;

	private readonly int width;
	private readonly int height;

	// ============================================================
	// Hash storage
	// ============================================================

	private readonly int[] head;
	private readonly int[] next;

	// ============================================================
	// Compatibility API
	// ============================================================

	private int[] compatibilityResults =
		new int[32];

	private int compatibilityResultCount;

	// ============================================================
	// Constructor
	// ============================================================

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

		head =
			new int[width * height];

		next =
			new int[maxParticles];

		Clear();
	}

	// ============================================================
	// Clear
	// ============================================================

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
		int cellX =
			(int)(posX * invCellSize);

		int cellY =
			(int)(posY * invCellSize);

		if (
			cellX < 0 ||
			cellY < 0 ||
			cellX >= width ||
			cellY >= height)
		{
			next[id] = -1;
			return;
		}

		int cellIndex =
			cellY * width + cellX;

		next[id] =
			head[cellIndex];

		head[cellIndex] =
			id;
	}

	// ============================================================
	// Optimized PBF Query
	//
	// Hot path used by PbfSolver.
	//
	// This version is optimized for the current PBF setup:
	//
	//     smoothing radius = cell size
	//
	// Therefore a particle can only have neighbors in its
	// own cell plus the surrounding 8 cells.
	//
	// We still perform the exact squared-distance test, so
	// the returned neighbor set is unchanged.
	// ============================================================

	public int QueryPbf(
		float posX,
		float posY,
		float radius,
		float[] positionsX,
		float[] positionsY,
		int[] output,
		int outputStart,
		int maxResults)
	{
		if (maxResults <= 0)
			return 0;

		int capacity =
			output.Length -
			outputStart;

		if (capacity <= 0)
			return 0;

		if (maxResults > capacity)
			maxResults = capacity;

		// --------------------------------------------------------
		// Radius squared
		// --------------------------------------------------------

		float radiusSquared =
			radius * radius;

		// --------------------------------------------------------
		// Particle's hash cell.
		//
		// We deliberately calculate the center cell only once.
		// --------------------------------------------------------

		int centerX =
			(int)(posX * invCellSize);

		int centerY =
			(int)(posY * invCellSize);

		// --------------------------------------------------------
		// If the particle is outside the hash, there can be
		// no valid neighbors.
		// --------------------------------------------------------

		if (
			centerX < 0 ||
			centerY < 0 ||
			centerX >= width ||
			centerY >= height)
		{
			return 0;
		}

		// --------------------------------------------------------
		// Calculate query cell range.
		//
		// We keep this generic enough that radius may be slightly
		// different from cellSize.
		//
		// For the current setup this normally becomes 3x3.
		// --------------------------------------------------------

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

		if (
			minX > maxX ||
			minY > maxY)
		{
			return 0;
		}

		// --------------------------------------------------------
		// Local references.
		//
		// These reduce repeated field accesses inside the hot
		// loops.
		// --------------------------------------------------------

		int[] localHead =
			head;

		int[] localNext =
			next;

		float[] localPosX =
			positionsX;

		float[] localPosY =
			positionsY;

		// --------------------------------------------------------
		// Search.
		// --------------------------------------------------------

		int count = 0;

		int writeIndex =
			outputStart;

		for (
			int cellY = minY;
			cellY <= maxY;
			cellY++)
		{
			int cellIndex =
				cellY * width +
				minX;

			for (
				int cellX = minX;
				cellX <= maxX;
				cellX++,
				cellIndex++)
			{
				int particle =
					localHead[cellIndex];

				while (particle != -1)
				{
					float dx =
						posX -
						localPosX[particle];

					float dy =
						posY -
						localPosY[particle];

					float distanceSquared =
						dx * dx +
						dy * dy;

					if (
						distanceSquared <
						radiusSquared)
					{
						output[writeIndex++] =
							particle;

						count++;

						if (count >= maxResults)
							return count;
					}

					particle =
						localNext[particle];
				}
			}
		}

		return count;
	}

	// ============================================================
	// Standard PBF Query
	//
	// Kept for compatibility with existing code.
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
		if (maxResults <= 0)
			return 0;

		int capacity =
			output.Length -
			outputStart;

		if (capacity <= 0)
			return 0;

		if (maxResults > capacity)
			maxResults = capacity;

		float radiusSquared =
			radius * radius;

		int minCellX =
			(int)((posX - radius) * invCellSize);

		int maxCellX =
			(int)((posX + radius) * invCellSize);

		int minCellY =
			(int)((posY - radius) * invCellSize);

		int maxCellY =
			(int)((posY + radius) * invCellSize);

		if (minCellX < 0)
			minCellX = 0;

		if (minCellY < 0)
			minCellY = 0;

		if (maxCellX >= width)
			maxCellX = width - 1;

		if (maxCellY >= height)
			maxCellY = height - 1;

		if (
			minCellX > maxCellX ||
			minCellY > maxCellY)
		{
			return 0;
		}

		int count = 0;

		int writeIndex =
			outputStart;

		for (
			int cellY = minCellY;
			cellY <= maxCellY;
			cellY++)
		{
			int cellIndex =
				cellY * width +
				minCellX;

			for (
				int cellX = minCellX;
				cellX <= maxCellX;
				cellX++,
				cellIndex++)
			{
				int particle =
					head[cellIndex];

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

					if (
						distanceSquared <
						radiusSquared)
					{
						output[writeIndex++] =
							particle;

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
	// Convenience PBF Query
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
	// Compatibility Query
	//
	// Older project code can continue using this API.
	//
	// It intentionally returns all particles in candidate cells
	// without performing the actual distance test.
	// ============================================================

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
		compatibilityResultCount =
			0;

		int minCellX =
			(int)((posX - radius) * invCellSize);

		int maxCellX =
			(int)((posX + radius) * invCellSize);

		int minCellY =
			(int)((posY - radius) * invCellSize);

		int maxCellY =
			(int)((posY + radius) * invCellSize);

		if (minCellX < 0)
			minCellX = 0;

		if (minCellY < 0)
			minCellY = 0;

		if (maxCellX >= width)
			maxCellX = width - 1;

		if (maxCellY >= height)
			maxCellY = height - 1;

		if (
			minCellX > maxCellX ||
			minCellY > maxCellY)
		{
			return 0;
		}

		for (
			int cellY = minCellY;
			cellY <= maxCellY;
			cellY++)
		{
			int cellIndex =
				cellY * width +
				minCellX;

			for (
				int cellX = minCellX;
				cellX <= maxCellX;
				cellX++,
				cellIndex++)
			{
				int particle =
					head[cellIndex];

				while (particle != -1)
				{
					if (
						compatibilityResultCount >=
						compatibilityResults.Length)
					{
						int newSize =
							compatibilityResults.Length * 2;

						if (newSize < 1)
							newSize = 1;

						Array.Resize(
							ref compatibilityResults,
							newSize
						);
					}

					compatibilityResults[
						compatibilityResultCount++
					] =
						particle;

					particle =
						next[particle];
				}
			}
		}

		return compatibilityResultCount;
	}

	// ============================================================
	// Compatibility result access
	// ============================================================

	public int GetResult(
		int index)
	{
		return compatibilityResults[index];
	}
}
