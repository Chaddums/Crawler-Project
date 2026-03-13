# Junkyard TD — Coordination Doc

*Last updated: 2026-03-13*

## Overview

Tower defense game set in the Junkbot Arena universe. Same repo, sibling Godot project (`Godot_TD/`). Shares lore, characters (AXIS, BIT), enemy concepts, and eventually models/audio with the roguelike.

**Engine:** Godot 4.6 (.NET/C#), net8.0
**Namespace:** `JunkyardTD`
**Status:** Core loop playable — 10 waves, 8 tower types, 8 enemy types, placement + pathing + combat all functional.

---

## Design Pillars (from td_design_pillars.md)

1. **Scrap Economy** — enemies drop scrap, it degrades on the ground, collect manually or via Recyclers
2. **Fabrication Combos** — mod components combine for emergent tower upgrades (not flat upgrade trees)
3. **Terrain as Resource** — bulldoze/pile debris to reshape enemy paths
4. **Bot Personality** — towers have names, silhouettes, readable roles
5. **Escalating Enemy Salvage** — late-wave enemies use scrap from your kills as armor
6. **Physical Presence** — hero bot you directly control on the field

---

## Architecture

```
Godot_TD/
├── Scripts/
│   ├── Core/         GameManager, ServiceLocator, GameEvents, StatBlock, BattleScene, Constants, Enums
│   ├── Map/          MapGrid, Pathfinder (A*), MapBuilder
│   ├── Towers/       TowerData/Registry, TowerController, TowerProjectile, TowerPlacer
│   ├── Enemies/      EnemyData/Registry, EnemyController
│   ├── Waves/        WaveData/Registry, WaveManager
│   ├── Economy/      ScrapManager
│   ├── Fabrication/  FabricationSystem
│   ├── Camera/       TDCamera
│   ├── UI/           HUD, MainMenuUI
│   ├── Commentary/   AXISCommentary
│   ├── HeroBots/     (empty — planned)
│   ├── VFX/          (empty — planned)
│   ├── Audio/        (empty — planned)
│   └── Debug/        (empty — planned)
├── Scenes/
│   ├── Main.tscn         GameManager autoload
│   ├── MainMenu.tscn     Title screen
│   └── Battle.tscn       Battle orchestrator
└── Data/                 (empty — will hold JSON configs)
```

Same patterns as Junkbot Arena: ServiceLocator for DI, GameEvents static event bus, StatBlock for stat stacking, BattleScene as the per-battle orchestrator.

---

## What's Built (v0.1 — 2026-03-13)

| System | Status | Notes |
|--------|--------|-------|
| Grid map (24x16) | Done | Ground plane, grid lines, coordinate conversion |
| A* pathfinding | Done | Cached paths, dirty flag, maze validation |
| Tower placement | Done | Ghost preview, validity check, shift-click for multi-place |
| 8 tower types | Done | Blaster, Scatter, Zapper, Incinerator, Freezer, Mortar, Rail Driver, Recycler |
| Tower targeting + firing | Done | Closest-enemy targeting, homing projectiles |
| 8 enemy types | Done | ScrapRat, WireWorm, RustHulk, SparkDrone (flying), ScrapThief, ShieldBearer, Bomber, Fabricator |
| Enemy pathing | Done | Follow A* path, flying enemies go direct |
| 10 waves | Done | Multi-group spawning, escalating composition |
| Scrap economy | Done | Drop on kill, decay timer (15s), click to collect, Recycler auto-collect |
| Fabrication system | Done | Component inventory, combo recipes, mod attachment to towers |
| HUD | Done | Scrap, lives, wave counter, phase label, tower build bar, speed toggle |
| AXIS commentary | Done | Snarky lines on wave start, tower place, enemy leak, wave clear |
| Camera | Done | Top-down, WASD pan, scroll zoom, map clamping |
| Lighting + environment | Done | Directional + fill lights, filmic tonemap, glow |

---

## Next Up — Priority Order

### 1. Tower Sell + Inspect UI
**What:** Right-click a placed tower to open a radial/popup with Sell, Inspect (stats), and Attach Mod options.
**Why:** Players need to be able to recover scrap from bad placements and see tower stats. Also the entry point for the fabrication combo system (Pillar #2) to become player-facing.
**Approach:**
- Add a `TowerInspector` script that raycasts on right-click to find towers
- Show a small popup panel: tower name, stats (damage/range/speed), mod slots, sell button
- Sell refunds 60% (already defined in Constants)
- Mod slot buttons open the fabrication component picker

### 2. Terrain Manipulation UI
**What:** Click debris cells to bulldoze (open path) or pile (block path). Costs scrap.
**Why:** Pillar #3 — terrain as a first-class mechanic. The grid already supports Debris→Open and Debris→Blocked mutations, but there's no player-facing way to trigger them.
**Approach:**
- Add a terrain mode toggle (T key, already mapped)
- In terrain mode, hovering over Debris shows bulldoze/pile options
- Bulldoze costs 5 scrap (opens path, enemies get a shorter route)
- Pile costs 10 scrap (blocks path, forces enemies to detour)
- Pathfinder already recalculates on terrain change

### 3. Visual Juice — Death Effects, Hit Flash, Projectile Trails
**What:** Enemies pop on death (particle burst + scrap scatter), towers flash on fire, projectiles leave trails, screen shake on big kills.
**Why:** The game plays correctly but feels flat. VFX sells the fantasy of "improvised ingenuity under pressure."
**Approach:**
- `VfxFactory` (like Junkbot Arena's) for particle bursts
- Enemy death: spawn 3-5 small mesh fragments that fly outward and fade
- Projectile trails: `ImmediateMesh` line segments or GPUParticles3D
- Tower muzzle flash: brief emissive spike on barrel mesh
- Screen shake: camera offset with decay, triggered by OnEnemyKilled for elites/bosses

### 4. Hero Bot (Pillar #6)
**What:** A controllable bot the player deploys onto the field. Can repair towers, collect scrap, body-block enemies.
**Why:** This is the differentiator from every other TD — "physical presence in the world." Transforms the game from pure god-view to partially visceral.
**Approach:**
- `HeroBotController` extends CharacterBody3D — WASD/click movement on the grid
- Abilities: Repair (restores tower HP), Overclock (temporary fire rate boost to nearby towers), Slam (AoE knockback on enemies)
- Deploy/recall with a hotkey (R)
- Costs scrap to deploy, refunded partially on recall
- If the hero bot dies, it's recalled automatically with a cooldown

### 5. Shared Asset Integration
**What:** Symlink or copy models, audio, and icons from `Godot/` so towers and enemies use real 3D assets.
**Why:** Both games benefit from the same art investment. Enemies like ScrapRat and RustHulk already have models in the roguelike.
**Approach:**
- Symlink `Godot/Models/Characters/Enemies/` → `Godot_TD/Models/Enemies/`
- Symlink `Godot/Audio/` → `Godot_TD/Audio/`
- Symlink `Godot/Icons/` → `Godot_TD/Icons/`
- Add a `ModelLibrary` to TD that loads FBX/GLB for towers and enemies
- Tower models: use KitBash3D turret/weapon props already in `Godot/Models/Dungeon/Hazards/`

### 6. More Maps + Procedural Generation
**What:** Additional hand-crafted maps and/or a procedural map generator.
**Why:** Replayability. One map gets solved.
**Approach:**
- JSON map format: grid dimensions, spawn points, core position, pre-placed terrain
- MapBuilder reads JSON instead of hardcoding
- Procedural option: random debris/wall placement with path validation
- Map select screen between MainMenu and Battle

### 7. Difficulty Tuning + Scaling
**What:** Enemy HP/speed/count scaling per wave, difficulty selector, endless mode.
**Why:** Wave 10 was completable on first try — needs to ramp harder, and endgame players need infinite scaling.
**Approach:**
- Wave scaling formula: HP *= 1 + (wave * 0.15), speed *= 1 + (wave * 0.05)
- Difficulty presets: Scrapyard (easy), Junkyard (normal), Wasteland (hard)
- Endless mode: procedurally generate waves after wave 10 with increasing scaling

### 8. Save/Load + Meta-Progression
**What:** Save mid-run state, persistent unlocks across runs.
**Why:** Players shouldn't lose progress on quit. Meta-progression gives long-term motivation.
**Approach:**
- Mirror Junkbot Arena's SaveManager pattern (JSON to user://)
- SaveData: current wave, tower positions + mods, scrap, core lives
- MetaSaveData: towers unlocked, maps unlocked, highest wave reached, total scrap earned
- Unlock new tower types and mod components through meta-progression

---

## Shared Universe Hooks

| Element | In Junkbot Arena | In Junkyard TD |
|---------|-----------------|----------------|
| AXIS | Dungeon master AI antagonist | Commentary during waves |
| BIT | Companion drone | Could be the hero bot or an advisor |
| Scrap | Currency for crafting | Primary economy resource |
| Bot Frames | 6 playable classes | Tower archetypes or hero bot classes |
| Enemies | 16 types in dungeon | 8 types on lanes (shared models) |
| DamageType | 7 types | Same 7 types |
| Lore | Arena run by AXIS | Junkyard defended against AXIS raids |

---

## Quick Reference

**Build:** `cd Godot_TD && dotnet build`
**Run:** Open `Godot_TD/project.godot` in Godot 4.6, F5
**Controls:** WASD pan, scroll zoom, left-click place tower, right-click cancel, Space start wave, Tab speed toggle
