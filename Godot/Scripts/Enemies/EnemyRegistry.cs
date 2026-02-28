using System.Collections.Generic;
using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Pre-built Floor 1 enemy definitions.
    /// </summary>
    public static class EnemyRegistry
    {
        private static readonly Dictionary<string, EnemyData> _enemies = new();
        private static bool _initialized;

        public static IReadOnlyDictionary<string, EnemyData> Enemies => _enemies;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            BuildTrainingDummy();
            BuildCrawlerRat();
            BuildMimic();
            BuildGrub();

            GD.Print($"[EnemyRegistry] Initialized {_enemies.Count} enemy types");
        }

        public static EnemyData GetEnemy(string id)
        {
            return _enemies.TryGetValue(id, out var data) ? data : null;
        }

        private static void BuildTrainingDummy()
        {
            var e = new EnemyData("training_dummy", "Training Dummy", EnemyTier.Normal, 50, 0, 0, 5)
            {
                AttackRange = 0,
                AttackCooldown = 999f,
                AggroRange = 0,
                MeshColor = new Color(0.6f, 0.6f, 0.3f)
            };

            // Dummies have a basic loot table
            e.LootTable = new LootTableData { Id = "loot_dummy", MinDrops = 0, MaxDrops = 1 };

            _enemies[e.Id] = e;
        }

        private static void BuildCrawlerRat()
        {
            var e = new EnemyData("crawler_rat", "Crawler Rat", EnemyTier.Normal, 25, 4, 4.5f, 15)
            {
                AttackRange = 1.2f,
                AttackCooldown = 1.0f,
                AggroRange = 6f,
                Armor = 1,
                MeshColor = new Color(0.5f, 0.35f, 0.25f)
            };

            e.LootTable = new LootTableData { Id = "loot_rat", MinDrops = 0, MaxDrops = 1 };

            _enemies[e.Id] = e;
        }

        private static void BuildMimic()
        {
            var e = new EnemyData("mimic", "Mimic", EnemyTier.Elite, 80, 12, 2f, 50)
            {
                AttackRange = 1.5f,
                AttackCooldown = 2.0f,
                AggroRange = 4f,
                Armor = 5,
                MeshColor = new Color(0.8f, 0.7f, 0.2f)
            };

            e.LootTable = new LootTableData { Id = "loot_mimic", MinDrops = 1, MaxDrops = 3 };

            _enemies[e.Id] = e;
        }

        private static void BuildGrub()
        {
            var e = new EnemyData("grub", "Grub", EnemyTier.Normal, 15, 2, 2f, 8)
            {
                AttackRange = 1.0f,
                AttackCooldown = 1.5f,
                AggroRange = 5f,
                Armor = 0,
                MeshColor = new Color(0.4f, 0.7f, 0.3f)
            };

            e.LootTable = new LootTableData { Id = "loot_grub", MinDrops = 0, MaxDrops = 1 };

            _enemies[e.Id] = e;
        }
    }
}
