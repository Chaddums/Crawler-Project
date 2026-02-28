using TMPro;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    public class DamageNumber : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _text;
        [SerializeField] private float _floatSpeed = 2f;
        [SerializeField] private float _lifetime = 1f;
        [SerializeField] private float _normalFontSize = 36f;
        [SerializeField] private float _critFontSize = 48f;
        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _critColor = Color.yellow;
        [SerializeField] private Color _healColor = Color.green;
        [SerializeField] private AnimationCurve _scaleCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

        private float _timer;
        private Vector3 _initialScale;

        public void Initialize(DamageInfo info)
        {
            if (_text == null) return;

            int displayValue = Mathf.RoundToInt(info.FinalDamage);
            _text.text = displayValue.ToString();
            _text.color = info.IsCritical ? _critColor : _normalColor;
            _text.fontSize = info.IsCritical ? _critFontSize : _normalFontSize;

            if (info.IsCritical)
                _text.text += "!";

            _initialScale = transform.localScale;
        }

        public void InitializeHeal(float amount)
        {
            if (_text == null) return;

            _text.text = $"+{Mathf.RoundToInt(amount)}";
            _text.color = _healColor;
            _text.fontSize = _normalFontSize;

            _initialScale = transform.localScale;
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            transform.position += Vector3.up * _floatSpeed * Time.deltaTime;

            float t = _timer / _lifetime;
            transform.localScale = _initialScale * _scaleCurve.Evaluate(t);

            if (_text != null)
            {
                var color = _text.color;
                color.a = 1f - t;
                _text.color = color;
            }

            if (_timer >= _lifetime)
                Destroy(gameObject);
        }
    }
}
