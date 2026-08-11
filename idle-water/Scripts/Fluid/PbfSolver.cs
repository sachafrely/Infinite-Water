
using Godot;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public class PbfSolver
{
	private readonly SpatialHash hash;

	private readonly List<FluidPolygonCollider>
		polygonColliders;

	private readonly List<FluidPolygonCollider>
		wheelColliders;

	private List<int>[] colliderGrid;
	private int colliderGridWidth;
	private int colliderGridHeight;
	private bool colliderGridDirty = true;
	private int[] colliderQueryStamp;
	private int colliderQueryId;

	// ============================================================
	// Simulation
	//
	// ============================================================
	
	private const float Gravity = 300.0f;

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

	private const int MinIterations = 2;
	private const int MaxIterations = 3;

	// Conservative adaptive convergence threshold.
	// After the mandatory two PBF iterations, a third iteration is only
	// needed when the remaining density error is significant.
	private const float DensityErrorThreshold = 0.90f;

	private const float MaxCorrection = 0.5f;
	private const float MaxCorrectionSquared = 0.25f;

	// ============================================================
	// Stability
	// ============================================================

	private const float VelocityDamping = 0.998f;

	// ============================================================
	// Impact
	// ============================================================

	private const float ImpactDamping = 0.5f;
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
	//
	// Camera:
	//     X = 360 .. 1080
	//     Y = 0 .. 720
	//
	// Buffered simulation:
	//     Left   = 260
	//     Right  = 1180
	//     Top    = -200
	//     Bottom = 820
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
	private const float ColliderGridCellSize = 64.0f;

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

		wheelColliders =
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

			if (collider.IsWheel)
				wheelColliders.Add(collider);

			colliderGridDirty = true;
		}
	}

	// ============================================================
	// Clear terrain colliders
	//
	// IMPORTANT:
	// TileMapPhysics rebuilds the terrain by calling this.
	//
	// Wheel colliders must NOT be deleted here.
	// ============================================================

	public void ClearPolygonColliders()
	{
		for (int i = polygonColliders.Count - 1; i >= 0; i--)
		{
			FluidPolygonCollider collider = polygonColliders[i];

			if (collider == null || !collider.IsWheel)
			{
				polygonColliders.RemoveAt(i);
				colliderGridDirty = true;
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

			for (int i = 0; i < wheelColliders.Count; i++)
			{
				FluidPolygonCollider collider = wheelColliders[i];
				if (collider != null)
					collider.UpdateWheelGeometry();
			}
		}

		// --------------------------------------------------------
		// Prepare static terrain collider grid.
		// --------------------------------------------------------
		if (colliderGridDirty)
			RebuildColliderGrid();

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
		// --------------------------------------------------------

		// The first two iterations are mandatory for stability.
		// The third iteration is adaptive: CalculateLambdas() reports the
		// worst density error before correction, so after the mandatory
		// second correction we can safely stop when the remaining error
		// is already small enough.
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
		// Local references keep the hot inner loop from repeatedly
		// resolving instance fields. No physics behavior is changed.
		float[] localLambdas =
			lambdas;

		int[] localNeighborBuffer =
			neighborBuffer;

		int[] localNeighborCounts =
			neighborCounts;

		float[] localGradientScale =
			neighborGradientScale;

		float[] localDx =
			neighborDx;

		float[] localDy =
			neighborDy;

		int stride =
			neighborStride;

		for (
			int i = 0;
			i < count;
			i++)
		{
			float correctionX = 0.0f;
			float correctionY = 0.0f;

			int start =
				i * stride;

			int end =
				start +
				localNeighborCounts[i];

			float lambdaI =
				localLambdas[i];

			for (
				int index = start;
				index < end;
				index++)
			{
				int j =
					localNeighborBuffer[index];

				float scale =
					(lambdaI + localLambdas[j]) *
					localGradientScale[index];

				correctionX +=
					scale *
					localDx[index];

				correctionY +=
					scale *
					localDy[index];
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
		if (colliderGrid == null)
			return;

		if (colliderQueryId == int.MaxValue)
		{
			Array.Clear(colliderQueryStamp, 0, colliderQueryStamp.Length);
			colliderQueryId = 1;
		}

		for (int i = 0; i < count; i++)
		{
			colliderQueryId++;
			if (colliderQueryId == int.MaxValue)
			{
				Array.Clear(colliderQueryStamp, 0, colliderQueryStamp.Length);
				colliderQueryId = 1;
			}

			Vector2 position = new Vector2(predX[i], predY[i]);
			Vector2 accumulatedNormal = Vector2.Zero;
			bool particleImpacted = false;

			int baseCellX = GetColliderCellX(position.X);
			int baseCellY = GetColliderCellY(position.Y);

			// Query neighboring cells because the collision radius can cross a cell boundary.
			for (int cy = baseCellY - 1; cy <= baseCellY + 1; cy++)
			{
				if (cy < 0 || cy >= colliderGridHeight)
					continue;

				for (int cx = baseCellX - 1; cx <= baseCellX + 1; cx++)
				{
					if (cx < 0 || cx >= colliderGridWidth)
						continue;

					List<int> cell = colliderGrid[cy * colliderGridWidth + cx];
					if (cell == null)
						continue;

					for (int k = 0; k < cell.Count; k++)
					{
						int c = cell[k];
						if (colliderQueryStamp[c] == colliderQueryId)
							continue;
						colliderQueryStamp[c] = colliderQueryId;

						FluidPolygonCollider collider = polygonColliders[c];
						if (collider == null || collider.IsWheel)
							continue;

						if (!collider.ResolveCollision(position, PolygonParticleRadius, out Vector2 correctedPosition, out Vector2 normal))
							continue;

						position = correctedPosition;

						if (normal.LengthSquared() > ImpactNormalEpsilon)
						{
							accumulatedNormal += normal;
							particleImpacted = true;
						}
					}
				}
			}

			// Wheels remain in their own tiny list so the terrain grid never misses a moving wheel.
			for (int w = 0; w < wheelColliders.Count; w++)
			{
				FluidPolygonCollider collider = wheelColliders[w];
				if (collider == null)
					continue;

				if (!collider.ResolveCollision(position, PolygonParticleRadius, out Vector2 correctedPosition, out Vector2 normal))
					continue;

				ApplyWheelTorque(collider, position, normal, velX[i], velY[i], dt);
				position = correctedPosition;

				if (normal.LengthSquared() > ImpactNormalEpsilon)
				{
					accumulatedNormal += normal;
					particleImpacted = true;
				}
			}

			predX[i] = position.X;
			predY[i] = position.Y;

			if (particleImpacted)
			{
				float normalLengthSquared = accumulatedNormal.LengthSquared();
				if (normalLengthSquared > ImpactNormalEpsilon)
				{
					float inverseLength = 1.0f / Mathf.Sqrt(normalLengthSquared);
					accumulatedNormal *= inverseLength;
					impactNormalX[i] = accumulatedNormal.X;
					impactNormalY[i] = accumulatedNormal.Y;
					impacted[i] = true;
				}
			}
		}
	}

	// ============================================================
	// Collider grid
	// ============================================================
	private void RebuildColliderGrid()
	{
		colliderGridWidth = Math.Max(1, (int)MathF.Ceiling((MaxX - MinX) / ColliderGridCellSize));
		colliderGridHeight = Math.Max(1, (int)MathF.Ceiling((MaxY - MinY) / ColliderGridCellSize));

		int cellCount = colliderGridWidth * colliderGridHeight;
		if (colliderGrid == null || colliderGrid.Length != cellCount)
		{
			colliderGrid = new List<int>[cellCount];
			for (int i = 0; i < cellCount; i++)
				colliderGrid[i] = new List<int>(4);
		}
		else
		{
			for (int i = 0; i < cellCount; i++)
				colliderGrid[i]?.Clear();
		}

		colliderQueryStamp = colliderQueryStamp == null || colliderQueryStamp.Length != polygonColliders.Count
			? new int[polygonColliders.Count]
			: colliderQueryStamp;

		float expansion = PolygonParticleRadius + 1.0f;
		for (int i = 0; i < polygonColliders.Count; i++)
		{
			FluidPolygonCollider collider = polygonColliders[i];
			if (collider == null || collider.IsWheel)
				continue;

			collider.GetBounds(out float minX, out float maxX, out float minY, out float maxY);
			int minCellX = GetColliderCellX(minX - expansion);
			int maxCellX = GetColliderCellX(maxX + expansion);
			int minCellY = GetColliderCellY(minY - expansion);
			int maxCellY = GetColliderCellY(maxY + expansion);

			for (int y = minCellY; y <= maxCellY; y++)
			{
				for (int x = minCellX; x <= maxCellX; x++)
					colliderGrid[y * colliderGridWidth + x].Add(i);
			}
		}

		colliderGridDirty = false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private int GetColliderCellX(float x)
	{
		int cell = (int)MathF.Floor((x - MinX) / ColliderGridCellSize);
		if (cell < 0) return 0;
		if (cell >= colliderGridWidth) return colliderGridWidth - 1;
		return cell;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private int GetColliderCellY(float y)
	{
		int cell = (int)MathF.Floor((y - MinY) / ColliderGridCellSize);
		if (cell < 0) return 0;
		if (cell >= colliderGridHeight) return colliderGridHeight - 1;
		return cell;
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

			// ----------------------------------------------------
			// LEFT
			// ----------------------------------------------------

			if (x < left)
			{
				x = left;

				impacted[i] = true;
				impactNormalX[i] = 1.0f;
				impactNormalY[i] = 0.0f;
			}

			// ----------------------------------------------------
			// RIGHT
			// ----------------------------------------------------

			else if (x > right)
			{
				x = right;

				impacted[i] = true;
				impactNormalX[i] = -1.0f;
				impactNormalY[i] = 0.0f;
			}

			// ----------------------------------------------------
			// TOP
			// ----------------------------------------------------

			if (y < top)
			{
				y = top;

				impacted[i] = true;
				impactNormalX[i] = 0.0f;
				impactNormalY[i] = 1.0f;
			}

			// ----------------------------------------------------
			// BOTTOM
			// ----------------------------------------------------

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
				hash.QueryPbfWithGeometry(
					px,
					py,
					predX,
					predY,
					neighborBuffer,
					neighborDx,
					neighborDy,
					neighborQ,
					neighborGradientScale,
					start,
					MaxNeighbors
				);

			neighborCounts[i] =
				neighborCount;


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

			UpdateNeighborGeometryRange(
				start,
				end,
				px,
				py,
				predX,
				predY
			);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void UpdateNeighborGeometryRange(
		int start,
		int end,
		float px,
		float py,
		float[] predX,
		float[] predY)
	{
		int[] localNeighbors = neighborBuffer;
		float[] localDx = neighborDx;
		float[] localDy = neighborDy;
		float[] localQ = neighborQ;
		float[] localGradient = neighborGradientScale;

		for (int index = start; index < end; index++)
		{
			int j = localNeighbors[index];
			float dx = px - predX[j];
			float dy = py - predY[j];
			float distanceSquared = dx * dx + dy * dy;

			localDx[index] = dx;
			localDy[index] = dy;

			if (distanceSquared <= 0.000001f)
			{
				localQ[index] = 1.0f;
				localGradient[index] = 0.0f;
				continue;
			}

			float inverseDistance = 1.0f / MathF.Sqrt(distanceSquared);
			float q = 1.0f - (distanceSquared * inverseDistance) * InverseSmoothingRadius;
			float q2 = q * q;
			localQ[index] = q;
			localGradient[index] =
				-3.0f * q2 * InverseSmoothingRadius * inverseDistance * InverseRestDensity;
		}
	}

	// ============================================================
	// Lambdas
	// ============================================================

	private float CalculateLambdas(
		int count)
	{
		float maximumDensityError =
			0.0f;

		// Local references for the hot neighbor loop.
		float[] localNeighborQ =
			neighborQ;

		float[] localNeighborGradientScale =
			neighborGradientScale;

		float[] localNeighborDx =
			neighborDx;

		float[] localNeighborDy =
			neighborDy;

		int[] localNeighborCounts =
			neighborCounts;

		float[] localParticleDensity =
			particleDensity;

		float[] localLambdas =
			lambdas;

		float inverseRestDensity = InverseRestDensity;

		int stride =
			neighborStride;

		for (
			int i = 0;
			i < count;
			i++)
		{
			int start =
				i * stride;

			int end =
				start +
				localNeighborCounts[i];

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
					localNeighborQ[index];

				float q2 =
					q * q;

				density +=
					q2 * q;

				float scale =
					localNeighborGradientScale[index];

				float gx =
					localNeighborDx[index] *
					scale;

				float gy =
					localNeighborDy[index] *
					scale;

				gradSumX +=
					gx;

				gradSumY +=
					gy;

				neighborGradientSquared +=
					gx * gx +
					gy * gy;
			}

			localParticleDensity[i] =
				density;

			float constraint =
				density *
				inverseRestDensity -
				1.0f;

			float absoluteConstraint =
				constraint < 0.0f ? -constraint : constraint;

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

			localLambdas[i] =
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
