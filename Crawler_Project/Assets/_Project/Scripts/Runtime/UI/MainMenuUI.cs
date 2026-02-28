using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DungeonCrawlerCarl
{
    public class MainMenuUI : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button _newGameButton;
        [SerializeField] private Button _continueButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _quitButton;

        [Header("Settings Panel")]
        [SerializeField] private GameObject _settingsPanel;

        private void Start()
        {
            if (_newGameButton != null)
                _newGameButton.onClick.AddListener(HandleNewGame);

            if (_continueButton != null)
                _continueButton.onClick.AddListener(HandleContinue);

            if (_settingsButton != null)
                _settingsButton.onClick.AddListener(HandleSettings);

            if (_quitButton != null)
                _quitButton.onClick.AddListener(HandleQuit);

            RefreshContinueButton();

            // Ensure settings panel starts hidden
            if (_settingsPanel != null)
                _settingsPanel.SetActive(false);

            // Setup hover/click feedback on all buttons
            SetupButtonFeedback(_newGameButton);
            SetupButtonFeedback(_continueButton);
            SetupButtonFeedback(_settingsButton);
            SetupButtonFeedback(_quitButton);
        }

        private void RefreshContinueButton()
        {
            if (_continueButton == null) return;

            bool hasSave = false;

            if (ServiceLocator.TryGet<SaveSystem>(out var saveSystem))
                hasSave = saveSystem.HasSave();

            _continueButton.interactable = hasSave;
        }

        private void HandleNewGame()
        {
            UISfx.Instance?.PlayClick();
            if (GameManager.Instance != null)
                GameManager.Instance.StartNewGame();
        }

        private void HandleContinue()
        {
            UISfx.Instance?.PlayClick();
            if (GameManager.Instance != null)
                GameManager.Instance.ContinueGame();
        }

        private void HandleSettings()
        {
            UISfx.Instance?.PlayClick();
            if (_settingsPanel != null)
                _settingsPanel.SetActive(!_settingsPanel.activeSelf);
        }

        private void HandleQuit()
        {
            UISfx.Instance?.PlayClick();
            if (GameManager.Instance != null)
                GameManager.Instance.QuitGame();
        }

        private void SetupButtonFeedback(Button button)
        {
            if (button == null) return;

            var trigger = button.gameObject.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = button.gameObject.AddComponent<EventTrigger>();

            var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener(_ =>
            {
                button.transform.localScale = Vector3.one * 1.05f;
                UISfx.Instance?.PlayHover();
            });
            trigger.triggers.Add(enterEntry);

            var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exitEntry.callback.AddListener(_ =>
            {
                button.transform.localScale = Vector3.one;
            });
            trigger.triggers.Add(exitEntry);
        }
    }
}
