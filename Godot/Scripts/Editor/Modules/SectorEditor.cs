using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JunkbotArena.Editor
{
    /// <summary>
    /// Sector Editor — tune dungeon sector configurations.
    /// Edit enemy pools, room distributions, difficulty scaling, and visual themes.
    /// </summary>
    public partial class SectorEditor : EditorPanel
    {
        public override string PanelName => "Sectors";
        public override Color AccentColor => EditorStyles.AccentRoom;

        private DataTable _table;
        private PropertyInspector _inspector;
        private Label _inspectorTitle;

        private Dictionary<string, Dictionary<string, object>> _sectorData;

        protected override void BuildUI(VBoxContainer content)
        {
            var split = new HBoxContainer();
            split.SizeFlagsVertical = SizeFlags.ExpandFill;
            split.AddThemeConstantOverride("separation", 8);

            // Left: sector table
            var leftPanel = new VBoxContainer();
            leftPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            leftPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            leftPanel.CustomMinimumSize = new Vector2(350, 0);

            _table = new DataTable();
            _table.SizeFlagsVertical = SizeFlags.ExpandFill;
            _table.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            leftPanel.AddChild(_table);
            split.AddChild(leftPanel);

            // Right: inspector
            var rightPanel = new VBoxContainer();
            rightPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            rightPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            rightPanel.CustomMinimumSize = new Vector2(350, 0);

            _inspectorTitle = EditorStyles.MakeLabel("Select a sector", EditorStyles.FontHeader, EditorStyles.TextSecondary);
            rightPanel.AddChild(_inspectorTitle);
            rightPanel.AddChild(EditorStyles.MakeSeparator());

            var inspScroll = new ScrollContainer();
            inspScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            inspScroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            _inspector = new PropertyInspector();
            _inspector.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            inspScroll.AddChild(_inspector);
            rightPanel.AddChild(inspScroll);

            split.AddChild(rightPanel);
            content.AddChild(split);
        }

        public override void _Ready()
        {
            base._Ready();
            CallDeferred(nameof(WireEvents));
        }

        private void WireEvents()
        {
            _table.OnRowSelected += OnSectorSelected;
            _inspector.OnValueChanged += OnValueChanged;
        }

        private void OnSectorSelected(string key, Dictionary<string, object> data)
        {
            _inspectorTitle.Text = $"Sector {key}";
            _inspectorTitle.AddThemeColorOverride("font_color", AccentColor);

            if (_sectorData.TryGetValue(key, out var detail))
            {
                _inspector.Build(new Dictionary<string, object>(detail), GetHints());
                PushUndo(MiniJsonWriter.Serialize(_sectorData));
            }
        }

        private void OnValueChanged(string property, object value)
        {
            if (_restoringSnapshot) return;
            var key = _table.SelectedKey;
            if (key == null || _sectorData == null) return;

            if (_sectorData.TryGetValue(key, out var data))
            {
                PushUndo(MiniJsonWriter.Serialize(_sectorData));
                data[property] = value;
                _table.UpdateRow(key, data);
                MarkDirty();
            }
        }

        protected override void Reload()
        {
            if (_table == null) return;

            _sectorData = new();

            var json = LoadJson("res://Data/sectors.json");
            if (json != null)
            {
                foreach (var kvp in json)
                {
                    if (kvp.Value is Dictionary<string, object> entry)
                        _sectorData[kvp.Key] = entry;
                }
            }
            else
            {
                // Load from registry
                for (int i = 1; i <= 5; i++)
                {
                    var s = SectorDataRegistry.GetSector(i);
                    if (s == null) continue;
                    _sectorData[i.ToString()] = new Dictionary<string, object>
                    {
                        ["Difficulty"] = (double)s.DifficultyMultiplier,
                        ["CombatRooms"] = (double)s.CombatRoomCount,
                        ["TreasureRooms"] = (double)s.TreasureRooms,
                        ["EventRooms"] = (double)s.EventRooms,
                        ["ShopRooms"] = (double)s.ShopRooms,
                        ["PuzzleRooms"] = (double)s.PuzzleRooms,
                        ["MinEnemies"] = (double)s.MinEnemiesPerRoom,
                        ["MaxEnemies"] = (double)s.MaxEnemiesPerRoom,
                        ["MegabonkChance"] = (double)s.MegabonkChance,
                        ["WaveChance"] = (double)s.WaveChance,
                        ["TimeLimit"] = (double)s.TimeLimit,
                        ["SafeRoomChance"] = (double)s.SafeRoomChance,
                        ["BossId"] = s.BossEnemyId ?? "",
                        ["EnemyPool"] = string.Join(", ", s.EnemyPool ?? new List<string>()),
                        ["Theme"] = s.ThemeName ?? $"Sector {i}"
                    };
                }
            }

            var columns = new[] { "Difficulty", "CombatRooms", "MinEnemies", "MaxEnemies", "BossId", "Theme" };
            _table.SetData(columns, _sectorData);
            MarkClean();
            SetStatus("Loaded sector data", EditorStyles.StatusSaved);
            PushInitialState(MiniJsonWriter.Serialize(_sectorData));
        }

        protected override void Save()
        {
            if (_sectorData == null) return;
            if (SaveJson("res://Data/sectors.json", _sectorData))
            {
                MarkClean();
                SetStatus("Saved sectors.json", EditorStyles.StatusSaved);
                GD.Print("[SectorEditor] Saved sectors.json");
            }
            else
            {
                SetStatus("Save failed!", EditorStyles.StatusError);
            }
        }

        protected override void RestoreSnapshot(string jsonSnapshot)
        {
            var parsed = MiniJson.Deserialize(jsonSnapshot) as Dictionary<string, object>;
            if (parsed == null) return;
            _sectorData = new();
            foreach (var kvp in parsed)
            {
                if (kvp.Value is Dictionary<string, object> entry)
                    _sectorData[kvp.Key] = entry;
            }
            var columns = new[] { "Difficulty", "CombatRooms", "MinEnemies", "MaxEnemies", "BossId", "Theme" };
            _table.SetData(columns, _sectorData);
        }

        private Dictionary<string, PropertyInspector.PropertyHint> GetHints()
        {
            return new()
            {
                ["Difficulty"] = new() { Min = 0.5f, Max = 10, Step = 0.1f },
                ["CombatRooms"] = new() { Min = 5, Max = 50, Step = 1 },
                ["TreasureRooms"] = new() { Min = 0, Max = 10, Step = 1 },
                ["EventRooms"] = new() { Min = 0, Max = 10, Step = 1 },
                ["ShopRooms"] = new() { Min = 0, Max = 5, Step = 1 },
                ["PuzzleRooms"] = new() { Min = 0, Max = 10, Step = 1 },
                ["MinEnemies"] = new() { Min = 1, Max = 20, Step = 1 },
                ["MaxEnemies"] = new() { Min = 1, Max = 30, Step = 1 },
                ["MegabonkChance"] = new() { Min = 0, Max = 1, Step = 0.05f },
                ["WaveChance"] = new() { Min = 0, Max = 1, Step = 0.05f },
                ["TimeLimit"] = new() { Min = 60, Max = 600, Step = 30 },
                ["SafeRoomChance"] = new() { Min = 0, Max = 0.5f, Step = 0.05f },
            };
        }

        // ═══════════════════════════════════════════════════════════════
        //  TEST API
        // ═══════════════════════════════════════════════════════════════

        private int _testSectorIndex;

        public override void TestCycleNext(string property)
        {
            switch (property)
            {
                case "sector":
                    if (_sectorData == null || _sectorData.Count == 0) break;
                    var keys = _sectorData.Keys.ToList();
                    keys.Sort();
                    _testSectorIndex = (_testSectorIndex + 1) % keys.Count;
                    var key = keys[_testSectorIndex];
                    if (_sectorData.TryGetValue(key, out var data))
                        OnSectorSelected(key, data);
                    break;
            }
        }
    }
}
