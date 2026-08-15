/// <summary>
/// TileCollisionAdapter — SCAFFOLDING PLACEHOLDER
///
/// Intended responsibility (Phase 3 migration target):
///   Bridges TileMapPhysics (the Godot-layer tilemap scanner) and the
///   PBF collision pipeline.
///
/// What will live here:
///   - Accept polygon collider lists from TileMapPhysics.
///   - Expose a stable AddCollider / ClearColliders / RebuildGrid API that
///     PbfBoundaryConstraints consumes.
///   - Own the collider spatial grid (colliderGrid, colliderMinX/Y,
///     colliderMaxX/Y) — currently living inside PbfSolver.cs.
///
/// What will NOT live here:
///   - Tile-scanning or polygon-generation logic (stays in
///     scripts/simulation/TileMapPhysics.cs).
///   - Geometry-polygon handling (→ GeometryCollisionAdapter).
///
/// Current state: EMPTY SCAFFOLD.
/// </summary>
internal sealed class TileCollisionAdapter
{
	// TODO (Phase 3): migrate collider grid from PbfSolver into this adapter.
}
