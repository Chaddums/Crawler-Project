# Junkbot Arena — TODO

> Generated from playtest bug reports and feature requests (2026-03-15)
> Source: `test-reports/bugs/` and `test-reports/features/`

---

## Feature Requests

### FR-01: Bot Frame Narrative Boxes
Add a short backstory/narrative for each bot frame in the character creation stats panel.
> *Source: 2026-02-28_18-59-37*

### FR-02: Batch Open Bronze/Silver Loot Boxes
Bronze and Silver loot boxes should open simultaneously since they're basic loot. Keep the existing celebration but get them out of the way quickly. Gold+ boxes keep the full ceremony.
> *Source: 2026-03-05_18-27-28*

### FR-03: Rarity-Scaled Loot Box Celebrations
The rarer the box, the more VFX, shiny visuals, exciting sounds, lights and colors on the celebration sequence.
> *Source: 2026-03-05_18-28-01*

### FR-04: Debug Freecam & Player Teleporter
Detachable debug camera that can be used to teleport the player character around when stuck or blocked.
> *Source: 2026-03-05_20-42-40*

### FR-05: Improve Player Bot Models
Player bots are very low-poly with disconnected appendages. Need better models with proper animations and connected limbs, or source additional robot assets.
> *Source: 2026-03-08_12-21-23*

---

## Bugs — Asset Loading (Root Cause Issues)

### BUG-01: PolygonDungeon Missing Materials (~62 reports)
**Priority: HIGH** — Nearly all PolygonDungeon `.tscn` prefabs reference `Dungeon_Material_01_mat.tres` or `Dungeons_Material_01_mat.tres` which doesn't exist. Affects props, walls, floors, characters. Fixing or creating this one material file eliminates ~30% of all open bugs.

### BUG-02: KitBash GLB Loading Failures (~17 reports)
**Priority: HIGH** — KitBash `.glb` files fail to load during dungeon room assembly. Affects crates, generators, barriers, HVAC, pallets, containers, lamp posts, turrets, etc. Likely import files out of sync.

### BUG-03: Missing Dungeon Prop FBX Files (5 reports)
Models referenced in code but files don't exist: `prop_shelves_thintall.fbx`, `prop_chair.fbx`, `prop_locker.fbx`, `prop_desk_medium.fbx`, `prop_desk_l.fbx`.

### BUG-04: Missing Enemy FBX Files (3 reports)
Enemy models referenced but don't exist: `eye_drone.fbx`, `trilobite.fbx`, `quad_shell.fbx`.

### BUG-05: RetroMech/SK_ISO_Mech Loading Failures (7 reports)
AXIS spider mech model and textures fail to load. Includes a case-sensitivity issue (uppercase vs lowercase `SK_ISO_Mech.fbx`). Associated textures and LOD material also missing.

### BUG-06: Other Texture/Material Failures (4 reports)
Scattered failures: `Dungeons_Material`, `T_Guns_Batch2_BaseColor.png`, `George_4_Texture`, SciFi enemy textures.

---

## Bugs — Enemy Models & Visuals

### ~~BUG-07: Enemies Using Default Red Pill Model (5 reports)~~ FIXED
~~Scrap Rat, Rust Mite, Shard Lobber, Volt Sprinter, and Overclock Drone all appear as the default red pill placeholder.~~
Root cause: Enemy FBX files were byte-identical copies of player models (scrap_rat=leela, etc.). Remapped all to POLYGON Dungeon characters or unique enemy FBX (trilobite, quad_shell, eye_drone).

### ~~BUG-08: Enemies T-Posed with No Animations (5 reports)~~ FIXED
~~Null Warden, Rust Titan, Scrap Hydra, Scrap Golem, and Glitch Phantom load their models but are stuck in T-pose.~~
Root cause: FBX copies had no AnimationPlayer data. Remapped to POLYGON Dungeon characters which have proper idle animations. BuildEnemyBody already plays idle on load.

### ~~BUG-09: Enemies Using Wrong Models (4 reports)~~ FIXED
~~Calibration Target using RustBucket (player) model; Junk Lurker same as Decoy Unit; Spark Drone same as Patch Bot; Wire Worm using a player character model.~~
Root cause: Enemy FBX files were copies of player models or each other (identical file sizes confirmed). Remapped all enemies to distinct POLYGON Dungeon characters or unique enemy FBX models.

