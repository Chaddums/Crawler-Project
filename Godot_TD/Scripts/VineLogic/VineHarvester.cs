using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Tracked autocannon projectile traveling toward a target.
    /// </summary>
    public struct AutocannonProjectile
    {
        public MeshInstance3D Visual;
        public Vector3 Start;
        public Vector3 Target;
        public float Progress;
        public float Damage;
        public Node3D TargetNode;
    }

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

        // Loaded from Data/Spires/*.json via SpireData
        private SpireData _spireData;
        private AnimationPlayer _animPlayer;
        private bool _animStarted;

        // ── Shield (Arcanist) ──
        private float _shieldHP;
        private float _shieldMax;
        private float _shieldRechargeDelay;
        private float _shieldRechargeTimer;
        private bool _shieldBroken;
        private MeshInstance3D _shieldOrb;
        private StandardMaterial3D _shieldOrbMat;

        // ── Beam attack (Obelisk) ──
        private float _beamCooldownTimer;
        private float _beamVisualTimer;
        private MeshInstance3D _beamMesh;

        // ── Autocannons (Bruteforge) ──
        private int _autocannonCount;
        private float _autocannonFireRate;
        private float _autocannonDamage;
        private float _autocannonRange;
        private float[] _autocannonCooldowns;
        private MeshInstance3D[] _autocannonBarrels;
        private readonly List<AutocannonProjectile> _projectiles = new();

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

            string role = GameManager.Instance?.SelectedRole ?? "Obelisk";
            _spireData = SpireData.Get(role);
            if (_spireData == null)
            {
                GD.PrintErr($"[Spire] No SpireData found for role: {role}, falling back to Obelisk");
                _spireData = SpireData.Get("Obelisk");
            }
            if (_spireData != null)
            {
                MaxHP = _spireData.MaxHP;
                CurrentHP = MaxHP;

                // Shield (Arcanist)
                _shieldMax = _spireData.Shield;
                _shieldHP = _shieldMax;
                _shieldRechargeDelay = _spireData.ShieldRechargeDelay;
            }
            GD.Print($"[Spire] Role: {role} → {_spireData?.DisplayName} ({_spireData?.ModelPath})");

            BuildVisual();
            BuildHealthBar();
            if (_shieldMax > 0) BuildShieldVisual();

            // Autocannons (Bruteforge)
            if (_spireData != null && _spireData.AutocannonCount > 0)
            {
                _autocannonCount = _spireData.AutocannonCount;
                _autocannonFireRate = _spireData.AutocannonFireRate;
                _autocannonDamage = _spireData.AutocannonDamage;
                _autocannonRange = _spireData.AutocannonRange;
                _autocannonCooldowns = new float[_autocannonCount];
                BuildAutocannons();
            }

            ServiceLocator.Register(this);
            GameEvents.OnHarvesterHPChanged?.Invoke(CurrentHP, MaxHP);

            // Reset Null Shard each wave
            GameEvents.OnWaveStarted += _ => _nullShardUsedThisWave = false;
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
            if (_modelRoot != null && _spireData != null && _spireData.RotationSpeed > 0f)
                _modelRoot.RotateY(_spireData.RotationSpeed * dt);

            // ── Beam attack (Obelisk) ──
            if (_spireData is { HasBeamAttack: true })
            {
                var phase = GameManager.Instance?.CurrentPhase ?? GamePhase.Build;
                if (phase == GamePhase.Wave)
                    UpdateBeamAttack(dt);

                // Fade beam visual
                if (_beamVisualTimer > 0f)
                {
                    _beamVisualTimer -= dt;
                    if (_beamVisualTimer <= 0f && _beamMesh != null)
                    {
                        _beamMesh.QueueFree();
                        _beamMesh = null;
                    }
                    else if (_beamMesh?.MaterialOverride is StandardMaterial3D beamMat)
                    {
                        float alpha = Mathf.Clamp(_beamVisualTimer / _spireData.BeamDuration, 0f, 1f);
                        beamMat.AlbedoColor = new Color(beamMat.AlbedoColor.R, beamMat.AlbedoColor.G, beamMat.AlbedoColor.B, alpha);
                        beamMat.EmissionEnergyMultiplier = alpha * 2f;
                    }
                }
            }

            // ── Autocannons (Bruteforge) ──
            if (_autocannonCount > 0)
            {
                var phase2 = GameManager.Instance?.CurrentPhase ?? GamePhase.Build;
                if (phase2 == GamePhase.Wave)
                    UpdateAutocannons(dt);
                UpdateProjectiles(dt);
            }

            // ── HP regen ──
            if (_spireData is { HpRegenPerSec: > 0f } && CurrentHP < MaxHP && CurrentHP > 0)
            {
                CurrentHP = Mathf.Min(MaxHP, CurrentHP + _spireData.HpRegenPerSec * dt);
                GameEvents.OnHarvesterHPChanged?.Invoke(CurrentHP, MaxHP);
            }

            // ── Shield recharge (Arcanist) ──
            if (_shieldMax > 0 && _shieldBroken)
            {
                _shieldRechargeTimer -= dt;
                if (_shieldRechargeTimer <= 0f)
                {
                    _shieldHP = _shieldMax;
                    _shieldBroken = false;
                    UpdateShieldVisual();
                    GD.Print("[Spire] Shield recharged");
                }
            }

            // Resource generation based on mining mode
            _incomeTimer += dt;
            if (_incomeTimer >= Constants.VINE_HARVESTER_INCOME_INTERVAL)
            {
                _incomeTimer -= Constants.VINE_HARVESTER_INCOME_INTERVAL;
                if (CurrentMode == MiningMode.Resources)
                {
                    int baseIncome = (int)(Constants.VINE_HARVESTER_INCOME * SignalTuningEditor.HarvesterIncomeMult)
                        + SignalTuningEditor.HarvesterIncomeBonus;

                    // Bonus from captured resource nodes
                    if (ServiceLocator.TryGet<VineGrid>(out var grid))
                    {
                        foreach (var rn in grid.GetResourceNodes())
                        {
                            if (grid.IsResourceNodeCaptured(rn))
                                baseIncome += Constants.RESOURCE_NODE_BONUS;
                        }
                    }

                    GameManager.Instance?.AddResources(baseIncome);
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

        private bool _nullShardUsedThisWave;

        public void TakeDamage(float amount)
        {
            if (IsDestroyed) return;

            // Relic: Null Shard — negates first hit each wave
            if (!_nullShardUsedThisWave && ServiceLocator.TryGet<RelicManager>(out var rm) && rm.HasNullShard)
            {
                _nullShardUsedThisWave = true;
                GD.Print("[VineHarvester] Null Shard absorbed hit");
                return;
            }

            // Shield absorbs ALL damage until broken (Arcanist)
            if (_shieldHP > 0)
            {
                _shieldHP = 0;
                _shieldBroken = true;
                _shieldRechargeTimer = _shieldRechargeDelay;
                UpdateShieldVisual();

                // Flash + small shake for shield break
                _flashTimer = 0.15f;
                if (_modelRoot != null) SetFlash(true);
                if (ServiceLocator.TryGet<TDCamera>(out var cam2))
                    cam2.Shake(0.8f, 0.25f);

                GD.Print($"[Spire] Shield broken! Recharges in {_shieldRechargeDelay}s");
                return; // ALL damage absorbed
            }

            // Reset shield recharge timer on damage while shield is down
            if (_shieldMax > 0 && _shieldBroken)
                _shieldRechargeTimer = _shieldRechargeDelay;

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

        // ── Beam Attack ──

        private void UpdateBeamAttack(float dt)
        {
            _beamCooldownTimer -= dt;
            if (_beamCooldownTimer > 0f) return;

            // Find closest enemy in range
            float rangeSq = _spireData.BeamRange * _spireData.BeamRange;
            Node3D closest = null;
            float closestDistSq = float.MaxValue;

            foreach (var node in GetTree().GetNodesInGroup(Constants.GROUP_VINE_ENEMY))
            {
                if (node is not Node3D enemy) continue;
                float distSq = GlobalPosition.DistanceSquaredTo(enemy.GlobalPosition);
                if (distSq < rangeSq && distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    closest = enemy;
                }
            }

            if (closest == null) return;

            // Fire beam
            _beamCooldownTimer = _spireData.BeamCooldown;

            // Deal damage
            if (closest is VineEnemy enemy2)
                enemy2.TakeDamage(_spireData.BeamDamage);

            // Visual beam line from top of spire to target
            FireBeamVisual(closest.GlobalPosition);
        }

        private void FireBeamVisual(Vector3 targetPos)
        {
            // Clean up old beam
            if (_beamMesh != null && IsInstanceValid(_beamMesh))
                _beamMesh.QueueFree();

            // Calculate beam geometry
            float modelHeight = (_spireData?.ModelScale ?? 0.35f) * 14f; // approximate top
            var beamStart = GlobalPosition + new Vector3(0, modelHeight, 0);
            var beamEnd = targetPos + new Vector3(0, 0.5f, 0);
            var midPoint = (beamStart + beamEnd) / 2f;
            var diff = beamEnd - beamStart;
            float length = diff.Length();

            // Create cylinder beam
            _beamMesh = new MeshInstance3D();
            _beamMesh.Mesh = new CylinderMesh
            {
                TopRadius = 0.06f,
                BottomRadius = 0.06f,
                Height = length,
                RadialSegments = 6
            };

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.4f, 0.7f, 1.0f, 1.0f);
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.EmissionEnabled = true;
            mat.Emission = new Color(0.5f, 0.8f, 1.0f);
            mat.EmissionEnergyMultiplier = 2f;
            _beamMesh.MaterialOverride = mat;

            // AddChild BEFORE setting GlobalPosition/LookAt (needs scene tree)
            GetTree().Root.AddChild(_beamMesh);

            // Position at midpoint, rotate to face target
            _beamMesh.GlobalPosition = midPoint;
            _beamMesh.LookAt(beamEnd, Vector3.Up);
            _beamMesh.RotateObjectLocal(Vector3.Right, Mathf.Pi * 0.5f);
            _beamVisualTimer = _spireData.BeamDuration;

            // Camera shake on beam fire
            if (ServiceLocator.TryGet<TDCamera>(out var cam))
                cam.Shake(0.4f, 0.15f);
        }

        // ── Autocannons (Bruteforge) ──

        private void BuildAutocannons()
        {
            float modelHeight = (_spireData?.ModelScale ?? 1f) * 14f;
            _autocannonBarrels = new MeshInstance3D[_autocannonCount];
            var barrelColor = _spireData?.Color ?? new Color(0.9f, 0.5f, 0.2f);

            for (int i = 0; i < _autocannonCount; i++)
            {
                float angle = Mathf.Tau * i / _autocannonCount;
                float offsetX = Mathf.Cos(angle) * 1.2f;
                float offsetZ = Mathf.Sin(angle) * 1.2f;

                var barrel = new MeshInstance3D();
                barrel.Mesh = new CylinderMesh
                {
                    TopRadius = 0.08f,
                    BottomRadius = 0.12f,
                    Height = 1.0f,
                    RadialSegments = 6
                };
                barrel.Position = new Vector3(offsetX, modelHeight * 0.55f, offsetZ);
                barrel.Rotation = new Vector3(Mathf.Pi * 0.5f, angle, 0);

                var mat = new StandardMaterial3D();
                mat.AlbedoColor = barrelColor.Darkened(0.3f);
                mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                mat.EmissionEnabled = true;
                mat.Emission = barrelColor;
                mat.EmissionEnergyMultiplier = 0.3f;
                barrel.MaterialOverride = mat;

                AddChild(barrel);
                _autocannonBarrels[i] = barrel;

                // Stagger initial cooldowns so they don't all fire at once
                _autocannonCooldowns[i] = (1f / _autocannonFireRate) * i / _autocannonCount;
            }
        }

        private void UpdateAutocannons(float dt)
        {
            float rangeSq = _autocannonRange * _autocannonRange;

            for (int i = 0; i < _autocannonCount; i++)
            {
                _autocannonCooldowns[i] -= dt;
                if (_autocannonCooldowns[i] > 0f) continue;

                // Find closest enemy in range
                Node3D closest = null;
                float closestDistSq = float.MaxValue;

                foreach (var node in GetTree().GetNodesInGroup(Constants.GROUP_VINE_ENEMY))
                {
                    if (node is not Node3D enemy) continue;
                    float distSq = GlobalPosition.DistanceSquaredTo(enemy.GlobalPosition);
                    if (distSq < rangeSq && distSq < closestDistSq)
                    {
                        closestDistSq = distSq;
                        closest = enemy;
                    }
                }

                if (closest == null) continue;

                _autocannonCooldowns[i] = 1f / _autocannonFireRate;
                FireAutocannon(i, closest);
            }
        }

        private void FireAutocannon(int barrelIndex, Node3D target)
        {
            var barrelPos = _autocannonBarrels[barrelIndex].GlobalPosition;

            // Spawn projectile visual
            var proj = new MeshInstance3D();
            proj.Mesh = new SphereMesh { Radius = 0.1f, Height = 0.2f };
            var mat = new StandardMaterial3D();
            var color = _spireData?.Color ?? new Color(0.9f, 0.5f, 0.2f);
            mat.AlbedoColor = color;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.EmissionEnabled = true;
            mat.Emission = color;
            mat.EmissionEnergyMultiplier = 2f;
            proj.MaterialOverride = mat;

            GetTree().Root.AddChild(proj);
            proj.GlobalPosition = barrelPos;

            _projectiles.Add(new AutocannonProjectile
            {
                Visual = proj,
                Start = barrelPos,
                Target = target.GlobalPosition + new Vector3(0, 0.5f, 0),
                Progress = 0f,
                Damage = _autocannonDamage,
                TargetNode = target
            });

            // Barrel muzzle flash
            if (_autocannonBarrels[barrelIndex].MaterialOverride is StandardMaterial3D bmat)
                bmat.EmissionEnergyMultiplier = 1.5f;
        }

        private void UpdateProjectiles(float dt)
        {
            float projectileSpeed = 25f;

            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                var p = _projectiles[i];
                float dist = p.Start.DistanceTo(p.Target);
                if (dist < 0.1f) dist = 0.1f;
                p.Progress += projectileSpeed * dt / dist;

                if (p.Progress >= 1f)
                {
                    // Hit — deal damage
                    if (p.TargetNode is VineEnemy enemy && IsInstanceValid(enemy))
                        enemy.TakeDamage(p.Damage);

                    // Small impact VFX
                    VfxFactory.SpawnSplashRing(GetTree(), p.Target, 0.5f, DamageType.Physical);

                    if (IsInstanceValid(p.Visual))
                        p.Visual.QueueFree();
                    _projectiles.RemoveAt(i);
                }
                else
                {
                    // Update visual position — track live target if still valid
                    if (p.TargetNode is Node3D liveTarget && IsInstanceValid(liveTarget))
                        p.Target = liveTarget.GlobalPosition + new Vector3(0, 0.5f, 0);

                    if (IsInstanceValid(p.Visual))
                        p.Visual.GlobalPosition = p.Start.Lerp(p.Target, p.Progress);
                    _projectiles[i] = p;
                }
            }

            // Decay barrel muzzle flash
            if (_autocannonBarrels != null)
            {
                foreach (var barrel in _autocannonBarrels)
                {
                    if (barrel?.MaterialOverride is StandardMaterial3D bmat && bmat.EmissionEnergyMultiplier > 0.3f)
                        bmat.EmissionEnergyMultiplier = Mathf.Lerp(bmat.EmissionEnergyMultiplier, 0.3f, dt * 8f);
                }
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
            string modelPath = _spireData?.ModelPath ?? "";
            var scene = string.IsNullOrEmpty(modelPath) ? null : GD.Load<PackedScene>(modelPath);
            if (scene == null)
            {
                GD.PrintErr($"[Spire] Failed to load model: {modelPath}");
                BuildFallbackVisual();
                return;
            }

            float scale = _spireData?.ModelScale ?? 0.35f;
            float burial = _spireData?.BurialDepth ?? 0f;
            _modelRoot = scene.Instantiate<Node3D>();
            _modelRoot.Scale = new Vector3(scale, scale, scale);
            _modelRoot.Position = new Vector3(0, 0.2f - burial, 0);
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

        // ── Shield Visual ──

        private void BuildShieldVisual()
        {
            float modelHeight = (_spireData?.ModelScale ?? 0.35f) * 14f;

            // Hollow oval ring built with SurfaceTool, billboarded to face camera
            _shieldOrb = new MeshInstance3D();

            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);

            int segments = 48;
            float outerW = 2.2f, outerH = 3.2f; // Outer oval semi-axes
            float innerW = 1.8f, innerH = 2.7f;  // Inner oval semi-axes (hollow center)

            for (int i = 0; i < segments; i++)
            {
                float a0 = Mathf.Tau * i / segments;
                float a1 = Mathf.Tau * (i + 1) / segments;

                // Vertices in XY plane — billboard handles camera-facing
                var o0 = new Vector3(Mathf.Cos(a0) * outerW, Mathf.Sin(a0) * outerH, 0);
                var o1 = new Vector3(Mathf.Cos(a1) * outerW, Mathf.Sin(a1) * outerH, 0);
                var i0 = new Vector3(Mathf.Cos(a0) * innerW, Mathf.Sin(a0) * innerH, 0);
                var i1 = new Vector3(Mathf.Cos(a1) * innerW, Mathf.Sin(a1) * innerH, 0);

                // Front-facing quad (two triangles)
                st.SetNormal(Vector3.Back);
                st.AddVertex(o0); st.AddVertex(i0); st.AddVertex(o1);
                st.AddVertex(i0); st.AddVertex(i1); st.AddVertex(o1);
            }

            _shieldOrb.Mesh = st.Commit();
            _shieldOrb.Position = new Vector3(0, modelHeight * 0.45f, 0);

            _shieldOrbMat = new StandardMaterial3D();
            _shieldOrbMat.AlbedoColor = new Color(0.3f, 0.6f, 1.0f, 0.2f);
            _shieldOrbMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            _shieldOrbMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            _shieldOrbMat.EmissionEnabled = true;
            _shieldOrbMat.Emission = new Color(0.3f, 0.6f, 1.0f);
            _shieldOrbMat.EmissionEnergyMultiplier = 0.5f;
            _shieldOrbMat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
            _shieldOrbMat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
            _shieldOrb.MaterialOverride = _shieldOrbMat;
            AddChild(_shieldOrb);
        }

        private void UpdateShieldVisual()
        {
            if (_shieldOrb == null) return;
            if (_shieldHP > 0)
            {
                _shieldOrb.Visible = true;
                _shieldOrbMat.AlbedoColor = new Color(0.3f, 0.6f, 1.0f, 0.2f);
                _shieldOrbMat.EmissionEnergyMultiplier = 0.5f;
            }
            else
            {
                _shieldOrb.Visible = false;
            }
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
