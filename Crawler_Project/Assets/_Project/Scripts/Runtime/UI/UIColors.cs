using UnityEngine;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Shared rarity/damage color utility. Single source of truth for rarity colors.
    /// </summary>
    public static class UIColors
    {
        public static Color GetRarityColor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Common: return Color.white;
                case ItemRarity.Uncommon: return new Color(0.12f, 1f, 0f);
                case ItemRarity.Rare: return new Color(0f, 0.44f, 1f);
                case ItemRarity.Epic: return new Color(0.64f, 0.21f, 0.93f);
                case ItemRarity.Legendary: return new Color(1f, 0.5f, 0f);
                case ItemRarity.Absurd: return new Color(1f, 0f, 1f);
                default: return Color.white;
            }
        }

        public static string GetRarityColorHex(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Common: return "#FFFFFF";
                case ItemRarity.Uncommon: return "#1EFF00";
                case ItemRarity.Rare: return "#0070FF";
                case ItemRarity.Epic: return "#A335EE";
                case ItemRarity.Legendary: return "#FF8000";
                case ItemRarity.Absurd: return "#FF00FF";
                default: return "#FFFFFF";
            }
        }
    }
}
