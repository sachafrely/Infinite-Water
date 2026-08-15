/// <summary>
/// PbfDensityConstraintsCoordinator — SCAFFOLDING PLACEHOLDER
///
/// Intended responsibility (Phase 3 migration target):
///   Drives the density-estimation pass: reads neighbor data from PbfState,
///   computes per-particle density estimates, and writes results back to
///   PbfState.  Delegates the per-particle kernel math to the existing
///   PbfDensityConstraints static class.
///
/// What will live here:
///   - Outer loop orchestration over all particles.
///   - Read from PbfState.neighborCounts / neighborBuffer.
///   - Write to PbfState.particleDensity.
///
/// What will NOT live here:
///   - Kernel math (Poly6 / Spiky gradient) — stays in
///     scripts/simulation/solvers/PbfDensityConstraints.cs.
///
/// Current state: EMPTY SCAFFOLD.
/// </summary>
internal static class PbfDensityConstraintsCoordinator
{
	// TODO (Phase 3): add density estimation entry point.
}
