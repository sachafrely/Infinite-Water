using Godot;

public partial class FluidRenderer : Node2D
{
	private MultiMeshInstance2D multiMeshInstance;
	private MultiMesh multiMesh;

	private int particleCount;

	private const float SurfaceBrightness = 1.0f;
	private const float BodyBrightness = 0.65f;

	public void Initialize(int count, float particleDiameter)
	{
		particleCount = count;

		// ------------------------------------------------------------
		// Larger visual particle size.
		//
		// Physics radius remains 4 px.
		// This is purely a rendering decision.
		// ------------------------------------------------------------

		float renderDiameter = particleDiameter * 3.5f;

		QuadMesh particleMesh = new QuadMesh
		{
			Size = new Vector2(
				renderDiameter,
				renderDiameter
			)
		};

		// ------------------------------------------------------------
		// MultiMesh
		// ------------------------------------------------------------

		multiMesh = new MultiMesh
		{
			TransformFormat =
				MultiMesh.TransformFormatEnum.Transform2D,

			UseColors = true,

			Mesh = particleMesh
		};

		multiMesh.InstanceCount = count;

		multiMeshInstance = new MultiMeshInstance2D
		{
			Multimesh = multiMesh
		};

		// ------------------------------------------------------------
		// Water shader
		// ------------------------------------------------------------

		Shader shader = new Shader();

		shader.Code = @"
shader_type canvas_item;

void fragment()
{
    // UV is 0..1 across the quad.
    // Convert it to a centered coordinate.
    vec2 centered = UV - vec2(0.5);

    // Distance from the center.
    float distanceFromCenter =
        length(centered) * 2.0;

    // Soft circular edge.
    float alpha =
        1.0 - smoothstep(
            0.72,
            1.0,
            distanceFromCenter
        );

    // Slightly soft interior.
    float innerGlow =
        1.0 - smoothstep(
            0.0,
            0.85,
            distanceFromCenter
        );

    // Base water color.
    vec3 waterBlue =
        vec3(
            0.08,
            0.42,
            0.85
        );

    // Surface particles are brighter through
    // their MultiMesh vertex color.
    float brightness =
        mix(
            0.65,
            1.0,
            COLOR.r
        );

    // Slight highlight toward the upper part
    // of each water particle.
    float highlight =
        smoothstep(
            0.8,
            0.15,
            UV.y
        ) * 0.12;

    vec3 finalColor =
        waterBlue *
        (brightness + highlight);

    // Slightly translucent water.
    float finalAlpha =
        alpha * 0.72;

    COLOR = vec4(
        finalColor,
        finalAlpha
    );
}
";

		ShaderMaterial material = new ShaderMaterial
		{
			Shader = shader
		};

		multiMeshInstance.Material = material;

		AddChild(multiMeshInstance);
	}

	public void UpdateParticles(
		ParticleData particles,
		bool[] surfaceParticles
	)
	{
		Transform2D transform =
			Transform2D.Identity;

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

			// --------------------------------------------------------
			// Surface particles are brighter.
			//
			// The shader reads the red channel of the instance color
			// as the brightness value.
			// --------------------------------------------------------

			if (surfaceParticles != null &&
				surfaceParticles[i])
			{
				multiMesh.SetInstanceColor(
					i,
					new Color(
						SurfaceBrightness,
						SurfaceBrightness,
						SurfaceBrightness,
						1.0f
					)
				);
			}
			else
			{
				multiMesh.SetInstanceColor(
					i,
					new Color(
						BodyBrightness,
						BodyBrightness,
						BodyBrightness,
						1.0f
					)
				);
			}
		}
	}
}
