using Godot;

/// <summary>
/// Owns the anti-lag cleanup state machine.
///
/// In addition to controlling the cleanup process, this class records
/// where evaporated particles were located in the simulation world.
///
/// The evaporation position statistics use an 8x8 spatial grid.
///
/// This class also performs a diagnostic-only particle bounds analysis
/// whenever a full profiler report is generated. The diagnostic does
/// not modify particle positions or particle lifetime in any way.
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
	// Particle bounds diagnostic
	// ============================================================

	private const float ParticleBoundsHistogramBinSize = 25.0f;

	private const int ParticleBoundsHistogramBinCount = 6;

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
	/// spatial analysis and particle bounds diagnostic.
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
		// ========================================================
		// Diagnostic only
		//
		// This is intentionally executed before the anti-lag state
		// machine logic. It only reads particle positions.
		// ========================================================

		PrintParticleBoundsDiagnostic(
			particles
		);

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
	// Particle bounds diagnostic
	// ============================================================

	/// <summary>
	/// Prints a diagnostic report describing where all active
	/// particles currently are relative to:
	///
	/// 1. The simulation world.
	/// 2. The currently visible GameView.
	///
	/// IMPORTANT:
	/// This method is diagnostic-only.
	/// It never changes particle positions, velocity, lifetime,
	/// particle count, or any simulation state.
	/// </summary>
	private void PrintParticleBoundsDiagnostic(
		ParticleData particles)
	{
		GD.Print(
			"========== PARTICLE BOUNDS DIAGNOSTIC =========="
		);

		int activeParticles =
			particles != null
				? particles.Count
				: 0;

		GD.Print(
			"Active particles: " +
			activeParticles
		);

		GD.Print("");

		// --------------------------------------------------------
		// Simulation world
		// --------------------------------------------------------

		GD.Print(
			"Simulation bounds:"
		);

		GD.Print(
			"  X = " +
			simulationWorldMinX.ToString("F1") +
			" -> " +
			simulationWorldMaxX.ToString("F1")
		);

		GD.Print(
			"  Y = " +
			simulationWorldMinY.ToString("F1") +
			" -> " +
			simulationWorldMaxY.ToString("F1")
		);

		GD.Print("");

		// --------------------------------------------------------
		// Visible GameView
		// --------------------------------------------------------

		PrintVisibleGameViewBounds();

		GD.Print("");

		// --------------------------------------------------------
		// No particles
		// --------------------------------------------------------

		if (
			particles == null ||
			particles.Count <= 0)
		{
			GD.Print(
				"Particle position bounds:"
			);

			GD.Print(
				"  No active particles."
			);

			GD.Print("");

			GD.Print(
				"==============================================="
			);

			return;
		}

		// --------------------------------------------------------
		// Particle position bounds
		// --------------------------------------------------------

		float minX =
			particles.PosX[0];

		float maxX =
			particles.PosX[0];

		float minY =
			particles.PosY[0];

		float maxY =
			particles.PosY[0];

		int leftCount = 0;
		int rightCount = 0;
		int topCount = 0;
		int bottomCount = 0;

		int minimumXParticleIndex = 0;

		float minimumXParticleX =
			particles.PosX[0];

		float minimumXParticleY =
			particles.PosY[0];

		// --------------------------------------------------------
		// Left out-of-bounds statistics
		// --------------------------------------------------------

		int leftOutOfBoundsCount = 0;

		double leftPositionSumX = 0.0;
		double leftPositionSumY = 0.0;

		float leftMinX = 0.0f;
		float leftMaxX = 0.0f;
		float leftMinY = 0.0f;
		float leftMaxY = 0.0f;

		bool hasLeftOutOfBoundsData = false;

		int[] leftHistogram =
			new int[
				ParticleBoundsHistogramBinCount
			];

		// --------------------------------------------------------
		// Scan all active particles
		// --------------------------------------------------------

		for (
			int i = 0;
			i < particles.Count;
			i++)
		{
			float x =
				particles.PosX[i];

			float y =
				particles.PosY[i];

			// Overall position bounds.
			minX =
				Mathf.Min(
					minX,
					x
				);

			maxX =
				Mathf.Max(
					maxX,
					x
				);

			minY =
				Mathf.Min(
					minY,
					y
				);

			maxY =
				Mathf.Max(
					maxY,
					y
				);

			// Minimum-X particle.
			if (
				x <
				minimumXParticleX)
			{
				minimumXParticleX =
					x;

				minimumXParticleY =
					y;

				minimumXParticleIndex =
					i;
			}

			// Simulation-world bounds.
			if (
				x <
				simulationWorldMinX)
			{
				leftCount++;

				leftOutOfBoundsCount++;

				leftPositionSumX +=
					x;

				leftPositionSumY +=
					y;

				if (
					!hasLeftOutOfBoundsData)
				{
					leftMinX = x;
					leftMaxX = x;
					leftMinY = y;
					leftMaxY = y;

					hasLeftOutOfBoundsData =
						true;
				}
				else
				{
					leftMinX =
						Mathf.Min(
							leftMinX,
							x
						);

					leftMaxX =
						Mathf.Max(
							leftMaxX,
							x
						);

					leftMinY =
						Mathf.Min(
							leftMinY,
							y
						);

					leftMaxY =
						Mathf.Max(
							leftMaxY,
							y
						);
				}

				// Histogram:
				//
				// Bin 0:
				//     -100 -> -125
				//
				// Bin 1:
				//     -125 -> -150
				//
				// etc.
				float distanceOutside =
					simulationWorldMinX - x;

				int histogramIndex =
					Mathf.FloorToInt(
						distanceOutside /
						ParticleBoundsHistogramBinSize
					);

				histogramIndex =
					Mathf.Clamp(
						histogramIndex,
						0,
						ParticleBoundsHistogramBinCount - 1
					);

				leftHistogram[
					histogramIndex
				]++;
			}

			if (
				x >
				simulationWorldMaxX)
			{
				rightCount++;
			}

			if (
				y <
				simulationWorldMinY)
			{
				topCount++;
			}

			if (
				y >
				simulationWorldMaxY)
			{
				bottomCount++;
			}
		}

		GD.Print(
			"Particle position bounds:"
		);

		GD.Print(
			"  X = " +
			minX.ToString("F1") +
			" -> " +
			maxX.ToString("F1")
		);

		GD.Print(
			"  Y = " +
			minY.ToString("F1") +
			" -> " +
			maxY.ToString("F1")
		);

		GD.Print("");

		// --------------------------------------------------------
		// Outside simulation world
		// --------------------------------------------------------

		GD.Print(
			"Outside simulation world:"
		);

		GD.Print(
			"  Left  (X < " +
			simulationWorldMinX.ToString("F1") +
			"): " +
			leftCount
		);

		GD.Print(
			"  Right (X > " +
			simulationWorldMaxX.ToString("F1") +
			"): " +
			rightCount
		);

		GD.Print(
			"  Top   (Y < " +
			simulationWorldMinY.ToString("F1") +
			"): " +
			topCount
		);

		GD.Print(
			"  Bottom(Y > " +
			simulationWorldMaxY.ToString("F1") +
			"): " +
			bottomCount
		);

		GD.Print("");

		// --------------------------------------------------------
		// Left detailed analysis
		// --------------------------------------------------------

		GD.Print(
			"LEFT OUT-OF-BOUNDS:"
		);

		double leftPercentage =
			activeParticles > 0
				? (
					leftOutOfBoundsCount *
					100.0 /
					activeParticles
				)
				: 0.0;

		GD.Print(
			"  Count: " +
			leftOutOfBoundsCount
		);

		GD.Print(
			"  Percentage: " +
			leftPercentage.ToString("F2") +
			"%"
		);

		if (
			hasLeftOutOfBoundsData)
		{
			GD.Print(
				"  X range: " +
				leftMinX.ToString("F1") +
				" -> " +
				leftMaxX.ToString("F1")
			);

			GD.Print(
				"  Y range: " +
				leftMinY.ToString("F1") +
				" -> " +
				leftMaxY.ToString("F1")
			);

			double averageLeftX =
				leftPositionSumX /
				leftOutOfBoundsCount;

			double averageLeftY =
				leftPositionSumY /
				leftOutOfBoundsCount;

			GD.Print(
				"  Average position: (" +
				averageLeftX.ToString("F1") +
				", " +
				averageLeftY.ToString("F1") +
				")"
			);
		}
		else
		{
			GD.Print(
				"  No particles are outside the left boundary."
			);
		}

		GD.Print("");

		// --------------------------------------------------------
		// Left-side distribution
		// --------------------------------------------------------

		GD.Print(
			"LEFT PARTICLE DISTRIBUTION"
		);

		GD.Print(
			"  X < " +
			simulationWorldMinX.ToString("F1") +
			" : " +
			leftOutOfBoundsCount
		);

		for (
			int i = 0;
			i < ParticleBoundsHistogramBinCount;
			i++)
		{
			float binMin =
				simulationWorldMinX -
				(
					(i + 1) *
					ParticleBoundsHistogramBinSize
				);

			float binMax =
				simulationWorldMinX -
				(
					i *
					ParticleBoundsHistogramBinSize
				);

			GD.Print(
				"  X " +
				binMin.ToString("F0") +
				".. " +
				binMax.ToString("F0") +
				" : " +
				leftHistogram[i]
			);
		}

		GD.Print("");

		// --------------------------------------------------------
		// Minimum-X particle
		// --------------------------------------------------------

		GD.Print(
			"MINIMUM-X PARTICLE:"
		);

		GD.Print(
			"  Index: " +
			minimumXParticleIndex
		);

		GD.Print(
			"  Position: (" +
			minimumXParticleX.ToString("F1") +
			", " +
			minimumXParticleY.ToString("F1") +
			")"
		);

		GD.Print("");

		GD.Print(
			"==============================================="
		);
	}

	/// <summary>
	/// Finds the currently active Camera2D and prints the world-space
	/// rectangle visible through its viewport.
	///
	/// Camera zoom is taken into account:
	///
	/// visible world width  = viewport width / zoom.X
	/// visible world height = viewport height / zoom.Y
	///
	/// This is diagnostic-only.
	/// </summary>
	private void PrintVisibleGameViewBounds()
	{
		Camera2D camera =
			FindActiveCamera();

		GD.Print(
			"Visible GameView bounds:"
		);

		if (
			camera == null)
		{
			GD.Print(
				"  Camera2D: NOT FOUND"
			);

			return;
		}

		Vector2 viewportSize =
			camera.GetViewport().GetVisibleRect().Size;

		Vector2 zoom =
			camera.Zoom;

		float zoomX =
			Mathf.Abs(
				zoom.X
			);

		float zoomY =
			Mathf.Abs(
				zoom.Y
			);

		if (
			zoomX < 0.0001f)
		{
			zoomX = 1.0f;
		}

		if (
			zoomY < 0.0001f)
		{
			zoomY = 1.0f;
		}

		float visibleWidth =
			viewportSize.X /
			zoomX;

		float visibleHeight =
			viewportSize.Y /
			zoomY;

		Vector2 cameraPosition =
			camera.GetGlobalPosition();

		float visibleMinX =
			cameraPosition.X -
			visibleWidth * 0.5f;

		float visibleMaxX =
			cameraPosition.X +
			visibleWidth * 0.5f;

		float visibleMinY =
			cameraPosition.Y -
			visibleHeight * 0.5f;

		float visibleMaxY =
			cameraPosition.Y +
			visibleHeight * 0.5f;

		GD.Print(
			"  Camera: " +
			camera.GetPath()
		);

		GD.Print(
			"  Camera position: (" +
			cameraPosition.X.ToString("F1") +
			", " +
			cameraPosition.Y.ToString("F1") +
			")"
		);

		GD.Print(
			"  Camera zoom: (" +
			zoom.X.ToString("F2") +
			", " +
			zoom.Y.ToString("F2") +
			")"
		);

		GD.Print(
			"  Viewport size: " +
			viewportSize.X.ToString("F1") +
			"x" +
			viewportSize.Y.ToString("F1")
		);

		GD.Print(
			"  X = " +
			visibleMinX.ToString("F1") +
			" -> " +
			visibleMaxX.ToString("F1")
		);

		GD.Print(
			"  Y = " +
			visibleMinY.ToString("F1") +
			" -> " +
			visibleMaxY.ToString("F1")
		);
	}

	/// <summary>
	/// Finds the active Camera2D in the current scene.
	/// </summary>
	private Camera2D FindActiveCamera()
	{
		SceneTree sceneTree =
			Engine.GetMainLoop() as SceneTree;

		if (
			sceneTree == null ||
			sceneTree.CurrentScene == null)
		{
			return null;
		}

		return FindActiveCameraRecursive(
			sceneTree.CurrentScene
		);
	}

	private Camera2D FindActiveCameraRecursive(
		Node node)
	{
		if (
			node is Camera2D camera &&
			camera.Enabled)
		{
			return camera;
		}

		foreach (
			Node child in
			node.GetChildren())
		{
			Camera2D result =
				FindActiveCameraRecursive(
					child
				);

			if (
				result != null)
			{
				return result;
			}
		}

		return null;
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
