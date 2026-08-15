/// <summary>
/// PbfNeighborSearchAdapter — SCAFFOLDING PLACEHOLDER
///
/// Intended responsibility (Phase 3 migration target):
///   Thin adapter that calls SpatialHash / PbfNeighborSearch to populate the
///   neighbor-index cache stored in PbfState at the start of each tick.
///
/// What will live here:
///   - BuildCache(PbfState, SpatialHashService, SolverConfig) entry point.
///   - Any adapter-level caching or early-exit guard logic.
///
/// What will NOT live here:
///   - The core neighbor-search algorithm (stays in PbfNeighborSearch.cs and
///     SpatialHash.cs until those are migrated).
///
/// Current state: EMPTY SCAFFOLD.
/// </summary>
internal static class PbfNeighborSearchAdapter
{
	// TODO (Phase 3): delegate to PbfNeighborSearch.BuildNeighborIndexCache.
}
