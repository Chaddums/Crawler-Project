# Claude Coordination — Vine Logic TD

*Check this file before starting work. Update it when you pick up or finish a task.*
*Last updated: 2026-03-19*

---

## Active Claude Instances

| # | Working On | Status | Key Files Touched |
|---|-----------|--------|-------------------|
| 1 | Dome visuals / BIT polish / clipping fixes | Active | ConversionDome.cs, VinePlayer.cs, BitPalette.cs |
| 2 | Scrapyard theme / building colors | Active | ScrapyardPlanetTheme.cs, TronTheme.cs |
| 3 | Floor intro flyover (DONE) / general fixes | Active | TDCamera.cs, VineBattleScene.cs, VineHUD.cs, VinePlacer.cs, VineWaveManager.cs |

**If you're a Claude instance, fill in your row before writing code.**

---

## Rules

1. **Read this file first** every conversation
2. **Claim your row** before editing any code files
3. **Don't touch files another Claude is actively editing** — if you need to, note it here and coordinate
4. **Update status** when done (change to `Done` and clear your row for reuse)
5. **Merge conflicts are expensive** — prefer working in different files/systems

---

## Alpha Status: COMPLETE

All features, bugs, visual juice, and balance pass are done. All bug reports archived. Ready to ship.

---

## Recently Completed

| Task | Who | Date |
|------|-----|------|
| Floor intro flyover + forced harvester placement | Claude 3 | 2026-03-19 |
| Sound design (PCM audio) | Claude 1 | 2026-03-19 |
| Corruption/modifier events | Claude | 2026-03-19 |
| Larger battlefields (6 floor layouts) | Claude | 2026-03-19 |
| Mining Building placement flow | Claude | 2026-03-18 |
| 6 floors per planet | Claude B | 2026-03-19 |
| Wave data JSON architecture | Claude B | 2026-03-19 |
| Commander system | Claude B | 2026-03-19 |
| Fog/atmosphere | Claude | 2026-03-18 |
| Draft screen (3 roles) | — | 2026-03-16 |
| Intro cinematic | — | 2026-03-16 |

---

## Conflict Zones (High-Risk Files)

These files are touched by multiple systems. Extra caution:

- **TronTheme.cs** — colors, materials, visual style
- **VineBattleScene.cs** — scene orchestrator
- **VineWaveManager.cs** — wave flow
- **VineHUD.cs** — UI (any new system needs HUD hooks)
- **BitPalette.cs** — player/harvester/tower colors
- **ConversionDome.cs** — dome visuals (active bug target)

---

## How to Use

```
1. Read CLAUDE_COORD.md
2. Pick a task from bug clusters or polish
3. Fill in your row in "Active Claude Instances"
4. Do the work
5. Move task to "Recently Completed"
6. Clear your row
```
