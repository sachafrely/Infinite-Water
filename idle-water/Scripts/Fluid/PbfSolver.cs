
using Godot;
using System;
using System.Diagnostics;
using System.Collections.Generic;

public class PbfSolver
{
	private readonly SpatialHash hash;
	private readonly List<FluidPolygonCollider> polygonColliders;

	// ============================================================
	// Simulation parameters
	// ============================================================

	private const float Gravity = 200.0f;

	private const float SmoothingRadius = 12.0f;
	private const float SmoothingRadiusSquared =
		SmoothingRadius * SmoothingRadius;

	// ============================================================
	// Density
	// ============================================================

	private const float RestDensity = 1.15f;
	private const float InverseRestDensity = 1.0f / RestDensity;

	private const float LambdaEpsilon = 0.00001f;

	// ============================================================
	// PBF iterations
	// ============================================================

	private const int MinIterations = 2;
	private const int MaxIterations = 4;

	private const float DensityErrorThreshold = 0.035f;

	// IMPORTANT:
	// Reduced substantially to prevent large particle jumps.
	private const float MaxCorrection = 0.5f;

	private const float MaxCorrectionSquared =
		MaxCorrection * MaxCorrection;

	// ============================================================
	// Artificial pressure
	//
	// Disabled for stability testing.
	// ============================================================

	private const float ArtificialPressureStrength = 0.0f;

	private const float ArtificialPressureReferenceDistance = 0.10f;

	private const float ArtificialPressureReferenceQ =
		1.0f - ArtificialPressureReferenceDistance;

	private static readonly float ArtificialPressureReferenceKernel =
		ArtificialPressureReferenceQ *
		ArtificialPressureReferenceQ *
		ArtificialPressureReferenceQ;

	private static readonly float ArtificialPressureReferenceKernelInverse =
		1.0f / ArtificialPressureReferenceKernel;

	// ============================================================
	// XSPH viscosity
	//
	// Disabled for stability testing.
	// ============================================================

	private const float Viscosity = 0.0f;

	// ============================================================
	// Vorticity
	//
	// Disabled for stability testing.
	// ============================================================

	private const float VorticityStrength = 0.0f;

	private const float MaxVorticityVelocity = 18.0f;

	private const float MaxVorticityVelocitySquared =
		MaxVorticityVelocity *
		MaxVorticityVelocity;

	// ============================================================
	// Surface
	//
	// Disabled for stability testing.
	// ============================================================

	private const float SurfaceThreshold = 0.35f;

	private const float SurfaceThresholdSquared =
		SurfaceThreshold *
		SurfaceThreshold;

	private const float SurfaceTension = 0.1f;

	private const float MaxSurfaceVelocity = 200.0f;

	private const float MaxSurfaceVelocitySquared =
		MaxSurfaceVelocity *
		MaxSurfaceVelocity;

	// ============================================================
	// Velocity damping
	//
	// Increased damping to remove residual energy.
	// ============================================================

	private const float VelocityDamping = 0.995f;

	// ============================================================
	// Sleeping
	//
	// Disabled for now.
	// ============================================================

	private const float SleepVelocityThreshold = 2.0f;
	private const float WakeVelocityThreshold = 4.0f;

	private const float SleepTime = 0.35f;
	private const float SleepDampingStrength = 3.0f;

	private const float SleepVelocityThresholdSquared =
		SleepVelocityThreshold *
		SleepVelocityThreshold;

	private const float WakeVelocityThresholdSquared =
		WakeVelocityThreshold *
		WakeVelocityThreshold;

	// ============================================================
	// World bounds
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
	// Polygon collision
	// ============================================================

	private const float PolygonParticleRadius = 4.0f;

	// ============================================================
	// Neighbor limit
	// ============================================================

	private const int MaxNeighbors = 48;

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
	private float[] neighborInverseDistance;

	private float[] neighborQ;
	private float[] neighborQ2;
	private float[] neighborGradientScale;

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

	private int _lastParticleCount;

	// ============================================================
	// Constructor
	// ============================================================

	public PbfSolver(
		SpatialHash spatialHash)
	{
		hash = spatialHash;

		polygonColliders =
			new List<FluidPolygonCollider>();
	}

	// ============================================================
	// Polygon collider management
	// ============================================================

	public void AddPolygonCollider(
		FluidPolygonCollider collider)
	{
		if (collider == null)
			return;

		polygonColliders.Add(collider);
	}

