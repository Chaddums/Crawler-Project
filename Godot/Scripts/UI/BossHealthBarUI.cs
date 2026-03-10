using Godot;

namespace JunkbotArena
{
	/// <summary>
	/// Wide health bar across the top-center of the screen for boss encounters.
	/// Programmatically created, slides in on boss spawn and out on defeat.
	/// Reads layout/color overrides from UIConfigLoader (edited via the UI Designer tab).
	/// </summary>
	public partial class BossHealthBarUI : CanvasLayer
	{
		private PanelContainer _panel;
		private ProgressBar _progressBar;
		private Label _nameLabel;
		private HBoxContainer _phaseDotsContainer;
		private Control[] _phaseDots = new Control[3];
		private StyleBoxFlat _fillStyleBox;

		private HealthComponent _boundHealth;
		private float _targetFill = 1f;
		private int _currentPhase = 1;
		private bool _dismissed;

		private float _barHeight;
		private Color _fillColor;
		private Color _phaseActiveColor;
		private Color _phaseInactiveColor;

		private const float LERP_SPEED = 6f;

		public override void _Ready()
		{
			Layer = 10;
			BuildUI();
			SlideIn();
		}

		private void BuildUI()
		{
			float sideMargin = UIConfigLoader.GetFloat("BossHealthBar", "Bar", "SideMargin", 200f);
			_barHeight = UIConfigLoader.GetFloat("BossHealthBar", "Bar", "Height", 24f);
			float topMargin = UIConfigLoader.GetFloat("BossHealthBar", "Bar", "TopMargin", 40f);
			_fillColor = UIConfigLoader.GetColor("BossHealthBar", "Bar", "FillColor", new Color(0.8f, 0.15f, 0.15f));
			var bgColor = UIConfigLoader.GetColor("BossHealthBar", "Bar", "BgColor", new Color(0.1f, 0.1f, 0.12f));
			var borderColor = UIConfigLoader.GetColor("BossHealthBar", "Bar", "BorderColor", new Color(0.6f, 0.2f, 0.2f));
			int nameFontSize = UIConfigLoader.GetInt("BossHealthBar", "NameLabel", "FontSize", 16);
			var nameColor = UIConfigLoader.GetColor("BossHealthBar", "NameLabel", "TextColor", new Color(0.9f, 0.3f, 0.2f));
			_phaseActiveColor = UIConfigLoader.GetColor("BossHealthBar", "PhaseIndicator", "ActiveColor", new Color(1f, 0.4f, 0.1f));
			_phaseInactiveColor = UIConfigLoader.GetColor("BossHealthBar", "PhaseIndicator", "InactiveColor", new Color(0.3f, 0.3f, 0.35f));
			float dotSize = UIConfigLoader.GetFloat("BossHealthBar", "PhaseIndicator", "DotSize", 8f);

			float barWidth = 1920f - sideMargin * 2f;

			// Root container
			var root = new Control();
			root.SetAnchorsPreset(Control.LayoutPreset.TopWide);
			root.Size = new Vector2(1920, 80);
			AddChild(root);

			// Panel background
			_panel = new PanelContainer();
			_panel.Position = new Vector2((1920 - barWidth - 40) / 2f, -80); // Start off-screen
			_panel.Size = new Vector2(barWidth + 40, 75);

			var panelStyle = new StyleBoxFlat();
			panelStyle.BgColor = new Color(0, 0, 0, 0.7f);
			panelStyle.CornerRadiusBottomLeft = 8;
			panelStyle.CornerRadiusBottomRight = 8;
			panelStyle.ContentMarginLeft = 20;
			panelStyle.ContentMarginRight = 20;
			panelStyle.ContentMarginTop = 8;
			panelStyle.ContentMarginBottom = 8;
			_panel.AddThemeStyleboxOverride("panel", panelStyle);
			root.AddChild(_panel);

			var vbox = new VBoxContainer();
			vbox.AddThemeConstantOverride("separation", 4);
			_panel.AddChild(vbox);

			// Boss name label
			_nameLabel = new Label();
			_nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
			_nameLabel.AddThemeFontSizeOverride("font_size", nameFontSize);
			_nameLabel.AddThemeColorOverride("font_color", nameColor);
			vbox.AddChild(_nameLabel);

			// Health bar
			_progressBar = new ProgressBar();
			_progressBar.CustomMinimumSize = new Vector2(barWidth, _barHeight);
			_progressBar.MinValue = 0;
			_progressBar.MaxValue = 100;
			_progressBar.Value = 100;
			_progressBar.ShowPercentage = false;

			// Fill stylebox
			_fillStyleBox = new StyleBoxFlat();
			_fillStyleBox.BgColor = _fillColor;
			_fillStyleBox.CornerRadiusBottomLeft = 3;
			_fillStyleBox.CornerRadiusBottomRight = 3;
			_fillStyleBox.CornerRadiusTopLeft = 3;
			_fillStyleBox.CornerRadiusTopRight = 3;
			_progressBar.AddThemeStyleboxOverride("fill", _fillStyleBox);

			// Background stylebox
			var barBgStyle = new StyleBoxFlat();
			barBgStyle.BgColor = bgColor;
			barBgStyle.CornerRadiusBottomLeft = 3;
			barBgStyle.CornerRadiusBottomRight = 3;
			barBgStyle.CornerRadiusTopLeft = 3;
			barBgStyle.CornerRadiusTopRight = 3;
			_progressBar.AddThemeStyleboxOverride("background", barBgStyle);

			vbox.AddChild(_progressBar);

			// Phase dots
			_phaseDotsContainer = new HBoxContainer();
			_phaseDotsContainer.Alignment = BoxContainer.AlignmentMode.Center;
			_phaseDotsContainer.AddThemeConstantOverride("separation", 8);
			vbox.AddChild(_phaseDotsContainer);

			for (int i = 0; i < 3; i++)
			{
				var dot = new Panel();
				dot.CustomMinimumSize = new Vector2(dotSize, dotSize);
				var dotStyle = new StyleBoxFlat();
				dotStyle.BgColor = i == 0 ? _phaseActiveColor : _phaseInactiveColor;
				float cornerRadius = dotSize / 2f;
				dotStyle.CornerRadiusBottomLeft = (int)cornerRadius;
				dotStyle.CornerRadiusBottomRight = (int)cornerRadius;
				dotStyle.CornerRadiusTopLeft = (int)cornerRadius;
				dotStyle.CornerRadiusTopRight = (int)cornerRadius;
				dot.AddThemeStyleboxOverride("panel", dotStyle);
				_phaseDotsContainer.AddChild(dot);
				_phaseDots[i] = dot;
			}
		}

