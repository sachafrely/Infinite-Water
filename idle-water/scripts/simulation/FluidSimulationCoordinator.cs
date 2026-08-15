/// <summary>
/// FluidSimulationCoordinator — SCAFFOLDING PLACEHOLDER
///
/// Intended responsibility (Phase 3 migration target):
///   Owns the high-level simulation update loop and wires the individual
///   sub-modules together each physics tick.
///
/// What will live here:
///   - Call sequence: neighbor search → density constraints → lambda solve →
///     position deltas → integration → collision → boundary.
///   - Dependency injection / initialization of solver sub-modules.
///   - Propagation of SimulationStepContext to each module.
///
/// What will NOT live here:
///   - Low-level PBF math (stays in simulation/solvers/pbf/).
///   - Scene-tree wiring / Godot Node lifecycle (_Ready, _Process) — those
///     remain in FluidSimulator.cs.
///   - Rendering or HUD concerns.
///
/// Current state: EMPTY SCAFFOLD — no logic has been migrated yet.
/// See docs/architecture.md and docs/refactor-plan.md for the migration plan.
/// </summary>
internal static class FluidSimulationCoordinator
{
	// TODO (Phase 3): inject modules and coordinate per-step execution.
}
