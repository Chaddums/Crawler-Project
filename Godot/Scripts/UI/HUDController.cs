using System.Collections.Generic;
using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Controls the in-game HUD. Code-builds health, mana, XP bars and buff indicators.
    /// Spawns inventory, passive tree, pause menu, character sheet, and minimap overlays.
    /// Attached to the HUD CanvasLayer node.
    /// </summary>
    public partial class HUDController : CanvasLayer
    {
        // Bar references
        private ProgressBar _healthBar;
        private Label _healthText;
        private StyleBoxFlat _healthFill;
        private float _healthTargetFill = 1f;

        private ProgressBar _manaBar;
        private Label _manaText;
        private float _manaTargetFill = 1f;

        private ProgressBar _xpBar;
        private Label _xpLevelLabel;
        private Label _xpText;
        private float _xpTargetFill;

        private HBoxContainer _buffContainer;
        private float _buffRefreshTimer;
        private const float BUFF_REFRESH_INTERVAL = 0.5f;

        private bool _bound;
        private PlayerController _player;
        private InventoryUI _inventoryUI;
        private PassiveTreeUI _passiveTreeUI;
        private PauseMenuUI _pauseMenuUI;
        private CharacterSheetUI _characterSheetUI;
        private MinimapUI _minimap;
        private Label _floorAreaLabel;
        private BossHealthBarUI _bossHealthBar;

        // Color thresholds
        private static readonly Color HealthHigh = new(0.2f, 0.8f, 0.2f);
        private static readonly Color HealthMid = new(0.9f, 0.7f, 0.1f);
        private static readonly Color HealthLow = new(0.8f, 0.15f, 0.15f);
        private static readonly Color ManaColor = new(0.2f, 0.4f, 0.9f);
        private static readonly Color XpFillColor = new(0.6f, 0.3f, 0.8f);
        private static readonly Color XpBgColor = new(0.15f, 0.1f, 0.2f);
        private const float FILL_LERP_SPEED = 8f;

        public override void _Ready()
        {
            BuildHealthBar();
            BuildManaBar();
            BuildXPBar();
            BuildBuffStrip();

            // Spawn inventory overlay
            _inventoryUI = new InventoryUI();
            _inventoryUI.Name = "InventoryUI";
            GetTree().Root.CallDeferred("add_child", _inventoryUI);

            // Spawn passive tree overlay
            _passiveTreeUI = new PassiveTreeUI();
            _passiveTreeUI.Name = "PassiveTreeUI";
            GetTree().Root.CallDeferred("add_child", _passiveTreeUI);

            // Spawn pause menu overlay
            _pauseMenuUI = new PauseMenuUI();
            _pauseMenuUI.Name = "PauseMenuUI";
            GetTree().Root.CallDeferred("add_child", _pauseMenuUI);

            // Spawn character sheet overlay
            _characterSheetUI = new CharacterSheetUI();
            _characterSheetUI.Name = "CharacterSheetUI";
            GetTree().Root.CallDeferred("add_child", _characterSheetUI);

            // Minimap (top-right, below floor/area label)
            _minimap = new MinimapUI();
            _minimap.Name = "Minimap";
            _minimap.Position = new Vector2(1700, 60);
            _minimap.Size = new Vector2(200, 200);
            AddChild(_minimap);

            // Floor/Area label (top-right)
            _floorAreaLabel = new Label();
            _floorAreaLabel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
            _floorAreaLabel.GrowHorizontal = Control.GrowDirection.Begin;
            _floorAreaLabel.Position = new Vector2(1600, 20);
            _floorAreaLabel.Size = new Vector2(300, 30);
            _floorAreaLabel.HorizontalAlignment = HorizontalAlignment.Right;
            _floorAreaLabel.AddThemeFontSizeOverride("font_size", 18);
            _floorAreaLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.65f, 0.5f));
            AddChild(_floorAreaLabel);
            UpdateFloorAreaLabel();

            GameEvents.OnBossSpawned += OnBossSpawned;
            GameEvents.OnBossDefeated += OnBossDefeated;
        }

        #region Bar Building

        private void BuildHealthBar()
        {
            // Container at Y=20
            var container = new Control();
            container.Position = new Vector2(20, 20);
            container.Size = new Vector2(300, 34);
            AddChild(container);

            // Background
            var bg = new ProgressBar();
            bg.Position = Vector2.Zero;
            bg.Size = new Vector2(300, 26);
            bg.MinValue = 0;
            bg.MaxValue = 100;
            bg.Value = 100;
            bg.ShowPercentage = false;

            var bgStyle = new StyleBoxFlat();
            bgStyle.BgColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            bgStyle.CornerRadiusBottomLeft = 3;
            bgStyle.CornerRadiusBottomRight = 3;
            bgStyle.CornerRadiusTopLeft = 3;
            bgStyle.CornerRadiusTopRight = 3;
            bg.AddThemeStyleboxOverride("background", bgStyle);

            _healthFill = new StyleBoxFlat();
            _healthFill.BgColor = HealthHigh;
            _healthFill.CornerRadiusBottomLeft = 3;
            _healthFill.CornerRadiusBottomRight = 3;
            _healthFill.CornerRadiusTopLeft = 3;
            _healthFill.CornerRadiusTopRight = 3;
            bg.AddThemeStyleboxOverride("fill", _healthFill);

            container.AddChild(bg);
            _healthBar = bg;

            // HP text centered on bar
            _healthText = new Label();
            _healthText.Position = new Vector2(0, 2);
            _healthText.Size = new Vector2(300, 26);
            _healthText.HorizontalAlignment = HorizontalAlignment.Center;
            _healthText.AddThemeFontSizeOverride("font_size", 14);
            _healthText.AddThemeColorOverride("font_color", Colors.White);
            _healthText.Text = "100 / 100";
            container.AddChild(_healthText);
        }

        private void BuildManaBar()
        {
            var container = new Control();
            container.Position = new Vector2(20, 52);
            container.Size = new Vector2(300, 30);
            AddChild(container);

            var bg = new ProgressBar();
            bg.Position = Vector2.Zero;
            bg.Size = new Vector2(300, 22);
            bg.MinValue = 0;
            bg.MaxValue = 100;
            bg.Value = 100;
            bg.ShowPercentage = false;

            var bgStyle = new StyleBoxFlat();
            bgStyle.BgColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            bgStyle.CornerRadiusBottomLeft = 3;
            bgStyle.CornerRadiusBottomRight = 3;
            bgStyle.CornerRadiusTopLeft = 3;
            bgStyle.CornerRadiusTopRight = 3;
            bg.AddThemeStyleboxOverride("background", bgStyle);

            var fill = new StyleBoxFlat();
            fill.BgColor = ManaColor;
            fill.CornerRadiusBottomLeft = 3;
            fill.CornerRadiusBottomRight = 3;
            fill.CornerRadiusTopLeft = 3;
            fill.CornerRadiusTopRight = 3;
            bg.AddThemeStyleboxOverride("fill", fill);

            container.AddChild(bg);
            _manaBar = bg;

            // MP text centered
            _manaText = new Label();
            _manaText.Position = new Vector2(0, 1);
            _manaText.Size = new Vector2(300, 22);
            _manaText.HorizontalAlignment = HorizontalAlignment.Center;
            _manaText.AddThemeFontSizeOverride("font_size", 12);
            _manaText.AddThemeColorOverride("font_color", Colors.White);
            _manaText.Text = "50 / 50";
            container.AddChild(_manaText);
        }

        private void BuildXPBar()
        {
            var container = new Control();
            container.Position = new Vector2(20, 80);
            container.Size = new Vector2(300, 28);
            AddChild(container);

            // Level label left of bar
            _xpLevelLabel = new Label();
            _xpLevelLabel.Position = Vector2.Zero;
            _xpLevelLabel.Size = new Vector2(50, 18);
            _xpLevelLabel.AddThemeFontSizeOverride("font_size", 13);
            _xpLevelLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.3f));
            _xpLevelLabel.Text = "Lv.1";
            container.AddChild(_xpLevelLabel);

            // Thin XP bar
            var bg = new ProgressBar();
            bg.Position = new Vector2(50, 2);
            bg.Size = new Vector2(180, 14);
            bg.MinValue = 0;
            bg.MaxValue = 100;
            bg.Value = 0;
            bg.ShowPercentage = false;

            var bgStyle = new StyleBoxFlat();
            bgStyle.BgColor = XpBgColor;
            bgStyle.CornerRadiusBottomLeft = 2;
            bgStyle.CornerRadiusBottomRight = 2;
            bgStyle.CornerRadiusTopLeft = 2;
            bgStyle.CornerRadiusTopRight = 2;
            bg.AddThemeStyleboxOverride("background", bgStyle);

            var fill = new StyleBoxFlat();
            fill.BgColor = XpFillColor;
            fill.CornerRadiusBottomLeft = 2;
            fill.CornerRadiusBottomRight = 2;
            fill.CornerRadiusTopLeft = 2;
            fill.CornerRadiusTopRight = 2;
            bg.AddThemeStyleboxOverride("fill", fill);

            container.AddChild(bg);
            _xpBar = bg;

            // XP text right of bar
            _xpText = new Label();
            _xpText.Position = new Vector2(235, 0);
            _xpText.Size = new Vector2(80, 18);
            _xpText.AddThemeFontSizeOverride("font_size", 11);
            _xpText.AddThemeColorOverride("font_color", new Color(0.7f, 0.6f, 0.8f));
            _xpText.Text = "0/100";
            container.AddChild(_xpText);
        }

        private void BuildBuffStrip()
        {
            _buffContainer = new HBoxContainer();
            _buffContainer.Position = new Vector2(20, 102);
            _buffContainer.AddThemeConstantOverride("separation", 4);
            AddChild(_buffContainer);
        }

        #endregion

        public void SetMinimapData(IReadOnlyDictionary<Vector2I, RoomType> roomGrid)
        {
            _minimap?.SetRoomGrid(roomGrid);
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;

            if (!_bound)
            {
                if (ServiceLocator.TryGet<PlayerController>(out var player))
                {
                    _player = player;
                    BindPlayer();
                    _bound = true;
                    GD.Print("[HUDController] HUD bars bound to player");
                }
                return;
            }

            // Smooth fill animation for health bar
            if (_healthBar != null)
            {
                _healthBar.Value = Mathf.Lerp((float)_healthBar.Value, _healthTargetFill * 100f, dt * FILL_LERP_SPEED);
            }

            // Smooth fill for mana bar
            if (_manaBar != null)
            {
                _manaBar.Value = Mathf.Lerp((float)_manaBar.Value, _manaTargetFill * 100f, dt * FILL_LERP_SPEED);
            }

            // Smooth fill for XP bar
            if (_xpBar != null)
            {
                _xpBar.Value = Mathf.Lerp((float)_xpBar.Value, _xpTargetFill * 100f, dt * FILL_LERP_SPEED);
            }

            // Buff refresh timer
            _buffRefreshTimer -= dt;
            if (_buffRefreshTimer <= 0f)
            {
                _buffRefreshTimer = BUFF_REFRESH_INTERVAL;
                RefreshBuffStrip();
            }
        }

        private void BindPlayer()
        {
            // Health
            if (_player.Health != null)
            {
                _player.Health.OnHealthChanged += OnHealthChanged;
                OnHealthChanged(_player.Health.CurrentHealth, _player.Health.MaxHealth);
            }

            // Mana
            if (_player.Stats != null)
            {
                _player.Stats.OnManaChanged += OnManaChanged;
                OnManaChanged(_player.Stats.CurrentMana, _player.Stats.MaxMana);

                _player.Stats.OnLevelUp += OnLevelUp;
                OnLevelUp(_player.Stats.Level);
            }

            // XP — subscribe to event bus for updates
            GameEvents.OnExperienceGained += OnExperienceGained;
            RefreshXP();
        }

        #region Event Handlers

        private void OnHealthChanged(float current, float max)
        {
            if (max <= 0f) return;
            float pct = Mathf.Clamp(current / max, 0f, 1f);
            _healthTargetFill = pct;

            _healthText.Text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";

            // Color shift
            if (_healthFill != null)
            {
                if (pct <= 0.3f)
                    _healthFill.BgColor = HealthLow;
                else if (pct <= 0.6f)
                    _healthFill.BgColor = HealthMid;
                else
                    _healthFill.BgColor = HealthHigh;
            }
        }

        private void OnManaChanged(float current, float max)
        {
            if (max <= 0f) return;
            _manaTargetFill = Mathf.Clamp(current / max, 0f, 1f);
            _manaText.Text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }

        private void OnLevelUp(int newLevel)
        {
            _xpLevelLabel.Text = $"Lv.{newLevel}";
            RefreshXP();
        }

        private void OnExperienceGained(int _amount)
        {
            RefreshXP();
        }

        private void RefreshXP()
        {
            if (_player?.Stats == null) return;
            int xp = _player.Stats.Experience;
            int next = _player.Stats.ExperienceToNextLevel;
            _xpTargetFill = next > 0 ? Mathf.Clamp((float)xp / next, 0f, 1f) : 0f;
            _xpText.Text = $"{xp}/{next}";
        }

        #endregion

        #region Buff Strip

        private void RefreshBuffStrip()
        {
            if (_player == null || _buffContainer == null) return;

            // Clear old icons
            foreach (var child in _buffContainer.GetChildren())
            {
                if (child is Node n) n.QueueFree();
            }

            var sem = _player.GetNodeOrNull<StatusEffectManager>("StatusEffectManager");
            if (sem == null) return;

            var effects = sem.ActiveEffects;
            for (int i = 0; i < effects.Count && i < 8; i++)
            {
                var effect = effects[i];
                _buffContainer.AddChild(BuildBuffIcon(effect));
            }
        }

        private static PanelContainer BuildBuffIcon(StatusEffect effect)
        {
            bool isDebuff = effect.Data.IsDebuff;
            var borderColor = isDebuff ? new Color(0.8f, 0.2f, 0.2f) : new Color(0.2f, 0.8f, 0.3f);

            var panel = new PanelContainer();
            panel.CustomMinimumSize = new Vector2(32, 32);

            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.08f, 0.08f, 0.12f, 0.9f);
            style.BorderColor = borderColor;
            style.BorderWidthBottom = 2;
            style.BorderWidthTop = 2;
            style.BorderWidthLeft = 2;
            style.BorderWidthRight = 2;
            style.CornerRadiusBottomLeft = 3;
            style.CornerRadiusBottomRight = 3;
            style.CornerRadiusTopLeft = 3;
            style.CornerRadiusTopRight = 3;
            panel.AddThemeStyleboxOverride("panel", style);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 0);
            panel.AddChild(vbox);

            // Effect initial
            var initial = new Label();
            string name = effect.Data.EffectName;
            initial.Text = name.Length > 0 ? name[..1].ToUpper() : "?";
            initial.HorizontalAlignment = HorizontalAlignment.Center;
            initial.AddThemeFontSizeOverride("font_size", 12);
            initial.AddThemeColorOverride("font_color", borderColor);
            vbox.AddChild(initial);

            // Duration
            var dur = new Label();
            int secs = Mathf.CeilToInt(effect.RemainingDuration);
            dur.Text = $"{secs}s";
            dur.HorizontalAlignment = HorizontalAlignment.Center;
            dur.AddThemeFontSizeOverride("font_size", 9);
            dur.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
            vbox.AddChild(dur);

            return panel;
        }

        #endregion

        private void UpdateFloorAreaLabel()
        {
            int floor = GameManager.Instance?.CurrentFloor ?? 1;
            int area = GameManager.Instance?.CurrentArea ?? 1;
            _floorAreaLabel.Text = $"Floor {floor} - Area {area}";
        }

        private void OnBossSpawned(Node bossNode)
        {
            if (bossNode is not EnemyController boss) return;
            if (!boss.Data.IsBoss) return;

            // Only create health bar on initial spawn (not phase transitions)
            if (_bossHealthBar != null && GodotObject.IsInstanceValid(_bossHealthBar))
            {
                _bossHealthBar.SetPhase(boss.BossAI?.CurrentPhase ?? 1);
                return;
            }

            _bossHealthBar = new BossHealthBarUI();
            _bossHealthBar.Name = "BossHealthBar";
            GetTree().Root.AddChild(_bossHealthBar);
            _bossHealthBar.BindToBoss(boss.Health, boss.Data.EnemyName);
        }

        private void OnBossDefeated(Node bossNode)
        {
            _bossHealthBar?.Dismiss();
            _bossHealthBar = null;
        }

        public override void _ExitTree()
        {
            GameEvents.OnBossSpawned -= OnBossSpawned;
            GameEvents.OnBossDefeated -= OnBossDefeated;
            GameEvents.OnExperienceGained -= OnExperienceGained;

            if (_player != null)
            {
                if (_player.Health != null)
                    _player.Health.OnHealthChanged -= OnHealthChanged;
                if (_player.Stats != null)
                {
                    _player.Stats.OnManaChanged -= OnManaChanged;
                    _player.Stats.OnLevelUp -= OnLevelUp;
                }
            }
        }
    }
}
