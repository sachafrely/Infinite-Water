/// <summary>
/// VelocityIntegrator — SCAFFOLDING PLACEHOLDER
///
/// Intended responsibility (Phase 3 migration target):
///   Handles the explicit velocity integration pass that runs at the
///   *start* of each physics tick, before the PBF constraint loop.
///
/// What will live here:
///   - Apply external forces (gravity, water-wheel drag) to velocities.
///   - Predict new positions: predPos = pos + vel * dt.
///   - Clamp velocities to MaxVelocity (if a cap is introduced).
///
/// What will NOT live here:
///   - Post-constraint velocity correction — that is PbfIntegrationStep's job.
///   - Force computation (forces passed in as parameters, not computed here).
///
/// Current state: EMPTY SCAFFOLD.
/// </summary>
internal static class VelocityIntegrator
{
	// TODO (Phase 3): add pre-constraint velocity and prediction integration.
}
