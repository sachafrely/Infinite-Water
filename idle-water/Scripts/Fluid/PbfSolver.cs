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

	// ------------------------------------------------------------
	// Density
	// ------------------------------------------------------------

	private const float RestDensity = 1.15f;

	private const float LambdaEpsilon = 0.00001f;

	// ------------------------------------------------------------
	// Adaptive PBF iterations
	// ------------------------------------------------------------

	private const int MinIterations = 2;
	private const int MaxIterations = 4;

	private const float DensityErrorThreshold = 0.035f;

	private const float MaxCorrection = 1.5f;

	// ============================================================
	// Artificial pressure / tensile instability
	// ============================================================

	private const float ArtificialPressureStrength = 0.0002f;

	private const float ArtificialPressureExponent = 4.0f;

	private const float ArtificialPressureReferenceDistance =
		0.10f;

	private const float ArtificialPressureReferenceQ =
		1.0f -
		ArtificialPressureReferenceDistance;

	private static readonly float ArtificialPressureReferenceKernel =
		ArtificialPressureReferenceQ *
		ArtificialPressureReferenceQ *
		ArtificialPressureReferenceQ;

	// ============================================================
	// XSPH viscosity
	// ============================================================

	private const float Viscosity = 0.9f;

	// ============================================================
	// Vorticity confinement
	// ============================================================

	private const float VorticityStrength = 0.35f;

	private const float MaxVorticityVelocity = 18.0f;

	// ============================================================
	// Surface detection / tension
	// ============================================================

	private const float SurfaceThreshold = 0.35f;

	private const float SurfaceTension = 4.0f;

	private const float MaxSurfaceVelocity = 200.0f;

	// ============================================================
	// Velocity damping
	// ============================================================

	private const float VelocityDamping = 0.996f;

	// ============================================================
	// Particle sleeping
	//
	// A particle does not immediately stop when its velocity
	// becomes small.
	//
	// Instead:
	//
	// 1. Velocity falls below SleepVelocityThreshold.
	// 2. Sleep progress gradually increases.
	// 3. Velocity is progressively damped.
	// 4. After SleepTime the particle becomes fully asleep.
	//
	// Sleeping particles do NOT receive gravity.
	//
	// WakeVelocityThreshold is slightly higher than the sleep
	// threshold to prevent rapid sleep/wake oscillation.
	// ============================================================

	private const float SleepVelocityThreshold = 2.0f;

	private const float WakeVelocityThreshold = 4.0f;

	private const float SleepTime = 0.35f;

	private const float SleepDampingStrength = 3.0f;

	// ============================================================
	// Boundary collision
	//
	// BoundarySkin:
	// Keeps particle centers slightly inside the simulation wall.
	//
	// BoundaryRestitution:
	// Very low because we want water to behave softly.
	//
	// BoundaryFriction:
	// Removes some tangential velocity when touching a wall.
	//
	// BoundaryVelocityEpsilon:
	// Prevents tiny numerical velocities from causing jitter.
	// ============================================================

	private const float MinX = 24.0f;
	private const float MaxX = 696.0f;

	private const float MinY = 24.0f;
	private const float MaxY = 1256.0f;

	private const float BoundarySkin = 0.5f;

	private const float BoundaryRestitution = 0.03f;

	private const float BoundaryFriction = 0.03f;

	private const float BoundaryVelocityEpsilon = 0.5f;

	// ============================================================
	// Neighbor limit
	// ============================================================

	private const int MaxNeighbors = 64;

	// ============================================================
	// Working arrays
	// ============================================================

	private float[] lambdas;

	private float[] deltaX;
	private float[] deltaY;

	private float[] viscosityX;
	private float[] viscosityY;

	private float[] surfaceVelocityX;
	private float[] surfaceVelocityY;

	private float[] vorticityVelocityX;
	private float[] vorticityVelocityY;

	private float[] vorticity;

	private float[] vorticityMagnitude;

	private float[] particleDensity;

	// ------------------------------------------------------------
	// Particle sleep state
	// ------------------------------------------------------------

	private float[] sleepProgress;

	private bool[] sleeping;

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
	// Profiler
	// ============================================================

	private const int ProfilerPrintInterval = 60;

	private int profilerFrames;

	private double accumBuildMs;
	private double accumPhaseAMs;
	private double accumPhaseBMs;
	private double accumVelocityMs;
	private double accumTotalMs;

	private int accumIterations;

	private float maxObservedDensityError;

	// ============================================================
	// Constructor
	// ============================================================

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

		if (dt <= 0.0f)
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
			// ----------------------------------------------------
			// Sleeping particle
			//
			// A sleeping particle stays exactly where it is.
			// Gravity is deliberately NOT applied.
			// ----------------------------------------------------

			if (sleeping[i])
			{
				particles.VelX[i] =
					0.0f;

				particles.VelY[i] =
					0.0f;

				particles.PredX[i] =
					particles.PosX[i];

				particles.PredY[i] =
					particles.PosY[i];

				continue;
			}

			// ----------------------------------------------------
			// Normal prediction
			// ----------------------------------------------------

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
		// 2. Adaptive PBF
		// ========================================================

		int iterationsUsed =
			0;

		float frameDensityError =
			float.MaxValue;

		for (int iteration = 0;
			 iteration < MaxIterations;
			 iteration++)
		{
			iterationsUsed++;

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

			BuildNeighborCache(
				particles,
				count
			);

			// ====================================================
			// Phase A
			//
			// Density + lambda
			// ====================================================

			long phaseAStart =
				Stopwatch.GetTimestamp();

			frameDensityError =
				CalculateLambdas(
					count
				);

			long phaseAEnd =
				Stopwatch.GetTimestamp();

			accumPhaseAMs +=
				(phaseAEnd - phaseAStart) *
				1000.0 /
				Stopwatch.Frequency;

			if (frameDensityError >
				maxObservedDensityError)
			{
				maxObservedDensityError =
					frameDensityError;
			}

			// ====================================================
			// Phase B
			//
			// PBF pressure + artificial pressure
			// ====================================================

			long phaseBStart =
				Stopwatch.GetTimestamp();

			CalculatePositionCorrections(
				count
			);

			for (int i = 0;
				 i < count;
				 i++)
			{
				particles.PredX[i] +=
					deltaX[i];

				particles.PredY[i] +=
					deltaY[i];
			}

			// ----------------------------------------------------
			// Boundary positional constraint
			//
			// This happens every PBF iteration so pressure
			// corrections can never push particles outside.
			// ----------------------------------------------------

			ConstrainToBounds(
				particles
			);

			long phaseBEnd =
				Stopwatch.GetTimestamp();

			accumPhaseBMs +=
				(phaseBEnd - phaseBStart) *
				1000.0 /
				Stopwatch.Frequency;

			if (iteration + 1 >=
				MinIterations &&
				frameDensityError <=
				DensityErrorThreshold)
			{
				break;
			}
		}

		accumIterations +=
			iterationsUsed;

		// ========================================================
		// 3. Velocity effects
		//
		// Final neighbor cache is reused.
		// ========================================================

		long velocityStart =
			Stopwatch.GetTimestamp();

		CalculateVelocityEffects(
			particles,
			dt,
			count
		);

		long velocityEnd =
			Stopwatch.GetTimestamp();

		accumVelocityMs +=
			(velocityEnd - velocityStart) *
			1000.0 /
			Stopwatch.Frequency;

		// ========================================================
		// 4. Update velocities + positions
		// ========================================================

		for (int i = 0;
			 i < count;
			 i++)
		{
			// ----------------------------------------------------
			// Already sleeping
			// ----------------------------------------------------

			if (sleeping[i])
			{
				particles.VelX[i] =
					0.0f;

				particles.VelY[i] =
					0.0f;

				particles.PosX[i] =
					particles.PredX[i];

				particles.PosY[i] =
					particles.PredY[i];

				continue;
			}

			float oldX =
				particles.PosX[i];

			float oldY =
				particles.PosY[i];

			float newVelocityX =
				(particles.PredX[i] -
				 oldX) /
				dt;

			float newVelocityY =
				(particles.PredY[i] -
				 oldY) /
				dt;

			// ----------------------------------------------------
			// Combine all velocity effects FIRST.
			//
			// Boundary response happens after this.
			// ----------------------------------------------------

			float finalVelocityX =
				newVelocityX +
				viscosityX[i] +
				surfaceVelocityX[i] +
				vorticityVelocityX[i];

			float finalVelocityY =
				newVelocityY +
				viscosityY[i] +
				surfaceVelocityY[i] +
				vorticityVelocityY[i];

			// ----------------------------------------------------
			// Velocity damping
			// ----------------------------------------------------

			finalVelocityX *=
				VelocityDamping;

			finalVelocityY *=
				VelocityDamping;

			// ----------------------------------------------------
			// Boundary response
			//
			// Apply this LAST so no later force can push the
			// particle back into the wall during this frame.
			// ----------------------------------------------------

			ApplyBoundaryVelocityResponse(
				particles,
				i,
				ref finalVelocityX,
				ref finalVelocityY
			);

			// ----------------------------------------------------
			// Particle sleeping
			//
			// This happens after all velocity effects have been
			// combined, so the sleep decision is based on the
			// particle's actual final velocity.
			// ----------------------------------------------------

			ApplySleepBehavior(
				particles,
				i,
				dt,
				ref finalVelocityX,
				ref finalVelocityY
			);

			particles.VelX[i] =
				finalVelocityX;

			particles.VelY[i] =
				finalVelocityY;

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

			double velocity =
				accumVelocityMs /
				profilerFrames;

			double total =
				accumTotalMs /
				profilerFrames;

			double averageIterations =
				(double)accumIterations /
				profilerFrames;

			GD.Print(
				$"PBF profiler " +
				$"(avg ms over {profilerFrames} frames): " +
				$"Particles={count} " +
				$"Build={build:F2}ms " +
				$"PhaseA={phaseA:F2}ms " +
				$"PhaseB={phaseB:F2}ms " +
				$"Velocity={velocity:F2}ms " +
				$"Total={total:F2}ms " +
				$"Iterations={averageIterations:F2} " +
				$"MaxDensityError={maxObservedDensityError:F4} " +
				$"(MaxNeighbors={MaxNeighbors})"
			);

			profilerFrames =
				0;

			accumBuildMs =
				0.0;

			accumPhaseAMs =
				0.0;

			accumPhaseBMs =
				0.0;

			accumVelocityMs =
				0.0;

			accumTotalMs =
				0.0;

			accumIterations =
				0;

			maxObservedDensityError =
				0.0f;
		}
	}

	// ============================================================
	// Particle sleeping
	// ============================================================

	private void ApplySleepBehavior(
		ParticleData particles,
		int i,
		float dt,
		ref float velocityX,
		ref float velocityY)
	{
		float velocitySquared =
			velocityX * velocityX +
			velocityY * velocityY;

		float sleepThresholdSquared =
			SleepVelocityThreshold *
			SleepVelocityThreshold;

		float wakeThresholdSquared =
			WakeVelocityThreshold *
			WakeVelocityThreshold;

		// --------------------------------------------------------
		// Fast particle
		//
		// Completely reset sleep progress.
		// --------------------------------------------------------

		if (velocitySquared >=
			wakeThresholdSquared)
		{
			sleepProgress[i] =
				0.0f;

			sleeping[i] =
				false;

			return;
		}

		// --------------------------------------------------------
		// Slow particle
		//
		// Gradually move toward sleep.
		// --------------------------------------------------------

		if (velocitySquared <
			sleepThresholdSquared)
		{
			sleepProgress[i] +=
				dt /
				SleepTime;

			sleepProgress[i] =
				Mathf.Clamp(
					sleepProgress[i],
					0.0f,
					1.0f
				);

			// ----------------------------------------------------
			// Gradually increase damping as the particle
			// approaches the sleeping state.
			// ----------------------------------------------------

			float sleepBlend =
				sleepProgress[i];

			float damping =
				1.0f -
				SleepDampingStrength *
				sleepBlend *
				dt;

			damping =
				Mathf.Clamp(
					damping,
					0.0f,
					1.0f
				);

			velocityX *=
				damping;

			velocityY *=
				damping;

			// ----------------------------------------------------
			// Fully asleep.
			// ----------------------------------------------------

			if (sleepProgress[i] >=
				1.0f)
			{
				sleeping[i] =
					true;

				velocityX =
					0.0f;

				velocityY =
					0.0f;
			}

			return;
		}

		// --------------------------------------------------------
		// Velocity is between sleep and wake thresholds.
		//
		// Slowly move back toward an active state instead of
		// immediately waking the particle.
		// --------------------------------------------------------

		sleepProgress[i] -=
			dt /
			SleepTime;

		sleepProgress[i] =
			Mathf.Clamp(
				sleepProgress[i],
				0.0f,
				1.0f
			);

		sleeping[i] =
			false;
	}

	// ============================================================
	// Build neighbor cache
	// ============================================================

	private void BuildNeighborCache(
		ParticleData particles,
		int count)
	{
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

			for (int n = 0;
				 n < neighborCount;
				 n++)
			{
				int index =
					bufferWritePosition +
					n;

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
					distance =
						0.0f;
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
					Mathf.Max(
						0.0f,
						q
					);
			}

			bufferWritePosition +=
				neighborCount;
		}
	}

	// ============================================================
	// Phase A
	//
	// Density + lambda
	// ============================================================

	private float CalculateLambdas(
		int count)
	{
		float maximumDensityError =
			0.0f;

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

				float q2 =
					q * q;

				float q3 =
					q2 * q;

				density +=
					q3;

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
					q2 /
					SmoothingRadius;

				float inverseDistance =
					1.0f /
					distance;

				float gradientScale =
					gradientMagnitude *
					inverseDistance /
					RestDensity;

				float gx =
					neighborDx[index] *
					gradientScale;

				float gy =
					neighborDy[index] *
					gradientScale;

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

			float absoluteConstraint =
				Mathf.Abs(
					constraint
				);

			if (absoluteConstraint >
				maximumDensityError)
			{
				maximumDensityError =
					absoluteConstraint;
			}

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

		return maximumDensityError;
	}

	// ============================================================
	// Phase B
	//
	// PBF correction + artificial pressure
	// ============================================================

	private void CalculatePositionCorrections(
		int count)
	{
		float maxCorrectionSquared =
			MaxCorrection *
			MaxCorrection;

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

			float lambdaI =
				lambdas[i];

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

				float q2 =
					q * q;

				float gradientMagnitude =
					-3.0f *
					q2 /
					SmoothingRadius;

				float inverseDistance =
					1.0f /
					distance;

				float gradientScale =
					gradientMagnitude *
					inverseDistance /
					RestDensity;

				float gradientX =
					neighborDx[index] *
					gradientScale;

				float gradientY =
					neighborDy[index] *
					gradientScale;

				float lambdaSum =
					lambdaI +
					lambdas[j];

				// ------------------------------------------------
				// Artificial pressure
				// ------------------------------------------------

				float kernelValue =
					q2 * q;

				float kernelRatio =
					kernelValue /
					ArtificialPressureReferenceKernel;

				kernelRatio =
					Mathf.Clamp(
						kernelRatio,
						0.0f,
						1.0f
					);

				float ratioSquared =
					kernelRatio *
					kernelRatio;

				float ratioPower =
					ratioSquared *
					ratioSquared;

				float artificialPressure =
					-ArtificialPressureStrength *
					ratioPower;

				float pressureTerm =
					lambdaSum +
					artificialPressure;

				correctionX +=
					pressureTerm *
					gradientX;

				correctionY +=
					pressureTerm *
					gradientY;
			}

			float correctionLengthSquared =
				correctionX * correctionX +
				correctionY * correctionY;

			if (correctionLengthSquared >
				maxCorrectionSquared)
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
	}

	// ============================================================
	// Velocity effects
	//
	// XSPH viscosity
	// Surface normal
	// Surface tension
	// Vorticity confinement
	// ============================================================

	private void CalculateVelocityEffects(
		ParticleData particles,
		float dt,
		int count)
	{
		// ========================================================
		// First pass:
		//
		// XSPH viscosity
		// surface normal
		// vorticity
		// ========================================================

		for (int i = 0;
			 i < count;
			 i++)
		{
			float velocityX =
				(particles.PredX[i] -
				 particles.PosX[i]) /
				dt;

			float velocityY =
				(particles.PredY[i] -
				 particles.PosY[i]) /
				dt;

			float viscosityCorrectionX =
				0.0f;

			float viscosityCorrectionY =
				0.0f;

			float normalX =
				0.0f;

			float normalY =
				0.0f;

			float localVorticity =
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
					continue;

				float q =
					neighborQ[index];

				float q2 =
					q * q;

				float inverseDistance =
					1.0f /
					distance;

				float nx =
					neighborDx[index] *
					inverseDistance;

				float ny =
					neighborDy[index] *
					inverseDistance;

				// ------------------------------------------------
				// Neighbor velocity
				// ------------------------------------------------

				float neighborVelocityX =
					(
						particles.PredX[j] -
						particles.PosX[j]
					) /
					dt;

				float neighborVelocityY =
					(
						particles.PredY[j] -
						particles.PosY[j]
					) /
					dt;

				// ------------------------------------------------
				// XSPH viscosity
				// ------------------------------------------------

				viscosityCorrectionX +=
					(
						neighborVelocityX -
						velocityX
					) *
					q2;

				viscosityCorrectionY +=
					(
						neighborVelocityY -
						velocityY
					) *
					q2;

				// ------------------------------------------------
				// Surface normal
				// ------------------------------------------------

				normalX +=
					nx *
					q2;

				normalY +=
					ny *
					q2;

				// ------------------------------------------------
				// Vorticity
				// ------------------------------------------------

				float velocityDifferenceX =
					neighborVelocityX -
					velocityX;

				float velocityDifferenceY =
					neighborVelocityY -
					velocityY;

				localVorticity +=
					(
						velocityDifferenceY *
						nx -
						velocityDifferenceX *
						ny
					) *
					q2;
			}

			viscosityX[i] =
				viscosityCorrectionX *
				Viscosity;

			viscosityY[i] =
				viscosityCorrectionY *
				Viscosity;

			vorticity[i] =
				localVorticity;

			vorticityMagnitude[i] =
				Mathf.Abs(
					localVorticity
				);

			// ----------------------------------------------------
			// Surface
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
			}
			else
			{
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

				// ------------------------------------------------
				// Surface tension
				// ------------------------------------------------

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

				float maxSurfaceVelocitySquared =
					MaxSurfaceVelocity *
					MaxSurfaceVelocity;

				if (velocityChangeLengthSquared >
					maxSurfaceVelocitySquared)
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
		}

		// ========================================================
		// Second pass:
		//
		// Vorticity confinement
		// ========================================================

		for (int i = 0;
			 i < count;
			 i++)
		{
			float gradientX =
				0.0f;

			float gradientY =
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
					continue;

				float q =
					neighborQ[index];

				float q2 =
					q * q;

				float inverseDistance =
					1.0f /
					distance;

				float nx =
					neighborDx[index] *
					inverseDistance;

				float ny =
					neighborDy[index] *
					inverseDistance;

				float vorticityDifference =
					vorticityMagnitude[j] -
					vorticityMagnitude[i];

				gradientX +=
					vorticityDifference *
					nx *
					q2;

				gradientY +=
					vorticityDifference *
					ny *
					q2;
			}

			float gradientLengthSquared =
				gradientX * gradientX +
				gradientY * gradientY;

			if (gradientLengthSquared <=
				0.000001f)
			{
				vorticityVelocityX[i] =
					0.0f;

				vorticityVelocityY[i] =
					0.0f;

				continue;
			}

			float gradientLength =
				Mathf.Sqrt(
					gradientLengthSquared
				);

			float normalX =
				gradientX /
				gradientLength;

			float normalY =
				gradientY /
				gradientLength;

			float localVorticity =
				vorticity[i];

			float forceX =
				normalY *
				localVorticity *
				VorticityStrength;

			float forceY =
				-normalX *
				localVorticity *
				VorticityStrength;

			float velocityLengthSquared =
				forceX * forceX +
				forceY * forceY;

			float maxVelocitySquared =
				MaxVorticityVelocity *
				MaxVorticityVelocity;

			if (velocityLengthSquared >
				maxVelocitySquared)
			{
				float velocityLength =
					Mathf.Sqrt(
						velocityLengthSquared
					);

				float scale =
					MaxVorticityVelocity /
					velocityLength;

				forceX *=
					scale;

				forceY *=
					scale;
			}

			vorticityVelocityX[i] =
				forceX *
				dt;

			vorticityVelocityY[i] =
				forceY *
				dt;
		}
	}

	// ============================================================
	// Boundary constraints
	//
	// Positional collision during PBF.
	// ============================================================

	private void ConstrainToBounds(
		ParticleData particles)
	{
		float leftBoundary =
			MinX +
			BoundarySkin;

		float rightBoundary =
			MaxX -
			BoundarySkin;

		float topBoundary =
			MinY +
			BoundarySkin;

		float bottomBoundary =
			MaxY -
			BoundarySkin;

		for (int i = 0;
			 i < particles.Count;
			 i++)
		{
			if (particles.PredX[i] <
				leftBoundary)
			{
				particles.PredX[i] =
					leftBoundary;
			}
			else if (particles.PredX[i] >
					 rightBoundary)
			{
				particles.PredX[i] =
					rightBoundary;
			}

			if (particles.PredY[i] <
				topBoundary)
			{
				particles.PredY[i] =
					topBoundary;
			}
			else if (particles.PredY[i] >
					 bottomBoundary)
			{
				particles.PredY[i] =
					bottomBoundary;
			}
		}
	}

	// ============================================================
	// Boundary velocity response
	//
	// This is deliberately handled after ALL other velocity
	// effects have been combined.
	//
	// The goal is:
	//
	// - no visible bouncing
	// - no particles entering walls
	// - no tiny jitter at rest
	// - some sliding along walls
	// - small amount of energy retained when hitting walls
	// ============================================================

	private void ApplyBoundaryVelocityResponse(
		ParticleData particles,
		int i,
		ref float velocityX,
		ref float velocityY)
	{
		float x =
			particles.PredX[i];

		float y =
			particles.PredY[i];

		float leftBoundary =
			MinX +
			BoundarySkin;

		float rightBoundary =
			MaxX -
			BoundarySkin;

		float topBoundary =
			MinY +
			BoundarySkin;

		float bottomBoundary =
			MaxY -
			BoundarySkin;

		// --------------------------------------------------------
		// Left wall
		// --------------------------------------------------------

		if (x <= leftBoundary +
			0.001f)
		{
			if (velocityX < 0.0f)
			{
				if (Mathf.Abs(velocityX) <
					BoundaryVelocityEpsilon)
				{
					velocityX =
						0.0f;
				}
				else
				{
					velocityX =
						-velocityX *
						BoundaryRestitution;
				}
			}

			velocityY *=
				1.0f -
				BoundaryFriction;
		}

		// --------------------------------------------------------
		// Right wall
		// --------------------------------------------------------

		if (x >= rightBoundary -
			0.001f)
		{
			if (velocityX > 0.0f)
			{
				if (Mathf.Abs(velocityX) <
					BoundaryVelocityEpsilon)
				{
					velocityX =
						0.0f;
				}
				else
				{
					velocityX =
						-velocityX *
						BoundaryRestitution;
				}
			}

			velocityY *=
				1.0f -
				BoundaryFriction;
		}

		// --------------------------------------------------------
		// Top wall
		// --------------------------------------------------------

		if (y <= topBoundary +
			0.001f)
		{
			if (velocityY < 0.0f)
			{
				if (Mathf.Abs(velocityY) <
					BoundaryVelocityEpsilon)
				{
					velocityY =
						0.0f;
				}
				else
				{
					velocityY =
						-velocityY *
						BoundaryRestitution;
				}
			}

			velocityX *=
				1.0f -
				BoundaryFriction;
		}

		// --------------------------------------------------------
		// Bottom wall
		// --------------------------------------------------------

		if (y >= bottomBoundary -
			0.001f)
		{
			if (velocityY > 0.0f)
			{
				if (Mathf.Abs(velocityY) <
					BoundaryVelocityEpsilon)
				{
					velocityY =
						0.0f;
				}
				else
				{
					velocityY =
						-velocityY *
						BoundaryRestitution;
				}
			}

			velocityX *=
				1.0f -
				BoundaryFriction;
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

		vorticityVelocityX =
			new float[count];

		vorticityVelocityY =
			new float[count];

		vorticity =
			new float[count];

		vorticityMagnitude =
			new float[count];

		SurfaceParticles =
			new bool[count];

		particleDensity =
			new float[count];

		// --------------------------------------------------------
		// Sleep buffers
		// --------------------------------------------------------

		sleepProgress =
			new float[count];

		sleeping =
			new bool[count];

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
