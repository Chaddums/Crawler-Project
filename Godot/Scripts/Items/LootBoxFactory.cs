using System;
using System.Collections.Generic;
using Godot;

namespace DungeonCrawlerCarl
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

            RegisterTemplate(LootBoxTier.Bronze, "Bronze Loot Box", 1, 2, 0.03f, 0.005f);
            RegisterTemplate(LootBoxTier.Silver, "Silver Loot Box", 2, 3, 0.08f, 0.02f);
            RegisterTemplate(LootBoxTier.Gold, "Gold Loot Box", 2, 4, 0.20f, 0.05f);
            RegisterTemplate(LootBoxTier.Diamond, "Diamond Loot Box", 3, 4, 0.40f, 0.15f);
            RegisterTemplate(LootBoxTier.Legendary, "Legendary Loot Box", 3, 5, 0.60f, 0.30f);

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
                _ => ItemRarity.Common
            };
        }

        private static ItemInstance RollRandomItem(ItemRarity rarity)
        {
            // Build a pool of equipment templates to roll from
            var templates = GetEquipmentTemplates();
            if (templates.Count == 0) return null;

            var baseData = templates[_rng.Next(templates.Count)];
            return new ItemInstance(baseData, rarity);
        }

        private static List<EquipmentData> GetEquipmentTemplates()
        {
            // Generate a standard pool of equipment if not provided externally
            var pool = new List<EquipmentData>();

            var slots = new[] {
                (EquipmentSlot.MainHand, "Sword", StatType.Strength, 5f),
                (EquipmentSlot.MainHand, "Staff", StatType.Intelligence, 5f),
                (EquipmentSlot.MainHand, "Dagger", StatType.Dexterity, 5f),
                (EquipmentSlot.OffHand, "Shield", StatType.Armor, 3f),
                (EquipmentSlot.Head, "Helmet", StatType.Armor, 2f),
                (EquipmentSlot.Chest, "Chestplate", StatType.Armor, 4f),
                (EquipmentSlot.Chest, "Robe", StatType.MaxMana, 20f),
                (EquipmentSlot.Legs, "Greaves", StatType.Armor, 3f),
                (EquipmentSlot.Feet, "Boots", StatType.MoveSpeed, 0.5f),
                (EquipmentSlot.Hands, "Gauntlets", StatType.AttackSpeed, 0.05f),
                (EquipmentSlot.Amulet, "Amulet", StatType.MaxHealth, 15f),
                (EquipmentSlot.Ring1, "Ring", StatType.CritChance, 0.03f),
                (EquipmentSlot.Back, "Cloak", StatType.CooldownReduction, 0.05f),
            };

            foreach (var (slot, name, stat, value) in slots)
            {
                var id = $"lootbox_{name.ToLower()}";
                var equip = new EquipmentData(id, name, ItemRarity.Common, slot, 1);
                equip.AddBaseStat(stat, ModifierType.Flat, value);
                pool.Add(equip);
                ItemRegistry.Register(equip);
            }

            return pool;
        }
    }
}
