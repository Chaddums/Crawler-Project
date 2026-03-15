# Passive Tree Expansion — Full Implementation Plan

## Engine Capabilities Summary

The combat system supports these effect categories:
- **14 stat types** (Str, Dex, Con, Int, Cha, Luck, MaxHP, MaxMana, Armor, CritChance, CritDamage, AttackSpeed, MoveSpeed, CDR)
- **7 damage types** (Physical, Fire, Ice, Lightning, Poison, Dark, Holy)
- **Rich hook system**: pre-hit, post-hit, on-kill, on-damaged, on-dash, per-tick auras, room/sector events
- **Existing patterns**: damage conversion, HP-scaling damage, mana shielding, status spread, chaining, stacking with timers, proc chances, persistent ground effects, revival mechanics

**Everything below is buildable with the existing engine architecture.** New perks follow existing PerkProcessor patterns.

---

## PHASE 1: Expand the Tree Structure

### 1A. Sub-Branch System (3 paths per class)

Each class branch splits after 2 initial nodes into 3 themed sub-branches. Each sub-branch has 8-10 nodes (mix of basic + notable + 1 capstone).

**Node budget per class:**
- 1 ClassStart
- 2 Shared trunk nodes (before split)
- 3 sub-branches × 8 nodes = 24 branch nodes
- 2 Keystones (mutually exclusive, off trunk)
- 1 Pinnacle (requires 1 keystone)
- 1 CoreSocket
- **Total per class: ~31 nodes**
- **6 classes × 31 = 186 class nodes**

**Shared nodes:**
- Inner ring: 12 nodes (expanded from 6)
- Bridge notables: 6 (existing, buffed)
- Bridge connectors: 12 (existing)
- **Total shared: 30 nodes**

**Grand total: ~216 nodes** (up from 96)

### 1B. Threshold Gates

Add to PassiveNodeData:
```csharp
public int RequiredPointsInBranch;  // 0 = no gate
public string RequiredBranchId;     // which branch counts
```

Deep capstone nodes require 6-8 points already spent in that sub-branch. Keystones require 4 points in trunk.

### 1C. Keystone Mutual Exclusivity

Add to PassiveTree:
```csharp
public string MutuallyExclusiveWith;  // keystone node ID
```

Each class gets 2 keystones. Allocating one grays out the other.

---

## PHASE 2: Notable & Keystone Effect Design

### Design Philosophy
- **Basic nodes**: Still grant stats, but 2-3 stats per node and/or conditional bonuses
- **Notables**: Grant a perk effect (mechanical change, not just numbers)
- **Keystones**: Build-defining with meaningful tradeoff
- **Capstones**: Sub-branch payoff, powerful but requires deep investment

### SCRAPHEAP (Tank/Brawler)

#### Keystone A: Berserker Protocol (existing, enhanced)
- **Up:** +2% damage per 1% HP missing. Below 30% HP: +15% attack speed.
- **Down:** -25% max HP. Cannot heal above 75% HP.

#### Keystone B: Ablative Plating *(NEW)*
- **Up:** First hit each room deals 0 damage. Every 10 seconds, reset this shield. +20% armor.
- **Down:** -20% move speed. -50% healing received.

#### Sub-Branch 1: JUGGERNAUT (HP + Damage Reduction)
| Node | Type | Effect |
|------|------|--------|
| Reinforced Frame | Basic | +15% MaxHP, +5 Armor |
| Hardened Alloy | Basic | +8 Armor, +5% HP |
| Blast Shield | Notable | **Perk: Explosion Dampener** — AoE damage reduced by 40%. Gain +2 armor for each enemy within 5m (max 10). |
| Layered Plating | Basic | +10% Armor, +10% HP |
| Shock Absorber | Basic | +3% damage reduction per 100 armor (max 15%) |
| Impact Dampener | Notable | **Perk: Kinetic Battery** — 10% of damage taken is stored (max 200). Next basic attack releases stored damage as bonus Physical. Resets after 5s of not being hit. |
| Titanium Core | Basic | +20% MaxHP, +5% Constitution |
| **Unstoppable** | Capstone | **Perk: Unstoppable Force** — While above 80% HP: immune to stun, slow, and knockback. Below 50% HP: +30% damage and +20% move speed (adrenaline panic). |

#### Sub-Branch 2: BERSERKER (Damage scales with risk)
| Node | Type | Effect |
|------|------|--------|
| Overdriven Servos | Basic | +10% AttackSpeed, +5% Strength |
| Frenzied Wiring | Basic | +8% AttackSpeed when below 50% HP |
| Blood Oil | Notable | **Perk: Leaking Fuel** — Basic attacks heal 3% of damage dealt. Each heal reduces armor by 1 for 3s (stacks). |
| Stripped Limiters | Basic | +15% CritDamage, +5% CritChance |
| Short Circuit | Basic | +20% damage for 2s after taking damage |
| Rage Core | Notable | **Perk: Rage Accumulator** — Gain 1 Rage stack per hit taken (max 10, 5s timer). Each stack: +3% damage, +2% attack speed. At 10 stacks: discharge — deal 300% weapon damage AoE, lose all stacks. |
| Meltdown Protocol | Basic | +5% damage per active debuff on you (max 25%) |
| **Overdrive** | Capstone | **Perk: Red Line** — Your HP continuously drains at 3%/s. While draining: +50% all damage, +30% attack speed. Basic attacks heal 5% damage dealt. Toggled on/off. |

