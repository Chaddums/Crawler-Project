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
- [ ] Continuous wave system (no floors) — **Stu S2**
- [ ] Dynamic entry points (map expands at milestones) — **Adam S3**
- [ ] Exponential extraction curve — **Stu S2**
- [ ] Wave milestone events — **Stu S2**
- [ ] Three mining rig variants — **Adam S3**
- [ ] White towers work by default — **Stu S5**
- [ ] Tower customization / modular slots — **Stu S5**
- [ ] Territory unlock system — **Adam S4**
- [ ] Suits system (save/load builds) — **Adam S4**
- [ ] Relic system — **Adam S6**
- [ ] Boss run mode — **Adam S4**
- [ ] Character barks (BIT + AXIS) — **Adam S6**

### UX Flow
- [x] Main menu → Planet select → Meta layer — **Stu S1**
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

### Squad S1 — Cleanup (2 instances) `squad/cleanup`
**GOES FIRST. Unblocks S2, S3, S5.**

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
**Waits for S1 merge.**

- [ ] **1.1** Continuous wave curve — rip out floor-based sequencing, single escalating sequence per planet, wire DifficultyScaler `L` — heaviest lift
- [ ] **1.3** Exponential extraction curve — replace flat wave bonuses, wave 15 >> wave 8 `M`
- [ ] **1.4** Wave milestone system — GameEvents.OnWaveMilestone(int), JSON per planet `M`
- [ ] **UX2** HUD updates — wave number (not floor), live extraction counter, rig status `M`
- [ ] **UX3** Perk select rewire — trigger on milestones instead of floor complete `M`
- [ ] **T1** Tool: Wave Milestone Designer — F12 module, visual timeline `M`
- [ ] **T2** Tool: Extraction Curve Tuner — F12 module, drag-point curve editor `M`
- [ ] **T3** Tool: Wave Editor extension — continuous waves, milestone markers `M`

**Files owned:** VineWaveManager.cs, VineWaveLoader.cs, VineWaveData.cs, DifficultyScaler.cs, Data/Waves/*, Data/difficulty_scaling.json, Data/milestones.json (new), Scripts/Editor/ (wave modules)

**While waiting for S1:** Design JSON schemas for wave data and milestones.

---

### Squad S5 — Towers (2 instances) `squad/towers`
**Waits for S1 merge.**

- [ ] **1.7** White towers work by default — no signal chain required, one class to start `M` — biggest gameplay change
- [ ] **2.6** Tower customization / modular slots — slottable components, adjacent synergies `L`
- [ ] **UX4** Tower build bar — simplified for white towers, slot selection UI `M`

**Files owned:** VineNode.cs, VineNodeData.cs, VineDraftScreen.cs, TowerSlotSystem.cs (new)

**While waiting for S1:** Design the modular slot system. What components? What synergies?

---

## Adam's Machine (6 Claude instances)

### Squad S3 — Map (2 instances) `squad/map`
**Waits for S1 merge.**

- [ ] **1.2** Dynamic entry points — milestones open new spawn regions, entry schedule JSON `M`
- [ ] **1.5** Three mining rig variants — turrets, shields, regen+pushback, stub all three `M`
- [ ] **1.8** Make 20 map variants and playtest — find the fun `M`
- [ ] **UX5** Run start flow — planet → mining rig select → draft → drop in `M`
- [ ] **T4** Tool: Signal Tuning extension — materials rate per rig, defense stats `S`

**Files owned:** VineGrid.cs, VinePathfinder.cs, VineHarvester.cs, VinePlacer.cs, ConversionDome.cs, Data/Levels/*, Data/entry_schedule.json (new), Scripts/Editor/ (signal tuning)

**While waiting for S1:** Create map layouts and entry_schedule.json (new files, no conflict).

---

### Squad S4 — Meta (2 instances) `squad/meta`
**Can start immediately — all new files.**

- [ ] **2.1** Territory unlock system — deterministic, fixed cost, JSON-driven, gates boss runs `M`
- [ ] **2.2** Suits system — achievement slots, serialize builds, consumable for boss, unlimited farming `L` — hardest piece
- [ ] **2.4** Boss run mode — planet + suit select, death = suit lost, victory = section cleared `M`
- [ ] **UX1** Main menu redesign — planet select, remove old buttons, meta layer access `S` — IN PROGRESS
- [ ] **UX6** Meta layer hub — territory → suits → node shop → relics → Start Run `M`
- [ ] **UX7** Boss run entry — suit select → "suit at risk" confirm → go `M`
- [ ] **UX8** Home base ship visual — suits on display, not just menus `M`
- [ ] **UX9** Suit management UI — browse, stats, equip relics, name suits `M`
- [ ] **T5** Tool: Territory Map Editor — F12 module, visual unlock tree editor `M`
- [ ] **T6** Tool: Suit Inspector — F12 module, view/create/debug suits `M`

**Files owned:** TerritoryMap.cs (new), TerritoryData.cs (new), SuitData.cs (new), SuitManager.cs (new), SuitInventoryUI.cs (new), MetaPerkSave.cs (extend), Data/territory.json (new), Scripts/Editor/ (territory + suit modules)

**No waiting.** Start immediately.

---

### Squad S6 — Polish (2 instances) `squad/polish`
**Can start immediately — mostly new files.**

- [ ] **2.5** Relic system — farming drops, persistent inventory, limited boss carry, flex items `M`
- [ ] **3.1** Character barks — BIT sarcasm, AXIS dismissive, ~5 lines per 10 waves, no dupes `M`
- [ ] **1.6** Debrief screen — extraction total, wave reached, personal best `M`
- [ ] **UX10** Relic inventory UI — browse, stats, drag to suit slots `M`
- [ ] **UX11** Debrief → meta transition — score screen flows into meta spend `M`
- [ ] **UX12** Scene transitions — fades between menu → meta → gameplay → debrief `S`
- [ ] **T7** Tool: Dialogue Editor — F12 module, author barks, tag triggers, export JSON `M`
- [ ] **T8** Tool: Sound Designer extension — voice preview, Ascendant SFX `S`

**Files owned:** AXISCommentary.cs (rewrite lines), BITCommentary.cs (new), RelicData.cs (new), RelicManager.cs (new), RelicInventoryUI.cs (new), VineHUD.cs (debrief), Scripts/Editor/ (dialogue + sound modules)

**No waiting.** Start immediately.

---

## Either (whoever finishes first)

- [ ] **UX13** Pause menu — Resume / Settings / Quit overlay `S`
- [ ] **UX14** Settings screen — volume sliders (SFX/Music), fullscreen toggle `S`
- [ ] **UX15** Tutorial hints — first-play tooltips for mining rig, building, wave start `M`

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

## Rules

1. **Pull before you start.** `git pull origin <your-branch>`
2. **Commit messages:** `S#: what you did` (e.g., `S3: add entry_schedule.json for Planet 1`)
3. **Only touch your files.** If you need a shared file, check `.locks/` first.
4. **Check the box** when a task is done. Push the update.
5. **S1 goes first.** Everyone else either waits or works on new files.
6. **Claude merges to dev.** When your squad is done: merge to dev, resolve conflicts, delete the branch.
7. **If two people edited the same file:** pull --rebase, resolve, or flag it.
