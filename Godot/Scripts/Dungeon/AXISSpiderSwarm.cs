using Godot;
using System.Collections.Generic;

namespace JunkbotArena
{
    /// <summary>
    /// Manages miniature AXIS spider bots crawling on the TrussDome.
    /// Each spider is a procedurally-built body+legs mesh with tripod gait animation.
    /// During the intro, spiders descend to "build" rooms, then retreat back to the
    /// dome. A few remain as ambient detail during gameplay.
    /// </summary>
    public partial class AXISSpiderSwarm : Node3D
    {
        private enum SpiderState { Crawling, Idle, Descending, Working, Retreating }

        private struct LegData
        {
            public Node3D Upper;
            public Node3D Lower;
        }

        private struct SpiderData
        {
            public Node3D Root;
            public SpiderState State;
            public int CurrentBeam;
            public float Progress; // 0→1 along beam
            public float Speed;
            public int CurrentJoint; // joint we're heading toward
            public bool IsNearTier;
            public float IdleTimer;
            public float WorkTimer;
            public Vector3 DescentTarget;
            public int FrameSkip; // for far-tier half-rate animation
            public LegData[] Legs; // procedural fallback
            public Skeleton3D Skeleton; // FBX skeleton (null for procedural)
            public Dictionary<string, int> Bones;
        }

        private readonly List<SpiderData> _spiders = new();
        private TrussDome _dome;
        private List<(int, int)> _beamJointPairs;
        private float _time;
        private RandomNumberGenerator _rng = new();
        private bool _ambientMode;
        private int _ambientKeepCount;
        private float _danger;

        // ── AXIS colors ──
        private static readonly Color AXIS_RED = new(1f, 0.12f, 0.08f);
        private static readonly Color AXIS_PURPLE = new(0.4f, 0.15f, 0.6f);

