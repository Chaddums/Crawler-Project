# Junkbot Arena - Icon Mapping: game-icons.net SVG Replacements

All SVG paths are relative to `Godot/_downloads/icons/game-icons-net/`.
All current icon paths are relative to `Godot/Icons/`.

Icons are loaded via `Data/icons.json` and resolved by `Scripts/Core/IconLoader.cs` using dot-path lookups (e.g., `IconLoader.Get("abilities.ability_strike")`). Replacing PNGs at the same path requires no code changes. If filenames change, update `icons.json`.

---

## ABILITIES (22 icons)

| Current Icon | Concept | Recommended SVG | Rationale |
|---|---|---|---|
| `Abilities/ability_strike.png` | Basic melee attack | `lorc/punch-blast.svg` | Mechanical punch impact -- no human hand visible, just force |
| `Abilities/ability_shield_bash.png` | Shield slam | `delapouite/shield-bash.svg` | Direct match -- shield being swung forward |
| `Abilities/ability_whirlwind.png` | Spinning AoE attack | `lorc/tornado-discs.svg` | Mechanical spinning discs in tornado pattern -- fits robot spin attack |
| `Abilities/ability_slam.png` | Ground pound | `lorc/quake-stomp.svg` | Ground-shaking stomp impact -- works for a heavy bot slamming down |
| `Abilities/ability_feral_roar.png` | Intimidation/buff shout | `lorc/sonic-shout.svg` | Sonic wave emanating outward -- robot war cry / speaker blast |
| `Abilities/ability_earthquake.png` | Large AoE ground attack | `lorc/meteor-impact.svg` | Massive ground impact with shockwave -- fits seismic bot ability |
| `Abilities/ability_arcane_bolt.png` | Energy projectile | `lorc/plasma-bolt.svg` | Plasma energy bolt -- perfect sci-fi replacement for "arcane" |
| `Abilities/ability_frost_nova.png` | Cryo AoE burst | `delapouite/cryo-chamber.svg` | Cryo/freezing theme -- robotic cold blast |
| `Abilities/ability_meteor.png` | Falling projectile attack | `lorc/burning-meteor.svg` | Direct match -- falling burning projectile |
| `Abilities/ability_backstab.png` | Stealth attack from behind | `lorc/backstab.svg` | Direct match -- works as a mechanical stealth strike too |
| `Abilities/ability_smoke_bomb.png` | Smoke/stealth deploy | `darkzaitzev/smoke-bomb.svg` | Direct match -- smoke grenade deployment |
| `Abilities/ability_assassinate.png` | High-damage single target | `lorc/dead-eye.svg` | Targeting reticle on eye -- precision kill shot, mechanical feel |
| `Abilities/ability_dark_chord.png` | Sound-based dark attack | `lorc/sonic-lightning.svg` | Lightning + sonic waves -- electric sound attack for Noisebox |
| `Abilities/ability_raise_dead.png` | Summon/reactivate units | `lorc/auto-repair.svg` | Auto-repair wrench symbol -- "reactivating" downed bots, not necromancy |
| `Abilities/ability_death_ballad.png` | AoE sound damage over time | `skoll/sound-waves.svg` | Emanating sound waves -- deadly frequency broadcast |
| `Abilities/ability_flurry.png` | Rapid multi-hit attack | `lorc/spinning-blades.svg` | Multiple spinning blades -- rapid mechanical strikes |
| `Abilities/ability_uppercut.png` | Launching melee attack | `lorc/fulguro-punch.svg` | Electric-charged uppercut punch -- mechanical power punch |
| `Abilities/ability_hundred_fists.png` | Rapid punching barrage | `lorc/mailed-fist.svg` | Armored/metal fist -- rapid mechanical punching |
| `Abilities/ability_cannon_blast.png` | Heavy ranged blast | `lorc/ion-cannon-blast.svg` | Ion cannon blast -- perfect sci-fi cannon shot |
| `Abilities/ability_burst_fire.png` | Multi-shot ranged attack | `skoll/laser-burst.svg` | Laser burst pattern -- rapid energy fire |
| `Abilities/ability_snipe_shot.png` | Long-range precision shot | `lorc/laser-precision.svg` | Laser precision targeting -- sniper bot shot |
| `Abilities/ability_rivet_burst.png` | Rivet/projectile spread | `lorc/cogsplosion.svg` | Exploding cogs/gears -- rivets and mechanical shrapnel |

---

## EQUIPMENT - WEAPONS (10 base icons, shared by 20 items in icons.json)

