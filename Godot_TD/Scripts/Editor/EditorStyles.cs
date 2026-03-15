using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Shared theme for the in-game editor. Dark cohesive look.
    /// </summary>
    public static class EditorStyles
    {
        // Backgrounds
        public static readonly Color BgDark = new(0.08f, 0.08f, 0.10f, 0.97f);
        public static readonly Color BgPanel = new(0.12f, 0.12f, 0.15f, 0.95f);
        public static readonly Color BgField = new(0.15f, 0.15f, 0.18f);
        public static readonly Color BgHeader = new(0.10f, 0.10f, 0.13f);
        public static readonly Color BgRow = new(0.13f, 0.13f, 0.16f);
        public static readonly Color BgRowAlt = new(0.11f, 0.11f, 0.14f);
        public static readonly Color BgRowSelected = new(0.20f, 0.22f, 0.30f);

        // Borders
        public static readonly Color Border = new(0.25f, 0.25f, 0.30f);
        public static readonly Color BorderFocus = new(0.4f, 0.6f, 0.8f);

        // Text
        public static readonly Color TextPrimary = new(0.85f, 0.85f, 0.85f);
        public static readonly Color TextSecondary = new(0.55f, 0.55f, 0.55f);
        public static readonly Color TextMuted = new(0.4f, 0.4f, 0.4f);

        // Accents per module
        public static readonly Color AccentNodes = new(0.4f, 0.8f, 0.5f);
        public static readonly Color AccentWaves = new(0.8f, 0.5f, 0.3f);
        public static readonly Color AccentSignals = new(0.5f, 0.7f, 1.0f);
        public static readonly Color AccentMap = new(0.7f, 0.5f, 0.9f);

        // Status
        public static readonly Color StatusOk = new(0.3f, 0.8f, 0.3f);
        public static readonly Color StatusWarn = new(0.9f, 0.7f, 0.2f);
        public static readonly Color StatusError = new(0.9f, 0.3f, 0.2f);

        public static Label MakeLabel(string text, int fontSize = 14, Color? color = null)
        {
            var label = new Label();
            label.Text = text;
            label.AddThemeFontSizeOverride("font_size", fontSize);
            label.AddThemeColorOverride("font_color", color ?? TextPrimary);
            return label;
        }

        public static Button MakeButton(string text, int fontSize = 14, Color? color = null)
        {
            var btn = new Button();
            btn.Text = text;
            btn.AddThemeFontSizeOverride("font_size", fontSize);
            if (color.HasValue)
                btn.AddThemeColorOverride("font_color", color.Value);
            return btn;
        }

        public static LineEdit MakeLineEdit(string placeholder = "", int fontSize = 14)
        {
            var edit = new LineEdit();
            edit.PlaceholderText = placeholder;
            edit.AddThemeFontSizeOverride("font_size", fontSize);

            var style = new StyleBoxFlat();
            style.BgColor = BgField;
            style.BorderColor = Border;
            style.SetBorderWidthAll(1);
            style.SetCornerRadiusAll(3);
            style.ContentMarginLeft = 6;
            style.ContentMarginRight = 6;
            edit.AddThemeStyleboxOverride("normal", style);

            return edit;
        }

        public static SpinBox MakeSpinBox(float value, float min, float max, float step = 0.1f)
        {
            var spin = new SpinBox();
            spin.Value = value;
            spin.MinValue = min;
            spin.MaxValue = max;
            spin.Step = step;
            spin.CustomMinimumSize = new Vector2(100, 0);
            return spin;
        }

        public static StyleBoxFlat MakePanel(Color? bg = null, Color? border = null)
        {
            var style = new StyleBoxFlat();
            style.BgColor = bg ?? BgPanel;
            if (border.HasValue)
            {
                style.BorderColor = border.Value;
                style.SetBorderWidthAll(1);
            }
            style.SetCornerRadiusAll(4);
            style.ContentMarginLeft = 8;
            style.ContentMarginRight = 8;
            style.ContentMarginTop = 6;
            style.ContentMarginBottom = 6;
            return style;
        }

        public static HSeparator MakeSeparator()
        {
            var sep = new HSeparator();
            var style = new StyleBoxFlat();
            style.BgColor = Border;
            style.ContentMarginTop = 1;
            style.ContentMarginBottom = 1;
            sep.AddThemeStyleboxOverride("separator", style);
            return sep;
        }
    }
}
