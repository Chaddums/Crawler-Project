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
        public float ManaCost;
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
        public float CurrentHP { get; private set; }
        public float MaxMana { get; set; }
        public float CurrentMana { get; private set; }
        public float MoveSpeed { get; set; } = Constants.VINE_PLAYER_MOVE_SPEED;
        public float AttackRange { get; set; } = Constants.VINE_PLAYER_ATTACK_RANGE;
        public float AttackDamage { get; set; }
        public float AttackSpeed { get; set; }
        public float ManaRegen { get; set; }
        public bool IsAlive => CurrentHP > 0;
        public int EnemiesKilledPersonally { get; set; }
        public Node3D ModelRoot => _modelRoot;
        internal float _baseModelScale = 1f; // Set during BuildVisual, read by SignalTuningEditor

        private float _attackCooldown;
        private float _respawnTimer;
        private bool _isDead;
        private VineGrid _grid;

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

        // Attack cast animation
        private float _castTimer;      // Counts up during wind-up
        private float _castDuration;   // Total wind-up time before projectile fires
        private VineEnemy _castTarget;  // Locked target during cast
        private bool _isCasting;

        // Abilities
        private VinePlayerAbility[] _abilities;

        public override void _Ready()
        {
            // Apply meta perk bonuses from SignalTuningEditor statics
            MaxHP = Constants.VINE_PLAYER_MAX_HP + SignalTuningEditor.PlayerMaxHPBonus;
            MaxMana = Constants.VINE_PLAYER_MAX_MANA + SignalTuningEditor.PlayerMaxManaBonus;
            AttackDamage = Constants.VINE_PLAYER_ATTACK_DAMAGE * SignalTuningEditor.PlayerAttackDamageMult;
            AttackSpeed = Constants.VINE_PLAYER_ATTACK_SPEED * SignalTuningEditor.PlayerAttackSpeedMult;
            ManaRegen = Constants.VINE_PLAYER_MANA_REGEN * SignalTuningEditor.PlayerManaRegenMult;

            CurrentHP = MaxHP;
            CurrentMana = MaxMana;

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
            GameEvents.OnPlayerManaChanged?.Invoke(CurrentMana, MaxMana);
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;

            // Respawn timer
            if (_isDead)
            {
                _respawnTimer -= dt;
                if (_respawnTimer <= 0)
                    Respawn();
                return;
            }

            // Movement — only during wave phase (build phase uses WASD for camera)
            var phase = GameManager.Instance?.CurrentPhase ?? GamePhase.Build;
            if (phase == GamePhase.Wave || phase == GamePhase.WaveComplete)
                HandleMovement(dt);

            // Mana regen
            if (CurrentMana < MaxMana)
            {
                CurrentMana = Mathf.Min(MaxMana, CurrentMana + ManaRegen * dt);
                GameEvents.OnPlayerManaChanged?.Invoke(CurrentMana, MaxMana);
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
                    _abilities[i].CurrentCooldown -= dt;
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
        private const float NARUTO_RUN_THRESHOLD = 2f; // Seconds before naruto run kicks in
        private const float NARUTO_SPEED_BONUS = 0.2f;
        private bool _isNarutoRunning;
        private bool _narutoForward = true; // Pingpong direction

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

            if (onSteepSlope && !_isCasting)
            {
                // Slipping! Play death/flail animation and slide downhill
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
                            // Naruto run — looping clip
                            _animator?.PlayCustom("Run");
                            _animator?.SetSpeed(0.8f);
                        }
                        else
                        {
                            // Cute bob walk — use idle anim (the sway) at walk speed
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
                    // Naruto run: let skeleton animation drive body motion
                    // Only apply facing direction + very subtle forward lean
                    _modelRoot.Position = new Vector3(0, 0, 0);
                    _modelRoot.Rotation = new Vector3(
                        Mathf.DegToRad(6f), // Slight constant forward lean
                        _smoothYaw,
                        0
                    );
                }
                else
                {
                    // Normal: penguin teeter walk / idle
                    float bounceHeight = _isMoving ? 0.15f : 0.06f;
                    float bounce = Mathf.Abs(Mathf.Sin(phase)) * bounceHeight;
                    float sway = _isMoving ? Mathf.Sin(phase) * 0.08f : 0f;

                    _modelRoot.Position = new Vector3(sway, bounce, 0);

                    float rollDeg = _isMoving ? Mathf.Sin(phase) * 12f : 0f;
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

            // Randomly pick between the 3 attack animations for variety
            var attackAnims = new[] { "Attack", "Attack_R", "Attack_L" };
            string pick = attackAnims[(int)GD.RandRange(0, attackAnims.Length - 0.01f)];
            _animator?.PlayCustom(pick);
            _animator?.SetSpeed(Mathf.Clamp(1f / (_castDuration * 2f), 0.3f, 2f));
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

            // Fire when cast completes
            if (t >= 1f)
            {
                _isCasting = false;
                _attackCooldown = 1f / AttackSpeed;
                _modelRoot.Scale = Vector3.One; // Reset scale

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
            if (CurrentMana < ability.ManaCost) return;

            CurrentMana -= ability.ManaCost;
            ability.CurrentCooldown = ability.Cooldown;
            ability.Execute?.Invoke(this);

            GameEvents.OnPlayerManaChanged?.Invoke(CurrentMana, MaxMana);
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
            CurrentMana = MaxMana;

            // Respawn at harvester
            if (_grid?.Harvester != null)
                GlobalPosition = _grid.Harvester.GlobalPosition + new Vector3(-4f, 0, 0);

            // Show visual
            if (_modelRoot != null) _modelRoot.Visible = true;
            if (_healthBar != null) _healthBar.Visible = true;
            if (_healthBarBg != null) _healthBarBg.Visible = true;

            GameEvents.OnPlayerHPChanged?.Invoke(CurrentHP, MaxHP);
            GameEvents.OnPlayerManaChanged?.Invoke(CurrentMana, MaxMana);
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

            // Try playing idle to confirm animations work
            _animator?.PlayCustom("Idle");
            GD.Print("[VinePlayer] Deferred BIT setup complete");
        }

        /// <summary>
        /// BIT's FBX has all animations baked into one "ArmatureAction" timeline.
        /// Equal-division split into 6 named segments.
        /// </summary>
        private static void SplitBitAnimations(Node3D modelRoot)
        {
            // BIT has 6 animation segments — always use equal-division split
            // BIT's animation order: Idle, Run, Attack_R, Attack_L, Attack, Death
            var animPlayer = FindAnimPlayerInTree(modelRoot);
            if (animPlayer == null) return;

            Animation sourceAnim = null;
            string sourceAnimName = null;
            foreach (var name in animPlayer.GetAnimationList())
            {
                string lower = name.ToLower();
                if (lower.Contains("action") || lower.Contains("armature"))
                { sourceAnim = animPlayer.GetAnimation(name); sourceAnimName = name; break; }
            }
            if (sourceAnim == null) return;

            float totalLength = (float)sourceAnim.Length;
            int trackCount = sourceAnim.GetTrackCount();
            GD.Print($"[VinePlayer] BIT anim '{sourceAnimName}': {totalLength:F2}s, {trackCount} tracks");

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
                    clip.LoopMode = Animation.LoopModeEnum.Linear;

                if (lib.HasAnimation(name)) lib.RemoveAnimation(name);
                lib.AddAnimation(name, clip);
                GD.Print($"[VinePlayer]   {name}: {start:F2}s-{end:F2}s ({clip.Length:F2}s)");
            }

            // Remove original monolithic animation
            string baseName = sourceAnimName.Contains("|") ? sourceAnimName.Split('|')[1] : sourceAnimName;
            if (lib.HasAnimation(sourceAnimName)) lib.RemoveAnimation(sourceAnimName);
            if (lib.HasAnimation(baseName)) lib.RemoveAnimation(baseName);
            foreach (var libName in animPlayer.GetAnimationLibraryList())
            {
                if (libName == "") continue;
                var otherLib = animPlayer.GetAnimationLibrary(libName);
                if (otherLib.HasAnimation(baseName)) otherLib.RemoveAnimation(baseName);
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
                    eyeMat.EmissionEnergyMultiplier = 2.5f;
                    _domeMaterials[mesh] = eyeMat;
                }
                else
                {
                    var bodyMat = new StandardMaterial3D();
                    bodyMat.AlbedoColor = new Color(0.9f, 0.92f, 0.95f);
                    bodyMat.Metallic = 0.5f;
                    bodyMat.Roughness = 0.2f;
                    bodyMat.EmissionEnabled = true;
                    bodyMat.Emission = new Color(0.92f, 0.94f, 0.97f);
                    bodyMat.EmissionEnergyMultiplier = 0.5f;
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

        private static VinePlayerAbility[] GetDefaultAbilities()
        {
            return new VinePlayerAbility[]
            {
                new VinePlayerAbility {
                    Name = "Shock Blast",
                    Description = "AoE damage around player",
                    Cooldown = 4f, ManaCost = 15f, Range = 5f,
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
                    Cooldown = 8f, ManaCost = 25f, Range = 12f,
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
                    Cooldown = 15f, ManaCost = 40f, Range = 8f,
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

        public override void _ExitTree()
        {
            ServiceLocator.Unregister<VinePlayer>();
        }
    }
}
