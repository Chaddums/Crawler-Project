using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// A vine (cable/pipe) connecting two adjacent nodes on the grid.
    /// Signals travel along connections. Visually rendered as a line between nodes.
    /// </summary>
    public partial class VineConnection : Node3D
    {
        public Vector2I CellA { get; private set; }
        public Vector2I CellB { get; private set; }

        private VineGrid _grid;
        private MeshInstance3D _vineMesh;
        private readonly List<TravelingSignal> _signals = new();

        // Visual: signal pulse dots traveling along the vine
        private readonly List<MeshInstance3D> _pulseDots = new();

        public VineConnection() { }

        public VineConnection(Vector2I a, Vector2I b, VineGrid grid)
        {
            CellA = a;
            CellB = b;
            _grid = grid;
        }

        public override void _Ready()
        {
            // Defer visual build so GlobalPosition is available
            CallDeferred(nameof(BuildVisual));

            // Rebuild visuals when the network changes (power status may update)
            GameEvents.OnVineNodePlaced += OnNetworkChanged;
            GameEvents.OnVineNodeSold += OnNetworkChanged;
        }

        public override void _ExitTree()
        {
            GameEvents.OnVineNodePlaced -= OnNetworkChanged;
            GameEvents.OnVineNodeSold -= OnNetworkChanged;
        }

        private void OnNetworkChanged(Node _) => CallDeferred(nameof(RebuildVisual));

        /// <summary>
        /// Inject a signal at one end of this connection, traveling toward the other end.
        /// </summary>
        public void InjectSignal(Vector2I fromCell, SignalType type, float strength = 1f)
        {
            var dest = fromCell == CellA ? CellB : CellA;
            _signals.Add(new TravelingSignal {
                Type = type,
                Strength = strength,
                Progress = 0f,
                DestinationCell = dest
            });
        }

        public override void _Process(double delta)
        {
            for (int i = _signals.Count - 1; i >= 0; i--)
            {
                var sig = _signals[i];
                sig.Progress += SignalTuningEditor.SignalTravelSpeed * (float)delta /
                    (Constants.VINE_CELL_SIZE * 1f); // Normalize to cell distance

                if (sig.Progress >= 1f)
                {
                    // Signal arrived — deliver to destination node
                    var destNode = _grid.GetNode(sig.DestinationCell);
                    destNode?.ReceiveSignal(sig.Type, sig.Strength, sig.DestinationCell == CellA ? CellB : CellA);
                    _signals.RemoveAt(i);
                }
                else
                {
                    _signals[i] = sig;
                }
            }

            UpdatePulseVisuals();
        }

        private void RebuildVisual()
        {
            // Remove old visual children (vine mesh, arrows) but keep pulse dots
            if (_vineMesh != null) { _vineMesh.QueueFree(); _vineMesh = null; }
            // Remove arrow meshes (non-pulse children)
            foreach (var child in GetChildren())
            {
                if (child is MeshInstance3D m && m != _vineMesh && !_pulseDots.Contains(m))
                    m.QueueFree();
            }
            BuildVisual();
        }

        private void BuildVisual()
        {
            if (_grid == null) return;

            var posA = _grid.GridToWorld(CellA) + new Vector3(0, 0.5f, 0);
            var posB = _grid.GridToWorld(CellB) + new Vector3(0, 0.5f, 0);

            // Color-code by what the connection links
            var nodeA = _grid.GetNode(CellA);
            var nodeB = _grid.GetNode(CellB);
            Color lineColor = GetConnectionColor(nodeA, nodeB);

            // Main vine line — use a BoxMesh stretched along the connection axis
            // (avoids cylinder rotation issues)
            _vineMesh = new MeshInstance3D();
            var direction = posB - posA;
            float dist = direction.Length();

            var box = new BoxMesh();
            // Thin box: thick enough to see, stretched along the connection
            float thickness = 0.1f;
            if (Mathf.Abs(direction.X) > Mathf.Abs(direction.Z))
                box.Size = new Vector3(dist, thickness, thickness); // Horizontal (X-axis)
            else
                box.Size = new Vector3(thickness, thickness, dist); // Vertical (Z-axis)

            _vineMesh.Mesh = box;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = lineColor;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.EmissionEnabled = true;
            mat.Emission = lineColor * 0.5f;
            _vineMesh.MaterialOverride = mat;

            AddChild(_vineMesh);
            _vineMesh.GlobalPosition = posA.Lerp(posB, 0.5f);

            // Flow direction indicator — small arrow at 75% mark pointing toward effect/output
            bool aIsSensor = nodeA?.Data?.Category == VineNodeCategory.Sensor;
            bool bIsEffect = nodeB?.Data?.Category == VineNodeCategory.Effect;
            bool showArrow = aIsSensor || bIsEffect ||
                nodeB?.Data?.Category == VineNodeCategory.Sensor ||
                nodeA?.Data?.Category == VineNodeCategory.Effect;

            if (showArrow)
            {
                float t = aIsSensor || !bIsEffect ? 0.7f : 0.3f;
                var arrowPos = posA.Lerp(posB, t);

                // Use a small diamond/sphere as flow indicator (avoids rotation issues)
                var arrowMesh = new MeshInstance3D();
                var sphere = new SphereMesh();
                sphere.Radius = 0.14f;
                sphere.Height = 0.28f;
                arrowMesh.Mesh = sphere;

                var arrowMat = new StandardMaterial3D();
                arrowMat.AlbedoColor = lineColor.Lightened(0.3f);
                arrowMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                arrowMat.EmissionEnabled = true;
                arrowMat.Emission = lineColor;
                arrowMat.EmissionEnergyMultiplier = 1.5f;
                arrowMesh.MaterialOverride = arrowMat;
                AddChild(arrowMesh);
                arrowMesh.GlobalPosition = arrowPos;
            }
        }

        private Color GetConnectionColor(VineNode a, VineNode b)
        {
            var catA = a?.Data?.Category;
            var catB = b?.Data?.Category;

            // Sensor → anything: green (signal source)
            if (catA == VineNodeCategory.Sensor || catB == VineNodeCategory.Sensor)
                return new Color(0.3f, 0.8f, 0.4f, 0.9f);

            // Check if any effect node in this connection is unpowered
            if (_grid != null && (catA == VineNodeCategory.Effect || catB == VineNodeCategory.Effect))
            {
                bool aUnpowered = catA == VineNodeCategory.Effect && !IsEffectPowered(CellA, _grid);
                bool bUnpowered = catB == VineNodeCategory.Effect && !IsEffectPowered(CellB, _grid);
                if (aUnpowered || bUnpowered)
                    return new Color(0.4f, 0.15f, 0.15f, 0.5f); // Dim gray-red = unpowered
            }

            // Effect → Effect: cyan (powered chain — signal flows through)
            if (catA == VineNodeCategory.Effect && catB == VineNodeCategory.Effect)
                return new Color(0.0f, 0.75f, 0.85f, 0.9f);

            // Route → Effect or Effect → Route: orange (signal reaching destination)
            if (catA == VineNodeCategory.Effect || catB == VineNodeCategory.Effect)
                return new Color(0.9f, 0.6f, 0.2f, 0.9f);

            // Route ↔ Route: blue (signal passthrough)
            return new Color(0.4f, 0.6f, 0.9f, 0.9f);
        }

        /// <summary>
        /// BFS backward from an effect cell through connections toward sensors.
        /// Counts effect-node hops. Returns true if a sensor with sufficient SignalPower is reachable.
        /// </summary>
        private static bool IsEffectPowered(Vector2I cell, VineGrid grid)
        {
            var startNode = grid.GetNode(cell);
            if (startNode?.Data == null) return true; // No node = don't flag

            // BFS: (cell, effectDepth) — depth counts effect nodes traversed (including start)
            var queue = new Queue<(Vector2I pos, int depth)>();
            var visited = new HashSet<Vector2I>();

            int startDepth = startNode.Data.Category == VineNodeCategory.Effect ? 1 : 0;
            queue.Enqueue((cell, startDepth));
            visited.Add(cell);

            while (queue.Count > 0)
            {
                var (pos, depth) = queue.Dequeue();
                var connections = grid.GetConnectionsFrom(pos);

                foreach (var conn in connections)
                {
                    var neighbor = conn.GetOtherEnd(pos);
                    if (visited.Contains(neighbor)) continue;
                    visited.Add(neighbor);

                    var neighborNode = grid.GetNode(neighbor);
                    if (neighborNode?.Data == null) continue;

                    if (neighborNode.Data.Category == VineNodeCategory.Sensor)
                    {
                        // Found a sensor — check if it has enough power
                        int power = neighborNode.Data.SignalPower > 0 ? neighborNode.Data.SignalPower : 3;
                        if (depth <= power) return true;
                    }
                    else if (neighborNode.Data.Category == VineNodeCategory.Effect)
                    {
                        queue.Enqueue((neighbor, depth + 1));
                    }
                    else
                    {
                        // Route nodes don't consume power
                        queue.Enqueue((neighbor, depth));
                    }
                }
            }

            return false; // No sensor with enough power found
        }

        private void UpdatePulseVisuals()
        {
            // Reuse or create pulse dots for active signals
            while (_pulseDots.Count < _signals.Count)
            {
                var dot = new MeshInstance3D();
                var sphere = new SphereMesh();
                sphere.Radius = 0.15f;
                sphere.Height = 0.3f;
                dot.Mesh = sphere;
                var mat = new StandardMaterial3D();
                mat.AlbedoColor = new Color(0.9f, 0.8f, 0.2f);
                mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                mat.EmissionEnabled = true;
                mat.Emission = new Color(0.9f, 0.8f, 0.2f);
                mat.EmissionEnergyMultiplier = 0.8f;
                dot.MaterialOverride = mat;
                AddChild(dot);
                _pulseDots.Add(dot);
            }

            // Hide excess dots
            for (int i = 0; i < _pulseDots.Count; i++)
                _pulseDots[i].Visible = i < _signals.Count;

            // Position active dots along the vine
            if (_grid == null) return;
            var posA = _grid.GridToWorld(CellA) + new Vector3(0, 0.5f, 0);
            var posB = _grid.GridToWorld(CellB) + new Vector3(0, 0.5f, 0);

            for (int i = 0; i < _signals.Count; i++)
            {
                var sig = _signals[i];
                var from = sig.DestinationCell == CellB ? posA : posB;
                var to = sig.DestinationCell == CellB ? posB : posA;
                _pulseDots[i].GlobalPosition = from.Lerp(to, sig.Progress);

                // Color by signal type
                if (_pulseDots[i].MaterialOverride is StandardMaterial3D pmat)
                {
                    pmat.AlbedoColor = sig.Type switch {
                        SignalType.Buff => new Color(0.2f, 0.8f, 0.9f),
                        SignalType.Reset => new Color(0.9f, 0.2f, 0.2f),
                        _ => new Color(0.9f, 0.8f, 0.2f)
                    };
                    pmat.Emission = pmat.AlbedoColor;
                }
            }
        }

        /// <summary>
        /// Get the other end of the connection from the given cell.
        /// </summary>
        public Vector2I GetOtherEnd(Vector2I from) => from == CellA ? CellB : CellA;
    }

    /// <summary>
    /// A signal currently traveling along a vine connection.
    /// </summary>
    public struct TravelingSignal
    {
        public SignalType Type;
        public float Strength;
        public float Progress;          // 0 = at source, 1 = at destination
        public Vector2I DestinationCell;
    }
}
