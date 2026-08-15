/// <summary>
/// BoundaryConstraint — SCAFFOLDING PLACEHOLDER
///
/// Intended responsibility (Phase 3 migration target):
///   Pure boundary-constraint helpers shared by both world-bounds clamping
///   and polygon-collider penetration resolution.
///
/// What will live here:
///   - Project(position, normal, offset) — project a point out of a half-plane.
///   - Restitution / friction impulse helpers.
///   - Any boundary-type-agnostic math that PbfBoundaryConstraints calls into.
///
/// What will NOT live here:
///   - Collider grid data or lookups (→ TileCollisionAdapter /
///     GeometryCollisionAdapter).
///   - Outer particle loops — those stay in PbfBoundaryConstraints.
///
/// Current state: EMPTY SCAFFOLD.
/// </summary>
internal static class BoundaryConstraint
{
	// TODO (Phase 3): add boundary projection and impulse helpers.
}
