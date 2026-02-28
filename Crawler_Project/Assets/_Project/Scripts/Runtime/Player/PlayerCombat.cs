using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonCrawlerCarl
{
    public class PlayerCombat : MonoBehaviour, IAttacker
    {
        [Header("Basic Attack")]
        [SerializeField] private float _basicAttackRange = 2f;
        [SerializeField] private float _basicAttackCooldown = 0.5f;
        [SerializeField] private GameObject _basicAttackPrefab;

        [Header("Abilities")]
        [SerializeField] private int _maxAbilitySlots = Constants.MAX_ABILITY_SLOTS;

        private PlayerStats _playerStats;
        private PlayerInputHandler _input;
        private PlayerMovement _movement;
        private Camera _mainCamera;

        private List<AbilitySlot> _abilitySlots = new();
        private float _basicAttackTimer;

        public StatBlock Stats => _playerStats.Stats;
        public Transform Transform => transform;
        public Team Team => Team.Player;
        public IReadOnlyList<AbilitySlot> AbilitySlots => _abilitySlots;

        private void Awake()
        {
            _playerStats = GetComponent<PlayerStats>();
            _input = GetComponent<PlayerInputHandler>();
            _movement = GetComponent<PlayerMovement>();
            _mainCamera = Camera.main;

            for (int i = 0; i < _maxAbilitySlots; i++)
                _abilitySlots.Add(new AbilitySlot());
        }

        private void OnEnable()
        {
            _input.OnClickToMove += HandleBasicAttack;
            _input.OnAbilityInput += HandleAbilityInput;
        }

        private void OnDisable()
        {
            _input.OnClickToMove -= HandleBasicAttack;
            _input.OnAbilityInput -= HandleAbilityInput;
        }

        private void Update()
        {
            _basicAttackTimer -= Time.deltaTime;

            float cdr = _playerStats.GetStat(StatType.CooldownReduction);
            foreach (var slot in _abilitySlots)
                slot.Tick(Time.deltaTime, cdr);
        }

        private void HandleBasicAttack()
        {
            if (_basicAttackTimer > 0) return;
            if (_mainCamera == null) return;

            Ray ray = _mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (!Physics.Raycast(ray, out RaycastHit hit, 100f)) return;

            var hurtBox = hit.collider.GetComponent<HurtBox>();
            if (hurtBox == null || hurtBox.Team == Team.Player) return;

            float distance = Vector3.Distance(transform.position, hit.point);
            if (distance > _basicAttackRange) return;

            _movement.Stop();
            _basicAttackTimer = _basicAttackCooldown / (1f + _playerStats.GetStat(StatType.AttackSpeed));

            var damage = DamageCalculator.CalculateBasicAttack(this);

            // Apply combo bonus
            var combatMgr = ServiceLocator.TryGet<CombatManager>(out var cm) ? cm : null;
            if (combatMgr != null)
                damage.FinalDamage = combatMgr.ApplyComboBonus(damage.FinalDamage);

            damage.Target = hurtBox.gameObject;
            damage.HitPoint = hit.point;
            hurtBox.Damageable.TakeDamage(damage);

            if (_basicAttackPrefab != null)
            {
                var vfx = Instantiate(_basicAttackPrefab, hit.point, Quaternion.identity);
                Destroy(vfx, 0.5f);
            }
        }

        private void HandleAbilityInput(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _abilitySlots.Count) return;

            var slot = _abilitySlots[slotIndex];
            if (!slot.IsReady) return;

            float manaCost = slot.Ability.ManaCost;
            if (manaCost > 0f && _playerStats.CurrentMana < manaCost) return;

            Vector3 targetPoint = GetMouseWorldPosition();
            var executor = ServiceLocator.Get<AbilityExecutor>();
            if (executor != null && executor.Execute(slot.Ability, this, targetPoint, null))
            {
                _playerStats.SpendMana(manaCost);
                slot.Use();

                // Register with combo tracker
                if (ServiceLocator.TryGet<CombatManager>(out var combatMgr))
                    combatMgr.RegisterAbilityHit(slot.Ability.AbilityId);
            }
        }

        private Vector3 GetMouseWorldPosition()
        {
            if (_mainCamera == null) return transform.position;

            Ray ray = _mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, Constants.MASK_GROUND))
                return hit.point;

            return transform.position;
        }

        public void EquipAbility(AbilityData ability, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _abilitySlots.Count) return;
            _abilitySlots[slotIndex].Ability = ability;
        }

        public void ClearAbility(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _abilitySlots.Count) return;
            _abilitySlots[slotIndex].Ability = null;
        }
    }
}
