using System.Collections.Generic;
using Godot;

namespace DungeonCrawlerCarl
{
    [GlobalClass]
    public partial class EquipmentData : ItemData
    {
        [Export] public EquipmentSlot Slot { get; set; }
        [Export] public int ItemLevel { get; set; } = 1;

        // Base stats baked into this equipment (before affixes)
        public List<StatModifier> BaseStatBonuses { get; set; } = new();

        // How many affixes can roll on this item by rarity
        public int MaxPrefixes { get; set; } = Constants.MAX_PREFIXES;
        public int MaxSuffixes { get; set; } = Constants.MAX_SUFFIXES;

        public EquipmentData() { Type = ItemType.Equipment; }

        public EquipmentData(string id, string name, ItemRarity rarity, EquipmentSlot slot, int itemLevel)
            : base(id, name, rarity, ItemType.Equipment)
        {
            Slot = slot;
            ItemLevel = itemLevel;
        }

        public void AddBaseStat(StatType stat, ModifierType mod, float value)
        {
            BaseStatBonuses.Add(new StatModifier(stat, mod, value));
        }
    }
}
