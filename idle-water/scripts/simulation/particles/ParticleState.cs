/// <summary>
/// ParticleState — SCAFFOLDING PLACEHOLDER
///
/// Intended responsibility (Phase 3 migration target):
///   The single authoritative owner of all per-particle position and velocity
///   arrays that persist across physics frames.
///
/// What will live here:
///   - posX[], posY[]    — authoritative world-space positions.
///   - velX[], velY[]    — authoritative velocities.
///   - count             — live particle count.
///   - Allocate(capacity) / Resize helpers.
///
/// What will NOT live here:
///   - Predicted / corrected positions — those are per-step scratch data
///     owned by PbfState.
///   - Visual/rendering properties — those stay in ParticleData.cs until
///     rendering is fully decoupled.
///
/// Relation to existing code:
///   ParticleData.cs currently holds this data; ParticleState will replace
///   or wrap it once the migration is ready.
///
/// Current state: EMPTY SCAFFOLD.
/// </summary>
internal sealed class ParticleState
{
	// TODO (Phase 3): declare authoritative position/velocity arrays.
}
