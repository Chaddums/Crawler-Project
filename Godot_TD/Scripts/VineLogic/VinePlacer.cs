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
        public bool IsPlacingMiningBuilding { get; private set; }

        private VineGrid _grid;
        private VinePathfinder _pathfinder;
        private MeshInstance3D _ghost;
        private Vector2I _ghostCell;
        private bool _ghostValid;
        private PlacementMode _placementMode;

        // Free placement (Obelisk)
        private Vector3 _freeGhostPos;
        private float _freeSnapIncrement = 0.25f;

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

            var spireData = SpireData.Get(GameManager.Instance?.SelectedRole ?? "Obelisk");
            _placementMode = spireData?.PlacementMode ?? PlacementMode.WireNetwork;
            if (spireData != null)
                _freeSnapIncrement = spireData.IncrementSize;

            ServiceLocator.Register(this);
        }

        public override void _Process(double delta)
        {
            if (IsPlacing && _ghost != null)
            {
                if (_placementMode == PlacementMode.FreeRadius)
                    UpdateGhostPositionFree();
                else if (_placementMode == PlacementMode.SocketGrid)
                    UpdateGhostPositionSocket();
                else
                    UpdateGhostPosition();
            }
        }

        public void StartPlacing(VineNodeType type)
        {
            if (_grid.Harvester == null)
            {
                GD.Print("[VinePlacer] Must place Mining Building first");
                return;
            }
            IsPlacingMiningBuilding = false;
            SelectedType = type;
            IsPlacing = true;
            CreateGhost(type);
        }

        public void StartPlacingMiningBuilding()
        {
            if (_grid.Harvester != null)
            {
                GD.Print("[VinePlacer] Mining Building already placed");
                return;
            }
            IsPlacingMiningBuilding = true;
            SelectedType = null;
            IsPlacing = true;
            CreateMiningGhost();
        }

        public void CancelPlacing()
        {
            IsPlacing = false;
            IsPlacingMiningBuilding = false;
            SelectedType = null;
            DestroyGhost();
            ClearPreviewLines();
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            var phase = GameManager.Instance?.CurrentPhase ?? GamePhase.Build;
            if (phase == GamePhase.Victory || phase == GamePhase.Defeat || phase == GamePhase.Paused) return;

            // Right-click on Mining Building to toggle Resources/Materials mode (when not placing)
            if (!IsPlacing && @event is InputEventMouseButton rmb && rmb.Pressed
                && rmb.ButtonIndex == MouseButton.Right)
            {
                TryToggleMiningBuilding(rmb);
                return;
            }

            if (!IsPlacing) return;
            if (!IsPlacingMiningBuilding && SelectedType == null) return;

            if (@event is InputEventMouseButton mb && mb.Pressed)
            {
                if (mb.ButtonIndex == MouseButton.Left)
                {
                    if (IsPlacingMiningBuilding)
                        TryPlaceMiningBuilding();
                    else if (_placementMode == PlacementMode.FreeRadius)
                        TryPlaceFree();
                    else if (_placementMode == PlacementMode.SocketGrid)
                        TryPlaceSocket();
                    else
                        TryPlace();
                }
                else if (mb.ButtonIndex == MouseButton.Right)
                    CancelPlacing();
            }
        }

        private void TryToggleMiningBuilding(InputEventMouseButton @event)
        {
            if (_grid.Harvester == null) return;

            var camera = GetViewport().GetCamera3D();
            if (camera == null) return;

            var mousePos = @event.Position;
            var from = camera.ProjectRayOrigin(mousePos);
            var dir = camera.ProjectRayNormal(mousePos);

            if (Mathf.Abs(dir.Y) < 0.001f) return;
            float t = -from.Y / dir.Y;
            if (t < 0) return;
            var worldPos = from + dir * t;

            // Check if click is near the Mining Building (within 3 units)
            float dist = worldPos.DistanceTo(_grid.Harvester.GlobalPosition);
            if (dist < 3f)
            {
                _grid.Harvester.ToggleMode();
                GetViewport().SetInputAsHandled();
            }
        }

        private void TryPlace()
        {
            if (!_ghostValid || !IsPlacing || SelectedType == null) return;

            var data = VineNodeRegistry.Get(SelectedType.Value);
            if (data == null) return;

            var gm = GameManager.Instance;
            if (gm != null && gm.CurrentResources < data.ResourceCost) return;
            if (_pathfinder.WouldBlockAllPaths(_ghostCell)) return;

            var node = new VineNode();
            node.Initialize(data);

            if (_grid.PlaceNode(node, _ghostCell))
            {
                gm?.SpendResources(data.ResourceCost);
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
            worldPos.Y = _grid.GetWorldHeight(worldPos.X, worldPos.Z);

            _ghostCell = _grid.WorldToGrid(worldPos);
            _ghostValid = _grid.CanPlace(_ghostCell);

            // All nodes block — always validate that a path remains
            if (_ghostValid)
                _ghostValid = !_pathfinder.WouldBlockAllPaths(_ghostCell);

            // WireNetwork mode: must be adjacent to an existing connected node
            if (_ghostValid && _placementMode == PlacementMode.WireNetwork)
            {
                if (ServiceLocator.TryGet<BruteforgeWireGrid>(out var wireGrid))
                    _ghostValid = wireGrid.WouldBeConnected(_ghostCell);
            }

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

        // ── Free Placement (Obelisk) ──

        private void UpdateGhostPositionFree()
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

            // Snap to fine increment
            worldPos.X = Mathf.Round(worldPos.X / _freeSnapIncrement) * _freeSnapIncrement;
            worldPos.Z = Mathf.Round(worldPos.Z / _freeSnapIncrement) * _freeSnapIncrement;
            worldPos.Y = _grid.GetWorldHeight(worldPos.X, worldPos.Z);

            _freeGhostPos = worldPos;

            // Validate: must be within a power radius and have clearance
            _ghostValid = false;
            if (ServiceLocator.TryGet<ObeliskPowerSystem>(out var power))
            {
                _ghostValid = power.IsPositionPowered(worldPos)
                    && power.HasClearance(worldPos);
            }

            // Also check that the nearest grid cell wouldn't block all paths
            if (_ghostValid)
            {
                _ghostCell = _grid.WorldToGrid(worldPos);
                if (_pathfinder.WouldBlockAllPaths(_ghostCell))
                    _ghostValid = false;
            }

            _ghost.GlobalPosition = worldPos + new Vector3(0, 0.5f, 0);

            if (_ghost.MaterialOverride is StandardMaterial3D mat)
            {
                mat.AlbedoColor = _ghostValid
                    ? new Color(0.2f, 0.8f, 0.2f, 0.5f)
                    : new Color(0.8f, 0.2f, 0.2f, 0.5f);
            }
        }

        private void TryPlaceFree()
        {
            if (!_ghostValid || !IsPlacing || SelectedType == null) return;

            var data = VineNodeRegistry.Get(SelectedType.Value);
            if (data == null) return;

            var gm = GameManager.Instance;
            if (gm != null && gm.CurrentResources < data.ResourceCost) return;

            if (!ServiceLocator.TryGet<ObeliskPowerSystem>(out var power)) return;

            if (SelectedType.Value == VineNodeType.Pylon)
            {
                // Place a Pylon
                var pylon = new ObeliskPylon();
                _grid.AddChild(pylon);
                pylon.GlobalPosition = _freeGhostPos;
                pylon.Initialize(power.PylonRadius > 0 ? power.PylonRadius : 6f);
                power.RegisterPylon(pylon);
                power.RegisterPlacedNode(pylon);
            }
            else
            {
                // Place a regular VineNode at free position
                var node = new VineNode();
                node.Initialize(data);
                _grid.AddChild(node);
                node.GlobalPosition = _freeGhostPos;

                // Mark nearest grid cell as wall for pathfinding
                var cell = _grid.WorldToGrid(_freeGhostPos);
                _grid.SetWall(cell.X, cell.Y);

                power.RegisterPlacedNode(node);
            }

            gm?.SpendResources(data.ResourceCost);

            if (!Input.IsKeyPressed(Key.Shift))
                CancelPlacing();
        }

        // ── Socket Grid Placement (Arcanist) ──

        private void UpdateGhostPositionSocket()
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
            worldPos.Y = _grid.GetWorldHeight(worldPos.X, worldPos.Z);

            _ghostCell = _grid.WorldToGrid(worldPos);

            if (!ServiceLocator.TryGet<ArcanistSocketGrid>(out var socketGrid))
            {
                _ghostValid = false;
                return;
            }

            bool isSocketType = SelectedType == VineNodeType.Socket;

            if (isSocketType)
            {
                _ghostValid = socketGrid.CanBuildSocket(_ghostCell);
                if (_ghostValid)
                    _ghostValid = !_pathfinder.WouldBlockAllPaths(_ghostCell);
            }
            else
            {
                _ghostValid = socketGrid.CanPlaceTower(_ghostCell);
            }

            _ghost.GlobalPosition = _grid.GridToWorld(_ghostCell) + new Vector3(0, 0.5f, 0);

            if (_ghost.MaterialOverride is StandardMaterial3D mat)
            {
                mat.AlbedoColor = _ghostValid
                    ? new Color(0.2f, 0.8f, 0.2f, 0.5f)
                    : new Color(0.8f, 0.2f, 0.2f, 0.5f);
            }
        }

        private void TryPlaceSocket()
        {
            if (!_ghostValid || !IsPlacing || SelectedType == null) return;

            var data = VineNodeRegistry.Get(SelectedType.Value);
            if (data == null) return;

            var gm = GameManager.Instance;
            if (gm != null && gm.CurrentResources < data.ResourceCost) return;

            if (!ServiceLocator.TryGet<ArcanistSocketGrid>(out var socketGrid)) return;

            bool isSocketType = SelectedType.Value == VineNodeType.Socket;

            if (isSocketType)
            {
                if (!socketGrid.BuildSocket(_ghostCell)) return;
            }
            else if (SelectedType.Value == VineNodeType.Prism)
            {
                if (!socketGrid.CanPlaceTower(_ghostCell)) return;
                var node = new VineNode();
                node.Initialize(data);
                _grid.AddChild(node);
                socketGrid.PlaceTower(node, _ghostCell);
                var socket = socketGrid.GetSocket(_ghostCell);
                if (socket != null) socket.IsPrism = true;
            }
            else
            {
                if (!socketGrid.CanPlaceTower(_ghostCell)) return;
                var node = new VineNode();
                node.Initialize(data);
                _grid.AddChild(node);
                socketGrid.PlaceTower(node, _ghostCell);
            }

            gm?.SpendResources(data.ResourceCost);

            if (!Input.IsKeyPressed(Key.Shift))
                CancelPlacing();
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

        // ── Mining Building Placement ──

        private void CreateMiningGhost()
        {
            DestroyGhost();
            _ghost = new MeshInstance3D();
            var cyl = new CylinderMesh { TopRadius = 1.2f, BottomRadius = 1.2f, Height = 0.3f };
            _ghost.Mesh = cyl;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.3f, 0.7f, 1f, 0.5f);
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.EmissionEnabled = true;
            mat.Emission = BitPalette.Accent;
            mat.EmissionEnergyMultiplier = 0.3f;
            _ghost.MaterialOverride = mat;

            GetTree().Root.AddChild(_ghost);
        }

        private void TryPlaceMiningBuilding()
        {
            if (!_ghostValid || _grid.Harvester != null) return;

            // Must be on an empty, non-entry, non-exit cell
            if (_grid.GetCell(_ghostCell.X, _ghostCell.Y) != VineCellType.Empty) return;

            // Place the Mining Building
            var harvester = new VineHarvester();
            _grid.AddChild(harvester);
            harvester.GlobalPosition = _grid.GridToWorld(_ghostCell);
            _grid.Harvester = harvester;

            // Update exit point to Mining Building location — enemies path HERE now
            _grid.SetExit(_ghostCell.X, _ghostCell.Y);

            // Block this cell so enemies path around it
            _grid.SetWall(_ghostCell.X, _ghostCell.Y);

            // Recalculate all enemy paths to the new exit
            if (ServiceLocator.TryGet<VinePathfinder>(out var pf))
                pf.RecalculateAllPaths();

            // Move BIT near the Mining Building
            if (ServiceLocator.TryGet<VinePlayer>(out var player))
                player.GlobalPosition = harvester.GlobalPosition + new Vector3(-4f, 0, 0);

            // Remove the old exit glow marker
            foreach (var glow in _grid.GetTree().GetNodesInGroup("ExitGlow"))
                glow.QueueFree();

            // Reposition conversion dome to Mining Building and force material re-conversion
            foreach (var child in _grid.GetParent().GetChildren())
            {
                if (child is ConversionDome dome)
                {
                    dome.GlobalPosition = harvester.GlobalPosition;
                    dome.ForceConversionUpdate();
                    break;
                }
            }

            GD.Print($"[VinePlacer] Mining Building placed at ({_ghostCell.X}, {_ghostCell.Y}) — exit point updated");
            CancelPlacing();

            // Prompt material type selection — the building is locked to one material type
            // Combat characters: 1 building, 1 material type, double rate
            // Non-attacker: can place 2 buildings (one per material type)
            ShowMagicTypeSelection(harvester);
        }

        private void ShowMagicTypeSelection(VineHarvester harvester)
        {
            // Code-built popup for material type selection
            var overlay = new CanvasLayer();
            overlay.Layer = 50;

            var panel = new PanelContainer();
            panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
            panel.CustomMinimumSize = new Vector2(300, 200);

            var bg = new StyleBoxFlat();
            bg.BgColor = new Color(0.05f, 0.05f, 0.1f, 0.95f);
            bg.BorderColor = BitPalette.Accent;
            bg.SetBorderWidthAll(2);
            bg.SetCornerRadiusAll(8);
            panel.AddThemeStyleboxOverride("panel", bg);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 12);

            var title = new Label();
            title.Text = "Choose Material Type";
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.AddThemeFontSizeOverride("font_size", 20);
            title.AddThemeColorOverride("font_color", BitPalette.Accent);
            vbox.AddChild(title);

            var desc = new Label();
            desc.Text = "Your Mining Building can harvest one type of magic.\nThis choice is permanent for this run.";
            desc.HorizontalAlignment = HorizontalAlignment.Center;
            desc.AddThemeFontSizeOverride("font_size", 12);
            desc.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
            desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            vbox.AddChild(desc);

            AddMaterialButton(vbox, overlay, harvester, MaterialType.Chaos,
                "Chaos", "Confusion, misdirection, corrosion",
                new Color(0.7f, 0.2f, 0.9f));
            AddMaterialButton(vbox, overlay, harvester, MaterialType.Power,
                "Power", "Range extension, signal amplification",
                new Color(1f, 0.7f, 0.1f));
            AddMaterialButton(vbox, overlay, harvester, MaterialType.Environment,
                "Environment", "Terrain manipulation, deconstruction",
                new Color(0.2f, 0.85f, 0.3f));

            panel.AddChild(vbox);
            overlay.AddChild(panel);
            GetTree().Root.AddChild(overlay);
        }

        private void AddMaterialButton(VBoxContainer parent, CanvasLayer overlay,
            VineHarvester harvester, MaterialType type, string name, string desc, Color color)
        {
            var btn = new Button();
            btn.Text = $"{name} — {desc}";
            btn.AddThemeFontSizeOverride("font_size", 14);
            btn.AddThemeColorOverride("font_color", color);

            var btnStyle = new StyleBoxFlat();
            btnStyle.BgColor = new Color(0.08f, 0.08f, 0.12f);
            btnStyle.BorderColor = color * 0.5f;
            btnStyle.SetBorderWidthAll(1);
            btnStyle.SetCornerRadiusAll(4);
            btn.AddThemeStyleboxOverride("normal", btnStyle);

            var hoverStyle = (StyleBoxFlat)btnStyle.Duplicate();
            hoverStyle.BgColor = new Color(color.R * 0.15f, color.G * 0.15f, color.B * 0.15f);
            hoverStyle.BorderColor = color;
            btn.AddThemeStyleboxOverride("hover", hoverStyle);

            btn.Pressed += () =>
            {
                harvester.SelectMaterialType(type);
                GameManager.Instance.SelectedMaterialType = type;
                overlay.QueueFree();
            };
            parent.AddChild(btn);
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

