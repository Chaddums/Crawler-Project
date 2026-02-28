using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Companion AI state machine: Follow player, attack nearby enemies, idle near player.
    /// </summary>
    public partial class CompanionAI : Node, IKnockbackable
    {
        public enum State { Idle, Follow, Chase, Attack, Stunned, Dead }

        private CompanionData _data;
        private StatBlock _stats;
        private CharacterBody3D _body;
        private NavigationAgent3D _navAgent;

        private State _currentState = State.Follow;
        private Node3D _target;
        private float _stunTimer;
        private Vector3 _knockbackVelocity;

        public State CurrentState => _currentState;
        public Node3D Target => _target;

        public override void _Ready()
        {
            _body = GetParent<CharacterBody3D>();
            _navAgent = _body.GetNodeOrNull<NavigationAgent3D>("NavigationAgent3D");
        }

        public void Initialize(CompanionData data, StatBlock stats)
        {
            _data = data;
            _stats = stats;
            SetState(State.Follow);
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
                case State.Follow:
                    ProcessFollow(dt);
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
            // Check for nearby enemies
            var enemy = FindNearestEnemy();
            if (enemy != null)
            {
                _target = enemy;
                SetState(State.Chase);
                return;
            }

            // If too far from player, follow
            if (!ServiceLocator.TryGet<PlayerController>(out var player)) return;
            float distToPlayer = _body.GlobalPosition.FlatDistance(player.GlobalPosition);
            float followDist = _data?.FollowDistance ?? 3f;

            if (distToPlayer > followDist * 2f)
                SetState(State.Follow);
        }

        private void ProcessFollow(float dt)
        {
            if (!ServiceLocator.TryGet<PlayerController>(out var player)) return;

            // Check for enemies while following
            var enemy = FindNearestEnemy();
            if (enemy != null)
            {
                _target = enemy;
                SetState(State.Chase);
                return;
            }

            float distToPlayer = _body.GlobalPosition.FlatDistance(player.GlobalPosition);
            float followDist = _data?.FollowDistance ?? 3f;

            // Close enough — idle
            if (distToPlayer <= followDist)
            {
                _body.Velocity = Vector3.Zero;
                _body.MoveAndSlide();
                SetState(State.Idle);
                return;
            }

            // Teleport if way too far (got stuck or separated)
            if (distToPlayer > 30f)
            {
                _body.GlobalPosition = player.GlobalPosition + new Vector3(2, 0, 2);
                return;
            }

            // Navigate toward player
            if (_navAgent != null)
            {
                _navAgent.TargetPosition = player.GlobalPosition;
                var nextPos = _navAgent.GetNextPathPosition();
                var direction = (nextPos - _body.GlobalPosition).Flat().Normalized();
                float speed = _data?.MoveSpeed ?? 7f;

                _body.Velocity = direction * speed;
                _body.MoveAndSlide();
                FaceDirection(direction);
            }
        }

        private void ProcessChase(float dt)
        {
            if (_target == null || !IsInstanceValid(_target))
            {
                _target = null;
                SetState(State.Follow);
                return;
            }

            // Check if target is still alive
            var health = _target.GetNodeOrNull<HealthComponent>("HealthComponent");
            if (health != null && !health.IsAlive)
            {
                _target = null;
                SetState(State.Follow);
                return;
            }

            float dist = _body.GlobalPosition.FlatDistance(_target.GlobalPosition);
            float attackRange = _data?.AttackRange ?? 1.5f;

            // In attack range
            if (dist <= attackRange)
            {
                SetState(State.Attack);
                return;
            }

            // Too far from player — return
            if (ServiceLocator.TryGet<PlayerController>(out var player))
            {
                float distToPlayer = _body.GlobalPosition.FlatDistance(player.GlobalPosition);
                if (distToPlayer > (_data?.AggroRange ?? 8f) * 1.5f)
                {
                    _target = null;
                    SetState(State.Follow);
                    return;
                }
            }

            // Chase target
            if (_navAgent != null)
            {
                _navAgent.TargetPosition = _target.GlobalPosition;
                var nextPos = _navAgent.GetNextPathPosition();
                var direction = (nextPos - _body.GlobalPosition).Flat().Normalized();
                float speed = _data?.MoveSpeed ?? 7f;

                _body.Velocity = direction * speed;
                _body.MoveAndSlide();
                FaceDirection(direction);
            }
        }

        private void ProcessAttack(float dt)
        {
            if (_target == null || !IsInstanceValid(_target))
            {
                _target = null;
                SetState(State.Follow);
                return;
            }

            float dist = _body.GlobalPosition.FlatDistance(_target.GlobalPosition);
            float attackRange = _data?.AttackRange ?? 1.5f;

            if (dist > attackRange * 1.3f)
            {
                SetState(State.Chase);
                return;
            }

            // Face target
            var dir = (_target.GlobalPosition - _body.GlobalPosition).Flat().Normalized();
            FaceDirection(dir);

            _body.Velocity = Vector3.Zero;
            _body.MoveAndSlide();
        }

        private void ProcessStunned(float dt)
        {
            _stunTimer -= dt;
            if (_stunTimer <= 0)
                SetState(_target != null ? State.Chase : State.Follow);
        }

        private Node3D FindNearestEnemy()
        {
            float aggroRange = _data?.AggroRange ?? 8f;
            float closestDist = aggroRange;
            Node3D closest = null;

            foreach (var node in _body.GetTree().GetNodesInGroup(Constants.GROUP_ENEMY))
            {
                if (node is not Node3D enemy3d) continue;
                var health = enemy3d.GetNodeOrNull<HealthComponent>("HealthComponent");
                if (health == null || !health.IsAlive) continue;

                float dist = _body.GlobalPosition.FlatDistance(enemy3d.GlobalPosition);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = enemy3d;
                }
            }

            return closest;
        }

        public void SetState(State newState)
        {
            _currentState = newState;
        }

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
