using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Phase-based AI for boss enemies. Replaces EnemyAI for boss-tier enemies.
    /// Supports intro sequence, 3-phase combat with special abilities, and enrage.
    /// </summary>
    public partial class BossAI : Node, IKnockbackable
    {
        public enum BossState { Idle, Intro, Chase, Attack, SpecialAttack, Stunned, PhaseTransition, Dead }

        private EnemyData _data;
        private StatBlock _stats;
        private CharacterBody3D _body;
        private NavigationAgent3D _navAgent;
        private HealthComponent _health;
        private IAnimatable _animatable;
        private BossConfig _config;

        private BossState _currentState = BossState.Idle;
        private Node3D _target;
        private float _stateTimer;
        private float _stunTimer;
        private float _attackCooldown;
        private Vector3 _knockbackVelocity;
        private Vector3 _introCenter;

        private int _currentPhase = 1;
        private int _attackCounter;
        private bool _invulnerable;
        private float _invulnerableTimer;
        private float _speedMultiplier = 1f;
        private float _damageMultiplier = 1f;
        private int _specialAbilityIndex;

        // Charge attack state
        private bool _isCharging;
        private Vector3 _chargeTarget;
        private float _chargeTimer;

        // Stationary boss (AXIS — doesn't walk, attacks with hands/head)
        private bool _isStationary;

        // Invulnerability health snapshot and re-entrancy guard
        private float _invulHealthSnapshot;
        private bool _processingHealthChange;

        public BossState CurrentState => _currentState;
        public int CurrentPhase => _currentPhase;
        public Node3D Target => _target;

        public override void _Ready()
        {
            _body = GetParent<CharacterBody3D>();
            _navAgent = _body.GetNodeOrNull<NavigationAgent3D>("NavigationAgent3D");
        }

        public void Initialize(EnemyData data, StatBlock stats, HealthComponent health)
        {
            _data = data;
            _stats = stats;
            _health = health;
            _config = BossRegistry.GetConfig(data.Id);

            _isStationary = data.Id == "axis_avatar";

            health.OnHealthChanged += OnHealthChanged;
            SetState(BossState.Intro);
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;

            // Apply gravity
            if (!_body.IsOnFloor())
                _body.Velocity += Vector3.Down * 20f * dt;

            // Process invulnerability timer
            if (_invulnerable)
            {
                _invulnerableTimer -= dt;
                if (_invulnerableTimer <= 0)
                    _invulnerable = false;
            }

            // Process knockback
            if (_knockbackVelocity.LengthSquared() > 0.1f)
            {
                _body.Velocity = _knockbackVelocity;
                _body.MoveAndSlide();
                _knockbackVelocity *= 0.85f;
            }

            if (_attackCooldown > 0)
                _attackCooldown -= dt;

            switch (_currentState)
            {
                case BossState.Intro: ProcessIntro(dt); break;
                case BossState.Chase: ProcessChase(dt); break;
                case BossState.Attack: ProcessAttack(dt); break;
                case BossState.SpecialAttack: ProcessSpecialAttack(dt); break;
                case BossState.Stunned: ProcessStunned(dt); break;
                case BossState.PhaseTransition: ProcessPhaseTransition(dt); break;
                case BossState.Dead:
                    _body.Velocity = new Vector3(0, _body.Velocity.Y, 0);
                    _body.MoveAndSlide();
                    break;
            }
        }

        private void ProcessIntro(float dt)
        {
            _stateTimer -= dt;

            // Walk toward room center for first 0.5s
            if (_stateTimer > 1.0f)
            {
                var dir = (_introCenter - _body.GlobalPosition).Flat();
                if (dir.LengthSquared() > 0.25f)
                {
                    dir = dir.Normalized();
                    float speed = _data?.MoveSpeed ?? 3f;
                    _body.Velocity = dir * speed * 0.5f;
                    _body.MoveAndSlide();
                    FaceDirection(dir);
                }
                else
                {
                    _body.Velocity = Vector3.Zero;
                    _body.MoveAndSlide();
                }
            }
            else
            {
                _body.Velocity = Vector3.Zero;
                _body.MoveAndSlide();
            }

            if (_stateTimer <= 0)
            {
                FindTarget();
                SetState(_target != null ? BossState.Chase : BossState.Idle);
            }
        }

        private void ProcessChase(float dt)
        {
            if (_target == null || !IsInstanceValid(_target) || !_target.IsInsideTree())
            {
                FindTarget();
                if (_target == null) { SetState(BossState.Idle); return; }
            }

            // Stationary bosses (AXIS) always attack — no chasing
            if (_isStationary)
            {
                FaceDirection((_target.GlobalPosition - _body.GlobalPosition).Flat().Normalized());
                if (_attackCooldown <= 0)
                {
                    if (ShouldUseSpecialAttack())
                        SetState(BossState.SpecialAttack);
                    else
                        SetState(BossState.Attack);
                }
                return;
            }

            float dist = _body.GlobalPosition.FlatDistance(_target.GlobalPosition);
            float attackRange = _data?.AttackRange ?? Constants.DEFAULT_ATTACK_RANGE;

            if (dist <= attackRange)
            {
                // Decide: basic attack or special
                if (ShouldUseSpecialAttack())
                    SetState(BossState.SpecialAttack);
                else
                    SetState(BossState.Attack);
                return;
            }

            // Chase — use nav agent if available, fallback to direct movement
            float speed = (_data?.MoveSpeed ?? 3f) * _speedMultiplier;
            var direction = (_target.GlobalPosition - _body.GlobalPosition).Flat().Normalized();

            if (_navAgent != null)
            {
                _navAgent.TargetPosition = _target.GlobalPosition;
                if (!_navAgent.IsNavigationFinished())
                {
                    var nextPos = _navAgent.GetNextPathPosition();
                    var navDir = (nextPos - _body.GlobalPosition).Flat().Normalized();
                    if (navDir.LengthSquared() > 0.01f)
                        direction = navDir;
                }
            }

            _body.Velocity = new Vector3(direction.X * speed, _body.Velocity.Y, direction.Z * speed);
            _body.MoveAndSlide();
            FaceDirection(direction);
        }

        private void ProcessAttack(float dt)
        {
            if (_target == null || !IsInstanceValid(_target) || !_target.IsInsideTree())
            {
                SetState(BossState.Chase);
                return;
            }

            float dist = _body.GlobalPosition.FlatDistance(_target.GlobalPosition);
            float attackRange = _data?.AttackRange ?? Constants.DEFAULT_ATTACK_RANGE;

            if (dist > attackRange * 1.3f)
            {
                SetState(BossState.Chase);
                return;
            }

            var dir = (_target.GlobalPosition - _body.GlobalPosition).Flat().Normalized();
            FaceDirection(dir);
            _body.Velocity = new Vector3(0, _body.Velocity.Y, 0);
            _body.MoveAndSlide();

            // Attack on cooldown
            if (_attackCooldown <= 0)
            {
                ExecuteBasicAttack();
                _attackCooldown = _data?.AttackCooldown ?? 1.5f;
                _attackCounter++;
            }
        }

        private void ProcessSpecialAttack(float dt)
        {
            _stateTimer -= dt;

            if (_isCharging)
            {
                ProcessChargeAttack(dt);
                return;
            }

            if (_stateTimer <= 0)
            {
                _attackCounter = 0;
                SetState(BossState.Chase);
            }
        }

        private void ProcessStunned(float dt)
        {
            _stunTimer -= dt;
            if (_stunTimer <= 0)
                SetState(_target != null ? BossState.Chase : BossState.Idle);
        }

        private void ProcessPhaseTransition(float dt)
        {
            _stateTimer -= dt;
            _body.Velocity = new Vector3(0, _body.Velocity.Y, 0);
            _body.MoveAndSlide();

            if (_stateTimer <= 0)
            {
                _invulnerable = false;
                SetState(BossState.Chase);
            }
        }

        private bool ShouldUseSpecialAttack()
        {
            if (_config == null || _config.Abilities.Count == 0) return false;
            if (_attackCooldown > 0) return false;

            // Stationary bosses use specials much more often (they can't melee effectively)
            if (_isStationary)
            {
                return _currentPhase switch
                {
                    1 => _attackCounter >= 1,
                    2 => true,  // always special in P2+
                    3 => true,
                    _ => _attackCounter >= 1
                };
            }

            return _currentPhase switch
            {
                1 => false,
                2 => _attackCounter >= 3,
                3 => _attackCounter >= 2,
                _ => false
            };
        }

        private void ExecuteBasicAttack()
        {
            if (_target == null || !IsInstanceValid(_target) || !_target.IsInsideTree()) return;

            IDamageable damageable = null;
            if (_target is IDamageable d)
                damageable = d;
            else
                damageable = _target.GetNodeOrNull<HealthComponent>("HealthComponent");

            if (damageable == null || !damageable.IsAlive) return;

            var damage = DamageCalculator.CalculateBasicAttack(_stats, _body, _target, _target.GlobalPosition, Team.Enemy);
            damage.FinalDamage *= _damageMultiplier;

            if (ServiceLocator.TryGet<PlayerController>(out var player))
                damage = DamageCalculator.ProcessDamage(damage, player.Stats.Stats);

            damageable.TakeDamage(damage);
            _animatable?.SetState(AnimState.Attack);
        }

        public void ExecuteSpecialAttack()
        {
            if (_config == null || _config.Abilities.Count == 0) return;

            var ability = _config.Abilities[_specialAbilityIndex % _config.Abilities.Count];
            _specialAbilityIndex++;

            switch (ability)
            {
                case BossAbilityType.GroundSlam: DoGroundSlam(); break;
                case BossAbilityType.ChargeAttack: DoChargeAttack(); break;
                case BossAbilityType.SummonAdds: DoSummonAdds(); break;
                case BossAbilityType.ProjectileBarrage: DoProjectileBarrage(); break;
            }

            _attackCooldown = (_data?.AttackCooldown ?? 1.5f) * 1.5f;
            _attackCounter = 0;
        }

        private void DoGroundSlam()
        {
            _animatable?.SetState(AnimState.Attack);

            // Shockwave VFX
            var shockwave = VfxFactory.CreateShockwaveRing(new Color(0.8f, 0.5f, 0.2f));
            _body.GetTree().Root.AddChild(shockwave);
            shockwave.GlobalPosition = _body.GlobalPosition + Vector3.Up * 0.1f;

            // AoE damage to player if in range
            if (ServiceLocator.TryGet<PlayerController>(out var player))
            {
                float dist = _body.GlobalPosition.FlatDistance(player.GlobalPosition);
                if (dist <= 4f)
                {
                    var damage = DamageCalculator.CalculateBasicAttack(_stats, _body, player, player.GlobalPosition, Team.Enemy);
                    damage.FinalDamage *= _damageMultiplier * 1.5f;
                    damage.KnockbackForce = 8f;
                    damage = DamageCalculator.ProcessDamage(damage, player.Stats.Stats);
                    player.Health.TakeDamage(damage);

                    // Knockback
                    if (player is IKnockbackable kb)
                        kb.ApplyKnockback(_body.GlobalPosition, 8f);
                }
            }

            _stateTimer = 1.0f;
            GD.Print($"[BossAI] {_data?.EnemyName} uses Ground Slam!");
        }

        private void DoChargeAttack()
        {
            if (_target == null || !IsInstanceValid(_target) || !_target.IsInsideTree()) return;

            _isCharging = true;
            _chargeTarget = _target.GlobalPosition;
            _chargeTimer = 1.5f;
            _animatable?.SetState(AnimState.Run);

            FaceDirection((_chargeTarget - _body.GlobalPosition).Flat().Normalized());
            GD.Print($"[BossAI] {_data?.EnemyName} charges!");
        }

        private void ProcessChargeAttack(float dt)
        {
            _chargeTimer -= dt;

            var dir = (_chargeTarget - _body.GlobalPosition).Flat();
            if (dir.LengthSquared() < 1f || _chargeTimer <= 0)
            {
                _isCharging = false;
                _stateTimer = 0.5f;

                // Impact at end
                var impact = VfxFactory.CreateImpactBurst(new Color(1f, 0.5f, 0.2f));
                _body.GetTree().Root.AddChild(impact);
                impact.GlobalPosition = _body.GlobalPosition + Vector3.Up * 0.5f;
                return;
            }

            float chargeSpeed = (_data?.MoveSpeed ?? 3f) * 3f;
            _body.Velocity = dir.Normalized() * chargeSpeed;
            _body.MoveAndSlide();

            // Check for player collision during charge
            if (ServiceLocator.TryGet<PlayerController>(out var player))
            {
                float dist = _body.GlobalPosition.FlatDistance(player.GlobalPosition);
                if (dist <= 1.5f)
                {
                    var damage = DamageCalculator.CalculateBasicAttack(_stats, _body, player, player.GlobalPosition, Team.Enemy);
                    damage.FinalDamage *= _damageMultiplier * 2f;
                    damage.KnockbackForce = 10f;
                    damage = DamageCalculator.ProcessDamage(damage, player.Stats.Stats);
                    player.Health.TakeDamage(damage);

                    if (player is IKnockbackable kb)
                        kb.ApplyKnockback(_body.GlobalPosition, 10f);

                    _isCharging = false;
                    _stateTimer = 0.8f;
                }
            }
        }

        private void DoSummonAdds()
        {
            _animatable?.SetState(AnimState.Attack);

            var enemyScene = GD.Load<PackedScene>(Constants.SCENE_ENEMY);
            if (enemyScene == null) return;

            // Pick weak enemies from the pool
            string[] addPool = { "scrap_rat", "wire_worm" };
            int count = _currentPhase >= 3 ? 3 : 2;

            for (int i = 0; i < count; i++)
            {
                var addId = addPool[GD.RandRange(0, addPool.Length - 1)];
                var addData = EnemyRegistry.GetEnemy(addId);
                if (addData == null) continue;

                float angle = (float)i / count * Mathf.Tau;
                var offset = new Vector3(Mathf.Cos(angle) * 3f, 0, Mathf.Sin(angle) * 3f);
                var spawnPos = _body.GlobalPosition + offset;

                var enemy = enemyScene.Instantiate<EnemyController>();
                _body.GetTree().Root.AddChild(enemy);
                enemy.GlobalPosition = spawnPos;
                enemy.Initialize(addData, 1f);

                // Summon VFX
                var vfx = VfxFactory.CreateDeathParticles(new Color(0.4f, 0.8f, 0.3f));
                _body.GetTree().Root.AddChild(vfx);
                vfx.GlobalPosition = spawnPos + Vector3.Up * 0.5f;
            }

            _stateTimer = 1.2f;
            GD.Print($"[BossAI] {_data?.EnemyName} summons {count} adds!");
        }

        private void DoProjectileBarrage()
        {
            if (_target == null || !IsInstanceValid(_target) || !_target.IsInsideTree()) return;

            _animatable?.SetState(AnimState.Attack);

            int count = _currentPhase >= 3 ? 5 : 3;
            float spreadAngle = Mathf.DegToRad(15f);
            var baseDir = (_target.GlobalPosition - _body.GlobalPosition).Flat().Normalized();

            for (int i = 0; i < count; i++)
            {
                float angle = (i - count / 2f) * spreadAngle;
                var rotated = baseDir.Rotated(Vector3.Up, angle);

                var projectile = new Projectile();
                _body.GetTree().Root.AddChild(projectile);
                projectile.GlobalPosition = _body.GlobalPosition + Vector3.Up * 1f + rotated * 0.5f;

                var damage = DamageCalculator.CalculateBasicAttack(_stats, _body, null, Vector3.Zero, Team.Enemy);
                damage.FinalDamage *= _damageMultiplier * 0.8f;
                projectile.Initialize(rotated, 12f, 20f, damage, Team.Enemy, DamageType.Dark);
            }

            _stateTimer = 1.0f;
            GD.Print($"[BossAI] {_data?.EnemyName} fires {count}-projectile barrage!");
        }

        private void OnHealthChanged(float current, float max)
        {
            if (_currentState == BossState.Dead) return;
            if (max <= 0) return;
            if (_processingHealthChange) return; // Prevent recursive heal loop

            float percent = current / max;

            // During invulnerability, restore health to snapshot — but never resurrect from zero
            if (_invulnerable && current > 0 && current < _invulHealthSnapshot)
            {
                _processingHealthChange = true;
                _health.SetCurrentHealth(_invulHealthSnapshot);
                _processingHealthChange = false;
                return;
            }

            int newPhase = 1;
            float p2 = _config?.Phase2Threshold ?? 0.6f;
            float p3 = _config?.Phase3Threshold ?? 0.3f;

            if (percent <= p3) newPhase = 3;
            else if (percent <= p2) newPhase = 2;

            if (newPhase > _currentPhase)
                TransitionToPhase(newPhase);
        }

        private void TransitionToPhase(int newPhase)
        {
            _currentPhase = newPhase;
            _invulnerable = true;
            _invulnerableTimer = 0.5f;
            _invulHealthSnapshot = _health.CurrentHealth;

            // Stat buffs per phase
            _speedMultiplier = newPhase switch
            {
                2 => 1.2f,
                3 => 1.4f,
                _ => 1f
            };
            _damageMultiplier = newPhase switch
            {
                3 => 1.25f,
                _ => 1f
            };

            SetState(BossState.PhaseTransition);
            _stateTimer = 1.0f;

            // VFX burst
            var burst = VfxFactory.CreateDeathParticles(new Color(1f, 0.3f, 0.1f));
            _body.GetTree().Root.AddChild(burst);
            burst.GlobalPosition = _body.GlobalPosition + Vector3.Up * 1f;

            var shockwave2 = VfxFactory.CreateShockwaveRing(new Color(1f, 0.4f, 0.1f));
            _body.GetTree().Root.AddChild(shockwave2);
            shockwave2.GlobalPosition = _body.GlobalPosition + Vector3.Up * 0.2f;

            // Commentary
            string msg = newPhase == 2
                ? $"{_data?.EnemyName} enters Phase 2! It's getting angry!"
                : $"{_data?.EnemyName} enters Phase 3! ENRAGE!";
            GameEvents.OnSystemMessage?.Invoke("Boss", msg);

            // Fire boss health bar phase update
            GameEvents.OnBossSpawned?.Invoke(_body);

            GD.Print($"[BossAI] {_data?.EnemyName} transitions to phase {newPhase}!");
        }

        private void FindTarget()
        {
            if (ServiceLocator.TryGet<PlayerController>(out var player) && player.Health.IsAlive)
                _target = player;
        }

        public void SetState(BossState newState)
        {
            _currentState = newState;

            if (_animatable == null)
                _animatable = (_body as EnemyController)?.Animatable;

            var animState = newState switch
            {
                BossState.Idle => AnimState.Idle,
                BossState.Intro => AnimState.Walk,
                BossState.Chase => AnimState.Run,
                BossState.Attack => AnimState.Attack,
                BossState.SpecialAttack => AnimState.Attack,
                BossState.Stunned => AnimState.Stunned,
                BossState.PhaseTransition => AnimState.Stunned,
                BossState.Dead => AnimState.Death,
                _ => AnimState.Idle
            };
            _animatable?.SetState(animState);

            switch (newState)
            {
                case BossState.Intro:
                    _stateTimer = 1.5f;
                    _introCenter = _body.GlobalPosition;
                    FindTarget();
                    break;
                case BossState.SpecialAttack:
                    ExecuteSpecialAttack();
                    break;
                case BossState.Dead:
                    _body.Velocity = Vector3.Zero;
                    break;
            }
        }

        /// <summary>
        /// Set the EnemyAI.State equivalent for compatibility with EnemyController death handler.
        /// </summary>
        public void SetDeadState()
        {
            SetState(BossState.Dead);
        }

        private void FaceDirection(Vector3 direction)
        {
            if (direction.LengthSquared() < 0.001f) return;
            _body.LookAt(_body.GlobalPosition + direction, Vector3.Up);
        }

        public void ApplyKnockback(Vector3 sourcePosition, float force)
        {
            // Bosses resist knockback — halve it
            var dir = (_body.GlobalPosition - sourcePosition).Flat().Normalized();
            _knockbackVelocity = dir * force * 0.5f;
        }

        public void ApplyStun(float duration)
        {
            // Bosses resist stun — halve duration
            _stunTimer = duration * 0.5f;
            SetState(BossState.Stunned);
        }

        public override void _ExitTree()
        {
            if (_health != null)
                _health.OnHealthChanged -= OnHealthChanged;
        }
    }
}
