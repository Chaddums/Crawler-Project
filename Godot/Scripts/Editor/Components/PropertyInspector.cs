using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace JunkbotArena.Editor
{
    /// <summary>
    /// Auto-generates editor controls from a dictionary of key-value pairs.
    /// Supports float, int, string, bool, enum (as string), Color (as list), and arrays.
    /// Emits OnValueChanged when any field is edited.
    /// </summary>
    public partial class PropertyInspector : VBoxContainer
    {
        public event Action<string, object> OnValueChanged;

        private readonly Dictionary<string, Control> _controls = new();
        private Dictionary<string, object> _data;
        private Dictionary<string, PropertyHint> _hints;

        /// <summary>
        /// Describes how to display a property (range, enum options, etc).
        /// </summary>
        public class PropertyHint
        {
            public float Min { get; set; }
            public float Max { get; set; } = 100f;
            public float Step { get; set; } = 0.1f;
            public string[] EnumOptions { get; set; }
            public bool ReadOnly { get; set; }
            public string Tooltip { get; set; }
        }

        /// <summary>
        /// Build the inspector from a data dictionary.
        /// Optionally provide hints for specific keys.
        /// </summary>
        public void Build(Dictionary<string, object> data, Dictionary<string, PropertyHint> hints = null)
        {
            _data = data;
            _hints = hints ?? new Dictionary<string, PropertyHint>();
            _controls.Clear();

            // Clear existing children immediately (not deferred) to prevent stacking
            var children = GetChildren();
            for (int i = children.Count - 1; i >= 0; i--)
            {
                var child = children[i];
                RemoveChild(child);
                child.Free();
            }

            foreach (var kvp in data)
            {
                var row = CreateRow(kvp.Key, kvp.Value);
                if (row != null)
                {
                    AddChild(row);
                    AddChild(EditorStyles.MakeSeparator());
                }
            }
        }

        /// <summary>
        /// Update a single field value without rebuilding.
        /// </summary>
        public void SetValue(string key, object value)
        {
            if (_data != null) _data[key] = value;
        }

        private Control CreateRow(string key, object value)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);

            // Label
            var label = EditorStyles.MakeLabel(FormatKey(key), EditorStyles.FontSmall, EditorStyles.TextSecondary);
            label.CustomMinimumSize = new Vector2(160, 0);
            label.ClipText = true;
            label.TooltipText = _hints.TryGetValue(key, out var h) ? h.Tooltip ?? key : key;
            row.AddChild(label);

            // Value control
            Control control;
            _hints.TryGetValue(key, out var hint);

            if (hint?.EnumOptions != null)
            {
                control = CreateEnumControl(key, value?.ToString() ?? "", hint.EnumOptions);
            }
            else if (value is double d)
            {
                control = CreateFloatControl(key, (float)d, hint);
            }
            else if (value is float f)
            {
                control = CreateFloatControl(key, f, hint);
            }
            else if (value is bool b)
            {
                control = CreateBoolControl(key, b);
            }
            else if (value is string s)
            {
                control = CreateStringControl(key, s, hint);
            }
            else if (value is List<object> list)
            {
                // Check if it looks like a color [r, g, b] or [r, g, b, a]
                if (list.Count >= 3 && list.Count <= 4 && list[0] is double)
                {
                    control = CreateColorControl(key, list);
                }
                else
                {
                    control = CreateArrayLabel(key, list);
                }
            }
            else if (value is Dictionary<string, object>)
            {
                control = EditorStyles.MakeLabel("{...}", EditorStyles.FontSmall, EditorStyles.TextMuted);
            }
            else if (value == null)
            {
                control = EditorStyles.MakeLabel("null", EditorStyles.FontSmall, EditorStyles.TextMuted);
            }
            else
            {
                control = CreateStringControl(key, value.ToString(), hint);
            }

            control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(control);
            _controls[key] = control;

            return row;
        }

        private Control CreateFloatControl(string key, float value, PropertyHint hint)
        {
            var container = new HBoxContainer();
            container.AddThemeConstantOverride("separation", 4);

            float min = hint?.Min ?? -1000f;
            float max = hint?.Max ?? 1000f;
            float step = hint?.Step ?? 0.1f;

            var spinBox = new SpinBox();
            spinBox.MinValue = min;
            spinBox.MaxValue = max;
            spinBox.Step = step;
            spinBox.Value = value;
            spinBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            spinBox.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
            spinBox.Editable = !(hint?.ReadOnly ?? false);
            spinBox.ValueChanged += v =>
            {
                if (_data != null) _data[key] = (double)v;
                OnValueChanged?.Invoke(key, v);
            };
            container.AddChild(spinBox);

            return container;
        }

        private Control CreateBoolControl(string key, bool value)
        {
            var check = new CheckBox();
            check.ButtonPressed = value;
            check.Text = value ? "Yes" : "No";
            check.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
            check.Toggled += v =>
            {
                check.Text = v ? "Yes" : "No";
                if (_data != null) _data[key] = v;
                OnValueChanged?.Invoke(key, v);
            };
            return check;
        }

        private Control CreateStringControl(string key, string value, PropertyHint hint)
        {
            var edit = EditorStyles.MakeLineEdit("", EditorStyles.FontSmall);
            edit.Text = value;
            edit.Editable = !(hint?.ReadOnly ?? false);
            edit.TextSubmitted += text =>
            {
                if (_data != null) _data[key] = text;
                OnValueChanged?.Invoke(key, text);
            };
            return edit;
        }

        private Control CreateEnumControl(string key, string value, string[] options)
        {
            var dropdown = new OptionButton();
            dropdown.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);

            int selected = 0;
            for (int i = 0; i < options.Length; i++)
            {
                dropdown.AddItem(options[i]);
                if (options[i] == value) selected = i;
            }
            dropdown.Selected = selected;

            dropdown.ItemSelected += idx =>
            {
                string val = options[idx];
                if (_data != null) _data[key] = val;
                OnValueChanged?.Invoke(key, val);
            };

            return dropdown;
        }

        private Control CreateColorControl(string key, List<object> colorList)
        {
            float r = Convert.ToSingle(colorList[0]);
            float g = Convert.ToSingle(colorList[1]);
            float b = Convert.ToSingle(colorList[2]);
            float a = colorList.Count > 3 ? Convert.ToSingle(colorList[3]) : 1f;

            var picker = new ColorPickerButton();
            picker.Color = new Color(r, g, b, a);
            picker.CustomMinimumSize = new Vector2(80, 28);
            picker.ColorChanged += color =>
            {
                var newList = new List<object> { (double)color.R, (double)color.G, (double)color.B };
                if (colorList.Count > 3) newList.Add((double)color.A);
                if (_data != null) _data[key] = newList;
                OnValueChanged?.Invoke(key, newList);
            };
            return picker;
        }

        private Control CreateArrayLabel(string key, List<object> list)
        {
            var label = EditorStyles.MakeLabel($"[{list.Count} items]", EditorStyles.FontSmall, EditorStyles.TextMuted);
            return label;
        }

        private static string FormatKey(string key)
        {
            // camelCase/snake_case to Title Case
            var sb = new System.Text.StringBuilder();
            bool capitalize = true;
            foreach (char c in key)
            {
                if (c == '_' || c == ' ')
                {
                    sb.Append(' ');
                    capitalize = true;
                }
                else if (char.IsUpper(c) && sb.Length > 0 && !capitalize)
                {
                    sb.Append(' ');
                    sb.Append(c);
                    capitalize = false;
                }
                else
                {
                    sb.Append(capitalize ? char.ToUpper(c) : c);
                    capitalize = false;
                }
            }
            return sb.ToString();
        }
    }
}