        public void Initialize(TrussDome dome, float danger, int count = 12)
        {
            _dome = dome;
            _danger = danger;
            _rng.Randomize();
            Name = "AXISSpiderSwarm";

            if (dome.Beams.Count == 0)
            {
                GD.PrintErr("[AXISSpiderSwarm] Dome has no beams — cannot spawn spiders");
                return;
            }

            // Precompute beam-joint pairs for pathfinding
            _beamJointPairs = dome.GetBeamJointPairs();

            // Determine near-tier threshold: lower 40% of dome height
            float maxY = 0;
            foreach (var j in dome.Joints)
                if (j.Y > maxY) maxY = j.Y;
            float nearThreshold = maxY * 0.4f;

            // ── Try loading the SK_ISO_Mech FBX model ──
            PackedScene spiderScene = null;
            string fbxPath = "res://Assets/RetroMech/Model/SK_ISO_Mech.fbx";
            if (ResourceLoader.Exists(fbxPath))
            {
                spiderScene = GD.Load<PackedScene>(fbxPath);
                if (spiderScene != null)
                    GD.Print("[AXISSpiderSwarm] Loaded SK_ISO_Mech FBX as PackedScene");
                else
                    GD.Print("[AXISSpiderSwarm] SK_ISO_Mech exists but failed to load — using procedural fallback");
            }
            bool useFBX = spiderScene != null;

            // Procedural fallback materials (only used if FBX unavailable)
            StandardMaterial3D bodyMat = null, eyeMat = null;
            if (!useFBX)
            {
                GD.Print("[AXISSpiderSwarm] FBX unavailable — using procedural spider fallback");
                bodyMat = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.12f, 0.08f, 0.15f),
                    Metallic = 0.85f,
                    Roughness = 0.3f,
                    EmissionEnabled = true,
                    Emission = AXIS_RED * 0.15f,
                    EmissionEnergyMultiplier = 1.0f,
                };
                eyeMat = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.02f, 0.01f, 0.01f),
                    Metallic = 0.95f,
                    Roughness = 0.1f,
                    EmissionEnabled = true,
                    Emission = AXIS_RED,
                    EmissionEnergyMultiplier = 4.0f,
                };
            }

            for (int i = 0; i < count; i++)
            {
                var root = new Node3D();
                root.Name = $"Spider_{i}";
                AddChild(root);

                LegData[] legs = null;
                Skeleton3D skeleton = null;
                Dictionary<string, int> bones = null;

                if (useFBX)
                {
                    var model = spiderScene.Instantiate<Node3D>();
                    if (model != null)
                    {
                        root.AddChild(model);
                        CharacterMeshBuilder.ScaleModelToFit(model, 3f);
                        ApplySpiderMaterials(model);

                        // Discover skeleton for procedural animation
                        skeleton = FindNodeOfType<Skeleton3D>(model);
                        if (skeleton != null)
                        {
                            bones = new Dictionary<string, int>();
                            for (int b = 0; b < skeleton.GetBoneCount(); b++)
                                bones[skeleton.GetBoneName(b)] = b;

                            if (i == 0)
                            {
                                var boneNames = new List<string>();
                                for (int b = 0; b < skeleton.GetBoneCount(); b++)
                                    boneNames.Add(skeleton.GetBoneName(b));
                                GD.Print($"[AXISSpiderSwarm] Mech bones ({skeleton.GetBoneCount()}): {string.Join(", ", boneNames)}");
                            }
                        }
                    }
                }
                else
                {
                    legs = BuildProceduralSpider(root, bodyMat, eyeMat);
                }

                // Assign to a random beam
                int beamIdx = _rng.RandiRange(0, dome.Beams.Count - 1);
                float progress = _rng.Randf();

                // Determine near/far tier
                var (start, end) = dome.Beams[beamIdx];
                float avgY = (start.Y + end.Y) * 0.5f;
                bool isNear = avgY <= nearThreshold;

                var spider = new SpiderData
                {
                    Root = root,
                    Legs = legs,
                    Skeleton = skeleton,
                    Bones = bones,
                    State = SpiderState.Crawling,
                    CurrentBeam = beamIdx,
                    Progress = progress,
                    Speed = _rng.RandfRange(4f, 8f),
                    CurrentJoint = _beamJointPairs[beamIdx].Item2,
                    IsNearTier = isNear,
                    IdleTimer = 0,
                    WorkTimer = 0,
                    FrameSkip = 0,
                };

                PositionOnBeam(ref spider);
                _spiders.Add(spider);
            }

            GD.Print($"[AXISSpiderSwarm] Spawned {_spiders.Count} spiders on dome (FBX={useFBX}, {dome.Beams.Count} beams, {dome.Joints.Count} joints)");
        }

        // ═════════════════════════════════════════════════════════
        //  PROCEDURAL SPIDER BUILDER
        // ═════════════════════════════════════════════════════════

        private const float BODY_RADIUS = 2.0f;
        private const float LEG_UPPER_LEN = 4f;
        private const float LEG_LOWER_LEN = 5f;
        private const float LEG_THICKNESS = 0.25f;

        private LegData[] BuildProceduralSpider(Node3D root, StandardMaterial3D bodyMat, StandardMaterial3D eyeMat)
        {
            // ── Body: flattened sphere ──
            var bodyMesh = new SphereMesh();
            bodyMesh.Radius = BODY_RADIUS;
            bodyMesh.Height = BODY_RADIUS * 1.2f;
            bodyMesh.RadialSegments = 12;
            bodyMesh.Rings = 6;
            var body = new MeshInstance3D();
            body.Mesh = bodyMesh;
            body.MaterialOverride = bodyMat;
            body.Name = "Body";
            root.AddChild(body);

            // ── Eye: emissive sphere on front ──
            var eyeMesh = new SphereMesh();
            eyeMesh.Radius = 0.4f;
            eyeMesh.Height = 0.8f;
            eyeMesh.RadialSegments = 6;
            eyeMesh.Rings = 3;
            var eye = new MeshInstance3D();
            eye.Mesh = eyeMesh;
            eye.MaterialOverride = eyeMat;
            eye.Position = new Vector3(0, 0.2f, BODY_RADIUS * 0.75f);
            eye.Name = "Eye";
            root.AddChild(eye);

            // ── 6 legs: 3 per side, each with upper + lower segment ──
            var legs = new LegData[6];
            float[] zOffsets = { 0.7f, 0f, -0.7f }; // front, mid, back
            float[] spreadAngles = { 30f, 0f, -30f };

            for (int side = 0; side < 2; side++)
            {
                float xSign = side == 0 ? -1f : 1f;
                for (int leg = 0; leg < 3; leg++)
                {
                    int idx = side * 3 + leg;

                    // Upper leg: pivot at body edge, angled outward+up
                    var upper = new Node3D();
                    upper.Name = $"Leg_{idx}_Upper";
                    upper.Position = new Vector3(xSign * BODY_RADIUS * 0.7f, 0, zOffsets[leg]);
                    upper.RotationDegrees = new Vector3(spreadAngles[leg], 0, xSign * 45f);
                    root.AddChild(upper);

                    var upperMeshInst = new MeshInstance3D();
                    var upperCyl = new CylinderMesh();
                    upperCyl.TopRadius = LEG_THICKNESS;
                    upperCyl.BottomRadius = LEG_THICKNESS * 0.7f;
                    upperCyl.Height = LEG_UPPER_LEN;
                    upperCyl.RadialSegments = 6;
                    upperMeshInst.Mesh = upperCyl;
                    upperMeshInst.MaterialOverride = bodyMat;
                    upperMeshInst.Position = new Vector3(0, LEG_UPPER_LEN * 0.5f, 0);
                    upper.AddChild(upperMeshInst);

                    // Lower leg: pivot at end of upper, angled down
                    var lower = new Node3D();
                    lower.Name = $"Leg_{idx}_Lower";
                    lower.Position = new Vector3(0, LEG_UPPER_LEN, 0);
                    lower.RotationDegrees = new Vector3(0, 0, xSign * -90f);
                    upper.AddChild(lower);

                    var lowerMeshInst = new MeshInstance3D();
                    var lowerCyl = new CylinderMesh();
                    lowerCyl.TopRadius = LEG_THICKNESS * 0.7f;
                    lowerCyl.BottomRadius = LEG_THICKNESS * 0.3f;
                    lowerCyl.Height = LEG_LOWER_LEN;
                    lowerCyl.RadialSegments = 6;
                    lowerMeshInst.Mesh = lowerCyl;
                    lowerMeshInst.MaterialOverride = bodyMat;
                    lowerMeshInst.Position = new Vector3(0, LEG_LOWER_LEN * 0.5f, 0);
                    lower.AddChild(lowerMeshInst);

                    legs[idx] = new LegData { Upper = upper, Lower = lower };
                }
            }

            return legs;
        }

        // ═════════════════════════════════════════════════════════
        //  PUBLIC API — called by AXISPresence / DungeonAssemblyIntro
        // ═════════════════════════════════════════════════════════

        /// <summary>
        /// Assign spiders to descend toward room positions for the intro build sequence.
        /// </summary>
        public void CommandDescent(List<Vector3> roomPositions)
        {
            // Only send ~60% of spiders to rooms — the rest keep crawling on the dome
            int maxDescend = Mathf.Max(1, (int)(_spiders.Count * 0.6f));
            int assignCount = Mathf.Min(maxDescend, roomPositions.Count);

            GD.Print($"[AXISSpiderSwarm] CommandDescent: {assignCount}/{_spiders.Count} spiders, {roomPositions.Count} rooms");

            for (int i = 0; i < assignCount; i++)
            {
                var spider = _spiders[i];
                spider.State = SpiderState.Descending;
                spider.DescentTarget = roomPositions[i] + Vector3.Up * 3f;

                if (spider.Root != null && IsInstanceValid(spider.Root))
                {
                    float dist = spider.Root.GlobalPosition.DistanceTo(spider.DescentTarget);
                    float duration = Mathf.Clamp(dist / 50f, 1.0f, 2.5f);
                    float delay = i * 0.15f;

                    if (i == 0)
                        GD.Print($"[AXISSpiderSwarm] Spider 0: from {spider.Root.GlobalPosition} to {spider.DescentTarget}, dist={dist:F0}, dur={duration:F1}s");

                    var tween = CreateTween();
                    tween.TweenInterval(delay);

                    var capturedTarget = spider.DescentTarget;
                    var capturedRoot = spider.Root;
                    int capturedIdx = i;

                    tween.TweenProperty(capturedRoot, "global_position",
                        capturedTarget, duration)
                        .SetEase(Tween.EaseType.InOut)
                        .SetTrans(Tween.TransitionType.Cubic);

                    tween.TweenCallback(Callable.From(() =>
                    {
                        if (capturedIdx < _spiders.Count)
                        {
                            var s = _spiders[capturedIdx];
                            s.State = SpiderState.Working;
                            s.WorkTimer = 0;
                            _spiders[capturedIdx] = s;
                        }
                    }));
                }

                _spiders[i] = spider;
            }
        }

        /// <summary>
        /// All working/descending spiders retreat back to the dome.
        /// </summary>
        public void CommandRetreat()
        {
            for (int i = 0; i < _spiders.Count; i++)
            {
                var spider = _spiders[i];
                if (spider.State != SpiderState.Working && spider.State != SpiderState.Descending)
                    continue;

                spider.State = SpiderState.Retreating;

                if (spider.Root != null && IsInstanceValid(spider.Root))
                {
                    var beamPos = GetBeamPosition(spider.CurrentBeam, spider.Progress);
                    float delay = i * 0.05f;
                    float duration = 1.2f;

                    var tween = CreateTween();
                    tween.TweenInterval(delay);

                    var capturedRoot = spider.Root;
                    var capturedPos = beamPos;
                    int capturedIdx = i;

                    tween.TweenProperty(capturedRoot, "global_position",
                        GlobalPosition + capturedPos, duration)
                        .SetEase(Tween.EaseType.InOut)
                        .SetTrans(Tween.TransitionType.Cubic);

                    tween.TweenCallback(Callable.From(() =>
                    {
                        if (capturedIdx < _spiders.Count)
                        {
                            var s = _spiders[capturedIdx];
                            s.State = SpiderState.Crawling;
                            _spiders[capturedIdx] = s;
                        }
                    }));
                }

                _spiders[i] = spider;
            }
        }

        /// <summary>
        /// After intro, keep N spiders crawling on lower beams. Hide the rest.
        /// </summary>
        public void SetAmbientMode(int keepCount)
        {
            _ambientMode = true;
            _ambientKeepCount = keepCount;

            int kept = 0;
            for (int i = 0; i < _spiders.Count; i++)
            {
                var spider = _spiders[i];
                if (kept < keepCount && spider.IsNearTier)
                {
                    spider.State = SpiderState.Crawling;
                    if (spider.Root != null && IsInstanceValid(spider.Root))
                        spider.Root.Visible = true;
                    kept++;
                }
                else
                {
                    if (spider.Root != null && IsInstanceValid(spider.Root))
                        spider.Root.Visible = false;
                    spider.State = SpiderState.Idle;
                }
                _spiders[i] = spider;
            }

            GD.Print($"[AXISSpiderSwarm] Ambient mode: {kept} spiders visible");
        }

        // ═════════════════════════════════════════════════════════
        //  PROCESS — movement + animation
        // ═════════════════════════════════════════════════════════

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _time += dt;

            for (int i = 0; i < _spiders.Count; i++)
            {
                var spider = _spiders[i];
                if (spider.Root == null || !IsInstanceValid(spider.Root)) continue;
                if (!spider.Root.Visible) continue;

                switch (spider.State)
                {
                    case SpiderState.Crawling:
                        UpdateCrawling(ref spider, dt);
                        break;
                    case SpiderState.Idle:
                        UpdateIdle(ref spider, dt);
                        break;
                    case SpiderState.Working:
                        spider.WorkTimer += dt;
                        break;
                    // Descending/Retreating handled by tweens
                }

                // Animate legs (skeleton or procedural)
                bool shouldAnimate = spider.State is SpiderState.Crawling
                    or SpiderState.Descending or SpiderState.Working or SpiderState.Retreating;

                if (shouldAnimate)
                {
                    if (!spider.IsNearTier)
                    {
                        spider.FrameSkip++;
                        if (spider.FrameSkip % 2 != 0)
                        {
                            _spiders[i] = spider;
                            continue;
                        }
                    }

                    float speedMult = spider.State == SpiderState.Idle ? 0.1f : 1f;
                    if (spider.Skeleton != null && spider.Bones != null)
                        AnimateSkeletonLegs(ref spider, speedMult);
                    else if (spider.Legs != null)
                        AnimateProceduralLegs(ref spider, speedMult);
                }

                _spiders[i] = spider;
            }
        }

        private void UpdateCrawling(ref SpiderData spider, float dt)
        {
            if (_dome == null) return;

            var (start, end) = _dome.Beams[spider.CurrentBeam];
            float beamLen = start.DistanceTo(end);
            if (beamLen < 0.1f) beamLen = 1f;

            spider.Progress += (spider.Speed * dt) / beamLen;

            if (spider.Progress >= 1f)
            {
                spider.Progress = 0f;
                int arrivedJoint = spider.CurrentJoint;

                if (_rng.Randf() < 0.3f)
                {
                    spider.State = SpiderState.Idle;
                    spider.IdleTimer = _rng.RandfRange(0.5f, 2f);
                    PositionOnBeam(ref spider);
                    return;
                }

                PickNextBeam(ref spider, arrivedJoint);
            }

            PositionOnBeam(ref spider);
        }

        private void UpdateIdle(ref SpiderData spider, float dt)
        {
            spider.IdleTimer -= dt;
            if (spider.IdleTimer <= 0)
            {
                spider.State = SpiderState.Crawling;
                PickNextBeam(ref spider, spider.CurrentJoint);
            }
        }

        private void PickNextBeam(ref SpiderData spider, int atJoint)
        {
            if (!_dome.Adjacency.TryGetValue(atJoint, out var adjacentBeams)) return;
            if (adjacentBeams.Count == 0) return;

            int attempts = 0;
            int nextBeam;
            do
            {
                nextBeam = adjacentBeams[_rng.RandiRange(0, adjacentBeams.Count - 1)];
                attempts++;
            } while (nextBeam == spider.CurrentBeam && attempts < 4 && adjacentBeams.Count > 1);

            spider.CurrentBeam = nextBeam;
            spider.Progress = 0f;

            var pair = _beamJointPairs[nextBeam];
            spider.CurrentJoint = (pair.Item1 == atJoint) ? pair.Item2 : pair.Item1;
        }

        private void PositionOnBeam(ref SpiderData spider)
        {
            if (_dome == null || spider.CurrentBeam >= _dome.Beams.Count) return;

            var pos = GetBeamPosition(spider.CurrentBeam, spider.Progress);
            spider.Root.Position = pos;

            // Orient: forward along beam, "up" points away from dome center (outward)
            var (start, end) = _dome.Beams[spider.CurrentBeam];
            var forward = (end - start).Normalized();
            if (forward.LengthSquared() < 0.01f) return;

            // "Up" for the spider = away from dome center (so legs grip downward/inward)
            // Dome center is at local origin (0,0,0), beams radiate outward+up
            var beamCenter = (start + end) * 0.5f;
            var outward = beamCenter.Normalized(); // points away from dome center
            if (outward.LengthSquared() < 0.001f) outward = Vector3.Up;

            // Ensure forward and up are orthogonal
            var right = outward.Cross(forward).Normalized();
            if (right.LengthSquared() < 0.001f) return;

            var up = forward.Cross(right).Normalized();
            spider.Root.Basis = new Basis(right, up, forward);
        }

        private Vector3 GetBeamPosition(int beamIdx, float progress)
        {
            if (beamIdx >= _dome.Beams.Count) return Vector3.Zero;
            var (start, end) = _dome.Beams[beamIdx];
            return start.Lerp(end, Mathf.Clamp(progress, 0f, 1f));
        }

        // ═════════════════════════════════════════════════════════
        //  ANIMATION — procedural tripod gait
        // ═════════════════════════════════════════════════════════

        // ── Skeleton leg group names (RetroMech convention) ──
        private static readonly string[] SK_LEG_GROUPS = { "FrontLeg", "MiddleLeg", "BackLeg" };
        private static readonly string[] SK_SIDES = { "_L", "_R" };

        private void AnimateSkeletonLegs(ref SpiderData spider, float speedMult)
        {
            if (spider.Skeleton == null) return;

            float walkSpeed = 0.6f * speedMult;
            float cycle = (_time + spider.CurrentBeam * 0.7f) * walkSpeed;

            for (int g = 0; g < SK_LEG_GROUPS.Length; g++)
            {
                string group = SK_LEG_GROUPS[g];
                for (int s = 0; s < SK_SIDES.Length; s++)
                {
                    string side = SK_SIDES[s];

                    // Tripod gait: front+back on one side, mid on opposite
                    bool isGroupA = (group != "MiddleLeg" && side == "_L")
                                 || (group == "MiddleLeg" && side == "_R");
                    float phase = isGroupA ? 0f : 0.5f;
                    float legCycle = (cycle + phase) * Mathf.Tau;

                    // Bone 1: hip — large forward/back swing
                    float hip = Mathf.Sin(legCycle) * 25f;
                    SetBoneRotation(spider.Skeleton, spider.Bones, $"{group}1{side}", new Vector3(0, 0, hip));

                    // Bone 2: upper leg — follows hip with slight delay
                    float upper = Mathf.Sin(legCycle - 0.3f) * 20f;
                    SetBoneRotation(spider.Skeleton, spider.Bones, $"{group}2{side}", new Vector3(0, 0, upper));

                    // Bone 3: knee — bends opposite to create stepping motion
                    float knee = Mathf.Sin(legCycle - 0.6f) * 30f;
                    SetBoneRotation(spider.Skeleton, spider.Bones, $"{group}3{side}", new Vector3(0, 0, knee));

                    // Bone 4: lower leg — compensates, keeps foot angled down
                    float lower = Mathf.Sin(legCycle - 0.9f) * 15f;
                    SetBoneRotation(spider.Skeleton, spider.Bones, $"{group}4{side}", new Vector3(0, 0, lower));

                    // Bone 5: ankle — subtle ground contact flex
                    float ankle = Mathf.Sin(legCycle - 1.1f) * 8f;
                    SetBoneRotation(spider.Skeleton, spider.Bones, $"{group}5{side}", new Vector3(0, 0, ankle));
                }
            }

            // Subtle body sway on Top_M
            float sway = Mathf.Sin(cycle * Mathf.Tau * 0.5f) * 3f;
            SetBoneRotation(spider.Skeleton, spider.Bones, "Top_M", new Vector3(sway * 0.5f, 0, sway));
        }

        private static void SetBoneRotation(Skeleton3D skeleton, Dictionary<string, int> bones,
            string boneName, Vector3 eulerDeg)
        {
            if (!bones.TryGetValue(boneName, out int idx)) return;
            var quat = Quaternion.FromEuler(eulerDeg * (Mathf.Pi / 180f));
            skeleton.SetBonePoseRotation(idx, quat);
        }

        private void AnimateProceduralLegs(ref SpiderData spider, float speedMult)
        {
            if (spider.Legs == null) return;

            float walkSpeed = 0.6f * speedMult;
            float cycle = (_time + spider.CurrentBeam * 0.7f) * walkSpeed;

            for (int i = 0; i < 6; i++)
            {
                var leg = spider.Legs[i];
                if (leg.Upper == null || !IsInstanceValid(leg.Upper)) continue;

                int side = i / 3;       // 0=left, 1=right
                int legIdx = i % 3;     // 0=front, 1=mid, 2=back
                float xSign = side == 0 ? -1f : 1f;

                // Tripod gait: front+back left + mid right move together
                bool isGroupA = (legIdx != 1 && side == 0) || (legIdx == 1 && side == 1);
                float phase = isGroupA ? 0f : 0.5f;
                float legCycle = (cycle + phase) * Mathf.Tau;

                // Upper leg swing
                float[] spreadAngles = { 30f, 0f, -30f };
                float swing = Mathf.Sin(legCycle) * 20f;
                leg.Upper.RotationDegrees = new Vector3(
                    spreadAngles[legIdx] + swing * 0.3f,
                    0,
                    xSign * 45f + swing);

                // Lower leg bend
                if (leg.Lower != null && IsInstanceValid(leg.Lower))
                {
                    float bend = Mathf.Sin(legCycle - 0.6f) * 25f;
                    leg.Lower.RotationDegrees = new Vector3(0, 0, xSign * -90f + bend);
                }
            }
        }

        // ═════════════════════════════════════════════════════════
        //  HELPERS
        // ═════════════════════════════════════════════════════════

        private static T FindNodeOfType<T>(Node root) where T : Node
        {
            if (root is T found) return found;
            foreach (Node child in root.GetChildren())
            {
                var result = FindNodeOfType<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        // ═════════════════════════════════════════════════════════
        //  FBX MODEL MATERIALS — dark AXIS theme for loaded mech models
        // ═════════════════════════════════════════════════════════

        private static void ApplySpiderMaterials(Node3D model)
        {
            ApplySpiderMaterialsRecursive(model);
        }

        private static void ApplySpiderMaterialsRecursive(Node node)
        {
            if (node is MeshInstance3D mi && mi.Mesh != null)
            {
                string name = mi.Name.ToString().ToLower();
                bool isEmissive = name.Contains("eye") || name.Contains("visor")
                    || name.Contains("light") || name.Contains("glow")
                    || name.Contains("screen") || name.Contains("lens");

                var mat = new StandardMaterial3D
                {
                    AlbedoColor = isEmissive ? new Color(0.02f, 0.01f, 0.01f) : new Color(0.12f, 0.08f, 0.15f),
                    Metallic = isEmissive ? 0.95f : 0.85f,
                    Roughness = isEmissive ? 0.1f : 0.3f,
                    EmissionEnabled = true,
                    Emission = AXIS_RED * (isEmissive ? 1.0f : 0.15f),
                    EmissionEnergyMultiplier = isEmissive ? 4.0f : 1.0f,
                };

                mi.MaterialOverride = mat;
                for (int i = 0; i < mi.GetSurfaceOverrideMaterialCount(); i++)
                    mi.SetSurfaceOverrideMaterial(i, mat);
            }

            foreach (Node child in node.GetChildren())
                ApplySpiderMaterialsRecursive(child);
        }

        // ═════════════════════════════════════════════════════════
        //  DEBUG — F11 spawns a spider at the player for close inspection
        // ═════════════════════════════════════════════════════════

        private Node3D _debugSpider;

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.F11)
            {
                SpawnDebugSpiderAtPlayer();
                GetViewport().SetInputAsHandled();
            }
        }

        private void SpawnDebugSpiderAtPlayer()
        {
            // Clean up previous debug spider
            if (_debugSpider != null && IsInstanceValid(_debugSpider))
                _debugSpider.QueueFree();

            var player = PlayerManager.P1;
            if (player == null || !IsInstanceValid(player))
            {
                GD.Print("[AXISSpiderSwarm] No player found for debug spider");
                return;
            }

            var pos = player.GlobalPosition + new Vector3(5f, 3f, 5f);

            _debugSpider = new Node3D();
            _debugSpider.Name = "DebugSpider";
            GetTree().Root.AddChild(_debugSpider);
            _debugSpider.GlobalPosition = pos;

            // Try FBX first
            string fbxPath = "res://Assets/RetroMech/Model/SK_ISO_Mech.fbx";
            bool loadedFBX = false;
            if (ResourceLoader.Exists(fbxPath))
            {
                var scene = GD.Load<PackedScene>(fbxPath);
                if (scene != null)
                {
                    var model = scene.Instantiate<Node3D>();
                    _debugSpider.AddChild(model);
                    CharacterMeshBuilder.ScaleModelToFit(model, 3f);
                    ApplySpiderMaterials(model);
                    loadedFBX = true;

                    var skel = FindNodeOfType<Skeleton3D>(model);
                    if (skel != null)
                    {
                        var boneNames = new List<string>();
                        for (int b = 0; b < skel.GetBoneCount(); b++)
                            boneNames.Add(skel.GetBoneName(b));
                        GD.Print($"[DebugSpider] Bones ({skel.GetBoneCount()}): {string.Join(", ", boneNames)}");
                    }
                    else
                    {
                        GD.Print("[DebugSpider] No skeleton found in model");
                    }
                }
            }

            if (!loadedFBX)
            {
                GD.Print("[DebugSpider] FBX unavailable — spawning procedural");
                var bodyMat = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.12f, 0.08f, 0.15f),
                    Metallic = 0.85f, Roughness = 0.3f,
                    EmissionEnabled = true, Emission = AXIS_RED * 0.15f, EmissionEnergyMultiplier = 1.0f,
                };
                var eyeMat = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.02f, 0.01f, 0.01f),
                    Metallic = 0.95f, Roughness = 0.1f,
                    EmissionEnabled = true, Emission = AXIS_RED, EmissionEnergyMultiplier = 4.0f,
                };
                BuildProceduralSpider(_debugSpider, bodyMat, eyeMat);
            }

            GD.Print($"[DebugSpider] Spawned at {pos} (FBX={loadedFBX}). Press F11 again to respawn.");
        }

        /// <summary>
        /// Spawn spark VFX at a spider's working position.
        /// </summary>
        public void SpawnWorkSparks(int spiderIndex)
        {
            if (spiderIndex >= _spiders.Count) return;
            var spider = _spiders[spiderIndex];
            if (spider.Root == null || !IsInstanceValid(spider.Root)) return;

            var sparks = VfxFactory.CreateElectricSparks(AXIS_RED);
            spider.Root.AddChild(sparks);
            sparks.Position = Vector3.Down * 1f;
            sparks.Emitting = true;

            var timer = GetTree().CreateTimer(2.0);
            timer.Timeout += () =>
            {
                if (IsInstanceValid(sparks))
                    sparks.QueueFree();
            };
        }
    }
}
