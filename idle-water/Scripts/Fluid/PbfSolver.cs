
using Godot;
using System;
using System.Diagnostics;
using System.Collections.Generic;

public class PbfSolver
{
	private readonly SpatialHash hash;
	private readonly List<FluidPolygonCollider> polygonColliders;

	// ============================================================
	// Simulation
	// ============================================================

	private const float Gravity = 200.0f;

	private const float SmoothingRadius = 12.0f;
	private const float SmoothingRadiusSquared = 144.0f;
	private const float InverseSmoothingRadius = 1.0f / 12.0f;

	// ============================================================
	// Density
	// ============================================================

	private const float RestDensity = 1.15f;
	private const float InverseRestDensity = 1.0f / RestDensity;
	private const float LambdaEpsilon = 0.00001f;

	// ============================================================
	// PBF
	// ============================================================

	private const int MinIterations = 2;
	private const int MaxIterations = 3;

	private const float DensityErrorThreshold = 0.75f;

	private const float MaxCorrection = 0.5f;
	private const float MaxCorrectionSquared = 0.25f;

	// ============================================================
	// Stability
	// ============================================================

	private const float VelocityDamping = 0.993f;

	// ============================================================
	// Impact damping
	// ============================================================

	private const float ImpactDamping = 0.65f;
	private const float ImpactNormalEpsilon = 0.0001f;

	// ============================================================
	// Sleeping
	// ============================================================

	private const float SleepVelocityThreshold = 2.0f;
	private const float WakeVelocityThreshold = 4.0f;

	private const float SleepTime = 0.35f;
	private const float SleepDampingStrength = 3.0f;

	private const float SleepVelocityThresholdSquared =
		SleepVelocityThreshold * SleepVelocityThreshold;

	private const float WakeVelocityThresholdSquared =
		WakeVelocityThreshold * WakeVelocityThreshold;

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
	// Polygon
	// ============================================================

	private const float PolygonParticleRadius = 4.0f;

	// ============================================================
	// Neighbors
	// ============================================================

	private const int MaxNeighbors = 40;

	// Fixed stride.

	private int neighborStride;

	// ============================================================
	// Working arrays
	// ============================================================

	private float[] lambdas;
	private float[] particleDensity;

	private float[] sleepProgress;
	private bool[] sleeping;

	public bool[] SurfaceParticles;

	// ============================================================
	// Collision impact data
	// ============================================================

	private float[] impactNormalX;
	private float[] impactNormalY;
	private bool[] impacted;

	// ============================================================
	// Neighbor cache
	//
	// Every particle owns exactly MaxNeighbors slots.
	//
	// Particle i:
	//
	//     start = i * MaxNeighbors
	//
	// This removes:
	//
	// - variable offsets
	// - dynamic buffer growth
	// - repeated capacity checks
	// - bufferWritePosition bookkeeping
	// ============================================================

	private int[] neighborBuffer;
	private int[] neighborCounts;

	private float[] neighborDx;
	private float[] neighborDy;

	private float[] neighborQ;
	private float[] neighborGradientScale;

	// ============================================================
	// Profiler
	// ============================================================

	private const int ProfilerPrintInterval = 60;

	private int profilerFrames;

	private double accumPredictMs;
	private double accumBuildMs;
	private double accumNeighborsMs;
	private double accumPhaseAMs;

	private double accumCorrectionMs;
	private double accumPolygonMs;
	private double accumBoundsMs;
	private double accumVelocityMs;

	private double accumTotalMs;

	private int accumIterations;

	private float maxObservedDensityError;

	private int lastParticleCount;

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

		lastParticleCount =
			count;

		if (count <= 0 || dt <= 0.0f)
			return;

		EnsureBuffers(count);

		long totalStart =
			Stopwatch.GetTimestamp();

		float[] posX = particles.PosX;
		float[] posY = particles.PosY;

		float[] velX = particles.VelX;
		float[] velY = particles.VelY;

		float[] predX = particles.PredX;
		float[] predY = particles.PredY;

		// ========================================================
		// Reset collision state
		// ========================================================

		Array.Clear(impacted, 0, count);
		Array.Clear(impactNormalX, 0, count);
		Array.Clear(impactNormalY, 0, count);

		// ========================================================
		// Predict positions
		// ========================================================

		long predictStart =
			Stopwatch.GetTimestamp();

		float gravityDt =
			Gravity * dt;

