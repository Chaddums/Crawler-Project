# Vine Logic TD — Concept Design Doc
*Scratchpad for Claude Code handoff*

---

## Elevator Pitch

A tower defense where your defenses are a **programmable logic machine** built from a vine/root network. Enemies aren't just shot at — they're routed, herded, looped, and processed by physical circuits you construct from node types. Each run is a Slay the Spire-style draft of node types and map topologies, so you learn build patterns that transfer across runs even when the pieces change.

---

## The Core Paradigm Shift

| Traditional TD | Vine Logic TD |
|---|---|
| Static maze enemies navigate | Programmable machine that processes enemies |
| Tower placement = spatial puzzle | Node wiring = systems engineering puzzle |
| Best tower in best spot | Right function built from available parts |
| You watch enemies get shot | You watch your trap execute |

---

## The Vine Network

The playfield is a grid of **vine slots**. Vines connect nodes to each other. Nodes have a limited number of connection slots (some have 2, some 8, some only extend in a line). Signals travel through vines between nodes.

### Placement Rules (from original memory)
- Each node type has a fixed number of open connection slots around it
- Some nodes extend vines in a line (wall-style, no damage on the extender)
- Buffer/extender nodes allow signals to travel farther — "hop" nodes that skip distance
- Buff signals propagate through the vine, so towers X links away receive buffs (not distance-based — link-count-based)

---

## Node Types

### Structural / Routing
| Node | Function | Analog |
|---|---|---|
| **Vine Extender** | Adds connection length, no damage | Wire |
| **Junction** | Splits signal to multiple outputs | Splitter |
| **Switch** | Toggles enemy route L/R on timer or signal | Railroad switch |
| **Gate** | Only opens when receiving signal from 2+ inputs simultaneously | AND gate |
| **Inverter** | Flips signal state | NOT gate |
| **Delay** | Holds signal N seconds before passing | Buffer / capacitor |
| **Latch** | Stays open after triggered until reset | Flip-flop |

### Sensor / Input
| Node | Trigger Condition |
|---|---|
| **Proximity Sensor** | Enemy within range |
| **Type Sensor** | Specific enemy type detected |
| **HP Sensor** | Enemy below HP threshold |
| **Count Sensor** | N or more enemies in zone |
| **Timer** | Fires on interval, no input required |

### Effect / Output
| Node | Effect |
|---|---|
| **Damage Tower** | Shoots enemies in range |
| **Slow Field** | Debuffs enemies passing through |
| **Push/Pull** | Redirects enemy movement direction |
| **Loop Anchor** | Creates circular route (enemies repeat section) |
| **Buff Emitter** | Sends damage/speed buff through vine to connected towers |
| **Signal Cannon** | Fires signal down vine on demand |

---

## Example Functions You Can Build

These are learned patterns — the "tech" players discover across runs.

### Armor Trap
`HP Sensor → Delay → Switch` 
Detects armored enemy, waits for it to reach redirect point, routes it into armor-piercing kill zone.

### Pulse Loop
`Timer → Gate → Loop Anchor → Damage Tower`
Timed gate releases enemies in controlled batches into a loop that runs them past damage towers multiple times.

### Enemy Counter
`Count Sensor (threshold: 5) → Gate`
Gate only opens when 5+ enemies are bunched — forces grouping for AoE tower.

### Far Buff Chain
`Buff Emitter → Extender → Extender → Extender → Damage Tower`
Buff travels by link count, not distance. Chain of cheap extenders routes a buff from a support cluster to a remote kill tower.

### Bouncer
`Sensor → Switch A → Switch B (inverted)`
Enemies alternate between two routes — spreads damage across two kill zones, prevents single lane stacking.

---

## Enemy Behavior

Enemies are **pathfinding agents** that respond to the network state:

- Follow lowest-resistance open path
- Bunch at closed gates → natural AoE opportunity
- Fast enemies may outrun signal propagation → timing pressure
- Different factions have different responses:
  - **Scavengers** — follow signals, get confused by flickering gates
  - **Brutes** — bulldoze switches, break logic state
  - **Ghosts** — ignore gate routing, phase through walls
  - **Swarms** — tiny, trigger count sensors early, overwhelm AoE windows

### Boss Behaviors
- **Signal Jammer** — disables sensor nodes in a radius
- **Overloader** — triggers all sensors simultaneously, blows open all gates
- **Pathfinder** — recalculates optimal route every 2 seconds, adapts to your layout

---

## The Run Structure (Slay the Spire Model)

### Pre-Run Draft
- Choose starting **vine seed** (map topology — grid shape, enemy entry/exit points)
- Draft 3 node type pools — pick 1 per pool (determines what node types are available this run)
- Pick a starting **corruption** (modifier that adds a twist)

### Mid-Run Progression
Each wave cleared gives:
- New node type (added to available build set)
- Gold to place more nodes
- Occasional **event** — choose between two modifiers or a node swap

### Corruptions / Modifiers (examples)
- *Overclock* — signals travel 2x faster, enemies also move 2x faster
- *Dampened* — buff signals lose 50% strength per hop
- *Feral Wave* — this wave ignores gate routing
- *Resonance* — every 3rd signal through a vine creates a damage pulse
- *Decay* — nodes degrade after 10 signals, must be repaired

