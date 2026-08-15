using System;

/// <summary>
/// PbfState — mutable per-step state for the PBF solver pipeline.
///
/// Owns all transient arrays that are written and read during a single
/// solver tick.  Authoritative cross-frame state (positions, velocities)
/// remains in <see cref="ParticleData"/>.
///
/// Call <see cref="EnsureCapacity"/> at the start of each tick before
/// passing this object to any sub-module.
/// </summary>
internal sealed class PbfState
{
	// ============================================================
	// Neighbor cache
	// ============================================================

	/// <summary>Maximum neighbors stored per particle (= MaxNeighbors).</summary>
	public int NeighborStride;

	/// <summary>Flat neighbor-index buffer: particle i's neighbors start at i*NeighborStride.</summary>
	public int[] NeighborBuffer;

	/// <summary>Number of neighbors found for each particle.</summary>
	public int[] NeighborCounts;

	/// <summary>Pre-computed dx (pi.x - pj.x) for each neighbor pair.</summary>
	public float[] NeighborDx;

	/// <summary>Pre-computed dy (pi.y - pj.y) for each neighbor pair.</summary>
	public float[] NeighborDy;

	/// <summary>Kernel weight q for each neighbor pair.</summary>
	public float[] NeighborQ;

	/// <summary>Gradient scale for each neighbor pair (used in lambda/delta passes).</summary>
	public float[] NeighborGradientScale;

	// ============================================================
	// Per-particle working arrays
	// ============================================================

	/// <summary>PBF constraint scalar λ_i.</summary>
	public float[] Lambdas;

	/// <summary>Density estimate ρ_i from kernel sum.</summary>
	public float[] ParticleDensity;

	/// <summary>Sleep progress [0, 1] per particle.</summary>
	public float[] SleepProgress;

	/// <summary>Whether each particle is currently sleeping.</summary>
	public bool[] Sleeping;

	/// <summary>Whether each particle is a surface (low-density) particle.</summary>
	public bool[] SurfaceParticles;

	// ============================================================
	// Impact / collision state
	// ============================================================

	/// <summary>Accumulated contact normal X for each particle (normalized).</summary>
	public float[] ImpactNormalX;

	/// <summary>Accumulated contact normal Y for each particle (normalized).</summary>
	public float[] ImpactNormalY;

	/// <summary>Whether each particle has a valid contact normal this tick.</summary>
	public bool[] Impacted;

	// ============================================================
	// Debug / profiler scratch
	// ============================================================

	/// <summary>Nearest-neighbor distance per particle, used for packing stats.</summary>
	public float[] PackingNearestDistances;

	// ============================================================
	// Pixel occupancy open-addressed hash table
	// ============================================================

	/// <summary>Pixel X coordinate stored in each slot.</summary>
	public int[] PixelOccupancyX;

	/// <summary>Pixel Y coordinate stored in each slot.</summary>
	public int[] PixelOccupancyY;

	/// <summary>Occupancy count (0–MaxParticlesPerPixel) in each slot.</summary>
	public int[] PixelOccupancyCount;

	/// <summary>Index of the first particle occupying this slot.</summary>
	public int[] PixelOccupancyFirstParticle;

	/// <summary>Index of the second particle occupying this slot (or -1).</summary>
	public int[] PixelOccupancySecondParticle;

	/// <summary>Generation stamp for each slot — allows reuse without clearing the table.</summary>
	public int[] PixelOccupancyStamp;

	/// <summary>Current generation counter; increment each tick.</summary>
	public int PixelOccupancyGeneration;

	// ============================================================
	// Constructor
	// ============================================================

	public PbfState()
	{
		InitializePixelOccupancyTable();
	}

	// ============================================================
	// Initialization
	// ============================================================

	private void InitializePixelOccupancyTable()
	{
		int size = PbfSolver.PixelOccupancyTableSize;

		PixelOccupancyX =
			new int[size];

		PixelOccupancyY =
			new int[size];

		PixelOccupancyCount =
			new int[size];

		PixelOccupancyFirstParticle =
			new int[size];

		PixelOccupancySecondParticle =
			new int[size];

		PixelOccupancyStamp =
			new int[size];

		PixelOccupancyGeneration = 0;
	}

	// ============================================================
	// Capacity management
	// ============================================================

	/// <summary>
	/// Ensures all arrays are large enough for <paramref name="count"/> particles.
	/// Must be called at the start of each solver tick before using sub-modules.
	/// </summary>
	public void EnsureCapacity(int count)
	{
		int requiredStride = PbfSolver.MaxNeighbors;

		if (NeighborStride != requiredStride)
			NeighborStride = requiredStride;

		int requiredNeighborLength = count * NeighborStride;

		if (
			NeighborBuffer == null ||
			NeighborBuffer.Length < requiredNeighborLength)
		{
			NeighborBuffer =
				new int[requiredNeighborLength];

			NeighborDx =
				new float[requiredNeighborLength];

			NeighborDy =
				new float[requiredNeighborLength];

			NeighborQ =
				new float[requiredNeighborLength];

			NeighborGradientScale =
				new float[requiredNeighborLength];
		}

		if (
			NeighborCounts == null ||
			NeighborCounts.Length < count)
		{
			NeighborCounts = new int[count];
		}

		if (
			Lambdas == null ||
			Lambdas.Length < count)
		{
			Lambdas =
				new float[count];

			ParticleDensity =
				new float[count];

			SleepProgress =
				new float[count];

			Sleeping =
				new bool[count];

			ImpactNormalX =
				new float[count];

			ImpactNormalY =
				new float[count];

			Impacted =
				new bool[count];

			SurfaceParticles =
				new bool[count];

			PackingNearestDistances =
				new float[count];
		}
		else if (
			SurfaceParticles == null ||
			SurfaceParticles.Length < count)
		{
			SurfaceParticles = new bool[count];
		}
	}

	/// <summary>
	/// Ensures the packing-nearest-distance scratch buffer is large enough.
	/// Called only when the profiler is about to print packing stats.
	/// </summary>
	public void EnsurePackingBuffer(int count)
	{
		if (
			PackingNearestDistances == null ||
			PackingNearestDistances.Length < count)
		{
			PackingNearestDistances =
				new float[count];
		}
	}
}
