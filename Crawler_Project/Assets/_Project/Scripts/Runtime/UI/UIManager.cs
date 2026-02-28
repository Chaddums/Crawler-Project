using System.Collections.Generic;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Marker interface for panels that should close other exclusive panels when opened.
    /// </summary>
    public interface IExclusivePanel { }

    /// <summary>
    /// Central panel coordinator. Tracks open panels, enforces exclusive-panel rules,
    /// and blocks player input while any exclusive panel is open.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [SerializeField] private CanvasGroup _screenDimmer;

        private readonly List<UIPanel> _openPanels = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (_screenDimmer != null)
            {
                _screenDimmer.alpha = 0f;
                _screenDimmer.blocksRaycasts = false;
            }
        }

        /// <summary>Called by UIPanel.Show() after a panel begins opening.</summary>
        public void OnPanelOpened(UIPanel panel)
        {
            // If this is an exclusive panel, close other exclusive panels
            if (panel is IExclusivePanel)
            {
                for (int i = _openPanels.Count - 1; i >= 0; i--)
                {
                    var other = _openPanels[i];
                    if (other != panel && other is IExclusivePanel && other.IsOpen)
                    {
                        other.Hide();
                    }
                }
            }

            if (!_openPanels.Contains(panel))
                _openPanels.Add(panel);

            UpdateInputBlocking();
            UpdateDimmer();
        }

        /// <summary>Called by UIPanel after hide animation completes.</summary>
        public void OnPanelClosed(UIPanel panel)
        {
            _openPanels.Remove(panel);
            UpdateInputBlocking();
            UpdateDimmer();
        }

        private void UpdateInputBlocking()
        {
            bool anyExclusiveOpen = false;
            foreach (var p in _openPanels)
            {
                if (p is IExclusivePanel && p.IsOpen)
                {
                    anyExclusiveOpen = true;
                    break;
                }
            }

            if (ServiceLocator.TryGet<PlayerController>(out var player))
            {
                var inputHandler = player.GetComponent<PlayerInputHandler>();
                if (inputHandler != null)
                {
                    if (anyExclusiveOpen)
                        inputHandler.DisableInput();
                    else
                        inputHandler.EnableInput();
                }
            }
        }

        private void UpdateDimmer()
        {
            if (_screenDimmer == null) return;

            bool anyExclusiveOpen = false;
            foreach (var p in _openPanels)
            {
                if (p is IExclusivePanel && p.IsOpen)
                {
                    anyExclusiveOpen = true;
                    break;
                }
            }

            _screenDimmer.alpha = anyExclusiveOpen ? 0.4f : 0f;
            _screenDimmer.blocksRaycasts = false; // Never block raycasts — panels handle that
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
