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

        // Fallback UI refs
        private Label _fbDetailName;
        private Label _fbDetailDesc;
        private Label _fbDetailTradeoff;
        private Button _fbEquipBtn;
        private Label _fbCountLabel;
        private Label _fbEquippedCount;

        public override void _Ready()
        {
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

        private int _blankPages;
        private Control _backdrop;

        private void OnPageBlank()
        {
            _blankPages++;
            CleanupCef();
            if (_blankPages == 1) { CreateCefBrowser(); return; }
            if (_backdrop != null && IsInstanceValid(_backdrop)) _backdrop.QueueFree();
            BuildFallbackUI();
        }

        private void CreateCefBrowser()
        {
            if (_backdrop == null || !IsInstanceValid(_backdrop))
                _backdrop = CefHelper.AddBackdrop(this, "RELICS\nEsc: back");
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
            CefHelper.Configure(_cefTexture); // CPU rendering unless the project setting asks for GPU sharing

            _cefTexture.Connect("load_finished", Callable.From<string, int>(OnPageLoaded));
            _cefTexture.Connect("load_error", Callable.From<string, int, string>(OnPageError));
            _cefTexture.Connect("ipc_data_message", Callable.From<Variant>(OnIpcData));
            _cefTexture.Connect("console_message", Callable.From<int, string, string, int>(OnConsoleMessage));

            AddChild(cefControl);
            // A page whose render process dies draws nothing (grey, no way out): load it again
            CefHelper.WatchCrash(_cefTexture, "Relics", () => _cefTexture?.Set("url", "res://ui/relic-inventory/index.html"));
            // A web view that never paints showed Godot's grey clear colour with no way out:
            // make a new one, and if that doesn't draw either, use the built-in screen
            CefHelper.WatchPaint(this, "Relics", OnPageBlank);
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
            _cefTexture.Call("eval", $"window.__relicUI.setCount({RelicInventory.OwnedCount}, {relics.Length})");
            _cefTexture.Call("eval", "window.__relicUI.clearGrid()");
            foreach (var kind in RelicRegistry.Kinds)
            {
                string id = RelicInventory.EquippedOfKind(kind);
                var r = id == null ? null : RelicRegistry.Get(id);
                _cefTexture.Call("eval", $"window.__relicUI.setSlot('{kind}', '{id ?? ""}', '{EscapeJs(r?.Name ?? "")}', '{Hex(r?.Tint ?? Colors.White)}')");
            }
            foreach (var kind in RelicRegistry.Kinds)
                foreach (var relic in RelicRegistry.OfKind(kind))
                {
                    bool locked = !RelicInventory.OwnsRelic(relic.Id);
                    bool equipped = RelicInventory.IsEquipped(relic.Id);
                    _cefTexture.Call("eval",
                        $"window.__relicUI.addRelic('{relic.Id}', '{EscapeJs(relic.Name)}', '{relic.Icon}', '{relic.Rarity}', '{EscapeJs(relic.Desc)}', {BoolJs(relic.Tradeoff != null)}, {BoolJs(locked)}, {BoolJs(equipped)}, '{relic.Kind}', '{Hex(relic.Tint)}')");
                }
            if (_selectedRelicId != null) HandleRelicSelected(_selectedRelicId);
        }

        private static string Hex(Color c) => "#" + c.ToHtml(false);

        /// <summary>What equipping it would do to its slot.</summary>
        internal static string SlotNote(RelicRegistry.Relic relic)
        {
            string inSlot = RelicInventory.EquippedOfKind(relic.Kind);
            if (inSlot == relic.Id) return $"On, in {RelicRegistry.TheSlot(relic.Kind)}.";
            if (inSlot != null) return $"Equipping it replaces {RelicRegistry.Get(inSlot)?.Name ?? inSlot} in {RelicRegistry.TheSlot(relic.Kind)}.";
            return $"Goes in {RelicRegistry.TheSlot(relic.Kind)}, which is empty.";
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
                    if (RelicInventory.Equip(equipId))
                    {
                        GD.Print($"[RelicInventory] Equipped: {equipId}");
                        PushRelicData(); // Refresh entire grid to update states
                    }
                    else
                        GD.Print($"[RelicInventory] Cannot equip: {equipId}");
                    break;

                case "unequip-relic":
                    string unequipId = actionData.ContainsKey("relic") ? actionData["relic"].AsString() : "";
                    if (RelicInventory.Unequip(unequipId))
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
            if (RelicRegistry.Get(relicId) is not { } relic) return;
            bool owned = RelicInventory.OwnsRelic(relic.Id);
            string name = owned ? EscapeJs(relic.Name) : "???";
            string desc = owned ? EscapeJs(relic.Desc) : "Not found yet. Relics come out of loot crates: Gold Caches and better.";
            string tradeoffJs = owned && relic.Tradeoff != null ? $"'{EscapeJs(relic.Tradeoff)}'" : "null";
            _cefTexture.Call("eval",
                $"window.__relicUI.showDetail('{relic.Id}', '{name}', '{relic.Icon}', '{relic.Rarity}', '{desc}', {tradeoffJs}, {BoolJs(owned)}, {BoolJs(RelicInventory.IsEquipped(relic.Id))}, '{relic.Kind}', '{Hex(relic.Tint)}', '{EscapeJs(owned ? SlotNote(relic) : "")}')");
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
            var bg = new ColorRect { Color = new Color(0.04f, 0.05f, 0.09f) };
            bg.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(bg);

            var margin = new MarginContainer();
            margin.SetAnchorsPreset(LayoutPreset.FullRect);
            foreach (var side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride($"margin_{side}", 36);
            AddChild(margin);
            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 12);
            margin.AddChild(vbox);

            var header = new HBoxContainer();
            header.AddThemeConstantOverride("separation", 14);
            vbox.AddChild(header);
            var backBtn = new Button { Text = "< BACK", CustomMinimumSize = new Vector2(110, 38) };
            backBtn.Pressed += () => GameManager.Instance?.ShowMetaHub();
            header.AddChild(backBtn);
            var title = FbLabel("RELICS", 28, new Color(0.76f, 0.91f, 0.99f));
            header.AddChild(title);
            var sub = FbLabel("Each relic changes how something fights. One slot each for BIT's weapon, the towers and the Spire.", 15, new Color(0.6f, 0.65f, 0.75f));
            sub.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            sub.VerticalAlignment = VerticalAlignment.Center;
            header.AddChild(sub);
            _fbCountLabel = FbLabel($"{RelicInventory.OwnedCount} / {RelicRegistry.All.Length} FOUND", 14, new Color(0.6f, 0.65f, 0.75f));
            header.AddChild(_fbCountLabel);

            // The three slots
            var slots = new HBoxContainer { Name = "Slots" };
            slots.AddThemeConstantOverride("separation", 12);
            vbox.AddChild(slots);
            foreach (var kind in RelicRegistry.Kinds)
            {
                string id = RelicInventory.EquippedOfKind(kind);
                var r = id == null ? null : RelicRegistry.Get(id);
                var card = FbPanel(r?.Tint ?? new Color(0.3f, 0.34f, 0.42f), id == null ? 0.35f : 1f);
                card.Name = "Slot_" + kind;
                card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                card.CustomMinimumSize = new Vector2(0, 84);
                var col = new VBoxContainer();
                col.AddChild(FbLabel(RelicRegistry.KindName(kind).ToUpper(), 13, new Color(0.6f, 0.65f, 0.75f)));
                col.AddChild(FbLabel(r?.Name.ToUpper() ?? "EMPTY", 20, r?.Tint ?? new Color(0.45f, 0.5f, 0.6f)));
                if (r == null) col.AddChild(FbLabel("Equip one below, or straight from a loot crate", 13, new Color(0.45f, 0.5f, 0.6f)));
                card.AddChild(col);
                slots.AddChild(card);
            }

            var split = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
            split.AddThemeConstantOverride("separation", 16);
            vbox.AddChild(split);

            var scroll = new ScrollContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            split.AddChild(scroll);
            var groups = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            groups.AddThemeConstantOverride("separation", 10);
            scroll.AddChild(groups);
            foreach (var kind in RelicRegistry.Kinds)
            {
                groups.AddChild(FbLabel(RelicRegistry.KindName(kind).ToUpper(), 13, new Color(0.6f, 0.65f, 0.75f)));
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 10);
                groups.AddChild(row);
                foreach (var relic in RelicRegistry.OfKind(kind))
                {
                    bool owned = RelicInventory.OwnsRelic(relic.Id);
                    bool equipped = RelicInventory.IsEquipped(relic.Id);
                    var btn = new Button
                    {
                        Name = "Relic_" + relic.Id,
                        Text = owned ? (equipped ? $"{relic.Name}\nEQUIPPED" : $"{relic.Name}\n{relic.Rarity}") : $"???\n{relic.Rarity}",
                        CustomMinimumSize = new Vector2(220, 64),
                        SizeFlagsHorizontal = SizeFlags.ExpandFill,
                    };
                    btn.AddThemeFontSizeOverride("font_size", 15);
                    btn.AddThemeColorOverride("font_color", owned ? relic.Tint : new Color(0.4f, 0.44f, 0.52f));
                    var st = new StyleBoxFlat { BgColor = new Color(0.08f, 0.1f, 0.16f, 0.95f), BorderColor = equipped ? relic.Tint : new Color(0.25f, 0.28f, 0.35f) };
                    st.SetBorderWidthAll(equipped ? 2 : 1);
                    btn.AddThemeStyleboxOverride("normal", st);
                    var r = relic;
                    btn.Pressed += () => SelectFallbackRelic(r);
                    row.AddChild(btn);
                }
            }

            var detail = FbPanel(new Color(0.3f, 0.34f, 0.42f), 0.6f);
            detail.CustomMinimumSize = new Vector2(400, 0);
            split.AddChild(detail);
            var dv = new VBoxContainer();
            dv.AddThemeConstantOverride("separation", 8);
            detail.AddChild(dv);
            _fbDetailName = FbLabel("Select a relic", 22, new Color(0.6f, 0.62f, 0.68f));
            dv.AddChild(_fbDetailName);
            _fbDetailDesc = FbLabel("", 15, new Color(0.8f, 0.83f, 0.9f));
            _fbDetailDesc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            dv.AddChild(_fbDetailDesc);
            _fbDetailTradeoff = FbLabel("", 14, new Color(1f, 0.6f, 0.45f));
            _fbDetailTradeoff.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            dv.AddChild(_fbDetailTradeoff);
            _fbEquippedCount = FbLabel("", 13, new Color(0.6f, 0.65f, 0.75f));
            _fbEquippedCount.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            dv.AddChild(_fbEquippedCount);
            _fbEquipBtn = new Button { Name = "EquipButton", Visible = false, CustomMinimumSize = new Vector2(0, 44) };
            _fbEquipBtn.AddThemeFontSizeOverride("font_size", 16);
            dv.AddChild(_fbEquipBtn);

            if (_selectedRelicId != null && RelicRegistry.Get(_selectedRelicId) is { } sel) SelectFallbackRelic(sel);
        }

        private static Label FbLabel(string text, int size, Color color)
        {
            var l = new Label { Text = text };
            l.AddThemeFontSizeOverride("font_size", size);
            l.AddThemeColorOverride("font_color", color);
            return l;
        }

        private static PanelContainer FbPanel(Color edge, float alpha)
        {
            var p = new PanelContainer();
            var st = new StyleBoxFlat { BgColor = new Color(0.07f, 0.09f, 0.15f, 0.95f), BorderColor = new Color(edge.R, edge.G, edge.B, alpha) };
            st.SetBorderWidthAll(1);
            st.BorderWidthLeft = 4;
            st.ContentMarginLeft = st.ContentMarginRight = 16;
            st.ContentMarginTop = st.ContentMarginBottom = 12;
            p.AddThemeStyleboxOverride("panel", st);
            return p;
        }

        private void SelectFallbackRelic(RelicRegistry.Relic relic)
        {
            _selectedRelicId = relic.Id;
            bool owned = RelicInventory.OwnsRelic(relic.Id);
            bool equipped = RelicInventory.IsEquipped(relic.Id);
            _fbDetailName.Text = owned ? relic.Name.ToUpper() : "???";
            _fbDetailName.AddThemeColorOverride("font_color", owned ? relic.Tint : new Color(0.5f, 0.54f, 0.62f));
            _fbDetailDesc.Text = owned ? relic.Desc : "Not found yet. Relics come out of loot crates: Gold Caches and better.";
            _fbDetailTradeoff.Text = owned && relic.Tradeoff != null ? $"Trade-off: {relic.Tradeoff}" : "";
            _fbEquippedCount.Text = owned ? SlotNote(relic) : "";
            _fbEquipBtn.Visible = owned;
            if (!owned) return;
            _fbEquipBtn.Text = equipped ? "TAKE OFF" : "EQUIP";
            foreach (var conn in _fbEquipBtn.GetSignalConnectionList("pressed"))
                _fbEquipBtn.Disconnect("pressed", (Callable)conn["callable"]);
            string id = relic.Id;
            if (equipped) _fbEquipBtn.Pressed += () => { RelicInventory.Unequip(id); RefreshFallbackUI(); };
            else _fbEquipBtn.Pressed += () => { RelicInventory.Equip(id); RefreshFallbackUI(); };
        }

        private void RefreshFallbackUI()
        {
            foreach (var child in GetChildren())
                child.QueueFree();
            BuildFallbackUI();
        }

        /// <summary>Tests: the built-in screen's slot cards and relic buttons.</summary>
        internal Node FallbackSlot(string kind) => FindChild("Slot_" + kind, true, false);
        internal Button FallbackRelic(string id) => FindChild("Relic_" + id, true, false) as Button;
        internal Button FallbackEquip => _fbEquipBtn;

        // ── Helpers ──

        private static string EscapeJs(string s) => s?.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n") ?? "";
        private static string BoolJs(bool v) => v ? "true" : "false";
    }
}