		for (int i = 0; i < count; i++)
		{
			velY[i] += gravityDt;

			predX[i] =
				posX[i] +
				velX[i] * dt;

			predY[i] =
				posY[i] +
				velY[i] * dt;
		}

		long predictEnd =
			Stopwatch.GetTimestamp();

		accumPredictMs +=
			(predictEnd - predictStart) *
			1000.0 / Stopwatch.Frequency;

		// ========================================================
		// Build spatial hash
		// ========================================================

		long buildStart =
			Stopwatch.GetTimestamp();

		hash.Clear();

		for (int i = 0; i < count; i++)
		{
			hash.Insert(
				i,
				predX[i],
				predY[i]
			);
		}

		long buildEnd =
			Stopwatch.GetTimestamp();

		accumBuildMs +=
			(buildEnd - buildStart) *
			1000.0 / Stopwatch.Frequency;

		// ========================================================
		// Build neighbor topology + geometry
		// ========================================================

		long neighborsStart =
			Stopwatch.GetTimestamp();

		BuildNeighborCache(
			predX,
			predY,
			count
		);

		long neighborsEnd =
			Stopwatch.GetTimestamp();

		accumNeighborsMs +=
			(neighborsEnd - neighborsStart) *
			1000.0 / Stopwatch.Frequency;

		// ========================================================
		// PBF iterations
		// ========================================================

		int iterationsUsed = 0;

		float frameDensityError =
			float.MaxValue;

		for (
			int iteration = 0;
			iteration < MaxIterations;
			iteration++)
		{
			iterationsUsed++;

			// ----------------------------------------------------
			// Update only neighbor geometry.
			//
			// Topology stays fixed for this frame.
			// ----------------------------------------------------

			if (iteration > 0)
			{
				long updateStart =
					Stopwatch.GetTimestamp();

				UpdateNeighborCache(
					predX,
					predY,
					count
				);

				long updateEnd =
					Stopwatch.GetTimestamp();

				accumNeighborsMs +=
					(updateEnd - updateStart) *
					1000.0 / Stopwatch.Frequency;
			}

			// ----------------------------------------------------
			// Phase A
			// ----------------------------------------------------

			long phaseAStart =
				Stopwatch.GetTimestamp();

			frameDensityError =
				CalculateLambdas(count);

			long phaseAEnd =
				Stopwatch.GetTimestamp();

			accumPhaseAMs +=
				(phaseAEnd - phaseAStart) *
				1000.0 / Stopwatch.Frequency;

			if (
				frameDensityError >
				maxObservedDensityError)
			{
				maxObservedDensityError =
					frameDensityError;
			}

			// ----------------------------------------------------
			// Position correction
			// ----------------------------------------------------

			long correctionStart =
				Stopwatch.GetTimestamp();

			ApplyPositionCorrections(
				predX,
				predY,
				count
			);

			long correctionEnd =
				Stopwatch.GetTimestamp();

			accumCorrectionMs +=
				(correctionEnd - correctionStart) *
				1000.0 / Stopwatch.Frequency;

			// ----------------------------------------------------
			// Polygon collision
			// ----------------------------------------------------

			if (polygonColliders.Count > 0)
			{
				long polygonStart =
					Stopwatch.GetTimestamp();

				ConstrainToPolygonColliders(
					predX,
					predY,
					count
				);

				long polygonEnd =
					Stopwatch.GetTimestamp();

				accumPolygonMs +=
					(polygonEnd - polygonStart) *
					1000.0 / Stopwatch.Frequency;
			}

			// ----------------------------------------------------
			// World bounds
			// ----------------------------------------------------

			long boundsStart =
				Stopwatch.GetTimestamp();

			ConstrainToBounds(
				predX,
				predY,
				count
			);

			long boundsEnd =
				Stopwatch.GetTimestamp();

			accumBoundsMs +=
				(boundsEnd - boundsStart) *
				1000.0 / Stopwatch.Frequency;

			// ----------------------------------------------------
			// Adaptive exit
			// ----------------------------------------------------

			if (
				iteration + 1 >= MinIterations &&
				frameDensityError <=
				DensityErrorThreshold)
			{
				break;
			}
		}

		accumIterations +=
			iterationsUsed;

		// ========================================================
		// Velocity reconstruction
		// ========================================================

		long velocityStart =
			Stopwatch.GetTimestamp();

		float inverseDt =
			1.0f / dt;

		float boundaryLeft =
			MinX + BoundarySkin;

