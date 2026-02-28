using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Service: queues AXIS/BIT-flavored voice lines from game events.
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
            GameEvents.OnSectorEntered += OnSectorEntered;
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
                "Target neutralized. AXIS is mildly disappointed it wasn't harder.",
                "Scrap collected. You're basically a recycling bin with legs.",
                "BIT: Nice shot! ...for a junkbot.",
                "AXIS: That unit cost me 3 credits to deploy. I want a refund.",
                "BIT: Enemy down. Don't let it go to your processors.",
            };
            string line = lines[GD.Randi() % lines.Length];
            QueueLine("AXIS", line, CommentaryPriority.Low, CommentaryCategory.CombatReaction);
        }

        private void OnLevelUp(int level)
        {
            QueueLine("AXIS", $"ATTENTION SCRAPPERS: Unit has reached level {level}. Threat assessment: still negligible.",
                CommentaryPriority.High, CommentaryCategory.LevelUpReaction);
        }

        private void OnPlayerDeath(Node player)
        {
            QueueLine("AXIS", "UNIT OFFLINE. Viewer ratings just spiked. Your sacrifice is appreciated, scrapper.",
                CommentaryPriority.Announcement, CommentaryCategory.DeathReaction);
        }

        private void OnItemPickedUp(Godot.Resource item)
        {
            QueueLine("BIT", "New component acquired. Try not to explode before installing it.",
                CommentaryPriority.Low, CommentaryCategory.LootReaction);
        }

        private void OnSectorEntered(int sector)
        {
            QueueLine("AXIS", $"Welcome to Sector {sector}. Systems online, scrapper. Try to last longer this time.",
                CommentaryPriority.High, CommentaryCategory.SectorIntro);
        }

        public override void _ExitTree()
        {
            GameEvents.OnEnemyKilled -= OnEnemyKilled;
            GameEvents.OnPlayerLevelUp -= OnLevelUp;
            GameEvents.OnPlayerDeath -= OnPlayerDeath;
            GameEvents.OnItemPickedUp -= OnItemPickedUp;
            GameEvents.OnSectorEntered -= OnSectorEntered;
            ServiceLocator.Unregister<CommentaryManager>();
        }
    }
}
