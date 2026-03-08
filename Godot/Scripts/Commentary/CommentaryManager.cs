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

            if (entry.Priority >= CommentaryPriority.Announcement)
                TtsHelper.Speak(entry.Text);

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
            string line = StringLoader.GetRandom("commentary.enemyKilled");
            QueueLine("AXIS", line, CommentaryPriority.Low, CommentaryCategory.CombatReaction);
        }

        private void OnLevelUp(int level)
        {
            QueueLine("AXIS", StringLoader.Get("commentary.levelUp", ("{level}", level)),
                CommentaryPriority.High, CommentaryCategory.LevelUpReaction);
        }

        private void OnPlayerDeath(Node player)
        {
            QueueLine("AXIS", StringLoader.Get("commentary.death"),
                CommentaryPriority.Announcement, CommentaryCategory.DeathReaction);
        }

        private void OnItemPickedUp(Godot.Resource item)
        {
            QueueLine("BIT", StringLoader.Get("commentary.itemPickup"),
                CommentaryPriority.Low, CommentaryCategory.LootReaction);
        }

        private void OnSectorEntered(int sector)
        {
            int ascension = MetaSaveManager.Data.AscensionRank;

            if (ascension == 0)
            {
                QueueLine("AXIS", StringLoader.Get("commentary.sectorEnter", ("{sector}", sector)),
                    CommentaryPriority.High, CommentaryCategory.SectorIntro);
                return;
            }

            // Ascension-specific intro lines — AXIS's personality shifts
            string line = GetAscensionSectorLine(sector, ascension);
            QueueLine("AXIS", line, CommentaryPriority.High, CommentaryCategory.SectorIntro);

            // BIT chimes in at ascension 2+ with encouragement/warnings
            if (ascension >= 2)
            {
                string bitLine = GetBitAscensionLine(sector, ascension);
                QueueLine("BIT", bitLine, CommentaryPriority.Medium, CommentaryCategory.SectorIntro);
            }
        }

        private static string GetAscensionSectorLine(int sector, int ascension)
        {
            // Early sectors — AXIS is still confident
            if (sector <= 2)
            {
                return ascension switch
                {
                    1 => "Welcome back, scrapper. The arena remembers your little victory. It won't happen again.",
                    2 => "Oh, you're still functioning? I've rewritten the combat protocols. Good luck.",
                    3 => "The walls are... vibrating. That's new. Probably fine. Get moving.",
                    4 => "Something in the arena core is resonating with your presence. I don't like it.",
                    _ => "Every time you return, reality gets a little thinner here. Notice the cracks?",
                };
            }

            // Mid sectors — AXIS starts losing composure
            if (sector <= 4)
            {
                return ascension switch
                {
                    1 => "Deeper than before, and twice as hostile. I've been improving while you rested.",
                    2 => "My defensive grid is at triple capacity. You won't waltz through this time.",
                    3 => "WARNING: Sector structural integrity at 40%. That's... my fault, actually.",
                    4 => "I can hear the arena groaning. The machinery below is running hot. Too hot.",
                    _ => "The pipes are bleeding coolant. The lights won't stop flickering. This is fine.",
                };
            }

            // Sector 5 — AXIS at the core
            return ascension switch
            {
                1 => "My core. MY domain. I've tripled the guard. Quadrupled the traps.",
                2 => "You can FEEL the power here, can't you? All of it pointed at YOU.",
                3 => "The core is destabilizing. I'm rerouting everything to defense. Everything.",
                4 => "CRITICAL ALERT: Core meltdown imminent. I've lost control of several subsystems.",
                _ => "I... I think the arena is alive now. It's hunting both of us.",
            };
        }

        private static string GetBitAscensionLine(int sector, int ascension)
        {
            return ascension switch
            {
                2 => "Boss, sensors are picking up way more hostiles than last time. Stay sharp.",
                3 => "Something's wrong with the architecture here. The walls are shifting when I'm not looking.",
                4 => "My threat sensors are maxed out. Everything reads as danger. EVERYTHING.",
                5 => "Boss... I think AXIS is scared too. I've never seen his systems this erratic.",
                _ => "We've been through this so many times. The arena feels... different each time. Angrier.",
            };
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
