using System;
using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Popup panel for selecting a graft to socket into a CoreSocket node.
    /// Shows available salvage cores from the player's inventory.
    /// </summary>
    public partial class GraftSocketPickerUI : PanelContainer
    {
        private VBoxContainer _listBox;
        private Label _titleLabel;
        private string _targetNodeId;
        private Action _onChanged;

        private static readonly Dictionary<SalvageCoreRarity, Color> RarityColors = new()
        {
            { SalvageCoreRarity.Rare, new Color(0.4f, 0.8f, 0.9f) },
            { SalvageCoreRarity.Epic, new Color(0.8f, 0.5f, 0.9f) },
            { SalvageCoreRarity.Legendary, new Color(1f, 0.6f, 0.2f) },
            { SalvageCoreRarity.Mythic, new Color(1f, 0.85f, 0.3f) },
        };

        public override void _Ready()
        {
            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.06f, 0.06f, 0.12f, 0.97f);
            style.BorderColor = new Color(0.4f, 0.8f, 0.9f);
            style.BorderWidthBottom = 2;
            style.BorderWidthTop = 2;
            style.BorderWidthLeft = 2;
            style.BorderWidthRight = 2;
            style.CornerRadiusBottomLeft = 6;
            style.CornerRadiusBottomRight = 6;
            style.CornerRadiusTopLeft = 6;
            style.CornerRadiusTopRight = 6;
            style.ContentMarginLeft = 10;
            style.ContentMarginRight = 10;
            style.ContentMarginTop = 8;
            style.ContentMarginBottom = 8;
            AddThemeStyleboxOverride("panel", style);

            CustomMinimumSize = new Vector2(300, 60);
            MouseFilter = MouseFilterEnum.Stop;
            ZIndex = 110;

            var outer = new VBoxContainer();
            outer.AddThemeConstantOverride("separation", 6);
            AddChild(outer);

            // Title
            _titleLabel = new Label();
            _titleLabel.AddThemeFontSizeOverride("font_size", 16);
            _titleLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.8f, 0.9f));
            outer.AddChild(_titleLabel);

            // Scrollable list
            var scroll = new ScrollContainer();
            scroll.CustomMinimumSize = new Vector2(280, 200);
            outer.AddChild(scroll);

            _listBox = new VBoxContainer();
            _listBox.AddThemeConstantOverride("separation", 4);
            _listBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            scroll.AddChild(_listBox);

            // Close button
            var closeBtn = new Button();
            closeBtn.Text = "Cancel";
            closeBtn.CustomMinimumSize = new Vector2(80, 30);
            closeBtn.Pressed += () => Visible = false;
            outer.AddChild(closeBtn);

            Visible = false;
        }

        /// <summary>
        /// Open the picker for a specific CoreSocket node.
        /// </summary>
        public void Show(string nodeId, Vector2 screenPos, Action onChanged)
        {
            _targetNodeId = nodeId;
            _onChanged = onChanged;

            if (!ServiceLocator.TryGet<PlayerController>(out var player)) return;
            var passiveTree = player.ClassController?.PassiveTree;
            if (passiveTree == null) return;

            var treeData = PassiveTreeBuilder.Tree;
            var nodeData = treeData?.GetNode(nodeId);
            if (nodeData == null) return;

            // Build title
            _titleLabel.Text = nodeData.SocketedCore != null
                ? $"Socket: {nodeData.SocketedCore.CoreName}"
                : "Select Graft";

            // Clear old entries
            foreach (var child in _listBox.GetChildren())
                if (child is Node n) n.QueueFree();

            // Unsocket option if something is socketed
            if (nodeData.SocketedCore != null)
            {
                var unsocketBtn = new Button();
                unsocketBtn.Text = $"Remove: {nodeData.SocketedCore.CoreName}";
                unsocketBtn.AddThemeFontSizeOverride("font_size", 14);
                unsocketBtn.AddThemeColorOverride("font_color", new Color(0.9f, 0.4f, 0.4f));
                unsocketBtn.Pressed += () => HandleUnsocket(player, passiveTree, nodeId);
                _listBox.AddChild(unsocketBtn);

                var sep = new HSeparator();
                _listBox.AddChild(sep);
            }

            // Gather available cores from inventory
            var availableCores = new List<(ItemInstance Item, SalvageCoreData Core)>();
            foreach (var item in player.Inventory.Items)
            {
                if (item.BaseData is SalvageCoreItemData coreItem && coreItem.CoreData != null)
                    availableCores.Add((item, coreItem.CoreData));
            }

            if (availableCores.Count == 0)
            {
                var emptyLabel = new Label();
                emptyLabel.Text = "No grafts in inventory";
                emptyLabel.AddThemeFontSizeOverride("font_size", 13);
                emptyLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
                _listBox.AddChild(emptyLabel);
            }
            else
            {
                // Sort by rarity descending
                availableCores.Sort((a, b) => b.Core.Rarity.CompareTo(a.Core.Rarity));

                foreach (var (item, core) in availableCores)
                {
                    var entryBox = new VBoxContainer();
                    entryBox.AddThemeConstantOverride("separation", 1);

                    var btn = new Button();
                    btn.Text = core.CoreName;
                    btn.AddThemeFontSizeOverride("font_size", 14);
                    var rarityColor = RarityColors.GetValueOrDefault(core.Rarity, new Color(0.8f, 0.8f, 0.8f));
                    btn.AddThemeColorOverride("font_color", rarityColor);
                    btn.Pressed += () => HandleSocket(player, passiveTree, nodeId, item, core);
                    entryBox.AddChild(btn);

                    var rarityLabel = new Label();
                    rarityLabel.Text = $"  {core.Rarity} — {TruncateDesc(core.Description, 60)}";
                    rarityLabel.AddThemeFontSizeOverride("font_size", 11);
                    rarityLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.55f, 0.6f));
                    rarityLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                    rarityLabel.CustomMinimumSize = new Vector2(260, 0);
                    entryBox.AddChild(rarityLabel);

                    _listBox.AddChild(entryBox);
                }
            }

            // Position — clamp to viewport
            var vpSize = GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920, 1080);
            var pos = screenPos + new Vector2(10, -20);
            if (pos.X + 320 > vpSize.X - 10)
                pos.X = screenPos.X - 320;
            if (pos.Y + 300 > vpSize.Y - 10)
                pos.Y = vpSize.Y - 300;
            Position = pos;
            Visible = true;
        }

        private void HandleSocket(PlayerController player, PassiveTree tree, string nodeId,
            ItemInstance item, SalvageCoreData core)
        {
            // Remove item from inventory
            player.Inventory.RemoveItem(item);

            // Socket the core
            tree.SocketCore(nodeId, core, player.Stats.Stats);
            GameEvents.OnGraftSocketed?.Invoke(core.Id);

            GD.Print($"[GraftSocketPicker] Socketed {core.CoreName} into {nodeId}");

            Visible = false;
            _onChanged?.Invoke();
        }

        private void HandleUnsocket(PlayerController player, PassiveTree tree, string nodeId)
        {
            var removed = tree.UnsocketCore(nodeId, player.Stats.Stats);
            if (removed != null)
            {
                // Return core to inventory as an item
                var coreItemData = new SalvageCoreItemData(removed);
                var instance = new ItemInstance(coreItemData, coreItemData.Rarity);
                player.Inventory.TryAddItem(instance);

                GameEvents.OnGraftUnsocketed?.Invoke(removed.Id);
                GD.Print($"[GraftSocketPicker] Unsocketed {removed.CoreName} from {nodeId}");
            }

            Visible = false;
            _onChanged?.Invoke();
        }

        private static string TruncateDesc(string desc, int maxLen)
        {
            if (string.IsNullOrEmpty(desc)) return "";
            // Take first line only
            int newline = desc.IndexOf('\n');
            string firstLine = newline >= 0 ? desc[..newline] : desc;
            return firstLine.Length <= maxLen ? firstLine : firstLine[..(maxLen - 3)] + "...";
        }
    }
}
