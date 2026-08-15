# Script Architecture & Conventions

This document defines the folder layout under `idle-water/scripts/` and the
rules for placing code there during ongoing refactors.

> **Current state (Phase 2)**: Production scripts are now normalized under the
> canonical lowercase `scripts/` directory. `Scripts/` has been retired.

---

## Folder Map

```
idle-water/
└── scripts/                 ← Canonical script root
    ├── core/               ← Shared runtime utilities and entry helpers
    ├── simulation/         ← Simulation nodes and orchestration
    │   ├── solvers/        ← `PbfSolver` and extracted solver modules
    │   └── particles/      ← Particle data models
    ├── rendering/          ← Rendering and visualization scripts
    ├── input/              ← Input-related scripts
    ├── systems/            ← Existing subsystem modules
    ├── services/           ← Existing service modules
    ├── ui/                 ← Existing UI modules
    ├── data/               ← Existing data modules
    └── utils/              ← Existing utility modules
```

---

## What Belongs Where

### `scripts/core/`
- The main `Node2D` (or `Node`) entry-point classes.
- `_Ready`, `_Process`, `_PhysicsProcess` overrides.
- Orchestration code that calls into `systems/` and `services/`.
- **Rule**: No algorithm logic. Delegate to systems or utils.

### `scripts/systems/`
- Self-contained subsystems: rain, anti-lag, wheel energy, collider
  generation, region merging.
- Each system should be independently testable.
- May hold state, but should receive that state via constructor or method
  parameters rather than reaching into a global singleton.
- **Rule**: One responsibility per file. If a file exceeds ~400 lines,
  reconsider its scope.

### `scripts/services/`
- Scene-tree traversal helpers (`FindNodeByName`, `FindNodeOfType`).
- Viewport/coordinate mapping services.
- Services are typically stateless or hold only cached derived state.
- **Rule**: No direct physics or simulation logic.

### `scripts/ui/`
- HUD setup and update methods (`SetupRainHud`, `UpdateStatisticsHud`).
- Debug draw overrides (`_Draw`, `DebugEdge`).
- UI classes must not call solver methods directly; receive data as plain
  values (floats, ints) from `core/`.
- **Rule**: Zero fluid-physics imports.

### `scripts/data/`
- `const` tuning parameters (gravity, smoothing radius, rest density, etc.).
- Plain structs / records: `ParticleData`, `SolidRectangle`, `RunKey`.
- Enums: `AntiLagState`, etc.
- **Rule**: No Godot `Node` inheritance. No logic, only data.

### `scripts/utils/`
- Pure algorithms: pixel occupancy table, spatial hashing helpers,
  hash functions.
- No Godot `Node` dependency — must compile against plain C# if needed.
- **Rule**: All methods should be `static` or operate only on passed-in
  parameters.

---

## Practical Refactor Rules

1. **One step at a time.** Move one responsibility group per PR. Never
   combine a move with a logic change.
2. **Use `partial class` to split large files.** Add `partial` to the
   original class, then create a new `.cs` file in `scripts/<folder>/`
   with the second `partial` declaration. The compiler treats them as one
   class — no reference changes needed.
3. **No behavior changes in move PRs.** If you spot a bug while moving
   code, open a separate issue/PR.
4. **Keep scene references aligned with moved scripts.** Update `.tscn`
   `ext_resource` script paths in the same PR as file moves.
5. **Test after every move.** Open the Godot project, run the scene, confirm
   the water simulation runs without errors in the Output panel.
6. **Line-count budget per file: ~400 lines.** If a new file already
   exceeds this after a move, split it further before merging.
7. **Namespace convention**: `IdleWater.Core`, `IdleWater.Systems`,
   `IdleWater.Services`, `IdleWater.UI`, `IdleWater.Data`,
   `IdleWater.Utils` — match the folder name.
   **Important**: namespace migration must be done one class at a time.
   All `partial` files for the same class must share the same namespace,
   so update the original source file and every partial at the same time.

---

## PBFSolver split (first modular extraction)

- `scripts/simulation/solvers/PbfSolver.cs` remains the public entry point.
- Neighbor-search and neighbor-geometry cache logic is extracted to
  `scripts/simulation/solvers/PbfNeighborSearch.cs`.
- Density-constraint lambda computation is extracted to
  `scripts/simulation/solvers/PbfDensityConstraints.cs`.
