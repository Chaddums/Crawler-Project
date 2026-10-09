using System.Linq;
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
            {
                cefNode.QueueFree();
            }
            _cefTexture = null;
        }

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
                _backdrop = CefHelper.AddBackdrop(this, "COMMAND CENTER\nEsc: title screen");
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
            CefHelper.Configure(_cefTexture); // CPU rendering unless the project setting asks for GPU sharing

            _cefTexture.Connect("load_finished", Callable.From<string, int>(OnPageLoaded));
            _cefTexture.Connect("load_error", Callable.From<string, int, string>(OnPageError));
            _cefTexture.Connect("ipc_data_message", Callable.From<Variant>(OnIpcData));
            _cefTexture.Connect("console_message", Callable.From<int, string, string, int>(OnConsoleMessage));

            AddChild(cefControl);
            // A page whose render process dies draws nothing (grey, no way out): load it again
            CefHelper.WatchCrash(_cefTexture, "MetaHub", () => _cefTexture?.Set("url", "res://ui/meta-hub/index.html"));
            // A web view that never paints showed Godot's grey clear colour with no way out:
            // make a new one, and if that doesn't draw either, use the built-in screen
            CefHelper.WatchPaint(this, "MetaHub", OnPageBlank);

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
            // RelicInventory, not RelicManager — the manager only exists inside a battle
            int ownedRelics = RelicInventory.OwnedCount;
            int totalRelics = RelicRegistry.All.Length;
            _cefTexture.Call("eval", $"window.__metaHubUI.setRelicCount({ownedRelics}, {totalRelics})");

            // Territory — sites cleared across all planets (UnlockedTerritories belongs to the
            // retired cost-unlock system and is never filled, so this always read 0)
            int cleared = 0, totalSections = 0;
            foreach (var kvp in TerritoryLoader.LoadAll())
            {
                var (c, t) = TerritoryManager.GetPlanetProgress(kvp.Key, gm.MetaSave);
                cleared += c;
                totalSections += t;
            }
            _cefTexture.Call("eval", $"window.__metaHubUI.setTerritoryCount({cleared}, {totalSections})");

            // Perk tree: unspent points get a badge so earned points aren't missed
            var (points, owned) = PerkSummary(gm);
            _cefTexture.Call("eval", $"window.__metaHubUI.setPerkPoints({points}, {owned}, {MetaPerkRegistry.TotalCost})");

            // The campaign's next step on the Deploy card
            var camp = TerritoryManager.Campaign(gm.MetaSave, gm.CurrentPlanet);
            var d = new Godot.Collections.Dictionary
            {
                ["site"] = camp.Site?.Name ?? "", ["planet"] = camp.PlanetName, ["region"] = camp.RegionName,
                ["line"] = camp.Line, ["objective"] = camp.Objective, ["done"] = camp.Done,
                ["cleared"] = new Godot.Collections.Array(camp.Cleared.Select(b => Variant.From(b))),
                ["names"] = new Godot.Collections.Array(camp.Names.Select(n => Variant.From(n))),
            };
            _cefTexture.Call("eval", $"window.__metaHubUI.setCampaign && window.__metaHubUI.setCampaign('{EscapeJs(Json.Stringify(d))}')");
        }

        /// <summary>Start the campaign's next site (Deploy), or open the map when every site is secured.</summary>
        public static void DeployNext()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            var c = TerritoryManager.Campaign(gm.MetaSave, gm.CurrentPlanet);
            if (c.Site == null) { gm.ShowTerritory(); return; }
            GD.Print($"[MetaHub] Deploy: P{c.Planet} {c.Site.Id}");
            gm.LaunchFromTerritorySection(c.Planet, c.Site.Id);
        }

        /// <summary>Unspent points and points spent on the tree.</summary>
        private static (int points, int spent) PerkSummary(GameManager gm)
        {
            var save = gm?.MetaSave ?? MetaPerkSave.Load();
            return (save.AvailablePoints, MetaPerkRegistry.SpentFixed(save));
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
                case "perks":
                    gm.ShowMetaPerkTree();
                    break;
                case "start-run":
                    gm.StartPlanetSelect();
                    break;
                case "deploy":
                    DeployNext();
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

            // The campaign's next step, first: what to play and what it opens
            var gm0 = GameManager.Instance;
            var camp = TerritoryManager.Campaign(gm0?.MetaSave, gm0?.CurrentPlanet ?? 1);
            var kicker = new Label { Text = camp.Done ? "EVERY SITE SECURED" : "NEXT DEPLOYMENT", HorizontalAlignment = HorizontalAlignment.Center };
            kicker.AddThemeFontSizeOverride("font_size", 14);
            kicker.AddThemeColorOverride("font_color", new Color(0.4f, 0.99f, 0.9f));
            vbox.AddChild(kicker);
            if (!camp.Done)
            {
                var where = new Label { Name = "DeployWhere", HorizontalAlignment = HorizontalAlignment.Center,
                    Text = $"{camp.Site.Name}  ·  {camp.PlanetName}, {camp.RegionName}  ·  {camp.Objective}" };
                where.AddThemeFontSizeOverride("font_size", 18);
                vbox.AddChild(where);
                var pips = string.Join("  ", camp.Cleared.Select((done, i) => done ? "■" : camp.Names[i] == camp.Site.Name ? "▣" : "□"));
                var pipLabel = new Label { Text = pips, HorizontalAlignment = HorizontalAlignment.Center };
                pipLabel.AddThemeFontSizeOverride("font_size", 18);
                pipLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.99f, 0.9f));
                vbox.AddChild(pipLabel);
            }
            var line = new Label { Name = "DeployLine", Text = camp.Line, HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(560, 0) };
            line.AddThemeFontSizeOverride("font_size", 15);
            line.AddThemeColorOverride("font_color", new Color(0.75f, 0.8f, 0.88f));
            vbox.AddChild(line);
            AddNavButton(vbox, camp.Done ? "Open the territory map  ▶" : $"Deploy: {camp.Site.Name}  ▶", DeployNext);

            var gap = new Control { CustomMinimumSize = new Vector2(0, 8) };
            vbox.AddChild(gap);
            AddNavButton(vbox, "Territory", () => GameManager.Instance?.ShowTerritory());
            AddNavButton(vbox, "Suits", () => GameManager.Instance?.ShowSuitInventory());
            AddNavButton(vbox, "Relics", () => GameManager.Instance?.ShowRelicInventory());
            var (points, _) = PerkSummary(GameManager.Instance);
            AddNavButton(vbox, points > 0 ? $"Perk Tree ({points} to spend)" : "Perk Tree",
                () => GameManager.Instance?.ShowMetaPerkTree());
            AddNavButton(vbox, "Choose planet & site", () => GameManager.Instance?.StartPlanetSelect());

            var spacer = new Control();
            spacer.CustomMinimumSize = new Vector2(0, 20);
            vbox.AddChild(spacer);

            AddNavButton(vbox, "Main Menu", () => GameManager.Instance?.ReturnToMainMenu());
        }

        private static void AddNavButton(VBoxContainer parent, string text, System.Action onPress)
        {
            var btn = new Button();
            btn.Text = text;
            btn.CustomMinimumSize = new Vector2(320, 50);
            btn.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            btn.Pressed += onPress;
            parent.AddChild(btn);
        }
    }
}
