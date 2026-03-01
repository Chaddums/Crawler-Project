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
                (EquipmentSlot.MainHand, "sword", StatType.Strength, 5f),
                (EquipmentSlot.MainHand, "staff", StatType.Intelligence, 5f),
                (EquipmentSlot.MainHand, "dagger", StatType.Dexterity, 5f),
                (EquipmentSlot.OffHand, "shield", StatType.Armor, 3f),
                (EquipmentSlot.Head, "helmet", StatType.Armor, 2f),
                (EquipmentSlot.Chest, "chestplate", StatType.Armor, 4f),
                (EquipmentSlot.Chest, "robe", StatType.MaxMana, 20f),
                (EquipmentSlot.Legs, "greaves", StatType.Armor, 3f),
                (EquipmentSlot.Feet, "boots", StatType.MoveSpeed, 0.5f),
                (EquipmentSlot.Hands, "gauntlets", StatType.AttackSpeed, 0.05f),
                (EquipmentSlot.Amulet, "amulet", StatType.MaxHealth, 15f),
                (EquipmentSlot.Ring1, "ring", StatType.CritChance, 0.03f),
                (EquipmentSlot.Back, "cloak", StatType.CooldownReduction, 0.05f),
            };

            foreach (var (slot, key, stat, value) in templates)
            {
                var id = $"base_{key}";
                var displayName = StringLoader.Get($"equipment.{key}");
                var equip = new EquipmentData(id, displayName, ItemRarity.Common, slot, 1);
                equip.AddBaseStat(stat, ModifierType.Flat, value);
                _equipment.Add(equip);
                ItemRegistry.Register(equip);
            }

            GD.Print($"[BaseItemPool] Initialized {_equipment.Count} equipment templates");
        }
    }
}
