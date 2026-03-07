using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Rarity tiers for grafts (harvested biological components).
    /// </summary>
    public enum SalvageCoreRarity
    {
        Rare,
        Epic,
        Legendary
    }

    /// <summary>
    /// Defines a graft — a harvested organ, parasite, or living tissue that
    /// can be bolted into a GraftSocket node on the passive tree. Grafts range
    /// from stat-boosting organs to build-defining parasites and spliced tissue
    /// that grant cross-class perks or amplify specific abilities.
    /// </summary>
    public class SalvageCoreData
    {
        public string Id { get; set; }
        public string CoreName { get; set; }
        public string Description { get; set; }
        public SalvageCoreRarity Rarity { get; set; }
        public Texture2D Icon { get; set; }

        /// <summary>
        /// Stat modifiers applied when socketed. Empty for perk-style cores.
        /// </summary>
        public List<StatModifier> StatBonuses { get; set; } = new();

        /// <summary>
        /// Perk ID activated when socketed. Lets cores grant perk effects
        /// like lifesteal, element conversion, etc.
        /// </summary>
        public string PerkId { get; set; } = "";

        /// <summary>
        /// For Amplifier Parasites: the ability ID that gets +3 levels.
        /// </summary>
        public string AmplifyAbilityId { get; set; } = "";

        /// <summary>
        /// For Spliced Organs: the perk from another class tree this graft grants.
        /// </summary>
        public string GrantsPerkId { get; set; } = "";
    }
}
