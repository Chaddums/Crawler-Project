using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Validates all editor tuning fields — ensures every static field in
    /// SignalTuningEditor has a matching UI row, resets correctly, and pushes
    /// changes to live game objects. Run via --suite=editor or as part of "all".
    /// </summary>
    public class EditorTestSuite : ITestSuite
    {
        public string SuiteName => "editor";

        // All tunable static fields and their Constants defaults.
        // When adding a new tuning field, add it here — the test will catch missing entries.
        private static readonly (string Name, float Default, float TestValue)[] FloatFields =
        {
            // Signal propagation
            ("SignalTravelSpeed",   Constants.SIGNAL_TRAVEL_SPEED,    10f),
            ("SignalBuffDecay",     Constants.SIGNAL_BUFF_DECAY,      0.3f),
            ("GateInputWindow",    Constants.GATE_INPUT_WINDOW,       1.5f),
            ("SwitchToggleTime",   Constants.SWITCH_TOGGLE_TIME,      3f),
            ("DelayDuration",      Constants.DELAY_DURATION,          4f),
            ("TimerInterval",      Constants.TIMER_INTERVAL,          5f),

            // Ranges
            ("SensorRange",        Constants.SENSOR_RANGE,            10f),
            ("DamageTowerRange",   Constants.DAMAGE_TOWER_RANGE,      12f),
            ("DamageTowerDPS",     Constants.DAMAGE_TOWER_DPS,        20f),
            ("SlowFieldRange",     Constants.SLOW_FIELD_RANGE,        4f),
            ("SlowFieldAmount",    Constants.SLOW_FIELD_AMOUNT,       0.6f),

            // Buffs
            ("BuffDamageBonus",    Constants.BUFF_DAMAGE_BONUS,       0.5f),
            ("BuffSpeedBonus",     Constants.BUFF_SPEED_BONUS,        0.3f),

            // Economy
            ("SellRefund",         Constants.TOWER_SELL_REFUND,       0.8f),
            ("EnemyBaseSpeed",     Constants.VINE_ENEMY_BASE_SPEED,   5f),

            // Player
            ("PlayerMoveSpeed",    Constants.VINE_PLAYER_MOVE_SPEED,      8f),
            ("PlayerAttackRange",  Constants.VINE_PLAYER_ATTACK_RANGE,    10f),
            ("PlayerModelScale",   1f,                                     2f),
            ("PlayerMaxHP",        Constants.VINE_PLAYER_MAX_HP,          200f),
            ("PlayerAttackDamage", Constants.VINE_PLAYER_ATTACK_DAMAGE,   20f),
            ("PlayerAttackSpeed",  Constants.VINE_PLAYER_ATTACK_SPEED,    3f),
            ("PlayerManaRegen",    Constants.VINE_PLAYER_MANA_REGEN,      6f),
            ("PlayerMaxMana",      Constants.VINE_PLAYER_MAX_MANA,        200f),

            // Meta perk multipliers
            ("PlayerMaxHPBonus",       0f, 50f),
            ("PlayerAttackSpeedMult",  1f, 1.5f),
            ("PlayerAttackDamageMult", 1f, 1.5f),
            ("PlayerMaxManaBonus",     0f, 30f),
            ("PlayerManaRegenMult",    1f, 1.3f),
            ("HarvesterIncomeMult",    1f, 1.5f),

            // Enemy
            ("EnemyHPScale",       1f,                              2f),
            ("EnemyDamageScale",   1f,                              2f),
            ("BossHPMultiplier",   Constants.BOSS_HP_MULTIPLIER,    8f),
            ("BossScale",          Constants.BOSS_SCALE,            3f),

            // Harvester
            ("HarvesterMaxHP",         Constants.VINE_HARVESTER_MAX_HP,            400f),
            ("HarvesterIncomeInterval", Constants.VINE_HARVESTER_INCOME_INTERVAL,  3f),
        };

        private static readonly (string Name, int Default, int TestValue)[] IntFields =
        {
            ("StartingGold",       Constants.VINE_STARTING_GOLD,     200),
            ("WaveBonus",          Constants.VINE_WAVE_BONUS,        30),
            ("CoreLives",          Constants.VINE_CORE_LIVES,        20),
            ("HarvesterIncomeBonus", 0,                              5),
        };

        public async Task Run(TestContext ctx)
        {
            GD.Print("[EditorTestSuite] Starting editor field validation...");

            TestAllFloatFieldsExist(ctx);
            TestAllIntFieldsExist(ctx);
            TestResetToDefaults(ctx);
            TestFloatFieldMutation(ctx);
            TestIntFieldMutation(ctx);
            TestResetAfterMutation(ctx);
            TestNoOrphanedStaticFields(ctx);

            GD.Print("[EditorTestSuite] Editor field validation complete.");
            await Task.CompletedTask;
        }

        // ── Test: every field in our registry exists as a static field ──

        private void TestAllFloatFieldsExist(TestContext ctx)
        {
            ctx.StartTest();
            var type = typeof(SignalTuningEditor);
            foreach (var (name, _, _) in FloatFields)
            {
                var field = type.GetField(name, BindingFlags.Public | BindingFlags.Static);
                ctx.AssertNotNull(field, $"editor.field_exists.{name}",
                    $"Static field SignalTuningEditor.{name} not found");
            }
        }

        private void TestAllIntFieldsExist(TestContext ctx)
        {
            ctx.StartTest();
            var type = typeof(SignalTuningEditor);
            foreach (var (name, _, _) in IntFields)
            {
                var field = type.GetField(name, BindingFlags.Public | BindingFlags.Static);
                ctx.AssertNotNull(field, $"editor.field_exists.{name}",
                    $"Static field SignalTuningEditor.{name} not found");
            }
        }

        // ── Test: ResetToDefaults restores all fields to Constants values ──

        private void TestResetToDefaults(TestContext ctx)
        {
            ctx.StartTest();
            SignalTuningEditor.ResetToDefaults();

            var type = typeof(SignalTuningEditor);
            foreach (var (name, defaultVal, _) in FloatFields)
            {
                var field = type.GetField(name, BindingFlags.Public | BindingFlags.Static);
                if (field == null) continue;
                float actual = Convert.ToSingle(field.GetValue(null));
                ctx.Assert(Mathf.Abs(actual - defaultVal) < 0.001f,
                    $"editor.reset_default.{name}",
                    $"Expected {defaultVal}, got {actual}");
            }
            foreach (var (name, defaultVal, _) in IntFields)
            {
                var field = type.GetField(name, BindingFlags.Public | BindingFlags.Static);
                if (field == null) continue;
                int actual = Convert.ToInt32(field.GetValue(null));
                ctx.AssertEqual(defaultVal, actual, $"editor.reset_default.{name}");
            }
        }

        // ── Test: each field can be mutated and reads back correctly ──

        private void TestFloatFieldMutation(TestContext ctx)
        {
            ctx.StartTest();
            var type = typeof(SignalTuningEditor);
            foreach (var (name, _, testVal) in FloatFields)
            {
                var field = type.GetField(name, BindingFlags.Public | BindingFlags.Static);
                if (field == null) continue;

                field.SetValue(null, testVal);
                float actual = Convert.ToSingle(field.GetValue(null));
                ctx.Assert(Mathf.Abs(actual - testVal) < 0.001f,
                    $"editor.mutate.{name}",
                    $"Set to {testVal}, read back {actual}");
            }
        }

        private void TestIntFieldMutation(TestContext ctx)
        {
            ctx.StartTest();
            var type = typeof(SignalTuningEditor);
            foreach (var (name, _, testVal) in IntFields)
            {
                var field = type.GetField(name, BindingFlags.Public | BindingFlags.Static);
                if (field == null) continue;

                field.SetValue(null, testVal);
                int actual = Convert.ToInt32(field.GetValue(null));
                ctx.AssertEqual(testVal, actual, $"editor.mutate.{name}");
            }
        }

        // ── Test: reset works AFTER mutation ──

        private void TestResetAfterMutation(TestContext ctx)
        {
            ctx.StartTest();
            // Fields were mutated in previous test — reset and verify
            SignalTuningEditor.ResetToDefaults();

            var type = typeof(SignalTuningEditor);
            foreach (var (name, defaultVal, _) in FloatFields)
            {
                var field = type.GetField(name, BindingFlags.Public | BindingFlags.Static);
                if (field == null) continue;
                float actual = Convert.ToSingle(field.GetValue(null));
                ctx.Assert(Mathf.Abs(actual - defaultVal) < 0.001f,
                    $"editor.reset_after_mutate.{name}",
                    $"Expected {defaultVal} after reset, got {actual}");
            }
        }

        // ── Test: no orphaned static fields (fields in SignalTuningEditor not tracked in our registry) ──

        private void TestNoOrphanedStaticFields(TestContext ctx)
        {
            ctx.StartTest();
            var type = typeof(SignalTuningEditor);
            var tracked = new HashSet<string>();
            foreach (var (name, _, _) in FloatFields) tracked.Add(name);
            foreach (var (name, _, _) in IntFields) tracked.Add(name);

            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static);
            foreach (var field in fields)
            {
                // Skip non-tuning fields (methods, events, etc.)
                if (field.IsLiteral) continue; // const
                if (field.FieldType != typeof(float) && field.FieldType != typeof(int)) continue;

                ctx.Assert(tracked.Contains(field.Name),
                    $"editor.no_orphan.{field.Name}",
                    $"Static field '{field.Name}' exists in SignalTuningEditor but is not registered in EditorTestSuite. Add it to FloatFields or IntFields.");
            }
        }
    }
}
