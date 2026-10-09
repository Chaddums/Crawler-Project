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

        // Only planets that exist. Picking the page's third planet (it had no territory) led to an
        // empty black Territory screen.
        private static readonly Dictionary<string, int> PlanetMap = new()
        {
            { "grid-prime", 1 },
            { "scrapyard", 2 },
            { "veridian-prime", 1 }, // older page ids
            { "xul-thul", 2 },
        };

        public override void _Ready()
        {
            // Skip CEF initialization if AutoPlayer is active (headless or batch mode)
            if (AutoPlayer.Instance?.IsActive == true)
            {
                GD.Print("[MainMenu] AutoPlayer active — skipping UI init");
                return;
            }

            if (CefHelper.Available)
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

            // A backdrop behind the page, so a page that dies or never paints shows it, not grey
            if (_backdrop == null || !IsInstanceValid(_backdrop))
                _backdrop = CefHelper.AddBackdrop(this, "JUNKYARD TD\nLoading...");

            _cefTexture.Set("background_color", new Color(0.055f, 0.055f, 0.055f, 1f));
            CefHelper.Configure(_cefTexture); // CPU rendering unless the project setting asks for GPU sharing

            _cefTexture.Connect("load_finished", Callable.From<string, int>(OnPageLoaded));
            _cefTexture.Connect("load_error", Callable.From<string, int, string>(OnPageError));
            _cefTexture.Connect("ipc_data_message", Callable.From<Variant>(OnIpcData));
            _cefTexture.Connect("console_message", Callable.From<int, string, string, int>(OnConsoleMessage));

            AddChild(cefControl);
            CefHelper.WatchCrash(_cefTexture, "MainMenu", OnPageCrashed);
            CefHelper.WatchPaint(this, "MainMenu", OnPageBlank);

            _cefTexture.Set("url", "res://ui/title/index.html");
            _currentPage = "title";
        }

        private int _crashes;
        private int _blankPages;
        private Control _backdrop;

        /// <summary>The page never drew: a new web view once, then the native menu.</summary>
        private void OnPageBlank()
        {
            _blankPages++;
            CleanupCef();
            if (_blankPages == 1) { CreateCefMenu(); return; }
            if (_backdrop != null && IsInstanceValid(_backdrop)) _backdrop.QueueFree();
            BuildFallbackUI();
        }

        /// <summary>The page died: reload it once; if it dies again, use the native menu.</summary>
        private void OnPageCrashed()
        {
            _crashes++;
            if (_crashes == 1 && _cefTexture != null)
            {
                _cefTexture.Set("url", _currentPage == "planet-select" ? "res://ui/code.html" : "res://ui/title/index.html");
                return;
            }
            CleanupCef();
            BuildFallbackUI();
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
                    if (_currentPage == "title")
                    {
                        // Show unspent perk points on the Command Center button
                        int points = GameManager.Instance?.MetaSave?.AvailablePoints ?? 0;
                        _cefTexture?.Call("eval", $"window.setPerkPoints && window.setPerkPoints({points});");
                    }
                    break;
            }
        }

        private void HandleMenuSelect(string item)
        {
            switch (item)
            {
                case "new-game":
                case "continue":
                case "load-game":
                    // Play: the Command Center is home base (deploy, perk tree, territory)
                    GD.Print($"[MainMenu] {item} -> Command Center");
                    GameManager.Instance?.ShowMetaHub();
                    break;

                case "back":
                    _cefTexture.Set("url", "res://ui/title/index.html");
                    _currentPage = "title";
                    _selectedPlanet = 0;
                    break;

                case "settings":
                    SettingsScreen.Open(this);
                    break;

                case "exit":
                    GetTree().Quit();
                    break;
            }
        }

        private void LaunchRun(string button)
        {
            if (_launched) return;

            int planet = _selectedPlanet is 1 or 2 ? _selectedPlanet : 1;
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

            // Play goes to the Command Center, home base between runs (it used to go straight to
            // planet select, so a new player never saw the Command Center until a run ended)
            int pts = GameManager.Instance?.MetaSave?.AvailablePoints ?? 0;
            AddFallbackButton(vbox, pts > 0 ? $"Play  ({pts} perk point{(pts == 1 ? "" : "s")} to spend)" : "Play",
                () => GameManager.Instance?.ShowMetaHub());

            var spacer2 = new Control();
            spacer2.CustomMinimumSize = new Vector2(0, 10);
            vbox.AddChild(spacer2);

            AddFallbackButton(vbox, "Settings", () => SettingsScreen.Open(this));
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
