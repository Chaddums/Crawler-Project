using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Rarity tiers for salvage cores.
    /// </summary>
    public enum SalvageCoreRarity
    {
        Rare,
        Epic,
        Legendary
    }

    /// <summary>
    /// Defines a salvage core that can be socketed into a CoreSocket node
    /// on the passive tree. Cores range from stat boosts to build-defining
    /// effects like cross-class perk access or ability amplification.
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
        /// For Amplifier Cores: the ability ID that gets +3 levels.
        /// </summary>
        public string AmplifyAbilityId { get; set; } = "";

        /// <summary>
        /// For Cross-Wired Cores: the perk from another class tree this core grants.
        /// </summary>
        public string GrantsPerkId { get; set; } = "";
    }
}
