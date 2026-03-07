using System.Collections.Generic;

namespace JunkbotArena
{
    /// <summary>
    /// Registry of all grafts — harvested biological components that can be
    /// socketed into GraftSocket nodes on the passive tree. Organs, parasites,
    /// and living tissue bolted onto your robot frame for power.
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
            // =============================================================
            // RARE GRAFTS — harvested organs, stat-focused
            // =============================================================

            Add(new SalvageCoreData
            {
                Id = "core_fortified",
                CoreName = "Calcified Heartstone",
                Description = "A petrified heart from something enormous. Whatever it was, it refused to die. The mineral deposits have fused with its chambers. It still beats — once every forty seconds.\n+20% max HP, +5 armor.",
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
                CoreName = "Pulsing Nerve Cluster",
                Description = "A knot of neural tissue that won't stop firing. Severed from the host three floors ago. It generates its own bioelectric field. Nobody wants to touch it, but the power output is undeniable.\n+30% max mana.",
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
                CoreName = "Stalker's Eye",
                Description = "A lidless eye from a creature that hunted by vibration. It tracks movement on its own, swiveling in its mounting bracket. It blinks when you're not looking. Your targeting systems have never been better.\n+8% crit chance, +20% crit damage.",
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
                CoreName = "Twitching Sinew Bundle",
                Description = "Harvested muscle fibers from a creature built entirely for speed. They contract and release in waves, even now. When you bolt them to your servos, your frame shudders once — then moves faster than it was ever designed to.\n+15% move speed, +10% attack speed.",
                Rarity = SalvageCoreRarity.Rare,
                StatBonuses =
                {
                    new StatModifier(StatType.MoveSpeed, ModifierType.Percent, 0.15f),
                    new StatModifier(StatType.AttackSpeed, ModifierType.Flat, 0.10f)
                }
            });

            // =============================================================
            // EPIC GRAFTS — parasites and organs with active effects
            // =============================================================

            Add(new SalvageCoreData
            {
                Id = "core_vampiric",
                CoreName = "Leech Gland",
                Description = "A throbbing parasitic organ that extends hair-thin filaments into your damage systems. When you hurt something, the gland drinks. When the gland drinks, you heal. It purrs when it feeds. You try not to think about that.\n3% lifesteal on all damage.",
                Rarity = SalvageCoreRarity.Epic,
                PerkId = Perks.CoreVampiric
            });

            Add(new SalvageCoreData
            {
                Id = "core_scavenger",
                CoreName = "Carrion Beetle Colony",
                Description = "A sealed canister of thumb-sized beetles that strip the useful parts from anything dead within seconds. They've learned to recognize loot. They bring it back. You don't feed them — they feed themselves.\n+100% item find. Extra enemy drops.",
                Rarity = SalvageCoreRarity.Epic,
                PerkId = Perks.CoreScavenger
            });

            Add(new SalvageCoreData
            {
                Id = "core_prismatic",
                CoreName = "Chromatic Tumor",
                Description = "A pulsing growth harvested from a creature that could shift its biology at will. It cycles through colors in a slow, nauseating rhythm. Bolted to your weapon systems, it randomizes the elemental signature of every shot. The tumor seems to enjoy this.\nAll damage converts to a random element.",
                Rarity = SalvageCoreRarity.Epic,
                PerkId = Perks.CorePrismatic
            });

            Add(new SalvageCoreData
            {
                Id = "core_volatile",
                CoreName = "Bloat Sac",
                Description = "A pressurized organ from something that used explosive reproduction as a defense mechanism. When you kill an enemy, the sac detects the death signal and triggers a sympathetic detonation in the corpse. The biological term for this is 'very upsetting.'\nKills explode for 30% of enemy max HP.",
                Rarity = SalvageCoreRarity.Epic,
                PerkId = Perks.CoreVolatile
            });

            // =============================================================
            // LEGENDARY GRAFTS — living parasites and spliced organs
            // =============================================================

            // --- Spliced Organs: grant perks from other class trees ---

