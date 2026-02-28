using System.Collections.Generic;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    public class CommentaryManager : MonoBehaviour
    {
        [SerializeField] private float _minTimeBetweenLines = Constants.MIN_COMMENTARY_INTERVAL;
        [SerializeField] private List<CommentaryPackData> _combatPacks;
        [SerializeField] private List<CommentaryPackData> _lootPacks;
        [SerializeField] private List<CommentaryPackData> _deathPacks;
        [SerializeField] private List<CommentaryPackData> _levelUpPacks;
        [SerializeField] private List<CommentaryPackData> _idlePacks;

        private CommentaryQueue _queue;
        private float _lastLineTime;
        private HashSet<string> _triggeredIds = new();

        private void Awake()
        {
            _queue = new CommentaryQueue();
            ServiceLocator.Register(this);
        }

        private void OnEnable()
        {
            GameEvents.OnEnemyKilled += HandleKillCommentary;
            GameEvents.OnLootBoxOpened += HandleLootCommentary;
            GameEvents.OnPlayerLevelUp += HandleLevelUpCommentary;
            GameEvents.OnPlayerDeath += HandleDeathCommentary;
            GameEvents.OnFloorEntered += HandleFloorCommentary;
            GameEvents.OnRoomEntered += HandleRoomCommentary;
            GameEvents.OnRoomCleared += HandleRoomClearedCommentary;
            GameEvents.OnCommentaryTriggered += EnqueueDirect;
        }

        private void OnDisable()
        {
            GameEvents.OnEnemyKilled -= HandleKillCommentary;
            GameEvents.OnLootBoxOpened -= HandleLootCommentary;
            GameEvents.OnPlayerLevelUp -= HandleLevelUpCommentary;
            GameEvents.OnPlayerDeath -= HandleDeathCommentary;
            GameEvents.OnFloorEntered -= HandleFloorCommentary;
            GameEvents.OnRoomEntered -= HandleRoomCommentary;
            GameEvents.OnRoomCleared -= HandleRoomClearedCommentary;
            GameEvents.OnCommentaryTriggered -= EnqueueDirect;
        }

        private void Update()
        {
            if (Time.time - _lastLineTime >= _minTimeBetweenLines && _queue.HasPending)
            {
                var entry = _queue.Dequeue();
                DisplayCommentary(entry);
                _lastLineTime = Time.time;
            }
        }

        public void PlayAnnouncement(string text)
        {
            var entry = new CommentaryEntry
            {
                Speaker = "The System",
                Text = text,
                Priority = CommentaryPriority.Announcement,
                Category = CommentaryCategory.Announcement
            };
            DisplayCommentary(entry);
        }

        public void EnqueueDirect(CommentaryEntry entry)
        {
            if (entry == null) return;

            if (!string.IsNullOrEmpty(entry.UniqueId))
            {
                if (_triggeredIds.Contains(entry.UniqueId)) return;
            }

            _queue.Enqueue(entry);
        }

        private void DisplayCommentary(CommentaryEntry entry)
        {
            if (entry == null) return;

            if (!string.IsNullOrEmpty(entry.UniqueId))
                _triggeredIds.Add(entry.UniqueId);

            string displayText = string.IsNullOrEmpty(entry.Speaker)
                ? entry.Text
                : $"[{entry.Speaker}] {entry.Text}";

            GameEvents.OnAIAnnouncementReceived?.Invoke(displayText);
        }

        private void HandleKillCommentary(GameObject enemy)
        {
            if (_combatPacks == null || _combatPacks.Count == 0) return;

            // 30% chance to comment on a kill
            if (Random.value > 0.3f) return;

            var pack = _combatPacks[Random.Range(0, _combatPacks.Count)];
            var entry = pack.GetRandom("Borant");
            if (entry != null)
            {
                entry.Priority = CommentaryPriority.Low;
                entry.Category = CommentaryCategory.CombatReaction;
                _queue.Enqueue(entry);
            }
        }

        private void HandleLootCommentary(LootBoxOpenedData data)
        {
            if (_lootPacks == null || _lootPacks.Count == 0) return;

            var pack = _lootPacks[Random.Range(0, _lootPacks.Count)];
            var entry = pack.GetRandom();
            if (entry != null)
            {
                entry.Priority = CommentaryPriority.Medium;
                entry.Category = CommentaryCategory.LootReaction;
                _queue.Enqueue(entry);
            }
        }

        private void HandleLevelUpCommentary(int level)
        {
            var entry = new CommentaryEntry
            {
                Speaker = "The System",
                Text = $"Crawler has reached level {level}. The audience cheers.",
                Priority = CommentaryPriority.High,
                Category = CommentaryCategory.LevelUpReaction
            };
            _queue.Enqueue(entry);
        }

        private void HandleDeathCommentary(GameObject player)
        {
            var entry = new CommentaryEntry
            {
                Speaker = "Borant",
                Text = "Another one bites the dust. How... expected.",
                Priority = CommentaryPriority.Announcement,
                Category = CommentaryCategory.DeathReaction
            };
            DisplayCommentary(entry);
        }

        private void HandleFloorCommentary(int floorNumber)
        {
            PlayAnnouncement($"Welcome to Floor {floorNumber}. Try not to die immediately.");
        }

        private void HandleRoomCommentary(GameObject roomObj)
        {
            // Occasional room entry commentary
            if (Random.value > 0.2f) return;

            var entry = new CommentaryEntry
            {
                Speaker = "Borant",
                Text = "Oh, this room. This should be fun.",
                Priority = CommentaryPriority.Low,
                Category = CommentaryCategory.RoomReaction
            };
            _queue.Enqueue(entry);
        }

        private void HandleRoomClearedCommentary(GameObject roomObj)
        {
            var entry = new CommentaryEntry
            {
                Speaker = "The System",
                Text = "Room cleared. Loot has been dispensed.",
                Priority = CommentaryPriority.Medium,
                Category = CommentaryCategory.RoomReaction
            };
            _queue.Enqueue(entry);
        }

        public bool HasTriggered(string uniqueId)
        {
            return _triggeredIds.Contains(uniqueId);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<CommentaryManager>();
        }
    }
}
