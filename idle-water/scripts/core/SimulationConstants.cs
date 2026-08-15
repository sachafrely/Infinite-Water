/// <summary>
/// SimulationConstants — SCAFFOLDING PLACEHOLDER
///
/// Intended responsibility (Phase 3 migration target):
///   Single authoritative location for all simulation-wide constants that
///   are shared across two or more subsystems.
///
/// What will live here:
///   - Physics universals: Gravity, SmoothingRadius, RestDensity.
///   - World bounds: MinX, MaxX, MinY, MaxY, BoundarySkin.
///   - Limits shared across solver and neighborhood modules.
///   - Any constant currently duplicated between PbfSolver.cs,
///     SpatialHash.cs, or FluidSimulator.cs.
///
/// What will NOT live here:
///   - PBF-only tuning constants that are never read outside PbfSolver
///     (those stay in PbfConstants.cs).
///   - Per-frame or mutable state.
///
/// Namespace note: this file uses no namespace to match the rest of the
/// codebase.  A future coordinated PR will add IdleWater.Core namespace.
///
/// Current state: EMPTY SCAFFOLD.
/// </summary>
internal static class SimulationConstants
{
	// TODO (Phase 3): move shared constants here from PbfConstants.cs and
	// FluidSimulator.cs once all consumers are updated in the same PR.
}
