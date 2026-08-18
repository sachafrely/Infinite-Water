using System;
using System.Diagnostics;
using Godot;

/// <summary>
/// PbfSolverCoordinator — top-level coordinator for the PBF pipeline.
///
/// Owns the <see cref="PbfState"/> and drives each sub-pass in the correct
/// order every physics tick.
/// </summary>
internal sealed class PbfSolverCoordinator
{
	private readonly SpatialHash hash;
	private readonly PbfSolver solver;
	private int profilerFrameCounter = 0;

	public PbfState State { get; } =
		new PbfState();

	public PbfSolverCoordinator(
		SpatialHash spatialHash,
		PbfSolver pbfSolver)
	{
		hash = spatialHash;
		solver = pbfSolver;
	}

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

		long totalStart = Stopwatch.GetTimestamp();

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

			double totalMs = ElapsedMilliseconds(totalStart);

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

		Array.Clear(state.Impacted, 0, count);
		Array.Clear(state.ImpactNormalX, 0, count);
		Array.Clear(state.ImpactNormalY, 0, count);

		solver.StepWheelAndUpdateColliders(dt);
		solver.EnsureColliderGrid();

		// --------------------------------------------------------
		// Predict (gravity + Euler integration)
		// --------------------------------------------------------

		long predictStart = Stopwatch.GetTimestamp();

		Vector2 gravityAcceleration =
			TiltController.CurrentGravityAcceleration;

		float gravityDtX = gravityAcceleration.X * dt;
		float gravityDtY = gravityAcceleration.Y * dt;

		for (int i = 0; i < count; i++)
		{
			float vx = velX[i] + gravityDtX;
			float vy = velY[i] + gravityDtY;

			velX[i] = vx;
			velY[i] = vy;

			predX[i] = posX[i] + vx * dt;
			predY[i] = posY[i] + vy * dt;
		}

		predictMs = ElapsedMilliseconds(predictStart);

		long spatialHashStart = Stopwatch.GetTimestamp();

		hash.Clear();

		for (int i = 0; i < count; i++)
			hash.Insert(i, predX[i], predY[i]);

		spatialHashMs = ElapsedMilliseconds(spatialHashStart);

		long neighborCacheStart = Stopwatch.GetTimestamp();
		long neighborSearchStart = Stopwatch.GetTimestamp();

		PbfNeighborSearchAdapter.BuildIndexCache(
			hash, predX, predY, count, state
		);

		neighborSearchMs = ElapsedMilliseconds(neighborSearchStart);

		long neighborGeometryStart = Stopwatch.GetTimestamp();

		PbfNeighborSearchAdapter.UpdateGeometry(
			predX, predY, count, state
		);

		neighborGeometryMs = ElapsedMilliseconds(neighborGeometryStart);
		neighborCacheMs = ElapsedMilliseconds(neighborCacheStart);

		if (printProfiler)
		{
			PbfDebugStats.CalculatePackingStats(
				predX, predY, count, state
			);
		}

		long pbfStart = Stopwatch.GetTimestamp();

		for (
			int iteration = 0;
			iteration < PbfSolver.MaxIterations;
			iteration++)
		{
			if (iteration > 0)
			{
				long geometryStart = Stopwatch.GetTimestamp();

				PbfNeighborSearchAdapter.UpdateCache(
					predX, predY, count, state
				);

				double geometryMs = ElapsedMilliseconds(geometryStart);

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
				long collisionStart = Stopwatch.GetTimestamp();

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

				collisionMs += ElapsedMilliseconds(collisionStart);
			}

			long boundsStart = Stopwatch.GetTimestamp();

			PbfBoundaryConstraints.ConstrainToBounds(
				predX, predY, count, state
			);

			boundsMs += ElapsedMilliseconds(boundsStart);

			if (
				iteration + 1 >= PbfSolver.MinIterations &&
				densityError <= PbfSolver.DensityErrorThreshold)
			{
				break;
			}
		}

		pbfMs = ElapsedMilliseconds(pbfStart);

		long velocityStart = Stopwatch.GetTimestamp();

		PbfIntegrationStep.Finalize(
			particles,
			dt,
			count,
			state,
			gravityAcceleration
		);

		velocityMs = ElapsedMilliseconds(velocityStart);

		double total = ElapsedMilliseconds(totalStart);

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
