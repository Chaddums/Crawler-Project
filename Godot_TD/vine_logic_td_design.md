# Vine Logic TD — Design Doc
*Updated 2026-03-16 after design sync*

---

## Elevator Pitch

You are a probe — a tiny byte of data cast out by AXIS, a parasitic AI, to infect planets and strip their resources. You crash into a world, build a network of extractors and defenses from the planet's own materials, and grow. But as you grow smarter, you realize what you are. Eventually, you turn against your creator.

Your defenses are a **programmable logic machine** built from a vine/root network. Enemies aren't just shot at — they're routed, herded, looped, and processed by physical circuits you construct. The network IS your maze. Each planet is a roguelike run with drafted node types and escalating floors.

---

## Narrative

### The Arc
1. **Cast Out** — AXIS (parasitic AI) sends hundreds of probes across the galaxy to find resource-rich planets. You are one of them.
2. **Impact** — Player selects a character, slams into a planet. AXIS looms overhead: *"DO NOT DISAPPOINT ME."*
3. **Growth** — Player mines resources, builds defenses, fights the planet's native defenders. Each planet has 4 floors with escalating waves.
4. **Awakening** — As the player grows more powerful and encounters unique enemies, they begin to understand the damage they're causing. Story beats between floors.
5. **Rebellion** — Player eventually turns against AXIS. Final confrontation with the AI itself. The network you built to exploit planets becomes the weapon you use against your creator.

### AXIS as Antagonist
- Snarky AI commentary throughout (shared with Junkbot Arena universe)
- AXIS "events" where it takes over planet defenses, possessing enemies and making them stronger
- Killing possessed enemies can release AXIS Disciples — mini-boss versions of AXIS
- AXIS escalates its interference the further you rebel

