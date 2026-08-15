using System;
using System.Collections.Generic;
using System.Diagnostics;
using Godot;

public partial class FluidSimulator : Node2D
{
	private ParticleData particles;
	private SpatialHash hash;
	private PbfSolver solver;
	private FluidRenderer renderer;
	private DensityField densityField;

	// ============================================================
	// Energy
	// ============================================================

	private EnergySystem energySystem;

	// ============================================================
	// Energy statistics graph
	// ============================================================

	private StatisticsGraph statisticsGraph;

	private double energyGeneratedThisFrame = 0.0;

	private float lastPhysicsDelta = 0.0f;

	// ============================================================
	// Rain / Energy statistical graph
	// ============================================================

	private const int RainEnergyGraphIntervalFrames = 600;

	private const int RainEnergyGraphInitialDelayFrames = 1200;

	private int rainEnergyGraphTotalFrames = 0;

	private int rainEnergyGraphWindowFrames = 0;

	private double rainEnergyRainSum = 0.0;

	private double rainEnergyEnergySum = 0.0;

	private double rainEnergyParticleSum = 0.0;

	// ============================================================
	// Current indicator
	// ============================================================

	private Sprite2D currentIndicator;
	private ShaderMaterial currentIndicatorMaterial;

	private const float CurrentGenerationThreshold = 0.0005f;

	// ============================================================
	// Maximum number of particles
	// ============================================================

	private const int ParticleCount = 4000;

	// ============================================================
	// Particle statistics
	// ============================================================

	private long totalRainSpawns = 0;

	private long rainRejectedByDensity = 0;

	private long rainRejectedByCapacity = 0;

	// ============================================================
	// Maximum number of particles per world pixel
	// ============================================================

	private const int MaxParticlesPerDensityCell = 1;

	private const int PixelGridWidth = 920;
	private const int PixelGridHeight = 1300;

	private int[] pixelOccupancy;

	private int[] pixelOccupancyStamp;

	private int pixelOccupancyGeneration = 0;

	private readonly List<int> occupiedPixelIndices =
		new List<int>();

	private int maxPixelOccupancy = 0;

	private int occupiedPixelCount = 0;

	// ============================================================
	// Density rendering grid
	// ============================================================

	private const int DensityWidth = 920;
	private const int DensityHeight = 1300;
	private const float DensityCellSize = 4.0f;

	// ============================================================
	// Simulation world
	// ============================================================

	private const float WorldWidth = 920.0f;
	private const float WorldHeight = 1300.0f;

	private const float WorldMinX = -100.0f;
	private const float WorldMaxX = 820.0f;

	private const float WorldMinY = -50.0f;
	private const float WorldMaxY = 1250.0f;

	// ============================================================
	// Simulation world center
	// ============================================================

	// Exact center of:
	//
	// X = -100 .. 920
	// Y = -50  .. 1300
	//
	// Therefore:
	//
	// Center X = 460
	// Center Y = 650
	//
	
	private const float WorldCenterX =
		(WorldMinX + WorldMaxX) * 0.5f;

	private const float WorldCenterY =
		(WorldMinY + WorldMaxY) * 0.5f;

	private static readonly Vector2 SimulationWorldCenter =
		new Vector2(
			WorldCenterX,
			WorldCenterY
		);

	// ============================================================
	// Dynamic Rain
	// ============================================================

	private const float RainAmount = 120.0f;

	private const int RainMinimumPercent = 0;
	private const int RainMaximumPercent = 100;
	private const int RainPercentStep = 10;

	private const float RainMinimumDuration = 13.0f;
	private const float RainMaximumDuration = 29.0f;

	private float currentRainPercent;
	private float targetRainPercent;
	private float rainTransitionStartPercent;
	private float rainTransitionTimer;
	private float rainPhaseTimer;

	private const float RainTransitionDuration = 10.0f;

	// ============================================================
	// Anti-lag cleanup
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

	private AntiLagState antiLagState =
		AntiLagState.Normal;

	private float antiLagStateTimer = 0.0f;

	private float antiLagStateStartRainPercent = 0.0f;

	private float antiLagRecoveryTargetRainPercent = 0.0f;

	private int consecutiveLowProfilerResults = 0;

	private long totalEvaporatedParticles = 0;

	private long evaporatedParticlesThisCleanup = 0;

	private int antiLagCleanupCount = 0;

	// ============================================================
	// Anti-lag evaporation tracking
	// ============================================================

	private int antiLagEvaporationStartParticleCount = 0;

	private int antiLagEvaporationParticlesRemoved = 0;

	// ============================================================
	// Rain HUD
	// ============================================================

	private CanvasLayer rainHudLayer;

	private Label rainHudLabel;

	private const float RainSpawnY =
		WorldMinY + 10.0f;

	private const float RainVelocityX =
		0.0f;

	private const float RainVelocityY =
		200.0f;

	private float rainSpawnAccumulator =
		0.0f;

	private readonly RandomNumberGenerator rainRandom =
		new RandomNumberGenerator();

	// ============================================================
	// Despawn
	// ============================================================

	private const float DespawnLeftX =
		WorldMinX + 8.0f;

	private const float DespawnRightX =
		WorldMaxX - 8.0f;

	private const float DespawnBottomY =
		WorldMaxY - 8.0f;

	// ============================================================
	// Water wheels
	// ============================================================

	private const int MaxWheelCount = 6;

	private const int WheelTileAtlasX = 7;

	private const int WheelTileAtlasY = 6;

	private const float WheelOuterRadius = 45.0f;

	private const float WheelInnerRadius = 12.5f;

	private const int WheelBladeCount = 8;

	private const float WheelBladeWidth = 7.5f;

	private readonly List<FluidWheelState>
		wheelStates =
		new List<FluidWheelState>();

	private readonly List<WaterWheelVisual>
		wheelVisuals =
		new List<WaterWheelVisual>();

	// ============================================================
	// Wheel energy tracking
	// ============================================================

	private float[] previousWheelAngles =
		Array.Empty<float>();

	private double[] wheelEnergyGeneratedThisFrame =
		Array.Empty<double>();

