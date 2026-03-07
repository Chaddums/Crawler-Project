using System;
using System.Collections.Generic;

namespace JunkbotArena
{
    public class ItemInstance
    {
        public string UniqueId { get; private set; }
        public ItemData BaseData { get; private set; }
        public ItemRarity Rarity { get; private set; }
        public int StackCount { get; set; } = 1;

        // Only for equipment
        public List<RolledAffix> Affixes { get; private set; } = new();

        public ItemInstance(ItemData data, ItemRarity? overrideRarity = null)
        {
            UniqueId = Guid.NewGuid().ToString("N")[..8];
            BaseData = data;
            Rarity = overrideRarity ?? data.Rarity;

            if (data is EquipmentData equipment)
            {
                Affixes = AffixRoller.RollAffixes(equipment, Rarity);
            }
        }

        public List<StatModifier> GetAllModifiers()
        {
            var mods = new List<StatModifier>();

            // Base equipment stats
            if (BaseData is EquipmentData equipment)
            {
                foreach (var baseMod in equipment.BaseStatBonuses)
                {
                    mods.Add(new StatModifier(baseMod.StatType, baseMod.ModType, baseMod.Value, this));
                }
            }

            // Affix stats
            foreach (var affix in Affixes)
            {
                mods.Add(affix.ToModifier(this));
            }

            return mods;
        }

        public string GetDisplayName()
        {
            if (BaseData is not EquipmentData || Affixes.Count == 0)
                return BaseData.ItemName;

            string prefix = "";
            string suffix = "";

            foreach (var affix in Affixes)
            {
                if (affix.Data.Type == AffixType.Prefix && string.IsNullOrEmpty(prefix))
                    prefix = affix.Data.AffixName + " ";
                else if (affix.Data.Type == AffixType.Suffix && string.IsNullOrEmpty(suffix))
                    suffix = " " + affix.Data.AffixName;
            }

            return $"{prefix}{BaseData.ItemName}{suffix}";
        }
    }
}
