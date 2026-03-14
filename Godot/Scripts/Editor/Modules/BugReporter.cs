using Godot;
using System;
using System.Collections.Generic;
using System.IO;

namespace JunkbotArena.Editor
{
    /// <summary>
    /// Bug Reporter editor tab — file bug reports and feature requests
    /// directly from the F12 editor with auto-captured game context,
    /// live metrics, screenshot, and report history.
    /// </summary>
    public partial class BugReporter : EditorPanel
    {
        public override string PanelName => "Bugs";
        public override Color AccentColor => new(1.0f, 0.35f, 0.35f);

        // Form fields
        private LineEdit _titleEdit;
        private OptionButton _typePicker;
        private OptionButton _severityPicker;
        private HBoxContainer _severityRow;
        private TextEdit _descriptionEdit;

        // Live metrics
        private Label _metricsFps;
        private Label _metricsState;
        private Label _metricsSector;
        private Label _metricsPlayer;
        private Label _metricsEnemies;
        private Label _metricsKills;
        private Label _metricsDebug;
        private Label _metricsThreat;

        // Context
        private TextEdit _contextDisplay;
        private string _capturedContext = "";

        // History
        private ItemList _historyList;
        private Label _historyCountLabel;
        private TextEdit _previewDisplay;
        private readonly List<string> _historyPaths = new();

        protected override void BuildUI(VBoxContainer content)
        {
            var split = new HSplitContainer();
            split.SizeFlagsVertical = SizeFlags.ExpandFill;
            split.SizeFlagsHorizontal = SizeFlags.ExpandFill;
#pragma warning disable CS0618
            split.SplitOffset = 500;
#pragma warning restore CS0618

            split.AddChild(BuildLeftPanel());
            split.AddChild(BuildRightPanel());
            content.AddChild(split);
        }

        // ===== LEFT PANEL: Form + Metrics =====

