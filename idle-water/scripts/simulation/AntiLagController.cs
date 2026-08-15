using Godot;

/// <summary>
/// Owns the anti-lag cleanup state machine.
///
/// In addition to controlling the cleanup process, this class records
/// where evaporated particles were located in the simulation world.
///
/// The evaporation position statistics use an 8x8 spatial grid.
/// </summary>
internal sealed class AntiLagController
{
	// ============================================================
	// State
	// ============================================================

	private enum AntiLagState
	{
		Normal,
		ReducingRain,
		Draining,
		Evaporating,
		Recovering
	}

	private const double AntiLagFpsThreshold = 25.0;

	private const int AntiLagRequiredLowProfilerResults = 4;

	private const float AntiLagRainReductionDuration = 10.0f;

	private const float AntiLagDrainDuration = 20.0f;

	private const float AntiLagEvaporationDuration = 10.0f;

	private const float AntiLagRecoveryDuration = 10.0f;

	// ============================================================
	// Evaporation spatial analysis
	// ============================================================

	/// <summary>
	/// Number of columns in the evaporation analysis grid.
	/// </summary>
	public const int EvaporationGridWidth = 8;

	/// <summary>
	/// Number of rows in the evaporation analysis grid.
	/// </summary>
	public const int EvaporationGridHeight = 8;

	private const int EvaporationGridCellCount =
		EvaporationGridWidth *
		EvaporationGridHeight;

	private readonly int[] evaporationSpatialCounts =
		new int[EvaporationGridCellCount];

	private float simulationWorldMinX = 0.0f;
	private float simulationWorldMinY = 0.0f;
	private float simulationWorldMaxX = 1.0f;
	private float simulationWorldMaxY = 1.0f;

	private double evaporationPositionSumX = 0.0;
	private double evaporationPositionSumY = 0.0;

	private float evaporationMinX = 0.0f;
	private float evaporationMaxX = 0.0f;
	private float evaporationMinY = 0.0f;
	private float evaporationMaxY = 0.0f;

	private bool hasEvaporationPositionData = false;

	// ============================================================
	// State variables
	// ============================================================

	private AntiLagState antiLagState =
		AntiLagState.Normal;

	private float antiLagStateTimer = 0.0f;

	private float antiLagStateStartRainPercent = 0.0f;

	private float antiLagRecoveryTargetRainPercent = 0.0f;

	private int consecutiveLowProfilerResults = 0;

	private long totalEvaporatedParticles = 0;

	private long evaporatedParticlesThisCleanup = 0;

	private int antiLagCleanupCount = 0;

	private int antiLagEvaporationStartParticleCount = 0;

	private int antiLagEvaporationParticlesRemoved = 0;

	// ============================================================
	// Properties
	// ============================================================

	public bool IsActive =>
		antiLagState != AntiLagState.Normal;

	public bool IsDrainingOrEvaporating =>
		antiLagState == AntiLagState.Draining ||
		antiLagState == AntiLagState.Evaporating;

	public long TotalEvaporatedParticles =>
		totalEvaporatedParticles;

	public long EvaporatedParticlesThisCleanup =>
		evaporatedParticlesThisCleanup;

	public int AntiLagCleanupCount =>
		antiLagCleanupCount;

	/// <summary>
	/// Gets the number of particles represented by the current
	/// evaporation spatial statistics.
	/// </summary>
	public int EvaporationSpatialParticleCount
	{
		get
		{
			int total = 0;

			for (
				int i = 0;
				i < evaporationSpatialCounts.Length;
				i++)
			{
				total += evaporationSpatialCounts[i];
			}

			return total;
		}
	}

	/// <summary>
	/// Gets a copy of the current evaporation spatial grid.
	///
	/// Index:
	///     y * EvaporationGridWidth + x
	/// </summary>
	public int[] CopyEvaporationSpatialCounts()
	{
		int[] copy =
			new int[
				evaporationSpatialCounts.Length
			];

		System.Array.Copy(
			evaporationSpatialCounts,
			copy,
			evaporationSpatialCounts.Length
		);

		return copy;
	}

	public double EvaporationAverageX
	{
		get
		{
			int count =
				EvaporationSpatialParticleCount;

			return count > 0
				? evaporationPositionSumX / count
				: 0.0;
		}
	}

