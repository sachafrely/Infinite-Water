using System;
using System.Diagnostics;

/// <summary>
/// PbfSolverCoordinator — top-level coordinator for the PBF pipeline.
///
/// Owns the <see cref="PbfState"/> and drives each sub-pass in the correct
/// order every physics tick.  <see cref="PbfSolver"/> holds the polygon-
/// collider management and delegates <see cref="PbfSolver.Solve"/> here.
///
/// Sub-pass order per tick:
/// <list type="number">
///   <item>Apply gravity and predict new positions.</item>
///   <item>Rebuild spatial hash.</item>
///   <item>Build neighbor-index cache (<see cref="PbfNeighborSearchAdapter"/>).</item>
///   <item>Compute neighbor geometry.</item>
///   <item>PBF iterations:
///     <list type="bullet">
///       <item>Density + lambda (<see cref="PbfDensityConstraintsCoordinator"/>).</item>
///       <item>Position corrections (<see cref="PbfPositionDeltaSolver"/>).</item>
///       <item>Pixel-overlap correction.</item>
///       <item>Polygon-collider constraints (via PbfSolver callback).</item>
///       <item>World-bounds constraints (<see cref="PbfBoundaryConstraints"/>).</item>
///     </list>
///   </item>
///   <item>Velocity integration and position commit (<see cref="PbfIntegrationStep"/>).</item>
/// </list>
/// </summary>
internal sealed class PbfSolverCoordinator
{
	// ============================================================
	// Fields
	// ============================================================

	private readonly SpatialHash hash;
	private readonly PbfSolver solver;

	private int profilerFrameCounter = 0;

	// ============================================================
	// State
	// ============================================================

	/// <summary>
	/// Mutable per-step state owned by this coordinator.
	/// Sub-modules access it via method parameters.
	/// </summary>
	public PbfState State { get; } =
		new PbfState();

	// ============================================================
	// Constructor
	// ============================================================

	public PbfSolverCoordinator(
		SpatialHash spatialHash,
		PbfSolver pbfSolver)
	{
		hash = spatialHash;
		solver = pbfSolver;
	}

	// ============================================================
	// Main solve
	// ============================================================

