using System;
using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Creates loot box items and resolves their contents when opened.
    /// Tier determines item count and rarity weighting.
    /// </summary>
    public static class LootBoxFactory
    {
        private static readonly Random _rng = new();
        private static readonly Dictionary<LootBoxTier, LootBoxData> _templates = new();
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            RegisterTemplate(LootBoxTier.Junk, StringLoader.Get("lootBoxes.Junk"), 1, 1, 0f, 0f);
            RegisterTemplate(LootBoxTier.Bronze, StringLoader.Get("lootBoxes.Bronze"), 1, 2, 0.03f, 0.005f);
            RegisterTemplate(LootBoxTier.Silver, StringLoader.Get("lootBoxes.Silver"), 2, 3, 0.08f, 0.02f);
            RegisterTemplate(LootBoxTier.Gold, StringLoader.Get("lootBoxes.Gold"), 2, 4, 0.20f, 0.05f);
            RegisterTemplate(LootBoxTier.Diamond, StringLoader.Get("lootBoxes.Diamond"), 3, 4, 0.40f, 0.15f);
            RegisterTemplate(LootBoxTier.Legendary, StringLoader.Get("lootBoxes.Legendary"), 3, 5, 0.60f, 0.30f);
            RegisterTemplate(LootBoxTier.Celestial, StringLoader.Get("lootBoxes.Celestial"), 4, 6, 0.80f, 0.50f);

            GD.Print($"[LootBoxFactory] Initialized {_templates.Count} loot box tiers");
        }

        private static void RegisterTemplate(LootBoxTier tier, string name, int min, int max,
            float epicChance, float legendaryChance)
        {
            string id = $"lootbox_{tier.ToString().ToLower()}";
            var data = new LootBoxData(id, name, tier, min, max, epicChance, legendaryChance);

            // Set display rarity based on tier
            data.Rarity = tier switch
            {
                LootBoxTier.Junk => ItemRarity.Common,
                LootBoxTier.Bronze => ItemRarity.Common,
                LootBoxTier.Silver => ItemRarity.Uncommon,
                LootBoxTier.Gold => ItemRarity.Rare,
                LootBoxTier.Diamond => ItemRarity.Epic,
                LootBoxTier.Legendary => ItemRarity.Legendary,
                LootBoxTier.Celestial => ItemRarity.Legendary,
                _ => ItemRarity.Common
            };

            _templates[tier] = data;
            ItemRegistry.Register(data);
        }

        /// <summary>
        /// Create a loot box ItemInstance of the given tier.
        /// </summary>
        public static ItemInstance CreateLootBox(LootBoxTier tier)
        {
            Initialize();

            if (!_templates.TryGetValue(tier, out var template))
            {
                GD.PrintErr($"[LootBoxFactory] Unknown tier: {tier}");
                return null;
            }

            return new ItemInstance(template, template.Rarity);
        }

        /// <summary>
        /// Open a loot box and roll its contents.
        /// Gold+ tiers have a chance to contain a salvage core.
        /// </summary>
        public static List<ItemInstance> OpenLootBox(LootBoxData data)
        {
            var results = new List<ItemInstance>();
            int itemCount = _rng.Next(data.MinItems, data.MaxItems + 1);

            for (int i = 0; i < itemCount; i++)
            {
                ItemInstance item;
                if (data.Tier == LootBoxTier.Junk)
                {
                    item = RollJunkItem();
                }
                else
                {
                    var rarity = RollTierRarity(data);
                    item = RollRandomItem(rarity);
                }
                if (item != null)
                    results.Add(item);
            }

            // Salvage core drop chance on Gold+ boxes
            float coreChance = data.Tier switch
            {
                LootBoxTier.Gold => 0.05f,
                LootBoxTier.Diamond => 0.15f,
                LootBoxTier.Legendary => 0.30f,
                LootBoxTier.Celestial => 0.50f,
                _ => 0f
            };
            if (coreChance > 0f && _rng.NextDouble() < coreChance)
            {
                int sector = GameManager.Instance?.CurrentSector ?? 1;
                int ascension = MetaSaveManager.Data.AscensionRank;
                var core = RollRandomCore(data.Tier, sector, ascension);
                if (core != null)
                    results.Add(core);
            }

            return results;
        }

        // Pity timer: tracks consecutive non-Mythic core rolls
        private static int _mythicPityCounter;
        private const int MythicPityThreshold = 200; // Guaranteed Mythic after this many rolls
        private const float MythicBaseChance = 0.005f; // 0.5% base

        /// <summary>
        /// Roll a random salvage core using weighted drops with source filtering.
        /// Higher tier boxes can roll rarer cores. Mythic drops use a pity timer.
        /// </summary>
        public static ItemInstance RollRandomCore(LootBoxTier sourceTier = LootBoxTier.Gold,
            int sector = 0, int ascension = 0, EnemyTier enemyTier = EnemyTier.Normal,
            string bossId = "")
        {
            var allCores = SalvageCoreRegistry.All;
            if (allCores.Count == 0) return null;

            // Max rarity based on source tier
            SalvageCoreRarity maxRarity = sourceTier switch
            {
                LootBoxTier.Gold => SalvageCoreRarity.Rare,
                LootBoxTier.Diamond => SalvageCoreRarity.Epic,
                LootBoxTier.Legendary => SalvageCoreRarity.Legendary,
                LootBoxTier.Celestial => SalvageCoreRarity.Mythic,
                _ => SalvageCoreRarity.Rare
            };

            // Celestial boxes can roll Mythic — check pity timer
            bool rollMythic = false;
            if (maxRarity >= SalvageCoreRarity.Mythic)
            {
                _mythicPityCounter++;
                float pityBonus = _mythicPityCounter / (float)MythicPityThreshold;
                float mythicChance = MythicBaseChance + (MythicBaseChance * pityBonus * 2f);
                if (_mythicPityCounter >= MythicPityThreshold)
                    mythicChance = 1f; // Guaranteed
                rollMythic = _rng.NextDouble() < mythicChance;
            }

            if (rollMythic)
            {
                // Roll from eligible Mythic grafts using source context
                var mythicEligible = SalvageCoreRegistry.GetEligibleDrops(
                    sector, ascension, enemyTier, bossId);
                mythicEligible.RemoveAll(c => c.Rarity != SalvageCoreRarity.Mythic);

                if (mythicEligible.Count > 0)
                {
                    var picked = RollWeighted(mythicEligible);
                    GD.Print($"[LootBoxFactory] MYTHIC DROP: {picked.CoreName}! (pity was {_mythicPityCounter})");
                    _mythicPityCounter = 0; // Reset pity
                    var itemData = new SalvageCoreItemData(picked);
                    return new ItemInstance(itemData, itemData.Rarity);
                }
                // No eligible Mythic for this context — fall through to normal roll
            }

            // Normal weighted roll from eligible non-Mythic cores
            var eligible = new List<SalvageCoreData>();
            foreach (var kvp in allCores)
            {
                if (kvp.Value.Rarity <= maxRarity && kvp.Value.Rarity < SalvageCoreRarity.Mythic)
                    eligible.Add(kvp.Value);
            }

            if (eligible.Count == 0) return null;

            // Weight by rarity: rarer = lower weight
            var picked2 = RollWeighted(eligible);
            var itemData2 = new SalvageCoreItemData(picked2);
            return new ItemInstance(itemData2, itemData2.Rarity);
        }

        /// <summary>
        /// Pick from a list of cores using their Weight values (from DropSource)
        /// and rarity-based weighting for cores without explicit weights.
        /// </summary>
        private static SalvageCoreData RollWeighted(List<SalvageCoreData> cores)
        {
            float totalWeight = 0f;
            foreach (var core in cores)
            {
                float w = core.DropSource?.Weight ?? GetDefaultWeight(core.Rarity);
                totalWeight += w;
            }

            float roll = (float)(_rng.NextDouble() * totalWeight);
            float cumulative = 0f;
            foreach (var core in cores)
            {
                cumulative += core.DropSource?.Weight ?? GetDefaultWeight(core.Rarity);
                if (roll <= cumulative)
                    return core;
            }
            return cores[cores.Count - 1];
        }

        private static float GetDefaultWeight(SalvageCoreRarity rarity) => rarity switch
        {
            SalvageCoreRarity.Rare => 10f,
            SalvageCoreRarity.Epic => 4f,
            SalvageCoreRarity.Legendary => 1f,
            SalvageCoreRarity.Mythic => 0.3f,
            _ => 5f
        };

        private static ItemRarity RollTierRarity(LootBoxData data)
        {
            float roll = (float)_rng.NextDouble();

            if (roll < data.LegendaryChance)
                return ItemRarity.Legendary;
            if (roll < data.LegendaryChance + data.EpicChance)
                return ItemRarity.Epic;

            // Scale base rarities by tier
            return data.Tier switch
            {
                LootBoxTier.Bronze => roll < 0.5f ? ItemRarity.Common : ItemRarity.Uncommon,
                LootBoxTier.Silver => roll < 0.4f ? ItemRarity.Uncommon : ItemRarity.Rare,
                LootBoxTier.Gold => roll < 0.5f ? ItemRarity.Rare : ItemRarity.Uncommon,
                LootBoxTier.Diamond => roll < 0.6f ? ItemRarity.Epic : ItemRarity.Rare,
                LootBoxTier.Legendary => ItemRarity.Epic,
                LootBoxTier.Celestial => ItemRarity.Legendary,
                _ => ItemRarity.Common
            };
        }

        /// <summary>
        /// Junk boxes contain a single small repair kit or battery pack.
        /// </summary>
        private static ItemInstance RollJunkItem()
        {
            string id = _rng.Next(2) == 0 ? "potion_health_small" : "potion_mana_small";
            var data = ConsumableRegistry.Get(id);
            if (data == null) return null;
            return new ItemInstance(data, ItemRarity.Common);
        }

        private static ItemInstance RollRandomItem(ItemRarity rarity)
        {
            // Use shared equipment pool from BaseItemPool
            var templates = BaseItemPool.Equipment;
            if (templates.Count == 0) return null;

            var baseData = templates[_rng.Next(templates.Count)];
            return new ItemInstance(baseData, rarity);
        }
    }
}
