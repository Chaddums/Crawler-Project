using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// HUD for Vine Logic TD. Shows gold, lives, wave info, and node build buttons.
    /// Extends CanvasLayer (like the existing HUD) to render over the 3D scene.
    /// </summary>
    public partial class VineHUD : CanvasLayer
    {
        private Label _goldLabel;
        private Label _livesLabel;
        private Label _waveLabel;
        private Label _phaseLabel;
        private Button _startWaveButton;
        private HBoxContainer _nodeButtons;
        private Label _tooltipLabel;
        private Button _speedButton;
        private PanelContainer _endOverlay;

        public override void _Ready()
        {
            BuildTopBar();
            BuildBottomBar();
            BuildTooltip();

            GameEvents.OnScrapChanged += UpdateGold;
            GameEvents.OnCoreLivesChanged += UpdateLives;
            GameEvents.OnWaveStarted += w => UpdateWaveInfo();
            GameEvents.OnWaveCompleted += w => UpdateWaveInfo();
            GameEvents.OnPhaseChanged += UpdatePhase;

            UpdateGold(GameManager.Instance?.CurrentScrap ?? Constants.VINE_STARTING_GOLD);
            UpdateLives(Constants.VINE_CORE_LIVES);
            UpdateWaveInfo();
        }

        private void BuildTopBar()
        {
            var topPanel = new PanelContainer();
            topPanel.SetAnchorsPreset(Control.LayoutPreset.TopWide);
            topPanel.OffsetBottom = 45;
            var style = new StyleBoxFlat();
            style.BgColor = new Color(TronTheme.PanelBg.R, TronTheme.PanelBg.G, TronTheme.PanelBg.B, 0.85f);
            topPanel.AddThemeStyleboxOverride("panel", style);
            AddChild(topPanel);

            var hbox = new HBoxContainer();
            hbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            hbox.AddThemeConstantOverride("separation", 30);
            topPanel.AddChild(hbox);

            _goldLabel = MakeLabel("Gold: 80", 20);
            _goldLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.2f));
            hbox.AddChild(_goldLabel);

            _livesLabel = MakeLabel("Lives: 10", 20);
            _livesLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.3f, 0.3f));
            hbox.AddChild(_livesLabel);

            _waveLabel = MakeLabel("Wave: 0 / 6", 20);
            hbox.AddChild(_waveLabel);

            _phaseLabel = MakeLabel("BUILD", 20);
            _phaseLabel.AddThemeColorOverride("font_color", new Color(0.3f, 0.9f, 0.3f));
            hbox.AddChild(_phaseLabel);

            // Spacer
            var spacer = new Control();
            spacer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            hbox.AddChild(spacer);

            // Speed button
            _speedButton = new Button();
            _speedButton.Text = "Speed: 1x";
            _speedButton.CustomMinimumSize = new Vector2(90, 0);
            _speedButton.Pressed += OnSpeedPressed;
            hbox.AddChild(_speedButton);

            // Hint text
            var hint = MakeLabel("[H] Help  [F12] Editor  [ESC] Menu", 14);
            hint.AddThemeColorOverride("font_color", new Color(0.4f, 0.4f, 0.4f));
            hbox.AddChild(hint);
        }

        private void BuildBottomBar()
        {
            var bottomPanel = new PanelContainer();
            bottomPanel.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
            bottomPanel.OffsetTop = -110;
            var style = new StyleBoxFlat();
            style.BgColor = new Color(TronTheme.PanelBg.R, TronTheme.PanelBg.G, TronTheme.PanelBg.B, 0.85f);
            bottomPanel.AddThemeStyleboxOverride("panel", style);
            AddChild(bottomPanel);

            var vbox = new VBoxContainer();
            vbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            vbox.AddThemeConstantOverride("separation", 8);
            bottomPanel.AddChild(vbox);

            // Start wave button
            _startWaveButton = new Button();
            _startWaveButton.Text = "Start Wave [Space]";
            _startWaveButton.CustomMinimumSize = new Vector2(160, 35);
            _startWaveButton.Pressed += OnStartWavePressed;
            vbox.AddChild(_startWaveButton);

            // Node buttons
            _nodeButtons = new HBoxContainer();
            _nodeButtons.AddThemeConstantOverride("separation", 4);
            vbox.AddChild(_nodeButtons);

            // Build buttons from draft role selection, or fallback for direct launch
            var nodes = GameManager.Instance?.AvailableNodes;
            if (nodes != null && nodes.Length > 0)
            {
                foreach (var type in nodes)
                    AddNodeButton(type);
            }
            else
            {
                // Fallback (debug/direct launch) — original 10
                AddNodeButton(VineNodeType.ProximitySensor);
                AddNodeButton(VineNodeType.DamageTower);
                AddNodeButton(VineNodeType.Switch);
                AddNodeButton(VineNodeType.Gate);
                AddNodeButton(VineNodeType.Extender);
                AddNodeButton(VineNodeType.Junction);
                AddNodeButton(VineNodeType.SlowField);
                AddNodeButton(VineNodeType.Timer);
                AddNodeButton(VineNodeType.Delay);
                AddNodeButton(VineNodeType.BuffEmitter);
            }
        }

        private void AddNodeButton(VineNodeType type)
        {
            var data = VineNodeRegistry.Get(type);
            if (data == null) return;

            var btn = new Button();
            string tag = data.Category switch {
                VineNodeCategory.Sensor => "[S]",
                VineNodeCategory.Effect => "[E]",
                _ => "[R]"
            };
            btn.Text = $"{tag} {data.Name}\n({data.GoldCost}g)";
            btn.CustomMinimumSize = new Vector2(110, 50);
            btn.TooltipText = data.Description;

            // Color-code by category
            Color catColor = data.Category switch {
                VineNodeCategory.Sensor => new Color(0.2f, 0.9f, 0.4f),
                VineNodeCategory.Effect => new Color(0.9f, 0.5f, 0.2f),
                _ => new Color(0.5f, 0.7f, 1.0f)
            };
            btn.AddThemeColorOverride("font_color", catColor);

            // Tinted background
            var btnStyle = new StyleBoxFlat();
            btnStyle.BgColor = new Color(catColor.R * 0.15f, catColor.G * 0.15f, catColor.B * 0.15f, 0.9f);
            btnStyle.BorderColor = catColor * 0.5f;
            btnStyle.SetBorderWidthAll(1);
            btnStyle.SetCornerRadiusAll(4);
            btnStyle.ContentMarginLeft = 4;
            btnStyle.ContentMarginRight = 4;
            btnStyle.ContentMarginTop = 4;
            btnStyle.ContentMarginBottom = 4;
            btn.AddThemeStyleboxOverride("normal", btnStyle);

            var hoverStyle = btnStyle.Duplicate() as StyleBoxFlat;
            hoverStyle.BgColor = new Color(catColor.R * 0.25f, catColor.G * 0.25f, catColor.B * 0.25f, 0.95f);
            hoverStyle.BorderColor = catColor * 0.8f;
            btn.AddThemeStyleboxOverride("hover", hoverStyle);

            var pressedStyle = btnStyle.Duplicate() as StyleBoxFlat;
            pressedStyle.BgColor = new Color(catColor.R * 0.35f, catColor.G * 0.35f, catColor.B * 0.35f, 1f);
            pressedStyle.BorderColor = catColor;
            btn.AddThemeStyleboxOverride("pressed", pressedStyle);

            btn.Pressed += () => OnNodeButtonPressed(type);
            btn.MouseEntered += () => ShowTooltip(data);
            btn.MouseExited += () => HideTooltip();
            _nodeButtons.AddChild(btn);
        }

        private void BuildTooltip()
        {
            _tooltipLabel = new Label();
            _tooltipLabel.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
            _tooltipLabel.OffsetTop = -140;
            _tooltipLabel.OffsetLeft = 10;
            _tooltipLabel.Visible = false;
            _tooltipLabel.AddThemeFontSizeOverride("font_size", 14);

            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.05f, 0.05f, 0.04f, 0.9f);
            style.ContentMarginLeft = 8;
            style.ContentMarginRight = 8;
            style.ContentMarginTop = 4;
            style.ContentMarginBottom = 4;
            _tooltipLabel.AddThemeStyleboxOverride("normal", style);

            AddChild(_tooltipLabel);
        }

        // ── Input ──

        // ── Help overlay ──
        private PanelContainer _helpOverlay;

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && !key.Echo)
            {
                if (key.Keycode == Key.Space)
                    OnStartWavePressed();
                else if (key.Keycode == Key.Escape)
                    GameManager.Instance?.ReturnToMainMenu();
                else if (key.Keycode == Key.H)
                    ToggleHelp();
                else if (key.Keycode == Key.Tab)
                    OnSpeedPressed();
                else if (key.Keycode == Key.B && key.CtrlPressed && key.ShiftPressed)
                    BugReportDialog.Show(GetTree());
                else if (key.Keycode == Key.K && key.CtrlPressed && key.ShiftPressed)
                    DebugKillAllEnemies();
                else if (key.Keycode == Key.G && key.CtrlPressed && key.ShiftPressed)
                    DebugAddGold();
            }
        }

        private void ToggleHelp()
        {
            if (_helpOverlay != null)
            {
                // Free the CenterContainer parent wrapping the panel
                _helpOverlay.GetParent()?.QueueFree();
                _helpOverlay = null;
                return;
            }

            // Full-screen container to center the help panel
            var centerWrap = new CenterContainer();
            centerWrap.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(centerWrap);

            _helpOverlay = new PanelContainer();
            _helpOverlay.CustomMinimumSize = new Vector2(650, 500);
            centerWrap.AddChild(_helpOverlay);
            var style = new StyleBoxFlat();
            style.BgColor = new Color(TronTheme.PanelBg.R, TronTheme.PanelBg.G, TronTheme.PanelBg.B, 0.95f);
            style.BorderColor = TronTheme.HelpBorder;
            style.SetBorderWidthAll(2);
            style.SetCornerRadiusAll(6);
            style.ContentMarginLeft = 16;
            style.ContentMarginRight = 16;
            style.ContentMarginTop = 12;
            style.ContentMarginBottom = 12;
            _helpOverlay.AddThemeStyleboxOverride("panel", style);

            var scroll = new ScrollContainer();
            scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            _helpOverlay.AddChild(scroll);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 6);
            scroll.AddChild(vbox);

            AddHelpTitle(vbox, "HOW TO BUILD YOUR MACHINE");
            AddHelpText(vbox, "Press [H] to close this help.", new Color(0.5f, 0.5f, 0.5f));

            AddHelpSection(vbox, "THE BASIC CHAIN", new Color(0.3f, 0.9f, 0.4f));
            AddHelpText(vbox, "SENSOR  >>  wire  >>  EFFECT");
            AddHelpText(vbox, "A sensor detects enemies and fires a signal.");
            AddHelpText(vbox, "The signal travels along vines (connections) to reach an effect node.");
            AddHelpText(vbox, "Effect nodes only activate WHEN they receive a signal — not automatically.");

            AddHelpSection(vbox, "SIMPLEST SETUP", new Color(0.9f, 0.8f, 0.2f));
            AddHelpText(vbox, "1. Place a Motion Detector (SENSOR) near the enemy path");
            AddHelpText(vbox, "2. Place a Junk Turret (EFFECT) next to the sensor");
            AddHelpText(vbox, "3. They auto-connect — green line = sensor output");
            AddHelpText(vbox, "4. Start wave — sensor detects enemies, turret fires!");

            AddHelpSection(vbox, "ADDING LOGIC", new Color(0.5f, 0.7f, 1.0f));
            AddHelpText(vbox, "SENSOR >> Rail Switch >> two different turret zones");
            AddHelpText(vbox, "  Switch alternates which path the signal takes");
            AddHelpText(vbox, "SENSOR >> Pneumatic Gate >> turret");
            AddHelpText(vbox, "  Gate needs 2+ signals at once to open (AND logic)");
            AddHelpText(vbox, "  Enemies bunch up at closed gates = AoE opportunity");

            AddHelpSection(vbox, "CONNECTION COLORS", new Color(0.9f, 0.9f, 0.9f));
            AddHelpText(vbox, "Green line  = from a SENSOR (signal source)", new Color(0.3f, 0.8f, 0.4f));
            AddHelpText(vbox, "Orange line = to an EFFECT (signal destination)", new Color(0.9f, 0.6f, 0.2f));
            AddHelpText(vbox, "Blue line   = ROUTE to ROUTE (signal passthrough)", new Color(0.4f, 0.6f, 0.9f));
            AddHelpText(vbox, "Yellow dots = signals traveling along the vine", new Color(0.9f, 0.8f, 0.2f));

            AddHelpSection(vbox, "NODE CATEGORIES", new Color(0.9f, 0.9f, 0.9f));
            AddHelpText(vbox, "[SENSOR] Detects enemies, fires signals", new Color(0.2f, 0.9f, 0.4f));
            AddHelpText(vbox, "  Motion Detector, Crank Timer");
            AddHelpText(vbox, "[ROUTE] Moves/transforms signals", new Color(0.5f, 0.7f, 1.0f));
            AddHelpText(vbox, "  Cable Splice, Rail Switch, Pneumatic Gate, Capacitor Bank");
            AddHelpText(vbox, "[EFFECT] Does something when signaled", new Color(0.9f, 0.5f, 0.2f));
            AddHelpText(vbox, "  Junk Turret, Tar Sprayer, Overclock Relay");

            AddHelpSection(vbox, "CONTROLS", new Color(0.9f, 0.9f, 0.9f));
            AddHelpText(vbox, "Left-click   = place node (Shift+click = keep placing)");
            AddHelpText(vbox, "Right-click  = cancel placement / sell existing node");
            AddHelpText(vbox, "Middle-click = manual trigger (Signal Cannon nodes)");
            AddHelpText(vbox, "WASD         = pan camera");
            AddHelpText(vbox, "Scroll       = zoom");
            AddHelpText(vbox, "Space        = start next wave");
            AddHelpText(vbox, "F12          = open editor (tune all values live)");
            AddHelpText(vbox, "ESC          = return to menu");

            AddHelpSection(vbox, "DEBUG", new Color(0.9f, 0.4f, 0.4f));
            AddHelpText(vbox, "Ctrl+Shift+B = bug report (screenshot + description)");
            AddHelpText(vbox, "Ctrl+Shift+K = kill all enemies (unstick waves)");
            AddHelpText(vbox, "Ctrl+Shift+G = add 100 gold");
        }

        private static void AddHelpTitle(VBoxContainer parent, string text)
        {
            var label = new Label();
            label.Text = text;
            label.AddThemeFontSizeOverride("font_size", 22);
            label.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.3f));
            label.HorizontalAlignment = HorizontalAlignment.Center;
            parent.AddChild(label);
            parent.AddChild(new HSeparator());
        }

        private static void AddHelpSection(VBoxContainer parent, string text, Color color)
        {
            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_top", 8);
            var label = new Label();
            label.Text = text;
            label.AddThemeFontSizeOverride("font_size", 16);
            label.AddThemeColorOverride("font_color", color);
            margin.AddChild(label);
            parent.AddChild(margin);
        }

        private static void AddHelpText(VBoxContainer parent, string text, Color? color = null)
        {
            var label = new Label();
            label.Text = text;
            label.AddThemeFontSizeOverride("font_size", 13);
            label.AddThemeColorOverride("font_color", color ?? new Color(0.75f, 0.75f, 0.7f));
            parent.AddChild(label);
        }

        // ── Debug tools ──

        private void DebugKillAllEnemies()
        {
            var enemies = GetTree().GetNodesInGroup(Constants.GROUP_VINE_ENEMY);
            int count = 0;
            foreach (var enemy in enemies)
            {
                if (enemy is VineEnemy ve && ve.IsAlive)
                {
                    ve.TakeDamage(9999);
                    count++;
                }
            }
            GD.Print($"[Debug] Killed {count} enemies");
        }

        private void DebugAddGold()
        {
            GameManager.Instance?.AddScrap(100);
            GD.Print("[Debug] Added 100 gold");
        }

        private void OnSpeedPressed()
        {
            GameManager.Instance?.ToggleSpeed();
            float speed = GameManager.Instance?.GameSpeed ?? 1f;
            if (_speedButton != null)
                _speedButton.Text = $"Speed: {speed}x";
        }

        private void OnStartWavePressed()
        {
            var phase = GameManager.Instance?.CurrentPhase ?? GamePhase.Build;
            if (phase != GamePhase.Build && phase != GamePhase.WaveComplete) return;

            if (ServiceLocator.TryGet<VineWaveManager>(out var wm))
                wm.StartWave();
        }

        private void OnNodeButtonPressed(VineNodeType type)
        {
            if (ServiceLocator.TryGet<VinePlacer>(out var placer))
                placer.StartPlacing(type);
        }

        // ── Tooltip ──

        private void ShowTooltip(VineNodeData data)
        {
            if (_tooltipLabel == null) return;
            _tooltipLabel.Text = $"{data.Name} — {data.Description}";
            _tooltipLabel.Visible = true;
        }

        private void HideTooltip()
        {
            if (_tooltipLabel != null)
                _tooltipLabel.Visible = false;
        }

        // ── Updates ──

        private void UpdateGold(int gold)
        {
            if (_goldLabel != null) _goldLabel.Text = $"Gold: {gold}";
        }

        private void UpdateLives(int lives)
        {
            if (_livesLabel != null)
            {
                _livesLabel.Text = $"Lives: {lives}";
                if (lives <= 3)
                    _livesLabel.AddThemeColorOverride("font_color", new Color(1f, 0.2f, 0.2f));
            }
        }

        private void UpdateWaveInfo()
        {
            var wm = ServiceLocator.TryGet<VineWaveManager>(out var manager) ? manager : null;
            int current = wm?.CurrentWave ?? 0;
            if (_waveLabel != null)
                _waveLabel.Text = $"Wave: {current} / {VineWaveRegistry.WaveCount}";
        }

        private void UpdatePhase(GamePhase phase)
        {
            if (_phaseLabel == null) return;

            _phaseLabel.Text = phase switch {
                GamePhase.Build => "BUILD",
                GamePhase.Wave => "WAVE",
                GamePhase.WaveComplete => "CLEAR",
                GamePhase.Victory => "VICTORY",
                GamePhase.Defeat => "DEFEAT",
                _ => phase.ToString().ToUpper()
            };

            _phaseLabel.AddThemeColorOverride("font_color", phase switch {
                GamePhase.Build => new Color(0.3f, 0.9f, 0.3f),
                GamePhase.Wave => new Color(0.9f, 0.6f, 0.1f),
                GamePhase.Victory => new Color(0.9f, 0.9f, 0.2f),
                GamePhase.Defeat => new Color(0.9f, 0.2f, 0.2f),
                _ => Colors.White
            });

            if (_startWaveButton != null)
                _startWaveButton.Visible = phase == GamePhase.Build || phase == GamePhase.WaveComplete;

            if (phase == GamePhase.Victory || phase == GamePhase.Defeat)
                ShowEndScreen(phase);
        }

        private void ShowEndScreen(GamePhase phase)
        {
            if (_endOverlay != null) return;

            _endOverlay = new PanelContainer();
            _endOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            var style = new StyleBoxFlat();
            style.BgColor = new Color(0f, 0f, 0f, 0.7f);
            _endOverlay.AddThemeStyleboxOverride("panel", style);
            AddChild(_endOverlay);

            var center = new CenterContainer();
            center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _endOverlay.AddChild(center);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 20);
            center.AddChild(vbox);

            bool won = phase == GamePhase.Victory;

            var title = new Label();
            title.Text = won ? "NETWORK COMPLETE" : "CORE BREACHED";
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.AddThemeFontSizeOverride("font_size", 48);
            title.AddThemeColorOverride("font_color",
                won ? new Color(0.9f, 0.9f, 0.2f) : new Color(0.9f, 0.2f, 0.2f));
            vbox.AddChild(title);

            var subtitle = new Label();
            subtitle.Text = won
                ? "Your machine held. AXIS is not impressed."
                : "The signal failed. AXIS sends regards.";
            subtitle.HorizontalAlignment = HorizontalAlignment.Center;
            subtitle.AddThemeFontSizeOverride("font_size", 18);
            subtitle.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.5f));
            vbox.AddChild(subtitle);

            var waveInfo = new Label();
            var wm = ServiceLocator.TryGet<VineWaveManager>(out var manager) ? manager : null;
            waveInfo.Text = $"Waves survived: {wm?.CurrentWave ?? 0} / {VineWaveRegistry.WaveCount}";
            waveInfo.HorizontalAlignment = HorizontalAlignment.Center;
            waveInfo.AddThemeFontSizeOverride("font_size", 16);
            vbox.AddChild(waveInfo);

            var menuBtn = new Button();
            menuBtn.Text = "Return to Menu [ESC]";
            menuBtn.CustomMinimumSize = new Vector2(200, 45);
            menuBtn.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            menuBtn.Pressed += () => GameManager.Instance?.ReturnToMainMenu();
            vbox.AddChild(menuBtn);
        }

        private static Label MakeLabel(string text, int fontSize = 16)
        {
            var label = new Label();
            label.Text = text;
            label.AddThemeFontSizeOverride("font_size", fontSize);
            return label;
        }

        public override void _ExitTree()
        {
            GameEvents.OnScrapChanged -= UpdateGold;
            GameEvents.OnCoreLivesChanged -= UpdateLives;
            GameEvents.OnPhaseChanged -= UpdatePhase;
        }
    }
}
