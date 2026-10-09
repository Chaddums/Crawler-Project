using Godot;
using System.Collections.Generic;

namespace JunkyardTD
{
    /// <summary>
    /// Pause menu with run analysis. Shows extraction progress, network stats,
    /// strategic hints, and Spire health trend. Information, not instructions.
    /// </summary>
    public partial class PauseMenu : CanvasLayer
    {
        private PanelContainer _overlay;
        private GamePhase _previousPhase;

        public override void _Ready()
        {
            ProcessMode = ProcessModeEnum.Always;
            Visible = false;
            // Above the HUD, AXIS's lines (layer 10) and the Spire menu: AXIS used to talk over the title
            Layer = 30;
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.Escape)
            {
                // Perk overlay owns the pause state while open
                if (VinePerkScreen.IsOverlayOpen) return;

                if (Visible)
                    Resume();
                else if (GameManager.Instance?.CurrentPhase != GamePhase.MainMenu &&
                         GameManager.Instance?.CurrentPhase != GamePhase.Victory &&
                         GameManager.Instance?.CurrentPhase != GamePhase.Defeat)
                    Open();

                GetViewport().SetInputAsHandled();
            }
        }

        public void Open()
        {
            _previousPhase = GameManager.Instance?.CurrentPhase ?? GamePhase.Build;
            GetTree().Paused = true;
            Visible = true;
            BuildUI();
        }

        public void Resume()
        {
            GetTree().Paused = false;
            Visible = false;
            // Opening the menu doesn't change the phase, so only restore it if something
            // (e.g. the debug pause toggle) moved it to Paused — never clobber a phase
            // change that happened while the menu was open.
            if (GameManager.Instance?.CurrentPhase == GamePhase.Paused)
                GameManager.Instance.SetPhase(_previousPhase);
            if (_overlay != null) { _overlay.QueueFree(); _overlay = null; }
        }

