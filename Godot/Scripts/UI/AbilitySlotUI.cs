using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Single ability slot widget: icon, cooldown overlay, hotkey label.
    /// </summary>
    public partial class AbilitySlotUI : Control
    {
        private ColorRect _background;
        private TextureRect _icon;
        private ColorRect _cooldownOverlay;
        private Label _cooldownLabel;
        private Label _hotkeyLabel;

        private int _slotIndex;

        public void Initialize(int slotIndex)
        {
            _slotIndex = slotIndex;
            CustomMinimumSize = new Vector2(64, 64);
            Size = new Vector2(64, 64);

            // Background
            _background = new ColorRect();
            _background.Color = new Color(0.1f, 0.1f, 0.15f, 0.85f);
            _background.Size = new Vector2(64, 64);
            AddChild(_background);

            // Border
            var border = new ReferenceRect();
            border.EditorOnly = false;
            border.Size = new Vector2(64, 64);
            border.BorderColor = new Color(0.5f, 0.45f, 0.2f);
            border.BorderWidth = 2f;
            AddChild(border);

            // Icon placeholder
            _icon = new TextureRect();
            _icon.Position = new Vector2(4, 4);
            _icon.Size = new Vector2(56, 56);
            _icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            _icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            _icon.Visible = false;
            AddChild(_icon);

            // Cooldown overlay
            _cooldownOverlay = new ColorRect();
            _cooldownOverlay.Color = new Color(0, 0, 0, 0.6f);
            _cooldownOverlay.Position = new Vector2(0, 0);
            _cooldownOverlay.Size = new Vector2(64, 64);
            _cooldownOverlay.Visible = false;
            AddChild(_cooldownOverlay);

            // Cooldown text
            _cooldownLabel = new Label();
            _cooldownLabel.Position = new Vector2(0, 18);
            _cooldownLabel.Size = new Vector2(64, 28);
            _cooldownLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _cooldownLabel.AddThemeFontSizeOverride("font_size", 18);
            _cooldownLabel.Visible = false;
            AddChild(_cooldownLabel);

            // Hotkey label
            _hotkeyLabel = new Label();
            _hotkeyLabel.Text = (slotIndex + 1).ToString();
            _hotkeyLabel.Position = new Vector2(2, 46);
            _hotkeyLabel.Size = new Vector2(20, 18);
            _hotkeyLabel.AddThemeFontSizeOverride("font_size", 12);
            _hotkeyLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.5f));
            AddChild(_hotkeyLabel);
        }

        public void UpdateSlot(AbilitySlot slot)
        {
            if (slot == null || slot.IsEmpty)
            {
                _icon.Visible = false;
                _cooldownOverlay.Visible = false;
                _cooldownLabel.Visible = false;
                _background.Color = new Color(0.1f, 0.1f, 0.15f, 0.85f);
                return;
            }

            // AbilityData has no icon texture — just show it as non-empty
            _icon.Visible = false;

            // Cooldown
            if (slot.CooldownRemaining > 0)
            {
                float ratio = slot.CooldownRemaining / slot.Data.Cooldown;
                _cooldownOverlay.Visible = true;
                _cooldownOverlay.Size = new Vector2(64, 64 * ratio);
                _cooldownOverlay.Position = new Vector2(0, 64 * (1f - ratio));

                _cooldownLabel.Visible = true;
                _cooldownLabel.Text = $"{slot.CooldownRemaining:F1}";

                _background.Color = new Color(0.08f, 0.08f, 0.12f, 0.85f);
            }
            else
            {
                _cooldownOverlay.Visible = false;
                _cooldownLabel.Visible = false;
                _background.Color = new Color(0.12f, 0.12f, 0.2f, 0.85f);
            }
        }
    }
}
