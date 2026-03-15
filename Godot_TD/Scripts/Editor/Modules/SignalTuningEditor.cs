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

            // Footer
            _content.AddChild(EditorStyles.MakeSeparator());
            _content.AddChild(EditorStyles.MakeLabel(
                "Values override Constants at runtime.\n" +
                "Reset by restarting the game.\n" +
                "Tip: Signal speed vs enemy speed is the most critical ratio to tune.",
                11, EditorStyles.TextMuted));
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