| Current Icon | Concept | Recommended SVG | Rationale |
|---|---|---|---|
| `Items/Equipment/sword.png` | Melee blade weapon | `lorc/energy-sword.svg` | Energy/plasma sword -- sci-fi blade, not medieval |
| `Items/Equipment/dagger.png` | Light melee weapon | `lorc/bolt-saw.svg` | Small bolt-driven sawblade -- junkbot shiv/knife |
| `Items/Equipment/staff.png` | Ranged/caster weapon | `lorc/tesla-coil.svg` | Tesla coil -- electric staff for energy-casting bots |
| `Items/Equipment/pistol.png` | Light ranged weapon | `john-colburn/pistol-gun.svg` | Pistol -- simple sidearm, works as-is |
| `Items/Equipment/rifle.png` | Medium ranged weapon | `sbed/rifle.svg` | Rifle silhouette -- bolt rifle equivalent |
| `Items/Equipment/shotgun.png` | Spread ranged weapon | `sbed/shotgun.svg` | Shotgun -- scatter gun direct match |
| `Items/Equipment/launcher.png` | Heavy ranged weapon | `delapouite/missile-launcher.svg` | Missile launcher -- arc launcher / heavy ordnance |
| `Items/Equipment/repeater.png` | Rapid-fire ranged weapon | `lorc/autogun.svg` | Automatic gun -- repeater / rapid fire weapon |
| `Items/Equipment/shield.png` | Defensive offhand | `lorc/bolt-shield.svg` | Shield with bolt/lightning motif -- electrified scrap buckler |
| `Items/Equipment/ring.png` | Accessory ring (blade ring, buzz saw, coil ring) | `lorc/bolt-saw.svg` | **FLAG: ring.png is used for 3 different items (blade_ring, buzz_saw, coil_ring). Consider splitting.** For buzz_saw: `sbed/circular-saw.svg`. For blade_ring/coil_ring: `delapouite/wire-coil.svg` |

---

## EQUIPMENT - ARMOR (8 base icons, shared by 16 items in icons.json)

| Current Icon | Concept | Recommended SVG | Rationale |
|---|---|---|---|
| `Items/Equipment/helmet.png` | Head armor (cranial_plating) | `delapouite/robot-helmet.svg` | Robot helmet -- direct match for cranial plating |
| `Items/Equipment/chestplate.png` | Chest armor (hull_plating) | `delapouite/chest-armor.svg` | Chest armor plate -- hull plating for bots |
| `Items/Equipment/robe.png` | Light chest armor (capacitor_vest) | `lorc/armor-vest.svg` | Armor vest -- lighter chest piece, capacitor vest |
| `Items/Equipment/greaves.png` | Leg armor (piston_guards) | `delapouite/greaves.svg` | Greaves -- leg armor, works for piston guards |
| `Items/Equipment/boots.png` | Foot armor (tread_plates) | `delapouite/metal-boot.svg` | Metal boots -- heavy mechanical treads |
| `Items/Equipment/gauntlets.png` | Hand armor (servo_grips) | `lorc/mechanical-arm.svg` | Mechanical arm -- servo grip / robot hand piece |
| `Items/Equipment/cloak.png` | Back armor (heat_shroud) | `delapouite/steam.svg` | Steam/heat emanation -- heat shroud visual |
| `Items/Equipment/amulet.png` | Neck accessory (signal_beacon) | `lorc/aerial-signal.svg` | Signal broadcast icon -- signal beacon / antenna module |

---

## CONSUMABLES (8 icons)

| Current Icon | Concept | Recommended SVG | Rationale |
|---|---|---|---|
| `Items/Consumables/potion_health_small.png` | Small repair kit | `sbed/battery-pack.svg` | Small battery pack -- energy/health restore for bots |
| `Items/Consumables/potion_health_medium.png` | Medium repair kit | `sbed/battery-pack-alt.svg` | Alt battery pack -- medium energy restore |
| `Items/Consumables/potion_health_large.png` | Large repair kit | `priorblue/battery-100.svg` | Full battery -- large energy/health restore |
| `Items/Consumables/potion_mana_small.png` | Small energy cell | `priorblue/battery-25.svg` | 25% battery -- small energy/mana cell |
| `Items/Consumables/potion_mana_medium.png` | Medium energy cell | `priorblue/battery-50.svg` | 50% battery -- medium energy/mana cell |
| `Items/Consumables/potion_mana_large.png` | Large energy cell | `priorblue/battery-75.svg` | 75% battery -- large energy/mana cell |
| `Items/Consumables/elixir_fortitude.png` | Defensive buff consumable | `lorc/energy-shield.svg` | Energy shield -- temporary defensive boost |
| `Items/Consumables/overclock_injector.png` | Overclock/speed buff | `lorc/overdrive.svg` | Overdrive symbol -- overclock / performance boost |

