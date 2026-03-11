using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Tooltip that follows the mouse, showing item details.
    /// When showing a bag equipment item, also displays a comparison against
    /// the currently equipped item in the same slot.
    /// </summary>
    public partial class ItemTooltipUI : PanelContainer
    {
        private Label _nameLabel;
        private Label _typeLabel;
        private Label _statsLabel;
        private Label _affixLabel;
        private Label _descLabel;
        private HSeparator _compareSep;
        private Label _compareHeader;
        private Label _compareLabel;
        private Label _verdictLabel;

        private PlayerInventory _inventory;

        private static readonly Color Green = new(0.3f, 0.9f, 0.3f);
        private static readonly Color Red = new(0.9f, 0.3f, 0.3f);
        private static readonly Color DimText = new(0.5f, 0.5f, 0.55f);

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

            CustomMinimumSize = new Vector2(260, 60);

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

            // --- Comparison section ---
            _compareSep = new HSeparator();
            _compareSep.Visible = false;
            vbox.AddChild(_compareSep);

            _compareHeader = new Label();
            _compareHeader.AddThemeFontSizeOverride("font_size", 13);
            _compareHeader.AddThemeColorOverride("font_color", DimText);
            _compareHeader.Visible = false;
            vbox.AddChild(_compareHeader);

            _compareLabel = new Label();
            _compareLabel.AddThemeFontSizeOverride("font_size", 13);
            _compareLabel.AutowrapMode = TextServer.AutowrapMode.Word;
            _compareLabel.Visible = false;
            vbox.AddChild(_compareLabel);

            _verdictLabel = new Label();
            _verdictLabel.AddThemeFontSizeOverride("font_size", 14);
            _verdictLabel.Visible = false;
            vbox.AddChild(_verdictLabel);

            Visible = false;
            MouseFilter = MouseFilterEnum.Ignore;
            ZIndex = 100;
        }

        /// <summary>
        /// Set the player inventory reference for equipment comparison.
        /// </summary>
        public void SetInventory(PlayerInventory inventory)
        {
            _inventory = inventory;
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
                _typeLabel.Text = $"{FormatSlotName(equip.Slot)} - {item.Rarity}";
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
                        statsText += $"{sign}{mod.Value * 100:F0}% {FormatStatName(mod.StatType)}\n";
                    else
                        statsText += $"{sign}{mod.Value:F0} {FormatStatName(mod.StatType)}\n";
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
                    affixText += $"{sign}{affix.RolledValue * 100:F0}% {FormatStatName(affix.Data.Stat)}\n";
                else
                {
                    string fmt = Mathf.Abs(affix.RolledValue) < 1f ? "F2" : "F0";
                    affixText += $"{sign}{affix.RolledValue.ToString(fmt)} {FormatStatName(affix.Data.Stat)}\n";
                }
            }
            _affixLabel.Text = affixText.TrimEnd('\n');
            _affixLabel.Visible = !string.IsNullOrEmpty(affixText);

            // Description
            _descLabel.Text = item.BaseData.Description;
            _descLabel.Visible = !string.IsNullOrEmpty(item.BaseData.Description);

            // --- Comparison ---
            BuildComparison(item);

            // Position near mouse, clamped to viewport
            var viewport = GetViewport().GetVisibleRect().Size;
            float x = Mathf.Min(mousePos.X + 16, viewport.X - Size.X - 10);
            float y = Mathf.Min(mousePos.Y + 16, viewport.Y - Size.Y - 10);
            Position = new Vector2(x, y);

            Visible = true;
        }

        private void BuildComparison(ItemInstance newItem)
        {
            // Only compare equipment when we have an inventory reference
            if (_inventory == null || newItem.BaseData is not EquipmentData newEquip)
            {
                HideComparison();
                return;
            }

            // Find the currently equipped item in the same slot
            EquipmentSlot slot = newEquip.Slot;

            // Handle ring slots: compare against whichever ring is equipped (prefer Ring1)
            if (slot == EquipmentSlot.Ring1 || slot == EquipmentSlot.Ring2)
            {
                if (!_inventory.Equipped.ContainsKey(EquipmentSlot.Ring1) &&
                    !_inventory.Equipped.ContainsKey(EquipmentSlot.Ring2))
                {
                    ShowEmptySlotComparison(slot);
                    return;
                }
                // Compare against the weaker ring
                slot = _inventory.Equipped.ContainsKey(EquipmentSlot.Ring1)
                    ? EquipmentSlot.Ring1 : EquipmentSlot.Ring2;
            }

            if (!_inventory.Equipped.TryGetValue(slot, out var equippedItem))
            {
                ShowEmptySlotComparison(slot);
                return;
            }

            // Don't compare an item against itself
            if (equippedItem.UniqueId == newItem.UniqueId)
            {
                HideComparison();
                return;
            }

            // Gather all modifiers from both items
            var newMods = newItem.GetAllModifiers();
            var oldMods = equippedItem.GetAllModifiers();

            // Build stat delta map: (StatType, ModType) → (newVal, oldVal)
            var deltas = new Dictionary<(StatType, ModifierType), (float newVal, float oldVal)>();

            foreach (var mod in newMods)
            {
                var key = (mod.StatType, mod.ModType);
                if (!deltas.ContainsKey(key))
                    deltas[key] = (0, 0);
                var (n, o) = deltas[key];
                deltas[key] = (n + mod.Value, o);
            }

            foreach (var mod in oldMods)
            {
                var key = (mod.StatType, mod.ModType);
                if (!deltas.ContainsKey(key))
                    deltas[key] = (0, 0);
                var (n, o) = deltas[key];
                deltas[key] = (n, o + mod.Value);
            }

            // Build comparison text
            string compareText = "";
            int upgrades = 0;
            int downgrades = 0;

            // Sort by stat type for consistent display
            foreach (var kvp in deltas.OrderBy(k => k.Key.Item1))
            {
                var (statType, modType) = kvp.Key;
                float diff = kvp.Value.newVal - kvp.Value.oldVal;
                if (Mathf.Abs(diff) < 0.001f) continue;

                bool isPercent = modType == ModifierType.Percent;
                float displayDiff = isPercent ? diff * 100f : diff;
                string fmt = Mathf.Abs(displayDiff) < 1f ? "F2" : "F0";
                string sign = displayDiff > 0 ? "+" : "";
                string suffix = isPercent ? "%" : "";
                string statName = FormatStatName(statType);

                compareText += $"{sign}{displayDiff.ToString(fmt)}{suffix} {statName}\n";

                if (diff > 0) upgrades++;
                else downgrades++;
            }

            if (string.IsNullOrEmpty(compareText))
            {
                // Items are identical stat-wise
                _compareSep.Visible = true;
                _compareHeader.Text = $"vs equipped {FormatSlotName(slot)}";
                _compareHeader.Visible = true;
                _compareLabel.Text = "Identical stats";
                _compareLabel.AddThemeColorOverride("font_color", DimText);
                _compareLabel.Visible = true;
                _verdictLabel.Visible = false;
                return;
            }

            _compareSep.Visible = true;
            _compareHeader.Text = $"vs {equippedItem.GetDisplayName()}";
            _compareHeader.Visible = true;

            // Use RichTextLabel-style coloring via the label
            // Since Label doesn't support inline colors, we use the overall color
            // based on whether it's mostly an upgrade or downgrade
            _compareLabel.Text = compareText.TrimEnd('\n');
            _compareLabel.Visible = true;

            if (upgrades > 0 && downgrades == 0)
                _compareLabel.AddThemeColorOverride("font_color", Green);
            else if (downgrades > 0 && upgrades == 0)
                _compareLabel.AddThemeColorOverride("font_color", Red);
            else
                _compareLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.5f)); // Mixed: yellow

            // Verdict
            _verdictLabel.Visible = true;
            if (upgrades > downgrades)
            {
                _verdictLabel.Text = ">> Upgrade";
                _verdictLabel.AddThemeColorOverride("font_color", Green);
            }
            else if (downgrades > upgrades)
            {
                _verdictLabel.Text = "<< Downgrade";
                _verdictLabel.AddThemeColorOverride("font_color", Red);
            }
            else if (upgrades > 0)
            {
                _verdictLabel.Text = "~~ Sidegrade";
                _verdictLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.5f));
            }
            else
            {
                _verdictLabel.Visible = false;
            }
        }

        private void ShowEmptySlotComparison(EquipmentSlot slot)
        {
            _compareSep.Visible = true;
            _compareHeader.Text = $"{FormatSlotName(slot)} slot is empty";
            _compareHeader.Visible = true;
            _compareLabel.Visible = false;
            _verdictLabel.Text = ">> Equip it!";
            _verdictLabel.AddThemeColorOverride("font_color", Green);
            _verdictLabel.Visible = true;
        }

        private void HideComparison()
        {
            _compareSep.Visible = false;
            _compareHeader.Visible = false;
            _compareLabel.Visible = false;
            _verdictLabel.Visible = false;
        }

        public new void Hide()
        {
            Visible = false;
        }

        private static string FormatSlotName(EquipmentSlot slot) => slot switch
        {
            EquipmentSlot.MainHand => "Main Hand",
            EquipmentSlot.OffHand => "Off Hand",
            EquipmentSlot.Ring1 => "Ring",
            EquipmentSlot.Ring2 => "Ring",
            _ => slot.ToString()
        };

        private static string FormatStatName(StatType stat)
        {
            var name = stat.ToString();
            var result = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]))
                    result.Append(' ');
                result.Append(name[i]);
            }
            return result.ToString();
        }
    }
}
