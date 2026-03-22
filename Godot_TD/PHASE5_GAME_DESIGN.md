# Phase 5 — Make It A Real Game

*Systems are built. Now make it feel like something you can't stop playing.*
*Phase 0-4 were infrastructure. Phase 5 is the game.*

---

## Progress Overview

### Map Design
- [ ] Map hazards — destructible walls, environmental damage, pits, DataStreams, elevated platforms
- [ ] Terrain mutation at milestones — walls collapse, pits open, map expands
- [ ] Map expansion zones — arena grows from small to full over a run
- [ ] Resource nodes on map — reward expansion and risk-taking

### Planet Content
- [ ] **Planet 1 (Grid Prime)** — 3-5 designed maps, props, hazard zones, entry point design
- [ ] **Planet 2 (Scrapyard)** — 3-5 maps, P2.json wave data, mercenary enemy behavior
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
| 5.2.1 | **Create P2.json** — 20 waves of Scrapyard-specific enemies. Mercenary squads, multi-entry surges, tighter timing, group spawns. Harder than P1. | M | Currently falls back to P1 data |
| 5.2.2 | **Scrapyard maps** — 3-5 layouts that feel industrial. Wider, more open, enemies from edges. Different geometry from Grid Prime. | M | Larger maps for off-screen spawning |
| 5.2.3 | **Scrapyard enemy behavior** — mercenary squads that arrive together, flank, and coordinate. Not just single-file lines like Grid Prime. | L | VineEnemy needs per-planet AI hooks |
| 5.2.4 | **Scrapyard environment dressing** — rust, debris, industrial props. Different props from Grid Prime. Use KitBash3D industrial assets. | M | ScrapyardEnvironment.cs exists but maps are bare |
| 5.2.5 | **Scrapyard milestones.json** — planet-specific milestones. Maybe different timing from P1. | S | Data/milestones.json currently only has planet 1 |
| 5.2.6 | **Scrapyard difficulty curve** — separate scaling from P1. Scrapyard should be harder. | S | DifficultyScaler can load per-planet |

---

## 5.3 — Planet 3: Design From Scratch

Planet 3 is the hardest planet. Military AI that scouts, flanks, and adapts.

| # | Task | Size | Notes |
|---|------|------|-------|
| 5.3.1 | **Choose theme** — candidates: Void Shard (black/purple/gold), Ice Moon (white/blue/silver), Bio-Organic (green/amber). Pick one. | S | Design decision needed |
| 5.3.2 | **PlanetTheme implementation** — new theme class like TronPlanetTheme/ScrapyardPlanetTheme. Material factories, color palette, outline style. | M | Follow existing pattern |
| 5.3.3 | **Military enemy AI** — scouts that probe defenses, main force that attacks weak points, flanking, feints, adaptive routing. The smartest enemies. | L | Biggest gameplay differentiator |
| 5.3.4 | **P3 maps** — largest maps. Multiple entry vectors. Player must build redundancy and anticipate flanks. | M | |
| 5.3.5 | **P3.json wave data** — 20 waves with military composition. Scouts early, heavy assault mid, adaptive late. | M | |
| 5.3.6 | **P3 environment** — theme-appropriate props, lighting, atmosphere. Should feel alien compared to P1 and P2. | M | |

---

## 5.4 — Enemy Design (Make Them Interesting)

Right now enemies walk toward the Spire. That's it. Each faction needs behaviors that create real decisions.

