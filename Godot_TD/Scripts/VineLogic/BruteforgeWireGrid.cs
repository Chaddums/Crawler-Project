using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Manages the Bruteforge's wire network. Towers must connect back to the Forge
    /// via wire connections to be powered. Power nodes can be added to inject magic
    /// into the circuit. Effects propagate based on connection hop count.
    /// Leverages the existing VineGrid connection system.
    /// </summary>
    public partial class BruteforgeWireGrid : Node3D
    {
        /// <summary>
        /// Tracks a node on the wire network and its power/magic state.
        /// </summary>
        public class WireNode
        {
            public Vector2I GridPos;
            public VineNode Tower;
            public bool IsPowerNode;
            public MaterialType? MagicInfusion;
            public bool IsConnectedToForge;
            public int HopsFromForge = -1; // -1 = not connected
            public MeshInstance3D PowerGlow;
        }

        private readonly Dictionary<Vector2I, WireNode> _wireNodes = new();
        private Vector2I _forgeCell;
        private bool _forgeRegistered;
        private VineGrid _grid;
        private int _defaultPropagationRange = 4;

        // Visual: wire connections colored by power state
        private readonly Dictionary<(Vector2I, Vector2I), MeshInstance3D> _wireVisuals = new();

        private static readonly Vector2I[] CardinalDirs = {
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1)
        };

        public void Initialize(Vector2I forgeCell, int propagationRange)
        {
            _forgeCell = forgeCell;
            _forgeRegistered = true;
            _defaultPropagationRange = propagationRange;
            _grid = ServiceLocator.Get<VineGrid>();

            // Register forge cell as a wire node (the root)
            var forgeNode = new WireNode
            {
                GridPos = forgeCell,
                IsConnectedToForge = true,
                HopsFromForge = 0
            };
            _wireNodes[forgeCell] = forgeNode;

            ServiceLocator.Register(this);
            GameEvents.OnVineNodePlaced += OnNodePlaced;
            GameEvents.OnVineNodeSold += OnNodeRemoved;

            GD.Print($"[BruteforgeWireGrid] Initialized at forge cell ({forgeCell.X}, {forgeCell.Y}), propagation range: {propagationRange}");
        }

        public override void _ExitTree()
        {
            GameEvents.OnVineNodePlaced -= OnNodePlaced;
            GameEvents.OnVineNodeSold -= OnNodeRemoved;
            ServiceLocator.Unregister<BruteforgeWireGrid>();
        }

        private void OnNodePlaced(Node node)
        {
            if (node is VineNode vine)
            {
                RegisterNode(vine);
                RecalculateConnectivity();
            }
        }

        private void OnNodeRemoved(Node node)
        {
            if (node is VineNode vine)
            {
                UnregisterNode(vine.GridPosition);
                RecalculateConnectivity();
            }
        }

        // ── Node Registration ──

        public void RegisterNode(VineNode tower)
        {
            var pos = tower.GridPosition;
            if (_wireNodes.ContainsKey(pos)) return;

            var wireNode = new WireNode
            {
                GridPos = pos,
                Tower = tower
            };
            _wireNodes[pos] = wireNode;
        }

        public void UnregisterNode(Vector2I pos)
        {
            if (!_wireNodes.TryGetValue(pos, out var node)) return;
            if (node.PowerGlow != null && IsInstanceValid(node.PowerGlow))
                node.PowerGlow.QueueFree();
            _wireNodes.Remove(pos);
        }

        // ── Power Node Management ──

        /// <summary>
        /// Mark a node as a power node, optionally infusing it with magic.
        /// Power nodes inject magic effects into the wire circuit.
        /// </summary>
        public void SetPowerNode(Vector2I pos, MaterialType magic)
        {
            if (!_wireNodes.TryGetValue(pos, out var node)) return;
            node.IsPowerNode = true;
            node.MagicInfusion = magic;
            BuildPowerGlow(node);
            RecalculateConnectivity();
            GD.Print($"[BruteforgeWireGrid] Power node set at ({pos.X}, {pos.Y}) with {magic} magic");
        }

        private void BuildPowerGlow(WireNode node)
        {
            if (node.PowerGlow != null && IsInstanceValid(node.PowerGlow))
                node.PowerGlow.QueueFree();

            var glow = new MeshInstance3D();
            var torus = new TorusMesh();
            torus.InnerRadius = 0.35f;
            torus.OuterRadius = 0.5f;
            torus.Rings = 16;
            torus.RingSegments = 12;
            glow.Mesh = torus;
            glow.Position = new Vector3(0, 0.15f, 0);

            var color = VineHarvester.GetMaterialColor(node.MagicInfusion ?? MaterialType.Power);
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(color.R, color.G, color.B, 0.5f);
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.EmissionEnabled = true;
            mat.Emission = color;
            mat.EmissionEnergyMultiplier = 1f;
            glow.MaterialOverride = mat;

            if (node.Tower != null && IsInstanceValid(node.Tower))
                node.Tower.AddChild(glow);
            else
                AddChild(glow);

            node.PowerGlow = glow;
        }

        // ── Connectivity ──

        /// <summary>
        /// BFS from the forge cell through vine connections to determine which
        /// nodes are connected and how many hops away they are.
        /// </summary>
        public void RecalculateConnectivity()
        {
            if (!_forgeRegistered || _grid == null) return;

            // Reset all connectivity
            foreach (var node in _wireNodes.Values)
            {
                node.IsConnectedToForge = false;
                node.HopsFromForge = -1;
            }

            // BFS from forge
            var queue = new Queue<(Vector2I pos, int hops)>();
            var visited = new HashSet<Vector2I>();

            if (_wireNodes.TryGetValue(_forgeCell, out var forgeNode))
            {
                forgeNode.IsConnectedToForge = true;
                forgeNode.HopsFromForge = 0;
                queue.Enqueue((_forgeCell, 0));
                visited.Add(_forgeCell);
            }

            while (queue.Count > 0)
            {
                var (pos, hops) = queue.Dequeue();

                // Check cardinal neighbors for connections
                var connections = _grid.GetConnectionsFrom(pos);
                foreach (var conn in connections)
                {
                    var neighbor = conn.GetOtherEnd(pos);
                    if (visited.Contains(neighbor)) continue;
                    visited.Add(neighbor);

                    if (_wireNodes.TryGetValue(neighbor, out var neighborNode))
                    {
                        neighborNode.IsConnectedToForge = true;
                        neighborNode.HopsFromForge = hops + 1;
                        queue.Enqueue((neighbor, hops + 1));
                    }
                }
            }

            // Also check nodes that are adjacent but might not have vine connections yet
            // (they could be placed but not connected via VineGrid)
            foreach (var node in _wireNodes.Values)
            {
                if (node.IsConnectedToForge) continue;

                // Check if any adjacent wire node is connected
                foreach (var dir in CardinalDirs)
                {
                    var neighbor = node.GridPos + dir;
                    if (_wireNodes.TryGetValue(neighbor, out var adj) && adj.IsConnectedToForge)
                    {
                        // There's a connected neighbor — check if VineGrid has a connection
                        var conn = _grid.GetConnection(node.GridPos, neighbor);
                        if (conn != null)
                        {
                            node.IsConnectedToForge = true;
                            node.HopsFromForge = adj.HopsFromForge + 1;
                            break;
                        }
                    }
                }
            }

            UpdateWireVisuals();
        }

        /// <summary>
        /// Check if a position would be connected to the forge if a node were placed there.
        /// Used during placement preview.
        /// </summary>
        public bool WouldBeConnected(Vector2I pos)
        {
            foreach (var dir in CardinalDirs)
            {
                var neighbor = pos + dir;
                if (_wireNodes.TryGetValue(neighbor, out var adj) && adj.IsConnectedToForge)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Check if a node at this position is connected to the forge.
        /// </summary>
        public bool IsConnected(Vector2I pos)
        {
            return _wireNodes.TryGetValue(pos, out var node) && node.IsConnectedToForge;
        }

        // ── Effect Propagation ──

        /// <summary>
        /// Get all nodes within propagation range of a source node.
        /// Returns nodes reachable within 'range' connection hops.
        /// </summary>
        public List<WireNode> GetNodesInPropagationRange(Vector2I source, int range = -1)
        {
            if (range < 0) range = _defaultPropagationRange;
            var result = new List<WireNode>();
            if (_grid == null) return result;

            var queue = new Queue<(Vector2I pos, int hops)>();
            var visited = new HashSet<Vector2I>();

            queue.Enqueue((source, 0));
            visited.Add(source);

            while (queue.Count > 0)
            {
                var (pos, hops) = queue.Dequeue();
                if (hops > range) continue;

                if (_wireNodes.TryGetValue(pos, out var node) && pos != source)
                    result.Add(node);

                if (hops >= range) continue;

                var connections = _grid.GetConnectionsFrom(pos);
                foreach (var conn in connections)
                {
                    var neighbor = conn.GetOtherEnd(pos);
                    if (visited.Contains(neighbor)) continue;
                    visited.Add(neighbor);

                    if (_wireNodes.ContainsKey(neighbor))
                        queue.Enqueue((neighbor, hops + 1));
                }
            }

            return result;
        }

        /// <summary>
        /// Get all magic effects that reach a given node, based on power nodes
        /// within propagation range of it.
        /// </summary>
        public List<(MaterialType magic, int hopsAway)> GetMagicEffectsAt(Vector2I pos)
        {
            var effects = new List<(MaterialType, int)>();
            if (_grid == null) return effects;

            // BFS outward from pos to find power nodes within range
            var queue = new Queue<(Vector2I p, int hops)>();
            var visited = new HashSet<Vector2I>();

            queue.Enqueue((pos, 0));
            visited.Add(pos);

            while (queue.Count > 0)
            {
                var (p, hops) = queue.Dequeue();

                if (_wireNodes.TryGetValue(p, out var node) && node.IsPowerNode && node.MagicInfusion.HasValue)
                    effects.Add((node.MagicInfusion.Value, hops));

                if (hops >= _defaultPropagationRange) continue;

                var connections = _grid.GetConnectionsFrom(p);
                foreach (var conn in connections)
                {
                    var neighbor = conn.GetOtherEnd(p);
                    if (visited.Contains(neighbor)) continue;
                    visited.Add(neighbor);

                    if (_wireNodes.ContainsKey(neighbor))
                        queue.Enqueue((neighbor, hops + 1));
                }
            }

            return effects;
        }

        // ── Wire Visuals ──

        private void UpdateWireVisuals()
        {
            // Color existing VineConnections based on forge connectivity
            if (_grid == null) return;

            foreach (var conn in _grid.GetConnections())
            {
                bool aConnected = _wireNodes.TryGetValue(conn.CellA, out var nodeA) && nodeA.IsConnectedToForge;
                bool bConnected = _wireNodes.TryGetValue(conn.CellB, out var nodeB) && nodeB.IsConnectedToForge;
                bool bothConnected = aConnected && bConnected;

                // Check if either end has magic
                bool hasMagic = (nodeA?.IsPowerNode == true) || (nodeB?.IsPowerNode == true);

                // The VineConnection handles its own visuals via GetConnectionColor,
                // but we want to add a power overlay for forge-connected wires
                var key = SortPair(conn.CellA, conn.CellB);

                if (bothConnected && !_wireVisuals.ContainsKey(key))
                {
                    // Add subtle powered overlay
                    var overlay = BuildPoweredOverlay(conn, hasMagic, nodeA, nodeB);
                    if (overlay != null)
                        _wireVisuals[key] = overlay;
                }
                else if (!bothConnected && _wireVisuals.TryGetValue(key, out var old))
                {
                    if (IsInstanceValid(old)) old.QueueFree();
                    _wireVisuals.Remove(key);
                }
                else if (bothConnected && _wireVisuals.TryGetValue(key, out var existing))
                {
                    // Update color if magic state changed
                    if (existing.MaterialOverride is StandardMaterial3D eMat)
                    {
                        var color = hasMagic
                            ? GetMagicOverlayColor(nodeA, nodeB)
                            : new Color(0.9f, 0.5f, 0.2f, 0.3f);
                        eMat.AlbedoColor = color;
                        eMat.Emission = new Color(color.R, color.G, color.B);
                    }
                }
            }
        }

        private MeshInstance3D BuildPoweredOverlay(VineConnection conn, bool hasMagic,
            WireNode nodeA, WireNode nodeB)
        {
            if (_grid == null) return null;

            var posA = _grid.GridToWorld(conn.CellA) + new Vector3(0, 0.6f, 0);
            var posB = _grid.GridToWorld(conn.CellB) + new Vector3(0, 0.6f, 0);

            var overlay = new MeshInstance3D();
            var direction = posB - posA;
            float dist = direction.Length();

            var box = new BoxMesh();
            float thickness = 0.06f;
            if (Mathf.Abs(direction.X) > Mathf.Abs(direction.Z))
                box.Size = new Vector3(dist, thickness, thickness);
            else
                box.Size = new Vector3(thickness, thickness, dist);

            overlay.Mesh = box;
            overlay.GlobalPosition = posA.Lerp(posB, 0.5f);

            var color = hasMagic
                ? GetMagicOverlayColor(nodeA, nodeB)
                : new Color(0.9f, 0.5f, 0.2f, 0.3f);

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = color;
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.EmissionEnabled = true;
            mat.Emission = new Color(color.R, color.G, color.B);
            mat.EmissionEnergyMultiplier = 0.8f;
            overlay.MaterialOverride = mat;

            AddChild(overlay);
            return overlay;
        }

        private Color GetMagicOverlayColor(WireNode a, WireNode b)
        {
            MaterialType? magic = a?.MagicInfusion ?? b?.MagicInfusion;
            if (!magic.HasValue) return new Color(0.9f, 0.5f, 0.2f, 0.3f);

            var baseColor = VineHarvester.GetMaterialColor(magic.Value);
            return new Color(baseColor.R, baseColor.G, baseColor.B, 0.4f);
        }

        private static (Vector2I, Vector2I) SortPair(Vector2I a, Vector2I b)
        {
            if (a.X < b.X || (a.X == b.X && a.Y < b.Y)) return (a, b);
            return (b, a);
        }

        // ── Queries ──

        public WireNode GetWireNode(Vector2I pos)
            => _wireNodes.TryGetValue(pos, out var node) ? node : null;

        public int GetHopsFromForge(Vector2I pos)
            => _wireNodes.TryGetValue(pos, out var node) ? node.HopsFromForge : -1;

        public bool IsForgeCell(Vector2I pos) => _forgeRegistered && pos == _forgeCell;

        public IEnumerable<WireNode> AllNodes => _wireNodes.Values;
    }
}
