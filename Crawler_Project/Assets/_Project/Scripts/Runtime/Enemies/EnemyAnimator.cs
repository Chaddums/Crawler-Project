using System.Collections;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    public class EnemyAnimator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator _animator;
        [SerializeField] private SpriteRenderer _spriteRenderer;

        [Header("Damage Flash")]
        [SerializeField] private Color _flashColor = Color.red;
        [SerializeField] private float _flashDuration = 0.1f;

        private static readonly int AnimIsMoving = Animator.StringToHash("IsMoving");
        private static readonly int AnimAttack = Animator.StringToHash("Attack");
        private static readonly int AnimHit = Animator.StringToHash("Hit");
        private static readonly int AnimIsDead = Animator.StringToHash("IsDead");

        private HealthComponent _health;
        private Coroutine _flashCoroutine;

        private void Awake()
        {
            _health = GetComponent<HealthComponent>();

            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();

            if (_spriteRenderer == null)
                _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
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

        public void SetMoving(bool isMoving)
        {
            if (_animator != null)
                _animator.SetBool(AnimIsMoving, isMoving);
        }

        public void TriggerAttack()
        {
            if (_animator != null)
                _animator.SetTrigger(AnimAttack);
        }

        private void OnDamaged(DamageInfo info)
        {
            if (_animator != null)
                _animator.SetTrigger(AnimHit);

            FlashSprite();
        }

        private void OnDeath()
        {
            if (_animator != null)
                _animator.SetBool(AnimIsDead, true);
        }

        public void FlashSprite()
        {
            if (_spriteRenderer == null) return;

            if (_flashCoroutine != null)
                StopCoroutine(_flashCoroutine);

            _flashCoroutine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            if (_spriteRenderer == null) yield break;

            Color original = _spriteRenderer.color;
            _spriteRenderer.color = _flashColor;
            yield return new WaitForSeconds(_flashDuration);
            _spriteRenderer.color = original;
            _flashCoroutine = null;
        }
    }
}
