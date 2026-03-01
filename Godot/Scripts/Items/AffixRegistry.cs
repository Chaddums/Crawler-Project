using System.Collections.Generic;
using System.Linq;

namespace JunkbotArena
{
    public static class AffixRegistry
    {
        private static readonly List<AffixData> _allAffixes = new();
        private static bool _initialized;

        public static IReadOnlyList<AffixData> AllAffixes => _allAffixes;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            // --- PREFIXES (offensive / resource stats) ---
            Add("pre_str_1", StringLoader.Get("affixes.pre_str_1"), AffixType.Prefix, StatType.Strength, ModifierType.Flat, 1, 5);
            Add("pre_str_2", StringLoader.Get("affixes.pre_str_2"), AffixType.Prefix, StatType.Strength, ModifierType.Flat, 4, 10, 5);
            Add("pre_dex_1", StringLoader.Get("affixes.pre_dex_1"), AffixType.Prefix, StatType.Dexterity, ModifierType.Flat, 1, 5);
            Add("pre_dex_2", StringLoader.Get("affixes.pre_dex_2"), AffixType.Prefix, StatType.Dexterity, ModifierType.Flat, 4, 10, 5);
            Add("pre_int_1", StringLoader.Get("affixes.pre_int_1"), AffixType.Prefix, StatType.Intelligence, ModifierType.Flat, 1, 5);
            Add("pre_int_2", StringLoader.Get("affixes.pre_int_2"), AffixType.Prefix, StatType.Intelligence, ModifierType.Flat, 4, 10, 5);
            Add("pre_con_1", StringLoader.Get("affixes.pre_con_1"), AffixType.Prefix, StatType.Constitution, ModifierType.Flat, 1, 5);
            Add("pre_con_2", StringLoader.Get("affixes.pre_con_2"), AffixType.Prefix, StatType.Constitution, ModifierType.Flat, 4, 10, 5);
            Add("pre_cha_1", StringLoader.Get("affixes.pre_cha_1"), AffixType.Prefix, StatType.Charisma, ModifierType.Flat, 1, 5);
            Add("pre_lck_1", StringLoader.Get("affixes.pre_lck_1"), AffixType.Prefix, StatType.Luck, ModifierType.Flat, 1, 5);
            Add("pre_hp_1", StringLoader.Get("affixes.pre_hp_1"), AffixType.Prefix, StatType.MaxHealth, ModifierType.Flat, 5, 20);
            Add("pre_hp_2", StringLoader.Get("affixes.pre_hp_2"), AffixType.Prefix, StatType.MaxHealth, ModifierType.Flat, 15, 40, 5);
            Add("pre_mana_1", StringLoader.Get("affixes.pre_mana_1"), AffixType.Prefix, StatType.MaxMana, ModifierType.Flat, 5, 15);
            Add("pre_mana_2", StringLoader.Get("affixes.pre_mana_2"), AffixType.Prefix, StatType.MaxMana, ModifierType.Flat, 10, 30, 5);
            Add("pre_armor_1", StringLoader.Get("affixes.pre_armor_1"), AffixType.Prefix, StatType.Armor, ModifierType.Flat, 2, 8);
            Add("pre_armor_2", StringLoader.Get("affixes.pre_armor_2"), AffixType.Prefix, StatType.Armor, ModifierType.Flat, 6, 15, 5);
            Add("pre_crit_1", StringLoader.Get("affixes.pre_crit_1"), AffixType.Prefix, StatType.CritChance, ModifierType.Flat, 1, 3);
            Add("pre_critdmg_1", StringLoader.Get("affixes.pre_critdmg_1"), AffixType.Prefix, StatType.CritDamage, ModifierType.Flat, 5, 15);

            // --- SUFFIXES (defensive / utility stats) ---
            Add("suf_hp_pct", StringLoader.Get("affixes.suf_hp_pct"), AffixType.Suffix, StatType.MaxHealth, ModifierType.Percent, 0.03f, 0.10f);
            Add("suf_mana_pct", StringLoader.Get("affixes.suf_mana_pct"), AffixType.Suffix, StatType.MaxMana, ModifierType.Percent, 0.05f, 0.15f);
            Add("suf_armor_pct", StringLoader.Get("affixes.suf_armor_pct"), AffixType.Suffix, StatType.Armor, ModifierType.Percent, 0.05f, 0.12f);
            Add("suf_spd_1", StringLoader.Get("affixes.suf_spd_1"), AffixType.Suffix, StatType.MoveSpeed, ModifierType.Flat, 0.3f, 1f);
            Add("suf_spd_pct", StringLoader.Get("affixes.suf_spd_pct"), AffixType.Suffix, StatType.MoveSpeed, ModifierType.Percent, 0.03f, 0.08f);
            Add("suf_aspd_1", StringLoader.Get("affixes.suf_aspd_1"), AffixType.Suffix, StatType.AttackSpeed, ModifierType.Flat, 0.02f, 0.08f);
            Add("suf_aspd_pct", StringLoader.Get("affixes.suf_aspd_pct"), AffixType.Suffix, StatType.AttackSpeed, ModifierType.Percent, 0.03f, 0.10f);
            Add("suf_cdr_1", StringLoader.Get("affixes.suf_cdr_1"), AffixType.Suffix, StatType.CooldownReduction, ModifierType.Flat, 0.02f, 0.06f);
            Add("suf_str_pct", StringLoader.Get("affixes.suf_str_pct"), AffixType.Suffix, StatType.Strength, ModifierType.Percent, 0.05f, 0.12f);
            Add("suf_dex_pct", StringLoader.Get("affixes.suf_dex_pct"), AffixType.Suffix, StatType.Dexterity, ModifierType.Percent, 0.05f, 0.12f);
            Add("suf_int_pct", StringLoader.Get("affixes.suf_int_pct"), AffixType.Suffix, StatType.Intelligence, ModifierType.Percent, 0.05f, 0.12f);
            Add("suf_con_pct", StringLoader.Get("affixes.suf_con_pct"), AffixType.Suffix, StatType.Constitution, ModifierType.Percent, 0.05f, 0.12f);
            Add("suf_lck_pct", StringLoader.Get("affixes.suf_lck_pct"), AffixType.Suffix, StatType.Luck, ModifierType.Percent, 0.05f, 0.12f);
            Add("suf_crit_pct", StringLoader.Get("affixes.suf_crit_pct"), AffixType.Suffix, StatType.CritChance, ModifierType.Percent, 0.05f, 0.15f);
            Add("suf_critdmg_pct", StringLoader.Get("affixes.suf_critdmg_pct"), AffixType.Suffix, StatType.CritDamage, ModifierType.Percent, 0.05f, 0.15f);
        }

        private static void Add(string id, string name, AffixType type, StatType stat,
            ModifierType modType, float min, float max, int minILvl = 1, int weight = 100)
        {
            _allAffixes.Add(new AffixData(id, name, type, stat, modType, min, max, minILvl, weight));
        }

        public static List<AffixData> GetEligibleAffixes(AffixType type, int itemLevel)
        {
            return _allAffixes
                .Where(a => a.Type == type && a.MinItemLevel <= itemLevel)
                .ToList();
        }
    }
}
