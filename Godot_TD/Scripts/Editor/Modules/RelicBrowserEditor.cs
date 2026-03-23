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

            // ── Right: detail inspector ──
            var rightPanel = new PanelContainer();
            rightPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            rightPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            rightPanel.AddThemeStyleboxOverride("panel", EditorStyles.MakePanel(EditorStyles.BgPanel));
            split.AddChild(rightPanel);

            _detailBox = new VBoxContainer();
            _detailBox.AddThemeConstantOverride("separation", 10);
            rightPanel.AddChild(_detailBox);

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

        private void ShowDetail(RelicRegistry.Relic relic)
        {
            _detailName.Text = relic.Name;
            _detailName.AddThemeColorOverride("font_color", relic.Tint);

            _detailRarity.Text = $"Rarity: {relic.Rarity.ToUpper()}";
            _detailIcon.Text = $"Icon: {relic.Icon}";
            _detailDesc.Text = relic.Desc;

            _detailTradeoff.Text = relic.Tradeoff != null ? $"Tradeoff: {relic.Tradeoff}" : "";
            _detailTradeoff.Visible = relic.Tradeoff != null;

            _detailTint.Color = relic.Tint;
        }
    }
}
