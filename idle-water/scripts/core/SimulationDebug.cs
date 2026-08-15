/// <summary>
/// SimulationDebug — SCAFFOLDING PLACEHOLDER
///
/// Intended responsibility (Phase 3 migration target):
///   Central debug-hook registry for the simulation subsystems.
///   Decouples debug instrumentation from hot-path code.
///
/// What will live here:
///   - IsEnabled flag (compile-time or runtime toggle).
///   - LogStep(string category, string message) — routed to Godot GD.Print
///     only when debug mode is active.
///   - DrawParticleStat(index, value) — hook for per-particle debug overlay.
///   - Hooks consumed by PbfDebugStats to push data to the debug HUD.
///
/// What will NOT live here:
///   - Rendering logic — this module only collects/forwards data.
///   - Any code that runs unconditionally in release builds.
///
/// Namespace note: no namespace to match codebase convention.
///
/// Current state: EMPTY SCAFFOLD.
/// </summary>
internal static class SimulationDebug
{
	// TODO (Phase 3): add debug toggle flag and log/draw hook stubs.
}
