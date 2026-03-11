using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JunkbotArena.Editor
{
    /// <summary>
    /// Combatants Editor — preview and tune all enemy combatants (regular enemies + bosses).
    /// Left: combatant list with tier filters. Center: 3D viewport. Right: stats inspector.
    /// Boss-specific sections (phases, attacks, abilities) shown only for boss-tier entries.
    /// Saves overrides to Data/boss_config.json (shared key space for all combatants).
    /// </summary>
    public partial class BossEditor : EditorPanel
    {
        public override string PanelName => "Combatants";
        public override Color AccentColor => new Color(1f, 0.25f, 0.2f);

        private const string CONFIG_PATH = "res://Data/boss_config.json";

        // 3D viewport
        private SubViewport _viewport;
        private SubViewportContainer _viewportContainer;
        private Node3D _modelRoot;
        private Camera3D _camera;
        private float _cameraAngle;
        private float _cameraRadius = 6f;
        private float _cameraHeight = 3f;
        private bool _autoRotate = true;
        private bool _isDragging;
        private Vector2 _lastMousePos;

        // Left panel
        private VBoxContainer _combatantListContainer;
        private Label _selectedNameLabel;
        private Label _stateLabel;
        private VBoxContainer _abilityListContainer;
        private LineEdit _filterEdit;

        // Stats inspector (shared)
        private SpinBox _healthSpin, _damageSpin, _speedSpin, _armorSpin;
        private SpinBox _attackRangeSpin, _cooldownSpin, _aggroSpin, _xpSpin;

        // Enemy-specific fields
        private VBoxContainer _enemyFieldsContainer;
        private OptionButton _tierPicker;
        private OptionButton _behaviorPicker;
        private SpinBox _sigDropChanceSpin;
        private SpinBox _lootBoxChanceSpin;
        private ColorPickerButton _meshColorPicker;

        // Boss-specific sections
        private VBoxContainer _bossFieldsContainer;
        private SpinBox _phase2Spin, _phase3Spin;
        private SpinBox _slamRadius, _slamKnockback, _slamMult;
        private SpinBox _chargeSpeedMult, _chargeKnockback, _chargeMult;
        private SpinBox _barrageCount, _barrageSpeed, _barrageRange, _barrageSpread;
        private SpinBox _summonCount;

        // Filter buttons
        private Button _filterAll, _filterNormal, _filterElite, _filterMiniBoss, _filterBoss;

        // State
        private string _currentId;
        private int _previewState;
        private bool _suppressSpinEvents;
        private Dictionary<string, object> _config;
        private string _tierFilter = "All";

        private static readonly string[] BossIds =
        {
            "corrupted_sentry", "rust_titan", "scrap_hydra", "null_warden", "axis_avatar"
        };

        private static readonly string[] StateNames =
        {
            "Idle", "Intro", "Chase", "Attack", "Special", "Stunned", "Phase Transition", "Dead"
        };

        protected override void BuildUI(VBoxContainer content)
        {
            var split = new HBoxContainer();
            split.SizeFlagsVertical = SizeFlags.ExpandFill;
            split.AddThemeConstantOverride("separation", 8);

            // ═══ LEFT PANEL: combatant list ═══
            var leftScroll = new ScrollContainer();
            leftScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            leftScroll.CustomMinimumSize = new Vector2(240, 0);

            var leftPanel = new VBoxContainer();
            leftPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            leftPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            leftPanel.AddChild(EditorStyles.MakeLabel("Combatants", EditorStyles.FontHeader, AccentColor));

            // Tier filter row
            var filterRow = new HBoxContainer();
            filterRow.AddThemeConstantOverride("separation", 2);

            _filterAll = MakeFilterBtn("All", true);
            _filterNormal = MakeFilterBtn("Normal", false);
            _filterElite = MakeFilterBtn("Elite", false);
            _filterMiniBoss = MakeFilterBtn("Mini", false);
            _filterBoss = MakeFilterBtn("Boss", false);

            filterRow.AddChild(_filterAll);
            filterRow.AddChild(_filterNormal);
            filterRow.AddChild(_filterElite);
            filterRow.AddChild(_filterMiniBoss);
            filterRow.AddChild(_filterBoss);
            leftPanel.AddChild(filterRow);

            // Search filter
            _filterEdit = EditorStyles.MakeLineEdit("Search...");
            _filterEdit.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
            _filterEdit.TextChanged += _ => RebuildCombatantList();
            leftPanel.AddChild(_filterEdit);

            leftPanel.AddChild(EditorStyles.MakeSeparator());

            // Combatant list
            _combatantListContainer = new VBoxContainer();
            _combatantListContainer.AddThemeConstantOverride("separation", 1);
            leftPanel.AddChild(_combatantListContainer);

            leftPanel.AddChild(EditorStyles.MakeSeparator());

            // Preview state cycler
            leftPanel.AddChild(EditorStyles.MakeLabel("Preview State", EditorStyles.FontSmall, EditorStyles.TextSecondary));
            leftPanel.AddChild(BuildCycler(
                () => StateNames[_previewState],
                dir => CycleState(dir),
                out _stateLabel));

            // Auto-rotate toggle
            var rotateCheck = new CheckBox();
            rotateCheck.Text = "Auto-Rotate";
            rotateCheck.ButtonPressed = true;
            rotateCheck.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
            rotateCheck.Toggled += v => _autoRotate = v;
            leftPanel.AddChild(rotateCheck);

            leftPanel.AddChild(EditorStyles.MakeSeparator());

            // Abilities list (shown for bosses)
            leftPanel.AddChild(EditorStyles.MakeLabel("Abilities", EditorStyles.FontHeader, EditorStyles.TextSecondary));

            var abilityScroll = new ScrollContainer();
            abilityScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            _abilityListContainer = new VBoxContainer();
            _abilityListContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            abilityScroll.AddChild(_abilityListContainer);
            leftPanel.AddChild(abilityScroll);

            var addAbilityBtn = EditorStyles.MakeButton("+ Add Ability", EditorStyles.FontSmall, new Color(0.3f, 0.8f, 0.4f));
            addAbilityBtn.Pressed += ShowAddAbilityPopup;
            leftPanel.AddChild(addAbilityBtn);

            leftScroll.AddChild(leftPanel);
            split.AddChild(leftScroll);

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

            var ground = new MeshInstance3D();
            ground.Mesh = new PlaneMesh { Size = new Vector2(20, 20) };
            ground.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.06f, 0.06f, 0.08f) };
            _viewport.AddChild(ground);

            var light = new DirectionalLight3D();
            light.Position = new Vector3(5, 10, 5);
            var lightDir = (Vector3.Zero - light.Position).Normalized();
            light.Transform = new Transform3D(Basis.LookingAt(lightDir, Vector3.Up), light.Position);
            light.LightEnergy = 1.2f;
            _viewport.AddChild(light);

            var fill = new DirectionalLight3D();
            fill.Position = new Vector3(-5, 8, -3);
            var fillDir = (Vector3.Zero - fill.Position).Normalized();
            fill.Transform = new Transform3D(Basis.LookingAt(fillDir, Vector3.Up), fill.Position);
            fill.LightEnergy = 0.4f;
            _viewport.AddChild(fill);

            var env = new WorldEnvironment();
            var envRes = new Godot.Environment();
            envRes.BackgroundMode = Godot.Environment.BGMode.Color;
            envRes.BackgroundColor = new Color(0.04f, 0.04f, 0.06f);
            envRes.AmbientLightSource = Godot.Environment.AmbientSource.Color;
            envRes.AmbientLightColor = new Color(0.12f, 0.12f, 0.15f);
            env.Environment = envRes;
            _viewport.AddChild(env);

            _viewportContainer.AddChild(_viewport);
            centerPanel.AddChild(_viewportContainer);

            // Selected name label below viewport
            _selectedNameLabel = EditorStyles.MakeLabel("(select a combatant)", EditorStyles.FontHeader, AccentColor);
            _selectedNameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            centerPanel.AddChild(_selectedNameLabel);

            split.AddChild(centerPanel);

            // ═══ RIGHT PANEL: stats inspector ═══
            var rightScroll = new ScrollContainer();
            rightScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            rightScroll.CustomMinimumSize = new Vector2(280, 0);

            var rightPanel = new VBoxContainer();
            rightPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            // --- Base Stats (shared) ---
            rightPanel.AddChild(EditorStyles.MakeLabel("Base Stats", EditorStyles.FontHeader, AccentColor));

            _healthSpin = AddStatRow(rightPanel, "Health", 1, 5000, 1);
            _damageSpin = AddStatRow(rightPanel, "Damage", 0, 200, 1);
            _speedSpin = AddStatRow(rightPanel, "Move Speed", 0, 20, 0.1);
            _armorSpin = AddStatRow(rightPanel, "Armor", 0, 50, 1);
            _attackRangeSpin = AddStatRow(rightPanel, "Attack Range", 0, 50, 0.5);
            _cooldownSpin = AddStatRow(rightPanel, "Atk Cooldown", 0.1, 10, 0.1);
            _aggroSpin = AddStatRow(rightPanel, "Aggro Range", 0, 50, 1);
            _xpSpin = AddStatRow(rightPanel, "XP Reward", 0, 2000, 1);

            rightPanel.AddChild(EditorStyles.MakeSeparator());

            // --- Enemy-specific fields ---
            _enemyFieldsContainer = new VBoxContainer();
            _enemyFieldsContainer.AddThemeConstantOverride("separation", 4);

            _enemyFieldsContainer.AddChild(EditorStyles.MakeLabel("Enemy Properties", EditorStyles.FontHeader, new Color(0.8f, 0.6f, 0.2f)));

            // Tier
            var tierRow = new HBoxContainer();
            tierRow.AddThemeConstantOverride("separation", 4);
            tierRow.AddChild(MakeRowLabel("Tier"));
            _tierPicker = new OptionButton();
            _tierPicker.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
            foreach (var tier in Enum.GetValues<EnemyTier>())
                _tierPicker.AddItem(tier.ToString());
            _tierPicker.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _tierPicker.ItemSelected += _ => { if (!_suppressSpinEvents) MarkDirty(); };
            tierRow.AddChild(_tierPicker);
            _enemyFieldsContainer.AddChild(tierRow);

            // Behavior
            var behaviorRow = new HBoxContainer();
            behaviorRow.AddThemeConstantOverride("separation", 4);
            behaviorRow.AddChild(MakeRowLabel("Behavior"));
            _behaviorPicker = new OptionButton();
            _behaviorPicker.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
            foreach (var b in Enum.GetValues<EnemyBehavior>())
                _behaviorPicker.AddItem(b.ToString());
            _behaviorPicker.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _behaviorPicker.ItemSelected += _ => { if (!_suppressSpinEvents) MarkDirty(); };
            behaviorRow.AddChild(_behaviorPicker);
            _enemyFieldsContainer.AddChild(behaviorRow);

            // Mesh color
            var colorRow = new HBoxContainer();
            colorRow.AddThemeConstantOverride("separation", 4);
            colorRow.AddChild(MakeRowLabel("Mesh Color"));
            _meshColorPicker = new ColorPickerButton();
            _meshColorPicker.CustomMinimumSize = new Vector2(0, 28);
            _meshColorPicker.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _meshColorPicker.ColorChanged += _ => { if (!_suppressSpinEvents) MarkDirty(); };
            colorRow.AddChild(_meshColorPicker);
            _enemyFieldsContainer.AddChild(colorRow);

            // Signature drop chance
            _sigDropChanceSpin = AddStatRow(_enemyFieldsContainer, "Sig Drop %", 0, 1, 0.05);
            _lootBoxChanceSpin = AddStatRow(_enemyFieldsContainer, "LootBox Drop %", 0, 1, 0.05);

            rightPanel.AddChild(_enemyFieldsContainer);
            rightPanel.AddChild(EditorStyles.MakeSeparator());

            // --- Boss-specific fields ---
            _bossFieldsContainer = new VBoxContainer();
            _bossFieldsContainer.AddThemeConstantOverride("separation", 4);

            _bossFieldsContainer.AddChild(EditorStyles.MakeLabel("Phase Thresholds", EditorStyles.FontHeader, AccentColor));
            _phase2Spin = AddStatRow(_bossFieldsContainer, "Phase 2 (%HP)", 0.1, 0.9, 0.05);
            _phase3Spin = AddStatRow(_bossFieldsContainer, "Phase 3 (%HP)", 0.05, 0.8, 0.05);

            _bossFieldsContainer.AddChild(EditorStyles.MakeSeparator());

            _bossFieldsContainer.AddChild(EditorStyles.MakeLabel("Ground Slam", EditorStyles.FontSmall, new Color(0.9f, 0.6f, 0.2f)));
            _slamRadius = AddStatRow(_bossFieldsContainer, "AoE Radius", 1, 20, 0.5);
            _slamKnockback = AddStatRow(_bossFieldsContainer, "Knockback", 1, 30, 1);
            _slamMult = AddStatRow(_bossFieldsContainer, "Damage Mult", 0.5, 5, 0.1);

            _bossFieldsContainer.AddChild(EditorStyles.MakeSeparator());

            _bossFieldsContainer.AddChild(EditorStyles.MakeLabel("Charge Attack", EditorStyles.FontSmall, new Color(1f, 0.4f, 0.3f)));
            _chargeSpeedMult = AddStatRow(_bossFieldsContainer, "Speed Mult", 1, 10, 0.5);
            _chargeKnockback = AddStatRow(_bossFieldsContainer, "Knockback", 1, 30, 1);
            _chargeMult = AddStatRow(_bossFieldsContainer, "Damage Mult", 0.5, 5, 0.1);

            _bossFieldsContainer.AddChild(EditorStyles.MakeSeparator());

            _bossFieldsContainer.AddChild(EditorStyles.MakeLabel("Projectile Barrage", EditorStyles.FontSmall, new Color(0.6f, 0.3f, 1f)));
            _barrageCount = AddStatRow(_bossFieldsContainer, "Projectiles", 1, 20, 1);
            _barrageSpeed = AddStatRow(_bossFieldsContainer, "Speed", 2, 40, 1);
            _barrageRange = AddStatRow(_bossFieldsContainer, "Range", 5, 50, 1);
            _barrageSpread = AddStatRow(_bossFieldsContainer, "Spread (deg)", 5, 90, 5);

            _bossFieldsContainer.AddChild(EditorStyles.MakeSeparator());

            _bossFieldsContainer.AddChild(EditorStyles.MakeLabel("Summon Adds", EditorStyles.FontSmall, new Color(0.4f, 0.8f, 0.3f)));
            _summonCount = AddStatRow(_bossFieldsContainer, "Add Count", 1, 10, 1);

            rightPanel.AddChild(_bossFieldsContainer);

            rightScroll.AddChild(rightPanel);
            split.AddChild(rightScroll);
            content.AddChild(split);
        }

        public override void _Ready()
        {
            base._Ready();
            EnemyRegistry.Initialize();
            SelectCombatant(BossIds[0]);
        }

        public override void _Process(double delta)
        {
            if (!Visible) return;
            if (_autoRotate && _modelRoot != null)
            {
                _cameraAngle += (float)delta * 0.6f;
                UpdateCameraOrbit();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  COMBATANT LIST
        // ═══════════════════════════════════════════════════════════════

        private Button MakeFilterBtn(string text, bool active)
        {
            var btn = new Button();
            btn.Text = text;
            btn.ToggleMode = true;
            btn.ButtonPressed = active;
            btn.AddThemeFontSizeOverride("font_size", EditorStyles.FontTiny);
            btn.CustomMinimumSize = new Vector2(36, 22);
            string filter = text;
            btn.Pressed += () => SetTierFilter(filter);
            return btn;
        }

        private void SetTierFilter(string filter)
        {
            _tierFilter = filter;

            // Update toggle visuals
            _filterAll.ButtonPressed = filter == "All";
            _filterNormal.ButtonPressed = filter == "Normal";
            _filterElite.ButtonPressed = filter == "Elite";
            _filterMiniBoss.ButtonPressed = filter == "Mini";
            _filterBoss.ButtonPressed = filter == "Boss";

            RebuildCombatantList();
        }

        private void RebuildCombatantList()
        {
            foreach (var child in _combatantListContainer.GetChildren())
                if (child is Node n) n.QueueFree();

            EnemyRegistry.Initialize();
            string search = _filterEdit?.Text?.ToLower() ?? "";

            // Collect and sort: bosses first, then by tier, then alphabetical
            var entries = new List<(string id, EnemyData data, bool isBoss)>();

            foreach (var kvp in EnemyRegistry.Enemies)
            {
                var data = kvp.Value;
                bool isBoss = BossIds.Contains(kvp.Key);

                // Tier filter
                if (_tierFilter != "All")
                {
                    if (_tierFilter == "Boss" && !isBoss) continue;
                    if (_tierFilter == "Mini" && data.Tier != EnemyTier.MiniBoss) continue;
                    if (_tierFilter == "Elite" && data.Tier != EnemyTier.Elite) continue;
                    if (_tierFilter == "Normal" && (data.Tier != EnemyTier.Normal || isBoss)) continue;
                }

                // Search filter
                if (!string.IsNullOrEmpty(search))
                {
                    if (!kvp.Key.Contains(search) && !(data.EnemyName?.ToLower().Contains(search) ?? false))
                        continue;
                }

                entries.Add((kvp.Key, data, isBoss));
            }

            // Add bosses that aren't in EnemyRegistry
            foreach (var bossId in BossIds)
            {
                if (entries.Any(e => e.id == bossId)) continue;
                if (_tierFilter != "All" && _tierFilter != "Boss") continue;
                if (!string.IsNullOrEmpty(search) && !bossId.Contains(search)) continue;
                entries.Add((bossId, null, true));
            }

            // Sort: bosses first, then elites, then normals
            entries.Sort((a, b) =>
            {
                int tierA = a.isBoss ? 3 : (a.data?.Tier == EnemyTier.MiniBoss ? 2 : (a.data?.Tier == EnemyTier.Elite ? 1 : 0));
                int tierB = b.isBoss ? 3 : (b.data?.Tier == EnemyTier.MiniBoss ? 2 : (b.data?.Tier == EnemyTier.Elite ? 1 : 0));
                if (tierA != tierB) return tierB.CompareTo(tierA);
                return string.Compare(a.id, b.id, StringComparison.Ordinal);
            });

            foreach (var (id, data, isBoss) in entries)
            {
                var btn = new Button();
                btn.Alignment = HorizontalAlignment.Left;
                btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                btn.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);

                // Color-code by tier
                Color labelColor;
                string prefix;
                if (isBoss)
                {
                    labelColor = AccentColor;
                    prefix = "[BOSS] ";
                }
                else if (data?.Tier == EnemyTier.MiniBoss)
                {
                    labelColor = new Color(1f, 0.5f, 0.2f);
                    prefix = "[MINI] ";
                }
                else if (data?.Tier == EnemyTier.Elite)
                {
                    labelColor = new Color(0.9f, 0.7f, 0.2f);
                    prefix = "[ELITE] ";
                }
                else
                {
                    labelColor = EditorStyles.TextPrimary;
                    prefix = "";
                }

                string displayName = data?.EnemyName ?? FormatId(id);
                btn.Text = $"{prefix}{displayName}";
                btn.AddThemeColorOverride("font_color", labelColor);

                // Highlight if selected
                if (id == _currentId)
                {
                    var pressedStyle = EditorStyles.MakeFlat(EditorStyles.BgSelected);
                    btn.AddThemeStyleboxOverride("normal", pressedStyle);
                }

                // Has config overrides indicator
                if (_config != null && _config.ContainsKey(id))
                    btn.Text += " *";

                string capturedId = id;
                btn.Pressed += () => SelectCombatant(capturedId);
                _combatantListContainer.AddChild(btn);
            }
        }

        private void SelectCombatant(string id)
        {
            _currentId = id;
            LoadModel();
            LoadValues();
            UpdateFieldVisibility();
            RebuildCombatantList();

            var data = EnemyRegistry.GetEnemy(id);
            string displayName = data?.EnemyName ?? FormatId(id);
            bool isBoss = BossIds.Contains(id);
            _selectedNameLabel.Text = isBoss ? $"[BOSS] {displayName}" : displayName;
        }

        private void UpdateFieldVisibility()
        {
            bool isBoss = BossIds.Contains(_currentId);

            // Boss fields only for bosses
            _bossFieldsContainer.Visible = isBoss;

            // Enemy fields for all (bosses share base enemy data)
            _enemyFieldsContainer.Visible = true;

            // Ability list is most useful for bosses but visible for all
            if (_abilityListContainer.GetParent()?.GetParent() is Control abilitySection)
                abilitySection.Visible = true;
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
                    _cameraRadius = Mathf.Min(20f, _cameraRadius + 0.5f);
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
                _lastMousePos = mm.Position;
                UpdateCameraOrbit();
                _viewportContainer.AcceptEvent();
            }
        }

        private void UpdateCameraOrbit()
        {
            if (_camera == null) return;
            _camera.Position = new Vector3(
                Mathf.Sin(_cameraAngle) * _cameraRadius,
                _cameraHeight,
                Mathf.Cos(_cameraAngle) * _cameraRadius);
            if (_camera.IsInsideTree())
                _camera.LookAt(new Vector3(0, _cameraHeight * 0.4f, 0), Vector3.Up);
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

        private void CycleState(int dir)
        {
            _previewState = (_previewState + dir + StateNames.Length) % StateNames.Length;
            _stateLabel.Text = StateNames[_previewState];
            ApplyPreviewAnimation();
        }

        private void ApplyPreviewAnimation()
        {
            IAnimatable anim = null;
            foreach (var child in _modelRoot.GetChildren())
            {
                if (child is Node n)
                {
                    foreach (var sub in n.GetChildren())
                    {
                        if (sub is IAnimatable a) { anim = a; break; }
                    }
                }
                if (anim != null) break;
            }

            if (anim == null) return;

            var state = _previewState switch
            {
                0 => AnimState.Idle,
                1 => AnimState.Idle,
                2 => AnimState.Run,
                3 => AnimState.Attack,
                4 => AnimState.Attack,
                5 => AnimState.Hit,
                6 => AnimState.Idle,
                7 => AnimState.Death,
                _ => AnimState.Idle
            };

            anim.SetState(state);
        }

        // ═══════════════════════════════════════════════════════════════
        //  MODEL LOADING
        // ═══════════════════════════════════════════════════════════════

        private void LoadModel()
        {
            if (_modelRoot == null) return;

            foreach (var child in _modelRoot.GetChildren())
                if (child is Node n) n.QueueFree();

            Node3D body;
            if (_currentId == "axis_avatar")
            {
                body = AxisBossBody.Build();
                _cameraRadius = 15f;
                _cameraHeight = 6f;
            }
            else
            {
                body = CharacterMeshBuilder.BuildEnemyBody(_currentId);
                // Scale camera based on enemy size
                var data = EnemyRegistry.GetEnemy(_currentId);
                bool isBoss = BossIds.Contains(_currentId);
                _cameraRadius = isBoss ? 8f : 5f;
                _cameraHeight = isBoss ? 4f : 2.5f;
            }

            if (body != null)
            {
                _modelRoot.AddChild(body);

                var animPlayer = CharacterMeshBuilder.FindAnimationPlayer(body);
                if (animPlayer == null)
                {
                    var procAnim = new ProceduralAnimator();
                    procAnim.Name = "ProceduralAnimator";
                    body.AddChild(procAnim);
                    procAnim.Initialize(body);
                }
            }

            UpdateCameraOrbit();
        }

        // ═══════════════════════════════════════════════════════════════
        //  ABILITY LIST (bosses)
        // ═══════════════════════════════════════════════════════════════

        private void RebuildAbilityList()
        {
            foreach (var child in _abilityListContainer.GetChildren())
                if (child is Node n) n.QueueFree();

            if (!BossIds.Contains(_currentId)) return;

            var config = GetCurrentBossConfig();
            if (config == null) return;

            for (int i = 0; i < config.Abilities.Count; i++)
            {
                var ability = config.Abilities[i];
                int capturedIndex = i;

                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 4);

                var color = ability switch
                {
                    BossAbilityType.GroundSlam => new Color(0.9f, 0.6f, 0.2f),
                    BossAbilityType.ChargeAttack => new Color(1f, 0.4f, 0.3f),
                    BossAbilityType.ProjectileBarrage => new Color(0.6f, 0.3f, 1f),
                    BossAbilityType.SummonAdds => new Color(0.4f, 0.8f, 0.3f),
                    _ => EditorStyles.TextPrimary
                };

                var label = EditorStyles.MakeLabel(ability.ToString(), EditorStyles.FontSmall, color);
                label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                row.AddChild(label);

                var removeBtn = EditorStyles.MakeButton("X", EditorStyles.FontTiny, new Color(0.8f, 0.2f, 0.2f));
                removeBtn.CustomMinimumSize = new Vector2(24, 22);
                removeBtn.Pressed += () => RemoveAbility(capturedIndex);
                row.AddChild(removeBtn);

                _abilityListContainer.AddChild(row);
            }
        }

        private void ShowAddAbilityPopup()
        {
            if (!BossIds.Contains(_currentId)) return;
            var config = GetCurrentBossConfig();
            if (config == null) return;

            var allAbilities = Enum.GetValues<BossAbilityType>();
            foreach (var ability in allAbilities)
            {
                if (!config.Abilities.Contains(ability))
                {
                    config.Abilities.Add(ability);
                    RebuildAbilityList();
                    MarkDirty();
                    return;
                }
            }

            if (allAbilities.Length > 0)
            {
                config.Abilities.Add(allAbilities[0]);
                RebuildAbilityList();
                MarkDirty();
            }
        }

        private void RemoveAbility(int index)
        {
            var config = GetCurrentBossConfig();
            if (config == null || index < 0 || index >= config.Abilities.Count) return;
            config.Abilities.RemoveAt(index);
            RebuildAbilityList();
            MarkDirty();
        }

        // ═══════════════════════════════════════════════════════════════
        //  STATS INSPECTOR
        // ═══════════════════════════════════════════════════════════════

        private Label MakeRowLabel(string text)
        {
            var lbl = EditorStyles.MakeLabel(text, EditorStyles.FontTiny, EditorStyles.TextMuted);
            lbl.CustomMinimumSize = new Vector2(100, 0);
            return lbl;
        }

        private SpinBox AddStatRow(VBoxContainer parent, string label, double min, double max, double step)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 4);

            row.AddChild(MakeRowLabel(label));

            var spin = new SpinBox();
            spin.MinValue = min;
            spin.MaxValue = max;
            spin.Step = step;
            spin.CustomMinimumSize = new Vector2(80, 0);
            spin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            spin.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
            spin.Rounded = false;
            spin.ValueChanged += _ => { if (!_suppressSpinEvents) MarkDirty(); };
            row.AddChild(spin);

            parent.AddChild(row);
            return spin;
        }

        private void LoadValues()
        {
            _suppressSpinEvents = true;

            var overrides = GetOverrides();
            var enemyData = EnemyRegistry.GetEnemy(_currentId);
            bool isBoss = BossIds.Contains(_currentId);

            // Base stats
            _healthSpin.Value = GetOverrideFloat(overrides, "BaseHealth", enemyData?.BaseHealth ?? (isBoss ? 200 : 30));
            _damageSpin.Value = GetOverrideFloat(overrides, "BaseDamage", enemyData?.BaseDamage ?? 10);
            _speedSpin.Value = GetOverrideFloat(overrides, "MoveSpeed", enemyData?.MoveSpeed ?? 3);
            _armorSpin.Value = GetOverrideFloat(overrides, "Armor", enemyData?.Armor ?? 0);
            _attackRangeSpin.Value = GetOverrideFloat(overrides, "AttackRange", enemyData?.AttackRange ?? 2);
            _cooldownSpin.Value = GetOverrideFloat(overrides, "AttackCooldown", enemyData?.AttackCooldown ?? 1.5f);
            _aggroSpin.Value = GetOverrideFloat(overrides, "AggroRange", enemyData?.AggroRange ?? 10);
            _xpSpin.Value = GetOverrideFloat(overrides, "XpReward", enemyData?.XpReward ?? 20);

            // Enemy-specific
            if (enemyData != null)
            {
                int tierIdx = (int)(overrides != null && overrides.ContainsKey("Tier")
                    ? Enum.Parse<EnemyTier>(overrides["Tier"].ToString())
                    : enemyData.Tier);
                _tierPicker.Selected = Mathf.Clamp(tierIdx, 0, _tierPicker.ItemCount - 1);

                int behaviorIdx = (int)(overrides != null && overrides.ContainsKey("Behavior")
                    ? Enum.Parse<EnemyBehavior>(overrides["Behavior"].ToString())
                    : enemyData.Behavior);
                _behaviorPicker.Selected = Mathf.Clamp(behaviorIdx, 0, _behaviorPicker.ItemCount - 1);

                _meshColorPicker.Color = enemyData.MeshColor;
                _sigDropChanceSpin.Value = GetOverrideFloat(overrides, "SignatureDropChance", enemyData.SignatureDropChance);
                _lootBoxChanceSpin.Value = GetOverrideFloat(overrides, "LootBoxDropChance", enemyData.LootBoxDropChance);
            }

            // Boss-specific
            if (isBoss)
            {
                BossRegistry.Initialize();
                var bossConfig = BossRegistry.GetConfig(_currentId);
                _phase2Spin.Value = GetOverrideFloat(overrides, "Phase2Threshold", bossConfig?.Phase2Threshold ?? 0.6f);
                _phase3Spin.Value = GetOverrideFloat(overrides, "Phase3Threshold", bossConfig?.Phase3Threshold ?? 0.3f);

                _slamRadius.Value = GetOverrideFloat(overrides, "SlamRadius", 4f);
                _slamKnockback.Value = GetOverrideFloat(overrides, "SlamKnockback", 8f);
                _slamMult.Value = GetOverrideFloat(overrides, "SlamDamageMult", 1.5f);

                _chargeSpeedMult.Value = GetOverrideFloat(overrides, "ChargeSpeedMult", 3f);
                _chargeKnockback.Value = GetOverrideFloat(overrides, "ChargeKnockback", 10f);
                _chargeMult.Value = GetOverrideFloat(overrides, "ChargeDamageMult", 2f);

                _barrageCount.Value = GetOverrideFloat(overrides, "BarrageCount", 3);
                _barrageSpeed.Value = GetOverrideFloat(overrides, "BarrageSpeed", 12);
                _barrageRange.Value = GetOverrideFloat(overrides, "BarrageRange", 20);
                _barrageSpread.Value = GetOverrideFloat(overrides, "BarrageSpread", 15);

                _summonCount.Value = GetOverrideFloat(overrides, "SummonCount", 2);

                RebuildAbilityList();
            }

            _suppressSpinEvents = false;
        }

        // ═══════════════════════════════════════════════════════════════
        //  CONFIG HELPERS
        // ═══════════════════════════════════════════════════════════════

        private BossConfig GetCurrentBossConfig()
        {
            BossRegistry.Initialize();
            return BossRegistry.GetConfig(_currentId);
        }

        private Dictionary<string, object> GetOverrides()
        {
            if (_config == null || _currentId == null) return null;
            if (!_config.TryGetValue(_currentId, out var obj)) return null;
            return obj as Dictionary<string, object>;
        }

        private static float GetOverrideFloat(Dictionary<string, object> overrides, string key, float fallback)
        {
            if (overrides != null && overrides.TryGetValue(key, out var val))
                return Convert.ToSingle(val);
            return fallback;
        }

        private static string FormatId(string id) =>
            string.Join(" ", id.Split('_').Select(w => w.Length > 0 ? char.ToUpper(w[0]) + w[1..] : w));

        // ═══════════════════════════════════════════════════════════════
        //  SAVE / LOAD
        // ═══════════════════════════════════════════════════════════════

        protected override void Reload()
        {
            _config = LoadJson(CONFIG_PATH) ?? new Dictionary<string, object>();
            EnemyRegistry.Initialize();
            if (_currentId == null)
                _currentId = BossIds[0];
            LoadModel();
            LoadValues();
            UpdateFieldVisibility();
            RebuildCombatantList();
            MarkClean();
        }

        protected override void Save()
        {
            if (_config == null)
                _config = new Dictionary<string, object>();

            var data = new Dictionary<string, object>
            {
                ["BaseHealth"] = _healthSpin.Value,
                ["BaseDamage"] = _damageSpin.Value,
                ["MoveSpeed"] = _speedSpin.Value,
                ["Armor"] = _armorSpin.Value,
                ["AttackRange"] = _attackRangeSpin.Value,
                ["AttackCooldown"] = _cooldownSpin.Value,
                ["AggroRange"] = _aggroSpin.Value,
                ["XpReward"] = _xpSpin.Value,

                // Enemy fields
                ["Tier"] = _tierPicker.GetItemText(_tierPicker.Selected),
                ["Behavior"] = _behaviorPicker.GetItemText(_behaviorPicker.Selected),
                ["MeshColor"] = $"{_meshColorPicker.Color.R:F3},{_meshColorPicker.Color.G:F3},{_meshColorPicker.Color.B:F3}",
                ["SignatureDropChance"] = _sigDropChanceSpin.Value,
                ["LootBoxDropChance"] = _lootBoxChanceSpin.Value,
            };

            // Boss-specific
            if (BossIds.Contains(_currentId))
            {
                data["Phase2Threshold"] = _phase2Spin.Value;
                data["Phase3Threshold"] = _phase3Spin.Value;

                data["SlamRadius"] = _slamRadius.Value;
                data["SlamKnockback"] = _slamKnockback.Value;
                data["SlamDamageMult"] = _slamMult.Value;

                data["ChargeSpeedMult"] = _chargeSpeedMult.Value;
                data["ChargeKnockback"] = _chargeKnockback.Value;
                data["ChargeDamageMult"] = _chargeMult.Value;

                data["BarrageCount"] = _barrageCount.Value;
                data["BarrageSpeed"] = _barrageSpeed.Value;
                data["BarrageRange"] = _barrageRange.Value;
                data["BarrageSpread"] = _barrageSpread.Value;

                data["SummonCount"] = _summonCount.Value;

                var config = GetCurrentBossConfig();
                if (config != null)
                {
                    var abilityList = new List<object>();
                    foreach (var ab in config.Abilities)
                        abilityList.Add(ab.ToString());
                    data["Abilities"] = abilityList;
                }
            }

            _config[_currentId] = data;

            if (SaveJson(CONFIG_PATH, _config))
            {
                MarkClean();
                SetStatus($"Saved {_currentId}", EditorStyles.StatusSaved);
            }
            else
            {
                SetStatus("Save failed!", EditorStyles.StatusError);
            }
        }

        protected override void RestoreSnapshot(string jsonSnapshot) { }
    }
}
