using System.Collections.Generic;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Builds and stores all 6 DCC classes.
    /// </summary>
    public static class CrawlerClassRegistry
    {
        private static readonly Dictionary<CrawlerClassName, CrawlerClassData> _classes = new();
        private static bool _initialized;

        public static IReadOnlyDictionary<CrawlerClassName, CrawlerClassData> Classes => _classes;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            BuildPrimal();
            BuildBoringOlFighter();
            BuildMagicUser();
            BuildRogue();
            BuildNecroBard();
            BuildPugilist();

            Godot.GD.Print($"[CrawlerClassRegistry] Initialized {_classes.Count} classes");
        }

        public static CrawlerClassData GetClass(CrawlerClassName name)
        {
            return _classes.TryGetValue(name, out var data) ? data : null;
        }

        private static void BuildPrimal()
        {
            var c = new CrawlerClassData
            {
                ClassName = CrawlerClassName.Primal,
                DisplayName = "Primal",
                Description = "A savage warrior channeling the raw power of the dungeon. Excels at brute force and surviving punishment.",
                PrimaryStat = StatType.Strength,
                SecondaryStat = StatType.Constitution,
                HpPerLevel = 12f,
                ManaPerLevel = 2f,
                PrimaryStatPerLevel = 3f,
                SecondaryStatPerLevel = 2f,
                TreeStartNodeId = "start_Primal",
                StartingAbilities = new() { "ability_slam" }
            };
            c.BaseStats.SetBaseStat(StatType.Strength, 16);
            c.BaseStats.SetBaseStat(StatType.Dexterity, 8);
            c.BaseStats.SetBaseStat(StatType.Constitution, 14);
            c.BaseStats.SetBaseStat(StatType.Intelligence, 6);
            c.BaseStats.SetBaseStat(StatType.Charisma, 8);
            c.BaseStats.SetBaseStat(StatType.Luck, 8);
            c.BaseStats.SetBaseStat(StatType.MaxHealth, 120);
            c.BaseStats.SetBaseStat(StatType.MaxMana, 30);
            c.BaseStats.SetBaseStat(StatType.Armor, 5);
            c.BaseStats.SetBaseStat(StatType.MoveSpeed, 5.5f);
            _classes[c.ClassName] = c;
        }

        private static void BuildBoringOlFighter()
        {
            var c = new CrawlerClassData
            {
                ClassName = CrawlerClassName.BoringOlFighter,
                DisplayName = "Boring Ol' Fighter",
                Description = "Nothing flashy. Just hits things really well. The most balanced and forgiving class.",
                PrimaryStat = StatType.Strength,
                SecondaryStat = StatType.Dexterity,
                HpPerLevel = 10f,
                ManaPerLevel = 3f,
                PrimaryStatPerLevel = 2f,
                SecondaryStatPerLevel = 2f,
                TreeStartNodeId = "start_BoringOlFighter",
                StartingAbilities = new() { "ability_strike" }
            };
            c.BaseStats.SetBaseStat(StatType.Strength, 14);
            c.BaseStats.SetBaseStat(StatType.Dexterity, 12);
            c.BaseStats.SetBaseStat(StatType.Constitution, 12);
            c.BaseStats.SetBaseStat(StatType.Intelligence, 8);
            c.BaseStats.SetBaseStat(StatType.Charisma, 8);
            c.BaseStats.SetBaseStat(StatType.Luck, 8);
            c.BaseStats.SetBaseStat(StatType.MaxHealth, 100);
            c.BaseStats.SetBaseStat(StatType.MaxMana, 40);
            c.BaseStats.SetBaseStat(StatType.Armor, 3);
            c.BaseStats.SetBaseStat(StatType.MoveSpeed, 6f);
            _classes[c.ClassName] = c;
        }

        private static void BuildMagicUser()
        {
            var c = new CrawlerClassData
            {
                ClassName = CrawlerClassName.MagicUser,
                DisplayName = "Magic User",
                Description = "Wields arcane forces from the dungeon's system. High damage but fragile.",
                PrimaryStat = StatType.Intelligence,
                SecondaryStat = StatType.Charisma,
                HpPerLevel = 6f,
                ManaPerLevel = 6f,
                PrimaryStatPerLevel = 3f,
                SecondaryStatPerLevel = 1f,
                TreeStartNodeId = "start_MagicUser",
                StartingAbilities = new() { "ability_arcane_bolt" }
            };
            c.BaseStats.SetBaseStat(StatType.Strength, 6);
            c.BaseStats.SetBaseStat(StatType.Dexterity, 8);
            c.BaseStats.SetBaseStat(StatType.Constitution, 8);
            c.BaseStats.SetBaseStat(StatType.Intelligence, 16);
            c.BaseStats.SetBaseStat(StatType.Charisma, 12);
            c.BaseStats.SetBaseStat(StatType.Luck, 10);
            c.BaseStats.SetBaseStat(StatType.MaxHealth, 70);
            c.BaseStats.SetBaseStat(StatType.MaxMana, 80);
            c.BaseStats.SetBaseStat(StatType.Armor, 1);
            c.BaseStats.SetBaseStat(StatType.MoveSpeed, 5.5f);
            _classes[c.ClassName] = c;
        }

        private static void BuildRogue()
        {
            var c = new CrawlerClassData
            {
                ClassName = CrawlerClassName.Rogue,
                DisplayName = "Rogue",
                Description = "Fast, sneaky, and lucky. Crits hard, dodges harder, dies if caught.",
                PrimaryStat = StatType.Dexterity,
                SecondaryStat = StatType.Luck,
                HpPerLevel = 7f,
                ManaPerLevel = 3f,
                PrimaryStatPerLevel = 3f,
                SecondaryStatPerLevel = 2f,
                TreeStartNodeId = "start_Rogue",
                StartingAbilities = new() { "ability_backstab" }
            };
            c.BaseStats.SetBaseStat(StatType.Strength, 8);
            c.BaseStats.SetBaseStat(StatType.Dexterity, 16);
            c.BaseStats.SetBaseStat(StatType.Constitution, 8);
            c.BaseStats.SetBaseStat(StatType.Intelligence, 10);
            c.BaseStats.SetBaseStat(StatType.Charisma, 8);
            c.BaseStats.SetBaseStat(StatType.Luck, 14);
            c.BaseStats.SetBaseStat(StatType.MaxHealth, 80);
            c.BaseStats.SetBaseStat(StatType.MaxMana, 40);
            c.BaseStats.SetBaseStat(StatType.Armor, 2);
            c.BaseStats.SetBaseStat(StatType.CritChance, 8);
            c.BaseStats.SetBaseStat(StatType.CritDamage, 60);
            c.BaseStats.SetBaseStat(StatType.MoveSpeed, 7f);
            _classes[c.ClassName] = c;
        }

        private static void BuildNecroBard()
        {
            var c = new CrawlerClassData
            {
                ClassName = CrawlerClassName.NecroBard,
                DisplayName = "Necro-Bard",
                Description = "Raises the dead and plays sick tunes. A support-damage hybrid from the dark side.",
                PrimaryStat = StatType.Intelligence,
                SecondaryStat = StatType.Charisma,
                HpPerLevel = 7f,
                ManaPerLevel = 5f,
                PrimaryStatPerLevel = 2f,
                SecondaryStatPerLevel = 2f,
                TreeStartNodeId = "start_NecroBard",
                StartingAbilities = new() { "ability_dark_chord" }
            };
            c.BaseStats.SetBaseStat(StatType.Strength, 6);
            c.BaseStats.SetBaseStat(StatType.Dexterity, 10);
            c.BaseStats.SetBaseStat(StatType.Constitution, 10);
            c.BaseStats.SetBaseStat(StatType.Intelligence, 14);
            c.BaseStats.SetBaseStat(StatType.Charisma, 14);
            c.BaseStats.SetBaseStat(StatType.Luck, 8);
            c.BaseStats.SetBaseStat(StatType.MaxHealth, 80);
            c.BaseStats.SetBaseStat(StatType.MaxMana, 70);
            c.BaseStats.SetBaseStat(StatType.Armor, 2);
            c.BaseStats.SetBaseStat(StatType.MoveSpeed, 5.5f);
            _classes[c.ClassName] = c;
        }

        private static void BuildPugilist()
        {
            var c = new CrawlerClassData
            {
                ClassName = CrawlerClassName.Pugilist,
                DisplayName = "Pugilist",
                Description = "Fists only. Fast attacks, combo-focused, surprisingly tanky through sheer toughness.",
                PrimaryStat = StatType.Strength,
                SecondaryStat = StatType.Dexterity,
                HpPerLevel = 9f,
                ManaPerLevel = 2f,
                PrimaryStatPerLevel = 2f,
                SecondaryStatPerLevel = 2f,
                TreeStartNodeId = "start_Pugilist",
                StartingAbilities = new() { "ability_flurry" }
            };
            c.BaseStats.SetBaseStat(StatType.Strength, 14);
            c.BaseStats.SetBaseStat(StatType.Dexterity, 14);
            c.BaseStats.SetBaseStat(StatType.Constitution, 12);
            c.BaseStats.SetBaseStat(StatType.Intelligence, 6);
            c.BaseStats.SetBaseStat(StatType.Charisma, 8);
            c.BaseStats.SetBaseStat(StatType.Luck, 8);
            c.BaseStats.SetBaseStat(StatType.MaxHealth, 90);
            c.BaseStats.SetBaseStat(StatType.MaxMana, 25);
            c.BaseStats.SetBaseStat(StatType.Armor, 4);
            c.BaseStats.SetBaseStat(StatType.AttackSpeed, 1.5f);
            c.BaseStats.SetBaseStat(StatType.MoveSpeed, 6.5f);
            _classes[c.ClassName] = c;
        }
    }
}