	public void ClearPolygonColliders()
	{
		polygonColliders.Clear();
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

		_lastParticleCount =
			count;

		if (count <= 0 ||
			dt <= 0.0f)
		{
			return;
		}

		EnsureBuffers(count);

		long totalStart =
			Stopwatch.GetTimestamp();

		// ========================================================
		// 1. Predict positions
		// ========================================================

		float gravityDt =
			Gravity * dt;

		for (int i = 0;
			 i < count;
			 i++)
		{
			particles.VelY[i] +=
				gravityDt;

			particles.PredX[i] =
				particles.PosX[i] +
				particles.VelX[i] * dt;

			particles.PredY[i] =
				particles.PosY[i] +
				particles.VelY[i] * dt;
		}

		// ========================================================
		// 2. PBF position solve
		// ========================================================

		int iterationsUsed = 0;

		float frameDensityError =
			float.MaxValue;

		for (int iteration = 0;
			 iteration < MaxIterations;
			 iteration++)
		{
			iterationsUsed++;

			// ----------------------------------------------------
			// Build spatial hash
			// ----------------------------------------------------

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

			// ----------------------------------------------------
			// Build neighbor cache
			// ----------------------------------------------------

			BuildNeighborCache(
				particles,
				count
			);

			// ----------------------------------------------------
			// Calculate lambdas
			// ----------------------------------------------------

			long phaseAStart =
				Stopwatch.GetTimestamp();

			frameDensityError =
				CalculateLambdas(count);

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

			// ----------------------------------------------------
			// Calculate position corrections
			// ----------------------------------------------------

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
			// Polygon collision
			// ----------------------------------------------------

			ConstrainToPolygonColliders(
				particles
			);

			// ----------------------------------------------------
			// World bounds
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

			// ----------------------------------------------------
			// Adaptive exit
			// ----------------------------------------------------

			if (iteration + 1 >= MinIterations &&
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
		// Currently all secondary velocity effects are disabled.
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
		// 4. Reconstruct velocity from corrected position
		// ========================================================

		float inverseDt =
			1.0f / dt;

		for (int i = 0;
			 i < count;
			 i++)
		{
			float oldX =
				particles.PosX[i];

			float oldY =
				particles.PosY[i];

			float newVelocityX =
				(
					particles.PredX[i] -
					oldX
				) *
				inverseDt;

			float newVelocityY =
				(
					particles.PredY[i] -
					oldY
				) *
				inverseDt;

			// Only damping is applied.
			//
			// No artificial surface force.
			// No vorticity.
			// No XSPH viscosity.

			float finalVelocityX =
				newVelocityX *
				VelocityDamping;

			float finalVelocityY =
				newVelocityY *
				VelocityDamping;

			ApplyBoundaryVelocityResponse(
				particles,
				i,
				ref finalVelocityX,
				ref finalVelocityY
			);

			// Sleeping intentionally disabled.

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
			PrintProfiler();

			ResetProfiler();
		}
	}

	// ============================================================
	// Phase B
	// ============================================================

	private void CalculatePositionCorrections(
		int count)
	{
		for (int i = 0;
			 i < count;
			 i++)
		{
			float correctionX = 0.0f;
			float correctionY = 0.0f;

			int offset =
				neighborOffsets[i];

			int neighborCount =
				neighborCounts[i];

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

				float gradientScale =
					neighborGradientScale[index];

				float gradientX =
					neighborDx[index] *
					gradientScale;

				float gradientY =
					neighborDy[index] *
					gradientScale;

				float lambdaSum =
					lambdaI +
					lambdas[j];

				// Artificial pressure is disabled.

				float pressureTerm =
					lambdaSum;

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
				MaxCorrectionSquared)
			{
				float inverseLength =
					1.0f /
					Mathf.Sqrt(
						correctionLengthSquared
					);

				float scale =
					MaxCorrection *
					inverseLength;

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
	// Neighbor cache
	// ============================================================

	private void BuildNeighborCache(
		ParticleData particles,
		int count)
	{
		int bufferWritePosition = 0;

		for (int i = 0;
			 i < count;
			 i++)
		{
			float px =
				particles.PredX[i];

			float py =
				particles.PredY[i];

			// Make sure enough memory exists before Query writes.

			EnsureNeighborCapacity(
				bufferWritePosition +
				MaxNeighbors
			);

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
				float inverseDistance;

				if (distanceSquared <=
					0.000001f)
				{
					distance = 0.0f;
					inverseDistance = 0.0f;
				}
				else
				{
					distance =
						Mathf.Sqrt(
							distanceSquared
						);

					inverseDistance =
						1.0f /
						distance;
				}

				neighborDx[index] =
					dx;

				neighborDy[index] =
					dy;

				neighborDistance[index] =
					distance;

				neighborInverseDistance[index] =
					inverseDistance;

				// =================================================
				// Correct smoothing kernel coordinate
				// =================================================

				float q;

				if (distance <= 0.000001f)
				{
					q = 1.0f;
				}
				else
				{
					q =
						1.0f -
						distance /
						SmoothingRadius;

					if (q < 0.0f)
						q = 0.0f;
				}

				neighborQ[index] =
					q;

				float q2 =
					q * q;

				neighborQ2[index] =
					q2;

				// =================================================
				// Kernel gradient
				// =================================================

				if (distance <= 0.000001f)
				{
					neighborGradientScale[index] =
						0.0f;
				}
				else
				{
					neighborGradientScale[index] =
						(
							-3.0f *
							q2 /
							SmoothingRadius
						) *
						inverseDistance *
						InverseRestDensity;
				}
			}

			bufferWritePosition +=
				neighborCount;
		}
	}

	// ============================================================
	// Phase A
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
					neighborQ2[index];

				// Density kernel = q^3

				density +=
					q2 * q;

				if (j == i)
					continue;

				float gradientScale =
					neighborGradientScale[index];

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
				density *
				InverseRestDensity -
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
	// Velocity effects
	// ============================================================

	private void CalculateVelocityEffects(
		ParticleData particles,
		float dt,
		int count)
	{
		// ========================================================
		// Everything here is intentionally disabled during
		// stability testing.
		// ========================================================

		for (int i = 0;
			 i < count;
			 i++)
		{
			viscosityX[i] =
				0.0f;

			viscosityY[i] =
				0.0f;

			surfaceVelocityX[i] =
				0.0f;

			surfaceVelocityY[i] =
				0.0f;

			vorticityVelocityX[i] =
				0.0f;

			vorticityVelocityY[i] =
				0.0f;

			vorticity[i] =
				0.0f;

			vorticityMagnitude[i] =
				0.0f;

			// We still expose surface particle information.
			// For now, classify everything as non-surface.

			SurfaceParticles[i] =
				false;
		}
	}

	// ============================================================
	// Polygon constraint
	// ============================================================

	private void ConstrainToPolygonColliders(
		ParticleData particles)
	{
		if (polygonColliders == null ||
			polygonColliders.Count == 0)
		{
			return;
		}

		for (int i = 0;
			 i < particles.Count;
			 i++)
		{
			Vector2 position =
				new Vector2(
					particles.PredX[i],
					particles.PredY[i]
				);

			for (int c = 0;
				 c < polygonColliders.Count;
				 c++)
			{
				FluidPolygonCollider collider =
					polygonColliders[c];

				if (collider == null)
					continue;

				if (!collider.ResolveCollision(
						position,
						PolygonParticleRadius,
						out Vector2 correctedPosition,
						out Vector2 normal))
				{
					continue;
				}

				position =
					correctedPosition;
			}

			particles.PredX[i] =
				position.X;

			particles.PredY[i] =
				position.Y;
		}
	}

	// ============================================================
	// Sleep
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

		if (velocitySquared >=
			WakeVelocityThresholdSquared)
		{
			sleepProgress[i] =
				0.0f;

			sleeping[i] =
				false;

			return;
		}

		if (velocitySquared <
			SleepVelocityThresholdSquared)
		{
			sleepProgress[i] +=
				dt / SleepTime;

			if (sleepProgress[i] > 1.0f)
				sleepProgress[i] = 1.0f;

			float sleepBlend =
				sleepProgress[i];

			float damping =
				1.0f -
				SleepDampingStrength *
				sleepBlend *
				dt;

			if (damping < 0.0f)
				damping = 0.0f;

			velocityX *=
				damping;

			velocityY *=
				damping;

			if (sleepProgress[i] >= 1.0f)
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

		sleepProgress[i] -=
			dt / SleepTime;

		if (sleepProgress[i] < 0.0f)
			sleepProgress[i] = 0.0f;

		sleeping[i] =
			false;
	}

	// ============================================================
	// World bounds
	// ============================================================

	private void ConstrainToBounds(
		ParticleData particles)
	{
		float left =
			MinX +
			BoundarySkin;

		float right =
			MaxX -
			BoundarySkin;

		float top =
			MinY +
			BoundarySkin;

		float bottom =
			MaxY -
			BoundarySkin;

		for (int i = 0;
			 i < particles.Count;
			 i++)
		{
			float x =
				particles.PredX[i];

			float y =
				particles.PredY[i];

			if (x < left)
				x = left;
			else if (x > right)
				x = right;

			if (y < top)
				y = top;
			else if (y > bottom)
				y = bottom;

			particles.PredX[i] =
				x;

			particles.PredY[i] =
				y;
		}
	}

	// ============================================================
	// Boundary velocity
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

		float left =
			MinX +
			BoundarySkin;

		float right =
			MaxX -
			BoundarySkin;

		float top =
			MinY +
			BoundarySkin;

		float bottom =
			MaxY -
			BoundarySkin;

		if (x <= left + 0.001f)
		{
			if (velocityX < 0.0f)
			{
				if (Mathf.Abs(velocityX) <
					BoundaryVelocityEpsilon)
				{
					velocityX = 0.0f;
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

		if (x >= right - 0.001f)
		{
			if (velocityX > 0.0f)
			{
				if (Mathf.Abs(velocityX) <
					BoundaryVelocityEpsilon)
				{
					velocityX = 0.0f;
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

		if (y <= top + 0.001f)
		{
			if (velocityY < 0.0f)
			{
				if (Mathf.Abs(velocityY) <
					BoundaryVelocityEpsilon)
				{
					velocityY = 0.0f;
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

		if (y >= bottom - 0.001f)
		{
			if (velocityY > 0.0f)
			{
				if (Mathf.Abs(velocityY) <
					BoundaryVelocityEpsilon)
				{
					velocityY = 0.0f;
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

		neighborInverseDistance =
			new float[initialCapacity];

		neighborQ =
			new float[initialCapacity];

		neighborQ2 =
			new float[initialCapacity];

		neighborGradientScale =
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

		float[] newNeighborInverseDistance =
			new float[newCapacity];

		float[] newNeighborQ =
			new float[newCapacity];

		float[] newNeighborQ2 =
			new float[newCapacity];

		float[] newNeighborGradientScale =
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
			neighborInverseDistance,
			newNeighborInverseDistance,
			neighborInverseDistance.Length
		);

		Array.Copy(
			neighborQ,
			newNeighborQ,
			neighborQ.Length
		);

		Array.Copy(
			neighborQ2,
			newNeighborQ2,
			neighborQ2.Length
		);

		Array.Copy(
			neighborGradientScale,
			newNeighborGradientScale,
			neighborGradientScale.Length
		);

		neighborBuffer =
			newNeighborBuffer;

		neighborDx =
			newNeighborDx;

		neighborDy =
			newNeighborDy;

		neighborDistance =
			newNeighborDistance;

		neighborInverseDistance =
			newNeighborInverseDistance;

		neighborQ =
			newNeighborQ;

		neighborQ2 =
			newNeighborQ2;

		neighborGradientScale =
			newNeighborGradientScale;
	}

	// ============================================================
	// Density field
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

	// ============================================================
	// Profiler
	// ============================================================

	private void PrintProfiler()
	{
		double frames =
			profilerFrames;

		double build =
			accumBuildMs /
			frames;

		double phaseA =
			accumPhaseAMs /
			frames;

		double phaseB =
			accumPhaseBMs /
			frames;

		double velocity =
			accumVelocityMs /
			frames;

		double total =
			accumTotalMs /
			frames;

		double averageIterations =
			accumIterations /
			frames;

		GD.Print(
			$"PBF profiler " +
			$"(avg ms over {profilerFrames} frames): " +
			$"Particles={_lastParticleCount} " +
			$"Build={build:F2}ms " +
			$"PhaseA={phaseA:F2}ms " +
			$"PhaseB={phaseB:F2}ms " +
			$"Velocity={velocity:F2}ms " +
			$"Total={total:F2}ms " +
			$"Iterations={averageIterations:F2} " +
			$"MaxDensityError={maxObservedDensityError:F4} " +
			$"(MaxNeighbors={MaxNeighbors})"
		);
	}

	private void ResetProfiler()
	{
		profilerFrames = 0;

		accumBuildMs = 0.0;
		accumPhaseAMs = 0.0;
		accumPhaseBMs = 0.0;
		accumVelocityMs = 0.0;
		accumTotalMs = 0.0;

		accumIterations = 0;

		maxObservedDensityError =
			0.0f;
	}
}