		float boundaryRight =
			MaxX - BoundarySkin;

		float boundaryTop =
			MinY + BoundarySkin;

		float boundaryBottom =
			MaxY - BoundarySkin;

		for (int i = 0; i < count; i++)
		{
			float oldX =
				posX[i];

			float oldY =
				posY[i];

			float finalVelocityX =
				(predX[i] - oldX) *
				inverseDt *
				VelocityDamping;

			float finalVelocityY =
				(predY[i] - oldY) *
				inverseDt *
				VelocityDamping;

			float x = predX[i];
			float y = predY[i];

			// ----------------------------------------------------
			// Boundary response
			// ----------------------------------------------------

			if (x <= boundaryLeft + 0.001f)
			{
				if (finalVelocityX < 0.0f)
				{
					if (
						Mathf.Abs(finalVelocityX) <
						BoundaryVelocityEpsilon)
					{
						finalVelocityX = 0.0f;
					}
					else
					{
						finalVelocityX =
							-finalVelocityX *
							BoundaryRestitution;
					}
				}

				finalVelocityY *=
					1.0f - BoundaryFriction;
			}
			else if (x >= boundaryRight - 0.001f)
			{
				if (finalVelocityX > 0.0f)
				{
					if (
						Mathf.Abs(finalVelocityX) <
						BoundaryVelocityEpsilon)
					{
						finalVelocityX = 0.0f;
					}
					else
					{
						finalVelocityX =
							-finalVelocityX *
							BoundaryRestitution;
					}
				}

				finalVelocityY *=
					1.0f - BoundaryFriction;
			}

			if (y <= boundaryTop + 0.001f)
			{
				if (finalVelocityY < 0.0f)
				{
					if (
						Mathf.Abs(finalVelocityY) <
						BoundaryVelocityEpsilon)
					{
						finalVelocityY = 0.0f;
					}
					else
					{
						finalVelocityY =
							-finalVelocityY *
							BoundaryRestitution;
					}
				}

				finalVelocityX *=
					1.0f - BoundaryFriction;
			}
			else if (y >= boundaryBottom - 0.001f)
			{
				if (finalVelocityY > 0.0f)
				{
					if (
						Mathf.Abs(finalVelocityY) <
						BoundaryVelocityEpsilon)
					{
						finalVelocityY = 0.0f;
					}
					else
					{
						finalVelocityY =
							-finalVelocityY *
							BoundaryRestitution;
					}
				}

				finalVelocityX *=
					1.0f - BoundaryFriction;
			}

			// ----------------------------------------------------
			// Polygon impact damping
			// ----------------------------------------------------

			if (impacted[i])
			{
				ApplyImpactDamping(
					i,
					ref finalVelocityX,
					ref finalVelocityY
				);
			}

			// ----------------------------------------------------
			// Sleep
			// ----------------------------------------------------

			ApplySleepBehavior(
				i,
				dt,
				ref finalVelocityX,
				ref finalVelocityY
			);

			velX[i] = finalVelocityX;
			velY[i] = finalVelocityY;

			posX[i] = predX[i];
			posY[i] = predY[i];
		}

		long velocityEnd =
			Stopwatch.GetTimestamp();

		accumVelocityMs +=
			(velocityEnd - velocityStart) *
			1000.0 / Stopwatch.Frequency;

		// ========================================================
		// Profiler
		// ========================================================

		long totalEnd =
			Stopwatch.GetTimestamp();

		accumTotalMs +=
			(totalEnd - totalStart) *
			1000.0 / Stopwatch.Frequency;

		profilerFrames++;

