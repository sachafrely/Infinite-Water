/// <summary>
/// SolverConfig — immutable configuration container for the PBF solver.
///
/// Centralises tuning parameters that are shared across two or more
/// solver modules, eliminating the need to hard-code the same magic
/// values in multiple places.
///
/// Usage: construct once from <see cref="PbfSolver"/> constants and pass
/// to any module that needs configuration at initialization time.
/// Per-step mutable state belongs in <see cref="PbfState"/>;
/// PBF-only constants that are never read outside PBF modules remain
/// in <c>PbfConstants.cs</c>.
/// </summary>
internal sealed class SolverConfig
{
	// ============================================================
	// Geometry
	// ============================================================

	public float SmoothingRadius { get; init; }
	public float InverseSmoothingRadius { get; init; }

	// ============================================================
	// Density
	// ============================================================

	public float RestDensity { get; init; }
	public float InverseRestDensity { get; init; }
	public float LambdaEpsilon { get; init; }

	// ============================================================
	// PBF iteration
	// ============================================================

	public int MinIterations { get; init; }
	public int MaxIterations { get; init; }
	public float DensityErrorThreshold { get; init; }
	public float MaxCorrection { get; init; }
	public int MaxNeighbors { get; init; }

	// ============================================================
	// World
	// ============================================================

	public float Gravity { get; init; }
	public float MinX { get; init; }
	public float MaxX { get; init; }
	public float MinY { get; init; }
	public float MaxY { get; init; }
	public float BoundarySkin { get; init; }

	// ============================================================
	// Factory — build from current PbfSolver constants
	// ============================================================

	/// <summary>
	/// Creates a <see cref="SolverConfig"/> pre-populated with the
	/// current values from <see cref="PbfSolver"/>'s constants.
	///
	/// TODO (Phase 4): Thread this config through module constructors so
	/// sub-modules read from it instead of referencing <c>PbfSolver.X</c>
	/// constants directly.  This will enable run-time configuration swaps
	/// and easier unit-testing of individual pipeline stages.
	/// </summary>
	public static SolverConfig FromPbfConstants() =>
		new SolverConfig
		{
			SmoothingRadius           = PbfSolver.SmoothingRadius,
			InverseSmoothingRadius    = PbfSolver.InverseSmoothingRadius,
			RestDensity               = PbfSolver.RestDensity,
			InverseRestDensity        = PbfSolver.InverseRestDensity,
			LambdaEpsilon             = PbfSolver.LambdaEpsilon,
			MinIterations             = PbfSolver.MinIterations,
			MaxIterations             = PbfSolver.MaxIterations,
			DensityErrorThreshold     = PbfSolver.DensityErrorThreshold,
			MaxCorrection             = PbfSolver.MaxCorrection,
			MaxNeighbors              = PbfSolver.MaxNeighbors,
			Gravity                   = PbfSolver.Gravity,
			MinX                      = PbfSolver.MinX,
			MaxX                      = PbfSolver.MaxX,
			MinY                      = PbfSolver.MinY,
			MaxY                      = PbfSolver.MaxY,
			BoundarySkin              = PbfSolver.BoundarySkin,
		};
}
