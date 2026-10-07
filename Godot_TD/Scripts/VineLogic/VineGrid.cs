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

        /// <summary>
        /// Entry regions that are currently active (not gated by shield walls).
        /// </summary>
        public List<VineEntryRegion> ActiveEntryRegions
        {
            get
            {
                var active = new List<VineEntryRegion>();
                foreach (var r in _entryRegions)
                    if (r.Active) active.Add(r);
                return active;
            }
        }

        // Harvester reference
        public VineHarvester Harvester { get; set; }

        private MeshInstance3D _groundMesh;
        public ShaderMaterial GroundShaderMat { get; private set; }

        // Track all terrain decoration nodes (walls, elevated, props) by grid position
        // so ConversionDome can re-theme ones inside the dome radius.
        private readonly Dictionary<Vector2I, Node3D> _terrainDecorNodes = new();
        public IReadOnlyDictionary<Vector2I, Node3D> TerrainDecorNodes => _terrainDecorNodes;

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
                || cell == VineCellType.Channel || cell == VineCellType.DataStream
                || cell == VineCellType.Hazard || cell == VineCellType.ResourceNode)
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
            var cell = _cells[x, y];
            return cell == VineCellType.Empty || cell == VineCellType.Elevated;
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
            node.GlobalPosition = GridToWorld(pos) + new Vector3(0, Constants.NODE_ORIGIN_HEIGHT, 0);

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
            ReplaceDecor(new Vector2I(x, y), wallNode);

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
            ReplaceDecor(new Vector2I(x, y), elevNode);

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
            ReplaceDecor(new Vector2I(x, y), channelNode);

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
            ReplaceDecor(new Vector2I(x, y), streamNode);

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

        // ── Phase5-MapDesign: new terrain visuals ──

        public void SetHazardCell(int x, int y, HazardType type = HazardType.Acid)
        {
            if (!InBounds(x, y)) return;
            _cells[x, y] = VineCellType.Hazard;
            _hazardTypes[new Vector2I(x, y)] = type;

            float cs = Constants.VINE_CELL_SIZE;
            var pos = GridToWorld(x, y);
            var hazNode = new Node3D();
            hazNode.Position = pos;
            AddChild(hazNode);
            ReplaceDecor(new Vector2I(x, y), hazNode);

            // Glowing pool on ground
            var color = type switch
            {
                HazardType.Acid => new Color(0.2f, 0.9f, 0.15f),
                HazardType.Lava => new Color(1f, 0.4f, 0.05f),
                HazardType.Electric => new Color(0.3f, 0.6f, 1f),
                _ => new Color(0.2f, 0.9f, 0.15f)
            };

            // A pool draped on the cell's four corner heights (a flat box sank into or floated
            // over sloped ground), glowing and slowly churning so it reads as a hazard. The
            // material never set EmissionEnabled, so pools were dull translucent squares.
            var mat = new ShaderMaterial { Shader = HazardShader };
            mat.SetShaderParameter("color", new Vector3(color.R, color.G, color.B));
            mat.SetShaderParameter("energy", type == HazardType.Electric ? 1.0f : 1.3f);
            mat.SetShaderParameter("speed", type == HazardType.Electric ? 1.6f : 0.35f);
            var pool = new MeshInstance3D
            {
                Mesh = BuildCellQuad(x, y, 0.06f, 0.04f),
                MaterialOverride = mat,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                // Marks it see-through for the Conversion Dome, which would paint it dark metal
                Transparency = 0.01f,
            };
            pool.Position = -pos; // mesh is in grid space; hazNode sits at the cell centre
            hazNode.AddChild(pool);
        }

        private static Shader _hazardShader;
        private static Shader HazardShader => _hazardShader ??= new Shader
        {
            Code = @"
shader_type spatial;
render_mode cull_disabled, depth_draw_never, shadows_disabled;
uniform vec3 color : source_color = vec3(1.0, 0.4, 0.05);
uniform float energy = 1.3;
uniform float speed = 0.35;
varying vec3 wp;
void vertex() { wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
    vec2 i = floor(p), f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i + vec2(1, 0)), u.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), u.x), u.y);
}
void fragment() {
    float e = min(min(UV.x, 1.0 - UV.x), min(UV.y, 1.0 - UV.y));
    float edge = smoothstep(0.0, 0.16, e);
    float n = noise(wp.xz * 1.6 + vec2(TIME * speed, TIME * speed * 0.7)) * 0.65
            + noise(wp.xz * 4.2 - vec2(TIME * speed * 1.3)) * 0.35;
    ALBEDO = color * (0.18 + 0.45 * n);
    EMISSION = color * energy * (0.35 + 0.9 * n * n);
    ROUGHNESS = 0.25;
    ALPHA = edge * (0.7 + 0.25 * n);
}
"
        };

        /// <summary>A quad over one cell following its corner heights, in grid space.</summary>
        private ArrayMesh BuildCellQuad(int x, int y, float inset, float lift)
        {
            float cs = Constants.VINE_CELL_SIZE;
            float H(float gx, float gy)
            {
                // Bilinear height inside the cell
                float fx = gx - x, fy = gy - y;
                float h00 = _heightmap[x, y], h10 = _heightmap[x + 1, y];
                float h01 = _heightmap[x, y + 1], h11 = _heightmap[x + 1, y + 1];
                return Mathf.Lerp(Mathf.Lerp(h00, h10, fx), Mathf.Lerp(h01, h11, fx), fy) + lift;
            }
            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);
            const int n = 4; // subdivisions so the pool bends with the terrain
            float a = inset / cs;
            for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
            {
                float u0 = a + (1 - 2 * a) * i / n, u1 = a + (1 - 2 * a) * (i + 1) / n;
                float v0 = a + (1 - 2 * a) * j / n, v1 = a + (1 - 2 * a) * (j + 1) / n;
                Vector3 P(float u, float v) => new((x + u) * cs, H(x + u, y + v), (y + v) * cs);
                Vector2 T(float u, float v) => new((u - a) / (1 - 2 * a), (v - a) / (1 - 2 * a));
                void Add(float u, float v) { st.SetUV(T(u, v)); st.AddVertex(P(u, v)); }
                Add(u0, v0); Add(u1, v0); Add(u0, v1);
                Add(u1, v0); Add(u1, v1); Add(u0, v1);
            }
            st.GenerateNormals();
            return st.Commit();
        }

        public void SetPit(int x, int y)
        {
            if (!InBounds(x, y)) return;
            _cells[x, y] = VineCellType.Pit;

            float cs = Constants.VINE_CELL_SIZE;
            var pos = GridToWorld(x, y);
            var pitNode = new Node3D();
            pitNode.Position = pos;
            AddChild(pitNode);
            ReplaceDecor(new Vector2I(x, y), pitNode);

            // Dark recessed pit
            var mat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.05f, 0.05f, 0.08f),
                Roughness = 1f
            };
            var hole = MakeMeshNode(new BoxMesh { Size = new Vector3(cs * 0.85f, 0.6f, cs * 0.85f) }, mat);
            hole.Position = new Vector3(0, -0.3f, 0);
            pitNode.AddChild(hole);
        }

        public void SetDestructibleWallVisual(int x, int y)
        {
            if (!InBounds(x, y)) return;
            // Cell type set by SetDestructibleWall()
            float cs = Constants.VINE_CELL_SIZE;
            var pos = GridToWorld(x, y);
            var dwNode = new Node3D();
            dwNode.Position = pos;
            AddChild(dwNode);
            ReplaceDecor(new Vector2I(x, y), dwNode);

            // Cracked wall — visually distinct from solid walls
            var bodyMat = GetTerrainBodyMaterial();
            var wall = MakeMeshNode(new BoxMesh { Size = new Vector3(cs * 0.8f, 0.7f, cs * 0.8f) }, bodyMat);
            wall.Position = new Vector3(0, 0.35f, 0);
            dwNode.AddChild(wall);

            // Crack lines — thin bright strip to signal breakability
            var crackColor = IsScrapyard ? new Color(0.8f, 0.5f, 0.2f) : new Color(0.8f, 0.2f, 0.2f);
            var crackMat = new StandardMaterial3D
            {
                AlbedoColor = crackColor,
                Emission = crackColor,
                EmissionEnergyMultiplier = 0.8f
            };
            var crack = MakeMeshNode(new BoxMesh { Size = new Vector3(cs * 0.82f, 0.02f, 0.05f) }, crackMat);
            crack.Position = new Vector3(0, 0.4f, 0);
            dwNode.AddChild(crack);
        }

        public void SetResourceNodeCell(int x, int y)
        {
            if (!InBounds(x, y)) return;
            _cells[x, y] = VineCellType.ResourceNode;

            float cs = Constants.VINE_CELL_SIZE;
            var pos = GridToWorld(x, y);
            var rnNode = new Node3D();
            rnNode.Position = pos;
            AddChild(rnNode);
            ReplaceDecor(new Vector2I(x, y), rnNode);

            // Glowing resource crystal
            var crystalColor = new Color(1f, 0.85f, 0.2f);
            var mat = new StandardMaterial3D
            {
                AlbedoColor = crystalColor,
                Emission = crystalColor,
                EmissionEnergyMultiplier = 1.2f
            };
            var crystal = MakeMeshNode(new CylinderMesh
            {
                TopRadius = 0.1f,
                BottomRadius = cs * 0.25f,
                Height = 0.6f
            }, mat);
            crystal.Position = new Vector3(0, 0.3f, 0);
            crystal.RotationDegrees = new Vector3(0, _terrainRng.RandfRange(0, 90), 0);
            rnNode.AddChild(crystal);
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
                region.Direction = InferDirection(region.Center.X, region.Center.Y);
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

        // ── Shield Wall / Entry Activation ──

        /// <summary>
        /// Infer cardinal direction from a cell position on the grid edge.
        /// </summary>
        public CardinalDirection InferDirection(int x, int y)
        {
            if (x == 0) return CardinalDirection.West;
            if (x >= Width - 1) return CardinalDirection.East;
            if (y == 0) return CardinalDirection.North;
            if (y >= Height - 1) return CardinalDirection.South;
            // Interior cell — pick closest edge
            int distW = x, distE = Width - 1 - x, distN = y, distS = Height - 1 - y;
            int min = Mathf.Min(Mathf.Min(distW, distE), Mathf.Min(distN, distS));
            if (min == distW) return CardinalDirection.West;
            if (min == distE) return CardinalDirection.East;
            if (min == distN) return CardinalDirection.North;
            return CardinalDirection.South;
        }

        /// <summary>
        /// Activate a dormant entry region — converts cells from Wall to Entry,
        /// adds to entry points, and triggers path recalculation.
        /// Called by ShieldWallManager when a wall collapses.
        /// </summary>
        public void ActivateEntryRegion(int regionIndex)
        {
            if (regionIndex < 0 || regionIndex >= _entryRegions.Count) return;
            var region = _entryRegions[regionIndex];
            if (region.Active) return;

            region.Active = true;
            foreach (var cell in region.Cells)
            {
                if (InBounds(cell) && _cells[cell.X, cell.Y] == VineCellType.Wall)
                    _cells[cell.X, cell.Y] = VineCellType.Entry;
            }
            _entryPoints.Add(region.Center);
            GameEvents.OnVinePathRecalculated?.Invoke();
        }

        /// <summary>
        /// Deactivate an entry region — converts cells from Entry to Wall,
        /// removes from entry points. Used if walls are rebuilt or for testing.
        /// </summary>
        public void DeactivateEntryRegion(int regionIndex)
        {
            if (regionIndex < 0 || regionIndex >= _entryRegions.Count) return;
            var region = _entryRegions[regionIndex];
            if (!region.Active) return;

            region.Active = false;
            foreach (var cell in region.Cells)
            {
                if (InBounds(cell) && _cells[cell.X, cell.Y] == VineCellType.Entry)
                    _cells[cell.X, cell.Y] = VineCellType.Wall;
            }
            _entryPoints.Remove(region.Center);
            GameEvents.OnVinePathRecalculated?.Invoke();
        }

        // ── Phase5-MapDesign: hazard, destructible wall, resource node, terrain mutation ──

        // Per-cell data for new terrain types
        private readonly Dictionary<Vector2I, HazardType> _hazardTypes = new();
        private readonly Dictionary<Vector2I, float> _destructibleWallHP = new();
        private readonly HashSet<Vector2I> _capturedResourceNodes = new();

        public HazardType GetHazardType(Vector2I pos) =>
            _hazardTypes.TryGetValue(pos, out var h) ? h : HazardType.Acid;

        public void SetHazardType(Vector2I pos, HazardType type) => _hazardTypes[pos] = type;

        /// <summary>Is this cell elevated terrain? Used by towers for range bonus.</summary>
        public bool IsElevated(Vector2I pos) =>
            InBounds(pos) && (_cells[pos.X, pos.Y] == VineCellType.Elevated);

        /// <summary>Is this cell a resource node?</summary>
        public bool IsResourceNode(Vector2I pos) =>
            InBounds(pos) && _cells[pos.X, pos.Y] == VineCellType.ResourceNode;

        /// <summary>Check if a resource node is captured (tower in adjacent cell).</summary>
        public bool IsResourceNodeCaptured(Vector2I pos) => _capturedResourceNodes.Contains(pos);

        public void SetResourceNodeCaptured(Vector2I pos, bool captured)
        {
            if (captured) _capturedResourceNodes.Add(pos);
            else _capturedResourceNodes.Remove(pos);
        }

        /// <summary>Get all resource node positions.</summary>
        public List<Vector2I> GetResourceNodes()
        {
            var nodes = new List<Vector2I>();
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    if (_cells[x, y] == VineCellType.ResourceNode)
                        nodes.Add(new Vector2I(x, y));
            return nodes;
        }

        /// <summary>Initialize a destructible wall at a cell.</summary>
        public void SetDestructibleWall(int x, int y, float hp = -1)
        {
            if (!InBounds(x, y)) return;
            _cells[x, y] = VineCellType.DestructibleWall;
            _destructibleWallHP[new Vector2I(x, y)] = hp < 0 ? Constants.DESTRUCTIBLE_WALL_HP : hp;
        }

        /// <summary>Damage a destructible wall. Returns true if it broke.</summary>
        public bool DamageDestructibleWall(Vector2I pos, float damage)
        {
            if (!_destructibleWallHP.ContainsKey(pos)) return false;
            _destructibleWallHP[pos] -= damage;
            if (_destructibleWallHP[pos] <= 0)
            {
                _destructibleWallHP.Remove(pos);
                _cells[pos.X, pos.Y] = VineCellType.Empty;
                RemoveDecor(pos); // broken wall mesh used to stay standing
                GameEvents.OnDestructibleWallBroken?.Invoke(pos);
                GameEvents.OnTerrainChanged?.Invoke(pos);
                GameEvents.OnVinePathRecalculated?.Invoke();
                GD.Print($"[VineGrid] Destructible wall broken at ({pos.X},{pos.Y})");
                return true;
            }
            return false;
        }

        public float GetDestructibleWallHP(Vector2I pos) =>
            _destructibleWallHP.TryGetValue(pos, out var hp) ? hp : 0;

        /// <summary>
        /// Mutate terrain at a cell — used by milestone events.
        /// Converts cell type, fires events, triggers repath.
        /// </summary>
        /// <summary>
        /// Destroy the node on a cell because the terrain under it changed (not a sale — no
        /// refund, fires OnVineNodeDestroyed). Removes its vine connections too; leaving them
        /// behind orphaned VineConnection visuals and neighbour slot counts.
        /// </summary>
        public void DestroyNodeAt(Vector2I pos)
        {
            if (!InBounds(pos)) return;
            var node = _nodes[pos.X, pos.Y];
            if (node == null) return;

            foreach (var dir in Directions)
                RemoveConnection(pos, pos + dir);

            _nodes[pos.X, pos.Y] = null;
            if (_cells[pos.X, pos.Y] == VineCellType.Node)
                _cells[pos.X, pos.Y] = VineCellType.Empty;
            GameEvents.OnVineNodeDestroyed?.Invoke(node);
            node.QueueFree();
        }

        public void MutateCell(Vector2I pos, VineCellType newType)
        {
            if (!InBounds(pos)) return;
            var oldType = _cells[pos.X, pos.Y];
            if (oldType == newType) return;
            if (oldType == VineCellType.Entry || oldType == VineCellType.Exit)
            {
                GD.PushWarning($"[VineGrid] Refusing to mutate {oldType} cell at ({pos.X},{pos.Y})");
                return;
            }

            // Remove any node on the cell if converting to non-placeable
            if (_nodes[pos.X, pos.Y] != null && newType != VineCellType.Node)
                DestroyNodeAt(pos);

            // Rebuild the cell's visuals for its new type (previously only the type changed,
            // so a collapsed wall stayed on screen and a new pit was invisible)
            ClearCell(pos.X, pos.Y);
            switch (newType)
            {
                case VineCellType.Empty: break;
                case VineCellType.Wall: SetWall(pos.X, pos.Y); break;
                case VineCellType.Pit: SetPit(pos.X, pos.Y); break;
                case VineCellType.Elevated: SetElevated(pos.X, pos.Y); break;
                case VineCellType.DataStream: SetDataStream(pos.X, pos.Y); break;
                case VineCellType.Channel: SetChannel(pos.X, pos.Y); break;
                case VineCellType.ResourceNode: SetResourceNodeCell(pos.X, pos.Y); break;
                case VineCellType.Hazard: SetHazardCell(pos.X, pos.Y); break;
                case VineCellType.DestructibleWall:
                    SetDestructibleWall(pos.X, pos.Y);
                    SetDestructibleWallVisual(pos.X, pos.Y);
                    break;
                default: _cells[pos.X, pos.Y] = newType; break;
            }
            GameEvents.OnTerrainMutated?.Invoke(pos, newType);
            GameEvents.OnTerrainChanged?.Invoke(pos);
            GameEvents.OnVinePathRecalculated?.Invoke();
            GD.Print($"[VineGrid] Terrain mutated at ({pos.X},{pos.Y}): {oldType} → {newType}");
        }

        // ── Editor operations ──

        /// <summary>
        /// Clear a cell back to empty (for level editor erasing).
        /// </summary>
        public void ClearCell(int x, int y)
        {
            if (!InBounds(x, y)) return;
            var pos = new Vector2I(x, y);
            if (_nodes[x, y] != null) DestroyNodeAt(pos);
            _cells[x, y] = VineCellType.Empty;
            // Visuals and per-cell data used to linger (revealed walls stayed on screen)
            RemoveDecor(pos);
            _hazardTypes.Remove(pos);
            _destructibleWallHP.Remove(pos);
        }

        /// <summary>Swap the decor mesh on a cell, freeing whatever was there.</summary>
        private void ReplaceDecor(Vector2I pos, Node3D node)
        {
            if (_terrainDecorNodes.TryGetValue(pos, out var old) && old != node && IsInstanceValid(old))
                old.QueueFree();
            _terrainDecorNodes[pos] = node;
        }

        /// <summary>Remove the decor mesh on a cell (if any).</summary>
        public void RemoveDecor(Vector2I pos)
        {
            if (_terrainDecorNodes.TryGetValue(pos, out var old))
            {
                if (IsInstanceValid(old)) old.QueueFree();
                _terrainDecorNodes.Remove(pos);
            }
        }

        /// <summary>
        /// Adjust heightmap corners around a cell by a delta (for height painting).
        /// </summary>
        public void AdjustHeight(int x, int y, float delta)
        {
            if (_heightmap == null || !InBounds(x, y)) return;
            int cx = Mathf.Clamp(x, 0, Width);
            int cy = Mathf.Clamp(y, 0, Height);
            _heightmap[cx, cy] += delta;
            if (cx + 1 <= Width) _heightmap[cx + 1, cy] += delta;
            if (cy + 1 <= Height) _heightmap[cx, cy + 1] += delta;
            if (cx + 1 <= Width && cy + 1 <= Height) _heightmap[cx + 1, cy + 1] += delta;
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
            ReplaceDecor(new Vector2I(x, y), propNode);

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

            // Dome-aware ground: blends planet terrain → BIT white inside dome
            if (IsScrapyard)
            {
                var scrapGround = ScrapyardEnvironment.GetGroundMaterial();
                GroundShaderMat = BitPalette.MakeDomeGroundMaterial(
                    scrapGround.AlbedoColor,
                    scrapGround.Roughness, 0.1f,
                    scrapGround.AlbedoTexture,
                    scrapGround.Uv1Scale,
                    showGrid: false,  // No grid lines on Scrapyard
                    planetNormal: scrapGround.NormalEnabled ? scrapGround.NormalTexture : null);
                // Texture laid out by world position so BuildOuterGround continues it past the edge
                GroundShaderMat.SetShaderParameter("tex_world_size", new Vector2(Width * cs, Height * cs));
            }
            else
            {
                GroundShaderMat = BitPalette.MakeDomeGroundMaterial(
                    TronTheme.GroundBase, 0.85f, 0.3f);
            }
            _groundMesh.MaterialOverride = GroundShaderMat;

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

            // Terrain mutations rebuild the field with a new material; keep the outer ground in step
            if (_outerGround != null && IsInstanceValid(_outerGround))
            {
                var parent = _outerGround.GetParent();
                _outerGround.QueueFree();
                _outerGround = null;
                var rebuilt = BuildOuterGround(_outerApron, _outerGroundY, _outerExtent);
                if (rebuilt != null) parent?.AddChild(rebuilt);
            }
        }

        private MeshInstance3D _outerGround;
        private int _outerApron;
        private float _outerGroundY, _outerExtent;

        /// <summary>
        /// Ground past the field's edge, for planets whose world carries on around it (Scrapyard).
        /// An apron eases the field's edge heights down to <paramref name="groundY"/> over
        /// <paramref name="apronCells"/> cells, then flat ground runs out to <paramref name="extent"/>
        /// from the field centre. It never overlaps the field, so a flat plane no longer cuts
        /// through the terrain's valleys, and it shares GroundShaderMat so the texture and the
        /// dome takeover continue across the edge. No collision: nothing is placed out here.
        /// </summary>
        public MeshInstance3D BuildOuterGround(int apronCells, float groundY, float extent)
        {
            if (_heightmap == null || apronCells < 1) return null;
            float cs = Constants.VINE_CELL_SIZE;
            float w = Width * cs, h = Height * cs;

            float VertexHeight(int i, int j)
            {
                int ci = Mathf.Clamp(i, 0, Width), cj = Mathf.Clamp(j, 0, Height);
                float edge = _heightmap[ci, cj];
                float d = new Vector2(i - ci, j - cj).Length();
                if (d <= 0f) return edge;
                float t = Mathf.SmoothStep(0f, apronCells, d);
                return Mathf.Lerp(edge, groundY, t);
            }

            Vector3 V(int i, int j) => new(i * cs, VertexHeight(i, j), j * cs);

            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);

            void Quad(Vector3 v00, Vector3 v10, Vector3 v01, Vector3 v11)
            {
                // Same winding as the field mesh
                foreach (var v in new[] { v00, v10, v01, v10, v11, v01 })
                {
                    st.SetUV(new Vector2(v.X / w, v.Z / h));
                    st.AddVertex(v);
                }
            }

            int a = apronCells;
            for (int i = -a; i < Width + a; i++)
            for (int j = -a; j < Height + a; j++)
            {
                if (i >= 0 && i < Width && j >= 0 && j < Height) continue; // the field itself
                Quad(V(i, j), V(i + 1, j), V(i, j + 1), V(i + 1, j + 1));
            }

            // Flat ground beyond the apron: four strips framing it
            float cx = w / 2f, cz = h / 2f;
            float x0 = cx - extent, x1 = cx + extent, z0 = cz - extent, z1 = cz + extent;
            float ax0 = -a * cs, ax1 = w + a * cs, az0 = -a * cs, az1 = h + a * cs;
            Vector3 P(float x, float z) => new(x, groundY, z);
            Quad(P(x0, z0), P(x1, z0), P(x0, az0), P(x1, az0));     // far side
            Quad(P(x0, az1), P(x1, az1), P(x0, z1), P(x1, z1));     // near side
            Quad(P(x0, az0), P(ax0, az0), P(x0, az1), P(ax0, az1)); // left
            Quad(P(ax1, az0), P(x1, az0), P(ax1, az1), P(x1, az1)); // right

            st.GenerateNormals();
            var outer = new MeshInstance3D
            {
                Name = "OuterGround",
                Mesh = st.Commit(),
                MaterialOverride = GroundShaderMat,
            };
            _outerGround = outer;
            _outerApron = apronCells; _outerGroundY = groundY; _outerExtent = extent;
            return outer;
        }

        public void BuildGridLines()
        {
            var gridVisual = new MeshInstance3D();
            gridVisual.Name = "GridLines";
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
