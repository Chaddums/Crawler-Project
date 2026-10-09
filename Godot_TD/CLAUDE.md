# Vine Logic TD — Project Reference

*Single source of truth for Claude instances and project context. Last updated: 2026-10-08.*

---

## What Is This Game?

A programmable-logic tower defense where your vine network IS the maze. Sensors detect enemies, fire signals along connections to turrets. You are BIT — an ancient AI cleanup script deployed by AXIS, a dismissive corporation that acquired BIT without understanding what it holds. You always lose. The question is how much you extract before you fall.

**One-liner:** "Mine everything you can before they take it all."

**Core reframe:** Not survival — extraction. Not pass/fail — optimization. Resources scale exponentially with wave depth. Every run extracts something. No run is wasted.

**Status:** Continuous extraction loop implemented. Core systems (vine network, mining building, BIT, wave/surge spawning, continuous wave curve, extraction scaling, milestone events, 2 planet themes) are solid. Floor layer removed. S4 meta layer (territory unlocks, suits, boss runs) implemented — data persistence, game flow, and UI screens built. Between-runs loop complete: debrief screen (resource transfer + suit capture) → meta hub (command center with territory/suits/relics/start-run nav) → sub-screens → next run. Scene transitions use TransitionManager (fade in/out). Ascendants fight as bosses (below); the narrative rewrite and node unlock shop are greenfield.

Note: The vine logic circuit system is one possible defense implementation, not core to the game. Level 1 towers should work by default without signal chains. Signal chains are an advanced/optional system.

**Canonical hierarchy:**
```
Run > Planet (1-3) > Wave (1-N continuous) > Surge (dynamic) > Enemy
```

**Address format:** `P#-W#-S#` in all logs, comments, data.

---

## The Core Strategic Axis

```
Mine Resources → fund vine nodes, infrastructure → your NETWORK carries you
Mine Materials → fund character abilities, upgrades → your CHARACTER carries you
```

Mining Building toggles between modes. Cannot do both simultaneously. This is THE decision.

---

## Conventions

