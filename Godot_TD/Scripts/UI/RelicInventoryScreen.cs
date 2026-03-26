using Godot;
using Godot.Collections;

namespace JunkyardTD
{
    /// <summary>
    /// Relic inventory screen — renders Stitch UI via godot-cef.
    /// Pushes relic data from RelicRegistry + RelicManager into the HTML grid.
    /// Falls back to native Godot UI if CEF is unavailable.
    /// </summary>
    public partial class RelicInventoryScreen : Control
    {
        private GodotObject _cefTexture;
        private string _selectedRelicId;
        private RelicManager _relicManager;

        // Fallback UI refs
        private Label _fbDetailName;
        private Label _fbDetailDesc;
        private Label _fbDetailTradeoff;
        private Button _fbEquipBtn;
        private Label _fbCountLabel;
        private Label _fbEquippedCount;

        public override void _Ready()
        {
            ServiceLocator.TryGet<RelicManager>(out _relicManager);

            if (CefHelper.Available)
                CreateCefBrowser();
            else
                BuildFallbackUI();
        }

        public override void _ExitTree()
        {
            CleanupCef();
        }

        private void CleanupCef()
        {
            if (_cefTexture == null) return;
            try { _cefTexture.Set("url", "about:blank"); } catch { /* ignore */ }
            if (_cefTexture is Node cefNode && IsInstanceValid(cefNode))
                cefNode.QueueFree();
            _cefTexture = null;
        }

        // ── CEF-based UI ──

        private void CreateCefBrowser()
        {
            _cefTexture = ClassDB.Instantiate("CefTexture").AsGodotObject();

            if (_cefTexture is not Control cefControl)
            {
                BuildFallbackUI();
                return;
            }

            cefControl.SetAnchorsPreset(LayoutPreset.FullRect);
            cefControl.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            cefControl.SizeFlagsVertical = SizeFlags.ExpandFill;

            _cefTexture.Set("background_color", new Color(0.055f, 0.055f, 0.055f, 1f));
            _cefTexture.Set("enable_accelerated_osr", true);

            _cefTexture.Connect("load_finished", Callable.From<string, int>(OnPageLoaded));
            _cefTexture.Connect("load_error", Callable.From<string, int, string>(OnPageError));
            _cefTexture.Connect("ipc_data_message", Callable.From<Variant>(OnIpcData));
            _cefTexture.Connect("console_message", Callable.From<int, string, string, int>(OnConsoleMessage));

            AddChild(cefControl);
            _cefTexture.Set("url", "res://ui/relic-inventory/index.html");
        }

        private void OnPageLoaded(string url, int httpStatus)
        {
            GD.Print($"[RelicInventory] Loaded: {url} (HTTP {httpStatus})");

            string bridgeJs = @"
                if (!window.__stitchBridge) {
                    window.__stitchBridge = {
                        sendAction: function(action, data) {
                            window.sendIpcData({ action: action, data: data || {} });
                        }
                    };
                }
            ";
            _cefTexture.Call("eval", bridgeJs);
            PushRelicData();
        }

        private void PushRelicData()
        {
            var relics = RelicRegistry.All;
            int owned = _relicManager?.OwnedCount ?? 0;
            int total = relics.Length;

            _cefTexture.Call("eval", $"window.__relicUI.setCount({owned}, {total})");
            _cefTexture.Call("eval", "window.__relicUI.clearGrid()");

            foreach (var relic in relics)
            {
                string name = EscapeJs(relic.Name);
                string desc = EscapeJs(relic.Desc);
                bool hasTradeoff = relic.Tradeoff != null;
                bool locked = _relicManager != null && !_relicManager.OwnsRelic(relic.Id);
                bool equipped = _relicManager?.IsEquipped(relic.Id) ?? false;

                _cefTexture.Call("eval",
                    $"window.__relicUI.addRelic('{relic.Id}', '{name}', '{relic.Icon}', '{relic.Rarity}', '{desc}', {BoolJs(hasTradeoff)}, {BoolJs(locked)}, {BoolJs(equipped)})");
            }

            // Push equipped count
            int equippedCount = _relicManager?.EquippedCount ?? 0;
            int maxEquip = _relicManager?.MaxEquipSlots ?? 3;
            _cefTexture.Call("eval", $"if(window.__relicUI.setEquipCount) window.__relicUI.setEquipCount({equippedCount}, {maxEquip})");
        }

        private void OnPageError(string url, int errorCode, string errorText)
        {
            GD.PrintErr($"[RelicInventory] CEF error: {errorText} ({errorCode}) for {url}");
        }

