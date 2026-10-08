using Godot;
using System.Linq;

namespace JunkyardTD
{
    /// <summary>
    /// HUD for Vine Logic TD. Shows gold, lives, wave info, and node build buttons.
    /// Extends CanvasLayer (like the existing HUD) to render over the 3D scene.
    /// </summary>
    public partial class VineHUD : CanvasLayer
    {
        private Label _resourceLabel;
        private Label _livesLabel;
        private ProgressBar _harvesterBar;
        private Label _harvesterLabel;
        private Label _waveLabel;
        private Label _extractionLabel;
        private Label _phaseLabel;
        private Button _startWaveButton;
        private Button _sendAllButton;
        private Label _waveTimerLabel;
        private HBoxContainer _nodeButtons;
        // Build-bar hover card: sits above the bar, never over another button
        private PanelContainer _infoCard;
        private Label _infoTitle, _infoRole, _infoBest, _infoWeak, _infoStats;
        private Button _speedButton;
        // Player HUD elements
        private ProgressBar _playerHPBar;
        private Label _mechLevelLabel;
        private ProgressBar _mechXpBar;
        private ProgressBar _playerMaterialsBar;
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
        private Label _materialTypeLabel;
        private ProgressBar _materialBar;
        private StyleBoxFlat _materialBarFill;
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

        // Shield wall HUD
        private VBoxContainer _shieldWallPanel;
        private readonly System.Collections.Generic.Dictionary<CardinalDirection, Label> _wallLabels = new();
        private Label _breachAnnouncement;
        private float _breachAnnouncementTimer;

        // Wave preview HUD
        private VBoxContainer _wavePreviewPanel;
        private VBoxContainer _wavePreviewSurges;
        private Label _wavePreviewHeader;
        private int _lastPreviewedWave = -1;

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
            GameEvents.OnMechXpChanged += UpdateMechXp;
            GameEvents.OnPlayerMaterialsChanged += UpdatePlayerMaterials;
            GameEvents.OnAbilityCooldownChanged += UpdateAbilityCooldown;
            GameEvents.OnMiningModeChanged += UpdateMiningMode;
            GameEvents.OnMaterialTypeSelected += UpdateMaterialType;
            GameEvents.OnMaterialsAccumulated += UpdateMaterialsAccumulated;
            GameEvents.OnCorruptionStarted += OnCorruptionStarted;
            GameEvents.OnCorruptionEnded += OnCorruptionEnded;

            BuildChaosHUD();
            BuildFlyoverOverlay();
            BuildPlacementPrompt();
            BuildShieldWallHUD();
            BuildWavePreviewPanel();

            GameEvents.OnShieldWallDestroyed += OnShieldWallDestroyed;
            GameEvents.OnRelicAcquired += OnRelicAcquired;
            // Generic announcements (site secured, expansion zones, inhabit, wall breach detail).
            // Several systems fired OnAnnouncement but nothing displayed it.
            GameEvents.OnAnnouncement += OnAnnouncement;

            UpdateGold(GameManager.Instance?.CurrentResources ?? Constants.VINE_STARTING_RESOURCES);
            UpdateLives(Constants.VINE_CORE_LIVES);
            UpdateWaveInfo();
        }

        // Top-right panels (shield walls, next wave) sit below the top bar instead of over its labels.
        private const float TopBarHeight = 52f;