- **Engine:** Godot 4.6 C#, namespace `JunkyardTD`
- **Stitch CEF UI** — primary screens use Stitch HTML rendered via godot-cef (see `docs/STITCH_WORKFLOW.md`). Code-built fallback for when CEF is unavailable. Some screens still code-built CanvasLayer pending CEF rewrite.
- **Singletons:** `ServiceLocator` for services, `GameEvents` static event bus
- **Constants** in `Constants.cs` — no magic numbers in code
- **All tuning in JSON** — never hardcode enemy counts, HP, timing
- **Colors:** `BitPalette.cs` for player/harvester/tower. `PlanetTheme.Current` for enemies/terrain
- **Node types** defined in `Enums.cs`, data in `VineNodeData.cs`
- **Characters** (BIT, enemies) are rigged FBX driven by `CharacterAnimator`. Models without usable clips get a procedural gait/bob, deaths without a death clip get a procedural roll, skin offsets are normalised at load, and `AssetLibrary.GetFacingYawOffset` turns models not authored facing +Z. Check with the `anim` / `anim-sheets` suites.
- **Planet looks** live on `PlanetTheme` (`ConfigureEnvironment`, `ConfigureLights`, accents, converted-ground colour). Scrapyard = dusk (low orange sun, cyan player accent); Grid Prime = two-tone (cyan world, orange player, red/magenta enemies). Player-owned things (towers, converted decor) wear a fresnel rim in the player accent via `BitPalette.ApplyAccentRim` (a `MaterialOverlay`, so imported materials stay untouched).
- **Towers:** each buildable tower is built from `Data/Towers/{id}.json` (`TowerSheet`, `TowerLook` under `Scripts/VineLogic/Towers/`, keyed by `VineNodeData.Id`): an optional pedestal in the KB3D textures with a band in the tower's colour, kit models (fitted by footprint, height or scale, sub-meshes hidden by name) and shapes (box, cylinder, prism, sphere, torus, capsule) in surfaces `concrete`, `panel`, `trim`, `hazard`, `white`, `dark` (triplanar KB3D building textures), `copper`, `chrome`, `tar` or `glow`. Parts marked `aim` (or a kit model's `aimNodes`) turn toward what the tower shoots and scan slowly when idle; the barrel direction and tip are found from the meshes (snapped to the model's axes) unless the sheet gives `muzzle`; shots leave from `TowerLook.MuzzleGlobal`. `recoil` kicks the aiming parts back on each shot, `punch` parts shoot out (the Pneumatic Ram), `spin` parts turn. Wall parts with `face`/`link` show only without/with a wall next door on that side, so lines of walls join. A tower must fit its cell at its `restYaw`. The build ghost uses the same sheet. Healthy towers show no HP bar and no status pip. Other nodes use the kit model mapped in `VineNode.GetModelPathForNodeType`; `TowerMeshes` is the fallback if a sheet fails to load. Kit models (KB3D) are recentred on their footprint at load (`AssetLibrary.RecenterFootprint`; the GLBs keep props at their kit-layout positions, tens of units from origin). A placed node's origin is `NODE_ORIGIN_HEIGHT` above its cell's highest corner (plus `ELEVATED_PLATFORM_HEIGHT` on an elevated cell); models are offset down by it. Cells sloping more than `FOOTING_MIN_SLOPE`, and elevated cells, get a footing (`VineNode.AddFooting`) down to the lowest ground within `NODE_FOOTPRINT_MARGIN` of the cell. Tower models fit inside their cell (`fidelity` allows 0.05).
- **Ground:** `VineGrid.GetWorldHeight` returns the drawn surface (two triangles per cell, split from (x+1,z) to (x,z+1), and the outer apron), so anything placed with it sits on what the player sees. Field decor is seated on the lowest corner of its cell and stretched to keep its top (`SeatDecor`, after any terrain change); flat overlays (channel floor, data stream, pit) are terrain-following quads (`DrapedCellQuad`). Enemies snap to the ground every frame and keep their body circle (`_bodyRadius`, capped at 0.45 of a cell) out of solid cells (`StayOutOfSolids`); Ghosts are exempt. The Spire is seated on the lowest ground under its footprint (`VineHarvester.SeatOn`). A Spire's model, its optional platform (`model.base`, a Data/Towers sheet) and its autocannons (`autocannonConfig.sheet`, each a `TowerLook` on the platform's corners that tracks its own target) sit under one `SpireModel` root; the health bar sits just above the model. The Arcanist's shield is a hex-panelled dome (`ShieldDome`) that collapses with a flash when the shield breaks and fades back in on recharge.
- **Player mech progression:** BIT earns XP during a run (any kill, its own kills, cleared waves), levels up (health, damage, size, outline and glow, plus parts at set levels) and bolts on gear for every perk picked, all from `Data/Mechs/bit.json` (`MechSheet`, `MechProgression`, `MechAppearance` under `Scripts/VineLogic/Mech/`). Nothing carries between runs. Gear hangs off `BoneAttachment3D` sockets so it moves with the animation, and wears BIT's hull, glow or lit "metal" materials so it swaps at the dome edge and flashes with the body. Gear with a `muzzle` is a gun: shots leave from it in turn, with its own size, colour and speed. Every offered perk needs gear in the sheet (`mech/sheet/every_perk_has_gear`). A new mech is a new sheet plus its sockets; `Constants.PLAYER_MECH_ID` picks BIT's.
- **Tower stats:** a tower's `Damage` is damage per second. Each shot deals `GetEffectiveDamage(interval)` and the timer rearms with `GetEffectiveFireInterval(interval)` (`VineNode.Rearm`, which carries the overshoot so a fast tower keeps its rate), so damage and fire-rate bonuses (perks, the Overclock Relay's +25% to both, component slots, relics) reach every tower. Flak's damage is per target. Bands checked by `balance`: the Junk Turret is the best single-target buy per cost; Scatter, Tesla and Flak beat it on groups of five; nothing falls under 0.3x its single-target value or over 2.6x its group value.
- **Perks** are registered in `VinePerkData.cs`. Perks with a `Tower` change how that tower plays (Arc Conductor, Tar Pools, Hydraulic Stun, Cluster Shells, Piercing Rail, Relay Mesh, Saturation Fire; constants `PERK_*` in `Constants.cs`) and are drawn at 0.35 weight until that tower is built. They also change the tower: `perkParts` in its `Data/Towers` sheet pop onto every standing and new tower when picked (`TowerLook.ShowPerk`), and BIT bolts on matching gear. Stuns use `VineEnemy.ApplyStun` (bosses and commanders take `STUN_BOSS_FACTOR` of it).
- **Screens between runs:** the title's PLAY goes to the Command Center (home base; it used to go to planet select, so a new player never saw it until a run ended). The hub's Deploy card starts the campaign's next site (`TerritoryManager.Campaign`/`NextSite`, `MetaHubScreen.DeployNext`) and shows its region's progress pips; "Choose planet & site" goes the long way. The debrief says where the campaign stands (`DebriefScreen.CampaignLine`). The web pages (title, planet select, debrief, hub, relics, suits) watch godot-cef's `render_process_terminated` (`CefHelper.WatchCrash`) and reload once, then fall back to the code-built screen; each also sits on a dark backdrop (`CefHelper.AddBackdrop`, also the clear colour) and is checked 2.5 s after it opens (`CefHelper.WatchPaint`/`LooksBlank`): a page that never drew is made again once, then the code-built screen takes over (that was the grey screen after a run). The debrief and planet select take Enter and Esc. The territory screen falls back to planet 1's sites for a planet without any, draws its own backdrop (`GridBackdrop`) and offers "Play next". `RunGuide` is a getting-started checklist on the first runs (dismissed state in `user://ui.cfg`).
- **Effects:** every transient effect is pooled particles (`VfxParticles`, one per battle scene: a MultiMesh per kind, sparks, glows, smoke, debris, ground rings and lightning segments, simulated in C# and pushed as one buffer a frame; looks in `VfxLook`, shaders and textures generated in code). `VfxFactory` builds effects from them: impacts, directional muzzle flashes, explosions, tracers, Tesla arcs, tar splats, shoves, buff motes, energy bursts, deaths. An effect makes no nodes, meshes or materials; projectiles are one node with a particle trail. The Conversion Dome's motes and wisps are particles too (`ConversionDome.AmbientParticles`). `AutoFadeNode`, `DeathFragment` and `VfxCache` remain for the intro cinematic, and `VfxCache`'s shared meshes for the Spire autocannon's shells.
- **Settings** (`GameSettings`, `SettingsScreen`, from the title and the pause menu; `user://settings.cfg`, never written by tests): volumes (buses SFX, Music, Voice), fullscreen, VSync, frame cap, Graphics (Low/Balanced/High: the sun's shadow cascades 1/2/4 and the mesh LOD threshold 6/3/1 px; four cascades drew a big field five times over), screen shake, gunner mouse speed, damage numbers, route preview, BIT auto-fire. The battle fits the sun's shadow reach to the camera (`VineBattleScene.FitShadowsToCamera`).
- **Big moments and feedback:** `Celebration` puts a site secured, a region taken, an Ascendant down, a relic, perk points and strikes in the middle of the screen (queued, Layer 28). `ResourceFlyout` shows every drop: "+N" where it fell and chips that fly into the Resources counter, which pulses. `DamageNumbers` (pooled Label3D) sums hits on an enemy into a number every 0.35 s, shows hits on BIT in red, and carries the small tags over towers (what boosts them, ranks) and rallied enemies. BIT getting hurt reddens the screen edge (stronger for bigger hits, pulsing below 30%). The HUD's OBJECTIVE card says what secures the site and the region's progress (`GameManager.ObjectiveLine`, `TerritoryManager.RegionProgressLine`).
- **BIT's controls and attacks:** WASD is screen-relative (`TDCamera.ScreenToGround`, so it follows the camera's orbit). BIT naruto runs the moment it moves (the Run clip is played looping, `CharacterAnimator.PlayCustomLooping`) and stays in it through attacks and key changes. BIT shoots the nearest enemy in range by itself; holding the left mouse button (not while placing, not over UI) fires where the mouse points instead (`VinePlayer.FireAimed`: the first enemy near the line, out to range). `EffectiveDamage`, `EffectiveAttackSpeed`, `EffectiveRange` and `AbilityPower` are the base stats (which level-ups raise) times the Spire upgrades. Aimed shots reach `BIT_AIMED_RANGE` (44); the naruto run ramps to +`NarutoSpeedBonus` of move speed over `NARUTO_RAMP_TIME`. BIT heals: `BIT_REGEN_SHARE` of max health a second once unhit for `BIT_REGEN_DELAY` s, `BIT_DOCKED_REGEN_SHARE` inside the Spire, and Repair Pulse patches it by `ABILITY_REPAIR_BIT_SHARE`.
- **The Spire station** (`Scripts/VineLogic/Spire/SpireStation.cs`, tuning in `Data/spire_station.json`): within reach, F opens the Spire menu (`SpirePanel`, code-built): Spire upgrades (Guns, Plating, Reach) cost Resources, BIT upgrades (Weapon, Trigger, Reach, Core) cost Materials banked at the Spire, plus refill BIT's Materials and train BIT (banked Materials to XP). G climbs in: BIT is hidden and safe, straight into first person (gunner view), the left button fires the cannon (F or G climbs out). Spire Guns mounts a visible gun per level on a ring round the Spire (any role, `VineHarvester.SetExtraGuns`, each fires on its own), Plating adds armour plates round the base (`SetPlating`), Reach shows its new range on the ground. **Strikes** (`strikes` in the JSON): one-shot weapons bought as charges with Resources and fired with 1, 2, 3 where the mouse or crosshair points (else at the thickest knot of enemies): Orbital Lance (huge damage, small circle), Cluster Barrage (shells over an area), EMP Burst (stun, strips shields, breaks Empowerer tethers); damage grows a share a wave and follows the health ramp, and the HUD lists ready charges. Holding F (a tap opens the menu) repairs a damaged Spire with BIT's own Materials; it is never automatic, since BIT starts the run beside the Spire. Materials mode banks `dropShare` of every enemy drop at the Spire and mines `spireRateMult` times faster (before 2026-10 Materials mode only grew a number nothing spent). Right-click acts on a click's release (`ClickGuard`): right-drag turns the camera.
- **Gunner view:** climbing in starts in first person from the Spire's top (V toggles the map view) (`SpireStation.EnterGunnerView`): its own camera just past the model's top, the mouse captured and turning the view (`_Input`, so the hidden cursor's clicks never reach the HUD), a crosshair, and the cannon fires where it points (`GunnerAim`; at the sky, its reach straight ahead). A held trigger is dropped when the button isn't down (it kept firing after the menu closed); climbing out, a pause (`NotificationPaused`) or leaving the scene gives the mouse back.
- **The maze:** walkers follow a flow field (`VinePathfinder.BuildFlow`: Dijkstra out from the Spire with A*'s step costs, rebuilt with `Version` bumped on every placement, sale or terrain change), so the route is whatever the towers and walls leave open, never a beeline past them. They cut corners between flow cells when the straight line is clear (`FollowFlowFromHere`, string pulling); Ghosts and flyers ignore it. `PathPreview` draws the route as chevrons marching to the Spire (strong while building, faint during waves) with a "ROUTE N cells (+M from your maze)" label, and the build ghost previews the route as it would be with that cell built on (`PreviewPath`).
- **Ascendants** (`AscendantManager`, `Ascendant`, `Data/ascendants.json`): from `min_wave`, a cleared wave can roll one. It is announced at once and named on the wave card, then comes with the next wave as a boss `VineEnemy` (`VineWaveManager.SpawnAscendant`: its own model at `model_height` with a rim in its colour, a name tag, health growing `hp_per_wave` past `min_wave`) that walks the maze to the Spire; towers, BIT and the Spire's guns hit it. `friendly_delay` seconds later its rival, a friendly `Ascendant` in its own model, comes from the Spire's side, walks to it and fights it (the enemy hits back at full `damage`; towers and BIT take `tower_damage_share` of it). Killed: its `reward` drops, a perk point, a centre-screen moment naming who made the kill (`VineEnemy.HitSource`/`LastHitBy`: BIT, your towers, the Spire or the rival), the friendly stays `friendly_linger` s swatting stragglers then walks off, and after 10 runs the body lies there for BIT to inhabit (`AscendantInhabit`, F within 8 units). Through to the Spire: it takes `spire_damage_share` of the Spire's health and leaves. A fallen friendly costs nothing by itself (it used to mean 120 a second to the Spire for 5 s). The Void Architect uses `rustbucket.fbx` (gun_robot's clips tumble it over).
- **Perk tree** (`MetaPerkTree.cs`, `MetaPerkTreeScreen`, `Data/perk_tree.json`): three lanes (Towers, BIT, Spire), each a ladder whose rows open once enough points are spent higher up the lane (`needs`, counted from the rows above, so a rank can't be taken back while a row below depends on it). Ranked perks take a point a rank; keystones (◆) change how a run plays: Field Kit (first towers free), Free Rebuild (full refunds between waves), Veteran Crews (towers built a level up), Head Start, Second Wind, Bounty Contracts, Compound Interest, Wider Choice (4 perk cards). `MetaPerkRegistry.Apply` sets `SignalTuningEditor` and `MetaRun` at run start; placement goes through `MetaRun.PlaceCost`, upgrades through `VineNode.PriceOf`. Points: one per `perWaves` waves cleared in every run, the first-time milestone points (milestones.json `metaPoints`) and one per Ascendant killed. Saves keep `ranks`; an old save's numbered perks are refunded once with a notice on the tree. Rows at 14 and 18 points per lane add crew, dome, tower health and double veterancy kills (Towers); BIT regen, speed, cooldowns and a BIT kill bounty (BIT); cheaper strikes, starting Spire guns, bigger drops and a charged strike of each kind (Spire). At 30 each lane has a Mastery card (`repeatable`, 99 ranks, not counted in `TotalCost`) so points never pile up with nothing to buy; the screen scrolls and opens at the first row with something left to take.
- **Roles** (`RoleRun.cs`, `role` block in `Data/Spires/*.json`): every role builds every tower; the role decides how the run plays. Bruteforge (weapons): upgrades and strikes 25% cheaper, Junk Turrets and Scatter Cannons +20%, a Spire gun at once and another every 10 waves (3 more at most, `SpireStation.GunsWanted`). Arcanist (signals): crew links worth double, Overclock Relays reach 2 cells and buff 50% harder, Tesla Coils +25%, the shield back in half the time and a shock that stuns everything within 8 for 2 s when it breaks. Obelisk (field control): BIT +30% damage and +40 health, abilities 30% cheaper and 25% stronger, slows and shoves 30% harder, the beam +50% and jumping on to 2 more enemies at half damage. `GameManager.ApplyMetaPerks` calls `RoleRun.Apply(SelectedRole)` after the tree; each bonus's `text` is a line on the role screen (`signature` marks the role's own twist, also shown on the Spire panel). Before 2026-10 the three roles played the same and the role screen listed the same eight towers on every card.
- **Camera shake** is a trauma model (`TDCamera.ShakeTrauma`): shakes add trauma that decays, the offset grows with its square up to `SHAKE_FULL`, and small shakes (ordinary deaths) are capped at `SHAKE_MINOR_CAP`, so a pile of deaths at once doesn't throw the view around.
- **Waves stack:** Send All sends every wave up to the next milestone (multiple of `SEND_ALL_MILESTONE`, the button says "TO W15"), at most `MAX_STACKED_WAVES` waiting, each arriving `STACK_STAGGER` seconds after the last (it sent three, which read as "only sends 3 waves").
- **The ramp after wave 20** (`ramp` in `Data/difficulty_scaling.json`, `VineWaveLoader.GenerateWave`): procedural waves compound health (`hp_growth` a wave), enemy damage, extra traits and commanders; counts stop at `count_cap` times the template and the rest goes into health (deep waves had 200 enemies and sank the frame rate); commanders' health scales too (it stayed at its authored 200); the wave bonus stops compounding at `bonus_growth_cap_wave`. Wave health against W20: about x2.7 by W25, x8.5 by W30, x60 by W40 (`counters/P#/steep_ramp_after_20`). Before, health grew 4% a wave and a full build cleared W150.
- **Enemies working together** (`VineEnemySupport.cs`): an enemy with `RALLY_COUNT` others within `RALLY_RADIUS` is rallied (+15% speed and damage, a red ring, a RALLY tag). The **Empowerer** trait (waves 12, 16, 19 on both planets, so deep waves too): a fragile support unit with a magenta halo that tethers to the toughest enemy near it (a magenta beam); that enemy takes `EMPOWER_TAKEN` of every hit and moves and hits harder until the Empowerer dies or is stunned.
- **Towers together:** firing towers linked to firing neighbours get `CREW_BONUS_PER_LINK` damage and rate per link (up to `CREW_MAX_LINKS`; cyan links), a relay's links glow in its colour while it boosts and go grey when it boosts nothing, links that do nothing are grey. Towers inside the Spire's dome fire `DOME_RATE_BONUS` faster and mend themselves. Every few seconds a tag over a tower says what boosts it; the build ghost says what a cell would give (`VineHUD.PlacementHint`). **Veterancy** (`Data/veterancy.json`, `Veterancy`): a tower's own kills (`VineEnemy.HitTower`/`LastTower`) rank it up for more damage and rate, shown as chevrons and in its panel. **Many at once:** the tower panel's UPGRADE ALL (or Shift-click UPGRADE) takes every tower of that type a step (cheapest first, a branch copies this tower's); Shift-drag boxes towers (Ctrl or Shift-click adds one) and the panel upgrades or sells the group (`TowerInspector.UpgradeMany`, `SelectInBox`).
- **Per-frame costs:** `Roster` reads the enemies and towers (with positions) once a frame for everything that searches them; `FrameProfiler` times the hot updates when a test switches it on (`siege` prints it); `LogOnce` keeps per-spawn set-up lines out of the log (10 lines an enemy made a 6 MB log by W140). The wave label counts enemies left and says the wave ends when they're all down. `FlightRecorder` rewrites `user://logs/flight.log` every second (fps, phase, waves, enemies, objects, memory) and keeps it as `flight_crash.log` when a session ends without its "END clean" line.
- **UI scale:** the UI is laid out on a 1920x1080 canvas (`canvas_items`, aspect `expand`, so wide and tall windows use their whole width or height instead of letterboxing). Below 85% of that size `GameManager.UpdateUiScale` scales the UI back up (up to 1.35x). HUD text is at least 14 px at 1080p (`hud/*/readable`). The right-hand column (shield walls, next wave) sits on dark cards. The web UI layer (godot-cef) draws on the CPU by default (`junkyard/ui/cef_accelerated_osr` turns GPU texture sharing back on); its Windows build is the only one in the repo, so it never runs in the Linux test container and the code-built fallbacks are what the suites test.
- **PCM audio synthesis** for placeholder sounds

### Terminology
| Use This | Never This |
|---|---|
| Surge | group, spawn group, sub-wave, pack |
| Wave | round, stage |
| Resources | gold, scrap, coins, credits, currency |
| Materials | mana, magic, energy |
| Commander | miniboss, special |
| Ascendant | god, hero bot |
| Run | session, game |
| Planet | world, map |

### Address Format
`P#-W#-S#` in logs, comments, data. Example: `P1-W4-S3` = Planet 1, Wave 4, Surge 3.

**REMOVED:** Floor layer (`P#-F#-W#-S#` is deprecated). One continuous map per run, no rebuilds.

---

## Architecture

```
Godot_TD/
├── Scripts/
│   ├── Core/           GameManager, ServiceLocator, GameEvents, Constants, Enums,
│   │                   TransitionManager (autoload — fade transitions),
│   │                   FrameBudget, EntityRegistry, BuffDebuffComponent,
│   │                   TerritoryData, SuitData, SuitManager
│   ├── VineLogic/      ALL vine TD gameplay code:
│   │   ├── VineGrid.cs              Grid + heightmap terrain + node placement
│   │   ├── VineNode.cs              Signal processing for all 18 node types
│   │   ├── VineNodeData.cs          Node type registry (costs, ranges, power)
│   │   ├── VineEnemy.cs             Enemy controller (frame stagger, march mode)
│   │   ├── VineWaveData.cs          SurgeData, VineWaveData, CommanderData
│   │   ├── VineWaveLoader.cs        JSON-first wave loading with hardcoded fallback
│   │   ├── VineWaveManager.cs       Surge spawning, completion modes, commander spawning
│   │   ├── VineMapLayouts.cs        Map layouts (being refactored — floor dispatch removed)
│   │   ├── DifficultyScaler.cs      Continuous difficulty scaling from JSON
│   │   ├── VinePlacer.cs            Node + Mining Building placement
│   │   ├── VinePlayer.cs            BIT — MOBA abilities, dome material swap
│   │   ├── Mech/                    MechSheet (data), MechProgression (XP, levels), MechAppearance (growth, gear)
│   │   ├── VineHarvester.cs         Mining Building (Resources/Materials toggle)
│   │   ├── ConversionDome.cs        Fog-ring VFX + dome radius + material swap
│   │   ├── ShieldWall.cs            Energy barrier Node3D (procedural mesh, pulse, collapse VFX)
│   │   ├── ShieldWallManager.cs     Shield wall lifecycle (time triggers, BreakWall API)
│   │   ├── ShieldWallData.cs        Shield wall config + trigger type enum
│   │   └── AssetLibrary.cs          Asset loading, scaling, verification
│   ├── Camera/         TDCamera (flyover, orbit, shake, WASD pan, player-follow)
│   ├── VFX/            VfxParticles (pooled particles), VfxLook (meshes, shaders), VfxFactory (effects)
│   ├── Commentary/     AXISCommentary (rewriting for BIT voice)
│   ├── UI/             MainMenuUI (CEF title + planet select via URL swap),
│   │                   MetaHubScreen (CEF meta hub), DebriefScreen (CEF debrief),
│   │                   RelicInventoryScreen (CEF), LoadoutsScreen, LoadoutSave,
│   │                   TerritoryScreen (code-built), BossConfirmScreen (code-built)
│   ├── Editor/         F12 editor suite
│   ├── Testing/        TestHarness + 7 test suites
│   └── Debug/          BugReportDialog, DebugMenu
├── ui/                 Stitch HTML screens (title/, code.html, meta-hub/, debrief/,
│                       relic-inventory/)
├── Data/
│   ├── Waves/          JSON wave data per planet (P1.json and P2.json, 20 waves each, continuous)
│   ├── Towers/         Tower sheets (models, aim, recoil, perk parts) per buildable tower
│   ├── Levels/         Map layout JSON
│   ├── Mechs/          Player mech sheets (bit.json: XP, levels, sockets, gear per perk)
│   ├── territory.json  Territory section definitions per planet
│   └── difficulty_scaling.json
└── docs/archived/      Pre-pivot planning docs
```

---

## Game Flow (Post-Pivot Target)

```
MainMenu → [Planet Select] → Territory Map (unlock sections, view suits) →
  Farming Run:
    IntroCinematic → VineDraft (choose role) →
    Place Mining Building → choose material type →
    Wave loop (continuous, escalating):
      Build phase → Wave with surges → Build phase → next wave
      Wave milestones trigger: perk selection, new entry points, map expansion
    Spire destroyed → 2s delay → Debrief (DebriefScreen) → Save Suit (optional, max 3 slots) →
    Continue → Meta Hub (MetaHubScreen) →
  Boss Run (from Territory Map):
    Select unlocked boss section → Select suit → Confirm ("RISK IT") →
    Skip draft → suit towers pre-placed on grid → waves until boss_wave →
    Win → section cleared, bonus resources
    Lose → suit DESTROYED
  Meta Layer:
    Spend extracted resources → Territory unlocks →
    Territory gates boss sections → boss runs need suits →
  Next Run (more options, clearer target)
```

### Difficulty Via Directionality (Shield Wall System)
- Game starts with West entry open, N/E/S blocked by Shield Walls
- Shield walls are visible energy barriers with procedural mesh + pulse animation
- Default trigger: time-based milestones (5min / 10min / 15min)
- Flexible trigger system: `ShieldWallManager.BreakWall(direction)` callable by any system
- Trigger types: `TimeMilestone`, `WorldObject`, `UIPrompt`, `Scripted`, `Manual`
- When a wall breaks: collapse VFX, entry region activates, pathfinder recalculates, HUD announces
- Entry regions have `Active` flag — VineWaveManager and VinePathfinder only use active regions
- Shield wall configs defined in level JSON (`shieldWalls` array) or hardcoded fallback
- More entries = more enemies = more resources to extract (opportunity, not just threat)
- Build compounds over time — no rebuilds, no resets

---

## Narrative

**Narrative is light-touch.** Character barks, BIT sarcasm, futility observations. ~5 lines every 10 waves. No complex memory bleed arcs. No 3-act structure across runs. Let the game tell the story.

### BIT
Ancient AI. Been doing this exact job longer than anyone in the game has existed. T'lan Imass archetype — functional nihilism, dark dry humor, flat affect. Not a hero on a journey. Just keeps moving. Doesn't beat anyone. They become irrelevant by proximity.

BIT doesn't know it's the cleanup script. Doesn't care about AXIS. Doesn't care about the Ascendants. Just executes, moves forward, and in doing so exposes everyone else's limitations without trying.

### AXIS
Nepo baby corporation that acquired BIT without understanding what it holds. Sends urgent directives — BIT has received 847 of them. Dismissive, not dramatic. Performatively urgent about everything because it has no actual context. Creates conditions for conflict, sends BIT to resolve it, harvests the byproduct. Keeps happening "by accident."

### Ascendants
Massively overpowered AIs who believe they've ascended. Show up reactively: an enemy Ascendant comes with a wave and walks at the Spire, the friendly one responds and fights it. The friendly isn't there for you; the player is just the stage, but the stage can shoot back. Cause map chaos: terrain destroyed, entries opened, nodes caught in crossfire. Each believes they're in the most important war. None of them are.

### The Loop IS The Story
Roguelike loop is the narrative. BIT has done this before — many times — but doesn't retain memory between deployments. AXIS wipes it. Except this run, something didn't wipe correctly. Fragments bleed through. BIT starts remembering.

---

## Resource System

### Resources (formerly Scrap/Gold)
- Universal, always collectable
- Funds vine node placement, infrastructure
- Mining Building produces in Resources mode
- Dropped by enemies

### Materials (formerly Mana/Magic)
- Harvested resource, gated by choice
- Accumulation-based passive buff system
- Spent in per-wave-milestone shop for ability upgrades
- Three types per planet: **Chaos, Power, Environment** (planet-agnostic)

### Material Types
| Type | Identity | Sub-paths |
|---|---|---|
| Chaos | Entropy | Mind (enemy AI disruption) or Corrosive (poison/acid) |
| Power | Amplification | Extend ranges, amplify outputs, supercharge signals |
| Environment | Space manipulation | Deconstruct/reconstruct terrain as weapon or shield |

### Character Material Access
- Combat characters: locked to 1 material type, double rate
- Non-attacker: can mine 2 types, strategic timing on second choice

---

## Mining Building

- Three mining rig variants: 1) Built-in turrets (offensive), 2) Regenerating shields (defensive), 3) High regen + enemy pushback (sustain). Each has different difficulty curve.
- Placed by player, prompts material type selection
- **Toggle:** Resources mode (fund network) vs Materials mode (fund character)
- Cannot produce both simultaneously
- Persists the entire run — no floor resets

