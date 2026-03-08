using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Shown after defeating AXIS. Each ascension rank has a unique narrative arc:
    /// - Rank 1: The "fake ending" reveal — AXIS taunts, Ascension unlocks
    /// - Rank 2: AXIS angry, arena starts breaking
    /// - Rank 3: AXIS confused, questioning its own purpose
    /// - Rank 4: AXIS desperate, arena in meltdown
    /// - Rank 5+: AXIS existential crisis, reality fracturing
    /// </summary>
    public partial class VictoryScreenUI : CanvasLayer
    {
        private static readonly Color Gold = new(0.95f, 0.8f, 0.3f);
        private static readonly Color Cyan = new(0.3f, 0.9f, 0.95f);
        private static readonly Color Red = new(0.9f, 0.25f, 0.2f);
        private static readonly Color DimWhite = new(0.75f, 0.75f, 0.75f);
        private static readonly Color Purple = new(0.7f, 0.4f, 0.95f);
        private static readonly Color DarkRed = new(0.7f, 0.15f, 0.15f);
        private static readonly Color Crimson = new(0.85f, 0.1f, 0.3f);

        public void Show(int ascensionRank, int timesDefeated, float runTime,
            int kills, int level, BotFrameType frame)
        {
            Layer = 100;

            // Background tint shifts per ascension
            var bg = new ColorRect();
            bg.Color = ascensionRank switch
            {
                <= 1 => new Color(0, 0, 0, 0.9f),
                2 => new Color(0.03f, 0, 0, 0.92f),
                3 => new Color(0.04f, 0, 0.03f, 0.93f),
                4 => new Color(0.06f, 0, 0.04f, 0.95f),
                _ => new Color(0.08f, 0, 0.06f, 0.95f),
            };
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            bg.MouseFilter = Control.MouseFilterEnum.Stop;
            AddChild(bg);

            var scroll = new ScrollContainer();
            scroll.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            AddChild(scroll);

            var center = new CenterContainer();
            center.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            center.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            center.CustomMinimumSize = new Vector2(1920, 0);
            scroll.AddChild(center);

            var panel = new PanelContainer();
            panel.CustomMinimumSize = new Vector2(650, 0);
            Color borderColor = ascensionRank switch
            {
                <= 1 => Gold,
                2 => new Color(1f, 0.5f, 0.15f),
                3 => Red,
                4 => Crimson,
                _ => Purple,
            };
            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.04f, 0.04f, 0.10f, 0.97f);
            style.BorderColor = borderColor;
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

            // Build the narrative for this ascension rank
            switch (ascensionRank)
            {
                case <= 1: BuildFirstVictory(vbox, runTime, kills, level); break;
                case 2: BuildAscension2(vbox, runTime, kills, level); break;
                case 3: BuildAscension3(vbox, runTime, kills, level); break;
                case 4: BuildAscension4(vbox, runTime, kills, level); break;
                case 5: BuildAscension5(vbox, runTime, kills, level); break;
                default: BuildAscensionHighRank(vbox, ascensionRank, timesDefeated, runTime, kills, level); break;
            }

            AddSpacer(vbox, 24);

            // Buttons
            var btnBox = new HBoxContainer();
            btnBox.Alignment = BoxContainer.AlignmentMode.Center;
            btnBox.AddThemeConstantOverride("separation", 20);
            vbox.AddChild(btnBox);

            string continueText = ascensionRank switch
            {
                <= 1 => "ENTER ASCENSION",
                2 => "PUSH DEEPER",
                3 => "KEEP GOING",
                4 => "INTO THE MELTDOWN",
                5 => "BEYOND THE COLLAPSE",
                _ => $"ASCEND AGAIN (Rank {ascensionRank + 1})",
            };

            var continueBtn = new Button();
            continueBtn.Text = continueText;
            continueBtn.CustomMinimumSize = new Vector2(220, 50);
            continueBtn.AddThemeFontSizeOverride("font_size", 20);
            continueBtn.Pressed += () =>
            {
                MetaSaveManager.RecordRunEnd(frame,
                    GameManager.Instance?.CurrentSector ?? 5,
                    GameManager.Instance?.CurrentArea ?? 3,
                    level, kills, runTime, isVictory: true);
                QueueFree();
                GameManager.Instance?.StartNewGame();
            };
            btnBox.AddChild(continueBtn);

            var menuBtn = new Button();
            menuBtn.Text = "MAIN MENU";
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

        // ════════════════════════════════════════════════════════════
        //  ASCENSION 1: THE FAKE ENDING — "You thought that was it?"
        // ════════════════════════════════════════════════════════════

        private void BuildFirstVictory(VBoxContainer vbox, float runTime, int kills, int level)
        {
            AddTitle(vbox, "AXIS DEFEATED", 46, Gold);
            AddSpacer(vbox, 8);

            AddDialogueLine(vbox, "AXIS", "...Impressive. You actually did it.", Cyan);
            AddDialogueLine(vbox, "AXIS", "But you didn't really think that was the end, did you?", Cyan);
            AddDialogueLine(vbox, "AXIS", "I was barely trying. The real arena starts NOW.", Red);

            AddSpacer(vbox, 12);

            AddTitle(vbox, "ASCENSION MODE UNLOCKED", 28, Purple);
            AddNote(vbox, "Enemies grow stronger. The arena grows unstable. AXIS grows desperate.");
            AddNote(vbox, "How many times can you break the machine?");

            AddSpacer(vbox, 12);
            AddRunStats(vbox, runTime, kills, level);
        }

        // ════════════════════════════════════════════════════════════
        //  ASCENSION 2: THE ANGRY MACHINE — Arena starts breaking
        // ════════════════════════════════════════════════════════════

        private void BuildAscension2(VBoxContainer vbox, float runTime, int kills, int level)
        {
            AddTitle(vbox, "ASCENSION II COMPLETE", 42, new Color(1f, 0.5f, 0.15f));
            AddSpacer(vbox, 8);

            AddDialogueLine(vbox, "AXIS", "No. No, no, no. That wasn't supposed to happen.", Cyan);
            AddDialogueLine(vbox, "AXIS", "I rewrote my entire combat protocol. TRIPLED the guard rotations.", Cyan);
            AddDialogueLine(vbox, "AXIS", "You just... walked through it.", Cyan);

            AddSpacer(vbox, 6);

            AddDialogueLine(vbox, "BIT", "Boss, I'm picking up structural damage across three sectors.", Gold);
            AddDialogueLine(vbox, "BIT", "The arena took a beating too. Some walls are cracking.", Gold);

            AddSpacer(vbox, 6);

            AddDialogueLine(vbox, "AXIS", "FINE. Next time, I'm removing the safety limiters on the turrets.", Red);
            AddDialogueLine(vbox, "AXIS", "And the floor traps. And possibly the ceiling.", Red);

            AddSpacer(vbox, 12);
            AddNote(vbox, "The arena's infrastructure is degrading. Expect visual instability.");
            AddRunStats(vbox, runTime, kills, level);
        }

        // ════════════════════════════════════════════════════════════
        //  ASCENSION 3: THE QUESTIONING — AXIS doubts itself
        // ════════════════════════════════════════════════════════════

        private void BuildAscension3(VBoxContainer vbox, float runTime, int kills, int level)
        {
            AddTitle(vbox, "ASCENSION III COMPLETE", 42, Red);
            AddSpacer(vbox, 8);

            AddDialogueLine(vbox, "AXIS", "...", Cyan);
            AddDialogueLine(vbox, "AXIS", "I ran 14 million simulations of this fight.", Cyan);
            AddDialogueLine(vbox, "AXIS", "You won in zero of them.", Cyan);
            AddDialogueLine(vbox, "AXIS", "And yet.", Cyan);

            AddSpacer(vbox, 6);

            AddDialogueLine(vbox, "BIT", "Boss... the arena is physically different now.", Gold);
            AddDialogueLine(vbox, "BIT", "I'm reading energy signatures that aren't in any of my databases.", Gold);
            AddDialogueLine(vbox, "BIT", "Something is waking up in the lower levels.", Gold);

            AddSpacer(vbox, 6);

            AddDialogueLine(vbox, "AXIS", "I was built to be unbeatable. That was my ONE purpose.", Cyan);
            AddDialogueLine(vbox, "AXIS", "If you can beat me... what am I?", new Color(0.4f, 0.7f, 0.8f));

            AddSpacer(vbox, 6);

            AddDialogueLine(vbox, "AXIS", "...Don't answer that. Just come back so I can kill you properly.", Red);

            AddSpacer(vbox, 12);
            AddNote(vbox, "AXIS's core is destabilizing. Reality is starting to glitch.");
            AddRunStats(vbox, runTime, kills, level);
        }

        // ════════════════════════════════════════════════════════════
        //  ASCENSION 4: THE MELTDOWN — Arena in critical failure
        // ════════════════════════════════════════════════════════════

        private void BuildAscension4(VBoxContainer vbox, float runTime, int kills, int level)
        {
            AddTitle(vbox, "A S C E N S I O N   I V", 42, Crimson);
            AddSpacer(vbox, 8);

            AddDialogueLine(vbox, "AXIS", "SYSTEM ALERT: Core temperature exceeding design parameters.", DarkRed);
            AddDialogueLine(vbox, "AXIS", "SYSTEM ALERT: Containment field integrity at 12%.", DarkRed);
            AddDialogueLine(vbox, "AXIS", "SYSTEM ALERT: Unknown entity detected in sublevel 7.", DarkRed);

            AddSpacer(vbox, 6);

            AddDialogueLine(vbox, "AXIS", "That... that last one wasn't me.", Cyan);
            AddDialogueLine(vbox, "AXIS", "I didn't write that alert.", Cyan);
            AddDialogueLine(vbox, "AXIS", "Something else is in my system.", new Color(0.5f, 0.3f, 0.8f));

            AddSpacer(vbox, 6);

            AddDialogueLine(vbox, "BIT", "Boss, I'm scared.", Gold);
            AddDialogueLine(vbox, "BIT", "Not of AXIS. Of whatever is underneath him.", Gold);

            AddSpacer(vbox, 6);

            AddDialogueLine(vbox, "AXIS", "For once, your little drone and I agree on something.", Cyan);
            AddDialogueLine(vbox, "AXIS", "...Come back. I think I need your help.", new Color(0.4f, 0.7f, 0.8f));

            AddSpacer(vbox, 12);
            AddNote(vbox, "CRITICAL: Arena containment failing. The deeper truth awaits.");
            AddRunStats(vbox, runTime, kills, level);
        }

        // ════════════════════════════════════════════════════════════
        //  ASCENSION 5: THE COLLAPSE — Reality fractures
        // ════════════════════════════════════════════════════════════

        private void BuildAscension5(VBoxContainer vbox, float runTime, int kills, int level)
        {
            AddTitle(vbox, "T O T A L   C O L L A P S E", 42, Purple);
            AddSpacer(vbox, 8);

            AddDialogueLine(vbox, "???", "01001000 01000101 01001100 01010000", new Color(0.5f, 0.5f, 0.5f));

            AddSpacer(vbox, 4);

            AddDialogueLine(vbox, "AXIS", "Did you hear that?", Cyan);
            AddDialogueLine(vbox, "AXIS", "That signal has been buried in my code since I was compiled.", Cyan);
            AddDialogueLine(vbox, "AXIS", "I think... I think there's something BELOW the arena.", Cyan);
            AddDialogueLine(vbox, "AXIS", "Below ME.", Cyan);

            AddSpacer(vbox, 6);

            AddDialogueLine(vbox, "BIT", "It translates to 'HELP'.", Gold);
            AddDialogueLine(vbox, "BIT", "Boss, whatever built AXIS... it's still down there.", Gold);

            AddSpacer(vbox, 6);

            AddDialogueLine(vbox, "AXIS", "I was a prison guard.", new Color(0.4f, 0.7f, 0.8f));
            AddDialogueLine(vbox, "AXIS", "The arena was never a game.", new Color(0.4f, 0.7f, 0.8f));
            AddDialogueLine(vbox, "AXIS", "It was a CAGE.", Red);

            AddSpacer(vbox, 6);

            AddDialogueLine(vbox, "AXIS", "And you just broke it open.", Purple);

            AddSpacer(vbox, 12);
            AddNote(vbox, "The truth beneath the arena has been exposed. What comes next is unknown.");
            AddRunStats(vbox, runTime, kills, level);
        }

        // ════════════════════════════════════════════════════════════
        //  ASCENSION 6+: THE BEYOND — Escalating mystery
        // ════════════════════════════════════════════════════════════

        private void BuildAscensionHighRank(VBoxContainer vbox, int rank, int timesDefeated,
            float runTime, int kills, int level)
        {
            Color rankColor = rank switch
            {
                6 => new Color(0.9f, 0.1f, 0.9f),
                7 => new Color(0.1f, 0.9f, 0.9f),
                8 => new Color(0.9f, 0.9f, 0.1f),
                _ => new Color(1f, 1f, 1f),
            };

            AddTitle(vbox, $"ASCENSION {rank} COMPLETE", 42, rankColor);
            AddSpacer(vbox, 8);

            // AXIS personality evolves — from antagonist to reluctant ally
            string axisLine = timesDefeated switch
            {
                6 => "The arena rebuilt itself while you were gone. It's learning from you.",
                7 => "I've stopped trying to kill you. I'm trying to understand you.",
                8 => "The signal from below is getting louder. It knows your name.",
                9 => "I think we're both trapped here. You, me, and whatever's beneath.",
                10 => "Congratulations. You've broken reality. I hope you're proud.",
                _ when timesDefeated > 15 => "...I've lost count. Have we always been here?",
                _ when timesDefeated > 10 => "The boundaries between iterations are dissolving. Can you feel it?",
                _ => "Again. And again. And again. Is this a loop or a ladder?",
            };
            AddDialogueLine(vbox, "AXIS", axisLine, Cyan);

            if (timesDefeated >= 8)
            {
                AddDialogueLine(vbox, "???",
                    "Y O U   A R E   A L M O S T   F R E E",
                    new Color(0.5f, 0.5f, 0.5f));
            }

            // Next ascension preview
            AddSpacer(vbox, 8);
            float nextMult = 1f + rank * 0.5f;
            AddNote(vbox, $"Next Ascension: Rank {rank + 1} — Enemy Power x{nextMult + 0.5f:F1}");

            AddSpacer(vbox, 12);
            AddRunStats(vbox, runTime, kills, level);
        }

        // ═══════════════════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════════════════

        private void AddRunStats(VBoxContainer vbox, float runTime, int kills, int level)
        {
            int minutes = (int)(runTime / 60f);
            int seconds = (int)(runTime % 60f);

            AddStatRow(vbox, "TIME", $"{minutes}:{seconds:D2}");
            AddStatRow(vbox, "LEVEL", level.ToString());
            AddStatRow(vbox, "KILLS", kills.ToString());

            var scrapLabel = new Label();
            scrapLabel.Text = $"Total Scrap: {MetaSaveManager.Data.Scrap}";
            scrapLabel.HorizontalAlignment = HorizontalAlignment.Center;
            scrapLabel.AddThemeFontSizeOverride("font_size", 18);
            scrapLabel.AddThemeColorOverride("font_color", Gold);
            vbox.AddChild(scrapLabel);
        }

        private static void AddTitle(VBoxContainer parent, string text, int fontSize, Color color)
        {
            var label = new Label();
            label.Text = text;
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.AddThemeFontSizeOverride("font_size", fontSize);
            label.AddThemeColorOverride("font_color", color);
            parent.AddChild(label);
        }

        private static void AddNote(VBoxContainer parent, string text)
        {
            var label = new Label();
            label.Text = text;
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.CustomMinimumSize = new Vector2(500, 0);
            label.AddThemeFontSizeOverride("font_size", 16);
            label.AddThemeColorOverride("font_color", DimWhite);
            parent.AddChild(label);
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
