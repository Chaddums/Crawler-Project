using System.Collections.Generic;
using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Static registry of all companion definitions.
    /// </summary>
    public static class CompanionRegistry
    {
        private static readonly Dictionary<string, CompanionData> _companions = new();
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            // Princess Donut — Persian cat, fast and agile
            _companions["donut"] = new CompanionData
            {
                Id = "donut",
                CompanionName = "Princess Donut",
                Description = "A former housecat turned dungeon crawler. Surprisingly deadly.",
                BaseHealth = 60f,
                BaseDamage = 8f,
                MoveSpeed = 8f,
                AttackRange = 1.5f,
                AttackCooldown = 0.8f,
                FollowDistance = 2.5f,
                AggroRange = 10f,
                Armor = 2f,
                MeshColor = new Color(0.95f, 0.7f, 0.3f),
                MeshScale = new Vector3(0.6f, 0.6f, 0.6f)
            };

            // Mongo — Donut's pet dinosaur, tanky and strong
            _companions["mongo"] = new CompanionData
            {
                Id = "mongo",
                CompanionName = "Mongo",
                Description = "A velocipede dinosaur. Donut's pet. Hits like a truck.",
                BaseHealth = 150f,
                BaseDamage = 15f,
                MoveSpeed = 5f,
                AttackRange = 2f,
                AttackCooldown = 2f,
                FollowDistance = 3.5f,
                AggroRange = 8f,
                Armor = 8f,
                MeshColor = new Color(0.3f, 0.7f, 0.3f),
                MeshScale = new Vector3(1.2f, 1.2f, 1.2f)
            };

            GD.Print($"[CompanionRegistry] Initialized {_companions.Count} companions");
        }

        public static CompanionData Get(string id)
        {
            return _companions.TryGetValue(id, out var data) ? data : null;
        }

        public static IReadOnlyDictionary<string, CompanionData> All => _companions;
    }
}
