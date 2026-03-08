# Claude Coordination File

Last updated: 2026-03-07
Updated by: Claude (WSL/remote via Termius)

## Project State

**Branch:** dev
**Engine:** Godot 4.6 (C#)
**Repo:** /mnt/c/Users/Stu/GitHub/Crawler_Project (WSL) or C:\Users\Stu\GitHub\Crawler_Project (Windows)

## Recently Completed (This Session)

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

## Known Issues / Observations

### From Save File Analysis (2026-03-07)
- **227 runs, never past Sector 1 Area 1** — root cause was PlayerMovement bug (fixed in 8eb3c7c by local Claude)
- No equipped items in save — gear sits in inventory bag, not auto-equipped
- Save file lacks `socketedCores` field (predates our change, will auto-create on next save)
- `bestSector: 1, bestArea: 1` across all 227 runs — meta stats need a reset or the movement fix should let runs progress now

### Bug Fixes by Local Claude (commit 8eb3c7c)
- **PlayerMovement** — `GetMaxDashCharges()` called `_body.GetParent<PlayerController>()` but `_body` IS the PlayerController. Used cached `_playerController` instead.
- **IsometricCamera** — set `Current = true` in Initialize()
- **SectorManager intro** — use DisableInput/EnableInput instead of SetProcess
- **DungeonAssemblyIntro** — removed per-room OmniLight3D (perf)
- **DungeonBackdrop** — AddChild before LookAt
- **CharacterCreationUI** — rotate bot model 180 degrees
- **LiftTimerUI** — guard against red flash when TimeLimit is 0
- **SalvageCoreRegistry** — named tuple fields to fix CS1061

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
