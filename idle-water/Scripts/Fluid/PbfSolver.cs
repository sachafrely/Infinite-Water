using Godot;

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

	private float[] lambda;
	private float[] deltaX;
	private float[] deltaY;

	// Neighbor cache per iteration to avoid double Query calls.
	private int[][] neighbors;
	private int[] neighborCounts;

	private const float LambdaEpsilon = 0.0001f;
	private const float Relaxation = 0.01f;
	private const float CollisionDamping = 0.9f;

	public PbfSolver(SpatialHash spatialHash)
	{
		hash = spatialHash;
	}

	public void Solve(ParticleData particles, float dt)
	{
		int count = particles.Count;

		// Allocate working buffers if needed.
		if (lambda == null || lambda.Length < count)
		{
			lambda = new float[count];
			deltaX = new float[count];
			deltaY = new float[count];
			neighbors = new int[count][];
			neighborCounts = new int[count];
		}

		// 1) Predict positions
		for (int i = 0; i < count; i++)
		{
			particles.VelY[i] += Gravity * dt;
			particles.PredX[i] = particles.PosX[i] + particles.VelX[i] * dt;
			particles.PredY[i] = particles.PosY[i] + particles.VelY[i] * dt;
		}

		// 2) PBF iterations (reduced to 1 for profiling)
		for (int iter = 0; iter < Iterations; iter++)
		{
			// Rebuild spatial hash using float API to avoid Vector2 allocations.
			hash.Clear();
			for (int i = 0; i < count; i++)
				hash.Insert(i, particles.PredX[i], particles.PredY[i]);

			// Phase A: compute density & gradient-sum in one neighbor traversal and cache neighbors.
			for (int i = 0; i < count; i++)
			{
				float px = particles.PredX[i];
				float py = particles.PredY[i];

				int ncount = hash.Query(px, py, SmoothingRadius);
				neighborCounts[i] = ncount;

				if (neighbors[i] == null || neighbors[i].Length < ncount)
					neighbors[i] = new int[ncount];

				for (int n = 0; n < ncount; n++)
					neighbors[i][n] = hash.GetResult(n);

				float density = 0.0f;
				float gradSum = 0.0f;

				for (int n = 0; n < ncount; n++)
				{
					int j = neighbors[i][n];
					float dx = px - particles.PredX[j];
					float dy = py - particles.PredY[j];
					float dist2 = dx * dx + dy * dy;
					if (dist2 >= SmoothingRadius * SmoothingRadius)
						continue;
					float dist = Mathf.Sqrt(dist2);
					float q = 1.0f - dist / SmoothingRadius;
					density += q * q * q; // Poly6-style

					if (j == i)
						continue;
					if (dist <= 1e-5f)
						continue;
					float g = SpikyGradient(dist);
					gradSum += (g * g);
				}

				float constraint = density / RestDensity - 1.0f;
				float lam = -constraint / (gradSum + LambdaEpsilon);
				lam *= Relaxation;
				lambda[i] = lam;
			}

			// Phase B: compute position deltas using cached neighbors.
			for (int i = 0; i < count; i++)
			{
				deltaX[i] = 0.0f;
				deltaY[i] = 0.0f;
			}

			for (int i = 0; i < count; i++)
			{
				float px = particles.PredX[i];
				int ncount = neighborCounts[i];
				for (int n = 0; n < ncount; n++)
				{
					int j = neighbors[i][n];
					if (j == i) continue;
					float dx = px - particles.PredX[j];
					float dy = py - particles.PredY[j];
					float dist2 = dx * dx + dy * dy;
					if (dist2 >= SmoothingRadius * SmoothingRadius || dist2 <= 1e-5f) continue;
					float dist = Mathf.Sqrt(dist2);
					float g = SpikyGradient(dist);
					float nx = dx / dist;
					float ny = dy / dist;
					float s = (lambda[i] + lambda[j]);
					deltaX[i] += s * nx * g;
					deltaY[i] += s * ny * g;
				}
			}

			// Apply deltas
			for (int i = 0; i < count; i++)
			{
				particles.PredX[i] += deltaX[i] / RestDensity;
				particles.PredY[i] += deltaY[i] / RestDensity;
			}

			ConstrainToBounds(particles);
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