#### Sub-Branch 3: FORTRESS (Shields + Retaliation)
| Node | Type | Effect |
|------|------|--------|
| Reactive Hull | Basic | +10 Armor, +3% Thorns |
| Mag-Lock Plating | Basic | +5% Armor, block 5 flat damage from each hit |
| Spiked Chassis | Notable | **Perk: Electrified Hull** — Melee attackers take 20 Lightning damage + are stunned 0.3s. 1s cooldown per target. |
| Bulwark Stance | Basic | +15% Armor while standing still for 1s+ |
| Anchored Frame | Basic | +5% DR, -10% move speed (permanent tradeoff) |
| Fortress Wall | Notable | **Perk: Taunt Emitter** — Every 8s, emit a taunt pulse (10m radius). Taunted enemies deal 15% less damage to allies. You take +10% damage from taunted enemies. |
| Siege Mode | Basic | +20% Armor, +10% damage while not moving |
| **Iron Curtain** | Capstone | **Perk: Last Stand** — When below 25% HP: become immobile for 3s. During Last Stand: +100% armor, reflect 50% of all damage taken, heal 5% HP/s. 90s cooldown. |

---

### TINCAN (Defender/Support)

#### Keystone A: Iron Fortress (existing, enhanced)
- **Up:** +50% healing received. +30 Armor. Healing abilities affect nearby allies (4m).
- **Down:** Cannot dash. -15% move speed.

#### Keystone B: Galvanic Core *(NEW)*
- **Up:** Your armor value is added to your ability damage as flat bonus. +20% CDR.
- **Down:** -40% basic attack damage. -20% MaxHP.

#### Sub-Branch 1: BULWARK (Block/Parry)
| Node | Type | Effect |
|------|------|--------|
| Reinforced Joints | Basic | +10 Armor, +5% Constitution |
| Quick Calibration | Basic | +5% AttackSpeed, +3% CDR |
| Deflector Array | Notable | **Perk: Auto-Parry** — 15% chance to negate incoming damage completely. When triggered: next attack within 1s deals +40% damage. Visual: spark flash on parry. |
| Hardened Servos | Basic | +8 Armor, +5% CritChance |
| Counter-Weight | Basic | +10% damage after being hit (2s window) |
| Adaptive Shield | Notable | **Perk: Elemental Tuning** — After taking elemental damage, gain 30% resistance to that element for 5s. Stacks with each new element. Lose all stacks after 8s of no damage taken. |
| Locked Joints | Basic | +15% Armor, cannot be knocked back |
| **Mirror Plating** | Capstone | **Perk: Full Reflect** — Every 4th hit taken is completely reflected back at the attacker at 200% damage. A visible counter (1/2/3/REFLECT!) shows progress. |

#### Sub-Branch 2: GUARDIAN (Ally Buffs/Healing)
| Node | Type | Effect |
|------|------|--------|
| Broadcast Antenna | Basic | +10% MaxMana, +5% Charisma |
| Signal Boost | Basic | +15% healing done, +5% CDR |
| Field Medic | Notable | **Perk: Repair Beacon** — On kill, drop a repair field (3m radius, 5s duration) that heals 3% max HP/s to allies inside. |
| Extended Range | Basic | +10% ability range, +5% Charisma |
| Sympathetic Link | Basic | +5% of your armor also applies to companions |
| Rallying Cry | Notable | **Perk: Overclock Allies** — When you use an ability, nearby allies (6m) gain +10% attack speed for 3s. 5s internal cooldown. |
| Shared Power | Basic | +10% healing received, +10% healing done |
| **Network Hub** | Capstone | **Perk: Distributed Processing** — All stat bonuses you receive are shared at 20% to allies within 8m. When an ally takes lethal damage, absorb it and take 50% of that damage instead (30s cooldown per ally). |

