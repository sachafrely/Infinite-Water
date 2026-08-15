/// <summary>
/// ISolver — SCAFFOLDING PLACEHOLDER
///
/// Intended responsibility (Phase 3 migration target):
///   Interface contract shared by all solver modules (PBF, future SPH, etc.)
///   so FluidSimulationCoordinator can drive them polymorphically.
///
/// What will live here:
///   - Initialize(SolverConfig) — configure the solver before the first step.
///   - Step(SimulationStepContext) — execute one physics sub-pass.
///   - Reset() — clear per-frame transient state.
///
/// Current state: EMPTY SCAFFOLD — no logic has been migrated yet.
/// See docs/architecture.md for the module boundary design.
/// </summary>
internal interface ISolver
{
	// TODO (Phase 3): define Initialize, Step, Reset method signatures.
}