        private void BuildUI()
        {
            if (_overlay != null) { _overlay.QueueFree(); _overlay = null; }

            _overlay = new PanelContainer();
            _overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            var bg = new StyleBoxFlat();
            bg.BgColor = new Color(0.03f, 0.04f, 0.06f, 0.92f);
            _overlay.AddThemeStyleboxOverride("panel", bg);
            AddChild(_overlay);

            // Center the content with margins
            var margin = new MarginContainer();
            margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            margin.AddThemeConstantOverride("margin_left", 200);
            margin.AddThemeConstantOverride("margin_right", 200);
            margin.AddThemeConstantOverride("margin_top", 40);
            margin.AddThemeConstantOverride("margin_bottom", 40);
            _overlay.AddChild(margin);

            var scroll = new ScrollContainer();
            scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            scroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            margin.AddChild(scroll);

            var root = new VBoxContainer();
            root.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            root.AddThemeConstantOverride("separation", 12);
            scroll.AddChild(root);

            // ── Header ──
            var header = MakeLabel("EXTRACTION PAUSED", 28, new Color(0.3f, 0.95f, 0.4f));
            header.HorizontalAlignment = HorizontalAlignment.Center;
            root.AddChild(header);

            // ── Run Status ──
            var statusPanel = MakeSectionPanel();
            root.AddChild(statusPanel);
            var statusVBox = (VBoxContainer)statusPanel.GetChild(0);
            statusVBox.AddChild(MakeSectionLabel("RUN STATUS"));
            var statusGrid = MakeGrid(2);
            statusVBox.AddChild(statusGrid);

            var gm = GameManager.Instance;
            var wm = ServiceLocator.TryGet<VineWaveManager>(out var wmgr) ? wmgr : null;
            var harvester = ServiceLocator.TryGet<VineHarvester>(out var harv) ? harv : null;
            var player = ServiceLocator.TryGet<VinePlayer>(out var pl) ? pl : null;
            var grid = ServiceLocator.TryGet<VineGrid>(out var g) ? g : null;

            int currentWave = gm?.CurrentWave ?? 0;
            int totalWaves = wm?.TotalWaves ?? 20;
            int extracted = gm?.TotalExtracted ?? 0;
            float spireHpPct = (harvester != null && harvester.MaxHP > 0)
                ? harvester.CurrentHP / harvester.MaxHP * 100f : 100f;

            AddStatRow(statusGrid, "Wave", $"{currentWave} / {totalWaves}", new Color(0.0f, 0.85f, 0.95f));
            AddStatRow(statusGrid, "Extracted", $"{extracted}", new Color(0.3f, 0.95f, 0.4f));
            AddStatRow(statusGrid, "Resources", $"{gm?.CurrentResources ?? 0}", new Color(0.9f, 0.9f, 0.2f));
            AddStatRow(statusGrid, "Spire HP", $"{spireHpPct:F0}%",
                spireHpPct > 60 ? new Color(0.3f, 0.95f, 0.4f) :
                spireHpPct > 25 ? new Color(0.95f, 0.7f, 0.1f) :
                new Color(0.95f, 0.3f, 0.2f));

            // ── Next milestone ──
            string nextMilestone = GetNextMilestone(currentWave);
            if (nextMilestone != null)
                AddStatRow(statusGrid, "Next Milestone", nextMilestone, new Color(0.95f, 0.7f, 0.1f));

            // ── Extraction projection ──
            var scaler = ServiceLocator.TryGet<DifficultyScaler>(out var ds) ? ds : null;
            if (scaler != null && currentWave > 0)
            {
                int nextWaveBonus = scaler.ComputeExtractionBonus(currentWave + 1);
                int fiveMore = 0;
                for (int w = currentWave + 1; w <= currentWave + 5 && w <= totalWaves; w++)
                    fiveMore += scaler.ComputeExtractionBonus(w);
                AddStatRow(statusGrid, "Next Wave Bonus", $"+{nextWaveBonus}", new Color(0.3f, 0.95f, 0.4f));
                AddStatRow(statusGrid, "Next 5 Waves", $"+{fiveMore}", new Color(0.3f, 0.8f, 0.4f));
            }

            // ── Defences: towers by name and BIT (the old sensor/routing/mod-slot counts described
            // the signal-chain build, which nothing in the roster uses any more) ──
            var netPanel = MakeSectionPanel();
            root.AddChild(netPanel);
            var netVBox = (VBoxContainer)netPanel.GetChild(0);
            netVBox.AddChild(MakeSectionLabel("DEFENCES"));
            var netGrid = MakeGrid(2);
            netVBox.AddChild(netGrid);

            if (grid != null)
            {
                int towerCount = 0;
                var typeCounts = new Dictionary<string, int>();
                for (int x = 0; x < grid.Width; x++)
                    for (int y = 0; y < grid.Height; y++)
                    {
                        var node = grid.GetNode(x, y);
                        if (node?.Data == null) continue;
                        towerCount++;
                        typeCounts[node.Data.Name] = typeCounts.GetValueOrDefault(node.Data.Name, 0) + 1;
                    }
                AddStatRow(netGrid, "Towers", $"{towerCount}", new Color(0.0f, 0.85f, 0.95f));
                if (typeCounts.Count > 0)
                {
                    var sorted = new List<KeyValuePair<string, int>>(typeCounts);
                    sorted.Sort((x1, x2) => x2.Value.CompareTo(x1.Value));
                    var top = new List<string>();
                    for (int i = 0; i < Mathf.Min(4, sorted.Count); i++) top.Add($"{sorted[i].Key} x{sorted[i].Value}");
                    AddStatRow(netGrid, "Most built", string.Join(",  ", top), new Color(0.8f, 0.82f, 0.88f));
                }
                int upgraded = 0;
                for (int x = 0; x < grid.Width; x++)
                    for (int y = 0; y < grid.Height; y++)
                        if (grid.GetNode(x, y) is VineNode un && (un.Level > 1 || un.Branch != null)) upgraded++;
                AddStatRow(netGrid, "Upgraded", upgraded > 0 ? $"{upgraded} (click a tower to upgrade it)" : "none yet: click a tower to upgrade it",
                    new Color(0.45f, 0.82f, 1f));
            }
            int points = GameManager.Instance?.MetaSave?.AvailablePoints ?? 0;
            if (points > 0)
                AddStatRow(netGrid, "Perk points", $"{points} to spend in the Command Center after this run", new Color(1f, 0.8f, 0.35f));

            if (player != null)
            {
                string lv = player.Progression != null ? $"Level {player.Progression.Level}" : "";
                AddStatRow(netGrid, "BIT", $"{lv}   {player.EnemiesKilledPersonally} kills", new Color(0.3f, 0.7f, 1.0f));
                if (SpireStation.Current != null)
                {
                    var st = SpireStation.Current;
                    AddStatRow(netGrid, "BIT damage", $"{player.EffectiveDamage:F0} x {player.EffectiveAttackSpeed:F1}/s, range {player.EffectiveRange:F1}",
                        new Color(0.6f, 0.75f, 1f));
                    AddStatRow(netGrid, "Materials banked", $"{st.MaterialsBanked:F0}", new Color(0.55f, 0.6f, 1f));
                }
            }

            // ── Strategic Hints ──
            var hints = GatherHints(grid, harvester, currentWave, emptySlots: 0);
            if (hints.Count > 0)
            {
                var hintPanel = MakeSectionPanel();
                root.AddChild(hintPanel);
                var hintVBox = (VBoxContainer)hintPanel.GetChild(0);
                hintVBox.AddChild(MakeSectionLabel("OBSERVATIONS"));
                foreach (var hint in hints)
                {
                    var hintLabel = MakeLabel(hint, 13, new Color(0.7f, 0.75f, 0.8f));
                    hintLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                    hintVBox.AddChild(hintLabel);
                }
            }

            // ── Spire Health Trend ──
            if (harvester != null)
            {
                var spirePanel = MakeSectionPanel();
                root.AddChild(spirePanel);
                var spireVBox = (VBoxContainer)spirePanel.GetChild(0);
                spireVBox.AddChild(MakeSectionLabel("SPIRE INTEGRITY"));

                var trendBar = new ProgressBar();
                trendBar.Value = spireHpPct;
                trendBar.CustomMinimumSize = new Vector2(0, 28);
                trendBar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                trendBar.ShowPercentage = false;

                var barStyle = new StyleBoxFlat();
                barStyle.BgColor = new Color(0.08f, 0.08f, 0.1f);
                barStyle.SetCornerRadiusAll(6);
                trendBar.AddThemeStyleboxOverride("background", barStyle);

                var fillStyle = new StyleBoxFlat();
                fillStyle.BgColor = spireHpPct > 60
                    ? new Color(0.2f, 0.7f, 0.3f)
                    : spireHpPct > 25
                        ? new Color(0.8f, 0.6f, 0.1f)
                        : new Color(0.8f, 0.2f, 0.15f);
                fillStyle.SetCornerRadiusAll(6);
                trendBar.AddThemeStyleboxOverride("fill", fillStyle);

                spireVBox.AddChild(trendBar);

                var hpDetail = MakeLabel(
                    $"{harvester.CurrentHP:F0} / {harvester.MaxHP:F0} HP  ({spireHpPct:F0}%)",
                    13, new Color(0.6f, 0.6f, 0.6f));
                hpDetail.HorizontalAlignment = HorizontalAlignment.Center;
                spireVBox.AddChild(hpDetail);
            }

            // ── Buttons ──
            root.AddChild(new HSeparator());
            var btnRow = new HBoxContainer();
            btnRow.AddThemeConstantOverride("separation", 16);
            btnRow.Alignment = BoxContainer.AlignmentMode.Center;
            root.AddChild(btnRow);

            var resumeBtn = new Button();
            resumeBtn.Text = "Resume [ESC]";
            resumeBtn.CustomMinimumSize = new Vector2(160, 40);
            resumeBtn.AddThemeFontSizeOverride("font_size", 16);
            resumeBtn.Pressed += Resume;
            btnRow.AddChild(resumeBtn);

            var settingsBtn = new Button();
            settingsBtn.Text = "Settings";
            settingsBtn.CustomMinimumSize = new Vector2(120, 40);
            settingsBtn.AddThemeFontSizeOverride("font_size", 14);
            settingsBtn.Pressed += ToggleSettings;
            btnRow.AddChild(settingsBtn);

            // Ending early still goes through the debrief so the run's extraction is
            // banked (Quit to Menu used to discard everything extracted this run).
            var quitBtn = new Button();
            quitBtn.Text = GameManager.Instance?.IsBossRun == true ? "Abandon Run" : "End Run";
            quitBtn.TooltipText = GameManager.Instance?.IsBossRun == true
                ? "Abandoning a boss run destroys the equipped suit"
                : "Bank this run's extraction and go to the debrief";
            quitBtn.CustomMinimumSize = new Vector2(140, 40);
            quitBtn.AddThemeFontSizeOverride("font_size", 14);
            quitBtn.AddThemeColorOverride("font_color", new Color(0.9f, 0.4f, 0.3f));
            quitBtn.Pressed += () =>
            {
                GetTree().Paused = false;
                Visible = false;
                GameManager.Instance?.EndRunEarly();
            };
            btnRow.AddChild(quitBtn);

        }