---

## BOT FRAMES (6 icons) - Player Classes

| Current Icon | Concept | Recommended SVG | Rationale |
|---|---|---|---|
| `BotFrames/scrapheap.png` | Tank/heavy class (Scrapheap) | `delapouite/battle-mech.svg` | Battle mech -- heavy armored bot frame |
| `BotFrames/tincan.png` | Balanced/defender class (TinCan) | `lorc/robot-golem.svg` | Robot golem -- sturdy defensive bot |
| `BotFrames/sparkplug.png` | Energy/caster class (SparkPlug) | `sbed/tesla.svg` | Tesla symbol -- electric/energy-focused bot |
| `BotFrames/rustbucket.png` | Rogue/stealth class (RustBucket) | `lorc/vintage-robot.svg` | Vintage robot -- old rusty bot aesthetic |
| `BotFrames/noisebox.png` | Bard/sound class (NoiseBox) | `delapouite/speaker.svg` | Speaker icon -- sound-based bot frame |
| `BotFrames/clunker.png` | Brawler/melee class (Clunker) | `delapouite/mecha-head.svg` | Mecha head -- aggressive brawler bot |

---

## ENEMIES (4 regular + 3 bosses)

| Current Icon | Concept | Recommended SVG | Rationale |
|---|---|---|---|
| `Enemies/calibration_target.png` | Training dummy enemy | `lorc/target-dummy.svg` | Target dummy -- direct match for calibration target |
| `Enemies/scrap_rat.png` | Small scavenger enemy | `delapouite/rat.svg` | Rat -- **FLAG: no robotic rat icon exists. Consider `delapouite/spider-bot.svg` as alt (small mechanical pest)** |
| `Enemies/decoy_unit.png` | Decoy/fake enemy | `lorc/target-shot.svg` | Target with shot mark -- decoy unit being shot at |
| `Enemies/wire_worm.png` | Worm-type enemy | `lorc/leeching-worm.svg` | Worm creature -- **FLAG: no mechanical worm. Could also use `delapouite/wire-coil.svg` for wire aesthetic** |
| `Enemies/Bosses/corrupted_sentry.png` | Corrupted guard boss | `lorc/sentry-gun.svg` | Sentry gun -- corrupted automated sentry |
| `Enemies/Bosses/scrap_hydra.png` | Multi-headed boss | `lorc/hydra.svg` | Hydra -- multi-headed mechanical beast |
| `Enemies/Bosses/axis_avatar.png` | Final/major boss | `delapouite/mecha-mask.svg` | Mecha mask -- imposing mechanical face for a major boss |

---

## COMPANIONS (1 icon)

| Current Icon | Concept | Recommended SVG | Rationale |
|---|---|---|---|
| `Companions/bit.png` | Small companion drone (Bit) | `delapouite/delivery-drone.svg` | Drone -- small flying companion bot |

---

## LOOT BOXES (5 icons)

| Current Icon | Concept | Recommended SVG | Rationale |
|---|---|---|---|
| `LootBoxes/bronze.png` | Lowest tier loot box | `delapouite/cardboard-box-closed.svg` | Closed box -- basic loot container |
| `LootBoxes/silver.png` | Second tier loot box | `delapouite/locked-box.svg` | Locked box -- slightly better container |
| `LootBoxes/gold.png` | Third tier loot box | `delapouite/strongbox.svg` | Strongbox -- valuable container |
| `LootBoxes/diamond.png` | Fourth tier loot box | `delapouite/chest.svg` | Chest -- premium container |
| `LootBoxes/legendary.png` | Top tier loot box | `delapouite/mimic-chest.svg` | Mimic chest (most ornate chest icon available) -- **FLAG: no great sci-fi crate icon. Consider `sbed/ammo-box.svg` for military crate feel** |

---

## UI - RARITY (6 icons)

