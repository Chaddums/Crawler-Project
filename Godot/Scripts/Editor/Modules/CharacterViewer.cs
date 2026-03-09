using Godot;
using System;
using System.Collections.Generic;

namespace JunkbotArena.Editor
{
    /// <summary>
    /// Character Designer — 3D model viewer with body part editing, color painting,
    /// decorative detail placement, growth piece adjustment, and animation preview.
    /// Select a bot frame to see its procedural mesh. Click parts in the scene tree
    /// to select them, then adjust position/rotation/color with the inspector.
    /// Saves overrides to Data/character_config.json.
    /// </summary>
    public partial class CharacterViewer : EditorPanel
    {
        public override string PanelName => "Characters";
        public override Color AccentColor => EditorStyles.AccentCharacter;

        private const string CONFIG_PATH = "res://Data/character_config.json";

        // ═══════════════════════════════════════════════
        //  3D Viewport
        // ═══════════════════════════════════════════════
        private SubViewport _viewport;
        private SubViewportContainer _viewportContainer;
        private Node3D _modelRoot;
        private Camera3D _camera;
        private float _cameraAngle;
        private float _cameraRadius = 4f;
        private float _cameraHeight = 2f;
        private bool _autoRotate = true;

        // ═══════════════════════════════════════════════
        //  Left panel controls
        // ═══════════════════════════════════════════════
        private Label _infoLabel;
        private Label _weaponLabelRef;
        private Label _growthLabelRef;
        private Label _mountLabelRef;

        // Part tree
        private VBoxContainer _partListContainer;
        private ScrollContainer _partScroll;

        // ═══════════════════════════════════════════════
        //  Right panel — Transform Inspector
        // ═══════════════════════════════════════════════
        private VBoxContainer _inspectorContainer;
        private Label _selectedPartLabel;
        private SpinBox _posX, _posY, _posZ;
        private SpinBox _rotX, _rotY, _rotZ;
        private SpinBox _scaleSpinBox;
        private VBoxContainer _scaleContainer;

        // Color editor
        private ColorPickerButton _colorPicker;
        private Button _resetColorBtn;
        private VBoxContainer _colorContainer;

        // Delete button (details only)
        private Button _deletePartBtn;

        // Parent bone selector (growth pieces)
        private VBoxContainer _parentBoneContainer;
        private OptionButton _parentBoneDropdown;

        // Fire point editor
        private SpinBox _fireX, _fireY, _fireForward;
        private MeshInstance3D _firePointMarker;

        // Stats
        private Label _statsLabel;

        // ═══════════════════════════════════════════════
        //  Selection state
        // ═══════════════════════════════════════════════
        private Node3D _selectedPart;
        private MeshInstance3D _selectionHighlight;
        private string _selectedPartName;
        private bool _suppressSpinEvents;

        // Drag state
        private bool _isDragging;
        private Vector2 _lastMousePos;

        // ═══════════════════════════════════════════════
        //  Growth & Mount state
        // ═══════════════════════════════════════════════
        private GrowthTier _currentGrowthTier = GrowthTier.Base;
        private WeaponMountType _currentMountType = WeaponMountType.HandHeld;
        private int _growthIndex;
        private int _mountIndex;

        // Frame / weapon state
        private BotFrameType _currentFrame = BotFrameType.TinCan;
        private WeaponType _currentWeapon = WeaponType.None;
        private int _frameIndex;
        private int _weaponIndex;

        // ═══════════════════════════════════════════════
        //  Animation preview
        // ═══════════════════════════════════════════════
        private ProceduralAnimator _animator;
        private bool _isAnimating;
        private Label _animWarningLabel;
        private AnimState _previewAnimState;

        // ═══════════════════════════════════════════════
        //  Details palette
        // ═══════════════════════════════════════════════
        private int _detailCounter;
        private string _pendingDetailType;
        private Label _placementModeLabel;

        // ═══════════════════════════════════════════════
        //  Persisted config
        // ═══════════════════════════════════════════════
        private Dictionary<string, object> _config;

        // ═══════════════════════════════════════════════
        //  Constants
        // ═══════════════════════════════════════════════

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

        private static readonly GrowthTier[] AllGrowthTiers =
        {
            GrowthTier.Base, GrowthTier.Plated, GrowthTier.Armored,
            GrowthTier.Heavy, GrowthTier.Evolved
        };

        private static readonly WeaponMountType[] AllMountTypes =
        {
            WeaponMountType.HandHeld, WeaponMountType.ShoulderMount,
            WeaponMountType.BackMount, WeaponMountType.ArmIntegrated
        };

        // Part names that are editable pivots (not decorative mesh children)
        private static readonly HashSet<string> EditableParts = new()
        {
            "Head", "Torso", "LeftArm", "RightArm", "LeftLeg", "RightLeg",
            "LeftElbow", "RightElbow", "LeftHand", "RightHand",
            "LeftKnee", "RightKnee", "LeftAnkle", "RightAnkle",
            "Weapon", "WeaponMount", "Body", "Crossbar", "Tail"
        };

        // Detail palette items
        private static readonly (string Name, string Label)[] DetailTypes =
        {
            ("Bolt", "Bolt"),
            ("Rivet", "Rivet"),
            ("PanelLine", "Panel Line"),
            ("Scratch", "Scratch"),
            ("PipeStub", "Pipe"),
            ("Plate", "Plate"),
            ("Wire", "Wire"),
            ("Antenna", "Antenna"),
            ("Box", "Box"),
            ("Cylinder", "Cylinder"),
            ("Sphere", "Sphere"),
            ("Vent", "Vent"),
        };

        // ═══════════════════════════════════════════════════════════════
        //  BUILD UI
        // ═══════════════════════════════════════════════════════════════

