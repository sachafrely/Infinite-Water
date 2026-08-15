using System;
using Godot;

/// <summary>
/// Aggregates and prints periodic simulation profiler output.
/// </summary>
internal sealed class SimulationProfiler
{
	// ============================================================
	// Report
	// ============================================================

	internal readonly struct Report
	{
		public readonly float WorldWidth;
		public readonly float WorldHeight;
		public readonly float WorldMinX;
		public readonly float WorldMinY;
		public readonly float WorldMaxX;
		public readonly float WorldMaxY;
		public readonly Vector2 SimulationWorldCenter;

		public readonly int ActiveParticleCount;
		public readonly int ParticleCapacity;

		public readonly long EvaporatedParticlesThisCleanup;
		public readonly long TotalEvaporatedParticles;
		public readonly int AntiLagCleanupCount;

		// ========================================================
		// Evaporation spatial analysis
		// ========================================================

		public readonly int[] EvaporationSpatialCounts;
		public readonly int EvaporationGridWidth;
		public readonly int EvaporationGridHeight;

		public readonly int EvaporationSpatialParticleCount;

		public readonly double EvaporationAverageX;
		public readonly double EvaporationAverageY;

		public readonly float EvaporationMinX;
		public readonly float EvaporationMaxX;
		public readonly float EvaporationMinY;
		public readonly float EvaporationMaxY;

		public readonly long TotalRainSpawns;
		public readonly long RainRejectedByDensity;
		public readonly long RainRejectedByCapacity;

		public readonly int DensityCapacity;
		public readonly int MaxCellOccupancy;
		public readonly int OccupiedDensityCells;

		public readonly int PixelGridWidth;
		public readonly int PixelGridHeight;

		public readonly int DensityWidth;
		public readonly int DensityHeight;
		public readonly float DensityCellSize;

		public readonly EnergySystem EnergySystem;
		public readonly double EnergyGeneratedThisFrame;
		public readonly float LastPhysicsDelta;

		public readonly int WheelCount;
		public readonly double[] WheelEnergyGeneratedThisFrame;

		public Report(
			float worldWidth,
			float worldHeight,
			float worldMinX,
			float worldMinY,
			float worldMaxX,
			float worldMaxY,
			Vector2 simulationWorldCenter,
			int activeParticleCount,
			int particleCapacity,
			long evaporatedParticlesThisCleanup,
			long totalEvaporatedParticles,
			int antiLagCleanupCount,
			int[] evaporationSpatialCounts,
			int evaporationGridWidth,
			int evaporationGridHeight,
			int evaporationSpatialParticleCount,
			double evaporationAverageX,
			double evaporationAverageY,
			float evaporationMinX,
			float evaporationMaxX,
			float evaporationMinY,
			float evaporationMaxY,
			long totalRainSpawns,
			long rainRejectedByDensity,
			long rainRejectedByCapacity,
			int densityCapacity,
			int maxCellOccupancy,
			int occupiedDensityCells,
			int pixelGridWidth,
			int pixelGridHeight,
			int densityWidth,
			int densityHeight,
			float densityCellSize,
			EnergySystem energySystem,
			double energyGeneratedThisFrame,
			float lastPhysicsDelta,
			int wheelCount,
			double[] wheelEnergyGeneratedThisFrame)
		{
			WorldWidth = worldWidth;
			WorldHeight = worldHeight;
			WorldMinX = worldMinX;
			WorldMinY = worldMinY;
			WorldMaxX = worldMaxX;
			WorldMaxY = worldMaxY;
			SimulationWorldCenter = simulationWorldCenter;

			ActiveParticleCount = activeParticleCount;
			ParticleCapacity = particleCapacity;

			EvaporatedParticlesThisCleanup =
				evaporatedParticlesThisCleanup;

			TotalEvaporatedParticles =
				totalEvaporatedParticles;

			AntiLagCleanupCount =
				antiLagCleanupCount;

			EvaporationSpatialCounts =
				evaporationSpatialCounts ??
				Array.Empty<int>();

			EvaporationGridWidth =
				evaporationGridWidth;

			EvaporationGridHeight =
				evaporationGridHeight;

			EvaporationSpatialParticleCount =
				evaporationSpatialParticleCount;

			EvaporationAverageX =
				evaporationAverageX;

			EvaporationAverageY =
				evaporationAverageY;

			EvaporationMinX =
				evaporationMinX;

			EvaporationMaxX =
				evaporationMaxX;

			EvaporationMinY =
				evaporationMinY;

			EvaporationMaxY =
				evaporationMaxY;

			TotalRainSpawns =
				totalRainSpawns;

			RainRejectedByDensity =
				rainRejectedByDensity;

			RainRejectedByCapacity =
				rainRejectedByCapacity;

			DensityCapacity =
				densityCapacity;

			MaxCellOccupancy =
				maxCellOccupancy;

			OccupiedDensityCells =
				occupiedDensityCells;

			PixelGridWidth =
				pixelGridWidth;

			PixelGridHeight =
				pixelGridHeight;

			DensityWidth =
				densityWidth;

			DensityHeight =
				densityHeight;

			DensityCellSize =
				densityCellSize;

			EnergySystem =
				energySystem;

			EnergyGeneratedThisFrame =
				energyGeneratedThisFrame;

			LastPhysicsDelta =
				lastPhysicsDelta;

			WheelCount =
				wheelCount;

			WheelEnergyGeneratedThisFrame =
				wheelEnergyGeneratedThisFrame ??
				Array.Empty<double>();
		}
	}

