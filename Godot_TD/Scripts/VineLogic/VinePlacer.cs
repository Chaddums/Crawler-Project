using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Handles vine node placement input. Left-click to place, right-click to cancel.
    /// Ghost preview + connection preview lines to adjacent nodes.
    /// </summary>
    public partial class VinePlacer : Node
    {
        public bool IsPlacing { get; private set; }
        public VineNodeType? SelectedType { get; private set; }

        private VineGrid _grid;
        private VinePathfinder _pathfinder;
        private MeshInstance3D _ghost;
        private Vector2I _ghostCell;
        private bool _ghostValid;

        // Connection preview — lines showing what this node will connect to
        private readonly List<MeshInstance3D> _previewLines = new();

        // Info label showing what connections will form
        private Label3D _connectionInfoLabel;

        private static readonly Vector2I[] Directions = {
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1)
        };

        public override void _Ready()
        {
            _grid = ServiceLocator.Get<VineGrid>();
            _pathfinder = ServiceLocator.Get<VinePathfinder>();
            ServiceLocator.Register(this);
        }

        public void StartPlacing(VineNodeType type)
        {
            SelectedType = type;
            IsPlacing = true;
            CreateGhost(type);
        }

        public void CancelPlacing()
        {
            IsPlacing = false;
            SelectedType = null;
            DestroyGhost();
            ClearPreviewLines();
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (!IsPlacing || SelectedType == null) return;

            var phase = GameManager.Instance?.CurrentPhase ?? GamePhase.Build;
            if (phase != GamePhase.Build) return;

            if (@event is InputEventMouseMotion)
            {
                UpdateGhostPosition();
            }
            else if (@event is InputEventMouseButton mb && mb.Pressed)
            {
                if (mb.ButtonIndex == MouseButton.Left)
                    TryPlace();
                else if (mb.ButtonIndex == MouseButton.Right)
                    CancelPlacing();
            }
        }

        private void TryPlace()
        {
            if (!_ghostValid || !IsPlacing || SelectedType == null) return;

            var data = VineNodeRegistry.Get(SelectedType.Value);
            if (data == null) return;

            var gm = GameManager.Instance;
            if (gm != null && gm.CurrentScrap < data.GoldCost) return;
            if (_pathfinder.WouldBlockAllPaths(_ghostCell)) return;

            var node = new VineNode();
            node.Initialize(data);

            if (_grid.PlaceNode(node, _ghostCell))
            {
                gm?.SpendScrap(data.GoldCost);
                if (!Input.IsKeyPressed(Key.Shift))
                    CancelPlacing();
            }
            else
            {
                node.QueueFree();
            }
        }

        private void UpdateGhostPosition()
        {
            if (_ghost == null) return;

            var camera = GetViewport().GetCamera3D();
            if (camera == null) return;

            var mousePos = GetViewport().GetMousePosition();
            var from = camera.ProjectRayOrigin(mousePos);
            var dir = camera.ProjectRayNormal(mousePos);

            if (Mathf.Abs(dir.Y) < 0.001f) return;
            float t = -from.Y / dir.Y;
            if (t < 0) return;
            var worldPos = from + dir * t;

            _ghostCell = _grid.WorldToGrid(worldPos);
            _ghostValid = _grid.CanPlace(_ghostCell);

            // All nodes block — always validate that a path remains
            if (_ghostValid)
                _ghostValid = !_pathfinder.WouldBlockAllPaths(_ghostCell);

            _ghost.GlobalPosition = _grid.GridToWorld(_ghostCell) + new Vector3(0, 0.5f, 0);

            if (_ghost.MaterialOverride is StandardMaterial3D mat)
            {
                mat.AlbedoColor = _ghostValid
                    ? new Color(0.2f, 0.8f, 0.2f, 0.5f)
                    : new Color(0.8f, 0.2f, 0.2f, 0.5f);
            }

            // Update connection preview lines
            UpdatePreviewLines();
        }

        private void UpdatePreviewLines()
        {
            ClearPreviewLines();
            if (!_ghostValid) return;

            var ghostWorldPos = _grid.GridToWorld(_ghostCell) + new Vector3(0, 0.5f, 0);
            var neighbors = new List<VineNode>();

            foreach (var dir in Directions)
            {
                var neighborPos = _ghostCell + dir;
                var neighborNode = _grid.GetNode(neighborPos);
                if (neighborNode != null && neighborNode.HasFreeSlot())
                {
                    neighbors.Add(neighborNode);

                    // Draw preview line to this neighbor
                    var line = new MeshInstance3D();
                    var im = new ImmediateMesh();
                    line.Mesh = im;

                    var lineMat = new StandardMaterial3D();
                    lineMat.AlbedoColor = new Color(0.2f, 0.9f, 0.3f, 0.7f);
                    lineMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                    lineMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                    line.MaterialOverride = lineMat;

                    var neighborWorldPos = _grid.GridToWorld(neighborPos) + new Vector3(0, 0.5f, 0);
                    im.SurfaceBegin(Mesh.PrimitiveType.Lines);
                    im.SurfaceAddVertex(ghostWorldPos);
                    im.SurfaceAddVertex(neighborWorldPos);
                    im.SurfaceEnd();

                    GetTree().Root.AddChild(line);
                    _previewLines.Add(line);

                    // Arrow indicator at midpoint showing signal direction
                    var midpoint = ghostWorldPos.Lerp(neighborWorldPos, 0.5f);
                    var arrow = new MeshInstance3D();
                    var arrowMesh = new SphereMesh();
                    arrowMesh.Radius = 0.12f;
                    arrowMesh.Height = 0.24f;
                    arrow.Mesh = arrowMesh;
                    var arrowMat = new StandardMaterial3D();
                    arrowMat.AlbedoColor = new Color(0.2f, 0.9f, 0.3f, 0.9f);
                    arrowMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                    arrowMat.EmissionEnabled = true;
                    arrowMat.Emission = new Color(0.2f, 0.9f, 0.3f);
                    arrow.MaterialOverride = arrowMat;
                    GetTree().Root.AddChild(arrow);
                    arrow.GlobalPosition = midpoint;
                    _previewLines.Add(arrow);
                }
            }

            // Update connection info label
            UpdateConnectionInfo(neighbors);
        }

        private void UpdateConnectionInfo(List<VineNode> neighbors)
        {
            if (_connectionInfoLabel == null)
            {
                _connectionInfoLabel = new Label3D();
                _connectionInfoLabel.FontSize = 22;
                _connectionInfoLabel.OutlineSize = 5;
                _connectionInfoLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
                _connectionInfoLabel.Modulate = new Color(0.9f, 0.9f, 0.6f);
                GetTree().Root.AddChild(_connectionInfoLabel);
            }

            if (neighbors.Count == 0 || !_ghostValid)
            {
                _connectionInfoLabel.Visible = false;
                return;
            }

            _connectionInfoLabel.Visible = true;
            if (_connectionInfoLabel.IsInsideTree())
                _connectionInfoLabel.GlobalPosition = _grid.GridToWorld(_ghostCell) + new Vector3(0, 1.8f, 0);

            // Build description of what will connect
            var selectedData = SelectedType.HasValue ? VineNodeRegistry.Get(SelectedType.Value) : null;
            if (selectedData == null) return;

            string selectedRole = CategoryTag(selectedData.Category);
            var lines = new System.Text.StringBuilder();
            lines.AppendLine($"Will connect to {neighbors.Count} node(s):");

            foreach (var n in neighbors)
            {
                string neighborRole = CategoryTag(n.Data.Category);
                string flowDesc = DescribeFlow(selectedData, n.Data);
                lines.AppendLine($"  {neighborRole} {n.Data.Name} {flowDesc}");
            }

            _connectionInfoLabel.Text = lines.ToString().TrimEnd();
        }

        private static string CategoryTag(VineNodeCategory cat) => cat switch {
            VineNodeCategory.Sensor => "[SENSOR]",
            VineNodeCategory.Effect => "[EFFECT]",
            _ => "[ROUTE]"
        };

        /// <summary>
        /// Describe the expected signal flow between two node types.
        /// </summary>
        private static string DescribeFlow(VineNodeData placing, VineNodeData neighbor)
        {
            // Sensor → anything: sensor fires signals outward
            if (placing.Category == VineNodeCategory.Sensor)
                return "<< sends signal to";
            if (neighbor.Category == VineNodeCategory.Sensor)
                return ">> receives signal from";

            // Effect nodes receive signals
            if (placing.Category == VineNodeCategory.Effect && neighbor.Category != VineNodeCategory.Effect)
                return ">> receives signal from";
            if (neighbor.Category == VineNodeCategory.Effect && placing.Category != VineNodeCategory.Effect)
                return "<< sends signal to";

            // Route ↔ Route: bidirectional
            return "<> passes signal through";
        }

        private void ClearPreviewLines()
        {
            foreach (var line in _previewLines)
                line?.QueueFree();
            _previewLines.Clear();

            if (_connectionInfoLabel != null)
                _connectionInfoLabel.Visible = false;
        }

        private void CreateGhost(VineNodeType type)
        {
            DestroyGhost();

            var data = VineNodeRegistry.Get(type);
            if (data == null) return;

            _ghost = new MeshInstance3D();
            var box = new BoxMesh();
            float size = data.Category switch {
                VineNodeCategory.Sensor => 0.6f,
                VineNodeCategory.Effect => 0.8f,
                _ => 0.5f
            };
            box.Size = new Vector3(size, size, size);
            _ghost.Mesh = box;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.2f, 0.8f, 0.2f, 0.5f);
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            _ghost.MaterialOverride = mat;

            GetTree().Root.AddChild(_ghost);
        }

        private void DestroyGhost()
        {
            _ghost?.QueueFree();
            _ghost = null;
        }

        public override void _ExitTree()
        {
            DestroyGhost();
            ClearPreviewLines();
            _connectionInfoLabel?.QueueFree();
            ServiceLocator.Unregister<VinePlacer>();
        }
    }
}
