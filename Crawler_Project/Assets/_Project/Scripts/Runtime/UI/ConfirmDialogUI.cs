using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Reusable modal confirmation dialog. Singleton, extends UIPanel.
    /// </summary>
    public class ConfirmDialogUI : UIPanel
    {
        public static ConfirmDialogUI Instance { get; private set; }

        [Header("Dialog Elements")]
        [SerializeField] private TextMeshProUGUI _messageText;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private TextMeshProUGUI _confirmButtonText;
        [SerializeField] private TextMeshProUGUI _cancelButtonText;

        private Action _onConfirm;

        protected override void Awake()
        {
            base.Awake();

            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            SetVisibleImmediate(false);

            if (_confirmButton != null)
                _confirmButton.onClick.AddListener(HandleConfirm);

            if (_cancelButton != null)
                _cancelButton.onClick.AddListener(HandleCancel);
        }

        /// <summary>
        /// Show a confirmation dialog.
        /// </summary>
        public void Prompt(string message, Action onConfirm, string confirmText = "Confirm", string cancelText = "Cancel")
        {
            _onConfirm = onConfirm;

            if (_messageText != null)
                _messageText.text = message;

            if (_confirmButtonText != null)
                _confirmButtonText.text = confirmText;

            if (_cancelButtonText != null)
                _cancelButtonText.text = cancelText;

            Show();
        }

        private void HandleConfirm()
        {
            UISfx.Instance?.PlayClick();
            var callback = _onConfirm;
            _onConfirm = null;
            Hide();
            callback?.Invoke();
        }

        private void HandleCancel()
        {
            UISfx.Instance?.PlayBack();
            _onConfirm = null;
            Hide();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
