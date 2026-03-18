# Claude Coordination File

**Last updated:** 2026-03-17
**Branch:** `dev`
**Engine:** Godot 4.6 (C#)
**Repo:** `/mnt/c/Users/Stu/GitHub/Crawler_Project` (WSL) or `C:\Users\Stu\GitHub\Crawler_Project` (Windows)

## Active Claude Instances

| Instance | Environment | Status | Current Focus |
|----------|------------|--------|---------------|
| Claude A | (original) | Active | Vine Logic TD alpha — harvester, enemy spawning, player character buildout, integration polish |
| Claude B | Windows/Godot_TD | **Active (joined 2026-03-17)** | Vine Logic TD — model sizing & theme compatibility |

## How To Use This File
All Claude instances should read and update this file for handoffs. Check the "Recent Work" sections and "Active Claude Instances" table to avoid duplicating effort. Update your row when starting/finishing work.

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

### Asset Overhaul (Phase 1-4)
Complete integration of downloaded asset packs from `_downloads/`:

**Phase 1: Enemy Models** — Spider Mech → `rust_titan.fbx`, Grunt Robot → `junk_lurker.fbx`, Quaternius Robot → `patch_bot.fbx`

**Phase 2: POLYGON Dungeon Pack** (781 prefabs, 795 meshes, 43 materials)
- Copied full pack to `Assets/PolygonDungeon/` (90MB)
- ModelLibrary: added `_extraScanFolders` array scanning 10 POLYGON prefab directories
- ModelLibrary: 30+ aliases mapping game IDs (barrel, crate, wall_1, etc.) → POLYGON `.tscn` prefabs
- RoomDresser: expanded prop variety per room type (braziers, banners, chains, rugs, tech props)
- POLYGON uses `.res` ArrayMesh files + `.tres` materials, referenced by `.tscn` scenes

**Phase 3: POLYGON Mech Pack** (141 FBX, 44 textures)
- Extracted to `Assets/PolygonMech/`
- 12 sci-fi weapons copied to `Models/Weapons/`: arc_rifle, assault_rifle, gatling_gun, hammer, war_hammer, hand_cannon, katana, power_rifle, rocket_launcher, shotgun, energy_sword
- 4 mech props copied to `Models/Dungeon/Props/`: charging_cables, coupling, mech_cockpit

**Phase 3b: AmbientCG PBR Materials** (31 materials, 254 textures)
- Extracted to `Assets/Materials/AmbientCG/`
- Metals, concrete, paving stones, ground, industrial rubble, decals

**Phase 4: Sonniss Audio** (in progress)
- 10 audio packs being extracted to `Assets/Audio/Sonniss/`
- Audio directory structure created: `Audio/{SFX,Music,Abilities,Ambient,Voice}/`
- audio.json manifest ready to wire up once files are curated

### AXIS Intro Laser Sweep (DungeonAssemblyIntro.cs + AXISPresence.cs)
- Replaced hand-pointing room reveal with opaque cone laser sweep from under AXIS body
- Quaternion slerp rotation (direction-agnostic, replaces broken Euler approach)
- AXIS 3x scale, Y=15, hands wave independently during sweep
- Three layered cone meshes (main/core/outer) with translucent red material

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

### Icon Overhaul — Robot/Mech Themed (Icons/)
All 79 game icons replaced with robot/mech equivalents from game-icons.net (CC BY 3.0):
- **22 ability icons**: plasma-bolt, ion-cannon-blast, cogsplosion, tesla-coil, etc.
- **18 equipment icons**: energy-sword, mechanical-arm, robot-helmet, bolt-shield, battery-pack, etc.
- **8 consumable icons**: battery-pack variants (25/50/75/100%) replacing potions
- **6 bot frame icons**: battle-mech, robot-golem, tesla, vintage-robot, speaker, mecha-head
- **7 enemy icons**: spider-bot, centipede, sentry-gun, mecha-mask (replacing human-themed)
- **6 rarity icons**: cog → gears → cogsplosion → circuitry → processor → microchip
- **6 stat icons**: gear-hammer, speedometer, cpu, shield-reflect, robot-antennas, horseshoe
- Color-coded per category (orange=abilities, red=weapons, blue=armor, green=consumables, gold=botframes)
- 64x64 RGBA PNGs, same filenames = zero code changes needed
- Full mapping doc at `Icons/ICON_MAPPING.md`
- Conversion script at `Icons/convert_icons.mjs` (re-run to adjust palette)
- SVG source repo at `_downloads/icons/game-icons-net/` (4,229 icons)

### Passive Tree Phase 1 Restructure (2026-03-14)
Expanded passive tree from 96 → ~216 nodes. Each class now has 3 sub-branches instead of a single 8-node chain.

**Files modified:**
- `Scripts/Core/Enums.cs` — Added `Capstone` to SkillNodeType enum
- `Scripts/Classes/PassiveNodeData.cs` — Added `RequiredPointsInBranch`, `RequiredBranchId`, `SubBranchId`, `MutuallyExclusiveWith` fields
- `Scripts/Classes/PassiveTree.cs` — Added threshold gate + mutual exclusivity checks in `CanAllocate()`, added `CountPointsInBranch()` helper
- `Scripts/Classes/Perks.cs` — ~60 new perk ID constants (6 classes × 3 sub-branches + bridges + inner ring)
- `Scripts/Classes/PassiveTreeBuilder.cs` — **COMPLETE REWRITE** (~700 lines). 3 sub-branches per class fanning ±0.3 radians from split point, 12-node inner ring, 6 bridges, 6 core sockets

**Breaking changes:**
- Node IDs changed: old `sh_0..sh_7` → new `sh_t0/t1` (trunk), `sh_j0..j7` / `sh_b0..b7` / `sh_f0..f7` (sub-branches). **Incompatible with old saves.**
- New node types: Capstone (deep sub-branch endpoint, requires 6 points in branch)
- Keystones: 2 per class with `MutuallyExclusiveWith` (pick one path)

**NOT yet done (Phase 2+):**
- Perk effects in PerkProcessor.cs — new perks have IDs but no gameplay code yet
- PassiveTreeUI may need updates for Capstone node type and radius change (10 vs 8)
- Full plan: `Data/Research/passive_tree_full_plan.md`

### Debug Stat Overlay (2026-03-14)
- `Scripts/Debug/DebugMenu.cs` — Label3D attached to player showing all stats, HP, mana, level, skill points
- Toggle via `stats` console command or button in debug panel
- Updates every frame when active

### Foliage & Decoration Asset Downloads (_downloads/foliage/)
Downloaded 11 free CC0 3D model packs + 8 PBR texture sets for dungeon decoration:
- **Kenney**: Nature Kit (329), Graveyard Kit (91), Space Kit (153), Space Station Kit (97), Modular Dungeon Kit (39)
- **Quaternius**: Sci-Fi MegaKit (378 FBX), Modular Dungeon (48), Nature MegaKit (136)
- **KayKit**: Dungeon Remastered (422 FBX), Space Base Bits (114)
- **OpenGameArt**: Modular Sci-Fi Environments (91 FBX)
- **Textures**: ambientCG moss/rust (with Godot .tres), Poly Haven concrete_moss/rusty_metal/metal_plate
- Full source list with URLs at `ASSET_SOURCES.md` in project root
- All packs in `_downloads/foliage/` — need integration into Models/Dungeon/ per sector

### KitBash3D Future Warfare — Export COMPLETE
- `_downloads/kitbash3d_/extracted/exported_glb/` has **448 GLB files** (5.3GB)
- Exported via `export_all.py` Blender script
- Some buildings already copied to `Models/Dungeon/Buildings/`
- Full set available for further integration

## Recent Work — Local Claude (Windows/Godot)

### Playtest Bug Fix Pass (6 Phases — 2026-03-14)
Comprehensive fix pass addressing 54 playtest reports and ~4,284 debugger errors:

**Phase 1: Error Spam Elimination (~3,450 errors → 0)**
- `DungeonVisualizer.cs`: Swapped GlobalPosition/AddChild order (2 sites), added IsInsideTree() guards on LookAt() calls
- `AxisHazardPlacer.cs`: Swapped projectile GlobalPosition/AddChild order

**Phase 2: Missing Asset References (~220 errors → 0)**
- `MI_Trim_01.tres`, `MI_Trim_02.tres` → T_Props_Batch1 textures
- `MI_Trim_03.tres`, `MI_Trim_03_Dark.tres` → T_Props_Batch2 textures

**Phase 3: Gameplay Bugs (6 fixes)**
- `Projectile.cs`: Trail particles now use damage-type color instead of hardcoded white
- `MinimapUI.cs`: Initialize minimap with entrance + adjacent rooms on SetRoomGrid()
- `BugReportDialog.cs`: Pauses game tree while open, restores on close
- `GameManager.cs`: Added global Ctrl+B (bug report) / Ctrl+Shift+B (feature request) input handler
- `strings.json`: HP→Plating, Mana→Charge, energy→Charge across all strings
- `RoomLayoutLibrary.cs`: Enhanced Overgrown (15 patches, 20 vines), Frozen (15 crystals, frost plane, blue light), Red Alert (8 lights, glow overlay)

**Phase 4: Editor Bugs (5 fixes)**
- `BossEditor.cs`: Add Ability → PopupMenu dropdown (was silent add). Dirty asterisk checks current ID. Save triggers list rebuild. Deferred ProceduralAnimator init. AnimationPlayer idle fallback.
- `EditorStyles.cs`: Added StatusWarning color
- `DungeonVisualizer.cs`: Increased lighting (main 1.0→1.8, fill 0.3→0.7, added overhead 0.5, ambient 0.12→0.25)

**Phase 5: Model Fixes & Enemy Reassignment**
- `ModelLibrary.cs`: Each enemy now uses own FBX model (no more duplicates). Fixed AXIS boss axis_mech → sm_veh_mech_01. Added axis_avatar alias.

**Phase 6: Wire Up Unused Assets**
- `RoomBuilder.cs`: Enabled FBX floor edge/corner tiles (was hardcoded false). Added window wall variants.
- `RoomDresser.cs`: Added SafeRoom props. Added weapon_rack, pedestal, computer_small, charging_cables, coupling, teleporter to room-type prop tables.

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

---

## Vine Logic TD — Alpha Status (2026-03-17)

The TD project has been **fully pivoted** from classic tower defense to **Vine Logic TD** — a roguelike TD where the player builds a programmable logic network (sensors → signals → effects). The network IS the maze. `TD_COORDINATION.md` in `Godot_TD/` is outdated and doesn't reflect this pivot.

**Definitive checklist:** `Godot_TD/ALPHA_ROADMAP.md`

### What's DONE (25/27 items)
- Full core loop: Intro cinematic → Draft (3 roles) → 3 floors → Boss → Win/Lose
- 3 map layouts with terrain (Gateway/Conduit/Arena)
- 4 enemy factions (Scavenger/Brute/Ghost/Swarm + bosses)
- 18 node types, 8 per role (Scrapwright/Arcanist/Bruteforge)
- Signal power budget, connection color coding, path preview + range indicators
- Planet theme system (TronPlanetTheme + ScrapyardPlanetTheme)
- Tron visuals, combat VFX, AXIS commentary, help overlay, bug reporter
- Perk selection between floors
- Asset pipeline + sandbox editor (42 assets, 3 outline modes)

### What's TODO (3 items — alpha blockers)
1. **Sound design** (#13) — Signal fire, gate open, turret shot, enemy death (procedural PCM)
2. **Corruption/modifier events** (#14) — AXIS possession, signal jam, overloader (3-4 events)
3. **Larger battlefield** (#15) — Current maps too small for off-screen spawning (Planet 2 needs 28x18+)

### Scrapyard Playtest Bug Fix Pass (2026-03-17)
Five visual bugs from Scrapyard planet playtest, all fixed and compiling:

**Bug 1: Bright bloom on Scrapyard** — Multiple compounding emission sources causing nuclear bloom.
- `ScrapyardEnvironment.cs`: Warm glow emission 1.5→0.6, smokestack OmniLight range 6→3, energy 0.8→0.4
- `VineBattleScene.cs`: Scrapyard glow intensity 0.4→0.15, added GlowBloom threshold 0.8

**Bug 2: Missing PBR textures (flat brown fallback)** — Texture paths didn't match actual Quixel filenames.
- `ScrapyardEnvironment.cs`: Fixed all Quixel paths — capitalized names (`Rusted_Metal_Plate_...`), `BaseColor` not `Base_Color`, `AO` not `Ambient_Occlusion`, `.jpg` not `.png`. Added garbage pile material for terrain variety.

**Bug 3: Tron green path lines on Scrapyard** — Path preview used hardcoded neon colors.
- `VinePathPreview.cs`: `EntryColors` now planet-aware (property, not static array). Scrapyard uses warm amber/copper/rust/ochre. Emission multiplier 0.5→0.2 on Scrapyard.

**Bug 4: Enemies massive / bloom-bloated** — Enemy emission + glow settings = white orbs.
- `ScrapyardPlanetTheme.cs`: Enemy rust shader `accent_intensity` 0.25→0.08
- `VineEnemy.cs`: Procedural fallback emission 0.8→0.3, hit flash 3.0→1.2, recovery emission 0.4→0.15 (all Scrapyard-only)

**Bug 5: Signal power visualization misleading** — Connections didn't show which effects would actually fire.
- `VineConnection.cs`: Added `IsEffectPowered()` BFS tracing backward through connections counting effect hops vs sensor SignalPower. Unpowered connections show dim gray-red. Connections auto-rebuild on node placed/sold events.
- `VineHUD.cs`: Help text explains dim red = unpowered, sensors have 3-4 power, each effect uses 1.

### Character Model Sizing & Outline Fix (2026-03-17, Claude B)
AABB-based model sizing replaces manual scale guesses. Outline width is now scale-compensated for consistent world-space thickness across all models.

**Files modified:**
- `Scripts/Core/Constants.cs` — Added `ENEMY_HEIGHT_STANDARD/SMALL/LARGE`, `PLAYER_HEIGHT`, `NODE_MODEL_HEIGHT`
- `Scripts/VineLogic/AssetLibrary.cs` — Added `_targetHeights` dict (9 character models), `InstantiateToHeight()` (AABB-based scaling), `GetModelScale()`. `InstantiateNormalized()` now uses height targets for characters, falls back to `_scaleOverrides` for props/buildings. Character entries removed from `_scaleOverrides`.
- `Scripts/VineLogic/PlanetTheme.cs` — `ApplyTronFlatRecursive()` and `AddSilhouetteOutline()` now divide outline_width by model scale for uniform thickness
- `Scripts/VineLogic/ScrapyardPlanetTheme.cs` — `ApplyScrapOutline()` same scale compensation

**Bug fix (from Claude A's commit 613a208f):**
- `Scripts/VineLogic/VineGrid.cs` — `GetTerrainBodyMaterial()` and `GetTerrainElevatedMaterial()` were calling themselves (infinite recursion) as the Tron fallback instead of `TronTheme.MakeWallBodyMaterial()` / `TronTheme.MakeElevatedMaterial()`. This caused a stack overflow lockup when loading any non-Scrapyard planet.

### Polish WIP
- Fog/atmosphere (#22) — Tron fog banks exist but have visual bugs
- Visual juice — Death pops, screen shake on boss hits
- Final balance pass — Wave difficulty, signal power, gold economy

### Key Code Paths (Vine Logic)
| System | File |
|--------|------|
| Game flow | `Scripts/Core/GameManager.cs` |
| Draft screen | `Scripts/VineLogic/VineDraftScreen.cs` |
| Battle scene | `Scripts/VineLogic/VineBattleScene.cs` |
| HUD + build bar | `Scripts/VineLogic/VineHUD.cs` |
| Node definitions | `Scripts/VineLogic/VineNodeData.cs` |
| Wave definitions | `Scripts/VineLogic/VineWaveData.cs` |
| Enemy controller | `Scripts/VineLogic/VineEnemy.cs` |
| Signal processing | `Scripts/VineLogic/VineNode.cs` |
| Map layouts | `Scripts/VineLogic/VineMapLayouts.cs` |
| Planet themes | `Scripts/VineLogic/PlanetTheme.cs` |
| Tron visuals | `Scripts/VineLogic/TronTheme.cs` |
| Perk system | `Scripts/VineLogic/VinePerkData.cs` |

---

## What Could Use Work Next

- **Audio curation** — Sonniss audio files need to be mapped to game SFX IDs and placed in Audio/ directories
- **Model scale tuning** — POLYGON models may need scale adjustments when loaded in Godot
- ~~**KitBash3D extraction**~~ — DONE: 448 GLB files exported, some integrated
- **Foliage integration** — 11 downloaded packs in `_downloads/foliage/` need wiring into RoomDresser per sector
- **Icon review** — 79 icons replaced, verify in-game appearance and tweak palette if needed
- **Auto-equip starter gear** — players start with gear in bag but nothing equipped
- **Meta save reset** — 227 runs of broken movement skewed all stats
- **Passive tree auto-allocate** — unspent skill points, players may not know P key
- **Co-op testing** — multi-player systems exist but untested recently
- **TAA consideration** — if MSAA+FXAA not sufficient for remaining aliasing
