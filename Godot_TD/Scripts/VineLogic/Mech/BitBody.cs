using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// BIT's body: the Mike robot (sparkplug.fbx, Quaternius robot rig) in BIT's colours. Until
    /// 2026-10 BIT was the round LilRobot, whose stub arms couldn't hold a relic weapon: ice
    /// gauntlets floated beside it and the lance hung off its side.
    ///
    /// The model rests in a T-pose 5.35 units tall facing +Z (right arm along -X), so gear in
    /// Data/Mechs/bit.json is authored in those units: a hand socket's X axis runs along the
    /// forearm, and parts follow the bone when the arm comes down.
    /// </summary>
    public static class BitBody
    {
        public const string Model = AssetLibrary.PLAYER_SPARKPLUG;
        /// <summary>The texture recoloured into BIT's hull (dark panels, cyan paint as the glowing accents).</summary>
        public const string Texture = "res://Models/Characters/Player/Textures/Mike_4_Texture.png";

        /// <summary>
        /// Clips BIT plays, by the names VinePlayer asks for, from the pack's clips. "_L" clips are
        /// the right-arm clips mirrored (the pack only punches and shoots with the right).
        /// </summary>
        private static readonly (string name, string source, bool loop, bool mirror)[] Clips =
        {
            ("Idle", "Idle", true, false),
            ("Walk", "Walk", true, false),
            ("Run", "Run", true, false),
            ("Attack", "Punch", false, false),
            ("Attack_R", "Punch", false, false),
            ("Attack_L", "Punch", false, true),
            ("Shoot_R", "Shoot", false, false),
            ("Shoot_L", "Shoot", false, true),
            ("Slash", "SwordSlash", false, false),
            ("Hit", "HitRecieve_1", false, false),
            ("Death", "Death", false, false),
            ("Cheer", "Dance", true, false),
        };

        // Mirrored clips are worked out once a session (the source clips are shared resources)
        private static readonly Dictionary<Animation, Animation> _mirrored = new();

        /// <summary>Add BIT's clips to the model's AnimationPlayer (copies: the pack's own stay as they are).</summary>
        public static void PrepareClips(Node3D model)
        {
            var ap = model.FindChildren("*", "AnimationPlayer", true, false).OfType<AnimationPlayer>().FirstOrDefault();
            var skel = model.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().FirstOrDefault();
            if (ap == null || skel == null) return;
            AnimationLibrary lib;
            if (ap.HasAnimationLibrary("")) lib = ap.GetAnimationLibrary("");
            else { lib = new AnimationLibrary(); ap.AddAnimationLibrary("", lib); }
            if (lib.HasAnimation("Shoot_L")) return;
            // Changing the library under a clip that is playing crashed the engine on the next Play
            ap.Stop();

            var all = ap.GetAnimationList();
            Animation Source(string clip)
            {
                string full = all.FirstOrDefault(a => a == clip || a.EndsWith("|" + clip));
                return full != null ? ap.GetAnimation(full) : null;
            }

            foreach (var (name, source, loop, mirror) in Clips)
            {
                var src = Source(source);
                if (src == null) { GD.PushWarning($"[BitBody] no '{source}' clip"); continue; }
                Animation clip;
                if (mirror)
                {
                    if (!_mirrored.TryGetValue(src, out var m)) _mirrored[src] = m = Mirror(src, skel, model);
                    clip = (Animation)m.Duplicate();
                }
                else clip = (Animation)src.Duplicate();
                clip.LoopMode = loop ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
                if (lib.HasAnimation(name)) lib.RemoveAnimation(name);
                lib.AddAnimation(name, clip);
            }
            // The player cached its tracks from the old library; playing a new clip before it
            // rebuilt them crashed the engine
            ap.ClearCaches();
        }

        /// <summary>
        /// The clip played on the other side: each bone takes its partner's motion (.L and .R
        /// swapped) reflected across the body's middle. Skinning, not the bone axes, is mirrored:
        /// for a bone b with partner a, b's skin transform is M·skin(a)·M (M flips the model's X),
        /// so the mesh moves as the mirror image whatever way the rig's bones are rolled.
        /// </summary>
        internal static Animation Mirror(Animation src, Skeleton3D skel, Node3D model)
        {
            int n = skel.GetBoneCount();
            var partner = new int[n];
            var rot = Enumerable.Repeat(-1, n).ToArray();
            var pos = Enumerable.Repeat(-1, n).ToArray();
            for (int i = 0; i < n; i++)
            {
                string name = skel.GetBoneName(i);
                string other = name.EndsWith(".L") ? name[..^2] + ".R" : name.EndsWith(".R") ? name[..^2] + ".L" : name;
                int j = skel.FindBone(other);
                partner[i] = j >= 0 ? j : i;
            }
            string prefix = null;
            for (int t = 0; t < src.GetTrackCount(); t++)
            {
                var path = src.TrackGetPath(t);
                string bone = path.GetConcatenatedSubNames();
                int b = skel.FindBone(bone);
                if (b < 0) continue;
                prefix ??= path.GetConcatenatedNames();
                if (src.TrackGetType(t) == Animation.TrackType.Rotation3D) rot[b] = t;
                else if (src.TrackGetType(t) == Animation.TrackType.Position3D) pos[b] = t;
            }
            var dst = new Animation { Length = src.Length, LoopMode = src.LoopMode };
            if (prefix == null) return dst;

            // The model's X flip, in the skeleton's own space
            var skelBasis = Relative(model, skel).Basis.Orthonormalized();
            var flip = new Basis(new Vector3(-1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, 1));
            var m = new Transform3D(skelBasis.Inverse() * flip * skelBasis, Vector3.Zero);

            var restLocal = new Transform3D[n];
            var restGlobal = new Transform3D[n];
            var parent = new int[n];
            for (int i = 0; i < n; i++)
            {
                restLocal[i] = skel.GetBoneRest(i);
                restGlobal[i] = skel.GetBoneGlobalRest(i);
                parent[i] = skel.GetBoneParent(i);
            }
            var order = new List<int>();
            var done = new bool[n];
            void Visit(int i) { if (done[i]) return; if (parent[i] >= 0) Visit(parent[i]); done[i] = true; order.Add(i); }
            for (int i = 0; i < n; i++) Visit(i);

            // A bone gets tracks when it or its partner moves
            var animated = Enumerable.Range(0, n).Where(i => rot[i] >= 0 || pos[i] >= 0 || rot[partner[i]] >= 0 || pos[partner[i]] >= 0).ToList();
            var rTrack = new Dictionary<int, int>();
            var pTrack = new Dictionary<int, int>();
            foreach (int b in animated)
            {
                string path = $"{prefix}:{skel.GetBoneName(b)}";
                int rt = dst.AddTrack(Animation.TrackType.Rotation3D);
                dst.TrackSetPath(rt, path);
                rTrack[b] = rt;
                int pt = dst.AddTrack(Animation.TrackType.Position3D);
                dst.TrackSetPath(pt, path);
                pTrack[b] = pt;
            }

            int frames = Mathf.Max(2, Mathf.CeilToInt((float)src.Length * 30f)) + 1;
            var global = new Transform3D[n];
            var mirrored = new Transform3D[n];
            for (int f = 0; f < frames; f++)
            {
                double time = src.Length * f / (frames - 1);
                foreach (int i in order)
                {
                    var q = rot[i] >= 0 ? src.RotationTrackInterpolate(rot[i], time) : restLocal[i].Basis.GetRotationQuaternion();
                    var p = pos[i] >= 0 ? src.PositionTrackInterpolate(pos[i], time) : restLocal[i].Origin;
                    var local = new Transform3D(new Basis(q).Scaled(restLocal[i].Basis.Scale), p);
                    global[i] = parent[i] >= 0 ? global[parent[i]] * local : local;
                }
                for (int b = 0; b < n; b++)
                {
                    int a = partner[b];
                    var skin = global[a] * restGlobal[a].AffineInverse();
                    mirrored[b] = m * skin * m * restGlobal[b];
                }
                foreach (int b in animated)
                {
                    var local = parent[b] >= 0 ? mirrored[parent[b]].AffineInverse() * mirrored[b] : mirrored[b];
                    dst.RotationTrackInsertKey(rTrack[b], time, local.Basis.Orthonormalized().GetRotationQuaternion());
                    dst.PositionTrackInsertKey(pTrack[b], time, local.Origin);
                }
            }
            return dst;
        }

        internal static Transform3D Relative(Node3D root, Node3D node)
        {
            var t = Transform3D.Identity;
            for (Node n = node; n != null && n != root; n = n.GetParent())
                if (n is Node3D n3) t = n3.Transform * t;
            return t;
        }

        // ── Look ──

        private static Shader _hullShader;
        /// <summary>
        /// The body's hull: Mike's texture turned into BIT's dark gunmetal (its brightness kept as
        /// panel detail) with the painted parts glowing in the accent colour, a fresnel rim, and a
        /// flash for hits (<see cref="HitFlash"/> drives <c>flash</c>).
        /// </summary>
        public static Shader HullShader => _hullShader ??= new Shader { Code = @"
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;
// Read raw (sRGB values, no conversion): brightness and paint are judged as the texture was painted
uniform sampler2D body_tex : filter_linear_mipmap;
uniform vec3 hull_dark : source_color = vec3(0.035, 0.04, 0.055);
uniform vec3 hull_light : source_color = vec3(0.34, 0.37, 0.44);
uniform float detail = 0.9;
uniform vec3 accent : source_color = vec3(0.5, 0.8, 1.0);
uniform float accent_energy = 2.4;
uniform vec3 rim_color : source_color = vec3(0.7, 0.8, 1.0);
uniform float rim = 0.45;
uniform vec4 flash = vec4(0.0);
void fragment() {
    vec3 t = texture(body_tex, UV).rgb;
    float lum = dot(t, vec3(0.299, 0.587, 0.114));
    float mx = max(t.r, max(t.g, t.b));
    float mn = min(t.r, min(t.g, t.b));
    float sat = (mx - mn) / max(mx, 0.001);
    float acc = smoothstep(0.35, 0.55, sat) * smoothstep(0.35, 0.55, mx);
    vec3 body = mix(hull_dark, hull_light, clamp(pow(lum, 1.2) * detail, 0.0, 1.0));
    ALBEDO = mix(body, accent * 0.25, acc);
    METALLIC = 0.55;
    ROUGHNESS = 0.48;
    float f = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
    EMISSION = accent * acc * accent_energy + rim_color * f * rim + flash.rgb * flash.a;
}
" };

        public static ShaderMaterial MakeHull(Texture2D tex, Color dark, Color light, Color accent, float accentEnergy, Color rim, float rimStrength)
        {
            var m = new ShaderMaterial { Shader = HullShader };
            m.SetShaderParameter("body_tex", tex);
            m.SetShaderParameter("hull_dark", new Vector3(dark.R, dark.G, dark.B));
            m.SetShaderParameter("hull_light", new Vector3(light.R, light.G, light.B));
            m.SetShaderParameter("accent", new Vector3(accent.R, accent.G, accent.B));
            m.SetShaderParameter("accent_energy", accentEnergy);
            m.SetShaderParameter("rim_color", new Vector3(rim.R, rim.G, rim.B));
            m.SetShaderParameter("rim", rimStrength);
            m.SetShaderParameter("flash", Vector4.Zero);
            m.SetMeta(HitFlash.ShaderFlashMeta, true);
            return m;
        }
    }

    /// <summary>
    /// BIT's arms on top of whatever clip plays: swept back for the naruto run, and pointed at a
    /// target when a gun, the lance or a running punch needs the arm out front. Runs after the
    /// AnimationPlayer (a SkeletonModifier3D), so the legs and body keep the clip.
    /// </summary>
    public partial class BitArms : SkeletonModifier3D
    {
        /// <summary>0 to 1: how far the arms are swept back (eased toward <see cref="RunWanted"/>).</summary>
        public float Run { get; private set; }
        public bool RunWanted;
        private readonly float[] _aim = new float[2];       // 0 left, 1 right
        private readonly float[] _aimHold = new float[2];
        private readonly Vector3[] _aimAt = new Vector3[2];
        private Node3D _model;
        private int[] _upper = new int[2], _lower = new int[2];
        private Vector3[] _upperAxis = new Vector3[2], _lowerAxis = new Vector3[2];
        private bool _ready;

        /// <summary>How far each arm is pointed at its target (tests).</summary>
        public float AimOf(string hand) => _aim[hand == "L" ? 0 : 1];

        public void Setup(Node3D model) { _model = model; _ready = false; }

        /// <summary>Point an arm at a world point for <paramref name="hold"/> seconds (refresh it every frame to keep it there).</summary>
        public void Aim(string hand, Vector3 worldTarget, float hold = 0.2f)
        {
            int s = hand == "L" ? 0 : 1;
            _aimAt[s] = worldTarget;
            _aimHold[s] = Mathf.Max(_aimHold[s], hold);
        }

        public void ClearAim() { _aimHold[0] = _aimHold[1] = 0f; _aim[0] = _aim[1] = 0f; }

        private bool Init(Skeleton3D sk)
        {
            if (_ready) return true;
            string[] side = { "L", "R" };
            for (int s = 0; s < 2; s++)
            {
                _upper[s] = sk.FindBone("UpperArm." + side[s]);
                _lower[s] = sk.FindBone("LowerArm." + side[s]);
                if (_upper[s] < 0 || _lower[s] < 0) return false;
                // Each bone's own axis toward the next joint (at rest), in its local space
                var upRest = sk.GetBoneGlobalRest(_upper[s]);
                var lowRest = sk.GetBoneGlobalRest(_lower[s]);
                int palm = sk.FindBone("PalmI." + side[s]);
                var tip = palm >= 0 ? sk.GetBoneGlobalRest(palm).Origin : lowRest.Origin + (lowRest.Origin - upRest.Origin);
                _upperAxis[s] = (upRest.Basis.Inverse() * (lowRest.Origin - upRest.Origin)).Normalized();
                _lowerAxis[s] = (lowRest.Basis.Inverse() * (tip - lowRest.Origin)).Normalized();
            }
            _ready = true;
            return true;
        }

        public override void _ProcessModificationWithDelta(double delta)
        {
            var sk = GetSkeleton();
            if (sk == null || _model == null || !IsInstanceValid(_model) || !Init(sk)) return;
            float dt = (float)delta;
            Run = Mathf.MoveToward(Run, RunWanted ? 1f : 0f, dt * 6f);
            for (int s = 0; s < 2; s++)
            {
                _aimHold[s] -= dt;
                _aim[s] = Mathf.MoveToward(_aim[s], _aimHold[s] > 0f ? 1f : 0f, dt * 10f);
            }
            if (Run <= 0.001f && _aim[0] <= 0.001f && _aim[1] <= 0.001f) return;

            // Directions are chosen in the model's frame (X left, Y up, Z forward) and taken into
            // the skeleton's space
            var skelInModel = BitBody.Relative(_model, sk);
            var toSkel = skelInModel.Basis.Orthonormalized().Inverse();
            var modelInv = _model.GlobalTransform.AffineInverse();
            for (int s = 0; s < 2; s++)
            {
                float x = s == 0 ? 1f : -1f;
                // Swept back and a little out, forearm in line: the naruto run
                var upDir = new Vector3(x * 0.35f, -0.62f, -1f).Normalized();
                var lowDir = new Vector3(x * 0.28f, -0.4f, -1f).Normalized();
                float w = Run;
                if (_aim[s] > 0.001f)
                {
                    var shoulder = skelInModel * sk.GetBoneGlobalPose(_upper[s]).Origin;
                    var at = modelInv * _aimAt[s];
                    var dir = (at - shoulder).Normalized();
                    if (dir.LengthSquared() < 0.5f) dir = new Vector3(0, 0, 1);
                    float a = _aim[s];
                    upDir = upDir.Slerp(dir, Mathf.Clamp(a / Mathf.Max(0.001f, Mathf.Max(w, a)), 0f, 1f));
                    lowDir = lowDir.Slerp(dir, Mathf.Clamp(a / Mathf.Max(0.001f, Mathf.Max(w, a)), 0f, 1f));
                    w = Mathf.Max(w, a);
                }
                Point(sk, _upper[s], _upperAxis[s], (toSkel * upDir).Normalized(), w);
                Point(sk, _lower[s], _lowerAxis[s], (toSkel * lowDir).Normalized(), w);
            }
        }

        /// <summary>Turn a bone so its axis points along <paramref name="want"/> (skeleton space), by <paramref name="weight"/>.</summary>
        private static void Point(Skeleton3D sk, int bone, Vector3 axisLocal, Vector3 want, float weight)
        {
            var g = sk.GetBoneGlobalPose(bone);
            var now = (g.Basis * axisLocal).Normalized();
            var target = now.Slerp(want, Mathf.Clamp(weight, 0f, 1f)).Normalized();
            var turn = new Quaternion(now, target);
            if (!turn.IsFinite()) return;
            g.Basis = new Basis(turn) * g.Basis;
            sk.SetBoneGlobalPose(bone, g);
        }
    }
}
