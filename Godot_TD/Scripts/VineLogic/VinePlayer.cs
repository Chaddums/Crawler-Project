using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Player character ability definition.
    /// </summary>
    public class VinePlayerAbility
    {
        public string Name;
        public string Description;
        public float Cooldown;
        public float CurrentCooldown;
        public float MaterialsCost;
        public float Range;
        public Color IconColor;

        public delegate void AbilityExecute(VinePlayer player);
        public AbilityExecute Execute;

        public bool IsReady => CurrentCooldown <= 0;
    }

    /// <summary>
    /// MOBA-style player character for Vine Logic TD.
    /// WASD movement, auto-attacks, 3 abilities (Q/E/R).
    /// </summary>
    public partial class VinePlayer : CharacterBody3D
    {
        public float MaxHP { get; set; }
        public float CurrentHP { get; internal set; }
        public float MaxMaterials { get; set; }
        public float CurrentMaterials { get; internal set; }
        public float MoveSpeed { get; set; } = Constants.VINE_PLAYER_MOVE_SPEED;
        public float AttackRange { get; set; } = Constants.VINE_PLAYER_ATTACK_RANGE;
        public float AttackDamage { get; set; }
        public float AttackSpeed { get; set; }
        public float ChaosAbilityCooldownMult { get; set; } = 1f;
        public float MaterialsRegen { get; set; }
        public bool IsAlive => CurrentHP > 0;
        public int EnemiesKilledPersonally { get; set; }
        public Node3D ModelRoot => _modelRoot;
        internal float _baseModelScale = 1f; // Set during BuildVisual, read by SignalTuningEditor

        private float _attackCooldown;
        private float _respawnTimer;
        private bool _isDead;
        private VineGrid _grid;

        // ── Emergence animation state ──
        private bool _emerging;
        private float _emergeTimer;
        private float _emergeDuration;
        private Vector3 _emergeStart;   // Spire position
        private Vector3 _emergeEnd;     // Final standing position
        public bool IsEmerging => _emerging;

        // Visual
        private Node3D _modelRoot;
        private CharacterAnimator _animator;
        private float _bounceTimer;
        private bool _isMoving;
        private MeshInstance3D _healthBar;
        private MeshInstance3D _healthBarBg;
        private float _flashTimer;

        // Dome material swap — cached materials for inside/outside dome
        private readonly Dictionary<MeshInstance3D, Material> _domeMaterials = new();   // Silver-white (inside)
        private readonly Dictionary<MeshInstance3D, Material> _themedMaterials = new(); // Planet-themed (outside)
        private bool _isInsideDome = true;

        // Skeleton bone overrides for naruto run arms-back pose
        private Skeleton3D _skeleton;
        private int _armRIdx = -1;
        private int _armLIdx = -1;

        // Attack cast animation
        private float _castTimer;      // Counts up during wind-up
        private float _castDuration;   // Total wind-up time before projectile fires
        private VineEnemy _castTarget;  // Locked target during cast
        private bool _isCasting;

        // Abilities
        private VinePlayerAbility[] _abilities;

        public override void _Ready()
        {
            // Use live tuning values (SignalTuningEditor) with meta perk bonuses on top
            MaxHP = SignalTuningEditor.PlayerMaxHP + SignalTuningEditor.PlayerMaxHPBonus;
            MaxMaterials = SignalTuningEditor.PlayerMaxMaterials + SignalTuningEditor.PlayerMaxMaterialsBonus;
            AttackDamage = SignalTuningEditor.PlayerAttackDamage * SignalTuningEditor.PlayerAttackDamageMult;
            AttackSpeed = SignalTuningEditor.PlayerAttackSpeed * SignalTuningEditor.PlayerAttackSpeedMult;
            MaterialsRegen = SignalTuningEditor.PlayerMaterialsRegen * SignalTuningEditor.PlayerMaterialsRegenMult;
            MoveSpeed = SignalTuningEditor.PlayerMoveSpeed;
            AttackRange = SignalTuningEditor.PlayerAttackRange;

            CurrentHP = MaxHP;
            CurrentMaterials = MaxMaterials;

            _grid = ServiceLocator.Get<VineGrid>();

            // Collision shape
            var shape = new CollisionShape3D();
            var capsule = new CapsuleShape3D();
            capsule.Radius = 0.4f;
            capsule.Height = 1.2f;
            shape.Shape = capsule;
            shape.Position = new Vector3(0, 0.6f, 0);
            AddChild(shape);

            // Physics layers
            CollisionLayer = 1u << (Constants.LAYER_HERO - 1);
            // No ground collision — Y is set manually from heightmap to avoid jitter
            CollisionMask = 1u << (Constants.LAYER_ENEMY - 1);

            AddToGroup(Constants.GROUP_VINE_PLAYER);

            BuildVisual();
            BuildHealthBar();

            _abilities = GetDefaultAbilities();

            ServiceLocator.Register(this);
            GameEvents.OnPlayerHPChanged?.Invoke(CurrentHP, MaxHP);
            GameEvents.OnPlayerMaterialsChanged?.Invoke(CurrentMaterials, MaxMaterials);
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;

            // Emergence animation — skip all other processing
            if (_emerging)
            {
                UpdateEmergence(dt);
                return;
            }

            // Respawn timer
            if (_isDead)
            {
                _respawnTimer -= dt;
                if (_respawnTimer <= 0)
                    Respawn();
                return;
            }

            // Movement — always active (camera follows player)
            var phase = GameManager.Instance?.CurrentPhase ?? GamePhase.Build;
            if (phase != GamePhase.Victory && phase != GamePhase.Defeat && phase != GamePhase.Paused)
                HandleMovement(dt);

            // Materials regen
            if (CurrentMaterials < MaxMaterials)
            {
                CurrentMaterials = Mathf.Min(MaxMaterials, CurrentMaterials + MaterialsRegen * dt);
                GameEvents.OnPlayerMaterialsChanged?.Invoke(CurrentMaterials, MaxMaterials);
            }

            // Cast animation update — wind-up then fire
            if (_isCasting)
            {
                UpdateCastAnimation(dt);
            }
            else
            {
                _attackCooldown -= dt;
                if (_attackCooldown <= 0)
                    TryAutoAttack();
            }

            // Ability cooldowns
            for (int i = 0; i < _abilities.Length; i++)
            {
                if (_abilities[i].CurrentCooldown > 0)
                {
                    _abilities[i].CurrentCooldown -= dt * ChaosAbilityCooldownMult;
                    GameEvents.OnAbilityCooldownChanged?.Invoke(i, _abilities[i].CurrentCooldown);
                }
            }

            // Ability input
            if (Input.IsActionJustPressed("ability_q")) TryUseAbility(0);
            if (Input.IsActionJustPressed("ability_e")) TryUseAbility(1);
            if (Input.IsActionJustPressed("ability_r")) TryUseAbility(2);

            // Flash decay
            if (_flashTimer > 0)
            {
                _flashTimer -= dt;
                if (_flashTimer <= 0) SetFlash(false);
            }

            UpdateHealthBar();
            UpdateDomeMaterials();
        }

        private float _smoothYaw; // Smoothed facing angle to prevent jitter
        private bool _isSlipping; // On a steep slope — flailing animation
        private const float SLIP_SLOPE_THRESHOLD = 0.8f; // Height diff per cell that triggers slip
        private const float SLIP_SLIDE_SPEED = 2f; // How fast BIT slides downhill

            // Run momentum — bob walk transitions to naruto run
        private float _runTimer; // How long BIT has been continuously running
        // Read from SignalTuningEditor for live tuning
        private static float NARUTO_RUN_THRESHOLD => SignalTuningEditor.NarutoRunThreshold;
        private static float NARUTO_SPEED_BONUS => SignalTuningEditor.NarutoSpeedBonus;
        private bool _isNarutoRunning;
        private bool _narutoForward = true; // Pingpong direction
        private const float NARUTO_FREEZE_FRAME = 0.5f; // Seek position in Run clip for arms-back pose

        /// <summary>
        /// Get terrain slope at a world position by sampling nearby heights.
        /// Returns the slope vector (points downhill) and its magnitude.
        /// </summary>
        private Vector3 GetTerrainSlope(float worldX, float worldZ)
        {
            const float sample = 0.5f; // Half-unit sample distance
            float hCenter = _grid.GetWorldHeight(worldX, worldZ);
            float hPosX = _grid.GetWorldHeight(worldX + sample, worldZ);
            float hNegX = _grid.GetWorldHeight(worldX - sample, worldZ);
            float hPosZ = _grid.GetWorldHeight(worldX, worldZ + sample);
            float hNegZ = _grid.GetWorldHeight(worldX, worldZ - sample);

            // Gradient: positive = uphill in that direction
            float slopeX = (hPosX - hNegX) / (sample * 2f);
            float slopeZ = (hPosZ - hNegZ) / (sample * 2f);
            return new Vector3(slopeX, 0, slopeZ);
        }

        private void HandleMovement(float dt)
        {
            var input = Vector3.Zero;
            if (Input.IsActionPressed("camera_pan_up")) input.Z -= 1;
            if (Input.IsActionPressed("camera_pan_down")) input.Z += 1;
            if (Input.IsActionPressed("camera_pan_left")) input.X -= 1;
            if (Input.IsActionPressed("camera_pan_right")) input.X += 1;

            // Check terrain slope for slip
            var pos = GlobalPosition;
            var slope = GetTerrainSlope(pos.X, pos.Z);
            float slopeMag = slope.Length();
            bool onSteepSlope = slopeMag > SLIP_SLOPE_THRESHOLD;

            if (onSteepSlope && !_isCasting && !_isNarutoRunning)
            {
                // Slipping! Play death/flail animation and slide downhill
                // (naruto run powers through minor slopes)
                if (!_isSlipping)
                {
                    _isSlipping = true;
                    _animator?.PlayCustom("Death");
                    _animator?.SetSpeed(1.8f); // Fast frantic flailing
                }

                // Slide downhill — slope vector points uphill, so negate it
                var slideDir = -slope.Normalized();
                float slideSpeed = SLIP_SLIDE_SPEED * Mathf.Clamp(slopeMag / SLIP_SLOPE_THRESHOLD, 1f, 2f);

                // Player input can fight the slide a bit but not fully overcome it
                if (input.LengthSquared() > 0)
                {
                    input = input.Normalized();
                    Velocity = (slideDir * slideSpeed + input * MoveSpeed * 0.3f);
                    float targetYaw = Mathf.Atan2(input.X, input.Z);
                    _smoothYaw = Mathf.LerpAngle(_smoothYaw, targetYaw, dt * 10f);
                }
                else
                {
                    Velocity = slideDir * slideSpeed;
                }
                _isMoving = true;
            }
            else
            {
                _isSlipping = false;

                if (input.LengthSquared() > 0)
                {
                    input = input.Normalized();
                    _isMoving = true;
                    _runTimer += dt;

                    // Bob walk → naruto run transition
                    bool wasNaruto = _isNarutoRunning;
                    _isNarutoRunning = _runTimer >= NARUTO_RUN_THRESHOLD;

                    float speed = MoveSpeed + (_isNarutoRunning ? NARUTO_SPEED_BONUS : 0f);
                    Velocity = input * speed;

                    if (!_isCasting)
                    {
                        if (_isNarutoRunning)
                        {
                            // Sprint uses the actual FBX Run clip (arms-back naruto run)
                            _animator?.PlayCustom("Run");
                            _animator?.SetSpeed(0.8f);
                        }
                        else
                        {
                            // Normal walk uses Idle skeleton clip + procedural bob
                            _animator?.PlayCustom("Idle");
                            _animator?.SetSpeed(0.7f);
                        }
                    }

                    // Smooth facing direction to prevent jitter
                    float targetYaw = Mathf.Atan2(input.X, input.Z);
                    _smoothYaw = Mathf.LerpAngle(_smoothYaw, targetYaw, dt * 10f);
                }
                else
                {
                    Velocity = Vector3.Zero;
                    _isMoving = false;
                    _runTimer = 0f;
                    _isNarutoRunning = false;
                    if (!_isCasting)
                    {
                        _animator?.PlayCustom("Idle");
                        _animator?.SetSpeed(0.4f); // Slow idle breathing
                    }
                }
            }

            MoveAndSlide();

            // Smooth Y position to terrain height
            float cs = Constants.VINE_CELL_SIZE;
            pos = GlobalPosition;
            pos.X = Mathf.Clamp(pos.X, 0, _grid.Width * cs);
            pos.Z = Mathf.Clamp(pos.Z, 0, _grid.Height * cs);
            float targetY = _grid.GetWorldHeight(pos.X, pos.Z);
            pos.Y = Mathf.Lerp(pos.Y, targetY, dt * 12f);
            GlobalPosition = pos;

            // Animate model
            if (_modelRoot != null)
            {
                _bounceTimer += dt * (_isMoving ? 3.5f : 0.8f);
                float phase = _bounceTimer * Mathf.Pi;

                if (_isSlipping)
                {
                    // Slipping: frantic wobble — fast erratic tilting
                    float wobbleSpeed = 8f;
                    float rollDeg = Mathf.Sin(phase * wobbleSpeed) * 25f;
                    float pitchDeg = Mathf.Cos(phase * wobbleSpeed * 1.3f) * 20f;
                    float sway = Mathf.Sin(phase * wobbleSpeed * 0.7f) * 0.15f;

                    _modelRoot.Position = new Vector3(sway, 0.05f, 0);
                    _modelRoot.Rotation = new Vector3(
                        Mathf.DegToRad(pitchDeg),
                        _smoothYaw,
                        Mathf.DegToRad(rollDeg)
                    );
                }
                else if (_isNarutoRunning)
                {
                    // Sprint — skeleton Run clip drives the arms-back naruto pose.
                    // We just apply facing direction + a subtle forward lean.
                    float leanDeg = SignalTuningEditor.NarutoForwardLean;
                    _modelRoot.Position = Vector3.Zero;
                    _modelRoot.Rotation = new Vector3(
                        Mathf.DegToRad(leanDeg),
                        _smoothYaw,
                        0
                    );
                }
                else
                {
                    // Normal: penguin teeter walk / idle
                    // Walk values from SignalTuningEditor for live tuning.
                    float bounceHeight = _isMoving ? SignalTuningEditor.WalkBounceHeight : 0.06f;
                    float bounce = Mathf.Abs(Mathf.Sin(phase)) * bounceHeight;
                    float sway = _isMoving ? Mathf.Sin(phase) * SignalTuningEditor.WalkSwayAmp : 0f;

                    _modelRoot.Position = new Vector3(sway, bounce, 0);

                    float rollDeg = _isMoving ? Mathf.Sin(phase) * SignalTuningEditor.WalkRollAmp : 0f;
                    float pitchDeg = _isMoving ? Mathf.Sin(phase * 2f) * 4f : 0f;
                    float idleRoll = _isMoving ? 0f : Mathf.Sin(phase * 0.5f) * 2f;

                    _modelRoot.Rotation = new Vector3(
                        Mathf.DegToRad(pitchDeg),
                        _smoothYaw,
                        Mathf.DegToRad(rollDeg + idleRoll)
                    );
                }
            }
        }

        private void TryAutoAttack()
        {
            var enemies = GetTree().GetNodesInGroup(Constants.GROUP_VINE_ENEMY);
            VineEnemy closest = null;
            float closestDist = AttackRange;

            foreach (var node in enemies)
            {
                if (node is not VineEnemy enemy || !enemy.IsAlive) continue;
                float dist = GlobalPosition.DistanceTo(enemy.GlobalPosition);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = enemy;
                }
            }

            if (closest == null) return;

            // Start cast wind-up — projectile fires when cast completes
            _isCasting = true;
            _castTarget = closest;
            _castTimer = 0f;
            // Cast duration scales inversely with attack speed: slower at low speed, faster at high
            // Base: 0.5s wind-up at 1.5 attack speed, minimum 0.15s at very high speed
            _castDuration = Mathf.Clamp(0.75f / AttackSpeed, 0.15f, 0.8f);

            // During naruto run, keep the run animation going — attacks fire without interrupting sprint
            if (!_isNarutoRunning)
            {
                var attackAnims = new[] { "Attack", "Attack_R", "Attack_L" };
                string pick = attackAnims[(int)GD.RandRange(0, attackAnims.Length - 0.01f)];
                _animator?.PlayCustom(pick);
                _animator?.SetSpeed(Mathf.Clamp(1f / (_castDuration * 2f), 0.3f, 2f));
            }
        }

        private void UpdateCastAnimation(float dt)
        {
            _castTimer += dt;
            float t = Mathf.Clamp(_castTimer / _castDuration, 0f, 1f);

            // Check if target died or went out of range during cast
            if (_castTarget == null || !IsInstanceValid(_castTarget) || !_castTarget.IsAlive)
            {
                _isCasting = false;
                _attackCooldown = 0.2f; // Brief delay before retargeting
                return;
            }

            // During naruto run, skip cast pose — let the sprint animation drive the body
            if (!_isNarutoRunning)
            {
                // Face target smoothly during cast
                if (_modelRoot != null)
                {
                    var dir = (_castTarget.GlobalPosition - GlobalPosition).Normalized();
                    float targetYaw = Mathf.Atan2(dir.X, dir.Z);
                    _smoothYaw = Mathf.LerpAngle(_smoothYaw, targetYaw, dt * 8f);
                }

                // Cast animation: lean back (wind-up) then thrust forward (release)
                if (_modelRoot != null)
                {
                    float pitchDeg;
                    float scalePulse;
                    if (t < 0.6f)
                    {
                        // Wind-up: lean back, scrunch down slightly
                        float windUp = t / 0.6f;
                        pitchDeg = -12f * Mathf.Sin(windUp * Mathf.Pi * 0.5f); // Lean back
                        scalePulse = 1f - 0.05f * windUp; // Slight crouch
                    }
                    else
                    {
                        // Release: thrust forward sharply
                        float release = (t - 0.6f) / 0.4f;
                        pitchDeg = Mathf.Lerp(-12f, 20f, release); // Snap forward
                        scalePulse = 1f + 0.08f * Mathf.Sin(release * Mathf.Pi); // Pop up
                    }

                    _modelRoot.Rotation = new Vector3(
                        Mathf.DegToRad(pitchDeg),
                        _smoothYaw,
                        _modelRoot.Rotation.Z
                    );
                    _modelRoot.Scale = Vector3.One * scalePulse;
                }
            }

            // Fire when cast completes
            if (t >= 1f)
            {
                _isCasting = false;
                _attackCooldown = 1f / AttackSpeed;
                if (_modelRoot != null) _modelRoot.Scale = Vector3.One; // Reset scale

                // Fire projectile VFX
                VfxFactory.SpawnProjectile(GetTree(), GlobalPosition + Vector3.Up * 0.5f,
                    _castTarget.GlobalPosition + Vector3.Up * 0.5f,
                    PlanetTheme.Current.ProjectileColor);

                // Deal damage
                float prevHP = _castTarget.CurrentHealth;
                _castTarget.TakeDamage(AttackDamage);
                if (!_castTarget.IsAlive && prevHP > 0)
                    EnemiesKilledPersonally++;

                _castTarget = null;
            }
        }

        private void TryUseAbility(int slot)
        {
            if (slot < 0 || slot >= _abilities.Length) return;
            var ability = _abilities[slot];
            if (!ability.IsReady) return;
            if (CurrentMaterials < ability.MaterialsCost) return;

            CurrentMaterials -= ability.MaterialsCost;
            ability.CurrentCooldown = ability.Cooldown;
            ability.Execute?.Invoke(this);

            if (ServiceLocator.TryGet<AudioManager>(out var audio))
            {
                string sfx = slot switch { 0 => "shock_blast", 1 => "repair_pulse", 2 => "overclock", _ => null };
                if (sfx != null) audio.PlaySFXByName(sfx);
            }

            GameEvents.OnPlayerMaterialsChanged?.Invoke(CurrentMaterials, MaxMaterials);
            GameEvents.OnAbilityCooldownChanged?.Invoke(slot, ability.CurrentCooldown);
        }

        public void TakeDamage(float amount)
        {
            if (!IsAlive || _isDead) return;
            CurrentHP = Mathf.Max(0, CurrentHP - amount);

            _flashTimer = 0.12f;
            SetFlash(true);

            if (ServiceLocator.TryGet<TDCamera>(out var cam))
                cam.Shake(0.3f, 0.2f);

            GameEvents.OnPlayerHPChanged?.Invoke(CurrentHP, MaxHP);

            if (CurrentHP <= 0)
                Die();
        }

        private void Die()
        {
            _isDead = true;
            _respawnTimer = Constants.VINE_PLAYER_RESPAWN_TIME;
            _animator?.SetState(AnimState.Death);

            // Death VFX
            VfxFactory.SpawnDeathBurst(GetTree(), GlobalPosition, new Color(0.2f, 0.6f, 1f), 8);

            // Penalty damage to harvester
            if (_grid?.Harvester != null && !_grid.Harvester.IsDestroyed)
                _grid.Harvester.TakeDamage(Constants.VINE_PLAYER_DEATH_PENALTY);

            // Hide visual
            if (_modelRoot != null) _modelRoot.Visible = false;
            if (_healthBar != null) _healthBar.Visible = false;
            if (_healthBarBg != null) _healthBarBg.Visible = false;

            GameEvents.OnPlayerDied?.Invoke();
        }

        private void Respawn()
        {
            _isDead = false;
            CurrentHP = MaxHP;
            CurrentMaterials = MaxMaterials;

            // Respawn at harvester
            if (_grid?.Harvester != null)
                GlobalPosition = _grid.Harvester.GlobalPosition + new Vector3(-4f, 0, 0);

            // Show visual
            if (_modelRoot != null) _modelRoot.Visible = true;
            if (_healthBar != null) _healthBar.Visible = true;
            if (_healthBarBg != null) _healthBarBg.Visible = true;

            GameEvents.OnPlayerHPChanged?.Invoke(CurrentHP, MaxHP);
            GameEvents.OnPlayerMaterialsChanged?.Invoke(CurrentMaterials, MaxMaterials);
        }

        // ── Emergence Animation ──

        /// <summary>
        /// Hide BIT and prepare for emergence from the Spire.
        /// Call this before the intro sequence starts.
        /// </summary>
        public void HideForIntro()
        {
            if (_modelRoot != null) _modelRoot.Visible = false;
            if (_healthBar != null) _healthBar.Visible = false;
            if (_healthBarBg != null) _healthBarBg.Visible = false;
            // Park at the Spire position (invisible)
            if (_grid?.Harvester != null)
                GlobalPosition = _grid.Harvester.GlobalPosition;
        }

        /// <summary>
        /// Start the emergence animation — BIT walks out from the Spire base.
        /// Scales up from tiny to full size while moving outward.
        /// </summary>
        public void StartEmergence(float duration = 1.5f)
        {
            if (_grid?.Harvester == null) return;

            _emerging = true;
            _emergeTimer = 0f;
            _emergeDuration = duration;
            _emergeStart = _grid.Harvester.GlobalPosition;
            _emergeEnd = _emergeStart + new Vector3(-4f, 0, 0);

            // Start at Spire center, tiny
            GlobalPosition = _emergeStart;
            if (_modelRoot != null)
            {
                _modelRoot.Visible = true;
                _modelRoot.Scale = Vector3.One * 0.01f;
            }
            if (_healthBar != null) _healthBar.Visible = false;
            if (_healthBarBg != null) _healthBarBg.Visible = false;

            // Energy burst at Spire base when BIT emerges
            VfxFactory.SpawnDeathBurst(GetTree(), _emergeStart + new Vector3(0, 0.5f, 0),
                BitPalette.AccentBright, 6);

            GD.Print("[BIT] Emergence started");
        }

        private void UpdateEmergence(float dt)
        {
            _emergeTimer += dt;
            float t = Mathf.Clamp(_emergeTimer / _emergeDuration, 0f, 1f);

            // Ease-out for smooth deceleration
            float eased = 1f - (1f - t) * (1f - t);

            // Move from Spire center to standing position
            GlobalPosition = _emergeStart.Lerp(_emergeEnd, eased);

            // Snap Y to terrain
            if (_grid != null)
                GlobalPosition = new Vector3(
                    GlobalPosition.X,
                    _grid.GetWorldHeight(GlobalPosition.X, GlobalPosition.Z),
                    GlobalPosition.Z);

            // Scale up from tiny to full size
            float scaleT = Mathf.Clamp(t / 0.6f, 0f, 1f); // Reach full size at 60% of duration
            float scaleEased = 1f - (1f - scaleT) * (1f - scaleT);
            float scale = Mathf.Lerp(0.01f, _baseModelScale, scaleEased);
            if (_modelRoot != null)
                _modelRoot.Scale = Vector3.One * scale;

            if (t >= 1f)
            {
                _emerging = false;
                if (_modelRoot != null) _modelRoot.Scale = Vector3.One * _baseModelScale;
                if (_healthBar != null) _healthBar.Visible = true;
                if (_healthBarBg != null) _healthBarBg.Visible = true;

                // Small arrival burst
                VfxFactory.SpawnDeathBurst(GetTree(), GlobalPosition + new Vector3(0, 0.5f, 0),
                    BitPalette.Accent, 4);

                GD.Print("[BIT] Emergence complete — player control active");
            }
        }

        // ── Visuals ──

        private void BuildVisual()
        {
            _modelRoot = AssetLibrary.InstantiateNormalized(AssetLibrary.COMPANION_BIT);
            if (_modelRoot != null)
            {
                AddChild(_modelRoot);
                AssetLibrary.GroundModel(_modelRoot);
                _baseModelScale = _modelRoot.Scale.X;

                // Initialize animator first (it finds the AnimationPlayer)
                _animator = new CharacterAnimator();
                AddChild(_animator);
                _animator.Initialize(_modelRoot);

                // Defer both split and material apply — FBX meshes may not be fully loaded yet
                CallDeferred(MethodName._DeferredBitSetup);
            }
            else
            {
                // Fallback: simple placeholder sphere
                _modelRoot = new Node3D();
                AddChild(_modelRoot);
                var sphere = new MeshInstance3D();
                sphere.Mesh = new SphereMesh { Radius = 0.4f, Height = 0.8f };
                sphere.Position = new Vector3(0, 0.5f, 0);
                var mat = AssetLibrary.MakeEmissive(new Color(0.2f, 0.7f, 1.0f));
                sphere.MaterialOverride = mat;
                _modelRoot.AddChild(sphere);
                GD.PushWarning("[VinePlayer] bit.fbx not found, using fallback sphere");
            }
        }

        private void _DeferredBitSetup()
        {
            if (_modelRoot == null) return;
            SplitBitAnimations(_modelRoot);
            _ApplyBitSilverWhite();

            // Re-initialize animator now that split clips exist —
            // the pre-split init mapped Walk/Run to the monolithic ArmatureAction
            _animator?.Initialize(_modelRoot);

            // Try playing idle to confirm animations work
            _animator?.PlayCustom("Idle");

            // Cache skeleton and arm bone indices for naruto run pose
            _skeleton = FindSkeleton(_modelRoot);
            if (_skeleton != null)
            {
                _armRIdx = _skeleton.FindBone("Arm.R");
                _armLIdx = _skeleton.FindBone("Arm.L");
                GD.Print($"[VinePlayer] Skeleton cached: Arm.R={_armRIdx}, Arm.L={_armLIdx}");
            }

            GD.Print("[VinePlayer] Deferred BIT setup complete");
        }

        /// <summary>
        /// BIT's FBX has all animations baked into one "ArmatureAction" timeline.
        /// Equal-division split into 6 named segments.
        /// </summary>
        internal static void SplitBitAnimations(Node3D modelRoot, bool keepOriginal = false, bool force = false)
        {
            // BIT has 6 animation segments with hardcoded boundaries.
            // keepOriginal=true preserves the monolithic clip for the editor's raw timeline.
            // force=true bypasses the "already split" check (used by editor with uncached models).
            var animPlayer = FindAnimPlayerInTree(modelRoot);
            if (animPlayer == null) return;

            // Already split? Check if "Run" clip exists and has valid length
            if (!force && animPlayer.HasAnimation("Run"))
            {
                var existing = animPlayer.GetAnimation("Run");
                if (existing != null && (float)existing.Length > 0.5f)
                {
                    GD.Print("[VinePlayer] BIT already split — skipping");
                    return;
                }
            }

            Animation sourceAnim = null;
            string sourceAnimName = null;
            Animation longestAnim = null;
            string longestName = null;
            float longestLen = 0;

            foreach (var name in animPlayer.GetAnimationList())
            {
                string lower = name.ToLower();
                var anim = animPlayer.GetAnimation(name);
                if (anim == null) continue;
                float len = (float)anim.Length;

                // Skip PoseLib, RESET, and tiny clips
                if (lower.Contains("poselib") || lower.Contains("reset") || len < 0.1f)
                    continue;

                // Prefer "Action" name (the monolithic combined clip)
                if (lower.Contains("action"))
                {
                    sourceAnim = anim;
                    sourceAnimName = name;
                    break;
                }

                // Track longest as fallback
                if (len > longestLen)
                {
                    longestLen = len;
                    longestAnim = anim;
                    longestName = name;
                }
            }

            // Fallback to longest clip if no "Action" found
            if (sourceAnim == null && longestAnim != null)
            {
                sourceAnim = longestAnim;
                sourceAnimName = longestName;
            }

            if (sourceAnim == null || (float)sourceAnim.Length < 1f)
            {
                GD.Print("[VinePlayer] No valid monolithic animation found for BIT");
                GD.Print($"[VinePlayer] Available: {string.Join(", ", animPlayer.GetAnimationList())}");
                return;
            }

            float totalLength = (float)sourceAnim.Length;
            int trackCount = sourceAnim.GetTrackCount();
            GD.Print($"[VinePlayer] BIT anim '{sourceAnimName}': {totalLength:F2}s, {trackCount} tracks");

            // Dump keyframe gaps to find real segment boundaries
            for (int t = 0; t < Mathf.Min(trackCount, 3); t++)
            {
                var tt = sourceAnim.TrackGetType(t);
                string tp = sourceAnim.TrackGetPath(t);
                int kc = sourceAnim.TrackGetKeyCount(t);
                if (kc < 2) continue;
                var gaps = new System.Text.StringBuilder();
                float prev = (float)sourceAnim.TrackGetKeyTime(t, 0);
                for (int k = 1; k < kc; k++)
                {
                    float kt = (float)sourceAnim.TrackGetKeyTime(t, k);
                    if (kt - prev > 0.08f)
                        gaps.Append($" {prev:F3}s(+{kt-prev:F3})");
                    prev = kt;
                }
                GD.Print($"[VinePlayer]   Track{t}({tt}) '{tp}' keys={kc} range={sourceAnim.TrackGetKeyTime(t,0):F3}-{sourceAnim.TrackGetKeyTime(t,kc-1):F3} GAPS:{gaps}");
            }

            // Hardcoded segment boundaries — user-verified timings from BIT's FBX
            var segments = new (string name, float start, float end)[]
            {
                ("Idle",       0f,     3.17f),
                ("Run",        3.17f,  4.13f),
                ("Attack_R",   4.13f,  5.07f),
                ("Attack_L",   5.07f,  5.90f),
                ("Attack",     5.90f,  6.73f),
                ("Death",      6.73f,  totalLength),
            };

            AnimationLibrary lib;
            if (animPlayer.HasAnimationLibrary(""))
                lib = animPlayer.GetAnimationLibrary("");
            else { lib = new AnimationLibrary(); animPlayer.AddAnimationLibrary("", lib); }

            foreach (var (name, start, end) in segments)
            {
                var clip = new Animation();
                clip.Length = end - start;

                for (int t = 0; t < trackCount; t++)
                {
                    var trackType = sourceAnim.TrackGetType(t);
                    if (trackType == Animation.TrackType.Scale3D) continue;
                    int newIdx = clip.AddTrack(trackType);
                    clip.TrackSetPath(newIdx, sourceAnim.TrackGetPath(t));
                    clip.TrackSetInterpolationType(newIdx, sourceAnim.TrackGetInterpolationType(t));
                    int keyCount = sourceAnim.TrackGetKeyCount(t);
                    for (int k = 0; k < keyCount; k++)
                    {
                        float keyTime = (float)sourceAnim.TrackGetKeyTime(t, k);
                        if (keyTime < start || keyTime >= end - 0.01f) continue;
                        clip.TrackInsertKey(newIdx, keyTime - start, sourceAnim.TrackGetKeyValue(t, k));
                    }
                }

                if (name == "Idle" || name == "Run")
                {
                    clip.LoopMode = Animation.LoopModeEnum.Linear;
                    // Duplicate first keyframe at the end of each track so the
                    // animation interpolates smoothly back to the start pose.
                    // Without this, the loop wrap creates a visible pop/reset.
                    for (int ti = 0; ti < clip.GetTrackCount(); ti++)
                    {
                        if (clip.TrackGetKeyCount(ti) > 0)
                        {
                            var firstKey = clip.TrackGetKeyValue(ti, 0);
                            clip.TrackInsertKey(ti, clip.Length, firstKey);
                        }
                    }
                }

                if (lib.HasAnimation(name)) lib.RemoveAnimation(name);
                lib.AddAnimation(name, clip);
                GD.Print($"[VinePlayer]   {name}: {start:F2}s-{end:F2}s ({clip.Length:F2}s)");
            }

            // Remove original monolithic animation (unless editor wants to keep it for raw timeline)
            if (!keepOriginal)
            {
                string baseName = sourceAnimName.Contains("|") ? sourceAnimName.Split('|')[1] : sourceAnimName;
                if (lib.HasAnimation(sourceAnimName)) lib.RemoveAnimation(sourceAnimName);
                if (lib.HasAnimation(baseName)) lib.RemoveAnimation(baseName);
                foreach (var libName in animPlayer.GetAnimationLibraryList())
                {
                    if (libName == "") continue;
                    var otherLib = animPlayer.GetAnimationLibrary(libName);
                    if (otherLib.HasAnimation(baseName)) otherLib.RemoveAnimation(baseName);
                }
            }
            GD.Print($"[VinePlayer] BIT split done. Anims: {string.Join(", ", animPlayer.GetAnimationList())}");
        }

        private static AnimationPlayer FindAnimPlayerInTree(Node root)
        {
            if (root is AnimationPlayer ap) return ap;
            foreach (var child in root.GetChildren())
            {
                var found = FindAnimPlayerInTree(child);
                if (found != null) return found;
            }
            return null;
        }

        private static Skeleton3D FindSkeleton(Node root)
        {
            if (root is Skeleton3D s) return s;
            foreach (var child in root.GetChildren())
            {
                var found = FindSkeleton(child);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// Build both material sets for BIT:
        /// 1. Silver-white "dome" materials (inside dome — the real BIT)
        /// 2. Planet-themed materials (outside dome — Tron/Scrapyard look)
        /// Applies dome materials immediately since BIT starts inside.
        /// </summary>
        private void _ApplyBitSilverWhite()
        {
            if (_modelRoot == null) return;

            var eyeTex = GD.Load<Texture2D>("res://Models/Characters/Companions/textures/LilRobotEyes.png")
                ?? GD.Load<Texture2D>("res://Models/Characters/Companions/LilRobotEyes.png");

            bool isScrapyard = PlanetTheme.Current is ScrapyardPlanetTheme;
            var planetAccent = isScrapyard ? new Color(0.9f, 0.6f, 0.1f) : new Color(0.0f, 0.85f, 0.95f);

            var meshes = _modelRoot.FindChildren("*", "MeshInstance3D", true, false);
            _domeMaterials.Clear();
            _themedMaterials.Clear();

            foreach (var node in meshes)
            {
                if (node is not MeshInstance3D mesh) continue;
                if (mesh.Mesh == null) { mesh.Visible = false; continue; }

                string meshName = mesh.Name.ToString().ToLower();
                bool isEye = meshName.Contains("eye");

                // ── Dome material (silver-white, inside dome) ──
                if (isEye)
                {
                    var eyeMat = new StandardMaterial3D();
                    eyeMat.AlbedoColor = new Color(0.5f, 0.8f, 1.0f);
                    if (eyeTex != null) eyeMat.AlbedoTexture = eyeTex;
                    eyeMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                    eyeMat.EmissionEnabled = true;
                    eyeMat.Emission = new Color(0.5f, 0.8f, 1.0f);
                    eyeMat.EmissionEnergyMultiplier = 4.0f;
                    _domeMaterials[mesh] = eyeMat;
                }
                else
                {
                    // Dark hull body with bright white outline — BIT stands out against dome floor
                    var bodyMat = new StandardMaterial3D();
                    bodyMat.AlbedoColor = BitPalette.BodyDark;
                    bodyMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;

                    // White emissive outline (inverted hull)
                    var outlineShader = new Shader();
                    outlineShader.Code = @"
shader_type spatial;
render_mode unshaded, cull_front;
uniform vec3 outline_color : source_color = vec3(0.92, 0.94, 1.0);
uniform float outline_width = 0.035;
void vertex() {
    float scale = length(MODEL_MATRIX[0].xyz);
    VERTEX += NORMAL * (outline_width / max(scale, 0.001));
}
void fragment() { ALBEDO = outline_color; ALPHA = 0.95; }
";
                    var outlineMat = new ShaderMaterial();
                    outlineMat.Shader = outlineShader;
                    outlineMat.SetShaderParameter("outline_color", new Vector3(0.92f, 0.94f, 1.0f));
                    outlineMat.SetShaderParameter("outline_width", 0.035f);
                    outlineMat.RenderPriority = -1;
                    bodyMat.NextPass = outlineMat;

                    _domeMaterials[mesh] = bodyMat;
                }

                // ── Planet-themed material (outside dome) ──
                if (isEye)
                {
                    var eyeThemed = new StandardMaterial3D();
                    eyeThemed.AlbedoColor = planetAccent;
                    eyeThemed.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                    eyeThemed.EmissionEnabled = true;
                    eyeThemed.Emission = planetAccent;
                    eyeThemed.EmissionEnergyMultiplier = 2f;
                    _themedMaterials[mesh] = eyeThemed;
                }
                else
                {
                    var bodyThemed = new StandardMaterial3D();
                    bodyThemed.AlbedoColor = new Color(0.02f, 0.02f, 0.03f);
                    bodyThemed.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                    bodyThemed.EmissionEnabled = true;
                    bodyThemed.Emission = planetAccent;
                    bodyThemed.EmissionEnergyMultiplier = isScrapyard ? 0.15f : 0.3f;

                    // Tron gets outline shader
                    if (!isScrapyard)
                    {
                        var outlineShader = new Shader();
                        outlineShader.Code = @"
shader_type spatial;
render_mode unshaded, cull_front;
uniform vec3 outline_color : source_color = vec3(0.0, 0.85, 0.95);
uniform float outline_width = 0.03;
void vertex() {
    float scale = length(MODEL_MATRIX[0].xyz);
    VERTEX += NORMAL * (outline_width / max(scale, 0.001));
}
void fragment() { ALBEDO = outline_color; ALPHA = 0.9; }
";
                        var outlineMat = new ShaderMaterial();
                        outlineMat.Shader = outlineShader;
                        outlineMat.SetShaderParameter("outline_color",
                            new Vector3(planetAccent.R, planetAccent.G, planetAccent.B));
                        outlineMat.SetShaderParameter("outline_width", 0.03f);
                        bodyThemed.NextPass = outlineMat;
                    }

                    _themedMaterials[mesh] = bodyThemed;
                }
            }

            // Apply dome materials — BIT starts inside dome
            foreach (var (mesh, mat) in _domeMaterials)
                if (IsInstanceValid(mesh)) mesh.MaterialOverride = mat;
            _isInsideDome = true;

            // Point light — makes BIT visible and glowing against the dome floor
            var light = new OmniLight3D();
            light.LightColor = new Color(0.9f, 0.93f, 1.0f);
            light.LightEnergy = 0.6f;
            light.OmniRange = 4f;
            light.OmniAttenuation = 2f;
            light.ShadowEnabled = false;
            light.Position = new Vector3(0, 1.2f, 0);
            AddChild(light);

            GD.Print($"[VinePlayer] BIT materials built: {_domeMaterials.Count} dome + {_themedMaterials.Count} themed");
        }

        /// <summary>
        /// Swap BIT materials based on dome position.
        /// Inside = silver-white. Outside = planet-themed.
        /// </summary>
        private void UpdateDomeMaterials()
        {
            if (_modelRoot == null || _domeMaterials.Count == 0) return;

            ConversionDome dome = null;
            var parent = GetParent();
            if (parent != null)
            {
                foreach (var child in parent.GetChildren())
                    if (child is ConversionDome d) { dome = d; break; }
            }
            if (dome == null) return;

            bool inside = dome.IsInsideDome(GlobalPosition);
            if (inside == _isInsideDome) return;
            _isInsideDome = inside;

            var source = inside ? _domeMaterials : _themedMaterials;
            foreach (var (mesh, mat) in source)
                if (IsInstanceValid(mesh)) mesh.MaterialOverride = mat;
        }

        private void BuildHealthBar()
        {
            float barY = 1.8f;

            _healthBarBg = new MeshInstance3D();
            _healthBarBg.Mesh = new BoxMesh { Size = new Vector3(0.8f, 0.08f, 0.08f) };
            _healthBarBg.Position = new Vector3(0, barY, 0);
            var bgMat = new StandardMaterial3D();
            bgMat.AlbedoColor = new Color(0.15f, 0.15f, 0.15f);
            bgMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            _healthBarBg.MaterialOverride = bgMat;
            AddChild(_healthBarBg);

            _healthBar = new MeshInstance3D();
            _healthBar.Mesh = new BoxMesh { Size = new Vector3(0.8f, 0.06f, 0.06f) };
            _healthBar.Position = new Vector3(0, barY, 0);
            var barMat = new StandardMaterial3D();
            barMat.AlbedoColor = new Color(0.2f, 0.7f, 1.0f);
            barMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            _healthBar.MaterialOverride = barMat;
            AddChild(_healthBar);
        }

        private void UpdateHealthBar()
        {
            if (_healthBar == null) return;
            float pct = Mathf.Clamp(CurrentHP / MaxHP, 0f, 1f);
            float halfBar = 0.4f;
            _healthBar.Scale = new Vector3(pct, 1, 1);
            _healthBar.Position = new Vector3((pct - 1f) * halfBar, _healthBar.Position.Y, 0);

            if (_healthBar.MaterialOverride is StandardMaterial3D mat)
                mat.AlbedoColor = pct > 0.5f ? new Color(0.2f, 0.7f, 1.0f) :
                    pct > 0.25f ? new Color(0.9f, 0.7f, 0.1f) :
                    new Color(0.9f, 0.1f, 0.1f);
        }

        private void SetFlash(bool flash)
        {
            if (_modelRoot == null) return;
            SetFlashRecursive(_modelRoot, flash);
        }

        private static void SetFlashRecursive(Node node, bool flash)
        {
            if (node is MeshInstance3D mesh && mesh.MaterialOverride is StandardMaterial3D mat)
            {
                if (flash)
                {
                    mat.EmissionEnabled = true;
                    mat.Emission = Colors.White;
                    mat.EmissionEnergyMultiplier = 1.2f;
                }
                else
                {
                    // Restore BIT's silver-white subtle emission
                    mat.Emission = new Color(0.92f, 0.94f, 0.97f);
                    mat.EmissionEnergyMultiplier = 0.06f;
                }
            }
            foreach (var child in node.GetChildren())
                SetFlashRecursive(child, flash);
        }

        // ── Abilities ──

        internal static VinePlayerAbility[] GetDefaultAbilities()
        {
            return new VinePlayerAbility[]
            {
                new VinePlayerAbility {
                    Name = "Shock Blast",
                    Description = "AoE damage around player",
                    Cooldown = 4f, MaterialsCost = 15f, Range = 5f,
                    IconColor = new Color(0.9f, 0.8f, 0.2f),
                    Execute = player => {
                        // AoE damage around player
                        var enemies = player.GetTree().GetNodesInGroup(Constants.GROUP_VINE_ENEMY);
                        foreach (var node in enemies)
                        {
                            if (node is not VineEnemy enemy || !enemy.IsAlive) continue;
                            float dist = player.GlobalPosition.DistanceTo(enemy.GlobalPosition);
                            if (dist < 5f)
                            {
                                float prevHP = enemy.CurrentHealth;
                                enemy.TakeDamage(25f);
                                if (!enemy.IsAlive && prevHP > 0)
                                    player.EnemiesKilledPersonally++;
                            }
                        }
                        VfxFactory.SpawnDeathBurst(player.GetTree(), player.GlobalPosition + Vector3.Up * 0.5f,
                            new Color(0.9f, 0.8f, 0.2f), 10);
                    }
                },
                new VinePlayerAbility {
                    Name = "Repair Pulse",
                    Description = "Heal the harvester",
                    Cooldown = 8f, MaterialsCost = 25f, Range = 12f,
                    IconColor = new Color(0.2f, 0.9f, 0.4f),
                    Execute = player => {
                        var grid = ServiceLocator.Get<VineGrid>();
                        if (grid?.Harvester != null)
                        {
                            grid.Harvester.Heal(40f);
                            VfxFactory.SpawnDeathBurst(player.GetTree(),
                                grid.Harvester.GlobalPosition + Vector3.Up * 2f,
                                new Color(0.2f, 0.9f, 0.4f), 8);
                        }
                    }
                },
                new VinePlayerAbility {
                    Name = "Overclock",
                    Description = "Boost all towers in radius for 5s",
                    Cooldown = 15f, MaterialsCost = 40f, Range = 8f,
                    IconColor = new Color(0.6f, 0.3f, 0.9f),
                    Execute = player => {
                        // Boost towers in radius — buff their damage
                        var towers = player.GetTree().GetNodesInGroup(Constants.GROUP_VINE_NODE);
                        foreach (var node in towers)
                        {
                            if (node is not VineNode vine) continue;
                            if (vine.Data?.Category != VineNodeCategory.Effect) continue;
                            float dist = player.GlobalPosition.DistanceTo(vine.GlobalPosition);
                            if (dist < 8f)
                                vine.ReceiveBuff(1.5f); // Strong buff
                        }
                        VfxFactory.SpawnDeathBurst(player.GetTree(), player.GlobalPosition + Vector3.Up * 0.5f,
                            new Color(0.6f, 0.3f, 0.9f), 8);
                    }
                }
            };
        }

        // ── Ascendant Inhabit API ──

        /// <summary>Get current ability array (for saving before inhabit).</summary>
        public VinePlayerAbility[] GetAbilities() => _abilities;

        /// <summary>Replace ability array (used by AscendantInhabit).</summary>
        public void SetAbilities(VinePlayerAbility[] abilities)
        {
            _abilities = abilities;
            // Refresh HUD ability display
            for (int i = 0; i < _abilities.Length; i++)
                GameEvents.OnAbilityCooldownChanged?.Invoke(i, 0);
        }

        /// <summary>Show/hide BIT's visual model (hidden while inhabiting Ascendant).</summary>
        public void SetVisible(bool visible)
        {
            if (_modelRoot != null) _modelRoot.Visible = visible;
        }

        public override void _ExitTree()
        {
            ServiceLocator.Unregister<VinePlayer>();
        }
    }
}
