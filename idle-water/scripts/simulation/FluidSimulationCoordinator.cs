/// <summary>
/// Coordinates one fluid simulation step.
/// </summary>
internal sealed class FluidSimulationCoordinator
{
	private readonly PbfSolver solver;
	private readonly TiltController tiltController;

	/// <summary>
	/// Creates a coordinator for the active PBF solver.
	/// </summary>
	public FluidSimulationCoordinator(
		PbfSolver solver)
	{
		this.solver =
			solver;

		tiltController =
			new TiltController();
	}

	/// <summary>
	/// Executes the solver-owned simulation pipeline for one step.
	/// </summary>
	public void Step(
		ParticleData particles,
		float dt)
	{
		// Read the current device orientation before the solver predicts
		// particle positions. The solver still owns normal gravity; the tilt
		// controller applies only the difference caused by device tilt.
		tiltController.Update(dt);
		tiltController.ApplyToParticles(
			particles,
			dt
		);

		// The detailed sub-pass order remains inside PbfSolver:
		// neighbor search -> density constraints -> lambda solve ->
		// position deltas -> integration -> collision -> boundary.
		solver.Solve(
			particles,
			dt
		);
	}
}
