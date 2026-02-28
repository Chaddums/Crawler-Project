using UnityEngine;

namespace DungeonCrawlerCarl
{
    [RequireComponent(typeof(PlayerMovement))]
    [RequireComponent(typeof(PlayerCombat))]
    [RequireComponent(typeof(PlayerInputHandler))]
    [RequireComponent(typeof(PlayerStats))]
    [RequireComponent(typeof(PlayerInventory))]
    public class PlayerController : MonoBehaviour, IItemReceiver
    {
        [Header("References")]
        [SerializeField] private SpriteRenderer _spriteRenderer;

        private HealthComponent _health;
        private PlayerMovement _movement;
        private PlayerCombat _combat;
        private PlayerInputHandler _input;
        private PlayerStats _stats;
        private PlayerInventory _inventory;
        private PlayerAnimator _animator;
        private SpriteDirectionSolver _directionSolver;

        public HealthComponent Health => _health;
        public PlayerMovement Movement => _movement;
        public PlayerCombat Combat => _combat;
        public PlayerStats Stats => _stats;
        public PlayerInventory Inventory => _inventory;
        public string PlayerName { get; private set; }
        public CrawlerClassData CurrentClassData { get; private set; }
        public RaceData CurrentRaceData { get; private set; }

        private void Awake()
        {
            _health = GetComponent<HealthComponent>();
            _movement = GetComponent<PlayerMovement>();
            _combat = GetComponent<PlayerCombat>();
            _input = GetComponent<PlayerInputHandler>();
            _stats = GetComponent<PlayerStats>();
            _inventory = GetComponent<PlayerInventory>();
            _animator = GetComponent<PlayerAnimator>();
            _directionSolver = GetComponent<SpriteDirectionSolver>();

            ServiceLocator.Register(this);
        }

        private void OnEnable()
        {
            _health.OnDeath += HandleDeath;
            _health.OnDamaged += HandleDamaged;
            _input.OnInteract += HandleInteract;
        }

        private void OnDisable()
        {
            _health.OnDeath -= HandleDeath;
            _health.OnDamaged -= HandleDamaged;
            _input.OnInteract -= HandleInteract;
        }

        private void Update()
        {
            if (_directionSolver != null && _movement.IsMoving)
            {
                _directionSolver.UpdateDirection(_movement.LastMoveDirection);
            }
        }

        public void Initialize(string playerName, CrawlerClassData classData, RaceData raceData)
        {
            PlayerName = playerName;
            CurrentClassData = classData;
            CurrentRaceData = raceData;

            // Apply race base stats
            if (raceData != null)
            {
                _stats.InitializeStats(raceData.BaseStats);

                // Apply racial passive stat bonuses
                if (raceData.RacialPassives != null)
                {
                    foreach (var passive in raceData.RacialPassives)
                    {
                        if (passive.StatBonuses == null) continue;
                        foreach (var mod in passive.StatBonuses)
                        {
                            mod.Source = passive;
                            _stats.Stats.AddModifier(mod);
                        }
                    }
                }
            }

            // Apply class stat growth for starting level
            if (classData != null)
            {
                foreach (var entry in classData.StatGrowthPerLevel.BaseStats)
                {
                    _stats.Stats.SetBaseStat(entry.Type,
                        _stats.Stats.GetBaseStat(entry.Type) + entry.BaseValue);
                }

                // Equip starting abilities
                for (int i = 0; i < classData.StartingAbilities.Count && i < Constants.MAX_ABILITY_SLOTS; i++)
                {
                    _combat.EquipAbility(classData.StartingAbilities[i], i);
                }
            }

            // Set health from constitution
            float maxHp = _stats.GetStat(StatType.MaxHealth);
            _health.SetMaxHealth(maxHp, true);

            // Set movement speed
            float moveSpeed = _stats.GetStat(StatType.MoveSpeed);
            if (moveSpeed > 0) _movement.SetMoveSpeed(moveSpeed);

            GameEvents.OnClassSelected?.Invoke(classData as ScriptableObject);
        }

        private void HandleDeath()
        {
            _input.DisableInput();
            _movement.Stop();
            GameEvents.OnPlayerDeath?.Invoke(gameObject);
        }

        private void HandleDamaged(DamageInfo damage)
        {
            // Flash sprite red, screen shake, etc. — handled by CombatFeedback listening to events
        }

        private void HandleInteract()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, 2f, Constants.MASK_INTERACTABLE);
            float closestDist = float.MaxValue;
            IInteractable closest = null;

            foreach (var hit in hits)
            {
                var interactable = hit.GetComponent<IInteractable>();
                if (interactable == null || !interactable.CanInteract) continue;

                float dist = Vector3.Distance(transform.position, hit.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = interactable;
                }
            }

            closest?.Interact(gameObject);
        }

        // --- IItemReceiver ---
        public string DisplayName => PlayerName;
        public bool TryAddItem(ItemInstance item) => _inventory.TryAddItem(item);

        private void OnDestroy()
        {
            ServiceLocator.Unregister<PlayerController>();
        }
    }
}
