# Script Architecture & Conventions

This document defines the folder layout under `idle-water/scripts/` and the
rules for placing code there during ongoing refactors.

> **Current state (Phase 3 — Scaffolding)**: Production scripts are normalized
> under the canonical lowercase `scripts/` directory. `Scripts/` has been
> retired.  Phase 3 adds folder structure and blank placeholder scripts that
> describe the intended module split for `FluidSimulator` and `PbfSolver`.
> **No logic has been migrated yet** — all runtime behaviour is unchanged.

---

## Folder Map

```
idle-water/
└── scripts/                 ← Canonical script root
    ├── core/               ← Shared runtime utilities, constants, debug hooks
    │   ├── FrameProfiler.cs
    │   ├── SimulationConstants.cs   ← SCAFFOLD: shared cross-system constants
    │   └── SimulationDebug.cs       ← SCAFFOLD: debug hook registry
    ├── simulation/         ← Simulation nodes and orchestration
    │   ├── FluidSimulator.cs        ← Live: Godot Node2D orchestrator (unchanged)
    │   ├── TileMapPhysics.cs        ← Live (unchanged)
    │   ├── DensityField.cs          ← Live (unchanged)
    │   ├── EnergySystem.cs          ← Live (unchanged)
    │   ├── FluidPolygonCollider.cs  ← Live (unchanged)
    │   ├── FluidSimulationCoordinator.cs  ← SCAFFOLD: future simulation loop host
    │   ├── SimulationStepContext.cs       ← SCAFFOLD: per-step shared context
    │   ├── solvers/
    │   │   ├── PbfSolver.cs         ← Live: current monolithic PBF entry point
    │   │   ├── PbfConstants.cs      ← Live: PBF-only tuning constants (partial)
    │   │   ├── PbfNeighborSearch.cs ← Live: neighbor index cache builder
    │   │   ├── PbfDensityConstraints.cs ← Live: density/lambda kernel math
    │   │   ├── SpatialHash.cs       ← Live: spatial hash grid
    │   │   ├── shared/
    │   │   │   ├── ISolver.cs       ← SCAFFOLD: solver interface contract
    │   │   │   └── SolverConfig.cs  ← SCAFFOLD: shared config container
    │   │   └── pbf/
    │       │       ├── PbfSolverCoordinator.cs        ← SCAFFOLD: future PBF pipeline host
    │   │       ├── PbfState.cs                    ← SCAFFOLD: per-step mutable arrays
    │   │       ├── PbfNeighborSearchAdapter.cs    ← SCAFFOLD: neighbor search adapter
    │   │       ├── PbfDensityConstraintsCoordinator.cs ← SCAFFOLD: density pass host
    │   │       ├── PbfLambdaSolver.cs             ← SCAFFOLD: lambda computation pass
    │   │       ├── PbfPositionDeltaSolver.cs      ← SCAFFOLD: delta accumulation pass
    │   │       ├── PbfIntegrationStep.cs          ← SCAFFOLD: final position commit
    │   │       ├── PbfBoundaryConstraints.cs      ← SCAFFOLD: boundary enforcement
    │   │       └── PbfDebugStats.cs               ← SCAFFOLD: optional debug stats
    │   ├── particles/
    │   │   ├── ParticleData.cs      ← Live (unchanged)
    │   │   ├── ParticleState.cs     ← SCAFFOLD: future authoritative position/vel arrays
    │   │   └── ParticleAttributes.cs ← SCAFFOLD: future per-particle attribute arrays
    │   ├── neighborhood/
    │   │   ├── SpatialHashService.cs ← SCAFFOLD: clean neighbor query API wrapper
    │   │   └── NeighborQuery.cs      ← SCAFFOLD: query input/result value type
    │   ├── constraints/
    │   │   ├── DensityConstraint.cs   ← SCAFFOLD: pure density constraint helpers
    │   │   ├── BoundaryConstraint.cs  ← SCAFFOLD: boundary projection helpers
    │   │   └── ViscosityConstraint.cs ← SCAFFOLD: future XSPH viscosity pass
    │   ├── integration/
    │   │   ├── VelocityIntegrator.cs  ← SCAFFOLD: pre-constraint velocity + prediction
    │   │   └── PositionIntegrator.cs  ← SCAFFOLD: post-constraint position commit
    │   └── collision/
    │       ├── TileCollisionAdapter.cs     ← SCAFFOLD: tilemap collider grid adapter
    │       └── GeometryCollisionAdapter.cs ← SCAFFOLD: polygon/wheel collider adapter
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

---

## Phase 3 — Module Boundaries & Scaffolding (current phase)

> **Status**: Placeholder files created. No logic migrated yet.

### Module boundary rules

| Module | Owns | Does NOT own |
|--------|------|-------------|
| `FluidSimulator.cs` | Godot Node2D lifecycle, scene wiring, HUD, rain, anti-lag, wheels | PBF math, neighbor search, collision internals |
| `FluidSimulationCoordinator` | High-level per-tick call sequence | Scene tree, Godot types, rendering |
| `PBFSolverCoordinator` | PBF sub-pass ordering | Any individual sub-pass math |
| `PbfState` | Mutable per-step arrays (lambdas, deltas, density) | Persistent cross-frame state (→ ParticleState) |
| `ParticleState` | Authoritative pos/vel arrays across frames | Per-step scratch (→ PbfState) |
| `SpatialHashService` | Clean neighbor query API | Hash cell math (→ SpatialHash.cs) |
| `TileCollisionAdapter` | Collider grid, tilemap-derived polygons | Tile scanning logic (→ TileMapPhysics.cs) |
| `GeometryCollisionAdapter` | Arbitrary polygon + wheel collider registration | Collision resolution math (→ PbfBoundaryConstraints) |
| `SimulationConstants` | Constants shared across ≥ 2 modules | PBF-only constants (→ PbfConstants.cs) |

### How `FluidSimulator` and `PBFSolver` will be split

**`FluidSimulator` (after migration)**
- Remains the Godot `Node2D` entry point — keeps `_Ready`, `_PhysicsProcess`.
- Creates and configures sub-modules but does not execute PBF math itself.
- Delegates the physics tick to `FluidSimulationCoordinator.Step(context)`.

**`PBFSolverCoordinator` (after migration)**
- Owns the ordered call sequence for one PBF solve step.
- Holds references to the sub-module instances.
- `PbfSolver.cs` remains the live, callable entry point until the migration
  is complete; `PBFSolverCoordinator` will replace it at the end of Phase 3.

### What these scaffold files are NOT

- They contain no simulation logic.
- They define no methods or properties; any attempt to call a method that
  does not yet exist will produce a compile error.
- They exist solely to lock in the intended file/class names and module
  responsibilities before logic migration begins.
- Runtime behaviour is **100% unchanged** from Phase 2.

### Next migration steps (recommended order)

1. Convert `PbfSolver` and `FluidSimulator` to `partial class` (zero-risk).
2. Move shared constants from `PbfConstants.cs` to `SimulationConstants`.
3. Implement `SolverConfig` and thread it through `PBFSolverCoordinator`.
4. Implement `PbfState` arrays; wire `PBFSolverCoordinator` to call the
   existing static methods in `PbfNeighborSearch` and `PbfDensityConstraints`.
5. Implement `TileCollisionAdapter` / `GeometryCollisionAdapter`; remove
   the collider-grid code from `PbfSolver.cs`.
6. Implement `VelocityIntegrator` / `PositionIntegrator`; extract from
   the `Solve()` method in `PbfSolver.cs`.
7. Implement `FluidSimulationCoordinator`; retire `_PhysicsProcess`
   inline logic in `FluidSimulator.cs`.

See `docs/refactor-plan.md` for the detailed line-level split plan for
each of the three largest files.
