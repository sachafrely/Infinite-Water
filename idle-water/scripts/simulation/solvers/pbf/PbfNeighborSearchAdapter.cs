/// <summary>
/// PbfNeighborSearchAdapter — thin adapter over the PbfNeighborSearch static helpers.
///
/// Reads predicted positions from the caller and writes the resulting
/// neighbor indices and per-pair geometry into <see cref="PbfState"/>.
/// The underlying kernel math remains in
/// <c>scripts/simulation/solvers/PbfNeighborSearch.cs</c>.
/// </summary>
internal static class PbfNeighborSearchAdapter
{
	/// <summary>
	/// Builds the flat neighbor-index cache for all particles using the
	/// provided spatial hash.
	/// </summary>
	public static void BuildIndexCache(
		SpatialHash hash,
		float[] predX,
		float[] predY,
		int count,
		PbfState state)
	{
		PbfNeighborSearch.BuildNeighborIndexCache(
			hash,
			predX,
			predY,
			count,
			state.NeighborStride,
			state.NeighborCounts,
			state.NeighborBuffer,
			PbfSolver.MaxNeighbors
		);
	}

	/// <summary>
	/// Updates the per-pair geometry (dx, dy, q, gradientScale) for all
	/// particles using their current predicted positions.
	/// </summary>
	public static void UpdateGeometry(
		float[] predX,
		float[] predY,
		int count,
		PbfState state)
	{
		PbfNeighborSearch.UpdateAllNeighborGeometry(
			predX,
			predY,
			count,
			state.NeighborStride,
			state.NeighborCounts,
			state.NeighborBuffer,
			state.NeighborDx,
			state.NeighborDy,
			state.NeighborQ,
			state.NeighborGradientScale,
			PbfSolver.InverseSmoothingRadius,
			PbfSolver.InverseRestDensity
		);
	}

	/// <summary>
	/// Re-runs only the geometry update pass (without rebuilding the
	/// neighbor-index cache) — called on solver iterations after the first.
	/// </summary>
	public static void UpdateCache(
		float[] predX,
		float[] predY,
		int count,
		PbfState state)
	{
		PbfNeighborSearch.UpdateNeighborCache(
			predX,
			predY,
			count,
			state.NeighborStride,
			state.NeighborCounts,
			state.NeighborBuffer,
			state.NeighborDx,
			state.NeighborDy,
			state.NeighborQ,
			state.NeighborGradientScale,
			PbfSolver.InverseSmoothingRadius,
			PbfSolver.InverseRestDensity
		);
	}
}
