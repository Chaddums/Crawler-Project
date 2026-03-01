# Junkbot Arena -- Temporary Asset Tracker

All placeholder, procedural, and temporary assets used in the current build.
Items marked with a checkmark have been upgraded (e.g., replaced with asset pack models).

---

## Level Art

### Floors

- [x] **Floor tile shader** -- `Scripts/Dungeon/RoomBuilder.cs` (`CreateFloorShader()`) -- Procedural checkerboard shader with per-tile hash variation, gap lines, and worn edges. Renders a single PlaneMesh per room with ShaderMaterial. Room-type colors defined in `GetFloorColor()`. **Final:** Tileable PBR floor textures (metal grating, concrete, industrial plating) per room type.
- [x] **Floor tile FBX models** -- `Models/Dungeon/Floors/` -- 7 floor tile variants already imported (`floortile_basic.fbx`, `floortile_basic2.fbx`, `floortile_corner.fbx`, `floortile_double_hallway.fbx`, `floortile_empty.fbx`, `floortile_innercorner.fbx`, `floortile_side.fbx`). **Status:** Asset pack models exist but are NOT yet wired into `RoomBuilder.BuildTileFloor()` -- the shader path is still used.
- **Corridor floor** -- `Scripts/Dungeon/RoomBuilder.cs` (`BuildCorridorFloor()`) -- Same floor shader as rooms, plus an emissive orange center strip (BoxMesh). **Final:** Corridor-specific floor tileset, LED strip decals.

### Walls

- [x] **Wall shader** -- `Scripts/Dungeon/RoomBuilder.cs` (`CreateWallShader()`) -- Procedural paneled wall shader with grooves, corner rivets, and accent stripe. Applied to BoxMesh walls. Room-type colors in `GetWallColor()`. **Final:** PBR wall textures with normal maps.
- [x] **Wall FBX models** -- `Models/Dungeon/Walls/` -- 20 wall variants already imported (wall_1-5, wall_empty, door variants, window variants). **Status:** `RoomBuilder.BuildWall()` calls `ModelLibrary.TryLoad("wall", "wall_segment")` but the IDs don't match the imported filenames (e.g., `wall_1` vs `wall_segment`). Falls back to procedural shader.
- **Wall trim** -- `Scripts/Dungeon/RoomBuilder.cs` (`AddWallTrim()`) -- Procedural baseboard and crown strips using BoxMesh with metallic material. **Final:** Modeled trim pieces or decal strips.
- **Wall torches/sconces** -- `Scripts/Dungeon/RoomBuilder.cs` (`AddTorch()`) -- Procedural bracket (BoxMesh) + torch head (CylinderMesh) with emissive material + OmniLight3D + `VfxFactory.CreateTorchFireParticles()`. Falls back when no `prop/torch` model found. **Final:** Modeled wall-mounted industrial lights or robot-themed sconces.
- [x] **Wall detail panels** -- `Models/Dungeon/Details/` -- 26 detail FBX models (arrows, basic panels, cylinders, dots, hexagons, pipes, plates, vents, x-pattern). **Status:** `AddWallDetails()` loads from `ModelLibrary.GetCategoryIds("detail")` -- these ARE integrated and working in combat/boss rooms.

### Obstacles & Props

