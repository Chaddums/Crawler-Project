using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Pre-built enemy definitions for all sectors.
    /// </summary>
    public static class EnemyRegistry
    {
        private static readonly Dictionary<string, EnemyData> _enemies = new();
        private static bool _initialized;

        public static IReadOnlyDictionary<string, EnemyData> Enemies => _enemies;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            BuildCalibrationTarget();
            BuildScrapRat();
            BuildDecoyUnit();
            BuildWireWorm();
            BuildSparkDrone();
            BuildJunkLurker();
            BuildPatchBot();
            BuildRustMite();
            BuildVoltSprinter();
            BuildShardLobber();
            BuildScrapGolem();
            BuildGlitchPhantom();
            BuildOverclockDrone();
            BuildAxisDisciple();

            BossRegistry.Initialize();

            GD.Print($"[EnemyRegistry] Initialized {_enemies.Count} enemy types");
        }

        public static EnemyData GetEnemy(string id)
        {
            return _enemies.TryGetValue(id, out var data) ? data : null;
        }

        public static void RegisterEnemy(EnemyData data)
        {
            _enemies[data.Id] = data;
        }

        private static void BuildCalibrationTarget()
        {
            var e = new EnemyData("calibration_target", StringLoader.Get("enemies.calibration_target"), EnemyTier.Normal, 50, 0, 0, 5)
            {
                AttackRange = 0,
                AttackCooldown = 999f,
                AggroRange = 0,
                MeshColor = new Color(0.6f, 0.6f, 0.3f)
            };

            e.LootTable = new LootTableData { Id = "loot_calibration", MinDrops = 0, MaxDrops = 1 };
            PopulateCalibrationTargetLoot(e.LootTable);

            _enemies[e.Id] = e;
        }

        private static void BuildScrapRat()
        {
            var e = new EnemyData("scrap_rat", StringLoader.Get("enemies.scrap_rat"), EnemyTier.Normal, 25, 4, 4.5f, 15)
            {
                AttackRange = 1.2f,
                AttackCooldown = 1.0f,
                AggroRange = 6f,
                Armor = 1,
                MeshColor = new Color(0.5f, 0.35f, 0.25f)
            };

            e.LootTable = new LootTableData { Id = "loot_scrap_rat", MinDrops = 0, MaxDrops = 1 };
            PopulateScrapRatLoot(e.LootTable);

            _enemies[e.Id] = e;
        }

        private static void BuildDecoyUnit()
        {
            var e = new EnemyData("decoy_unit", StringLoader.Get("enemies.decoy_unit"), EnemyTier.Elite, 80, 12, 2f, 50)
            {
                AttackRange = 1.5f,
                AttackCooldown = 2.0f,
                AggroRange = 4f,
                Armor = 5,
                MeshColor = new Color(0.8f, 0.7f, 0.2f)
            };

            e.LootTable = new LootTableData { Id = "loot_decoy", MinDrops = 1, MaxDrops = 3 };
            PopulateDecoyUnitLoot(e.LootTable);

            _enemies[e.Id] = e;
        }

        private static void BuildWireWorm()
        {
            var e = new EnemyData("wire_worm", StringLoader.Get("enemies.wire_worm"), EnemyTier.Normal, 15, 2, 2f, 8)
            {
                AttackRange = 1.0f,
                AttackCooldown = 1.5f,
                AggroRange = 5f,
                Armor = 0,
                MeshColor = new Color(0.4f, 0.7f, 0.3f)
            };

            e.LootTable = new LootTableData { Id = "loot_wire_worm", MinDrops = 0, MaxDrops = 1 };
            PopulateWireWormLoot(e.LootTable);

            _enemies[e.Id] = e;
        }

        private static void BuildSparkDrone()
        {
            var e = new EnemyData("spark_drone", StringLoader.Get("enemies.spark_drone"), EnemyTier.Normal, 20, 6, 2.5f, 18)
            {
                AttackRange = 8f,
                AttackCooldown = 1.8f,
                AggroRange = 10f,
                Armor = 0,
                Behavior = EnemyBehavior.Ranged,
                MeshColor = new Color(0.3f, 0.6f, 0.9f)
            };

            e.LootTable = new LootTableData { Id = "loot_spark_drone", MinDrops = 0, MaxDrops = 1 };
            PopulateScrapRatLoot(e.LootTable); // Reuse scrap rat loot for now
            _enemies[e.Id] = e;
        }

        private static void BuildJunkLurker()
        {
            var e = new EnemyData("junk_lurker", StringLoader.Get("enemies.junk_lurker"), EnemyTier.Normal, 18, 7, 6f, 20)
            {
                AttackRange = 1.2f,
                AttackCooldown = 0.8f,
                AggroRange = 8f,
                Armor = 0,
                Behavior = EnemyBehavior.Flanker,
                MeshColor = new Color(0.2f, 0.5f, 0.2f)
            };

            e.LootTable = new LootTableData { Id = "loot_junk_lurker", MinDrops = 0, MaxDrops = 1 };
            PopulateScrapRatLoot(e.LootTable);
            _enemies[e.Id] = e;
        }

        private static void BuildPatchBot()
        {
            var e = new EnemyData("patch_bot", StringLoader.Get("enemies.patch_bot"), EnemyTier.Normal, 30, 2, 3f, 25)
            {
                AttackRange = 6f,
                AttackCooldown = 3.0f,
                AggroRange = 12f,
                Armor = 2,
                Behavior = EnemyBehavior.Healer,
                MeshColor = new Color(0.3f, 0.9f, 0.5f)
            };

            e.LootTable = new LootTableData { Id = "loot_patch_bot", MinDrops = 0, MaxDrops = 2 };
            PopulateDecoyUnitLoot(e.LootTable);
            _enemies[e.Id] = e;
        }

        private static void BuildRustMite()
        {
            // Tiny, fast swarm enemy — low HP, fast attack, appears in packs
            var e = new EnemyData("rust_mite", StringLoader.Get("enemies.rust_mite"), EnemyTier.Normal, 10, 3, 5.5f, 8)
            {
                AttackRange = 0.8f,
                AttackCooldown = 0.6f,
                AggroRange = 7f,
                Armor = 0,
                Behavior = EnemyBehavior.Swarm,
                MeshColor = new Color(0.6f, 0.3f, 0.15f)
            };
            e.LootTable = new LootTableData { Id = "loot_rust_mite", MinDrops = 0, MaxDrops = 1 };
            PopulateWireWormLoot(e.LootTable);
            _enemies[e.Id] = e;
        }

        private static void BuildVoltSprinter()
        {
            // Fast charger — dashes at player, high burst damage, fragile
            var e = new EnemyData("volt_sprinter", StringLoader.Get("enemies.volt_sprinter"), EnemyTier.Normal, 22, 10, 7f, 22)
            {
                AttackRange = 1.5f,
                AttackCooldown = 2.5f,
                AggroRange = 12f,
                Armor = 0,
                Behavior = EnemyBehavior.Charger,
                MeshColor = new Color(0.9f, 0.8f, 0.1f)
            };
            e.LootTable = new LootTableData { Id = "loot_volt_sprinter", MinDrops = 0, MaxDrops = 1 };
            PopulateScrapRatLoot(e.LootTable);
            _enemies[e.Id] = e;
        }

        private static void BuildShardLobber()
        {
            // Mid-range artillery — lobs slow projectiles, area threat
            var e = new EnemyData("shard_lobber", StringLoader.Get("enemies.shard_lobber"), EnemyTier.Normal, 28, 8, 2f, 25)
            {
                AttackRange = 10f,
                AttackCooldown = 2.2f,
                AggroRange = 12f,
                Armor = 1,
                Behavior = EnemyBehavior.Ranged,
                MeshColor = new Color(0.7f, 0.4f, 0.5f)
            };
            e.LootTable = new LootTableData { Id = "loot_shard_lobber", MinDrops = 0, MaxDrops = 1 };
            PopulateScrapRatLoot(e.LootTable);
            _enemies[e.Id] = e;
        }

        private static void BuildScrapGolem()
        {
            // Heavy tank — slow, high HP, high armor, draws attention
            var e = new EnemyData("scrap_golem", StringLoader.Get("enemies.scrap_golem"), EnemyTier.Elite, 100, 8, 1.5f, 60)
            {
                AttackRange = 1.8f,
                AttackCooldown = 2.5f,
                AggroRange = 6f,
                Armor = 8,
                Behavior = EnemyBehavior.Tank,
                MeshColor = new Color(0.45f, 0.4f, 0.35f)
            };
            e.LootTable = new LootTableData { Id = "loot_scrap_golem", MinDrops = 1, MaxDrops = 2 };
            PopulateDecoyUnitLoot(e.LootTable);
            _enemies[e.Id] = e;
        }

        private static void BuildGlitchPhantom()
        {
            // Teleporting flanker — appears behind player, hits and vanishes
            var e = new EnemyData("glitch_phantom", StringLoader.Get("enemies.glitch_phantom"), EnemyTier.Normal, 16, 12, 4f, 30)
            {
                AttackRange = 1.2f,
                AttackCooldown = 3.0f,
                AggroRange = 15f,
                Armor = 0,
                Behavior = EnemyBehavior.Flanker,
                MeshColor = new Color(0.5f, 0.2f, 0.7f)
            };
            e.LootTable = new LootTableData { Id = "loot_glitch_phantom", MinDrops = 0, MaxDrops = 1 };
            PopulateScrapRatLoot(e.LootTable);
            _enemies[e.Id] = e;
        }

        private static void BuildOverclockDrone()
        {
            // Support enemy — buffs nearby allies, priority target
            var e = new EnemyData("overclock_drone", StringLoader.Get("enemies.overclock_drone"), EnemyTier.Normal, 20, 3, 3f, 28)
            {
                AttackRange = 8f,
                AttackCooldown = 4.0f,
                AggroRange = 14f,
                Armor = 1,
                Behavior = EnemyBehavior.Healer,
                MeshColor = new Color(0.9f, 0.5f, 0.2f)
            };
            e.LootTable = new LootTableData { Id = "loot_overclock_drone", MinDrops = 0, MaxDrops = 2 };
            PopulateScrapRatLoot(e.LootTable);
            _enemies[e.Id] = e;
        }

        private static void BuildAxisDisciple()
        {
            // Rare unique encounter — direct servant of AXIS, tough mini-boss
            // Only spawns via 5% per-floor roll, one room per floor
            var e = new EnemyData("axis_disciple", StringLoader.Get("enemies.axis_disciple"), EnemyTier.MiniBoss, 200, 15, 3.5f, 100)
            {
                AttackRange = 1.8f,
                AttackCooldown = 1.5f,
                AggroRange = 18f,
                Armor = 6,
                Behavior = EnemyBehavior.Melee,
                MeshColor = new Color(0.6f, 0.05f, 0.1f) // Dark crimson — AXIS's chosen
            };
            e.LootTable = new LootTableData { Id = "loot_axis_disciple", MinDrops = 2, MaxDrops = 4 };
            PopulateDecoyUnitLoot(e.LootTable); // Elite-tier loot as base drops
            _enemies[e.Id] = e;
        }

        // --- Loot table population ---

        private static void PopulateWireWormLoot(LootTableData table)
        {
            // Weakest enemy: mostly small potions, very rare gear
            table.AddEntry(ConsumableRegistry.Get("potion_health_small"), weight: 150);
            table.AddEntry(ConsumableRegistry.Get("potion_mana_small"), weight: 100);
            foreach (var equip in BaseItemPool.Equipment)
                table.AddEntry(equip, weight: 5);
        }

        private static void PopulateScrapRatLoot(LootTableData table)
        {
            // Small/medium potions + occasional common gear
            table.AddEntry(ConsumableRegistry.Get("potion_health_small"), weight: 120);
            table.AddEntry(ConsumableRegistry.Get("potion_health_medium"), weight: 40);
            table.AddEntry(ConsumableRegistry.Get("potion_mana_small"), weight: 100);
            table.AddEntry(ConsumableRegistry.Get("potion_mana_medium"), weight: 30);
            foreach (var equip in BaseItemPool.Equipment)
                table.AddEntry(equip, weight: 10);
        }

        private static void PopulateDecoyUnitLoot(LootTableData table)
        {
            // Elite: medium potions + uncommon/rare gear
            table.AddEntry(ConsumableRegistry.Get("potion_health_medium"), weight: 100);
            table.AddEntry(ConsumableRegistry.Get("potion_health_large"), weight: 30);
            table.AddEntry(ConsumableRegistry.Get("potion_mana_medium"), weight: 80);
            table.AddEntry(ConsumableRegistry.Get("potion_mana_large"), weight: 20);
            table.AddEntry(ConsumableRegistry.Get("elixir_fortitude"), weight: 15);
            table.AddEntry(ConsumableRegistry.Get("overclock_injector"), weight: 15);
            foreach (var equip in BaseItemPool.Equipment)
                table.AddEntry(equip, weight: 25);
        }

        private static void PopulateCalibrationTargetLoot(LootTableData table)
        {
            // Calibration target: a bit of everything
            table.AddEntry(ConsumableRegistry.Get("potion_health_small"), weight: 100);
            table.AddEntry(ConsumableRegistry.Get("potion_mana_small"), weight: 100);
            foreach (var equip in BaseItemPool.Equipment)
                table.AddEntry(equip, weight: 8);
        }
    }
}
