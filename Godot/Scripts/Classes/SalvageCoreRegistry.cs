using System.Collections.Generic;

namespace JunkbotArena
{
    /// <summary>
    /// Registry of all salvage cores that can drop and be socketed into
    /// CoreSocket nodes on the passive tree.
    /// </summary>
    public static class SalvageCoreRegistry
    {
        private static readonly Dictionary<string, SalvageCoreData> _cores = new();
        public static IReadOnlyDictionary<string, SalvageCoreData> All => _cores;

        static SalvageCoreRegistry()
        {
            Register();
        }

        public static SalvageCoreData Get(string id) =>
            _cores.TryGetValue(id, out var core) ? core : null;

        private static void Register()
        {
            // --- RARE CORES (stat-focused) ---

            Add(new SalvageCoreData
            {
                Id = "core_fortified",
                CoreName = "Fortified Core",
                Description = "+20% max HP, +5 armor. A solid defensive foundation.",
                Rarity = SalvageCoreRarity.Rare,
                StatBonuses =
                {
                    new StatModifier(StatType.MaxHealth, ModifierType.Percent, 0.20f),
                    new StatModifier(StatType.Armor, ModifierType.Flat, 5f)
                }
            });

            Add(new SalvageCoreData
            {
                Id = "core_capacitor",
                CoreName = "Capacitor Core",
                Description = "+30% max mana, +2 mana regen. Powers ability-heavy builds.",
                Rarity = SalvageCoreRarity.Rare,
                StatBonuses =
                {
                    new StatModifier(StatType.MaxMana, ModifierType.Percent, 0.30f)
                },
                PerkId = Perks.CoreCapacitor
            });

            Add(new SalvageCoreData
            {
                Id = "core_precision",
                CoreName = "Precision Core",
                Description = "+8% crit chance, +20% crit damage. For those who land the shot.",
                Rarity = SalvageCoreRarity.Rare,
                StatBonuses =
                {
                    new StatModifier(StatType.CritChance, ModifierType.Flat, 0.08f),
                    new StatModifier(StatType.CritDamage, ModifierType.Flat, 0.20f)
                }
            });

            Add(new SalvageCoreData
            {
                Id = "core_accelerator",
                CoreName = "Accelerator Core",
                Description = "+15% move speed, +10% attack speed. Always stay on the move.",
                Rarity = SalvageCoreRarity.Rare,
                StatBonuses =
                {
                    new StatModifier(StatType.MoveSpeed, ModifierType.Percent, 0.15f),
                    new StatModifier(StatType.AttackSpeed, ModifierType.Flat, 0.10f)
                }
            });

            // --- EPIC CORES (perk-granting, build-altering) ---

            Add(new SalvageCoreData
            {
                Id = "core_vampiric",
                CoreName = "Vampiric Core",
                Description = "3% of all damage dealt is returned as health. Sustain through aggression.",
                Rarity = SalvageCoreRarity.Epic,
                PerkId = Perks.CoreVampiric
            });

            Add(new SalvageCoreData
            {
                Id = "core_scavenger",
                CoreName = "Scavenger Core",
                Description = "+100% item find. Enemies drop extra loot. The hoarder's dream.",
                Rarity = SalvageCoreRarity.Epic,
                PerkId = Perks.CoreScavenger
            });

            Add(new SalvageCoreData
            {
                Id = "core_prismatic",
                CoreName = "Prismatic Core",
                Description = "All damage converts to a random element each hit. Debuffs everywhere.",
                Rarity = SalvageCoreRarity.Epic,
                PerkId = Perks.CorePrismatic
            });

            Add(new SalvageCoreData
            {
                Id = "core_volatile",
                CoreName = "Volatile Core",
                Description = "Kills cause enemies to explode, dealing 30% of their max HP to nearby foes.",
                Rarity = SalvageCoreRarity.Epic,
                PerkId = Perks.CoreVolatile
            });

            // --- LEGENDARY CORES (build-defining, very rare) ---

            Add(new SalvageCoreData
            {
                Id = "core_cross_wired_tank",
                CoreName = "Cross-Wired Core: Fortress",
                Description = "Grants Iron Fortress perk from the TinCan tree. +50% healing, cannot dash.",
                Rarity = SalvageCoreRarity.Legendary,
                GrantsPerkId = Perks.IronFortress
            });

            Add(new SalvageCoreData
            {
                Id = "core_cross_wired_caster",
                CoreName = "Cross-Wired Core: Overcharge",
                Description = "Grants Overcharge perk from the SparkPlug tree. Abilities cost +30% mana but deal +40% damage.",
                Rarity = SalvageCoreRarity.Legendary,
                GrantsPerkId = Perks.Overcharge
            });

            Add(new SalvageCoreData
            {
                Id = "core_cross_wired_brawler",
                CoreName = "Cross-Wired Core: Berserker",
                Description = "Grants Berserker Protocol from the Scrapheap tree. +2% damage per 1% HP missing.",
                Rarity = SalvageCoreRarity.Legendary,
                GrantsPerkId = Perks.BerserkerProtocol
            });

            Add(new SalvageCoreData
            {
                Id = "core_cross_wired_agile",
                CoreName = "Cross-Wired Core: Glass Cannon",
                Description = "Grants Glass Cannon from the RustBucket tree. +50% damage dealt, +30% damage taken.",
                Rarity = SalvageCoreRarity.Legendary,
                GrantsPerkId = Perks.GlassCannon
            });

            Add(new SalvageCoreData
            {
                Id = "core_amplifier_shield_bash",
                CoreName = "Amplifier Core: Shield Bash",
                Description = "+3 levels to Shield Bash. Massively increased damage and stun duration.",
                Rarity = SalvageCoreRarity.Legendary,
                AmplifyAbilityId = "shield_bash"
            });

            Add(new SalvageCoreData
            {
                Id = "core_amplifier_fireball",
                CoreName = "Amplifier Core: Fireball",
                Description = "+3 levels to Fireball. Enormous explosion radius and damage.",
                Rarity = SalvageCoreRarity.Legendary,
                AmplifyAbilityId = "fireball"
            });

            Add(new SalvageCoreData
            {
                Id = "core_amplifier_chain_shot",
                CoreName = "Amplifier Core: Chain Shot",
                Description = "+3 levels to Chain Shot. More bounces, more carnage.",
                Rarity = SalvageCoreRarity.Legendary,
                AmplifyAbilityId = "chain_shot"
            });

            Add(new SalvageCoreData
            {
                Id = "core_singularity",
                CoreName = "Singularity Core",
                Description = "On ability cast, pull all enemies within 8m toward you. 5s cooldown.",
                Rarity = SalvageCoreRarity.Legendary,
                PerkId = Perks.CoreSingularity
            });
        }

        private static void Add(SalvageCoreData core) => _cores[core.Id] = core;
    }
}
