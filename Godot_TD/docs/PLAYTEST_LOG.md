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

### 2026-11-15: the PC run's open items (a third run, and two look sweeps)

| # | Was | Fix | Check |
|---|---|---|---|
| 30 | Tower tags ran a third of the screen's width ("+10% crew (2 linked)  +15% rate in the dome  +55% relay") | "+10% crew  +15% dome  +55% relay", smaller, and a change says only what changed ("+20% crew"); the tower's panel keeps the breakdown | `links/tag_is_short`, `links/tag_says_what_changed` |
| 31 | Fireworks burst across the reveal's title and the list | they burst down both sides, their rings clear of the middle | `loot/fireworks_keep_to_the_sides` |
| 32 | Two background buildings logged four errors and a warning each, every run | they are kept out of the repo by `.gitignore` (CheckPoint, WaterTowers); a machine without them leaves them out with one line. A model that is there but not imported still warns | `assets/absent_model_left_out_*` |
| 33 | The floor inside the dome was a bright amber wash; route arrows, enemies and text were lost on it | the ground's fill glow 0.12 to 0.035 (`PlanetTheme.DomeFloorGlow`): cells a fifth as bright, the grid lines twice as far from them, still warm | `spire/dome_floor_glow_is_faint`, seen in run 3 |
| 34 | The HUD said "Speed: 1x" in a bot run at 2x | a bot run shows the engine's speed | seen in run 3 |
| 36 | The First Cache alone opened to "all of it. at once." (seen in run 3) | one crate gets its own kind's line | `loot/one_crate_gets_its_own_line` |

