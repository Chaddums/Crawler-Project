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
    /// Tracks temp vs final status per string in string_status.json.
    /// </summary>
    public partial class StringEditor : EditorPanel
    {
        public override string PanelName => "Strings";
        public override Color AccentColor => EditorStyles.AccentUI;

        private const string STRINGS_JSON = "res://Data/strings.json";
        private const string STATUS_JSON = "res://Data/string_status.json";

        // Status colors
        private static readonly Color TempColor = new(1.0f, 0.75f, 0.25f);   // yellow-orange
        private static readonly Color FinalColor = new(0.40f, 0.85f, 0.45f);  // green
        private static readonly Color DialogueColor = new(0.55f, 0.70f, 1.0f); // blue

        // Dialogue key prefixes — keys starting with these are "dialogue"
        private static readonly string[] DialoguePrefixes =
        {
            "commentary.", "trollEvents.", "lootNarration.", "systemMessages."
        };

        private DataTable _table;
        private SearchFilter _search;
        private VBoxContainer _editArea;
        private LineEdit _keyEdit;
        private TextEdit _valueEdit;
        private Label _editTitle;

        // Status toggle in edit panel
        private Button _statusToggleBtn;
        private Label _statusCountLabel;

        // Filter state
        private enum ViewFilter { All, TempOnly, FinalOnly, StringsOnly, DialogueOnly }
        private ViewFilter _viewFilter = ViewFilter.All;
        private readonly List<Button> _filterButtons = new();

        private Dictionary<string, Dictionary<string, object>> _flatStrings;
        private Dictionary<string, object> _rawJson;

        // Status tracking: key -> "temp" | "final"
        private Dictionary<string, object> _statusMap;

        protected override void BuildUI(VBoxContainer content)
        {
            // Filter bar
            var filterRow = new HBoxContainer();
            filterRow.AddThemeConstantOverride("separation", 4);
            filterRow.AddChild(EditorStyles.MakeLabel("View:", EditorStyles.FontSmall, EditorStyles.TextSecondary));

            string[] filterNames = { "All", "Temp", "Final", "Strings", "Dialogue" };
            ViewFilter[] filterValues = { ViewFilter.All, ViewFilter.TempOnly, ViewFilter.FinalOnly, ViewFilter.StringsOnly, ViewFilter.DialogueOnly };
            for (int i = 0; i < filterNames.Length; i++)
            {
                var btn = EditorStyles.MakeButton(filterNames[i], EditorStyles.FontSmall,
                    i == 0 ? AccentColor : EditorStyles.TextSecondary);
                btn.CustomMinimumSize = new Vector2(55, 24);
                var capturedFilter = filterValues[i];
                btn.Pressed += () => SetViewFilter(capturedFilter);
                filterRow.AddChild(btn);
                _filterButtons.Add(btn);
            }

            var spacer = new Control();
            spacer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            filterRow.AddChild(spacer);

            _statusCountLabel = EditorStyles.MakeLabel("", EditorStyles.FontSmall, EditorStyles.TextMuted);
            filterRow.AddChild(_statusCountLabel);

            content.AddChild(filterRow);

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
            _table.RowTintOverride = GetRowTint;
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

            // Status toggle row
            var statusRow = new HBoxContainer();
            statusRow.AddThemeConstantOverride("separation", 8);
            statusRow.AddChild(EditorStyles.MakeLabel("Status:", EditorStyles.FontSmall, EditorStyles.TextSecondary));
            _statusToggleBtn = EditorStyles.MakeButton("TEMP", EditorStyles.FontSmall, TempColor);
            _statusToggleBtn.CustomMinimumSize = new Vector2(80, 24);
            _statusToggleBtn.Pressed += ToggleSelectedStatus;
            statusRow.AddChild(_statusToggleBtn);
            var markAllTempBtn = EditorStyles.MakeButton("Mark Filtered Temp", EditorStyles.FontTiny, TempColor);
            markAllTempBtn.CustomMinimumSize = new Vector2(0, 24);
            markAllTempBtn.Pressed += () => BulkSetStatus("temp");
            statusRow.AddChild(markAllTempBtn);
            var markAllFinalBtn = EditorStyles.MakeButton("Mark Filtered Final", EditorStyles.FontTiny, FinalColor);
            markAllFinalBtn.CustomMinimumSize = new Vector2(0, 24);
            markAllFinalBtn.Pressed += () => BulkSetStatus("final");
            statusRow.AddChild(markAllFinalBtn);
            rightPanel.AddChild(statusRow);

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

        // ── View Filter ──

        private void SetViewFilter(ViewFilter filter)
        {
            _viewFilter = filter;

            // Update button colors
            ViewFilter[] filterValues = { ViewFilter.All, ViewFilter.TempOnly, ViewFilter.FinalOnly, ViewFilter.StringsOnly, ViewFilter.DialogueOnly };
            for (int i = 0; i < _filterButtons.Count && i < filterValues.Length; i++)
                _filterButtons[i].AddThemeColorOverride("font_color",
                    filterValues[i] == filter ? AccentColor : EditorStyles.TextSecondary);

            RebuildTable();
        }

        private bool PassesViewFilter(string key)
        {
            switch (_viewFilter)
            {
                case ViewFilter.TempOnly:
                    return GetStatus(key) == "temp";
                case ViewFilter.FinalOnly:
                    return GetStatus(key) == "final";
                case ViewFilter.StringsOnly:
                    return !IsDialogue(key);
                case ViewFilter.DialogueOnly:
                    return IsDialogue(key);
                default:
                    return true;
            }
        }

        private static bool IsDialogue(string key)
        {
            foreach (var prefix in DialoguePrefixes)
                if (key.StartsWith(prefix)) return true;
            return false;
        }

        // ── Status Management ──

        private string GetStatus(string key)
        {
            if (_statusMap != null && _statusMap.TryGetValue(key, out var val))
                return val?.ToString() ?? "temp";
            return "temp"; // default: everything is temp until marked final
        }

        private void SetStatus(string key, string status)
        {
            _statusMap ??= new Dictionary<string, object>();
            _statusMap[key] = status;
        }

        private void ToggleSelectedStatus()
        {
            var key = _table?.SelectedKey;
            if (string.IsNullOrEmpty(key)) return;

            string current = GetStatus(key);
            string next = current == "final" ? "temp" : "final";
            SetStatus(key, next);
            UpdateStatusButton(next);
            SaveStatusFile();

            // Update the table row data
            if (_flatStrings != null && _flatStrings.TryGetValue(key, out var row))
            {
                row["Status"] = FormatStatusLabel(next);
                _table.UpdateRow(key, row);
            }
            UpdateStatusCounts();
        }

        private void BulkSetStatus(string status)
        {
            if (_flatStrings == null) return;

            int count = 0;
            foreach (var key in _flatStrings.Keys.ToList())
            {
                if (!PassesViewFilter(key)) continue;
                SetStatus(key, status);
                if (_flatStrings.TryGetValue(key, out var row))
                    row["Status"] = FormatStatusLabel(status);
                count++;
            }

            SaveStatusFile();
            RebuildTable();
            SetStatus($"Marked {count} strings as {status}", status == "final" ? FinalColor : TempColor);
        }

        private void UpdateStatusButton(string status)
        {
            if (_statusToggleBtn == null) return;
            bool isFinal = status == "final";
            _statusToggleBtn.Text = isFinal ? "FINAL" : "TEMP";
            _statusToggleBtn.AddThemeColorOverride("font_color", isFinal ? FinalColor : TempColor);
        }

        private static string FormatStatusLabel(string status)
        {
            return status == "final" ? "FINAL" : "TEMP";
        }

        private Color? GetRowTint(string key, Dictionary<string, object> data)
        {
            string status = GetStatus(key);
            bool dialogue = IsDialogue(key);

            if (status == "final")
                return FinalColor;
            if (dialogue)
                return DialogueColor;
            return TempColor;
        }

        private void UpdateStatusCounts()
        {
            if (_flatStrings == null || _statusCountLabel == null) return;

            int total = _flatStrings.Count;
            int finalCount = 0;
            int dialogueCount = 0;
            foreach (var key in _flatStrings.Keys)
            {
                if (GetStatus(key) == "final") finalCount++;
                if (IsDialogue(key)) dialogueCount++;
            }
            int tempCount = total - finalCount;

            _statusCountLabel.Text = $"{finalCount} final / {tempCount} temp  |  {dialogueCount} dialogue / {total - dialogueCount} strings  |  {total} total";
        }

        // ── Status File I/O ──

        private void LoadStatusFile()
        {
            _statusMap = LoadJson(STATUS_JSON);
            if (_statusMap == null)
                _statusMap = new Dictionary<string, object>();
        }

        private void SaveStatusFile()
        {
            if (_statusMap == null) return;
            SaveJson(STATUS_JSON, _statusMap);
        }

        // ── Selection / Edit ──

        private void OnStringSelected(string key, Dictionary<string, object> data)
        {
            _editTitle.Text = key;
            _editTitle.AddThemeColorOverride("font_color", AccentColor);
            _keyEdit.Text = key;
            _valueEdit.Text = data.TryGetValue("Value", out var v) ? v?.ToString() ?? "" : "";

            // Update status toggle
            UpdateStatusButton(GetStatus(key));

            // Show type hint
            bool dialogue = IsDialogue(key);
            if (dialogue)
            {
                _editTitle.Text = $"{key}  [DIALOGUE]";
            }
        }

        private void OnAddString()
        {
            if (_flatStrings == null || _rawJson == null) return;

            var newKey = "new.string.key";
            int suffix = 1;
            while (_flatStrings.ContainsKey(newKey))
                newKey = $"new.string.key_{suffix++}";

            PushUndo(MiniJsonWriter.Serialize(_rawJson));
            _flatStrings[newKey] = new Dictionary<string, object>
            {
                ["Value"] = "",
                ["Status"] = FormatStatusLabel("temp")
            };
            SetNestedValue(_rawJson, newKey, "", create: true);
            _table.AddRow(newKey, _flatStrings[newKey]);
            _table.Select(newKey);
            MarkDirty();
            UpdateStatusCounts();
            SetStatus($"Added '{newKey}'", EditorStyles.StatusSaved);
        }

        private void OnRenameString(string key)
        {
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

            // Move status
            string oldStatus = GetStatus(oldKey);
            _statusMap?.Remove(oldKey);
            SetStatus(newKey, oldStatus);
            SaveStatusFile();

            // Move in raw JSON: remove old, set new
            RemoveNestedValue(_rawJson, oldKey);
            var val = _flatStrings[newKey].TryGetValue("Value", out var v) ? v?.ToString() ?? "" : "";
            SetNestedValue(_rawJson, newKey, val, create: true);

            _table.RenameRow(oldKey, newKey);
            _editTitle.Text = newKey;
            MarkDirty();
            UpdateStatusCounts();
            SetStatus($"Renamed '{oldKey}' -> '{newKey}'", EditorStyles.StatusSaved);
        }

        private void OnDeleteString(string key)
        {
            if (_flatStrings == null || _rawJson == null || !_flatStrings.ContainsKey(key)) return;

            PushUndo(MiniJsonWriter.Serialize(_rawJson));
            _flatStrings.Remove(key);
            RemoveNestedValue(_rawJson, key);
            _statusMap?.Remove(key);
            SaveStatusFile();
            _table.RemoveRow(key);
            _editTitle.Text = "Select a string";
            _keyEdit.Text = "";
            _valueEdit.Text = "";
            MarkDirty();
            UpdateStatusCounts();
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

        // ── Table Rebuild ──

        private void RebuildTable()
        {
            if (_table == null || _flatStrings == null) return;

            // Apply view filter
            var filtered = new Dictionary<string, Dictionary<string, object>>();
            foreach (var kvp in _flatStrings)
            {
                if (PassesViewFilter(kvp.Key))
                    filtered[kvp.Key] = kvp.Value;
            }

            var columns = new[] { "Status", "Value" };
            _table.SetData(columns, filtered);
            UpdateStatusCounts();
        }

        // ── Data Loading ──

        protected override void Reload()
        {
            if (_table == null) return;

            _rawJson = LoadJson(STRINGS_JSON);
            if (_rawJson == null)
            {
                _rawJson = new();
            }

            LoadStatusFile();

            // Auto-populate missing registry strings
            EnsureRegistryStrings();

            // Flatten to dot-path key-value pairs
            _flatStrings = new();
            FlattenJson("", _rawJson);

            RebuildTable();
            MarkClean();
            SetStatus($"Loaded {_flatStrings.Count} strings", EditorStyles.StatusSaved);
            PushInitialState(MiniJsonWriter.Serialize(_rawJson));
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
                        ["Status"] = FormatStatusLabel(GetStatus(path)),
                        ["Value"] = $"[{list.Count} items]"
                    };
                }
                else
                {
                    _flatStrings[path] = new Dictionary<string, object>
                    {
                        ["Status"] = FormatStatusLabel(GetStatus(path)),
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
            if (SaveJson(STRINGS_JSON, _rawJson))
            {
                SaveStatusFile();
                MarkClean();
                SetStatus("Saved strings.json + status", EditorStyles.StatusSaved);
                GD.Print("[StringEditor] Saved strings.json + string_status.json");
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
