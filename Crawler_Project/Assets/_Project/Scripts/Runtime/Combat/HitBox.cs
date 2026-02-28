using System.Collections.Generic;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    [RequireComponent(typeof(Collider))]
    public class HitBox : MonoBehaviour
    {
        [SerializeField] private Team _team;
        [SerializeField] private float _lifetime = 0f;

        public DamageInfo DamagePayload { get; set; }
        public IAttacker Owner { get; set; }
        public Team Team => _team;

        private HashSet<IDamageable> _alreadyHit = new();

        private void Start()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true;

            if (_lifetime > 0)
                Destroy(gameObject, _lifetime);
        }

        private void OnTriggerEnter(Collider other)
        {
            var hurtBox = other.GetComponent<HurtBox>();
            if (hurtBox == null) return;
            if (hurtBox.Team == _team) return;
            if (_alreadyHit.Contains(hurtBox.Damageable)) return;

            _alreadyHit.Add(hurtBox.Damageable);

            var damage = DamagePayload;
            damage.Target = other.gameObject;
            damage.HitPoint = other.ClosestPoint(transform.position);

            // Apply armor reduction
            var targetHealth = other.GetComponentInParent<HealthComponent>();
            if (targetHealth != null)
            {
                var targetAttacker = other.GetComponentInParent<IAttacker>();
                if (targetAttacker != null)
                {
                    float armor = targetAttacker.Stats.GetStat(StatType.Armor);
                    damage.FinalDamage = DamageCalculator.ApplyArmor(damage.FinalDamage, armor);
                }
            }

            hurtBox.Damageable.TakeDamage(damage);

            // Apply knockback and stun to enemies
            var knockbackable = other.GetComponentInParent<IKnockbackable>();
            if (knockbackable != null)
            {
                if (damage.KnockbackForce > 0f)
                    knockbackable.ApplyKnockback(transform.position, damage.KnockbackForce);

                if (damage.StunDuration > 0f)
                    knockbackable.ApplyStun(damage.StunDuration);
            }
        }

        public void SetTeam(Team team) => _team = team;
        public void ResetHits() => _alreadyHit.Clear();
    }
}
