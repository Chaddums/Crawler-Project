using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Full-screen overlay shown during sector transitions.
    /// Black fade-in, "ENTERING SECTOR X" title, hold, fade-out, then callback.
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

            _background = new ColorRect();
            _background.Color = new Color(0, 0, 0, 0);
            _background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            AddChild(_background);

            _title = new Label();
            _title.Text = StringLoader.Get("ui.sectorTransition.entering", ("{sector}", sectorNumber));
            _title.HorizontalAlignment = HorizontalAlignment.Center;
            _title.VerticalAlignment = VerticalAlignment.Center;
            _title.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _title.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f, 0f));
            _title.AddThemeFontSizeOverride("font_size", 48);
            _title.OffsetTop = -30;
            AddChild(_title);

            _subtitle = new Label();
            _subtitle.Text = StringLoader.Get("ui.sectorTransition.subtitle");
            _subtitle.HorizontalAlignment = HorizontalAlignment.Center;
            _subtitle.VerticalAlignment = VerticalAlignment.Center;
            _subtitle.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _subtitle.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f, 0f));
            _subtitle.AddThemeFontSizeOverride("font_size", 24);
            _subtitle.OffsetTop = 30;
            AddChild(_subtitle);
        }

        private void PlaySequence()
        {
            var tween = CreateTween();
            tween.SetParallel(false);

            // Fade in background
            tween.TweenProperty(_background, "color:a", 1f, FADE_IN_DURATION)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.In);

            // Fade in title text (gold)
            tween.TweenProperty(_title, "theme_override_colors/font_color:a", 1f, TEXT_FADE_DURATION)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);

            // Fade in subtitle (gray)
            tween.TweenProperty(_subtitle, "theme_override_colors/font_color:a", 1f, TEXT_FADE_DURATION * 0.5f)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);

            // Hold
            tween.TweenInterval(HOLD_DURATION);

            // Fade out everything
            tween.SetParallel(true);
            tween.TweenProperty(_background, "color:a", 0f, FADE_OUT_DURATION);
            tween.TweenProperty(_title, "theme_override_colors/font_color:a", 0f, FADE_OUT_DURATION);
            tween.TweenProperty(_subtitle, "theme_override_colors/font_color:a", 0f, FADE_OUT_DURATION);

            tween.SetParallel(false);
            tween.TweenCallback(_onComplete);
            tween.TweenCallback(Callable.From(QueueFree));
        }
    }
}