---

## Planets & Enemy Behavior

### Planet 1: Grid Prime (Tron)
- Dark blue-black, cyan emissive grid lines, digital aesthetic
- **Circuit AI:** predictable paths, exploitable patterns
- Fixed entry points, orderly lines

### Planet 2: Scrapyard (Rust)
- Warm browns, corroded oranges, industrial grime
- **Mercenary AI:** squads from edges, less predictable
- Broader defense required

### Planet 3: TBD
- **Military AI:** scouts, flanks, adaptive routing

### Enemy Traits (counters)

Surges in `Data/Waves/P#.json` can carry `"traits"`: **armoured** (light and electric hits do 30%, ordinary 75%, heavy hits and BIT's shots land in full; Shredder strips it), **flying** (keeps 2.4 above the ground and flies straight over everything at the Spire; only Flak, Tesla, the Minigun branch, BIT and the Spire's guns reach it; Flak shoots flyers first) and **shielded** (a shield of 60% of health that takes hits first, electric hits three times over, and grows back 15% a second after 2.5 s unhit). Each tower hits as Heavy (Junk Turret, Scatter Cannon), Light (Flak) or Electric (Tesla). Waves 1 to 3 are plain; armoured comes in on wave 4, flying on 6, shielded on 7 (P2 the same), and the wave card names the trait and its answer. Trait surges carry less health (armoured 0.85, shielded 0.7, flying 0.8, flyers also 0.7 speed). Looks: armoured enemies wear a faceted gunmetal shell with an orange rim and ribs and a steel-blue health bar (on its own `ArmourRig`, sized from the body and turned with it, so hit flashes and import scale leave it alone), shielded ones a cyan bubble and a shield bar over the health bar, flyers a ground shadow and a thruster. Logic in `VineEnemyTraits.cs`.

