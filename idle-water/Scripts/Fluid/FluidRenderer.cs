using Godot;

public partial class FluidRenderer : Node2D
{
	private MultiMeshInstance2D multiMeshInstance;
	private MultiMesh multiMesh;

	private int particleCount;


	public void Initialize(int count, float particleDiameter)
	{
		particleCount = count;

		// Create the mesh used by every particle.
		QuadMesh particleMesh = new QuadMesh
		{
			Size = new Vector2(
				particleDiameter,
				particleDiameter
			)
		};


		// Create the GPU instance buffer.
		multiMesh = new MultiMesh
		{
			TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
			UseColors = true,
			InstanceCount = count,
			Mesh = particleMesh
		};


		multiMeshInstance = new MultiMeshInstance2D
		{
			Multimesh = multiMesh
		};


		AddChild(multiMeshInstance);


		// Give every particle its color.
		for (int i = 0; i < count; i++)
		{
			multiMesh.SetInstanceColor(
				i,
				Colors.Blue
			);
		}
	}


	public void UpdateParticles(ParticleData particles)
	{
		// Reuse a single Transform2D to avoid per-particle allocations.
		Transform2D transform = new Transform2D();
		for (int i = 0; i < particleCount; i++)
		{
			// Write particle position directly into the transform origin.
			transform.origin.x = particles.PosX[i];
			transform.origin.y = particles.PosY[i];

			multiMesh.SetInstanceTransform2D(i, transform);
		}
	}
}
