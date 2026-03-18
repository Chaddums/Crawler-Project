# Vine Logic TD — Design Doc
*Updated 2026-03-17 — reflects current build + design decisions*

---

## Elevator Pitch

You are a probe — a tiny byte of data cast out by AXIS, a parasitic AI, to infect planets and strip their resources. You crash into a world, build a network of extractors and defenses from the planet's own materials, and grow. But as you grow smarter, you realize what you are. Eventually, you turn against your creator.

Your defenses are a **programmable logic machine** built from a vine/root network. Enemies aren't just shot at — they're routed, herded, looped, and processed by physical circuits you construct. The network IS your maze. Each planet is a roguelike run with drafted node types and escalating floors.

---

## Narrative

### The Arc
1. **Cast Out** — AXIS (parasitic AI) sends hundreds of probes across the galaxy to find resource-rich planets. You are one of them.
2. **Impact** — Player selects a role, slams into a planet. AXIS looms overhead: *"DO NOT DISAPPOINT ME."*
3. **Growth** — Player mines resources, builds defenses, fights the planet's native defenders. Each planet has 3 floors with escalating waves + boss.
4. **Awakening** — As the player grows more powerful and encounters unique enemies, they begin to understand the damage they're causing. Story beats between floors.
5. **Rebellion** — Player eventually turns against AXIS. Final confrontation with the AI itself.

### AXIS as Antagonist
- Snarky DCC-style AI commentary throughout
- AXIS "events" where it takes over planet defenses, possessing enemies and making them stronger
- Killing possessed enemies can release AXIS Disciples — mini-boss versions of AXIS
- AXIS escalates its interference the further you rebel

### Intro Cinematic (Implemented)
30-second in-engine sequence, skippable:
1. Stars fade in. AXIS procedural obelisk with purple eye, amber edge-glow surge, floating debris.
2. Eye pulses. 60 golden probes launch outward.
3. Camera follows player's probe through space with particle trail.
4. Planet appears. Cut to terraformed surface with KitBash buildings, turrets, props.
5. Probe impacts — crater, debris, dust VFX.
6. Camera tilts up. AXIS silhouette looms in sky. "DO NOT DISAPPOINT ME."
7. Fade to gameplay.

---

## Player Roles (Implemented as Draft)

Three roles selected at draft screen before each run. Each determines which 8 node types are available:

### Scrapwright
- Balanced builder — good mix of sensors, turrets, and routing
- Default pick for learning the game

### Arcanist
- Signal-focused — more routing and logic nodes
- Relies on complex signal chains over raw damage

### Bruteforge
- Damage-focused — stronger turrets, fewer routing options
- Simple but powerful chains

---

## Planets & Enemy Behavior

### Planet 1: Grid Prime (Tron)
- **Theme:** Dark blue-black surfaces, cyan emissive grid lines, digital aesthetic
- **Enemy AI: Circuit-based** — enemies follow predictable paths along data streams and grid lines. They're programs — dumb, pattern-based, exploitable.
- **Spawn behavior:** Enemies emerge from fixed entry points in orderly lines. Predictable timing. The player learns the system.
- **Battlefield:** Smaller, more contained. Elevated platforms and walls create clear lanes.

### Planet 2: Scrapyard (Rust/Metal)
- **Theme:** Warm browns, corroded oranges, industrial grime. The Junkbot Arena aesthetic.
- **Enemy AI: Mercenary-based** — enemies are scavenger bands, not programs. They arrive in groups from off-screen, not from fixed spawners.
- **Spawn behavior:** Groups of enemies enter the camera view from multiple directions simultaneously. Less predictable than Planet 1. The player must react to squads, not lines.
- **Battlefield:** Larger map. More open terrain. Enemies approach from the edges, requiring broader defense coverage.

### Planet 3: (TBD Theme)
- **Theme:** TBD — possibly Void Shard (black/purple/gold) or Ice Moon (white/blue/silver)
- **Enemy AI: Military intelligence** — enemies enter strategically. Scouts probe defenses, then the main force attacks weak points. Flanking, feints, adaptive routing.
- **Spawn behavior:** Enemies assess the player's network before committing. They avoid kill zones, target undefended paths, and coordinate assaults. The player must build redundancy and anticipate flanks.
- **Battlefield:** Largest map. Multiple entry vectors. The player's network must cover wide areas or risk being outmaneuvered.

### Enemy Faction Behaviors (All Planets)
| Faction | Behavior | Color |
|---------|----------|-------|
| Scavenger | Follow paths normally, confused by flickering gates | Bright red |
| Brute | Bulldoze switches, break logic state, attack nodes | Dark crimson |
| Ghost | Ignore gate routing, phase through walls | Magenta-red |
| Swarm | Tiny, fast, trigger count sensors early, overwhelm AoE | Orange-red |

