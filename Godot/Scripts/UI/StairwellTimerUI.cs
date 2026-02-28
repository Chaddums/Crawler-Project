using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// HUD element showing the floor countdown timer. Top-left position.
    /// Changes color at warning thresholds and pulses when critical.
    /// </summary>
    public partial class StairwellTimerUI : CanvasLayer
    {
        private Label _timerLabel;
        private Label _floorLabel;
        private ColorRect _vignette;
        private PanelContainer _panel;

        private float _pulseTimer;
        private bool _isPulsing;
        private bool _isCritical;
        private bool _showVignette;

        private static readonly Color NormalColor = new(0.9f, 0.9f, 0.9f);
        private static readonly Color WarningColor = new(1f, 0.85f, 0.2f);
        private static readonly Color DangerColor = new(1f, 0.2f, 0.2f);

        public override void _Ready()
        {
            Layer = 30;
            BuildUI();

            GameEvents.OnTimerExpired += OnTimerExpired;
        }

        private void BuildUI()
        {
            // Timer panel — top left
            _panel = new PanelContainer();
            _panel.Position = new Vector2(16, 16);
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
            _timerLabel.AddThemeFontSizeOverride("font_size", 28);
            _timerLabel.AddThemeColorOverride("font_color", NormalColor);
            _timerLabel.HorizontalAlignment = HorizontalAlignment.Center;
            vbox.AddChild(_timerLabel);

            _floorLabel = new Label();
            _floorLabel.Text = "Floor 1";
            _floorLabel.AddThemeFontSizeOverride("font_size", 12);
            _floorLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            _floorLabel.HorizontalAlignment = HorizontalAlignment.Center;
            vbox.AddChild(_floorLabel);

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
            if (!ServiceLocator.TryGet<StairwellTimer>(out var timer)) return;

            float remaining = timer.TimeRemaining;
            int minutes = (int)(remaining / 60f);
            int seconds = (int)(remaining % 60f);

            _timerLabel.Text = $"{minutes}:{seconds:D2}";

            // Floor label
            int floor = GameManager.Instance?.CurrentFloor ?? 1;
            _floorLabel.Text = $"Floor {floor}";

            // Color thresholds
            if (remaining <= 10f)
            {
                _timerLabel.AddThemeColorOverride("font_color", DangerColor);
                _isPulsing = true;
                _isCritical = true;
                _showVignette = true;
            }
            else if (remaining <= 30f)
            {
                _timerLabel.AddThemeColorOverride("font_color", DangerColor);
                _isPulsing = true;
                _isCritical = false;
                _showVignette = false;
            }
            else if (remaining <= 60f)
            {
                _timerLabel.AddThemeColorOverride("font_color", WarningColor);
                _isPulsing = false;
                _isCritical = false;
                _showVignette = false;
            }
            else
            {
                _timerLabel.AddThemeColorOverride("font_color", NormalColor);
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
            _timerLabel.AddThemeColorOverride("font_color", DangerColor);
            _isPulsing = false;
            _panel.Scale = Vector2.One;

            // Flash red screen
            _vignette.Visible = true;
            _vignette.Color = new Color(0.8f, 0f, 0f, 0.4f);

            var tween = CreateTween();
            tween.TweenProperty(_vignette, "color:a", 0f, 1.5f);
            tween.TweenCallback(Callable.From(() => _vignette.Visible = false));
        }

        public override void _ExitTree()
        {
            GameEvents.OnTimerExpired -= OnTimerExpired;
        }
    }
}
