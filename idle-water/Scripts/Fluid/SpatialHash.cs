
using Godot;
using System;

public sealed class SpatialHash
{
	// ============================================================
	// Configuration
	// ============================================================

	// IMPORTANT:
	// PbfSolver uses an 8px smoothing radius.
	//
	// The previous hash used 12px, which meant the hash was
	// searching a larger neighborhood than the PBF solver could
	// actually use.
	//
	// Matching the cell size to the PBF radius keeps the 3x3
	// lookup tight.
	private const float CellSize = 8.0f;
	private const float InverseCellSize = 1.0f / CellSize;

	private const float PbfRadiusSquared = 64.0f;

	// ============================================================
	// Hash table
	// ============================================================

	private const int HashCapacity = 8192;
	private const int HashMask =
		HashCapacity - 1;

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

	public SpatialHash(
		int particleCapacity)
	{
		if (
			particleCapacity < 1)
		{
			particleCapacity = 1;
		}

		heads =
			new int[
				HashCapacity
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
			newCapacity <
			required)
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
	// Optimized PBF query
	//
	// PBF radius = 8.
	//
	// With an 8px cell size, a 3x3 neighborhood is sufficient
	// to cover the complete smoothing radius.
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

		// --------------------------------------------------------
		// 3x3 neighborhood
		// --------------------------------------------------------

		for (
			int cellY =
				centerCellY - 1;

			cellY <=
				centerCellY + 1;

			cellY++)
		{
			for (
				int cellX =
					centerCellX - 1;

				cellX <=
					centerCellX + 1;

				cellX++)
			{
				int bucket =
					HashCell(
						cellX,
						cellY
					);

				int particle =
					heads[bucket];

				while (
					particle != -1)
				{
					// ------------------------------------------------
					// Stop immediately once the neighbor buffer is
					// full.
					// ------------------------------------------------

					if (
						count >=
						maxNeighbors)
					{
						return count;
					}

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
	// ============================================================
	public int QueryPbfWithGeometry(
		float px, float py,
		float[] positionsX, float[] positionsY,
		int[] output, float[] outputDx, float[] outputDy,
		float[] outputQ, float[] outputQSquared,
		float[] outputGradientScale,
		int outputOffset, int maxNeighbors)
	{
		if (maxNeighbors <= 0) return 0;

		int centerCellX = FastFloorToInt((px - WorldMinX) * InverseCellSize);
		int centerCellY = FastFloorToInt((py - WorldMinY) * InverseCellSize);
		int count = 0;

		for (int cellY = centerCellY - 1; cellY <= centerCellY + 1; cellY++)
		{
			for (int cellX = centerCellX - 1; cellX <= centerCellX + 1; cellX++)
			{
				int particle = heads[HashCell(cellX, cellY)];
				while (particle != -1)
				{
					if (count >= maxNeighbors) return count;

					float dx = px - positionsX[particle];
					float dy = py - positionsY[particle];
					float distanceSquared = dx * dx + dy * dy;

					if (distanceSquared <= PbfRadiusSquared)
					{
						int index = outputOffset + count;
						output[index] = particle;
						outputDx[index] = dx;
						outputDy[index] = dy;

						if (distanceSquared <= 0.000001f)
						{
							outputQ[index] = 1.0f;
							outputQSquared[index] = 1.0f;
							outputGradientScale[index] = 0.0f;
						}
						else
						{
							float inverseDistance = 1.0f / MathF.Sqrt(distanceSquared);
							float q = 1.0f - (distanceSquared * inverseDistance) * (1.0f / 8.0f);
							if (q > 0.0f)
							{
								float q2 = q * q;
								outputQ[index] = q;
								outputQSquared[index] = q2;
								outputGradientScale[index] = -3.0f * q2 * (1.0f / 8.0f) * inverseDistance * (1.0f / 1.15f);
							}
							else
							{
								outputQ[index] = 0.0f;
								outputQSquared[index] = 0.0f;
								outputGradientScale[index] = 0.0f;
							}
						}
						count++;
					}

					particle = next[particle];
				}
			}
		}
		return count;
	}

	// ============================================================
	// Generic radius query
	//
	// Kept for compatibility with existing code.
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
			radius * radius;

		int count = 0;

		for (
			int cellY =
				centerCellY - 1;

			cellY <=
				centerCellY + 1;

			cellY++)
		{
			for (
				int cellX =
					centerCellX - 1;

				cellX <=
					centerCellX + 1;

				cellX++)
			{
				int bucket =
					HashCell(
						cellX,
						cellY
					);

				int particle =
					heads[bucket];

				while (
					particle != -1)
				{
					if (
						count >=
						maxNeighbors)
					{
						return count;
					}

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

			return
				(int)(
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

		if (
			value <
			integer)
		{
			return integer - 1;
		}

		return integer;
	}
}
