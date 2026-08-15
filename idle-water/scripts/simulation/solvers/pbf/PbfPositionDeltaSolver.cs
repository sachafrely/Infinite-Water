using Godot;
using System;
using System.Runtime.CompilerServices;

/// <summary>
/// PbfPositionDeltaSolver — accumulates position-correction deltas and
/// applies the pixel-overlap correction for one PBF iteration.
///
/// Two entry points:
/// <list type="bullet">
///   <item><see cref="ApplyCorrections"/> — PBF density-based Δ position.</item>
///   <item><see cref="ApplyPixelOccupancyCorrection"/> — prevents ≥ 3
///     particles occupying the same screen pixel.</item>
/// </list>
/// </summary>
internal static class PbfPositionDeltaSolver
{
	// ============================================================
	// Position corrections
	// ============================================================

	/// <summary>
	/// Applies the PBF position-correction step: for each particle sums the
	/// (λ_i + λ_j) × ∇W contributions from all neighbors and adds the
	/// result to its predicted position.
	/// </summary>
	public static void ApplyCorrections(
		float[] predX,
		float[] predY,
		int count,
		PbfState state)
	{
		float[] localLambdas =
			state.Lambdas;

		int[] localNeighborBuffer =
			state.NeighborBuffer;

		int[] localNeighborCounts =
			state.NeighborCounts;

		float[] localGradientScale =
			state.NeighborGradientScale;

		float[] localDx =
			state.NeighborDx;

		float[] localDy =
			state.NeighborDy;

		int stride =
			state.NeighborStride;

		float maxCorrection =
			PbfSolver.MaxCorrection;

		float maxCorrectionSquared =
			PbfSolver.MaxCorrectionSquared;

		for (
			int i = 0;
			i < count;
			i++)
		{
			float correctionX = 0.0f;
			float correctionY = 0.0f;

			int start =
				i * stride;

			int end =
				start +
				localNeighborCounts[i];

			float lambdaI =
				localLambdas[i];

			for (
				int index = start;
				index < end;
				index++)
			{
				int j =
					localNeighborBuffer[index];

				float scale =
					(lambdaI +
					localLambdas[j]) *
					localGradientScale[index];

				correctionX +=
					scale *
					localDx[index];

				correctionY +=
					scale *
					localDy[index];
			}

			float lengthSquared =
				correctionX * correctionX +
				correctionY * correctionY;

			if (
				lengthSquared >
				maxCorrectionSquared)
			{
				float inverseLength =
					1.0f /
					MathF.Sqrt(
						lengthSquared
					);

				float scale =
					maxCorrection *
					inverseLength;

				correctionX *=
					scale;

				correctionY *=
					scale;
			}

			predX[i] +=
				correctionX;

			predY[i] +=
				correctionY;
		}
	}

	// ============================================================
	// Pixel occupancy correction
	// ============================================================

