# Work Assignment — Stu + Adam

*Shared reference. Update checkboxes as you go. Pull before checking.*

---

## Progress Overview

### Phases
- [x] **Phase 0 — Cleanup** (delete dead code, strip floors, terminology)
- [ ] **Phase 1 — The Run Works** (wave curve, entry points, extraction, milestones, mining rigs, towers, maps)
- [ ] **Phase 2 — The Meta Works** (territory, suits, boss mode, relics, tower customization)
- [ ] **Phase 3 — Narrative** (character barks)
- [ ] **Phase 4 — Ascendants** (spawn, combat, map chaos — CUT from first playable if needed)

### Core Systems
- [x] Continuous wave system (no floors) — **Stu S2**
- [ ] Dynamic entry points (map expands at milestones) — **Adam S3**
- [x] Exponential extraction curve — **Stu S2**
- [x] Wave milestone events — **Stu S2**
- [ ] Three mining rig variants — **Adam S3**
- [ ] White towers work by default — **Stu S5**
- [ ] Tower customization / modular slots — **Stu S5**
- [ ] Territory unlock system — **Adam S4**
- [ ] Suits system (save/load builds) — **Adam S4**
- [ ] Relic system — **Adam S6**
- [ ] Boss run mode — **Adam S4**
- [ ] Character barks (BIT + AXIS) — **Adam S6**

### UX Flow
- [ ] Main menu → Planet select → Meta layer — **Stu S1**
- [ ] Meta hub (territory, suits, relics, node shop) — **Adam S4**
- [ ] Run start (planet → rig → draft → drop in) — **Adam S3**
- [ ] In-game HUD (wave number, extraction counter, rig status) — **Stu S2**
- [ ] Perk select on milestones — **Stu S2**
- [ ] Debrief screen ("how far did you push it?") — **Adam S6**
- [ ] Boss run entry (suit at risk confirmation) — **Adam S4**
- [ ] Home base ship visual — **Adam S4**
- [ ] Tower build bar (simplified, slot selection) — **Stu S5**
- [ ] Relic inventory UI — **Adam S6**
- [ ] Scene transitions — **Adam S6**
- [ ] Pause menu + Settings — **whoever finishes first**

### Editor Tools
- [ ] Wave Milestone Designer — **Stu S2**
- [ ] Extraction Curve Tuner — **Stu S2**
- [ ] Wave Editor extension (continuous) — **Stu S2**
- [ ] Signal Tuning extension (mining rigs) — **Adam S3**
- [ ] Territory Map Editor — **Adam S4**
- [ ] Suit Inspector — **Adam S4**
- [ ] Dialogue Editor — **Adam S6**
- [ ] Sound Designer extension — **Adam S6**

---

## Stu's Machine (6 Claude instances)

### Squad S1 — Cleanup (2 instances) ~~`squad/cleanup`~~ MERGED TO DEV
**COMPLETE. S2, S3, S5 unblocked.**

- [x] **0.1** Delete Classic TD (WaveManager, WaveData, WaveRegistry, Battle.tscn, BattleScene, MapSelect.tscn, MapSelectUI) `S`
- [x] **0.2** Delete deprecated (HeroBotController, FabricationSystem, ScrapManager) `S`
- [x] **0.3** Strip floor refs (GameManager, GamePhase enum, VineMapLayouts, VineWaveRegistry, perk select trigger) `M` — most invasive task
- [x] **0.4** Address sweep: P#-F#-W#-S# to P#-W#-S# everywhere `S` — no instances found in code
- [x] **0.5** Terminology sweep: Mana→Materials, Gold/Scrap→Resources, Psychic→Chaos, SpawnGroup→Surge `S`
- [x] **0.6** Fix compilation — delete orphaned Classic TD files (HUD, TowerInspector, TowerPlacer, TerrainManipulator), strip remaining floor refs across all files, complete terminology rename across entire codebase `M`
- [x] **UX1** Main menu redesign — planet select, remove old buttons, meta layer access `S`
- [x] **S1 merged to dev** — notify Adam, all waiting squads rebase

