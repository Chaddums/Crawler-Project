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
            if (cell == VineCellType.Empty || cell == VineCellType.Entry || cell == VineCellType.Exit)
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

        public void SetWall(int x, int y)
        {
            if (!InBounds(x, y)) return;
            if (_cells[x, y] == VineCellType.Entry || _cells[x, y] == VineCellType.Exit) return;
            _cells[x, y] = VineCellType.Wall;

            // Visual: dark block
            var wallMesh = new MeshInstance3D();
            var box = new BoxMesh();
            box.Size = new Vector3(Constants.VINE_CELL_SIZE * 0.9f, 1f, Constants.VINE_CELL_SIZE * 0.9f);
            wallMesh.Mesh = box;
            wallMesh.Position = GridToWorld(x, y) + new Vector3(0, 0.5f, 0);
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.15f, 0.12f, 0.1f);
            mat.Roughness = 0.95f;
            wallMesh.MaterialOverride = mat;
            AddChild(wallMesh);
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

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.1f, 0.09f, 0.08f);
            mat.Roughness = 0.95f;
            _groundMesh.MaterialOverride = mat;

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

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.2f, 0.25f, 0.15f, 0.25f);
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            gridVisual.MaterialOverride = mat;

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
