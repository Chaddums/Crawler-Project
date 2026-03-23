using Godot;
using Godot.Collections;

namespace JunkyardTD
{
    /// <summary>
    /// Full-screen loot inspection flyout — shown when the player presses [E] on a ground relic.
    /// CEF overlay renders ui/loot-flyout/index.html.
    /// Call ShowRelic() to populate and display; handles claim/abandon via IPC.
    /// </summary>
    public partial class LootFlyoutScreen : Control
    {
        private GodotObject _cefTexture;
        private bool _cefReady;
        private RelicRegistry.Relic _currentRelic;
        private bool _claimed;

        // Pending push if CEF not ready yet
        private RelicRegistry.Relic? _pendingRelic;

        public override void _Ready()
        {
            if (ClassDB.ClassExists("CefTexture"))
                CreateCefBrowser();
            else
                BuildFallbackUI();
        }

        // ── Public API ──

        public void ShowRelic(RelicRegistry.Relic relic)
        {
            _currentRelic = relic;
            _claimed = false;
            Visible = true;

            if (_cefReady && _cefTexture != null)
                PushRelicData(relic);
            else
                _pendingRelic = relic;

            // Update fallback if present
            UpdateFallback(relic);
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

            _cefTexture.Set("background_color", new Color(0.024f, 0.047f, 0.149f, 0.6f));
            _cefTexture.Set("enable_accelerated_osr", true);

            _cefTexture.Connect("load_finished", Callable.From<string, int>(OnPageLoaded));
            _cefTexture.Connect("load_error", Callable.From<string, int, string>(OnPageError));
            _cefTexture.Connect("ipc_data_message", Callable.From<Variant>(OnIpcData));
            _cefTexture.Connect("console_message", Callable.From<int, string, string, int>(OnConsoleMessage));

            AddChild(cefControl);

            _cefTexture.Set("url", "res://ui/loot-flyout/index.html");
        }

        private void OnPageLoaded(string url, int httpStatus)
        {
            GD.Print($"[LootFlyout] Loaded: {url} (HTTP {httpStatus})");

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

            if (_pendingRelic.HasValue)
            {
                PushRelicData(_pendingRelic.Value);
                _pendingRelic = null;
            }
        }

        private void PushRelicData(RelicRegistry.Relic relic)
        {
            string tradeoffJs = relic.Tradeoff != null
                ? $"'{EscapeJs(relic.Tradeoff)}'"
                : "null";

            _cefTexture.Call("eval",
                $"window.__lootFlyoutUI.show('{EscapeJs(relic.Id)}', '{EscapeJs(relic.Name)}', " +
                $"'{EscapeJs(relic.Rarity)}', '{EscapeJs(relic.Icon)}', " +
                $"'{EscapeJs(relic.Desc)}', {tradeoffJs}, " +
                $"'{EscapeJs(relic.Desc)}')");
        }

        private void OnPageError(string url, int errorCode, string errorText)
        {
            GD.PrintErr($"[LootFlyout] CEF error: {errorText} ({errorCode}) for {url}");
        }

        private void OnIpcData(Variant data)
        {
            if (data.VariantType != Variant.Type.Dictionary) return;

            var dict = data.AsGodotDictionary();
            string action = dict.ContainsKey("action") ? dict["action"].AsString() : "";

            switch (action)
            {
                case "claim":
                    if (!_claimed)
                        HandleClaim();
                    break;

                case "abandon":
                    HandleAbandon();
                    break;

                case "ready":
                    GD.Print("[LootFlyout] Screen ready");
                    break;
            }
        }

        private void HandleClaim()
        {
            _claimed = true;
            GameEvents.OnRelicAcquired?.Invoke(_currentRelic.Id, true);
            GD.Print($"[LootFlyout] Claimed relic: {_currentRelic.Id}");

            _cefTexture?.Call("eval", "window.__lootFlyoutUI.setClaimResult(true)");

            // Auto-close after brief delay
            var timer = GetTree().CreateTimer(0.8f);
            timer.Timeout += () => CloseFlyout();
        }

