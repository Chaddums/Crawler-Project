# Claude Coordination File

**Last updated:** 2026-03-07
**Branch:** `dev`
**Last commit:** `8eb3c7c` — Fix player movement bug, intro performance, and multiple UI fixes

## What Was Done (This Session — Machine A)

### Critical Bug Fix: Player Can't Move
- **Root cause:** `PlayerMovement.GetMaxDashCharges()` called `_body.GetParent<PlayerController>()` every physics frame
- `_body` IS the PlayerController (CharacterBody3D), so `GetParent()` returns SectorManager → **silent InvalidCastException** every frame
- Godot 4 C# silently swallows exceptions in `_PhysicsProcess` — no error in logs, just kills the rest of the method
- **Fix:** Replaced all 3 occurrences of `_body.GetParent<PlayerController>()` with `_playerController` (already cached via `GetParentOrNull`)
- **Files:** `Godot/Scripts/Player/PlayerMovement.cs`

### Intro Performance Cleanup
- Removed per-room OmniLight3D creation (was creating 40+ dynamic lights during intro)
- Removed `TintMeshMaterials` static method (was cloning hundreds of materials)
- Optimized bob loop to use `_bobOrder` list instead of dictionary iteration
- **Files:** `Godot/Scripts/Dungeon/DungeonAssemblyIntro.cs`

### Other Fixes
| File | Fix |
|------|-----|
| `IsometricCamera.cs` | Set `Current = true` in both Initialize() overloads |
| `SectorManager.cs` | Changed intro disable from `SetProcess(false)` to `DisableInput()`, added 30s safety timer, `_introFinished` guard |
| `PlayerInputHandler.cs` | Added GD.Print to Enable/DisableInput for visibility |
| `CharacterCreationUI.cs` | Rotated bot model 180° (images were backwards) |
| `LiftTimerUI.cs` | Guard against red flash when `TimeLimit <= 0` |
| `DungeonBackdrop.cs` | `AddChild` before `LookAt` (node must be in tree) |
| `SalvageCoreRegistry.cs` | Named tuple fields to fix CS1061 |

## Files Modified (Code Only)
```
Godot/Scripts/Camera/IsometricCamera.cs
Godot/Scripts/Classes/SalvageCoreRegistry.cs
Godot/Scripts/Dungeon/DungeonAssemblyIntro.cs
Godot/Scripts/Dungeon/DungeonBackdrop.cs
Godot/Scripts/Dungeon/SectorManager.cs
Godot/Scripts/Player/PlayerInputHandler.cs
Godot/Scripts/Player/PlayerMovement.cs
Godot/Scripts/UI/CharacterCreationUI.cs
Godot/Scripts/UI/LiftTimerUI.cs
```

## Known Gotcha: Silent Exceptions in Godot 4 C#
Godot 4's C# runtime **silently catches exceptions** in `_PhysicsProcess`, `_Process`, and `_Ready`. The method just stops executing — no error in logs. If movement or logic "stops working" with no errors, wrap the suspicious code in try-catch to find the hidden exception.

## What Still Needs Attention
- **Playtest the movement fix** — player should now move with WASD after spawning
- **Verify intro plays correctly** — rooms should assemble, camera should sweep, then player input re-enables
- Many `.import` files changed (Godot regenerated UUIDs) — these are committed and harmless
- Remaining modified files in git status are `.import` files from the other machine opening the project

## Current State
- Build compiles with 0 errors
- On `dev` branch, pushed to origin
- Game should be playable: spawn → intro → move with WASD → combat