        // ── Settings ──


        private void ToggleSettings()
        {
            // The full settings screen (it used to be two sliders and a button here)
            SettingsScreen.Open(this);
        }

        // ── Strategic Hints ──

        private List<string> GatherHints(VineGrid grid, VineHarvester harvester, int currentWave, int emptySlots)
        {
            var hints = new List<string>();

            // Spire health warning
            if (harvester != null)
            {
                float pct = harvester.CurrentHP / harvester.MaxHP;
                if (pct < 0.25f)
                    hints.Add("Spire integrity is critical. Consider Repair Pulse (E) or defensive positioning.");
                else if (pct < 0.5f)
                    hints.Add("Spire taking significant damage. The trend is not favorable.");
            }

            // Entry point coverage
            if (grid != null)
            {
                var entries = grid.ActiveEntryRegions;
                if (entries != null && entries.Count > 1)
                {
                    // Check if any entry region has very few towers nearby
                    foreach (var entry in entries)
                    {
                        int nearbyTowers = 0;
                        for (int dx = -3; dx <= 3; dx++)
                        {
                            for (int dy = -3; dy <= 3; dy++)
                            {
                                int cx = entry.Center.X + dx;
                                int cy = entry.Center.Y + dy;
                                if (grid.GetNode(cx, cy) != null) nearbyTowers++;
                            }
                        }
                        if (nearbyTowers < 2)
                            hints.Add($"Entry region at ({entry.Center.X}, {entry.Center.Y}) has minimal coverage.");
                    }
                }
            }

            // Milestone approaching
            string next = GetNextMilestone(currentWave);
            if (next != null && next.Contains("wave"))
                hints.Add($"Milestone approaching: {next}. Perk selection incoming.");

            // Extraction encouragement
            var scaler = ServiceLocator.TryGet<DifficultyScaler>(out var ds) ? ds : null;
            if (scaler != null && currentWave >= 8)
            {
                int currentBonus = scaler.ComputeExtractionBonus(currentWave);
                int nextBonus = scaler.ComputeExtractionBonus(currentWave + 1);
                if (nextBonus > currentBonus * 1.1f)
                    hints.Add($"Extraction rate accelerating. Next wave pays {nextBonus} resources.");
            }

            return hints;
        }

