using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// S4: Territory map screen — unlock planet sections, gate boss runs.
    /// Code-built CanvasLayer (no .tscn UI).
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
        private TerritorySaveData _save;
        private Label _resourceLabel;
        private VBoxContainer _sectionContainer;

        public override void _Ready()
        {
            Layer = 10;
            _save = GameManager.Instance?.TerritorySave ?? TerritorySave.Load();
            BuildUI();
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && !key.Echo)
            {
                if (key.Keycode == Key.Escape)
                {
                    GetViewport().SetInputAsHandled();
                    // If we came from planet select (pre-run flow), go back there
                    // If we came from meta hub, go back to meta hub
                    if (GameManager.Instance?.CurrentPhase == GamePhase.PlanetSelect)
                        GameManager.Instance?.StartPlanetSelect();
                    else
                        GameManager.Instance?.ShowMetaHub();
                }
            }
        }

        private void BuildUI()
        {
            // Full-screen background
            var bg = new ColorRect();
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            bg.Color = TronTheme.Background;
            AddChild(bg);

            // Main layout
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

            // Header row
            var headerRow = new HBoxContainer();
            headerRow.AddThemeConstantOverride("separation", 20);
            root.AddChild(headerRow);

            var title = new Label();
            title.Text = "TERRITORY";
            title.AddThemeFontSizeOverride("font_size", 36);
            title.AddThemeColorOverride("font_color", Accent);
            headerRow.AddChild(title);

            // Spacer
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

            // Scrollable section container
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
            var backStyle = CreateButtonStyle(new Color(0.4f, 0.4f, 0.4f));
            backBtn.AddThemeStyleboxOverride("normal", backStyle);
            bottomRow.AddChild(backBtn);

            var spacer2 = new Control();
            spacer2.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            bottomRow.AddChild(spacer2);

            var farmBtn = new Button();
            farmBtn.Text = "Start Farming Run";
            farmBtn.AddThemeFontSizeOverride("font_size", 18);
            farmBtn.Pressed += () =>
            {
                GameManager.Instance?.LaunchFromPlanetSelect(_selectedPlanet, RunMode.Harvest);
            };
            var farmStyle = CreateButtonStyle(Unlocked);
            farmBtn.AddThemeStyleboxOverride("normal", farmStyle);
            bottomRow.AddChild(farmBtn);
        }

        private void SelectPlanet(int planetId)
        {
            _selectedPlanet = planetId;
            // Rebuild UI — simplest approach
            foreach (var child in GetChildren())
                child.QueueFree();
            BuildUI();
        }

        private void PopulateSections()
        {
            // Clear existing
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

            foreach (var section in planet.Sections)
            {
                var card = CreateSectionCard(section);
                _sectionContainer.AddChild(card);
            }
        }

        private PanelContainer CreateSectionCard(TerritorySection section)
        {
            bool isUnlocked = TerritoryLoader.IsUnlocked(section.Id, _save);
            bool isCleared = _save.ClearedBossSections.Contains(section.Id);
            int metaResources = GameManager.Instance?.TotalExtracted ?? 0;
            bool canUnlock = TerritoryLoader.CanUnlock(section.Id, _save, metaResources);

            Color statusColor;
            string statusText;
            if (isCleared)
            {
                statusColor = Cleared;
                statusText = "CLEARED";
            }
            else if (isUnlocked)
            {
                statusColor = Unlocked;
                statusText = "UNLOCKED";
            }
            else if (canUnlock)
            {
                statusColor = Available;
                statusText = $"UNLOCK ({section.Cost} resources)";
            }
            else
            {
                statusColor = Locked;
                statusText = $"LOCKED (cost: {section.Cost})";
            }

            var card = new PanelContainer();
            var cardStyle = new StyleBoxFlat();
            cardStyle.BgColor = new Color(0.03f, 0.03f, 0.05f, 0.95f);
            cardStyle.BorderColor = section.GatesBoss ? BossColor : statusColor;
            cardStyle.SetBorderWidthAll(2);
            cardStyle.SetCornerRadiusAll(6);
            cardStyle.ContentMarginLeft = 20;
            cardStyle.ContentMarginRight = 20;
            cardStyle.ContentMarginTop = 14;
            cardStyle.ContentMarginBottom = 14;
            card.AddThemeStyleboxOverride("panel", cardStyle);

            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 20);
            card.AddChild(hbox);

            // Left side — section info
            var info = new VBoxContainer();
            info.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            info.AddThemeConstantOverride("separation", 4);
            hbox.AddChild(info);

            var nameLabel = new Label();
            nameLabel.Text = section.Name;
            nameLabel.AddThemeFontSizeOverride("font_size", 22);
            nameLabel.AddThemeColorOverride("font_color", statusColor);
            info.AddChild(nameLabel);

            if (section.GatesBoss)
            {
                var bossLabel = new Label();
                bossLabel.Text = $"BOSS GATE — Wave {section.BossWave}";
                bossLabel.AddThemeFontSizeOverride("font_size", 14);
                bossLabel.AddThemeColorOverride("font_color", BossColor);
                info.AddChild(bossLabel);
            }

            if (section.MapVariants.Count > 0)
            {
                var variants = new Label();
                variants.Text = $"Maps: {string.Join(", ", section.MapVariants)}";
                variants.AddThemeFontSizeOverride("font_size", 13);
                variants.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
                info.AddChild(variants);
            }

            if (!string.IsNullOrEmpty(section.Requires) && !isUnlocked)
            {
                var reqs = new Label();
                reqs.Text = $"Requires: {section.Requires}";
                reqs.AddThemeFontSizeOverride("font_size", 13);
                reqs.AddThemeColorOverride("font_color", new Color(0.6f, 0.4f, 0.3f));
                info.AddChild(reqs);
            }

            // Right side — status + action button
            var actions = new VBoxContainer();
            actions.AddThemeConstantOverride("separation", 6);
            hbox.AddChild(actions);

            var status = new Label();
            status.Text = statusText;
            status.AddThemeFontSizeOverride("font_size", 16);
            status.AddThemeColorOverride("font_color", statusColor);
            status.HorizontalAlignment = HorizontalAlignment.Right;
            actions.AddChild(status);

            if (canUnlock)
            {
                var unlockBtn = new Button();
                unlockBtn.Text = "Unlock";
                unlockBtn.AddThemeFontSizeOverride("font_size", 16);
                var unlockStyle = CreateButtonStyle(Available);
                unlockBtn.AddThemeStyleboxOverride("normal", unlockStyle);
                string sectionId = section.Id;
                unlockBtn.Pressed += () => OnUnlockPressed(sectionId);
                actions.AddChild(unlockBtn);
            }

            // Launch button for playable sites (unlocked or cleared — can replay for resources)
            if (isUnlocked || isCleared)
            {
                if (section.GatesBoss)
                {
                    var bossBtn = new Button();
                    bossBtn.Text = "Boss Run";
                    bossBtn.AddThemeFontSizeOverride("font_size", 16);
                    var bossStyle = CreateButtonStyle(BossColor);
                    bossBtn.AddThemeStyleboxOverride("normal", bossStyle);

                    var availableSuits = SuitManager.GetAvailableSuits();
                    if (availableSuits.Count == 0)
                    {
                        bossBtn.Disabled = true;
                        bossBtn.TooltipText = "No suits available — save a suit from a farming run first";
                    }
                    else
                    {
                        string sid = section.Id;
                        bossBtn.Pressed += () =>
                            GameManager.Instance?.ShowBossConfirmation(_selectedPlanet, 0, sid);
                    }
                    actions.AddChild(bossBtn);
                }
                else
                {
                    var launchBtn = new Button();
                    launchBtn.Text = isCleared ? "Replay" : "Launch";
                    launchBtn.AddThemeFontSizeOverride("font_size", 16);
                    launchBtn.CustomMinimumSize = new Vector2(100, 0);
                    var launchStyle = CreateButtonStyle(isCleared ? Cleared : Unlocked);
                    launchBtn.AddThemeStyleboxOverride("normal", launchStyle);
                    string siteId = section.Id;
                    launchBtn.Pressed += () =>
                        GameManager.Instance?.LaunchFromTerritorySection(_selectedPlanet, siteId);
                    actions.AddChild(launchBtn);
                }
            }

            return card;
        }

        private void OnUnlockPressed(string sectionId)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            if (gm.UnlockSection(sectionId))
            {
                // Refresh the UI
                _save = gm.TerritorySave;
                UpdateResourceDisplay();
                PopulateSections();
            }
        }

        private void UpdateResourceDisplay()
        {
            int resources = GameManager.Instance?.TotalExtracted ?? 0;
            _resourceLabel.Text = $"Resources: {resources}";
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
