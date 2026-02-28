using UnityEngine;
using UnityEngine.AI;

namespace DungeonCrawlerCarl
{
    public enum CompanionState
    {
        Idle,
        Follow,
        Attack,
        UseAbility,
        Flee
    }

    [RequireComponent(typeof(CompanionController))]
    public class CompanionAI : MonoBehaviour
    {
        [Header("Behavior Settings")]
        [SerializeField] private float _aggroRange = 8f;
        [SerializeField] private float _attackRange = 2f;
        [SerializeField] private float _fleeThreshold = 0.2f;
        [SerializeField] private float _enemyScanInterval = 0.25f;
        [SerializeField] private float _idleWanderRadius = 2f;

        private CompanionController _controller;
        private NavMeshAgent _agent;
        private Transform _followTarget;
        private Transform _currentEnemy;
        private CompanionState _state = CompanionState.Idle;
        private float _scanTimer;

        public CompanionState CurrentState => _state;
        public Transform CurrentEnemy => _currentEnemy;
        public float AggroRange => _aggroRange;
        public float AttackRange => _attackRange;

        private void Awake()
        {
            _controller = GetComponent<CompanionController>();
        }

        private void Start()
        {
            _agent = _controller.Agent;
        }

        private void Update()
        {
            if (!_controller.Health.IsAlive) return;

            _scanTimer -= Time.deltaTime;
            if (_scanTimer <= 0f)
            {
                _scanTimer = _enemyScanInterval;
                ScanForEnemies();
            }

            EvaluateBehavior();
            ExecuteState();
        }

        /// <summary>
        /// Behavior priority evaluation:
        /// 1) Flee if health below threshold
        /// 2) Use ability if one is ready and we have a target
        /// 3) Attack nearest enemy in aggro range
        /// 4) Follow player if too far
        /// 5) Idle near player
        /// </summary>
        private void EvaluateBehavior()
        {
            // Priority 1: Flee if health is critically low
            if (_controller.Health.HealthPercent < _fleeThreshold)
            {
                _state = CompanionState.Flee;
                return;
            }

            // Priority 2: Use ability if one is ready and we have a target
            if (_currentEnemy != null && _controller.Abilities.HasReadyAbility())
            {
                float distToEnemy = Vector3.Distance(transform.position, _currentEnemy.position);
                if (distToEnemy <= _aggroRange)
                {
                    _state = CompanionState.UseAbility;
                    return;
                }
            }

            // Priority 3: Attack nearest enemy in aggro range
            if (_currentEnemy != null)
            {
                float distToEnemy = Vector3.Distance(transform.position, _currentEnemy.position);
                if (distToEnemy <= _aggroRange)
                {
                    _state = CompanionState.Attack;
                    return;
                }
            }

            // Priority 4: Follow player if too far
            if (_followTarget != null)
            {
                float distToPlayer = Vector3.Distance(transform.position, _followTarget.position);

                // Leash distance: teleport back if way too far
                if (distToPlayer > _controller.LeashDistance)
                {
                    TeleportToFollowTarget();
                    _state = CompanionState.Idle;
                    return;
                }

                if (distToPlayer > _controller.FollowDistance)
                {
                    _state = CompanionState.Follow;
                    return;
                }
            }

            // Priority 5: Idle near player
            _state = CompanionState.Idle;
        }

        private void ExecuteState()
        {
            switch (_state)
            {
                case CompanionState.Idle:
                    ExecuteIdle();
                    break;
                case CompanionState.Follow:
                    ExecuteFollow();
                    break;
                case CompanionState.Attack:
                    ExecuteAttack();
                    break;
                case CompanionState.UseAbility:
                    ExecuteUseAbility();
                    break;
                case CompanionState.Flee:
                    ExecuteFlee();
                    break;
            }
        }

        private void ExecuteIdle()
        {
            if (_agent.hasPath)
            {
                _agent.ResetPath();
            }
        }

        private void ExecuteFollow()
        {
            if (_followTarget == null) return;

            // Navigate toward a point near the player, offset by follow distance
            Vector3 dirToCompanion = (transform.position - _followTarget.position).normalized;
            Vector3 targetPos = _followTarget.position + dirToCompanion * (_controller.FollowDistance * 0.5f);

            _agent.SetDestination(targetPos);
        }

        private void ExecuteAttack()
        {
            if (_currentEnemy == null)
            {
                _state = CompanionState.Idle;
                return;
            }

            float distToEnemy = Vector3.Distance(transform.position, _currentEnemy.position);

            if (distToEnemy <= _attackRange)
            {
                // Stop and attack
                _agent.ResetPath();
                _controller.Combat.PerformAttack(_currentEnemy);
            }
            else
            {
                // Move toward enemy
                _agent.SetDestination(_currentEnemy.position);
            }
        }

        private void ExecuteUseAbility()
        {
            if (_currentEnemy == null)
            {
                _state = CompanionState.Attack;
                return;
            }

            float distToEnemy = Vector3.Distance(transform.position, _currentEnemy.position);

            if (distToEnemy <= _aggroRange)
            {
                _agent.ResetPath();
                _controller.Abilities.TryUseAbility(_currentEnemy);
            }

            // Fall back to attack after attempting ability
            _state = CompanionState.Attack;
        }

        private void ExecuteFlee()
        {
            if (_followTarget == null) return;

            // Run toward the player when fleeing
            _agent.SetDestination(_followTarget.position);

            // Resume normal behavior once we reach the player
            float distToPlayer = Vector3.Distance(transform.position, _followTarget.position);
            if (distToPlayer < _controller.FollowDistance)
            {
                _state = CompanionState.Idle;
            }
        }

        private void ScanForEnemies()
        {
            _currentEnemy = FindNearestEnemy();
        }

        private Transform FindNearestEnemy()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, _aggroRange, Constants.MASK_ENEMY);

            if (hits.Length == 0) return null;

            Transform nearest = null;
            float closestDist = float.MaxValue;

            foreach (var hit in hits)
            {
                var damageable = hit.GetComponent<IDamageable>();
                if (damageable == null || !damageable.IsAlive) continue;
                if (damageable.Team == Team.Player) continue;

                float dist = Vector3.Distance(transform.position, hit.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    nearest = hit.transform;
                }
            }

            return nearest;
        }

        private void TeleportToFollowTarget()
        {
            if (_followTarget == null) return;

            Vector3 offset = Random.insideUnitSphere * _controller.FollowDistance * 0.5f;
            offset.y = 0f;
            Vector3 targetPos = _followTarget.position + offset;

            if (NavMesh.SamplePosition(targetPos, out NavMeshHit navHit, _controller.FollowDistance, NavMesh.AllAreas))
            {
                _agent.Warp(navHit.position);
            }
        }

        public void SetFollowTarget(Transform target)
        {
            _followTarget = target;
        }

        public void SetAggroRange(float range)
        {
            _aggroRange = range;
        }

        public void SetAttackRange(float range)
        {
            _attackRange = range;
        }

        public void ForceAttackTarget(Transform target)
        {
            _currentEnemy = target;
            _state = CompanionState.Attack;
        }
    }
}
