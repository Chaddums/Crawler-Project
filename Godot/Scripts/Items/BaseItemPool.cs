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
                // Guns
                (EquipmentSlot.MainHand, "pistol", StatType.Dexterity, 4f),
                (EquipmentSlot.MainHand, "rifle", StatType.Strength, 6f),
                (EquipmentSlot.MainHand, "shotgun", StatType.Strength, 7f),
                (EquipmentSlot.MainHand, "launcher", StatType.Intelligence, 8f),
                (EquipmentSlot.MainHand, "repeater", StatType.Dexterity, 5f),
                // AoE melee weapon
                (EquipmentSlot.MainHand, "blade_ring", StatType.Strength, 4f),
            };

            foreach (var (slot, key, stat, value) in templates)
            {
                var id = $"base_{key}";
                var displayName = StringLoader.Get($"equipment.{key}");
                var equip = new EquipmentData(id, displayName, ItemRarity.Common, slot, 1);
                equip.AddBaseStat(stat, ModifierType.Flat, value);

                // Tag MainHand items with their weapon type
                if (slot == EquipmentSlot.MainHand)
                {
                    equip.WeaponType = key == "blade_ring"
                        ? WeaponType.BladeRing
                        : WeaponType.Gun;
                }

                _equipment.Add(equip);
                ItemRegistry.Register(equip);
            }

            GD.Print($"[BaseItemPool] Initialized {_equipment.Count} equipment templates");
        }
    }
}
