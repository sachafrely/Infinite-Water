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
	private const float SmoothingRadiusSquared =
		SmoothingRadius * SmoothingRadius;

	// ============================================================
	// Density
	// ============================================================

	private const float RestDensity = 1.15f;
	private const float InverseRestDensity =
		1.0f / RestDensity;

	private const float LambdaEpsilon = 0.00001f;

	// ============================================================
	// PBF
	// ============================================================

	private const int MinIterations = 2;
	private const int MaxIterations = 3;

	private const float DensityErrorThreshold = 0.035f;

	private const float MaxCorrection = 0.5f;
	private const float MaxCorrectionSquared =
		MaxCorrection * MaxCorrection;

	// ============================================================
	// Stability
	// ============================================================

	private const float VelocityDamping = 0.995f;

	// ============================================================
	// Sleeping
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
	// Polygon
	// ============================================================

	private const float PolygonParticleRadius = 4.0f;

	// ============================================================
	// Neighbor limit
	// ============================================================

	private const int MaxNeighbors = 40;

	// ============================================================
	// Working arrays
	// ============================================================

	private float[] lambdas;
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

		// ========================================================
		// Direct array references
		// ========================================================

		float[] posX =
			particles.PosX;

		float[] posY =
			particles.PosY;

		float[] velX =
			particles.VelX;

		float[] velY =
			particles.VelY;

		float[] predX =
			particles.PredX;

		float[] predY =
			particles.PredY;

		// ========================================================
		// 1. Predict positions
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
			1000.0 /
			Stopwatch.Frequency;

		// ========================================================
		// 2. PBF
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
			// Spatial hash
			// ----------------------------------------------------

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
				1000.0 /
				Stopwatch.Frequency;

			// ----------------------------------------------------
			// Neighbor cache
			// ----------------------------------------------------

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
				1000.0 /
				Stopwatch.Frequency;

			// ----------------------------------------------------
			// Phase A
			// ----------------------------------------------------

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
				1000.0 /
				Stopwatch.Frequency;

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
					1000.0 /
					Stopwatch.Frequency;
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
				1000.0 /
				Stopwatch.Frequency;

			// ----------------------------------------------------
			// Adaptive exit
			//
			// We always do two iterations.
			// A third is only used if necessary.
			// ----------------------------------------------------

			if (
				iteration + 1 >= MinIterations &&
				frameDensityError <=
				DensityErrorThreshold
			)
			{
				break;
			}
		}

		accumIterations +=
			iterationsUsed;

		// ========================================================
		// 3. Velocity reconstruction
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

			float newVelocityX =
				(predX[i] - oldX) *
				inverseDt;

			float newVelocityY =
				(predY[i] - oldY) *
				inverseDt;

			float finalVelocityX =
				newVelocityX *
				VelocityDamping;

			float finalVelocityY =
				newVelocityY *
				VelocityDamping;

			float x =
				predX[i];

			float y =
				predY[i];

			// ----------------------------------------------------
			// Boundary response
			// ----------------------------------------------------

			if (x <= boundaryLeft + 0.001f)
			{
				if (finalVelocityX < 0.0f)
				{
					if (
						Mathf.Abs(finalVelocityX) <
						BoundaryVelocityEpsilon
					)
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
						BoundaryVelocityEpsilon
					)
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
						BoundaryVelocityEpsilon
					)
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
						BoundaryVelocityEpsilon
					)
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
			// Sleep
			// ----------------------------------------------------

			ApplySleepBehavior(
				i,
				dt,
				ref finalVelocityX,
				ref finalVelocityY
			);

			velX[i] =
				finalVelocityX;

			velY[i] =
				finalVelocityY;

			posX[i] =
				predX[i];

			posY[i] =
				predY[i];
		}

		long velocityEnd =
			Stopwatch.GetTimestamp();

		accumVelocityMs +=
			(velocityEnd - velocityStart) *
			1000.0 /
			Stopwatch.Frequency;

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

		if (
			profilerFrames >=
			ProfilerPrintInterval
		)
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
		for (int i = 0; i < count; i++)
		{
			float correctionX = 0.0f;
			float correctionY = 0.0f;

			int offset =
				neighborOffsets[i];

			int neighborCount =
				neighborCounts[i];

			float lambdaI =
				lambdas[i];

			int end =
				offset + neighborCount;

			for (
				int index = offset;
				index < end;
				index++)
			{
				int j =
					neighborBuffer[index];

				if (j == i)
					continue;

				float gradientScale =
					neighborGradientScale[index];

				correctionX +=
					(lambdaI + lambdas[j]) *
					neighborDx[index] *
					gradientScale;

				correctionY +=
					(lambdaI + lambdas[j]) *
					neighborDy[index] *
					gradientScale;
			}

			float correctionLengthSquared =
				correctionX * correctionX +
				correctionY * correctionY;

			if (
				correctionLengthSquared >
				MaxCorrectionSquared
			)
			{
				float inverseLength =
					1.0f /
					Mathf.Sqrt(
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
	// Neighbor cache
	// ============================================================

	private void BuildNeighborCache(
		float[] predX,
		float[] predY,
		int count)
	{
		int bufferWritePosition = 0;

		for (int i = 0; i < count; i++)
		{
			float px =
				predX[i];

			float py =
				predY[i];

			EnsureNeighborCapacity(
				bufferWritePosition +
				MaxNeighbors
			);

			int neighborCount =
				hash.Query(
					px,
					py,
					SmoothingRadius,
					predX,
					predY,
					neighborBuffer,
					bufferWritePosition,
					MaxNeighbors
				);

			neighborOffsets[i] =
				bufferWritePosition;

			neighborCounts[i] =
				neighborCount;

			int end =
				bufferWritePosition +
				neighborCount;

			for (
				int index = bufferWritePosition;
				index < end;
				index++)
			{
				int j =
					neighborBuffer[index];

				float dx =
					px - predX[j];

				float dy =
					py - predY[j];

				float distanceSquared =
					dx * dx +
					dy * dy;

				neighborDx[index] =
					dx;

				neighborDy[index] =
					dy;

				if (
					distanceSquared <=
					0.000001f
				)
				{
					neighborQ[index] =
						1.0f;

					neighborGradientScale[index] =
						0.0f;

					continue;
				}

				float inverseDistance =
					1.0f /
					Mathf.Sqrt(
						distanceSquared
					);

				float distance =
					distanceSquared *
					inverseDistance;

				float q =
					1.0f -
					distance /
					SmoothingRadius;

				if (q < 0.0f)
					q = 0.0f;

				neighborQ[index] =
					q;

				float q2 =
					q * q;

				neighborGradientScale[index] =
					(
						-3.0f *
						q2 /
						SmoothingRadius
					) *
					inverseDistance *
					InverseRestDensity;
			}

			bufferWritePosition =
				end;
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

		for (int i = 0; i < count; i++)
		{
			int offset =
				neighborOffsets[i];

			int neighborCount =
				neighborCounts[i];

			int end =
				offset + neighborCount;

			float density =
				0.0f;

			float gradSumX =
				0.0f;

			float gradSumY =
				0.0f;

			float neighborGradientSquared =
				0.0f;

			for (
				int index = offset;
				index < end;
				index++)
			{
				int j =
					neighborBuffer[index];

				float q =
					neighborQ[index];

				// q³
				density +=
					q * q * q;

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
				Mathf.Abs(
					constraint
				);

			if (
				absoluteConstraint >
				maximumDensityError
			)
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
		for (int i = 0; i < count; i++)
		{
			Vector2 position =
				new Vector2(
					predX[i],
					predY[i]
				);

			for (
				int c = 0;
				c < polygonColliders.Count;
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
					)
				)
				{
					continue;
				}

				position =
					correctedPosition;
			}

			predX[i] =
				position.X;

			predY[i] =
				position.Y;
		}
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
			WakeVelocityThresholdSquared
		)
		{
			sleepProgress[i] = 0.0f;
			sleeping[i] = false;
			return;
		}

		if (
			velocitySquared <
			SleepVelocityThresholdSquared
		)
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
			float x =
				predX[i];

			float y =
				predY[i];

			if (x < left)
				x = left;
			else if (x > right)
				x = right;

			if (y < top)
				y = top;
			else if (y > bottom)
				y = bottom;

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
			lambdas.Length >= count
		)
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

		neighborOffsets =
			new int[count];

		neighborCounts =
			new int[count];

		int capacity =
			Math.Max(
				1,
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

	private void EnsureNeighborCapacity(
		int required)
	{
		if (
			neighborBuffer.Length >=
			required
		)
		{
			return;
		}

		int newCapacity =
			Math.Max(
				neighborBuffer.Length * 2,
				required
			);

		Array.Resize(
			ref neighborBuffer,
			newCapacity
		);

		Array.Resize(
			ref neighborDx,
			newCapacity
		);

		Array.Resize(
			ref neighborDy,
			newCapacity
		);

		Array.Resize(
			ref neighborQ,
			newCapacity
		);

		Array.Resize(
			ref neighborGradientScale,
			newCapacity
		);
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

			$"MaxDensityError={maxObservedDensityError:F4} " +

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
