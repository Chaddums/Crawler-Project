using UnityEngine;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Mongo-specific behavior for the velociraptor pet companion.
    /// Mongo is a simple, aggressive melee fighter that always charges
    /// the nearest enemy. Less complex AI than Donut -- pure offense.
    /// </summary>
    [RequireComponent(typeof(CompanionController))]
    public class MongoBehavior : MonoBehaviour
    {
        [Header("Mongo's Abilities")]
        [SerializeField] private AbilityData _biteAttack;

        [Header("Aggression Settings")]
        [SerializeField] private float _aggressiveAggroRange = 12f;
        [SerializeField] private float _biteRange = 1.5f;
        [SerializeField] private float _scanInterval = 0.15f;

        private CompanionController _controller;
        private CompanionAI _ai;
        private float _scanTimer;
        private Transform _aggressiveTarget;

        private void Awake()
        {
            _controller = GetComponent<CompanionController>();
            _ai = GetComponent<CompanionAI>();
        }

        private void Start()
        {
            // Override default AI ranges with more aggressive values
            _ai.SetAggroRange(_aggressiveAggroRange);
            _ai.SetAttackRange(_biteRange);

            // Equip bite attack if assigned
            if (_biteAttack != null)
            {
                _controller.Abilities.AddAbility(_biteAttack);
            }
        }

        private void Update()
        {
            if (!_controller.Health.IsAlive) return;

            _scanTimer -= Time.deltaTime;
            if (_scanTimer <= 0f)
            {
                _scanTimer = _scanInterval;
                AggressiveScan();
            }
        }

        /// <summary>
        /// Mongo constantly scans with a wider range and immediately
        /// forces attack on the nearest enemy, overriding normal AI priorities.
        /// Mongo does not flee -- he fights until downed.
        /// </summary>
        private void AggressiveScan()
        {
            Collider[] hits = Physics.OverlapSphere(
                transform.position,
                _aggressiveAggroRange,
                Constants.MASK_ENEMY
            );

            if (hits.Length == 0)
            {
                _aggressiveTarget = null;
                return;
            }

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

            if (nearest != null && nearest != _aggressiveTarget)
            {
                _aggressiveTarget = nearest;
                _ai.ForceAttackTarget(_aggressiveTarget);
            }
        }

        private void OnEnable()
        {
            GameEvents.OnEnemyKilled += HandleEnemyKilled;
        }

        private void OnDisable()
        {
            GameEvents.OnEnemyKilled -= HandleEnemyKilled;
        }

        /// <summary>
        /// When an enemy is killed, immediately scan for the next target
        /// so Mongo wastes no time standing idle.
        /// </summary>
        private void HandleEnemyKilled(GameObject enemy)
        {
            if (enemy.transform == _aggressiveTarget)
            {
                _aggressiveTarget = null;
                _scanTimer = 0f; // Force immediate rescan
            }
        }
    }
}
