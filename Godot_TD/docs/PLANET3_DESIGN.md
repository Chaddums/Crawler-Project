# Planet 3: Fathom — Design Document

*The liquid planet. No solid ground. Everything is gas, liquid, or particles.*

---

## Identity

**Name:** Fathom
**Tagline:** "Nothing here holds still."
**Enemy AI:** Military — scouts, flanks, adaptive routing
**Difficulty:** Hardest planet. The map itself is unstable.

**Mood:** Vast, cold, alien, alive. You're building on the surface of something that doesn't want you there. The ground ripples. The walls melt. The fog breathes. Everything you build is temporary — the question is whether it lasts long enough.

**Core contrast:**
| | Grid Prime | Scrapyard | Fathom |
|---|---|---|---|
| Surface | Flat dark plane | Rough terrain | Liquid surface |
| Walls | Digital barriers | Metal/junk piles | Ice/crystal formations |
| Props | Containers, antennas | Debris, scaffolding | Frozen geysers, coral growths, light columns |
| Fog | Minimal | Dust/haze | Dense volumetric nebula |
| Material feel | Emissive, flat | Rough, corroded | Translucent, refractive, alive |
| Sound | Clean digital hum | Industrial grind | Deep pressure, whale-song echoes |
| Palette | Blue-black + cyan | Browns + orange | Blues, purples, greens, reds |

---

## Color Palette

Grounded in real nebula color science (Hubble SHO palette):

| Role | Color | Hex | Source |
|---|---|---|---|
| Ground liquid (base) | Deep indigo-black | `#0A0818` | Deep ocean abyss |
| Ground liquid (highlight) | Dark purple-blue | `#1A0E2E` | Bioluminescent undercurrent |
| Wall ice (body) | Pale blue-white | `#B8D4E8` | Europa surface ice |
| Wall ice (crack veins) | Red-brown | `#8B3A2A` | Europa chaos terrain salt deposits |
| Fog (near) | Teal-green | `#0A6B5C` | Oxygen III emission |
| Fog (far) | Deep purple | `#2A0845` | Hydrogen-alpha absorption |
| Enemy Scavenger | Bright green | `#15CC55` | Bioluminescent jellyfish |
| Enemy Brute | Deep crimson | `#8B1515` | Sulfur emission |
| Enemy Ghost | Violet | `#9B30FF` | Ionized helium |
| Enemy Swarm | Yellow-green | `#AADD22` | Plankton bloom |
| Projectile | Hot magenta | `#FF1493` | Hydrogen-beta recombination |
| Signal pulse | Cyan-white | `#88FFEE` | Enceladus geyser plume |
| Death burst | Green flash | `#00FF88` | Bioluminescent death flare |
| Entry marker | Warm amber | `#FFAA22` | Thermal vent glow |
| Exit marker | Cold white | `#E8F0FF` | Ice core |
| Main light | Cool blue-white | `#C0D8FF` | Reflected starlight on ice |
| Fill light | Warm purple | `#6B3FA0` | Nebula backlight |

**Rule:** No pure white. No pure black. Everything has color. Even "dark" is purple-black. Even "light" is blue-white.

---

## Terrain System — Every Lever, Reimagined

### Ground (was: flat plane / rough terrain)

**Implementation:** Animated liquid surface shader. NOT a static plane. The ground subtly undulates with slow sine waves. Color shifts between deep indigo and purple-blue based on a noise pattern. Faint caustic light patterns project upward from below.

**Heightmap profile:** `TerrainProfile.Tidal` (new) — Low-amplitude rolling waves. No sharp peaks. Height values represent wave crests and troughs. The map feels like it's floating on something.

**Heightmap parameters:**
- Amplitude: 0.8f (lower than other planets — liquid is flatter)
- Frequency: 0.04f (broader features — ocean swells, not rocky terrain)
- Animation: Height values shift slowly over time (±0.1f, 8s cycle) — the ground breathes

**Ground shader uniforms:**
- `liquid_color_deep`: Deep indigo-black
- `liquid_color_shallow`: Purple-blue
- `caustic_intensity`: 0.3 (faint light patterns from below)
- `wave_speed`: 0.15 (slow undulation)
- `wave_amplitude`: 0.05 (subtle surface motion)
- `bioluminescence_noise`: Scrolling noise texture, green-blue glow spots that drift

