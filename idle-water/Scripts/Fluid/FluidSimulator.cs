using Godot;

public partial class FluidSimulator : Node2D
{
	private ParticleData particles;
	private SpatialHash hash;
	private PbfSolver solver;

	private const int ParticleCount = 1000;

	// Simulation world.
	private const float WorldWidth = 8.0f;
	private const float WorldHeight = 14.0f;

	// Fluid parameters.
	private const float ParticleRadius = 0.05f;
	private const float SmoothingRadius = 0.10f;

	// Spatial hash.
	private const float HashCellSize = SmoothingRadius;
	private const int HashWidth = 80;
	private const int HashHeight = 140;

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

		Spawn();
	}


	private void Spawn()
	{
		const int columns = 20;
		const float spacing = 0.08f;

		const float startX = 2.0f;
		const float startY = 2.0f;

		for (int i = 0; i < ParticleCount; i++)
		{
			int x = i % columns;
			int y = i / columns;

			particles.PosX[i] = startX + x * spacing;
			particles.PosY[i] = startY + y * spacing;

			particles.VelX[i] = 0.0f;
			particles.VelY[i] = 0.0f;
		}
	}


	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;

		ApplyGravity(dt);
		UpdateHash();

		// PBF will be enabled later.
		// solver.Solve(particles);

		QueueRedraw();
	}


	private void ApplyGravity(float dt)
	{
		for (int i = 0; i < particles.Count; i++)
		{
			particles.VelY[i] += Gravity * dt;

			particles.PosX[i] += particles.VelX[i] * dt;
			particles.PosY[i] += particles.VelY[i] * dt;
		}
	}


	private void UpdateHash()
	{
		hash.Clear();

		for (int i = 0; i < particles.Count; i++)
		{
			hash.Insert(
				i,
				particles.GetPosition(i)
			);
		}
	}


	public override void _Draw()
	{
		for (int i = 0; i < particles.Count; i++)
		{
			DrawCircle(
				particles.GetPosition(i),
				ParticleRadius,
				Colors.Blue
			);
		}
	}
}
