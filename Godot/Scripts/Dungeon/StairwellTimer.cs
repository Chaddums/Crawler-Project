using System.Collections.Generic;
using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Per-floor countdown timer. Creates urgency and DCC-style floor collapse on expiry.
    /// Pauses in safe rooms. Fires warning events at 60s, 30s, 10s thresholds.
    /// </summary>
    public partial class StairwellTimer : Node
    {
        public float TimeRemaining { get; private set; }
        public float TimeLimit { get; private set; }
        public bool IsRunning { get; private set; }

        private bool _warnedAt60;
        private bool _warnedAt30;
        private bool _warnedAt10;
        private bool _expired;

        public override void _Ready()
        {
            ServiceLocator.Register(this);

            GameEvents.OnFloorEntered += OnFloorEntered;
            GameEvents.OnGameStateChanged += OnGameStateChanged;

            GD.Print("[StairwellTimer] Ready");
        }

        private void OnFloorEntered(int floor)
        {
            var floorData = FloorDataRegistry.GetFloor(floor);
            TimeLimit = floorData.TimeLimit;
            TimeRemaining = TimeLimit;

            _warnedAt60 = false;
            _warnedAt30 = false;
            _warnedAt10 = false;
            _expired = false;
            IsRunning = true;

            GD.Print($"[StairwellTimer] Floor {floor} timer started: {TimeLimit}s");
        }

        private void OnGameStateChanged(GameState state)
        {
            // Pause in safe rooms — DCC rule
            IsRunning = state == GameState.InFloor;
        }

        public override void _Process(double delta)
        {
            if (!IsRunning || _expired) return;

            TimeRemaining -= (float)delta;

            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                _expired = true;
                IsRunning = false;
                OnTimerExpired();
                return;
            }

            // Warning thresholds
            if (!_warnedAt60 && TimeRemaining <= 60f)
            {
                _warnedAt60 = true;
                GameEvents.OnTimerWarning?.Invoke(60f);
                GameEvents.OnSystemMessage?.Invoke("Warning",
                    "ATTENTION CRAWLERS: 60 seconds remaining on this floor. The dungeon grows impatient.");
            }
            else if (!_warnedAt30 && TimeRemaining <= 30f)
            {
                _warnedAt30 = true;
                GameEvents.OnTimerWarning?.Invoke(30f);
                GameEvents.OnSystemMessage?.Invoke("Warning",
                    "WARNING: 30 seconds! The walls are starting to shake. Find the stairwell!");
            }
            else if (!_warnedAt10 && TimeRemaining <= 10f)
            {
                _warnedAt10 = true;
                GameEvents.OnTimerWarning?.Invoke(10f);

                if (ServiceLocator.TryGet<AudioManager>(out var audio))
                    audio.PlaySFXByName("heartbeat");
            }
        }

        private void OnTimerExpired()
        {
            GameEvents.OnTimerExpired?.Invoke();

            // Deal 50% max HP damage to player
            if (ServiceLocator.TryGet<PlayerController>(out var player))
            {
                float maxHp = player.Stats.GetStat(StatType.MaxHealth);
                float dmg = maxHp * 0.5f;

                var damageInfo = new DamageInfo
                {
                    RawDamage = dmg,
                    FinalDamage = dmg,
                    IsCritical = false,
                    DamageType = DamageType.Physical,
                    HitPoint = player.GlobalPosition
                };
                player.Health.TakeDamage(damageInfo);

                GD.Print($"[StairwellTimer] Floor collapsed! Dealt {dmg:F0} damage to player");
            }

            // System message
            GameEvents.OnSystemMessage?.Invoke("Collapse",
                "THE FLOOR HAS COLLAPSED! The dungeon's patience has run out. You've been dragged to the stairwell.");

            // Commentary
            if (ServiceLocator.TryGet<CommentaryManager>(out var commentary))
            {
                commentary.QueueLine("Dungeon AI",
                    "Time's up! The audience thought you'd be faster. They were wrong.",
                    CommentaryPriority.High, CommentaryCategory.CombatReaction);
            }
        }

        /// <summary>
        /// Restore timer state from save data.
        /// </summary>
        public void SetTimeRemaining(float time)
        {
            TimeRemaining = time;
            if (time <= 0f)
            {
                _expired = true;
                IsRunning = false;
            }
        }

        public override void _ExitTree()
        {
            GameEvents.OnFloorEntered -= OnFloorEntered;
            GameEvents.OnGameStateChanged -= OnGameStateChanged;
            ServiceLocator.Unregister<StairwellTimer>();
        }
    }
}
