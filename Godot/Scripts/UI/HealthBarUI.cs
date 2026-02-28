using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// HUD health bar using a ProgressBar control. Binds to a HealthComponent
    /// and shows animated fill + color transitions.
    /// </summary>
    public partial class HealthBarUI : Control
    {
        [Export] private ProgressBar _progressBar;
        [Export] private Label _healthText;

        [ExportGroup("Colors")]
        [Export] private Color _highHealthColor = new(0.2f, 0.8f, 0.2f);
        [Export] private Color _midHealthColor = new(0.9f, 0.7f, 0.1f);
        [Export] private Color _lowHealthColor = new(0.8f, 0.15f, 0.15f);
        [Export] private float _lowHealthThreshold = 0.3f;
        [Export] private float _midHealthThreshold = 0.6f;

        [ExportGroup("Animation")]
        [Export] private float _fillLerpSpeed = 8f;

        private HealthComponent _boundHealth;
        private float _targetFill;
        private StyleBoxFlat _fillStyleBox;

        public override void _Ready()
        {
            // Get or create the fill stylebox so we can change its color
            if (_progressBar != null)
            {
                var existing = _progressBar.GetThemeStylebox("fill");
                if (existing is StyleBoxFlat flat)
                {
                    _fillStyleBox = (StyleBoxFlat)flat.Duplicate();
                }
                else
                {
                    _fillStyleBox = new StyleBoxFlat();
                }
                _fillStyleBox.BgColor = _highHealthColor;
                _progressBar.AddThemeStyleboxOverride("fill", _fillStyleBox);
                _progressBar.MinValue = 0;
                _progressBar.MaxValue = 100;
                _progressBar.Value = 100;
            }
        }

        public override void _Process(double delta)
        {
            if (_progressBar == null) return;

            // Smooth fill animation
            _progressBar.Value = Mathf.Lerp((float)_progressBar.Value, _targetFill * 100f, (float)delta * _fillLerpSpeed);
        }

        public void Bind(HealthComponent health)
        {
            if (_boundHealth != null)
                _boundHealth.OnHealthChanged -= HandleHealthChanged;

            _boundHealth = health;

            if (_boundHealth != null)
            {
                _boundHealth.OnHealthChanged += HandleHealthChanged;
                SetHealth(_boundHealth.CurrentHealth, _boundHealth.MaxHealth);
            }
        }

        public void SetHealth(float current, float max)
        {
            if (max <= 0f) return;

            float percent = Mathf.Clamp(current / max, 0f, 1f);
            _targetFill = percent;

            if (_healthText != null)
                _healthText.Text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";

            UpdateFillColor(percent);
        }

        private void HandleHealthChanged(float current, float max)
        {
            SetHealth(current, max);
        }

        private void UpdateFillColor(float percent)
        {
            if (_fillStyleBox == null) return;

            if (percent <= _lowHealthThreshold)
                _fillStyleBox.BgColor = _lowHealthColor;
            else if (percent <= _midHealthThreshold)
                _fillStyleBox.BgColor = _midHealthColor;
            else
                _fillStyleBox.BgColor = _highHealthColor;
        }

        public override void _ExitTree()
        {
            if (_boundHealth != null)
                _boundHealth.OnHealthChanged -= HandleHealthChanged;
        }
    }
}
