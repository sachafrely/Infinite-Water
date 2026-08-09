
using Godot;
using System;
using System.Diagnostics;

public class PbfSolver
{
	private readonly SpatialHash hash;

	// ------------------------------------------------------------
	// Simulation parameters
	// ------------------------------------------------------------

	private const float Gravity = 200f;
	private const float SmoothingRadius = 12.0f;
	private const float Viscosity = 0.4f;
	private const float SurfaceTension = 18.0f;

	// Minimum surface-normal strength before we consider
	// a particle to be a surface particle.
	private const float SurfaceThreshold = 0.35f;

	// Prevent surface tension from becoming unstable.
	private const float MaxSurfaceVelocity = 25.0f;
	// Our particles start 8 pixels apart.
	// With the q^3 kernel below, the density of that initial
	// lattice is approximately 1.15.
	private const float RestDensity = 1.15f;

	private const float LambdaEpsilon = 0.00001f;

	// Start with one iteration for performance/stability.
	private const int Iterations = 1;

	// Position correction safety limit.
	private const float MaxCorrection = 1.5f;

	// Simulation boundaries.
	private const float MinX = 24.0f;
	private const float MaxX = 696.0f;
	private const float MinY = 24.0f;
	private const float MaxY = 1256.0f;

	// Only process this many neighbors per particle.
	private const int MaxNeighbors = 32;

	// ------------------------------------------------------------
	// Working arrays
	// ------------------------------------------------------------

	private float[] lambdas;
	private float[] deltaX;
	private float[] deltaY;

	private float[] gradientSumX;
	private float[] gradientSumY;
	private float[] gradientSumSquared;
	
	private float[] viscosityX;
	private float[] viscosityY;
	
	private float[] surfaceVelocityX;
	private float[] surfaceVelocityY;
	
	public bool[] SurfaceParticles;
	
	private int[] neighborBuffer;
	private int[] neighborOffsets;
	private int[] neighborCounts;

	// ------------------------------------------------------------
	// Profiler
	// ------------------------------------------------------------

	private const int ProfilerPrintInterval = 60;

	private int profilerFrames;

	private double accumBuildMs;
	private double accumPhaseAMs;
	private double accumPhaseBMs;
	private double accumTotalMs;

	public PbfSolver(SpatialHash spatialHash)
	{
		hash = spatialHash;
	}

