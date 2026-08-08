using Godot;

public partial class FluidRenderer : Node2D
{
	private MultiMeshInstance2D multiMeshInstance;
	private MultiMesh multiMesh;

	private int particleCount;

	public void Initialize(int count, float particleSize)
	{
		particleCount = count;

		multiMesh = new MultiMesh
		{
			TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
			UseColors = false,
			UseCustomData = false,
			InstanceCount = count
		};

		// A simple quad that represents one particle.
		QuadMesh quad = new QuadMesh
		{
			Size = new Vector2(particleSize, particleSize)
		};

		multiMesh.Mesh = quad;

		multiMeshInstance = new MultiMeshInstance2D
		{
			Multimesh = multiMesh
		};

		AddChild(multiMeshInstance);
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

			multiMesh.SetInstanceTransform2D(i, transform);
		}
	}
}