### Tower Upgrades

Click a placed tower for its panel (`TowerInspector`/`TowerPanel`): two levels, then one of two branches, or sell (refunds the upgrades too). `Data/tower_upgrades.json` holds costs and effects (damage, rate, range, hp, slow, buff, force; `grants` a tower perk's behaviour for that tower; `antiAir`; `kind`). Branches: Junk Turret Railgun or Minigun (anti-air), Scatter Mortar or Shredder, Tesla Storm Coil or Overload, Flak Skyguard or Shrapnel, Tar Napalm or Glue, Ram Piston or Repulsor, Relay Network or Overdrive, Wall Bulwark or Spiked. A press on a tower is a pick, so BIT doesn't fire.

### Enemy Factions
| Faction | Behavior | Color |
|---|---|---|
| Scavenger | Follow paths, confused by flickering gates | Bright red |
| Brute | Bulldoze switches, break logic state | Dark crimson |
| Ghost | Ignore gate routing, phase through walls | Magenta-red |
| Swarm | Tiny, fast, trigger count sensors early | Orange-red |

---

## Signal Power System

Sensors have a **power budget** — the number of effect nodes one signal can activate.

| Sensor | Power | Notes |
|---|---|---|
| Proximity Sensor | 3 | Standard detection |
| Type Sensor | 3 | Faction-specific |
| HP Sensor | 3 | Wounded enemies |
| Count Sensor | 4 | Group triggers |
| Timer | 4 | Fires on interval |

- Each effect node costs 1 power. Routing nodes pass through FREE.
- Signal dies when power reaches 0.

---

## Node Types (18 implemented, 8 per role via draft)

**Structural / Routing (free):** Extender, Junction, Switch, Gate (AND), Inverter, Delay, Latch

**Sensor / Input (generate signals):** Proximity Sensor, Type Sensor, HP Sensor, Count Sensor, Timer

**Effect / Output (cost 1 power each):** Damage Tower, Slow Field, Push/Pull, Loop Anchor, Buff Emitter, Signal Cannon

### Signal Chain
`Sensor → signal → vine connections → effect node (activates, -1 power) → next effect → ... → power 0`

---

## Commander System

Commanders are optional special enemies attached at Surge level.

### Spawn Conditions
- **Scripted** — always at a specific address
- **Random** — probability roll
- **Reactive** — triggered by player behavior (WaveClearTime, PlayerOutOfBase, more TBD)

### Behavior Types (mix-and-match with any spawn condition)
- **Elite** — hard enemy, no special mechanic (implemented)
- **AuraBuffer** — buffs nearby units (stub)
- **Rally** — calls reinforcements, changes aggro (stub)
- **Assassin** — beelines to player, ignores all threats, telegraphed (stub)

---

## Meta Layer (S4 — Implemented)

### Territory
- Deterministic planet section unlocks, fixed cost, no RNG
- Data: `Data/territory.json` — sections per planet with id, cost, requires, map_variants, gates_boss, boss_wave
- Persistence: `TerritorySave` → `user://territory.json` (unlocked sections, cleared bosses, total spent)
- Loader: `TerritoryLoader` with `IsUnlocked()`, `CanUnlock()`, `TryUnlock()`, `GetBossSection()`
- UI: `TerritoryScreen.cs` — code-built CanvasLayer with planet tabs, section cards, unlock/boss buttons
- Gates boss runs and opens new farming map variants

### Boss Run Mode
- `RunMode.BossRun` in `GameManager` — high-stakes mode
- Flow: Territory → select boss section → select suit → BossConfirmScreen → StartBossRun()
- Skips draft screen, applies suit towers directly to grid via `SuitManager.ApplySuit()`
- `VineWaveManager.CheckBossWaveTrigger()` fires `OnBossDefeated` at `boss_wave` milestone
- Win: section cleared permanently, bonus resources awarded
- Lose: equipped suit destroyed via `SuitManager.DestroySuit()`
- UI: `BossConfirmScreen.cs` — suit preview, warning, suit selector, confirm/cancel

### Suits
- Serialize a successful build to meta storage (max 3 slots, `Constants.MAX_SUIT_SLOTS`)
- Data: `SuitSaveData` (name, role, planet, material, nodes list, consumed flag)
- `SuitNodeEntry` stores grid position + VineNodeType + slotted TowerComponentTypes
- `SuitManager`: static manager with `CaptureSuit()`, `ApplySuit()`, `DestroySuit()`, `GetAvailableSuits()`
- Persistence: `user://suits.json`
- Displayed in `LoadoutsScreen.cs` suits section (above existing loadout grid)
- Lose the suit if you die on the boss run

### Node Unlocks (NOT YET IMPLEMENTED)
- Spend meta resources to add new node types to permanent draft pool

---

## Systems Being Removed

- **Floors** — `CurrentFloor`, `FloorComplete`, floor-indexed dispatch, floor-based wave lookup, floor-triggered perk select
- **Classic TD** — `WaveManager`, `WaveData`, `WaveRegistry`, `Battle.tscn`, `BattleScene`, `MapSelect.tscn`
- **Deprecated** — `HeroBotController`, `FabricationSystem`, `ScrapManager`

---

## Hard Rules

1. All spawn, scaling, and tuning data belongs in JSON. Never hardcode.
2. Use address format `P#-W#-S#` in all logs, comments, data.
3. Commander behavior and spawn condition are always separate fields.
4. Bosses spawn at wave milestones. Commanders attach at Surge level.
5. Completion mode lives on the Wave, not the Surge.
6. Do not build on the `Gold` or `Scrap` currency names — use Resources.
7. Do not build on `Mana` or `Magic` — use Materials.
8. Textures under `Models/` and `Materials/` import with `compress/mode=2` (VRAM), `mipmaps/generate=true` and `process/size_limit` at most 1024, or 2048 for `Models/Spires/` and `Materials/`. New kit assets arrive lossless and uncapped: set these before using them and run the `textures` suite. If you edit `.import` files outside the editor, check that the editor reimported them (`textures/imported_as_vram`).

---

## Known Issues

- **DifficultyScaler, EntityRegistry, FrameBudget are never instantiated** — every `ServiceLocator.TryGet` for them returns false. Procedural-wave escalation lives in `VineWaveLoader.GenerateWave` instead.
- **TypeSensor triggers on ALL enemies** — no faction filter (sensors are no longer in the build roster)
- **Web UI is untested here:** godot-cef ships only a Windows build, so the HTML screens (title, planet select, debrief, meta hub, relics, suits, territory) have never run under the test harness. On the dev laptop (RTX 3050, Vulkan) GPU texture sharing drew pages as a strip of noise and froze the game after a few screen changes; CPU rendering is the default now and needs confirming on that machine.
- **Difficulty after the ramp (2026-10):** a soak with 30 towers (half at level 2), no BIT and an unkillable Spire lost every tower by W26; 60 towers at the top level with a branch (perks picked automatically, no BIT, no maze) on a normal Spire held it untouched to W28 and fell on W29 (`soak` with `SOAK_STRONG=1`). A human with BIT, a maze and strikes should get a few waves further. Earlier autoplay (before the ramp, one run each): P1: building nothing loses on wave 3 (it reached 16 while the old chaos stalled every wave), the MixedDefense tower bot 9 (14 with chaos switched off), the all-BIT bot (`BITOnly`, Materials mode, no towers) 16. P2: MixedDefense 9, BITOnly 9. Enemies differ little (four factions, no armour or flyers in normal waves), so nothing forces a particular tower; in-run perks are mostly flat stat bumps. A design proposal for counters, tower upgrades and difficulty is waiting on a decision.
- **Flak Battery crash on the dev PC (2026-10, not reproduced):** picking the Flak Battery after P1-W6 (Relay Station Alpha, Bruteforge) ended the run with nothing in the log. The same pick and a ghost sweep over the field run clean here (`playtest`). `VinePlacer` now logs `Placing <tower>` and `Ghost ready` so the next occurrence shows how far it got.
- **No music:** only SFX and ambient. No music files exist on the dev PC either; this needs assets.
- **Role structures are unreachable:** every role uses standard grid placement (`VinePlacer` forces it) and no role's node list offers Pylon, Socket or Prism, so `ObeliskPowerSystem`/`ObeliskPylon`, `ArcanistSocketGrid` and `BruteforgeWireGrid` are never created. Their visuals are still placeholder shapes; rebuild them only if the role placement modes come back.
- **Signal drops do nothing:** `SignalDropManager` collects component drops, but there is no UI for them and none of their effects are implemented (part of the unbuilt tower slot system).
- **Bruteforge Spire model** (`Driller.glb`) is the weakest art in the game; the kit has nothing better, so it needs a new asset.
- **Stale texture imports:** `Materials/Scrapyard/Textures/` has `.import` files for Ground031 and Metal042A but not the PNGs; loading them logs engine errors, so nothing references them.
- **Two placed buildings are missing:** `VineBattleScene` places `BLDG_CHECKPOINT` and `BLDG_WATER_TOWERS`, but both `.glb` files are in `.gitignore` and absent, so they never appear. Their 229 extracted textures are still in `Models/Buildings/`.
- **Unused textures:** about 357 textures (about 590 MB of source images) have no reference in any script, scene, material or model file: the two missing buildings' textures, the Disemech, SM_Armour and T_Enemies maps in `Characters/Enemies`, the Stan, George, Leela and Mike sets in `Characters/Player`, and the Scrapyard material channels no material reads (Bump, Cavity, Gloss, Opacity, Displacement, most Specular, all of Small Garbage Scatter). They cost disk and import time, not video memory.
- **VineWaveRegistry fallback uses old speeds** — JSON has correct values
- **Send All crash on the dev PC (2026-10, not reproduced):** pressing Send All again later in a run (P1, Relay Station Alpha, Arcanist) closed the game; the log stopped right after waves 8 to 10 were stacked. Headless and rendered `stress` runs (Send All on 3, 7 and twice on 11, 28 towers, 4x) reach wave 17. Stacking is now capped at `MAX_STACKED_WAVES` and staggered, and `user://logs/flight_crash.log` keeps the last seconds of any session that ends without closing cleanly: read it after the next crash.
- **Overclock Relays and mazing:** relay boosts don't stack (a tower takes the strongest one near it), so a field of relays mostly works as cheap, sturdy maze blocks that enemies shoot first (they target effect nodes). If that stays the best build, raise the relay's cost or HP cost rather than adding stacking rules.

### Resolved

- **Playtest round 5 (2026-10):** Settings said "coming soon" (now `SettingsScreen`). [T] did nothing (now switches mining mode between waves). Climbing in stayed in the map view; after an upgrade bought from inside, the cannon kept firing on its own. BIT's aimed shots dropped after about 2 cells (now 44). The narrative line sat bottom left over BIT's panel (moved top left). Big moments were a line of text and loot a card under the build bar (now `Celebration`); drops showed nothing (`ResourceFlyout`); BIT getting hurt showed only a white flash on the model (red edge, red numbers) and nothing healed BIT (regeneration, the Spire, Repair Pulse). Links and relays didn't show whether they did anything, buying Spire Guns changed nothing visible, building inside the dome did nothing, and there was no way to upgrade many towers or any natural progression (crew links, dome, guns and plates, veterancy, upgrade all, drag-select). The naruto run added nothing (now a ramp to +60%) and BIT slowed as the run went on (move speed growth and chaos boosts compounded wrongly). Send All sent three waves (now to the milestone). A level-up over the Spire menu blocked every click (the perk pick closes the menu first). 40 maxed turrets with 120 enemies cost about 6 ms a frame in enemy updates alone (each enemy walked its model every frame for a glow pulse and scanned every tower; now 0.9 ms) and four shadow cascades drew everything five times over (Graphics setting). Performance sank over a long run: deep waves had five times the enemies and every spawn wrote 10 log lines (count cap, `LogOnce`). A boss spawn shook the screen hard for a second (now a light rumble, rate limited). The Void Architect lay on its side and kept falling over (new model); the friendly Ascendant walked off without fighting (it now fights and lingers); an Ascendant's death was unexplained (kill attribution). A Tesla Coil took damage for nothing (acid hazards chipped towers; `HAZARD_TOWER_CHIP_DPS` 0). Waves went on forever (the ramp; a new player was at W61 and a full build at W150). The grey screen after a match (paint watchdog). Progression was hard to see and the Command Center only appeared after a run (objective card, celebrations, debrief line, PLAY to the Command Center, Deploy). Guarded by `flow`, `spire`, `player`, `counters`, `gameplay`, `ascendant`, `hud`, `menus`, `siege` and `soak`.
- **Playtest round 4 (2026-10):** BIT's shots dove into the ground (they aimed at the feet; now the body's centre, misses fly level and skim the terrain); a level-up part stuck out of BIT's head (the long barrel and rail now sit on the forearm and hip); the naruto run hitched every second (the loop crossed a held key; now 3.3667 to 3.9167 s with both ends pinned, `runloop`). Ascendants were untouchable capsules that fought each other and, if the friendly fell, blasted the Spire for 5 s (now a boss that walks the maze, see Ascendants). The perk tree ran out of tiers after 8 points while points kept coming (rebuilt, see Perk tree). Q/E/R spent Materials with no visible result and mining was unexplained (now each ability shows its reach and reports what it did, or why nothing happened; the help, wave label and mining card explain mining and how a wave ends). Enemies beelined whenever the straight line was clear, so mazing did nothing (now the flow field). Many deaths at once shook the camera hard (now trauma with small shakes capped). Planet select drew a big blue box round the planet; a planet with no sites opened a black or grey screen, and a crashed web page left a grey screen with no way out (now reload once, then the code-built screen). The debrief couldn't be clicked past (Enter/Esc continue). Guarded by `player`, `ascendant`, `metaperks`, `maze`, `menus`, `hud`, `stress` and `flow`.

