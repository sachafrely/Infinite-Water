/// <summary>
/// PbfLambdaSolver — computes per-particle PBF constraint scalars (λ_i).
///
/// Reads the neighbor geometry from <see cref="PbfState"/> and delegates
/// the per-particle math to
/// <c>scripts/simulation/solvers/PbfDensityConstraints.cs</c>.
/// Writes the results back to <see cref="PbfState.Lambdas"/> and
/// <see cref="PbfState.ParticleDensity"/>.
/// </summary>
internal static class PbfLambdaSolver
{
	/// <summary>
	/// Computes lambdas and density estimates for all particles.
	/// </summary>
	/// <returns>Maximum density error across all particles this iteration.</returns>
	public static float ComputeLambdas(
		int count,
		PbfState state)
	{
		return PbfDensityConstraints.CalculateLambdas(
			count,
			state.NeighborStride,
			state.NeighborCounts,
			state.NeighborQ,
			state.NeighborGradientScale,
			state.NeighborDx,
			state.NeighborDy,
			state.ParticleDensity,
			state.Lambdas,
			PbfSolver.InverseRestDensity,
			PbfSolver.LambdaEpsilon
		);
	}
}
