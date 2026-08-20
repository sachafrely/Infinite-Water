# Refactor Plan — Top-3 Longest Scripts

Generated: 2026-08-15  
**Current state reviewed: 2026-08-20**

---

## Current Refactoring State — 2026-08-20

This section supersedes the original status line below and records the actual state of the project as of **20.08.2026**.

### Overall status

**Refactoring is partially started, but the original Top-3 refactor is not yet completed.**

The project has evolved beyond the original Phase 1 scaffolding state. The repository now has a clearer folder structure with dedicated areas such as:

- `Scripts/core`
- `Scripts/simulation`
- `Scripts/ui`

There are also already extracted/supporting systems in the newer structure, including the tilt-related classes (`TiltController`, `TiltSettings`) and other core/simulation/UI components.

However, the main objective of this document remains unfinished: the large monolithic simulation classes have **not yet been systematically split into the responsibility-based files proposed below**.

### Current refactor assessment

| Area | Current state | Assessment |
|---|---|---|
| Folder/architecture organization | Core, simulation and UI areas exist | **Started** |
| Supporting systems extracted | Several newer dedicated systems/classes exist | **Partially done** |
| `PbfSolver` full responsibility split | Still requires major decomposition | **Not done** |
| `FluidSimulator` full responsibility split | Still requires major decomposition | **Not done** |
| `TileMapPhysics` full responsibility split | Still requires major decomposition | **Not done** |
| Constants/config separation | Some configuration has been moved/organized, but the planned complete separation is not finished | **Partial** |
| UI separation | UI has its own directory and multiple dedicated scripts, but the larger refactor is unfinished | **Partial** |
| Gameplay systems separation | Rain, tilt, energy/wheel and related systems have evolved into dedicated components, but coupling remains | **Partial** |
| Top-3 refactor completion | The planned extraction sequence has not been completed | **Not done** |

### Important conclusion

The project should **not** be described as being in "Phase 1 — Scaffolding only. No code moved yet" anymore. That description was accurate for the original 15.08.2026 snapshot, but it is outdated as of 20.08.2026.

The more accurate status is:

> **Phase 2 — Partial modularization. Several systems have already been separated, but the three major monolithic classes still need their planned responsibility-based refactor.**

The refactor should now continue incrementally rather than restarting the project architecture from scratch.

### Recommended next refactoring order as of 20.08.2026

1. Inspect the current `PbfSolver`, `FluidSimulator`, and `TileMapPhysics` implementations and update their exact line counts/responsibility boundaries.
2. Convert the remaining monolithic classes to `partial class` where this reduces risk.
3. Extract constants/configuration without changing behavior.
4. Extract one self-contained responsibility at a time.
5. Compile and run the Godot project after every extraction.
6. Keep existing gameplay behavior unchanged during the refactor.
7. After the Top-3 are under control, continue with `StatisticsGraph`, `FluidPolygonCollider`, `FrameProfiler`, `WaterWheelVisual`, and `RainSystem`.

**Refactor principle:** this is a structural cleanup, not a gameplay rewrite. Existing behavior should remain unchanged unless a specific bug fix is intentionally being made.

---

## Original Refactor Snapshot — 2026-08-15

Status at the time this plan was generated: **Phase 1 — Scaffolding only. No code moved yet.**

---

## Top-3 Longest Scripts

| Rank | File | Lines |
|------|------|-------|
| 1 | `Scripts/Fluid/PbfSolver.cs` | 3 611 |
| 2 | `Scripts/Fluid/FluidSimulator.cs` | 3 216 |
| 3 | `Scripts/Fluid/TileMapPhysics.cs` | 1 979 |

> These line counts are historical measurements from 15.08.2026. They should not be treated as the current line counts.

---

## 1. `PbfSolver.cs` — Proposed Split Boundaries

`PbfSolver` is a monolithic Position-Based Fluids solver with five distinct
responsibility clusters.

### 1.1 Responsibility Groups

| Group | Responsibility | Key Members |
|-------|---------------|-------------|
| **Constants / Config** | All `private const` simulation parameters (gravity, smoothing radius, rest density, sleep thresholds, world bounds, etc.) | lines 75–147 — all `private const` fields |
| **Collider Management** | Polygon collider list maintenance, collider spatial grid, bounds arrays, terrain stamp | `ClearPolygonColliders`, `AddPolygonCollider`, `RebuildColliderGrid`, `GetColliderCellX/Y`, `colliderGrid`, `colliderMinX/Y`, `colliderMaxX/Y`, `terrainColliderQueryStamp` |
| **Wheel Interaction** | Water-wheel collision groups, wheel bounds, wheel registration | `RegisterWheelCollider`, `EnsureWheelBounds`, `UpdateWheelBounds`, `WheelCollisionGroup` nested class, `wheelMinX/Y`, `wheelMaxX/Y` |
| **Solver Core** | PBF iteration: density estimation, lambda calculation, position correction, velocity update | `Solve` (entry point, line ~401), `CalculateParticlePackingStats`, `ApplyPositionCorrections`, `ConstrainToPolygonColliders`, `ApplySurfaceFlow`, `lambdas`, `particleDensity` |
| **Pixel Occupancy** | Sub-pixel collision deduplication table | `InitializePixelOccupancyTable`, `FindPixelOccupancySlot`, `HashPixelCoordinates`, `IsExactPixelOverlap`, `pixelOccupancyX/Y/Count/...` |
| **Sleep System** | Particle sleeping / waking | `sleepProgress`, `sleeping`, sleep threshold consts, sleep logic inside `Solve` |

