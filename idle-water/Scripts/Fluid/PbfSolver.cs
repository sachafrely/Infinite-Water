using Godot;

public class PbfSolver
{
	private readonly SpatialHash hash;

	private const float Gravity = 500.0f;
	private const float SmoothingRadius = 12.0f;

	// Isolated particle contributes 1.0 to density.
	private const float RestDensity = 1.0f;

	private const float Epsilon = 0.000001f;

	private const int Iterations = 2;

	private const float MinX = 24.0f;
	private const float MaxX = 696.0f;
	private const float MinY = 24.0f;
	private const float MaxY = 1256.0f;

	private readonly float[] lambda;
	private readonly float[] deltaX;
	private readonly float[] deltaY;

	public PbfSolver(SpatialHash spatialHash)
	{
		hash = spatialHash;

		// Our current project uses at most 10,000 particles.
		lambda = new float[10000];
		deltaX = new float[10000];
		deltaY = new float[10000];
	}

	public void Solve(ParticleData particles, float dt)
	{
		int count = particles.PosX.Length;

		// ------------------------------------------------------------
		// 1. Predict positions
		// ------------------------------------------------------------

		for (int i = 0; i < count; i++)
		{
			particles.VelY[i] += Gravity * dt;

			particles.PredX[i] =
				particles.PosX[i] +
				particles.VelX[i] * dt;

			particles.PredY[i] =
				particles.PosY[i] +
				particles.VelY[i] * dt;
		}

		// ------------------------------------------------------------
		// 2. PBF iterations
		// ------------------------------------------------------------

		for (int iteration = 0; iteration < Iterations; iteration++)
		{
			hash.Clear();

			// Insert predicted positions.
			for (int i = 0; i < count; i++)
			{
				hash.Insert(
					i,
					particles.PredX[i],
					particles.PredY[i]
				);
			}

			// --------------------------------------------------------
			// Calculate lambda for every particle
			// --------------------------------------------------------

			for (int i = 0; i < count; i++)
			{
				float px = particles.PredX[i];
				float py = particles.PredY[i];

				int neighborCount =
					hash.Query(
						px,
						py,
						SmoothingRadius
					);

				float density = 0.0f;

				for (int k = 0; k < neighborCount; k++)
				{
					int j = hash.GetResult(k);

					float dx =
						px - particles.PredX[j];

					float dy =
						py - particles.PredY[j];

					float distanceSquared =
						dx * dx + dy * dy;

					if (distanceSquared >
						SmoothingRadius * SmoothingRadius)
					{
						continue;
					}

					float distance =
						Mathf.Sqrt(distanceSquared);

					float q =
						1.0f -
						distance / SmoothingRadius;

					// Normalized-to-self kernel.
					density += q * q * q;
				}

				float constraint =
					density / RestDensity - 1.0f;

				// Approximation of the PBF constraint denominator.
				float denominator = 0.0f;

				for (int k = 0; k < neighborCount; k++)
				{
					int j = hash.GetResult(k);

					if (j == i)
						continue;

					float dx =
						px - particles.PredX[j];

					float dy =
						py - particles.PredY[j];

					float distanceSquared =
						dx * dx + dy * dy;

					if (distanceSquared < Epsilon ||
						distanceSquared >
						SmoothingRadius * SmoothingRadius)
					{
						continue;
					}

					float distance =
						Mathf.Sqrt(distanceSquared);

					float q =
						1.0f -
						distance / SmoothingRadius;

					float gradientMagnitude =
						3.0f *
						q * q /
						SmoothingRadius;

					denominator +=
						gradientMagnitude *
						gradientMagnitude;
				}

				lambda[i] =
					-constraint /
					(denominator + Epsilon);
			}

			// --------------------------------------------------------
			// Calculate position corrections
			// --------------------------------------------------------

			for (int i = 0; i < count; i++)
			{
				float px = particles.PredX[i];
				float py = particles.PredY[i];

				int neighborCount =
					hash.Query(
						px,
						py,
						SmoothingRadius
					);

				float correctionX = 0.0f;
				float correctionY = 0.0f;

				for (int k = 0; k < neighborCount; k++)
				{
					int j = hash.GetResult(k);

					if (j == i)
						continue;

					float dx =
						px - particles.PredX[j];

					float dy =
						py - particles.PredY[j];

					float distanceSquared =
						dx * dx + dy * dy;

					if (distanceSquared < Epsilon ||
						distanceSquared >
						SmoothingRadius * SmoothingRadius)
					{
						continue;
					}

					float distance =
						Mathf.Sqrt(distanceSquared);

					float q =
						1.0f -
						distance / SmoothingRadius;

					float gradient =
						-3.0f *
						q * q /
						SmoothingRadius;

					float gradientX =
						gradient * dx / distance;

					float gradientY =
						gradient * dy / distance;

					float strength =
						lambda[i] +
						lambda[j];

					correctionX +=
						strength * gradientX;

					correctionY +=
						strength * gradientY;
				}

				deltaX[i] = correctionX;
				deltaY[i] = correctionY;
			}

			// --------------------------------------------------------
			// Apply corrections and collision
			// --------------------------------------------------------

			for (int i = 0; i < count; i++)
			{
				particles.PredX[i] += deltaX[i];
				particles.PredY[i] += deltaY[i];

				particles.PredX[i] =
					Mathf.Clamp(
						particles.PredX[i],
						MinX,
						MaxX
					);

				particles.PredY[i] =
					Mathf.Clamp(
						particles.PredY[i],
						MinY,
						MaxY
					);
			}
		}

		// ------------------------------------------------------------
		// 3. Update velocity and positions
		// ------------------------------------------------------------

		for (int i = 0; i < count; i++)
		{
			particles.VelX[i] =
				(particles.PredX[i] -
				 particles.PosX[i]) / dt;

			particles.VelY[i] =
				(particles.PredY[i] -
				 particles.PosY[i]) / dt;

			particles.PosX[i] =
				particles.PredX[i];

			particles.PosY[i] =
				particles.PredY[i];

			// Stop particles from bouncing off the floor.
			if (particles.PosY[i] >= MaxY &&
				particles.VelY[i] > 0.0f)
			{
				particles.VelY[i] = 0.0f;
			}

			if (particles.PosX[i] <= MinX ||
				particles.PosX[i] >= MaxX)
			{
				particles.VelX[i] = 0.0f;
			}
		}
	}
}
