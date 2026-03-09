# Claude Coordination File

**Last updated:** 2026-03-08
**Branch:** `dev`
**Engine:** Godot 4.6 (C#)
**Repo:** `/mnt/c/Users/Stu/GitHub/Crawler_Project` (WSL) or `C:\Users\Stu\GitHub\Crawler_Project` (Windows)

## How To Use This File
Both local (Windows/Godot) and remote (WSL/Termius) Claude instances should read and update this file for handoffs. Check the "Recent Work" sections to avoid duplicating effort.

## Architecture Quick Reference

### Key File Locations
| System | Primary File(s) |
|--------|----------------|
| Game Flow | `Scripts/Core/GameManager.cs` (autoload, state machine) |
| Player | `Scripts/Player/PlayerController.cs`, `PlayerMovement.cs`, `PlayerCombat.cs`, `PlayerStats.cs` |
| Passive Tree | `Scripts/Classes/PassiveTree.cs`, `PassiveTreeBuilder.cs`, `PassiveNodeData.cs` |
| Grafts | `Scripts/Classes/SalvageCoreData.cs`, `SalvageCoreRegistry.cs`, `SalvageCoreItemData.cs` |
| Perk Effects | `Scripts/Classes/PerkProcessor.cs` (~900 lines), `Perks.cs` |
| Combat Hooks | `Scripts/Player/PlayerCombat.cs` (Storm Caller, Void Heart, Echo Chamber, Blood Economy) |
| Dungeon Gen | `Scripts/Dungeon/DungeonGenerator.cs`, `RoomBuilder.cs`, `RoomLayoutLibrary.cs`, `RoomDresser.cs` |
| Items/Loot | `Scripts/Items/LootBoxFactory.cs`, `ItemRegistry.cs`, `AffixRoller.cs`, `BaseItemPool.cs` |
| Save System | `Scripts/Core/SaveManager.cs`, `SaveData.cs`, `MetaSaveManager.cs` |
| UI Strings | `Data/strings.json` — dot-path keys, `StringLoader.Get()` |
| FBX Assets | `Models/Dungeon/{Floors,Walls,Props,Details,Doors}/` via `Assets/ModelLibrary.cs` |
| Editor Suite | `Scripts/Editor/EditorManager.cs` (F12), 9 modules in `Editor/Modules/` |
| Debug Console | `Scripts/Debug/DebugMenu.cs` (~ key) |
| Registry Overrides | `Scripts/Utils/RegistryOverrides.cs` — loads JSON overrides into live registries |
| Constants | `Scripts/Core/Constants.cs` — physics layers, gameplay defaults, scene paths |

### Registries (all static, init on startup)
AbilityRegistry (28), EnemyRegistry (16), BossRegistry (5), BotFrameRegistry (6), AffixRegistry (50+), ConsumableRegistry (20+), RelicRegistry (15+), SalvageCoreRegistry (15+), CompanionRegistry, AchievementRegistry (~25), PerkRegistry (9), SectorDataRegistry (5)

All registries call `RegistryOverrides.Apply*()` at end of Initialize() to load JSON overrides from `Data/`.

### Autoloads (project.godot)
GameManager (Main.tscn), DebugTestRunner, AutoPlayer, DebugMenu, PlaytestReporter, ErrorCatcher, EditorManager

### Physics Layers
1=Default, 2=Player, 3=Companion, 4=Enemy, 5=PlayerProjectile, 6=EnemyProjectile, 7=Interactable, 8=Ground

### Inventory / Items
- `PlayerInventory.Items` — List<ItemInstance> bag (30 slots)
- `PlayerInventory.Equipped` — Dict<EquipmentSlot, ItemInstance>
- 10 slots: MainHand, OffHand, Chest, Head, Feet, Hands, Back, Amulet, Ring1, Ring2
- Grafts in inventory: `item.BaseData is SalvageCoreItemData`
- Item reconstruction: `ItemRegistry.Reconstruct(ItemSaveData)`

### Git Workflow
- Author env vars: `GIT_AUTHOR_NAME="calschuss" GIT_AUTHOR_EMAIL="stuart.white28@protonmail.com"`
- Committer env vars also needed (WSL has no global git config)
- Push: `git -c core.hooksPath=/dev/null push origin dev` (LFS workaround)
- Always push to `dev` branch

## Known Gotcha: Silent Exceptions in Godot 4 C#
Godot 4's C# runtime **silently catches exceptions** in `_PhysicsProcess`, `_Process`, and `_Ready`. The method just stops executing — no error in logs. Wrap suspicious code in try-catch to find hidden exceptions.

---

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

### String Externalization (strings.json + 13 UI files)
- 90+ hardcoded strings moved to `Data/strings.json`
- Uses `StringLoader.Get("ui.section.key")` with template vars

### FBX Floor/Wall Tile Integration (RoomBuilder.cs)
- `BuildTileFloor` tries FBX models first, falls back to procedural shader meshes
- `BuildWall` tries FBX wall segments (wall_1 through wall_5)

### Save/Load Socketed Grafts (SaveData.cs + SaveManager.cs)
- `SocketedCores` dict in PlayerSaveData (nodeId -> coreId)
- Restored via SalvageCoreRegistry.Get() + PassiveTree.SocketCore()

## Recent Work — Local Claude (Windows/Godot)

### In-Game Editor Suite (F12)
9 editor modules with live editing, undo/redo, and JSON persistence:
- **Balance** — all 6 sub-tabs (abilities, enemies, equipment, consumables, relics, bot frames)
- **Dungeon** — 2D sector map + 3D room viewer with object selection/transform editing
- **Assets** — 3D asset preview with collision configuration
- **Characters**, **Sectors**, **Strings**, **VFX**, **Sound**, **Bugs**

### Registry Override System (RegistryOverrides.cs)
- Balance editor saves to JSON → registries load overrides on next init
- All 6 registries wired: Abilities, Enemies, Equipment, Consumables, Relics, BotFrames
- BalanceEditor.ApplyToRegistry() patches live objects immediately on save

### Anti-Aliasing & Z-Fighting Fixes
- MSAA 4x + FXAA in project.godot
- Camera near/far: 0.5-200 (from 0.05-4000) for 160x better depth precision
- DungeonBackdrop grid line Y offsets increased
- Intro glow platforms moved further from floor
- Health bar fill quad Z offset 10x increase

### Critical Bug Fix: Player Can't Move
- `PlayerMovement.GetMaxDashCharges()` called `GetParent<PlayerController>()` on PlayerController itself
- Silent InvalidCastException killed the method every physics frame
- Fixed by using cached `_playerController` reference

### Intro Performance Cleanup
- Removed per-room OmniLight3D (was 40+ dynamic lights)
- Removed `TintMeshMaterials` (cloning hundreds of materials)

---

## What Could Use Work Next

- **Auto-equip starter gear** — players start with gear in bag but nothing equipped
- **Meta save reset** — 227 runs of broken movement skewed all stats
- **Passive tree auto-allocate** — unspent skill points, players may not know P key
- **FBX tile scaling** — floor/wall FBX models need testing with actual Godot import
- **Audio assets** — AudioManager has 18 procedural synth sounds; needs real audio
- **Prop placement variety** — RoomDresser needs more variety per sector theme
- **Co-op testing** — multi-player systems exist but untested recently
- **TAA consideration** — if MSAA+FXAA not sufficient for remaining aliasing
