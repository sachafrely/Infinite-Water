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

	private int[] terrainColliderQueryStamp;
	private int terrainColliderQueryStampId = 0;

	// ============================================================
	// Wheel bounds
	// ============================================================

	private float[] wheelMinX;
	private float[] wheelMaxX;
	private float[] wheelMinY;
	private float[] wheelMaxY;

	private const float WheelBoundsExpansion = 2.5f;

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

		public float MinX;
		public float MaxX;
		public float MinY;
		public float MaxY;
	}

	// ============================================================
	// Simulation
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

	private const float DensityErrorThreshold = 0.90f;
	private const float MaxCorrection = 0.5f;
	private const float MaxCorrectionSquared = 0.25f;

	// ============================================================
	// Stability
	// ============================================================

	private const float VelocityDamping = 0.998f;

	// ============================================================
	// Surface behavior
	// ============================================================
	//
	// IMPORTANT:
	//
	// The old values allowed collision handling to behave like
	// sticky material:
	//
	// GroundDrag  = 0.09
	// GroundStick = 0.10
	//
	// That is especially noticeable on shallow/slanted surfaces.
	//
	// Water should instead retain its tangential velocity.
	// ============================================================

	private const float ImpactDamping = 0.10f;

	private const float ImpactNormalEpsilon = 0.0001f;

	// Almost no artificial friction along terrain.
	private const float GroundDrag = 0.005f;

	// Do NOT artificially pull particles back into surfaces.
	private const float GroundStick = 0.0f;

	// Small compensation used when collision projection has removed
	// too much of the tangential gravity component.
	//
	// This is intentionally below 1.0 so it does not create
	// unrealistic acceleration.
	private const float SurfaceGravityRetention = 0.85f;

	// Surfaces whose normal is almost vertical are considered
	// horizontal ground for sleeping purposes.
	//
	// A slanted surface should NOT easily put water to sleep.
	private const float HorizontalSurfaceNormalY = 0.92f;

	// ============================================================
	// Sleeping
	// ============================================================

	private const float SleepVelocityThreshold = 1.0f;
	private const float WakeVelocityThreshold = 3.0f;
	private const float SleepTime = 0.50f;
	private const float SleepDampingStrength = 1.5f;

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
	// Pixel occupancy
	// ============================================================

	private const int MaxParticlesPerPixel = 2;

	private const float ExactOverlapDistanceSquared =
		0.000001f;

	private const float ExactOverlapSeparation = 0.05f;

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
	// Particle packing profiler
	// ============================================================

	private float[] packingNearestDistances;

	// ============================================================
	// Pixel occupancy working buffers
	// ============================================================

	private Dictionary<long, int> pixelOccupancy;

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

		pixelOccupancy =
			new Dictionary<long, int>();
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

				RegisterWheelCollider(
					collider
				);

				EnsureWheelBounds();

				colliderGridDirty =
					true;
			}
			else
			{
				colliderGridDirty =
					true;
			}
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

				colliderGridDirty =
					true;
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
			profilerFrameCounter >=
			ProfilerIntervalFrames;

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

		EnsureBuffers(
			count
		);

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

			UpdateWheelBounds();
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
		// Particle packing profiler
		// --------------------------------------------------------

		if (printProfiler)
		{
			CalculateParticlePackingStats(
				predX,
				predY,
				count
			);
		}

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

			ApplyPixelOccupancyCorrection(
				predX,
				predY,
				count
			);

			if (
				polygonColliders.Count > 0)
			{
				collisionStart =
					Stopwatch.GetTimestamp();

				ConstrainToPolygonColliders(
					predX,
					predY,
					posX,
					posY,
					velX,
					velY,
					count,
					dt,
					iteration == 0,
					ref terrainQueryMs,
					ref terrainResolveMs,
					ref wheelCollisionMs
				);

				collisionMs +=
					ElapsedMilliseconds(
						collisionStart
					);
			}

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
			// World boundaries
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
						finalVelocityX =
							0.0f;
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
			else if (
				x >=
				MaxX - BoundarySkin)
			{
				if (
					finalVelocityX > 0.0f)
				{
					if (
						Mathf.Abs(
							finalVelocityX
						) <
						BoundaryVelocityEpsilon)
					{
						finalVelocityX =
							0.0f;
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
			// Surface flow
			// ----------------------------------------------------
			//
			// Collision projection can remove part of the movement
			// that should have remained tangent to a slanted surface.
			//
			// Gravity is:
			//
			//      G = (0, Gravity)
			//
			// Remove its component along the collision normal:
			//
			//      Gt = G - N * dot(G,N)
			//
			// Gt is therefore the component of gravity that should
			// naturally make the water flow DOWN the surface.
			//
			// We only add a fraction of it. This is not intended to
			// create extra gravity; it is mainly there to prevent
			// collision projection/damping from destroying downhill
			// motion.
			// ----------------------------------------------------

			if (impacted[i])
			{
				ApplySurfaceFlow(
					i,
					dt,
					ref finalVelocityX,
					ref finalVelocityY
				);

				ApplyImpactDamping(
					i,
					ref finalVelocityX,
					ref finalVelocityY
				);
			}

			// ----------------------------------------------------
			// Sleep
			// ----------------------------------------------------
			//
			// A key change:
			//
			// A particle touching a slanted surface is NOT allowed
			// to sleep just because its instantaneous velocity is
			// small.
			//
			// Otherwise shallow slopes become sticky because the
			// particle eventually gets classified as sleeping while
			// gravity is still trying to pull it downhill.
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
	// Surface flow
	// ============================================================

	private void ApplySurfaceFlow(
		int i,
		float dt,
		ref float velocityX,
		ref float velocityY)
	{
		float normalX =
			impactNormalX[i];

		float normalY =
			impactNormalY[i];

		float normalLengthSquared =
			normalX * normalX +
			normalY * normalY;

		if (
			normalLengthSquared <=
			ImpactNormalEpsilon)
		{
			return;
		}

		float inverseLength =
			1.0f /
			Mathf.Sqrt(
				normalLengthSquared
			);

		normalX *=
			inverseLength;

		normalY *=
			inverseLength;

		// Gravity vector.
		float gravityX =
			0.0f;

		float gravityY =
			Gravity;

		float gravityNormal =
			gravityX * normalX +
			gravityY * normalY;

		// Tangential component of gravity.
		float tangentGravityX =
			gravityX -
			normalX *
			gravityNormal;

		float tangentGravityY =
			gravityY -
			normalY *
			gravityNormal;

		// Only compensate for part of the lost surface acceleration.
		velocityX +=
			tangentGravityX *
			dt *
			SurfaceGravityRetention;

		velocityY +=
			tangentGravityY *
			dt *
			SurfaceGravityRetention;
	}

	// ============================================================
	// Pixel occupancy correction
	// ============================================================

	private void ApplyPixelOccupancyCorrection(
		float[] predX,
		float[] predY,
		int count)
	{
		if (count <= 0)
			return;

		pixelOccupancy.Clear();

		for (
			int i = 0;
			i < count;
			i++)
		{
			int pixelX =
				(int)MathF.Floor(
					predX[i]
				);

			int pixelY =
				(int)MathF.Floor(
					predY[i]
				);

			long key =
				MakePixelKey(
					pixelX,
					pixelY
				);

			if (
				!pixelOccupancy.TryGetValue(
					key,
					out int occupancy
				))
			{
				pixelOccupancy[key] =
					1;

				continue;
			}

			if (
				occupancy <
				MaxParticlesPerPixel)
			{
				pixelOccupancy[key] =
					occupancy + 1;

				continue;
			}

			bool exactlyOverlapping =
				false;

			int overlappingParticle =
				-1;

			for (
				int j = 0;
				j < i;
				j++)
			{
				int otherPixelX =
					(int)MathF.Floor(
						predX[j]
					);

				int otherPixelY =
					(int)MathF.Floor(
						predY[j]
					);

				if (
					otherPixelX != pixelX ||
					otherPixelY != pixelY)
				{
					continue;
				}

				float dx =
					predX[i] -
					predX[j];

				float dy =
					predY[i] -
					predY[j];

				float distanceSquared =
					dx * dx +
					dy * dy;

				if (
					distanceSquared <=
					ExactOverlapDistanceSquared)
				{
					exactlyOverlapping =
						true;

					overlappingParticle =
						j;

					break;
				}
			}

			if (exactlyOverlapping)
			{
				Vector2 direction =
					GetDeterministicSeparationDirection(
						i,
						overlappingParticle
					);

				predX[i] +=
					direction.X *
					ExactOverlapSeparation;

				predY[i] +=
					direction.Y *
					ExactOverlapSeparation;
			}
		}
	}

	// ============================================================
	// Pixel key
	// ============================================================

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static long MakePixelKey(
		int x,
		int y)
	{
		return
			((long)x << 32) ^
			(uint)y;
	}

	// ============================================================
	// Deterministic exact-overlap direction
	// ============================================================

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static Vector2
		GetDeterministicSeparationDirection(
			int particleIndex,
			int otherParticleIndex)
	{
		int value =
			particleIndex ^
			(otherParticleIndex * 31);

		switch (
			Math.Abs(
				value %
				4))
		{
			case 0:
				return new Vector2(
					1.0f,
					0.0f
				);

			case 1:
				return new Vector2(
					-1.0f,
					0.0f
				);

			case 2:
				return new Vector2(
					0.0f,
					1.0f
				);

			default:
				return new Vector2(
					0.0f,
					-1.0f
				);
		}
	}

	// ============================================================
	// Particle packing profiler
	// ============================================================

	private void CalculateParticlePackingStats(
		float[] predX,
		float[] predY,
		int count)
	{
		if (count <= 0)
			return;

		EnsurePackingBuffer(
			count
		);

		int neighborSum =
			0;

		int maximumNeighbors =
			0;

		int neighborCapHits =
			0;

		double nearestDistanceSum =
			0.0;

		double minimumDistance =
			double.MaxValue;

		int validCount =
			0;

		int below1 =
			0;

		int below2 =
			0;

		int below3 =
			0;

		int below4 =
			0;

		for (
			int i = 0;
			i < count;
			i++)
		{
			int neighborCount =
				neighborCounts[i];

			neighborSum +=
				neighborCount;

			if (
				neighborCount >
				maximumNeighbors)
			{
				maximumNeighbors =
					neighborCount;
			}

			if (
				neighborCount >=
				MaxNeighbors)
			{
				neighborCapHits++;
			}

			float nearestDistance =
				float.MaxValue;

			int start =
				i *
				neighborStride;

			int end =
				start +
				neighborCount;

			for (
				int index = start;
				index < end;
				index++)
			{
				int j =
					neighborBuffer[index];

				float dx =
					predX[i] -
					predX[j];

				float dy =
					predY[i] -
					predY[j];

				float distanceSquared =
					dx * dx +
					dy * dy;

				if (
					distanceSquared <=
					0.000001f)
				{
					continue;
				}

				float distance =
					Mathf.Sqrt(
						distanceSquared
					);

				if (
					distance <
					nearestDistance)
				{
					nearestDistance =
						distance;
				}
			}

			if (
				nearestDistance ==
				float.MaxValue)
			{
				packingNearestDistances[i] =
					float.NaN;

				continue;
			}

			packingNearestDistances[i] =
				nearestDistance;

			nearestDistanceSum +=
				nearestDistance;

			validCount++;

			if (
				nearestDistance <
				minimumDistance)
			{
				minimumDistance =
					nearestDistance;
			}

			if (
				nearestDistance <
				1.0f)
			{
				below1++;
			}

			if (
				nearestDistance <
				2.0f)
			{
				below2++;
			}

			if (
				nearestDistance <
				3.0f)
			{
				below3++;
			}

			if (
				nearestDistance <
				4.0f)
			{
				below4++;
			}
		}

		if (
			validCount <= 0)
		{
			GD.Print(
				"[PARTICLE PACKING] " +
				$"Particles: {count} | " +
				"No valid nearest neighbors"
			);

			return;
		}

		Array.Sort(
			packingNearestDistances,
			0,
			validCount
		);

		int p5Index =
			(int)Math.Floor(
				(validCount - 1) *
				0.05
			);

		int medianIndex =
			(validCount - 1) /
			2;

		float p5 =
			packingNearestDistances[
				p5Index
			];

		float median =
			packingNearestDistances[
				medianIndex
			];

		double averageNearest =
			nearestDistanceSum /
			validCount;

		double averageNeighbors =
			(double)neighborSum /
			count;

		double minDistance =
			minimumDistance ==
			double.MaxValue
				? 0.0
				: minimumDistance;

		GD.Print(
			"[PARTICLE PACKING] " +
			$"Particles: {count} | " +
			$"MinDistance: {minDistance:F3} px | " +
			$"AvgNearest: {averageNearest:F3} px | " +
			$"P5: {p5:F3} px | " +
			$"Median: {median:F3} px | " +
			$"<1px: {below1} | " +
			$"<2px: {below2} | " +
			$"<3px: {below3} | " +
			$"<4px: {below4} | " +
			$"AvgNeighbors: {averageNeighbors:F2} | " +
			$"MaxNeighbors: {maximumNeighbors} | " +
			$"NeighborCapHits: {neighborCapHits}"
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
			float correctionX =
				0.0f;

			float correctionY =
				0.0f;

			int start =
				i *
				stride;

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
					(lambdaI +
					localLambdas[j]) *
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

		long wheelCollisionStart =
			0;

		bool measureWheelCollision =
			wheelCollisionGroups.Count > 0;

		if (measureWheelCollision)
		{
			wheelCollisionStart =
				Stopwatch.GetTimestamp();
		}

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

			// ----------------------------------------------------
			// Terrain grid query
			// ----------------------------------------------------

			long terrainQueryStart =
				Stopwatch.GetTimestamp();

			int queryMinCellX;
			int queryMaxCellX;
			int queryMinCellY;
			int queryMaxCellY;

			if (useSweptTerrain)
			{
				float minPathX =
					Mathf.Min(
						startX[i],
						position.X
					);

				float maxPathX =
					Mathf.Max(
						startX[i],
						position.X
					);

				float minPathY =
					Mathf.Min(
						startY[i],
						position.Y
					);

				float maxPathY =
					Mathf.Max(
						startY[i],
						position.Y
					);

				float expansion =
					PolygonParticleRadius +
					1.0f;

				queryMinCellX =
					GetColliderCellX(
						minPathX - expansion
					);

				queryMaxCellX =
					GetColliderCellX(
						maxPathX + expansion
					);

				queryMinCellY =
					GetColliderCellY(
						minPathY - expansion
					);

				queryMaxCellY =
					GetColliderCellY(
						maxPathY + expansion
					);
			}
			else
			{
				queryMinCellX =
					GetColliderCellX(
						position.X
					);

				queryMaxCellX =
					queryMinCellX;

				queryMinCellY =
					GetColliderCellY(
						position.Y
					);

				queryMaxCellY =
					queryMinCellY;
			}

			terrainColliderQueryStampId++;

			if (
				terrainColliderQueryStampId ==
				int.MaxValue)
			{
				Array.Clear(
					terrainColliderQueryStamp,
					0,
					terrainColliderQueryStamp.Length
				);

				terrainColliderQueryStampId =
					1;
			}

			int terrainStamp =
				terrainColliderQueryStampId;

			for (
				int cellY = queryMinCellY;
				cellY <= queryMaxCellY;
				cellY++)
			{
				if (
					cellY < 0 ||
					cellY >= localGridHeight)
				{
					continue;
				}

				for (
					int cellX = queryMinCellX;
					cellX <= queryMaxCellX;
					cellX++)
				{
					if (
						cellX < 0 ||
						cellX >= localGridWidth)
					{
						continue;
					}

					List<int> cell =
						localGrid[
							cellY *
							localGridWidth +
							cellX
						];

					if (cell == null)
						continue;

					for (
						int k = 0;
						k < cell.Count;
						k++)
					{
						int c =
							cell[k];

						if (
							c < 0 ||
							c >= localColliders.Count)
						{
							continue;
						}

						if (
							terrainColliderQueryStamp[c] ==
							terrainStamp)
						{
							continue;
						}

						terrainColliderQueryStamp[c] =
							terrainStamp;

						FluidPolygonCollider collider =
							localColliders[c];

						if (
							collider == null ||
							collider.IsWheel)
						{
							continue;
						}

						long terrainResolveStart =
							Stopwatch.GetTimestamp();

						bool resolved;
						Vector2 correctedPosition;
						Vector2 normal;

						if (useSweptTerrain)
						{
							resolved =
								collider.ResolveSweptCollision(
									new Vector2(
										startX[i],
										startY[i]
									),
									position,
									PolygonParticleRadius,
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
									PolygonParticleRadius,
									out correctedPosition,
									out normal
								);
						}

						terrainResolveMs +=
							ElapsedMilliseconds(
								terrainResolveStart
							);

						if (!resolved)
							continue;

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
			// ----------------------------------------------------

			for (
				int w = 0;
				w < wheelCollisionGroups.Count;
				w++)
			{
				WheelCollisionGroup group =
					wheelCollisionGroups[w];

				if (
					group == null ||
					group.Colliders.Count == 0)
				{
					continue;
				}

				if (
					position.X < group.MinX ||
					position.X > group.MaxX ||
					position.Y < group.MinY ||
					position.Y > group.MaxY)
				{
					continue;
				}

				List<FluidPolygonCollider>
					groupColliders =
					group.Colliders;

				for (
					int c = 0;
					c < groupColliders.Count;
					c++)
				{
					FluidPolygonCollider collider =
						groupColliders[c];

					if (collider == null)
						continue;

					collider.GetBounds(
						out float colliderMinX,
						out float colliderMaxX,
						out float colliderMinY,
						out float colliderMaxY
					);

					if (
						!ParticleOverlapsBounds(
							position.X,
							position.Y,
							colliderMinX,
							colliderMaxX,
							colliderMinY,
							colliderMaxY
						))
					{
						continue;
					}

					bool wheelResolved =
						collider.ResolveCollision(
							position,
							PolygonParticleRadius,
							out Vector2 correctedPosition,
							out Vector2 normal
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

		if (measureWheelCollision)
		{
			wheelCollisionMs +=
				ElapsedMilliseconds(
					wheelCollisionStart
				);
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
		float dx =
			0.0f;

		if (
			particleX <
			minX)
		{
			dx =
				minX -
				particleX;
		}
		else if (
			particleX >
			maxX)
		{
			dx =
				particleX -
				maxX;
		}

		float dy =
			0.0f;

		if (
			particleY <
			minY)
		{
			dy =
				minY -
				particleY;
		}
		else if (
			particleY >
			maxY)
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
				existing.Colliders.Add(
					collider
				);

				return;
			}
		}

		WheelCollisionGroup group =
			new WheelCollisionGroup();

		group.Wheel =
			wheelState;

		group.Colliders.Add(
			collider
		);

		wheelCollisionGroups.Add(
			group
		);
	}

	// ============================================================
	// Wheel bounds
	// ============================================================

	private void EnsureWheelBounds()
	{
		int count =
			wheelColliders.Count;

		if (
			wheelMinX != null &&
			wheelMinX.Length == count)
		{
			return;
		}

		wheelMinX =
			new float[count];

		wheelMaxX =
			new float[count];

		wheelMinY =
			new float[count];

		wheelMaxY =
			new float[count];
	}

	// ============================================================
	// Update wheel bounds
	// ============================================================

	private void UpdateWheelBounds()
	{
		int colliderCount =
			wheelColliders.Count;

		if (colliderCount <= 0)
			return;

		EnsureWheelBounds();

		for (
			int i = 0;
			i < colliderCount;
			i++)
		{
			FluidPolygonCollider collider =
				wheelColliders[i];

			if (collider == null)
			{
				wheelMinX[i] =
					float.MaxValue;

				wheelMaxX[i] =
					float.MinValue;

				wheelMinY[i] =
					float.MaxValue;

				wheelMaxY[i] =
					float.MinValue;

				continue;
			}

			collider.GetBounds(
				out float minX,
				out float maxX,
				out float minY,
				out float maxY
			);

			wheelMinX[i] =
				minX -
				WheelBoundsExpansion;

			wheelMaxX[i] =
				maxX +
				WheelBoundsExpansion;

			wheelMinY[i] =
				minY -
				WheelBoundsExpansion;

			wheelMaxY[i] =
				maxY +
				WheelBoundsExpansion;
		}

		for (
			int w = 0;
			w < wheelCollisionGroups.Count;
			w++)
		{
			WheelCollisionGroup group =
				wheelCollisionGroups[w];

			if (
				group == null ||
				group.Colliders.Count == 0)
			{
				continue;
			}

			float minX =
				float.MaxValue;

			float maxX =
				float.MinValue;

			float minY =
				float.MaxValue;

			float maxY =
				float.MinValue;

			for (
				int c = 0;
				c < group.Colliders.Count;
				c++)
			{
				FluidPolygonCollider collider =
					group.Colliders[c];

				if (collider == null)
					continue;

				collider.GetBounds(
					out float colliderMinX,
					out float colliderMaxX,
					out float colliderMinY,
					out float colliderMaxY
				);

				if (
					colliderMinX <
					minX)
				{
					minX =
						colliderMinX;
				}

				if (
					colliderMaxX >
					maxX)
				{
					maxX =
						colliderMaxX;
				}

				if (
					colliderMinY <
					minY)
				{
					minY =
						colliderMinY;
				}

				if (
					colliderMaxY >
					maxY)
				{
					maxY =
						colliderMaxY;
				}
			}

			group.MinX =
				minX -
				WheelBoundsExpansion;

			group.MaxX =
				maxX +
				WheelBoundsExpansion;

			group.MinY =
				minY -
				WheelBoundsExpansion;

			group.MaxY =
				maxY +
				WheelBoundsExpansion;
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
			colliderGrid =
				new List<int>[
					cellCount
				];

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
			colliderMinX.Length !=
			colliderCount)
		{
			colliderMinX =
				new float[
					colliderCount
				];

			colliderMaxX =
				new float[
					colliderCount
				];

			colliderMinY =
				new float[
					colliderCount
				];

			colliderMaxY =
				new float[
					colliderCount
				];

			terrainColliderQueryStamp =
				new int[
					colliderCount
				];
		}
		else if (
			terrainColliderQueryStamp == null ||
			terrainColliderQueryStamp.Length !=
			colliderCount)
		{
			terrainColliderQueryStamp =
				new int[
					colliderCount
				];

			terrainColliderQueryStampId =
				0;
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
			return
				colliderGridWidth - 1;
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
			return
				colliderGridHeight - 1;
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

		// Only damp motion INTO the surface.
		//
		// Motion away from the surface is never damped here.
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

		// GroundStick intentionally remains zero.
		//
		// We do not add an artificial velocity toward the surface.
		if (GroundStick > 0.0f)
		{
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
		}

		// Very small tangential drag.
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
				x =
					left;

				impacted[i] =
					true;

				impactNormalX[i] =
					1.0f;

				impactNormalY[i] =
					0.0f;
			}
			else if (x > right)
			{
				x =
					right;

				impacted[i] =
					true;

				impactNormalX[i] =
					-1.0f;

				impactNormalY[i] =
					0.0f;
			}

			if (y < top)
			{
				y =
					top;

				impacted[i] =
					true;

				impactNormalX[i] =
					0.0f;

				impactNormalY[i] =
					1.0f;
			}
			else if (y > bottom)
			{
				y =
					bottom;

				impacted[i] =
					true;

				impactNormalX[i] =
					0.0f;

				impactNormalY[i] =
					-1.0f;
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

		// --------------------------------------------------------
		// IMPORTANT:
		//
		// Do not sleep particles that are touching a meaningful
		// slanted surface.
		//
		// The old sleep system could turn shallow-slope water into
		// "glue": velocity becomes small -> particle sleeps -> the
		// particle remains on the surface.
		// --------------------------------------------------------

		if (impacted[i])
		{
			float normalY =
				Mathf.Abs(
					impactNormalY[i]
				);

			// If the normal is not close to vertical, this is a
			// meaningful slanted surface.
			//
			// Keep the particle awake so gravity can continue to
			// move it downhill.
			if (
				normalY <
				HorizontalSurfaceNormalY)
			{
				sleepProgress[i] =
					0.0f;

				sleeping[i] =
					false;

				return;
			}
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
				q *
				q;

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
					q *
					q;

				density +=
					q2 *
					q;

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
				gradSumX *
				gradSumX +
				gradSumY *
				gradSumY +
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
	// Remaining solver helpers
	// ============================================================

	private void EnsureBuffers(
		int count)
	{
		int requiredStride =
			MaxNeighbors;

		if (
			neighborStride !=
			requiredStride)
		{
			neighborStride =
				requiredStride;
		}

		int requiredNeighborLength =
			count *
			neighborStride;

		if (
			neighborBuffer == null ||
			neighborBuffer.Length <
			requiredNeighborLength)
		{
			neighborBuffer =
				new int[
					requiredNeighborLength
				];

			neighborDx =
				new float[
					requiredNeighborLength
				];

			neighborDy =
				new float[
					requiredNeighborLength
				];

			neighborQ =
				new float[
					requiredNeighborLength
				];

			neighborGradientScale =
				new float[
					requiredNeighborLength
				];
		}

		if (
			neighborCounts == null ||
			neighborCounts.Length <
			count)
		{
			neighborCounts =
				new int[count];
		}

		if (
			lambdas == null ||
			lambdas.Length <
			count)
		{
			lambdas =
				new float[count];

			particleDensity =
				new float[count];

			sleepProgress =
				new float[count];

			sleeping =
				new bool[count];

			impactNormalX =
				new float[count];

			impactNormalY =
				new float[count];

			impacted =
				new bool[count];

			SurfaceParticles =
				new bool[count];

			packingNearestDistances =
				new float[count];
		}
		else if (
			SurfaceParticles == null ||
			SurfaceParticles.Length <
			count)
		{
			SurfaceParticles =
				new bool[count];
		}
	}

	private void EnsurePackingBuffer(
		int count)
	{
		if (
			packingNearestDistances == null ||
			packingNearestDistances.Length <
			count)
		{
			packingNearestDistances =
				new float[count];
		}
	}

	private static double
		ElapsedMilliseconds(
			long startTimestamp)
	{
		return
			(
				Stopwatch.GetTimestamp() -
				startTimestamp
			) *
			1000.0 /
			Stopwatch.Frequency;
	}

	private void PrintProfiler(
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
			"========== PBF PROFILER =========="
		);

		GD.Print(
			$"Particles: {neighborCounts?.Length ?? 0}"
		);

		GD.Print(
			$"Predict: {predictMs:F3} ms"
		);

		GD.Print(
			$"SpatialHash: {spatialHashMs:F3} ms"
		);

		GD.Print(
			$"NeighborCache: {neighborCacheMs:F3} ms"
		);

		GD.Print(
			$"PBF: {pbfMs:F3} ms"
		);

		GD.Print(
			$"Collision: {collisionMs:F3} ms"
		);

		GD.Print(
			$"TerrainQuery: {terrainQueryMs:F3} ms"
		);

		GD.Print(
			$"TerrainResolve: {terrainResolveMs:F3} ms"
		);

		GD.Print(
			$"Wheel: {wheelCollisionMs:F3} ms"
		);

		GD.Print(
			$"Bounds: {boundsMs:F3} ms"
		);

		GD.Print(
			$"Velocity: {velocityMs:F3} ms"
		);

		GD.Print(
			$"TOTAL: {totalMs:F3} ms"
		);

		GD.Print(
			"=================================="
		);
	}
}
