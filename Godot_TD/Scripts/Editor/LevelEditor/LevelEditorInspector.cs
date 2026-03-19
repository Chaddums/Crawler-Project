using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Right panel: transform spinboxes, type-specific properties,
    /// material override controls, multi-select actions,
    /// pathfinding validation display.
    /// </summary>
    public partial class LevelEditorInspector : VBoxContainer
    {
        public LevelEditorScene Editor { get; set; }

        private Label _selectionLabel;
        private SpinBox _posX, _posY, _posZ;
        private SpinBox _rotX, _rotY, _rotZ;
        private SpinBox _scaleX, _scaleY, _scaleZ;
        private VBoxContainer _propertiesPanel;
        private VBoxContainer _materialPanel;
        private VBoxContainer _actionsPanel;
        private VBoxContainer _validationPanel;
        private Label _validationStatus;

        public override void _Ready()
        {
            AddThemeConstantOverride("separation", 8);

            // Section: Selection info
            _selectionLabel = EditorStyles.MakeLabel("No selection", 12, EditorStyles.TextMuted);
            AddChild(_selectionLabel);

            // Section: Transform
            AddChild(EditorStyles.MakeLabel("TRANSFORM", 12, EditorStyles.TextSecondary));

            AddChild(MakeVec3Row("Position", out _posX, out _posY, out _posZ, -200, 200));
            AddChild(MakeVec3Row("Rotation", out _rotX, out _rotY, out _rotZ, -360, 360, 5f));
            AddChild(MakeVec3Row("Scale", out _scaleX, out _scaleY, out _scaleZ, 0.01f, 10f, 0.1f));

            // Wire value changed
            _posX.ValueChanged += _ => ApplyTransform();
            _posY.ValueChanged += _ => ApplyTransform();
            _posZ.ValueChanged += _ => ApplyTransform();
            _rotX.ValueChanged += _ => ApplyTransform();
            _rotY.ValueChanged += _ => ApplyTransform();
            _rotZ.ValueChanged += _ => ApplyTransform();
            _scaleX.ValueChanged += _ => ApplyTransform();
            _scaleY.ValueChanged += _ => ApplyTransform();
            _scaleZ.ValueChanged += _ => ApplyTransform();

            AddChild(EditorStyles.MakeSeparator());

            // Section: Properties (dynamic — light settings, etc.)
            AddChild(EditorStyles.MakeLabel("PROPERTIES", 12, EditorStyles.TextSecondary));
            _propertiesPanel = new VBoxContainer();
            AddChild(_propertiesPanel);

            AddChild(EditorStyles.MakeSeparator());

            // Section: Material Override
            AddChild(EditorStyles.MakeLabel("MATERIAL", 12, EditorStyles.TextSecondary));
            _materialPanel = new VBoxContainer();
            _materialPanel.AddThemeConstantOverride("separation", 4);
            AddChild(_materialPanel);

            AddChild(EditorStyles.MakeSeparator());

            // Section: Actions (duplicate, create assembly, delete)
            AddChild(EditorStyles.MakeLabel("ACTIONS", 12, EditorStyles.TextSecondary));
            _actionsPanel = new VBoxContainer();
            _actionsPanel.AddThemeConstantOverride("separation", 4);
            AddChild(_actionsPanel);

            AddChild(EditorStyles.MakeSeparator());

            // Section: Pathfinding Validation
            AddChild(EditorStyles.MakeLabel("PATH VALIDATION", 12, EditorStyles.TextSecondary));
            _validationPanel = new VBoxContainer();
            _validationPanel.AddThemeConstantOverride("separation", 4);
            AddChild(_validationPanel);

            var validateBtn = EditorStyles.MakeButton("Validate Paths", 13, EditorStyles.AccentNodes);
            validateBtn.Pressed += OnValidate;
            _validationPanel.AddChild(validateBtn);

            _validationStatus = EditorStyles.MakeLabel("Not validated", 12, EditorStyles.TextMuted);
            _validationPanel.AddChild(_validationStatus);

            // Height info
            AddChild(EditorStyles.MakeSeparator());
            AddChild(EditorStyles.MakeLabel("HEIGHT", 12, EditorStyles.TextSecondary));
            var regenBtn = EditorStyles.MakeButton("Regenerate Heightmap", 12);
            regenBtn.Pressed += OnRegenHeightmap;
            AddChild(regenBtn);
        }

        // ── Single selection ──

        public void UpdateFromSelection(Node3D selected)
        {
            ClearDynamic();

            if (selected == null)
            {
                _selectionLabel.Text = "No selection";
                _posX.Value = 0; _posY.Value = 0; _posZ.Value = 0;
                _rotX.Value = 0; _rotY.Value = 0; _rotZ.Value = 0;
                _scaleX.Value = 1; _scaleY.Value = 1; _scaleZ.Value = 1;
                return;
            }

            _selectionLabel.Text = "1 object selected";

            _posX.SetValueNoSignal(selected.Position.X);
            _posY.SetValueNoSignal(selected.Position.Y);
            _posZ.SetValueNoSignal(selected.Position.Z);
            _rotX.SetValueNoSignal(selected.RotationDegrees.X);
            _rotY.SetValueNoSignal(selected.RotationDegrees.Y);
            _rotZ.SetValueNoSignal(selected.RotationDegrees.Z);
            _scaleX.SetValueNoSignal(selected.Scale.X);
            _scaleY.SetValueNoSignal(selected.Scale.Y);
            _scaleZ.SetValueNoSignal(selected.Scale.Z);

            // Type-specific properties
            if (selected is Light3D light)
                BuildLightProperties(light);

            // Asset path label
            if (selected.HasMeta("asset_path"))
            {
                var pathLabel = EditorStyles.MakeLabel(
                    $"Path: {selected.GetMeta("asset_path").AsString()}", 10, EditorStyles.TextMuted);
                _propertiesPanel.AddChild(pathLabel);
            }

            // Material section
            BuildMaterialSection(selected);

            // Action buttons
            BuildSingleActions();
        }

        // ── Multi-selection ──

        public void UpdateFromMultiSelection(IReadOnlyList<Node3D> selectedNodes)
        {
            ClearDynamic();

            if (selectedNodes == null || selectedNodes.Count == 0)
            {
                UpdateFromSelection(null);
                return;
            }

            if (selectedNodes.Count == 1)
            {
                UpdateFromSelection(selectedNodes[0]);
                return;
            }

            _selectionLabel.Text = $"{selectedNodes.Count} objects selected";

            // Zero out transform (not meaningful for multi-select)
            _posX.SetValueNoSignal(0); _posY.SetValueNoSignal(0); _posZ.SetValueNoSignal(0);
            _rotX.SetValueNoSignal(0); _rotY.SetValueNoSignal(0); _rotZ.SetValueNoSignal(0);
            _scaleX.SetValueNoSignal(1); _scaleY.SetValueNoSignal(1); _scaleZ.SetValueNoSignal(1);

            // Batch material controls
            BuildBatchMaterialSection(selectedNodes);

            // Multi-select actions
            BuildMultiActions(selectedNodes);
        }

        private void ClearDynamic()
        {
            foreach (var c in _propertiesPanel.GetChildren()) c.QueueFree();
            foreach (var c in _materialPanel.GetChildren()) c.QueueFree();
            foreach (var c in _actionsPanel.GetChildren()) c.QueueFree();
        }

        // ── Material section (single) ──

        private void BuildMaterialSection(Node3D node)
        {
            // Faction dropdown
            var factionRow = new HBoxContainer();
            factionRow.AddChild(EditorStyles.MakeLabel("Faction:", 11, EditorStyles.TextMuted));
            var factionOption = new OptionButton();
            factionOption.AddItem("Player", 0);
            factionOption.AddItem("Scavenger", 1);
            factionOption.AddItem("Brute", 2);
            factionOption.AddItem("Swarm", 3);
            factionOption.AddItem("Ghost", 4);
            factionOption.CustomMinimumSize = new Vector2(100, 0);
            factionRow.AddChild(factionOption);
            _materialPanel.AddChild(factionRow);

            // Outline mode
            var outlineRow = new HBoxContainer();
            outlineRow.AddChild(EditorStyles.MakeLabel("Outline:", 11, EditorStyles.TextMuted));
            var outlineOption = new OptionButton();
            outlineOption.AddItem("Per-Mesh", 0);
            outlineOption.AddItem("Silhouette", 1);
            outlineOption.AddItem("None", 2);
            outlineOption.CustomMinimumSize = new Vector2(100, 0);
            outlineRow.AddChild(outlineOption);
            _materialPanel.AddChild(outlineRow);

            // Apply button
            var target = node;
            var applyBtn = EditorStyles.MakeButton("Apply Material", 11, EditorStyles.AccentNodes);
            applyBtn.Pressed += () =>
            {
                var data = LevelEditorMaterialPainter.MakeFactionPreset(factionOption.Selected);
                data.OutlineMode = outlineOption.Selected;
                LevelEditorMaterialPainter.Apply(target, data);

                // Update level data
                var tools = Editor?.GetNodeOrNull<LevelEditorTools>("LevelEditorTools");
                if (tools != null)
                    UpdatePlacementMaterial(target, data);

                Editor?.PushUndoState();
            };
            _materialPanel.AddChild(applyBtn);
        }

        // ── Material section (batch) ──

        private void BuildBatchMaterialSection(IReadOnlyList<Node3D> nodes)
        {
            var factionRow = new HBoxContainer();
            factionRow.AddChild(EditorStyles.MakeLabel("Faction:", 11, EditorStyles.TextMuted));
            var factionOption = new OptionButton();
            factionOption.AddItem("Player", 0);
            factionOption.AddItem("Scavenger", 1);
            factionOption.AddItem("Brute", 2);
            factionOption.AddItem("Swarm", 3);
            factionOption.AddItem("Ghost", 4);
            factionOption.CustomMinimumSize = new Vector2(100, 0);
            factionRow.AddChild(factionOption);
            _materialPanel.AddChild(factionRow);

            var applyBtn = EditorStyles.MakeButton("Apply to All", 11, EditorStyles.AccentNodes);
            applyBtn.Pressed += () =>
            {
                var data = LevelEditorMaterialPainter.MakeFactionPreset(factionOption.Selected);
                foreach (var node in nodes)
                {
                    LevelEditorMaterialPainter.Apply(node, data);
                    UpdatePlacementMaterial(node, data);
                }
                Editor?.PushUndoState();
            };
            _materialPanel.AddChild(applyBtn);
        }

        // ── Action buttons ──

        private void BuildSingleActions()
        {
            var dupBtn = EditorStyles.MakeButton("Duplicate (Ctrl+D)", 11);
            dupBtn.Pressed += () =>
            {
                var tools = Editor?.GetNodeOrNull<LevelEditorTools>("LevelEditorTools");
                // Simulate Ctrl+D
                var keyEvent = new InputEventKey();
                keyEvent.Keycode = Key.D;
                keyEvent.CtrlPressed = true;
                keyEvent.Pressed = true;
                tools?._UnhandledInput(keyEvent);
            };
            _actionsPanel.AddChild(dupBtn);

            var delBtn = EditorStyles.MakeButton("Delete (Del)", 11, EditorStyles.StatusError);
            delBtn.Pressed += () =>
            {
                var tools = Editor?.GetNodeOrNull<LevelEditorTools>("LevelEditorTools");
                tools?.DeleteSelected();
            };
            _actionsPanel.AddChild(delBtn);
        }

        private void BuildMultiActions(IReadOnlyList<Node3D> nodes)
        {
            // Create Assembly
            if (nodes.Count >= 2)
            {
                var createAsmBtn = EditorStyles.MakeButton("Create Assembly", 11, EditorStyles.AccentMap);
                createAsmBtn.Pressed += () => ShowAssemblyNamePrompt(nodes);
                _actionsPanel.AddChild(createAsmBtn);
            }

            var dupBtn = EditorStyles.MakeButton("Duplicate All (Ctrl+D)", 11);
            dupBtn.Pressed += () =>
            {
                var tools = Editor?.GetNodeOrNull<LevelEditorTools>("LevelEditorTools");
                var keyEvent = new InputEventKey();
                keyEvent.Keycode = Key.D;
                keyEvent.CtrlPressed = true;
                keyEvent.Pressed = true;
                tools?._UnhandledInput(keyEvent);
            };
            _actionsPanel.AddChild(dupBtn);

            var delBtn = EditorStyles.MakeButton("Delete All (Del)", 11, EditorStyles.StatusError);
            delBtn.Pressed += () =>
            {
                var tools = Editor?.GetNodeOrNull<LevelEditorTools>("LevelEditorTools");
                tools?.DeleteSelected();
            };
            _actionsPanel.AddChild(delBtn);
        }

        private void ShowAssemblyNamePrompt(IReadOnlyList<Node3D> nodes)
        {
            var popup = new AcceptDialog();
            popup.Title = "Create Assembly";
            popup.DialogText = "Assembly name:";

            var nameEdit = EditorStyles.MakeLineEdit("my_assembly");
            popup.AddChild(nameEdit);

            popup.Confirmed += () =>
            {
                string name = nameEdit.Text;
                if (string.IsNullOrWhiteSpace(name)) name = "unnamed";
                var assembly = Editor?.GetNodeOrNull<LevelEditorAssembly>("LevelEditorAssembly");
                assembly?.CreateFromSelection(nodes, name);
                popup.QueueFree();
            };
            popup.Canceled += () => popup.QueueFree();

            AddChild(popup);
            popup.PopupCentered(new Vector2I(300, 120));
        }

        private void UpdatePlacementMaterial(Node3D node, MaterialOverrideData data)
        {
            if (Editor?.CurrentLevel == null) return;
            foreach (var asset in Editor.CurrentLevel.Assets)
            {
                if (Mathf.Abs(asset.PosX - node.Position.X) < 0.01f &&
                    Mathf.Abs(asset.PosZ - node.Position.Z) < 0.01f)
                {
                    asset.MaterialOverride = data;
                    return;
                }
            }
        }

        // ── Existing methods ──

        private void BuildLightProperties(Light3D light)
        {
            var colorLabel = EditorStyles.MakeLabel($"Color: {light.LightColor}", 12);
            _propertiesPanel.AddChild(colorLabel);

            var energySpin = EditorStyles.MakeSpinBox((float)light.LightEnergy, 0, 16, 0.1f);
            var energyRow = MakeLabeledRow("Energy", energySpin);
            _propertiesPanel.AddChild(energyRow);
            energySpin.ValueChanged += v => light.LightEnergy = (float)v;

            if (light is OmniLight3D omni)
            {
                var rangeSpin = EditorStyles.MakeSpinBox(omni.OmniRange, 1, 50, 0.5f);
                var rangeRow = MakeLabeledRow("Range", rangeSpin);
                _propertiesPanel.AddChild(rangeRow);
                rangeSpin.ValueChanged += v => omni.OmniRange = (float)v;
            }
            else if (light is SpotLight3D spot)
            {
                var rangeSpin = EditorStyles.MakeSpinBox(spot.SpotRange, 1, 50, 0.5f);
                var rangeRow = MakeLabeledRow("Range", rangeSpin);
                _propertiesPanel.AddChild(rangeRow);
                rangeSpin.ValueChanged += v => spot.SpotRange = (float)v;
            }
        }

        private void ApplyTransform()
        {
            var tools = Editor?.GetNode<LevelEditorTools>("LevelEditorTools");
            if (tools?.SelectedNode == null) return;

            tools.SelectedNode.Position = new Vector3((float)_posX.Value, (float)_posY.Value, (float)_posZ.Value);
            tools.SelectedNode.RotationDegrees = new Vector3((float)_rotX.Value, (float)_rotY.Value, (float)_rotZ.Value);
            tools.SelectedNode.Scale = new Vector3((float)_scaleX.Value, (float)_scaleY.Value, (float)_scaleZ.Value);
        }

        private void OnValidate()
        {
            var grid = Editor?.Grid;
            if (grid == null) return;

            var pathfinder = new VinePathfinder();
            Editor.AddChild(pathfinder);
            pathfinder.Initialize(grid);

            int validCount = 0;
            int totalEntries = grid.EntryPoints.Count;

            foreach (var entry in grid.EntryPoints)
            {
                var path = pathfinder.FindPath(entry, grid.ExitPoint);
                if (path != null && path.Count > 0)
                    validCount++;
            }

            pathfinder.QueueFree();

            if (validCount == totalEntries)
            {
                _validationStatus.Text = $"All {totalEntries} paths valid";
                _validationStatus.AddThemeColorOverride("font_color", EditorStyles.StatusOk);
            }
            else
            {
                _validationStatus.Text = $"{validCount}/{totalEntries} paths valid";
                _validationStatus.AddThemeColorOverride("font_color", EditorStyles.StatusError);
            }
        }

        private void OnRegenHeightmap()
        {
            var grid = Editor?.Grid;
            if (grid == null) return;

            TerrainProfile profile = Editor.CurrentLevel.HeightmapProfile switch
            {
                "Valley" => TerrainProfile.Valley,
                "Complex" => TerrainProfile.Complex,
                _ => TerrainProfile.Gentle
            };

            var overrides = new List<HeightOverride>();
            if (Editor.CurrentLevel.HeightOverrides != null)
            {
                foreach (var ov in Editor.CurrentLevel.HeightOverrides)
                    overrides.Add(new HeightOverride(ov.X1, ov.Y1, ov.X2, ov.Y2, ov.TargetHeight));
            }

            grid.GenerateHeightmap(profile, overrides);
            Editor.RefreshTerrain();
            Editor.PushUndoState();
        }

        // ── Helpers ──

        private static HBoxContainer MakeVec3Row(string label, out SpinBox x, out SpinBox y, out SpinBox z,
            float min = -100, float max = 100, float step = 0.1f)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 4);

            var lbl = EditorStyles.MakeLabel(label, 11, EditorStyles.TextMuted);
            lbl.CustomMinimumSize = new Vector2(60, 0);
            row.AddChild(lbl);

            x = EditorStyles.MakeSpinBox(0, min, max, step);
            x.CustomMinimumSize = new Vector2(55, 0);
            y = EditorStyles.MakeSpinBox(0, min, max, step);
            y.CustomMinimumSize = new Vector2(55, 0);
            z = EditorStyles.MakeSpinBox(0, min, max, step);
            z.CustomMinimumSize = new Vector2(55, 0);

            row.AddChild(x);
            row.AddChild(y);
            row.AddChild(z);

            return row;
        }

        private static HBoxContainer MakeLabeledRow(string label, Control control)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);
            var lbl = EditorStyles.MakeLabel(label, 11, EditorStyles.TextMuted);
            lbl.CustomMinimumSize = new Vector2(60, 0);
            row.AddChild(lbl);
            row.AddChild(control);
            return row;
        }
    }
}
