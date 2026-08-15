# Script Architecture & Conventions

This document defines the folder layout under `idle-water/scripts/` and the
rules for placing code there during ongoing refactors.

> **Current state (Phase 1)**: The folders exist as scaffolding. Existing
> production code is still under `Scripts/Fluid/` and has not been moved.
> Move code here incrementally, one responsibility group at a time.

---

## Folder Map

```
idle-water/
├── Scripts/           ← Original location — keep untouched until Phase 2+
│   └── Fluid/
└── scripts/           ← New home for refactored code (lowercase, intentional)
    ├── core/          ← Node lifecycle, main loop, primary entry points
    ├── systems/       ← Self-contained simulation sub-systems
    ├── services/      ← Stateless helpers and scene-tree utilities
    ├── ui/            ← HUD, debug overlays, panel sync
    ├── data/          ← Pure data: structs, constants, enums, config objects
    └── utils/         ← Algorithms and math helpers with no Godot dependencies
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
4. **Keep existing `Scripts/` references working.** Do not rename or delete
   files in `Scripts/Fluid/` until all callers are updated and the change
   is verified in Godot.
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
