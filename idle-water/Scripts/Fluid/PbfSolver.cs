
using Godot;
using System;
using System.Collections.Generic;

public class PbfSolver
{
	private readonly SpatialHash hash;

	private readonly List<FluidPolygonCollider>
		polygonColliders;

	// ============================================================
	// Simulation
	// ============================================================

	private const float Gravity = 300.0f;

	// IMPORTANT:
	// This is the actual PBF smoothing radius.
	private const float SmoothingRadius = 8.0f;
	private const float SmoothingRadiusSquared = 64.0f;
	private const float InverseSmoothingRadius = 1.0f / 8.0f;

	// ============================================================
	// Density
	// ============================================================

	private const float RestDensity = 1.15f;
	private const float InverseRestDensity = 1.0f / RestDensity;
	private const float LambdaEpsilon = 0.00001f;

	// ============================================================
	// PBF
	// ============================================================

	// Optimization:
	//
	// The old solver could perform 3 iterations.
	// Profiling showed that PBF dominates the frame time.
	//
	// Two iterations are already the minimum required by the
	// existing solver, so we remove the expensive third pass.
	private const int MinIterations = 2;
	private const int MaxIterations = 2;

	private const float DensityErrorThreshold = 0.75f;

	private const float MaxCorrection = 0.5f;
	private const float MaxCorrectionSquared = 0.25f;

	// ============================================================
	// Stability
	// ============================================================

	private const float VelocityDamping = 0.996f;

	// ============================================================
	// Impact
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
		SleepVelocityThreshold *
		SleepVelocityThreshold;

	private const float WakeVelocityThresholdSquared =
		WakeVelocityThreshold *
		WakeVelocityThreshold;

	// ============================================================
	// Current simulation world
	// ============================================================

	private const float MinX = 260.0f;
	private const float MaxX = 1180.0f;

	private const float MinY = -200.0f;
	private const float MaxY = 820.0f;

	private const float BoundarySkin = 0.5f;

	private const float BoundaryRestitution = 0.03f;
	private const float BoundaryFriction = 0.03f;

	private const float BoundaryVelocityEpsilon = 0.5f;

	// ============================================================
	// Polygon
	// ============================================================

	private const float PolygonParticleRadius = 2.5f;

	// ============================================================
	// Neighbors
	// ============================================================

	private const int MaxNeighbors = 40;

	private int neighborStride;

	// ============================================================
	// Working arrays
	// ============================================================

	private float[] lambdas;
	private float[] particleDensity;

	private float[] sleepProgress;
	private bool[] sleeping;

	public bool[] SurfaceParticles;

	private float[] impactNormalX;
	private float[] impactNormalY;
	private bool[] impacted;

	// ============================================================
	// Neighbor cache
	// ============================================================

	private int[] neighborBuffer;
	private int[] neighborCounts;

	private float[] neighborDx;
	private float[] neighborDy;

	private float[] neighborQ;
	private float[] neighborGradientScale;

	// ============================================================
	// Wheel
	// ============================================================

	private FluidWheelState wheel;

	public FluidWheelState Wheel =>
		wheel;

	// ============================================================
	// Constructor
	// ============================================================

	public PbfSolver(
		SpatialHash spatialHash)
	{
		hash =
			spatialHash;

		polygonColliders =
			new List<FluidPolygonCollider>();
	}

	// ============================================================
	// Create wheel
	// ============================================================

	public FluidWheelState CreateWheel(
		Vector2 center)
	{
		wheel =
			new FluidWheelState(
				center
			);

		return wheel;
	}

	// ============================================================
	// Add collider
	// ============================================================

	public void AddPolygonCollider(
		FluidPolygonCollider collider)
	{
		if (collider == null)
			return;

		if (!polygonColliders.Contains(collider))
		{
			polygonColliders.Add(
				collider
			);
		}
	}

	// ============================================================
	// Clear terrain colliders
	//
	// Wheel colliders must survive terrain rebuilds.
	// ============================================================

