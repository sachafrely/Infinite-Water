/// <summary>
/// Coordinates one fluid simulation step.
/// </summary>
internal sealed class FluidSimulationCoordinator
{
	private readonly PbfSolver solver;

	/// <summary>
	/// Creates a coordinator for the active PBF solver.
	/// </summary>
	public FluidSimulationCoordinator(
		PbfSolver solver)
	{
		this.solver =
			solver;
	}

	/// <summary>
	/// Executes the solver-owned simulation pipeline for one step.
	/// </summary>
	public void Step(
		ParticleData particles,
		float dt)
	{
		// The detailed sub-pass order remains inside PbfSolver:
		// neighbor search -> density constraints -> lambda solve ->
		// position deltas -> integration -> collision -> boundary.
		solver.Solve(
			particles,
			dt
		);
	}
}
