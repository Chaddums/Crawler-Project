using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Enemies that work together. Rally: an enemy with RALLY_COUNT or more others within
    /// RALLY_RADIUS moves and hits RALLY_BONUS harder (a red ring at its feet, a "RALLY" tag now
    /// and then). Empowerer (a trait): a fragile support unit that tethers itself to the toughest
    /// enemy near it and makes it take EMPOWER_TAKEN of every hit, move and hit harder, until the
    /// Empowerer dies or is stunned (the tether is a magenta beam you can follow to it).
    /// </summary>
    public partial class VineEnemy
    {
        public const float RALLY_RADIUS = 5f, RALLY_BONUS = 0.15f;
        public const int RALLY_COUNT = 3;
        public const float EMPOWER_RANGE = 9f, EMPOWER_TAKEN = 0.4f, EMPOWER_SPEED = 0.25f, EMPOWER_DAMAGE = 0.3f;

        public bool IsEmpowerer => (Traits & EnemyTraits.Empowerer) != 0;
        /// <summary>Rallied by others close by (checked twice a second).</summary>
        public bool Rallied { get; private set; }
        /// <summary>The Empowerer tethered to this enemy, if any.</summary>
        public VineEnemy EmpoweredBy { get; private set; }
        public bool Empowered => EmpoweredBy != null && IsInstanceValid(EmpoweredBy) && EmpoweredBy.IsAlive && EmpoweredBy.IsTethering;
        /// <summary>The enemy this Empowerer is tethered to.</summary>
        public VineEnemy TetherTarget { get; private set; }
        public bool IsTethering => TetherTarget != null && IsInstanceValid(TetherTarget) && TetherTarget.IsAlive && _stunTimer <= 0f;

        /// <summary>Speed and damage share from rallying and being empowered.</summary>
        public float SupportSpeedBonus => (Rallied ? RALLY_BONUS : 0f) + (Empowered ? EMPOWER_SPEED : 0f);
        public float SupportDamageBonus => (Rallied ? RALLY_BONUS : 0f) + (Empowered ? EMPOWER_DAMAGE : 0f);
        /// <summary>Share of a hit an empowered enemy takes.</summary>
        public float SupportTakenMult => Empowered ? EMPOWER_TAKEN : 1f;

        private float _supportTimer = (float)GD.RandRange(0.0, 0.5), _rallyTagTimer = (float)GD.RandRange(2.0, 6.0);
        private MeshInstance3D _rallyRing, _empowerRing, _tether;
        private static StandardMaterial3D _rallyMat, _empowerMat, _tetherMat;

        private void TickSupport(float dt)
        {
            if ((_supportTimer -= dt) <= 0f)
            {
                _supportTimer = 0.5f;
                CheckRally();
                if (IsEmpowerer) PickTetherTarget();
            }
            UpdateSupportVisuals();
            if (Rallied && (_rallyTagTimer -= dt) <= 0f)
            {
                _rallyTagTimer = (float)GD.RandRange(5.0, 9.0);
                DamageNumbers.Tag(GlobalPosition + Vector3.Up * 2.4f, $"RALLY +{RALLY_BONUS * 100:0}%", new Color(1f, 0.45f, 0.35f));
            }
        }

        private void CheckRally()
        {
            var tree = GetTree();
            if (tree == null) return;
            var list = Roster.Enemies(tree);
            var pos = Roster.EnemyPositions(tree);
            var me = GlobalPosition;
            int near = 0;
            float r2 = RALLY_RADIUS * RALLY_RADIUS;
            for (int i = 0; i < list.Count && near < RALLY_COUNT; i++)
            {
                var e = list[i];
                if (e == this || !IsInstanceValid(e) || !e.IsAlive) continue;
                if (me.DistanceSquaredTo(pos[i]) < r2) near++;
            }
            Rallied = near >= RALLY_COUNT;
        }

        /// <summary>An Empowerer picks the toughest enemy in reach that nobody else is empowering.</summary>
        private void PickTetherTarget()
        {
            if (IsTethering && GlobalPosition.DistanceTo(TetherTarget.GlobalPosition) < EMPOWER_RANGE * 1.3f) return;
            if (TetherTarget != null && IsInstanceValid(TetherTarget) && TetherTarget.EmpoweredBy == this) TetherTarget.EmpoweredBy = null;
            TetherTarget = null;
            if (_stunTimer > 0f) return;
            var list = Roster.Enemies(GetTree());
            VineEnemy best = null;
            float bestHp = 0f;
            foreach (var e in list)
            {
                if (e == this || !IsInstanceValid(e) || !e.IsAlive || e.IsEmpowerer || e.Empowered) continue;
                if (e.GlobalPosition.DistanceTo(GlobalPosition) > EMPOWER_RANGE) continue;
                if (e.MaxHealth > bestHp) { bestHp = e.MaxHealth; best = e; }
            }
            if (best == null) return;
            TetherTarget = best;
            best.EmpoweredBy = this;
            if (!_announcedTether)
            {
                _announcedTether = true;
                DamageNumbers.Tag(best.GlobalPosition + Vector3.Up * 2.6f, "EMPOWERED: kill or stun its Empowerer", new Color(1f, 0.35f, 0.95f));
            }
        }
        private bool _announcedTether;

        private void UpdateSupportVisuals()
        {
            // Rally: a thin red ring at the feet
            if (Rallied && _rallyRing == null)
            {
                _rallyMat ??= new StandardMaterial3D { AlbedoColor = new Color(1f, 0.3f, 0.2f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    EmissionEnabled = true, Emission = new Color(1f, 0.25f, 0.15f), EmissionEnergyMultiplier = 1.2f };
                _rallyRing = new MeshInstance3D { Name = "RallyRing", Mesh = new TorusMesh { InnerRadius = 0.55f, OuterRadius = 0.68f, Rings = 24, RingSegments = 4 },
                    MaterialOverride = _rallyMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Position = new Vector3(0, 0.06f, 0), Scale = new Vector3(1, 0.1f, 1) };
                AddChild(_rallyRing);
            }
            if (_rallyRing != null) _rallyRing.Visible = Rallied && !Empowered;

            // Empowered: a bright magenta ring
            bool emp = Empowered;
            if (emp && _empowerRing == null)
            {
                _empowerMat ??= new StandardMaterial3D { AlbedoColor = new Color(1f, 0.3f, 0.95f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    EmissionEnabled = true, Emission = new Color(1f, 0.25f, 0.9f), EmissionEnergyMultiplier = 2f };
                _empowerRing = new MeshInstance3D { Name = "EmpowerRing", Mesh = new TorusMesh { InnerRadius = 0.75f, OuterRadius = 0.95f, Rings = 28, RingSegments = 4 },
                    MaterialOverride = _empowerMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Position = new Vector3(0, 0.08f, 0), Scale = new Vector3(1, 0.12f, 1) };
                AddChild(_empowerRing);
            }
            if (_empowerRing != null)
            {
                _empowerRing.Visible = emp;
                if (emp) _empowerRing.Rotation = new Vector3(0, _breathTimer * 2f, 0);
            }

            // Empowerer: the tether, a beam from it to what it powers
            if (!IsEmpowerer) return;
            bool on = IsTethering;
            if (on && _tether == null)
            {
                _tetherMat ??= new StandardMaterial3D { AlbedoColor = new Color(1f, 0.35f, 0.95f, 0.85f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha, EmissionEnabled = true, Emission = new Color(1f, 0.3f, 0.9f), EmissionEnergyMultiplier = 2.5f };
                _tether = new MeshInstance3D { Name = "Tether", Mesh = new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.05f, Height = 1f, RadialSegments = 6, Rings = 1 },
                    MaterialOverride = _tetherMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, TopLevel = true };
                AddChild(_tether);
            }
            if (_tether == null) return;
            _tether.Visible = on;
            if (!on) return;
            var from = GlobalPosition + Vector3.Up * 1.0f;
            var to = TetherTarget.BodyCentre;
            var d = to - from;
            float len = d.Length();
            if (len < 0.05f) { _tether.Visible = false; return; }
            var up = d / len;
            var side = Mathf.Abs(up.Dot(Vector3.Forward)) > 0.95f ? Vector3.Right : Vector3.Forward;
            var x = up.Cross(side).Normalized();
            var z = x.Cross(up).Normalized();
            float pulse = 1f + 0.35f * Mathf.Sin(_breathTimer * 9f);
            _tether.GlobalTransform = new Transform3D(new Basis(x * pulse, up * len, z * pulse), from + d * 0.5f);
        }

        /// <summary>Break the tether at once (death or a stun).</summary>
        private void DropTether()
        {
            if (TetherTarget != null && IsInstanceValid(TetherTarget) && TetherTarget.EmpoweredBy == this) TetherTarget.EmpoweredBy = null;
            TetherTarget = null;
            if (_tether != null) _tether.Visible = false;
        }
    }
}
