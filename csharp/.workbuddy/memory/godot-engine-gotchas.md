---
name: godot-engine-gotchas
description: Godot 4.x facts learned here — TreeItem.Select() re-emits ItemSelected
  (infinite recursion trap); metadata/_tab_title editor-only; % unique names
  scoped per scene owner
metadata:
  node_type: memory
  type: reference
  originSessionId: sess_6f8a7e0c-dbbb-4b75-8a10-07c8b249ac7a
---

- **`TreeItem.Select()` re-emits `Tree.ItemSelected`** — programmatically selecting an item inside the handler of that same signal causes infinite recursion (real Stack Overflow hit in the layer list-table). Always guard the handler with `if (newId == currentId) return;` before re-selecting. Relates to [[planetgen-ui-refactor-state]].
- Tree C# API traps: selectable state is a method `SetSelectable(column, bool)` (no `Selectable` property); `SetColumnCustomMinimumWidth(col, int)` takes an int, not float; `select_mode` 0 = single cell, 1 = whole-row highlight (row mode is the right choice for a list-table UI). Tree has no `title` property — set column headers via `SetColumnTitle(i, text)` + `ColumnTitlesVisible`.
- `metadata/_tab_title` in a .tscn only affects the editor; at runtime a TabContainer shows child node names until `SetTabTitle(i, title)` is called (done in `GeneratorControlsController._Ready`). Relates to [[planetgen-ui-refactor-state]].
- `GetNode("%Name")` resolves unique names only within the calling node's own scene (owner), so cross-scene % lookups fail. That is why the Main.* partials use name-based `FindChild` lookups instead — they survive any re-parenting as long as names stay unique.
- Node-header `unique_id=...` attributes seen in this project's .tscn files are non-standard but tolerated (ignored) by Godot 4.7.
- **HSlider draws its track at the stylebox's minimum size** — a StyleBoxFlat with no content margins yields a 0-px-tall track AND 0-px fill, leaving only the grabber icon visible (the "invisible sliders" bug of 2026-09-06). Always give `slider`/`grabber_area`/`grabber_area_highlight` equal top/bottom content margins (5px → 10px track).
- **CheckButton/CheckBox inherit Button's styleboxes** via theme type fallback — a colored `Button/styles/normal` in a custom Theme paints a box behind the switch icon. Define `CheckButton/styles/normal|hover|pressed|disabled = StyleBoxEmpty` (and CheckBox's) in the theme to break the inheritance.
- The default `focus` StyleBoxFlat draws an opaque plate OVER the focused control; restyle to `DrawCenter = false` + 1px semi-alpha border for a hairline focus ring.
