using System.Collections.Generic;

namespace JunkbotArena
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

            // --- Tin Can (balanced) abilities ---
            Register(new AbilityData("ability_strike", StringLoader.Get("abilities.ability_strike.name"), AbilityType.Melee, 15f,
                StatType.Strength, 0.6f, 1.2f, 0f)
            { Range = 2.5f, Description = StringLoader.Get("abilities.ability_strike.description") });

            Register(new AbilityData("ability_shield_bash", StringLoader.Get("abilities.ability_shield_bash.name"), AbilityType.Melee, 20f,
                StatType.Strength, 0.7f, 2.5f, 8f)
            { Range = 2f, StunDuration = 1f, Description = StringLoader.Get("abilities.ability_shield_bash.description") });

            Register(new AbilityData("ability_whirlwind", StringLoader.Get("abilities.ability_whirlwind.name"), AbilityType.AoE, 30f,
                StatType.Strength, 0.8f, 4f, 15f)
            { Range = 3.5f, AoERadius = 3.5f, KnockbackForce = 4f, Description = StringLoader.Get("abilities.ability_whirlwind.description") });

            // --- Scrapheap (tank) abilities ---
            Register(new AbilityData("ability_slam", StringLoader.Get("abilities.ability_slam.name"), AbilityType.Melee, 25f,
                StatType.Strength, 0.8f, 2.5f, 10f)
            { Range = 3f, AoERadius = 3f, KnockbackForce = 5f, Description = StringLoader.Get("abilities.ability_slam.description") });

            Register(new AbilityData("ability_feral_roar", StringLoader.Get("abilities.ability_feral_roar.name"), AbilityType.AoE, 10f,
                StatType.Strength, 0.4f, 5f, 12f)
            { Range = 6f, AoERadius = 6f, Description = StringLoader.Get("abilities.ability_feral_roar.description") });

            Register(new AbilityData("ability_earthquake", StringLoader.Get("abilities.ability_earthquake.name"), AbilityType.AoE, 45f,
                StatType.Strength, 1.0f, 6f, 25f)
            { Range = 5f, AoERadius = 5f, KnockbackForce = 3f, Description = StringLoader.Get("abilities.ability_earthquake.description") });

            // --- Spark Plug (energy caster) abilities ---
            Register(new AbilityData("ability_arcane_bolt", StringLoader.Get("abilities.ability_arcane_bolt.name"), AbilityType.Projectile, 20f,
                StatType.Intelligence, 0.7f, 1.5f, 8f)
            { Range = 12f, DamageType = DamageType.Lightning, Description = StringLoader.Get("abilities.ability_arcane_bolt.description") });

            Register(new AbilityData("ability_frost_nova", StringLoader.Get("abilities.ability_frost_nova.name"), AbilityType.AoE, 25f,
                StatType.Intelligence, 0.6f, 4f, 15f)
            { Range = 5f, AoERadius = 5f, DamageType = DamageType.Ice, StunDuration = 0.5f, Description = StringLoader.Get("abilities.ability_frost_nova.description") });

            Register(new AbilityData("ability_meteor", StringLoader.Get("abilities.ability_meteor.name"), AbilityType.Projectile, 50f,
                StatType.Intelligence, 1.0f, 6f, 30f)
            { Range = 14f, AoERadius = 4f, DamageType = DamageType.Fire, Description = StringLoader.Get("abilities.ability_meteor.description") });

            // --- Rust Bucket (stealth/crit) abilities ---
            Register(new AbilityData("ability_backstab", StringLoader.Get("abilities.ability_backstab.name"), AbilityType.Melee, 30f,
                StatType.Dexterity, 0.9f, 3f, 5f)
            { Range = 2f, Description = StringLoader.Get("abilities.ability_backstab.description") });

            Register(new AbilityData("ability_smoke_bomb", StringLoader.Get("abilities.ability_smoke_bomb.name"), AbilityType.AoE, 12f,
                StatType.Dexterity, 0.3f, 5f, 10f)
            { Range = 5f, AoERadius = 4f, StunDuration = 0.8f, Description = StringLoader.Get("abilities.ability_smoke_bomb.description") });

            Register(new AbilityData("ability_assassinate", StringLoader.Get("abilities.ability_assassinate.name"), AbilityType.Melee, 60f,
                StatType.Dexterity, 1.2f, 8f, 20f)
            { Range = 2.5f, Description = StringLoader.Get("abilities.ability_assassinate.description") });

            // --- Noise Box (support/disruptor) abilities ---
            Register(new AbilityData("ability_dark_chord", StringLoader.Get("abilities.ability_dark_chord.name"), AbilityType.AoE, 18f,
                StatType.Intelligence, 0.5f, 2f, 12f)
            { Range = 8f, AoERadius = 4f, DamageType = DamageType.Dark, Description = StringLoader.Get("abilities.ability_dark_chord.description") });

            Register(new AbilityData("ability_raise_dead", StringLoader.Get("abilities.ability_raise_dead.name"), AbilityType.Summon, 5f,
                StatType.Intelligence, 0.3f, 8f, 20f)
            { Range = 4f, DamageType = DamageType.Dark, Description = StringLoader.Get("abilities.ability_raise_dead.description") });

            Register(new AbilityData("ability_death_ballad", StringLoader.Get("abilities.ability_death_ballad.name"), AbilityType.AoE, 15f,
                StatType.Intelligence, 0.7f, 3f, 18f)
            { Range = 7f, AoERadius = 7f, DamageType = DamageType.Dark, Description = StringLoader.Get("abilities.ability_death_ballad.description") });

            // --- Clunker (melee combo) abilities ---
            Register(new AbilityData("ability_flurry", StringLoader.Get("abilities.ability_flurry.name"), AbilityType.Melee, 8f,
                StatType.Dexterity, 0.4f, 0.6f, 3f)
            { Range = 2f, Description = StringLoader.Get("abilities.ability_flurry.description") });

            Register(new AbilityData("ability_uppercut", StringLoader.Get("abilities.ability_uppercut.name"), AbilityType.Melee, 35f,
                StatType.Strength, 0.8f, 3f, 8f)
            { Range = 2f, KnockbackForce = 6f, Description = StringLoader.Get("abilities.ability_uppercut.description") });

            Register(new AbilityData("ability_hundred_fists", StringLoader.Get("abilities.ability_hundred_fists.name"), AbilityType.Melee, 5f,
                StatType.Dexterity, 0.3f, 5f, 12f)
            { Range = 2.5f, AoERadius = 2.5f, Description = StringLoader.Get("abilities.ability_hundred_fists.description") });

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
