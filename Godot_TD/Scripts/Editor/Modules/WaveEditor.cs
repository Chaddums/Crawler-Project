using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// S2/T1/T3: Wave editor with milestone timeline and difficulty preview.
    /// Left: continuous wave list with milestone markers and difficulty scaling.
    /// Right: surge inspector for selected wave.
    /// </summary>
    public partial class WaveEditor : EditorModule
    {
        public override string ModuleName => "Waves";
        public override Color AccentColor => EditorStyles.AccentWaves;

        private VBoxContainer _waveList;
        private VBoxContainer _inspector;
        private VineWaveData _selectedWave;
        private int _selectedSurgeIndex = -1;
        private Label _statusLabel;

        // Milestone data from JSON
        private struct MilestoneEntry
        {
            public int Wave;
            public string Type;
            public string Label;
        }
        private List<MilestoneEntry> _milestones = new();

        public override void _Ready()
        {
            LoadMilestones();
            BuildUI();
        }

        public override void OnActivated()
        {
            LoadMilestones();
            PopulateWaveList();
        }

        private void LoadMilestones()
        {
            _milestones.Clear();
            if (!FileAccess.FileExists("res://Data/milestones.json")) return;

            var file = FileAccess.Open("res://Data/milestones.json", FileAccess.ModeFlags.Read);
            if (file == null) return;

            var json = new Json();
            if (json.Parse(file.GetAsText()) != Error.Ok) { file.Close(); return; }
            file.Close();

            if (json.Data.Obj is not Godot.Collections.Dictionary dict) return;
            if (!dict.ContainsKey("planets")) return;
            if (dict["planets"].Obj is not Godot.Collections.Dictionary planets) return;

            // Load planet 1 milestones (extend later for multi-planet)
            string planetKey = (GameManager.Instance?.CurrentPlanet ?? 1).ToString();
            if (!planets.ContainsKey(planetKey)) planetKey = "1";
            if (!planets.ContainsKey(planetKey)) return;

            if (planets[planetKey].Obj is not Godot.Collections.Dictionary pData) return;
            if (!pData.ContainsKey("milestones")) return;
            if (pData["milestones"].Obj is not Godot.Collections.Array arr) return;

            foreach (var item in arr)
            {
                if (item.Obj is not Godot.Collections.Dictionary entry) continue;
                _milestones.Add(new MilestoneEntry {
                    Wave = entry.ContainsKey("wave") ? (int)(double)entry["wave"] : 0,
                    Type = entry.ContainsKey("type") ? (string)entry["type"] : "",
                    Label = entry.ContainsKey("label") ? (string)entry["label"] : ""
                });
            }
        }

        private void BuildUI()
        {
            var split = new HSplitContainer();
            split.SizeFlagsVertical = SizeFlags.ExpandFill;
            split.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            AddChild(split);

            // ── Left: Wave list with milestones ──
            var leftPanel = new PanelContainer();
            leftPanel.CustomMinimumSize = new Vector2(280, 0);
            leftPanel.AddThemeStyleboxOverride("panel", EditorStyles.MakePanel(EditorStyles.BgPanel));
            split.AddChild(leftPanel);

            var leftVBox = new VBoxContainer();
            leftVBox.AddThemeConstantOverride("separation", 2);
            leftPanel.AddChild(leftVBox);

            leftVBox.AddChild(EditorStyles.MakeLabel("Continuous Waves", 16, AccentColor));
            leftVBox.AddChild(EditorStyles.MakeLabel(
                "★ = milestone (perk select)  |  Scaling shown per wave", 10, EditorStyles.TextMuted));
            leftVBox.AddChild(EditorStyles.MakeSeparator());

            var scroll = new ScrollContainer();
            scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            leftVBox.AddChild(scroll);

            _waveList = new VBoxContainer();
            _waveList.AddThemeConstantOverride("separation", 1);
            scroll.AddChild(_waveList);

            // ── Right: Inspector ──
            var rightPanel = new PanelContainer();
            rightPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            rightPanel.AddThemeStyleboxOverride("panel", EditorStyles.MakePanel(EditorStyles.BgPanel));
            split.AddChild(rightPanel);

            var rightScroll = new ScrollContainer();
            rightScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            rightScroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            rightPanel.AddChild(rightScroll);

            _inspector = new VBoxContainer();
            _inspector.AddThemeConstantOverride("separation", 6);
            _inspector.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            rightScroll.AddChild(_inspector);

            // Status
            _statusLabel = EditorStyles.MakeLabel("Select a wave to inspect", 11, EditorStyles.TextMuted);
            leftVBox.AddChild(_statusLabel);

            PopulateWaveList();
        }

        private void PopulateWaveList()
        {
            if (_waveList == null) return;

            foreach (var child in _waveList.GetChildren())
                child.QueueFree();

            var scaler = ServiceLocator.TryGet<DifficultyScaler>(out var ds) ? ds : null;
            var allWaves = VineWaveRegistry.GetAll();

            foreach (var wave in allWaves)
            {
                int wNum = wave.WaveNumber;
                int totalEnemies = 0;
                foreach (var g in wave.Surges) totalEnemies += g.Count;

                // Check if this wave is a milestone
                bool isMilestone = false;
                string milestoneLabel = "";
                foreach (var m in _milestones)
                {
                    if (m.Wave == wNum)
                    {
                        isMilestone = true;
                        milestoneLabel = m.Label;
                        break;
                    }
                }

                // Compute difficulty scaling preview
                float hpMult = scaler?.GetWaveHpMultiplier(wNum) ?? 1f;
                float spdMult = scaler?.GetWaveSpeedMultiplier(wNum) ?? 1f;
                int extraction = scaler?.ComputeExtractionBonus(wNum) ?? 0;

                // Build wave row
                var rowPanel = new PanelContainer();
                var rowBg = isMilestone
                    ? new Color(0.2f, 0.15f, 0.05f, 0.6f)
                    : (wNum % 2 == 0 ? EditorStyles.BgRowAlt : EditorStyles.BgRow);
                rowPanel.AddThemeStyleboxOverride("panel", EditorStyles.MakePanel(rowBg));
                _waveList.AddChild(rowPanel);

                var rowHBox = new HBoxContainer();
                rowHBox.AddThemeConstantOverride("separation", 6);
                rowPanel.AddChild(rowHBox);

                // Milestone marker
                if (isMilestone)
                {
                    var star = EditorStyles.MakeLabel("★", 14, new Color(0.95f, 0.7f, 0.1f));
                    star.TooltipText = milestoneLabel;
                    rowHBox.AddChild(star);
                }

                // Wave button
                var btn = new Button();
                btn.Text = $"W{wNum}: {wave.Name} ({totalEnemies}e)";
                btn.Alignment = HorizontalAlignment.Left;
                btn.CustomMinimumSize = new Vector2(130, 24);
                btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                btn.AddThemeFontSizeOverride("font_size", 11);
                var w = wave;
                btn.Pressed += () => SelectWave(w);
                rowHBox.AddChild(btn);

                // Scaling preview
                var scalingLabel = EditorStyles.MakeLabel(
                    $"HP:{hpMult:F2}x  Spd:{spdMult:F2}x  +{extraction}res",
                    9, EditorStyles.TextMuted);
                scalingLabel.TooltipText = $"Wave {wNum} difficulty scaling\nHP multiplier: {hpMult:F3}\nSpeed multiplier: {spdMult:F3}\nExtraction bonus: {extraction}";
                rowHBox.AddChild(scalingLabel);
            }

            if (_statusLabel != null)
                _statusLabel.Text = $"{allWaves.Count} waves loaded  |  {_milestones.Count} milestones";
        }

        private void SelectWave(VineWaveData wave)
        {
            _selectedWave = wave;
            _selectedSurgeIndex = -1;
            BuildInspector();
        }

        private void BuildInspector()
        {
            foreach (var child in _inspector.GetChildren())
                child.QueueFree();

            if (_selectedWave == null)
            {
                _inspector.AddChild(EditorStyles.MakeLabel("Select a wave", 14, EditorStyles.TextMuted));
                return;
            }

            int wNum = _selectedWave.WaveNumber;

            // Header
            _inspector.AddChild(EditorStyles.MakeLabel(
                $"Wave {wNum}: {_selectedWave.Name}", 18, AccentColor));

            // Check milestone
            foreach (var m in _milestones)
            {
                if (m.Wave == wNum)
                {
                    _inspector.AddChild(EditorStyles.MakeLabel(
                        $"★ {m.Label} ({m.Type})", 13, new Color(0.95f, 0.7f, 0.1f)));
                    break;
                }
            }

            // Difficulty scaling for this wave
            var scaler = ServiceLocator.TryGet<DifficultyScaler>(out var ds) ? ds : null;
            if (scaler != null)
            {
                _inspector.AddChild(EditorStyles.MakeSeparator());
                _inspector.AddChild(EditorStyles.MakeLabel("Difficulty Scaling", 14, EditorStyles.TextAccent));

                float hpMult = scaler.GetWaveHpMultiplier(wNum);
                float spdMult = scaler.GetWaveSpeedMultiplier(wNum);
                float cntMult = scaler.GetWaveCountMultiplier(wNum);
                float armor = scaler.GetWaveArmorBonus(wNum);
                int extraction = scaler.ComputeExtractionBonus(wNum);

                AddReadonlyRow("HP Multiplier", $"{hpMult:F3}x");
                AddReadonlyRow("Speed Multiplier", $"{spdMult:F3}x");
                AddReadonlyRow("Count Multiplier", $"{cntMult:F3}x");
                AddReadonlyRow("Armor Bonus", $"+{armor:F1}");
                AddReadonlyRow("Extraction Bonus", $"+{extraction} resources");
            }

            // Wave-level editable properties
            _inspector.AddChild(EditorStyles.MakeSeparator());
            _inspector.AddChild(EditorStyles.MakeLabel("Wave Properties", 14, EditorStyles.TextPrimary));

            AddWaveProperty("Bonus Resources", _selectedWave.BonusResources, 0, 100, 1,
                v => _selectedWave.BonusResources = (int)v);

            _inspector.AddChild(EditorStyles.MakeSeparator());
            _inspector.AddChild(EditorStyles.MakeLabel("Surges", 15, EditorStyles.TextPrimary));

            // Spawn groups
            for (int i = 0; i < _selectedWave.Surges.Count; i++)
            {
                var group = _selectedWave.Surges[i];
                int idx = i;

                var groupPanel = new PanelContainer();
                groupPanel.AddThemeStyleboxOverride("panel", EditorStyles.MakePanel(
                    EditorStyles.BgRow, EditorStyles.Border));
                _inspector.AddChild(groupPanel);

                var groupVBox = new VBoxContainer();
                groupVBox.AddThemeConstantOverride("separation", 4);
                groupPanel.AddChild(groupVBox);

                // Group header
                var factionColor = group.Faction switch {
                    VineEnemyFaction.Scavenger => new Color(0.6f, 0.5f, 0.3f),
                    VineEnemyFaction.Brute => new Color(0.5f, 0.3f, 0.2f),
                    VineEnemyFaction.Ghost => new Color(0.5f, 0.5f, 0.8f),
                    VineEnemyFaction.Swarm => new Color(0.8f, 0.7f, 0.2f),
                    _ => EditorStyles.TextPrimary
                };
                groupVBox.AddChild(EditorStyles.MakeLabel(
                    $"S{i + 1}: {group.EnemyName} ({group.Faction})", 13, factionColor));

                // Commander indicator
                if (group.Commander != null)
                {
                    groupVBox.AddChild(EditorStyles.MakeLabel(
                        $"  Commander: {group.Commander.EnemyName} [{group.Commander.SpawnType}]",
                        11, new Color(0.9f, 0.5f, 0.2f)));
                }

                // Editable fields
                AddGroupProperty(groupVBox, "Count", group.Count, 1, 50, 1,
                    v => _selectedWave.Surges[idx].Count = (int)v);
                AddGroupProperty(groupVBox, "Health", group.Health, 5, 500, 5,
                    v => _selectedWave.Surges[idx].Health = (float)v);
                AddGroupProperty(groupVBox, "Speed", group.Speed, 0.5f, 10f, 0.5f,
                    v => _selectedWave.Surges[idx].Speed = (float)v);
                AddGroupProperty(groupVBox, "Resource Value", group.ResourceValue, 0, 30, 1,
                    v => _selectedWave.Surges[idx].ResourceValue = (int)v);
                AddGroupProperty(groupVBox, "Spawn Interval", group.SpawnInterval, 0.1f, 5f, 0.1f,
                    v => _selectedWave.Surges[idx].SpawnInterval = (float)v);
                AddGroupProperty(groupVBox, "Start Delay", group.StartDelay, 0, 30, 0.5f,
                    v => _selectedWave.Surges[idx].StartDelay = (float)v);
                AddGroupProperty(groupVBox, "Entry Index (-1=random)", group.EntryIndex, -1, 4, 1,
                    v => _selectedWave.Surges[idx].EntryIndex = (int)v);
            }

            _inspector.AddChild(EditorStyles.MakeSeparator());
            _inspector.AddChild(EditorStyles.MakeLabel(
                "Changes apply immediately to wave registry.\nNext wave start uses updated values.",
                11, EditorStyles.TextMuted));
        }

        private void AddReadonlyRow(string label, string value)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);
            var lbl = EditorStyles.MakeLabel(label, 12);
            lbl.CustomMinimumSize = new Vector2(140, 0);
            row.AddChild(lbl);
            row.AddChild(EditorStyles.MakeLabel(value, 12, EditorStyles.TextAccent));
            _inspector.AddChild(row);
        }

        private void AddWaveProperty(string label, float value, float min, float max, float step,
            System.Action<double> onChange)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);
            row.AddChild(EditorStyles.MakeLabel(label, 13));
            var spin = EditorStyles.MakeSpinBox(value, min, max, step);
            spin.ValueChanged += (double v) => onChange(v);
            row.AddChild(spin);
            _inspector.AddChild(row);
        }

        private void AddGroupProperty(VBoxContainer parent, string label, float value,
            float min, float max, float step, System.Action<double> onChange)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);

            var lbl = EditorStyles.MakeLabel(label, 12);
            lbl.CustomMinimumSize = new Vector2(140, 0);
            row.AddChild(lbl);

            var spin = EditorStyles.MakeSpinBox(value, min, max, step);
            spin.ValueChanged += (double v) => onChange(v);
            row.AddChild(spin);

            parent.AddChild(row);
        }
    }
}
