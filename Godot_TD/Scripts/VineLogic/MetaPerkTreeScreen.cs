using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// The permanent perk tree, reached from the Command Center (Data/perk_tree.json). Three lanes
    /// side by side; in each, rows open as points are spent higher up the lane. Click a card to
    /// take a rank, right-click to take one back (unless something further down needs it).
    /// Keystones (◆) change how a run plays. Code-built, styled to match the Command Center.
    /// </summary>
    public partial class MetaPerkTreeScreen : CanvasLayer
    {
        private static readonly Color NetworkColor = new(0.35f, 0.85f, 1f);
        private static readonly Color PlayerColor = new(0.78f, 0.56f, 1f);
        private static readonly Color HarvesterColor = new(0.45f, 0.95f, 0.55f);

        private const float LaneW = 560f;
        private const float LaneGap = 36f;
        private const float CardW = 272f;
        private const float CardH = 112f;
        private const float CardGap = 16f;
        private const float RowHeaderH = 24f;
        private const float RowGap = 14f;
        private const float LaneHeaderH = 58f;

        private MetaPerkSaveData _saveData;
        private Control _tree;
        private ScrollContainer _scroll;
        private Label _pointsLabel;
        private Label _countLabel;
        private Label _notice;
        private Button _resetBtn;
        private readonly Dictionary<string, Button> _cards = new();
        private readonly Dictionary<MetaPerkLane, Label> _laneSpent = new();
        private readonly List<(MetaPerkLane lane, int needs, Label label)> _rowLabels = new();

        /// <summary>Cards by perk id (tests press them).</summary>
        internal IReadOnlyDictionary<string, Button> Cards => _cards;

        public override void _Ready()
        {
            Layer = 10;
            _saveData = GameManager.Instance?.MetaSave ?? MetaPerkSave.Load();
            if (GameManager.Instance != null) GameManager.Instance.MetaSave = _saveData;
            BuildUI();
            if (_saveData.MigratedRefund > 0)
            {
                _notice.Text = $"The perk tree was rebuilt. Your {_saveData.MigratedRefund} point{(_saveData.MigratedRefund == 1 ? " was" : "s were")} refunded: spend {(_saveData.MigratedRefund == 1 ? "it" : "them")} again.";
                _notice.Visible = true;
                _saveData.MigratedRefund = 0;
                Save();
            }
            Refresh();
            CallDeferred(nameof(ScrollToNext));
        }

        /// <summary>Open the tree where the next buyable perk is (a full tree opens at its new rows).</summary>
        private void ScrollToNext()
        {
            if (_scroll == null) return;
            var next = MetaPerkRegistry.GetAll().Where(n => StateOf(n) == CardState.Buyable && !n.Repeatable)
                .OrderBy(n => n.Needs).FirstOrDefault()
                ?? MetaPerkRegistry.GetAll().Where(n => StateOf(n) == CardState.Buyable).OrderBy(n => n.Needs).FirstOrDefault();
            if (next == null || !_cards.TryGetValue(next.Id, out var card)) return;
            _scroll.ScrollVertical = Mathf.Max(0, (int)(card.Position.Y - RowHeaderH - LaneHeaderH));
        }

        /// <summary>How far down the tree is scrolled (tests).</summary>
        internal int ScrolledTo => _scroll?.ScrollVertical ?? 0;

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

        private static float LaneX(int i) => i * (LaneW + LaneGap);
        private static float TreeWidth => 3 * LaneW + 2 * LaneGap;

        internal static Color LaneColor(MetaPerkLane lane) => lane switch
        {
            MetaPerkLane.Network => NetworkColor,
            MetaPerkLane.Player => PlayerColor,
            _ => HarvesterColor,
        };

        private static List<int> Thresholds()
            => MetaPerkRegistry.GetAll().Select(n => n.Needs).Distinct().OrderBy(n => n).ToList();

        private static float RowTop(int rowIdx) => LaneHeaderH + rowIdx * (RowHeaderH + CardH + RowGap);

        private void BuildUI()
        {
            var root = MetaUiStyle.Backdrop();
            AddChild(root);

            var margin = new MarginContainer();
            margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            foreach (var side in new[] { "left", "right" }) margin.AddThemeConstantOverride($"margin_{side}", 60);
            margin.AddThemeConstantOverride("margin_top", 24);
            margin.AddThemeConstantOverride("margin_bottom", 22);
            root.AddChild(margin);

            var col = new VBoxContainer();
            col.AddThemeConstantOverride("separation", 8);
            margin.AddChild(col);

            var header = new HBoxContainer();
            col.AddChild(header);
            var titles = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            titles.AddChild(MetaUiStyle.Label("PERK TREE", 34, MetaUiStyle.Frame));
            titles.AddChild(MetaUiStyle.Label("Permanent upgrades every run starts with. Click a card to take a rank, right-click to take one back.", 15, MetaUiStyle.TextDim));
            header.AddChild(titles);

            var pointsBox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            _pointsLabel = MetaUiStyle.Label("", 22, MetaUiStyle.Currency, HorizontalAlignment.Right);
            _countLabel = MetaUiStyle.Label("", 14, MetaUiStyle.TextDim, HorizontalAlignment.Right);
            pointsBox.AddChild(_pointsLabel);
            pointsBox.AddChild(_countLabel);
            header.AddChild(pointsBox);

            _notice = MetaUiStyle.Label("", 16, MetaUiStyle.Currency, HorizontalAlignment.Center);
            _notice.Name = "MigrationNotice";
            _notice.Visible = false;
            col.AddChild(_notice);

            // The tree scrolls: seven rows don't fit a 1080p screen
            _scroll = new ScrollContainer
            {
                Name = "TreeScroll",
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            };
            col.AddChild(_scroll);
            var center = new CenterContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _scroll.AddChild(center);
            var thresholds = Thresholds();
            _tree = new Control { CustomMinimumSize = new Vector2(TreeWidth, RowTop(thresholds.Count)) };
            center.AddChild(_tree);

            foreach (var (lane, name, blurb) in MetaPerkRegistry.Lanes)
                BuildLane(lane, name, blurb, thresholds);

            var footer = new HBoxContainer();
            footer.AddThemeConstantOverride("separation", 16);
            col.AddChild(footer);

            var back = MetaUiStyle.Button("BACK TO COMMAND CENTER", MetaUiStyle.Frame, new Vector2(300, 46));
            back.Pressed += Leave;
            footer.AddChild(back);

            var how = MetaUiStyle.Label(EarnHint(), 14, MetaUiStyle.TextDim, HorizontalAlignment.Center);
            how.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            how.VerticalAlignment = VerticalAlignment.Center;
            how.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            footer.AddChild(how);

            _resetBtn = MetaUiStyle.Button("RESET TREE", MetaUiStyle.Danger, new Vector2(220, 46));
            _resetBtn.TooltipText = "Refund every point and choose again";
            _resetBtn.Pressed += OnReset;
            footer.AddChild(_resetBtn);

            back.CallDeferred(Control.MethodName.GrabFocus);
        }

        private static string EarnHint()
        {
            int every = MetaPerkRegistry.PointsPerWaves;
            var firsts = VineWaveLoader.LoadMilestones(1).Where(m => m.MetaPoints > 0).Select(m => $"W{m.Wave}").ToList();
            string first = firsts.Count == 0 ? ""
                : $", one more the first time you reach {(firsts.Count == 1 ? firsts[0] : string.Join(", ", firsts.Take(firsts.Count - 1)) + " or " + firsts[^1])} on each planet";
            string asc = MetaPerkRegistry.AscendantKillPoints > 0 ? ", and one for every Ascendant you kill" : "";
            return $"Earn a point for every {every} waves you clear in a run{first}{asc}.";
        }

        private void BuildLane(MetaPerkLane lane, string name, string blurb, List<int> thresholds)
        {
            int li = LaneIndex(lane);
            var color = LaneColor(lane);
            float x0 = LaneX(li);

            var head = new VBoxContainer { Position = new Vector2(x0, 0), Size = new Vector2(LaneW, LaneHeaderH), MouseFilter = Control.MouseFilterEnum.Ignore };
            head.AddThemeConstantOverride("separation", 0);
            var title = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            var nameL = MetaUiStyle.Label(name, 22, color);
            nameL.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            title.AddChild(nameL);
            var spent = MetaUiStyle.Label("", 15, MetaUiStyle.TextDim, HorizontalAlignment.Right);
            spent.VerticalAlignment = VerticalAlignment.Center;
            title.AddChild(spent);
            _laneSpent[lane] = spent;
            head.AddChild(title);
            head.AddChild(MetaUiStyle.Label(blurb, 13, MetaUiStyle.TextDim));
            _tree.AddChild(head);

            for (int r = 0; r < thresholds.Count; r++)
            {
                int needs = thresholds[r];
                var perks = MetaPerkRegistry.GetAll().Where(n => n.Lane == lane && n.Needs == needs).ToList();
                if (perks.Count == 0) continue;
                float y = RowTop(r);
                var rowLabel = MetaUiStyle.Label("", 12, MetaUiStyle.TextFaint);
                rowLabel.Position = new Vector2(x0, y);
                rowLabel.Size = new Vector2(LaneW, RowHeaderH);
                rowLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
                _tree.AddChild(rowLabel);
                _rowLabels.Add((lane, needs, rowLabel));
                for (int i = 0; i < perks.Count; i++)
                    BuildCard(perks[i], new Vector2(x0 + i * (CardW + CardGap), y + RowHeaderH));
            }
        }

        private void BuildCard(MetaPerkNode node, Vector2 pos)
        {
            var card = new Button
            {
                Name = $"Perk_{node.Id}",
                Position = pos,
                Size = new Vector2(CardW, CardH),
                CustomMinimumSize = new Vector2(CardW, CardH),
                FocusMode = Control.FocusModeEnum.All,
                Text = "",
                ClipContents = true,
            };
            var v = new VBoxContainer { Name = "Body", MouseFilter = Control.MouseFilterEnum.Ignore };
            v.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            v.OffsetLeft = 12; v.OffsetRight = -12; v.OffsetTop = 8; v.OffsetBottom = -6;
            v.AddThemeConstantOverride("separation", 2);

            var top = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            var nameL = MetaUiStyle.Label((node.IsKeystone ? "◆ " : "") + node.Name.ToUpper(), 15, MetaUiStyle.Text);
            nameL.Name = "Name";
            nameL.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            nameL.ClipText = true;
            nameL.MouseFilter = Control.MouseFilterEnum.Ignore;
            top.AddChild(nameL);
            var rank = MetaUiStyle.Label("", 13, MetaUiStyle.TextDim, HorizontalAlignment.Right);
            rank.Name = "Rank";
            rank.MouseFilter = Control.MouseFilterEnum.Ignore;
            top.AddChild(rank);
            top.Name = "Top";
            v.AddChild(top);

            var desc = MetaUiStyle.Label("", 13, MetaUiStyle.TextDim);
            desc.Name = "Desc";
            desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            desc.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            desc.MouseFilter = Control.MouseFilterEnum.Ignore;
            v.AddChild(desc);

            var cost = MetaUiStyle.Label("", 12, MetaUiStyle.Currency);
            cost.Name = "Cost";
            cost.MouseFilter = Control.MouseFilterEnum.Ignore;
            v.AddChild(cost);
            card.AddChild(v);

            string id = node.Id;
            card.Pressed += () => OnBuy(id);
            card.GuiInput += e =>
            {
                if (e is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Right && !mb.Pressed)
                {
                    card.AcceptEvent();
                    OnRefund(id);
                }
            };
            _tree.AddChild(card);
            _cards[id] = card;
        }

        // ── State ──

        private enum CardState { Maxed, Buyable, NoPoints, Locked }

        private CardState StateOf(MetaPerkNode node)
        {
            int r = MetaPerkRegistry.RankOf(_saveData, node.Id);
            if (r >= node.MaxRank) return CardState.Maxed;
            if (!MetaPerkRegistry.IsOpen(node, _saveData)) return CardState.Locked;
            return MetaPerkRegistry.WhyNot(node, _saveData) == null ? CardState.Buyable : CardState.NoPoints;
        }

        internal string TooltipOf(string id) => _cards.TryGetValue(id, out var c) ? c.TooltipText : null;

        private void Refresh()
        {
            foreach (var node in MetaPerkRegistry.GetAll())
            {
                if (!_cards.TryGetValue(node.Id, out var card)) continue;
                var lane = LaneColor(node.Lane);
                var state = StateOf(node);
                int r = MetaPerkRegistry.RankOf(_saveData, node.Id);
                int bw = node.IsKeystone ? 3 : 2;
                bool owned = r > 0;

                StyleBoxFlat normal, hover;
                Color nameCol, descCol;
                if (state == CardState.Locked)
                {
                    normal = MetaUiStyle.Box(new Color(MetaUiStyle.Panel.R, MetaUiStyle.Panel.G, MetaUiStyle.Panel.B, 0.55f),
                        new Color(0.35f, 0.4f, 0.48f, 0.35f), node.IsKeystone ? 2 : 1, 4, 0);
                    hover = normal;
                    nameCol = MetaUiStyle.TextFaint;
                    descCol = new Color(MetaUiStyle.TextFaint.R, MetaUiStyle.TextFaint.G, MetaUiStyle.TextFaint.B, 0.85f);
                }
                else
                {
                    var bg = owned ? new Color(lane.R * 0.2f, lane.G * 0.2f, lane.B * 0.2f, 0.95f) : MetaUiStyle.Panel;
                    float edge = state == CardState.Buyable ? 0.85f : owned ? 1f : 0.4f;
                    normal = MetaUiStyle.Box(bg, new Color(lane.R, lane.G, lane.B, edge), bw, 4, 0);
                    hover = state == CardState.Buyable
                        ? MetaUiStyle.Box(new Color(lane.R * 0.26f, lane.G * 0.26f, lane.B * 0.26f, 1f), lane, bw, 4, 0)
                        : normal;
                    nameCol = owned ? Colors.White : state == CardState.Buyable ? lane : new Color(lane.R, lane.G, lane.B, 0.8f);
                    descCol = owned ? lane.Lightened(0.4f) : state == CardState.Buyable ? MetaUiStyle.Text : MetaUiStyle.TextDim;
                }
                card.AddThemeStyleboxOverride("normal", normal);
                card.AddThemeStyleboxOverride("hover", hover);
                card.AddThemeStyleboxOverride("pressed", hover);
                card.AddThemeStyleboxOverride("focus", hover);
                card.AddThemeStyleboxOverride("disabled", normal);
                // Owned cards stay enabled so right-click can take a rank back
                card.Disabled = state == CardState.Locked;
                card.MouseDefaultCursorShape = state == CardState.Buyable ? Control.CursorShape.PointingHand : Control.CursorShape.Arrow;

                string refund = owned ? (MetaPerkRegistry.WhyNotRefund(node, _saveData) is { } why ? $"Can't take back: {why}" : "Right-click to take a rank back") : null;
                card.TooltipText = state switch
                {
                    CardState.Buyable => owned ? $"Click for rank {r + 1}.\n{refund}" : "Click to take this perk",
                    CardState.Maxed => node.MaxRank > 1 ? $"All {node.MaxRank} ranks taken.\n{refund}" : $"Owned.\n{refund}",
                    CardState.NoPoints => owned ? $"{MetaPerkRegistry.WhyNot(node, _saveData)}.\n{refund}" : MetaPerkRegistry.WhyNot(node, _saveData),
                    _ => MetaPerkRegistry.WhyNot(node, _saveData),
                };

                var name = card.GetNode<Label>("Body/Top/Name");
                name.AddThemeColorOverride("font_color", nameCol);
                var rankL = card.GetNode<Label>("Body/Top/Rank");
                rankL.Text = node.Repeatable ? (owned ? $"RANK {r}" : "")
                    : node.MaxRank > 1 ? $"{new string('■', r)}{new string('□', node.MaxRank - r)}" : (owned ? "OWNED" : "");
                rankL.AddThemeColorOverride("font_color", owned ? lane : MetaUiStyle.TextFaint);
                var desc = card.GetNode<Label>("Body/Desc");
                // What it does now (the tooltip says what the next rank makes it)
                desc.Text = node.Describe(r > 0 ? r : 1);
                if (r > 0 && r < node.MaxRank) card.TooltipText += $"\nNext rank: {node.Describe(r + 1)}";
                desc.AddThemeColorOverride("font_color", descCol);
                var cost = card.GetNode<Label>("Body/Cost");
                cost.Text = state == CardState.Maxed ? "" : state == CardState.Locked
                    ? $"opens at {node.Needs} points higher up"
                    : node.Repeatable ? $"{node.Cost} point a rank, no limit"
                    : $"{node.Cost} point{(node.Cost == 1 ? "" : "s")}{(node.MaxRank > 1 ? " a rank" : "")}";
                cost.AddThemeColorOverride("font_color", state == CardState.Buyable ? MetaUiStyle.Currency
                    : state == CardState.Locked ? MetaUiStyle.TextFaint : new Color(MetaUiStyle.Currency.R, MetaUiStyle.Currency.G, MetaUiStyle.Currency.B, 0.6f));
            }

            foreach (var (lane, needs, label) in _rowLabels)
            {
                int have = MetaPerkRegistry.LowerSpent(_saveData, lane, needs);
                label.Text = needs == 0 ? "OPEN" : have >= needs ? $"OPEN  ({needs} points spent above)" : $"OPENS AT {needs} POINTS SPENT ABOVE  ({have}/{needs})";
                label.AddThemeColorOverride("font_color", have >= needs ? LaneColor(lane).Darkened(0.1f) : MetaUiStyle.TextFaint);
            }
            foreach (var kv in _laneSpent)
                kv.Value.Text = $"{MetaPerkRegistry.LaneSpent(_saveData, kv.Key)} spent";

            int pts = _saveData.AvailablePoints;
            _pointsLabel.Text = pts > 0 ? $"{pts} POINT{(pts == 1 ? "" : "S")} TO SPEND" : "NO POINTS TO SPEND";
            _pointsLabel.AddThemeColorOverride("font_color", pts > 0 ? MetaUiStyle.Currency : MetaUiStyle.TextFaint);
            int spentAll = MetaPerkRegistry.Spent(_saveData);
            int fixedSpent = MetaPerkRegistry.SpentFixed(_saveData);
            _countLabel.Text = $"{fixedSpent} / {MetaPerkRegistry.TotalCost} points in the tree" + (spentAll > fixedSpent ? $"  ·  {spentAll - fixedSpent} in Mastery" : "");
            _resetBtn.Disabled = spentAll == 0;
            _resetBtn.Text = spentAll == 0 ? "RESET TREE" : $"RESET TREE (+{spentAll})";
        }

        // ── Actions ──

        internal void OnBuy(string id)
        {
            if (!MetaPerkRegistry.TryBuy(id, _saveData)) return;
            GD.Print($"[MetaPerk] {id} -> rank {MetaPerkRegistry.RankOf(_saveData, id)} (points left: {_saveData.AvailablePoints})");
            Save();
            Refresh();
            Flash(id);
        }

        internal void OnRefund(string id)
        {
            if (!MetaPerkRegistry.TryRefund(id, _saveData)) return;
            GD.Print($"[MetaPerk] {id} -> rank {MetaPerkRegistry.RankOf(_saveData, id)} (refunded)");
            Save();
            Refresh();
        }

        private void OnReset()
        {
            int refund = MetaPerkRegistry.Reset(_saveData);
            if (refund == 0) return;
            GD.Print($"[MetaPerk] Reset tree, refunded {refund} point(s)");
            Save();
            Refresh();
        }

        private void Flash(string id)
        {
            if (!_cards.TryGetValue(id, out var card)) return;
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
