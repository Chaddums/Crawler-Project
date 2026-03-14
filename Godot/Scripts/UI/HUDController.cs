using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Controls the in-game HUD. Code-builds health, mana, XP bars and buff indicators.
    /// Spawns inventory, passive tree, pause menu, character sheet, and minimap overlays.
    /// Attached to the HUD CanvasLayer node.
    /// Reads layout/color overrides from UIConfigLoader (edited via the UI Designer tab).
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
        private Label _sectorAreaLabel;
        private BossHealthBarUI _bossHealthBar;
        private ScrapPopupUI _scrapPopup;
        private LootBoxTrackerUI _lootBoxTracker;
        private Label[] _dashPips;
        private VBoxContainer _healthPotionList;
        private VBoxContainer _manaPotionList;
        private CommentaryToastUI _commentaryToast;

        // Color thresholds (defaults — overridable via UIConfigLoader)
        private Color _healthHigh;
        private Color _healthMid;
        private Color _healthLow;
        private Color _dashActiveColor;
        private Color _dashInactiveColor;
        private const float FILL_LERP_SPEED = 8f;

        public override void _Ready()
        {
            LoadConfigColors();
            BuildHealthBar();
            BuildManaBar();
            BuildXPBar();
            BuildBuffStrip();
            BuildDashIndicator();
            BuildConsumableIndicators();

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

            // Scrap popup notifications
            _scrapPopup = new ScrapPopupUI();
            _scrapPopup.Name = "ScrapPopupUI";
            GetTree().Root.CallDeferred("add_child", _scrapPopup);

            // Loot box tracker (top-left, below the round timer)
            _lootBoxTracker = new LootBoxTrackerUI();
            _lootBoxTracker.Name = "LootBoxTracker";
            _lootBoxTracker.Position = new Vector2(
                UIConfigLoader.GetFloat("HUD", "LootBoxTracker", "PositionX", 20),
                UIConfigLoader.GetFloat("HUD", "LootBoxTracker", "PositionY", 95));
            AddChild(_lootBoxTracker);

            // Minimap (top-right, below floor/area label)
            _minimap = new MinimapUI();
            _minimap.Name = "Minimap";
            _minimap.Position = new Vector2(
                UIConfigLoader.GetFloat("HUD", "Minimap", "PositionX", 1700),
                UIConfigLoader.GetFloat("HUD", "Minimap", "PositionY", 60));
            _minimap.Size = new Vector2(
                UIConfigLoader.GetFloat("HUD", "Minimap", "Width", 200),
                UIConfigLoader.GetFloat("HUD", "Minimap", "Height", 200));
            AddChild(_minimap);

            // Sector/Area label (top-right)
            _sectorAreaLabel = new Label();
            _sectorAreaLabel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
            _sectorAreaLabel.GrowHorizontal = Control.GrowDirection.Begin;
            _sectorAreaLabel.Position = new Vector2(
                UIConfigLoader.GetFloat("HUD", "SectorLabel", "PositionX", 1600),
                UIConfigLoader.GetFloat("HUD", "SectorLabel", "PositionY", 20));
            _sectorAreaLabel.Size = new Vector2(300, 30);
            _sectorAreaLabel.HorizontalAlignment = HorizontalAlignment.Right;
            _sectorAreaLabel.AddThemeFontSizeOverride("font_size",
                UIConfigLoader.GetInt("HUD", "SectorLabel", "FontSize", 18));
            _sectorAreaLabel.AddThemeColorOverride("font_color",
                UIConfigLoader.GetColor("HUD", "SectorLabel", "TextColor", new Color(0.7f, 0.65f, 0.5f)));
            AddChild(_sectorAreaLabel);
            UpdateSectorAreaLabel();

            // Commentary toast (bottom-center, above XP bar)
            _commentaryToast = new CommentaryToastUI();
            _commentaryToast.Name = "CommentaryToast";
            AddChild(_commentaryToast);

            GameEvents.OnBossSpawned += OnBossSpawned;
            GameEvents.OnBossDefeated += OnBossDefeated;
        }

        #region Bar Building

        private void LoadConfigColors()
        {
            _healthHigh = UIConfigLoader.GetColor("HUD", "HealthBar", "FillColorHigh", new Color(0.2f, 0.8f, 0.2f));
            _healthMid = UIConfigLoader.GetColor("HUD", "HealthBar", "FillColorMid", new Color(0.9f, 0.7f, 0.1f));
            _healthLow = UIConfigLoader.GetColor("HUD", "HealthBar", "FillColorLow", new Color(0.8f, 0.15f, 0.15f));
            _dashActiveColor = UIConfigLoader.GetColor("HUD", "DashIndicator", "ActiveColor", new Color(0.3f, 0.8f, 0.9f));
            _dashInactiveColor = UIConfigLoader.GetColor("HUD", "DashIndicator", "InactiveColor", new Color(0.2f, 0.2f, 0.25f));
        }

        private void BuildHealthBar()
        {
            var accent = UIConfigLoader.GetColor("HUD", "HealthBar", "AccentColor", new Color(0.7f, 0.45f, 0.15f));
            var header = UIConfigLoader.Get("HUD", "HealthBar", "HeaderText", "SCRAP");
            float edgeMargin = UIConfigLoader.GetFloat("HUD", "HealthBar", "EdgeMargin", 30f);
            float boxW = UIConfigLoader.GetFloat("HUD", "HealthBar", "Width", 150f);
            float boxH = UIConfigLoader.GetFloat("HUD", "HealthBar", "Height", 220f);
            float bottomMargin = UIConfigLoader.GetFloat("HUD", "HealthBar", "BottomMargin", 28f);
            int borderWidth = UIConfigLoader.GetInt("HUD", "HealthBar", "BorderWidth", 3);
            var bgColor = UIConfigLoader.GetColor("HUD", "HealthBar", "BgColor", new Color(0.04f, 0.04f, 0.06f));
            float bgOpacity = UIConfigLoader.GetFloat("HUD", "HealthBar", "BgOpacity", 0.93f);
            int fontSize = UIConfigLoader.GetInt("HUD", "HealthBar", "FontSize", 11);

            float posX = UIConfigLoader.GetFloat("HUD", "HealthBar", "PositionX", -1);
            float posY = UIConfigLoader.GetFloat("HUD", "HealthBar", "PositionY", -1);
            BuildResourceBox(header, accent, _healthHigh,
                0f, edgeMargin, false, boxW, boxH, bottomMargin, borderWidth,
                bgColor, bgOpacity, fontSize,
                out _healthBar, out _healthText, out _healthFill,
                posX, posY);
        }

        private void BuildManaBar()
        {
            var accent = UIConfigLoader.GetColor("HUD", "BatteryBar", "AccentColor", new Color(0.15f, 0.45f, 0.95f));
            var fillColor = UIConfigLoader.GetColor("HUD", "BatteryBar", "FillColor", new Color(0.2f, 0.4f, 0.9f));
            var header = UIConfigLoader.Get("HUD", "BatteryBar", "HeaderText", "BATTERY");
            float edgeMargin = UIConfigLoader.GetFloat("HUD", "BatteryBar", "EdgeMargin", 30f);
            float boxW = UIConfigLoader.GetFloat("HUD", "BatteryBar", "Width", 150f);
            float boxH = UIConfigLoader.GetFloat("HUD", "BatteryBar", "Height", 220f);
            float bottomMargin = UIConfigLoader.GetFloat("HUD", "BatteryBar", "BottomMargin", 28f);
            int borderWidth = UIConfigLoader.GetInt("HUD", "BatteryBar", "BorderWidth", 3);
            var bgColor = UIConfigLoader.GetColor("HUD", "BatteryBar", "BgColor", new Color(0.04f, 0.04f, 0.06f));
            float bgOpacity = UIConfigLoader.GetFloat("HUD", "BatteryBar", "BgOpacity", 0.93f);
            int fontSize = UIConfigLoader.GetInt("HUD", "BatteryBar", "FontSize", 11);

            float posX = UIConfigLoader.GetFloat("HUD", "BatteryBar", "PositionX", -1);
            float posY = UIConfigLoader.GetFloat("HUD", "BatteryBar", "PositionY", -1);
            BuildResourceBox(header, accent, fillColor,
                1f, edgeMargin, true, boxW, boxH, bottomMargin, borderWidth,
                bgColor, bgOpacity, fontSize,
                out _manaBar, out _manaText, out _,
                posX, posY);
        }

        /// <summary>
        /// Builds a tall vertical resource box with industrial aesthetic.
        /// Used for both Scrap (health) and Battery (mana) displays.
        /// </summary>
        private Control BuildResourceBox(string headerText, Color accentColor, Color fillColor,
            float anchorH, float edgeMargin, bool rightSide,
            float boxW, float boxH, float bottomMargin, int borderWidth,
            Color bgColor, float bgOpacity, int headerFontSize,
            out ProgressBar bar, out Label valueText, out StyleBoxFlat fillStyle,
            float configX = -1, float configY = -1)
        {
            var container = new Control();

            // Use explicit config position if available, otherwise fall back to anchor-based layout
            if (configX >= 0 && configY >= 0)
            {
                container.Position = new Vector2(configX, configY);
                container.Size = new Vector2(boxW, boxH);
            }
            else
            {
                container.AnchorLeft = anchorH;
                container.AnchorRight = anchorH;
                container.AnchorTop = 1f;
                container.AnchorBottom = 1f;

                if (rightSide)
                {
                    container.OffsetLeft = -(edgeMargin + boxW);
                    container.OffsetRight = -edgeMargin;
                }
                else
                {
                    container.OffsetLeft = edgeMargin;
                    container.OffsetRight = edgeMargin + boxW;
                }
                container.OffsetTop = -(boxH + bottomMargin);
                container.OffsetBottom = -bottomMargin;
            }
            AddChild(container);

            // Industrial panel frame — sharp corners, thick border
            var panel = new PanelContainer();
            panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            var panelStyle = new StyleBoxFlat();
            panelStyle.BgColor = new Color(bgColor.R, bgColor.G, bgColor.B, bgOpacity);
            panelStyle.BorderColor = accentColor * new Color(1, 1, 1, 0.5f);
            panelStyle.BorderWidthLeft = borderWidth;
            panelStyle.BorderWidthRight = borderWidth;
            panelStyle.BorderWidthTop = borderWidth;
            panelStyle.BorderWidthBottom = borderWidth;
            panelStyle.CornerRadiusBottomLeft = 0;
            panelStyle.CornerRadiusBottomRight = 0;
            panelStyle.CornerRadiusTopLeft = 0;
            panelStyle.CornerRadiusTopRight = 0;
            panelStyle.ContentMarginLeft = 10;
            panelStyle.ContentMarginRight = 10;
            panelStyle.ContentMarginTop = 8;
            panelStyle.ContentMarginBottom = 8;
            panel.AddThemeStyleboxOverride("panel", panelStyle);
            container.AddChild(panel);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 4);
            panel.AddChild(vbox);

            // Header label — stencil-style industrial text
            var header = new Label();
            header.Text = headerText;
            header.HorizontalAlignment = HorizontalAlignment.Center;
            header.AddThemeFontSizeOverride("font_size", headerFontSize);
            header.AddThemeColorOverride("font_color", accentColor);
            vbox.AddChild(header);

            // Accent separator line
            var sep = new ColorRect();
            sep.Color = accentColor * new Color(1, 1, 1, 0.3f);
            sep.CustomMinimumSize = new Vector2(0, 2);
            vbox.AddChild(sep);

            // Vertical fill bar (bottom-to-top, like a tank gauge)
            bar = new ProgressBar();
            bar.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            bar.MinValue = 0;
            bar.MaxValue = 100;
            bar.Value = 100;
            bar.ShowPercentage = false;
            bar.FillMode = 3; // BottomToTop

            var barBgStyle = new StyleBoxFlat();
            barBgStyle.BgColor = new Color(0.02f, 0.02f, 0.03f);
            barBgStyle.CornerRadiusBottomLeft = 0;
            barBgStyle.CornerRadiusBottomRight = 0;
            barBgStyle.CornerRadiusTopLeft = 0;
            barBgStyle.CornerRadiusTopRight = 0;
            barBgStyle.BorderWidthLeft = 1;
            barBgStyle.BorderWidthRight = 1;
            barBgStyle.BorderWidthTop = 1;
            barBgStyle.BorderWidthBottom = 1;
            barBgStyle.BorderColor = accentColor * new Color(1, 1, 1, 0.15f);
            bar.AddThemeStyleboxOverride("background", barBgStyle);

            fillStyle = new StyleBoxFlat();
            fillStyle.BgColor = fillColor;
            fillStyle.CornerRadiusBottomLeft = 0;
            fillStyle.CornerRadiusBottomRight = 0;
            fillStyle.CornerRadiusTopLeft = 0;
            fillStyle.CornerRadiusTopRight = 0;
            bar.AddThemeStyleboxOverride("fill", fillStyle);

            vbox.AddChild(bar);

            // Value text below the gauge
            valueText = new Label();
            valueText.Text = "100 / 100";
            valueText.HorizontalAlignment = HorizontalAlignment.Center;
            valueText.AddThemeFontSizeOverride("font_size", 13);
            valueText.AddThemeColorOverride("font_color", Colors.White);
            vbox.AddChild(valueText);

            // Corner bolt decorations (industrial rivets)
            const float boltSize = 6f;
            var boltColor = accentColor * new Color(1, 1, 1, 0.45f);
            AddBolt(container, 0, 0, boltSize, boltColor);
            AddBolt(container, boxW - boltSize, 0, boltSize, boltColor);
            AddBolt(container, 0, boxH - boltSize, boltSize, boltColor);
            AddBolt(container, boxW - boltSize, boxH - boltSize, boltSize, boltColor);

            return container;
        }

        private static void AddBolt(Control parent, float x, float y, float size, Color color)
        {
            var bolt = new ColorRect();
            bolt.Color = color;
            bolt.Position = new Vector2(x, y);
            bolt.Size = new Vector2(size, size);
            parent.AddChild(bolt);
        }

        private void BuildXPBar()
        {
            var xpFillColor = UIConfigLoader.GetColor("HUD", "XPBar", "FillColor", new Color(0.6f, 0.3f, 0.8f));
            var xpBgColor = UIConfigLoader.GetColor("HUD", "XPBar", "BgColor", new Color(0.15f, 0.1f, 0.2f));
            int xpFontSize = UIConfigLoader.GetInt("HUD", "XPBar", "FontSize", 12);

            // Thin industrial XP strip spanning bottom-center between resource boxes
            float xpPosX = UIConfigLoader.GetFloat("HUD", "XPBar", "PositionX", 190f);
            float xpPosY = UIConfigLoader.GetFloat("HUD", "XPBar", "PositionY", 1068f);
            float xpH = UIConfigLoader.GetFloat("HUD", "XPBar", "Height", 8f);

            var container = new HBoxContainer();
            container.Position = new Vector2(xpPosX, xpPosY);
            // Width spans between the two resource boxes
            container.Size = new Vector2(1920f - xpPosX * 2f, xpH + 16f);
            container.AddThemeConstantOverride("separation", 6);
            AddChild(container);

            _xpLevelLabel = new Label();
            _xpLevelLabel.AddThemeFontSizeOverride("font_size", 11);
            _xpLevelLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.3f));
            _xpLevelLabel.Text = StringLoader.Get("ui.hud.levelPrefix") + "1";
            container.AddChild(_xpLevelLabel);

            _xpBar = new ProgressBar();
            _xpBar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _xpBar.CustomMinimumSize = new Vector2(0, 14);
            _xpBar.MinValue = 0;
            _xpBar.MaxValue = 100;
            _xpBar.Value = 0;
            _xpBar.ShowPercentage = false;

            var bgStyle = new StyleBoxFlat();
            bgStyle.BgColor = xpBgColor;
            bgStyle.CornerRadiusBottomLeft = 0;
            bgStyle.CornerRadiusBottomRight = 0;
            bgStyle.CornerRadiusTopLeft = 0;
            bgStyle.CornerRadiusTopRight = 0;
            _xpBar.AddThemeStyleboxOverride("background", bgStyle);

            var fill = new StyleBoxFlat();
            fill.BgColor = xpFillColor;
            fill.CornerRadiusBottomLeft = 0;
            fill.CornerRadiusBottomRight = 0;
            fill.CornerRadiusTopLeft = 0;
            fill.CornerRadiusTopRight = 0;
            _xpBar.AddThemeStyleboxOverride("fill", fill);

            container.AddChild(_xpBar);

            _xpText = new Label();
            _xpText.CustomMinimumSize = new Vector2(55, 0);
            _xpText.AddThemeFontSizeOverride("font_size", xpFontSize);
            _xpText.AddThemeColorOverride("font_color", new Color(0.7f, 0.6f, 0.8f));
            _xpText.Text = "0/100";
            container.AddChild(_xpText);
        }

        private void BuildBuffStrip()
        {
            float spacing = UIConfigLoader.GetFloat("HUD", "BuffStrip", "Spacing", 4f);
            float posX = UIConfigLoader.GetFloat("HUD", "BuffStrip", "PositionX", 30f);
            float posY = UIConfigLoader.GetFloat("HUD", "BuffStrip", "PositionY", 780f);

            _buffContainer = new HBoxContainer();
            _buffContainer.Position = new Vector2(posX, posY);
            _buffContainer.Size = new Vector2(200, 32);
            _buffContainer.AddThemeConstantOverride("separation", (int)spacing);
            AddChild(_buffContainer);
        }

        private void BuildDashIndicator()
        {
            int fontSize = UIConfigLoader.GetInt("HUD", "DashIndicator", "FontSize", 10);
            float posX = UIConfigLoader.GetFloat("HUD", "DashIndicator", "PositionX", 880f);
            float posY = UIConfigLoader.GetFloat("HUD", "DashIndicator", "PositionY", 1056f);

            var container = new HBoxContainer();
            container.Position = new Vector2(posX, posY);
            container.Size = new Vector2(160, 20);
            container.OffsetBottom = -6;
            container.AddThemeConstantOverride("separation", 4);
            AddChild(container);

            var label = new Label();
            label.Text = StringLoader.Get("ui.hud.dash");
            label.AddThemeFontSizeOverride("font_size", fontSize);
            label.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.6f));
            container.AddChild(label);

            _dashPips = new Label[2];
            for (int i = 0; i < 2; i++)
            {
                var pip = new Label();
                pip.Text = "[=]";
                pip.AddThemeFontSizeOverride("font_size", 11);
                pip.AddThemeColorOverride("font_color", _dashActiveColor);
                container.AddChild(pip);
                _dashPips[i] = pip;
            }
        }

        private void BuildConsumableIndicators()
        {
            int fontSize = UIConfigLoader.GetInt("HUD", "ConsumableList", "FontSize", 12);
            float posX = UIConfigLoader.GetFloat("HUD", "ConsumableList", "PositionX", 30f);
            float posY = UIConfigLoader.GetFloat("HUD", "ConsumableList", "PositionY", 730f);

            // Health consumable list — near Scrap box
            _healthPotionList = new VBoxContainer();
            _healthPotionList.Position = new Vector2(posX, posY);
            _healthPotionList.Size = new Vector2(160, 50);
            _healthPotionList.AddThemeConstantOverride("separation", 1);
            AddChild(_healthPotionList);

            // Mana consumable list — mirrored on right side
            float manaX = UIConfigLoader.GetFloat("HUD", "BatteryBar", "PositionX", 1740f);
            _manaPotionList = new VBoxContainer();
            _manaPotionList.Position = new Vector2(manaX, posY);
            _manaPotionList.Size = new Vector2(160, 50);
            _manaPotionList.AddThemeConstantOverride("separation", 1);
            AddChild(_manaPotionList);
        }

        private void RefreshConsumableIndicators()
        {
            if (_player?.Inventory == null) return;

            int fontSize = UIConfigLoader.GetInt("HUD", "ConsumableList", "FontSize", 12);
            string healthKey = UIConfigLoader.Get("HUD", "ConsumableList", "HealthKeyLabel", "[Q]");
            string manaKey = UIConfigLoader.Get("HUD", "ConsumableList", "ManaKeyLabel", "[F]");

            // Group consumables by base data ID, summing stack counts
            var healthItems = new System.Collections.Generic.Dictionary<string, (string name, int count)>();
            var manaItems = new System.Collections.Generic.Dictionary<string, (string name, int count)>();

            foreach (var item in _player.Inventory.Items)
            {
                if (item.BaseData is not ConsumableData c) continue;

                if (c.HealAmount > 0)
                {
                    if (healthItems.TryGetValue(c.Id, out var existing))
                        healthItems[c.Id] = (existing.name, existing.count + item.StackCount);
                    else
                        healthItems[c.Id] = (c.ItemName, item.StackCount);
                }

                if (c.ManaRestoreAmount > 0)
                {
                    if (manaItems.TryGetValue(c.Id, out var existing))
                        manaItems[c.Id] = (existing.name, existing.count + item.StackCount);
                    else
                        manaItems[c.Id] = (c.ItemName, item.StackCount);
                }
            }

            // Rebuild health list
            foreach (var child in _healthPotionList.GetChildren())
                if (child is Node n) n.QueueFree();

            foreach (var kv in healthItems)
            {
                var label = new Label();
                label.Text = $"{healthKey} {kv.Value.name} x{kv.Value.count}";
                label.AddThemeFontSizeOverride("font_size", fontSize);
                label.AddThemeColorOverride("font_color", new Color(0.3f, 0.8f, 0.3f, 0.85f));
                _healthPotionList.AddChild(label);
            }

            // Rebuild mana list
            foreach (var child in _manaPotionList.GetChildren())
                if (child is Node n) n.QueueFree();

            foreach (var kv in manaItems)
            {
                var label = new Label();
                label.Text = $"{manaKey} {kv.Value.name} x{kv.Value.count}";
                label.HorizontalAlignment = HorizontalAlignment.Right;
                label.AddThemeFontSizeOverride("font_size", fontSize);
                label.AddThemeColorOverride("font_color", new Color(0.3f, 0.5f, 0.95f, 0.85f));
                _manaPotionList.AddChild(label);
            }
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

            // Dash charge pips
            if (_dashPips != null && _player?.Movement != null)
            {
                int charges = _player.Movement.DashCharges;
                for (int i = 0; i < _dashPips.Length; i++)
                {
                    _dashPips[i].AddThemeColorOverride("font_color",
                        i < charges ? _dashActiveColor : _dashInactiveColor);
                }
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

            // Consumable indicators
            if (_player.Inventory != null)
            {
                _player.Inventory.OnItemAdded += OnInventoryChanged;
                _player.Inventory.OnItemRemoved += OnInventoryChanged;
            }
            GameEvents.OnItemUsed += OnItemUsedRefresh;
            RefreshConsumableIndicators();
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
                    _healthFill.BgColor = _healthLow;
                else if (pct <= 0.6f)
                    _healthFill.BgColor = _healthMid;
                else
                    _healthFill.BgColor = _healthHigh;
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
            _xpLevelLabel.Text = StringLoader.Get("ui.hud.levelPrefix") + newLevel;
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

        private void OnInventoryChanged(ItemInstance _) => RefreshConsumableIndicators();
        private void OnItemUsedRefresh(object _) => RefreshConsumableIndicators();

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

            int maxVisible = UIConfigLoader.GetInt("HUD", "BuffStrip", "MaxVisible", 8);
            var effects = sem.ActiveEffects;
            for (int i = 0; i < effects.Count && i < maxVisible; i++)
            {
                var effect = effects[i];
                _buffContainer.AddChild(BuildBuffIcon(effect));
            }
        }

        private static PanelContainer BuildBuffIcon(StatusEffect effect)
        {
            bool isDebuff = effect.Data.IsDebuff;
            var borderColor = isDebuff ? new Color(0.8f, 0.2f, 0.2f) : new Color(0.2f, 0.8f, 0.3f);

            float iconSize = UIConfigLoader.GetFloat("HUD", "BuffStrip", "IconSize", 32f);
            var bgColor = UIConfigLoader.GetColor("HUD", "BuffStrip", "BgColor", new Color(0.1f, 0.1f, 0.12f));

            var panel = new PanelContainer();
            panel.CustomMinimumSize = new Vector2(iconSize, iconSize);

            var style = new StyleBoxFlat();
            style.BgColor = new Color(bgColor.R, bgColor.G, bgColor.B, 0.9f);
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

        private void UpdateSectorAreaLabel()
        {
            int sector = GameManager.Instance?.CurrentSector ?? 1;
            int area = GameManager.Instance?.CurrentArea ?? 1;
            _sectorAreaLabel.Text = $"Sector {sector} - Area {area}";
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
            GameEvents.OnItemUsed -= OnItemUsedRefresh;

            if (_player != null)
            {
                if (_player.Health != null)
                    _player.Health.OnHealthChanged -= OnHealthChanged;
                if (_player.Stats != null)
                {
                    _player.Stats.OnManaChanged -= OnManaChanged;
                    _player.Stats.OnLevelUp -= OnLevelUp;
                }
                if (_player.Inventory != null)
                {
                    _player.Inventory.OnItemAdded -= OnInventoryChanged;
                    _player.Inventory.OnItemRemoved -= OnInventoryChanged;
                }
            }

            // Clean up overlays added to root (prevents them persisting across scene changes)
            if (_inventoryUI != null && GodotObject.IsInstanceValid(_inventoryUI))
                _inventoryUI.QueueFree();
            if (_passiveTreeUI != null && GodotObject.IsInstanceValid(_passiveTreeUI))
                _passiveTreeUI.QueueFree();
            if (_pauseMenuUI != null && GodotObject.IsInstanceValid(_pauseMenuUI))
                _pauseMenuUI.QueueFree();
            if (_characterSheetUI != null && GodotObject.IsInstanceValid(_characterSheetUI))
                _characterSheetUI.QueueFree();
            if (_bossHealthBar != null && GodotObject.IsInstanceValid(_bossHealthBar))
                _bossHealthBar.QueueFree();
            if (_scrapPopup != null && GodotObject.IsInstanceValid(_scrapPopup))
                _scrapPopup.QueueFree();
            if (_lootBoxTracker != null && GodotObject.IsInstanceValid(_lootBoxTracker))
                _lootBoxTracker.QueueFree();
            if (_commentaryToast != null && GodotObject.IsInstanceValid(_commentaryToast))
                _commentaryToast.QueueFree();
        }
    }
}
