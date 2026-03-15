# Skill Tree / Passive Tree / Perk Tree Design Research
## For Junkbot Arena — Roguelike Dungeon Crawler

---

# PART 1: ARPG SYSTEMS

---

## 1. Path of Exile 2 — Passive Skill Tree

**Structure:** Massive shared web/network graph. All classes share one tree (~1,500+ nodes). Classes start in the center but with different starting nodes tuned to their archetype (e.g., Sorceress starts near Elemental Damage, Witch near Chaos/Minion Damage).

**Node Types:**
- **Small/Attribute Nodes** (~1,800+): Simple stat increases (+3% attack speed, +10 life, etc.). These are "travel" nodes — you path through them to reach the good stuff.
- **Notable Passives** (~500+): Medium-power nodes with meaningful bonuses. Usually the target destination when pathing.
- **Keystones** (~16+): Powerful build-defining nodes with significant trade-offs (e.g., "Your critical strikes deal no extra damage, but you gain a massive bonus to base damage"). Integrated throughout the tree rather than just at edges.
- **Jewel Sockets**: Special nodes where you slot Jewels — essentially customizable Notables with random/crafted stats. Huge source of build customization.

**Point Budget:** Characters earn passive points from leveling + campaign quests. You can fill maybe 15-20% of the tree, forcing heavy specialization.

**Preventing "Grab Everything":**
- Limited passive points (~100-120 total) vs ~1,500 nodes
- Pathing cost — you must spend points on travel nodes to reach distant clusters
- Keystones have drawbacks that shape builds

**Build Diversity Mechanics:**
- Keystones fundamentally alter how mechanics work (e.g., converting all damage to one type)
- Jewel Sockets allow infinite customization within the tree
- Web structure means multiple viable paths to reach the same Notable

**Weapon Specialization (NEW in PoE2):**
- Passive nodes can be color-coded: Gold (global/always active), Red (Weapon Set I only), Green (Weapon Set II only)
- Up to 24 Weapon Set passive points from campaign
- Dynamically swap passive bonuses when switching weapons mid-combat

**Ascendancy Classes:**
- Small sub-trees (8 points) with highly specialized, build-defining passives
- Unlocked via Ascension Trials
- Each class has access to specific Ascendancy options

**What Makes It Fun:**
- The sheer scale creates a feeling of discovery — "I found a path nobody else uses"
- Theorycrafting depth is essentially infinite
- Jewels allow personalizing within the framework
- Weapon Specialization rewards mastery of two playstyles

**Common Complaints:**
- Overwhelming for new players — analysis paralysis
- Many small nodes feel like "filler" (+5 str)
- Some paths are mathematically always better, creating "illusion of choice" in travel sections
- Respec cost can make experimentation punishing

**Applicable to Junkbot Arena:**
- Weapon Specialization concept maps perfectly to swappable robot loadouts
- Keystones with drawbacks = interesting for junkbot "firmware" choices
- Jewel Sockets = modular component slots in the tree

---

## 2. Last Epoch — Class Passive Tree + Skill Specialization

**Structure:** Multiple smaller linear/branching trees per class (not one massive web). Base class tree + 3 Mastery trees.

**How It Works:**
1. **Base Class Tree**: Available immediately. Spend 20 points here to unlock mastery choice.
2. **Mastery Choice**: Permanent. Pick 1 of 3 masteries (e.g., Mage -> Sorcerer, Spellblade, or Runemaster).
3. **Mastery Trees**: First half of ALL 3 mastery trees accessible. Second half ONLY for your chosen mastery.
4. **Total Points**: 113 passive points (98 from levels + 15 from quests).

**Node Types:**
- Small passives (stats, % bonuses)
- Notable-equivalent gated nodes (require X points spent in tree to unlock)
- Build-defining nodes deep in chosen mastery

**Skill Specialization System (Separate from Passives):**
- 100+ skills each have their own mini skill tree
- Specialize in up to 5 skills (slots unlock at levels 3, 9, 19, 34, 50)
- Each skill tree has ~20 levels of nodes that can fundamentally transform the skill
- Example: A fireball skill can be transformed into a channeled beam, a shotgun spread, or a delayed explosion

**Preventing "Grab Everything":**
- 113 points across 4 trees means heavy commitment
- Mastery lock-out (second half of unchosen trees inaccessible)
- Skill specialization limited to 5 skills

**Build Diversity:**
- Mastery choice creates 15 distinct class identities (5 base x 3 mastery)
- Cross-mastery dipping (first half of other trees) allows hybrid builds
- Skill trees within skills mean same skill plays completely differently between builds

**What Makes It Fun:**
- Skill specialization trees are the star — transforming a skill feels incredible
- Mastery choice is meaningful but not crushing (can still dip other trees)
- More approachable than PoE while still deep

**Common Complaints:**
- Some mastery trees feel underdeveloped compared to others
- 20-point gate for mastery feels like a tax
- Skill specialization leveling can be grindy for alts

**Applicable to Junkbot Arena:**
- Skill specialization trees are PERFECT for a roguelike — each ability could have a mini-tree you invest in during runs
- "Choose your mastery" maps to junkbot chassis/frame selection
- Cross-dipping first half of other trees = salvaging parts from other bot types

---

## 3. Diablo 4 — Skill Tree + Paragon Boards

**Structure:** Two-phase system. Linear skill tree (levels 1-59) + grid-based Paragon boards (level 60+).

### Skill Tree (Leveling Phase)
- 7 skill clusters arranged top-to-bottom (Basic -> Core -> Defensive -> Conjuration -> Ultimate -> Key Passive)
- ~71 total skill points (58 from leveling + 12 from Renown + bonus from Season)
- Each skill ranks up to 5; Ultimate skills rank 1 only; Passives rank to 3
- Target: fill ~30-40% of nodes at endgame — forces specialization
- Clusters gate behind point thresholds (must spend X points to unlock next tier)