#### Sub-Branch 3: SENTINEL (Counter-Attack)
| Node | Type | Effect |
|------|------|--------|
| Targeting Subroutine | Basic | +5% CritChance, +10% CritDamage |
| Overwatch Module | Basic | +10% damage to enemies targeting allies |
| Interceptor | Notable | **Perk: Intercept Protocol** — When an ally is hit, 20% chance you fire a retaliatory shot at the attacker dealing 150% weapon damage. 0.5s cooldown. |
| Threat Assessment | Basic | +5% damage per unique enemy type in room (max 20%) |
| Tracking Lock | Basic | Attacks against enemies you've hit in the last 3s deal +8% damage |
| Mark Target | Notable | **Perk: Vulnerability Scanner** — Your critical hits mark enemies for 4s. Marked enemies take +15% damage from all sources. Only 1 mark active at a time. |
| Combat Analysis | Basic | +3% CritChance per consecutive hit on same target (max 15%, resets on target switch) |
| **Overwatch** | Capstone | **Perk: Sentinel Protocol** — While standing still for 2s+: attack range doubles, gain +25% CritChance, basic attacks pierce through enemies. Moving cancels this state. |

---

### SPARKPLUG (Energy/Caster)

#### Keystone A: Mana Shield (existing, enhanced)
- **Up:** 30% of damage absorbed by mana. While mana > 50%: +15% ability damage.
- **Down:** -30% max mana regen. When mana hits 0: stunned for 1s.

#### Keystone B: Arcane Conduit *(NEW)*
- **Up:** Abilities that hit 3+ enemies refund 40% mana cost. Status effects you apply last 50% longer.
- **Down:** Single-target abilities deal -25% damage. -15% MaxHP.

#### Sub-Branch 1: OVERCHARGE (Mana → Damage)
| Node | Type | Effect |
|------|------|--------|
| Power Surge | Basic | +10% Intelligence, +5% ability damage |
| Volatile Capacitor | Basic | +15% CritDamage on abilities, +5% MaxMana |
| Mana Burn | Notable | **Perk: Energy Overload** — Abilities deal bonus damage equal to 8% of your current mana. Incentivizes staying high mana. |
| Focused Beam | Basic | +10% ability damage, -5% AoE radius (more focused) |
| Unstable Core | Basic | +20% ability damage when mana > 75% |
| Arcane Detonation | Notable | **Perk: Mana Bomb** — When you spend 100+ mana within 3s, trigger an explosion around you dealing 200% Intelligence as damage. 8s cooldown. |
| Power Siphon | Basic | +5% mana leech on ability damage |
| **Critical Mass** | Capstone | **Perk: Supernova** — Critical ability hits have a 20% chance to reset that ability's cooldown. Each reset in a row reduces the chance by 5% (20% → 15% → 10%...). Resets to 20% after 5s of no crits. |

#### Sub-Branch 2: CONDUIT (Chain/Spread/AoE)
| Node | Type | Effect |
|------|------|--------|
| Wide Frequency | Basic | +15% AoE radius, +5% Intelligence |
| Charged Air | Basic | +10% Lightning damage, +5% status chance |
| Arc Welder | Notable | **Perk: Lightning Rod** — Lightning damage chains to 2 additional targets at 30% damage. Enemies hit by chains have -10% Lightning resistance for 3s. |
| Static Field | Basic | +10% status effect duration |
| Charged Ground | Basic | +8% damage for each enemy within 8m (max 40%) |
| Cascade Failure | Notable | **Perk: Status Cascade** — When a status-effected enemy dies, their status effects jump to 2 nearby enemies at 80% remaining duration. |
| Overloaded Grid | Basic | +15% AoE damage, +5% chance to apply random debuff |
| **Chain Reaction** | Capstone | **Perk: Propagation** — All your damage has a 10% chance to chain to a nearby enemy at 50% damage. Chains can chain (up to 3 bounces). Each bounce: -15% damage. |

#### Sub-Branch 3: CAPACITOR (Mana Shield/Regen/CDR)
| Node | Type | Effect |
|------|------|--------|
| Efficient Wiring | Basic | +15% MaxMana, +5% mana regen |
| Thermal Vent | Basic | +10% CDR, +5% MaxMana |
| Mana Weave | Notable | **Perk: Regeneration Field** — While standing still: regenerate 3% max mana/s. While above 80% mana: +10% armor. |
| Battery Bank | Basic | +20% MaxMana |
| Flow State | Basic | CDR increases by 1% per 50 current mana (max 10%) |
| Power Recycler | Notable | **Perk: Spell Echo** — Every 4th ability cast costs 0 mana. Counter is visible on UI (1/2/3/FREE!). |
| Deep Reserve | Basic | +10% MaxMana, heal 5% HP when spending 50+ mana |
| **Infinite Loop** | Capstone | **Perk: Perpetual Engine** — Ability cooldowns tick 50% faster while mana > 50%. When mana drops below 25%: instantly refill 30% mana and all abilities go on 3s cooldown. 45s internal cooldown. |

---

### RUSTBUCKET (Rogue/Stealth)

#### Keystone A: Glass Cannon (existing, enhanced)
- **Up:** +50% all damage. Critical hits deal +25% bonus damage.
- **Down:** +40% damage taken. -20% MaxHP.

