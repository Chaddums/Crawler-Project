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

### Open

| # | Finding | Evidence | Idea |
|---|---|---|---|
| 7 | (card factors by block added 2026-11-15, refit once; recheck) The wave card is too soft on hulks, armour and shields and too hard on swarms | after the spacing fix, at a ratio of 0.8 to 1.6: hulk waves leaked more than two 50% of the time, armour and shields 40%, flyers 21%, swarms 0% (six runs) | calibrate `ThreatModel` by block; a TIGHT that means a coin flip in one block and nothing in another can't be planned around |
| 8 | (interest raised to 8%, measured neutral) No reason to save money | every bot ends every wave with 14 to 22 Resources; interest is 2 to 3% of income | fewer, bigger purchases; a reason to bank |
| 9 | (cap 10 to 7; long streaks still happen) The streak can run away | one Arcanist run held 47 waves in a row and reached W75 | lower the cap, or a bad wave costs half the streak |
| 10 | (Spire upgrades and banking taught; BIT upgrades and proactive strikes still not) Bots ignore whole levers | no Spire or BIT upgrades, no early sends, strikes only in trouble | teach the pilot, then measure those levers |
| 11 | `audio.folder_exists.UIWhoosh` fails in the container | the Sonniss audio folder is not in the repo | environment only; check on the PC |