	public void Solve(ParticleData particles, float dt)
	{
		int count = particles.Count;

		EnsureBuffers(count);

		long totalStart = Stopwatch.GetTimestamp();

		// ============================================================
		// 1. Predict positions
		// ============================================================

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
		

		// ============================================================
		// 2. PBF iterations
		// ============================================================

		for (int iteration = 0;
			 iteration < Iterations;
			 iteration++)
		{
			// --------------------------------------------------------
			// Build spatial hash
			// --------------------------------------------------------

			long buildStart = Stopwatch.GetTimestamp();

			hash.Clear();

			for (int i = 0; i < count; i++)
			{
				hash.Insert(
					i,
					particles.PredX[i],
					particles.PredY[i]
				);
			}

			long buildEnd = Stopwatch.GetTimestamp();

			accumBuildMs +=
				(buildEnd - buildStart) *
				1000.0 /
				Stopwatch.Frequency;

			// --------------------------------------------------------
			// Find and cache neighbors
			// --------------------------------------------------------

			int bufferWritePosition = 0;

			for (int i = 0; i < count; i++)
			{
				float px = particles.PredX[i];
				float py = particles.PredY[i];

				int rawCount =
					hash.Query(
						px,
						py,
						SmoothingRadius
					);

				int neighborCount =
					Math.Min(
						rawCount,
						MaxNeighbors
					);

				neighborCounts[i] =
					neighborCount;

				neighborOffsets[i] =
					bufferWritePosition;

				EnsureNeighborCapacity(
					bufferWritePosition +
					neighborCount
				);

				for (int n = 0;
					 n < neighborCount;
					 n++)
				{
					neighborBuffer[
						bufferWritePosition + n
					] = hash.GetResult(n);
				}

				bufferWritePosition +=
					neighborCount;
			}

			// ========================================================
			// Phase A
			//
			// Calculate density and the complete PBF gradient.
			// ========================================================

			long phaseAStart =
				Stopwatch.GetTimestamp();

			for (int i = 0; i < count; i++)
			{
				float px =
					particles.PredX[i];

				float py =
					particles.PredY[i];

				int neighborCount =
					neighborCounts[i];

				int offset =
					neighborOffsets[i];

				float density =
					0.0f;

				float gradSumX =
					0.0f;

				float gradSumY =
					0.0f;

				float neighborGradientSquared =
					0.0f;

				// ----------------------------------------------------
				// Density
				// ----------------------------------------------------

				for (int n = 0;
					 n < neighborCount;
					 n++)
				{
					int j =
						neighborBuffer[
							offset + n
						];

					float dx =
						px -
						particles.PredX[j];

					float dy =
						py -
						particles.PredY[j];

					float distanceSquared =
						dx * dx +
						dy * dy;

					if (distanceSquared >=
						SmoothingRadius *
						SmoothingRadius)
					{
						continue;
					}

					float distance =
						Mathf.Sqrt(
							distanceSquared
						);

					float q =
						1.0f -
						distance /
						SmoothingRadius;

					density +=
						q * q * q;
				}

				// ----------------------------------------------------
				// Constraint
				// ----------------------------------------------------

				float constraint =
					density /
					RestDensity -
					1.0f;

				// ----------------------------------------------------
				// Complete gradient of constraint C_i
				//
				// grad_i C_i =
				//     sum_j grad W_ij / rho0
				//
				// grad_j C_i =
				//     -grad W_ij / rho0
				// ----------------------------------------------------

				for (int n = 0;
					 n < neighborCount;
					 n++)
				{
					int j =
						neighborBuffer[
							offset + n
						];

					if (j == i)
					{
						continue;
					}

					float dx =
						px -
						particles.PredX[j];

					float dy =
						py -
						particles.PredY[j];

					float distanceSquared =
						dx * dx +
						dy * dy;

					if (distanceSquared <=
						0.000001f ||
						distanceSquared >=
						SmoothingRadius *
						SmoothingRadius)
					{
						continue;
					}

					float distance =
						Mathf.Sqrt(
							distanceSquared
						);

					float q =
						1.0f -
						distance /
						SmoothingRadius;

					// Gradient of q^3.
					float gradientMagnitude =
						-3.0f *
						q * q /
						SmoothingRadius;

					// Normalize direction.
					float nx =
						dx / distance;

					float ny =
						dy / distance;

					float gx =
						gradientMagnitude *
						nx /
						RestDensity;

					float gy =
						gradientMagnitude *
						ny /
						RestDensity;

					// Contribution to grad_i C_i.
					gradSumX += gx;
					gradSumY += gy;

					// Contribution from grad_j C_i.
					neighborGradientSquared +=
						gx * gx +
						gy * gy;
				}

				float denominator =
					gradSumX * gradSumX +
					gradSumY * gradSumY +
					neighborGradientSquared;

				lambdas[i] =
					-constraint /
					(denominator +
					 LambdaEpsilon);

				// Store the complete gradient for diagnostics/
				// possible future optimizations.
				gradientSumX[i] =
					gradSumX;

				gradientSumY[i] =
					gradSumY;

				gradientSumSquared[i] =
					denominator;
			}

			long phaseAEnd =
				Stopwatch.GetTimestamp();

			accumPhaseAMs +=
				(phaseAEnd - phaseAStart) *
				1000.0 /
				Stopwatch.Frequency;

			// ========================================================
			// Phase B
			//
			// Calculate position corrections.
			// ========================================================

			long phaseBStart =
				Stopwatch.GetTimestamp();

			for (int i = 0; i < count; i++)
			{
				deltaX[i] =
					0.0f;

				deltaY[i] =
					0.0f;
			}

			for (int i = 0; i < count; i++)
			{
				float px =
					particles.PredX[i];

				float py =
					particles.PredY[i];

				int neighborCount =
					neighborCounts[i];

				int offset =
					neighborOffsets[i];

				float correctionX =
					0.0f;

				float correctionY =
					0.0f;

				for (int n = 0;
					 n < neighborCount;
					 n++)
				{
					int j =
						neighborBuffer[
							offset + n
						];

					if (j == i)
					{
						continue;
					}

					float dx =
						px -
						particles.PredX[j];

					float dy =
						py -
						particles.PredY[j];

					float distanceSquared =
						dx * dx +
						dy * dy;

					if (distanceSquared <=
						0.000001f ||
						distanceSquared >=
						SmoothingRadius *
						SmoothingRadius)
					{
						continue;
					}

					float distance =
						Mathf.Sqrt(
							distanceSquared
						);

					float q =
						1.0f -
						distance /
						SmoothingRadius;

					float gradientMagnitude =
						-3.0f *
						q * q /
						SmoothingRadius;

					float nx =
						dx / distance;

					float ny =
						dy / distance;

					float gradientX =
						gradientMagnitude *
						nx /
						RestDensity;

					float gradientY =
						gradientMagnitude *
						ny /
						RestDensity;

					float lambdaSum =
						lambdas[i] +
						lambdas[j];

					correctionX +=
						lambdaSum *
						gradientX;

					correctionY +=
						lambdaSum *
						gradientY;
				}

				deltaX[i] =
					correctionX;

				deltaY[i] =
					correctionY;

				// ----------------------------------------------------
				// Safety clamp.
				//
				// A single unstable correction must not launch a
				// particle across the simulation.
				// ----------------------------------------------------

				float correctionLength =
					Mathf.Sqrt(
						correctionX * correctionX +
						correctionY * correctionY
					);

				if (correctionLength >
					MaxCorrection)
				{
					float scale =
						MaxCorrection /
						correctionLength;

					deltaX[i] *=
						scale;

					deltaY[i] *=
						scale;
				}
			}

			// --------------------------------------------------------
			// Apply corrections.
			// --------------------------------------------------------

			for (int i = 0; i < count; i++)
			{
				particles.PredX[i] +=
					deltaX[i];

				particles.PredY[i] +=
					deltaY[i];
			}

			// Keep predicted positions inside the container.
			ConstrainToBounds(particles);

			long phaseBEnd =
				Stopwatch.GetTimestamp();

			accumPhaseBMs +=
				(phaseBEnd - phaseBStart) *
				1000.0 /
				Stopwatch.Frequency;
		}
// ============================================================
// XSPH viscosity
// Smooth velocity differences between nearby particles.
// ============================================================

for (int i = 0; i < count; i++)
{
	float oldX = particles.PosX[i];
	float oldY = particles.PosY[i];

	float velocityX =
		(particles.PredX[i] - oldX) / dt;

	float velocityY =
		(particles.PredY[i] - oldY) / dt;

	float correctionX = 0.0f;
	float correctionY = 0.0f;

	int neighborCount = neighborCounts[i];
	int offset = neighborOffsets[i];

	for (int n = 0; n < neighborCount; n++)
	{
		int j = neighborBuffer[offset + n];

		if (j == i)
			continue;

		float dx =
			particles.PredX[i] -
			particles.PredX[j];

		float dy =
			particles.PredY[i] -
			particles.PredY[j];

		float distanceSquared =
			dx * dx +
			dy * dy;

		if (distanceSquared <= 0.000001f ||
			distanceSquared >= SmoothingRadius * SmoothingRadius)
		{
			continue;
		}

		float distance =
			Mathf.Sqrt(distanceSquared);

		float q =
			1.0f -
			distance / SmoothingRadius;

		float neighborVelocityX =
			(particles.PredX[j] -
			 particles.PosX[j]) / dt;

		float neighborVelocityY =
			(particles.PredY[j] -
			 particles.PosY[j]) / dt;

		correctionX +=
			(neighborVelocityX - velocityX) *
			q;

		correctionY +=
			(neighborVelocityY - velocityY) *
			q;
	}

	viscosityX[i] =
		correctionX * Viscosity;

	viscosityY[i] =
		correctionY * Viscosity;
}


// ============================================================
// Surface tension / cohesion
//
// Estimate the surface normal from the local particle
// distribution.
//
// For a particle in the middle of the fluid, neighbors
// surround it and the vectors cancel out.
//
// For a particle on the surface, neighbors exist mostly
// on one side, producing a normal pointing outward.
//
// We then apply a small velocity correction back toward
// the fluid.
// ============================================================

for (int i = 0; i < count; i++)
{
	float px = particles.PredX[i];
	float py = particles.PredY[i];

	int neighborCount = neighborCounts[i];
	int offset = neighborOffsets[i];

	float normalX = 0.0f;
	float normalY = 0.0f;

	for (int n = 0; n < neighborCount; n++)
	{
		int j = neighborBuffer[offset + n];

		if (j == i)
			continue;

		float dx =
			px -
			particles.PredX[j];

		float dy =
			py -
			particles.PredY[j];

		float distanceSquared =
			dx * dx +
			dy * dy;

		if (distanceSquared <= 0.000001f ||
			distanceSquared >=
				SmoothingRadius * SmoothingRadius)
		{
			continue;
		}

		float distance =
			Mathf.Sqrt(distanceSquared);

		float q =
			1.0f -
			distance / SmoothingRadius;

		// Direction from neighbor toward this particle.
		float nx =
			dx / distance;

		float ny =
			dy / distance;

		// Weight closer particles more strongly.
		float weight = q * q;

		normalX += nx * weight;
		normalY += ny * weight;
	}

	float normalLength =
		Mathf.Sqrt(
			normalX * normalX +
			normalY * normalY
		);

	if (normalLength < SurfaceThreshold)
	{
		SurfaceParticles[i] = false;
		surfaceVelocityX[i] = 0.0f;
		surfaceVelocityY[i] = 0.0f;
		continue;
	}
	SurfaceParticles[i] = true;
	
	// Normalize the outward surface normal.
	float surfaceNormalX =
		normalX / normalLength;

	float surfaceNormalY =
		normalY / normalLength;

	// The surface normal points OUT of the fluid.
	// Surface tension pulls back IN.
	float forceX =
		-surfaceNormalX * SurfaceTension;

	float forceY =
		-surfaceNormalY * SurfaceTension;

	// Convert the force to a velocity change.
	float velocityChangeX =
		forceX * dt;

	float velocityChangeY =
		forceY * dt;

	// Safety clamp.
	float velocityChangeLength =
		Mathf.Sqrt(
			velocityChangeX * velocityChangeX +
			velocityChangeY * velocityChangeY
		);

	if (velocityChangeLength >
		MaxSurfaceVelocity)
	{
		float scale =
			MaxSurfaceVelocity /
			velocityChangeLength;

		velocityChangeX *= scale;
		velocityChangeY *= scale;
	}

	surfaceVelocityX[i] =
		velocityChangeX;

	surfaceVelocityY[i] =
		velocityChangeY;
}


		// ============================================================
		// 3. Update velocities from corrected positions
		// ============================================================

		for (int i = 0; i < count; i++)
		{
			float oldX =
				particles.PosX[i];

			float oldY =
				particles.PosY[i];

			float newVelX =
				(particles.PredX[i] -
				 oldX) /
				dt;

			float newVelY =
				(particles.PredY[i] -
				 oldY) /
				dt;

			// Non-bouncy boundaries.
			if (particles.PredX[i] <= MinX ||
				particles.PredX[i] >= MaxX)
			{
				newVelX = 0.0f;
			}

			if (particles.PredY[i] <= MinY ||
				particles.PredY[i] >= MaxY)
			{
				newVelY = 0.0f;
			}

		particles.VelX[i] =
			newVelX +
			viscosityX[i] +
			surfaceVelocityX[i];

		particles.VelY[i] =
			newVelY +
			viscosityY[i] +
			surfaceVelocityY[i];

			particles.PosX[i] =
				particles.PredX[i];

			particles.PosY[i] =
				particles.PredY[i];
		}

		// ============================================================
		// Profiler
		// ============================================================

		long totalEnd =
			Stopwatch.GetTimestamp();

		accumTotalMs +=
			(totalEnd - totalStart) *
			1000.0 /
			Stopwatch.Frequency;

		profilerFrames++;

		if (profilerFrames >=
			ProfilerPrintInterval)
		{
			double build =
				accumBuildMs /
				profilerFrames;

			double phaseA =
				accumPhaseAMs /
				profilerFrames;

			double phaseB =
				accumPhaseBMs /
				profilerFrames;

			double total =
				accumTotalMs /
				profilerFrames;

			GD.Print(
$"PBF profiler " +
$"(avg ms over {profilerFrames} frames): " +
$"Particles={count} " +
$"Build={build}ms " +
$"PhaseA={phaseA}ms " +
$"PhaseB={phaseB}ms " +
$"Total={total}ms " +
$"(MaxNeighbors={MaxNeighbors})"
);

			profilerFrames = 0;

			accumBuildMs = 0.0;
			accumPhaseAMs = 0.0;
			accumPhaseBMs = 0.0;
			accumTotalMs = 0.0;
		}
			}

