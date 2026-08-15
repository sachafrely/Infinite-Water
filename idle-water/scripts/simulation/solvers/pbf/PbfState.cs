/// <summary>
/// PbfState — SCAFFOLDING PLACEHOLDER
///
/// Intended responsibility (Phase 3 migration target):
///   Owns all mutable per-frame PBF state arrays that are written during a
///   single solver tick and read by the sub-modules that follow.
///
/// What will live here:
///   - Predicted position arrays (predX, predY).
///   - Lambda array (one scalar per particle).
///   - Density estimate array (particleDensity).
///   - Position-correction accumulation arrays (deltaX, deltaY).
///   - Neighbor count / neighbor buffer arrays.
///   - Sleep-progress and sleeping-flag arrays.
///
/// What will NOT live here:
///   - Authoritative particle positions/velocities — those live in
///     ParticleState (or remain in ParticleData until migrated).
///   - Constants and config — those live in PbfConstants / SolverConfig.
///
/// Current state: EMPTY SCAFFOLD — live state arrays remain in PbfSolver.cs.
/// </summary>
internal sealed class PbfState
{
	// TODO (Phase 3): declare arrays and allocation logic.
}
