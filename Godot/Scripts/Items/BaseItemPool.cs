using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Shared pool of base equipment templates used by loot tables and LootBoxFactory.
    /// Must be initialized after ItemRegistry and before EnemyRegistry.
    /// </summary>
    public static class BaseItemPool
    {
        private static readonly List<EquipmentData> _equipment = new();
        private static bool _initialized;

        public static IReadOnlyList<EquipmentData> Equipment => _equipment;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            var templates = new[]
            {
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

            foreach (var (slot, name, stat, value) in templates)
            {
                var id = $"base_{name.ToLower()}";
                var equip = new EquipmentData(id, name, ItemRarity.Common, slot, 1);
                equip.AddBaseStat(stat, ModifierType.Flat, value);
                _equipment.Add(equip);
                ItemRegistry.Register(equip);
            }

            GD.Print($"[BaseItemPool] Initialized {_equipment.Count} equipment templates");
        }
    }
}
