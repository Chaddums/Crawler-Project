using UnityEngine;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Donut-specific behavior for "Princess Donut the Queen Anne Chonk".
    /// Handles Donut's unique abilities (Primal Scream, Royal Decree) and
    /// her reactions to game events via the commentary system.
    /// </summary>
    [RequireComponent(typeof(CompanionController))]
    public class DonutBehavior : MonoBehaviour
    {
        [Header("Donut's Abilities")]
        [SerializeField] private AbilityData _primalScream;
        [SerializeField] private AbilityData _royalDecree;

        [Header("Commentary Settings")]
        [SerializeField] private float _commentaryCooldown = 5f;

        [Header("Loot Reaction Lines")]
        [SerializeField] [TextArea(1, 2)]
        private string[] _lootBoxReactions = new string[]
        {
            "Ooh, what did we get? Is it something befitting a princess?",
            "If that box doesn't have a tiara in it, I'm going to be very disappointed.",
            "I deserve the best loot. Obviously.",
            "A princess should always get first pick!"
        };

        private CompanionController _controller;
        private float _commentaryTimer;

        private void Awake()
        {
            _controller = GetComponent<CompanionController>();
        }

        private void Start()
        {
            // Ensure Donut's signature abilities are equipped if references are assigned
            if (_primalScream != null)
            {
                _controller.Abilities.AddAbility(_primalScream);
            }
            if (_royalDecree != null)
            {
                _controller.Abilities.AddAbility(_royalDecree);
            }
        }

        private void OnEnable()
        {
            GameEvents.OnLootBoxOpened += HandleLootBoxOpened;
            GameEvents.OnEnemyKilled += HandleEnemyKilled;
            GameEvents.OnPlayerLevelUp += HandlePlayerLevelUp;
        }

        private void OnDisable()
        {
            GameEvents.OnLootBoxOpened -= HandleLootBoxOpened;
            GameEvents.OnEnemyKilled -= HandleEnemyKilled;
            GameEvents.OnPlayerLevelUp -= HandlePlayerLevelUp;
        }

        private void Update()
        {
            if (_commentaryTimer > 0f)
            {
                _commentaryTimer -= Time.deltaTime;
            }
        }

        private void HandleLootBoxOpened(LootBoxOpenedData lootData)
        {
            if (!CanSpeak()) return;

            string line = _lootBoxReactions[Random.Range(0, _lootBoxReactions.Length)];

            // Legendary loot gets a special reaction
            if (lootData.Tier == LootBoxTier.Legendary || lootData.Tier == LootBoxTier.Diamond)
            {
                line = "Now THAT is loot worthy of a princess! Give it to me!";
            }

            FireCommentary(line, CommentaryCategory.LootReaction, CommentaryPriority.Medium);
        }

        private void HandleEnemyKilled(GameObject enemy)
        {
            if (!CanSpeak()) return;

            // Only react sometimes to avoid spam
            if (Random.value > 0.3f) return;

            string[] combatLines = new string[]
            {
                "Another one bites the dust! You're welcome.",
                "That was all me. Obviously.",
                "Fear the power of Princess Donut!"
            };

            string line = combatLines[Random.Range(0, combatLines.Length)];
            FireCommentary(line, CommentaryCategory.CombatReaction, CommentaryPriority.Low);
        }

        private void HandlePlayerLevelUp(int newLevel)
        {
            if (!CanSpeak()) return;

            string line = $"Level {newLevel}? I've been that level for ages. Try to keep up.";
            FireCommentary(line, CommentaryCategory.LevelUpReaction, CommentaryPriority.Medium);
        }

        private bool CanSpeak()
        {
            if (!_controller.Health.IsAlive) return false;
            if (_commentaryTimer > 0f) return false;
            return true;
        }

        private void FireCommentary(string line, CommentaryCategory category, CommentaryPriority priority)
        {
            _commentaryTimer = _commentaryCooldown;

            var entry = new CommentaryEntry
            {
                Speaker = "Donut",
                Text = line,
                Category = category,
                Priority = priority
            };

            GameEvents.OnCommentaryTriggered?.Invoke(entry);
        }
    }
}