	// ============================================================
	// Constants
	// ============================================================

	public const int FullProfilerInterval = 600;

	// ============================================================
	// Accumulators
	// ============================================================

	private int fullProfilerFrames = 0;

	private double fullPhysicsTime = 0.0;

	private double fullRenderedFpsSum = 0.0;

	private double fullSpawnTime = 0.0;

	private double fullPbfTime = 0.0;

	private double fullDensityTime = 0.0;

	private double fullRendererTime = 0.0;

	private double fullRendererBuildPixelsTime = 0.0;

	private double fullRendererSurfaceGlowTime = 0.0;

	private double fullRendererFillBytesTime = 0.0;

	private double fullRendererTextureUploadTime = 0.0;

	// ============================================================
	// Properties
	// ============================================================

	public double LastFps
	{
		get;
		private set;
	}

	// ============================================================
	// Accumulate
	// ============================================================

	public void Accumulate(
		double physicsMs,
		double renderedFps,
		double spawnMs,
		double pbfMs,
		double densityMs,
		double rendererMs,
		double rendererBuildPixelsMs,
		double rendererSurfaceGlowMs,
		double rendererFillBytesMs,
		double rendererTextureUploadMs)
	{
		fullPhysicsTime +=
			physicsMs;

		fullRenderedFpsSum +=
			renderedFps;

		fullSpawnTime +=
			spawnMs;

		fullPbfTime +=
			pbfMs;

		fullDensityTime +=
			densityMs;

		fullRendererTime +=
			rendererMs;

		fullRendererBuildPixelsTime +=
			rendererBuildPixelsMs;

		fullRendererSurfaceGlowTime +=
			rendererSurfaceGlowMs;

		fullRendererFillBytesTime +=
			rendererFillBytesMs;

		fullRendererTextureUploadTime +=
			rendererTextureUploadMs;

		fullProfilerFrames++;
	}

	// ============================================================
	// Flush
	// ============================================================

