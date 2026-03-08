using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Pre-built enemy definitions for all sectors.
    /// Significant enemies carry signature gear that drops on death.
    /// Fodder enemies (Scrap Rat, Wire Worm, Rust Mite, Calibration Target) drop nothing.
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

            // Apply JSON overrides from Data/enemies.json (saved by BalanceEditor)
            RegistryOverrides.ApplyEnemies();
        }

        public static EnemyData GetEnemy(string id)
        {
            return _enemies.TryGetValue(id, out var data) ? data : null;
        }

        public static void RegisterEnemy(EnemyData data)
        {
            _enemies[data.Id] = data;
        }

        // --- Signature drop helpers ---

        private static EquipmentData MakeSignatureGun(string id, string stringKey, StatType stat, ModifierType mod, float value)
        {
            var equip = new EquipmentData(id, StringLoader.Get($"signatureDrops.{stringKey}"), ItemRarity.Common, EquipmentSlot.MainHand, 1)
            {
                WeaponType = WeaponType.Pistol
            };
            equip.AddBaseStat(stat, mod, value);
            return equip;
        }

        private static EquipmentData MakeSignatureBlade(string id, string stringKey, StatType stat, ModifierType mod, float value)
        {
            var equip = new EquipmentData(id, StringLoader.Get($"signatureDrops.{stringKey}"), ItemRarity.Common, EquipmentSlot.MainHand, 1)
            {
                WeaponType = WeaponType.BladeRing
            };
            equip.AddBaseStat(stat, mod, value);
            return equip;
        }

        private static EquipmentData MakeSignatureEquip(string id, string stringKey, EquipmentSlot slot, StatType stat, ModifierType mod, float value)
        {
            var equip = new EquipmentData(id, StringLoader.Get($"signatureDrops.{stringKey}"), ItemRarity.Common, slot, 1);
            equip.AddBaseStat(stat, mod, value);
            return equip;
        }

        // --- Fodder enemies (no drops) ---

        private static void BuildCalibrationTarget()
        {
            var e = new EnemyData("calibration_target", StringLoader.Get("enemies.calibration_target"), EnemyTier.Normal, 50, 0, 0, 5)
            {
                AttackRange = 0,
                AttackCooldown = 999f,
                AggroRange = 0,
                MeshColor = new Color(0.6f, 0.6f, 0.3f)
            };
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
            _enemies[e.Id] = e;
        }

        private static void BuildRustMite()
        {
            var e = new EnemyData("rust_mite", StringLoader.Get("enemies.rust_mite"), EnemyTier.Normal, 10, 3, 5.5f, 8)
            {
                AttackRange = 0.8f,
                AttackCooldown = 0.6f,
                AggroRange = 7f,
                Armor = 0,
                Behavior = EnemyBehavior.Swarm,
                MeshColor = new Color(0.6f, 0.3f, 0.15f)
            };
            _enemies[e.Id] = e;
        }

        // --- Significant enemies (signature drops) ---

        private static void BuildSparkDrone()
        {
            var e = new EnemyData("spark_drone", StringLoader.Get("enemies.spark_drone"), EnemyTier.Normal, 20, 6, 2.5f, 18)
            {
                AttackRange = 8f,
                AttackCooldown = 1.8f,
                AggroRange = 10f,
                Armor = 0,
                Behavior = EnemyBehavior.Ranged,
                MeshColor = new Color(0.3f, 0.6f, 0.9f),
                SignatureDrop = MakeSignatureGun("sig_spark_emitter", "spark_emitter", StatType.Dexterity, ModifierType.Flat, 5f),
                SignatureDropChance = 0.20f,
                LootBoxDrop = LootBoxTier.Bronze,
                LootBoxDropChance = 0.10f
            };
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
                MeshColor = new Color(0.2f, 0.5f, 0.2f),
                SignatureDrop = MakeSignatureBlade("sig_lurkers_shiv", "lurkers_shiv", StatType.Strength, ModifierType.Flat, 5f),
                SignatureDropChance = 0.20f,
                LootBoxDrop = LootBoxTier.Bronze,
                LootBoxDropChance = 0.10f
            };
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
                MeshColor = new Color(0.3f, 0.9f, 0.5f),
                SignatureDrop = MakeSignatureEquip("sig_repair_module", "repair_module", EquipmentSlot.Back, StatType.CooldownReduction, ModifierType.Flat, 0.08f),
                SignatureDropChance = 0.25f,
                LootBoxDrop = LootBoxTier.Bronze,
                LootBoxDropChance = 0.15f
            };
            _enemies[e.Id] = e;
        }

        private static void BuildVoltSprinter()
        {
            var e = new EnemyData("volt_sprinter", StringLoader.Get("enemies.volt_sprinter"), EnemyTier.Normal, 22, 10, 7f, 22)
            {
                AttackRange = 1.5f,
                AttackCooldown = 2.5f,
                AggroRange = 12f,
                Armor = 0,
                Behavior = EnemyBehavior.Charger,
                MeshColor = new Color(0.9f, 0.8f, 0.1f),
                SignatureDrop = MakeSignatureEquip("sig_volt_treads", "volt_treads", EquipmentSlot.Feet, StatType.MoveSpeed, ModifierType.Flat, 0.8f),
                SignatureDropChance = 0.20f,
                LootBoxDrop = LootBoxTier.Bronze,
                LootBoxDropChance = 0.10f
            };
            _enemies[e.Id] = e;
        }

        private static void BuildShardLobber()
        {
            var e = new EnemyData("shard_lobber", StringLoader.Get("enemies.shard_lobber"), EnemyTier.Normal, 28, 8, 2f, 25)
            {
                AttackRange = 10f,
                AttackCooldown = 2.2f,
                AggroRange = 12f,
                Armor = 1,
                Behavior = EnemyBehavior.Ranged,
                MeshColor = new Color(0.7f, 0.4f, 0.5f),
                SignatureDrop = MakeSignatureGun("sig_shard_cannon", "shard_cannon", StatType.Intelligence, ModifierType.Flat, 7f),
                SignatureDropChance = 0.25f,
                LootBoxDrop = LootBoxTier.Bronze,
                LootBoxDropChance = 0.15f
            };
            _enemies[e.Id] = e;
        }

        private static void BuildGlitchPhantom()
        {
            var e = new EnemyData("glitch_phantom", StringLoader.Get("enemies.glitch_phantom"), EnemyTier.Normal, 16, 12, 4f, 30)
            {
                AttackRange = 1.2f,
                AttackCooldown = 3.0f,
                AggroRange = 15f,
                Armor = 0,
                Behavior = EnemyBehavior.Flanker,
                MeshColor = new Color(0.5f, 0.2f, 0.7f),
                SignatureDrop = MakeSignatureEquip("sig_phase_cloak", "phase_cloak", EquipmentSlot.Back, StatType.CritChance, ModifierType.Flat, 0.04f),
                SignatureDropChance = 0.25f,
                LootBoxDrop = LootBoxTier.Bronze,
                LootBoxDropChance = 0.15f
            };
            _enemies[e.Id] = e;
        }

        private static void BuildOverclockDrone()
        {
            var e = new EnemyData("overclock_drone", StringLoader.Get("enemies.overclock_drone"), EnemyTier.Normal, 20, 3, 3f, 28)
            {
                AttackRange = 8f,
                AttackCooldown = 4.0f,
                AggroRange = 14f,
                Armor = 1,
                Behavior = EnemyBehavior.Healer,
                MeshColor = new Color(0.9f, 0.5f, 0.2f),
                SignatureDrop = MakeSignatureEquip("sig_overclock_core", "overclock_core", EquipmentSlot.Amulet, StatType.MaxMana, ModifierType.Flat, 20f),
                SignatureDropChance = 0.25f,
                LootBoxDrop = LootBoxTier.Bronze,
                LootBoxDropChance = 0.15f
            };
            _enemies[e.Id] = e;
        }

        // --- Elite enemies (higher drop rates, Silver boxes) ---

        private static void BuildDecoyUnit()
        {
            var e = new EnemyData("decoy_unit", StringLoader.Get("enemies.decoy_unit"), EnemyTier.Elite, 80, 12, 2f, 50)
            {
                AttackRange = 1.5f,
                AttackCooldown = 2.0f,
                AggroRange = 4f,
                Armor = 5,
                MeshColor = new Color(0.8f, 0.7f, 0.2f),
                SignatureDrop = MakeSignatureEquip("sig_decoy_barrier", "decoy_barrier", EquipmentSlot.OffHand, StatType.Armor, ModifierType.Flat, 5f),
                SignatureDropChance = 0.40f,
                LootBoxDrop = LootBoxTier.Silver,
                LootBoxDropChance = 0.30f
            };
            _enemies[e.Id] = e;
        }

        private static void BuildScrapGolem()
        {
            var e = new EnemyData("scrap_golem", StringLoader.Get("enemies.scrap_golem"), EnemyTier.Elite, 100, 8, 1.5f, 60)
            {
                AttackRange = 1.8f,
                AttackCooldown = 2.5f,
                AggroRange = 6f,
                Armor = 8,
                Behavior = EnemyBehavior.Tank,
                MeshColor = new Color(0.45f, 0.4f, 0.35f),
                SignatureDrop = MakeSignatureEquip("sig_golem_plate", "golem_plate", EquipmentSlot.Chest, StatType.Armor, ModifierType.Flat, 6f),
                SignatureDropChance = 0.40f,
                LootBoxDrop = LootBoxTier.Silver,
                LootBoxDropChance = 0.30f
            };
            _enemies[e.Id] = e;
        }

        // --- MiniBoss (guaranteed drops, Gold box) ---

        private static void BuildAxisDisciple()
        {
            var e = new EnemyData("axis_disciple", StringLoader.Get("enemies.axis_disciple"), EnemyTier.MiniBoss, 200, 15, 3.5f, 100)
            {
                AttackRange = 1.8f,
                AttackCooldown = 1.5f,
                AggroRange = 18f,
                Armor = 6,
                Behavior = EnemyBehavior.Melee,
                MeshColor = new Color(0.6f, 0.05f, 0.1f),
                SignatureDrop = MakeSignatureBlade("sig_axis_crimson_blade", "axis_crimson_blade", StatType.Strength, ModifierType.Flat, 10f),
                SignatureDropChance = 1.0f,
                LootBoxDrop = LootBoxTier.Gold,
                LootBoxDropChance = 1.0f
            };
            _enemies[e.Id] = e;
        }
    }
}
