using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    public class EnemyData
    {
        public string Id;
        public string Name;
        public EnemyType Type;
        public float Health;
        public float Armor;
        public float MoveSpeed;
        public int ResourceValue;        // Resources dropped on death
        public bool IsFlying;         // Ignores maze
        public Color TintColor;
        public float Scale;
    }

    public static class EnemyRegistry
    {
        private static readonly Dictionary<EnemyType, EnemyData> _enemies = new();
        private static bool _initialized;

        public static EnemyData Get(EnemyType type)
        {
            EnsureInit();
            return _enemies.TryGetValue(type, out var data) ? data : null;
        }

        private static void EnsureInit()
        {
            if (_initialized) return;
            _initialized = true;

            Register(new EnemyData {
                Id = "scrap_rat", Name = "Scrap Rat",
                Type = EnemyType.ScrapRat,
                Health = 20, Armor = 0, MoveSpeed = 4f,
                ResourceValue = 3, IsFlying = false,
                TintColor = new Color(0.5f, 0.4f, 0.3f), Scale = 0.5f
            });

            Register(new EnemyData {
                Id = "wire_worm", Name = "Wire Worm",
                Type = EnemyType.WireWorm,
                Health = 40, Armor = 5, MoveSpeed = 2.5f,
                ResourceValue = 5, IsFlying = false,
                TintColor = new Color(0.3f, 0.5f, 0.3f), Scale = 0.6f
            });

            Register(new EnemyData {
                Id = "rust_hulk", Name = "Rust Hulk",
                Type = EnemyType.RustHulk,
                Health = 150, Armor = 20, MoveSpeed = 1.2f,
                ResourceValue = 12, IsFlying = false,
                TintColor = new Color(0.6f, 0.3f, 0.2f), Scale = 1f
            });

            Register(new EnemyData {
                Id = "spark_drone", Name = "Spark Drone",
                Type = EnemyType.SparkDrone,
                Health = 25, Armor = 0, MoveSpeed = 5f,
                ResourceValue = 8, IsFlying = true,
                TintColor = new Color(0.6f, 0.6f, 0.9f), Scale = 0.4f
            });

            Register(new EnemyData {
                Id = "scrap_thief", Name = "Scrap Thief",
                Type = EnemyType.ScrapThief,
                Health = 35, Armor = 0, MoveSpeed = 3.5f,
                ResourceValue = 2, IsFlying = false,
                TintColor = new Color(0.4f, 0.4f, 0.2f), Scale = 0.55f
            });

            Register(new EnemyData {
                Id = "shield_bearer", Name = "Shield Bearer",
                Type = EnemyType.ShieldBearer,
                Health = 80, Armor = 15, MoveSpeed = 1.8f,
                ResourceValue = 10, IsFlying = false,
                TintColor = new Color(0.5f, 0.5f, 0.6f), Scale = 0.8f
            });

            Register(new EnemyData {
                Id = "bomber", Name = "Bomber",
                Type = EnemyType.Bomber,
                Health = 30, Armor = 0, MoveSpeed = 2.5f,
                ResourceValue = 6, IsFlying = false,
                TintColor = new Color(0.8f, 0.3f, 0.1f), Scale = 0.7f
            });

            Register(new EnemyData {
                Id = "fabricator", Name = "Fabricator",
                Type = EnemyType.Fabricator,
                Health = 60, Armor = 10, MoveSpeed = 1.5f,
                ResourceValue = 15, IsFlying = false,
                TintColor = new Color(0.4f, 0.6f, 0.4f), Scale = 0.75f
            });
        }

        private static void Register(EnemyData data)
        {
            _enemies[data.Type] = data;
        }
    }
}
