using Godot;
using System;

public class PbfSolver
{
	private readonly SpatialHash hash;

	private const float Gravity = 500.0f;
	private const float SmoothingRadius = 12.0f;

	// Isolated particle contributes 1.0 to density.
	private const float RestDensity = 1.0f;

	private const float Epsilon = 0.000001f;

	private const int Iterations = 1;

	private const float MinX = 24.0f;
	private const float MaxX = 696.0f;
	private const float MinY = 24.0f;
	private const float MaxY = 1256.0f;

	private float[] lambdas;
	private float[] deltaX;
	private float[] deltaY;

	// Contiguous neighbor buffer to avoid per-particle allocations.
	private int[] neighborBuffer;
	private int[] neighborOffsets;
	private int[] neighborCounts;

	private const float LambdaEpsilon = 0.0001f;
	private const float Relaxation = 0.01f;
	private const float CollisionDamping = 0.9f;

	// Perf knobs
	private const int MaxNeighbors = 32; // cap per-particle neighbors for bounded cost

	// Lightweight profiler (averages printed every N frames).
	private const int ProfilerPrintInterval = 60;
	private int profilerFrames = 0;
	private ulong accumBuild = 0;
	private ulong accumPhaseA = 0;
	private ulong accumPhaseB = 0;
	private ulong accumTotal = 0;

	public PbfSolver(SpatialHash spatialHash)
	{
		hash = spatialHash;
	}