            Add(new SalvageCoreData
            {
                Id = "core_cross_wired_tank",
                CoreName = "Ironclad Carapace Graft",
                Description = "A section of shell from a creature that lived for centuries by never moving. Layers of calcified armor fused with regenerative tissue. When bolted to your frame, wounds close faster but your legs feel... optional.\nGrants Iron Fortress: +50% healing, cannot dash.",
                Rarity = SalvageCoreRarity.Legendary,
                GrantsPerkId = Perks.IronFortress
            });

            Add(new SalvageCoreData
            {
                Id = "core_cross_wired_caster",
                CoreName = "Overloaded Synapse",
                Description = "A brain node burning so hot it glows through the casing. The creature it came from thought itself to death. Its final neural pattern is stuck in a loop: more power, more power, more power. Your abilities hit harder. Your mana bleeds faster.\nGrants Overcharge: +40% ability damage, +30% mana cost.",
                Rarity = SalvageCoreRarity.Legendary,
                GrantsPerkId = Perks.Overcharge
            });

            Add(new SalvageCoreData
            {
                Id = "core_cross_wired_brawler",
                CoreName = "Berserker's Adrenal Gland",
                Description = "Ripped from something that only fought harder as it bled out. The gland is oversized, scarred, and angry-looking. It floods your systems with synthetic adrenaline proportional to structural damage. The closer you are to death, the more alive it makes you feel.\nGrants Berserker Protocol: +2% damage per 1% HP missing.",
                Rarity = SalvageCoreRarity.Legendary,
                GrantsPerkId = Perks.BerserkerProtocol
            });

            Add(new SalvageCoreData
            {
                Id = "core_cross_wired_agile",
                CoreName = "Hollow Bone Lattice",
                Description = "The skeletal framework of a creature designed for maximum force with minimum mass. Beautiful, in a horrible way. Your attacks channel through it with devastating efficiency, but your frame absorbs impacts like wet paper.\nGrants Glass Cannon: +50% damage dealt, +30% damage taken.",
                Rarity = SalvageCoreRarity.Legendary,
                GrantsPerkId = Perks.GlassCannon
            });

            // --- Amplifier Parasites: living creatures that enhance abilities ---

            Add(new SalvageCoreData
            {
                Id = "core_amplifier_shield_bash",
                CoreName = "Battering Skull Cap",
                Description = "The reinforced frontal plate of a ram-beast that charged through solid rock. It's been dead for weeks but the bone is still warm. When grafted to your shield systems, impacts carry the memory of ten thousand headlong collisions.\n+3 levels to Shield Bash.",
                Rarity = SalvageCoreRarity.Legendary,
                AmplifyAbilityId = "shield_bash"
            });

            Add(new SalvageCoreData
            {
                Id = "core_amplifier_fireball",
                CoreName = "Magma Gland",
                Description = "A fireproof organ that generated temperatures hot enough to melt stone. The creature used it for digestion. You're using it for murder. It whines at a frequency below hearing when it charges. The air around it shimmers.\n+3 levels to Fireball.",
                Rarity = SalvageCoreRarity.Legendary,
                AmplifyAbilityId = "fireball"
            });

            Add(new SalvageCoreData
            {
                Id = "core_amplifier_chain_shot",
                CoreName = "Hydra Nerve Strand",
                Description = "A neural thread from a multi-headed predator. It splits and seeks targets autonomously, overriding your aim with something older and hungrier. Each tendril finds a throat. You stopped counting heads three fights ago.\n+3 levels to Chain Shot.",
                Rarity = SalvageCoreRarity.Legendary,
                AmplifyAbilityId = "chain_shot"
            });

            Add(new SalvageCoreData
            {
                Id = "core_singularity",
                CoreName = "Gravity Parasite",
                Description = "A living organism that bends space around itself to feed. It was killing its host slowly — collapsing the creature's organs inward, one at a time. Now it's bolted to your frame. When you channel energy, it wakes up and everything nearby slides toward you. It's not grateful. It's just hungry.\nAbility casts pull enemies within 8m. 5s cooldown.",
                Rarity = SalvageCoreRarity.Legendary,
                PerkId = Perks.CoreSingularity
            });
        }

        private static void Add(SalvageCoreData core) => _cores[core.Id] = core;
    }
}
