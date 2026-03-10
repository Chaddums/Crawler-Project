using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// HUD element showing the sector countdown timer. Top-left position.
    /// Changes color at warning thresholds and pulses when critical.
    /// Reads layout/color overrides from UIConfigLoader (edited via the UI Designer tab).
    /// </summary>
    public partial class LiftTimerUI : CanvasLayer
    {
        private Label _timerLabel;
        private Label _sectorLabel;
        private ColorRect _vignette;
        private PanelContainer _panel;

        private float _pulseTimer;
        private bool _isPulsing;
        private bool _isCritical;
        private bool _showVignette;
        private float _visualTimeOffset;
        private Vector2 _basePosition;

        private Color _normalColor;
        private Color _warningColor;
        private Color _dangerColor;
        private float _warningThreshold;

        public override void _Ready()
        {
            Layer = 30;
            LoadConfig();
            BuildUI();
            ServiceLocator.Register(this);

            GameEvents.OnTimerExpired += OnTimerExpired;
        }

        private void LoadConfig()
        {
            _normalColor = UIConfigLoader.GetColor("HUD", "LiftTimer", "TextColor", new Color(0.9f, 0.85f, 0.7f));
            _warningColor = UIConfigLoader.GetColor("HUD", "LiftTimer", "WarningColor", new Color(1f, 0.3f, 0.2f));
            _warningThreshold = UIConfigLoader.GetFloat("HUD", "LiftTimer", "WarningThreshold", 60f);
            _dangerColor = new Color(1f, 0.2f, 0.2f);
            _basePosition = new Vector2(
                UIConfigLoader.GetFloat("HUD", "LiftTimer", "PositionX", 16f),
                UIConfigLoader.GetFloat("HUD", "LiftTimer", "PositionY", 16f));
        }

        private void BuildUI()
        {
            int fontSize = UIConfigLoader.GetInt("HUD", "LiftTimer", "FontSize", 28);

            // Timer panel — top left
            _panel = new PanelContainer();
            _panel.Position = _basePosition;
            _panel.CustomMinimumSize = new Vector2(140, 60);

            var style = new StyleBoxFlat();
            style.BgColor = new Color(0, 0, 0, 0.5f);
            style.CornerRadiusBottomLeft = 4;
            style.CornerRadiusBottomRight = 4;
            style.CornerRadiusTopLeft = 4;
            style.CornerRadiusTopRight = 4;
            style.ContentMarginLeft = 10;
            style.ContentMarginRight = 10;
            style.ContentMarginTop = 6;
            style.ContentMarginBottom = 6;
            _panel.AddThemeStyleboxOverride("panel", style);
            AddChild(_panel);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 2);
            _panel.AddChild(vbox);

            _timerLabel = new Label();
            _timerLabel.Text = "5:00";
            _timerLabel.AddThemeFontSizeOverride("font_size", fontSize);
            _timerLabel.AddThemeColorOverride("font_color", _normalColor);
            _timerLabel.HorizontalAlignment = HorizontalAlignment.Center;
            vbox.AddChild(_timerLabel);

            _sectorLabel = new Label();
            _sectorLabel.Text = "Sector 1";
            _sectorLabel.AddThemeFontSizeOverride("font_size", 12);
            _sectorLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            _sectorLabel.HorizontalAlignment = HorizontalAlignment.Center;
            vbox.AddChild(_sectorLabel);

            // Screen-edge vignette for critical timer
            _vignette = new ColorRect();
            _vignette.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _vignette.Color = new Color(0.8f, 0f, 0f, 0f);
            _vignette.MouseFilter = Control.MouseFilterEnum.Ignore;
            _vignette.Visible = false;
            AddChild(_vignette);
        }

        public override void _Process(double delta)
        {
            if (!ServiceLocator.TryGet<LiftTimer>(out var timer)) return;

            // Don't show danger colors before the sector timer has been initialized
            if (timer.TimeLimit <= 0f)
            {
                _timerLabel.Text = "--:--";
                _timerLabel.AddThemeColorOverride("font_color", _normalColor);
                _panel.Scale = Vector2.One;
                _vignette.Visible = false;
                return;
            }

            float remaining = timer.TimeRemaining;
            float displayRemaining = Mathf.Max(0f, remaining + _visualTimeOffset);
            int minutes = (int)(displayRemaining / 60f);
            int seconds = (int)(displayRemaining % 60f);

            _timerLabel.Text = $"{minutes}:{seconds:D2}";

            // Sector label
            int sector = GameManager.Instance?.CurrentSector ?? 1;
            _sectorLabel.Text = $"Sector {sector}";

            // Color thresholds
            if (remaining <= 10f)
            {
                _timerLabel.AddThemeColorOverride("font_color", _dangerColor);
                _isPulsing = true;
                _isCritical = true;
                _showVignette = true;
            }
            else if (remaining <= 30f)
            {
                _timerLabel.AddThemeColorOverride("font_color", _dangerColor);
                _isPulsing = true;
                _isCritical = false;
                _showVignette = false;
            }
            else if (remaining <= _warningThreshold)
            {
                _timerLabel.AddThemeColorOverride("font_color", _warningColor);
                _isPulsing = false;
                _isCritical = false;
                _showVignette = false;
            }
            else
            {
                _timerLabel.AddThemeColorOverride("font_color", _normalColor);
                _isPulsing = false;
                _isCritical = false;
                _showVignette = false;
            }

            // Pulse animation
            if (_isPulsing)
            {
                float speed = _isCritical ? 6f : 3f;
                _pulseTimer += (float)delta * speed;
                float pulse = (Mathf.Sin(_pulseTimer) + 1f) * 0.5f;
                float scale = 1f + pulse * 0.08f;
                _panel.Scale = new Vector2(scale, scale);
            }
            else
            {
                _panel.Scale = Vector2.One;
                _pulseTimer = 0f;
            }

            // Vignette
            _vignette.Visible = _showVignette;
            if (_showVignette)
            {
                float alpha = (Mathf.Sin(_pulseTimer * 0.8f) + 1f) * 0.5f * 0.15f;
                _vignette.Color = new Color(0.8f, 0f, 0f, alpha);
            }
        }

        private void OnTimerExpired()
        {
            _timerLabel.Text = "0:00";
            _timerLabel.AddThemeColorOverride("font_color", _dangerColor);
            _isPulsing = false;
            _panel.Scale = Vector2.One;

            // Flash red screen
            _vignette.Visible = true;
            _vignette.Color = new Color(0.8f, 0f, 0f, 0.4f);

            var tween = CreateTween();
            tween.TweenProperty(_vignette, "color:a", 0f, 1.5f);
            tween.TweenCallback(Callable.From(() => _vignette.Visible = false));
        }

        public void SetVisualOffset(float offset)
        {
            _visualTimeOffset = offset;
            _panel.Position = _basePosition + new Vector2(offset, 0);
        }

        public void ClearVisualOffset()
        {
            _visualTimeOffset = 0f;
            _panel.Position = _basePosition;
        }

        public override void _ExitTree()
        {
            GameEvents.OnTimerExpired -= OnTimerExpired;
            ServiceLocator.Unregister<LiftTimerUI>();
        }
    }
}
