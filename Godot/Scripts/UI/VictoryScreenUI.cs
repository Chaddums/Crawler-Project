using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Shown after defeating AXIS. First victory reveals the "fake ending" —
    /// AXIS taunts the player and unlocks Ascension mode. Subsequent victories
    /// show the new ascension rank and escalating AXIS dialogue.
    /// </summary>
    public partial class VictoryScreenUI : CanvasLayer
    {
        private static readonly Color Gold = new(0.95f, 0.8f, 0.3f);
        private static readonly Color Cyan = new(0.3f, 0.9f, 0.95f);
        private static readonly Color Red = new(0.9f, 0.25f, 0.2f);
        private static readonly Color DimWhite = new(0.75f, 0.75f, 0.75f);
        private static readonly Color Purple = new(0.7f, 0.4f, 0.95f);

        public void Show(int ascensionRank, int timesDefeated, float runTime,
            int kills, int level, BotFrameType frame)
        {
            Layer = 100;

            var bg = new ColorRect();
            bg.Color = new Color(0, 0, 0, 0.9f);
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            bg.MouseFilter = Control.MouseFilterEnum.Stop;
            AddChild(bg);

            var center = new CenterContainer();
            center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(center);

            var panel = new PanelContainer();
            panel.CustomMinimumSize = new Vector2(600, 0);
            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.04f, 0.04f, 0.10f, 0.97f);
            style.BorderColor = ascensionRank <= 1 ? Gold : Purple;
            style.SetBorderWidthAll(2);
            style.SetCornerRadiusAll(10);
            style.ContentMarginLeft = 50;
            style.ContentMarginRight = 50;
            style.ContentMarginTop = 40;
            style.ContentMarginBottom = 40;
            panel.AddThemeStyleboxOverride("panel", style);
            center.AddChild(panel);

            var vbox = new VBoxContainer();
            vbox.Alignment = BoxContainer.AlignmentMode.Center;
            vbox.AddThemeConstantOverride("separation", 8);
            panel.AddChild(vbox);

            if (ascensionRank <= 1)
                BuildFirstVictory(vbox, runTime, kills, level);
            else
                BuildAscensionVictory(vbox, ascensionRank, timesDefeated, runTime, kills, level);

            AddSpacer(vbox, 24);

            // Buttons
            var btnBox = new HBoxContainer();
            btnBox.Alignment = BoxContainer.AlignmentMode.Center;
            btnBox.AddThemeConstantOverride("separation", 20);
            vbox.AddChild(btnBox);

            var continueBtn = new Button();
            continueBtn.Text = ascensionRank <= 1
                ? StringLoader.Get("ui.victory.enterAscension")
                : StringLoader.Get("ui.victory.continueAscending");
            continueBtn.CustomMinimumSize = new Vector2(220, 50);
            continueBtn.AddThemeFontSizeOverride("font_size", 20);
            continueBtn.Pressed += () =>
            {
                // Record the run as a victory, then start new game at higher ascension
                MetaSaveManager.RecordRunEnd(frame,
                    GameManager.Instance?.CurrentSector ?? 5,
                    GameManager.Instance?.CurrentArea ?? 3,
                    level, kills, runTime, isVictory: true);
                QueueFree();
                GameManager.Instance?.StartNewGame();
            };
            btnBox.AddChild(continueBtn);

            var menuBtn = new Button();
            menuBtn.Text = StringLoader.Get("ui.victory.mainMenu");
            menuBtn.CustomMinimumSize = new Vector2(180, 50);
            menuBtn.AddThemeFontSizeOverride("font_size", 20);
            menuBtn.Pressed += () =>
            {
                MetaSaveManager.RecordRunEnd(frame,
                    GameManager.Instance?.CurrentSector ?? 5,
                    GameManager.Instance?.CurrentArea ?? 3,
                    level, kills, runTime, isVictory: true);
                QueueFree();
                GameManager.Instance?.ReturnToMainMenu();
            };
            btnBox.AddChild(menuBtn);
        }

        private void BuildFirstVictory(VBoxContainer vbox, float runTime, int kills, int level)
        {
            // Title
            var title = new Label();
            title.Text = StringLoader.Get("ui.victory.axisDefeated");
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.AddThemeFontSizeOverride("font_size", 46);
            title.AddThemeColorOverride("font_color", Gold);
            vbox.AddChild(title);

            AddSpacer(vbox, 8);

            // AXIS dialogue — the fake ending reveal
            AddDialogueLine(vbox, "AXIS", "...Impressive. You actually did it.", Cyan);
            AddDialogueLine(vbox, "AXIS", "But you didn't really think that was the end, did you?", Cyan);
            AddDialogueLine(vbox, "AXIS", "I was barely trying. The real arena starts NOW.", Red);

            AddSpacer(vbox, 12);

            // Ascension unlock announcement
            var unlock = new Label();
            unlock.Text = StringLoader.Get("ui.victory.ascensionUnlocked");
            unlock.HorizontalAlignment = HorizontalAlignment.Center;
            unlock.AddThemeFontSizeOverride("font_size", 28);
            unlock.AddThemeColorOverride("font_color", Purple);
            vbox.AddChild(unlock);

            var desc = new Label();
            desc.Text = StringLoader.Get("ui.victory.ascensionDesc");
            desc.HorizontalAlignment = HorizontalAlignment.Center;
            desc.AddThemeFontSizeOverride("font_size", 16);
            desc.AddThemeColorOverride("font_color", DimWhite);
            vbox.AddChild(desc);

            AddSpacer(vbox, 12);
            AddRunStats(vbox, runTime, kills, level);
        }

        private void BuildAscensionVictory(VBoxContainer vbox, int rank, int timesDefeated,
            float runTime, int kills, int level)
        {
            // Title
            var title = new Label();
            title.Text = StringLoader.Get("ui.victory.ascensionComplete", ("{rank}", rank.ToString()));
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.AddThemeFontSizeOverride("font_size", 42);
            title.AddThemeColorOverride("font_color", Purple);
            vbox.AddChild(title);

            AddSpacer(vbox, 8);

            // Escalating AXIS dialogue based on defeat count
            string axisLine = timesDefeated switch
            {
                2 => "You're getting lucky. It won't last.",
                3 => "Okay, I'll admit — you have some skill.",
                4 => "This is... unprecedented. Who built you?",
                5 => "I'm running out of ways to stop you. Almost.",
                _ when timesDefeated >= 10 => "I... I don't understand. You shouldn't exist.",
                _ => "You keep coming back. Why won't you break?",
            };
            AddDialogueLine(vbox, "AXIS", axisLine, Cyan);

            AddSpacer(vbox, 8);

            // Next ascension preview
            float nextMult = 1f + rank * 0.5f;
            var preview = new Label();
            preview.Text = StringLoader.Get("ui.victory.nextAscension",
                ("{rank}", (rank + 1).ToString()), ("{power}", $"{nextMult + 0.5f:F1}"));
            preview.HorizontalAlignment = HorizontalAlignment.Center;
            preview.AddThemeFontSizeOverride("font_size", 18);
            preview.AddThemeColorOverride("font_color", Red);
            vbox.AddChild(preview);

            AddSpacer(vbox, 12);
            AddRunStats(vbox, runTime, kills, level);
        }

        private void AddRunStats(VBoxContainer vbox, float runTime, int kills, int level)
        {
            int minutes = (int)(runTime / 60f);
            int seconds = (int)(runTime % 60f);

            AddStatRow(vbox, StringLoader.Get("ui.victory.timeLabel"), $"{minutes}:{seconds:D2}");
            AddStatRow(vbox, StringLoader.Get("ui.victory.levelLabel"), level.ToString());
            AddStatRow(vbox, StringLoader.Get("ui.victory.killsLabel"), kills.ToString());

            var scrapLabel = new Label();
            scrapLabel.Text = StringLoader.Get("ui.victory.scrapLabel", ("{value}", MetaSaveManager.Data.Scrap.ToString()));
            scrapLabel.HorizontalAlignment = HorizontalAlignment.Center;
            scrapLabel.AddThemeFontSizeOverride("font_size", 18);
            scrapLabel.AddThemeColorOverride("font_color", Gold);
            vbox.AddChild(scrapLabel);
        }

        private static void AddDialogueLine(VBoxContainer parent, string speaker, string text, Color color)
        {
            var line = new Label();
            line.Text = $"[{speaker}]: \"{text}\"";
            line.HorizontalAlignment = HorizontalAlignment.Center;
            line.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            line.CustomMinimumSize = new Vector2(500, 0);
            line.AddThemeFontSizeOverride("font_size", 16);
            line.AddThemeColorOverride("font_color", color);
            parent.AddChild(line);
        }

        private static void AddStatRow(VBoxContainer parent, string label, string value)
        {
            var row = new HBoxContainer();
            parent.AddChild(row);
            var nameLabel = new Label();
            nameLabel.Text = label;
            nameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            nameLabel.AddThemeFontSizeOverride("font_size", 20);
            nameLabel.AddThemeColorOverride("font_color", DimWhite);
            row.AddChild(nameLabel);
            var valLabel = new Label();
            valLabel.Text = value;
            valLabel.AddThemeFontSizeOverride("font_size", 20);
            valLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f));
            row.AddChild(valLabel);
        }

        private static void AddSpacer(VBoxContainer parent, float height)
        {
            var spacer = new Control();
            spacer.CustomMinimumSize = new Vector2(0, height);
            parent.AddChild(spacer);
        }
    }
}
