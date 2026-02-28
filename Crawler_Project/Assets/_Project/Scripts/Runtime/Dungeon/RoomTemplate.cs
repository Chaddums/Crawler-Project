using UnityEngine;

namespace DungeonCrawlerCarl
{
    [System.Serializable]
    public class RoomTemplate
    {
        [Tooltip("The functional type of this room.")]
        public RoomType Type = RoomType.Combat;

        [Tooltip("Prefab to instantiate for this room layout.")]
        public GameObject Prefab;

        [Tooltip("Relative probability weight used during random room selection.")]
        [Range(0f, 10f)]
        public float Weight = 1f;

        [Tooltip("Minimum number of enemies spawned in this room (combat rooms).")]
        [Min(0)]
        public int MinEnemies;

        [Tooltip("Maximum number of enemies spawned in this room (combat rooms).")]
        [Min(0)]
        public int MaxEnemies = 3;

        [Tooltip("Whether this room contains a treasure chest or loot spawn.")]
        public bool HasTreasure;
    }
}