	// ================================================================
	// Buffer management
	// ================================================================

	private void EnsureBuffers(int count)
	{
		if (lambdas != null &&
			lambdas.Length >= count)
		{
			return;
		}
		viscosityX = new float[count];
		viscosityY = new float[count];
		
		surfaceVelocityX = new float[count];
		surfaceVelocityY = new float[count];
		
		SurfaceParticles = new bool[count];
		
		lambdas = new float[count];

		deltaX = new float[count];

		deltaY = new float[count];

		gradientSumX = new float[count];

		gradientSumY = new float[count];

		gradientSumSquared = new float[count];

		neighborOffsets = new int[count];

		neighborCounts = new int[count];

		neighborBuffer = new int[Math.Max(1,count * 8)];
	}

	private void EnsureNeighborCapacity(
		int required)
	{
		if (neighborBuffer.Length >=
			required)
		{
			return;
		}

		int newCapacity =
			Math.Max(
				neighborBuffer.Length * 2,
				required
			);

		int[] newBuffer =
			new int[newCapacity];

		Array.Copy(
			neighborBuffer,
			newBuffer,
			neighborBuffer.Length
		);

		neighborBuffer =
			newBuffer;
	}

	// ================================================================
	// Boundary constraints
	// ================================================================

	private void ConstrainToBounds(
		ParticleData particles)
	{
		for (int i = 0;
			 i < particles.Count;
			 i++)
		{
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

public void BuildDensityField(
	ParticleData particles,
	DensityField field)
{
	field.Clear();

	int count = particles.Count;

	float h = SmoothingRadius;

	float hSquared =
		h * h;

	for (int i = 0; i < count; i++)
	{
		float px =
			particles.PredX[i];

		float py =
			particles.PredY[i];

		int neighborCount =
			neighborCounts[i];

		int offset =
			neighborOffsets[i];

		for (int n = 0;
			 n < neighborCount;
			 n++)
		{
			int j =
				neighborBuffer[offset + n];

			float dx =
				px -
				particles.PredX[j];

			float dy =
				py -
				particles.PredY[j];

			float distanceSquared =
				dx * dx +
				dy * dy;

			if (distanceSquared >= hSquared)
				continue;

			float distance =
				Mathf.Sqrt(distanceSquared);

			float q =
				1.0f -
				distance / h;

			float density =
				q * q * q;

			field.AddDensity(
				px,
				py,
				density
			);
		}
	}
}

}
