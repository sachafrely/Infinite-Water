/// <summary>
/// DensityConstraint — SCAFFOLDING PLACEHOLDER
///
/// Intended responsibility (Phase 3 migration target):
///   Encapsulates a single particle's density constraint evaluation, separate
///   from the outer PBF iteration loop.
///
/// What will live here:
///   - Evaluate(particleIndex, neighbors, positions) → constraint value C_i.
///   - Gradient(particleIndex, neighborIndex, positions) → ∇C vector.
///   - These are pure functions with no side effects.
///
/// What will NOT live here:
///   - Lambda computation (→ PbfLambdaSolver).
///   - Position delta application (→ PbfPositionDeltaSolver).
///
/// Current state: EMPTY SCAFFOLD.
/// </summary>
internal static class DensityConstraint
{
	// TODO (Phase 3): add pure density constraint evaluation helpers.
}