#### Keystone B: Shadow Processor *(NEW)*
- **Up:** First hit on each enemy deals +80% damage ("ambush bonus"). After dashing: invisible for 1s.
- **Down:** Second+ hits on same target deal -20% damage. -30% Armor.

#### Sub-Branch 1: INFILTRATOR (Crit/Backstab)
| Node | Type | Effect |
|------|------|--------|
| Precision Targeting | Basic | +8% CritChance, +5% Dexterity |
| Weak Point Scanner | Basic | +15% CritDamage |
| Exposed Wiring | Notable | **Perk: Armor Shred** — Critical hits reduce target's armor by 10% for 4s (stacks up to 3 times, -30% total). |
| Surgical Strike | Basic | +10% damage to targets above 80% HP |
| Lethal Calibration | Basic | +3% CritChance per consecutive crit (max 15%, resets on non-crit) |
| Execution Protocol | Notable | **Perk: Execute** — Targets below 20% HP take +50% damage from you. Kills on low-HP targets have +25% chance to drop rare loot. |
| Hunter's Mark | Basic | +10% damage to targets you've crit in last 5s |
| **Assassin Core** | Capstone | **Perk: Death Mark** — Every 5th critical hit applies Death Mark to the target (4s duration). Death Marked enemies take 3x damage from your next hit. If that hit kills: reset all ability cooldowns. |

#### Sub-Branch 2: SABOTEUR (Traps/DoT/Debuffs)
| Node | Type | Effect |
|------|------|--------|
| Corrosive Rounds | Basic | +10% Poison damage, +5% status duration |
| Acid Bath | Basic | +8% DoT damage, +5% Dexterity |
| Toxic Payload | Notable | **Perk: Poison Cloud** — Enemies killed while poisoned explode into a poison cloud (3m radius, 4s, deals 50% of poison DPS). |
| Weakening Agent | Basic | +10% debuff effectiveness |
| Lingering Effect | Basic | +20% status effect duration |
| Crippling Strike | Notable | **Perk: Systemic Failure** — Enemies with 3+ debuffs take +25% damage from all sources and have -30% move speed. |
| Viral Load | Basic | Status effects you apply have 15% chance to spread on hit |
| **Pandemic** | Capstone | **Perk: Viral Cascade** — Your DoTs can crit (at 50% of your crit chance). DoT crits deal 150% tick damage and spread the DoT to 1 nearby enemy. |

#### Sub-Branch 3: SCAVENGER (Loot/Economy/Survival)
| Node | Type | Effect |
|------|------|--------|
| Salvage Scanner | Basic | +15% Luck, +10% item find |
| Quick Hands | Basic | +10% AttackSpeed, +5% MoveSpeed |
| Opportunist | Notable | **Perk: Scrap Magnet** — Pickup radius doubled. Picking up any item heals 3% HP and grants +5% move speed for 2s. |
| Lucky Find | Basic | +10% Luck, +5% rare drop chance |
| Efficient Recycler | Basic | Consumables are 30% more effective |
| Bargain Hunter | Notable | **Perk: Vendor Discount** — Shop prices -25%. Selling items gives +50% gold. Every 500 gold earned: +1% all damage (permanent for run, max 20%). |
| Treasure Sense | Basic | +15% item find, minimap shows rare items |
| **Jackpot** | Capstone | **Perk: Golden Touch** — Kills have a 5% chance to drop a random consumable. Bosses always drop an extra loot box. Every 10th pickup grants a random temporary buff (10s). |

---

### NOISEBOX (Bard/Controller)

#### Keystone A: Entropy Field (existing, enhanced)
- **Up:** Enemies within 8m take 3% MaxHP/s as Dark damage. Each debuffed enemy in range: +5% ability damage (max 30%).
- **Down:** You take 2% MaxHP/s per debuff on YOU. -15% healing received.

#### Keystone B: Harmonic Resonance *(NEW)*
- **Up:** Your abilities apply their status effects in an AoE (3m around target). +20% status duration.
- **Down:** Ability damage -25%. Abilities cost +20% mana.

#### Sub-Branch 1: BROADCAST (Aura Buffs/Debuffs)
| Node | Type | Effect |
|------|------|--------|
| Signal Amplifier | Basic | +5% Charisma, +10% aura radius |
| Wide Band | Basic | +10% aura effectiveness, +5% Intelligence |
| Morale Booster | Notable | **Perk: Rally Frequency** — Allies within 8m gain +10% damage and +5% move speed. You gain these bonuses doubled (+20%/+10%). |
| Interference Pattern | Basic | +10% debuff effectiveness on enemies |
| Jamming Signal | Basic | Enemies within 6m have -10% attack speed |
| Disruptive Aura | Notable | **Perk: Frequency Jam** — Every 6s, emit a pulse (8m). All enemies: -20% damage for 3s. Debuffed enemies are also slowed 15%. |
| Resonant Field | Basic | +15% aura radius, +5% Charisma |
| **Conductor** | Capstone | **Perk: Orchestrator** — Your aura effects stack. Each aura you have active: +8% to ALL aura effects. With 3+ auras: enemies in range can't regenerate HP. |

