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
        /// <summary>Top of the Spire model above its origin.</summary>
        public float ModelTop => _modelTop > 0f ? _modelTop : 5f;

        /// <summary>Bank Materials at the Spire (Materials-mode drops).</summary>
        public void AddMaterials(float amount)
        {
            if (amount <= 0f) return;
            MaterialsAccumulated += amount;
            GameEvents.OnMaterialsAccumulated?.Invoke(MaterialsAccumulated, SelectedMaterial);
        }

        /// <summary>Spend banked Materials (Spire menu). False when there isn't enough.</summary>
        public bool SpendMaterials(float amount)
        {
            if (amount < 0f || MaterialsAccumulated + 0.001f < amount) return false;
            MaterialsAccumulated = Mathf.Max(0f, MaterialsAccumulated - amount);
            GameEvents.OnMaterialsAccumulated?.Invoke(MaterialsAccumulated, SelectedMaterial);
            return true;
        }

        private MeshInstance3D _healthBar;
        private MeshInstance3D _healthBarBg;
        private float _incomeTimer;

        // Hit flash
        private float _flashTimer;
        private Node3D _modelRoot;
        /// <summary>The visible model, for tests.</summary>
        internal Node3D VisualRoot => _modelRoot;
        // Model's ground footprint (X/Z, relative to this node), for seating it on slopes
        private Rect2 _footprint = new(-1f, -1f, 2f, 2f);

        /// <summary>
        /// Put the Spire at a cell centre, low enough that its base meets the lowest ground under
        /// it. The exit cell is often on a slope; at the cell's average height the downhill side
        /// of the base hovered about 0.2 up.
        /// </summary>
        public void SeatOn(VineGrid grid, Vector3 at)
        {
            const float inset = 0.3f;  // the outer rim of the base may overhang a little
            float x0 = at.X + _footprint.Position.X + inset, x1 = at.X + _footprint.End.X - inset;
            float z0 = at.Z + _footprint.Position.Y + inset, z1 = at.Z + _footprint.End.Y - inset;
            if (x1 < x0) x0 = x1 = at.X;
            if (z1 < z0) z0 = z1 = at.Z;
            float low = float.MaxValue;
            for (int i = 0; i <= 4; i++)
            for (int j = 0; j <= 4; j++)
                low = Mathf.Min(low, grid.GetWorldHeight(Mathf.Lerp(x0, x1, i / 4f), Mathf.Lerp(z0, z1, j / 4f)));
            GlobalPosition = new Vector3(at.X, low, at.Z);
        }

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
        private ShaderMaterial _shieldMat;
        private float _shieldShown;      // 0..1, eases toward 1 while the shield is up
        private float _shieldHitGlow;    // flash when it breaks

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
        private TowerLook[] _autocannonLooks;   // kit turrets on the platform, when the Spire has a sheet for them
        private float _baseTop;                 // top of the Spire's platform (0 without one), node space
        private float _modelTop;                // top of the whole model, node space (0 until built)
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
            MaxHP = Constants.VINE_HARVESTER_MAX_HP * MetaRun.SpireHpMult;
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
                MaxHP = _spireData.MaxHP * MetaRun.SpireHpMult; // Reinforced Spire (perk tree)
                CurrentHP = MaxHP;

                // Shield (Arcanist)
                _shieldMax = _spireData.Shield;
                _shieldHP = _shieldMax;
                _shieldRechargeDelay = _spireData.ShieldRechargeDelay / Mathf.Max(0.1f, RoleRun.ShieldRechargeMult); // Arcanist: faster
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
            long __pt = FrameProfiler.Start();
            try
            {
                VineEnemy.HitSource = "the Spire";
                try { ProcessTick(delta); }
                finally { VineEnemy.HitSource = null; }
        
            }
            finally { FrameProfiler.Stop("spire", __pt); }
        }

        private void ProcessTick(double delta)
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

            // ── Autocannons (Bruteforge) and guns bought at the Spire ──
            if (_autocannonCount > 0 || _extraGuns.Count > 0)
            {
                var phase2 = GameManager.Instance?.CurrentPhase ?? GamePhase.Build;
                if (phase2 == GamePhase.Wave)
                {
                    if (_autocannonCount > 0) UpdateAutocannons(dt);
                    UpdateExtraGuns(dt);
                }
                UpdateProjectiles(dt);
            }

            // ── HP regen ──
            if (_spireData is { HpRegenPerSec: > 0f } && CurrentHP < MaxHP && CurrentHP > 0)
            {
                CurrentHP = Mathf.Min(MaxHP, CurrentHP + _spireData.HpRegenPerSec * dt);
                GameEvents.OnHarvesterHPChanged?.Invoke(CurrentHP, MaxHP);
            }

            UpdateShieldDome(dt);

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

                    var gm = GameManager.Instance;
                    gm?.AddResources(Mathf.RoundToInt(baseIncome * (gm?.RunResourceMult ?? 1f)));
                }
                else if (CurrentMode == MiningMode.Materials && SelectedMaterial != MaterialType.None)
                {
                    // Materials mine faster than Resources (the trade for giving up the network's income)
                    float magicRate = Constants.VINE_HARVESTER_INCOME * SignalTuningEditor.HarvesterIncomeMult
                        * (SpireStation.Current?.Data.MaterialsMode.SpireRateMult ?? 1f);
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
                ShieldPulse();
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

        /// <summary>Raise max HP and heal by the same amount.</summary>
        public void IncreaseMaxHP(float amount)
        {
            if (IsDestroyed || amount <= 0) return;
            MaxHP += amount;
            CurrentHP = Mathf.Min(MaxHP, CurrentHP + amount);
            GameEvents.OnHarvesterHPChanged?.Invoke(CurrentHP, MaxHP);
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
            float beamRange = _spireData.BeamRange + (SpireStation.Current?.SpireRangeBonus ?? 0f);
            float rangeSq = beamRange * beamRange;
            Node3D closest = null;
            float closestDistSq = float.MaxValue;

            foreach (var node in Roster.Enemies(GetTree()))
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
            float beamDamage = _spireData.BeamDamage * (SpireStation.Current?.SpireDamageMult ?? 1f) * RoleRun.BeamMult;
            if (closest is VineEnemy enemy2)
            {
                enemy2.TakeDamage(beamDamage);
                if (RoleRun.BeamChain > 0) ChainBeam(enemy2, beamDamage * RoleRun.BeamChainShare);
            }

            // Visual beam line from top of spire to target
            FireBeamVisual(closest.GlobalPosition);
        }

        /// <summary>The shield (Arcanist): up now, and how long it takes to come back (tests).</summary>
        public bool ShieldUp => _shieldHP > 0f;
        public float ShieldRechargeDelay => _shieldRechargeDelay;
        /// <summary>Enemies the beam has jumped to (tests).</summary>
        public int BeamChainHits { get; private set; }
        /// <summary>Times the shield breaking shocked enemies, and how many it caught (tests).</summary>
        public int ShieldPulses { get; private set; }
        public int LastShieldPulseHits { get; private set; }

        /// <summary>Obelisk: the beam jumps on from <paramref name="first"/> to the nearest enemies it hasn't hit.</summary>
        private void ChainBeam(VineEnemy first, float damage)
        {
            var hit = new List<VineEnemy> { first };
            var from = first;
            var color = new Color(0.5f, 0.8f, 1f);
            float r2 = RoleRun.BeamChainRange * RoleRun.BeamChainRange;
            for (int j = 0; j < RoleRun.BeamChain; j++)
            {
                VineEnemy next = null;
                float best = r2;
                foreach (var e in Roster.Enemies(GetTree()))
                {
                    if (!IsInstanceValid(e) || !e.IsAlive || hit.Contains(e)) continue;
                    float d = from.GlobalPosition.DistanceSquaredTo(e.GlobalPosition);
                    if (d < best) { best = d; next = e; }
                }
                if (next == null) break;
                VfxFactory.SpawnArc(GetTree(), from.BodyCentre, next.BodyCentre, color);
                next.TakeDamage(damage);
                BeamChainHits++;
                hit.Add(next);
                from = next;
            }
        }

        /// <summary>Arcanist: the shield breaking shocks and stuns everything close by.</summary>
        private void ShieldPulse()
        {
            if (RoleRun.ShieldPulseStun <= 0f || RoleRun.ShieldPulseRadius <= 0f) return;
            float r2 = RoleRun.ShieldPulseRadius * RoleRun.ShieldPulseRadius;
            int n = 0;
            foreach (var e in Roster.Enemies(GetTree()))
            {
                if (!IsInstanceValid(e) || !e.IsAlive) continue;
                if (GlobalPosition.DistanceSquaredTo(e.GlobalPosition) > r2) continue;
                e.ApplyStun(RoleRun.ShieldPulseStun);
                if (RoleRun.ShieldPulseDamage > 0f)
                {
                    var was = VineEnemy.HitSource;
                    VineEnemy.HitSource = "the Spire";
                    try { e.TakeDamage(RoleRun.ShieldPulseDamage, DamageKind.Electric); }
                    finally { VineEnemy.HitSource = was; }
                }
                n++;
            }
            ShieldPulses++;
            LastShieldPulseHits = n;
            var tint = _spireData?.Color ?? new Color(0.2f, 0.9f, 0.4f);
            VfxFactory.SpawnAreaPulse(GetTree(), GlobalPosition, RoleRun.ShieldPulseRadius, tint, 0.7f, 0.9f);
            if (n > 0) DamageNumbers.Tag(GlobalPosition + Vector3.Up * 4.5f, $"SHIELD SHOCK: {n} STUNNED", tint.Lightened(0.3f));
        }

        private void FireBeamVisual(Vector3 targetPos)
        {
            // Clean up old beam
            if (_beamMesh != null && IsInstanceValid(_beamMesh))
                _beamMesh.QueueFree();

            // Calculate beam geometry
            float modelHeight = _modelTop > 0f ? _modelTop * 0.95f : (_spireData?.ModelScale ?? 0.35f) * 14f;
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
            // Kit turrets on the platform's corners, each tracking its own target
            var sheet = TowerSheet.Load(_spireData?.AutocannonSheet);
            if (sheet != null && _modelRoot != null)
            {
                _autocannonLooks = new TowerLook[_autocannonCount];
                float r = _spireData.AutocannonMountRadius;
                for (int i = 0; i < _autocannonCount; i++)
                {
                    float angle = Mathf.Tau * i / _autocannonCount + Mathf.Pi / 4f;
                    var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    // Idle facing straight out from the Spire
                    var look = TowerLook.Build(sheet, _spireData.Color, Mathf.RadToDeg(Mathf.Atan2(dir.X, dir.Y)));
                    look.Name = $"Autocannon{i}";
                    look.Position = new Vector3(dir.X * r, _baseTop, dir.Y * r);
                    _modelRoot.AddChild(look);
                    _autocannonLooks[i] = look;
                    _autocannonCooldowns[i] = (1f / _autocannonFireRate) * i / _autocannonCount;
                }
                return;
            }

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

        // ── Spire Guns: one more gun per level, on a ring round the Spire (every role) ──
        private readonly List<(TowerLook look, float cooldown)> _extraGuns = new();
        /// <summary>Guns mounted by Spire Guns upgrades (tests).</summary>
        public int ExtraGunCount => _extraGuns.Count;
        public IReadOnlyList<TowerLook> ExtraGunLooks => _extraGuns.ConvertAll(g => g.look);
        /// <summary>Shots the bought guns have fired (tests).</summary>
        public int ExtraGunShots { get; private set; }

        /// <summary>
        /// Mount <paramref name="count"/> extra guns (Spire Guns levels). Buying Spire Guns only
        /// raised a damage number before, so nothing on the Spire changed.
        /// </summary>
        public void SetExtraGuns(int count)
        {
            count = Mathf.Clamp(count, 0, 8);
            if (_modelRoot == null) return;
            var sheet = TowerSheet.Load("spire_autocannon");
            if (sheet == null) return;
            while (_extraGuns.Count > count)
            {
                var last = _extraGuns[^1];
                if (IsInstanceValid(last.look)) last.look.QueueFree();
                _extraGuns.RemoveAt(_extraGuns.Count - 1);
            }
            float r = (_spireData?.AutocannonMountRadius ?? 1.05f) + 0.85f;
            var tint = _spireData?.Color ?? new Color(0.9f, 0.5f, 0.2f);
            while (_extraGuns.Count < count)
            {
                int i = _extraGuns.Count;
                // Between the platform's own guns, going round
                float angle = Mathf.Tau * i / 8f;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var look = TowerLook.Build(sheet, tint, Mathf.RadToDeg(Mathf.Atan2(dir.X, dir.Y)));
                look.Name = $"SpireGun{i}";
                look.Position = new Vector3(dir.X * r, Mathf.Max(0f, _baseTop) + 0.05f, dir.Y * r);
                look.Scale = Vector3.One * 0.9f;
                _modelRoot.AddChild(look);
                _extraGuns.Add((look, 0.25f * i));
                VfxFactory.SpawnEnergyBurst(GetTree(), look.GlobalPosition + Vector3.Up * 0.5f, tint, 10);
            }
        }

        private Node3D _plating;
        /// <summary>Plating levels shown on the Spire (tests).</summary>
        public int PlatingShown { get; private set; }

        /// <summary>
        /// Plating bought at the Spire: armour plates round its base, taller each level, with a
        /// band in the role's colour from level 3 (it only raised max health before).
        /// </summary>
        public void SetPlating(int level)
        {
            if (_modelRoot == null) return;
            if (_plating != null && IsInstanceValid(_plating)) _plating.QueueFree();
            PlatingShown = level;
            if (level <= 0) { _plating = null; return; }
            _plating = new Node3D { Name = "SpirePlating" };
            _modelRoot.AddChild(_plating);
            var tint = _spireData?.Color ?? new Color(0.9f, 0.5f, 0.2f);
            var steel = TowerLook.Surface("panel", tint);
            var band = TowerLook.Surface("glow", tint, 1.2f);
            float r = (_spireData?.AutocannonMountRadius ?? 1.05f) + 0.35f;
            float h = 0.35f + 0.2f * level;
            float y0 = Mathf.Max(0f, _baseTop);
            for (int i = 0; i < 8; i++)
            {
                float a = Mathf.Tau * i / 8f + Mathf.Pi / 8f;
                var plate = new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(0.62f, h, 0.12f) },
                    MaterialOverride = steel,
                    Position = new Vector3(Mathf.Cos(a) * r, y0 + h * 0.5f, Mathf.Sin(a) * r),
                    Rotation = new Vector3(-0.12f, -a + Mathf.Pi / 2f, 0f),
                };
                _plating.AddChild(plate);
                if (level >= 3)
                    plate.AddChild(new MeshInstance3D
                    {
                        Mesh = new BoxMesh { Size = new Vector3(0.64f, 0.06f, 0.13f) },
                        MaterialOverride = band,
                        Position = new Vector3(0, h * 0.5f - 0.08f, 0),
                    });
            }
            VfxFactory.SpawnEnergyBurst(GetTree(), GlobalPosition + Vector3.Up * (y0 + h), tint, 12);
        }

        private void UpdateExtraGuns(float dt)
        {
            if (_extraGuns.Count == 0) return;
            float range = (_autocannonCount > 0 ? _autocannonRange : 14f) + (SpireStation.Current?.SpireRangeBonus ?? 0f);
            float rangeSq = range * range;
            float rate = _autocannonCount > 0 ? _autocannonFireRate : 2f;
            var enemies = Roster.Enemies(GetTree());
            var pos = Roster.EnemyPositions(GetTree());
            var me = GlobalPosition;
            for (int g = 0; g < _extraGuns.Count; g++)
            {
                var (look, cd) = _extraGuns[g];
                cd -= dt;
                if (cd <= 0f && IsInstanceValid(look))
                {
                    VineEnemy best = null;
                    float bestSq = rangeSq;
                    for (int i = 0; i < enemies.Count; i++)
                    {
                        var e = enemies[i];
                        if (!IsInstanceValid(e) || !e.IsAlive) continue;
                        float d = me.DistanceSquaredTo(pos[i]);
                        if (d < bestSq) { bestSq = d; best = e; }
                    }
                    if (best != null)
                    {
                        cd = 1f / rate;
                        look.Track(best);
                        look.Fire();
                        FireShot(look.MuzzleGlobal, best, _autocannonCount > 0 ? _autocannonDamage : 12f);
                        ExtraGunShots++;
                    }
                }
                _extraGuns[g] = (look, cd);
            }
        }

        private void FireShot(Vector3 from, Node3D target, float damage)
        {
            var color = _spireData?.Color ?? new Color(0.9f, 0.5f, 0.2f);
            var proj = new MeshInstance3D
            {
                Mesh = VfxCache.Sphere(0.1f),
                MaterialOverride = VfxCache.Glow(color.Lerp(Colors.White, 0.3f), color, 2f, alpha: false),
            };
            VfxFactory.SpawnMuzzleFlash(GetTree(), from, target.GlobalPosition - from, color, 0.9f);
            GetTree().Root.AddChild(proj);
            proj.GlobalPosition = from;
            _projectiles.Add(new AutocannonProjectile
            {
                Visual = proj, Start = from, Target = target.GlobalPosition + new Vector3(0, 0.5f, 0),
                Progress = 0f, Damage = damage * (SpireStation.Current?.SpireDamageMult ?? 1f), TargetNode = target
            });
        }

        private void UpdateAutocannons(float dt)
        {
            float acRange = _autocannonRange + (SpireStation.Current?.SpireRangeBonus ?? 0f);
            float rangeSq = acRange * acRange;

            for (int i = 0; i < _autocannonCount; i++)
            {
                _autocannonCooldowns[i] -= dt;
                if (_autocannonCooldowns[i] > 0f) continue;

                // Find closest enemy in range
                Node3D closest = null;
                float closestDistSq = float.MaxValue;

                foreach (var node in Roster.Enemies(GetTree()))
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
            var look = _autocannonLooks?[barrelIndex];
            look?.Track(target);
            look?.Fire();
            var barrelPos = look != null ? look.MuzzleGlobal : _autocannonBarrels[barrelIndex].GlobalPosition;

            // Projectile visual: shared mesh and material (a new pair per shot leaked like the old effects)
            var color = _spireData?.Color ?? new Color(0.9f, 0.5f, 0.2f);
            var proj = new MeshInstance3D
            {
                Mesh = VfxCache.Sphere(0.1f),
                MaterialOverride = VfxCache.Glow(color.Lerp(Colors.White, 0.3f), color, 2f, alpha: false),
            };
            VfxFactory.SpawnMuzzleFlash(GetTree(), barrelPos, target.GlobalPosition - barrelPos, color, 0.9f);

            GetTree().Root.AddChild(proj);
            proj.GlobalPosition = barrelPos;

            _projectiles.Add(new AutocannonProjectile
            {
                Visual = proj,
                Start = barrelPos,
                Target = target.GlobalPosition + new Vector3(0, 0.5f, 0),
                Progress = 0f,
                Damage = _autocannonDamage * (SpireStation.Current?.SpireDamageMult ?? 1f),
                TargetNode = target
            });

            // Barrel muzzle flash
            if (_autocannonBarrels?[barrelIndex]?.MaterialOverride is StandardMaterial3D bmat)
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
                    VfxFactory.SpawnImpact(GetTree(), p.Target, _spireData?.Color ?? new Color(0.9f, 0.5f, 0.2f));

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
            // The model (and its platform and guns, if it has them) under one root, so the
            // whole Spire hides, spins and flashes together
            _modelRoot = new Node3D { Name = "SpireModel" };
            AddChild(_modelRoot);
            var model = scene.Instantiate<Node3D>();
            model.Scale = new Vector3(scale, scale, scale);
            _modelRoot.AddChild(model);

            // A platform in the kit's materials, with a band in the role's colour
            var baseSheet = TowerSheet.Load(_spireData?.BaseSheet);
            if (baseSheet != null)
            {
                var plat = TowerLook.Build(baseSheet, _spireData.Color);
                plat.Name = "SpireBase";
                plat.Position = new Vector3(0, -burial, 0);
                _modelRoot.AddChild(plat);
                _baseTop = (baseSheet.Pedestal?.Height ?? 0f) - burial;
            }

            // Stand the model on the ground (or its platform) by its own bounds, then sink it
            // burialDepth. The models' origins sit at different heights; a fixed 0.2 lift plus
            // hand-tuned offsets left the Obelisk's base hovering a quarter unit up.
            var bounds = AssetLibrary.GetCombinedAABB(model);
            float bottom = bounds.Position.Y * scale;
            model.Position = new Vector3(0, -bottom - burial + Mathf.Max(_baseTop + burial, 0f), 0);
            _footprint = new Rect2(bounds.Position.X * scale, bounds.Position.Z * scale,
                bounds.Size.X * scale, bounds.Size.Z * scale);
            if (baseSheet?.Pedestal != null)
            {
                float r = baseSheet.Pedestal.Radius + 0.14f;
                _footprint = _footprint.Merge(new Rect2(-r, -r, 2f * r, 2f * r));
            }

            _modelTop = AssetLibrary.GetCombinedAABB(_modelRoot).End.Y;

            // Find the AnimationPlayer (created by GLB importer)
            _animPlayer = FindChild<AnimationPlayer>(model);
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
            // Just above the model, whatever its height (a fixed 5.8 cut through taller Spires)
            float barWidth = 2.0f;
            float barY = _modelTop > 0f ? _modelTop + 0.5f : 5.8f;

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

        private static Shader _domeShader;

        /// <summary>
        /// A hex-panelled energy dome over the Spire while its shield is up. (It was a flat oval
        /// ring billboarded at the camera.)
        /// </summary>
        private void BuildShieldVisual()
        {
            float top = _modelRoot != null ? AssetLibrary.GetCombinedAABB(_modelRoot).End.Y : 5f;
            // Wide enough to read as a dome over a tall, thin Spire rather than a capsule
            float radius = Mathf.Max(Mathf.Max(_footprint.Size.X, _footprint.Size.Y) * 0.5f + 1.2f, top * 0.42f);
            _shieldOrb = new MeshInstance3D
            {
                Name = "ShieldDome",
                Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 48, Rings = 24 },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                // Centred on the ground: only the upper half is drawn
                Scale = new Vector3(radius, Mathf.Max(top, 3f) * 1.08f, radius),
            };
            _domeShader ??= new Shader { Code = @"
shader_type spatial;
render_mode blend_add, unshaded, cull_disabled, depth_draw_never, shadows_disabled;
uniform vec3 color : source_color = vec3(0.35, 0.65, 1.0);
uniform float strength = 1.0;
uniform float hit = 0.0;
varying vec3 lp;
void vertex() { lp = VERTEX; }
float hexd(vec2 p) { p = abs(p); return max(dot(p, normalize(vec2(1.0, 1.7320508))), p.x); }
void fragment() {
    if (lp.y < 0.0) discard;
    // Clamped: pow() of a hair below zero (facing the camera exactly) is NaN, which the glow
    // pass smeared into white blobs on the dome's sides
    float ndv = clamp(abs(dot(NORMAL, VIEW)), 0.0, 1.0);
    float rim = pow(max(1.0 - ndv, 0.0), 2.5);
    // Hex cells: 18 around, latitude stretched to match
    vec2 uv = vec2(atan(lp.z, lp.x) * 2.8648, asin(clamp(lp.y, -1.0, 1.0)) * 3.2);
    vec2 r = vec2(1.0, 1.7320508);
    vec2 a = mod(uv, r) - r * 0.5;
    vec2 b = mod(uv - r * 0.5, r) - r * 0.5;
    vec2 g = dot(a, a) < dot(b, b) ? a : b;
    float edge = smoothstep(0.40, 0.48, hexd(g));
    // A slow scan band climbing the dome
    float band = 1.0 - smoothstep(0.0, 0.05, abs(fract(lp.y * 1.4 - TIME * 0.3) - 0.5));
    // The inside (back faces) at half strength, and no band at the rim: where both faces
    // overlap at the silhouette the band added up to white-hot spots
    float face = FRONT_FACING ? 1.0 : 0.45;
    float glow = (0.02 + rim * 0.7 + edge * (0.07 + rim * 0.3) + band * 0.06 * (1.0 - rim)) * face;
    float foot = smoothstep(0.0, 0.06, lp.y);
    ALBEDO = clamp((color * glow + vec3(1.0) * hit * (0.3 + rim)) * strength * foot, vec3(0.0), vec3(1.5));
}
" };
            _shieldMat = new ShaderMaterial { Shader = _domeShader };
            var c = _spireData?.Color ?? new Color(0.3f, 0.6f, 1f);
            // The Arcanist's green read poorly as a shield: keep it blue-leaning but tinted
            var shieldColor = new Color(0.3f, 0.6f, 1f).Lerp(c, 0.25f);
            _shieldMat.SetShaderParameter("color", new Vector3(shieldColor.R, shieldColor.G, shieldColor.B));
            _shieldOrb.MaterialOverride = _shieldMat;
            AddChild(_shieldOrb);
            _shieldShown = _shieldHP > 0 ? 1f : 0f;
            UpdateShieldDome(0f);
        }

        private void UpdateShieldVisual()
        {
            // A break flashes the dome as it collapses; a recharge fades it back in (UpdateShieldDome)
            if (_shieldOrb != null && _shieldHP <= 0) _shieldHitGlow = 1f;
        }

        private void UpdateShieldDome(float dt)
        {
            if (_shieldOrb == null || _shieldMat == null) return;
            float target = _shieldHP > 0 ? 1f : 0f;
            // Fade in over about half a second, collapse in a quarter
            _shieldShown = Mathf.MoveToward(_shieldShown, target, dt * (target > _shieldShown ? 2f : 4f));
            _shieldHitGlow = Mathf.MoveToward(_shieldHitGlow, 0f, dt * 3f);
            float shown = Mathf.Max(_shieldShown, _shieldHitGlow * 0.6f);
            _shieldOrb.Visible = shown > 0.01f && (_modelRoot?.Visible ?? true);
            _shieldMat.SetShaderParameter("strength", 0.6f * shown);
            _shieldMat.SetShaderParameter("hit", _shieldHitGlow);
        }

        private void SetFlash(bool flash)
        {
            if (_modelRoot == null) return;
            // Restore exactly afterwards: zeroing the emission put out the platform's lit band
            if (flash) HitFlash.On(_modelRoot, Colors.White, 1.2f);
            else HitFlash.Off(_modelRoot);
        }

        public override void _ExitTree()
        {
            ServiceLocator.Unregister<VineHarvester>();
        }
    }
}
