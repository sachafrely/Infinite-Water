using Godot;


public partial class FluidSimulator : Node2D
{

	private ParticleData particles;

	private SpatialHash hash;

	private PbfSolver solver;



	private const int ParticleCount = 1000;



	public override void _Ready()
	{

		particles =
			new ParticleData(
				ParticleCount
			);


		hash =
			new SpatialHash(
				ParticleCount,
				0.12f,
				100,
				200
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
