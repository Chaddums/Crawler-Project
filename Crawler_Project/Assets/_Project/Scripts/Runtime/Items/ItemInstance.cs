using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    [System.Serializable]
    public class ItemInstance
    {
        public ItemData Data;
        public int StackCount;
        public List<StatModifier> RolledStats = new();
        public string UniqueId;

        /// <summary>
        /// Creates a new ItemInstance from the given ItemData.
        /// Generates a unique ID and rolls random stats for equipment items.
        /// </summary>
        public static ItemInstance Create(ItemData data, int stackCount = 1)
        {
            var instance = new ItemInstance
            {
                Data = data,
                StackCount = stackCount,
                UniqueId = Guid.NewGuid().ToString()
            };

            // Roll random stats if this is equipment
            if (data is EquipmentData equipData && equipData.RandomStatPool.Count > 0 && equipData.RandomStatCount > 0)
            {
                RollRandomStats(instance, equipData);
            }

            return instance;
        }

        private static void RollRandomStats(ItemInstance instance, EquipmentData equipData)
        {
            var availablePool = new List<StatRange>(equipData.RandomStatPool);
            int rollCount = Mathf.Min(equipData.RandomStatCount, availablePool.Count);

            for (int i = 0; i < rollCount; i++)
            {
                if (availablePool.Count == 0) break;

                // Pick a random stat from the remaining pool
                int index = UnityEngine.Random.Range(0, availablePool.Count);
                StatRange range = availablePool[index];
                availablePool.RemoveAt(index);

                // Roll a value within the range
                float rolledValue = UnityEngine.Random.Range(range.MinValue, range.MaxValue);

                // Round to one decimal place for cleanliness
                rolledValue = Mathf.Round(rolledValue * 10f) / 10f;

                var modifier = new StatModifier(range.Stat, range.ModType, rolledValue, instance);
                instance.RolledStats.Add(modifier);
            }
        }
    }
}