### Meta Progression
- No stat upgrades
- **Pattern library** — you learn build functions that work across runs
- Unlock new node types available in the draft pool
- Unlock new vine seed shapes (map topologies)
- Unlock new enemy factions

---

## Visual Language (Critical)

The network must be **readable during live waves**.

- Signals shown as traveling sparks/pulses along vine in real time
- Switch states: physically animated (rail switches, flaps, gates)
- Open gate = lit/green. Closed = dark/red. Transitioning = flickering
- Enemy routing paths projected faintly ahead of their position
- Buff propagation shown as a color wash traveling through the vine
- When a trap executes perfectly → satisfying visual payoff (chain reaction glow, enemies reverse direction visibly)

---

## Aesthetic (Junkyard Universe)

- Logic gates are salvaged railroad switches, busted motion detectors, pneumatic pistons, duct-taped relays
- Signals travel as electrical sparks or pneumatic hisses through corroded pipes
- Vine = literal rusted cable or overgrown wire — organic/mechanical hybrid
- "This shouldn't work this well, but it does" — everything looks improvised
- Sound design: clunk of switch throwing, hiss of signal passing, spark crackle of buff propagating

---

## Closest Existing References (for Claude Code context)

| Reference | What to borrow |
|---|---|
| **Factorio circuit network** | Logic gate feel, signal propagation, readable state |
| **Opus Magnum** | Physical machine you build to process a thing |
| **Slay the Spire** | Run structure, draft, corruption modifiers |
| **Defense Grid** | Maze-as-first-class-mechanic |
| **Original WC3 vine TD** (unknown map) | Node slots, link-count-based buffs, vine placement rules |
| **Legion TD** | PvEvP loop if multiplayer ever added |

---

## Open Design Questions

1. Is the grid free-placement or does the vine structure constrain where you can build? (Probably constrained — vines must connect to existing nodes)
2. How is the enemy path defined? Fixed entry/exit with routing in between? Or fully emergent from gate states?
3. What is the "fail state" — enemies reach exit, OR something more interesting (signal overload, node destruction)?
4. Multiplayer later? If so, Legion TD model — you build logic AND send enemies at opponent
5. What engine? (Godot — check if this lives in the dungeon crawler fork or is a standalone project)

---

## Concerns / Risks to Resolve Early

### 1. Readability at Speed is Make-or-Break
The visual language section acknowledges this but underestimates the difficulty. Factorio works because you can pause and zoom. A live TD wave with 30+ enemies flowing through flickering gates, traveling signals, buff propagation washes, AND projected enemy paths could become unreadable fast. **Prototype with just 2-3 node types and validate readability before adding more.** Do not build the full visual system until the minimal version is legible under stress.

### 2. Signal Propagation Timing vs Enemy Speed
This is a balancing nightmare. If signals are too fast, the logic feels irrelevant (everything reacts instantly). Too slow, and fast enemies outrun your machine and the player feels cheated. The Delay node and signal speed should probably be **the first thing tuned** after basic propagation works. This needs dedicated prototyping time — not something to patch later.

### 3. Open Question #2 is the Most Important One
Fixed entry/exit with routing in between is probably the right answer. Fully emergent pathing from gate states could lead to enemies getting permanently stuck or infinite loops. The Loop Anchor node already implies you want *controlled* loops, not accidental ones. Decide this before building enemy pathfinding — it changes the architecture significantly.

### 4. Complexity Ceiling Risk
The doc lists 18 node types. For a StS-style draft you probably want 10-12 max in any single run — the draft handles this. But the tutorial/onboarding for "here's what a Gate does, here's what a Latch does, here's the difference" could be steep. **Consider whether Latch, Inverter, and Delay are all needed at launch** or if some are unlockable post-core-validation. Launch with fewer, prove they're fun, then expand.

### 5. Lean Hard Into the Sound Design
The junkyard aesthetic is a great fit — salvaged railroad switches and duct-taped relays give the logic network a physical, tangible feel that pure "circuit board" games lack. Sound design (clunk of switch throwing, hiss of signal, spark crackle of buff) does heavy lifting here. This is not polish — it's core to making the system feel real and readable. Treat it as a first-class concern alongside visuals.

---

## Prototype Order (Revised)

The original next steps are solid but reordered to validate the core feel before building breadth:

- [ ] **Step 1** — Grid + vine connection rules + basic signal propagation (A fires → signal travels to B)
- [ ] **Step 2** — Sensor + Damage Tower only. Validate that "signal triggers tower" *feels different* from "tower auto-fires." If it doesn't feel meaningfully different, the whole premise needs rethinking.
- [ ] **Step 3** — Add Switch node. Validate the "sheep herder" feel with just Sensor → Switch → two kill zones.
- [ ] **Step 4** — Add Gate (AND). Validate that logic combinations feel emergent, not confusing.
- [ ] **Step 5** — Enemies that respond to gate state (pathfinding reacts to open/closed).
- [ ] **Step 6** — Only then add Slow Field, Loop Anchor, Buff Emitter.

**The key risk is building all 18 node types before proving that signal propagation + routing feels fun with just 3-4 nodes. Validate the core feel first. Do not skip this.**