**No grid lines.** Instead: faint bioluminescent particle drifts on the surface (like plankton currents). These serve the same readability function — showing cell boundaries — but feel organic.

**Dome ground conversion:** When BIT's dome covers terrain, the liquid surface freezes. Caustics stop. Color shifts to pale silver-blue. Faint frost crystal patterns appear. The dome is literally freezing the ocean surface to build on it.

---

### Walls (was: solid blocks / metal junk)

**Implementation:** Ice/crystal formations that grow up from the frozen liquid surface. Translucent. Light passes through them. Internal crack veins glow faintly (Europa chaos terrain pattern). They look like they froze mid-eruption.

**5 wall variants (matching existing system):**

| # | Name | Geometry | Notes |
|---|---|---|---|
| 1 | Ice Spike | Tapered cone, tilted ±12° | Like a stalagmite of frozen liquid. Translucent blue-white. |
| 2 | Crystal Cluster | 3-5 angled boxes, overlapping | Faceted ice crystals growing in a group. Each shard slightly different scale/angle. |
| 3 | Frozen Geyser | Cylinder base + spray top (inverted cone) | A geyser that froze mid-eruption. Hollow center. Ice spray at top. |
| 4 | Pressure Ridge | Long box, angled upward from one edge | Like tectonic ice pushed up by pressure from below. Europa-style ridge. |
| 5 | Ice Shelf | Flat-topped box, thick, rough edges | A chunk of frozen surface thrust upward. Flat top could almost be buildable. |

**Wall material:**
- Base: Translucent blue-white (albedo alpha ~0.7)
- Subsurface scattering approximation via emission: faint blue glow from within
- Crack veins: Red-brown emission (Europa salt deposits), mapped via noise texture
- Fresnel rim: Brighter at edges (ice catches light at angles)
- No outlines (Tron thing). No rust (Scrapyard thing). The translucency IS the visual identity.

**Frozen bubble detail:** Internal bubble pattern (Abraham Lake methane bubbles) visible through translucent ice. Achieved via a scrolling 3D noise in the shader. Suggests something alive beneath the frozen surface.

---

### Elevated (was: mesa / stepped formation / spire)

**Implementation:** Frozen liquid pillars — columns of ice that rise above the surface. Enemies can't cross. Towers get +range from the vantage point. They look like the liquid flash-froze while erupting upward.

**4 elevated variants:**

| # | Name | Geometry | Notes |
|---|---|---|---|
| 1 | Frozen Plume | Tall tapered cylinder, smooth | A column of liquid that froze mid-rise. Slight taper at top. |
| 2 | Ice Terrace | 2-3 stacked discs of decreasing radius | Layered frozen tiers. Like a frozen wedding cake. |
| 3 | Crystal Throne | Flat-topped prism with angled crystal shards around base | Premium tower position. Crystals frame the platform. |
| 4 | Geyser Column | Tall cylinder with particle emission at top | Still venting — faint particle spray from the peak. Active, not fully frozen. |

**Material:** Same as walls but with more emission — elevated positions glow brighter because they're newer ice (recently frozen). More blue, less red-brown veining.

---

### Channel (was: recessed trench)

**Implementation:** Current streams — visible liquid flow channels where the surface hasn't fully frozen. The liquid moves faster here. Enemies walking through channels get a speed boost (same 0.8x pathfinding cost) because they're riding the current.

**Visual:** The ground shader in channel cells has:
- Higher wave amplitude (the liquid is more active here)
- Visible flow direction (scrolling noise texture, directional)
- Brighter bioluminescence (more life in the current)
- No ice crust (the channel is liquid, not frozen)
- Faint mist rising from the surface (temperature differential)

**Design intent:** Channels on Fathom are current streams. Enemies prefer them because they move faster. Player must decide: block the current (place towers across it) or let enemies take the fast route into a kill zone.

---

### DataStream (was: animated glowing track)

