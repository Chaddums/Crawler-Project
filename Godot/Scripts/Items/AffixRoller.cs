using System;
using System.Collections.Generic;
using System.Linq;

namespace JunkbotArena
{
    public static class AffixRoller
    {
        private static readonly Random _rng = new();

        /// <summary>
        /// Roll affixes for an equipment item based on rarity.
        /// Common=0 affixes, Uncommon=1, Rare=2, Epic=3.
        /// </summary>
        public static List<RolledAffix> RollAffixes(EquipmentData equipment, ItemRarity rarity)
        {
            int affixCount = rarity switch
            {
                ItemRarity.Common => 0,
                ItemRarity.Uncommon => 1,
                ItemRarity.Rare => 2,
                ItemRarity.Epic => 3,
                ItemRarity.Legendary => 3,
                ItemRarity.Absurd => 3,
                _ => 0
            };

            var rolled = new List<RolledAffix>();
            int prefixCount = 0;
            int suffixCount = 0;
            var usedStats = new HashSet<StatType>();

            for (int i = 0; i < affixCount; i++)
            {
                // Determine which affix types are still available
                bool canPrefix = prefixCount < equipment.MaxPrefixes;
                bool canSuffix = suffixCount < equipment.MaxSuffixes;

                if (!canPrefix && !canSuffix) break;

                AffixType targetType;
                if (canPrefix && canSuffix)
                    targetType = _rng.Next(2) == 0 ? AffixType.Prefix : AffixType.Suffix;
                else
                    targetType = canPrefix ? AffixType.Prefix : AffixType.Suffix;

                var pool = AffixRegistry.GetEligibleAffixes(targetType, equipment.ItemLevel)
                    .Where(a => !usedStats.Contains(a.Stat))
                    .ToList();

                if (pool.Count == 0)
                {
                    // Try the other type
                    targetType = targetType == AffixType.Prefix ? AffixType.Suffix : AffixType.Prefix;
                    bool canOther = targetType == AffixType.Prefix ? canPrefix : canSuffix;
                    if (!canOther) continue;

                    pool = AffixRegistry.GetEligibleAffixes(targetType, equipment.ItemLevel)
                        .Where(a => !usedStats.Contains(a.Stat))
                        .ToList();

                    if (pool.Count == 0) break;
                }

                var affix = WeightedPick(pool);
                float value = RollValue(affix.MinValue, affix.MaxValue);

                rolled.Add(new RolledAffix(affix, value));
                usedStats.Add(affix.Stat);

                if (targetType == AffixType.Prefix) prefixCount++;
                else suffixCount++;
            }

            return rolled;
        }

        private static AffixData WeightedPick(List<AffixData> pool)
        {
            int totalWeight = pool.Sum(a => a.Weight);
            int roll = _rng.Next(totalWeight);
            int cumulative = 0;

            foreach (var affix in pool)
            {
                cumulative += affix.Weight;
                if (roll < cumulative)
                    return affix;
            }

            return pool[^1];
        }

        private static float RollValue(float min, float max)
        {
            return min + (float)_rng.NextDouble() * (max - min);
        }
    }

    public class RolledAffix
    {
        public AffixData Data;
        public float RolledValue;

        public RolledAffix(AffixData data, float value)
        {
            Data = data;
            RolledValue = value;
        }

        public StatModifier ToModifier(object source)
        {
            return new StatModifier(Data.Stat, Data.ModType, RolledValue, source);
        }
    }
}
