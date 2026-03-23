using Godot;

namespace JunkyardTD
{
    public partial class RelicBrowserEditor : EditorModule
    {
        public override string ModuleName => "Relics";
        public override Color AccentColor => new Color(0.3f, 0.8f, 0.6f);

        private VBoxContainer _listBox;
        private VBoxContainer _detailBox;
        private Label _detailName;
        private Label _detailDesc;
        private Label _detailRarity;
        private Label _detailIcon;
        private Label _detailTradeoff;
        private ColorRect _detailTint;

        // ── Loot UI preview ──
        private LootLabelUI _previewLabel;
        private LootFlyoutScreen _previewFlyout;

        public override void _Ready()
        {
            var split = new HSplitContainer();
            split.SizeFlagsVertical = SizeFlags.ExpandFill;
            split.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            AddChild(split);

            // ── Left: relic list ──
            var leftPanel = new PanelContainer();
            leftPanel.CustomMinimumSize = new Vector2(260, 0);
            leftPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            leftPanel.AddThemeStyleboxOverride("panel", EditorStyles.MakePanel(EditorStyles.BgPanel));
            split.AddChild(leftPanel);

            var leftScroll = new ScrollContainer();
            leftScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            leftScroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            leftPanel.AddChild(leftScroll);

            var leftVBox = new VBoxContainer();
            leftVBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            leftVBox.AddThemeConstantOverride("separation", 2);
            leftScroll.AddChild(leftVBox);

            leftVBox.AddChild(EditorStyles.MakeLabel("Relic Registry", 16, AccentColor));
            leftVBox.AddChild(EditorStyles.MakeSeparator());

            _listBox = new VBoxContainer();
            _listBox.AddThemeConstantOverride("separation", 2);
            leftVBox.AddChild(_listBox);

            // ── Right: detail inspector + preview ──
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
            _detailBox.AddThemeConstantOverride("separation", 10);
            _detailBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            rightScroll.AddChild(_detailBox);

            _detailName = EditorStyles.MakeLabel("Select a relic", 20, EditorStyles.TextSecondary);
            _detailBox.AddChild(_detailName);

            _detailRarity = EditorStyles.MakeLabel("", 12, EditorStyles.TextMuted);
            _detailBox.AddChild(_detailRarity);

            _detailBox.AddChild(EditorStyles.MakeSeparator());

            _detailIcon = EditorStyles.MakeLabel("", 14, EditorStyles.TextSecondary);
            _detailBox.AddChild(_detailIcon);

            _detailDesc = EditorStyles.MakeLabel("", 14, EditorStyles.TextPrimary);
            _detailDesc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _detailBox.AddChild(_detailDesc);

            _detailTradeoff = EditorStyles.MakeLabel("", 13, EditorStyles.StatusWarn);
            _detailTradeoff.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _detailBox.AddChild(_detailTradeoff);

            var tintRow = new HBoxContainer();
            tintRow.AddThemeConstantOverride("separation", 8);
            _detailBox.AddChild(tintRow);

            tintRow.AddChild(EditorStyles.MakeLabel("Tint:", 12, EditorStyles.TextMuted));

            _detailTint = new ColorRect();
            _detailTint.CustomMinimumSize = new Vector2(24, 24);
            _detailTint.Color = Colors.Black;
            tintRow.AddChild(_detailTint);

            // ── Preview section ──
            _detailBox.AddChild(EditorStyles.MakeSeparator());
            _detailBox.AddChild(EditorStyles.MakeLabel("Loot UI Preview", 16, AccentColor));

            // Label preview (native fallback — shows rarity-colored text)
            var labelPreviewBox = new PanelContainer();
            labelPreviewBox.CustomMinimumSize = new Vector2(0, 48);
            labelPreviewBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            labelPreviewBox.AddThemeStyleboxOverride("panel", EditorStyles.MakePanel(new Color(0.03f, 0.05f, 0.12f)));
            _detailBox.AddChild(labelPreviewBox);

            _previewLabel = new LootLabelUI();
            _previewLabel.SetAnchorsPreset(LayoutPreset.FullRect);
            labelPreviewBox.AddChild(_previewLabel);

            _detailBox.AddChild(EditorStyles.MakeLabel("Ground Label ↑  |  Flyout Preview ↓", 11, EditorStyles.TextMuted));

            // Flyout preview button
            var previewBtn = EditorStyles.MakeButton("Open Flyout Preview", 13, AccentColor);
            previewBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            previewBtn.CustomMinimumSize = new Vector2(0, 32);
            previewBtn.Pressed += OnPreviewFlyoutPressed;
            _detailBox.AddChild(previewBtn);

            PopulateList();
        }

        public override void OnActivated()
        {
            PopulateList();
        }

        private void PopulateList()
        {
            foreach (var child in _listBox.GetChildren())
                child.QueueFree();

            foreach (var relic in RelicRegistry.All)
            {
                var btn = EditorStyles.MakeButton(relic.Name, 13, relic.Tint);
                btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                btn.CustomMinimumSize = new Vector2(0, 28);

                var r = relic; // capture
                btn.Pressed += () => ShowDetail(r);

                _listBox.AddChild(btn);
            }
        }

        private RelicRegistry.Relic _selectedRelic;

        private void ShowDetail(RelicRegistry.Relic relic)
        {
            _selectedRelic = relic;

            _detailName.Text = relic.Name;
            _detailName.AddThemeColorOverride("font_color", relic.Tint);

            _detailRarity.Text = $"Rarity: {relic.Rarity.ToUpper()}";
            _detailIcon.Text = $"Icon: {relic.Icon}";
            _detailDesc.Text = relic.Desc;

            _detailTradeoff.Text = relic.Tradeoff != null ? $"Tradeoff: {relic.Tradeoff}" : "";
            _detailTradeoff.Visible = relic.Tradeoff != null;

            _detailTint.Color = relic.Tint;

            // Update label preview
            _previewLabel?.PushRelic(relic);
        }

        private void OnPreviewFlyoutPressed()
        {
            if (_selectedRelic.Id == null)
            {
                GD.Print("[RelicEditor] Select a relic first");
                return;
            }

            // Create or reuse the flyout preview overlay
            if (_previewFlyout == null || !IsInstanceValid(_previewFlyout))
            {
                _previewFlyout = new LootFlyoutScreen();
                _previewFlyout.SetAnchorsPreset(LayoutPreset.FullRect);
                _previewFlyout.ZIndex = 100;
                GetTree().Root.AddChild(_previewFlyout);
            }

            _previewFlyout.ShowRelic(_selectedRelic);
        }
    }
}
