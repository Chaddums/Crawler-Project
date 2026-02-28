using System.Collections.Generic;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Configuration for a dungeon floor: room count, difficulty, enemy pool.
    /// </summary>
    public class FloorData
    {
        public int FloorNumber { get; set; }
        public int MinRooms { get; set; } = 5;
        public int MaxRooms { get; set; } = 8;
        public float DifficultyMultiplier { get; set; } = 1f;
        public List<string> EnemyPool { get; set; } = new();
        public int MinEnemiesPerRoom { get; set; } = 2;
        public int MaxEnemiesPerRoom { get; set; } = 4;
        public string BossEnemyId { get; set; }

        public FloorData() { }

        public FloorData(int floor, float difficulty, List<string> enemies, string boss = null)
        {
            FloorNumber = floor;
            DifficultyMultiplier = difficulty;
            EnemyPool = enemies;
            BossEnemyId = boss;
        }
    }
}
