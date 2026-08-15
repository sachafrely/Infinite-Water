/// <summary>
/// SpatialHashService — SCAFFOLDING PLACEHOLDER
///
/// Intended responsibility (Phase 3 migration target):
///   Service wrapper around the low-level SpatialHash struct that exposes a
///   clean API for neighbor queries without leaking implementation details.
///
/// What will live here:
///   - Rebuild(positions, count) — update the hash grid for the current step.
///   - QueryRadius(x, y, radius, out results) — public neighbor lookup API.
///   - Geometry-aware query variant (replaces SpatialHash.QueryPbfWithGeometry
///     once constants are centralized in SolverConfig).
///
/// What will NOT live here:
///   - Hash cell math — stays in SpatialHash.cs.
///   - Neighbor-list storage (lives in PbfState).
///
/// Current state: EMPTY SCAFFOLD.
/// </summary>
internal sealed class SpatialHashService
{
	// TODO (Phase 3): wrap SpatialHash with a stable public query API.
}