	public double EvaporationAverageY
	{
		get
		{
			int count =
				EvaporationSpatialParticleCount;

			return count > 0
				? evaporationPositionSumY / count
				: 0.0;
		}
	}

	public float EvaporationMinX =>
		hasEvaporationPositionData
			? evaporationMinX
			: 0.0f;

	public float EvaporationMaxX =>
		hasEvaporationPositionData
			? evaporationMaxX
			: 0.0f;

	public float EvaporationMinY =>
		hasEvaporationPositionData
			? evaporationMinY
			: 0.0f;

	public float EvaporationMaxY =>
		hasEvaporationPositionData
			? evaporationMaxY
			: 0.0f;

	public float SimulationWorldMinX =>
		simulationWorldMinX;

	public float SimulationWorldMinY =>
		simulationWorldMinY;

	public float SimulationWorldMaxX =>
		simulationWorldMaxX;

	public float SimulationWorldMaxY =>
		simulationWorldMaxY;

	// ============================================================
	// World bounds
	// ============================================================

	/// <summary>
	/// Supplies the simulation world bounds used by the evaporation
	/// spatial analysis.
	/// </summary>
	public void SetSimulationWorldBounds(
		float minX,
		float minY,
		float maxX,
		float maxY)
	{
		simulationWorldMinX = minX;
		simulationWorldMinY = minY;
		simulationWorldMaxX = maxX;
		simulationWorldMaxY = maxY;
	}

	// ============================================================
	// Public entry points
	// ============================================================

	public void EvaluateAntiLagProfilerResult(
		double profilerFps,
		ParticleData particles,
		RainSystem rainSystem)
	{
		if (
			antiLagState !=
			AntiLagState.Normal)
		{
			return;
		}

		if (
			profilerFps <
			AntiLagFpsThreshold)
		{
			consecutiveLowProfilerResults++;

			GD.Print(
				"ANTI-LAG CHECK: low profiler FPS " +
				profilerFps.ToString("F1") +
				" (" +
				consecutiveLowProfilerResults +
				"/" +
				AntiLagRequiredLowProfilerResults +
				")"
			);

			if (
				consecutiveLowProfilerResults >=
				AntiLagRequiredLowProfilerResults)
			{
				StartAntiLagCleanup(
					profilerFps,
					particles,
					rainSystem
				);
			}
		}
		else
		{
			if (
				consecutiveLowProfilerResults > 0)
			{
				GD.Print(
					"ANTI-LAG CHECK: FPS recovered to " +
					profilerFps.ToString("F1") +
					", resetting consecutive low-FPS count."
				);
			}

			consecutiveLowProfilerResults = 0;
		}
	}

	/// <summary>
	/// Advances the anti-lag state machine.
	/// </summary>
	public void UpdateAntiLagCleanup(
		float dt,
		ParticleData particles,
		RainSystem rainSystem)
	{
		if (
			antiLagState ==
			AntiLagState.Normal)
		{
			return;
		}

		antiLagStateTimer +=
			Mathf.Max(
				dt,
				0.0f
			);

		switch (
			antiLagState)
		{
			case AntiLagState.ReducingRain:
				UpdateAntiLagRainReduction(
					rainSystem
				);
				break;

			case AntiLagState.Draining:
				rainSystem.CurrentRainPercent = 0.0f;

				rainSystem.TargetRainPercent = 0.0f;

				rainSystem.RainPhaseTimer =
					Mathf.Max(
						AntiLagDrainDuration -
						antiLagStateTimer,
						0.0f
					);

				if (
					antiLagStateTimer >=
					AntiLagDrainDuration)
				{
					BeginAntiLagEvaporation(
						particles
					);
				}

				break;

			case AntiLagState.Evaporating:
				UpdateAntiLagEvaporation(
					particles,
					rainSystem
				);
				break;

			case AntiLagState.Recovering:
				UpdateAntiLagRecovery(
					rainSystem
				);
				break;
		}
	}

	// ============================================================
	// Rain reduction
	// ============================================================

