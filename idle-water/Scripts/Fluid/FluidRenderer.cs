using Godot;

public partial class FluidRenderer : Node2D
{
	private MultiMeshInstance2D multiMeshInstance;
	private MultiMesh multiMesh;

	private int particleCount;

	public void Initialize(int count, float particleDiameter)
	{
		particleCount = count;

		QuadMesh particleMesh = new QuadMesh
		{
			Size = new Vector2(
				particleDiameter,
				particleDiameter
			)
		};

		multiMesh = new MultiMesh
		{
			TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,

			// IMPORTANT:
			// This must be enabled before InstanceCount.
			UseColors = true,

			Mesh = particleMesh
		};

		// Set the instance count AFTER UseColors.
		multiMesh.InstanceCount = count;

		multiMeshInstance = new MultiMeshInstance2D
		{
			Multimesh = multiMesh
		};

		AddChild(multiMeshInstance);

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
		Transform2D transform = Transform2D.Identity;

		for (int i = 0; i < particleCount; i++)
		{
			transform.Origin = new Vector2(
				particles.PosX[i],
				particles.PosY[i]
			);

			multiMesh.SetInstanceTransform2D(
				i,
				transform
			);
		}
	}
}
