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

            if (ClassDB.ClassExists("CefTexture"))
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

            _cefTexture.Set("url", "res://ui/debrief/index.html");
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

            bool isVictory = gm.CurrentPhase == GamePhase.Victory;
            int totalExtracted = gm.TotalExtracted;
            int metaGained = totalExtracted; // 1:1 transfer for now
            int deepestWave = gm.CurrentWave;
            int coreLives = gm.CoreLives;
            string runMode = gm.CurrentRunMode.ToString();
            string planetName = $"P{gm.CurrentPlanet}";

            string victoryJs = isVictory ? "true" : "false";
            _cefTexture.Call("eval",
                $"window.__debriefUI.init({victoryJs}, {totalExtracted}, {metaGained}, {deepestWave}, {coreLives}, '{EscapeJs(runMode)}', '{EscapeJs(planetName)}')");

            // Show suit capture prompt for farming runs with available slots
            if (gm.CurrentRunMode == RunMode.Harvest)
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

            // Find VineGrid via ServiceLocator
            VineGrid grid = null;
            ServiceLocator.TryGet<VineGrid>(out grid);

            if (grid == null)
            {
                GD.PushWarning("[Debrief] Cannot capture suit — VineGrid not found in ServiceLocator");
                _cefTexture?.Call("eval", "window.__debriefUI.setSuitSaveResult(false, -1)");
                return;
            }

            int slot = SuitManager.CaptureSuit(
                grid,
                name,
                gm.SelectedRole,
                gm.CurrentPlanet,
                gm.SelectedMaterialType ?? MaterialType.None
            );

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
            bool isVictory = gm?.CurrentPhase == GamePhase.Victory;

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
    }
}
