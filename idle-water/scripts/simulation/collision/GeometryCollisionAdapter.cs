/// <summary>
/// GeometryCollisionAdapter — SCAFFOLDING PLACEHOLDER
///
/// Intended responsibility (Phase 3 migration target):
///   Manages arbitrary polygon colliders (non-tilemap geometry such as
///   FluidPolygonCollider nodes) and adapts them for the PBF pipeline.
///
/// What will live here:
///   - Register / unregister FluidPolygonCollider instances.
///   - Maintain bounding-box arrays (minX/Y, maxX/Y) per collider.
///   - Expose an API used by PbfBoundaryConstraints to iterate and test
///     polygon collider contacts.
///   - Wheel-collider management (currently in PbfSolver: RegisterWheelCollider,
///     EnsureWheelBounds, UpdateWheelBounds, WheelCollisionGroup) will
///     eventually live here or in a dedicated WheelCollisionAdapter.
///
/// What will NOT live here:
///   - Tile-derived colliders (→ TileCollisionAdapter).
///   - Particle collision math (→ PbfBoundaryConstraints).
///
/// Current state: EMPTY SCAFFOLD.
/// </summary>
internal sealed class GeometryCollisionAdapter
{
	// TODO (Phase 3): migrate polygon collider management from PbfSolver.
}
