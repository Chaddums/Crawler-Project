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

            Register(new AbilityData("ability_shield_bash", "Shield Bash", AbilityType.Melee, 20f,
                StatType.Strength, 0.7f, 2.5f, 8f)
            { Range = 2f, StunDuration = 1f, Description = "Bash an enemy with your shield, stunning them." });

            Register(new AbilityData("ability_whirlwind", "Whirlwind", AbilityType.AoE, 30f,
                StatType.Strength, 0.8f, 4f, 15f)
            { Range = 3.5f, AoERadius = 3.5f, KnockbackForce = 4f, Description = "Spin in a circle, hitting all nearby enemies." });

            // --- Primal abilities ---
            Register(new AbilityData("ability_slam", "Slam", AbilityType.Melee, 25f,
                StatType.Strength, 0.8f, 2.5f, 10f)
            { Range = 3f, AoERadius = 3f, KnockbackForce = 5f, Description = "Slam the ground, damaging and knocking back nearby enemies." });

            Register(new AbilityData("ability_feral_roar", "Feral Roar", AbilityType.AoE, 10f,
                StatType.Strength, 0.4f, 5f, 12f)
            { Range = 6f, AoERadius = 6f, Description = "A terrifying roar that weakens all nearby enemies." });

            Register(new AbilityData("ability_earthquake", "Earthquake", AbilityType.AoE, 45f,
                StatType.Strength, 1.0f, 6f, 25f)
            { Range = 5f, AoERadius = 5f, KnockbackForce = 3f, Description = "Shatter the ground beneath your enemies." });

            // --- Magic User abilities ---
            Register(new AbilityData("ability_arcane_bolt", "Arcane Bolt", AbilityType.Projectile, 20f,
                StatType.Intelligence, 0.7f, 1.5f, 8f)
            { Range = 12f, DamageType = DamageType.Lightning, Description = "A bolt of pure arcane energy." });

            Register(new AbilityData("ability_frost_nova", "Frost Nova", AbilityType.AoE, 25f,
                StatType.Intelligence, 0.6f, 4f, 15f)
            { Range = 5f, AoERadius = 5f, DamageType = DamageType.Ice, StunDuration = 0.5f, Description = "A burst of frost that damages and slows nearby enemies." });

            Register(new AbilityData("ability_meteor", "Meteor", AbilityType.Projectile, 50f,
                StatType.Intelligence, 1.0f, 6f, 30f)
            { Range = 14f, AoERadius = 4f, DamageType = DamageType.Fire, Description = "Call down a meteor from the dungeon ceiling. Devastating." });

            // --- Rogue abilities ---
            Register(new AbilityData("ability_backstab", "Backstab", AbilityType.Melee, 30f,
                StatType.Dexterity, 0.9f, 3f, 5f)
            { Range = 2f, Description = "A precise, devastating strike from the shadows." });

            Register(new AbilityData("ability_smoke_bomb", "Smoke Bomb", AbilityType.AoE, 12f,
                StatType.Dexterity, 0.3f, 5f, 10f)
            { Range = 5f, AoERadius = 4f, StunDuration = 0.8f, Description = "Throw a smoke bomb that blinds enemies in the area." });

            Register(new AbilityData("ability_assassinate", "Assassinate", AbilityType.Melee, 60f,
                StatType.Dexterity, 1.2f, 8f, 20f)
            { Range = 2.5f, Description = "A lethal strike with 3x critical damage." });

            // --- Necro-Bard abilities ---
            Register(new AbilityData("ability_dark_chord", "Dark Chord", AbilityType.AoE, 18f,
                StatType.Intelligence, 0.5f, 2f, 12f)
            { Range = 8f, AoERadius = 4f, DamageType = DamageType.Dark, Description = "A dissonant chord that damages all nearby enemies." });

            Register(new AbilityData("ability_raise_dead", "Raise Dead", AbilityType.Summon, 5f,
                StatType.Intelligence, 0.3f, 8f, 20f)
            { Range = 4f, DamageType = DamageType.Dark, Description = "Raise a skeletal minion to fight for you." });

            Register(new AbilityData("ability_death_ballad", "Death Ballad", AbilityType.AoE, 15f,
                StatType.Intelligence, 0.7f, 3f, 18f)
            { Range = 7f, AoERadius = 7f, DamageType = DamageType.Dark, Description = "A haunting melody that deals damage over time to all enemies." });

            // --- Pugilist abilities ---
            Register(new AbilityData("ability_flurry", "Flurry", AbilityType.Melee, 8f,
                StatType.Dexterity, 0.4f, 0.6f, 3f)
            { Range = 2f, Description = "A rapid series of punches." });

            Register(new AbilityData("ability_uppercut", "Uppercut", AbilityType.Melee, 35f,
                StatType.Strength, 0.8f, 3f, 8f)
            { Range = 2f, KnockbackForce = 6f, Description = "A devastating uppercut that sends enemies flying." });

            Register(new AbilityData("ability_hundred_fists", "Hundred Fists", AbilityType.Melee, 5f,
                StatType.Dexterity, 0.3f, 5f, 12f)
            { Range = 2.5f, AoERadius = 2.5f, Description = "Unleash a barrage of rapid punches hitting everything in range." });

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
