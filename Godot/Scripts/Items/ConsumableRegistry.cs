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
                ItemName = StringLoader.Get("consumables.potion_health_small.name"),
                Description = StringLoader.Get("consumables.potion_health_small.description"),
                Rarity = ItemRarity.Common,
                MaxStack = 5,
                BaseValue = 10,
                HealAmount = 25f
            });

            Register(new ConsumableData
            {
                Id = "potion_health_medium",
                ItemName = StringLoader.Get("consumables.potion_health_medium.name"),
                Description = StringLoader.Get("consumables.potion_health_medium.description"),
                Rarity = ItemRarity.Uncommon,
                MaxStack = 5,
                BaseValue = 25,
                HealAmount = 50f
            });

            Register(new ConsumableData
            {
                Id = "potion_health_large",
                ItemName = StringLoader.Get("consumables.potion_health_large.name"),
                Description = StringLoader.Get("consumables.potion_health_large.description"),
                Rarity = ItemRarity.Rare,
                MaxStack = 5,
                BaseValue = 50,
                HealAmount = 100f
            });

            // --- Battery Packs ---
            Register(new ConsumableData
            {
                Id = "potion_mana_small",
                ItemName = StringLoader.Get("consumables.potion_mana_small.name"),
                Description = StringLoader.Get("consumables.potion_mana_small.description"),
                Rarity = ItemRarity.Common,
                MaxStack = 5,
                BaseValue = 10,
                ManaRestoreAmount = 15f
            });

            Register(new ConsumableData
            {
                Id = "potion_mana_medium",
                ItemName = StringLoader.Get("consumables.potion_mana_medium.name"),
                Description = StringLoader.Get("consumables.potion_mana_medium.description"),
                Rarity = ItemRarity.Uncommon,
                MaxStack = 5,
                BaseValue = 25,
                ManaRestoreAmount = 30f
            });

            Register(new ConsumableData
            {
                Id = "potion_mana_large",
                ItemName = StringLoader.Get("consumables.potion_mana_large.name"),
                Description = StringLoader.Get("consumables.potion_mana_large.description"),
                Rarity = ItemRarity.Rare,
                MaxStack = 5,
                BaseValue = 50,
                ManaRestoreAmount = 60f
            });

            // --- Buff Modules ---
            Register(new ConsumableData
            {
                Id = "elixir_fortitude",
                ItemName = StringLoader.Get("consumables.elixir_fortitude.name"),
                Description = StringLoader.Get("consumables.elixir_fortitude.description"),
                Rarity = ItemRarity.Rare,
                MaxStack = 5,
                BaseValue = 40,
                BuffId = "buff_fortitude",
                BuffDuration = 30f
            });

            Register(new ConsumableData
            {
                Id = "overclock_injector",
                ItemName = StringLoader.Get("consumables.overclock_injector.name"),
                Description = StringLoader.Get("consumables.overclock_injector.description"),
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
