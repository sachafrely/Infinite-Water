/// <summary>
/// ViscosityConstraint — SCAFFOLDING PLACEHOLDER
///
/// Intended responsibility (Phase 3 migration target, if viscosity is added):
///   Velocity-smoothing / viscosity sub-pass that reduces relative velocity
///   between neighboring particles.
///
/// What will live here:
///   - XSPH viscosity update: v_i += ε · Σ_j (v_j - v_i) · W(r_ij).
///   - Viscosity coefficient and kernel constants.
///
/// What will NOT live here:
///   - Density constraint math (→ DensityConstraint).
///   - Boundary enforcement (→ BoundaryConstraint / PbfBoundaryConstraints).
///
/// Relevance: currently the live solver does not implement explicit viscosity;
/// this file is reserved for a future optional viscosity pass.
///
/// Current state: EMPTY SCAFFOLD.
/// </summary>
internal static class ViscosityConstraint
{
	// TODO (future): add XSPH velocity smoothing pass if viscosity is needed.
}