	public void Solve(
		ParticleData particles,
		float dt)
	{
		profilerFrameCounter++;

		bool printProfiler =
			profilerFrameCounter >=
			PbfSolver.ProfilerIntervalFrames;

		if (printProfiler)
			profilerFrameCounter = 0;

		long totalStart =
			Stopwatch.GetTimestamp();

		double predictMs = 0.0;
		double spatialHashMs = 0.0;
		double neighborSearchMs = 0.0;
		double neighborGeometryMs = 0.0;
		double neighborCacheMs = 0.0;
		double pbfMs = 0.0;
		double collisionMs = 0.0;
		double terrainQueryMs = 0.0;
		double terrainResolveMs = 0.0;
		double wheelCollisionMs = 0.0;
		double boundsMs = 0.0;
		double velocityMs = 0.0;

		int count = particles.Count;

		if (count <= 0 || dt <= 0.0f)
		{
			solver.StepWheel(dt);

			double totalMs =
				ElapsedMilliseconds(totalStart);

			if (printProfiler)
			{
				PbfDebugStats.PrintProfiler(
					predictMs, spatialHashMs,
					neighborSearchMs, neighborGeometryMs,
					neighborCacheMs, pbfMs,
					collisionMs, terrainQueryMs,
					terrainResolveMs, wheelCollisionMs,
					boundsMs, velocityMs,
					totalMs, 0
				);
			}

			return;
		}

		PbfState state = State;
		state.EnsureCapacity(count);

		float[] posX = particles.PosX;
		float[] posY = particles.PosY;
		float[] velX = particles.VelX;
		float[] velY = particles.VelY;
		float[] predX = particles.PredX;
		float[] predY = particles.PredY;

		// --------------------------------------------------------
		// Clear collision state
		// --------------------------------------------------------

		Array.Clear(state.Impacted, 0, count);
		Array.Clear(state.ImpactNormalX, 0, count);
		Array.Clear(state.ImpactNormalY, 0, count);

		// --------------------------------------------------------
		// Rotate wheel before collision detection
		// --------------------------------------------------------

		solver.StepWheelAndUpdateColliders(dt);

		// --------------------------------------------------------
		// Prepare terrain collider grid
		// --------------------------------------------------------

		solver.EnsureColliderGrid();

		// --------------------------------------------------------
		// Predict (gravity + Euler integration)
		// --------------------------------------------------------

		long predictStart = Stopwatch.GetTimestamp();

		float gravityDt = PbfSolver.Gravity * dt;

		for (int i = 0; i < count; i++)
		{
			float vx = velX[i];
			float vy = velY[i] + gravityDt;

			velY[i] = vy;

			predX[i] = posX[i] + vx * dt;
			predY[i] = posY[i] + vy * dt;
		}

		predictMs =
			ElapsedMilliseconds(predictStart);

		// --------------------------------------------------------
		// Spatial hash
		// --------------------------------------------------------

		long spatialHashStart =
			Stopwatch.GetTimestamp();

		hash.Clear();

		for (int i = 0; i < count; i++)
		{
			hash.Insert(i, predX[i], predY[i]);
		}

		spatialHashMs =
			ElapsedMilliseconds(spatialHashStart);

		// --------------------------------------------------------
		// Initial neighbor cache
		// --------------------------------------------------------

		long neighborCacheStart =
			Stopwatch.GetTimestamp();

		long neighborSearchStart =
			Stopwatch.GetTimestamp();

		PbfNeighborSearchAdapter.BuildIndexCache(
			hash, predX, predY, count, state
		);

		neighborSearchMs =
			ElapsedMilliseconds(neighborSearchStart);

		long neighborGeometryStart =
			Stopwatch.GetTimestamp();

		PbfNeighborSearchAdapter.UpdateGeometry(
			predX, predY, count, state
		);

		neighborGeometryMs =
			ElapsedMilliseconds(neighborGeometryStart);

		neighborCacheMs =
			ElapsedMilliseconds(neighborCacheStart);

		// --------------------------------------------------------
		// Particle packing profiler
		// --------------------------------------------------------

		if (printProfiler)
		{
			PbfDebugStats.CalculatePackingStats(
				predX, predY, count, state
			);
		}

		// --------------------------------------------------------
		// PBF iterations
		// --------------------------------------------------------

		long pbfStart = Stopwatch.GetTimestamp();

		for (
			int iteration = 0;
			iteration < PbfSolver.MaxIterations;
			iteration++)
		{
			if (iteration > 0)
			{
				long geometryStart =
					Stopwatch.GetTimestamp();

				PbfNeighborSearchAdapter.UpdateCache(
					predX, predY, count, state
				);

				double geometryMs =
					ElapsedMilliseconds(geometryStart);

				neighborGeometryMs += geometryMs;
				neighborCacheMs += geometryMs;
			}

			float densityError =
				PbfDensityConstraintsCoordinator.ComputeDensityAndLambdas(
					count, state
				);

			PbfPositionDeltaSolver.ApplyCorrections(
				predX, predY, count, state
			);

			if (iteration + 1 >= PbfSolver.MinIterations)
			{
				PbfPositionDeltaSolver.ApplyPixelOccupancyCorrection(
					predX, predY, count, state
				);
			}

			if (solver.HasPolygonColliders)
			{
				long collisionStart =
					Stopwatch.GetTimestamp();

				solver.ApplyPolygonCollision(
					predX, predY,
					posX, posY,
					velX, velY,
					count, dt,
					iteration == 0,
					ref terrainQueryMs,
					ref terrainResolveMs,
					ref wheelCollisionMs
				);

				collisionMs +=
					ElapsedMilliseconds(collisionStart);
			}

			long boundsStart =
				Stopwatch.GetTimestamp();

			PbfBoundaryConstraints.ConstrainToBounds(
				predX, predY, count, state
			);

			boundsMs +=
				ElapsedMilliseconds(boundsStart);

			if (
				iteration + 1 >= PbfSolver.MinIterations &&
				densityError <=
				PbfSolver.DensityErrorThreshold)
			{
				break;
			}
		}

		pbfMs = ElapsedMilliseconds(pbfStart);

		// --------------------------------------------------------
		// Velocity integration and position commit
		// --------------------------------------------------------

		long velocityStart =
			Stopwatch.GetTimestamp();

		PbfIntegrationStep.Finalize(
			particles, dt, count, state
		);

		velocityMs =
			ElapsedMilliseconds(velocityStart);

		double total =
			ElapsedMilliseconds(totalStart);

		if (printProfiler)
		{
			PbfDebugStats.PrintProfiler(
				predictMs, spatialHashMs,
				neighborSearchMs, neighborGeometryMs,
				neighborCacheMs, pbfMs,
				collisionMs, terrainQueryMs,
				terrainResolveMs, wheelCollisionMs,
				boundsMs, velocityMs,
				total, count
			);
		}
	}

	// ============================================================
	// Timing helper
	// ============================================================

	private static double ElapsedMilliseconds(
		long startTimestamp)
	{
		return
			(
				Stopwatch.GetTimestamp() -
				startTimestamp
			) *
			1000.0 /
			Stopwatch.Frequency;
	}
}
