using System.Collections.Generic;

namespace JunkbotArena
{
    /// <summary>
    /// Builds and stores all 6 bot frames.
    /// </summary>
    public static class BotFrameRegistry
    {
        private static readonly Dictionary<BotFrameType, BotFrameData> _classes = new();
        private static bool _initialized;

        public static IReadOnlyDictionary<BotFrameType, BotFrameData> Classes => _classes;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            BuildScrapheap();
            BuildTinCan();
            BuildSparkPlug();
            BuildRustBucket();
            BuildNoiseBox();
            BuildClunker();

            Godot.GD.Print($"[BotFrameRegistry] Initialized {_classes.Count} classes");
        }

        public static BotFrameData GetClass(BotFrameType name)
        {
            return _classes.TryGetValue(name, out var data) ? data : null;
        }

        private static void BuildScrapheap()
        {
            var c = new BotFrameData
            {
                ClassName = BotFrameType.Scrapheap,
                DisplayName = "Scrapheap",
                Description = "A hulking junkbot welded from heavy scrap. Absorbs punishment and hits like a falling dumpster.",
                PrimaryStat = StatType.Strength,
                SecondaryStat = StatType.Constitution,
                HpPerLevel = 12f,
                ManaPerLevel = 2f,
                PrimaryStatPerLevel = 3f,
                SecondaryStatPerLevel = 2f,
                TreeStartNodeId = "start_Scrapheap",
                StartingAbilities = new() { "ability_slam" },
                AbilityProgression = new() { { 3, "ability_feral_roar" }, { 5, "ability_earthquake" } }
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

        private static void BuildTinCan()
        {
            var c = new BotFrameData
            {
                ClassName = BotFrameType.TinCan,
                DisplayName = "Tin Can",
                Description = "Standard-issue combat bot. Nothing fancy. Just reliable servos and a solid chassis.",
                PrimaryStat = StatType.Strength,
                SecondaryStat = StatType.Dexterity,
                HpPerLevel = 10f,
                ManaPerLevel = 3f,
                PrimaryStatPerLevel = 2f,
                SecondaryStatPerLevel = 2f,
                TreeStartNodeId = "start_TinCan",
                StartingAbilities = new() { "ability_strike" },
                AbilityProgression = new() { { 3, "ability_shield_bash" }, { 5, "ability_whirlwind" } }
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

        private static void BuildSparkPlug()
        {
            var c = new BotFrameData
            {
                ClassName = BotFrameType.SparkPlug,
                DisplayName = "Spark Plug",
                Description = "Overcharged energy core in a fragile frame. Devastating arc discharge, zero armor.",
                PrimaryStat = StatType.Intelligence,
                SecondaryStat = StatType.Charisma,
                HpPerLevel = 6f,
                ManaPerLevel = 6f,
                PrimaryStatPerLevel = 3f,
                SecondaryStatPerLevel = 1f,
                TreeStartNodeId = "start_SparkPlug",
                StartingAbilities = new() { "ability_arcane_bolt" },
                AbilityProgression = new() { { 3, "ability_frost_nova" }, { 5, "ability_meteor" } }
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

        private static void BuildRustBucket()
        {
            var c = new BotFrameData
            {
                ClassName = BotFrameType.RustBucket,
                DisplayName = "Rust Bucket",
                Description = "Lightweight stealth frame with active camouflage. Crits hard, dodges harder, shatters if caught.",
                PrimaryStat = StatType.Dexterity,
                SecondaryStat = StatType.Luck,
                HpPerLevel = 7f,
                ManaPerLevel = 3f,
                PrimaryStatPerLevel = 3f,
                SecondaryStatPerLevel = 2f,
                TreeStartNodeId = "start_RustBucket",
                StartingAbilities = new() { "ability_backstab" },
                AbilityProgression = new() { { 3, "ability_smoke_bomb" }, { 5, "ability_assassinate" } }
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

        private static void BuildNoiseBox()
        {
            var c = new BotFrameData
            {
                ClassName = BotFrameType.NoiseBox,
                DisplayName = "Noise Box",
                Description = "Signal-disruption chassis. Jams enemy targeting, buffs allies with resonance fields.",
                PrimaryStat = StatType.Intelligence,
                SecondaryStat = StatType.Charisma,
                HpPerLevel = 7f,
                ManaPerLevel = 5f,
                PrimaryStatPerLevel = 2f,
                SecondaryStatPerLevel = 2f,
                TreeStartNodeId = "start_NoiseBox",
                StartingAbilities = new() { "ability_dark_chord" },
                AbilityProgression = new() { { 3, "ability_raise_dead" }, { 5, "ability_death_ballad" } }
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

        private static void BuildClunker()
        {
            var c = new BotFrameData
            {
                ClassName = BotFrameType.Clunker,
                DisplayName = "Clunker",
                Description = "Piston-driven melee frame. Fast hydraulic fists, combo-focused, surprisingly durable.",
                PrimaryStat = StatType.Strength,
                SecondaryStat = StatType.Dexterity,
                HpPerLevel = 9f,
                ManaPerLevel = 2f,
                PrimaryStatPerLevel = 2f,
                SecondaryStatPerLevel = 2f,
                TreeStartNodeId = "start_Clunker",
                StartingAbilities = new() { "ability_flurry" },
                AbilityProgression = new() { { 3, "ability_uppercut" }, { 5, "ability_hundred_fists" } }
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
