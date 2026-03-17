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
        private bool _autoRotate = false;
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
        private SpinBox _collOffsetX, _collOffsetY, _collOffsetZ;
        private Label _aabbLabel;
        private OptionButton _materialPicker;
        private string[] _materialNames;

        // State
        private string _selectedCategory = "prop";
        private string _selectedAssetId;
        private Dictionary<string, object> _assetConfigs;
        private Dictionary<string, object> _rawJson;

        // Catalog export
        private AssetCatalogExporter _catalogExporter;
        private Button _exportBtn;
        private Label _exportProgress;

        private static readonly string[] Categories =
            { "--- MODEL VIEWER ---",
              "humanoid_models", "quadruped_models", "drone_models",
              "creature_models", "mech_models", "turret_models",
              "weapon_models", "building_models",
              "--- ASSIGNED ---",
              "player", "enemy", "companion",
              "--- DUNGEON ---",
              "prop", "floor", "wall", "door", "detail", "hazard", "building",
              "pillar", "rock", "wood", "bone",
              "--- GEAR ---",
              "weapon", "item",
              "--- MECHS ---",
              "boss", "attachment" };

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
            for (int i = 0; i < Categories.Length; i++)
            {
                _categoryPicker.AddItem(Categories[i]);
                if (Categories[i].StartsWith("---"))
                    _categoryPicker.SetItemDisabled(i, true);
            }
            _categoryPicker.ItemSelected += idx =>
            {
                if (Categories[idx].StartsWith("---")) return;
                _selectedCategory = Categories[idx];
                PopulateAssetList();
            };
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

            // Export catalog
            leftPanel.AddChild(EditorStyles.MakeSeparator());
            _exportBtn = EditorStyles.MakeButton("Export Asset Catalog", EditorStyles.FontSmall, EditorStyles.AccentAsset);
            _exportBtn.CustomMinimumSize = new Vector2(0, 28);
            _exportBtn.TooltipText = "Render all assets to PNG and generate HTML gallery";
            _exportBtn.Pressed += OnExportCatalog;
            leftPanel.AddChild(_exportBtn);

            _exportProgress = EditorStyles.MakeLabel("", EditorStyles.FontTiny, EditorStyles.TextMuted);
            _exportProgress.AutowrapMode = TextServer.AutowrapMode.Word;
            leftPanel.AddChild(_exportProgress);

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
            _camera.Transform = _camera.Transform.LookingAt(Vector3.Zero, Vector3.Up);
            _viewport.AddChild(_camera);

            _previewRoot = new Node3D();
            _viewport.AddChild(_previewRoot);

            _collisionOverlay = new Node3D();
            _collisionOverlay.Name = "CollisionOverlay";
            _viewport.AddChild(_collisionOverlay);

            // Lighting
            var light = new DirectionalLight3D();
            light.RotationDegrees = new Vector3(-60, 45, 0);
            light.LightEnergy = 1.5f;
            _viewport.AddChild(light);

            var fill = new DirectionalLight3D();
            fill.RotationDegrees = new Vector3(-50, -60, 0);
            fill.LightEnergy = 0.6f;
            _viewport.AddChild(fill);

            var env = new WorldEnvironment();
            var envRes = new Godot.Environment();
            envRes.BackgroundMode = Godot.Environment.BGMode.Color;
            envRes.BackgroundColor = new Color(0.15f, 0.15f, 0.18f);
            envRes.AmbientLightSource = Godot.Environment.AmbientSource.Color;
            envRes.AmbientLightColor = new Color(0.45f, 0.45f, 0.5f);
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
            rotateCheck.ButtonPressed = _autoRotate;
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
            _collSizeX.ValueChanged += _ => OnConfigChanged();
            _collSizeY.ValueChanged += _ => OnConfigChanged();
            _collSizeZ.ValueChanged += _ => OnConfigChanged();
            sizeRow.AddChild(_collSizeX);
            sizeRow.AddChild(_collSizeY);
            sizeRow.AddChild(_collSizeZ);
            rightPanel.AddChild(sizeRow);

            rightPanel.AddChild(EditorStyles.MakeLabel("Radius / Thickness:", EditorStyles.FontSmall, EditorStyles.TextSecondary));
            _collRadius = MakeSpinBox(0.1, 25, 0.1, 0.5);
            _collRadius.ValueChanged += _ => OnConfigChanged();
            rightPanel.AddChild(_collRadius);

            rightPanel.AddChild(EditorStyles.MakeLabel("Offset (X/Y/Z):", EditorStyles.FontSmall, EditorStyles.TextSecondary));
            var offsetRow = new HBoxContainer();
            offsetRow.AddThemeConstantOverride("separation", 4);
            _collOffsetX = MakeSpinBox(-25, 25, 0.1, 0.0);
            _collOffsetY = MakeSpinBox(-25, 25, 0.1, 0.0);
            _collOffsetZ = MakeSpinBox(-25, 25, 0.1, 0.0);
            _collOffsetX.ValueChanged += _ => OnConfigChanged();
            _collOffsetY.ValueChanged += _ => OnConfigChanged();
            _collOffsetZ.ValueChanged += _ => OnConfigChanged();
            offsetRow.AddChild(_collOffsetX);
            offsetRow.AddChild(_collOffsetY);
            offsetRow.AddChild(_collOffsetZ);
            rightPanel.AddChild(offsetRow);

            rightPanel.AddChild(EditorStyles.MakeSeparator());

            // Material override
            rightPanel.AddChild(EditorStyles.MakeLabel("Material Override:", EditorStyles.FontSmall, EditorStyles.TextSecondary));
            _materialPicker = new OptionButton();
            _materialPicker.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
            _materialPicker.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            PopulateMaterialList();
            _materialPicker.ItemSelected += _ => ApplyMaterialOverride();
            rightPanel.AddChild(_materialPicker);

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
                _cameraHeight = Mathf.Clamp(_cameraHeight - delta.Y * 0.01f, 0.5f, 15f);
                _lastMousePos = mm.Position;
                _viewportContainer.AcceptEvent();
            }
        }

        // ===== ASSET LIST =====

        // Known procedural enemy IDs (all enemies with unique builds)
        private static readonly string[] ProceduralEnemyIds =
        {
            "calibration_target", "scrap_rat", "decoy_unit", "wire_worm",
            "corrupted_sentry", "scrap_hydra", "rust_titan", "null_warden",
            "rust_mite", "volt_sprinter", "shard_lobber", "scrap_golem",
            "glitch_phantom", "overclock_drone", "axis_disciple", "axis_avatar"
        };

        private void PopulateAssetList()
        {
            if (_assetList == null) return;

            foreach (var child in _assetList.GetChildren())
                if (child is Node n) n.QueueFree();

            string[] ids;

            if (_selectedCategory == "player")
            {
                var frames = Enum.GetNames(typeof(BotFrameType));
                ids = new string[frames.Length];
                for (int i = 0; i < frames.Length; i++)
                    ids[i] = frames[i].ToLower();
            }
            else if (_selectedCategory == "enemy")
            {
                ids = (string[])ProceduralEnemyIds.Clone();
            }
            else if (_selectedCategory.EndsWith("_models"))
            {
                // MODEL VIEWER — real FBX/GLB files grouped by type
                ModelLibrary.Initialize();
                var list = new List<string>();
                var seenPaths = new HashSet<string>();

                // Define which models belong to each type group
                // Humanoid: player bots + PolygonDungeon characters + quaternius_robot + gun_robot
                var humanoidIds = new HashSet<string> {
                    "stan", "george", "leela", "mike",
                    "clunker", "tincan", "noisebox", "rustbucket", "scrapheap", "sparkplug",
                    "gun_robot",
                    // Enemy FBX that are humanoid bipedal robots
                    "calibration_target", "scrap_rat", "wire_worm", "patch_bot",
                    "overclock_drone", "shard_lobber", "volt_sprinter", "junk_lurker",
                    "axis_disciple", "corrupted_sentry", "rust_titan"
                };
                var humanoidPrefixes = System.Array.Empty<string>(); // no prefix matching needed

                // Quadruped: spider bots only
                var quadrupedIds = new HashSet<string> {
                    "spider_bot"
                };

                // Drone/Flying: small flying units
                var droneIds = new HashSet<string> {
                    "eye_drone", "spark_drone", "bit"
                };

                // Creature: organic/alien shaped robots
                var creatureIds = new HashSet<string> {
                    "trilobite", "quad_shell", "decoy_unit"
                };

                // Mech/Vehicle: full mech vehicles
                var mechIds = new HashSet<string> {
                    "sm_veh_mech_01", "sk_polygonmech_main_full", "characters"
                };
                var mechPrefixes = new[] { "sm_veh_", "sk_polygon", "sk_iso" };

                // Turret: static weapon platforms
                var turretPrefixes = new[] { "kb3d_ftw_propturret", "kb3d_ftw_propweapon",
                    "kb3d_ftw_heroprop", "kb3d_ftw_proprobotobserver", "kb3d_ftw_propsurveillance" };

                // Weapon: all weapon models (no prefix filter — show everything in weapon category)
                var weaponPrefixes = System.Array.Empty<string>(); // match all

                // Building: full structures
                var buildingPrefixes = new[] { "kb3d_ftw_bldg" };

                // Determine which filter to use
                string modelType = _selectedCategory.Replace("_models", "");

                // Scan relevant categories
                string[] scanCats = modelType switch
                {
                    "humanoid" => new[] { "player", "enemy", "boss" },
                    "quadruped" => new[] { "enemy", "boss" },
                    "drone" => new[] { "enemy", "companion" },
                    "creature" => new[] { "enemy" },
                    "mech" => new[] { "boss" },
                    "turret" => new[] { "hazard" },
                    "weapon" => new[] { "weapon" },
                    "building" => new[] { "building" },
                    _ => new[] { "enemy", "player", "companion", "boss", "hazard", "weapon", "building" }
                };

                foreach (var cat in scanCats)
                {
                    foreach (var id in ModelLibrary.GetCategoryIds(cat))
                    {
                        var resPath = ModelLibrary.GetResourcePath(cat, id);
                        if (resPath == null) continue;
                        if (!seenPaths.Add(resPath)) continue;

                        var lower = id.ToLower();
                        bool match = modelType switch
                        {
                            "humanoid" => humanoidIds.Contains(lower)
                                || System.Array.Exists(humanoidPrefixes, p => lower.StartsWith(p)),
                            "quadruped" => quadrupedIds.Contains(lower),
                            "drone" => droneIds.Contains(lower),
                            "creature" => creatureIds.Contains(lower),
                            "mech" => mechIds.Contains(lower)
                                || System.Array.Exists(mechPrefixes, p => lower.StartsWith(p)),
                            "turret" => System.Array.Exists(turretPrefixes, p => lower.StartsWith(p)),
                            "weapon" => true, // show all weapons
                            "building" => System.Array.Exists(buildingPrefixes, p => lower.StartsWith(p)),
                            _ => true
                        };

                        if (!match) continue;

                        var shortPath = resPath.Replace("res://Models/", "")
                                               .Replace("res://Assets/", "Assets/");
                        list.Add($"{shortPath}  [{cat}]");
                    }
                }
                ids = list.ToArray();
                ids = list.ToArray();
            }
            else
            {
                ModelLibrary.Initialize();
                ids = ModelLibrary.GetCategoryIds(_selectedCategory);

                // Bug #15: Filter boss category to only show full models (SK_*, SM_Veh_*),
                // not individual mech attachment parts (SM_Mech_Arm_*, SM_Chr_Attach_*, etc.)
                if (_selectedCategory == "boss")
                {
                    var filtered = new List<string>();
                    foreach (var id in ids)
                    {
                        var lower = id.ToLower();
                        if (lower.StartsWith("sk_") || lower.StartsWith("sm_veh_") ||
                            lower == "characters" || lower == "axis_avatar" ||
                            lower.StartsWith("a_iso_"))
                        {
                            filtered.Add(id);
                        }
                    }
                    ids = filtered.ToArray();
                }
            }

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

            Node3D model = null;

            // Parse display formats:
            //   "[enemy] spider_bot"                        → category=enemy, id=spider_bot
            //   "Characters/Enemies/spider_bot.fbx  [enemy]" → category=enemy, id=spider_bot
            string resolvedCategory = _selectedCategory;
            string resolvedId = _selectedAssetId;

            if (_selectedAssetId.Contains("  [") && !_selectedAssetId.StartsWith("["))
            {
                // all_models format: "path/file.ext  [category]"
                int bracketStart = _selectedAssetId.LastIndexOf("  [");
                var pathPart = _selectedAssetId.Substring(0, bracketStart).Trim();
                resolvedCategory = _selectedAssetId.Substring(bracketStart + 3).TrimEnd(']');
                resolvedId = System.IO.Path.GetFileNameWithoutExtension(pathPart);
            }
            else if (_selectedAssetId.StartsWith("["))
            {
                // Bracketed prefix format: "[enemy] id" or "[enemy] id  (alias: x)"
                int closeBracket = _selectedAssetId.IndexOf(']');
                if (closeBracket > 1)
                {
                    resolvedCategory = _selectedAssetId.Substring(1, closeBracket - 1);
                    resolvedId = _selectedAssetId.Substring(closeBracket + 2).Trim();
                    int aliasParen = resolvedId.IndexOf("  (alias:");
                    if (aliasParen >= 0)
                        resolvedId = resolvedId.Substring(0, aliasParen).Trim();
                }
            }

            if (resolvedCategory == "player")
            {
                if (Enum.TryParse<BotFrameType>(resolvedId, true, out var frame))
                {
                    model = CharacterMeshBuilder.BuildPlayerBody(frame);
                }
                else
                {
                    // Bug #2: Raw player model IDs (stan, george, leela, mike) aren't
                    // BotFrameType values — load them directly and apply textures so
                    // they don't appear blank in the humanoid_models viewer.
                    model = ModelLibrary.TryLoad("player", resolvedId);
                    if (model != null)
                        CharacterMeshBuilder.ApplyRawPlayerModelTextures(model, resolvedId);
                }
            }
            else if (resolvedCategory == "enemy")
            {
                model = CharacterMeshBuilder.BuildEnemyBody(resolvedId);
            }
            else if (resolvedCategory == "companion")
            {
                model = CharacterMeshBuilder.BuildCompanionBody(resolvedId);
            }
            else
            {
                // Bug #5/#6/#7/#8: GLB/FBX models loaded via TryLoad may have no materials.
                // Apply fallback materials for categories with commonly untextured models.
                model = ModelLibrary.TryLoad(resolvedCategory, resolvedId);
                if (model != null)
                    CharacterMeshBuilder.ApplyFallbackMaterialIfNeeded(model, resolvedCategory);
            }

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

            // Auto-fit camera to model size — no upper limit so large buildings/turrets are visible
            float maxDim = Mathf.Max(aabb.Size.X, Mathf.Max(aabb.Size.Y, aabb.Size.Z));
            if (maxDim > 0.01f)
            {
                _cameraRadius = Mathf.Max(2f, maxDim * 2.0f);
                _cameraHeight = Mathf.Max(1f, maxDim * 0.8f);
            }
            // Center model at origin so camera orbits around it.
            // Bug #11: For models that clip below the ground plane (e.g. sk_iso_mech),
            // lift the model so its bottom sits at Y=0 instead of centering at origin.
            var center = aabb.GetCenter();
            if (center.Length() > 0.5f)
            {
                var offset = -center * model.Scale;
                // Ensure model bottom is at or above Y=0 (ground plane)
                float bottomY = (aabb.Position.Y) * model.Scale.Y + offset.Y;
                if (bottomY < -0.1f)
                    offset.Y -= bottomY;
                model.Position = offset;
            }

            // Apply material override if selected
            ApplyMaterialOverride();

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
            float offsetX = (float)_collOffsetX.Value;
            float offsetY = (float)_collOffsetY.Value;
            float offsetZ = (float)_collOffsetZ.Value;

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
                    new Vector3(-halfW + thickness / 2f + offsetX, pillarH / 2f + offsetY, offsetZ), collMat);
                AddCollisionBox(new Vector3(thickness, pillarH, sizeZ),
                    new Vector3(halfW - thickness / 2f + offsetX, pillarH / 2f + offsetY, offsetZ), collMat);
                AddCollisionBox(new Vector3(sizeX, thickness, sizeZ),
                    new Vector3(offsetX, sizeY - thickness / 2f + offsetY, offsetZ), collMat);

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
            mi.Position = new Vector3(offsetX, sizeY / 2f + offsetY, offsetZ);
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
            _collOffsetX.Value = 0.0;
            _collOffsetY.Value = 0.0;
            _collOffsetZ.Value = 0.0;
            if (_materialPicker != null) _materialPicker.Selected = 0;

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
                if (cfg.TryGetValue("collOffsetX", out var cox)) _collOffsetX.Value = Convert.ToDouble(cox);
                if (cfg.TryGetValue("collOffsetY", out var co)) _collOffsetY.Value = Convert.ToDouble(co);
                if (cfg.TryGetValue("collOffsetZ", out var coz)) _collOffsetZ.Value = Convert.ToDouble(coz);

                // Material override
                if (cfg.TryGetValue("material", out var matName) && matName is string matStr
                    && !string.IsNullOrEmpty(matStr) && _materialNames != null)
                {
                    int matIdx = Array.IndexOf(_materialNames, matStr);
                    if (matIdx >= 0 && _materialPicker != null)
                        _materialPicker.Selected = matIdx + 1; // +1 for "(None)" at index 0
                }

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
                ["collOffsetX"] = _collOffsetX.Value,
                ["collOffsetY"] = _collOffsetY.Value,
                ["collOffsetZ"] = _collOffsetZ.Value,
                ["material"] = _materialPicker != null && _materialPicker.Selected > 0 && _materialNames != null
                    ? _materialNames[_materialPicker.Selected - 1] : "",
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
            _collOffsetX.Value = 0.0;
            _collOffsetY.Value = 0.0;
            _collOffsetZ.Value = 0.0;
            if (_materialPicker != null) _materialPicker.Selected = 0;

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

        // ===== MATERIAL PICKER =====

        private void PopulateMaterialList()
        {
            _materialPicker.Clear();
            _materialPicker.AddItem("(None)");

            var names = new List<string>();
            using var dir = DirAccess.Open("res://Assets/Materials/Generated/");
            if (dir != null)
            {
                dir.ListDirBegin();
                string file;
                while ((file = dir.GetNext()) != "")
                {
                    if (file.EndsWith(".tres"))
                        names.Add(file.Replace(".tres", ""));
                }
                dir.ListDirEnd();
            }
            names.Sort();
            _materialNames = names.ToArray();

            foreach (var name in _materialNames)
                _materialPicker.AddItem(name);
        }

        private void ApplyMaterialOverride()
        {
            if (_previewRoot == null || _materialPicker == null) return;

            StandardMaterial3D mat = null;
            if (_materialPicker.Selected > 0 && _materialNames != null && _materialPicker.Selected - 1 < _materialNames.Length)
            {
                string matName = _materialNames[_materialPicker.Selected - 1];
                string path = $"res://Assets/Materials/Generated/{matName}.tres";
                if (ResourceLoader.Exists(path))
                    mat = GD.Load<StandardMaterial3D>(path);
            }

            // Apply (or clear) material override on all MeshInstance3D descendants
            ApplyMaterialToDescendants(_previewRoot, mat);
        }

        private static void ApplyMaterialToDescendants(Node node, StandardMaterial3D mat)
        {
            if (node is MeshInstance3D mi)
                mi.MaterialOverride = mat;
            foreach (var child in node.GetChildren())
                if (child is Node n) ApplyMaterialToDescendants(n, mat);
        }

        // ===== CATALOG EXPORT =====

        private void OnExportCatalog()
        {
            if (_catalogExporter != null && _catalogExporter.IsInsideTree())
            {
                SetStatus("Export already in progress", EditorStyles.StatusError);
                return;
            }

            _catalogExporter = new AssetCatalogExporter();
            _catalogExporter.OnProgress += msg =>
            {
                if (_exportProgress != null) _exportProgress.Text = msg;
                SetStatus(msg, EditorStyles.TextAccent);
            };
            _catalogExporter.OnComplete += () =>
            {
                _exportBtn.Disabled = false;
                _exportBtn.Text = "Export Asset Catalog";

                string path = ProjectSettings.GlobalizePath("user://asset_catalog/index.html");
                SetStatus($"Catalog saved to {path}", EditorStyles.StatusSaved);
                if (_exportProgress != null)
                    _exportProgress.Text = $"Open: {path}";

                // Clean up exporter after a moment
                GetTree().CreateTimer(1.0).Timeout += () =>
                {
                    if (_catalogExporter != null && _catalogExporter.IsInsideTree())
                    {
                        RemoveChild(_catalogExporter);
                        _catalogExporter.QueueFree();
                        _catalogExporter = null;
                    }
                };
            };

            AddChild(_catalogExporter);
            _catalogExporter.StartExport();
            _exportBtn.Disabled = true;
            _exportBtn.Text = "Exporting...";
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
            PushInitialState(MiniJsonWriter.Serialize(_assetConfigs));
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

        // ═══════════════════════════════════════════════════════════════
        //  TEST API
        // ═══════════════════════════════════════════════════════════════

        private int _testCategoryIndex;
        private int _testAssetIndex;

        public override SubViewport TestGetViewport() => _viewport;
        public override void TestSetAutoRotate(bool enabled) => _autoRotate = enabled;

        public override void TestCycleNext(string property)
        {
            switch (property)
            {
                case "category":
                    _testCategoryIndex = (_testCategoryIndex + 1) % Categories.Length;
                    _selectedCategory = Categories[_testCategoryIndex];
                    if (_categoryPicker != null)
                        _categoryPicker.Selected = _testCategoryIndex;
                    PopulateAssetList();
                    _testAssetIndex = 0;
                    // Reset camera to default orbit so the asset is visible
                    _cameraRadius = 8f;
                    _cameraHeight = 4f;
                    _cameraAngle = 0.5f;
                    _camera.Position = new Vector3(
                        Mathf.Cos(_cameraAngle) * _cameraRadius,
                        _cameraHeight,
                        Mathf.Sin(_cameraAngle) * _cameraRadius);
                    _camera.LookAt(new Vector3(0, _cameraHeight * 0.3f, 0));
                    break;
                case "asset":
                    if (_assetList == null) break;
                    int childCount = _assetList.GetChildCount();
                    if (childCount > 0)
                    {
                        _testAssetIndex = (_testAssetIndex + 1) % childCount;
                        var btn = _assetList.GetChild(_testAssetIndex) as Button;
                        if (btn != null)
                        {
                            // The button text is the asset ID
                            SelectAsset(btn.Text);
                        }
                        // Reset camera to default orbit so the asset is visible
                        _cameraRadius = 5f;
                        _cameraHeight = 3f;
                        _cameraAngle = 0f;
                    }
                    break;
                case "collision":
                    _showCollision = !_showCollision;
                    PreviewAsset();
                    break;
            }
        }
    }
}
