using System.Collections.Generic;

namespace JunkbotArena
{
    /// <summary>
    /// Static registry of sector configurations for sectors 1-5.
    /// </summary>
    public static class SectorDataRegistry
    {
        private static readonly Dictionary<int, SectorData> _sectors = new();
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            _sectors[1] = new SectorData(1, 1.0f, new() { "wire_worm", "scrap_rat" })
            {
                MinRooms = 5, MaxRooms = 6,
                MinEnemiesPerRoom = 2, MaxEnemiesPerRoom = 3,
                BossEnemyId = "corrupted_sentry",
                TimeLimit = 300f // 5:00
            };

            _sectors[2] = new SectorData(2, 1.3f, new() { "wire_worm", "scrap_rat", "scrap_rat" })
            {
                MinRooms = 5, MaxRooms = 7,
                MinEnemiesPerRoom = 2, MaxEnemiesPerRoom = 4,
                BossEnemyId = "corrupted_sentry",
                TimeLimit = 270f // 4:30
            };

            _sectors[3] = new SectorData(3, 1.6f, new() { "scrap_rat", "scrap_rat", "wire_worm" })
            {
                MinRooms = 6, MaxRooms = 8,
                MinEnemiesPerRoom = 3, MaxEnemiesPerRoom = 5,
                BossEnemyId = "scrap_hydra",
                TimeLimit = 240f // 4:00
            };

            _sectors[4] = new SectorData(4, 2.0f, new() { "scrap_rat", "decoy_unit" })
            {
                MinRooms = 6, MaxRooms = 8,
                MinEnemiesPerRoom = 3, MaxEnemiesPerRoom = 5,
                BossEnemyId = "scrap_hydra",
                TimeLimit = 210f // 3:30
            };

            _sectors[5] = new SectorData(5, 2.5f, new() { "scrap_rat", "decoy_unit", "decoy_unit" })
            {
                MinRooms = 7, MaxRooms = 9,
                MinEnemiesPerRoom = 3, MaxEnemiesPerRoom = 6,
                BossEnemyId = "axis_avatar",
                TimeLimit = 180f // 3:00
            };

            Godot.GD.Print($"[SectorDataRegistry] Initialized {_sectors.Count} sector configs");
        }

        public static SectorData GetSector(int sectorNumber)
        {
            if (_sectors.TryGetValue(sectorNumber, out var data))
                return data;

            // Generate a scaled sector for beyond sector 5
            return new SectorData(sectorNumber, 1f + (sectorNumber - 1) * 0.5f,
                new() { "scrap_rat", "decoy_unit" })
            {
                MinRooms = 7, MaxRooms = 10,
                MinEnemiesPerRoom = 3, MaxEnemiesPerRoom = 6,
                BossEnemyId = "axis_avatar",
                TimeLimit = 180f // 3:00 for all sectors 5+
            };
        }
    }
}