#### Sub-Branch 2: DISSONANCE (Confusion/Fear/Reflect)
| Node | Type | Effect |
|------|------|--------|
| Feedback Spike | Basic | +10% damage reflected, +5% Intelligence |
| Disorienting Burst | Basic | +10% chance to confuse on hit (attack random target for 2s) |
| Cacophony | Notable | **Perk: Sonic Overload** — Every 10s, emit a scream (6m). Enemies: 50% chance to flee for 2s OR attack an ally for 2s. Bosses: -20% damage for 3s instead. |
| Noise Floor | Basic | +8% damage per debuffed enemy in 8m (max 32%) |
| Signal Corruption | Basic | Debuffs you apply have 20% chance to double duration |
| Mind Worm | Notable | **Perk: Neural Virus** — Confused enemies take +30% damage. If a confused enemy kills another enemy, you gain the XP and the kill counts for on-kill effects. |
| Echo Chamber | Basic | +10% of ability damage is dealt again after 1s delay |
| **Pandemonium** | Capstone | **Perk: Total Chaos** — Confused/feared enemies have a 15% chance per second to permanently switch sides (become your ally for the room). Max 3 converted enemies. Converted enemies deal +50% damage. |

#### Sub-Branch 3: RESONANCE (Status Amplification/Combos)
| Node | Type | Effect |
|------|------|--------|
| Harmonic Frequency | Basic | +10% status effect damage, +5% duration |
| Sympathetic Vibration | Basic | Status effects deal +15% damage on first tick |
| Combo Amplifier | Notable | **Perk: Element Synergy** — Applying a 2nd different element to a target triggers a combo burst: Fire+Ice = Steam (AoE blind 2s), Lightning+Poison = Toxic Shock (+50% DoT), Fire+Lightning = Overload (AoE 150% damage). |
| Resonant Frequency | Basic | +20% status effect duration |
| Sustained Oscillation | Basic | Status effects don't expire while you're within 6m of the target |
| Frequency Lock | Notable | **Perk: Permanent Debuff** — Status effects you apply that last their full duration have a 30% chance to become permanent (for the room). Max 2 permanent debuffs per enemy. |
| Deep Vibration | Basic | +10% DoT damage, DoTs tick 20% faster |
| **Harmonic Convergence** | Capstone | **Perk: Ultimate Combo** — Enemies with 4+ status effects detonate for 500% combined DoT damage as burst. Detonation spreads all status effects to enemies in 5m. 3s cooldown per target. |

---

### CLUNKER (Brawler/Berserker)

#### Keystone A: Overclocked (existing, enhanced)
- **Up:** +25% all stats. Basic attacks deal bonus damage equal to 5% target max HP.
- **Down:** Take 5 DPS. Cannot heal above 80% HP.

#### Keystone B: Rampage Core *(NEW)*
- **Up:** Each kill within 5s of the last grants +10% damage and +5% move speed (stacks up to 5, resets if 5s pass without a kill). At 5 stacks: abilities have no cooldown for 3s.
- **Down:** -30% damage when no kill stacks. -20% MaxMana.

#### Sub-Branch 1: PISTON (Melee DPS/Speed)
| Node | Type | Effect |
|------|------|--------|
| Rapid Pistons | Basic | +10% AttackSpeed, +5% Strength |
| Precision Gears | Basic | +5% CritChance, +10% CritDamage |
| Combo Driver | Notable | **Perk: Combo Engine** — Each consecutive hit on the same target: +5% damage (max +30%). Missing resets combo. At 6+ combo: attacks cleave to nearby enemies at 40% damage. |
| Lubricated Joints | Basic | +8% AttackSpeed, +3% MoveSpeed |
| Revving Up | Basic | +3% AttackSpeed per second in combat (max 30%, resets out of combat) |
| Piledriver | Notable | **Perk: Impact Charge** — Every 5th basic attack is a charged strike dealing 250% damage with knockback. Charged strikes always crit. |
| Perpetual Motion | Basic | +5% attack speed while moving |
| **Machine Gun** | Capstone | **Perk: Infinite Combo** — Basic attacks have no attack speed cap (normally 200%). Each hit grants +1% attack speed for 10s (stacks infinitely). Attack speed bonus also increases move speed at 50% rate. You lose 1% MaxHP per second while above 150% attack speed. |

