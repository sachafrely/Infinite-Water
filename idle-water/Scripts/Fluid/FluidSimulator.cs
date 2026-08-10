
using System.Diagnostics;
using Godot;

public partial class FluidSimulator : Node2D
{
	private ParticleData particles;
	private SpatialHash hash;
	private PbfSolver solver;
	private FluidRenderer renderer;
	private DensityField densityField;

	// ------------------------------------------------------------
	// Maximum number of particles
	// ------------------------------------------------------------

	private const int ParticleCount = 4000;

	// ------------------------------------------------------------
	// Density rendering grid
	// ------------------------------------------------------------

	private const int DensityWidth = 180;
	private const int DensityHeight = 320;
	private const float DensityCellSize = 4.0f;

	// ------------------------------------------------------------
	// Simulation world
	// ------------------------------------------------------------

	private const float WorldWidth = 720.0f;
	private const float WorldHeight = 1280.0f;

	// ------------------------------------------------------------
	// Walls
	// ------------------------------------------------------------

	private const float LeftBound = 20.0f;
	private const float RightBound = 700.0f;
	private const float TopBound = 20.0f;
	private const float BottomBound = 1260.0f;

	// ------------------------------------------------------------
	// Fluid parameters
	// ------------------------------------------------------------

	private const float ParticleRadius = 4.0f;

	// ------------------------------------------------------------
	// Emitter
	// ------------------------------------------------------------

	private const float EmitterCenterX = 40.0f;
	private const float EmitterY = 40.0f;

	private const float EmitterSpacing = 8.0f;

	private static readonly float[] EmitterOffsets =
	{
		-EmitterSpacing,
		0.0f,
		EmitterSpacing
	};

	private int emitterIndex = 0;

	// ------------------------------------------------------------
	// Water wheel
	// ------------------------------------------------------------

	private WaterWheelVisual waterWheel;

	private const float WheelCenterX = 350.0f;
	private const float WheelCenterY = 600.0f;

	private const float WheelOuterRadius = 115.0f;
	private const float WheelInnerRadius = 55.0f;

	private const int WheelBladeCount = 10;

	// ------------------------------------------------------------
	// Pipes
	// ------------------------------------------------------------

	private WaterPipeVisual emitterPipe;
	private WaterPipeVisual despawnerPipe;

	private const float EmitterPipeX = 40.0f;
	private const float EmitterPipeY = 8.0f;

	private const float DespawnerPipeX = 350.0f;
	private const float DespawnerPipeY = 1260.0f;

	// ------------------------------------------------------------
	// Full-frame profiler
	// ------------------------------------------------------------

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

	// ------------------------------------------------------------
	// Initialization
	// ------------------------------------------------------------

	public override void _Ready()
	{
		particles =
			new ParticleData(
				ParticleCount
			);

		hash =
			new SpatialHash(
				ParticleCount
			);

		solver =
			new PbfSolver(hash);

		// --------------------------------------------------------
		// Create water wheel
		// --------------------------------------------------------

		CreateWaterWheel();

		// --------------------------------------------------------
		// Create pipes
		// --------------------------------------------------------

		CreatePipes();

		// --------------------------------------------------------
		// Density field
		// --------------------------------------------------------

		densityField =
			new DensityField(
				DensityWidth,
				DensityHeight,
				DensityCellSize
			);

		// --------------------------------------------------------
		// Renderer
		// --------------------------------------------------------

		renderer =
			new FluidRenderer();

		AddChild(renderer);

		renderer.Initialize(
			DensityWidth,
			DensityHeight,
			DensityCellSize
		);

		// --------------------------------------------------------
		// Start with zero particles
		// --------------------------------------------------------

		BuildDensityField();

		renderer.Update(
			particles,
			densityField
		);

		GD.Print(
			"Fluid initialized with " +
			particles.Count +
			" particles."
		);
	}

	// ------------------------------------------------------------
	// Physics
	// ------------------------------------------------------------

