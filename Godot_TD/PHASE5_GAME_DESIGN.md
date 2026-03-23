# Phase 5 — Make It A Real Game

*Systems are built. Now make it feel like something you can't stop playing.*
*Phase 0-4 were infrastructure. Phase 5 is the game.*

**Rule 1: Every system gets an editor.** If you build a mechanic that has tunable values, place-able objects, or authored content — build the F12 editor module for it at the same time. No system ships without a way to tune it live in-game. Code + editor = one task, not two.

**Rule 2: Every system gets tested on every planet.** Don't build and test on Grid Prime only. Every new feature, hazard, enemy behavior, tower mechanic, and VFX must be verified on all active planets (currently Grid Prime + Scrapyard, eventually Planet 3). Planet themes change materials, colors, enemy AI style, and map geometry — something that works on Tron can break or look wrong on Scrapyard. If a system has per-planet data (wave JSON, milestones, maps, entry schedules), create that data for ALL planets when you build the system, not as a follow-up.

**Rule 3: Check file ownership before editing.** Multiple people and Claude instances work on this codebase simultaneously. Before modifying any file, check WORK_ASSIGNMENT.md for who owns it. If a file isn't in your ownership list, coordinate before touching it. For shared files (Constants.cs, GameEvents.cs, Enums.cs, GameManager.cs) — add to the bottom only, comment your section with `// Phase5-SectionName:`, and pull before pushing. If you see someone else's uncommitted changes when you pull, don't overwrite them — merge or ask.

---

## Progress Overview

### Map Design
- [x] Map hazards — destructible walls, environmental damage, pits, DataStreams, elevated platforms — DONE (HazardManager, 4 new VineCellTypes, 3 HazardTypes, elevated range bonus, pathfinder updated)
- [x] Terrain mutation at milestones — walls collapse, pits open, map expands — DONE (TerrainMutationManager, JSON-driven, VFX + screen shake)
- [x] Map expansion zones — arena grows from small to full over a run — DONE (ExpansionZone system, seals/reveals rectangular regions at wave milestones)
- [x] Resource nodes on map — reward expansion and risk-taking — DONE (ResourceNode cell type, captured by adjacent tower, +5 income/tick)

### Planet Content
- [x] **Planet 1 (Grid Prime)** — 5 maps: gateway (tutorial), circuit_lanes (parallel DataStreams), antenna_field (wide open platforms), grid_maze (dense corridors), data_nexus (pit ring + DataStream highways)
- [x] **Planet 2 (Scrapyard)** — 4 maps: salvage_yard (wide open debris), rust_pit (central pit + bridges), foundry (acid corridors), smelter (asymmetric + expansion). Still needs P2.json wave data.
- [ ] **Planet 3 (New)** — theme, military AI, maps, wave data, visual identity

### Combat Feel
- [ ] Enemy behaviors that create real decisions
- [ ] Towers that feel powerful and distinct
- [ ] Commander encounters that change the wave
- [ ] Boss design that isn't just "big HP enemy"
- [ ] Player abilities (Q/E/R) that feel impactful

### Game Feel
- [ ] Audio — music, distinct SFX, impact sounds
- [ ] Visual juice — death effects, hit flash, screen shake, particles
- [ ] Extraction tension — "one more wave" feeling
- [ ] First 60 seconds — immediately engaging
- [ ] Difficulty curve — challenging but fair across 20 waves

### Content Depth
- [ ] Material system (Chaos/Power/Environment) with real effects
- [ ] Perk variety — choices that change playstyle
- [ ] Enough enemy variety that wave 15 feels different from wave 5
- [ ] Map hazards and environmental interactions

### Editor Tools (built alongside every system)
- [ ] F12 Map Designer — paint terrain, place props, preview entry schedules
- [ ] F12 Hazard Tuner — damage, tick rate, radius per hazard type
- [ ] F12 Terrain Mutation Timeline — define cell changes per milestone
- [ ] F12 Enemy Designer — create/edit enemies, test-spawn on map
- [ ] F12 Commander Editor — spawn conditions, behaviors, buff values
- [ ] F12 Boss Editor — mechanics, phases, abilities
- [ ] F12 Tower Slot Editor — mod component stats, synergy preview
- [ ] F12 Tower Test Mode — spawn test wave, watch towers fight
- [ ] F12 Material Effect Tuner — chaos/power/environment values
- [ ] F12 Material Shop Editor — upgrade options per milestone
- [ ] F12 Sound Event Mapper — assign Sonniss WAVs to game events
- [ ] F12 VFX Preview — tune particles, compare configs
- [ ] F12 Perk Editor — create/edit perks, preview selection screen
- [ ] F12 Balance Dashboard — all tunable values in one view

---

## 5.1 — Map Design System (The Arena Is The Game)

Maps aren't just grids to place towers on. The map IS the difficulty. Environmental hazards, dynamic terrain, and layout progression turn each run into a problem-solving session where the map fights you as much as the enemies do.

### 5.1A — Map Mechanics & Hazards

| # | Task | Size | Notes |
|---|------|------|-------|
| 5.1.1 | **Destructible walls** — walls that enemies can break through if undefended. Creates breach points the player must choose to reinforce or abandon. Different HP per wall segment. | M | Ties into shield wall system Adam built |
| 5.1.2 | **Environmental hazards** — terrain cells that damage anything standing on them. Lava/acid pools, electric floors, gas vents. Enemies path through them (taking damage) or around them. Towers placed near hazards take chip damage. Player must decide: is the hazard helping or hurting? | L | New terrain type in VineGrid |
| 5.1.3 | **Pit/hole terrain** — impassable holes in the ground. Enemies path around them. Towers can't be placed on them. Creates natural chokepoints, but also limits your build space. Some pits could open mid-run at milestones. | M | New terrain type, pathfinder respects it |
| 5.1.4 | **DataStream corridors** — enemies move 50% faster through these. Already defined in code but never placed. Strategic risk: shorter path but enemies arrive faster. Place towers along DataStreams for a gauntlet or avoid them entirely. | S | TerrainType.DataStream exists, just unplaced |
| 5.1.5 | **Elevated platforms** — raised terrain enemies can't walk on but towers get +range when placed there. Limited build space, premium positioning. | M | TerrainType.Elevated exists, needs range bonus |
| 5.1.6 | **Terrain mutation at milestones** — walls collapse, new corridors open, pits crack open, hazards activate. The map physically changes mid-run at wave milestones. Your carefully built defenses suddenly have new holes. | L | VineGrid needs mid-wave terrain change support |
| 5.1.7 | **Repath on terrain change** — when walls break or new paths open, enemies reroute in real time. The player sees their carefully built maze get bypassed. Forces reactive building. | M | VinePathfinder recalc on terrain change events |
| 5.1.8 | **Resource nodes on map** — specific cells that give bonus resources when captured (tower placed adjacent). Reward expansion and risk-taking. Place them away from safe positions. | M | Incentivizes spreading out, not turtling |
| 5.1.9 | **Fog of war / vision radius** — can only see terrain near your towers and BIT. Enemies emerge from fog. Forces scouting with BIT and broader tower placement. | L | Optional — may be too complex for v1 |

