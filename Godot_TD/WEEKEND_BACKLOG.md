# Vine Logic TD — Weekend Sprint Backlog

*Goal: Shippable, fun-to-play game by Monday. 2 developers + 3 Claude Code instances.*
*Created: 2026-03-19*

---

## Current State Summary

**What works well:** Full game loop (menu → cinematic → draft → 6 floors → perks → victory/defeat), 18 node types with real signal processing, 4 enemy factions, 2 planet themes, mining building placement, corruption events, flyover cinematic, meta-perk tree, debug tools, code-built UI everywhere.

**Critical gaps:** Planet 2 has no wave data (uses P1 fallback), no music at all, PushPull node is a no-op, main menu has dev buttons, only 4 enemy types across 12 floors, victory/defeat screens are bare, audio reuse is extreme, no tutorial, no settings screen. All 4 level JSON files have empty prop/asset/light arrays (maps look bare). Channel and DataStream terrain types are defined but never placed. Scrapwright role has only 1 sensor (weak). 19 prop GLB models and 3+ enemy FBX models sit unused.

---

## Day 1 (Friday) — Foundation & Critical Fixes

*Focus: Make the existing game work correctly end-to-end with real content.*

### P0 — Must Do (Blocking)

| # | Task | Size | Files | Notes |
|---|------|------|-------|-------|
| 1.1 | **Planet 2 wave data** — Create P2-F1.json through P2-F6.json with Scrapyard-themed enemies | M | `Data/Waves/P2-F*.json` | Use existing enemy types but different names/stats. Scrapyard should feel like mercenary squads — more multi-entry surges, tighter timing, group spawns. Scalers should be slightly harder than P1. |
| 1.2 | **Fix PushPull node** — Currently calls ActivateEffect but never actually pushes/pulls enemies | S | `Scripts/VineLogic/VineNode.cs` | Add `UpdatePushPull()` method. Find enemies in range, apply velocity toward (pull) or away from (push) node. Toggle push/pull on signal. Bruteforge role has this — it must work. |
| 1.3 | **Fix TypeSensor** — Triggers on ANY enemy, should filter by configured faction | S | `Scripts/VineLogic/VineNode.cs`, `VineNodeData.cs` | Add `TargetFaction` field to sensor config. UI to select which faction to detect. Arcanist role depends on this. |
| 1.4 | **Main menu cleanup** — Remove "Classic TD" and "Level Editor" buttons, add simple background | S | `Scripts/UI/MainMenuUI.cs` | Keep Planet 1, Planet 2, Quit. Add a starfield or dark gradient BG. Consider adding meta-perk tree access. |
| 1.5 | **Play-test full run P1** — Play through all 6 floors, note every issue | M | Various | Document balance issues, crashes, soft-locks, confusing moments. This informs all other work. |
| 1.6 | **Populate level JSON props/lights** — All 4 level JSONs have empty props/assets/lights arrays | M | `Data/Levels/floor_*.json` | 19 prop GLBs exist (Antenna, Barrel, Container, Crate, Generator, etc.) — place them in levels. Add OmniLight3D entries. Maps look bare without them. |
| 1.7 | **Fix Scrapwright draft** — Only has 1 sensor (ProximitySensor), making the role one-dimensional | S | `Scripts/VineLogic/VineDraftScreen.cs` | Add Timer to Scrapwright's pool (replace Delay or Inverter). Timer + Gate creates timed-gate-toggle combos which is the whole point of the "maze builder" role. |

### P1 — Should Do (Important for Fun)

| # | Task | Size | Files | Notes |
|---|------|------|-------|-------|
| 1.8 | **Background music** — At minimum: menu loop, build phase loop, wave phase loop | M | `Audio/Music/`, `Scripts/Audio/AudioManager.cs`, `Data/audio.json` | Can use royalty-free tracks or generate ambient loops. AudioManager already has Music bus. |
| 1.9 | **Wire DifficultyScaler** — HP/speed/count multipliers should actually apply to spawned enemies | S | `Scripts/VineLogic/VineWaveManager.cs`, `DifficultyScaler.cs` | Scaler is registered but only surge spawn multiplier is read. Apply HP/speed/count on `SpawnEnemy()`. |
| 1.10 | **Intro cinematic for Scrapyard** — Current cinematic is Tron-only | M | `Scripts/VineLogic/IntroCinematic.cs` | At minimum, swap surface materials to Scrapyard theme when CurrentPlanet == 2. Planet surface section needs ScrapyardEnvironment. |
| 1.11 | **Balance pass** — Adjust starting scrap, node costs, enemy HP/speed based on play-test | S | `Constants.cs`, `Data/Waves/*.json` | Starting scrap (90) may be too low/high. IFF Scanner overpriced at 8 (reduce to 6), Timer underpriced at 6 (bump to 8). DamageTower cost (15) vs Extender (3) — is the ratio right? |
| 1.12 | **Add Channel/DataStream terrain to layouts** — Both terrain types are defined in code but never placed in any map | S | `Scripts/VineLogic/VineMapLayouts.cs`, `Data/Levels/*.json` | DataStream gives enemies +50% speed (strategic risk/reward). Channel slightly preferred by enemies. Add to floors 3-6 for strategic depth. |