        private VBoxContainer BuildLeftPanel()
        {
            var panel = new VBoxContainer();
            panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            panel.SizeFlagsVertical = SizeFlags.ExpandFill;
            panel.CustomMinimumSize = new Vector2(400, 0);
            panel.AddThemeConstantOverride("separation", 4);

            // -- NEW REPORT header --
            panel.AddChild(EditorStyles.MakeLabel("NEW REPORT", EditorStyles.FontHeader, AccentColor));
            panel.AddChild(EditorStyles.MakeSeparator());

            // Title
            var titleRow = new HBoxContainer();
            titleRow.AddThemeConstantOverride("separation", 8);
            titleRow.AddChild(EditorStyles.MakeLabel("Title:", EditorStyles.FontBody, EditorStyles.TextSecondary));
            _titleEdit = EditorStyles.MakeLineEdit("Brief summary...");
            _titleEdit.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            titleRow.AddChild(_titleEdit);
            panel.AddChild(titleRow);

            // Type
            var typeRow = new HBoxContainer();
            typeRow.AddThemeConstantOverride("separation", 8);
            typeRow.AddChild(EditorStyles.MakeLabel("Type:", EditorStyles.FontBody, EditorStyles.TextSecondary));
            _typePicker = new OptionButton();
            _typePicker.AddThemeFontSizeOverride("font_size", EditorStyles.FontBody);
            _typePicker.AddItem("Bug");
            _typePicker.AddItem("Feature");
            _typePicker.Selected = 0;
            _typePicker.ItemSelected += OnTypeChanged;
            typeRow.AddChild(_typePicker);
            panel.AddChild(typeRow);

            // Severity
            _severityRow = new HBoxContainer();
            _severityRow.AddThemeConstantOverride("separation", 8);
            _severityRow.AddChild(EditorStyles.MakeLabel("Severity:", EditorStyles.FontBody, EditorStyles.TextSecondary));
            _severityPicker = new OptionButton();
            _severityPicker.AddThemeFontSizeOverride("font_size", EditorStyles.FontBody);
            _severityPicker.AddItem("Low - Visual/cosmetic");
            _severityPicker.AddItem("Medium - Gameplay issue");
            _severityPicker.AddItem("High - Crash/blocker");
            _severityPicker.Selected = 1;
            _severityRow.AddChild(_severityPicker);
            panel.AddChild(_severityRow);

            // Description
            panel.AddChild(EditorStyles.MakeLabel("Description:", EditorStyles.FontBody, EditorStyles.TextSecondary));
            _descriptionEdit = new TextEdit();
            _descriptionEdit.PlaceholderText = "What happened? What did you expect?";
            _descriptionEdit.CustomMinimumSize = new Vector2(0, 120);
            _descriptionEdit.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _descriptionEdit.AddThemeFontSizeOverride("font_size", EditorStyles.FontBody);
            _descriptionEdit.WrapMode = TextEdit.LineWrappingMode.Boundary;
            var descBg = EditorStyles.MakeFlat(EditorStyles.BgField);
            _descriptionEdit.AddThemeStyleboxOverride("normal", descBg);
            panel.AddChild(_descriptionEdit);

            // Buttons
            var btnRow = new HBoxContainer();
            btnRow.AddThemeConstantOverride("separation", 6);

            var submitBtn = EditorStyles.MakeButton("Submit", EditorStyles.FontBody, AccentColor);
            submitBtn.CustomMinimumSize = new Vector2(80, 30);
            submitBtn.Pressed += OnSubmit;
            btnRow.AddChild(submitBtn);

            var copyBtn = EditorStyles.MakeButton("Copy", EditorStyles.FontSmall);
            copyBtn.Pressed += OnCopy;
            btnRow.AddChild(copyBtn);

            var clearBtn = EditorStyles.MakeButton("Clear", EditorStyles.FontSmall, EditorStyles.TextSecondary);
            clearBtn.Pressed += OnClear;
            btnRow.AddChild(clearBtn);

            panel.AddChild(btnRow);

            // -- LIVE METRICS --
            panel.AddChild(EditorStyles.MakeSeparator());
            panel.AddChild(EditorStyles.MakeLabel("LIVE METRICS", EditorStyles.FontSmall, EditorStyles.TextAccent));

            var metricsPanel = new PanelContainer();
            metricsPanel.AddThemeStyleboxOverride("panel", EditorStyles.MakePanel(
                bg: EditorStyles.BgField, border: EditorStyles.BorderColor, borderWidth: 1, margin: 6));

            var metricsVBox = new VBoxContainer();
            metricsVBox.AddThemeConstantOverride("separation", 2);

            _metricsFps = EditorStyles.MakeLabel("FPS: --", EditorStyles.FontSmall, EditorStyles.TextPrimary);
            _metricsState = EditorStyles.MakeLabel("State: --", EditorStyles.FontSmall, EditorStyles.TextPrimary);
            _metricsSector = EditorStyles.MakeLabel("Sector: --, Area: --", EditorStyles.FontSmall, EditorStyles.TextPrimary);
            _metricsPlayer = EditorStyles.MakeLabel("HP: --/--  Lv: --", EditorStyles.FontSmall, EditorStyles.TextPrimary);
            _metricsEnemies = EditorStyles.MakeLabel("Enemies: --", EditorStyles.FontSmall, EditorStyles.TextPrimary);
            _metricsKills = EditorStyles.MakeLabel("Kills: --", EditorStyles.FontSmall, EditorStyles.TextPrimary);
            _metricsDebug = EditorStyles.MakeLabel("Debug: --", EditorStyles.FontSmall, EditorStyles.TextPrimary);
            _metricsThreat = EditorStyles.MakeLabel("Threat: --  Ascension: --", EditorStyles.FontSmall, EditorStyles.TextPrimary);

            metricsVBox.AddChild(_metricsFps);
            metricsVBox.AddChild(_metricsState);
            metricsVBox.AddChild(_metricsSector);
            metricsVBox.AddChild(_metricsPlayer);
            metricsVBox.AddChild(_metricsEnemies);
            metricsVBox.AddChild(_metricsKills);
            metricsVBox.AddChild(_metricsDebug);
            metricsVBox.AddChild(_metricsThreat);

            metricsPanel.AddChild(metricsVBox);
            panel.AddChild(metricsPanel);

            return panel;
        }

        // ===== RIGHT PANEL: Context + History =====

