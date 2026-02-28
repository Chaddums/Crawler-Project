using UnityEngine;
using UnityEngine.AI;

namespace DungeonCrawlerCarl
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyAI : MonoBehaviour, IKnockbackable
    {
        public enum AIState
        {
            Idle,
            Patrol,
            Chase,
            Attack,
            Flee,
            Stunned,
            Dead
        }

        [Header("Detection")]
        [SerializeField] private float _detectionRange = Constants.DEFAULT_AGGRO_RANGE;
        [SerializeField] private float _attackRange = Constants.DEFAULT_ATTACK_RANGE;
        [SerializeField] private float _fleeHealthPercent = 0.2f;

        [Header("Patrol")]
        [SerializeField] private float _patrolRadius = 5f;
        [SerializeField] private float _patrolWaitTime = 2f;
        [SerializeField] private float _idleWaitTime = 3f;
        [SerializeField] private float _maxFleeTime = 5f;

        private NavMeshAgent _agent;
        private EnemyController _controller;
        private EnemyCombat _combat;
        private EnemyAnimator _animator;
        private HealthComponent _health;

        private AIState _currentState = AIState.Idle;
        private Transform _currentTarget;
        private Vector3 _spawnPosition;
        private float _stateTimer;
        private float _patrolWaitTimer;

        private static readonly Collider[] _detectionBuffer = new Collider[16];
        private float _stunTimer;
        private FormationCoordinator _formation;

        public AIState CurrentState => _currentState;
        public Transform CurrentTarget => _currentTarget;
        public bool IsMoving => _agent != null && _agent.hasPath && _agent.remainingDistance > _agent.stoppingDistance;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _combat = GetComponent<EnemyCombat>();
            _animator = GetComponent<EnemyAnimator>();
            _health = GetComponent<HealthComponent>();
        }

        private void Start()
        {
            _spawnPosition = transform.position;
        }

        public void Initialize(EnemyController controller)
        {
            _controller = controller;

            // Set agent speed from stats
            float moveSpeed = controller.RuntimeStats.GetStat(StatType.MoveSpeed);
            if (moveSpeed > 0f)
                _agent.speed = moveSpeed;
            else
                _agent.speed = Constants.DEFAULT_MOVE_SPEED;

            // For 2.5D/top-down, prevent NavMeshAgent from rotating the transform
            _agent.updateRotation = false;
            _agent.updateUpAxis = false;

            SetState(AIState.Idle);
        }

        private void Update()
        {
            if (_currentState == AIState.Dead) return;

            if (_currentState == AIState.Stunned)
            {
                UpdateStunned();
                return;
            }

            // Check for flee condition
            if (_currentState != AIState.Flee && _health != null && _health.HealthPercent <= _fleeHealthPercent)
            {
                SetState(AIState.Flee);
                return;
            }

            switch (_currentState)
            {
                case AIState.Idle:
                    UpdateIdle();
                    break;
                case AIState.Patrol:
                    UpdatePatrol();
                    break;
                case AIState.Chase:
                    UpdateChase();
                    break;
                case AIState.Attack:
                    UpdateAttack();
                    break;
                case AIState.Flee:
                    UpdateFlee();
                    break;
            }

            // Update animator movement state
            if (_animator != null)
                _animator.SetMoving(IsMoving);
        }

        public void SetState(AIState newState)
        {
            if (_currentState == AIState.Dead && newState != AIState.Dead) return;

            _currentState = newState;
            _stateTimer = 0f;

            switch (newState)
            {
                case AIState.Idle:
                    _agent.ResetPath();
                    break;

                case AIState.Patrol:
                    NavigateToRandomPatrolPoint();
                    break;

                case AIState.Chase:
                    PlayAggroSound();
                    break;

                case AIState.Attack:
                    _agent.ResetPath();
                    break;

                case AIState.Flee:
                    NavigateAwayFromTarget();
                    break;

                case AIState.Stunned:
                    _agent.ResetPath();
                    break;

                case AIState.Dead:
                    _agent.ResetPath();
                    _agent.enabled = false;
                    break;
            }
        }

        // --------------------------------------------------
        // State Updates
        // --------------------------------------------------

        private void UpdateIdle()
        {
            _stateTimer += Time.deltaTime;

            // Check for targets in range
            _currentTarget = FindPlayerOrCompanionInRange();
            if (_currentTarget != null)
            {
                SetState(AIState.Chase);
                return;
            }

            // After idling for a while, start patrolling
            if (_stateTimer >= _idleWaitTime)
            {
                SetState(AIState.Patrol);
            }
        }

        private void UpdatePatrol()
        {
            // Check for targets while patrolling
            _currentTarget = FindPlayerOrCompanionInRange();
            if (_currentTarget != null)
            {
                SetState(AIState.Chase);
                return;
            }

            // If we've reached the patrol destination, wait then pick a new one
            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance)
            {
                _patrolWaitTimer += Time.deltaTime;
                if (_patrolWaitTimer >= _patrolWaitTime)
                {
                    _patrolWaitTimer = 0f;
                    NavigateToRandomPatrolPoint();
                }
            }
        }

        private void UpdateChase()
        {
            // Lost target check
            if (_currentTarget == null || !IsTargetAlive(_currentTarget))
            {
                _currentTarget = FindPlayerOrCompanionInRange();
                if (_currentTarget == null)
                {
                    SetState(AIState.Idle);
                    return;
                }
            }

            float distance = Vector3.Distance(transform.position, _currentTarget.position);

            // Target left detection range — return to idle
            if (distance > _detectionRange * 1.5f)
            {
                _currentTarget = null;
                SetState(AIState.Idle);
                return;
            }

            // In attack range — switch to attack
            if (distance <= _attackRange)
            {
                SetState(AIState.Attack);
                return;
            }

            // Continue chasing — use formation offset if coordinated
            Vector3 destination = _formation != null
                ? _formation.GetFormationDestination(this)
                : _currentTarget.position;
            _agent.SetDestination(destination);
        }

        private void UpdateAttack()
        {
            if (_currentTarget == null || !IsTargetAlive(_currentTarget))
            {
                _currentTarget = FindPlayerOrCompanionInRange();
                if (_currentTarget == null)
                {
                    SetState(AIState.Idle);
                    return;
                }
            }

            float distance = Vector3.Distance(transform.position, _currentTarget.position);

            // Target moved out of attack range — chase again
            if (distance > _attackRange * 1.2f)
            {
                SetState(AIState.Chase);
                return;
            }

            // Face the target
            Vector3 direction = (_currentTarget.position - transform.position).normalized;
            if (direction != Vector3.zero)
                transform.forward = new Vector3(direction.x, 0f, direction.z);

            // Attempt to attack
            if (_combat != null)
            {
                var damageable = _currentTarget.GetComponent<IDamageable>();
                if (damageable == null)
                    damageable = _currentTarget.GetComponentInParent<IDamageable>();

                if (damageable != null)
                    _combat.PerformAttack(damageable);
            }
        }

        private void UpdateFlee()
        {
            _stateTimer += Time.deltaTime;

            // If health recovered above threshold, stop fleeing
            if (_health != null && _health.HealthPercent > _fleeHealthPercent)
            {
                SetState(AIState.Idle);
                return;
            }

            // Cap flee duration — eventually turn and fight
            if (_stateTimer >= _maxFleeTime)
            {
                SetState(AIState.Chase);
                return;
            }

            // Re-navigate away periodically
            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance)
            {
                NavigateAwayFromTarget();
            }
        }

        private void UpdateStunned()
        {
            _stunTimer -= Time.deltaTime;
            if (_stunTimer <= 0f)
            {
                SetState(AIState.Idle);
            }
        }

        public void SetFormation(FormationCoordinator formation)
        {
            _formation = formation;
        }

        public void ApplyStun(float duration)
        {
            if (_currentState == AIState.Dead || duration <= 0f) return;
            _stunTimer = duration;
            SetState(AIState.Stunned);
        }

        public void ApplyKnockback(Vector3 sourcePosition, float force)
        {
            if (_currentState == AIState.Dead || force <= 0f || _agent == null) return;

            Vector3 knockDir = (transform.position - sourcePosition).normalized;
            knockDir.y = 0f;
            Vector3 knockTarget = transform.position + knockDir * force;

            if (UnityEngine.AI.NavMesh.SamplePosition(knockTarget, out var navHit, force, UnityEngine.AI.NavMesh.AllAreas))
            {
                _agent.Warp(navHit.position);
            }
        }

        // --------------------------------------------------
        // Detection
        // --------------------------------------------------

        public Transform FindPlayerOrCompanionInRange()
        {
            int mask = Constants.MASK_PLAYER | (1 << Constants.LAYER_COMPANION);
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, _detectionRange, _detectionBuffer, mask);

            Transform closest = null;
            float closestDist = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                var hit = _detectionBuffer[i];
                var damageable = hit.GetComponent<IDamageable>();
                if (damageable == null)
                    damageable = hit.GetComponentInParent<IDamageable>();

                if (damageable == null || !damageable.IsAlive) continue;
                if (damageable.Team == Team.Enemy) continue;

                float dist = Vector3.Distance(transform.position, hit.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = hit.transform;
                }
            }

            return closest;
        }

        // --------------------------------------------------
        // Helpers
        // --------------------------------------------------

        private bool IsTargetAlive(Transform target)
        {
            if (target == null) return false;

            var damageable = target.GetComponent<IDamageable>();
            if (damageable == null)
                damageable = target.GetComponentInParent<IDamageable>();

            return damageable != null && damageable.IsAlive;
        }

        private void NavigateToRandomPatrolPoint()
        {
            Vector3 randomDirection = Random.insideUnitSphere * _patrolRadius;
            randomDirection += _spawnPosition;
            randomDirection.y = _spawnPosition.y;

            if (NavMesh.SamplePosition(randomDirection, out NavMeshHit navHit, _patrolRadius, NavMesh.AllAreas))
            {
                _agent.SetDestination(navHit.position);
            }
        }

        private void NavigateAwayFromTarget()
        {
            Vector3 fleeDirection;

            if (_currentTarget != null)
                fleeDirection = (transform.position - _currentTarget.position).normalized;
            else
                fleeDirection = Random.insideUnitSphere.normalized;

            Vector3 fleePoint = transform.position + fleeDirection * _detectionRange;
            fleePoint.y = transform.position.y;

            if (NavMesh.SamplePosition(fleePoint, out NavMeshHit navHit, _detectionRange, NavMesh.AllAreas))
            {
                _agent.SetDestination(navHit.position);
            }
        }

        private void PlayAggroSound()
        {
            if (_controller == null || _controller.EnemyData == null) return;

            var aggroSounds = _controller.EnemyData.AggroSounds;
            if (aggroSounds != null && aggroSounds.Length > 0)
            {
                var clip = aggroSounds[Random.Range(0, aggroSounds.Length)];
                if (clip != null)
                    AudioSource.PlayClipAtPoint(clip, transform.position);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _detectionRange);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _attackRange);

            Gizmos.color = Color.blue;
            Vector3 origin = Application.isPlaying ? _spawnPosition : transform.position;
            Gizmos.DrawWireSphere(origin, _patrolRadius);
        }
    }
}
