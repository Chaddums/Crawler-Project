using Godot;
using System;
using System.Collections.Generic;

namespace JunkbotArena.Editor
{
    /// <summary>
    /// Asset Editor — browse, preview, and configure dungeon assets by ID.
    /// Left: category picker + asset list. Center: 3D preview with collision wireframe.
    /// Right: transform/collision/config controls. Saves to asset_config.json.
    /// </summary>
    public partial class AssetEditor : EditorPanel
    {
        public override string PanelName => "Assets";
        public override Color AccentColor => EditorStyles.AccentAsset;

        // Category + asset list
        private OptionButton _categoryPicker;
        private VBoxContainer _assetList;
        private ScrollContainer _assetScroll;
        private Label _assetCount;

        // 3D Preview
        private SubViewport _viewport;
        private SubViewportContainer _viewportContainer;
        private Node3D _previewRoot;
        private Camera3D _camera;
        private float _cameraAngle;
        private float _cameraRadius = 5f;
        private float _cameraHeight = 3f;
        private Label _previewLabel;
        private bool _autoRotate = true;
        private bool _isDragging;
        private Vector2 _lastMousePos;
        private bool _showCollision = true;
        private Node3D _collisionOverlay;

        // Config panel
        private Label _idLabel;
        private SpinBox _scaleX, _scaleY, _scaleZ;
        private SpinBox _rotY;
        private OptionButton _collisionType;
        private SpinBox _collSizeX, _collSizeY, _collSizeZ;
        private SpinBox _collRadius;
        private SpinBox _collOffsetY;
        private Label _aabbLabel;

        // State
        private string _selectedCategory = "prop";
        private string _selectedAssetId;
        private Dictionary<string, object> _assetConfigs;
        private Dictionary<string, object> _rawJson;

        private static readonly string[] Categories =
            { "prop", "floor", "wall", "door", "detail", "player", "enemy" };