        private void OnIpcData(Variant data)
        {
            if (data.VariantType != Variant.Type.Dictionary) return;

            var dict = data.AsGodotDictionary();
            string action = dict.ContainsKey("action") ? dict["action"].AsString() : "";
            var actionData = dict.ContainsKey("data")
                ? dict["data"].AsGodotDictionary()
                : new Dictionary();

            switch (action)
            {
                case "menu-select":
                    string item = actionData.ContainsKey("item") ? actionData["item"].AsString() : "";
                    if (item == "back")
                        GameManager.Instance?.ShowMetaHub();
                    break;

                case "relic-selected":
                    string relicId = actionData.ContainsKey("relic") ? actionData["relic"].AsString() : "";
                    HandleRelicSelected(relicId);
                    break;

                case "equip-relic":
                    string equipId = actionData.ContainsKey("relic") ? actionData["relic"].AsString() : "";
                    if (_relicManager != null && _relicManager.Equip(equipId))
                    {
                        GD.Print($"[RelicInventory] Equipped: {equipId}");
                        PushRelicData(); // Refresh entire grid to update states
                    }
                    else
                        GD.Print($"[RelicInventory] Cannot equip: {equipId}");
                    break;

                case "unequip-relic":
                    string unequipId = actionData.ContainsKey("relic") ? actionData["relic"].AsString() : "";
                    if (_relicManager != null && _relicManager.Unequip(unequipId))
                    {
                        GD.Print($"[RelicInventory] Unequipped: {unequipId}");
                        PushRelicData();
                    }
                    break;

                case "ready":
                    GD.Print("[RelicInventory] Screen ready");
                    break;
            }
        }

        private void HandleRelicSelected(string relicId)
        {
            _selectedRelicId = relicId;

            foreach (var relic in RelicRegistry.All)
            {
                if (relic.Id != relicId) continue;

                string name = EscapeJs(relic.Name);
                string desc = EscapeJs(relic.Desc);
                string tradeoffJs = relic.Tradeoff != null ? $"'{EscapeJs(relic.Tradeoff)}'" : "null";
                bool owned = _relicManager?.OwnsRelic(relic.Id) ?? false;
                bool equipped = _relicManager?.IsEquipped(relic.Id) ?? false;

                _cefTexture.Call("eval",
                    $"window.__relicUI.showDetail('{relic.Id}', '{name}', '{relic.Icon}', '{relic.Rarity}', '{desc}', {tradeoffJs}, {BoolJs(owned)}, {BoolJs(equipped)})");
                break;
            }
        }

        private void OnConsoleMessage(int level, string message, string source, int line)
        {
            string lvl = level switch { 0 => "DBG", 1 => "INF", 2 => "WRN", 3 => "ERR", _ => "LOG" };
            GD.Print($"[CEF {lvl}] {message}");
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event.IsActionPressed("ui_cancel"))
                GameManager.Instance?.ShowMetaHub();
        }

        // ── Fallback native UI (no CEF) ──

        private void BuildFallbackUI()
        {
            var bg = new ColorRect();
            bg.SetAnchorsPreset(LayoutPreset.FullRect);
            bg.Color = new Color(0.06f, 0.05f, 0.04f);
            AddChild(bg);

            var margin = new MarginContainer();
            margin.SetAnchorsPreset(LayoutPreset.FullRect);
            margin.AddThemeConstantOverride("margin_left", 40);
            margin.AddThemeConstantOverride("margin_right", 40);
            margin.AddThemeConstantOverride("margin_top", 40);
            margin.AddThemeConstantOverride("margin_bottom", 40);
            AddChild(margin);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 12);
            margin.AddChild(vbox);

            // Header
            var headerRow = new HBoxContainer();
            headerRow.AddThemeConstantOverride("separation", 12);
            vbox.AddChild(headerRow);

            var backBtn = new Button();
            backBtn.Text = "< BACK";
            backBtn.Pressed += () => GameManager.Instance?.ShowMetaHub();
            headerRow.AddChild(backBtn);

            var title = new Label();
            title.Text = "  RELICS";
            title.AddThemeFontSizeOverride("font_size", 28);
            title.AddThemeColorOverride("font_color", new Color(0.3f, 0.8f, 0.6f));
            title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            headerRow.AddChild(title);

            _fbCountLabel = new Label();
            _fbCountLabel.AddThemeFontSizeOverride("font_size", 14);
            _fbCountLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
            headerRow.AddChild(_fbCountLabel);

            _fbEquippedCount = new Label();
            _fbEquippedCount.AddThemeFontSizeOverride("font_size", 14);
            _fbEquippedCount.AddThemeColorOverride("font_color", new Color(0.24f, 0.87f, 0.78f));
            headerRow.AddChild(_fbEquippedCount);

            // Split: grid left, detail right
            var split = new HSplitContainer();
            split.SizeFlagsVertical = SizeFlags.ExpandFill;
            vbox.AddChild(split);

            // Grid
            var gridScroll = new ScrollContainer();
            gridScroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            split.AddChild(gridScroll);

            var grid = new GridContainer();
            grid.Columns = 3;
            grid.AddThemeConstantOverride("h_separation", 10);
            grid.AddThemeConstantOverride("v_separation", 10);
            gridScroll.AddChild(grid);

