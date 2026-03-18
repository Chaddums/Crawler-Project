using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Main menu with New Game / Quit.
    /// </summary>
    public partial class MainMenuUI : Control
    {
        public override void _Ready()
        {
            // Dark background
            var bg = new ColorRect();
            bg.SetAnchorsPreset(LayoutPreset.FullRect);
            bg.Color = new Color(0.06f, 0.05f, 0.04f);
            AddChild(bg);

            // Center container
            var center = new CenterContainer();
            center.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(center);

            var vbox = new VBoxContainer();
            vbox.CustomMinimumSize = new Vector2(400, 0);
            vbox.AddThemeConstantOverride("separation", 20);
            center.AddChild(vbox);

            var title = new Label();
            title.Text = "JUNKYARD TD";
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.AddThemeFontSizeOverride("font_size", 48);
            title.AddThemeColorOverride("font_color", new Color(0.9f, 0.7f, 0.3f));
            vbox.AddChild(title);

            var subtitle = new Label();
            subtitle.Text = "Defend the Core. Salvage Everything.";
            subtitle.HorizontalAlignment = HorizontalAlignment.Center;
            subtitle.AddThemeFontSizeOverride("font_size", 16);
            subtitle.AddThemeColorOverride("font_color", new Color(0.6f, 0.55f, 0.5f));
            vbox.AddChild(subtitle);

            var spacer = new Control();
            spacer.CustomMinimumSize = new Vector2(0, 30);
            vbox.AddChild(spacer);

            var newGameBtn = new Button();
            newGameBtn.Text = "Classic TD";
            newGameBtn.CustomMinimumSize = new Vector2(200, 50);
            newGameBtn.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            newGameBtn.Pressed += OnNewGame;
            vbox.AddChild(newGameBtn);

            var vineBtn = new Button();
            vineBtn.Text = "Planet 1: Grid Prime";
            vineBtn.CustomMinimumSize = new Vector2(200, 50);
            vineBtn.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            vineBtn.Pressed += () => LaunchPlanet(1);
            vbox.AddChild(vineBtn);

            var scrapBtn = new Button();
            scrapBtn.Text = "Planet 2: Scrapyard";
            scrapBtn.CustomMinimumSize = new Vector2(200, 50);
            scrapBtn.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            scrapBtn.Pressed += () => LaunchPlanet(2);
            vbox.AddChild(scrapBtn);

            var quitBtn = new Button();
            quitBtn.Text = "Quit";
            quitBtn.CustomMinimumSize = new Vector2(200, 50);
            quitBtn.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            quitBtn.Pressed += () => GetTree().Quit();
            vbox.AddChild(quitBtn);
        }

        private void OnNewGame()
        {
            GameManager.Instance?.GoToMapSelect();
        }

        private void LaunchPlanet(int planet)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.CurrentPlanet = planet;
            GameEvents.ClearAll();
            GetTree().ChangeSceneToFile(Constants.SCENE_INTRO_CINEMATIC);
        }
    }
}
