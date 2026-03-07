using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// The Workshop: permanent perk shop + gear codex viewer + run history.
    /// Accessible from main menu. Spend Scrap on permanent upgrades.
    /// Visual style: dark industrial with gold/orange accents.
    /// </summary>
    public partial class WorkshopUI : CanvasLayer
    {
        private Control _root;
        private Label _scrapLabel;
        private Label _threatLabel;
        private VBoxContainer _perkList;
        private VBoxContainer _frameList;
        private VBoxContainer _codexList;
        private VBoxContainer _historyList;
        private TabContainer _tabs;

        private static readonly Color Gold = new(0.9f, 0.8f, 0.3f);
        private static readonly Color DimGold = new(0.6f, 0.5f, 0.2f);
        private static readonly Color BgDark = new(0.05f, 0.05f, 0.1f, 0.97f);
        private static readonly Color PanelBg = new(0.08f, 0.08f, 0.14f);
        private static readonly Color ButtonBg = new(0.12f, 0.12f, 0.2f);
        private static readonly Color Affordable = new(0.5f, 0.9f, 0.5f);
        private static readonly Color TooExpensive = new(0.6f, 0.3f, 0.3f);
        private static readonly Color MaxedOut = new(0.3f, 0.7f, 1f);

        public static WorkshopUI Instance { get; private set; }

        public override void _Ready()
        {
            Instance = this;
            Layer = 50;
            BuildUI();
            _root.Visible = false;
        }

        public void Open()
        {
            RefreshAll();
            _root.Visible = true;
        }

        public void Close()
        {
            _root.Visible = false;
        }

        public bool IsOpen => _root?.Visible ?? false;

        private void BuildUI()
        {
            _root = new Control();
            _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(_root);

            // Full-screen dark background
            var bg = new ColorRect();
            bg.Color = BgDark;
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            bg.MouseFilter = Control.MouseFilterEnum.Stop;
            _root.AddChild(bg);

            // Header
            var header = new HBoxContainer();
            header.Position = new Vector2(40, 20);
            header.Size = new Vector2(1840, 60);
            _root.AddChild(header);

            var title = new Label();
            title.Text = "THE WORKSHOP";
            title.AddThemeFontSizeOverride("font_size", 40);
            title.AddThemeColorOverride("font_color", Gold);
            title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            header.AddChild(title);

            _scrapLabel = new Label();
            _scrapLabel.AddThemeFontSizeOverride("font_size", 28);
            _scrapLabel.AddThemeColorOverride("font_color", Gold);
            header.AddChild(_scrapLabel);

            var spacer = new Control();
            spacer.CustomMinimumSize = new Vector2(30, 0);
            header.AddChild(spacer);

            _threatLabel = new Label();
            _threatLabel.AddThemeFontSizeOverride("font_size", 22);
            _threatLabel.AddThemeColorOverride("font_color", new Color(1f, 0.4f, 0.3f));
            header.AddChild(_threatLabel);

            var spacer2 = new Control();
            spacer2.CustomMinimumSize = new Vector2(30, 0);
            header.AddChild(spacer2);

            var closeBtn = new Button();
            closeBtn.Text = "X";
            closeBtn.CustomMinimumSize = new Vector2(50, 50);
            closeBtn.AddThemeFontSizeOverride("font_size", 24);
            closeBtn.Pressed += Close;
            header.AddChild(closeBtn);

            // Tab container
            _tabs = new TabContainer();
            _tabs.Position = new Vector2(40, 90);
            _tabs.Size = new Vector2(1840, 900);
            _tabs.AddThemeColorOverride("font_selected_color", Gold);
            _tabs.AddThemeColorOverride("font_unselected_color", DimGold);
            _root.AddChild(_tabs);

            // Perks tab
            BuildPerksTab();

            // Frames tab
            BuildFramesTab();

            // Codex tab
            BuildCodexTab();

            // History tab
            BuildHistoryTab();
        }

        private void BuildPerksTab()
        {
            var scroll = new ScrollContainer();
            scroll.Name = "Upgrades";
            _tabs.AddChild(scroll);

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 20);
            margin.AddThemeConstantOverride("margin_right", 20);
            margin.AddThemeConstantOverride("margin_top", 20);
            margin.AddThemeConstantOverride("margin_bottom", 20);
            margin.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            scroll.AddChild(margin);

            _perkList = new VBoxContainer();
            _perkList.AddThemeConstantOverride("separation", 8);
            _perkList.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            margin.AddChild(_perkList);
        }

        private void BuildFramesTab()
        {
            var scroll = new ScrollContainer();
            scroll.Name = "Frames";
            _tabs.AddChild(scroll);

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 20);
            margin.AddThemeConstantOverride("margin_right", 20);
            margin.AddThemeConstantOverride("margin_top", 20);
            margin.AddThemeConstantOverride("margin_bottom", 20);
            margin.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            scroll.AddChild(margin);

            _frameList = new VBoxContainer();
            _frameList.AddThemeConstantOverride("separation", 12);
            _frameList.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            margin.AddChild(_frameList);
        }

        private void BuildCodexTab()
        {
            var scroll = new ScrollContainer();
            scroll.Name = "Gear Codex";
            _tabs.AddChild(scroll);

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 20);
            margin.AddThemeConstantOverride("margin_right", 20);
            margin.AddThemeConstantOverride("margin_top", 20);
            margin.AddThemeConstantOverride("margin_bottom", 20);
            margin.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            scroll.AddChild(margin);

            _codexList = new VBoxContainer();
            _codexList.AddThemeConstantOverride("separation", 4);
            _codexList.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            margin.AddChild(_codexList);
        }

        private void BuildHistoryTab()
        {
            var scroll = new ScrollContainer();
            scroll.Name = "Run History";
            _tabs.AddChild(scroll);

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 20);
            margin.AddThemeConstantOverride("margin_right", 20);
            margin.AddThemeConstantOverride("margin_top", 20);
            margin.AddThemeConstantOverride("margin_bottom", 20);
            margin.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            scroll.AddChild(margin);

            _historyList = new VBoxContainer();
            _historyList.AddThemeConstantOverride("separation", 8);
            _historyList.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            margin.AddChild(_historyList);
        }

        private void RefreshAll()
        {
            var data = MetaSaveManager.Data;
            _scrapLabel.Text = $"Scrap: {data.Scrap}";
            _threatLabel.Text = $"Threat Level: {MetaSaveManager.ThreatLevel}";

            RefreshPerks();
            RefreshFrames();
            RefreshCodex();
            RefreshHistory();
        }

        private void RefreshPerks()
        {
            // Clear existing
            foreach (var child in _perkList.GetChildren())
                child.QueueFree();

            foreach (var (id, perk) in PerkRegistry.All)
            {
                int currentRank = MetaSaveManager.GetPerkRank(id);
                bool maxed = currentRank >= perk.MaxRank;
                int cost = maxed ? 0 : perk.GetCost(currentRank + 1);
                bool canAfford = !maxed && MetaSaveManager.Data.Scrap >= cost;

                var row = new PanelContainer();
                row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                row.CustomMinimumSize = new Vector2(0, 80);
                var rowStyle = new StyleBoxFlat();
                rowStyle.BgColor = PanelBg;
                rowStyle.BorderColor = maxed ? MaxedOut : (canAfford ? DimGold : new Color(0.3f, 0.3f, 0.3f));
                rowStyle.BorderWidthBottom = 1;
                rowStyle.BorderWidthTop = 1;
                rowStyle.BorderWidthLeft = 1;
                rowStyle.BorderWidthRight = 1;
                rowStyle.CornerRadiusBottomLeft = 4;
                rowStyle.CornerRadiusBottomRight = 4;
                rowStyle.CornerRadiusTopLeft = 4;
                rowStyle.CornerRadiusTopRight = 4;
                rowStyle.ContentMarginLeft = 16;
                rowStyle.ContentMarginRight = 16;
                rowStyle.ContentMarginTop = 8;
                rowStyle.ContentMarginBottom = 8;
                row.AddThemeStyleboxOverride("panel", rowStyle);
                _perkList.AddChild(row);

                var hbox = new HBoxContainer();
                hbox.AddThemeConstantOverride("separation", 16);
                row.AddChild(hbox);

                // Perk info
                var infoVbox = new VBoxContainer();
                infoVbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                hbox.AddChild(infoVbox);

                var nameLabel = new Label();
                nameLabel.Text = perk.Name;
                nameLabel.AddThemeFontSizeOverride("font_size", 22);
                nameLabel.AddThemeColorOverride("font_color", maxed ? MaxedOut : Gold);
                infoVbox.AddChild(nameLabel);

                var descLabel = new Label();
                descLabel.Text = perk.Description;
                descLabel.AddThemeFontSizeOverride("font_size", 16);
                descLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
                infoVbox.AddChild(descLabel);

                // Rank display
                var rankLabel = new Label();
                rankLabel.Text = $"Rank {currentRank}/{perk.MaxRank}";
                rankLabel.AddThemeFontSizeOverride("font_size", 18);
                rankLabel.AddThemeColorOverride("font_color", maxed ? MaxedOut : new Color(0.8f, 0.8f, 0.8f));
                rankLabel.CustomMinimumSize = new Vector2(120, 0);
                hbox.AddChild(rankLabel);

                // Stat bonus display
                var bonusLabel = new Label();
                float currentBonus = perk.ValuePerRank * currentRank;
                string bonusText = FormatStatBonus(perk, currentBonus);
                bonusLabel.Text = bonusText;
                bonusLabel.AddThemeFontSizeOverride("font_size", 16);
                bonusLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.9f, 0.6f));
                bonusLabel.CustomMinimumSize = new Vector2(120, 0);
                hbox.AddChild(bonusLabel);

                // Buy button
                if (maxed)
                {
                    var maxLabel = new Label();
                    maxLabel.Text = "MAXED";
                    maxLabel.AddThemeFontSizeOverride("font_size", 18);
                    maxLabel.AddThemeColorOverride("font_color", MaxedOut);
                    maxLabel.CustomMinimumSize = new Vector2(140, 0);
                    maxLabel.HorizontalAlignment = HorizontalAlignment.Center;
                    maxLabel.VerticalAlignment = VerticalAlignment.Center;
                    hbox.AddChild(maxLabel);
                }
                else
                {
                    var buyBtn = new Button();
                    buyBtn.Text = $"Upgrade\n{cost} Scrap";
                    buyBtn.CustomMinimumSize = new Vector2(140, 60);
                    buyBtn.Disabled = !canAfford;
                    buyBtn.AddThemeFontSizeOverride("font_size", 16);

                    string perkId = id; // Capture for lambda
                    buyBtn.Pressed += () =>
                    {
                        if (MetaSaveManager.UnlockPerk(perkId))
                            RefreshAll();
                    };
                    hbox.AddChild(buyBtn);
                }

                // Threat indicator
                var threatIcon = new Label();
                threatIcon.Text = $"+{perk.ThreatPerRank}";
                threatIcon.AddThemeFontSizeOverride("font_size", 14);
                threatIcon.AddThemeColorOverride("font_color", new Color(1f, 0.4f, 0.3f, 0.7f));
                threatIcon.CustomMinimumSize = new Vector2(30, 0);
                threatIcon.VerticalAlignment = VerticalAlignment.Center;
                threatIcon.TooltipText = "Threat per rank — increases enemy difficulty";
                hbox.AddChild(threatIcon);
            }
        }

        private void RefreshFrames()
        {
            foreach (var child in _frameList.GetChildren())
                child.QueueFree();

            BotFrameRegistry.Initialize();

            foreach (var (frameType, frameData) in BotFrameRegistry.Classes)
            {
                bool unlocked = MetaSaveManager.IsFrameUnlocked(frameType);
                bool meetsReq = MetaSaveManager.MeetsFrameRequirement(frameType);
                bool canAfford = !unlocked && meetsReq && MetaSaveManager.Data.Scrap >= frameData.UnlockCost;

                var card = new PanelContainer();
                card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                card.CustomMinimumSize = new Vector2(0, 120);
                var cardStyle = new StyleBoxFlat();
                cardStyle.BgColor = PanelBg;
                cardStyle.BorderColor = unlocked ? Affordable : (meetsReq ? DimGold : new Color(0.25f, 0.25f, 0.3f));
                cardStyle.BorderWidthBottom = 2;
                cardStyle.BorderWidthTop = 2;
                cardStyle.BorderWidthLeft = 2;
                cardStyle.BorderWidthRight = 2;
                cardStyle.CornerRadiusBottomLeft = 4;
                cardStyle.CornerRadiusBottomRight = 4;
                cardStyle.CornerRadiusTopLeft = 4;
                cardStyle.CornerRadiusTopRight = 4;
                cardStyle.ContentMarginLeft = 20;
                cardStyle.ContentMarginRight = 20;
                cardStyle.ContentMarginTop = 12;
                cardStyle.ContentMarginBottom = 12;
                card.AddThemeStyleboxOverride("panel", cardStyle);
                _frameList.AddChild(card);

                var hbox = new HBoxContainer();
                hbox.AddThemeConstantOverride("separation", 20);
                card.AddChild(hbox);

                // Frame icon area (colored block representing the bot)
                var iconBox = new PanelContainer();
                iconBox.CustomMinimumSize = new Vector2(80, 80);
                var iconStyle = new StyleBoxFlat();
                iconStyle.BgColor = unlocked ? new Color(0.15f, 0.25f, 0.15f) : new Color(0.1f, 0.1f, 0.15f);
                iconStyle.CornerRadiusBottomLeft = 4;
                iconStyle.CornerRadiusBottomRight = 4;
                iconStyle.CornerRadiusTopLeft = 4;
                iconStyle.CornerRadiusTopRight = 4;
                iconBox.AddThemeStyleboxOverride("panel", iconStyle);
                hbox.AddChild(iconBox);

                var iconLabel = new Label();
                iconLabel.Text = unlocked ? frameData.DisplayName[..1] : "?";
                iconLabel.AddThemeFontSizeOverride("font_size", 36);
                iconLabel.AddThemeColorOverride("font_color", unlocked ? Gold : new Color(0.4f, 0.4f, 0.45f));
                iconLabel.HorizontalAlignment = HorizontalAlignment.Center;
                iconLabel.VerticalAlignment = VerticalAlignment.Center;
                iconBox.AddChild(iconLabel);

                // Frame info
                var infoVbox = new VBoxContainer();
                infoVbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                infoVbox.AddThemeConstantOverride("separation", 4);
                hbox.AddChild(infoVbox);

                var nameLabel = new Label();
                nameLabel.Text = unlocked ? frameData.DisplayName : (meetsReq ? frameData.DisplayName : "???");
                nameLabel.AddThemeFontSizeOverride("font_size", 24);
                nameLabel.AddThemeColorOverride("font_color", unlocked ? Gold : (meetsReq ? DimGold : new Color(0.4f, 0.4f, 0.45f)));
                infoVbox.AddChild(nameLabel);

                var descLabel = new Label();
                descLabel.Text = unlocked ? frameData.Description : frameData.UnlockHint;
                descLabel.AddThemeFontSizeOverride("font_size", 15);
                descLabel.AddThemeColorOverride("font_color", unlocked ? new Color(0.7f, 0.7f, 0.7f) : new Color(0.5f, 0.5f, 0.55f));
                descLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                infoVbox.AddChild(descLabel);

                if (unlocked && !string.IsNullOrEmpty(frameData.Lore))
                {
                    var loreLabel = new Label();
                    loreLabel.Text = frameData.Lore;
                    loreLabel.AddThemeFontSizeOverride("font_size", 12);
                    loreLabel.AddThemeColorOverride("font_color", new Color(0.45f, 0.45f, 0.5f));
                    loreLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                    infoVbox.AddChild(loreLabel);
                }

                // Stats preview (when unlocked)
                if (unlocked)
                {
                    var statsLabel = new Label();
                    statsLabel.Text = $"Primary: {frameData.PrimaryStat}  |  Secondary: {frameData.SecondaryStat}";
                    statsLabel.AddThemeFontSizeOverride("font_size", 13);
                    statsLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.7f, 0.6f));
                    infoVbox.AddChild(statsLabel);
                }

                // Right side: status / buy button
                var rightVbox = new VBoxContainer();
                rightVbox.CustomMinimumSize = new Vector2(160, 0);
                rightVbox.AddThemeConstantOverride("separation", 6);
                hbox.AddChild(rightVbox);

                if (unlocked)
                {
                    var unlockedLabel = new Label();
                    unlockedLabel.Text = "UNLOCKED";
                    unlockedLabel.AddThemeFontSizeOverride("font_size", 18);
                    unlockedLabel.AddThemeColorOverride("font_color", Affordable);
                    unlockedLabel.HorizontalAlignment = HorizontalAlignment.Center;
                    unlockedLabel.VerticalAlignment = VerticalAlignment.Center;
                    rightVbox.AddChild(unlockedLabel);
                }
                else if (meetsReq)
                {
                    var costLabel = new Label();
                    costLabel.Text = $"{frameData.UnlockCost} Scrap";
                    costLabel.AddThemeFontSizeOverride("font_size", 14);
                    costLabel.AddThemeColorOverride("font_color", canAfford ? Affordable : TooExpensive);
                    costLabel.HorizontalAlignment = HorizontalAlignment.Center;
                    rightVbox.AddChild(costLabel);

                    var buyBtn = new Button();
                    buyBtn.Text = "Unlock";
                    buyBtn.CustomMinimumSize = new Vector2(140, 45);
                    buyBtn.Disabled = !canAfford;
                    buyBtn.AddThemeFontSizeOverride("font_size", 18);

                    var capturedFrame = frameType;
                    buyBtn.Pressed += () =>
                    {
                        if (MetaSaveManager.UnlockFrame(capturedFrame))
                            RefreshAll();
                    };
                    rightVbox.AddChild(buyBtn);
                }
                else
                {
                    var lockedLabel = new Label();
                    lockedLabel.Text = "LOCKED";
                    lockedLabel.AddThemeFontSizeOverride("font_size", 16);
                    lockedLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.3f, 0.3f));
                    lockedLabel.HorizontalAlignment = HorizontalAlignment.Center;
                    rightVbox.AddChild(lockedLabel);

                    var reqLabel = new Label();
                    reqLabel.Text = frameData.UnlockHint;
                    reqLabel.AddThemeFontSizeOverride("font_size", 12);
                    reqLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.55f));
                    reqLabel.HorizontalAlignment = HorizontalAlignment.Center;
                    reqLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                    rightVbox.AddChild(reqLabel);
                }
            }
        }

        private void RefreshCodex()
        {
            foreach (var child in _codexList.GetChildren())
                child.QueueFree();

            var codex = MetaSaveManager.Data.GearCodex;

            var countLabel = new Label();
            countLabel.Text = $"Items Discovered: {codex.Count}  |  Threat from Codex: +{codex.Count / 5}";
            countLabel.AddThemeFontSizeOverride("font_size", 22);
            countLabel.AddThemeColorOverride("font_color", Gold);
            _codexList.AddChild(countLabel);

            var sep = new HSeparator();
            _codexList.AddChild(sep);

            if (codex.Count == 0)
            {
                var emptyLabel = new Label();
                emptyLabel.Text = "No items discovered yet. Play a run to fill the codex!";
                emptyLabel.AddThemeFontSizeOverride("font_size", 18);
                emptyLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
                _codexList.AddChild(emptyLabel);
                return;
            }

            // Grid of discovered items
            var grid = new GridContainer();
            grid.Columns = 4;
            grid.AddThemeConstantOverride("h_separation", 8);
            grid.AddThemeConstantOverride("v_separation", 8);
            _codexList.AddChild(grid);

            foreach (var itemId in codex)
            {
                var itemData = ItemRegistry.GetItem(itemId);
                string displayName = itemData?.ItemName ?? itemId;
                var rarity = itemData?.Rarity ?? ItemRarity.Common;

                var card = new PanelContainer();
                card.CustomMinimumSize = new Vector2(420, 40);
                var cardStyle = new StyleBoxFlat();
                cardStyle.BgColor = new Color(0.1f, 0.1f, 0.16f);
                cardStyle.BorderColor = GetRarityColor(rarity);
                cardStyle.BorderWidthBottom = 1;
                cardStyle.BorderWidthLeft = 2;
                cardStyle.ContentMarginLeft = 8;
                cardStyle.ContentMarginTop = 4;
                cardStyle.ContentMarginBottom = 4;
                card.AddThemeStyleboxOverride("panel", cardStyle);
                grid.AddChild(card);

                var itemLabel = new Label();
                itemLabel.Text = displayName;
                itemLabel.AddThemeFontSizeOverride("font_size", 16);
                itemLabel.AddThemeColorOverride("font_color", GetRarityColor(rarity));
                card.AddChild(itemLabel);
            }
        }

        private void RefreshHistory()
        {
            foreach (var child in _historyList.GetChildren())
                child.QueueFree();

            var h = MetaSaveManager.Data.History;

            AddHistoryStat("Total Runs", h.TotalRuns.ToString());
            AddHistoryStat("Total Kills", h.TotalKills.ToString());
            AddHistoryStat("Best Sector", h.BestSector > 0 ? $"Sector {h.BestSector}, Area {h.BestArea}" : "—");
            AddHistoryStat("Highest Level", h.HighestLevel > 0 ? $"Lv {h.HighestLevel}" : "—");
            AddHistoryStat("Lifetime Scrap", MetaSaveManager.Data.LifetimeScrap.ToString());

            if (h.RunsPerClass.Count > 0)
            {
                var sep = new HSeparator();
                _historyList.AddChild(sep);

                var classTitle = new Label();
                classTitle.Text = "Runs Per Frame";
                classTitle.AddThemeFontSizeOverride("font_size", 22);
                classTitle.AddThemeColorOverride("font_color", Gold);
                _historyList.AddChild(classTitle);

                foreach (var (className, runs) in h.RunsPerClass)
                {
                    h.BestSectorPerClass.TryGetValue(className, out int bestSector);
                    AddHistoryStat($"  {className}", $"{runs} runs (best: Sector {bestSector})");
                }
            }
        }

        private void AddHistoryStat(string label, string value)
        {
            var hbox = new HBoxContainer();
            _historyList.AddChild(hbox);

            var nameLabel = new Label();
            nameLabel.Text = label;
            nameLabel.AddThemeFontSizeOverride("font_size", 20);
            nameLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
            nameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            hbox.AddChild(nameLabel);

            var valueLabel = new Label();
            valueLabel.Text = value;
            valueLabel.AddThemeFontSizeOverride("font_size", 20);
            valueLabel.AddThemeColorOverride("font_color", Gold);
            hbox.AddChild(valueLabel);
        }

        private static string FormatStatBonus(PerkData perk, float value)
        {
            if (value == 0) return "—";
            if (perk.ModType == ModifierType.Percent)
                return $"+{value * 100:F0}% {perk.AffectedStat}";
            return $"+{value:F0} {perk.AffectedStat}";
        }

        private static Color GetRarityColor(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Common => new Color(0.7f, 0.7f, 0.7f),
            ItemRarity.Uncommon => new Color(0.3f, 0.9f, 0.3f),
            ItemRarity.Rare => new Color(0.3f, 0.5f, 1f),
            ItemRarity.Epic => new Color(0.7f, 0.3f, 0.9f),
            ItemRarity.Legendary => new Color(1f, 0.65f, 0f),
            ItemRarity.Absurd => new Color(1f, 0.2f, 0.4f),
            _ => new Color(0.7f, 0.7f, 0.7f)
        };
    }
}
