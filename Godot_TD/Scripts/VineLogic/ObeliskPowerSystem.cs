using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Tracks the Obelisk's power radius and all Pylon power radii.
    /// Answers "is this position powered?" for free placement validation.
    /// </summary>
    public partial class ObeliskPowerSystem : Node3D
    {
        private Vector3 _obeliskPosition;
        private float _obeliskRadius;
        public float PylonRadius { get; private set; }
        private readonly List<ObeliskPylon> _pylons = new();
        private readonly List<Node3D> _placedNodes = new();

        // Visual ring for the Obelisk's power radius
        private MeshInstance3D _radiusRing;

        public void Initialize(Vector3 obeliskPos, float baseRadius, float pylonRadius)
        {
            _obeliskPosition = obeliskPos;
            _obeliskRadius = baseRadius;
            PylonRadius = pylonRadius;

            BuildRadiusVisual();

            ServiceLocator.Register(this);

            // Show/hide radius visuals based on game phase
            GameEvents.OnPhaseChanged += phase =>
                SetVisualsVisible(phase == GamePhase.Build);

            GD.Print($"[ObeliskPower] Initialized — radius {baseRadius}, pylon radius {pylonRadius}");
        }

        public void UpdateObeliskPosition(Vector3 pos)
        {
            _obeliskPosition = pos;
            if (_radiusRing != null)
                _radiusRing.GlobalPosition = new Vector3(pos.X, 0.05f, pos.Z);
        }

        /// <summary>
        /// Check if a world position is within any power source's radius.
        /// </summary>
        public bool IsPositionPowered(Vector3 worldPos)
        {
            // Check Obelisk base radius
            float distSq = FlatDistanceSq(worldPos, _obeliskPosition);
            if (distSq <= _obeliskRadius * _obeliskRadius)
                return true;

            // Check each pylon
            foreach (var pylon in _pylons)
            {
                if (!IsInstanceValid(pylon)) continue;
                float pylonDistSq = FlatDistanceSq(worldPos, pylon.GlobalPosition);
                if (pylonDistSq <= pylon.PowerRadius * pylon.PowerRadius)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Get all pylons whose radius covers a position (for magic stacking).
        /// </summary>
        public List<ObeliskPylon> GetPylonsCovering(Vector3 worldPos)
        {
            var result = new List<ObeliskPylon>();
            foreach (var pylon in _pylons)
            {
                if (!IsInstanceValid(pylon)) continue;
                float distSq = FlatDistanceSq(worldPos, pylon.GlobalPosition);
                if (distSq <= pylon.PowerRadius * pylon.PowerRadius)
                    result.Add(pylon);
            }
            return result;
        }

        public void RegisterPylon(ObeliskPylon pylon)
        {
            _pylons.Add(pylon);
            GD.Print($"[ObeliskPower] Pylon registered at {pylon.GlobalPosition}");
        }

        public void UnregisterPylon(ObeliskPylon pylon)
        {
            _pylons.Remove(pylon);
        }

        public void RegisterPlacedNode(Node3D node)
        {
            _placedNodes.Add(node);
        }

        public void UnregisterPlacedNode(Node3D node)
        {
            _placedNodes.Remove(node);
        }

        /// <summary>
        /// Check if a position has minimum clearance from existing placed nodes.
        /// </summary>
        public bool HasClearance(Vector3 worldPos, float minDistance = 1.5f)
        {
            float minDistSq = minDistance * minDistance;
            foreach (var node in _placedNodes)
            {
                if (!IsInstanceValid(node)) continue;
                if (FlatDistanceSq(worldPos, node.GlobalPosition) < minDistSq)
                    return false;
            }
            return true;
        }

        private static float FlatDistanceSq(Vector3 a, Vector3 b)
        {
            float dx = a.X - b.X;
            float dz = a.Z - b.Z;
            return dx * dx + dz * dz;
        }

        private void BuildRadiusVisual()
        {
            _radiusRing = new MeshInstance3D();
            var torus = new TorusMesh();
            torus.InnerRadius = _obeliskRadius - 0.15f;
            torus.OuterRadius = _obeliskRadius + 0.15f;
            torus.Rings = 48;
            torus.RingSegments = 12;
            _radiusRing.Mesh = torus;
            _radiusRing.Rotation = new Vector3(Mathf.Pi * 0.5f, 0, 0);
            _radiusRing.GlobalPosition = new Vector3(_obeliskPosition.X, 0.05f, _obeliskPosition.Z);

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.3f, 0.5f, 1.0f, 0.25f);
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.EmissionEnabled = true;
            mat.Emission = new Color(0.4f, 0.6f, 1.0f);
            mat.EmissionEnergyMultiplier = 0.3f;
            _radiusRing.MaterialOverride = mat;

            AddChild(_radiusRing);
        }

        /// <summary>
        /// Show/hide radius visuals (show during Build phase, hide during Wave).
        /// </summary>
        public void SetVisualsVisible(bool visible)
        {
            if (_radiusRing != null)
                _radiusRing.Visible = visible;

            foreach (var pylon in _pylons)
            {
                if (IsInstanceValid(pylon))
                    pylon.SetRadiusVisible(visible);
            }
        }

        public override void _ExitTree()
        {
            ServiceLocator.Unregister<ObeliskPowerSystem>();
        }
    }
}