- **Playtest round 3 (2026-10):** the build bar showed two tooltips at once, Godot's own popup over the neighbouring buttons and a label over BIT's panel; now one card above the bar with the tower's job, what it beats, what beats it and its numbers (`TowerInfo`). The Flak Battery's muzzle sat at the finned back of its rocket pods, so it aimed its tail at targets; turrets fired the moment a target appeared, out of the side of a barrel still swinging round (now they turn first, `TowerLook.ReadyToFire`); between targets every turret rested on the same world diagonal (now it watches the enemy path upstream); a turret on a turned parent aimed off by the parent's turn. Shots checked for arrival after moving, so fast shots at low frame rates stepped past the target and flew 30 units on into the ground; they also passed through hills (now they land on the step that reaches the target and stop at the ground). AXIS chaos fired every wave from 2 and only made enemies mill about within five cells of where they stood, so they stopped advancing and died; that milling made every wave easier (enemies stopped walking at the Spire), which is most of why building nothing reached wave 16. Now it comes on waves 5, 9, 13 and so on (the wave card warns), enemies go berserk (1.3x health and damage, faster fire, longer reach, hunters and wreckers 1.2x speed) and leave the path: two in five hunt BIT, two wreck the nearest tower, one rushes the Spire at its own pace; AXIS drops three squads of tougher reinforcements on the far side of the field that fight (they never rush) and hold the wave open. Resource nodes paid out unexplained (now labelled on the map, and the top bar shows what the Spire earns per tick and how many nodes are held). Guarded by `chaos`, `playtest`, `player/shot_*`, `towers/aims_on_turned_parent` and `towers/idle_watches_path`.

