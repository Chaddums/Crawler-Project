using System.Linq;
using Godot;
using Godot.Collections;

namespace JunkyardTD
{
    /// <summary>
    /// UX11: Debrief screen — shows extraction results, transfers meta resources, optional suit capture.
    /// CEF bridge renders ui/debrief/index.html.
    /// </summary>
    public partial class DebriefScreen : Control
    {
        private GodotObject _cefTexture;
        private bool _suitSaved;

        public override void _Ready()
        {
            // Transfer extracted resources to meta save
            TransferResources();

            if (CefHelper.Available)
                CreateCefBrowser();
            else
                BuildFallbackUI();
        }

        private void TransferResources()
        {
            var gm = GameManager.Instance;
            if (gm?.MetaSave == null) return;

            int extracted = gm.TotalExtracted;
            gm.MetaSave.MetaResources += extracted;
            gm.MetaSave.RunCount++;
            MetaPerkSave.Save(gm.MetaSave);

            GD.Print($"[Debrief] Transferred {extracted} to meta resources (total: {gm.MetaSave.MetaResources}), run #{gm.MetaSave.RunCount}");
        }

        public override void _ExitTree()
        {
            CleanupCef();
        }

        private void CleanupCef()
        {
            if (_cefTexture == null) return;
            // Navigate away to release page resources before destroying
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
                _backdrop = CefHelper.AddBackdrop(this, "DEBRIEF\nEnter: continue to the Command Center");
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
            CefHelper.WatchCrash(_cefTexture, "Debrief", () => _cefTexture?.Set("url", "res://ui/debrief/index.html"));
            // A web view that never paints showed Godot's grey clear colour with no way out:
            // make a new one, and if that doesn't draw either, use the built-in screen
            CefHelper.WatchPaint(this, "Debrief", OnPageBlank);

            _cefTexture.Set("url", "res://ui/debrief/index.html");
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            // Keys always work here, whatever the page is doing with the mouse
            if (@event is InputEventKey { Pressed: true, Echo: false } k
                && (k.Keycode == Key.Enter || k.Keycode == Key.KpEnter || k.Keycode == Key.Escape))
            {
                GetViewport().SetInputAsHandled();
                GameManager.Instance?.ShowMetaHub();
            }
        }

        private void OnPageLoaded(string url, int httpStatus)
        {
            GD.Print($"[Debrief] Loaded: {url} (HTTP {httpStatus})");

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

            PushDebriefData();
        }

        private void PushDebriefData()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            // CurrentPhase is already Debrief here — use the outcome captured in ShowDebrief
            bool isVictory = gm.LastRunVictory;
            int totalExtracted = gm.TotalExtracted;
            int metaGained = totalExtracted; // 1:1 transfer for now
            int deepestWave = gm.CurrentWave;
            int coreLives = gm.CoreLives;
            string runMode = gm.CurrentRunMode.ToString();
            string planetName = $"P{gm.CurrentPlanet}";

            string victoryJs = isVictory ? "true" : "false";
            _cefTexture.Call("eval",
                $"window.__debriefUI.init({victoryJs}, {totalExtracted}, {metaGained}, {deepestWave}, {coreLives}, '{EscapeJs(runMode)}', '{EscapeJs(planetName)}')");

            // Push territory conquest info when a site was cleared for the first time this run
            // (farming runs secure a site by reaching its clear wave; boss runs by winning)
            if (!string.IsNullOrEmpty(gm.NewlyClearedSiteId))
            {
                var site = TerritoryManager.GetSite(gm.NewlyClearedSiteId);
                string siteName = site?.Name ?? gm.NewlyClearedSiteId;

                // This clear may have completed its region
                string conqueredRegion = null;
                var region = TerritoryManager.GetRegionForSite(gm.NewlyClearedSiteId);
                if (region != null && TerritoryManager.IsRegionConquered(region.Id, gm.MetaSave))
                    conqueredRegion = region.Name;

                string regionJs = conqueredRegion != null ? $"'{EscapeJs(conqueredRegion)}'" : "null";
                _cefTexture.Call("eval",
                    $"if(window.__debriefUI.showConquest) window.__debriefUI.showConquest('{EscapeJs(siteName)}', {regionJs});");
            }

            string campaign = CampaignLine(gm);
            if (campaign.Length > 0)
                _cefTexture.Call("eval", $"if(window.__debriefUI.showCampaign) window.__debriefUI.showCampaign('{EscapeJs(campaign)}');");

            if (gm.MetaPointsEarnedThisRun > 0)
                _cefTexture.Call("eval",
                    $"if(window.__debriefUI.showPerkPoints) window.__debriefUI.showPerkPoints({gm.MetaPointsEarnedThisRun});");

            // Show suit capture prompt for farming runs with available slots and a build to save
            if (gm.CurrentRunMode != RunMode.BossRun && gm.PendingSuitSnapshot?.Nodes.Count > 0)
            {
                var suits = SuitManager.GetAll();
                int availableSlots = 0;
                for (int i = 0; i < Constants.MAX_SUIT_SLOTS; i++)
                {
                    if (suits[i] == null || suits[i].Consumed || suits[i].Nodes.Count == 0)
                        availableSlots++;
                }

                if (availableSlots > 0)
                {
                    string suggested = $"P{gm.CurrentPlanet} W{deepestWave} Build";
                    _cefTexture.Call("eval",
                        $"window.__debriefUI.showSuitPrompt({availableSlots}, '{EscapeJs(suggested)}')");
                }
            }
        }

