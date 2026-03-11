using System;
using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Main inventory panel: bag grid + equipment slots + tooltip.
    /// Toggled via I key (GameEvents.OnInventoryToggled).
    /// </summary>
    public partial class InventoryUI : CanvasLayer
    {
        private Control _panel;
        private GridContainer _bagGrid;
        private VBoxContainer _equipmentColumn;
        private ItemTooltipUI _tooltip;
        private PopupMenu _contextMenu;

        private readonly List<ItemSlotUI> _bagSlots = new();
        private readonly Dictionary<EquipmentSlot, EquipmentSlotUI> _equipSlots = new();

        private ItemInstance _contextItem;
        private int _contextSlotIndex = -1;
        private bool _isEquipmentSlot;
        private bool _isOpen;

        private static readonly EquipmentSlot[] EquipmentSlotOrder =
        {
            EquipmentSlot.Head, EquipmentSlot.Amulet,
            EquipmentSlot.Chest, EquipmentSlot.Back,
            EquipmentSlot.Hands, EquipmentSlot.MainHand,
            EquipmentSlot.OffHand, EquipmentSlot.Ring1,
            EquipmentSlot.Legs, EquipmentSlot.Ring2,
            EquipmentSlot.Feet
        };

        public override void _Ready()
        {
            Layer = 20;
            ProcessMode = ProcessModeEnum.Always;
            BuildUI();

            GameEvents.OnInventoryToggled += ToggleInventory;

            _panel.Visible = false;
        }

        public override void _ExitTree()
        {
            GameEvents.OnInventoryToggled -= ToggleInventory;
        }

        private void BuildUI()
        {
            // Full-screen dim overlay
            var overlay = new ColorRect();
            overlay.Color = new Color(0, 0, 0, 0.4f);
            overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            overlay.MouseFilter = Control.MouseFilterEnum.Stop;
            overlay.GuiInput += (InputEvent ev) =>
            {
                if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                    Close();
            };

            _panel = new Control();
            _panel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(_panel);
            _panel.AddChild(overlay);

            // Main inventory container
            var mainPanel = new PanelContainer();
            mainPanel.Position = new Vector2(360, 140);
            mainPanel.Size = new Vector2(1200, 750);

            var mainStyle = new StyleBoxFlat();
            mainStyle.BgColor = new Color(0.06f, 0.06f, 0.1f, 0.97f);
            mainStyle.BorderColor = new Color(0.6f, 0.5f, 0.2f);
            mainStyle.BorderWidthBottom = 2;
            mainStyle.BorderWidthTop = 2;
            mainStyle.BorderWidthLeft = 2;
            mainStyle.BorderWidthRight = 2;
            mainStyle.CornerRadiusBottomLeft = 8;
            mainStyle.CornerRadiusBottomRight = 8;
            mainStyle.CornerRadiusTopLeft = 8;
            mainStyle.CornerRadiusTopRight = 8;
            mainStyle.ContentMarginLeft = 20;
            mainStyle.ContentMarginRight = 20;
            mainStyle.ContentMarginTop = 10;
            mainStyle.ContentMarginBottom = 10;
            mainPanel.AddThemeStyleboxOverride("panel", mainStyle);
            _panel.AddChild(mainPanel);

            var outerVBox = new VBoxContainer();
            outerVBox.AddThemeConstantOverride("separation", 8);
            mainPanel.AddChild(outerVBox);

            // Title bar
            var titleBar = new HBoxContainer();
            outerVBox.AddChild(titleBar);

            var titleLabel = new Label();
            titleLabel.Text = StringLoader.Get("ui.inventory.title");
            titleLabel.AddThemeFontSizeOverride("font_size", 28);
            titleLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.3f));
            titleLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            titleBar.AddChild(titleLabel);

            var closeBtn = new Button();
            closeBtn.Text = "X";
            closeBtn.CustomMinimumSize = new Vector2(40, 40);
            closeBtn.Pressed += Close;
            titleBar.AddChild(closeBtn);

            // HSplit: equipment left, bag right
            var hSplit = new HBoxContainer();
            hSplit.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            hSplit.AddThemeConstantOverride("separation", 30);
            outerVBox.AddChild(hSplit);

            // Equipment side
            var equipVBox = new VBoxContainer();
            equipVBox.CustomMinimumSize = new Vector2(340, 0);
            equipVBox.AddThemeConstantOverride("separation", 4);
            hSplit.AddChild(equipVBox);

            var equipTitle = new Label();
            equipTitle.Text = StringLoader.Get("ui.inventory.equipment");
            equipTitle.AddThemeFontSizeOverride("font_size", 20);
            equipTitle.AddThemeColorOverride("font_color", new Color(0.7f, 0.6f, 0.3f));
            equipVBox.AddChild(equipTitle);

            _equipmentColumn = new VBoxContainer();
            _equipmentColumn.AddThemeConstantOverride("separation", 4);
            equipVBox.AddChild(_equipmentColumn);

            // Equipment slots in a grid
            var equipGrid = new GridContainer();
            equipGrid.Columns = 2;
            equipGrid.AddThemeConstantOverride("h_separation", 8);
            equipGrid.AddThemeConstantOverride("v_separation", 4);
            _equipmentColumn.AddChild(equipGrid);

            foreach (var slot in EquipmentSlotOrder)
            {
                var slotUI = new EquipmentSlotUI();
                slotUI.Initialize(slot);
                equipGrid.AddChild(slotUI);
                _equipSlots[slot] = slotUI;

                // Hover for tooltip
                slotUI.MouseEntered += () =>
                {
                    if (slotUI.Item != null)
                        _tooltip.ShowItem(slotUI.Item, slotUI.GlobalPosition);
                };
                slotUI.MouseExited += () => _tooltip.Hide();

                // Right-click for context menu, double-click to unequip
                slotUI.GuiInput += (InputEvent ev) =>
                {
                    if (ev is InputEventMouseButton mb && mb.Pressed)
                    {
                        if (mb.ButtonIndex == MouseButton.Right && slotUI.Item != null)
                            ShowEquipmentContextMenu(slotUI.Slot, slotUI.Item, mb.GlobalPosition);
                        else if (mb.ButtonIndex == MouseButton.Left && mb.DoubleClick && slotUI.Item != null)
                            QuickUnequip(slotUI.Slot);
                    }
                };
            }

            // Separator
            var sep = new VSeparator();
            hSplit.AddChild(sep);

            // Bag side
            var bagVBox = new VBoxContainer();
            bagVBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            bagVBox.AddThemeConstantOverride("separation", 8);
            hSplit.AddChild(bagVBox);

            var bagTitle = new Label();
            bagTitle.Text = StringLoader.Get("ui.inventory.bag");
            bagTitle.AddThemeFontSizeOverride("font_size", 20);
            bagTitle.AddThemeColorOverride("font_color", new Color(0.7f, 0.6f, 0.3f));
            bagVBox.AddChild(bagTitle);

            var bagScroll = new ScrollContainer();
            bagScroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            bagScroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            bagScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            bagVBox.AddChild(bagScroll);

            _bagGrid = new GridContainer();
            _bagGrid.Columns = 6;
            _bagGrid.AddThemeConstantOverride("h_separation", 4);
            _bagGrid.AddThemeConstantOverride("v_separation", 4);
            _bagGrid.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            bagScroll.AddChild(_bagGrid);

            // Initial slots built during first RefreshAll
            // (slot count adapts to actual inventory size)

            // Tooltip
            _tooltip = new ItemTooltipUI();
            _panel.AddChild(_tooltip);

            // Context menu
            _contextMenu = new PopupMenu();
            _contextMenu.IdPressed += HandleContextMenuAction;
            _panel.AddChild(_contextMenu);
        }

        private void ToggleInventory()
        {
            if (_isOpen)
                Close();
            else
                Open();
        }

        private void Open()
        {
            _isOpen = true;
            _panel.Visible = true;
            GetTree().Paused = true;
            RefreshAll();
        }

        private void Close()
        {
            _isOpen = false;
            _panel.Visible = false;
            _tooltip.Hide();
            GetViewport().GuiReleaseFocus();
            GetTree().Paused = false;
        }

        private void RefreshAll()
        {
            if (!ServiceLocator.TryGet<PlayerController>(out var player)) return;

            var inventory = player.Inventory;
            _tooltip.SetInventory(inventory);

            // Ensure enough slots exist (minimum DEFAULT_INVENTORY_SIZE, grow as needed)
            int needed = Math.Max(Constants.DEFAULT_INVENTORY_SIZE, inventory.Items.Count);
            while (_bagSlots.Count < needed)
            {
                int idx = _bagSlots.Count;
                var slotUI = new ItemSlotUI();
                slotUI.Initialize(idx);
                _bagGrid.AddChild(slotUI);
                _bagSlots.Add(slotUI);

                slotUI.MouseEntered += () =>
                {
                    if (slotUI.Item != null)
                        _tooltip.ShowItem(slotUI.Item, slotUI.GlobalPosition);
                };
                slotUI.MouseExited += () => _tooltip.Hide();

                slotUI.GuiInput += (InputEvent ev) =>
                {
                    if (ev is InputEventMouseButton mb && mb.Pressed)
                    {
                        if (mb.ButtonIndex == MouseButton.Right && slotUI.Item != null)
                            ShowBagContextMenu(idx, slotUI.Item, mb.GlobalPosition);
                        else if (mb.ButtonIndex == MouseButton.Left && mb.DoubleClick && slotUI.Item != null)
                            QuickUseItem(slotUI.Item);
                    }
                };
            }

            // Refresh bag
            for (int i = 0; i < _bagSlots.Count; i++)
            {
                var item = i < inventory.Items.Count ? inventory.Items[i] : null;
                _bagSlots[i].SetItem(item);
            }

            // Refresh equipment
            foreach (var (slot, slotUI) in _equipSlots)
            {
                inventory.Equipped.TryGetValue(slot, out var equipped);
                slotUI.SetItem(equipped);
            }
        }

        private void ShowBagContextMenu(int slotIndex, ItemInstance item, Vector2 pos)
        {
            _contextItem = item;
            _contextSlotIndex = slotIndex;
            _isEquipmentSlot = false;

            _contextMenu.Clear();

            if (item.BaseData is EquipmentData equipData)
            {
                if (equipData.Slot == EquipmentSlot.Ring1)
                {
                    // Show ring slot options
                    _contextMenu.AddItem("Equip (Ring 1)", 0);
                    _contextMenu.AddItem("Equip (Ring 2)", 5);
                }
                else
                {
                    _contextMenu.AddItem("Equip", 0);
                }
            }

            if (item.BaseData is ConsumableData)
                _contextMenu.AddItem("Use", 1);

            if (item.BaseData is LootBoxData)
            {
                bool inSafeRoom = GameManager.Instance?.CurrentState == GameState.SafeRoom;
                if (inSafeRoom)
                {
                    _contextMenu.AddItem("Open", 4);
                }
                else
                {
                    _contextMenu.AddItem("Open (Safe Room only)", 4);
                    int idx = _contextMenu.GetItemIndex(4);
                    _contextMenu.SetItemDisabled(idx, true);
                }
            }

            if (item.BaseData is EquipmentData)
            {
                int yield = CraftingSystem.SalvageYield(item.Rarity);
                _contextMenu.AddItem($"Salvage (+{yield} ⚙)", 6);
            }

            _contextMenu.AddItem("Discard", 2);

            _contextMenu.Position = new Vector2I((int)pos.X, (int)pos.Y);
            _contextMenu.Popup();
        }

        private void ShowEquipmentContextMenu(EquipmentSlot slot, ItemInstance item, Vector2 pos)
        {
            _contextItem = item;
            _contextSlotIndex = (int)slot;
            _isEquipmentSlot = true;

            _contextMenu.Clear();
            _contextMenu.AddItem("Unequip", 3);

            _contextMenu.Position = new Vector2I((int)pos.X, (int)pos.Y);
            _contextMenu.Popup();
        }

        /// <summary>
        /// Double-click a bag item: equip equipment, use consumables.
        /// </summary>
        private void QuickUseItem(ItemInstance item)
        {
            if (!ServiceLocator.TryGet<PlayerController>(out var player)) return;

            if (item.BaseData is EquipmentData)
            {
                player.Inventory.Equip(item);
            }
            else if (item.BaseData is ConsumableData)
            {
                player.Inventory.UseConsumable(item);
            }

            _tooltip.Hide();
            RefreshAll();
        }

        /// <summary>
        /// Double-click an equipped item to unequip it back to bag.
        /// </summary>
        private void QuickUnequip(EquipmentSlot slot)
        {
            if (!ServiceLocator.TryGet<PlayerController>(out var player)) return;

            player.Inventory.Unequip(slot);
            _tooltip.Hide();
            RefreshAll();
        }

        private void HandleContextMenuAction(long id)
        {
            if (_contextItem == null) return;
            if (!ServiceLocator.TryGet<PlayerController>(out var player)) return;

            switch (id)
            {
                case 0: // Equip (or Equip Ring 1)
                    player.Inventory.Equip(_contextItem);
                    break;
                case 5: // Equip Ring 2
                    player.Inventory.Equip(_contextItem, EquipmentSlot.Ring2);
                    break;
                case 1: // Use consumable
                    player.Inventory.UseConsumable(_contextItem);
                    break;
                case 2: // Discard
                    player.Inventory.RemoveItem(_contextItem);
                    break;
                case 3: // Unequip
                    if (_isEquipmentSlot)
                        player.Inventory.Unequip((EquipmentSlot)_contextSlotIndex);
                    break;
                case 4: // Open loot box
                    if (_contextItem?.BaseData is LootBoxData lootBoxData)
                    {
                        player.Inventory.RemoveItem(_contextItem);
                        var ceremony = new LootBoxCeremonyUI();
                        GetTree().Root.AddChild(ceremony);
                        ceremony.StartCeremony(lootBoxData);
                    }
                    break;
                case 6: // Salvage equipment
                    if (_contextItem?.BaseData is EquipmentData)
                        CraftingSystem.SalvageItem(_contextItem, player.Inventory);
                    break;
            }

            _contextItem = null;
            _tooltip.Hide();
            RefreshAll();
            GetViewport().GuiReleaseFocus();
        }

        public override void _Process(double delta)
        {
            if (!_isOpen) return;

            // Update tooltip position to follow mouse
            if (_tooltip.Visible)
            {
                var mousePos = _panel.GetViewport().GetMousePosition();
                _tooltip.Position = mousePos + new Vector2(16, 16);
            }
        }
    }
}
