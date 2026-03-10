# Art Pass Status

**Last updated:** 2026-03-09
**Branch:** `dev`
**Done by:** Remote Claude (WSL)

---

## Completed Work

### Phase 1: Enemy Models
Copied 3 FBX models from `_downloads/` into `Models/Characters/Enemies/`:
| Game Enemy ID | Source Asset | File |
|---------------|-------------|------|
| `rust_titan` | Spider Mech (Diesmech) | 3.9MB, boss enemy (2.2x player height) |
| `junk_lurker` | Grunt Robot | 94KB |
| `patch_bot` | Quaternius Robot | 3.3MB |

- Textures already in Enemies folder: `Disemech_*.png`, `SM_*.png`, `GRUNT_*.png`
- No code changes needed — `CharacterMeshBuilder.BuildEnemyBody()` already tries `ModelLibrary.TryLoad("enemy", enemyId)` before procedural fallback
- Height mapping for `rust_titan` (2.2x) already exists in `GetEnemyModelHeight()`

### Phase 2: POLYGON Dungeon Pack (Synty)
- **Source zip:** `_downloads/polygon_/POLYGON_Dungeon_Godot_4_5_1_v1_0_1.zip` (42MB)
- **Extracted to:** `Assets/PolygonDungeon/` (90MB)
- **Format:** `.tscn` prefabs referencing `.res` ArrayMesh files + `.tres` Godot materials
- **Content:** 781 prefabs, 795 meshes, 43 materials, 49+ textures

| Category | Count | Location |
|----------|-------|----------|
| Props | 180 | `Prefabs/Props/` |
| Walls | 88 | `Prefabs/Environments/Walls/` |
| Floors | 48 | `Prefabs/Environments/Floors/` |
| Pillars | 23 | `Prefabs/Environments/Pillars/` |
| Rocks | 138 | `Prefabs/Environments/Rocks/` |
| Wood | 37 | `Prefabs/Environments/Wood/` |
| Bones | 25 | `Prefabs/Environments/Bones/` |
| Misc (env) | 99 | `Prefabs/Environments/Misc/` |
| Items | 30 | `Prefabs/Items/` |
| Weapons | 73 | `Prefabs/Weapons/` |
| Characters | 16 | `Prefabs/Characters/` |

**Code changes — ModelLibrary.cs:**
- Added `_extraScanFolders` array scanning 10 POLYGON prefab directories
- Folders are merged into existing categories: `prop`, `wall`, `floor`, `pillar`, `rock`, `wood`, `bone`, `item`, `weapon`
- `Environments/Misc` and `Environments/Pillars` also merged into `prop` category for alias resolution

**30+ aliases mapping game IDs → POLYGON prefab IDs:**
```
barrel         → sm_prop_barrel_01
crate          → sm_prop_crate_metal_01
crate_long     → sm_prop_crate_metal_03
chest          → sm_prop_chest_01
weapon_rack    → sm_prop_weaponrack_01
torch          → sm_prop_torchstick_01
statue         → sm_env_statue_01
pedestal       → sm_prop_stonechair_01
shelf_tall     → sm_prop_bookcase_01
computer       → sm_prop_tech_switchboard_01
computer_small → sm_prop_tech_lever_01
pipes          → sm_prop_tech_pipe_01
capsule/pod    → sm_prop_tech_chamber_01
vessel         → sm_prop_vase_01
vessel_short   → sm_prop_vase_02
vessel_tall    → sm_prop_vase_04
laser          → sm_prop_tech_crystal_01
portal         → sm_prop_tech_engine_01
teleporter     → sm_prop_tech_turbine_01
wall_1..5      → sm_env_wall_01..05
floortile_basic  → sm_env_tiles_01
floortile_basic2 → sm_env_tiles_02
door_frame     → sm_env_door_frame_01
door_double    → sm_env_doordouble_flat_01
column_1..3    → sm_env_pillar_square/round_01..02
```

**Code changes — RoomDresser.cs:**
- Expanded `CornerProps`, `WallProps`, `FloorProps` dictionaries with POLYGON model IDs
- Combat rooms now get: barrels, crates, tech pipes, chains
- Boss rooms: statues, obelisks, braziers, crystals, wall banners
- Shop rooms: bookcases, lanterns
- Treasure rooms: chests, vases, gems, jewels
- Event rooms: tech engines, cauldrons, cogs, conveyors
- Entrance rooms: torches, bonfires, banners

**Material variants:** 4 color themes (A/B/C/D) for each of 4 base dungeon materials — could be used per-sector.

