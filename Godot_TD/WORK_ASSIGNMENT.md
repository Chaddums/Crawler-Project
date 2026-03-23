# Work Assignment

*Solo dev (Stu). Adam left 2026-03-22. Squad system is historical — all tasks assigned to Stu.*

---

## Progress Overview

### Phases
- [x] **Phase 0 — Cleanup** (delete dead code, strip floors, terminology)
- [x] **Phase 1 — The Run Works** (wave curve, entry points, extraction, milestones, mining rigs, towers, maps) — mostly done, maps/rigs by Adam
- [ ] **Phase 2 — The Meta Works** (territory, suits, boss mode, relics, tower customization) — territory + suits + boss runs implemented (S4), relics + debrief remaining
- [x] **Phase 3 — Narrative** (BIT commentary + AXIS rewrite + memory bleed) — DONE
- [x] **Phase 4 — Ascendants** (spawn system, inter-Ascendant combat AI, map chaos, 4 profiles) — DONE
- [ ] **Phase 5 — Make It A Real Game** (see PHASE5_GAME_DESIGN.md) — planet content, enemy behaviors, audio, VFX, balance, the hook

### Core Systems
- [x] Continuous wave system (no floors) — **Stu S2**
- [x] Dynamic entry points (map expands at milestones) — **Adam S3** — DONE (Shield Wall system)
- [x] Exponential extraction curve — **Stu S2**
- [x] Wave milestone events — **Stu S2**
- [x] Three mining rig variants — **Adam S3** — DONE
- [x] White towers work by default — **Stu S5** — DONE
- [x] Tower customization / modular slots — **Stu S5** — DONE
- [x] Territory unlock system — **Stu** — DONE (TerritoryData + TerritoryManager + TerritoryLoader + territory.json + MetaPerkSave persistence)
- [x] Suits system (save/load builds) — **Stu S4** — DONE (SuitData + SuitManager + capture/apply/destroy + persistence)
- [ ] Relic system — **Stu**
- [x] Boss run mode — **Stu S4** — DONE (GameManager boss flow + VineWaveManager boss trigger + BossConfirmScreen)
- [x] Character barks (BIT + AXIS) — **Stu** — DONE (BITCommentary + AXIS rewrite + memory bleed framework)

### UX Flow
- [ ] Main menu → Planet select → Meta layer — **Stu**
- [x] Meta hub — territory map where players spend meta resources to unlock planet sections, view unlocked/locked sections, see costs and prerequisites — **Stu S4** — DONE (TerritoryScreen)
- [ ] Run start — select planet, then select unlocked territory section to play, then pick mining rig, draft towers, drop in — **Stu**
- [x] In-game HUD (wave number, extraction counter, rig status) — **Stu S2** — DONE
- [x] Perk select on milestones — **Stu S2** — DONE
- [ ] Debrief screen — show extraction total, transfer extracted resources to MetaResources for spending in the meta hub. "How far did you push it?" then flow into meta hub to spend. — **Stu**
- [x] Boss run entry (suit at risk confirmation) — **Stu S4** — DONE (BossConfirmScreen)
- [ ] Home base ship visual — **Stu**
- [x] Tower build bar (simplified, slot selection) — **Stu S5** — DONE
- [ ] Relic inventory UI — **Stu**
- [ ] Scene transitions — **Stu**
- [x] Pause menu + Settings — **Stu** — DONE (run analysis, network stats, strategic hints, spire health bar, settings panel)

### Integration (connects systems to UX)
- [ ] Debrief → MetaResources transfer — when a run ends, TotalExtracted gets added to MetaPerkSaveData.MetaResources and persisted. This is how players earn currency for territory unlocks.
- [ ] Territory section → Run start — when player selects a territory section in the meta hub, the run loads that section's map layout and wave set variant. Unlocked sections show as playable, locked ones show cost + prerequisites.
- [x] Boss gating — boss run option only appears in the meta hub if TerritoryManager.IsBossUnlocked() returns true for that planet. Player must unlock all prerequisite sections first. — **DONE (TerritoryScreen checks GatesBoss + unlock state)**

### Editor Tools
- [x] Wave Milestone Designer — **Stu S2** — DONE
- [x] Extraction Curve Tuner — **Stu S2** — DONE
- [x] Wave Editor extension (continuous) — **Stu S2** — DONE
- [ ] Signal Tuning extension (mining rigs) — **Stu**
- [ ] Territory Map Editor — **Stu**
- [ ] Suit Inspector — **Stu**
- [ ] Dialogue Editor — **Stu**
- [ ] Sound Designer extension — **Stu**

---

## Completed Squads

### Squad S1 — Cleanup ~~`squad/cleanup`~~ MERGED TO DEV
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

### Squad S2 — Waves `squad/waves`
**COMPLETE.**