        private void OnPageError(string url, int errorCode, string errorText)
        {
            GD.PrintErr($"[Debrief] CEF error: {errorText} ({errorCode}) for {url}");
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
                case "continue-to-hub":
                    GameManager.Instance?.ShowMetaHub();
                    break;

                case "save-suit":
                    if (!_suitSaved)
                        HandleSaveSuit(actionData);
                    break;

                case "skip-suit":
                    GD.Print("[Debrief] Suit capture skipped");
                    break;

                case "ready":
                    GD.Print("[Debrief] Screen ready");
                    break;
            }
        }

        private void HandleSaveSuit(Godot.Collections.Dictionary actionData)
        {
            string name = actionData.ContainsKey("name") ? actionData["name"].AsString() : "Unnamed Suit";
            var gm = GameManager.Instance;
            if (gm == null) return;

            // The battle grid is gone by now — save the snapshot taken when the run ended
            if (gm.PendingSuitSnapshot == null)
            {
                GD.PushWarning("[Debrief] Cannot capture suit — no build snapshot from the run");
                _cefTexture?.Call("eval", "window.__debriefUI.setSuitSaveResult(false, -1)");
                return;
            }

            int slot = SuitManager.SaveSnapshot(gm.PendingSuitSnapshot, name);

            if (slot >= 0)
            {
                _suitSaved = true;
                _cefTexture?.Call("eval", $"window.__debriefUI.setSuitSaveResult(true, {slot})");
                GD.Print($"[Debrief] Suit '{name}' saved to slot {slot}");
            }
            else
            {
                _cefTexture?.Call("eval", "window.__debriefUI.setSuitSaveResult(false, -1)");
            }
        }

        private void OnConsoleMessage(int level, string message, string source, int line)
        {
            string lvl = level switch { 0 => "DBG", 1 => "INF", 2 => "WRN", 3 => "ERR", _ => "LOG" };
            GD.Print($"[CEF {lvl}] {message}");
        }

        private static string EscapeJs(string s)
        {
            return s.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n").Replace("\r", "");
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

            var gm = GameManager.Instance;
            bool isVictory = gm?.LastRunVictory == true;

            var title = new Label();
            title.Text = isVictory ? "EXTRACTION COMPLETE" : "EXTRACTION TERMINATED";
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.AddThemeFontSizeOverride("font_size", 36);
            title.AddThemeColorOverride("font_color",
                isVictory ? new Color(0.3f, 0.87f, 0.5f) : new Color(0.98f, 0.75f, 0.14f));
            vbox.AddChild(title);

            var score = new Label();
            score.Text = $"{gm?.TotalExtracted ?? 0}";
            score.HorizontalAlignment = HorizontalAlignment.Center;
            score.AddThemeFontSizeOverride("font_size", 64);
            score.AddThemeColorOverride("font_color", new Color(0.3f, 0.87f, 0.5f));
            vbox.AddChild(score);

            var caption = new Label();
            caption.Text = "RESOURCES EXTRACTED";
            caption.HorizontalAlignment = HorizontalAlignment.Center;
            caption.AddThemeFontSizeOverride("font_size", 14);
            caption.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.45f));
            vbox.AddChild(caption);

            int perkPoints = gm?.MetaPointsEarnedThisRun ?? 0;
            if (perkPoints > 0)
            {
                var perks = MetaUiStyle.Label(
                    $"+{perkPoints} PERK POINT{(perkPoints == 1 ? "" : "S")} · spend them on the Perk Tree",
                    16, MetaUiStyle.Currency, HorizontalAlignment.Center);
                vbox.AddChild(perks);
            }

            string campaign = CampaignLine(gm);
            if (campaign.Length > 0)
            {
                var camp = MetaUiStyle.Label(campaign, 16, MetaUiStyle.Text, HorizontalAlignment.Center);
                camp.Name = "CampaignLine";
                camp.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                camp.CustomMinimumSize = new Vector2(640, 0);
                vbox.AddChild(camp);
            }

            var spacer = new Control();
            spacer.CustomMinimumSize = new Vector2(0, 20);
            vbox.AddChild(spacer);

            var btn = new Button();
            btn.Text = "Continue to Hub";
            btn.CustomMinimumSize = new Vector2(250, 50);
            btn.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            btn.Pressed += () => GameManager.Instance?.ShowMetaHub();
            vbox.AddChild(btn);
        }

        /// <summary>
        /// The campaign after this run, in a line: the site's state (secured, or how close) and
        /// its region's progress, so a run that fell short still says what it's working toward.
        /// </summary>
        public static string CampaignLine(GameManager gm)
        {
            if (gm == null || gm.IsBossRun || string.IsNullOrEmpty(gm.CurrentTerritorySectionId)) return "";
            var site = TerritoryManager.GetSite(gm.CurrentTerritorySectionId);
            if (site == null || site.IsBossSite) return "";
            bool secured = TerritoryManager.IsSiteCleared(site.Id, gm.MetaSave);
            string state = secured
                ? (gm.NewlyClearedSiteId == site.Id ? $"{site.Name} secured this run." : $"{site.Name} is secured.")
                : $"{site.Name} not secured yet: this run reached wave {gm.CurrentWave} of {site.ClearWave}.";
            string region = TerritoryManager.RegionProgressLine(site.Id, gm.MetaSave);
            var next = TerritoryManager.NextSite(gm.MetaSave, gm.CurrentPlanet);
            string after = next != null && next.Id != site.Id ? $" Next: {next.Name}." : "";
            return $"{state} {region}{after}".Trim();
        }
    }
}
