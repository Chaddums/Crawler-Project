using Godot;
using System.Collections.Generic;

namespace JunkyardTD
{
    /// <summary>
    /// Renders the Stitch-designed planet selection screen via godot-cef.
    /// Listens for IPC messages from the HTML UI to set planet + run mode,
    /// then transitions to IntroCinematic.
    /// </summary>
    public partial class PlanetSelectScreen : Control
    {
        private GodotObject _cefTexture;
        private Label _statusLabel;
        private bool _launched;

        // Map Stitch planet IDs to game planet numbers
        private static readonly Dictionary<string, int> PlanetMap = new()
        {
            { "veridian-prime", 1 },
            { "xul-thul", 2 },
            { "scorching-waste", 3 },
            { "hollow-plane", 4 },
        };

        private int _selectedPlanet;

        public override void _Ready()
        {
            // Status label for debugging / fallback
            _statusLabel = new Label();
            _statusLabel.SetAnchorsPreset(LayoutPreset.BottomLeft);
            _statusLabel.Position = new Vector2(10, -30);
            _statusLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.7f, 1f, 0.6f));
            AddChild(_statusLabel);

            if (!ClassDB.ClassExists("CefTexture"))
            {
                SetStatus("godot-cef not found — falling back to direct launch");
                BuildFallbackUI();
                return;
            }

            CreateCefBrowser();
        }

        private void CreateCefBrowser()
        {
            _cefTexture = ClassDB.Instantiate("CefTexture").AsGodotObject();

            if (_cefTexture is not Control cefControl)
            {
                SetStatus("CefTexture is not a Control — check godot-cef version");
                BuildFallbackUI();
                return;
            }

            cefControl.SetAnchorsPreset(LayoutPreset.FullRect);
            cefControl.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            cefControl.SizeFlagsVertical = SizeFlags.ExpandFill;

            _cefTexture.Set("background_color", new Color(0.024f, 0.055f, 0.125f, 1f));
            _cefTexture.Set("enable_accelerated_osr", true);

            _cefTexture.Connect("load_finished", Callable.From<string, int>(OnPageLoaded));
            _cefTexture.Connect("load_error", Callable.From<string, int, string>(OnPageError));
            _cefTexture.Connect("ipc_data_message", Callable.From<Variant>(OnIpcData));
            _cefTexture.Connect("console_message", Callable.From<int, string, string, int>(OnConsoleMessage));

            AddChild(cefControl);
            // Move status label to front so it renders on top
            MoveChild(_statusLabel, -1);

            _cefTexture.Set("url", "res://ui/code.html");
            SetStatus("Loading planet selection...");
        }

        private void OnPageLoaded(string url, int httpStatus)
        {
            SetStatus("");

            // Inject bridge so the HTML can call back
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
        }

        private void OnPageError(string url, int errorCode, string errorText)
        {
            SetStatus($"Load error: {errorText}");
            GD.PrintErr($"[PlanetSelect] CEF error: {errorText} ({errorCode}) for {url}");
        }

        private void OnIpcData(Variant data)
        {
            if (_launched) return;
            if (data.VariantType != Variant.Type.Dictionary) return;

            var dict = data.AsGodotDictionary();
            string action = dict.ContainsKey("action") ? dict["action"].AsString() : "";
            var actionData = dict.ContainsKey("data")
                ? dict["data"].AsGodotDictionary()
                : new Godot.Collections.Dictionary();

            switch (action)
            {
                case "planet-selected":
                    string planetId = actionData.ContainsKey("planet")
                        ? actionData["planet"].AsString() : "";
                    if (PlanetMap.TryGetValue(planetId, out int planetNum))
                    {
                        _selectedPlanet = planetNum;
                        SetStatus($"Selected: {planetId}");
                        GD.Print($"[PlanetSelect] Planet selected: {planetId} -> P{planetNum}");
                    }
                    break;

                case "button-clicked":
                    string button = actionData.ContainsKey("button")
                        ? actionData["button"].AsString() : "";
                    LaunchRun(button);
                    break;

                case "ready":
                    GD.Print("[PlanetSelect] Stitch UI ready");
                    break;
            }
        }

        private void LaunchRun(string button)
        {
            if (_launched) return;

            // Default to planet 1 if none selected
            int planet = _selectedPlanet > 0 ? _selectedPlanet : 1;

            RunMode mode = button switch
            {
                "attempt-invasion" => RunMode.Invasion,
                _ => RunMode.Harvest
            };

            _launched = true;
            GD.Print($"[PlanetSelect] Launching P{planet} mode={mode}");
            GameManager.Instance?.LaunchFromPlanetSelect(planet, mode);
        }

        private void OnConsoleMessage(int level, string message, string source, int line)
        {
            string levelStr = level switch { 0 => "DBG", 1 => "INF", 2 => "WRN", 3 => "ERR", _ => "LOG" };
            GD.Print($"[CEF {levelStr}] {message}");
        }

        private void BuildFallbackUI()
        {
            // Simple fallback if CEF isn't available
            var bg = new ColorRect();
            bg.SetAnchorsPreset(LayoutPreset.FullRect);
            bg.Color = new Color(0.04f, 0.06f, 0.12f);
            AddChild(bg);

            var center = new CenterContainer();
            center.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(center);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 16);
            center.AddChild(vbox);

            var title = new Label();
            title.Text = "SELECT TARGET";
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.AddThemeFontSizeOverride("font_size", 36);
            title.AddThemeColorOverride("font_color", new Color(0.5f, 0.8f, 1f));
            vbox.AddChild(title);

            var hint = new Label();
            hint.Text = "(Install godot-cef for the full Stitch UI experience)";
            hint.HorizontalAlignment = HorizontalAlignment.Center;
            hint.AddThemeFontSizeOverride("font_size", 14);
            hint.AddThemeColorOverride("font_color", new Color(0.4f, 0.4f, 0.5f));
            vbox.AddChild(hint);

            string[] planets = { "Grid Prime", "Scrapyard" };
            for (int i = 0; i < planets.Length; i++)
            {
                int planetNum = i + 1;
                var btn = new Button();
                btn.Text = planets[i];
                btn.CustomMinimumSize = new Vector2(200, 50);
                btn.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
                btn.Pressed += () =>
                {
                    _selectedPlanet = planetNum;
                    LaunchRun("harvest-resources");
                };
                vbox.AddChild(btn);
            }

            var backBtn = new Button();
            backBtn.Text = "Back";
            backBtn.CustomMinimumSize = new Vector2(200, 40);
            backBtn.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            backBtn.Pressed += () => GameManager.Instance?.ReturnToMainMenu();
            vbox.AddChild(backBtn);
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event.IsActionPressed("ui_cancel"))
                GameManager.Instance?.ReturnToMainMenu();
        }

        private void SetStatus(string text)
        {
            if (_statusLabel != null)
                _statusLabel.Text = text;
            if (!string.IsNullOrEmpty(text))
                GD.Print($"[PlanetSelect] {text}");
        }
    }
}
