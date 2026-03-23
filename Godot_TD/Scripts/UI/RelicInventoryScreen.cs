using Godot;
using Godot.Collections;

namespace JunkyardTD
{
    /// <summary>
    /// Relic inventory screen — renders Stitch UI via godot-cef.
    /// Pushes relic data from RelicRegistry into the HTML grid.
    /// Falls back to native Godot UI if CEF is unavailable.
    /// </summary>
    public partial class RelicInventoryScreen : Control
    {
        private GodotObject _cefTexture;
        private string _selectedRelicId;

        public override void _Ready()
        {
            if (ClassDB.ClassExists("CefTexture"))
                CreateCefBrowser();
            else
                BuildFallbackUI();
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

            // Inject bridge
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

            // Push relic data
            PushRelicData();
        }

        private void PushRelicData()
        {
            var relics = RelicRegistry.All;
            int collected = relics.Length; // TODO: filter by actual unlock state
            int total = relics.Length;

            _cefTexture.Call("eval", $"window.__relicUI.setCount({collected}, {total})");
            _cefTexture.Call("eval", "window.__relicUI.clearGrid()");

            foreach (var relic in relics)
            {
                string name = relic.Name.Replace("'", "\\'");
                string desc = relic.Desc.Replace("'", "\\'");
                bool hasTradeoff = relic.Tradeoff != null;
                bool locked = false; // TODO: check actual unlock state

                _cefTexture.Call("eval",
                    $"window.__relicUI.addRelic('{relic.Id}', '{name}', '{relic.Icon}', '{relic.Rarity}', '{desc}', {(hasTradeoff ? "true" : "false")}, {(locked ? "true" : "false")})");
            }
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
                        GameManager.Instance?.ReturnToMainMenu();
                    break;

                case "relic-selected":
                    string relicId = actionData.ContainsKey("relic") ? actionData["relic"].AsString() : "";
                    HandleRelicSelected(relicId);
                    break;

                case "equip-relic":
                    string equipId = actionData.ContainsKey("relic") ? actionData["relic"].AsString() : "";
                    GD.Print($"[RelicInventory] Equip: {equipId}");
                    break;

                case "unequip-relic":
                    string unequipId = actionData.ContainsKey("relic") ? actionData["relic"].AsString() : "";
                    GD.Print($"[RelicInventory] Unequip: {unequipId}");
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

                string name = relic.Name.Replace("'", "\\'");
                string desc = relic.Desc.Replace("'", "\\'");
                string tradeoff = relic.Tradeoff != null ? relic.Tradeoff.Replace("'", "\\'") : "";
                string tradeoffJs = relic.Tradeoff != null ? $"'{tradeoff}'" : "null";

                _cefTexture.Call("eval",
                    $"window.__relicUI.showDetail('{relic.Id}', '{name}', '{relic.Icon}', '{relic.Rarity}', '{desc}', {tradeoffJs}, null, [])");
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
                GameManager.Instance?.ReturnToMainMenu();
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
            vbox.AddChild(headerRow);

            var backBtn = new Button();
            backBtn.Text = "< BACK";
            backBtn.Pressed += () => GameManager.Instance?.ReturnToMainMenu();
            headerRow.AddChild(backBtn);

            var title = new Label();
            title.Text = "  RELICS";
            title.AddThemeFontSizeOverride("font_size", 28);
            title.AddThemeColorOverride("font_color", new Color(0.3f, 0.8f, 0.6f));
            title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            headerRow.AddChild(title);

            var countLabel = new Label();
            countLabel.Text = $"{RelicRegistry.All.Length} / {RelicRegistry.All.Length} COLLECTED";
            countLabel.AddThemeFontSizeOverride("font_size", 14);
            countLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
            headerRow.AddChild(countLabel);

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
            split.AddChild(detailPanel);

            var detailVBox = new VBoxContainer();
            detailVBox.AddThemeConstantOverride("separation", 8);
            detailPanel.AddChild(detailVBox);

            var detailName = new Label();
            detailName.Text = "Select a relic";
            detailName.AddThemeFontSizeOverride("font_size", 20);
            detailName.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            detailVBox.AddChild(detailName);

            var detailDesc = new Label();
            detailDesc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            detailVBox.AddChild(detailDesc);

            // Populate grid
            foreach (var relic in RelicRegistry.All)
            {
                var btn = new Button();
                btn.Text = relic.Name;
                btn.CustomMinimumSize = new Vector2(160, 70);
                btn.AddThemeColorOverride("font_color", relic.Tint);

                var r = relic; // capture
                btn.Pressed += () =>
                {
                    detailName.Text = r.Name;
                    detailName.AddThemeColorOverride("font_color", r.Tint);
                    detailDesc.Text = r.Desc + (r.Tradeoff != null ? $"\n\nTradeoff: {r.Tradeoff}" : "");
                };

                grid.AddChild(btn);
            }
        }
    }
}
