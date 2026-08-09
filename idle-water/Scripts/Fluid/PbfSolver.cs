
using Godot;
using System;
using System.Diagnostics;

public class PbfSolver
{
	private readonly SpatialHash hash;

	// ============================================================
	// Simulation parameters
	// ============================================================

	private const float Gravity = 200.0f;

	private const float SmoothingRadius = 12.0f;

	private const float SmoothingRadiusSquared =
		SmoothingRadius * SmoothingRadius;

	private const float Viscosity = 0.4f;

	private const float SurfaceTension = 18.0f;

	private const float SurfaceThreshold = 0.35f;

	private const float MaxSurfaceVelocity = 25.0f;

	private const float RestDensity = 1.15f;

	private const float LambdaEpsilon = 0.00001f;

	private const int Iterations = 2;

	private const float MaxCorrection = 1.5f;

	// ============================================================
	// Simulation boundaries
	// ============================================================

	private const float MinX = 24.0f;
	private const float MaxX = 696.0f;

	private const float MinY = 24.0f;
	private const float MaxY = 1256.0f;

	// ============================================================
	// Neighbor limit
	// ============================================================

	private const int MaxNeighbors = 32;

	// ============================================================
	// PBF working arrays
	// ============================================================

	private float[] lambdas;

	private float[] deltaX;
	private float[] deltaY;

	private float[] viscosityX;
	private float[] viscosityY;

	private float[] surfaceVelocityX;
	private float[] surfaceVelocityY;

	public bool[] SurfaceParticles;

	// ============================================================
	// Neighbor cache
	// ============================================================

	private int[] neighborBuffer;
	private int[] neighborOffsets;
	private int[] neighborCounts;

	private float[] neighborDx;
	private float[] neighborDy;
	private float[] neighborDistance;
	private float[] neighborQ;

	// ============================================================
	// Density cache
	// ============================================================

	private float[] particleDensity;

	// ============================================================
	// Profiler
	// ============================================================

	private const int ProfilerPrintInterval = 60;

	private int profilerFrames;

	private double accumBuildMs;
	private double accumPhaseAMs;
	private double accumPhaseBMs;
	private double accumTotalMs;

	public PbfSolver(
		SpatialHash spatialHash)
	{
		hash = spatialHash;
	}

	// ============================================================
	// Main solver
	// ============================================================