**Implementation:** Thermal vents — cracks in the frozen surface where superheated liquid erupts. Enemies move 50% faster through them (same 0.5x pathfinding cost) but the heat also damages anything nearby. DataStreams on Fathom are risk/reward: fast path, but hostile.

**Visual:**
- Crack in the ice surface, glowing amber/red from below
- Rising particle steam (amber, flickering)
- Pulsing light (brighter during wave phase, dimmer during build)
- Ice edges around the crack glow orange from heat
- Scrolling shader: rising heat distortion (same slot as Tron's scrolling cyan lines)

**Design twist:** On Tron, DataStreams are neutral fast lanes. On Fathom, thermal vents are environmental hazards that ALSO speed up enemies. The speed benefit comes with danger. This ties into the 5.1.2 environmental hazard design — thermal vents are Fathom's native hazard type.

---

### Props (was: containers, generators, barrels, antennas, rubble, pipes)

**"Less physical objects"** — Fathom's props are not manufactured things. They're natural phenomena frozen in place. Each serves the same gameplay role (non-walkable decoration, visual variety) but feels organic.

**6 prop variants:**

| # | Name | Replaces | Geometry | Notes |
|---|---|---|---|---|
| 1 | Frozen Geyser Vent | Container | Cylinder + cone spray at top | A small geyser frozen mid-burst. Steam particles still rising. |
| 2 | Coral Growth | Generator | Branching cylinders (3-5 arms) | Alien coral frozen in ice. Bioluminescent tips glow green. |
| 3 | Bubble Column | Barrel Stack | 2-4 stacked spheres, translucent | Frozen methane bubbles (Abraham Lake). Various sizes. |
| 4 | Light Pillar | Antenna | Tall thin cylinder, bright emission at top | A column of bioluminescent particles frozen in ice. Glows upward. |
| 5 | Ice Debris | Rubble Pile | 3-5 angular shards, scattered | Shattered ice chunks. Like something broke through from below. |
| 6 | Crystal Arch | Pipe Cluster | 2-3 curved cylinders forming an arch | Frozen liquid that arced between two points before freezing. |

**Prop material:** Matches wall material (translucent ice) but with per-prop tinting:
- Coral growths: green-blue bioluminescent tips
- Light pillars: bright white-blue emission
- Bubble columns: higher transparency, visible internal spheres
- Ice debris: more opaque, rougher (older ice, weathered)

---

### Entry Regions (was: edge spawn areas behind shield walls)

**Implementation:** Whirlpool zones — areas where the frozen surface is thinner and the liquid below is churning. When a shield wall "breaks," the ice cracks open and enemies surge out of the liquid below. They're not walking in from off-screen — they're emerging from the deep.

**Visual for inactive entry:**
- Thin ice surface (higher transparency than normal ground)
- Visible churning liquid beneath (faster wave animation)
- Faint rumble (audio cue)
- Occasional bubble bursts from below (particle effect)

**Visual for shield wall on entry:**
- Ice dam — thick frozen barrier holding back the liquid pressure
- Visible cracks growing over time (as HP decreases)
- Liquid seeping through cracks (particle drips)
- Pressure groaning (audio)

**Shield wall break sequence:**
1. Cracks spiderweb across the dam
2. Liquid bursts through (particle explosion — blue-purple splash)
3. Ice shards scatter (same 12-shard system, but ice-colored)
4. The entry zone surface becomes liquid (unfrozen) permanently
5. Enemies surface from below (emerge animation: rise from liquid)

**Shield wall material (replaces energy barrier):**
- NOT an energy field (that's Tron). NOT amber metal (that's Scrapyard).
- Thick translucent ice dam. Blue-white body. Crack veins that glow red-amber.
- Pulse animation: ice creaks and shifts (slower pulse than energy fields — 0.8f speed)
- Critical state (<25% HP): cracks widen, liquid visibly pushing through, red-amber glow intensifies

---

### Exit / Spire

**Implementation:** The Spire on Fathom is a massive frozen crystal formation at the map center. The deepest, oldest ice on the planet. It's what the enemies are trying to reach — not to destroy a building, but to crack open the ice and release what's beneath.

**Visual:** Tall crystal cluster (like elevated variant 3 but 3x scale). Multiple faceted spires. Internal glow (warm white). Base embedded in the frozen surface. Particle wisps orbiting the peak.

**When Spire takes damage:** Cracks appear. Internal glow shifts from white to red. Ice shards chip off (particles). The implication: whatever's sealed inside is getting closer to breaking free.

---

## Atmosphere & Environment

### Fog System

Fathom uses Godot's volumetric fog heavily. Three layers:

| Layer | Height | Color | Density | Purpose |
|---|---|---|---|---|
| Surface mist | 0-1f | Teal-green | 0.04 | Liquid evaporation. Clings to ground. Obscures feet. |
| Mid fog | 1-5f | Purple-blue | 0.02 | Reduces visibility at distance. Creates mystery. |
| High haze | 5-15f | Deep purple | 0.01 | Sky boundary. Nebula transition. |

**Fog color shifts during gameplay:**
- Build phase: Cool teal-green (calm, the liquid is still)
- Wave phase: Shifts toward purple-red (the liquid is agitated, enemies are disturbing it)
- Boss/commander present: Deep red pulses in the fog (something massive is moving below)

### Skybox

Nebula skybox — no visible ground horizon, no sky. The planet has no atmosphere boundary. The liquid surface fades into gas which fades into nebula. Colors: deep purple base, teal-green oxygen emission bands, red-pink hydrogen clouds. Stars visible through thin patches.

**Not a procedural shader initially.** Use a baked HDRI nebula skybox. Faster, cheaper, and the player barely looks up in a TD game. Can upgrade to procedural later.

### Ambient Particles

Constant particle systems floating in the air:

| Particle | Count | Size | Color | Behavior |
|---|---|---|---|---|
| Bioluminescent motes | 200 | 0.02-0.06f | Green-blue, pulsing | Slow drift upward, fade in/out |
| Ice crystals | 80 | 0.01-0.03f | White-blue | Slow fall, slight horizontal drift |
| Bubble wisps | 40 | 0.04-0.12f | Purple, translucent | Rise from surface, pop after 2-3s |
| Deep glow | 20 | 0.1-0.3f | Warm amber | Visible through liquid surface, drifts below ground plane |

**Performance note:** Use GPUParticles3D with simple billboard quads. No physics. Cull beyond camera distance. Budget: <1ms frame time.

### Lighting

| Light | Type | Color | Energy | Notes |
|---|---|---|---|---|
| Main | Directional | Cool blue-white (#C0D8FF) | 0.6 | Low angle, long shadows across ice |
| Fill | Directional | Warm purple (#6B3FA0) | 0.3 | Opposite angle, fills shadows with color |
| Ambient | Environment | Deep blue (#0A1428) | 0.4 | Global baseline |
| Subsurface fake | OmniLight (below ground) | Teal-green | 0.2 | Placed below liquid surface, creates glow-from-below |

**Key visual rule:** Shadows on Fathom are purple, not black. Fill light ensures nothing is truly dark — the nebula illuminates everything from all angles, just dimly.

---

## Enemy AI: Military Doctrine

Fathom enemies behave like a coordinated military force. This is the planet's core difficulty mechanic — not harder stats, smarter enemies.

### Scout Phase (new behavior, Fathom-only)
- Waves 1-3: Enemies are purely scouts — small, fast, low HP
- Scouts don't attack the Spire. They path through your maze and report the layout
- After scouts complete a pass, the main force attacks your weakest flank
- **Implementation:** Scouts path to Spire, then path BACK to entry. Once they complete the round trip, the next surge targets the entry point closest to the path with fewest towers

### Flanking (new behavior, Fathom-only)
- When 2+ entry points are active, enemies split forces
- Main force at one entry (obvious threat), small flanking squad at another (the real danger)
- Flanking squad has higher speed, lower HP — designed to slip past while you're focused on the main wave
- Flank entry chosen by: fewest towers within 3 cells of the entry region

### Adaptive Routing (new behavior, Fathom-only)
- After wave 5, enemies recalculate optimal path every 5 seconds (not just at spawn)
- If the player builds new towers during a wave, enemies reroute in real time
- Enemies prefer routes that avoid tower firing arcs, not just shortest path
- **Implementation:** Modified A* cost that adds penalty for cells within tower range

### Faction Behavior on Fathom

| Faction | Fathom Behavior | How It Differs From Other Planets |
|---|---|---|
| Scavenger | **Probe and scatter** — enters in a group, scatters when first tower fires. Each unit picks a different path. Regroups at the exit. | On Tron: confused by gates. On Scrapyard: squad movement. On Fathom: scatter-on-contact. |
| Brute | **Siege engineer** — targets ice walls specifically. When a Brute reaches a wall, it starts melting it (wall HP decreases). If the wall breaks, a new path opens. | On Tron: breaks switch logic. On Scrapyard: bulldozes. On Fathom: melts ice walls. |
| Ghost | **Deep diver** — phases below the liquid surface. Invisible while submerged. Surfaces near the Spire. Towers can only target Ghosts while they're above the surface. | On Tron: phases through gates. On Scrapyard: phases through walls. On Fathom: goes underwater. |
| Swarm | **Emerging tide** — spawns in massive numbers from the liquid surface across a wide area, not from a single entry point. Like a tide of creatures surfacing everywhere. | On Tron: single-file line. On Scrapyard: pack movement. On Fathom: wide-area emergence. |

---

## Environmental Hazards (Fathom-Specific)

Every hazard on Fathom comes from the liquid/gas/ice. No lava, no electric plates, no manufactured danger.

| Hazard | Terrain Cell | Effect | Visual |
|---|---|---|---|
| **Thermal Vent** | DataStream | Enemies +50% speed. Towers within 1 cell take 2 DPS chip damage. | Crack in ice, amber glow, steam particles. |
| **Thin Ice** | New type or Channel variant | Enemies crossing have 15% chance to fall through (1s stun + damage). Towers placed here have 20% less HP. | Higher transparency, visible churning below, creak SFX. |
| **Geyser Eruption** | Prop + timed event | Every 15s, erupts and pushes all units (enemies AND projectiles) 2 cells outward. Creates a no-build dead zone. | Intermittent steam bursts, rumble audio, ice ring around vent. |
| **Pressure Crack** | Wall variant (breakable) | Enemies can damage this wall. If it breaks, a new path opens AND the adjacent cells flood (become liquid, impassable for 10s). | Visible crack line, amber glow, dripping particles. |
| **Fog Bank** | Area effect (3x3) | Units inside have -50% vision radius (towers fire later, enemies detected later). Drifts slowly across the map. | Dense teal-green fog volume, moves 0.5 cells/minute. |
| **Undertow** | Current stream path | Enemies in the current are pulled toward a direction. Can pull them PAST tower kill zones faster, or pull them INTO towers depending on placement. | Visible flow direction arrows in the liquid, blue-green streaks. |

### Hazard Placement Philosophy

Fathom hazards are **not static obstacles to avoid**. They're dynamic forces to harness or work around:

- Thermal vents speed up enemies BUT also damage your towers — do you build near the fast lane or stay safe?
- Geyser eruptions push enemies away — bad if they push enemies past your towers, good if they push enemies back into your kill zone
- Undertow currents pull enemies in a direction — build towers downstream to catch them, or upstream to slow them?
- Fog banks drift — a tower that's clear now might be fogged in 2 minutes

The map is alive. Your static defenses interact with dynamic terrain.

---

## Map Design Principles (Fathom-Specific)

### Less Physical, More Spatial

Fathom maps use **fewer walls and more open liquid**. The maze isn't built from walls — it's built from the player's towers, the terrain currents, and the ice formations.

**Grid Prime:** Dense walls create corridors. Player optimizes tower placement within a maze.
**Scrapyard:** Scattered debris creates loose chokepoints. Player fills gaps with towers.
**Fathom:** Open liquid surface with sparse ice formations. Player CREATES the maze entirely from their own towers and the natural currents.

This means:
- Fewer VineCellType.Wall cells per map (15-25% coverage vs 30-40% on other planets)
- More open VineCellType.Empty cells (the liquid surface)
- Ice walls are placed at strategic points — natural chokepoints, not corridors
- The player must spend more resources on towers to create pathing
- Current streams (channels) and thermal vents (DataStreams) create soft routing that the player works with or against

### Map Expansion on Fathom

As shield walls break, the map doesn't just get bigger — the liquid gets more volatile:

| Stage | Timing | Map State |
|---|---|---|
| Frozen Core | Waves 1-4 | Small arena. Surface is mostly frozen. 1 entry (West). Stable. |
| First Thaw | Wave 5 milestone | North wall breaks. Surface starts undulating more. First fog banks appear. |
| Rising Tide | Wave 10 milestone | East wall breaks. Thermal vents activate. Current streams begin flowing. The liquid is fighting the ice. |
| Deep Breach | Wave 15 milestone | South wall breaks. Full map. Geyser eruptions begin. Undertow currents strengthen. The ocean is winning. |
| The Abyss | Wave 20+ | All entries open. Fog banks everywhere. The ice is cracking. Even the Spire's platform is unstable. |

**Key:** The map doesn't just expand — the environment escalates. Early Fathom is a calm frozen pond. Late Fathom is a storm at sea.

### 5 Map Layouts

| Map | Size | Identity | Key Feature |
|---|---|---|---|
| **Shallows** | Small-medium | Tutorial map | Simple layout. Wide frozen platform. Single current stream through center. 2 entry points. Teaches Fathom basics. |
| **Rift** | Medium | Linear gauntlet | Long narrow frozen bridge over a chasm of liquid. Enemies come from both ends. Nowhere to retreat. Pressure ridge walls along edges. |
| **Caldera** | Large | Central depression | Frozen crater. Spire at the center low point. Enemies descend from the rim. Ice walls ring the crater. Current streams spiral inward. |
| **Archipelago** | Large | Scattered platforms | Multiple frozen platforms connected by thin ice bridges. Each platform is a mini-arena. Enemies can cross bridges OR swim through the liquid between (slower but bypasses bridges). |
| **Maelstrom** | Extra large | Endgame chaos | Massive map. Undertow currents spiral toward center. Multiple geyser eruptions. Fog banks everywhere. 4 entry points. The most hostile map in the game. |

---

## Conversion Dome on Fathom

When BIT's dome covers terrain on Fathom, the liquid freezes solid:

| Outside Dome | Inside Dome |
|---|---|
| Undulating liquid surface | Frozen flat ice (BIT silver-blue) |
| Bioluminescent motes | Frost crystal particles |
| Teal-green fog | Clear (fog suppressed inside dome) |
| Ambient bubble wisps | Frozen in place (static, embedded in ice) |
| Warm amber sub-glow | Cool white sub-glow |

**Dome boundary VFX:**
- Ice crystallization ring at the dome edge (instead of fog rings)
- Visible freeze-line where liquid meets ice
- Cracking sound when dome expands (ice forming)
- Thawing sound when dome shrinks (ice retreating)

**Takeover structures (BIT's auto-built structures inside dome):**
- Ice pylons (instead of antennas)
- Frozen data nodes (instead of power nodes)
- Crystal lattice (instead of small dome)
- Frost beacon (instead of pylon) — emits cold particles upward

---

## Wave Data Design (P3.json)

### Pacing Philosophy

Fathom waves are **slower to start, faster to escalate**. The military AI needs time to scout before it commits. But once it commits, the pressure is relentless.

| Wave Range | Feel | Composition |
|---|---|---|
| 1-3 | Recon | Small scout groups. 1 entry point. Low HP, high speed. Testing your layout. |
| 4-6 | Probing | Mixed factions. Scouts + first Brutes. 2 entry points. First flanking attempts. |
| 7-10 | Assault | Full military doctrine. Coordinated multi-entry attacks. First commander at wave 8. Fog banks active. |
| 11-15 | Siege | Brutes targeting ice walls. Ghost deep-diver squads. Swarm tide emergences. Thermal vents active. Second commander at wave 12. |
| 16-20 | Overwhelming | All systems active. Adaptive routing. 4 entry points. Continuous pressure. Boss at wave 18. The ocean is winning. |

### Milestone Schedule (P3 milestones.json)

| Wave | Event | Effect |
|---|---|---|
| 3 | First Thaw | North entry opens. Surface agitation increases. |
| 5 | Perk Select | "MILESTONE: Deep Protocol" |
| 8 | Rising Tide | East entry opens. Thermal vents activate. First commander. |
| 10 | Perk Select | "MILESTONE: Pressure Adaptation" |
| 13 | Deep Breach | South entry opens. Geyser eruptions begin. |
| 15 | Perk Select | "MILESTONE: Abyss Walker" |
| 18 | Boss Wave | Fathom boss encounter. |
| 20 | Perk Select | "MILESTONE: The Deep Remembers" |

**Note:** Entry points open faster on Fathom than Grid Prime (wave 3/8/13 vs 5/10/15). The player is under multi-directional pressure sooner. This is the planet's difficulty lever.

---

## Difficulty Scaling (Fathom-Specific)

Fathom uses the same DifficultyScaler but with different JSON parameters:

| Parameter | Grid Prime | Scrapyard | Fathom |
|---|---|---|---|
| HP scale/min | 0.02 | 0.025 | 0.015 |
| Damage scale/min | 0.015 | 0.018 | 0.02 |
| Speed scale/min | 0.01 | 0.012 | 0.015 |
| Armor scale/min | 0.005 | 0.006 | 0.003 |

**Fathom enemies are faster and hit harder, but have less HP and armor.** They're glass cannons. The difficulty comes from their AI (flanking, adaptive routing) and the environment (fog, currents, geysers), not from spongy HP pools. Kills feel quick and satisfying. But if you miss them, they hit hard.

---

## Audio Identity

| Event | Sound Character | Notes |
|---|---|---|
| Ambient | Deep pressure hum, distant whale-song echoes, ice creak | Constant. The planet is alive. |
| Wave start | Rising water rush, pressure building | Liquid churning, not a digital countdown |
| Wave clear | Ice crystallization snap, pressure release | The surface refreezes. Calm returns briefly. |
| Enemy spawn | Bubbling, surfacing splash | They emerge FROM the liquid |
| Enemy death | Liquid splash + crystallization shatter | They dissolve back into the ocean |
| Tower fire | Sharp crack (ice breaking) or deep pulse (pressure wave) | Per tower type — cryo=crack, damage=pulse |
| Shield wall break | Ice dam rupture, rushing liquid, debris scatter | Most dramatic sound on the planet |
| Geyser eruption | Pressurized steam blast, rumble | Periodic ambient hazard |
| Fog bank drift | Low wind moan, muffled sounds inside fog | Fog suppresses other SFX (audio ducking) |
| Boss approach | Deep sub-bass rumble, ice cracking from below | Something massive is surfacing |

---

## Boss Concept: The Leviathan

A massive entity that surfaces from below the liquid. Not a walking enemy — a creature that IS the terrain.

**Phase 1 (100%-60% HP): Circling**
- Visible as a shadow beneath the liquid surface, circling the map
- Periodically surfaces a tentacle/fin that sweeps across 3-4 cells, damaging towers
- Player must position towers to hit the exposed part during sweeps

**Phase 2 (60%-30% HP): Surfacing**
- Partially surfaces near the Spire. Head/body visible.
- Creates a massive wave that pushes all enemies toward the Spire (friendly fire concern for the boss)
- Spawns smaller creatures from its body (Swarm-type, continuous)
- Geyser eruptions become more frequent across the map

**Phase 3 (30%-0% HP: Thrashing)**
- Fully surfaced. Destroys ice walls by thrashing. Map terrain changes in real time.
- Undertow currents all pull toward the boss (pulls enemies AND player toward it)
- The frozen surface is cracking everywhere. Towers on thin ice start taking damage.
- Defeating it: the liquid flash-freezes. Everything goes still. Then the extraction score tallies.

---

## Technical Implementation Notes

### New Files Needed
- `FathomPlanetTheme.cs` — PlanetTheme subclass (40+ colors, 5 material factories)
- `FathomEnvironment.cs` — Static helper (liquid shader setup, ice materials, coral/geyser props)
- `Data/Waves/P3.json` — 20 waves of military-doctrine wave data
- `Data/milestones.json` update — Planet 3 milestone schedule
- `Data/difficulty_scaling.json` update — Planet 3 scaling parameters
- `Data/Levels/fathom_*.json` — 5 map layouts

### Shader Work
- Liquid surface shader (animated, caustics, bioluminescence)
- Ice/crystal material shader (translucent, Fresnel, crack veins, frozen bubbles)
- Thermal vent shader (crack glow, heat distortion, steam)

### Existing System Modifications
- `GameManager.cs` — Add `case 3 =>` for FathomPlanetTheme
- `VineGrid.cs` — Add `IsFathom` conditional for liquid ground, ice walls, new prop variants
- `VineEnemy.cs` — Add Fathom-specific faction behaviors (scout/flank/adaptive)
- `ShieldWall.cs` — Ice dam visual variant
- `ConversionDome.cs` — Freeze-the-liquid dome conversion
- `VineMapLayouts.cs` — 5 new Fathom layouts (or JSON-only if Map Designer is built)
- Editor dropdowns — Add Planet 3 to pickers

### Asset Shopping List
See purchasable assets research. Priority buys:
1. Dragonforge Ice Shaders ($10, Godot native, CC0)
2. Free GodotShaders.com crystal/water/ice shaders ($0)
3. Boujie Water Shader from Godot Asset Library ($0)
4. Atmosphere Shader from Godot Asset Library ($0)
5. Synty Alpine Mountain Biome ($28, style-consistent)

---

## Design Checklist

Every system must be verified on Fathom before marking done:

```
[ ] FathomPlanetTheme.cs created with full color palette
[ ] FathomEnvironment.cs created with material factories
[ ] Liquid surface shader working (animated, caustics, bioluminescence)
[ ] Ice wall shader working (translucent, Fresnel, crack veins)
[ ] All 5 wall variants rendering correctly
[ ] All 4 elevated variants rendering correctly
[ ] All 6 prop variants rendering correctly
[ ] Channel (current stream) visuals working
[ ] DataStream (thermal vent) visuals working
[ ] Shield wall ice dam variant working
[ ] Shield wall break VFX (ice shatter + liquid burst)
[ ] Conversion dome freeze effect working
[ ] Volumetric fog 3-layer system working
[ ] Ambient particles (motes, crystals, bubbles, deep glow)
[ ] Skybox (nebula HDRI or procedural)
[ ] P3.json wave data authored (20 waves)
[ ] milestones.json updated for Planet 3
[ ] difficulty_scaling.json updated for Planet 3
[ ] 5 map layouts created (Shallows, Rift, Caldera, Archipelago, Maelstrom)
[ ] Scout AI behavior implemented
[ ] Flanking behavior implemented
[ ] Adaptive routing implemented
[ ] Per-faction Fathom behaviors implemented
[ ] Thermal vent hazard working
[ ] Thin ice hazard working
[ ] Geyser eruption hazard working
[ ] Fog bank drift working
[ ] Undertow current working
[ ] Entry point emerge-from-liquid enemy animation
[ ] Audio identity (at least ambient + spawn + death + wave start/end)
[ ] Leviathan boss encounter
[ ] F12 editor planet picker updated
[ ] Full 20-wave playtest on at least 2 Fathom maps
```

---

## References

| Reference | What To Borrow |
|---|---|
| Subnautica: Below Zero | Ice spires, crystal caves, bioluminescent color pops |
| Metroid Prime 3 (Phaaze) | Pulsing liquid ground, energy tendrils, terrain that feels alive |
| Destiny 2 (Titan) | Methane ocean waves, infrastructure above alien liquid |
| Outer Wilds (Giant's Deep) | Ocean planet atmosphere, cyclones, layered density |
| Europa (NASA) | Ice crack patterns, red-brown salt deposits, chaos terrain |
| Enceladus (NASA) | Cryovolcanic geysers, tiger stripe fractures |
| Abraham Lake (Alberta) | Frozen methane bubbles inside transparent ice |
| Nebula Hubble Palette | Color science: hydrogen red/blue, oxygen teal, sulfur deep red |
| Deep-sea bioluminescence | Pulsing blue-green ambient particles |
| Abzu | Underwater illumination, god rays, depth color grading |
