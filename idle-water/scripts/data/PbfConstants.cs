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

	private const int ProfilerIntervalFrames = 600;

	// ============================================================
	// Wheel bounds
	// ============================================================

	private const float WheelBoundsExpansion = 2.5f;

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
	private const int MaxIterations = 2;

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

	private const float ImpactDamping = 0.10f;
	private const float ImpactNormalEpsilon = 0.0001f;

	private const float GroundDrag = 0.005f;
	private const float GroundStick = 0.0f;

	private const float SurfaceGravityRetention = 0.85f;

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

	private const float MinX = -100.0f;
	private const float MaxX = 920.0f;

	private const float MinY = -50.0f;
	private const float MaxY = 1250.0f;

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
	// Terrain optimization
	// ============================================================

	private const float SweptCollisionDistanceSquared = 9.0f;

	private const float TerrainBoundsExtraMargin = 0.25f;

	// ============================================================
	// Neighbors
	// ============================================================

	private const int MaxNeighbors = 40;

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

	private const int MaxParticlesPerPixel = 2;

	private const float ExactOverlapDistanceSquared =
		0.000001f;

	private const float ExactOverlapSeparation = 0.05f;

	// Maximum particle capacity is currently 4000.
	// 16384 slots gives a very low load factor and fast probing.
	private const int PixelOccupancyTableSize = 16384;
	private const int PixelOccupancyTableMask =
		PixelOccupancyTableSize - 1;
}
