# Refactor Plan — Top-3 Longest Scripts

Generated: 2026-08-15  
Status: **Phase 1 — Scaffolding only. No code moved yet.**

---

## Top-3 Longest Scripts

| Rank | File | Lines |
|------|------|-------|
| 1 | `Scripts/Fluid/PbfSolver.cs` | 3 611 |
| 2 | `Scripts/Fluid/FluidSimulator.cs` | 3 216 |
| 3 | `Scripts/Fluid/TileMapPhysics.cs` | 1 979 |

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
