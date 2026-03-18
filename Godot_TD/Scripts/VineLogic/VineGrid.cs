using System;
using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Grid for Vine Logic TD. Tracks cells, node placement, and vine connections.
    /// Connections are edges between adjacent cells that hold nodes.
    /// </summary>
    public partial class VineGrid : Node3D
    {
        [Export] public int Width { get; set; } = Constants.VINE_MAP_WIDTH;
        [Export] public int Height { get; set; } = Constants.VINE_MAP_HEIGHT;

        private VineCellType[,] _cells;
        private VineNode[,] _nodes;  // Node reference per cell (null if empty)

        // Connections between adjacent node cells — key is sorted pair of grid positions
        private readonly Dictionary<(Vector2I, Vector2I), VineConnection> _connections = new();

        // Entry/exit points
        private readonly List<Vector2I> _entryPoints = new();
        private Vector2I _exitPoint;

        public IReadOnlyList<Vector2I> EntryPoints => _entryPoints;
        public Vector2I ExitPoint => _exitPoint;

        private MeshInstance3D _groundMesh;

        private static readonly Vector2I[] Directions = {
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1)
        };

        public override void _Ready()
        {
            _cells = new VineCellType[Width, Height];
            _nodes = new VineNode[Width, Height];

            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                _cells[x, y] = VineCellType.Empty;

            BuildGroundPlane();
            ServiceLocator.Register(this);
        }

        // ── Grid queries ──

        public VineCellType GetCell(int x, int y)
        {
            if (!InBounds(x, y)) return VineCellType.Wall;
            return _cells[x, y];
        }

        public VineCellType GetCell(Vector2I pos) => GetCell(pos.X, pos.Y);
        public bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;
        public bool InBounds(Vector2I pos) => InBounds(pos.X, pos.Y);

        public VineNode GetNode(int x, int y) => InBounds(x, y) ? _nodes[x, y] : null;
        public VineNode GetNode(Vector2I pos) => GetNode(pos.X, pos.Y);

        /// <summary>
        /// Whether enemies can walk through this cell. Empty, Entry, Exit are walkable.
        /// Nodes with routing behavior (Gate, Switch) may be walkable depending on state.
        /// </summary>
        public bool IsWalkable(int x, int y)
        {
            if (!InBounds(x, y)) return false;
            var cell = _cells[x, y];
            if (cell == VineCellType.Empty || cell == VineCellType.Entry || cell == VineCellType.Exit
                || cell == VineCellType.Channel || cell == VineCellType.DataStream)
                return true;
            if (cell == VineCellType.Node)
            {
                var node = _nodes[x, y];
                if (node != null) return node.IsWalkable;
            }
            return false;
        }

        public bool IsWalkable(Vector2I pos) => IsWalkable(pos.X, pos.Y);

        /// <summary>
        /// Whether a vine node can be placed on this cell.
        /// </summary>
        public bool CanPlace(int x, int y)
        {
            if (!InBounds(x, y)) return false;
            return _cells[x, y] == VineCellType.Empty;
        }

        public bool CanPlace(Vector2I pos) => CanPlace(pos.X, pos.Y);

        // ── Node placement ──

        public bool PlaceNode(VineNode node, Vector2I pos)
        {
            if (!CanPlace(pos)) return false;

            _cells[pos.X, pos.Y] = VineCellType.Node;
            _nodes[pos.X, pos.Y] = node;
            node.GridPosition = pos;

            AddChild(node);
            node.GlobalPosition = GridToWorld(pos) + new Vector3(0, 0.5f, 0);

            // Auto-connect to adjacent nodes
            foreach (var dir in Directions)
            {
                var neighbor = pos + dir;
                var neighborNode = GetNode(neighbor);
                if (neighborNode != null)
                    CreateConnection(pos, neighbor);
            }

            GameEvents.OnVineNodePlaced?.Invoke(node);
            GameEvents.OnVinePathRecalculated?.Invoke();
            return true;
        }

        public void RemoveNode(Vector2I pos)
        {
            if (!InBounds(pos) || _cells[pos.X, pos.Y] != VineCellType.Node) return;

            var node = _nodes[pos.X, pos.Y];

            // Remove all connections to this node
            foreach (var dir in Directions)
            {
                var neighbor = pos + dir;
                RemoveConnection(pos, neighbor);
            }

            _cells[pos.X, pos.Y] = VineCellType.Empty;
            _nodes[pos.X, pos.Y] = null;

            if (node != null)
            {
                GameEvents.OnVineNodeSold?.Invoke(node);
                node.QueueFree();
            }
            GameEvents.OnVinePathRecalculated?.Invoke();
        }

        // ── Connections ──

        private static (Vector2I, Vector2I) SortPair(Vector2I a, Vector2I b)
        {
            if (a.X < b.X || (a.X == b.X && a.Y < b.Y)) return (a, b);
            return (b, a);
        }

        public void CreateConnection(Vector2I a, Vector2I b)
        {
            var key = SortPair(a, b);
            if (_connections.ContainsKey(key)) return;

            var nodeA = GetNode(a);
            var nodeB = GetNode(b);
            if (nodeA == null || nodeB == null) return;

            // Check both nodes have available connection slots
            if (!nodeA.HasFreeSlot() || !nodeB.HasFreeSlot()) return;

            var conn = new VineConnection(a, b, this);
            _connections[key] = conn;
            AddChild(conn);

            nodeA.OnConnected(b);
            nodeB.OnConnected(a);
        }

        public void RemoveConnection(Vector2I a, Vector2I b)
        {
            var key = SortPair(a, b);
            if (!_connections.TryGetValue(key, out var conn)) return;

            var nodeA = GetNode(a);
            var nodeB = GetNode(b);
            nodeA?.OnDisconnected(b);
            nodeB?.OnDisconnected(a);

            _connections.Remove(key);
            conn.QueueFree();
        }

        public VineConnection GetConnection(Vector2I a, Vector2I b)
        {
            var key = SortPair(a, b);
            return _connections.TryGetValue(key, out var conn) ? conn : null;
        }

        public IEnumerable<VineConnection> GetConnections() => _connections.Values;

        /// <summary>
        /// Get all nodes connected to the node at the given position.
        /// </summary>
        public List<VineNode> GetConnectedNodes(Vector2I pos)
        {
            var result = new List<VineNode>();
            foreach (var dir in Directions)
            {
                var neighbor = pos + dir;
                var key = SortPair(pos, neighbor);
                if (_connections.ContainsKey(key))
                {
                    var node = GetNode(neighbor);
                    if (node != null) result.Add(node);
                }
            }
            return result;
        }

        /// <summary>
        /// Get connections from a specific node position.
        /// </summary>
        public List<VineConnection> GetConnectionsFrom(Vector2I pos)
        {
            var result = new List<VineConnection>();
            foreach (var dir in Directions)
            {
                var neighbor = pos + dir;
                var key = SortPair(pos, neighbor);
                if (_connections.TryGetValue(key, out var conn))
                    result.Add(conn);
            }
            return result;
        }

        // ── Walls ──

        private static readonly RandomNumberGenerator _terrainRng = new();

        public void SetWall(int x, int y)
        {
            if (!InBounds(x, y)) return;
            if (_cells[x, y] == VineCellType.Entry || _cells[x, y] == VineCellType.Exit) return;
            _cells[x, y] = VineCellType.Wall;

            float cs = Constants.VINE_CELL_SIZE;
            var pos = GridToWorld(x, y);
            var wallNode = new Node3D();
            wallNode.Position = pos;
            AddChild(wallNode);

            // Randomize wall shape — jagged shards, broken pillars, angled debris
            int variant = _terrainRng.RandiRange(0, 4);
            switch (variant)
            {
                case 0: // Angled slab — tilted box
                    var slab = MakeMeshNode(new BoxMesh { Size = new Vector3(cs * 0.85f, 0.9f, cs * 0.7f) },
                        TronTheme.MakeWallBodyMaterial());
                    slab.Position = new Vector3(0, 0.45f, 0);
                    slab.RotationDegrees = new Vector3(_terrainRng.RandfRange(-8, 8), _terrainRng.RandfRange(-15, 15), _terrainRng.RandfRange(-5, 5));
                    wallNode.AddChild(slab);
                    TronTheme.AddWireframeEdges(slab, new Vector3(cs * 0.85f, 0.9f, cs * 0.7f));
                    break;

                case 1: // Broken pillar — cylinder with tilt
                    var pillar = MakeMeshNode(new CylinderMesh {
                        TopRadius = cs * 0.2f, BottomRadius = cs * 0.35f,
                        Height = _terrainRng.RandfRange(0.8f, 1.4f) },
                        TronTheme.MakeWallBodyMaterial());
                    pillar.Position = new Vector3(0, pillar.Mesh is CylinderMesh c ? c.Height / 2f : 0.5f, 0);
                    pillar.RotationDegrees = new Vector3(_terrainRng.RandfRange(-10, 10), 0, _terrainRng.RandfRange(-10, 10));
                    wallNode.AddChild(pillar);
                    break;

                case 2: // Triangular shard — prism using a thin tall box rotated 45°
                    var shard = MakeMeshNode(new BoxMesh { Size = new Vector3(cs * 0.6f, 1.1f, cs * 0.6f) },
                        TronTheme.MakeWallBodyMaterial());
                    shard.Position = new Vector3(0, 0.55f, 0);
                    shard.RotationDegrees = new Vector3(0, 45, 0);
                    shard.Scale = new Vector3(1f, 1f, 0.5f); // Flatten one axis to make it triangular
                    wallNode.AddChild(shard);
                    TronTheme.AddWireframeEdges(shard, new Vector3(cs * 0.6f, 1.1f, cs * 0.3f));
                    break;

                case 3: // Rock pile — 2-3 small offset boxes
                    for (int i = 0; i < _terrainRng.RandiRange(2, 3); i++)
                    {
                        float s = _terrainRng.RandfRange(0.25f, 0.5f);
                        var rock = MakeMeshNode(new BoxMesh { Size = new Vector3(s, s * 0.8f, s * _terrainRng.RandfRange(0.6f, 1.2f)) },
                            TronTheme.MakeWallBodyMaterial());
                        rock.Position = new Vector3(
                            _terrainRng.RandfRange(-0.3f, 0.3f),
                            s * 0.4f,
                            _terrainRng.RandfRange(-0.3f, 0.3f));
                        rock.RotationDegrees = new Vector3(
                            _terrainRng.RandfRange(-15, 15),
                            _terrainRng.RandfRange(0, 90),
                            _terrainRng.RandfRange(-15, 15));
                        wallNode.AddChild(rock);
                    }
                    break;

                default: // Classic block (fallback)
                    var block = MakeMeshNode(new BoxMesh { Size = new Vector3(cs * 0.9f, 1f, cs * 0.9f) },
                        TronTheme.MakeWallBodyMaterial());
                    block.Position = new Vector3(0, 0.5f, 0);
                    wallNode.AddChild(block);
                    TronTheme.AddWireframeEdges(block, new Vector3(cs * 0.9f, 1f, cs * 0.9f));
                    break;
            }
        }

        // ── Terrain Features ──

        public void SetElevated(int x, int y)
        {
            if (!InBounds(x, y)) return;
            if (_cells[x, y] == VineCellType.Entry || _cells[x, y] == VineCellType.Exit) return;
            _cells[x, y] = VineCellType.Elevated;

            float cs = Constants.VINE_CELL_SIZE;
            var pos = GridToWorld(x, y);
            var elevNode = new Node3D();
            elevNode.Position = pos;
            AddChild(elevNode);

            int variant = _terrainRng.RandiRange(0, 3);
            switch (variant)
            {
                case 0: // Mesa — flat-topped cylinder
                    float mH = _terrainRng.RandfRange(1.2f, 2.0f);
                    var mesa = MakeMeshNode(new CylinderMesh {
                        TopRadius = cs * 0.4f, BottomRadius = cs * 0.45f,
                        Height = mH, RadialSegments = _terrainRng.RandiRange(5, 8) },
                        TronTheme.MakeElevatedMaterial());
                    mesa.Position = new Vector3(0, mH / 2f, 0);
                    elevNode.AddChild(mesa);
                    break;

                case 1: // Stepped formation — 2-3 stacked boxes getting smaller
                    float baseH = 0;
                    for (int i = 0; i < _terrainRng.RandiRange(2, 3); i++)
                    {
                        float stepW = cs * (0.85f - i * 0.15f);
                        float stepH = _terrainRng.RandfRange(0.3f, 0.6f);
                        var step = MakeMeshNode(new BoxMesh { Size = new Vector3(stepW, stepH, stepW) },
                            TronTheme.MakeElevatedMaterial());
                        step.Position = new Vector3(0, baseH + stepH / 2f, 0);
                        step.RotationDegrees = new Vector3(0, i * 15, 0);
                        elevNode.AddChild(step);
                        TronTheme.AddWireframeEdges(step, new Vector3(stepW, stepH, stepW));
                        baseH += stepH;
                    }
                    break;

                case 2: // Angled impact slab — tilted flat piece like shattered ground
                    var impact = MakeMeshNode(new BoxMesh { Size = new Vector3(cs * 0.8f, 0.2f, cs * 0.8f) },
                        TronTheme.MakeElevatedMaterial());
                    impact.Position = new Vector3(0, 0.8f, 0);
                    impact.RotationDegrees = new Vector3(_terrainRng.RandfRange(-20, 20), _terrainRng.RandfRange(0, 45), _terrainRng.RandfRange(-15, 15));
                    elevNode.AddChild(impact);
                    // Support pillar underneath
                    var support = MakeMeshNode(new CylinderMesh {
                        TopRadius = 0.15f, BottomRadius = 0.25f, Height = 0.8f },
                        TronTheme.MakeWallBodyMaterial());
                    support.Position = new Vector3(_terrainRng.RandfRange(-0.2f, 0.2f), 0.4f, _terrainRng.RandfRange(-0.2f, 0.2f));
                    elevNode.AddChild(support);
                    break;

                default: // Spire — tall pointed column
                    float sH = _terrainRng.RandfRange(1.5f, 2.5f);
                    var spire = MakeMeshNode(new CylinderMesh {
                        TopRadius = 0.05f, BottomRadius = cs * 0.3f,
                        Height = sH, RadialSegments = _terrainRng.RandiRange(4, 6) },
                        TronTheme.MakeElevatedMaterial());
                    spire.Position = new Vector3(0, sH / 2f, 0);
                    elevNode.AddChild(spire);
                    break;
            }
        }

        public void SetChannel(int x, int y)
        {
            if (!InBounds(x, y)) return;
            if (_cells[x, y] == VineCellType.Entry || _cells[x, y] == VineCellType.Exit) return;
            _cells[x, y] = VineCellType.Channel;

            float cs = Constants.VINE_CELL_SIZE;
            var pos = GridToWorld(x, y);

            // Recessed trench with angular edges
            var channelNode = new Node3D();
            channelNode.Position = pos;
            AddChild(channelNode);

            // Main recessed floor
            var floor = MakeMeshNode(new BoxMesh {
                Size = new Vector3(cs * 0.95f, 0.08f, cs * 0.95f) },
                TronTheme.MakeChannelMaterial());
            floor.Position = new Vector3(0, -0.15f, 0);
            channelNode.AddChild(floor);

            // Angled edge pieces — broken lip of the trench
            if (_terrainRng.Randf() > 0.4f)
            {
                var edgeL = MakeMeshNode(new BoxMesh { Size = new Vector3(0.1f, 0.15f, cs * 0.6f) },
                    TronTheme.MakeWallBodyMaterial());
                edgeL.Position = new Vector3(-cs * 0.45f, 0.02f, 0);
                edgeL.RotationDegrees = new Vector3(0, 0, _terrainRng.RandfRange(-15, 15));
                channelNode.AddChild(edgeL);
            }
            if (_terrainRng.Randf() > 0.4f)
            {
                var edgeR = MakeMeshNode(new BoxMesh { Size = new Vector3(0.1f, 0.12f, cs * 0.5f) },
                    TronTheme.MakeWallBodyMaterial());
                edgeR.Position = new Vector3(cs * 0.45f, 0.02f, 0);
                edgeR.RotationDegrees = new Vector3(0, 0, _terrainRng.RandfRange(-15, 15));
                channelNode.AddChild(edgeR);
            }
        }

        public void SetDataStream(int x, int y)
        {
            if (!InBounds(x, y)) return;
            if (_cells[x, y] == VineCellType.Entry || _cells[x, y] == VineCellType.Exit) return;
            _cells[x, y] = VineCellType.DataStream;

            float cs = Constants.VINE_CELL_SIZE;
            var pos = GridToWorld(x, y);

            var streamNode = new Node3D();
            streamNode.Position = pos;
            AddChild(streamNode);

            // Main stream surface
            var surface = MakeMeshNode(new BoxMesh {
                Size = new Vector3(cs * 0.95f, 0.04f, cs * 0.95f) },
                TronTheme.MakeDataStreamMaterial());
            surface.Position = new Vector3(0, 0.02f, 0);
            streamNode.AddChild(surface);

            // Cracked conduit edges — small angular debris alongside
            if (_terrainRng.Randf() > 0.6f)
            {
                float s = _terrainRng.RandfRange(0.08f, 0.15f);
                var debris = MakeMeshNode(new BoxMesh { Size = new Vector3(s, s * 1.5f, s * 0.7f) },
                    TronTheme.MakeWallBodyMaterial());
                debris.Position = new Vector3(
                    _terrainRng.RandfRange(-0.7f, 0.7f), s * 0.5f,
                    _terrainRng.RandfRange(-0.7f, 0.7f));
                debris.RotationDegrees = new Vector3(
                    _terrainRng.RandfRange(-20, 20),
                    _terrainRng.RandfRange(0, 90),
                    _terrainRng.RandfRange(-20, 20));
                streamNode.AddChild(debris);
            }
        }

        private static MeshInstance3D MakeMeshNode(Mesh mesh, Material material)
        {
            var inst = new MeshInstance3D();
            inst.Mesh = mesh;
            // Apply Tron outline treatment if it's a StandardMaterial3D
            if (material is StandardMaterial3D stdMat)
                TronTheme.ApplyTronOutline(inst, stdMat);
            else
                inst.MaterialOverride = material;
            return inst;
        }

        // ── Entry/Exit ──

        public void SetEntry(int x, int y)
        {
            if (!InBounds(x, y)) return;
            _cells[x, y] = VineCellType.Entry;
            _entryPoints.Add(new Vector2I(x, y));
        }

        public void SetExit(int x, int y)
        {
            if (!InBounds(x, y)) return;
            _cells[x, y] = VineCellType.Exit;
            _exitPoint = new Vector2I(x, y);
        }

        // ── Coordinate conversion ──

        public Vector3 GridToWorld(int x, int y) =>
            new((x + 0.5f) * Constants.VINE_CELL_SIZE, 0f, (y + 0.5f) * Constants.VINE_CELL_SIZE);

        public Vector3 GridToWorld(Vector2I pos) => GridToWorld(pos.X, pos.Y);

        public Vector2I WorldToGrid(Vector3 worldPos) =>
            new(Mathf.FloorToInt(worldPos.X / Constants.VINE_CELL_SIZE),
                Mathf.FloorToInt(worldPos.Z / Constants.VINE_CELL_SIZE));

        // ── Visuals ──

        private void BuildGroundPlane()
        {
            _groundMesh = new MeshInstance3D();
            var planeMesh = new PlaneMesh();
            planeMesh.Size = new Vector2(Width * Constants.VINE_CELL_SIZE, Height * Constants.VINE_CELL_SIZE);
            _groundMesh.Mesh = planeMesh;
            _groundMesh.Position = new Vector3(
                Width * Constants.VINE_CELL_SIZE / 2f, 0f,
                Height * Constants.VINE_CELL_SIZE / 2f);

            _groundMesh.MaterialOverride = TronTheme.MakeGroundMaterial();

            // Ground collision for raycasting
            var body = new StaticBody3D();
            body.CollisionLayer = Constants.MASK_GROUND;
            var shape = new CollisionShape3D();
            var boxShape = new BoxShape3D();
            boxShape.Size = new Vector3(Width * Constants.VINE_CELL_SIZE, 0.1f, Height * Constants.VINE_CELL_SIZE);
            shape.Shape = boxShape;
            body.AddChild(shape);
            _groundMesh.AddChild(body);

            AddChild(_groundMesh);
            BuildGridLines();
        }

        private void BuildGridLines()
        {
            var gridVisual = new MeshInstance3D();
            var im = new ImmediateMesh();
            gridVisual.Mesh = im;
            gridVisual.Position = new Vector3(0f, 0.02f, 0f);

            gridVisual.MaterialOverride = TronTheme.MakeGridLineMaterial();

            im.SurfaceBegin(Mesh.PrimitiveType.Lines);
            float cs = Constants.VINE_CELL_SIZE;
            for (int x = 0; x <= Width; x++)
            {
                im.SurfaceAddVertex(new Vector3(x * cs, 0, 0));
                im.SurfaceAddVertex(new Vector3(x * cs, 0, Height * cs));
            }
            for (int y = 0; y <= Height; y++)
            {
                im.SurfaceAddVertex(new Vector3(0, 0, y * cs));
                im.SurfaceAddVertex(new Vector3(Width * cs, 0, y * cs));
            }
            im.SurfaceEnd();

            AddChild(gridVisual);
        }

        public override void _ExitTree()
        {
            ServiceLocator.Unregister<VineGrid>();
        }
    }
}
