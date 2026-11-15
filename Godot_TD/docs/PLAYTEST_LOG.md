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

### Open

| # | Finding | Evidence | Idea |
|---|---|---|---|
| 7 | The wave card is too hard on flyers and too soft on hulks | at a ratio of 0.8 to 1.6, 2% of flyer waves leaked more than two, 50% of hulk waves, 40% of armour waves (9 runs) | calibrate `ThreatModel` per trait after the spacing fix |
| 8 | No reason to save money | every bot ends every wave with 14 to 22 Resources; interest is 2 to 3% of income | fewer, bigger purchases; a reason to bank |
| 9 | The streak can run away | one Arcanist run held 47 waves in a row and reached W75 | lower the cap, or a bad wave costs half the streak |
| 10 | Bots ignore whole levers | no Spire or BIT upgrades, no early sends, strikes only in trouble | teach the pilot, then measure those levers |
| 11 | `audio.folder_exists.UIWhoosh` fails in the container | the Sonniss audio folder is not in the repo | environment only; check on the PC |
