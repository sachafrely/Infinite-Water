# UI Implementation Plan

This plan sequences the requested UI work with minimal coupling and clear acceptance gates.

## Phase 1 — Introduce central UI state/controller

### Goal
Create one place that owns panel visibility and shared UI interaction rules.

### UI elements involved
- Graph toggle button (sprite button)
- Graph panel/window
- Settings button (sprite button)
- Settings window/panel

### Data/state needed
- `bool isGraphOpen`
- `bool isSettingsOpen`
- Optional enum for active exclusive panel (e.g. `None`, `Graph`, `Settings`)
- References to graph panel node, settings panel node, and both buttons

### Interaction rules
- Controller methods handle all panel open/close transitions.
- UI elements should not directly manipulate each other; they call controller actions.

### Definition of done / acceptance criteria
- A single controller node/script drives visibility for graph and settings panels.
- Existing scenes continue to load without runtime errors.
- No duplicated open/close logic spread across unrelated scripts.

## Phase 2 — Graph togglable with sprite button

### Goal
Allow opening/closing the graph via a sprite-based button from the start.

### UI elements involved
- Graph button using provided sprite assets (normal/hover/pressed if available)
- Graph panel/window

### Data/state needed
- Button node reference
- Graph panel reference
- Controller state from Phase 1

### Interaction rules
- Pressing graph button toggles graph visibility.
- If settings is open, opening graph closes settings first (mutual exclusivity handled by controller).

### Definition of done / acceptance criteria
- Graph opens and closes reliably with repeated clicks.
- Sprite visuals are used for the button (not temporary text-only UI).
- Graph can be opened from default game state without errors.

## Phase 3 — Rain amount display with sprite

### Goal
Display rain amount with the provided sprite-based UI element.

### UI elements involved
- Rain icon/background sprite
- Rain amount label/value overlay (or sprite digits if desired later)

### Data/state needed
- Source rain value (current percent/amount)
- Formatting rule (e.g. integer percent)
- Node refs for rain icon and value text/visual

### Interaction rules
- Rain display updates whenever rain amount changes (or every frame if already done elsewhere).
- Display remains visible regardless of graph/settings panel state.

### Definition of done / acceptance criteria
- Rain sprite is visible in HUD.
- Value reflects runtime rain amount correctly.
- No flicker, stale values, or null-reference errors on startup.

## Phase 4 — Settings button and settings window

### Goal
Add a sprite-based settings button that opens/closes a settings window.

### UI elements involved
- Settings sprite button
- Settings panel/window container

### Data/state needed
- Settings panel reference
- Controller state from Phase 1
- Optional settings model for values shown in panel

### Interaction rules
- Pressing settings button toggles settings panel.
- If graph is open, opening settings closes graph first (controller-managed exclusivity).

### Definition of done / acceptance criteria
- Settings panel opens/closes from the button.
- Sprite assets are used for settings button.
- Opening settings from all normal game states is stable.

## Phase 5 — Enforce graph/settings mutual exclusivity

### Goal
Guarantee graph and settings cannot be open at the same time.

### UI elements involved
- Graph button + panel
- Settings button + panel

### Data/state needed
- Centralized exclusive-panel state in controller
- Single helper methods such as `OpenGraph()`, `OpenSettings()`, `CloseAllPanels()`

### Interaction rules
- `OpenGraph()` always closes settings before opening graph.
- `OpenSettings()` always closes graph before opening settings.
- Closing one panel does not auto-open the other.

### Definition of done / acceptance criteria
- Manual toggling never results in both panels visible together.
- Rapid click sequences still preserve exclusivity.
- Panel state remains consistent after scene reload if persistence is used.

## Phase 6 — Money system + Sell Energy button

### Goal
Implement a money progression loop tied to generated energy and a `Sell Energy` action.

### UI elements involved
- Money display (label/sprite-backed HUD element)
- `Sell Energy` button
- Optional confirmation/feedback text

### Data/state needed
- `float` or `double energy`
- `int` or `double money`
- Conversion rule (e.g. money gained per sold energy unit)
- Sell constraints (minimum sell amount, sell-all vs fixed chunk)

### Interaction rules
- Clicking `Sell Energy` reduces stored energy and increases money per conversion rules.
- Disable button or no-op when energy is insufficient.
- HUD updates immediately after a sell action.

### Definition of done / acceptance criteria
- Money value persists during play session and updates on sell.
- Energy decreases correctly when sold.
- Button behavior is deterministic and guarded against invalid sells.

## Suggested state model

Use a dedicated UI controller (e.g. `UiStateController`) that owns:
- Panel state: `isGraphOpen`, `isSettingsOpen`
- Optional derived property: `activeExclusivePanel`
- Public intent methods: `ToggleGraph()`, `ToggleSettings()`, `OpenGraph()`, `OpenSettings()`, `CloseGraph()`, `CloseSettings()`

Use a dedicated economy model/service for gameplay values:
- `energy`
- `money`
- `SellEnergy(amount)` method containing conversion + validation logic

## Sprite asset integration checklist

- Import graph button sprites and verify filter/compression settings match pixel-art style.
- Import settings button sprites with consistent sizing/anchor strategy.
- Import rain amount sprite/icon and verify layering over HUD background.
- Confirm pressed/hover/disabled states are wired where available.
- Verify all sprite references are assigned in scene/prefab and survive reload.

## Basic manual QA checklist

- Graph button toggles graph on/off repeatedly without errors.
- Settings button toggles settings on/off repeatedly without errors.
- Opening graph while settings is open closes settings first.
- Opening settings while graph is open closes graph first.
- Rain display sprite is visible and rain value updates correctly.
- `Sell Energy` increases money and decreases energy according to conversion rules.
- `Sell Energy` is blocked or safely ignored when energy is insufficient.
- UI still behaves correctly after pausing/resuming or scene reload (if applicable).