### 5.1B — Map Layout Design (Per Planet)

| # | Task | Size | Notes |
|---|------|------|-------|
| 5.1.10 | **Grid Prime maps (3-5)** — circuit-board aesthetic. Clean geometry, DataStream corridors, elevated platforms. Predictable layouts that teach the system. Each map should have 1-2 chokepoints and 1 hazard zone. | M | Tutorial planet — designed to teach |
| 5.1.11 | **Scrapyard maps (3-5)** — industrial chaos. Wide open with debris clusters creating organic walls. Acid pools, unstable ground. Less predictable geometry than Grid Prime. Wider maps for multi-direction pressure. | M | Harder planet — less predictable |
| 5.1.12 | **Planet 3 maps (3-5)** — military compounds. Long sightlines, bunker positions, overlapping fire lanes. Designed for smart enemies that scout and flank. Most hazards, most terrain mutation. | M | Hardest planet — map actively fights you |
| 5.1.13 | **Map expansion zones** — areas outside the initial play area that unlock at wave milestones. The map literally grows. New terrain, new entry points, new hazards in the expansion zone. Early waves are in a small arena, late waves use the full map. | L | Replaces floors as the feeling of escalation |
| 5.1.14 | **Map-specific wave composition** — certain maps favor certain factions. A map with lots of walls plays different with Brutes (wall breakers) than Ghosts (phase through). Wave data should reference which map it expects. | M | Design level, data in JSON |

### 5.1C — Environment & Visual

| # | Task | Size | Notes |
|---|------|------|-------|
| 5.1.15 | **Props placed in maps** — antennas, generators, crates, barriers, fences, containers. The 19 prop GLBs sitting unused. Maps should feel like places, not empty arenas. | M | Models/Props/ has assets ready |
| 5.1.16 | **Hazard VFX** — lava glow, acid bubbles, electric sparks, gas plumes. Hazards need to look dangerous before you step on them. | M | |
| 5.1.17 | **Terrain mutation VFX** — when walls collapse or pits crack open at milestones, visual + audio beat. Dust, debris, screen shake. The map changing should feel like an event. | M | Ties into milestone system |
| 5.1.18 | **Tron environment polish** — fog, grid lines, horizon silhouettes could be better. Lighting pass. Make Grid Prime feel like you're inside a circuit. | M | TronTheme architecture exists |
| 5.1.19 | **Scrapyard environment dressing** — rust, debris, industrial props different from Grid Prime. Use KitBash3D industrial assets. | M | ScrapyardEnvironment.cs exists, maps bare |
| 5.1.20 | **Entry point design per map** — where entries open at which milestone, designed per map not random. Player learns the map across runs. | S | entry_schedule.json per planet |

### 5.1D — Map Editor Tools

| # | Task | Size | Notes |
|---|------|------|-------|
| 5.1.21 | **F12 Map Designer** — visual grid editor. Paint terrain types (empty, wall, elevated, channel, DataStream, hazard, pit, entry, exit). Place props by clicking. Save/load to JSON. Preview entry point schedule on the grid. | L | This is how all maps get built. Without it, maps are hand-edited JSON. |
| 5.1.22 | **Hazard Tuner** — F12 tab or sub-panel. Tune hazard damage, tick rate, visual intensity per hazard type. See hazard radius overlay on map. | M | |
| 5.1.23 | **Terrain Mutation Timeline** — F12 sub-panel in Wave Editor. Define which cells change at which milestone: "wave 8: wall at (5,3) becomes empty, pit opens at (10,7)." Visual overlay showing before/after state per milestone. | M | Ties into Wave Milestone Designer |
| 5.1.24 | **Map Expansion Preview** — F12 button that shows the map at each milestone stage. "Wave 1 arena" → "Wave 8 expanded" → "Wave 15 full map." Verify the progression feels right without playing through. | M | |
| 5.1.25 | **Resource Node Placer** — in Map Designer, place resource nodes and set their value/capture radius. Preview which tower positions would capture them. | S | |

### Map Design Principles

- Every map should have at least one "obvious" tower position and one that's better but riskier
- Hazards should create tradeoffs, not just "avoid this cell"
- Terrain changes at milestones should invalidate at least one tower position, forcing adaptation
- Maps should feel different from each other — not just rotations of the same layout
- Early game (waves 1-5) uses a small portion of the map. Late game uses all of it.
- The expansion from small arena to full map is the difficulty curve, not just harder enemies

---

## 5.2 — Planet 2: Scrapyard (Build It Out)

Scrapyard has a theme but no wave data and limited map design. Needs to feel like a completely different game from Grid Prime.

