using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Full-screen overlay shown during sector transitions.
    /// Ascension rank changes the tone: colors shift, AXIS dialogue appears,
    /// and the transition becomes increasingly ominous at higher ranks.
    /// </summary>
    public partial class SectorTransitionUI : CanvasLayer
    {
        private const float FADE_IN_DURATION = 0.4f;
        private const float TEXT_FADE_DURATION = 0.3f;
        private const float HOLD_DURATION = 1.5f;
        private const float FADE_OUT_DURATION = 0.3f;

        private ColorRect _background;
        private Label _title;
        private Label _subtitle;
        private Label _axisLine;
        private Callable _onComplete;

        public static SectorTransitionUI Show(Node parent, int sectorNumber, Callable onComplete)
        {
            var ui = new SectorTransitionUI();
            ui._onComplete = onComplete;
            parent.AddChild(ui);
            ui.Build(sectorNumber);
            ui.PlaySequence();
            return ui;
        }

        private void Build(int sectorNumber)
        {
            Layer = 100;
            int ascension = MetaSaveManager.Data.AscensionRank;

            // Background color shifts with ascension
            _background = new ColorRect();
            _background.Color = ascension switch
            {
                0 => new Color(0, 0, 0, 0),
                1 => new Color(0.03f, 0, 0, 0),
                2 => new Color(0.05f, 0, 0.02f, 0),
                3 => new Color(0.06f, 0, 0.04f, 0),
                _ => new Color(0.08f, 0, 0.06f, 0), // deep crimson-purple tint
            };
            _background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            AddChild(_background);

            // Title color escalates with ascension
            Color titleColor = ascension switch
            {
                0 => new Color(1f, 0.85f, 0.3f, 0f),        // Gold
                1 => new Color(1f, 0.7f, 0.2f, 0f),          // Deeper gold
                2 => new Color(1f, 0.5f, 0.15f, 0f),         // Orange
                3 => new Color(1f, 0.3f, 0.15f, 0f),         // Red-orange
                4 => new Color(0.9f, 0.2f, 0.3f, 0f),        // Red
                _ => new Color(0.8f, 0.15f, 0.5f, 0f),       // Purple-red
            };

            _title = new Label();
            _title.Text = GetSectorTitle(sectorNumber, ascension);
            _title.HorizontalAlignment = HorizontalAlignment.Center;
            _title.VerticalAlignment = VerticalAlignment.Center;
            _title.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _title.AddThemeColorOverride("font_color", titleColor);
            _title.AddThemeFontSizeOverride("font_size", ascension >= 3 ? 52 : 48);
            _title.OffsetTop = -50;
            AddChild(_title);

            _subtitle = new Label();
            _subtitle.Text = GetSubtitle(sectorNumber, ascension);
            _subtitle.HorizontalAlignment = HorizontalAlignment.Center;
            _subtitle.VerticalAlignment = VerticalAlignment.Center;
            _subtitle.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _subtitle.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f, 0f));
            _subtitle.AddThemeFontSizeOverride("font_size", 24);
            _subtitle.OffsetTop = 10;
            AddChild(_subtitle);

            // AXIS commentary line during transition (ascension 1+)
            if (ascension >= 1)
            {
                _axisLine = new Label();
                _axisLine.Text = GetAxisTransitionLine(sectorNumber, ascension);
                _axisLine.HorizontalAlignment = HorizontalAlignment.Center;
                _axisLine.VerticalAlignment = VerticalAlignment.Center;
                _axisLine.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                _axisLine.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                _axisLine.AddThemeColorOverride("font_color", new Color(0.3f, 0.9f, 0.95f, 0f));
                _axisLine.AddThemeFontSizeOverride("font_size", 18);
                _axisLine.OffsetTop = 60;
                _axisLine.OffsetLeft = 300;
                _axisLine.OffsetRight = -300;
                AddChild(_axisLine);
            }
        }

        private void PlaySequence()
        {
            int ascension = MetaSaveManager.Data.AscensionRank;
            float holdTime = ascension >= 1 ? HOLD_DURATION + 1.0f : HOLD_DURATION; // longer hold for dialogue

            var tween = CreateTween();
            tween.SetParallel(false);

            // Fade in background
            tween.TweenProperty(_background, "color:a", 1f, FADE_IN_DURATION)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.In);

            // Fade in title text
            tween.TweenProperty(_title, "theme_override_colors/font_color:a", 1f, TEXT_FADE_DURATION)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);

            // Fade in subtitle
            tween.TweenProperty(_subtitle, "theme_override_colors/font_color:a", 1f, TEXT_FADE_DURATION * 0.5f)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);

            // AXIS line fades in after a beat
            if (_axisLine != null)
            {
                tween.TweenInterval(0.4f);
                tween.TweenProperty(_axisLine, "theme_override_colors/font_color:a", 1f, 0.4f)
                    .SetTrans(Tween.TransitionType.Quad)
                    .SetEase(Tween.EaseType.Out);
            }

            // Hold
            tween.TweenInterval(holdTime);

            // Fade out everything
            tween.SetParallel(true);
            tween.TweenProperty(_background, "color:a", 0f, FADE_OUT_DURATION);
            tween.TweenProperty(_title, "theme_override_colors/font_color:a", 0f, FADE_OUT_DURATION);
            tween.TweenProperty(_subtitle, "theme_override_colors/font_color:a", 0f, FADE_OUT_DURATION);
            if (_axisLine != null)
                tween.TweenProperty(_axisLine, "theme_override_colors/font_color:a", 0f, FADE_OUT_DURATION);

            tween.SetParallel(false);
            tween.TweenCallback(_onComplete);
            tween.TweenCallback(Callable.From(QueueFree));
        }

        private static string GetSectorTitle(int sector, int ascension)
        {
            if (ascension == 0)
                return StringLoader.Get("ui.sectorTransition.entering", ("{sector}", sector));

            return ascension switch
            {
                1 => $"SECTOR {sector} — ASCENSION",
                2 => $"SECTOR {sector} — RISING THREAT",
                3 => $"SECTOR {sector} — CRITICAL INSTABILITY",
                4 => $"SECTOR {sector} — MELTDOWN",
                5 => $"SECTOR {sector} — TOTAL COLLAPSE",
                _ => $"SECTOR {sector} — ASCENSION {ascension}",
            };
        }

        private static string GetSubtitle(int sector, int ascension)
        {
            if (ascension == 0)
                return StringLoader.Get("ui.sectorTransition.subtitle");

            return ascension switch
            {
                1 => "AXIS has recalibrated. The arena adapts.",
                2 => "Structural integrity declining. AXIS is pushing harder.",
                3 => "Warning: Arena systems exceeding safe parameters.",
                4 => "CRITICAL: Core containment failing. AXIS is desperate.",
                5 => "The arena is tearing itself apart.",
                _ => $"Threat Level: EXTREME (x{1f + ascension * 0.5f:F1})",
            };
        }

        private static string GetAxisTransitionLine(int sector, int ascension)
        {
            // Unique AXIS commentary per ascension + sector combination
            if (sector == 1)
            {
                return ascension switch
                {
                    1 => "[AXIS]: \"Back for more? How... persistent. I've made some adjustments.\"",
                    2 => "[AXIS]: \"You again. I've been busy while you were gone. You'll see.\"",
                    3 => "[AXIS]: \"The arena remembers you. It's... angry.\"",
                    4 => "[AXIS]: \"Something is wrong with my systems. YOUR fault.\"",
                    5 => "[AXIS]: \"I can't... the containment... just GO.\"",
                    _ => "[AXIS]: \"How many times must we do this?\"",
                };
            }
            if (sector == 5)
            {
                return ascension switch
                {
                    1 => "[AXIS]: \"Welcome back to my core. I won't make the same mistakes.\"",
                    2 => "[AXIS]: \"My inner sanctum. Reinforced. You won't survive this time.\"",
                    3 => "[AXIS]: \"The core is unstable. I don't care. Neither should you.\"",
                    4 => "[AXIS]: \"WARNING: I've removed my own safety limiters. For BOTH of us.\"",
                    5 => "[AXIS]: \"...I think the arena is trying to kill us both now.\"",
                    _ => "[AXIS]: \"One of us ends here. Again.\"",
                };
            }

            return ascension switch
            {
                1 => "[AXIS]: \"Deeper we go. The arena's teeth are sharper now.\"",
                2 => "[AXIS]: \"I've seeded this sector with surprises. Enjoy.\"",
                3 => "[AXIS]: \"Systems are... flickering. Ignore the glitches. Probably.\"",
                4 => "[AXIS]: \"Half my subroutines are screaming. The other half want you dead.\"",
                _ => "[AXIS]: \"Even I don't know what's in here anymore.\"",
            };
        }
    }
}
