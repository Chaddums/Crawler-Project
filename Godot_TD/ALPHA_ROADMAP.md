# Vine Logic TD — Alpha Roadmap

*Single source of truth for what ships in the jam alpha. If it's not on this list, it's post-jam.*
*Last updated: 2026-03-16*

---

## What IS the Alpha

A single roguelike run: draft screen → 10 waves → win/lose. Desktop only, Windows first. Target play time: 25-35 min per run. Replayable via 3 role drafts and multiple maps.

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
| 1 | Pre-run draft screen (3 roles, pick 1) | `DONE` | VineDraftScreen.cs — Scrapwright/Arcanist/Bruteforge |
| 2 | 10 waves balanced start to finish | `DONE` | VineWaveRegistry, tuning ongoing |
| 3 | Build → wave → reward loop | `DONE` | VineHUD + VineWaveManager |
| 4 | Win screen + lose screen | `DONE` | VineHUD.ShowEndScreen() |
| 5 | Intro cinematic | `DONE` | IntroCinematic.cs, skippable |
| 6 | Sound design (placeholder minimum) | `TODO` | Signal fire, gate open, turret shot, enemy death, wave start/end |

### Content (Minimum Viable)

| # | Feature | Status | Notes |
|---|---------|--------|-------|
| 7 | 3 map layouts | `WIP` | 1 done (scrapyard). Need 2 more with different spawn/core positions |
| 8 | Enemy types tuned per faction | `DONE` | 4 factions: Scavenger/Brute/Ghost/Swarm, 8 enemy types |
| 9 | 8-10 node types on build bar | `DONE` | 18 implemented, 8 per role via draft |
| 10 | 3-4 corruption/modifier events | `TODO` | One per run randomly. Mid-wave twists (AXIS possession, signal jam, etc.) |

### Polish (Ship Quality)

| # | Feature | Status | Notes |
|---|---------|--------|-------|
| 11 | Tron-ify unit visuals (friendly + enemy) | `WIP` | Faction colors done (blue=player, red=enemy). Need: wireframe edges, grid-line overlays, emissive circuit traces on meshes |
| 12 | Visual juice — death effects, hit flash | `WIP` | Basic VFX exist. Need: death pops, projectile trails, screen shake |
| 13 | Fog/atmosphere | `WIP` | 6 bug reports on fog quality. Needs Tron-style grid clumps, less grey |
| 14 | AXIS commentary lines | `DONE` | AXISCommentary.cs — wave start, tower place, leak, clear |
| 15 | Help overlay / tutorial clarity | `DONE` | H key overlay. May need update for draft roles |
| 16 | Bug reporter | `DONE` | Ctrl+Shift+B |

### Known Bugs (From Playtests)

| # | Bug | Severity | Notes |
|---|-----|----------|-------|
| B1 | ~~Towers stop firing in wave 2+~~ | RESOLVED | Fixed — confirmed 6 waves firing correctly |
| B2 | Lives system confusing to new players | MEDIUM | Help overlay may need clearer explanation |
| B3 | Fog looks like grey squares | LOW | Aesthetic, not gameplay-blocking |

---

## Explicitly CUT for Alpha

Do not implement these. They are post-jam:

- Meta progression / persistent unlocks
- HeroBot system (code exists, not needed)
- Full 18 node types on build bar (8 per role is enough)
- Campaign mode (roguelike only)
- Multiplayer
- Save/load mid-run
- Steam / console builds
- Building builder editor tool
- Magic system / elemental types
- Relic/core socket system
- Unique enemy celebration system
- Node HP + enemy attacks on network
- Post-wave tips system
- Animated enemy/player models (use existing procedural meshes)
- Tower sell/inspect UI
- Terrain manipulation UI

---

## Suggested Work Order

Priority order — each item unblocks the next or improves the weakest link:

### Tier 1: Fix What's Broken
1. **Fix wave 2+ activation bug** (B1) — Core loop is broken if towers don't fire
2. **Wave balance pass** — Make sure 10 waves ramp properly, not beatable first try

### Tier 2: Missing Content
3. **2 more map layouts** — Different spawn/core positions, different terrain
4. **Sound design** — Even procedural PCM sounds (project already has GenerateImpactBoom patterns)
5. **3-4 corruption events** — AXIS Broadcast mid-wave, signal jammer, overloader, etc.

### Tier 3: Polish
6. **Visual juice** — Enemy death pops, projectile trails, screen shake
7. **Fog/atmosphere cleanup** — Tron-style fog clumps, camera clip-through
8. **Final balance + bug fix pass**

---

## Key Files

| Purpose | File |
|---------|------|
| Design doc (full vision) | `vine_logic_td_design.md` |
| Architecture + what's built | `TD_COORDINATION.md` |
| Meeting decisions | `meeting_agenda.md` |
| Bug reports | `bugs/` directory |
| This roadmap | `ALPHA_ROADMAP.md` |

## Key Code Paths

| System | Entry Point |
|--------|------------|
| Game flow | `Scripts/Core/GameManager.cs` |
| Draft screen | `Scripts/VineLogic/VineDraftScreen.cs` |
| Battle scene | `Scripts/VineLogic/VineBattleScene.cs` |
| HUD + build bar | `Scripts/VineLogic/VineHUD.cs` |
| Node definitions | `Scripts/VineLogic/VineNodeData.cs` |
| Wave definitions | `Scripts/VineLogic/VineWaveRegistry.cs` |
| Enemy spawning | `Scripts/VineLogic/VineWaveManager.cs` |
| Signal system | `Scripts/VineLogic/VineSignalManager.cs` |
| AXIS commentary | `Scripts/Commentary/AXISCommentary.cs` |
| Tron visual theme | `Scripts/VineLogic/TronTheme.cs` |
| Constants | `Scripts/Core/Constants.cs` |
| Enums | `Scripts/Core/Enums.cs` |
| Map grid | `Scripts/VineLogic/VineMapBuilder.cs` |
| F12 editor | `Scripts/Editor/` |

---

## Build & Run

```
cd Godot_TD && dotnet build
# Open Godot_TD/project.godot in Godot 4.6, F5
```

**Controls:** WASD pan, scroll zoom, left-click place, right-click cancel/sell, Space start wave, Tab speed, H help, F12 editor, ESC menu
