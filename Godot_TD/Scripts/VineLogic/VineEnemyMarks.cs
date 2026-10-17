using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// A Beacon's mark: for a few seconds every hit lands harder, BIT's hardest of all, and with
    /// Target Lock the marked enemy is slowed too. A red ring over its head while it lasts. Also
    /// how fast the enemy is moving, for towers that lead their shots (the Mortar Pit).
    /// </summary>
    public partial class VineEnemy
    {
        private float _markTimer, _markTaken, _markBitTaken, _markSlow;
        private MeshInstance3D _markRing;
        private Vector3 _velPrev;
        private bool _velKnown;

        public bool IsMarked => _markTimer > 0f;

        /// <summary>Its speed and heading over the last frame (units a second, flat).</summary>
        public Vector3 Velocity { get; private set; }

        /// <summary>Every hit times this while marked (BIT's own hits take the bigger share).</summary>
        public float MarkTakenMult => _markTimer > 0f ? 1f + (HitSource == "BIT" ? _markBitTaken : _markTaken) : 1f;

        public void Mark(float seconds, float taken, float bitTaken, float slow)
        {
            if (!IsAlive) return;
            bool was = _markTimer > 0f;
            _markTaken = Mathf.Max(was ? _markTaken : 0f, taken);
            _markBitTaken = Mathf.Max(was ? _markBitTaken : 0f, bitTaken);
            _markSlow = Mathf.Max(was ? _markSlow : 0f, slow);
            _markTimer = Mathf.Max(_markTimer, seconds);
            if (_markRing == null || !IsInstanceValid(_markRing))
            {
                _markRing = new MeshInstance3D
                {
                    Name = "BeaconMark",
                    Mesh = new TorusMesh { InnerRadius = 0.32f, OuterRadius = 0.42f, Rings = 16, RingSegments = 6 },
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                    MaterialOverride = new StandardMaterial3D
                    {
                        AlbedoColor = ClassTowers.BeaconColor,
                        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                        EmissionEnabled = true,
                        Emission = ClassTowers.BeaconColor,
                        EmissionEnergyMultiplier = 2f,
                        NoDepthTest = true,
                    },
                    Position = new Vector3(0, (IsBoss ? 3.4f : 2.1f), 0),
                };
                AddChild(_markRing);
            }
            _markRing.Visible = true;
        }

        private void TickMarks(float dt)
        {
            var p = GlobalPosition;
            if (_velKnown && dt > 0f) Velocity = new Vector3((p.X - _velPrev.X) / dt, 0f, (p.Z - _velPrev.Z) / dt);
            _velPrev = p;
            _velKnown = true;
            if (_markTimer <= 0f) return;
            _markTimer -= dt;
            if (_markSlow > 0f) ApplySlow(_markSlow, 0.3f);
            if (_markRing != null && IsInstanceValid(_markRing))
            {
                _markRing.RotateY(dt * 3f);
                if (_markTimer <= 0f) { _markRing.Visible = false; _markTaken = _markBitTaken = _markSlow = 0f; }
            }
        }
    }
}
