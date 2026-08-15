using System;
using System.Collections.Generic;
using System.Diagnostics;
using Godot;

public partial class FluidSimulator : Node2D
{
	private ParticleData particles;
	private SpatialHash hash;
	private PbfSolver solver;
	private FluidSimulationCoordinator simulationCoordinator;
	private FluidRenderer renderer;
	private DensityField densityField;
	private PixelOccupancyGrid pixelOccupancyGrid;
	private RainSystem rainSystem;
	private AntiLagController antiLagController;
	private WaterWheelManager waterWheelManager;
	private SimulationProfiler simulationProfiler;

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

	// ============================================================
	// Maximum number of particles
	// ============================================================

	private const int ParticleCount = 4000;

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
	// Despawn
	// ============================================================

	private const float DespawnLeftX =
		WorldMinX + 8.0f;

	private const float DespawnRightX =
		WorldMaxX - 8.0f;

	private const float DespawnBottomY =
		WorldMaxY - 8.0f;

	// ============================================================
	// Cached rain requests
	// ============================================================

	private readonly List<RainSystem.RainSpawnRequest> rainSpawnRequests =
		new List<RainSystem.RainSpawnRequest>();

	// ============================================================
	// Initialization
	// ============================================================

	public override void _Ready()
	{
		rainSystem =
			new RainSystem(
				this,
				WorldMinX,
				WorldMaxX
			);

		antiLagController =
			new AntiLagController();
			
			antiLagController.SetSimulationWorldBounds(
	WorldMinX,
	WorldMinY,
	WorldMaxX,
	WorldMaxY
);


		rainSystem.AntiLagController =
			antiLagController;

		rainSystem.InitializeDynamicRain();

		energySystem =
			new EnergySystem();

		SetupCurrentIndicator();

		SetupStatisticsHud();

		rainSystem.SetupRainHud();

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

		simulationCoordinator =
			new FluidSimulationCoordinator(
				solver
			);

		waterWheelManager =
			new WaterWheelManager(
				solver,
				energySystem,
				this
			);

		TileMapLayer environment =
			FindEnvironment();

		GameViewMapping mapping =
			CreateGameViewMapping();

		Func<Vector2, Vector2> toSimulationSpace =
			mapping.IsValid
				? new Func<Vector2, Vector2>(
					mapping.ToSimulationSpace
				)
				: null;

		waterWheelManager.CreateWaterWheelsFromEnvironment(
			environment,
			toSimulationSpace
		);

		waterWheelManager.InitializeWheelEnergyTracking();

		densityField =
			new DensityField(
				DensityWidth,
				DensityHeight,
				DensityCellSize
			);

		pixelOccupancyGrid =
			new PixelOccupancyGrid(
				PixelOccupancyGrid.PixelGridWidth,
				PixelOccupancyGrid.PixelGridHeight,
				WorldMinX,
				WorldMinY,
				PixelOccupancyGrid.MaxParticlesPerDensityCell
			);

		simulationProfiler =
			new SimulationProfiler();

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

		CenterSimulationCamera();

		BuildDensityField();

		renderer.Update(
			particles,
			densityField
		);

		UpdateStatisticsHud(
			0.0f
		);

		rainSystem.UpdateRainHud();

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
			rainSystem.CurrentRainPercent.ToString("F0") +
			"%, Wheels=" +
			waterWheelManager.WheelCount +
			", Energy=" +
			energySystem.Energy.ToString("F2") +
			", MaxParticlesPerDensityCell=" +
			PixelOccupancyGrid.MaxParticlesPerDensityCell +
			", DensityGrid=" +
			DensityWidth +
			"x" +
			DensityHeight +
			", WorldCenter=" +
			SimulationWorldCenter
		);

