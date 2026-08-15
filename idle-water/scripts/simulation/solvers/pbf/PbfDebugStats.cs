using Godot;
using System;

/// <summary>
/// PbfDebugStats — optional debug output for the PBF solver pipeline.
///
/// Contains two entry points:
/// <list type="bullet">
///   <item><see cref="PrintProfiler"/> — prints per-pass timing to the
///     Godot output panel every <c>ProfilerIntervalFrames</c> ticks.</item>
///   <item><see cref="CalculatePackingStats"/> — logs nearest-neighbour
///     distance statistics to diagnose particle packing quality.</item>
/// </list>
/// Neither method affects simulation state.
/// </summary>
internal static class PbfDebugStats
{
	// ============================================================
	// Profiler output
	// ============================================================

	/// <summary>
	/// Prints per-pass timing to the Godot output panel.
	/// </summary>
	public static void PrintProfiler(
		double predictMs,
		double spatialHashMs,
		double neighborSearchMs,
		double neighborGeometryMs,
		double neighborCacheMs,
		double pbfMs,
		double collisionMs,
		double terrainQueryMs,
		double terrainResolveMs,
		double wheelCollisionMs,
		double boundsMs,
		double velocityMs,
		double totalMs,
		int particleCount)
	{
		GD.Print("========== PBF PROFILER ==========");
		GD.Print($"Particles: {particleCount}");
		GD.Print($"Predict: {predictMs:F3} ms");
		GD.Print($"SpatialHash: {spatialHashMs:F3} ms");
		GD.Print($"NeighborSearch: {neighborSearchMs:F3} ms");
		GD.Print($"NeighborGeometry: {neighborGeometryMs:F3} ms");
		GD.Print($"NeighborCache: {neighborCacheMs:F3} ms");
		GD.Print($"PBF: {pbfMs:F3} ms");
		GD.Print($"Collision: {collisionMs:F3} ms");
		GD.Print($"TerrainQuery: {terrainQueryMs:F3} ms");
		GD.Print($"TerrainResolve: {terrainResolveMs:F3} ms");
		GD.Print($"Wheel: {wheelCollisionMs:F3} ms");
		GD.Print($"Bounds: {boundsMs:F3} ms");
		GD.Print($"Velocity: {velocityMs:F3} ms");
		GD.Print($"TOTAL: {totalMs:F3} ms");
		GD.Print("==================================");
	}

	// ============================================================
	// Particle packing stats
	// ============================================================

	/// <summary>
	/// Logs nearest-neighbour distance statistics for all particles.
	/// Requires a valid <see cref="PbfState"/> with neighbor cache populated.
	/// </summary>
	public static void CalculatePackingStats(
		float[] predX,
		float[] predY,
		int count,
		PbfState state)
	{
		if (count <= 0)
			return;

		state.EnsurePackingBuffer(count);

		int neighborSum = 0;
		int maximumNeighbors = 0;
		int neighborCapHits = 0;

		double nearestDistanceSum = 0.0;
		double minimumDistance = double.MaxValue;

		int validCount = 0;

		int below1 = 0;
		int below2 = 0;
		int below3 = 0;
		int below4 = 0;

		int stride = state.NeighborStride;
		int[] localCounts = state.NeighborCounts;
		int[] localBuffer = state.NeighborBuffer;
		float[] localPacking = state.PackingNearestDistances;

		for (
			int i = 0;
			i < count;
			i++)
		{
			int neighborCount =
				localCounts[i];

			neighborSum += neighborCount;

			if (neighborCount > maximumNeighbors)
				maximumNeighbors = neighborCount;

			if (neighborCount >= PbfSolver.MaxNeighbors)
				neighborCapHits++;

			float nearestDistance = float.MaxValue;

			int start = i * stride;
			int end = start + neighborCount;

			float px = predX[i];
			float py = predY[i];

			for (
				int index = start;
				index < end;
				index++)
			{
				int j = localBuffer[index];

				float dx = px - predX[j];
				float dy = py - predY[j];

				float distanceSquared =
					dx * dx + dy * dy;

				if (distanceSquared <= 0.000001f)
					continue;

				float distance =
					Mathf.Sqrt(distanceSquared);

				if (distance < nearestDistance)
					nearestDistance = distance;
			}

			if (nearestDistance == float.MaxValue)
			{
				localPacking[i] = float.NaN;
				continue;
			}

			localPacking[i] = nearestDistance;
			nearestDistanceSum += nearestDistance;
			validCount++;

			if (nearestDistance < minimumDistance)
				minimumDistance = nearestDistance;

			if (nearestDistance < 1.0f) below1++;
			if (nearestDistance < 2.0f) below2++;
			if (nearestDistance < 3.0f) below3++;
			if (nearestDistance < 4.0f) below4++;
		}

		if (validCount <= 0)
		{
			GD.Print(
				"[PARTICLE PACKING] " +
				$"Particles: {count} | " +
				"No valid nearest neighbors"
			);

			return;
		}

		Array.Sort(localPacking, 0, validCount);

		int p5Index =
			(int)Math.Floor((validCount - 1) * 0.05);

		int medianIndex =
			(validCount - 1) / 2;

		float p5 = localPacking[p5Index];
		float median = localPacking[medianIndex];

		double averageNearest =
			nearestDistanceSum / validCount;

		double averageNeighbors =
			(double)neighborSum / count;

		double minDistance =
			minimumDistance == double.MaxValue
				? 0.0
				: minimumDistance;

		GD.Print(
			"[PARTICLE PACKING] " +
			$"Particles: {count} | " +
			$"MinDistance: {minDistance:F3} px | " +
			$"AvgNearest: {averageNearest:F3} px | " +
			$"P5: {p5:F3} px | " +
			$"Median: {median:F3} px | " +
			$"<1px: {below1} | " +
			$"<2px: {below2} | " +
			$"<3px: {below3} | " +
			$"<4px: {below4} | " +
			$"AvgNeighbors: {averageNeighbors:F2} | " +
			$"MaxNeighbors: {maximumNeighbors} | " +
			$"NeighborCapHits: {neighborCapHits}"
		);
	}
}
