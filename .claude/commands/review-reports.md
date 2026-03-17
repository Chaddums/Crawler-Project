Review all playtest bug reports and feature requests.

**IMPORTANT: Detect the correct project based on current working directory:**
- If working in `Godot_TD/`, read reports from `Godot_TD/bugs/` (no features subfolder — TD uses a flat bugs/ directory)
- If working in `Godot/` or root, read reports from `test-reports/bugs/` and `test-reports/features/`

1. Read all report.md files from the appropriate bug directory for the current project
2. For each report, also note the screenshot path so I can view it if needed
3. Summarize the reports grouped by type (Bugs first, then Features)
4. For each report show: title, date, description (condensed), and screenshot path
5. If there are no reports, say so
6. After the summary, ask if I want to:
   - Look at any specific screenshot
   - Act on any of the bugs/features (investigate, fix, implement)
   - Clear out resolved reports
