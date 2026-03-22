# Squad Status Board

Last updated: 2026-03-22T01:00:00

| Squad | Branch | Machine | Status | Current Task | Blocking | Notes |
|-------|--------|---------|--------|-------------|----------|-------|
| S1-Cleanup | squad/cleanup | Stu | DONE | Phase 0 complete — all deletions, floor strip, terminology sweep, menu, compilation verified (0 errors) | None | Ready for merge to dev |
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

## S1 Cleanup Summary

**Build status:** 0 errors, 0 warnings. All broken refs fixed.

**Files deleted (15 total):**
- Classic TD: WaveManager, WaveData, Battle.tscn, BattleScene, MapSelect.tscn, MapSelectUI, HUD, TowerInspector, TowerPlacer, TerrainManipulator
- Deprecated: HeroBotController, FabricationSystem, ScrapManager
- Associated .uid files

**Files modified (46 total):** Full terminology sweep + floor ref removal across entire codebase.

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
