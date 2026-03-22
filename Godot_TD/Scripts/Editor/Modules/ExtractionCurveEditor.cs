using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// S2/T2: Visual extraction curve editor. Shows resource rewards per wave as a bar chart.
    /// Drag base/growth sliders to tune the exponential curve. Instantly see the difference
    /// between quitting at wave 8 vs wave 15.
    /// </summary>
    public partial class ExtractionCurveEditor : EditorModule
    {
        public override string ModuleName => "Extraction";
        public override Color AccentColor => new(0.3f, 0.95f, 0.4f);

        private const int MAX_WAVES = 30;
        private const float BAR_MAX_HEIGHT = 200f;

        private float _base = Constants.EXTRACTION_BASE;
        private float _growth = Constants.EXTRACTION_GROWTH;

        private HBoxContainer _barContainer;
        private Label _comparisonLabel;
        private Label _formulaLabel;
        private Label _statusLabel;

        public override void _Ready()
        {
            BuildUI();
        }

        public override void OnActivated()
        {
            // Refresh from current constants in case they changed
            _base = Constants.EXTRACTION_BASE;
            _growth = Constants.EXTRACTION_GROWTH;
            RefreshBars();
        }

        private void BuildUI()
        {
            AddChild(EditorStyles.MakeLabel("Extraction Curve Tuner", 18, AccentColor));
            AddChild(EditorStyles.MakeLabel(
                "Resources earned per wave. Exponential curve: base * growth^(wave-1)",
                12, EditorStyles.TextMuted));
            AddChild(EditorStyles.MakeSeparator());

            // Formula display
            _formulaLabel = EditorStyles.MakeLabel("", 14, EditorStyles.TextSecondary);
            AddChild(_formulaLabel);

            // Sliders
            var sliderBox = new VBoxContainer();
            sliderBox.AddThemeConstantOverride("separation", 8);
            AddChild(sliderBox);

            // Base slider
            var baseRow = new HBoxContainer();
            baseRow.AddThemeConstantOverride("separation", 10);
            baseRow.AddChild(EditorStyles.MakeLabel("Base:", 13));
            var baseSpin = EditorStyles.MakeSpinBox(_base, 1f, 50f, 1f);
            baseSpin.ValueChanged += v => { _base = (float)v; RefreshBars(); };
            baseRow.AddChild(baseSpin);
            sliderBox.AddChild(baseRow);

            // Growth slider
            var growthRow = new HBoxContainer();
            growthRow.AddThemeConstantOverride("separation", 10);
            growthRow.AddChild(EditorStyles.MakeLabel("Growth:", 13));
            var growthSpin = EditorStyles.MakeSpinBox(_growth, 1.0f, 1.5f, 0.01f);
            growthSpin.ValueChanged += v => { _growth = (float)v; RefreshBars(); };
            growthRow.AddChild(growthSpin);
            sliderBox.AddChild(growthRow);

            AddChild(EditorStyles.MakeSeparator());

            // Comparison label
            _comparisonLabel = EditorStyles.MakeLabel("", 14, new Color(0.0f, 0.85f, 0.95f));
            AddChild(_comparisonLabel);

            AddChild(EditorStyles.MakeSeparator());

            // Bar chart area
            var chartLabel = EditorStyles.MakeLabel("Resources per wave (height = reward)", 11, EditorStyles.TextMuted);
            AddChild(chartLabel);

            var chartPanel = new PanelContainer();
            chartPanel.CustomMinimumSize = new Vector2(0, BAR_MAX_HEIGHT + 40);
            chartPanel.AddThemeStyleboxOverride("panel", EditorStyles.MakePanel(EditorStyles.BgPanel));
            AddChild(chartPanel);

            var chartVBox = new VBoxContainer();
            chartPanel.AddChild(chartVBox);

            _barContainer = new HBoxContainer();
            _barContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
            _barContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _barContainer.AddThemeConstantOverride("separation", 2);
            _barContainer.Alignment = BoxContainer.AlignmentMode.End;
            chartVBox.AddChild(_barContainer);

            // Status
            _statusLabel = EditorStyles.MakeLabel("", 11, EditorStyles.TextMuted);
            AddChild(_statusLabel);

            RefreshBars();
        }

        private void RefreshBars()
        {
            if (_barContainer == null) return;

            // Clear old bars
            foreach (var child in _barContainer.GetChildren())
                child.QueueFree();

            // Compute values
            float maxVal = 0;
            var values = new float[MAX_WAVES];
            var cumulative = new float[MAX_WAVES];
            float runningTotal = 0;

            for (int w = 0; w < MAX_WAVES; w++)
            {
                values[w] = _base * Mathf.Pow(_growth, w);
                runningTotal += values[w];
                cumulative[w] = runningTotal;
                if (values[w] > maxVal) maxVal = values[w];
            }

            // Check which waves are milestones
            bool[] isMilestone = new bool[MAX_WAVES];
            if (FileAccess.FileExists("res://Data/milestones.json"))
            {
                var file = FileAccess.Open("res://Data/milestones.json", FileAccess.ModeFlags.Read);
                if (file != null)
                {
                    var json = new Json();
                    if (json.Parse(file.GetAsText()) == Error.Ok &&
                        json.Data.Obj is Godot.Collections.Dictionary dict &&
                        dict.ContainsKey("planets") &&
                        dict["planets"].Obj is Godot.Collections.Dictionary planets &&
                        planets.ContainsKey("1") &&
                        planets["1"].Obj is Godot.Collections.Dictionary p1 &&
                        p1.ContainsKey("milestones") &&
                        p1["milestones"].Obj is Godot.Collections.Array milestones)
                    {
                        foreach (var m in milestones)
                        {
                            if (m.Obj is Godot.Collections.Dictionary md && md.ContainsKey("wave"))
                            {
                                int mw = (int)(double)md["wave"];
                                if (mw > 0 && mw <= MAX_WAVES) isMilestone[mw - 1] = true;
                            }
                        }
                    }
                    file.Close();
                }
            }

            // Draw bars
            for (int w = 0; w < MAX_WAVES; w++)
            {
                var barWrapper = new VBoxContainer();
                barWrapper.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                barWrapper.SizeFlagsVertical = SizeFlags.ExpandFill;

                // Spacer to push bar to bottom
                var spacer = new Control();
                spacer.SizeFlagsVertical = SizeFlags.ExpandFill;
                barWrapper.AddChild(spacer);

                // Bar
                float heightPct = maxVal > 0 ? values[w] / maxVal : 0;
                var bar = new ColorRect();
                bar.CustomMinimumSize = new Vector2(0, BAR_MAX_HEIGHT * heightPct);
                bar.SizeFlagsHorizontal = SizeFlags.ExpandFill;

                if (isMilestone[w])
                    bar.Color = new Color(0.9f, 0.6f, 0.1f, 0.9f); // amber for milestones
                else if (w < 8)
                    bar.Color = new Color(0.3f, 0.7f, 0.4f, 0.7f); // green early
                else if (w < 15)
                    bar.Color = new Color(0.3f, 0.85f, 0.5f, 0.8f); // brighter mid
                else
                    bar.Color = new Color(0.3f, 0.95f, 0.4f, 0.9f); // brightest late

                bar.TooltipText = $"Wave {w + 1}: {values[w]:F0} resources\nTotal if exit here: {cumulative[w]:F0}" +
                    (isMilestone[w] ? "\n★ MILESTONE" : "");
                barWrapper.AddChild(bar);

                // Wave number label
                var label = EditorStyles.MakeLabel($"{w + 1}", 9, EditorStyles.TextMuted);
                label.HorizontalAlignment = HorizontalAlignment.Center;
                barWrapper.AddChild(label);

                _barContainer.AddChild(barWrapper);
            }

            // Update formula
            if (_formulaLabel != null)
                _formulaLabel.Text = $"Formula: {_base:F0} × {_growth:F2}^(wave-1)";

            // Comparison
            if (_comparisonLabel != null)
            {
                float at8 = cumulative.Length >= 8 ? cumulative[7] : 0;
                float at15 = cumulative.Length >= 15 ? cumulative[14] : 0;
                float at20 = cumulative.Length >= 20 ? cumulative[19] : 0;
                _comparisonLabel.Text = $"Exit wave 8: {at8:F0}  |  Exit wave 15: {at15:F0} ({at15 / Mathf.Max(at8, 1):F1}x)  |  Exit wave 20: {at20:F0} ({at20 / Mathf.Max(at8, 1):F1}x)";
            }

            if (_statusLabel != null)
                _statusLabel.Text = $"Amber bars = milestone waves. Hover for details.";
        }
    }
}