### BUG-10: Decoy Unit Right Leg Detached (1 report)
Model geometry issue — right leg disconnected from body.

---

## Bugs — AXIS Boss

### BUG-11: AXIS Boss Model Issues (4 reports)
- AXIS is super tiny and T-posed
- Old AXIS bipedal model still appearing in combatant editor
- Old AXIS model appearing in intro (should be spider bot)
- Giant mech references need to be fully removed

---

## Bugs — Player Character

### BUG-12: Player Legs/Arms Break During Movement (2 reports)
Legs break when walking/running, arms go backwards. Regression from earlier fixes.

### BUG-13: SparkPlug Missing Arms (1 report)
SparkPlug frame has no arms to hold weapons — needs a body mount point instead.

---

## Bugs — Weapons & Projectiles

### BUG-14: Weapons Showing as Default White Textures (1 report)
All weapon models render with no textures applied.

### BUG-15: Projectiles Are White Balls (2 reports)
All ability/weapon projectiles are default white spheres with no VFX. Burst Fire specifically called out.

### BUG-16: Weapons Showing at Feet (1 report)
Weapon models attach to wrong bone/position — appearing at player's feet instead of hands.

---

## Bugs — VFX

### BUG-17: Ambient & Combat VFX Are White Placeholders (3 reports)
All ambient VFX and dungeon effects are still just white pixels/circles. Need real VFX implemented.

---

## Bugs — Companion (BIT)

### BUG-18: BIT Appears as Small Blue Circle (1 report)
Companion has no proper model or visual representation.

---

## Bugs — Dungeon Visuals & Layout

### BUG-19: Floor Tile Mapping Issues (1 report)
Floor tiles look broken/misaligned.

### BUG-20: Overlapping Wall Types (1 report)
Multiple wall types overlapping, showing walkable areas that aren't actually walkable.

### BUG-21: No Foliage or Decor in Rooms (2 reports)
Rooms lack decorative elements — need moss, debris, mechanical parts themed per sector.

### BUG-22: Boss Rooms Have Giant Black/Purple Circle (1 report)
Visual artifact in boss room layouts.

### BUG-23: Rooms Spawning Outside Dungeon Bounds (1 report)
Minimap shows rooms generating outside the dungeon boundary.

---

## Bugs — Dungeon Mood System

### BUG-24: All 6 Mood Variants Non-Functional (6 reports)
- **Frozen**: Just adds a small blue triangle
- **Overgrown**: No overgrown elements added
- **Red Alert**: Just one red circle on the ground
- **Dark**: Rooms just go super dark, no visual variation
- **Toxic**: Nothing visible
- **Scorched**: No fire or burnt objects

---

## Bugs — Room Type Mechanics

### BUG-25: Shop Rooms Not Functional (1 report)
Shops use default green boxes, shopkeeper statue not interactable.

### BUG-26: Puzzle Rooms Blank (1 report)
No puzzle content implemented — rooms are empty.

### BUG-27: Event Rooms Not Hooked Up (1 report)
Events exist but mechanics aren't functional.

### BUG-28: Megabonk Rooms Not Working (1 report)
No Megabonk content or mechanics.

---

## Bugs — Runtime Errors

### BUG-29: LootBoxModel "transparency" Tween Error (2 reports)
Tweened property "transparency" doesn't exist on LootBoxModel. Property name likely wrong.

### BUG-30: Node look_at() After Removal (1 report)
A node calls `look_at()` after being removed from the scene tree. Needs `IsInsideTree()` guard.

### BUG-31: Projectile TryRicochet Signal Blocking (1 report)
`Projectile.TryRicohet()` sets `Monitorable` during Area3D signal callback. Needs `SetDeferred()`.

### BUG-32: ObjectDisposedException (1 report)
Code accesses a disposed Godot object (enemy or projectile already freed).

---

## Bugs — HUD & UI

### BUG-33: Strings Reference "Mana"/"HP" Instead of "Battery"/"Armor" (1 report)
Localization strings not updated to match junkbot robot theme.