	public void ClearPolygonColliders()
	{
		for (
			int i = polygonColliders.Count - 1;
			i >= 0;
			i--)
		{
			FluidPolygonCollider collider =
				polygonColliders[i];

			if (
				collider == null ||
				!collider.IsWheel)
			{
				polygonColliders.RemoveAt(i);
			}
		}
	}

	// ============================================================
	// Main solve
	// ============================================================

	public void Solve(
		ParticleData particles,
		float dt)
	{
		int count =
			particles.Count;

		if (
			count <= 0 ||
			dt <= 0.0f)
		{
			if (wheel != null)
				wheel.Step(dt);

			return;
		}

		EnsureBuffers(count);

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

		Array.Clear(
			impacted,
			0,
			count
		);

		Array.Clear(
			impactNormalX,
			0,
			count
		);

		Array.Clear(
			impactNormalY,
			0,
			count
		);

		// --------------------------------------------------------
		// Rotate wheel before collision detection.
		// --------------------------------------------------------

		if (wheel != null)
		{
			wheel.Step(dt);

			for (
				int i = 0;
				i < polygonColliders.Count;
				i++)
			{
				FluidPolygonCollider collider =
					polygonColliders[i];

				if (
					collider != null &&
					collider.IsWheel)
				{
					collider.UpdateWheelGeometry();
				}
			}
		}

		// --------------------------------------------------------
		// Predict
		// --------------------------------------------------------

		float gravityDt =
			Gravity * dt;

		for (
			int i = 0;
			i < count;
			i++)
		{
			velY[i] +=
				gravityDt;

			predX[i] =
				posX[i] +
				velX[i] * dt;

			predY[i] =
				posY[i] +
				velY[i] * dt;
		}

		// --------------------------------------------------------
		// Spatial hash
		// --------------------------------------------------------

		hash.Clear();

		for (
			int i = 0;
			i < count;
			i++)
		{
			hash.Insert(
				i,
				predX[i],
				predY[i]
			);
		}

		BuildNeighborCache(
			predX,
			predY,
			count
		);

		// --------------------------------------------------------
		// PBF iterations
		//
		// Optimization:
		// MaxIterations is now 2 instead of 3.
		// --------------------------------------------------------

		for (
			int iteration = 0;
			iteration < MaxIterations;
			iteration++)
		{
			if (iteration > 0)
			{
				UpdateNeighborCache(
					predX,
					predY,
					count
				);
			}

			float densityError =
				CalculateLambdas(
					count
				);

			ApplyPositionCorrections(
				predX,
				predY,
				count
			);

			// ----------------------------------------------------
			// Polygon / wheel collision
			// ----------------------------------------------------

			if (
				polygonColliders.Count > 0)
			{
				ConstrainToPolygonColliders(
					predX,
					predY,
					velX,
					velY,
					count,
					dt
				);
			}

			// ----------------------------------------------------
			// World bounds
			// ----------------------------------------------------

			ConstrainToBounds(
				predX,
				predY,
				count
			);

			if (
				iteration + 1 >= MinIterations &&
				densityError <=
				DensityErrorThreshold)
			{
				break;
			}
		}

		// --------------------------------------------------------
		// Reconstruct velocity
		// --------------------------------------------------------

		float inverseDt =
			1.0f / dt;

		float boundaryLeft =
			MinX + BoundarySkin;

		float boundaryTop =
			MinY + BoundarySkin;

		float boundaryBottom =
			MaxY - BoundarySkin;

		for (
			int i = 0;
			i < count;
			i++)
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

			float x =
				predX[i];

			float y =
				predY[i];

			// ----------------------------------------------------
			// Left boundary
			// ----------------------------------------------------

			if (
				x <=
				boundaryLeft + 0.001f)
			{
				if (
					finalVelocityX < 0.0f)
				{
					if (
						Mathf.Abs(
							finalVelocityX
						) <
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
					1.0f -
					BoundaryFriction;
			}

			// ----------------------------------------------------
			// Top boundary
			// ----------------------------------------------------

			if (
				y <=
				boundaryTop + 0.001f)
			{
				if (
					finalVelocityY < 0.0f)
				{
					finalVelocityY =
						-finalVelocityY *
						BoundaryRestitution;
				}

				finalVelocityX *=
					1.0f -
					BoundaryFriction;
			}

			// ----------------------------------------------------
			// Bottom boundary
			// ----------------------------------------------------

			else if (
				y >=
				boundaryBottom - 0.001f)
			{
				if (
					finalVelocityY > 0.0f)
				{
					finalVelocityY =
						-finalVelocityY *
						BoundaryRestitution;
				}

				finalVelocityX *=
					1.0f -
					BoundaryFriction;
			}

			// ----------------------------------------------------
			// Impact damping
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

			velX[i] =
				finalVelocityX;

			velY[i] =
				finalVelocityY;

			posX[i] =
				predX[i];

			posY[i] =
				predY[i];
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
		for (
			int i = 0;
			i < count;
			i++)
		{
			float correctionX = 0.0f;
			float correctionY = 0.0f;

			int start =
				i * neighborStride;

			int end =
				start +
				neighborCounts[i];

			float lambdaI =
				lambdas[i];

			for (
				int index = start;
				index < end;
				index++)
			{
				int j =
					neighborBuffer[index];

				if (j == i)
					continue;

				float lambdaSum =
					lambdaI +
					lambdas[j];

				float scale =
					lambdaSum *
					neighborGradientScale[index];

				correctionX +=
					scale *
					neighborDx[index];

				correctionY +=
					scale *
					neighborDy[index];
			}

			float lengthSquared =
				correctionX *
				correctionX +
				correctionY *
				correctionY;

			if (
				lengthSquared >
				MaxCorrectionSquared)
			{
				float inverseLength =
					1.0f /
					MathF.Sqrt(
						lengthSquared
					);

				float scale =
					MaxCorrection *
					inverseLength;

				correctionX *=
					scale;

				correctionY *=
					scale;
			}

			predX[i] +=
				correctionX;

			predY[i] +=
				correctionY;
		}
	}

	// ============================================================
	// Polygon collision + wheel torque
	// ============================================================

	private void ConstrainToPolygonColliders(
		float[] predX,
		float[] predY,
		float[] velX,
		float[] velY,
		int count,
		float dt)
	{
		for (
			int i = 0;
			i < count;
			i++)
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
					))
				{
					continue;
				}

				// ------------------------------------------------
				// Wheel interaction
				// ------------------------------------------------

				if (collider.IsWheel)
				{
					ApplyWheelTorque(
						collider,
						position,
						normal,
						velX[i],
						velY[i],
						dt
					);
				}

				position =
					correctedPosition;

				if (
					normal.LengthSquared() >
					ImpactNormalEpsilon)
				{
					accumulatedNormal +=
						normal;

					particleImpacted =
						true;
				}
			}

