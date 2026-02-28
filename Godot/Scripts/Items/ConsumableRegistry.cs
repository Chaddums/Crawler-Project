using System.Collections.Generic;
using Godot;

namespace JunkbotArena
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

            // --- Repair Kits ---
            Register(new ConsumableData
            {
                Id = "potion_health_small",
                ItemName = "Small Repair Kit",
                Description = "Restores 25 HP. Mostly duct tape and hope.",
                Rarity = ItemRarity.Common,
                MaxStack = 5,
                BaseValue = 10,
                HealAmount = 25f
            });

            Register(new ConsumableData
            {
                Id = "potion_health_medium",
                ItemName = "Standard Repair Kit",
                Description = "Restores 50 HP. Includes actual solder this time.",
                Rarity = ItemRarity.Uncommon,
                MaxStack = 5,
                BaseValue = 25,
                HealAmount = 50f
            });

            Register(new ConsumableData
            {
                Id = "potion_health_large",
                ItemName = "Deluxe Repair Kit",
                Description = "Restores 100 HP. Factory-grade nanopaste. The good stuff.",
                Rarity = ItemRarity.Rare,
                MaxStack = 5,
                BaseValue = 50,
                HealAmount = 100f
            });

            // --- Battery Packs ---
            Register(new ConsumableData
            {
                Id = "potion_mana_small",
                ItemName = "Small Battery Pack",
                Description = "Restores 15 energy. Salvaged from a dead vending machine.",
                Rarity = ItemRarity.Common,
                MaxStack = 5,
                BaseValue = 10,
                ManaRestoreAmount = 15f
            });

            Register(new ConsumableData
            {
                Id = "potion_mana_medium",
                ItemName = "Standard Battery Pack",
                Description = "Restores 30 energy. Hums aggressively when shaken.",
                Rarity = ItemRarity.Uncommon,
                MaxStack = 5,
                BaseValue = 25,
                ManaRestoreAmount = 30f
            });

            Register(new ConsumableData
            {
                Id = "potion_mana_large",
                ItemName = "Industrial Battery Pack",
                Description = "Restores 60 energy. Basically liquid lightning.",
                Rarity = ItemRarity.Rare,
                MaxStack = 5,
                BaseValue = 50,
                ManaRestoreAmount = 60f
            });

            // --- Buff Modules ---
            Register(new ConsumableData
            {
                Id = "elixir_fortitude",
                ItemName = "Plating Booster",
                Description = "+5 Armor for 30 seconds. Temporary hardlight shell. Itches.",
                Rarity = ItemRarity.Rare,
                MaxStack = 5,
                BaseValue = 40,
                BuffId = "buff_fortitude",
                BuffDuration = 30f
            });

            Register(new ConsumableData
            {
                Id = "overclock_injector",
                ItemName = "Overclock Injector",
                Description = "+20% Attack Speed for 20 seconds. Side effects include servo whine and voided warranties.",
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
