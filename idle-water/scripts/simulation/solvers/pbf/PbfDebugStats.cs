/// <summary>
/// PbfDebugStats — SCAFFOLDING PLACEHOLDER
///
/// Intended responsibility (Phase 3 migration target, optional):
///   Collects and exposes per-step diagnostic data for the PBF pipeline
///   without polluting the hot-path solver code.
///
/// What will live here:
///   - Average / max density error per step.
///   - Iteration convergence counters.
///   - Sleep / wake event counters per frame.
///   - Helper to format stats into a human-readable string for the HUD.
///
/// What will NOT live here:
///   - Any logic that runs when debug stats are disabled — this module is
///     guarded by a compile-time or runtime flag so it costs nothing in
///     release builds.
///
/// Current state: EMPTY SCAFFOLD.
/// </summary>
internal static class PbfDebugStats
{
	// TODO (Phase 3, optional): add stat accumulation and reporting helpers.
}
