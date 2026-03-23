using System.Linq;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// F12 Editor module: browse territory data (planets, regions, sites) and preview the CEF territory map.
    /// </summary>
    public partial class TerritoryEditor : EditorModule
    {
        public override string ModuleName => "Territory";
        public override Color AccentColor => new Color(0.0f, 0.85f, 0.95f);

        private VBoxContainer _listBox;
        private VBoxContainer _detailBox;
        private int _selectedPlanet = 1;

        public override void _Ready()
        {
            var split = new HSplitContainer();
            split.SizeFlagsVertical = SizeFlags.ExpandFill;
            split.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            AddChild(split);

            // ── Left: region/site tree ──
            var leftPanel = new PanelContainer();
            leftPanel.CustomMinimumSize = new Vector2(280, 0);
            leftPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            leftPanel.AddThemeStyleboxOverride("panel", EditorStyles.MakePanel(EditorStyles.BgPanel));
            split.AddChild(leftPanel);

            var leftVBox = new VBoxContainer();
            leftVBox.AddThemeConstantOverride("separation", 6);
            leftPanel.AddChild(leftVBox);

            // Planet tabs
            var tabRow = new HBoxContainer();
            tabRow.AddThemeConstantOverride("separation", 4);
            leftVBox.AddChild(tabRow);

            var planets = TerritoryLoader.LoadAll();
            foreach (var kvp in planets)
            {
                int pid = kvp.Key;
                var btn = EditorStyles.MakeButton($"P{pid}", 12, pid == _selectedPlanet ? AccentColor : EditorStyles.TextMuted);
                btn.CustomMinimumSize = new Vector2(40, 0);
                btn.Pressed += () => { _selectedPlanet = pid; PopulateList(); };
                tabRow.AddChild(btn);
            }

            leftVBox.AddChild(EditorStyles.MakeSeparator());

            var scroll = new ScrollContainer();
            scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            leftVBox.AddChild(scroll);

            _listBox = new VBoxContainer();
            _listBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _listBox.AddThemeConstantOverride("separation", 2);
            scroll.AddChild(_listBox);

            // ── Right: detail panel ──
            var rightPanel = new PanelContainer();
            rightPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            rightPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            rightPanel.AddThemeStyleboxOverride("panel", EditorStyles.MakePanel(EditorStyles.BgPanel));
            split.AddChild(rightPanel);

            var rightScroll = new ScrollContainer();
            rightScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            rightScroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            rightPanel.AddChild(rightScroll);

            _detailBox = new VBoxContainer();
            _detailBox.AddThemeConstantOverride("separation", 8);
            _detailBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            rightScroll.AddChild(_detailBox);

            PopulateList();
            ShowPlanetOverview();
        }

        private void PopulateList()
        {
            foreach (var c in _listBox.GetChildren()) c.QueueFree();

            var planet = TerritoryManager.GetPlanet(_selectedPlanet);
            if (planet == null)
            {
                _listBox.AddChild(EditorStyles.MakeLabel("No data", 13, EditorStyles.TextMuted));
                return;
            }

            var save = GameManager.Instance?.MetaSave ?? MetaPerkSave.Load();

            foreach (var region in planet.Regions)
            {
                bool conquered = TerritoryManager.IsRegionConquered(region.Id, save);
                bool accessible = TerritoryManager.IsRegionAccessible(region.Id, save);
                Color regionColor = conquered ? new Color(0.24f, 0.87f, 0.78f) :
                                    accessible ? AccentColor : EditorStyles.TextMuted;

                var regionLabel = EditorStyles.MakeLabel(
                    $"{(conquered ? "[OK] " : accessible ? "" : "[X] ")}{region.Name}", 14, regionColor);
                _listBox.AddChild(regionLabel);

                foreach (var site in region.Sites)
                {
                    bool cleared = TerritoryManager.IsSiteCleared(site.Id, save);
                    Color siteColor = cleared ? new Color(0.4f, 0.8f, 1.0f) :
                                     site.IsBossSite ? new Color(0.9f, 0.2f, 0.15f) :
                                     accessible ? EditorStyles.TextPrimary : EditorStyles.TextMuted;

                    var siteBtn = EditorStyles.MakeButton(
                        $"  {(cleared ? "+" : "-")} {site.Name} [D{site.Difficulty}]", 12, siteColor);
                    siteBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                    string siteId = site.Id;
                    string regionId = region.Id;
                    siteBtn.Pressed += () => ShowSiteDetail(siteId, regionId);
                    _listBox.AddChild(siteBtn);
                }

                if (region.Buff != null)
                {
                    var buffLabel = EditorStyles.MakeLabel(
                        $"  Buff: {region.Buff.Label}", 11,
                        conquered ? new Color(0.24f, 0.87f, 0.78f) : EditorStyles.TextMuted);
                    _listBox.AddChild(buffLabel);
                }

                _listBox.AddChild(EditorStyles.MakeSeparator());
            }
        }

        private void ShowPlanetOverview()
        {
            foreach (var c in _detailBox.GetChildren()) c.QueueFree();

            var planet = TerritoryManager.GetPlanet(_selectedPlanet);
            if (planet == null) return;
            var save = GameManager.Instance?.MetaSave ?? MetaPerkSave.Load();

            _detailBox.AddChild(EditorStyles.MakeLabel(planet.Name, 24, AccentColor));

            var (cleared, total) = TerritoryManager.GetPlanetProgress(_selectedPlanet, save);
            _detailBox.AddChild(EditorStyles.MakeLabel($"Progress: {cleared}/{total} sites cleared", 14, EditorStyles.TextSecondary));
            _detailBox.AddChild(EditorStyles.MakeLabel($"Regions: {planet.Regions.Count}", 14, EditorStyles.TextSecondary));

            var buffs = TerritoryManager.GetActiveBuffs(_selectedPlanet, save);
            if (buffs.Count > 0)
            {
                _detailBox.AddChild(EditorStyles.MakeSeparator());
                _detailBox.AddChild(EditorStyles.MakeLabel("Active Conquest Buffs", 16, new Color(0.24f, 0.87f, 0.78f)));
                foreach (var b in buffs)
                    _detailBox.AddChild(EditorStyles.MakeLabel($"  {b.Label} ({b.Type}: {b.Value})", 13, EditorStyles.TextPrimary));
            }

            if (!string.IsNullOrEmpty(planet.RequiresPlanetBoss))
            {
                _detailBox.AddChild(EditorStyles.MakeSeparator());
                bool unlocked = TerritoryManager.IsPlanetAccessible(_selectedPlanet, save);
                _detailBox.AddChild(EditorStyles.MakeLabel(
                    $"Requires boss: {planet.RequiresPlanetBoss} ({(unlocked ? "CLEARED" : "LOCKED")})",
                    13, unlocked ? new Color(0.4f, 0.8f, 1.0f) : new Color(0.9f, 0.2f, 0.15f)));
            }
        }

        private void ShowSiteDetail(string siteId, string regionId)
        {
            foreach (var c in _detailBox.GetChildren()) c.QueueFree();

            var site = TerritoryManager.GetSite(siteId);
            var region = TerritoryManager.GetRegion(regionId);
            if (site == null) return;
            var save = GameManager.Instance?.MetaSave ?? MetaPerkSave.Load();
            bool cleared = TerritoryManager.IsSiteCleared(siteId, save);

            Color titleColor = site.IsBossSite ? new Color(0.9f, 0.2f, 0.15f) :
                               cleared ? new Color(0.4f, 0.8f, 1.0f) : AccentColor;

            _detailBox.AddChild(EditorStyles.MakeLabel(site.Name, 22, titleColor));
            _detailBox.AddChild(EditorStyles.MakeLabel($"ID: {site.Id}", 11, EditorStyles.TextMuted));
            _detailBox.AddChild(EditorStyles.MakeLabel($"Region: {region?.Name ?? regionId}", 13, EditorStyles.TextSecondary));
            _detailBox.AddChild(EditorStyles.MakeLabel($"Status: {(cleared ? "CLEARED" : "AVAILABLE")}", 14,
                cleared ? new Color(0.4f, 0.8f, 1.0f) : new Color(0.2f, 0.7f, 0.3f)));

            _detailBox.AddChild(EditorStyles.MakeSeparator());

            _detailBox.AddChild(EditorStyles.MakeLabel($"Difficulty: {site.Difficulty}/7", 14, EditorStyles.TextPrimary));
            _detailBox.AddChild(EditorStyles.MakeLabel($"Map Layout: {site.MapLayout}", 14, EditorStyles.TextPrimary));
            _detailBox.AddChild(EditorStyles.MakeLabel($"Wave Set: {site.WaveSet}", 14, EditorStyles.TextPrimary));
            _detailBox.AddChild(EditorStyles.MakeLabel($"Reward: +{site.RewardResources} resources", 14, new Color(0.9f, 0.8f, 0.3f)));

            if (site.BonusExtractionMult > 1f)
                _detailBox.AddChild(EditorStyles.MakeLabel($"Extraction Bonus: x{site.BonusExtractionMult:F2}", 14, new Color(0.24f, 0.87f, 0.78f)));

            if (!string.IsNullOrEmpty(site.RewardLabel))
            {
                _detailBox.AddChild(EditorStyles.MakeSeparator());
                _detailBox.AddChild(EditorStyles.MakeLabel(site.RewardLabel, 13, EditorStyles.TextSecondary));
            }

            if (site.IsBossSite)
            {
                _detailBox.AddChild(EditorStyles.MakeSeparator());
                _detailBox.AddChild(EditorStyles.MakeLabel("BOSS SITE", 16, new Color(0.9f, 0.2f, 0.15f)));
                _detailBox.AddChild(EditorStyles.MakeLabel($"Boss: {site.BossId}", 14, new Color(0.9f, 0.2f, 0.15f)));
                _detailBox.AddChild(EditorStyles.MakeLabel($"Boss Wave: {site.BossWave}", 14, new Color(0.9f, 0.2f, 0.15f)));
            }
        }
    }
}
