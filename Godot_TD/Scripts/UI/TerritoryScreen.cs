using System.Collections.Generic;
using System.Linq;
using Godot;
using Godot.Collections;

namespace JunkyardTD
{
    /// <summary>
    /// S4: Territory map screen — Helldivers-style hex region map.
    /// CEF bridge renders ui/territory/index.html; falls back to code-built UI.
    /// </summary>
    public partial class TerritoryScreen : CanvasLayer
    {
        private static readonly Color Accent = new(0.0f, 0.85f, 0.95f);
        private static readonly Color Locked = new(0.3f, 0.3f, 0.3f);
        private static readonly Color Unlocked = new(0.2f, 0.7f, 0.3f);
        private static readonly Color Available = new(0.9f, 0.7f, 0.2f);
        private static readonly Color BossColor = new(0.9f, 0.2f, 0.15f);
        private static readonly Color Cleared = new(0.4f, 0.8f, 1.0f);

        private int _selectedPlanet = 1;
        private MetaPerkSaveData _save;
        private GodotObject _cefTexture;

        // Fallback UI refs
        private Label _resourceLabel;
        private VBoxContainer _sectionContainer;

        public override void _Ready()
        {
            Layer = 10;
            _selectedPlanet = GameManager.Instance?.CurrentPlanet ?? 1;
            _save = GameManager.Instance?.MetaSave ?? MetaPerkSave.Load();

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
                cefNode.QueueFree();
            _cefTexture = null;
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && !key.Echo)
            {
                if (key.Keycode == Key.Escape)
                {
                    GetViewport().SetInputAsHandled();
                    GameManager.Instance?.ShowMetaHub();
                }
            }
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

            // Full-screen background
            var bg = new ColorRect();
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            bg.Color = new Color(0.043f, 0.075f, 0.149f);
            AddChild(bg);

            cefControl.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            cefControl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            cefControl.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

            _cefTexture.Set("background_color", new Color(0.043f, 0.075f, 0.149f, 1f));
            _cefTexture.Set("enable_accelerated_osr", true);

            _cefTexture.Connect("load_finished", Callable.From<string, int>(OnPageLoaded));
            _cefTexture.Connect("load_error", Callable.From<string, int, string>(OnPageError));
            _cefTexture.Connect("ipc_data_message", Callable.From<Variant>(OnIpcData));
            _cefTexture.Connect("console_message", Callable.From<int, string, string, int>(OnConsoleMessage));

            AddChild(cefControl);
            _cefTexture.Set("url", "res://ui/territory/index.html");
        }

        private void OnPageLoaded(string url, int statusCode)
        {
            GD.Print($"[Territory] CEF loaded: {url} ({statusCode})");
            PushPlanetData();
        }

        private void OnPageError(string url, int errorCode, string errorText)
        {
            GD.PrintErr($"[Territory] CEF error: {url} {errorCode} {errorText}");
        }

        private void OnConsoleMessage(int level, string message, string source, int line)
        {
            if (level >= 2) GD.PrintErr($"[Territory/JS] {message} ({source}:{line})");
        }

