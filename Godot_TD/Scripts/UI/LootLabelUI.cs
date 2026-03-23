using Godot;
using Godot.Collections;

namespace JunkyardTD
{
    /// <summary>
    /// In-world loot label — single-line rarity-colored strip shown above a dropped relic.
    /// CEF overlay renders ui/loot-label/index.html. Attaches as child of a 3D world item.
    /// Call Show()/Hide() to control visibility; PushRelic() to set the displayed relic data.
    /// </summary>
    public partial class LootLabelUI : Control
    {
        private GodotObject _cefTexture;
        private bool _cefReady;
        private string _pendingName;
        private string _pendingRarity;
        private string _pendingIcon;

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Ignore;

            if (ClassDB.ClassExists("CefTexture"))
                CreateCefBrowser();
            else
                BuildFallbackUI();
        }

        // ── Public API ──

        public void PushRelic(RelicRegistry.Relic relic)
        {
            PushRelic(relic.Name, relic.Rarity, relic.Icon);
        }

        public void PushRelic(string name, string rarity, string icon)
        {
            if (_cefReady && _cefTexture != null)
            {
                _cefTexture.Call("eval",
                    $"window.__lootLabelUI.show('{EscapeJs(name)}', '{EscapeJs(rarity)}', '{EscapeJs(icon)}')");
            }
            else
            {
                _pendingName = name;
                _pendingRarity = rarity;
                _pendingIcon = icon;
            }

            // Update fallback label if present
            var lbl = GetNodeOrNull<Label>("FallbackLabel");
            if (lbl != null)
            {
                lbl.Text = $"[{rarity?.ToUpper()}] {name}";
                lbl.AddThemeColorOverride("font_color", RarityColor(rarity));
            }
        }

        public override void _ExitTree()
        {
            if (_cefTexture == null) return;
            try { _cefTexture.Set("url", "about:blank"); } catch { /* ignore */ }
            if (_cefTexture is Node cefNode && IsInstanceValid(cefNode))
                cefNode.QueueFree();
            _cefTexture = null;
        }

        // ── CEF ──

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
            cefControl.MouseFilter = MouseFilterEnum.Ignore;

            _cefTexture.Set("background_color", new Color(0f, 0f, 0f, 0f)); // transparent
            _cefTexture.Set("enable_accelerated_osr", true);

            _cefTexture.Connect("load_finished", Callable.From<string, int>(OnPageLoaded));
            _cefTexture.Connect("load_error", Callable.From<string, int, string>(OnPageError));
            _cefTexture.Connect("ipc_data_message", Callable.From<Variant>(OnIpcData));
            _cefTexture.Connect("console_message", Callable.From<int, string, string, int>(OnConsoleMessage));

            AddChild(cefControl);

            _cefTexture.Set("url", "res://ui/loot-label/index.html");
        }

        private void OnPageLoaded(string url, int httpStatus)
        {
            GD.Print($"[LootLabel] Loaded: {url} (HTTP {httpStatus})");

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
            _cefReady = true;

            // Push any pending data
            if (_pendingName != null)
            {
                PushRelic(_pendingName, _pendingRarity, _pendingIcon);
                _pendingName = null;
            }
        }

        private void OnPageError(string url, int errorCode, string errorText)
        {
            GD.PrintErr($"[LootLabel] CEF error: {errorText} ({errorCode}) for {url}");
        }

        private void OnIpcData(Variant data)
        {
            if (data.VariantType != Variant.Type.Dictionary) return;

            var dict = data.AsGodotDictionary();
            string action = dict.ContainsKey("action") ? dict["action"].AsString() : "";

            switch (action)
            {
                case "ready":
                    GD.Print("[LootLabel] Label ready");
                    break;
            }
        }

        private void OnConsoleMessage(int level, string message, string source, int line)
        {
            string lvl = level switch { 0 => "DBG", 1 => "INF", 2 => "WRN", 3 => "ERR", _ => "LOG" };
            GD.Print($"[CEF-Label {lvl}] {message}");
        }

        // ── Fallback ──

        private void BuildFallbackUI()
        {
            var lbl = new Label();
            lbl.Name = "FallbackLabel";
            lbl.HorizontalAlignment = HorizontalAlignment.Center;
            lbl.SetAnchorsPreset(LayoutPreset.CenterTop);
            lbl.AddThemeFontSizeOverride("font_size", 14);
            lbl.Text = "[COMMON] Unknown Relic";
            lbl.AddThemeColorOverride("font_color", new Color(0.55f, 0.57f, 0.59f));
            AddChild(lbl);
        }

        // ── Helpers ──

        public static Color RarityColor(string rarity)
        {
            return rarity switch
            {
                "uncommon"  => new Color(0.38f, 0.98f, 0.89f),  // #62fae4
                "rare"      => new Color(0.66f, 0.33f, 0.97f),  // #a855f7
                "legendary" => new Color(0.96f, 0.62f, 0.04f),  // #f59e0b
                "mythic"    => new Color(0.93f, 0.28f, 0.6f),   // #ec4899
                _           => new Color(0.55f, 0.57f, 0.59f),  // #8b9196
            };
        }

        private static string EscapeJs(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n").Replace("\r", "");
        }
    }
}