        private void HandleAbandon()
        {
            GD.Print($"[LootFlyout] Abandoned relic: {_currentRelic.Id}");
            CloseFlyout();
        }

        private void CloseFlyout()
        {
            Visible = false;
            // Resume game if paused
            Engine.TimeScale = 1.0;
        }

        private void OnConsoleMessage(int level, string message, string source, int line)
        {
            string lvl = level switch { 0 => "DBG", 1 => "INF", 2 => "WRN", 3 => "ERR", _ => "LOG" };
            GD.Print($"[CEF-Flyout {lvl}] {message}");
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (!Visible) return;

            if (@event.IsActionPressed("ui_cancel"))
            {
                HandleAbandon();
                GetViewport().SetInputAsHandled();
            }
        }

        // ── Fallback native UI ──

        private Label _fbName;
        private Label _fbEffect;
        private Label _fbTradeoff;
        private Label _fbRarity;

        private void BuildFallbackUI()
        {
            var bg = new ColorRect();
            bg.SetAnchorsPreset(LayoutPreset.FullRect);
            bg.Color = new Color(0.02f, 0.05f, 0.15f, 0.85f);
            AddChild(bg);

            var center = new CenterContainer();
            center.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(center);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 12);
            vbox.CustomMinimumSize = new Vector2(400, 0);
            center.AddChild(vbox);

            _fbRarity = new Label();
            _fbRarity.HorizontalAlignment = HorizontalAlignment.Center;
            _fbRarity.AddThemeFontSizeOverride("font_size", 12);
            vbox.AddChild(_fbRarity);

            _fbName = new Label();
            _fbName.HorizontalAlignment = HorizontalAlignment.Center;
            _fbName.AddThemeFontSizeOverride("font_size", 32);
            vbox.AddChild(_fbName);

            var sep = new HSeparator();
            vbox.AddChild(sep);

            _fbEffect = new Label();
            _fbEffect.HorizontalAlignment = HorizontalAlignment.Center;
            _fbEffect.AddThemeFontSizeOverride("font_size", 16);
            _fbEffect.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            vbox.AddChild(_fbEffect);

            _fbTradeoff = new Label();
            _fbTradeoff.HorizontalAlignment = HorizontalAlignment.Center;
            _fbTradeoff.AddThemeFontSizeOverride("font_size", 14);
            _fbTradeoff.AddThemeColorOverride("font_color", new Color(1f, 0.7f, 0.67f));
            _fbTradeoff.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            vbox.AddChild(_fbTradeoff);

            var spacer = new Control();
            spacer.CustomMinimumSize = new Vector2(0, 16);
            vbox.AddChild(spacer);

            var btnRow = new HBoxContainer();
            btnRow.AddThemeConstantOverride("separation", 12);
            btnRow.Alignment = BoxContainer.AlignmentMode.Center;
            vbox.AddChild(btnRow);

            var abandonBtn = new Button();
            abandonBtn.Text = "Abandon";
            abandonBtn.CustomMinimumSize = new Vector2(140, 44);
            abandonBtn.Pressed += HandleAbandon;
            btnRow.AddChild(abandonBtn);

            var claimBtn = new Button();
            claimBtn.Text = "Claim Relic";
            claimBtn.CustomMinimumSize = new Vector2(180, 44);
            claimBtn.Pressed += () => { if (!_claimed) HandleClaim(); };
            btnRow.AddChild(claimBtn);
        }

        private void UpdateFallback(RelicRegistry.Relic relic)
        {
            if (_fbName == null) return;

            var col = LootLabelUI.RarityColor(relic.Rarity);
            _fbRarity.Text = relic.Rarity?.ToUpper() ?? "COMMON";
            _fbRarity.AddThemeColorOverride("font_color", col);
            _fbName.Text = relic.Name;
            _fbName.AddThemeColorOverride("font_color", col);
            _fbEffect.Text = relic.Desc;
            _fbTradeoff.Text = relic.Tradeoff ?? "";
            _fbTradeoff.Visible = relic.Tradeoff != null;
        }

        // ── Helpers ──

        private static string EscapeJs(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n").Replace("\r", "");
        }
    }
}
