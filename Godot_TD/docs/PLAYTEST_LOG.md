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

`BitAce` ("bitSkill": "ace") plays BIT through the player's own inputs (`VinePlayer.BotMove`, `BotAim`): aimed fire held on what matters (a tethering Empowerer, a raider, a bedded siege enemy, then whoever is nearest the Spire), a place to stand in sight of it and 7.5 m from the rest, among the towers when Overclock is up, into the Spire under 35% health and out at 90%, over the crates when nothing is alive. Its first version walked to whoever was worth most, up the canyon to the entry, and fought each fresh pack there alone (laptop, wave 11): it now stays on ground the defence holds (9 m of a tower or drill, 12 m of the Spire). On Relay Station Alpha it walks 45 to 160 m a wave, fires 24 to 125 aimed shots, and was not downed in 12 waves. `qa/configs/campaign/talented.json` puts it with the Greedy builder, Spire and BIT upgrades and balanced perks. Through its campaign (318 waves) BIT walked 31 km, fired 14,000 aimed shots, made 2,156 kills, walked over 161 crates, climbed into the Spire 14 times and was downed 10 times.

What the walking is worth, same bot, same site (Relay Station Alpha), same waves (`WAVE_SEED=1`), one run each: to wave 45 the parked pilot let 38 enemies through and the skilled one 2. Both Spires were still standing when the workspace restarted and cut the runs (the parked one at W63 with 297 of 450 and 16 to 24 through a wave, the skilled one at W45 untouched), so where each ends is not measured yet.

Watching those runs Stu saw "a lot of visual issues, all minor but noticeable": the big player unit not animating, boxes around enemies. The runs had only saved stills at fixed moments, which can't show a thing not moving. What was found and changed, each looked at on the laptop's renderer:

