/// <summary>
/// PbfDensityConstraintsCoordinator — orchestrates the density-estimation
/// and lambda-computation pass for one PBF iteration.
///
/// Currently a thin delegation layer to <see cref="PbfLambdaSolver"/>.
/// In future phases this coordinator will drive additional density passes
/// (e.g., a surface-particle density sub-pass) before returning the maximum
/// density error to the caller.
/// </summary>
internal static class PbfDensityConstraintsCoordinator
{
	/// <summary>
	/// Runs the density + lambda pass for all particles.
	/// </summary>
	/// <returns>Maximum density error (used for early-exit check).</returns>
	public static float ComputeDensityAndLambdas(
		int count,
		PbfState state)
	{
		// TODO (Phase 4): add surface-particle density sub-pass here before
		// returning, once SurfaceParticles tagging moves into this coordinator.
		return PbfLambdaSolver.ComputeLambdas(
			count,
			state
		);
	}
}