        private void OnIpcData(Variant data)
        {
            if (data.Obj is not Godot.Collections.Dictionary dict) return;
            string action = dict.ContainsKey("action") ? dict["action"].AsString() : "";
            Godot.Collections.Dictionary payload = null;
            if (dict.ContainsKey("data") && dict["data"].Obj is Godot.Collections.Dictionary d)
                payload = d;

            GD.Print($"[Territory] IPC: {action}");

            switch (action)
            {
                case "back":
                    GameManager.Instance?.ShowMetaHub();
                    break;

                case "site-launch":
                    if (payload != null && payload.ContainsKey("siteId"))
                    {
                        string siteId = payload["siteId"].AsString();
                        GD.Print($"[Territory] Launching site: {siteId}");
                        GameManager.Instance?.LaunchFromTerritorySection(_selectedPlanet, siteId);
                    }
                    break;

                case "boss-run":
                    if (payload != null && payload.ContainsKey("siteId"))
                    {
                        string siteId = payload["siteId"].AsString();
                        GD.Print($"[Territory] Boss run: {siteId}");
                        GameManager.Instance?.ShowBossConfirmation(_selectedPlanet, 0, siteId);
                    }
                    break;

                case "boss-run-region":
                    if (payload != null && payload.ContainsKey("regionId"))
                    {
                        string regionId = payload["regionId"].AsString();
                        var region = TerritoryManager.GetRegion(regionId);
                        var bossSite = region?.Sites?.FirstOrDefault(s => s.IsBossSite);
                        if (bossSite != null)
                        {
                            GD.Print($"[Territory] Boss run from region: {bossSite.Id}");
                            GameManager.Instance?.ShowBossConfirmation(_selectedPlanet, 0, bossSite.Id);
                        }
                    }
                    break;

                case "farming-run":
                    if (payload != null && payload.ContainsKey("regionId"))
                    {
                        string regionId = payload["regionId"].AsString();
                        var region = TerritoryManager.GetRegion(regionId);
                        // Pick first uncleared site, or first site for replay
                        var site = region?.Sites?.FirstOrDefault(s => !TerritoryManager.IsSiteCleared(s.Id, _save))
                                ?? region?.Sites?.FirstOrDefault();
                        if (site != null)
                        {
                            GD.Print($"[Territory] Farming run: {site.Id}");
                            GameManager.Instance?.LaunchFromTerritorySection(_selectedPlanet, site.Id);
                        }
                    }
                    break;

                case "region-selected":
                    // Just informational
                    break;
            }
        }

        private void PushPlanetData()
        {
            var planet = TerritoryManager.GetPlanet(_selectedPlanet);
            if (planet == null) return;

            // Build region data with states
            var regions = new Godot.Collections.Array();
            foreach (var region in planet.Regions)
            {
                string state;
                if (TerritoryManager.IsRegionConquered(region.Id, _save))
                    state = "conquered";
                else if (TerritoryManager.IsRegionAccessible(region.Id, _save))
                    state = "accessible";
                else
                    state = "locked";

                var regionDict = new Godot.Collections.Dictionary
                {
                    ["id"] = region.Id,
                    ["name"] = region.Name ?? "",
                    ["description"] = region.Description ?? "",
                    ["state"] = state,
                    ["isBossRegion"] = region.IsBossRegion,
                    ["requiresRegion"] = region.RequiresRegion ?? "",
                };

                // Buff
                if (region.Buff != null)
                {
                    regionDict["buff"] = new Godot.Collections.Dictionary
                    {
                        ["type"] = region.Buff.Type ?? "",
                        ["value"] = region.Buff.Value,
                        ["label"] = region.Buff.Label ?? "",
                    };
                }

                // Sites
                var sites = new Godot.Collections.Array();
                foreach (var site in region.Sites)
                {
                    sites.Add(new Godot.Collections.Dictionary
                    {
                        ["id"] = site.Id,
                        ["name"] = site.Name ?? "",
                        ["difficulty"] = site.Difficulty,
                        ["mapLayout"] = site.MapLayout ?? "",
                        ["waveSet"] = site.WaveSet ?? "",
                        ["rewardResources"] = site.RewardResources,
                        ["rewardLabel"] = site.RewardLabel ?? "",
                        ["bonusExtractionMult"] = site.BonusExtractionMult,
                        ["isBossSite"] = site.IsBossSite,
                        ["bossId"] = site.BossId ?? "",
                        ["bossWave"] = site.BossWave,
                        ["cleared"] = TerritoryManager.IsSiteCleared(site.Id, _save),
                    });
                }
                regionDict["sites"] = sites;

                regions.Add(regionDict);
            }

            var planetDict = new Godot.Collections.Dictionary
            {
                ["name"] = planet.Name ?? "",
                ["planetId"] = planet.PlanetId,
                ["regions"] = regions,
            };

            string json = Json.Stringify(planetDict);
            string js = $"window.__territoryUI.setPlanet({json});";
            _cefTexture?.Call("eval", js);

            // Resources
            int resources = GameManager.Instance?.MetaSave?.MetaResources ?? 0;
            _cefTexture?.Call("eval", $"window.__territoryUI.setResources({resources});");

            // Progress
            var (cleared, total) = TerritoryManager.GetPlanetProgress(_selectedPlanet, _save);
            _cefTexture?.Call("eval", $"window.__territoryUI.updateProgress({cleared},{total});");
        }