        protected override void BuildUI(VBoxContainer content)
        {
            var split = new HBoxContainer();
            split.SizeFlagsVertical = SizeFlags.ExpandFill;
            split.AddThemeConstantOverride("separation", 8);

            // ═══ LEFT PANEL ═══
            var leftPanel = new VBoxContainer();
            leftPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            leftPanel.CustomMinimumSize = new Vector2(270, 0);

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

            // Growth Tier selector
            leftPanel.AddChild(EditorStyles.MakeLabel("Growth Tier", EditorStyles.FontHeader, new Color(0.5f, 1f, 0.5f)));
            leftPanel.AddChild(BuildCycler(
                () => _currentGrowthTier.ToString(),
                dir => CycleGrowthTier(dir),
                out _growthLabelRef));

            leftPanel.AddChild(EditorStyles.MakeSeparator());

            // Weapon Mount Type selector
            leftPanel.AddChild(EditorStyles.MakeLabel("Weapon Mount", EditorStyles.FontHeader, new Color(1f, 0.7f, 0.4f)));
            leftPanel.AddChild(BuildCycler(
                () => _currentMountType.ToString(),
                dir => CycleMountType(dir),
                out _mountLabelRef));

            leftPanel.AddChild(EditorStyles.MakeSeparator());

            // Auto-rotate toggle
            var rotateCheck = new CheckBox();
            rotateCheck.Text = "Auto-Rotate";
            rotateCheck.ButtonPressed = true;
            rotateCheck.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
            rotateCheck.Toggled += v => _autoRotate = v;
            leftPanel.AddChild(rotateCheck);

            leftPanel.AddChild(EditorStyles.MakeSeparator());

            // ── Animation Preview ──
            leftPanel.AddChild(EditorStyles.MakeLabel("Animation", EditorStyles.FontHeader, new Color(0.4f, 0.8f, 1f)));
            var animRow1 = new HBoxContainer();
            animRow1.AddThemeConstantOverride("separation", 3);
            foreach (var state in new[] { AnimState.Idle, AnimState.Walk, AnimState.Run, AnimState.Attack })
            {
                var s = state;
                var btn = EditorStyles.MakeButton(state.ToString(), EditorStyles.FontTiny);
                btn.CustomMinimumSize = new Vector2(50, 24);
                btn.Pressed += () => PlayAnimation(s);
                animRow1.AddChild(btn);
            }
            leftPanel.AddChild(animRow1);

            var animRow2 = new HBoxContainer();
            animRow2.AddThemeConstantOverride("separation", 3);
            foreach (var state in new[] { AnimState.Hit, AnimState.Death, AnimState.Stunned })
            {
                var s = state;
                var btn = EditorStyles.MakeButton(state.ToString(), EditorStyles.FontTiny);
                btn.CustomMinimumSize = new Vector2(50, 24);
                btn.Pressed += () => PlayAnimation(s);
                animRow2.AddChild(btn);
            }
            var stopBtn = EditorStyles.MakeButton("Stop", EditorStyles.FontTiny, new Color(1f, 0.4f, 0.4f));
            stopBtn.CustomMinimumSize = new Vector2(50, 24);
            stopBtn.Pressed += StopAnimation;
            animRow2.AddChild(stopBtn);
            leftPanel.AddChild(animRow2);

            _animWarningLabel = EditorStyles.MakeLabel("", EditorStyles.FontTiny, new Color(1f, 0.8f, 0.3f));
            leftPanel.AddChild(_animWarningLabel);

            leftPanel.AddChild(EditorStyles.MakeSeparator());

            // ── Details Palette ──
            leftPanel.AddChild(EditorStyles.MakeLabel("Add Detail", EditorStyles.FontHeader, new Color(1f, 0.85f, 0.5f)));
            var paletteGrid = new GridContainer();
            paletteGrid.Columns = 4;
            paletteGrid.AddThemeConstantOverride("h_separation", 3);
            paletteGrid.AddThemeConstantOverride("v_separation", 3);
            foreach (var (typeName, label) in DetailTypes)
            {
                var captured = typeName;
                var btn = EditorStyles.MakeButton(label, EditorStyles.FontTiny);
                btn.CustomMinimumSize = new Vector2(55, 24);
                btn.Pressed += () => StartDetailPlacement(captured);
                paletteGrid.AddChild(btn);
            }
            leftPanel.AddChild(paletteGrid);

            _placementModeLabel = EditorStyles.MakeLabel("", EditorStyles.FontTiny, new Color(0.3f, 1f, 0.6f));
            leftPanel.AddChild(_placementModeLabel);

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

            // Lighting
            var light = new DirectionalLight3D();
            light.RotationDegrees = new Vector3(-55, 45, 0);
            light.LightEnergy = 2.5f;
            _viewport.AddChild(light);

            var fill = new DirectionalLight3D();
            fill.RotationDegrees = new Vector3(-48, -56, 0);
            fill.LightEnergy = 1.2f;
            _viewport.AddChild(fill);

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

            // ═══ RIGHT PANEL ═══
            var rightScroll = new ScrollContainer();
            rightScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            rightScroll.CustomMinimumSize = new Vector2(270, 0);

            var rightPanel = new VBoxContainer();
            rightPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            rightPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            // ── Transform Inspector ──
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

            // Scale (for detail pieces)
            _scaleContainer = new VBoxContainer();
            _scaleContainer.AddChild(EditorStyles.MakeLabel("Scale", EditorStyles.FontSmall, EditorStyles.TextMuted));
            var scaleRow = new HBoxContainer();
            scaleRow.AddThemeConstantOverride("separation", 4);
            _scaleSpinBox = MakeSpinBox("S", 0.1, 5.0, 0.1);
            _scaleSpinBox.Value = 1.0;
            scaleRow.AddChild(MakeLabeledSpin("Uniform", _scaleSpinBox));
            _scaleContainer.AddChild(scaleRow);
            _scaleContainer.Visible = false;
            _inspectorContainer.AddChild(_scaleContainer);

            // Nudge buttons
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

            // Delete button (detail pieces only)
            _deletePartBtn = EditorStyles.MakeButton("Delete Part", EditorStyles.FontSmall, new Color(1f, 0.3f, 0.3f));
            _deletePartBtn.Pressed += DeleteSelectedDetail;
            _deletePartBtn.Visible = false;
            _inspectorContainer.AddChild(_deletePartBtn);

            _inspectorContainer.AddChild(EditorStyles.MakeSeparator());

            // Parent bone selector — controls which body part this piece animates with
            _parentBoneContainer = new VBoxContainer();
            _parentBoneContainer.AddChild(EditorStyles.MakeLabel("Animate With", EditorStyles.FontHeader, new Color(0.5f, 1f, 0.5f)));
            _parentBoneContainer.AddChild(EditorStyles.MakeLabel("Which body part this moves with during animation", EditorStyles.FontTiny, EditorStyles.TextMuted));
            _parentBoneDropdown = new OptionButton();
            _parentBoneDropdown.AddThemeFontSizeOverride("font_size", EditorStyles.FontBody);
            _parentBoneDropdown.CustomMinimumSize = new Vector2(200, 30);
            _parentBoneDropdown.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            foreach (var pivotName in CharacterMeshBuilder.BodyPivotNames)
                _parentBoneDropdown.AddItem(pivotName);
            _parentBoneDropdown.Selected = 0;
            _parentBoneDropdown.ItemSelected += OnParentBoneChanged;
            _parentBoneContainer.AddChild(_parentBoneDropdown);
            _parentBoneContainer.Visible = false;
            _inspectorContainer.AddChild(_parentBoneContainer);

            rightPanel.AddChild(_inspectorContainer);

            rightPanel.AddChild(EditorStyles.MakeSeparator());

            // ── Color Editor ──
            _colorContainer = new VBoxContainer();
            _colorContainer.AddChild(EditorStyles.MakeLabel("Part Color", EditorStyles.FontHeader, new Color(1f, 0.7f, 0.9f)));

            var colorRow = new HBoxContainer();
            colorRow.AddThemeConstantOverride("separation", 4);
            _colorPicker = new ColorPickerButton();
            _colorPicker.CustomMinimumSize = new Vector2(60, 28);
            _colorPicker.Color = new Color(0.5f, 0.5f, 0.5f);
            _colorPicker.ColorChanged += OnColorChanged;
            colorRow.AddChild(_colorPicker);

            _resetColorBtn = EditorStyles.MakeButton("Reset", EditorStyles.FontSmall);
            _resetColorBtn.Pressed += ResetPartColor;
            colorRow.AddChild(_resetColorBtn);
            _colorContainer.AddChild(colorRow);
            rightPanel.AddChild(_colorContainer);

            rightPanel.AddChild(EditorStyles.MakeSeparator());

            // ── Fire Point Editor ──
            rightPanel.AddChild(EditorStyles.MakeLabel("Fire Point", EditorStyles.FontHeader, new Color(1f, 0.6f, 0.3f)));
            rightPanel.AddChild(EditorStyles.MakeLabel("Muzzle flash / projectile spawn offset", EditorStyles.FontTiny, EditorStyles.TextMuted));

            var fireRow = new HBoxContainer();
            fireRow.AddThemeConstantOverride("separation", 4);
            _fireX = MakeSpinBox("FX", -2, 2, 0.05f);
            _fireX.Value = 0.0;
            _fireY = MakeSpinBox("FY", 0, 3, 0.05f);
            _fireY.Value = 0.9;
            _fireForward = MakeSpinBox("FF", 0, 3, 0.05f);
            _fireForward.Value = 0.8;
            fireRow.AddChild(MakeLabeledSpin("Side", _fireX));
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

            rightScroll.AddChild(rightPanel);
            split.AddChild(rightScroll);
            content.AddChild(split);

            // Wire up spin events
            _posX.ValueChanged += _ => OnSpinChanged();
            _posY.ValueChanged += _ => OnSpinChanged();
            _posZ.ValueChanged += _ => OnSpinChanged();
            _rotX.ValueChanged += _ => OnSpinChanged();
            _rotY.ValueChanged += _ => OnSpinChanged();
            _rotZ.ValueChanged += _ => OnSpinChanged();
            _scaleSpinBox.ValueChanged += _ => OnScaleChanged();
            _fireX.ValueChanged += _ => { UpdateFirePointMarker(); OnFirePointChanged(); };
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

        // ═══════════════════════════════════════════════════════════════
        //  VIEWPORT INPUT
        // ═══════════════════════════════════════════════════════════════

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

                    // If in placement mode and left click pressed, place the detail
                    if (mb.ButtonIndex == MouseButton.Left && mb.Pressed && _pendingDetailType != null)
                    {
                        PlaceDetail();
                        _viewportContainer.AcceptEvent();
                        return;
                    }

                    // Click-to-select: left click picks the mesh under cursor
                    if (mb.ButtonIndex == MouseButton.Left && mb.Pressed && _pendingDetailType == null)
                    {
                        PickMeshAtClick(mb.Position);
                    }

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

        // ═══════════════════════════════════════════════════════════════
        //  CLICK-TO-SELECT (RAY-AABB PICKING)
        // ═══════════════════════════════════════════════════════════════

        private void PickMeshAtClick(Vector2 clickPos)
        {
            if (_camera == null || _modelRoot == null) return;
            if (_isAnimating) return;

            // Remap click position from container space to viewport space
            var containerSize = _viewportContainer.Size;
            var viewportSize = (Vector2)_viewport.Size;
            var vpPos = clickPos * viewportSize / containerSize;

            // Project ray from camera
            var rayOrigin = _camera.ProjectRayOrigin(vpPos);
            var rayDir = _camera.ProjectRayNormal(vpPos);

            // Find closest MeshInstance3D hit by ray-AABB test
            MeshInstance3D bestHit = null;
            float bestDist = float.MaxValue;
            CollectMeshHits(_modelRoot, rayOrigin, rayDir, ref bestHit, ref bestDist);

            if (bestHit == null) return;

            // Walk up from the hit mesh to find the nearest selectable ancestor
            Node3D selectable = FindSelectableAncestor(bestHit);
            if (selectable != null)
                SelectPart(selectable);
        }

        private void CollectMeshHits(Node node, Vector3 rayOrigin, Vector3 rayDir, ref MeshInstance3D bestHit, ref float bestDist)
        {
            if (node is MeshInstance3D mi && mi.Mesh != null)
            {
                // Skip the selection highlight sphere and fire point marker
                if (mi == _selectionHighlight || mi == _firePointMarker) goto children;

                var aabb = mi.GetAabb();
                var globalAabb = mi.GlobalTransform * aabb;

                // Ray-AABB intersection test
                if (IntersectRayAabb(rayOrigin, rayDir, globalAabb, out float dist))
                {
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestHit = mi;
                    }
                }
            }

            children:
            foreach (var child in node.GetChildren())
            {
                if (child is Node n)
                    CollectMeshHits(n, rayOrigin, rayDir, ref bestHit, ref bestDist);
            }
        }

