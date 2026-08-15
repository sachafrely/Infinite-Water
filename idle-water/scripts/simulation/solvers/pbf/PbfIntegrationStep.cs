/// <summary>
/// PbfIntegrationStep — SCAFFOLDING PLACEHOLDER
///
/// Intended responsibility (Phase 3 migration target):
///   Finalizes the PBF iteration by applying accumulated position deltas and
///   updating particle velocities from the position change.
///
/// What will live here:
///   - Apply PbfState.deltaX / deltaY to predicted positions.
///   - Derive velocity from (newPos - oldPos) / dt.
///   - Apply velocity damping (VelocityDamping constant).
///   - Copy predicted positions back to authoritative positions at end of
///     the last iteration.
///
/// What will NOT live here:
///   - Gravity application — belongs in the pre-integration pass inside
///     FluidSimulationCoordinator or a dedicated ApplyForces module.
///   - Boundary constraint enforcement — see PbfBoundaryConstraints.
///
/// Current state: EMPTY SCAFFOLD.
/// </summary>
internal static class PbfIntegrationStep
{
	// TODO (Phase 3): add delta application and velocity derivation.
}