			predX[i] =
				position.X;

			predY[i] =
				position.Y;

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
	// Wheel torque
	// ============================================================

	private void ApplyWheelTorque(
		FluidPolygonCollider collider,
		Vector2 contactPosition,
		Vector2 normal,
		float velocityX,
		float velocityY,
		float dt)
	{
		FluidWheelState wheelState =
			collider.Wheel;

		if (wheelState == null)
			return;

		Vector2 wheelVelocity =
			wheelState.GetSurfaceVelocity(
				contactPosition
			);

		Vector2 particleVelocity =
			new Vector2(
				velocityX,
				velocityY
			);

		Vector2 relativeVelocity =
			particleVelocity -
			wheelVelocity;

		Vector2 tangent =
			new Vector2(
				-normal.Y,
				normal.X
			);

		float tangentialVelocity =
			relativeVelocity.Dot(
				tangent
			);

		float impulse =
			tangentialVelocity *
			0.15f;

		Vector2 radius =
			contactPosition -
			wheelState.Center;

		float torque =
			radius.X *
			(tangent.Y * impulse) -
			radius.Y *
			(tangent.X * impulse);

		wheelState.AddTorque(
			torque
		);
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
			velocityX *
			normalX +
			velocityY *
			normalY;

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
	// Bounds
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

		for (
			int i = 0;
			i < count;
			i++)
		{
			float x =
				predX[i];

			float y =
				predY[i];

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

			predX[i] =
				x;

			predY[i] =
				y;
		}
	}