        private static bool IntersectRayAabb(Vector3 origin, Vector3 dir, Aabb aabb, out float distance)
        {
            distance = 0f;
            float tmin = float.NegativeInfinity;
            float tmax = float.PositiveInfinity;

            for (int i = 0; i < 3; i++)
            {
                float o = i == 0 ? origin.X : i == 1 ? origin.Y : origin.Z;
                float d = i == 0 ? dir.X : i == 1 ? dir.Y : dir.Z;
                float bmin = i == 0 ? aabb.Position.X : i == 1 ? aabb.Position.Y : aabb.Position.Z;
                float bmax = bmin + (i == 0 ? aabb.Size.X : i == 1 ? aabb.Size.Y : aabb.Size.Z);

                if (Mathf.Abs(d) < 1e-8f)
                {
                    if (o < bmin || o > bmax) return false;
                }
                else
                {
                    float t1 = (bmin - o) / d;
                    float t2 = (bmax - o) / d;
                    if (t1 > t2) (t1, t2) = (t2, t1);
                    tmin = Mathf.Max(tmin, t1);
                    tmax = Mathf.Min(tmax, t2);
                    if (tmin > tmax) return false;
                }
            }

            if (tmax < 0) return false;
            distance = tmin > 0 ? tmin : tmax;
            return true;
        }

        private Node3D FindSelectableAncestor(Node3D node)
        {
            Node current = node;
            while (current != null && current != _modelRoot)
            {
                if (current is Node3D n3d)
                {
                    string name = n3d.Name.ToString();
                    if (EditableParts.Contains(name) || IsGrowthPiece(name) || IsDetailPiece(name))
                        return n3d;
                }
                current = current.GetParent();
            }
            return null;
        }

        // ═══════════════════════════════════════════════════════════════
        //  CYCLERS
        // ═══════════════════════════════════════════════════════════════

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

        private void CycleGrowthTier(int dir)
        {
            _growthIndex = (_growthIndex + dir + AllGrowthTiers.Length) % AllGrowthTiers.Length;
            _currentGrowthTier = AllGrowthTiers[_growthIndex];
            _growthLabelRef.Text = _currentGrowthTier.ToString();
            LoadModel();
        }

        private void CycleMountType(int dir)
        {
            _mountIndex = (_mountIndex + dir + AllMountTypes.Length) % AllMountTypes.Length;
            _currentMountType = AllMountTypes[_mountIndex];
            _mountLabelRef.Text = _currentMountType.ToString();
            LoadModel();
        }

        // ═══════════════════════════════════════════════════════════════
        //  CAMERA
        // ═══════════════════════════════════════════════════════════════

        private void UpdateCameraOrbit()
        {
            if (_camera == null) return;
            var pos = new Vector3(
                Mathf.Sin(_cameraAngle) * _cameraRadius,
                _cameraHeight,
                Mathf.Cos(_cameraAngle) * _cameraRadius);
            _camera.Position = pos;
            var target = new Vector3(0, 1, 0);
            var dir = (target - pos).Normalized();
            _camera.Transform = new Transform3D(
                Basis.LookingAt(dir, Vector3.Up), pos);
        }

        // ═══════════════════════════════════════════════════════════════
        //  MODEL LOADING
        // ═══════════════════════════════════════════════════════════════

