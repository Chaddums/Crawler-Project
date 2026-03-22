using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Main menu — planet select, meta layer access.
    /// S1: removed Classic TD button, updated branding for extraction loop.
    /// </summary>
    public partial class MainMenuUI : Control
    {
        public override void _Ready()
        {
            var bg = new ColorRect();
            bg.SetAnchorsPreset(LayoutPreset.FullRect);
            bg.Color = new Color(0.06f, 0.05f, 0.04f);
            AddChild(bg);

            var center = new CenterContainer();
            center.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(center);

            var vbox = new VBoxContainer();
            vbox.CustomMinimumSize = new Vector2(400, 0);
            vbox.AddThemeConstantOverride("separation", 20);
            center.AddChild(vbox);

            var title = new Label();
            title.Text = "VINE LOGIC TD";
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.AddThemeFontSizeOverride("font_size", 48);
            title.AddThemeColorOverride("font_color", new Color(0.9f, 0.7f, 0.3f));
            vbox.AddChild(title);

            var subtitle = new Label();
            subtitle.Text = "Mine everything you can before they take it all.";
            subtitle.HorizontalAlignment = HorizontalAlignment.Center;
            subtitle.AddThemeFontSizeOverride("font_size", 16);
            subtitle.AddThemeColorOverride("font_color", new Color(0.6f, 0.55f, 0.5f));
            vbox.AddChild(subtitle);

            var spacer = new Control();
            spacer.CustomMinimumSize = new Vector2(0, 30);
            vbox.AddChild(spacer);

            // Planet select
            var planetLabel = new Label();
            planetLabel.Text = "SELECT PLANET";
            planetLabel.HorizontalAlignment = HorizontalAlignment.Center;
            planetLabel.AddThemeFontSizeOverride("font_size", 20);
            planetLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.65f, 0.6f));
            vbox.AddChild(planetLabel);

            var planet1Btn = new Button();
            planet1Btn.Text = "Grid Prime";
            planet1Btn.CustomMinimumSize = new Vector2(200, 50);
            planet1Btn.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            planet1Btn.Pressed += () => LaunchPlanet(1);
            vbox.AddChild(planet1Btn);

            var planet2Btn = new Button();
            planet2Btn.Text = "Scrapyard";
            planet2Btn.CustomMinimumSize = new Vector2(200, 50);
            planet2Btn.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            planet2Btn.Pressed += () => LaunchPlanet(2);
            vbox.AddChild(planet2Btn);

            var spacer2 = new Control();
            spacer2.CustomMinimumSize = new Vector2(0, 10);
            vbox.AddChild(spacer2);

            // S1: meta layer access point — S4 will build the full meta hub
            var metaBtn = new Button();
            metaBtn.Text = "Home Base";
            metaBtn.CustomMinimumSize = new Vector2(200, 50);
            metaBtn.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            metaBtn.Disabled = true;  // S4 will enable when meta hub exists
            metaBtn.TooltipText = "Territory, Suits, Relics (coming soon)";
            vbox.AddChild(metaBtn);

            // S1: Level Editor removed from main menu (access via F12 in-game)

            var quitBtn = new Button();
            quitBtn.Text = "Quit";
            quitBtn.CustomMinimumSize = new Vector2(200, 50);
            quitBtn.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            quitBtn.Pressed += () => GetTree().Quit();
            vbox.AddChild(quitBtn);
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
