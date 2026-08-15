# General Roadmap

This document describes the planned features and improvements for Infinite Water, ordered by priority. It replaces the earlier UI Implementation Plan and now covers the full scope of upcoming work.

---

## Asset Notes

Buttons, symbols, and the rain amount display use sprites from the pixel-button tileset located at:

```
idle-water/pixelbuttons/
```

Specifically:
- **Tilt button and directional arrow** — `idle-water/pixelbuttons/00.png`
- All other buttons and HUD symbols — additional sheets in the same `pixelbuttons` folder.

When adding new HUD elements, pull assets from these tilesets first before introducing external art.

---

## Completed Work

The following phases from the earlier UI plan have been addressed in previous PRs:

- Phase 1 — Central UI state/controller (`UiStateController`)
- Phase 2 — Graph togglable with sprite button
- Phase 3 — Rain amount display with sprite
- Phase 4 — Settings button and settings window
- Phase 5 — Graph/settings mutual exclusivity
- Phase 6 — Money system + Sell Energy button

---

## Next Roadmap Points

### 1. Implement tilt and tilt settings

Add gravity-tilt mechanics so the fluid simulation responds to a configurable tilt angle.

- Add a tilt button and a directional arrow to the HUD using assets from `idle-water/pixelbuttons/00.png`.
- Expose a tilt angle setting in the settings panel (e.g. a slider from -45 to +45 degrees).
- Pass the tilt angle to the solver as a modified gravity direction vector.
- Acceptance criteria: tilting the level causes water to flow in the expected direction; resetting tilt returns water to neutral gravity.

### 2. Implement sound and sound settings

Add ambient and event-driven audio to enrich the experience.

- Integrate a sound manager node that owns all audio playback.
- Add a sound settings section to the existing settings panel (master volume, music volume, SFX volume).
- Wire water sounds to particle density thresholds and wheel rotation events.
- Acceptance criteria: sounds play at startup; volume sliders update audio in real time; muting is persisted across sessions.

### 3. Start with one wheel; make 5 additional wheels unlockable

Scope the initial experience to a single water wheel and add a progression unlock system.

- Default scene ships with exactly one active wheel.
- Define an unlock table for five additional wheels (e.g. unlocked by accumulated money milestones).
- Each unlocked wheel appears in the scene and contributes to energy production.
- Acceptance criteria: new game has one wheel; wheels unlock at the correct money thresholds; unlocked wheels persist across sessions.

### 4. Implement wheel upgrades

Allow players to spend money to improve wheel performance.

- First upgrade: increase wheel friction (higher drag on passing particles => more torque extracted).
- Expose an upgrade button per wheel in the UI (disabled when unaffordable).
- Apply the upgraded friction constant in `FluidPolygonCollider` / `WaterWheelManager` when the upgrade is active.
- Acceptance criteria: purchasing friction upgrade visibly increases angular velocity for the same rain rate; upgrade state persists across sessions.

### 5. Fix water still accumulating somewhere

Diagnose and resolve the persistent water accumulation bug.

- **Debug aid**: temporarily set the water simulation render layer in front of all other textures so accumulation spots are immediately visible.
- Identify the source (boundary leak, sleep logic not waking stuck particles, pixel-occupancy grid off-by-one, etc.).
- Apply a targeted fix and restore the normal render order.
- Acceptance criteria: long-running sessions (10+ minutes) do not show growing water deposits in unexpected locations.

### 6. Add sound and sound settings *(see also item 2)*

> This item is intentionally listed separately to capture any sound work deferred from item 2 (e.g. wheel-specific audio, rain intensity audio cues, or UI sound effects added later in the project).

- Complete any remaining audio integration not addressed in item 2.
- Ensure all new sounds added for tilt, unlocks, and upgrades (items 1-4) are covered.

### 7. General optimization

Profile and improve performance for lower-end devices.

- Run the Godot profiler and identify the heaviest per-frame operations.
- Evaluate and apply spatial-hash tuning, sleep-threshold adjustments, and draw-call batching.
- Consider reducing worst-case particle count or adding a quality setting.
- Acceptance criteria: stable 60 fps on a mid-range Android device with default rain settings; no regressions in fluid behavior.

---

## Suggested State Model (reference)

Use a dedicated UI controller (`UiStateController`) that owns:
- Panel state: `isGraphOpen`, `isSettingsOpen`
- Public intent methods: `ToggleGraph()`, `ToggleSettings()`, `OpenGraph()`, `OpenSettings()`, `CloseAllPanels()`

Use a dedicated economy model/service for gameplay values:
- `energy`, `money`
- `SellEnergy(amount)` — conversion + validation logic

---

## Basic Manual QA Checklist

- Graph and settings panels cannot be open simultaneously.
- Rain display sprite is visible and updates correctly.
- `Sell Energy` increases money and decreases energy per conversion rules.
- Tilt slider changes water flow direction in real time.
- Volume sliders update audio without restarting the scene.
- Wheel unlock milestones trigger at correct money values.
- Wheel upgrade persists after closing and reopening the app.
- No unexpected water accumulation after a 10-minute session.
