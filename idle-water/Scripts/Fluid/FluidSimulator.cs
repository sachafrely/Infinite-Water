
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
	// Maximum number of particles
	// ============================================================

	private const int ParticleCount = 4000;

	// ============================================================
	// Density rendering grid
	// ============================================================

	private const int DensityWidth = 360;
	private const int DensityHeight = 180;
	private const float DensityCellSize = 4.0f;

	// ============================================================
	// Simulation world
	// ============================================================

	private const float WorldWidth = 920.0f;
	private const float WorldHeight = 1020.0f;

	private const float WorldMinX = 260.0f;
	private const float WorldMaxX = 1180.0f;

	private const float WorldMinY = -200.0f;
	private const float WorldMaxY = 820.0f;

	// ============================================================
	// Rain
	// ============================================================

	private const float RainAmount = 2.25f;

	private const float RainSpawnY =
		WorldMinY;

	private const float RainVelocityX =
		0.0f;

	private const float RainVelocityY =
		250.0f;

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
	//
	// Wheels are placed automatically on every Environment tile
	// whose atlas coordinate is (4,7).
	//
	// Maximum = 4 wheels.
	// ============================================================

	private const int MaxWheelCount = 4;

	private const int WheelTileAtlasX = 4;
	private const int WheelTileAtlasY = 7;

	private const float WheelOuterRadius = 50.0f;
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

	// ============================================================
	// Full-frame profiler
	// ============================================================

	private const int FullProfilerInterval = 60;

	private int fullProfilerFrames = 0;

	private double fullPhysicsTime = 0.0;

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

		energySystem =
			new EnergySystem();

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

		BuildDensityField();

		renderer.Update(
			particles,
			densityField
		);

		GD.Print(
			"Fluid initialized. " +
			"World=" +
			WorldWidth +
			"x" +
			WorldHeight +
			", Particles=" +
			ParticleCount +
			", Rain=" +
			(RainAmount * 100.0f)
				.ToString("F0") +
			"%, Wheels=" +
			wheelStates.Count +
			", Energy=" +
			energySystem.Energy.ToString("F2")
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

		// --------------------------------------------------------
		// Rain
		// --------------------------------------------------------

		Stopwatch spawnTimer =
			Stopwatch.StartNew();

		SpawnRainParticle();

		spawnTimer.Stop();

		fullSpawnTime +=
			spawnTimer.Elapsed.TotalMilliseconds;

		// --------------------------------------------------------
		// Additional wheel states
		//
		// PbfSolver owns and advances the first wheel through
		// CreateWheel(). Additional wheels are advanced here.
		// --------------------------------------------------------

		StepAdditionalWheels(dt);

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
			// PbfSolver normally advances its own wheel only when
			// Solve() is called, so keep the first wheel moving even
			// when there are temporarily no particles.

			if (wheelStates.Count > 0)
			{
				wheelStates[0].Step(dt);
			}
		}

		pbfTimer.Stop();

		fullPbfTime +=
			pbfTimer.Elapsed.TotalMilliseconds;

		// --------------------------------------------------------
		// Energy
		//
		// Measure how far every wheel actually rotated during
		// this physics frame.
		//
		// Absolute rotation is used so both directions produce
		// energy.
		// --------------------------------------------------------

		UpdateEnergyFromWheelRotation();

		// --------------------------------------------------------
		// Despawn
		// --------------------------------------------------------

		RecycleParticlesAtOuterEdges();

		// --------------------------------------------------------
		// Update wheel visuals
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
	// Initialize wheel energy tracking
	// ============================================================

	private void InitializeWheelEnergyTracking()
	{
		previousWheelAngles =
			new float[
				wheelStates.Count
			];

		for (
			int i = 0;
			i < wheelStates.Count;
			i++)
		{
			previousWheelAngles[i] =
				wheelStates[i].Angle;
		}
	}

	// ============================================================
	// Energy from wheel rotation
	// ============================================================

	private void UpdateEnergyFromWheelRotation()
	{
		int wheelCount =
			wheelStates.Count;

		if (
			wheelCount <= 0)
		{
			return;
		}

		if (
			previousWheelAngles.Length !=
			wheelCount)
		{
			InitializeWheelEnergyTracking();

			return;
		}

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
				angularMovement > 0.0f)
			{
				energySystem.AddEnergy(
					angularMovement *
					energySystem.EnergyPerRadian
				);
			}

			previousWheelAngles[i] =
				currentAngle;
		}
	}

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
	// Find Environment
	// ============================================================

	private TileMapLayer FindEnvironment()
	{
		TileMapLayer environment =
			GetNodeOrNull<TileMapLayer>(
				"../Environment"
			);

		if (environment != null)
			return environment;

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
			Node child in node.GetChildren())
		{
			TileMapLayer result =
				FindEnvironmentRecursive(
					child
				);

			if (result != null)
				return result;
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

		if (environment == null)
		{
			GD.PushWarning(
				"FluidSimulator: Environment TileMapLayer " +
				"could not be found. No wheels created."
			);

			return;
		}

		GameViewMapping mapping =
			CreateGameViewMapping();

		if (!mapping.IsValid)
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
			Vector2I cell in usedCells)
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

			if (sourceId < 0)
				continue;

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

		if (mapping.GameView == null)
		{
			mapping.GameView =
				FindNodeOfType<SubViewportContainer>(
					GetTree().Root
				);
		}

		if (mapping.GameView != null)
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

		if (mapping.SimulationViewport != null)
		{
			mapping.Camera =
				mapping.SimulationViewport.GetNodeOrNull<Camera2D>(
					"Camera2D"
				);

			if (mapping.Camera == null)
			{
				mapping.Camera =
					FindNodeOfType<Camera2D>(
						mapping.SimulationViewport
					);
			}
		}

		return mapping;
	}

	private static T FindNodeOfType<T>(
		Node node)
		where T : Node
	{
		if (node is T)
			return (T)node;

		foreach (
			Node child in node.GetChildren())
		{
			T result =
				FindNodeOfType<T>(
					child
				);

			if (result != null)
				return result;
		}

		return null;
	}

	// ============================================================
	// Create one wheel
	// ============================================================

	private void CreateWaterWheel(
		Vector2 center)
	{
		FluidWheelState wheelState;

		// --------------------------------------------------------
		// The first wheel is owned by PbfSolver.
		//
		// This preserves the existing working wheel physics.
		// Additional wheels are manually stepped by this simulator.
		// --------------------------------------------------------

		if (wheelStates.Count == 0)
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

		// --------------------------------------------------------
		// Wheel blades
		// --------------------------------------------------------

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

		// --------------------------------------------------------
		// Central hub
		// --------------------------------------------------------

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

		// --------------------------------------------------------
		// Visual
		// --------------------------------------------------------

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
			wheelStates[i].Step(dt);
		}
	}

	// ============================================================
	// Update wheel visuals
	// ============================================================

	private void UpdateWheelVisuals()
	{
		int count =
			Mathf.Min(
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
	// Rain emitter
	// ============================================================

	private void SpawnRainParticle()
	{
		if (
			RainAmount <= 0.0f ||
			particles.Count >=
			particles.Capacity)
		{
			return;
		}

		rainSpawnAccumulator +=
			RainAmount;

		while (
			rainSpawnAccumulator >= 1.0f &&
			particles.Count <
			particles.Capacity)
		{
			SpawnSingleRainParticle();

			rainSpawnAccumulator -=
				1.0f;
		}
	}

	private void SpawnSingleRainParticle()
	{
		float x =
			rainRandom.RandfRange(
				WorldMinX,
				WorldMaxX
			);

		particles.AddParticle(
			x,
			RainSpawnY,
			RainVelocityX,
			RainVelocityY
		);
	}

	// ============================================================
	// Despawn / recycle
	// ============================================================

	private void RecycleParticlesAtOuterEdges()
	{
		int count =
			particles.Count;

		for (
			int i = 0;
			i < count;
			i++)
		{
			float x =
				particles.PosX[i];

			float y =
				particles.PosY[i];

			bool reachedLeft =
				x <= DespawnLeftX;

			bool reachedRight =
				x >= DespawnRightX;

			bool reachedBottom =
				y >= DespawnBottomY;

			if (
				reachedLeft ||
				reachedRight ||
				reachedBottom)
			{
				RecycleParticle(i);
			}
		}
	}

	private void RecycleParticle(
		int index)
	{
		float x =
			rainRandom.RandfRange(
				WorldMinX,
				WorldMaxX
			);

		float y =
			RainSpawnY;

		particles.PosX[index] =
			x;

		particles.PosY[index] =
			y;

		particles.PredX[index] =
			x;

		particles.PredY[index] =
			y;

		particles.VelX[index] =
			RainVelocityX;

		particles.VelY[index] =
			RainVelocityY;
	}

	// ============================================================
	// Density
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
	// Full profiler
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

		if (otherMs < 0.0)
			otherMs = 0.0;

		double fps =
			Engine.GetFramesPerSecond();

		double physicsFps =
			physicsMs > 0.001
				? 1000.0 / physicsMs
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
			"Particles=" +
			particles.Count
		);

		GD.Print(
			"Wheels=" +
			wheelStates.Count
		);

		GD.Print(
			"Energy=" +
			energySystem.Energy.ToString("F2")
		);

		GD.Print(
			"TotalEnergyGenerated=" +
			energySystem.TotalGenerated.ToString("F2")
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
