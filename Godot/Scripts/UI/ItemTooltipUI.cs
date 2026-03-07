using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Tooltip that follows the mouse, showing item details.
    /// </summary>
    public partial class ItemTooltipUI : PanelContainer
    {
        private Label _nameLabel;
        private Label _typeLabel;
        private Label _statsLabel;
        private Label _affixLabel;
        private Label _descLabel;

        public override void _Ready()
        {
            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.05f, 0.05f, 0.1f, 0.95f);
            style.BorderColor = new Color(0.5f, 0.45f, 0.2f);
            style.BorderWidthBottom = 2;
            style.BorderWidthTop = 2;
            style.BorderWidthLeft = 2;
            style.BorderWidthRight = 2;
            style.CornerRadiusBottomLeft = 4;
            style.CornerRadiusBottomRight = 4;
            style.CornerRadiusTopLeft = 4;
            style.CornerRadiusTopRight = 4;
            style.ContentMarginLeft = 12;
            style.ContentMarginRight = 12;
            style.ContentMarginTop = 8;
            style.ContentMarginBottom = 8;
            AddThemeStyleboxOverride("panel", style);

            CustomMinimumSize = new Vector2(250, 60);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 4);
            AddChild(vbox);

            _nameLabel = new Label();
            _nameLabel.AddThemeFontSizeOverride("font_size", 18);
            vbox.AddChild(_nameLabel);

            _typeLabel = new Label();
            _typeLabel.AddThemeFontSizeOverride("font_size", 13);
            _typeLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            vbox.AddChild(_typeLabel);

            _statsLabel = new Label();
            _statsLabel.AddThemeFontSizeOverride("font_size", 14);
            _statsLabel.AutowrapMode = TextServer.AutowrapMode.Word;
            vbox.AddChild(_statsLabel);

            _affixLabel = new Label();
            _affixLabel.AddThemeFontSizeOverride("font_size", 14);
            _affixLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.7f, 1f));
            _affixLabel.AutowrapMode = TextServer.AutowrapMode.Word;
            vbox.AddChild(_affixLabel);

            _descLabel = new Label();
            _descLabel.AddThemeFontSizeOverride("font_size", 12);
            _descLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            _descLabel.AutowrapMode = TextServer.AutowrapMode.Word;
            vbox.AddChild(_descLabel);

            Visible = false;
            MouseFilter = MouseFilterEnum.Ignore;
            ZIndex = 100;
        }

        public void ShowItem(ItemInstance item, Vector2 mousePos)
        {
            if (item == null)
            {
                Visible = false;
                return;
            }

            _nameLabel.Text = item.GetDisplayName();
            _nameLabel.AddThemeColorOverride("font_color", ItemSlotUI.GetRarityColor(item.Rarity));

            // Type line
            if (item.BaseData is EquipmentData equip)
                _typeLabel.Text = $"{equip.Slot} - {item.Rarity}";
            else if (item.BaseData is ConsumableData)
                _typeLabel.Text = $"Consumable - {item.Rarity}";
            else
                _typeLabel.Text = item.Rarity.ToString();

            // Base stats
            var statsText = "";
            if (item.BaseData is EquipmentData equipment)
            {
                foreach (var mod in equipment.BaseStatBonuses)
                {
                    string sign = mod.Value >= 0 ? "+" : "";
                    if (mod.ModType == ModifierType.Percent)
                        statsText += $"{sign}{mod.Value * 100:F0}% {mod.StatType}\n";
                    else
                        statsText += $"{sign}{mod.Value:F0} {mod.StatType}\n";
                }
            }

            if (item.BaseData is ConsumableData cons)
            {
                if (cons.HealAmount > 0) statsText += $"Heals {cons.HealAmount} HP\n";
                if (cons.ManaRestoreAmount > 0) statsText += $"Restores {cons.ManaRestoreAmount} Mana\n";
            }
            _statsLabel.Text = statsText.TrimEnd('\n');
            _statsLabel.Visible = !string.IsNullOrEmpty(statsText);

            // Affixes
            var affixText = "";
            foreach (var affix in item.Affixes)
            {
                string sign = affix.RolledValue >= 0 ? "+" : "";
                if (affix.Data.ModType == ModifierType.Percent)
                    affixText += $"{sign}{affix.RolledValue * 100:F0}% {affix.Data.Stat}\n";
                else
                    affixText += $"{sign}{affix.RolledValue:F0} {affix.Data.Stat}\n";
            }
            _affixLabel.Text = affixText.TrimEnd('\n');
            _affixLabel.Visible = !string.IsNullOrEmpty(affixText);

            // Description
            _descLabel.Text = item.BaseData.Description;
            _descLabel.Visible = !string.IsNullOrEmpty(item.BaseData.Description);

            // Position near mouse, clamped to viewport
            var viewport = GetViewport().GetVisibleRect().Size;
            float x = Mathf.Min(mousePos.X + 16, viewport.X - Size.X - 10);
            float y = Mathf.Min(mousePos.Y + 16, viewport.Y - Size.Y - 10);
            Position = new Vector2(x, y);

            Visible = true;
        }

        public new void Hide()
        {
            Visible = false;
        }
    }
}
