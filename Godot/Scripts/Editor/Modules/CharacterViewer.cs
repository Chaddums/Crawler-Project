using Godot;
using System;
using System.Collections.Generic;

namespace JunkbotArena.Editor
{
    /// <summary>
    /// Character Viewer — 3D model viewer with body part editing.
    /// Select a bot frame to see its procedural mesh. Click parts in the scene tree
    /// to select them, then adjust position/rotation with spinboxes.
    /// Also supports editing WeaponMount position and muzzle fire point.
    /// Saves overrides to Data/character_config.json.
    /// </summary>
    public partial class CharacterViewer : EditorPanel
    {
        public override string PanelName => "Characters";
        public override Color AccentColor => EditorStyles.AccentCharacter;

        private const string CONFIG_PATH = "res://Data/character_config.json";

        // 3D viewport
        private SubViewport _viewport;
        private SubViewportContainer _viewportContainer;
        private Node3D _modelRoot;
        private Camera3D _camera;
        private float _cameraAngle;
        private float _cameraRadius = 4f;
        private float _cameraHeight = 2f;
        private bool _autoRotate = true;

        // Selectors
        private Label _infoLabel;
        private Label _weaponLabelRef;

        // Part tree + inspector
        private VBoxContainer _partListContainer;
        private ScrollContainer _partScroll;
        private VBoxContainer _inspectorContainer;
        private Label _selectedPartLabel;
        private SpinBox _posX, _posY, _posZ;
        private SpinBox _rotX, _rotY, _rotZ;
        private Label _statsLabel;

        // Fire point editor
        private SpinBox _fireY, _fireForward;
        private MeshInstance3D _firePointMarker;

        // Selection
        private Node3D _selectedPart;
        private MeshInstance3D _selectionHighlight;
        private string _selectedPartName;

        // Drag state
        private bool _isDragging;
        private Vector2 _lastMousePos;

        // State
        private BotFrameType _currentFrame = BotFrameType.TinCan;
        private WeaponType _currentWeapon = WeaponType.None;
        private int _frameIndex;
        private int _weaponIndex;
        private bool _suppressSpinEvents;

        // Persisted config
        private Dictionary<string, object> _config;

        private static readonly BotFrameType[] AllFrames =
        {
            BotFrameType.TinCan, BotFrameType.Scrapheap, BotFrameType.SparkPlug,
            BotFrameType.RustBucket, BotFrameType.NoiseBox, BotFrameType.Clunker
        };

        private static readonly WeaponType[] AllWeapons =
        {
            WeaponType.None, WeaponType.Pistol, WeaponType.Rifle, WeaponType.Shotgun,
            WeaponType.Launcher, WeaponType.Repeater, WeaponType.BladeRing,
            WeaponType.FlailChain, WeaponType.ShockCoil, WeaponType.FlameThrower
        };

        // Part names that are editable pivots (not decorative mesh children)
        private static readonly HashSet<string> EditableParts = new()
        {
            "Head", "Torso", "LeftArm", "RightArm", "LeftLeg", "RightLeg",
            "LeftElbow", "RightElbow", "LeftHand", "RightHand",
            "LeftKnee", "RightKnee", "LeftAnkle", "RightAnkle",
            "Weapon", "WeaponMount", "Body", "Crossbar", "Tail"
        };

