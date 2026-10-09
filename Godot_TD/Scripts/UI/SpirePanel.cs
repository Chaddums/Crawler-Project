using System.Linq;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// The Spire menu (F at the Spire): upgrades for the Spire paid in Resources, upgrades for
    /// BIT paid in Materials banked at the Spire, and quick actions (climb in, refill BIT's
    /// Materials, train BIT). Code-built so it works without the web UI layer.
    /// </summary>
    public partial class SpirePanel : CanvasLayer
    {
        private readonly SpireStation _station;
        private VBoxContainer _spireRows, _bitRows;
        private Label _currency, _footer;
        private Button _dock, _refill, _train;
        private float _refreshTimer;

        public SpirePanel(SpireStation station) { _station = station; }

        public override void _Ready()
        {
            Layer = 25;
            Name = "SpirePanel";
            var center = new CenterContainer();
            center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            center.MouseFilter = Control.MouseFilterEnum.Ignore;
            AddChild(center);

            var panel = new PanelContainer { CustomMinimumSize = new Vector2(900, 0) };
            var st = new StyleBoxFlat { BgColor = new Color(0.03f, 0.04f, 0.07f, 0.94f) };
            st.SetCornerRadiusAll(6);
            st.SetBorderWidthAll(2);
            st.BorderColor = BitPalette.Accent * 0.75f;
            st.ContentMarginLeft = st.ContentMarginRight = 24;
            st.ContentMarginTop = st.ContentMarginBottom = 18;
            panel.AddThemeStyleboxOverride("panel", st);
            center.AddChild(panel);

            var v = new VBoxContainer();
            v.AddThemeConstantOverride("separation", 12);
            panel.AddChild(v);

            var head = new HBoxContainer();
            head.AddChild(L("SPIRE", 30, BitPalette.Accent));
            head.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
            head.AddChild(L("[F] or [Esc] to close", 16, new Color(0.62f, 0.64f, 0.7f)));
            v.AddChild(head);

            // The role picked before the run, and its own twist
            var roleData = string.IsNullOrEmpty(RoleRun.Role) ? null : SpireData.Get(RoleRun.Role);
            if (roleData != null)
            {
                var sig = roleData.RoleBonuses.Find(b => b.Signature)?.Text ?? roleData.RolePlaystyle;
                var role = L($"{roleData.DisplayName.ToUpper()}: {sig}", 16, roleData.Color.Lightened(0.25f));
                role.Name = "RoleLine";
                role.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                role.CustomMinimumSize = new Vector2(850, 0);
                v.AddChild(role);
            }

            _currency = L("", 18, new Color(0.88f, 0.9f, 0.96f));
            v.AddChild(_currency);

            var cols = new HBoxContainer();
            cols.AddThemeConstantOverride("separation", 28);
            v.AddChild(cols);
            _spireRows = Column(cols, "SPIRE  (Resources)", new Color(0.95f, 0.8f, 0.3f));
            _bitRows = Column(cols, "BIT  (banked Materials)", new Color(0.45f, 0.65f, 1f));

            var actions = new HBoxContainer();
            actions.AddThemeConstantOverride("separation", 12);
            v.AddChild(actions);
            _dock = Btn("Climb in  [G]", () => _station.Dock());
            _refill = Btn("", () => _station.Refill());
            _train = Btn("", () => _station.Train());
            actions.AddChild(_dock);
            actions.AddChild(_refill);
            actions.AddChild(_train);

            // One-shot strikes for spare Resources: buy charges here, fire with 1, 2, 3
            var strikeHead = L("STRIKES  (Resources: buy a charge, fire it with its key where you aim)", 17, new Color(1f, 0.8f, 0.4f));
            v.AddChild(strikeHead);
            var strikes = new HBoxContainer();
            strikes.AddThemeConstantOverride("separation", 12);
            v.AddChild(strikes);
            foreach (var sk in _station.Data.Strikes)
            {
                string id = sk.Id;
                var col = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                col.AddThemeConstantOverride("separation", 2);
                var b = Btn("", () => _station.BuyStrike(id));
                b.Name = $"Strike_{id}";
                b.CustomMinimumSize = new Vector2(270, 40);
                col.AddChild(b);
                var t = L(sk.Text, 14, new Color(0.62f, 0.66f, 0.74f));
                t.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                t.CustomMinimumSize = new Vector2(270, 0);
                col.AddChild(t);
                strikes.AddChild(col);
                _strikeButtons[id] = b;
            }

            _footer = L("", 16, new Color(0.66f, 0.68f, 0.74f));
            _footer.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _footer.CustomMinimumSize = new Vector2(850, 0);
            v.AddChild(_footer);

            _station.Changed += Refresh;
            Refresh();
        }

        public override void _ExitTree()
        {
            if (_station != null) _station.Changed -= Refresh;
        }

        public override void _Process(double delta)
        {
            if (!Visible) return;
            _refreshTimer -= (float)delta;
            if (_refreshTimer <= 0f) { _refreshTimer = 0.25f; Refresh(); }
        }

        public void Refresh()
        {
            if (_spireRows == null || !IsInstanceValid(_station)) return;
            var gm = GameManager.Instance;
            ServiceLocator.TryGet<VinePlayer>(out var player);
            ServiceLocator.TryGet<VineGrid>(out var grid);
            var h = grid?.Harvester;
            string mat = h != null && h.SelectedMaterial != MaterialType.None ? $" ({h.SelectedMaterial})" : "";
            _currency.Text = $"Resources {gm?.CurrentResources ?? 0}     Materials banked {_station.MaterialsBanked:F0}{mat}     "
                + $"BIT Materials {player?.CurrentMaterials ?? 0:F0}/{player?.MaxMaterials ?? 0:F0}";

            Fill(_spireRows, "spire");
            Fill(_bitRows, "bit");

            var b = _station.Data.Bank;
            _refill.Text = $"Refill BIT Materials  ({b.RefillCost:F0})";
            _refill.Disabled = !_station.CanRefill;
            _train.Text = $"Train BIT  ({b.TrainCost:F0} for {b.TrainXp:F0} XP)";
            _train.Disabled = !_station.CanTrain;

            foreach (var sk in _station.Data.Strikes)
            {
                if (!_strikeButtons.TryGetValue(sk.Id, out var sb)) continue;
                int have = _station.Charges(sk.Id);
                sb.Text = $"[{sk.Key}] {sk.Name}  {_station.StrikeCost(sk.Id)}r" + (have > 0 ? $"  ({have} ready)" : "");
                sb.Disabled = _station.CantBuyStrike(sk.Id) != null;
            }

            bool materialsMode = h?.CurrentMode == MiningMode.Materials;
            _footer.Text = (materialsMode
                    ? $"Materials mode: {_station.Data.MaterialsMode.DropShare * 100:F0}% of every drop is banked here as Materials."
                    : "Switch the Spire to Materials mode [T] to bank Materials for BIT's upgrades.")
                + "  Hold F beside a damaged Spire to repair it with BIT's own Materials.";
        }

        private readonly System.Collections.Generic.Dictionary<string, (Label title, Button buy)> _rows = new();
        private readonly System.Collections.Generic.Dictionary<string, Button> _strikeButtons = new();
        /// <summary>A strike's buy button (tests).</summary>
        internal Button StrikeButton(string id) => _strikeButtons.TryGetValue(id, out var b) ? b : null;

        /// <summary>Rows are built once and updated in place (no node churn while open).</summary>
        private void Fill(VBoxContainer rows, string group)
        {
            foreach (var u in _station.Data.Upgrades.Where(u => u.Group == group))
            {
                if (!_rows.TryGetValue(u.Id, out var r))
                {
                    var row = new HBoxContainer();
                    row.AddThemeConstantOverride("separation", 10);
                    var text = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                    text.AddThemeConstantOverride("separation", 0);
                    var title = L("", 18, new Color(0.92f, 0.94f, 1f));
                    text.AddChild(title);
                    text.AddChild(L(u.Text, 15, new Color(0.62f, 0.66f, 0.74f)));
                    row.AddChild(text);
                    string id = u.Id;
                    var buy = Btn("", () => _station.Buy(id));
                    buy.CustomMinimumSize = new Vector2(120, 40);
                    buy.Name = $"Buy_{u.Id}";
                    row.AddChild(buy);
                    rows.AddChild(row);
                    r = (title, buy);
                    _rows[u.Id] = r;
                }
                int lv = _station.LevelOf(u.Id);
                r.title.Text = $"{u.Name}   {lv}/{u.MaxLevel}";
                string why = _station.CantBuy(u.Id);
                r.buy.Text = lv >= u.MaxLevel ? "MAX" : $"BUY  {u.CostAt(lv)}";
                r.buy.Disabled = why != null;
                r.buy.TooltipText = why ?? "";
            }
        }

        /// <summary>The buy button for an upgrade (tests).</summary>
        internal Button BuyButton(string id) => _rows.TryGetValue(id, out var r) ? r.buy : null;

        private static VBoxContainer Column(HBoxContainer parent, string title, Color col)
        {
            var c = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(410, 0) };
            c.AddThemeConstantOverride("separation", 10);
            c.AddChild(L(title, 18, col));
            parent.AddChild(c);
            return c;
        }

        private static Label L(string text, int size, Color col)
        {
            var l = new Label { Text = text };
            l.AddThemeFontSizeOverride("font_size", size);
            l.AddThemeColorOverride("font_color", col);
            return l;
        }

        private static Button Btn(string text, System.Action onPress)
        {
            var b = new Button { Text = text, CustomMinimumSize = new Vector2(0, 40), FocusMode = Control.FocusModeEnum.None };
            b.AddThemeFontSizeOverride("font_size", 16);
            b.Pressed += onPress;
            return b;
        }
    }
}
