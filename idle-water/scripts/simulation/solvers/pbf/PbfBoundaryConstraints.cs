/// <summary>
/// PbfBoundaryConstraints — SCAFFOLDING PLACEHOLDER
///
/// Intended responsibility (Phase 3 migration target):
///   Enforces world-bounds and polygon-collider constraints for every particle
///   after each PBF iteration.
///
/// What will live here:
///   - World AABB clamping (MinX/MaxX/MinY/MaxY with BoundarySkin).
///   - Polygon collider penetration resolution via the collider grid.
///   - Wheel-collider boundary enforcement.
///   - Restitution and friction application on boundary contacts.
///
/// What will NOT live here:
///   - Collider list management or spatial grid rebuild — those belong in
///     TileCollisionAdapter / GeometryCollisionAdapter.
///   - Surface flow / GroundDrag — those are post-constraint adjustments
///     applied later in the pipeline.
///
/// Current state: EMPTY SCAFFOLD.
/// </summary>
internal static class PbfBoundaryConstraints
{
	// TODO (Phase 3): add world-bounds and polygon boundary enforcement.
}
