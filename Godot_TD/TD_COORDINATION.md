# Vine Logic TD — Coordination Doc

*Last updated: 2026-03-17 — for handoff between Claude instances*

---

## Quick Catchup for New Claude

**What is this game?** A programmable-logic tower defense where your vine network IS the maze. Sensors detect enemies, fire signals along connections to turrets. Each planet has a unique visual theme and enemy AI style.

**Current state:** Full playable alpha with 2 planets, 3 floors per planet, draft system, perk system, meta-perk progression, intro cinematic, real 3D models, and two completely different visual themes (Tron + Scrapyard).

**What was just built (latest session 2026-03-17):**
- Real 3D enemy models wired to factions (scrap_rat, quad_shell, trilobite, spark_drone)
- Real 3D node models for turrets/sensors (KitBash turrets, radar, satellite, generator)
- Signal power budget system (sensors power 3-4 effect nodes, routing nodes free)
- Effect nodes propagate signals through chains
- Connection colors show power state (green=sensor, cyan=powered chain, orange=destination, dim red=unpowered)
- Dense terrain ring around play area with canyon openings at entries
- Planet 2 Scrapyard with PBR textures (rusted metal, damaged concrete, industrial rubble)
- VineGrid is fully planet-aware (different materials per planet)
- Entry regions (multi-cell spawn areas) instead of single-point entries
- VinePlayer, VineHarvester at exit instead of abstract core
- Meta-perk system with save/load persistence
- AABB-based height targeting for model scaling

---

## Architecture

```
Godot_TD/
├── Scripts/
│   ├── Core/           GameManager, ServiceLocator, GameEvents, Constants, Enums
│   ├── VineLogic/      ALL vine TD gameplay code:
│   │   ├── VineGrid.cs              Grid + terrain + node placement
│   │   ├── VineNode.cs              Signal processing for all 18 node types
│   │   ├── VineNodeData.cs          Node type registry (costs, ranges, power)
│   │   ├── VineEnemy.cs             Enemy controller with real 3D models
│   │   ├── VineConnection.cs        Vine connections + signal travel + power checking
│   │   ├── VinePathfinder.cs        A* with ghost mode + terrain cost
│   │   ├── VinePathPreview.cs       Live path visualization
│   │   ├── VineWaveData.cs          Per-floor wave definitions
│   │   ├── VineWaveManager.cs       Wave spawning with floor progression
│   │   ├── VineMapLayouts.cs        3 floor layouts (Gateway/Conduit/Arena)
│   │   ├── VineBattleScene.cs       Battle orchestrator + environment dressing
│   │   ├── VineHUD.cs               HUD with draft-aware build bar
│   │   ├── VinePlacer.cs            Node placement with preview
│   │   ├── VineDraftScreen.cs       Role selection (Scrapwright/Arcanist/Bruteforge)
│   │   ├── VinePerkData.cs          Between-floor perk system
│   │   ├── VinePerkScreen.cs        Perk selection UI
│   │   ├── VinePlayer.cs            Player character on the field
│   │   ├── VineHarvester.cs         Exit point / core replacement
│   │   ├── IntroCinematic.cs        30s intro sequence
│   │   ├── PlanetTheme.cs           Abstract planet theme + TronPlanetTheme
│   │   ├── ScrapyardPlanetTheme.cs  Planet 2 theme with rust shader
│   │   ├── ScrapyardEnvironment.cs  Planet 2 PBR environment (containers, pipes, etc.)
│   │   ├── TronTheme.cs             Planet 1 Tron visuals (800+ lines)
│   │   └── AssetLibrary.cs          Asset loading, scaling, verification
│   ├── Editor/         F12 editor suite
│   │   ├── EditorManager.cs
│   │   ├── EditorStyles.cs
│   │   └── Modules/    NodeBalance, WaveEditor, SignalTuning, AssetSandbox
│   ├── VFX/            VfxFactory, DamageNumber, VineProjectile
│   ├── Commentary/     AXISCommentary
│   ├── Camera/         TDCamera
│   ├── UI/             MainMenuUI
│   └── Debug/          BugReportDialog
├── Scenes/             Main, MainMenu, IntroCinematic, VineDraft, VineBattle, VinePerkSelect, MetaPerk
├── Models/             Imported 3D models (AXIS, Buildings, Turrets, Props, Characters)
├── Materials/Scrapyard/  PBR textures (rusted metal, concrete, rubble, garbage, ground, metal)
├── bugs/               Bug reports with screenshots
└── vine_logic_td_design.md  Full design doc
```

---

## Key Systems

### Signal Chain
`Sensor detects enemy → fires signal (power=3-4) → travels along vine → hits effect node (costs 1 power, activates + propagates) → next effect → ... → power exhausted`

- Routing nodes (Extender, Junction, Switch, Gate) pass signals FREE
- Effect nodes (Turret, SlowField, PushPull) cost 1 power each
- Connections show power state via color

### Planet Themes
- `PlanetTheme.Current` — static reference, set in `GameManager.StartVineBattle()`
- Planet 1: `TronPlanetTheme` — dark + cyan outlines, digital aesthetic
- Planet 2: `ScrapyardPlanetTheme` — PBR rust/metal/concrete, industrial aesthetic
- `VineGrid`, `VineBattleScene`, `VineEnemy`, `VineNode` all check `PlanetTheme.Current`

### Floor Progression
- 3 floors per planet, each with own map layout and wave set
- `GameManager.CurrentFloor` tracks progress
- Between floors: perk selection, gold carries over
- Floor 3 has boss wave

### Models
- `AssetLibrary.InstantiateNormalized(path)` — loads + scales any model
- Character models use AABB-based height targeting (`InstantiateToHeight`)
- All models get planet theme applied automatically
- Procedural fallback if any model fails to load

---

## Game Flow

```
MainMenu → [Planet 1 or 2] → IntroCinematic → VineDraftScreen →
  Floor 1: VineBattle (Gateway, 3 waves) → PerkSelect →
  Floor 2: VineBattle (Conduit, 3 waves) → PerkSelect →
  Floor 3: VineBattle (Arena, 3 waves + boss) → Victory/Defeat →
MainMenu
```

---

## What Needs Work

### Planet 2 Scrapyard (WIP)
- Grid terrain uses scrapyard materials ✅
- Environment ring has scrap piles, containers, smokestacks ✅
- Needs: more density, KitBash structures with real textures not white, smoke/ember particles
- Needs: completely different feel from Tron (no outlines, solid opaque textured surfaces)

### Alpha Remaining
- Sound design (signal fire, turret shot, enemy death)
- Corruption events (AXIS possession mid-wave)
- Balance pass on waves/economy/power

### Post-Alpha
- Planet 3 with military AI enemies
- Campaign mode (multi-planet progression)
- Animated models (CharacterAnimator from crawler project)

---

## Controls

WASD pan, scroll zoom, left-click place, right-click cancel/sell, Space start wave, Tab speed (1x/2x/3x), H help, F12 editor, ESC menu, Ctrl+Shift+B bug report, Ctrl+Shift+K kill all enemies, Ctrl+Shift+G add gold

---

## Build & Run

```
cd Godot_TD && dotnet build
# Open project.godot in Godot 4.6, F5
```
