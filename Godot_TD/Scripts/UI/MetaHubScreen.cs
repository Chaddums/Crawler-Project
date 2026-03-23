using Godot;
using Godot.Collections;

namespace JunkyardTD
{
    /// <summary>
    /// UX6: Meta Hub / Command Center — CEF bridge for the between-runs hub screen.
    /// Shows meta resources, suit mannequins, and navigation to Territory/Suits/Relics/StartRun.
    /// </summary>
    public partial class MetaHubScreen : Control
    {
        private GodotObject _cefTexture;

        public override void _Ready()
        {
            if (ClassDB.ClassExists("CefTexture"))
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
            {
                cefNode.QueueFree();
            }
            _cefTexture = null;
        }

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

            _cefTexture.Set("background_color", new Color(0.024f, 0.047f, 0.149f, 1f));
            _cefTexture.Set("enable_accelerated_osr", true);

            _cefTexture.Connect("load_finished", Callable.From<string, int>(OnPageLoaded));
            _cefTexture.Connect("load_error", Callable.From<string, int, string>(OnPageError));
            _cefTexture.Connect("ipc_data_message", Callable.From<Variant>(OnIpcData));
            _cefTexture.Connect("console_message", Callable.From<int, string, string, int>(OnConsoleMessage));

            AddChild(cefControl);

            _cefTexture.Set("url", "res://ui/meta-hub/index.html");
        }

        private void OnPageLoaded(string url, int httpStatus)
        {
            GD.Print($"[MetaHub] Loaded: {url} (HTTP {httpStatus})");

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

            PushHubData();
        }

        private void PushHubData()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            int metaRes = gm.MetaSave?.MetaResources ?? 0;
            int runCount = gm.MetaSave?.RunCount ?? 0;

            _cefTexture.Call("eval", $"window.__metaHubUI.setResources({metaRes})");
            _cefTexture.Call("eval", $"window.__metaHubUI.setRunCount({runCount})");

            // Suits
            var suits = SuitManager.GetAll();
            int available = 0;
            for (int i = 0; i < suits.Length; i++)
            {
                if (suits[i] != null && suits[i].Nodes.Count > 0 && !suits[i].Consumed)
                    available++;
            }
            _cefTexture.Call("eval", $"window.__metaHubUI.setSuitCount({available}, {Constants.MAX_SUIT_SLOTS})");

            // Ship suit mannequins
            string suitsJson = BuildSuitsJson(suits);
            _cefTexture.Call("eval", $"window.__metaHubUI.setShipSuits('{EscapeJs(suitsJson)}')");

            // Relics — show actual owned count, not total
            int ownedRelics = 0;
            if (ServiceLocator.TryGet<RelicManager>(out var rm))
                ownedRelics = rm.OwnedCount;
            int totalRelics = RelicRegistry.All.Length;
            _cefTexture.Call("eval", $"window.__metaHubUI.setRelicCount({ownedRelics}, {totalRelics})");

            // Territory
            int unlocked = gm.MetaSave?.UnlockedTerritories?.Count ?? 0;
            var planets = TerritoryLoader.LoadAll();
            int totalSections = 0;
            foreach (var kvp in planets)
                totalSections += kvp.Value.Sections.Count;
            _cefTexture.Call("eval", $"window.__metaHubUI.setTerritoryCount({unlocked}, {totalSections})");
        }

        private static string BuildSuitsJson(SuitSaveData[] suits)
        {
            var arr = new Godot.Collections.Array();
            for (int i = 0; i < Constants.MAX_SUIT_SLOTS; i++)
            {
                var s = i < suits.Length ? suits[i] : null;
                if (s != null && s.Nodes.Count > 0)
                {
                    var dict = new Dictionary();
                    dict["name"] = s.Name ?? $"Suit {i + 1}";
                    dict["role"] = s.Role ?? "";
                    dict["consumed"] = s.Consumed;
                    arr.Add(dict);
                }
                else
                {
                    arr.Add(new Variant());
                }
            }
            return Json.Stringify(arr);
        }

        private static string EscapeJs(string s)
        {
            return s.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n").Replace("\r", "");
        }

        private void OnPageError(string url, int errorCode, string errorText)
        {
            GD.PrintErr($"[MetaHub] CEF error: {errorText} ({errorCode}) for {url}");
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
                case "navigate":
                    string target = actionData.ContainsKey("target") ? actionData["target"].AsString() : "";
                    HandleNavigate(target);
                    break;

                case "ready":
                    GD.Print("[MetaHub] Screen ready");
                    break;
            }
        }

        private void HandleNavigate(string target)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            switch (target)
            {
                case "territory":
                    gm.ShowTerritory();
                    break;
                case "suits":
                    gm.ShowSuitInventory();
                    break;
                case "relics":
                    gm.ShowRelicInventory();
                    break;
                case "start-run":
                    gm.StartPlanetSelect();
                    break;
                case "main-menu":
                    gm.ReturnToMainMenu();
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

        // ── Fallback native UI ──

        private void BuildFallbackUI()
        {
            var bg = new ColorRect();
            bg.SetAnchorsPreset(LayoutPreset.FullRect);
            bg.Color = new Color(0.04f, 0.06f, 0.15f);
            AddChild(bg);

            var center = new CenterContainer();
            center.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(center);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 16);
            center.AddChild(vbox);

            var title = new Label();
            title.Text = "COMMAND CENTER";
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.AddThemeFontSizeOverride("font_size", 36);
            title.AddThemeColorOverride("font_color", new Color(0.49f, 0.82f, 0.98f));
            vbox.AddChild(title);

            AddNavButton(vbox, "Territory", () => GameManager.Instance?.ShowTerritory());
            AddNavButton(vbox, "Suits", () => GameManager.Instance?.ShowSuitInventory());
            AddNavButton(vbox, "Relics", () => GameManager.Instance?.ShowRelicInventory());
            AddNavButton(vbox, "Start Run", () => GameManager.Instance?.StartPlanetSelect());

            var spacer = new Control();
            spacer.CustomMinimumSize = new Vector2(0, 20);
            vbox.AddChild(spacer);

            AddNavButton(vbox, "Main Menu", () => GameManager.Instance?.ReturnToMainMenu());
        }

        private static void AddNavButton(VBoxContainer parent, string text, System.Action onPress)
        {
            var btn = new Button();
            btn.Text = text;
            btn.CustomMinimumSize = new Vector2(250, 50);
            btn.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            btn.Pressed += onPress;
            parent.AddChild(btn);
        }
    }
}
