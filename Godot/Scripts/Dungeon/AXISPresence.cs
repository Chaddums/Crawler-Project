using Godot;
using System.Collections.Generic;

namespace JunkbotArena
{
    /// <summary>
    /// The AXIS Overseer — a spider mech that traverses overhead wire rails,
    /// scanning the dungeon below. Loads the Retro ISO Mech FBX, applies dark
    /// menacing materials with red emissive accents, builds a wire/rail system,
    /// and runs procedural leg-stomp animation as it patrols.
    ///
    /// During the intro, DungeonAssemblyIntro positions/scales AXIS and calls
    /// CommandSweep/CommandAssembly/GoIdle to drive animation phases.
    /// </summary>
    public partial class AXISPresence : Node3D
    {
        // ── Model ──
        private Node3D _model;
        private Skeleton3D _skeleton;

        // ── Bone indices for procedural animation ──
        private readonly Dictionary<string, int> _bones = new();

        // ── Animation state ──
        private float _time;
        private float _headBaseY;

        // ── Sweep/assembly modes (driven by DungeonAssemblyIntro) ──
        private bool _sweepMode;
        private float _sweepTimer;
        private float _sweepDuration;
        private bool _assemblyMode;

        private Vector3 _modelBaseScale = Vector3.One;
        private float _modelBaseXRot; // FBX axis correction stored after Initialize

        // ── Wire system ──
        private Node3D _wireSystem;
        private readonly List<MeshInstance3D> _wires = new();

        // ── Constants ──
        private const float HEAD_Y = 42f;
        private const float HOVER_AMPLITUDE = 1.5f;
        private const float HOVER_SPEED = 0.15f;
        private const float SCAN_AMPLITUDE = 18f;
        private const float SCAN_SPEED = 0.05f;

        // Wire system dimensions
        private const float WIRE_HEIGHT = 8f;       // wires above the mech's top
        private const float WIRE_SPREAD = 12f;       // lateral spread of wire pairs
        private const float WIRE_LENGTH = 200f;      // how far wires extend
        private const float WIRE_RADIUS = 0.15f;     // wire thickness

        // ── AXIS signature colors ──
        private static readonly Color AXIS_RED = new(1f, 0.12f, 0.08f);
        private static readonly Color AXIS_DARK = new(0.03f, 0.025f, 0.04f);
        private static readonly Color WIRE_COLOR = new(0.12f, 0.1f, 0.14f);

        private Color _accentColor;

        // ── Lights ──
        private OmniLight3D _coreLight;
        private OmniLight3D _underLight;
        private SpotLight3D _eyeSpotLeft;
        private SpotLight3D _eyeSpotRight;

        public void Initialize(Color sectorAccent, float danger)
        {
            _accentColor = sectorAccent;
            _headBaseY = 0f;

            _model = ModelLibrary.TryLoad("boss", "axis_avatar");
            if (_model != null)
            {
                AddChild(_model);

                // Find skeleton
                _skeleton = FindNodeOfType<Skeleton3D>(_model);
                if (_skeleton != null)
                {
                    CacheBoneIndices();
                    GD.Print($"[AXISPresence] Skeleton: {_bones.Count} bones cached of {_skeleton.GetBoneCount()} total");
                }

                // Use dark tinted PBR materials — not flat black, so mesh detail is visible
                ApplyAXISMaterials(_model, danger);

                CharacterMeshBuilder.ScaleModelToFit(_model, 60f);
                _modelBaseScale = _model.Scale;

                // Log the node tree so we can see if there's an intermediate rotation node
                LogNodeTree(_model, 0);

                // Don't fight the importer — just face the camera, no axis correction.
                // If the FBX importer already handles Z-up→Y-up, extra rotation makes it worse.
                _model.RotationDegrees = new Vector3(0, 180, 0);

                _modelBaseXRot = _model.RotationDegrees.X;
                GD.Print("[AXISPresence] AXIS Overseer spider mech loaded");
            }
            else
            {
                _model = new Node3D();
                _model.Name = "AXISPlaceholder";
                var box = new MeshInstance3D();
                box.Mesh = new BoxMesh { Size = new Vector3(8, 16, 6) };
                box.MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = AXIS_DARK, Metallic = 0.9f, Roughness = 0.25f,
                    EmissionEnabled = true, Emission = AXIS_RED, EmissionEnergyMultiplier = 0.5f
                };
                _model.AddChild(box);
                AddChild(_model);
                GD.Print("[AXISPresence] AXIS Overseer placeholder (FBX not found)");
            }

