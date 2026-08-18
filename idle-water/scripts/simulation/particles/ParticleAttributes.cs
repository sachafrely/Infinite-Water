/// <summary>
/// ParticleAttributes — SCAFFOLDING PLACEHOLDER
///
/// Intended responsibility (Phase 3 migration target):
///   Stores per-particle attributes that are neither positions nor velocities
///   but are needed across multiple subsystems.
///
/// What will live here:
///   - phase[]               — fluid vs. solid markers (if phases are used).
///   - color[] / renderType  — lightweight render hint, no Godot types.
///   - Any future per-particle flag or category field.
///
/// What will NOT live here:
///   - Position / velocity (→ ParticleState).
///   - PBF scratch arrays (→ PbfState).
///   - Godot Node/Resource references.
///
/// Current state: EMPTY SCAFFOLD.
/// </summary>
internal sealed class ParticleAttributes
{
	// TODO (Phase 3): add per-particle attribute arrays as needed.
}
