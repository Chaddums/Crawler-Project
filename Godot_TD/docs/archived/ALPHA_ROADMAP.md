# Vine Logic TD — Alpha Roadmap

*Single source of truth for what ships in the alpha. If it's not on this list, it's post-alpha.*
*Last updated: 2026-03-19*

---

## What IS the Alpha

A single-planet roguelike run: cinematic → draft → 6 floors (3+ waves each) → boss → win/lose. Desktop only, Windows first. Target play time: 25-35 min per run. Replayable via 3 role drafts.

**The game is called Vine Logic TD.**

---

## Status Key

- `DONE` — Implemented and working
- `WIP` — Partially implemented
- `TODO` — Not started, required for alpha
- `CUT` — Explicitly not in alpha

---

## Alpha Checklist

### Core Loop (Non-Negotiable)

| # | Feature | Status | Notes |
|---|---------|--------|-------|
| 1 | Intro cinematic | `DONE` | 30s in-engine, skippable, AXIS obelisk + planet impact |
| 2 | Pre-run draft screen (3 roles) | `DONE` | Scrapwright/Arcanist/Bruteforge, 8 nodes each |
| 3 | 6 floors per planet with escalating waves | `DONE` | Gateway → Conduit → Arena → Forge → Labyrinth → Crucible |
| 4 | Bosses on floors 3 + 6 | `DONE` | Forge Overseer (F3) + Apex Protocol (F6) |
| 5 | Perk selection between floors | `DONE` | VinePerkScreen.cs, choose 1 of 3 |
| 6 | Signal power budget | `DONE` | Sensors power 3-4 effect nodes, routing free |
| 7 | Build → wave → reward loop | `DONE` | VineHUD + VineWaveManager |
| 8 | Win screen + lose screen | `DONE` | VineHUD.ShowEndScreen() |
| 9 | Full run confirmed playable | `DONE` | Draft → 3 floors → boss → victory |

### Content (Minimum Viable)

| # | Feature | Status | Notes |
|---|---------|--------|-------|
| 10 | 6 map layouts with terrain features | `DONE` | Gateway/Conduit/Arena/Forge/Labyrinth/Crucible |
| 11 | Enemy types tuned per faction | `DONE` | 4 factions: Scavenger/Brute/Ghost/Swarm + bosses |
| 12 | 8-10 node types per role via draft | `DONE` | 18 implemented, 8 per role |
| 13 | Sound design (placeholder minimum) | `DONE` | PCM audio synthesis for battle ambience, UI, combat |
| 14 | 3-4 corruption/modifier events | `DONE` | CorruptionManager — AXIS possession, signal jam, overloader |
| 15 | Larger battlefield for enemy variety | `DONE` | 6 floor layouts with varied sizes, Crucible = 4 entries |

### Polish (Ship Quality)

| # | Feature | Status | Notes |
|---|---------|--------|-------|
| 16 | Planet theme system | `DONE` | PlanetTheme base + TronPlanetTheme + ScrapyardPlanetTheme |
| 17 | Asset pipeline + sandbox editor | `DONE` | 42 assets, 3 outline modes, save themed versions |
| 18 | Tron visuals (outline shaders) | `DONE` | Per-mesh, silhouette, no-outline modes |
| 19 | Combat VFX | `DONE` | Projectiles, muzzle flash, hit flash, area pulses |
| 20 | Connection color coding | `DONE` | Green/cyan/orange/blue by type |
| 21 | Path preview + range indicators | `DONE` | Live route lines, sensor/turret range circles |
| 22 | Fog/atmosphere | `DONE` | Tron fog banks + horizon walls + terrain ring |
| 23 | AXIS commentary | `DONE` | Wave start, tower place, leak, clear |
| 24 | Help overlay | `DONE` | H key, controls + wiring guide |
| 25 | Bug reporter | `DONE` | Ctrl+Shift+B with screenshot snip |
| 26 | Floor intro flyover | `DONE` | 5s cinematic orbit + letterbox + forced harvester placement |
| 27 | Mining Building placement flow | `DONE` | Forced before waves, auto-selects ghost, magic type popup |
| 28 | Conversion dome + BIT visuals | `DONE` | Dome material swap, clipping fixed |

### Known Bugs (From Playtests)

| # | Bug | Severity | Notes |
|---|-----|----------|-------|
| B1 | ~~Towers stop firing~~ | RESOLVED | Signal chain confirmed working |
| B2 | ~~Signal doesn't propagate through turret chains~~ | RESOLVED | Effect nodes now propagate |
| B3 | ~~Connection colors wrong for turret chains~~ | RESOLVED | Cyan for effect-to-effect |
| B4 | Fog shader warning (INSTANCE_CUSTOM) | LOW | Godot shader compile warning, fog works |

---

## Enemy Behavior Per Planet (Design Decision 2026-03-17)

| Planet | AI Style | Spawn Behavior | Battlefield Size |
|--------|----------|---------------|-----------------|
| 1: Grid Prime | **Circuit** — predictable, pattern-based | Fixed entry points, orderly lines | Current (20x14) |
| 2: Scrapyard | **Mercenary** — group-based, reactive | Squads from off-screen edges | Larger (28x18+) |
| 3: TBD | **Military** — intelligent, strategic | Scouts → flanks → coordinated assault | Largest (32x22+) |

This escalation means:
- Planet 1 teaches the system with predictable enemies
- Planet 2 breaks predictability — squads arrive from unexpected directions
- Planet 3 enemies actively exploit weaknesses in your network

---

## Explicitly CUT for Alpha

Do not implement these. They are post-alpha:

- Meta progression / persistent unlocks
- HeroBot system
- Campaign mode (multi-planet)
- Multiplayer
- Save/load mid-run
- Steam / console builds
- Magic system / elemental types
- Relic/core socket system
- Unique enemy celebration system
- Node HP + enemy attacks on network
- Post-wave tips system
- Animated enemy/player models
- Building builder editor tool

---

## Status: ALPHA COMPLETE

All features implemented. All bugs resolved and archived. Visual juice and balance pass done.

No remaining TODO items for alpha ship.

---

## Key Files

| Purpose | File |
|---------|------|
| Design doc (full vision) | `vine_logic_td_design.md` |
| This roadmap | `ALPHA_ROADMAP.md` |
| Architecture | `TD_COORDINATION.md` |
| Meeting decisions | `meeting_agenda.md` |
| Bug reports | `bugs/` directory |

## Key Code Paths

| System | Entry Point |
|--------|------------|
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
| Asset library | `Scripts/VineLogic/AssetLibrary.cs` |
| Perk system | `Scripts/VineLogic/VinePerkData.cs` |
| F12 editor | `Scripts/Editor/` |
| AXIS commentary | `Scripts/Commentary/AXISCommentary.cs` |
| Intro cinematic | `Scripts/VineLogic/IntroCinematic.cs` |

---

## Build & Run

```
cd Godot_TD && dotnet build
# Open Godot_TD/project.godot in Godot 4.6, F5
```

**Controls:** WASD pan, scroll zoom, left-click place, right-click cancel/sell, Space start wave, Tab speed, H help, F12 editor, ESC menu, Ctrl+Shift+B bug report, Ctrl+Shift+K kill all enemies, Ctrl+Shift+G add gold
