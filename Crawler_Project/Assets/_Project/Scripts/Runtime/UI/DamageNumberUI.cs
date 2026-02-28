using TMPro;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    public class DamageNumberUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _text;

        [Header("Movement")]
        [SerializeField] private float _floatSpeed = 1.5f;
        [SerializeField] private float _horizontalDrift = 0.3f;
        [SerializeField] private float _lifetime = 1.2f;

        [Header("Scaling")]
        [SerializeField] private AnimationCurve _scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 0f);
        [SerializeField] private float _normalScale = 1f;
        [SerializeField] private float _critScale = 1.5f;
        [SerializeField] private float _healScale = 1f;

        [Header("Colors")]
        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _critColor = new Color(1f, 0.9f, 0f); // Yellow
        [SerializeField] private Color _healColor = new Color(0.2f, 0.9f, 0.2f); // Green

        private float _elapsed;
        private float _driftDirection;
        private float _baseScale;
        private CanvasGroup _canvasGroup;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // Set default scale curve if not set in inspector
            if (_scaleCurve == null || _scaleCurve.length == 0)
            {
                _scaleCurve = new AnimationCurve(
                    new Keyframe(0f, 0f),
                    new Keyframe(0.15f, 1.2f),
                    new Keyframe(0.3f, 1f),
                    new Keyframe(0.8f, 1f),
                    new Keyframe(1f, 0f)
                );
            }
        }

        public void Initialize(DamageInfo damageInfo)
        {
            bool isCrit = damageInfo.IsCritical;
            float damage = damageInfo.FinalDamage;

            if (_text != null)
            {
                _text.text = Mathf.CeilToInt(Mathf.Abs(damage)).ToString();

                if (isCrit)
                {
                    _text.text += "!";
                    _text.color = _critColor;
                    _baseScale = _critScale;
                }
                else
                {
                    _text.color = _normalColor;
                    _baseScale = _normalScale;
                }
            }

            _driftDirection = Random.Range(-1f, 1f);
            transform.localScale = Vector3.zero;
        }

        public void InitializeHeal(float amount)
        {
            if (_text != null)
            {
                _text.text = $"+{Mathf.CeilToInt(amount)}";
                _text.color = _healColor;
            }

            _baseScale = _healScale;
            _driftDirection = Random.Range(-1f, 1f);
            transform.localScale = Vector3.zero;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float normalizedTime = _elapsed / _lifetime;

            if (normalizedTime >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            // Float upward
            transform.position += Vector3.up * (_floatSpeed * Time.deltaTime);

            // Horizontal drift
            transform.position += Vector3.right * (_driftDirection * _horizontalDrift * Time.deltaTime);

            // Scale from curve
            float scaleMultiplier = _scaleCurve.Evaluate(normalizedTime);
            transform.localScale = Vector3.one * (_baseScale * scaleMultiplier);

            // Fade out in final portion
            if (_canvasGroup != null)
            {
                float fadeStart = 0.7f;
                if (normalizedTime > fadeStart)
                {
                    float fadeProgress = (normalizedTime - fadeStart) / (1f - fadeStart);
                    _canvasGroup.alpha = 1f - fadeProgress;
                }
                else
                {
                    _canvasGroup.alpha = 1f;
                }
            }
        }
    }
}
