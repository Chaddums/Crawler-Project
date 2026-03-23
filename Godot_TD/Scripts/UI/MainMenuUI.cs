using Godot;
using System.Collections.Generic;

namespace JunkyardTD
{
    /// <summary>
    /// Main menu — renders Stitch UI via godot-cef.
    /// Starts on title screen, navigates to planet select on New Game/Continue.
    /// Falls back to native Godot UI if CEF is unavailable.
    /// </summary>
    public partial class MainMenuUI : Control
    {
        public static bool StartOnPlanetSelect;

        private GodotObject _cefTexture;
        private bool _launched;
        private string _currentPage = "title";
        private int _selectedPlanet;

        private static readonly Dictionary<string, int> PlanetMap = new()
        {
            { "veridian-prime", 1 },
            { "xul-thul", 2 },
            { "scorching-waste", 3 },
            { "hollow-plane", 4 },
        };

        public override void _Ready()
        {
            if (ClassDB.ClassExists("CefTexture"))
                CreateCefMenu();
            else
                BuildFallbackUI();

            if (StartOnPlanetSelect)
            {
                StartOnPlanetSelect = false;
                if (_cefTexture != null)
                {
                    _cefTexture.Set("url", "res://ui/code.html");
                    _currentPage = "planet-select";
                    _selectedPlanet = 0;
                }
            }
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

        // ── CEF-based menu ──

        private void CreateCefMenu()
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

            _cefTexture.Set("url", "res://ui/title/index.html");
            _currentPage = "title";
        }

        private void OnPageLoaded(string url, int httpStatus)
        {
            GD.Print($"[MainMenu] Loaded: {url} (HTTP {httpStatus})");

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
        }

        private void OnPageError(string url, int errorCode, string errorText)
        {
            GD.PrintErr($"[MainMenu] CEF error: {errorText} ({errorCode}) for {url}");
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
                case "menu-select":
                    string item = actionData.ContainsKey("item")
                        ? actionData["item"].AsString() : "";
                    HandleMenuSelect(item);
                    break;

                case "planet-selected":
                    string planetId = actionData.ContainsKey("planet")
                        ? actionData["planet"].AsString() : "";
                    if (PlanetMap.TryGetValue(planetId, out int planetNum))
                    {
                        _selectedPlanet = planetNum;
                        GD.Print($"[MainMenu] Planet selected: {planetId} -> P{planetNum}");
                    }
                    break;

                case "button-clicked":
                    string button = actionData.ContainsKey("button")
                        ? actionData["button"].AsString() : "";
                    LaunchRun(button);
                    break;

                case "ready":
                    GD.Print($"[MainMenu] Screen ready: {_currentPage}");
                    break;
            }
        }

        private void HandleMenuSelect(string item)
        {
            switch (item)
            {
                case "new-game":
                case "continue":
                    // Navigate CEF to planet select
                    _cefTexture.Set("url", "res://ui/code.html");
                    _currentPage = "planet-select";
                    _selectedPlanet = 0;
                    GD.Print($"[MainMenu] {item} -> planet select");
                    break;

                case "load-game":
                    GameEvents.ClearAll();
                    if (TransitionManager.Instance != null)
                        TransitionManager.Instance.TransitionToScene(Constants.SCENE_LOADOUTS);
                    else
                        GetTree().ChangeSceneToFile(Constants.SCENE_LOADOUTS);
                    break;

                case "back":
                    _cefTexture.Set("url", "res://ui/title/index.html");
                    _currentPage = "title";
                    _selectedPlanet = 0;
                    break;

                case "settings":
                    _cefTexture?.Call("eval",
                        "var d=document.createElement('div');" +
                        "d.style.cssText='position:fixed;top:50%;left:50%;transform:translate(-50%,-50%);" +
                        "background:#171f33;border:1px solid rgba(125,211,252,0.3);padding:24px 40px;" +
                        "color:#c5eaff;font-family:Space Grotesk;font-size:18px;z-index:999;border-radius:8px';" +
                        "d.textContent='Settings — coming soon';" +
                        "document.body.appendChild(d);" +
                        "setTimeout(function(){d.remove()},2000)");
                    break;

                case "exit":
                    GetTree().Quit();
                    break;
            }
        }

        private void LaunchRun(string button)
        {
            if (_launched) return;

            int planet = _selectedPlanet > 0 ? _selectedPlanet : 1;
            RunMode mode = button switch
            {
                "attempt-invasion" => RunMode.Invasion,
                _ => RunMode.Harvest
            };

            _launched = true;
            GD.Print($"[MainMenu] Launching P{planet} mode={mode}");
            GameManager.Instance?.LaunchFromPlanetSelect(planet, mode);
        }

        private void OnConsoleMessage(int level, string message, string source, int line)
        {
            string lvl = level switch { 0 => "DBG", 1 => "INF", 2 => "WRN", 3 => "ERR", _ => "LOG" };
            GD.Print($"[CEF {lvl}] {message}");
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event.IsActionPressed("ui_cancel"))
            {
                if (_currentPage == "planet-select" && _cefTexture != null)
                {
                    // ESC from planet select -> back to title
                    _cefTexture.Set("url", "res://ui/title/index.html");
                    _currentPage = "title";
                    _selectedPlanet = 0;
                }
            }
        }

        // ── Fallback native UI (no CEF) ──

        private void BuildFallbackUI()
        {
            var bg = new ColorRect();
            bg.SetAnchorsPreset(LayoutPreset.FullRect);
            bg.Color = new Color(0.06f, 0.05f, 0.04f);
            AddChild(bg);

            var center = new CenterContainer();
            center.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(center);

            var vbox = new VBoxContainer();
            vbox.CustomMinimumSize = new Vector2(400, 0);
            vbox.AddThemeConstantOverride("separation", 20);
            center.AddChild(vbox);

            var title = new Label();
            title.Text = "VINE LOGIC TD";
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.AddThemeFontSizeOverride("font_size", 48);
            title.AddThemeColorOverride("font_color", new Color(0.9f, 0.7f, 0.3f));
            vbox.AddChild(title);

            var subtitle = new Label();
            subtitle.Text = "Mine everything you can before they take it all.";
            subtitle.HorizontalAlignment = HorizontalAlignment.Center;
            subtitle.AddThemeFontSizeOverride("font_size", 16);
            subtitle.AddThemeColorOverride("font_color", new Color(0.6f, 0.55f, 0.5f));
            vbox.AddChild(subtitle);

            var spacer = new Control();
            spacer.CustomMinimumSize = new Vector2(0, 30);
            vbox.AddChild(spacer);

            AddFallbackButton(vbox, "New Game", () => GameManager.Instance?.StartPlanetSelect());
            AddFallbackButton(vbox, "Loadouts", () => {
                GameEvents.ClearAll();
                if (TransitionManager.Instance != null)
                        TransitionManager.Instance.TransitionToScene(Constants.SCENE_LOADOUTS);
                    else
                        GetTree().ChangeSceneToFile(Constants.SCENE_LOADOUTS);
            });

            var spacer2 = new Control();
            spacer2.CustomMinimumSize = new Vector2(0, 10);
            vbox.AddChild(spacer2);

            AddFallbackButton(vbox, "Quit", () => GetTree().Quit());
        }

        private void AddFallbackButton(VBoxContainer parent, string text, System.Action onPress)
        {
            var btn = new Button();
            btn.Text = text;
            btn.CustomMinimumSize = new Vector2(200, 50);
            btn.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            btn.Pressed += onPress;
            parent.AddChild(btn);
        }
    }
}
