using System.Collections.Generic;
using Godot;

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
                TimeLimit = 300f,
                WaveChance = 0f, MaxWaves = 1,
                // Industrial theme
                ThemeName = "Industrial",
                FloorTint = new Color(0.18f, 0.17f, 0.15f),
                WallTint = new Color(0.32f, 0.29f, 0.25f),
                AccentColor = new Color(0.85f, 0.55f, 0.15f),
                TorchTint = new Color(0.95f, 0.7f, 0.35f),
            };

            _sectors[2] = new SectorData(2, 1.3f, new() { "wire_worm", "scrap_rat", "scrap_rat" })
            {
                MinRooms = 10, MaxRooms = 12,
                MinEnemiesPerRoom = 3, MaxEnemiesPerRoom = 5,
                BossEnemyId = "corrupted_sentry",
                TimeLimit = 300f,
                WaveChance = 0.3f, MaxWaves = 2,
                AllowedHazards = new() { HazardType.PoisonPool },
                // Toxic theme
                ThemeName = "Toxic",
                FloorTint = new Color(0.12f, 0.18f, 0.1f),
                WallTint = new Color(0.22f, 0.28f, 0.18f),
                AccentColor = new Color(0.4f, 0.85f, 0.15f),
                TorchTint = new Color(0.3f, 0.9f, 0.2f),
            };

            _sectors[3] = new SectorData(3, 1.6f, new() { "scrap_rat", "scrap_rat", "wire_worm" })
            {
                MinRooms = 12, MaxRooms = 15,
                MinEnemiesPerRoom = 4, MaxEnemiesPerRoom = 6,
                BossEnemyId = "scrap_hydra",
                TimeLimit = 300f,
                WaveChance = 0.5f, MaxWaves = 3,
                AllowedHazards = new() { HazardType.PoisonPool, HazardType.ElectricPlate },
                // Military theme
                ThemeName = "Military",
                FloorTint = new Color(0.16f, 0.15f, 0.18f),
                WallTint = new Color(0.25f, 0.2f, 0.22f),
                AccentColor = new Color(0.85f, 0.2f, 0.15f),
                TorchTint = new Color(0.9f, 0.45f, 0.2f),
            };

            _sectors[4] = new SectorData(4, 2.0f, new() { "scrap_rat", "decoy_unit" })
            {
                MinRooms = 12, MaxRooms = 15,
                MinEnemiesPerRoom = 4, MaxEnemiesPerRoom = 7,
                BossEnemyId = "scrap_hydra",
                TimeLimit = 300f,
                WaveChance = 0.5f, MaxWaves = 3,
                AllowedHazards = new() { HazardType.PoisonPool, HazardType.ElectricPlate, HazardType.LavaCrack },
                // Lab theme
                ThemeName = "Lab",
                FloorTint = new Color(0.15f, 0.17f, 0.22f),
                WallTint = new Color(0.25f, 0.27f, 0.32f),
                AccentColor = new Color(0.2f, 0.5f, 0.95f),
                TorchTint = new Color(0.5f, 0.7f, 1.0f),
            };

            _sectors[5] = new SectorData(5, 2.5f, new() { "scrap_rat", "decoy_unit", "decoy_unit" })
            {
                MinRooms = 12, MaxRooms = 15,
                MinEnemiesPerRoom = 5, MaxEnemiesPerRoom = 8,
                BossEnemyId = "axis_avatar",
                TimeLimit = 300f,
                WaveChance = 0.5f, MaxWaves = 3,
                AllowedHazards = new() { HazardType.PoisonPool, HazardType.ElectricPlate, HazardType.LavaCrack },
                // Core theme
                ThemeName = "Core",
                FloorTint = new Color(0.15f, 0.1f, 0.2f),
                WallTint = new Color(0.2f, 0.12f, 0.28f),
                AccentColor = new Color(0.8f, 0.2f, 0.85f),
                TorchTint = new Color(0.6f, 0.25f, 0.9f),
            };

            GD.Print($"[SectorDataRegistry] Initialized {_sectors.Count} sector configs");
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