	public void Solve(
		ParticleData particles,
		float dt)
	{
		int count =
			particles.Count;

		if (count <= 0)
			return;

		EnsureBuffers(count);

		long totalStart =
			Stopwatch.GetTimestamp();

		// ========================================================
		// 1. Predict positions
		// ========================================================

		for (int i = 0;
			 i < count;
			 i++)
		{
			particles.VelY[i] +=
				Gravity * dt;

			particles.PredX[i] =
				particles.PosX[i] +
				particles.VelX[i] * dt;

			particles.PredY[i] =
				particles.PosY[i] +
				particles.VelY[i] * dt;
		}

		// ========================================================
		// 2. PBF
		// ========================================================

		for (int iteration = 0;
			 iteration < Iterations;
			 iteration++)
		{
			// ====================================================
			// Build spatial hash
			// ====================================================

			long buildStart =
				Stopwatch.GetTimestamp();

			hash.Clear();

			for (int i = 0;
				 i < count;
				 i++)
			{
				hash.Insert(
					i,
					particles.PredX[i],
					particles.PredY[i]
				);
			}

			long buildEnd =
				Stopwatch.GetTimestamp();

			accumBuildMs +=
				(buildEnd - buildStart) *
				1000.0 /
				Stopwatch.Frequency;

			// ====================================================
			// Build neighbor cache
			// ====================================================

			int bufferWritePosition =
				0;

			for (int i = 0;
				 i < count;
				 i++)
			{
				float px =
					particles.PredX[i];

				float py =
					particles.PredY[i];

				// ------------------------------------------------
				// Ask SpatialHash for ONLY actual neighbors.
				// Maximum is already enforced here.
				// ------------------------------------------------

				int neighborCount =
					hash.Query(
						px,
						py,
						SmoothingRadius,
						particles.PredX,
						particles.PredY,
						neighborBuffer,
						bufferWritePosition,
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

				// ------------------------------------------------
				// Cache geometry.
				// ------------------------------------------------

				for (int n = 0;
					 n < neighborCount;
					 n++)
				{
					int index =
						bufferWritePosition + n;

					int j =
						neighborBuffer[index];

					float dx =
						px -
						particles.PredX[j];

					float dy =
						py -
						particles.PredY[j];

					float distanceSquared =
						dx * dx +
						dy * dy;

					float distance;

					if (distanceSquared <=
						0.000001f)
					{
						distance = 0.0f;
					}
					else
					{
						distance =
							Mathf.Sqrt(
								distanceSquared
							);
					}

					neighborDx[index] =
						dx;

					neighborDy[index] =
						dy;

					neighborDistance[index] =
						distance;

					float q =
						1.0f -
						distance /
						SmoothingRadius;

					neighborQ[index] =
						q;
				}

				bufferWritePosition +=
					neighborCount;
			}

			// ====================================================
			// Phase A
			//
			// Density + complete PBF gradient.
			// ====================================================

			long phaseAStart =
				Stopwatch.GetTimestamp();

			for (int i = 0;
				 i < count;
				 i++)
			{
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

				for (int n = 0;
					 n < neighborCount;
					 n++)
				{
					int index =
						offset + n;

					int j =
						neighborBuffer[index];

					float q =
						neighborQ[index];

					// Density.
					density +=
						q * q * q;

					if (j == i)
						continue;

					float distance =
						neighborDistance[index];

					if (distance <=
						0.000001f)
					{
						continue;
					}

					float gradientMagnitude =
						-3.0f *
						q * q /
						SmoothingRadius;

					float invDistance =
						1.0f /
						distance;

					float nx =
						neighborDx[index] *
						invDistance;

					float ny =
						neighborDy[index] *
						invDistance;

					float gx =
						gradientMagnitude *
						nx /
						RestDensity;

					float gy =
						gradientMagnitude *
						ny /
						RestDensity;

					gradSumX +=
						gx;

					gradSumY +=
						gy;

					neighborGradientSquared +=
						gx * gx +
						gy * gy;
				}

				particleDensity[i] =
					density;

				float constraint =
					density /
					RestDensity -
					1.0f;

				float denominator =
					gradSumX * gradSumX +
					gradSumY * gradSumY +
					neighborGradientSquared;

				lambdas[i] =
					-constraint /
					(
						denominator +
						LambdaEpsilon
					);
			}

			long phaseAEnd =
				Stopwatch.GetTimestamp();

			accumPhaseAMs +=
				(phaseAEnd - phaseAStart) *
				1000.0 /
				Stopwatch.Frequency;

			// ====================================================
			// Phase B
			// ====================================================

			long phaseBStart =
				Stopwatch.GetTimestamp();

			for (int i = 0;
				 i < count;
				 i++)
			{
				float correctionX =
					0.0f;

				float correctionY =
					0.0f;

				int neighborCount =
					neighborCounts[i];

				int offset =
					neighborOffsets[i];

				for (int n = 0;
					 n < neighborCount;
					 n++)
				{
					int index =
						offset + n;

					int j =
						neighborBuffer[index];

					if (j == i)
						continue;

					float distance =
						neighborDistance[index];

					if (distance <=
						0.000001f)
					{
						continue;
					}

					float q =
						neighborQ[index];

					float gradientMagnitude =
						-3.0f *
						q * q /
						SmoothingRadius;

					float invDistance =
						1.0f /
						distance;

					float gradientX =
						gradientMagnitude *
						neighborDx[index] *
						invDistance /
						RestDensity;

					float gradientY =
						gradientMagnitude *
						neighborDy[index] *
						invDistance /
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

				float correctionLengthSquared =
					correctionX * correctionX +
					correctionY * correctionY;

				if (correctionLengthSquared >
					MaxCorrection *
					MaxCorrection)
				{
					float correctionLength =
						Mathf.Sqrt(
							correctionLengthSquared
						);

					float scale =
						MaxCorrection /
						correctionLength;

					correctionX *=
						scale;

					correctionY *=
						scale;
				}

				deltaX[i] =
					correctionX;

				deltaY[i] =
					correctionY;
			}

			// ----------------------------------------------------
			// Apply corrections.
			// ----------------------------------------------------

			for (int i = 0;
				 i < count;
				 i++)
			{
				particles.PredX[i] +=
					deltaX[i];

				particles.PredY[i] +=
					deltaY[i];
			}

			ConstrainToBounds(
				particles
			);

			long phaseBEnd =
				Stopwatch.GetTimestamp();

			accumPhaseBMs +=
				(phaseBEnd - phaseBStart) *
				1000.0 /
				Stopwatch.Frequency;
		}

		// ========================================================
		// 3. Viscosity + surface normal
		//
		// One neighbor pass.
		// ========================================================

		for (int i = 0;
			 i < count;
			 i++)
		{
			float oldX =
				particles.PosX[i];

			float oldY =
				particles.PosY[i];

			float velocityX =
				(particles.PredX[i] -
				 oldX) /
				dt;

			float velocityY =
				(particles.PredY[i] -
				 oldY) /
				dt;

			float viscosityCorrectionX =
				0.0f;

			float viscosityCorrectionY =
				0.0f;

			float normalX =
				0.0f;

			float normalY =
				0.0f;

			int neighborCount =
				neighborCounts[i];

			int offset =
				neighborOffsets[i];

			for (int n = 0;
				 n < neighborCount;
				 n++)
			{
				int index =
					offset + n;

				int j =
					neighborBuffer[index];

				if (j == i)
					continue;

				float distance =
					neighborDistance[index];

				if (distance <=
					0.000001f)
				{
					continue;
				}

				float q =
					neighborQ[index];

				// ------------------------------------------------
				// Viscosity
				// ------------------------------------------------

				float neighborVelocityX =
					(
						particles.PredX[j] -
						particles.PosX[j]
					) / dt;

				float neighborVelocityY =
					(
						particles.PredY[j] -
						particles.PosY[j]
					) / dt;

				viscosityCorrectionX +=
					(
						neighborVelocityX -
						velocityX
					) * q;

				viscosityCorrectionY +=
					(
						neighborVelocityY -
						velocityY
					) * q;

				// ------------------------------------------------
				// Surface normal
				// ------------------------------------------------

				float weight =
					q * q;

				float invDistance =
					1.0f /
					distance;

				float nx =
					neighborDx[index] *
					invDistance;

				float ny =
					neighborDy[index] *
					invDistance;

				normalX +=
					nx * weight;

				normalY +=
					ny * weight;
			}

			viscosityX[i] =
				viscosityCorrectionX *
				Viscosity;

			viscosityY[i] =
				viscosityCorrectionY *
				Viscosity;

			// ----------------------------------------------------
			// Surface tension
			// ----------------------------------------------------

			float normalLengthSquared =
				normalX * normalX +
				normalY * normalY;

			if (normalLengthSquared <
				SurfaceThreshold *
				SurfaceThreshold)
			{
				SurfaceParticles[i] =
					false;

				surfaceVelocityX[i] =
					0.0f;

				surfaceVelocityY[i] =
					0.0f;

				continue;
			}

			SurfaceParticles[i] =
				true;

			float normalLength =
				Mathf.Sqrt(
					normalLengthSquared
				);

			float surfaceNormalX =
				normalX /
				normalLength;

			float surfaceNormalY =
				normalY /
				normalLength;

			float velocityChangeX =
				-surfaceNormalX *
				SurfaceTension *
				dt;

			float velocityChangeY =
				-surfaceNormalY *
				SurfaceTension *
				dt;

			float velocityChangeLengthSquared =
				velocityChangeX *
				velocityChangeX +
				velocityChangeY *
				velocityChangeY;

			if (velocityChangeLengthSquared >
				MaxSurfaceVelocity *
				MaxSurfaceVelocity)
			{
				float velocityChangeLength =
					Mathf.Sqrt(
						velocityChangeLengthSquared
					);

				float scale =
					MaxSurfaceVelocity /
					velocityChangeLength;

				velocityChangeX *=
					scale;

				velocityChangeY *=
					scale;
			}

			surfaceVelocityX[i] =
				velocityChangeX;

			surfaceVelocityY[i] =
				velocityChangeY;
		}

		// ========================================================
		// 4. Update velocities
		// ========================================================

		for (int i = 0;
			 i < count;
			 i++)
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

			if (particles.PredX[i] <= MinX ||
				particles.PredX[i] >= MaxX)
			{
				newVelX =
					0.0f;
			}

			if (particles.PredY[i] <= MinY ||
				particles.PredY[i] >= MaxY)
			{
				newVelY =
					0.0f;
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

		// ========================================================
		// Profiler
		// ========================================================

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
				$"Build={build:F2}ms " +
				$"PhaseA={phaseA:F2}ms " +
				$"PhaseB={phaseB:F2}ms " +
				$"Total={total:F2}ms " +
				$"(MaxNeighbors={MaxNeighbors})"
			);

			profilerFrames = 0;

			accumBuildMs = 0.0;
			accumPhaseAMs = 0.0;
			accumPhaseBMs = 0.0;
			accumTotalMs = 0.0;
		}
	}

	// ============================================================
	// Buffer management
	// ============================================================

	private void EnsureBuffers(
		int count)
	{
		if (lambdas != null &&
			lambdas.Length >= count)
		{
			return;
		}

		lambdas =
			new float[count];

		deltaX =
			new float[count];

		deltaY =
			new float[count];

		viscosityX =
			new float[count];

		viscosityY =
			new float[count];

		surfaceVelocityX =
			new float[count];

		surfaceVelocityY =
			new float[count];

		SurfaceParticles =
			new bool[count];

		particleDensity =
			new float[count];

		neighborOffsets =
			new int[count];

		neighborCounts =
			new int[count];

		int initialCapacity =
			Math.Max(
				1,
				count * MaxNeighbors
			);

		neighborBuffer =
			new int[initialCapacity];

		neighborDx =
			new float[initialCapacity];

		neighborDy =
			new float[initialCapacity];

		neighborDistance =
			new float[initialCapacity];

		neighborQ =
			new float[initialCapacity];
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

		int[] newNeighborBuffer =
			new int[newCapacity];

		float[] newNeighborDx =
			new float[newCapacity];

		float[] newNeighborDy =
			new float[newCapacity];

		float[] newNeighborDistance =
			new float[newCapacity];

		float[] newNeighborQ =
			new float[newCapacity];

		Array.Copy(
			neighborBuffer,
			newNeighborBuffer,
			neighborBuffer.Length
		);

		Array.Copy(
			neighborDx,
			newNeighborDx,
			neighborDx.Length
		);

		Array.Copy(
			neighborDy,
			newNeighborDy,
			neighborDy.Length
		);

		Array.Copy(
			neighborDistance,
			newNeighborDistance,
			neighborDistance.Length
		);

		Array.Copy(
			neighborQ,
			newNeighborQ,
			neighborQ.Length
		);

		neighborBuffer =
			newNeighborBuffer;

		neighborDx =
			newNeighborDx;

		neighborDy =
			newNeighborDy;

		neighborDistance =
			newNeighborDistance;

		neighborQ =
			newNeighborQ;
	}

	// ============================================================
	// Boundary constraints
	// ============================================================

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

	// ============================================================
	// Density field support
	// ============================================================

	public void BuildDensityField(
		ParticleData particles,
		DensityField field)
	{
		field.Clear();

		int count =
			particles.Count;

		for (int i = 0;
			 i < count;
			 i++)
		{
			float density =
				particleDensity[i];

			if (density <= 0.0f)
				continue;

			field.AddDensity(
				particles.PredX[i],
				particles.PredY[i],
				density
			);
		}
	}
}