- [x] **1.1** Waves never stop. One continuous escalating sequence per planet instead of 6 separate floors. Enemies get harder every wave (more HP, faster, more armor). All scaling values come from JSON. `L` — DONE
- [x] **1.3** The longer you survive, the more you earn. Resources per wave follow an exponential curve so wave 15 pays 5x more than wave 8. This is what makes players say "just one more wave." `M` — DONE
- [x] **1.4** Special events fire at specific wave numbers (wave 5, 10, 15, etc.). These trigger: perk selection, new entry points opening, difficulty spikes, and later Ascendant appearances. Defined in JSON per planet. `M` — DONE
- [x] **UX2** Update the HUD: show wave number instead of floor, add a live extraction counter that ticks up during waves, show which mining rig you're using and its current mode. `M` — DONE
- [x] **UX3** Perk selection (pick 1 of 3) currently triggers when a floor ends. Rewire it to trigger at wave milestones instead. Same screen, different trigger. `M` — DONE
- [x] **T1** F12 editor tool: visual timeline showing all milestones for a planet. Drag to move them, click to edit what each one triggers. `M` — DONE (merged into Wave Editor)
- [x] **T2** F12 editor tool: visual curve editor for the extraction reward scaling. X = wave number, Y = resources. Drag control points. See side-by-side comparison of "quit at wave 8" vs "quit at wave 15." `M` — DONE (new Extraction tab)
- [x] **T3** Extend the existing F12 Wave Editor to work with continuous waves instead of floors. Show milestone markers on the timeline. Add commander config and difficulty preview per wave. `M` — DONE

