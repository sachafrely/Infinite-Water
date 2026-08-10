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

private const float EmitterCenterX = 76.0f;
private const float EmitterY = 128.0f;
private const float EmitterSpacing = 8.0f;

private const float EmitterVelocityX = 90.0f;
private const float EmitterVelocityY = 0.0f;

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
private const float WheelBladeWidth = 18.0f;

// ------------------------------------------------------------
// Pipes
// ------------------------------------------------------------

private WaterPipeVisual emitterPipe;
private WaterPipeVisual despawnerPipe;

private const float EmitterPipeX = -15.0f;
private const float EmitterPipeY = 128.0f;

private const float DespawnerPipeX = -15.0f;
private const float DespawnerPipeY = 1100.0f;

private const float PipeWidth = 48.0f;
private const float PipeLength = 96.0f;

// ------------------------------------------------------------
// Drain
// ------------------------------------------------------------

private const float DrainOpeningRadius =
	18.0f;

private const float DrainCenterX =
	DespawnerPipeX + PipeLength;

private const float DrainCenterY =
	DespawnerPipeY;

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
	// Remove water at bottom pipe
	// --------------------------------------------------------

	RemoveDrainParticles();

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
// Remove particles entering drain
// ------------------------------------------------------------

private void RemoveDrainParticles()
{
	int i = 0;

	while (
		i <
		particles.Count)
	{
		float dx =
			particles.PosX[i] -
			DrainCenterX;

		float dy =
			particles.PosY[i] -
			DrainCenterY;

		float distanceSquared =
			dx * dx +
			dy * dy;

		float removalRadius =
			DrainOpeningRadius +
			ParticleRadius;

		if (
			distanceSquared <=
			removalRadius *
			removalRadius)
		{
			particles.RemoveParticle(i);

			// Do not increment i.
			// A new particle was swapped into this slot.
			continue;
		}

		i++;
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

	FluidWheelState wheelState =
		solver.CreateWheel(
			center
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
	// Top emitter pipe
	// ========================================================

	emitterPipe =
		new WaterPipeVisual();

	emitterPipe.Width =
		PipeWidth;

	emitterPipe.Length =
		PipeLength;

	emitterPipe.Position =
		new Vector2(
			EmitterPipeX,
			EmitterPipeY
		);

	emitterPipe.SetPipeAngle(
		0.0f
	);

	AddChild(
		emitterPipe
	);

	// ========================================================
	// Bottom drain pipe
	// ========================================================

	despawnerPipe =
		new WaterPipeVisual();

	despawnerPipe.Width =
		PipeWidth;

	despawnerPipe.Length =
		PipeLength;

	despawnerPipe.Position =
		new Vector2(
			DespawnerPipeX,
			DespawnerPipeY
		);

	despawnerPipe.SetPipeAngle(
		0.0f
	);

	AddChild(
		despawnerPipe
	);

	// ========================================================
	// Make pipes render above the water.
	// ========================================================

	emitterPipe.ZIndex =
		100;

	despawnerPipe.ZIndex =
		100;
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