- [x] **Barrel** -- `Models/Dungeon/Props/barrel.fbx` -- Loaded via `ModelLibrary.TryLoad("prop", "barrel")` in `AddCombatDecorations()`. **Status:** Integrated.
- [x] **Crate** -- `Models/Dungeon/Props/crate.fbx` (also `crate_long.fbx`) -- Loaded via `ModelLibrary.TryLoad("prop", "crate")`. **Status:** Integrated.
- [x] **Weapon rack** -- `Models/Dungeon/Props/weapon_rack.fbx` -- Loaded in `AddCombatDecorations()`. **Status:** Integrated.
- [x] **Pedestal** -- `Models/Dungeon/Props/pedestal.fbx` -- Used in treasure rooms. **Status:** Integrated.
- [x] **Pillar/Column** -- `Models/Dungeon/Props/column_1.fbx` (aliased as "pillar"), `column_2.fbx`, `column_3.fbx`, `column_slim.fbx` -- Used in boss rooms. **Status:** Integrated.
- [x] **Stairs** -- `Models/Dungeon/Props/stairs.fbx` -- Used in entrance rooms. **Status:** Integrated.
- **Gold piles** -- `Scripts/Dungeon/RoomBuilder.cs` (`AddTreasureDecorations()`) -- Procedural BoxMesh "piles" if no `prop/gold_pile` model found. **Final:** Modeled scrap/salvage piles.
- **Metal pillar obstacles** -- `Scripts/Dungeon/RoomBuilder.cs` (`AddObstacles()`) -- CylinderMesh procedural pillars. **Final:** Use column models or new obstacle set.
- **Crate stack obstacles** -- `Scripts/Dungeon/RoomBuilder.cs` (`AddObstacles()`) -- BoxMesh procedural crate stacks. **Final:** Use existing crate models with stacking.
- **Low wall obstacles** -- `Scripts/Dungeon/RoomBuilder.cs` (`AddObstacles()`) -- BoxMesh procedural barriers. **Final:** Modeled cover walls or barricades.
- **Raised platforms** -- `Scripts/Dungeon/RoomBuilder.cs` (`AddRaisedPlatform()`) -- BoxMesh platforms with procedural ramp. **Final:** Modeled arena platforms with proper ramp geometry.
- **Event room brazier** -- `Scripts/Dungeon/RoomBuilder.cs` (`AddEventDecorations()`) -- CylinderMesh with emissive purple material. **Final:** Modeled terminal/console.
- **Event room rune stones** -- `Scripts/Dungeon/RoomBuilder.cs` (`AddEventDecorations()`) -- BoxMesh with emissive material at room corners. **Final:** Modeled data nodes or pillars.
- **Shop counter** -- `Scripts/Dungeon/RoomBuilder.cs` (`AddShopDecorations()`) -- BoxMesh counter + CylinderMesh pedestals + BoxMesh floating item previews. **Final:** Modeled vendor booth.
- **Shop NPC** -- `Scripts/Dungeon/RoomBuilder.cs` (`AddShopDecorations()`) -- Procedural CylinderMesh body + BoxMesh head with emissive eyes + Label3D "SHOP" sign. **Final:** Modeled robot vendor character.
- [x] **Other props in Models** -- `Models/Dungeon/Props/` -- Additional models exist but are not yet referenced by any code: `capsule.fbx`, `chest.fbx`, `computer.fbx`, `computer_small.fbx`, `laser.fbx`, `pipes.fbx`, `pod.fbx`, `portal.fbx`, `shelf_tall.fbx`, `statue.fbx`, `teleporter.fbx`, `vessel.fbx`, `vessel_short.fbx`, `vessel_tall.fbx`. **Status:** Available but unused -- need to be wired into room decoration logic.

### Doors

- [x] **Door frame FBX** -- `Models/Dungeon/Doors/door_frame.fbx`, `door_double.fbx` -- Loaded via `ModelLibrary.TryLoad("door", "door_frame")` in `BuildDoorFrame()` / `BuildDoorFrameZ()`. **Status:** Integrated.
- **Procedural door frame fallback** -- `Scripts/Dungeon/RoomBuilder.cs` (`BuildDoorFrame()`) -- BoxMesh pillars + lintel with metallic material. Used when model not found. **Final:** Always use FBX models.

### Hazards

- **Poison pool** -- `Scripts/Dungeon/RoomBuilder.cs` (`AddPoisonPool()`) -- PlaneMesh with green emissive semi-transparent material + `VfxFactory.CreateAmbientParticles()`. **Final:** Modeled acid pool with animated surface shader.
- **Electric plate** -- `Scripts/Dungeon/RoomBuilder.cs` (`AddElectricPlate()`) -- BoxMesh with blue emissive material. **Final:** Modeled electric floor panel with spark VFX.
- **Lava crack** -- `Scripts/Dungeon/RoomBuilder.cs` (`AddLavaCrack()`) -- Narrow BoxMesh with orange emissive material + torch fire particles. **Final:** Modeled floor crack with lava shader.

