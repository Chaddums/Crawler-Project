using UnityEngine;

namespace DungeonCrawlerCarl
{
    public class PlayerAnimator : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private SpriteRenderer _spriteRenderer;

        private static readonly int AnimIsMoving = Animator.StringToHash("IsMoving");
        private static readonly int AnimIsAttacking = Animator.StringToHash("IsAttacking");
        private static readonly int AnimIsDead = Animator.StringToHash("IsDead");
        private static readonly int AnimHit = Animator.StringToHash("Hit");
        private static readonly int AnimAttack = Animator.StringToHash("Attack");

        private PlayerMovement _movement;
        private HealthComponent _health;
        private MaterialPropertyBlock _propBlock;

        private void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            _health = GetComponent<HealthComponent>();
            _propBlock = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            if (_health != null)
            {
                _health.OnDamaged += OnDamaged;
                _health.OnDeath += OnDeath;
            }
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.OnDamaged -= OnDamaged;
                _health.OnDeath -= OnDeath;
            }
        }

        private void Update()
        {
            if (_animator == null) return;
            _animator.SetBool(AnimIsMoving, _movement != null && _movement.IsMoving);
        }

        public void PlayAttackAnimation()
        {
            if (_animator != null)
                _animator.SetTrigger(AnimAttack);
        }

        private void OnDamaged(DamageInfo info)
        {
            if (_animator != null)
                _animator.SetTrigger(AnimHit);

            // Flash red
            if (_spriteRenderer != null)
                StartCoroutine(FlashRoutine());
        }

        private void OnDeath()
        {
            if (_animator != null)
                _animator.SetBool(AnimIsDead, true);
        }

        private System.Collections.IEnumerator FlashRoutine()
        {
            if (_spriteRenderer == null) yield break;

            Color original = _spriteRenderer.color;
            _spriteRenderer.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            _spriteRenderer.color = original;
        }
    }
}