        // ── Fallback (code-built) ──

        private void BuildFallbackUI()
        {
            var bg = new ColorRect();
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            bg.Color = TronTheme.Background;
            AddChild(bg);

            var margin = new MarginContainer();
            margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            margin.AddThemeConstantOverride("margin_left", 40);
            margin.AddThemeConstantOverride("margin_right", 40);
            margin.AddThemeConstantOverride("margin_top", 30);
            margin.AddThemeConstantOverride("margin_bottom", 30);
            AddChild(margin);

            var root = new VBoxContainer();
            root.AddThemeConstantOverride("separation", 20);
            margin.AddChild(root);

            // Header
            var headerRow = new HBoxContainer();
            headerRow.AddThemeConstantOverride("separation", 20);
            root.AddChild(headerRow);

            var title = new Label();
            title.Text = "TERRITORY";
            title.AddThemeFontSizeOverride("font_size", 36);
            title.AddThemeColorOverride("font_color", Accent);
            headerRow.AddChild(title);

            var spacer = new Control();
            spacer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            headerRow.AddChild(spacer);

            _resourceLabel = new Label();
            _resourceLabel.AddThemeFontSizeOverride("font_size", 22);
            _resourceLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.3f));
            headerRow.AddChild(_resourceLabel);
            UpdateResourceDisplay();

            // Planet tabs
            var tabRow = new HBoxContainer();
            tabRow.AddThemeConstantOverride("separation", 8);
            root.AddChild(tabRow);

            var planets = TerritoryLoader.LoadAll();
            foreach (var kvp in planets)
            {
                var planetBtn = new Button();
                planetBtn.Text = $"P{kvp.Key}: {kvp.Value.Name}";
                planetBtn.AddThemeFontSizeOverride("font_size", 16);
                int pid = kvp.Key;
                planetBtn.Pressed += () => SelectPlanet(pid);

                var style = new StyleBoxFlat();
                style.BgColor = pid == _selectedPlanet
                    ? new Color(Accent.R * 0.2f, Accent.G * 0.2f, Accent.B * 0.2f)
                    : new Color(0.05f, 0.05f, 0.08f);
                style.BorderColor = pid == _selectedPlanet ? Accent : Locked;
                style.SetBorderWidthAll(2);
                style.SetCornerRadiusAll(4);
                style.ContentMarginLeft = 16;
                style.ContentMarginRight = 16;
                style.ContentMarginTop = 8;
                style.ContentMarginBottom = 8;
                planetBtn.AddThemeStyleboxOverride("normal", style);
                tabRow.AddChild(planetBtn);
            }

            // Scrollable sections
            var scroll = new ScrollContainer();
            scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            root.AddChild(scroll);

            _sectionContainer = new VBoxContainer();
            _sectionContainer.AddThemeConstantOverride("separation", 12);
            _sectionContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            scroll.AddChild(_sectionContainer);

            PopulateSections();

            // Bottom bar
            var bottomRow = new HBoxContainer();
            bottomRow.AddThemeConstantOverride("separation", 12);
            root.AddChild(bottomRow);

            var backBtn = new Button();
            backBtn.Text = "Back";
            backBtn.AddThemeFontSizeOverride("font_size", 18);
            backBtn.Pressed += () => GameManager.Instance?.ShowMetaHub();
            backBtn.AddThemeStyleboxOverride("normal", CreateButtonStyle(new Color(0.4f, 0.4f, 0.4f)));
            bottomRow.AddChild(backBtn);

            var spacer2 = new Control();
            spacer2.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            bottomRow.AddChild(spacer2);

            var farmBtn = new Button();
            farmBtn.Text = "Start Farming Run";
            farmBtn.AddThemeFontSizeOverride("font_size", 18);
            farmBtn.Pressed += () => GameManager.Instance?.LaunchFromPlanetSelect(_selectedPlanet, RunMode.Harvest);
            farmBtn.AddThemeStyleboxOverride("normal", CreateButtonStyle(Unlocked));
            bottomRow.AddChild(farmBtn);
        }

        private void SelectPlanet(int planetId)
        {
            _selectedPlanet = planetId;
            foreach (var child in GetChildren())
                child.QueueFree();
            BuildFallbackUI();
        }

        private void PopulateSections()
        {
            foreach (var child in _sectionContainer.GetChildren())
                child.QueueFree();

            var planet = TerritoryLoader.GetPlanet(_selectedPlanet);
            if (planet == null)
            {
                var empty = new Label();
                empty.Text = "No territory data for this planet.";
                empty.AddThemeFontSizeOverride("font_size", 18);
                empty.AddThemeColorOverride("font_color", Locked);
                _sectionContainer.AddChild(empty);
                return;
            }

            // Show by region
            foreach (var region in planet.Regions)
            {
                // Region header
                bool conquered = TerritoryManager.IsRegionConquered(region.Id, _save);
                bool accessible = TerritoryManager.IsRegionAccessible(region.Id, _save);

                var regionLabel = new Label();
                regionLabel.Text = $"━━ {region.Name} {(conquered ? "[CONQUERED]" : accessible ? "" : "[LOCKED]")}";
                regionLabel.AddThemeFontSizeOverride("font_size", 18);
                regionLabel.AddThemeColorOverride("font_color", conquered ? Cleared : accessible ? Accent : Locked);
                _sectionContainer.AddChild(regionLabel);

                if (region.Buff != null && conquered)
                {
                    var buffLabel = new Label();
                    buffLabel.Text = $"  Buff: {region.Buff.Label}";
                    buffLabel.AddThemeFontSizeOverride("font_size", 13);
                    buffLabel.AddThemeColorOverride("font_color", new Color(0.24f, 0.87f, 0.78f));
                    _sectionContainer.AddChild(buffLabel);
                }

                foreach (var site in region.Sites)
                {
                    var card = CreateSiteCard(site, accessible);
                    _sectionContainer.AddChild(card);
                }
            }
        }

        private PanelContainer CreateSiteCard(TerritorySite site, bool regionAccessible)
        {
            bool isCleared = TerritoryManager.IsSiteCleared(site.Id, _save);
            bool canPlay = regionAccessible;

            Color statusColor = isCleared ? Cleared : canPlay ? (site.IsBossSite ? BossColor : Unlocked) : Locked;
            string statusText = isCleared ? "CLEARED" : canPlay ? (site.IsBossSite ? "BOSS" : "LAUNCH") : "LOCKED";

            var card = new PanelContainer();
            var cardStyle = new StyleBoxFlat();
            cardStyle.BgColor = new Color(0.03f, 0.03f, 0.05f, 0.95f);
            cardStyle.BorderColor = statusColor;
            cardStyle.SetBorderWidthAll(2);
            cardStyle.SetCornerRadiusAll(6);
            cardStyle.ContentMarginLeft = 20;
            cardStyle.ContentMarginRight = 20;
            cardStyle.ContentMarginTop = 10;
            cardStyle.ContentMarginBottom = 10;
            card.AddThemeStyleboxOverride("panel", cardStyle);

            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 20);
            card.AddChild(hbox);

            var info = new VBoxContainer();
            info.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            info.AddThemeConstantOverride("separation", 3);
            hbox.AddChild(info);

            var nameLabel = new Label();
            nameLabel.Text = $"{site.Name}  [Diff {site.Difficulty}]";
            nameLabel.AddThemeFontSizeOverride("font_size", 18);
            nameLabel.AddThemeColorOverride("font_color", statusColor);
            info.AddChild(nameLabel);

            var detailLabel = new Label();
            detailLabel.Text = $"Map: {site.MapLayout} | Waves: {site.WaveSet} | Reward: +{site.RewardResources}";
            detailLabel.AddThemeFontSizeOverride("font_size", 12);
            detailLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
            info.AddChild(detailLabel);

            if (site.IsBossSite)
            {
                var bossLabel = new Label();
                bossLabel.Text = $"BOSS: {site.BossId} — Wave {site.BossWave}";
                bossLabel.AddThemeFontSizeOverride("font_size", 13);
                bossLabel.AddThemeColorOverride("font_color", BossColor);
                info.AddChild(bossLabel);
            }

            // Action
            var actions = new VBoxContainer();
            actions.AddThemeConstantOverride("separation", 4);
            hbox.AddChild(actions);

            var status = new Label();
            status.Text = statusText;
            status.AddThemeFontSizeOverride("font_size", 14);
            status.AddThemeColorOverride("font_color", statusColor);
            status.HorizontalAlignment = HorizontalAlignment.Right;
            actions.AddChild(status);

            if (canPlay)
            {
                if (site.IsBossSite)
                {
                    var bossBtn = new Button();
                    bossBtn.Text = "Boss Run";
                    bossBtn.AddThemeFontSizeOverride("font_size", 14);
                    bossBtn.AddThemeStyleboxOverride("normal", CreateButtonStyle(BossColor));
                    var suits = SuitManager.GetAvailableSuits();
                    if (suits.Count == 0)
                    {
                        bossBtn.Disabled = true;
                        bossBtn.TooltipText = "No suits available";
                    }
                    else
                    {
                        string sid = site.Id;
                        bossBtn.Pressed += () => GameManager.Instance?.ShowBossConfirmation(_selectedPlanet, 0, sid);
                    }
                    actions.AddChild(bossBtn);
                }
                else
                {
                    var launchBtn = new Button();
                    launchBtn.Text = isCleared ? "Replay" : "Launch";
                    launchBtn.AddThemeFontSizeOverride("font_size", 14);
                    launchBtn.CustomMinimumSize = new Vector2(90, 0);
                    launchBtn.AddThemeStyleboxOverride("normal", CreateButtonStyle(isCleared ? Cleared : Unlocked));
                    string siteId = site.Id;
                    launchBtn.Pressed += () => GameManager.Instance?.LaunchFromTerritorySection(_selectedPlanet, siteId);
                    actions.AddChild(launchBtn);
                }
            }

            return card;
        }

        private void UpdateResourceDisplay()
        {
            int resources = GameManager.Instance?.MetaSave?.MetaResources ?? 0;
            if (_resourceLabel != null) _resourceLabel.Text = $"Resources: {resources}";
        }

        private static StyleBoxFlat CreateButtonStyle(Color borderColor)
        {
            var style = new StyleBoxFlat();
            style.BgColor = new Color(borderColor.R * 0.15f, borderColor.G * 0.15f, borderColor.B * 0.15f, 0.9f);
            style.BorderColor = borderColor;
            style.SetBorderWidthAll(2);
            style.SetCornerRadiusAll(4);
            style.ContentMarginLeft = 16;
            style.ContentMarginRight = 16;
            style.ContentMarginTop = 8;
            style.ContentMarginBottom = 8;
            return style;
        }
    }
}
