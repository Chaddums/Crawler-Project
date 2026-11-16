# Playtest log

Findings from bot runs and reviews, newest first. Each entry: what was seen, the evidence, and its state.

## 2026-11-14

### Fixed

| # | Finding | Evidence | Fix |
|---|---|---|---|
| 1 | The Spire's guns threw ObjectDisposedException | 300 to 470 a bot run; the enemy list built in a physics step was reused by the next frame after the step freed its kills | 23cd62de, `roster/no_freed_enemy_after_physics` |
| 2 | Chaos fighters vanished | 6 to 67 a run despawned as "stuck" while standing to fight, nearly all in chaos waves | cfbebf7d, `chaos/fighters_not_despawned_as_stuck`, `chaos/standing_fighter_stays` |
| 3 | Bots never wore relics | every relic from a crate stayed in the catalogue | 316a1da7, `equipRelics` |
| 4 | Every run ended near the same wave | half damage W34, normal W47, double W68; all runs earned within 7% of each other through W20 | themed blocks e3edbdaf, early climb 45a2e69c, clean streak fb280de7 |
| 5 | A HEAVY METAL block finished every run | 27 to 58 leaks a block for all bots: half the enemies at the same spacing brought the wave's health in half the time (and SWARM in twice the time, the easiest block) | spacing scales with the count; `themes/each_theme_shapes_the_wave` checks the time |
| 6 | Relics arrived too late to build around | first relic at W20 or W30, into 100 towers | First Cache after W5: two relics for one slot, wear one; `loot/first_cache_is_a_relic_choice` |

### After the First Cache and spacing fix (six runs, two seeds)

| Bot | Seed 1 | Seed 2 |
|---|---|---|
| Smart Arcanist | W70 | W56 |
| Smart Bruteforge | W49 | W52 |
| Random Bruteforge | W34 | W28 |

Runs now end in different blocks (hulks, flyers, armour, shields, swarm) and the smart bots finish 15 to 40 waves past the random one (3 to 9 before tonight). Every run got its First Cache at W5; three of six bots wore a relic from it at once (the others were offered relics for towers they hadn't built). Stuck despawns 0 to 3 a run, no gun exceptions.

### 2026-11-15: the four open items (nine runs)

| Bot (Bruteforge) | Seed 1 | Seed 2 |
|---|---|---|
| Smart | W49 | W47 |
| Smart, buys Spire upgrades | W54 | W54 |
| Greedy (banks, sends early) | W49 | W47 |

Arcanist W68, Obelisk W66, random Bruteforge W41 (seed 1). Spire upgrades are worth 5 to 7 waves, so the class bots buy them now. Banking at 8% interest is neutral for the Greedy bot (interest 7% of its income against 5%): a choice that neither wins nor loses. The streak cap of 7 left the long holders about where they were (best streaks 46 and 47).

### 2026-11-15: rendered playthrough (three runs, 36 screenshots each)

A Bruteforge bot run with a display (`VISUAL_RUN`), through the First Cache, four themed blocks, the W20 mass opening and the debrief, going through safe rooms with real key presses. Each finding was seen on screen, fixed, and seen fixed in a later run.

| # | Seen | Fix | Check |
|---|---|---|---|
| 12 | The card told a Bruteforge to beat HEAVY METAL with "tar" (an Obelisk tower) | each block's answer by class, from that class's build bar | `themes/answers_fit_the_role` |
| 13 | The block info was six lines of small red text | four lines: block (its colour), what beats it (white), next block (grey), streak (green) | `themes/card_lines_are_separate` |
| 14 | Block announcements ran to two rows of capitals over the "[F] SPIRE" label and lasted under a second at 3x | short line on a dark plate that fits it, timed in real seconds | `themes/announcement_reads` |
| 15 | "THE CANYON IS QUIET: 3 crates to open" came back after the crates were collected | the banner goes when the safe room is left | `loot/safe_room_banner_goes_with_the_crates` |
| 16 | Both relics of the choice said "EQUIP [E]" and neither said what it cost | number keys [1] [2], each relic's cost under it (every relic in a reveal) | `loot/first_cache_choice_is_clear` |
| 17 | The camera was thrown to the map's edge on slow frames (follow weight over 1 from 0.2 s a frame) | weight capped at 1 | `player/camera_holds_through_slow_frames` |
| 18 | A Bruteforge's First Cache offered Gravity Rams (Pneumatic Rams are the Obelisk's) | relics name the towers they need; a class is only offered relics it can use | `loot/relics_fit_the_class` |
| 19 | BIT's panel couldn't be read over a lit tower behind it | darker panel | seen in run 3 |
| 20 | Seven crates in the field each said "loot: walk BIT over it", covering each other; tags of crates taken together piled on one spot | the hint stops after BIT has walked over two; tags stack | `loot/crate_hint_stops_once_learned` |
| 21 | A nine-row reveal ran over AXIS's line | list centred by its real height, the line under it or hidden | seen in run 3 |
| 22 | The debrief listed "Found a relic: Drone Bay" twice and counted five relics for three | a relic worn from the reveal is recorded once | `relics/recorded_once` |

