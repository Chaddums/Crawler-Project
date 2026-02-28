using System.Collections.Generic;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    [CreateAssetMenu(fileName = "NewEquipment", menuName = "DCC/Items/Equipment")]
    public class EquipmentData : ItemData
    {
        [Header("Equipment")]
        public EquipmentSlot Slot;

        [Header("Fixed Stats")]
        public List<StatModifier> FixedStats = new();

        [Header("Random Stats")]
        [Tooltip("Number of random stats to roll when this item drops.")]
        public int RandomStatCount;

        [Tooltip("Pool of possible random stats and their value ranges.")]
        public List<StatRange> RandomStatPool = new();
    }
}
