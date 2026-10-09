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
        public TowerPanel Panel => GodotObject.IsInstanceValid(_panel) ? _panel : null;

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
            if (@event is InputEventKey k && k.Pressed && !k.Echo && k.Keycode == Key.Escape && (PanelOpen || Selection.Count > 0))
            {
                Close();
                GetViewport().SetInputAsHandled();
                return;
            }
            // Shift + drag: a box that selects every tower in it
            if (@event is InputEventMouseMotion mm && _boxing)
            {
                _boxEnd = mm.Position;
                DrawBox();
                GetViewport().SetInputAsHandled();
                return;
            }
            if (@event is not InputEventMouseButton mb || mb.ButtonIndex != MouseButton.Left) return;
            if (ServiceLocator.TryGet<VinePlacer>(out var placer) && (placer.IsPlacing || placer.IsPlacingMiningBuilding)) return;
            if (SpireStation.Current?.Docked == true || SpireStation.Current?.PanelOpen == true || VinePerkScreen.IsOverlayOpen) return;
            var phase = GameManager.Instance?.CurrentPhase ?? GamePhase.Build;
            if (phase is not (GamePhase.Build or GamePhase.Wave or GamePhase.WaveComplete)) return;
            bool shift = mb.ShiftPressed || Input.IsKeyPressed(Key.Shift);
            bool ctrl = mb.CtrlPressed || Input.IsKeyPressed(Key.Ctrl);
            bool click = _click.IsClick(@event);
            var tower = TowerUnderCursor(GetViewport(), mb.Position);
            if (_boxing && !mb.Pressed)
            {
                _boxing = false;
                HideBox();
                // A Shift-drag boxes; a Shift-click (barely moved) adds or drops the tower under it
                if (_boxStart.DistanceTo(mb.Position) >= 10f) SelectInBox(_boxStart, mb.Position, add: ctrl);
                else if (tower != null) ToggleInSelection(tower);
                GetViewport().SetInputAsHandled();
                return;
            }
            if (mb.Pressed)
            {
                if (shift)
                {
                    _boxing = true;
                    _boxStart = _boxEnd = mb.Position;
                    GetViewport().SetInputAsHandled(); // BIT doesn't fire while selecting
                    return;
                }
                // A press on a tower is a pick, not BIT's trigger (BIT sees it after this)
                if (tower != null) GetViewport().SetInputAsHandled();
                return;
            }
            if (!click) return;
            if (tower != null && ctrl)
            {
                ToggleInSelection(tower);
                GetViewport().SetInputAsHandled();
            }
            else if (tower != null) { Open(tower); GetViewport().SetInputAsHandled(); }
            else if (PanelOpen || Selection.Count > 0) Close();
        }

        /// <summary>Shift or Ctrl + click: add a tower to the selection, or take it out.</summary>
        public void ToggleInSelection(VineNode tower)
        {
            var list = Selection.Count > 0 ? new List<VineNode>(Selection) : Selected != null ? new List<VineNode> { Selected } : new List<VineNode>();
            if (!list.Remove(tower)) list.Add(tower);
            if (list.Count == 1) Open(list[0]); else if (list.Count > 1) OpenMany(list); else Close();
        }

        // ── Many towers at once ──
        /// <summary>Towers picked with a Shift-drag box or Shift/Ctrl-clicks (empty with one tower open).</summary>
        public List<VineNode> Selection { get; } = new();
        private bool _boxing;
        private Vector2 _boxStart, _boxEnd;
        private CanvasLayer _boxLayer;
        private Panel _box;
        private readonly List<MeshInstance3D> _marks = new();

        private void DrawBox()
        {
            if (_boxLayer == null || !IsInstanceValid(_boxLayer))
            {
                _boxLayer = new CanvasLayer { Layer = 23, Name = "SelectBoxLayer" };
                AddChild(_boxLayer);
                _box = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
                var st = new StyleBoxFlat { BgColor = new Color(0.35f, 0.8f, 1f, 0.12f), BorderColor = new Color(0.45f, 0.85f, 1f, 0.9f) };
                st.SetBorderWidthAll(2);
                _box.AddThemeStyleboxOverride("panel", st);
                _boxLayer.AddChild(_box);
            }
            var a = _boxStart; var b = _boxEnd;
            _box.Position = new Vector2(Mathf.Min(a.X, b.X), Mathf.Min(a.Y, b.Y));
            _box.Size = (a - b).Abs();
            _box.Visible = true;
        }

        private void HideBox() { if (_box != null && IsInstanceValid(_box)) _box.Visible = false; }

        /// <summary>Select the towers whose bodies are inside the screen rectangle from <paramref name="a"/> to <paramref name="b"/>.</summary>
        public void SelectInBox(Vector2 a, Vector2 b, bool add = false)
        {
            var cam = GetViewport()?.GetCamera3D();
            if (cam == null) return;
            var rect = new Rect2(new Vector2(Mathf.Min(a.X, b.X), Mathf.Min(a.Y, b.Y)), (a - b).Abs()).Grow(6f);
            var list = add ? new List<VineNode>(Selection) : new List<VineNode>();
            foreach (var n in GetTree().GetNodesInGroup(Constants.GROUP_VINE_NODE))
            {
                if (n is not VineNode vn || vn.Data == null || vn.IsDestroyed || !vn.IsInsideTree() || list.Contains(vn)) continue;
                var p = vn.GlobalPosition + Vector3.Up * 0.5f;
                if (!cam.IsPositionBehind(p) && rect.HasPoint(cam.UnprojectPosition(p))) list.Add(vn);
            }
            if (list.Count == 0) { Close(); return; }
            if (list.Count == 1) Open(list[0]); else OpenMany(list);
        }

        public void OpenMany(List<VineNode> towers)
        {
            towers.RemoveAll(t => !IsInstanceValid(t) || t.IsDestroyed);
            if (towers.Count == 0) { Close(); return; }
            Selected = null;
            Selection.Clear();
            Selection.AddRange(towers);
            if (!GodotObject.IsInstanceValid(_panel))
            {
                _panel = new TowerPanel(this);
                GetTree().CurrentScene.AddChild(_panel);
            }
            _panel.Visible = true;
            _panel.ShowMany(Selection);
            if (IsInstanceValid(_rangeRing)) _rangeRing.Visible = false;
            MarkSelection();
        }

        /// <summary>A small ring under each selected tower.</summary>
        private void MarkSelection()
        {
            while (_marks.Count < Selection.Count)
            {
                var m = new MeshInstance3D
                {
                    Mesh = new TorusMesh { InnerRadius = 0.7f, OuterRadius = 0.85f, Rings = 32, RingSegments = 4 },
                    MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.45f, 0.85f, 1f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, NoDepthTest = true },
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Scale = new Vector3(1f, 0.05f, 1f),
                };
                GetTree().CurrentScene.AddChild(m);
                _marks.Add(m);
            }
            for (int i = 0; i < _marks.Count; i++)
            {
                bool on = i < Selection.Count && IsInstanceValid(Selection[i]);
                _marks[i].Visible = on;
                if (on) _marks[i].GlobalPosition = Selection[i].GlobalPosition + Vector3.Down * 0.3f;
            }
        }

        /// <summary>The next step for a tower in a group: its next level, or the branch <paramref name="like"/> took.</summary>
        private static (TowerUpgradeStep step, bool branch) NextStep(VineNode t, VineNode like)
        {
            if (!IsInstanceValid(t) || t.IsDestroyed || t.UpgradePlan == null) return (null, false);
            if (t.NextLevel != null) return (t.NextLevel, false);
            if (t.Branch == null && like?.Branch != null && t.Data?.Type == like.Data?.Type)
            {
                var b = t.BranchChoices.FirstOrDefault(x => x.Id == like.Branch.Id);
                if (b != null) return (b, true);
            }
            return (null, false);
        }

        /// <summary>
        /// One step for each tower (cheapest first, while the Resources last): what it would buy now.
        /// <paramref name="like"/> gives the branch for towers at the branch stage.
        /// </summary>
        public static (int count, int cost, int waiting) PlanUpgrade(IEnumerable<VineNode> towers, VineNode like)
        {
            int money = GameManager.Instance?.CurrentResources ?? 0, count = 0, cost = 0, waiting = 0;
            foreach (var price in towers.Select(t => NextStep(t, like)).Where(x => x.step != null).Select(x => VineNode.PriceOf(x.step)).OrderBy(p => p))
            {
                waiting++;
                if (cost + price > money) continue;
                cost += price;
                count++;
            }
            return (count, cost, waiting);
        }

        /// <summary>Buy one step for each tower, cheapest first, while the Resources last. Returns how many.</summary>
        public static int UpgradeMany(IEnumerable<VineNode> towers, VineNode like)
        {
            int done = 0;
            foreach (var (t, step, branch) in towers.Select(t => { var (s, b) = NextStep(t, like); return (t, s, b); })
                         .Where(x => x.s != null).OrderBy(x => VineNode.PriceOf(x.s)).ToList())
            {
                bool ok = branch ? t.TryBranch(step.Id) : t.TryUpgrade();
                if (ok) done++;
            }
            if (done > 0) GD.Print($"[Upgrades] {done} towers upgraded at once");
            return done;
        }

        /// <summary>Every tower of <paramref name="like"/>'s type on the field.</summary>
        public List<VineNode> AllOfType(VineNode like)
        {
            var list = new List<VineNode>();
            if (like?.Data == null) return list;
            foreach (var n in GetTree().GetNodesInGroup(Constants.GROUP_VINE_NODE))
                if (n is VineNode vn && !vn.IsDestroyed && vn.Data?.Type == like.Data.Type) list.Add(vn);
            return list;
        }

        public void SellMany(List<VineNode> towers)
        {
            if (!ServiceLocator.TryGet<VineGrid>(out var grid)) return;
            int refund = 0;
            foreach (var t in towers.ToList())
            {
                if (!IsInstanceValid(t) || t.IsDestroyed) continue;
                refund += t.SellValue;
                grid.RemoveNode(t.GridPosition);
            }
            GameManager.Instance?.RefundResources(refund);
            Close();
        }

        public void Open(VineNode tower)
        {
            if (tower?.Data == null) return;
            Selected = tower;
            Selection.Clear();
            MarkSelection();
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
            Selection.Clear();
            MarkSelection();
            if (GodotObject.IsInstanceValid(_panel)) _panel.Visible = false;
            if (IsInstanceValid(_rangeRing)) _rangeRing.Visible = false;
        }

        public override void _Process(double delta)
        {
            if (Selected != null && (!IsInstanceValid(Selected) || Selected.IsDestroyed || !Selected.IsInsideTree())) Close();
            if (PanelOpen) ShowRange(Selected);
            if (Selection.Count > 0)
            {
                int before = Selection.Count;
                Selection.RemoveAll(t => !IsInstanceValid(t) || t.IsDestroyed);
                if (Selection.Count == 0) Close();
                else if (Selection.Count != before) MarkSelection();
            }
        }

        /// <summary>The panel is open on a group of towers.</summary>
        public bool ManyOpen => GodotObject.IsInstanceValid(_panel) && _panel.Visible && Selection.Count > 1;

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
        private Label _title, _level, _role, _stats, _vs, _nextHead, _nextText, _branchTaken, _footer, _rank;
        private Button _upgrade, _sell, _upgradeAll;
        private List<VineNode> _many;
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
            _rank = Wrap(L("", 15, new Color(1f, 0.82f, 0.35f)));
            _rank.Name = "RankLine";
            v.AddChild(_rank);
            v.AddChild(new HSeparator());

            _nextHead = L("", 17, new Color(0.45f, 0.82f, 1f));
            v.AddChild(_nextHead);
            var nextRow = new HBoxContainer();
            nextRow.AddThemeConstantOverride("separation", 10);
            _nextText = Wrap(L("", 15, new Color(0.85f, 0.88f, 0.95f)));
            _nextText.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _nextText.CustomMinimumSize = new Vector2(360, 0);
            nextRow.AddChild(_nextText);
            // Shift-click upgrades every tower of this type a step
            _upgrade = Btn("", () =>
            {
                if (Input.IsKeyPressed(Key.Shift)) { UpgradeAllOfType(); return; }
                if (_tower?.TryUpgrade() == true) ShowTower(_tower);
            });
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
            _sell = Btn("", () => { if (_many != null) _owner.SellMany(_many); else _owner.Sell(); });
            _sell.Name = "SellButton";
            _sell.CustomMinimumSize = new Vector2(170, 36);
            foot.AddChild(_sell);
            _upgradeAll = Btn("", () => { if (_many != null) UpgradeSelection(); else UpgradeAllOfType(); });
            _upgradeAll.Name = "UpgradeAllButton";
            _upgradeAll.CustomMinimumSize = new Vector2(250, 36);
            foot.AddChild(_upgradeAll);
            foot.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
            _footer = L("Shift-drag to select many", 14, new Color(0.55f, 0.58f, 0.66f));
            _footer.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            foot.AddChild(_footer);
            v.AddChild(foot);
        }

        private void UpgradeAllOfType()
        {
            if (!IsInstanceValid(_tower)) return;
            TowerInspector.UpgradeMany(_owner.AllOfType(_tower), _tower);
            ShowTower(_tower);
        }

        private void UpgradeSelection()
        {
            if (_many == null) return;
            TowerInspector.UpgradeMany(_many, null);
            ShowMany(_many);
        }

        /// <summary>The panel for a group: what's in it, upgrade them all a step, sell them all.</summary>
        public void ShowMany(List<VineNode> towers)
        {
            _many = towers;
            _tower = null;
            if (_title == null) return;
            Refresh();
        }

        private void RefreshMany()
        {
            _many.RemoveAll(t => !IsInstanceValid(t) || t.IsDestroyed);
            _title.Text = $"{_many.Count} towers selected";
            _level.Text = "";
            _role.Text = string.Join(", ", _many.GroupBy(t => t.Data.Name).Select(g => $"{g.Count()} {g.Key}"));
            _vs.Visible = false;
            int vets = _many.Count(t => t.Rank > 0);
            _stats.Text = $"Kills between them: {_many.Sum(t => t.Kills)}" + (vets > 0 ? $"  ·  {vets} veteran{(vets == 1 ? "" : "s")}" : "");
            _rank.Visible = false;
            _nextHead.Visible = true;
            _nextHead.Text = "Upgrade them all a step (cheapest first) or sell them together";
            _nextText.GetParent<Control>().Visible = false;
            _branchRow.Visible = false;
            _branchTaken.Visible = false;
            var (n, cost, waiting) = TowerInspector.PlanUpgrade(_many, null);
            _upgradeAll.Visible = true;
            _upgradeAll.Text = waiting == 0 ? "All at the top (branches: one at a time)" : n == 0 ? $"UPGRADE ALL: not enough Resources" : $"UPGRADE {n} OF {waiting}  {cost}r";
            _upgradeAll.Disabled = n == 0;
            int refund = _many.Sum(t => t.SellValue);
            _sell.Text = $"Sell all for {refund}r";
            _footer.Text = "Esc to clear the selection";
        }

        /// <summary>Buttons by name (tests).</summary>
        internal Button UpgradeAllButton => _upgradeAll;

        private void BuyBranch(int i)
        {
            var choices = _tower?.BranchChoices;
            if (choices == null || i >= choices.Count) return;
            if (_tower.TryBranch(choices[i].Id)) ShowTower(_tower);
        }

        public void ShowTower(VineNode tower)
        {
            _tower = tower;
            _many = null;
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
            if (_many != null) { RefreshMany(); return; }
            if (!IsInstanceValid(_tower) || _tower.Data == null) return;
            _stats.Text = _tower.StatLine();
            _rank.Visible = _tower.Data.AutoFires && _tower.Data.Type != VineNodeType.BuffEmitter;
            _rank.Text = _tower.RankLine();
            _footer.Text = "Shift-click UPGRADE: every one of this type  ·  Shift-drag: select many";
            // Every tower of this type a step at once
            var all = _owner.AllOfType(_tower);
            var (n, cost, waiting) = TowerInspector.PlanUpgrade(all, _tower);
            _upgradeAll.Visible = all.Count > 1 && waiting > 0;
            _upgradeAll.Text = n == 0 ? $"UPGRADE ALL {all.Count}: not enough" : $"UPGRADE ALL {n} {_tower.Data.Name.ToUpperInvariant()}S  {cost}r";
            _upgradeAll.Disabled = n == 0;
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
                _upgrade.Text = $"UPGRADE  {VineNode.PriceOf(next)}r";
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
                buy.Text = $"BUY  {VineNode.PriceOf(b)}r";
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
