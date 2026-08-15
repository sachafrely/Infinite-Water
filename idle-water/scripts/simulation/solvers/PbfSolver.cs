using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

/// <summary>
/// PbfSolver — public entry point and collision-management service for the
/// Position-Based Fluids pipeline.
///
/// After Phase 3 extraction this class owns:
/// <list type="bullet">
///   <item>The public API consumed by <see cref="FluidSimulator"/>
///         (AddPolygonCollider, ClearPolygonColliders, CreateWheel, Solve).</item>
///   <item>The terrain and wheel collider grid + broad-phase management.</item>
///   <item>ConstrainToPolygonColliders — the polygon + wheel collision loop,
///         exposed as <see cref="ApplyPolygonCollision"/> for the coordinator.</item>
/// </list>
///
/// Per-step mutable state (lambdas, neighbor arrays, impact normals, etc.) has
/// moved to <see cref="PbfState"/> which is owned by the coordinator.
/// Sub-pass math lives in the modules under
/// <c>scripts/simulation/solvers/pbf/</c>.
///
/// Constants are defined in <c>PbfConstants.cs</c> (partial class).
/// </summary>
public partial class PbfSolver
{
	// ============================================================
	// Coordinator (owns PbfState and the solve loop)
	// ============================================================

	private readonly PbfSolverCoordinator coordinator;

	// ============================================================
	// Collider lists
	// ============================================================

	private readonly SpatialHash hash;

	private readonly List<FluidPolygonCollider> polygonColliders;
	private readonly List<FluidPolygonCollider> wheelColliders;

	// ============================================================
	// Collider grid
	// ============================================================

	private List<int>[] colliderGrid;
	private int colliderGridWidth;
	private int colliderGridHeight;
	private bool colliderGridDirty = true;

	private float[] colliderMinX;
	private float[] colliderMaxX;
	private float[] colliderMinY;
	private float[] colliderMaxY;

	private int[] terrainColliderQueryStamp;
	private int terrainColliderQueryStampId = 0;

	// ============================================================
	// Wheel bounds
	// ============================================================

	private float[] wheelMinX;
	private float[] wheelMaxX;
	private float[] wheelMinY;
	private float[] wheelMaxY;

	// ============================================================
	// Wheel collision groups
	// ============================================================

	private readonly List<WheelCollisionGroup>
		wheelCollisionGroups =
			new List<WheelCollisionGroup>();

	private sealed class WheelCollisionGroup
	{
		public FluidWheelState Wheel;

		public readonly List<FluidPolygonCollider>
			Colliders =
			new List<FluidPolygonCollider>(9);

		public readonly List<int>
			ColliderIndices =
			new List<int>(9);

		public float MinX;
		public float MaxX;
		public float MinY;
		public float MaxY;
	}

	// ============================================================
	// Wheel
	// ============================================================

	private FluidWheelState wheel;

	public FluidWheelState Wheel =>
		wheel;

	// ============================================================
	// Surface particles (per-step, lives in PbfState)
	// ============================================================

	/// <summary>
	/// Per-particle flag set after the neighbor search indicating which
	/// particles are on the fluid surface (low local density).
	/// Backed by <see cref="PbfState.SurfaceParticles"/>.
	/// </summary>
	public bool[] SurfaceParticles =>
		coordinator.State.SurfaceParticles;

	// ============================================================
	// Constructor
	// ============================================================

	public PbfSolver(
		SpatialHash spatialHash)
	{
		hash = spatialHash;

		polygonColliders =
			new List<FluidPolygonCollider>();

		wheelColliders =
			new List<FluidPolygonCollider>();

		coordinator =
			new PbfSolverCoordinator(
				spatialHash,
				this
			);
	}

	// ============================================================
	// Add / clear colliders
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

