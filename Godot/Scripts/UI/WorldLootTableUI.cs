using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Post-campaign UI showing all grafts and where they drop.
    /// Accessible from the pause menu after first AXIS defeat.
    /// </summary>
    public partial class WorldLootTableUI : CanvasLayer
    {
        private static readonly Color Mythic = new(1f, 0.2f, 0.4f);
        private static readonly Color Legendary = new(1f, 0.55f, 0f);
        private static readonly Color Epic = new(0.7f, 0.3f, 0.9f);
        private static readonly Color Rare = new(0.3f, 0.5f, 1f);
        private static readonly Color DimWhite = new(0.65f, 0.65f, 0.65f);
        private static readonly Color HeaderGold = new(0.95f, 0.8f, 0.3f);

        public override void _Ready()
        {
            Layer = 50;
            ProcessMode = ProcessModeEnum.Always;
            BuildUI();
        }

        private void BuildUI()
        {
            // Full-screen dim
            var bg = new ColorRect();
            bg.Color = new Color(0, 0, 0, 0.85f);
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            bg.MouseFilter = Control.MouseFilterEnum.Stop;
            AddChild(bg);

            // Main panel
            var margin = new MarginContainer();
            margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            margin.AddThemeConstantOverride("margin_left", 80);
            margin.AddThemeConstantOverride("margin_right", 80);
            margin.AddThemeConstantOverride("margin_top", 40);
            margin.AddThemeConstantOverride("margin_bottom", 40);
            AddChild(margin);

            var outerVbox = new VBoxContainer();
            outerVbox.AddThemeConstantOverride("separation", 8);
            margin.AddChild(outerVbox);

            // Title
            var title = new Label();
            title.Text = StringLoader.Get("ui.worldLootTable.title");
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.AddThemeFontSizeOverride("font_size", 36);
            title.AddThemeColorOverride("font_color", HeaderGold);
            outerVbox.AddChild(title);

            var subtitle = new Label();
            subtitle.Text = StringLoader.Get("ui.worldLootTable.subtitle");
            subtitle.HorizontalAlignment = HorizontalAlignment.Center;
            subtitle.AddThemeFontSizeOverride("font_size", 16);
            subtitle.AddThemeColorOverride("font_color", DimWhite);
            outerVbox.AddChild(subtitle);

            AddSpacer(outerVbox, 12);

            // Scrollable list
            var scroll = new ScrollContainer();
            scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            scroll.ProcessMode = ProcessModeEnum.Always;
            outerVbox.AddChild(scroll);

            var list = new VBoxContainer();
            list.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            list.AddThemeConstantOverride("separation", 6);
            scroll.AddChild(list);

            // Populate from registry
            var entries = SalvageCoreRegistry.GetWorldLootTable();
            SalvageCoreRarity? lastRarity = null;

            foreach (var (core, sourceDesc) in entries)
            {
                // Section header when rarity changes
                if (lastRarity != core.Rarity)
                {
                    if (lastRarity != null)
                        AddSpacer(list, 8);
                    AddRarityHeader(list, core.Rarity);
                    lastRarity = core.Rarity;
                }

                AddGraftEntry(list, core, sourceDesc);
            }

            AddSpacer(outerVbox, 8);

            // Close button
            var btnBox = new HBoxContainer();
            btnBox.Alignment = BoxContainer.AlignmentMode.Center;
            outerVbox.AddChild(btnBox);

            var closeBtn = new Button();
            closeBtn.Text = StringLoader.Get("ui.worldLootTable.close");
            closeBtn.CustomMinimumSize = new Vector2(200, 50);
            closeBtn.AddThemeFontSizeOverride("font_size", 22);
            closeBtn.ProcessMode = ProcessModeEnum.Always;
            closeBtn.Pressed += () => QueueFree();
            btnBox.AddChild(closeBtn);
        }

        private void AddRarityHeader(VBoxContainer parent, SalvageCoreRarity rarity)
        {
            var header = new Label();
            header.Text = rarity == SalvageCoreRarity.Mythic
                ? "MYTHIC — Ultra-Rare Chase Grafts"
                : $"{rarity.ToString().ToUpper()} GRAFTS";
            header.HorizontalAlignment = HorizontalAlignment.Left;
            header.AddThemeFontSizeOverride("font_size", 22);
            header.AddThemeColorOverride("font_color", GetRarityColor(rarity));
            parent.AddChild(header);

            // Separator line
            var sep = new HSeparator();
            sep.AddThemeConstantOverride("separation", 4);
            parent.AddChild(sep);
        }

        private void AddGraftEntry(VBoxContainer parent, SalvageCoreData core, string sourceDesc)
        {
            var card = new PanelContainer();
            card.CustomMinimumSize = new Vector2(0, 60);
            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.06f, 0.06f, 0.12f);
            style.BorderColor = GetRarityColor(core.Rarity);
            style.SetBorderWidthAll(1);
            style.SetCornerRadiusAll(4);
            style.ContentMarginLeft = 16;
            style.ContentMarginRight = 16;
            style.ContentMarginTop = 8;
            style.ContentMarginBottom = 8;
            card.AddThemeStyleboxOverride("panel", style);
            parent.AddChild(card);

            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 16);
            card.AddChild(hbox);

            // Left: name + rarity
            var leftVbox = new VBoxContainer();
            leftVbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            leftVbox.AddThemeConstantOverride("separation", 2);
            hbox.AddChild(leftVbox);

            var nameLabel = new Label();
            nameLabel.Text = core.CoreName;
            nameLabel.AddThemeFontSizeOverride("font_size", 18);
            nameLabel.AddThemeColorOverride("font_color", GetRarityColor(core.Rarity));
            leftVbox.AddChild(nameLabel);

            // Short description (first line only)
            string shortDesc = core.Description;
            int nlIndex = shortDesc.IndexOf('\n');
            if (nlIndex > 0)
                shortDesc = shortDesc[(nlIndex + 1)..]; // Use the stat/effect line
            if (shortDesc.Length > 100)
                shortDesc = shortDesc[..97] + "...";
            var descLabel = new Label();
            descLabel.Text = shortDesc;
            descLabel.AddThemeFontSizeOverride("font_size", 13);
            descLabel.AddThemeColorOverride("font_color", DimWhite);
            descLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            leftVbox.AddChild(descLabel);

            // Right: drop source
            var sourceVbox = new VBoxContainer();
            sourceVbox.CustomMinimumSize = new Vector2(200, 0);
            sourceVbox.AddThemeConstantOverride("separation", 2);
            hbox.AddChild(sourceVbox);

            var sourceTitle = new Label();
            sourceTitle.Text = StringLoader.Get("ui.worldLootTable.dropsFrom");
            sourceTitle.AddThemeFontSizeOverride("font_size", 12);
            sourceTitle.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
            sourceVbox.AddChild(sourceTitle);

            var sourceLabel = new Label();
            sourceLabel.Text = sourceDesc;
            sourceLabel.AddThemeFontSizeOverride("font_size", 15);
            sourceLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.85f));
            sourceLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            sourceVbox.AddChild(sourceLabel);
        }

        private static Color GetRarityColor(SalvageCoreRarity rarity) => rarity switch
        {
            SalvageCoreRarity.Mythic => Mythic,
            SalvageCoreRarity.Legendary => Legendary,
            SalvageCoreRarity.Epic => Epic,
            SalvageCoreRarity.Rare => Rare,
            _ => DimWhite
        };

        private static void AddSpacer(VBoxContainer parent, float height)
        {
            var spacer = new Control();
            spacer.CustomMinimumSize = new Vector2(0, height);
            parent.AddChild(spacer);
        }
    }
}
