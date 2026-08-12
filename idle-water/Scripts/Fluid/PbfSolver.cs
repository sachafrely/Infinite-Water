using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

public class PbfSolver
{
	private const int ProfilerIntervalFrames = 600;
	private int profilerFrameCounter = 0;

	private readonly SpatialHash hash;

	private readonly List<FluidPolygonCollider>
		polygonColliders;

	private readonly List<FluidPolygonCollider>
		wheelColliders;

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

	// ============================================================
	// Simulation
	// ============================================================

	private const float Gravity = 250.0f;

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

	private const float GroundDrag = 0.09f;
	private const float GroundStick = 0.10f;

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

	private const float ColliderGridCellSize = 32.0f;

	private const float ColliderGridExpansion = 1.0f;

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
			{
				wheelColliders.Add(
					collider
				);
			}

			colliderGridDirty = true;
		}
	}

	// ============================================================
	// Clear terrain colliders
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
		profilerFrameCounter++;

		bool printProfiler =
			profilerFrameCounter >= ProfilerIntervalFrames;

		if (printProfiler)
			profilerFrameCounter = 0;

		long totalStart =
			Stopwatch.GetTimestamp();

		long predictStart;
		long spatialHashStart;
		long neighborCacheStart;
		long pbfStart;
		long collisionStart;
		long boundsStart;
		long velocityStart;

		double predictMs = 0.0;
		double spatialHashMs = 0.0;
		double neighborCacheMs = 0.0;
		double pbfMs = 0.0;
		double collisionMs = 0.0;

		double terrainQueryMs = 0.0;
		double terrainResolveMs = 0.0;
		double wheelCollisionMs = 0.0;

		double boundsMs = 0.0;
		double velocityMs = 0.0;

		int count =
			particles.Count;

		if (
			count <= 0 ||
			dt <= 0.0f)
		{
			if (wheel != null)
				wheel.Step(dt);

			double totalMs =
				ElapsedMilliseconds(
					totalStart
				);

			if (printProfiler)
			{
			PrintProfiler(
				predictMs,
				spatialHashMs,
				neighborCacheMs,
				pbfMs,
				collisionMs,
				terrainQueryMs,
				terrainResolveMs,
				wheelCollisionMs,
				boundsMs,
				velocityMs,
				totalMs
			);
			}

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
				i < wheelColliders.Count;
				i++)
			{
				FluidPolygonCollider collider =
					wheelColliders[i];

				if (collider != null)
				{
					collider.UpdateWheelGeometry();
				}
			}
		}

		// --------------------------------------------------------
		// Prepare static terrain collider grid.
		// --------------------------------------------------------

		if (colliderGridDirty)
		{
			RebuildColliderGrid();
		}

		// --------------------------------------------------------
		// Predict
		// --------------------------------------------------------

		predictStart =
			Stopwatch.GetTimestamp();

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

		predictMs =
			ElapsedMilliseconds(
				predictStart
			);

		// --------------------------------------------------------
		// Spatial hash
		// --------------------------------------------------------

		spatialHashStart =
			Stopwatch.GetTimestamp();

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

		spatialHashMs =
			ElapsedMilliseconds(
				spatialHashStart
			);

		// --------------------------------------------------------
		// Neighbor cache
		// --------------------------------------------------------

		neighborCacheStart =
			Stopwatch.GetTimestamp();

		BuildNeighborCache(
			predX,
			predY,
			count
		);

		neighborCacheMs =
			ElapsedMilliseconds(
				neighborCacheStart
			);

		// --------------------------------------------------------
		// PBF iterations
		// --------------------------------------------------------

		pbfStart =
			Stopwatch.GetTimestamp();

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
				collisionStart =
					Stopwatch.GetTimestamp();

				ConstrainToPolygonColliders(
					predX,
					predY,
					velX,
					velY,
					count,
					dt,
					ref terrainQueryMs,
					ref terrainResolveMs,
					ref wheelCollisionMs
				);

				collisionMs +=
					ElapsedMilliseconds(
						collisionStart
					);
			}

			// ----------------------------------------------------
			// World bounds
			// ----------------------------------------------------

			boundsStart =
				Stopwatch.GetTimestamp();

			ConstrainToBounds(
				predX,
				predY,
				count
			);

			boundsMs +=
				ElapsedMilliseconds(
					boundsStart
				);

			if (
				iteration + 1 >= MinIterations &&
				densityError <=
				DensityErrorThreshold)
			{
				break;
			}
		}

		pbfMs =
			ElapsedMilliseconds(
				pbfStart
			);

		// --------------------------------------------------------
		// Reconstruct velocity
		// --------------------------------------------------------

		velocityStart =
			Stopwatch.GetTimestamp();

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

		velocityMs =
			ElapsedMilliseconds(
				velocityStart
			);

		// --------------------------------------------------------
		// Total profiler time
		// --------------------------------------------------------

		double total =
			ElapsedMilliseconds(
				totalStart
			);

		if (printProfiler)
		{
		PrintProfiler(
			predictMs,
			spatialHashMs,
			neighborCacheMs,
			pbfMs,
			collisionMs,
			terrainQueryMs,
			terrainResolveMs,
			wheelCollisionMs,
			boundsMs,
			velocityMs,
			total
		);
		}
	}

	// ============================================================
	// Profiler
	// ============================================================

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static double ElapsedMilliseconds(
		long start)
	{
		return
			(double)(
				Stopwatch.GetTimestamp() -
				start
			) *
			1000.0 /
			Stopwatch.Frequency;
	}

	private static void PrintProfiler(
		double predictMs,
		double spatialHashMs,
		double neighborCacheMs,
		double pbfMs,
		double collisionMs,
		double terrainQueryMs,
		double terrainResolveMs,
		double wheelCollisionMs,
		double boundsMs,
		double velocityMs,
		double totalMs)
	{
		GD.Print(
			$"[PBF PROFILER] " +
			$"Predict: {predictMs:F3} ms | " +
			$"SpatialHash: {spatialHashMs:F3} ms | " +
			$"NeighborCache: {neighborCacheMs:F3} ms | " +
			$"PBF: {pbfMs:F3} ms | " +
			$"Collision: {collisionMs:F3} ms " +
			$"(Query: {terrainQueryMs:F3} ms | " +
			$"TerrainResolve: {terrainResolveMs:F3} ms | " +
			$"Wheel: {wheelCollisionMs:F3} ms) | " +
			$"Bounds: {boundsMs:F3} ms | " +
			$"Velocity: {velocityMs:F3} ms | " +
			$"Total: {totalMs:F3} ms"
		);
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
		float dt,
		ref double terrainQueryMs,
		ref double terrainResolveMs,
		ref double wheelCollisionMs)
	{
		if (colliderGrid == null)
			return;

		List<int>[] localGrid =
			colliderGrid;

		int localGridWidth =
			colliderGridWidth;

		int localGridHeight =
			colliderGridHeight;

		List<FluidPolygonCollider> localColliders =
			polygonColliders;

		float[] localMinX =
			colliderMinX;

		float[] localMaxX =
			colliderMaxX;

		float[] localMinY =
			colliderMinY;

		float[] localMaxY =
			colliderMaxY;

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

			int baseCellX =
				GetColliderCellX(
					position.X
				);

			int baseCellY =
				GetColliderCellY(
					position.Y
				);

			// ----------------------------------------------------
			// Terrain grid query
			//
			// The collider grid is already built with an expanded
			// AABB:
			//
			// PolygonParticleRadius + ColliderGridExpansion
			//
			// Therefore a collider capable of overlapping the
			// particle is already registered in the particle's
			// current grid cell.
			//
			// The previous implementation searched a complete
			// 3x3 group of cells for every particle, every PBF
			// iteration. This was the main source of collision
			// query cost.
			// ----------------------------------------------------

			long terrainQueryStart =
				Stopwatch.GetTimestamp();

			if (
				baseCellX >= 0 &&
				baseCellX < localGridWidth &&
				baseCellY >= 0 &&
				baseCellY < localGridHeight)
			{
				List<int> cell =
					localGrid[
						baseCellY *
						localGridWidth +
						baseCellX
					];

				if (cell != null)
				{
					for (
						int k = 0;
						k < cell.Count;
						k++)
					{
						int c =
							cell[k];

						FluidPolygonCollider collider =
							localColliders[c];

						if (
							collider == null ||
							collider.IsWheel)
						{
							continue;
						}

						if (
							!ParticleOverlapsBounds(
								position.X,
								position.Y,
								localMinX[c],
								localMaxX[c],
								localMinY[c],
								localMaxY[c]
							))
						{
							continue;
						}

						long terrainResolveStart =
							Stopwatch.GetTimestamp();

						bool resolved =
							collider.ResolveCollision(
								position,
								PolygonParticleRadius,
								out Vector2 correctedPosition,
								out Vector2 normal
							);

						terrainResolveMs +=
							ElapsedMilliseconds(
								terrainResolveStart
							);

						if (!resolved)
						{
							continue;
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
				}
			}

			terrainQueryMs +=
				ElapsedMilliseconds(
					terrainQueryStart
				);

			// ----------------------------------------------------
			// Wheel collision
			//
			// Wheels remain separate because their geometry is
			// dynamic and changes every frame.
			// ----------------------------------------------------

			for (
				int w = 0;
				w < wheelColliders.Count;
				w++)
			{
				FluidPolygonCollider collider =
					wheelColliders[w];

				if (collider == null)
					continue;

				long wheelStart =
					Stopwatch.GetTimestamp();

				bool wheelResolved =
					collider.ResolveCollision(
						position,
						PolygonParticleRadius,
						out Vector2 correctedPosition,
						out Vector2 normal
					);

				wheelCollisionMs +=
					ElapsedMilliseconds(
						wheelStart
					);

				if (!wheelResolved)
				{
					continue;
				}

				ApplyWheelTorque(
					collider,
					position,
					normal,
					velX[i],
					velY[i],
					dt
				);

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
	// Fast particle / AABB overlap
	// ============================================================

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool ParticleOverlapsBounds(
		float particleX,
		float particleY,
		float minX,
		float maxX,
		float minY,
		float maxY)
	{
		float dx = 0.0f;

		if (particleX < minX)
		{
			dx =
				minX -
				particleX;
		}
		else if (particleX > maxX)
		{
			dx =
				particleX -
				maxX;
		}

		float dy = 0.0f;

		if (particleY < minY)
		{
			dy =
				minY -
				particleY;
		}
		else if (particleY > maxY)
		{
			dy =
				particleY -
				maxY;
		}

		float distanceSquared =
			dx * dx +
			dy * dy;

		return
			distanceSquared <=
			PolygonParticleRadius *
			PolygonParticleRadius;
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
			colliderGrid =
				new List<int>[cellCount];

			for (
				int i = 0;
				i < cellCount;
				i++)
			{
				colliderGrid[i] =
					new List<int>(4);
			}
		}
		else
		{
			for (
				int i = 0;
				i < cellCount;
				i++)
			{
				colliderGrid[i]?.Clear();
			}
		}

		int colliderCount =
			polygonColliders.Count;

		if (
			colliderMinX == null ||
			colliderMinX.Length != colliderCount)
		{
			colliderMinX =
				new float[colliderCount];

			colliderMaxX =
				new float[colliderCount];

			colliderMinY =
				new float[colliderCount];

			colliderMaxY =
				new float[colliderCount];
		}

		float expansion =
			PolygonParticleRadius +
			ColliderGridExpansion;

		for (
			int i = 0;
			i < colliderCount;
			i++)
		{
			FluidPolygonCollider collider =
				polygonColliders[i];

			if (
				collider == null ||
				collider.IsWheel)
			{
				colliderMinX[i] =
					0.0f;

				colliderMaxX[i] =
					0.0f;

				colliderMinY[i] =
					0.0f;

				colliderMaxY[i] =
					0.0f;

				continue;
			}

			collider.GetBounds(
				out float minX,
				out float maxX,
				out float minY,
				out float maxY
			);

			colliderMinX[i] =
				minX;

			colliderMaxX[i] =
				maxX;

			colliderMinY[i] =
				minY;

			colliderMaxY[i] =
				maxY;

			int minCellX =
				GetColliderCellX(
					minX -
					expansion
				);

			int maxCellX =
				GetColliderCellX(
					maxX +
					expansion
				);

			int minCellY =
				GetColliderCellY(
					minY -
					expansion
				);

			int maxCellY =
				GetColliderCellY(
					maxY +
					expansion
				);

			for (
				int y = minCellY;
				y <= maxCellY;
				y++)
			{
				for (
					int x = minCellX;
					x <= maxCellX;
					x++)
				{
					colliderGrid[
						y *
						colliderGridWidth +
						x
					].Add(i);
				}
			}
		}

		colliderGridDirty =
			false;
	}

	// ============================================================
	// Collider grid X
	// ============================================================

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private int GetColliderCellX(
		float x)
	{
		int cell =
			(int)MathF.Floor(
				(x - MinX) /
				ColliderGridCellSize
			);

		if (cell < 0)
			return 0;

		if (
			cell >=
			colliderGridWidth)
		{
			return colliderGridWidth - 1;
		}

		return cell;
	}

	// ============================================================
	// Collider grid Y
	// ============================================================

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private int GetColliderCellY(
		float y)
	{
		int cell =
			(int)MathF.Floor(
				(y - MinY) /
				ColliderGridCellSize
			);

		if (cell < 0)
			return 0;

		if (
			cell >=
			colliderGridHeight)
		{
			return colliderGridHeight - 1;
		}

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

		if (normalVelocity > 0.0f)
		{
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

		float separationVelocity =
			velocityX *
			normalX +
			velocityY *
			normalY;

		if (separationVelocity < 0.0f)
		{
			float stickAmount =
				-separationVelocity *
				GroundStick;

			velocityX +=
				normalX *
				stickAmount;

			velocityY +=
				normalY *
				stickAmount;
		}

		float tangentX =
			-normalY;

		float tangentY =
			normalX;

		float tangentialVelocity =
			velocityX *
			tangentX +
			velocityY *
			tangentY;

		velocityX -=
			tangentX *
			tangentialVelocity *
			GroundDrag;

		velocityY -=
			tangentY *
			tangentialVelocity *
			GroundDrag;
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

			// LEFT

			if (x < left)
			{
				x = left;

				impacted[i] = true;
				impactNormalX[i] = 1.0f;
				impactNormalY[i] = 0.0f;
			}

			// RIGHT

			else if (x > right)
			{
				x = right;

				impacted[i] = true;
				impactNormalX[i] = -1.0f;
				impactNormalY[i] = 0.0f;
			}

			// TOP

			if (y < top)
			{
				y = top;

				impacted[i] = true;
				impactNormalX[i] = 0.0f;
				impactNormalY[i] = 1.0f;
			}

			// BOTTOM

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
			sleepProgress[i] =
				0.0f;

			sleeping[i] =
				false;

			return;
		}

		if (
			velocitySquared <
			SleepVelocityThresholdSquared)
		{
			sleepProgress[i] +=
				dt /
				SleepTime;

			if (
				sleepProgress[i] >
				1.0f)
			{
				sleepProgress[i] =
					1.0f;
			}

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
			dt /
			SleepTime;

		if (
			sleepProgress[i] <
			0.0f)
		{
			sleepProgress[i] =
				0.0f;
		}

		sleeping[i] =
			false;
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
				i *
				neighborStride;

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
				i *
				neighborStride;

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

	// ============================================================
	// Neighbor geometry
	// ============================================================

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void UpdateNeighborGeometryRange(
		int start,
		int end,
		float px,
		float py,
		float[] predX,
		float[] predY)
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

			float q =
				1.0f -
				(
					distanceSquared *
					inverseDistance
				) *
				InverseSmoothingRadius;

			float q2 =
				q * q;

			localQ[index] =
				q;

			localGradient[index] =
				-3.0f *
				q2 *
				InverseSmoothingRadius *
				inverseDistance *
				InverseRestDensity;
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

		float inverseRestDensity =
			InverseRestDensity;

		int stride =
			neighborStride;

		for (
			int i = 0;
			i < count;
			i++)
		{
			int start =
				i *
				stride;

			int end =
				start +
				localNeighborCounts[i];

			float density =
				0.0f;

			float gradSumX =
				0.0f;

			float gradSumY =
				0.0f;

			float neighborGradientSquared =
				0.0f;

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
				constraint < 0.0f
					? -constraint
					: constraint;

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