		CallDeferred(
			nameof(CenterSimulationCamera)
		);
	}

	// ============================================================
	// Center simulation camera
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

		camera.Enabled =
			true;

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

		energyGeneratedThisFrame =
			0.0;

		waterWheelManager.ResetFrameEnergy();

		antiLagController.UpdateAntiLagCleanup(
			dt,
			particles,
			rainSystem
		);

		Stopwatch spawnTimer =
			Stopwatch.StartNew();

		SpawnRainParticle(
			dt
		);

		rainSystem.UpdateRainHud();

		spawnTimer.Stop();

		waterWheelManager.StepAdditionalWheels(
			dt
		);

		Stopwatch pbfTimer =
			Stopwatch.StartNew();

		if (
			particles.Count > 0)
		{
			simulationCoordinator.Step(
				particles,
				dt
			);
		}
		else
		{
			waterWheelManager.StepPrimaryWheel(
				dt
			);
		}

		pbfTimer.Stop();

		bool currentGenerated =
			waterWheelManager.UpdateEnergyFromWheelRotation();

		energyGeneratedThisFrame =
			waterWheelManager.EnergyGeneratedThisFrame;

		UpdateCurrentIndicator(
			currentGenerated
		);

		RemoveParticlesAtOuterEdges();

		pixelOccupancyGrid.RebuildPixelOccupancy(
			particles
		);

		UpdateStatisticsHud(
			dt
		);

		waterWheelManager.UpdateWheelVisuals();

		Stopwatch densityTimer =
			Stopwatch.StartNew();

		BuildDensityField();

		densityTimer.Stop();

		Stopwatch rendererTimer =
			Stopwatch.StartNew();

		renderer.Update(
			particles,
			densityField
		);

		rendererTimer.Stop();

		physicsTimer.Stop();

		simulationProfiler.Accumulate(
			physicsTimer.Elapsed.TotalMilliseconds,
			Engine.GetFramesPerSecond(),
			spawnTimer.Elapsed.TotalMilliseconds,
			pbfTimer.Elapsed.TotalMilliseconds,
			densityTimer.Elapsed.TotalMilliseconds,
			rendererTimer.Elapsed.TotalMilliseconds,
			renderer.LastBuildPixelsMs,
			renderer.LastSurfaceGlowMs,
			renderer.LastFillBytesMs,
			renderer.LastTextureUploadMs
		);

		simulationProfiler.TryFlush(
			CreateSimulationProfilerReport(),
			fps =>
				antiLagController.EvaluateAntiLagProfilerResult(
					fps,
					particles,
					rainSystem
				)
		);
	}

	// ============================================================
	// Rain spawning
	// ============================================================

	private void SpawnRainParticle(
		float dt)
	{
		rainSystem.PrepareRainSpawnRequests(
			dt,
			particles.Count,
			particles.Capacity,
			pixelOccupancyGrid,
			rainSpawnRequests
		);

		for (
			int i = 0;
			i < rainSpawnRequests.Count;
			i++)
		{
			RainSystem.RainSpawnRequest request =
				rainSpawnRequests[i];

			if (
				!particles.AddParticle(
					request.X,
					request.Y,
					request.VelocityX,
					request.VelocityY
				))
			{
				rainSystem.RegisterCapacityRejection();
				break;
			}

			rainSystem.RegisterSuccessfulRainSpawn(
				request.PixelIndex,
				pixelOccupancyGrid
			);
		}
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
			rainSystem.CurrentRainPercent;

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
	public PbfSolver GetPbfSolver() =>
		solver;

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
	// Public wheel energy access
	// ============================================================

	public int WheelCount =>
		waterWheelManager != null
			? waterWheelManager.WheelCount
			: 0;

	public double GetWheelEnergyThisFrame(
		int wheelIndex)
	{
		if (
			waterWheelManager == null)
		{
			return 0.0;
		}

		return waterWheelManager.GetWheelEnergyThisFrame(
			wheelIndex
		);
	}

	public double GetWheelEnergyPerSecond(
		int wheelIndex,
		float delta)
	{
		if (
			waterWheelManager == null)
		{
			return 0.0;
		}

		return waterWheelManager.GetWheelEnergyPerSecond(
			wheelIndex,
			delta
		);
	}

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
		rainSystem != null
			? rainSystem.TotalRainSpawns
			: 0;

	public long TotalEvaporatedParticles =>
		antiLagController != null
			? antiLagController.TotalEvaporatedParticles
			: 0;

	public long EvaporatedParticlesThisCleanup =>
		antiLagController != null
			? antiLagController.EvaporatedParticlesThisCleanup
			: 0;

	public bool AntiLagCleanupActive =>
		antiLagController != null &&
		antiLagController.IsActive;

	public long RainRejectedByDensity =>
		rainSystem != null
			? rainSystem.RainRejectedByDensity
			: 0;

	public long RainRejectedByCapacity =>
		rainSystem != null
			? rainSystem.RainRejectedByCapacity
			: 0;

	public int MaxCellOccupancy =>
		pixelOccupancyGrid != null
			? pixelOccupancyGrid.MaxPixelOccupancy
			: 0;

	public int OccupiedDensityCells =>
		pixelOccupancyGrid != null
			? pixelOccupancyGrid.OccupiedPixelCount
			: 0;

	public int DensityCapacity =>
		PixelOccupancyGrid.MaxParticlesPerDensityCell;

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
	// Profiler report creation
	// ============================================================

	private SimulationProfiler.Report CreateSimulationProfilerReport()
{
	double[] wheelEnergyGeneratedThisFrame =
		waterWheelManager != null
			? waterWheelManager.CopyWheelEnergyGeneratedThisFrame()
			: Array.Empty<double>();

	int[] evaporationSpatialCounts =
		antiLagController != null
			? antiLagController.CopyEvaporationSpatialCounts()
			: Array.Empty<int>();

	return new SimulationProfiler.Report(
		WorldWidth,
		WorldHeight,
		WorldMinX,
		WorldMinY,
		WorldMaxX,
		WorldMaxY,
		SimulationWorldCenter,

		ActiveParticleCount,
		ParticleCapacity,

		EvaporatedParticlesThisCleanup,
		TotalEvaporatedParticles,
		antiLagController.AntiLagCleanupCount,

		evaporationSpatialCounts,
		AntiLagController.EvaporationGridWidth,
		AntiLagController.EvaporationGridHeight,
		antiLagController.EvaporationSpatialParticleCount,
		antiLagController.EvaporationAverageX,
		antiLagController.EvaporationAverageY,
		antiLagController.EvaporationMinX,
		antiLagController.EvaporationMaxX,
		antiLagController.EvaporationMinY,
		antiLagController.EvaporationMaxY,

		TotalRainSpawns,
		RainRejectedByDensity,
		RainRejectedByCapacity,

		DensityCapacity,
		MaxCellOccupancy,
		OccupiedDensityCells,

		PixelOccupancyGrid.PixelGridWidth,
		PixelOccupancyGrid.PixelGridHeight,

		DensityWidth,
		DensityHeight,
		DensityCellSize,

		energySystem,
		energyGeneratedThisFrame,
		lastPhysicsDelta,

		WheelCount,
		wheelEnergyGeneratedThisFrame
	);
	}
}