| # | Task | Size | Notes |
|---|------|------|-------|
| 5.2.1 | **Create P2.json wave data file** — Scrapyard currently has no wave data of its own and silently falls back to Grid Prime's P1.json, which means both planets feel identical. Author a full 20-wave file at Data/Waves/P2.json with Scrapyard-specific composition: mercenary-themed squads that spawn in groups rather than drip-feeds, surges from multiple entry points at once, and tighter spawn timing than Grid Prime. The result should make Scrapyard feel noticeably harder and more chaotic than Planet 1 from wave 1 onward. | M | Currently falls back to P1 data |
| 5.2.2 | **Design 3-5 Scrapyard map layouts** — Grid Prime maps are small, clean, and symmetrical. Scrapyard needs the opposite: wider arenas with irregular geometry, debris clusters forming organic walls instead of neat corridors, and entry points along the map edges (not just top/bottom). These layouts go in Data/Levels/ as JSON files and should be buildable in the F12 Map Designer. Each map should have at least one wide-open danger zone where enemies can swarm from multiple angles, giving the planet its identity as the "no safe position" planet. | M | Larger maps for off-screen spawning |
| 5.2.3 | **Implement Scrapyard mercenary squad AI** — Grid Prime enemies walk in single-file lines toward the Spire. Scrapyard enemies should feel like coordinated mercenary squads: they spawn in groups of 3-5, move together, and split to flank when they encounter towers. This requires adding per-planet AI hooks in VineEnemy.cs so the same enemy type (e.g., Brute) behaves differently depending on which planet it spawns on. The player should immediately feel that Scrapyard enemies are smarter and more aggressive than Grid Prime enemies. | L | VineEnemy needs per-planet AI hooks |
| 5.2.4 | **Dress Scrapyard maps with industrial props and atmosphere** — ScrapyardEnvironment.cs exists but the maps are visually empty. Place industrial props from the KitBash3D asset pack (containers, pipework, cranes, debris piles) throughout each Scrapyard map so the environment feels like a lived-in junkyard, not a blank grid. Props should also serve as visual landmarks so players can orient themselves on the wider maps. Use a distinct warm/amber color palette that contrasts with Grid Prime's cool cyan. | M | ScrapyardEnvironment.cs exists but maps are bare |
| 5.2.5 | **Author Scrapyard-specific milestones** — Data/milestones.json currently only defines milestone timing for Planet 1. Add a planet 2 section with its own milestone wave numbers and rewards. Scrapyard milestones might come faster or slower than Grid Prime to create a different pacing feel — for example, first perk at wave 4 instead of 5, or map expansion earlier to pressure the player sooner. Without this, Scrapyard uses P1 milestone timing, which won't match its harder wave data. | S | Data/milestones.json currently only has planet 1 |
| 5.2.6 | **Tune Scrapyard difficulty scaling independently from Grid Prime** — DifficultyScaler currently applies the same HP/speed/count scaling curve to both planets. Scrapyard should ramp harder: enemies gain HP faster per wave, spawn counts increase more aggressively, and late-wave surges are larger. Load per-planet scaling parameters from JSON so each planet has its own difficulty identity. Scrapyard is the "intermediate" planet — harder than Grid Prime but not as punishing as Planet 3. | S | DifficultyScaler can load per-planet |

---

## 5.3 — Planet 3: Design From Scratch

Planet 3 is the hardest planet. Military AI that scouts, flanks, and adapts.

| # | Task | Size | Notes |
|---|------|------|-------|
| 5.3.1 | **Choose Planet 3's visual theme** — Planet 3 needs a distinct visual identity that immediately tells the player "this is the hardest planet." Three candidates: Void Shard (black/purple/gold, alien crystalline), Ice Moon (white/blue/silver, frozen desolation), Bio-Organic (green/amber, living terrain). Pick one and define its color palette, material style, and mood. This is a design decision that gates all other P3 work — nothing else in this section can start until the theme is locked. | S | Design decision needed |
| 5.3.2 | **Build the Planet 3 PlanetTheme class** — Following the pattern established by TronPlanetTheme and ScrapyardPlanetTheme, create a new theme class that defines P3's material factories, color palette, outline rendering style, and ambient lighting. This is the technical foundation that makes everything on Planet 3 look correct — enemies, towers, terrain, and VFX all pull their colors and materials from this theme. Without it, Planet 3 would render with default/placeholder visuals. | M | Follow existing pattern |
| 5.3.3 | **Implement military enemy AI for Planet 3** — This is the biggest gameplay differentiator across all three planets. Planet 3 enemies behave like a military force: scouts probe your defenses first (small fast units that path through your maze and report back), then the main force attacks your weakest point instead of following the shortest path. They feint at one entrance then push hard at another. They adapt their routing when you rebuild. This requires significant new AI logic in VineEnemy.cs — Planet 1 enemies are dumb, Planet 2 enemies coordinate in squads, Planet 3 enemies actively try to outsmart you. | L | Biggest gameplay differentiator |
| 5.3.4 | **Design Planet 3 map layouts (3-5 maps)** — The largest maps in the game with the most entry vectors (4+ entry points that activate at different milestones). These maps demand that the player build redundant defenses and anticipate flanks — you can't just wall off one chokepoint because enemies come from everywhere. Include long sightlines and bunker positions that reward tower placement planning. These maps should have the most terrain mutation events and the most hazard zones of any planet. | M | |
| 5.3.5 | **Author P3.json wave data (20 waves)** — Write the full 20-wave composition for Planet 3 at Data/Waves/P3.json. The wave structure should reflect military tactics: early waves send scouts (fast, low HP, probing your layout), mid waves bring heavy assault units (high HP, slow, grouped), and late waves feature adaptive squads that change their approach based on where your towers are concentrated. Include commander appearances and at least one boss wave. This should be the hardest wave set in the game. | M | |
| 5.3.6 | **Build Planet 3 environment art and atmosphere** — Place theme-appropriate props, set up planet-specific lighting, and create an atmosphere that feels alien compared to Grid Prime's clean circuits and Scrapyard's industrial grime. Planet 3 should feel hostile and foreign from the moment it loads — the player should sense that this place is dangerous before any enemies even spawn. Use the chosen theme (Void Shard/Ice Moon/Bio-Organic) to drive all visual decisions. | M | |

---

## 5.4 — Enemy Design (Make Them Interesting)

Right now enemies walk toward the Spire. That's it. Each faction needs behaviors that create real decisions.