#### Sub-Branch 2: WRECKING BALL (AoE/Knockback)
| Node | Type | Effect |
|------|------|--------|
| Heavy Frame | Basic | +10% Strength, +5% AoE radius |
| Seismic Treads | Basic | +10% knockback force, +5% AoE damage |
| Ground Pound | Notable | **Perk: Seismic Slam** — Dashing into enemies deals 100% Strength as Physical damage and staggers (0.5s). Enemies knocked into walls take double damage. |
| Massive Impact | Basic | +15% AoE damage |
| Collateral Damage | Basic | Knockback deals 50% of hit damage to other enemies the target collides with |
| Wrecking Swing | Notable | **Perk: Demolition** — Basic attacks hit in a 180° arc (up from single target). Arc attacks deal 60% damage. Destructible objects in the arc always drop loot. |
| Earthquake Treads | Basic | Walking near enemies (2m) deals 5% Strength/s as Physical damage |
| **Meteor Drop** | Capstone | **Perk: Orbital Strike** — Hold dash to charge (up to 2s). Release to leap to target location. Impact: deals 300-800% Strength as damage in 5m radius (scales with charge time). Landing creates a 3s slow field (-40% enemy move speed). 15s cooldown. |

#### Sub-Branch 3: SCRAP ENGINE (On-Kill/Momentum)
| Node | Type | Effect |
|------|------|--------|
| Kill Fuel | Basic | +5% HP restored on kill, +5% Strength |
| Battle Hunger | Basic | +10% damage for 3s after killing an enemy |
| Salvage Protocol | Notable | **Perk: Part Scavenger** — Kills have 20% chance to drop a temporary part (15s): Scrap Blade (+15% damage), Scrap Shield (+10 armor), or Scrap Booster (+20% speed). |
| Bloodlust Wiring | Basic | +3% attack speed per kill in last 5s (max 15%) |
| Feeding Frenzy | Basic | +8% lifesteal for 3s after killing 2+ enemies within 1s |
| Momentum Engine | Notable | **Perk: Kill Chain** — Kills within 2s of each other grant +20% damage per chain (1 kill = 0%, 2 in 2s = +20%, 3 in 2s = +40%, etc., max +100%). Chain breaks after 2s with no kill. |
| Second Wind | Basic | Killing an enemy while below 30% HP heals 15% MaxHP |
| **Annihilation Engine** | Capstone | **Perk: Exterminator** — Enemies below 15% HP are instantly executed. Each execution within 5s of the last: +5% execution threshold (15% → 20% → 25%... max 35%). Executed enemies explode for 200% of their remaining HP as AoE damage. |

---

## PHASE 3: Inner Ring & Bridge Rework

### Expanded Inner Ring (12 nodes)

**Ring 1 (Defensive, radius 2):** 6 nodes
| Node | Type | Effect |
|------|------|--------|
| Hardened Shell | Basic | +10 Armor, +5% MaxHP |
| Power Reserve | Basic | +10% MaxMana, +5% mana regen |
| Quick Recovery | Basic | +5% MoveSpeed, +3% CDR |
| Core Stability | Basic | +5% all resistances |
| Emergency Repairs | Notable | **Perk:** Below 25% HP: heal 20% MaxHP (60s CD). Now also grants 3s of +50% armor after trigger. |
| Adaptive Plating | Notable | **Perk:** +3% armor per enemy within 8m (max 15%). Now also grants +2% damage per enemy (max 10%). |

**Ring 2 (Offensive, radius 4):** 6 new nodes
| Node | Type | Effect |
|------|------|--------|
| Targeting Matrix | Basic | +5% CritChance, +10% CritDamage |
| Overclock Module | Basic | +8% AttackSpeed, +5% ability damage |
| Elemental Converter | Notable | **Perk: Type Shift** — Your damage type cycles every 5 hits (Physical → Fire → Ice → Lightning → Poison). +10% damage for matching enemy weakness. |
| Multi-Target Processor | Notable | **Perk: Splash Protocol** — Single-target attacks deal 25% of their damage in a 3m radius around the target. |
| Scavenger Module | Basic | +10% Luck, +10% item find |
| Survivor's Instinct | Notable | **Perk: Last Resort** — While below 30% HP: +20% damage, +20% move speed, +10% dodge chance. Heal 2% MaxHP/s while below 15% HP. |

### Buffed Bridge Notables (6)

Bridges should be worth the 3-4 point detour. Each now has a powerful cross-class synergy perk:

| Bridge | Classes | Effect |
|--------|---------|--------|
| Resonance | SparkPlug↔RustBucket | **Energy Blade** — Crit damage is dealt again as Lightning after 0.5s (30% of original). Crits restore 3% mana. |
| Exploit Weakness | RustBucket↔NoiseBox | **Predator Protocol** — Debuffed enemies take +20% damage. You deal +10% damage for each unique debuff on target (max 40%). |
| Adrenaline Rush | Clunker↔Scrapheap | **Berserker Network** — On kill: +15% speed and +8% crit for 4s. While this buff is active, kills extend it by 2s and refresh stacks. |
| Fortified Circuits | Scrapheap↔TinCan | **Perk: Juggernaut Link** — Armor also increases healing received (1% per 10 armor, max 20%). When healed: gain +5% damage for 3s. |
| Arcane Plating | TinCan↔SparkPlug | **Perk: Mana Armor** — 15% of max mana added as flat armor. Spending mana generates a shield equal to 10% of mana spent (max 50 shield, decays at 10/s). |
| Chaos Engine | NoiseBox↔Clunker | **Perk: Feedback Frenzy** — Kill speed increases status effect damage (+5% per kill in last 5s, max 25%). Status-affected enemies drop repair orbs on death (10% chance). |

