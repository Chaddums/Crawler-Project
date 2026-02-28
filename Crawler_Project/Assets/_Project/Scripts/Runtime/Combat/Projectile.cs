using UnityEngine;

namespace DungeonCrawlerCarl
{
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private float _speed = 15f;
        [SerializeField] private float _lifetime = 5f;
        [SerializeField] private GameObject _impactEffectPrefab;
        [SerializeField] private bool _destroyOnHit = true;

        private HitBox _hitBox;
        private Rigidbody _rb;
        private bool _initialized;

        private void Awake()
        {
            _hitBox = GetComponent<HitBox>();
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.isKinematic = true;
        }

        public void Initialize(DamageInfo damage, Team team)
        {
            if (_hitBox != null)
            {
                _hitBox.SetTeam(team);
                _hitBox.DamagePayload = damage;
            }

            Destroy(gameObject, _lifetime);
            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized) return;
            transform.Translate(Vector3.forward * _speed * Time.deltaTime);
        }

        private void OnTriggerEnter(Collider other)
        {
            // Let the HitBox handle damage; we just handle impact effects
            var hurtBox = other.GetComponent<HurtBox>();
            if (hurtBox == null) return;
            if (_hitBox != null && hurtBox.Team == _hitBox.Team) return;

            SpawnImpactEffect(other.ClosestPoint(transform.position));

            if (_destroyOnHit)
                Destroy(gameObject);
        }

        private void SpawnImpactEffect(Vector3 position)
        {
            if (_impactEffectPrefab != null)
            {
                var impact = Instantiate(_impactEffectPrefab, position, Quaternion.identity);
                Destroy(impact, 2f);
            }
        }
    }
}