| # | Task | Size | Notes |
|---|------|------|-------|
| 5.4.1 | **Implement Scavenger faction behavior** — Scavengers are the "unpredictable" enemy type. When a gate flickers or opens/closes nearby, Scavengers should visibly react: they repath, hesitate, or change direction. The player can exploit this by toggling gates to confuse them, but the randomness also makes them dangerous because you can't predict exactly where they'll go. Faction behavior stubs exist in VineEnemy.cs — this task fills in the Scavenger logic so they actually respond to gate state changes instead of ignoring them. | M | VineEnemy faction behavior stubs exist |
| 5.4.2 | **Implement Brute faction behavior** — Brutes are the "destructive" enemy type that the player should dread seeing in a wave. Instead of just walking past towers like current enemies do, Brutes should actively target and damage tower nodes (VineNode) as they pass within melee range. They bulldoze switches, break logic gate states, and leave a trail of damaged infrastructure. This forces the player to either place towers out of Brute reach (sacrificing optimal positioning) or invest in defenses that kill Brutes before they get close. The fear factor is the point. | M | Should target tower nodes not just walk past |
| 5.4.3 | **Implement Ghost faction behavior with real phase mechanic** — Ghosts phase through walls and gates, meaning they cannot be maze'd. They take the shortest straight-line path to the Spire regardless of your tower layout. Currently this is implemented as simply ignoring the pathfinder, but it needs a proper phase mechanic: Ghosts should visually fade as they pass through solid terrain, become temporarily immune to certain tower types while phased, and resolidify in open areas where they become vulnerable again. In groups, Ghosts are terrifying because your maze is irrelevant — they force you to invest in direct-damage solutions instead of pathing strategies. | M | Currently just ignores pathing — needs real phase mechanic |
| 5.4.4 | **Tune Swarm enemy count and spawn behavior** — Swarms are tiny, fast enemies that arrive in large numbers and overwhelm single-target towers. They force the player to invest in AoE (area of effect) towers instead of relying on high-damage single-target builds. The basic behavior mostly works already — this task is about tuning the spawn counts per wave so Swarms feel threatening but not impossible, and ensuring count-based sensors trigger at the right thresholds when a Swarm wave arrives. | S | Mostly works, needs count tuning |
| 5.4.5 | **Design roles for wire_worm and spark_drone and add them to wave data** — Two enemy models (wire_worm and spark_drone) exist in the Models/Enemies/ folder but are never actually spawned because they aren't referenced in any wave JSON file. Design a gameplay role for each (e.g., wire_worm could be a burrowing enemy that bypasses surface towers, spark_drone could be a flying unit that ignores terrain). Then add them to P1.json and P2.json wave compositions so players actually encounter them during runs. | M | Models in /Enemies, not in any wave JSON |
| 5.4.6 | **Add ranged attack behavior for artillery/siege enemies** — Currently all enemies just walk toward the Spire and deal damage on contact. Some enemies should stop at range and shoot at your towers, forcing you to deal with threats that don't walk into your kill zones. ENEMY_ATTACK_RANGE is already defined in Constants.cs but the actual attack-from-range behavior in VineEnemy.cs is minimal. Implement proper ranged attack logic: the enemy stops when in range, fires at the nearest tower, and only advances if its target is destroyed. This creates a fundamentally different threat type. | M | ENEMY_ATTACK_RANGE exists in Constants but behavior is basic |
| 5.4.7 | **Implement Commander runtime behaviors** — Commanders are elite enemies that appear at key moments and change the dynamic of a wave. Three types are defined in CommanderData but only exist as data stubs with no runtime logic: AuraBuffer (passively buffs all nearby enemies with increased speed/armor while alive), Rally (calls in reinforcement spawns mid-wave when it reaches a certain HP threshold), and Assassin (ignores the Spire entirely and beelines toward the player's BIT to disrupt their ability to build). Implement the actual runtime behavior for all three so Commanders feel like mini-boss events that demand an immediate response. | L | CommanderData exists, behaviors are stubs |
| 5.4.8 | **Design and implement at least one boss per planet** — Bosses should not just be "regular enemy with 10x HP." Each planet needs at least one boss with unique mechanics that force the player to change their strategy mid-wave. Concepts from design docs: Signal Jammer (disables towers in an area, forcing the player to move their BIT to manually re-activate them), Overloader (supercharges your towers but makes them overheat and shut down), Pathfinder (rewrites the enemy path mid-wave, routing enemies around your defenses). Boss encounters should feel like a climactic event, not just a sponge that takes longer to kill. | L | Boss concepts exist in design docs |
| 5.4.9 | **F12 Enemy Designer** — create/edit enemy types in-game. Set faction, HP, speed, armor, behavior type, attack pattern, model, scale. Spawn test enemies on the current map to observe behavior. | L | Without this, every enemy change is code |
| 5.4.10 | **F12 Commander Editor** — define commander spawn conditions (reactive/random/scripted), behavior type (Elite/AuraBuffer/Rally/Assassin), buff values, radius. Test-spawn on map. | M | CommanderData exists, needs editor |
| 5.4.11 | **F12 Boss Editor** — define boss mechanics, phase transitions, ability cooldowns. Preview boss on map. Existing BossEditor in Crawler project can be adapted. | M | |
| 5.4.12 | **F12 Ascendant Inhabit Tuner** — tune inhabit parameters live: corpse window duration, base inhabit time, relic bonus per relic, camera zoom multiplier, ability damage values, unlock run threshold. Test inhabit by spawning a dead Ascendant on demand. | M | AscendantInhabit.cs has all values as constants — move to tunable |

---

## 5.5 — Tower & Build Depth (Make Building Fun)

Towers auto-fire and have mod slots. Now make each tower type feel distinct and make build decisions matter.

| # | Task | Size | Notes |
|---|------|------|-------|
| 5.5.1 | **Replace procedural tower meshes with real 3D models** — There are 7 turret GLB models sitting in Models/Turrets/ that look great, but DamageTower (and most other tower types) still render using basic procedural meshes generated in code. Map each real model to a tower type so players can instantly tell towers apart by their visual silhouette. This is the single fastest visual upgrade available — swap a mesh reference and every tower on every map looks dramatically better with zero gameplay code changes. | M | Models/Turrets/ has 7 variants |
| 5.5.2 | **Make each tower type feel radically distinct in gameplay** — DamageTower, SlowField, PushPull, BuffEmitter, and SignalCannon are all different tower types, but in practice they mostly look and feel similar during gameplay. Each needs its own distinct projectile visual (beam vs. bullet vs. pulse vs. aura), unique impact effect when hitting enemies, and a different sound profile. The goal is that a player watching their defense from the overview can instantly identify what each tower is doing without reading labels. Tower identity drives build strategy — if towers feel samey, build decisions feel meaningless. | L | Currently most feel similar |
| 5.5.3 | **Implement visible effects for mod slot components** — The mod slot system in TowerSlotSystem.cs defines components like Chain Arc (damage bounces between enemies), Scatter (hits multiple targets in a cone), Cryo (slows enemies), and Incendiary (burns enemies over time). The data and stat modifications work, but there are no visual effects — the player can't see whether their tower has Chain Arc or Cryo installed. Add distinct VFX for each: Chain Arc should show a visible lightning bolt bouncing between enemies, Cryo should show ice particles and a blue tint on slowed enemies, Incendiary should show flames on burning targets. Players need to SEE what their mod choices are doing. | L | TowerSlotSystem has data, needs VFX |
| 5.5.4 | **Add visual and audio feedback when tower synergies activate** — Synergies (like Thermal Shock, Arc Network, Suppression) trigger when specific mod component combinations are present across nearby towers. The synergy damage/effects are already computed in code, but they're completely invisible to the player — you get a bonus but never know it fired. Add particle effects, a color flash on the involved towers, and a sound cue when a synergy activates. The player should have an "oh cool, my combo just fired!" moment that rewards thoughtful tower placement. | M | Synergies are computed but invisible |
| 5.5.5 | **Run a full balance pass on all tower stats** — Use the existing F12 Node Balance editor (NodeBalanceEditor) to tune tower costs, DPS, ranges, fire rates, and mod slot component values while playing. The goal is that every tower type has a clear use case: DamageTower shouldn't be strictly better than everything else, cheap towers should be viable for early waves, and expensive towers should feel worth saving up for. Every mod component should feel like a meaningful upgrade, not a marginal stat bump. | M | NodeBalanceEditor exists |
| 5.5.6 | **Fix the PushPull tower so it actually moves enemies** — The PushPull tower is supposed to be one of the most fun mechanics in the game: it physically shoves enemies backward (pull) or sideways (push), disrupting their pathing and buying time for damage towers. Currently it's completely broken — ActivateEffect fires in VineNode.cs but there's no movement logic applied to enemies. Implement actual physics-based push/pull: when the tower fires, nearby enemies should be visibly displaced along a force vector. This has been broken since alpha and players keep asking about it. | M | ActivateEffect fires but no movement logic |
| 5.5.7 | **F12 Tower Slot Editor** — edit mod slot component stats live. Change Chain Arc bounce range, Cryo slow amount, Incendiary burn DPS. See effect on placed towers immediately. Synergy preview panel showing which synergies are possible with current loadout. | M | Extends existing Node Balance editor |
| 5.5.8 | **F12 Tower Test Mode** — button that spawns a wave of test enemies and lets you watch your current tower setup fight them. Reset and try different tower configs without restarting the run. | M | |

---

## 5.6 — Material System (Make Magic Matter)

Three material types exist as labels. They need real gameplay effects.

| # | Task | Size | Notes |
|---|------|------|-------|
| 5.6.1 | **Implement Chaos material gameplay effects** — Chaos is one of three material types the player can harvest, but currently it's just a label with no gameplay impact. Chaos should offer two sub-paths the player chooses between at a material selection screen: Mind (enemies in an area become confused, attack each other, and randomly change direction — crowd control through disruption) or Corrosive (enemies take damage over time and their armor degrades, making them increasingly vulnerable). The player picks one sub-path per run, which changes how they use Chaos material for the rest of that session. This is a design + implementation task: define the exact values, build the runtime effects, and wire them into VineEnemy.cs damage/behavior systems. | L | Design + implementation |
| 5.6.2 | **Implement Power material gameplay effects** — Power is the straightforward "make towers stronger" material type. Towers within range of accumulated Power material should gain extended firing range, amplified damage, and supercharged signal propagation (signals travel faster/farther along the node chain). Add a visible aura around Power-boosted towers so the player can see the buff zone and strategically position towers inside it. The visual feedback is critical — without it, the player won't understand why some towers perform better than others. Wire the stat buffs through VineNode.cs. | M | Straightforward stat buff with visual |
| 5.6.3 | **Implement Environment material gameplay effects** — Environment is the most ambitious material type: it lets the player reshape the terrain itself. With enough Environment material, the player can convert wall cells to walkable paths (opening new routes), create new walls on empty cells (blocking enemy paths), or lay down channel terrain on demand. The map becomes a weapon the player actively sculpts during a run. This requires deep integration with VineGrid.cs (terrain mutation) and VinePathfinder.cs (repath after player-triggered terrain changes). The player experience should feel like having god-mode over the map layout. | L | Most ambitious material type |
| 5.6.4 | **Add visual feedback for material accumulation** — VineHarvester.cs tracks how much of each material type the player has gathered, but there's zero visual indication in the game. The player has no idea how much Chaos/Power/Environment material they've collected or how close they are to being able to use it. Add a visible material meter to the HUD, particle effects around the harvester that intensify as materials accumulate, and a color shift on the mining dome that reflects the dominant material type. This transforms an invisible number into a tangible resource the player actively monitors. | M | VineHarvester tracks it, no visual |
| 5.6.5 | **Build the material shop that appears at milestone waves** — At milestone waves (e.g., wave 5, 10, 15), the player should get a shop screen where they spend accumulated materials on upgrades. The shop offerings are deterministic (authored per milestone per planet, not random), so players can plan ahead across runs. The shop doesn't exist yet — this task builds the entire UI, the purchase flow, the upgrade application logic, and the data structure for defining shop contents in JSON. This is the payoff for the entire material gathering loop: harvest materials during waves, spend them at milestones to power up. | M | Shop doesn't exist yet |
| 5.6.6 | **F12 Material Effect Tuner** — tune all material effect values live: Chaos confusion radius/duration, Power range bonus %, Environment terrain conversion cost. Preview material aura radius on map. | M | |
| 5.6.7 | **F12 Material Shop Editor** — define which upgrades appear at which milestone for each material type. Set costs, preview upgrade tree per planet. Export to JSON. | M | Deterministic shop = authored content = needs editor |

---

## 5.7 — Audio (The Game Is Silent)

34 audio files exist. No music. Most SFX are placeholder PCM bleeps.

| # | Task | Size | Notes |
|---|------|------|-------|
| 5.7.1 | **Source and integrate background music tracks** — The game is completely silent except for a few bleeps. AudioManager has a Music bus ready but no music is loaded. At minimum, create or source four tracks: a menu loop (chill, atmospheric), build phase ambient (calm tension, the player is thinking), wave phase tension (driving beat that escalates with wave difficulty), and a boss theme (dramatic, signals a major threat). Music is the single biggest contributor to game atmosphere — without it, even great gameplay feels flat. Use royalty-free or AI-generated tracks. | L | AudioManager has Music bus, no music loaded |
| 5.7.2 | **Assign distinct fire sounds to each tower type** — Currently all towers reuse the same fire sound, which makes them feel identical even when their gameplay is different. Each tower type (DamageTower, SlowField, PushPull, BuffEmitter, SignalCannon) needs its own firing SFX that matches its personality: DamageTower should sound sharp and punchy, SlowField should hum or pulse, PushPull should whoosh, etc. There are approximately 100 unmapped Sonniss WAV files in Assets/Audio/ — many of these are usable with minimal editing. Wire the sounds through the existing audio event system. | M | ~100 Sonniss WAVs unmapped in Assets/Audio |
| 5.7.3 | **Add enemy spawn and death sounds per faction** — Enemies currently appear and die silently. Each faction needs audio identity: Brutes should sound heavy and metallic (loud footsteps, crunchy death), Swarms should sound skittery and chittering (rapid light sounds), Ghosts should have an eerie phasing hum, Scavengers should sound erratic. Spawn sounds alert the player to what's coming before they see it. Death sounds provide the satisfying feedback that their defenses are working. | M | |
| 5.7.4 | **Add impact and hit SFX with dynamic audio ducking** — When a tower projectile hits an enemy, when a chain arc bounces between 5 targets, when a boss takes a massive hit — these moments need satisfying audio. Hit sounds, explosion sounds, and the crunchy "that chain just fired perfectly" payoff sound. Implement dynamic ducking so that big impact sounds momentarily lower the volume of everything else, making major hits feel weighty. This is the audio equivalent of screen shake — it makes combat feel physical and responsive. | M | Concept from design session |
| 5.7.5 | **Add UI sound effects for all game events** — Currently about 7 different UI events (node place, node sell, wave start, wave complete, milestone reached, perk selected, resource earned) all share a single pickup.wav file. Each event needs its own distinct sound so the player gets audio confirmation of their actions without looking at the screen. Wave start should sound urgent, wave complete should sound triumphant, milestone reached should sound like a reward. These are small sounds but they make the entire interface feel polished and responsive. | S | |
| 5.7.6 | **Map the Sonniss WAV library to game events in Data/audio.json** — There are 100+ professional WAV files in Assets/Audio/Sonniss/ from a sound effects pack, but Data/audio.json doesn't reference any of them. Go through the library, categorize each file (impact, ambient, UI, mechanical, organic, etc.), and map appropriate files to game events in the audio manifest. This is the foundation for all other audio tasks — once the mapping exists, the F12 Sound Designer can be used to swap and tune sounds without touching code. | M | Files exist, manifest doesn't reference them |
| 5.7.7 | **F12 Sound Designer extensions** — extend existing module: browse unmapped Sonniss WAVs, preview them, drag to assign to game events. Show which events have no sound assigned. Volume/pitch randomization per event. | M | SoundDesigner module exists, needs event mapping UI |

---

## 5.8 — Visual Juice (Make It Feel Good)

The game needs to feel satisfying moment to moment.

| # | Task | Size | Notes |
|---|------|------|-------|
| 5.8.1 | **Add enemy death VFX so kills feel satisfying** — Currently when an enemy dies, it just disappears instantly. This is the single biggest "game feel" gap. When enemies die, they should pop with a particle burst (color-matched to their faction), scatter small resource drop particles that fly toward the player's resource counter, and leave a brief residue on the ground. VfxFactory already exists and has the infrastructure for spawning particle effects — it just needs death burst configurations added. Every single enemy kill should feel crunchy and rewarding because kills are the core feedback loop of the entire game. | M | VfxFactory exists, needs death burst |
| 5.8.2 | **Add distinct fire VFX to each tower type** — Towers need visible muzzle flashes when they fire, projectile trails as shots travel to targets, and impact sparks when shots hit enemies. Each tower type should have visually distinct fire effects: DamageTower gets a bright sharp muzzle flash and fast projectile, SlowField gets a spreading pulse wave, SignalCannon gets a beam that lingers briefly. Some basic VFX exist but they're minimal and mostly shared across tower types. Distinct fire VFX let the player read their defense at a glance and feel the power difference between tower tiers. | M | Some VFX exist but are minimal |
| 5.8.3 | **Implement screen shake tuned to event severity** — TDCamera already has a Shake method, but it's not wired to most game events. Add screen shake on: big enemy kills (light shake), boss damage taken (medium shake), Spire taking damage (heavy shake — the player should feel alarmed), wave complete (short celebratory shake). Calibrate intensity by event severity so small kills don't shake but a boss explosion rocks the screen. Screen shake is the cheapest way to make combat feel impactful — it costs almost nothing to implement but dramatically improves how hits feel. | S | TDCamera has Shake method |
| 5.8.4 | **Add a visual cascade effect when signal chains propagate through towers** — The signal chain system is one of the most unique mechanics in the game: a sensor detects an enemy, fires a signal, and that signal propagates through connected nodes causing each to fire in sequence. Currently this happens with minimal visual feedback. When a signal propagates through 3+ nodes, each node should light up in sequence with a visible pulse traveling along the connection lines between them. This creates the "oh wow, my chain just fired perfectly" moment that rewards thoughtful tower placement and makes the signal system feel magical instead of invisible. | M | Signal propagation exists, visual feedback is minimal |
| 5.8.5 | **Add wave start and wave complete visual/audio fanfare** — Wave transitions are major game moments but currently happen with no ceremony. Before a wave starts, build tension: a countdown, darkening screen edges, an ominous audio swell. When a wave is cleared, provide satisfaction: a brief flash, a triumphant sound sting, maybe the HUD pulses. These bookends turn waves from arbitrary segments into distinct dramatic beats. The player should feel "here it comes" before each wave and "I survived" after each clear. | S | |
| 5.8.6 | **Animate the extraction counter so score increases feel rewarding** — The extraction number is the player's primary score and the measure of a successful run, but currently it just silently updates its text value. When the extraction count increases, the number should pulse larger briefly, glow with a color flash, and maybe emit small particles. Bigger increases (like end-of-wave bonuses) should get a more dramatic animation. This makes the core reward loop — "kill enemies, earn extraction, see number go up" — feel viscerally satisfying instead of clinical. | S | Currently just updates text |
| 5.8.7 | **Verify floating damage numbers are working and visible** — DamageNumber.cs already exists in VFX/ and is supposedly implemented. This task is to verify it's actually spawning, visible against all planet theme backgrounds, properly scaled, and not overlapping into unreadable clumps during heavy combat. If it's broken, fix it. If it's working but hard to read, tune the font size, float speed, fade timing, and color. Damage numbers let players understand how effective their tower builds are — they're a critical feedback mechanism for the entire tower strategy system. | S | VFX/DamageNumber.cs exists |
| 5.8.8 | **F12 VFX Preview** — extend editor: preview death burst, fire VFX, signal cascade in isolation. Tune particle count, size, duration, color without restarting. Side-by-side comparison of different VFX configs. | M | VfxFactory exists, needs preview mode |

---

## 5.9 — Difficulty & Balance (Make It Fair)

| # | Task | Size | Notes |
|---|------|------|-------|
| 5.9.1 | **Complete a full 20-wave playtest of Grid Prime and document every pain point** — Play through all 20 waves on at least 2-3 different Grid Prime maps, taking notes on every moment that feels too easy, too hard, boring, or unfair. Document specific wave numbers: "wave 7 felt like nothing happened," "wave 12 was an impossible spike," "I had nothing to do between waves 10-13." This is a human task that produces a written list of balance issues to fix. Every other task in this section depends on this playtest data — without it, balance tuning is guesswork. | L | Human task |
| 5.9.2 | **Determine if 90 starting resources is the right amount** — The player begins each run with 90 resources (defined in Constants.VINE_STARTING_RESOURCES). The question is whether that's enough to build a functional initial defense before wave 1 hits, but not so much that the first few waves feel trivial. If 90 is too high, early waves have no tension. If it's too low, new players can't figure out the game before dying. Test multiple values (60, 75, 90, 120) and find the sweet spot where the player can place 2-3 towers and a harvester but still feels pressure. | S | Constants.VINE_STARTING_RESOURCES |
| 5.9.3 | **Balance the tower cost curve so every tower type has a use case** — DamageTower costs 15 resources while Extender costs 3. Is that 5:1 ratio correct? If DamageTower is too cheap, players spam it and ignore other types. If Extender is too cheap, there's no reason not to fill the map with them. Use the F12 Node Balance editor to tune costs, DPS, and ranges live during gameplay. The goal: cheap towers are viable for early waves and gap-filling, expensive towers are powerful enough to justify saving up, and no single tower type dominates every situation. | S | |
| 5.9.4 | **Tune the extraction reward curve so late waves feel like a massive payoff** — The extraction counter is the player's score and the reason to keep pushing for "one more wave." Use the existing F12 Extraction Curve Tuner to adjust how much extraction the player earns per wave. Early waves should give modest extraction (enough to feel progress), mid waves should accelerate noticeably, and surviving to wave 15+ should feel like hitting a jackpot. The curve should create a "I should extract now... but ONE more wave would give me so much more" tension that defines the risk/reward loop. | M | Tool exists |
| 5.9.5 | **Smooth out the enemy HP scaling curve across 20 waves** — Enemy HP increases each wave via DifficultyScaler, but the question is whether it feels like a smooth ramp or has jarring spikes where suddenly enemies become bullet sponges. Load the DifficultyScaler JSON, play through waves, and adjust the HP multiplier curve so difficulty increases feel gradual and fair. The player should always feel like they COULD have survived the next wave if they'd built better — not that the game arbitrarily became impossible at wave X. | M | |
| 5.9.6 | **Review all 13 existing perks for balance and add 5-10 new ones that change playstyle** — Perks are offered at milestone waves and should present meaningful choices that change how the player approaches the rest of the run. Review all 13 perks in VinePerkData.cs: are any obviously better than others (always-pick)? Are any useless (never-pick)? Then design and implement 5-10 new perks that create real build diversity — e.g., "towers cost less but deal less damage" (economy build), "enemies drop double resources but move faster" (risk/reward), "signal chains propagate farther but towers fire slower" (chain build). Every perk selection should feel like a hard choice. | M | VinePerkData.cs |
| 5.9.7 | **F12 Perk Editor** — create/edit perks in-game. Set name, description, effect type, values, icon. Preview perk selection screen with your perks. Mark perks as active/disabled for testing. | M | |
| 5.9.8 | **F12 Balance Dashboard** — single view showing all tunable values across systems: tower DPS, enemy HP curve, extraction curve, perk values, material rates. Spot outliers. | M | Aggregates data from all other editors |

---

## 5.10 — First 60 Seconds (The Hook)

If the first minute isn't compelling, nothing else matters.

| # | Task | Size | Notes |
|---|------|------|-------|
| 5.10.1 | **Tune wave 1 timing so enemies arrive within 15 seconds of placing the mining rig** — The first minute of a run must feel urgent, not idle. Currently the build phase before wave 1 can feel too long — the player places their mining rig, builds a few towers, and waits. Adjust the wave 1 spawn timing in P1.json (and P2.json) so enemies start arriving fast, within 15 seconds of the rig being placed. The player should feel "oh no, they're already coming" not "I guess I'll wait." This single timing change transforms the opening from boring setup into an exciting scramble. | S | Wave 1 timing in P1.json |
| 5.10.2 | **Ensure the first tower placement, first kill, and first resource earn all have satisfying audiovisual feedback** — The first three actions a new player takes define whether they keep playing. First tower placed: a chunky "locked in" sound and a brief visual pulse on the tower. First enemy killed: a satisfying pop, particles, and the resource counter visibly ticking up. First resource earned: an audio cha-ching and the resource number animating. These moments tie into the VFX (section 5.8) and SFX (section 5.7) work but are called out here because they must be verified as a sequence — the first 60 seconds is a chain of "action → satisfying feedback" that teaches the game loop. | M | Ties into VFX + SFX work |
| 5.10.3 | **Make the wave 5 perk feel like a meaningful reward that changes the next 5 waves** — The first perk selection is already wired to appear at wave 5 (a milestone), but the quality of the perks offered determines whether this moment feels exciting or forgettable. Ensure the wave 5 perk pool contains choices that visibly change how the player plays waves 6-10: a perk that makes towers fire faster (immediately noticeable), a perk that gives bonus resources per kill (changes build strategy), a perk that reveals more of the map (changes scouting). The player should think "oh cool, now I can do something new" not "I guess that's slightly better." | S | Already wired, needs perk quality |
| 5.10.4 | **Build first-run tutorial hints that guide new players through their first 60 seconds** — A brand new player has no idea what to do. Add a sequence of contextual hints that appear only on the first run: "Place mining rig" (with an arrow pointing to valid spots) → "Build towers to defend" (after rig is placed) → "Press Space to start the wave" (after first tower is built). Track whether the player has seen the tutorial using a MetaPerkSave flag so it never appears again after the first run. Keep hints minimal and non-intrusive — they should feel like gentle nudges, not a mandatory walkthrough. | M | UX15, not yet built |
| 5.10.5 | **Make the extraction counter prominent and immediately understandable from wave 1** — The extraction counter is already in the HUD, but verify that it's visually prominent enough that a new player notices it within the first minute. It should be large, well-positioned (top center or similar), and have a brief "this is your score" callout on first run. The player should immediately understand the core loop: "kill enemies → earn extraction → bigger number = better run → extract at the right time." If the counter is small, buried, or unclear, the entire risk/reward extraction mechanic falls flat because the player doesn't know what they're optimizing for. | S | Already in HUD, verify visibility |

---

## 5.11 — Cross-Planet Verification

Every system must work across all planets. This is not optional polish — it's part of building the system.

| # | Task | Size | Notes |
|---|------|------|-------|
| 5.11.1 | **Per-planet map data** — every map mechanic (hazards, pits, destructible walls, resource nodes, expansion zones) needs map JSONs for BOTH Grid Prime and Scrapyard. Don't author content for one planet only. | M | Data/Levels/ needs P1 and P2 variants |
| 5.11.2 | **Per-planet wave data** — P2.json must exist before any wave-related feature can be considered done. If you add a new enemy type or commander, add it to both P1.json and P2.json. | M | P2.json still doesn't exist |
| 5.11.3 | **Per-planet milestones** — milestones.json needs entries for planet 2 (and 3 when it exists). Different milestone timing per planet = different difficulty feel. | S | Currently only planet 1 |
| 5.11.4 | **Per-planet entry schedules** — entry_schedule.json needs planet 2 variant. Different maps = different entry progression. | S | |
| 5.11.5 | **Theme compatibility testing** — every new VFX, particle, material, or shader must be tested on both TronPlanetTheme and ScrapyardPlanetTheme. Tron uses cyan emissive outlines, Scrapyard uses warm amber. A VFX that looks great on dark blue looks washed out on brown. | M | Known issue from alpha — Scrapyard bloom was nuclear |
| 5.11.6 | **Enemy behavior per planet** — Grid Prime enemies are circuit-based (predictable). Scrapyard enemies are mercenary (squad-based). New behaviors must respect the per-planet AI style, not be one-size-fits-all. | M | VineEnemy has faction behavior, needs planet context |
| 5.11.7 | **F12 Planet Switcher** — button in the editor that switches the current planet theme live. Immediately see how your current map/VFX/enemies look under different themes without restarting. | M | PlanetTheme.Current is already swappable |
| 5.11.8 | **Automated cross-planet smoke test** — F12 button: run each planet for 5 waves automatically, report any errors, null refs, missing assets, visual anomalies. Catches "works on P1, crashes on P2" before humans playtest. | L | BVT system exists, extend for multi-planet |

### Per-System Checklist (copy this for every new feature)

When you build a new system, verify ALL of these before marking it done:

```
[ ] Works on Grid Prime (Planet 1)
[ ] Works on Scrapyard (Planet 2)
[ ] Data files created for both planets (JSON, milestones, maps)
[ ] VFX looks correct under both planet themes
[ ] Enemy behaviors interact correctly with new system on both planets
[ ] F12 editor tool works regardless of active planet
[ ] No hardcoded planet-1 assumptions in the code
```

---

## File Ownership for Phase 5

To prevent conflicts when multiple people work simultaneously, here's who can touch what. Check this before editing any file.

### Map & Terrain (coordinate before touching)
- VineGrid.cs — terrain types, cell data, entry regions, terrain mutation
- VinePathfinder.cs — pathing, recalc on terrain change
- VineMapLayouts.cs — layout definitions
- Data/Levels/*.json — map layout data files

### Enemies (coordinate before touching)
- VineEnemy.cs — enemy controller, faction behaviors, attack logic
- VineWaveData.cs — surge/wave/commander data structures
- Data/Waves/*.json — wave composition per planet

### Towers (coordinate before touching)
- VineNode.cs — tower behavior, signal processing, auto-fire
- VineNodeData.cs — tower type definitions, costs, ranges
- TowerSlotSystem.cs — mod slots, components, synergies

### Player (coordinate before touching)
- VinePlayer.cs — BIT movement, abilities, material interaction
- VineHarvester.cs — mining building, resource/material production

### Shared (add to bottom only, comment your section)
- Constants.cs — all tuning numbers
- GameEvents.cs — event bus
- Enums.cs — type definitions
- GameManager.cs — game state, phases

### Safe to create new files
- Scripts/Editor/Modules/ — new F12 editor tabs
- Data/ — new JSON data files
- Scripts/VineLogic/ — new system classes (e.g., HazardManager.cs, MaterialEffects.cs)

---

## Priority Order

```
Must do first (the game needs these to be testable):
  5.1 Grid Prime maps + dressing
  5.4 Enemy behaviors (at least Scavenger + Brute)
  5.7.1-5.7.2 Music + tower SFX
  5.8.1-5.8.3 Death VFX, fire VFX, screen shake
  5.10 First 60 seconds

Then (content expansion):
  5.2 Scrapyard planet build-out
  5.5 Tower differentiation + mod slot VFX
  5.9 Balance pass

Then (depth):
  5.6 Material system real effects
  5.4.7-5.4.8 Commanders + bosses
  5.3 Planet 3

Last (polish):
  5.8.4-5.8.7 Signal cascade, wave fanfare, number animation
  5.7.3-5.7.6 Enemy/impact/UI SFX, Sonniss mapping
```

---

## Size Legend

- **S** = hours, single file
- **M** = 1-3 sessions, 2-4 files
- **L** = multi-session, significant new functionality or design work