        protected override void BuildUI(VBoxContainer content)
        {
            var split = new HBoxContainer();
            split.SizeFlagsVertical = SizeFlags.ExpandFill;
            split.AddThemeConstantOverride("separation", 8);

            // === LEFT: Category + Asset List ===
            var leftPanel = new VBoxContainer();
            leftPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            leftPanel.CustomMinimumSize = new Vector2(200, 0);

            // Category picker
            leftPanel.AddChild(EditorStyles.MakeLabel("Category:", EditorStyles.FontSmall, EditorStyles.TextSecondary));
            _categoryPicker = new OptionButton();
            _categoryPicker.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
            foreach (var cat in Categories)
                _categoryPicker.AddItem(cat);
            _categoryPicker.ItemSelected += idx => { _selectedCategory = Categories[idx]; PopulateAssetList(); };
            leftPanel.AddChild(_categoryPicker);

            leftPanel.AddChild(EditorStyles.MakeSeparator());

            _assetCount = EditorStyles.MakeLabel("", EditorStyles.FontTiny, EditorStyles.TextMuted);
            leftPanel.AddChild(_assetCount);

            // Asset list
            _assetScroll = new ScrollContainer();
            _assetScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            _assetScroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            _assetList = new VBoxContainer();
            _assetList.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _assetList.AddThemeConstantOverride("separation", 2);
            _assetScroll.AddChild(_assetList);
            leftPanel.AddChild(_assetScroll);

            // ID lookup
            leftPanel.AddChild(EditorStyles.MakeSeparator());
            leftPanel.AddChild(EditorStyles.MakeLabel("Quick Lookup:", EditorStyles.FontSmall, EditorStyles.TextSecondary));
            var lookupEdit = EditorStyles.MakeLineEdit("asset_id", EditorStyles.FontSmall);
            lookupEdit.TextSubmitted += OnLookupSubmitted;
            leftPanel.AddChild(lookupEdit);

            split.AddChild(leftPanel);

            // === CENTER: 3D Preview ===
            var centerPanel = new VBoxContainer();
            centerPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            centerPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            _previewLabel = EditorStyles.MakeLabel("Select an asset", EditorStyles.FontBody, AccentColor);
            _previewLabel.HorizontalAlignment = HorizontalAlignment.Center;
            centerPanel.AddChild(_previewLabel);

            _viewportContainer = new SubViewportContainer();
            _viewportContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
            _viewportContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _viewportContainer.Stretch = true;
            _viewportContainer.GuiInput += OnViewportInput;

            _viewport = new SubViewport();
            _viewport.Size = new Vector2I(512, 400);
            _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
            _viewport.OwnWorld3D = true;

            _camera = new Camera3D();
            _camera.Position = new Vector3(3, 3, 3);
            _camera.LookAt(Vector3.Zero);
            _viewport.AddChild(_camera);

            _previewRoot = new Node3D();
            _viewport.AddChild(_previewRoot);

            _collisionOverlay = new Node3D();
            _collisionOverlay.Name = "CollisionOverlay";
            _viewport.AddChild(_collisionOverlay);

            // Lighting
            var light = new DirectionalLight3D();
            light.Position = new Vector3(5, 10, 5);
            light.LookAt(Vector3.Zero);
            light.LightEnergy = 1.2f;
            _viewport.AddChild(light);

            var fill = new DirectionalLight3D();
            fill.Position = new Vector3(-5, 8, -3);
            fill.LookAt(Vector3.Zero);
            fill.LightEnergy = 0.4f;
            _viewport.AddChild(fill);

            var env = new WorldEnvironment();
            var envRes = new Godot.Environment();
            envRes.BackgroundMode = Godot.Environment.BGMode.Color;
            envRes.BackgroundColor = new Color(0.06f, 0.06f, 0.08f);
            envRes.AmbientLightSource = Godot.Environment.AmbientSource.Color;
            envRes.AmbientLightColor = new Color(0.15f, 0.15f, 0.18f);
            env.Environment = envRes;
            _viewport.AddChild(env);

            // Grid floor reference
            AddGridFloor();

            _viewportContainer.AddChild(_viewport);
            centerPanel.AddChild(_viewportContainer);

            // AABB info
            _aabbLabel = EditorStyles.MakeLabel("", EditorStyles.FontTiny, EditorStyles.TextMuted);
            centerPanel.AddChild(_aabbLabel);

            // Collision toggle
            var collCheck = new CheckBox();
            collCheck.Text = "Show Collision Shape";
            collCheck.ButtonPressed = true;
            collCheck.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
            collCheck.Toggled += v => { _showCollision = v; _collisionOverlay.Visible = v; };
            centerPanel.AddChild(collCheck);

            // Auto-rotate toggle
            var rotateCheck = new CheckBox();
            rotateCheck.Text = "Auto-Rotate";
            rotateCheck.ButtonPressed = true;
            rotateCheck.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
            rotateCheck.Toggled += v => _autoRotate = v;
            centerPanel.AddChild(rotateCheck);

            split.AddChild(centerPanel);

            // === RIGHT: Config Panel ===
            var rightPanel = new VBoxContainer();
            rightPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            rightPanel.CustomMinimumSize = new Vector2(250, 0);

            rightPanel.AddChild(EditorStyles.MakeLabel("Asset Config", EditorStyles.FontHeader, AccentColor));
            _idLabel = EditorStyles.MakeLabel("No asset selected", EditorStyles.FontBody, EditorStyles.TextSecondary);
            rightPanel.AddChild(_idLabel);
            rightPanel.AddChild(EditorStyles.MakeSeparator());

            // Scale controls
            rightPanel.AddChild(EditorStyles.MakeLabel("Scale:", EditorStyles.FontSmall, EditorStyles.TextSecondary));
            var scaleRow = new HBoxContainer();
            scaleRow.AddThemeConstantOverride("separation", 4);
            scaleRow.AddChild(EditorStyles.MakeLabel("X", EditorStyles.FontTiny, EditorStyles.TextMuted));
            _scaleX = MakeSpinBox(0.01, 10, 0.01, 1.0);
            scaleRow.AddChild(_scaleX);
            scaleRow.AddChild(EditorStyles.MakeLabel("Y", EditorStyles.FontTiny, EditorStyles.TextMuted));
            _scaleY = MakeSpinBox(0.01, 10, 0.01, 1.0);
            scaleRow.AddChild(_scaleY);
            scaleRow.AddChild(EditorStyles.MakeLabel("Z", EditorStyles.FontTiny, EditorStyles.TextMuted));
            _scaleZ = MakeSpinBox(0.01, 10, 0.01, 1.0);
            scaleRow.AddChild(_scaleZ);
            rightPanel.AddChild(scaleRow);

            // Rotation
            rightPanel.AddChild(EditorStyles.MakeLabel("Rotation Y:", EditorStyles.FontSmall, EditorStyles.TextSecondary));
            _rotY = MakeSpinBox(0, 360, 15, 0);
            rightPanel.AddChild(_rotY);

            rightPanel.AddChild(EditorStyles.MakeSeparator());

            // Collision shape config
            rightPanel.AddChild(EditorStyles.MakeLabel("Collision Shape:", EditorStyles.FontSmall, EditorStyles.TextSecondary));
            _collisionType = new OptionButton();
            _collisionType.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
            _collisionType.AddItem("None");
            _collisionType.AddItem("Box");
            _collisionType.AddItem("Cylinder");
            _collisionType.AddItem("Sphere");
            _collisionType.AddItem("Ramp");
            _collisionType.AddItem("Doorframe");
            _collisionType.ItemSelected += _ => OnConfigChanged();
            rightPanel.AddChild(_collisionType);

            rightPanel.AddChild(EditorStyles.MakeLabel("Size (X/Y/Z):", EditorStyles.FontSmall, EditorStyles.TextSecondary));
            var sizeRow = new HBoxContainer();
            sizeRow.AddThemeConstantOverride("separation", 4);
            _collSizeX = MakeSpinBox(0.1, 50, 0.1, 1.0);
            _collSizeY = MakeSpinBox(0.1, 50, 0.1, 1.0);
            _collSizeZ = MakeSpinBox(0.1, 50, 0.1, 1.0);
            sizeRow.AddChild(_collSizeX);
            sizeRow.AddChild(_collSizeY);
            sizeRow.AddChild(_collSizeZ);
            rightPanel.AddChild(sizeRow);

            rightPanel.AddChild(EditorStyles.MakeLabel("Radius / Thickness:", EditorStyles.FontSmall, EditorStyles.TextSecondary));
            _collRadius = MakeSpinBox(0.1, 25, 0.1, 0.5);
            rightPanel.AddChild(_collRadius);

            rightPanel.AddChild(EditorStyles.MakeLabel("Offset Y:", EditorStyles.FontSmall, EditorStyles.TextSecondary));
            _collOffsetY = MakeSpinBox(-25, 25, 0.1, 0.0);
            _collOffsetY.ValueChanged += _ => OnConfigChanged();
            rightPanel.AddChild(_collOffsetY);

            rightPanel.AddChild(EditorStyles.MakeSeparator());

            // Apply + Preview buttons
            var applyBtn = EditorStyles.MakeButton("Apply & Preview", EditorStyles.FontSmall, AccentColor);
            applyBtn.CustomMinimumSize = new Vector2(0, 28);
            applyBtn.Pressed += OnApplyConfig;
            rightPanel.AddChild(applyBtn);

            var resetBtn = EditorStyles.MakeButton("Reset to Default", EditorStyles.FontSmall, EditorStyles.StatusError);
            resetBtn.CustomMinimumSize = new Vector2(0, 28);
            resetBtn.Pressed += OnResetConfig;
            rightPanel.AddChild(resetBtn);

            // Spacer
            var spacer = new Control();
            spacer.SizeFlagsVertical = SizeFlags.ExpandFill;
            rightPanel.AddChild(spacer);

            // Camera controls
            rightPanel.AddChild(EditorStyles.MakeSeparator());
            rightPanel.AddChild(EditorStyles.MakeLabel("Camera:", EditorStyles.FontSmall, EditorStyles.TextSecondary));
            var camRow = new HBoxContainer();
            camRow.AddThemeConstantOverride("separation", 4);
            camRow.AddChild(EditorStyles.MakeLabel("Dist", EditorStyles.FontTiny, EditorStyles.TextMuted));
            var camDist = MakeSpinBox(1, 30, 0.5, 5);
            camDist.ValueChanged += v => _cameraRadius = (float)v;
            camRow.AddChild(camDist);
            camRow.AddChild(EditorStyles.MakeLabel("Height", EditorStyles.FontTiny, EditorStyles.TextMuted));
            var camHeight = MakeSpinBox(0.5, 20, 0.5, 3);
            camHeight.ValueChanged += v => _cameraHeight = (float)v;
            camRow.AddChild(camHeight);
            rightPanel.AddChild(camRow);

            split.AddChild(rightPanel);
            content.AddChild(split);
        }