            // Detail panel
            var detailPanel = new PanelContainer();
            detailPanel.CustomMinimumSize = new Vector2(350, 0);
            var detailStyle = new StyleBoxFlat();
            detailStyle.BgColor = new Color(0.04f, 0.04f, 0.06f, 0.9f);
            detailStyle.ContentMarginLeft = 16;
            detailStyle.ContentMarginRight = 16;
            detailStyle.ContentMarginTop = 16;
            detailStyle.ContentMarginBottom = 16;
            detailPanel.AddThemeStyleboxOverride("panel", detailStyle);
            split.AddChild(detailPanel);

            var detailVBox = new VBoxContainer();
            detailVBox.AddThemeConstantOverride("separation", 8);
            detailPanel.AddChild(detailVBox);

            _fbDetailName = new Label();
            _fbDetailName.Text = "Select a relic";
            _fbDetailName.AddThemeFontSizeOverride("font_size", 22);
            _fbDetailName.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            detailVBox.AddChild(_fbDetailName);

            _fbDetailDesc = new Label();
            _fbDetailDesc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _fbDetailDesc.AddThemeFontSizeOverride("font_size", 14);
            detailVBox.AddChild(_fbDetailDesc);

            _fbDetailTradeoff = new Label();
            _fbDetailTradeoff.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _fbDetailTradeoff.AddThemeFontSizeOverride("font_size", 13);
            _fbDetailTradeoff.AddThemeColorOverride("font_color", new Color(1f, 0.6f, 0.3f));
            detailVBox.AddChild(_fbDetailTradeoff);

            _fbEquipBtn = new Button();
            _fbEquipBtn.Visible = false;
            _fbEquipBtn.AddThemeFontSizeOverride("font_size", 16);
            detailVBox.AddChild(_fbEquipBtn);

            // Populate grid
            foreach (var relic in RelicRegistry.All)
            {
                bool owned = _relicManager?.OwnsRelic(relic.Id) ?? false;
                bool equipped = _relicManager?.IsEquipped(relic.Id) ?? false;

                var btn = new Button();
                btn.CustomMinimumSize = new Vector2(160, 70);

                if (!owned)
                {
                    btn.Text = "???";
                    btn.AddThemeColorOverride("font_color", new Color(0.3f, 0.3f, 0.3f));
                    btn.Disabled = true;
                }
                else
                {
                    btn.Text = equipped ? $"[E] {relic.Name}" : relic.Name;
                    btn.AddThemeColorOverride("font_color", relic.Tint);

                    var r = relic;
                    btn.Pressed += () => SelectFallbackRelic(r);
                }

                grid.AddChild(btn);
            }

            UpdateFallbackCounts();
        }

        private void SelectFallbackRelic(RelicRegistry.Relic relic)
        {
            _selectedRelicId = relic.Id;
            bool owned = _relicManager?.OwnsRelic(relic.Id) ?? false;
            bool equipped = _relicManager?.IsEquipped(relic.Id) ?? false;

            _fbDetailName.Text = relic.Name;
            _fbDetailName.AddThemeColorOverride("font_color", relic.Tint);
            _fbDetailDesc.Text = relic.Desc;
            _fbDetailTradeoff.Text = relic.Tradeoff != null ? $"Tradeoff: {relic.Tradeoff}" : "";

            if (owned)
            {
                _fbEquipBtn.Visible = true;
                _fbEquipBtn.Text = equipped ? "UNEQUIP" : "EQUIP";

                // Disconnect old handlers
                foreach (var conn in _fbEquipBtn.GetSignalConnectionList("pressed"))
                    _fbEquipBtn.Disconnect("pressed", (Callable)conn["callable"]);

                string id = relic.Id;
                if (equipped)
                    _fbEquipBtn.Pressed += () => { _relicManager?.Unequip(id); RefreshFallbackUI(); };
                else
                    _fbEquipBtn.Pressed += () => { _relicManager?.Equip(id); RefreshFallbackUI(); };
            }
            else
            {
                _fbEquipBtn.Visible = false;
            }
        }

        private void RefreshFallbackUI()
        {
            // Rebuild the entire fallback UI to reflect new equip states
            foreach (var child in GetChildren())
                child.QueueFree();
            BuildFallbackUI();
        }

        private void UpdateFallbackCounts()
        {
            int owned = _relicManager?.OwnedCount ?? 0;
            int total = RelicRegistry.All.Length;
            int equipped = _relicManager?.EquippedCount ?? 0;
            int maxEquip = _relicManager?.MaxEquipSlots ?? 3;

            if (_fbCountLabel != null)
                _fbCountLabel.Text = $"{owned} / {total} COLLECTED";
            if (_fbEquippedCount != null)
                _fbEquippedCount.Text = $"  [{equipped}/{maxEquip} EQUIPPED]";
        }

        // ── Helpers ──

        private static string EscapeJs(string s) => s?.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n") ?? "";
        private static string BoolJs(bool v) => v ? "true" : "false";
    }
}