### Paragon Board System (Endgame)
- Grid-based boards (not a tree — you navigate a 2D grid)
- Start with 1 board, attach up to 4 additional boards via Gate Nodes
- 8 class-specific boards to choose from (pick 4)

**Paragon Node Types:**
- **Normal Nodes**: +5 to an attribute. Filler/pathing.
- **Magic Nodes**: Damage, defense, or utility bonuses.
- **Rare Nodes**: Two attributes + a conditional bonus (must meet attribute threshold to activate bonus).
- **Legendary Nodes**: One per board. Build-defining powerful buff for a specific playstyle.
- **Gate Nodes**: Board connectors. +5 all attributes.
- **Glyph Sockets**: One per board. Insert a Glyph that buffs based on attributes of surrounding nodes within a radius.

**Glyph System:**
- Magic Glyphs: Radius 2, direct bonus
- Rare Glyphs: Radius 3 (expands to 4 at level 15), bonus + conditional bonus based on attributes within radius
- Glyphs level up in Nightmare Dungeons
- Creates spatial puzzle: arrange your pathing to maximize attributes within glyph radius

**Preventing "Grab Everything":**
- Limited skill points (30-40% fill)
- Paragon points limited; must choose which boards and paths
- Glyph radius mechanic means pathing matters, not just destinations

**Build Diversity:**
- Board selection defines endgame identity
- Glyph + board combination creates unique attribute landscapes
- Legendary nodes are build-defining pivots

**What Makes It Fun:**
- Two-phase system gives fresh feeling at endgame
- Paragon boards as "spatial puzzle" is novel
- Glyph radius mechanic adds strategy to pathing (not just A-to-B)

**Common Complaints:**
- Skill tree (leveling) phase is too simple — feels like "pick obvious skills"
- Paragon board navigation is tedious (lots of +5 nodes)
- Board attachment is confusing for new players
- Many nodes feel like filler

**Applicable to Junkbot Arena:**
- Glyph radius concept is VERY interesting for a junkbot game — imagine a "power core" that buffs adjacent installed modules
- Board selection = choosing which salvage/upgrade boards to install in your bot
- Spatial puzzle aspect maps well to physically arranging components

---

## 4. Grim Dawn — Devotion Constellation System + Dual-Class Mastery

**Structure:** Two parallel systems — Class Masteries (traditional skill trees) + Devotion (constellation map).

### Dual-Class Mastery
- Choose 1st mastery at level 2, 2nd at level 10
- 9 masteries available (Soldier, Demolitionist, Occultist, Nightblade, Arcanist, Shaman, Inquisitor, Necromancer, Oathkeeper)
- 36 possible dual-class combinations, each with a unique name (e.g., Soldier+Occultist = Witchblade)
- Each mastery: ~26 skills + synergistic modifiers
- Skill points spent in mastery bar (linear unlock) + individual skills

### Devotion Constellation System
- **Structure:** Constellation map (star map aesthetic)
- **Points:** Up to 50-55 Devotion points from restoring shrines in the world
- **How it works:**
  1. Spend points to light up stars in constellations
  2. Each star = passive bonus (attributes, resistances, damage)
  3. Completing a constellation grants an **Affinity** bonus in one or more colors (Primordial, Eldritch, Chaos, Order, Ascendant)
  4. Higher-tier constellations require minimum Affinity scores to unlock
  5. Completing certain constellations grants **Celestial Powers** — proc-based skills that bind to your active abilities

- **Celestial Powers:** Unique skills bound to your mastery skills. Damage procs bind to attack skills; defensive procs bind to buff skills. These create emergent combos.

**Preventing "Grab Everything":**
- 50-55 points vs hundreds of stars
- Affinity gating means you must commit to color themes
- Can't light all constellations — must choose paths through the star map
- Dual-class means skill points split between two trees

**Build Diversity:**
- 36 class combos x constellation pathing = enormous variety
- Celestial Powers binding creates unique interactions
- Affinity colors force thematic commitment (chaos build vs order build)

**What Makes It Fun:**
- Constellation map is visually beautiful and thematically satisfying
- Affinity system creates "unlocking chains" — feels like discovery
- Binding celestial powers to skills is a eureka moment
- Dual-class naming convention gives identity to combinations

**Common Complaints:**
- Devotion system is opaque for new players
- Some constellations are mathematically dominant
- Respec cost is punishing

**Applicable to Junkbot Arena:**
- Constellation/Affinity system maps PERFECTLY to junkbot tech trees — "unlock alien tech constellation, need 5 Alien Affinity first"
- Celestial Powers binding = "install this salvaged ability into one of your existing skill slots"
- Dual-class as "pick two junk sources" (military scrap + alien tech, or corporate parts + underground mods)
- Star map aesthetic fits sci-fi theme

---

# PART 2: ROGUELIKE / DUNGEON CRAWLER SYSTEMS

---

## 5. Hades / Hades 2 — Boon System

**Structure:** In-run build construction through offered choices (not a persistent tree). Meta-progression via Mirror of Night (persistent).