**Files owned:** GameManager.cs, Enums.cs (GamePhase), VineMapLayouts.cs, VineWaveRegistry.cs, BattleScene.cs, HeroBotController.cs, FabricationSystem.cs, ScrapManager.cs

---

### Squad S2 — Waves (2 instances) `squad/waves`
**S1 merged — ready to start.**

- [ ] **1.1** Waves never stop. One continuous escalating sequence per planet instead of 6 separate floors. Enemies get harder every wave (more HP, faster, more armor). All scaling values come from JSON. `L` — heaviest lift
- [ ] **1.3** The longer you survive, the more you earn. Resources per wave follow an exponential curve so wave 15 pays 5x more than wave 8. This is what makes players say "just one more wave." `M`
- [ ] **1.4** Special events fire at specific wave numbers (wave 5, 10, 15, etc.). These trigger: perk selection, new entry points opening, difficulty spikes, and later Ascendant appearances. Defined in JSON per planet. `M`
- [ ] **UX2** Update the HUD: show wave number instead of floor, add a live extraction counter that ticks up during waves, show which mining rig you're using and its current mode. `M`
- [ ] **UX3** Perk selection (pick 1 of 3) currently triggers when a floor ends. Rewire it to trigger at wave milestones instead. Same screen, different trigger. `M`
- [ ] **T1** F12 editor tool: visual timeline showing all milestones for a planet. Drag to move them, click to edit what each one triggers. `M`
- [ ] **T2** F12 editor tool: visual curve editor for the extraction reward scaling. X = wave number, Y = resources. Drag control points. See side-by-side comparison of "quit at wave 8" vs "quit at wave 15." `M`
- [ ] **T3** Extend the existing F12 Wave Editor to work with continuous waves instead of floors. Show milestone markers on the timeline. Add commander config and difficulty preview per wave. `M`

