using Godot;
using System;
using System.Runtime.CompilerServices;

public sealed class SpatialHash
{
	// ============================================================
	// Configuration
	// ============================================================

	// PBF smoothing radius is 8px.
	//
	// Cell size matches the smoothing radius, meaning a particle
	// only needs to inspect the surrounding 3x3 cells.
	private const float CellSize = 8.0f;
	private const float InverseCellSize = 1.0f / CellSize;

	private const float PbfRadiusSquared = 64.0f;

	// ============================================================
	// World
	// ============================================================

	private const float WorldMinX = 0.0f;
	private const float WorldMinY = -200.0f;

	private const float WorldMaxX = 1200.0f;
	private const float WorldMaxY = 840.0f;

	// ============================================================
	// Direct grid
	//
	// Instead of hashing cells into 8192 buckets, every spatial
	// cell gets its own head entry.
	//
	// This completely removes hash collisions.
	// ============================================================

	private readonly int gridWidth;
	private readonly int gridHeight;
	private readonly int gridCellCount;

	private readonly int[] heads;

	private int[] next;

	// ============================================================
	// Constructor
	// ============================================================

	public SpatialHash(
		int particleCapacity)
	{
		if (
			particleCapacity < 1)
		{
			particleCapacity = 1;
		}

		gridWidth =
			(int)MathF.Ceiling(
				(WorldMaxX - WorldMinX) *
				InverseCellSize
			);

		gridHeight =
			(int)MathF.Ceiling(
				(WorldMaxY - WorldMinY) *
				InverseCellSize
			);

		gridCellCount =
			gridWidth *
			gridHeight;

		heads =
			new int[
				gridCellCount
			];

		next =
			new int[
				particleCapacity
			];

		Clear();
	}

	// ============================================================
	// Clear
	// ============================================================

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Clear()
	{
		Array.Fill(
			heads,
			-1
		);
	}

	// ============================================================
	// Ensure particle capacity
	// ============================================================

	private void EnsureParticleCapacity(
		int required)
	{
		if (
			next.Length >=
			required)
		{
			return;
		}

		int newCapacity =
			next.Length * 2;

		if (
			newCapacity < required)
		{
			newCapacity =
				required;
		}

		if (
			newCapacity < 1)
		{
			newCapacity = 1;
		}

		Array.Resize(
			ref next,
			newCapacity
		);
	}

	// ============================================================
	// Insert
	// ============================================================

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Insert(
		int particleIndex,
		float x,
		float y)
	{
		EnsureParticleCapacity(
			particleIndex + 1
		);

		int cellX =
			FastFloorToInt(
				(x - WorldMinX) *
				InverseCellSize
			);

		int cellY =
			FastFloorToInt(
				(y - WorldMinY) *
				InverseCellSize
			);

		// Clamp to valid grid.
		//
		// This is cheap and prevents particles temporarily outside
		// the simulation buffer from producing invalid indices.

		if (cellX < 0)
			cellX = 0;
		else if (cellX >= gridWidth)
			cellX = gridWidth - 1;

		if (cellY < 0)
			cellY = 0;
		else if (cellY >= gridHeight)
			cellY = gridHeight - 1;

		int cellIndex =
			cellY *
			gridWidth +
			cellX;

		next[particleIndex] =
			heads[cellIndex];

		heads[cellIndex] =
			particleIndex;
	}

	// ============================================================
	// Optimized PBF query
	//
	// Cell size = smoothing radius = 8px.
	//
	// Therefore only the 3x3 surrounding cells can contain
	// particles within an 8px radius.
	// ============================================================

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int QueryPbf(
		float px,
		float py,
		float[] positionsX,
		float[] positionsY,
		int[] output,
		int outputOffset,
		int maxNeighbors)
	{
		if (
			maxNeighbors <= 0)
		{
			return 0;
		}

		int centerCellX =
			FastFloorToInt(
				(px - WorldMinX) *
				InverseCellSize
			);

		int centerCellY =
			FastFloorToInt(
				(py - WorldMinY) *
				InverseCellSize
			);

		int count = 0;

		int minCellX =
			centerCellX - 1;

		int maxCellX =
			centerCellX + 1;

		int minCellY =
			centerCellY - 1;

		int maxCellY =
			centerCellY + 1;

		// Clamp query range.

		if (minCellX < 0)
			minCellX = 0;

		if (maxCellX >= gridWidth)
			maxCellX = gridWidth - 1;

		if (minCellY < 0)
			minCellY = 0;

		if (maxCellY >= gridHeight)
			maxCellY = gridHeight - 1;

		// --------------------------------------------------------
		// 3x3 cells
		// --------------------------------------------------------

		for (
			int cellY = minCellY;
			cellY <= maxCellY;
			cellY++)
		{
			int rowStart =
				cellY *
				gridWidth;

			for (
				int cellX = minCellX;
				cellX <= maxCellX;
				cellX++)
			{
				int particle =
					heads[
						rowStart +
						cellX
					];

				while (
					particle != -1)
				{
					float dx =
						px -
						positionsX[particle];

					float dy =
						py -
						positionsY[particle];

					float distanceSquared =
						dx * dx +
						dy * dy;

					if (
						distanceSquared <=
						PbfRadiusSquared)
					{
						if (
							count >=
							maxNeighbors)
						{
							return count;
						}

						output[
							outputOffset +
							count
						] =
							particle;

						count++;
					}

					particle =
						next[particle];
				}
			}
		}

		return count;
	}

