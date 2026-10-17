using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// The class towers. Every Spire class builds the same four (Junk Turret for armour, Flak
    /// Battery for flyers, Tesla Coil for shields, Salvage Drill), and five of its own:
    ///
    /// Bruteforge (firepower): Scatter Cannon, Mortar Pit (long shells onto the canyon floor, can't
    /// hit close), Rail Lance (a slug through every enemy on a line), Slag Chute (on a canyon's
    /// lip, pours burning slag on the floor below) and Autocannon Nest (fast heavy fire, two
    /// targets). Arcanist (the network): Overclock Relay, Prism (echoes the arcs of Tesla Coils
    /// near it), Signal Jammer (a field that slows and strips shields), Storm Capacitor (charges
    /// while it waits, dumps it in one blast) and Lens (a beam that grows with its crew and the
    /// longer it holds a target). Obelisk (BIT in the field): Tar Sprayer, Pneumatic Ram, Gravity
    /// Well (pulls enemies into a knot), Beacon (marked enemies take more from everything) and
    /// Barricade (the Barrier Wall: on a canyon map the one thing built on the floor, and enemies
    /// stop to break it).
    ///
    /// The playtest's verdict on one shared roster of nine was "boring, it should change by class".
    /// </summary>
    public static class ClassTowers
    {
        // ── Bruteforge ──
        public const float MORTAR_RANGE = 26f, MORTAR_MIN_RANGE = 6f, MORTAR_DAMAGE = 10f, MORTAR_INTERVAL = 3f;
        public const float MORTAR_RADIUS = 2.6f, MORTAR_FLIGHT = 1.1f;
        public const float RAIL_RANGE = 22f, RAIL_DAMAGE = 16f, RAIL_INTERVAL = 2.4f, RAIL_WIDTH = 0.8f;
        public const float SLAG_RANGE = 6.5f, SLAG_DPS = 10f, SLAG_INTERVAL = 4f, SLAG_RADIUS = 2.2f, SLAG_SECONDS = 5f, SLAG_SLOW = 0.15f;
        public const float NEST_RANGE = 13f, NEST_DAMAGE = 16f, NEST_INTERVAL = 0.25f; // split over two: at 8 it was a worse Junk Turret for more
        // ── Arcanist ──
        public const float PRISM_RANGE = 12f, PRISM_SHARE = 0.7f, PRISM_LISTEN = 7f;
        public const int PRISM_ARCS = 2;
        public const float JAMMER_RANGE = 9f, JAMMER_DPS = 3f, JAMMER_SLOW = 0.2f, JAMMER_DRAIN = 20f, JAMMER_TICK = 0.5f;
        public const float CAP_RANGE = 15f, CAP_DAMAGE = 14f, CAP_MAX = 8f, CAP_MIN = 3f, CAP_SPLASH = 2.5f, CAP_SPLASH_SHARE = 0.5f;
        public const float LENS_RANGE = 18f, LENS_DPS = 9f, LENS_TICK = 0.1f, LENS_RAMP = 0.4f, LENS_RAMP_MAX = 3f, LENS_PER_LINK = 0.1f;
        // ── Obelisk ──
        public const float WELL_RANGE = 10f, WELL_INTERVAL = 3.5f, WELL_PULL = 3f, WELL_STUN = 0.4f, WELL_BOSS = 0.25f;
        public const float BEACON_RANGE = 14f, BEACON_INTERVAL = 2.5f, BEACON_SECONDS = 6f, BEACON_TAKEN = 0.25f, BEACON_BIT_TAKEN = 0.5f;
        public const int BEACON_MARKS = 2;

        public static bool IsClassTower(VineNodeType t) => t >= VineNodeType.MortarPit && t <= VineNodeType.Beacon;

        public static readonly Color MortarColor = new(1f, 0.6f, 0.2f), RailColor = new(0.55f, 0.85f, 1f), SlagColor = new(1f, 0.42f, 0.08f),
            NestColor = new(1f, 0.78f, 0.3f), PrismColor = new(0.55f, 0.95f, 1f), JammerColor = new(0.35f, 0.9f, 0.75f),
            CapColor = new(0.6f, 0.75f, 1f), LensColor = new(1f, 0.92f, 0.55f), WellColor = new(0.7f, 0.5f, 1f), BeaconColor = new(1f, 0.3f, 0.3f);
    }

    public partial class VineNode
    {
        private float _classTimer, _classTick;
        private float _charge;
        private int _nestBarrel;
        private VineEnemy _lensTarget;
        private float _lensHeld;
        private MeshInstance3D _lensBeam;

        /// <summary>The class towers' per-step behaviour; false when this isn't one.</summary>
        private bool UpdateClassTower(float dt)
        {
            switch (Data?.Type)
            {
                case VineNodeType.MortarPit: UpdateMortar(dt); return true;
                case VineNodeType.RailLance: UpdateRail(dt); return true;
                case VineNodeType.SlagChute: UpdateSlag(dt); return true;
                case VineNodeType.AutocannonNest: UpdateNest(dt); return true;
                case VineNodeType.ArcPrism: return true; // fires when a Tesla Coil near it does (EchoArcs)
                case VineNodeType.SignalJammer: UpdateJammer(dt); return true;
                case VineNodeType.StormCapacitor: UpdateCapacitor(dt); return true;
                case VineNodeType.Lens: UpdateLens(dt); return true;
                case VineNodeType.GravityWell: UpdateWell(dt); return true;
                case VineNodeType.Beacon: UpdateBeacon(dt); return true;
            }
            return false;
        }

        private float Flat2(Vector3 a, Vector3 b) { float dx = a.X - b.X, dz = a.Z - b.Z; return dx * dx + dz * dz; }

        // ── Mortar Pit ──

        private void UpdateMortar(float dt)
        {
            if (!_autoFireEnabled) return;
            if ((_classTimer -= dt) > 0f) return;
            float range = GetEffectiveRange(), min = ClassTowers.MORTAR_MIN_RANGE;
            // The farthest enemy in reach that isn't inside its minimum: shells go long
            VineEnemy target = null;
            float best = -1f;
            foreach (var e in Roster.Enemies(GetTree()))
            {
                if (!CanTarget(e)) continue;
                float d2 = Flat2(e.GlobalPosition, At);
                if (d2 > range * range || d2 < min * min) continue;
                if (d2 > best) { best = d2; target = e; }
            }
            if (target == null) { _classTimer = IdleRescan; IsActive = false; return; }
            if (_look != null && !_look.ReadyToFire(target)) return;

            float flight = ClassTowers.MORTAR_FLIGHT;
            // Lead the target by where it will be when the shell lands
            var vel = target.Velocity;
            var land = target.GlobalPosition + new Vector3(vel.X, 0f, vel.Z) * flight;
            if (ServiceLocator.TryGet<VineGrid>(out var grid)) land.Y = grid.GetWorldHeight(land.X, land.Z);
            float dmg = GetEffectiveDamage(ClassTowers.MORTAR_INTERVAL);
            float radius = ClassTowers.MORTAR_RADIUS * (HasBranch("cluster_bombs") ? 1.35f : 1f);
            bool burn = HasBranch("incendiary");
            var kind = HitKind;
            var muzzle = Muzzle();
            _look?.Track(target);
            _look?.Fire();
            VfxFactory.SpawnMuzzleFlash(GetTree(), muzzle, Vector3.Up, ClassTowers.MortarColor, 2f);
            VfxFactory.SpawnShell(GetTree(), muzzle, land, ClassTowers.MortarColor, flight);
            var tree = GetTree();
            var self = this;
            tree.CreateTimer(flight, false).Timeout += () =>
            {
                if (!IsInstanceValid(self)) return;
                VineEnemy.HitSource = "your towers";
                VineEnemy.HitTower = self;
                try
                {
                    foreach (var e in Roster.Enemies(tree))
                    {
                        if (!IsInstanceValid(e) || !e.IsAlive || e.IsFlying) continue;
                        float d = Mathf.Sqrt(self.Flat2(e.GlobalPosition, land));
                        if (d > radius) continue;
                        e.TakeDamage(dmg * (1f - d / radius * 0.5f), kind);
                        if (burn) e.Ignite(ClassTowers.SLAG_DPS * 0.8f * self.UpgradeDamageMult, 3f, 0f);
                    }
                }
                finally { VineEnemy.HitSource = null; VineEnemy.HitTower = null; }
                VfxFactory.SpawnExplosion(tree, land + Vector3.Up * 0.3f, radius, burn ? VineEnemy.FireColor : ClassTowers.MortarColor);
                if (burn) VfxFactory.SpawnEmbers(tree, land, radius * 0.8f);
            };
            Rearm(ref _classTimer, GetEffectiveFireInterval(ClassTowers.MORTAR_INTERVAL));
            IsActive = true;
        }

        // ── Rail Lance ──

        private void UpdateRail(float dt)
        {
            if (!_autoFireEnabled) return;
            if ((_classTimer -= dt) > 0f) return;
            float range = GetEffectiveRange();
            var target = NearestInRange(range, out _);
            if (target == null) { _classTimer = IdleRescan; IsActive = false; return; }
            if (_look != null && !_look.ReadyToFire(target)) return;
            float dmg = GetEffectiveDamage(ClassTowers.RAIL_INTERVAL) * (HasBranch("heavy_slug") ? 2f : 1f);
            FireRail(target, dmg, range);
            if (HasBranch("twin_rail"))
            {
                // The second slug takes the next enemy off the first line
                VineEnemy second = null;
                float best = range * range;
                foreach (var e in Roster.Enemies(GetTree()))
                {
                    if (e == target || !CanTarget(e)) continue;
                    float d2 = Flat2(e.GlobalPosition, At);
                    if (d2 < best) { best = d2; second = e; }
                }
                if (second != null) FireRail(second, dmg * 0.6f, range);
            }
            Rearm(ref _classTimer, GetEffectiveFireInterval(ClassTowers.RAIL_INTERVAL) * (HasBranch("heavy_slug") ? 1.4f : 1f));
            IsActive = true;
        }

        /// <summary>One slug from the muzzle through <paramref name="target"/> to the end of its reach: everything near the line takes it.</summary>
        private void FireRail(VineEnemy target, float dmg, float range)
        {
            var from = Muzzle();
            var dir = target.GlobalPosition - At;
            dir.Y = 0f;
            if (dir.LengthSquared() < 0.0001f) return;
            dir = dir.Normalized();
            var end = At + dir * range;
            if (ServiceLocator.TryGet<VineGrid>(out var grid)) end.Y = grid.GetWorldHeight(end.X, end.Z) + 0.8f;
            var kind = HitKind;
            float w = ClassTowers.RAIL_WIDTH;
            foreach (var e in Roster.Enemies(GetTree()))
            {
                if (!CanTarget(e)) continue;
                var rel = e.GlobalPosition - At;
                rel.Y = 0f;
                float along = rel.Dot(dir);
                if (along < 0f || along > range) continue;
                float off = (rel - dir * along).Length();
                if (off <= w + e.BodyRadius) e.TakeDamage(dmg, kind);
            }
            _look?.Track(target);
            _look?.Fire();
            VfxFactory.SpawnMuzzleFlash(GetTree(), from, dir, ClassTowers.RailColor, 1.6f);
            VfxFactory.SpawnLanceBeam(GetTree(), from, end, ClassTowers.RailColor);
        }

        // ── Slag Chute ──

        /// <summary>A Slag Chute needs the floor below it: a canyon's lip, a walkable cell within two.</summary>
        public static string SlagWhyNot(VineGrid grid, Vector2I cell)
        {
            if (grid == null) return null;
            for (int dx = -2; dx <= 2; dx++)
                for (int dy = -2; dy <= 2; dy++)
                    if (grid.IsWalkable(cell + new Vector2I(dx, dy))) return null;
            return "A Slag Chute pours onto the floor: build it on the lip of a canyon";
        }

        private void UpdateSlag(float dt)
        {
            if (!_autoFireEnabled) return;
            if ((_classTimer -= dt) > 0f) return;
            float range = GetEffectiveRange();
            var target = NearestInRange(range, out _);
            if (target == null) { _classTimer = IdleRescan; IsActive = false; return; }
            var at = target.GlobalPosition;
            if (ServiceLocator.TryGet<VineGrid>(out var grid)) at.Y = grid.GetWorldHeight(at.X, at.Z);
            bool magma = HasBranch("magma");
            float dps = Data.Damage * UpgradeDamageMult * (1f + RoleRun.TypeDamageOf(Data.Type)) * (1f + CrewBonus) * MetaRun.TowerDamageFactor
                * (GameManager.Instance?.RunTowerDamageMult ?? 1f);
            float slow = ClassTowers.SLAG_SLOW + (HasBranch("clinker") ? 0.3f : 0f);
            float delay = 0.45f;
            VfxFactory.SpawnShell(GetTree(), Muzzle(), at, ClassTowers.SlagColor, delay);
            SlagPool.Spawn(GetTree(), at, delay, this, dps, slow,
                ClassTowers.SLAG_RADIUS * (magma ? 1.3f : 1f), ClassTowers.SLAG_SECONDS * (magma ? 2f : 1f));
            _look?.Track(target);
            _look?.Fire();
            Rearm(ref _classTimer, GetEffectiveFireInterval(ClassTowers.SLAG_INTERVAL));
            IsActive = true;
        }

        // ── Autocannon Nest ──

        private void UpdateNest(float dt)
        {
            if (!_autoFireEnabled) return;
            if ((_classTimer -= dt) > 0f) return;
            float range = GetEffectiveRange();
            // Two barrels: each takes a different one of the two nearest
            VineEnemy a = null, b = null;
            float da = range * range, db = range * range;
            foreach (var e in Roster.Enemies(GetTree()))
            {
                if (!CanTarget(e)) continue;
                float d2 = Flat2(e.GlobalPosition, At);
                if (d2 < da) { b = a; db = da; a = e; da = d2; }
                else if (d2 < db) { b = e; db = d2; }
            }
            if (a == null) { _classTimer = IdleRescan; IsActive = false; return; }
            _nestBarrel ^= 1;
            var target = _nestBarrel == 1 && b != null ? b : a;
            if (_look != null && !_look.ReadyToFire(a)) return;
            float dmg = GetEffectiveDamage(ClassTowers.NEST_INTERVAL);
            target.TakeDamage(dmg, HitKind);
            if (HasBranch("ap_rounds")) target.BreakArmour(1.2f);
            var muzzle = Muzzle();
            _look?.Track(a);
            _look?.Fire();
            VfxFactory.SpawnMuzzleFlash(GetTree(), muzzle, target.GlobalPosition - muzzle, ClassTowers.NestColor, 0.8f);
            VfxFactory.SpawnTracer(GetTree(), muzzle, target.GlobalPosition, ClassTowers.NestColor, 46f);
            Rearm(ref _classTimer, GetEffectiveFireInterval(ClassTowers.NEST_INTERVAL));
            IsActive = true;
        }

        // ── Prism ──

        /// <summary>
        /// A Tesla Coil fired for <paramref name="hit"/>: every Prism within PRISM_LISTEN of it
        /// arcs on to enemies in its own reach for its share.
        /// </summary>
        private void EchoArcs(float hit)
        {
            foreach (var n in Roster.Nodes(GetTree()))
            {
                if (n == null || n.Data?.Type != VineNodeType.ArcPrism || n.IsDestroyed || !n._autoFireEnabled) continue;
                if (n.Flat2(n.At, At) > ClassTowers.PRISM_LISTEN * ClassTowers.PRISM_LISTEN) continue;
                n.Echo(hit, this);
            }
        }

        private void Echo(float hit, VineNode coil)
        {
            bool focus = HasBranch("focus");
            int arcs = focus ? 1 : ClassTowers.PRISM_ARCS + (HasBranch("spectrum") ? 2 : 0);
            float share = (ClassTowers.PRISM_SHARE + (focus ? 0.8f : 0f)) * UpgradeDamageMult * (1f + CrewBonus);
            float range = GetEffectiveRange();
            var hitList = new List<VineEnemy>();
            for (int k = 0; k < arcs; k++)
            {
                VineEnemy best = null;
                float bd = range * range;
                foreach (var e in Roster.Enemies(GetTree()))
                {
                    if (!CanTarget(e) || hitList.Contains(e)) continue;
                    float d2 = Flat2(e.GlobalPosition, At);
                    if (d2 < bd) { bd = d2; best = e; }
                }
                if (best == null) break;
                hitList.Add(best);
            }
            if (hitList.Count == 0) return;
            var prev = VineEnemy.HitTower;
            VineEnemy.HitTower = this;
            try
            {
                var from = Muzzle();
                VfxFactory.SpawnArc(GetTree(), coil.Muzzle(), from, ClassTowers.PrismColor);
                foreach (var e in hitList)
                {
                    e.TakeDamage(hit * share, DamageKind.Electric);
                    VfxFactory.SpawnArc(GetTree(), from, e.GlobalPosition + Vector3.Up * 0.6f, ClassTowers.PrismColor);
                }
            }
            finally { VineEnemy.HitTower = prev; }
            _look?.Fire();
            IsActive = true;
            _stateTimer = 0.4f;
        }

        // ── Signal Jammer ──

        private void UpdateJammer(float dt)
        {
            if (!_autoFireEnabled) return;
            if ((_classTick -= dt) > 0f) return;
            float tick = ClassTowers.JAMMER_TICK;
            _classTick = tick;
            float range = GetEffectiveRange(), r2 = range * range;
            float dmg = GetEffectiveDamage(tick);
            float slow = Mathf.Clamp(ClassTowers.JAMMER_SLOW + UpgradeSlowBonus, 0f, 0.8f);
            float drain = ClassTowers.JAMMER_DRAIN * UpgradeDamageMult * tick;
            bool blackout = HasBranch("blackout");
            bool feedback = HasBranch("feedback");
            if (blackout) _classTimer -= tick;
            bool stunNow = blackout && _classTimer <= 0f;
            if (stunNow) _classTimer = 5f;
            int inField = 0;
            foreach (var e in Roster.Enemies(GetTree()))
            {
                if (!CanTarget(e) || Flat2(e.GlobalPosition, At) > r2) continue;
                inField++;
                e.ApplySlow(slow, tick * 1.5f);
                bool hadShield = e.ShieldHP > 0f;
                e.DrainShield(drain);
                e.TakeDamage(dmg, DamageKind.Electric);
                if (feedback && hadShield && e.ShieldHP <= 0f) e.TakeDamage(40f * UpgradeDamageMult, DamageKind.Electric);
                if (stunNow) e.ApplyStun(e.IsBoss || e.IsCommander ? 0.3f : 1f);
            }
            IsActive = inField > 0;
            if (inField > 0 && (_charge += tick) >= 1f)
            {
                _charge = 0f;
                VfxFactory.SpawnAreaPulse(GetTree(), At, range, ClassTowers.JammerColor, 0.7f, stunNow ? 1.2f : 0.45f);
            }
        }

        // ── Storm Capacitor ──

        /// <summary>The Storm Capacitor's stored charge, in seconds of build-up (tests, panel).</summary>
        public float Charge => _charge;

        private void UpdateCapacitor(float dt)
        {
            if (!_autoFireEnabled) return;
            bool bank = HasBranch("bank");
            float max = ClassTowers.CAP_MAX * (bank ? 1.75f : 1f);
            float rate = 1f / Mathf.Max(0.1f, GetEffectiveFireInterval(1f)) * (bank ? 1.3f : 1f);
            _charge = Mathf.Min(max, _charge + dt * rate);
            if ((_classTimer -= dt) > 0f) return;
            _classTimer = 0.2f;
            if (_charge < ClassTowers.CAP_MIN) return;
            float range = GetEffectiveRange();
            var target = NearestInRange(range, out _);
            if (target == null) { IsActive = false; return; }
            if (_look != null && !_look.ReadyToFire(target)) return;
            float dmg = GetEffectiveDamage(_charge) * (1f + RoleRun.TypeDamageOf(VineNodeType.TeslaCoil) * 0f);
            target.TakeDamage(dmg, DamageKind.Electric);
            float splash = ClassTowers.CAP_SPLASH;
            var hit = new List<VineEnemy> { target };
            foreach (var e in Roster.Enemies(GetTree()))
            {
                if (e == target || !CanTarget(e) || Flat2(e.GlobalPosition, target.GlobalPosition) > splash * splash) continue;
                e.TakeDamage(dmg * ClassTowers.CAP_SPLASH_SHARE, DamageKind.Electric);
                hit.Add(e);
            }
            var from = Muzzle();
            VfxFactory.SpawnArc(GetTree(), from, target.GlobalPosition + Vector3.Up * 0.6f, ClassTowers.CapColor);
            VfxFactory.SpawnArc(GetTree(), from, target.GlobalPosition + Vector3.Up * 0.9f, ClassTowers.CapColor);
            VfxFactory.SpawnExplosion(GetTree(), target.GlobalPosition + Vector3.Up * 0.4f, splash, ClassTowers.CapColor);
            if (HasBranch("chain"))
            {
                // Arcs on to three more past the blast
                var last = target;
                for (int k = 0; k < 3; k++)
                {
                    VineEnemy next = null;
                    float bd = 25f;
                    foreach (var e in Roster.Enemies(GetTree()))
                    {
                        if (hit.Contains(e) || !CanTarget(e)) continue;
                        float d2 = Flat2(e.GlobalPosition, last.GlobalPosition);
                        if (d2 < bd) { bd = d2; next = e; }
                    }
                    if (next == null) break;
                    next.TakeDamage(dmg * 0.4f, DamageKind.Electric);
                    VfxFactory.SpawnArc(GetTree(), last.GlobalPosition + Vector3.Up * 0.6f, next.GlobalPosition + Vector3.Up * 0.6f, ClassTowers.CapColor);
                    hit.Add(next);
                    last = next;
                }
            }
            _look?.Track(target);
            _look?.Fire();
            _charge = 0f;
            IsActive = true;
        }

        // ── Lens ──

        /// <summary>How hard the Lens's beam is burning now (1 at first, up to LENS_RAMP_MAX).</summary>
        public float LensRamp => 1f + Mathf.Min(ClassTowers.LENS_RAMP_MAX - 1f + (HasBranch("burn") ? 1f : 0f), _lensHeld * ClassTowers.LENS_RAMP);

        private void UpdateLens(float dt)
        {
            if (!_autoFireEnabled) { ShowLensBeam(null); return; }
            float range = GetEffectiveRange();
            if (_lensTarget != null && (!IsInstanceValid(_lensTarget) || !CanTarget(_lensTarget) || Flat2(_lensTarget.GlobalPosition, At) > range * range))
            { _lensTarget = null; _lensHeld = 0f; }
            if (_lensTarget == null) _lensTarget = NearestInRange(range, out _);
            if (_lensTarget == null) { ShowLensBeam(null); IsActive = false; return; }
            _lensHeld += dt;
            ShowLensBeam(_lensTarget);
            if ((_classTimer -= dt) > 0f) return;
            float tick = ClassTowers.LENS_TICK;
            // Each crew link counts again for the Lens: it grows with the network round it
            float crew = 1f + Mathf.Min(CrewLinks, Constants.CREW_MAX_LINKS) * ClassTowers.LENS_PER_LINK;
            float dmg = GetEffectiveDamage(tick) * crew * LensRamp;
            _lensTarget.TakeDamage(dmg, HitKind);
            if (HasBranch("burn") && _lensHeld > 2f) _lensTarget.Ignite(dmg / tick * 0.25f, 2f, 0f);
            if (HasBranch("split"))
            {
                VineEnemy second = null;
                float bd = range * range;
                foreach (var e in Roster.Enemies(GetTree()))
                {
                    if (e == _lensTarget || !CanTarget(e)) continue;
                    float d2 = Flat2(e.GlobalPosition, _lensTarget.GlobalPosition);
                    if (d2 < bd && Flat2(e.GlobalPosition, At) <= range * range) { bd = d2; second = e; }
                }
                second?.TakeDamage(dmg * 0.6f, HitKind);
            }
            _look?.Track(_lensTarget);
            Rearm(ref _classTimer, GetEffectiveFireInterval(tick));
            IsActive = true;
        }

        private void ShowLensBeam(VineEnemy target)
        {
            if (target == null)
            {
                if (_lensBeam != null && IsInstanceValid(_lensBeam)) _lensBeam.Visible = false;
                return;
            }
            if (_lensBeam == null || !IsInstanceValid(_lensBeam))
            {
                _lensBeam = new MeshInstance3D
                {
                    Name = "LensBeam",
                    Mesh = new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.06f, Height = 1f, RadialSegments = 6, Rings = 1 },
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                    MaterialOverride = new StandardMaterial3D
                    {
                        AlbedoColor = ClassTowers.LensColor,
                        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                        EmissionEnabled = true,
                        Emission = ClassTowers.LensColor,
                        EmissionEnergyMultiplier = 2.5f,
                    },
                    TopLevel = true,
                };
                AddChild(_lensBeam);
            }
            var a = Muzzle();
            var b = target.GlobalPosition + Vector3.Up * 0.7f;
            var mid = (a + b) * 0.5f;
            float len = a.DistanceTo(b);
            if (len < 0.05f) return;
            var dir = (b - a) / len;
            // A thicker, hotter beam the longer it holds on
            float k = LensRamp;
            var basis = Basis.LookingAt(dir, Mathf.Abs(dir.Y) > 0.98f ? Vector3.Right : Vector3.Up) * Basis.FromEuler(new Vector3(Mathf.Pi / 2f, 0f, 0f));
            _lensBeam.GlobalTransform = new Transform3D(basis.Scaled(new Vector3(0.6f + 0.25f * k, len, 0.6f + 0.25f * k)), mid);
            _lensBeam.Visible = true;
        }

        // ── Gravity Well ──

        private void UpdateWell(float dt)
        {
            if (!_autoFireEnabled) return;
            if ((_classTimer -= dt) > 0f) return;
            float range = GetEffectiveRange(), r2 = range * range;
            var pulled = new List<VineEnemy>();
            var center = Vector3.Zero;
            foreach (var e in Roster.Enemies(GetTree()))
            {
                if (!CanTarget(e) || Flat2(e.GlobalPosition, At) > r2) continue;
                pulled.Add(e);
                center += e.GlobalPosition;
            }
            if (pulled.Count == 0) { _classTimer = IdleRescan; IsActive = false; return; }
            // The knot forms at the middle of the group, on the floor (never up on the wall)
            center /= pulled.Count;
            float force = ClassTowers.WELL_PULL * UpgradeForceMult * RoleRun.ForceMult;
            bool crush = HasBranch("crush");
            foreach (var e in pulled)
            {
                var to = center - e.GlobalPosition;
                to.Y = 0f;
                float d = to.Length();
                if (d < 0.05f) continue;
                float resist = e.IsBoss || e.IsCommander || e.IsAscendant ? ClassTowers.WELL_BOSS : 1f;
                e.ApplyKnockback(to / d * Mathf.Min(d, force * resist));
                e.ApplyStun(ClassTowers.WELL_STUN * resist);
                if (crush) e.Crush(1.3f, 2f);
            }
            VfxFactory.SpawnImplosion(GetTree(), center + Vector3.Up * 0.5f, Mathf.Min(range, 4f));
            _look?.Fire();
            Rearm(ref _classTimer, GetEffectiveFireInterval(ClassTowers.WELL_INTERVAL * (HasBranch("singularity") ? 0.6f : 1f)));
            IsActive = true;
        }

        // ── Beacon ──

        private void UpdateBeacon(float dt)
        {
            if (!_autoFireEnabled) return;
            if ((_classTimer -= dt) > 0f) return;
            float range = GetEffectiveRange(), r2 = range * range;
            int marks = HasBranch("spotter") ? 4 : ClassTowers.BEACON_MARKS;
            // The toughest unmarked enemies in reach
            var pick = new List<VineEnemy>();
            foreach (var e in Roster.Enemies(GetTree()))
            {
                if (!CanTarget(e) || e.IsMarked || Flat2(e.GlobalPosition, At) > r2) continue;
                pick.Add(e);
            }
            if (pick.Count == 0) { _classTimer = IdleRescan; IsActive = false; return; }
            pick.Sort((a, b) => b.CurrentHealth.CompareTo(a.CurrentHealth));
            float taken = ClassTowers.BEACON_TAKEN * UpgradeBuffMult;
            var from = Muzzle();
            for (int i = 0; i < pick.Count && i < marks; i++)
            {
                pick[i].Mark(ClassTowers.BEACON_SECONDS, taken, ClassTowers.BEACON_BIT_TAKEN * UpgradeBuffMult, HasBranch("target_lock") ? 0.25f : 0f);
                VfxFactory.SpawnTracer(GetTree(), from, pick[i].GlobalPosition + Vector3.Up * 0.8f, ClassTowers.BeaconColor, 60f);
            }
            _look?.Track(pick[0]);
            _look?.Fire();
            Rearm(ref _classTimer, GetEffectiveFireInterval(ClassTowers.BEACON_INTERVAL));
            IsActive = true;
        }
    }
}
