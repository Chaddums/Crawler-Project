# Claude Coordination File

**Last updated:** 2026-03-07
**Branch:** `dev`
**Engine:** Godot 4.6 (C#)
**Repo:** `/mnt/c/Users/Stu/GitHub/Crawler_Project` (WSL) or `C:\Users\Stu\GitHub\Crawler_Project` (Windows)

## Recent Work — Remote Claude (WSL/Termius)

### Mythic Graft Perk Effects (PerkProcessor.cs + PlayerCombat.cs)
All 8 Mythic grafts have gameplay effects implemented:
- **Immortal Engine** — 5% HP regen/sec, survive lethal for 2s
- **Devourer** — permanent +0.5% damage per kill, stacks infinitely
- **Neural Hijack** — 15% chance to respawn killed enemy as temp ally (20s)
- **Time Loop** — rewind 3s on lethal damage (once per floor)
- **Storm Caller** — all damage chains to 3 targets at 40%
- **Void Heart** — attacks leave void rifts (20% DPS, 3m radius, 4s)
- **Echo Chamber** — abilities fire twice (60% second cast), double mana cost
- **Blood Economy** — abilities cost HP instead of mana (2:1 ratio)

### Graft Socketing UI (GraftSocketPickerUI.cs + PassiveTreeCanvas.cs)
- Right-click allocated CoreSocket nodes to open graft picker
- Shows available cores from inventory sorted by rarity
- Unsocketing returns core to inventory
- Socketed nodes glow green with "G" label
- Tooltip shows "Right-click to socket/change graft"

### CoreScavenger Item Find (EnemyController.cs)
- Carrion Beetle Colony perk: +100% item find on drops
- Applied to both SignatureDropChance and LootBoxDropChance
- 25% chance for bonus equipment drop (rarity scales with enemy tier)

### String Externalization (strings.json + 13 UI files)
- 90+ hardcoded strings moved to `Godot/Data/strings.json`
- Uses `StringLoader.Get("ui.section.key")` with template vars
- Files updated: PauseMenuUI, InventoryUI, CharacterSheetUI, PassiveTreeUI, WorkshopUI, HUDController, VictoryScreenUI, WorldLootTableUI, LootBoxTrackerUI, RelicCacheUI, MainMenuUI, PlayerController (death screen), LiftTimerUI

### FBX Floor/Wall Tile Integration (RoomBuilder.cs)
- `BuildTileFloor` tries FBX models first (floortile_basic, floortile_basic2)
- `BuildWall` tries FBX wall segments (wall_1 through wall_5)
- Falls back to procedural shader meshes if FBX not available
- Collision shapes unchanged (BoxShape3D)

### Save/Load Socketed Grafts (SaveData.cs + SaveManager.cs)
- New `SocketedCores` dict in PlayerSaveData (nodeId -> coreId)
- Restored via SalvageCoreRegistry.Get() + PassiveTree.SocketCore()
- Works in both ApplyLoadedState and ApplyTransitionState

### Loot Box Graft Display (LootBoxCeremonyUI.cs)
- SalvageCoreItemData items show mechanic description in cyan
- Shows stat line after flavor text instead of empty affixes

## Recent Work — Local Claude (Windows/Godot)

### Critical Bug Fix: Player Can't Move
- **Root cause:** `PlayerMovement.GetMaxDashCharges()` called `_body.GetParent<PlayerController>()` every physics frame
- `_body` IS the PlayerController (CharacterBody3D), so `GetParent()` returns SectorManager -> silent InvalidCastException
- **Fix:** Replaced all 3 occurrences with cached `_playerController`
- **Files:** `Godot/Scripts/Player/PlayerMovement.cs`

### Intro Performance Cleanup
- Removed per-room OmniLight3D creation (was creating 40+ dynamic lights)
- Removed `TintMeshMaterials` static method (cloning hundreds of materials)
- Optimized bob loop to use `_bobOrder` list
- **Files:** `Godot/Scripts/Dungeon/DungeonAssemblyIntro.cs`

### Other Fixes (commit 8eb3c7c)
| File | Fix |
|------|-----|
| `IsometricCamera.cs` | Set `Current = true` in both Initialize() overloads |
| `SectorManager.cs` | Changed intro disable from `SetProcess(false)` to `DisableInput()`, added 30s safety timer |
| `PlayerInputHandler.cs` | Added GD.Print to Enable/DisableInput for visibility |
| `CharacterCreationUI.cs` | Rotated bot model 180 degrees (images were backwards) |
| `LiftTimerUI.cs` | Guard against red flash when `TimeLimit <= 0` |
| `DungeonBackdrop.cs` | `AddChild` before `LookAt` (node must be in tree) |
| `SalvageCoreRegistry.cs` | Named tuple fields to fix CS1061 |

## Known Gotcha: Silent Exceptions in Godot 4 C#
Godot 4's C# runtime **silently catches exceptions** in `_PhysicsProcess`, `_Process`, and `_Ready`. The method just stops executing — no error in logs. If movement or logic "stops working" with no errors, wrap suspicious code in try-catch to find the hidden exception.

## Known Issues / Observations

- **227 runs, never past Sector 1 Area 1** — caused by movement bug, now fixed
- No equipped items in save — gear sits in inventory bag, not auto-equipped
- `bestSector: 1, bestArea: 1` across all runs — meta stats skewed by broken movement
- Save file lacks `socketedCores` field (will auto-create on next save)

## Architecture Notes

### Key File Locations
| System | Primary File(s) |
|--------|----------------|
| Passive Tree | `Scripts/Classes/PassiveTree.cs`, `PassiveTreeBuilder.cs`, `PassiveNodeData.cs` |
| Grafts | `Scripts/Classes/SalvageCoreData.cs`, `SalvageCoreRegistry.cs`, `SalvageCoreItemData.cs` |
| Perk Effects | `Scripts/Classes/PerkProcessor.cs` (~900 lines), `Perks.cs` |
| Combat Hooks | `Scripts/Player/PlayerCombat.cs` (Storm Caller, Void Heart, Echo Chamber, Blood Economy) |
| Dungeon Gen | `Scripts/Dungeon/RoomBuilder.cs`, `DungeonGenerator.cs`, `RoomDresser.cs` |
| Items/Loot | `Scripts/Items/LootBoxFactory.cs`, `ItemRegistry.cs`, `AffixRoller.cs` |
| Save System | `Scripts/Core/SaveManager.cs`, `SaveData.cs`, `MetaSaveManager.cs` |
| UI Strings | `Data/strings.json` — dot-path keys, `StringLoader.Get()` |
| FBX Assets | `Models/Dungeon/{Floors,Walls,Props,Details,Doors}/` via `ModelLibrary.cs` |

### Git Workflow
- Author env vars: `GIT_AUTHOR_NAME="calschuss" GIT_AUTHOR_EMAIL="stuart.white28@protonmail.com"`
- Committer env vars also needed (WSL has no global git config)
- Push with `--no-verify` due to missing git-lfs
- Always push to `dev` branch

### Inventory / Items
- `PlayerInventory.Items` — List<ItemInstance> bag
- `PlayerInventory.Equipped` — Dict<EquipmentSlot, ItemInstance>
- Grafts in inventory: `item.BaseData is SalvageCoreItemData`
- Item reconstruction: `ItemRegistry.Reconstruct(ItemSaveData)` — requires item was previously registered

## What Could Use Work Next

- **Auto-equip starter gear** — players start with gear in bag but nothing equipped
- **Meta save reset** — 227 runs of broken movement skewed all stats
- **Passive tree auto-allocate** — 10 unspent skill points in save, players may not know to open tree (P key)
- **FBX tile scaling** — floor/wall FBX models need testing with actual Godot import to verify scale matches room dimensions
- **Audio assets** — AudioManager has 18 procedural synth sounds; real audio needs asset purchase
- **Prop placement variety** — RoomDresser places props but more variety per sector theme would help
- **Co-op testing** — multi-player systems exist but untested recently