How 33 was measured: `sweep.json` in the shots folder makes the visual run freeze one frame, with three enemies stood on the dome floor, and shoot it under each look in the file (`test-reports\visual_sweep1\` and `visual_sweep2\` on the laptop). Twelve looks showed the wash is the ground shader's fill emission alone: turning off glow, bloom, either light, the dome's rim or the floor's metal changed nothing visible, and the fill at zero left a black floor. Eight more picked the value: cells drew at (104,75,37) at 0.12, (73,50,25) at 0.07, (57,39,20) at 0.05, (44,30,16) at 0.035 and (30,19,11) at 0.02.

On the PC the enemies wear their own textures (a blue Scrap Rat, a grey Rust Hulk). This workspace's import leaves those models without their textures, so the game falls back to the planet's enemy look, a red outline: screenshots made here show enemies the PC doesn't.

### 2026-11-16: the campaign, played from a new save by bots

What "completing the game" is, read from the code and then played: 19 sites and 2 boss fights on 2 planets (`Data/territory.json`). A site is secured by clearing its wave (W8 on the first, W18 on the last) in an ordinary run; three sites take a region, which opens the next and gives a small buff on that planet; a planet's boss fight is the same waves held to W25 (Grid Prime) or W30 (Scrapyard) on the crucible map, and Grid Prime's opens Scrapyard. After the last boss fight every site can be played again. `CampaignPilot` plays it as the Command Center offers it, one run a launch, and `qa/run_campaign.py` loops it from a new save.

Five campaigns, the first four each from a new save:

| Bot | How it plays | Runs | Failed | Held | Game time | Perk points spent | Ended |
|---|---|---|---|---|---|---|---|
| novice | builds without reading anything (random guns, everything spent every build), Bruteforge, BIT parked on one spot, perks bought at random, leaves four waves after the goal | 21 | 0 | 21 of 21 | 5.0 h | 39 of 131 (enemies x1.57) | complete |
| careful | reads the wave card and builds its counters, Arcanist, perks spread evenly | 14 | 0 | 14 of 21 | 3.5 h | 22 | stopped there to free a core; no run lost |
| talented | reads the card, banks for interest and sends waves early, buys Spire and BIT upgrades, Obelisk, BIT walked by the skilled pilot (below), perks spread evenly | 21 | 0 | 21 of 21 | 4.0 h | 43 of 131 (enemies x1.66) | complete |
| lazy | two purchases a build with 70% of its money, towers anywhere, no perks, no relics, BIT left alone, leaves at the goal | 16 | 10 | 6 of 21 | 2.5 h | 0 (15 unspent) | gave up: Signal Watchtower (W14) lost six times running |
| lazy, then spending | the same save, its 15 points and each one earned after spent at random | 6 more | 6 | 6 of 21 | 0.9 h more | 20 (enemies x1.20) | gave up at the same site, reaching W11 to W13 |

What that says:

- **Anyone who keeps building completes it, and never loses a run.** The novice read nothing and chose nothing and took all 21 in 21 runs. The goals (W8 to W18, bosses at W25 and W30) sit under where even random building breaks, so no site asked it for a decision. Its tightest run was the last boss fight: leaks in most waves from W15 on, the Spire down to 87 at its lowest.
- **Playing well changes the margin, not the outcome.** The talented bot finished an hour sooner (4.0 h against 5.0 h) and held the last boss fight with five leaks in thirty waves and a full Spire, where the novice's Spire fell to 87. Both took every site first time.
- **The floor is a player who under-builds, and the perk tree does not lift it.** The lazy bot lost Main Conduit twice (W12), Buffer Overflow twice (W14) and Signal Watchtower six times (W14), and stopped with 15 perk points it had never spent. Given the same save and told to spend them (20 points at random by the end, which also makes enemies 20% tougher) it lost the same site six more times, no further in than before.
- **Every site is the same fight to a different wave.** A site's `difficulty`, `wave_set` and `boss_id` are shown on the map and read by nothing; a boss fight is the ordinary waves held to W25 or W30, with no boss in it.
- **It takes about five hours**, a run being 7 to 25 game minutes. The perk tree is under a third bought at the end, and the banked Resources (230,000 for the novice) buy nothing.
- **Nothing follows the last boss fight.** The Command Center says the campaign is complete and every site can be played again.

`qa\hub_look.bat` and `qa\campaign_run.bat` photographed the stages on the laptop: a new save, a boss fight open, Scrapyard open, the last boss fight won, the end.

Found on the way, and fixed:

| # | Was | Fix | Check |
|---|---|---|---|
| 37 | A run never ended on Buffer Overflow (the arena map): a raider stood on a wall top beside its drill for good, in both campaigns. Raiders walked to the nearest floor cell and then straight at the drill, and were exempt from the stuck despawn | raiders path to a cell beside the drill, climbing where they must; one that can't get there hits the drill's guards or leaves; one that stands still six seconds gives up and walks down (`RaidManager`). The runner kills a run with no wave cleared in seven minutes and names it in `softlocks.txt` | `raid/gets_round_a_tower`, `raid/walks_back_down`, `raid/walled_in_drill_costs_its_guards`, `raid/nothing_left_to_hit_it_leaves`, `raid/path_goes_over_the_wall`; no softlock in the runs since |
| 38 | With a planet's sites all held, the Command Center said "take a boss run from the territory map" and Deploy opened the map; with everything beaten it said the same | the boss fight is the next step, named, with its wave, and Deploy goes to it; the end says the campaign is complete (`TerritoryManager.Campaign`) | `campaign/boss_is_the_next_step`, `campaign/boss_down_opens_the_next_planet`, `campaign/says_when_it_is_complete` |
| 39 | A boss fight won showed "VICTORY" in the HUD's corner for two seconds, the last one included | it says the planet taken and what opens, or "Campaign complete", for six seconds before the debrief (`TerritoryManager.BossWinWords`) | `campaign/boss_win_names_what_opens`, `campaign/last_boss_win_says_complete` |
| 40 | With Grid Prime taken the territory map still opened on it, every site CLEARED, and its corner button offered "Play next: Relay Station Alpha" | the map opens on the planet the next step is on and offers that step: a site, the boss fight, or "Play again" (seen on the laptop) | `campaign/map_offers_the_next_step` |
| 41 | Three suit slots sat behind the Command Center's cards: a person icon and "LOT", or a suit's name cut off, showed in the gap between Relics and Perk Tree (seen on the laptop) | removed; the Suits card counts them | laptop, second look |
| 42 | The boss screen said "the boss comes at wave 25" and the map "BOSS: signal_jammer". No boss comes: the fight is the waves held to that wave | both say "hold to wave 25" | read |
| 43 | In a boss fight the bots read a suit's kit towers as free and never used the kit up: the Mindless bot built 450 free towers and won without a leak, the Counters bot stopped buying anything else, upgraded nothing and lost on the last wave | the placer and the bots settle a placed tower through one method (`VinePlacer.Settle`); both boss fights replayed | `suits/bot_takes_from_the_kit`, `suits/bot_pays_when_the_kit_is_out` |

| 44 | With every boss fight won the Command Center's card read "Next deployment: Campaign complete", the same sentence twice under it, and "Deploy" on a button that opens the map; the last region promised "Unlocks Planet 3" (seen on the laptop) | "The campaign: Complete", one line, "Choose a site"; an unlock no planet answers to says nothing (`TerritoryManager.RewardWords`) | `campaign/no_promise_of_a_planet_that_isnt_there`, laptop |
| 45 | The last boss fight won showed "+1 PERK POINT" (wave 30's milestone was queued first) and went to the debrief before "Campaign complete" had its turn (seen on the laptop) | the boss win goes ahead of what's queued (`Celebration.ShowFirst`) | `celebration/the_big_one_goes_first` |
| 46 | The flow suite's SafeFile check wrote to "user://sandbox/" by name; under SANDBOX_NAME it corrupted a file nobody reads and failed | it asks SafeFile where the file is | flow 93/93 with and without a named sandbox |

### 2026-11-16: a skilled pilot, and what Stu saw watching the bots on the laptop

"The player character does attack but he doesn't move at all. We should have a talented autoplayer as well." Every bot run so far had BIT put on one spot each wave (`BitPilot`): it never walked, kited, chased a raider or picked anything up. In the novice's boss fights it never fired either: nothing came within its 8 metres (`ANIM_WATCH`: "frame titan ... Idle 100%" of every wave).

`BitAce` ("bitSkill": "ace") plays BIT through the player's own inputs (`VinePlayer.BotMove`, `BotAim`): aimed fire held on what matters (a tethering Empowerer, a raider, a bedded siege enemy, then whoever is nearest the Spire), a place to stand in sight of it and 7.5 m from the rest, among the towers when Overclock is up, into the Spire under 35% health and out at 90%, over the crates when nothing is alive. Its first version walked to whoever was worth most, up the canyon to the entry, and fought each fresh pack there alone (laptop, wave 11): it now stays on ground the defence holds (9 m of a tower or drill, 12 m of the Spire). On Relay Station Alpha it walks 45 to 160 m a wave, fires 24 to 125 aimed shots, and was not downed in 12 waves. `qa/configs/campaign/talented.json` puts it with the Greedy builder, Spire and BIT upgrades and balanced perks.

Watching those runs Stu saw "a lot of visual issues, all minor but noticeable": the big player unit not animating, boxes around enemies. The runs had only saved stills at fixed moments, which can't show a thing not moving. What was found and changed, each looked at on the laptop's renderer:

| # | Was | Fix | Check |
|---|---|---|---|
| 47 | A frozen enemy wore a plain translucent box | a cluster of six-sided ice crystals grown up round it (`VineEnemy.IceCrystals`) | `relics/ice_is_crystals`, `relics-sheets` on the laptop |
| 48 | A dozen boxes flew out of every dying enemy and lay round it | debris is a bent five-cornered piece of plate (`VfxLook.Shard`) | `vfx/debris_is_not_a_box`, `vfx-sheets` on the laptop |
| 49 | A Titan standing at its post read as a statue, firing or not: its clips play, but its idle turns bones a few degrees, which can't be seen from the game's camera (five frames over 1.5 s were near identical) | a standing frame shifts its weight (4% of its height), looks 22 degrees either way when it has nothing to shoot, and rocks back on each shot (`PoseStandingFrame`) | `mech/frames/<id>/stands_alive`, `framelook` on the laptop: the body visibly turns over three seconds |

Tools for looking: `framelook` (`qa\frame_look.bat`) measures and photographs BIT on foot and in each frame, standing, walking, shooting and aiming; `ANIM_WATCH=1` says how much of each fight BIT's frame spent on each clip; `qa\suite.bat` runs any suite in a window on the laptop. Ctrl+Shift+B during a bot run files a report with the frame Stu saw (`Godot_TD\bugs\`).

Left as they are, and said so: the rings under enemies (rallied, targeted), the entry pylons (a red cube on a black post), the small dark shapes inside the dome.

`qa\hub_look.bat` (HUB_LOOK) shows the screens between runs as a save sees them, on the laptop's renderer, without playing to them: a new save, a boss fight open, Scrapyard open, the end.

### Open

| # | Finding | Evidence | Idea |
|---|---|---|---|
| 7 | (card factors by block added 2026-11-15, refit once; recheck) The wave card is too soft on hulks, armour and shields and too hard on swarms | after the spacing fix, at a ratio of 0.8 to 1.6: hulk waves leaked more than two 50% of the time, armour and shields 40%, flyers 21%, swarms 0% (six runs) | calibrate `ThreatModel` by block; a TIGHT that means a coin flip in one block and nothing in another can't be planned around |
| 23 | Late enemies still despawn as stuck for the Obelisk | 17 in one run, 10 of them Aegis Rats on one spot in W66 (not a chaos wave): likely slowed to a crawl by tar, a Gravity Well and Living Walls together | count a heavily slowed enemy as held, after checking that is the cause |
| 24 | The Arcanist ran to W83 on seed 1 | 28 waves past the Bruteforge on the same seed; the streak cap of 7 has not stopped long runs | watch a few more seeds; if it holds, the Arcanist's late game (Lens, relays) is the place to look, not the streak |
| 8 | (interest raised to 8%, measured neutral) No reason to save money | every bot ends every wave with 14 to 22 Resources; interest is 2 to 3% of income | fewer, bigger purchases; a reason to bank |
| 9 | (cap 10 to 7; long streaks still happen) The streak can run away | one Arcanist run held 47 waves in a row and reached W75 | lower the cap, or a bad wave costs half the streak |
| 10 | (Spire upgrades and banking taught; BIT upgrades and proactive strikes still not) Bots ignore whole levers | no Spire or BIT upgrades, no early sends, strikes only in trouble | teach the pilot, then measure those levers |
| 35 | `spire/barrage_covers_an_area` fails now and then | 2 runs in 14 on 2026-11-15, "2 of 5 hit"; the shells' pattern makes that under 1 in 1000 | the check now prints each target's damage and place: read the next failure |
| 11 | `audio.folder_exists.UIWhoosh` fails in the container | the Sonniss audio folder is not in the repo | environment only; check on the PC |