### Boon System (In-Run)
- Gods offer boons after clearing rooms (marked with god's symbol)
- Each god specializes in an effect type:
  - Zeus: Lightning/chain damage
  - Aphrodite: Charm/weak
  - Ares: Doom/damage-over-time
  - Athena: Deflect/defense
  - Poseidon: Knockback
  - Dionysus: Hangover/poison
  - Artemis: Crit
  - Demeter: Chill/slow
- Boons slot into specific aspects of your kit: Attack, Special, Cast, Dash, Call

**Boon Rarity:** Common -> Rare -> Epic -> Heroic -> Legendary -> Duo

**Duo Boons (Key Innovation):**
- 28 Duo Boons in Hades 1 (one per god pair)
- Require specific prerequisite boons from BOTH gods
- Create emergent build archetypes: "Zeus + Demeter = freeze triggers lightning bolts"
- Discoverable — players learn combinations over multiple runs
- Chance increased by: god keepsakes, Mirror upgrades, special items

**Legendary Boons:** Require multiple boons from a single god. Reward commitment to one god's theme.

### Mirror of Night (Meta-Progression)
- Persistent upgrades bought with Darkness currency
- Each ability has TWO versions (red/green) — binary choice per slot
- Unlocked with Chthonic Keys
- Examples: Extra dashes, death defiance, bonus rare chance
- Can respec freely after unlocking feature

**Preventing "Grab Everything" (In-Run):**
- Offered 3 choices per room — can't pick all
- Boon slots limited (one Attack boon, one Special boon, etc.)
- God pool per run is randomized (usually 4-5 gods appear)
- Duo prerequisites encourage focus over breadth

**Build Diversity:**
- God combinations create distinct archetypes per run
- Weapon + Aspect choice sets the foundation
- Keepsake selection (guaranteed first god encounter) enables planning
- Chaos boons add risk/reward layer (debuff now, buff later)
- Hades 2 adds Hexes (resource-spending abilities from boons)

**What Makes It Fun:**
- Every run feels different due to god pool randomization
- Duo Boons create "aha!" moments of discovery
- Building toward a specific combo creates tension and excitement
- Simple enough to grasp, deep enough to master
- Story integration — gods have personality, boons feel like gifts not just stats

**Common Complaints:**
- Some god combinations vastly outperform others
- RNG can deny build-enabling boons
- Mirror of Night choices become "solved" quickly

**Applicable to Junkbot Arena:**
- Boon system is the GOLD STANDARD for roguelike build variety
- Map gods to "sponsors" or "alien benefactors" — each offers thematic upgrades
- Duo Boons = "sponsor synergies" when you combine parts from two sponsors
- Mirror of Night = permanent workshop upgrades between runs
- Boon slots on Attack/Special/Dash = module slots on junkbot (weapon, shield, movement, utility)

---

## 6. Risk of Rain 2 — Item Stacking System

**Structure:** No tree at all. Build emerges purely from accumulated items. Character-agnostic item pool.

**How It Works:**
- Kill enemies -> open chests -> get random items
- Items stack infinitely (mostly) — each additional copy increases the effect
- No choices to make about items — you get what you find
- Build identity emerges from what stacks you accumulate

**Item Tiers:**
- Common (White): Basic effects, huge value when stacked
- Uncommon (Green): More specialized effects
- Legendary (Red): Powerful unique effects
- Boss (Yellow): From specific bosses
- Lunar (Blue): Powerful with drawbacks
- Equipment (Orange): Active-use items

**Stacking Mechanics:**
- Most items scale linearly (e.g., +15% crit per stack of Lens-Maker's Glasses)
- Some have effective caps (Lens-Maker caps at 10 stacks = 100% crit)
- Some have diminishing returns (hyperbolic stacking)
- Understanding stacking formulas IS the depth

**Synergy Examples:**
- Crit chance items + crit damage items = multiplicative scaling
- On-hit effects + attack speed = proc frequency explosion
- Drone items + drone army items = entire drone build archetype
- Kjaro's Band + Runald's Band = one proc triggers the other

**Preventing "Grab Everything":**
- Time pressure — spending too long shopping means difficulty scales
- Chest cost increases
- Lunar items have drawbacks
- Equipment slot is singular (opportunity cost)

**Build Diversity:**
- Emerges naturally from item RNG
- Character abilities bias toward certain item synergies
- 3D printing (item recycling) allows steering builds
- Command artifact (choose items) reveals intentional build depth

**What Makes It Fun:**
- Power fantasy — going from weak to absurdly overpowered
- Emergent combos feel discovered, not prescribed
- Stacking is inherently satisfying (watching numbers grow)
- Simple to understand, complex in interaction

**Common Complaints:**
- Heavily RNG-dependent — bad items = bad run
- Some items are clearly trash
- Lack of meaningful choice (mostly take everything offered)
- Late-game becomes visual chaos

**Applicable to Junkbot Arena:**
- Stacking = installing duplicate junk parts for compounding effects
- Item synergies = parts from same manufacturer work better together
- Time pressure = TV show timer / audience patience
- The "no choice" weakness can be addressed by offering curated selections

---

## 7. Slay the Spire — Card-Based Progression

**Structure:** Deck-building within runs. Branching map paths. No persistent tree.

**How It Works:**
- Start with basic deck of Strikes and Defends
- After combat: offered 3 cards, pick 1 (or skip!)
- Shops: buy cards, remove cards, buy relics/potions
- Campfires: heal or upgrade a card (binary choice)
- ~75 cards per character in the pool
- Deck size matters — smaller decks are more consistent

**Branching Path Design (Map):**
- Each floor is a branching map (inspired by FTL)
- Node types: Monster, Elite, Campfire, Shop, Chest, Event (random encounter)
- Path choice = risk/reward calculation (Elites are hard but give Relics)
- Can see upcoming paths 1-2 nodes ahead

**Card Removal:**
- Shops and events can remove cards
- Removing weak starter cards = KEY strategy (increases draw consistency)
- Creates a "sculpting" feel — you're not just adding, you're refining

**Relics (Passive Items):**
- ~150 relics per character
- Persist for entire run
- Some are build-defining (e.g., "Gain 1 energy at start of turn" or "Cards cost 1 less but deal 25% less damage")
- Create constraints that inform card choices

**Preventing "Grab Everything":**
- Deck dilution — too many cards = inconsistent draws
- Skip option is critical (knowing when NOT to take a card)
- Card removal costs gold (opportunity cost)
- Energy system limits cards played per turn

**Build Diversity (per character, ~75 cards):**
- Ironclad: Strength-scaling, exhaust, self-damage
- Silent: Poison, shiv spam, discard
- Defect: Orbs (lightning/frost/dark/plasma), focus scaling
- Watcher: Stance dancing (calm/wrath/divinity)

**What Makes It Fun:**
- Deck sculpting feels like crafting a machine
- Skip/remove decisions are as important as add decisions
- Relics create "build around" moments
- Each card offered is a genuine decision
- The ~75 card pool per character is the "sweet spot" — enough variety without being overwhelming

**Common Complaints:**
- Some card combinations are clearly dominant
- RNG can offer only bad options
- Difficulty spikes from bad relic RNG

**Applicable to Junkbot Arena:**
- Deck-building = module/part collection during runs
- Card removal = scrapping junk parts to streamline your bot
- Skip decision = "this part doesn't fit my build, leave it"
- Relics = chassis modifications that define build constraints
- Branching path design directly applicable to dungeon floor layout
- ~75 options per class is the proven sweet spot for pool size

---

## 8. Dead Cells — Mutation System + Permanent Progression

**Structure:** Two layers — Mutations (in-run, limited selection) + Permanent unlocks (meta-progression).

### Mutations (In-Run)
- Passive upgrades selected between areas (at the Guillain)
- Up to 3 mutations per run (1 per area cleared)
- Three categories tied to stats: Brutality (red), Tactics (purple), Survival (green)
- Mutation power scales with investment in matching stat color
- Must choose from offered set — can't get all

### Permanent Progression
- Cells (currency) unlock blueprints for new weapons, mutations, abilities
- ~90% of unlocks are new GEAR, not flat stat bonuses
- No permanent +health or +damage — game stays skill-based
- Higher difficulty (Boss Cells 1-5) adds harder enemies AND new loot/areas

### Scroll System (In-Run Stat Investment)
- Find scrolls throughout levels
- Each scroll: pick 1 of 2 stats to increase (Brutality/Tactics/Survival)
- Chosen stat increases HP + damage of matching-color weapons/mutations
- Creates build commitment within runs — "going all red" vs "splitting"

**Preventing "Grab Everything":**
- Only 3 mutation slots
- Scroll investment creates stat identity (splitting = weaker)
- Weapon colors encourage matching

**Build Diversity:**
- Stat color system creates three distinct playstyles per run
- Weapon/mutation combinations within each color
- Some mutations scale with specific stats, rewarding commitment

**What Makes It Fun:**
- Lean system — 3 mutations is all you get, every choice matters
- Permanent unlocks expand the possibility space, not power
- Difficulty scaling adds new content, not just bigger numbers
- Fast-paced; decisions are quick but consequential

**Common Complaints:**
- Some mutations are clearly superior
- Meta becomes "solved" at high difficulty
- Permanent unlocks can dilute the loot pool (more items = harder to find favorites)

**Applicable to Junkbot Arena:**
- 3-mutation limit = "firmware slots" on junkbot (pick 3 passive subroutines)
- Stat color system = junkbot module types (Offense/Defense/Utility)
- Permanent unlocks expanding possibility, not power = CRUCIAL for roguelike feel
- Scroll system = finding upgrade chips that commit you to a playstyle

---

# PART 3: TOWER DEFENSE SYSTEMS

---

## 9. Bloons TD 6 — Tower Upgrade Paths + Hero Leveling

**Structure:** 3 upgrade paths per tower, mutually exclusive at high tiers. Heroes level passively.

### Tower Upgrade System
- Each tower has 3 upgrade paths (Top, Middle, Bottom)
- Each path has 5 tiers
- **Crosspath Rule:** Can max ONE path (up to tier 5). Can put tier 2 in ONE other path. Third path stays at 0.
- Notation: "5-2-0" means Path 1 maxed, Path 2 at tier 2, Path 3 untouched

**Example (Dart Monkey):**
- Path 1: Range/damage focus (Ultra-Juggernaut at T5)
- Path 2: Multi-shot/speed focus (Plasma Monkey Fan Club at T5)
- Path 3: Special utility (Crossbow Master at T5)
- Crosspath "0-2-5" = Crossbow Master with multi-shot enhancement

### Hero System
- Heroes level up passively during the game
- Each hero has unique abilities that unlock at specific levels
- One hero per game
- Heroes define strategy alongside tower choices

**Preventing "Grab Everything":**
- Crosspath rule is the KEY constraint — forces meaningful choice per tower
- Gold economy limits total tower count
- One hero limit

**Build Diversity:**
- Each tower effectively has 6 viable configurations (3 main paths x 2 crosspath options)
- Same tower type plays completely differently based on path
- Hero synergy with specific tower strategies

**What Makes It Fun:**
- Crosspath system is elegant — simple rule, deep consequences
- Same tower can fill completely different roles
- Visual changes with upgrades are satisfying
- Easy to understand, hard to optimize

**Common Complaints:**
- Some crosspaths are clearly dominant (one "correct" answer)
- T5 upgrades can be so expensive they're impractical
- Power gap between good and bad crosspaths

**Applicable to Junkbot Arena:**
- Crosspath rule is BRILLIANT for junkbot modules — each module has 3 upgrade tracks, can only max 1 and minor-invest in 1 other
- Simple, elegant, easy to communicate
- "Same part, different specialization" fits the junk/salvage theme
- Could apply to individual bot systems (weapons, armor, movement, sensors)

---

## 10. Kingdom Rush — Tower Specialization

**Structure:** (Classic games 1-3) Linear upgrade tiers with binary branching at tier 4.

**How It Works:**
- 4 base tower types (Archers, Barracks, Mages, Artillery)
- Each tower upgrades linearly through tiers 1-3 (gold cost, anyone can upgrade)
- At tier 4: BINARY CHOICE between two specializations
- Choice is permanent for that tower placement
- Example: Mage Tower -> Arcane Wizard OR Sorcerer Mage

**Global Upgrades:**
- Stars earned from completing levels
- Spent on upgrade trees (one per tower type + spells)
- 5-6 upgrades per tree
- Permanent, apply to all towers of that type

**Evolution in Later Games:**
- Kingdom Rush: Vengeance dropped branching — instead 18 tower types, pick 5 per level
- Kingdom Rush 5: Alliance — more tower types, extra tier, ability suite per tower
- Trend: Moved from "branch within tower" to "choose which towers to bring"

**What Makes It Fun:**
- Binary tier-4 choice is clean and impactful
- Each specialization plays completely differently
- Easy to understand — even casual players get it
- Stars-for-upgrades gives meta-progression

**Common Complaints:**
- Often one specialization is clearly better
- Limited depth compared to more complex systems
- Newer games abandoning the system suggests limitations

**Applicable to Junkbot Arena:**
- Binary specialization at a certain upgrade threshold = "choose your junkbot's role evolution"
- Simple enough for run-based decisions (not overwhelming mid-dungeon)
- Could apply at sector transitions — "at sector 3, choose specialization A or B for each equipped module"

---

# PART 4: LOOTER SHOOTER SYSTEMS

---

## 11. Destiny 2 — Subclass 3.0 (Aspects + Fragments)

**Structure:** Modular slot-based system. Not a tree — more like a loadout with constraints.

**How It Works:**
- Choose a subclass element (Void, Solar, Arc, Strand, Stasis)
- Within element, choose:
  - **Super** (ultimate ability) — pick 1 of 2-3
  - **Grenade** — pick 1 of 3-4
  - **Melee** — pick 1 of 2-3
  - **Class Ability** — pick 1 of 2
  - **Aspects** — pick 2 of 3-4 (CLASS-SPECIFIC)
  - **Fragments** — pick 3-5 (UNIVERSAL across classes)

**Aspects (Class-Specific):**
- Define your build identity
- Each Aspect has a different number of Fragment slots (1-3)
- Choosing high-slot-count Aspects = more Fragments but possibly weaker Aspect effect
- Choosing powerful low-slot Aspects = fewer Fragment options

**Fragments (Universal):**
- Shared across all classes within an element
- Smaller bonuses, but can affect stats (+10 or -10 to various stats)
- Many available — pick based on build synergy

**The Constraint:**
- Aspect slot count determines Fragment budget
- Example: 2 Aspects with 2 slots each = 4 Fragments total
- This creates a natural balancing mechanism

**Preventing "Grab Everything":**
- Limited Aspect slots (2)
- Fragment slots determined by Aspect choices (budget system)
- Single element per loadout

**Build Diversity:**
- Class identity through Aspects (Titan plays Solar differently than Warlock)
- Fragment customization adds personal flavor
- Exotic armor pieces often enhance specific Aspects
- Stat tradeoffs on Fragments create optimization puzzles

**What Makes It Fun:**
- Modular system is easy to respec and experiment
- Fragment budget mechanic creates interesting tradeoffs
- Class identity maintained through Aspects while Fragments allow cross-class theorycrafting

**Common Complaints:**
- Some Aspects are clearly dominant per class
- Fragment stat penalties feel bad
- System replaced deeper old subclass trees — some feel it's dumbed down

**Applicable to Junkbot Arena:**
- Aspect + Fragment budget system is ELEGANT — junkbot "core modules" (aspects) determine how many "minor modules" (fragments) you can install
- Powerful core = fewer minor slots. Flexible core = more minor slots. Great tradeoff.
- Universal fragments across bot frames = shared salvage pool
- Stat tradeoffs on fragments = weight/power budget on junkbot

---

## 12. Warframe — Mod System + Focus Trees

**Structure:** Two systems — Mods (primary, equipment-based) + Focus Trees (secondary, operator-based).

### Mod System
- Warframes and weapons have mod slots
- Mods have a CAPACITY cost (drain) — total drain limited by equipment level
- Polarity system: matching mod polarity to slot polarity halves the drain cost
- Mods rank up (0-10), each rank increases power AND drain cost
- No tree — it's a constrained loadout/deckbuilding system
- Key tension: powerful mods cost more drain, forcing choices about what to include

### Focus Trees (5 Schools)
- Madurai (offense), Naramon (melee), Zenurik (energy), Unairu (defense), Vazarin (support)
- Each school: 10 abilities ("Ways") arranged in a dependency tree
- Only ONE school active at a time
- Inner ways must be unlocked before outer ways
- Focus points earned through gameplay (lenses on equipment convert XP)
- "Waybound" nodes from one school can be used while another school is active (costly to unlock)

**Preventing "Grab Everything":**
- Mod capacity = hard budget on equipment power
- One active Focus school at a time
- Waybound cross-school unlocks are extremely expensive
- Mod polarity matching adds optimization layer

**What Makes It Fun:**
- Mod system is incredibly flexible — same Warframe plays totally differently based on mods
- Capacity budget forces genuine tradeoffs
- Focus schools add a second layer of identity
- Polarity matching rewards investment in specific loadouts

**Common Complaints:**
- Mod system has "mandatory" mods (damage/multishot always needed)
- Focus grind is extremely long
- New player experience is confusing

**Applicable to Junkbot Arena:**
- Capacity/drain budget is PERFECT for junkbot power management — each module has a power cost, total limited by battery/reactor
- Polarity matching = compatible junk parts (military parts in military slots = half power cost)
- Mod ranking = upgrading salvaged parts (more powerful but more power-hungry)
- This is probably the single most applicable system for a junkbot game

---

# PART 5: OTHER NOTABLE SYSTEMS

---

## 13. Borderlands 3 — 3 Skill Trees + Action Skill Augments

**Structure:** 3 parallel branching trees per character (4 with DLC). Each tree has an Action Skill at top.

**How It Works:**
- 3 trees, each ~25 nodes deep across 6 tiers
- Tiers gate behind "points spent in tree" thresholds
- Points can be freely distributed across all 3 trees
- Max level provides enough points to max ~1.5 trees
- Action Skills freely swappable (not tied to point investment)

**Action Skill Augments:**
- Side nodes on each tree that modify Action Skills
- DO NOT cost skill points — free once tier is reached
- Can equip multiple augments simultaneously
- Augments from different trees can combo

**Preventing "Grab Everything":**
- ~48 skill points at max level vs ~75+ total nodes
- Tier gating requires commitment to access deep nodes
- Capstone (bottom) skills require heavy investment in one tree

**Build Diversity:**
- Splitting across trees creates hybrid builds
- Action skill augments add free customization layer
- Each character's 3 trees have distinct themes (e.g., Moze: mech damage, splash damage, shields)
- Zane uniquely equips 2 action skills (sacrificing grenades)

**What Makes It Fun:**
- Action Skill Augments being free is great — rewards exploration without cost
- Hybrid builds are viable and fun
- Respec is cheap and easy — experimentation encouraged
- Each tree has a distinct fantasy

**Common Complaints:**
- Some capstones are underpowered relative to investment
- Hybrid builds sometimes outperform focused builds (undermines "specialization" feeling)
- Some skills are clearly trap nodes

**Applicable to Junkbot Arena:**
- Free augments for reaching thresholds = "bonus subroutines" unlocked by investing in a system category
- Multi-tree splitting = allocating junkbot upgrade resources across weapon/defense/utility systems
- Cheap respec = fits roguelike experimentation

---

## 14. Final Fantasy X — Sphere Grid

**Structure:** Massive shared grid/web. All 7 characters navigate the same grid from different starting positions.

**How It Works:**
- One enormous interconnected grid of ~800+ nodes
- Each character starts at a different position on the grid
- Movement: Spend Sphere Levels (earned from battles) to move along paths
- Activation: Spend specific Spheres (Power, Mana, Speed, etc.) to activate nodes you've moved to
- Node effects: +HP, +Strength, learn ability, etc.
- Lock Nodes: Require special Key Spheres to open, blocking paths to other characters' sections

**Standard vs Expert Grid:**
- Standard: Characters start far apart with clear intended paths
- Expert: Characters start closer together with more branching — allows faster cross-pathing

**Grid Navigation:**
- Characters naturally follow their own section (Tidus = speed/agility, Lulu = magic, Auron = strength)
- Later in the game, characters can path into other characters' sections
- Endgame: All characters can theoretically max the entire grid
- Teleport Spheres allow jumping to another character's position

**Preventing "Grab Everything" (during story):**
- Sphere Levels are limited by XP earned
- Key Spheres are rare (gating cross-character paths)
- Specific activation spheres are limited resources
- Only endgame grind allows maxing everything

**Build Diversity:**
- Early/mid game: each character has a clear identity
- Late game: character identity dissolves as everyone can learn everything
- Expert Grid allows earlier deviation and hybrid builds
- Lock placement creates meaningful "do I open this path?" decisions

**What Makes It Fun:**
- Physical navigation of the grid is tactile and satisfying
- Seeing unexplored territories is exciting
- Key Spheres feel like finding a key to a new area
- Expert Grid allows creative early-game builds

**Common Complaints:**
- Standard Grid is too linear — illusion of choice (just follow your path)
- Endgame homogenizes all characters
- Navigation is tedious (lots of clicking through nodes)
- Activation sphere management is confusing

**Applicable to Junkbot Arena:**
- Shared grid concept = all junkbot frames share one massive upgrade board but start in different positions
- Lock nodes = rare key items that open cross-frame tech
- Physical navigation = exploring/unlocking different junkyard sections
- The "homogenization" problem must be addressed if using this concept — maybe make grid sections permanently exclusive per frame type

---

## 15. Vampire Survivors — Passive Item Evolution

**Structure:** No tree. Build emerges from weapon + passive item combinations. Evolution system rewards specific pairings.

**How It Works:**
- Level up -> choose 1 of 3-4 offered items (weapons or passives)
- Weapons cap at level 8
- Passive items provide stat bonuses (Area, Speed, Might, etc.)
- **Evolution:** Max-level weapon + correct passive item -> open boss chest after 10 min -> weapon evolves into powerful form
- **Union:** Two specific weapons combine into one (freeing a slot)
- **Gifts:** Meeting conditions grants additional items without replacing anything

**The Discovery Loop:**
- Players don't know evolution recipes initially
- Discovering what pairs with what is a core gameplay loop
- Community sharing of recipes drives engagement

**Preventing "Grab Everything":**
- 6 weapon slots + 6 passive slots = hard cap
- Offered items are random — can't always get what you want
- Slot management: Do you take another weapon or the passive needed to evolve an existing one?

**Build Diversity:**
- Weapon selection defines build identity
- Passive items serve dual purpose: stat bonus + evolution catalyst
- Character choice biases toward certain weapons
- Arcana system (late-game) adds pre-run modifier selection

**What Makes It Fun:**
- Evolution discovery is addictive
- Slot management creates genuine tension
- Power growth is extreme and satisfying
- Simple inputs, complex outputs
- "Just one more run" factor

**Common Complaints:**
- Once recipes are known, builds become formulaic
- Late game devolves into screen-filling chaos
- Limited strategic depth after learning the system
- Some evolutions are clearly superior

**Applicable to Junkbot Arena:**
- Evolution/combination system = combining junk parts creates upgraded versions
- Recipe discovery = players experiment with part combinations
- Slot limits = junkbot has limited module bays
- Union (two items -> one, freeing a slot) = welding two parts together, very thematic for junkbots

---

# PART 6: GAME DESIGN THEORY — SKILL TREES

---

## What Makes a GOOD Skill Tree

### 1. Meaningful Scarcity
- Players should fill 30-50% of the tree maximum
- If you can get everything, the tree is pointless
- Scarcity creates identity: "I'm a fire build" vs "I'm a lightning build"
- Every point spent should feel like a decision, not a formality

### 2. No Dominant Strategy
- If one path is clearly best, the tree has failed
- All paths should be viable, even if for different playstyles
- Balance doesn't mean equal — asymmetric power is fine if tradeoffs exist
- Regular rebalancing is necessary

### 3. Immediate + Delayed Gratification
- Mix of small immediate bonuses with exciting milestone nodes
- Travel nodes should still feel like progress
- "One more node until the big one" is a powerful motivator

### 4. Visual Clarity
- Players should understand the tree structure at a glance
- Group related nodes visually
- Use size/color/shape to indicate node importance
- Path connections should be obvious

### 5. Reversibility (or Commitment)
- Either make respec easy (encourages experimentation) or make it meaningful (adds weight)
- Don't make respec punishing AND choices unclear — that's just frustrating
- Best practice: cheap respec early, more costly later as builds solidify

### 6. Correct Pool Size
- ~75 options per character class is the proven sweet spot (Slay the Spire)
- Too many = analysis paralysis and impossible to balance
- Too few = lack of variety and replayability
- For a skill tree: nodes should be maybe 3-5x your point budget

## What Makes a BAD Skill Tree

### 1. Illusion of Choice
- "+5% damage" vs "+5% damage but slightly different" is not a choice
- If every build takes the same first 20 nodes, those nodes shouldn't be in the tree
- If one path is 10x better than another, the "choice" is fake

### 2. Trap Nodes
- Nodes that SEEM good but are mathematically terrible
- These punish new players and make experienced players ignore portions of the tree
- Design principle: every node should be correct for SOME build
- If a node is never taken, it should be redesigned or removed

### 3. Mandatory Paths
- If every build must path through the same nodes, those aren't choices
- Essential mechanics should be part of the base kit, not locked in trees
- The tree should be for CUSTOMIZATION, not gating core functionality

### 4. Filler Bloat
- Hundreds of "+5 to stat" nodes exist only to create the illusion of scale
- Players see through this — it feels like a chore, not progression
- Better to have 50 meaningful nodes than 500 forgettable ones

### 5. One-Time Exploration
- If the tree is solved after one playthrough, it lacks replay value
- Synergies between tree choices and external systems (items, enemies) extend life
- Context-dependent value (this node is great for THIS dungeon) adds replayability

### 6. Locking Essential Mechanics Deep in Trees
- Playtesters consistently reject having to "earn" basic functionality
- Players should start with a complete moveset
- Trees should ENHANCE and SPECIALIZE, not provide basics

---

# PART 7: SYNTHESIS — RECOMMENDATIONS FOR JUNKBOT ARENA

---

## Key Takeaways for a Roguelike Dungeon Crawler About Junkbot Robots

### Recommended Hybrid Approach

Based on all 15 systems analyzed, Junkbot Arena should combine elements from multiple sources:

#### 1. **Persistent Progression: Warframe Mod Capacity System**
- Each junkbot frame has a POWER BUDGET (battery/reactor capacity)
- Modules (mods) have a power drain cost
- More powerful modules cost more power — forces tradeoffs
- Polarity matching: parts from matching manufacturers cost half power
- Between runs: upgrade your reactor (expand budget) and unlock new module blueprints

#### 2. **In-Run Build Construction: Hades Boon System + Slay the Spire Sculpting**
- Sponsors/alien benefactors offer themed upgrade packages (like gods offering boons)
- Each sponsor specializes: damage types, defense, mobility, utility
- Duo-sponsor synergies when you combine parts from two sponsors
- Can REFUSE offered parts (skip) and can SCRAP installed parts (card removal)
- Limited module slots force build focus

#### 3. **Module Specialization: Bloons TD 6 Crosspath System**
- Each module has 3 upgrade paths
- Can max 1 path + minor investment in 1 other path + third locked
- Same weapon module plays completely differently based on path choice
- Simple, elegant, easy to communicate mid-run

#### 4. **Frame Progression: Last Epoch Mastery System**
- Choose junkbot frame (base class) at run start
- At sector 2 or 3: choose frame specialization (mastery) — binary or ternary choice
- Specialization unlocks deeper upgrade options
- Can still use basic upgrades from unchosen specializations

#### 5. **Discovery/Combination: Vampire Survivors Evolution**
- Certain part combinations create evolved/fused parts
- Players discover recipes through experimentation
- Fusing two parts into one frees a module slot — strategic choice
- Recipe book fills out over multiple runs (meta-progression)

#### 6. **Spatial Puzzle Element: Diablo 4 Glyph Radius**
- Core modules (power cores, processors) buff adjacent module slots
- Physical arrangement of modules matters — not just what you have, but WHERE you install it
- Creates optimization puzzle: "I need this module next to my processor for the bonus"

### Design Principles for Junkbot Arena

1. **Start Complete, Specialize Through Runs**: Junkbots should have a full functional kit from room 1. The tree/modules ENHANCE and SPECIALIZE, never gate basic mechanics.

2. **30-40% Fill Rate**: If there are 100 possible upgrades, a full run should acquire ~30-40 of them. This ensures build diversity.

3. **~75 Options Per Frame**: Following the Slay the Spire lesson, each junkbot frame's upgrade pool should be around 75 options (mix of modules, firmware, and augments).

4. **Meaningful Scarcity + Meaningful Skipping**: The decision to NOT take something should be as interesting as the decision to take it. (Slay the Spire skip, Dead Cells 3-slot limit, Bloons crosspath lockout)

5. **Two Layers of Progression**: In-run (modules, sponsor boons, specialization choices) + Between-run (workshop upgrades, unlocking new modules for the pool, expanding power budget). Between-run should expand VARIETY, not POWER. (Dead Cells principle)

6. **Synergy as Discovery**: Duo-sponsor bonuses, part evolution recipes, and manufacturer set bonuses should be discoverable through play, not listed in a menu. The "aha!" moment is the fun. (Hades Duo Boons, Vampire Survivors evolution)

7. **Visual Clarity**: Given the sci-fi/junkbot theme, the "tree" could literally be the robot's body schematic — head slot, torso slot, arms, legs, internal systems. Upgrades are literally visible on the bot. Physical metaphor > abstract tree.

---

## Sources

### Path of Exile 2
- [PoE 2 Passive Tree and Weapon Specialisation - Maxroll](https://maxroll.gg/poe2/resources/poe-2-passive-tree-and-dual-specialization)
- [Passive Skill Tree - Path of Exile 2 Wiki](https://www.poe2wiki.net/wiki/Passive_skill_tree)
- [Path of Exile 2 Passive Skill Tree Guide - VULKK](https://vulkk.com/2025/02/08/path-of-exile-2-passive-tree-guide/)
- [Path of Exile 2 passive skill tree explained - PCGamesN](https://www.pcgamesn.com/path-of-exile-2/passive-skill-tree)
- [PoE 2 Passive Skill Tree Keystones - AOEAH](https://www.aoeah.com/news/3679--poe-2-passive-skill-tree-keystones-locations--effects--path-of-exile-2-keystone-tier-list)

### Last Epoch
- [Passives and Skills Overview - Maxroll](https://maxroll.gg/last-epoch/resources/passives-and-skills)
- [Passives - Official Last Epoch Wiki](https://lastepoch.fandom.com/wiki/Passives)
- [Passive Points - Icy Veins](https://www.icy-veins.com/last-epoch/passive-points)
- [How Does Skill Specialization Work - Escapist](https://www.escapistmagazine.com/how-does-skill-specialization-work-in-last-epoch/)

### Diablo 4
- [Paragon Boards & Glyphs - Maxroll](https://maxroll.gg/d4/resources/paragon-boards)
- [Skill Tree Overview - Maxroll](https://maxroll.gg/d4/getting-started/skill-trees)
- [Paragon Board Overview - Wowhead](https://www.wowhead.com/diablo-4/guide/gameplay/paragon-boards-nodes-glyphs)
- [Diablo 4 Paragon board and Glyphs explained - PCGamesN](https://www.pcgamesn.com/diablo-4/paragon-board-glyphs)

### Grim Dawn
- [Devotion Guide - Grim Dawn Official](https://www.grimdawn.com/guide/character/devotion/)
- [Devotion - Official Wiki](https://grimdawn.fandom.com/wiki/Devotion)
- [Masteries - Official Wiki](https://grimdawn.fandom.com/wiki/Masteries)
- [Dual Class System - Crate Forums](https://forums.crateentertainment.com/t/grim-dawns-dual-class-system/94002)

### Hades / Hades 2
- [Duo Boons - Hades Wiki](https://hades.fandom.com/wiki/Duo_Boons)
- [Mirror of Night - Hades Wiki](https://hades.fandom.com/wiki/Mirror_of_Night)
- [Boons/Hades II - Hades Wiki](https://hades.fandom.com/wiki/Boons/Hades_II)
- [Hades 2 Boon Guide - TechTimes](https://www.techtimes.com/articles/313887/20260127/hades-2-boon-guide-broken-god-combinations-run-optimization-tips-faster-clears.htm)

### Risk of Rain 2
- [Item Stacking - Risk of Rain 2 Wiki](https://riskofrain2.fandom.com/wiki/Item_Stacking)
- [Item Synergies Guide - Steam](https://steamcommunity.com/sharedfiles/filedetails/?id=1940344201)

### Slay the Spire
- [Slay the Spire - Wikipedia](https://en.wikipedia.org/wiki/Slay_the_Spire)
- [Slay the Spire Tips - Eneba](https://www.eneba.com/hub/games-guides/slay-the-spire-tips/)
- [Slay the Spire 2 Beginner Guide - Mobalytics](https://mobalytics.gg/slay-the-spire-2/guides/beginner-guide)

### Dead Cells
- [Mutations - Official Dead Cells Wiki](https://deadcells.wiki.gg/wiki/Mutations)
- [Dead Cells - Wikipedia](https://en.wikipedia.org/wiki/Dead_Cells)

### Bloons TD 6
- [Crosspathing - Bloons Wiki](https://bloons.fandom.com/wiki/Crosspathing)
- [Upgrades - Bloons Wiki](https://bloons.fandom.com/wiki/Upgrades)

### Kingdom Rush
- [Upgrades - Kingdom Rush Wiki](https://kingdomrushtd.fandom.com/wiki/Upgrades)
- [Towers - Kingdom Rush Wiki](https://kingdomrushtd.fandom.com/wiki/Category:Towers)

### Destiny 2
- [Void 3.0 Aspects and Fragments - TheGamer](https://www.thegamer.com/destiny-2-void-aspects-fragments-guide/)
- [Understanding Subclasses - Shattered Vault](https://shatteredvault.com/kb/subclasses/)

### Warframe
- [Focus - WARFRAME Wiki](https://wiki.warframe.com/w/Focus)
- [Focus 3.0 Complete Guide - TheGamer](https://www.thegamer.com/warframe-focus-schools-waybounds-explained/)

### Borderlands 3
- [Skill Tree Deep Dive - MentalMars](https://mentalmars.com/guides/borderlands-3-skill-trees-for-all-characters/)
- [BL3 Skill Trees - PCGamer](https://www.pcgamer.com/borderlands-3-skill-trees/)

### Final Fantasy X
- [Sphere Grid - Final Fantasy Wiki](https://finalfantasy.fandom.com/wiki/Sphere_Grid)
- [Sphere Grid Guide - EIP Gaming](https://eip.gg/ffx-x2/guides/sphere-grid/)

### Vampire Survivors
- [Evolution - Vampire Survivors Wiki](https://vampire-survivors.fandom.com/wiki/Evolution)
- [Passive Items - Vampire Survivors Wiki](https://vampire.survivors.wiki/w/Passive_items)

### Game Design Theory
- [Keys to Meaningful Skill Trees - GDKeys](https://gdkeys.com/keys-to-meaningful-skill-trees/)
- [Game Design Skill Trees - GameDesigning.org](https://gamedesigning.org/learn/skill-trees/)
- [Skill Tree Design Guide - Adrian Crook](https://adriancrook.com/skill-tree-design-ultimate-guide-for-freemium-games/)
- [A Guide to Designing Skill Trees - Santeri Orava (Thesis)](https://www.theseus.fi/bitstream/handle/10024/192256/Orava_Santeri.pdf?sequence=1&isAllowed=y)
- [Mastering Skill Trees in Game Design - Number Analytics](https://www.numberanalytics.com/blog/ultimate-guide-to-skill-trees-in-game-design)
