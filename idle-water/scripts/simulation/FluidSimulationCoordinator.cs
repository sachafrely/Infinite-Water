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
		// particle positions. Tilt changes the gravity vector itself; it does
		// not directly modify particle velocities.
		tiltController.Update(dt);

		// The PBF coordinator reads the gravity vector calculated by the tilt
		// controller when it performs its normal gravity prediction.
		solver.Solve(
			particles,
			dt
		);
	}
}