### 1.2 Recommended Future File Split

```
scripts/core/PbfSolverCore.cs         — Solve(), lambda, density, position corrections
scripts/core/PbfSolverSleep.cs        — Sleep/wake logic (partial class)
scripts/systems/ColliderGrid.cs       — Terrain collider grid and polygon management
scripts/systems/WheelCollision.cs     — Water-wheel collision groups and bounds
scripts/utils/PixelOccupancyTable.cs  — Pixel-level overlap deduplication
scripts/data/PbfConstants.cs          — All const tuning parameters
```

> **Prerequisite**: Convert `PbfSolver` to `partial class` before splitting.

---

## 2. `FluidSimulator.cs` — Proposed Split Boundaries

`FluidSimulator` is the Godot `Node2D` that orchestrates the whole simulation.
It mixes scene setup, rain spawning, anti-lag, HUD sync, energy, and physics.

### 2.1 Responsibility Groups

| Group | Responsibility | Key Members |
|-------|---------------|-------------|
| **Node Lifecycle / Setup** | `_Ready`, camera centering, scene-node wiring | `_Ready` (line 318), `CenterSimulationCamera`, `FindEnvironmentRecursive`, `FindNodeByName`, `FindNodeOfType` |
| **Physics Tick** | `_PhysicsProcess`, solver step, profiler | `_PhysicsProcess` (line 517) |
| **Rain System** | Rain spawn rate, transitions, anti-lag, density gate | `currentRainPercent`, `targetRainPercent`, `rainPhaseTimer`, `CanSpawnAtPixel`, `RegisterParticlePixel`, `RebuildPixelOccupancy`, `rainSpawnAccumulator` |
| **Anti-Lag System** | FPS-based particle culling/evaporation | `EvaluateAntiLagProfilerResult`, `StartAntiLagCleanup`, `AntiLagState` enum, `antiLagStateTimer`, `antiLagCleanupCount` |
| **Water Wheel / Energy** | Wheel creation, energy accumulation, current indicator | `CreateWaterWheelsFromEnvironment`, `CreateWaterWheel`, `StepAdditionalWheels`, `UpdateWheelVisuals`, `InitializeWheelEnergyTracking`, `UpdateEnergyFromWheelRotation`, `SetupCurrentIndicator`, `UpdateCurrentIndicator` |
| **HUD / UI Sync** | Rain slider, statistics panel updates | `SetupRainHud`, `UpdateRainHud`, `SetupStatisticsHud`, `UpdateStatisticsHud` |
| **Pixel Occupancy** | High-level pixel density grid used by rain spawn | `InitializePixelOccupancy`, `TryGetPixelIndex`, `GetPixelOccupancy`, `pixelOccupancy`, `pixelOccupancyStamp` |

### 2.2 Recommended Future File Split

```
scripts/core/FluidSimulatorCore.cs     — _Ready, _PhysicsProcess, node wiring
scripts/systems/RainSystem.cs          — Rain spawning, transitions, density gate
scripts/systems/AntiLagSystem.cs       — FPS-based particle culling
scripts/systems/WheelEnergySystem.cs   — Water wheels and energy generation
scripts/ui/FluidSimulatorHud.cs        — Rain HUD, statistics panel (partial class)
scripts/utils/PixelOccupancy.cs        — Pixel-level spawn grid helpers
```

---

## 3. `TileMapPhysics.cs` — Proposed Split Boundaries

`TileMapPhysics` scans a `TileMapLayer`, converts solid tiles into polygon
colliders, handles viewport mapping, and draws debug geometry.

### 3.1 Responsibility Groups

