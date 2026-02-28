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
            Register(new AbilityData("ability_strike", "Piston Strike", AbilityType.Melee, 15f,
                StatType.Strength, 0.6f, 1.2f, 0f)
            { Range = 2.5f, Description = "A hydraulic-assisted arm strike. Standard issue, reliable output." });

            Register(new AbilityData("ability_shield_bash", "Bulkhead Slam", AbilityType.Melee, 20f,
                StatType.Strength, 0.7f, 2.5f, 8f)
            { Range = 2f, StunDuration = 1f, Description = "Ram your plating into the target, overloading their gyroscopes." });

            Register(new AbilityData("ability_whirlwind", "Rotary Shred", AbilityType.AoE, 30f,
                StatType.Strength, 0.8f, 4f, 15f)
            { Range = 3.5f, AoERadius = 3.5f, KnockbackForce = 4f, Description = "Engage rotational servos at max RPM, shredding everything in range." });

            // --- Scrapheap (tank) abilities ---
            Register(new AbilityData("ability_slam", "Chassis Slam", AbilityType.Melee, 25f,
                StatType.Strength, 0.8f, 2.5f, 10f)
            { Range = 3f, AoERadius = 3f, KnockbackForce = 5f, Description = "Hurl your full tonnage into the floor, sending shockwaves through nearby units." });

            Register(new AbilityData("ability_feral_roar", "Threat Broadcast", AbilityType.AoE, 10f,
                StatType.Strength, 0.4f, 5f, 12f)
            { Range = 6f, AoERadius = 6f, Description = "Blast a high-decibel threat signal that scrambles enemy targeting." });

            Register(new AbilityData("ability_earthquake", "Seismic Pound", AbilityType.AoE, 45f,
                StatType.Strength, 1.0f, 6f, 25f)
            { Range = 5f, AoERadius = 5f, KnockbackForce = 3f, Description = "Drive both fists into the arena floor. Structural integrity not guaranteed." });

            // --- Spark Plug (energy caster) abilities ---
            Register(new AbilityData("ability_arcane_bolt", "Arc Discharge", AbilityType.Projectile, 20f,
                StatType.Intelligence, 0.7f, 1.5f, 8f)
            { Range = 12f, DamageType = DamageType.Lightning, Description = "Fire a concentrated bolt of electrical energy from your core." });

            Register(new AbilityData("ability_frost_nova", "Cryo Burst", AbilityType.AoE, 25f,
                StatType.Intelligence, 0.6f, 4f, 15f)
            { Range = 5f, AoERadius = 5f, DamageType = DamageType.Ice, StunDuration = 0.5f, Description = "Vent coolant in a flash-freeze burst, locking nearby servos solid." });

            Register(new AbilityData("ability_meteor", "Orbital Drop", AbilityType.Projectile, 50f,
                StatType.Intelligence, 1.0f, 6f, 30f)
            { Range = 14f, AoERadius = 4f, DamageType = DamageType.Fire, Description = "Call down a superheated payload from the arena ceiling. AXIS disapproves." });

            // --- Rust Bucket (stealth/crit) abilities ---
            Register(new AbilityData("ability_backstab", "Blind Spot Strike", AbilityType.Melee, 30f,
                StatType.Dexterity, 0.9f, 3f, 5f)
            { Range = 2f, Description = "Exploit a gap in the target's sensor coverage for maximum damage." });

            Register(new AbilityData("ability_smoke_bomb", "EMP Grenade", AbilityType.AoE, 12f,
                StatType.Dexterity, 0.3f, 5f, 10f)
            { Range = 5f, AoERadius = 4f, StunDuration = 0.8f, Description = "Lob an electromagnetic pulse that scrambles sensors in the blast zone." });

            Register(new AbilityData("ability_assassinate", "Core Breach", AbilityType.Melee, 60f,
                StatType.Dexterity, 1.2f, 8f, 20f)
            { Range = 2.5f, Description = "Puncture the target's core housing. 3x critical damage." });

            // --- Noise Box (support/disruptor) abilities ---
            Register(new AbilityData("ability_dark_chord", "Dissonance Pulse", AbilityType.AoE, 18f,
                StatType.Intelligence, 0.5f, 2f, 12f)
            { Range = 8f, AoERadius = 4f, DamageType = DamageType.Dark, Description = "Emit a corrupted signal wave that damages all nearby units." });

            Register(new AbilityData("ability_raise_dead", "Salvage Drone", AbilityType.Summon, 5f,
                StatType.Intelligence, 0.3f, 8f, 20f)
            { Range = 4f, DamageType = DamageType.Dark, Description = "Reactivate a scrapped unit to fight on your behalf." });

            Register(new AbilityData("ability_death_ballad", "Feedback Loop", AbilityType.AoE, 15f,
                StatType.Intelligence, 0.7f, 3f, 18f)
            { Range = 7f, AoERadius = 7f, DamageType = DamageType.Dark, Description = "Broadcast a recursive signal that degrades enemy systems over time." });

            // --- Clunker (melee combo) abilities ---
            Register(new AbilityData("ability_flurry", "Piston Flurry", AbilityType.Melee, 8f,
                StatType.Dexterity, 0.4f, 0.6f, 3f)
            { Range = 2f, Description = "Rapid-fire piston punches. Quantity over quality." });

            Register(new AbilityData("ability_uppercut", "Pneumatic Uppercut", AbilityType.Melee, 35f,
                StatType.Strength, 0.8f, 3f, 8f)
            { Range = 2f, KnockbackForce = 6f, Description = "Compress and release a pneumatic fist, launching the target skyward." });

            Register(new AbilityData("ability_hundred_fists", "Overdrive Barrage", AbilityType.Melee, 5f,
                StatType.Dexterity, 0.3f, 5f, 12f)
            { Range = 2.5f, AoERadius = 2.5f, Description = "Overclock your piston array, hammering everything within arm's reach." });

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