---

## Day 2 (Saturday) — Content & Game Feel

*Focus: Make the game feel complete and satisfying. More variety, better feedback.*

### P0 — Must Do

| # | Task | Size | Files | Notes |
|---|------|------|-------|-------|
| 2.1 | **New enemy types for P2** — Add 2-3 enemies using existing FBX models | M | `Scripts/VineLogic/VineEnemy.cs`, wave JSONs | Models exist: wire_worm, volt_sprinter, overclock_drone, rust_titan, shard_lobber. Map to factions, add to P2 wave data. Needs model-to-faction mapping in VineEnemy visual code. |
| 2.2 | **Tutorial / first-play experience** — Guided Floor 1 with tooltip prompts | M | `Scripts/VineLogic/VineHUD.cs` or new `TutorialManager.cs` | Show "Place a Sensor near the entry" → "Connect it to a Tower" → "Start the wave!" tooltips on first run. Track with a flag in MetaPerkSave. |
| 2.3 | **Victory/defeat screen improvements** — Add score, stats, transition | M | `Scripts/VineLogic/VineHUD.cs` | Add: total scrap earned, nodes built, time played, damage dealt, meta-perk points earned. "Play Again" and "Next Planet" buttons on victory. |
| 2.4 | **Corruption variants** — Add Gate Scramble and Sensor Jam | S | `Scripts/VineLogic/CorruptionManager.cs` | Hooks already exist: `VineNode.ForceToggleGate()` for gate scramble, `VineNode.IsJammed` for sensor jam. Just need CorruptionManager to invoke them. Rotate randomly between AxisChaos, GateScramble, SensorJam. |

### P1 — Should Do

| # | Task | Size | Files | Notes |
|---|------|------|-------|-------|
| 2.5 | **More perks** — Expand from 13 to 20+ | S | `Scripts/VineLogic/VinePerkData.cs` | Ideas: "Signal Amplifier" (+1 power budget), "Quick Build" (-20% costs), "Danger Pay" (+50% scrap from bosses), "Chaos Attunement" (+scrap during corruption), "Extra Timer" (+5s wave prep). Need 2-3 perks that change gameplay, not just +% stat bumps. |
| 2.6 | **Create floor_5.json and floor_6.json** — These floors exist only as hardcoded layouts | S | `Data/Levels/floor_5.json`, `floor_6.json` | Export from hardcoded VineMapLayouts data. Populate props/assets/lights arrays. Makes them editable in level editor. |
| 2.7 | **Audio variety** — Replace reused WAVs with distinct sounds per event | M | `Audio/SFX/`, `Data/audio.json` | Priority: node_place, node_sell, wave_start, wave_complete, boss_spawn need distinct sounds. Currently ~7 events share pickup.wav. |
| 2.8 | **Commander behaviors** — Implement AuraBuffer and Rally | M | `Scripts/VineLogic/VineWaveManager.cs`, `VineEnemy.cs` | AuraBuffer: +25% HP to nearby enemies. Rally: +30% speed to nearby. Both use radius check in _PhysicsProcess. |
| 2.9 | **Wave preview** — Show upcoming wave composition in HUD | S | `Scripts/VineLogic/VineHUD.cs` | During build phase, show enemy icons + counts for next wave. Read from `_floorWaves[_currentWaveInFloor]`. |
| 2.10 | **Node info panel** — Click existing node to see stats/connections | S | `Scripts/VineLogic/VineHUD.cs`, `VinePlacer.cs` | Show: type, health, DPS, connections, signal count. Right-click to sell (already works). |
| 2.11 | **Screen transitions** — Fade between scenes | S | Various scene files | Add a global fade overlay. Fade out before scene change, fade in after load. |
| 2.12 | **Use turret models for DamageTower** — 7 turret GLB models exist but DamageTower uses procedural mesh | S | `Scripts/VineLogic/VineNode.cs`, `AssetLibrary.cs` | Map turret models (PlasmaGun, MultiRocketLauncher, Turret_A/B/C) to DamageTower visual. Instant visual upgrade. |