	// ============================================================
	// Full-frame profiler
	// ============================================================

	private const int FullProfilerInterval = 600;

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
	// Initialization
	// ============================================================

	public override void _Ready()
	{
		rainRandom.Randomize();

		InitializeDynamicRain();

		energySystem =
			new EnergySystem();

		SetupCurrentIndicator();

		SetupStatisticsHud();

		SetupRainHud();

		particles =
			new ParticleData(
				ParticleCount
			);

		hash =
			new SpatialHash(
				ParticleCount
			);

		solver =
			new PbfSolver(
				hash
			);

		CreateWaterWheelsFromEnvironment();

		InitializeWheelEnergyTracking();

		densityField =
			new DensityField(
				DensityWidth,
				DensityHeight,
				DensityCellSize
			);

		InitializePixelOccupancy();

		renderer =
			new FluidRenderer();

		AddChild(
			renderer
		);

		renderer.Initialize(
			DensityWidth,
			DensityHeight,
			DensityCellSize
		);

		// --------------------------------------------------------
		// Explicitly center the simulation camera.
		// --------------------------------------------------------

		CenterSimulationCamera();

		BuildDensityField();

		renderer.Update(
			particles,
			densityField
		);

		UpdateStatisticsHud(
			0.0f
		);

		UpdateRainHud();

		GD.Print(
			"Fluid initialized. " +
			"World=" +
			WorldWidth +
			"x" +
			WorldHeight +
			" @ (" +
			WorldMinX +
			"," +
			WorldMinY +
			"), ParticleCapacity=" +
			ParticleCount +
			", ActiveParticles=" +
			particles.Count +
			", Rain=" +
			currentRainPercent.ToString("F0") +
			"%, Wheels=" +
			wheelStates.Count +
			", Energy=" +
			energySystem.Energy.ToString("F2") +
			", MaxParticlesPerDensityCell=" +
			MaxParticlesPerDensityCell +
			", DensityGrid=" +
			DensityWidth +
			"x" +
			DensityHeight +
			", WorldCenter=" +
			SimulationWorldCenter
		);

		// --------------------------------------------------------
		// Run one more time after the scene has finished its
		// initialization. This prevents another Camera2D or
		// viewport initialization step from overwriting the
		// desired center.
		// --------------------------------------------------------

		CallDeferred(
			nameof(CenterSimulationCamera)
		);
	}

	// ============================================================
	// Center simulation camera
	// ============================================================
	//
	// The simulation world is explicitly centered at:
	//
	//     (460, 650)
	//
	// because:
	//
	//     X = -100 .. 920
	//     Y = -50  .. 1300
	//
	// This method does not modify the simulation itself.
	// It only establishes the Camera2D position.
	//
	// ============================================================

	private void CenterSimulationCamera()
	{
		GameViewMapping mapping =
			CreateGameViewMapping();

		if (
			!mapping.IsValid)
		{
			GD.PushWarning(
				"FluidSimulator: Could not center simulation camera. " +
				"GameView, SimulationViewport, or Camera2D is missing."
			);

			return;
		}

		Camera2D camera =
			mapping.Camera;

		// Make absolutely sure this camera is the active camera
		// for the SimulationViewport.
		camera.Enabled =
			true;

		// The camera position is in simulation/world coordinates.
		camera.Position =
			SimulationWorldCenter;

		GD.Print(
			"SIMULATION CAMERA CENTERED -> " +
			SimulationWorldCenter
		);

		GD.Print(
			"Simulation world bounds: " +
			WorldMinX +
			".." +
			WorldMaxX +
			" x " +
			WorldMinY +
			".." +
			WorldMaxY
		);

		GD.Print(
			"Simulation world center: " +
			SimulationWorldCenter
		);

		GD.Print(
			"Camera position after centering: " +
			camera.Position
		);

		GD.Print(
			"Simulation viewport size: " +
			mapping.SimulationViewport.Size
		);
	}

	// ============================================================
	// Physics
	// ============================================================

	public override void _PhysicsProcess(
		double delta)
	{
		Stopwatch physicsTimer =
			Stopwatch.StartNew();

		float dt =
			(float)delta;

		lastPhysicsDelta =
			dt;

		// --------------------------------------------------------
		// Reset per-frame energy counters
		// --------------------------------------------------------

		energyGeneratedThisFrame =
			0.0;

		if (
			wheelEnergyGeneratedThisFrame.Length !=
			wheelStates.Count)
		{
			wheelEnergyGeneratedThisFrame =
				new double[
					wheelStates.Count
				];
		}

		for (
			int i = 0;
			i < wheelEnergyGeneratedThisFrame.Length;
			i++)
		{
			wheelEnergyGeneratedThisFrame[i] =
				0.0;
		}

		// --------------------------------------------------------
		// Anti-lag cleanup state
		// --------------------------------------------------------

		UpdateAntiLagCleanup(
			dt
		);

		// --------------------------------------------------------
		// Rain
		// --------------------------------------------------------

		Stopwatch spawnTimer =
			Stopwatch.StartNew();

		SpawnRainParticle(
			dt
		);

		UpdateRainHud();

		spawnTimer.Stop();

		fullSpawnTime +=
			spawnTimer.Elapsed.TotalMilliseconds;

		// --------------------------------------------------------
		// Additional wheel physics
		// --------------------------------------------------------

		StepAdditionalWheels(
			dt
		);

		// --------------------------------------------------------
		// PBF
		// --------------------------------------------------------

		Stopwatch pbfTimer =
			Stopwatch.StartNew();

		if (
			particles.Count > 0)
		{
			solver.Solve(
				particles,
				dt
			);
		}
		else
		{
			if (
				wheelStates.Count > 0)
			{
				wheelStates[0].Step(
					dt
				);
			}
		}

		pbfTimer.Stop();

		fullPbfTime +=
			pbfTimer.Elapsed.TotalMilliseconds;

		// --------------------------------------------------------
		// Energy + current indicator
		// --------------------------------------------------------

		bool currentGenerated =
			UpdateEnergyFromWheelRotation();

		UpdateCurrentIndicator(
			currentGenerated
		);

		// --------------------------------------------------------
		// Despawn particles
		// --------------------------------------------------------

		RemoveParticlesAtOuterEdges();

		// --------------------------------------------------------
		// Rebuild pixel occupancy
		// --------------------------------------------------------

		RebuildPixelOccupancy();

		// --------------------------------------------------------
		// Statistics graph
		// --------------------------------------------------------

		UpdateStatisticsHud(
			dt
		);

		// --------------------------------------------------------
		// Wheel visuals
		// --------------------------------------------------------

		UpdateWheelVisuals();

		// --------------------------------------------------------
		// Density
		// --------------------------------------------------------

		Stopwatch densityTimer =
			Stopwatch.StartNew();

		BuildDensityField();

		densityTimer.Stop();

		fullDensityTime +=
			densityTimer.Elapsed.TotalMilliseconds;

		// --------------------------------------------------------
		// Renderer
		// --------------------------------------------------------

		Stopwatch rendererTimer =
			Stopwatch.StartNew();

		renderer.Update(
			particles,
			densityField
		);

		rendererTimer.Stop();

		fullRendererTime +=
			rendererTimer.Elapsed.TotalMilliseconds;

		fullRendererBuildPixelsTime +=
			renderer.LastBuildPixelsMs;

		fullRendererSurfaceGlowTime +=
			renderer.LastSurfaceGlowMs;

		fullRendererFillBytesTime +=
			renderer.LastFillBytesMs;

		fullRendererTextureUploadTime +=
			renderer.LastTextureUploadMs;

		// --------------------------------------------------------
		// Profiler
		// --------------------------------------------------------

		physicsTimer.Stop();

		fullPhysicsTime +=
			physicsTimer.Elapsed.TotalMilliseconds;

		fullRenderedFpsSum +=
			Engine.GetFramesPerSecond();

		fullProfilerFrames++;

		if (
			fullProfilerFrames >=
			FullProfilerInterval)
		{
			PrintFullProfiler();

			ResetFullProfiler();
		}
	}

