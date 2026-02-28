using System;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    public class CombatManager : MonoBehaviour
    {
        [SerializeField] private GameObject _damageNumberPrefab;

        [Header("Combo System")]
        [SerializeField] private float _comboWindow = 2f;
        [SerializeField] private float _comboBonusPerHit = 0.1f;
        [SerializeField] private int _maxComboStacks = 10;

        private int _comboCount;
        private float _comboTimer;
        private string _lastAbilityId;

        public int ComboCount => _comboCount;
        public float ComboTimer => _comboTimer;
        public float ComboBonusMultiplier => 1f + _comboCount * _comboBonusPerHit;
        public bool IsComboActive => _comboCount > 0 && _comboTimer > 0f;

        public static event Action<int, float> OnComboChanged; // (count, bonusMultiplier)

        private void Awake()
        {
            ServiceLocator.Register(this);
        }

        private void OnEnable()
        {
            GameEvents.OnDamageDealt += HandleDamageDealt;
        }

        private void OnDisable()
        {
            GameEvents.OnDamageDealt -= HandleDamageDealt;
        }

        private void Update()
        {
            if (_comboCount > 0)
            {
                _comboTimer -= Time.deltaTime;
                if (_comboTimer <= 0f)
                {
                    ResetCombo();
                }
            }
        }

        private void HandleDamageDealt(DamageInfo damage)
        {
            SpawnDamageNumber(damage);

            // Only track player-initiated damage for combos
            if (damage.Attacker == null) return;
            if (!damage.Attacker.CompareTag(Constants.TAG_PLAYER)) return;

            AdvanceCombo();
        }

        /// <summary>
        /// Call when a player ability successfully hits.
        /// Different abilities in sequence build higher combos.
        /// </summary>
        public void RegisterAbilityHit(string abilityId)
        {
            // Bonus: using different abilities extends the combo further
            if (abilityId != _lastAbilityId && _comboCount > 0)
            {
                _comboTimer = _comboWindow * 1.25f; // Reward variety with extra time
            }
            _lastAbilityId = abilityId;
        }

        private void AdvanceCombo()
        {
            if (_comboCount < _maxComboStacks)
                _comboCount++;

            _comboTimer = _comboWindow;
            OnComboChanged?.Invoke(_comboCount, ComboBonusMultiplier);
        }

        private void ResetCombo()
        {
            if (_comboCount == 0) return;

            _comboCount = 0;
            _comboTimer = 0f;
            _lastAbilityId = null;
            OnComboChanged?.Invoke(0, 1f);
        }

        /// <summary>
        /// Returns the current combo damage multiplier and optionally applies it to a damage value.
        /// </summary>
        public float ApplyComboBonus(float baseDamage)
        {
            return baseDamage * ComboBonusMultiplier;
        }

        private void SpawnDamageNumber(DamageInfo damage)
        {
            if (_damageNumberPrefab == null) return;

            Vector3 spawnPos = damage.HitPoint + Vector3.up * 1.5f;
            spawnPos += new Vector3(UnityEngine.Random.Range(-0.3f, 0.3f), UnityEngine.Random.Range(0f, 0.3f), 0f);

            var dmgNumObj = Instantiate(_damageNumberPrefab, spawnPos, Quaternion.identity);
            var dmgNum = dmgNumObj.GetComponent<DamageNumber>();
            if (dmgNum != null)
                dmgNum.Initialize(damage);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<CombatManager>();
        }
    }
}
