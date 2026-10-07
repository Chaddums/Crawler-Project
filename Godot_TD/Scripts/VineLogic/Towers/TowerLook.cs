using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// A tower's model built from its <see cref="TowerSheet"/>: a pedestal, kit models and
    /// shapes in the kit's own textures, a lit band in the tower's colour. At run time the
    /// aiming parts turn toward whatever the tower is shooting (and scan slowly when idle),
    /// kick back when it fires, rams punch out, dishes spin, and wall blocks join up with the
    /// walls next to them. Shots leave from <see cref="MuzzleGlobal"/>.
    /// </summary>
    public partial class TowerLook : Node3D
    {
        private TowerSheet _sheet;
        private Node3D _aim;     // turns about the cell's centre
        private Node3D _kick;    // inside _aim: recoils along the barrel
        private readonly List<(Node3D node, float speed)> _spinners = new();
        private readonly List<(Node3D node, Vector3 rest, float dist)> _rams = new();
        private readonly List<(Node3D node, Vector2I dir, bool link)> _wallParts = new();

        private Vector3 _muzzle;        // in _kick space
        private float _forwardYaw;      // where the barrel points in _aim space (radians, from +Z toward +X)
        private float _yaw;
        private float _restYaw;         // degrees
        private bool _canAim;
        private Node3D _target;
        private float _trackTime;
        private float _kickT = 1f;
        private float _clock;
        private float _wallTimer;

        /// <summary>The parts that turn (null-safe: towers without any never turn).</summary>
        internal Node3D AimNode => _aim;
        internal bool CanAim => _canAim;
        internal TowerSheet Sheet => _sheet;
        /// <summary>Wall faces and links with the side they belong to (for tests).</summary>
        internal IEnumerable<(Node3D part, Vector2I dir, bool link)> WallParts => _wallParts;
        /// <summary>Current barrel direction, degrees from +Z toward +X.</summary>
        internal float BarrelYawDegrees => Mathf.RadToDeg(_yaw + _forwardYaw);

        /// <summary>Where shots leave the tower, in world space.</summary>
        public Vector3 MuzzleGlobal => IsInsideTree() ? _kick.GlobalTransform * _muzzle : GlobalPosition + _muzzle;

        // ── Build ──

        private static readonly Dictionary<string, (float yaw, Vector3 muzzle)> _barrelCache = new();

        /// <param name="restYaw">Idle barrel direction in degrees, overriding the sheet's.</param>
        public static TowerLook Build(TowerSheet sheet, Color tint, float? restYaw = null)
        {
            var look = new TowerLook { Name = "TowerModel", _sheet = sheet, _restYaw = restYaw ?? sheet.RestYaw };
            look._aim = new Node3D { Name = "Aim" };
            look._kick = new Node3D { Name = "Kick" };
            look._aim.AddChild(look._kick);
            look.AddChild(look._aim);

            float top = sheet.Pedestal != null ? look.BuildPedestal(sheet.Pedestal, tint) : 0f;
            foreach (var part in sheet.Parts)
                look.AddPart(part, tint, top);

            look.FindBarrel();
            look._yaw = Mathf.DegToRad(look._restYaw) - look._forwardYaw;
            look._aim.Rotation = new Vector3(0, look._yaw, 0);
            return look;
        }

        private float BuildPedestal(TowerPedestal p, Color tint)
        {
            const float skirtH = 0.12f, capH = 0.06f, bandH = 0.05f;
            float r = p.Radius, h = p.Height;
            float bodyH = Mathf.Max(0.05f, h - skirtH - capH);
            var ped = new Node3D { Name = "Pedestal" };
            AddChild(ped);
            Add(ped, Prism(p.Sides, r + 0.14f, r + 0.1f, skirtH), Surface(p.Skirt, tint), new Vector3(0, skirtH / 2f, 0));
            Add(ped, Prism(p.Sides, r, r - 0.06f, bodyH), Surface(p.Body, tint), new Vector3(0, skirtH + bodyH / 2f, 0));
            if (p.Band)
                Add(ped, Prism(p.Sides, r - 0.045f, r - 0.045f, bandH), Surface("glow", tint, 0.7f),
                    new Vector3(0, skirtH + bodyH - bandH / 2f, 0));
            Add(ped, Prism(p.Sides, r - 0.03f, r - 0.09f, capH), Surface(p.Cap, tint), new Vector3(0, h - capH / 2f, 0));
            return h;
        }

        private void AddPart(TowerPart part, Color tint, float top)
        {
            Node3D body = !string.IsNullOrEmpty(part.Model) ? FitModel(part) : Shape(part, tint);
            if (body == null) return;
            var wrap = new Node3D { Name = body.Name + "Part" };
            wrap.Position = V(part.Pos) + (part.OnPedestal ? new Vector3(0, top, 0) : Vector3.Zero);
            wrap.RotationDegrees = V(part.Rot);
            wrap.AddChild(body);

            bool aims = part.Aim || part.Punch > 0f;
            (aims ? _kick : this).AddChild(wrap);
            if (part.Spin != 0f) _spinners.Add((wrap, Mathf.DegToRad(part.Spin)));
            if (part.Punch > 0f) _rams.Add((wrap, wrap.Position, part.Punch));
            if (Side(part.Face) is Vector2I f) _wallParts.Add((wrap, f, false));
            if (Side(part.Link) is Vector2I l) { _wallParts.Add((wrap, l, true)); wrap.Visible = false; }

            // Named sub-meshes of a kit model that turn while the rest of it stays put
            if (part.AimNodes.Length > 0)
                foreach (var n in Matching(body, part.AimNodes))
                {
                    var rel = RelativeTransform(this, n);
                    n.GetParent().RemoveChild(n);
                    _kick.AddChild(n);
                    n.Transform = rel; // _aim and _kick are at identity while building
                }
        }

        /// <summary>A kit model with its pivot (or middle) on the origin and its base on y = 0.</summary>
        private static Node3D FitModel(TowerPart part)
        {
            var model = AssetLibrary.Instantiate(part.Model);
            if (model == null) return null;
            foreach (var n in Matching(model, part.Hide))
            {
                n.GetParent().RemoveChild(n);
                n.Free();
            }
            var b = Bounds(model, null);
            if (b.Size == Vector3.Zero) return model;
            float s = part.Scale > 0f ? part.Scale
                : part.Fit > 0f ? part.Fit / Mathf.Max(b.Size.X, b.Size.Z)
                : part.FitHeight > 0f ? part.FitHeight / b.Size.Y
                : 1f;
            var pc = string.IsNullOrEmpty(part.Pivot) ? b.GetCenter() : Bounds(model, part.Pivot).GetCenter();
            model.Scale = Vector3.One * s;
            model.Position = new Vector3(-pc.X * s, -b.Position.Y * s, -pc.Z * s);
            var holder = new Node3D { Name = System.IO.Path.GetFileNameWithoutExtension(part.Model).Replace("KB3D_FTW_", "") };
            holder.AddChild(model);
            return holder;
        }

        private static Node3D Shape(TowerPart part, Color tint)
        {
            float a = part.Size.Length > 0 ? part.Size[0] : 0.2f;
            float b = part.Size.Length > 1 ? part.Size[1] : a;
            float c = part.Size.Length > 2 ? part.Size[2] : a;
            Mesh mesh = part.Shape switch
            {
                "box" => new BoxMesh { Size = new Vector3(a, b, c) },
                "cylinder" => new CylinderMesh { BottomRadius = a, TopRadius = part.Size.Length > 2 ? c : a, Height = b, RadialSegments = Mathf.Max(part.Sides, 6) },
                "prism" => Prism(part.Sides, a, part.Size.Length > 2 ? c : a, b),
                "sphere" => new SphereMesh { Radius = a, Height = a * 2f, RadialSegments = 20, Rings = 10 },
                "dome" => new SphereMesh { Radius = a, Height = a, IsHemisphere = true, RadialSegments = 20, Rings = 6 },
                "torus" => new TorusMesh { InnerRadius = a, OuterRadius = b, Rings = 24, RingSegments = 8 },
                "capsule" => new CapsuleMesh { Radius = a, Height = Mathf.Max(b, a * 2f), RadialSegments = 16 },
                _ => null,
            };
            if (mesh == null) { GD.PushWarning($"[Tower] Unknown shape '{part.Shape}'"); return null; }
            var mi = new MeshInstance3D { Mesh = mesh, Name = part.Shape, MaterialOverride = Surface(part.Surface, tint, part.Glow) };
            // Prisms put a flat face, not a corner, toward +Z
            if (part.Shape == "prism") mi.RotationDegrees = new Vector3(0, 180f / Mathf.Max(part.Sides, 3), 0);
            return mi;
        }

        /// <summary>Where the barrel points and its tip: the aiming mesh vertex furthest from the pivot.</summary>
        private void FindBarrel()
        {
            var meshes = new List<MeshInstance3D>();
            Collect(_kick, meshes);
            _canAim = meshes.Count > 0 && _sheet.AimSpeed > 0f;
            if (_sheet.Muzzle.Length >= 3)
            {
                _muzzle = V(_sheet.Muzzle);
                _forwardYaw = Mathf.Atan2(_muzzle.X, _muzzle.Z);
                return;
            }
            if (!_canAim)
            {
                // Nothing turns: shots leave from the top middle
                var all = AssetLibrary.GetCombinedAABB(this);
                _muzzle = new Vector3(0, all.End.Y, 0);
                _forwardYaw = 0f;
                return;
            }
            if (!_barrelCache.TryGetValue(_sheet.Id, out var hit))
            {
                float best = -1f;
                var tip = Vector3.Zero;
                foreach (var m in meshes)
                {
                    if (m.Mesh == null) continue;
                    var xf = RelativeTransform(_kick, m);
                    foreach (var v in m.Mesh.GetFaces())
                    {
                        var w = xf * v;
                        float d = w.X * w.X + w.Z * w.Z;
                        if (d > best) { best = d; tip = w; }
                    }
                }
                // Kit barrels lie along the model's axes; the tip itself can sit off the pivot's
                // line (the plasma gun's is to one side), which skewed the aim by ten degrees
                float yaw = Mathf.Round(Mathf.Atan2(tip.X, tip.Z) / (Mathf.Pi / 2f)) * (Mathf.Pi / 2f);
                hit = (yaw, tip);
                _barrelCache[_sheet.Id] = hit;
            }
            _forwardYaw = hit.yaw;
            _muzzle = hit.muzzle;
        }

        // ── Run time ──

        /// <summary>Turn toward this target for the next moment (call again each shot).</summary>
        public void Track(Node3D target)
        {
            if (target == null) return;
            _target = target;
            _trackTime = 1.6f;
        }

        /// <summary>The tower just fired: recoil and rams.</summary>
        public void Fire() => _kickT = 0f;

        public override void _Ready()
        {
            // Idle scans out of step from tower to tower
            _clock = (GlobalPosition.X * 0.37f + GlobalPosition.Z * 0.61f) % 6.283f;
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _clock += dt;
            foreach (var (node, speed) in _spinners)
                node.RotateY(speed * dt);

            if (_canAim) UpdateAim(dt);

            if (_kickT < 1f)
            {
                _kickT = Mathf.Min(1f, _kickT + dt / 0.35f);
                // Snap out over the first sixth, ease back over the rest
                float k = _kickT < 0.16f ? _kickT / 0.16f : 1f - Mathf.SmoothStep(0.16f, 1f, _kickT);
                var fwd = new Vector3(Mathf.Sin(_forwardYaw), 0, Mathf.Cos(_forwardYaw));
                _kick.Position = -fwd * _sheet.Recoil * k;
                foreach (var (node, rest, dist) in _rams)
                    node.Position = rest + fwd * dist * k;
            }

            if (_wallParts.Count > 0 && (_wallTimer -= dt) <= 0f)
            {
                _wallTimer = 0.4f;
                UpdateWall();
            }
        }

        private void UpdateAim(float dt)
        {
            float desired;
            _trackTime -= dt;
            if (_trackTime > 0f && _target != null && IsInstanceValid(_target) && _target.IsInsideTree())
            {
                var d = _target.GlobalPosition - _aim.GlobalPosition;
                desired = Mathf.Atan2(d.X, d.Z) - _forwardYaw;
            }
            else
            {
                _target = null;
                desired = Mathf.DegToRad(_restYaw + _sheet.IdleSweep * Mathf.Sin(_clock * 0.45f)) - _forwardYaw;
            }
            float speed = Mathf.DegToRad(_target != null ? _sheet.AimSpeed : 25f);
            float diff = Mathf.Wrap(desired - _yaw, -Mathf.Pi, Mathf.Pi);
            _yaw += Mathf.Clamp(diff, -speed * dt, speed * dt);
            _aim.Rotation = new Vector3(0, _yaw, 0);
        }

        /// <summary>Wall faces show only where no wall is next door; links only where one is.</summary>
        private void UpdateWall()
        {
            if (GetParent() is not VineNode node || !ServiceLocator.TryGet<VineGrid>(out var grid)) return;
            foreach (var (part, dir, link) in _wallParts)
            {
                var n = grid.GetNode(node.GridPosition + dir);
                bool wallThere = n != null && n != node && n.Data?.Type == VineNodeType.BarrierWall && !n.IsDestroyed;
                part.Visible = link ? wallThere : !wallThere;
            }
        }

        // ── Materials ──

        private const string Tex = "res://Models/Buildings/KB3D_FTW_BldgSmCheckPoint_A_grp_KB3D_FTW_";
        private static readonly Dictionary<string, (string set, float tile)> _textured = new()
        {
            { "concrete", ("ConcreteMetalPanels", 0.9f) },
            { "panel", ("MetalPanelWornGrayA", 1.1f) },
            { "trim", ("MetallicDarkGrayTrimA", 1.0f) },
            { "hazard", ("TrimMetalPanelsWornYellowA", 1.0f) },
            { "white", ("MetallicPWhiteA", 1.0f) },
            { "dark", ("MetallicDarkGrayA", 1.0f) },
        };
        private static readonly Dictionary<string, Texture2D> _tex = new();

        private static Texture2D LoadTex(string path)
        {
            if (_tex.TryGetValue(path, out var t)) return t;
            t = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
            _tex[path] = t;
            return t;
        }

        /// <summary>A fresh material per part (hit flashes change materials in place).</summary>
        internal static StandardMaterial3D Surface(string name, Color tint, float glow = 0.8f)
        {
            if (_textured.TryGetValue(name ?? "", out var set))
            {
                var m = new StandardMaterial3D
                {
                    AlbedoTexture = LoadTex($"{Tex}{set.set}_basecolor.jpg"),
                    Uv1Triplanar = true,
                    Uv1Scale = Vector3.One * set.tile,
                    Roughness = 1f,
                    Metallic = 1f,
                };
                var mr = LoadTex($"{Tex}{set.set}_metallic-KB3D_FTW_{set.set}_roughness.jpg");
                if (mr != null)
                {
                    m.MetallicTexture = mr;
                    m.MetallicTextureChannel = BaseMaterial3D.TextureChannel.Blue;
                    m.RoughnessTexture = mr;
                    m.RoughnessTextureChannel = BaseMaterial3D.TextureChannel.Green;
                }
                else { m.Metallic = 0.5f; m.Roughness = 0.6f; }
                var nm = LoadTex($"{Tex}{set.set}_normal.jpg");
                if (nm != null) { m.NormalEnabled = true; m.NormalTexture = nm; }
                if (m.AlbedoTexture == null) m.AlbedoColor = new Color(0.3f, 0.3f, 0.33f);
                return m;
            }
            return name switch
            {
                "glow" => new StandardMaterial3D
                {
                    AlbedoColor = tint, EmissionEnabled = true, Emission = tint,
                    EmissionEnergyMultiplier = glow, Roughness = 0.4f,
                },
                "copper" => new StandardMaterial3D { AlbedoColor = new Color(0.6f, 0.34f, 0.17f), Metallic = 0.9f, Roughness = 0.32f },
                "chrome" => new StandardMaterial3D { AlbedoColor = new Color(0.78f, 0.8f, 0.83f), Metallic = 1f, Roughness = 0.18f },
                "tar" => new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.035f, 0.03f, 0.045f), Metallic = 0.2f, Roughness = 0.08f,
                    EmissionEnabled = true, Emission = tint, EmissionEnergyMultiplier = 0.05f,
                },
                _ => new StandardMaterial3D { AlbedoColor = new Color(0.14f, 0.145f, 0.16f), Metallic = 0.7f, Roughness = 0.4f },
            };
        }

        // ── Helpers ──

        private static MeshInstance3D Add(Node3D parent, Mesh mesh, Material mat, Vector3 pos)
        {
            var mi = new MeshInstance3D { Mesh = mesh, MaterialOverride = mat, Position = pos };
            parent.AddChild(mi);
            return mi;
        }

        /// <summary>An n-sided prism (flat faces toward the axes for 4 and 8 sides).</summary>
        private static Mesh Prism(int sides, float bottom, float top, float height)
        {
            sides = Mathf.Max(sides, 3);
            // A cylinder with a corner on +X; turning the vertices half a side puts a face there
            var cyl = new CylinderMesh { BottomRadius = bottom, TopRadius = top, Height = height, RadialSegments = sides, Rings = 1 };
            var arrays = cyl.GetMeshArrays();
            var verts = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var norms = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
            var rot = new Basis(Vector3.Up, Mathf.Pi / sides);
            for (int i = 0; i < verts.Length; i++) { verts[i] = rot * verts[i]; norms[i] = rot * norms[i]; }
            arrays[(int)Mesh.ArrayType.Vertex] = verts;
            arrays[(int)Mesh.ArrayType.Normal] = norms;
            var tangents = arrays[(int)Mesh.ArrayType.Tangent].AsFloat32Array();
            for (int i = 0; i + 3 < tangents.Length; i += 4)
            {
                var t = rot * new Vector3(tangents[i], tangents[i + 1], tangents[i + 2]);
                tangents[i] = t.X; tangents[i + 1] = t.Y; tangents[i + 2] = t.Z;
            }
            arrays[(int)Mesh.ArrayType.Tangent] = tangents;
            var mesh = new ArrayMesh();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            return mesh;
        }

        private static Vector3 V(float[] a) => a.Length >= 3 ? new Vector3(a[0], a[1], a[2]) : Vector3.Zero;

        private static Vector2I? Side(string s) => s switch
        {
            "+x" => new Vector2I(1, 0),
            "-x" => new Vector2I(-1, 0),
            "+z" => new Vector2I(0, 1),
            "-z" => new Vector2I(0, -1),
            _ => null,
        };

        /// <summary>Top-most descendants whose names contain any of the tokens.</summary>
        private static List<Node3D> Matching(Node root, string[] tokens)
        {
            var found = new List<Node3D>();
            if (tokens.Length == 0) return found;
            void Walk(Node n)
            {
                foreach (var c in n.GetChildren())
                {
                    if (c is Node3D c3 && System.Array.Exists(tokens, t => c3.Name.ToString().Contains(t)))
                        found.Add(c3);
                    else Walk(c);
                }
            }
            Walk(root);
            return found;
        }

        /// <summary>Bounds of the meshes (under nodes named like <paramref name="only"/>, if given) in root's space.</summary>
        private static Aabb Bounds(Node3D root, string only)
        {
            var box = new Aabb();
            bool first = true;
            void Walk(Node n, bool inside)
            {
                inside |= only == null || n.Name.ToString().Contains(only);
                if (inside && n is MeshInstance3D mi && mi.Mesh != null)
                {
                    var b = RelativeTransform(root, mi) * mi.GetAabb();
                    box = first ? b : box.Merge(b);
                    first = false;
                }
                foreach (var c in n.GetChildren()) Walk(c, inside);
            }
            Walk(root, false);
            return box;
        }

        private static void Collect(Node n, List<MeshInstance3D> into)
        {
            if (n is MeshInstance3D mi && mi.Visible) into.Add(mi);
            foreach (var c in n.GetChildren()) Collect(c, into);
        }

        /// <summary>node's transform in root's space (root's own transform left out).</summary>
        private static Transform3D RelativeTransform(Node3D root, Node3D node)
        {
            var t = Transform3D.Identity;
            for (Node n = node; n != null && n != root; n = n.GetParent())
                if (n is Node3D n3) t = n3.Transform * t;
            return t;
        }
    }
}
