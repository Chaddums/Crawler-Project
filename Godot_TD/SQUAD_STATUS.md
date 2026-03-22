# Squad Status Board

Last updated: 2026-03-22T00:30:00

| Squad | Branch | Machine | Status | Current Task | Blocking | Notes |
|-------|--------|---------|--------|-------------|----------|-------|
| S1-Cleanup | squad/cleanup | Stu | DONE | Phase 0 complete — deletions, floor strip, terminology, menu | None | Ready for merge to dev |
| S2-Waves | squad/waves | Stu | WAITING | Phase 1.1/1.3/1.4: wave curve, extraction, milestones | S1 | Can design JSON schemas while waiting |
| S3-Map | squad/map | Adam | WAITING | Phase 1.2/1.5/1.8: entry points, mining rigs, map testing | S1 | Can design entry_schedule.json while waiting |
| S4-Meta | squad/meta | Adam | READY | Phase 2.1/2.2/2.4: territory, suits, boss mode | None | All new files, no conflicts |
| S5-Towers | squad/towers | Stu | WAITING | Phase 1.7/2.6: white towers, modular slots | S1 | Can design slot system while waiting |
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

## S1 Cleanup Notes — Known Broken Refs After Merge

The following files reference deleted systems and will need fixes by their owning squads:

**Files referencing deleted HeroBotController/FabricationSystem/ScrapManager:**
- `Scripts/UI/TowerInspector.cs` — old Classic TD, likely should also be deleted
- `Scripts/Towers/TowerPlacer.cs` — old Classic TD, replaced by VinePlacer
- `Scripts/Map/TerrainManipulator.cs` — old Classic TD
- `Scripts/Testing/Suites/ContentTestSuite.cs` — references SpawnGroup from deleted WaveData

**Files referencing removed CurrentFloor/FloorComplete:**
- `Scripts/VineLogic/VineWaveManager.cs` — S2 will refactor (owns this file)
- `Scripts/VineLogic/VinePerkScreen.cs` — S2 will fix (perk trigger rewire)
- `Scripts/VineLogic/VineHUD.cs` — S6 will fix (owns for debrief)
- `Scripts/VineLogic/VineBattleScene.cs` — needs floor refs removed
- `Scripts/VineLogic/CorruptionManager.cs` — uses CurrentFloor for scaling
- `Scripts/Editor/LevelEditor/LevelEditorScene.cs` — uses CurrentFloor
- `Scripts/Debug/DebugMenu.cs` — has floor debug commands
- `Scripts/Audio/AudioManager.cs` — refs FloorComplete

**Files referencing renamed events/constants (OnScrapChanged→OnResourcesChanged, etc.):**
- Any file subscribing to old event names will need updating when it rebuilds

**Terminology renames applied:**
- `ScrapType` → `ResourceType`
- `MiningMode.Scrap` → `MiningMode.Resources`
- `MiningMode.Magic` → `MiningMode.Materials`
- `MagicType` → `MaterialType`
- `OnScrapChanged` → `OnResourcesChanged`
- `OnScrapDropped` → `OnResourcesDropped`
- `OnScrapCollected` → `OnResourcesCollected`
- `OnMagicChanged` → `OnMaterialsChanged`
- `OnMagicTypeSelected` → `OnMaterialTypeSelected`
- `OnMagicAccumulated` → `OnMaterialsAccumulated`
- `OnPlayerMagicChanged` → `OnPlayerMaterialsChanged`
- `OnFloorCompleted` → removed (S2 will add OnWaveMilestone)
- `CurrentScrap` → `CurrentResources`
- `CurrentMagic` → `CurrentMaterials`
- `STARTING_SCRAP` → `STARTING_RESOURCES`
- `VINE_STARTING_SCRAP` → `VINE_STARTING_RESOURCES`
- `VINE_PLAYER_MAX_MANA` → `VINE_PLAYER_MAX_MATERIALS`
- `VINE_PLAYER_MANA_REGEN` → `VINE_PLAYER_MATERIALS_REGEN`
- `BOSS_SCRAP_VALUE` → `BOSS_RESOURCE_VALUE`
- `PROP_SCATTER_PER_FLOOR` → `PROP_SCATTER_PER_LEVEL`
- `VineMapLayouts.BuildFloor(grid, floor)` → `BuildMap(grid, layoutName)`