| Current Icon | Concept | Recommended SVG | Rationale |
|---|---|---|---|
| `UI/Rarity/common.png` | Common rarity indicator | `badges/cog.svg` | Single cog -- simple, mechanical, common |
| `UI/Rarity/uncommon.png` | Uncommon rarity | `lorc/cog.svg` | Cog variant -- slightly different from common |
| `UI/Rarity/rare.png` | Rare rarity | `lorc/gears.svg` | Interlocking gears -- more complex = rarer |
| `UI/Rarity/epic.png` | Epic rarity | `lorc/circuitry.svg` | Circuit board pattern -- high-tech = epic tier |
| `UI/Rarity/legendary.png` | Legendary rarity | `lorc/processor.svg` | Processor/CPU chip -- top-tier tech |
| `UI/Rarity/absurd.png` | Absurd/max rarity | `lorc/microchip.svg` | Microchip -- ultimate miniaturized tech, absurd tier |

---

## UI - STATS (6 icons)

| Current Icon | Concept | Recommended SVG | Rationale |
|---|---|---|---|
| `UI/Stats/strength.png` | Strength stat | `lorc/mailed-fist.svg` | Armored metal fist -- mechanical strength. Alt: `lorc/piston.svg` if available, otherwise `delapouite/biceps.svg` is too human. |
| `UI/Stats/dexterity.png` | Dexterity/agility stat | `lorc/sprint.svg` | Sprint/speed silhouette -- **FLAG: somewhat human. Alt: `lorc/gears.svg` or `delapouite/speedometer.svg` for a more mechanical feel** |
| `UI/Stats/intelligence.png` | Intelligence stat | `delapouite/cpu.svg` | CPU chip -- processing power = robot intelligence |
| `UI/Stats/constitution.png` | Constitution/durability stat | `delapouite/chest-armor.svg` | Chest armor -- structural integrity / hull durability. Alt: `lorc/shield-reflect.svg` |
| `UI/Stats/charisma.png` | Charisma stat | `lorc/aerial-signal.svg` | Signal broadcast -- robot charisma = signal strength / broadcast power. Alt: `delapouite/robot-antennas.svg` |
| `UI/Stats/luck.png` | Luck stat | `delapouite/horseshoe.svg` | Horseshoe -- universal luck symbol, works even for robots. Alt: `lorc/clover.svg` |

---

## FLAGGED ISSUES - ICONS NEEDING SPECIAL ATTENTION

### No Good Robotic Match
1. **`Enemies/scrap_rat.png`** - No mechanical rat icon. Best options: `delapouite/spider-bot.svg` (mechanical pest) or `delapouite/rat.svg` (organic rat, re-style with color).
2. **`Enemies/wire_worm.png`** - No mechanical worm. Best options: `lorc/leeching-worm.svg` (organic) or `lorc/centipede.svg` (segmented, more mechanical feel) or `delapouite/wire-coil.svg` (abstract wire).
3. **`UI/Stats/dexterity.png`** - Most agility icons show human figures. `delapouite/speedometer.svg` is the most mechanical alternative.
4. **`UI/Stats/strength.png`** - `lorc/mailed-fist.svg` is armored but still a fist shape. Consider `lorc/gear-hammer.svg` (gear + hammer = mechanical power).

### Shared Icons That Should Be Split
5. **`Items/Equipment/ring.png`** is mapped to 4 different items (blade_ring, buzz_saw, ring, coil_ring). Recommend creating distinct icons:
   - `blade_ring` -> `lorc/spinning-blades.svg` (spinning blade ring)
   - `buzz_saw` -> `sbed/circular-saw.svg` (circular sawblade)
   - `ring` / `coil_ring` -> `delapouite/wire-coil.svg` (coiled wire ring)

### Style Consistency Notes
6. All game-icons.net SVGs are white-on-transparent by default. You will need to:
   - Rasterize to PNG at your target resolution (the repo includes `rasterize-svgs.sh`)
   - Apply color tinting per rarity/category
   - Consider using `colorize-svgs.sh` from the repo for batch coloring

---

## SUMMARY

| Category | Total Icons | Direct Match | Good Thematic Match | Flagged/Weak Match |
|---|---|---|---|---|
| Abilities | 22 | 6 | 14 | 2 |
| Equipment - Weapons | 10 | 4 | 5 | 1 (ring shared) |
| Equipment - Armor | 8 | 2 | 6 | 0 |
| Consumables | 8 | 0 | 8 | 0 |
| Bot Frames | 6 | 0 | 6 | 0 |
| Enemies | 7 | 3 | 2 | 2 |
| Companions | 1 | 0 | 1 | 0 |
| Loot Boxes | 5 | 0 | 3 | 2 |
| UI Rarity | 6 | 0 | 6 | 0 |
| UI Stats | 6 | 0 | 3 | 3 |
| **TOTAL** | **79** | **15** | **54** | **10** |
