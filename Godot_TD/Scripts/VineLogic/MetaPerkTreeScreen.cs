using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Permanent perk tree, reached from the Command Center. Points come from wave
    /// milestones (milestones.json "metaPoints"), once per planet. One perk per tier;
    /// notable tiers and the tier after them let you switch lanes, the others continue
    /// the lane you are in (MetaPerkRegistry.CanAllocate).
    /// Code-built, styled to match the Command Center (MetaUiStyle).
    /// </summary>
    public partial class MetaPerkTreeScreen : CanvasLayer
    {
        // Lane colours: distinct from each other and from the currency amber
        private static readonly Color NetworkColor = new(0.35f, 0.85f, 1f);
        private static readonly Color PlayerColor = new(0.78f, 0.56f, 1f);
        private static readonly Color HarvesterColor = new(0.45f, 0.95f, 0.55f);
        private static readonly Color LinkIdle = new(0.42f, 0.5f, 0.62f, 0.28f);

        private const int MaxTier = 8;
        private const float TierColW = 170f;
        private const float LaneW = 300f;
        private const float LaneGap = 40f;
        private const float CardH = 62f;
        private const float RowPitch = 88f;
        private const float LaneHeaderH = 56f;
        private const float RootY = 70f;
        private const float RootH = 34f;
        private const float FirstRowY = 140f;

        private MetaPerkSaveData _saveData;
        private HashSet<int> _allocated;
        private Control _tree;
        private Control _links;
        private Label _pointsLabel;
        private Label _countLabel;
        private Button _resetBtn;
        private readonly Dictionary<int, Button> _cards = new();

        public override void _Ready()
        {
            Layer = 10;
            _saveData = GameManager.Instance?.MetaSave ?? MetaPerkSave.Load();
            if (GameManager.Instance != null) GameManager.Instance.MetaSave = _saveData;
            _allocated = new HashSet<int>(_saveData.AllocatedIds);
            BuildUI();
            Refresh();
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
            {
                GetViewport().SetInputAsHandled();
                Leave();
            }
        }

        // ── Layout ──

        private static int LaneIndex(MetaPerkLane lane) => lane switch
        {
            MetaPerkLane.Network => 0,
            MetaPerkLane.Player => 1,
            _ => 2,
        };

        private static float LaneX(int laneIdx) => TierColW + laneIdx * (LaneW + LaneGap);
        private static float LaneCenterX(int laneIdx) => LaneX(laneIdx) + LaneW / 2f;
        private static float RowY(int tier) => FirstRowY + (tier - 1) * RowPitch;
        private static float TreeWidth => TierColW + 3 * LaneW + 2 * LaneGap;
        private static float TreeHeight => RowY(MaxTier) + CardH + 8f;

        private static Color LaneColor(MetaPerkLane lane) => lane switch
        {
            MetaPerkLane.Network => NetworkColor,
            MetaPerkLane.Player => PlayerColor,
            _ => HarvesterColor,
        };

        /// <summary>Tiers that must continue the lane of the tier before (see CanAllocate).</summary>
        private static bool SameLaneTier(int tier) => tier is 2 or 5 or 8;

        private void BuildUI()
        {
            var root = MetaUiStyle.Backdrop();
            AddChild(root);

            var margin = new MarginContainer();
            margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            foreach (var side in new[] { "left", "right" }) margin.AddThemeConstantOverride($"margin_{side}", 60);
            margin.AddThemeConstantOverride("margin_top", 28);
            margin.AddThemeConstantOverride("margin_bottom", 24);
            root.AddChild(margin);

            var col = new VBoxContainer();
            col.AddThemeConstantOverride("separation", 10);
            margin.AddChild(col);

            // Header: title left, points right
            var header = new HBoxContainer();
            col.AddChild(header);
            var titles = new VBoxContainer();
            titles.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            titles.AddChild(MetaUiStyle.Label("PERK TREE", 34, MetaUiStyle.Frame));
            titles.AddChild(MetaUiStyle.Label("Permanent upgrades. Every run starts with them.", 15, MetaUiStyle.TextDim));
            header.AddChild(titles);

            var pointsBox = new VBoxContainer();
            pointsBox.Alignment = BoxContainer.AlignmentMode.Center;
            _pointsLabel = MetaUiStyle.Label("", 22, MetaUiStyle.Currency, HorizontalAlignment.Right);
            _countLabel = MetaUiStyle.Label("", 14, MetaUiStyle.TextDim, HorizontalAlignment.Right);
            pointsBox.AddChild(_pointsLabel);
            pointsBox.AddChild(_countLabel);
            header.AddChild(pointsBox);

            // Tree
            var center = new CenterContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            col.AddChild(center);
            _tree = new Control { CustomMinimumSize = new Vector2(TreeWidth, TreeHeight) };
            center.AddChild(_tree);

            _links = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
            _links.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _links.Draw += DrawLinks;
            _tree.AddChild(_links);

            BuildLaneHeaders();
            BuildRoot();
            BuildTierLabels();
            foreach (var node in MetaPerkRegistry.GetAll())
                if (node.Tier > 0) BuildCard(node);

            // Footer
            var footer = new HBoxContainer();
            footer.AddThemeConstantOverride("separation", 16);
            col.AddChild(footer);

            var back = MetaUiStyle.Button("BACK TO COMMAND CENTER", MetaUiStyle.Frame, new Vector2(300, 46));
            back.Pressed += Leave;
            footer.AddChild(back);

            var how = MetaUiStyle.Label(EarnHint(), 14, MetaUiStyle.TextDim, HorizontalAlignment.Center);
            how.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            how.VerticalAlignment = VerticalAlignment.Center;
            footer.AddChild(how);

            _resetBtn = MetaUiStyle.Button("RESET TREE", MetaUiStyle.Danger, new Vector2(220, 46));
            _resetBtn.TooltipText = "Refund every point and choose again";
            _resetBtn.Pressed += OnReset;
            footer.AddChild(_resetBtn);

            back.CallDeferred(Control.MethodName.GrabFocus);
        }

        private static string EarnHint()
        {
            var waves = VineWaveLoader.LoadMilestones(1).Where(m => m.MetaPoints > 0).Select(m => $"W{m.Wave}").ToList();
            if (waves.Count == 0) return "Points come from wave milestones.";
            string list = waves.Count == 1 ? waves[0]
                : string.Join(", ", waves.Take(waves.Count - 1)) + " and " + waves[^1];
            return $"Earn a point the first time you reach {list} on each planet.";
        }

        private void BuildLaneHeaders()
        {
            var lanes = new (MetaPerkLane lane, string name, string blurb)[] {
                (MetaPerkLane.Network, "NETWORK", "towers, range and signals"),
                (MetaPerkLane.Player, "BIT", "your hull, attacks and Materials"),
                (MetaPerkLane.Harvester, "SPIRE", "Resources, refunds and core lives"),
            };
            foreach (var (lane, name, blurb) in lanes)
            {
                int i = LaneIndex(lane);
                var box = new VBoxContainer
                {
                    Position = new Vector2(LaneX(i), 0),
                    Size = new Vector2(LaneW, LaneHeaderH),
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                };
                box.AddThemeConstantOverride("separation", 0);
                box.AddChild(MetaUiStyle.Label(name, 20, LaneColor(lane), HorizontalAlignment.Center));
                box.AddChild(MetaUiStyle.Label(blurb, 13, MetaUiStyle.TextDim, HorizontalAlignment.Center));
                _tree.AddChild(box);
            }
        }

        private void BuildRoot()
        {
            var root = MetaPerkRegistry.GetNode(0);
            var pill = new PanelContainer
            {
                Position = new Vector2(LaneX(1) + 40f, RootY),
                Size = new Vector2(LaneW - 80f, RootH),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            pill.AddThemeStyleboxOverride("panel", MetaUiStyle.Box(MetaUiStyle.PanelHigh,
                new Color(MetaUiStyle.Frame.R, MetaUiStyle.Frame.G, MetaUiStyle.Frame.B, 0.5f), 1, 17, 4));
            var l = MetaUiStyle.Label($"{root?.Name?.ToUpper() ?? "ROOT"} · CONNECTED", 13, MetaUiStyle.Frame, HorizontalAlignment.Center);
            l.VerticalAlignment = VerticalAlignment.Center;
            pill.AddChild(l);
            _tree.AddChild(pill);
        }

        private void BuildTierLabels()
        {
            for (int t = 1; t <= MaxTier; t++)
            {
                string rule = t switch
                {
                    1 => "pick a lane",
                    3 or 6 => "notable · any lane",
                    _ when SameLaneTier(t) => "same lane",
                    _ => "any lane",
                };
                var box = new VBoxContainer
                {
                    Position = new Vector2(0, RowY(t) + 8f),
                    Size = new Vector2(TierColW - 20f, CardH),
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                };
                box.AddThemeConstantOverride("separation", 0);
                box.AddChild(MetaUiStyle.Label($"TIER {t}", 15, MetaUiStyle.Text));
                box.AddChild(MetaUiStyle.Label(rule, 12, t is 3 or 6 ? MetaUiStyle.Currency : MetaUiStyle.TextFaint));
                _tree.AddChild(box);
            }
        }

        private void BuildCard(MetaPerkNode node)
        {
            var card = new Button
            {
                Position = new Vector2(LaneX(LaneIndex(node.Lane)), RowY(node.Tier)),
                Size = new Vector2(LaneW, CardH),
                CustomMinimumSize = new Vector2(LaneW, CardH),
                FocusMode = Control.FocusModeEnum.All,
                Text = "",
            };
            var v = new VBoxContainer { Name = "Body", MouseFilter = Control.MouseFilterEnum.Ignore };
            v.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            v.OffsetLeft = 14; v.OffsetRight = -14; v.OffsetTop = 8; v.OffsetBottom = -6;
            v.AddThemeConstantOverride("separation", 2);
            var name = MetaUiStyle.Label((node.IsNotable ? "◆ " : "") + node.Name.ToUpper(), 16, MetaUiStyle.Text);
            name.Name = "Name";
            name.MouseFilter = Control.MouseFilterEnum.Ignore;
            var desc = MetaUiStyle.Label(node.Description, 13, MetaUiStyle.TextDim);
            desc.Name = "Desc";
            desc.MouseFilter = Control.MouseFilterEnum.Ignore;
            desc.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            v.AddChild(name);
            v.AddChild(desc);
            card.AddChild(v);

            int id = node.Id;
            card.Pressed += () => OnNodeClicked(id);
            _tree.AddChild(card);
            _cards[id] = card;
        }

        // ── State ──

        private enum CardState { Owned, Available, Reachable, Locked }

        private CardState StateOf(MetaPerkNode node)
        {
            if (_allocated.Contains(node.Id)) return CardState.Owned;
            if (MetaPerkRegistry.CanAllocate(node.Id, _allocated))
                return _saveData.AvailablePoints > 0 ? CardState.Available : CardState.Reachable;
            return CardState.Locked;
        }

        private void Refresh()
        {
            foreach (var node in MetaPerkRegistry.GetAll())
            {
                if (!_cards.TryGetValue(node.Id, out var card)) continue;
                var lane = LaneColor(node.Lane);
                var state = StateOf(node);
                int bw = node.IsNotable ? 3 : 2;

                StyleBoxFlat normal, hover;
                Color nameCol, descCol;
                switch (state)
                {
                    case CardState.Owned:
                        normal = MetaUiStyle.Box(new Color(lane.R * 0.2f, lane.G * 0.2f, lane.B * 0.2f, 0.95f), lane, bw, 4, 0);
                        hover = normal;
                        nameCol = Colors.White;
                        descCol = lane.Lightened(0.35f);
                        break;
                    case CardState.Available:
                        normal = MetaUiStyle.Box(MetaUiStyle.Panel, new Color(lane.R, lane.G, lane.B, 0.75f), bw, 4, 0);
                        hover = MetaUiStyle.Box(new Color(lane.R * 0.14f, lane.G * 0.14f, lane.B * 0.14f, 1f), lane, bw, 4, 0);
                        nameCol = lane;
                        descCol = MetaUiStyle.Text;
                        break;
                    case CardState.Reachable:
                        normal = MetaUiStyle.Box(MetaUiStyle.Panel, new Color(lane.R, lane.G, lane.B, 0.35f), bw, 4, 0);
                        hover = normal;
                        nameCol = new Color(lane.R, lane.G, lane.B, 0.8f);
                        descCol = MetaUiStyle.TextDim;
                        break;
                    default:
                        normal = MetaUiStyle.Box(new Color(MetaUiStyle.Panel.R, MetaUiStyle.Panel.G, MetaUiStyle.Panel.B, 0.55f),
                            new Color(0.35f, 0.4f, 0.48f, 0.35f), node.IsNotable ? 2 : 1, 4, 0);
                        hover = normal;
                        nameCol = MetaUiStyle.TextFaint;
                        descCol = new Color(MetaUiStyle.TextFaint.R, MetaUiStyle.TextFaint.G, MetaUiStyle.TextFaint.B, 0.8f);
                        break;
                }
                card.AddThemeStyleboxOverride("normal", normal);
                card.AddThemeStyleboxOverride("hover", hover);
                card.AddThemeStyleboxOverride("pressed", hover);
                card.AddThemeStyleboxOverride("focus", state == CardState.Available ? hover : normal);
                card.AddThemeStyleboxOverride("disabled", normal);
                card.Disabled = state != CardState.Available;
                card.MouseDefaultCursorShape = state == CardState.Available
                    ? Control.CursorShape.PointingHand : Control.CursorShape.Arrow;
                card.TooltipText = state switch
                {
                    CardState.Owned => "Owned",
                    CardState.Available => "Click to take this perk",
                    CardState.Reachable => "Needs a perk point",
                    _ => LockedReason(node),
                };
                card.GetNode<Label>("Body/Name").AddThemeColorOverride("font_color", nameCol);
                card.GetNode<Label>("Body/Desc").AddThemeColorOverride("font_color", descCol);
            }

            int pts = _saveData.AvailablePoints;
            _pointsLabel.Text = pts > 0 ? $"{pts} POINT{(pts == 1 ? "" : "S")} TO SPEND" : "NO POINTS TO SPEND";
            _pointsLabel.AddThemeColorOverride("font_color", pts > 0 ? MetaUiStyle.Currency : MetaUiStyle.TextFaint);
            int owned = _allocated.Count(id => id != 0);
            _countLabel.Text = $"{owned} / {MaxTier} tiers taken";
            _resetBtn.Disabled = owned == 0;
            _resetBtn.Text = owned == 0 ? "RESET TREE" : $"RESET TREE (+{owned})";
            _links.QueueRedraw();
        }

        private string LockedReason(MetaPerkNode node)
        {
            if (TierTaken(node.Tier)) return "You already took a perk in this tier";
            return SameLaneTier(node.Tier)
                ? $"Needs the tier {node.Tier - 1} perk in this lane"
                : $"Needs a tier {node.Tier - 1} perk first";
        }

        private bool TierTaken(int tier)
            => MetaPerkRegistry.GetAll().Any(n => n.Tier == tier && _allocated.Contains(n.Id));

        // ── Links ──

        private void DrawLinks()
        {
            var nodes = MetaPerkRegistry.GetAll();
            MetaPerkNode At(int tier, int lane) => nodes.FirstOrDefault(n => n.Tier == tier && LaneIndex(n.Lane) == lane);

            // Root to tier 1 (any lane)
            float rootBottom = RootY + RootH;
            DrawBus(rootBottom, RowY(1), new[] { LaneCenterX(1) }, new[] { 0, 1, 2 },
                fromOwned: _ => true, target: lane => At(1, lane));

            for (int t = 2; t <= MaxTier; t++)
            {
                float top = RowY(t - 1) + CardH;
                float bottom = RowY(t);
                if (SameLaneTier(t))
                {
                    for (int lane = 0; lane < 3; lane++)
                    {
                        var from = At(t - 1, lane);
                        var to = At(t, lane);
                        if (from == null || to == null) continue;
                        float x = LaneCenterX(lane);
                        var (col, w) = LinkStyle(from, to);
                        _links.DrawLine(new Vector2(x, top), new Vector2(x, bottom), col, w, true);
                    }
                }
                else
                {
                    int tierPrev = t - 1;
                    DrawBus(top, bottom, new[] { LaneCenterX(0), LaneCenterX(1), LaneCenterX(2) }, new[] { 0, 1, 2 },
                        fromOwned: x => { var n = At(tierPrev, LaneOfX(x)); return n != null && _allocated.Contains(n.Id); },
                        target: lane => At(t, lane), fromNode: x => At(tierPrev, LaneOfX(x)));
                }
            }
        }

        private static int LaneOfX(float x)
        {
            for (int i = 0; i < 3; i++) if (Mathf.Abs(LaneCenterX(i) - x) < 1f) return i;
            return 1;
        }

        /// <summary>
        /// "Any of these to any of those": stubs down from each source to a horizontal bus,
        /// then stubs down into each target. The route actually taken is drawn in lane colour.
        /// </summary>
        private void DrawBus(float top, float bottom, float[] fromXs, int[] toLanes,
            System.Func<float, bool> fromOwned, System.Func<int, MetaPerkNode> target,
            System.Func<float, MetaPerkNode> fromNode = null)
        {
            float mid = (top + bottom) / 2f;
            float minX = Mathf.Min(fromXs.Min(), LaneCenterX(toLanes.Min()));
            float maxX = Mathf.Max(fromXs.Max(), LaneCenterX(toLanes.Max()));
            _links.DrawLine(new Vector2(minX, mid), new Vector2(maxX, mid), LinkIdle, 1.5f, true);
            foreach (float fx in fromXs)
                _links.DrawLine(new Vector2(fx, top), new Vector2(fx, mid), LinkIdle, 1.5f, true);
            foreach (int lane in toLanes)
                _links.DrawLine(new Vector2(LaneCenterX(lane), mid), new Vector2(LaneCenterX(lane), bottom), LinkIdle, 1.5f, true);

            // Highlight from each owned source to targets that are owned or open
            foreach (float fx in fromXs)
            {
                if (!fromOwned(fx)) continue;
                foreach (int lane in toLanes)
                {
                    var to = target(lane);
                    if (to == null) continue;
                    bool owned = _allocated.Contains(to.Id);
                    bool open = !owned && StateOf(to) is CardState.Available or CardState.Reachable;
                    if (!owned && !open) continue;
                    var lc = LaneColor(to.Lane);
                    var col = owned ? lc : new Color(lc.R, lc.G, lc.B, 0.45f);
                    float w = owned ? 3f : 2f;
                    float tx = LaneCenterX(lane);
                    var src = fromNode?.Invoke(fx);
                    var srcCol = owned && src != null ? LaneColor(src.Lane) : col;
                    _links.DrawLine(new Vector2(fx, top), new Vector2(fx, mid), srcCol, w, true);
                    _links.DrawLine(new Vector2(fx, mid), new Vector2(tx, mid), col, w, true);
                    _links.DrawLine(new Vector2(tx, mid), new Vector2(tx, bottom), col, w, true);
                }
            }
        }

        private (Color, float) LinkStyle(MetaPerkNode from, MetaPerkNode to)
        {
            var lc = LaneColor(to.Lane);
            if (_allocated.Contains(from.Id) && _allocated.Contains(to.Id)) return (lc, 3f);
            if (_allocated.Contains(from.Id) && StateOf(to) is CardState.Available or CardState.Reachable)
                return (new Color(lc.R, lc.G, lc.B, 0.45f), 2f);
            return (LinkIdle, 1.5f);
        }

        // ── Actions ──

        private void OnNodeClicked(int nodeId)
        {
            if (_saveData.AvailablePoints <= 0) return;
            if (!MetaPerkRegistry.CanAllocate(nodeId, _allocated)) return;

            _allocated.Add(nodeId);
            _saveData.AllocatedIds.Add(nodeId);
            _saveData.AvailablePoints--;
            GD.Print($"[MetaPerk] Allocated: {MetaPerkRegistry.GetNode(nodeId)?.Name} (points left: {_saveData.AvailablePoints})");
            Save();
            Refresh();
            Flash(nodeId);
        }

        private void OnReset()
        {
            int refund = _allocated.Count(id => id != 0);
            if (refund == 0) return;
            _allocated.Clear();
            _allocated.Add(0);
            _saveData.AllocatedIds.Clear();
            _saveData.AllocatedIds.Add(0);
            _saveData.AvailablePoints += refund;
            GD.Print($"[MetaPerk] Reset tree, refunded {refund} point(s)");
            Save();
            Refresh();
        }

        private void Flash(int nodeId)
        {
            if (!_cards.TryGetValue(nodeId, out var card)) return;
            card.Modulate = new Color(1.8f, 1.8f, 1.8f);
            CreateTween().TweenProperty(card, "modulate", Colors.White, 0.45f)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        }

        private void Save()
        {
            MetaPerkSave.Save(_saveData);
            if (GameManager.Instance != null)
                GameManager.Instance.MetaSave = _saveData;
        }

        private void Leave()
        {
            Save();
            if (GameManager.Instance != null) GameManager.Instance.ShowMetaHub();
            else GetTree().ChangeSceneToFile(Constants.SCENE_META_HUB);
        }
    }
}
