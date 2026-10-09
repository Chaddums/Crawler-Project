using System;
using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// "Getting started": a short checklist at the top left for the first runs, ticking itself
    /// off as the player builds, mines a resource node, switches mining, uses an ability, clears
    /// a wave and upgrades a tower. The playtest found none of these discoverable (how to mine,
    /// what Q/E/R do, how a wave ends). Hidden for good once everything is ticked or the player
    /// closes it (user://ui.cfg).
    /// </summary>
    public partial class RunGuide : PanelContainer
    {
        private const string CFG = "user://ui.cfg";
        public static bool Dismissed
        {
            get { var c = new ConfigFile(); return c.Load(CFG) == Error.Ok && (bool)c.GetValue("guide", "dismissed", false); }
            set { var c = new ConfigFile(); c.Load(CFG); c.SetValue("guide", "dismissed", value); c.Save(CFG); }
        }

        private readonly List<(string id, string text, Label mark, Label label)> _items = new();
        private readonly HashSet<string> _done = new();
        private float _closeTimer = -1f;

        /// <summary>Tests: ids of the steps ticked so far.</summary>
        internal IReadOnlyCollection<string> Done => _done;

        public override void _Ready()
        {
            Name = "RunGuide";
            SetAnchorsPreset(LayoutPreset.TopLeft);
            OffsetLeft = 12;
            OffsetTop = 128;
            OffsetRight = 470;
            MouseFilter = MouseFilterEnum.Stop;
            var st = new StyleBoxFlat { BgColor = new Color(0.03f, 0.05f, 0.09f, 0.86f), BorderColor = new Color(1f, 0.78f, 0.3f, 0.55f) };
            st.SetBorderWidthAll(1);
            st.SetCornerRadiusAll(5);
            st.ContentMarginLeft = st.ContentMarginRight = 12;
            st.ContentMarginTop = st.ContentMarginBottom = 8;
            AddThemeStyleboxOverride("panel", st);
            var v = new VBoxContainer();
            v.AddThemeConstantOverride("separation", 3);
            AddChild(v);
            var head = new HBoxContainer();
            v.AddChild(head);
            var title = new Label { Text = "GETTING STARTED", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            title.AddThemeFontSizeOverride("font_size", 16);
            title.AddThemeColorOverride("font_color", new Color(1f, 0.82f, 0.35f));
            head.AddChild(title);
            var close = new Button { Text = "Hide", FocusMode = FocusModeEnum.None };
            close.AddThemeFontSizeOverride("font_size", 14);
            close.Pressed += () => { Dismissed = true; Visible = false; };
            head.AddChild(close);

            Add(v, "build", "Build a tower: pick one on the bar below, then click the ground by the enemy path.");
            Add(v, "node", $"Mine more: build next to a gold RESOURCE NODE (+{Constants.RESOURCE_NODE_BONUS} every 5 s).");
            Add(v, "mode", "[T] switches what the Spire mines: Resources for towers, Materials for BIT.");
            Add(v, "ability", "Press Q, E or R: BIT's abilities, paid in Materials (the blue bar).");
            Add(v, "wave", "A wave ends when all its enemies are down. Space sends the next one early.");
            Add(v, "upgrade", "Click a tower you built to upgrade it.");

            GameEvents.OnVineNodePlaced += OnPlaced;
            GameEvents.OnResourceNodeCaptured += OnNode;
            GameEvents.OnMiningModeChanged += OnMode;
            GameEvents.OnAbilityUsed += OnAbility;
            GameEvents.OnWaveCompleted += OnWave;
            GameEvents.OnTowerUpgraded += OnUpgrade;
        }

        public override void _ExitTree()
        {
            GameEvents.OnVineNodePlaced -= OnPlaced;
            GameEvents.OnResourceNodeCaptured -= OnNode;
            GameEvents.OnMiningModeChanged -= OnMode;
            GameEvents.OnAbilityUsed -= OnAbility;
            GameEvents.OnWaveCompleted -= OnWave;
            GameEvents.OnTowerUpgraded -= OnUpgrade;
        }

        private void OnPlaced(Node n) { if (n is VineNode v && v.Data?.Category == VineNodeCategory.Effect) Tick("build"); }
        private void OnNode(Vector2I _) => Tick("node");
        private void OnMode(MiningMode _) => Tick("mode");
        private void OnAbility(int slot, string text, bool used) { if (used) Tick("ability"); }
        private void OnWave(int _) => Tick("wave");
        private void OnUpgrade(Node _) => Tick("upgrade");

        private void Add(VBoxContainer v, string id, string text)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);
            v.AddChild(row);
            var mark = new Label { Text = "·", CustomMinimumSize = new Vector2(16, 0) };
            mark.AddThemeFontSizeOverride("font_size", 15);
            mark.AddThemeColorOverride("font_color", new Color(0.6f, 0.62f, 0.68f));
            row.AddChild(mark);
            var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            label.AddThemeFontSizeOverride("font_size", 14);
            label.AddThemeColorOverride("font_color", new Color(0.86f, 0.88f, 0.93f));
            row.AddChild(label);
            _items.Add((id, text, mark, label));
        }

        private void Tick(string id)
        {
            if (!_done.Add(id)) return;
            foreach (var it in _items)
            {
                if (it.id != id) continue;
                it.mark.Text = "✓";
                it.mark.AddThemeColorOverride("font_color", new Color(0.45f, 1f, 0.55f));
                it.label.AddThemeColorOverride("font_color", new Color(0.55f, 0.6f, 0.62f));
            }
            if (_done.Count >= _items.Count) _closeTimer = 4f;
        }

        public override void _Process(double delta)
        {
            if (_closeTimer < 0f) return;
            _closeTimer -= (float)delta;
            if (_closeTimer <= 0f) { Dismissed = true; Visible = false; _closeTimer = -1f; }
        }
    }
}