        private string GetNextMilestone(int currentWave)
        {
            if (!FileAccess.FileExists("res://Data/milestones.json")) return null;

            var file = FileAccess.Open("res://Data/milestones.json", FileAccess.ModeFlags.Read);
            if (file == null) return null;

            var json = new Json();
            if (json.Parse(file.GetAsText()) != Error.Ok) { file.Close(); return null; }
            file.Close();

            if (json.Data.Obj is not Godot.Collections.Dictionary dict) return null;
            if (!dict.ContainsKey("planets")) return null;
            if (dict["planets"].Obj is not Godot.Collections.Dictionary planets) return null;

            string pk = (GameManager.Instance?.CurrentPlanet ?? 1).ToString();
            if (!planets.ContainsKey(pk)) pk = "1";
            if (!planets.ContainsKey(pk)) return null;
            if (planets[pk].Obj is not Godot.Collections.Dictionary pData) return null;
            if (!pData.ContainsKey("milestones")) return null;
            if (pData["milestones"].Obj is not Godot.Collections.Array arr) return null;

            foreach (var item in arr)
            {
                if (item.Obj is not Godot.Collections.Dictionary entry) continue;
                int mWave = entry.ContainsKey("wave") ? (int)(double)entry["wave"] : 0;
                if (mWave > currentWave)
                {
                    string label = entry.ContainsKey("label") ? (string)entry["label"] : $"wave {mWave}";
                    // The labels carry their own "MILESTONE:" prefix (shown as banners); the row is
                    // already called Next Milestone
                    if (label.StartsWith("MILESTONE:")) label = label["MILESTONE:".Length..].Trim();
                    return $"{label} (wave {mWave})";
                }
            }
            return null;
        }

        // ── UI Helpers ──

        private static Label MakeLabel(string text, int fontSize, Color color)
        {
            var label = new Label();
            label.Text = text;
            label.AddThemeFontSizeOverride("font_size", fontSize);
            label.AddThemeColorOverride("font_color", color);
            return label;
        }

        private static PanelContainer MakeSectionPanel()
        {
            var panel = new PanelContainer();
            panel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.06f, 0.07f, 0.09f, 0.8f);
            style.BorderColor = new Color(0.15f, 0.16f, 0.2f);
            style.SetBorderWidthAll(1);
            style.SetCornerRadiusAll(8);
            style.ContentMarginLeft = 20;
            style.ContentMarginRight = 20;
            style.ContentMarginTop = 14;
            style.ContentMarginBottom = 14;
            panel.AddThemeStyleboxOverride("panel", style);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 8);
            panel.AddChild(vbox);

            return panel;
        }

        private static Label MakeSectionLabel(string text)
        {
            var label = MakeLabel(text, 12, new Color(0.5f, 0.55f, 0.6f));
            return label;
        }

        private static GridContainer MakeGrid(int columns)
        {
            var grid = new GridContainer();
            grid.Columns = columns * 2; // label + value pairs
            grid.AddThemeConstantOverride("h_separation", 16);
            grid.AddThemeConstantOverride("v_separation", 6);
            return grid;
        }

        private static void AddStatRow(GridContainer grid, string label, string value, Color valueColor)
        {
            var lbl = MakeLabel(label, 14, new Color(0.55f, 0.55f, 0.55f));
            lbl.CustomMinimumSize = new Vector2(140, 0);
            grid.AddChild(lbl);
            grid.AddChild(MakeLabel(value, 14, valueColor));
        }
    }
}
