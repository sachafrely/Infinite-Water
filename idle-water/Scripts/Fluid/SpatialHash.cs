using Godot;
using System;

public sealed class SpatialHash
{
// ============================================================
// Configuration
// ============================================================


// Must be >= PBF smoothing radius.
//
// Your PBF uses:
//     SmoothingRadius = 12
//
// Therefore 12 is the ideal cell size because a query only
// needs the 3x3 cells surrounding the particle.
// ============================================================

private const float CellSize = 12.0f;
private const float InverseCellSize = 1.0f / CellSize;

private const int MaxNeighborsPerQuery = 40;

// ============================================================
// Hash table
// ============================================================

// Power-of-two table allows:
//
//     hash & (capacity - 1)
//
// instead of modulo.
// ============================================================

private const int HashCapacity = 8192;
private const int HashMask = HashCapacity - 1;

// ============================================================
// World
// ============================================================

private const float WorldMinX = 0.0f;
private const float WorldMinY = 0.0f;

// ============================================================
// Bucket storage
//
// Each hash bucket is a linked list.
//
// head[bucket] -> particle index
//
// next[particle] -> next particle in same bucket
// ============================================================

private readonly int[] heads;
private int[] next;

// ============================================================
// Constructor
// ============================================================

public SpatialHash(int particleCapacity)
{
	if (particleCapacity < 1)
		particleCapacity = 1;

	heads =
		new int[HashCapacity];

	next =
		new int[particleCapacity];

	Clear();
}

// ============================================================
// Clear
// ============================================================

public void Clear()
{
	// -1 means bucket is empty.
	Array.Fill(heads, -1);
}

// ============================================================
// Ensure particle capacity
// ============================================================

private void EnsureParticleCapacity(int required)
{
	if (next.Length >= required)
		return;

	int newCapacity =
		next.Length * 2;

	if (newCapacity < required)
		newCapacity = required;

	if (newCapacity < 1)
		newCapacity = 1;

	Array.Resize(
		ref next,
		newCapacity
	);
}

// ============================================================
// Insert
// ============================================================

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

	int bucket =
		HashCell(
			cellX,
			cellY
		);

	next[particleIndex] =
		heads[bucket];

	heads[bucket] =
		particleIndex;
}

// ============================================================
// PBF query
//
// Finds particles within radius.
//
// IMPORTANT:
//
// This method intentionally does NOT allocate.
//
// The caller provides the output array and write offset.
// ============================================================

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
	if (maxNeighbors <= 0)
		return 0;

	// --------------------------------------------------------
	// Calculate center cell
	// --------------------------------------------------------

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

	// --------------------------------------------------------
	// Because the cell size is equal to the smoothing radius,
	// we only need:
	//
	//     X = -1, 0, +1
	//     Y = -1, 0, +1
	//
	// --------------------------------------------------------

	int minCellX =
		centerCellX - 1;

	int maxCellX =
		centerCellX + 1;

	int minCellY =
		centerCellY - 1;

	int maxCellY =
		centerCellY + 1;

	float radiusSquared =
		radius * radius;

	int count = 0;

	// ========================================================
	// 3x3 cell traversal
	// ========================================================

	for (
		int cellY = minCellY;
		cellY <= maxCellY;
		cellY++)
	{
		for (
			int cellX = minCellX;
			cellX <= maxCellX;
			cellX++)
		{
			int bucket =
				HashCell(
					cellX,
					cellY
				);

			int particle =
				heads[bucket];

			while (particle != -1)
			{
				// ------------------------------------------------
				// Don't calculate the distance if the output
				// buffer is already full.
				// ------------------------------------------------

				if (count >= maxNeighbors)
					return count;

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
// Optimized PBF query
//
// This overload avoids recalculating radiusSquared when the
// caller already uses the standard PBF smoothing radius.
//
// Kept separate so the existing PbfSolver remains compatible.
// ============================================================

public int QueryPbf(
	float px,
	float py,
	float[] positionsX,
	float[] positionsY,
	int[] output,
	int outputOffset,
	int maxNeighbors)
{
	const float radiusSquared =
		12.0f * 12.0f;

	int centerCellX =
		FastFloorToInt(
			px *
			InverseCellSize
		);

	int centerCellY =
		FastFloorToInt(
			py *
			InverseCellSize
		);

	int count = 0;

	// ========================================================
	// 3x3 cells
	// ========================================================

	for (
		int cellY = centerCellY - 1;
		cellY <= centerCellY + 1;
		cellY++)
	{
		for (
			int cellX = centerCellX - 1;
			cellX <= centerCellX + 1;
			cellX++)
		{
			int bucket =
				HashCell(
					cellX,
					cellY
				);

			int particle =
				heads[bucket];

			while (particle != -1)
			{
				if (count >= maxNeighbors)
					return count;

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
// Hash
// ============================================================

private static int HashCell(
	int cellX,
	int cellY)
{
	unchecked
	{
		// Two large odd constants.
		//
		// The final bit mask is extremely cheap and produces
		// good distribution for the relatively small fluid
		// world used by this simulation.

		uint hash =
			(uint)(
				cellX *
				73856093
			);

		hash ^=
			(uint)(
				cellY *
				19349663
			);

		hash ^=
			hash >> 13;

		hash *=
			0x5bd1e995u;

		hash ^=
			hash >> 15;

		return (int)(
			hash &
			HashMask
		);
	}
}

// ============================================================
// Fast floor
//
// Coordinates can be negative, so this intentionally handles
// negative values correctly.
// ============================================================

private static int FastFloorToInt(
	float value)
{
	int integer =
		(int)value;

	if (value < integer)
		return integer - 1;

	return integer;
}


}
