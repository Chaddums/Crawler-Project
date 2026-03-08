using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JunkbotArena.Editor
{
    /// <summary>
    /// String/UX Editor — edit all game strings from strings.json.
    /// Flat key-value view with search filter. Saves directly to strings.json.
    /// Supports hot-reload via StringLoader.Reload().
    /// </summary>
    public partial class StringEditor : EditorPanel
    {
        public override string PanelName => "Strings";
        public override Color AccentColor => EditorStyles.AccentUI;

        private DataTable _table;
        private SearchFilter _search;
        private VBoxContainer _editArea;
        private LineEdit _keyEdit;
        private TextEdit _valueEdit;
        private Label _editTitle;

        private Dictionary<string, Dictionary<string, object>> _flatStrings;
        private Dictionary<string, object> _rawJson;

        protected override void BuildUI(VBoxContainer content)
        {
            // Search
            _search = new SearchFilter();
            content.AddChild(_search);

            var split = new HBoxContainer();
            split.SizeFlagsVertical = SizeFlags.ExpandFill;
            split.AddThemeConstantOverride("separation", 8);

            // Left: string table
            var leftPanel = new VBoxContainer();
            leftPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            leftPanel.SizeFlagsVertical = SizeFlags.ExpandFill;

            _table = new DataTable();
            _table.SizeFlagsVertical = SizeFlags.ExpandFill;
            _table.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            leftPanel.AddChild(_table);
            split.AddChild(leftPanel);

            // Right: edit area
            var rightPanel = new VBoxContainer();
            rightPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            rightPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            rightPanel.CustomMinimumSize = new Vector2(350, 0);

            _editTitle = EditorStyles.MakeLabel("Select a string", EditorStyles.FontHeader, EditorStyles.TextSecondary);
            rightPanel.AddChild(_editTitle);
            rightPanel.AddChild(EditorStyles.MakeSeparator());

            rightPanel.AddChild(EditorStyles.MakeLabel("Key:", EditorStyles.FontSmall, EditorStyles.TextSecondary));
            _keyEdit = EditorStyles.MakeLineEdit("", EditorStyles.FontSmall);
            _keyEdit.Editable = true;
            _keyEdit.TextSubmitted += OnKeyRenamed;
            rightPanel.AddChild(_keyEdit);

            rightPanel.AddChild(EditorStyles.MakeLabel("Value:", EditorStyles.FontSmall, EditorStyles.TextSecondary));
            _valueEdit = new TextEdit();
            _valueEdit.SizeFlagsVertical = SizeFlags.ExpandFill;
            _valueEdit.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _valueEdit.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
            _valueEdit.CustomMinimumSize = new Vector2(0, 100);
            rightPanel.AddChild(_valueEdit);

            var applyBtn = EditorStyles.MakeButton("Apply Change", EditorStyles.FontSmall, AccentColor);
            applyBtn.CustomMinimumSize = new Vector2(0, 28);
            applyBtn.Pressed += ApplyEdit;
            rightPanel.AddChild(applyBtn);

            var reloadBtn = EditorStyles.MakeButton("Hot-Reload Strings", EditorStyles.FontSmall, EditorStyles.StatusSaved);
            reloadBtn.CustomMinimumSize = new Vector2(0, 28);
            reloadBtn.Pressed += () =>
            {
                StringLoader.Reload();
                SetStatus("StringLoader reloaded!", EditorStyles.StatusSaved);
            };
            rightPanel.AddChild(reloadBtn);

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
            _search.OnFilterChanged += text => _table?.Filter(text);
            _table.OnRowSelected += OnStringSelected;
            _table.OnAddRequested += OnAddString;
            _table.OnRenameRequested += OnRenameString;
            _table.OnDeleteRequested += OnDeleteString;
        }

        private void OnStringSelected(string key, Dictionary<string, object> data)
        {
            _editTitle.Text = key;
            _editTitle.AddThemeColorOverride("font_color", AccentColor);
            _keyEdit.Text = key;
            _valueEdit.Text = data.TryGetValue("Value", out var v) ? v?.ToString() ?? "" : "";
        }

        private void OnAddString()
        {
            if (_flatStrings == null || _rawJson == null) return;

            var newKey = "new.string.key";
            int suffix = 1;
            while (_flatStrings.ContainsKey(newKey))
                newKey = $"new.string.key_{suffix++}";

            PushUndo(MiniJsonWriter.Serialize(_rawJson));
            _flatStrings[newKey] = new Dictionary<string, object> { ["Value"] = "" };
            SetNestedValue(_rawJson, newKey, "", create: true);
            _table.AddRow(newKey, _flatStrings[newKey]);
            _table.Select(newKey);
            MarkDirty();
            SetStatus($"Added '{newKey}'", EditorStyles.StatusSaved);
        }

        private void OnRenameString(string key)
        {
            // Select and focus the key editor for inline rename
            _keyEdit.Text = key;
            _keyEdit.GrabFocus();
            _keyEdit.SelectAll();
        }

        private void OnKeyRenamed(string newKey)
        {
            var oldKey = _table.SelectedKey;
            if (oldKey == null || oldKey == newKey || _flatStrings == null || _rawJson == null) return;
            if (string.IsNullOrWhiteSpace(newKey))
            {
                _keyEdit.Text = oldKey;
                return;
            }
            if (_flatStrings.ContainsKey(newKey))
            {
                SetStatus($"Key '{newKey}' already exists!", EditorStyles.StatusError);
                _keyEdit.Text = oldKey;
                return;
            }

            PushUndo(MiniJsonWriter.Serialize(_rawJson));

            // Move in flat strings
            _flatStrings[newKey] = _flatStrings[oldKey];
            _flatStrings.Remove(oldKey);

            // Move in raw JSON: remove old, set new
            RemoveNestedValue(_rawJson, oldKey);
            var val = _flatStrings[newKey].TryGetValue("Value", out var v) ? v?.ToString() ?? "" : "";
            SetNestedValue(_rawJson, newKey, val, create: true);

            _table.RenameRow(oldKey, newKey);
            _editTitle.Text = newKey;
            MarkDirty();
            SetStatus($"Renamed '{oldKey}' -> '{newKey}'", EditorStyles.StatusSaved);
        }

        private void OnDeleteString(string key)
        {
            if (_flatStrings == null || _rawJson == null || !_flatStrings.ContainsKey(key)) return;

            PushUndo(MiniJsonWriter.Serialize(_rawJson));
            _flatStrings.Remove(key);
            RemoveNestedValue(_rawJson, key);
            _table.RemoveRow(key);
            _editTitle.Text = "Select a string";
            _keyEdit.Text = "";
            _valueEdit.Text = "";
            MarkDirty();
            SetStatus($"Deleted '{key}'", EditorStyles.StatusError);
        }

        private void ApplyEdit()
        {
            var key = _keyEdit.Text;
            if (string.IsNullOrEmpty(key) || _flatStrings == null) return;

            var newValue = _valueEdit.Text;
            if (_flatStrings.TryGetValue(key, out var row))
            {
                row["Value"] = newValue;
                _table.UpdateRow(key, row);
            }

            // Update in raw JSON structure
            SetNestedValue(_rawJson, key, newValue);
            MarkDirty();
            PushUndo(MiniJsonWriter.Serialize(_rawJson));
        }

        protected override void Reload()
        {
            if (_table == null) return;

            _rawJson = LoadJson("res://Data/strings.json");
            if (_rawJson == null)
            {
                _rawJson = new();
            }

            // Auto-populate missing registry strings
            EnsureRegistryStrings();

            // Flatten to dot-path key-value pairs
            _flatStrings = new();
            FlattenJson("", _rawJson);

            var columns = new[] { "Value" };
            _table.SetData(columns, _flatStrings);
            MarkClean();
            SetStatus($"Loaded {_flatStrings.Count} strings", EditorStyles.StatusSaved);
        }

        /// <summary>
        /// Auto-generate stub entries in strings.json for any registry content
        /// that doesn't have string entries yet (relics, equipment, consumables, enemies).
        /// </summary>
        private void EnsureRegistryStrings()
        {
            bool added = false;

            // Relics
            if (!_rawJson.ContainsKey("relics"))
            {
                var section = new Dictionary<string, object>();
                foreach (var id in RelicRegistry.AllIds)
                {
                    var r = RelicRegistry.Get(id);
                    if (r == null) continue;
                    section[id] = new Dictionary<string, object>
                    {
                        ["name"] = r.ItemName ?? FormatId(id),
                        ["description"] = r.Description ?? "",
                        ["flavorText"] = r.FlavorText ?? "",
                        ["axisQuote"] = r.AxisQuote ?? ""
                    };
                }
                if (section.Count > 0) { _rawJson["relics"] = section; added = true; }
            }

            // Equipment (only if section missing)
            if (!_rawJson.ContainsKey("equipment"))
            {
                var section = new Dictionary<string, object>();
                foreach (var equip in BaseItemPool.Equipment)
                {
                    section[equip.Id] = new Dictionary<string, object>
                    {
                        ["name"] = equip.ItemName ?? FormatId(equip.Id),
                        ["description"] = equip.Description ?? ""
                    };
                }
                if (section.Count > 0) { _rawJson["equipment"] = section; added = true; }
            }

            // Consumables
            if (!_rawJson.ContainsKey("consumables"))
            {
                var section = new Dictionary<string, object>();
                foreach (var kvp in ConsumableRegistry.Consumables)
                {
                    var c = kvp.Value;
                    section[kvp.Key] = new Dictionary<string, object>
                    {
                        ["name"] = c.ItemName ?? FormatId(kvp.Key),
                        ["description"] = c.Description ?? ""
                    };
                }
                if (section.Count > 0) { _rawJson["consumables"] = section; added = true; }
            }

            // Enemies
            if (!_rawJson.ContainsKey("enemies"))
            {
                var section = new Dictionary<string, object>();
                foreach (var kvp in EnemyRegistry.Enemies)
                {
                    var e = kvp.Value;
                    section[kvp.Key] = new Dictionary<string, object>
                    {
                        ["name"] = e.EnemyName ?? FormatId(kvp.Key)
                    };
                }
                if (section.Count > 0) { _rawJson["enemies"] = section; added = true; }
            }

            if (added)
                GD.Print("[StringEditor] Auto-populated missing registry strings");
        }

        /// <summary>
        /// Format an ID like "nipple_ring_of_fury" into "Nipple Ring Of Fury".
        /// </summary>
        private static string FormatId(string id)
        {
            var parts = id.Split('_');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 0)
                    parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1);
            }
            return string.Join(" ", parts);
        }

        private void FlattenJson(string prefix, Dictionary<string, object> obj)
        {
            foreach (var kvp in obj)
            {
                var path = string.IsNullOrEmpty(prefix) ? kvp.Key : $"{prefix}.{kvp.Key}";
                if (kvp.Value is Dictionary<string, object> nested)
                {
                    FlattenJson(path, nested);
                }
                else if (kvp.Value is List<object> list)
                {
                    _flatStrings[path] = new Dictionary<string, object>
                    {
                        ["Value"] = $"[{list.Count} items]"
                    };
                }
                else
                {
                    _flatStrings[path] = new Dictionary<string, object>
                    {
                        ["Value"] = kvp.Value?.ToString() ?? "null"
                    };
                }
            }
        }

        private static void SetNestedValue(Dictionary<string, object> root, string dotPath, string value, bool create = false)
        {
            var parts = dotPath.Split('.');
            var current = root;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (current.TryGetValue(parts[i], out var next) && next is Dictionary<string, object> dict)
                {
                    current = dict;
                }
                else if (create)
                {
                    var newDict = new Dictionary<string, object>();
                    current[parts[i]] = newDict;
                    current = newDict;
                }
                else
                {
                    return;
                }
            }
            current[parts[^1]] = value;
        }

        private static void RemoveNestedValue(Dictionary<string, object> root, string dotPath)
        {
            var parts = dotPath.Split('.');
            var current = root;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (current.TryGetValue(parts[i], out var next) && next is Dictionary<string, object> dict)
                    current = dict;
                else
                    return;
            }
            current.Remove(parts[^1]);
        }

        protected override void Save()
        {
            if (_rawJson == null) return;
            if (SaveJson("res://Data/strings.json", _rawJson))
            {
                MarkClean();
                SetStatus("Saved strings.json", EditorStyles.StatusSaved);
                GD.Print("[StringEditor] Saved strings.json");
            }
            else
            {
                SetStatus("Save failed!", EditorStyles.StatusError);
            }
        }

        protected override void RestoreSnapshot(string jsonSnapshot)
        {
            _rawJson = MiniJson.Deserialize(jsonSnapshot) as Dictionary<string, object>;
            if (_rawJson != null) Reload();
        }
    }
}
