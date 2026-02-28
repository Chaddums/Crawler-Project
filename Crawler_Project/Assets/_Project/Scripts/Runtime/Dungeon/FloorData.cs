using System.Collections.Generic;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    [CreateAssetMenu(fileName = "NewFloorData", menuName = "DCC/Floor Data")]
    public class FloorData : ScriptableObject
    {
        [Header("Identity")]
        public int FloorNumber = 1;
        public string FloorName = "Unnamed Floor";
        [TextArea(2, 4)]
        public string FloorDescription;
        [TextArea(3, 8)]
        public string AIAnnouncement;
        public Sprite FloorIcon;

        [Header("Generation")]
        [Tooltip("If true, rooms are procedurally generated. If false, use hand-placed rooms in scene.")]
        public bool UseProceduralGeneration = true;
        [Min(1)]
        public int MinRooms = 5;
        [Min(1)]
        public int MaxRooms = 10;
        public List<RoomTemplate> RoomTemplates = new();

        [Header("Enemies")]
        public List<EnemyData> EnemyPool = new();

        [Header("Loot")]
        public List<LootTableData> FloorLootTables = new();

        [Header("Audio")]
        public AudioClip AmbientMusic;
        public AudioClip BossMusic;

        [Header("Difficulty")]
        [Tooltip("Multiplier applied to enemy stats on this floor.")]
        [Min(0.1f)]
        public float DifficultyMultiplier = 1f;
        [Min(1)]
        public int RecommendedLevel = 1;

        [Header("Commentary")]
        public List<CommentaryEntry> FloorCommentary = new();

        private void OnValidate()
        {
            if (MaxRooms < MinRooms)
                MaxRooms = MinRooms;
        }
    }
}
