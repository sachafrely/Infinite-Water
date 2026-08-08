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
		for (int i = 0; i < particleCount; i++)
		{
			Transform2D transform = new Transform2D(
				0.0f,
				new Vector2(
					particles.PosX[i],
					particles.PosY[i]
				)
			);

			multiMesh.SetInstanceTransform2D(
				i,
				transform
			);
		}
	}
}
