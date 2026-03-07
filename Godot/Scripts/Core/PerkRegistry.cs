using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Defines all permanent meta-perks. Think Hades Mirror of Night.
    /// Each perk has multiple ranks, costs scrap, and adds to threat level.
    /// </summary>
    public class PerkData
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int MaxRank { get; set; }
        public int BaseCost { get; set; }
        public float CostScale { get; set; } = 1.5f;
        public StatType AffectedStat { get; set; }
        public ModifierType ModType { get; set; }
        public float ValuePerRank { get; set; }
        public int ThreatPerRank { get; set; } = 1;

        public int GetCost(int rank)
        {
            return Mathf.RoundToInt(BaseCost * Mathf.Pow(CostScale, rank - 1));
        }
    }

    /// <summary>
    /// Static registry of all available meta-perks.
    /// </summary>
    public static class PerkRegistry
    {
        private static readonly Dictionary<string, PerkData> _perks = new();
        private static bool _initialized;

        public static IReadOnlyDictionary<string, PerkData> All => _perks;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            // ── Offensive Perks ──

            Register(new PerkData
            {
                Id = "perk_raw_power",
                Name = "Raw Power",
                Description = "Increase base Strength per rank.",
                MaxRank = 10,
                BaseCost = 50,
                CostScale = 1.4f,
                AffectedStat = StatType.Strength,
                ModType = ModifierType.Flat,
                ValuePerRank = 3f,
                ThreatPerRank = 1
            });

            Register(new PerkData
            {
                Id = "perk_precision",
                Name = "Precision Targeting",
                Description = "Increase critical hit chance per rank.",
                MaxRank = 8,
                BaseCost = 75,
                CostScale = 1.5f,
                AffectedStat = StatType.CritChance,
                ModType = ModifierType.Flat,
                ValuePerRank = 0.02f,
                ThreatPerRank = 1
            });

            Register(new PerkData
            {
                Id = "perk_brutality",
                Name = "Brutality Protocol",
                Description = "Increase critical damage multiplier per rank.",
                MaxRank = 6,
                BaseCost = 100,
                CostScale = 1.5f,
                AffectedStat = StatType.CritDamage,
                ModType = ModifierType.Flat,
                ValuePerRank = 0.1f,
                ThreatPerRank = 1
            });

            Register(new PerkData
            {
                Id = "perk_rapid_fire",
                Name = "Rapid Fire",
                Description = "Increase attack speed per rank.",
                MaxRank = 5,
                BaseCost = 80,
                CostScale = 1.6f,
                AffectedStat = StatType.AttackSpeed,
                ModType = ModifierType.Flat,
                ValuePerRank = 0.05f,
                ThreatPerRank = 1
            });

            // ── Defensive Perks ──

            Register(new PerkData
            {
                Id = "perk_reinforced",
                Name = "Reinforced Chassis",
                Description = "Increase max health per rank.",
                MaxRank = 10,
                BaseCost = 50,
                CostScale = 1.4f,
                AffectedStat = StatType.MaxHealth,
                ModType = ModifierType.Flat,
                ValuePerRank = 15f,
                ThreatPerRank = 1
            });

            Register(new PerkData
            {
                Id = "perk_plating",
                Name = "Scrap Plating",
                Description = "Increase armor per rank.",
                MaxRank = 8,
                BaseCost = 60,
                CostScale = 1.4f,
                AffectedStat = StatType.Armor,
                ModType = ModifierType.Flat,
                ValuePerRank = 3f,
                ThreatPerRank = 1
            });

            Register(new PerkData
            {
                Id = "perk_energy_core",
                Name = "Energy Core",
                Description = "Increase max mana per rank.",
                MaxRank = 6,
                BaseCost = 60,
                CostScale = 1.5f,
                AffectedStat = StatType.MaxMana,
                ModType = ModifierType.Flat,
                ValuePerRank = 10f,
                ThreatPerRank = 1
            });

            // ── Utility Perks ──

            Register(new PerkData
            {
                Id = "perk_swift",
                Name = "Swift Servos",
                Description = "Increase move speed per rank.",
                MaxRank = 5,
                BaseCost = 40,
                CostScale = 1.5f,
                AffectedStat = StatType.MoveSpeed,
                ModType = ModifierType.Flat,
                ValuePerRank = 0.5f,
                ThreatPerRank = 1
            });

            Register(new PerkData
            {
                Id = "perk_cooldown",
                Name = "Overclock",
                Description = "Increase cooldown reduction per rank.",
                MaxRank = 5,
                BaseCost = 100,
                CostScale = 1.6f,
                AffectedStat = StatType.CooldownReduction,
                ModType = ModifierType.Flat,
                ValuePerRank = 0.04f,
                ThreatPerRank = 2
            });

            Register(new PerkData
            {
                Id = "perk_luck",
                Name = "Scavenger's Luck",
                Description = "Increase luck (item find + rarity) per rank.",
                MaxRank = 8,
                BaseCost = 80,
                CostScale = 1.5f,
                AffectedStat = StatType.Luck,
                ModType = ModifierType.Flat,
                ValuePerRank = 3f,
                ThreatPerRank = 1
            });

            GD.Print($"[PerkRegistry] Initialized {_perks.Count} perks");
        }

        public static PerkData Get(string id)
        {
            _perks.TryGetValue(id, out var perk);
            return perk;
        }

        private static void Register(PerkData perk)
        {
            _perks[perk.Id] = perk;
        }
    }
}