### Phase 3: POLYGON Mech Pack (Synty)
- **Source zip:** `_downloads/polygon_/POLYGON_Mech_SourceFiles_v2.zip` (97MB)
- **Extracted to:** `Assets/PolygonMech/` (FBX + textures only)
- **Content:** 141 FBX models, 44 textures

**12 weapons copied to `Models/Weapons/`:**
| Game Weapon ID | Source |
|----------------|--------|
| `arc_rifle` | SM_Wep_ArcRifle_01 |
| `assault_rifle` | SM_Wep_AssaultRifle_01 |
| `gatling_gun` | SM_Wep_GattlingGun_01 |
| `hammer` | SM_Wep_Hammer_01 |
| `war_hammer` | SM_Wep_Hammer_02 |
| `hand_cannon` | SM_Wep_HandCannon_01 |
| `katana` | SM_Wep_Katana_01 |
| `power_rifle` | SM_Wep_PowerRifle_01 |
| `rocket_launcher` | SM_Wep_Rocket_01 |
| `rocket_launcher_2` | SM_Wep_Rocket_02 |
| `shotgun` | SM_Wep_Shotgun_01 |
| `energy_sword` | SM_Wep_Sword_01 |

**4 props copied to `Models/Dungeon/Props/`:**
- `charging_cables`, `charging_cables_2`, `coupling`, `mech_cockpit`

**Unused mech assets still in `Assets/PolygonMech/`:**
- 80+ mech body part FBX (arms, legs, chest, head armor variants)
- Full mech model: `SM_Veh_Mech_01.fbx`
- Character attachments: helmets, pouches, holsters
- Building floor tiles: `SM_Bld_Floor_01..04.fbx`

### Phase 3b: AmbientCG PBR Materials
- **Source:** `_downloads/ambientcg/` (multiple zips)
- **Extracted to:** `Assets/Materials/AmbientCG/` (31 material folders, 254 textures)

| Material | Maps |
|----------|------|
| Metal040, 042A/B, 045B, 048A, 049A, 053C, 055C | Color, NormalGL, NormalDX, Roughness, Metalness, Displacement |
| Ground031, Ground071 | Color, NormalGL, Roughness, Displacement |
| PavingStones049, 082, 084, 149 | Color, NormalGL, Roughness, Displacement |
| Leaking019B | Color, NormalGL, Roughness, Displacement |
| RoadLines021B, 022B, 034C | Color, NormalGL, Roughness |
| concrete_crack, damaged_concrete | BaseColor, Normal, Roughness, AO, Bump, Displacement |
| garbage_pile, industrial_rubble | BaseColor, Normal, Roughness, AO |
| rusted_metal_plate | BaseColor, Normal, Roughness, AO |
| hand_smear, small_garbage_scatter | BaseColor, Normal, Roughness (decals) |
| painted_zero/two/five, poster, japanese_sign | BaseColor, Normal, Roughness (signage) |

**Not yet wired into any shader or material system.** These need Godot StandardMaterial3D resources created to use them.

### Phase 4: Sonniss Audio
- **Source:** `_downloads/sonniss_audio_/` (10 zip packs)
- **Full library extracted to:** `Assets/Audio/Sonniss/` (1,423 .wav files)

| Pack | Files | Content |
|------|-------|---------|
| BigMechanical | 104 | Heavy mech impacts, stomps, servos |
| FuturisticWeapons | 215 | Sci-fi weapon fire, charges, zaps |
| HeavyMechanical | 157 | Industrial mech sounds |
| Mechanical | 201 | Gear clicks, mechanisms, metal |
| Mechanics2 | 115 | More mechanical SFX |
| SciFiWeapons | 110 | Futuristic weapon shots |
| SciFiWeapons2 | 110 | More sci-fi weapons |
| SciFiWeapons3 | 150 | Even more sci-fi weapons |
| SciFiBlasters | 153 | Blaster/laser fire sounds |
| SteampunkMachines | 108 | Steampunk machinery ambience |

**34 files wired to game audio slots in `Audio/`:**
- 14 SFX → `Audio/SFX/` (hit, crit_hit, enemy_death, swing, pickup, etc.)
- 18 ability sounds → `Audio/Abilities/` (all 18 abilities mapped)
- 2 ambient loops → `Audio/Ambient/` (arena_hum, machinery)
- `audio.json` updated (ambient paths changed from .ogg to .wav)

**Audio mapping is generic** — files were picked by number, not auditioned. These should be swapped with better-fitting sounds during playtesting.

