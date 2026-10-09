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
        private string _lastPreviewedAscendant;

        public override void _Ready()
        {
            BuildTopBar();
            BuildBottomBar();
            BuildTooltip();
            BuildPlayerHUD();
            // First runs: a checklist of the basics, ticked off as they happen
            if (!RunGuide.Dismissed && !SafeFile.IsSandboxed) AddChild(new RunGuide());

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
            GameEvents.OnAbilityUsed += OnAbilityUsed;
            GameEvents.OnMiningModeChanged += UpdateMiningMode;
            GameEvents.OnMaterialTypeSelected += UpdateMaterialType;
            GameEvents.OnMaterialsAccumulated += UpdateMaterialsAccumulated;
            GameEvents.OnCorruptionStarted += OnCorruptionStarted;
            GameEvents.OnCorruptionEnded += OnCorruptionEnded;

            BuildChaosHUD();
            BuildFlyoverOverlay();
            BuildPlacementPrompt();
            BuildObjectiveCard();
            BuildStrikesCard();
            BuildShieldWallHUD();
            BuildWavePreviewPanel();

            GameEvents.OnShieldWallDestroyed += OnShieldWallDestroyed;
            GameEvents.OnRelicAcquired += OnRelicAcquired;
            BuildHurtVignette();
            // Drops fly up into the Resources counter
            Flyout = new ResourceFlyout { Name = "ResourceFlyout" };
            AddChild(Flyout);
            Flyout.Init(_resourceLabel);
            GameEvents.OnResourcesDropped += OnResourcesDroppedFx;
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
            _sendAllButton.Text = "TO W5 [Shift+Space]";
            _sendAllButton.TooltipText = "Send every wave up to the next milestone (a multiple of 5), a few seconds apart";
            _sendAllButton.CustomMinimumSize = new Vector2(170, 35);
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

        /// <summary>T: switch what the Spire mines (between waves), or pick a material first.</summary>
        internal void ToggleMiningFromKey()
        {
            if (!ServiceLocator.TryGet<VineGrid>(out var grid) || grid.Harvester == null) return;
            var h = grid.Harvester;
            var phase = GameManager.Instance?.CurrentPhase ?? GamePhase.Build;
            if (phase != GamePhase.Build)
            {
                GameEvents.OnAnnouncement?.Invoke("Switch what the Spire mines between waves [T]");
                return;
            }
            if (h.SelectedMaterial == MaterialType.None)
            {
                if (ServiceLocator.TryGet<VinePlacer>(out var placer)) placer.ShowMaterialTypeSelection(h);
                return;
            }
            PlayUIClick();
            h.ToggleMode();
            UpdateMiningButtonText();
            GameEvents.OnAnnouncement?.Invoke(h.CurrentMode == MiningMode.Materials
                ? $"MINING {h.SelectedMaterial.ToString().ToUpperInvariant()} MATERIALS: for BIT and the Spire's BIT upgrades"
                : "MINING RESOURCES: for towers and Spire upgrades");
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
                string mode = h.CurrentMode == MiningMode.Resources ? "Resources" : "Materials";
                _miningBuildingBtn.Text = $"⛏ Spire mines: {mode}\n[T] or click to switch";
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
            btn.Text = BuildButtonText(data);
            _buildButtons[type] = btn;
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

        // Build bar prices follow the perk tree (free towers, cheaper walls)
        private readonly System.Collections.Generic.Dictionary<VineNodeType, Button> _buildButtons = new();
        private int _lastFreeTowers = -1;

        private static string BuildButtonText(VineNodeData data)
        {
            int cost = MetaRun.PlaceCost(data);
            return cost == 0 ? $"{data.Name}\n(FREE)" : $"{data.Name}\n({cost}r)";
        }

        private void RefreshBuildPrices()
        {
            if (_lastFreeTowers == MetaRun.FreeTowersLeft) return;
            _lastFreeTowers = MetaRun.FreeTowersLeft;
            foreach (var kv in _buildButtons)
                if (IsInstanceValid(kv.Value) && VineNodeRegistry.Get(kv.Key) is { } d)
                    kv.Value.Text = BuildButtonText(d);
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
            long __pt = FrameProfiler.Start();
            try
            {
                float dt = (float)delta;
                UpdateRelicNotification(dt);
            TickHurt(dt);
                if ((_economyTimer -= dt) <= 0f) { _economyTimer = 0.5f; RefreshEconomyLine(); RefreshObjective(); RefreshSendAll(); RefreshStrikes(); }
                RefreshBuildPrices();
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

                // What the last ability did fades after a moment
                if (_abilityResultTimer > 0f)
                {
                    _abilityResultTimer -= dt;
                    if (_abilityResultTimer <= 0f && _abilityResult != null) _abilityResult.Visible = false;
                }

                // ── What a tower gets where the ghost stands: the dome, crew links ──
                if (_placementPrompt != null)
                {
                    string hint = PlacementHint();
                    _placementPrompt.Visible = hint.Length > 0;
                    if (hint.Length > 0 && _placementPrompt.Text != hint) _placementPrompt.Text = hint;
                }

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
                        _chaosTimerLabel.Text = $"AXIS CHAOS: {cm.RemainingDuration:F1}s";
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
                        // How the wave ends: when every enemy in it is down
                        if (_waveTimerLabel != null)
                        {
                            int left = wm.EnemiesRemaining, stacked = wm.PendingStackedWaves;
                            _waveTimerLabel.Text = $"Wave {wm.CurrentWave}: {left} enemies left" +
                                (stacked > 0 ? $"  (+{stacked} more wave{(stacked == 1 ? "" : "s")} coming)" : "") +
                                "  ·  it ends when they're all down";
                            _waveTimerLabel.Visible = true;
                        }
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
            finally { FrameProfiler.Stop("hud", __pt); }
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
                else if (key.Keycode == Key.T && !key.CtrlPressed && !key.ShiftPressed)
                {
                    // [T] was on the top bar and in the guide but nothing listened for it
                    ToggleMiningFromKey();
                    GetViewport().SetInputAsHandled();
                }
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

            AddHelpSection(vbox, "MINING", new Color(1f, 0.85f, 0.3f));
            AddHelpText(vbox, "The Spire mines on its own every 5 s. [T] (or the button bottom left) picks what:");
            AddHelpText(vbox, "  Resources for towers and upgrades, or Materials for BIT's abilities and upgrades.");
            AddHelpText(vbox, $"  Build next to a gold RESOURCE NODE to mine it as well (+{Constants.RESOURCE_NODE_BONUS} each).");

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
            AddHelpText(vbox, "Q  Shock Blast: hits and stuns every enemy around BIT");
            AddHelpText(vbox, "E  Repair Pulse: repairs the Spire, BIT and towers near BIT");
            AddHelpText(vbox, "BIT heals by itself a few seconds after its last hit, and fast inside the Spire (G)");
            AddHelpText(vbox, $"Towers inside the Spire's golden dome fire {Constants.DOME_RATE_BONUS * 100:0}% faster and mend themselves");
            AddHelpText(vbox, $"Towers side by side link up (cyan): +{Constants.CREW_BONUS_PER_LINK * 100:0}% damage and rate per link, up to {Constants.CREW_MAX_LINKS}. Grey links do nothing");
            AddHelpText(vbox, "R  Overclock: towers near BIT fire faster and harder for 6 s");
            AddHelpText(vbox, "   Each costs Materials (the blue bar) and recharges; hover a row in BIT's panel.");

            AddHelpSection(vbox, "THE SPIRE", new Color(0.95f, 0.6f, 0.25f));
            AddHelpText(vbox, "F at the Spire     upgrades for the Spire and BIT, refill and training");
            AddHelpText(vbox, "G at the Spire    climb in: the mouse aims the Spire's cannon, left mouse fires");
            AddHelpText(vbox, "                  (V: gunner view, first person from the top; F or G climbs back out)");
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
            AddHelpText(vbox, "A wave ends when every enemy in it is down (the count shows by the wave buttons).");
            AddHelpText(vbox, "Space        send the next wave now      Shift+Space  send the next three");
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

        /// <summary>The Send All button says how far it sends ("TO W15").</summary>
        private void RefreshSendAll()
        {
            if (_sendAllButton == null || !ServiceLocator.TryGet<VineWaveManager>(out var wm)) return;
            string t = $"TO W{wm.SendAllTarget} [Shift+Space]";
            if (_sendAllButton.Text != t) _sendAllButton.Text = t;
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
            int price = MetaRun.PlaceCost(data);
            _infoTitle.Text = price == 0 ? $"{data.Name}   FREE" : $"{data.Name}   {price}r";
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
            _infoTitle.Text = placed ? "Mining" : "Mining Building";
            _infoRole.Text = placed
                ? "The Spire mines by itself, every 5 s. Click or press [T] to switch what it mines."
                : "Place it to start mining.";
            _infoBest.Text = "Resources: towers and their upgrades, Spire upgrades.";
            _infoWeak.Text = "Materials: BIT's Q/E/R abilities and BIT's upgrades ([F] at the Spire). Half of every drop is banked too.";
            _infoStats.Text = $"Mine more: build any tower next to a gold RESOURCE NODE on the map (+{Constants.RESOURCE_NODE_BONUS} each, every 5 s). Kills drop more, and each cleared wave pays a bonus.";
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
            float minX = (_playerPanel?.GetGlobalRect().End.X ?? 312f) + 12f; // clear of BIT's panel
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

        private PanelContainer _playerPanel;

        private void BuildPlayerHUD()
        {
            var playerPanel = _playerPanel = new PanelContainer();
            playerPanel.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
            // Sits above the build bar and grows upward with its contents (bigger text no longer
            // pushes it down into the bar)
            playerPanel.OffsetBottom = -120;
            playerPanel.OffsetTop = -120 - 150;
            playerPanel.GrowVertical = Control.GrowDirection.Begin;
            playerPanel.OffsetLeft = 12;
            playerPanel.OffsetRight = 312;
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
            _playerHPBar.CustomMinimumSize = new Vector2(280, 14);
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
            _playerMaterialsBar.CustomMinimumSize = new Vector2(280, 14);
            _playerMaterialsBar.MaxValue = Constants.VINE_PLAYER_MAX_MATERIALS;
            _playerMaterialsBar.Value = Constants.VINE_PLAYER_MAX_MATERIALS;
            _playerMaterialsBar.ShowPercentage = false;
            var manaBg = new StyleBoxFlat { BgColor = new Color(0.05f, 0.05f, 0.2f) };
            _playerMaterialsBar.AddThemeStyleboxOverride("background", manaBg);
            var manaFill = new StyleBoxFlat { BgColor = new Color(0.2f, 0.4f, 0.9f) };
            _playerMaterialsBar.AddThemeStyleboxOverride("fill", manaFill);
            vbox.AddChild(_playerMaterialsBar);

            // Abilities: key, name, cost and state on a row each, so Q/E/R say what they are
            // (it was "[Q] Ready [E] Ready [R] Ready" and nothing else)
            var abilityBox = new VBoxContainer();
            abilityBox.AddThemeConstantOverride("separation", 1);
            vbox.AddChild(abilityBox);
            string[] keys = { "Q", "E", "R" };
            var defs = VinePlayer.GetDefaultAbilities();
            for (int i = 0; i < 3; i++)
            {
                int slot = i;
                var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Stop };
                row.AddThemeConstantOverride("separation", 6);
                abilityBox.AddChild(row);
                var key = MakeLabel($"[{keys[i]}]", 15);
                key.AddThemeColorOverride("font_color", defs[i].IconColor);
                row.AddChild(key);
                var name = MakeLabel(defs[i].Name, 15);
                name.AddThemeColorOverride("font_color", defs[i].IconColor.Lightened(0.25f));
                name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                row.AddChild(name);
                var state = MakeLabel($"{defs[i].MaterialsCost:0} M", 14);
                state.HorizontalAlignment = HorizontalAlignment.Right;
                state.CustomMinimumSize = new Vector2(96, 0);
                row.AddChild(state);
                row.MouseEntered += () => ShowAbilityInfo(slot, row);
                row.MouseExited += HideTooltip;
                _abilityNames[i] = name;
                _abilityLabels[i] = state;
                _abilityRows[i] = row;
            }
            _abilityResult = MakeLabel("", 14);
            _abilityResult.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _abilityResult.CustomMinimumSize = new Vector2(260, 0);
            _abilityResult.Visible = false;
            vbox.AddChild(_abilityResult);
        }

        private readonly Label[] _abilityNames = new Label[3];
        private readonly HBoxContainer[] _abilityRows = new HBoxContainer[3];
        private Label _abilityResult;
        private float _abilityResultTimer;
        private float _playerMaterials = -1f;

        private VinePlayerAbility AbilityAt(int slot)
        {
            var abilities = ServiceLocator.TryGet<VinePlayer>(out var pl) ? pl.GetAbilities() : null;
            abilities ??= VinePlayer.GetDefaultAbilities();
            return slot >= 0 && slot < abilities.Length ? abilities[slot] : null;
        }

        /// <summary>Tests: hover the ability row for <paramref name="slot"/>.</summary>
        internal void TestShowAbilityInfo(int slot)
        {
            if (slot < 0 || slot >= 3) { HideTooltip(); return; }
            if (_abilityRows[slot] != null) ShowAbilityInfo(slot, _abilityRows[slot]);
        }

        private void ShowAbilityInfo(int slot, Control over)
        {
            var a = AbilityAt(slot);
            if (_infoCard == null || a == null) return;
            string[] keys = { "Q", "E", "R" };
            _infoTitle.Text = $"[{keys[slot]}] {a.Name}";
            _infoRole.Text = a.Description;
            _infoBest.Text = $"Costs {a.MaterialsCost:0} Materials, then {a.Cooldown:0} s to recharge.";
            _infoWeak.Text = "";
            _infoStats.Text = "Materials refill slowly by themselves; Materials mode [T] banks more at the Spire ([F] there to refill BIT).";
            PlaceInfoCardBeside(over);
        }

        /// <summary>To the right of BIT's panel, level with the hovered row.</summary>
        private void PlaceInfoCardBeside(Control over)
        {
            _infoBest.Visible = _infoBest.Text.Length > 0;
            _infoWeak.Visible = _infoWeak.Text.Length > 0;
            _infoStats.Visible = _infoStats.Text.Length > 0;
            _infoCard.Size = new Vector2(InfoCardWidth, 0);
            _infoCard.ResetSize();
            var vis = GetViewport().GetVisibleRect();
            var panel = _playerPanel?.GetGlobalRect() ?? over.GetGlobalRect();
            var size = _infoCard.GetCombinedMinimumSize();
            float y = Mathf.Clamp(over.GetGlobalRect().GetCenter().Y - size.Y / 2f, vis.Position.Y + 60f, vis.End.Y - size.Y - 130f);
            _infoCard.Position = new Vector2(panel.End.X + 12f, y);
            _infoCard.Visible = true;
        }

        private void OnAbilityUsed(int slot, string text, bool used)
        {
            if (_abilityResult == null || slot < 0 || slot >= 3) return;
            var a = AbilityAt(slot);
            if (string.IsNullOrEmpty(text) && !used) return;
            _abilityResult.Text = used ? $"{a?.Name}: {text}" : $"{a?.Name} {text}";
            _abilityResult.AddThemeColorOverride("font_color", used ? (a?.IconColor.Lightened(0.3f) ?? Colors.White) : new Color(1f, 0.45f, 0.4f));
            _abilityResult.Visible = true;
            _abilityResultTimer = 2.5f;
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
            _bitHpFrac = max > 0f ? current / max : 1f;
        }

        // ── BIT getting hurt: a red edge round the screen, stronger for bigger hits, and a slow
        // pulse while BIT is low (there was only a white flash on a model that's often off-screen)
        private ColorRect _hurtVignette;
        private ShaderMaterial _hurtMat;
        private float _hurt, _bitHpFrac = 1f, _hurtClock, _hpBarFlash;

        /// <summary>How strong the red edge is right now (tests).</summary>
        public float HurtVignette => _hurtMat != null ? (float)_hurtMat.GetShaderParameter("strength") : 0f;

        private void BuildHurtVignette()
        {
            _hurtMat = new ShaderMaterial { Shader = new Shader { Code = @"
shader_type canvas_item;
uniform float strength = 0.0;
void fragment() {
    vec2 d = abs(UV - 0.5) * 2.0;
    float edge = smoothstep(0.55, 1.05, max(d.x * 0.9, d.y) + length(d) * 0.18);
    COLOR = vec4(0.85, 0.05, 0.04, edge * strength);
}" } };
            _hurtVignette = new ColorRect { Name = "HurtVignette", Material = _hurtMat, MouseFilter = Control.MouseFilterEnum.Ignore, Color = Colors.White };
            _hurtVignette.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(_hurtVignette);
            MoveChild(_hurtVignette, 0);
            GameEvents.OnPlayerDamaged += OnBitDamaged;
        }

        private void OnBitDamaged(float amount)
        {
            if (!IsInstanceValid(this)) return;
            float max = ServiceLocator.TryGet<VinePlayer>(out var p) ? p.MaxHP : 100f;
            _hurt = Mathf.Max(_hurt, Mathf.Clamp(0.45f + amount / Mathf.Max(1f, max) * 3f, 0.45f, 1f));
            _hpBarFlash = 1f;
        }

        private void TickHurt(float dt)
        {
            if (_hurtMat == null) return;
            _hurt = Mathf.Max(0f, _hurt - dt * 1.6f);
            _hurtClock += dt;
            float low = _bitHpFrac < 0.3f && _bitHpFrac > 0f ? (0.18f + 0.12f * Mathf.Sin(_hurtClock * Mathf.Tau)) * (1f - _bitHpFrac / 0.3f + 0.4f) : 0f;
            float s = Mathf.Max(_hurt, low);
            _hurtMat.SetShaderParameter("strength", s);
            _hurtVignette.Visible = s > 0.005f;
            if (_playerHPBar != null)
            {
                _hpBarFlash = Mathf.Max(0f, _hpBarFlash - dt * 3f);
                _playerHPBar.Modulate = Colors.White.Lerp(new Color(2f, 1.6f, 1.6f), _hpBarFlash);
            }
        }

        private void UpdatePlayerMaterials(float current, float max)
        {
            if (_playerMaterialsBar != null)
            {
                _playerMaterialsBar.MaxValue = max;
                _playerMaterialsBar.Value = current;
            }
            bool changedTier = _playerMaterials < 0f || Mathf.FloorToInt(current) != Mathf.FloorToInt(_playerMaterials);
            _playerMaterials = current;
            if (changedTier) for (int i = 0; i < 3; i++) RefreshAbilityRow(i);
        }

        private void UpdateAbilityCooldown(int slot, float remaining)
        {
            if (slot < 0 || slot >= 3) return;
            _abilityCooldowns[slot] = remaining;
            RefreshAbilityRow(slot);
        }

        private void RefreshAbilityRow(int slot)
        {
            if (_abilityLabels[slot] == null) return;
            var a = AbilityAt(slot);
            if (a == null) return;
            if (_abilityNames[slot] != null) _abilityNames[slot].Text = a.Name;
            float remaining = _abilityCooldowns[slot];
            bool broke = _playerMaterials >= 0f && _playerMaterials < a.MaterialsCost;
            _abilityLabels[slot].Text = remaining > 0.1f ? $"{remaining:F1} s"
                : broke ? $"needs {a.MaterialsCost:0} M" : $"{a.MaterialsCost:0} M  ready";
            _abilityLabels[slot].AddThemeColorOverride("font_color",
                remaining > 0.1f ? new Color(0.6f, 0.62f, 0.68f) : broke ? new Color(1f, 0.5f, 0.45f) : new Color(0.75f, 1f, 0.75f));
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

        /// <summary>
        /// While placing a tower: what it would get on that cell ("Inside the Spire's dome: +15%
        /// fire rate, mends itself · links to 2 towers: +10% crew").
        /// </summary>
        public string PlacementHint()
        {
            if (!ServiceLocator.TryGet<VinePlacer>(out var placer) || placer.GhostCell is not Vector2I cell || placer.SelectedType is not VineNodeType type)
                return "";
            var data = VineNodeRegistry.Get(type);
            if (data == null || !data.AutoFires) return "";
            var grid = ServiceLocator.Get<VineGrid>();
            var parts = new System.Collections.Generic.List<string>();
            var world = grid.GridToWorld(cell);
            bool crewType = type != VineNodeType.BuffEmitter;
            if (crewType && ServiceLocator.TryGet<ConversionDome>(out var dome) && IsInstanceValid(dome) && dome.IsInsideDome(world))
                parts.Add($"Inside the Spire's dome: +{Constants.DOME_RATE_BONUS * 100:0}% fire rate, mends itself");
            int crew = 0, relays = 0;
            foreach (var d in new[] { Vector2I.Up, Vector2I.Down, Vector2I.Left, Vector2I.Right })
            {
                var n = grid.GetNode(cell + d);
                if (n == null) continue;
                if (n.IsCrew) crew++;
                if (n.Data?.Type == VineNodeType.BuffEmitter) relays++;
            }
            if (crewType && crew > 0) parts.Add($"links to {crew} tower{(crew == 1 ? "" : "s")}: +{Mathf.Min(crew, Constants.CREW_MAX_LINKS) * Constants.CREW_BONUS_PER_LINK * 100:0}% crew");
            if (type == VineNodeType.BuffEmitter) parts.Add(crew > 0 ? $"boosts {crew} tower{(crew == 1 ? "" : "s")} beside it" : "nothing beside it to boost yet");
            else if (relays > 0) parts.Add("next to a relay: +25% damage and rate");
            return string.Join("  ·  ", parts);
        }

        private void BuildPlacementPrompt()
        {
            // What the cell under the build ghost gives a tower: centred above the build bar
            _placementPrompt = new Label { Name = "PlacementHint", MouseFilter = Control.MouseFilterEnum.Ignore };
            _placementPrompt.Text = "";
            _placementPrompt.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
            _placementPrompt.OffsetTop = -178;
            _placementPrompt.OffsetBottom = -146;
            _placementPrompt.OffsetLeft = -620;
            _placementPrompt.OffsetRight = 620;
            _placementPrompt.HorizontalAlignment = HorizontalAlignment.Center;
            _placementPrompt.AddThemeFontSizeOverride("font_size", 20);
            _placementPrompt.AddThemeColorOverride("font_color", BitPalette.Accent);
            _placementPrompt.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
            _placementPrompt.AddThemeConstantOverride("outline_size", 8);
            _placementPrompt.Visible = false;
            AddChild(_placementPrompt);
        }

        // ── The run's goal, always in view (top of the right column) ──
        private Label _objectiveLabel, _objectiveRegion;
        private PanelContainer _objectiveCard;
        private string _objectiveShown;

        private void BuildObjectiveCard()
        {
            EnsureRightColumn();
            var col = new VBoxContainer { Name = "Objective", MouseFilter = Control.MouseFilterEnum.Ignore };
            col.AddThemeConstantOverride("separation", 1);
            var head = MakeLabel("OBJECTIVE", 14);
            head.AddThemeColorOverride("font_color", new Color(0.35f, 0.95f, 0.75f));
            head.HorizontalAlignment = HorizontalAlignment.Right;
            col.AddChild(head);
            _objectiveLabel = MakeLabel("", 16);
            _objectiveLabel.HorizontalAlignment = HorizontalAlignment.Right;
            _objectiveLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _objectiveLabel.CustomMinimumSize = new Vector2(300, 0);
            col.AddChild(_objectiveLabel);
            _objectiveRegion = MakeLabel("", 14);
            _objectiveRegion.HorizontalAlignment = HorizontalAlignment.Right;
            _objectiveRegion.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _objectiveRegion.CustomMinimumSize = new Vector2(300, 0);
            _objectiveRegion.AddThemeColorOverride("font_color", new Color(0.7f, 0.78f, 0.86f));
            col.AddChild(_objectiveRegion);
            _objectiveCard = Card(col);
            RefreshObjective();
        }

        /// <summary>The objective card's text for this wave (and the region's progress under it).</summary>
        private void RefreshObjective()
        {
            if (_objectiveLabel == null) return;
            var gm = GameManager.Instance;
            int wave = ServiceLocator.TryGet<VineWaveManager>(out var wm) ? wm.CurrentWave : 0;
            string line = gm?.ObjectiveLine(wave) ?? "";
            string site = gm?.CurrentTerritorySectionId;
            string region = string.IsNullOrEmpty(site) || gm?.IsBossRun == true ? "" : TerritoryManager.RegionProgressLine(site, gm.MetaSave);
            string key = line + "|" + region;
            if (key == _objectiveShown) return;
            _objectiveShown = key;
            _objectiveLabel.Text = line;
            _objectiveRegion.Text = region;
            _objectiveRegion.Visible = region.Length > 0;
            if (_objectiveCard != null) _objectiveCard.Visible = line.Length > 0;
        }

        /// <summary>The pickups that fly to the Resources counter.</summary>
        public ResourceFlyout Flyout { get; private set; }

        private void OnResourcesDroppedFx(Vector3 pos, int amount)
        {
            if (!IsInstanceValid(this)) return;
            Flyout?.Drop(pos, amount);
            VfxFactory.SpawnScrapCollectPop(GetTree(), pos + Vector3.Up * 0.4f);
        }

        /// <summary>The objective card's goal line (tests).</summary>
        public string ObjectiveText => _objectiveLabel?.Text ?? "";

        // ── Strike charges bought at the Spire, and their keys ──
        private Label _strikesLabel;
        private PanelContainer _strikesCard;

        private void BuildStrikesCard()
        {
            EnsureRightColumn();
            _strikesLabel = MakeLabel("", 15);
            _strikesLabel.HorizontalAlignment = HorizontalAlignment.Right;
            _strikesLabel.AddThemeColorOverride("font_color", new Color(1f, 0.8f, 0.4f));
            _strikesLabel.Visible = false;
            _strikesCard = Card(_strikesLabel);
        }

        private void RefreshStrikes()
        {
            if (_strikesLabel == null) return;
            var st = SpireStation.Current;
            string text = "";
            if (st != null && st.TotalCharges > 0)
                text = "STRIKES READY\n" + string.Join("\n", st.Data.Strikes.Where(x => st.Charges(x.Id) > 0).Select(x => $"[{x.Key}] {x.Name} x{st.Charges(x.Id)}"));
            if (_strikesLabel.Text != text) _strikesLabel.Text = text;
            _strikesLabel.Visible = text.Length > 0;
            if (_strikesCard != null) _strikesCard.Visible = text.Length > 0;
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
            _breachAnnouncement = new Label { Name = "Announcement" };
            // A centred band across the middle of the screen: anchored at the centre point with no
            // width, long lines grew off the right edge
            _breachAnnouncement.AnchorLeft = 0.15f;
            _breachAnnouncement.AnchorRight = 0.85f;
            _breachAnnouncement.OffsetTop = 140;
            _breachAnnouncement.HorizontalAlignment = HorizontalAlignment.Center;
            _breachAnnouncement.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _breachAnnouncement.MouseFilter = Control.MouseFilterEnum.Ignore;
            _breachAnnouncement.AddThemeFontSizeOverride("font_size", 32);
            _breachAnnouncement.AddThemeConstantOverride("outline_size", 6);
            _breachAnnouncement.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.85f));
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
            string ascendant = ServiceLocator.TryGet<AscendantManager>(out var am) ? am.PendingName : null;
            if (nextWave == _lastPreviewedWave && ascendant == _lastPreviewedAscendant) return;
            _lastPreviewedWave = nextWave;
            _lastPreviewedAscendant = ascendant;

            // Position below shield wall panel if visible

            // Clear old surge labels
            foreach (var child in _wavePreviewSurges.GetChildren())
                child.QueueFree();

            // An Ascendant comes with this wave: say who and what to do about it
            if (ascendant != null)
            {
                var asc = new Label
                {
                    Name = "AscendantWarning",
                    Text = $"ASCENDANT: {ascendant}\nkill it before it reaches the Spire",
                    HorizontalAlignment = HorizontalAlignment.Right,
                };
                asc.AddThemeFontSizeOverride("font_size", 15);
                asc.AddThemeColorOverride("font_color", new Color(0.85f, 0.6f, 1f));
                _wavePreviewSurges.AddChild(asc);
            }

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
            foreach (var t in new[] { EnemyTraits.Armoured, EnemyTraits.Flying, EnemyTraits.Shielded, EnemyTraits.Empowerer })
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
            (t & EnemyTraits.Empowerer) != 0 ? new Color(1f, 0.4f, 0.95f)
            : (t & EnemyTraits.Armoured) != 0 ? new Color(0.72f, 0.78f, 0.88f)
            : (t & EnemyTraits.Flying) != 0 ? new Color(1f, 0.75f, 0.35f)
            : new Color(0.4f, 0.85f, 1f);

        /// <summary>The towers that answer a trait, as the wave card says it.</summary>
        internal static string TraitCounter(EnemyTraits t) => t switch
        {
            EnemyTraits.Armoured => "Armoured: Junk Turret, Scatter, BIT",
            EnemyTraits.Flying => "Flying: Flak, Tesla, BIT (walls don't stop it)",
            EnemyTraits.Shielded => "Shielded: Tesla Coil strips shields",
            EnemyTraits.Empowerer => "Empowerer: kill or stun it to break its tether",
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
            RefreshObjective();

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

            // Loot is a moment: the middle of the screen (it was a small card under the build bar)
            Celebration.Show(isNew ? $"New relic: {relic.Value.Rarity}" : $"Relic: {relic.Value.Rarity}", relic.Value.Name,
                relic.Value.Desc ?? "", GetRarityColor(relic.Value.Rarity), isNew ? 3.5f : 2.2f);
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
            GameEvents.OnResourcesDropped -= OnResourcesDroppedFx;
            GameEvents.OnPlayerDamaged -= OnBitDamaged;
            GameEvents.OnResourcesChanged -= UpdateGold;
            GameEvents.OnCoreLivesChanged -= UpdateLives;
            GameEvents.OnHarvesterHPChanged -= UpdateHarvesterHP;
            GameEvents.OnPhaseChanged -= UpdatePhase;
            GameEvents.OnPlayerHPChanged -= UpdatePlayerHP;
            GameEvents.OnMechXpChanged -= UpdateMechXp;
            GameEvents.OnPlayerMaterialsChanged -= UpdatePlayerMaterials;
            GameEvents.OnAbilityCooldownChanged -= UpdateAbilityCooldown;
            GameEvents.OnAbilityUsed -= OnAbilityUsed;
            GameEvents.OnCorruptionStarted -= OnCorruptionStarted;
            GameEvents.OnCorruptionEnded -= OnCorruptionEnded;
            GameEvents.OnRelicAcquired -= OnRelicAcquired;
            GameEvents.OnAnnouncement -= OnAnnouncement;
        }
    }
}
