using UnityEngine;

namespace DungeonCrawlerCarl
{
    public class CombatFeedback : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private float _flashDuration = 0.1f;
        [SerializeField] private Color _damageFlashColor = Color.red;
        [SerializeField] private float _hitStopDuration = 0.05f;

        private HealthComponent _health;
        private Color _originalColor;
        private Coroutine _flashCoroutine;

        private void Awake()
        {
            _health = GetComponent<HealthComponent>();
            if (_spriteRenderer != null)
                _originalColor = _spriteRenderer.color;
        }

        private void OnEnable()
        {
            if (_health != null)
                _health.OnDamaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (_health != null)
                _health.OnDamaged -= OnDamaged;
        }

        private void OnDamaged(DamageInfo info)
        {
            if (_spriteRenderer != null)
            {
                if (_flashCoroutine != null)
                    StopCoroutine(_flashCoroutine);
                _flashCoroutine = StartCoroutine(FlashRoutine());
            }
        }

        private System.Collections.IEnumerator FlashRoutine()
        {
            _spriteRenderer.color = _damageFlashColor;
            yield return new WaitForSeconds(_flashDuration);
            _spriteRenderer.color = _originalColor;
            _flashCoroutine = null;
        }
    }
}