---

## Character Models

### Player (6 Bot Frames)

All 6 bot frames are built as procedural multi-part junkbot bodies in `Scripts/VFX/CharacterMeshBuilder.cs` (`BuildJunkbotBody()`). Each has ~20-30 mesh parts: tracked chassis legs with wheels, articulated arms, torso, head with emissive eyes, and class-specific details. Animated by `Scripts/VFX/ProceduralAnimator.cs` (tween-based limb animation with walk/idle/attack/hit/death states and track-wheel spinning).

`ModelLibrary.TryLoad("player", classId)` is called first but `Models/Characters/Player/` is **empty** -- all 6 frames use procedural geometry.

- **Scrapheap** -- Heavy tank junkbot (tracked chassis, heavy cannon weapon)
- **Tin Can** -- Standard balanced junkbot (tracked chassis, standard blaster weapon)
- **Spark Plug** -- Mage junkbot (tracked chassis, arc caster weapon with energy coils)
- **Rust Bucket** -- Stealth junkbot (tracked chassis, needler weapon with suppressor)
- **Noise Box** -- Support junkbot (tracked chassis, pulse emitter weapon with resonance disc)
- **Clunker** -- Melee/brawler junkbot (tracked chassis, rivet gun weapon)

Each class also has a procedural weapon built in `CharacterMeshBuilder.cs`:
- **Standard Blaster** (TinCan) -- CylinderMesh barrel + BoxMesh body/grip + emissive muzzle
- **Heavy Cannon** (Scrapheap) -- Fat barrel + TorusMesh muzzle brake + BoxMesh receiver/ammo box
- **Arc Caster** (SparkPlug) -- Thin barrel + 3 TorusMesh energy coils + emissive orb tip
- **Needler** (RustBucket) -- Long thin barrel + suppressor + scope
- **Pulse Emitter** (NoiseBox) -- Barrel + resonance disc + tuning forks
- **Rivet Gun** (Clunker) -- Wide barrel + drum magazine + bayonet

**Available asset pack:** `C:\Users\Stu\Downloads\Modular_Robots.zip` -- 79 voxel robot OBJ models with palette textures. Not yet integrated. Could provide player body models for all 6 frames.

**Final:** Modeled robot characters (rigged + animated) per frame, or voxel models from the Modular_Robots pack with ProceduralAnimator support.

### Companion (BIT)

- **BIT drone** -- `Scripts/VFX/CharacterMeshBuilder.cs` (`BuildCompanionBody()`) -- Procedural hovering drone with ~12 parts (body sphere, eye lens, antenna, rotor hub + 4 rotors, stabilizer fins, signal disc, landing skids). **Final:** Modeled drone with spinning rotor animation.

### Enemies (4 types)

`Scripts/VFX/CharacterMeshBuilder.cs` (`BuildEnemyBody()`) checks `ModelLibrary.TryLoad("enemy", enemyId)` first. FBX models exist in `Models/Characters/Enemies/` with textures.

- [x] **Calibration Target** -- `Models/Characters/Enemies/calibration_target.fbx` + `George_Texture.png` -- FBX model available. Procedural fallback: barrel body with target rings, crossbar with pads, head with helmet, post base.
- [x] **Scrap Rat** -- `Models/Characters/Enemies/scrap_rat.fbx` + `Leela_Texture.png` -- FBX model available. Procedural fallback: squashed body sphere with gear discs, 4 articulated legs, antenna ears, tail segments, emissive eyes.
- [x] **Decoy Unit** -- `Models/Characters/Enemies/decoy_unit.fbx` + `Mike_Texture.png` -- FBX model available. Procedural fallback exists.
- [x] **Wire Worm** -- `Models/Characters/Enemies/wire_worm.fbx` + `Stan_Texture.png` -- FBX model available. Procedural fallback exists.

