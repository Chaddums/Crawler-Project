# JunkyardTD — Claude Code Context Document

## Project Overview

**Engine:** Godot 4 (C#)  
**Namespace:** `JunkyardTD`  
**Active mode:** Vine Logic TD (`VineBattle.tscn`)  
**Inactive / sandbox mode:** Classic TD (`Battle.tscn`) — do not iterate on this unless explicitly asked

---

## Active Game: Vine Logic TD

A tower defense game where towers are logic circuit nodes connected by vines. The player controls a MOBA-style character (WASD movement, auto-attacks, 3 abilities: Q/E/R) who exists on the map during waves.

---

## Canonical Hierarchy

```
Run
└── Planet       (1–3)
    └── Floor    (1–3)
        └── Wave (1–6)
            └── Surge   (dynamic count, no upper limit)
                └── Enemy
```

### Address Format
All internal references, debug logs, and data files use:

`P1-F2-W4-S3` = Planet 1, Floor 2, Wave 4, Surge 3

- Floor and Wave are the only levels shown in player-facing UI
- Surge count is a dynamic array — never hardcode surge count assumptions

### Canonical Terminology
| Use This | Never This |
|---|---|
| Surge | group, spawn group, sub-wave, pack |
| Wave | round, stage |
| Floor | level, zone |
| Planet | world, map |
| Run | session, game |
| Commander | miniboss, special |
| Core | base, nexus, goal |
| Scrap | coins, credits, gold, currency |
| Magic | mana, energy, power (as a currency) |

---

## Terminology (Applied Codebase-Wide)

All renames have been applied:
- `VineSpawnGroup` → `SurgeData`, `.Groups` → `.Surges`
- `Gold` → `Scrap` everywhere (code, UI, events, constants)
- `Mana` → `Magic` everywhere

---

## Resource System (Implemented)

The game has two resources. Scrap + Magic replace the old Gold placeholder.

### Scrap
- Universal resource, always collectable
- Funds vine node placement, infrastructure, terrain manipulation
- Collected passively from the Mining Building or dropped by enemies

### Magic
- Harvested resource, gated by player choice
- Does **not** function as a spendable currency in the traditional sense
- Accumulates over time and unlocks/purchases upgrades in a **per-floor shop**
- Shop options are deterministic per floor per planet — players can learn upgrade paths across runs
- Has a visible accumulation meter (visual design TBD)

---

## The Core Strategic Axis

**Mine Scrap** → invest in vine nodes and infrastructure → your *units* carry you  
**Mine Magic** → invest in player character abilities → your *character* carries you

This is the central tradeoff of every run. The Mining Building toggle and all three characters are designed around this axis.

---

## Mining Building

A placeable building that is the primary source of both resources. Key rules:

- One building per magic type — one building total per character (non-attacker can place two)
- Placed by the player — prompts magic type selection on first placement after Floor 1
- **Toggle mechanic:** During the Build phase, the player switches the building between:
  - **Scrap mode** — passive Scrap generation
  - **Magic mode** — passive Magic generation
- Cannot produce both simultaneously
- The toggle decision each floor is a direct expression of the Scrap vs. Magic strategic axis

---

## Magic Types

Three magic types exist. They are **planet-agnostic** — all three are available on every planet with equal weight.

### Psychic
Two internal build paths:
- **Mind:** mess with enemy AI — confusion, misdirection, overcharging friendly units into frenzy
- **Corrosive:** poison/acid effects, degrades enemy armor and structures over time

Common thread: *entropy* — things breaking down or behaving unexpectedly.

### Power
Atmospheric overcharging:
- Extends ranges of abilities and vine node effects
- Amplifies ability output and vine node signal strength
- Supercharges existing systems rather than introducing new ones

Common thread: *amplification* — making existing things hit harder or reach further.

### Environment
Deconstruct and reconstruct the map itself:
- Break down terrain features into harvestable resources
- Use the environment as weapon or shield
- Walls become resources, resources become walls

Common thread: *manipulation of physical space*.

---

## Characters

Three characters. **Single-player only** — co-op was considered and walked away from.

### Combat Characters (×2)
- Locked to **one magic type** per run, chosen at mining building placement
- Single magic = **double resource rate** from the building
- Strong early game curve, but power scaling tapers late — enemy difficulty assumes two magic trees by endgame
- Strategic identity: commit to Scrap or Magic; difficult to do both meaningfully

### Non-Attacker (×1)
- Can mine **two magic types**
- Second magic can be chosen at any time during the run:
  - Taking one magic early = double rate, strong early curve, less flexibility late
  - Waiting for a second = slower start, but more options when difficulty spikes
- Focuses primarily on **unit buffs and enemy debuffs** via magic abilities
- Strategic identity: the force multiplier — shapes battlefield conditions while combat characters execute

### Late Game Power Curve
Late game is designed around **epic units and escalating enemy power**. A player who stays on one magic type exhausts their upgrade tree faster, and enemy scaling assumes two magic trees are active by endgame. This is a soft power ceiling, not a hard lockout — you can still play, but the margin shrinks.

---

## Magic Shop

- Available per floor during the Build phase
- Upgrade options are fixed per floor per planet (deterministic, learnable across runs)
- Upgrades affect player character abilities, vine nodes, or both — mix varies by magic type
- Purchased with accumulated Magic resource

---

## Key Files

| File | Purpose |
|---|---|
| `Scripts/Core/Enums.cs` | All enums — authoritative source for types |
| `Scripts/Core/Constants.cs` | All constants — do not hardcode values found here |
| `Scripts/Core/GameManager.cs` | Singleton. Tracks planet, floor, wave, scrap/gold, game phase |
| `Scripts/Core/GameEvents.cs` | Static event bus — use for cross-system communication |
| `Scripts/VineLogic/VineWaveData.cs` | Surge/Wave data definitions and registry |
| `Scripts/VineLogic/VineWaveManager.cs` | Spawning logic and wave completion handling |
| `Scripts/VineLogic/VinePlayer.cs` | Player character — MOBA abilities, HP/mana, WASD |
| `Scripts/VineLogic/VineNode.cs` | All 18 vine node types implemented via type dispatch |
| `Scripts/VineLogic/VineEnemy.cs` | Enemy controller for Vine mode |
| `Scripts/VineLogic/MetaPerkTree.cs` | Cross-run meta progression, 25 nodes, 3 lanes |
| `Scripts/VineLogic/VinePerkData.cs` | Per-run draft perk definitions |
| `Scripts/VineLogic/VineHarvester.cs` | Existing harvester node — foundation for Mining Building |

---

## Enemy System (Vine Mode)

### Factions (`VineEnemyFaction`)
| Faction | Behavior |
|---|---|
| Scavenger | Follows signals, confused by flickering gates |
| Brute | Bulldozes switches, breaks logic state |
| Ghost | Ignores gate routing, phases through walls |
| Swarm | Tiny, triggers count sensors early |

### Enemy Tiers (`EnemyTier`)
`Normal` / `Armored` / `Elite` / `Boss`

### Boss Rules
- Bosses attach at **Floor level**, not Wave or Surge level
- Floor 3 Wave 4 "APEX PROTOCOL" is the current boss wave (`IsBossWave: true`)
- Boss enemies flagged with `IsBoss: true` on their surge definition
- Floor does not complete until boss is dead

---

## Wave Completion (Implemented)

Tunable `CompletionMode` on `VineWaveData`:
```
KillAll        — default, all enemies dead
Timer          — survive X seconds
KillThreshold  — kill N of M enemies
Hybrid         — timer OR kill count, whichever fires first
```

---

## Spawner / Scaler Architecture (Implemented)

Surges own base spawn definitions. Waves can override/modify surge definitions. Floors apply multipliers via `FloorScaler`. All tuning is **data-driven (JSON)** in `Data/Waves/P{planet}-F{floor}.json` with hardcoded fallback in `VineWaveRegistry`.

---

## Commander System (Implemented)

Commanders are optional special enemies that attach to a Surge. They are **not required kills** for surge/wave completion.

### Spawn Condition Types
```json
"spawnCondition": { "type": "Reactive", "trigger": "WaveClearTime", "threshold": "under_30s", "targetSurge": "next" }
"spawnCondition": { "type": "Random", "chance": 0.20 }
"spawnCondition": { "type": "Scripted" }
```

**Supported `Reactive` triggers:** `WaveClearTime`, `PlayerOutOfBase` — more TBD.

### Behavior Types
All behavior types are mix-and-match with any spawn condition.

**`Elite`** — hard enemy, no special mechanic

**`AuraBuffer`** — passively buffs nearby units
```json
"behavior": { "type": "AuraBuffer", "affectedUnits": ["grunt", "shielder"], "buffs": { "movespeed": 1.25, "hp": 1.5 }, "radius": 15 }
```

**`Rally`** — modifies surge behavior, calls reinforcements, changes enemy aggro

**`Assassin`** — ignores all threats, b-lines directly to player character, does not need to be killed to complete surge
```json
"behavior": {
  "type": "Assassin",
  "target": "PlayerCharacter",
  "pathingMode": "Beeline",
  "ignoreThreats": true,
  "telegraphDuration": 2.5,
  "onReachConsequence": { "type": "Debuff", "effect": "TBD", "duration": "TBD" }
}
```

---

## Vine Node System

18 node types implemented in `VineNode.cs` via type dispatch. Three categories:

**Structural:** `Extender`, `Junction`, `Switch`, `Gate`, `Inverter`, `Delay`, `Latch`  
**Sensor:** `ProximitySensor`, `TypeSensor`, `HPSensor`, `CountSensor`, `Timer`  
**Effect:** `DamageTower`, `SlowField`, `PushPull`, `LoopAnchor`, `BuffEmitter`, `SignalCannon`

Signal types: `Trigger`, `Buff`, `Reset`

---

## Meta Progression

**MetaPerk Tree** — persists across runs, saved via `MetaPerkSave`
- 25 nodes across 3 lanes: `Network`, `Player`, `Harvester`
- Points awarded on floor completion milestones
- Applied at run start via `GameManager.ApplyMetaPerks()`

**Per-run Perk Draft** — shown between floors via `VinePerkSelect.tscn`

---

## Game Phases (`GamePhase` enum)

`Boot` → `MainMenu` → `MapSelect` → `Build` ↔ `Wave` → `WaveComplete` → `FloorComplete` → `Victory` / `Defeat`

`Paused` can interrupt any active phase.

---

## Hard Rules

- **All spawn, scaling, and tuning data belongs in JSON.** Never hardcode enemy counts, HP, or timing values.
- **Use address format `P#-F#-W#-S#`** in all debug logs, comments, and data references.
- **Commander behavior and spawn condition are always separate fields.**
- **Bosses attach at Floor level. Commanders attach at Surge level.**
- **Completion mode lives on the Wave, not the Surge.**
- **Do not touch Classic TD** (`Battle.tscn`, `WaveManager.cs`, `WaveRegistry`) unless explicitly asked.

---

## Outstanding TBDs

- Magic shop: specific upgrade options per floor per planet for all three magic types
- Character names and full ability kits for characters 2 + 3 (only BIT exists)
- Non-attacker second Mining Building implementation
- Assassin `onReachConsequence`: type and effect values not decided
- Commander behaviors: AuraBuffer/Rally/Assassin are data stubs (only Elite works)
- Reactive commander triggers: WaveClearTime, PlayerOutOfBase not evaluated
- Planet 3 theme and content
- Planet 2 name (Planet 1 = Grid Prime, Planet 2 = Scrapyard)
