# Claude Coordination — Vine Logic TD

*Check this file before starting work. Update it when you pick up or finish a task.*
*Last updated: 2026-03-16*

---

## Active Claude Instances

| # | Working On | Status | Key Files Touched |
|---|-----------|--------|-------------------|
| 1 | ? | ? | ? |
| 2 | ? | ? | ? |
| 3 | (this instance — just got caught up) | Idle | None yet |

**If you're a Claude instance, fill in your row before writing code.**

---

## Rules

1. **Read this file first** every conversation
2. **Claim your row** before editing any code files
3. **Don't touch files another Claude is actively editing** — if you need to, note it here and coordinate
4. **Update status** when done (change to `Done` and clear your row for reuse)
5. **Merge conflicts are expensive** — prefer working in different files/systems

---

## Alpha TODO (Unclaimed)

Pick from here. See `ALPHA_ROADMAP.md` for full context.

| Task | Priority | Key Files | Notes |
|------|----------|-----------|-------|
| Sound design | Tier 2 | New: Scripts/Audio/, TronTheme.cs | Procedural PCM sounds — signal fire, gate open, turret shot, enemy death, wave start/end |
| 2 more map layouts | Tier 2 | VineMapLayouts.cs, VineGrid.cs | Different spawn/core positions, different terrain |
| 3-4 corruption events | Tier 2 | New file likely, VineWaveManager.cs | AXIS mid-wave twists (possession, signal jam, overloader) |
| Tron-ify unit visuals | Tier 3 | TronTheme.cs, VineNode.cs, VineEnemy.cs | Wireframe edges, grid-line overlays, emissive circuit traces |
| Visual juice | Tier 3 | VfxFactory.cs, VineEnemy.cs | Death pops, projectile trails, screen shake |
| Fog/atmosphere overhaul | Tier 3 | VineBattleScene.cs or TronTheme.cs | 6 bug reports — needs clumped Tron fog, higher altitude, camera clip-through, black interiors with edge glow |
| Remove/restyle grey buildings | Tier 3 | VineBattleScene.cs or VineMapBuilder.cs | 2 plain grey buildings need Tron colors or removal |
| More background shapes | Tier 3 | VineBattleScene.cs | More decorative geometry in backdrop |

---

## Recently Completed

| Task | Who | Commit | Date |
|------|-----|--------|------|
| Draft screen (3 roles) | ? | c6b330c5 | 2026-03-16 |
| Intro cinematic | ? | 2eb4b3d7 | 2026-03-16 |
| Tron faction colors | ? | c6b330c5 | 2026-03-16 |
| Alpha roadmap + CLAUDE.md | ? | c6b330c5 | 2026-03-16 |
| Model bug fixes (24 total) | ? | 3817a55a, cc61217f | 2026-03-16 |

---

## Conflict Zones (High-Risk Files)

These files are touched by multiple systems. Extra caution:

- **TronTheme.cs** — colors, materials, visual style (sound, fog, unit visuals all touch this)
- **VineBattleScene.cs** — scene orchestrator (fog, background, corruption events could all land here)
- **VineWaveManager.cs** — wave flow (corruption events, sound triggers)
- **VineHUD.cs** — UI (any new system needs HUD hooks)
- **Enums.cs** — shared enums (new node types, events, etc.)

---

## How to Use

```
1. Read CLAUDE_COORD.md
2. Pick a task from "Alpha TODO (Unclaimed)"
3. Fill in your row in "Active Claude Instances"
4. Do the work
5. Move task to "Recently Completed" with commit hash
6. Clear your row
```
