using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Resolves LootTableData into a list of ItemInstances using weighted random.
    /// </summary>
    public static class LootTableResolver
    {
        private static readonly Random _rng = new();

        public static List<ItemInstance> Resolve(LootTableData table)
        {
            var results = new List<ItemInstance>();

            if (table == null || table.Entries.Count == 0)
                return results;

            int dropCount = _rng.Next(table.MinDrops, table.MaxDrops + 1);

            for (int i = 0; i < dropCount; i++)
            {
                var entry = WeightedPick(table.Entries);
                if (entry?.Item == null) continue;

                int count = _rng.Next(entry.MinCount, entry.MaxCount + 1);

                // Roll rarity
                var rarity = RollRarity();

                var instance = new ItemInstance(entry.Item, rarity);
                instance.StackCount = count;
                results.Add(instance);
            }

            return results;
        }

        private static LootEntry WeightedPick(List<LootEntry> entries)
        {
            int totalWeight = entries.Sum(e => e.Weight);
            if (totalWeight <= 0) return entries.FirstOrDefault();

            int roll = _rng.Next(totalWeight);
            int cumulative = 0;

            foreach (var entry in entries)
            {
                cumulative += entry.Weight;
                if (roll < cumulative)
                    return entry;
            }

            return entries[^1];
        }

        /// <summary>
        /// Public rarity roll for external reward systems.
        /// </summary>
        public static ItemRarity RollRarityPublic() => RollRarity();

        /// <summary>
        /// Roll item rarity. Weighted toward common.
        /// </summary>
        private static ItemRarity RollRarity()
        {
            int roll = _rng.Next(100);
            return roll switch
            {
                < 50 => ItemRarity.Common,
                < 75 => ItemRarity.Uncommon,
                < 90 => ItemRarity.Rare,
                < 97 => ItemRarity.Epic,
                _ => ItemRarity.Legendary
            };
        }
    }
}
