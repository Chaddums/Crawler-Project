using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Click a placed tower to inspect it: what it does, its numbers, the next level up or (at the
    /// top level) a choice of two branches, and selling. Lives in the battle scene; the panel
    /// sits above the build bar on the right. Esc, the close button or clicking empty ground
    /// closes it.
    /// </summary>
    public partial class TowerInspector : Node
    {
        public static TowerInspector Current { get; private set; }
        public VineNode Selected { get; private set; }
        public bool PanelOpen => GodotObject.IsInstanceValid(_panel) && _panel.Visible && IsInstanceValid(Selected);

        private TowerPanel _panel;
        private MeshInstance3D _rangeRing;
        private readonly ClickGuard _click = new(MouseButton.Left);

        public override void _Ready()
        {
            Name = "TowerInspector";
            Current = this;
        }

        public override void _ExitTree()
        {
            if (Current == this) Current = null;
            if (IsInstanceValid(_rangeRing)) _rangeRing.QueueFree();
        }

        /// <summary>
        /// The tower under the mouse: the one whose body is nearest the cursor on screen (a tower
        /// stands up off its cell, so the ground point under the cursor is often the cell behind).
        /// </summary>
        public static VineNode TowerUnderCursor(Viewport vp, Vector2? at = null)
        {
            var cam = vp?.GetCamera3D();
            if (cam == null || vp.GetTree() == null) return null;
            var mouse = at ?? vp.GetMousePosition();
            float scale = vp.GetVisibleRect().Size.Y / 1080f;
            float best = 46f * scale;
            VineNode pick = null;
            foreach (var n in vp.GetTree().GetNodesInGroup(Constants.GROUP_VINE_NODE))
            {
                if (n is not VineNode vn || vn.Data == null || vn.IsDestroyed || !vn.IsInsideTree()) continue;
                var p = vn.GlobalPosition + Vector3.Up * 0.5f;
                if (cam.IsPositionBehind(p)) continue;
                float d = cam.UnprojectPosition(p).DistanceTo(mouse);
                if (d < best) { best = d; pick = vn; }
            }
            return pick;
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey k && k.Pressed && !k.Echo && k.Keycode == Key.Escape && PanelOpen)
            {
                Close();
                GetViewport().SetInputAsHandled();
                return;
            }
            if (@event is not InputEventMouseButton mb || mb.ButtonIndex != MouseButton.Left) return;
            if (ServiceLocator.TryGet<VinePlacer>(out var placer) && (placer.IsPlacing || placer.IsPlacingMiningBuilding)) return;
            if (SpireStation.Current?.Docked == true || SpireStation.Current?.PanelOpen == true || VinePerkScreen.IsOverlayOpen) return;
            var phase = GameManager.Instance?.CurrentPhase ?? GamePhase.Build;
            if (phase is not (GamePhase.Build or GamePhase.Wave or GamePhase.WaveComplete)) return;
            bool click = _click.IsClick(@event);
            var tower = TowerUnderCursor(GetViewport(), mb.Position);
            if (mb.Pressed)
            {
                // A press on a tower is a pick, not BIT's trigger (BIT sees it after this)
                if (tower != null) GetViewport().SetInputAsHandled();
                return;
            }
            if (!click) return;
            if (tower != null) { Open(tower); GetViewport().SetInputAsHandled(); }
            else if (PanelOpen) Close();
        }

        public void Open(VineNode tower)
        {
            if (tower?.Data == null) return;
            Selected = tower;
            if (!GodotObject.IsInstanceValid(_panel))
            {
                _panel = new TowerPanel(this);
                GetTree().CurrentScene.AddChild(_panel);
            }
            _panel.Visible = true;
            _panel.ShowTower(tower);
            ShowRange(tower);
        }

        public void Close()
        {
            Selected = null;
            if (GodotObject.IsInstanceValid(_panel)) _panel.Visible = false;
            if (IsInstanceValid(_rangeRing)) _rangeRing.Visible = false;
        }

        public override void _Process(double delta)
        {
            if (Selected != null && (!IsInstanceValid(Selected) || Selected.IsDestroyed || !Selected.IsInsideTree())) Close();
            if (PanelOpen) ShowRange(Selected);
        }

        /// <summary>A flat ring at the tower's reach while its panel is open.</summary>
        private void ShowRange(VineNode tower)
        {
            float r = tower.CurrentRange;
            if (r <= 0f) { if (IsInstanceValid(_rangeRing)) _rangeRing.Visible = false; return; }
            if (!IsInstanceValid(_rangeRing))
            {
                _rangeRing = new MeshInstance3D
                {
                    Name = "TowerRangeRing",
                    MaterialOverride = new StandardMaterial3D
                    {
                        AlbedoColor = new Color(0.35f, 0.8f, 1f, 0.45f),
                        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                        NoDepthTest = true,
                    },
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                };
                GetTree().CurrentScene.AddChild(_rangeRing);
            }
            if (_rangeRing.Mesh is not TorusMesh t || !Mathf.IsEqualApprox(t.OuterRadius, r))
                _rangeRing.Mesh = new TorusMesh { InnerRadius = r - 0.35f, OuterRadius = r, Rings = 72, RingSegments = 4 };
            var p = tower.GlobalPosition;
            float y = ServiceLocator.TryGet<VineGrid>(out var g) ? g.GetWorldHeight(p.X, p.Z) : p.Y;
            _rangeRing.GlobalPosition = new Vector3(p.X, y + 0.06f, p.Z);
            _rangeRing.Scale = new Vector3(1f, 0.05f, 1f);
            _rangeRing.Visible = true;
        }

        public void Sell()
        {
            var t = Selected;
            if (!IsInstanceValid(t) || !ServiceLocator.TryGet<VineGrid>(out var grid)) return;
            int refund = t.SellValue;
            grid.RemoveNode(t.GridPosition);
            GameManager.Instance?.RefundResources(refund);
            Close();
        }
    }

    /// <summary>The panel itself (code-built, like the Spire's).</summary>
    public partial class TowerPanel : CanvasLayer
    {
        private readonly TowerInspector _owner;
        private VineNode _tower;
        private Label _title, _level, _role, _stats, _vs, _nextHead, _nextText, _branchTaken, _footer;
        private Button _upgrade, _sell;
        private HBoxContainer _branchRow;
        private readonly List<(Button buy, Label name, Label text)> _branchCards = new();
        private float _refresh;

        public TowerPanel(TowerInspector owner) { _owner = owner; }

        public override void _Ready()
        {
            Layer = 24;
            Name = "TowerPanel";
            var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
            root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(root);

            var panel = new PanelContainer { Name = "TowerPanelBox" };
            panel.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
            panel.GrowHorizontal = Control.GrowDirection.Begin;
            panel.GrowVertical = Control.GrowDirection.Begin;
            panel.OffsetRight = -14;
            panel.OffsetBottom = -122; // above the build bar
            panel.CustomMinimumSize = new Vector2(560, 0);
            var st = new StyleBoxFlat { BgColor = new Color(0.03f, 0.04f, 0.07f, 0.95f) };
            st.SetCornerRadiusAll(6);
            st.SetBorderWidthAll(2);
            st.BorderColor = new Color(0.9f, 0.5f, 0.2f, 0.8f);
            st.ContentMarginLeft = st.ContentMarginRight = 18;
            st.ContentMarginTop = st.ContentMarginBottom = 14;
            panel.AddThemeStyleboxOverride("panel", st);
            root.AddChild(panel);

            var v = new VBoxContainer();
            v.AddThemeConstantOverride("separation", 6);
            panel.AddChild(v);

            var head = new HBoxContainer();
            head.AddThemeConstantOverride("separation", 10);
            _title = L("", 24, new Color(1f, 0.72f, 0.35f));
            head.AddChild(_title);
            _level = L("", 17, new Color(0.45f, 0.82f, 1f));
            _level.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            head.AddChild(_level);
            head.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
            var close = Btn("Close [Esc]", () => _owner.Close());
            close.CustomMinimumSize = new Vector2(0, 32);
            head.AddChild(close);
            v.AddChild(head);

            _role = Wrap(L("", 16, new Color(0.9f, 0.92f, 0.96f)));
            v.AddChild(_role);
            _vs = Wrap(L("", 15, new Color(0.62f, 0.66f, 0.74f)));
            v.AddChild(_vs);
            _stats = Wrap(L("", 16, new Color(0.85f, 0.85f, 0.6f)));
            v.AddChild(_stats);
            v.AddChild(new HSeparator());

            _nextHead = L("", 17, new Color(0.45f, 0.82f, 1f));
            v.AddChild(_nextHead);
            var nextRow = new HBoxContainer();
            nextRow.AddThemeConstantOverride("separation", 10);
            _nextText = Wrap(L("", 15, new Color(0.85f, 0.88f, 0.95f)));
            _nextText.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _nextText.CustomMinimumSize = new Vector2(360, 0);
            nextRow.AddChild(_nextText);
            _upgrade = Btn("", () => { if (_tower?.TryUpgrade() == true) ShowTower(_tower); });
            _upgrade.Name = "UpgradeButton";
            _upgrade.CustomMinimumSize = new Vector2(150, 40);
            nextRow.AddChild(_upgrade);
            v.AddChild(nextRow);

            _branchRow = new HBoxContainer();
            _branchRow.AddThemeConstantOverride("separation", 10);
            v.AddChild(_branchRow);
            for (int i = 0; i < 2; i++)
            {
                var card = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(255, 0) };
                var cs = new StyleBoxFlat { BgColor = new Color(0.08f, 0.09f, 0.13f, 0.95f) };
                cs.SetCornerRadiusAll(4);
                cs.SetBorderWidthAll(1);
                cs.BorderColor = new Color(1f, 0.78f, 0.25f, 0.6f);
                cs.ContentMarginLeft = cs.ContentMarginRight = 10;
                cs.ContentMarginTop = cs.ContentMarginBottom = 8;
                card.AddThemeStyleboxOverride("panel", cs);
                var cv = new VBoxContainer();
                cv.AddThemeConstantOverride("separation", 4);
                card.AddChild(cv);
                var name = L("", 17, new Color(1f, 0.8f, 0.35f));
                cv.AddChild(name);
                var text = Wrap(L("", 15, new Color(0.82f, 0.85f, 0.92f)));
                text.CustomMinimumSize = new Vector2(230, 0);
                // Takes the spare height, so both BUY buttons line up at the bottom
                text.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
                cv.AddChild(text);
                int idx = i;
                var buy = Btn("", () => BuyBranch(idx));
                buy.Name = $"BranchButton{i}";
                buy.CustomMinimumSize = new Vector2(0, 38);
                cv.AddChild(buy);
                _branchRow.AddChild(card);
                _branchCards.Add((buy, name, text));
            }
            _branchTaken = Wrap(L("", 15, new Color(1f, 0.82f, 0.4f)));
            v.AddChild(_branchTaken);

            v.AddChild(new HSeparator());
            var foot = new HBoxContainer();
            _sell = Btn("", () => _owner.Sell());
            _sell.Name = "SellButton";
            _sell.CustomMinimumSize = new Vector2(170, 36);
            foot.AddChild(_sell);
            foot.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
            _footer = L("Click another tower to inspect it", 14, new Color(0.55f, 0.58f, 0.66f));
            _footer.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            foot.AddChild(_footer);
            v.AddChild(foot);
        }

        private void BuyBranch(int i)
        {
            var choices = _tower?.BranchChoices;
            if (choices == null || i >= choices.Count) return;
            if (_tower.TryBranch(choices[i].Id)) ShowTower(_tower);
        }

        public void ShowTower(VineNode tower)
        {
            _tower = tower;
            if (_title == null || !IsInstanceValid(tower) || tower.Data == null) return;
            var info = TowerInfo.Get(tower.Data.Type);
            _title.Text = tower.Branch != null ? $"{tower.Data.Name}: {tower.Branch.Name}" : tower.Data.Name;
            _level.Text = tower.UpgradePlan != null ? $"Level {tower.Level}/{tower.MaxLevel}" + (tower.Branch != null ? " + branch" : "") : "";
            var br = tower.Branch;
            string role = br?.Role ?? (info.Role.Length > 0 ? info.Role : tower.Data.Description);
            string best = br?.Best ?? info.Best, weak = br?.Weak ?? info.Weak;
            _role.Text = role;
            _vs.Text = best.Length > 0 ? $"Good against: {best}.   Weak against: {weak}." : "";
            _vs.Visible = _vs.Text.Length > 0;
            Refresh();
        }

        public void Refresh()
        {
            if (!IsInstanceValid(_tower) || _tower.Data == null) return;
            _stats.Text = _tower.StatLine();
            var next = _tower.NextLevel;
            var choices = _tower.BranchChoices;
            bool hasPlan = _tower.UpgradePlan != null;

            _nextHead.Visible = hasPlan && (next != null || choices.Count > 0);
            _nextText.GetParent<Control>().Visible = next != null;
            if (next != null)
            {
                _nextHead.Text = $"NEXT: LEVEL {_tower.Level + 1}";
                _nextText.Text = VineNode.StepText(next);
                string why = _tower.CantUpgrade();
                _upgrade.Text = $"UPGRADE  {next.Cost}r";
                _upgrade.Disabled = why != null;
                _upgrade.TooltipText = "";
            }
            else if (choices.Count > 0) _nextHead.Text = "CHOOSE A BRANCH (one only)";

            _branchRow.Visible = choices.Count > 0;
            for (int i = 0; i < _branchCards.Count; i++)
            {
                var (buy, name, text) = _branchCards[i];
                bool show = i < choices.Count;
                buy.GetParent().GetParent<Control>().Visible = show;
                if (!show) continue;
                var b = choices[i];
                name.Text = b.Name;
                text.Text = b.Text;
                buy.Text = $"BUY  {b.Cost}r";
                buy.Disabled = _tower.CantBranch(b.Id) != null;
            }

            _branchTaken.Visible = _tower.Branch != null;
            if (_tower.Branch != null) _branchTaken.Text = $"{_tower.Branch.Name}: {_tower.Branch.Text}";
            _sell.Text = $"Sell for {_tower.SellValue}r";
        }

        public override void _Process(double delta)
        {
            if (!Visible) return;
            if ((_refresh -= (float)delta) <= 0f) { _refresh = 0.25f; Refresh(); }
        }

        /// <summary>Buttons by name (tests).</summary>
        internal Button UpgradeButton => _upgrade;
        internal Button BranchButton(int i) => i < _branchCards.Count ? _branchCards[i].buy : null;
        internal Button SellButton => _sell;

        private static Label L(string text, int size, Color col)
        {
            var l = new Label { Text = text };
            l.AddThemeFontSizeOverride("font_size", size);
            l.AddThemeColorOverride("font_color", col);
            return l;
        }

        private static Label Wrap(Label l)
        {
            l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            l.CustomMinimumSize = new Vector2(520, 0);
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
