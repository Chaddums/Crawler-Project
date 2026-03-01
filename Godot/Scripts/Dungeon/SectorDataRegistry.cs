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
                MinRooms = 8, MaxRooms = 10,
                MinEnemiesPerRoom = 3, MaxEnemiesPerRoom = 5,
                BossEnemyId = "corrupted_sentry",
                TimeLimit = 300f, // 5:00
                WaveChance = 0f, MaxWaves = 1
            };

            _sectors[2] = new SectorData(2, 1.3f, new() { "wire_worm", "scrap_rat", "scrap_rat" })
            {
                MinRooms = 10, MaxRooms = 12,
                MinEnemiesPerRoom = 3, MaxEnemiesPerRoom = 5,
                BossEnemyId = "corrupted_sentry",
                TimeLimit = 300f, // 5:00
                WaveChance = 0.3f, MaxWaves = 2,
                AllowedHazards = new() { HazardType.PoisonPool }
            };

            _sectors[3] = new SectorData(3, 1.6f, new() { "scrap_rat", "scrap_rat", "wire_worm" })
            {
                MinRooms = 12, MaxRooms = 15,
                MinEnemiesPerRoom = 4, MaxEnemiesPerRoom = 6,
                BossEnemyId = "scrap_hydra",
                TimeLimit = 300f, // 5:00
                WaveChance = 0.5f, MaxWaves = 3,
                AllowedHazards = new() { HazardType.PoisonPool, HazardType.ElectricPlate }
            };

            _sectors[4] = new SectorData(4, 2.0f, new() { "scrap_rat", "decoy_unit" })
            {
                MinRooms = 12, MaxRooms = 15,
                MinEnemiesPerRoom = 4, MaxEnemiesPerRoom = 7,
                BossEnemyId = "scrap_hydra",
                TimeLimit = 300f, // 5:00
                WaveChance = 0.5f, MaxWaves = 3,
                AllowedHazards = new() { HazardType.PoisonPool, HazardType.ElectricPlate, HazardType.LavaCrack }
            };

            _sectors[5] = new SectorData(5, 2.5f, new() { "scrap_rat", "decoy_unit", "decoy_unit" })
            {
                MinRooms = 12, MaxRooms = 15,
                MinEnemiesPerRoom = 5, MaxEnemiesPerRoom = 8,
                BossEnemyId = "axis_avatar",
                TimeLimit = 300f, // 5:00
                WaveChance = 0.5f, MaxWaves = 3,
                AllowedHazards = new() { HazardType.PoisonPool, HazardType.ElectricPlate, HazardType.LavaCrack }
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
                MinRooms = 12, MaxRooms = 15,
                MinEnemiesPerRoom = 5, MaxEnemiesPerRoom = 8,
                BossEnemyId = "axis_avatar",
                TimeLimit = 300f,
                WaveChance = 0.5f, MaxWaves = 3,
                AllowedHazards = new() { HazardType.PoisonPool, HazardType.ElectricPlate, HazardType.LavaCrack }
            };
        }
    }
}