        protected override void BuildUI(VBoxContainer content)
        {
            var split = new HBoxContainer();
            split.SizeFlagsVertical = SizeFlags.ExpandFill;
            split.AddThemeConstantOverride("separation", 8);

            // ═══ LEFT PANEL: selectors + part tree ═══
            var leftPanel = new VBoxContainer();
            leftPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            leftPanel.CustomMinimumSize = new Vector2(260, 0);

            // Bot Frame selector
            leftPanel.AddChild(EditorStyles.MakeLabel("Bot Frame", EditorStyles.FontHeader, AccentColor));
            leftPanel.AddChild(BuildCycler(
                () => _currentFrame.ToString(),
                dir => CycleFrame(dir),
                out _infoLabel));

            leftPanel.AddChild(EditorStyles.MakeSeparator());

            // Weapon selector
            leftPanel.AddChild(EditorStyles.MakeLabel("Weapon", EditorStyles.FontHeader, AccentColor));
            leftPanel.AddChild(BuildCycler(
                () => _currentWeapon.ToString(),
                dir => CycleWeapon(dir),
                out _weaponLabelRef));

            leftPanel.AddChild(EditorStyles.MakeSeparator());

            // Auto-rotate toggle
            var rotateCheck = new CheckBox();
            rotateCheck.Text = "Auto-Rotate";
            rotateCheck.ButtonPressed = true;
            rotateCheck.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
            rotateCheck.Toggled += v => _autoRotate = v;
            leftPanel.AddChild(rotateCheck);

            leftPanel.AddChild(EditorStyles.MakeSeparator());

            // Part tree header
            leftPanel.AddChild(EditorStyles.MakeLabel("Body Parts", EditorStyles.FontHeader, EditorStyles.TextSecondary));

            _partScroll = new ScrollContainer();
            _partScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            _partListContainer = new VBoxContainer();
            _partListContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _partScroll.AddChild(_partListContainer);
            leftPanel.AddChild(_partScroll);

            split.AddChild(leftPanel);

            // ═══ CENTER: 3D viewport ═══
            var centerPanel = new VBoxContainer();
            centerPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            centerPanel.SizeFlagsVertical = SizeFlags.ExpandFill;

            _viewportContainer = new SubViewportContainer();
            _viewportContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
            _viewportContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _viewportContainer.Stretch = true;
            _viewportContainer.GuiInput += OnViewportInput;

            _viewport = new SubViewport();
            _viewport.Size = new Vector2I(800, 600);
            _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
            _viewport.OwnWorld3D = true;

            _camera = new Camera3D();
            _viewport.AddChild(_camera);
            UpdateCameraOrbit();

            _modelRoot = new Node3D();
            _viewport.AddChild(_modelRoot);

            // Ground
            var ground = new MeshInstance3D();
            var planeMesh = new PlaneMesh { Size = new Vector2(8, 8) };
            ground.Mesh = planeMesh;
            var groundMat = new StandardMaterial3D { AlbedoColor = new Color(0.25f, 0.25f, 0.28f) };
            ground.MaterialOverride = groundMat;
            _viewport.AddChild(ground);

            // Lighting — add to tree BEFORE LookAt (requires valid transform)
            var light = new DirectionalLight3D();
            light.Position = new Vector3(3, 6, 3);
            light.LightEnergy = 2.5f;
            _viewport.AddChild(light);
            light.LookAt(Vector3.Zero);

            var fill = new DirectionalLight3D();
            fill.Position = new Vector3(-3, 4, -2);
            fill.LightEnergy = 1.2f;
            _viewport.AddChild(fill);
            fill.LookAt(Vector3.Zero);

            var env = new WorldEnvironment();
            var envRes = new Godot.Environment();
            envRes.BackgroundMode = Godot.Environment.BGMode.Color;
            envRes.BackgroundColor = new Color(0.18f, 0.18f, 0.22f);
            envRes.AmbientLightSource = Godot.Environment.AmbientSource.Color;
            envRes.AmbientLightColor = new Color(0.45f, 0.45f, 0.5f);
            env.Environment = envRes;
            _viewport.AddChild(env);

            _viewportContainer.AddChild(_viewport);
            centerPanel.AddChild(_viewportContainer);
            split.AddChild(centerPanel);

            // ═══ RIGHT PANEL: inspector + fire point ═══
            var rightPanel = new VBoxContainer();
            rightPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            rightPanel.CustomMinimumSize = new Vector2(260, 0);

            // Selected part inspector
            rightPanel.AddChild(EditorStyles.MakeLabel("Transform", EditorStyles.FontHeader, AccentColor));
            _selectedPartLabel = EditorStyles.MakeLabel("(none selected)", EditorStyles.FontBody, EditorStyles.TextSecondary);
            rightPanel.AddChild(_selectedPartLabel);

            _inspectorContainer = new VBoxContainer();
            _inspectorContainer.AddThemeConstantOverride("separation", 2);

            // Position
            _inspectorContainer.AddChild(EditorStyles.MakeLabel("Position", EditorStyles.FontSmall, EditorStyles.TextMuted));
            var posRow = new HBoxContainer();
            posRow.AddThemeConstantOverride("separation", 4);
            _posX = MakeSpinBox("X", -5, 5, 0.01f);
            _posY = MakeSpinBox("Y", -5, 5, 0.01f);
            _posZ = MakeSpinBox("Z", -5, 5, 0.01f);
            posRow.AddChild(MakeLabeledSpin("X", _posX));
            posRow.AddChild(MakeLabeledSpin("Y", _posY));
            posRow.AddChild(MakeLabeledSpin("Z", _posZ));
            _inspectorContainer.AddChild(posRow);

            // Rotation
            _inspectorContainer.AddChild(EditorStyles.MakeLabel("Rotation", EditorStyles.FontSmall, EditorStyles.TextMuted));
            var rotRow = new HBoxContainer();
            rotRow.AddThemeConstantOverride("separation", 4);
            _rotX = MakeSpinBox("RX", -180, 180, 1f);
            _rotY = MakeSpinBox("RY", -180, 180, 1f);
            _rotZ = MakeSpinBox("RZ", -180, 180, 1f);
            rotRow.AddChild(MakeLabeledSpin("X", _rotX));
            rotRow.AddChild(MakeLabeledSpin("Y", _rotY));
            rotRow.AddChild(MakeLabeledSpin("Z", _rotZ));
            _inspectorContainer.AddChild(rotRow);

            // Quick actions
            var actionsRow = new HBoxContainer();
            actionsRow.AddThemeConstantOverride("separation", 4);
            var resetBtn = EditorStyles.MakeButton("Reset", EditorStyles.FontSmall);
            resetBtn.Pressed += ResetSelectedPart;
            actionsRow.AddChild(resetBtn);

            var nudgeUp = EditorStyles.MakeButton("Y+", EditorStyles.FontSmall);
            nudgeUp.Pressed += () => NudgeSelected(Vector3.Up * 0.02f);
            actionsRow.AddChild(nudgeUp);

            var nudgeDown = EditorStyles.MakeButton("Y-", EditorStyles.FontSmall);
            nudgeDown.Pressed += () => NudgeSelected(Vector3.Down * 0.02f);
            actionsRow.AddChild(nudgeDown);

            var nudgeFwd = EditorStyles.MakeButton("Z-", EditorStyles.FontSmall);
            nudgeFwd.Pressed += () => NudgeSelected(new Vector3(0, 0, -0.02f));
            actionsRow.AddChild(nudgeFwd);

            var nudgeBack = EditorStyles.MakeButton("Z+", EditorStyles.FontSmall);
            nudgeBack.Pressed += () => NudgeSelected(new Vector3(0, 0, 0.02f));
            actionsRow.AddChild(nudgeBack);
            _inspectorContainer.AddChild(actionsRow);

            rightPanel.AddChild(_inspectorContainer);

            rightPanel.AddChild(EditorStyles.MakeSeparator());

            // ── Fire Point Editor ──
            rightPanel.AddChild(EditorStyles.MakeLabel("Fire Point", EditorStyles.FontHeader, new Color(1f, 0.6f, 0.3f)));
            rightPanel.AddChild(EditorStyles.MakeLabel("Muzzle flash / projectile spawn offset", EditorStyles.FontTiny, EditorStyles.TextMuted));

            var fireRow = new HBoxContainer();
            fireRow.AddThemeConstantOverride("separation", 4);
            _fireY = MakeSpinBox("FY", 0, 3, 0.05f);
            _fireY.Value = 0.9;
            _fireForward = MakeSpinBox("FF", 0, 3, 0.05f);
            _fireForward.Value = 0.8;
            fireRow.AddChild(MakeLabeledSpin("Height", _fireY));
            fireRow.AddChild(MakeLabeledSpin("Forward", _fireForward));
            rightPanel.AddChild(fireRow);

            var showFireBtn = EditorStyles.MakeButton("Show Fire Point", EditorStyles.FontSmall, new Color(1f, 0.6f, 0.3f));
            showFireBtn.Pressed += ToggleFirePointMarker;
            rightPanel.AddChild(showFireBtn);

            rightPanel.AddChild(EditorStyles.MakeSeparator());

            // Stats display
            rightPanel.AddChild(EditorStyles.MakeLabel("Base Stats", EditorStyles.FontHeader, EditorStyles.TextSecondary));
            _statsLabel = EditorStyles.MakeLabel("", EditorStyles.FontSmall, EditorStyles.TextMuted);
            rightPanel.AddChild(_statsLabel);

            split.AddChild(rightPanel);
            content.AddChild(split);

            // Wire up spin events
            _posX.ValueChanged += _ => OnSpinChanged();
            _posY.ValueChanged += _ => OnSpinChanged();
            _posZ.ValueChanged += _ => OnSpinChanged();
            _rotX.ValueChanged += _ => OnSpinChanged();
            _rotY.ValueChanged += _ => OnSpinChanged();
            _rotZ.ValueChanged += _ => OnSpinChanged();
            _fireY.ValueChanged += _ => { UpdateFirePointMarker(); OnFirePointChanged(); };
            _fireForward.ValueChanged += _ => { UpdateFirePointMarker(); OnFirePointChanged(); };
        }