	public void Solve(ParticleData particles, float dt)
	{
		int count = particles.Count;

		// Allocate working buffers if needed.
		if (lambdas == null || lambdas.Length < count)
		{
			lambdas = new float[count];
			deltaX = new float[count];
			deltaY = new float[count];
			neighborOffsets = new int[count];
			neighborCounts = new int[count];
			neighborBuffer = new int[Mathf.Max(1, count * 8)]; // initial capacity guess
		}

		ulong tTotalStart = OS.GetTicksMsec();

		// 1) Predict positions
		for (int i = 0; i < count; i++)
		{
			particles.VelY[i] += Gravity * dt;
			particles.PredX[i] = particles.PosX[i] + particles.VelX[i] * dt;
			particles.PredY[i] = particles.PosY[i] + particles.VelY[i] * dt;
		}

		// 2) PBF iterations (reduced for profiling)
		for (int iter = 0; iter < Iterations; iter++)
		{
			// Rebuild spatial hash using float API to avoid Vector2 allocations.
			ulong tBuildStart = OS.GetTicksMsec();
			hash.Clear();
			for (int i = 0; i < count; i++)
				hash.Insert(i, particles.PredX[i], particles.PredY[i]);
			accumBuild += OS.GetTicksMsec() - tBuildStart;

			// Phase A: compute density & gradient-sum in one neighbor traversal and cache neighbors in a contiguous buffer.
			ulong tPhaseAStart = OS.GetTicksMsec();
			int bufferWritePos = 0;
			for (int i = 0; i < count; i++)
			{
				float px = particles.PredX[i];
				float py = particles.PredY[i];

				int rawCount = hash.Query(px, py, SmoothingRadius);
				int ncount = rawCount > MaxNeighbors ? MaxNeighbors : rawCount; // cap neighbors to bound cost
				neighborCounts[i] = ncount;
				neighborOffsets[i] = bufferWritePos;

				int required = bufferWritePos + ncount;
				if (neighborBuffer == null || neighborBuffer.Length < required)
				{
					int newCap = Math.Max(neighborBuffer == null ? 0 : neighborBuffer.Length * 2, required);
					if (newCap < required) newCap = required;
					int[] newBuf = new int[newCap];
					if (neighborBuffer != null) Array.Copy(neighborBuffer, newBuf, neighborBuffer.Length);
					neighborBuffer = newBuf;
				}

				// Copy up to ncount neighbors from the hash results.
				for (int n = 0; n < ncount; n++)
				{
					int nb = hash.GetResult(n);
					neighborBuffer[bufferWritePos + n] = nb;
				}
				bufferWritePos += ncount;

				// Compute density and gradient-sum using the cached neighbors.
				float density = 0.0f;
				float gradSum = 0.0f;
				int baseOff = neighborOffsets[i];
				for (int n = 0; n < ncount; n++)
				{
					int j = neighborBuffer[baseOff + n];
					float dx = px - particles.PredX[j];
					float dy = py - particles.PredY[j];
					float dist2 = dx * dx + dy * dy;
					if (dist2 >= SmoothingRadius * SmoothingRadius) continue;
					float dist = Mathf.Sqrt(dist2);
					float q = 1.0f - dist / SmoothingRadius;
					density += q * q * q;
					if (j == i) continue;
					if (dist <= 1e-5f) continue;
					float g = SpikyGradient(dist);
					gradSum += (g * g);
				}

				float constraint = density / RestDensity - 1.0f;
				float lam = -constraint / (gradSum + LambdaEpsilon);
				lam *= Relaxation;
				lambdas[i] = lam;
			}
			accumPhaseA += OS.GetTicksMsec() - tPhaseAStart;

			// Phase B: compute position deltas using the contiguous neighbor buffer.
			ulong tPhaseBStart = OS.GetTicksMsec();
			for (int i = 0; i < count; i++)
			{
				deltaX[i] = 0.0f;
				deltaY[i] = 0.0f;
			}

			for (int i = 0; i < count; i++)
			{
				float px = particles.PredX[i];
				float py = particles.PredY[i];
				int ncount = neighborCounts[i];
				int baseOff = neighborOffsets[i];
				for (int n = 0; n < ncount; n++)
				{
					int j = neighborBuffer[baseOff + n];
					if (j == i) continue;
					float dx = px - particles.PredX[j];
					float dy = py - particles.PredY[j];
					float dist2 = dx * dx + dy * dy;
					if (dist2 >= SmoothingRadius * SmoothingRadius || dist2 <= 1e-5f) continue;
					float dist = Mathf.Sqrt(dist2);
					float g = SpikyGradient(dist);
					float nx = dx / dist;
					float ny = dy / dist;
					float s = (lambdas[i] + lambdas[j]);
					deltaX[i] += s * nx * g;
					deltaY[i] += s * ny * g;
				}
			}
			accumPhaseB += OS.GetTicksMsec() - tPhaseBStart;

			// Apply deltas
			for (int i = 0; i < count; i++)
			{
				particles.PredX[i] += deltaX[i] / RestDensity;
				particles.PredY[i] += deltaY[i] / RestDensity;
			}

			ConstrainToBounds(particles);
		}

		accumTotal += OS.GetTicksMsec() - tTotalStart;
		profilerFrames++;
		if (profilerFrames >= ProfilerPrintInterval)
		{
			ulong avgBuild = accumBuild / (ulong)profilerFrames;
			ulong avgA = accumPhaseA / (ulong)profilerFrames;
			ulong avgB = accumPhaseB / (ulong)profilerFrames;
			ulong avgTotal = accumTotal / (ulong)profilerFrames;
			GD.Print($"PBF profiler (avg ms over {profilerFrames} frames): Build={avgBuild}ms PhaseA={avgA}ms PhaseB={avgB}ms Total={avgTotal}ms (MaxNeighbors={MaxNeighbors})");
			// reset
			profilerFrames = 0;
			accumBuild = accumPhaseA = accumPhaseB = accumTotal = 0;
		}

		// 3) Update velocities & positions
		for (int i = 0; i < count; i++)
		{
			float oldX = particles.PosX[i];
			float oldY = particles.PosY[i];
			float newVelX = (particles.PredX[i] - oldX) / dt;
			float newVelY = (particles.PredY[i] - oldY) / dt;
			if (particles.PredX[i] <= MinX || particles.PredX[i] >= MaxX) newVelX *= -CollisionDamping;
			if (particles.PredY[i] <= MinY || particles.PredY[i] >= MaxY) newVelY *= -CollisionDamping;
			particles.VelX[i] = newVelX;
			particles.VelY[i] = newVelY;
			particles.PosX[i] = particles.PredX[i];
			particles.PosY[i] = particles.PredY[i];
		}
	}

	private float SpikyGradient(float distance)
	{
		float h = SmoothingRadius;
		if (distance <= 0.0f || distance >= h) return 0.0f;
		float v = (h - distance) / h;
		return (v * v) / h;
	}

	private void ConstrainToBounds(ParticleData particles)
	{
		for (int i = 0; i < particles.Count; i++)
		{
			particles.PredX[i] = Mathf.Clamp(particles.PredX[i], MinX, MaxX);
			particles.PredY[i] = Mathf.Clamp(particles.PredY[i], MinY, MaxY);
		}
	}
}