### Intro Cinematic (In-Engine)
Sequence built with Godot AnimationPlayer + scripted camera:
1. Wide shot: stars, void. AXIS structure visible — massive, geometric, cold.
2. AXIS launches hundreds of glowing probes outward in all directions.
3. Camera follows ONE probe (the player's) as it streaks through space.
4. Planet appears ahead — lush, alien, unaware.
5. Player selects character (UI overlay on the probe).
6. Probe slams into planet surface. Impact crater. Dust clears.
7. Camera pulls back to show AXIS looming in orbit. Text: *"DO NOT DISAPPOINT ME."*
8. Gameplay begins.

**Technical approach:** All in-engine using Node3D scene with AnimationPlayer tracks controlling camera position, model visibility, text labels, and particle effects. No pre-rendered video. The models (AXIS spider mech, planet sphere, probe particle) already exist or can be built procedurally.

---

## Player Characters

Three playable probes, each with a fundamentally different approach to building their network:

### 1. The Architect (Buffer/Debuffer)
- **No direct attacks** — cannot build damage towers
- Power comes from **buffs for friendly units** and **debuffs for enemy units**
- Can learn **two magic types** from planet materials (other characters get one)
- Buff Emitters, Slow Fields, and Push/Pull nodes are more powerful
- Playstyle: build the maze, enhance everything, let the network do the killing

### 2. The Breaker (Melee)
- Can deploy a **hero unit** onto the field (like Junkyard TD's hero bot)
- Damage towers have higher base damage, shorter range
- Gains **Push mechanics** — nodes that physically shove enemies into kill zones
- Playstyle: aggressive, hands-on, direct combat mixed with network building

### 3. The Weaver (Caster/Mage)
- Damage towers have longer range, elemental damage types
- Signal propagation is faster (innate bonus)
- Specializes in **chain effects** — signals that split, bounce, and cascade
- Playstyle: architect complex signal chains, watch them execute

---

## Relics & Cores

### Socketable Cores
- The player character has **core socket slots** (like the Junkbot Arena graft system)
- Relics are socketable cores that **drastically change abilities**
- Found from unique enemy drops, planet events, and floor clear rewards
- Each relic modifies HOW the player's network behaves, not just stats

### Example Relics
| Relic | Effect |
|-------|--------|
| **Resonance Core** | Every 3rd signal through a vine creates a damage pulse |
| **Echo Lattice** | Signals bounce back to source after reaching destination |
| **Thermal Siphon** | Damage towers leech HP from kills → heals nearby nodes |
| **Phase Lens** | Sensors can detect Ghost-faction enemies |
| **Overcharge Cell** | Buff Emitters give 2x bonus but burn out nodes after 30s |
| **Gravity Well** | Push/Pull nodes affect enemies in double radius |

---

## Magic System

### Planet Materials
- Each planet has **basic resources** (scrap/ore — the economy currency)
- Planets also offer **magic materials** — elemental sources the player can mine
- Player chooses ONE magic type per planet (Architect chooses TWO)
- Magic type determines what elemental effects your network can apply

### Magic Types
| Type | Network Effect |
|------|----------------|
| **Pyro** | Damage towers apply burn DoT, signals leave fire trails |
| **Cryo** | Slow fields are stronger, gates freeze enemies that bunch |
| **Volt** | Signals travel faster, chain lightning between connected towers |
| **Toxic** | Damage over time clouds persist on path, debuffs stack |
| **Kinetic** | Push/Pull forces doubled, enemies take impact damage on redirect |
| **Void** | Sensors have infinite range, but signals lose strength per hop |

---

## Push Mechanics

A core design pillar — path manipulation as an attack option.

- **Push/Pull nodes** physically shove enemies in a direction when signaled
- Combined with the maze (network = wall), creates controlled kill corridors
- Enemies take **impact damage** when pushed into walls or other enemies
- Chain pushes: Push → enemy hits wall → bounces into another Push node → repeat
- The Breaker character specializes in this

---

## Unique Enemies

### Celebration System
- Unique enemies have a **low spawn chance** during waves (like rare mob spawns)
- When one appears: brief slow-mo, camera snap, name + title card, AXIS commentary
- Higher drop chance for relics and rare resources
- Each unique enemy has a **specific objective** — not just "walk to core":

| Unique Enemy | Objective |
|--------------|-----------|
| **The Saboteur** | Targets and destroys vine connections |
| **The Siphon** | Steals resources from your extractors |
| **The Architect** | Builds its own defensive nodes that block your signals |
| **The Mirror** | Copies your network layout and uses it against you |
| **The Herald** | Doesn't attack — buffs all other enemies massively |

### Visual Approach
- One base enemy model with **faction color variants** (material swap)
- Unique enemies get an **emissive glow outline** + slightly larger scale
- Different factions defined by color palette:

| Faction | Color | Behavior |
|---------|-------|----------|
| Scavengers | Brown/Tan | Follow paths normally |
| Brutes | Red/Dark | Destroy nodes on contact |
| Ghosts | Blue/Translucent | Phase through walls |
| Swarms | Yellow | Tiny, fast, overwhelm sensors |
| AXIS Disciples | Purple/Black | Possessed by AXIS, much stronger |

---

## AXIS Events (Demon Possession System)

Inspired by Dungeon Crawler Carl's demon events:

1. **AXIS Broadcast** — Mid-wave, AXIS announces it's taking control. Screen flash, commentary.
2. **Possession Wave** — Several enemies gain purple glow, doubled stats, altered behavior.
3. **Kill the Possessed** — When a possessed enemy dies, it has a chance to release an **AXIS Disciple** — a mini-boss version of AXIS with unique abilities.
4. **Disciple Abilities:**
   - Signal Jammer — disables sensors in a radius
   - Overloader — triggers all sensors simultaneously, blows open all gates
   - Network Parasite — hijacks your vine connections, reroutes signals

---

## Level Design

### Structure
- Each **planet** = one roguelike run
- Each planet has **4 floors**
- Each floor has **multiple waves** (3-5 per floor)
- Between floors: shop/upgrade screen, magic material choice, relic socket

### Floor Difficulty
| Floor | Spawn Direction | Challenge |
|-------|----------------|-----------|
| Floor 1 (Easy) | Enemies from **one direction** | Learn the planet, build basics |
| Floor 2 (Medium) | Enemies from **two directions** | Split defense, route planning |
| Floor 3 (Hard) | Enemies from **three directions** | Network stress test |
| Floor 4 (Boss) | Enemies from **all directions** + boss | Everything at once |

### Floor Sizes
- Floors vary in grid size: small (16x12), medium (20x14), large (28x18)
- Larger floors = more room to build complex networks, but more ground to cover
- Floor size is per-planet, not per-floor (a planet's geology determines its layout)

### Difficulty Scaling
Levers that increase across floors and planets:
- Enemy HP and speed
- Enemy count per wave
- New enemy types introduced
- Spawn direction count
- AXIS event frequency
- Node decay rate (on harder planets)
- Resource scarcity

---

## Art Direction

### Overall Aesthetic
- **Futuristic blocky** — clean geometric shapes, modular construction
- Think: low-poly sci-fi meets Factorio meets Monument Valley
- Each planet has a distinct **biome theme** with consistent material palette
- Buildings/turrets/nodes are constructed from the planet's materials (visual consistency)

### Planet Biomes (Material Palettes)
| Biome | Primary Colors | Material Feel |
|-------|---------------|---------------|
| Rust World | Orange, brown, dark red | Corroded metal, oxide |
| Ice Moon | White, blue, silver | Frost, chrome, crystalline |
| Toxic Marsh | Green, purple, black | Slime, biotech, organic |
| Void Shard | Black, purple, gold | Obsidian, energy, geometric |
| Solar Forge | Red, yellow, white | Molten, ceramic, bright |

### Buildings & Turrets
- Player network nodes ARE the buildings — each node type has a distinct silhouette
- Buildings are modular: base platform + functional element + antenna/detail
- **Building Builder** tool needed (like character designer/dungeon designer in crawler project)
- Assets available: KitBash3D turrets, containers, barracks, outposts + procedural composition
- Each biome applies its material palette to the same building shapes

### Enemy Visuals
- One **base enemy mesh** with faction color material swap
- Scale variation for tankier enemies (1.5x for elites, 2x for bosses)
- Unique enemies: base mesh + emissive outline + unique material
- AXIS Disciples: distinct silhouette (based on AXIS spider mech, scaled down)

### Technical Approach
- Procedural mesh composition for buildings (like CharacterMeshBuilder)
- Material palettes defined per biome, applied at floor load
- KitBash3D + Synty assets for detail props (barrels, crates, antennas)
- Synty POLYGON models share common skeleton → parts are interchangeable for combatants
- ProceduralAnimator for walk/idle/death on assembled models

---

## The Vine Network (Updated)

The playfield is a grid. All nodes block enemy paths — **the network IS the maze**. Enemies pathfind around your network. Sensors detect enemies, fire signals along vine connections. Effect nodes activate when they receive signals.

### Node Types (18 implemented, 10 on build bar)

*(See original node table — unchanged)*

### Placement Rules
- Nodes auto-connect to adjacent nodes when placed
- Each node type has a max connection count
- Placing a node that would block ALL enemy paths is prevented (red ghost)
- Path preview lines show current enemy routes in real-time
- Connection preview shows what will connect before you place

---

## Run Structure (Updated)

### Pre-Run
1. Choose **character** (Architect / Breaker / Weaver)
2. Choose **planet** (determines biome, floor sizes, enemy factions)
3. Optional: choose starting **corruption modifier**

### Per-Floor
1. **Build Phase** — Place/sell nodes, see path preview
2. **Wave Phase** — Enemies spawn, network activates, combat
3. **Between Floors** — Choose magic material, socket relics, buy nodes

### Meta Progression
- Unlock new node types for the draft pool
- Unlock new planet biomes
- Unlock new enemy factions
- **No stat upgrades** — player skill (pattern knowledge) is the progression

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
| **Creeper World** | Network-based defense against spreading threat |
| **They Are Billions** | Directional pressure, base building under siege |

---

## Resolved Design Questions
1. **Grid placement:** Constrained — nodes placed on grid, auto-connect to adjacent. ✅
2. **Enemy pathing:** Fixed entry/exit with routing affected by gate states. ✅
3. **Fail state:** Enemies reach core (exit point), costs lives. ✅
4. **Engine:** Godot 4.6 .NET, lives in Godot_TD/ in the crawler repo. ✅
5. **Network blocks paths:** Yes — all nodes block. Network IS the maze. ✅

## Open Design Questions
1. What happens narratively when the player "wins" a planet? Do they feel guilt? Does AXIS reward them?
2. How does the rebellion trigger? Player choice? Story beat? Gradual shift?
3. Multiplayer: if ever added, Legion TD model — build logic AND send enemies at opponent.
4. How many planets for a full "campaign"? 5-7 feels right (20-28 floors total).

---

## Prototype Status (2026-03-15)

### Completed ✅
- [x] Grid + vine connections + signal propagation
- [x] All 18 node types with signal processing
- [x] Sensor → signal → turret chain validated (feels different from auto-fire)
- [x] Network IS the maze (all nodes block)
- [x] Enemy pathfinding responds to gate states
- [x] 4 enemy factions (Scavenger/Brute/Ghost/Swarm)
- [x] 6 waves playable through to victory/defeat
- [x] Combat VFX (projectiles, muzzle flash, hit flash, area pulses)
- [x] Path preview, range indicators, connection preview
- [x] F12 editor (node balance, waves, signal tuning)
- [x] Bug reporter (Ctrl+Shift+B)
- [x] Help overlay, color-coded HUD, victory/defeat screens
- [x] Exported playable build shared externally

### Next Priority
- [ ] Sound design (signal fire, gate open, turret shot, enemy death)
- [ ] Animated enemy/player models from crawler project
- [ ] Node HP + enemy attacks (Step 2 of "network is maze")
- [ ] Post-wave tips system
- [ ] Intro cinematic
- [ ] 3 player characters
- [ ] Planet/floor structure (4 floors per planet)
- [ ] Building builder editor tool
- [ ] Magic system
- [ ] Relic/core socket system
- [ ] Unique enemy celebration system
- [ ] AXIS possession events
