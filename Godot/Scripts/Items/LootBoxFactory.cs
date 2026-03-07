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
        /// </summary>
        public static List<ItemInstance> OpenLootBox(LootBoxData data)
        {
            var results = new List<ItemInstance>();
            int itemCount = _rng.Next(data.MinItems, data.MaxItems + 1);

            for (int i = 0; i < itemCount; i++)
            {
                var rarity = RollTierRarity(data);
                var item = RollRandomItem(rarity);
                if (item != null)
                    results.Add(item);
            }

            return results;
        }

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
