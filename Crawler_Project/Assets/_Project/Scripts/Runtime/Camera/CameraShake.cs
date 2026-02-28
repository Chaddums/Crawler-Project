using System.Collections;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    public class CameraShake : MonoBehaviour
    {
        [SerializeField] private float _defaultDuration = 0.15f;
        [SerializeField] private float _defaultMagnitude = 0.3f;

        private Vector3 _originalPosition;
        private Coroutine _shakeCoroutine;

        private void Awake()
        {
            ServiceLocator.Register(this);
        }

        private void OnEnable()
        {
            GameEvents.OnDamageDealt += OnDamageDealt;
        }

        private void OnDisable()
        {
            GameEvents.OnDamageDealt -= OnDamageDealt;
        }

        private void OnDamageDealt(DamageInfo info)
        {
            // Only shake for significant damage or crits on the player
            if (info.Target == null) return;

            bool isPlayerHit = info.Target.CompareTag(Constants.TAG_PLAYER);
            if (isPlayerHit || info.IsCritical)
            {
                float magnitude = info.IsCritical ? _defaultMagnitude * 1.5f : _defaultMagnitude;
                Shake(_defaultDuration, magnitude);
            }
        }

        public void Shake(float duration = -1f, float magnitude = -1f)
        {
            if (duration < 0) duration = _defaultDuration;
            if (magnitude < 0) magnitude = _defaultMagnitude;

            if (_shakeCoroutine != null)
                StopCoroutine(_shakeCoroutine);

            _shakeCoroutine = StartCoroutine(ShakeRoutine(duration, magnitude));
        }

        private IEnumerator ShakeRoutine(float duration, float magnitude)
        {
            _originalPosition = transform.localPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float x = Random.Range(-1f, 1f) * magnitude;
                float y = Random.Range(-1f, 1f) * magnitude;

                transform.localPosition = _originalPosition + new Vector3(x, y, 0);

                elapsed += Time.deltaTime;
                magnitude *= 0.9f; // Decay

                yield return null;
            }

            transform.localPosition = _originalPosition;
            _shakeCoroutine = null;
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<CameraShake>();
        }
    }
}
