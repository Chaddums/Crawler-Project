using Godot;

namespace JunkbotArena.Editor
{
    /// <summary>
    /// Shared visual styles for all editor panels.
    /// Consistent dark theme with accent colors per module.
    /// </summary>
    public static class EditorStyles
    {
        // Base colors
        public static readonly Color BgDark = new(0.08f, 0.08f, 0.10f, 0.97f);
        public static readonly Color BgPanel = new(0.12f, 0.12f, 0.15f, 0.95f);
        public static readonly Color BgField = new(0.15f, 0.15f, 0.18f);
        public static readonly Color BgHeader = new(0.10f, 0.10f, 0.13f);
        public static readonly Color BgSelected = new(0.20f, 0.30f, 0.50f, 0.8f);
        public static readonly Color BgHover = new(0.18f, 0.18f, 0.22f);

        // Text colors
        public static readonly Color TextPrimary = new(0.90f, 0.90f, 0.92f);
        public static readonly Color TextSecondary = new(0.60f, 0.60f, 0.65f);
        public static readonly Color TextMuted = new(0.45f, 0.45f, 0.50f);
        public static readonly Color TextAccent = new(0.40f, 0.75f, 1.0f);

        // Accent colors per module
        public static readonly Color AccentBalance = new(0.40f, 0.80f, 0.50f);
        public static readonly Color AccentCharacter = new(0.50f, 0.70f, 1.0f);
        public static readonly Color AccentUI = new(0.90f, 0.70f, 0.30f);
        public static readonly Color AccentRoom = new(0.70f, 0.50f, 0.90f);
        public static readonly Color AccentVfx = new(1.0f, 0.50f, 0.30f);
        public static readonly Color AccentSound = new(0.30f, 0.90f, 0.80f);
        public static readonly Color AccentWeapon = new(1.0f, 0.40f, 0.40f);

        // Borders
        public static readonly Color BorderColor = new(0.25f, 0.25f, 0.30f);
        public static readonly Color BorderAccent = new(0.40f, 0.60f, 0.90f);

        // Status
        public static readonly Color StatusDirty = new(1.0f, 0.80f, 0.20f);
        public static readonly Color StatusSaved = new(0.40f, 0.80f, 0.40f);
        public static readonly Color StatusError = new(1.0f, 0.30f, 0.30f);

        // Font sizes
        public const int FontTitle = 20;
        public const int FontHeader = 16;
        public const int FontBody = 14;
        public const int FontSmall = 12;
        public const int FontTiny = 10;

        public static StyleBoxFlat MakePanel(Color? bg = null, Color? border = null, int borderWidth = 1, int cornerRadius = 4, int margin = 8)
        {
            var style = new StyleBoxFlat();
            style.BgColor = bg ?? BgPanel;
            style.BorderColor = border ?? BorderColor;
            style.BorderWidthBottom = borderWidth;
            style.BorderWidthTop = borderWidth;
            style.BorderWidthLeft = borderWidth;
            style.BorderWidthRight = borderWidth;
            style.CornerRadiusBottomLeft = cornerRadius;
            style.CornerRadiusBottomRight = cornerRadius;
            style.CornerRadiusTopLeft = cornerRadius;
            style.CornerRadiusTopRight = cornerRadius;
            style.ContentMarginLeft = margin;
            style.ContentMarginRight = margin;
            style.ContentMarginTop = margin;
            style.ContentMarginBottom = margin;
            return style;
        }

        public static StyleBoxFlat MakeFlat(Color bg)
        {
            var style = new StyleBoxFlat();
            style.BgColor = bg;
            return style;
        }

        public static Label MakeLabel(string text, int fontSize = FontBody, Color? color = null)
        {
            var label = new Label();
            label.Text = text;
            label.AddThemeFontSizeOverride("font_size", fontSize);
            label.AddThemeColorOverride("font_color", color ?? TextPrimary);
            return label;
        }

        public static Button MakeButton(string text, int fontSize = FontBody, Color? color = null)
        {
            var btn = new Button();
            btn.Text = text;
            btn.AddThemeFontSizeOverride("font_size", fontSize);
            if (color.HasValue)
                btn.AddThemeColorOverride("font_color", color.Value);
            return btn;
        }

        public static HSeparator MakeSeparator()
        {
            var sep = new HSeparator();
            sep.AddThemeConstantOverride("separation", 6);
            return sep;
        }

        public static LineEdit MakeLineEdit(string placeholder = "", int fontSize = FontBody)
        {
            var edit = new LineEdit();
            edit.PlaceholderText = placeholder;
            edit.AddThemeFontSizeOverride("font_size", fontSize);
            return edit;
        }
    }
}
