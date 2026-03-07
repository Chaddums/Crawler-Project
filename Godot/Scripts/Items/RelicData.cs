using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// A unique one-of-a-kind relic item with a special passive ability.
    /// Found only in Relic Caches — unique loot boxes with AXIS narration.
    /// Each relic has absurd lore and a powerful (often weird) stat bonus.
    /// </summary>
    public partial class RelicData : EquipmentData
    {
        /// <summary>Short flavor text shown in the opening ceremony.</summary>
        public string FlavorText { get; set; } = "";

        /// <summary>AXIS's commentary when this relic is revealed.</summary>
        public string AxisQuote { get; set; } = "";

        /// <summary>The relic's unique visual color for the opening ceremony.</summary>
        public Color GlowColor { get; set; } = new Color(1f, 0.5f, 0f);

        /// <summary>Unique tag for preventing duplicates in a run.</summary>
        public string RelicTag { get; set; } = "";

        public RelicData() { Rarity = ItemRarity.Absurd; }

        public RelicData(string id, string name, EquipmentSlot slot,
            string description, string flavor, string axisQuote,
            List<(StatType stat, ModifierType mod, float value)> bonuses,
            Color glowColor)
            : base(id, name, ItemRarity.Absurd, slot, 1)
        {
            Description = description;
            FlavorText = flavor;
            AxisQuote = axisQuote;
            GlowColor = glowColor;
            RelicTag = id;
            Rarity = ItemRarity.Absurd;
            MaxPrefixes = 0;
            MaxSuffixes = 0;

            foreach (var (stat, mod, value) in bonuses)
                AddBaseStat(stat, mod, value);
        }
    }
}