	private void UpdateAntiLagRainReduction(
		RainSystem rainSystem)
	{
		float progress =
			Mathf.Clamp(
				antiLagStateTimer /
				AntiLagRainReductionDuration,
				0.0f,
				1.0f
			);

		rainSystem.CurrentRainPercent =
			Mathf.Lerp(
				antiLagStateStartRainPercent,
				0.0f,
				progress
			);

		rainSystem.TargetRainPercent =
			0.0f;

		rainSystem.RainPhaseTimer =
			Mathf.Max(
				AntiLagRainReductionDuration -
				antiLagStateTimer,
				0.0f
			);

		if (
			antiLagStateTimer >=
			AntiLagRainReductionDuration)
		{
			antiLagState =
				AntiLagState.Draining;

			antiLagStateTimer =
				0.0f;

			rainSystem.CurrentRainPercent =
				0.0f;

			GD.Print(
				"ANTI-LAG: Rain reached 0%. Starting 20s natural drain."
			);
		}
	}

	// ============================================================
	// Evaporation
	// ============================================================

	private void BeginAntiLagEvaporation(
		ParticleData particles)
	{
		antiLagState =
			AntiLagState.Evaporating;

		antiLagStateTimer =
			0.0f;

		antiLagEvaporationStartParticleCount =
			particles.Count;

		antiLagEvaporationParticlesRemoved =
			0;

		ResetEvaporationSpatialStatistics();

		GD.Print(
			"ANTI-LAG: Natural drain complete. Remaining particles=" +
			particles.Count +
			". Starting 10s evaporation."
		);

		GD.Print(
			"ANTI-LAG: Evaporation target=" +
			antiLagEvaporationStartParticleCount +
			" particles over " +
			AntiLagEvaporationDuration.ToString("F0") +
			"s."
		);
	}

	private void UpdateAntiLagEvaporation(
		ParticleData particles,
		RainSystem rainSystem)
	{
		rainSystem.CurrentRainPercent =
			0.0f;

		rainSystem.TargetRainPercent =
			0.0f;

		rainSystem.RainPhaseTimer =
			Mathf.Max(
				AntiLagEvaporationDuration -
				antiLagStateTimer,
				0.0f
			);

		if (
			antiLagEvaporationStartParticleCount >
			0)
		{
			float progress =
				Mathf.Clamp(
					antiLagStateTimer /
					AntiLagEvaporationDuration,
					0.0f,
					1.0f
				);

			int targetRemoved =
				Mathf.FloorToInt(
					antiLagEvaporationStartParticleCount *
					progress
				);

			int particlesToRemove =
				targetRemoved -
				antiLagEvaporationParticlesRemoved;

			if (
				particlesToRemove > 0 &&
				particles.Count > 0)
			{
				particlesToRemove =
					System.Math.Min(
						particlesToRemove,
						particles.Count
					);

				for (
					int i = 0;
					i < particlesToRemove;
					i++)
				{
					RemoveAndRecordParticle(
						particles
					);
				}
			}
		}

		if (
			antiLagStateTimer >=
			AntiLagEvaporationDuration)
		{
			while (
				particles.Count > 0)
			{
				RemoveAndRecordParticle(
					particles
				);
			}

			GD.Print(
				"========================================"
			);

			GD.Print(
				"ANTI-LAG EVAPORATION COMPLETE"
			);

			GD.Print(
				"Evaporation Start Particles=" +
				antiLagEvaporationStartParticleCount
			);

			GD.Print(
				"Evaporated Particles=" +
				evaporatedParticlesThisCleanup
			);

			GD.Print(
				"Total Evaporated Particles=" +
				totalEvaporatedParticles
			);

			GD.Print(
				"Remaining Particles=" +
				particles.Count
			);

			GD.Print(
				"Evaporation Average Position=(" +
				EvaporationAverageX.ToString("F1") +
				"," +
				EvaporationAverageY.ToString("F1") +
				")"
			);

			GD.Print(
				"Evaporation Position Range=(" +
				EvaporationMinX.ToString("F1") +
				"," +
				EvaporationMinY.ToString("F1") +
				") -> (" +
				EvaporationMaxX.ToString("F1") +
				"," +
				EvaporationMaxY.ToString("F1") +
				")"
			);

			PrintEvaporationSpatialSummary();

			BeginAntiLagRecovery(
				rainSystem
			);
		}
	}