	/// <summary>
	/// Prevents more than <c>MaxParticlesPerPixel</c> particles from
	/// occupying the same screen pixel by nudging extras apart.
	/// Uses the generation-stamped open-addressed hash table in
	/// <see cref="PbfState"/>.
	/// </summary>
	public static void ApplyPixelOccupancyCorrection(
		float[] predX,
		float[] predY,
		int count,
		PbfState state)
	{
		if (count <= 0)
			return;

		int generation =
			++state.PixelOccupancyGeneration;

		// Extremely unlikely, but safely reset generation state
		// rather than allowing a wrapped generation to match old entries.
		if (generation == int.MaxValue)
		{
			Array.Clear(
				state.PixelOccupancyStamp,
				0,
				state.PixelOccupancyStamp.Length
			);

			generation = 1;
			state.PixelOccupancyGeneration = generation;
		}

		int[] stamps =
			state.PixelOccupancyStamp;

		int[] occupancy =
			state.PixelOccupancyCount;

		int[] pixelX =
			state.PixelOccupancyX;

		int[] pixelY =
			state.PixelOccupancyY;

		int[] firstParticle =
			state.PixelOccupancyFirstParticle;

		int[] secondParticle =
			state.PixelOccupancySecondParticle;

		int maxParticles =
			PbfSolver.MaxParticlesPerPixel;

		float separation =
			PbfSolver.ExactOverlapSeparation;

		for (
			int i = 0;
			i < count;
			i++)
		{
			int px =
				(int)MathF.Floor(predX[i]);

			int py =
				(int)MathF.Floor(predY[i]);

			int slot =
				FindPixelOccupancySlot(
					px,
					py,
					generation,
					state
				);

			if (stamps[slot] != generation)
			{
				stamps[slot] = generation;
				pixelX[slot] = px;
				pixelY[slot] = py;
				occupancy[slot] = 1;
				firstParticle[slot] = i;
				secondParticle[slot] = -1;
				continue;
			}

			int currentOccupancy =
				occupancy[slot];

			if (currentOccupancy < maxParticles)
			{
				occupancy[slot] =
					currentOccupancy + 1;

				if (currentOccupancy == 1)
					secondParticle[slot] = i;

				continue;
			}

			// Pixel already has maxParticles.
			// Only check the two already-stored particles.
			int first =
				firstParticle[slot];

			int second =
				secondParticle[slot];

			if (
				first >= 0 &&
				IsExactPixelOverlap(
					i,
					first,
					predX,
					predY
				))
			{
				Vector2 dir =
					GetDeterministicSeparationDirection(
						i,
						first
					);

				predX[i] +=
					dir.X * separation;

				predY[i] +=
					dir.Y * separation;

				continue;
			}

			if (
				second >= 0 &&
				IsExactPixelOverlap(
					i,
					second,
					predX,
					predY
				))
			{
				Vector2 dir =
					GetDeterministicSeparationDirection(
						i,
						second
					);

				predX[i] +=
					dir.X * separation;

				predY[i] +=
					dir.Y * separation;
			}
		}
	}

	// ============================================================
	// Pixel occupancy slot lookup
	// ============================================================

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int FindPixelOccupancySlot(
		int x,
		int y,
		int generation,
		PbfState state)
	{
		int hashValue =
			HashPixelCoordinates(x, y);

		int slot =
			hashValue &
			PbfSolver.PixelOccupancyTableMask;

		int[] stamps =
			state.PixelOccupancyStamp;

		int[] xs =
			state.PixelOccupancyX;

		int[] ys =
			state.PixelOccupancyY;

		while (true)
		{
			if (stamps[slot] != generation)
				return slot;

			if (xs[slot] == x && ys[slot] == y)
				return slot;

			slot =
				(slot + 1) &
				PbfSolver.PixelOccupancyTableMask;
		}
	}

	// ============================================================
	// Pixel coordinate hash
	// ============================================================

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int HashPixelCoordinates(
		int x,
		int y)
	{
		unchecked
		{
			uint h = (uint)x * 0x9E3779B1u;
			h ^= (uint)y * 0x85EBCA77u;
			h ^= h >> 16;
			h *= 0xC2B2AE3Du;
			h ^= h >> 13;
			return (int)h;
		}
	}

	// ============================================================
	// Exact overlap test
	// ============================================================

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool IsExactPixelOverlap(
		int a,
		int b,
		float[] predX,
		float[] predY)
	{
		float dx = predX[a] - predX[b];
		float dy = predY[a] - predY[b];

		return
			dx * dx + dy * dy <=
			PbfSolver.ExactOverlapDistanceSquared;
	}

	// ============================================================
	// Deterministic separation direction
	// ============================================================

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static Vector2
		GetDeterministicSeparationDirection(
			int a,
			int b)
	{
		int value = a ^ (b * 31);
		int dir = Math.Abs(value % 4);

		switch (dir)
		{
			case 0:  return new Vector2(1.0f, 0.0f);
			case 1:  return new Vector2(-1.0f, 0.0f);
			case 2:  return new Vector2(0.0f, 1.0f);
			default: return new Vector2(0.0f, -1.0f);
		}
	}
}