        private void LoadModel()
        {
            if (_modelRoot == null) return;

            // Stop any animation
            StopAnimation();

            // Clear existing
            foreach (var child in _modelRoot.GetChildren())
            {
                if (child is Node n) n.QueueFree();
            }
            _selectedPart = null;
            _selectionHighlight = null;
            _firePointMarker = null;
            _pendingDetailType = null;
            if (_placementModeLabel != null) _placementModeLabel.Text = "";

            Node3D body = null;
            try
            {
                body = CharacterMeshBuilder.BuildPlayerBody(_currentFrame);
                if (body != null)
                {
                    _modelRoot.AddChild(body);
                    ApplyOverrides(body);
                }

                // Apply growth tier pieces
                if (_currentGrowthTier != GrowthTier.Base && body != null)
                {
                    var growthPieces = CharacterMeshBuilder.BuildGrowthPieces(_currentFrame, _currentGrowthTier);
                    if (growthPieces != null)
                    {
                        body.AddChild(growthPieces);
                        ApplyGrowthOverrides(growthPieces);

                        // Reparent growth pieces onto animated body pivots so they move with animations
                        var parentOverrides = LoadAllParentOverrides();
                        CharacterMeshBuilder.AttachGrowthToSkeleton(body, growthPieces, parentOverrides);
                    }

                    // Apply growth scale on top of the ScaleModelToFit base scale
                    float tierScale = _currentGrowthTier switch
                    {
                        GrowthTier.Plated => 1.08f,
                        GrowthTier.Armored => 1.15f,
                        GrowthTier.Heavy => 1.22f,
                        GrowthTier.Evolved => 1.3f,
                        _ => 1.0f
                    };
                    body.Scale *= tierScale;
                }

                // Apply general part parent overrides (reparent body parts to different pivots)
                if (body != null)
                    ApplyPartParentOverrides(body);

                // Find the default WeaponMount built into the body and clear its default weapon
                var defaultMount = body != null ? FindMarker(body, "WeaponMount") : null;
                if (defaultMount != null)
                {
                    foreach (var child in defaultMount.GetChildren())
                    {
                        if (child is Node3D c) { defaultMount.RemoveChild(c); c.QueueFree(); }
                    }
                }

                // For non-HandHeld mounts, hide the default mount and create a new one
                Marker3D activeMount = defaultMount;
                if (_currentMountType != WeaponMountType.HandHeld && body != null)
                {
                    var customMount = new Marker3D();
                    customMount.Name = $"Mount_{_currentMountType}";
                    customMount.Position = CharacterMeshBuilder.GetMountPosition(_currentFrame, _currentMountType);
                    body.AddChild(customMount);
                    activeMount = customMount;
                }

                if (_currentWeapon != WeaponType.None)
                {
                    bool isAoE = _currentWeapon is WeaponType.BladeRing or WeaponType.FlailChain
                        or WeaponType.ShockCoil or WeaponType.FlameThrower;

                    Node3D weaponModel;
                    if (isAoE)
                    {
                        weaponModel = _currentWeapon switch
                        {
                            WeaponType.BladeRing => CharacterMeshBuilder.BuildBladeRing(),
                            WeaponType.FlailChain => CharacterMeshBuilder.BuildFlailChain(),
                            WeaponType.ShockCoil => CharacterMeshBuilder.BuildShockCoil(),
                            _ => CharacterMeshBuilder.BuildFlameThrower(),
                        };
                    }
                    else
                    {
                        var weaponId = WeaponTypeToItemId(_currentWeapon);
                        var itemData = ItemRegistry.GetItem(weaponId);
                        weaponModel = itemData != null
                            ? CharacterMeshBuilder.BuildItemModel(new ItemInstance(itemData, ItemRarity.Common))
                            : CharacterMeshBuilder.BuildWeapon(_currentFrame);
                    }

                    if (weaponModel != null)
                    {
                        if (!isAoE && activeMount != null)
                        {
                            weaponModel.Position = Vector3.Zero;
                            weaponModel.RotationDegrees = CharacterMeshBuilder.GetMountRotation(_currentMountType);
                            float mountScale = CharacterMeshBuilder.GetMountScale(_currentMountType);
                            weaponModel.Scale = Vector3.One * mountScale;
                            activeMount.AddChild(weaponModel);
                        }
                        else if (isAoE)
                        {
                            weaponModel.Position = new Vector3(0, 1, 0);
                            _modelRoot.AddChild(weaponModel);
                        }
                    }
                }

                // Spawn saved detail pieces
                if (body != null)
                    SpawnSavedDetails(body);
            }
            catch (Exception e)
            {
                GD.PrintErr($"[CharacterViewer] Error loading model: {e.Message}");
            }

            BuildPartTree();
            UpdateStats();
            LoadFirePoint();

            // Initialize animator for preview
            InitAnimator(body);
        }

        // ═══════════════════════════════════════════════════════════════
        //  ANIMATION PREVIEW
        // ═══════════════════════════════════════════════════════════════

        private void InitAnimator(Node3D body)
        {
            // Clean up old animator
            if (_animator != null && GodotObject.IsInstanceValid(_animator))
            {
                _animator.QueueFree();
                _animator = null;
            }

            if (body == null) return;

            _animator = new ProceduralAnimator();
            AddChild(_animator);
            _animator.Initialize(body);
        }

        private void PlayAnimation(AnimState state)
        {
            if (_animator == null) return;

            _isAnimating = true;
            _previewAnimState = state;
            _animator.SetState(state);

            if (_animWarningLabel != null)
                _animWarningLabel.Text = $"Playing: {state} (editing disabled)";

            // Disable transform editing while animating
            SetInspectorEnabled(false);
        }

        private void StopAnimation()
        {
            if (_animator != null && GodotObject.IsInstanceValid(_animator) && _isAnimating)
            {
                _animator.ResetToBaseline();
                _animator.SetState(AnimState.Idle);
                // Immediately reset to stop the idle from running
                _animator.ResetToBaseline();
            }

            _isAnimating = false;
            if (_animWarningLabel != null)
                _animWarningLabel.Text = "";

            SetInspectorEnabled(true);
        }

        private void SetInspectorEnabled(bool enabled)
        {
            _posX.Editable = enabled;
            _posY.Editable = enabled;
            _posZ.Editable = enabled;
            _rotX.Editable = enabled;
            _rotY.Editable = enabled;
            _rotZ.Editable = enabled;
            _scaleSpinBox.Editable = enabled;
        }

        // ═══════════════════════════════════════════════════════════════
        //  PART TREE
        // ═══════════════════════════════════════════════════════════════

        private void BuildPartTree()
        {
            foreach (var child in _partListContainer.GetChildren())
            {
                if (child is Node n) n.QueueFree();
            }

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
            bool isGrowthPiece = IsGrowthPiece(name);
            bool isDetailPiece = IsDetailPiece(name);
            bool isSelectable = isEditable || isGrowthPiece || isDetailPiece;

            if (isSelectable || depth == 0)
            {
                var btn = new Button();
                string indent = new string(' ', depth * 2);

                // Icon and color coding
                string icon;
                Color textColor;
                if (isGrowthPiece)
                {
                    icon = "+";
                    textColor = new Color(0.5f, 1f, 0.5f); // green
                }
                else if (isDetailPiece)
                {
                    icon = "*";
                    textColor = new Color(1f, 0.85f, 0.5f); // gold
                }
                else if (isEditable)
                {
                    icon = ">";
                    textColor = AccentColor;
                }
                else
                {
                    icon = "-";
                    textColor = EditorStyles.TextMuted;
                }

                btn.Text = $"{indent}{icon} {name}";
                btn.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
                btn.Alignment = HorizontalAlignment.Left;
                btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                btn.AddThemeColorOverride("font_color", textColor);

                if (isSelectable)
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
                    bool childEditable = EditableParts.Contains(childName);
                    bool childGrowth = IsGrowthPiece(childName);
                    bool childDetail = IsDetailPiece(childName);
                    // Show editable children, growth pieces, details, and structural pivots
                    if (childEditable || childGrowth || childDetail || !childName.StartsWith("_"))
                        AddPartButtons(child3D, depth + 1);
                }
            }
        }

        private static bool IsGrowthPiece(string name)
        {
            return name.StartsWith("_T1_") || name.StartsWith("_T2_") ||
                   name.StartsWith("_T3_") || name.StartsWith("_T4_");
        }

