using UnityEngine;

namespace DungeonCrawlerCarl
{
    public class EnemyCombat : MonoBehaviour, IAttacker
    {
        [Header("Basic Attack")]
        [SerializeField] private float _attackCooldown = 1.5f;
        [SerializeField] private GameObject _basicAttackPrefab;

        private EnemyController _controller;
        private EnemyAnimator _animator;
        private float _attackTimer;

        public StatBlock Stats => _controller != null ? _controller.RuntimeStats : null;
        public Transform Transform => transform;
        public Team Team => Team.Enemy;

        public bool CanAttack => _attackTimer <= 0f;

        private void Update()
        {
            if (_attackTimer > 0f)
                _attackTimer -= Time.deltaTime;
        }

        public void Initialize(EnemyController controller)
        {
            _controller = controller;
            _animator = GetComponent<EnemyAnimator>();

            // Scale cooldown by attack speed stat
            float attackSpeed = controller.RuntimeStats.GetStat(StatType.AttackSpeed);
            if (attackSpeed > 0f)
                _attackCooldown = _attackCooldown / (1f + attackSpeed);
        }

        public void PerformAttack(IDamageable target)
        {
            if (!CanAttack) return;
            if (target == null || !target.IsAlive) return;
            if (target.Team == Team.Enemy) return;

            _attackTimer = _attackCooldown;

            // Calculate damage using the existing DamageCalculator
            var damage = DamageCalculator.CalculateBasicAttack(this);
            damage.Target = target.Transform.gameObject;
            damage.HitPoint = target.Transform.position;

            // Apply armor reduction
            float armor = 0f;
            var targetAttacker = target.Transform.GetComponent<IAttacker>();
            if (targetAttacker != null)
            {
                armor = targetAttacker.Stats.GetStat(StatType.Armor);
            }

            if (armor > 0f)
                damage.FinalDamage = DamageCalculator.ApplyArmor(damage.FinalDamage, armor);

            // Apply damage
            target.TakeDamage(damage);

            // Trigger attack animation
            if (_animator != null)
                _animator.TriggerAttack();

            // Spawn VFX
            if (_basicAttackPrefab != null)
            {
                var vfx = Instantiate(_basicAttackPrefab, target.Transform.position, Quaternion.identity);
                Destroy(vfx, 0.5f);
            }
        }
    }
}
