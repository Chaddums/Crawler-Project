using System.Collections.Generic;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    [CreateAssetMenu(fileName = "NewLootTable", menuName = "DCC/Loot Table")]
    public class LootTableData : ScriptableObject
    {
        [System.Serializable]
        public struct LootEntry
        {
            public ItemData Item;
            public float Weight;
            public int MinQuantity;
            public int MaxQuantity;
        }

        public string TableName;

        [Header("Entries")]
        public List<LootEntry> Entries = new();

        [Header("Drop Settings")]
        [Tooltip("Minimum number of drop rolls when this table is resolved.")]
        public int MinDrops = 1;

        [Tooltip("Maximum number of drop rolls when this table is resolved.")]
        public int MaxDrops = 3;

        [Tooltip("Weight for rolling nothing on a single drop attempt.")]
        public float NothingWeight = 1f;
    }
}
