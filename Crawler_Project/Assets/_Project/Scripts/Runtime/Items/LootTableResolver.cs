using System.Collections.Generic;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Static utility class that resolves a LootTableData into a list of ItemInstances.
    /// Applies luck modifiers to rare and above items during weighted selection.
    /// </summary>
    public static class LootTableResolver
    {
        /// <summary>
        /// Resolves the given loot table and returns a list of dropped item instances.
        /// </summary>
        /// <param name="table">The loot table to resolve.</param>
        /// <param name="luckModifier">Multiplier applied to weights of Rare+ items. Higher = better drops.</param>
        public static List<ItemInstance> Resolve(LootTableData table, float luckModifier = 1f)
        {
            var results = new List<ItemInstance>();

            if (table == null || table.Entries.Count == 0)
                return results;

            int dropCount = Random.Range(table.MinDrops, table.MaxDrops + 1);

            for (int i = 0; i < dropCount; i++)
            {
                var entry = RollEntry(table, luckModifier);
                if (entry.HasValue)
                {
                    var lootEntry = entry.Value;
                    int quantity = Random.Range(lootEntry.MinQuantity, lootEntry.MaxQuantity + 1);
                    quantity = Mathf.Max(1, quantity);

                    var instance = ItemInstance.Create(lootEntry.Item, quantity);
                    results.Add(instance);
                }
            }

            return results;
        }

        private static LootTableData.LootEntry? RollEntry(LootTableData table, float luckModifier)
        {
            // Calculate total weight, applying luck modifier to rare+ items
            float totalWeight = table.NothingWeight;

            foreach (var entry in table.Entries)
            {
                float weight = GetAdjustedWeight(entry, luckModifier);
                totalWeight += weight;
            }

            // Roll
            float roll = Random.Range(0f, totalWeight);

            // Check nothing first
            roll -= table.NothingWeight;
            if (roll < 0f)
                return null;

            // Walk through entries
            foreach (var entry in table.Entries)
            {
                float weight = GetAdjustedWeight(entry, luckModifier);
                roll -= weight;
                if (roll < 0f)
                    return entry;
            }

            // Fallback (should not happen with correct weights)
            return null;
        }

        private static float GetAdjustedWeight(LootTableData.LootEntry entry, float luckModifier)
        {
            if (entry.Item == null)
                return 0f;

            float weight = entry.Weight;

            // Apply luck modifier to Rare and above items
            if (entry.Item.Rarity >= ItemRarity.Rare)
            {
                weight *= luckModifier;
            }

            return Mathf.Max(0f, weight);
        }
    }
}
