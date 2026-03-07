using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Single inventory bag slot: icon, stack count, rarity-colored border.
    /// </summary>
    public partial class ItemSlotUI : Control
    {
        private ColorRect _background;
        private ColorRect _border;
        private TextureRect _icon;
        private Label _stackLabel;
        private Label _nameLabel;
        private int _slotIndex;
        private ItemInstance _item;

        public ItemInstance Item => _item;

        public void Initialize(int slotIndex)
        {
            _slotIndex = slotIndex;
            CustomMinimumSize = new Vector2(64, 64);
            Size = new Vector2(64, 64);
            MouseFilter = MouseFilterEnum.Stop;

            // Background
            _background = new ColorRect();
            _background.Color = new Color(0.08f, 0.08f, 0.12f, 0.9f);
            _background.Size = new Vector2(64, 64);
            _background.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_background);

            // Border
            _border = new ColorRect();
            _border.Color = new Color(0.3f, 0.3f, 0.35f, 0.8f);
            _border.Size = new Vector2(64, 64);
            _border.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_border);

            var inner = new ColorRect();
            inner.Color = new Color(0.08f, 0.08f, 0.12f, 0.9f);
            inner.Position = new Vector2(2, 2);
            inner.Size = new Vector2(60, 60);
            inner.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(inner);

            // Icon
            _icon = new TextureRect();
            _icon.Position = new Vector2(4, 4);
            _icon.Size = new Vector2(56, 56);
            _icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            _icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            _icon.Visible = false;
            _icon.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_icon);

            // Name placeholder (shows when no icon)
            _nameLabel = new Label();
            _nameLabel.Position = new Vector2(2, 16);
            _nameLabel.Size = new Vector2(60, 32);
            _nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _nameLabel.AddThemeFontSizeOverride("font_size", 10);
            _nameLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _nameLabel.Visible = false;
            _nameLabel.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_nameLabel);

            // Stack count
            _stackLabel = new Label();
            _stackLabel.Position = new Vector2(40, 46);
            _stackLabel.Size = new Vector2(22, 16);
            _stackLabel.HorizontalAlignment = HorizontalAlignment.Right;
            _stackLabel.AddThemeFontSizeOverride("font_size", 12);
            _stackLabel.Visible = false;
            _stackLabel.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_stackLabel);
        }

        public void SetItem(ItemInstance item)
        {
            _item = item;

            if (item == null)
            {
                _icon.Visible = false;
                _stackLabel.Visible = false;
                _nameLabel.Visible = false;
                _border.Color = new Color(0.3f, 0.3f, 0.35f, 0.8f);
                return;
            }

            // Icon
            if (item.BaseData.Icon != null)
            {
                _icon.Texture = item.BaseData.Icon;
                _icon.Visible = true;
                _nameLabel.Visible = false;
            }
            else
            {
                _icon.Visible = false;
                _nameLabel.Text = item.BaseData.ItemName;
                _nameLabel.Visible = true;
            }

            // Stack count
            if (item.StackCount > 1)
            {
                _stackLabel.Text = item.StackCount.ToString();
                _stackLabel.Visible = true;
            }
            else
            {
                _stackLabel.Visible = false;
            }

            // Rarity border color
            _border.Color = GetRarityColor(item.Rarity);
        }

        public static Color GetRarityColor(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Common => new Color(0.7f, 0.7f, 0.7f),
            ItemRarity.Uncommon => new Color(0, 1, 0),
            ItemRarity.Rare => new Color(0.27f, 0.53f, 1f),
            ItemRarity.Epic => new Color(0.67f, 0.27f, 1f),
            ItemRarity.Legendary => new Color(1f, 0.53f, 0f),
            ItemRarity.Absurd => new Color(0.8f, 0.8f, 0.2f),
            _ => new Color(0.5f, 0.5f, 0.5f),
        };
    }
}