	public bool TryFlush(
		Report report,
		Action<double> onFpsComputed = null)
	{
		if (
			fullProfilerFrames <
			FullProfilerInterval)
		{
			return false;
		}

		double frameCount =
			fullProfilerFrames;

		double physicsMs =
			fullPhysicsTime /
			frameCount;

		double spawnMs =
			fullSpawnTime /
			frameCount;

		double pbfMs =
			fullPbfTime /
			frameCount;

		double densityMs =
			fullDensityTime /
			frameCount;

		double rendererMs =
			fullRendererTime /
			frameCount;

		double rendererBuildPixelsMs =
			fullRendererBuildPixelsTime /
			frameCount;

		double rendererSurfaceGlowMs =
			fullRendererSurfaceGlowTime /
			frameCount;

		double rendererFillBytesMs =
			fullRendererFillBytesTime /
			frameCount;

		double rendererTextureUploadMs =
			fullRendererTextureUploadTime /
			frameCount;

		double measuredWork =
			spawnMs +
			pbfMs +
			densityMs +
			rendererMs;

		double otherMs =
			physicsMs -
			measuredWork;

		if (
			otherMs < 0.0)
		{
			otherMs = 0.0;
		}

		double fps =
			fullProfilerFrames > 0
				? fullRenderedFpsSum /
					fullProfilerFrames
				: 0.0;

		LastFps =
			fps;

		onFpsComputed?.Invoke(
			fps
		);

		double physicsFps =
			physicsMs > 0.001
				? 1000.0 /
					physicsMs
				: 0.0;

		GD.Print(
			"========================================"
		);

		GD.Print(
			"FULL FRAME PROFILER " +
			"(avg over " +
			fullProfilerFrames +
			" physics frames)"
		);

		GD.Print(
			"SimulationWorld=" +
			report.WorldWidth +
			"x" +
			report.WorldHeight +
			" (" +
			report.WorldMinX +
			"," +
			report.WorldMinY +
			") -> (" +
			report.WorldMaxX +
			"," +
			report.WorldMaxY +
			")"
		);

		GD.Print(
			"SimulationWorldCenter=" +
			report.SimulationWorldCenter
		);

		GD.Print(
			"ActiveParticles=" +
			report.ActiveParticleCount +
			"/" +
			report.ParticleCapacity
		);

		GD.Print(
			"Evaporated Particles=" +
			report.EvaporatedParticlesThisCleanup
		);

		GD.Print(
			"Total Evaporated Particles=" +
			report.TotalEvaporatedParticles
		);

		GD.Print(
			"AntiLagCleanupCount=" +
			report.AntiLagCleanupCount
		);

		// ========================================================
		// Evaporation spatial analysis
		// ========================================================

		GD.Print(
			"EvaporationSpatialParticles=" +
			report.EvaporationSpatialParticleCount
		);

		if (
			report.EvaporationSpatialParticleCount >
			0)
		{
			GD.Print(
				"EvaporationAveragePosition=(" +
				report.EvaporationAverageX.ToString("F1") +
				"," +
				report.EvaporationAverageY.ToString("F1") +
				")"
			);

			GD.Print(
				"EvaporationPositionRange=(" +
				report.EvaporationMinX.ToString("F1") +
				"," +
				report.EvaporationMinY.ToString("F1") +
				") -> (" +
				report.EvaporationMaxX.ToString("F1") +
				"," +
				report.EvaporationMaxY.ToString("F1") +
				")"
			);

			PrintEvaporationSpatialGrid(
				report
			);
		}

		GD.Print(
			"ParticleCapacity=" +
			report.ParticleCapacity
		);

		GD.Print(
			"TotalRainSpawns=" +
			report.TotalRainSpawns
		);

		GD.Print(
			"RainRejectedByDensity=" +
			report.RainRejectedByDensity
		);

		GD.Print(
			"RainRejectedByCapacity=" +
			report.RainRejectedByCapacity
		);

		GD.Print(
			"DensityCapacity=" +
			report.DensityCapacity +
			" particles/pixel"
		);

		GD.Print(
			"MaxCellOccupancy=" +
			report.MaxCellOccupancy
		);

		GD.Print(
			"OccupiedDensityCells=" +
			report.OccupiedDensityCells +
			"/" +
			(
				report.PixelGridWidth *
				report.PixelGridHeight
			)
		);

		GD.Print(
			"DensityGrid=" +
			report.DensityWidth +
			"x" +
			report.DensityHeight +
			" cells @ " +
			report.DensityCellSize +
			" px"
		);

		GD.Print(
			"RenderedFPS=" +
			fps.ToString("F1")
		);

		GD.Print(
			"PhysicsFPS=" +
			physicsFps.ToString("F1")
		);

		GD.Print(
			"PhysicsProcess=" +
			physicsMs.ToString("F2") +
			"ms"
		);

		GD.Print(
			"  Spawn=" +
			spawnMs.ToString("F2") +
			"ms"
		);

		GD.Print(
			"  PBF=" +
			pbfMs.ToString("F2") +
			"ms"
		);

		GD.Print(
			"  Density=" +
			densityMs.ToString("F2") +
			"ms"
		);

		GD.Print(
			"  Renderer=" +
			rendererMs.ToString("F2") +
			"ms"
		);

		GD.Print(
			"    BuildPixels=" +
			rendererBuildPixelsMs.ToString("F2") +
			"ms"
		);

		GD.Print(
			"    SurfaceGlow=" +
			rendererSurfaceGlowMs.ToString("F2") +
			"ms"
		);

		GD.Print(
			"    FillBytes=" +
			rendererFillBytesMs.ToString("F2") +
			"ms"
		);

		GD.Print(
			"    TextureUpload=" +
			rendererTextureUploadMs.ToString("F2") +
			"ms"
		);

		GD.Print(
			"  Other=" +
			otherMs.ToString("F2") +
			"ms"
		);

		GD.Print(
			"MeasuredWork=" +
			measuredWork.ToString("F2") +
			"ms"
		);

		GD.Print(
			"Energy=" +
			report.EnergySystem.Energy.ToString("F2")
		);

		GD.Print(
			"EnergyThisFrame=" +
			report.EnergyGeneratedThisFrame.ToString("F2")
		);

		double energyPerSecond =
			report.LastPhysicsDelta > 0.000001f
				? report.EnergyGeneratedThisFrame /
					report.LastPhysicsDelta
				: 0.0;

		GD.Print(
			"EnergyPerSecond=" +
			energyPerSecond.ToString("F2")
		);

		GD.Print(
			"TotalEnergyGenerated=" +
			report.EnergySystem.TotalGenerated.ToString("F2")
		);

		for (
			int i = 0;
			i < report.WheelCount;
			i++)
		{
			double wheelEnergy =
				i < report.WheelEnergyGeneratedThisFrame.Length
					? report.WheelEnergyGeneratedThisFrame[i]
					: 0.0;

			double wheelEnergyPerSecond =
				report.LastPhysicsDelta > 0.000001f
					? wheelEnergy /
						report.LastPhysicsDelta
					: 0.0;

			GD.Print(
				"Wheel " +
				(i + 1) +
				" EnergyThisFrame=" +
				wheelEnergy.ToString("F4") +
				" EnergyPerSecond=" +
				wheelEnergyPerSecond.ToString("F4")
			);
		}

		GD.Print(
			"========================================"
		);

		Reset();

		return true;
	}

