using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Persistent meta perk tree shown between floors when the player has unspent points.
    /// Code-built UI following VinePerkScreen pattern.
    /// </summary>
    public partial class MetaPerkTreeScreen : CanvasLayer
    {
        // Lane colors
        private static readonly Color NetworkColor = new(0f, 0.85f, 0.95f);
        private static readonly Color PlayerColor = new(0.5f, 0.7f, 1.0f);
        private static readonly Color HarvesterColor = new(0.9f, 0.75f, 0.2f);
        private static readonly Color LockedColor = new(0.25f, 0.25f, 0.25f);
        private static readonly Color AllocatedBorder = new(0.9f, 0.9f, 0.9f);

        private const float NodeSize = 50f;
        private const float NotableSize = 60f;
        private const float TierSpacing = 70f;
        private const float LaneSpacing = 120f;

        private MetaPerkSaveData _saveData;
        private HashSet<int> _allocatedSet;
        private Label _pointsLabel;
        private Control _treeContainer;
        private readonly Dictionary<int, Control> _nodeControls = new();

        public override void _Ready()
        {
            Layer = 10;

            _saveData = GameManager.Instance?.MetaSave ?? MetaPerkSave.Load();
            _allocatedSet = new HashSet<int>(_saveData.AllocatedIds);

            BuildUI();
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && !key.Echo)
            {
                if (key.Keycode == Key.Escape)
                {
                    GetViewport().SetInputAsHandled();
                    GameManager.Instance?.ReturnToMainMenu();
                }
            }
        }

        private void BuildUI()
        {
            // Full-screen dark background
            var bg = new ColorRect();
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            bg.Color = TronTheme.Background;
            AddChild(bg);

            // Outer centered container
            var outerCenter = new CenterContainer();
            outerCenter.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(outerCenter);

            var outerPanel = new PanelContainer();
            outerPanel.CustomMinimumSize = new Vector2(500, 720);
            var outerStyle = new StyleBoxFlat();
            outerStyle.BgColor = new Color(TronTheme.PanelBg.R, TronTheme.PanelBg.G, TronTheme.PanelBg.B, 0.95f);
            outerStyle.BorderColor = TronTheme.GridCyan;
            outerStyle.SetBorderWidthAll(2);
            outerStyle.SetCornerRadiusAll(6);
            outerStyle.ContentMarginLeft = 24;
            outerStyle.ContentMarginRight = 24;
            outerStyle.ContentMarginTop = 16;
            outerStyle.ContentMarginBottom = 16;
            outerPanel.AddThemeStyleboxOverride("panel", outerStyle);
            outerCenter.AddChild(outerPanel);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 8);
            outerPanel.AddChild(vbox);

            // Title
            var title = new Label();
            title.Text = "META PERK TREE";
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.AddThemeFontSizeOverride("font_size", 28);
            title.AddThemeColorOverride("font_color", TronTheme.GridCyan);
            vbox.AddChild(title);

            // Points label
            _pointsLabel = new Label();
            UpdatePointsLabel();
            _pointsLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _pointsLabel.AddThemeFontSizeOverride("font_size", 18);
            vbox.AddChild(_pointsLabel);

            // Lane legend
            var legend = new HBoxContainer();
            legend.Alignment = BoxContainer.AlignmentMode.Center;
            legend.AddThemeConstantOverride("separation", 20);
            AddLegendItem(legend, "Network", NetworkColor);
            AddLegendItem(legend, "Player", PlayerColor);
            AddLegendItem(legend, "Harvester", HarvesterColor);
            vbox.AddChild(legend);

            // Tree area — use a ScrollContainer wrapping a Control for the node layout
            var scroll = new ScrollContainer();
            scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            vbox.AddChild(scroll);

            _treeContainer = new Control();
            _treeContainer.CustomMinimumSize = new Vector2(440, TierSpacing * 9 + 20);
            scroll.AddChild(_treeContainer);

            // Build connection lines first (behind nodes)
            BuildConnections();

            // Build nodes — root at bottom, tier 8 at top
            var allNodes = MetaPerkRegistry.GetAll();
            foreach (var node in allNodes)
                BuildNodeControl(node);

            // Continue button
            var continueBtn = new Button();
            continueBtn.Text = "CONTINUE";
            continueBtn.CustomMinimumSize = new Vector2(0, 44);

            var btnStyle = new StyleBoxFlat();
            btnStyle.BgColor = new Color(0.0f, 0.15f, 0.2f, 1f);
            btnStyle.BorderColor = TronTheme.GridCyan;
            btnStyle.SetBorderWidthAll(2);
            btnStyle.SetCornerRadiusAll(4);
            btnStyle.ContentMarginTop = 6;
            btnStyle.ContentMarginBottom = 6;
            continueBtn.AddThemeStyleboxOverride("normal", btnStyle);
            continueBtn.AddThemeColorOverride("font_color", TronTheme.GridCyan);

            var hoverStyle = (StyleBoxFlat)btnStyle.Duplicate();
            hoverStyle.BgColor = new Color(0.0f, 0.25f, 0.35f, 1f);
            continueBtn.AddThemeStyleboxOverride("hover", hoverStyle);

            var pressedStyle = (StyleBoxFlat)btnStyle.Duplicate();
            pressedStyle.BgColor = new Color(0.0f, 0.35f, 0.45f, 1f);
            continueBtn.AddThemeStyleboxOverride("pressed", pressedStyle);

            continueBtn.Pressed += OnContinue;
            vbox.AddChild(continueBtn);

            // ESC hint
            var hint = new Label();
            hint.Text = "ESC \u2014 back to menu";
            hint.HorizontalAlignment = HorizontalAlignment.Center;
            hint.AddThemeFontSizeOverride("font_size", 12);
            hint.AddThemeColorOverride("font_color", new Color(0.35f, 0.35f, 0.35f));
            vbox.AddChild(hint);
        }

        private void AddLegendItem(HBoxContainer parent, string text, Color color)
        {
            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 4);

            var swatch = new ColorRect();
            swatch.CustomMinimumSize = new Vector2(12, 12);
            swatch.Color = color;
            hbox.AddChild(swatch);

            var label = new Label();
            label.Text = text;
            label.AddThemeFontSizeOverride("font_size", 13);
            label.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.65f));
            hbox.AddChild(label);

            parent.AddChild(hbox);
        }

        private Vector2 GetNodePosition(MetaPerkNode node)
        {
            float centerX = 220f;

            if (node.Tier == 0)
                return new Vector2(centerX, TierSpacing * 8 + 10);

            int laneIdx = node.Lane switch {
                MetaPerkLane.Network => 0,
                MetaPerkLane.Player => 1,
                MetaPerkLane.Harvester => 2,
                _ => 1
            };

            float x = centerX + (laneIdx - 1) * LaneSpacing;
            float y = TierSpacing * (8 - node.Tier) + 10;
            return new Vector2(x, y);
        }

        private void BuildConnections()
        {
            // Draw simple vertical lines from each node to its prerequisite tier
            // We'll use ColorRects as thin line segments
            var allNodes = MetaPerkRegistry.GetAll();
            foreach (var node in allNodes)
            {
                if (node.Tier == 0) continue;

                var pos = GetNodePosition(node);
                float halfSize = (node.IsNotable ? NotableSize : NodeSize) / 2f;

                if (node.Tier == 1)
                {
                    // Connect to root
                    var rootPos = GetNodePosition(MetaPerkRegistry.GetNode(0));
                    AddLine(rootPos, pos, LockedColor);
                }
                else if (node.Tier == 3 || node.Tier == 6)
                {
                    // Notables — draw horizontal crossover line across all 3 lanes
                    // Only draw this once per notable tier (do it for the Network lane node)
                    if (node.Lane == MetaPerkLane.Network)
                    {
                        var netPos = GetNodePosition(node);
                        var harvPos = GetNodePosition(FindNode(node.Tier, MetaPerkLane.Harvester));
                        float y = netPos.Y + halfSize / 2f;
                        var lineRect = new ColorRect();
                        lineRect.Color = new Color(LockedColor.R, LockedColor.G, LockedColor.B, 0.4f);
                        lineRect.Position = new Vector2(netPos.X, y);
                        lineRect.Size = new Vector2(harvPos.X - netPos.X, 2);
                        _treeContainer.AddChild(lineRect);
                    }

                    // Connect to tier below (any tier-1 node in the center)
                    int prevTier = node.Tier - 1;
                    var prevNode = FindNode(prevTier, node.Lane);
                    if (prevNode != null)
                    {
                        var prevPos = GetNodePosition(prevNode);
                        AddLine(prevPos, pos, LockedColor);
                    }
                }
                else
                {
                    // Standard: connect to same-lane node one tier below
                    int prevTier = node.Tier - 1;
                    var prevNode = FindNode(prevTier, node.Lane);
                    if (prevNode != null)
                    {
                        var prevPos = GetNodePosition(prevNode);
                        AddLine(prevPos, pos, LockedColor);
                    }
                }
            }
        }

        private MetaPerkNode FindNode(int tier, MetaPerkLane lane)
        {
            foreach (var n in MetaPerkRegistry.GetAll())
            {
                if (n.Tier == tier && n.Lane == lane)
                    return n;
            }
            return null;
        }

        private void AddLine(Vector2 from, Vector2 to, Color color)
        {
            // Simple vertical/diagonal line using a ColorRect
            float dx = to.X - from.X;
            float dy = to.Y - from.Y;
            float length = new Vector2(dx, dy).Length();
            float angle = Mathf.Atan2(dy, dx);

            var line = new ColorRect();
            line.Color = new Color(color.R, color.G, color.B, 0.3f);
            line.Size = new Vector2(length, 2);
            line.Position = from;
            line.Rotation = angle;
            line.PivotOffset = Vector2.Zero;
            _treeContainer.AddChild(line);
        }

        private void BuildNodeControl(MetaPerkNode node)
        {
            float size = node.IsNotable ? NotableSize : NodeSize;
            var pos = GetNodePosition(node);
            var laneColor = GetLaneColor(node.Lane);

            bool isAllocated = _allocatedSet.Contains(node.Id);
            bool canAllocate = !isAllocated && _saveData.AvailablePoints > 0
                && MetaPerkRegistry.CanAllocate(node.Id, _allocatedSet);

            var btn = new Button();
            btn.CustomMinimumSize = new Vector2(size, size);
            btn.Size = new Vector2(size, size);
            btn.Position = pos - new Vector2(size / 2f, size / 2f);
            btn.ClipText = true;
            btn.TooltipText = $"{node.Name}\n{node.Description}";

            // Style
            var style = new StyleBoxFlat();

            if (isAllocated)
            {
                style.BgColor = new Color(laneColor.R * 0.35f, laneColor.G * 0.35f, laneColor.B * 0.35f, 1f);
                style.BorderColor = new Color(
                    Mathf.Min(1f, laneColor.R + 0.3f),
                    Mathf.Min(1f, laneColor.G + 0.3f),
                    Mathf.Min(1f, laneColor.B + 0.3f));
                btn.AddThemeColorOverride("font_color", Colors.White);
            }
            else if (canAllocate)
            {
                style.BgColor = new Color(laneColor.R * 0.08f, laneColor.G * 0.08f, laneColor.B * 0.08f, 1f);
                style.BorderColor = new Color(laneColor.R * 0.7f, laneColor.G * 0.7f, laneColor.B * 0.7f, 1f);
                btn.AddThemeColorOverride("font_color", new Color(laneColor.R * 0.7f, laneColor.G * 0.7f, laneColor.B * 0.7f));
            }
            else
            {
                style.BgColor = new Color(0.05f, 0.05f, 0.05f, 1f);
                style.BorderColor = LockedColor;
                btn.AddThemeColorOverride("font_color", LockedColor);
            }

            style.SetBorderWidthAll(node.IsNotable ? 3 : 2);
            style.SetCornerRadiusAll(node.IsNotable ? 2 : (int)(size / 2f));
            style.ContentMarginLeft = 2;
            style.ContentMarginRight = 2;
            style.ContentMarginTop = 2;
            style.ContentMarginBottom = 2;
            btn.AddThemeStyleboxOverride("normal", style);

            // Hover
            if (canAllocate)
            {
                var hoverStyle = (StyleBoxFlat)style.Duplicate();
                hoverStyle.BgColor = new Color(laneColor.R * 0.15f, laneColor.G * 0.15f, laneColor.B * 0.15f, 1f);
                hoverStyle.BorderColor = laneColor;
                btn.AddThemeStyleboxOverride("hover", hoverStyle);

                var pressStyle = (StyleBoxFlat)style.Duplicate();
                pressStyle.BgColor = new Color(laneColor.R * 0.3f, laneColor.G * 0.3f, laneColor.B * 0.3f, 1f);
                btn.AddThemeStyleboxOverride("pressed", pressStyle);
            }
            else
            {
                btn.AddThemeStyleboxOverride("hover", style);
                btn.AddThemeStyleboxOverride("pressed", style);
            }

            // Text — abbreviated name
            string shortName = GetShortName(node);
            btn.Text = shortName;
            btn.AddThemeFontSizeOverride("font_size", node.IsNotable ? 10 : 9);

            int capturedId = node.Id;
            btn.Pressed += () => OnNodeClicked(capturedId);

            _treeContainer.AddChild(btn);
            _nodeControls[node.Id] = btn;
        }

        private static string GetShortName(MetaPerkNode node)
        {
            // Use initials if name is long
            if (node.Tier == 0) return "ROOT";
            var parts = node.Name.Split(' ');
            if (parts.Length >= 2)
                return parts[0][..System.Math.Min(3, parts[0].Length)] + "\n" + parts[1][..System.Math.Min(4, parts[1].Length)];
            return node.Name[..System.Math.Min(6, node.Name.Length)];
        }

        private static Color GetLaneColor(MetaPerkLane lane)
        {
            return lane switch {
                MetaPerkLane.Network => NetworkColor,
                MetaPerkLane.Player => PlayerColor,
                MetaPerkLane.Harvester => HarvesterColor,
                _ => NetworkColor
            };
        }

        private void OnNodeClicked(int nodeId)
        {
            if (_saveData.AvailablePoints <= 0) return;
            if (_allocatedSet.Contains(nodeId)) return;
            if (!MetaPerkRegistry.CanAllocate(nodeId, _allocatedSet)) return;

            // Allocate
            _allocatedSet.Add(nodeId);
            _saveData.AllocatedIds.Add(nodeId);
            _saveData.AvailablePoints--;

            GD.Print($"[MetaPerk] Allocated: {MetaPerkRegistry.GetNode(nodeId)?.Name} (points left: {_saveData.AvailablePoints})");

            // Save immediately
            MetaPerkSave.Save(_saveData);
            if (GameManager.Instance != null)
                GameManager.Instance.MetaSave = _saveData;

            // Rebuild the tree UI to reflect new state
            RefreshTree();

            // Flash feedback on the just-allocated node
            PlayAllocateFlash(nodeId);
        }

        private void PlayAllocateFlash(int nodeId)
        {
            if (!_nodeControls.TryGetValue(nodeId, out var ctrl)) return;
            var node = MetaPerkRegistry.GetNode(nodeId);
            if (node == null) return;

            var laneColor = GetLaneColor(node.Lane);

            // Bright overlay that fades out
            var flash = new ColorRect();
            float size = node.IsNotable ? NotableSize : NodeSize;
            flash.Size = new Vector2(size, size);
            flash.Position = ctrl.Position;
            flash.Color = new Color(laneColor.R, laneColor.G, laneColor.B, 0.7f);
            flash.MouseFilter = Control.MouseFilterEnum.Ignore;
            _treeContainer.AddChild(flash);

            // Expanding ring that fades out
            var ring = new ColorRect();
            float ringSize = size + 20f;
            ring.Size = new Vector2(ringSize, ringSize);
            ring.Position = ctrl.Position - new Vector2(10f, 10f);
            ring.Color = new Color(laneColor.R, laneColor.G, laneColor.B, 0.4f);
            ring.MouseFilter = Control.MouseFilterEnum.Ignore;
            _treeContainer.AddChild(ring);

            // Animate flash fade-out
            var tween = CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(flash, "color:a", 0f, 0.4f)
                .SetEase(Tween.EaseType.Out);
            tween.TweenProperty(ring, "color:a", 0f, 0.5f)
                .SetEase(Tween.EaseType.Out);
            tween.TweenProperty(ring, "position",
                ring.Position - new Vector2(8f, 8f), 0.5f)
                .SetEase(Tween.EaseType.Out);
            tween.TweenProperty(ring, "size",
                ring.Size + new Vector2(16f, 16f), 0.5f)
                .SetEase(Tween.EaseType.Out);
            tween.SetParallel(false);
            tween.TweenCallback(Callable.From(() => {
                flash.QueueFree();
                ring.QueueFree();
            }));
        }

        private void RefreshTree()
        {
            // Remove old node controls
            foreach (var kv in _nodeControls)
                kv.Value.QueueFree();
            _nodeControls.Clear();

            // Rebuild nodes
            foreach (var node in MetaPerkRegistry.GetAll())
                BuildNodeControl(node);

            UpdatePointsLabel();
        }

        private void UpdatePointsLabel()
        {
            int pts = _saveData?.AvailablePoints ?? 0;
            _pointsLabel.Text = $"AVAILABLE POINTS: {pts}";
            _pointsLabel.AddThemeColorOverride("font_color",
                pts > 0 ? new Color(0.3f, 0.9f, 0.3f) : new Color(0.5f, 0.5f, 0.5f));
        }

        private void OnContinue()
        {
            // Save and proceed to per-run perk select
            MetaPerkSave.Save(_saveData);
            if (GameManager.Instance != null)
                GameManager.Instance.MetaSave = _saveData;

            GetTree().ChangeSceneToFile(Constants.SCENE_VINE_PERK);
        }
    }
}