            BuildWireSystem();
            BuildLighting();
        }

        // ═════════════════════════════════════════════════════════
        //  WIRE / RAIL SYSTEM — overhead cables AXIS traverses
        // ═════════════════════════════════════════════════════════

        private void BuildWireSystem()
        {
            _wireSystem = new Node3D();
            _wireSystem.Name = "WireSystem";
            AddChild(_wireSystem);

            var wireMat = new StandardMaterial3D
            {
                AlbedoColor = WIRE_COLOR,
                Metallic = 0.85f,
                Roughness = 0.35f,
            };

            var glowMat = new StandardMaterial3D
            {
                AlbedoColor = WIRE_COLOR,
                Metallic = 0.9f,
                Roughness = 0.2f,
                EmissionEnabled = true,
                Emission = AXIS_RED * 0.3f,
                EmissionEnergyMultiplier = 0.3f,
            };

            // Main rail pair — two thick cables running front-to-back
            for (int side = -1; side <= 1; side += 2)
            {
                var rail = CreateWire(WIRE_LENGTH, WIRE_RADIUS * 2f, wireMat);
                rail.Position = new Vector3(side * WIRE_SPREAD * 0.5f, WIRE_HEIGHT, 0);
                rail.RotationDegrees = new Vector3(90, 0, 0); // orient along Z
                _wireSystem.AddChild(rail);
                _wires.Add(rail);
            }

            // Cross-braces connecting the two rails — every 20 units
            for (float z = -WIRE_LENGTH * 0.4f; z <= WIRE_LENGTH * 0.4f; z += 20f)
            {
                var brace = CreateWire(WIRE_SPREAD, WIRE_RADIUS, wireMat);
                brace.Position = new Vector3(0, WIRE_HEIGHT, z);
                brace.RotationDegrees = new Vector3(0, 0, 90); // orient along X
                _wireSystem.AddChild(brace);
            }

            // Suspension cables — angled wires from rail down to near the mech's shoulder area
            for (int side = -1; side <= 1; side += 2)
            {
                for (float z = -15f; z <= 15f; z += 15f)
                {
                    var cable = CreateSuspensionCable(
                        new Vector3(side * WIRE_SPREAD * 0.5f, WIRE_HEIGHT, z),
                        new Vector3(side * WIRE_SPREAD * 0.2f, 2f, z),
                        WIRE_RADIUS * 0.7f, glowMat);
                    _wireSystem.AddChild(cable);
                }
            }

            // Vertical drop cables from rail to anchor points way above (into darkness)
            for (int side = -1; side <= 1; side += 2)
            {
                for (float z = -WIRE_LENGTH * 0.3f; z <= WIRE_LENGTH * 0.3f; z += 40f)
                {
                    var drop = CreateWire(80f, WIRE_RADIUS * 1.5f, wireMat);
                    drop.Position = new Vector3(side * WIRE_SPREAD * 0.5f, WIRE_HEIGHT + 40f, z);
                    // Default orientation is along Y — vertical
                    _wireSystem.AddChild(drop);
                }
            }

            // Small glowing node lights along the rails
            for (float z = -WIRE_LENGTH * 0.35f; z <= WIRE_LENGTH * 0.35f; z += 25f)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var nodeMesh = new MeshInstance3D();
                    nodeMesh.Mesh = new SphereMesh { Radius = 0.4f, Height = 0.8f };
                    nodeMesh.MaterialOverride = glowMat;
                    nodeMesh.Position = new Vector3(side * WIRE_SPREAD * 0.5f, WIRE_HEIGHT, z);
                    _wireSystem.AddChild(nodeMesh);
                }
            }
        }

        private static MeshInstance3D CreateWire(float length, float radius, StandardMaterial3D mat)
        {
            var mesh = new CylinderMesh
            {
                TopRadius = radius,
                BottomRadius = radius,
                Height = length,
                RadialSegments = 6,
            };

            var mi = new MeshInstance3D { Mesh = mesh, MaterialOverride = mat };
            return mi;
        }

        private static MeshInstance3D CreateSuspensionCable(Vector3 from, Vector3 to, float radius, StandardMaterial3D mat)
        {
            float length = from.DistanceTo(to);
            var mi = CreateWire(length, radius, mat);

            // Position at midpoint
            mi.Position = (from + to) * 0.5f;

            // Orient to connect the two points
            Vector3 dir = (to - from).Normalized();
            mi.LookAt(mi.Position + dir, Vector3.Right);
            mi.RotateObjectLocal(Vector3.Right, Mathf.Pi * 0.5f);

            return mi;
        }

        // ═════════════════════════════════════════════════════════
        //  SKELETON POSING & PROCEDURAL WALK
        // ═════════════════════════════════════════════════════════

        private void CacheBoneIndices()
        {
            if (_skeleton == null) return;

            var boneNames = new List<string>();
            for (int i = 0; i < _skeleton.GetBoneCount(); i++)
            {
                string name = _skeleton.GetBoneName(i);
                boneNames.Add(name);
                _bones[name] = i;
            }
            GD.Print($"[AXISPresence] Bones: {string.Join(", ", boneNames)}");
        }

        private void SetBonePose(string boneName, Vector3 eulerDeg)
        {
            if (_skeleton == null) return;
            if (!_bones.TryGetValue(boneName, out int idx)) return;
            var quat = Quaternion.FromEuler(eulerDeg * (Mathf.Pi / 180f));
            _skeleton.SetBonePoseRotation(idx, quat);
        }

        private void SetBonePose(int boneIdx, Vector3 eulerDeg)
        {
            if (_skeleton == null || boneIdx < 0) return;
            var quat = Quaternion.FromEuler(eulerDeg * (Mathf.Pi / 180f));
            _skeleton.SetBonePoseRotation(boneIdx, quat);
        }

        // ── Spider leg bone name templates ──
        // Each leg has 5 segments: Leg1 (hip) → Leg2 → Leg3 → Leg4 → Leg5 (foot)
        private static readonly string[] LEG_GROUPS = { "FrontLeg", "MiddleLeg", "BackLeg" };
        private static readonly string[] SIDES = { "_L", "_R" };

        /// <summary>
        /// Procedural spider walk — tripod gait. Only animates leg segments 1-3
        /// with small rotations on Z axis (curl in/out in bone-local space).
        /// F9 toggles bones off to verify rest pose. F10 for manual pose editor.
        /// </summary>
        private void AnimateLegs()
        {
            if (_skeleton == null) return;

            float walkSpeed = 0.6f;
            float cycle = _time * walkSpeed;

            for (int g = 0; g < LEG_GROUPS.Length; g++)
            {
                string group = LEG_GROUPS[g];
                for (int s = 0; s < SIDES.Length; s++)
                {
                    string side = SIDES[s];

                    // Tripod gait phase
                    bool isGroupA = (group != "MiddleLeg" && side == "_L")
                                 || (group == "MiddleLeg" && side == "_R");
                    float phase = isGroupA ? 0f : 0.5f;
                    float legCycle = (cycle + phase) * Mathf.Tau;

                    float swing = Mathf.Sin(legCycle);
                    float lift = Mathf.Max(0, Mathf.Sin(legCycle));

                    // Segment 1 (hip): small Z rotation for swing
                    // Z-axis curls the leg in bone-local space for spider rigs
                    SetBonePose($"{group}1{side}", new Vector3(0, 0, swing * 5f));

                    // Segment 2: cascading
                    float swing2 = Mathf.Sin(legCycle - 0.4f);
                    SetBonePose($"{group}2{side}", new Vector3(0, 0, swing2 * 4f));

                    // Segment 3 (knee): bend when lifted
                    float bend3 = Mathf.Sin(legCycle - 0.6f);
                    SetBonePose($"{group}3{side}", new Vector3(0, 0, bend3 * 6f));

                    // Segments 4-5: leave at rest
                }
            }
        }

        /// <summary>
        /// Animate body — only Top_M (turret) with small Y rotation for scanning.
        /// No Root_M rotation to avoid flipping.
        /// </summary>
        private void AnimateBody()
        {
            if (_skeleton == null) return;

            // Top turret: slow scan on Z (yaw in bone-local might be Z for this rig)
            float scan = Mathf.Sin(_time * 0.15f) * 8f;
            SetBonePose("Top_M", new Vector3(0, 0, scan));
        }

        private void AnimateSweepBody()
        {
            if (_skeleton == null) return;
            float t = Mathf.Clamp(_sweepTimer / _sweepDuration, 0f, 1f);
            float sweepAngle = Mathf.Lerp(15f, -15f, t);
            SetBonePose("Top_M", new Vector3(0, 0, sweepAngle));
        }

        private void AnimateAssemblyBody()
        {
            if (_skeleton == null) return;
            float scan = Mathf.Sin(_time * 0.2f) * 5f;
            SetBonePose("Top_M", new Vector3(0, 0, scan));
        }

        // ═════════════════════════════════════════════════════════
        //  MATERIALS — dark with visible detail + red accents
        // ═════════════════════════════════════════════════════════

        private static void ApplyAXISMaterials(Node node, float danger)
        {
            int count = 0;
            ApplyAXISMaterialsRecursive(node, danger, ref count);
            GD.Print($"[AXISPresence] Applied AXIS materials to {count} meshes");
        }

        private static void ApplyAXISMaterialsRecursive(Node node, float danger, ref int count)
        {
            if (node is MeshInstance3D mi && mi.Mesh != null)
            {
                string name = mi.Name.ToString().ToLower();

                // Dark metallic base — not pure black so you can see surface detail
                // Slightly lighter with higher danger for drama
                float brightness = 0.04f + danger * 0.02f;
                var bodyColor = new Color(brightness, brightness * 0.9f, brightness * 1.1f);

                StandardMaterial3D mat;

                bool isEmissive = name.Contains("eye") || name.Contains("visor") || name.Contains("cockpit")
                    || name.Contains("light") || name.Contains("lens") || name.Contains("glass")
                    || name.Contains("glow") || name.Contains("emissive") || name.Contains("screen");

                if (isEmissive)
                {
                    mat = new StandardMaterial3D
                    {
                        AlbedoColor = new Color(0.01f, 0.005f, 0.005f),
                        Metallic = 0.95f,
                        Roughness = 0.1f,
                        EmissionEnabled = true,
                        Emission = AXIS_RED,
                        EmissionEnergyMultiplier = 1.2f + danger * 0.5f,
                    };
                }
                else
                {
                    mat = new StandardMaterial3D
                    {
                        AlbedoColor = bodyColor,
                        Metallic = 0.88f,
                        Roughness = 0.25f,
                        // Subtle red rim on all body parts for menacing edge lighting
                        EmissionEnabled = true,
                        Emission = AXIS_RED * 0.05f,
                        EmissionEnergyMultiplier = 0.15f,
                    };
                }

                mi.MaterialOverride = mat;
                for (int i = 0; i < mi.GetSurfaceOverrideMaterialCount(); i++)
                    mi.SetSurfaceOverrideMaterial(i, mat);
                count++;
            }

            foreach (Node child in node.GetChildren())
                ApplyAXISMaterialsRecursive(child, danger, ref count);
        }

        // ═════════════════════════════════════════════════════════
        //  LIGHTING — more dramatic, reveals silhouette
        // ═════════════════════════════════════════════════════════

        private void BuildLighting()
        {
            // Main core glow — red light from the center/chest area
            _coreLight = new OmniLight3D();
            _coreLight.LightColor = AXIS_RED;
            _coreLight.LightEnergy = 2.0f;
            _coreLight.OmniRange = 50f;
            _coreLight.OmniAttenuation = 1.5f;
            _coreLight.ShadowEnabled = false;
            _coreLight.Position = new Vector3(0, 3f, 0);
            AddChild(_coreLight);

            // Under-light — illuminates the area below AXIS, shows scanning presence
            _underLight = new OmniLight3D();
            _underLight.LightColor = AXIS_RED.Lerp(Colors.White, 0.2f);
            _underLight.LightEnergy = 1.5f;
            _underLight.OmniRange = 80f;
            _underLight.OmniAttenuation = 2f;
            _underLight.ShadowEnabled = false;
            _underLight.Position = new Vector3(0, -5f, 0);
            AddChild(_underLight);

            // Eye spotlights — angled down like searchlights
            for (int side = -1; side <= 1; side += 2)
            {
                var eyeSpot = new SpotLight3D();
                eyeSpot.LightColor = AXIS_RED;
                eyeSpot.LightEnergy = 1.0f;
                eyeSpot.SpotRange = 80f;
                eyeSpot.SpotAngle = 25f;
                eyeSpot.RotationDegrees = new Vector3(-70, 0, 0);
                eyeSpot.Position = new Vector3(side * 4f, 6f, -6f);
                eyeSpot.ShadowEnabled = false;
                AddChild(eyeSpot);

                if (side == -1) _eyeSpotLeft = eyeSpot;
                else _eyeSpotRight = eyeSpot;
            }

            // Backlight — subtle white/blue rim light so silhouette pops against dark bg
            var backLight = new SpotLight3D();
            backLight.LightColor = new Color(0.5f, 0.5f, 0.7f);
            backLight.LightEnergy = 0.8f;
            backLight.SpotRange = 60f;
            backLight.SpotAngle = 40f;
            backLight.RotationDegrees = new Vector3(10, 180, 0); // shining from behind
            backLight.Position = new Vector3(0, 10f, 15f);
            backLight.ShadowEnabled = false;
            AddChild(backLight);

            // Wire glow lights — small red lights along the rails
            for (float z = -30f; z <= 30f; z += 20f)
            {
                var wireLight = new OmniLight3D();
                wireLight.LightColor = AXIS_RED;
                wireLight.LightEnergy = 0.4f;
                wireLight.OmniRange = 15f;
                wireLight.OmniAttenuation = 2f;
                wireLight.ShadowEnabled = false;
                wireLight.Position = new Vector3(0, WIRE_HEIGHT, z);
                AddChild(wireLight);
            }
        }

        // ═════════════════════════════════════════════════════════
        //  PUBLIC API — called by DungeonAssemblyIntro
        // ═════════════════════════════════════════════════════════

        public void GestureToward(Vector3 worldPosition)
        {
            var dir = (worldPosition - GlobalPosition).Normalized();
            var tween = CreateTween();
            tween.TweenProperty(this, "rotation_degrees",
                new Vector3(dir.Z * 5f, Mathf.RadToDeg(Mathf.Atan2(dir.X, dir.Z)), dir.X * -3f),
                0.3f).SetEase(Tween.EaseType.Out);
        }

        public void CommandAssembly()
        {
            _assemblyMode = true;
            _sweepMode = false;
        }

        public void CommandSweep(float duration)
        {
            _sweepMode = true;
            _sweepTimer = 0f;
            _sweepDuration = duration;
            _assemblyMode = false;
        }

        public void GoIdle()
        {
            _assemblyMode = false;
            _sweepMode = false;
            _headBaseY = HEAD_Y;
        }

        // ═════════════════════════════════════════════════════════
        //  ANIMATION — procedural body + leg stomping
        // ═════════════════════════════════════════════════════════

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _time += dt;

            if (_model == null || !IsInstanceValid(_model)) return;

            // When pose editor is active, skip all animation so sliders work
            if (_poseEditorActive) return;

            // ── Subtle hover (mech on wires has slight sway) ──
            float hoverY = _headBaseY + Mathf.Sin(_time * HOVER_SPEED * Mathf.Tau) * HOVER_AMPLITUDE;
            float lateralSway = Mathf.Sin(_time * 0.07f * Mathf.Tau) * 0.5f;
            _model.Position = new Vector3(lateralSway, hoverY, 0);

            // ── Slow scanning rotation ──
            float scanAngle = Mathf.Sin(_time * SCAN_SPEED * Mathf.Tau) * SCAN_AMPLITUDE;
            float nod = Mathf.Sin(_time * 0.08f) * 2f;
            float tilt = Mathf.Sin(_time * 0.06f) * 1f;

            // Preserve FBX axis correction on X
            _model.RotationDegrees = new Vector3(_modelBaseXRot + nod, 180f + scanAngle, tilt);

            // ── Sweep mode: deliberate turn tracking the laser ──
            if (_sweepMode)
            {
                _sweepTimer += dt;
                float t = Mathf.Clamp(_sweepTimer / _sweepDuration, 0f, 1f);
                _model.RotationDegrees = new Vector3(
                    _modelBaseXRot + nod - 3f,
                    180f + Mathf.Lerp(SCAN_AMPLITUDE, -SCAN_AMPLITUDE, t),
                    tilt);
            }

            // ── Scale: preserve base scale, slight pulse in assembly ──
            if (_assemblyMode)
            {
                float pulse = 1f + Mathf.Sin(_time * 2f) * 0.015f;
                _model.Scale = _modelBaseScale * pulse;
            }
            else
            {
                _model.Scale = _modelBaseScale;
            }

            // ── Procedural skeleton animation ──
            // DEBUG: disabled to check rest pose orientation. Press F10 for pose editor.
            if (!_debugDisableBones)
            {
                AnimateLegs();
                if (_sweepMode)
                    AnimateSweepBody();
                else if (_assemblyMode)
                    AnimateAssemblyBody();
                else
                    AnimateBody();
            }

            // ── Pulsing lights ──
            float lightPulse = 1f + Mathf.Sin(_time * 2.5f) * 0.3f;
            float scanPulse = 1f + Mathf.Sin(_time * 1.5f) * 0.15f;
            if (_coreLight != null) _coreLight.LightEnergy = 2.0f * lightPulse;
            if (_underLight != null) _underLight.LightEnergy = 1.5f * scanPulse;
            if (_eyeSpotLeft != null) _eyeSpotLeft.LightEnergy = 1.0f * lightPulse;
            if (_eyeSpotRight != null) _eyeSpotRight.LightEnergy = 1.0f * lightPulse;

            // ── Wire system sway ──
            if (_wireSystem != null)
            {
                // Wires sway very subtly with the mech's movement
                float wireSway = Mathf.Sin(_time * 0.1f) * 0.3f;
                _wireSystem.RotationDegrees = new Vector3(wireSway * 0.5f, 0, wireSway);
            }
        }

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

        private static void LogNodeTree(Node node, int depth)
        {
            string indent = new string(' ', depth * 2);
            string extra = "";
            if (node is Node3D n3d)
            {
                var pos = n3d.Position;
                var rot = n3d.RotationDegrees;
                var scl = n3d.Scale;
                if (rot.LengthSquared() > 0.01f || scl != Vector3.One)
                    extra = $" rot=({rot.X:F1},{rot.Y:F1},{rot.Z:F1}) scl=({scl.X:F2},{scl.Y:F2},{scl.Z:F2})";
            }
            if (node is Skeleton3D skel)
                extra += $" [Skeleton3D: {skel.GetBoneCount()} bones]";
            if (node is MeshInstance3D mi)
                extra += $" [Mesh: {mi.Mesh?.GetType().Name ?? "null"}]";

            GD.Print($"[AXISPresence] {indent}{node.GetType().Name} '{node.Name}'{extra}");

            // Only log first 3 levels deep to avoid spam
            if (depth >= 3) return;
            foreach (Node child in node.GetChildren())
                LogNodeTree(child, depth + 1);
        }

        // ═════════════════════════════════════════════════════════
        //  DEBUG POSE EDITOR — F10 to toggle live bone sliders
        // ═════════════════════════════════════════════════════════

        private bool _poseEditorActive;
        private bool _debugDisableBones = false; // Bones on — using safe small rotations
        private CanvasLayer _poseUI;

        private readonly Dictionary<string, Vector3> _poseValues = new()
        {
            { "Root_M", new Vector3(0, 0, 0) },
            { "Top_M", new Vector3(0, 0, 0) },
            { "ShotgunTop_L", new Vector3(0, 0, 0) },
            { "ShotgunTop_R", new Vector3(0, 0, 0) },
            { "ShotgunBot_L", new Vector3(0, 0, 0) },
            { "ShotgunBot_R", new Vector3(0, 0, 0) },
            { "FrontLeg1_L", new Vector3(0, 0, 0) },
            { "FrontLeg1_R", new Vector3(0, 0, 0) },
            { "FrontLeg3_L", new Vector3(0, 0, 0) },
            { "FrontLeg3_R", new Vector3(0, 0, 0) },
            { "MiddleLeg1_L", new Vector3(0, 0, 0) },
            { "MiddleLeg1_R", new Vector3(0, 0, 0) },
            { "MiddleLeg3_L", new Vector3(0, 0, 0) },
            { "MiddleLeg3_R", new Vector3(0, 0, 0) },
            { "BackLeg1_L", new Vector3(0, 0, 0) },
            { "BackLeg1_R", new Vector3(0, 0, 0) },
            { "BackLeg3_L", new Vector3(0, 0, 0) },
            { "BackLeg3_R", new Vector3(0, 0, 0) },
        };

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && !key.Echo)
            {
                if (key.Keycode == Key.F10)
                {
                    TogglePoseEditor();
                    GetViewport().SetInputAsHandled();
                }
                else if (key.Keycode == Key.F9)
                {
                    _debugDisableBones = !_debugDisableBones;
                    GD.Print($"[AXISPresence] Bone animation: {(_debugDisableBones ? "OFF (rest pose)" : "ON")}");
                    if (_debugDisableBones && _skeleton != null)
                    {
                        // Reset all bones to rest pose
                        for (int i = 0; i < _skeleton.GetBoneCount(); i++)
                            _skeleton.SetBonePoseRotation(i, Quaternion.Identity);
                    }
                    GetViewport().SetInputAsHandled();
                }
            }
        }

        private void TogglePoseEditor()
        {
            _poseEditorActive = !_poseEditorActive;

            if (_poseEditorActive)
            {
                Engine.TimeScale = 0;
                ProcessMode = ProcessModeEnum.Always;

                BuildPoseUI();
                ApplyEditorPose();
                GD.Print("[AXISPresence] Pose editor ON — time frozen, drag sliders to pose");
            }
            else
            {
                Engine.TimeScale = 1;
                ProcessMode = ProcessModeEnum.Inherit;

                if (_poseUI != null && IsInstanceValid(_poseUI))
                    _poseUI.QueueFree();
                _poseUI = null;
                GD.Print("[AXISPresence] Pose editor OFF — time resumed");
                PrintPoseCode();
            }
        }

        private void BuildPoseUI()
        {
            _poseUI = new CanvasLayer();
            _poseUI.Layer = 100;
            _poseUI.ProcessMode = ProcessModeEnum.Always;

            var panel = new PanelContainer();
            panel.Position = new Vector2(10, 10);
            panel.Size = new Vector2(420, 650);

            var scroll = new ScrollContainer();
            scroll.CustomMinimumSize = new Vector2(400, 630);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 2);

            var title = new Label();
            title.Text = "AXIS Bone Pose Editor (F10 to close)";
            title.AddThemeColorOverride("font_color", new Color(1, 0.3f, 0.2f));
            vbox.AddChild(title);

            foreach (var boneName in _poseValues.Keys)
            {
                // Skip bones that don't exist on this skeleton
                if (!_bones.ContainsKey(boneName)) continue;

                var boneLabel = new Label();
                boneLabel.Text = boneName;
                boneLabel.AddThemeColorOverride("font_color", new Color(1, 0.9f, 0.5f));
                vbox.AddChild(boneLabel);

                string[] axes = { "X", "Y", "Z" };
                for (int a = 0; a < 3; a++)
                {
                    var hbox = new HBoxContainer();

                    var axLabel = new Label();
                    axLabel.Text = $"  {axes[a]}:";
                    axLabel.CustomMinimumSize = new Vector2(30, 0);
                    hbox.AddChild(axLabel);

                    var slider = new HSlider();
                    slider.MinValue = -180;
                    slider.MaxValue = 180;
                    slider.Step = 1;
                    slider.CustomMinimumSize = new Vector2(280, 0);
                    slider.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

                    var val = _poseValues[boneName];
                    slider.Value = a == 0 ? val.X : a == 1 ? val.Y : val.Z;

                    var valLabel = new Label();
                    valLabel.Text = $"{slider.Value:F0}°";
                    valLabel.CustomMinimumSize = new Vector2(50, 0);

                    string bn = boneName;
                    int axis = a;
                    slider.ValueChanged += (double v) =>
                    {
                        var cur = _poseValues[bn];
                        if (axis == 0) cur.X = (float)v;
                        else if (axis == 1) cur.Y = (float)v;
                        else cur.Z = (float)v;
                        _poseValues[bn] = cur;
                        valLabel.Text = $"{v:F0}°";
                        ApplyEditorPose();
                    };

                    hbox.AddChild(slider);
                    hbox.AddChild(valLabel);
                    vbox.AddChild(hbox);
                }
            }

            scroll.AddChild(vbox);
            panel.AddChild(scroll);
            _poseUI.AddChild(panel);
            AddChild(_poseUI);
        }

        private void ApplyEditorPose()
        {
            if (_skeleton == null) return;
            foreach (var (boneName, euler) in _poseValues)
            {
                if (_bones.TryGetValue(boneName, out int idx))
                {
                    var quat = Quaternion.FromEuler(euler * (Mathf.Pi / 180f));
                    _skeleton.SetBonePoseRotation(idx, quat);
                }
            }
        }

        private void PrintPoseCode()
        {
            GD.Print("=== AXIS POSE VALUES (paste into code) ===");
            foreach (var (boneName, euler) in _poseValues)
            {
                if (euler.LengthSquared() > 0.01f)
                    GD.Print($"SetBonePose(\"{boneName}\", new Vector3({euler.X:F0}, {euler.Y:F0}, {euler.Z:F0}));");
            }
            GD.Print("=== END POSE ===");
        }
    }
}
