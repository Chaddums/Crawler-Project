using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DungeonCrawlerCarl
{
    public class PauseMenuUI : UIPanel, IExclusivePanel
    {
        [Header("Buttons")]
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _mainMenuButton;
        [SerializeField] private Button _quitButton;

        [Header("Settings Panel")]
        [SerializeField] private GameObject _settingsPanel;

        private bool _isPaused;
        private float _previousTimeScale;

        protected override void Awake()
        {
            base.Awake();
            SetVisibleImmediate(false);
        }

        private void OnEnable()
        {
            GameEvents.OnPauseToggled += HandlePauseToggled;

            if (_resumeButton != null)
                _resumeButton.onClick.AddListener(Resume);

            if (_settingsButton != null)
                _settingsButton.onClick.AddListener(ToggleSettings);

            if (_mainMenuButton != null)
                _mainMenuButton.onClick.AddListener(RequestReturnToMainMenu);

            if (_quitButton != null)
                _quitButton.onClick.AddListener(RequestQuitGame);
        }

        private void OnDisable()
        {
            GameEvents.OnPauseToggled -= HandlePauseToggled;

            if (_resumeButton != null)
                _resumeButton.onClick.RemoveListener(Resume);

            if (_settingsButton != null)
                _settingsButton.onClick.RemoveListener(ToggleSettings);

            if (_mainMenuButton != null)
                _mainMenuButton.onClick.RemoveListener(RequestReturnToMainMenu);

            if (_quitButton != null)
                _quitButton.onClick.RemoveListener(RequestQuitGame);

            // Ensure time is restored if this object is disabled while paused
            if (_isPaused)
            {
                Time.timeScale = _previousTimeScale;
                _isPaused = false;
            }
        }

        private void Start()
        {
            SetupButtonFeedback(_resumeButton);
            SetupButtonFeedback(_settingsButton);
            SetupButtonFeedback(_mainMenuButton);
            SetupButtonFeedback(_quitButton);
        }

        private void HandlePauseToggled()
        {
            if (_isPaused)
                Resume();
            else
                Pause();
        }

        public void Pause()
        {
            _isPaused = true;
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            Show();

            if (GameManager.Instance != null)
                GameManager.Instance.ChangeState(GameState.Paused);

            // Hide settings sub-panel when opening pause menu
            if (_settingsPanel != null)
                _settingsPanel.SetActive(false);
        }

        public void Resume()
        {
            _isPaused = false;
            Time.timeScale = _previousTimeScale;
            Hide();

            if (GameManager.Instance != null)
                GameManager.Instance.ChangeState(GameState.InFloor);
        }

        private void ToggleSettings()
        {
            UISfx.Instance?.PlayClick();
            if (_settingsPanel != null)
                _settingsPanel.SetActive(!_settingsPanel.activeSelf);
        }

        private void RequestReturnToMainMenu()
        {
            UISfx.Instance?.PlayClick();
            if (ConfirmDialogUI.Instance != null)
            {
                ConfirmDialogUI.Instance.Prompt(
                    "Return to the main menu? Unsaved progress will be lost.",
                    ReturnToMainMenu,
                    "Return", "Cancel");
            }
            else
            {
                ReturnToMainMenu();
            }
        }

        private void ReturnToMainMenu()
        {
            _isPaused = false;
            Time.timeScale = 1f;

            if (GameManager.Instance != null)
                GameManager.Instance.ReturnToMainMenu();
        }

        private void RequestQuitGame()
        {
            UISfx.Instance?.PlayClick();
            if (ConfirmDialogUI.Instance != null)
            {
                ConfirmDialogUI.Instance.Prompt(
                    "Quit the game? Unsaved progress will be lost.",
                    QuitGame,
                    "Quit", "Cancel");
            }
            else
            {
                QuitGame();
            }
        }

        private void QuitGame()
        {
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

            var clickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            clickEntry.callback.AddListener(_ =>
            {
                UISfx.Instance?.PlayClick();
            });
            trigger.triggers.Add(clickEntry);
        }
    }
}
