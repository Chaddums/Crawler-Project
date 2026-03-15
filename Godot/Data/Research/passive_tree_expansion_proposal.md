# Passive Tree Expansion Proposal — Junkbot Arena

## The Problem

**Current state:** 96 nodes, ~29 points per run = **30% fill rate** (good!)
**But:** Most nodes are linear stat chains with few real decisions. A player walks down their branch, maybe crosses one bridge, done. The *feeling* is "follow the path" not "build my bot."

**Goals:**
1. Prevent easy tree-jumping while *increasing* build variety
2. More "do I go left or right?" moments
3. Every point should feel like a real choice
4. Support the junkbot fantasy — you're a robot frankensteining yourself together

---

## Proposal: Expanded Modular Tree (3 Layers)

### Layer 1: Expanded Passive Tree (~200 nodes)
### Layer 2: Ability Specialization Trees (mini-trees per ability)
### Layer 3: Sponsor Boons (in-run offered upgrades)

---

## LAYER 1: THE EXPANDED PASSIVE TREE

### Current → Proposed Comparison

| Aspect | Current | Proposed |
|--------|---------|----------|
| Total nodes | 96 | ~200-220 |
| Points per run | ~29 | ~29-35 |
| Fill rate | 30% | 15-18% |
| Branch structure | Linear chain (8 nodes) | Branching web (3-way splits) |
| Cross-class gates | Bridge connectors (3-4 pts) | Affinity gates + power budget |
| Meaningful choices | ~6 per run | ~15-20 per run |
| Node types | 6 | 8 (add Threshold + Firmware) |

### New Node Types

**Threshold Nodes** (inspired by Borderlands tier gates)
- Require X total points spent in a specific branch before unlocking
- Gate access to deeper, more powerful nodes
- Example: "Requires 8 points in Scrapheap branch" to access the heavy armor cluster
- Prevents cherry-picking deep nodes cheaply

**Firmware Nodes** (inspired by Destiny 2 Aspects)
- Powerful build-defining nodes with a **slot cost**
- Each Firmware node has a "power drain" value (1-3)
- Players have a total Firmware Budget of 4-5
- Taking a 3-drain Firmware means you can only fit 1-2 more
- Creates tradeoff: one BIG firmware vs several small ones
- Examples:
  - "Siege Mode" (3 drain): Can't move while attacking, +100% damage, +50% armor
  - "Overclock Protocol" (2 drain): +30% all stats, take 5 DPS
  - "Salvage Scanner" (1 drain): Highlight rare loot, +25% item find

### Branch Restructure: From Lines to Webs

**Current:** ClassStart → 8 nodes in a line → inner ring
**Proposed:** ClassStart → splits into 2-3 sub-branches after 2 nodes

Each class gets 3 themed sub-branches (inspired by Borderlands 3):

#### Scrapheap (Tank/Brawler)
- **Juggernaut Path:** HP, armor, damage reduction, taunt
- **Berserker Path:** Damage scales with missing HP, lifesteal, frenzy
- **Fortress Path:** Shields, thorns, area denial, crowd control

#### TinCan (Defender/Support)
- **Bulwark Path:** Block, parry, reactive armor stacking
- **Guardian Path:** Ally buffs, heal auras, damage sharing
- **Sentinel Path:** Counter-attacks, stagger, interrupt

#### SparkPlug (Energy/Caster)
- **Overcharge Path:** Mana → damage conversion, ability power scaling
- **Conduit Path:** Chain lightning, AoE, status spread
- **Capacitor Path:** Mana shields, energy regen, cooldown reduction

#### RustBucket (Rogue/Stealth)
- **Infiltrator Path:** Crit, stealth, backstab multipliers
- **Saboteur Path:** Traps, debuffs, damage over time
- **Scavenger Path:** Loot bonuses, consumable efficiency, dodge

#### NoiseBox (Bard/Controller)
- **Broadcast Path:** AoE auras, ally buffs, enemy debuffs
- **Dissonance Path:** Confusion, fear, damage reflection
- **Resonance Path:** Status effect amplification, combo chains

#### Clunker (Brawler/Berserker)
- **Piston Path:** Melee damage, attack speed, combo hits
- **Wrecking Ball Path:** AoE melee, knockback, destruction
- **Scrap Engine Path:** On-kill effects, rampage, momentum

Each sub-branch has ~8-10 nodes with 2-3 notables each. Players can't max all 3 sub-branches — must specialize in 1-2.

### Cross-Class Gating: Affinity System (inspired by Grim Dawn)

Replace simple bridge connectors with an **Affinity requirement** system:

