using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Live editor for vine node stats: cost, range, damage, intervals, etc.
    /// Changes apply immediately to the VineNodeRegistry.
    /// Left panel: node list. Right panel: property inspector.
    /// </summary>
    public partial class NodeBalanceEditor : EditorModule
    {
        public override string ModuleName => "Nodes";
        public override Color AccentColor => EditorStyles.AccentNodes;

        private VBoxContainer _nodeList;
        private VBoxContainer _inspector;
        private VineNodeData _selected;
        private readonly Dictionary<string, SpinBox> _spinBoxes = new();

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

            // ── Left: Node list ──
            var leftPanel = new PanelContainer();
            leftPanel.CustomMinimumSize = new Vector2(220, 0);
            leftPanel.AddThemeStyleboxOverride("panel", EditorStyles.MakePanel(EditorStyles.BgPanel));
            split.AddChild(leftPanel);

            var leftVBox = new VBoxContainer();
            leftVBox.AddThemeConstantOverride("separation", 2);
            leftPanel.AddChild(leftVBox);

            leftVBox.AddChild(EditorStyles.MakeLabel("Node Types", 16, AccentColor));
            leftVBox.AddChild(EditorStyles.MakeSeparator());

            var scroll = new ScrollContainer();
            scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            leftVBox.AddChild(scroll);

            _nodeList = new VBoxContainer();
            _nodeList.AddThemeConstantOverride("separation", 1);
            scroll.AddChild(_nodeList);

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

            PopulateNodeList();
        }

        private void PopulateNodeList()
        {
            foreach (var child in _nodeList.GetChildren())
                child.QueueFree();

            // Group by category
            AddCategoryHeader("Structural");
            foreach (var data in VineNodeRegistry.GetByCategory(VineNodeCategory.Structural))
                AddNodeButton(data);

            AddCategoryHeader("Sensors");
            foreach (var data in VineNodeRegistry.GetByCategory(VineNodeCategory.Sensor))
                AddNodeButton(data);

            AddCategoryHeader("Effects");
            foreach (var data in VineNodeRegistry.GetByCategory(VineNodeCategory.Effect))
                AddNodeButton(data);
        }

        private void AddCategoryHeader(string name)
        {
            var label = EditorStyles.MakeLabel(name, 12, EditorStyles.TextSecondary);
            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_top", 8);
            margin.AddChild(label);
            _nodeList.AddChild(margin);
        }

        private void AddNodeButton(VineNodeData data)
        {
            var btn = new Button();
            btn.Text = $"{data.Name} ({data.ResourceCost}g)";
            btn.Alignment = HorizontalAlignment.Left;
            btn.CustomMinimumSize = new Vector2(0, 28);
            btn.Pressed += () => SelectNode(data);
            _nodeList.AddChild(btn);
        }

        private void SelectNode(VineNodeData data)
        {
            _selected = data;
            BuildInspector();
        }

        private void BuildInspector()
        {
            foreach (var child in _inspector.GetChildren())
                child.QueueFree();
            _spinBoxes.Clear();

            if (_selected == null)
            {
                _inspector.AddChild(EditorStyles.MakeLabel("Select a node type", 14, EditorStyles.TextMuted));
                return;
            }

            // Header
            _inspector.AddChild(EditorStyles.MakeLabel(_selected.Name, 20, _selected.TintColor));
            _inspector.AddChild(EditorStyles.MakeLabel(_selected.Description, 12, EditorStyles.TextSecondary));
            _inspector.AddChild(EditorStyles.MakeSeparator());

            // Editable properties
            AddProperty("Resource Cost", _selected.ResourceCost, 0, 100, 1, v => _selected.ResourceCost = (int)v);
            AddProperty("Max Connections", _selected.MaxConnections, 1, 8, 1, v => _selected.MaxConnections = (int)v);

            if (_selected.Range > 0)
                AddProperty("Range", _selected.Range, 0.5f, 20f, 0.5f, v => _selected.Range = (float)v);

            if (_selected.Damage > 0)
                AddProperty("Damage/sec", _selected.Damage, 0.5f, 100f, 0.5f, v => _selected.Damage = (float)v);

            if (_selected.Interval > 0)
                AddProperty("Interval (sec)", _selected.Interval, 0.1f, 30f, 0.1f, v => _selected.Interval = (float)v);

            if (_selected.SlowAmount > 0)
                AddProperty("Slow Amount", _selected.SlowAmount, 0.05f, 0.95f, 0.05f, v => _selected.SlowAmount = (float)v);

            if (_selected.RequiredInputs > 0)
                AddProperty("Required Inputs", _selected.RequiredInputs, 1, 6, 1, v => _selected.RequiredInputs = (int)v);

            // Flags
            _inspector.AddChild(EditorStyles.MakeSeparator());
            AddCheckbox("Blocks Path", _selected.BlocksPath, v => _selected.BlocksPath = v);
            AddCheckbox("Dynamic Routing", _selected.HasDynamicRouting, v => _selected.HasDynamicRouting = v);

            // Color
            _inspector.AddChild(EditorStyles.MakeSeparator());
            _inspector.AddChild(EditorStyles.MakeLabel("Tint Color", 12, EditorStyles.TextSecondary));
            var colorPicker = new ColorPickerButton();
            colorPicker.Color = _selected.TintColor;
            colorPicker.CustomMinimumSize = new Vector2(200, 30);
            colorPicker.ColorChanged += c => _selected.TintColor = c;
            _inspector.AddChild(colorPicker);

            // Status
            _inspector.AddChild(EditorStyles.MakeSeparator());
            _inspector.AddChild(EditorStyles.MakeLabel(
                "Changes apply to registry immediately.\nNew nodes placed will use updated values.",
                11, EditorStyles.TextMuted));
        }

        private void AddProperty(string label, float value, float min, float max, float step,
            System.Action<double> onChange)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);

            var lbl = EditorStyles.MakeLabel(label, 13);
            lbl.CustomMinimumSize = new Vector2(150, 0);
            row.AddChild(lbl);

            var spin = EditorStyles.MakeSpinBox(value, min, max, step);
            spin.ValueChanged += (double v) => onChange(v);
            row.AddChild(spin);

            _spinBoxes[label] = spin;
            _inspector.AddChild(row);
        }

        private void AddCheckbox(string label, bool value, System.Action<bool> onChange)
        {
            var check = new CheckBox();
            check.Text = label;
            check.ButtonPressed = value;
            check.Toggled += (bool v) => onChange(v);
            check.AddThemeColorOverride("font_color", EditorStyles.TextPrimary);
            _inspector.AddChild(check);
        }

        public override void OnActivated()
        {
            // Refresh if registry has changed
        }
    }
}