### Boss Behaviors
- **Signal Jammer** — disables sensor nodes in a radius
- **Overloader** — triggers all sensors simultaneously, blows open all gates
- **Pathfinder** — recalculates optimal route every 2 seconds, adapts to layout

---

## Signal Power System (Implemented)

Sensors have a **power budget** — the number of effect nodes one signal can activate before dying.

| Sensor | Power | Notes |
|--------|-------|-------|
| Motion Detector | 3 | Standard detection, powers 3 turrets |
| IFF Scanner | 3 | Type-specific detection |
| Damage Gauge | 3 | Triggers on wounded enemies |
| Crowd Counter | 4 | Triggers on groups, slightly stronger |
| Crank Timer | 4 | Fires on interval, no detection needed |

**How it works:**
- Sensor fires signal with strength = power count
- Each effect node (turret, slow field, push/pull) costs 1 power
- Routing nodes (extender, junction, switch, gate) pass through FREE
- Signal dies when power reaches 0

**Build implications:**
- 1 sensor → 3 turrets max (Motion Detector)
- Want more turrets? Add another sensor or use Junction to split into shorter chains
- Buff Emitters propagate buffs separately from trigger signals

---

## The Vine Network

The playfield is a grid. All nodes block enemy paths — **the network IS the maze**. Enemies pathfind around your network.

### Signal Chain
`Sensor detects enemy → fires signal → signal travels along vine connections → reaches effect node → effect activates AND passes signal onward (costs 1 power) → next effect → ... → power runs out`

### Connection Colors (Implemented)
| Color | Meaning |
|-------|---------|
| Green | Sensor connection (signal source) |
| Cyan | Effect-to-effect (powered chain, signal flows through) |
| Orange | Route-to-effect (signal reaching destination) |
| Blue | Route-to-route (passthrough) |

### Terrain Types (Implemented)
| Type | Walkable | Buildable | Effect |
|------|----------|-----------|--------|
| Empty | Yes | Yes | Standard ground |
| Wall | No | No | Impassable obstacle |
| Elevated | No | No | Raised platform landmark |
| Channel | Yes | No | Enemies slightly prefer these paths |
| DataStream | Yes | No | Enemies move 50% faster, prefer these |
| Entry | Yes | No | Enemy spawn point |
| Exit | Yes | No | Core — defend this |

### Node Types (18 implemented, 8 per role via draft)

**Structural / Routing (pass signals free):**
Extender, Junction, Switch, Gate (AND), Inverter, Delay, Latch

**Sensor / Input (generate signals with power budget):**
Proximity Sensor, Type Sensor, HP Sensor, Count Sensor, Timer

**Effect / Output (consume 1 power, activate + propagate):**
Damage Tower, Slow Field, Push/Pull, Loop Anchor, Buff Emitter, Signal Cannon

---

## Run Structure (Implemented)

### Pre-Run
1. Intro cinematic (skippable)
2. **Draft screen** — choose role (Scrapwright/Arcanist/Bruteforge), determines 8 available nodes

### Per-Planet (3 Floors)
Each floor has its own map layout and wave set:
1. **Floor 1: Gateway** — 1 entry, 1 exit. Tutorial-level. 3 waves.
2. **Floor 2: Conduit** — 2 entries, 1 exit. Split paths. 3-4 waves.
3. **Floor 3: Arena** — 3 entries, 1 exit. Boss floor. DataStreams + fortifications. 3 waves + boss.

### Per-Floor Loop
1. **Build Phase** — Place/sell nodes, see path preview, check range indicators
2. **Wave Phase** — Enemies spawn, signals fire, turrets activate
3. **Wave Complete** — Bonus gold, brief build window
4. **Floor Complete** — Perk selection screen, then next floor loads

### Between Floors
- **Perk selection** — choose 1 of 3 perks that modify your network for the rest of the run

---

## Planet Theme System (Implemented)

### Architecture
- `PlanetTheme` — abstract base class defining palette + material factories
- `TronPlanetTheme` — Planet 1 (Grid Prime). Dark body + Fresnel rim or inverted hull outline.
- `ScrapyardPlanetTheme` — Planet 2 (Scrapyard). Warm rusty metals, amber glow.
- `PlanetTheme.Current` — static reference, swappable per planet

### Outline Modes (Implemented in Asset Sandbox)
| Mode | Effect |
|------|--------|
| Per-Mesh Outline | Each mesh gets inverted hull outline. Good for simple models. |
| Silhouette Only | Accent-colored body + black outline clone. Clean outer edge, no internal noise. |
| No Outline | Accent body only. |

