using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonCrawlerCarl
{
    public class HealthBarUI : MonoBehaviour
    {
        [SerializeField] private Image _fillImage;
        [SerializeField] private TextMeshProUGUI _healthText;
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("Colors")]
        [SerializeField] private Color _highHealthColor = new Color(0.2f, 0.8f, 0.2f);
        [SerializeField] private Color _midHealthColor = new Color(0.9f, 0.7f, 0.1f);
        [SerializeField] private Color _lowHealthColor = new Color(0.8f, 0.15f, 0.15f);
        [SerializeField] private float _lowHealthThreshold = 0.3f;
        [SerializeField] private float _midHealthThreshold = 0.6f;

        [Header("Animation")]
        [SerializeField] private float _fillLerpSpeed = 8f;

        [Header("Damage Flash")]
        [SerializeField] private float _flashDuration = 0.15f;

        [Header("Low Health Pulse")]
        [SerializeField] private float _pulseSpeed = 4f;

        private HealthComponent _boundHealth;
        private float _targetFill;
        private float _previousFill = 1f;

        // Damage flash state
        private float _flashTimer;
        private Color _currentHealthColor;

        public void Bind(HealthComponent health)
        {
            if (_boundHealth != null)
                _boundHealth.OnHealthChanged -= HandleHealthChanged;

            _boundHealth = health;

            if (_boundHealth != null)
            {
                _boundHealth.OnHealthChanged += HandleHealthChanged;
                SetHealth(_boundHealth.CurrentHealth, _boundHealth.MaxHealth);
                _previousFill = _targetFill;
                Show();
            }
            else
            {
                Hide();
            }
        }

        public void SetHealth(float current, float max)
        {
            if (max <= 0f) return;

            float percent = Mathf.Clamp01(current / max);
            _targetFill = percent;

            if (_healthText != null)
                _healthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";

            UpdateFillColor(percent);

            // Detect health decrease for damage flash
            if (percent < _previousFill)
            {
                _flashTimer = _flashDuration;
            }

            _previousFill = percent;
        }

        private void Update()
        {
            if (_fillImage == null) return;

            _fillImage.fillAmount = Mathf.Lerp(_fillImage.fillAmount, _targetFill, Time.deltaTime * _fillLerpSpeed);

            // Damage flash
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                float flashT = _flashTimer / _flashDuration;
                _fillImage.color = Color.Lerp(_currentHealthColor, Color.white, flashT);
            }
            // Low health pulse
            else if (_targetFill <= _lowHealthThreshold && _targetFill > 0f)
            {
                float pulse = (Mathf.Sin(Time.time * _pulseSpeed) + 1f) * 0.5f; // 0..1
                _fillImage.color = Color.Lerp(_lowHealthColor, Color.white, pulse * 0.3f);
            }
            else
            {
                _fillImage.color = _currentHealthColor;
            }
        }

        private void HandleHealthChanged(float current, float max)
        {
            SetHealth(current, max);
        }

        private void UpdateFillColor(float percent)
        {
            if (percent <= _lowHealthThreshold)
                _currentHealthColor = _lowHealthColor;
            else if (percent <= _midHealthThreshold)
                _currentHealthColor = _midHealthColor;
            else
                _currentHealthColor = _highHealthColor;
        }

        public void Show()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.blocksRaycasts = true;
            }
        }

        public void Hide()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
            }
        }

        private void OnDestroy()
        {
            if (_boundHealth != null)
                _boundHealth.OnHealthChanged -= HandleHealthChanged;
        }
    }
}
