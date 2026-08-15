/// <summary>
/// PbfSolverCoordinator — SCAFFOLDING PLACEHOLDER
///   (future home of the PBF pipeline coordinator shell)
///
/// Intended responsibility (Phase 3 migration target):
///   Top-level coordinator for the Position-Based Fluids pipeline.
///   Replaces the monolithic PbfSolver.cs by delegating each sub-pass to a
///   dedicated module.
///
/// Planned execution order each physics tick:
///   1. PbfNeighborSearchAdapter  — build neighbor index cache
///   2. PbfDensityConstraints     — estimate per-particle density
///   3. PbfLambdaSolver           — compute lambda corrections
///   4. PbfPositionDeltaSolver    — accumulate position deltas
///   5. PbfBoundaryConstraints    — enforce world + polygon boundaries
///   6. PbfIntegrationStep        — apply deltas, update velocities
///
/// What will NOT live here:
///   - The actual math for each sub-pass (stays in the dedicated modules).
///   - Scene-tree / Godot Node API (stays in FluidSimulator.cs).
///
/// Current state: EMPTY SCAFFOLD — the live PBF logic remains in
///   scripts/simulation/solvers/PbfSolver.cs.
/// See docs/refactor-plan.md §1 for the split plan.
/// </summary>
internal static class PbfSolverCoordinator
{
	// TODO (Phase 3): wire sub-module calls in the correct order.
}
