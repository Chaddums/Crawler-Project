using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Live editor for signal propagation and economy constants.
    /// These are the "feel" knobs — signal speed, buff decay, gate timing, etc.
    /// Changes modify Constants at runtime via reflection-free property wrappers.
    /// </summary>
    public partial class SignalTuningEditor : EditorModule
    {
        public override string ModuleName => "Signals & Economy";
        public override Color AccentColor => EditorStyles.AccentSignals;

        private VBoxContainer _content;

        // Runtime-mutable copies of constants (since const fields can't be changed)
        // These get read by systems that check TuningOverrides first
        public static float SignalTravelSpeed = Constants.SIGNAL_TRAVEL_SPEED;
        public static float SignalBuffDecay = Constants.SIGNAL_BUFF_DECAY;
        public static float GateInputWindow = Constants.GATE_INPUT_WINDOW;
        public static float SwitchToggleTime = Constants.SWITCH_TOGGLE_TIME;
        public static float DelayDuration = Constants.DELAY_DURATION;
        public static float TimerInterval = Constants.TIMER_INTERVAL;
        public static float SensorRange = Constants.SENSOR_RANGE;        // 7
        public static float DamageTowerRange = Constants.DAMAGE_TOWER_RANGE; // 8
        public static float DamageTowerDPS = Constants.DAMAGE_TOWER_DPS;     // 12
        public static float SlowFieldRange = Constants.SLOW_FIELD_RANGE;     // 5
        public static float SlowFieldAmount = Constants.SLOW_FIELD_AMOUNT;
        public static float BuffDamageBonus = Constants.BUFF_DAMAGE_BONUS;
        public static float BuffSpeedBonus = Constants.BUFF_SPEED_BONUS;
        public static int StartingGold = Constants.VINE_STARTING_GOLD;
        public static int WaveBonus = Constants.VINE_WAVE_BONUS;
        public static int CoreLives = Constants.VINE_CORE_LIVES;
        public static float EnemyBaseSpeed = Constants.VINE_ENEMY_BASE_SPEED;
        public static float SellRefund = Constants.TOWER_SELL_REFUND;

        // Meta perk target fields (reset each run)
        public static float PlayerMaxHPBonus = 0f;
        public static float PlayerAttackSpeedMult = 1f;
        public static float PlayerAttackDamageMult = 1f;
        public static float PlayerMaxManaBonus = 0f;
        public static float PlayerManaRegenMult = 1f;
        public static float HarvesterIncomeMult = 1f;
        public static int HarvesterIncomeBonus = 0;

        // Live player tuning — applied immediately to active VinePlayer
        public static float PlayerMoveSpeed = Constants.VINE_PLAYER_MOVE_SPEED;
        public static float PlayerAttackRange = Constants.VINE_PLAYER_ATTACK_RANGE;
        public static float PlayerModelScale = 1f;
        public static float PlayerMaxHP = Constants.VINE_PLAYER_MAX_HP;
        public static float PlayerAttackDamage = Constants.VINE_PLAYER_ATTACK_DAMAGE;
        public static float PlayerAttackSpeed = Constants.VINE_PLAYER_ATTACK_SPEED;
        public static float PlayerManaRegen = Constants.VINE_PLAYER_MANA_REGEN;
        public static float PlayerMaxMana = Constants.VINE_PLAYER_MAX_MANA;

        // Enemy tuning
        public static float EnemyHPScale = 1f;
        public static float EnemyDamageScale = 1f;
        public static float BossHPMultiplier = Constants.BOSS_HP_MULTIPLIER;
        public static float BossScale = Constants.BOSS_SCALE;

        // Harvester
        public static float HarvesterMaxHP = Constants.VINE_HARVESTER_MAX_HP;
        public static float HarvesterIncomeInterval = Constants.VINE_HARVESTER_INCOME_INTERVAL;

        /// <summary>
        /// Reset ALL static tuning fields to their Constants defaults.
        /// Called at the start of each run before meta perks are applied.
        /// </summary>
        public static void ResetToDefaults()
        {
            SignalTravelSpeed = Constants.SIGNAL_TRAVEL_SPEED;
            SignalBuffDecay = Constants.SIGNAL_BUFF_DECAY;
            GateInputWindow = Constants.GATE_INPUT_WINDOW;
            SwitchToggleTime = Constants.SWITCH_TOGGLE_TIME;
            DelayDuration = Constants.DELAY_DURATION;
            TimerInterval = Constants.TIMER_INTERVAL;
            SensorRange = Constants.SENSOR_RANGE;
            DamageTowerRange = Constants.DAMAGE_TOWER_RANGE;
            DamageTowerDPS = Constants.DAMAGE_TOWER_DPS;
            SlowFieldRange = Constants.SLOW_FIELD_RANGE;
            SlowFieldAmount = Constants.SLOW_FIELD_AMOUNT;
            BuffDamageBonus = Constants.BUFF_DAMAGE_BONUS;
            BuffSpeedBonus = Constants.BUFF_SPEED_BONUS;
            StartingGold = Constants.VINE_STARTING_GOLD;
            WaveBonus = Constants.VINE_WAVE_BONUS;
            CoreLives = Constants.VINE_CORE_LIVES;
            EnemyBaseSpeed = Constants.VINE_ENEMY_BASE_SPEED;
            SellRefund = Constants.TOWER_SELL_REFUND;

            // Meta perk fields
            PlayerMaxHPBonus = 0f;
            PlayerAttackSpeedMult = 1f;
            PlayerAttackDamageMult = 1f;
            PlayerMaxManaBonus = 0f;
            PlayerManaRegenMult = 1f;
            HarvesterIncomeMult = 1f;
            HarvesterIncomeBonus = 0;

            // Live player tuning
            PlayerMoveSpeed = Constants.VINE_PLAYER_MOVE_SPEED;
            PlayerAttackRange = Constants.VINE_PLAYER_ATTACK_RANGE;
            PlayerModelScale = 1f;
            PlayerMaxHP = Constants.VINE_PLAYER_MAX_HP;
            PlayerAttackDamage = Constants.VINE_PLAYER_ATTACK_DAMAGE;
            PlayerAttackSpeed = Constants.VINE_PLAYER_ATTACK_SPEED;
            PlayerManaRegen = Constants.VINE_PLAYER_MANA_REGEN;
            PlayerMaxMana = Constants.VINE_PLAYER_MAX_MANA;

            // Enemy tuning
            EnemyHPScale = 1f;
            EnemyDamageScale = 1f;
            BossHPMultiplier = Constants.BOSS_HP_MULTIPLIER;
            BossScale = Constants.BOSS_SCALE;

            // Harvester
            HarvesterMaxHP = Constants.VINE_HARVESTER_MAX_HP;
            HarvesterIncomeInterval = Constants.VINE_HARVESTER_INCOME_INTERVAL;
        }

        public override void _Ready()
        {
            BuildUI();
        }

        private void BuildUI()
        {
            var scroll = new ScrollContainer();
            scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            AddChild(scroll);

            _content = new VBoxContainer();
            _content.AddThemeConstantOverride("separation", 4);
            _content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            scroll.AddChild(_content);

            // ── Signal Propagation ──
            AddSectionHeader("Signal Propagation");
            AddTuningRow("Signal Travel Speed", SignalTravelSpeed, 1f, 20f, 0.5f,
                v => SignalTravelSpeed = (float)v,
                "Units/sec along vine. Higher = snappier response, lower = more timing play.");
            AddTuningRow("Buff Decay per Hop", SignalBuffDecay, 0f, 0.5f, 0.01f,
                v => SignalBuffDecay = (float)v,
                "Fraction of buff lost each hop. 0 = no decay, 0.5 = halved each hop.");
            AddTuningRow("Gate Input Window", GateInputWindow, 0.1f, 3f, 0.1f,
                v => GateInputWindow = (float)v,
                "Seconds both inputs must fire within for AND gate to open.");
            AddTuningRow("Switch Toggle Time", SwitchToggleTime, 0.5f, 10f, 0.5f,
                v => SwitchToggleTime = (float)v,
                "Default auto-toggle interval for switches.");
            AddTuningRow("Delay Duration", DelayDuration, 0.5f, 10f, 0.5f,
                v => DelayDuration = (float)v,
                "How long Delay nodes hold a signal before passing.");
            AddTuningRow("Timer Interval", TimerInterval, 0.5f, 15f, 0.5f,
                v => TimerInterval = (float)v,
                "Default fire interval for Timer nodes.");

            // ── Ranges ──
            AddSectionHeader("Node Ranges");
            AddTuningRow("Sensor Range", SensorRange, 1f, 12f, 0.5f,
                v => SensorRange = (float)v,
                "Detection range for all sensor types.");
            AddTuningRow("Damage Tower Range", DamageTowerRange, 1f, 15f, 0.5f,
                v => DamageTowerRange = (float)v,
                "Firing range for Junk Turrets.");
            AddTuningRow("Damage Tower DPS", DamageTowerDPS, 1f, 50f, 1f,
                v => DamageTowerDPS = (float)v,
                "Base damage per second when active.");
            AddTuningRow("Slow Field Range", SlowFieldRange, 1f, 8f, 0.5f,
                v => SlowFieldRange = (float)v,
                "Area of effect for Tar Sprayer.");
            AddTuningRow("Slow Field Amount", SlowFieldAmount, 0.1f, 0.9f, 0.05f,
                v => SlowFieldAmount = (float)v,
                "Fraction of speed removed. 0.4 = 40% slow.");

            // ── Buffs ──
            AddSectionHeader("Buff System");
            AddTuningRow("Buff Damage Bonus", BuffDamageBonus, 0.05f, 1f, 0.05f,
                v => BuffDamageBonus = (float)v,
                "+damage % per active buff on a tower.");
            AddTuningRow("Buff Speed Bonus", BuffSpeedBonus, 0.05f, 1f, 0.05f,
                v => BuffSpeedBonus = (float)v,
                "+fire rate % per active buff.");

            // ── Economy ──
            AddSectionHeader("Economy");
            AddTuningRow("Starting Gold", StartingGold, 20, 500, 10,
                v => StartingGold = (int)v,
                "Gold available at battle start.");
            AddTuningRow("Wave Bonus", WaveBonus, 0, 100, 5,
                v => WaveBonus = (int)v,
                "Gold awarded per wave clear.");
            AddTuningRow("Sell Refund %", SellRefund * 100, 10, 100, 5,
                v => SellRefund = (float)v / 100f,
                "Percentage of node cost refunded on sell.");

            // ── Core ──
            AddSectionHeader("Core Defense");
            AddTuningRow("Core Lives", CoreLives, 1, 50, 1,
                v => CoreLives = (int)v,
                "Enemies that can reach the core before defeat.");
            AddTuningRow("Enemy Base Speed", EnemyBaseSpeed, 0.5f, 10f, 0.5f,
                v => EnemyBaseSpeed = (float)v,
                "Base movement speed for standard enemies.");

            // ── Player (BIT) ──
            AddSectionHeader("Player (BIT)");
            AddTuningRow("Move Speed", PlayerMoveSpeed, 1f, 15f, 0.5f,
                v => { PlayerMoveSpeed = (float)v; PushPlayerStat(p => p.MoveSpeed = (float)v); },
                "BIT's movement speed.");
            AddTuningRow("Attack Speed", PlayerAttackSpeed, 0.5f, 10f, 0.1f,
                v => { PlayerAttackSpeed = (float)v; PushPlayerStat(p => p.AttackSpeed = (float)v); },
                "Attacks per second.");
            AddTuningRow("Attack Damage", PlayerAttackDamage, 1f, 100f, 1f,
                v => { PlayerAttackDamage = (float)v; PushPlayerStat(p => p.AttackDamage = (float)v); },
                "Base damage per hit.");
            AddTuningRow("Attack Range", PlayerAttackRange, 2f, 20f, 0.5f,
                v => { PlayerAttackRange = (float)v; PushPlayerStat(p => p.AttackRange = (float)v); },
                "Auto-attack targeting range.");
            AddTuningRow("Max HP", PlayerMaxHP, 10f, 500f, 10f,
                v => { PlayerMaxHP = (float)v; PushPlayerStat(p => {
                    p.MaxHP = (float)v;
                    GameEvents.OnPlayerHPChanged?.Invoke(p.CurrentHP, p.MaxHP);
                }); },
                "Maximum health. Changes take effect immediately.");
            AddTuningRow("Max Mana", PlayerMaxMana, 10f, 500f, 10f,
                v => { PlayerMaxMana = (float)v; PushPlayerStat(p => {
                    p.MaxMana = (float)v;
                    GameEvents.OnPlayerManaChanged?.Invoke(p.CurrentMana, p.MaxMana);
                }); },
                "Maximum mana pool.");
            AddTuningRow("Mana Regen", PlayerManaRegen, 0.5f, 20f, 0.5f,
                v => { PlayerManaRegen = (float)v; PushPlayerStat(p => p.ManaRegen = (float)v); },
                "Mana regenerated per second.");
            AddTuningRow("Model Scale", PlayerModelScale, 0.3f, 3f, 0.1f,
                v => { PlayerModelScale = (float)v; PushPlayerStat(p => {
                    if (p.ModelRoot != null)
                        p.ModelRoot.Scale = Vector3.One * p._baseModelScale * (float)v;
                }); },
                "Visual size multiplier for BIT.");

            // ── Enemies ──
            AddSectionHeader("Enemies");
            AddTuningRow("Enemy HP Scale", EnemyHPScale, 0.1f, 5f, 0.1f,
                v => EnemyHPScale = (float)v,
                "Multiplier on all enemy HP. Applied to new spawns.");
            AddTuningRow("Enemy Damage Scale", EnemyDamageScale, 0.1f, 5f, 0.1f,
                v => EnemyDamageScale = (float)v,
                "Multiplier on enemy damage (if applicable).");
            AddTuningRow("Boss HP Multiplier", BossHPMultiplier, 1f, 20f, 0.5f,
                v => BossHPMultiplier = (float)v,
                "Boss HP = base enemy HP × this.");
            AddTuningRow("Boss Scale", BossScale, 1f, 5f, 0.25f,
                v => BossScale = (float)v,
                "Visual size of boss enemies.");

            // ── Harvester ──
            AddSectionHeader("Harvester");
            AddTuningRow("Harvester Max HP", HarvesterMaxHP, 50f, 1000f, 25f,
                v => HarvesterMaxHP = (float)v,
                "Harvester maximum health.");
            AddTuningRow("Income Interval", HarvesterIncomeInterval, 1f, 20f, 0.5f,
                v => HarvesterIncomeInterval = (float)v,
                "Seconds between harvester income ticks.");

            // Footer
            _content.AddChild(EditorStyles.MakeSeparator());
            _content.AddChild(EditorStyles.MakeLabel(
                "Values override Constants at runtime.\n" +
                "Reset by restarting the game.\n" +
                "Tip: Signal speed vs enemy speed is the most critical ratio to tune.",
                11, EditorStyles.TextMuted));
        }

        /// <summary>
        /// Push a single changed value to the live VinePlayer.
        /// Only modifies the specific stat that was changed, not all of them.
        /// </summary>
        private static void PushPlayerStat(System.Action<VinePlayer> apply)
        {
            if (!ServiceLocator.TryGet<VinePlayer>(out var player)) return;
            apply(player);
        }

        private void AddSectionHeader(string title)
        {
            _content.AddChild(EditorStyles.MakeSeparator());
            var label = EditorStyles.MakeLabel(title, 16, AccentColor);
            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_top", 8);
            margin.AddChild(label);
            _content.AddChild(margin);
        }

        private void AddTuningRow(string label, float value, float min, float max, float step,
            System.Action<double> onChange, string tooltip)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);

            var lbl = EditorStyles.MakeLabel(label, 13);
            lbl.CustomMinimumSize = new Vector2(180, 0);
            lbl.TooltipText = tooltip;
            row.AddChild(lbl);

            var spin = EditorStyles.MakeSpinBox(value, min, max, step);
            spin.ValueChanged += (double v) => onChange(v);
            spin.TooltipText = tooltip;
            row.AddChild(spin);

            // Tooltip hint text
            var hint = EditorStyles.MakeLabel(tooltip, 10, EditorStyles.TextMuted);
            hint.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            row.AddChild(hint);

            _content.AddChild(row);
        }
    }
}