**Status:** FBX files are present; integration depends on whether `ModelLibrary` auto-scanning picks up the filenames correctly. The procedural bodies remain as fallback.

### Bosses (3 types)

All boss bodies are procedural in `Scripts/VFX/CharacterMeshBuilder.cs`. No boss FBX models exist.

- **Corrupted Sentry** -- `BuildCorruptedSentryBody()` -- Multi-part procedural boss mesh.
- **Scrap Hydra** -- `BuildScrapHydraBody()` -- Multi-part procedural boss mesh.
- **AXIS Avatar** -- `BuildAxisAvatarBody()` -- Multi-part procedural boss mesh.

**Final:** Modeled boss characters (rigged + animated), or enhanced procedural meshes with custom shaders.

### Item Models

- **Item world drops** -- `Scripts/VFX/CharacterMeshBuilder.cs` (`BuildItemModel()`) -- Procedural geometry for ground-dropped items. **Final:** Modeled item pickups.
- **Loot boxes** -- `Scripts/VFX/CharacterMeshBuilder.cs` (`BuildLootBoxModel()`) -- Procedural box geometry per tier. **Final:** Modeled loot crate with tier-specific materials.
- **Blade Ring** -- `Scripts/VFX/CharacterMeshBuilder.cs` (`BuildBladeRing()`) -- Procedural spinning ring weapon.

---

## Icons

