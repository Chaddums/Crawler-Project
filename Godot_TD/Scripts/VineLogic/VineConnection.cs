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
        }

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
            _vineMesh.GlobalPosition = posA.Lerp(posB, 0.5f);

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = lineColor;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.EmissionEnabled = true;
            mat.Emission = lineColor * 0.5f;
            _vineMesh.MaterialOverride = mat;

            AddChild(_vineMesh);

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
                arrowMesh.GlobalPosition = arrowPos;

                var arrowMat = new StandardMaterial3D();
                arrowMat.AlbedoColor = lineColor.Lightened(0.3f);
                arrowMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                arrowMat.EmissionEnabled = true;
                arrowMat.Emission = lineColor;
                arrowMat.EmissionEnergyMultiplier = 1.5f;
                arrowMesh.MaterialOverride = arrowMat;
                AddChild(arrowMesh);
            }
        }

        private static Color GetConnectionColor(VineNode a, VineNode b)
        {
            // Sensor → anything: green (signal source)
            if (a?.Data?.Category == VineNodeCategory.Sensor ||
                b?.Data?.Category == VineNodeCategory.Sensor)
                return new Color(0.3f, 0.8f, 0.4f, 0.9f);

            // Anything → Effect: orange (signal destination)
            if (a?.Data?.Category == VineNodeCategory.Effect ||
                b?.Data?.Category == VineNodeCategory.Effect)
                return new Color(0.9f, 0.6f, 0.2f, 0.9f);

            // Route ↔ Route: blue (signal passthrough)
            return new Color(0.4f, 0.6f, 0.9f, 0.9f);
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
                mat.EmissionEnergyMultiplier = 2f;
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