- **Controls, UI and BIT (2026-10):** WASD stayed on the map's axes after the camera was turned. BIT's Run clip wasn't looped, so the naruto run played once and blended back in from the rest pose (and only started after 2 s of holding a key). Right-click acted on the press, so starting a camera turn over a tower sold it (over the Spire, it flipped the mining mode). HUD text was 10 to 13 px with the shield-wall and next-wave lists drawn straight over the bright shield walls; the top bar's labels touched the screen edges; at 720p the UI shrank to two thirds; on wide screens it letterboxed. The help described the old signal-chain build ("effect nodes only activate when they receive a signal"); the pause menu counted towers as Sensors/Effects/Routing and empty mod slots, said "MILESTONE: MILESTONE:", and AXIS's lines drew over its title; perk cards with long descriptions pushed TAKE out below the card. Grid Prime's converted decor was unshaded near-black, so from above it looked like holes in the dome floor. F11 in the editor's Game tab did nothing (it can only be windowed). Guarded by `player`, `spire`, `hud` and `input/right_drag_keeps_tower`.

- **Stats (2026-10):** Flak used its base damage directly, so no bonus reached it, and it dealt 20/s to each of five targets for 22 (now 6/s per target for 20, scaled by bonuses). Scatter, Tesla and Flak fired on fixed timers, so fire-rate bonuses never raised their damage, and the RapidFire slot slowed towers down. The Relay's fire-rate half never reached most towers. Planet 2 had no wave data and reused P1 (now `Data/Waves/P2.json`, 20 mercenary waves with squads, commanders and two bosses, about 1.3 to 1.6 times P1's health per wave). A slow kept its strength after it expired. Guarded by `balance`.
- **Effects (2026-10):** there were no particles. Every shot, hit and pulse was a glowing sphere or torus node with its own mesh and material (a trail dot every 0.03 s per projectile), the Tar Sprayer's and Ram's pulses swept wide opaque bands across the field, and the Conversion Dome added about 8 mesh nodes a second for its motes. A P1 autoplay peaked at 25,700 objects and 763 MB; it now peaks at 18,800 and 645 MB. Links between two towers that fire on their own were drawn dim red as "unpowered" (red is the enemies' colour); the Flak Battery's tint was a dusty red too (now gold). Guarded by `vfx` and `visual.player_colors_clear_of_enemies`.

- **BIT shrank after its first shot (2026-10):** the attack pulse set the model's scale to 1 instead of its normalised 1.2, so BIT dropped to 84% of its size on its first attack and stayed there. A hit flash also wrote silver into the planet-themed materials, so BIT kept a silver sheen outside the dome after its first hit (now `HitFlash`). Guarded by `mech/growth_survives_attacks`.
- **Visual fidelity pass (2026-10):** on the 13 battle maps, 291 of 733 fidelity checks failed. Enemies kept their spawn height and waded up to 2 units into hills or floated over valleys; towers stood at the cell's average height with the downhill side in the air, and on elevated cells inside the mesa decor (whose range bonus never applied); decor floated over dips and channel floors and data streams were buried under the terrain; the Spire hovered (Obelisk 0.24, from hand-tuned burial depths); walkers cut corners through towers and walls and knockback and swarm jitter pushed them in; Turret A (Damage Tower) and the Plasma Gun (Buff Emitter) reached 0.21 and 0.13 past their cells, and the Brute's legs spanned 3 units (now `ENEMY_HEIGHT_LARGE` 1.4, 2.6 across). Bosses at `BOSS_SCALE` 2 (Brute boss 5.3 units across in 2-unit lanes) cut 0.9 into the towers beside them; at 1.3 they brush up to about 0.33. `GetWorldHeight` also interpolated bilinearly, not along the drawn triangles. All 788 checks now pass. Guarded by `fidelity`.
- **Kit textures filled video memory (2026-10):** 447 KB3D and material textures imported lossless (RGB8/RGBA8), the rest VRAM compressed but with no size limit (327 sources at 4096 or more), 165 without mipmaps. A Grid Prime battle held about 5.3 GB and ran the software-rendered tests out of memory. Every 3D texture now follows hard rule 8; the same battle measures 253 MB of textures and 407 MB of video memory (Scrapyard 335 and 504). Guarded by `textures`.
- **Animation pass (2026-10):** the Scavenger's model ships with an empty clip and slid along frozen (now a procedural trot); the Swarm drone rendered 2 units above where it was grounded (skin offset); Brute/Ghost deaths played Idle (now their power-down); Hit looped via Stunned and restarted on every tower hit; a hit flash left enemies glowing white for life; the Scavenger walked sideways. Guarded by `anim`.
- **Perk Tree hidden (2026-10):** the Command Center, where the tree lives, was the title screen's "LOAD GAME". Now "COMMAND CENTER" with a badge for unspent points; the in-run announcement and the pause menu say where to spend them.
- **Perk Tree unreachable (2026-10):** nothing linked to it and no points were ever awarded. Now in the Command Center; milestones.json `metaPoints` pays a point the first time each milestone is reached per planet; tree has a reset. Scrapyard had no milestones at all (no perk picks); it now uses planet 1's.
- **Scrapyard ground:** a flat 200x200 plane at y=0 cut through the field's valleys; replaced by `VineGrid.BuildOuterGround` (apron easing the edge heights down, shared dome material). Junk ring, debris and lamps stay off the field.
- **Towers invisible / black monoliths on the field (2026-10):** KB3D towers rendered about 70 units off their cells (only the HP bar showed) and Grid Prime's background turrets, mesas and ridges (laid out for a 40x28 field) stood on the 80x48 field. Models are recentred and `VineBattleScene.PruneDressingFromField` frees dressing that reaches the field. Tesla Coil, Flak Battery, Scatter Cannon and Barrier Wall were bare cubes; now `TowerMeshes`. Towers hovered 0.5 above the ground.
- **Temp-looking towers and Spires (2026-10):** four towers were primitive shapes (`TowerMeshes`), three wore kit models that didn't match what they do (gun turrets for the Tar Sprayer and Pneumatic Ram, an artillery diorama for the Overclock Relay), none turned toward their targets, shots left from a fixed point above the cell, a grey pip and an HP bar floated over every tower, the Arcanist's shield was a flat oval billboard, the Bruteforge's autocannons were unshaded cylinders floating 8 units up a 14-unit column, and Spire hit flashes zeroed every lit material. Now built from `Data/Towers` sheets and guarded by `towers`.
- **Hit flashes stuck (2026-10):** enemies and towers kept the flash colour after the first hit (only the energy was reset). `HitFlash` restores the material exactly.

- **Scrapyard (P2) loaded as a flat haze (2026-10):** the Conversion Dome recolored every mesh whose origin was inside its radius, including the grid-centered 120x120 haze layers and ground plane, turning them into opaque metal sheets over the map. Now only meshes whose whole footprint fits inside the dome, and that are not see-through, are converted. Guarded by the `planets` suite.
- **HUD top-right overlap:** shield-wall and next-wave panels started at y=10, on top of the top bar's Speed/Help labels; they now start below the bar. Build bar costs read "r" (Resources), not "g".
- **Memory leaks that ran headless/autoplay runs out of memory (2026-10)** — ConversionDome rebuilt 17 meshes every frame (now cached, rebuilt only when radius/position/colors/terrain change); every placement/sale rebuilt every VineConnection's meshes (now one in-place recolor per frame); transient VFX allocated a mesh + material per hit/trail dot that the C# wrapper kept alive until GC (now shared via `VfxCache`, faded with `GeometryInstance3D.Transparency`). Guarded by the `perf` suite.
- **Tron fog-bank shader never compiled** — read `INSTANCE_CUSTOM` in `fragment()`; now passed through a varying. Guarded by `health/battle_load_errors`.
- **Input** — right-click cancel also sold the tower under the cursor; Tab toggled speed twice per press; F11 opened the level editor mid-run. Guarded by the `input` suite.
- **Materials mode unreachable** — the material picker only followed manual Mining Building placement; the Spire is auto-placed. Picker now opens when the intro ends.
- **Orphaned PlanetSelectScreen** — `PlanetSelectScreen.cs` and `PlanetSelect.tscn` deleted. `MainMenuUI` handles both title and planet select screens via CEF URL swap. `GameManager.StartPlanetSelect()` sets `MainMenuUI.StartOnPlanetSelect` flag and loads MainMenu scene. `SCENE_PLANET_SELECT` constant removed. Back button added to planet select HTML. Settings button shows toast overlay.
- **Grunt Mech (decoy_unit.fbx)** — Actually `Robots_Grunt.FBX` from InvisGun Hero 2016 pack (3ds Max 2014). Texture: `GRUNT_red.png`. Was white/untextured (PNG gitignored), tracks appeared misaligned (`root_scale=100` distortion). Fixed: gitignore whitelist for `Godot_TD/Models/**/*.png`, `root_scale=1.0`, `materials/extract=1`. Constant renamed `ENEMY_DECOY` → `ENEMY_GRUNT_MECH`. FBX filename unchanged to avoid reimport churn. **Hierarchy note:** `totalControl` has rot=(-90,0,0) converting Z-up to Y-up. Track transforms under `leftControl`/`rightControl` are symmetric — do NOT adjust Y positions (local Y = world Z in this model). Track alignment is handled entirely by the `root_scale=1.0` import fix.

---

## Not Yet Implemented

- Continuous wave curve (replacing floor-based progression)
- ~~Dynamic entry points at wave gates~~ (DONE — Shield Wall system with flexible triggers)
- Exponential extraction resource curve
- Wave milestone system (perks, map expansion, Ascendant triggers)
- ~~Debrief/extraction score screen~~ (DONE — DebriefScreen.cs + ui/debrief/index.html, resource transfer, suit capture prompt)
- ~~Territory unlock system~~ (DONE — S4: TerritoryData, TerritoryScreen, persistence)
- ~~Suits system~~ (DONE — S4: SuitData, SuitManager, LoadoutsScreen integration)
- ~~Boss run mode~~ (DONE — S4: GameManager.StartBossRun, BossConfirmScreen, VineWaveManager boss trigger)
- ~~Scene transitions~~ (DONE — TransitionManager autoload, all scene changes use fade transitions)
- ~~Meta Hub~~ (DONE — MetaHubScreen.cs + ui/meta-hub/index.html, command center between runs)
- ~~Suit capture UI~~ (DONE — integrated into DebriefScreen, post-farming-run save prompt)
- Node unlock shop
- BIT memory bleed
- AXIS + BIT dialogue rewrite
- Materials shop
- 3 characters (only BIT exists)
- Planet 3
- Relic system (persistent inventory, limited boss carry, visual flex items)
- Tower customization / modular slots (white towers with slottable components)
- Three mining rig variants
- 20 map variant playtesting
- Suit detail view (left: 3 suit slots, right: grid viz + stats + relic equip)
- Territory CEF rewrite (replace code-built version with Stitch HTML)
- Boss Confirm CEF rewrite (replace code-built version with Stitch HTML)
- TowerSlotSystem serialization in suit capture (currently captures empty components)

---

## Controls

WASD move BIT (screen-relative), hold left mouse aim and fire (left-click places when a tower is selected), right-click cancel/sell or toggle the Mining Building (a click; right-drag turns the camera), scroll zoom, Q/E/R abilities, 1/2/3 fire a bought strike where you aim, F Spire menu and G climb in (at the Spire, starts in first person), V map/first-person view (in the Spire), T mining mode, Space start wave, Shift+Space Send All (to the next milestone), Shift-drag select towers (Ctrl or Shift-click adds one), Shift-click UPGRADE upgrades every tower of that type, Tab speed (1x/2x/3x), H help, F11 fullscreen (not in the editor's embedded Game tab), F12 editor, ESC menu. Debug builds: Ctrl+F11 level editor (outside a run).

---

## Build & Run

```
cd Godot_TD && dotnet build
# Open project.godot in Godot 4.6, F5
```

## Testing (headless)

```
godot --headless --path . -- --test-harness --suite=<name> --request-id=<id>
# suites: bvt content editor ui gameplay maps relics flow perf planets maze mech player spire chaos counters ascendant metaperks hud towers balance vfx input anim textures fidelity integration visual all
# also: stress runloop menus modelprobe siege soak clipprobe (and the -sheets suites); not in all
# results: test-reports/results/<id>.json ; unknown suite names fail
godot --headless --path . -- --autoplay --config qa/configs/turret_spam.json
# reports: autoplay-reports/<timestamp>_<strategy>_<role>/report.json (errors, peak objects/memory)
```

`anim` = every animated character built the way the game builds it: the clip each state plays, loops, moving tracks, no root drift or skin offset, a visible death, one-shots that don't loop, front legs ahead when walking (`anim-sheets` also renders contact sheets and facing shots to `test-reports/anim/`, needs a display). `textures` = every texture under Models/ and Materials/ is VRAM compressed, inside its folder's size cap and actually reimported that way (reads `.import` files and image headers, prints the budget by folder). `perf` = per-frame/per-edit allocation guards + no shader/script errors on battle load. `planets` = a real battle on every planet via its first territory site: no load errors, nothing opaque between camera and grid, HUD panels clear of the top bar. `maze` = enemies never stand inside solid nodes. `fidelity` = a real battle on every battle map (first non-boss site per layout on both planets): decor, overlays, the Spire and BIT sit on the drawn terrain; 8 towers by the path plus one on an elevated cell stand on the ground or a footing, fit their cell and clear the decor; 8 combatants (4 factions, normal and boss) walk 20 s with the model's bottom inside the range of ground under it, never get their body inside a solid cell and cut no more than 0.3 into a tower or wall (bosses 0.45; Ghosts phase through and are not checked). Writes `test-reports/fidelity/P#_layout.json`; `FIDELITY_SITES=P1:gateway,P2:foundry` limits the maps. `fidelity-sheets` also renders overview, play, Spire, tower, combatant and clip shots per map (needs a display, about 4.5 min per map; not in `all`). `mech` = BIT's progression in a real battle: the sheet loads and every offered perk has gear on known sockets; XP from kills (not despawns), BIT's own kills and cleared waves; levels at the curve's thresholds with their health, damage, growth and tier parts, capped at the top; the size survives attacks; each perk's gear is shown, on the body, in BIT's materials and above its feet; shots leave from the guns in turn; a new run starts bare. `mech-sheets` also renders BIT at each tier, with each perk's gear, and fully built on both planets to `test-reports/mech/` (needs a display). `towers` = every buildable tower built from its sheet in a real battle: models exist; it fits its cell at rest, stands on its base, reads at play distance (at least 1.0 across, 0.7 to 2.4 tall) and its muzzle is on the model; turrets turn to a target, the muzzle faces it and firing visibly kicks or punches; a Junk Turret shoots from its barrel tip and faces what it shoots; the build ghost matches; walls join along a line and round a corner, a lone wall is closed and neighbours reopen when a wall goes; the Bruteforge's guns are turrets on its platform; the Arcanist's dome covers the Spire and drops when the shield breaks. `tower-sheets` renders close-ups of every tower, the lineup from the play camera, a wall group, a top-down aim check (each barrel turned toward a marker due +X with its muzzle dotted) and each role's Spire on both planets to `test-reports/towers/` (`TOWER_TAG` names the set; `TOWER_KIT=1` instead renders every candidate kit model with its size; needs a display). `balance` = every shooting tower against 100,000-HP targets at 4x speed: single-target and five-target DPS per cost inside the bands under Tower stats, damage, fire-rate and Relay boosts each raise DPS, and each tower perk does what it says (arcs, cluster blast, saturation, pierce, relay reach, stun, tar pools); writes `test-reports/balance.json`. `vfx` = in a real battle each effect puts particles up, makes no engine objects and clears within 3.2 s; a projectile's trail makes no nodes; 130 effects in one frame fit the pools; debris lands on the ground; the dome's motes are particles; a firing Flak uses the pools and leaves no nodes. `vfx-sheets` also renders every effect at three moments (stepped by hand, `VfxParticles.Advance`) and a fight between every shooting tower and a line of targets at play distance to `test-reports/vfx/` (needs a display). `player` = BIT under real input: W and D move up and right on the screen at three camera angles, the naruto run starts at once, never drops off the Run clip for 3 s of held keys with key changes and attacks, stops on release, and the clip loops; BIT auto-fires at the nearest enemy, holding the left button fires at the aimed one instead. `spire` = the Spire station: F opens the menu only within reach, every upgrade costs what it says in the right currency and does what it says, the menu's buttons buy and refuse without the money, holding F repairs (standing there doesn't, and the hold doesn't open the menu), refill and training, G climbs in (hidden, safe), the cannon hits where it aims, G climbs out, Materials mode banks drops. `hud` = the battle HUD in every state (build, placing, help, material picker, perk pick, Spire menu, docked, pause, wave): everything on screen, no text spilling out of its box, top-level panels clear of each other, text at least 14 px. `hud-sheets` also saves a screenshot of every state at the window size it was started with (`--resolution WxH`; resizing from inside the game isn't reliable) to `test-reports/hud/` (needs a display). `input` = real viewport input routing (Tab, right-click, right-drag, F11, material picker). `chaos` = AXIS chaos mid-wave: every enemy on the grid goes berserk, all three roles (BIT hunters, tower wreckers, Spire rushers) are in play, reinforcements drop at once and again later, far from the Spire, and hold the wave open, hunters close on BIT and enemies leave the path, towers or BIT take damage, and when it ends the survivors head for the Spire. `counters` = enemy traits and tower upgrades: armour by hit kind and broken armour, each tower's hit kind and anti-air, shields (size, first, electric triple, overflow, regrowth), a flyer's height and straight line, ground towers missing a flyer while Flak and Tesla hit it, traits in the waves by wave 8 on both planets (none before 4) and in the last authored wave, every tower's two levels and two branches (cost, stats, one branch only, grants, sell value), the Minigun reaching the air, the armour shell on the body and turning with it, and the tower panel through real input (a click opens it and BIT holds fire, its button upgrades, Esc closes it without pausing). `playtest` = the reported session on Relay Station Alpha as Bruteforge through real mouse events: pick the Flak Battery and sweep its ghost over the field, hover every build-bar button (its card shows and nothing covers the other buttons), and each turret plus the Spire's guns tracking a marker, from above and from the side (`PLAYTEST_PARTS=flak,pick,hover,aim,enemies,card,panel` picks the parts; `enemies`, `card` and `panel` shoot the trait enemies, the wave card before waves 4, 6, 7 and 9, and the tower panel at each stage; screenshots to `test-reports/playtest/`; needs a display, about 12 min).

`ascendant` = an Ascendant in a real battle: announced on the wave card, comes with the next wave as a boss in its own model, the friendly answers and fights it, BIT and turrets hurt it, a kill pays the reward and sends the friendly off, the body waits for BIT after 10 runs and clears itself, one that gets through takes its share of the Spire without ending the run, a fallen friendly doesn't hurt the Spire (shots to `test-reports/ascendant/` with a display). `metaperks` = every perk-tree perk bought, in a real battle: stats, free towers and the build bar saying FREE, cheaper tougher walls, the free level, cheaper upgrades, full refunds between waves, Head Start, cheaper abilities, Second Wind once, double bounties, interest, four perk cards; then an empty tree changes nothing. `roles` = the role screen (each card shows how the role plays, its Spire's weapon, bonuses and a signature, and no tower list; the cards differ; a card's SELECT starts the run as that role), then a battle as each role with an empty tree: Bruteforge's starting gun, guns at waves 20 and 90, cheaper upgrades and strikes, +20% turret DPS; Arcanist's faster shield, double crew links, relay reach and strength, +25% coil DPS, the shield shock stunning three enemies without the hit getting through; Obelisk's tougher, harder-hitting BIT, cheaper abilities and a beam that chains; no role's kit shows up in another's run. `flow` also covers the tree screen (top row open, ranks stack, lower rows wait, every point spendable, guarded refunds, reset, an old save refunded) and perk points (depth, first-time milestones, Ascendant kills). `spire` also covers gunner view (V opens it above the Spire, the crosshair's aim is where the cannon hits, the mouse turns it, V, a pause and climbing out leave it). `stress` = Send All twice in a run at 4x until wave 14 (the reported crash). `runloop` prints BIT's Run clip pose rates and the best loop windows. `menus` = screenshots of the territory screen on each planet, the hub and the perk tree (as an old save opens it) with a check that each draws something and has a way out (needs a display). `modelprobe` renders candidate Ascendant models (needs a display). `clipprobe` renders every clip of `CLIP_MODEL` to `test-reports/anim/`.

Round 5 additions: `ascendant` also checks the kill names the killer and the friendly fights stragglers while it lingers. `flow` covers the campaign words (region progress, the next site, a region opening), Send All reaching the milestone and not piling up, the objective on the HUD and the site-secured celebration. `spire` covers guns and plates showing on the Spire, a bought gun firing, and each strike (bought, key 1 fires, the lance's damage, the barrage's area, the EMP's stun and shield strip, the HUD list). `player` covers the red edge and number when hurt, no healing right after a hit, healing by itself, Repair Pulse healing BIT. `counters` covers the ramp after W20 and the count cap, rallying packs, the Empowerer (tethers the toughest, damage cut, stun and death break it, in the waves and on the card), and many towers (Shift-drag selects, BIT holds fire, upgrade the group, upgrade all of a type, veterancy ranks and kill credit, sell the group). `gameplay` covers crew links, relay links lit or grey, an idle relay and the floating tags. `menus` also shoots the Spire pick reached by the hub's Deploy. `siege` = 40 Minigun turrets and 120 enemies held for 15 s: frame time (headless budget p95 16 ms), script time by system (`FrameProfiler`), draws and triangles with each group hidden, shadow cascades and LOD thresholds compared (with a display). `soak` = waves back to back at 4x with 30 towers to `SOAK_WAVES` (30): nodes, objects and an empty field's frame time stay flat, and what grew by type; `SOAK_STRONG=1` instead plays 60 top-level towers on a normal Spire and reports the wave it reaches.

---

## References

| Reference | What to borrow |
|---|---|
| Factorio circuit network | Logic gate feel, signal propagation |
| Opus Magnum | Physical machine satisfaction |
| Vampire Survivors | Exponential reward curve, "you already lost" framing |
| Slay the Spire | Run structure, draft, modifiers |
| Defense Grid | Maze-as-first-class-mechanic |
| Malazan Book of the Fallen | BIT voice — T'lan Imass flat affect, ancient weariness |
| Dungeon Crawler Carl | AXIS events, snarky AI antagonist |
| Tron Legacy | Planet 1 visual aesthetic |
| Balatro | Suit/build saving, synergy discovery, hand-building |
| Path of Exile 2 | Relic tradeoffs, self-debuff synergy builds |
| Beyond All Reason | Production-focused RTS, tug-of-war unit streaming |
