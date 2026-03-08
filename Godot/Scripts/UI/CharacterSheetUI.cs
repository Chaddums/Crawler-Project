using System;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Character sheet overlay. C key to toggle. Shows class, level, XP, all stats.
    /// </summary>
    public partial class CharacterSheetUI : CanvasLayer
    {
        private Control _panel;
        private bool _isOpen;
        private VBoxContainer _statsContainer;
        private Label _classLabel;
        private Label _levelLabel;
        private ProgressBar _xpBar;
        private Label _xpLabel;

        public override void _Ready()
        {
            Layer = 22;
            ProcessMode = ProcessModeEnum.Always;
            BuildUI();
            _panel.Visible = false;
        }

        private void BuildUI()
        {
            _panel = new Control();
            _panel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(_panel);

            // Dim background
            var bg = new ColorRect();
            bg.Color = new Color(0, 0, 0, 0.6f);
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            bg.MouseFilter = Control.MouseFilterEnum.Stop;
            bg.GuiInput += (ev) =>
            {
                if (ev is InputEventMouseButton { Pressed: true })
                    Close();
            };
            _panel.AddChild(bg);

            // Main panel
            var panelBox = new PanelContainer();
            panelBox.Position = new Vector2(560, 100);
            panelBox.Size = new Vector2(800, 880);
            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.06f, 0.06f, 0.12f, 0.95f);
            style.BorderColor = new Color(0.5f, 0.45f, 0.2f);
            style.BorderWidthBottom = 2;
            style.BorderWidthTop = 2;
            style.BorderWidthLeft = 2;
            style.BorderWidthRight = 2;
            style.ContentMarginLeft = 24;
            style.ContentMarginRight = 24;
            style.ContentMarginTop = 16;
            style.ContentMarginBottom = 16;
            panelBox.AddThemeStyleboxOverride("panel", style);
            _panel.AddChild(panelBox);

            var mainVbox = new VBoxContainer();
            mainVbox.AddThemeConstantOverride("separation", 8);
            panelBox.AddChild(mainVbox);

            // Title bar
            var titleBar = new HBoxContainer();
            mainVbox.AddChild(titleBar);

            var title = new Label();
            title.Text = StringLoader.Get("ui.characterSheet.title");
            title.AddThemeFontSizeOverride("font_size", 28);
            title.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.3f));
            title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            titleBar.AddChild(title);

            var closeBtn = new Button();
            closeBtn.Text = "X";
            closeBtn.CustomMinimumSize = new Vector2(36, 36);
            closeBtn.Pressed += Close;
            titleBar.AddChild(closeBtn);

            // Class and level
            _classLabel = new Label();
            _classLabel.AddThemeFontSizeOverride("font_size", 22);
            _classLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.85f, 1f));
            mainVbox.AddChild(_classLabel);

            _levelLabel = new Label();
            _levelLabel.AddThemeFontSizeOverride("font_size", 18);
            mainVbox.AddChild(_levelLabel);

            // XP bar
            var xpBox = new HBoxContainer();
            xpBox.AddThemeConstantOverride("separation", 8);
            mainVbox.AddChild(xpBox);

            var xpLabel = new Label();
            xpLabel.Text = StringLoader.Get("ui.characterSheet.xpLabel");
            xpLabel.AddThemeFontSizeOverride("font_size", 16);
            xpBox.AddChild(xpLabel);

            _xpBar = new ProgressBar();
            _xpBar.CustomMinimumSize = new Vector2(500, 24);
            _xpBar.ShowPercentage = false;
            xpBox.AddChild(_xpBar);

            _xpLabel = new Label();
            _xpLabel.AddThemeFontSizeOverride("font_size", 14);
            xpBox.AddChild(_xpLabel);

            // Separator
            var sep = new HSeparator();
            sep.CustomMinimumSize = new Vector2(0, 8);
            mainVbox.AddChild(sep);

            // Stats header
            var statsTitle = new Label();
            statsTitle.Text = StringLoader.Get("ui.characterSheet.statsHeader");
            statsTitle.AddThemeFontSizeOverride("font_size", 20);
            statsTitle.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.3f));
            mainVbox.AddChild(statsTitle);

            // Scrollable stats grid
            var scroll = new ScrollContainer();
            scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            mainVbox.AddChild(scroll);

            _statsContainer = new VBoxContainer();
            _statsContainer.AddThemeConstantOverride("separation", 4);
            scroll.AddChild(_statsContainer);
        }

        public override void _UnhandledInput(InputEvent ev)
        {
            if (ev.IsActionPressed("character_sheet"))
            {
                if (_isOpen) Close();
                else Open();
                GetViewport().SetInputAsHandled();
            }
        }

        private void Open()
        {
            _isOpen = true;
            _panel.Visible = true;
            GetTree().Paused = true;
            Refresh();
        }

        private void Close()
        {
            _isOpen = false;
            _panel.Visible = false;
            GetTree().Paused = false;
        }

        private void Refresh()
        {
            if (!ServiceLocator.TryGet<PlayerController>(out var player)) return;

            var classCtrl = player.ClassController;
            var stats = player.Stats;

            // Class info
            string className = classCtrl.ClassData?.DisplayName ?? "Unknown";
            _classLabel.Text = className;
            _levelLabel.Text = $"Level {stats.Level}  |  Skill Points: {stats.AvailableSkillPoints}";

            // XP
            int xpNeeded = stats.ExperienceToNextLevel;
            _xpBar.MaxValue = xpNeeded > 0 ? xpNeeded : 1;
            _xpBar.Value = stats.Experience;
            _xpLabel.Text = $"{stats.Experience} / {xpNeeded}";

            // Stats
            foreach (var child in _statsContainer.GetChildren())
                child.QueueFree();

            var statTypes = Enum.GetValues<StatType>();
            foreach (var statType in statTypes)
            {
                float baseVal = stats.Stats.GetBaseStat(statType);
                float totalVal = stats.Stats.GetStat(statType);

                // Skip zero stats
                if (baseVal == 0 && totalVal == 0) continue;

                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 8);
                _statsContainer.AddChild(row);

                var nameLabel = new Label();
                nameLabel.Text = FormatStatName(statType);
                nameLabel.AddThemeFontSizeOverride("font_size", 16);
                nameLabel.CustomMinimumSize = new Vector2(200, 0);
                row.AddChild(nameLabel);

                var valueLabel = new Label();
                valueLabel.Text = $"{totalVal:F1}";
                valueLabel.AddThemeFontSizeOverride("font_size", 16);
                valueLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 0.6f));
                valueLabel.CustomMinimumSize = new Vector2(80, 0);
                row.AddChild(valueLabel);

                // Show bonus if different from base
                float bonus = totalVal - baseVal;
                if (Mathf.Abs(bonus) > 0.01f)
                {
                    var bonusLabel = new Label();
                    string sign = bonus > 0 ? "+" : "";
                    bonusLabel.Text = $"({sign}{bonus:F1})";
                    bonusLabel.AddThemeFontSizeOverride("font_size", 14);
                    bonusLabel.AddThemeColorOverride("font_color",
                        bonus > 0 ? new Color(0.3f, 0.9f, 0.3f) : new Color(0.9f, 0.3f, 0.3f));
                    row.AddChild(bonusLabel);
                }
            }
        }

        private static string FormatStatName(StatType stat)
        {
            // Insert spaces before capitals: "MaxHealth" -> "Max Health"
            var name = stat.ToString();
            var result = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]))
                    result.Append(' ');
                result.Append(name[i]);
            }
            return result.ToString();
        }

        public override void _Process(double delta)
        {
            if (_isOpen)
                Refresh();
        }
    }
}