| Group | Responsibility | Key Members |
|-------|---------------|-------------|
| **Node Lifecycle** | `_Ready`, `_Process`, property exports, node resolution | `_Ready` (line 204), `Initialize` (line 238), `_Process` (line 320), `GetEnvironment`, `FindTileMapLayer`, `GetSolver`, `FindNodeOfType` |
| **Collider Generation** | Tile scanning, solid rectangle detection, polygon building | `GenerateColliders` (line 609), `IsEmptyBackgroundPixel`, run-length merge structs (`SolidRun`, `RunKey`), `SolidRectangle` |
| **Viewport Mapping** | SimulationViewport ↔ world space transform lookup | `FindViewportMapping` (line 430) |
| **Debug Rendering** | `_Draw`, debug edges, overlay geometry | `_Draw` (line 333), `DebugEdge` struct, `debugEdges` list |
| **Merge / Region Logic** | Merging adjacent solid rectangles to reduce collider count | `MergeSolidRegions`, `ListSolidRectangles` (line 1175), `HashSet<Vector2I>` helper (line 903) |

### 3.2 Recommended Future File Split

```
scripts/core/TileMapPhysicsCore.cs          — _Ready, _Process, Initialize, GetEnvironment
scripts/systems/ColliderGenerator.cs        — GenerateColliders, IsEmptyBackgroundPixel
scripts/systems/SolidRegionMerger.cs        — MergeSolidRegions, SolidRectangle, RunKey
scripts/services/ViewportMapper.cs          — FindViewportMapping, coordinate helpers
scripts/ui/TileMapPhysicsDebugDraw.cs       — _Draw, DebugEdge
```

---

## Next Incremental Refactor Steps (Recommended Order)

1. **Convert to `partial class`** — Add `partial` keyword to `PbfSolver`,
   `FluidSimulator`, and `TileMapPhysics` without moving any code. Zero-risk.
2. **Extract `PbfConstants.cs`** — Move all `private const` tuning values into
   a separate `partial` file; no logic changes.
3. **Extract `AntiLagSystem` methods** from `FluidSimulator` — self-contained,
   few external dependencies.
4. **Extract `SetupRainHud` / `UpdateRainHud`** — UI-only, no solver state.
5. **Extract `ColliderGenerator`** from `TileMapPhysics` — well-bounded,
   tested by the existing collider-generation path.
6. After each step: run the Godot project, verify fluid simulation still runs
   and no `NullReferenceException` appears.

---

## Next Refactor Candidates

The following files were identified in a follow-up review pass as good targets after the original top-3 are addressed.

| Rank | File | Lines | Rationale |
|---|---|---:|---|
| 4 | `scripts/rendering/StatisticsGraph.cs` | 1 619 (historical) | Mixes graph layout constants, two distinct data series (top + bottom graph), drawing logic, and sample management. Each concern can become a separate partial-class file or a small helper class. |
| 5 | `scripts/simulation/FluidPolygonCollider.cs` | 900 (historical) | Contains wheel physics state (`FluidWheelState`), polygon collision geometry, and torque/energy calculations in one file. `FluidWheelState` is a standalone value type that belongs in `scripts/data/` or `scripts/simulation/particles/`. |
| 6 | `scripts/core/FrameProfiler.cs` | 769 (historical) | Combines timing-bucket logic, rolling-average computation, and GDScript-facing export properties. Splitting the math utilities from the Godot node lifecycle would make both halves unit-testable. |
| 7 | `scripts/rendering/WaterWheelVisual.cs` | 744 (historical) | Visual presentation of the water wheel is currently entangled with wheel-state queries. Extracting a thin `WheelStateReader` interface would decouple simulation data from rendering code. |
| 8 | `scripts/simulation/RainSystem.cs` | 642 (historical) | Rain spawn logic, transition easing, and anti-lag gate already exist as a separate file but still contain inline constants and pixel-grid lookups. Extracting constants to `SimulationConstants` and the pixel helpers to `PixelOccupancyGrid` would reduce coupling. |

### Recommended Split for `StatisticsGraph.cs`

```
scripts/rendering/StatisticsGraph.cs          — _Ready, _Process, node wiring (entry point only)
scripts/rendering/TopGraphRenderer.cs         — Top graph draw methods + GraphSample data
scripts/rendering/BottomGraphRenderer.cs      — Bottom graph draw methods + RainEnergySample data
scripts/data/GraphConstants.cs               — All const layout values (margins, widths, titles)
```

### Recommended Split for `FluidPolygonCollider.cs`

```
scripts/data/FluidWheelState.cs              — Pure value-type wheel state (angle, velocity, torque)
scripts/simulation/FluidPolygonCollider.cs   — Polygon geometry and collision only (reduced file)
```

### Recommended Split for `FrameProfiler.cs`

```
scripts/core/FrameProfiler.cs               — Godot Node2D lifecycle, export properties
scripts/utils/RollingAverageBuffer.cs       — Timing bucket math, rolling averages
```

### General Guidance

- All splits above should use `partial class` as a zero-risk first step.
- No public API changes are required for any of these splits.
- Priority order: `StatisticsGraph` (largest, clearest split boundary) then `FluidPolygonCollider` (unlocks cleaner wheel-upgrade implementation in roadmap item 4).
