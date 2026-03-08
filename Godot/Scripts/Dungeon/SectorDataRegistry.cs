using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Static registry of sector configurations for sectors 1-5.
    /// Each sector targets 40 rooms. As sectors increase, combat rooms decrease
    /// and special rooms (treasure, event, shop, megabonk) increase.
    /// </summary>
    public static class SectorDataRegistry
    {
        private static readonly Dictionary<int, SectorData> _sectors = new();
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            // Sector 1: 35 combat, 5 special — tutorial-ish, basic enemies + rust mites for swarm feel
            _sectors[1] = new SectorData(1, 1.0f, new() { "wire_worm", "wire_worm", "scrap_rat", "scrap_rat", "rust_mite" })
            {
                TotalRooms = 40, CombatRoomCount = 35,
                TreasureRooms = 0, TreasureRoomChance = 0.02f, MaxTreasureRooms = 1,
                EventRooms = 1, ShopRooms = 1, PuzzleRooms = 1,
                SafeRoomChance = 0.10f,
                MegabonkChance = 0f, MaxMegabonkRooms = 0,
                RareLootChance = 0.02f,
                MinEnemiesPerRoom = 4, MaxEnemiesPerRoom = 7,
                BossEnemyId = "corrupted_sentry",
                TimeLimit = 600f,
                WaveChance = 0.2f, MaxWaves = 2,
                ThemeName = "Industrial",
                FloorTint = new Color(0.35f, 0.33f, 0.30f),
                WallTint = new Color(0.42f, 0.38f, 0.34f),
                AccentColor = new Color(0.85f, 0.55f, 0.15f),
                TorchTint = new Color(0.95f, 0.7f, 0.35f),
            };

            // Sector 2: 33 combat, 7 special — ranged threats introduced, chargers appear
            _sectors[2] = new SectorData(2, 1.3f, new() { "scrap_rat", "scrap_rat", "rust_mite", "spark_drone", "spark_drone", "volt_sprinter" })
            {
                TotalRooms = 40, CombatRoomCount = 33,
                TreasureRooms = 0, TreasureRoomChance = 0.03f, MaxTreasureRooms = 2,
                EventRooms = 2, ShopRooms = 1, PuzzleRooms = 2,
                SafeRoomChance = 0.10f,
                MegabonkChance = 0f, MaxMegabonkRooms = 0,
                RareLootChance = 0.04f,
                MinEnemiesPerRoom = 5, MaxEnemiesPerRoom = 8,
                BossEnemyId = "rust_titan",
                TimeLimit = 600f,
                WaveChance = 0.4f, MaxWaves = 2,
                AllowedHazards = new() { HazardType.PoisonPool },
                ThemeName = "Toxic",
                FloorTint = new Color(0.25f, 0.35f, 0.22f),
                WallTint = new Color(0.30f, 0.38f, 0.28f),
                AccentColor = new Color(0.4f, 0.85f, 0.15f),
                TorchTint = new Color(0.3f, 0.9f, 0.2f),
            };

            // Sector 3: 30 combat, 10 special — elites + tanks + artillery, multi-role compositions
            _sectors[3] = new SectorData(3, 1.6f, new() { "scrap_rat", "spark_drone", "junk_lurker", "junk_lurker", "shard_lobber", "volt_sprinter", "scrap_golem" })
            {
                TotalRooms = 40, CombatRoomCount = 30,
                TreasureRooms = 3, EventRooms = 3, ShopRooms = 2, PuzzleRooms = 2,
                SafeRoomChance = 0.10f,
                MegabonkChance = 0f, MaxMegabonkRooms = 0,
                RareLootChance = 0.06f,
                MinEnemiesPerRoom = 5, MaxEnemiesPerRoom = 9,
                BossEnemyId = "scrap_hydra",
                TimeLimit = 600f,
                WaveChance = 0.6f, MaxWaves = 3,
                AllowedHazards = new() { HazardType.PoisonPool, HazardType.ElectricPlate },
                ThemeName = "Military",
                FloorTint = new Color(0.32f, 0.30f, 0.34f),
                WallTint = new Color(0.38f, 0.32f, 0.34f),
                AccentColor = new Color(0.85f, 0.2f, 0.15f),
                TorchTint = new Color(0.9f, 0.45f, 0.2f),
            };

            // Sector 4: 27 combat, 13 special — phantoms + support drones, full roster danger
            _sectors[4] = new SectorData(4, 2.0f, new() { "scrap_rat", "decoy_unit", "spark_drone", "junk_lurker", "shard_lobber", "glitch_phantom", "scrap_golem", "overclock_drone", "patch_bot" })
            {
                TotalRooms = 40, CombatRoomCount = 27,
                TreasureRooms = 4, EventRooms = 3, ShopRooms = 2, PuzzleRooms = 2,
                SafeRoomChance = 0.10f,
                MegabonkChance = 0.05f, MaxMegabonkRooms = 1,
                RareLootChance = 0.08f,
                MinEnemiesPerRoom = 6, MaxEnemiesPerRoom = 10,
                BossEnemyId = "null_warden",
                TimeLimit = 600f,
                WaveChance = 0.6f, MaxWaves = 3,
                AllowedHazards = new() { HazardType.PoisonPool, HazardType.ElectricPlate, HazardType.LavaCrack },
                ThemeName = "Lab",
                FloorTint = new Color(0.30f, 0.33f, 0.40f),
                WallTint = new Color(0.35f, 0.38f, 0.45f),
                AccentColor = new Color(0.2f, 0.5f, 0.95f),
                TorchTint = new Color(0.5f, 0.7f, 1.0f),
            };

            // Sector 5: 24 combat, 16 special — everything, max danger, chaotic compositions
            _sectors[5] = new SectorData(5, 2.5f, new() { "decoy_unit", "spark_drone", "junk_lurker", "volt_sprinter", "shard_lobber", "glitch_phantom", "scrap_golem", "overclock_drone", "patch_bot" })
            {
                TotalRooms = 40, CombatRoomCount = 24,
                TreasureRooms = 5, EventRooms = 4, ShopRooms = 3, PuzzleRooms = 2,
                SafeRoomChance = 0.10f,
                MegabonkChance = 0.05f, MaxMegabonkRooms = 2,
                RareLootChance = 0.10f,
                MinEnemiesPerRoom = 7, MaxEnemiesPerRoom = 12,
                BossEnemyId = "axis_avatar",
                TimeLimit = 600f,
                WaveChance = 0.7f, MaxWaves = 4,
                AllowedHazards = new() { HazardType.PoisonPool, HazardType.ElectricPlate, HazardType.LavaCrack },
                ThemeName = "Core",
                FloorTint = new Color(0.30f, 0.22f, 0.38f),
                WallTint = new Color(0.32f, 0.24f, 0.42f),
                AccentColor = new Color(0.8f, 0.2f, 0.85f),
                TorchTint = new Color(0.6f, 0.25f, 0.9f),
            };

            GD.Print($"[SectorDataRegistry] Initialized {_sectors.Count} sector configs");
        }

        public static SectorData GetSector(int sectorNumber)
        {
            if (_sectors.TryGetValue(sectorNumber, out var data))
                return data;

            // Generate a scaled sector for beyond sector 5 — endgame scaling
            int sector = Mathf.Min(sectorNumber, 10);
            int combatRooms = Mathf.Max(20, 35 - (sector - 1) * 3);
            return new SectorData(sectorNumber, 1f + (sectorNumber - 1) * 0.5f,
                new() { "decoy_unit", "spark_drone", "junk_lurker", "volt_sprinter", "shard_lobber", "glitch_phantom", "scrap_golem", "overclock_drone", "patch_bot" })
            {
                TotalRooms = 40, CombatRoomCount = combatRooms,
                TreasureRooms = Mathf.Min(5, 2 + sector / 2),
                EventRooms = Mathf.Min(5, 1 + sector / 2),
                ShopRooms = Mathf.Min(3, 1 + sector / 3),
                PuzzleRooms = 2,
                SafeRoomChance = 0.10f,
                MegabonkChance = sector >= 4 ? 0.05f : 0f,
                MaxMegabonkRooms = sector >= 4 ? 2 : 0,
                RareLootChance = Mathf.Min(0.15f, 0.02f + sector * 0.02f),
                MinEnemiesPerRoom = 7, MaxEnemiesPerRoom = 12,
                BossEnemyId = "axis_avatar",
                TimeLimit = 600f,
                WaveChance = 0.7f, MaxWaves = 4,
                AllowedHazards = new() { HazardType.PoisonPool, HazardType.ElectricPlate, HazardType.LavaCrack }
            };
        }
    }
}
