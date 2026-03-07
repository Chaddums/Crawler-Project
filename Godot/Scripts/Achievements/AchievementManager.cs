using System;
using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Tracks achievement progress and unlocks. Subscribes to game events,
    /// increments counters, and fires OnAchievementUnlocked when thresholds are met.
    /// </summary>
    public partial class AchievementManager : Node
    {
        private HashSet<string> _unlocked = new();
        private Dictionary<string, int> _counters = new();

        /// <summary>
        /// Loot boxes earned from achievements, waiting to be opened in the safe room.
        /// </summary>
        public static readonly Queue<ItemInstance> PendingLootBoxes = new();

        // Per-floor tracking
        private int _sectorKills;
        private int _sectorPotionsUsed;
        private int _consecutiveCrits;
        private float _sectorStartTime;
        private int _currentSector;

        public override void _Ready()
        {
            AchievementRegistry.Initialize();
            LootBoxFactory.Initialize();

            ServiceLocator.Register(this);

            GameEvents.OnEnemyKilled += OnEnemyKilled;
            GameEvents.OnBossDefeated += OnBossDefeated;
            GameEvents.OnSectorEntered += OnSectorEntered;
            GameEvents.OnItemUsed += OnItemUsed;
            GameEvents.OnItemPickedUp += OnItemPickedUp;
            GameEvents.OnComboHit += OnComboHit;
            GameEvents.OnDamageDealt += OnDamageDealt;
            GameEvents.OnRoomCleared += OnRoomCleared;
            GameEvents.OnClassSelected += OnClassSelected;
            GameEvents.OnPlayerLevelUp += OnPlayerLevelUp;
            GameEvents.OnItemEquipped += OnItemEquipped;
            GameEvents.OnTimerWarning += OnTimerWarning;

            GD.Print("[AchievementManager] Ready");
        }

        // ── Event Handlers ──

        private void OnEnemyKilled(Node enemy)
        {
            _sectorKills++;
            Increment("kills");
            CheckThresholds();
        }

        private void OnBossDefeated(Node boss)
        {
            Increment("bosses_killed");
            TryUnlock("boss_slayer");
        }

        private void OnSectorEntered(int sector)
        {
            // Check for pacifist on previous sector
            if (_currentSector > 0 && _sectorKills == 0)
                TryUnlock("pacifist_floor");

            // Check for iron frame on previous sector
            if (_currentSector > 0 && _sectorPotionsUsed == 0)
                TryUnlock("iron_frame");

            _currentSector = sector;
            _sectorKills = 0;
            _sectorPotionsUsed = 0;
            _sectorStartTime = (float)Time.GetTicksMsec() / 1000f;

            if (sector >= 2) TryUnlock("floor_2");
            if (sector >= 5) TryUnlock("floor_5");
        }

        private void OnItemUsed(object item)
        {
            _sectorPotionsUsed++;
            Increment("consumables_used");

            // Check back_from_the_brink
            if (ServiceLocator.TryGet<PlayerController>(out var player))
            {
                float maxHp = player.Stats.GetStat(StatType.MaxHealth);
                float currentHp = player.Health.CurrentHealth;
                float hpPercent = maxHp > 0 ? currentHp / maxHp : 1f;

                if (hpPercent > 0.8f)
                {
                    // We just healed — check if we were below 10% before
                    // (tracked via close_call counter being recent)
                    if (GetCounter("was_low_hp") > 0)
                    {
                        TryUnlock("back_from_the_brink");
                        SetCounter("was_low_hp", 0);
                    }
                }
            }

            CheckThresholds();
        }

        private void OnItemPickedUp(Godot.Resource item)
        {
            // Check hoarder
            if (ServiceLocator.TryGet<PlayerController>(out var player))
            {
                if (player.Inventory.Items.Count >= 25)
                    TryUnlock("hoarder");
            }
        }

        private void OnComboHit(int comboCount)
        {
            if (comboCount >= 5)
                TryUnlock("combo_master");
        }

        private void OnDamageDealt(DamageInfo damage)
        {
            // Track overkill
            if (damage.FinalDamage >= 100f && damage.Attacker != null)
            {
                // Only for player damage
                if (damage.Attacker.IsInGroup(Constants.GROUP_PLAYER))
                    TryUnlock("overkill");
            }

            // Track consecutive crits
            if (damage.Attacker != null && damage.Attacker.IsInGroup(Constants.GROUP_PLAYER))
            {
                if (damage.IsCritical)
                {
                    _consecutiveCrits++;
                    if (_consecutiveCrits >= 3)
                        TryUnlock("critical_streak");
                }
                else
                {
                    _consecutiveCrits = 0;
                }
            }

            // Track close call — survival at <10% HP
            if (damage.Target != null && damage.Target.IsInGroup(Constants.GROUP_PLAYER))
            {
                if (ServiceLocator.TryGet<PlayerController>(out var player))
                {
                    float maxHp = player.Stats.GetStat(StatType.MaxHealth);
                    float currentHp = player.Health.CurrentHealth;
                    if (currentHp > 0 && maxHp > 0 && currentHp / maxHp < 0.1f)
                    {
                        TryUnlock("close_call");
                        SetCounter("was_low_hp", 1);
                    }
                }
            }
        }

        private void OnRoomCleared(Node room)
        {
            Increment("rooms_cleared");

            // Check treasure room
            if (room is RoomController rc && rc.RoomType == RoomType.Treasure)
                Increment("treasure_rooms");

            CheckThresholds();
        }

        private void OnClassSelected(Godot.Resource classData)
        {
            TryUnlock("class_chosen");
        }

        private void OnPlayerLevelUp(int level)
        {
            Increment("abilities_cast"); // Not exactly right, but level tracks progression
            if (level >= 5) TryUnlock("level_5");
            if (level >= 10) TryUnlock("level_10");
        }

        private void OnItemEquipped(Godot.Resource item)
        {
            // Check well_equipped
            if (ServiceLocator.TryGet<PlayerController>(out var player))
            {
                if (player.Inventory.Equipped.Count >= 11) // All slots filled
                    TryUnlock("well_equipped");
            }
        }

        private void OnTimerWarning(float secondsRemaining)
        {
            if (secondsRemaining <= 30f)
            {
                // stairwell_rush is checked when entering stairwell, but we flag it here
                SetCounter("timer_was_under_30", 1);
            }
        }

        // Called from outside when player enters stairwell
        public void CheckStairwellRush()
        {
            if (GetCounter("timer_was_under_30") > 0)
            {
                TryUnlock("stairwell_rush");
                SetCounter("timer_was_under_30", 0);
            }
        }

        // Called from outside to track ability casts
        public void TrackAbilityCast()
        {
            Increment("abilities_cast");
            CheckThresholds();
        }

        // Called when floor is completed to check speed run
        public void CheckSpeedRun(int floor)
        {
            if (floor != 1) return;
            float elapsed = (float)Time.GetTicksMsec() / 1000f - _sectorStartTime;
            if (elapsed < 120f)
                TryUnlock("speed_runner");
        }

        // ── Core ──

        public bool TryUnlock(string id)
        {
            if (_unlocked.Contains(id)) return false;

            var data = AchievementRegistry.Get(id);
            if (data == null) return false;

            _unlocked.Add(id);

            // Fire event
            GameEvents.OnAchievementUnlocked?.Invoke(id);

            // Queue loot box reward for safe room ceremony
            if (data.RewardTier.HasValue)
            {
                var lootBox = LootBoxFactory.CreateLootBox(data.RewardTier.Value);
                if (lootBox != null)
                    PendingLootBoxes.Enqueue(lootBox);
            }

            // Play audio
            if (ServiceLocator.TryGet<AudioManager>(out var audio))
                audio.PlaySFXByName("achievement");

            GD.Print($"[Achievement] Unlocked: {data.Title} — {data.SnarkMessage}");

            // Check meta achievement
            if (_unlocked.Count >= 15)
                TryUnlock("achievement_hunter");

            return true;
        }

        private void CheckThresholds()
        {
            int kills = GetCounter("kills");
            if (kills >= 1) TryUnlock("first_blood");
            if (kills >= 10) TryUnlock("getting_warmed_up");
            if (kills >= 50) TryUnlock("dungeon_menace");

            int consumables = GetCounter("consumables_used");
            if (consumables >= 20) TryUnlock("potion_addict");

            int treasureRooms = GetCounter("treasure_rooms");
            if (treasureRooms >= 10) TryUnlock("treasure_hunter");

            int abilities = GetCounter("abilities_cast");
            if (abilities >= 50) TryUnlock("spell_slinger");
        }

        // ── Counter Helpers ──

        private void Increment(string key, int amount = 1)
        {
            _counters.TryGetValue(key, out int current);
            _counters[key] = current + amount;
        }

        private int GetCounter(string key)
        {
            return _counters.TryGetValue(key, out int val) ? val : 0;
        }

        private void SetCounter(string key, int value)
        {
            _counters[key] = value;
        }

        // ── Save/Load ──

        public AchievementSaveData GetSaveData()
        {
            return new AchievementSaveData
            {
                Unlocked = new HashSet<string>(_unlocked),
                Counters = new Dictionary<string, int>(_counters)
            };
        }

        public void LoadSaveData(AchievementSaveData data)
        {
            if (data == null) return;
            _unlocked = data.Unlocked ?? new HashSet<string>();
            _counters = data.Counters ?? new Dictionary<string, int>();
            GD.Print($"[AchievementManager] Loaded {_unlocked.Count} unlocked, {_counters.Count} counters");
        }

        public bool IsUnlocked(string id) => _unlocked.Contains(id);
        public int UnlockedCount => _unlocked.Count;

        public override void _ExitTree()
        {
            GameEvents.OnEnemyKilled -= OnEnemyKilled;
            GameEvents.OnBossDefeated -= OnBossDefeated;
            GameEvents.OnSectorEntered -= OnSectorEntered;
            GameEvents.OnItemUsed -= OnItemUsed;
            GameEvents.OnItemPickedUp -= OnItemPickedUp;
            GameEvents.OnComboHit -= OnComboHit;
            GameEvents.OnDamageDealt -= OnDamageDealt;
            GameEvents.OnRoomCleared -= OnRoomCleared;
            GameEvents.OnClassSelected -= OnClassSelected;
            GameEvents.OnPlayerLevelUp -= OnPlayerLevelUp;
            GameEvents.OnItemEquipped -= OnItemEquipped;
            GameEvents.OnTimerWarning -= OnTimerWarning;
            ServiceLocator.Unregister<AchievementManager>();
        }
    }
}