### BUG-34: Item Icons Use Human-Style Items (1 report)
Icons show rifles, boots, chestpieces — not themed for robots.

### BUG-35: Hit Feedback Makes Enemies Flat Red (1 report)
Damage flash too aggressive — should be an opaque sheen that fades.

### BUG-36: Minimap Blank at Start (1 report)
Should show layout within 1 block of player but hide room types until entered.

### BUG-37: Game Window Doesn't Fit Screen (1 report)
Can't see lower portions of the game window.

---

## Bugs — Gameplay

### BUG-38: New Game Starts with 1 Bronze Box (1 report)
Loot box count not resetting on new game.

---

## Bugs — Editor Issues

### BUG-39: Combatant Editor Issues (5 reports)
- Corrupted Sentry always shows dirty flag asterisk
- Corrupted Sentry plays all animations on reselect
- Animation previewer non-functional for all combatants
- "Add Ability" button does nothing
- Base stat changes don't persist or give feedback

### BUG-40: Dungeon Editor Issues (6 reports)
- Lighting too dark to see details
- Collision display doesn't show selected object's collision
- Asset bounding box shows wrong size
- No transform tools (trim, cut, paint, color, lighting)
- Material override just changes color, not actual material
- Missing spawn points, enemy spawning/playback, navmesh toggle

### BUG-41: UI/Layout Editor Issues (4 reports)
- Layout mode fights directional input making items hard to move
- Changes in layout/UIUX editor don't appear in-game
- Options go off screen, need scroll and collapsible sections
- HUD editor missing abilities, commentary box elements

### BUG-42: Sound/VFX/Balance Editor Issues (4 reports)
- Need sound layering, speed/morph controls, naming
- Adjustment levers don't work
- Scale box exists but no levers for size/shape
- Balance editor needs an "ALL" tab

### BUG-43: Safe Room Visual in Editor (1 report)
Safe room in editor doesn't look like a safe room.

---

## Feature Requests Filed as Bugs

### FR-06: Ctrl+B Bug Report Shortcut In-Game
Currently only works in editor — should work during gameplay too.

### FR-07: Bug Report Dialog Should Pause Game
Game continues running while filing a bug report.

### FR-08: Double Perk Tree Size with Stronger Endpoints
Expand passive tree with more powerful endpoint perks. *(Partially done — tree expanded to ~216 nodes)*

### FR-09: PoE-Style Class-Locked Perk Tree Starts
Each bot frame class starts at a different position in the perk tree with class-locked sections.

### FR-10: Build Tips System
AI-assisted build advice feature (like a "llama tool") suggesting optimal perk/gear combos.

### FR-11: Richer Character Sheet
Character sheet needs more strategic info for planning builds.

### FR-12: Sector Editor Visual Feedback
Sector editor needs difficulty graphs and visual feedback on scaling.

### FR-13: Enemy Combat Barks/Dialogue
Enemies should have audio barks and dialogue during combat.

### FR-14: Socketable Equipment Visible on Model
Rare items with sockets should visually appear on the player model (shoulder rocket launchers, etc.).

### FR-15: Editor — Mesh Editing Tools
Full mesh editing (cut, paint materials, multiple collision boxes).

### FR-16: Editor — Animations, Sheens, Audio Triggers
More editing options for animations, visual effects, and audio in the editor.

### FR-17: Bug Report Screenshot Preview & Annotation
Bug menu should preview screenshots with annotation/boxing tools.

### FR-18: Balance Editor — Visual Impact Data
Balance editor needs data visualization for impact analysis.

---

## Priority Summary

| Priority | Items | Impact |
|----------|-------|--------|
| **Critical** | BUG-01, BUG-02 | Fixes ~40% of all reports (asset loading) |
| **High** | ~~BUG-07–09~~, BUG-11–16, BUG-24 | Core visual experience broken (BUG-07–09 FIXED) |
| **Medium** | BUG-17–23, BUG-25–28, BUG-29–32, FR-01–05 | Polish and missing features |
| **Low** | BUG-33–43, FR-06–18 | Editor improvements, QoL, UI theming |