**Files owned:** VineWaveManager.cs, VineWaveLoader.cs, VineWaveData.cs, DifficultyScaler.cs, Data/Waves/*, Data/difficulty_scaling.json, Data/milestones.json (new), Scripts/Editor/ (wave modules)

---

### Squad S5 — Towers `squad/towers`
**COMPLETE.**

- [x] **1.7** Towers just work when you place them. No sensor or signal chain needed for basic towers to shoot. Players start with one tower class. Advanced signal chains become optional for players who want deeper builds. `M` — biggest gameplay change
- [x] **2.6** Towers have slots you can put components into. Different components change what the tower does. Towers next to each other can create synergies (e.g., Gatling + chain stun = enemies permanently slowed). This is the new build depth mechanic. `L`
- [x] **UX4** Simplified tower build bar. Show available towers, highlight open slots on selected tower, component selection dropdown. `M`

**Files owned:** VineNode.cs, VineNodeData.cs, VineDraftScreen.cs, TowerSlotSystem.cs (new)

---

## Remaining Tasks (all Stu)

### Squad S3 — Map (Stu) `squad/map`

- [x] **1.2** Dynamic entry points (Shield Wall system with flexible triggers) `M` — DONE
- [x] **1.5** Three mining rig variants `M` — DONE
- [ ] **1.8** Build 20 different map layouts in the editor. Open arenas, tight corridors, asymmetric mazes, wide fields. Play each one. Figure out which shapes make the game feel good. `M`
- [ ] **UX5** New run start screen: pick your planet, pick your mining rig type (show stats and difficulty), draft your towers, drop in. `M`
- [ ] **T4** Extend the F12 Signal Tuning tab: add sliders for materials generation rate per rig type, mining toggle speed, and per-rig defense stats (turret DPS, shield regen rate, pushback force). `S`

**Files owned:** VineGrid.cs, VinePathfinder.cs, VineHarvester.cs, VinePlacer.cs, ConversionDome.cs, Data/Levels/*, Scripts/Editor/ (signal tuning)

---

### Squad S4 — Meta (Stu) `squad/meta`
**CORE SYSTEMS DONE. UI polish and editor tools remaining.**

- [x] **2.1** Planet map with sections you unlock by spending extracted resources. Fixed costs, no RNG. Unlocking sections opens new map variants and gates boss fights. All data in JSON. `M` — DONE (TerritoryData + TerritoryManager + TerritoryLoader + TerritoryScreen + territory.json)
- [x] **2.2** Save a successful tower build as a "suit." Bring that suit into a boss run fully loaded. If you die on the boss run, the suit is destroyed. 3 suit slots. Unlimited use in farming, consumed in boss runs. `L` — DONE (SuitData + SuitManager + capture/apply/destroy + user://suits.json persistence). TODO: suit capture UI trigger after successful farming run, TowerSlotSystem component serialization.
- [x] **2.4** Boss runs are separate from farming. Pick a planet, pick a suit, confirm you're risking it. Start at wave 1 with your suit's build pre-placed. Boss appears at a late wave milestone. Win = section cleared + reward. Die = suit gone. `M` — DONE (GameManager.StartBossRun + VineWaveManager boss trigger + BossConfirmScreen + OnBossRunComplete/OnBossRunFailed)
- [x] **UX1** Main menu redesign — planet select, remove old buttons, meta layer access `S` — DONE by S1
- [ ] **UX6** The between-runs hub screen. Navigate between: territory map, suit inventory, node shop, relic inventory, and a "Start Run" button. Should feel like a home base, not a menu stack. `M` — TerritoryScreen exists but full hub not yet wired
- [x] **UX7** Boss run confirmation screen. Show the suit you're bringing, preview the build, big warning: "This suit will be destroyed if you fail." Deliberate, no accidental boss runs. `M` — DONE (BossConfirmScreen)
- [ ] **UX8** Visual space where your suits are displayed physically on mannequins or racks, not just a list. Walk around or orbit camera. `M`
- [ ] **UX9** Browse your saved suits. See the tower layout, material type, attached relics, stats. Name them. Drag relics onto suit slots. `M` — LoadoutsScreen has basic suit display, needs full detail view
- [ ] **T5** F12 editor tool: visual editor for the planet unlock tree. Define sections, set costs, configure what each unlock gates. Drag to rearrange. Export to JSON. `M`
- [ ] **T6** F12 editor tool: inspect a serialized suit. See the grid layout, material type, upgrades. Create test suits for debugging boss runs. Verify save/load works correctly. `M`

**Files owned:** TerritoryData.cs (VineLogic/), SuitData.cs, SuitManager.cs, TerritoryScreen.cs, BossConfirmScreen.cs, LoadoutsScreen.cs, MetaPerkSave.cs (extend), Data/territory.json, Scenes/Territory.tscn, Scenes/BossConfirm.tscn

---

### Squad S6 — Polish (Stu) `squad/polish`

- [ ] **2.5** Relics drop during farming runs and go into a persistent inventory. You can bring a limited number into boss runs. Some relics are cosmetic-only flex items (choosing looks over power = skill flex). Some have negative tradeoffs that enable powerful combos (POE2 style). `M`
- [x] **3.1** Short character barks (BIT + AXIS). `M` — DONE (BITCommentary + AXIS rewrite + memory bleed framework, Phase 3)
- [ ] **1.6** When the Spire is destroyed, show the debrief screen: total resources extracted, what wave you reached, personal best comparison. Framing is "how far did you push it?" not "you lost." Every run should feel like it mattered. `M`
- [ ] **UX10** Relic inventory screen. Browse your collected relics, see their stats and tradeoffs, drag them onto suit slots. `M`
- [ ] **UX11** After the debrief score screen, transition smoothly into the meta layer where you spend what you earned. No jarring scene switch. `M`
- [ ] **UX12** Fade transitions between all major screens: menu → meta hub → gameplay → debrief → back to meta. `S`
- [ ] **T7** F12 editor tool: write BIT and AXIS bark lines in a UI instead of raw JSON. Tag each line with its trigger (wave start, leak, milestone, etc.). Preview how it looks in-game. Track line count per pool. `M`
- [ ] **T8** Extend the F12 Sound Designer: preview BIT/AXIS voice lines with their visual style, map Ascendant arrival and clash sounds to audio events. `S`

**Files owned:** AXISCommentary.cs, BITCommentary.cs, RelicData.cs (new), RelicManager.cs (new), RelicInventoryUI.cs (new), VineHUD.cs (debrief), Scripts/Editor/ (dialogue + sound modules)

---

## Unassigned

- [x] **UX13** Pause menu with run analysis, network stats, strategic hints, spire health bar. `M` — DONE
- [x] **UX14** Settings panel inside pause menu: SFX/Music volume sliders, fullscreen toggle. `S` — DONE
- [ ] **UX15** First-time player tooltips: "Place your mining rig," "Build towers to defend," "Press Space to start the wave." Only shows on first run. `M`

---

## Key Files

| File | S4 Changes |
|------|------------|
| Constants.cs | SCENE_TERRITORY, SCENE_BOSS_CONFIRM, MAX_SUIT_SLOTS |
| GameEvents.cs | OnTerritoryUnlocked, OnBossSectionCleared, OnSuitSaved/Destroyed/Equipped, OnBossDefeated, OnBossRunComplete |
| Enums.cs | RunMode.BossRun, GamePhase.Territory/SuitInventory/BossConfirm |
| GameManager.cs | TerritorySave, boss run flow, suit equip, territory unlock |

---

## Resolved

- **Grunt Mech model fixed** — `decoy_unit.fbx` is actually `Robots_Grunt.FBX` (InvisGun Hero 2016 pack). Textures (`GRUNT_red.png`) were gitignored. Import `root_scale` was 100 (should be 1). Renamed internally to "Grunt Mech" everywhere. FBX file kept as `decoy_unit.fbx` to avoid reimport churn.

---

## Rules

1. **Pull before you start.** `git pull origin dev`
2. **Commit messages:** `S#: what you did` (e.g., `S4: implement territory unlock system`)
3. **Check the box** when a task is done. Push the update.
4. All work on `dev` branch (solo dev, no squad branches needed).
