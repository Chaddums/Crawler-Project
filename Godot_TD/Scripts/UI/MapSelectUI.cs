using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Map and difficulty selection before battle.
    /// </summary>
    public partial class MapSelectUI : Control
    {
        public override void _Ready()
        {
            // Background
            var bg = new ColorRect();
            bg.SetAnchorsPreset(LayoutPreset.FullRect);
            bg.Color = new Color(0.06f, 0.05f, 0.04f);
            AddChild(bg);

            var center = new CenterContainer();
            center.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(center);

            var vbox = new VBoxContainer();
            vbox.CustomMinimumSize = new Vector2(500, 0);
            vbox.AddThemeConstantOverride("separation", 15);
            center.AddChild(vbox);

            // Title
            var title = new Label();
            title.Text = "SELECT MAP";
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.AddThemeFontSizeOverride("font_size", 36);
            title.AddThemeColorOverride("font_color", new Color(0.9f, 0.7f, 0.3f));
            vbox.AddChild(title);

            // Map buttons
            foreach (var map in MapLayouts.Available)
            {
                var btn = new Button();
                btn.Text = $"{map.Name}  ({map.Width}x{map.Height})\n{map.Description}";
                btn.CustomMinimumSize = new Vector2(0, 60);
                var id = map.Id;
                btn.Pressed += () => SelectMap(id);
                vbox.AddChild(btn);
            }

            // Difficulty section
            var sep = new HSeparator();
            vbox.AddChild(sep);

            var diffLabel = new Label();
            diffLabel.Text = "DIFFICULTY";
            diffLabel.HorizontalAlignment = HorizontalAlignment.Center;
            diffLabel.AddThemeFontSizeOverride("font_size", 20);
            diffLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.5f, 0.3f));
            vbox.AddChild(diffLabel);

            var diffBar = new HBoxContainer();
            diffBar.AddThemeConstantOverride("separation", 10);
            vbox.AddChild(diffBar);

            AddDifficultyButton(diffBar, "Scrapyard", 0.7f, new Color(0.3f, 0.8f, 0.3f));
            AddDifficultyButton(diffBar, "Junkyard", 1.0f, new Color(0.9f, 0.7f, 0.2f));
            AddDifficultyButton(diffBar, "Wasteland", 1.5f, new Color(0.9f, 0.3f, 0.2f));

            // Back button
            var backBtn = new Button();
            backBtn.Text = "Back";
            backBtn.CustomMinimumSize = new Vector2(0, 40);
            backBtn.Pressed += () => GameManager.Instance?.ReturnToMainMenu();
            vbox.AddChild(backBtn);
        }

        private float _selectedDifficulty = 1.0f;

        private void AddDifficultyButton(HBoxContainer parent, string name, float multiplier, Color color)
        {
            var btn = new Button();
            btn.Text = name;
            btn.CustomMinimumSize = new Vector2(140, 40);
            btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            btn.Pressed += () =>
            {
                _selectedDifficulty = multiplier;
                // Visual feedback — update button states
                foreach (var child in parent.GetChildren())
                {
                    if (child is Button b)
                        b.Modulate = Colors.White;
                }
                btn.Modulate = color;
            };

            // Default selection for Junkyard
            if (multiplier == 1.0f)
                btn.Modulate = new Color(0.9f, 0.7f, 0.2f);

            parent.AddChild(btn);
        }

        private void SelectMap(string mapId)
        {
            GameManager.Instance.SelectedMapId = mapId;
            GameManager.Instance.DifficultyMultiplier = _selectedDifficulty;
            GameManager.Instance.StartBattle();
        }
    }
}