| # | Task | Size | Notes |
|---|------|------|-------|
| 5.4.1 | **Scavenger behavior** — confused by flickering gates (already designed). Implement: they actually path differently when a gate opens/closes nearby. Exploitable but unpredictable. | M | VineEnemy faction behavior stubs exist |
| 5.4.2 | **Brute behavior** — bulldoze switches, break logic state, attack nodes. They should physically damage your towers as they pass. Make the player fear brutes. | M | Should target tower nodes not just walk past |
| 5.4.3 | **Ghost behavior** — phase through walls/gates. Can't be maze'd. Forces direct-damage solutions. Terrifying in groups. | M | Currently just ignores pathing — needs real phase mechanic |
| 5.4.4 | **Swarm behavior** — tiny, fast, trigger count sensors early. Overwhelm single-target towers. Force AoE investment. | S | Mostly works, needs count tuning |
| 5.4.5 | **New enemy types** — wire_worm and spark_drone models exist but aren't spawned. Design their roles and add to wave data. | M | Models in /Enemies, not in any wave JSON |
| 5.4.6 | **Enemy ranged attacks** — some enemies should shoot at towers from range, not just walk past. Artillery enemies, siege units. | M | ENEMY_ATTACK_RANGE exists in Constants but behavior is basic |
| 5.4.7 | **Commander behaviors** — AuraBuffer (buff nearby), Rally (call reinforcements), Assassin (beeline to player). Data stubs exist, need runtime logic. | L | CommanderData exists, behaviors are stubs |
| 5.4.8 | **Boss design** — at least one real boss per planet. Not just high-HP enemy. Unique mechanics: signal jammer, overloader, pathfinder. | L | Boss concepts exist in design docs |

---

## 5.5 — Tower & Build Depth (Make Building Fun)

Towers auto-fire and have mod slots. Now make each tower type feel distinct and make build decisions matter.

| # | Task | Size | Notes |
|---|------|------|-------|
| 5.5.1 | **Tower visual variety** — 7 turret GLB models exist but DamageTower uses procedural mesh. Map real models to tower types. Instant visual upgrade. | M | Models/Turrets/ has 7 variants |
| 5.5.2 | **Tower type differentiation** — DamageTower, SlowField, PushPull, BuffEmitter, SignalCannon should each feel radically different. Distinct projectiles, effects, sounds. | L | Currently most feel similar |
| 5.5.3 | **Mod slot component effects** — Chain Arc, Scatter, Cryo, Incendiary are defined. Implement visual effects for each. Players need to SEE the difference. | L | TowerSlotSystem has data, needs VFX |
| 5.5.4 | **Synergy visual feedback** — when Thermal Shock / Arc Network / Suppression activates, show it. Particle effects, color change, sound cue. | M | Synergies are computed but invisible |
| 5.5.5 | **Balance pass** — tower costs, DPS, ranges, slot component values. Use the F12 Node Balance editor to tune live. | M | NodeBalanceEditor exists |
| 5.5.6 | **PushPull node fix** — still a no-op. Implement actual push/pull physics on enemies. This is a fun mechanic that's been broken since alpha. | M | ActivateEffect fires but no movement logic |

---

## 5.6 — Material System (Make Magic Matter)

Three material types exist as labels. They need real gameplay effects.

| # | Task | Size | Notes |
|---|------|------|-------|
| 5.6.1 | **Chaos effects** — Mind path: enemies get confused, attack each other, change direction. Corrosive path: DoT, armor degradation. Choose one sub-path at material selection. | L | Design + implementation |
| 5.6.2 | **Power effects** — towers in range of Power material get extended range, amplified damage, supercharged signal propagation. Visible aura. | M | Straightforward stat buff with visual |
| 5.6.3 | **Environment effects** — terrain manipulation. Convert walls to walkable, create new walls, channel terrain on demand. Map is your weapon. | L | Most ambitious material type |
| 5.6.4 | **Material accumulation visual** — show the player how much material they've gathered. Meter, particles, dome color shift. Currently invisible. | M | VineHarvester tracks it, no visual |
| 5.6.5 | **Material shop at milestones** — spend accumulated materials on upgrades. Deterministic options per milestone per planet. | M | Shop doesn't exist yet |

---

## 5.7 — Audio (The Game Is Silent)

34 audio files exist. No music. Most SFX are placeholder PCM bleeps.

| # | Task | Size | Notes |
|---|------|------|-------|
| 5.7.1 | **Background music** — at minimum: menu loop, build phase ambient, wave phase tension, boss theme. Royalty-free or AI-generated. | L | AudioManager has Music bus, no music loaded |
| 5.7.2 | **Tower SFX** — distinct fire sounds per tower type. Currently reused. Each tower should sound different. | M | ~100 Sonniss WAVs unmapped in Assets/Audio |
| 5.7.3 | **Enemy SFX** — spawn sounds, death sounds, faction-specific audio. Brutes should sound heavy. Swarm should sound skittery. | M | |
| 5.7.4 | **Impact SFX** — hit sounds, explosion sounds, the satisfying crunch when a chain fires perfectly. Dynamic ducking for big hits. | M | Concept from design session |
| 5.7.5 | **UI SFX** — node place, node sell, wave start, wave complete, milestone reached, perk selected. Currently ~7 events share pickup.wav. | S | |
| 5.7.6 | **Map Sonniss library** — 100+ WAV files in Assets/Audio/Sonniss/ need mapping to game events in Data/audio.json. | M | Files exist, manifest doesn't reference them |

