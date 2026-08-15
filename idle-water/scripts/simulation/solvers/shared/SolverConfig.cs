/// <summary>
/// SolverConfig — SCAFFOLDING PLACEHOLDER
///
/// Intended responsibility (Phase 3 migration target):
///   Immutable configuration container passed to every solver module at
///   initialization time.  Replaces the current scatter of private const
///   fields across PbfSolver.cs.
///
/// What will live here:
///   - Smoothing radius, rest density, gravity, world bounds.
///   - Iteration limits, correction caps, damping factors.
///   - Any tuning parameter that is shared across two or more solver modules.
///
/// What will NOT live here:
///   - Per-step mutable state (belongs in PbfState / SimulationStepContext).
///   - PBF-only constants that are never read by other modules
///     (those stay in PbfConstants.cs).
///
/// Current state: EMPTY SCAFFOLD — no logic has been migrated yet.
/// See docs/refactor-plan.md §1 for the constants-extraction plan.
/// </summary>
internal sealed class SolverConfig
{
	// TODO (Phase 3): add shared solver parameters as readonly properties.
}
