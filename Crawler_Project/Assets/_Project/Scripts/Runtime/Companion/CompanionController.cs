using UnityEngine;
using UnityEngine.AI;

namespace DungeonCrawlerCarl
{
    [RequireComponent(typeof(HealthComponent))]
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(CompanionAI))]
    [RequireComponent(typeof(CompanionCombat))]
    [RequireComponent(typeof(CompanionAbilities))]
    public class CompanionController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private CompanionData _companionData;

        [Header("Behavior Tuning")]
        [SerializeField] private float _followDistance = 3f;
        [SerializeField] private float _leashDistance = 15f;

        private StatBlock _stats = new();
        private HealthComponent _health;
        private NavMeshAgent _agent;
        private CompanionAI _ai;
        private CompanionCombat _combat;
        private CompanionAbilities _abilities;

        public CompanionData Data => _companionData;
        public StatBlock Stats => _stats;
        public HealthComponent Health => _health;
        public NavMeshAgent Agent => _agent;
        public CompanionAI AI => _ai;
        public CompanionCombat Combat => _combat;
        public CompanionAbilities Abilities => _abilities;
        public float FollowDistance => _followDistance;
        public float LeashDistance => _leashDistance;

        private void Awake()
        {
            _health = GetComponent<HealthComponent>();
            _agent = GetComponent<NavMeshAgent>();
            _ai = GetComponent<CompanionAI>();
            _combat = GetComponent<CompanionCombat>();
            _abilities = GetComponent<CompanionAbilities>();
        }

        private void Start()
        {
            if (_companionData != null)
            {
                Initialize(_companionData);
            }

            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                _ai.SetFollowTarget(playerObj.transform);
            }
            else
            {
                Debug.LogWarning($"[CompanionController] No Player found for {_companionData?.CompanionName ?? "companion"}.");
            }

            ServiceLocator.Register(this);
            GameEvents.OnCompanionSummoned?.Invoke(gameObject);
        }

        public void Initialize(CompanionData data)
        {
            _companionData = data;

            // Copy base stats from data asset
            _stats.CopyBaseStatsFrom(data.BaseStats);

            // Apply follow distance and aggro range from data
            _followDistance = data.BaseFollowDistance;
            _ai.SetAggroRange(data.BaseAggroRange);

            // Set health from stats
            float maxHp = _stats.GetStat(StatType.MaxHealth);
            if (maxHp > 0)
            {
                _health.SetMaxHealth(maxHp, true);
            }

            _health.SetTeam(Team.Player);

            // Set NavMeshAgent speed from stats
            float moveSpeed = _stats.GetStat(StatType.MoveSpeed);
            if (moveSpeed > 0)
            {
                _agent.speed = moveSpeed;
            }

            // Initialize starting abilities
            _abilities.InitializeAbilities(data.StartingAbilities);

            Debug.Log($"[CompanionController] Initialized companion: {data.CompanionName} - \"{data.Title}\"");
        }

        private void OnEnable()
        {
            _health.OnDeath += HandleDeath;
        }

        private void OnDisable()
        {
            _health.OnDeath -= HandleDeath;
        }

        private void HandleDeath()
        {
            _ai.enabled = false;
            _combat.enabled = false;
            _agent.isStopped = true;
            Debug.Log($"[CompanionController] {_companionData?.CompanionName ?? "Companion"} has been downed!");
        }

        public void Revive(float healthPercent = 1f)
        {
            _health.Revive(healthPercent);
            _ai.enabled = true;
            _combat.enabled = true;
            _agent.isStopped = false;
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<CompanionController>();
        }
    }
}