	// ============================================================
	// Pixel occupancy initialization
	// ============================================================

	private void InitializePixelOccupancy()
	{
		int pixelCount =
			PixelGridWidth *
			PixelGridHeight;

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
	// Convert world position to occupancy pixel
	// ============================================================

	private bool TryGetPixelIndex(
		float x,
		float y,
		out int pixelIndex)
	{
		pixelIndex = -1;

		int pixelX =
			Mathf.FloorToInt(
				x -
				WorldMinX
			);

		int pixelY =
			Mathf.FloorToInt(
				y -
				WorldMinY
			);

		if (
			pixelX < 0 ||
			pixelX >= PixelGridWidth ||
			pixelY < 0 ||
			pixelY >= PixelGridHeight)
		{
			return false;
		}

		pixelIndex =
			pixelY *
			PixelGridWidth +
			pixelX;

		return true;
	}

	// ============================================================
	// Get current occupancy for one pixel
	// ============================================================

	private int GetPixelOccupancy(
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

	// ============================================================
	// Check pixel density
	// ============================================================

	private bool CanSpawnAtPixel(
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
			MaxParticlesPerDensityCell;
	}

	// ============================================================
	// Register newly spawned particle
	// ============================================================

	private void RegisterParticlePixel(
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

	// ============================================================
	// Rebuild pixel occupancy
	// ============================================================

	private void RebuildPixelOccupancy()
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

	// ============================================================
	// Rain HUD setup
	// ============================================================

	private void SetupRainHud()
	{
		rainHudLayer =
			new CanvasLayer();

		rainHudLayer.Layer =
			20;

		rainHudLabel =
			new Label();

		rainHudLabel.Position =
			new Vector2(
				20.0f,
				20.0f
			);

		rainHudLabel.AddThemeFontSizeOverride(
			"font_size",
			22
		);

		rainHudLabel.Text =
			"RAIN  --%\nNEXT CHANGE --s";

		rainHudLayer.AddChild(
			rainHudLabel
		);

		AddChild(
			rainHudLayer
		);
	}

	// ============================================================
	// Rain HUD update
	// ============================================================

	private void UpdateRainHud()
	{
		if (
			rainHudLabel == null)
		{
			return;
		}

		float remaining =
			Mathf.Max(
				rainPhaseTimer,
				0.0f
			);

		float currentRainAmount =
			RainAmount *
			(currentRainPercent / 50.0f);

		rainHudLabel.Text =
			"RAIN  " +
			currentRainPercent.ToString("F0") +
			"%\nRATE  " +
			currentRainAmount.ToString("F0") +
			" / sec\nNEXT CHANGE  " +
			remaining.ToString("F0") +
			"s";
	}

	// ============================================================
	// Statistics graph setup
	// ============================================================

	private void SetupStatisticsHud()
	{
		Node mainScene =
			GetTree().CurrentScene;

		if (
			mainScene == null)
		{
			GD.PushWarning(
				"FluidSimulator: CurrentScene could not be found. " +
				"Statistics graph cannot be found."
			);

			return;
		}

		statisticsGraph =
			FindNodeOfType<StatisticsGraph>(
				mainScene
			);

		if (
			statisticsGraph == null)
		{
			GD.PushWarning(
				"FluidSimulator: StatisticsGraph could not be found " +
				"in the scene. Make sure the StatisticsGraph node " +
				"is attached to the Control under BottomUI."
			);

			return;
		}

		GD.Print(
			"FluidSimulator: Using existing StatisticsGraph: " +
			statisticsGraph.GetPath()
		);
	}

	// ============================================================
	// Statistics graph update
	// ============================================================

	private void UpdateStatisticsHud(
		float delta)
	{
		if (
			statisticsGraph == null)
		{
			return;
		}

		double energyPerSecond =
			delta > 0.000001f
				? energyGeneratedThisFrame /
					delta
				: 0.0;

		statisticsGraph.AddSample(
			ActiveParticleCount,
			energyPerSecond,
			(float)Engine.GetFramesPerSecond(),
			delta
		);

		rainEnergyGraphTotalFrames++;

		if (
			rainEnergyGraphTotalFrames <=
			RainEnergyGraphInitialDelayFrames -
			RainEnergyGraphIntervalFrames)
		{
			return;
		}

		rainEnergyGraphWindowFrames++;

		rainEnergyRainSum +=
			currentRainPercent;

		rainEnergyEnergySum +=
			energyGeneratedThisFrame;

		rainEnergyParticleSum +=
			ActiveParticleCount;

		if (
			rainEnergyGraphWindowFrames <
			RainEnergyGraphIntervalFrames)
		{
			return;
		}

		double averageRain =
			rainEnergyRainSum /
			RainEnergyGraphIntervalFrames;

		double averageEnergy =
			rainEnergyEnergySum /
			RainEnergyGraphIntervalFrames;

		double averageParticles =
			rainEnergyParticleSum /
			RainEnergyGraphIntervalFrames;

		statisticsGraph.AddRainEnergySample(
			(float)averageRain,
			(float)averageEnergy,
			(float)averageParticles
		);

		GD.Print(
			"RAIN/ENERGY GRAPH POINT: " +
			"Frame=" +
			rainEnergyGraphTotalFrames +
			", AverageRain=" +
			averageRain.ToString("F2") +
			"%, AverageEnergy=" +
			averageEnergy.ToString("F4") +
			", AverageParticles=" +
			averageParticles.ToString("F1")
		);

		rainEnergyGraphWindowFrames =
			0;

		rainEnergyRainSum =
			0.0;

		rainEnergyEnergySum =
			0.0;

		rainEnergyParticleSum =
			0.0;
	}

	// ============================================================
	// Initialize wheel energy tracking
	// ============================================================

	private void InitializeWheelEnergyTracking()
	{
		previousWheelAngles =
			new float[
				wheelStates.Count
			];

		wheelEnergyGeneratedThisFrame =
			new double[
				wheelStates.Count
			];

		for (
			int i = 0;
			i < wheelStates.Count;
			i++)
		{
			previousWheelAngles[i] =
				wheelStates[i].Angle;

			wheelEnergyGeneratedThisFrame[i] =
				0.0;
		}
	}

	// ============================================================
	// Energy from wheel rotation
	// ============================================================

	private bool UpdateEnergyFromWheelRotation()
	{
		int wheelCount =
			wheelStates.Count;

		if (
			wheelCount <= 0)
		{
			return false;
		}

		if (
			previousWheelAngles.Length !=
			wheelCount)
		{
			InitializeWheelEnergyTracking();

			return false;
		}

		if (
			wheelEnergyGeneratedThisFrame.Length !=
			wheelCount)
		{
			wheelEnergyGeneratedThisFrame =
				new double[
					wheelCount
				];
		}

		bool currentGenerated =
			false;

		for (
			int i = 0;
			i < wheelCount;
			i++)
		{
			float currentAngle =
				wheelStates[i].Angle;

			float previousAngle =
				previousWheelAngles[i];

			float angularMovement =
				Mathf.Abs(
					Mathf.AngleDifference(
						previousAngle,
						currentAngle
					)
				);

			if (
				angularMovement >
				0.0f)
			{
				double frameEnergy =
					angularMovement *
					energySystem.EnergyPerRadian;

				energySystem.AddEnergy(
					frameEnergy
				);

				energyGeneratedThisFrame +=
					frameEnergy;

				wheelEnergyGeneratedThisFrame[i] +=
					frameEnergy;
			}

			if (
				angularMovement >
				CurrentGenerationThreshold)
			{
				currentGenerated =
					true;
			}

			previousWheelAngles[i] =
				currentAngle;
		}

		return currentGenerated;
	}

	// ============================================================
	// Public wheel energy access
	// ============================================================

	public int WheelCount =>
		wheelStates.Count;

	public double GetWheelEnergyThisFrame(
		int wheelIndex)
	{
		if (
			wheelIndex < 0 ||
			wheelIndex >=
			wheelEnergyGeneratedThisFrame.Length)
		{
			return 0.0;
		}

		return wheelEnergyGeneratedThisFrame[
			wheelIndex
		];
	}

	public double GetWheelEnergyPerSecond(
		int wheelIndex,
		float delta)
	{
		if (
			delta <= 0.000001f)
		{
			return 0.0;
		}

		return
			GetWheelEnergyThisFrame(
				wheelIndex
			) /
			delta;
	}

	// ============================================================
	// Current indicator setup
	// ============================================================

	private void SetupCurrentIndicator()
	{
		currentIndicator =
			FindNodeByName<Sprite2D>(
				GetTree().Root,
				"CurrentIndicator"
			);

		if (
			currentIndicator == null)
		{
			GD.PushWarning(
				"FluidSimulator: CurrentIndicator Sprite2D " +
				"could not be found."
			);

			return;
		}

		Shader shader =
			new Shader();

		shader.Code = @"
shader_type canvas_item;

uniform float grayscale_amount = 1.0;

void fragment()
{
	vec4 tex = texture(TEXTURE, UV);

	float gray =
		dot(
			tex.rgb,
			vec3(
				0.299,
				0.587,
				0.114
			)
		);

	vec3 result =
		mix(
			tex.rgb,
			vec3(gray),
			grayscale_amount
		);

	COLOR =
		vec4(
			result,
			tex.a
		);
}
";

		currentIndicatorMaterial =
			new ShaderMaterial();

		currentIndicatorMaterial.Shader =
			shader;

		currentIndicator.Material =
			currentIndicatorMaterial;

		SetCurrentIndicatorLit(
			false
		);
	}

	// ============================================================
	// Current indicator state
	// ============================================================

	private void UpdateCurrentIndicator(
		bool currentGenerated)
	{
		if (
			currentIndicatorMaterial == null)
		{
			return;
		}

		SetCurrentIndicatorLit(
			currentGenerated
		);
	}

	private void SetCurrentIndicatorLit(
		bool lit)
	{
		if (
			currentIndicatorMaterial == null)
		{
			return;
		}

		currentIndicatorMaterial.SetShaderParameter(
			"grayscale_amount",
			lit
				? 0.0f
				: 1.0f
		);
	}

	// ============================================================
	// Find node by name
	// ============================================================

	private static T FindNodeByName<T>(
		Node node,
		string nodeName)
		where T : Node
	{
		if (
			node is T typedNode &&
			typedNode.Name == nodeName)
		{
			return typedNode;
		}

		foreach (
			Node child in
			node.GetChildren())
		{
			T result =
				FindNodeByName<T>(
					child,
					nodeName
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
	// Find node by type
	// ============================================================

	private static T FindNodeOfType<T>(
		Node node)
		where T : Node
	{
		if (
			node is T typedNode)
		{
			return typedNode;
		}

		foreach (
			Node child in
			node.GetChildren())
		{
			T result =
				FindNodeOfType<T>(
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
	// Solver access (direct typed API — replaces reflection)
	// ============================================================

	/// <summary>
	/// Returns the active <see cref="PbfSolver"/>, or <c>null</c> if it has
	/// not been created yet (call after <c>_Ready</c> has completed).
	/// Prefer this over reflection-based lookups.
	/// </summary>
	public PbfSolver GetPbfSolver() => solver;

	// ============================================================
	// Public energy access
	// ============================================================

	public EnergySystem Energy =>
		energySystem;

	public double CurrentEnergy =>
		energySystem != null
			? energySystem.Energy
			: 0.0;

	public double TotalEnergyGenerated =>
		energySystem != null
			? energySystem.TotalGenerated
			: 0.0;

	// ============================================================
	// Public particle statistics
	// ============================================================

	public int ActiveParticleCount =>
		particles != null
			? particles.Count
			: 0;

	public int ParticleCapacity =>
		ParticleCount;

	public long TotalRainSpawns =>
		totalRainSpawns;

	public long TotalEvaporatedParticles =>
		totalEvaporatedParticles;

	public long EvaporatedParticlesThisCleanup =>
		evaporatedParticlesThisCleanup;

	public bool AntiLagCleanupActive =>
		antiLagState != AntiLagState.Normal;

	public long RainRejectedByDensity =>
		rainRejectedByDensity;

	public int MaxCellOccupancy =>
		maxPixelOccupancy;

	public int OccupiedDensityCells =>
		occupiedPixelCount;

	public int DensityCapacity =>
		MaxParticlesPerDensityCell;

	// ============================================================
	// Find Environment
	// ============================================================

	private TileMapLayer FindEnvironment()
	{
		TileMapLayer environment =
			GetNodeOrNull<TileMapLayer>(
				"../Environment"
			);

		if (
			environment != null)
		{
			return environment;
		}

		return FindEnvironmentRecursive(
			GetTree().Root
		);
	}

	private static TileMapLayer FindEnvironmentRecursive(
		Node node)
	{
		if (
			node is TileMapLayer &&
			node.Name == "Environment")
		{
			return (TileMapLayer)node;
		}

		foreach (
			Node child in
			node.GetChildren())
		{
			TileMapLayer result =
				FindEnvironmentRecursive(
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
	// Create wheels from marker tiles
	// ============================================================

	private void CreateWaterWheelsFromEnvironment()
	{
		TileMapLayer environment =
			FindEnvironment();

		if (
			environment == null)
		{
			GD.PushWarning(
				"FluidSimulator: Environment TileMapLayer " +
				"could not be found. No wheels created."
			);

			return;
		}

		GameViewMapping mapping =
			CreateGameViewMapping();

		if (
			!mapping.IsValid)
		{
			GD.PushWarning(
				"FluidSimulator: Could not establish " +
				"viewport mapping. No wheels created."
			);

			return;
		}

		IEnumerable<Vector2I> usedCells =
			environment.GetUsedCells();

		foreach (
			Vector2I cell in
			usedCells)
		{
			if (
				wheelStates.Count >=
				MaxWheelCount)
			{
				break;
			}

			int sourceId =
				environment.GetCellSourceId(
					cell
				);

			if (
				sourceId < 0)
			{
				continue;
			}

			Vector2I atlasCoords =
				environment.GetCellAtlasCoords(
					cell
				);

			if (
				atlasCoords.X !=
				WheelTileAtlasX ||
				atlasCoords.Y !=
				WheelTileAtlasY)
			{
				continue;
			}

			Vector2 tileCenterLocal =
				environment.MapToLocal(
					cell
				);

			Vector2 tileCenterGlobal =
				environment.ToGlobal(
					tileCenterLocal
				);

			Vector2 simulationPosition =
				mapping.ToSimulationSpace(
					tileCenterGlobal
				);

			CreateWaterWheel(
				simulationPosition
			);

			GD.Print(
				"Water wheel placed on Environment tile " +
				cell +
				" atlas " +
				atlasCoords +
				" -> simulation " +
				simulationPosition
			);
		}

		GD.Print(
			"Water wheels created from marker tiles: " +
			wheelStates.Count +
			"/" +
			MaxWheelCount
		);
	}

	// ============================================================
	// Viewport mapping
	// ============================================================

	private struct GameViewMapping
	{
		public SubViewportContainer GameView;

		public SubViewport SimulationViewport;

		public Camera2D Camera;

		public bool IsValid =>
			GameView != null &&
			SimulationViewport != null &&
			Camera != null;

		public Vector2 ToSimulationSpace(
			Vector2 globalPosition)
		{
			Vector2 viewportPoint =
				globalPosition -
				GameView.GlobalPosition;

			Vector2 viewportSize =
				new Vector2(
					SimulationViewport.Size.X,
					SimulationViewport.Size.Y
				);

			Vector2 screenCenter =
				viewportSize *
				0.5f;

			Vector2 cameraCenter =
				Camera.GetScreenCenterPosition();

			return
				cameraCenter +
				(
					viewportPoint -
					screenCenter
				);
		}
	}

	private GameViewMapping CreateGameViewMapping()
	{
		GameViewMapping mapping =
			new GameViewMapping();

		mapping.GameView =
			GetNodeOrNull<SubViewportContainer>(
				"../GameView"
			);

		if (
			mapping.GameView == null)
		{
			mapping.GameView =
				FindNodeOfType<SubViewportContainer>(
					GetTree().Root
				);
		}

		if (
			mapping.GameView != null)
		{
			mapping.SimulationViewport =
				mapping.GameView.GetNodeOrNull<SubViewport>(
					"SimulationViewport"
				);

			if (
				mapping.SimulationViewport == null)
			{
				foreach (
					Node child in
					mapping.GameView.GetChildren())
				{
					if (
						child is SubViewport)
					{
						mapping.SimulationViewport =
							(SubViewport)child;

						break;
					}
				}
			}
		}

		if (
			mapping.SimulationViewport != null)
		{
			mapping.Camera =
				mapping.SimulationViewport.GetNodeOrNull<Camera2D>(
					"Camera2D"
				);

			if (
				mapping.Camera == null)
			{
				mapping.Camera =
					FindNodeOfType<Camera2D>(
						mapping.SimulationViewport
					);
			}
		}

		return mapping;
	}

	// ============================================================
	// Create one wheel
	// ============================================================

	private void CreateWaterWheel(
		Vector2 center)
	{
		FluidWheelState wheelState;

		if (
			wheelStates.Count == 0)
		{
			wheelState =
				solver.CreateWheel(
					center
				);
		}
		else
		{
			wheelState =
				new FluidWheelState(
					center
				);
		}

		wheelStates.Add(
			wheelState
		);

		for (
			int i = 0;
			i < WheelBladeCount;
			i++)
		{
			float angle =
				Mathf.Tau *
				i /
				WheelBladeCount;

			Vector2 direction =
				new Vector2(
					Mathf.Cos(angle),
					Mathf.Sin(angle)
				);

			Vector2 tangent =
				new Vector2(
					-direction.Y,
					direction.X
				);

			Vector2 innerCenter =
				direction *
				WheelInnerRadius;

			Vector2 outerCenter =
				direction *
				WheelOuterRadius;

			Vector2[] blade =
			{
				innerCenter +
				tangent *
				WheelBladeWidth,

				outerCenter +
				tangent *
				WheelBladeWidth,

				outerCenter -
				tangent *
				WheelBladeWidth,

				innerCenter -
				tangent *
				WheelBladeWidth
			};

			FluidPolygonCollider collider =
				new FluidPolygonCollider(
					blade
				);

			collider.ConfigureAsWheel(
				wheelState
			);

			solver.AddPolygonCollider(
				collider
			);
		}

		const int hubSegments = 16;

		Vector2[] hub =
			new Vector2[
				hubSegments
			];

		for (
			int i = 0;
			i < hubSegments;
			i++)
		{
			float angle =
				Mathf.Tau *
				i /
				hubSegments;

			hub[i] =
				new Vector2(
					Mathf.Cos(angle),
					Mathf.Sin(angle)
				) *
				WheelInnerRadius;
		}

		FluidPolygonCollider hubCollider =
			new FluidPolygonCollider(
				hub
			);

		solver.AddPolygonCollider(
			hubCollider
		);

		WaterWheelVisual visual =
			new WaterWheelVisual();

		visual.Position =
			center;

		visual.OuterRadius =
			WheelOuterRadius;

		visual.InnerRadius =
			WheelInnerRadius;

		visual.BladeCount =
			WheelBladeCount;

		visual.BladeWidth =
			WheelBladeWidth;

		AddChild(
			visual
		);

		visual.SetWheelAngle(
			wheelState.Angle
		);

		wheelVisuals.Add(
			visual
		);
	}

	// ============================================================
	// Additional wheel physics
	// ============================================================

	private void StepAdditionalWheels(
		float dt)
	{
		for (
			int i = 1;
			i < wheelStates.Count;
			i++)
		{
			wheelStates[i].Step(
				dt
			);
		}
	}

	// ============================================================
	// Update wheel visuals
	// ============================================================

	private void UpdateWheelVisuals()
	{
		int count =
			Math.Min(
				wheelStates.Count,
				wheelVisuals.Count
			);

		for (
			int i = 0;
			i < count;
			i++)
		{
			wheelVisuals[i].SetWheelAngle(
				wheelStates[i].Angle
			);
		}
	}

	// ============================================================
	// Anti-lag profiler evaluation
	// ============================================================

	private void EvaluateAntiLagProfilerResult(
		double profilerFps)
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
					profilerFps
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

	// ============================================================
	// Start anti-lag cleanup
	// ============================================================

	private void StartAntiLagCleanup(
		double triggerProfilerFps)
	{
		antiLagCleanupCount++;

		consecutiveLowProfilerResults = 0;

		antiLagState =
			AntiLagState.ReducingRain;

		antiLagStateTimer =
			0.0f;

		antiLagStateStartRainPercent =
			currentRainPercent;

		rainSpawnAccumulator =
			0.0f;

		evaporatedParticlesThisCleanup =
			0;

		antiLagEvaporationStartParticleCount =
			0;

		antiLagEvaporationParticlesRemoved =
			0;

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

	// ============================================================
	// Update anti-lag cleanup
	// ============================================================

	private void UpdateAntiLagCleanup(
		float dt)
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
				UpdateAntiLagRainReduction();
				break;

			case AntiLagState.Draining:
				currentRainPercent = 0.0f;

				targetRainPercent = 0.0f;

				rainPhaseTimer =
					Mathf.Max(
						AntiLagDrainDuration -
						antiLagStateTimer,
						0.0f
					);

				if (
					antiLagStateTimer >=
					AntiLagDrainDuration)
				{
					BeginAntiLagEvaporation();
				}

				break;

			case AntiLagState.Evaporating:
				UpdateAntiLagEvaporation(
					dt
				);
				break;

			case AntiLagState.Recovering:
				UpdateAntiLagRecovery();
				break;
		}
	}

	// ============================================================
	// Anti-lag rain reduction
	// ============================================================

	private void UpdateAntiLagRainReduction()
	{
		float progress =
			Mathf.Clamp(
				antiLagStateTimer /
				AntiLagRainReductionDuration,
				0.0f,
				1.0f
			);

		currentRainPercent =
			Mathf.Lerp(
				antiLagStateStartRainPercent,
				0.0f,
				progress
			);

		targetRainPercent =
			0.0f;

		rainPhaseTimer =
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

			currentRainPercent =
				0.0f;

			GD.Print(
				"ANTI-LAG: Rain reached 0%. Starting 20s natural drain."
			);
		}
	}

	// ============================================================
	// Begin anti-lag evaporation
	// ============================================================

	private void BeginAntiLagEvaporation()
	{
		antiLagState =
			AntiLagState.Evaporating;

		antiLagStateTimer =
			0.0f;

		antiLagEvaporationStartParticleCount =
			particles.Count;

		antiLagEvaporationParticlesRemoved =
			0;

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

	// ============================================================
	// Anti-lag evaporation
	// ============================================================

	private void UpdateAntiLagEvaporation(
		float dt)
	{
		currentRainPercent =
			0.0f;

		targetRainPercent =
			0.0f;

		rainPhaseTimer =
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
					Math.Min(
						particlesToRemove,
						particles.Count
					);

				for (
					int i = 0;
					i < particlesToRemove;
					i++)
				{
					particles.RemoveParticle(
						particles.Count - 1
					);

					evaporatedParticlesThisCleanup++;

					totalEvaporatedParticles++;

					antiLagEvaporationParticlesRemoved++;
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
				particles.RemoveParticle(
					particles.Count - 1
				);

				evaporatedParticlesThisCleanup++;

				totalEvaporatedParticles++;
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

			BeginAntiLagRecovery();
		}
	}

	// ============================================================
	// Begin anti-lag recovery
	// ============================================================

	private void BeginAntiLagRecovery()
	{
		antiLagState =
			AntiLagState.Recovering;

		antiLagStateTimer =
			0.0f;

		antiLagRecoveryTargetRainPercent =
			GetRandomRainPercent();

		currentRainPercent =
			0.0f;

		targetRainPercent =
			antiLagRecoveryTargetRainPercent;

		rainPhaseTimer =
			AntiLagRecoveryDuration;

		GD.Print(
			"ANTI-LAG: Recovery started. Target rain=" +
			antiLagRecoveryTargetRainPercent.ToString("F0") +
			"% over " +
			AntiLagRecoveryDuration.ToString("F0") +
			"s."
		);
	}

	// ============================================================
	// Anti-lag recovery
	// ============================================================

	private void UpdateAntiLagRecovery()
	{
		float progress =
			Mathf.Clamp(
				antiLagStateTimer /
				AntiLagRecoveryDuration,
				0.0f,
				1.0f
			);

		currentRainPercent =
			Mathf.Lerp(
				0.0f,
				antiLagRecoveryTargetRainPercent,
				progress
			);

		targetRainPercent =
			antiLagRecoveryTargetRainPercent;

		rainPhaseTimer =
			Mathf.Max(
				AntiLagRecoveryDuration -
				antiLagStateTimer,
				0.0f
			);

		if (
			antiLagStateTimer >=
			AntiLagRecoveryDuration)
		{
			currentRainPercent =
				antiLagRecoveryTargetRainPercent;

			targetRainPercent =
				antiLagRecoveryTargetRainPercent;

			antiLagState =
				AntiLagState.Normal;

			antiLagStateTimer =
				0.0f;

			rainPhaseTimer =
				rainRandom.RandfRange(
					RainMinimumDuration,
					RainMaximumDuration
				);

			rainTransitionStartPercent =
				currentRainPercent;

			rainTransitionTimer =
				RainTransitionDuration;

			GD.Print(
				"ANTI-LAG CLEANUP COMPLETE. Returning to normal rain."
			);

			GD.Print(
				"========================================"
			);
		}
	}

	// ============================================================
	// Random rain percentage
	// ============================================================

	private int GetRandomRainPercent()
	{
		int stepCount =
			(
				RainMaximumPercent -
				RainMinimumPercent
			) /
			RainPercentStep +
			1;

		int randomStep =
			rainRandom.RandiRange(
				0,
				stepCount - 1
			);

		return
			RainMinimumPercent +
			randomStep *
			RainPercentStep;
	}

	// ============================================================
	// Rain initialization
	// ============================================================

	private void InitializeDynamicRain()
	{
		int stepCount =
			(
				RainMaximumPercent -
				RainMinimumPercent
			) /
			RainPercentStep +
			1;

		int randomStep =
			rainRandom.RandiRange(
				0,
				stepCount - 1
			);

		float initialRainPercent =
			RainMinimumPercent +
			randomStep *
			RainPercentStep;

		currentRainPercent =
			initialRainPercent;

		targetRainPercent =
			initialRainPercent;

		rainTransitionStartPercent =
			initialRainPercent;

		rainTransitionTimer =
			RainTransitionDuration;

		rainPhaseTimer =
			rainRandom.RandfRange(
				RainMinimumDuration,
				RainMaximumDuration
			);

		GD.Print(
			"RAIN CHANGE -> " +
			currentRainPercent.ToString("F0") +
			"% for " +
			rainPhaseTimer.ToString("F1") +
			"s"
		);
	}

	// ============================================================
	// Select new rain phase
	// ============================================================

	private void SelectNewRainPhase()
	{
		int stepCount =
			(
				RainMaximumPercent -
				RainMinimumPercent
			) /
			RainPercentStep +
			1;

		int randomStep =
			rainRandom.RandiRange(
				0,
				stepCount - 1
			);

		rainTransitionStartPercent =
			currentRainPercent;

		targetRainPercent =
			RainMinimumPercent +
			randomStep *
			RainPercentStep;

		rainTransitionTimer =
			0.0f;

		rainPhaseTimer =
			rainRandom.RandfRange(
				RainMinimumDuration,
				RainMaximumDuration
			);

		GD.Print(
			"RAIN CHANGE -> " +
			targetRainPercent.ToString("F0") +
			"% for " +
			rainPhaseTimer.ToString("F1") +
			"s (transition " +
			RainTransitionDuration.ToString("F1") +
			"s)"
		);
	}

	// ============================================================
	// Dynamic rain update
	// ============================================================

	private void UpdateDynamicRain(
		float dt)
	{
		if (
			antiLagState !=
			AntiLagState.Normal)
		{
			return;
		}

		rainPhaseTimer -=
			dt;

		if (
			rainPhaseTimer <=
			0.0f)
		{
			SelectNewRainPhase();
		}

		if (
			rainTransitionTimer <
			RainTransitionDuration)
		{
			rainTransitionTimer +=
				dt;

			if (
				rainTransitionTimer >
				RainTransitionDuration)
			{
				rainTransitionTimer =
					RainTransitionDuration;
			}

			float transitionProgress =
				rainTransitionTimer /
				RainTransitionDuration;

			currentRainPercent =
				Mathf.Lerp(
					rainTransitionStartPercent,
					targetRainPercent,
					transitionProgress
				);
		}
		else
		{
			currentRainPercent =
				targetRainPercent;
		}
	}

	// ============================================================
	// Spawn rain particles
	// ============================================================

	private void SpawnRainParticle(
		float dt)
	{
		UpdateDynamicRain(
			dt
		);

		if (
			antiLagState ==
			AntiLagState.Draining ||
			antiLagState ==
			AntiLagState.Evaporating)
		{
			return;
		}

		float currentRainAmount =
			RainAmount *
			(currentRainPercent / 50.0f);

		if (
			particles.Count >=
			particles.Capacity)
		{
			rainRejectedByCapacity++;

			return;
		}

		rainSpawnAccumulator +=
			currentRainAmount *
			dt;

		int spawnCount =
			(int)rainSpawnAccumulator;

		if (
			spawnCount <= 0)
		{
			return;
		}

		rainSpawnAccumulator -=
			spawnCount;

		for (
			int i = 0;
			i < spawnCount;
			i++)
		{
			if (
				particles.Count >=
				particles.Capacity)
			{
				rainRejectedByCapacity++;

				break;
			}

			float x =
				rainRandom.RandfRange(
					WorldMinX,
					WorldMaxX
				);

			float y =
				RainSpawnY;

			int pixelIndex;

			if (
				!CanSpawnAtPixel(
					x,
					y,
					out pixelIndex
				))
			{
				rainRejectedByDensity++;

				continue;
			}

			particles.AddParticle(
				x,
				y,
				RainVelocityX,
				RainVelocityY
			);

			totalRainSpawns++;

			if (
				pixelIndex >= 0)
			{
				RegisterParticlePixel(
					pixelIndex
				);
			}
		}
	}

	// ============================================================
	// Despawn particles at outer edges
	// ============================================================

	private void RemoveParticlesAtOuterEdges()
	{
		int i = 0;

		while (
			i < particles.Count)
		{
			float x =
				particles.PosX[i];

			float y =
				particles.PosY[i];

			bool outside =
				x <= DespawnLeftX ||
				x >= DespawnRightX ||
				y >= DespawnBottomY;

			if (
				outside)
			{
				particles.RemoveParticle(
					i
				);

				continue;
			}

			i++;
		}
	}

	// ============================================================
	// Density field
	// ============================================================

	private void BuildDensityField()
	{
		densityField.Clear();

		for (
			int i = 0;
			i < particles.Count;
			i++)
		{
			densityField.AddParticle(
				particles.PosX[i],
				particles.PosY[i]
			);
		}
	}

	// ============================================================
	// Full profiler output
	// ============================================================

	private void PrintFullProfiler()
	{
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

		double physicsFps =
			physicsMs > 0.001
				? 1000.0 /
					physicsMs
				: 0.0;

		EvaluateAntiLagProfilerResult(
			fps
		);

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
			WorldWidth +
			"x" +
			WorldHeight +
			" (" +
			WorldMinX +
			"," +
			WorldMinY +
			") -> (" +
			WorldMaxX +
			"," +
			WorldMaxY +
			")"
		);

		GD.Print(
			"SimulationWorldCenter=" +
			SimulationWorldCenter
		);

		GD.Print(
			"ActiveParticles=" +
			particles.Count +
			"/" +
			particles.Capacity
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
			"AntiLagCleanupCount=" +
			antiLagCleanupCount
		);

		GD.Print(
			"ParticleCapacity=" +
			ParticleCount
		);

		GD.Print(
			"TotalRainSpawns=" +
			totalRainSpawns
		);

		GD.Print(
			"RainRejectedByDensity=" +
			rainRejectedByDensity
		);

		GD.Print(
			"RainRejectedByCapacity=" +
			rainRejectedByCapacity
		);

		GD.Print(
			"DensityCapacity=" +
			MaxParticlesPerDensityCell +
			" particles/pixel"
		);

		GD.Print(
			"MaxCellOccupancy=" +
			maxPixelOccupancy
		);

		GD.Print(
			"OccupiedDensityCells=" +
			occupiedPixelCount +
			"/" +
			(
				PixelGridWidth *
				PixelGridHeight
			)
		);

		GD.Print(
			"DensityGrid=" +
			DensityWidth +
			"x" +
			DensityHeight +
			" cells @ " +
			DensityCellSize +
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

		// --------------------------------------------------------
		// Energy statistics
		// --------------------------------------------------------

		GD.Print(
			"Energy=" +
			energySystem.Energy.ToString("F2")
		);

		GD.Print(
			"EnergyThisFrame=" +
			energyGeneratedThisFrame.ToString("F2")
		);

		double energyPerSecond =
			lastPhysicsDelta > 0.000001f
				? energyGeneratedThisFrame /
					lastPhysicsDelta
				: 0.0;

		GD.Print(
			"EnergyPerSecond=" +
			energyPerSecond.ToString("F2")
		);

		GD.Print(
			"TotalEnergyGenerated=" +
			energySystem.TotalGenerated.ToString("F2")
		);

		// --------------------------------------------------------
		// Individual wheel energy
		// --------------------------------------------------------

		for (
			int i = 0;
			i < wheelStates.Count;
			i++)
		{
			double wheelEnergy =
				i <
				wheelEnergyGeneratedThisFrame.Length
					? wheelEnergyGeneratedThisFrame[i]
					: 0.0;

			double wheelEnergyPerSecond =
				lastPhysicsDelta > 0.000001f
					? wheelEnergy /
						lastPhysicsDelta
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
	}

	// ============================================================
	// Reset profiler
	// ============================================================

	private void ResetFullProfiler()
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
