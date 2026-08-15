/// <summary>
/// NeighborQuery — SCAFFOLDING PLACEHOLDER
///
/// Intended responsibility (Phase 3 migration target):
///   Value type / result container for a single neighbor-search query,
///   used to decouple query input parameters from the results written back
///   into PbfState.
///
/// What will live here:
///   - queryX, queryY     — query origin.
///   - radius             — search radius.
///   - neighborIndices[]  — output: particle indices within radius.
///   - count              — output: number of valid neighbors found.
///
/// What will NOT live here:
///   - Query execution logic (→ SpatialHashService).
///   - Persistent neighbor cache (→ PbfState).
///
/// Current state: EMPTY SCAFFOLD.
/// </summary>
internal struct NeighborQuery
{
	// TODO (Phase 3): add query fields and result buffers.
}