        public override void _Ready()
        {
            base._Ready();
            CallDeferred(nameof(LoadModel));
        }

        public override void _Process(double delta)
        {
            if (!Visible) return;
            if (_autoRotate && _modelRoot != null)
            {
                _cameraAngle += (float)delta * 0.8f;
                UpdateCameraOrbit();
            }
        }

        private void OnViewportInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mb)
            {
                if (mb.ButtonIndex == MouseButton.WheelUp)
                {
                    _cameraRadius = Mathf.Max(2f, _cameraRadius - 0.5f);
                    UpdateCameraOrbit();
                    _viewportContainer.AcceptEvent();
                }
                else if (mb.ButtonIndex == MouseButton.WheelDown)
                {
                    _cameraRadius = Mathf.Min(10f, _cameraRadius + 0.5f);
                    UpdateCameraOrbit();
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
                _cameraHeight = Mathf.Clamp(_cameraHeight - delta.Y * 0.01f, 0.5f, 6f);
                _lastMousePos = mm.Position;
                UpdateCameraOrbit();
                _viewportContainer.AcceptEvent();
            }
        }

        // ── Cycler UI builder ──

        private HBoxContainer BuildCycler(Func<string> getText, Action<int> cycle, out Label label)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 4);

            var prev = EditorStyles.MakeButton("<", EditorStyles.FontBody);
            prev.CustomMinimumSize = new Vector2(32, 28);
            prev.Pressed += () => cycle(-1);
            row.AddChild(prev);

            label = EditorStyles.MakeLabel(getText(), EditorStyles.FontBody, AccentColor);
            label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            label.HorizontalAlignment = HorizontalAlignment.Center;
            row.AddChild(label);

            var next = EditorStyles.MakeButton(">", EditorStyles.FontBody);
            next.CustomMinimumSize = new Vector2(32, 28);
            next.Pressed += () => cycle(1);
            row.AddChild(next);

            return row;
        }

        private void CycleFrame(int dir)
        {
            _frameIndex = (_frameIndex + dir + AllFrames.Length) % AllFrames.Length;
            _currentFrame = AllFrames[_frameIndex];
            _infoLabel.Text = _currentFrame.ToString();
            LoadModel();
        }

        private void CycleWeapon(int dir)
        {
            _weaponIndex = (_weaponIndex + dir + AllWeapons.Length) % AllWeapons.Length;
            _currentWeapon = AllWeapons[_weaponIndex];
            _weaponLabelRef.Text = _currentWeapon.ToString();
            LoadModel();
        }

        // ── Camera ──

        private void UpdateCameraOrbit()
        {
            if (_camera == null) return;
            _camera.Position = new Vector3(
                Mathf.Sin(_cameraAngle) * _cameraRadius,
                _cameraHeight,
                Mathf.Cos(_cameraAngle) * _cameraRadius);
            _camera.LookAt(new Vector3(0, 1, 0), Vector3.Up);
        }

        // ── Model Loading ──

        private void LoadModel()
        {
            if (_modelRoot == null) return;

            // Clear existing
            foreach (var child in _modelRoot.GetChildren())
            {
                if (child is Node n) n.QueueFree();
            }
            _selectedPart = null;
            _selectionHighlight = null;
            _firePointMarker = null;

            try
            {
                var body = CharacterMeshBuilder.BuildPlayerBody(_currentFrame);
                if (body != null)
                {
                    _modelRoot.AddChild(body);
                    ApplyOverrides(body);
                }

                if (_currentWeapon != WeaponType.None)
                {
                    // AoE / melee weapons use dedicated builders
                    Node3D weaponModel = _currentWeapon switch
                    {
                        WeaponType.BladeRing => CharacterMeshBuilder.BuildBladeRing(),
                        WeaponType.FlailChain => CharacterMeshBuilder.BuildFlailChain(),
                        WeaponType.ShockCoil => CharacterMeshBuilder.BuildShockCoil(),
                        WeaponType.FlameThrower => CharacterMeshBuilder.BuildFlameThrower(),
                        _ => CharacterMeshBuilder.BuildWeapon(_currentFrame) // ranged
                    };

                    if (weaponModel != null)
                    {
                        bool isAoE = _currentWeapon is WeaponType.BladeRing or WeaponType.FlailChain
                            or WeaponType.ShockCoil or WeaponType.FlameThrower;
                        weaponModel.Position = isAoE
                            ? new Vector3(0, 1, 0)
                            : new Vector3(0.6f, 1.0f, 0);
                        _modelRoot.AddChild(weaponModel);
                    }
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"[CharacterViewer] Error loading model: {e.Message}");
            }

            BuildPartTree();
            UpdateStats();
            LoadFirePoint();
        }

        // ── Part Tree ──

        private void BuildPartTree()
        {
            // Clear existing buttons
            foreach (var child in _partListContainer.GetChildren())
            {
                if (child is Node n) n.QueueFree();
            }

            // Walk model root and list editable parts
            foreach (var child in _modelRoot.GetChildren())
            {
                if (child is Node3D body)
                    AddPartButtons(body, 0);
            }
        }

        private void AddPartButtons(Node3D node, int depth)
        {
            string name = node.Name.ToString();
            bool isEditable = EditableParts.Contains(name);

            if (isEditable || depth == 0)
            {
                var btn = new Button();
                string indent = new string(' ', depth * 2);
                string icon = isEditable ? ">" : "-";
                btn.Text = $"{indent}{icon} {name}";
                btn.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
                btn.Alignment = HorizontalAlignment.Left;
                btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;

                if (isEditable)
                {
                    var capturedNode = node;
                    btn.Pressed += () => SelectPart(capturedNode);
                }
                else
                {
                    btn.Disabled = true;
                    btn.AddThemeColorOverride("font_disabled_color", EditorStyles.TextMuted);
                }

                _partListContainer.AddChild(btn);
            }

            foreach (var child in node.GetChildren())
            {
                if (child is Node3D child3D)
                {
                    string childName = child3D.Name.ToString();
                    // Show editable children and structural pivots (skip decorative _ prefixed)
                    if (EditableParts.Contains(childName) || !childName.StartsWith("_"))
                        AddPartButtons(child3D, depth + 1);
                }
            }
        }

        // ── Selection ──

        private void SelectPart(Node3D part)
        {
            _selectedPart = part;
            _selectedPartName = part.Name.ToString();
            _selectedPartLabel.Text = _selectedPartName;

            // Update spinboxes
            _suppressSpinEvents = true;
            _posX.Value = part.Position.X;
            _posY.Value = part.Position.Y;
            _posZ.Value = part.Position.Z;
            _rotX.Value = part.RotationDegrees.X;
            _rotY.Value = part.RotationDegrees.Y;
            _rotZ.Value = part.RotationDegrees.Z;
            _suppressSpinEvents = false;

            // Highlight selection
            UpdateSelectionHighlight();
        }

        private void UpdateSelectionHighlight()
        {
            // Remove old highlight
            if (_selectionHighlight != null && GodotObject.IsInstanceValid(_selectionHighlight))
                _selectionHighlight.QueueFree();
            _selectionHighlight = null;

            if (_selectedPart == null || !GodotObject.IsInstanceValid(_selectedPart)) return;

            // Create a small wireframe sphere at the pivot point
            var highlight = new MeshInstance3D();
            var sphere = new SphereMesh();
            sphere.Radius = 0.05f;
            sphere.Height = 0.1f;
            sphere.RadialSegments = 8;
            sphere.Rings = 4;
            highlight.Mesh = sphere;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.3f, 1f, 0.4f, 0.8f);
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.NoDepthTest = true;
            mat.RenderPriority = 100;
            highlight.MaterialOverride = mat;

            _selectedPart.AddChild(highlight);
            _selectionHighlight = highlight;
        }

        // ── Spinbox Events ──

        private void OnSpinChanged()
        {
            if (_suppressSpinEvents) return;
            if (_selectedPart == null || !GodotObject.IsInstanceValid(_selectedPart)) return;

            _selectedPart.Position = new Vector3((float)_posX.Value, (float)_posY.Value, (float)_posZ.Value);
            _selectedPart.RotationDegrees = new Vector3((float)_rotX.Value, (float)_rotY.Value, (float)_rotZ.Value);
            MarkDirty();
        }

        private void ResetSelectedPart()
        {
            if (_selectedPart == null) return;

            // Reload model to get original transform
            LoadModel();
            MarkDirty();
        }

        private void NudgeSelected(Vector3 offset)
        {
            if (_selectedPart == null || !GodotObject.IsInstanceValid(_selectedPart)) return;
            _selectedPart.Position += offset;

            _suppressSpinEvents = true;
            _posX.Value = _selectedPart.Position.X;
            _posY.Value = _selectedPart.Position.Y;
            _posZ.Value = _selectedPart.Position.Z;
            _suppressSpinEvents = false;
            MarkDirty();
        }

        // ── Fire Point ──

        private void LoadFirePoint()
        {
            if (_config == null) return;
            string frameKey = _currentFrame.ToString();
            if (_config.TryGetValue(frameKey, out var frameObj) && frameObj is Dictionary<string, object> frameData)
            {
                if (frameData.TryGetValue("FirePointY", out var fy))
                    _fireY.Value = Convert.ToDouble(fy);
                if (frameData.TryGetValue("FirePointForward", out var ff))
                    _fireForward.Value = Convert.ToDouble(ff);
            }
        }

        private void OnFirePointChanged()
        {
            if (_suppressSpinEvents) return;
            MarkDirty();
        }

        private void ToggleFirePointMarker()
        {
            if (_firePointMarker != null && GodotObject.IsInstanceValid(_firePointMarker))
            {
                _firePointMarker.QueueFree();
                _firePointMarker = null;
                return;
            }

            CreateFirePointMarker();
        }

        private void CreateFirePointMarker()
        {
            if (_firePointMarker != null && GodotObject.IsInstanceValid(_firePointMarker))
                _firePointMarker.QueueFree();

            var marker = new MeshInstance3D();
            var sphere = new SphereMesh();
            sphere.Radius = 0.04f;
            sphere.Height = 0.08f;
            sphere.RadialSegments = 8;
            sphere.Rings = 4;
            marker.Mesh = sphere;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(1f, 0.5f, 0.1f, 0.9f);
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.EmissionEnabled = true;
            mat.Emission = new Color(1f, 0.5f, 0.1f);
            mat.EmissionEnergyMultiplier = 3f;
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.NoDepthTest = true;
            mat.RenderPriority = 100;
            marker.MaterialOverride = mat;

            // Position: height Y, forward on -Z (character faces -Z)
            marker.Position = new Vector3(0, (float)_fireY.Value, -(float)_fireForward.Value);
            _modelRoot.AddChild(marker);
            _firePointMarker = marker;
        }

        private void UpdateFirePointMarker()
        {
            if (_firePointMarker == null || !GodotObject.IsInstanceValid(_firePointMarker)) return;
            _firePointMarker.Position = new Vector3(0, (float)_fireY.Value, -(float)_fireForward.Value);
        }

        // ── Stats ──

        private void UpdateStats()
        {
            var data = BotFrameRegistry.GetClass(_currentFrame);
            if (data == null)
            {
                _statsLabel.Text = "No data";
                return;
            }

            var lines = new List<string>
            {
                $"HP: {data.BaseStats.GetBaseStat(StatType.MaxHealth)}  Mana: {data.BaseStats.GetBaseStat(StatType.MaxMana)}",
                $"STR: {data.BaseStats.GetBaseStat(StatType.Strength)}  DEX: {data.BaseStats.GetBaseStat(StatType.Dexterity)}",
                $"CON: {data.BaseStats.GetBaseStat(StatType.Constitution)}  INT: {data.BaseStats.GetBaseStat(StatType.Intelligence)}",
                $"CHA: {data.BaseStats.GetBaseStat(StatType.Charisma)}  LCK: {data.BaseStats.GetBaseStat(StatType.Luck)}",
                $"Armor: {data.BaseStats.GetBaseStat(StatType.Armor)}  Speed: {data.BaseStats.GetBaseStat(StatType.MoveSpeed)}",
                $"HP/Lvl: {data.HpPerLevel}  Mana/Lvl: {data.ManaPerLevel}",
                $"Primary: {data.PrimaryStat} (+{data.PrimaryStatPerLevel}/lvl)",
                $"Secondary: {data.SecondaryStat} (+{data.SecondaryStatPerLevel}/lvl)",
            };
            _statsLabel.Text = string.Join("\n", lines);
        }

        // ── Save / Load ──

        protected override void Reload()
        {
            _config = LoadJson(CONFIG_PATH) ?? new Dictionary<string, object>();
            LoadModel();
            MarkClean();
        }

        protected override void Save()
        {
            if (_config == null)
                _config = new Dictionary<string, object>();

            string frameKey = _currentFrame.ToString();
            var frameData = new Dictionary<string, object>();

            // Save part overrides
            var partOverrides = new Dictionary<string, object>();
            CollectPartOverrides(_modelRoot, partOverrides);
            if (partOverrides.Count > 0)
                frameData["Parts"] = partOverrides;

            // Save fire point
            frameData["FirePointY"] = _fireY.Value;
            frameData["FirePointForward"] = _fireForward.Value;

            _config[frameKey] = frameData;

            if (SaveJson(CONFIG_PATH, _config))
            {
                MarkClean();
                SetStatus($"Saved {frameKey} config", EditorStyles.StatusSaved);
            }
            else
            {
                SetStatus("Save failed!", EditorStyles.StatusError);
            }
        }

        private void CollectPartOverrides(Node root, Dictionary<string, object> overrides)
        {
            foreach (var child in root.GetChildren())
            {
                if (child is Node3D node)
                {
                    string name = node.Name.ToString();
                    if (EditableParts.Contains(name))
                    {
                        var partData = new Dictionary<string, object>
                        {
                            ["PosX"] = Math.Round(node.Position.X, 4),
                            ["PosY"] = Math.Round(node.Position.Y, 4),
                            ["PosZ"] = Math.Round(node.Position.Z, 4),
                            ["RotX"] = Math.Round(node.RotationDegrees.X, 2),
                            ["RotY"] = Math.Round(node.RotationDegrees.Y, 2),
                            ["RotZ"] = Math.Round(node.RotationDegrees.Z, 2),
                        };
                        overrides[name] = partData;
                    }
                    CollectPartOverrides(node, overrides);
                }
            }
        }

        private void ApplyOverrides(Node3D body)
        {
            if (_config == null) return;
            string frameKey = _currentFrame.ToString();
            if (!_config.TryGetValue(frameKey, out var frameObj)) return;
            if (frameObj is not Dictionary<string, object> frameData) return;
            if (!frameData.TryGetValue("Parts", out var partsObj)) return;
            if (partsObj is not Dictionary<string, object> parts) return;

            ApplyPartOverrides(body, parts);
        }

        private void ApplyPartOverrides(Node node, Dictionary<string, object> parts)
        {
            if (node is Node3D n3d)
            {
                string name = n3d.Name.ToString();
                if (parts.TryGetValue(name, out var partObj) && partObj is Dictionary<string, object> pd)
                {
                    if (pd.TryGetValue("PosX", out var px) && pd.TryGetValue("PosY", out var py) && pd.TryGetValue("PosZ", out var pz))
                        n3d.Position = new Vector3(Convert.ToSingle(px), Convert.ToSingle(py), Convert.ToSingle(pz));
                    if (pd.TryGetValue("RotX", out var rx) && pd.TryGetValue("RotY", out var ry) && pd.TryGetValue("RotZ", out var rz))
                        n3d.RotationDegrees = new Vector3(Convert.ToSingle(rx), Convert.ToSingle(ry), Convert.ToSingle(rz));
                }
            }

            foreach (var child in node.GetChildren())
            {
                if (child is Node childNode)
                    ApplyPartOverrides(childNode, parts);
            }
        }

        protected override void RestoreSnapshot(string jsonSnapshot) { }

        // ── UI Helpers ──

        private static SpinBox MakeSpinBox(string prefix, double min, double max, double step)
        {
            var spin = new SpinBox();
            spin.MinValue = min;
            spin.MaxValue = max;
            spin.Step = step;
            spin.CustomMinimumSize = new Vector2(70, 0);
            spin.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
            spin.Rounded = false;
            return spin;
        }

        private static VBoxContainer MakeLabeledSpin(string label, SpinBox spin)
        {
            var container = new VBoxContainer();
            container.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            var lbl = EditorStyles.MakeLabel(label, EditorStyles.FontTiny, EditorStyles.TextMuted);
            container.AddChild(lbl);
            container.AddChild(spin);
            return container;
        }
    }
}
