using UnityEngine;

namespace DungeonCrawlerCarl
{
    [RequireComponent(typeof(HealthComponent))]
    public class EnemyController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private EnemyData _enemyData;

        private HealthComponent _health;
        private EnemyAI _ai;
        private EnemyCombat _combat;
        private EnemyAnimator _animator;
        private float _difficultyMultiplier = 1f;

        public EnemyData EnemyData => _enemyData;
        public HealthComponent Health => _health;
        public EnemyAI AI => _ai;
        public EnemyCombat Combat => _combat;
        public StatBlock RuntimeStats { get; private set; } = new();

        public bool IsAlive => _health != null && _health.IsAlive;

        private void Awake()
        {
            _health = GetComponent<HealthComponent>();
            _ai = GetComponent<EnemyAI>();
            _combat = GetComponent<EnemyCombat>();
            _animator = GetComponent<EnemyAnimator>();
        }

        private void Start()
        {
            if (_enemyData != null)
                Initialize(_enemyData);
        }

        private void OnEnable()
        {
            _health.OnDeath += HandleDeath;
        }

        private void OnDisable()
        {
            _health.OnDeath -= HandleDeath;
        }

        public void Initialize(EnemyData data) => Initialize(data, 1f);

        public void Initialize(EnemyData data, float difficultyMultiplier)
        {
            _enemyData = data;
            _difficultyMultiplier = difficultyMultiplier;

            // Copy base stats from data so runtime modifiers don't affect the asset
            RuntimeStats.CopyBaseStatsFrom(data.BaseStats);

            // Apply difficulty scaling to combat stats
            if (difficultyMultiplier > 1f)
            {
                ScaleStat(StatType.MaxHealth, difficultyMultiplier);
                ScaleStat(StatType.Strength, difficultyMultiplier);
                ScaleStat(StatType.Armor, difficultyMultiplier);
            }

            // Configure health
            float maxHp = RuntimeStats.GetStat(StatType.MaxHealth);
            if (maxHp <= 0f) maxHp = 100f;
            _health.SetMaxHealth(maxHp, true);
            _health.SetTeam(Team.Enemy);

            // Set up combat component
            if (_combat != null)
                _combat.Initialize(this);

            // Set up AI component
            if (_ai != null)
                _ai.Initialize(this);

            // Set up animator controller if provided
            if (_animator != null && data.AnimatorController != null)
            {
                var animatorComponent = GetComponentInChildren<Animator>();
                if (animatorComponent != null)
                    animatorComponent.runtimeAnimatorController = data.AnimatorController;
            }

            gameObject.name = data.EnemyName;
            gameObject.tag = Constants.TAG_ENEMY;
            gameObject.layer = Constants.LAYER_ENEMY;
        }

        private void HandleDeath()
        {
            // Fire global event
            GameEvents.OnEnemyKilled?.Invoke(gameObject);

            // Award experience to player via event (PlayerStats subscribes to this)
            if (_enemyData != null && _enemyData.ExperienceReward > 0)
            {
                int scaledXp = Mathf.RoundToInt(_enemyData.ExperienceReward * _difficultyMultiplier);
                GameEvents.OnExperienceGained?.Invoke(scaledXp);
            }

            // Drop loot
            if (_enemyData != null && _enemyData.LootTable != null)
            {
                var lootDropper = GetComponent<LootDropper>();
                if (lootDropper != null)
                    lootDropper.DropLoot(_enemyData.LootTable, transform.position);
            }

            // Play death sound
            if (_enemyData != null && _enemyData.DeathSounds != null && _enemyData.DeathSounds.Length > 0)
            {
                var clip = _enemyData.DeathSounds[Random.Range(0, _enemyData.DeathSounds.Length)];
                if (clip != null)
                    AudioSource.PlayClipAtPoint(clip, transform.position);
            }

            // Notify AI to enter dead state
            if (_ai != null)
                _ai.SetState(EnemyAI.AIState.Dead);

            Debug.Log($"[EnemyController] {_enemyData?.EnemyName ?? gameObject.name} was killed.");
        }

        private void ScaleStat(StatType type, float multiplier)
        {
            float baseVal = RuntimeStats.GetBaseStat(type);
            if (baseVal > 0f)
                RuntimeStats.SetBaseStat(type, baseVal * multiplier);
        }
    }
}