        private VBoxContainer BuildRightPanel()
        {
            var panel = new VBoxContainer();
            panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            panel.SizeFlagsVertical = SizeFlags.ExpandFill;
            panel.CustomMinimumSize = new Vector2(350, 0);
            panel.AddThemeConstantOverride("separation", 4);

            // -- SCENE CONTEXT --
            panel.AddChild(EditorStyles.MakeLabel("SCENE CONTEXT", EditorStyles.FontSmall, EditorStyles.StatusSaved));

            _contextDisplay = new TextEdit();
            _contextDisplay.Editable = false;
            _contextDisplay.CustomMinimumSize = new Vector2(0, 180);
            _contextDisplay.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _contextDisplay.AddThemeFontSizeOverride("font_size", EditorStyles.FontTiny);
            _contextDisplay.AddThemeColorOverride("font_color", new Color(0.5f, 0.8f, 0.5f));
            _contextDisplay.AddThemeStyleboxOverride("normal", EditorStyles.MakeFlat(new Color(0.05f, 0.08f, 0.05f)));
            _contextDisplay.AddThemeStyleboxOverride("read_only", EditorStyles.MakeFlat(new Color(0.05f, 0.08f, 0.05f)));
            panel.AddChild(_contextDisplay);

            var refreshBtn = EditorStyles.MakeButton("Refresh Context", EditorStyles.FontSmall, EditorStyles.StatusSaved);
            refreshBtn.Pressed += RefreshContext;
            panel.AddChild(refreshBtn);

            // -- REPORT HISTORY --
            panel.AddChild(EditorStyles.MakeSeparator());
            _historyCountLabel = EditorStyles.MakeLabel("REPORT HISTORY (0)", EditorStyles.FontSmall, EditorStyles.TextAccent);
            panel.AddChild(_historyCountLabel);

            _historyList = new ItemList();
            _historyList.CustomMinimumSize = new Vector2(0, 140);
            _historyList.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _historyList.SizeFlagsVertical = SizeFlags.ExpandFill;
            _historyList.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
            _historyList.AddThemeColorOverride("font_color", EditorStyles.TextPrimary);
            _historyList.AddThemeStyleboxOverride("panel", EditorStyles.MakeFlat(EditorStyles.BgField));
            _historyList.ItemSelected += OnHistorySelected;
            panel.AddChild(_historyList);

            // Preview
            panel.AddChild(EditorStyles.MakeLabel("Preview:", EditorStyles.FontSmall, EditorStyles.TextSecondary));
            _previewDisplay = new TextEdit();
            _previewDisplay.Editable = false;
            _previewDisplay.CustomMinimumSize = new Vector2(0, 120);
            _previewDisplay.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _previewDisplay.SizeFlagsVertical = SizeFlags.ExpandFill;
            _previewDisplay.AddThemeFontSizeOverride("font_size", EditorStyles.FontTiny);
            _previewDisplay.AddThemeColorOverride("font_color", EditorStyles.TextSecondary);
            _previewDisplay.AddThemeStyleboxOverride("normal", EditorStyles.MakeFlat(EditorStyles.BgField));
            _previewDisplay.AddThemeStyleboxOverride("read_only", EditorStyles.MakeFlat(EditorStyles.BgField));
            _previewDisplay.WrapMode = TextEdit.LineWrappingMode.Boundary;
            panel.AddChild(_previewDisplay);

            return panel;
        }

        // ===== VISIBILITY =====

        public override void _Ready()
        {
            base._Ready();
            VisibilityChanged += OnVisibilityChanged;
        }

        private void OnVisibilityChanged()
        {
            if (Visible)
            {
                RefreshMetrics();
                RefreshContext();
                RefreshHistory();
            }
        }

        // ===== TYPE TOGGLE =====

        private void OnTypeChanged(long index)
        {
            _severityRow.Visible = index == 0; // Hide severity for Feature
            _descriptionEdit.PlaceholderText = index == 0
                ? "What happened? What did you expect?"
                : "Describe the feature you'd like...";
        }

        // ===== METRICS =====