**Files owned:** VineWaveManager.cs, VineWaveLoader.cs, VineWaveData.cs, DifficultyScaler.cs, Data/Waves/*, Data/difficulty_scaling.json, Data/milestones.json (new), Scripts/Editor/ (wave modules)

**While waiting for S1:** Design JSON schemas for wave data and milestones.

---

### Squad S5 — Towers (2 instances) `squad/towers`
**S1 merged — ready to start.**

- [ ] **1.7** Towers just work when you place them. No sensor or signal chain needed for basic towers to shoot. Players start with one tower class. Advanced signal chains become optional for players who want deeper builds. `M` — biggest gameplay change
- [ ] **2.6** Towers have slots you can put components into. Different components change what the tower does. Towers next to each other can create synergies (e.g., Gatling + chain stun = enemies permanently slowed). This is the new build depth mechanic. `L`
- [ ] **UX4** Simplified tower build bar. Show available towers, highlight open slots on selected tower, component selection dropdown. `M`

**Files owned:** VineNode.cs, VineNodeData.cs, VineDraftScreen.cs, TowerSlotSystem.cs (new)

**While waiting for S1:** Design the modular slot system. What components? What synergies?

---

## Adam's Machine (6 Claude instances)

### Squad S3 — Map (2 instances) `squad/map`
**S1 merged — ready to start.**

- [ ] **1.2** Enemies start coming from one direction. At wave milestones, new entry points crack open on other sides of the map. By late game, enemies attack from all 4 directions. Schedule defined in JSON per planet. `M`
- [ ] **1.5** Three different mining rigs to choose at run start. Turret Rig has built-in guns. Shield Rig regenerates a protective barrier. Regen Rig heals fast and pushes enemies back. Each plays differently. Stub all three for testing. `M`
- [ ] **1.8** Build 20 different map layouts in the editor. Open arenas, tight corridors, asymmetric mazes, wide fields. Play each one. Figure out which shapes make the game feel good. `M`
- [ ] **UX5** New run start screen: pick your planet, pick your mining rig type (show stats and difficulty), draft your towers, drop in. `M`
- [ ] **T4** Extend the F12 Signal Tuning tab: add sliders for materials generation rate per rig type, mining toggle speed, and per-rig defense stats (turret DPS, shield regen rate, pushback force). `S`

**Files owned:** VineGrid.cs, VinePathfinder.cs, VineHarvester.cs, VinePlacer.cs, ConversionDome.cs, Data/Levels/*, Data/entry_schedule.json (new), Scripts/Editor/ (signal tuning)

**While waiting for S1:** Create map layouts and entry_schedule.json (new files, no conflict).

---

### Squad S4 — Meta (2 instances) `squad/meta`
**Can start immediately — all new files.**

- [ ] **2.1** Planet map with sections you unlock by spending extracted resources. Fixed costs, no RNG. Unlocking sections opens new map variants and gates boss fights. All data in JSON. `M`
- [ ] **2.2** Save a successful tower build as a "suit." Bring that suit into a boss run fully loaded. If you die on the boss run, the suit is destroyed. Achievement milestones unlock suit slots (2-3). Unlimited use in farming, consumed in boss runs. `L` — hardest piece
- [ ] **2.4** Boss runs are separate from farming. Pick a planet, pick a suit, confirm you're risking it. Start at wave 1 with your suit's build pre-placed. Boss appears at a late wave milestone. Win = section cleared + reward. Die = suit gone. `M`
- [x] **UX1** Main menu redesign — planet select, remove old buttons, meta layer access `S` — DONE by S1
- [ ] **UX6** The between-runs hub screen. Navigate between: territory map, suit inventory, node shop, relic inventory, and a "Start Run" button. Should feel like a home base, not a menu stack. `M`
- [ ] **UX7** Boss run confirmation screen. Show the suit you're bringing, preview the build, big warning: "This suit will be destroyed if you fail." Deliberate, no accidental boss runs. `M`
- [ ] **UX8** Visual space where your suits are displayed physically on mannequins or racks, not just a list. Walk around or orbit camera. Adam specifically wanted this. `M`
- [ ] **UX9** Browse your saved suits. See the tower layout, material type, attached relics, stats. Name them. Drag relics onto suit slots. `M`
- [ ] **T5** F12 editor tool: visual editor for the planet unlock tree. Define sections, set costs, configure what each unlock gates. Drag to rearrange. Export to JSON. `M`
- [ ] **T6** F12 editor tool: inspect a serialized suit. See the grid layout, material type, upgrades. Create test suits for debugging boss runs. Verify save/load works correctly. `M`

**Files owned:** TerritoryMap.cs (new), TerritoryData.cs (new), SuitData.cs (new), SuitManager.cs (new), SuitInventoryUI.cs (new), MetaPerkSave.cs (extend), Data/territory.json (new), Scripts/Editor/ (territory + suit modules)

**No waiting.** Start immediately.

---

### Squad S6 — Polish (2 instances) `squad/polish`
**Can start immediately — mostly new files.**

- [ ] **2.5** Relics drop during farming runs and go into a persistent inventory. You can bring a limited number into boss runs. Some relics are cosmetic-only flex items (choosing looks over power = skill flex). Some have negative tradeoffs that enable powerful combos (POE2 style). `M`
- [ ] **3.1** Short character barks. BIT: dry sarcasm about futility, flat observations, never uses exclamation marks. AXIS: dismissive corporate directives, performatively urgent. About 5 lines per 10 waves. Hundreds of variations, system never repeats until all are shown. Keep it light. `M`
- [ ] **1.6** When the Spire is destroyed, show the debrief screen: total resources extracted, what wave you reached, personal best comparison. Framing is "how far did you push it?" not "you lost." Every run should feel like it mattered. `M`
- [ ] **UX10** Relic inventory screen. Browse your collected relics, see their stats and tradeoffs, drag them onto suit slots. `M`
- [ ] **UX11** After the debrief score screen, transition smoothly into the meta layer where you spend what you earned. No jarring scene switch. `M`
- [ ] **UX12** Fade transitions between all major screens: menu → meta hub → gameplay → debrief → back to meta. `S`
- [ ] **T7** F12 editor tool: write BIT and AXIS bark lines in a UI instead of raw JSON. Tag each line with its trigger (wave start, leak, milestone, etc.). Preview how it looks in-game. Track line count per pool. `M`
- [ ] **T8** Extend the F12 Sound Designer: preview BIT/AXIS voice lines with their visual style, map Ascendant arrival and clash sounds to audio events. `S`

**Files owned:** AXISCommentary.cs (rewrite lines), BITCommentary.cs (new), RelicData.cs (new), RelicManager.cs (new), RelicInventoryUI.cs (new), VineHUD.cs (debrief), Scripts/Editor/ (dialogue + sound modules)

**No waiting.** Start immediately.

---

## Either (whoever finishes first)

- [ ] **UX13** Pause menu overlay: Resume, Settings, Quit. Simple. `S`
- [ ] **UX14** Settings screen: volume sliders for SFX and Music (buses already exist in AudioManager), fullscreen toggle. `S`
- [ ] **UX15** First-time player tooltips: "Place your mining rig," "Build towers to defend," "Press Space to start the wave." Only shows on first run. `M`

---

## Shared Files (Lock Before Touching)

| File | Who Might Need It |
|------|-------------------|
| Constants.cs | S1 (cleanup), S2 (wave constants), S3 (entry constants), S5 (tower constants) |
| GameEvents.cs | S2 (OnWaveMilestone), S3 (OnEntryOpened), S4 (OnSuitSaved), S6 (debrief events) |
| Enums.cs | S1 (remove FloorComplete), S3 (MiningRigType), S5 (TowerSlotType), S4 (RunMode) |
| GameManager.cs | S1 (strip floors), S2 (extraction tracking), S4 (run mode) |

Check `.locks/` before editing. Add to the bottom only. Comment your squad `// S#:`.

---

## Work Balance

| | Stu | Adam |
|---|---|---|
| System tasks | 10 | 9 |
| Editor tools | 3 | 5 |
| UX pieces | 3 | 9 |
| **Total items** | **16** | **23** |
| L-size tasks | 2 (wave curve, tower slots) | 1 (suits) |
| Start immediately | 2 instances (S1) | 4 instances (S4, S6) |
| Blocked until S1 | 4 instances (S2, S5) | 2 instances (S3) |

Adam has more items but most are M/S. Stu has fewer but two L-size tasks. Adam gets a head start. Roughly even in total effort.

---

## Timeline

```
Hour 0-2:   S1 cleanup (Stu)
            S4 meta scaffolding (Adam) — new files, no wait
            S6 bark writing + relic scaffolding (Adam) — new files, no wait

Hour 2:     S1 merges to dev. All squads rebase.

Hour 2-6:   S2 wave system + tools (Stu)
            S5 tower simplification (Stu)
            S3 map + entry points + mining rigs (Adam)
            S4 continues suits + meta UX (Adam)
            S6 continues relics + debrief + dialogue tool (Adam)

Hour 6+:    Merge squads to dev one at a time
            Playtest
            Iterate
```

---

## Resolved

- **Grunt Mech model fixed** — `decoy_unit.fbx` is actually `Robots_Grunt.FBX` (InvisGun Hero 2016 pack). Textures (`GRUNT_red.png`) were gitignored. Import `root_scale` was 100 (should be 1). Renamed internally to "Grunt Mech" everywhere. FBX file kept as `decoy_unit.fbx` to avoid reimport churn.

---

## Rules

1. **Pull before you start.** `git pull origin <your-branch>`
2. **Commit messages:** `S#: what you did` (e.g., `S3: add entry_schedule.json for Planet 1`)
3. **Only touch your files.** If you need a shared file, check `.locks/` first.
4. **Check the box** when a task is done. Push the update.
5. **S1 goes first.** Everyone else either waits or works on new files.
6. **Claude merges to dev.** When your squad is done: merge to dev, resolve conflicts, delete the branch.
7. **If two people edited the same file:** pull --rebase, resolve, or flag it.
