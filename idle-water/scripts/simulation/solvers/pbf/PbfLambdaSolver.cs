/// <summary>
/// PbfLambdaSolver — SCAFFOLDING PLACEHOLDER
///
/// Intended responsibility (Phase 3 migration target):
///   Computes the PBF constraint scalar lambda for every particle from the
///   density estimates stored in PbfState.
///
/// What will live here:
///   - Per-particle lambda calculation: C_i / (|∇C_i|² + ε).
///   - Reads: PbfState.particleDensity, neighbor data.
///   - Writes: PbfState.lambdas.
///
/// What will NOT live here:
///   - Gradient kernel math — extracted to PbfDensityConstraints.cs.
///   - Position delta application — that is PbfPositionDeltaSolver's job.
///
/// Current state: EMPTY SCAFFOLD.
/// </summary>
internal static class PbfLambdaSolver
{
	// TODO (Phase 3): add lambda computation pass.
}
