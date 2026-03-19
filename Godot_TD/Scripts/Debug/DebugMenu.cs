using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// In-game debug menu toggled with ~ (tilde/backtick).
    /// Quick buttons for common debug actions. Type commands in the input bar.
    /// </summary>
    public partial class DebugMenu : CanvasLayer
    {
        private PanelContainer _panel;
        private LineEdit _input;
        private Label _feedback;
        private bool _visible;
        private double _feedbackTimer;

        // Cheat state
        private static bool _godMode;
        private static bool _instantKill;
        public static bool GodMode => _godMode;
        public static bool InstantKill => _instantKill;

        public override void _Ready()
        {
            Layer = 99;
            ProcessMode = ProcessModeEnum.Always;
            BuildUI();
            _panel.Visible = false;
        }

        public override void _Process(double delta)
        {
            if (_feedbackTimer > 0)
            {
                _feedbackTimer -= delta;
                if (_feedbackTimer <= 0) _feedback.Text = "";
            }
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && !key.Echo)
            {
                if (key.Keycode == Key.Quoteleft) // ~ tilde
                {
                    _visible = !_visible;
                    _panel.Visible = _visible;
                    if (_visible) _input.GrabFocus();
                    GetViewport().SetInputAsHandled();
                }
            }
        }

        private void BuildUI()
        {
            _panel = new PanelContainer();
            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.05f, 0.05f, 0.08f, 0.92f);
            style.BorderColor = new Color(0.3f, 0.7f, 0.9f, 0.6f);
            style.SetBorderWidthAll(1);
            style.SetContentMarginAll(10);
            style.CornerRadiusBottomLeft = 4;
            style.CornerRadiusBottomRight = 4;
            _panel.AddThemeStyleboxOverride("panel", style);
            _panel.AnchorLeft = 0;
            _panel.AnchorRight = 1;
            _panel.AnchorTop = 0;
            _panel.AnchorBottom = 0;
            _panel.OffsetBottom = 0;
            AddChild(_panel);

            var mainVBox = new VBoxContainer();
            mainVBox.AddThemeConstantOverride("separation", 6);
            _panel.AddChild(mainVBox);

            // Title
            var title = new Label();
            title.Text = "DEBUG MENU (~)";
            title.AddThemeColorOverride("font_color", new Color(0.3f, 0.8f, 1f));
            title.AddThemeFontSizeOverride("font_size", 14);
            mainVBox.AddChild(title);

            // Button rows
            var row1 = new HBoxContainer();
            row1.AddThemeConstantOverride("separation", 4);
            mainVBox.AddChild(row1);

            AddBtn(row1, "+100 Scrap", () => { GameManager.Instance?.AddScrap(100); Msg("+100 Scrap"); });
            AddBtn(row1, "+500 Scrap", () => { GameManager.Instance?.AddScrap(500); Msg("+500 Scrap"); });
            AddBtn(row1, "Kill All", () => { KillAllEnemies(); Msg("All enemies killed"); });
            AddBtn(row1, "Skip Wave", () => { KillAllEnemies(); Msg("Wave skipped"); });
            AddBtn(row1, "God Mode", () => { _godMode = !_godMode; Msg($"God Mode: {(_godMode ? "ON" : "OFF")}"); });
            AddBtn(row1, "Insta-Kill", () => { _instantKill = !_instantKill; Msg($"Instant Kill: {(_instantKill ? "ON" : "OFF")}"); });

            var row2 = new HBoxContainer();
            row2.AddThemeConstantOverride("separation", 4);
            mainVBox.AddChild(row2);

            AddBtn(row2, "Heal Harvester", () => {
                if (ServiceLocator.TryGet<VineHarvester>(out var h)) { h.Heal(h.MaxHP); Msg("Harvester healed"); }
            });
            AddBtn(row2, "Damage Harvester", () => {
                if (ServiceLocator.TryGet<VineHarvester>(out var h)) { h.TakeDamage(20); Msg("Harvester -20 HP"); }
            });
            AddBtn(row2, "Toggle Mining", () => {
                if (ServiceLocator.TryGet<VineHarvester>(out var h)) { h.ToggleMode(); Msg($"Mining: {h.CurrentMode}"); }
            });

            // Magic type row
            var row3 = new HBoxContainer();
            row3.AddThemeConstantOverride("separation", 4);
            mainVBox.AddChild(row3);

            var magicLabel = new Label();
            magicLabel.Text = "Set Magic:";
            magicLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
            magicLabel.AddThemeFontSizeOverride("font_size", 12);
            row3.AddChild(magicLabel);

            AddBtn(row3, "Chaos", () => { SetMagic(MagicType.Chaos); }, new Color(0.7f, 0.2f, 0.9f));
            AddBtn(row3, "Power", () => { SetMagic(MagicType.Power); }, new Color(1f, 0.7f, 0.1f));
            AddBtn(row3, "Environment", () => { SetMagic(MagicType.Environment); }, new Color(0.2f, 0.85f, 0.3f));

            // Player row
            var row4 = new HBoxContainer();
            row4.AddThemeConstantOverride("separation", 4);
            mainVBox.AddChild(row4);

            AddBtn(row4, "Heal Player", () => {
                if (ServiceLocator.TryGet<VinePlayer>(out var p))
                {
                    p.CurrentHP = p.MaxHP;
                    GameEvents.OnPlayerHPChanged?.Invoke(p.CurrentHP, p.MaxHP);
                    Msg("Player healed");
                }
            });
            AddBtn(row4, "Max Mana", () => {
                if (ServiceLocator.TryGet<VinePlayer>(out var p))
                {
                    p.CurrentMagic = p.MaxMagic;
                    GameEvents.OnPlayerMagicChanged?.Invoke(p.CurrentMagic, p.MaxMagic);
                    Msg("Mana maxed");
                }
            });
            AddBtn(row4, "Speed x1", () => { Engine.TimeScale = 1; Msg("Speed: 1x"); });
            AddBtn(row4, "Speed x3", () => { Engine.TimeScale = 3; Msg("Speed: 3x"); });
            AddBtn(row4, "Speed x10", () => { Engine.TimeScale = 10; Msg("Speed: 10x"); });

            // Command input
            var inputRow = new HBoxContainer();
            inputRow.AddThemeConstantOverride("separation", 4);
            mainVBox.AddChild(inputRow);

            var prompt = new Label();
            prompt.Text = ">";
            prompt.AddThemeColorOverride("font_color", new Color(0.3f, 0.8f, 1f));
            inputRow.AddChild(prompt);

            _input = new LineEdit();
            _input.PlaceholderText = "type command...";
            _input.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _input.AddThemeColorOverride("font_color", Colors.White);
            _input.AddThemeFontSizeOverride("font_size", 13);
            _input.TextSubmitted += OnCommand;
            inputRow.AddChild(_input);

            // Feedback
            _feedback = new Label();
            _feedback.AddThemeColorOverride("font_color", new Color(0.5f, 0.9f, 0.5f));
            _feedback.AddThemeFontSizeOverride("font_size", 12);
            mainVBox.AddChild(_feedback);
        }

        private void AddBtn(HBoxContainer row, string text, System.Action action, Color? tint = null)
        {
            var btn = new Button();
            btn.Text = text;
            btn.AddThemeFontSizeOverride("font_size", 11);
            if (tint.HasValue)
                btn.AddThemeColorOverride("font_color", tint.Value);
            btn.Pressed += () => action();
            row.AddChild(btn);
        }

        private void SetMagic(MagicType type)
        {
            if (ServiceLocator.TryGet<VineHarvester>(out var h))
            {
                h.SelectMagicType(type);
                Msg($"Magic type: {type}");
            }
        }

        private void OnCommand(string text)
        {
            _input.Clear();
            if (string.IsNullOrWhiteSpace(text)) return;

            string cmd = text.Trim().ToLower();
            string[] parts = cmd.Split(' ');

            switch (parts[0])
            {
                case "help":
                    Msg("scrap [n], kill, god, instakill, heal, damage [n], magic [chaos|power|env], toggle, speed [n], floor [n]");
                    break;
                case "scrap":
                    int amount = parts.Length > 1 && int.TryParse(parts[1], out int s) ? s : 100;
                    GameManager.Instance?.AddScrap(amount);
                    Msg($"+{amount} Scrap");
                    break;
                case "kill":
                    KillAllEnemies();
                    Msg("All enemies killed");
                    break;
                case "god":
                    _godMode = !_godMode;
                    Msg($"God Mode: {(_godMode ? "ON" : "OFF")}");
                    break;
                case "instakill":
                    _instantKill = !_instantKill;
                    Msg($"Instant Kill: {(_instantKill ? "ON" : "OFF")}");
                    break;
                case "heal":
                    if (ServiceLocator.TryGet<VineHarvester>(out var hh)) hh.Heal(hh.MaxHP);
                    if (ServiceLocator.TryGet<VinePlayer>(out var pp))
                    {
                        pp.CurrentHP = pp.MaxHP;
                        GameEvents.OnPlayerHPChanged?.Invoke(pp.CurrentHP, pp.MaxHP);
                    }
                    Msg("Healed all");
                    break;
                case "damage":
                    float dmg = parts.Length > 1 && float.TryParse(parts[1], out float d) ? d : 20;
                    if (ServiceLocator.TryGet<VineHarvester>(out var hd)) hd.TakeDamage(dmg);
                    Msg($"Harvester -{dmg} HP");
                    break;
                case "magic":
                    if (parts.Length > 1)
                    {
                        MagicType mt = parts[1] switch
                        {
                            "chaos" => MagicType.Chaos,
                            "power" => MagicType.Power,
                            "env" or "environment" => MagicType.Environment,
                            _ => MagicType.None
                        };
                        if (mt != MagicType.None) SetMagic(mt);
                        else Msg("Unknown magic type. Use: chaos, power, env");
                    }
                    break;
                case "toggle":
                    if (ServiceLocator.TryGet<VineHarvester>(out var ht)) { ht.ToggleMode(); Msg($"Mining: {ht.CurrentMode}"); }
                    break;
                case "speed":
                    double spd = parts.Length > 1 && double.TryParse(parts[1], out double sv) ? sv : 1;
                    Engine.TimeScale = spd;
                    Msg($"Speed: {spd}x");
                    break;
                case "floor":
                    if (parts.Length > 1 && int.TryParse(parts[1], out int fl))
                    {
                        if (GameManager.Instance != null) GameManager.Instance.CurrentFloor = fl;
                        Msg($"Floor set to {fl} (restart battle to see)");
                    }
                    break;
                default:
                    Msg($"Unknown: {cmd}. Type 'help'.");
                    break;
            }
        }

        private void KillAllEnemies()
        {
            var tree = GetTree();
            if (tree == null) return;
            foreach (var node in tree.GetNodesInGroup(Constants.GROUP_VINE_ENEMY))
            {
                if (node is VineEnemy enemy && enemy.IsAlive)
                    enemy.TakeDamage(99999);
            }
        }

        private void Msg(string text)
        {
            _feedback.Text = text;
            _feedbackTimer = 4.0;
            GD.Print($"[Debug] {text}");
        }
    }
}
