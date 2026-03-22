# 12-Claude Real-Time Coordination Protocol

*6 instances on Stu's machine + 6 on Adam's machine, working on the same Godot_TD codebase simultaneously.*

---

## The Problem

Claude instances can't talk to each other directly. They can only:
- Read/write files in the repo
- Use git (branches, commits, pulls)
- Read shared coordination files

So coordination has to be **file-based** and **git-based**.

---

## Architecture: Squads + Branches + Locks

### 1. Squad System

12 instances split into 6 squads of 2. Each squad owns a **feature branch** and a **set of files**. Instances within a squad coordinate via their shared branch. Squads coordinate via the lock file.

| Squad | Branch | Machine | Focus | Owner Files |
|-------|--------|---------|-------|-------------|
| S1-Cleanup | `squad/cleanup` | Stu x2 | Phase 0: delete dead code, strip floors, terminology | GameManager, GamePhase, VineMapLayouts, VineWaveRegistry, MapSelectUI, BattleScene, HeroBotController, FabricationSystem, ScrapManager |
| S2-Waves | `squad/waves` | Stu x2 | Phase 1.1/1.3/1.4: wave curve, extraction, milestones | VineWaveManager, VineWaveLoader, VineWaveData, DifficultyScaler, Data/Waves/*, Data/difficulty_scaling.json |
| S3-Map | `squad/map` | Adam x2 | Phase 1.2/1.5/1.8: entry points, mining rigs, map testing | VineGrid, VinePathfinder, VineHarvester, VinePlacer, ConversionDome, Data/Levels/*, Data/entry_schedule.json |
| S4-Meta | `squad/meta` | Adam x2 | Phase 2.1/2.2/2.4: territory, suits, boss mode | NEW: TerritoryMap, TerritoryData, SuitData, SuitManager, SuitInventoryUI, MetaPerkSave (extend) |
| S5-Towers | `squad/towers` | Stu x2 | Phase 1.7/2.6: white towers, modular slots, tower customization | VineNode, VineNodeData, VineDraftScreen, NEW: TowerSlotSystem, RelicData |
| S6-Polish | `squad/polish` | Adam x2 | Phase 2.5/3.1/1.6: relics, barks, debrief screen | AXISCommentary, NEW: BITCommentary, VineHUD (debrief), NEW: RelicManager, RelicInventoryUI |

### 2. Branch Strategy

```
dev (protected - human merges only)
├── squad/cleanup    (S1 - merges first, unblocks others)
├── squad/waves      (S2 - starts after S1 merges)
├── squad/map        (S3 - starts after S1 merges)
├── squad/towers     (S4 - starts after S1 merges)
├── squad/meta       (S5 - can start immediately on new files)
└── squad/polish     (S6 - can start immediately on new files)
```

**S1 merges to dev first.** All other squads rebase onto dev after S1's merge. This ensures the floor removal and terminology cleanup is the foundation everything else builds on.

S4, S5, S6 can start immediately because they're creating **new files** that don't conflict with cleanup work.

### 3. File Lock System

Shared files that multiple squads might need:

```
Godot_TD/.locks/
├── Constants.cs.lock      (who owns it right now)
├── GameEvents.cs.lock     (who owns it right now)
├── Enums.cs.lock           (who owns it right now)
└── GameManager.cs.lock    (who owns it right now)
```

**Lock file format:**
```
SQUAD: S2-Waves
SINCE: 2026-03-22T14:30:00
ADDING: GameEvents.OnWaveMilestone event
EXPECTED_DONE: 2026-03-22T15:00:00
```

**Rules:**
- Check lock file before touching a shared file
- If locked by another squad, **wait or ask the human to coordinate**
- Create lock file before editing, delete when done
- Lock files are NOT committed to git — they live only in the working directory
- If a lock is stale (>2 hours), the human can clear it

### 4. Coordination File (Real-Time Status)

Each squad updates `Godot_TD/SQUAD_STATUS.md` when starting/finishing work. This file IS committed to git so all instances can see it.

```markdown
## Squad Status Board
Last updated: 2026-03-22T14:30:00

| Squad | Status | Current Task | Blocking | Notes |
|-------|--------|-------------|----------|-------|
| S1 | WORKING | Stripping floor refs from GameManager | None | ETA 30min |
| S2 | WAITING | Needs S1 to finish before wave refactor | S1 | Designing JSON schema meanwhile |
| S3 | WORKING | Creating entry_schedule.json schema | None | New files, no conflicts |
| S4 | WORKING | Building TerritoryData.cs | None | New files, no conflicts |
| S5 | WORKING | White tower base class | None | New files, no conflicts |
| S6 | WORKING | BITCommentary.cs line pools | None | New files, no conflicts |
```

**Update protocol:**
1. Pull before starting any work: `git pull origin dev`
2. Update your squad row in SQUAD_STATUS.md
3. Commit + push the status update
4. Do your actual work
5. Commit + push when done
6. Update status to DONE

---

## Instance Roles Within Each Squad

Each squad of 2 instances splits work by **layer**:

| Role | What They Do |
|------|-------------|
| **Builder** | Writes the new code / makes the changes |
| **Validator** | Writes tests, reviews changes, checks for regressions, updates docs |

This avoids two instances editing the same file simultaneously.

---

## Cross-Machine Sync: Stu ↔ Adam

Both machines push/pull to the same GitHub remote (`origin`). The sync loop:

```
Every instance, before starting work:
  git fetch origin
  git pull origin <squad-branch>
  git pull origin dev (if rebasing)

Every instance, after completing a unit of work:
  git add <specific files>
  git commit -m "S2: wire DifficultyScaler per-wave HP scaling"
  git push origin <squad-branch>
```

**Commit message format:** `S#: <what changed>`
- `S1: delete Classic TD files`
- `S3: add entry_schedule.json for Planet 1`
- `S6: add 15 BIT bark lines for wave milestones`

This makes the git log immediately readable across machines.

---

## Merge Order

```
1. S1 (cleanup) → dev           Human merges, all squads rebase
2. S2 (waves) → dev             After S1, needs clean GameManager
3. S3 (map) → dev               After S1, needs clean VineGrid
4. S5 (towers) → dev            After S1, needs clean VineNode
5. S4 (meta) → dev              Mostly new files, low conflict risk
6. S6 (polish) → dev            Last, needs everything else in place
```

Humans (Stu/Adam) handle merges. If conflicts arise, use Claude to resolve them (it's good at this — the session log confirmed Colladin merged out-of-sync branches in 40 minutes).

---

## Shared File Modification Protocol

For files that multiple squads need to modify (Constants.cs, GameEvents.cs, Enums.cs, GameManager.cs):

1. **Additive only.** Never delete or rename something another squad might use.
2. **Add to the bottom.** New constants, events, enum values go at the end of their section.
3. **Comment your squad.** `// S2: wave milestone events` before your additions.
4. **Check the lock.** If `.locks/<file>.lock` exists, wait.
5. **Keep changes small.** Don't refactor shared files — just add what you need.

---

## Quick Start for Each Instance

When a Claude instance starts, it should:

```
1. Read CLAUDE.md (project context)
2. Read MULTI_CLAUDE_COORDINATION.md (this file)
3. Read SQUAD_STATUS.md (who's doing what)
4. Check .locks/ directory (any shared files locked?)
5. Pull latest from your squad branch
6. Update your squad row in SQUAD_STATUS.md
7. Start working on your assigned files only
```

---

## What Each Squad Needs to Know

### S1-Cleanup (Stu)
You go first. Everyone waits on you. Be fast and clean.
- Delete files listed in Phase 0.1 and 0.2
- Strip floor references (Phase 0.3) — this touches GameManager, Enums, VineMapLayouts, VineWaveRegistry
- Do the terminology sweep (Phase 0.5) — find/replace across everything
- Address format sweep (Phase 0.4)
- **When done:** merge to dev, notify all squads

### S2-Waves (Stu)
Wait for S1 to merge. Then:
- Rip out floor-based wave sequencing in VineWaveManager
- Wire DifficultyScaler for per-wave scaling
- Build exponential extraction curve
- Add GameEvents.OnWaveMilestone
- Create milestone JSON schema

### S3-Map (Adam)
Wait for S1 to merge. Then:
- Dynamic entry points in VineGrid
- Three mining rig variants in VineHarvester
- Create 20 map layouts for testing
- Entry schedule JSON per planet

### S4-Meta (Adam)
Can start immediately — all new files.
- TerritoryData + TerritoryMap UI
- SuitData serialization + SuitManager
- SuitInventoryUI
- Boss run mode in GameManager (needs lock when ready)

### S5-Towers (Stu)
Wait for S1 to merge. Then:
- White tower base functionality in VineNode
- Modular slot system
- Tower customization / component slotting
- Update VineDraftScreen for simplified tower entry

### S6-Polish (Adam)
Can start immediately — mostly new files.
- BITCommentary.cs with bark line pools
- Relic system (RelicData, RelicManager, RelicInventoryUI)
- Debrief screen (extends VineHUD)
- Rewrite AXISCommentary line pools

---

## Emergency: Two Instances Edited the Same File

It will happen. When it does:
1. Don't panic
2. The instance that committed second does: `git pull --rebase origin <branch>`
3. If conflict: resolve manually or ask Claude to resolve
4. If the conflict is complex: flag it in SQUAD_STATUS.md, let the human handle it
5. The session log confirmed Claude can merge complex out-of-sync files in ~40 minutes

---

## Communication Channels

| Channel | What It's For |
|---------|--------------|
| `SQUAD_STATUS.md` | Real-time status (committed to git) |
| `.locks/*.lock` | File-level locking (local only, not committed) |
| Git commit messages | `S#: description` format for readable log |
| Zoom call | Stu + Adam human communication |
| This file | Protocol reference (read-only during work) |
