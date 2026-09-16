---
name: planetgen-ui-refactor-state
description: UI refactored 2026-09-06 — 5-tab console, terrain params all on one
  page, layers switched via LayerTree list-table, MapMode deleted; node names
  are load-bearing (~90 GetNodeByName lookups); two migrations remain
metadata:
  node_type: memory
  type: project
  originSessionId: sess_6f8a7e0c-dbbb-4b75-8a10-07c8b249ac7a
---

The PlanetGeneration Godot UI was refactored on 2026-09-06 per Godot UI design guidelines, then further reorganized the same day (terrain-config consolidation). Current architecture: `MainLayout.tscn` root children are `Background`, `WorkspaceHBox` (HBox containing ONLY `MapWorkspace`, full-bleed with 12px margins), the **floating** `ConsolePanel`, collapse/summon tabs, and `MainMenu`. `ConsolePanel` (360px, anchored left 12→372, top 12→bottom −12) floats OVER the map and holds `ConsoleVBox` = HeaderPanel + static TabContainer; the **five** console tabs are static scene instances (`ConsoleParamsPage|ConsoleLayersPage|ConsoleAdvancedPage|ConsoleHistoryPage.tscn` + MainRightLore), titled 地形配置/图层/高级设置/世界与AI/历史统计 by `GeneratorControlsController.TabTitles` (controller `SetTabTitle` wins over `metadata/_tab_title`). All runtime Reparent hacks and `ApplyResponsiveLayout` manual offset math are gone. `MainHeaderController` sits on the header sub-scene. Bootstrap's `GetNodeOrNull("MainLayout/ConsolePanel/ConsoleVBox/...")` paths are now valid (ConsolePanel is a direct child of MainLayout).

2026-09-06 (late) floating console + minimap (user-directed, do not undo):
- **Console collapses/summons**: `ConsoleCollapseTab` ("⟨", 22×28 anchored to panel's right edge, vertical center) hides the panel; `ConsoleSummonTab` ("☰ 控制台", top-left 12,12) brings it back. Visibility centralized in `ApplyConsolePanelVisibility` (`scripts/Main.ConsolePanel.cs`); state `_consolePanelVisible` persisted as `console_panel_visible` via `UiPreferences.ConsolePanelVisible`. `ShowAdvancedSettingsPage` summons the panel before switching tabs. No slide animation (plain Visible toggle).
- **Minimap** (`scripts/Main.Minimap.cs`): `MinimapPanel` (PanelContainer, top-right of `PreviewPanel/MapRoot`, z_index 12, 220×124, hidden until a world texture exists) → `MinimapMargin` (6px) → `MinimapTexture` (TextureRect, linear filter, click/drag to pan) → `MinimapViewRect` (ReferenceRect with **`editor_only = false`** — default true draws nothing at runtime). Minimap image is a 512×256 bilinear `Duplicate()`+`Resize()` of the current layer image, rebuilt only when the source Texture instance changes. `UpdateMinimapViewportRect` runs from `_Process` with an epsilon dirty-check. The gradient/biome legend panels were moved down to `offset_top = 148` to clear the minimap.
- **Minimap math traps** (both bit once): (1) MapTexture letterboxes its texture inside the control, so texel(0,0) global = `controlXf * FitRectInside(texSize, control.Size).Position` and per-texel scale = `basisScale * painted.Size / texSize` — the transform basis length alone is NOT the texel scale (basis is 1 at zoom 1 regardless of letterbox). (2) Click-to-pan must divide the global delta by `_mapZoom`: `_mapAspect` is the zoomed ancestor, so `Position += globalDelta` overshoots by z.
- PrintWindow window captures of this game can silently drop/miss content (DPI-virtualization mismatch, cost ~1h of false "it's not rendering" debugging) — for UI ground truth use in-app `GetViewport().GetTexture().GetImage().SavePng(...)` from a temp timer instead.

2026-09-06 reorganization (user-directed, do not undo):
- **Terrain params live on one page** (`ConsoleParamsPage.tscn`, 4 groups: 世界框架/海洋与气候/山脉与造山/河流). Mountain preset + 4 mountain sliders + rivers toggle/river density moved here from Advanced page; the old collapsible MountainControl panel and its `mountain_control_expanded` persistence were deleted.
- **Layer switching = list table**: `LayerTree` (Tree, 2 columns 图层/类别, row select, `hide_root`) replaces the old card-button grid. The hidden `LayerOption` OptionButton stays as the data source; `BuildLayerTree`/`SyncLayerTreeSelection` in `Main.Options.LayerMode.cs`. Beware `TreeItem.Select()` re-signal (see [[godot-engine-gotchas]]).
- **MapMode deleted entirely** — enum, 视图模式 dropdown, `map_mode` persistence key, `UiPreferences.MapModeId` are gone; lore "模式" text now comes from `GetViewModeText()` inferring 地理/政区/奥术 from the active layer's category.
- Sim sliders 资源丰度/文明侵略/物种多样 moved to the 世界与AI tab (`MainRightLore.tscn` "世界法则" section) — they only affect ecology/civ sim + lore, not terrain. `GeneratorControlsController` no longer exposes them.

**Why:** Future edits must not undo this structure.

**How to apply:**
- Keep node names stable: ~90 name-based `GetNodeByName` (recursive FindChild) lookups across the `Main.*.cs` partials resolve by name, so renaming or duplicating scene nodes breaks them silently. (Verified 2026-09-06 that every GetNodeByName target exists in the layout scenes.)
- Wiring rule that emerged: controls owned by `GeneratorControlsController` (terrain page) are wired via its C# events when the controller exists, else wired directly on the nodes — never both, or handlers double-fire.
- Styling architecture (reworked 2026-09-06 for slider/select visibility): HSlider groove/fill styleboxes (10px track via 5px top/bottom content margins) and grabber icons (GradientTexture2D radial circles, accent-colored per theme) live ONLY in `ChineseTheme.tres`/`SteampunkTheme.tres` — the Params/Advanced scenes' local slider overrides were deleted, so new pages inherit them automatically. `ThemeManager.ApplyColors` still repaints the whole tree but now layers: panel=surface 17232f, `normal`(fields/input)=1d2b38, hover=293d4d, slider track=2a3a49, fill=accent; focus is a hairline ring (DrawCenter=false, 1px, accent 55%); plain Buttons (not OptionButton/CheckButton/CheckBox) get an accent-tinted bg+border; Labels named `Section*` get accent color (in addition to existing `*Title`/`*Value` accent); CheckButton/CheckBox styleboxes are skipped there because the themes define StyleBoxEmpty for them (Button-style inheritance leak, see [[godot-engine-gotchas]]).
- Offered-but-not-done follow-ups: (1) still replace the `ApplyColors` walk with proper theme type variants; (2) migrate `GetNodeByName` calls to `%` unique names (see [[godot-engine-gotchas]] for why % is scoped per scene).
- `scripts/UI/GeographyGeneratorUI.cs` + `scenes/GeographyGeneratorUI.tscn` are a standalone legacy demo scene, not part of the main UI — leave them alone.