| # | Was | Fix | Check |
|---|---|---|---|
| 47 | A frozen enemy wore a plain translucent box | a cluster of six-sided ice crystals grown up round it (`VineEnemy.IceCrystals`) | `relics/ice_is_crystals`, `relics-sheets` on the laptop |
| 48 | A dozen boxes flew out of every dying enemy and lay round it | debris is a bent five-cornered piece of plate (`VfxLook.Shard`) | `vfx/debris_is_not_a_box`, `vfx-sheets` on the laptop |
| 49 | A Titan standing at its post read as a statue, firing or not: its clips play, but its idle turns bones a few degrees, which can't be seen from the game's camera (five frames over 1.5 s were near identical) | a standing frame leans and nods about its feet, looks 22 degrees either way when it has nothing to shoot, and rocks back on each shot (`PoseStandingFrame`; a bob of 4% of its height was tried first and lifted the Titan's feet off the ground) | `mech/frames/<id>/stands_alive`, `framelook` on the laptop: the body visibly turns over three seconds |

Tools for looking: `framelook` (`qa\frame_look.bat`) measures and photographs BIT on foot and in each frame, standing, walking, shooting and aiming; `ANIM_WATCH=1` says how much of each fight BIT's frame spent on each clip; `qa\suite.bat` runs any suite in a window on the laptop. Ctrl+Shift+B during a bot run files a report with the frame Stu saw (`Godot_TD\bugs\`).

Left as they are, and said so: the rings under enemies (rallied, targeted), the entry pylons (a red cube on a black post), the small dark shapes inside the dome.

`qa\hub_look.bat` (HUB_LOOK) shows the screens between runs as a save sees them, on the laptop's renderer, without playing to them: a new save, a boss fight open, Scrapyard open, the end.

### 2026-11-16: Stu's four answers: dots that count, a boss in the boss fight, a store, an end

The questions the campaign left (items 50 to 52), and Stu's answers: site goals should sit nearer where a careless build breaks ("yes"); asked what site difficulty did (nothing: the map drew the dots and no code read them), he chose tougher enemies for each dot over longer goals; a boss fight should contain a boss, a named one with a trick; banked Resources should buy what other players don't get, and of the kinds offered he picked cosmetics; the last boss fight is the end, and after it a new campaign starts, harder, with the perk tree, the bank and what was bought kept.

**Dots.** Every enemy of a run carries its site's share of health (`Climb.SiteHp`, `site_hp` in `difficulty_scaling.json`, by dots). Where a careless build breaks: single runs at a forced share (`SITE_HP`), the novice bot from its own campaign's saves at that point, the talented bot from its finished save (all its perks, the AXIS threat at x1.76):

| Bot | Site | Goal | AXIS threat | Enemies through before the goal, by enemy health |
|---|---|---|---|---|
| novice | Junction Corridor (Grid Prime, 2 dots) | W10 | x1.00 | x1.4: 0 · x2: 0 · x3: 0 |
| novice | Main Conduit (Grid Prime, 3 dots) | W12 | x1.00 | x1.6: 0 · x2.2: 9 · x3: 38 · x4: 47 |
| novice | Buffer Overflow (Grid Prime, 4 dots) | W14 | x1.10 | x1.7: 11 · x2.3: 22 · x3: 7 · x4: 61 |
| novice | Grid Apex (Grid Prime, 5 dots) | W16 | x1.10 | x1.8: 32 · x2.4: 28 · x3: 41 · x3.2: 76 · x4: broke on W16 · x5: broke on W15 |
| novice | The Foundry (Scrapyard, 4 dots) | W14 | x1.27 | x1.5: 1 · x2: 2 · x2.6: 11 · x3.4: 50 |
| novice | Depth Core (Scrapyard, 6 dots) | W18 | x1.46 | x1.5: 19 · x2: 49 · x2.6: broke on W14 · x3.4: broke on W18 |
| talented | Main Conduit (Grid Prime, 3 dots) | W12 | x1.76 | x4: 59 |
| talented | Grid Apex (Grid Prime, 5 dots) | W16 | x1.76 | x2.4: 3 · x3.2: 20 · x4: broke on W16 · x5: broke on W13 |
| talented | Depth Core (Scrapyard, 6 dots) | W18 | x1.76 | x2: 1 · x2.6: 11 · x3.4: 0 |

At W10 three times the health cost the novice nothing, and on Grid Prime's W16 it took four times to break it. The room shrinks along the campaign: at W18 on Scrapyard, with the threat at x1.46 for the perks it had bought, 2.6 times broke it. The talented bot broke on Grid Apex at four times, where the novice did: on a goal that short the better build buys leaks (20 against 76 at x3.2), not waves. One run a cell, and a run with a good relic and one without differ by more than a step of the table (the novice fell on W14 of Depth Core at x2.6 and reached W18 at x3.4).

The table set from that: one dot x1.0, two x1.3, three x1.6, four x1.9, five x2.2, six x2.5. At those values, one run each from the same saves:

| Bot | Site | Result |
|---|---|---|
| novice | Grid Apex (5 dots) | secured, 46 through |
| novice | The Crucible (Scrapyard, 5 dots) | secured, 79 through, the Spire at 38 of 63 |
| novice | Depth Core (6 dots) | secured, 40 through |
| novice | Forge Gates (6 dots) | lost on W18, 92 through |
| careful (14 of 21 held, threat x1.25) | Grid Apex | secured, 35 through |
| careful | Depth Core | secured, 22 through |
| talented (finished tree) | Depth Core | secured, 54 through |
| talented, second campaign | Depth Core | secured, 34 through |

Before the dots the novice took the same four sites first time with 0, 1, 19 and 1 through.

**Bosses.** `Data/bosses.json`, `Scripts/VineLogic/Bosses/`. The boss comes with the boss wave, walks the route, and the fight is won when it dies. At the Spire it stays and takes 5% of the Spire a second until one of them falls. The Silencer (Grid Prime; the Arcanist builds a tower called Signal Jammer, so the boss has its own name) silences every tower within 4 cells of it. The Overloader (Scrapyard) charges for 3 s every 10 s, a ring closing on it, and bursts: towers within 5 cells lose 60% of their health, BIT in the ring 35% of its own.

First numbers (waves at x1.0, the bots playing as on any site): the novice beat both, and the talented bot lost to The Silencer, its guns standing along the route inside the field. `bossAware` gives a bot what the boss screen says: build round the Spire, don't bank for interest near the boss wave, don't send it early. Then the talented bot killed The Overloader on the route without losing a tower, 6 bursts in 112 s where 11 were due: its Gravity Wells' stuns and the Cryo Fists' freeze (every punch put 0.3 s back on) held the charge, and a Barricade repaired every 3 s kept the boss standing in front of the guns. A boss now takes a stun or freeze only 6 s after the last one let go (`stun_guard`), further chills don't lengthen its freeze, and each blow takes half a Barricade (`barricade_share`). The Overloader's waves went from x1.3 to x1.15: at x1.3 the novice fell on W19, eleven waves before the boss came.

As set (The Silencer `hp` 6500, its waves x1.5; The Overloader 9000, x1.15), one run a row:

| Bot | Boss, its health | Result |
|---|---|---|
| novice (threat x1.17) | The Silencer, 166,231 | lost: 50 through before it, it reached the Spire with 69% of its health and broke it (a run before the stun rule won with the Spire at 35 of 100) |
| careful (x1.25) | The Silencer, 177,516 | won: 21 through, it reached the Spire with 38%, the Spire took 250 of 500 |
| talented (x1.76) | The Silencer, 249,901 | won on the route in 73 s (a run before the stun rule: it reached the Spire with 47% and the Spire was left 81 of 540) |
| talented, second campaign | The Silencer, 312,376 | won: it reached the Spire with 40%, the Spire took 270 of 540 |
| novice (x1.57) | The Overloader, 316,667 | lost twice: 135 and 155 through in the 29 waves before it, it reached the Spire with 65% and 73% |
| talented | The Overloader, 355,256 | won: 7 bursts burned 79 towers and wrecked 23, it reached the Spire with 34%, the Spire took 273 of 420 |
| talented, second campaign | The Overloader, 444,071 | won: 9 bursts, 85 burned, 26 wrecked, it reached the Spire with 36%, the Spire took 273 of 420 |

**The store.** The Company Store, from the Command Center: 16 looks for 544,000 Resources in all (a campaign banked about 230,000 before the dots): seven paints for BIT (its frames wear them too), five trims for the towers' edge light, four sets of lights for the Spire with a crown over its top. Three ask for a finished campaign, one for two. Nothing sold changes how anything fights.

**The end.** The last boss fight's debrief is followed by the ending: AXIS's and BIT's last words, the campaign in numbers, and BEGIN CAMPAIGN II. The next campaign gives every site and region back, keeps the perk tree, the bank, the store's items, the relics and the suits, and adds 25% to every enemy's health for each campaign finished (`campaign_hp_step`).

Seen under the software renderer here (`endlook`; `qa\end_look.bat` does the same on the laptop): the store with seven sets of items on its stage, the ending, a second campaign's Command Center and map, both boss screens, The Silencer among nine dark towers, The Overloader's ring closing and sixteen wrecks after two bursts. Changed after looking: the stage's framing (BIT and the tower were cut off at its edges), crowns fitted to the Spire's column and their glow halved (they bloomed to white), the bosses' rigs the same, The Silencer's ring lit on the ground (it was a darker band of it), the boss bar's plate made solid and moved under the announcement line, SILENCED labels smaller, and every wreck's label on one line once four towers are wrecked (sixteen two-line labels lay over each other). The web page's store card and campaign line were run in a headless browser for their words; how the page lays out with them has not been seen (`qa\hub_look.bat` on the laptop shows it).

**From new saves, with all of it in** (`qa/run_campaign.py`, a bot a save).

The novice held 10 of 21. Through before the goal: none on the one- to three-dot sites, 16 and 20 at four dots, 60, 50 and 85 at five, and on two of those the Spire fell two or three waves after the goal. Then it lost The Silencer's fight six times running: four times on W16 to W23, before the boss came, and twice to the boss, which reached the Spire with 60% and 68% of its health. Each loss paid perk points (14 spent at the first try, 23 at the sixth, at random) and they took the AXIS threat from x1.12 to x1.25: item 53. With the fight's waves at x1.25 for x1.5 (three more tries from that save, `SITE_HP`) it fell on W16, W19 and W23, so the share is left where it was.

The talented bot (it reads the boss screen, and BIT is played) took Campaign I in 21 runs and lost none. Through before the goal: 0 to 19 up to four dots, 13 to 36 at five dots on Grid Prime and 1 to 20 on Scrapyard, 3 and 85 at six. The Silencer (164,160 health) died on the route in 56 s, shrugging off 424 stuns and freezes on the way; The Overloader (312,126) in 64 s at the Spire's foot, after 6 bursts that burned 87 towers and wrecked 6. It bought 10 of the store's 16 looks on the way (173,000 Resources) and took the ending into Campaign II.

Every suite on this build: 3,903 of 3,904 checks pass, the one the audio folder that isn't in the workspace (item 11).

| # | Was | Now | Check |
|---|---|---|---|
| 55 | A site's dots were drawn on the map and read by nothing (51) | each dot is enemy health, x1.0 to x2.5; the map, the wave card and the help say the share | `site/dots_scale_enemy_health`, `site/enemies_carry_the_site`, `site/map_says_what_dots_do`, `site/map_shows_enemy_health` |
| 56 | A boss fight was the ordinary waves held to W25 or W30 (50, 51) | The Silencer and The Overloader; the fight is won when the boss dies and lost when it breaks the Spire | `boss/*` in `endgame` (17 checks) |
| 57 | Grid Prime's boss wave, W25, was an AXIS chaos wave | a boss's wave never is | read (`CorruptionManager.IsChaosWave`) |
| 58 | Banked Resources bought nothing (52) | the Company Store | `store/*` in `endgame` (7 checks) |
| 59 | Nothing followed the last boss fight: every site could be played again (52) | the ending, then the next campaign, harder | `ending/*` in `endgame` (9 checks), `campaign/again_is_harder` |
| 60 | A named boss's health bar (an Ascendant's) shrank about a point 0.6 from its middle, though it is 2.6 wide | it empties from the right like every other bar | read |
| 61 | The built-in Command Center showed neither the bank nor a way to spend it | the bank and the campaign on a line, and a Company Store button | `ending/begins_the_next_campaign` |
| 62 | A boss could be kept still: stuns one after another, a freeze every punch renewed, a Barricade repaired faster than it broke | a stun or freeze takes hold 6 s after the last let go, chills don't lengthen its freeze, a blow takes half a Barricade | `boss/shrugs_off_the_next_stun`, `boss/breaks_a_barricade` |