	/// <summary>
	/// Records the position of the particle that is about to be
	/// removed, then removes it.
	///
	/// ParticleData.RemoveParticle() swaps the final active particle
	/// into the removed slot, so the position MUST be captured first.
	/// </summary>
	private void RemoveAndRecordParticle(
		ParticleData particles)
	{
		int index =
			particles.Count - 1;

		if (index < 0)
		{
			return;
		}

		float x =
			particles.PosX[index];

		float y =
			particles.PosY[index];

		RecordEvaporatedParticlePosition(
			x,
			y
		);

		if (
			particles.RemoveParticle(index))
		{
			evaporatedParticlesThisCleanup++;

			totalEvaporatedParticles++;

			antiLagEvaporationParticlesRemoved++;
		}
	}

	// ============================================================
	// Spatial analysis
	// ============================================================

	private void ResetEvaporationSpatialStatistics()
	{
		System.Array.Clear(
			evaporationSpatialCounts,
			0,
			evaporationSpatialCounts.Length
		);

		evaporationPositionSumX =
			0.0;

		evaporationPositionSumY =
			0.0;

		evaporationMinX =
			0.0f;

		evaporationMaxX =
			0.0f;

		evaporationMinY =
			0.0f;

		evaporationMaxY =
			0.0f;

		hasEvaporationPositionData =
			false;
	}

	private void RecordEvaporatedParticlePosition(
		float x,
		float y)
	{
		evaporationPositionSumX +=
			x;

		evaporationPositionSumY +=
			y;

		if (!hasEvaporationPositionData)
		{
			evaporationMinX = x;
			evaporationMaxX = x;
			evaporationMinY = y;
			evaporationMaxY = y;

			hasEvaporationPositionData = true;
		}
		else
		{
			evaporationMinX =
				Mathf.Min(
					evaporationMinX,
					x
				);

			evaporationMaxX =
				Mathf.Max(
					evaporationMaxX,
					x
				);

			evaporationMinY =
				Mathf.Min(
					evaporationMinY,
					y
				);

			evaporationMaxY =
				Mathf.Max(
					evaporationMaxY,
					y
				);
		}

		float worldWidth =
			Mathf.Max(
				simulationWorldMaxX -
				simulationWorldMinX,
				0.0001f
			);

		float worldHeight =
			Mathf.Max(
				simulationWorldMaxY -
				simulationWorldMinY,
				0.0001f
			);

		float normalizedX =
			(x - simulationWorldMinX) /
			worldWidth;

		float normalizedY =
			(y - simulationWorldMinY) /
			worldHeight;

		int gridX =
			Mathf.FloorToInt(
				normalizedX *
				EvaporationGridWidth
			);

		int gridY =
			Mathf.FloorToInt(
				normalizedY *
				EvaporationGridHeight
			);

		gridX =
			Mathf.Clamp(
				gridX,
				0,
				EvaporationGridWidth - 1
			);

		gridY =
			Mathf.Clamp(
				gridY,
				0,
				EvaporationGridHeight - 1
			);

		int index =
			gridY *
			EvaporationGridWidth +
			gridX;

		evaporationSpatialCounts[index]++;
	}

