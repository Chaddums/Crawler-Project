using UnityEngine;

namespace DungeonCrawlerCarl
{
    public class HUDController : MonoBehaviour
    {
        [Header("Health Bars")]
        [SerializeField] private HealthBarUI _playerHealthBar;
        [SerializeField] private HealthBarUI _companionHealthBar;

        [Header("Ability Bar")]
        [SerializeField] private AbilityBarUI _abilityBar;

        [Header("Commentary Display")]
        [SerializeField] private DialogueUI _commentaryDisplay;

        private PlayerController _player;
        private CompanionController _companion;

        private void OnEnable()
        {
            GameEvents.OnCompanionSummoned += HandleCompanionSummoned;
            GameEvents.OnCommentaryTriggered += HandleCommentaryTriggered;
            GameEvents.OnAIAnnouncementReceived += HandleAnnouncementReceived;
            GameEvents.OnPlayerLevelUp += HandlePlayerLevelUp;
            GameEvents.OnGameStateChanged += HandleGameStateChanged;
        }

        private void OnDisable()
        {
            GameEvents.OnCompanionSummoned -= HandleCompanionSummoned;
            GameEvents.OnCommentaryTriggered -= HandleCommentaryTriggered;
            GameEvents.OnAIAnnouncementReceived -= HandleAnnouncementReceived;
            GameEvents.OnPlayerLevelUp -= HandlePlayerLevelUp;
            GameEvents.OnGameStateChanged -= HandleGameStateChanged;
        }

        private void Start()
        {
            BindPlayer();
            BindCompanion();
        }

        private void BindPlayer()
        {
            if (!ServiceLocator.TryGet(out _player)) return;

            if (_playerHealthBar != null)
                _playerHealthBar.Bind(_player.Health);

            if (_abilityBar != null)
                _abilityBar.Initialize(_player.Combat.AbilitySlots);
        }

        private void BindCompanion()
        {
            if (!ServiceLocator.TryGet(out _companion))
            {
                if (_companionHealthBar != null)
                    _companionHealthBar.Hide();
                return;
            }

            if (_companionHealthBar != null)
                _companionHealthBar.Bind(_companion.Health);
        }

        private void HandleCompanionSummoned(GameObject companionObj)
        {
            _companion = companionObj != null ? companionObj.GetComponent<CompanionController>() : null;

            if (_companion != null && _companionHealthBar != null)
                _companionHealthBar.Bind(_companion.Health);
        }

        private void HandleCommentaryTriggered(CommentaryEntry entry)
        {
            if (_commentaryDisplay != null)
                _commentaryDisplay.ShowCommentary(entry);
        }

        private void HandleAnnouncementReceived(string announcement)
        {
            if (_commentaryDisplay != null)
                _commentaryDisplay.ShowAnnouncement(announcement);
        }

        private void HandlePlayerLevelUp(int newLevel)
        {
            if (_commentaryDisplay != null)
                _commentaryDisplay.ShowMessage("LEVEL UP", $"You reached level {newLevel}!");
        }

        private void HandleGameStateChanged(GameState newState)
        {
            // Hide HUD during menus and loading
            bool showHUD = newState == GameState.InFloor;
            gameObject.SetActive(showHUD);
        }
    }
}