### Also This Session: AXIS Intro Laser Sweep
- Replaced hand-pointing room reveal with opaque cone laser sweep from under AXIS body
- Three layered cone meshes (main/core/outer) with translucent red material
- Quaternion slerp rotation (direction-agnostic, fixed broken Euler approach)
- AXIS scaled to 3x, positioned at Y=15
- Hands wave independently during sweep (left/right stay on their own sides)
- Files changed: `Scripts/Dungeon/DungeonAssemblyIntro.cs`, `Scripts/Dungeon/AXISPresence.cs`

---

## Not Yet Done

### Models & Props
- [ ] **POLYGON prop scaling** — prefabs may not match the game's tile grid (5m tiles). Test in Godot and adjust
- [ ] **POLYGON material assignment** — FBX models (weapons, enemies) may load with default materials. Need Godot import settings or material overrides
- [ ] **Weapon → game item mapping** — 12 new weapon FBX files exist but aren't mapped to `BaseItemPool` or `ItemRegistry` weapon entries
- [ ] **Mech body parts** — 80+ mech armor FBX in `Assets/PolygonMech/` could be used for player bot frame customization
- [ ] **POLYGON Dungeon characters** — 16 fantasy character prefabs exist but don't fit robot theme (goblins, skeletons, knights)
- [ ] **Door models** — POLYGON has 17 door variants (`SM_Env_Door_*`). `RoomBuilder.BuildWallWithDoor()` doesn't load door geometry yet
- [ ] **Ceiling models** — POLYGON has 17 ceiling variants. Game has no ceiling system
- [ ] **6 unused Kenney blasters** (i, l, m, n, o, q) — could be loot weapon variants

### Materials
- [ ] **Create Godot StandardMaterial3D resources** from AmbientCG PBR textures
- [ ] **Apply PBR materials to dungeon surfaces** — floors, walls, props
- [ ] **POLYGON material variants** — 4 color themes (A/B/C/D) could map to 4 of the 5 sector themes
- [ ] **Sector-based material swapping** — different dungeon appearance per sector

### Audio
- [ ] **Curate SFX** — playtest and swap generic number-picks with fitting sounds
- [ ] **Music tracks** — no music files yet (all 5 music slots still use procedural fallback)
- [ ] **Voice lines** — no voice files (3 AXIS/BIT voice slots still procedural)
- [ ] **Weapon-specific SFX** — rifle, shotgun, launcher sounds exist in Sonniss but aren't mapped
- [ ] **Enemy-specific SFX** — could map different mechanical sounds to different enemy types

### Large Assets Not Extracted
- [ ] **KitBash3D Future Warfare** — 2.9GB `.blend` zip at `_downloads/kitbash3d_/`. Needs Blender to open and export to FBX/GLB. Contains high-quality sci-fi environment pieces
- [ ] **POLYGON Dungeon Source Files** — 54MB zip with raw FBX versions of dungeon assets (alternative to the Godot .tscn pack)

---

## Key File Locations

| What | Path |
|------|------|
| ModelLibrary (scan + aliases) | `Scripts/Assets/ModelLibrary.cs` |
| RoomDresser (prop placement) | `Scripts/Dungeon/RoomDresser.cs` |
| RoomBuilder (floors/walls/decorations) | `Scripts/Dungeon/RoomBuilder.cs` |
| CharacterMeshBuilder (enemy models) | `Scripts/VFX/CharacterMeshBuilder.cs` |
| AudioManager (SFX playback) | `Scripts/Audio/AudioManager.cs` |
| AudioLoader (manifest loader) | `Scripts/Core/AudioLoader.cs` |
| Audio manifest | `Data/audio.json` |
| POLYGON Dungeon assets | `Assets/PolygonDungeon/` |
| POLYGON Mech assets | `Assets/PolygonMech/` |
| AmbientCG materials | `Assets/Materials/AmbientCG/` |
| Sonniss audio library | `Assets/Audio/Sonniss/` |
| Game audio (active) | `Audio/{SFX,Abilities,Ambient,Music,Voice}/` |
| Raw downloads | `_downloads/` (do not commit) |

## How It Works

**Model loading:** `ModelLibrary.TryLoad(category, id)` scans directories on first call, caches results. Returns `Node3D` or `null` (caller falls back to procedural). Aliases map simple game IDs to POLYGON prefab names.

**Audio loading:** `AudioManager.PlaySFXByName(name)` checks `audio.json` manifest → convention path (`Audio/SFX/{name}.wav`) → procedural PCM fallback. Files just need to exist at the manifest path.

**Everything is graceful-fallback.** Missing assets = procedural generation. No crashes.
