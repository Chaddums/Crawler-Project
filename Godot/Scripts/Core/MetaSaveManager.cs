using System;
using System.Text.Json;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Manages the persistent meta-progression save file.
    /// This file is NEVER deleted — it persists across deaths and new runs.
    /// </summary>
    public static class MetaSaveManager
    {
        private const string META_SAVE_PATH = "res://junkbot_meta.json";

        private static MetaSaveData _data;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>Current meta save data. Loaded once, kept in memory.</summary>
        public static MetaSaveData Data
        {
            get
            {
                _data ??= Load();
                return _data;
            }
        }

        /// <summary>
        /// Computed threat level based on unlocked perks and gear codex size.
        /// Drives difficulty scaling for the current run.
        /// </summary>
        public static int ThreatLevel
        {
            get
            {
                int threat = 0;

                // Each perk rank adds threat
                foreach (var (perkId, rank) in Data.UnlockedPerks)
                {
                    var perk = PerkRegistry.Get(perkId);
                    if (perk != null)
                        threat += rank * perk.ThreatPerRank;
                }

                // Every 5 codex entries adds 1 threat
                threat += Data.GearCodex.Count / 5;

                return threat;
            }
        }

        /// <summary>
        /// Difficulty multiplier derived from threat level.
        /// Applied to enemy HP, damage, and density.
        /// </summary>
        public static float DifficultyScale => 1f + ThreatLevel * 0.05f;

        /// <summary>
        /// Bonus loot rarity chance from threat level.
        /// Higher threat = better loot baseline.
        /// </summary>
        public static float BonusRarityChance => ThreatLevel * 0.01f;

        public static void AddScrap(int amount)
        {
            Data.Scrap += amount;
            Data.LifetimeScrap += amount;
        }

        public static bool SpendScrap(int amount)
        {
            if (Data.Scrap < amount) return false;
            Data.Scrap -= amount;
            return true;
        }

        /// <summary>
        /// Register a gear item as discovered in the codex.
        /// Returns true if this is a NEW discovery.
        /// </summary>
        public static bool DiscoverGear(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            bool isNew = Data.GearCodex.Add(itemId);
            if (isNew)
                GD.Print($"[MetaSave] NEW codex entry: {itemId} (total: {Data.GearCodex.Count})");
            return isNew;
        }

        /// <summary>
        /// Record end-of-run stats. Called when the player dies or completes a run.
        /// </summary>
        public static void RecordRunEnd(BotFrameType className, int sector, int area,
            int level, int kills, float timeElapsed, bool isVictory = false)
        {
            var h = Data.History;
            h.TotalRuns++;
            if (!isVictory) h.TotalDeaths++;
            h.TotalKills += kills;

            if (sector > h.BestSector || (sector == h.BestSector && area > h.BestArea))
            {
                h.BestSector = sector;
                h.BestArea = area;
            }

            if (level > h.HighestLevel)
                h.HighestLevel = level;

            if (timeElapsed > h.BestTime)
                h.BestTime = timeElapsed;

            string classKey = className.ToString();
            h.RunsPerClass.TryGetValue(classKey, out int runs);
            h.RunsPerClass[classKey] = runs + 1;

            h.BestSectorPerClass.TryGetValue(classKey, out int best);
            if (sector > best)
                h.BestSectorPerClass[classKey] = sector;

            // Award scrap based on performance
            int scrapEarned = CalculateRunScrap(sector, area, level, kills);
            AddScrap(scrapEarned);

            GD.Print($"[MetaSave] Run ended: Sector {sector}-{area}, Lv{level}, " +
                $"{kills} kills, earned {scrapEarned} scrap (total: {Data.Scrap})");

            Save();
        }

        private static int CalculateRunScrap(int sector, int area, int level, int kills)
        {
            // Base scrap from progress
            int scrap = (sector - 1) * 100 + (area - 1) * 25;
            // Bonus from kills
            scrap += kills * 2;
            // Bonus from level
            scrap += level * 10;
            return scrap;
        }

        public static bool IsFrameUnlocked(BotFrameType frame)
        {
            return Data.UnlockedFrames.Contains(frame.ToString());
        }

        public static bool CanUnlockFrame(BotFrameType frame)
        {
            if (IsFrameUnlocked(frame)) return false;
            var frameData = BotFrameRegistry.GetClass(frame);
            if (frameData == null) return false;
            return MeetsFrameRequirement(frame) && Data.Scrap >= frameData.UnlockCost;
        }

        public static bool MeetsFrameRequirement(BotFrameType frame)
        {
            var h = Data.History;
            return frame switch
            {
                BotFrameType.TinCan => true,
                BotFrameType.Scrapheap => h.BestSector >= 2,
                BotFrameType.SparkPlug => h.HighestLevel >= 5,
                BotFrameType.RustBucket => h.TotalKills >= 100,
                BotFrameType.NoiseBox => h.TotalRuns >= 5,
                BotFrameType.Clunker => h.BestSector >= 3,
                _ => false,
            };
        }

        public static bool UnlockFrame(BotFrameType frame)
        {
            if (IsFrameUnlocked(frame)) return false;
            var frameData = BotFrameRegistry.GetClass(frame);
            if (frameData == null) return false;
            if (!MeetsFrameRequirement(frame)) return false;
            if (!SpendScrap(frameData.UnlockCost)) return false;

            Data.UnlockedFrames.Add(frame.ToString());
            GD.Print($"[MetaSave] Frame unlocked: {frame}");
            Save();
            return true;
        }

        public static bool UnlockPerk(string perkId)
        {
            var perk = PerkRegistry.Get(perkId);
            if (perk == null) return false;

            Data.UnlockedPerks.TryGetValue(perkId, out int currentRank);
            if (currentRank >= perk.MaxRank) return false;

            int cost = perk.GetCost(currentRank + 1);
            if (!SpendScrap(cost)) return false;

            Data.UnlockedPerks[perkId] = currentRank + 1;
            GD.Print($"[MetaSave] Perk unlocked: {perk.Name} rank {currentRank + 1}/{perk.MaxRank}");
            Save();
            return true;
        }

        public static int GetPerkRank(string perkId)
        {
            Data.UnlockedPerks.TryGetValue(perkId, out int rank);
            return rank;
        }

        /// <summary>
        /// Apply permanent perk bonuses to a player's stat block at run start.
        /// </summary>
        public static void ApplyPerksToPlayer(PlayerStats stats)
        {
            foreach (var (perkId, rank) in Data.UnlockedPerks)
            {
                var perk = PerkRegistry.Get(perkId);
                if (perk == null || rank <= 0) continue;

                float value = perk.ValuePerRank * rank;
                stats.Stats.AddModifier(new StatModifier(
                    perk.AffectedStat, perk.ModType, value, "MetaPerk"));
            }

            GD.Print($"[MetaSave] Applied {Data.UnlockedPerks.Count} perks to player (Threat: {ThreatLevel})");
        }

        /// <summary>
        /// Apply threat level scaling to a sector's difficulty.
        /// </summary>
        public static void ApplyThreatToSector(SectorData sector)
        {
            float scale = DifficultyScale * AscensionMultiplier;
            sector.DifficultyMultiplier *= scale;
            sector.MinEnemiesPerRoom = Mathf.RoundToInt(sector.MinEnemiesPerRoom * Mathf.Sqrt(scale));
            sector.MaxEnemiesPerRoom = Mathf.RoundToInt(sector.MaxEnemiesPerRoom * Mathf.Sqrt(scale));
            sector.RareLootChance += BonusRarityChance + AscensionLootBonus;
        }

        public static void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(Data, JsonOptions);
                using var file = FileAccess.Open(META_SAVE_PATH, FileAccess.ModeFlags.Write);
                if (file != null)
                    file.StoreString(json);
                else
                    GD.PrintErr($"[MetaSave] Failed to save: {FileAccess.GetOpenError()}");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MetaSave] Save error: {ex.Message}");
            }
        }

        private static MetaSaveData Load()
        {
            if (!FileAccess.FileExists(META_SAVE_PATH))
            {
                GD.Print("[MetaSave] No meta save found, creating fresh");
                var fresh = new MetaSaveData();
                return fresh;
            }

            try
            {
                using var file = FileAccess.Open(META_SAVE_PATH, FileAccess.ModeFlags.Read);
                if (file == null) return new MetaSaveData();

                string json = file.GetAsText();
                var data = JsonSerializer.Deserialize<MetaSaveData>(json, JsonOptions);
                GD.Print($"[MetaSave] Loaded: {data.Scrap} scrap, {data.GearCodex.Count} codex, " +
                    $"{data.UnlockedPerks.Count} perks, Threat {ThreatLevel}");
                return data;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MetaSave] Load error: {ex.Message}");
                return new MetaSaveData();
            }
        }

        /// <summary>
        /// Called when AXIS is defeated. Increments ascension rank and records the victory.
        /// </summary>
        public static void RecordAxisVictory()
        {
            Data.TimesAxisDefeated++;
            Data.AscensionRank++;
            GD.Print($"[MetaSave] AXIS defeated! Ascension rank now {Data.AscensionRank}");
            Save();
        }

        /// <summary>
        /// Global difficulty multiplier from ascension rank.
        /// Stacks with threat level and sector difficulty.
        /// </summary>
        public static float AscensionMultiplier => 1f + Data.AscensionRank * 0.5f;

        /// <summary>
        /// Bonus loot rarity from ascension rank. Higher ascension = better drops.
        /// </summary>
        public static float AscensionLootBonus => Data.AscensionRank * 0.03f;

        /// <summary>Reset for testing only.</summary>
        public static void ResetAll()
        {
            _data = new MetaSaveData();
            Save();
            GD.Print("[MetaSave] Reset all meta progression");
        }
    }
}