	// ============================================================
	// Sleep
	// ============================================================

	private void ApplySleepBehavior(
		int i,
		float dt,
		ref float velocityX,
		ref float velocityY)
	{
		float velocitySquared =
			velocityX *
			velocityX +
			velocityY *
			velocityY;

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
				dt /
				SleepTime;

			if (sleepProgress[i] > 1.0f)
				sleepProgress[i] = 1.0f;

			float damping =
				1.0f -
				SleepDampingStrength *
				sleepProgress[i] *
				dt;

			if (damping < 0.0f)
				damping = 0.0f;

			velocityX *=
				damping;

			velocityY *=
				damping;

			if (
				sleepProgress[i] >=
				1.0f)
			{
				sleeping[i] = true;

				velocityX = 0.0f;
				velocityY = 0.0f;
			}

			return;
		}

		sleepProgress[i] -=
			dt /
			SleepTime;

		if (sleepProgress[i] < 0.0f)
			sleepProgress[i] = 0.0f;

		sleeping[i] = false;
	}

	// ============================================================
	// Neighbor cache
	// ============================================================

	private void BuildNeighborCache(
		float[] predX,
		float[] predY,
		int count)
	{
		for (
			int i = 0;
			i < count;
			i++)
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
					neighborBuffer,
					start,
					MaxNeighbors
				);

			neighborCounts[i] =
				neighborCount;

			int end =
				start +
				neighborCount;

			for (
				int index = start;
				index < end;
				index++)
			{
				UpdateNeighborGeometry(
					index,
					px,
					py,
					predX,
					predY
				);
			}
		}
	}

	private void UpdateNeighborCache(
		float[] predX,
		float[] predY,
		int count)
	{
		for (
			int i = 0;
			i < count;
			i++)
		{
			float px =
				predX[i];

			float py =
				predY[i];

			int start =
				i * neighborStride;

			int end =
				start +
				neighborCounts[i];

			for (
				int index = start;
				index < end;
				index++)
			{
				UpdateNeighborGeometry(
					index,
					px,
					py,
					predX,
					predY
				);
			}
		}
	}

	private void UpdateNeighborGeometry(
		int index,
		float px,
		float py,
		float[] predX,
		float[] predY)
	{
		int j =
			neighborBuffer[index];

		float dx =
			px -
			predX[j];

		float dy =
			py -
			predY[j];

		float distanceSquared =
			dx * dx +
			dy * dy;

		neighborDx[index] =
			dx;

		neighborDy[index] =
			dy;

		if (
			distanceSquared <=
			0.000001f)
		{
			neighborQ[index] =
				1.0f;

			neighborGradientScale[index] =
				0.0f;

			return;
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
			neighborQ[index] =
				0.0f;

			neighborGradientScale[index] =
				0.0f;

			return;
		}

		neighborQ[index] =
			q;

		float q2 =
			q * q;

		neighborGradientScale[index] =
			-3.0f *
			q2 *
			InverseSmoothingRadius *
			inverseDistance *
			InverseRestDensity;
	}

	// ============================================================
	// Lambdas
	// ============================================================

	private float CalculateLambdas(
		int count)
	{
		float maximumDensityError =
			0.0f;

		for (
			int i = 0;
			i < count;
			i++)
		{
			int start =
				i * neighborStride;

			int end =
				start +
				neighborCounts[i];

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
					neighborQ[index];

				float q2 =
					q * q;

				density +=
					q2 * q;

				int j =
					neighborBuffer[index];

				if (j == i)
					continue;

				float scale =
					neighborGradientScale[index];

				float gx =
					neighborDx[index] *
					scale;

				float gy =
					neighborDy[index] *
					scale;

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
				count *
				MaxNeighbors
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

		for (
			int i = 0;
			i < particles.Count;
			i++)
		{
			field.AddDensity(
				particles.PredX[i],
				particles.PredY[i],
				particleDensity[i]
			);
		}
	}
}