	// ============================================================
	// Optimized PBF query with geometry output
	//
	// This is the HOT PATH used by PbfSolver.
	// ============================================================

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int QueryPbfWithGeometry(
		float px,
		float py,
		float[] positionsX,
		float[] positionsY,
		int[] output,
		float[] outputDx,
		float[] outputDy,
		float[] outputQ,
		float[] outputGradientScale,
		int outputOffset,
		int maxNeighbors)
	{
		if (
			maxNeighbors <= 0)
		{
			return 0;
		}

		int centerCellX =
			FastFloorToInt(
				(px - WorldMinX) *
				InverseCellSize
			);

		int centerCellY =
			FastFloorToInt(
				(py - WorldMinY) *
				InverseCellSize
			);

		int count = 0;

		int minCellX =
			centerCellX - 1;

		int maxCellX =
			centerCellX + 1;

		int minCellY =
			centerCellY - 1;

		int maxCellY =
			centerCellY + 1;

		if (minCellX < 0)
			minCellX = 0;

		if (maxCellX >= gridWidth)
			maxCellX = gridWidth - 1;

		if (minCellY < 0)
			minCellY = 0;

		if (maxCellY >= gridHeight)
			maxCellY = gridHeight - 1;

		// Constants pulled into locals for the hot loop.

		const float inverseSmoothingRadius =
			1.0f / 8.0f;

		const float inverseRestDensity =
			1.0f / 1.15f;

		const float epsilon =
			0.000001f;

		// --------------------------------------------------------
		// 3x3 cells
		// --------------------------------------------------------

		for (
			int cellY = minCellY;
			cellY <= maxCellY;
			cellY++)
		{
			int rowStart =
				cellY *
				gridWidth;

			for (
				int cellX = minCellX;
				cellX <= maxCellX;
				cellX++)
			{
				int particle =
					heads[
						rowStart +
						cellX
					];

				while (
					particle != -1)
				{
					float dx =
						px -
						positionsX[particle];

					float dy =
						py -
						positionsY[particle];

					float distanceSquared =
						dx * dx +
						dy * dy;

					if (
						distanceSquared <=
						PbfRadiusSquared)
					{
						if (
							count >=
							maxNeighbors)
						{
							return count;
						}

						int index =
							outputOffset +
							count;

						output[index] =
							particle;

						outputDx[index] =
							dx;

						outputDy[index] =
							dy;

						if (
							distanceSquared <=
							epsilon)
						{
							outputQ[index] =
								1.0f;

							outputGradientScale[index] =
								0.0f;
						}
						else
						{
							float inverseDistance =
								1.0f /
								MathF.Sqrt(
									distanceSquared
								);

							float distance =
								distanceSquared *
								inverseDistance;

							float q =
								1.0f -
								distance *
								inverseSmoothingRadius;

							float q2 =
								q *
								q;

							outputQ[index] =
								q;

							outputGradientScale[index] =
								-3.0f *
								q2 *
								inverseSmoothingRadius *
								inverseDistance *
								inverseRestDensity;
						}

						count++;
					}

					particle =
						next[particle];
				}
			}
		}

		return count;
	}

	// ============================================================
	// Generic radius query
	//
	// Kept for compatibility.
	//
	// The current PBF solver normally uses the specialized
	// QueryPbfWithGeometry() above.
	// ============================================================

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int QueryPbf(
		float px,
		float py,
		float radius,
		float[] positionsX,
		float[] positionsY,
		int[] output,
		int outputOffset,
		int maxNeighbors)
	{
		if (
			maxNeighbors <= 0)
		{
			return 0;
		}

		int centerCellX =
			FastFloorToInt(
				(px - WorldMinX) *
				InverseCellSize
			);

		int centerCellY =
			FastFloorToInt(
				(py - WorldMinY) *
				InverseCellSize
			);

		float radiusSquared =
			radius *
			radius;

		// Determine how many cells are actually required.

		int cellRadius =
			(int)MathF.Ceiling(
				radius *
				InverseCellSize
			);

		if (cellRadius < 1)
			cellRadius = 1;

		int minCellX =
			centerCellX -
			cellRadius;

		int maxCellX =
			centerCellX +
			cellRadius;

		int minCellY =
			centerCellY -
			cellRadius;

		int maxCellY =
			centerCellY +
			cellRadius;

		if (minCellX < 0)
			minCellX = 0;

		if (maxCellX >= gridWidth)
			maxCellX = gridWidth - 1;

		if (minCellY < 0)
			minCellY = 0;

		if (maxCellY >= gridHeight)
			maxCellY = gridHeight - 1;

		int count = 0;

		for (
			int cellY = minCellY;
			cellY <= maxCellY;
			cellY++)
		{
			int rowStart =
				cellY *
				gridWidth;

			for (
				int cellX = minCellX;
				cellX <= maxCellX;
				cellX++)
			{
				int particle =
					heads[
						rowStart +
						cellX
					];

				while (
					particle != -1)
				{
					float dx =
						px -
						positionsX[particle];

					float dy =
						py -
						positionsY[particle];

					float distanceSquared =
						dx * dx +
						dy * dy;

					if (
						distanceSquared <=
						radiusSquared)
					{
						if (
							count >=
							maxNeighbors)
						{
							return count;
						}

						output[
							outputOffset +
							count
						] =
							particle;

						count++;
					}

					particle =
						next[particle];
				}
			}
		}

		return count;
	}

	// ============================================================
	// Fast floor
	// ============================================================

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int FastFloorToInt(
		float value)
	{
		int integer =
			(int)value;

		if (
			value <
			integer)
		{
			return integer - 1;
		}

		return integer;
	}
}
