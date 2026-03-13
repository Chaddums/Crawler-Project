using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Main battle HUD — scrap counter, wave info, tower build bar, lives.
    /// </summary>
    public partial class HUD : CanvasLayer
    {
        private Label _scrapLabel;
        private Label _waveLabel;
        private Label _livesLabel;
        private Label _phaseLabel;
        private HBoxContainer _towerBar;
        private Button _startWaveBtn;
        private Button _speedBtn;

        public override void _Ready()
        {
            BuildUI();

            GameEvents.OnScrapChanged += UpdateScrap;
            GameEvents.OnWaveStarted += w => UpdateWave(w);
            GameEvents.OnWaveCompleted += w => UpdateWave(w);
            GameEvents.OnCoreLivesChanged += UpdateLives;
            GameEvents.OnPhaseChanged += UpdatePhase;

            UpdateScrap(Constants.STARTING_SCRAP);
            UpdateLives(Constants.CORE_LIVES);
            UpdatePhase(GamePhase.Build);
        }

        private void BuildUI()
        {
            // Top bar
            var topBar = new HBoxContainer();
            topBar.SetAnchorsPreset(Control.LayoutPreset.TopWide);
            topBar.OffsetBottom = 50;
            topBar.AddThemeConstantOverride("separation", 30);

            var topPanel = new PanelContainer();
            topPanel.SetAnchorsPreset(Control.LayoutPreset.TopWide);
            topPanel.OffsetBottom = 50;
            var topStyle = new StyleBoxFlat();
            topStyle.BgColor = new Color(0, 0, 0, 0.6f);
            topPanel.AddThemeStyleboxOverride("panel", topStyle);
            AddChild(topPanel);

            topPanel.AddChild(topBar);

            _scrapLabel = new Label();
            _scrapLabel.Text = "Scrap: 100";
            _scrapLabel.AddThemeFontSizeOverride("font_size", 20);
            topBar.AddChild(_scrapLabel);

            _livesLabel = new Label();
            _livesLabel.Text = "Lives: 20";
            _livesLabel.AddThemeFontSizeOverride("font_size", 20);
            topBar.AddChild(_livesLabel);

            _waveLabel = new Label();
            _waveLabel.Text = "Wave: 0 / 10";
            _waveLabel.AddThemeFontSizeOverride("font_size", 20);
            topBar.AddChild(_waveLabel);

            _phaseLabel = new Label();
            _phaseLabel.Text = "BUILD PHASE";
            _phaseLabel.AddThemeFontSizeOverride("font_size", 20);
            _phaseLabel.AddThemeColorOverride("font_color", new Color(0.2f, 1f, 0.4f));
            topBar.AddChild(_phaseLabel);

            // Spacer
            var spacer = new Control();
            spacer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            topBar.AddChild(spacer);

            _speedBtn = new Button();
            _speedBtn.Text = "Speed: 1x";
            _speedBtn.Pressed += OnSpeedPressed;
            topBar.AddChild(_speedBtn);

            // Bottom tower bar
            var bottomPanel = new PanelContainer();
            bottomPanel.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
            bottomPanel.OffsetTop = -70;
            var botStyle = new StyleBoxFlat();
            botStyle.BgColor = new Color(0, 0, 0, 0.6f);
            bottomPanel.AddThemeStyleboxOverride("panel", botStyle);
            AddChild(bottomPanel);

            var bottomContainer = new HBoxContainer();
            bottomContainer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            bottomContainer.AddThemeConstantOverride("separation", 10);
            bottomPanel.AddChild(bottomContainer);

            // Start wave button
            _startWaveBtn = new Button();
            _startWaveBtn.Text = "Start Wave [Space]";
            _startWaveBtn.CustomMinimumSize = new Vector2(150, 50);
            _startWaveBtn.Pressed += OnStartWavePressed;
            bottomContainer.AddChild(_startWaveBtn);

            // Separator
            var sep = new VSeparator();
            bottomContainer.AddChild(sep);

            // Tower buttons
            _towerBar = bottomContainer;
            foreach (var towerData in TowerRegistry.GetAll())
            {
                var btn = new Button();
                btn.Text = $"{towerData.Name}\n{towerData.ScrapCost}s";
                btn.CustomMinimumSize = new Vector2(100, 50);
                btn.TooltipText = towerData.Description;
                var type = towerData.Type; // Capture for lambda
                btn.Pressed += () => OnTowerSelected(type);
                _towerBar.AddChild(btn);
            }
        }

        private void OnTowerSelected(TowerType type)
        {
            if (ServiceLocator.TryGet<TowerPlacer>(out var placer))
                placer.StartPlacement(type);
        }

        private void OnStartWavePressed()
        {
            if (ServiceLocator.TryGet<WaveManager>(out var waveMgr) && !waveMgr.IsWaveActive)
                waveMgr.StartNextWave();
        }

        private void OnSpeedPressed()
        {
            GameManager.Instance?.ToggleSpeed();
            float speed = GameManager.Instance?.GameSpeed ?? 1f;
            _speedBtn.Text = $"Speed: {speed}x";
        }

        private void UpdateScrap(int amount)
        {
            _scrapLabel.Text = $"Scrap: {amount}";
        }

        private void UpdateWave(int wave)
        {
            var waveMgr = ServiceLocator.TryGet<WaveManager>(out var wm) ? wm : null;
            int total = waveMgr?.TotalWaves ?? 10;
            _waveLabel.Text = $"Wave: {wave} / {total}";
        }

        private void UpdateLives(int lives)
        {
            _livesLabel.Text = $"Lives: {lives}";
            if (lives <= 5)
                _livesLabel.AddThemeColorOverride("font_color", new Color(1f, 0.3f, 0.2f));
        }

        private void UpdatePhase(GamePhase phase)
        {
            _phaseLabel.Text = phase switch
            {
                GamePhase.Build => "BUILD PHASE",
                GamePhase.Wave => "WAVE IN PROGRESS",
                GamePhase.WaveComplete => "WAVE CLEAR!",
                GamePhase.Victory => "VICTORY!",
                GamePhase.Defeat => "DEFEATED",
                _ => phase.ToString().ToUpper()
            };

            _phaseLabel.AddThemeColorOverride("font_color", phase switch
            {
                GamePhase.Build => new Color(0.2f, 1f, 0.4f),
                GamePhase.Wave => new Color(1f, 0.6f, 0.2f),
                GamePhase.Victory => new Color(1f, 0.85f, 0.2f),
                GamePhase.Defeat => new Color(1f, 0.2f, 0.2f),
                _ => Colors.White
            });

            _startWaveBtn.Disabled = phase != GamePhase.Build;
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event.IsActionPressed("start_wave"))
                OnStartWavePressed();
        }
    }
}