	// ============================================================
	// Evaporation grid printing
	// ============================================================

	private void PrintEvaporationSpatialGrid(
		Report report)
	{
		GD.Print(
			"EvaporationSpatial=" +
			report.EvaporationGridWidth +
			"x" +
			report.EvaporationGridHeight +
			" (rows=Y, columns=X)"
		);

		for (
			int y =
				report.EvaporationGridHeight - 1;
			y >= 0;
			y--)
		{
			string row = "";

			for (
				int x = 0;
				x < report.EvaporationGridWidth;
				x++)
			{
				int index =
					y *
					report.EvaporationGridWidth +
					x;

				int count =
					index >= 0 &&
					index < report.EvaporationSpatialCounts.Length
						? report.EvaporationSpatialCounts[index]
						: 0;

				row +=
					count.ToString("D4") +
					" ";
			}

			GD.Print(
				"  " +
				row
			);
		}

		// Find the strongest concentration.
		int highestIndex = -1;
		int highestCount = 0;

		for (
			int i = 0;
			i < report.EvaporationSpatialCounts.Length;
			i++)
		{
			if (
				report.EvaporationSpatialCounts[i] >
				highestCount)
			{
				highestCount =
					report.EvaporationSpatialCounts[i];

				highestIndex =
					i;
			}
		}

		if (
			highestIndex < 0)
		{
			return;
		}

		int cellX =
			highestIndex %
			report.EvaporationGridWidth;

		int cellY =
			highestIndex /
			report.EvaporationGridWidth;

		float cellWidth =
			(
				report.WorldMaxX -
				report.WorldMinX
			) /
			report.EvaporationGridWidth;

		float cellHeight =
			(
				report.WorldMaxY -
				report.WorldMinY
			) /
			report.EvaporationGridHeight;

		float cellMinX =
			report.WorldMinX +
			cellX *
			cellWidth;

		float cellMaxX =
			cellMinX +
			cellWidth;

		float cellMinY =
			report.WorldMinY +
			cellY *
			cellHeight;

		float cellMaxY =
			cellMinY +
			cellHeight;

		GD.Print(
			"EvaporationHighestCell=" +
			"(" +
			cellX +
			"," +
			cellY +
			") Count=" +
			highestCount +
			" Bounds=(" +
			cellMinX.ToString("F1") +
			"," +
			cellMinY.ToString("F1") +
			") -> (" +
			cellMaxX.ToString("F1") +
			"," +
			cellMaxY.ToString("F1") +
			")"
		);
	}

	// ============================================================
	// Reset
	// ============================================================

	private void Reset()
	{
		fullProfilerFrames = 0;
		fullPhysicsTime = 0.0;
		fullRenderedFpsSum = 0.0;
		fullSpawnTime = 0.0;
		fullPbfTime = 0.0;
		fullDensityTime = 0.0;
		fullRendererTime = 0.0;
		fullRendererBuildPixelsTime = 0.0;
		fullRendererSurfaceGlowTime = 0.0;
		fullRendererFillBytesTime = 0.0;
		fullRendererTextureUploadTime = 0.0;
	}
}
