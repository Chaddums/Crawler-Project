using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Enemy AI state machine: Idle, Patrol, Chase, Attack, Stunned, Dead.
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
            SetState(State.Patrol);
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;

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
                    break;
            }
        }

        private void ProcessIdle(float dt)
        {
            _stateTimer -= dt;
            if (_stateTimer <= 0)
                SetState(State.Patrol);

            CheckForPlayer();
        }

        private void ProcessPatrol(float dt)
        {
            if (_navAgent == null) { SetState(State.Idle); return; }

            _patrolWaitTimer -= dt;
            if (_patrolWaitTimer > 0) return;

            if (_navAgent.IsNavigationFinished())
            {
                // Pick new random patrol point
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

            _body.Velocity = direction * speed * 0.5f; // Patrol at half speed
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

            // Lost aggro?
            float aggroRange = _data?.AggroRange ?? Constants.DEFAULT_AGGRO_RANGE;
            if (dist > aggroRange * 1.5f)
            {
                _target = null;
                SetState(State.Patrol);
                return;
            }

            // In attack range?
            float attackRange = _data?.AttackRange ?? Constants.DEFAULT_ATTACK_RANGE;
            if (dist <= attackRange)
            {
                SetState(State.Attack);
                return;
            }

            // Chase
            if (_navAgent != null)
            {
                _navAgent.TargetPosition = _target.GlobalPosition;
                var nextPos = _navAgent.GetNextPathPosition();
                var direction = (nextPos - _body.GlobalPosition).Flat().Normalized();
                float speed = _data?.MoveSpeed ?? 3f;

                _body.Velocity = direction * speed;
                _body.MoveAndSlide();
                FaceDirection(direction);
            }
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

            if (dist > attackRange * 1.2f)
            {
                SetState(State.Chase);
                return;
            }

            // Face target
            var dir = (_target.GlobalPosition - _body.GlobalPosition).Flat().Normalized();
            FaceDirection(dir);

            // Attack is handled by EnemyCombat
            _body.Velocity = Vector3.Zero;
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
            if (ServiceLocator.TryGet<PlayerController>(out var player))
            {
                float dist = _body.GlobalPosition.FlatDistance(player.GlobalPosition);
                float aggroRange = _data?.AggroRange ?? Constants.DEFAULT_AGGRO_RANGE;

                if (dist <= aggroRange && player.Health.IsAlive)
                {
                    _target = player;
                    SetState(State.Chase);
                }
            }
        }

        public void SetState(State newState)
        {
            _currentState = newState;

            // Lazily grab IAnimatable from EnemyController
            if (_animatable == null)
            {
                _animatable = (_body as EnemyController)?.Animatable;
            }

            // Map AI state to animation state
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