		public void BindToBoss(HealthComponent health, string bossName)
		{
			if (_boundHealth != null)
				_boundHealth.OnHealthChanged -= HandleHealthChanged;

			_boundHealth = health;
			_nameLabel.Text = bossName;

			if (_boundHealth != null)
			{
				_boundHealth.OnHealthChanged += HandleHealthChanged;
				SetHealth(_boundHealth.CurrentHealth, _boundHealth.MaxHealth);
			}
		}

		public void SetPhase(int phase)
		{
			_currentPhase = phase;
			for (int i = 0; i < 3; i++)
			{
				var dot = _phaseDots[i];
				var style = dot.GetThemeStylebox("panel") as StyleBoxFlat;
				if (style != null)
				{
					var newStyle = (StyleBoxFlat)style.Duplicate();
					newStyle.BgColor = i < phase ? _phaseActiveColor : _phaseInactiveColor;
					dot.AddThemeStyleboxOverride("panel", newStyle);
				}
			}
		}

		public void Dismiss()
		{
			if (_dismissed) return;
			_dismissed = true;

			if (_boundHealth != null)
				_boundHealth.OnHealthChanged -= HandleHealthChanged;

			SlideOut();
		}

		public override void _Process(double delta)
		{
			if (_progressBar == null || _dismissed) return;
			_progressBar.Value = Mathf.Lerp((float)_progressBar.Value, _targetFill * 100f, (float)delta * LERP_SPEED);

			// Darken fill as health drops
			float percent = _targetFill;
			if (_fillStyleBox != null)
			{
				float darkness = (1f - percent) * 0.4f;
				_fillStyleBox.BgColor = new Color(_fillColor.R - darkness, _fillColor.G, _fillColor.B);
			}
		}

		private void SetHealth(float current, float max)
		{
			if (max <= 0) return;
			_targetFill = Mathf.Clamp(current / max, 0f, 1f);
		}

		private void HandleHealthChanged(float current, float max)
		{
			SetHealth(current, max);
		}

		private void SlideIn()
		{
			if (_panel == null) return;
			var tween = CreateTween();
			tween.TweenProperty(_panel, "position:y", 10f, 0.4f)
				.SetTrans(Tween.TransitionType.Back)
				.SetEase(Tween.EaseType.Out);
		}

		private void SlideOut()
		{
			if (_panel == null) return;
			var tween = CreateTween();
			tween.TweenProperty(_panel, "position:y", -80f, 0.3f)
				.SetTrans(Tween.TransitionType.Back)
				.SetEase(Tween.EaseType.In);
			tween.TweenCallback(Callable.From(QueueFree));
		}

		public override void _ExitTree()
		{
			if (_boundHealth != null)
				_boundHealth.OnHealthChanged -= HandleHealthChanged;
		}
	}
}
