using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// A relic that replaces BIT's gun (one at a time, the "bit" slot). The player calls
    /// <see cref="Tick"/> every physics frame BIT is out and alive, in place of its own shooting;
    /// the weapon finds its targets, strikes, and draws it. Its gear goes on in
    /// <see cref="Begin"/> (the perk guns are hidden while it's on) and comes off in <see cref="End"/>.
    /// </summary>
    public abstract class BitWeapon
    {
        public string Id { get; }
        protected readonly VinePlayer P;
        /// <summary>Strikes made (tests).</summary>
        public int Attacks { get; protected set; }
        /// <summary>Last enemy struck (tests).</summary>
        public VineEnemy LastTarget { get; protected set; }

        protected BitWeapon(string id, VinePlayer player) { Id = id; P = player; }

        protected float T(string key, float fallback = 0f) => RelicRun.T(Id, key, fallback);
        protected SceneTree Tree => P.GetTree();
        public Color Tint => RelicRegistry.Get(Id)?.Tint ?? Colors.White;

        /// <summary>Move speed multiplier while it's on.</summary>
        public virtual float MoveMult => 1f;
        /// <summary>Damage BIT takes multiplier while it's on.</summary>
        public virtual float DamageTakenMult => 1f;
        /// <summary>How far it reaches (the HUD and the range ring).</summary>
        public virtual float Reach => P.EffectiveRange;

        public virtual void Begin()
        {
            P.Look?.AddPerkGear("relic:" + Id);
            P.Look?.SetWeaponsHidden(true);
        }

        public virtual void End()
        {
            P.Look?.RemoveGear("relic:" + Id);
            P.Look?.SetWeaponsHidden(false);
        }

        /// <param name="aim">Where the mouse points while the left button is held (null: not aiming).</param>
        public abstract void Tick(float dt, Vector3? aim);

        public static BitWeapon Make(string id, VinePlayer p) => id switch
        {
            RelicRun.CryoFists => new CryoFists(p),
            RelicRun.StormLance => new StormLance(p),
            RelicRun.DroneBay => new DroneBay(p),
            _ => null,
        };

        // ── Helpers ──

        protected static Vector3 Flat(Vector3 v) { v.Y = 0; return v; }

        protected IEnumerable<VineEnemy> Alive() => Roster.Enemies(Tree).Where(e => GodotObject.IsInstanceValid(e) && e.IsAlive);

        protected VineEnemy Nearest(Vector3 from, float range, System.Func<VineEnemy, bool> ok = null)
        {
            VineEnemy best = null;
            float bd = range * range;
            foreach (var e in Alive())
            {
                if (ok != null && !ok(e)) continue;
                float d = Flat(e.GlobalPosition - from).LengthSquared();
                if (d <= bd) { bd = d; best = e; }
            }
            return best;
        }

        /// <summary>The first enemy close to the line from BIT along <paramref name="dir"/>, out to <paramref name="range"/>.</summary>
        protected VineEnemy AlongLine(Vector3 dir, float range)
        {
            VineEnemy hit = null;
            float best = range;
            foreach (var e in Alive())
            {
                var v = Flat(e.GlobalPosition - P.GlobalPosition);
                float along = v.Dot(dir);
                if (along < 0f || along > best) continue;
                float perp = (v - dir * along).Length();
                if (perp <= (e.IsBoss ? 1.1f : 0.7f) + along * Constants.BIT_AIM_SPREAD) { best = along; hit = e; }
            }
            return hit;
        }

        protected void Hit(VineEnemy e, float dmg, DamageKind kind)
        {
            float before = e.CurrentHealth;
            e.TakeDamage(dmg, kind);
            if (!e.IsAlive && before > 0f) P.CreditKill();
        }
    }

    /// <summary>
    /// Cryo Fists: BIT punches what's in reach (or what's in front of it while aiming), chilling it;
    /// three chills freeze an enemy solid, and a frozen enemy that dies shatters
    /// (<see cref="VineEnemy.Chill"/>). Each punch also clips enemies close behind the target.
    /// BIT takes less damage and moves faster, since it has to walk into the fight.
    /// </summary>
    public sealed class CryoFists : BitWeapon
    {
        private float _cd, _lungeCd;
        private bool _left;
        private VineEnemy _lungeAt;
        // Where BIT stopped: standing still, it guards this spot, lunging at what comes within
        // reach of it and stepping back when the fight is done
        private Vector3? _post;
        private float _quiet;
        public CryoFists(VinePlayer p) : base(RelicRun.CryoFists, p) { }

        /// <summary>The spot BIT guards while it stands still (null while moving).</summary>
        public Vector3? Post => _post;

        /// <summary>Held fire on an enemy this far away lunges BIT to it (it had to walk into the crowd).</summary>
        public float LungeRange => T("lungeRange", 9f);
        /// <summary>Lunges made (tests).</summary>
        public int Lunges { get; private set; }

        public override float MoveMult => 1f + T("moveBonus", 0.15f);
        public override float DamageTakenMult => T("damageTaken", 0.8f);
        public override float Reach => T("reach", 4f);
        public float PunchDamage => P.EffectiveDamage * T("damageMult", 2.6f);

        public override void Tick(float dt, Vector3? aim)
        {
            _cd -= dt;
            _lungeCd -= dt;
            float reach = Reach;
            // Landing a lunge: the first punch comes as BIT arrives
            if (_lungeAt != null)
            {
                bool valid = GodotObject.IsInstanceValid(_lungeAt) && _lungeAt.IsAlive;
                if (!valid) _lungeAt = null;
                else if (!P.IsDashing || Flat(_lungeAt.GlobalPosition - P.GlobalPosition).Length() <= reach)
                {
                    var t = _lungeAt;
                    _lungeAt = null;
                    if (Flat(t.GlobalPosition - P.GlobalPosition).Length() <= reach + 1f)
                    {
                        Punch(t, T("lungeHit", 1.6f));
                        _cd = 1f / Mathf.Max(0.1f, P.EffectiveAttackSpeed * T("rateMult", 0.85f));
                    }
                    return;
                }
                else return;
            }
            // Standing still is guarding: the post is where BIT stopped
            bool moving = P.SinceMoved < 0.3f && !P.IsDashing;
            if (moving) _post = null;
            else if (_post == null && !P.IsDashing) _post = P.GlobalPosition;

            // Held fire on an enemy out of reach: lunge to it
            VineEnemy far = null;
            if (aim != null && _lungeCd <= 0f)
                far = Nearest(aim.Value, 3.5f, e => Flat(e.GlobalPosition - P.GlobalPosition).Length() <= LungeRange);
            // Guarding (auto-fire, standing still): lunge at what comes near the post. Melee you
            // had to walk into and click enemies with was never a way to play
            else if (aim == null && GameSettings.BitAutoFire && _post != null && _lungeCd <= 0f && Nearest(P.GlobalPosition, reach) == null)
            {
                var post = _post.Value;
                float guard = T("guardRange", 7f);
                far = Nearest(post, guard, e => Flat(e.GlobalPosition - P.GlobalPosition).Length() <= LungeRange);
            }
            if (far != null && Flat(far.GlobalPosition - P.GlobalPosition).Length() > reach)
            {
                _lungeAt = far;
                _lungeCd = T("lungeCooldown", 1.1f);
                _quiet = 0f;
                P.Dash(far.GlobalPosition, T("lungeSpeed", 28f), reach * 0.5f);
                VfxFactory.SpawnEnergyBurst(Tree, P.GlobalPosition + Vector3.Up * 0.6f, Tint, 8);
                Lunges++;
                return;
            }
            // Nothing left near the post: step back to it
            if (_post != null && !P.IsDashing)
            {
                bool busy = Nearest(P.GlobalPosition, reach) != null;
                _quiet = busy ? 0f : _quiet + dt;
                if (_quiet > T("returnAfter", 1.5f) && Flat(_post.Value - P.GlobalPosition).Length() > 1.5f)
                {
                    _quiet = 0f;
                    var back = _post.Value;
                    P.Dash(back, T("returnSpeed", 12f), 0.2f);
                    _post = back; // the dash doesn't count as moving off the post
                }
            }
            if (_cd > 0f) return;
            VineEnemy target = null;
            if (aim != null)
            {
                var dir = Flat(aim.Value - P.GlobalPosition);
                if (dir.LengthSquared() > 0.01f)
                {
                    dir = dir.Normalized();
                    float cos = Mathf.Cos(T("cone", 0.55f) * Mathf.Pi);
                    target = Nearest(P.GlobalPosition, reach, e => Flat(e.GlobalPosition - P.GlobalPosition).Normalized().Dot(dir) >= cos)
                        ?? Nearest(P.GlobalPosition, reach * 0.5f);
                }
            }
            else if (GameSettings.BitAutoFire) target = Nearest(P.GlobalPosition, reach);
            if (target == null) return;
            Punch(target);
            _cd = 1f / Mathf.Max(0.1f, P.EffectiveAttackSpeed * T("rateMult", 0.85f));
        }

        private void Punch(VineEnemy target, float mult = 1f)
        {
            float dmg = PunchDamage * mult;
            var tree = Tree;
            _left = !_left;
            P.FaceToward(target.GlobalPosition, snap: true);
            var at = target.BodyCentre;
            P.PlayAttackClip(_left ? "L" : "R", at);
            var hand = P.Look?.SocketGlobal(_left ? "hand_l" : "hand_r") ?? P.GlobalPosition + Vector3.Up * 0.7f;
            VfxFactory.SpawnFrostPunch(tree, at, at - hand);
            VfxFactory.SpawnTracer(tree, hand, at, Tint, 40f);
            if (ServiceLocator.TryGet<TDCamera>(out var cam)) cam.Shake(0.12f, 0.06f);
            var cleave = Alive().Where(e => e != target && Flat(e.GlobalPosition - target.GlobalPosition).Length() <= T("cleaveRadius", 1.6f)).ToList();
            Strike(target, dmg);
            foreach (var e in cleave) Strike(e, dmg * T("cleaveShare", 0.4f));
            Attacks++;
            LastTarget = target;
        }

        private void Strike(VineEnemy e, float dmg)
        {
            if (!GodotObject.IsInstanceValid(e) || !e.IsAlive) return;
            // Chill first: the punch that freezes it and kills it shatters it
            e.Chill(T("chillSlow", 0.18f), T("chillTime", 3f), Mathf.RoundToInt(T("freezeAt", 3f)), T("freezeTime", 1.6f),
                PunchDamage * T("shatterHitCap", 4f), T("shatterHealthShare", 0.25f), T("shatterRadius", 3.5f));
            Hit(e, dmg, DamageKind.Heavy);
        }
    }

    /// <summary>
    /// Storm Lance: a beam of lightning from the lance's tip to the target (the nearest, or the one
    /// along the aim) that jumps on to more enemies near it, each jump weaker. Electric: shields go
    /// three times as fast, armour soaks most of it. Drains Materials while it fires; dry, it is a
    /// weak single beam.
    /// </summary>
    public sealed class StormLance : BitWeapon
    {
        private VineEnemy _target;
        private float _tick;
        public StormLance(VinePlayer p) : base(RelicRun.StormLance, p) { }

        // BIT's own reach (Spire upgrades) carries over: the lance used to stop at 9 whatever BIT
        // could reach, so a BIT standing behind its towers never touched anything
        public override float Reach => T("range", 14f) + (SpireStation.Current?.BitRangeBonus ?? 0f);
        /// <summary>How far a held beam reaches (longer than auto-fire, like BIT's aimed shots).</summary>
        public float AimReach => T("aimRange", 26f) + (SpireStation.Current?.BitRangeBonus ?? 0f);
        public bool Firing { get; private set; }
        public int LastJumps { get; private set; }
        /// <summary>Fizzles drawn because nothing was in reach (tests).</summary>
        public int Fizzles { get; private set; }
        private float _fizzle;
        public float Dps => P.EffectiveDamage * P.EffectiveAttackSpeed * T("dpsMult", 1.7f);

        public override void Tick(float dt, Vector3? aim)
        {
            float range = Reach;
            _fizzle -= dt;
            if (aim != null)
            {
                var dir = Flat(aim.Value - P.GlobalPosition);
                float far = AimReach;
                _target = null;
                if (dir.LengthSquared() > 0.01f)
                {
                    _target = AlongLine(dir.Normalized(), far)
                        ?? Nearest(aim.Value, 3f, e => Flat(e.GlobalPosition - P.GlobalPosition).Length() <= far);
                    // Nothing in reach: the lance crackles out toward the mouse and dies off at its
                    // reach, so holding fire always shows something and shows how far it goes
                    if (_target == null && _fizzle <= 0f)
                    {
                        _fizzle = 0.12f;
                        var tip0 = P.Look?.SocketGlobal("lance_tip") ?? P.Look?.SocketGlobal("hand_r") ?? P.GlobalPosition + Vector3.Up * 0.9f;
                        float len = Mathf.Min(dir.Length(), far);
                        var end = P.GlobalPosition + dir.Normalized() * len;
                        end.Y = tip0.Y - 0.3f;
                        P.FaceToward(aim.Value);
                        P.HoldAim("R", end);
                        var dim = Tint * 0.55f; dim.A = 1f;
                        VfxFactory.SpawnLanceBeam(Tree, tip0, end, dim);
                        Fizzles++;
                    }
                }
            }
            else if (GameSettings.BitAutoFire)
            {
                bool keep = _target != null && GodotObject.IsInstanceValid(_target) && _target.IsAlive
                    && Flat(_target.GlobalPosition - P.GlobalPosition).Length() <= range + 1f;
                if (!keep) _target = Nearest(P.GlobalPosition, range);
            }
            else _target = null;
            Firing = _target != null;
            if (!Firing) { _tick = 0f; return; }
            P.FaceToward(_target.GlobalPosition);
            P.HoldAim("R", _target.BodyCentre);
            _tick += dt;
            float step = T("tick", 0.1f);
            if (_tick < step) return;
            step = _tick;
            _tick = 0f;

            bool dry = P.CurrentMaterials <= 0.01f;
            if (!dry) P.SpendMaterials(T("materialsPerSecond", 5f) * step);
            float dmg = Dps * step * (dry ? T("dryShare", 0.3f) : 1f);
            var tree = Tree;
            // The lance arm points at the target; the beam leaves its tip
            P.HoldAim("R", _target.BodyCentre);
            var tip = P.Look?.SocketGlobal("lance_tip") ?? P.Look?.SocketGlobal("hand_r") ?? P.GlobalPosition + Vector3.Up * 0.9f;
            VfxFactory.SpawnLanceBeam(tree, tip, _target.BodyCentre, Tint);
            var hit = new HashSet<VineEnemy> { _target };
            var prev = _target;
            Hit(_target, dmg, DamageKind.Electric);
            int jumps = dry ? 0 : Mathf.RoundToInt(T("jumps", 5f));
            LastJumps = 0;
            for (int j = 0; j < jumps; j++)
            {
                var from = prev.GlobalPosition;
                var next = Nearest(from, T("jumpRange", 4.5f), e => !hit.Contains(e));
                if (next == null) break;
                dmg *= T("jumpFalloff", 0.75f);
                VfxFactory.SpawnLanceBeam(tree, prev.BodyCentre, next.BodyCentre, Tint);
                hit.Add(next);
                Hit(next, dmg, DamageKind.Electric);
                prev = next;
                LastJumps++;
            }
            Attacks++;
            LastTarget = _target;
        }
    }

    /// <summary>
    /// Drone Bay: BIT's gun is gone; hunter drones fly out to enemies near BIT and shoot them,
    /// spreading over the targets in reach (holding fire sends them all at what the mouse points
    /// at). One more drone every few levels. Idle, they orbit BIT.
    /// </summary>
    public sealed class DroneBay : BitWeapon
    {
        private readonly List<BitDrone> _drones = new();
        private float _clock;
        public DroneBay(VinePlayer p) : base(RelicRun.DroneBay, p) { }

        public IReadOnlyList<BitDrone> Drones => _drones;
        public override float Reach => T("range", 14f);
        public int Wanted
        {
            get
            {
                int level = P.Progression?.Level ?? 1;
                int per = Mathf.Max(1, Mathf.RoundToInt(T("perLevels", 3f)));
                return Mathf.Clamp(Mathf.RoundToInt(T("drones", 3f)) + (level - 1) / per, 1, Mathf.RoundToInt(T("max", 7f)));
            }
        }

        public override void End()
        {
            base.End();
            foreach (var d in _drones) if (GodotObject.IsInstanceValid(d)) d.QueueFree();
            _drones.Clear();
        }

        public override void Tick(float dt, Vector3? aim)
        {
            _clock += dt;
            _drones.RemoveAll(d => !GodotObject.IsInstanceValid(d));
            while (_drones.Count < Wanted)
            {
                var d = new BitDrone { Name = $"BitDrone{_drones.Count}", Tint = Tint, Speed = T("speed", 13f) };
                (P.GetParent() ?? P).AddChild(d);
                d.GlobalPosition = P.GlobalPosition + Vector3.Up * T("height", 1.9f);
                _drones.Add(d);
                VfxFactory.SpawnEnergyBurst(Tree, d.GlobalPosition, Tint, 6);
            }

            float range = Reach;
            VineEnemy focus = null;
            if (aim != null)
            {
                focus = Nearest(aim.Value, 3f, e => Flat(e.GlobalPosition - P.GlobalPosition).Length() <= Mathf.Max(range, P.AimedRange));
                if (focus == null)
                {
                    var dir = Flat(aim.Value - P.GlobalPosition);
                    if (dir.LengthSquared() > 0.01f) focus = AlongLine(dir.Normalized(), range);
                }
            }
            bool auto = GameSettings.BitAutoFire;
            var taken = new Dictionary<VineEnemy, int>();
            for (int i = 0; i < _drones.Count; i++)
            {
                var d = _drones[i];
                var t = focus;
                if (t == null && auto)
                {
                    bool keep = d.Target != null && GodotObject.IsInstanceValid(d.Target) && d.Target.IsAlive
                        && Flat(d.Target.GlobalPosition - P.GlobalPosition).Length() <= range;
                    t = keep ? d.Target : Nearest(P.GlobalPosition, range, e => taken.GetValueOrDefault(e) < 2);
                }
                d.Target = t;
                float a = i * Mathf.Tau / Mathf.Max(1, _drones.Count) + _clock * 0.9f;
                var ring = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                if (t != null)
                {
                    taken[t] = taken.GetValueOrDefault(t) + 1;
                    d.Desired = t.GlobalPosition + ring * 2.4f + Vector3.Up * (T("height", 1.9f) + 0.4f);
                    d.Cooldown -= dt;
                    if (d.Cooldown <= 0f && d.GlobalPosition.DistanceTo(t.BodyCentre) < 7f)
                    {
                        Shoot(d, t);
                        d.Cooldown = 1f / Mathf.Max(0.1f, P.EffectiveAttackSpeed * T("rateMult", 0.85f)) * (float)GD.RandRange(0.85, 1.15);
                    }
                }
                else d.Desired = P.GlobalPosition + ring * T("orbit", 1.7f) + Vector3.Up * T("height", 1.9f);
            }
        }

        private void Shoot(BitDrone d, VineEnemy t)
        {
            var from = d.GlobalPosition;
            VfxFactory.SpawnProjectile(Tree, from, t.BodyCentre, Tint, 32f, 0.6f);
            VfxFactory.SpawnMuzzleFlash(Tree, from, t.BodyCentre - from, Tint, 0.5f);
            d.Recoil();
            Hit(t, P.EffectiveDamage * T("damageShare", 0.42f), DamageKind.Heavy);
            Attacks++;
            LastTarget = t;
        }
    }

    /// <summary>One of the Drone Bay's drones: a small hull with a glowing ring and two rotors that flies to <see cref="Desired"/>.</summary>
    public partial class BitDrone : Node3D
    {
        public Vector3 Desired;
        public VineEnemy Target;
        public float Cooldown;
        public Color Tint = new(1f, 0.72f, 0.3f);
        public float Speed = 13f;
        private Node3D _body, _rotorA, _rotorB;
        private float _kick, _trail;
        private Vector3 _vel;

        private static Mesh _hullMesh, _ringMesh, _rotorMesh, _eyeMesh;
        private static StandardMaterial3D _hullMat;
        private static readonly Dictionary<Color, StandardMaterial3D> _glow = new();

        public override void _Ready()
        {
            AddToGroup("BitDrone");
            Desired = GlobalPosition;
            _hullMesh ??= new CylinderMesh { TopRadius = 0.16f, BottomRadius = 0.22f, Height = 0.12f, RadialSegments = 6, Rings = 1 };
            _ringMesh ??= new TorusMesh { InnerRadius = 0.2f, OuterRadius = 0.25f, Rings = 18, RingSegments = 6 };
            _rotorMesh ??= new BoxMesh { Size = new Vector3(0.42f, 0.015f, 0.05f) };
            _eyeMesh ??= new SphereMesh { Radius = 0.05f, Height = 0.1f, RadialSegments = 8, Rings = 4 };
            _hullMat ??= new StandardMaterial3D { AlbedoColor = new Color(0.16f, 0.17f, 0.2f), Metallic = 0.7f, Roughness = 0.35f, RimEnabled = true, Rim = 0.5f };
            if (!_glow.TryGetValue(Tint, out var glow))
            {
                glow = new StandardMaterial3D { AlbedoColor = Tint, EmissionEnabled = true, Emission = Tint, EmissionEnergyMultiplier = 2.6f, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
                _glow[Tint] = glow;
            }
            _body = new Node3D { Name = "Body" };
            AddChild(_body);
            Scale = Vector3.One * 1.7f; // they read at play distance
            _body.AddChild(new MeshInstance3D { Mesh = _hullMesh, MaterialOverride = _hullMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            _body.AddChild(new MeshInstance3D { Mesh = _ringMesh, MaterialOverride = glow, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            _body.AddChild(new MeshInstance3D { Mesh = _eyeMesh, MaterialOverride = glow, Position = new Vector3(0, -0.02f, 0.2f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            _rotorA = new MeshInstance3D { Mesh = _rotorMesh, MaterialOverride = _hullMat, Position = new Vector3(0, 0.1f, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            _rotorB = new MeshInstance3D { Mesh = _rotorMesh, MaterialOverride = _hullMat, Position = new Vector3(0, 0.1f, 0), RotationDegrees = new Vector3(0, 90, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            _body.AddChild(_rotorA);
            _body.AddChild(_rotorB);
        }

        public void Recoil() => _kick = 1f;

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            var to = Desired - GlobalPosition;
            // Spring toward where it wants to be: quick but with a little swing
            _vel = _vel.Lerp(to * 4f, Mathf.Min(1f, dt * 5f));
            if (_vel.Length() > Speed) _vel = _vel.Normalized() * Speed;
            GlobalPosition += _vel * dt;
            var look = Target != null && IsInstanceValid(Target) && Target.IsAlive ? Target.GlobalPosition - GlobalPosition : _vel;
            look.Y = 0;
            if (look.LengthSquared() > 0.001f)
            {
                float yaw = Mathf.Atan2(look.X, look.Z);
                Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, yaw, Mathf.Min(1f, dt * 8f)), 0);
            }
            // Bank into its motion, bob, kick back on a shot
            var local = GlobalTransform.Basis.Inverse() * _vel;
            _kick = Mathf.Max(0f, _kick - dt * 6f);
            _body.Rotation = new Vector3(Mathf.Clamp(local.Z * 0.05f, -0.4f, 0.4f) - _kick * 0.3f, 0, Mathf.Clamp(-local.X * 0.05f, -0.4f, 0.4f));
            _body.Position = new Vector3(0, Mathf.Sin(Time.GetTicksMsec() * 0.004f + GetInstanceId() % 7) * 0.05f, -_kick * 0.08f);
            _rotorA.RotateY(dt * 40f);
            _rotorB.RotateY(dt * 40f);
            if ((_trail -= dt) <= 0f)
            {
                _trail = 0.08f;
                if (VfxParticles.Get(GetTree()) is { } p)
                    p.Emit(VfxParticles.Kind.Glow, GlobalPosition - Vector3.Up * 0.05f, -_vel * 0.2f + Vector3.Down * 0.4f, Tint, 0.12f, 0.25f, -0.5f);
            }
        }
    }
}