Final headless check on this build (seed 1, all three classes): no exceptions, every First Cache held relics its class can use (Bruteforge and Arcanist Spire relics, Obelisk Living Walls or Gravity Rams). Bruteforge W55, Obelisk W70, Arcanist W83.

Not judged there: lighting and materials (that workspace draws with a software renderer), and the web debrief page. Both were seen on the PC the same day (below).

### 2026-11-15: windowed run on the PC (two runs, 36 screenshots each)

`qa\visual_run.bat` on the laptop: Vulkan Forward+ on an RTX 3050, 60 fps throughout, to W22 in seven and a half minutes, shots in `test-reports\visual_run\`. The first real look at lighting, materials and the web debrief page. Each fix was seen in the second run.

| # | Seen | Fix | Check |
|---|---|---|---|
| 25 | The Bruteforge Spire was a black silhouette in the middle of the screen: Driller.glb's mask texture was exported as its metal and roughness map (metal 0.98, roughness 0.02), and the canyon has no sky for a mirror to show | `ModelLook.Tame`: mirror-metal surfaces become worn steel (colour and normal maps kept); the Spire's model wears the player's rim like the towers | `spire/no_black_metal`, `spire/wears_the_players_rim`, `spire/model_reads_<class>` |
| 26 | Black balls and boxes around the Spire (the dome's small structures): their outline was the accent, the colour of the lit floor under them | paler, wider edge; body a shade off black | `spire/dome_structures_edged_pale` |
| 27 | Towers, crate labels and tower tags showed through the wave card's rows | card background 0.78 to 0.92 | seen in run 2 |
| 28 | "THE CANYON IS QUIET" and its detail line were hard to read over the lit floor | a soft dark plate behind the banner's words | `site_secured/words_on_a_plate` |
| 29 | The web debrief's Continue button was below the fold at 1080p, with nothing on screen saying how to leave | pinned to the bottom, the page scrolls under it | seen in run 2 |

The web debrief page (CEF) draws correctly: headline, the run's numbers, Spire health bars, moments. Loot reveals, the relic choice, block announcements and the card's four lines all read on the real renderer. No exceptions in either run.

The `lookprobe` suite lists every mesh near the Spire with what it's made of and marks the ones that will draw black, so a thing seen on screen can be traced to its node without a display.

### Open

| # | Finding | Evidence | Idea |
|---|---|---|---|
| 7 | (card factors by block added 2026-11-15, refit once; recheck) The wave card is too soft on hulks, armour and shields and too hard on swarms | after the spacing fix, at a ratio of 0.8 to 1.6: hulk waves leaked more than two 50% of the time, armour and shields 40%, flyers 21%, swarms 0% (six runs) | calibrate `ThreatModel` by block; a TIGHT that means a coin flip in one block and nothing in another can't be planned around |
| 23 | Late enemies still despawn as stuck for the Obelisk | 17 in one run, 10 of them Aegis Rats on one spot in W66 (not a chaos wave): likely slowed to a crawl by tar, a Gravity Well and Living Walls together | count a heavily slowed enemy as held, after checking that is the cause |
| 24 | The Arcanist ran to W83 on seed 1 | 28 waves past the Bruteforge on the same seed; the streak cap of 7 has not stopped long runs | watch a few more seeds; if it holds, the Arcanist's late game (Lens, relays) is the place to look, not the streak |
| 8 | (interest raised to 8%, measured neutral) No reason to save money | every bot ends every wave with 14 to 22 Resources; interest is 2 to 3% of income | fewer, bigger purchases; a reason to bank |
| 9 | (cap 10 to 7; long streaks still happen) The streak can run away | one Arcanist run held 47 waves in a row and reached W75 | lower the cap, or a bad wave costs half the streak |
| 10 | (Spire upgrades and banking taught; BIT upgrades and proactive strikes still not) Bots ignore whole levers | no Spire or BIT upgrades, no early sends, strikes only in trouble | teach the pilot, then measure those levers |
| 30 | Tower tags are big | "+10% crew (2 linked) +15% rate in the dome +55% relay" runs a third of the screen's width over the towers and under the wave card | a short tag ("+80%") with the breakdown in the tower's panel |
| 31 | Fireworks burst over the reveal's title | a ring of sparks across "SAFE ROOM: 7 CRATES" in both runs | keep them to the sides |
| 32 | Two background buildings never load | `KB3D_FTW_BldgSmWaterTowers_A_grp.glb` and `KB3D_FTW_BldgSmCheckPoint_A_grp.glb` are named in `AssetLibrary` but only their `.import` files are in the repo; an error and a warning each run | add the models or drop the references |
| 33 | The lit floor inside the dome is the same amber as everything the player owns | enemies' red markers and the route arrows have little contrast on it; the Spire needed a rim to stand out | the art direction's call; a darker fill would also help the text over it |
| 34 | The HUD says "Speed: 1x" in a bot run at 2x | the bot sets the engine's speed, not the game's own | bot runs only |
| 11 | `audio.folder_exists.UIWhoosh` fails in the container | the Sonniss audio folder is not in the repo | environment only; check on the PC |