---

## Day 3 (Sunday) — Polish, Performance & Ship Prep

*Focus: Bug fixes, optimization, final balance, build export.*

### P0 — Must Do

| # | Task | Size | Files | Notes |
|---|------|------|-------|-------|
| 3.1 | **Full play-test both planets** — Complete run P1 + P2, fix every issue found | L | Various | Both developers play through independently. Log issues. Fix showstoppers. |
| 3.2 | **Final balance pass** — Adjust all numbers based on play-testing | M | `Constants.cs`, `Data/Waves/*.json`, `VineNodeData.cs` | Economy curve, difficulty curve, perk power, ability values. Should feel challenging but fair on first try. |
| 3.3 | **Performance optimization** — ConversionDome frame rebuilds, scene tree walks | M | `Scripts/VineLogic/ConversionDome.cs` | `RebuildAll()` runs every frame with SurfaceTool. Cache meshes, only rebuild on radius/position change. `ConvertSceneChildren()` walks entire tree — use spatial tracking instead. |
| 3.4 | **Strip debug features for release** — Hide/disable dev shortcuts | S | `Scripts/Debug/DebugMenu.cs`, `VineHUD.cs` | Disable Ctrl+Shift+K (kill all), Ctrl+Shift+G (+gold) in release. Keep F12 editor accessible but hidden. Add `#if DEBUG` guards or a const flag. |
| 3.5 | **Export build** — Godot export for Windows (and web if possible) | M | `project.godot`, export presets | Configure export preset, test exported build, verify all assets load correctly. |

### P1 — Should Do (If Time)

| # | Task | Size | Files | Notes |
|---|------|------|-------|-------|
| 3.6 | **Settings screen** — Volume sliders (SFX, Music), fullscreen toggle | M | New `SettingsScreen.cs` | AudioManager has SFX/Music/Voice buses. Just need UI sliders wired to bus volume. |
| 3.7 | **Floating damage numbers** — Show damage dealt to enemies | S | New or `VfxFactory.cs` | Spawn Label3D at hit position with damage value, float upward, fade out. |
| 3.8 | **LoopAnchor implementation** — Currently toggle-only, no actual loop routing | M | `Scripts/VineLogic/VineNode.cs`, `VinePathfinder.cs` | Needs pathfinder integration to create circular paths around anchor. Design decision needed. |
| 3.9 | **Perk history display** — Show active perks on selection screen and HUD | S | `VinePerkScreen.cs`, `VineHUD.cs` | Small icons or text list showing what you've already picked this run. |

---

## Work Distribution Guide (3 Claude Instances)

### Claude A — Gameplay & Balance
- Wave data creation (P2-F1 through P2-F6)
- PushPull/TypeSensor fixes
- DifficultyScaler wiring
- Balance passes
- New enemy types
- Commander behaviors

### Claude B — Content & Polish
- Music integration
- Audio variety
- Tutorial system
- Victory/defeat improvements
- Perks expansion
- Corruption variants
- Screen transitions

### Claude C — Systems & Ship Prep
- Main menu cleanup
- Settings screen
- Performance optimization (ConversionDome)
- Debug stripping
- Export build
- Wave preview HUD
- Node info panel

### Human Developers
- Play-testing (critical — machines can't evaluate "fun")
- Final balance decisions
- Music/SFX sourcing (royalty-free or AI-generated)
- Art direction calls
- Ship/deploy decisions

---

## Size Legend

- **S** = Small (< 1 hour, single file, straightforward)
- **M** = Medium (1-3 hours, 2-4 files, some design decisions)
- **L** = Large (3+ hours, many files, significant new functionality)

---

## Scope Cuts (Don't Do This Weekend)

These are explicitly OUT OF SCOPE for the weekend sprint:

- Planet 3 (no theme, no content — ship with 2 planets)
- 2nd/3rd characters (ship with BIT only)
- Magic Shop (Magic accumulates but isn't spent — OK for now)
- Non-attacker's second Mining Building
- Save/load mid-run
- Multiplayer
- Localization
- Mobile export
- Credits screen (add "Made by [names]" to main menu instead)
- Web build (nice-to-have but not required)
