using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// HUD toast bar that displays AXIS/BIT commentary and system messages.
    /// Shows at bottom-center, above the XP bar, with speaker-colored styling.
    /// Queued entries fade in/out automatically based on display duration.
    /// </summary>
    public partial class CommentaryToastUI : Control
    {
        private PanelContainer _panel;
        private HBoxContainer _hbox;
        private Label _speakerLabel;
        private Label _textLabel;
        private float _displayTimer;
        private float _fadeTimer;
        private bool _showing;

        private readonly Queue<(string speaker, string text, float duration)> _pendingQueue = new();

        private const float FADE_DURATION = 0.35f;
        private const float MIN_DISPLAY = 2f;
        private const float MAX_DISPLAY = 8f;

        // Speaker colors
        private static readonly Color AxisColor = new(0.9f, 0.25f, 0.2f);
        private static readonly Color BitColor = new(0.3f, 0.75f, 0.95f);
        private static readonly Color SystemColor = new(0.85f, 0.75f, 0.3f);
        private static readonly Color DefaultColor = new(0.7f, 0.7f, 0.7f);

        public override void _Ready()
        {
            // Anchor bottom-center, above XP bar
            AnchorLeft = 0.5f;
            AnchorRight = 0.5f;
            AnchorTop = 1f;
            AnchorBottom = 1f;

            float width = UIConfigLoader.GetFloat("HUD", "CommentaryToast", "Width", 700f);
            float bottomOffset = UIConfigLoader.GetFloat("HUD", "CommentaryToast", "BottomOffset", 50f);

            OffsetLeft = -width / 2f;
            OffsetRight = width / 2f;
            OffsetTop = -(bottomOffset + 50f);
            OffsetBottom = -bottomOffset;
            MouseFilter = MouseFilterEnum.Ignore;

            BuildUI();

            // Start hidden
            _panel.Modulate = new Color(1, 1, 1, 0);
            _panel.Visible = false;

            GameEvents.OnCommentaryTriggered += OnCommentaryTriggered;
            GameEvents.OnSystemMessage += OnSystemMessage;
        }

        private void BuildUI()
        {
            _panel = new PanelContainer();
            _panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _panel.MouseFilter = MouseFilterEnum.Ignore;

            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.04f, 0.04f, 0.06f, 0.85f);
            style.BorderColor = new Color(0.3f, 0.3f, 0.35f, 0.5f);
            style.BorderWidthLeft = 2;
            style.BorderWidthRight = 2;
            style.BorderWidthTop = 1;
            style.BorderWidthBottom = 1;
            style.ContentMarginLeft = 14;
            style.ContentMarginRight = 14;
            style.ContentMarginTop = 6;
            style.ContentMarginBottom = 6;
            style.CornerRadiusBottomLeft = 0;
            style.CornerRadiusBottomRight = 0;
            style.CornerRadiusTopLeft = 0;
            style.CornerRadiusTopRight = 0;
            _panel.AddThemeStyleboxOverride("panel", style);
            AddChild(_panel);

            _hbox = new HBoxContainer();
            _hbox.AddThemeConstantOverride("separation", 10);
            _hbox.MouseFilter = MouseFilterEnum.Ignore;
            _panel.AddChild(_hbox);

            int speakerFontSize = UIConfigLoader.GetInt("HUD", "CommentaryToast", "SpeakerFontSize", 14);
            int textFontSize = UIConfigLoader.GetInt("HUD", "CommentaryToast", "TextFontSize", 13);

            _speakerLabel = new Label();
            _speakerLabel.AddThemeFontSizeOverride("font_size", speakerFontSize);
            _speakerLabel.MouseFilter = MouseFilterEnum.Ignore;
            _hbox.AddChild(_speakerLabel);

            _textLabel = new Label();
            _textLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _textLabel.AddThemeFontSizeOverride("font_size", textFontSize);
            _textLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.88f));
            _textLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _textLabel.MouseFilter = MouseFilterEnum.Ignore;
            _hbox.AddChild(_textLabel);
        }

        private void OnCommentaryTriggered(CommentaryEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Text)) return;
            float dur = Mathf.Clamp(entry.GetDisplayDuration(), MIN_DISPLAY, MAX_DISPLAY);
            EnqueueToast(entry.Speaker ?? "???", entry.Text, dur);
        }

        private void OnSystemMessage(string category, string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            string speaker = category switch
            {
                "Sector" => "AXIS",
                "Boss" => "AXIS",
                "Purge" => "AXIS",
                "Warning" => "AXIS",
                _ => "SYSTEM"
            };
            float dur = Mathf.Clamp(MIN_DISPLAY + message.Length * 0.05f, MIN_DISPLAY, MAX_DISPLAY);
            EnqueueToast(speaker, message, dur);
        }

        private void EnqueueToast(string speaker, string text, float duration)
        {
            if (_showing)
            {
                // If something's already showing, queue the new one
                _pendingQueue.Enqueue((speaker, text, duration));
                return;
            }
            ShowToast(speaker, text, duration);
        }

        private void ShowToast(string speaker, string text, float duration)
        {
            _showing = true;
            _displayTimer = duration;
            _fadeTimer = FADE_DURATION;

            Color speakerColor = speaker.ToUpperInvariant() switch
            {
                "AXIS" => AxisColor,
                "BIT" => BitColor,
                "SYSTEM" => SystemColor,
                _ => DefaultColor
            };

            _speakerLabel.Text = $"[{speaker}]";
            _speakerLabel.AddThemeColorOverride("font_color", speakerColor);
            _textLabel.Text = text;

            // Update border accent to match speaker
            if (_panel.GetThemeStylebox("panel") is StyleBoxFlat s)
            {
                s.BorderColor = new Color(speakerColor.R, speakerColor.G, speakerColor.B, 0.5f);
            }

            _panel.Visible = true;
            _panel.Modulate = new Color(1, 1, 1, 0);
        }

        public override void _Process(double delta)
        {
            if (!_showing) return;

            float dt = (float)delta;

            // Fade in
            if (_fadeTimer > 0 && _displayTimer > 0)
            {
                _fadeTimer -= dt;
                float alpha = 1f - Mathf.Clamp(_fadeTimer / FADE_DURATION, 0f, 1f);
                _panel.Modulate = new Color(1, 1, 1, alpha);
                return;
            }

            // Display countdown
            if (_displayTimer > 0)
            {
                _displayTimer -= dt;
                _panel.Modulate = new Color(1, 1, 1, 1);
                return;
            }

            // Fade out
            if (_panel.Modulate.A > 0.01f)
            {
                float a = _panel.Modulate.A - dt / FADE_DURATION;
                _panel.Modulate = new Color(1, 1, 1, Mathf.Max(a, 0f));
                return;
            }

            // Done — check for queued entries
            _panel.Visible = false;
            _showing = false;

            if (_pendingQueue.Count > 0)
            {
                var (speaker, text, duration) = _pendingQueue.Dequeue();
                ShowToast(speaker, text, duration);
            }
        }

        public override void _ExitTree()
        {
            GameEvents.OnCommentaryTriggered -= OnCommentaryTriggered;
            GameEvents.OnSystemMessage -= OnSystemMessage;
        }
    }
}
