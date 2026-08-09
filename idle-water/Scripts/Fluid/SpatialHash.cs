
using Godot;
using System;

public sealed class SpatialHash
{
	// ============================================================
	// Configuration
	// ============================================================

	private const float CellSize = 12.0f;
	private const float InverseCellSize = 1.0f / CellSize;

	// PBF smoothing radius = 12
	private const float PbfRadiusSquared = 144.0f;

	// ============================================================
	// Hash table
	// ============================================================

	private const int HashCapacity = 8192;
	private const int HashMask = HashCapacity - 1;

	// ============================================================
	// World
	// ============================================================

	private const float WorldMinX = 0.0f;
	private const float WorldMinY = 0.0f;

	// ============================================================
	// Storage
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

		heads = new int[HashCapacity];
		next = new int[particleCapacity];

		Clear();
	}

	// ============================================================
	// Clear
	// ============================================================

	public void Clear()
	{
		Array.Fill(heads, -1);
	}

	// ============================================================
	// Ensure particle capacity
	// ============================================================

	private void EnsureParticleCapacity(int required)
	{
		if (next.Length >= required)
			return;

		int newCapacity = next.Length * 2;

		if (newCapacity < required)
			newCapacity = required;

		if (newCapacity < 1)
			newCapacity = 1;

		Array.Resize(ref next, newCapacity);
	}

	// ============================================================
	// Insert
	// ============================================================

	public void Insert(
		int particleIndex,
		float x,
		float y)
	{
		EnsureParticleCapacity(particleIndex + 1);

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
			HashCell(cellX, cellY);

		next[particleIndex] =
			heads[bucket];

		heads[bucket] =
			particleIndex;
	}

	// ============================================================
	// Optimized PBF query
	//
	// Standard radius = 12.
	//
	// IMPORTANT:
	// The output capacity check happens BEFORE doing another
	// distance calculation.
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
		if (maxNeighbors <= 0)
			return 0;

		int centerCellX =
			FastFloorToInt(
				px * InverseCellSize
			);

		int centerCellY =
			FastFloorToInt(
				py * InverseCellSize
			);

		int count = 0;

		// --------------------------------------------------------
		// Inline the 3x3 traversal.
		//
		// This avoids temporary values and keeps the hot loop
		// as small as possible.
		// --------------------------------------------------------

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
					// ------------------------------------------------
					// Stop immediately once the output is full.
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
						PbfRadiusSquared)
					{
						output[
							outputOffset + count
						] = particle;

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
	// Kept for compatibility with other code.
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
			radius * radius;

		int count = 0;

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
					// ------------------------------------------------
					// Check capacity BEFORE distance calculation.
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
							outputOffset + count
						] = particle;

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
