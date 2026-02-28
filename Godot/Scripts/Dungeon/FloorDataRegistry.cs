using System.Collections.Generic;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Static registry of floor configurations for floors 1-5.
    /// </summary>
    public static class FloorDataRegistry
    {
        private static readonly Dictionary<int, FloorData> _floors = new();
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            _floors[1] = new FloorData(1, 1.0f, new() { "grub", "crawler_rat" })
            {
                MinRooms = 5, MaxRooms = 6,
                MinEnemiesPerRoom = 2, MaxEnemiesPerRoom = 3,
                BossEnemyId = "goblin_overseer"
            };

            _floors[2] = new FloorData(2, 1.3f, new() { "grub", "crawler_rat", "crawler_rat" })
            {
                MinRooms = 5, MaxRooms = 7,
                MinEnemiesPerRoom = 2, MaxEnemiesPerRoom = 4,
                BossEnemyId = "goblin_overseer"
            };

            _floors[3] = new FloorData(3, 1.6f, new() { "crawler_rat", "crawler_rat", "grub" })
            {
                MinRooms = 6, MaxRooms = 8,
                MinEnemiesPerRoom = 3, MaxEnemiesPerRoom = 5,
                BossEnemyId = "mimic_king"
            };

            _floors[4] = new FloorData(4, 2.0f, new() { "crawler_rat", "mimic" })
            {
                MinRooms = 6, MaxRooms = 8,
                MinEnemiesPerRoom = 3, MaxEnemiesPerRoom = 5,
                BossEnemyId = "mimic_king"
            };

            _floors[5] = new FloorData(5, 2.5f, new() { "crawler_rat", "mimic", "mimic" })
            {
                MinRooms = 7, MaxRooms = 9,
                MinEnemiesPerRoom = 3, MaxEnemiesPerRoom = 6,
                BossEnemyId = "announcer_champion"
            };

            Godot.GD.Print($"[FloorDataRegistry] Initialized {_floors.Count} floor configs");
        }

        public static FloorData GetFloor(int floorNumber)
        {
            if (_floors.TryGetValue(floorNumber, out var data))
                return data;

            // Generate a scaled floor for beyond floor 5
            return new FloorData(floorNumber, 1f + (floorNumber - 1) * 0.5f,
                new() { "crawler_rat", "mimic" })
            {
                MinRooms = 7, MaxRooms = 10,
                MinEnemiesPerRoom = 3, MaxEnemiesPerRoom = 6,
                BossEnemyId = "announcer_champion"
            };
        }
    }
}
