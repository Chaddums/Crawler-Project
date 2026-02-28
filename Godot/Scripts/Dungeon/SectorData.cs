using System.Collections.Generic;

namespace JunkbotArena
{
    /// <summary>
    /// Configuration for an arena sector: room count, difficulty, enemy pool.
    /// </summary>
    public class SectorData
    {
        public int SectorNumber { get; set; }
        public int MinRooms { get; set; } = 5;
        public int MaxRooms { get; set; } = 8;
        public float DifficultyMultiplier { get; set; } = 1f;
        public List<string> EnemyPool { get; set; } = new();
        public int MinEnemiesPerRoom { get; set; } = 2;
        public int MaxEnemiesPerRoom { get; set; } = 4;
        public string BossEnemyId { get; set; }
        public float TimeLimit { get; set; } = 300f;

        public SectorData() { }

        public SectorData(int floor, float difficulty, List<string> enemies, string boss = null)
        {
            SectorNumber = floor;
            DifficultyMultiplier = difficulty;
            EnemyPool = enemies;
            BossEnemyId = boss;
        }
    }
}