        private void BuildTopBar()
        {
            var topPanel = new PanelContainer();
            topPanel.SetAnchorsPreset(Control.LayoutPreset.TopWide);
            topPanel.OffsetBottom = TopBarHeight;
            var style = new StyleBoxFlat();
            style.BgColor = new Color(TronTheme.PanelBg.R, TronTheme.PanelBg.G, TronTheme.PanelBg.B, 0.85f);
            // Keep the first and last labels off the screen edges
            style.ContentMarginLeft = 14;
            style.ContentMarginRight = 14;
            topPanel.AddThemeStyleboxOverride("panel", style);
            AddChild(topPanel);

            var hbox = new HBoxContainer();
            hbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            hbox.AddThemeConstantOverride("separation", 30);
            topPanel.AddChild(hbox);

            _resourceLabel = MakeLabel("Resources: 80", 20);
            _resourceLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.2f));
            hbox.AddChild(_resourceLabel);

            // Harvester HP bar
            var harvesterBox = new VBoxContainer();
            harvesterBox.CustomMinimumSize = new Vector2(140, 0);
            hbox.AddChild(harvesterBox);

            _harvesterLabel = MakeLabel("Harvester: 200", 15);
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

            _miningModeLabel = MakeLabel("[T] RESOURCES MODE", 15);
            _miningModeLabel.AddThemeColorOverride("font_color", BitPalette.Accent);
            miningBox.AddChild(_miningModeLabel);

            _materialTypeLabel = MakeLabel("", 14);
            _materialTypeLabel.AddThemeColorOverride("font_color", new Color(0.68f, 0.68f, 0.72f));
            miningBox.AddChild(_materialTypeLabel);

            _materialBar = new ProgressBar();
            _materialBar.CustomMinimumSize = new Vector2(120, 8);
            _materialBar.MaxValue = 100;
            _materialBar.Value = 0;
            _materialBar.ShowPercentage = false;
            var mbBg = new StyleBoxFlat();
            mbBg.BgColor = new Color(0.1f, 0.1f, 0.1f);
            _materialBar.AddThemeStyleboxOverride("background", mbBg);
            _materialBarFill = new StyleBoxFlat();
            _materialBarFill.BgColor = new Color(0.5f, 0.5f, 0.5f);
            _materialBar.AddThemeStyleboxOverride("fill", _materialBarFill);
            _materialBar.Visible = false; // Hidden until material type selected
            miningBox.AddChild(_materialBar);

            // Keep _livesLabel hidden as fallback
            _livesLabel = MakeLabel("", 14);
            _livesLabel.Visible = false;
            hbox.AddChild(_livesLabel);

            _extractionLabel = MakeLabel("Extracted: 0", 20);
            _extractionLabel.AddThemeColorOverride("font_color", new Color(0.3f, 0.95f, 0.4f));
            hbox.AddChild(_extractionLabel);

            // Equipped relics indicator
            _equippedRelicsLabel = MakeLabel("", 15);
            _equippedRelicsLabel.AddThemeColorOverride("font_color", new Color(0.66f, 0.33f, 0.97f));
            hbox.AddChild(_equippedRelicsLabel);
            UpdateEquippedRelicsDisplay();

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
            var hint = MakeLabel("[H] Help  [ESC] Menu", 15);
            hint.AddThemeColorOverride("font_color", new Color(0.62f, 0.64f, 0.7f));
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
                GD.Print($"[VineHUD] Building {nodes.Length} node buttons: {string.Join(", ", nodes)}");
                foreach (var type in nodes)
                    AddNodeButton(type);
            }
            else
            {
                // Fallback (debug/direct launch) — the shared tower roster. The old list
                // offered sensors/routing nodes that aren't buildable since the tower overhaul.
                foreach (var type in VineDraftScreen.GetRoleNodes(0))
                    AddNodeButton(type);
            }
        }

        private Button _miningBuildingBtn;

        private void AddMiningBuildingButton(HBoxContainer parent)
        {
            _miningBuildingBtn = new Button();
            _miningBuildingBtn.Text = "⛏ Mining Building";
            _miningBuildingBtn.AddThemeFontSizeOverride("font_size", 15);
            _miningBuildingBtn.AddThemeColorOverride("font_color", BitPalette.Accent);
            // No TooltipText: Godot's popup opened over the neighbouring buttons. The hover card does it.
            _miningBuildingBtn.MouseEntered += () => ShowMiningInfo(_miningBuildingBtn);
            _miningBuildingBtn.MouseExited += HideTooltip;

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
                string mode = h.CurrentMode == MiningMode.Resources ? "⛏ Resources" : "✦ Materials";
                string magic = h.SelectedMaterial != MaterialType.None ? $" ({h.SelectedMaterial})" : "";
                _miningBuildingBtn.Text = $"{mode}{magic} [Click to toggle]";
            }
        }

        private void AddNodeButton(VineNodeType type)
        {
            var data = VineNodeRegistry.Get(type);
            if (data == null)
            {
                GD.PrintErr($"[VineHUD] No registry data for node type: {type} — button skipped!");
                return;
            }

            var btn = new Button();
            btn.Text = $"{data.Name}\n({data.ResourceCost}r)";
            btn.CustomMinimumSize = new Vector2(110, 50);

            Color catColor = new Color(0.9f, 0.5f, 0.2f); // All towers same warm color
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
            btn.MouseEntered += () => ShowTooltip(data, btn);
            btn.MouseExited += () => HideTooltip();
            _nodeButtons.AddChild(btn);
        }

        private void BuildTooltip()
        {
            _infoCard = new PanelContainer { Name = "TowerInfoCard", Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
            var style = new StyleBoxFlat { BgColor = new Color(0.03f, 0.04f, 0.07f, 0.95f) };
            style.SetCornerRadiusAll(5);
            style.SetBorderWidthAll(1);
            style.BorderColor = new Color(0.9f, 0.5f, 0.2f, 0.8f);
            style.ContentMarginLeft = style.ContentMarginRight = 12;
            style.ContentMarginTop = style.ContentMarginBottom = 8;
            _infoCard.AddThemeStyleboxOverride("panel", style);
            var v = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            v.AddThemeConstantOverride("separation", 3);
            _infoCard.AddChild(v);
            Label L(int size, Color c)
            {
                var l = new Label { MouseFilter = Control.MouseFilterEnum.Ignore, AutowrapMode = TextServer.AutowrapMode.WordSmart };
                l.CustomMinimumSize = new Vector2(InfoCardWidth - 24, 0);
                l.AddThemeFontSizeOverride("font_size", size);
                l.AddThemeColorOverride("font_color", c);
                v.AddChild(l);
                return l;
            }
            _infoTitle = L(19, new Color(1f, 0.72f, 0.35f));
            _infoRole = L(15, new Color(0.9f, 0.92f, 0.96f));
            _infoBest = L(15, new Color(0.45f, 0.9f, 0.5f));
            _infoWeak = L(15, new Color(0.95f, 0.5f, 0.45f));
            _infoStats = L(15, new Color(0.66f, 0.7f, 0.78f));
            AddChild(_infoCard);
        }

        private const float InfoCardWidth = 430f;

        /// <summary>The hover card over a build-bar button (for tests).</summary>
        internal PanelContainer InfoCard => _infoCard;

        // ── Per-frame timer display ──

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            UpdateRelicNotification(dt);
            if ((_economyTimer -= dt) <= 0f) { _economyTimer = 0.5f; RefreshEconomyLine(); }
            bool flyoverActive = ServiceLocator.TryGet<TDCamera>(out var cam) && cam.FlyoverActive;
            bool playerEmerging = ServiceLocator.TryGet<VinePlayer>(out var vp) && vp.IsEmerging;
            bool introActive = flyoverActive || playerEmerging;
            var phase = GameManager.Instance?.CurrentPhase ?? GamePhase.Build;
            bool hasHarvester = ServiceLocator.TryGet<VineGrid>(out var grid2) && grid2.Harvester != null;

            // ── Flyover/intro overlay visibility ──
            if (_flyoverOverlay != null) _flyoverOverlay.Visible = flyoverActive;
            if (_letterboxTop != null) _letterboxTop.Visible = introActive;
            if (_letterboxBottom != null) _letterboxBottom.Visible = introActive;

            // Hide normal HUD during intro sequence
            if (_nodeButtons != null) _nodeButtons.Visible = !introActive && hasHarvester;
            if (_startWaveButton != null && introActive) _startWaveButton.Visible = false;
            if (_sendAllButton != null && introActive) _sendAllButton.Visible = false;
            if (_waveTimerLabel != null && introActive) _waveTimerLabel.Visible = false;

            // ── Placement prompt (legacy — Spire is now auto-placed) ──
            if (_placementPrompt != null)
                _placementPrompt.Visible = false;

            // ── Harvester placement notification now handled by VineBattleScene intro sequence ──

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

            // Shield wall timer updates
            if (_wallLabels.Count > 0 && ServiceLocator.TryGet<ShieldWallManager>(out var swm))
            {
                foreach (var kvp in _wallLabels)
                    UpdateWallLabel(kvp.Value, kvp.Key, swm);
            }

            // Wave preview panel
            UpdateWavePreview();

            // Breach announcement fade
            if (_breachAnnouncement != null && _breachAnnouncement.Visible)
            {
                _breachAnnouncementTimer -= dt;
                if (_breachAnnouncementTimer <= 0)
                {
                    _breachAnnouncement.Visible = false;
                }
                else if (_breachAnnouncementTimer < 1f)
                {
                    // Fade out over last second
                    var c = _breachAnnouncement.GetThemeColor("font_color");
                    _breachAnnouncement.AddThemeColorOverride("font_color",
                        new Color(c.R, c.G, c.B, _breachAnnouncementTimer));
                }
            }

            if (introActive) return;

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
                // ESC handled by PauseMenu
                else if (key.Keycode == Key.H)
                    ToggleHelp();
                else if (@event.IsActionPressed("speed_up"))
                {
                    // Consume it — the GameManager autoload also listens for speed_up, and
                    // handling it in both places toggled twice per press (1x → 3x).
                    OnSpeedPressed();
                    GetViewport().SetInputAsHandled();
                }
                else if (key.Keycode == Key.B && key.CtrlPressed && key.ShiftPressed)
                    BugReportDialog.Show(GetTree());
                // Cheats only in debug builds — +resources feeds straight into meta currency
                else if (key.Keycode == Key.K && key.CtrlPressed && key.ShiftPressed && OS.IsDebugBuild())
                    DebugKillAllEnemies();
                else if (key.Keycode == Key.G && key.CtrlPressed && key.ShiftPressed && OS.IsDebugBuild())
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
            _helpOverlay.CustomMinimumSize = new Vector2(760, 620);
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

            AddHelpTitle(vbox, "HOW TO PLAY");
            AddHelpText(vbox, "Press [H] to close this help.", new Color(0.62f, 0.64f, 0.7f));

            AddHelpSection(vbox, "THE GOAL", new Color(0.3f, 0.9f, 0.4f));
            AddHelpText(vbox, "Enemies walk the paths to your Spire. Keep it standing as long as you can:");
            AddHelpText(vbox, "every wave you survive extracts more. The Spire always falls in the end.");

            AddHelpSection(vbox, "TWO WAYS TO FIGHT", new Color(0.9f, 0.8f, 0.2f));
            AddHelpText(vbox, "Resources mode: the Spire mines Resources and every drop is Resources.");
            AddHelpText(vbox, "  Spend them on towers (the build bar) and Spire upgrades.");
            AddHelpText(vbox, "Materials mode [T]: half of every drop is banked at the Spire as Materials,");
            AddHelpText(vbox, "  and the Spire mines Materials twice as fast. Spend them on BIT.");
            AddHelpText(vbox, "Towers fire on their own. BIT alone can carry a run if you build it up.");
            AddHelpText(vbox, $"Income: the Spire mines +{Constants.VINE_HARVESTER_INCOME} every {Constants.VINE_HARVESTER_INCOME_INTERVAL:0} s, kills drop more, and each wave pays a bonus.");
            AddHelpText(vbox, $"  Gold RESOURCE NODES on the map add +{Constants.RESOURCE_NODE_BONUS} each while a tower stands next to one.");

            AddHelpSection(vbox, "BIT", new Color(0.45f, 0.65f, 1f));
            AddHelpText(vbox, "WASD             move (up is always up the screen)");
            AddHelpText(vbox, "Left mouse held  aim and fire where the mouse points");
            AddHelpText(vbox, "                 (BIT shoots the nearest enemy by itself otherwise)");
            AddHelpText(vbox, "Q  Shock Blast    E  Repair Pulse (heals the Spire)    R  Overclock towers");

            AddHelpSection(vbox, "THE SPIRE", new Color(0.95f, 0.6f, 0.25f));
            AddHelpText(vbox, "F at the Spire     upgrades for the Spire and BIT, refill and training");
            AddHelpText(vbox, "G at the Spire    climb in: the mouse aims the Spire's cannon, left mouse fires");
            AddHelpText(vbox, "                  (F or G climbs back out)");
            AddHelpText(vbox, "Hold F at the Spire  repair it with BIT's Materials");

            AddHelpSection(vbox, "BUILDING", new Color(0.9f, 0.9f, 0.9f));
            AddHelpText(vbox, "Left-click        place the selected tower (Shift keeps placing)");
            AddHelpText(vbox, "Click a tower     its panel: two upgrades, then one of two branches, or sell");
            AddHelpText(vbox, "Right-click       cancel placement / sell a tower (click, don't drag)");
            AddHelpText(vbox, "Right-drag        turn the camera      Scroll  zoom");

            AddHelpSection(vbox, "ENEMIES", new Color(0.95f, 0.45f, 0.4f));
            AddHelpText(vbox, "The wave card (top right) lists what's coming and what beats it.");
            AddHelpText(vbox, "Armoured    light hits barely scratch it: Junk Turret, Scatter Cannon, BIT");
            AddHelpText(vbox, "Flying      flies over walls straight at the Spire: Flak, Tesla, BIT");
            AddHelpText(vbox, "Shielded    a shield that grows back: Tesla strips it three times as fast");

            AddHelpSection(vbox, "WAVES", new Color(0.9f, 0.9f, 0.9f));
            AddHelpText(vbox, "Space        send the next wave now      Shift+Space  send every wave");
            AddHelpText(vbox, "Tab          game speed (1x, 2x, 3x)     ESC  menu      F11  fullscreen");

            AddHelpSection(vbox, "DEBUG", new Color(0.9f, 0.4f, 0.4f));
            AddHelpText(vbox, "Ctrl+Shift+B  bug report    Ctrl+Shift+K  kill all enemies    F12  editor");
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
            label.AddThemeFontSizeOverride("font_size", 15);
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

        private void ShowTooltip(VineNodeData data, Control over)
        {
            if (_infoCard == null) return;
            var info = TowerInfo.Get(data.Type);
            _infoTitle.Text = $"{data.Name}   {data.ResourceCost}r";
            _infoRole.Text = info.Role.Length > 0 ? info.Role : data.Description;
            _infoBest.Text = info.Best.Length > 0 ? $"Good against: {info.Best}" : "";
            _infoWeak.Text = info.Weak.Length > 0 ? $"Weak against: {info.Weak}" : "";
            _infoStats.Text = info.Stats + "\nClick a built one to upgrade it.";
            PlaceInfoCard(over);
        }

        private void ShowMiningInfo(Control over)
        {
            if (_infoCard == null) return;
            bool placed = ServiceLocator.TryGet<VineGrid>(out var g) && g.Harvester != null;
            _infoTitle.Text = placed ? "Spire mining mode" : "Mining Building";
            _infoRole.Text = placed
                ? "Click or press [T] to switch what the Spire mines."
                : "Place it to start mining.";
            _infoBest.Text = "Resources: buy and upgrade towers.";
            _infoWeak.Text = "";
            _infoStats.Text = "Materials: banked at the Spire for BIT's upgrades ([F] at the Spire). Choose the material type when asked.";
            PlaceInfoCard(over);
        }

        /// <summary>Above the bar, over the hovered button, clear of BIT's panel on the left.</summary>
        private void PlaceInfoCard(Control over)
        {
            _infoBest.Visible = _infoBest.Text.Length > 0;
            _infoWeak.Visible = _infoWeak.Text.Length > 0;
            _infoStats.Visible = _infoStats.Text.Length > 0;
            _infoCard.Size = new Vector2(InfoCardWidth, 0);
            _infoCard.ResetSize();
            var vis = GetViewport().GetVisibleRect();
            var r = over.GetGlobalRect();
            float barTop = _nodeButtons?.GetParentControl()?.GetGlobalRect().Position.Y ?? r.Position.Y - 45;
            float minX = vis.Position.X + 262f; // BIT's panel ends at 250
            float x = Mathf.Clamp(r.GetCenter().X - InfoCardWidth / 2f, minX, vis.End.X - InfoCardWidth - 8f);
            var size = _infoCard.GetCombinedMinimumSize();
            _infoCard.Position = new Vector2(x, barTop - size.Y - 8f);
            _infoCard.Visible = true;
        }

        private void HideTooltip()
        {
            if (_infoCard != null)
                _infoCard.Visible = false;
        }

        // ── Player HUD ──

        private void BuildPlayerHUD()
        {
            var playerPanel = new PanelContainer();
            playerPanel.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
            // Sits above the build bar and grows upward with its contents (bigger text no longer
            // pushes it down into the bar)
            playerPanel.OffsetBottom = -120;
            playerPanel.OffsetTop = -120 - 150;
            playerPanel.GrowVertical = Control.GrowDirection.Begin;
            playerPanel.OffsetLeft = 12;
            playerPanel.OffsetRight = 250;
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

            // Mech level and XP toward the next one
            var levelRow = new HBoxContainer();
            levelRow.AddThemeConstantOverride("separation", 6);
            vbox.AddChild(levelRow);
            _mechLevelLabel = MakeLabel("BIT  LV 1", 16);
            _mechLevelLabel.AddThemeColorOverride("font_color", BitPalette.Accent);
            levelRow.AddChild(_mechLevelLabel);
            _mechXpBar = new ProgressBar();
            _mechXpBar.CustomMinimumSize = new Vector2(0, 8);
            _mechXpBar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _mechXpBar.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            _mechXpBar.MaxValue = 1;
            _mechXpBar.Value = 0;
            _mechXpBar.ShowPercentage = false;
            _mechXpBar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(0.12f, 0.12f, 0.14f) });
            _mechXpBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = BitPalette.Accent });
            levelRow.AddChild(_mechXpBar);

            // HP bar
            var hpLabel = MakeLabel("HP", 15);
            hpLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.3f, 0.3f));
            vbox.AddChild(hpLabel);

            _playerHPBar = new ProgressBar();
            _playerHPBar.CustomMinimumSize = new Vector2(220, 14);
            _playerHPBar.MaxValue = Constants.VINE_PLAYER_MAX_HP;
            _playerHPBar.Value = Constants.VINE_PLAYER_MAX_HP;
            _playerHPBar.ShowPercentage = false;
            var hpBg = new StyleBoxFlat { BgColor = new Color(0.2f, 0.05f, 0.05f) };
            _playerHPBar.AddThemeStyleboxOverride("background", hpBg);
            var hpFill = new StyleBoxFlat { BgColor = new Color(0.8f, 0.2f, 0.2f) };
            _playerHPBar.AddThemeStyleboxOverride("fill", hpFill);
            vbox.AddChild(_playerHPBar);

            // Materials bar
            var manaLabel = MakeLabel("Materials", 15);
            manaLabel.AddThemeColorOverride("font_color", new Color(0.3f, 0.5f, 0.9f));
            vbox.AddChild(manaLabel);

            _playerMaterialsBar = new ProgressBar();
            _playerMaterialsBar.CustomMinimumSize = new Vector2(220, 14);
            _playerMaterialsBar.MaxValue = Constants.VINE_PLAYER_MAX_MATERIALS;
            _playerMaterialsBar.Value = Constants.VINE_PLAYER_MAX_MATERIALS;
            _playerMaterialsBar.ShowPercentage = false;
            var manaBg = new StyleBoxFlat { BgColor = new Color(0.05f, 0.05f, 0.2f) };
            _playerMaterialsBar.AddThemeStyleboxOverride("background", manaBg);
            var manaFill = new StyleBoxFlat { BgColor = new Color(0.2f, 0.4f, 0.9f) };
            _playerMaterialsBar.AddThemeStyleboxOverride("fill", manaFill);
            vbox.AddChild(_playerMaterialsBar);

            // Ability cooldowns
            var abilityBox = new HBoxContainer();
            abilityBox.AddThemeConstantOverride("separation", 8);
            vbox.AddChild(abilityBox);

            string[] keys = { "Q", "E", "R" };
            Color[] colors = { new(0.9f, 0.8f, 0.2f), new(0.2f, 0.9f, 0.4f), new(0.6f, 0.3f, 0.9f) };
            for (int i = 0; i < 3; i++)
            {
                var lbl = MakeLabel($"[{keys[i]}] Ready", 15);
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

        private void UpdateMechXp(int level, float xp, float needed)
        {
            if (_mechLevelLabel != null)
                _mechLevelLabel.Text = needed > 0f ? $"BIT  LV {level}" : $"BIT  LV {level}  MAX";
            if (_mechXpBar != null)
            {
                _mechXpBar.MaxValue = needed > 0f ? needed : 1;
                _mechXpBar.Value = needed > 0f ? xp : 1;
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

        private void UpdatePlayerMaterials(float current, float max)
        {
            if (_playerMaterialsBar != null)
            {
                _playerMaterialsBar.MaxValue = max;
                _playerMaterialsBar.Value = current;
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
                _miningModeLabel.Text = "[T] RESOURCES MODE";
                _miningModeLabel.AddThemeColorOverride("font_color", BitPalette.Accent);
            }
            else
            {
                var harvester = ServiceLocator.TryGet<VineHarvester>(out var h) ? h : null;
                var magicType = harvester?.SelectedMaterial ?? MaterialType.None;
                var color = VineHarvester.GetMaterialColor(magicType);
                _miningModeLabel.Text = $"[T] MATERIALS MODE";
                _miningModeLabel.AddThemeColorOverride("font_color", color);
            }
        }

        private void UpdateMaterialType(MaterialType type)
        {
            if (_materialTypeLabel == null) return;
            if (type == MaterialType.None)
            {
                _materialBar.Visible = false;
            }
            else
            {
                var color = VineHarvester.GetMaterialColor(type);
                _materialBarFill.BgColor = color;
                _materialBar.Visible = true;
            }
            RefreshEconomyLine();
        }

        private float _economyTimer;

        /// <summary>
        /// What the Spire is earning right now, under the mode: a player couldn't tell where
        /// Resources came from, or that the gold resource nodes pay when you build next to them.
        /// </summary>
        private void RefreshEconomyLine()
        {
            if (_materialTypeLabel == null || !ServiceLocator.TryGet<VineGrid>(out var grid) || grid.Harvester == null) return;
            var h = grid.Harvester;
            float every = Constants.VINE_HARVESTER_INCOME_INTERVAL;
            if (h.CurrentMode == MiningMode.Resources)
            {
                var nodes = grid.GetResourceNodes();
                int taken = nodes.Count(grid.IsResourceNodeCaptured);
                int income = (int)(Constants.VINE_HARVESTER_INCOME * SignalTuningEditor.HarvesterIncomeMult)
                    + SignalTuningEditor.HarvesterIncomeBonus + taken * Constants.RESOURCE_NODE_BONUS;
                _materialTypeLabel.Text = nodes.Count > 0
                    ? $"+{income} every {every:0} s  ·  nodes {taken}/{nodes.Count}"
                    : $"+{income} every {every:0} s";
                _materialTypeLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.6f));
            }
            else if (h.SelectedMaterial == MaterialType.None)
            {
                _materialTypeLabel.Text = "Choose a material first";
                _materialTypeLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.6f, 0.4f));
            }
            else
            {
                float rate = Constants.VINE_HARVESTER_INCOME * SignalTuningEditor.HarvesterIncomeMult
                    * (SpireStation.Current?.Data.MaterialsMode.SpireRateMult ?? 1f);
                _materialTypeLabel.Text = $"Banking +{rate:0} {h.SelectedMaterial} every {every:0} s";
                _materialTypeLabel.AddThemeColorOverride("font_color", VineHarvester.GetMaterialColor(h.SelectedMaterial));
            }
        }

        private void UpdateMaterialsAccumulated(float total, MaterialType type)
        {
            if (_materialBar == null) return;
            // Materials bar fills up — max scales with total so it always looks like progress
            _materialBar.MaxValue = Mathf.Max(100, total + 50);
            _materialBar.Value = total;
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

        private void BuildShieldWallHUD()
        {
            // Compact panel in top-right showing shield wall status per direction
            // The right-hand column: shield walls, then the next wave, each on a dark card so the
            // text reads over the bright shield walls behind it (it was 11-13 px text on nothing)
            EnsureRightColumn();
            _shieldWallPanel = new VBoxContainer();

            var header = new Label();
            header.Text = "SHIELD WALLS";
            header.AddThemeFontSizeOverride("font_size", 14);
            header.AddThemeColorOverride("font_color", new Color(0.62f, 0.66f, 0.78f));
            header.HorizontalAlignment = HorizontalAlignment.Center;
            _shieldWallPanel.AddChild(header);

            // Check if shield wall manager exists
            if (!ServiceLocator.TryGet<ShieldWallManager>(out var swm))
            {
                _shieldWallPanel.Visible = false;
                _shieldCard = Card(_shieldWallPanel);
                return;
            }

            var directions = new[] { CardinalDirection.North, CardinalDirection.East, CardinalDirection.South, CardinalDirection.West };
            foreach (var dir in directions)
            {
                if (!swm.IsWallActive(dir) && !swm.IsWallDestroyed(dir)) continue;

                var label = new Label();
                label.AddThemeFontSizeOverride("font_size", 16);
                label.HorizontalAlignment = HorizontalAlignment.Left;
                UpdateWallLabel(label, dir, swm);
                _shieldWallPanel.AddChild(label);
                _wallLabels[dir] = label;
            }

            if (_wallLabels.Count == 0)
                _shieldWallPanel.Visible = false;

            _shieldCard = Card(_shieldWallPanel);

            // Breach announcement label (center screen, hidden by default)
            _breachAnnouncement = new Label();
            _breachAnnouncement.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
            _breachAnnouncement.OffsetTop = 140;
            _breachAnnouncement.HorizontalAlignment = HorizontalAlignment.Center;
            _breachAnnouncement.AddThemeFontSizeOverride("font_size", 32);
            _breachAnnouncement.Visible = false;
            AddChild(_breachAnnouncement);
        }

        private void UpdateWallLabel(Label label, CardinalDirection dir, ShieldWallManager swm)
        {
            string dirName = dir.ToString().ToUpper();
            if (swm.IsWallDestroyed(dir))
            {
                label.Text = $"  {dirName}: BREACHED";
                label.AddThemeColorOverride("font_color", new Color(0.95f, 0.3f, 0.1f));
            }
            else
            {
                float remaining = swm.GetTimeRemaining(dir);
                if (remaining > 0)
                {
                    int min = (int)(remaining / 60f);
                    int sec = (int)(remaining % 60f);
                    label.Text = $"  {dirName}: {min}:{sec:D2}";
                    label.AddThemeColorOverride("font_color", TronTheme.GridCyan);
                }
                else
                {
                    label.Text = $"  {dirName}: ACTIVE";
                    label.AddThemeColorOverride("font_color", TronTheme.GridCyan);
                }
            }
        }

        private void OnShieldWallDestroyed(CardinalDirection dir)
        {
            // Update wall label
            if (_wallLabels.TryGetValue(dir, out var label))
            {
                if (ServiceLocator.TryGet<ShieldWallManager>(out var swm))
                    UpdateWallLabel(label, dir, swm);
            }

            // Show breach announcement
            string dirName = dir.ToString().ToUpper();
            _breachAnnouncement.Text = $"{dirName} WALL BREACHED";
            _breachAnnouncement.AddThemeColorOverride("font_color", new Color(0.95f, 0.2f, 0.1f));
            _breachAnnouncement.Visible = true;
            _breachAnnouncementTimer = 3f;
            _breachFrame = Engine.GetProcessFrames();
        }

        private ulong _breachFrame = ulong.MaxValue;

        private void OnAnnouncement(string text)
        {
            if (_breachAnnouncement == null || string.IsNullOrEmpty(text)) return;
            // A wall breach fires its own event first in the same frame — keep its red styling
            bool breachThisFrame = _breachFrame == Engine.GetProcessFrames();
            _breachAnnouncement.Text = text.ToUpper();
            if (!breachThisFrame)
                _breachAnnouncement.AddThemeColorOverride("font_color", new Color(0.3f, 0.95f, 0.8f));
            _breachAnnouncement.Visible = true;
            _breachAnnouncementTimer = 3.5f;
        }

        // ── Wave Preview Panel ──

        private void BuildWavePreviewPanel()
        {
            EnsureRightColumn();
            _wavePreviewPanel = new VBoxContainer();
            _wavePreviewPanel.AddThemeConstantOverride("separation", 2);

            _wavePreviewHeader = new Label();
            _wavePreviewHeader.Text = "NEXT WAVE";
            _wavePreviewHeader.AddThemeFontSizeOverride("font_size", 15);
            _wavePreviewHeader.AddThemeColorOverride("font_color", new Color(0.35f, 0.75f, 0.95f));
            _wavePreviewHeader.HorizontalAlignment = HorizontalAlignment.Right;
            _wavePreviewPanel.AddChild(_wavePreviewHeader);

            _wavePreviewSurges = new VBoxContainer();
            _wavePreviewSurges.AddThemeConstantOverride("separation", 1);
            _wavePreviewPanel.AddChild(_wavePreviewSurges);

            _waveCard = Card(_wavePreviewPanel);
        }

        private VBoxContainer _rightColumn;
        private PanelContainer _shieldCard, _waveCard;

        private void EnsureRightColumn()
        {
            if (_rightColumn != null) return;
            _rightColumn = new VBoxContainer { Name = "RightColumn" };
            _rightColumn.SetAnchorsPreset(Control.LayoutPreset.TopRight);
            _rightColumn.OffsetTop = TopBarHeight + 10;
            _rightColumn.OffsetRight = -12;
            _rightColumn.OffsetLeft = -12;
            _rightColumn.GrowHorizontal = Control.GrowDirection.Begin;
            _rightColumn.AddThemeConstantOverride("separation", 8);
            _rightColumn.MouseFilter = Control.MouseFilterEnum.Ignore;
            AddChild(_rightColumn);
        }

        /// <summary>A dark card in the right-hand column holding <paramref name="content"/>.</summary>
        private PanelContainer Card(Control content)
        {
            var card = new PanelContainer();
            var st = new StyleBoxFlat { BgColor = new Color(0.02f, 0.03f, 0.06f, 0.78f) };
            st.SetCornerRadiusAll(4);
            st.ContentMarginLeft = st.ContentMarginRight = 12;
            st.ContentMarginTop = st.ContentMarginBottom = 8;
            card.AddThemeStyleboxOverride("panel", st);
            card.MouseFilter = Control.MouseFilterEnum.Ignore;
            card.AddChild(content);
            _rightColumn.AddChild(card);
            card.Visible = content.Visible;
            return card;
        }

        private void UpdateWavePreview()
        {
            if (_wavePreviewPanel == null) return;
            if (!ServiceLocator.TryGet<VineWaveManager>(out var wm)) return;

            // Show during build phase, hide during waves
            var phase = GameManager.Instance?.CurrentPhase ?? GamePhase.Build;
            bool showPreview = phase == GamePhase.Build || phase == GamePhase.WaveComplete;
            _wavePreviewPanel.Visible = showPreview;
            if (_waveCard != null) _waveCard.Visible = showPreview;
            if (!showPreview) return;

            int nextWave = wm.CurrentWave + 1;
            if (nextWave == _lastPreviewedWave) return;
            _lastPreviewedWave = nextWave;

            // Position below shield wall panel if visible

            // Clear old surge labels
            foreach (var child in _wavePreviewSurges.GetChildren())
                child.QueueFree();

            var waveData = VineWaveRegistry.Get(nextWave);
            if (waveData == null)
            {
                _wavePreviewHeader.Text = "WAVE PREVIEW";
                var noData = new Label();
                noData.Text = "  Procedural wave";
                noData.AddThemeFontSizeOverride("font_size", 15);
                noData.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.6f));
                noData.HorizontalAlignment = HorizontalAlignment.Right;
                _wavePreviewSurges.AddChild(noData);
                return;
            }

            _wavePreviewHeader.Text = $"WAVE {nextWave}" + (waveData.IsBossWave ? " — BOSS" : "");
            if (waveData.IsBossWave)
                _wavePreviewHeader.AddThemeColorOverride("font_color", new Color(0.95f, 0.3f, 0.1f));
            else
                _wavePreviewHeader.AddThemeColorOverride("font_color", new Color(0.35f, 0.75f, 0.95f));

            foreach (var surge in waveData.Surges)
            {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 6);
                row.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;

                // Enemy name
                var nameLabel = new Label();
                nameLabel.Text = surge.EnemyName ?? surge.Faction.ToString();
                nameLabel.AddThemeFontSizeOverride("font_size", 16);
                nameLabel.AddThemeColorOverride("font_color", new Color(0.86f, 0.89f, 0.99f));
                nameLabel.HorizontalAlignment = HorizontalAlignment.Right;
                row.AddChild(nameLabel);

                // Count
                var countLabel = new Label();
                countLabel.Text = $"x{surge.Count}";
                countLabel.AddThemeFontSizeOverride("font_size", 15);
                countLabel.AddThemeColorOverride("font_color", new Color(0.68f, 0.7f, 0.8f));
                row.AddChild(countLabel);

                // What it does, so the wave can be planned for: its trait, else its faction's habit
                var trait = new Label();
                trait.Text = surge.Traits != EnemyTraits.None ? TraitWords(surge.Traits) : FactionTrait(surge.Faction);
                trait.AddThemeFontSizeOverride("font_size", 14);
                trait.AddThemeColorOverride("font_color", surge.Traits != EnemyTraits.None ? TraitColor(surge.Traits) : new Color(0.6f, 0.62f, 0.7f));
                row.AddChild(trait);

                // Faction color pip
                var pip = new ColorRect();
                pip.CustomMinimumSize = new Vector2(10, 10);
                pip.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
                pip.Color = FactionColor(surge.Faction);
                row.AddChild(pip);

                // Boss indicator
                if (surge.IsBoss)
                {
                    var bossLabel = new Label();
                    bossLabel.Text = "BOSS";
                    bossLabel.AddThemeFontSizeOverride("font_size", 14);
                    bossLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.2f, 0.1f));
                    row.AddChild(bossLabel);
                }

                // Commander indicator
                if (surge.Commander != null)
                {
                    var cmdLabel = new Label();
                    cmdLabel.Text = "CMD";
                    cmdLabel.AddThemeFontSizeOverride("font_size", 14);
                    cmdLabel.AddThemeColorOverride("font_color", new Color(0.96f, 0.62f, 0.04f));
                    row.AddChild(cmdLabel);
                }

                _wavePreviewSurges.AddChild(row);
            }

            // What answers each trait in this wave
            var traits = EnemyTraits.None;
            foreach (var sg in waveData.Surges) traits |= sg.Traits;
            foreach (var t in new[] { EnemyTraits.Armoured, EnemyTraits.Flying, EnemyTraits.Shielded })
            {
                if ((traits & t) == 0) continue;
                var hint = new Label { Text = TraitCounter(t), HorizontalAlignment = HorizontalAlignment.Right };
                hint.AddThemeFontSizeOverride("font_size", 14);
                hint.AddThemeColorOverride("font_color", TraitColor(t).Lerp(Colors.White, 0.35f));
                _wavePreviewSurges.AddChild(hint);
            }

            // AXIS takes over every few waves: say so before it starts
            if (CorruptionManager.IsChaosWave(nextWave))
            {
                var warn = new Label { Text = "AXIS CHAOS this wave", HorizontalAlignment = HorizontalAlignment.Right };
                warn.AddThemeFontSizeOverride("font_size", 15);
                warn.AddThemeColorOverride("font_color", CorruptionManager.GetCorruptionColor());
                _wavePreviewSurges.AddChild(warn);
            }
        }

        internal static string TraitWords(EnemyTraits t)
        {
            var w = new System.Collections.Generic.List<string>();
            if ((t & EnemyTraits.Armoured) != 0) w.Add("armoured");
            if ((t & EnemyTraits.Flying) != 0) w.Add("flying");
            if ((t & EnemyTraits.Shielded) != 0) w.Add("shielded");
            return string.Join(", ", w);
        }

        internal static Color TraitColor(EnemyTraits t) =>
            (t & EnemyTraits.Armoured) != 0 ? new Color(0.72f, 0.78f, 0.88f)
            : (t & EnemyTraits.Flying) != 0 ? new Color(1f, 0.75f, 0.35f)
            : new Color(0.4f, 0.85f, 1f);

        /// <summary>The towers that answer a trait, as the wave card says it.</summary>
        internal static string TraitCounter(EnemyTraits t) => t switch
        {
            EnemyTraits.Armoured => "Armoured: Junk Turret, Scatter, BIT",
            EnemyTraits.Flying => "Flying: Flak, Tesla, BIT (walls don't stop it)",
            EnemyTraits.Shielded => "Shielded: Tesla Coil strips shields",
            _ => "",
        };

        private static string FactionTrait(VineEnemyFaction faction) => faction switch
        {
            VineEnemyFaction.Brute => "smashes walls",
            VineEnemyFaction.Ghost => "walks through walls",
            VineEnemyFaction.Swarm => "comes in packs",
            _ => "",
        };

        private static Color FactionColor(VineEnemyFaction faction)
        {
            return faction switch
            {
                VineEnemyFaction.Scavenger => new Color(0.95f, 0.2f, 0.15f),   // bright red
                VineEnemyFaction.Brute     => new Color(0.55f, 0.08f, 0.08f),  // dark crimson
                VineEnemyFaction.Ghost     => new Color(0.85f, 0.2f, 0.55f),   // magenta-red
                VineEnemyFaction.Swarm     => new Color(0.95f, 0.5f, 0.15f),   // orange-red
                _                          => new Color(0.5f, 0.5f, 0.6f),
            };
        }

        // ── Updates ──

        private void UpdateGold(int gold)
        {
            if (_resourceLabel != null) _resourceLabel.Text = $"Resources: {gold}";
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
            {
                // Continuous mode has no final wave ("Wave 21 / 20" after the authored set);
                // boss runs count toward the boss wave.
                var gm = GameManager.Instance;
                var bossSite = gm != null && gm.IsBossRun && gm.BossSectionId != null
                    ? TerritoryManager.GetSite(gm.BossSectionId) : null;
                _waveLabel.Text = bossSite != null && bossSite.BossWave > 0
                    ? $"Wave: {current} / {bossSite.BossWave}"
                    : $"Wave: {current}";
            }

            // Reset wave preview so it refreshes for the next wave
            _lastPreviewedWave = -1;

            // S2: live extraction counter
            if (_extractionLabel != null)
            {
                int extracted = GameManager.Instance?.TotalExtracted ?? 0;
                _extractionLabel.Text = $"Extracted: {extracted}";
            }
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
                GamePhase.BattleLoading => "DEPLOYING",
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
                // UX11: Schedule transition to debrief screen after 2s delay
                // Skip in autoplay — AutoPlayer handles run completion directly.
                // (AutoPlayer is an autoload, so Instance is never null — check IsActive.
                // The null check meant real players never reached the debrief.)
                if (AutoPlayer.Instance?.IsActive != true)
                    GameManager.Instance?.ScheduleDebrief(2.0f);
            }
        }

        private static Label MakeLabel(string text, int fontSize = 16)
        {
            var label = new Label();
            label.Text = text;
            label.AddThemeFontSizeOverride("font_size", fontSize);
            return label;
        }

        // ── Relic display ──

        private Label _equippedRelicsLabel;
        private Control _relicNotification;
        private float _relicNotifTimer;

        private void UpdateEquippedRelicsDisplay()
        {
            if (_equippedRelicsLabel == null) return;
            if (!ServiceLocator.TryGet<RelicManager>(out var rm) || rm.EquippedCount == 0)
            {
                _equippedRelicsLabel.Text = "";
                return;
            }

            var names = new System.Collections.Generic.List<string>();
            foreach (var id in rm.EquippedRelics)
            {
                var relic = RelicManager.GetRelicById(id);
                if (relic != null) names.Add(relic.Value.Name);
            }
            _equippedRelicsLabel.Text = names.Count > 0 ? $"[{string.Join(" | ", names)}]" : "";
        }

        private void OnRelicAcquired(string relicId, bool isNew)
        {
            var relic = RelicManager.GetRelicById(relicId);
            if (relic == null) return;

            ShowRelicNotification(relic.Value, isNew);
            UpdateEquippedRelicsDisplay();
        }

        private void ShowRelicNotification(RelicRegistry.Relic relic, bool isNew)
        {
            // Remove existing notification
            _relicNotification?.QueueFree();

            var panel = new PanelContainer();
            panel.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
            panel.OffsetTop = -80;
            panel.OffsetBottom = -20;
            panel.OffsetLeft = -220;
            panel.OffsetRight = 220;

            var rarityColor = GetRarityColor(relic.Rarity);

            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.024f, 0.05f, 0.1f, 0.92f);
            style.BorderColor = new Color(rarityColor.R, rarityColor.G, rarityColor.B, 0.5f);
            style.SetBorderWidthAll(2);
            style.ContentMarginLeft = 0;
            style.ContentMarginRight = 16;
            style.ContentMarginTop = 0;
            style.ContentMarginBottom = 0;
            panel.AddThemeStyleboxOverride("panel", style);
            AddChild(panel);

            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 12);
            panel.AddChild(hbox);

            // Rarity color block (left)
            var colorBlock = new ColorRect();
            colorBlock.CustomMinimumSize = new Vector2(6, 0);
            colorBlock.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            colorBlock.Color = rarityColor;
            hbox.AddChild(colorBlock);

            // Icon
            var iconLabel = new Label();
            iconLabel.Text = relic.Icon ?? "diamond";
            iconLabel.AddThemeFontSizeOverride("font_size", 20);
            iconLabel.AddThemeColorOverride("font_color", rarityColor);
            iconLabel.VerticalAlignment = VerticalAlignment.Center;
            hbox.AddChild(iconLabel);

            // Info column
            var info = new VBoxContainer();
            info.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            info.AddThemeConstantOverride("separation", 1);
            hbox.AddChild(info);

            var nameLabel = new Label();
            nameLabel.Text = relic.Name.ToUpper();
            nameLabel.AddThemeFontSizeOverride("font_size", 18);
            nameLabel.AddThemeColorOverride("font_color", Colors.White);
            info.AddChild(nameLabel);

            var rarityLabel = new Label();
            rarityLabel.Text = isNew ? $"{relic.Rarity.ToUpper()} — NEW" : relic.Rarity.ToUpper();
            rarityLabel.AddThemeFontSizeOverride("font_size", 11);
            rarityLabel.AddThemeColorOverride("font_color", rarityColor);
            info.AddChild(rarityLabel);

            _relicNotification = panel;
            _relicNotifTimer = 4f; // Show for 4 seconds
        }

        private void UpdateRelicNotification(float dt)
        {
            if (_relicNotification == null) return;
            _relicNotifTimer -= dt;

            // Fade out in last second
            if (_relicNotifTimer < 1f && _relicNotifTimer > 0)
                _relicNotification.Modulate = new Color(1, 1, 1, _relicNotifTimer);
            else if (_relicNotifTimer <= 0)
            {
                _relicNotification.QueueFree();
                _relicNotification = null;
            }
        }

        private static Color GetRarityColor(string rarity) => (rarity?.ToLower()) switch
        {
            "common" => new Color(0.55f, 0.57f, 0.59f),
            "uncommon" => new Color(0.24f, 0.98f, 0.89f),
            "rare" => new Color(0.66f, 0.33f, 0.97f),
            "legendary" => new Color(0.96f, 0.62f, 0.04f),
            _ => new Color(0.75f, 0.78f, 0.88f)
        };

        public override void _ExitTree()
        {
            GameEvents.OnResourcesChanged -= UpdateGold;
            GameEvents.OnCoreLivesChanged -= UpdateLives;
            GameEvents.OnHarvesterHPChanged -= UpdateHarvesterHP;
            GameEvents.OnPhaseChanged -= UpdatePhase;
            GameEvents.OnPlayerHPChanged -= UpdatePlayerHP;
            GameEvents.OnMechXpChanged -= UpdateMechXp;
            GameEvents.OnPlayerMaterialsChanged -= UpdatePlayerMaterials;
            GameEvents.OnAbilityCooldownChanged -= UpdateAbilityCooldown;
            GameEvents.OnCorruptionStarted -= OnCorruptionStarted;
            GameEvents.OnCorruptionEnded -= OnCorruptionEnded;
            GameEvents.OnRelicAcquired -= OnRelicAcquired;
            GameEvents.OnAnnouncement -= OnAnnouncement;
        }
    }
}