        private void RefreshMetrics()
        {
            _metricsFps.Text = $"FPS: {Engine.GetFramesPerSecond()}";

            var gm = GameManager.Instance;
            if (gm != null)
            {
                _metricsState.Text = $"State: {gm.CurrentState}";
                _metricsSector.Text = $"Sector {gm.CurrentSector}, Area {gm.CurrentArea}";
                _metricsKills.Text = $"Kills: {gm.RunKills}";
            }
            else
            {
                _metricsState.Text = "State: No GameManager";
                _metricsSector.Text = "Sector: --, Area: --";
                _metricsKills.Text = "Kills: --";
            }

            var player = PlayerManager.P1;
            if (player != null)
            {
                float hp = player.Health?.CurrentHealth ?? 0;
                float maxHp = player.Health?.MaxHealth ?? 0;
                int level = player.Stats?.Level ?? 0;
                _metricsPlayer.Text = $"HP: {hp:F0}/{maxHp:F0}  Lv: {level}";
            }
            else
            {
                _metricsPlayer.Text = "HP: --/--  Lv: --";
            }

            int enemyCount = 0;
            try { enemyCount = GetTree().GetNodesInGroup("Enemy").Count; } catch { }
            _metricsEnemies.Text = $"Enemies: {enemyCount}";

            string godMode = DebugMenu.GodMode ? "ON" : "OFF";
            string instantKill = DebugMenu.InstantKill ? "ON" : "OFF";
            _metricsDebug.Text = $"Debug: GodMode={godMode}  InstantKill={instantKill}  DmgMult={DebugMenu.DamageMultiplier:F1}x";

            _metricsThreat.Text = $"Threat: {MetaSaveManager.ThreatLevel}  Ascension: {MetaSaveManager.Data.AscensionRank}";
        }

        // ===== CONTEXT =====

        private void RefreshContext()
        {
            try
            {
                _capturedContext = SceneContext.Capture(GetTree());
            }
            catch (Exception e)
            {
                _capturedContext = $"Error capturing context: {e.Message}";
            }

            if (_contextDisplay != null)
                _contextDisplay.Text = _capturedContext;
        }

        // ===== HISTORY =====

        private void RefreshHistory()
        {
            _historyPaths.Clear();
            if (_historyList == null) return;
            _historyList.Clear();

            string repoRoot = GetRepoRoot();
            if (repoRoot == null) return;

            // Scan both bugs and features directories
            string[] subdirs = { "bugs", "features" };
            var entries = new List<(string path, string displayName, DateTime time)>();

            foreach (string sub in subdirs)
            {
                string dir = Path.Combine(repoRoot, "test-reports", sub);
                if (!Directory.Exists(dir)) continue;

                foreach (string folder in Directory.GetDirectories(dir))
                {
                    string reportFile = Path.Combine(folder, "report.md");
                    if (!File.Exists(reportFile)) continue;

                    string folderName = Path.GetFileName(folder);
                    DateTime modified = File.GetLastWriteTime(reportFile);

                    // Extract a short display: "bug 03-08 14:22 - title..."
                    string firstLine = "";
                    try
                    {
                        using var reader = new StreamReader(reportFile);
                        firstLine = reader.ReadLine() ?? "";
                    }
                    catch { }

                    // Strip markdown header prefix
                    string title = firstLine.TrimStart('#', ' ');
                    if (title.Length > 50) title = title[..47] + "...";

                    string badge = sub == "bugs" ? "bug" : "feature";
                    string display = $"{badge} {modified:MM-dd HH:mm}  {title}";

                    entries.Add((reportFile, display, modified));
                }
            }

            // Sort newest first, limit 50
            entries.Sort((a, b) => b.time.CompareTo(a.time));
            int max = Math.Min(entries.Count, 50);

            for (int i = 0; i < max; i++)
            {
                _historyList.AddItem(entries[i].displayName);
                _historyPaths.Add(entries[i].path);
            }

            _historyCountLabel.Text = $"REPORT HISTORY ({max})";
        }

        private void OnHistorySelected(long index)
        {
            if (index < 0 || index >= _historyPaths.Count) return;

            try
            {
                string content = File.ReadAllText(_historyPaths[(int)index]);
                _previewDisplay.Text = content;
            }
            catch (Exception e)
            {
                _previewDisplay.Text = $"Error loading report: {e.Message}";
            }
        }

        // ===== SUBMIT =====

