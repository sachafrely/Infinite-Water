using Godot;

public partial class FluidSimulator : Node2D
{
	private ParticleData particles;
	private SpatialHash hash;
	private PbfSolver solver;
	private FluidRenderer renderer;
	private DensityField densityField;

	// Maximum number of particles.
	private const int ParticleCount = 4000;

	// ------------------------------------------------------------
	// Density rendering grid
	// ------------------------------------------------------------

	private const int DensityWidth = 180;
	private const int DensityHeight = 320;
	private const float DensityCellSize = 4.0f;

	// ------------------------------------------------------------
	// Simulation world
	// ------------------------------------------------------------

	private const float WorldWidth = 720.0f;
	private const float WorldHeight = 1280.0f;

	// ------------------------------------------------------------
	// Walls
	// ------------------------------------------------------------

	private const float LeftBound = 20.0f;
	private const float RightBound = 700.0f;
	private const float TopBound = 20.0f;
	private const float BottomBound = 1260.0f;

	// ------------------------------------------------------------
	// Fluid parameters
	// ------------------------------------------------------------

	private const float ParticleRadius = 4.0f;

	// ------------------------------------------------------------
	// Spatial hash
	// ------------------------------------------------------------

	private const float SmoothingRadius = 12.0f;
	private const float HashCellSize = SmoothingRadius;
	private const int HashWidth = 60;
	private const int HashHeight = 110;

	// ------------------------------------------------------------
	// Emitter
	// ------------------------------------------------------------

	private const float EmitterCenterX = 40.0f;
	private const float EmitterY = 40.0f;

	private const float EmitterSpacing = 8.0f;

	// Three possible emission positions.
	private static readonly float[] EmitterOffsets =
	{
		-EmitterSpacing,
		0.0f,
		EmitterSpacing
	};

	// Keeps track of which emitter position is used next.
	private int emitterIndex = 0;

	// ------------------------------------------------------------
	// Initialization
	// ------------------------------------------------------------

	public override void _Ready()
	{
		particles =
			new ParticleData(
				ParticleCount
			);

		hash =
			new SpatialHash(
				ParticleCount,
				HashCellSize,
				HashWidth,
				HashHeight
			);

		solver =
			new PbfSolver(hash);

		densityField =
			new DensityField(
				DensityWidth,
				DensityHeight,
				DensityCellSize
			);

		renderer =
			new FluidRenderer();

		AddChild(renderer);

		renderer.Initialize(
			DensityWidth,
			DensityHeight,
			DensityCellSize
		);

		// --------------------------------------------------------
		// Start with ZERO particles.
		// --------------------------------------------------------

		BuildDensityField();

		renderer.Update(
			particles,
			densityField
		);

		GD.Print(
			"Fluid initialized with " +
			particles.Count +
			" particles."
		);
	}

	// ------------------------------------------------------------
	// Physics
	// ------------------------------------------------------------

	public override void _PhysicsProcess(
		double delta)
	{
		float dt = (float)delta;

		// Spawn exactly ONE particle per physics frame.
		SpawnParticle();

		// Don't run the solver when there are no particles.
		if (particles.Count > 0)
		{
			solver.Solve(
				particles,
				dt
			);
		}

		BuildDensityField();

		renderer.Update(
			particles,
			densityField
		);
	}

	// ------------------------------------------------------------
	// Particle emitter
	// ------------------------------------------------------------

	private void SpawnParticle()
	{
		if (particles.Count >=
			particles.Capacity)
		{
			return;
		}

		float x =
			EmitterCenterX +
			EmitterOffsets[emitterIndex];

		float y =
			EmitterY;

		particles.AddParticle(
			x,
			y,
			0.0f,
			0.0f
		);

		// Move to the next emitter position.
		emitterIndex++;

		if (emitterIndex >=
			EmitterOffsets.Length)
		{
			emitterIndex = 0;
		}
	}

	// ------------------------------------------------------------
	// Density field
	// ------------------------------------------------------------

	private void BuildDensityField()
	{
		densityField.Clear();

		for (
			int i = 0;
			i < particles.Count;
			i++)
		{
			densityField.AddParticle(
				particles.PosX[i],
				particles.PosY[i]
			);
		}
	}
}
