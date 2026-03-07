using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Enemy AI state machine: Idle, Patrol, Chase, Attack, Stunned, Dead.
    /// Supports behavior variants: Melee, Ranged, Flanker, Healer.
    /// Uses NavigationAgent3D for pathfinding.
    /// </summary>
    public partial class EnemyAI : Node, IKnockbackable
    {
        public enum State { Idle, Patrol, Chase, Attack, Stunned, Dead }

        private EnemyData _data;
        private StatBlock _stats;
        private CharacterBody3D _body;
        private NavigationAgent3D _navAgent;
        private IAnimatable _animatable;

        private State _currentState = State.Idle;
        private Node3D _target;
        private float _stateTimer;
        private float _stunTimer;
        private Vector3 _knockbackVelocity;
        private Vector3 _patrolTarget;
        private float _patrolWaitTimer;

        // Behavior-specific
        private float _strafeAngle;
        private float _strafeTimer;
        private float _healTimer;

        public State CurrentState => _currentState;

        public override void _Ready()
        {
            _body = GetParent<CharacterBody3D>();
            _navAgent = _body.GetNodeOrNull<NavigationAgent3D>("NavigationAgent3D");
        }

        public void Initialize(EnemyData data, StatBlock stats)
        {
            _data = data;
            _stats = stats;
            _strafeAngle = (float)GD.RandRange(-1f, 1f);
            SetState(State.Patrol);
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;

            // Apply gravity
            if (!_body.IsOnFloor())
                _body.Velocity += Vector3.Down * 20f * dt;

            // Process knockback
            if (_knockbackVelocity.LengthSquared() > 0.1f)
            {
                _body.Velocity = _knockbackVelocity;
                _body.MoveAndSlide();
                _knockbackVelocity *= 0.85f;
            }

            switch (_currentState)
            {
                case State.Idle:
                    ProcessIdle(dt);
                    break;
                case State.Patrol:
                    ProcessPatrol(dt);
                    break;
                case State.Chase:
                    ProcessChase(dt);
                    break;
                case State.Attack:
                    ProcessAttack(dt);
                    break;
                case State.Stunned:
                    ProcessStunned(dt);
                    break;
                case State.Dead:
                    _body.Velocity = new Vector3(0, _body.Velocity.Y, 0);
                    _body.MoveAndSlide();
                    break;
            }
        }

        private void ProcessIdle(float dt)
        {
            _body.Velocity = new Vector3(0, _body.Velocity.Y, 0);
            _body.MoveAndSlide();

            _stateTimer -= dt;
            if (_stateTimer <= 0)
                SetState(State.Patrol);

            CheckForPlayer();
        }

        private void ProcessPatrol(float dt)
        {
            if (_navAgent == null) { SetState(State.Idle); return; }

            _patrolWaitTimer -= dt;
            if (_patrolWaitTimer > 0)
            {
                _body.Velocity = new Vector3(0, _body.Velocity.Y, 0);
                _body.MoveAndSlide();
                CheckForPlayer();
                return;
            }

            if (_navAgent.IsNavigationFinished())
            {
                var offset = new Vector3(
                    (float)GD.RandRange(-5, 5), 0,
                    (float)GD.RandRange(-5, 5));
                _patrolTarget = _body.GlobalPosition + offset;
                _navAgent.TargetPosition = _patrolTarget;
                _patrolWaitTimer = (float)GD.RandRange(1.0, 3.0);
                return;
            }

            var nextPos = _navAgent.GetNextPathPosition();
            var direction = (nextPos - _body.GlobalPosition).Flat().Normalized();
            float speed = _data?.MoveSpeed ?? 3f;

            _body.Velocity = new Vector3(direction.X * speed * 0.5f, _body.Velocity.Y, direction.Z * speed * 0.5f);
            _body.MoveAndSlide();
            FaceDirection(direction);

            CheckForPlayer();
        }

        private void ProcessChase(float dt)
        {
            if (_target == null || !IsInstanceValid(_target))
            {
                SetState(State.Patrol);
                return;
            }

            float dist = _body.GlobalPosition.FlatDistance(_target.GlobalPosition);
            float aggroRange = _data?.AggroRange ?? Constants.DEFAULT_AGGRO_RANGE;
            if (dist > aggroRange * 1.5f)
            {
                _target = null;
                SetState(State.Patrol);
                return;
            }

            float attackRange = _data?.AttackRange ?? Constants.DEFAULT_ATTACK_RANGE;

            var behavior = _data?.Behavior ?? EnemyBehavior.Melee;

            switch (behavior)
            {
                case EnemyBehavior.Ranged:
                    ProcessChaseRanged(dt, dist, attackRange);
                    return;
                case EnemyBehavior.Flanker:
                    ProcessChaseFlanker(dt, dist, attackRange);
                    return;
                case EnemyBehavior.Healer:
                    ProcessChaseHealer(dt, dist, attackRange);
                    return;
            }

            // Default melee chase
            if (dist <= attackRange)
            {
                SetState(State.Attack);
                return;
            }

            MoveTowardTarget(dt);
        }

        private void ProcessChaseRanged(float dt, float dist, float attackRange)
        {
            // Ranged: maintain distance, retreat if player gets too close
            float idealRange = attackRange * 0.8f;
            float tooClose = attackRange * 0.4f;

            if (dist <= attackRange && dist > tooClose)
            {
                SetState(State.Attack);
                return;
            }

            if (dist <= tooClose)
            {
                // Retreat — move away from player while strafing
                var awayDir = (_body.GlobalPosition - _target.GlobalPosition).Flat().Normalized();
                var strafeDir = new Vector3(-awayDir.Z, 0, awayDir.X) * _strafeAngle;
                var moveDir = (awayDir + strafeDir * 0.4f).Normalized();
                float speed = _data?.MoveSpeed ?? 3f;

                _body.Velocity = new Vector3(moveDir.X * speed * 1.2f, _body.Velocity.Y, moveDir.Z * speed * 1.2f);
                _body.MoveAndSlide();
                FaceDirection((_target.GlobalPosition - _body.GlobalPosition).Flat().Normalized());
            }
            else
            {
                MoveTowardTarget(dt);
            }
        }

        private void ProcessChaseFlanker(float dt, float dist, float attackRange)
        {
            // Flanker: circle around player, attack from the side
            if (dist <= attackRange)
            {
                SetState(State.Attack);
                return;
            }

            var toTarget = (_target.GlobalPosition - _body.GlobalPosition).Flat().Normalized();
            float speed = _data?.MoveSpeed ?? 3f;

            if (dist < attackRange * 3f)
            {
                // Circle strafe toward player
                _strafeTimer -= dt;
                if (_strafeTimer <= 0f)
                {
                    _strafeTimer = (float)GD.RandRange(1f, 2.5f);
                    _strafeAngle = -_strafeAngle; // Reverse direction
                }

                var perpendicular = new Vector3(-toTarget.Z, 0, toTarget.X) * _strafeAngle;
                var moveDir = (toTarget * 0.6f + perpendicular * 0.8f).Normalized();

                _body.Velocity = new Vector3(moveDir.X * speed, _body.Velocity.Y, moveDir.Z * speed);
                _body.MoveAndSlide();
                FaceDirection(toTarget);
            }
            else
            {
                MoveTowardTarget(dt);
            }
        }

        private void ProcessChaseHealer(float dt, float dist, float attackRange)
        {
            // Healer: stay near allies, heal wounded ones, avoid player
            _healTimer -= dt;

            if (_healTimer <= 0f)
            {
                _healTimer = _data?.AttackCooldown ?? 3f;
                TryHealAlly();
            }

            // Stay away from player
            float safeDistance = attackRange * 0.6f;
            if (dist < safeDistance)
            {
                var awayDir = (_body.GlobalPosition - _target.GlobalPosition).Flat().Normalized();
                float speed = _data?.MoveSpeed ?? 3f;
                _body.Velocity = new Vector3(awayDir.X * speed, _body.Velocity.Y, awayDir.Z * speed);
                _body.MoveAndSlide();
                FaceDirection(-awayDir);
            }
            else
            {
                // Drift toward wounded ally or patrol near other enemies
                var woundedAlly = FindWoundedAlly();
                if (woundedAlly != null)
                {
                    var toAlly = (woundedAlly.GlobalPosition - _body.GlobalPosition).Flat().Normalized();
                    float speed = _data?.MoveSpeed ?? 3f;
                    _body.Velocity = new Vector3(toAlly.X * speed * 0.7f, _body.Velocity.Y, toAlly.Z * speed * 0.7f);
                    _body.MoveAndSlide();
                    FaceDirection(toAlly);
                }
                else
                {
                    // Idle near current position
                    _body.Velocity = new Vector3(0, _body.Velocity.Y, 0);
                    _body.MoveAndSlide();
                }
            }
        }

        private void TryHealAlly()
        {
            var enemies = _body.GetTree().GetNodesInGroup(Constants.GROUP_ENEMY);
            float healRange = _data?.AttackRange ?? 6f;
            float healAmount = _data?.BaseDamage ?? 5f; // Repurpose damage stat as heal amount

            foreach (var node in enemies)
            {
                if (node == _body) continue;
                if (node is not Node3D ally3d) continue;
                if (!IsInstanceValid(ally3d)) continue;

                float dist = _body.GlobalPosition.FlatDistance(ally3d.GlobalPosition);
                if (dist > healRange) continue;

                var health = ally3d.GetNodeOrNull<HealthComponent>("HealthComponent");
                if (health == null || !health.IsAlive) continue;
                if (health.CurrentHealth >= health.MaxHealth * 0.9f) continue;

                health.Heal(healAmount);

                // Green heal VFX
                var vfx = VfxFactory.CreateHitParticles(new Color(0.3f, 1f, 0.4f));
                _body.GetTree().Root.AddChild(vfx);
                vfx.GlobalPosition = ally3d.GlobalPosition + Vector3.Up * 1f;

                GD.Print($"[EnemyAI] Patch Bot healed ally for {healAmount:F0}");
                return;
            }
        }

        private Node3D FindWoundedAlly()
        {
            var enemies = _body.GetTree().GetNodesInGroup(Constants.GROUP_ENEMY);
            Node3D bestAlly = null;
            float lowestHpRatio = 1f;

            foreach (var node in enemies)
            {
                if (node == _body) continue;
                if (node is not Node3D ally3d) continue;
                if (!IsInstanceValid(ally3d)) continue;

                float dist = _body.GlobalPosition.FlatDistance(ally3d.GlobalPosition);
                if (dist > 15f) continue;

                var health = ally3d.GetNodeOrNull<HealthComponent>("HealthComponent");
                if (health == null || !health.IsAlive) continue;

                float ratio = health.CurrentHealth / health.MaxHealth;
                if (ratio < lowestHpRatio)
                {
                    lowestHpRatio = ratio;
                    bestAlly = ally3d;
                }
            }

            return lowestHpRatio < 0.8f ? bestAlly : null;
        }

        private void MoveTowardTarget(float dt)
        {
            float speed = _data?.MoveSpeed ?? 3f;
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
            if (_target == null || !IsInstanceValid(_target))
            {
                SetState(State.Chase);
                return;
            }

            float dist = _body.GlobalPosition.FlatDistance(_target.GlobalPosition);
            float attackRange = _data?.AttackRange ?? Constants.DEFAULT_ATTACK_RANGE;
            var behavior = _data?.Behavior ?? EnemyBehavior.Melee;

            // Ranged enemies have wider attack tolerance
            float breakRange = behavior == EnemyBehavior.Ranged ? attackRange * 1.3f : attackRange * 1.2f;

            if (dist > breakRange)
            {
                SetState(State.Chase);
                return;
            }

            // Face target
            var dir = (_target.GlobalPosition - _body.GlobalPosition).Flat().Normalized();
            FaceDirection(dir);

            // Ranged enemies strafe during attack
            if (behavior == EnemyBehavior.Ranged)
            {
                var perpendicular = new Vector3(-dir.Z, 0, dir.X) * _strafeAngle;
                float speed = (_data?.MoveSpeed ?? 3f) * 0.3f;
                _body.Velocity = new Vector3(perpendicular.X * speed, _body.Velocity.Y, perpendicular.Z * speed);
            }
            else
            {
                _body.Velocity = new Vector3(0, _body.Velocity.Y, 0);
            }

            _body.MoveAndSlide();
        }

        private void ProcessStunned(float dt)
        {
            _stunTimer -= dt;
            if (_stunTimer <= 0)
            {
                SetState(_target != null ? State.Chase : State.Patrol);
            }
        }

        private void CheckForPlayer()
        {
            float aggroRange = _data?.AggroRange ?? Constants.DEFAULT_AGGRO_RANGE;
            var nearest = PlayerManager.GetNearestPlayerInRange(_body.GlobalPosition, aggroRange);
            if (nearest != null)
            {
                _target = nearest;
                SetState(State.Chase);
            }
        }

        public void SetState(State newState)
        {
            _currentState = newState;

            if (_animatable == null)
                _animatable = (_body as EnemyController)?.Animatable;

            var animState = newState switch
            {
                State.Idle => AnimState.Idle,
                State.Patrol => AnimState.Walk,
                State.Chase => AnimState.Run,
                State.Attack => AnimState.Attack,
                State.Stunned => AnimState.Stunned,
                State.Dead => AnimState.Death,
                _ => AnimState.Idle
            };
            _animatable?.SetState(animState);

            switch (newState)
            {
                case State.Idle:
                    _stateTimer = (float)GD.RandRange(1.0, 3.0);
                    break;
                case State.Patrol:
                    _patrolWaitTimer = 0.5f;
                    break;
                case State.Dead:
                    _body.Velocity = Vector3.Zero;
                    break;
            }
        }

        public Node3D Target => _target;

        private void FaceDirection(Vector3 direction)
        {
            if (direction.LengthSquared() < 0.001f) return;
            _body.LookAt(_body.GlobalPosition + direction, Vector3.Up);
        }

        // --- IKnockbackable ---
        public void ApplyKnockback(Vector3 sourcePosition, float force)
        {
            var dir = (_body.GlobalPosition - sourcePosition).Flat().Normalized();
            _knockbackVelocity = dir * force;
        }

        public void ApplyStun(float duration)
        {
            _stunTimer = duration;
            SetState(State.Stunned);
        }
    }
}
