using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// F12 editor tab — Build Verification Test dashboard.
    /// Auto-runs on first editor open per session. Shows pass/warn/fail per check,
    /// grouped by collapsible categories. Re-runnable via button.
    /// </summary>
    public partial class EditorBVT : EditorModule
    {
        public override string ModuleName => "BVT";
        public override Color AccentColor => new(0.95f, 0.55f, 0.15f);

        private bool _hasRunThisSession;
        private List<BVTCheckResult> _cachedResults;

        private VBoxContainer _resultsContainer;
        private Label _summaryLabel;
        private ScrollContainer _scroll;

        public override void _Ready()
        {
            BuildUI();
        }

        public override void OnActivated()
        {
            if (!_hasRunThisSession)
            {
                _hasRunThisSession = true;
                RunAll();
            }
            else if (_cachedResults != null)
            {
                // Already ran, just show cached
            }
        }

        private void BuildUI()
        {
            // Header bar
            var header = new HBoxContainer();
            header.AddThemeConstantOverride("separation", 12);
            AddChild(header);

            header.AddChild(EditorStyles.MakeLabel("Build Verification Test", EditorStyles.FontHeader, AccentColor));

            var runBtn = EditorStyles.MakeButton("Run All", EditorStyles.FontBody, AccentColor);
            runBtn.CustomMinimumSize = new Vector2(100, 32);
            runBtn.Pressed += RunAll;
            header.AddChild(runBtn);

            var spacer = new Control();
            spacer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            header.AddChild(spacer);

            _summaryLabel = EditorStyles.MakeLabel("Not yet run", EditorStyles.FontBody, EditorStyles.TextMuted);
            header.AddChild(_summaryLabel);

            AddChild(EditorStyles.MakeSeparator());

            // Scrollable results area
            _scroll = new ScrollContainer();
            _scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            _scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            AddChild(_scroll);

            _resultsContainer = new VBoxContainer();
            _resultsContainer.AddThemeConstantOverride("separation", 1);
            _resultsContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _scroll.AddChild(_resultsContainer);
        }

        private void RunAll()
        {
            _cachedResults = BVTRunner.RunAll(GetTree());
            RebuildResultsUI();
        }

        private void RebuildResultsUI()
        {
            // Clear old results
            foreach (var child in _resultsContainer.GetChildren())
                child.QueueFree();

            if (_cachedResults == null || _cachedResults.Count == 0)
            {
                _resultsContainer.AddChild(EditorStyles.MakeLabel("No results", EditorStyles.FontBody, EditorStyles.TextMuted));
                return;
            }

            int totalPass = _cachedResults.Count(r => r.Status == BVTStatus.Pass);
            int totalWarn = _cachedResults.Count(r => r.Status == BVTStatus.Warn);
            int totalFail = _cachedResults.Count(r => r.Status == BVTStatus.Fail);

            // Summary
            Color summaryColor = totalFail > 0 ? EditorStyles.StatusError
                : totalWarn > 0 ? EditorStyles.StatusWarn
                : EditorStyles.StatusOk;
            _summaryLabel.Text = $"{totalPass} PASS  {totalWarn} WARN  {totalFail} FAIL  /  {_cachedResults.Count} total";
            _summaryLabel.AddThemeColorOverride("font_color", summaryColor);

            // Group by category
            var groups = _cachedResults
                .GroupBy(r => (r.Category, r.CategoryName))
                .OrderBy(g => g.Key.Category);

            foreach (var group in groups)
            {
                int catPass = group.Count(r => r.Status == BVTStatus.Pass);
                int catWarn = group.Count(r => r.Status == BVTStatus.Warn);
                int catFail = group.Count(r => r.Status == BVTStatus.Fail);

                Color catColor = catFail > 0 ? EditorStyles.StatusError
                    : catWarn > 0 ? EditorStyles.StatusWarn
                    : EditorStyles.StatusOk;

                // Category header (clickable to collapse)
                var catHeader = new Button();
                catHeader.Text = $"  [{group.Key.Category}] {group.Key.CategoryName}  —  {catPass} pass, {catWarn} warn, {catFail} fail";
                catHeader.Alignment = HorizontalAlignment.Left;
                catHeader.AddThemeFontSizeOverride("font_size", 13);
                catHeader.AddThemeColorOverride("font_color", catColor);
                var catHeaderStyle = new StyleBoxFlat();
                catHeaderStyle.BgColor = new Color(catColor.R * 0.1f, catColor.G * 0.1f, catColor.B * 0.1f, 0.5f);
                catHeaderStyle.SetBorderWidthAll(0);
                catHeader.AddThemeStyleboxOverride("normal", catHeaderStyle);
                catHeader.AddThemeStyleboxOverride("hover", catHeaderStyle);
                _resultsContainer.AddChild(catHeader);

                // Results container (collapsible)
                var catResults = new VBoxContainer();
                catResults.AddThemeConstantOverride("separation", 0);
                _resultsContainer.AddChild(catResults);

                // Toggle collapse on header click
                catHeader.Pressed += () => catResults.Visible = !catResults.Visible;

                // Only show non-pass by default if category has many items
                bool collapsePass = group.Count() > 10;

                foreach (var result in group)
                {
                    // Skip passing checks in large categories (still counted in header)
                    if (collapsePass && result.Status == BVTStatus.Pass) continue;

                    var row = new HBoxContainer();
                    row.AddThemeConstantOverride("separation", 8);

                    // Status indicator
                    string statusIcon = result.Status switch {
                        BVTStatus.Pass => "OK",
                        BVTStatus.Warn => "!?",
                        BVTStatus.Fail => "XX",
                        _ => "??"
                    };
                    Color statusColor = result.Status switch {
                        BVTStatus.Pass => EditorStyles.StatusOk,
                        BVTStatus.Warn => EditorStyles.StatusWarn,
                        BVTStatus.Fail => EditorStyles.StatusError,
                        _ => EditorStyles.TextMuted
                    };
                    row.AddChild(EditorStyles.MakeLabel(statusIcon, EditorStyles.FontSmall, statusColor));

                    // Test name
                    row.AddChild(EditorStyles.MakeLabel(result.TestName, EditorStyles.FontSmall, EditorStyles.TextSecondary));

                    // Duration
                    if (result.DurationMs > 0)
                        row.AddChild(EditorStyles.MakeLabel($"{result.DurationMs}ms", EditorStyles.FontTiny, EditorStyles.TextMuted));

                    // Message
                    if (!string.IsNullOrEmpty(result.Message))
                        row.AddChild(EditorStyles.MakeLabel(result.Message, EditorStyles.FontTiny, statusColor));

                    catResults.AddChild(row);
                }

                // If all passed and collapsed, show summary line
                if (collapsePass && catFail == 0 && catWarn == 0)
                {
                    catResults.AddChild(EditorStyles.MakeLabel(
                        $"    All {catPass} checks passed", EditorStyles.FontSmall, EditorStyles.StatusOk));
                }
            }

            GD.Print($"[BVT] Complete: {totalPass} pass, {totalWarn} warn, {totalFail} fail / {_cachedResults.Count} total");
        }
    }
}
