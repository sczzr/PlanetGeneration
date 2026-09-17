---
name: godot-workflow-commands
description: Build/run/verify commands for the Godot C# project — explicit
  csproj, godot binary location, headless smoke test, window-only screenshots
metadata:
  node_type: memory
  type: project
  originSessionId: sess_6f8a7e0c-dbbb-4b75-8a10-07c8b249ac7a
---

Workflow for the PlanetGeneration Godot C# project (workspace root `F:\GameDev\Original\PlanetGeneration\procgenesis_local\csharp`):

- Build with `dotnet build PlanetGeneration.csproj` — bare `dotnet build` fails with MSB1011 because the folder has both `csharp.sln` and the csproj.
- Godot binary is at `/c/Users/shawn/bin/godot` (v4.7.2 mono, even though project.godot says features 4.6).
- Smoke test without a window: `timeout 90 godot --headless res://scenes/Main.tscn --quit-after 180` (run from the csharp dir after building) — exercises scene parse, `_Ready` wiring, and full world-gen; grep output for `error|exception|overflow|stack|failed`. This catches runtime bugs a clean `dotnet build` misses — e.g. it exposed a signal-recursion Stack Overflow in the layer Tree on 2026-09-06 ([[godot-engine-gotchas]]). CAVEAT: `--quit-after N` is N rendered FRAMES (~60/s), not operations — 180 frames (~3s) aborts generation mid-run on maps ≥512x256 and looks like a crash/silent truncation; use `--quit-after 6000` for 1024x512. To benchmark other map sizes headless, temporarily set `const int selectedIndex` in `SetupMapSizeOptions` (scripts/Main.Options.Presets.cs) — the startup preset overrides the [Export] MapWidth/MapHeight defaults; revert both after.
- Visual check: launch windowed in background and capture ONLY the game window via PowerShell `PrintWindow` (flag 2 = PW_RENDERFULLCONTENT) on the godot process's MainWindowHandle. Do NOT take fullscreen desktop screenshots — the user may have private content on screen (a fullscreen capture once caught their browsing), and they often use the machine while the game runs, so windows may close or be interacted with mid-verification.
- Finding the right window: several Godot editor instances are usually open; the process name is `Godot_v4.7.2-stable_mono_win64` (plain `Get-Process godot` matches nothing). Snapshot godot pids before launch, launch, diff after — the new pid is the game; kill only that pid.
- `Main.tscn` opens behind the MainMenu title overlay (行星志·远古纪元). For screenshots, temporarily add `GetTree().CreateTimer(0.5).Timeout += Close;` at the top of `MainMenu._Ready` (scripts/UI/MainMenu.cs), build, capture, then REVERT the line and rebuild. To verify the Chinese theme without clicking 主题, temporarily flip `ThemeManager._currentTheme` default to `ThemeType.Chinese`, capture, revert.