	public override void _PhysicsProcess(
		double delta)
	{
		Stopwatch physicsTimer =
			Stopwatch.StartNew();

		float dt =
			(float)delta;

		// --------------------------------------------------------
		// Spawn
		// --------------------------------------------------------

		Stopwatch spawnTimer =
			Stopwatch.StartNew();

		SpawnParticle();

		spawnTimer.Stop();

		fullSpawnTime +=
			spawnTimer.Elapsed.TotalMilliseconds;

		// --------------------------------------------------------
		// PBF
		// --------------------------------------------------------

		Stopwatch pbfTimer =
			Stopwatch.StartNew();

		if (particles.Count > 0)
		{
			solver.Solve(
				particles,
				dt
			);
		}

		pbfTimer.Stop();

		fullPbfTime +=
			pbfTimer.Elapsed.TotalMilliseconds;

		// --------------------------------------------------------
		// Density field
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
		// Stop entire PhysicsProcess timer
		// --------------------------------------------------------

		physicsTimer.Stop();

		fullPhysicsTime +=
			physicsTimer.Elapsed.TotalMilliseconds;

		fullProfilerFrames++;

		// --------------------------------------------------------
		// Print profiler
		// --------------------------------------------------------

		if (
			fullProfilerFrames >=
			FullProfilerInterval)
		{
			PrintFullProfiler();

			ResetFullProfiler();
		}
	}

	// ------------------------------------------------------------
	// Water wheel creation
	// ------------------------------------------------------------

	private void CreateWaterWheel()
	{
		Vector2 center =
			new Vector2(
				WheelCenterX,
				WheelCenterY
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

			float bladeWidth =
				18.0f;

			Vector2 innerCenter =
				center +
				direction *
				WheelInnerRadius;

			Vector2 outerCenter =
				center +
				direction *
				WheelOuterRadius;

			Vector2[] blade =
			{
				innerCenter + tangent * bladeWidth,
				outerCenter + tangent * bladeWidth,
				outerCenter - tangent * bladeWidth,
				innerCenter - tangent * bladeWidth
			};

			FluidPolygonCollider collider =
				new FluidPolygonCollider(
					blade
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
			new Vector2[hubSegments];

		const float hubRadius =
			55.0f;

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
				center +
				new Vector2(
					Mathf.Cos(angle),
					Mathf.Sin(angle)
				) *
				hubRadius;
		}

		FluidPolygonCollider hubCollider =
			new FluidPolygonCollider(
				hub
			);

		solver.AddPolygonCollider(
			hubCollider
		);

		// --------------------------------------------------------
		// Visual wheel
		// --------------------------------------------------------

		waterWheel =
			new WaterWheelVisual();

		waterWheel.Position =
			center;

		waterWheel.OuterRadius =
			WheelOuterRadius;

		waterWheel.InnerRadius =
			WheelInnerRadius;

		waterWheel.BladeCount =
			WheelBladeCount;

		AddChild(
			waterWheel
		);
	}

	// ------------------------------------------------------------
	// Pipe creation
	// ------------------------------------------------------------

	private void CreatePipes()
	{
		// ========================================================
		// Emitter pipe
		//
		// Points downward into the simulation.
		// ========================================================

		emitterPipe =
			new WaterPipeVisual();

		emitterPipe.Width =
			32.0f;

		emitterPipe.Length =
			48.0f;

		emitterPipe.Position =
			new Vector2(
				EmitterPipeX,
				EmitterPipeY
			);

		emitterPipe.SetPipeAngle(
			90.0f
		);

		AddChild(
			emitterPipe
		);

		// ========================================================
		// Despawner pipe
		//
		// Points downward out of the simulation.
		// ========================================================

		despawnerPipe =
			new WaterPipeVisual();

		despawnerPipe.Width =
			36.0f;

		despawnerPipe.Length =
			56.0f;

		despawnerPipe.Position =
			new Vector2(
				DespawnerPipeX,
				DespawnerPipeY
			);

		despawnerPipe.SetPipeAngle(
			90.0f
		);

		AddChild(
			despawnerPipe
		);
	}

	// ------------------------------------------------------------
	// Full profiler output
	// ------------------------------------------------------------

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

	// ------------------------------------------------------------
	// Reset profiler
	// ------------------------------------------------------------

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

	// ------------------------------------------------------------
	// Particle emitter
	// ------------------------------------------------------------

	private void SpawnParticle()
	{
		if (
			particles.Count >=
			particles.Capacity)
		{
			return;
		}

		float x =
			EmitterCenterX +
			EmitterOffsets[emitterIndex];

		float y =
			EmitterY;

		particles.AddParticle(
			x,
			y,
			0.0f,
			0.0f
		);

		emitterIndex++;

		if (
			emitterIndex >=
			EmitterOffsets.Length)
		{
			emitterIndex = 0;
		}
	}

	// ------------------------------------------------------------
	// Density field
	// ------------------------------------------------------------

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
}
