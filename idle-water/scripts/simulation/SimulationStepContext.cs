/// <summary>
/// SimulationStepContext — SCAFFOLDING PLACEHOLDER
///
/// Intended responsibility (Phase 3 migration target):
///   A lightweight value-container that is populated once at the start of
///   each physics tick and passed (by reference) into every solver module.
///
/// What will live here:
///   - Per-step scalars: delta time, current iteration index, particle count.
///   - Read-only references to shared arrays (positions, velocities, etc.)
///     that must not be re-allocated mid-step.
///   - Flags for optional sub-passes (e.g. debug stats enabled).
///
/// What will NOT live here:
///   - Persistent cross-frame state (stays in PbfState / ParticleState).
///   - Scene-tree references or Godot Node types.
///
/// Current state: EMPTY SCAFFOLD — no logic has been migrated yet.
/// See docs/refactor-plan.md for the migration plan.
/// </summary>
internal struct SimulationStepContext
{
	// TODO (Phase 3): add per-step fields (dt, iteration, particleCount, …).
}
