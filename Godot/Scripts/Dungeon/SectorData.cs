using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Configuration for an arena sector: room count, difficulty, enemy pool, visual theme.
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
        public float WaveChance { get; set; } = 0f;
        public int MaxWaves { get; set; } = 1;
        public List<HazardType> AllowedHazards { get; set; } = new();

        // Room distribution — how many of the TotalRooms are guaranteed combat
        public int TotalRooms { get; set; } = 40;
        public int CombatRoomCount { get; set; } = 35;
        public int TreasureRooms { get; set; } = 1;
        public int EventRooms { get; set; } = 1;
        public int ShopRooms { get; set; } = 1;
        public int PuzzleRooms { get; set; } = 0;
        public float SafeRoomChance { get; set; } = 0.10f;
        public float MegabonkChance { get; set; } = 0f;
        public int MaxMegabonkRooms { get; set; } = 0;
        public float RareLootChance { get; set; } = 0.02f;

        // Visual theme
        public Color FloorTint { get; set; } = new Color(0.2f, 0.18f, 0.16f);
        public Color WallTint { get; set; } = new Color(0.3f, 0.28f, 0.25f);
        public Color AccentColor { get; set; } = new Color(0.85f, 0.55f, 0.15f);
        public Color TorchTint { get; set; } = new Color(0.9f, 0.7f, 0.4f);
        public string ThemeName { get; set; } = "Industrial";

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