	private void PrintEvaporationSpatialSummary()
	{
		if (
			EvaporationSpatialParticleCount <=
			0)
		{
			GD.Print(
				"EvaporationSpatial: no position data."
			);

			return;
		}

		GD.Print(
			"EvaporationSpatial=" +
			EvaporationGridWidth +
			"x" +
			EvaporationGridHeight +
			" grid"
		);

		GD.Print(
			"EvaporationSpatial: rows are Y, columns are X."
		);

		for (
			int y = EvaporationGridHeight - 1;
			y >= 0;
			y--)
		{
			string row = "";

			for (
				int x = 0;
				x < EvaporationGridWidth;
				x++)
			{
				int count =
					evaporationSpatialCounts[
						y *
						EvaporationGridWidth +
						x
					];

				row +=
					count.ToString("D4") +
					" ";
			}

			GD.Print(
				"  " +
				row
			);
		}

		int highestIndex = -1;
		int highestCount = 0;

		for (
			int i = 0;
			i < evaporationSpatialCounts.Length;
			i++)
		{
			if (
				evaporationSpatialCounts[i] >
				highestCount)
			{
				highestCount =
					evaporationSpatialCounts[i];

				highestIndex =
					i;
			}
		}

		if (highestIndex >= 0)
		{
			int cellX =
				highestIndex %
				EvaporationGridWidth;

			int cellY =
				highestIndex /
				EvaporationGridWidth;

			float cellWidth =
				(
					simulationWorldMaxX -
					simulationWorldMinX
				) /
				EvaporationGridWidth;

			float cellHeight =
				(
					simulationWorldMaxY -
					simulationWorldMinY
				) /
				EvaporationGridHeight;

			float cellMinX =
				simulationWorldMinX +
				cellX *
				cellWidth;

			float cellMaxX =
				cellMinX +
				cellWidth;

			float cellMinY =
				simulationWorldMinY +
				cellY *
				cellHeight;

			float cellMaxY =
				cellMinY +
				cellHeight;

			GD.Print(
				"EvaporationSpatial: Highest concentration cell=" +
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
	}

	// ============================================================
	// Recovery
	// ============================================================

	private void BeginAntiLagRecovery(
		RainSystem rainSystem)
	{
		antiLagState =
			AntiLagState.Recovering;

		antiLagStateTimer =
			0.0f;

		antiLagRecoveryTargetRainPercent =
			rainSystem.GetRandomRainPercent();

		rainSystem.CurrentRainPercent =
			0.0f;

		rainSystem.TargetRainPercent =
			antiLagRecoveryTargetRainPercent;

		rainSystem.RainPhaseTimer =
			AntiLagRecoveryDuration;

		GD.Print(
			"ANTI-LAG: Recovery started. Target rain=" +
			antiLagRecoveryTargetRainPercent.ToString("F0") +
			"% over " +
			AntiLagRecoveryDuration.ToString("F0") +
			"s."
		);
	}

	private void UpdateAntiLagRecovery(
		RainSystem rainSystem)
	{
		float progress =
			Mathf.Clamp(
				antiLagStateTimer /
				AntiLagRecoveryDuration,
				0.0f,
				1.0f
			);

		rainSystem.CurrentRainPercent =
			Mathf.Lerp(
				0.0f,
				antiLagRecoveryTargetRainPercent,
				progress
			);

		rainSystem.TargetRainPercent =
			antiLagRecoveryTargetRainPercent;

		rainSystem.RainPhaseTimer =
			Mathf.Max(
				AntiLagRecoveryDuration -
				antiLagStateTimer,
				0.0f
			);

		if (
			antiLagStateTimer >=
			AntiLagRecoveryDuration)
		{
			rainSystem.CurrentRainPercent =
				antiLagRecoveryTargetRainPercent;

			rainSystem.TargetRainPercent =
				antiLagRecoveryTargetRainPercent;

			antiLagState =
				AntiLagState.Normal;

			antiLagStateTimer =
				0.0f;

			rainSystem.RainPhaseTimer =
				rainSystem.GetRandomPhaseDuration();

			rainSystem.RainTransitionStartPercent =
				rainSystem.CurrentRainPercent;

			rainSystem.RainTransitionTimer =
				RainSystem.RainTransitionDuration;

			GD.Print(
				"ANTI-LAG CLEANUP COMPLETE. Returning to normal rain."
			);

			GD.Print(
				"========================================"
			);
		}
	}

	// ============================================================
	// Cleanup start
	// ============================================================

	private void StartAntiLagCleanup(
		double triggerProfilerFps,
		ParticleData particles,
		RainSystem rainSystem)
	{
		antiLagCleanupCount++;

		consecutiveLowProfilerResults = 0;

		antiLagState =
			AntiLagState.ReducingRain;

		antiLagStateTimer =
			0.0f;

		antiLagStateStartRainPercent =
			rainSystem.CurrentRainPercent;

		rainSystem.ResetSpawnAccumulator();

		evaporatedParticlesThisCleanup =
			0;

		antiLagEvaporationStartParticleCount =
			0;

		antiLagEvaporationParticlesRemoved =
			0;

		ResetEvaporationSpatialStatistics();

		GD.Print(
			"========================================"
		);

		GD.Print(
			"ANTI-LAG CLEANUP #" +
			antiLagCleanupCount +
			" STARTED"
		);

		GD.Print(
			"Trigger profiler Frame FPS=" +
			triggerProfilerFps.ToString("F1")
		);

		GD.Print(
			"Starting ActiveParticles=" +
			particles.Count
		);

		GD.Print(
			"Rain reducing from " +
			antiLagStateStartRainPercent.ToString("F1") +
			"% to 0% over " +
			AntiLagRainReductionDuration.ToString("F0") +
			"s."
		);
	}
}
