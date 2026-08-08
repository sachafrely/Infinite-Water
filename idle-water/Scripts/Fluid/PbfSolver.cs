using Godot;


public class PbfSolver
{

	private readonly SpatialHash hash;


	public float SmoothingRadius = 0.12f;


	public PbfSolver(
		SpatialHash hash)
	{
		this.hash = hash;
	}



public void Solve(ParticleData particles)
{
	for(int i = 0; i < particles.Count; i++)
	{
		Vector2 pos = particles.GetPosition(i);

		int count = hash.Query(
			pos,
			SmoothingRadius
		);


		for(int n = 0; n < count; n++)
		{
			int neighbor = hash.GetResult(n);

			if(neighbor == i)
				continue;


			Vector2 a = particles.GetPosition(i);
			Vector2 b = particles.GetPosition(neighbor);


			if(a.DistanceSquaredTo(b) >
			   SmoothingRadius * SmoothingRadius)
			{
				continue;
			}


			// Density calculation later
		}
	}
}
}
