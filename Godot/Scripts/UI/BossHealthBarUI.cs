using Godot;

namespace JunkbotArena
{
	/// <summary>
	/// Wide health bar across the top-center of the screen for boss encounters.
	/// Programmatically created, slides in on boss spawn and out on defeat.
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

		private const float BAR_WIDTH = 600f;
		private const float BAR_HEIGHT = 24f;
		private const float LERP_SPEED = 6f;

		public override void _Ready()
		{
			Layer = 10;
			BuildUI();
			SlideIn();
		}

		private void BuildUI()
		{
			// Root container
			var root = new Control();
			root.SetAnchorsPreset(Control.LayoutPreset.TopWide);
			root.Size = new Vector2(1920, 80);
			AddChild(root);

			// Panel background
			_panel = new PanelContainer();
			_panel.Position = new Vector2((1920 - BAR_WIDTH - 40) / 2f, -80); // Start off-screen
			_panel.Size = new Vector2(BAR_WIDTH + 40, 75);

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
			_nameLabel.AddThemeFontSizeOverride("font_size", 16);
			_nameLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.4f));
			vbox.AddChild(_nameLabel);

			// Health bar
			_progressBar = new ProgressBar();
			_progressBar.CustomMinimumSize = new Vector2(BAR_WIDTH, BAR_HEIGHT);
			_progressBar.MinValue = 0;
			_progressBar.MaxValue = 100;
			_progressBar.Value = 100;
			_progressBar.ShowPercentage = false;

			// Fill stylebox
			_fillStyleBox = new StyleBoxFlat();
			_fillStyleBox.BgColor = new Color(0.8f, 0.15f, 0.1f);
			_fillStyleBox.CornerRadiusBottomLeft = 3;
			_fillStyleBox.CornerRadiusBottomRight = 3;
			_fillStyleBox.CornerRadiusTopLeft = 3;
			_fillStyleBox.CornerRadiusTopRight = 3;
			_progressBar.AddThemeStyleboxOverride("fill", _fillStyleBox);

			// Background stylebox
			var bgStyle = new StyleBoxFlat();
			bgStyle.BgColor = new Color(0.15f, 0.1f, 0.1f);
			bgStyle.CornerRadiusBottomLeft = 3;
			bgStyle.CornerRadiusBottomRight = 3;
			bgStyle.CornerRadiusTopLeft = 3;
			bgStyle.CornerRadiusTopRight = 3;
			_progressBar.AddThemeStyleboxOverride("background", bgStyle);

			vbox.AddChild(_progressBar);

			// Phase dots
			_phaseDotsContainer = new HBoxContainer();
			_phaseDotsContainer.Alignment = BoxContainer.AlignmentMode.Center;
			_phaseDotsContainer.AddThemeConstantOverride("separation", 8);
			vbox.AddChild(_phaseDotsContainer);

			for (int i = 0; i < 3; i++)
			{
				var dot = new Panel();
				dot.CustomMinimumSize = new Vector2(10, 10);
				var dotStyle = new StyleBoxFlat();
				dotStyle.BgColor = i == 0 ? new Color(1f, 0.4f, 0.2f) : new Color(0.3f, 0.3f, 0.3f);
				dotStyle.CornerRadiusBottomLeft = 5;
				dotStyle.CornerRadiusBottomRight = 5;
				dotStyle.CornerRadiusTopLeft = 5;
				dotStyle.CornerRadiusTopRight = 5;
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
					newStyle.BgColor = i < phase ? new Color(1f, 0.4f, 0.2f) : new Color(0.3f, 0.3f, 0.3f);
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
				_fillStyleBox.BgColor = new Color(0.8f - darkness, 0.15f, 0.1f);
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
