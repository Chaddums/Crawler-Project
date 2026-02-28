using System.Collections.Generic;
using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Service: queues DCC-flavored voice lines from game events.
    /// Respects cooldown between lines.
    /// </summary>
    public partial class CommentaryManager : Node
    {
        private readonly Queue<CommentaryEntry> _queue = new();
        private float _cooldownTimer;
        private bool _isPlaying;

        public override void _Ready()
        {
            ServiceLocator.Register(this);

            GameEvents.OnEnemyKilled += OnEnemyKilled;
            GameEvents.OnPlayerLevelUp += OnLevelUp;
            GameEvents.OnPlayerDeath += OnPlayerDeath;
            GameEvents.OnItemPickedUp += OnItemPickedUp;
            GameEvents.OnFloorEntered += OnFloorEntered;
        }

        public override void _Process(double delta)
        {
            if (_cooldownTimer > 0)
            {
                _cooldownTimer -= (float)delta;
                return;
            }

            if (_queue.Count > 0 && !_isPlaying)
            {
                var entry = _queue.Dequeue();
                PlayCommentary(entry);
            }
        }

        private void PlayCommentary(CommentaryEntry entry)
        {
            _isPlaying = true;
            _cooldownTimer = entry.GetDisplayDuration() + Constants.MIN_COMMENTARY_INTERVAL;

            GameEvents.OnCommentaryTriggered?.Invoke(entry);
            GD.Print($"[Commentary] {entry.Speaker}: \"{entry.Text}\"");

            // Auto-reset after display duration
            GetTree().CreateTimer(entry.GetDisplayDuration()).Timeout += () =>
            {
                _isPlaying = false;
            };
        }

        public void QueueLine(string speaker, string text, CommentaryPriority priority,
            CommentaryCategory category)
        {
            var entry = new CommentaryEntry
            {
                UniqueId = $"commentary_{Time.GetTicksMsec()}",
                Speaker = speaker,
                Text = text,
                Priority = priority,
                Category = category
            };

            // High priority jumps the queue
            if (priority >= CommentaryPriority.High)
            {
                var temp = new Queue<CommentaryEntry>();
                temp.Enqueue(entry);
                while (_queue.Count > 0)
                    temp.Enqueue(_queue.Dequeue());
                while (temp.Count > 0)
                    _queue.Enqueue(temp.Dequeue());
            }
            else
            {
                _queue.Enqueue(entry);
            }
        }

        private void OnEnemyKilled(Node enemy)
        {
            var lines = new[]
            {
                "Another one bites the dust.",
                "That's how it's done in the dungeon.",
                "The crowd goes mild!",
                "Not bad for a crawler.",
                "Keep it up, Carl.",
            };
            string line = lines[GD.Randi() % lines.Length];
            QueueLine("Dungeon", line, CommentaryPriority.Low, CommentaryCategory.CombatReaction);
        }

        private void OnLevelUp(int level)
        {
            QueueLine("System", $"ATTENTION CRAWLERS: Carl has reached level {level}!",
                CommentaryPriority.High, CommentaryCategory.LevelUpReaction);
        }

        private void OnPlayerDeath(Node player)
        {
            QueueLine("Dungeon", "And the crowd goes wild... for all the wrong reasons.",
                CommentaryPriority.Announcement, CommentaryCategory.DeathReaction);
        }

        private void OnItemPickedUp(Godot.Resource item)
        {
            QueueLine("System", "New loot acquired. Try not to die before you can use it.",
                CommentaryPriority.Low, CommentaryCategory.LootReaction);
        }

        private void OnFloorEntered(int floor)
        {
            QueueLine("System", $"Welcome to Floor {floor}. Good luck, crawler.",
                CommentaryPriority.High, CommentaryCategory.FloorIntro);
        }

        public override void _ExitTree()
        {
            GameEvents.OnEnemyKilled -= OnEnemyKilled;
            GameEvents.OnPlayerLevelUp -= OnLevelUp;
            GameEvents.OnPlayerDeath -= OnPlayerDeath;
            GameEvents.OnItemPickedUp -= OnItemPickedUp;
            GameEvents.OnFloorEntered -= OnFloorEntered;
            ServiceLocator.Unregister<CommentaryManager>();
        }
    }
}
