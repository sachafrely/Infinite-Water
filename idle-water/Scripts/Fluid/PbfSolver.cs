using Godot;

public class PbfSolver
{
	private readonly SpatialHash hash;

	public float SmoothingRadius = 12.0f;

	private float[] lambdas;
	private float[] deltaX;
	private float[] deltaY;

	// Neighbor cache per iteration to avoid double Query calls.
	private int[][] neighbors;
	private int[] neighborCounts;

	private const float RestDensity = 1.0f;
	private const float LambdaEpsilon = 0.0001f;
	private const float Relaxation = 0.01f;
	private const int Iterations = 2;

	private const float MinX = 20.0f;
	private const float MaxX = 700.0f;
	private const float MinY = 20.0f;
	private const float MaxY = 1260.0f;

	private const float CollisionDamping = 0.9f;

	public PbfSolver(SpatialHash hash)
	{
		this.hash = hash;
	}

	public void Solve(ParticleData particles, float dt)
	{
		// Ensure working arrays are allocated for this particle count.
		if (lambdas == null || lambdas.Length < particles.Count)
		{
			lambdas = new float[particles.Count];
			deltaX = new float[particles.Count];
			deltaY = new float[particles.Count];
			neighbors = new int[particles.Count][];
			neighborCounts = new int[particles.Count];
		}

		// 1. Apply gravity and create predicted positions.
		for (int i = 0; i < particles.Count; i++)
		{
			particles.VelY[i] += 500.0f * dt;

			particles.PredX[i] =
				particles.PosX[i] +
				particles.VelX[i] * dt;

			particles.PredY[i] =
				particles.PosY[i] +
				particles.VelY[i] * dt;
		}

		// 2. Iteratively solve the density constraint using cached neighbor lists.
		for (int iteration = 0; iteration < Iterations; iteration++)
		{
			UpdateHash(particles);

			// Phase A: compute densities and lambdas, and cache neighbors.
			for (int i = 0; i < particles.Count; i++)
			{
				Vector2 pos = GetPredictedPosition(particles, i);

				int count = hash.Query(pos, SmoothingRadius);
				neighborCounts[i] = count;

				if (neighbors[i] == null || neighbors[i].Length < count)
					neighbors[i] = new int[count];

				for (int n = 0; n < count; n++)
				{
					neighbors[i][n] = hash.GetResult(n);
				}

				// Compute density and gradient sum using cached neighbors.
				float density = 0.0f;
				float gradientSum = 0.0f;

				for (int n = 0; n < count; n++)
				{
					int neighbor = neighbors[i][n];

					Vector2 neighborPos = GetPredictedPosition(particles, neighbor);

					float dx = pos.X - neighborPos.X;
					float dy = pos.Y - neighborPos.Y;
					float dist2 = dx * dx + dy * dy;

					if (dist2 >= SmoothingRadius * SmoothingRadius)
						continue;

					float dist = Mathf.Sqrt(dist2);
					density += Poly6Kernel(dist);

					if (neighbor == i)
						continue;

					if (dist <= 0.00001f)
						continue;

					float grad = SpikyGradient(dist);
					// gradient vector magnitude squared: (grad^2) since normalized direction has length 1.
					gradientSum += (grad * grad);
				}

				float constraint = density / RestDensity - 1.0f;

				float lambda = -constraint / (gradientSum + LambdaEpsilon);
				lambda *= Relaxation;

				lambdas[i] = lambda;
			}

			// Phase B: compute position deltas for every particle using cached neighbors.
			for (int i = 0; i < particles.Count; i++)
			{
				deltaX[i] = 0.0f;
				deltaY[i] = 0.0f;
			}

			for (int i = 0; i < particles.Count; i++)
			{
				Vector2 pos = GetPredictedPosition(particles, i);
				int count = neighborCounts[i];

				for (int n = 0; n < count; n++)
				{
					int neighbor = neighbors[i][n];
					if (neighbor == i)
						continue;

					Vector2 neighborPos = GetPredictedPosition(particles, neighbor);
					float dx = pos.X - neighborPos.X;
					float dy = pos.Y - neighborPos.Y;
					float dist2 = dx * dx + dy * dy;
					if (dist2 >= SmoothingRadius * SmoothingRadius || dist2 <= 0.00001f)
						continue;

					float dist = Mathf.Sqrt(dist2);
					float grad = SpikyGradient(dist);
					float nx = dx / dist;
					float ny = dy / dist;

					float scalar = (lambdas[i] + lambdas[neighbor]);
					deltaX[i] += scalar * nx * grad;
					deltaY[i] += scalar * ny * grad;
				}
			}

			// Apply all deltas (scaled by rest density) to predicted positions.
			for (int i = 0; i < particles.Count; i++)
			{
				particles.PredX[i] += deltaX[i] / RestDensity;
				particles.PredY[i] += deltaY[i] / RestDensity;
			}

			// Enforce boundary constraints after corrections.
			ConstrainToBounds(particles);
		}

		// 3. Convert corrected predicted positions back into velocity and position.
		for (int i = 0; i < particles.Count; i++)
		{
			float oldX = particles.PosX[i];
			float oldY = particles.PosY[i];

			float newVelocityX = (particles.PredX[i] - oldX) / dt;
			float newVelocityY = (particles.PredY[i] - oldY) / dt;

			if (particles.PredX[i] <= MinX || particles.PredX[i] >= MaxX)
			{
				newVelocityX *= -CollisionDamping;
			}

			if (particles.PredY[i] <= MinY || particles.PredY[i] >= MaxY)
			{
				newVelocityY *= -CollisionDamping;
			}

			particles.VelX[i] = newVelocityX;
			particles.VelY[i] = newVelocityY;

			particles.PosX[i] = particles.PredX[i];
			particles.PosY[i] = particles.PredY[i];
		}
	}

	private void UpdateHash(ParticleData particles)
	{
		hash.Clear();

		for (int i = 0; i < particles.Count; i++)
		{
			// Use the float-based Insert API to avoid Vector2 allocations.
			hash.Insert(i, GetPredictedPosition(particles, i));
		}
	}

	private Vector2 GetPredictedPosition(
		ParticleData particles,
		int i)
	{
		return new Vector2(
			particles.PredX[i],
			particles.PredY[i]
		);
	}

	private float CalculateDensity(
		ParticleData particles,
		Vector2 position)
	{
		float density = 0.0f;

		int count = hash.Query(
			position,
			SmoothingRadius
		);

		for (int n = 0; n < count; n++)
		{
			int neighbor = hash.GetResult(n);

			Vector2 neighborPos =
				GetPredictedPosition(
					particles,
					neighbor
				);

			float distance = position.DistanceTo(neighborPos);

			if (distance >= SmoothingRadius)
				continue;

			density += Poly6Kernel(distance);
		}

		return density;
	}

	private float Poly6Kernel(float distance)
	{
		float h = SmoothingRadius;

		if (distance >= h)
			return 0.0f;

		float h2 = h * h;
		float value = (h2 - distance * distance) / h2; // normalized to [0,1]

		return value * value * value;
	}

	private float SpikyGradient(float distance)
	{
		float h = SmoothingRadius;

		if (distance <= 0.0f || distance >= h)
		{
			return 0.0f;
		}

		float value = (h - distance) / h; // normalized to [0,1]

		return (value * value) / h; // scale by 1/h so gradient magnitude is ~1/h
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
