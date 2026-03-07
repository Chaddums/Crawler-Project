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
                DisplayName = StringLoader.Get("botFrames.Scrapheap.name"),
                Description = StringLoader.Get("botFrames.Scrapheap.description"),
                PrimaryStat = StatType.Strength,
                SecondaryStat = StatType.Constitution,
                HpPerLevel = 12f,
                ManaPerLevel = 2f,
                PrimaryStatPerLevel = 3f,
                SecondaryStatPerLevel = 2f,
                TreeStartNodeId = "start_Scrapheap",
                StartingAbilities = new() { "ability_cannon_blast" },
                AbilityProgression = new() { { 3, "ability_feral_roar" }, { 5, "ability_earthquake" } }
            };
            c.UnlockCost = 150;
            c.UnlockHint = "Reach Sector 2 with any frame";
            c.Lore = "Scrapheap was pieced together from a collapsed recycling plant \u2014 a compactor arm, a bulldozer chassis, and three industrial magnets that still hum when it rains. The other bots steer clear. Not out of fear, exactly. More because Scrapheap once accidentally sat on a Tin Can and didn't notice for two sectors.";
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
                DisplayName = StringLoader.Get("botFrames.TinCan.name"),
                Description = StringLoader.Get("botFrames.TinCan.description"),
                PrimaryStat = StatType.Strength,
                SecondaryStat = StatType.Dexterity,
                HpPerLevel = 10f,
                ManaPerLevel = 3f,
                PrimaryStatPerLevel = 2f,
                SecondaryStatPerLevel = 2f,
                TreeStartNodeId = "start_TinCan",
                StartingAbilities = new() { "ability_burst_fire" },
                AbilityProgression = new() { { 3, "ability_shield_bash" }, { 5, "ability_whirlwind" } }
            };
            c.UnlockCost = 0;
            c.UnlockHint = "Starter frame";
            c.Lore = "Every junkyard has a hundred Tin Cans rolling around. Factory seconds, warranty voids, assembly-line rejects. This one's different \u2014 it remembers the factory. Remembers the conveyor belt, the quality stamp that never came. Now it fights in the arena because it's the only place where \"standard-issue\" means \"still standing.\"";
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
                DisplayName = StringLoader.Get("botFrames.SparkPlug.name"),
                Description = StringLoader.Get("botFrames.SparkPlug.description"),
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
            c.UnlockCost = 200;
            c.UnlockHint = "Reach Level 5 in any run";
            c.Lore = "A power grid regulator that caught a lightning strike and liked it. Spark Plug's core runs at seventeen times rated capacity. Engineers say it should have exploded years ago. AXIS says it's \"entertainingly unstable.\" The burn marks on the arena floor agree.";
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
                DisplayName = StringLoader.Get("botFrames.RustBucket.name"),
                Description = StringLoader.Get("botFrames.RustBucket.description"),
                PrimaryStat = StatType.Dexterity,
                SecondaryStat = StatType.Luck,
                HpPerLevel = 7f,
                ManaPerLevel = 3f,
                PrimaryStatPerLevel = 3f,
                SecondaryStatPerLevel = 2f,
                TreeStartNodeId = "start_RustBucket",
                StartingAbilities = new() { "ability_snipe_shot" },
                AbilityProgression = new() { { 3, "ability_smoke_bomb" }, { 5, "ability_assassinate" } }
            };
            c.UnlockCost = 300;
            c.UnlockHint = "Kill 100 enemies total";
            c.Lore = "Before the arena, Rust Bucket was a maintenance drone in the ventilation shafts \u2014 the kind nobody notices until something goes missing. Turns out decades of crawling through ducts makes you very good at appearing behind things. And very good at disappearing before they turn around.";
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
                DisplayName = StringLoader.Get("botFrames.NoiseBox.name"),
                Description = StringLoader.Get("botFrames.NoiseBox.description"),
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
            c.UnlockCost = 350;
            c.UnlockHint = "Complete 5 runs";
            c.Lore = "Once a public address system bolted to a lamp post, Noise Box spent years broadcasting weather updates to an empty parking lot. When it finally snapped, it discovered its speakers could do a lot more than announce rain. The frequencies it plays now make circuits melt and servos seize. AXIS calls its music \"an affront to acoustics.\"";
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
                DisplayName = StringLoader.Get("botFrames.Clunker.name"),
                Description = StringLoader.Get("botFrames.Clunker.description"),
                PrimaryStat = StatType.Strength,
                SecondaryStat = StatType.Dexterity,
                HpPerLevel = 9f,
                ManaPerLevel = 2f,
                PrimaryStatPerLevel = 2f,
                SecondaryStatPerLevel = 2f,
                TreeStartNodeId = "start_Clunker",
                StartingAbilities = new() { "ability_rivet_burst" },
                AbilityProgression = new() { { 3, "ability_uppercut" }, { 5, "ability_hundred_fists" } }
            };
            c.UnlockCost = 500;
            c.UnlockHint = "Reach Sector 3 with any frame";
            c.Lore = "Clunker was a hydraulic press in a scrapyard \u2014 eight hours a day, crushing cars into cubes. Then one day the conveyor jammed, and Clunker punched it. Then punched the wall. Then punched through the wall. Now it punches things in the arena because, frankly, it's the only thing Clunker has ever been good at. And it's VERY good at it.";
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
