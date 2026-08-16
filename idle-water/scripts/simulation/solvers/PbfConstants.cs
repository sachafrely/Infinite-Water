/// <summary>
/// All tuning constants for the PBF (Position-Based Fluids) solver.
/// Extracted from PbfSolver.cs as a partial class to keep simulation
/// parameters in one easy-to-find location.
///
/// NOTE: This file intentionally has no namespace to match the existing
/// PbfSolver.cs (global namespace). Adding IdleWater.Data here will require
/// a matching namespace change in PbfSolver.cs — do that as a single
/// coordinated step in a future refactor PR.
/// </summary>
public partial class PbfSolver
{
	// ============================================================
	// Profiler
	// ============================================================

	internal const int ProfilerIntervalFrames = 600;

	// ============================================================
	// Wheel bounds
	// ============================================================

	internal const float WheelBoundsExpansion = 2.5f;

	// ============================================================
	// Simulation
	// ============================================================

	internal const float Gravity = 300.0f;

	internal const float SmoothingRadius = 8.0f;
	internal const float SmoothingRadiusSquared = 64.0f;
	internal const float InverseSmoothingRadius = 1.0f / 8.0f;

	// ============================================================
	// Density
	// ============================================================

	internal const float RestDensity = 1.15f;
	internal const float InverseRestDensity = 1.0f / RestDensity;
	internal const float LambdaEpsilon = 0.00001f;

	// ============================================================
	// PBF
	// ============================================================

	internal const int MinIterations = 2;
	internal const int MaxIterations = 2;

	internal const float DensityErrorThreshold = 0.90f;
	internal const float MaxCorrection = 0.5f;
	internal const float MaxCorrectionSquared = 0.25f;

	// ============================================================
	// Stability
	// ============================================================

	internal const float VelocityDamping = 0.998f;

	// ============================================================
	// Surface behavior
	// ============================================================

	internal const float ImpactDamping = 0.10f;
	internal const float ImpactNormalEpsilon = 0.0001f;

	internal const float GroundDrag = 0.005f;
	internal const float GroundStick = 0.0f;

	internal const float SurfaceGravityRetention = 0.85f;

	internal const float HorizontalSurfaceNormalY = 0.92f;

	// ============================================================
	// Sleeping
	// ============================================================

	internal const float SleepVelocityThreshold = 1.0f;
	internal const float WakeVelocityThreshold = 3.0f;
	internal const float SleepTime = 0.50f;
	internal const float SleepDampingStrength = 1.5f;

	internal const float SleepVelocityThresholdSquared =
		SleepVelocityThreshold *
		SleepVelocityThreshold;

	internal const float WakeVelocityThresholdSquared =
		WakeVelocityThreshold *
		WakeVelocityThreshold;

	// ============================================================
	// Current simulation world
	// ============================================================

	internal const float MinX = -100.0f;
	internal const float MaxX = 820.0f;

	internal const float MinY = -50.0f;
	internal const float MaxY = 1210.0f;

	internal const float BoundarySkin = 0.5f;

	internal const float BoundaryRestitution = 0.03f;
	internal const float BoundaryFriction = 0.03f;

	internal const float BoundaryVelocityEpsilon = 0.5f;

	// ============================================================
	// Polygon
	// ============================================================

	internal const float PolygonParticleRadius = 2.5f;

	internal const float ColliderGridCellSize = 32.0f;
	internal const float ColliderGridExpansion = 1.0f;

	// ============================================================
	// Terrain optimization
	// ============================================================

	internal const float SweptCollisionDistanceSquared = 9.0f;

	internal const float TerrainBoundsExtraMargin = 0.25f;

	// ============================================================
	// Neighbors
	// ============================================================

	internal const int MaxNeighbors = 40;

	// ============================================================
	// Pixel occupancy
	//
	// OPTIMIZED:
	//
	// The previous implementation used:
	//
	//     Dictionary<long, int>
	//
	// This implementation uses a fixed open-addressed hash table.
	//
	// Each occupied slot stores:
	//
	//     X coordinate
	//     Y coordinate
	//     occupancy
	//     first particle index
	//     second particle index
	//
	// Generation stamps allow the table to be reused without
	// clearing the entire array every invocation.
	// ============================================================

	internal const int MaxParticlesPerPixel = 2;

	internal const float ExactOverlapDistanceSquared =
		0.000001f;

	internal const float ExactOverlapSeparation = 0.05f;

	// Maximum particle capacity is currently 4000.
	// 16384 slots gives a very low load factor and fast probing.
	internal const int PixelOccupancyTableSize = 16384;
	internal const int PixelOccupancyTableMask =
		PixelOccupancyTableSize - 1;
}