**5 Affinity Types** (themed to junkbot sources):
- **Military** (red): Scrapheap + Clunker nodes grant Military affinity
- **Tech** (blue): SparkPlug + TinCan nodes grant Tech affinity
- **Stealth** (green): RustBucket + NoiseBox nodes grant Stealth affinity
- **Scrap** (orange): Universal — inner ring and bridge nodes
- **Alien** (purple): Rare nodes, graft-related

**How it works:**
- Spending points in a class branch accumulates affinity in that branch's colors
- Cross-class notable/keystone nodes require minimum affinity levels
- Example: A powerful hybrid node between SparkPlug and RustBucket might require "Tech 5, Stealth 3"
- You earn affinity naturally by investing in your class — dipping elsewhere costs more points for less affinity
- Makes tree-jumping expensive but not impossible

**Why this is better than simple bridges:**
- Gradual gating instead of binary (bridge exists or doesn't)
- Rewards deep investment in your own class before branching
- Creates meaningful "how much do I invest here vs there?" math
- Affinity thresholds can gate powerful cross-class combos

### Keystone Rework: Tradeoffs Required

Current keystones are strong but don't all have meaningful downsides. Every keystone should have a real cost:

| Keystone | Upside | Downside |
|----------|--------|----------|
| Berserker Protocol | +50% damage below 50% HP | -30% max HP |
| Iron Fortress | +50% healing, +30 armor | No dashing, -20% move speed |
| Mana Shield | 30% damage absorbed by mana | -30% max mana regen |
| Glass Cannon | +40% all damage | +30% damage taken |
| Entropy Field | 3% HP/s per debuffed enemy | Take 2% HP/s per debuff on YOU |
| Overclocked | +25% all stats | Take 5 DPS, can't heal above 80% |

Plus add **6 new keystones** (one per class, deeper in tree, requiring Threshold gate):
- These are mutually exclusive with the original keystone (can only have 1 per class)
- Creates a "which keystone defines my build?" decision

### Inner Ring Expansion

Expand inner ring from 6 nodes to 12 — add a second ring:
- **Inner ring (radius 2):** Defensive/utility (current)
- **Middle ring (radius 4):** Offensive/specialized (new)
- Both rings connect to all class branches
- Creates more routing options and cross-class shortcuts (but at point cost)

---

## LAYER 2: ABILITY SPECIALIZATION TREES (inspired by Last Epoch)

Each of the game's abilities gets a **mini skill tree** (8-12 nodes) that transforms how the ability works. This is separate from the passive tree — uses its own "ability XP" earned by using the ability.

### How It Works
- Each ability has 3 upgrade paths within its mini-tree
- Ability XP earned by using the ability in combat
- Can max ~1 path fully, dabble in a second
- Transforms the ability's behavior, not just numbers

### Example: Piston Strike (Melee Ability)

**Path A — Pneumatic Hammer:**
- Hits become slower but much harder
- Final node: Single massive hit that staggers bosses

**Path B — Jackhammer:**
- Hits become rapid multi-strikes
- Final node: 8-hit combo that shreds armor

**Path C — Piston Launcher:**
- Strikes launch a shockwave projectile
- Final node: Shockwave pierces and bounces off walls

### Why This Works
- Adds ~75+ decisions per run without bloating the passive tree
- Same ability feels different every run based on spec path
- Natural progression — use ability → ability evolves
- Doesn't compete with passive tree points

### Ability Spec Slots (inspired by Last Epoch)
- Start with 2 ability spec slots
- Unlock 3rd at sector 3, 4th at sector 5
- Can have 4+ abilities but only 2-4 get specialized
- Creates "which abilities do I invest in?" decision

---

## LAYER 3: SPONSOR BOONS (inspired by Hades)

In-run offered upgrades from the reality TV sponsors. This is your Hades boon system re-themed.

### 5-6 Sponsors (Alien Corporations)
Each sponsor has a theme and offers upgrades that slot onto your abilities/stats:

| Sponsor | Theme | Buff Type | Color |
|---------|-------|-----------|-------|
| **KroxCorp** | Raw power | Flat damage, crit | Red |
| **NullTek** | Void/drain | Lifesteal, mana steal, debuffs | Purple |
| **FluxDyne** | Speed/mobility | Attack speed, move speed, dodge | Green |
| **ArmorAll** | Defense | Shields, armor, DR, thorns | Blue |
| **ScrapKing** | Loot/economy | Drop rates, gold, consumable power | Gold |
| **AXIS Labs** | Weird/chaos | Random effects, transformation, high risk/reward | White |

### Boon Mechanics
- After clearing certain rooms, a sponsor offers 3 boons (pick 1)
- Boons attach to specific slots: Weapon, Shield, Movement, Utility, Passive
- Higher rarity boons from sponsor loyalty (taking 3+ from same sponsor)
- **Sponsor Synergies** (Duo Boons): Having boons from 2 specific sponsors unlocks a powerful combo
  - Example: KroxCorp + FluxDyne = "Overdrive" — damage scales with movement speed
  - Example: NullTek + ArmorAll = "Void Armor" — absorbed damage converts to mana
- **Sponsor Rivalry:** Some sponsors conflict — taking too many from one reduces offerings from its rival

### Why This Layer Matters
- Adds per-run variety WITHOUT expanding the passive tree
- Creates emergent builds (this run I got offered NullTek + FluxDyne, so I'm building drain/speed)
- Fits the reality TV theme perfectly — sponsors literally sponsoring your run
- Discoverable synergies keep the game fresh over many runs

---

## POINT ECONOMY REBALANCE

| Aspect | Current | Proposed |
|--------|---------|----------|
| Passive tree points/run | ~29 | ~32-35 |
| Passive tree nodes | 96 | ~200-220 |
| Fill rate | 30% | 15-18% |
| Ability spec points | N/A | XP-based (use ability to level) |
| Sponsor boons/run | N/A | 8-12 offered, take ~8-10 |
| Firmware budget | N/A | 4-5 drain capacity |

### Points Per Level Adjustment
- Keep 2 points per level (current)
- Add bonus points from: sector completion (+2), boss kills (+1), optional challenges (+1)
- Total: ~35 points maximum for a thorough run
- 35 points / 200 nodes = **17.5% fill rate** — forces real specialization

---

## ANTI-TREE-JUMPING MECHANISMS (Ranked by Impact)

1. **Affinity gates** — Cross-class nodes require affinity earned by investing in specific branches. Natural investment in your class earns affinity; dipping costs points for less affinity return.

2. **Threshold gates** — Deep class nodes require X total points spent in that class branch. Can't cherry-pick the capstone without committing.

3. **Sub-branch splitting** — 3 sub-branches per class means even within your own class, you're making tradeoffs. Can't max all 3.

4. **Firmware budget** — Limited drain capacity means you can't stack all the best firmwares. Must choose.

5. **Keystone mutual exclusivity** — 2 keystones per class, can only take 1. First real "this or that" per class.

6. **Ability spec slots** — Only 2-4 abilities get specialization. Rest stay basic.

---

## BUILD VARIETY EXPANSION (Ranked by Impact)

1. **Sub-branches** — 3 per class × 6 classes = 18 build archetypes just from passive tree
2. **Ability specialization** — Each ability has 3 paths, creating huge combinatorial variety
3. **Sponsor boons** — Per-run randomization ensures no two runs feel the same
4. **Sponsor synergies** — Discoverable combos reward experimentation across runs
5. **Cross-class affinity combos** — Hybrid builds viable but expensive, creating a spectrum from "pure class" to "jack of all trades"
6. **Firmware choices** — Budget tradeoffs within the tree add another axis of customization
7. **Graft interactions** — Grafts + specific sub-branch perks = emergent combos

---

## IMPLEMENTATION PRIORITY

### Phase 1: Expand the Passive Tree (Biggest Impact)
- Add sub-branches to all 6 classes (3 per class)
- Add threshold gates
- Add 6 new keystones (1 per class, mutually exclusive with existing)
- Expand inner ring
- Target: ~200 nodes total
- **Estimated work:** Large — PassiveTreeBuilder.cs rewrite

### Phase 2: Ability Specialization Trees
- Design mini-trees for all abilities
- Implement ability XP system
- Add spec slot UI
- **Estimated work:** Medium — new system, but self-contained

### Phase 3: Sponsor Boon System
- Design 5-6 sponsors with themed boons
- Implement boon offering UI (post-room reward)
- Add duo-sponsor synergies
- **Estimated work:** Large — new system with UI, similar scope to graft system

### Phase 4: Affinity System
- Add affinity tracking to passive tree
- Add affinity requirements to cross-class nodes
- Update UI to show affinity levels
- **Estimated work:** Medium — extends existing tree system

### Phase 5: Firmware Budget
- Add firmware node type
- Implement drain/budget tracking
- Add UI for budget management
- **Estimated work:** Small — new node type + simple counter

---

## QUICK WINS (Can Do Now)

These improve the tree immediately without the full expansion:

1. **Add 2nd keystone per class** with mutual exclusivity — instant meaningful choice
2. **Split linear branches into Y-forks** — even just 1 fork per branch doubles decisions
3. **Add threshold gates** to existing keystones/pinnacles — require 5+ points in branch
4. **Make bridge notables more powerful** — currently weak, should be worth the detour
5. **Add more notable nodes** (currently only 16/96) — aim for 30%+ notable rate
