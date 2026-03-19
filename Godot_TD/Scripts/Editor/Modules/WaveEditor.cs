using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Live editor for wave composition. Tune enemy counts, health, speed, timing.
    /// Left panel: wave list. Right panel: spawn group details.
    /// </summary>
    public partial class WaveEditor : EditorModule
    {
        public override string ModuleName => "Waves";
        public override Color AccentColor => EditorStyles.AccentWaves;

        private VBoxContainer _waveList;
        private VBoxContainer _inspector;
        private VineWaveData _selectedWave;
        private int _selectedSurgeIndex = -1;

        public override void _Ready()
        {
            BuildUI();
        }

        private void BuildUI()
        {
            var split = new HSplitContainer();
            split.SizeFlagsVertical = SizeFlags.ExpandFill;
            split.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            AddChild(split);

            // ── Left: Wave list ──
            var leftPanel = new PanelContainer();
            leftPanel.CustomMinimumSize = new Vector2(200, 0);
            leftPanel.AddThemeStyleboxOverride("panel", EditorStyles.MakePanel(EditorStyles.BgPanel));
            split.AddChild(leftPanel);

            var leftVBox = new VBoxContainer();
            leftVBox.AddThemeConstantOverride("separation", 2);
            leftPanel.AddChild(leftVBox);

            leftVBox.AddChild(EditorStyles.MakeLabel("Waves", 16, AccentColor));
            leftVBox.AddChild(EditorStyles.MakeSeparator());

            var scroll = new ScrollContainer();
            scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            leftVBox.AddChild(scroll);

            _waveList = new VBoxContainer();
            _waveList.AddThemeConstantOverride("separation", 2);
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

            PopulateWaveList();
        }

        private void PopulateWaveList()
        {
            foreach (var child in _waveList.GetChildren())
                child.QueueFree();

            foreach (var wave in VineWaveRegistry.GetAll())
            {
                int totalEnemies = 0;
                foreach (var g in wave.Surges) totalEnemies += g.Count;

                var btn = new Button();
                btn.Text = $"Wave {wave.WaveNumber}: {wave.Name} ({totalEnemies} enemies)";
                btn.Alignment = HorizontalAlignment.Left;
                btn.CustomMinimumSize = new Vector2(0, 28);
                var w = wave;
                btn.Pressed += () => SelectWave(w);
                _waveList.AddChild(btn);
            }
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

            // Header
            _inspector.AddChild(EditorStyles.MakeLabel(
                $"Wave {_selectedWave.WaveNumber}: {_selectedWave.Name}", 18, AccentColor));

            // Wave-level properties
            AddWaveProperty("Bonus Scrap", _selectedWave.BonusScrap, 0, 100, 1,
                v => _selectedWave.BonusScrap = (int)v);

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
                    $"{group.EnemyName} ({group.Faction})", 14, factionColor));

                // Editable fields
                AddGroupProperty(groupVBox, "Count", group.Count, 1, 50, 1,
                    v => _selectedWave.Surges[idx].Count = (int)v);
                AddGroupProperty(groupVBox, "Health", group.Health, 5, 500, 5,
                    v => _selectedWave.Surges[idx].Health = (float)v);
                AddGroupProperty(groupVBox, "Speed", group.Speed, 0.5f, 10f, 0.5f,
                    v => _selectedWave.Surges[idx].Speed = (float)v);
                AddGroupProperty(groupVBox, "Scrap Value", group.ScrapValue, 0, 30, 1,
                    v => _selectedWave.Surges[idx].ScrapValue = (int)v);
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