---

## PHASE 4: Affinity System

### 5 Affinity Types

| Affinity | Color | Earned From | Theme |
|----------|-------|------------|-------|
| Military | Red | Scrapheap + Clunker nodes | Damage, armor, HP |
| Tech | Blue | SparkPlug + TinCan nodes | Mana, CDR, abilities |
| Stealth | Green | RustBucket + NoiseBox nodes | Crit, status, debuffs |
| Scrap | Orange | Inner ring, bridge, basic nodes | Universal/neutral |
| Alien | Purple | Core sockets, specific notables | Rare, graft-related |

### Affinity Earnings
- Basic node in-class: +1 affinity in that class's color
- Notable in-class: +2 affinity
- Keystone: +3 affinity
- Capstone: +3 affinity
- Bridge nodes: +1 Scrap affinity
- Inner ring: +1 Scrap affinity
- Core socket (when filled): +2 Alien affinity

### Affinity Gates on Cross-Class Nodes
- Bridge notables: Require 3 in each adjacent class's affinity
- Second ring notables: Require 5 Scrap
- Deep cross-class dipping: Require 8+ in target class's affinity (effectively 8+ points spent there)

### Implementation
Add to PassiveNodeData:
```csharp
public Dictionary<AffinityType, int> AffinityGrant;    // what this node gives
public Dictionary<AffinityType, int> AffinityRequired;  // what's needed to allocate
```

Add to PassiveTree:
```csharp
public Dictionary<AffinityType, int> CurrentAffinity;   // accumulated from allocated nodes
```

---

## PHASE 5: Firmware Budget System

### Power Budget
- Each bot frame has a **Firmware Capacity** of 5
- Firmware nodes have a **Drain** cost (1-3)
- Can only allocate firmware if total drain ≤ capacity
- Capacity can be increased by: reactor upgrades (meta-progression), specific rare grafts (+1 capacity)

### Firmware Nodes (18 total, 3 per class)

Located as separate off-branch options, not on the main paths:

**Scrapheap Firmware:**
| Name | Drain | Effect |
|------|-------|--------|
| Siege Mode | 3 | Can't move while attacking. +80% damage, +50% armor, attacks hit in 360° |
| Regeneration Matrix | 2 | +3% HP regen/s. Heal effectiveness +30%. No mana regen. |
| Ablative Nanites | 1 | Take 15% less damage from hits that deal more than 20% MaxHP |

**TinCan Firmware:**
| Name | Drain | Effect |
|------|-------|--------|
| Guardian Protocol | 3 | All damage to allies in 6m redirected to you at 60%. +40% armor while redirecting. |
| Overwatch System | 2 | Auto-fire at nearest enemy every 2s for 80% weapon damage. Can't control target. |
| Repair Subroutines | 1 | Heal 1% MaxHP/s. Doubles while standing still. |

**SparkPlug Firmware:**
| Name | Drain | Effect |
|------|-------|--------|
| Spell Amplifier | 3 | Abilities deal +60% damage. Abilities cost +40% mana. Mana regen +30%. |
| Elemental Mastery | 2 | +20% elemental damage. Elemental kills leave a ground pool of that element (3s, deals 50% DPS). |
| Mana Siphon | 1 | Basic attacks restore 3% MaxMana. -10% basic attack damage. |

**RustBucket Firmware:**
| Name | Drain | Effect |
|------|-------|--------|
| Assassination Protocol | 3 | First hit on each enemy: +100% CritDamage. 2nd+ hits: -15% damage. |
| Poison Mastery | 2 | All damage has 20% chance to apply poison. Poison damage +40%. |
| Evasion Matrix | 1 | 10% chance to dodge attacks. Dodging grants +20% speed for 1s. |

**NoiseBox Firmware:**
| Name | Drain | Effect |
|------|------|--------|
| Broadcast Tower | 3 | All aura radii +50%. Aura effects +30%. Your auras persist for 3s after leaving range. |
| Neural Override | 2 | 5% chance per hit to mind control enemy for 5s. Max 2 controlled. -10% damage to controlled enemies. |
| Status Amplifier | 1 | Status effect damage +20%. Duration +20%. |

**Clunker Firmware:**
| Name | Drain | Effect |
|------|-------|--------|
| Overdrive Engine | 3 | +40% attack speed. +20% move speed. Take 3 DPS. Each kill heals 2% MaxHP. |
| Heavy Weapons | 2 | Basic attacks deal +30% damage as AoE (3m). -15% attack speed. |
| Momentum Drive | 1 | +1% damage per second in combat (max +30%). Resets when hit 3 times in 2s. |