        private static bool IsDetailPiece(string name)
        {
            return name.StartsWith("_Detail_");
        }

        // ═══════════════════════════════════════════════════════════════
        //  SELECTION
        // ═══════════════════════════════════════════════════════════════

        private void SelectPart(Node3D part)
        {
            if (part == null || !GodotObject.IsInstanceValid(part)) return;
            if (_isAnimating) return; // Don't allow selection during animation

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

            // Show scale for detail pieces
            bool isDetail = IsDetailPiece(_selectedPartName);
            bool isGrowth = IsGrowthPiece(_selectedPartName);
            _scaleContainer.Visible = isDetail || isGrowth;
            _deletePartBtn.Visible = isDetail;
            if (_scaleContainer.Visible)
                _scaleSpinBox.Value = part.Scale.X;

            // Show "Animate With" for all parts — lets user assign any part to a body pivot
            _parentBoneContainer.Visible = true;
            {
                // Find current parent pivot name
                string currentParent = "Body";
                // If this part IS a pivot, show its own name
                for (int i = 0; i < CharacterMeshBuilder.BodyPivotNames.Length; i++)
                {
                    if (CharacterMeshBuilder.BodyPivotNames[i] == _selectedPartName)
                    {
                        currentParent = _selectedPartName;
                        break;
                    }
                }
                // Otherwise check what pivot it's parented to
                if (currentParent == "Body" && part.GetParent() is Node3D parentNode)
                {
                    string pName = parentNode.Name.ToString();
                    for (int i = 0; i < CharacterMeshBuilder.BodyPivotNames.Length; i++)
                    {
                        if (CharacterMeshBuilder.BodyPivotNames[i] == pName)
                        {
                            currentParent = pName;
                            break;
                        }
                    }
                }
                for (int i = 0; i < _parentBoneDropdown.ItemCount; i++)
                {
                    if (_parentBoneDropdown.GetItemText(i) == currentParent)
                    {
                        _parentBoneDropdown.Selected = i;
                        break;
                    }
                }
            }

            _suppressSpinEvents = false;

            // Update color picker
            UpdateColorPickerFromSelection();

            // Highlight selection
            UpdateSelectionHighlight();
        }

