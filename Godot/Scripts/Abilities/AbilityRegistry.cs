using System.Collections.Generic;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Static registry of all ability definitions, keyed by ID.
    /// </summary>
    public static class AbilityRegistry
    {
        private static readonly Dictionary<string, AbilityData> _abilities = new();
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            // --- Fighter abilities ---
            Register(new AbilityData("ability_strike", "Strike", AbilityType.Melee, 15f,
                StatType.Strength, 0.6f, 1.2f, 0f)
            { Range = 2.5f, Description = "A solid, dependable strike." });

            // --- Primal abilities ---
            Register(new AbilityData("ability_slam", "Slam", AbilityType.Melee, 25f,
                StatType.Strength, 0.8f, 2.5f, 10f)
            { Range = 3f, AoERadius = 3f, KnockbackForce = 5f, Description = "Slam the ground, damaging and knocking back nearby enemies." });

            // --- Magic User abilities ---
            Register(new AbilityData("ability_arcane_bolt", "Arcane Bolt", AbilityType.Projectile, 20f,
                StatType.Intelligence, 0.7f, 1.5f, 8f)
            { Range = 12f, DamageType = DamageType.Lightning, Description = "A bolt of pure arcane energy." });

            // --- Rogue abilities ---
            Register(new AbilityData("ability_backstab", "Backstab", AbilityType.Melee, 30f,
                StatType.Dexterity, 0.9f, 3f, 5f)
            { Range = 2f, Description = "A precise, devastating strike from the shadows." });

            // --- Necro-Bard abilities ---
            Register(new AbilityData("ability_dark_chord", "Dark Chord", AbilityType.AoE, 18f,
                StatType.Intelligence, 0.5f, 2f, 12f)
            { Range = 8f, AoERadius = 4f, DamageType = DamageType.Dark, Description = "A dissonant chord that damages all nearby enemies." });

            // --- Pugilist abilities ---
            Register(new AbilityData("ability_flurry", "Flurry", AbilityType.Melee, 8f,
                StatType.Dexterity, 0.4f, 0.6f, 3f)
            { Range = 2f, Description = "A rapid series of punches." });

            Godot.GD.Print($"[AbilityRegistry] Initialized {_abilities.Count} abilities");
        }

        public static AbilityData Get(string id)
        {
            return _abilities.TryGetValue(id, out var data) ? data : null;
        }

        private static void Register(AbilityData data)
        {
            _abilities[data.Id] = data;
        }
    }
}