---

## FULL IMPLEMENTATION ROADMAP

### Phase 1: Tree Structure (Estimated: 2-3 sessions)
**Files:** PassiveTreeBuilder.cs (major rewrite), PassiveNodeData.cs (add fields), PassiveTree.cs (threshold/exclusivity logic)
- Restructure 6 class branches into 3 sub-branches each
- Add threshold gate checks to allocation logic
- Add keystone mutual exclusivity
- Add 2nd keystone per class
- Expand inner ring to 12 nodes
- Wire up all connections
- Update PassiveTreeUI.cs for new layout (may need zoom/pan)
- **Total new nodes: ~120 new nodes (96 → 216)**

### Phase 2: New Perk Effects (Estimated: 3-4 sessions)
**Files:** PerkProcessor.cs (major expansion), Perks.cs (new IDs), PlayerCombat.cs (new hooks)
- Implement all notable/keystone/capstone perk effects
- ~60 new perks to implement (currently 24 tree perks → ~84)
- Most follow existing patterns in PerkProcessor
- Some need new hooks: confusion, fear, permanent debuff, combo element bursts
- New status effects: Confuse, Fear, Death Mark, Armor Shred
- **Estimated new Perks.cs constants: ~60**
- **Estimated PerkProcessor.cs growth: +1500 lines (currently ~1524)**

### Phase 3: Affinity System (Estimated: 1-2 sessions)
**Files:** PassiveNodeData.cs, PassiveTree.cs, PassiveTreeBuilder.cs, PassiveTreeUI.cs
- Add AffinityType enum (5 types)
- Add affinity grant/require to node data
- Track accumulated affinity in PassiveTree
- Check affinity requirements on allocation
- UI: show affinity levels on tree screen
- Assign affinity values to all 216 nodes

### Phase 4: Firmware Budget (Estimated: 1-2 sessions)
**Files:** PassiveNodeData.cs (add Drain field), PassiveTree.cs (budget tracking), PerkProcessor.cs (18 firmware effects), PassiveTreeUI.cs (budget display)
- Add Firmware node type
- Implement drain budget
- 18 firmware perks (3 per class)
- UI for budget display

### Phase 5: Bridge & Inner Ring Rework (Estimated: 1 session)
**Files:** PassiveTreeBuilder.cs, PerkProcessor.cs
- Buff existing bridges with new perk effects (6 new perks)
- Add 6 new inner ring nodes with perks
- Connect new ring to existing tree

### Phase 6: Ability Specialization Trees (Estimated: 3-4 sessions)
**Files:** NEW system — AbilitySpecTree.cs, AbilitySpecData.cs, AbilitySpecUI.cs, PlayerCombat.cs integration
- Design mini-trees for each ability (3 paths × 8 nodes per ability)
- Implement ability XP system
- Spec slot management
- UI for ability tree screen
- **This is a standalone system, can be done independently**

### Phase 7: Sponsor Boon System (Estimated: 3-4 sessions)
**Files:** NEW system — SponsorManager.cs, BoonData.cs, SponsorRegistry.cs, BoonUI.cs, BoonProcessor.cs
- 5-6 sponsors with themed boon pools
- Boon offering after room clears
- Sponsor loyalty tracking
- Duo-sponsor synergy system
- **This is a standalone system, can be done independently**

---

## SUMMARY

| Metric | Current | After Full Expansion |
|--------|---------|---------------------|
| Passive tree nodes | 96 | ~216 |
| Points per run | ~29 | ~32-35 |
| Fill rate | 30% | ~16% |
| Tree perks (mechanical effects) | 24 | ~84 |
| Keystones per class | 1 | 2 (mutually exclusive) |
| Sub-branches per class | 0 (linear) | 3 (with capstones) |
| Firmware slots | N/A | 5 drain budget |
| Affinity types | N/A | 5 |
| Ability spec trees | N/A | 1 per ability (Phase 6) |
| Sponsor boons | N/A | 5-6 sponsors (Phase 7) |
| "Real decisions" per run | ~6 | ~20-25 |

### New Effect Categories Beyond +Stats:
- Damage conversion (mana→damage, armor→damage, HP→damage)
- Conditional scaling (missing HP, enemy count, combo, kill chains)
- Proc chains (chain lightning, status cascade, crit chains)
- Elemental combos (Fire+Ice=Steam blind, Lightning+Poison=Toxic Shock)
- Risk/reward toggles (Red Line, Overclocked, Glass Cannon)
- Build-around mechanics (execute thresholds, parry counters, spell echo)
- Aura stacking and interaction
- Status effect manipulation (permanent debuffs, spread, combo detonation)
- Resource conversion (Blood Economy, Mana Bomb, mana armor)
- Spatial mechanics (standing still bonuses, ground pools, aura radii)