        private void OnSubmit()
        {
            string title = _titleEdit.Text.Trim();
            if (string.IsNullOrEmpty(title))
            {
                SetStatus("Title is required!", EditorStyles.StatusError);
                return;
            }

            bool isFeature = _typePicker.Selected == 1;
            string type = isFeature ? "feature" : "bug";
            string severity = "";
            if (!isFeature)
            {
                string[] sevLabels = { "low", "medium", "high" };
                severity = sevLabels[_severityPicker.Selected];
            }

            string description = _descriptionEdit.Text.Trim();
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string folderName = $"{type}-{timestamp}";

            string repoRoot = GetRepoRoot();
            if (repoRoot == null)
            {
                SetStatus("Could not find repo root!", EditorStyles.StatusError);
                return;
            }

            string subDir = isFeature ? "features" : "bugs";
            string reportDir = Path.Combine(repoRoot, "test-reports", subDir, folderName);

            try
            {
                Directory.CreateDirectory(reportDir);

                string report = BuildReport(title, type, severity, description, isFeature);
                File.WriteAllText(Path.Combine(reportDir, "report.md"), report);

                // Save screenshot if available
                var screenshot = EditorManager.LastScreenshot;
                if (screenshot != null)
                {
                    string screenshotPath = Path.Combine(reportDir, "screenshot.png");
                    screenshot.SavePng(screenshotPath);
                }

                SetStatus($"Saved to test-reports/{subDir}/{folderName}/", EditorStyles.StatusSaved);
                GD.Print($"[BugReporter] Saved {type} report to {reportDir}");

                OnClear();
                RefreshHistory();
            }
            catch (Exception e)
            {
                SetStatus($"Save failed: {e.Message}", EditorStyles.StatusError);
                GD.PrintErr($"[BugReporter] Save failed: {e}");
            }
        }

        private string BuildReport(string title, string type, string severity, string description, bool isFeature)
        {
            string typeTitle = isFeature ? "Feature Request" : "Bug Report";
            string sevSection = !isFeature ? $"**Severity:** {severity}\n" : "";

            var gm = GameManager.Instance;
            int sector = gm?.CurrentSector ?? 0;
            int area = gm?.CurrentArea ?? 0;

            // Live metrics section
            var player = PlayerManager.P1;
            float hp = player?.Health?.CurrentHealth ?? 0;
            float maxHp = player?.Health?.MaxHealth ?? 0;
            int level = player?.Stats?.Level ?? 0;
            int enemyCount = 0;
            try { enemyCount = GetTree().GetNodesInGroup("Enemy").Count; } catch { }

            string metricsBlock = $@"## Live Metrics
- **FPS:** {Engine.GetFramesPerSecond()}
- **State:** {gm?.CurrentState.ToString() ?? "Unknown"}
- **Sector:** {sector}, **Area:** {area}
- **Player HP:** {hp:F0}/{maxHp:F0}, **Level:** {level}
- **Enemies alive:** {enemyCount}
- **Run kills:** {gm?.RunKills ?? 0}
- **GodMode:** {(DebugMenu.GodMode ? "ON" : "OFF")}, **InstantKill:** {(DebugMenu.InstantKill ? "ON" : "OFF")}, **DmgMult:** {DebugMenu.DamageMultiplier:F1}x
- **Threat Level:** {MetaSaveManager.ThreatLevel}, **Ascension:** {MetaSaveManager.Data.AscensionRank}";

            return $@"# {typeTitle}: {title}
**Date:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}
**Type:** {type}
{sevSection}
## Description
{description}

{metricsBlock}

## Scene Context
```
{_capturedContext}
```

## How to Reproduce
1. Open the game
2. Navigate to Sector {sector}, Area {area}
3. [Fill in steps]

## Expected Behavior
[What should have happened]

## Actual Behavior
[What actually happened]
";
        }

        // ===== COPY / CLEAR =====

        private void OnCopy()
        {
            string title = _titleEdit.Text.Trim();
            string type = _typePicker.Selected == 1 ? "Feature" : "Bug";
            string desc = _descriptionEdit.Text.Trim();
            string report = $"[{type}] {title}\n\n{desc}\n\n{_capturedContext}";
            DisplayServer.ClipboardSet(report);
            SetStatus("Copied to clipboard!", EditorStyles.StatusSaved);
        }

        private void OnClear()
        {
            _titleEdit.Text = "";
            _descriptionEdit.Text = "";
            _typePicker.Selected = 0;
            _severityPicker.Selected = 1;
            _severityRow.Visible = true;
            _descriptionEdit.PlaceholderText = "What happened? What did you expect?";
        }

        // ===== HELPERS =====

        private static string GetRepoRoot()
        {
            string godotDir = ProjectSettings.GlobalizePath("res://").TrimEnd('/', '\\');
            return Path.GetDirectoryName(godotDir);
        }

        // ===== EDITOR PANEL OVERRIDES =====

        protected override void Reload()
        {
            if (_contextDisplay == null) return;
            RefreshContext();
            RefreshMetrics();
            RefreshHistory();
            MarkClean();
        }

        protected override void Save()
        {
            OnSubmit();
        }

        protected override void RestoreSnapshot(string jsonSnapshot)
        {
            // No undo-able state
        }
    }
}
