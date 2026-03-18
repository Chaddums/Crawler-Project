using System;
using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Height override region — forces a rectangular area to a target height.
    /// Used to flatten entry/exit zones.
    /// </summary>
    public struct HeightOverride
    {
        public int X1, Y1, X2, Y2;
        public float TargetHeight;
        public HeightOverride(int x1, int y1, int x2, int y2, float target)
        { X1 = x1; Y1 = y1; X2 = x2; Y2 = y2; TargetHeight = target; }
    }

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

        // Heightmap — stores height at cell CORNERS: [Width+1, Height+1]
        private float[,] _heightmap;

        // Connections between adjacent node cells — key is sorted pair of grid positions
        private readonly Dictionary<(Vector2I, Vector2I), VineConnection> _connections = new();

        // Entry/exit points
        private readonly List<Vector2I> _entryPoints = new();
        private Vector2I _exitPoint;

        // Entry regions (for chaotic spawning)
        private readonly List<VineEntryRegion> _entryRegions = new();

        public IReadOnlyList<Vector2I> EntryPoints => _entryPoints;
        public Vector2I ExitPoint => _exitPoint;
        public IReadOnlyList<VineEntryRegion> EntryRegions => _entryRegions;

        // Harvester reference
        public VineHarvester Harvester { get; set; }

        private MeshInstance3D _groundMesh;

        private static readonly Vector2I[] Directions = {
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1)
        };

        public override void _Ready()
        {
            _cells = new VineCellType[Width, Height];
            _nodes = new VineNode[Width, Height];
            _heightmap = new float[Width + 1, Height + 1];

            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                _cells[x, y] = VineCellType.Empty;

            // Don't build terrain mesh yet — defer until after layout + heightmap are set
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
                        GetTerrainBodyMaterial());
                    slab.Position = new Vector3(0, 0.45f, 0);
                    slab.RotationDegrees = new Vector3(_terrainRng.RandfRange(-8, 8), _terrainRng.RandfRange(-15, 15), _terrainRng.RandfRange(-5, 5));
                    wallNode.AddChild(slab);
                    if (!IsScrapyard) TronTheme.AddWireframeEdges(slab, new Vector3(cs * 0.85f, 0.9f, cs * 0.7f));
                    break;

                case 1: // Broken pillar — cylinder with tilt
                    var pillar = MakeMeshNode(new CylinderMesh {
                        TopRadius = cs * 0.2f, BottomRadius = cs * 0.35f,
                        Height = _terrainRng.RandfRange(0.8f, 1.4f) },
                        GetTerrainBodyMaterial());
                    pillar.Position = new Vector3(0, pillar.Mesh is CylinderMesh c ? c.Height / 2f : 0.5f, 0);
                    pillar.RotationDegrees = new Vector3(_terrainRng.RandfRange(-10, 10), 0, _terrainRng.RandfRange(-10, 10));
                    wallNode.AddChild(pillar);
                    break;

                case 2: // Triangular shard — prism using a thin tall box rotated 45°
                    var shard = MakeMeshNode(new BoxMesh { Size = new Vector3(cs * 0.6f, 1.1f, cs * 0.6f) },
                        GetTerrainBodyMaterial());
                    shard.Position = new Vector3(0, 0.55f, 0);
                    shard.RotationDegrees = new Vector3(0, 45, 0);
                    shard.Scale = new Vector3(1f, 1f, 0.5f); // Flatten one axis to make it triangular
                    wallNode.AddChild(shard);
                    if (!IsScrapyard) TronTheme.AddWireframeEdges(shard, new Vector3(cs * 0.6f, 1.1f, cs * 0.3f));
                    break;

                case 3: // Rock pile — 2-3 small offset boxes
                    for (int i = 0; i < _terrainRng.RandiRange(2, 3); i++)
                    {
                        float s = _terrainRng.RandfRange(0.25f, 0.5f);
                        var rock = MakeMeshNode(new BoxMesh { Size = new Vector3(s, s * 0.8f, s * _terrainRng.RandfRange(0.6f, 1.2f)) },
                            GetTerrainBodyMaterial());
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
                        GetTerrainBodyMaterial());
                    block.Position = new Vector3(0, 0.5f, 0);
                    wallNode.AddChild(block);
                    if (!IsScrapyard) TronTheme.AddWireframeEdges(block, new Vector3(cs * 0.9f, 1f, cs * 0.9f));
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
                        GetTerrainElevatedMaterial());
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
                            GetTerrainElevatedMaterial());
                        step.Position = new Vector3(0, baseH + stepH / 2f, 0);
                        step.RotationDegrees = new Vector3(0, i * 15, 0);
                        elevNode.AddChild(step);
                        if (!IsScrapyard) TronTheme.AddWireframeEdges(step, new Vector3(stepW, stepH, stepW));
                        baseH += stepH;
                    }
                    break;

                case 2: // Angled impact slab — tilted flat piece like shattered ground
                    var impact = MakeMeshNode(new BoxMesh { Size = new Vector3(cs * 0.8f, 0.2f, cs * 0.8f) },
                        GetTerrainElevatedMaterial());
                    impact.Position = new Vector3(0, 0.8f, 0);
                    impact.RotationDegrees = new Vector3(_terrainRng.RandfRange(-20, 20), _terrainRng.RandfRange(0, 45), _terrainRng.RandfRange(-15, 15));
                    elevNode.AddChild(impact);
                    // Support pillar underneath
                    var support = MakeMeshNode(new CylinderMesh {
                        TopRadius = 0.15f, BottomRadius = 0.25f, Height = 0.8f },
                        GetTerrainBodyMaterial());
                    support.Position = new Vector3(_terrainRng.RandfRange(-0.2f, 0.2f), 0.4f, _terrainRng.RandfRange(-0.2f, 0.2f));
                    elevNode.AddChild(support);
                    break;

                default: // Spire — tall pointed column
                    float sH = _terrainRng.RandfRange(1.5f, 2.5f);
                    var spire = MakeMeshNode(new CylinderMesh {
                        TopRadius = 0.05f, BottomRadius = cs * 0.3f,
                        Height = sH, RadialSegments = _terrainRng.RandiRange(4, 6) },
                        GetTerrainElevatedMaterial());
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
                IsScrapyard ? ScrapyardEnvironment.GetDarkMetalMaterial() : TronTheme.MakeChannelMaterial());
            floor.Position = new Vector3(0, -0.15f, 0);
            channelNode.AddChild(floor);

            // Angled edge pieces — broken lip of the trench
            if (_terrainRng.Randf() > 0.4f)
            {
                var edgeL = MakeMeshNode(new BoxMesh { Size = new Vector3(0.1f, 0.15f, cs * 0.6f) },
                    GetTerrainBodyMaterial());
                edgeL.Position = new Vector3(-cs * 0.45f, 0.02f, 0);
                edgeL.RotationDegrees = new Vector3(0, 0, _terrainRng.RandfRange(-15, 15));
                channelNode.AddChild(edgeL);
            }
            if (_terrainRng.Randf() > 0.4f)
            {
                var edgeR = MakeMeshNode(new BoxMesh { Size = new Vector3(0.1f, 0.12f, cs * 0.5f) },
                    GetTerrainBodyMaterial());
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
                IsScrapyard ? ScrapyardEnvironment.GetRustedMetalMaterial() : TronTheme.MakeDataStreamMaterial());
            surface.Position = new Vector3(0, 0.02f, 0);
            streamNode.AddChild(surface);

            // Cracked conduit edges — small angular debris alongside
            if (_terrainRng.Randf() > 0.6f)
            {
                float s = _terrainRng.RandfRange(0.08f, 0.15f);
                var debris = MakeMeshNode(new BoxMesh { Size = new Vector3(s, s * 1.5f, s * 0.7f) },
                    GetTerrainBodyMaterial());
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

        // ── Planet-aware material helpers ──

        private static bool IsScrapyard => PlanetTheme.Current is ScrapyardPlanetTheme;

        private static StandardMaterial3D GetTerrainBodyMaterial()
        {
            if (IsScrapyard)
                return ScrapyardEnvironment.GetRustedMetalMaterial();
            return TronTheme.MakeWallBodyMaterial();
        }

        private static StandardMaterial3D GetTerrainElevatedMaterial()
        {
            if (IsScrapyard)
                return ScrapyardEnvironment.GetConcreteMaterial();
            return TronTheme.MakeElevatedMaterial();
        }

        private static void ApplyTerrainStyle(MeshInstance3D mesh, StandardMaterial3D bodyMat, Vector3? wireframeSize = null)
        {
            if (IsScrapyard)
            {
                // Scrapyard: solid textured, no outline
                mesh.MaterialOverride = bodyMat;
            }
            else
            {
                // Tron: dark body + cyan outline
                TronTheme.ApplyTronOutline(mesh, bodyMat);
            }
        }

        private static MeshInstance3D MakeMeshNode(Mesh mesh, Material material)
        {
            var inst = new MeshInstance3D();
            inst.Mesh = mesh;
            if (material is StandardMaterial3D stdMat)
            {
                if (IsScrapyard)
                    inst.MaterialOverride = stdMat; // Solid textured, no outline
                else
                    TronTheme.ApplyTronOutline(inst, stdMat); // Tron outline treatment
            }
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

            // Also create a single-cell entry region for backward compat
            var region = new VineEntryRegion(_entryRegions.Count);
            region.Cells.Add(new Vector2I(x, y));
            _entryRegions.Add(region);
        }

        public void SetEntryRegion(int startX, int startY, int endX, int endY)
        {
            var region = new VineEntryRegion(_entryRegions.Count);
            for (int x = startX; x <= endX; x++)
            for (int y = startY; y <= endY; y++)
            {
                if (!InBounds(x, y)) continue;
                _cells[x, y] = VineCellType.Entry;
                region.Cells.Add(new Vector2I(x, y));
            }
            if (region.Cells.Count > 0)
            {
                _entryPoints.Add(region.Center);
                _entryRegions.Add(region);
            }
        }

        public void SetExit(int x, int y)
        {
            if (!InBounds(x, y)) return;
            _cells[x, y] = VineCellType.Exit;
            _exitPoint = new Vector2I(x, y);
        }

        // ── Coordinate conversion ──

        public Vector3 GridToWorld(int x, int y) =>
            new((x + 0.5f) * Constants.VINE_CELL_SIZE, GetCellHeight(x, y), (y + 0.5f) * Constants.VINE_CELL_SIZE);

        public Vector3 GridToWorld(Vector2I pos) => GridToWorld(pos.X, pos.Y);

        public Vector2I WorldToGrid(Vector3 worldPos) =>
            new(Mathf.FloorToInt(worldPos.X / Constants.VINE_CELL_SIZE),
                Mathf.FloorToInt(worldPos.Z / Constants.VINE_CELL_SIZE));

        // ── Heightmap ──

        /// <summary>
        /// Generate heightmap using Simplex noise. Called from VineMapLayouts before terrain mesh is built.
        /// </summary>
        public void GenerateHeightmap(TerrainProfile profile, List<HeightOverride> overrides = null)
        {
            var noise = new FastNoiseLite();
            noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
            noise.Frequency = Constants.HEIGHTMAP_NOISE_FREQ;
            noise.Seed = (int)(GD.Randi() % 99999);

            float amplitude;
            switch (profile)
            {
                case TerrainProfile.Gentle:
                    amplitude = 1.0f;
                    for (int cx = 0; cx <= Width; cx++)
                    for (int cy = 0; cy <= Height; cy++)
                        _heightmap[cx, cy] = noise.GetNoise2D(cx, cy) * amplitude;
                    break;

                case TerrainProfile.Valley:
                    amplitude = 2.5f;
                    for (int cx = 0; cx <= Width; cx++)
                    for (int cy = 0; cy <= Height; cy++)
                    {
                        float n = noise.GetNoise2D(cx, cy);
                        // Parabolic depression along Z center — ridges at top/bottom
                        float centerZ = Height / 2f;
                        float distFromCenter = Mathf.Abs(cy - centerZ) / centerZ;
                        float valleyShape = distFromCenter * distFromCenter; // 0 at center, 1 at edges
                        _heightmap[cx, cy] = (n * 0.5f + valleyShape) * amplitude;
                    }
                    break;

                case TerrainProfile.Complex:
                default:
                    amplitude = Constants.HEIGHTMAP_MAX_AMPLITUDE;
                    var noise2 = new FastNoiseLite();
                    noise2.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
                    noise2.Frequency = Constants.HEIGHTMAP_NOISE_FREQ * 2f;
                    noise2.Seed = noise.Seed + 42;

                    for (int cx = 0; cx <= Width; cx++)
                    for (int cy = 0; cy <= Height; cy++)
                    {
                        float n1 = noise.GetNoise2D(cx, cy);
                        float n2 = noise2.GetNoise2D(cx, cy) * 0.4f;
                        float combined = n1 + n2;
                        // Plateau forcing — clamp peaks to create flat tops
                        if (combined > 0.6f) combined = 0.6f + (combined - 0.6f) * 0.2f;
                        _heightmap[cx, cy] = combined * amplitude;
                    }
                    break;
            }

            // Apply height overrides (flatten entry/exit zones etc.)
            if (overrides != null)
            {
                foreach (var ov in overrides)
                {
                    for (int cx = Mathf.Max(0, ov.X1); cx <= Mathf.Min(Width, ov.X2 + 1); cx++)
                    for (int cy = Mathf.Max(0, ov.Y1); cy <= Mathf.Min(Height, ov.Y2 + 1); cy++)
                        _heightmap[cx, cy] = ov.TargetHeight;
                }
            }
        }

        /// <summary>
        /// Get height at cell center (average of 4 corner heights).
        /// </summary>
        public float GetCellHeight(int x, int y)
        {
            if (_heightmap == null) return 0f;
            int cx = Mathf.Clamp(x, 0, Width - 1);
            int cy = Mathf.Clamp(y, 0, Height - 1);
            return (_heightmap[cx, cy] + _heightmap[cx + 1, cy] +
                    _heightmap[cx, cy + 1] + _heightmap[cx + 1, cy + 1]) * 0.25f;
        }

        public float GetCellHeight(Vector2I pos) => GetCellHeight(pos.X, pos.Y);

        /// <summary>
        /// Bilinear interpolation for smooth height at any world position.
        /// </summary>
        public float GetWorldHeight(float worldX, float worldZ)
        {
            if (_heightmap == null) return 0f;
            float cs = Constants.VINE_CELL_SIZE;
            // Convert to corner-space coordinates
            float fx = worldX / cs;
            float fz = worldZ / cs;
            int ix = Mathf.Clamp((int)fx, 0, Width - 1);
            int iz = Mathf.Clamp((int)fz, 0, Height - 1);
            float tx = Mathf.Clamp(fx - ix, 0f, 1f);
            float tz = Mathf.Clamp(fz - iz, 0f, 1f);

            float h00 = _heightmap[ix, iz];
            float h10 = _heightmap[ix + 1, iz];
            float h01 = _heightmap[ix, iz + 1];
            float h11 = _heightmap[ix + 1, iz + 1];

            float h0 = Mathf.Lerp(h00, h10, tx);
            float h1 = Mathf.Lerp(h01, h11, tx);
            return Mathf.Lerp(h0, h1, tz);
        }

        // ── Props ──

        public void SetProp(int x, int y, string propType)
        {
            if (!InBounds(x, y)) return;
            if (_cells[x, y] != VineCellType.Empty) return;
            _cells[x, y] = VineCellType.Prop;

            float cs = Constants.VINE_CELL_SIZE;
            var pos = GridToWorld(x, y);
            var propNode = new Node3D();
            propNode.Position = pos;
            AddChild(propNode);

            int variant = _terrainRng.RandiRange(0, 2);
            float rotY = _terrainRng.RandfRange(0, 360);
            float scaleJitter = _terrainRng.RandfRange(0.85f, 1.15f);

            switch (propType)
            {
                case "container":
                    var container = MakeMeshNode(new BoxMesh {
                        Size = new Vector3(cs * 0.7f * scaleJitter, 0.8f * scaleJitter, cs * 0.5f * scaleJitter) },
                        GetTerrainBodyMaterial());
                    container.Position = new Vector3(0, 0.4f * scaleJitter, 0);
                    container.RotationDegrees = new Vector3(0, rotY, 0);
                    propNode.AddChild(container);
                    if (!IsScrapyard) TronTheme.AddWireframeEdges(container, new Vector3(cs * 0.7f, 0.8f, cs * 0.5f) * scaleJitter);
                    break;

                case "generator":
                    float gH = 0.9f * scaleJitter;
                    var gen = MakeMeshNode(new CylinderMesh {
                        TopRadius = cs * 0.25f * scaleJitter, BottomRadius = cs * 0.3f * scaleJitter,
                        Height = gH, RadialSegments = 6 },
                        GetTerrainElevatedMaterial());
                    gen.Position = new Vector3(0, gH / 2f, 0);
                    gen.RotationDegrees = new Vector3(0, rotY, 0);
                    propNode.AddChild(gen);
                    break;

                case "barrel_stack":
                    for (int i = 0; i < _terrainRng.RandiRange(2, 3); i++)
                    {
                        float bH = _terrainRng.RandfRange(0.3f, 0.5f);
                        var barrel = MakeMeshNode(new CylinderMesh {
                            TopRadius = 0.2f, BottomRadius = 0.22f, Height = bH, RadialSegments = 8 },
                            GetTerrainBodyMaterial());
                        barrel.Position = new Vector3(
                            _terrainRng.RandfRange(-0.3f, 0.3f), bH / 2f + i * 0.15f,
                            _terrainRng.RandfRange(-0.3f, 0.3f));
                        barrel.RotationDegrees = new Vector3(
                            _terrainRng.RandfRange(-10, 10), rotY + i * 30, _terrainRng.RandfRange(-10, 10));
                        propNode.AddChild(barrel);
                    }
                    break;

                case "antenna":
                    float aH = _terrainRng.RandfRange(1.2f, 2.0f) * scaleJitter;
                    var antenna = MakeMeshNode(new CylinderMesh {
                        TopRadius = 0.04f, BottomRadius = 0.12f, Height = aH, RadialSegments = 4 },
                        GetTerrainElevatedMaterial());
                    antenna.Position = new Vector3(0, aH / 2f, 0);
                    propNode.AddChild(antenna);
                    // Dish at top
                    var dish = MakeMeshNode(new BoxMesh {
                        Size = new Vector3(0.4f * scaleJitter, 0.05f, 0.3f * scaleJitter) },
                        GetTerrainBodyMaterial());
                    dish.Position = new Vector3(0, aH - 0.1f, 0);
                    dish.RotationDegrees = new Vector3(30, rotY, 0);
                    propNode.AddChild(dish);
                    break;

                case "rubble_pile":
                    for (int i = 0; i < _terrainRng.RandiRange(3, 5); i++)
                    {
                        float s = _terrainRng.RandfRange(0.15f, 0.35f) * scaleJitter;
                        var rubble = MakeMeshNode(new BoxMesh {
                            Size = new Vector3(s, s * 0.6f, s * _terrainRng.RandfRange(0.5f, 1.3f)) },
                            GetTerrainBodyMaterial());
                        rubble.Position = new Vector3(
                            _terrainRng.RandfRange(-0.4f, 0.4f), s * 0.3f,
                            _terrainRng.RandfRange(-0.4f, 0.4f));
                        rubble.RotationDegrees = new Vector3(
                            _terrainRng.RandfRange(-20, 20), _terrainRng.RandfRange(0, 90),
                            _terrainRng.RandfRange(-20, 20));
                        propNode.AddChild(rubble);
                    }
                    break;

                case "pipe_cluster":
                default:
                    for (int i = 0; i < _terrainRng.RandiRange(2, 4); i++)
                    {
                        float pH = _terrainRng.RandfRange(0.5f, 1.0f) * scaleJitter;
                        var pipe = MakeMeshNode(new CylinderMesh {
                            TopRadius = 0.08f, BottomRadius = 0.08f, Height = pH, RadialSegments = 6 },
                            GetTerrainBodyMaterial());
                        pipe.Position = new Vector3(
                            _terrainRng.RandfRange(-0.3f, 0.3f), pH / 2f,
                            _terrainRng.RandfRange(-0.3f, 0.3f));
                        pipe.RotationDegrees = new Vector3(
                            _terrainRng.RandfRange(-30, 30), rotY + i * 25, _terrainRng.RandfRange(-15, 15));
                        propNode.AddChild(pipe);
                    }
                    break;
            }
        }

        // ── Visuals ──

        /// <summary>
        /// Build terrain mesh from heightmap. Call AFTER layout + heightmap are finalized.
        /// </summary>
        public void RebuildTerrainMesh()
        {
            // Remove old ground mesh if rebuilding
            _groundMesh?.QueueFree();

            float cs = Constants.VINE_CELL_SIZE;

            // Build ArrayMesh with 1 quad (2 tris) per cell
            var surfTool = new SurfaceTool();
            surfTool.Begin(Mesh.PrimitiveType.Triangles);

            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
            {
                // Corner positions with heightmap Y
                var v00 = new Vector3(x * cs, _heightmap[x, y], y * cs);
                var v10 = new Vector3((x + 1) * cs, _heightmap[x + 1, y], y * cs);
                var v01 = new Vector3(x * cs, _heightmap[x, y + 1], (y + 1) * cs);
                var v11 = new Vector3((x + 1) * cs, _heightmap[x + 1, y + 1], (y + 1) * cs);

                // UVs
                var uv00 = new Vector2((float)x / Width, (float)y / Height);
                var uv10 = new Vector2((float)(x + 1) / Width, (float)y / Height);
                var uv01 = new Vector2((float)x / Width, (float)(y + 1) / Height);
                var uv11 = new Vector2((float)(x + 1) / Width, (float)(y + 1) / Height);

                // Tri 1: v00, v10, v01
                surfTool.SetUV(uv00); surfTool.AddVertex(v00);
                surfTool.SetUV(uv10); surfTool.AddVertex(v10);
                surfTool.SetUV(uv01); surfTool.AddVertex(v01);

                // Tri 2: v10, v11, v01
                surfTool.SetUV(uv10); surfTool.AddVertex(v10);
                surfTool.SetUV(uv11); surfTool.AddVertex(v11);
                surfTool.SetUV(uv01); surfTool.AddVertex(v01);
            }

            surfTool.GenerateNormals();
            var arrayMesh = surfTool.Commit();

            _groundMesh = new MeshInstance3D();
            _groundMesh.Mesh = arrayMesh;
            _groundMesh.MaterialOverride = IsScrapyard
                ? ScrapyardEnvironment.GetGroundMaterial()
                : TronTheme.MakeGroundMaterial();

            // Collision from the same mesh for raycasting
            var body = new StaticBody3D();
            body.CollisionLayer = Constants.MASK_GROUND;
            var collisionShape = new CollisionShape3D();
            var concaveShape = arrayMesh.CreateTrimeshShape();
            collisionShape.Shape = concaveShape;
            body.AddChild(collisionShape);
            _groundMesh.AddChild(body);

            AddChild(_groundMesh);
            BuildGridLines();
        }

        private void BuildGridLines()
        {
            var gridVisual = new MeshInstance3D();
            var im = new ImmediateMesh();
            gridVisual.Mesh = im;

            if (IsScrapyard)
            {
                var scrapGridMat = new StandardMaterial3D();
                scrapGridMat.AlbedoColor = new Color(0.3f, 0.18f, 0.08f, 0.2f);
                scrapGridMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                scrapGridMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                gridVisual.MaterialOverride = scrapGridMat;
            }
            else
            {
                gridVisual.MaterialOverride = TronTheme.MakeGridLineMaterial();
            }

            float cs = Constants.VINE_CELL_SIZE;
            float lineY = 0.04f; // Small offset above terrain surface

            im.SurfaceBegin(Mesh.PrimitiveType.Lines);

            // Vertical grid lines (along Z axis) — drape over terrain
            for (int x = 0; x <= Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    float h0 = _heightmap[x < Width ? x : Width - 1, y];
                    float h1 = _heightmap[x < Width ? x : Width - 1, y + 1];
                    // Use corner heights directly, clamp x index
                    int hx = Mathf.Min(x, Width);
                    im.SurfaceAddVertex(new Vector3(x * cs, _heightmap[hx, y] + lineY, y * cs));
                    im.SurfaceAddVertex(new Vector3(x * cs, _heightmap[hx, y + 1] + lineY, (y + 1) * cs));
                }
            }

            // Horizontal grid lines (along X axis) — drape over terrain
            for (int y = 0; y <= Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int hy = Mathf.Min(y, Height);
                    im.SurfaceAddVertex(new Vector3(x * cs, _heightmap[x, hy] + lineY, y * cs));
                    im.SurfaceAddVertex(new Vector3((x + 1) * cs, _heightmap[x + 1, hy] + lineY, y * cs));
                }
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