- **79 temp icons** from [game-icons.net](https://game-icons.net) -- `Godot/Icons/` directory, manifest at `Godot/Data/icons.json`
- **License:** CC BY 3.0 -- Attribution in `Godot/Icons/ATTRIBUTION.txt`
- **Placeholder fallback:** `Scripts/Core/IconLoader.cs` generates a magenta checkerboard 32x32 texture when an icon file is missing.

### Icon Categories
| Category | Count | Location |
|----------|-------|----------|
| Abilities | 22 | `Icons/Abilities/` (strike, shield bash, whirlwind, slam, feral roar, earthquake, arcane bolt, frost nova, meteor, backstab, smoke bomb, assassinate, dark chord, raise dead, death ballad, flurry, uppercut, hundred fists, cannon blast, burst fire, snipe shot, rivet burst) |
| Equipment | 18 | `Icons/Items/Equipment/` (sword, staff, dagger, shield, helmet, chestplate, robe, greaves, boots, gauntlets, amulet, ring, cloak, pistol, rifle, shotgun, launcher, repeater) |
| Consumables | 8 | `Icons/Items/Consumables/` (3 health potions, 3 mana potions, elixir, overclock injector) |
| Stats | 6 | `Icons/UI/Stats/` (strength, dexterity, intelligence, constitution, charisma, luck) |
| Rarity | 6 | `Icons/UI/Rarity/` (common, uncommon, rare, epic, legendary, absurd) |
| Bot Frames | 6 | `Icons/BotFrames/` (scrapheap, tincan, sparkplug, rustbucket, noisebox, clunker) |
| Enemies | 4 | `Icons/Enemies/` (calibration_target, scrap_rat, decoy_unit, wire_worm) |
| Bosses | 3 | `Icons/Enemies/Bosses/` (corrupted_sentry, scrap_hydra, axis_avatar) |
| Loot Boxes | 5 | `Icons/LootBoxes/` (bronze, silver, gold, diamond, legendary) |
| Companion | 1 | `Icons/Companions/` (bit) |

**Final:** Custom pixel art or vector icon set with consistent style. Must cover all 79+ slots.

---

## Audio

All 14 sounds are procedurally generated from raw PCM16 data at 22050 Hz sample rate in `Scripts/Audio/AudioManager.cs`. Cached after first generation. The system checks for real audio files first (via `AudioLoader` manifest and convention path `res://Audio/SFX/{name}.wav`) before falling back to procedural.

| Sound | Method | Description |
|-------|--------|-------------|
| `hit` | `GenerateHitSound()` | Short noise burst + 800Hz sine -- metallic clang (0.08s) |
| `crit_hit` | `GenerateCritSound()` | Hit sound + 1200Hz overlay for crit feel (0.15s) |
| `enemy_death` | `GenerateDeathSound()` | Descending tone 300-50Hz with noise (0.35s) |
| `swing` | `GenerateSwingSound()` | Whoosh: sine sweep 400-100Hz + noise (0.15s) |
| `pickup` | `GeneratePickupSound()` | Ascending chime: two quick 440Hz/660Hz sine tones (0.2s) |
| `level_up` | `GenerateLevelUpSound()` | Ascending arpeggio C-E-G-C (262-523Hz) with harmonics (0.4s) |
| `projectile` | `GenerateProjectileSound()` | Zap: sawtooth sweep 800-200Hz (0.12s) |
| `heal` | `GenerateHealSound()` | Gentle rising shimmer 400-600Hz with 3rd harmonic (0.25s) |
| `achievement` | `GenerateAchievementSound()` | Rising arpeggio fanfare G-B-D-G (392-784Hz) (0.39s) |
| `box_shake` | `GenerateBoxShakeSound()` | Rattling: pulsed noise bursts that grow louder (0.8s) |
| `box_open` | `GenerateBoxOpenSound()` | Burst: noise + ascending sweep 200-1200Hz (0.25s) |
| `item_reveal` | `GenerateItemRevealSound()` | Sparkle chime: 1047Hz + 1568Hz sine (0.12s) |
| `heartbeat` | `GenerateHeartbeatSound()` | Deep double-thump: 50Hz sine beats (0.6s) |
| `epic_drop` | `GenerateEpicDropSound()` | Deep 60Hz impact thump + ascending shimmer sweep (0.5s) |

**No background music** exists in any form -- procedural or otherwise.

**Final:** Recorded/synthesized SFX .wav files placed in `res://Audio/SFX/`. Background music tracks (ambient, combat, boss, menu). Voice lines for AXIS and BIT commentary.

---

## Strings / Dialogue

All game text lives in `Godot/Data/strings.json`, loaded by `Scripts/Core/StringLoader.cs`.

### Content Categories
- **Abilities** -- 22 abilities with name + description (robot-themed: "Piston Strike", "Bulkhead Slam", "Arc Discharge", etc.)
- **Bot Frames** -- 6 frame descriptions
- **Achievements** -- 25 achievements with title, description, and AXIS commentary line
- **Consumables** -- 8 items with name + description (repair kits, battery packs, etc.)
- **Equipment** -- 17 equipment display names
- **Affixes** -- 33 prefix/suffix names for item generation
- **Enemies** -- 4 enemy display names
- **Bosses** -- 3 boss display names
- **Companions** -- BIT name + description
- **Loot Boxes** -- 5 tier display names
- **Loot Narration** -- 4 templates for loot box ceremony (first item, last item, epic, normal)
- **UI strings** -- Character creation, loot box, sector transition labels

### Commentary System (`Scripts/Commentary/CommentaryManager.cs`)
- **Enemy killed** -- 5 AXIS/BIT lines (randomly selected via `StringLoader.GetRandom()`)
- **Level up** -- 1 AXIS announcement with `{level}` template
- **Player death** -- 1 AXIS death announcement
- **Item pickup** -- 1 BIT line
- **Sector enter** -- 1 AXIS line with `{sector}` template
- **System messages** -- 4 messages (level up, sector enter, loot warning, death)
- **Troll events** -- 7 AXIS troll strings (inventory shuffle, UI dodge, enemy swap, fake level up, timer glitch)

**Final:** Professional writing pass expanding commentary to 10+ lines per event. Localization support. Additional dialogue for shop NPC, boss encounters, achievements.

---

## VFX

All VFX are built from GpuParticles3D with ParticleProcessMaterial in `Scripts/VFX/VfxFactory.cs`. Draw pass uses a shared SphereMesh (0.06 radius, 4 radial segments) with billboard unshaded material.

### Particle Effects
| Effect | Method | Description |
|--------|--------|-------------|
| Hit sparks | `CreateHitParticles()` | 12 particles, one-shot burst, color by damage type (0.3s) |
| Death burst | `CreateDeathParticles()` | 24 particles, outward explosion on kill (0.6s) |
| Loot burst | `CreateLootBurstParticles()` | 16 gold particles, upward arc on loot drop (0.5s) |
| Ambient motes | `CreateAmbientParticles()` | 20 slow floating particles in sphere radius, looping |
| Torch fire | `CreateTorchFireParticles()` | 8 particles, upward orange-to-red gradient, looping |
| Portal swirl | `CreatePortalParticles()` | 16 orbiting particles in sphere, looping |
| Rarity aura | `CreateRarityAura()` | 10 orbiting particles, color by rarity (Rare/Epic/Legendary/Absurd) |
| Celebration | `CreateCelebrationParticles()` | 30 upward sparkles on room clear (1.2s) |
| Pickup trail | `CreatePickupTrail()` | 8 particles, upward arc on item collect (0.4s) |
| Impact burst | `CreateImpactBurst()` | 18 particles, full-sphere explosion for projectile hits (0.35s) |
| Music notes | `CreateMusicNotes()` | 8 upward floating particles for NoiseBox (0.8s) |
| Light pillar | `CreateLightPillar()` | Emissive CylinderMesh (12 units tall) + 20 rising particles for Epic+ drops |

### Mesh-Based VFX
| Effect | Method | Description |
|--------|--------|-------------|
| Arcane circle | `CreateArcaneCircle()` | Flat TorusMesh ring, spins + fades via tween (0.6s) |
| Shockwave ring | `CreateShockwaveRing()` | TorusMesh that expands 5x + fades (0.3s) |
| Crit ring | `CombatVfxManager.SpawnCritRing()` | SphereMesh that expands 3x + fades with gold emissive (0.25s) |
| Projectile | `Scripts/Combat/Projectile.cs` | Emissive SphereMesh (0.15r) + trailing GpuParticles3D, color by damage type |

**Final:** GPU particle effects with proper sprite sheets/textures. Custom shaders for fire, electricity, poison. Mesh-based effects replaced with authored VFX scenes.

---

## UI

All UI is code-built using Godot Control nodes. No .tscn scene files or designed sprites for UI elements (except `MainMenu.tscn` which has a basic background + VBoxContainer layout).

### HUD Elements (`Scripts/UI/HUDController.cs`)
- **Health bar** -- ProgressBar with StyleBoxFlat fill, color shifts (green/yellow/red), smooth lerp animation. Position: top-left (20, 20), 300x26px.
- **Mana bar** -- ProgressBar with blue StyleBoxFlat fill, smooth lerp. Position: top-left (20, 52), 300x22px.
- **XP bar** -- ProgressBar with purple fill + "Lv.X" label + "XP/Next" text. Position: top-left (20, 80), 180x14px.
- **Buff strip** -- HBoxContainer of 32x32 PanelContainer icons with single-letter labels + duration text. Position: (20, 102). Red border for debuffs, green for buffs.
- **Sector/Area label** -- Label at top-right, "Sector X - Area Y".

### Bars and Indicators
- **Boss health bar** (`Scripts/UI/BossHealthBarUI.cs`) -- Full CanvasLayer, 600x24 ProgressBar with slide-in/out animation, name label, 3 phase dots. Code-built.
- **Enemy health bar** (`Scripts/UI/EnemyHealthBar3D.cs`) -- Billboard QuadMesh above enemies, 1.2x0.12 units. Hidden at full HP, shown on damage. Color-shifting fill.
- **Damage numbers** (`Scripts/UI/DamageNumberUI.cs`) -- Label3D nodes spawned at hit points, float up + fade out via tween. Crits: gold + 1.5x scale pop. Color by damage type.

### Inventory (`Scripts/UI/InventoryUI.cs` + `ItemSlotUI.cs`)
- **Item slots** -- 64x64 ColorRect background + ColorRect border (rarity-colored) + TextureRect icon + Label stack count. All code-built.
- **Equipment slots** (`Scripts/UI/EquipmentSlotUI.cs`) -- Similar to item slots with named equipment position.
- **Item tooltip** (`Scripts/UI/ItemTooltipUI.cs`) -- PanelContainer with styled text for item details.

### Ability Bar (`Scripts/UI/AbilityBarUI.cs` + `AbilitySlotUI.cs`)
- 6-slot horizontal bar at bottom-center. Each slot: code-built PanelContainer with icon + cooldown overlay + hotkey label.

### Overlays
- **Inventory UI** (`Scripts/UI/InventoryUI.cs`) -- Full inventory screen with bag grid + equipment panel + character sheet. Code-built overlay.
- **Passive tree** (`Scripts/UI/PassiveTreeUI.cs` + `PassiveTreeCanvas.cs`) -- POE-style hex node grid with connections. All code-drawn.
- **Passive node tooltip** (`Scripts/UI/PassiveNodeTooltipUI.cs`) -- PanelContainer with node details.
- **Character sheet** (`Scripts/UI/CharacterSheetUI.cs`) -- Stats display overlay.
- **Pause menu** (`Scripts/UI/PauseMenuUI.cs`) -- Overlay with resume/save/quit buttons.
- **Minimap** (`Scripts/UI/MinimapUI.cs`) -- 200x200 custom _Draw() minimap with colored room rectangles, corridor lines, blinking player dot. Position: top-right (1700, 60).

### Full-Screen UI
- **Character creation** (`Scripts/UI/CharacterCreationUI.cs`) -- 6 bot frame cards + description panel + stats grid + "Enter Arena" button. Code-built, no scene file.
- **Main menu** (`Scripts/UI/MainMenuUI.cs`) -- Continue/New Game/Quit buttons. Uses `MainMenu.tscn` scene with VBoxContainer layout.
- **Sector transition** (`Scripts/UI/SectorTransitionUI.cs`) -- Black fade + "Entering Sector X" gold title + subtitle. Code-built CanvasLayer.
- **Loot box ceremony** (`Scripts/UI/LootBoxCeremonyUI.cs`) -- Full-screen dimmed overlay, shaking box visual, flash effect, item reveal list. Code-built.
- **Lift timer** (`Scripts/UI/LiftTimerUI.cs`) -- Per-sector countdown display.
- **Achievement notification** (`Scripts/UI/AchievementNotificationUI.cs`) -- Pop-up notification with achievement details.

**Final:** Designed UI theme with custom sprites (bar frames, slot backgrounds, button styles, panel textures). Proper .tscn scene files for major screens. Custom font with robot/industrial aesthetic. Minimap with fog-of-war and room icons.

---

## Available But Not Integrated

### Modular Robots Pack
- **Location:** `C:\Users\Stu\Downloads\Modular_Robots.zip`
- **Contents:** 79 voxel robot OBJ models with palette textures
- **Potential use:** Player bot frame bodies, enemy variants, boss parts, NPC shopkeeper
- **Integration path:** Extract to `Models/Characters/Player/`, name files to match BotFrameType IDs (e.g., `scrapheap.obj`), and ModelLibrary auto-scanning will pick them up

### Unused Dungeon Props in Models
The following models are imported in `Models/Dungeon/Props/` but not yet referenced in code:
- `capsule.fbx`, `chest.fbx`, `computer.fbx`, `computer_small.fbx`
- `laser.fbx`, `pipes.fbx`, `pod.fbx`, `portal.fbx`
- `shelf_tall.fbx`, `statue.fbx`, `teleporter.fbx`
- `vessel.fbx`, `vessel_short.fbx`, `vessel_tall.fbx`

These could be wired into room decoration logic (shop computers, treasure room chests, event room portals/teleporters, etc.).

### Floor Tile Models
7 floor tile FBX models exist in `Models/Dungeon/Floors/` but `RoomBuilder.BuildTileFloor()` does not attempt to load them -- it always uses the shader path. These need a tiling system to replace the single-plane shader approach.
