using Godot;
public partial class FluidSimulator : Node2D

{
	private ParticleData particles;
	private SpatialHash hash;
	private PbfSolver solver;
	
	private const int ParticleCount = 1000;

		// Simulation world dimensions.
	private const float WorldWidth = 8.0f;
	private const float WorldHeight = 14.0f;

		// PBF parameters.
	private const float ParticleRadius = 0.05f;
	private const float SmoothingRadius = 0.10f;

		// Spatial hash.
	private const float HashCellSize = SmoothingRadius;
	private const int HashWidth = 80;
	private const int HashHeight = 140;

	public override void _Ready()
	{

		particles =
			new ParticleData(
				ParticleCount
			);


hash = new SpatialHash(
	ParticleCount,
	HashCellSize,
	HashWidth,
	HashHeight
);


		solver =
			new PbfSolver(hash);



		Spawn();

	}




	private void Spawn()
	{

		for(int i=0;i<ParticleCount;i++)
		{

			particles.PosX[i] =
				300 + (i%25)*8;


			particles.PosY[i] =
				100 + (i/25)*8;

		}

	}




	public override void _PhysicsProcess(double delta)
	{

		float dt = (float)delta;

		ApplyGravity(dt);

		UpdateHash();

		// solver.Solve(particles);

		QueueRedraw();

	}




	private void ApplyGravity(float dt)
	{

		for(int i=0;i<particles.Count;i++)
		{

			particles.VelY[i] +=
				500f * dt;


			particles.PosX[i] +=
				particles.VelX[i]*dt;


			particles.PosY[i] +=
				particles.VelY[i]*dt;

		}

	}




	private void UpdateHash()
	{

		hash.Clear();


		for(int i=0;i<particles.Count;i++)
		{

			hash.Insert(
				i,
				particles.GetPosition(i)
			);

		}

	}




	public override void _Draw()
	{

		for(int i=0;i<particles.Count;i++)
		{

			DrawCircle(
				particles.GetPosition(i),
				3,
				Colors.Blue
			);

		}

	}
}