---

## 5.8 — Visual Juice (Make It Feel Good)

The game needs to feel satisfying moment to moment.

| # | Task | Size | Notes |
|---|------|------|-------|
| 5.8.1 | **Enemy death effects** — pop, burst, particles, resource drop VFX. Deaths should feel crunchy. Currently enemies just disappear. | M | VfxFactory exists, needs death burst |
| 5.8.2 | **Tower fire VFX** — muzzle flash, projectile trails, impact sparks. Each tower type should have distinct fire visuals. | M | Some VFX exist but are minimal |
| 5.8.3 | **Screen shake** — on big hits, boss damage, Spire damage, wave complete. Calibrate intensity by event severity. | S | TDCamera has Shake method |
| 5.8.4 | **Signal chain cascade visual** — when a sensor fires and the signal propagates through 3+ nodes, light them up in sequence. The "oh that chain just fired perfectly" moment. | M | Signal propagation exists, visual feedback is minimal |
| 5.8.5 | **Wave start/complete fanfare** — visual + audio beat marking wave transitions. Build tension before wave start, satisfaction after clear. | S | |
| 5.8.6 | **Extraction number animation** — the extraction counter should pulse, glow, or grow when it increases. Make the number feel alive. | S | Currently just updates text |
| 5.8.7 | **Damage numbers** — floating damage text on enemies showing hit values. DamageNumber.cs exists and is implemented. Verify it's working and visible. | S | VFX/DamageNumber.cs exists |

---

## 5.9 — Difficulty & Balance (Make It Fair)

| # | Task | Size | Notes |
|---|------|------|-------|
| 5.9.1 | **Full playtest P1** — play through all 20 waves, note every moment that feels too easy, too hard, boring, or unfair. | L | Human task |
| 5.9.2 | **Starting resources balance** — is 90 too much? Too little? Can you build a functional defense before wave 1? | S | Constants.VINE_STARTING_RESOURCES |
| 5.9.3 | **Tower cost curve** — DamageTower 15 vs Extender 3. Is the ratio right? Use F12 Node Balance to tune. | S | |
| 5.9.4 | **Extraction curve tuning** — use the F12 Extraction Curve Tuner to find the sweet spot. Wave 15 should feel like a massive payoff. | M | Tool exists |
| 5.9.5 | **Enemy HP scaling** — does the difficulty curve feel smooth or spiky? Use DifficultyScaler JSON to adjust. | M | |
| 5.9.6 | **Perk balance** — are perks meaningful choices or obvious picks? Add 5-10 new perks that change playstyle. Current 13 need review. | M | VinePerkData.cs |

---

## 5.10 — First 60 Seconds (The Hook)

If the first minute isn't compelling, nothing else matters.

| # | Task | Size | Notes |
|---|------|------|-------|
| 5.10.1 | **Immediate pressure** — enemies arrive fast. No long build phase before action. The player should feel urgency within 15 seconds of placing the mining rig. | S | Wave 1 timing in P1.json |
| 5.10.2 | **Instant feedback** — first tower placed, first enemy killed, first resource earned. Each of these needs a satisfying audiovisual hit. | M | Ties into VFX + SFX work |
| 5.10.3 | **First perk at wave 5** — milestone is set. Make the wave 5 perk feel like a reward that changes how you play the next 5 waves. | S | Already wired, needs perk quality |
| 5.10.4 | **Tutorial hints** — "Place mining rig" → "Build towers" → "Space to start wave." Only on first run. Track with MetaPerkSave flag. | M | UX15, not yet built |
| 5.10.5 | **Score visible immediately** — extraction counter should be prominent from wave 1. The player should immediately understand "bigger number = better run." | S | Already in HUD, verify visibility |

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
