
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
	//
	// Simulation world = 920 x 1020
	// World bounds: X 260..1180, Y -200..820
	// Density rendering remains 1440 x 720.
	// Cell size = 4
	// ------------------------------------------------------------

	private const int DensityWidth = 360;
	private const int DensityHeight = 180;
	private const float DensityCellSize = 4.0f;

	// ------------------------------------------------------------
	// Simulation world
	// ------------------------------------------------------------

	private const float WorldWidth = 920.0f;
	private const float WorldHeight = 1020.0f;

	private const float WorldMinX = 260.0f;
	private const float WorldMaxX = 1180.0f;
	private const float WorldMinY = -200.0f;
	private const float WorldMaxY = 820.0f;

	// ------------------------------------------------------------
	// Rain
	// ------------------------------------------------------------

	private const float RainAmount = 0.25f;

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

	// ------------------------------------------------------------
	// Despawn
	// ------------------------------------------------------------

	private const float DespawnLeftX =
		WorldMinX + 8.0f;

	private const float DespawnRightX =
		WorldMaxX - 8.0f;

	private const float DespawnBottomY =
		WorldMaxY - 8.0f;

	// ------------------------------------------------------------
	// Water wheel
	//
	// HALF the previous size.
	//
	// Outer radius:
	//   100 -> 50
	//
	// Inner radius:
	//   25 -> 12.5
	//
	// Blade width:
	//   15 -> 7.5
	// ------------------------------------------------------------

	private WaterWheelVisual waterWheel;

	private const float WheelCenterX =
		720.0f;

	private const float WheelCenterY =
		360.0f;

	private const float WheelOuterRadius =
		50.0f;

	private const float WheelInnerRadius =
		12.5f;

	private const int WheelBladeCount =
		8;

	private const float WheelBladeWidth =
		7.5f;

	// ------------------------------------------------------------
	// Full-frame profiler
	// ------------------------------------------------------------

	private const int FullProfilerInterval =
		60;

	private int fullProfilerFrames =
		0;

	private double fullPhysicsTime =
		0.0;

	private double fullSpawnTime =
		0.0;

	private double fullPbfTime =
		0.0;

	private double fullDensityTime =
		0.0;

	private double fullRendererTime =
		0.0;

	private double fullRendererBuildPixelsTime =
		0.0;

	private double fullRendererSurfaceGlowTime =
		0.0;

	private double fullRendererFillBytesTime =
		0.0;

	private double fullRendererTextureUploadTime =
		0.0;

	// ------------------------------------------------------------
	// Initialization
	// ------------------------------------------------------------

	public override void _Ready()
	{
		rainRandom.Randomize();

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

		CreateWaterWheel();

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
			"%"
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
		// Rain
		// --------------------------------------------------------

		Stopwatch spawnTimer =
			Stopwatch.StartNew();

		SpawnRainParticle();

		spawnTimer.Stop();

		fullSpawnTime +=
			spawnTimer.Elapsed.TotalMilliseconds;

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

		pbfTimer.Stop();

		fullPbfTime +=
			pbfTimer.Elapsed.TotalMilliseconds;

		// --------------------------------------------------------
		// Despawn
		// --------------------------------------------------------

		RecycleParticlesAtOuterEdges();

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
		// Shared wheel physics state.
		// --------------------------------------------------------

		FluidWheelState wheelState =
			solver.CreateWheel(
				center
			);

		// --------------------------------------------------------
		// Create wheel blade colliders.
		//
		// Keep the original winding. FluidPolygonCollider
		// automatically determines the winding and chooses the
		// correct normal.
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

		const int hubSegments =
			16;

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

		// The hub is intentionally NOT configured as a wheel.
		// Only the blades transmit torque.
		solver.AddPolygonCollider(
			hubCollider
		);

		// --------------------------------------------------------
		// Visual wheel
		//
		// The visual dimensions match the physical half-size.
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
	// Rain emitter
	// ------------------------------------------------------------

	private void SpawnRainParticle()
	{
		if (
			RainAmount <= 0.0f)
		{
			return;
		}

		if (
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

	// ------------------------------------------------------------
	// Spawn one rain particle
	// ------------------------------------------------------------

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

	// ------------------------------------------------------------
	// Despawn / recycle
	// ------------------------------------------------------------

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
				x <=
				DespawnLeftX;

			bool reachedRight =
				x >=
				DespawnRightX;

			bool reachedBottom =
				y >=
				DespawnBottomY;

			if (
				reachedLeft ||
				reachedRight ||
				reachedBottom)
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
		fullProfilerFrames =
			0;

		fullPhysicsTime =
			0.0;

		fullSpawnTime =
			0.0;

		fullPbfTime =
			0.0;

		fullDensityTime =
			0.0;

		fullRendererTime =
			0.0;

		fullRendererBuildPixelsTime =
			0.0;

		fullRendererSurfaceGlowTime =
			0.0;

		fullRendererFillBytesTime =
			0.0;

		fullRendererTextureUploadTime =
			0.0;
	}
}