		if (
			profilerFrames >=
			ProfilerPrintInterval)
		{
			PrintProfiler();
			ResetProfiler();
		}
	}

	// ============================================================
	// Position corrections
	// ============================================================

	private void ApplyPositionCorrections(
		float[] predX,
		float[] predY,
		int count)
	{
		float[] localLambdas =
			lambdas;

		float[] localDx =
			neighborDx;

		float[] localDy =
			neighborDy;

		float[] localGradient =
			neighborGradientScale;

		int[] localNeighbors =
			neighborBuffer;

		for (int i = 0; i < count; i++)
		{
			float correctionX = 0.0f;
			float correctionY = 0.0f;

			int start =
				i * neighborStride;

			int end =
				start + neighborCounts[i];

			float lambdaI =
				localLambdas[i];

			for (
				int index = start;
				index < end;
				index++)
			{
				int j =
					localNeighbors[index];

				if (j == i)
					continue;

				float lambdaSum =
					lambdaI +
					localLambdas[j];

				float scale =
					lambdaSum *
					localGradient[index];

				correctionX +=
					scale *
					localDx[index];

				correctionY +=
					scale *
					localDy[index];
			}

			float correctionLengthSquared =
				correctionX * correctionX +
				correctionY * correctionY;

			if (
				correctionLengthSquared >
				MaxCorrectionSquared)
			{
				float inverseLength =
					1.0f /
					MathF.Sqrt(
						correctionLengthSquared
					);

				float scale =
					MaxCorrection *
					inverseLength;

				correctionX *= scale;
				correctionY *= scale;
			}

			predX[i] += correctionX;
			predY[i] += correctionY;
		}
	}

	// ============================================================
	// Build neighbor cache
	// ============================================================

	private void BuildNeighborCache(
		float[] predX,
		float[] predY,
		int count)
	{
		int[] localNeighbors =
			neighborBuffer;

		int[] localCounts =
			neighborCounts;

		float[] localDx =
			neighborDx;

		float[] localDy =
			neighborDy;

		float[] localQ =
			neighborQ;

		float[] localGradient =
			neighborGradientScale;

		for (int i = 0; i < count; i++)
		{
			float px =
				predX[i];

			float py =
				predY[i];

			int start =
				i * neighborStride;

			int neighborCount =
				hash.QueryPbf(
					px,
					py,
					predX,
					predY,
					localNeighbors,
					start,
					MaxNeighbors
				);

			localCounts[i] =
				neighborCount;

			int end =
				start + neighborCount;

			for (
				int index = start;
				index < end;
				index++)
			{
				int j =
					localNeighbors[index];

				float dx =
					px -
					predX[j];

				float dy =
					py -
					predY[j];

				float distanceSquared =
					dx * dx +
					dy * dy;

				localDx[index] =
					dx;

				localDy[index] =
					dy;

				if (
					distanceSquared <=
					0.000001f)
				{
					localQ[index] =
						1.0f;

					localGradient[index] =
						0.0f;

					continue;
				}

				float inverseDistance =
					1.0f /
					MathF.Sqrt(
						distanceSquared
					);

				float distance =
					distanceSquared *
					inverseDistance;

				float q =
					1.0f -
					distance *
					InverseSmoothingRadius;

				if (q <= 0.0f)
				{
					localQ[index] = 0.0f;
					localGradient[index] = 0.0f;
					continue;
				}

				localQ[index] =
					q;

				float q2 =
					q * q;

				localGradient[index] =
					-3.0f *
					q2 *
					InverseSmoothingRadius *
					inverseDistance *
					InverseRestDensity;
			}
		}
	}

	// ============================================================
	// Update neighbor geometry
	// ============================================================

	private void UpdateNeighborCache(
		float[] predX,
		float[] predY,
		int count)
	{
		int[] localNeighbors =
			neighborBuffer;

		float[] localDx =
			neighborDx;

		float[] localDy =
			neighborDy;

		float[] localQ =
			neighborQ;

		float[] localGradient =
			neighborGradientScale;

		int[] localCounts =
			neighborCounts;

		for (int i = 0; i < count; i++)
		{
			float px =
				predX[i];

			float py =
				predY[i];

			int start =
				i * neighborStride;

			int end =
				start + localCounts[i];

			for (
				int index = start;
				index < end;
				index++)
			{
				int j =
					localNeighbors[index];

				float dx =
					px -
					predX[j];

				float dy =
					py -
					predY[j];

				float distanceSquared =
					dx * dx +
					dy * dy;

				localDx[index] =
					dx;

				localDy[index] =
					dy;

				if (
					distanceSquared <=
					0.000001f)
				{
					localQ[index] =
						1.0f;

					localGradient[index] =
						0.0f;

					continue;
				}

				float inverseDistance =
					1.0f /
					MathF.Sqrt(
						distanceSquared
					);

				float distance =
					distanceSquared *
					inverseDistance;

				float q =
					1.0f -
					distance *
					InverseSmoothingRadius;

				if (q <= 0.0f)
				{
					localQ[index] = 0.0f;
					localGradient[index] = 0.0f;
					continue;
				}

				localQ[index] =
					q;

				float q2 =
					q * q;

				localGradient[index] =
					-3.0f *
					q2 *
					InverseSmoothingRadius *
					inverseDistance *
					InverseRestDensity;
			}
		}
	}

	// ============================================================
	// Lambda calculation
	// ============================================================

	private float CalculateLambdas(
		int count)
	{
		float maximumDensityError =
			0.0f;

		int[] localNeighbors =
			neighborBuffer;

		float[] localQ =
			neighborQ;

		float[] localDx =
			neighborDx;

		float[] localDy =
			neighborDy;

		float[] localGradient =
			neighborGradientScale;

		for (int i = 0; i < count; i++)
		{
			int start =
				i * neighborStride;

			int end =
				start + neighborCounts[i];

			float density = 0.0f;
			float gradSumX = 0.0f;
			float gradSumY = 0.0f;
			float neighborGradientSquared = 0.0f;

			for (
				int index = start;
				index < end;
				index++)
			{
				float q =
					localQ[index];

				float q2 =
					q * q;

				density +=
					q2 * q;

				int j =
					localNeighbors[index];

				if (j == i)
					continue;

				float scale =
					localGradient[index];

				float gx =
					localDx[index] *
					scale;

				float gy =
					localDy[index] *
					scale;

				gradSumX += gx;
				gradSumY += gy;

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
				Mathf.Abs(constraint);

			if (
				absoluteConstraint >
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
	// Polygon collision
	// ============================================================

	private void ConstrainToPolygonColliders(
		float[] predX,
		float[] predY,
		int count)
	{
		int colliderCount =
			polygonColliders.Count;

		for (int i = 0; i < count; i++)
		{
			Vector2 position =
				new Vector2(
					predX[i],
					predY[i]
				);

			Vector2 accumulatedNormal =
				Vector2.Zero;

			bool particleImpacted =
				false;

			for (
				int c = 0;
				c < colliderCount;
				c++)
			{
				FluidPolygonCollider collider =
					polygonColliders[c];

				if (collider == null)
					continue;

				if (
					!collider.ResolveCollision(
						position,
						PolygonParticleRadius,
						out Vector2 correctedPosition,
						out Vector2 normal
					))
				{
					continue;
				}

				position =
					correctedPosition;

				if (
					normal.LengthSquared() >
					ImpactNormalEpsilon)
				{
					accumulatedNormal += normal;
					particleImpacted = true;
				}
			}

			predX[i] = position.X;
			predY[i] = position.Y;

			if (particleImpacted)
			{
				float normalLengthSquared =
					accumulatedNormal.LengthSquared();

				if (
					normalLengthSquared >
					ImpactNormalEpsilon)
				{
					float inverseLength =
						1.0f /
						Mathf.Sqrt(
							normalLengthSquared
						);

					accumulatedNormal *=
						inverseLength;

					impactNormalX[i] =
						accumulatedNormal.X;

					impactNormalY[i] =
						accumulatedNormal.Y;

					impacted[i] =
						true;
				}
			}
		}
	}

	// ============================================================
	// Impact damping
	// ============================================================

	private void ApplyImpactDamping(
		int i,
		ref float velocityX,
		ref float velocityY)
	{
		float normalX =
			impactNormalX[i];

		float normalY =
			impactNormalY[i];

		float normalVelocity =
			velocityX * normalX +
			velocityY * normalY;

		if (normalVelocity <= 0.0f)
			return;

		float velocityChange =
			normalVelocity *
			ImpactDamping;

		velocityX -=
			normalX *
			velocityChange;

		velocityY -=
			normalY *
			velocityChange;
	}

	// ============================================================
	// Sleeping
	// ============================================================

	private void ApplySleepBehavior(
		int i,
		float dt,
		ref float velocityX,
		ref float velocityY)
	{
		float velocitySquared =
			velocityX * velocityX +
			velocityY * velocityY;

		if (
			velocitySquared >=
			WakeVelocityThresholdSquared)
		{
			sleepProgress[i] = 0.0f;
			sleeping[i] = false;
			return;
		}

		if (
			velocitySquared <
			SleepVelocityThresholdSquared)
		{
			sleepProgress[i] +=
				dt / SleepTime;

			if (sleepProgress[i] > 1.0f)
				sleepProgress[i] = 1.0f;

			float damping =
				1.0f -
				SleepDampingStrength *
				sleepProgress[i] *
				dt;

			if (damping < 0.0f)
				damping = 0.0f;

			velocityX *= damping;
			velocityY *= damping;

			if (sleepProgress[i] >= 1.0f)
			{
				sleeping[i] = true;
				velocityX = 0.0f;
				velocityY = 0.0f;
			}

			return;
		}

		sleepProgress[i] -=
			dt / SleepTime;

		if (sleepProgress[i] < 0.0f)
			sleepProgress[i] = 0.0f;

		sleeping[i] = false;
	}

	// ============================================================
	// World bounds
	// ============================================================

	private void ConstrainToBounds(
		float[] predX,
		float[] predY,
		int count)
	{
		float left =
			MinX + BoundarySkin;

		float right =
			MaxX - BoundarySkin;

		float top =
			MinY + BoundarySkin;

		float bottom =
			MaxY - BoundarySkin;

		for (int i = 0; i < count; i++)
		{
			float x = predX[i];
			float y = predY[i];

			if (x < left)
			{
				x = left;

				impacted[i] = true;
				impactNormalX[i] = 1.0f;
				impactNormalY[i] = 0.0f;
			}
			else if (x > right)
			{
				x = right;

				impacted[i] = true;
				impactNormalX[i] = -1.0f;
				impactNormalY[i] = 0.0f;
			}

			if (y < top)
			{
				y = top;

				impacted[i] = true;
				impactNormalX[i] = 0.0f;
				impactNormalY[i] = 1.0f;
			}
			else if (y > bottom)
			{
				y = bottom;

				impacted[i] = true;
				impactNormalX[i] = 0.0f;
				impactNormalY[i] = -1.0f;
			}

			predX[i] = x;
			predY[i] = y;
		}
	}

	// ============================================================
	// Buffers
	// ============================================================

	private void EnsureBuffers(
		int count)
	{
		if (
			lambdas != null &&
			lambdas.Length >= count)
		{
			return;
		}

		lambdas =
			new float[count];

		particleDensity =
			new float[count];

		sleepProgress =
			new float[count];

		sleeping =
			new bool[count];

		SurfaceParticles =
			new bool[count];

		impactNormalX =
			new float[count];

		impactNormalY =
			new float[count];

		impacted =
			new bool[count];

		neighborCounts =
			new int[count];

		neighborStride =
			MaxNeighbors;

		int capacity =
			Math.Max(
				MaxNeighbors,
				count * MaxNeighbors
			);

		neighborBuffer =
			new int[capacity];

		neighborDx =
			new float[capacity];

		neighborDy =
			new float[capacity];

		neighborQ =
			new float[capacity];

		neighborGradientScale =
			new float[capacity];
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

		for (int i = 0; i < count; i++)
		{
			field.AddDensity(
				particles.PredX[i],
				particles.PredY[i],
				particleDensity[i]
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

		double predict =
			accumPredictMs / frames;

		double build =
			accumBuildMs / frames;

		double neighbors =
			accumNeighborsMs / frames;

		double phaseA =
			accumPhaseAMs / frames;

		double correction =
			accumCorrectionMs / frames;

		double polygon =
			accumPolygonMs / frames;

		double bounds =
			accumBoundsMs / frames;

		double velocity =
			accumVelocityMs / frames;

		double total =
			accumTotalMs / frames;

		double iterations =
			accumIterations / frames;

		GD.Print(
			$"PBF profiler " +
			$"(avg ms over {profilerFrames} frames): " +

			$"Particles={lastParticleCount} " +

			$"Predict={predict:F2}ms " +
			$"Build={build:F2}ms " +
			$"Neighbors={neighbors:F2}ms " +
			$"PhaseA={phaseA:F2}ms " +
			$"Correction={correction:F2}ms " +
			$"Polygon={polygon:F2}ms " +
			$"Bounds={bounds:F2}ms " +
			$"Velocity={velocity:F2}ms " +

			$"Total={total:F2}ms " +

			$"Iterations={iterations:F2} " +

			$"MaxDensityError=" +
			$"{maxObservedDensityError:F4} " +

			$"MaxNeighbors={MaxNeighbors}"
		);
	}

	private void ResetProfiler()
	{
		profilerFrames = 0;

		accumPredictMs = 0.0;
		accumBuildMs = 0.0;
		accumNeighborsMs = 0.0;
		accumPhaseAMs = 0.0;

		accumCorrectionMs = 0.0;
		accumPolygonMs = 0.0;
		accumBoundsMs = 0.0;
		accumVelocityMs = 0.0;

		accumTotalMs = 0.0;

		accumIterations = 0;

		maxObservedDensityError =
			0.0f;
	}
}
