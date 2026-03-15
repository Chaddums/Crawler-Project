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

        // --- Scrapheap Sub-Branch: Juggernaut ---
        /// <summary>AoE damage reduced by 40%. +2 armor per enemy within 5m (max 10).</summary>
        public const string ExplosionDampener = "explosion_dampener";
        /// <summary>10% of damage taken stored (max 200). Next basic releases as bonus Physical.</summary>
        public const string KineticBattery = "kinetic_battery";
        /// <summary>Above 80% HP: immune to stun/slow/knockback. Below 50%: +30% damage, +20% speed.</summary>
        public const string UnstoppableForce = "unstoppable_force";

        // --- Scrapheap Sub-Branch: Berserker ---
        /// <summary>Basic attacks heal 3% of damage dealt. Each heal reduces armor by 1 for 3s.</summary>
        public const string LeakingFuel = "leaking_fuel";
        /// <summary>Gain 1 Rage per hit taken (max 10). Each: +3% damage, +2% attack speed. At 10: AoE discharge.</summary>
        public const string RageAccumulator = "rage_accumulator";
        /// <summary>HP drains 3%/s. While draining: +50% damage, +30% attack speed. Toggled on/off.</summary>
        public const string RedLine = "red_line";

        // --- Scrapheap Sub-Branch: Fortress ---
        /// <summary>Melee attackers take 20 Lightning damage + stunned 0.3s. 1s CD per target.</summary>
        public const string ElectrifiedHull = "electrified_hull";
        /// <summary>Every 8s, emit taunt pulse (10m). Taunted enemies deal -15% to allies, +10% to you.</summary>
        public const string TauntEmitter = "taunt_emitter";
        /// <summary>Below 25% HP: immobile 3s, +100% armor, reflect 50% damage, heal 5%/s. 90s CD.</summary>
        public const string LastStand = "last_stand";

        // --- Scrapheap Keystone B ---
        /// <summary>First hit each room = 0 damage. Reset every 10s. +20% armor. -20% speed, -50% healing.</summary>
        public const string AblativePlating = "ablative_plating";

        // --- TinCan Sub-Branch: Bulwark ---
        /// <summary>15% chance to negate damage. On parry: next attack +40% damage within 1s.</summary>
        public const string AutoParry = "auto_parry";
        /// <summary>After taking elemental damage, gain 30% resistance to that element for 5s.</summary>
        public const string ElementalTuning = "elemental_tuning";
        /// <summary>Every 4th hit taken is reflected at 200% damage. Visible counter.</summary>
        public const string FullReflect = "full_reflect";

        // --- TinCan Sub-Branch: Guardian ---
        /// <summary>On kill, drop a repair field (3m, 5s) that heals 3% max HP/s.</summary>
        public const string RepairBeacon = "repair_beacon";
        /// <summary>On ability use, nearby allies gain +10% attack speed for 3s. 5s CD.</summary>
        public const string OverclockAllies = "overclock_allies";
        /// <summary>Stat bonuses shared at 20% to allies in 8m. Absorb lethal ally damage (30s CD).</summary>
        public const string DistributedProcessing = "distributed_processing";

        // --- TinCan Sub-Branch: Sentinel ---
        /// <summary>When ally hit, 20% chance to fire retaliatory shot at 150% weapon damage.</summary>
        public const string InterceptProtocol = "intercept_protocol";
        /// <summary>Crits mark enemies for 4s. Marked take +15% from all sources. 1 mark at a time.</summary>
        public const string VulnerabilityScanner = "vulnerability_scanner";
        /// <summary>Standing still 2s+: attack range doubles, +25% CritChance, attacks pierce.</summary>
        public const string SentinelProtocol = "sentinel_protocol";

        // --- TinCan Keystone B ---
        /// <summary>Armor value added as flat ability damage. +20% CDR. -40% basic attack damage, -20% HP.</summary>
        public const string GalvanicCore = "galvanic_core";

        // --- SparkPlug Sub-Branch: Overcharge ---
        /// <summary>Abilities deal bonus damage equal to 8% of current mana.</summary>
        public const string EnergyOverload = "energy_overload";
        /// <summary>Spend 100+ mana in 3s: trigger AoE explosion (200% INT damage). 8s CD.</summary>
        public const string ManaBomb = "mana_bomb";
        /// <summary>Crit ability hits have 20% chance to reset that ability's cooldown (diminishing).</summary>
        public const string Supernova = "supernova";

        // --- SparkPlug Sub-Branch: Conduit ---
        /// <summary>Lightning chains to 2 targets at 30%. Chained enemies get -10% Lightning res.</summary>
        public const string LightningRod = "lightning_rod";
        /// <summary>Status-effected enemy dies: status jumps to 2 nearby at 80% duration.</summary>
        public const string StatusCascade = "status_cascade";
        /// <summary>All damage has 10% chance to chain (50% damage). Chains can chain (up to 3).</summary>
        public const string Propagation = "propagation";

        // --- SparkPlug Sub-Branch: Capacitor ---
        /// <summary>Standing still: +3% mana regen/s. Above 80% mana: +10% armor.</summary>
        public const string RegenerationField = "regeneration_field";
        /// <summary>Every 4th ability cast costs 0 mana. Counter visible on UI.</summary>
        public const string SpellEcho = "spell_echo";
        /// <summary>CDs tick 50% faster while mana >50%. Below 25%: refill 30% + 3s CD. 45s internal CD.</summary>
        public const string PerpetualEngine = "perpetual_engine";

        // --- SparkPlug Keystone B ---
        /// <summary>Abilities hitting 3+ enemies refund 40% mana. Status lasts 50% longer. -25% single target.</summary>
        public const string ArcaneConduit = "arcane_conduit";

        // --- RustBucket Sub-Branch: Infiltrator ---
        /// <summary>Crits reduce target armor by 10% for 4s (stacks 3x = -30%).</summary>
        public const string ArmorShred = "armor_shred";
        /// <summary>Targets below 20% HP take +50% damage. Low-HP kills: +25% rare loot.</summary>
        public const string Execute = "execute";
        /// <summary>Every 5th crit applies Death Mark (4s). Death Marked: 3x damage next hit. Kill = CD reset.</summary>
        public const string DeathMark = "death_mark";

        // --- RustBucket Sub-Branch: Saboteur ---
        /// <summary>Poisoned enemies explode on death into poison cloud (3m, 4s, 50% DPS).</summary>
        public const string PoisonCloud = "poison_cloud";
        /// <summary>Enemies with 3+ debuffs: +25% damage taken, -30% move speed.</summary>
        public const string SystemicFailure = "systemic_failure";
        /// <summary>DoTs can crit (50% crit chance). DoT crits: 150% tick + spread to 1 nearby.</summary>
        public const string ViralCascade = "viral_cascade";

        // --- RustBucket Sub-Branch: Scavenger ---
        /// <summary>Pickup radius doubled. Pickups heal 3% HP + 5% speed for 2s.</summary>
        public const string ScrapMagnet = "scrap_magnet";
        /// <summary>Shop -25%. Sell +50%. Every 500g earned: +1% damage (max 20%).</summary>
        public const string VendorDiscount = "vendor_discount";
        /// <summary>5% kill → consumable drop. Bosses +1 loot box. Every 10th pickup → random buff.</summary>
        public const string GoldenTouch = "golden_touch";

        // --- RustBucket Keystone B ---
        /// <summary>First hit +80% damage (ambush). Post-dash: invisible 1s. 2nd+ hits -20%.</summary>
        public const string ShadowProcessor = "shadow_processor";

        // --- NoiseBox Sub-Branch: Broadcast ---
        /// <summary>Allies in 8m: +10% damage, +5% speed. You: doubled bonuses.</summary>
        public const string RallyFrequency = "rally_frequency";
        /// <summary>Every 6s pulse (8m): enemies -20% damage 3s. Debuffed also slowed 15%.</summary>
        public const string FrequencyJam = "frequency_jam";
        /// <summary>Auras stack. Each active aura: +8% to ALL aura effects. 3+ auras: enemies can't regen.</summary>
        public const string Orchestrator = "orchestrator";

        // --- NoiseBox Sub-Branch: Dissonance ---
        /// <summary>Every 10s scream (6m). 50% flee/attack ally for 2s. Bosses: -20% damage.</summary>
        public const string SonicOverload = "sonic_overload";
        /// <summary>Confused enemies: +30% damage taken. Confused kills grant XP + on-kill effects.</summary>
        public const string NeuralVirus = "neural_virus";
        /// <summary>Confused/feared: 15%/s to switch sides permanently. Max 3 converts. +50% damage.</summary>
        public const string TotalChaos = "total_chaos";

        // --- NoiseBox Sub-Branch: Resonance ---
        /// <summary>2nd element on target triggers combo burst (Fire+Ice=blind, etc.).</summary>
        public const string ElementSynergy = "element_synergy";
        /// <summary>Full-duration status effects: 30% chance to become permanent. Max 2 per enemy.</summary>
        public const string PermanentDebuff = "permanent_debuff";
        /// <summary>4+ status enemies detonate for 500% combined DoT burst. Spreads all status in 5m.</summary>
        public const string UltimateCombo = "ultimate_combo";

        // --- NoiseBox Keystone B ---
        /// <summary>Abilities apply status in AoE (3m around target). +20% duration. -25% damage, +20% cost.</summary>
        public const string HarmonicResonance = "harmonic_resonance";

        // --- Clunker Sub-Branch: Piston ---
        /// <summary>Each consecutive hit on same target: +5% damage (max +30%). 6+: cleave 40%.</summary>
        public const string ComboEngine = "combo_engine";
        /// <summary>Every 5th basic: charged strike 250% damage + knockback. Always crits.</summary>
        public const string ImpactCharge = "impact_charge";
        /// <summary>No attack speed cap. +1% AS per hit (stacks infinitely, 10s). -1% MaxHP/s above 150% AS.</summary>
        public const string InfiniteCombo = "infinite_combo";

        // --- Clunker Sub-Branch: Wrecking Ball ---
        /// <summary>Dash into enemies: 100% STR Physical + stagger. Wall collision = double damage.</summary>
        public const string SeismicSlam = "seismic_slam";
        /// <summary>Basic attacks hit 180° arc at 60%. Destructibles always drop loot.</summary>
        public const string Demolition = "demolition";
        /// <summary>Hold dash to charge (2s). Leap AoE: 300-800% STR. Landing: 3s slow field (-40%).</summary>
        public const string OrbitalStrike = "orbital_strike";

        // --- Clunker Sub-Branch: Scrap Engine ---
        /// <summary>Kills: 20% chance to drop temp part (Blade/Shield/Booster, 15s).</summary>
        public const string PartScavenger = "part_scavenger";
        /// <summary>Kills within 2s: +20% damage per chain (max +100%). Breaks after 2s no kill.</summary>
        public const string KillChain = "kill_chain";
        /// <summary>Below 15% HP: instant execute. +5% threshold per chain (max 35%). Kills explode AoE.</summary>
        public const string Exterminator = "exterminator";

        // --- Clunker Keystone B ---
        /// <summary>Kill within 5s: +10% damage, +5% speed (5 stacks). At 5: no cooldowns 3s. -30% no stacks.</summary>
        public const string RampageCore = "rampage_core";

        // --- Cross-class bridge perks (enhanced) ---
        /// <summary>+20% damage to debuffed enemies. +10% per unique debuff on target (max 40%).</summary>
        public const string ExploitWeakness = "exploit_weakness";
        /// <summary>Ability hits have 10% chance to apply a random debuff. (Legacy, kept for PerkProcessor)</summary>
        public const string Resonance = "resonance";
        /// <summary>Crit damage dealt again as Lightning after 0.5s (30%). Crits restore 3% mana.</summary>
        public const string EnergyBlade = "energy_blade";
        /// <summary>On kill: +15% speed, +8% crit for 4s. Kills extend by 2s and refresh stacks.</summary>
        public const string AdrenalineRush = "adrenaline_rush";
        /// <summary>Armor increases healing (1% per 10 armor, max 20%). Healed: +5% damage 3s.</summary>
        public const string JuggernautLink = "juggernaut_link";
        /// <summary>15% max mana added as flat armor. Mana spent → shield (10%, max 50, decays 10/s).</summary>
        public const string ManaArmor = "mana_armor";
        /// <summary>Kill speed +5% status damage per kill in 5s (max 25%). Status kills: 10% repair orb.</summary>
        public const string FeedbackFrenzy = "feedback_frenzy";

        // --- Inner ring perks ---
        /// <summary>Heal 20% max HP when dropping below 25%. 60s CD. +50% armor for 3s after.</summary>
        public const string EmergencyRepairs = "emergency_repairs";
        /// <summary>+3% armor per enemy within 8m (max 15%). +2% damage per enemy (max 10%).</summary>
        public const string AdaptivePlating = "adaptive_plating";
        /// <summary>Damage type cycles every 5 hits. +10% damage for matching weakness.</summary>
        public const string TypeShift = "type_shift";
        /// <summary>Single-target attacks deal 25% damage in 3m AoE around target.</summary>
        public const string SplashProtocol = "splash_protocol";
        /// <summary>Below 30%: +20% damage, +20% speed, +10% dodge. Below 15%: heal 2%/s.</summary>
        public const string LastResort = "last_resort";

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
