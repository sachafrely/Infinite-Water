
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
	// Emitter pipe
	// ------------------------------------------------------------

	private const float EmitterPipeX = 40.0f;
	private const float EmitterPipeY = 128.0f;
	private const float EmitterPipeLength = 144.0f;

	// Particles spawn from the right opening of the pipe.
	private const float EmitterOpeningOffset = 4.0f;

	// Small vertical stagger.
	private const float EmitterSpacing = 8.0f;

	// Initial horizontal velocity.
	private const float EmitterVelocityX = 120.0f;
	private const float EmitterVelocityY = 0.0f;

	private static readonly float[] EmitterOffsets =
	{
		-EmitterSpacing,
		0.0f,
		EmitterSpacing
	};

	private int emitterIndex = 0;

	// ------------------------------------------------------------
	// Drain pipe
	// ------------------------------------------------------------

	private const float DespawnerPipeX = 40.0f;
	private const float DespawnerPipeY = 1096.0f;
	private const float DespawnerPipeLength = 144.0f;

	// The opening is on the RIGHT side.
	private const float DespawnerOpeningX =
		DespawnerPipeX + DespawnerPipeLength;

	// Pipe width is 48, so this gives a generous opening area.
	private const float DespawnerOpeningHalfHeight = 28.0f;

	// Small horizontal tolerance around the opening.
	private const float DespawnerOpeningTolerance = 12.0f;

	// ------------------------------------------------------------
	// Water wheel
	// ------------------------------------------------------------

	private WaterWheelVisual waterWheel;

	private const float WheelCenterX = 350.0f;
	private const float WheelCenterY = 600.0f;

	private const float WheelOuterRadius = 115.0f;
	private const float WheelInnerRadius = 55.0f;

	private const int WheelBladeCount = 10;
	private const float WheelBladeWidth = 18.0f;

	// ------------------------------------------------------------
	// Pipes
	// ------------------------------------------------------------

	private WaterPipeVisual emitterPipe;
	private WaterPipeVisual despawnerPipe;

	// High Z so pipes are always drawn over the water.
	private const int PipeZIndex = 100;

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

		CreateWaterWheel();
		CreatePipes();

		densityField =
			new DensityField(
				DensityWidth,
				DensityHeight,
				DensityCellSize
			);

		renderer =
			new FluidRenderer();

		AddChild(renderer);

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
		// Drain
		//
		// IMPORTANT:
		// The drain opening is at the RIGHT end of the pipe.
		//
		// Pipe:
		// X = 40
		// Length = 144
		// Opening = X 184
		//
		// Particles reaching that opening are recycled back
		// to the emitter. This gives us an infinite water loop
		// without increasing the particle count.
		// --------------------------------------------------------

		RecycleParticlesAtDrain();

		// --------------------------------------------------------
		// Update wheel visual
		// --------------------------------------------------------

		if (
			waterWheel != null &&
			solver.Wheel != null)
		{
			waterWheel.SetWheelAngle(
				solver.Wheel.Angle
			);
		}

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
		// Create ONE shared physics wheel state.
		// --------------------------------------------------------

		FluidWheelState wheelState =
			solver.CreateWheel(
				center
			);

		// --------------------------------------------------------
		// Create wheel blade colliders.
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
			new Vector2[hubSegments];

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

		waterWheel.BladeWidth =
			WheelBladeWidth;

		AddChild(
			waterWheel
		);

		waterWheel.SetWheelAngle(
			wheelState.Angle
		);
	}

	// ------------------------------------------------------------
	// Pipe creation
	// ------------------------------------------------------------

	private void CreatePipes()
	{
		// ========================================================
		// Emitter pipe
		// ========================================================

		emitterPipe =
			new WaterPipeVisual();

		emitterPipe.Width =
			48.0f;

		emitterPipe.Length =
			EmitterPipeLength;

		emitterPipe.Position =
			new Vector2(
				EmitterPipeX,
				EmitterPipeY
			);

		// Horizontal.
		// Pipe extends from the LEFT toward the RIGHT.
		emitterPipe.SetPipeAngle(
			0.0f
		);

		// IMPORTANT:
		// Draw above the fluid renderer.
		emitterPipe.ZIndex =
			PipeZIndex;

		AddChild(
			emitterPipe
		);

		// ========================================================
		// Despawner pipe
		// ========================================================

		despawnerPipe =
			new WaterPipeVisual();

		despawnerPipe.Width =
			48.0f;

		despawnerPipe.Length =
			DespawnerPipeLength;

		despawnerPipe.Position =
			new Vector2(
				DespawnerPipeX,
				DespawnerPipeY
			);

		// Horizontal.
		// Opening is on the RIGHT.
		despawnerPipe.SetPipeAngle(
			0.0f
		);

		// IMPORTANT:
		// Draw above the fluid renderer.
		despawnerPipe.ZIndex =
			PipeZIndex;

		AddChild(
			despawnerPipe
		);
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

		// --------------------------------------------------------
		// Spawn at the RIGHT opening of the emitter pipe.
		//
		// Pipe starts at X = 40.
		// Pipe length = 144.
		// Opening = X 184.
		// --------------------------------------------------------

		float x =
			EmitterPipeX +
			EmitterPipeLength +
			EmitterOpeningOffset;

		float y =
			EmitterPipeY +
			EmitterOffsets[emitterIndex];

		// --------------------------------------------------------
		// Initial horizontal velocity.
		// --------------------------------------------------------

		particles.AddParticle(
			x,
			y,
			EmitterVelocityX,
			EmitterVelocityY
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
	// Drain
	// ------------------------------------------------------------

	private void RecycleParticlesAtDrain()
	{
		int count =
			particles.Count;

		float drainX =
			DespawnerOpeningX;

		float minX =
			drainX -
			DespawnerOpeningTolerance;

		float maxX =
			drainX +
			DespawnerOpeningTolerance;

		float minY =
			DespawnerPipeY -
			DespawnerOpeningHalfHeight;

		float maxY =
			DespawnerPipeY +
			DespawnerOpeningHalfHeight;

		for (
			int i = 0;
			i < count;
			i++)
		{
			float x =
				particles.PosX[i];

			float y =
				particles.PosY[i];

			// ----------------------------------------------------
			// Only remove/recycle water at the RIGHT opening.
			// ----------------------------------------------------

			if (
				x >= minX &&
				x <= maxX &&
				y >= minY &&
				y <= maxY)
			{
				RecycleParticle(
					i
				);
			}
		}
	}

	// ------------------------------------------------------------
	// Recycle individual particle
	// ------------------------------------------------------------

	private void RecycleParticle(
		int index)
	{
		float x =
			EmitterPipeX +
			EmitterPipeLength +
			EmitterOpeningOffset;

		float y =
			EmitterPipeY +
			EmitterOffsets[emitterIndex];

		particles.PosX[index] =
			x;

		particles.PosY[index] =
			y;

		particles.PredX[index] =
			x;

		particles.PredY[index] =
			y;

		particles.VelX[index] =
			EmitterVelocityX;

		particles.VelY[index] =
			EmitterVelocityY;

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
}
