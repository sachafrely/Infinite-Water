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

	private const int ParticleCount = 10000;

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
	// Spatial hash
	// ------------------------------------------------------------

	private const float SmoothingRadius = 12.0f;
	private const float HashCellSize = SmoothingRadius;
	private const int HashWidth = 60;
	private const int HashHeight = 110;

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
	// Full-frame profiler
	// ------------------------------------------------------------

	private const int FullProfilerInterval = 60;

	private int fullProfilerFrames = 0;

	private double fullPhysicsTime = 0.0;

	private double fullSpawnTime = 0.0;
	private double fullPbfTime = 0.0;
	private double fullDensityTime = 0.0;
	private double fullRendererTime = 0.0;

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
				ParticleCount,
				HashCellSize,
				HashWidth,
				HashHeight
			);

		solver =
	new PbfSolver(hash);

// --------------------------------------------------------
// Triangle obstacle
// --------------------------------------------------------

Vector2[] triangle =
{
	new Vector2(250, 700),
	new Vector2(450, 700),
	new Vector2(350, 500)
};

FluidPolygonCollider obstacle =
	new FluidPolygonCollider(triangle);

solver.AddPolygonCollider(obstacle);
			
			
	
		
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

		// --------------------------------------------------------
		// Start with ZERO particles.
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
		// Marching Squares / renderer
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

		if (fullProfilerFrames >=
			FullProfilerInterval)
		{
			PrintFullProfiler();

			ResetFullProfiler();
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

		// This is the actual rendered FPS.
		double fps =
			Engine.GetFramesPerSecond();

		// Theoretical FPS based ONLY on our
		// measured PhysicsProcess duration.
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
	}

	// ------------------------------------------------------------
	// Particle emitter
	// ------------------------------------------------------------

	private void SpawnParticle()
	{
		if (particles.Count >=
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

		if (emitterIndex >=
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
