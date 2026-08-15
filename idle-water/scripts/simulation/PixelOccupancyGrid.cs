using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Tracks per-pixel particle occupancy for rain spawn density checks.
/// </summary>
internal sealed class PixelOccupancyGrid
{
	// ============================================================
	// Default configuration
	// ============================================================

	public const int PixelGridWidth = 920;

	public const int PixelGridHeight = 1300;

	public const int MaxParticlesPerDensityCell = 1;

	// ============================================================
	// State
	// ============================================================

	private readonly int gridWidth;

	private readonly int gridHeight;

	private readonly float worldMinX;

	private readonly float worldMinY;

	private readonly int maxPerCell;

	private int[] pixelOccupancy;

	private int[] pixelOccupancyStamp;

	private int pixelOccupancyGeneration = 0;

	private readonly List<int> occupiedPixelIndices =
		new List<int>();

	private int maxPixelOccupancy = 0;

	private int occupiedPixelCount = 0;

	// ============================================================
	// Properties
	// ============================================================

	/// <summary>
	/// Gets the configured grid width.
	/// </summary>
	public int GridWidth =>
		gridWidth;

	/// <summary>
	/// Gets the configured grid height.
	/// </summary>
	public int GridHeight =>
		gridHeight;

	/// <summary>
	/// Gets the maximum number of particles allowed per density cell.
	/// </summary>
	public int MaxPerCell =>
		maxPerCell;

	/// <summary>
	/// Gets the highest occupancy observed in the current generation.
	/// </summary>
	public int MaxPixelOccupancy =>
		maxPixelOccupancy;

	/// <summary>
	/// Gets the number of occupied density cells in the current generation.
	/// </summary>
	public int OccupiedPixelCount =>
		occupiedPixelCount;

	// ============================================================
	// Construction
	// ============================================================

	/// <summary>
	/// Creates a new occupancy grid.
	/// </summary>
	public PixelOccupancyGrid(
		int gridWidth,
		int gridHeight,
		float worldMinX,
		float worldMinY,
		int maxPerCell)
	{
		this.gridWidth =
			gridWidth;

		this.gridHeight =
			gridHeight;

		this.worldMinX =
			worldMinX;

		this.worldMinY =
			worldMinY;

		this.maxPerCell =
			maxPerCell;

		InitializePixelOccupancy();
	}

	// ============================================================
	// Initialization
	// ============================================================

	/// <summary>
	/// Allocates the occupancy buffers and resets tracked metrics.
	/// </summary>
	public void InitializePixelOccupancy()
	{
		int pixelCount =
			gridWidth *
			gridHeight;

		pixelOccupancy =
			new int[pixelCount];

		pixelOccupancyStamp =
			new int[pixelCount];

		pixelOccupancyGeneration =
			1;

		occupiedPixelIndices.Clear();

		maxPixelOccupancy = 0;

		occupiedPixelCount = 0;
	}

	// ============================================================
	// Coordinate lookup
	// ============================================================

	/// <summary>
	/// Converts a world-space position into an occupancy pixel index.
	/// </summary>
	public bool TryGetPixelIndex(
		float x,
		float y,
		out int pixelIndex)
	{
		pixelIndex = -1;

		int pixelX =
			Mathf.FloorToInt(
				x -
				worldMinX
			);

		int pixelY =
			Mathf.FloorToInt(
				y -
				worldMinY
			);

		if (
			pixelX < 0 ||
			pixelX >= gridWidth ||
			pixelY < 0 ||
			pixelY >= gridHeight)
		{
			return false;
		}

		pixelIndex =
			pixelY *
			gridWidth +
			pixelX;

		return true;
	}

	// ============================================================
	// Occupancy queries
	// ============================================================

	/// <summary>
	/// Gets the occupancy count for one pixel in the current generation.
	/// </summary>
	public int GetPixelOccupancy(
		int pixelIndex)
	{
		if (
			pixelIndex < 0 ||
			pixelIndex >= pixelOccupancy.Length)
		{
			return 0;
		}

		if (
			pixelOccupancyStamp[pixelIndex] !=
			pixelOccupancyGeneration)
		{
			return 0;
		}

		return pixelOccupancy[pixelIndex];
	}

	/// <summary>
	/// Returns whether a particle can be spawned at the target pixel.
	/// </summary>
	public bool CanSpawnAtPixel(
		float x,
		float y,
		out int pixelIndex)
	{
		if (
			!TryGetPixelIndex(
				x,
				y,
				out pixelIndex
			))
		{
			return true;
		}

		return
			GetPixelOccupancy(
				pixelIndex
			) <
			maxPerCell;
	}

	// ============================================================
	// Occupancy mutation
	// ============================================================

	/// <summary>
	/// Registers a particle into the current occupancy generation.
	/// </summary>
	public void RegisterParticlePixel(
		int pixelIndex)
	{
		if (
			pixelIndex < 0 ||
			pixelIndex >= pixelOccupancy.Length)
		{
			return;
		}

		if (
			pixelOccupancyStamp[pixelIndex] !=
			pixelOccupancyGeneration)
		{
			pixelOccupancyStamp[pixelIndex] =
				pixelOccupancyGeneration;

			pixelOccupancy[pixelIndex] =
				0;

			occupiedPixelIndices.Add(
				pixelIndex
			);

			occupiedPixelCount++;
		}

		int occupancy =
			++pixelOccupancy[pixelIndex];

		if (
			occupancy >
			maxPixelOccupancy)
		{
			maxPixelOccupancy =
				occupancy;
		}
	}

	/// <summary>
	/// Rebuilds occupancy from the active particle set.
	/// </summary>
	public void RebuildPixelOccupancy(
		ParticleData particles)
	{
		pixelOccupancyGeneration++;

		if (
			pixelOccupancyGeneration == int.MaxValue)
		{
			Array.Clear(
				pixelOccupancyStamp,
				0,
				pixelOccupancyStamp.Length
			);

			pixelOccupancyGeneration =
				1;
		}

		int generation =
			pixelOccupancyGeneration;

		occupiedPixelIndices.Clear();

		occupiedPixelCount = 0;

		maxPixelOccupancy = 0;

		for (
			int i = 0;
			i < particles.Count;
			i++)
		{
			int pixelIndex;

			if (
				!TryGetPixelIndex(
					particles.PosX[i],
					particles.PosY[i],
					out pixelIndex
				))
			{
				continue;
			}

			if (
				pixelOccupancyStamp[pixelIndex] !=
				generation)
			{
				pixelOccupancyStamp[pixelIndex] =
					generation;

				pixelOccupancy[pixelIndex] =
					1;

				occupiedPixelIndices.Add(
					pixelIndex
				);

				occupiedPixelCount++;

				if (
					maxPixelOccupancy < 1)
				{
					maxPixelOccupancy =
						1;
				}
			}
			else
			{
				int occupancy =
					++pixelOccupancy[pixelIndex];

				if (
					occupancy >
					maxPixelOccupancy)
				{
					maxPixelOccupancy =
						occupancy;
				}
			}
		}
	}
}
