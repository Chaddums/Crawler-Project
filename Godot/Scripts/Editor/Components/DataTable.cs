using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JunkbotArena.Editor
{
    /// <summary>
    /// Sortable, filterable data grid. Shows all entries of a data type.
    /// Clicking a row selects it and emits OnRowSelected.
    /// Supports column headers, sorting, and text filtering.
    /// </summary>
    public partial class DataTable : VBoxContainer
    {
        public event Action<string, Dictionary<string, object>> OnRowSelected;

        private readonly List<string> _columns = new();
        private readonly Dictionary<string, Dictionary<string, object>> _rows = new();
        private readonly List<string> _filteredKeys = new();

        private VBoxContainer _rowContainer;
        private ScrollContainer _scroll;
        private string _selectedKey;
        private string _filterText = "";
        private string _sortColumn;
        private bool _sortAscending = true;

        public string SelectedKey => _selectedKey;

        public override void _Ready()
        {
            // Header row is added in BuildHeader
            _scroll = new ScrollContainer();
            _scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            _scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            AddChild(_scroll);

            _rowContainer = new VBoxContainer();
            _rowContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _rowContainer.AddThemeConstantOverride("separation", 1);
            _scroll.AddChild(_rowContainer);
        }

        /// <summary>
        /// Set up the table with column definitions and data.
        /// </summary>
        public void SetData(string[] columns, Dictionary<string, Dictionary<string, object>> rows)
        {
            _columns.Clear();
            _columns.AddRange(columns);
            _rows.Clear();
            foreach (var kvp in rows)
                _rows[kvp.Key] = kvp.Value;

            Rebuild();
        }

        /// <summary>
        /// Update a single row's data without full rebuild.
        /// </summary>
        public void UpdateRow(string key, Dictionary<string, object> data)
        {
            _rows[key] = data;
            Rebuild();
        }

        /// <summary>
        /// Add a new row.
        /// </summary>
        public void AddRow(string key, Dictionary<string, object> data)
        {
            _rows[key] = data;
            Rebuild();
        }

        /// <summary>
        /// Remove a row.
        /// </summary>
        public void RemoveRow(string key)
        {
            _rows.Remove(key);
            if (_selectedKey == key) _selectedKey = null;
            Rebuild();
        }

        /// <summary>
        /// Apply a text filter. Rows whose key or any column value contains the text are shown.
        /// </summary>
        public void Filter(string text)
        {
            _filterText = text?.ToLowerInvariant() ?? "";
            Rebuild();
        }

        /// <summary>
        /// Select a row by key.
        /// </summary>
        public void Select(string key)
        {
            _selectedKey = key;
            if (_rows.TryGetValue(key, out var data))
                OnRowSelected?.Invoke(key, data);
            Rebuild();
        }

        private void Rebuild()
        {
            if (_rowContainer == null) return;

            // Clear existing
            foreach (var child in _rowContainer.GetChildren())
            {
                if (child is Node n) n.QueueFree();
            }

            // Build header
            var header = BuildHeaderRow();
            _rowContainer.AddChild(header);

            // Filter
            _filteredKeys.Clear();
            foreach (var kvp in _rows)
            {
                if (string.IsNullOrEmpty(_filterText) || MatchesFilter(kvp.Key, kvp.Value))
                    _filteredKeys.Add(kvp.Key);
            }

            // Sort
            if (!string.IsNullOrEmpty(_sortColumn))
            {
                _filteredKeys.Sort((a, b) =>
                {
                    var va = _rows[a].TryGetValue(_sortColumn, out var oa) ? oa : null;
                    var vb = _rows[b].TryGetValue(_sortColumn, out var ob) ? ob : null;
                    int cmp = CompareValues(va, vb);
                    return _sortAscending ? cmp : -cmp;
                });
            }

            // Build rows
            for (int i = 0; i < _filteredKeys.Count; i++)
            {
                var key = _filteredKeys[i];
                var row = BuildDataRow(key, _rows[key], i % 2 == 1);
                _rowContainer.AddChild(row);
            }
        }

        private HBoxContainer BuildHeaderRow()
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 2);

            // ID column
            var idBtn = EditorStyles.MakeButton("ID", EditorStyles.FontSmall, EditorStyles.TextAccent);
            idBtn.CustomMinimumSize = new Vector2(140, 24);
            idBtn.Alignment = HorizontalAlignment.Left;
            idBtn.Pressed += () => ToggleSort("_id");
            row.AddChild(idBtn);

            foreach (var col in _columns)
            {
                var btn = EditorStyles.MakeButton(col, EditorStyles.FontSmall, EditorStyles.TextAccent);
                btn.CustomMinimumSize = new Vector2(80, 24);
                btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                btn.Alignment = HorizontalAlignment.Left;
                var capturedCol = col;
                btn.Pressed += () => ToggleSort(capturedCol);
                row.AddChild(btn);
            }

            return row;
        }

        private PanelContainer BuildDataRow(string key, Dictionary<string, object> data, bool alternate)
        {
            var panel = new PanelContainer();
            bool isSelected = key == _selectedKey;
            var bgColor = isSelected ? EditorStyles.BgSelected :
                          alternate ? EditorStyles.BgField : EditorStyles.BgPanel;
            panel.AddThemeStyleboxOverride("panel", EditorStyles.MakeFlat(bgColor));

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 2);

            // ID label
            var idLabel = EditorStyles.MakeLabel(key, EditorStyles.FontSmall,
                isSelected ? EditorStyles.TextPrimary : EditorStyles.TextAccent);
            idLabel.CustomMinimumSize = new Vector2(140, 22);
            idLabel.ClipText = true;
            row.AddChild(idLabel);

            // Column values
            foreach (var col in _columns)
            {
                var val = data.TryGetValue(col, out var v) ? FormatValue(v) : "--";
                var label = EditorStyles.MakeLabel(val, EditorStyles.FontSmall);
                label.CustomMinimumSize = new Vector2(80, 22);
                label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                label.ClipText = true;
                row.AddChild(label);
            }

            panel.AddChild(row);

            // Click to select
            panel.GuiInput += e =>
            {
                if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                {
                    _selectedKey = key;
                    OnRowSelected?.Invoke(key, data);
                    Rebuild();
                }
            };
            panel.MouseFilter = Control.MouseFilterEnum.Stop;

            return panel;
        }

        private bool MatchesFilter(string key, Dictionary<string, object> data)
        {
            if (key.ToLowerInvariant().Contains(_filterText)) return true;
            foreach (var v in data.Values)
            {
                if (v != null && v.ToString().ToLowerInvariant().Contains(_filterText))
                    return true;
            }
            return false;
        }

        private void ToggleSort(string column)
        {
            if (_sortColumn == column)
                _sortAscending = !_sortAscending;
            else
            {
                _sortColumn = column;
                _sortAscending = true;
            }
            Rebuild();
        }

        private static int CompareValues(object a, object b)
        {
            if (a is double da && b is double db) return da.CompareTo(db);
            string sa = a?.ToString() ?? "";
            string sb = b?.ToString() ?? "";
            return string.Compare(sa, sb, StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatValue(object value)
        {
            if (value == null) return "null";
            if (value is double d) return d == (long)d ? ((long)d).ToString() : d.ToString("F2");
            if (value is bool b) return b ? "Yes" : "No";
            if (value is List<object> list) return $"[{list.Count}]";
            if (value is Dictionary<string, object>) return "{...}";
            return value.ToString();
        }
    }
}
