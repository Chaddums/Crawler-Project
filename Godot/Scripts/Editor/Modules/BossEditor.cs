using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JunkbotArena.Editor
{
    /// <summary>
    /// Boss Editor — preview boss models, tweak stats, abilities, phase thresholds,
    /// and attack parameters. Saves overrides to Data/boss_config.json.
    /// 3-panel layout: left (boss selector + ability list), center (3D viewport),
    /// right (stats inspector + phase config + attack tuning).
    /// </summary>
    public partial class BossEditor : EditorPanel
    {
        public override string PanelName => "Bosses";
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

        // Selectors
        private Label _bossNameLabel;
        private Label _stateLabel;
        private VBoxContainer _abilityListContainer;

        // Stats inspector
        private SpinBox _healthSpin, _damageSpin, _speedSpin, _armorSpin;
        private SpinBox _attackRangeSpin, _cooldownSpin, _aggroSpin, _xpSpin;

        // Phase config
        private SpinBox _phase2Spin, _phase3Spin;

        // Attack tuning
        private SpinBox _slamRadius, _slamKnockback, _slamMult;
        private SpinBox _chargeSpeedMult, _chargeKnockback, _chargeMult;
        private SpinBox _barrageCount, _barrageSpeed, _barrageRange, _barrageSpread;
        private SpinBox _summonCount;

        // State
        private int _bossIndex;
        private string _currentBossId;
        private int _previewState; // index into BossState for preview animation
        private bool _suppressSpinEvents;
        private Dictionary<string, object> _config;

        private static readonly string[] BossIds =
        {
            "corrupted_sentry", "rust_titan", "scrap_hydra", "null_warden", "axis_avatar"
        };

        private static readonly string[] BossDisplayNames =
        {
            "Corrupted Sentry", "Rust Titan", "Scrap Hydra", "Null Warden", "AXIS Avatar"
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

            // ═══ LEFT PANEL: boss selector + abilities ═══
            var leftPanel = new VBoxContainer();
            leftPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            leftPanel.CustomMinimumSize = new Vector2(240, 0);

            // Boss selector
            leftPanel.AddChild(EditorStyles.MakeLabel("Boss", EditorStyles.FontHeader, AccentColor));
            leftPanel.AddChild(BuildCycler(
                () => BossDisplayNames[_bossIndex],
                dir => CycleBoss(dir),
                out _bossNameLabel));

            leftPanel.AddChild(EditorStyles.MakeSeparator());

            // Animation state preview
            leftPanel.AddChild(EditorStyles.MakeLabel("Preview State", EditorStyles.FontSmall, EditorStyles.TextSecondary));
            leftPanel.AddChild(BuildCycler(
                () => StateNames[_previewState],
                dir => CycleState(dir),
                out _stateLabel));

            leftPanel.AddChild(EditorStyles.MakeSeparator());

            // Auto-rotate toggle
            var rotateCheck = new CheckBox();
            rotateCheck.Text = "Auto-Rotate";
            rotateCheck.ButtonPressed = true;
            rotateCheck.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
            rotateCheck.Toggled += v => _autoRotate = v;
            leftPanel.AddChild(rotateCheck);

            leftPanel.AddChild(EditorStyles.MakeSeparator());

            // Abilities list
            leftPanel.AddChild(EditorStyles.MakeLabel("Abilities", EditorStyles.FontHeader, EditorStyles.TextSecondary));

            var abilityScroll = new ScrollContainer();
            abilityScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            _abilityListContainer = new VBoxContainer();
            _abilityListContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            abilityScroll.AddChild(_abilityListContainer);
            leftPanel.AddChild(abilityScroll);

            // Add ability button
            var addAbilityBtn = EditorStyles.MakeButton("+ Add Ability", EditorStyles.FontSmall, new Color(0.3f, 0.8f, 0.4f));
            addAbilityBtn.Pressed += ShowAddAbilityPopup;
            leftPanel.AddChild(addAbilityBtn);

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
            ground.Mesh = new PlaneMesh { Size = new Vector2(20, 20) };
            ground.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.06f, 0.06f, 0.08f) };
            _viewport.AddChild(ground);

            // Lighting
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
            split.AddChild(centerPanel);

            // ═══ RIGHT PANEL: stats + phases + attack tuning ═══
            var rightScroll = new ScrollContainer();
            rightScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            rightScroll.CustomMinimumSize = new Vector2(280, 0);

            var rightPanel = new VBoxContainer();
            rightPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            // --- Base Stats ---
            rightPanel.AddChild(EditorStyles.MakeLabel("Base Stats", EditorStyles.FontHeader, AccentColor));

            _healthSpin = AddStatRow(rightPanel, "Health", 50, 5000, 10);
            _damageSpin = AddStatRow(rightPanel, "Damage", 1, 200, 1);
            _speedSpin = AddStatRow(rightPanel, "Move Speed", 0, 20, 0.1);
            _armorSpin = AddStatRow(rightPanel, "Armor", 0, 50, 1);
            _attackRangeSpin = AddStatRow(rightPanel, "Attack Range", 1, 50, 0.5);
            _cooldownSpin = AddStatRow(rightPanel, "Atk Cooldown", 0.2, 10, 0.1);
            _aggroSpin = AddStatRow(rightPanel, "Aggro Range", 5, 50, 1);
            _xpSpin = AddStatRow(rightPanel, "XP Reward", 10, 2000, 10);

            rightPanel.AddChild(EditorStyles.MakeSeparator());

            // --- Phase Thresholds ---
            rightPanel.AddChild(EditorStyles.MakeLabel("Phase Thresholds", EditorStyles.FontHeader, AccentColor));
            _phase2Spin = AddStatRow(rightPanel, "Phase 2 (%HP)", 0.1, 0.9, 0.05);
            _phase3Spin = AddStatRow(rightPanel, "Phase 3 (%HP)", 0.05, 0.8, 0.05);

            rightPanel.AddChild(EditorStyles.MakeSeparator());

            // --- Ground Slam ---
            rightPanel.AddChild(EditorStyles.MakeLabel("Ground Slam", EditorStyles.FontSmall, new Color(0.9f, 0.6f, 0.2f)));
            _slamRadius = AddStatRow(rightPanel, "AoE Radius", 1, 20, 0.5);
            _slamKnockback = AddStatRow(rightPanel, "Knockback", 1, 30, 1);
            _slamMult = AddStatRow(rightPanel, "Damage Mult", 0.5, 5, 0.1);

            rightPanel.AddChild(EditorStyles.MakeSeparator());

            // --- Charge Attack ---
            rightPanel.AddChild(EditorStyles.MakeLabel("Charge Attack", EditorStyles.FontSmall, new Color(1f, 0.4f, 0.3f)));
            _chargeSpeedMult = AddStatRow(rightPanel, "Speed Mult", 1, 10, 0.5);
            _chargeKnockback = AddStatRow(rightPanel, "Knockback", 1, 30, 1);
            _chargeMult = AddStatRow(rightPanel, "Damage Mult", 0.5, 5, 0.1);

            rightPanel.AddChild(EditorStyles.MakeSeparator());

            // --- Projectile Barrage ---
            rightPanel.AddChild(EditorStyles.MakeLabel("Projectile Barrage", EditorStyles.FontSmall, new Color(0.6f, 0.3f, 1f)));
            _barrageCount = AddStatRow(rightPanel, "Projectiles", 1, 20, 1);
            _barrageSpeed = AddStatRow(rightPanel, "Speed", 2, 40, 1);
            _barrageRange = AddStatRow(rightPanel, "Range", 5, 50, 1);
            _barrageSpread = AddStatRow(rightPanel, "Spread (deg)", 5, 90, 5);

            rightPanel.AddChild(EditorStyles.MakeSeparator());

            // --- Summon Adds ---
            rightPanel.AddChild(EditorStyles.MakeLabel("Summon Adds", EditorStyles.FontSmall, new Color(0.4f, 0.8f, 0.3f)));
            _summonCount = AddStatRow(rightPanel, "Add Count", 1, 10, 1);

            rightScroll.AddChild(rightPanel);
            split.AddChild(rightScroll);
            content.AddChild(split);
        }

        public override void _Ready()
        {
            base._Ready();
            _currentBossId = BossIds[0];
            CallDeferred(nameof(LoadModel));
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

        // ── Viewport input ──

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

        // ── Camera ──

        private void UpdateCameraOrbit()
        {
            if (_camera == null) return;
            _camera.Position = new Vector3(
                Mathf.Sin(_cameraAngle) * _cameraRadius,
                _cameraHeight,
                Mathf.Cos(_cameraAngle) * _cameraRadius);
            _camera.LookAt(new Vector3(0, _cameraHeight * 0.4f, 0), Vector3.Up);
        }

        // ── Cyclers ──

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

        private void CycleBoss(int dir)
        {
            _bossIndex = (_bossIndex + dir + BossIds.Length) % BossIds.Length;
            _currentBossId = BossIds[_bossIndex];
            _bossNameLabel.Text = BossDisplayNames[_bossIndex];
            LoadModel();
            LoadBossValues();
        }

        private void CycleState(int dir)
        {
            _previewState = (_previewState + dir + StateNames.Length) % StateNames.Length;
            _stateLabel.Text = StateNames[_previewState];
            ApplyPreviewAnimation();
        }

        private void ApplyPreviewAnimation()
        {
            // Find ProceduralAnimator or CharacterAnimator on the model
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
                1 => AnimState.Idle,     // Intro = idle visually
                2 => AnimState.Run,      // Chase
                3 => AnimState.Attack,
                4 => AnimState.Attack,   // Special
                5 => AnimState.Hit,      // Stunned
                6 => AnimState.Idle,     // Phase transition
                7 => AnimState.Death,
                _ => AnimState.Idle
            };

            anim.SetState(state);
        }

        // ── Model Loading ──

        private void LoadModel()
        {
            if (_modelRoot == null) return;

            foreach (var child in _modelRoot.GetChildren())
                if (child is Node n) n.QueueFree();

            // Build the boss body
            Node3D body;
            if (_currentBossId == "axis_avatar")
            {
                body = AxisBossBody.Build();
                // AXIS is huge — zoom out and raise camera
                _cameraRadius = 15f;
                _cameraHeight = 6f;
            }
            else
            {
                body = CharacterMeshBuilder.BuildEnemyBody(_currentBossId);
                _cameraRadius = 6f;
                _cameraHeight = 3f;
            }

            if (body != null)
            {
                _modelRoot.AddChild(body);

                // Add ProceduralAnimator for preview
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

        // ── Ability List ──

        private void RebuildAbilityList()
        {
            foreach (var child in _abilityListContainer.GetChildren())
                if (child is Node n) n.QueueFree();

            var config = GetCurrentConfig();
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
            var config = GetCurrentConfig();
            if (config == null) return;

            // Cycle through available abilities, add the next one not yet in the list
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

            // All abilities already added — add a duplicate of the first
            if (allAbilities.Length > 0)
            {
                config.Abilities.Add(allAbilities[0]);
                RebuildAbilityList();
                MarkDirty();
            }
        }

        private void RemoveAbility(int index)
        {
            var config = GetCurrentConfig();
            if (config == null || index < 0 || index >= config.Abilities.Count) return;

            config.Abilities.RemoveAt(index);
            RebuildAbilityList();
            MarkDirty();
        }

        // ── Stats Inspector ──

        private SpinBox AddStatRow(VBoxContainer parent, string label, double min, double max, double step)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 4);

            var lbl = EditorStyles.MakeLabel(label, EditorStyles.FontTiny, EditorStyles.TextMuted);
            lbl.CustomMinimumSize = new Vector2(100, 0);
            row.AddChild(lbl);

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

        private void LoadBossValues()
        {
            _suppressSpinEvents = true;

            // Load from config override first, then fall back to registry
            var overrides = GetBossOverrides();

            // Enemy data
            BossRegistry.Initialize();
            EnemyRegistry.Initialize();
            var enemyData = EnemyRegistry.GetEnemy(_currentBossId);

            float health = GetOverrideFloat(overrides, "BaseHealth", enemyData?.BaseHealth ?? 200);
            float damage = GetOverrideFloat(overrides, "BaseDamage", enemyData?.BaseDamage ?? 10);
            float speed = GetOverrideFloat(overrides, "MoveSpeed", enemyData?.MoveSpeed ?? 3);
            float armor = GetOverrideFloat(overrides, "Armor", enemyData?.Armor ?? 0);
            float atkRange = GetOverrideFloat(overrides, "AttackRange", enemyData?.AttackRange ?? 2);
            float cooldown = GetOverrideFloat(overrides, "AttackCooldown", enemyData?.AttackCooldown ?? 1.5f);
            float aggro = GetOverrideFloat(overrides, "AggroRange", enemyData?.AggroRange ?? 10);
            float xp = GetOverrideFloat(overrides, "XpReward", enemyData?.XpReward ?? 100);

            _healthSpin.Value = health;
            _damageSpin.Value = damage;
            _speedSpin.Value = speed;
            _armorSpin.Value = armor;
            _attackRangeSpin.Value = atkRange;
            _cooldownSpin.Value = cooldown;
            _aggroSpin.Value = aggro;
            _xpSpin.Value = xp;

            // Phase thresholds
            var bossConfig = BossRegistry.GetConfig(_currentBossId);
            float p2 = GetOverrideFloat(overrides, "Phase2Threshold", bossConfig?.Phase2Threshold ?? 0.6f);
            float p3 = GetOverrideFloat(overrides, "Phase3Threshold", bossConfig?.Phase3Threshold ?? 0.3f);
            _phase2Spin.Value = p2;
            _phase3Spin.Value = p3;

            // Attack params (defaults from code)
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

            _suppressSpinEvents = false;

            RebuildAbilityList();
        }

        // ── Config helpers ──

        private BossConfig GetCurrentConfig()
        {
            BossRegistry.Initialize();
            return BossRegistry.GetConfig(_currentBossId);
        }

        private Dictionary<string, object> GetBossOverrides()
        {
            if (_config == null) return null;
            if (!_config.TryGetValue(_currentBossId, out var obj)) return null;
            return obj as Dictionary<string, object>;
        }

        private static float GetOverrideFloat(Dictionary<string, object> overrides, string key, float fallback)
        {
            if (overrides != null && overrides.TryGetValue(key, out var val))
                return Convert.ToSingle(val);
            return fallback;
        }

        // ── Save / Load ──

        protected override void Reload()
        {
            _config = LoadJson(CONFIG_PATH) ?? new Dictionary<string, object>();
            LoadModel();
            LoadBossValues();
            MarkClean();
        }

        protected override void Save()
        {
            if (_config == null)
                _config = new Dictionary<string, object>();

            var data = new Dictionary<string, object>
            {
                // Base stats
                ["BaseHealth"] = _healthSpin.Value,
                ["BaseDamage"] = _damageSpin.Value,
                ["MoveSpeed"] = _speedSpin.Value,
                ["Armor"] = _armorSpin.Value,
                ["AttackRange"] = _attackRangeSpin.Value,
                ["AttackCooldown"] = _cooldownSpin.Value,
                ["AggroRange"] = _aggroSpin.Value,
                ["XpReward"] = _xpSpin.Value,

                // Phase thresholds
                ["Phase2Threshold"] = _phase2Spin.Value,
                ["Phase3Threshold"] = _phase3Spin.Value,

                // Ground Slam
                ["SlamRadius"] = _slamRadius.Value,
                ["SlamKnockback"] = _slamKnockback.Value,
                ["SlamDamageMult"] = _slamMult.Value,

                // Charge Attack
                ["ChargeSpeedMult"] = _chargeSpeedMult.Value,
                ["ChargeKnockback"] = _chargeKnockback.Value,
                ["ChargeDamageMult"] = _chargeMult.Value,

                // Projectile Barrage
                ["BarrageCount"] = _barrageCount.Value,
                ["BarrageSpeed"] = _barrageSpeed.Value,
                ["BarrageRange"] = _barrageRange.Value,
                ["BarrageSpread"] = _barrageSpread.Value,

                // Summon Adds
                ["SummonCount"] = _summonCount.Value,
            };

            // Abilities list
            var config = GetCurrentConfig();
            if (config != null)
            {
                var abilityList = new List<object>();
                foreach (var ab in config.Abilities)
                    abilityList.Add(ab.ToString());
                data["Abilities"] = abilityList;
            }

            _config[_currentBossId] = data;

            if (SaveJson(CONFIG_PATH, _config))
            {
                MarkClean();
                SetStatus($"Saved {_currentBossId}", EditorStyles.StatusSaved);
            }
            else
            {
                SetStatus("Save failed!", EditorStyles.StatusError);
            }
        }

        protected override void RestoreSnapshot(string jsonSnapshot) { }
    }
}
