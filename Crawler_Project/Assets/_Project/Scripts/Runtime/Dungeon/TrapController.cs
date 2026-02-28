using UnityEngine;

namespace DungeonCrawlerCarl
{
    public class TrapController : MonoBehaviour
    {
        [Header("Damage")]
        [SerializeField] private float _damage = 20f;
        [SerializeField] private DamageType _damageType = DamageType.Physical;

        [Header("Timing")]
        [SerializeField] private float _cooldown = 2f;

        [Header("State")]
        [SerializeField] private bool _isActive = true;

        private float _lastTriggerTime = Mathf.NegativeInfinity;

        /// <summary>
        /// Enable this trap so it can deal damage.
        /// </summary>
        public void Activate()
        {
            _isActive = true;
        }

        /// <summary>
        /// Disable this trap so it no longer deals damage.
        /// </summary>
        public void Deactivate()
        {
            _isActive = false;
        }

        public bool IsActive => _isActive;

        private void OnTriggerEnter(Collider other)
        {
            if (!_isActive) return;

            if (Time.time - _lastTriggerTime < _cooldown) return;

            var damageable = other.GetComponent<IDamageable>();
            if (damageable == null || !damageable.IsAlive) return;

            _lastTriggerTime = Time.time;

            var damageInfo = new DamageInfo
            {
                RawDamage = _damage,
                FinalDamage = _damage,
                IsCritical = false,
                DamageType = _damageType,
                Attacker = gameObject,
                Target = other.gameObject,
                HitPoint = other.ClosestPoint(transform.position)
            };

            damageable.TakeDamage(damageInfo);

            Debug.Log($"[TrapController] {gameObject.name} dealt {_damage} {_damageType} damage to {other.gameObject.name}.");
        }
    }
}
