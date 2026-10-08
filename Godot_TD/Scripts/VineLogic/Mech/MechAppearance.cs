using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// What a player mech looks like as it levels and picks perks: its size, the parts each level
    /// adds and the gear each perk bolts on, all from its <see cref="MechSheet"/>. Parts hang off
    /// bone attachments, so they move with the animation, and wear the mech's own hull and glow
    /// materials, so they swap with it at the dome edge and flash with it when hit.
    /// </summary>
    public partial class MechAppearance : Node
    {
        /// <summary>A gun the mech fires from.</summary>
        public sealed class WeaponMount
        {
            public string Owner;          // perk id
            public MechPart Part;
            public Marker3D Muzzle;
        }

        private sealed class BuiltPart
        {
            public string Owner;          // perk id, or "level:N"
            public MechPart Part;
            public Node3D Pivot;
            public Vector3 TargetScale;
            public float PopTimer = -1f;  // counts up from 0 while popping in
        }

        private const float PopTime = 0.35f;

        private VinePlayer _player;
        private MechSheet _sheet;
        private Node3D _model;
        private Skeleton3D _skeleton;
        private bool _built;
        private readonly Dictionary<string, Node3D> _sockets = new();
        private readonly List<BuiltPart> _parts = new();
        private readonly List<WeaponMount> _weapons = new();
        private readonly HashSet<string> _gearOwners = new();
        private readonly List<string> _pendingGear = new();
        private int _weaponTurn;

        public int Level { get; private set; } = 1;
        /// <summary>Scale multiplier the level calls for.</summary>
        public float Growth => 1f + (_sheet?.Levels.GrowthPerLevel ?? 0f) * (Level - 1);
        /// <summary>The growth being drawn: eases toward <see cref="Growth"/> after a level-up.</summary>
        public float ShownGrowth { get; private set; } = 1f;
        public IReadOnlyList<WeaponMount> Weapons => _weapons;
        public bool IsBuilt => _built;

        /// <summary>Owner (perk id or "level:N") and pivot of every part, for tests.</summary>
        internal IEnumerable<(string owner, Node3D pivot, MechPart part)> Parts =>
            _parts.Select(p => (p.Owner, p.Pivot, p.Part));

        public void Init(VinePlayer player, MechSheet sheet)
        {
            _player = player;
            _sheet = sheet;
            Name = "MechAppearance";
        }

        /// <summary>
        /// Called once the model, its skeleton and its materials exist. Applies whatever level and
        /// gear arrived before then.
        /// </summary>
        public void Build()
        {
            if (_built || _player?.ModelRoot == null) return;
            _model = _player.ModelRoot;
            _skeleton = FindSkeleton(_model);
            _built = true;
            ApplyLevelParts(animate: false);
            foreach (var id in _pendingGear) AddGear(id, animate: false);
            _pendingGear.Clear();
            _player.SetLevelLook(Level, _sheet.Levels);
        }

        /// <summary>Bolt on the gear a perk brings (once per perk). Unknown perks change nothing.</summary>
        public void AddPerkGear(string perkId, bool animate = true)
        {
            if (string.IsNullOrEmpty(perkId) || _gearOwners.Contains(perkId) || _pendingGear.Contains(perkId)) return;
            if (!_built) { _pendingGear.Add(perkId); return; }
            AddGear(perkId, animate);
        }

        public void SetLevel(int level, bool animate = true)
        {
            Level = Mathf.Clamp(level, 1, Mathf.Max(1, _sheet?.Levels.Max ?? 1));
            if (!animate) ShownGrowth = Growth;
            if (!_built) return;
            ApplyLevelParts(animate);
            _player.SetLevelLook(Level, _sheet.Levels);
        }

        /// <summary>Back to a bare level-1 mech (tests and renders).</summary>
        internal void ResetAll()
        {
            foreach (var p in _parts)
                if (GodotObject.IsInstanceValid(p.Pivot)) p.Pivot.QueueFree();
            _parts.Clear();
            _weapons.Clear();
            _gearOwners.Clear();
            _pendingGear.Clear();
            _weaponTurn = 0;
            Level = 1;
            ShownGrowth = 1f;
            if (_built) _player.SetLevelLook(Level, _sheet.Levels);
        }

        /// <summary>Next gun in turn (round robin), or null when the mech has none.</summary>
        public WeaponMount TakeWeapon()
        {
            if (_weapons.Count == 0) return null;
            var w = _weapons[_weaponTurn % _weapons.Count];
            _weaponTurn++;
            return w;
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            ShownGrowth = Mathf.MoveToward(ShownGrowth, Growth, dt * 0.6f);
            foreach (var p in _parts)
            {
                if (!GodotObject.IsInstanceValid(p.Pivot)) continue;
                if (p.PopTimer >= 0f)
                {
                    p.PopTimer += dt;
                    float t = Mathf.Clamp(p.PopTimer / PopTime, 0f, 1f);
                    // Ease out with a little overshoot (easeOutBack), so the part snaps on
                    const float c1 = 1.70158f;
                    float s = 1f + (c1 + 1f) * Mathf.Pow(t - 1f, 3) + c1 * Mathf.Pow(t - 1f, 2);
                    p.Pivot.Scale = p.TargetScale * Mathf.Max(0.01f, s);
                    if (t >= 1f) { p.Pivot.Scale = p.TargetScale; p.PopTimer = -1f; }
                }
                if (p.Part.Spin != 0f)
                    p.Pivot.RotateObjectLocal(Vector3.Up, Mathf.DegToRad(p.Part.Spin) * dt);
            }
        }

        // ── Building ──

        private void ApplyLevelParts(bool animate)
        {
            foreach (var tier in _sheet.Levels.Tiers)
            {
                if (tier.Level > Level) continue;
                string owner = $"level:{tier.Level}";
                if (_gearOwners.Contains(owner)) continue;
                _gearOwners.Add(owner);
                foreach (var part in tier.Parts) BuildPart(owner, part, animate);
            }
        }

        private void AddGear(string perkId, bool animate)
        {
            _gearOwners.Add(perkId);
            if (!_sheet.Gear.TryGetValue(perkId, out var parts)) return;
            foreach (var part in parts) BuildPart(perkId, part, animate);
            if (animate && _model != null)
                VfxFactory.SpawnEnergyBurst(_player.GetTree(), _model.GlobalPosition + Vector3.Up * 0.8f,
                    BitPalette.Accent, 6);
        }

        private void BuildPart(string owner, MechPart part, bool animate)
        {
            var socket = Socket(part.Socket);
            if (socket == null) { GD.PushWarning($"[Mech] {owner}: no socket '{part.Socket}'"); return; }

            var pivot = new Node3D { Name = $"Part_{owner.Replace(':', '_')}_{_parts.Count}" };
            pivot.Position = V(part.Offset);
            pivot.RotationDegrees = V(part.Rot);
            socket.AddChild(pivot);

            Node3D body = !string.IsNullOrEmpty(part.Model) ? FitModel(part.Model, part.Fit) : Shape(part);
            if (body == null) { pivot.QueueFree(); return; }
            pivot.AddChild(body);
            if (part.Scale != null && part.Scale.Length == 3) body.Scale *= V(part.Scale);

            // hull: BIT's dark body and outline. glow: its accent glow. metal: lit gunmetal (kit
            // models are dense, and an outline on every small piece turns them into a white smear).
            // kit: the model's own materials, untouched.
            bool kitModel = !string.IsNullOrEmpty(part.Model);
            float outline = part.Outline >= 0f ? part.Outline : kitModel ? 0.25f : 1f;
            float tone = part.Tone >= 0f ? part.Tone : 0.05f;
            if (part.Surface != "kit")
            {
                foreach (var m in body.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
                    _player.RegisterGearMesh(m, part.Surface, outline, tone);
                if (body is MeshInstance3D single) _player.RegisterGearMesh(single, part.Surface, outline, tone);
            }

            if (part.IsWeapon)
            {
                var muzzle = new Marker3D { Name = "Muzzle", Position = V(part.Muzzle) };
                pivot.AddChild(muzzle);
                _weapons.Add(new WeaponMount { Owner = owner, Part = part, Muzzle = muzzle });
            }

            var built = new BuiltPart { Owner = owner, Part = part, Pivot = pivot, TargetScale = pivot.Scale };
            if (animate) { built.PopTimer = 0f; pivot.Scale = built.TargetScale * 0.01f; }
            _parts.Add(built);
        }

        /// <summary>
        /// A mount point whose axes match the model's at rest: X right-to-left, Y up, Z forward.
        /// On a bone, it rides a BoneAttachment3D, offset so the rest pose lands on the socket.
        /// </summary>
        private Node3D Socket(string name)
        {
            if (string.IsNullOrEmpty(name) || _model == null) return null;
            if (_sockets.TryGetValue(name, out var existing)) return existing;
            if (!_sheet.Sockets.TryGetValue(name, out var def)) return null;

            var holder = new Node3D { Name = $"Socket_{name}" };
            var offset = V(def.Offset);
            int bone = _skeleton != null && !string.IsNullOrEmpty(def.Bone) ? _skeleton.FindBone(def.Bone) : -1;
            if (bone < 0)
            {
                holder.Position = offset;
                _model.AddChild(holder);
            }
            else
            {
                var att = _skeleton.GetChildren().OfType<BoneAttachment3D>().FirstOrDefault(a => a.BoneName == def.Bone);
                if (att == null)
                {
                    att = new BoneAttachment3D { BoneName = def.Bone, Name = $"Bone_{def.Bone.Replace('.', '_')}" };
                    _skeleton.AddChild(att);
                }
                att.AddChild(holder);
                // Rest pose of the bone in the model root's space; the holder undoes it
                var skelInModel = RelativeTransform(_model, _skeleton);
                var restInModel = skelInModel * _skeleton.GetBoneGlobalRest(bone);
                holder.Transform = restInModel.AffineInverse() * new Transform3D(Basis.Identity, offset);
            }
            _sockets[name] = holder;
            return holder;
        }

        /// <summary>A kit model centred on its own middle, longest side scaled to <paramref name="fit"/>.</summary>
        private static Node3D FitModel(string path, float fit)
        {
            var model = AssetLibrary.Instantiate(path);
            if (model == null) return null;
            var aabb = AssetLibrary.GetCombinedAABB(model);
            float longest = Mathf.Max(aabb.Size.X, Mathf.Max(aabb.Size.Y, aabb.Size.Z));
            if (longest < 0.0001f) return model;
            var c = aabb.GetCenter();
            foreach (var child in model.GetChildren())
                if (child is Node3D n) n.Position -= c;
            model.Scale = Vector3.One * (fit / longest);
            return model;
        }

        private static Node3D Shape(MechPart part)
        {
            float a = part.Size.Length > 0 ? part.Size[0] : 0.1f;
            float b = part.Size.Length > 1 ? part.Size[1] : a;
            float c = part.Size.Length > 2 ? part.Size[2] : a;
            Mesh mesh = part.Shape switch
            {
                "box" => new BoxMesh { Size = new Vector3(a, b, c) },
                "sphere" => new SphereMesh { Radius = a, Height = a * 2f, RadialSegments = 16, Rings = 8 },
                "cylinder" => new CylinderMesh { TopRadius = a, BottomRadius = a, Height = b, RadialSegments = 16 },
                "capsule" => new CapsuleMesh { Radius = a, Height = Mathf.Max(b, a * 2f), RadialSegments = 16 },
                "torus" => new TorusMesh { InnerRadius = a, OuterRadius = b, Rings = 24, RingSegments = 8 },
                _ => null,
            };
            if (mesh == null) { GD.PushWarning($"[Mech] Unknown shape '{part.Shape}'"); return null; }
            return new MeshInstance3D { Mesh = mesh, Name = part.Shape };
        }

        private static Transform3D RelativeTransform(Node3D root, Node3D node)
        {
            var t = Transform3D.Identity;
            for (Node n = node; n != null && n != root; n = n.GetParent())
                if (n is Node3D n3) t = n3.Transform * t;
            return t;
        }

        private static Skeleton3D FindSkeleton(Node root)
        {
            if (root is Skeleton3D s) return s;
            foreach (var child in root.GetChildren())
            {
                var found = FindSkeleton(child);
                if (found != null) return found;
            }
            return null;
        }

        private static Vector3 V(float[] a) =>
            a != null && a.Length == 3 ? new Vector3(a[0], a[1], a[2]) : Vector3.Zero;
    }
}
