using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Per-sector countdown timer. Creates urgency and AXIS-style sector purge on expiry.
    /// Pauses in safe rooms. Fires warning events at 60s, 30s, 10s thresholds.
    /// </summary>
    public partial class LiftTimer : Node
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

            GameEvents.OnSectorEntered += OnSectorEntered;
            GameEvents.OnGameStateChanged += OnGameStateChanged;

            GD.Print("[LiftTimer] Ready");
        }

        private void OnSectorEntered(int floor)
        {
            var sectorData = SectorDataRegistry.GetSector(floor);
            TimeLimit = sectorData.TimeLimit;
            TimeRemaining = TimeLimit;

            _warnedAt60 = false;
            _warnedAt30 = false;
            _warnedAt10 = false;
            _expired = false;
            IsRunning = true;

            GD.Print($"[LiftTimer] Sector {floor} timer started: {TimeLimit}s");
        }

        private void OnGameStateChanged(GameState state)
        {
            // Pause in safe rooms
            IsRunning = state == GameState.InSector;
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
                    "ATTENTION SCRAPPERS: 60 seconds remaining. AXIS is growing impatient.");
            }
            else if (!_warnedAt30 && TimeRemaining <= 30f)
            {
                _warnedAt30 = true;
                GameEvents.OnTimerWarning?.Invoke(30f);
                GameEvents.OnSystemMessage?.Invoke("Warning",
                    "WARNING: 30 seconds! Sector purge initiating. Find the lift!");
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

                GD.Print($"[LiftTimer] Sector purge! Dealt {dmg:F0} damage to player");
            }

            // System message
            GameEvents.OnSystemMessage?.Invoke("Purge",
                "SECTOR PURGE COMPLETE. AXIS has run out of patience. You've been dragged to the lift.");

            // Commentary
            if (ServiceLocator.TryGet<CommentaryManager>(out var commentary))
            {
                commentary.QueueLine("AXIS",
                    "Time's up! I expected better from a machine. Dragging you to the next sector.",
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
            GameEvents.OnSectorEntered -= OnSectorEntered;
            GameEvents.OnGameStateChanged -= OnGameStateChanged;
            ServiceLocator.Unregister<LiftTimer>();
        }
    }
}
