using UnityEngine;

namespace DungeonCrawlerCarl
{
    [RequireComponent(typeof(CompanionController))]
    public class CompanionCombat : MonoBehaviour, IAttacker
    {
        [Header("Basic Attack")]
        [SerializeField] private float _basicAttackCooldown = 1f;
        [SerializeField] private GameObject _attackVfxPrefab;

        private CompanionController _controller;
        private float _attackTimer;

        public StatBlock Stats => _controller.Stats;
        public Transform Transform => transform;
        public Team Team => Team.Player;
        public bool CanAttack => _attackTimer <= 0f;

        private void Awake()
        {
            _controller = GetComponent<CompanionController>();
        }

        private void Update()
        {
            if (_attackTimer > 0f)
            {
                _attackTimer -= Time.deltaTime;
            }
        }

        public void PerformAttack(Transform target)
        {
            if (!CanAttack) return;
            if (target == null) return;

            var damageable = target.GetComponent<IDamageable>();
            if (damageable == null || !damageable.IsAlive) return;

            // Calculate cooldown based on attack speed stat
            float attackSpeed = _controller.Stats.GetStat(StatType.AttackSpeed);
            _attackTimer = _basicAttackCooldown / (1f + attackSpeed);

            // Calculate and apply damage
            var damage = DamageCalculator.CalculateBasicAttack(this);
            damage.Target = target.gameObject;
            damage.HitPoint = target.position;

            damageable.TakeDamage(damage);

            // Spawn attack VFX if assigned
            if (_attackVfxPrefab != null)
            {
                var vfx = Instantiate(_attackVfxPrefab, target.position, Quaternion.identity);
                Destroy(vfx, 0.5f);
            }

            // Play combat sound
            PlayCombatSound();
        }

        private void PlayCombatSound()
        {
            if (_controller.Data == null) return;
            if (_controller.Data.CombatSounds == null || _controller.Data.CombatSounds.Length == 0) return;

            var clip = _controller.Data.CombatSounds[Random.Range(0, _controller.Data.CombatSounds.Length)];
            if (clip != null)
            {
                AudioSource.PlayClipAtPoint(clip, transform.position);
            }
        }
    }
}
