using System.Collections.Generic;
using Godot;

namespace JunkbotArena
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

            // BIT — Basic Intelligence Terminal, hovering drone companion
            _companions["bit"] = new CompanionData
            {
                Id = "bit",
                CompanionName = StringLoader.Get("companions.bit.name"),
                Description = StringLoader.Get("companions.bit.description"),
                BaseHealth = 40f,
                BaseDamage = 6f,
                MoveSpeed = 9f,
                AttackRange = 2f,
                AttackCooldown = 1.0f,
                FollowDistance = 2f,
                AggroRange = 12f,
                Armor = 1f,
                MeshColor = new Color(0.3f, 0.8f, 1f),
                MeshScale = new Vector3(0.5f, 0.5f, 0.5f)
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