### Asset Sandbox (F12 Editor)
- Browse all 42 imported assets by category
- 3D SubViewport preview with orbit camera
- Apply planet theme per faction (Player/Scavenger/Brute/Swarm/Ghost)
- Switch between planet themes (Grid Prime / Scrapyard)
- Save themed versions as .tscn files
- Verify All Assets (Raw and Themed modes)
- Scale normalization for all assets

---

## Art Direction

### Overall Aesthetic
- **Futuristic blocky** — clean geometric shapes, modular construction
- Each planet has a distinct theme with consistent material palette
- Player nodes = blue/cyan family. Enemy units = red family.

### Planet Biomes
| Planet | Theme | Enemy AI | Spawn Style |
|--------|-------|----------|-------------|
| Grid Prime | Tron (cyan/dark) | Circuit — predictable, pattern-based | Fixed entry points, orderly lines |
| Scrapyard | Rust/metal (amber/brown) | Mercenary — group-based, off-screen | Squads from edges, less predictable |
| Planet 3 TBD | TBD | Military — intelligent, strategic | Scouts, flanks, adaptive routing |

### Technical Approach
- KitBash3D + Synty assets themed via PlanetTheme system
- Inverted hull outline shader for Tron look
- Fresnel rim shader for simple models
- 44 assets imported with normalized scales
- All assets pass themed verification (42/42 OK)

---

## Closest References

| Reference | What to borrow |
|---|---|
| **Factorio circuit network** | Logic gate feel, signal propagation |
| **Opus Magnum** | Physical machine satisfaction |
| **Slay the Spire** | Run structure, draft, modifiers |
| **Defense Grid** | Maze-as-first-class-mechanic |
| **Project Hail Mary (astrophage)** | Probe-as-character, growing awareness |
| **Dungeon Crawler Carl** | Demon possession events, snarky AI antagonist |
| **Tron Legacy** | Planet 1 visual aesthetic |

---

## Resolved Design Questions
1. **Grid placement:** Constrained — nodes placed on grid, auto-connect to adjacent. ✅
2. **Enemy pathing:** Fixed entry/exit with routing affected by gate states. ✅
3. **Fail state:** Enemies reach core (exit point), costs lives. ✅
4. **Engine:** Godot 4.6 .NET, lives in Godot_TD/ in the crawler repo. ✅
5. **Network blocks paths:** Yes — all nodes block. Network IS the maze. ✅
6. **Signal power:** Sensors have power budget (3-4). Each effect node costs 1. ✅
7. **Effect propagation:** Effect nodes activate AND pass signals onward. ✅
8. **Planet themes:** PlanetTheme base class, per-planet implementations. ✅
9. **Enemy behavior per planet:** Circuit → Mercenary → Military escalation. ✅

## Open Design Questions
1. What happens narratively when the player "wins" a planet? Does AXIS reward them?
2. How does the rebellion trigger? Player choice? Story beat?
3. Planet 3 theme and specific enemy AI behaviors
4. Battlefield size scaling — Planet 2/3 need larger maps for off-screen spawning
5. How do mercenary squads spawn mechanically? Random edge positions? Wave-based clusters?

---

## Build Status (2026-03-17)

### Completed ✅
- [x] Intro cinematic (30s, skippable, procedural AXIS + planet surface)
- [x] Draft screen (3 roles, 8 nodes each)
- [x] 3 floor progression with boss on floor 3
- [x] Perk selection between floors
- [x] Signal power budget system
- [x] Effect chain propagation (turrets pass signals)
- [x] Connection color coding (green/cyan/orange/blue)
- [x] 18 node types with signal processing
- [x] 4 enemy factions + boss enemies
- [x] Network IS the maze (all nodes block)
- [x] 3 map layouts with terrain features (elevated, channel, datastream)
- [x] Combat VFX (projectiles, muzzle flash, hit flash, area pulses)
- [x] Path preview, range indicators, connection preview
- [x] Tron theme with outline shaders (per-mesh, silhouette, no outline)
- [x] Scrapyard theme defined
- [x] PlanetTheme system with per-planet material factories
- [x] Asset Sandbox editor (F12) with 3D preview + theme application
- [x] 42 assets imported, normalized, verified
- [x] F12 editor (node balance, waves, signal tuning, asset sandbox)
- [x] Bug reporter (Ctrl+Shift+B with screenshot snip)
- [x] Help overlay (H key)
- [x] Speed control (Tab: 1x/2x/3x)
- [x] Exported playable build shared externally
- [x] Full run confirmed: draft → 3 floors → boss → victory

### Next Priority
- [ ] Larger battlefield for Planet 2+ (off-screen spawning needs space)
- [ ] Planet 2 mercenary spawn system (group-based, from edges)
- [ ] Sound design (signal fire, gate open, turret shot, enemy death)
- [ ] Corruption/modifier events (AXIS possession, signal jam)
- [ ] Visual polish pass (death pops, screen shake)
