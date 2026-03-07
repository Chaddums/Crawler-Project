namespace JunkbotArena
{
    /// <summary>
    /// Perk ID constants for gameplay-changing passive tree nodes.
    /// Referenced by PassiveTreeBuilder (definition) and combat/movement systems (query).
    /// Use PlayerClassController.HasPerk(Perks.XXX) to check if active.
    /// </summary>
    public static class Perks
    {
        // --- Scrapheap (STR/CON — Heavy Brawler) ---
        /// <summary>Deal +2% damage per 1% HP missing. -20% max HP.</summary>
        public const string BerserkerProtocol = "berserker_protocol";
        /// <summary>Basic attacks have 20% chance to stagger enemies for 0.5s.</summary>
        public const string ImpactDriver = "impact_driver";
        /// <summary>Deal more damage the longer you keep moving without stopping.</summary>
        public const string Momentum = "momentum";
        /// <summary>Pinnacle: +30% size, +50 HP, +8 armor, immune to knockback, -15% speed.</summary>
        public const string JuggernautFrame = "juggernaut_frame";

        // --- TinCan (CON/STR — Tank) ---
        /// <summary>When hit, gain +5 armor for 3s. Stacks 3x.</summary>
        public const string ReactivePlating = "reactive_plating";
        /// <summary>Reflect 15% of damage taken back to attacker.</summary>
        public const string ThornsProtocol = "thorns_protocol";
        /// <summary>All healing received +50%. Cannot dash.</summary>
        public const string IronFortress = "iron_fortress";
        /// <summary>Pinnacle: +100 HP, +15 armor, allies near you take 20% less damage.</summary>
        public const string SiegePlating = "siege_plating";

        // --- SparkPlug (INT/DEX — Caster) ---
        /// <summary>Abilities cost 30% more mana but deal 40% more damage.</summary>
        public const string Overcharge = "overcharge";
        /// <summary>Ability hits arc to 1 additional nearby target at 50% damage.</summary>
        public const string ChainLightning = "chain_lightning";
        /// <summary>All damage taken from mana instead of HP. HP set to 1.</summary>
        public const string ManaShield = "mana_shield";
        /// <summary>Pinnacle: +50 mana, 20% CDR, lightning aura damages nearby enemies.</summary>
        public const string ArcReactor = "arc_reactor";

        // --- RustBucket (DEX/STR — Agile) ---
        /// <summary>Projectiles bounce to 1 nearby enemy at 60% damage.</summary>
        public const string RicochetRounds = "ricochet_rounds";
        /// <summary>Dashing leaves a smoke cloud that blinds enemies for 2s.</summary>
        public const string SmokeScreen = "smoke_screen";
        /// <summary>+50% damage dealt, +30% damage taken.</summary>
        public const string GlassCannon = "glass_cannon";
        /// <summary>Pinnacle: +2 dash charges, +25% attack speed, +15% crit, +20% speed.</summary>
        public const string AssaultFrame = "assault_frame";

        // --- NoiseBox (CHA/INT — Support/Debuffer) ---
        /// <summary>Enemies within 5m have -15% armor.</summary>
        public const string CorrosiveAura = "corrosive_aura";
        /// <summary>Status effects you apply also heal you for 2% max HP/s.</summary>
        public const string FeedbackLoop = "feedback_loop";
        /// <summary>All damage converts to DoT over 3s. Total damage +60%.</summary>
        public const string EntropyField = "entropy_field";
        /// <summary>Pinnacle: Debuffs spread to nearby enemies, +50% status duration.</summary>
        public const string BroadcastTower = "broadcast_tower";

        // --- Clunker (STR/INT — Heavy Hybrid) ---
        /// <summary>Basic attacks consume 10 mana to deal 50% more damage.</summary>
        public const string PoweredStrike = "powered_strike";
        /// <summary>Killing enemies has 15% chance to drop a repair orb.</summary>
        public const string ScrapRecycler = "scrap_recycler";
        /// <summary>+25% to all stats. Take 5 damage per second.</summary>
        public const string Overclocked = "overclocked";
        /// <summary>Pinnacle: +25% size, +30 HP, +20 mana, -25% ability cost, +10% all damage.</summary>
        public const string WarMachine = "war_machine";

        // --- Cross-class bridge perks ---
        /// <summary>+15% damage to enemies affected by any debuff.</summary>
        public const string ExploitWeakness = "exploit_weakness";
        /// <summary>Ability hits have 10% chance to apply a random debuff.</summary>
        public const string Resonance = "resonance";
        /// <summary>+15% move speed and +5% crit for 3s after killing an enemy.</summary>
        public const string AdrenalineRush = "adrenaline_rush";

        // --- Inner ring universal perks ---
        /// <summary>Heal 20% max HP when dropping below 25%. 60s cooldown.</summary>
        public const string EmergencyRepairs = "emergency_repairs";
        /// <summary>+3% damage reduction per nearby enemy (max 15%).</summary>
        public const string AdaptivePlating = "adaptive_plating";

        // --- Graft perk effects (harvested organs / living parasites) ---
        /// <summary>Leech Gland: 3% lifesteal on all damage dealt.</summary>
        public const string CoreVampiric = "core_vampiric";
        /// <summary>Carrion Beetle Colony: +100% item find, enemies drop extra loot.</summary>
        public const string CoreScavenger = "core_scavenger";
        /// <summary>Chromatic Tumor: all damage converts to random element each hit.</summary>
        public const string CorePrismatic = "core_prismatic";
        /// <summary>Bloat Sac: kills cause enemies to explode for 30% max HP AoE.</summary>
        public const string CoreVolatile = "core_volatile";
        /// <summary>Gravity Parasite: ability casts pull enemies within 8m. 5s CD.</summary>
        public const string CoreSingularity = "core_singularity";
        /// <summary>Pulsing Nerve Cluster: +30% max mana.</summary>
        public const string CoreCapacitor = "core_capacitor";

        // --- Mythic graft perk effects (ultra-rare, build-defining) ---
        /// <summary>The Immortal Engine: regen 5% max HP/s, survive lethal for 2s.</summary>
        public const string MythicImmortalEngine = "mythic_immortal_engine";
        /// <summary>The Devourer: permanent +0.5% damage per kill, stacks infinitely.</summary>
        public const string MythicDevourer = "mythic_devourer";
        /// <summary>Neural Hijack Tendril: 15% chance to convert killed enemy to ally.</summary>
        public const string MythicNeuralHijack = "mythic_neural_hijack";
        /// <summary>Paradox Gland: rewind 3s on lethal damage, once per floor.</summary>
        public const string MythicTimeLoop = "mythic_time_loop";
        /// <summary>Living Storm Core: all damage chains to 3 extra targets at 40%.</summary>
        public const string MythicStormCaller = "mythic_storm_caller";
        /// <summary>Void Heart: attacks leave void rifts (20% dmg/s, 3m, 4s).</summary>
        public const string MythicVoidHeart = "mythic_void_heart";
        /// <summary>Echo Chamber Organ: abilities fire twice (60% second), double mana cost.</summary>
        public const string MythicEchoChamber = "mythic_echo_chamber";
        /// <summary>Hemorrhage Engine: abilities cost HP instead of mana, +100% mana as HP.</summary>
        public const string MythicBloodEconomy = "mythic_blood_economy";
    }
}