	public void AddPolygonCollider(
		FluidPolygonCollider collider)
	{
		if (collider == null)
			return;

		if (polygonColliders.Contains(collider))
			return;

		polygonColliders.Add(collider);

		if (collider.IsWheel)
		{
			wheelColliders.Add(collider);

			RegisterWheelCollider(collider);

			EnsureWheelBounds();

			colliderGridDirty = true;
		}
		else
		{
			colliderGridDirty = true;
		}
	}

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

				colliderGridDirty = true;
			}
		}
	}

	// ============================================================
	// Main entry point — delegates to PbfSolverCoordinator
	// ============================================================

	public void Solve(
		ParticleData particles,
		float dt)
	{
		coordinator.Solve(particles, dt);
	}

	// ============================================================
	// Internal helpers used by PbfSolverCoordinator
	// ============================================================

	/// <summary>Returns true if there are any polygon colliders registered.</summary>
	internal bool HasPolygonColliders =>
		polygonColliders.Count > 0;

	/// <summary>Advances the wheel simulation one tick (call when count == 0 too).</summary>
	internal void StepWheel(float dt)
	{
		if (wheel != null)
			wheel.Step(dt);
	}

	/// <summary>
	/// Steps the wheel, refreshes wheel collider geometry, and updates
	/// wheel bounds.  Call once per physics tick before collision.
	/// </summary>
	internal void StepWheelAndUpdateColliders(float dt)
	{
		if (wheel == null)
			return;

		wheel.Step(dt);

		int wheelCount = wheelColliders.Count;

		for (int i = 0; i < wheelCount; i++)
		{
			FluidPolygonCollider collider =
				wheelColliders[i];

			if (collider != null)
				collider.UpdateWheelGeometry();
		}

		UpdateWheelBounds();
	}

	/// <summary>
	/// Rebuilds the collider grid if it has been dirtied by a collider
	/// add/remove.
	/// </summary>
	internal void EnsureColliderGrid()
	{
		if (colliderGridDirty)
			RebuildColliderGrid();
	}

	/// <summary>
	/// Runs the polygon-collider and wheel-collider constraint loop,
	/// writing results to the predicted-position arrays and to the impact-
	/// normal arrays inside <paramref name="state"/>.
	/// </summary>
	internal void ApplyPolygonCollision(
		float[] predX,
		float[] predY,
		float[] startX,
		float[] startY,
		float[] velX,
		float[] velY,
		int count,
		float dt,
		bool useSweptTerrain,
		ref double terrainQueryMs,
		ref double terrainResolveMs,
		ref double wheelCollisionMs)
	{
		PbfState state = coordinator.State;

		ConstrainToPolygonColliders(
			predX, predY,
			startX, startY,
			velX, velY,
			count, dt,
			useSweptTerrain,
			state,
			ref terrainQueryMs,
			ref terrainResolveMs,
			ref wheelCollisionMs
		);
	}

	// ============================================================
	// Polygon collision (core loop)
	// ============================================================

	private void ConstrainToPolygonColliders(
		float[] predX,
		float[] predY,
		float[] startX,
		float[] startY,
		float[] velX,
		float[] velY,
		int count,
		float dt,
		bool useSweptTerrain,
		PbfState state,
		ref double terrainQueryMs,
		ref double terrainResolveMs,
		ref double wheelCollisionMs)
	{
		if (colliderGrid == null)
			return;

		List<int>[] localGrid = colliderGrid;
		int localGridWidth = colliderGridWidth;
		int localGridHeight = colliderGridHeight;
		List<FluidPolygonCollider> localColliders = polygonColliders;
		List<WheelCollisionGroup> localWheelGroups = wheelCollisionGroups;

		bool measureWheelCollision =
			localWheelGroups.Count > 0;

		long wheelCollisionStart =
			measureWheelCollision
				? Stopwatch.GetTimestamp()
				: 0;

		float particleRadius = PolygonParticleRadius;
		float terrainMargin =
			particleRadius +
			TerrainBoundsExtraMargin;

		int localStampId =
			terrainColliderQueryStampId;

		int[] localStamps =
			terrainColliderQueryStamp;

		float[] localColliderMinX = colliderMinX;
		float[] localColliderMaxX = colliderMaxX;
		float[] localColliderMinY = colliderMinY;
		float[] localColliderMaxY = colliderMaxY;

		bool[] impacted = state.Impacted;
		float[] impactNormalX = state.ImpactNormalX;
		float[] impactNormalY = state.ImpactNormalY;

		int wheelGroupCount = localWheelGroups.Count;

		for (int i = 0; i < count; i++)
		{
			float currentX = predX[i];
			float currentY = predY[i];
			float previousX = startX[i];
			float previousY = startY[i];

			Vector2 position =
				new Vector2(currentX, currentY);

			Vector2 accumulatedNormal =
				Vector2.Zero;

			bool particleImpacted = false;

			float movementX = currentX - previousX;
			float movementY = currentY - previousY;

			float movementSquared =
				movementX * movementX +
				movementY * movementY;

			bool particleNeedsSwept =
				useSweptTerrain &&
				movementSquared >
				SweptCollisionDistanceSquared;

			float queryMinX, queryMaxX, queryMinY, queryMaxY;

			if (particleNeedsSwept)
			{
				float minPathX = previousX < currentX ? previousX : currentX;
				float maxPathX = previousX > currentX ? previousX : currentX;
				float minPathY = previousY < currentY ? previousY : currentY;
				float maxPathY = previousY > currentY ? previousY : currentY;

				queryMinX = minPathX - terrainMargin;
				queryMaxX = maxPathX + terrainMargin;
				queryMinY = minPathY - terrainMargin;
				queryMaxY = maxPathY + terrainMargin;
			}
			else
			{
				queryMinX = currentX - terrainMargin;
				queryMaxX = currentX + terrainMargin;
				queryMinY = currentY - terrainMargin;
				queryMaxY = currentY + terrainMargin;
			}

			int queryMinCellX = GetColliderCellX(queryMinX);
			int queryMaxCellX = GetColliderCellX(queryMaxX);
			int queryMinCellY = GetColliderCellY(queryMinY);
			int queryMaxCellY = GetColliderCellY(queryMaxY);

			localStampId++;

			if (localStampId == int.MaxValue)
			{
				Array.Clear(
					localStamps,
					0,
					localStamps.Length
				);

				localStampId = 1;
			}

			int terrainStamp = localStampId;

			for (
				int cellY = queryMinCellY;
				cellY <= queryMaxCellY;
				cellY++)
			{
				if (cellY < 0 || cellY >= localGridHeight)
					continue;

				int rowOffset = cellY * localGridWidth;

				for (
					int cellX = queryMinCellX;
					cellX <= queryMaxCellX;
					cellX++)
				{
					if (cellX < 0 || cellX >= localGridWidth)
						continue;

					List<int> cell =
						localGrid[rowOffset + cellX];

					if (cell == null || cell.Count == 0)
						continue;

					int cellCount = cell.Count;

					for (int k = 0; k < cellCount; k++)
					{
						int c = cell[k];

						if (c < 0 || c >= localColliders.Count)
							continue;

						if (localStamps[c] == terrainStamp)
							continue;

						localStamps[c] = terrainStamp;

						FluidPolygonCollider collider =
							localColliders[c];

						if (collider == null || collider.IsWheel)
							continue;

						float minColliderX =
							localColliderMinX[c] - particleRadius;

						float maxColliderX =
							localColliderMaxX[c] + particleRadius;

						float minColliderY =
							localColliderMinY[c] - particleRadius;

						float maxColliderY =
							localColliderMaxY[c] + particleRadius;

						bool boundsHit;

						if (particleNeedsSwept)
						{
							boundsHit =
								queryMinX <= maxColliderX &&
								queryMaxX >= minColliderX &&
								queryMinY <= maxColliderY &&
								queryMaxY >= minColliderY;
						}
						else
						{
							boundsHit =
								currentX >= minColliderX &&
								currentX <= maxColliderX &&
								currentY >= minColliderY &&
								currentY <= maxColliderY;
						}

						if (!boundsHit)
							continue;

						bool resolved;
						Vector2 correctedPosition;
						Vector2 normal;

						if (particleNeedsSwept)
						{
							resolved =
								collider.ResolveSweptCollision(
									new Vector2(previousX, previousY),
									position,
									particleRadius,
									out correctedPosition,
									out normal,
									out _
								);
						}
						else
						{
							resolved =
								collider.ResolveCollision(
									position,
									particleRadius,
									out correctedPosition,
									out normal
								);
						}

						if (!resolved)
							continue;

						position = correctedPosition;
						currentX = position.X;
						currentY = position.Y;

						float normalLengthSquared =
							normal.X * normal.X +
							normal.Y * normal.Y;

						if (normalLengthSquared > ImpactNormalEpsilon)
						{
							accumulatedNormal += normal;
							particleImpacted = true;
						}
					}
				}
			}

			// --------------------------------------------------------
			// Wheel broad phase
			// --------------------------------------------------------

			for (int w = 0; w < wheelGroupCount; w++)
			{
				WheelCollisionGroup group =
					localWheelGroups[w];

				if (
					group == null ||
					group.Colliders.Count == 0)
				{
					continue;
				}

				if (
					currentX < group.MinX ||
					currentX > group.MaxX ||
					currentY < group.MinY ||
					currentY > group.MaxY)
				{
					continue;
				}

				List<FluidPolygonCollider> groupColliders =
					group.Colliders;

				List<int> groupIndices =
					group.ColliderIndices;

				int groupCount = groupColliders.Count;

				for (int c = 0; c < groupCount; c++)
				{
					FluidPolygonCollider collider =
						groupColliders[c];

					if (collider == null)
						continue;

					int wheelIndex = groupIndices[c];

					if (
						wheelMinX != null &&
						wheelIndex >= 0 &&
						wheelIndex < wheelMinX.Length)
					{
						if (
							currentX < wheelMinX[wheelIndex] ||
							currentX > wheelMaxX[wheelIndex] ||
							currentY < wheelMinY[wheelIndex] ||
							currentY > wheelMaxY[wheelIndex])
						{
							continue;
						}
					}

					Vector2 correctedPosition;
					Vector2 normal;

					bool wheelResolved =
						collider.ResolveCollision(
							position,
							particleRadius,
							out correctedPosition,
							out normal
						);

					if (!wheelResolved)
						continue;

					ApplyWheelTorque(
						collider,
						position,
						normal,
						velX[i],
						velY[i],
						dt
					);

					position = correctedPosition;
					currentX = position.X;
					currentY = position.Y;

					float normalLengthSquared =
						normal.X * normal.X +
						normal.Y * normal.Y;

					if (normalLengthSquared > ImpactNormalEpsilon)
					{
						accumulatedNormal += normal;
						particleImpacted = true;
					}
				}
			}

			predX[i] = currentX;
			predY[i] = currentY;

			if (particleImpacted)
			{
				float normalLengthSquared =
					accumulatedNormal.X *
					accumulatedNormal.X +
					accumulatedNormal.Y *
					accumulatedNormal.Y;

				if (normalLengthSquared > ImpactNormalEpsilon)
				{
					float inverseLength =
						1.0f /
						Mathf.Sqrt(normalLengthSquared);

					impactNormalX[i] =
						accumulatedNormal.X *
						inverseLength;

					impactNormalY[i] =
						accumulatedNormal.Y *
						inverseLength;

					impacted[i] = true;
				}
			}
		}

		terrainColliderQueryStampId = localStampId;

		if (measureWheelCollision)
		{
			wheelCollisionMs +=
				(
					Stopwatch.GetTimestamp() -
					wheelCollisionStart
				) *
				1000.0 /
				Stopwatch.Frequency;
		}
	}

	// ============================================================
	// Register wheel collider
	// ============================================================

	private void RegisterWheelCollider(
		FluidPolygonCollider collider)
	{
		if (
			collider == null ||
			!collider.IsWheel)
		{
			return;
		}

		FluidWheelState wheelState =
			collider.Wheel;

		if (wheelState == null)
			return;

		int wheelColliderIndex =
			wheelColliders.Count - 1;

		for (
			int i = 0;
			i < wheelCollisionGroups.Count;
			i++)
		{
			WheelCollisionGroup existing =
				wheelCollisionGroups[i];

			if (
				ReferenceEquals(
					existing.Wheel,
					wheelState
				))
			{
				existing.Colliders.Add(collider);
				existing.ColliderIndices.Add(wheelColliderIndex);
				return;
			}
		}

		WheelCollisionGroup group =
			new WheelCollisionGroup();

		group.Wheel = wheelState;
		group.Colliders.Add(collider);
		group.ColliderIndices.Add(wheelColliderIndex);

		wheelCollisionGroups.Add(group);
	}

	// ============================================================
	// Wheel bounds
	// ============================================================

	private void EnsureWheelBounds()
	{
		int count = wheelColliders.Count;

		if (
			wheelMinX != null &&
			wheelMinX.Length == count)
		{
			return;
		}

		wheelMinX = new float[count];
		wheelMaxX = new float[count];
		wheelMinY = new float[count];
		wheelMaxY = new float[count];
	}

	private void UpdateWheelBounds()
	{
		int colliderCount = wheelColliders.Count;

		if (colliderCount <= 0)
			return;

		EnsureWheelBounds();

		for (int i = 0; i < colliderCount; i++)
		{
			FluidPolygonCollider collider =
				wheelColliders[i];

			if (collider == null)
			{
				wheelMinX[i] = float.MaxValue;
				wheelMaxX[i] = float.MinValue;
				wheelMinY[i] = float.MaxValue;
				wheelMaxY[i] = float.MinValue;
				continue;
			}

			collider.GetBounds(
				out float minX,
				out float maxX,
				out float minY,
				out float maxY
			);

			wheelMinX[i] = minX - WheelBoundsExpansion;
			wheelMaxX[i] = maxX + WheelBoundsExpansion;
			wheelMinY[i] = minY - WheelBoundsExpansion;
			wheelMaxY[i] = maxY + WheelBoundsExpansion;
		}

		int groupCount = wheelCollisionGroups.Count;

		for (int w = 0; w < groupCount; w++)
		{
			WheelCollisionGroup group =
				wheelCollisionGroups[w];

			if (
				group == null ||
				group.Colliders.Count == 0)
			{
				continue;
			}

			float minX = float.MaxValue;
			float maxX = float.MinValue;
			float minY = float.MaxValue;
			float maxY = float.MinValue;

			List<FluidPolygonCollider> groupColliders =
				group.Colliders;

			int groupColliderCount =
				groupColliders.Count;

			for (int c = 0; c < groupColliderCount; c++)
			{
				FluidPolygonCollider collider =
					groupColliders[c];

				if (collider == null)
					continue;

				collider.GetBounds(
					out float cMinX,
					out float cMaxX,
					out float cMinY,
					out float cMaxY
				);

				if (cMinX < minX) minX = cMinX;
				if (cMaxX > maxX) maxX = cMaxX;
				if (cMinY < minY) minY = cMinY;
				if (cMaxY > maxY) maxY = cMaxY;
			}

			group.MinX = minX - WheelBoundsExpansion;
			group.MaxX = maxX + WheelBoundsExpansion;
			group.MinY = minY - WheelBoundsExpansion;
			group.MaxY = maxY + WheelBoundsExpansion;
		}
	}

	// ============================================================
	// Collider grid
	// ============================================================

	private void RebuildColliderGrid()
	{
		colliderGridWidth =
			Math.Max(
				1,
				(int)MathF.Ceiling(
					(MaxX - MinX) /
					ColliderGridCellSize
				)
			);

		colliderGridHeight =
			Math.Max(
				1,
				(int)MathF.Ceiling(
					(MaxY - MinY) /
					ColliderGridCellSize
				)
			);

		int cellCount =
			colliderGridWidth *
			colliderGridHeight;

		if (
			colliderGrid == null ||
			colliderGrid.Length != cellCount)
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

		int colliderCount = polygonColliders.Count;

		if (
			colliderMinX == null ||
			colliderMinX.Length != colliderCount)
		{
			colliderMinX = new float[colliderCount];
			colliderMaxX = new float[colliderCount];
			colliderMinY = new float[colliderCount];
			colliderMaxY = new float[colliderCount];

			terrainColliderQueryStamp =
				new int[colliderCount];

			terrainColliderQueryStampId = 0;
		}
		else if (
			terrainColliderQueryStamp == null ||
			terrainColliderQueryStamp.Length != colliderCount)
		{
			terrainColliderQueryStamp =
				new int[colliderCount];

			terrainColliderQueryStampId = 0;
		}

		float expansion =
			PolygonParticleRadius +
			ColliderGridExpansion;

		for (int i = 0; i < colliderCount; i++)
		{
			FluidPolygonCollider collider =
				polygonColliders[i];

			if (
				collider == null ||
				collider.IsWheel)
			{
				colliderMinX[i] = 0.0f;
				colliderMaxX[i] = 0.0f;
				colliderMinY[i] = 0.0f;
				colliderMaxY[i] = 0.0f;
				continue;
			}

			collider.GetBounds(
				out float minX,
				out float maxX,
				out float minY,
				out float maxY
			);

			colliderMinX[i] = minX;
			colliderMaxX[i] = maxX;
			colliderMinY[i] = minY;
			colliderMaxY[i] = maxY;

			int minCellX = GetColliderCellX(minX - expansion);
			int maxCellX = GetColliderCellX(maxX + expansion);
			int minCellY = GetColliderCellY(minY - expansion);
			int maxCellY = GetColliderCellY(maxY + expansion);

			for (int y = minCellY; y <= maxCellY; y++)
			{
				int rowOffset = y * colliderGridWidth;

				for (int x = minCellX; x <= maxCellX; x++)
					colliderGrid[rowOffset + x].Add(i);
			}
		}

		colliderGridDirty = false;
	}

	// ============================================================
	// Collider grid coordinate helpers
	// ============================================================

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private int GetColliderCellX(float x)
	{
		int cell =
			(int)MathF.Floor(
				(x - MinX) / ColliderGridCellSize
			);

		if (cell < 0) return 0;
		if (cell >= colliderGridWidth) return colliderGridWidth - 1;
		return cell;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private int GetColliderCellY(float y)
	{
		int cell =
			(int)MathF.Floor(
				(y - MinY) / ColliderGridCellSize
			);

		if (cell < 0) return 0;
		if (cell >= colliderGridHeight) return colliderGridHeight - 1;
		return cell;
	}

	// ============================================================
	// Wheel torque
	// ============================================================

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ApplyWheelTorque(
		FluidPolygonCollider collider,
		Vector2 contactPosition,
		Vector2 normal,
		float velocityX,
		float velocityY,
		float dt)
	{
		FluidWheelState wheelState = collider.Wheel;

		if (wheelState == null)
			return;

		Vector2 wheelVelocity =
			wheelState.GetSurfaceVelocity(contactPosition);

		float relativeVelocityX =
			velocityX - wheelVelocity.X;

		float relativeVelocityY =
			velocityY - wheelVelocity.Y;

		float tangentX = -normal.Y;
		float tangentY =  normal.X;

		float tangentialVelocity =
			relativeVelocityX * tangentX +
			relativeVelocityY * tangentY;

		float impulse = tangentialVelocity * 0.15f;

		Vector2 radius =
			contactPosition - wheelState.Center;

		float torque =
			radius.X * (tangentY * impulse) -
			radius.Y * (tangentX * impulse);

		wheelState.AddTorque(torque);
	}
}
