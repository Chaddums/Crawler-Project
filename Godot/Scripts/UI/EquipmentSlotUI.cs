using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Single equipment slot widget: slot label, icon, item display.
    /// </summary>
    public partial class EquipmentSlotUI : Control
    {
        private ColorRect _background;
        private ColorRect _border;
        private TextureRect _icon;
        private Label _slotLabel;
        private Label _nameLabel;
        private EquipmentSlot _slot;
        private ItemInstance _item;

        public EquipmentSlot Slot => _slot;
        public ItemInstance Item => _item;

        public void Initialize(EquipmentSlot slot)
        {
            _slot = slot;
            CustomMinimumSize = new Vector2(80, 80);
            Size = new Vector2(80, 80);
            MouseFilter = MouseFilterEnum.Stop;

            // Background
            _background = new ColorRect();
            _background.Color = new Color(0.06f, 0.06f, 0.1f, 0.9f);
            _background.Size = new Vector2(80, 80);
            _background.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_background);

            // Border
            _border = new ColorRect();
            _border.Color = new Color(0.4f, 0.35f, 0.15f, 0.8f);
            _border.Size = new Vector2(80, 80);
            _border.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_border);

            var inner = new ColorRect();
            inner.Color = new Color(0.06f, 0.06f, 0.1f, 0.9f);
            inner.Position = new Vector2(2, 2);
            inner.Size = new Vector2(76, 76);
            inner.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(inner);

            // Slot label
            _slotLabel = new Label();
            _slotLabel.Text = FormatSlotName(slot);
            _slotLabel.Position = new Vector2(0, 2);
            _slotLabel.Size = new Vector2(80, 18);
            _slotLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _slotLabel.AddThemeFontSizeOverride("font_size", 10);
            _slotLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.45f, 0.2f));
            _slotLabel.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_slotLabel);

            // Icon
            _icon = new TextureRect();
            _icon.Position = new Vector2(12, 20);
            _icon.Size = new Vector2(56, 56);
            _icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            _icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            _icon.Visible = false;
            _icon.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_icon);

            // Name label (when no icon)
            _nameLabel = new Label();
            _nameLabel.Position = new Vector2(4, 28);
            _nameLabel.Size = new Vector2(72, 44);
            _nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _nameLabel.VerticalAlignment = VerticalAlignment.Center;
            _nameLabel.AddThemeFontSizeOverride("font_size", 10);
            _nameLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _nameLabel.Visible = false;
            _nameLabel.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_nameLabel);
        }

        public void SetItem(ItemInstance item)
        {
            _item = item;

            if (item == null)
            {
                _icon.Visible = false;
                _nameLabel.Visible = false;
                _border.Color = new Color(0.4f, 0.35f, 0.15f, 0.8f);
                return;
            }

            var tex = item.BaseData.Icon ?? ItemSlotUI.ResolveIcon(item);
            if (tex != null)
            {
                _icon.Texture = tex;
                _icon.Visible = true;
                _nameLabel.Visible = false;
            }
            else
            {
                _icon.Visible = false;
                _nameLabel.Text = item.GetDisplayName();
                _nameLabel.Visible = true;
            }

            _border.Color = ItemSlotUI.GetRarityColor(item.Rarity);
        }

        private static string FormatSlotName(EquipmentSlot slot) => slot switch
        {
            EquipmentSlot.MainHand => "Main",
            EquipmentSlot.OffHand => "Off",
            EquipmentSlot.Ring1 => "Ring 1",
            EquipmentSlot.Ring2 => "Ring 2",
            _ => slot.ToString()
        };
    }
}
