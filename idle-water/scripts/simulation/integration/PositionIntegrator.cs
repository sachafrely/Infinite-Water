/// <summary>
/// PositionIntegrator — SCAFFOLDING PLACEHOLDER
///
/// Intended responsibility (Phase 3 migration target):
///   Applies the final committed position update after all PBF constraint
///   iterations are complete.
///
/// What will live here:
///   - Commit predicted positions to authoritative positions in ParticleState.
///   - Re-derive corrected velocity: vel = (newPos - oldPos) / dt.
///   - Apply VelocityDamping.
///   - Reset per-step accumulators (deltaX, deltaY) to zero.
///
/// What will NOT live here:
///   - Per-iteration delta accumulation (→ PbfPositionDeltaSolver).
///   - Force/gravity application (→ VelocityIntegrator).
///
/// Current state: EMPTY SCAFFOLD.
/// </summary>
internal static class PositionIntegrator
{
	// TODO (Phase 3): add final position commit and velocity re-derivation.
}
