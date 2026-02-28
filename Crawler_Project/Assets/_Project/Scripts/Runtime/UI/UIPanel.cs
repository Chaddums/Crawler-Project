using System.Collections;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Abstract base class for all UI panels. Provides animated Show/Hide with
    /// fade + scale, virtual hooks, and state tracking.
    /// </summary>
    public abstract class UIPanel : MonoBehaviour
    {
        [Header("UIPanel Animation")]
        [SerializeField] private float _showDuration = 0.2f;
        [SerializeField] private float _hideDuration = 0.15f;
        [SerializeField] private float _scaleFrom = 0.92f;

        private CanvasGroup _panelCanvasGroup;
        private RectTransform _panelRect;
        private Coroutine _animCoroutine;

        public bool IsOpen { get; private set; }

        protected virtual void Awake()
        {
            _panelCanvasGroup = GetComponent<CanvasGroup>();
            _panelRect = GetComponent<RectTransform>();
        }

        /// <summary>Show with fade+scale animation.</summary>
        public void Show()
        {
            if (IsOpen) return;
            IsOpen = true;

            OnBeforeShow();
            UIManager.Instance?.OnPanelOpened(this);
            UISfx.Instance?.PlayPanelOpen();

            if (_animCoroutine != null) StopCoroutine(_animCoroutine);
            _animCoroutine = StartCoroutine(AnimateShow());
        }

        /// <summary>Hide with fade animation.</summary>
        public void Hide()
        {
            if (!IsOpen) return;
            IsOpen = false;

            OnBeforeHide();
            UISfx.Instance?.PlayPanelClose();

            if (_animCoroutine != null) StopCoroutine(_animCoroutine);
            _animCoroutine = StartCoroutine(AnimateHide());
        }

        /// <summary>Instant visibility (no animation). Use in Start/Awake.</summary>
        public void SetVisibleImmediate(bool visible)
        {
            IsOpen = visible;
            if (_panelCanvasGroup == null)
                _panelCanvasGroup = GetComponent<CanvasGroup>();
            if (_panelRect == null)
                _panelRect = GetComponent<RectTransform>();

            SetCanvasGroupState(visible ? 1f : 0f, visible);

            if (_panelRect != null)
                _panelRect.localScale = Vector3.one;
        }

        // --- Virtual hooks for subclasses ---
        protected virtual void OnBeforeShow() { }
        protected virtual void OnAfterShow() { }
        protected virtual void OnBeforeHide() { }
        protected virtual void OnAfterHide() { }

        // --- Animation coroutines ---

        private IEnumerator AnimateShow()
        {
            SetCanvasGroupState(0f, true);

            float elapsed = 0f;
            while (elapsed < _showDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _showDuration);
                float eased = EaseOutCubic(t);

                if (_panelCanvasGroup != null)
                    _panelCanvasGroup.alpha = eased;

                if (_panelRect != null)
                {
                    float scale = Mathf.Lerp(_scaleFrom, 1f, eased);
                    _panelRect.localScale = new Vector3(scale, scale, 1f);
                }

                yield return null;
            }

            SetCanvasGroupState(1f, true);
            if (_panelRect != null)
                _panelRect.localScale = Vector3.one;

            _animCoroutine = null;
            OnAfterShow();
        }

        private IEnumerator AnimateHide()
        {
            float elapsed = 0f;
            while (elapsed < _hideDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _hideDuration);

                if (_panelCanvasGroup != null)
                    _panelCanvasGroup.alpha = 1f - t;

                yield return null;
            }

            SetCanvasGroupState(0f, false);
            if (_panelRect != null)
                _panelRect.localScale = Vector3.one;

            _animCoroutine = null;
            OnAfterHide();
            UIManager.Instance?.OnPanelClosed(this);
        }

        private void SetCanvasGroupState(float alpha, bool active)
        {
            if (_panelCanvasGroup == null) return;
            _panelCanvasGroup.alpha = alpha;
            _panelCanvasGroup.blocksRaycasts = active;
            _panelCanvasGroup.interactable = active;
        }

        private static float EaseOutCubic(float t)
        {
            t -= 1f;
            return t * t * t + 1f;
        }
    }
}
