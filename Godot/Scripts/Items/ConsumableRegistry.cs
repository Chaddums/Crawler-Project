using System.Collections.Generic;
using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Static registry of all consumable item definitions: potions, elixirs, etc.
    /// Must be initialized after ItemRegistry.
    /// </summary>
    public static class ConsumableRegistry
    {
        private static readonly Dictionary<string, ConsumableData> _consumables = new();
        private static bool _initialized;

        public static IReadOnlyDictionary<string, ConsumableData> Consumables => _consumables;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            // --- Health Potions ---
            Register(new ConsumableData
            {
                Id = "potion_health_small",
                ItemName = "Small Health Potion",
                Description = "Restores 25 HP. Tastes like floor water.",
                Rarity = ItemRarity.Common,
                MaxStack = 5,
                BaseValue = 10,
                HealAmount = 25f
            });

            Register(new ConsumableData
            {
                Id = "potion_health_medium",
                ItemName = "Medium Health Potion",
                Description = "Restores 50 HP. Suspiciously warm.",
                Rarity = ItemRarity.Uncommon,
                MaxStack = 5,
                BaseValue = 25,
                HealAmount = 50f
            });

            Register(new ConsumableData
            {
                Id = "potion_health_large",
                ItemName = "Large Health Potion",
                Description = "Restores 100 HP. The good stuff.",
                Rarity = ItemRarity.Rare,
                MaxStack = 5,
                BaseValue = 50,
                HealAmount = 100f
            });

            // --- Mana Potions ---
            Register(new ConsumableData
            {
                Id = "potion_mana_small",
                ItemName = "Small Mana Potion",
                Description = "Restores 15 mana. Glows faintly blue.",
                Rarity = ItemRarity.Common,
                MaxStack = 5,
                BaseValue = 10,
                ManaRestoreAmount = 15f
            });

            Register(new ConsumableData
            {
                Id = "potion_mana_medium",
                ItemName = "Medium Mana Potion",
                Description = "Restores 30 mana. Glows aggressively blue.",
                Rarity = ItemRarity.Uncommon,
                MaxStack = 5,
                BaseValue = 25,
                ManaRestoreAmount = 30f
            });

            Register(new ConsumableData
            {
                Id = "potion_mana_large",
                ItemName = "Large Mana Potion",
                Description = "Restores 60 mana. Basically liquid magic.",
                Rarity = ItemRarity.Rare,
                MaxStack = 5,
                BaseValue = 50,
                ManaRestoreAmount = 60f
            });

            // --- Buff Potions ---
            Register(new ConsumableData
            {
                Id = "elixir_fortitude",
                ItemName = "Elixir of Fortitude",
                Description = "+5 Armor for 30 seconds. Your skin hardens uncomfortably.",
                Rarity = ItemRarity.Rare,
                MaxStack = 5,
                BaseValue = 40,
                BuffId = "buff_fortitude",
                BuffDuration = 30f
            });

            Register(new ConsumableData
            {
                Id = "crawlers_adrenaline",
                ItemName = "Crawler's Adrenaline",
                Description = "+20% Attack Speed for 20 seconds. Side effects include jitteriness and poor life choices.",
                Rarity = ItemRarity.Rare,
                MaxStack = 5,
                BaseValue = 45,
                BuffId = "buff_adrenaline",
                BuffDuration = 20f
            });

            GD.Print($"[ConsumableRegistry] Initialized {_consumables.Count} consumables");
        }

        public static ConsumableData Get(string id)
        {
            return _consumables.TryGetValue(id, out var data) ? data : null;
        }

        private static void Register(ConsumableData data)
        {
            _consumables[data.Id] = data;
            ItemRegistry.Register(data);
        }
    }
}
