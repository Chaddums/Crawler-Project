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
        private readonly Dictionary<MeshInstance3D, Material> _originalMaterials = new();
        private readonly Dictionary<MeshInstance3D, Material> _themedMaterials = new();
        private bool _isInsideDome = true; // Start inside

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
                            // Naruto run — pingpong: play forward then backward, no seam
                            _animator?.PlayCustom("Run");
                            float animPos = _animator?.GetPlaybackPosition() ?? 0f;
                            float len = _animator?.GetAnimationLength() ?? 1f;
                            if (_narutoForward && animPos >= len - 0.05f)
                                _narutoForward = false;
                            else if (!_narutoForward && animPos <= 0.05f)
                                _narutoForward = true;
                            _animator?.SetSpeed(_narutoForward ? 0.8f : -0.8f);
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

                // Cache original FBX materials before theming
                // (deferred because FBX meshes may not be fully loaded yet)
                CallDeferred(MethodName._CacheOriginalMaterials);

                // Defer tronification to next frame so FBX meshes are fully loaded
                CallDeferred(MethodName._ApplyBitThemeDeferred);

                // Split BIT's single ArmatureAction into separate named clips,
                // then initialize animator so it can map them to states
                SplitBitAnimations(_modelRoot);

                // Initialize animator
                _animator = new CharacterAnimator();
                AddChild(_animator);
                _animator.Initialize(_modelRoot);
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

        /// <summary>
        /// BIT's FBX has all animations baked into one "ArmatureAction" timeline.
        /// Split it into individual named clips so CharacterAnimator can map them.
        /// User-identified animation order:
        ///   1. Idle sway (kicks feet/arms back and forth)
        ///   2. Naruto run (arms back, sprint forward)
        ///   3. Right arm attack
        ///   4. Left arm attack
        ///   5. Head attack (headbutt)
        ///   6. Die/fallback/slip
        /// </summary>
        private static void SplitBitAnimations(Node3D modelRoot)
        {
            // Find the AnimationPlayer
            var animPlayer = FindAnimPlayerRecursive(modelRoot);
            if (animPlayer == null)
            {
                GD.PrintErr("[VinePlayer] No AnimationPlayer found in BIT model");
                return;
            }

            // Find the source animation — try common FBX names
            Animation sourceAnim = null;
            string sourceAnimName = null;
            foreach (var name in animPlayer.GetAnimationList())
            {
                string lower = name.ToLower();
                if (lower.Contains("action") || lower.Contains("armature"))
                {
                    sourceAnim = animPlayer.GetAnimation(name);
                    sourceAnimName = name;
                    break;
                }
            }

            if (sourceAnim == null)
            {
                GD.Print("[VinePlayer] No ArmatureAction animation found to split");
                return;
            }

            float totalLength = (float)sourceAnim.Length;
            int trackCount = sourceAnim.GetTrackCount();
            GD.Print($"[VinePlayer] BIT animation '{sourceAnimName}': length={totalLength:F2}s tracks={trackCount}");

            // Print keyframe density to help identify segment boundaries
            // Look at the first bone track and find gaps between keyframes
            if (trackCount > 0)
            {
                // Sample first few tracks to find segment boundaries
                // Segments often have brief pauses (keyframe gaps) between them
                var gaps = new List<float>();
                for (int t = 0; t < Mathf.Min(trackCount, 3); t++)
                {
                    int keyCount = sourceAnim.TrackGetKeyCount(t);
                    if (keyCount < 2) continue;

                    float prevTime = (float)sourceAnim.TrackGetKeyTime(t, 0);
                    for (int k = 1; k < keyCount; k++)
                    {
                        float time = (float)sourceAnim.TrackGetKeyTime(t, k);
                        float delta = time - prevTime;
                        // Gaps significantly larger than the average frame time suggest segment boundaries
                        if (delta > totalLength / keyCount * 3f && !gaps.Contains(time))
                            gaps.Add(prevTime);
                        prevTime = time;
                    }
                }
                if (gaps.Count > 0)
                {
                    gaps.Sort();
                    GD.Print($"[VinePlayer] Detected animation gaps at: {string.Join(", ", gaps.ConvertAll(g => g.ToString("F2")))}s");
                }
            }

            // Split points from gap detection on 7.9s timeline:
            // Gaps at: 3.17, 4.13, 5.07, 5.23, 6.73
            // Idle sway is the full 0-3.17 (long, feet+arm kicks)
            // Naruto run is the short 3.17-4.13 cycle
            // Three attacks: 4.13-5.07, 5.07-5.90, 5.90-6.73
            // Death/slip: 6.73-end
            var segments = new (string name, float start, float end)[]
            {
                ("Idle",       0f,     3.17f),
                ("Run",        3.17f,  4.13f),
                ("Attack_R",   4.13f,  5.07f),
                ("Attack_L",   5.07f,  5.90f),
                ("Attack",     5.90f,  6.73f),  // Head attack
                ("Death",      6.73f,  totalLength),
            };

            GD.Print($"[VinePlayer] Splitting into {segments.Length} segments (gap-based):");

            // Get or create a library to add clips to
            AnimationLibrary lib;
            if (animPlayer.HasAnimationLibrary(""))
                lib = animPlayer.GetAnimationLibrary("");
            else
            {
                lib = new AnimationLibrary();
                animPlayer.AddAnimationLibrary("", lib);
            }

            foreach (var (name, start, end) in segments)
            {
                var clip = new Animation();
                clip.Length = end - start;

                // Copy all tracks, offsetting keyframe times
                // Skip scale tracks (Scale3D) — they cause BIT to grow/shrink
                for (int t = 0; t < trackCount; t++)
                {
                    var trackType = sourceAnim.TrackGetType(t);

                    // Strip scale tracks to prevent size fluctuation
                    if (trackType == Animation.TrackType.Scale3D)
                        continue;

                    int newTrackIdx = clip.AddTrack(trackType);
                    clip.TrackSetPath(newTrackIdx, sourceAnim.TrackGetPath(t));
                    clip.TrackSetInterpolationType(newTrackIdx, sourceAnim.TrackGetInterpolationType(t));

                    int keyCount = sourceAnim.TrackGetKeyCount(t);
                    for (int k = 0; k < keyCount; k++)
                    {
                        float keyTime = (float)sourceAnim.TrackGetKeyTime(t, k);
                        // Exclude boundary keyframes — they belong to the next segment's start pose
                        if (keyTime < start || keyTime >= end - 0.01f) continue;

                        float newTime = keyTime - start;
                        var value = sourceAnim.TrackGetKeyValue(t, k);
                        clip.TrackInsertKey(newTrackIdx, newTime, value);
                    }
                }

                if (name == "Idle")
                {
                    clip.LoopMode = Animation.LoopModeEnum.Linear;
                }
                // Run uses manual pingpong — no loop mode needed

                // Add to library (remove existing if present)
                if (lib.HasAnimation(name))
                    lib.RemoveAnimation(name);
                lib.AddAnimation(name, clip);

                GD.Print($"[VinePlayer]   {name}: {start:F2}s - {end:F2}s ({clip.Length:F2}s, {(name == "Idle" || name == "Run" ? "loop" : "once")})");
            }

            // Remove original to avoid confusion
            if (lib.HasAnimation(sourceAnimName))
                lib.RemoveAnimation(sourceAnimName);
            // Also try without library prefix
            string baseName = sourceAnimName.Contains("|") ? sourceAnimName.Split('|')[1] : sourceAnimName;
            if (lib.HasAnimation(baseName))
                lib.RemoveAnimation(baseName);

            // Also try removing from other libraries (FBX may use "Armature" library)
            foreach (var libName in animPlayer.GetAnimationLibraryList())
            {
                if (libName == "") continue;
                var otherLib = animPlayer.GetAnimationLibrary(libName);
                // Remove the original combined animation from its source library
                if (otherLib.HasAnimation(baseName))
                    otherLib.RemoveAnimation(baseName);
            }

            // Verify: print all animations now available
            var finalAnims = animPlayer.GetAnimationList();
            GD.Print($"[VinePlayer] After split, available anims: {string.Join(", ", finalAnims)}");
        }

        private static AnimationPlayer FindAnimPlayerRecursive(Node root)
        {
            if (root is AnimationPlayer ap) return ap;
            foreach (var child in root.GetChildren())
            {
                var result = FindAnimPlayerRecursive(child);
                if (result != null) return result;
            }
            return null;
        }

        /// <summary>
        /// Deferred theme application — ensures FBX children are fully loaded.
        /// </summary>
        /// <summary>
        /// Build the "real" BIT look — clean futuristic space-tech.
        /// Silver-white metallic body with the FBX texture for surface detail,
        /// bright blue-white eyes, subtle accent glow on panel lines.
        /// This is what BIT looks like inside the conversion dome.
        /// </summary>
        private void _CacheOriginalMaterials()
        {
            if (_modelRoot == null) return;
            _originalMaterials.Clear();

            // Load BIT's textures for surface detail
            var bodyTex = GD.Load<Texture2D>("res://Models/Characters/Companions/textures/LilRobot.png")
                ?? GD.Load<Texture2D>("res://Models/Characters/Companions/LilRobot.png");
            var eyeTex = GD.Load<Texture2D>("res://Models/Characters/Companions/textures/LilRobotEyes.png")
                ?? GD.Load<Texture2D>("res://Models/Characters/Companions/LilRobotEyes.png");

            // Harvester accent — clean blue-white, distinct from any planet theme
            var harvesterAccent = new Color(0.4f, 0.7f, 1.0f);   // Soft blue
            var harvesterGlow = new Color(0.5f, 0.8f, 1.0f);     // Brighter blue-white

            var meshes = _modelRoot.FindChildren("*", "MeshInstance3D", true, false);
            foreach (var node in meshes)
            {
                if (node is not MeshInstance3D mesh || mesh.Mesh == null) continue;
                string meshName = mesh.Name.ToString().ToLower();

                if (meshName.Contains("eye"))
                {
                    // Eyes: bright blue-white emissive visor
                    var eyeMat = new StandardMaterial3D();
                    eyeMat.AlbedoColor = harvesterGlow;
                    if (eyeTex != null) eyeMat.AlbedoTexture = eyeTex;
                    eyeMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                    eyeMat.EmissionEnabled = true;
                    eyeMat.Emission = harvesterGlow;
                    eyeMat.EmissionEnergyMultiplier = 2.5f;
                    _originalMaterials[mesh] = eyeMat;
                }
                else
                {
                    // Body: polished silver-white — no texture (FBX tex is dark grey, kills the color)
                    var bodyMat = new StandardMaterial3D();
                    bodyMat.AlbedoColor = new Color(0.88f, 0.9f, 0.93f); // Clean silver-white
                    bodyMat.Metallic = 0.65f;
                    bodyMat.Roughness = 0.18f; // Polished spacecraft hull
                    bodyMat.EmissionEnabled = true;
                    bodyMat.Emission = new Color(0.9f, 0.93f, 0.97f);
                    bodyMat.EmissionEnergyMultiplier = 0.08f;
                    _originalMaterials[mesh] = bodyMat;
                }
            }
            GD.Print($"[VinePlayer] Built {_originalMaterials.Count} harvester BIT materials");
        }

        private void _ApplyBitThemeDeferred()
        {
            if (_modelRoot == null) return;

            bool isScrapyard = PlanetTheme.Current is ScrapyardPlanetTheme;
            var accent = isScrapyard ? new Color(0.9f, 0.6f, 0.1f) : new Color(0.0f, 0.85f, 0.95f);

            // Use Godot's built-in recursive search — handles ImporterMeshInstance3D etc.
            var meshes = _modelRoot.FindChildren("*", "MeshInstance3D", true, false);
            GD.Print($"[VinePlayer] BIT model tree: root='{_modelRoot.Name}' type={_modelRoot.GetType().Name} meshCount={meshes.Count}");

            // Debug: dump full tree
            DumpNodeTree(_modelRoot, 0);

            // Hide any mesh nodes with null Mesh (potential deferred-load sphere)
            // and log all meshes we find
            foreach (var node in meshes)
            {
                if (node is not MeshInstance3D mesh) continue;
                if (mesh.Mesh == null)
                {
                    mesh.Visible = false;
                    GD.Print($"[VinePlayer] HIDING null-mesh node '{mesh.Name}'");
                    continue;
                }

                var aabb = mesh.GetAabb();
                string meshName = mesh.Name.ToString().ToLower();
                GD.Print($"[VinePlayer] BIT mesh: '{mesh.Name}' aabb={aabb.Size}");

                // Dark body material
                var bodyMat = new StandardMaterial3D();
                bodyMat.AlbedoColor = new Color(0.02f, 0.02f, 0.03f);
                bodyMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;

                // World-space outline shader — auto-compensates for model scale
                if (_bitOutlineShader == null)
                {
                    _bitOutlineShader = new Shader();
                    _bitOutlineShader.Code = @"
shader_type spatial;
render_mode unshaded, cull_front;
uniform vec3 outline_color : source_color = vec3(0.0, 0.85, 0.95);
uniform float outline_width = 0.03;
void vertex() {
    // Compute scale from MODEL_MATRIX so outline is constant world-space width
    float scale = length(MODEL_MATRIX[0].xyz);
    VERTEX += NORMAL * (outline_width / max(scale, 0.001));
}
void fragment() { ALBEDO = outline_color; ALPHA = 0.9; }
";
                }

                var outlineMat = new ShaderMaterial();
                outlineMat.Shader = _bitOutlineShader;
                outlineMat.SetShaderParameter("outline_color",
                    new Vector3(accent.R, accent.G, accent.B));
                outlineMat.SetShaderParameter("outline_width", 0.03f); // World units

                bodyMat.NextPass = outlineMat;

                // Eye meshes get bright emissive instead
                if (meshName.Contains("eye"))
                {
                    var eyeMat = new StandardMaterial3D();
                    eyeMat.AlbedoColor = accent;
                    eyeMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                    eyeMat.EmissionEnabled = true;
                    eyeMat.Emission = accent;
                    eyeMat.EmissionEnergyMultiplier = 2f;
                    mesh.MaterialOverride = eyeMat;
                }
                else
                {
                    mesh.MaterialOverride = bodyMat;
                }
            }

            // Cache the themed materials
            _themedMaterials.Clear();
            foreach (var node in meshes)
            {
                if (node is not MeshInstance3D mesh || mesh.Mesh == null) continue;
                if (mesh.MaterialOverride != null)
                    _themedMaterials[mesh] = mesh.MaterialOverride.Duplicate() as Material;
            }
            GD.Print($"[VinePlayer] BIT tronified — cached {_themedMaterials.Count} themed materials");

            // BIT starts near the harvester (inside dome) — apply original materials immediately
            foreach (var (mesh, mat) in _originalMaterials)
            {
                if (IsInstanceValid(mesh))
                    mesh.MaterialOverride = mat;
            }
            _isInsideDome = true;
        }

        private static Shader _bitOutlineShader;

        /// <summary>
        /// Fallback manual recursive — used only if FindChildren returns nothing.
        /// </summary>
        private static void ApplyBitTronOutline(Node node, Color accent)
        {
            GD.Print($"[VinePlayer] ManualTraverse: {node.GetType().Name} '{node.Name}' children={node.GetChildCount()}");
            if (node is GeometryInstance3D geo)
            {
                GD.Print($"[VinePlayer] Found GeometryInstance3D: '{node.Name}' type={node.GetType().Name}");
                // Apply dark body material
                var bodyMat = new StandardMaterial3D();
                bodyMat.AlbedoColor = new Color(0.02f, 0.02f, 0.03f);
                bodyMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                geo.MaterialOverride = bodyMat;
            }
            foreach (var child in node.GetChildren())
                ApplyBitTronOutline(child, accent);
        }

        private static void DumpNodeTree(Node node, int depth)
        {
            string indent = new string(' ', depth * 2);
            string extra = "";
            if (node is MeshInstance3D m && m.Mesh != null)
                extra = $" [MESH surfaces={m.Mesh.GetSurfaceCount()} aabb={m.GetAabb().Size}]";
            GD.Print($"[BIT Tree] {indent}{node.GetType().Name}: '{node.Name}'{extra}");
            foreach (var child in node.GetChildren())
                DumpNodeTree(child, depth + 1);
        }

        /// <summary>
        /// Check if BIT is inside/outside the conversion dome and swap materials accordingly.
        /// Inside dome = original FBX materials (the "real" BIT).
        /// Outside dome = planet-themed materials (Tron outline / Scrapyard rust).
        /// </summary>
        private void UpdateDomeMaterials()
        {
            if (_modelRoot == null || _originalMaterials.Count == 0) return;
            if (!ServiceLocator.TryGet<VineHarvester>(out _)) return; // No harvester = no dome

            // Find the dome
            ConversionDome dome = null;
            foreach (var child in GetParent().GetChildren())
            {
                if (child is ConversionDome d) { dome = d; break; }
            }
            if (dome == null) return;

            bool inside = dome.IsInsideDome(GlobalPosition);
            if (inside == _isInsideDome) return; // No change
            _isInsideDome = inside;

            // Swap materials on all cached meshes
            var source = inside ? _originalMaterials : _themedMaterials;
            foreach (var (mesh, mat) in source)
            {
                if (IsInstanceValid(mesh))
                    mesh.MaterialOverride = mat;
            }

            GD.Print($"[VinePlayer] BIT materials → {(inside ? "ORIGINAL (inside dome)" : "THEMED (outside dome)")}");
        }

        private static void ApplyMaterialToAll(Node node, StandardMaterial3D mat)
        {
            if (node is MeshInstance3D mesh)
                mesh.MaterialOverride = mat;
            foreach (var child in node.GetChildren())
                ApplyMaterialToAll(child, mat);
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
                    bool isScrapyard = PlanetTheme.Current is ScrapyardPlanetTheme;
                    var accent = isScrapyard ? new Color(0.9f, 0.6f, 0.1f) : new Color(0.2f, 0.7f, 1.0f);
                    mat.Emission = accent;
                    mat.EmissionEnergyMultiplier = isScrapyard ? 0.1f : 0.2f;
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
