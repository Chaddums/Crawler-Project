using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Renders the ~120 node passive tree via _Draw() override.
    /// Supports pan (mouse drag) and zoom (scroll wheel).
    /// Click to allocate nodes.
    /// </summary>
    public partial class PassiveTreeCanvas : Control
    {
        private Vector2 _panOffset = Vector2.Zero;
        private float _zoom = 0.7f;
        private const float MIN_ZOOM = 0.4f;
        private const float MAX_ZOOM = 2.5f;
        private const float SCALE = 60f;

        private bool _dragging;
        private Vector2 _dragStart;
        private string _hoveredNode;

        private PassiveNodeTooltipUI _tooltip;
        private GraftSocketPickerUI _graftPicker;

        private static readonly Color AllocatedFill = new(0.9f, 0.8f, 0.3f);
        private static readonly Color PinnacleFill = new(1f, 0.6f, 0.2f);
        private static readonly Color KeystoneFill = new(0.8f, 0.5f, 0.9f);
        private static readonly Color CoreSocketFill = new(0.4f, 0.8f, 0.9f);
        private static readonly Color CoreSocketFilledFill = new(0.3f, 1f, 0.6f);
        private static readonly Color CapstoneFill = new(0.9f, 0.25f, 0.3f);
        private static readonly Color AvailableOutline = new(0.8f, 0.7f, 0.2f);
        private static readonly Color UnavailableColor = new(0.3f, 0.3f, 0.35f);
        private static readonly Color ConnectionGold = new(0.7f, 0.6f, 0.2f);
        private static readonly Color ConnectionDim = new(0.3f, 0.3f, 0.3f);
        private static readonly Color ClassStartColor = new(0.4f, 0.7f, 1f);

        public override void _Ready()
        {
            // Set up tooltip
            _tooltip = new PassiveNodeTooltipUI();
            AddChild(_tooltip);

            // Graft socket picker
            _graftPicker = new GraftSocketPickerUI();
            AddChild(_graftPicker);

            // Default center — will be overridden by CenterOnClass when tree opens
            _panOffset = Size / 2f;

            MouseFilter = MouseFilterEnum.Stop;
        }

        /// <summary>
        /// Center the view on the player's class area (trunk/split region).
        /// </summary>
        public void CenterOnClass(BotFrameType cls)
        {
            var tree = PassiveTreeBuilder.Tree;
            if (tree == null) return;

            // Find the class start node and use its split area as focal point
            var startId = $"start_{cls}";
            var startNode = tree.GetNode(startId);
            if (startNode == null) return;

            // Focus on the split point area (55% from center toward class start)
            // This shows the trunk and branching area nicely
            var focusPos = startNode.TreePosition * 0.55f;
            _panOffset = Size / 2f - focusPos * SCALE * _zoom;
            QueueRedraw();
        }

        public override void _Draw()
        {
            var tree = PassiveTreeBuilder.Tree;
            if (tree == null) return;

            PassiveTree playerTree = null;
            int availablePoints = 0;
            HashSet<string> allocatable = null;

            if (ServiceLocator.TryGet<PlayerController>(out var player))
            {
                playerTree = player.ClassController.PassiveTree;
                availablePoints = player.Stats.AvailableSkillPoints;
                allocatable = playerTree != null
                    ? new HashSet<string>(playerTree.GetAllocatableNodes(availablePoints))
                    : new();
            }

            // Draw connections first
            foreach (var (nodeId, nodeData) in tree.Nodes)
            {
                var fromPos = TreeToScreen(nodeData.TreePosition);

                foreach (var connId in nodeData.Connections)
                {
                    var connNode = tree.GetNode(connId);
                    if (connNode == null) continue;

                    // Only draw each connection once
                    if (string.Compare(nodeId, connId, System.StringComparison.Ordinal) > 0) continue;

                    var toPos = TreeToScreen(connNode.TreePosition);

                    bool bothAllocated = playerTree != null &&
                        playerTree.AllocatedNodes.Contains(nodeId) &&
                        playerTree.AllocatedNodes.Contains(connId);

                    DrawLine(fromPos, toPos, bothAllocated ? ConnectionGold : ConnectionDim, 2f * _zoom);
                }
            }

            // Draw nodes
            foreach (var (nodeId, nodeData) in tree.Nodes)
            {
                var screenPos = TreeToScreen(nodeData.TreePosition);
                float radius = GetNodeRadius(nodeData.NodeType) * _zoom;

                bool isAllocated = playerTree?.AllocatedNodes.Contains(nodeId) ?? false;
                bool isAvailable = allocatable?.Contains(nodeId) ?? false;

                Color fillColor;
                Color outlineColor;

                if (isAllocated)
                {
                    fillColor = nodeData.NodeType switch
                    {
                        SkillNodeType.ClassStart => ClassStartColor,
                        SkillNodeType.Pinnacle => PinnacleFill,
                        SkillNodeType.Keystone => KeystoneFill,
                        SkillNodeType.Capstone => CapstoneFill,
                        SkillNodeType.CoreSocket => nodeData.SocketedCore != null ? CoreSocketFilledFill : CoreSocketFill,
                        _ => AllocatedFill
                    };
                    outlineColor = fillColor;
                }
                else if (isAvailable)
                {
                    fillColor = new Color(0.15f, 0.14f, 0.1f, 0.8f);
                    outlineColor = AvailableOutline;
                }
                else
                {
                    fillColor = new Color(0.1f, 0.1f, 0.12f, 0.7f);
                    outlineColor = UnavailableColor;
                }

                // Draw filled circle
                DrawCircle(screenPos, radius, fillColor);
                // Draw outline
                DrawArc(screenPos, radius, 0, Mathf.Tau, 32, outlineColor, 2f * _zoom);

                // Draw node name for notables and keystones
                if (nodeData.NodeType != SkillNodeType.Basic && _zoom > 0.7f)
                {
                    var font = ThemeDB.FallbackFont;
                    int fontSize = (int)(10 * _zoom);
                    if (font != null)
                    {
                        string label = nodeData.NodeType == SkillNodeType.ClassStart
                            ? nodeData.ClassStartFor.ToString()[..3]
                            : nodeData.NodeType == SkillNodeType.CoreSocket ? (nodeData.SocketedCore != null ? "G" : "C")
                            : nodeData.NodeType == SkillNodeType.Pinnacle ? "P"
                            : nodeData.NodeType == SkillNodeType.Capstone ? "★"
                            : nodeData.NodeType == SkillNodeType.Keystone ? "K" : "";

                        if (!string.IsNullOrEmpty(label))
                        {
                            var textSize = font.GetStringSize(label, HorizontalAlignment.Center, -1, fontSize);
                            DrawString(font, screenPos - textSize / 2f + new Vector2(0, textSize.Y * 0.4f),
                                label, HorizontalAlignment.Center, -1, fontSize, Colors.White);
                        }
                    }
                }

                // Highlight hovered node
                if (nodeId == _hoveredNode)
                    DrawArc(screenPos, radius + 3 * _zoom, 0, Mathf.Tau, 32, Colors.White, 1.5f * _zoom);
            }
        }

        public override void _GuiInput(InputEvent ev)
        {
            if (ev is InputEventMouseButton mb)
            {
                if (mb.ButtonIndex == MouseButton.Left)
                {
                    if (mb.Pressed)
                    {
                        _dragging = true;
                        _dragStart = mb.Position;
                    }
                    else
                    {
                        if (_dragging && mb.Position.DistanceTo(_dragStart) < 5)
                        {
                            // Click — try to allocate
                            HandleClick(mb.Position);
                        }
                        _dragging = false;
                    }
                }
                else if (mb.ButtonIndex == MouseButton.Right && mb.Pressed)
                {
                    HandleRightClick(mb.Position);
                }
                else if (mb.ButtonIndex == MouseButton.WheelUp)
                {
                    Zoom(mb.Position, 1.1f);
                }
                else if (mb.ButtonIndex == MouseButton.WheelDown)
                {
                    Zoom(mb.Position, 0.9f);
                }
            }
            else if (ev is InputEventMouseMotion mm)
            {
                if (_dragging)
                {
                    _panOffset += mm.Relative;
                    QueueRedraw();
                }
                else
                {
                    UpdateHover(mm.Position);
                }
            }
        }

        private void Zoom(Vector2 center, float factor)
        {
            float oldZoom = _zoom;
            _zoom = Mathf.Clamp(_zoom * factor, MIN_ZOOM, MAX_ZOOM);

            // Zoom toward mouse position
            float ratio = _zoom / oldZoom;
            _panOffset = center + ((_panOffset - center) * ratio);
            QueueRedraw();
        }

        private void HandleClick(Vector2 screenPos)
        {
            var tree = PassiveTreeBuilder.Tree;
            if (tree == null) return;

            if (!ServiceLocator.TryGet<PlayerController>(out var player)) return;

            string closestId = FindNodeAtScreen(screenPos);
            if (closestId != null)
            {
                if (player.ClassController.AllocatePassiveNode(closestId))
                    QueueRedraw();
            }
        }

        private void HandleRightClick(Vector2 screenPos)
        {
            var tree = PassiveTreeBuilder.Tree;
            if (tree == null) return;

            if (!ServiceLocator.TryGet<PlayerController>(out var player)) return;
            var passiveTree = player.ClassController?.PassiveTree;
            if (passiveTree == null) return;

            string closestId = FindNodeAtScreen(screenPos);
            if (closestId == null) return;

            var nodeData = tree.GetNode(closestId);
            if (nodeData == null) return;

            // Only open picker for allocated CoreSocket nodes
            if (nodeData.NodeType != SkillNodeType.CoreSocket) return;
            if (!passiveTree.AllocatedNodes.Contains(closestId)) return;

            _graftPicker.Show(closestId, screenPos, () => QueueRedraw());
        }

        private void UpdateHover(Vector2 screenPos)
        {
            var oldHovered = _hoveredNode;
            _hoveredNode = FindNodeAtScreen(screenPos);

            if (_hoveredNode != oldHovered)
            {
                QueueRedraw();

                if (_hoveredNode != null)
                {
                    var tree = PassiveTreeBuilder.Tree;
                    var nodeData = tree?.GetNode(_hoveredNode);

                    PassiveTree playerTree = null;
                    if (ServiceLocator.TryGet<PlayerController>(out var player))
                        playerTree = player.ClassController.PassiveTree;

                    if (nodeData != null)
                        _tooltip.ShowNode(nodeData, playerTree, screenPos);
                }
                else
                {
                    _tooltip.HideTooltip();
                }
            }
        }

        private string FindNodeAtScreen(Vector2 screenPos)
        {
            var tree = PassiveTreeBuilder.Tree;
            if (tree == null) return null;

            float closestDist = float.MaxValue;
            string closestId = null;

            foreach (var (nodeId, nodeData) in tree.Nodes)
            {
                var nodeScreen = TreeToScreen(nodeData.TreePosition);
                float radius = GetNodeRadius(nodeData.NodeType) * _zoom;
                float dist = screenPos.DistanceTo(nodeScreen);

                if (dist < radius + 4 * _zoom && dist < closestDist)
                {
                    closestDist = dist;
                    closestId = nodeId;
                }
            }

            return closestId;
        }

        private Vector2 TreeToScreen(Vector2 treePos)
        {
            return treePos * SCALE * _zoom + _panOffset;
        }

        private static float GetNodeRadius(SkillNodeType type) => type switch
        {
            SkillNodeType.Basic => 12f,
            SkillNodeType.Notable => 18f,
            SkillNodeType.Keystone => 24f,
            SkillNodeType.Capstone => 20f,
            SkillNodeType.Pinnacle => 28f,
            SkillNodeType.ClassStart => 20f,
            SkillNodeType.CoreSocket => 16f,
            _ => 12f,
        };

        /// <summary>
        /// Subscribe to tree events for auto-redraw.
        /// </summary>
        public override void _EnterTree()
        {
            GameEvents.OnPassiveNodeAllocated += OnTreeChanged;
            GameEvents.OnPassiveNodeDeallocated += OnTreeChanged;
            GameEvents.OnPassiveTreeReset += OnTreeReset;
        }

        public override void _ExitTree()
        {
            GameEvents.OnPassiveNodeAllocated -= OnTreeChanged;
            GameEvents.OnPassiveNodeDeallocated -= OnTreeChanged;
            GameEvents.OnPassiveTreeReset -= OnTreeReset;
        }

        private void OnTreeChanged(string _) => QueueRedraw();
        private void OnTreeReset() => QueueRedraw();
    }
}