        private void UpdateSelectionHighlight()
        {
            if (_selectionHighlight != null && GodotObject.IsInstanceValid(_selectionHighlight))
                _selectionHighlight.QueueFree();
            _selectionHighlight = null;

            if (_selectedPart == null || !GodotObject.IsInstanceValid(_selectedPart)) return;

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

        // ═══════════════════════════════════════════════════════════════
        //  TRANSFORM EDITING
        // ═══════════════════════════════════════════════════════════════

        private void OnSpinChanged()
        {
            if (_suppressSpinEvents) return;
            if (_selectedPart == null || !GodotObject.IsInstanceValid(_selectedPart)) return;

            _selectedPart.Position = new Vector3((float)_posX.Value, (float)_posY.Value, (float)_posZ.Value);
            _selectedPart.RotationDegrees = new Vector3((float)_rotX.Value, (float)_rotY.Value, (float)_rotZ.Value);
            MarkDirty();
        }

        private void OnScaleChanged()
        {
            if (_suppressSpinEvents) return;
            if (_selectedPart == null || !GodotObject.IsInstanceValid(_selectedPart)) return;

            float s = (float)_scaleSpinBox.Value;
            _selectedPart.Scale = Vector3.One * s;
            MarkDirty();
        }

        private void OnParentBoneChanged(long index)
        {
            if (_suppressSpinEvents) return;
            if (_selectedPart == null || !GodotObject.IsInstanceValid(_selectedPart)) return;

            string newParent = _parentBoneDropdown.GetItemText((int)index);
            string savedName = _selectedPartName;

            // If the selected part IS the pivot itself, nothing to reparent
            bool isSelf = false;
            for (int i = 0; i < CharacterMeshBuilder.BodyPivotNames.Length; i++)
            {
                if (CharacterMeshBuilder.BodyPivotNames[i] == savedName)
                {
                    isSelf = true;
                    break;
                }
            }
            if (isSelf && newParent == savedName) return;

            // Save the parent assignment — works for any part type
            SavePartParentOverride(savedName, newParent);
            LoadModel();

            // Re-select the part after reload
            var reselect = FindGrowthPieceByName(_modelRoot, savedName);
            if (reselect != null) SelectPart(reselect);
        }

        private Node3D FindGrowthPieceByName(Node root, string name)
        {
            foreach (var child in root.GetChildren())
            {
                if (child is Node3D n3d)
                {
                    if (n3d.Name.ToString() == name) return n3d;
                    var found = FindGrowthPieceByName(n3d, name);
                    if (found != null) return found;
                }
            }
            return null;
        }

        private void ResetSelectedPart()
        {
            if (_selectedPart == null) return;
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

        // ═══════════════════════════════════════════════════════════════
        //  COLOR EDITING
        // ═══════════════════════════════════════════════════════════════

        private MeshInstance3D GetEditableMesh(Node3D part)
        {
            if (part == null) return null;

            // If the part itself is a MeshInstance3D, use it directly
            if (part is MeshInstance3D mi) return mi;

            // Otherwise find the first MeshInstance3D child (for pivot nodes like Head, Torso)
            foreach (var child in part.GetChildren())
            {
                if (child is MeshInstance3D mesh)
                    return mesh;
            }
            return null;
        }

        private void UpdateColorPickerFromSelection()
        {
            var mesh = GetEditableMesh(_selectedPart);
            if (mesh?.MaterialOverride is StandardMaterial3D mat)
            {
                _suppressSpinEvents = true;
                _colorPicker.Color = mat.AlbedoColor;
                _suppressSpinEvents = false;
            }
        }

        private void OnColorChanged(Color color)
        {
            if (_suppressSpinEvents) return;
            if (_selectedPart == null || !GodotObject.IsInstanceValid(_selectedPart)) return;

            // Apply to all mesh children of the selected part
            ApplyColorToNode(_selectedPart, color);
            MarkDirty();
        }

        private static void ApplyColorToNode(Node3D node, Color color)
        {
            if (node is MeshInstance3D mi)
            {
                // Clone material if shared
                if (mi.MaterialOverride is StandardMaterial3D existing)
                {
                    var cloned = (StandardMaterial3D)existing.Duplicate();
                    cloned.AlbedoColor = color;
                    mi.MaterialOverride = cloned;
                }
                return;
            }

            // Apply to first mesh child for pivot nodes
            foreach (var child in node.GetChildren())
            {
                if (child is MeshInstance3D mesh && mesh.MaterialOverride is StandardMaterial3D childMat)
                {
                    var cloned = (StandardMaterial3D)childMat.Duplicate();
                    cloned.AlbedoColor = color;
                    mesh.MaterialOverride = cloned;
                    break;
                }
            }
        }

        private void ResetPartColor()
        {
            // Reload model to restore original colors
            LoadModel();
        }

        // ═══════════════════════════════════════════════════════════════
        //  DETAILS PALETTE & PLACEMENT
        // ═══════════════════════════════════════════════════════════════

        private void StartDetailPlacement(string detailType)
        {
            if (_isAnimating) return;

            _pendingDetailType = detailType;
            if (_placementModeLabel != null)
                _placementModeLabel.Text = $"Click viewport to place {detailType}...";
        }

        private void PlaceDetail()
        {
            if (_pendingDetailType == null) return;

            // Find the body root
            Node3D body = null;
            foreach (var child in _modelRoot.GetChildren())
            {
                if (child is Node3D n && n.Name.ToString() == "PlayerBody")
                { body = n; break; }
            }
            if (body == null)
            {
                foreach (var child in _modelRoot.GetChildren())
                {
                    if (child is Node3D n) { body = n; break; }
                }
            }
            if (body == null) return;

            // Determine parent: selected editable part or body root
            Node3D parent = body;
            if (_selectedPart != null && GodotObject.IsInstanceValid(_selectedPart) &&
                EditableParts.Contains(_selectedPart.Name.ToString()))
            {
                parent = _selectedPart;
            }

            // Create the detail mesh
            var detail = CreateDetailMesh(_pendingDetailType, _detailCounter++);
            if (detail == null) return;

            // Place at a reasonable default position relative to parent
            detail.Position = new Vector3(0, 0.05f, -0.08f);
            parent.AddChild(detail);

            // Clear placement mode
            _pendingDetailType = null;
            if (_placementModeLabel != null)
                _placementModeLabel.Text = "";

            // Select the newly placed detail
            SelectPart(detail);
            BuildPartTree();
            MarkDirty();
        }

        private static MeshInstance3D CreateDetailMesh(string type, int index)
        {
            var node = new MeshInstance3D();
            node.Name = $"_Detail_{type}_{index}";

            Color defaultColor = new Color(0.35f, 0.35f, 0.38f);
            Color boltColor = new Color(0.5f, 0.5f, 0.52f);

            Mesh mesh;
            Color color;
            switch (type)
            {
                case "Bolt":
                    mesh = new CylinderMesh { TopRadius = 0.018f, BottomRadius = 0.018f, Height = 0.015f, RadialSegments = 6 };
                    color = boltColor;
                    break;
                case "Rivet":
                    mesh = new SphereMesh { Radius = 0.012f, Height = 0.024f, RadialSegments = 6, Rings = 3 };
                    color = boltColor;
                    break;
                case "PanelLine":
                    mesh = new BoxMesh { Size = new Vector3(0.15f, 0.004f, 0.004f) };
                    color = new Color(0.2f, 0.2f, 0.22f);
                    break;
                case "Scratch":
                    mesh = new BoxMesh { Size = new Vector3(0.1f, 0.002f, 0.002f) };
                    color = new Color(0.55f, 0.5f, 0.45f);
                    break;
                case "PipeStub":
                    mesh = new CylinderMesh { TopRadius = 0.022f, BottomRadius = 0.025f, Height = 0.06f, RadialSegments = 8 };
                    color = defaultColor;
                    break;
                case "Plate":
                    mesh = new BoxMesh { Size = new Vector3(0.08f, 0.008f, 0.06f) };
                    color = defaultColor;
                    break;
                case "Wire":
                    mesh = new CylinderMesh { TopRadius = 0.005f, BottomRadius = 0.005f, Height = 0.12f, RadialSegments = 4 };
                    color = new Color(0.15f, 0.15f, 0.18f);
                    break;
                case "Antenna":
                    mesh = new CylinderMesh { TopRadius = 0.004f, BottomRadius = 0.01f, Height = 0.15f, RadialSegments = 4 };
                    color = defaultColor;
                    break;
                case "Box":
                    mesh = new BoxMesh { Size = new Vector3(0.06f, 0.06f, 0.06f) };
                    color = defaultColor;
                    break;
                case "Cylinder":
                    mesh = new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.03f, Height = 0.06f, RadialSegments = 8 };
                    color = defaultColor;
                    break;
                case "Sphere":
                    mesh = new SphereMesh { Radius = 0.03f, Height = 0.06f, RadialSegments = 8, Rings = 4 };
                    color = defaultColor;
                    break;
                case "Vent":
                    mesh = new BoxMesh { Size = new Vector3(0.05f, 0.03f, 0.008f) };
                    color = new Color(0.18f, 0.18f, 0.2f);
                    break;
                default:
                    return null;
            }

            node.Mesh = mesh;
            var mat = new StandardMaterial3D { AlbedoColor = color };
            node.MaterialOverride = mat;
            return node;
        }

        private void DeleteSelectedDetail()
        {
            if (_selectedPart == null || !GodotObject.IsInstanceValid(_selectedPart)) return;
            if (!IsDetailPiece(_selectedPart.Name.ToString())) return;

            _selectedPart.QueueFree();
            _selectedPart = null;
            _selectedPartLabel.Text = "(none selected)";
            _deletePartBtn.Visible = false;
            _scaleContainer.Visible = false;

            BuildPartTree();
            MarkDirty();
        }

        // ═══════════════════════════════════════════════════════════════
        //  FIRE POINT
        // ═══════════════════════════════════════════════════════════════

        private void LoadFirePoint()
        {
            if (_config == null) return;
            string frameKey = _currentFrame.ToString();
            if (_config.TryGetValue(frameKey, out var frameObj) && frameObj is Dictionary<string, object> frameData)
            {
                if (frameData.TryGetValue("FirePointX", out var fx))
                    _fireX.Value = Convert.ToDouble(fx);
                if (frameData.TryGetValue("FirePointY", out var fy))
                    _fireY.Value = Convert.ToDouble(fy);
                if (frameData.TryGetValue("FirePointForward", out var ff))
                    _fireForward.Value = Convert.ToDouble(ff);

                if (frameData.TryGetValue("WeaponMountType", out var mt) && mt is string mountStr)
                {
                    if (Enum.TryParse<WeaponMountType>(mountStr, out var parsed))
                    {
                        _currentMountType = parsed;
                        _mountIndex = Array.IndexOf(AllMountTypes, parsed);
                        if (_mountLabelRef != null) _mountLabelRef.Text = parsed.ToString();
                    }
                }
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
            var sphere = new SphereMesh { Radius = 0.04f, Height = 0.08f, RadialSegments = 8, Rings = 4 };
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

            marker.Position = new Vector3((float)_fireX.Value, (float)_fireY.Value, -(float)_fireForward.Value);
            _modelRoot.AddChild(marker);
            _firePointMarker = marker;
        }

        private void UpdateFirePointMarker()
        {
            if (_firePointMarker == null || !GodotObject.IsInstanceValid(_firePointMarker)) return;
            _firePointMarker.Position = new Vector3((float)_fireX.Value, (float)_fireY.Value, -(float)_fireForward.Value);
        }

        // ═══════════════════════════════════════════════════════════════
        //  STATS
        // ═══════════════════════════════════════════════════════════════

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

        // ═══════════════════════════════════════════════════════════════
        //  SAVE / LOAD
        // ═══════════════════════════════════════════════════════════════

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

            // Save part overrides (position, rotation, color)
            var partOverrides = new Dictionary<string, object>();
            CollectPartOverrides(_modelRoot, partOverrides);
            if (partOverrides.Count > 0)
                frameData["Parts"] = partOverrides;

            // Save growth piece overrides
            var growthOverrides = new Dictionary<string, object>();
            CollectGrowthOverrides(_modelRoot, growthOverrides);
            if (growthOverrides.Count > 0)
                frameData["GrowthParts"] = growthOverrides;

            // Preserve parent overrides from config (they're saved incrementally via dropdown)
            if (_config.TryGetValue(frameKey, out var existingFrame)
                && existingFrame is Dictionary<string, object> existingData)
            {
                string growthKey = $"GrowthParents_{_currentGrowthTier}";
                if (existingData.TryGetValue(growthKey, out var gpOverrides))
                    frameData[growthKey] = gpOverrides;
                if (existingData.TryGetValue("PartParents", out var ppOverrides))
                    frameData["PartParents"] = ppOverrides;
            }

            // Save detail pieces
            var details = new List<object>();
            CollectDetailPieces(_modelRoot, details);
            if (details.Count > 0)
                frameData["Details"] = details;

            // Save fire point
            frameData["FirePointX"] = _fireX.Value;
            frameData["FirePointY"] = _fireY.Value;
            frameData["FirePointForward"] = _fireForward.Value;

            // Save weapon mount type
            frameData["WeaponMountType"] = _currentMountType.ToString();

            _config[frameKey] = frameData;

            if (SaveJson(CONFIG_PATH, _config))
            {
                // Invalidate the runtime config cache so in-game systems
                // pick up the new overrides without requiring a full restart
                CharacterConfigLoader.Reload();

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

                        // Save color if mesh exists
                        var mesh = GetEditableMesh(node);
                        if (mesh?.MaterialOverride is StandardMaterial3D mat)
                        {
                            partData["ColorR"] = Math.Round(mat.AlbedoColor.R, 3);
                            partData["ColorG"] = Math.Round(mat.AlbedoColor.G, 3);
                            partData["ColorB"] = Math.Round(mat.AlbedoColor.B, 3);
                        }

                        overrides[name] = partData;
                    }
                    CollectPartOverrides(node, overrides);
                }
            }
        }

        private void CollectGrowthOverrides(Node root, Dictionary<string, object> overrides)
        {
            foreach (var child in root.GetChildren())
            {
                if (child is Node3D node)
                {
                    string name = node.Name.ToString();
                    if (IsGrowthPiece(name))
                    {
                        string key = $"{_currentGrowthTier}_{name}";
                        var partData = new Dictionary<string, object>
                        {
                            ["PosX"] = Math.Round(node.Position.X, 4),
                            ["PosY"] = Math.Round(node.Position.Y, 4),
                            ["PosZ"] = Math.Round(node.Position.Z, 4),
                            ["RotX"] = Math.Round(node.RotationDegrees.X, 2),
                            ["RotY"] = Math.Round(node.RotationDegrees.Y, 2),
                            ["RotZ"] = Math.Round(node.RotationDegrees.Z, 2),
                            ["ScaleX"] = Math.Round(node.Scale.X, 3),
                        };

                        if (node is MeshInstance3D mi && mi.MaterialOverride is StandardMaterial3D mat)
                        {
                            partData["ColorR"] = Math.Round(mat.AlbedoColor.R, 3);
                            partData["ColorG"] = Math.Round(mat.AlbedoColor.G, 3);
                            partData["ColorB"] = Math.Round(mat.AlbedoColor.B, 3);
                        }

                        overrides[key] = partData;
                    }
                    CollectGrowthOverrides(node, overrides);
                }
            }
        }

        private void CollectDetailPieces(Node root, List<object> details)
        {
            foreach (var child in root.GetChildren())
            {
                if (child is Node3D node)
                {
                    string name = node.Name.ToString();
                    if (IsDetailPiece(name))
                    {
                        // Parse type from name: _Detail_{Type}_{index}
                        string[] parts = name.Split('_');
                        string type = parts.Length >= 3 ? parts[2] : "Box";

                        string parentName = "Root";
                        if (node.GetParent() is Node3D parentNode)
                            parentName = parentNode.Name.ToString();

                        var detailData = new Dictionary<string, object>
                        {
                            ["Type"] = type,
                            ["Parent"] = parentName,
                            ["PosX"] = Math.Round(node.Position.X, 4),
                            ["PosY"] = Math.Round(node.Position.Y, 4),
                            ["PosZ"] = Math.Round(node.Position.Z, 4),
                            ["RotX"] = Math.Round(node.RotationDegrees.X, 2),
                            ["RotY"] = Math.Round(node.RotationDegrees.Y, 2),
                            ["RotZ"] = Math.Round(node.RotationDegrees.Z, 2),
                            ["Scale"] = Math.Round(node.Scale.X, 3),
                        };

                        if (node is MeshInstance3D mi && mi.MaterialOverride is StandardMaterial3D mat)
                        {
                            detailData["ColorR"] = Math.Round(mat.AlbedoColor.R, 3);
                            detailData["ColorG"] = Math.Round(mat.AlbedoColor.G, 3);
                            detailData["ColorB"] = Math.Round(mat.AlbedoColor.B, 3);
                        }

                        details.Add(detailData);
                    }
                    else
                    {
                        CollectDetailPieces(node, details);
                    }
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

                    // Apply color override
                    if (pd.TryGetValue("ColorR", out var cr) && pd.TryGetValue("ColorG", out var cg) && pd.TryGetValue("ColorB", out var cb))
                    {
                        var color = new Color(Convert.ToSingle(cr), Convert.ToSingle(cg), Convert.ToSingle(cb));
                        ApplyColorToNode(n3d, color);
                    }
                }
            }

            foreach (var child in node.GetChildren())
            {
                if (child is Node childNode)
                    ApplyPartOverrides(childNode, parts);
            }
        }

        private void ApplyGrowthOverrides(Node3D growthRoot)
        {
            if (_config == null || growthRoot == null) return;
            string frameKey = _currentFrame.ToString();
            if (!_config.TryGetValue(frameKey, out var frameObj)) return;
            if (frameObj is not Dictionary<string, object> frameData) return;
            if (!frameData.TryGetValue("GrowthParts", out var gpObj)) return;
            if (gpObj is not Dictionary<string, object> growthParts) return;

            ApplyGrowthOverridesRecursive(growthRoot, growthParts);
        }

        private void ApplyGrowthOverridesRecursive(Node node, Dictionary<string, object> growthParts)
        {
            if (node is Node3D n3d && IsGrowthPiece(n3d.Name.ToString()))
            {
                string key = $"{_currentGrowthTier}_{n3d.Name}";
                if (growthParts.TryGetValue(key, out var gpObj) && gpObj is Dictionary<string, object> pd)
                {
                    if (pd.TryGetValue("PosX", out var px) && pd.TryGetValue("PosY", out var py) && pd.TryGetValue("PosZ", out var pz))
                        n3d.Position = new Vector3(Convert.ToSingle(px), Convert.ToSingle(py), Convert.ToSingle(pz));
                    if (pd.TryGetValue("RotX", out var rx) && pd.TryGetValue("RotY", out var ry) && pd.TryGetValue("RotZ", out var rz))
                        n3d.RotationDegrees = new Vector3(Convert.ToSingle(rx), Convert.ToSingle(ry), Convert.ToSingle(rz));
                    if (pd.TryGetValue("ScaleX", out var sx))
                        n3d.Scale = Vector3.One * Convert.ToSingle(sx);

                    if (pd.TryGetValue("ColorR", out var cr) && pd.TryGetValue("ColorG", out var cg) && pd.TryGetValue("ColorB", out var cb))
                    {
                        var color = new Color(Convert.ToSingle(cr), Convert.ToSingle(cg), Convert.ToSingle(cb));
                        if (n3d is MeshInstance3D mi && mi.MaterialOverride is StandardMaterial3D mat)
                        {
                            var cloned = (StandardMaterial3D)mat.Duplicate();
                            cloned.AlbedoColor = color;
                            mi.MaterialOverride = cloned;
                        }
                    }
                }
            }

            foreach (var child in node.GetChildren())
            {
                if (child is Node cn)
                    ApplyGrowthOverridesRecursive(cn, growthParts);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  PART PARENT OVERRIDES (animation attachment)
        // ═══════════════════════════════════════════════════════════════

        private Dictionary<string, string> LoadAllParentOverrides()
        {
            if (_config == null) return null;
            string frameKey = _currentFrame.ToString();
            if (!_config.TryGetValue(frameKey, out var frameObj)) return null;
            if (frameObj is not Dictionary<string, object> frameData) return null;

            var result = new Dictionary<string, string>();

            // Load growth-tier-specific overrides
            string growthKey = $"GrowthParents_{_currentGrowthTier}";
            if (frameData.TryGetValue(growthKey, out var gpObj) && gpObj is Dictionary<string, object> growthOverrides)
            {
                foreach (var kvp in growthOverrides)
                    result[kvp.Key] = kvp.Value?.ToString() ?? "Body";
            }

            // Load general part parent overrides (for body parts, details, etc.)
            if (frameData.TryGetValue("PartParents", out var ppObj) && ppObj is Dictionary<string, object> partParents)
            {
                foreach (var kvp in partParents)
                    result[kvp.Key] = kvp.Value?.ToString() ?? "Body";
            }

            return result.Count > 0 ? result : null;
        }

        private void SavePartParentOverride(string partName, string parentPivot)
        {
            if (_config == null) return;
            string frameKey = _currentFrame.ToString();
            if (!_config.TryGetValue(frameKey, out var frameObj))
            {
                frameObj = new Dictionary<string, object>();
                _config[frameKey] = frameObj;
            }
            var frameData = frameObj as Dictionary<string, object>;
            if (frameData == null) return;

            // Growth pieces go in tier-specific key, everything else in PartParents
            if (IsGrowthPiece(partName))
            {
                string growthKey = $"GrowthParents_{_currentGrowthTier}";
                if (!frameData.TryGetValue(growthKey, out var gpObj) || gpObj is not Dictionary<string, object> overrides)
                {
                    overrides = new Dictionary<string, object>();
                    frameData[growthKey] = overrides;
                }
                overrides[partName] = parentPivot;
            }
            else
            {
                if (!frameData.TryGetValue("PartParents", out var ppObj) || ppObj is not Dictionary<string, object> partParents)
                {
                    partParents = new Dictionary<string, object>();
                    frameData["PartParents"] = partParents;
                }
                partParents[partName] = parentPivot;
            }
            MarkDirty();
        }

        private void ApplyPartParentOverrides(Node3D body)
        {
            if (_config == null) return;
            string frameKey = _currentFrame.ToString();
            if (!_config.TryGetValue(frameKey, out var frameObj)) return;
            if (frameObj is not Dictionary<string, object> frameData) return;
            if (!frameData.TryGetValue("PartParents", out var ppObj)) return;
            if (ppObj is not Dictionary<string, object> partParents) return;

            foreach (var kvp in partParents)
            {
                string partName = kvp.Key;
                string targetPivotName = kvp.Value?.ToString() ?? "Body";

                // Find the part in the body tree
                var part = FindGrowthPieceByName(body, partName);
                if (part == null) continue;

                // Find the target pivot
                Node3D target;
                if (targetPivotName == "Body")
                    target = body;
                else
                {
                    target = FindGrowthPieceByName(body, targetPivotName);
                    if (target == null) target = body;
                }

                // Don't reparent to self or to own descendant
                if (target == part) continue;
                if (IsDescendantOf(target, part)) continue;

                // Already in the right place?
                if (part.GetParent() == target) continue;

                // Calculate position in body-root space, then convert to target-local space
                Vector3 bodySpacePos = CharacterMeshBuilder.GetPositionRelativeToPublic(part, body);
                Vector3 targetPosInBodySpace = (target == body) ? Vector3.Zero
                    : CharacterMeshBuilder.GetPositionRelativeToPublic(target, body);
                Vector3 localPos = bodySpacePos - targetPosInBodySpace;

                var oldParent = part.GetParent();
                oldParent?.RemoveChild(part);
                target.AddChild(part);
                part.Position = localPos;
            }
        }

        private static bool IsDescendantOf(Node potentialDescendant, Node potentialAncestor)
        {
            var current = potentialDescendant;
            while (current != null)
            {
                if (current == potentialAncestor) return true;
                current = current.GetParent();
            }
            return false;
        }

        private void SpawnSavedDetails(Node3D body)
        {
            if (_config == null) return;
            string frameKey = _currentFrame.ToString();
            if (!_config.TryGetValue(frameKey, out var frameObj)) return;
            if (frameObj is not Dictionary<string, object> frameData) return;
            if (!frameData.TryGetValue("Details", out var detailsObj)) return;
            if (detailsObj is not List<object> details) return;

            _detailCounter = 0;
            foreach (var item in details)
            {
                if (item is not Dictionary<string, object> dd) continue;

                string type = dd.TryGetValue("Type", out var t) ? t.ToString() : "Box";
                string parentName = dd.TryGetValue("Parent", out var p) ? p.ToString() : "Root";

                var detail = CreateDetailMesh(type, _detailCounter++);
                if (detail == null) continue;

                // Apply transform
                if (dd.TryGetValue("PosX", out var px) && dd.TryGetValue("PosY", out var py) && dd.TryGetValue("PosZ", out var pz))
                    detail.Position = new Vector3(Convert.ToSingle(px), Convert.ToSingle(py), Convert.ToSingle(pz));
                if (dd.TryGetValue("RotX", out var rx) && dd.TryGetValue("RotY", out var ry) && dd.TryGetValue("RotZ", out var rz))
                    detail.RotationDegrees = new Vector3(Convert.ToSingle(rx), Convert.ToSingle(ry), Convert.ToSingle(rz));
                if (dd.TryGetValue("Scale", out var s))
                    detail.Scale = Vector3.One * Convert.ToSingle(s);

                // Apply color
                if (dd.TryGetValue("ColorR", out var cr) && dd.TryGetValue("ColorG", out var cg) && dd.TryGetValue("ColorB", out var cb))
                {
                    var color = new Color(Convert.ToSingle(cr), Convert.ToSingle(cg), Convert.ToSingle(cb));
                    if (detail.MaterialOverride is StandardMaterial3D mat)
                    {
                        mat.AlbedoColor = color;
                    }
                }

                // Find parent node
                Node3D parent = body;
                if (parentName != "Root" && parentName != "PlayerBody")
                {
                    var found = FindPartByName(body, parentName);
                    if (found != null) parent = found;
                }

                parent.AddChild(detail);
            }
        }

        private static Node3D FindPartByName(Node root, string name)
        {
            if (root is Node3D n3d && n3d.Name.ToString() == name) return n3d;
            foreach (var child in root.GetChildren())
            {
                if (child is Node cn)
                {
                    var found = FindPartByName(cn, name);
                    if (found != null) return found;
                }
            }
            return null;
        }

        protected override void RestoreSnapshot(string jsonSnapshot) { }

        // ═══════════════════════════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════════════════════════

        private static string WeaponTypeToItemId(WeaponType type) => type switch
        {
            WeaponType.Pistol => "base_pistol",
            WeaponType.Rifle => "base_rifle",
            WeaponType.Shotgun => "base_shotgun",
            WeaponType.Launcher => "base_launcher",
            WeaponType.Repeater => "base_repeater",
            WeaponType.BladeRing => "base_blade_ring",
            WeaponType.FlailChain => "base_flail_chain",
            WeaponType.ShockCoil => "base_shock_coil",
            WeaponType.FlameThrower => "base_flame_thrower",
            _ => "base_pistol"
        };

        private static Marker3D FindMarker(Node root, string name)
        {
            if (root is Marker3D m && m.Name == name) return m;
            foreach (var child in root.GetChildren())
            {
                if (child is Node n)
                {
                    var found = FindMarker(n, name);
                    if (found != null) return found;
                }
            }
            return null;
        }

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