        public override void _Process(double delta)
        {
            if (_previewRoot != null && Visible)
            {
                if (_autoRotate)
                    _cameraAngle += (float)delta * 0.4f;
                _camera.Position = new Vector3(
                    Mathf.Cos(_cameraAngle) * _cameraRadius,
                    _cameraHeight,
                    Mathf.Sin(_cameraAngle) * _cameraRadius
                );
                _camera.LookAt(new Vector3(0, _cameraHeight * 0.3f, 0));
            }
        }

        private void OnViewportInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mb)
            {
                if (mb.ButtonIndex == MouseButton.WheelUp)
                {
                    _cameraRadius = Mathf.Max(1f, _cameraRadius - 0.5f);
                    _viewportContainer.AcceptEvent();
                }
                else if (mb.ButtonIndex == MouseButton.WheelDown)
                {
                    _cameraRadius = Mathf.Min(20f, _cameraRadius + 0.5f);
                    _viewportContainer.AcceptEvent();
                }
                else if (mb.ButtonIndex == MouseButton.Left || mb.ButtonIndex == MouseButton.Middle)
                {
                    _isDragging = mb.Pressed;
                    _lastMousePos = mb.Position;
                    _viewportContainer.AcceptEvent();
                }
            }

            if (@event is InputEventMouseMotion mm && _isDragging && !_autoRotate)
            {
                var delta = mm.Position - _lastMousePos;
                _cameraAngle -= delta.X * 0.005f;
                _lastMousePos = mm.Position;
                _viewportContainer.AcceptEvent();
            }
        }

        // ===== ASSET LIST =====

        private void PopulateAssetList()
        {
            if (_assetList == null) return;

            foreach (var child in _assetList.GetChildren())
                if (child is Node n) n.QueueFree();

            ModelLibrary.Initialize();
            var ids = ModelLibrary.GetCategoryIds(_selectedCategory);
            Array.Sort(ids);

            _assetCount.Text = $"{ids.Length} assets in '{_selectedCategory}'";

            foreach (var id in ids)
            {
                var btn = EditorStyles.MakeButton(id, EditorStyles.FontSmall);
                btn.Alignment = HorizontalAlignment.Left;
                btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                var capturedId = id;
                btn.Pressed += () => SelectAsset(capturedId);
                _assetList.AddChild(btn);
            }
        }

        private void OnLookupSubmitted(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;

            // Try to find in current category first, then search all
            id = id.ToLower().Replace(" ", "_").Replace("-", "_");

            if (ModelLibrary.HasModel(_selectedCategory, id))
            {
                SelectAsset(id);
                return;
            }

            foreach (var cat in Categories)
            {
                if (ModelLibrary.HasModel(cat, id))
                {
                    // Switch category
                    for (int i = 0; i < Categories.Length; i++)
                    {
                        if (Categories[i] == cat)
                        {
                            _categoryPicker.Selected = i;
                            _selectedCategory = cat;
                            PopulateAssetList();
                            break;
                        }
                    }
                    SelectAsset(id);
                    return;
                }
            }

            SetStatus($"Asset '{id}' not found in any category", EditorStyles.StatusError);
        }

        private void SelectAsset(string id)
        {
            _selectedAssetId = id;
            _idLabel.Text = $"{_selectedCategory}/{id}";
            _idLabel.AddThemeColorOverride("font_color", AccentColor);

            // Load saved config if exists
            LoadAssetConfig(id);

            // Preview
            PreviewAsset();
        }

        // ===== 3D PREVIEW =====

        private void PreviewAsset()
        {
            if (_previewRoot == null || string.IsNullOrEmpty(_selectedAssetId)) return;

            // Clear
            foreach (var child in _previewRoot.GetChildren())
                if (child is Node n) n.QueueFree();
            foreach (var child in _collisionOverlay.GetChildren())
                if (child is Node n) n.QueueFree();

            var model = ModelLibrary.TryLoad(_selectedCategory, _selectedAssetId);
            if (model == null)
            {
                _previewLabel.Text = $"No model: {_selectedCategory}/{_selectedAssetId}";
                _aabbLabel.Text = "Model not found";
                return;
            }

            // Apply current config
            model.Scale = new Vector3((float)_scaleX.Value, (float)_scaleY.Value, (float)_scaleZ.Value);
            model.RotationDegrees = new Vector3(0, (float)_rotY.Value, 0);
            _previewRoot.AddChild(model);

            // Measure and display AABB
            var aabb = GetModelAabb(model);
            _aabbLabel.Text = $"AABB: Size({aabb.Size.X:F2}, {aabb.Size.Y:F2}, {aabb.Size.Z:F2}) | " +
                              $"Pos({aabb.Position.X:F2}, {aabb.Position.Y:F2}, {aabb.Position.Z:F2})";
            _previewLabel.Text = $"{_selectedCategory}/{_selectedAssetId}";

            // Add collision shape preview
            BuildCollisionPreview();
        }

        private void BuildCollisionPreview()
        {
            foreach (var child in _collisionOverlay.GetChildren())
                if (child is Node n) n.QueueFree();

            int collType = _collisionType.Selected;
            if (collType <= 0) return;

            float sizeX = (float)_collSizeX.Value;
            float sizeY = (float)_collSizeY.Value;
            float sizeZ = (float)_collSizeZ.Value;
            float radius = (float)_collRadius.Value;
            float offsetY = (float)_collOffsetY.Value;

            var collMat = new StandardMaterial3D();
            collMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            collMat.AlbedoColor = new Color(0f, 1f, 0.3f, 0.2f);
            collMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            collMat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;

            if (collType == 5) // Doorframe — three boxes (left pillar, right pillar, top lintel)
            {
                float thickness = radius;
                float pillarH = sizeY - thickness;
                float halfW = sizeX / 2f;

                AddCollisionBox(new Vector3(thickness, pillarH, sizeZ),
                    new Vector3(-halfW + thickness / 2f, pillarH / 2f + offsetY, 0), collMat);
                AddCollisionBox(new Vector3(thickness, pillarH, sizeZ),
                    new Vector3(halfW - thickness / 2f, pillarH / 2f + offsetY, 0), collMat);
                AddCollisionBox(new Vector3(sizeX, thickness, sizeZ),
                    new Vector3(0, sizeY - thickness / 2f + offsetY, 0), collMat);

                _collisionOverlay.Visible = _showCollision;
                return;
            }

            Mesh mesh = null;
            if (collType == 1) // Box
                mesh = new BoxMesh { Size = new Vector3(sizeX, sizeY, sizeZ) };
            else if (collType == 2) // Cylinder
                mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = sizeY };
            else if (collType == 3) // Sphere
                mesh = new SphereMesh { Radius = radius, Height = radius * 2 };
            else if (collType == 4) // Ramp
                mesh = CreateRampMesh(sizeX, sizeY, sizeZ);

            if (mesh == null) return;

            var mi = new MeshInstance3D();
            mi.Mesh = mesh;
            mi.Position = new Vector3(0, sizeY / 2f + offsetY, 0);
            mi.MaterialOverride = collMat;
            _collisionOverlay.AddChild(mi);
            _collisionOverlay.Visible = _showCollision;
        }

        private Aabb GetModelAabb(Node3D model)
        {
            var aabb = new Aabb();
            bool first = true;
            CollectAabb(model, ref aabb, ref first);
            return aabb;
        }

        private void CollectAabb(Node node, ref Aabb aabb, ref bool first)
        {
            if (node is MeshInstance3D mi && mi.Mesh != null)
            {
                var meshAabb = mi.Mesh.GetAabb();
                // Transform to model space
                var transformed = mi.Transform * meshAabb;
                if (first) { aabb = transformed; first = false; }
                else aabb = aabb.Merge(transformed);
            }
            foreach (var child in node.GetChildren())
                if (child is Node n) CollectAabb(n, ref aabb, ref first);
        }

        private void AddGridFloor()
        {
            // Simple reference grid
            var grid = new MeshInstance3D();
            var planeMesh = new PlaneMesh();
            planeMesh.Size = new Vector2(10, 10);
            planeMesh.SubdivideWidth = 10;
            planeMesh.SubdivideDepth = 10;
            grid.Mesh = planeMesh;
            grid.Position = new Vector3(0, -0.01f, 0);

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.15f, 0.15f, 0.18f, 0.5f);
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            grid.MaterialOverride = mat;
            _viewport.AddChild(grid);
        }

        // ===== CONFIG LOAD/SAVE =====

        private void LoadAssetConfig(string id)
        {
            var key = $"{_selectedCategory}/{id}";

            // Defaults
            _scaleX.Value = 1.0;
            _scaleY.Value = 1.0;
            _scaleZ.Value = 1.0;
            _rotY.Value = 0;
            _collisionType.Selected = 0;
            _collSizeX.Value = 1.0;
            _collSizeY.Value = 1.0;
            _collSizeZ.Value = 1.0;
            _collRadius.Value = 0.5;
            _collOffsetY.Value = 0.0;

            if (_assetConfigs != null && _assetConfigs.TryGetValue(key, out var cfgObj) &&
                cfgObj is Dictionary<string, object> cfg)
            {
                if (cfg.TryGetValue("scaleX", out var sx)) _scaleX.Value = Convert.ToDouble(sx);
                if (cfg.TryGetValue("scaleY", out var sy)) _scaleY.Value = Convert.ToDouble(sy);
                if (cfg.TryGetValue("scaleZ", out var sz)) _scaleZ.Value = Convert.ToDouble(sz);
                if (cfg.TryGetValue("rotY", out var ry)) _rotY.Value = Convert.ToDouble(ry);
                if (cfg.TryGetValue("collisionType", out var ct)) _collisionType.Selected = Convert.ToInt32(ct);
                if (cfg.TryGetValue("collSizeX", out var cx)) _collSizeX.Value = Convert.ToDouble(cx);
                if (cfg.TryGetValue("collSizeY", out var cy)) _collSizeY.Value = Convert.ToDouble(cy);
                if (cfg.TryGetValue("collSizeZ", out var cz)) _collSizeZ.Value = Convert.ToDouble(cz);
                if (cfg.TryGetValue("collRadius", out var cr)) _collRadius.Value = Convert.ToDouble(cr);
                if (cfg.TryGetValue("collOffsetY", out var co)) _collOffsetY.Value = Convert.ToDouble(co);

                SetStatus($"Loaded config for {key}", EditorStyles.StatusSaved);
            }
        }

        private void OnConfigChanged()
        {
            // Rebuild collision preview when type changes
            BuildCollisionPreview();
        }

        private void OnApplyConfig()
        {
            if (string.IsNullOrEmpty(_selectedAssetId)) return;

            var key = $"{_selectedCategory}/{_selectedAssetId}";

            if (_assetConfigs == null)
                _assetConfigs = new Dictionary<string, object>();

            var cfg = new Dictionary<string, object>
            {
                ["scaleX"] = _scaleX.Value,
                ["scaleY"] = _scaleY.Value,
                ["scaleZ"] = _scaleZ.Value,
                ["rotY"] = _rotY.Value,
                ["collisionType"] = _collisionType.Selected,
                ["collSizeX"] = _collSizeX.Value,
                ["collSizeY"] = _collSizeY.Value,
                ["collSizeZ"] = _collSizeZ.Value,
                ["collRadius"] = _collRadius.Value,
                ["collOffsetY"] = _collOffsetY.Value,
            };

            PushUndo(MiniJsonWriter.Serialize(_assetConfigs));
            _assetConfigs[key] = cfg;
            MarkDirty();

            // Refresh preview with new config
            PreviewAsset();
            SetStatus($"Applied config for {key}", EditorStyles.StatusSaved);
        }

        private void OnResetConfig()
        {
            if (string.IsNullOrEmpty(_selectedAssetId)) return;

            var key = $"{_selectedCategory}/{_selectedAssetId}";

            if (_assetConfigs != null && _assetConfigs.ContainsKey(key))
            {
                PushUndo(MiniJsonWriter.Serialize(_assetConfigs));
                _assetConfigs.Remove(key);
                MarkDirty();
            }

            // Reset to defaults
            _scaleX.Value = 1.0;
            _scaleY.Value = 1.0;
            _scaleZ.Value = 1.0;
            _rotY.Value = 0;
            _collisionType.Selected = 0;
            _collSizeX.Value = 1.0;
            _collSizeY.Value = 1.0;
            _collSizeZ.Value = 1.0;
            _collRadius.Value = 0.5;
            _collOffsetY.Value = 0.0;

            PreviewAsset();
            SetStatus($"Reset {key} to defaults", EditorStyles.TextMuted);
        }

        // ===== HELPERS =====

        private static SpinBox MakeSpinBox(double min, double max, double step, double value)
        {
            var sb = new SpinBox();
            sb.MinValue = min;
            sb.MaxValue = max;
            sb.Step = step;
            sb.Value = value;
            sb.CustomMinimumSize = new Vector2(60, 0);
            sb.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
            sb.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            return sb;
        }

        private void AddCollisionBox(Vector3 size, Vector3 position, StandardMaterial3D mat)
        {
            var mi = new MeshInstance3D();
            mi.Mesh = new BoxMesh { Size = size };
            mi.Position = position;
            mi.MaterialOverride = mat;
            _collisionOverlay.AddChild(mi);
        }

        /// <summary>
        /// Create a wedge/ramp mesh — flat at front (positive Z), rises to full height
        /// at back (negative Z). Centered at origin like Box/Cylinder/Sphere.
        /// </summary>
        private static ArrayMesh CreateRampMesh(float sizeX, float sizeY, float sizeZ)
        {
            float hx = sizeX / 2f, hy = sizeY / 2f, hz = sizeZ / 2f;

            Vector3 fbl = new(-hx, -hy,  hz);
            Vector3 fbr = new( hx, -hy,  hz);
            Vector3 bbl = new(-hx, -hy, -hz);
            Vector3 bbr = new( hx, -hy, -hz);
            Vector3 btl = new(-hx,  hy, -hz);
            Vector3 btr = new( hx,  hy, -hz);

            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);

            AddQuad(st, fbl, bbl, bbr, fbr, Vector3.Down);
            AddQuad(st, bbl, btl, btr, bbr, Vector3.Forward);
            var slopeNormal = new Vector3(0, sizeZ, sizeY).Normalized();
            AddQuad(st, fbr, btr, btl, fbl, slopeNormal);
            AddTri(st, fbl, btl, bbl, Vector3.Left);
            AddTri(st, fbr, bbr, btr, Vector3.Right);

            var mesh = new ArrayMesh();
            st.Commit(mesh);
            return mesh;
        }

        private static void AddQuad(SurfaceTool st, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            st.SetNormal(normal); st.AddVertex(a);
            st.SetNormal(normal); st.AddVertex(b);
            st.SetNormal(normal); st.AddVertex(c);
            st.SetNormal(normal); st.AddVertex(a);
            st.SetNormal(normal); st.AddVertex(c);
            st.SetNormal(normal); st.AddVertex(d);
        }

        private static void AddTri(SurfaceTool st, Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
        {
            st.SetNormal(normal); st.AddVertex(a);
            st.SetNormal(normal); st.AddVertex(b);
            st.SetNormal(normal); st.AddVertex(c);
        }

        // ===== LIFECYCLE =====

        protected override void Reload()
        {
            if (_assetList == null) return;

            _rawJson = LoadJson("res://Data/asset_config.json");
            if (_rawJson != null && _rawJson.TryGetValue("assets", out var assetsObj) &&
                assetsObj is Dictionary<string, object> assets)
            {
                _assetConfigs = assets;
            }
            else
            {
                _assetConfigs = new Dictionary<string, object>();
            }

            PopulateAssetList();
            MarkClean();
            SetStatus($"Loaded {_assetConfigs.Count} asset configs", EditorStyles.StatusSaved);
        }

        protected override void Save()
        {
            var data = new Dictionary<string, object>
            {
                ["assets"] = _assetConfigs ?? new Dictionary<string, object>()
            };

            if (SaveJson("res://Data/asset_config.json", data))
            {
                MarkClean();
                SetStatus($"Saved {(_assetConfigs?.Count ?? 0)} asset configs", EditorStyles.StatusSaved);
                GD.Print("[AssetEditor] Saved asset_config.json");
            }
            else
            {
                SetStatus("Save failed!", EditorStyles.StatusError);
            }
        }

        protected override void RestoreSnapshot(string jsonSnapshot)
        {
            var parsed = MiniJson.Deserialize(jsonSnapshot) as Dictionary<string, object>;
            if (parsed != null)
            {
                _assetConfigs = parsed;
                MarkDirty();
            }
        }
    }
}
