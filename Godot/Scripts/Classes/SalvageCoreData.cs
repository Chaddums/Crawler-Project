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
        Legendary,
        Mythic  // Ultra-rare chase grafts — build-defining, astronomically low drop rate
    }

    /// <summary>
    /// Where a graft can drop from. Used by the World Loot Table UI
    /// to show players what to farm.
    /// </summary>
    public class GraftDropSource
    {
        /// <summary>Minimum sector number (1-5) where this can drop. 0 = any.</summary>
        public int MinSector { get; set; }
        /// <summary>Minimum ascension rank required. 0 = any.</summary>
        public int MinAscension { get; set; }
        /// <summary>Specific boss ID that drops this. Empty = any source.</summary>
        public string BossId { get; set; } = "";
        /// <summary>Minimum enemy tier required (Normal, Elite, MiniBoss, Boss).</summary>
        public EnemyTier MinEnemyTier { get; set; } = EnemyTier.Normal;
        /// <summary>Base drop weight (higher = more common within its rarity tier).</summary>
        public float Weight { get; set; } = 1f;
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

        /// <summary>
        /// Where this graft can drop. Null = unrestricted (any Gold+ loot box).
        /// </summary>
        public GraftDropSource DropSource { get; set; }
    }
}
