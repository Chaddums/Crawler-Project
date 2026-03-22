# Squad Status Board

Last updated: 2026-03-22T02:30:00

| Squad | Branch | Machine | Status | Current Task | Blocking | Notes |
|-------|--------|---------|--------|-------------|----------|-------|
| S1-Cleanup | dev (merged) | Stu | DONE | Phase 0 complete — Builder + Validator passes, deep terminology sweep (36 files), 0 errors | None | Merged to dev, branch deleted |
| S2-Waves | squad/waves | Stu | WAITING | Phase 1.1/1.3/1.4: wave curve, extraction, milestones | S1 | Can design JSON schemas while waiting |
| S3-Map | squad/map | Adam | WAITING | Phase 1.2/1.5/1.8: entry points, mining rigs, map testing | S1 | Can design entry_schedule.json while waiting |
| S4-Meta | squad/meta | Adam | READY | Phase 2.1/2.2/2.4: territory, suits, boss mode | None | All new files, no conflicts |
| S5-Towers | squad/towers | Stu | DONE | Phase 1.7/2.6: towers auto-fire, modular slot system, draft screen updated | None | Build passes, 0 errors |
| S6-Polish | squad/polish | Adam | READY | Phase 2.5/3.1/1.6: relics, barks, debrief | None | Mostly new files |

## Merge Queue

1. [x] S1 → dev (ready for human merge — unblocks S2, S3, S5)
2. [ ] S2 → dev
3. [ ] S3 → dev
4. [ ] S5 → dev
5. [ ] S4 → dev
6. [ ] S6 → dev

## Active Locks

None currently.

## S1 Cleanup Summary

**Build status:** 0 errors, 0 warnings. All broken refs fixed.

**Files deleted (15 total):**
- Classic TD: WaveManager, WaveData, Battle.tscn, BattleScene, MapSelect.tscn, MapSelectUI, HUD, TowerInspector, TowerPlacer, TerrainManipulator
- Deprecated: HeroBotController, FabricationSystem, ScrapManager
- Associated .uid files

**Files modified (46 total across Builder + Validator):** Full terminology sweep + floor ref removal across entire codebase.

**Floor refs stubbed (owning squads will refactor):**
- `VineWaveManager.cs` — floor completion → victory, `_currentFloor` hardcoded to 1. S2 will refactor to continuous.
- `VinePerkScreen.cs` — title says "MILESTONE REACHED", returns to build phase. S2 will rewire to milestone triggers.
- `VineHUD.cs` — floor label cleared, flyover says "EXTRACTION", end screen uses wave count. S6 will build debrief.
- `VineBattleScene.cs` — uses `BuildMap("gateway")`, no floor-conditional economy. S2/S3 will wire planet layout selection.
- `CorruptionManager.cs` — uses waveNum instead of floor for chaos gating.
- `DebugMenu.cs` — floor command is no-op, WinFloor triggers victory.
- `AudioManager.cs` — OnFloorCompleted subscription removed.
- `LevelEditorScene.cs` — CurrentFloor assignment removed.

**Terminology renames applied across all .cs files:**
- `ScrapType` → `ResourceType`, `MagicType` → `MaterialType`
- `MiningMode.Scrap/Magic` → `MiningMode.Resources/Materials`
- All `OnScrap*`/`OnMagic*` events → `OnResources*`/`OnMaterials*`
- `CurrentScrap/Magic` → `CurrentResources/Materials`
- `SpendScrap` → `SpendResources`, `AddScrap` → `AddResources`
- All `SCRAP_*`/`MANA_*` constants → `RESOURCE_*`/`MATERIALS_*`
- `BuildFloor(grid, floor)` → `BuildMap(grid, layoutName)`
- `OnFloorCompleted` → removed (S2 will add OnWaveMilestone)

**Validator deep sweep (36 files):**
- `ScrapValue/Cost/Bonus/Multiplier` → `ResourceValue/Cost/Bonus/Multiplier`
- `CurrentMagic/MaxMagic/MagicRegen/MagicCost` → `CurrentMaterials/MaxMaterials/MaterialsRegen/MaterialsCost`
- `SelectedMagic/MagicAccumulated/SelectMagicType/GetMagicColor` → `SelectedMaterial/MaterialsAccumulated/SelectMaterialType/GetMaterialColor`
- All UI strings: "Scrap:"→"Resources:", "Mana"→"Materials", "Psychic"→"Chaos"
- SFX: `scrap_drop/collect`→`resource_drop/collect`, `floor_complete`→`wave_milestone`
- Perk names: "Mana Surge"→"Materials Surge", "Scrap Windfall"→"Resource Windfall"
- Removed Level Editor button from MainMenuUI
- Remaining "Scrap" is legitimate: enemy names (Scrap Rat), planet (Scrapyard), TowerRarity.Scrap

## S5 Towers Summary

**Build status:** 0 errors. All pre-existing warnings from other squads.

**New file created (1):**
- `TowerSlotSystem.cs` — TowerComponentData, TowerSlotSystem (per-tower slot management, adjacency synergy detection), SynergyEffect struct, TowerComponentRegistry (13 components: 5 barrel, 4 core, 4 frame)

**Files modified (6):**
- `Enums.cs` — Added `TowerSlotType` (Barrel/Core/Frame), `TowerComponentType` (13 component types)
- `Constants.cs` — Added 18 tower slot constants (auto-fire interval, signal boost, component stats, synergy bonuses)
- `GameEvents.cs` — Added `OnComponentSlotted`, `OnComponentRemoved`, `OnSynergyActivated` events + ClearAll cleanup
- `VineNodeData.cs` — Added `AutoFires`, `SlotCount`, `SlotTypes` fields. Updated DamageTower (auto-fire, 2 slots: Barrel+Frame), SlowField (auto-fire, 2 slots: Core+Frame), PushPull (1 Frame slot), BuffEmitter (1 Core slot)
- `VineNode.cs` — Towers auto-fire without signal chains. Signal chains boost damage (1.5x). Added slot system integration, effective stat helpers (range/fire rate/damage modified by components), on-hit effects (CryoBolt slow), idle label shows AUTO/BOOSTED/NO SIGNAL
- `VineDraftScreen.cs` — Towers shown first with "auto-fire" header, slot counts displayed, subtitle updated to explain the system

**Design decisions:**
- Auto-fire towers use `Constants.TOWER_AUTO_FIRE_INTERVAL` (0.5s) base rate, slower than signal-activated (0.4s)
- Signal boost = 1.5x damage multiplier — makes signal chains worthwhile but not required
- 3 synergies: Thermal Shock (Cryo+Incendiary), Arc Network (ChainArc+ChainArc), Suppression Field (RapidFire+RapidFire)
- Synergies check both vine-connected and grid-adjacent towers
- NodeMaxHealth changed to `internal set` for slot system HP modifiers
