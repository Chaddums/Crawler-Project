using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Mining Building — the core objective and resource generator.
    /// Enemies attack it when they reach the exit. Toggles between Resources and Materials
    /// production. Loads the Mystical Watchtower GLB model with animated eye.
    /// </summary>
    public partial class VineHarvester : Node3D
    {
        public float MaxHP { get; private set; }
        public float CurrentHP { get; private set; }
        public bool IsDestroyed => CurrentHP <= 0;

        // ── Mining mode toggle ──
        public MiningMode CurrentMode { get; private set; } = MiningMode.Resources;
        public MaterialType SelectedMaterial { get; private set; } = MaterialType.None;
        public float MaterialsAccumulated { get; private set; }

        private MeshInstance3D _healthBar;
        private MeshInstance3D _healthBarBg;
        private float _incomeTimer;

        // Hit flash
        private float _flashTimer;
        private Node3D _modelRoot;

        // GLB model config per role
        private struct SpireModelConfig
        {
            public string Path;
            public float Scale;
            public float BurialDepth;
            public float RotationSpeed; // radians/sec, 0 = no rotation
        }

        private static SpireModelConfig GetModelConfig(string role) => role switch
        {
            "Arcanist" => new SpireModelConfig
            {
                Path = "res://Models/Spires/MysticalWatchtower.glb",
                Scale = 0.35f,
                BurialDepth = 0f,
                RotationSpeed = 0f // uses GLB animation instead
            },
            "Scrapwright" => new SpireModelConfig
            {
                Path = "res://Models/Spires/AlphaBlueblackTower.glb",
                Scale = 0.35f,
                BurialDepth = -2.8f,
                RotationSpeed = 0.15f
            },
            "Bruteforge" => new SpireModelConfig
            {
                Path = "res://Models/Spires/Driller.glb",
                Scale = 1.05f,
                BurialDepth = -0.6f,
                RotationSpeed = 0f
            },
            _ => new SpireModelConfig
            {
                Path = "res://Models/Spires/AlphaBlueblackTower.glb",
                Scale = 0.35f,
                BurialDepth = -2.8f,
                RotationSpeed = 0.15f
            }
        };

        private SpireModelConfig _config;
        private AnimationPlayer _animPlayer;
        private bool _animStarted;

        // ── Slam-in animation state ──
        private bool _slamming;
        private float _slamTimer;
        private float _slamDuration;
        private float _slamStartY;
        private float _slamTargetY;
        private bool _slamImpactFired;

        public override void _Ready()
        {
            MaxHP = Constants.VINE_HARVESTER_MAX_HP;
            CurrentHP = MaxHP;

            _config = GetModelConfig(GameManager.Instance?.SelectedRole ?? "Scrapwright");
            GD.Print($"[Spire] Role: {GameManager.Instance?.SelectedRole} → model: {_config.Path}");

            BuildVisual();
            BuildHealthBar();

            ServiceLocator.Register(this);
            GameEvents.OnHarvesterHPChanged?.Invoke(CurrentHP, MaxHP);
        }

        public override void _Process(double delta)
        {
            if (IsDestroyed) return;
            float dt = (float)delta;

            // ── Slam-in animation ──
            if (_slamming)
            {
                _slamTimer += dt;
                float t = Mathf.Clamp(_slamTimer / _slamDuration, 0f, 1f);

                // Ease-in (accelerate like gravity): t^2.5
                float eased = Mathf.Pow(t, 2.5f);
                float y = Mathf.Lerp(_slamStartY, _slamTargetY, eased);
                GlobalPosition = new Vector3(GlobalPosition.X, y, GlobalPosition.Z);

                // Hide model root until close to impact for dramatic reveal
                if (_modelRoot != null)
                    _modelRoot.Visible = t > 0.1f;

                // Impact moment
                if (t >= 1f && !_slamImpactFired)
                {
                    _slamImpactFired = true;
                    GlobalPosition = new Vector3(GlobalPosition.X, _slamTargetY, GlobalPosition.Z);
                    OnSlamImpact();
                }

                // Post-impact settle: slight bounce for 0.3s after landing
                if (_slamImpactFired)
                {
                    float postImpact = _slamTimer - _slamDuration;
                    if (postImpact < 0.4f)
                    {
                        float bounce = Mathf.Sin(postImpact * Mathf.Pi / 0.12f) * 0.3f * Mathf.Exp(-postImpact * 8f);
                        GlobalPosition = new Vector3(GlobalPosition.X, _slamTargetY + bounce, GlobalPosition.Z);
                    }
                    else
                    {
                        GlobalPosition = new Vector3(GlobalPosition.X, _slamTargetY, GlobalPosition.Z);
                        _slamming = false;

                        // Start GLB animation if model has one (e.g. Arcanist eye)
                        StartModelAnimation();
                    }
                }

                return; // Skip normal processing during slam
            }

            // Slow rotation (if configured for this model)
            if (_modelRoot != null && _config.RotationSpeed > 0f)
                _modelRoot.RotateY(_config.RotationSpeed * dt);

            // Resource generation based on mining mode
            _incomeTimer += dt;
            if (_incomeTimer >= Constants.VINE_HARVESTER_INCOME_INTERVAL)
            {
                _incomeTimer -= Constants.VINE_HARVESTER_INCOME_INTERVAL;
                if (CurrentMode == MiningMode.Resources)
                {
                    GameManager.Instance?.AddResources(
                        (int)(Constants.VINE_HARVESTER_INCOME * SignalTuningEditor.HarvesterIncomeMult)
                        + SignalTuningEditor.HarvesterIncomeBonus);
                }
                else if (CurrentMode == MiningMode.Materials && SelectedMaterial != MaterialType.None)
                {
                    float magicRate = Constants.VINE_HARVESTER_INCOME * SignalTuningEditor.HarvesterIncomeMult;
                    MaterialsAccumulated += magicRate;
                    GameEvents.OnMaterialsAccumulated?.Invoke(MaterialsAccumulated, SelectedMaterial);
                }
            }

            // Hit flash decay
            if (_flashTimer > 0)
            {
                _flashTimer -= dt;
                if (_flashTimer <= 0 && _modelRoot != null)
                    SetFlash(false);
            }

            UpdateHealthBar();
        }

        public void TakeDamage(float amount)
        {
            if (IsDestroyed) return;
            CurrentHP = Mathf.Max(0, CurrentHP - amount);

            // Flash
            _flashTimer = 0.15f;
            if (_modelRoot != null) SetFlash(true);

            // Screen shake
            if (ServiceLocator.TryGet<TDCamera>(out var cam))
                cam.Shake(0.5f + (1f - CurrentHP / MaxHP) * 0.8f, 0.3f);

            GameEvents.OnHarvesterDamaged?.Invoke(CurrentHP);
            GameEvents.OnHarvesterHPChanged?.Invoke(CurrentHP, MaxHP);

            if (IsDestroyed)
                OnDestroyed();
        }

        public void Heal(float amount)
        {
            if (IsDestroyed) return;
            CurrentHP = Mathf.Min(MaxHP, CurrentHP + amount);
            GameEvents.OnHarvesterHPChanged?.Invoke(CurrentHP, MaxHP);
        }

        private void OnDestroyed()
        {
            // Death VFX
            VfxFactory.SpawnDeathBurst(GetTree(), GlobalPosition, new Color(1f, 0.4f, 0.1f), 12);

            GameManager.Instance?.SetPhase(GamePhase.Defeat);
            GameEvents.OnCoreDestroyed?.Invoke();
        }

        // ── Slam-In Animation ──

        /// <summary>
        /// Start the slam-in animation. The Spire falls from dropHeight above its target
        /// position and slams down over the given duration.
        /// </summary>
        public void SlamIn(float dropHeight = 40f, float duration = 1.2f)
        {
            _slamTargetY = GlobalPosition.Y;
            _slamStartY = _slamTargetY + dropHeight;
            _slamDuration = duration;
            _slamTimer = 0f;
            _slamImpactFired = false;
            _slamming = true;

            // Start at the top
            GlobalPosition = new Vector3(GlobalPosition.X, _slamStartY, GlobalPosition.Z);

            // Hide the model root initially for dramatic reveal
            if (_modelRoot != null)
                _modelRoot.Visible = false;

            GD.Print("[Spire] Slam-in started — dropping from height " + dropHeight);
        }

        private void OnSlamImpact()
        {
            GD.Print("[Spire] IMPACT!");

            // Camera shake — big, dramatic
            if (ServiceLocator.TryGet<TDCamera>(out var cam))
                cam.Shake(2.5f, 0.6f);

            // Dust burst VFX — ring of debris expanding outward
            var tree = GetTree();
            var pos = GlobalPosition;

            // Large ground ring expansion
            VfxFactory.SpawnSplashRing(tree, pos, 4f, DamageType.Physical);

            // Death burst particles repurposed as impact debris
            VfxFactory.SpawnDeathBurst(tree, pos + new Vector3(0, 0.5f, 0),
                BitPalette.DirtColor, 16);

            // Secondary burst with accent color for energy discharge
            VfxFactory.SpawnDeathBurst(tree, pos + new Vector3(0, 1.5f, 0),
                BitPalette.AccentBright, 8);
        }

        // ── GLB Model Animation ──

        /// <summary>
        /// Find and play the watchtower's eye animation after slam impact.
        /// </summary>
        private void StartModelAnimation()
        {
            if (_animStarted || _animPlayer == null) return;
            _animStarted = true;

            var anims = _animPlayer.GetAnimationList();
            if (anims.Length > 0)
            {
                string animName = anims[0];
                // Set animation to loop
                var anim = _animPlayer.GetAnimation(animName);
                if (anim != null)
                    anim.LoopMode = Animation.LoopModeEnum.Linear;

                _animPlayer.Play(animName);
                GD.Print($"[Spire] Playing animation: {animName}");
            }
            else
            {
                GD.PrintErr("[Spire] No animations found in watchtower model");
            }
        }

        // ── Mining Mode Toggle ──

        /// <summary>
        /// Toggle between Resources and Materials production modes.
        /// Only works during Build phase. Materials mode requires a selected material type.
        /// </summary>
        public void ToggleMode()
        {
            if (IsDestroyed) return;
            var phase = GameManager.Instance?.CurrentPhase ?? GamePhase.Wave;
            if (phase != GamePhase.Build) return;

            if (CurrentMode == MiningMode.Resources && SelectedMaterial != MaterialType.None)
            {
                CurrentMode = MiningMode.Materials;
            }
            else
            {
                CurrentMode = MiningMode.Resources;
            }

            GameEvents.OnMiningModeChanged?.Invoke(CurrentMode);
            GD.Print($"[MiningBuilding] Mode → {CurrentMode}" +
                (CurrentMode == MiningMode.Materials ? $" ({SelectedMaterial})" : ""));
        }

        /// <summary>
        /// Select the material type for this mining building.
        /// </summary>
        public void SelectMaterialType(MaterialType type)
        {
            if (type == MaterialType.None) return;
            SelectedMaterial = type;
            GameEvents.OnMaterialTypeSelected?.Invoke(type);
            GD.Print($"[MiningBuilding] Material type selected: {type}");
        }

        /// <summary>
        /// Get the accent color for the current material type.
        /// </summary>
        public static Color GetMaterialColor(MaterialType type) => type switch
        {
            MaterialType.Chaos => new Color(0.7f, 0.2f, 0.9f),
            MaterialType.Power => new Color(1.0f, 0.7f, 0.1f),
            MaterialType.Environment => new Color(0.2f, 0.85f, 0.3f),
            _ => BitPalette.Accent
        };

        // ── Visual Build ──

        private void BuildVisual()
        {
            var scene = GD.Load<PackedScene>(_config.Path);
            if (scene == null)
            {
                GD.PrintErr($"[Spire] Failed to load model: {_config.Path}");
                BuildFallbackVisual();
                return;
            }

            _modelRoot = scene.Instantiate<Node3D>();
            _modelRoot.Scale = new Vector3(_config.Scale, _config.Scale, _config.Scale);
            _modelRoot.Position = new Vector3(0, 0.2f - _config.BurialDepth, 0);
            AddChild(_modelRoot);

            // Find the AnimationPlayer (created by GLB importer)
            _animPlayer = FindChild<AnimationPlayer>(_modelRoot);
            if (_animPlayer != null)
                GD.Print($"[Spire] Found AnimationPlayer with {_animPlayer.GetAnimationList().Length} animation(s)");
            else
                GD.Print("[Spire] No AnimationPlayer found in model");

            GD.Print("[Spire] Mystical Watchtower model loaded");
        }

        /// <summary>
        /// Fallback procedural visual if the GLB fails to load.
        /// </summary>
        private void BuildFallbackVisual()
        {
            _modelRoot = new Node3D();
            _modelRoot.Position = new Vector3(0, 0.4f, 0);
            AddChild(_modelRoot);

            // Simple pillar + orb as fallback
            var pillar = new MeshInstance3D();
            pillar.Mesh = new CylinderMesh
            {
                TopRadius = 0.3f, BottomRadius = 0.5f, Height = 4f, RadialSegments = 8
            };
            pillar.Position = new Vector3(0, 2f, 0);
            pillar.MaterialOverride = BitPalette.MakeSolidMaterial(0.15f);
            _modelRoot.AddChild(pillar);

            var orb = new MeshInstance3D();
            orb.Mesh = new SphereMesh { Radius = 0.5f, Height = 1f };
            orb.Position = new Vector3(0, 4.2f, 0);
            orb.MaterialOverride = BitPalette.MakeGlowMaterial(1f, 1.0f);
            _modelRoot.AddChild(orb);
        }

        /// <summary>
        /// Recursively find a child node of type T.
        /// </summary>
        private static T FindChild<T>(Node parent) where T : Node
        {
            foreach (var child in parent.GetChildren())
            {
                if (child is T found) return found;
                var deeper = FindChild<T>(child);
                if (deeper != null) return deeper;
            }
            return null;
        }

        private void BuildHealthBar()
        {
            // Position health bar above the model (model is ~15 units tall * 0.35 scale = ~5.25)
            float barWidth = 2.0f;
            float barY = 5.8f;

            // Background
            _healthBarBg = new MeshInstance3D();
            _healthBarBg.Mesh = new BoxMesh { Size = new Vector3(barWidth, 0.12f, 0.12f) };
            _healthBarBg.Position = new Vector3(0, barY, 0);
            var bgMat = new StandardMaterial3D();
            bgMat.AlbedoColor = new Color(0.15f, 0.15f, 0.15f);
            bgMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            _healthBarBg.MaterialOverride = bgMat;
            AddChild(_healthBarBg);

            // Foreground
            _healthBar = new MeshInstance3D();
            _healthBar.Mesh = new BoxMesh { Size = new Vector3(barWidth, 0.1f, 0.1f) };
            _healthBar.Position = new Vector3(0, barY, 0);
            var barMat = new StandardMaterial3D();
            barMat.AlbedoColor = new Color(0.1f, 0.9f, 0.1f);
            barMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            _healthBar.MaterialOverride = barMat;
            AddChild(_healthBar);

            // Label
            var label = new Label3D();
            label.Text = "MINING STATION";
            label.FontSize = 48;
            label.OutlineSize = 6;
            label.Modulate = BitPalette.Accent;
            label.Position = new Vector3(0, barY + 0.4f, 0);
            label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            AddChild(label);
        }

        private void UpdateHealthBar()
        {
            if (_healthBar == null) return;
            float pct = Mathf.Clamp(CurrentHP / MaxHP, 0f, 1f);
            float halfBar = 1.0f;
            _healthBar.Scale = new Vector3(pct, 1, 1);
            _healthBar.Position = new Vector3((pct - 1f) * halfBar, _healthBar.Position.Y, 0);

            if (_healthBar.MaterialOverride is StandardMaterial3D mat)
                mat.AlbedoColor = pct > 0.5f
                    ? new Color(0.1f, 0.9f, 0.1f)
                    : pct > 0.25f
                        ? new Color(0.9f, 0.7f, 0.1f)
                        : new Color(0.9f, 0.1f, 0.1f);
        }

        private void SetFlash(bool flash)
        {
            if (_modelRoot == null) return;
            SetFlashRecursive(_modelRoot, flash);
        }

        private void SetFlashRecursive(Node parent, bool flash)
        {
            foreach (var child in parent.GetChildren())
            {
                if (child is MeshInstance3D mesh)
                {
                    // For GLB models, materials may be on the mesh surface slots
                    var mat = mesh.MaterialOverride as StandardMaterial3D
                        ?? (mesh.GetSurfaceOverrideMaterialCount() > 0
                            ? mesh.GetSurfaceOverrideMaterial(0) as StandardMaterial3D
                            : null);

                    if (mat != null)
                    {
                        if (flash)
                        {
                            mat.EmissionEnabled = true;
                            mat.Emission = Colors.White;
                            mat.EmissionEnergyMultiplier = 1.2f;
                        }
                        else
                        {
                            mat.EmissionEnergyMultiplier = 0.0f;
                        }
                    }
                }
                if (child is Node node)
                    SetFlashRecursive(node, flash);
            }
        }

        public override void _ExitTree()
        {
            ServiceLocator.Unregister<VineHarvester>();
        }
    }
}
