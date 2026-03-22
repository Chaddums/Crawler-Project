using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// HUD for Vine Logic TD. Shows gold, lives, wave info, and node build buttons.
    /// Extends CanvasLayer (like the existing HUD) to render over the 3D scene.
    /// </summary>
    public partial class VineHUD : CanvasLayer
    {
        private Label _scrapLabel;
        private Label _livesLabel;
        private ProgressBar _harvesterBar;
        private Label _harvesterLabel;
        private Label _waveLabel;
        private Label _floorLabel;
        private Label _phaseLabel;
        private Button _startWaveButton;
        private Button _sendAllButton;
        private Label _waveTimerLabel;
        private HBoxContainer _nodeButtons;
        private Label _tooltipLabel;
        private Button _speedButton;
        private PanelContainer _endOverlay;

        // Player HUD elements
        private ProgressBar _playerHPBar;
        private ProgressBar _playerManaBar;
        private Label[] _abilityLabels = new Label[3];

        // Chaos HUD
        private Label _chaosTimerLabel;
        private ColorRect _chaosOverlay;
        private Label _chaosTitleLabel;
        private Label _chaosSubtitleLabel;
        private float _chaosOverlayTimer;
        private bool _chaosOverlayActive;

        // Mining mode HUD
        private Label _miningModeLabel;
        private Label _magicTypeLabel;
        private ProgressBar _magicBar;
        private StyleBoxFlat _magicBarFill;
        private float[] _abilityCooldowns = new float[3];

        // Flyover overlay
        private ColorRect _letterboxTop;
        private ColorRect _letterboxBottom;
        private CenterContainer _flyoverOverlay;
        private Label _flyoverTitle;
        private Label _flyoverSubtitle;

        // Placement prompt
        private Label _placementPrompt;
        private float _placementPromptPulse;
        private bool _harvesterPlacedNotified;

        public override void _Ready()
        {
            BuildTopBar();
            BuildBottomBar();
            BuildTooltip();
            BuildPlayerHUD();

            GameEvents.OnResourcesChanged += UpdateGold;
            GameEvents.OnCoreLivesChanged += UpdateLives;
            GameEvents.OnHarvesterHPChanged += UpdateHarvesterHP;
            GameEvents.OnWaveStarted += w => UpdateWaveInfo();
            GameEvents.OnWaveCompleted += w => UpdateWaveInfo();
            GameEvents.OnPhaseChanged += UpdatePhase;
            GameEvents.OnPlayerHPChanged += UpdatePlayerHP;
            GameEvents.OnPlayerMaterialsChanged += UpdatePlayerMana;
            GameEvents.OnAbilityCooldownChanged += UpdateAbilityCooldown;
            GameEvents.OnMiningModeChanged += UpdateMiningMode;
            GameEvents.OnMaterialTypeSelected += UpdateMaterialType;
            GameEvents.OnMaterialsAccumulated += UpdateMagicAccumulated;
            GameEvents.OnCorruptionStarted += OnCorruptionStarted;
            GameEvents.OnCorruptionEnded += OnCorruptionEnded;

            BuildChaosHUD();
            BuildFlyoverOverlay();
            BuildPlacementPrompt();

            UpdateGold(GameManager.Instance?.CurrentResources ?? Constants.VINE_STARTING_RESOURCES);
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

            _scrapLabel = MakeLabel("Scrap: 80", 20);
            _scrapLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.2f));
            hbox.AddChild(_scrapLabel);

            // Harvester HP bar
            var harvesterBox = new VBoxContainer();
            harvesterBox.CustomMinimumSize = new Vector2(140, 0);
            hbox.AddChild(harvesterBox);

            _harvesterLabel = MakeLabel("Harvester: 200", 13);
            _harvesterLabel.AddThemeColorOverride("font_color", new Color(0.3f, 0.9f, 0.3f));
            harvesterBox.AddChild(_harvesterLabel);

            _harvesterBar = new ProgressBar();
            _harvesterBar.CustomMinimumSize = new Vector2(130, 12);
            _harvesterBar.MaxValue = Constants.VINE_HARVESTER_MAX_HP;
            _harvesterBar.Value = Constants.VINE_HARVESTER_MAX_HP;
            _harvesterBar.ShowPercentage = false;
            var hbStyle = new StyleBoxFlat();
            hbStyle.BgColor = new Color(0.15f, 0.15f, 0.15f);
            _harvesterBar.AddThemeStyleboxOverride("background", hbStyle);
            var hbFill = new StyleBoxFlat();
            hbFill.BgColor = new Color(0.2f, 0.9f, 0.2f);
            _harvesterBar.AddThemeStyleboxOverride("fill", hbFill);
            harvesterBox.AddChild(_harvesterBar);

            // Mining mode indicator
            var miningBox = new VBoxContainer();
            miningBox.CustomMinimumSize = new Vector2(130, 0);
            hbox.AddChild(miningBox);

            _miningModeLabel = MakeLabel("[T] SCRAP MODE", 13);
            _miningModeLabel.AddThemeColorOverride("font_color", BitPalette.Accent);
            miningBox.AddChild(_miningModeLabel);

            _magicTypeLabel = MakeLabel("No magic selected", 11);
            _magicTypeLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
            miningBox.AddChild(_magicTypeLabel);

            _magicBar = new ProgressBar();
            _magicBar.CustomMinimumSize = new Vector2(120, 8);
            _magicBar.MaxValue = 100;
            _magicBar.Value = 0;
            _magicBar.ShowPercentage = false;
            var mbBg = new StyleBoxFlat();
            mbBg.BgColor = new Color(0.1f, 0.1f, 0.1f);
            _magicBar.AddThemeStyleboxOverride("background", mbBg);
            _magicBarFill = new StyleBoxFlat();
            _magicBarFill.BgColor = new Color(0.5f, 0.5f, 0.5f);
            _magicBar.AddThemeStyleboxOverride("fill", _magicBarFill);
            _magicBar.Visible = false; // Hidden until magic type selected
            miningBox.AddChild(_magicBar);

            // Keep _livesLabel hidden as fallback
            _livesLabel = MakeLabel("", 14);
            _livesLabel.Visible = false;
            hbox.AddChild(_livesLabel);

            _floorLabel = MakeLabel("Floor: 1 / 3", 20);
            _floorLabel.AddThemeColorOverride("font_color", new Color(0.0f, 0.85f, 0.95f));
            hbox.AddChild(_floorLabel);

            _waveLabel = MakeLabel("Wave: 0 / 3", 20);
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

            // Wave controls row
            var waveRow = new HBoxContainer();
            waveRow.AddThemeConstantOverride("separation", 8);
            vbox.AddChild(waveRow);

            _startWaveButton = new Button();
            _startWaveButton.Text = "Start Wave [Space]";
            _startWaveButton.CustomMinimumSize = new Vector2(160, 35);
            _startWaveButton.Pressed += OnStartWavePressed;
            waveRow.AddChild(_startWaveButton);

            _sendAllButton = new Button();
            _sendAllButton.Text = "ALL [Shift+Space]";
            _sendAllButton.CustomMinimumSize = new Vector2(140, 35);
            _sendAllButton.Pressed += OnSendAllPressed;
            _sendAllButton.Visible = false;
            waveRow.AddChild(_sendAllButton);

            _waveTimerLabel = MakeLabel("", 18);
            _waveTimerLabel.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.3f));
            _waveTimerLabel.Visible = false;
            waveRow.AddChild(_waveTimerLabel);

            // Node buttons
            _nodeButtons = new HBoxContainer();
            _nodeButtons.AddThemeConstantOverride("separation", 4);
            vbox.AddChild(_nodeButtons);

            // Mining Building button — first in the row, visually distinct
            AddMiningBuildingButton(_nodeButtons);

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

        private Button _miningBuildingBtn;

        private void AddMiningBuildingButton(HBoxContainer parent)
        {
            _miningBuildingBtn = new Button();
            _miningBuildingBtn.Text = "⛏ Mining Building";
            _miningBuildingBtn.AddThemeFontSizeOverride("font_size", 13);
            _miningBuildingBtn.AddThemeColorOverride("font_color", BitPalette.Accent);
            _miningBuildingBtn.TooltipText = "Place the Mining Building to generate Scrap or Magic.\nRight-click to toggle mode after placement.";

            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.06f, 0.06f, 0.1f, 0.9f);
            style.BorderColor = BitPalette.Accent * 0.6f;
            style.SetBorderWidthAll(2);
            style.SetCornerRadiusAll(4);
            style.ContentMarginLeft = 8;
            style.ContentMarginRight = 8;
            style.ContentMarginTop = 4;
            style.ContentMarginBottom = 4;
            _miningBuildingBtn.AddThemeStyleboxOverride("normal", style);

            var hoverStyle = (StyleBoxFlat)style.Duplicate();
            hoverStyle.BgColor = new Color(0.1f, 0.1f, 0.18f, 0.95f);
            hoverStyle.BorderColor = BitPalette.Accent;
            _miningBuildingBtn.AddThemeStyleboxOverride("hover", hoverStyle);

            _miningBuildingBtn.Pressed += OnMiningBuildingPressed;
            parent.AddChild(_miningBuildingBtn);
        }

        private void OnMiningBuildingPressed()
        {
            PlayUIClick();
            // If already placed, toggle mode instead
            if (ServiceLocator.TryGet<VineGrid>(out var grid) && grid.Harvester != null)
            {
                grid.Harvester.ToggleMode();
                UpdateMiningButtonText();
                return;
            }

            if (ServiceLocator.TryGet<VinePlacer>(out var placer))
                placer.StartPlacingMiningBuilding();
        }

        private void UpdateMiningButtonText()
        {
            if (_miningBuildingBtn == null) return;
            if (ServiceLocator.TryGet<VineGrid>(out var grid) && grid.Harvester != null)
            {
                var h = grid.Harvester;
                string mode = h.CurrentMode == MiningMode.Resources ? "⛏ Scrap" : "✦ Magic";
                string magic = h.SelectedMagic != MaterialType.None ? $" ({h.SelectedMagic})" : "";
                _miningBuildingBtn.Text = $"{mode}{magic} [Click to toggle]";
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
            btn.Text = $"{tag} {data.Name}\n({data.ScrapCost}g)";
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

        // ── Per-frame timer display ──

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            bool flyoverActive = ServiceLocator.TryGet<TDCamera>(out var cam) && cam.FlyoverActive;
            var phase = GameManager.Instance?.CurrentPhase ?? GamePhase.Build;
            bool hasHarvester = ServiceLocator.TryGet<VineGrid>(out var grid2) && grid2.Harvester != null;

            // ── Flyover overlay visibility ──
            if (_flyoverOverlay != null) _flyoverOverlay.Visible = flyoverActive;
            if (_letterboxTop != null) _letterboxTop.Visible = flyoverActive;
            if (_letterboxBottom != null) _letterboxBottom.Visible = flyoverActive;

            // Hide normal HUD during flyover
            if (_nodeButtons != null) _nodeButtons.Visible = !flyoverActive && hasHarvester;
            if (_startWaveButton != null && flyoverActive) _startWaveButton.Visible = false;
            if (_sendAllButton != null && flyoverActive) _sendAllButton.Visible = false;
            if (_waveTimerLabel != null && flyoverActive) _waveTimerLabel.Visible = false;

            // ── Placement prompt ──
            if (_placementPrompt != null)
            {
                bool showPrompt = !flyoverActive && !hasHarvester && phase == GamePhase.Build;
                _placementPrompt.Visible = showPrompt;
                if (showPrompt)
                {
                    // Pulsing animation
                    _placementPromptPulse += dt * 3f;
                    float alpha = 0.6f + 0.4f * Mathf.Sin(_placementPromptPulse);
                    _placementPrompt.AddThemeColorOverride("font_color",
                        new Color(BitPalette.Accent.R, BitPalette.Accent.G, BitPalette.Accent.B, alpha));

                    // Auto-select mining building placement
                    if (ServiceLocator.TryGet<VinePlacer>(out var placer) && !placer.IsPlacing)
                        placer.StartPlacingMiningBuilding();
                }
            }

            // ── Harvester just placed — notify wave manager ──
            if (hasHarvester && !_harvesterPlacedNotified)
            {
                _harvesterPlacedNotified = true;
                if (ServiceLocator.TryGet<VineWaveManager>(out var wm2))
                    wm2.OnHarvesterPlaced();
            }

            // Chaos overlay fade
            if (_chaosOverlayActive && _chaosOverlay != null)
            {
                _chaosOverlayTimer -= dt;
                if (_chaosOverlayTimer <= 0)
                {
                    _chaosOverlayActive = false;
                    _chaosOverlay.Visible = false;
                    if (_chaosTitleLabel != null) _chaosTitleLabel.Visible = false;
                    if (_chaosSubtitleLabel != null) _chaosSubtitleLabel.Visible = false;
                }
                else
                {
                    // Fade out over last 1s
                    float overlayAlpha = _chaosOverlayTimer < 1f ? _chaosOverlayTimer * 0.6f : 0.6f;
                    _chaosOverlay.Color = new Color(0, 0, 0, overlayAlpha);
                    // Title pulse
                    if (_chaosTitleLabel != null)
                    {
                        float pulse = 0.7f + 0.3f * Mathf.Sin(_chaosOverlayTimer * 8f);
                        _chaosTitleLabel.AddThemeColorOverride("font_color",
                            new Color(0.95f * pulse, 0.1f * pulse, 0.05f * pulse));
                    }
                }
            }

            // Chaos persistent timer label
            if (_chaosTimerLabel != null && _chaosTimerLabel.Visible)
            {
                var cm = GetTree().CurrentScene?.GetNodeOrNull<CorruptionManager>("CorruptionManager");
                if (cm is { IsCorruptionActive: true })
                {
                    _chaosTimerLabel.Text = $"AXIS CHAOS \u2014 {cm.RemainingDuration:F1}s";
                    float pulse = 0.6f + 0.4f * Mathf.Sin(cm.RemainingDuration * 4f);
                    _chaosTimerLabel.AddThemeColorOverride("font_color",
                        new Color(0.95f * pulse, 0.1f, 0.05f));
                }
                else
                {
                    _chaosTimerLabel.Visible = false;
                }
            }

            if (flyoverActive) return;

            if (ServiceLocator.TryGet<VineWaveManager>(out var wm))
            {
                float timer = wm.AutoStartTimer;
                if (timer > 0)
                {
                    if (_waveTimerLabel != null)
                    {
                        _waveTimerLabel.Text = $"Next wave in: {Mathf.CeilToInt(timer)}s";
                        _waveTimerLabel.Visible = true;
                    }
                    if (_startWaveButton != null)
                        _startWaveButton.Text = "Send Now [Space]";
                    if (_sendAllButton != null)
                        _sendAllButton.Visible = wm.HasMoreWaves;
                }
                else if (wm.WaveActive && wm.HasMoreWaves)
                {
                    if (_waveTimerLabel != null)
                        _waveTimerLabel.Visible = false;
                    if (_startWaveButton != null)
                    {
                        _startWaveButton.Text = "Send Next [Space]";
                        _startWaveButton.Visible = true;
                    }
                    if (_sendAllButton != null)
                        _sendAllButton.Visible = true;
                }
                else if (wm.WaveActive)
                {
                    if (_waveTimerLabel != null) _waveTimerLabel.Visible = false;
                    if (_sendAllButton != null) _sendAllButton.Visible = false;
                }
                else
                {
                    if (_waveTimerLabel != null) _waveTimerLabel.Visible = false;
                    if (_startWaveButton != null)
                        _startWaveButton.Text = "Start Wave [Space]";
                }
            }
        }

        // ── Input ──

        // ── Help overlay ──
        private PanelContainer _helpOverlay;

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && !key.Echo)
            {
                if (key.Keycode == Key.Space && key.ShiftPressed)
                    OnSendAllPressed();
                else if (key.Keycode == Key.Space)
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
            AddHelpText(vbox, "Dim red     = UNPOWERED (too many effects in chain)", new Color(0.4f, 0.15f, 0.15f));
            AddHelpText(vbox, "  Sensors have limited power (3-4). Each effect uses 1 power.", new Color(0.6f, 0.5f, 0.4f));
            AddHelpText(vbox, "  Add more sensors or use route nodes to reach distant effects.", new Color(0.6f, 0.5f, 0.4f));
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
            AddHelpText(vbox, "Space        = send next wave / send early");
            AddHelpText(vbox, "Shift+Space  = send ALL remaining waves at once");
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
            GameManager.Instance?.AddResources(100);
            GD.Print("[Debug] Added 100 gold");
        }

        private static void PlayUIClick()
        {
            if (ServiceLocator.TryGet<AudioManager>(out var audio))
                audio.PlaySFXByName("button_click");
        }

        private void OnSpeedPressed()
        {
            PlayUIClick();
            GameManager.Instance?.ToggleSpeed();
            float speed = GameManager.Instance?.GameSpeed ?? 1f;
            if (_speedButton != null)
                _speedButton.Text = $"Speed: {speed}x";
        }

        private void OnStartWavePressed()
        {
            PlayUIClick();
            var phase = GameManager.Instance?.CurrentPhase ?? GamePhase.Build;
            if (phase != GamePhase.Build && phase != GamePhase.WaveComplete && phase != GamePhase.Wave) return;

            // Must place Mining Building before starting waves
            if (ServiceLocator.TryGet<VineGrid>(out var grid) && grid.Harvester == null)
            {
                // Flash the start wave button with warning text
                if (_startWaveButton != null)
                {
                    _startWaveButton.Text = "Place Mining Building First!";
                    _startWaveButton.AddThemeColorOverride("font_color", new Color(1f, 0.3f, 0.2f));
                    GetTree().CreateTimer(2.0).Timeout += () => {
                        if (_startWaveButton != null && IsInstanceValid(_startWaveButton))
                        {
                            _startWaveButton.Text = "Start Wave [Space]";
                            _startWaveButton.RemoveThemeColorOverride("font_color");
                        }
                    };
                }
                // Also flash the mining building button
                if (_miningBuildingBtn != null)
                {
                    var flashStyle = new StyleBoxFlat();
                    flashStyle.BgColor = new Color(0.2f, 0.15f, 0.05f, 0.95f);
                    flashStyle.BorderColor = BitPalette.Accent;
                    flashStyle.SetBorderWidthAll(3);
                    flashStyle.SetCornerRadiusAll(4);
                    flashStyle.ContentMarginLeft = 8;
                    flashStyle.ContentMarginRight = 8;
                    flashStyle.ContentMarginTop = 4;
                    flashStyle.ContentMarginBottom = 4;
                    _miningBuildingBtn.AddThemeStyleboxOverride("normal", flashStyle);
                }
                return;
            }

            if (ServiceLocator.TryGet<VineWaveManager>(out var wm))
                wm.RequestNextWave();
        }

        private void OnSendAllPressed()
        {
            PlayUIClick();
            if (ServiceLocator.TryGet<VineGrid>(out var grid) && grid.Harvester == null) return;
            if (ServiceLocator.TryGet<VineWaveManager>(out var wm))
                wm.SendAllRemaining();
        }

        private void OnNodeButtonPressed(VineNodeType type)
        {
            PlayUIClick();
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

        // ── Player HUD ──

        private void BuildPlayerHUD()
        {
            var playerPanel = new PanelContainer();
            playerPanel.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
            playerPanel.OffsetTop = -210;
            playerPanel.OffsetBottom = -120;
            playerPanel.OffsetLeft = 10;
            playerPanel.OffsetRight = 220;
            var pStyle = new StyleBoxFlat();
            pStyle.BgColor = new Color(0f, 0f, 0f, 0.6f);
            pStyle.SetCornerRadiusAll(4);
            pStyle.ContentMarginLeft = 8;
            pStyle.ContentMarginRight = 8;
            pStyle.ContentMarginTop = 4;
            pStyle.ContentMarginBottom = 4;
            playerPanel.AddThemeStyleboxOverride("panel", pStyle);
            AddChild(playerPanel);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 3);
            playerPanel.AddChild(vbox);

            // HP bar
            var hpLabel = MakeLabel("HP", 12);
            hpLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.3f, 0.3f));
            vbox.AddChild(hpLabel);

            _playerHPBar = new ProgressBar();
            _playerHPBar.CustomMinimumSize = new Vector2(190, 14);
            _playerHPBar.MaxValue = Constants.VINE_PLAYER_MAX_HP;
            _playerHPBar.Value = Constants.VINE_PLAYER_MAX_HP;
            _playerHPBar.ShowPercentage = false;
            var hpBg = new StyleBoxFlat { BgColor = new Color(0.2f, 0.05f, 0.05f) };
            _playerHPBar.AddThemeStyleboxOverride("background", hpBg);
            var hpFill = new StyleBoxFlat { BgColor = new Color(0.8f, 0.2f, 0.2f) };
            _playerHPBar.AddThemeStyleboxOverride("fill", hpFill);
            vbox.AddChild(_playerHPBar);

            // Mana bar
            var manaLabel = MakeLabel("Mana", 12);
            manaLabel.AddThemeColorOverride("font_color", new Color(0.3f, 0.5f, 0.9f));
            vbox.AddChild(manaLabel);

            _playerManaBar = new ProgressBar();
            _playerManaBar.CustomMinimumSize = new Vector2(190, 14);
            _playerManaBar.MaxValue = Constants.VINE_PLAYER_MAX_MATERIALS;
            _playerManaBar.Value = Constants.VINE_PLAYER_MAX_MATERIALS;
            _playerManaBar.ShowPercentage = false;
            var manaBg = new StyleBoxFlat { BgColor = new Color(0.05f, 0.05f, 0.2f) };
            _playerManaBar.AddThemeStyleboxOverride("background", manaBg);
            var manaFill = new StyleBoxFlat { BgColor = new Color(0.2f, 0.4f, 0.9f) };
            _playerManaBar.AddThemeStyleboxOverride("fill", manaFill);
            vbox.AddChild(_playerManaBar);

            // Ability cooldowns
            var abilityBox = new HBoxContainer();
            abilityBox.AddThemeConstantOverride("separation", 8);
            vbox.AddChild(abilityBox);

            string[] keys = { "Q", "E", "R" };
            Color[] colors = { new(0.9f, 0.8f, 0.2f), new(0.2f, 0.9f, 0.4f), new(0.6f, 0.3f, 0.9f) };
            for (int i = 0; i < 3; i++)
            {
                var lbl = MakeLabel($"[{keys[i]}] Ready", 12);
                lbl.AddThemeColorOverride("font_color", colors[i]);
                abilityBox.AddChild(lbl);
                _abilityLabels[i] = lbl;
            }
        }

        private void UpdateHarvesterHP(float current, float max)
        {
            if (_harvesterBar != null)
            {
                _harvesterBar.MaxValue = max;
                _harvesterBar.Value = current;
            }
            if (_harvesterLabel != null)
            {
                _harvesterLabel.Text = $"Harvester: {current:F0}/{max:F0}";
                float pct = max > 0 ? current / max : 0;
                _harvesterLabel.AddThemeColorOverride("font_color",
                    pct > 0.5f ? new Color(0.3f, 0.9f, 0.3f) :
                    pct > 0.25f ? new Color(0.9f, 0.7f, 0.1f) :
                    new Color(0.9f, 0.2f, 0.2f));

                // Update fill color too
                var fillStyle = _harvesterBar?.GetThemeStylebox("fill") as StyleBoxFlat;
                if (fillStyle != null)
                    fillStyle.BgColor = pct > 0.5f ? new Color(0.2f, 0.9f, 0.2f) :
                        pct > 0.25f ? new Color(0.9f, 0.7f, 0.1f) :
                        new Color(0.9f, 0.2f, 0.2f);
            }
        }

        private void UpdatePlayerHP(float current, float max)
        {
            if (_playerHPBar != null)
            {
                _playerHPBar.MaxValue = max;
                _playerHPBar.Value = current;
            }
        }

        private void UpdatePlayerMana(float current, float max)
        {
            if (_playerManaBar != null)
            {
                _playerManaBar.MaxValue = max;
                _playerManaBar.Value = current;
            }
        }

        private void UpdateAbilityCooldown(int slot, float remaining)
        {
            if (slot < 0 || slot >= 3) return;
            _abilityCooldowns[slot] = remaining;
            if (_abilityLabels[slot] != null)
            {
                string[] keys = { "Q", "E", "R" };
                _abilityLabels[slot].Text = remaining > 0.1f
                    ? $"[{keys[slot]}] {remaining:F1}s"
                    : $"[{keys[slot]}] Ready";
            }
        }

        // ── Mining Mode Updates ──

        private void UpdateMiningMode(MiningMode mode)
        {
            if (_miningModeLabel == null) return;
            if (mode == MiningMode.Resources)
            {
                _miningModeLabel.Text = "[T] SCRAP MODE";
                _miningModeLabel.AddThemeColorOverride("font_color", BitPalette.Accent);
            }
            else
            {
                var harvester = ServiceLocator.TryGet<VineHarvester>(out var h) ? h : null;
                var magicType = harvester?.SelectedMagic ?? MaterialType.None;
                var color = VineHarvester.GetMagicColor(magicType);
                _miningModeLabel.Text = $"[T] MAGIC MODE";
                _miningModeLabel.AddThemeColorOverride("font_color", color);
            }
        }

        private void UpdateMaterialType(MaterialType type)
        {
            if (_magicTypeLabel == null) return;
            if (type == MaterialType.None)
            {
                _magicTypeLabel.Text = "No magic selected";
                _magicTypeLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
                _magicBar.Visible = false;
            }
            else
            {
                var color = VineHarvester.GetMagicColor(type);
                _magicTypeLabel.Text = $"Magic: {type}";
                _magicTypeLabel.AddThemeColorOverride("font_color", color);
                _magicBarFill.BgColor = color;
                _magicBar.Visible = true;
            }
        }

        private void UpdateMagicAccumulated(float total, MaterialType type)
        {
            if (_magicBar == null) return;
            // Magic bar fills up — max scales with total so it always looks like progress
            _magicBar.MaxValue = Mathf.Max(100, total + 50);
            _magicBar.Value = total;
        }

        // ── Chaos HUD ──

        private void BuildChaosHUD()
        {
            // Persistent timer label at top center
            _chaosTimerLabel = new Label();
            _chaosTimerLabel.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
            _chaosTimerLabel.OffsetTop = 55;
            _chaosTimerLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _chaosTimerLabel.AddThemeFontSizeOverride("font_size", 28);
            _chaosTimerLabel.Visible = false;
            AddChild(_chaosTimerLabel);

            // Fullscreen dark overlay
            _chaosOverlay = new ColorRect();
            _chaosOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _chaosOverlay.Color = new Color(0, 0, 0, 0.6f);
            _chaosOverlay.MouseFilter = Control.MouseFilterEnum.Ignore;
            _chaosOverlay.Visible = false;
            AddChild(_chaosOverlay);

            // Title + subtitle centered on overlay
            var centerBox = new CenterContainer();
            centerBox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            centerBox.MouseFilter = Control.MouseFilterEnum.Ignore;
            AddChild(centerBox);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 12);
            vbox.MouseFilter = Control.MouseFilterEnum.Ignore;
            centerBox.AddChild(vbox);

            _chaosTitleLabel = new Label();
            _chaosTitleLabel.Text = "AXIS HAS TAKEN CONTROL";
            _chaosTitleLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _chaosTitleLabel.AddThemeFontSizeOverride("font_size", 52);
            _chaosTitleLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.1f, 0.05f));
            _chaosTitleLabel.Visible = false;
            vbox.AddChild(_chaosTitleLabel);

            _chaosSubtitleLabel = new Label();
            _chaosSubtitleLabel.Text = "All enemies unleashed \u2014 3x scrap bounty active";
            _chaosSubtitleLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _chaosSubtitleLabel.AddThemeFontSizeOverride("font_size", 20);
            _chaosSubtitleLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.2f));
            _chaosSubtitleLabel.Visible = false;
            vbox.AddChild(_chaosSubtitleLabel);
        }

        private void OnCorruptionStarted(CorruptionType type)
        {
            // Show fullscreen overlay for 3s
            _chaosOverlayActive = true;
            _chaosOverlayTimer = 3.0f;
            if (_chaosOverlay != null)
            {
                _chaosOverlay.Visible = true;
                _chaosOverlay.Color = new Color(0, 0, 0, 0.6f);
            }
            if (_chaosTitleLabel != null) _chaosTitleLabel.Visible = true;
            if (_chaosSubtitleLabel != null) _chaosSubtitleLabel.Visible = true;

            // Show persistent timer
            if (_chaosTimerLabel != null) _chaosTimerLabel.Visible = true;
        }

        private void OnCorruptionEnded(CorruptionType type)
        {
            _chaosOverlayActive = false;
            if (_chaosOverlay != null) _chaosOverlay.Visible = false;
            if (_chaosTitleLabel != null) _chaosTitleLabel.Visible = false;
            if (_chaosSubtitleLabel != null) _chaosSubtitleLabel.Visible = false;
            if (_chaosTimerLabel != null) _chaosTimerLabel.Visible = false;
        }

        // ── Flyover Overlay ──

        private void BuildFlyoverOverlay()
        {
            // S1: floors removed

            // Top letterbox bar
            _letterboxTop = new ColorRect();
            _letterboxTop.Color = Colors.Black;
            _letterboxTop.SetAnchorsPreset(Control.LayoutPreset.TopWide);
            _letterboxTop.OffsetBottom = 80;
            AddChild(_letterboxTop);

            // Bottom letterbox bar
            _letterboxBottom = new ColorRect();
            _letterboxBottom.Color = Colors.Black;
            _letterboxBottom.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
            _letterboxBottom.OffsetTop = -80;
            AddChild(_letterboxBottom);

            // Centered title overlay
            _flyoverOverlay = new CenterContainer();
            _flyoverOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(_flyoverOverlay);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 8);
            _flyoverOverlay.AddChild(vbox);

            _flyoverTitle = new Label();
            _flyoverTitle.Text = "EXTRACTION";
            _flyoverTitle.HorizontalAlignment = HorizontalAlignment.Center;
            _flyoverTitle.AddThemeFontSizeOverride("font_size", 52);
            _flyoverTitle.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.2f));
            vbox.AddChild(_flyoverTitle);

            _flyoverSubtitle = new Label();
            string planetName = PlanetTheme.Current?.PlanetName ?? "Unknown World";
            _flyoverSubtitle.Text = planetName;
            _flyoverSubtitle.HorizontalAlignment = HorizontalAlignment.Center;
            _flyoverSubtitle.AddThemeFontSizeOverride("font_size", 20);
            _flyoverSubtitle.AddThemeColorOverride("font_color", new Color(0.5f, 0.7f, 0.9f));
            vbox.AddChild(_flyoverSubtitle);

            // Flyover starts visible; will be hidden when flyover ends
        }

        private void BuildPlacementPrompt()
        {
            _placementPrompt = new Label();
            _placementPrompt.Text = "Place your Mining Building!";
            _placementPrompt.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
            _placementPrompt.OffsetTop = 100;
            _placementPrompt.HorizontalAlignment = HorizontalAlignment.Center;
            _placementPrompt.AddThemeFontSizeOverride("font_size", 28);
            _placementPrompt.AddThemeColorOverride("font_color", BitPalette.Accent);
            _placementPrompt.Visible = false;
            AddChild(_placementPrompt);
        }

        // ── Updates ──

        private void UpdateGold(int gold)
        {
            if (_scrapLabel != null) _scrapLabel.Text = $"Scrap: {gold}";
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
            int total = wm?.TotalWavesThisFloor ?? 3;
            if (_waveLabel != null)
                _waveLabel.Text = $"Wave: {current} / {total}";

            // S1: floor label removed — S2 will add extraction counter
            if (_floorLabel != null)
                _floorLabel.Text = "";
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
                // S1: FloorComplete removed
                GamePhase.Victory => new Color(0.9f, 0.9f, 0.2f),
                GamePhase.Defeat => new Color(0.9f, 0.2f, 0.2f),
                _ => Colors.White
            });

            // Wave button visibility — show during Build/WaveComplete, and during Wave if more waves remain
            bool hasMore = ServiceLocator.TryGet<VineWaveManager>(out var wm2) && wm2.HasMoreWaves;
            if (_startWaveButton != null)
                _startWaveButton.Visible = phase == GamePhase.Build || phase == GamePhase.WaveComplete
                    || (phase == GamePhase.Wave && hasMore);
            if (_sendAllButton != null)
                _sendAllButton.Visible = phase == GamePhase.Wave && hasMore;

            if (phase == GamePhase.Victory || phase == GamePhase.Defeat)
            {
                if (_startWaveButton != null) _startWaveButton.Visible = false;
                if (_sendAllButton != null) _sendAllButton.Visible = false;
                if (_waveTimerLabel != null) _waveTimerLabel.Visible = false;
                ShowEndScreen(phase);
            }
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
            title.Text = won ? "HARVEST COMPLETE" : "HARVESTER DESTROYED";
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.AddThemeFontSizeOverride("font_size", 48);
            title.AddThemeColorOverride("font_color",
                won ? new Color(0.9f, 0.9f, 0.2f) : new Color(0.9f, 0.2f, 0.2f));
            vbox.AddChild(title);

            var subtitle = new Label();
            // S1: floors removed — S6 will build debrief screen
            int waveReached = GameManager.Instance?.CurrentWave ?? 0;
            subtitle.Text = won
                ? "Extraction complete. AXIS is not impressed."
                : $"Fell on wave {waveReached}. AXIS sends regards.";
            subtitle.HorizontalAlignment = HorizontalAlignment.Center;
            subtitle.AddThemeFontSizeOverride("font_size", 18);
            subtitle.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.5f));
            vbox.AddChild(subtitle);

            var waveInfo = new Label();
            var wm = ServiceLocator.TryGet<VineWaveManager>(out var manager) ? manager : null;
            waveInfo.Text = $"Waves survived: {wm?.CurrentWave ?? 0} / {wm?.TotalWavesThisFloor ?? 0}";
            waveInfo.HorizontalAlignment = HorizontalAlignment.Center;
            waveInfo.AddThemeFontSizeOverride("font_size", 16);
            vbox.AddChild(waveInfo);

            // Player kill stat
            if (ServiceLocator.TryGet<VinePlayer>(out var player))
            {
                var killStat = new Label();
                killStat.Text = $"Enemies killed personally: {player.EnemiesKilledPersonally}";
                killStat.HorizontalAlignment = HorizontalAlignment.Center;
                killStat.AddThemeFontSizeOverride("font_size", 16);
                killStat.AddThemeColorOverride("font_color", new Color(0.3f, 0.7f, 1.0f));
                vbox.AddChild(killStat);
            }

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
            GameEvents.OnResourcesChanged -= UpdateGold;
            GameEvents.OnCoreLivesChanged -= UpdateLives;
            GameEvents.OnHarvesterHPChanged -= UpdateHarvesterHP;
            GameEvents.OnPhaseChanged -= UpdatePhase;
            GameEvents.OnPlayerHPChanged -= UpdatePlayerHP;
            GameEvents.OnPlayerMaterialsChanged -= UpdatePlayerMana;
            GameEvents.OnAbilityCooldownChanged -= UpdateAbilityCooldown;
            GameEvents.OnCorruptionStarted -= OnCorruptionStarted;
            GameEvents.OnCorruptionEnded -= OnCorruptionEnded;
        }
    }
}
