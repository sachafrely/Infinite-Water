using Godot;

public partial class FluidSimulator : Node2D
{
	private ParticleData particles;
	private SpatialHash hash;
	private PbfSolver solver;
	private FluidRenderer renderer;
	
	private const int ParticleCount = 1000;

	// Simulation world (use viewport-sized coordinates).
	private const float WorldWidth = 720.0f;
	private const float WorldHeight = 1280.0f;

	// Fluid parameters.
	private const float ParticleRadius = 4.0f;
	private const float SmoothingRadius = 12.0f;

	// Spatial hash.
	private const float HashCellSize = SmoothingRadius;
	private const int HashWidth = 60;
	private const int HashHeight = 110;

	// Physics.
	private const float Gravity = 9.81f;


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

	renderer = new FluidRenderer();
	AddChild(renderer);

	renderer.Initialize(
		ParticleCount,
		ParticleRadius * 2.0f
	);

	Spawn();
}

	private void Spawn()
	{
		const int Columns = 80;
		const float ParticleSpacing = 8.0f;

		for (int i = 0; i < ParticleCount; i++)
		{
			int column = i % Columns;
			int row = i / Columns;

			particles.PosX[i] = 40.0f + column * ParticleSpacing;
			particles.PosY[i] = 40.0f + row * ParticleSpacing;

			particles.VelX[i] = 0.0f;
			particles.VelY[i] = 0.0f;

			particles.PredX[i] = particles.PosX[i];
			particles.PredY[i] = particles.PosY[i];
		}
	}


	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;

		solver.Solve(particles, dt);

		renderer.UpdateParticles(particles);
	}


}
