using Godot;

public partial class FluidSimulator : Node2D
{
private ParticleData particles;
private SpatialHash hash;
private PbfSolver solver;
private FluidRenderer renderer;
private DensityField densityField;


private const int ParticleCount = 1000;

// Density rendering grid.
private const int DensityWidth = 180;
private const int DensityHeight = 320;
private const float DensityCellSize = 4.0f;

// Simulation world.
private const float WorldWidth = 720.0f;
private const float WorldHeight = 1280.0f;

// Walls.
private const float LeftBound = 20.0f;
private const float RightBound = 700.0f;
private const float TopBound = 20.0f;
private const float BottomBound = 1260.0f;

// Fluid parameters.
private const float ParticleRadius = 4.0f;

// Spatial hash.
private const float SmoothingRadius = 12.0f;
private const float HashCellSize = SmoothingRadius;
private const int HashWidth = 60;
private const int HashHeight = 110;

public override void _Ready()
{
	particles = new ParticleData(ParticleCount);

	hash = new SpatialHash(
		ParticleCount,
		HashCellSize,
		HashWidth,
		HashHeight
	);

	solver = new PbfSolver(hash);

	densityField = new DensityField(
		DensityWidth,
		DensityHeight,
		DensityCellSize
	);

	renderer = new FluidRenderer();
	AddChild(renderer);

	renderer.Initialize(
		DensityWidth,
		DensityHeight,
		DensityCellSize
	);

	Spawn();

	// Build the initial visual field immediately.
	BuildDensityField();

	renderer.Update(
		particles,
		densityField
	);

	GD.Print($"Particle 0: {particles.PosX[0]}, {particles.PosY[0]}");
	GD.Print($"Particle 999: {particles.PosX[999]}, {particles.PosY[999]}");
}

private void Spawn()
{
	const int Columns = 80;
	const float ParticleSpacing = 8.0f;

	for (int i = 0; i < ParticleCount; i++)
	{
		int column = i % Columns;
		int row = i / Columns;

		particles.PosX[i] =
			40.0f + column * ParticleSpacing;

		particles.PosY[i] =
			40.0f + row * ParticleSpacing;

		particles.VelX[i] = 0.0f;
		particles.VelY[i] = 0.0f;

		particles.PredX[i] = particles.PosX[i];
		particles.PredY[i] = particles.PosY[i];
	}
}

public override void _PhysicsProcess(double delta)
{
	float dt = (float)delta;

	solver.Solve(
		particles,
		dt
	);

	BuildDensityField();

	renderer.Update(
		particles,
		densityField
	);
}

private void BuildDensityField()
{
	densityField.Clear();

	for (int i = 0; i < particles.Count; i++)
	{
		densityField.AddParticle(
			particles.PosX[i],
			particles.PosY[i]
		);
	}
}


}