### Open

| # | Finding | Evidence | Idea |
|---|---|---|---|
| 53 | The perk tree doesn't lift a player who is stuck | the lazy bot spent 20 points at random and lost Signal Watchtower six more times at W11 to W13; the points also made enemies 20% tougher. With the dots the novice lost its first boss fight six times, each loss paying points that raised the threat (x1.12 to x1.25) | measure a tower-lane-only spend; if that fails too, the threat's cost at low counts is the place to look |
| 54 | Three of the relic screen's checks fail on the laptop | `relics/screen_says_relics_are_for_a_run`, `screen_hides_relics_not_found`, `screen_is_a_catalogue` read the built-in screen; with the web view present the page is drawn instead | read the page there, as the hub checks do |
| 63 | The dots bring a weak player's wall forward, by how much is not measured | the lazy bot lost Signal Watchtower (4 dots) six times before the dots and has not been run with them; the novice now loses Forge Gates and the last boss | run it; if it sticks in Grid Prime's first regions, lower the two- to four-dot shares |
| 64 | A dot is enemy health and nothing else | enemy numbers, damage, speed and waves are the same on every site; `wave_set` is still read by nothing | a wave set a region, or take the label out of the data |
| 65 | A boss fight is mostly decided in the 20 s at the Spire | the boss reached the Spire in most fights; the talented bot's same fight with The Silencer ended on the route once and with the Spire at 15% once | more runs a cell before trusting a number; a slower boss with more health would put more of the fight on the route |
| 66 | Not seen on the laptop | the store's stage, the paints, trims and crowns in a run, both bosses on the field, the web page's store card and campaign line | `qa\end_look.bat`, `qa\hub_look.bat` |
| 67 | Ascendants and wave bosses can still be held | the Cryo Fists renew a freeze with every punch and stuns from several towers overlap; only site bosses got the guard | measure a Cryo Fists run against an Ascendant; give it the same guard if it is held |
| 7 | (card factors by block added 2026-11-15, refit once; recheck) The wave card is too soft on hulks, armour and shields and too hard on swarms | after the spacing fix, at a ratio of 0.8 to 1.6: hulk waves leaked more than two 50% of the time, armour and shields 40%, flyers 21%, swarms 0% (six runs) | calibrate `ThreatModel` by block; a TIGHT that means a coin flip in one block and nothing in another can't be planned around |
| 23 | Late enemies still despawn as stuck for the Obelisk | 17 in one run, 10 of them Aegis Rats on one spot in W66 (not a chaos wave): likely slowed to a crawl by tar, a Gravity Well and Living Walls together | count a heavily slowed enemy as held, after checking that is the cause |
| 24 | The Arcanist ran to W83 on seed 1 | 28 waves past the Bruteforge on the same seed; the streak cap of 7 has not stopped long runs | watch a few more seeds; if it holds, the Arcanist's late game (Lens, relays) is the place to look, not the streak |
| 8 | (interest raised to 8%, measured neutral) No reason to save money | every bot ends every wave with 14 to 22 Resources; interest is 2 to 3% of income | fewer, bigger purchases; a reason to bank |
| 9 | (cap 10 to 7; long streaks still happen) The streak can run away | one Arcanist run held 47 waves in a row and reached W75 | lower the cap, or a bad wave costs half the streak |
| 10 | (Spire upgrades and banking taught; BIT upgrades and proactive strikes still not) Bots ignore whole levers | no Spire or BIT upgrades, no early sends, strikes only in trouble | teach the pilot, then measure those levers |
| 35 | `spire/barrage_covers_an_area` fails now and then | 2 runs in 14 on 2026-11-15, "2 of 5 hit"; the shells' pattern makes that under 1 in 1000 | the check now prints each target's damage and place: read the next failure |
| 11 | `audio.folder_exists.UIWhoosh` fails in the container | the Sonniss audio folder is not in the repo | environment only; check on the PC |
