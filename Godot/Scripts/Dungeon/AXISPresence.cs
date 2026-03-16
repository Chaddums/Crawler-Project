using Godot;
using System.Collections.Generic;

namespace JunkbotArena
{
    /// <summary>
    /// The AXIS Overseer presence — a massive spider mech silhouette looming in the
    /// background beyond dungeon walls, an industrial truss dome overhead with a swarm
    /// of mini AXIS spider bots crawling on it. During the intro, spiders descend from
    /// the dome to "build" rooms into place. Post-intro, a few ambient spiders remain.
    /// </summary>
    public partial class AXISPresence : Node3D
    {
        // ── Dome + Swarm ──
        private TrussDome _dome;
        public AXISSpiderSwarm Swarm { get; private set; }

        // ── Background AXIS (massive silhouette) ──
        private Node3D _backgroundModel;
        private Skeleton3D _bgSkeleton;
        private readonly Dictionary<string, int> _bgBones = new();

        // ── Animation state ──
        private float _time;

        // ── Assembly mode (driven by DungeonAssemblyIntro) ──
        private bool _assemblyMode;

        // ── Constants ──
        private static readonly Color AXIS_RED = new(1f, 0.12f, 0.08f);
        private static readonly Color AXIS_DARK = new(0.03f, 0.025f, 0.04f);

        private Color _accentColor;
        private float _danger;
        private float _radius;

        // ── Lights ──
        private OmniLight3D _coreLight;

        // ── Spider leg bone name templates ──
        private static readonly string[] LEG_GROUPS = { "FrontLeg", "MiddleLeg", "BackLeg" };
        private static readonly string[] SIDES = { "_L", "_R" };

        public void Initialize(Color sectorAccent, float danger, float radius = 320f)
        {
            _accentColor = sectorAccent;
            _danger = danger;
            _radius = radius;

            // ── Build truss dome overhead ──
            // Lift dome above dungeon (overhead girders at Y=22, rooms at Y=0)
            float domeBaseY = 30f;
            _dome = new TrussDome();
            AddChild(_dome);
            _dome.Position = new Vector3(0, domeBaseY, 0);
            _dome.Initialize(radius);

            // ── Build spider swarm on the dome ──
            // Same Y offset so spider positions match dome beam positions
            Swarm = new AXISSpiderSwarm();
            AddChild(Swarm);
            Swarm.Position = new Vector3(0, domeBaseY, 0);
            Swarm.Initialize(_dome, danger);

            // ── Build massive background AXIS silhouette ──
            BuildBackgroundAXIS();

            // ── Lighting for the dome area ──
            BuildLighting();

            GD.Print("[AXISPresence] Initialized: dome + spider swarm + background AXIS");
        }

        // ═════════════════════════════════════════════════════════
        //  BACKGROUND AXIS — massive silhouette beyond dungeon walls
        // ═════════════════════════════════════════════════════════

        private void BuildBackgroundAXIS()
        {
            _backgroundModel = ModelLibrary.TryLoad("boss", "axis_avatar");
            if (_backgroundModel != null)
            {
                AddChild(_backgroundModel);

                // Scale to 250 units tall — massive looming presence.
                // Use AABB height (not maxDim) so the wide spider legs don't shrink the body.
                ScaleModelToHeight(_backgroundModel, 250f);

                // Position beyond dungeon walls: legs below horizon, body looms over
                _backgroundModel.Position = new Vector3(0, -30f, -_radius - 200f);
                _backgroundModel.RotationDegrees = new Vector3(0, 180, 0);

                // Apply silhouette materials — near-black body, brighter red eye glow
                ApplySilhouetteMaterials(_backgroundModel);

                // Play idle animation to escape T-pose / rest pose
                var animPlayer = FindNodeOfType<AnimationPlayer>(_backgroundModel);
                if (animPlayer != null)
                {
                    var anims = animPlayer.GetAnimationList();
                    string idleAnim = null;
                    foreach (var anim in anims)
                    {
                        if (anim.ToLower().Contains("idle"))
                        {
                            idleAnim = anim;
                            break;
                        }
                    }
                    if (idleAnim == null && anims.Length > 0)
                        idleAnim = anims[0];
                    if (idleAnim != null)
                    {
                        animPlayer.Play(idleAnim);
                        GD.Print($"[AXISPresence] Background AXIS playing animation '{idleAnim}'");
                    }
                }

                // Find skeleton for slow animation
                _bgSkeleton = FindNodeOfType<Skeleton3D>(_backgroundModel);
                if (_bgSkeleton != null)
                    CacheBoneIndices(_bgSkeleton, _bgBones);

                GD.Print("[AXISPresence] Background AXIS silhouette loaded (250 units tall)");
            }
            else
            {
                GD.Print("[AXISPresence] Background AXIS model not found — skipping silhouette");
            }
        }

        private static void ApplySilhouetteMaterials(Node node)
        {
            ApplySilhouetteMaterialsRecursive(node);
        }

        private static void ApplySilhouetteMaterialsRecursive(Node node)
        {
            if (node is MeshInstance3D mi && mi.Mesh != null)
            {
                string name = mi.Name.ToString().ToLower();

                bool isEmissive = name.Contains("eye") || name.Contains("visor")
                    || name.Contains("light") || name.Contains("glow")
                    || name.Contains("screen") || name.Contains("lens");

                StandardMaterial3D mat;
                if (isEmissive)
                {
                    mat = new StandardMaterial3D
                    {
                        AlbedoColor = new Color(0.01f, 0.005f, 0.005f),
                        Metallic = 0.95f,
                        Roughness = 0.1f,
                        EmissionEnabled = true,
                        Emission = AXIS_RED,
                        EmissionEnergyMultiplier = 2.0f,
                    };
                }
                else
                {
                    // Near-black for silhouette effect — fog naturally obscures
                    mat = new StandardMaterial3D
                    {
                        AlbedoColor = new Color(0.015f, 0.012f, 0.02f),
                        Metallic = 0.9f,
                        Roughness = 0.3f,
                        EmissionEnabled = true,
                        Emission = AXIS_RED * 0.02f,
                        EmissionEnergyMultiplier = 0.05f,
                    };
                }

                mi.MaterialOverride = mat;
                for (int i = 0; i < mi.GetSurfaceOverrideMaterialCount(); i++)
                    mi.SetSurfaceOverrideMaterial(i, mat);
            }

            foreach (Node child in node.GetChildren())
                ApplySilhouetteMaterialsRecursive(child);
        }

        // ═════════════════════════════════════════════════════════
        //  SKELETON POSING
        // ═════════════════════════════════════════════════════════

        private static void CacheBoneIndices(Skeleton3D skeleton, Dictionary<string, int> bones)
        {
            bones.Clear();
            for (int i = 0; i < skeleton.GetBoneCount(); i++)
                bones[skeleton.GetBoneName(i)] = i;
        }

        private static void SetBonePose(Skeleton3D skeleton, Dictionary<string, int> bones,
            string boneName, Vector3 eulerDeg)
        {
            if (skeleton == null) return;
            if (!bones.TryGetValue(boneName, out int idx)) return;
            var quat = Quaternion.FromEuler(eulerDeg * (Mathf.Pi / 180f));
            skeleton.SetBonePoseRotation(idx, quat);
        }

        /// <summary>
        /// Animate background AXIS legs at very slow speed (0.1x).
        /// </summary>
        private void AnimateBackgroundLegs()
        {
            if (_bgSkeleton == null) return;

            float walkSpeed = 0.06f; // very slow — atmospheric
            float cycle = _time * walkSpeed;

            for (int g = 0; g < LEG_GROUPS.Length; g++)
            {
                string group = LEG_GROUPS[g];
                for (int s = 0; s < SIDES.Length; s++)
                {
                    string side = SIDES[s];

                    bool isGroupA = (group != "MiddleLeg" && side == "_L")
                                 || (group == "MiddleLeg" && side == "_R");
                    float phase = isGroupA ? 0f : 0.5f;
                    float legCycle = (cycle + phase) * Mathf.Tau;

                    float swing = Mathf.Sin(legCycle);
                    SetBonePose(_bgSkeleton, _bgBones, $"{group}1{side}", new Vector3(0, 0, swing * 5f));

                    float swing2 = Mathf.Sin(legCycle - 0.4f);
                    SetBonePose(_bgSkeleton, _bgBones, $"{group}2{side}", new Vector3(0, 0, swing2 * 4f));

                    float bend3 = Mathf.Sin(legCycle - 0.6f);
                    SetBonePose(_bgSkeleton, _bgBones, $"{group}3{side}", new Vector3(0, 0, bend3 * 6f));
                }
            }
        }

        /// <summary>
        /// Background AXIS body — slow turret scan.
        /// </summary>
        private void AnimateBackgroundBody()
        {
            if (_bgSkeleton == null) return;
            float scan = Mathf.Sin(_time * 0.08f) * 6f;
            SetBonePose(_bgSkeleton, _bgBones, "Top_M", new Vector3(0, 0, scan));
        }

        // ═════════════════════════════════════════════════════════
        //  LIGHTING — dome accent lights + ambient AXIS glow
        // ═════════════════════════════════════════════════════════

        private void BuildLighting()
        {
            // Core glow — red light in the dome center area
            _coreLight = new OmniLight3D();
            _coreLight.LightColor = AXIS_RED;
            _coreLight.LightEnergy = 1.5f;
            _coreLight.OmniRange = 60f;
            _coreLight.OmniAttenuation = 1.5f;
            _coreLight.ShadowEnabled = false;
            _coreLight.Position = new Vector3(0, 40f, 0);
            AddChild(_coreLight);

            // Backlight behind background AXIS for silhouette rim
            var backLight = new SpotLight3D();
            backLight.LightColor = new Color(0.5f, 0.5f, 0.7f);
            backLight.LightEnergy = 0.6f;
            backLight.SpotRange = 300f;
            backLight.SpotAngle = 35f;
            backLight.RotationDegrees = new Vector3(10, 0, 0);
            backLight.Position = new Vector3(0, 50f, -_radius - 250f);
            backLight.ShadowEnabled = false;
            AddChild(backLight);

            // Background AXIS eye glow
            var eyeGlow = new OmniLight3D();
            eyeGlow.LightColor = AXIS_RED;
            eyeGlow.LightEnergy = 1.0f;
            eyeGlow.OmniRange = 100f;
            eyeGlow.OmniAttenuation = 2f;
            eyeGlow.ShadowEnabled = false;
            eyeGlow.Position = new Vector3(0, 80f, -_radius - 200f);
            AddChild(eyeGlow);
        }

        // ═════════════════════════════════════════════════════════
        //  PUBLIC API — called by DungeonAssemblyIntro
        // ═════════════════════════════════════════════════════════

        public void GestureToward(Vector3 worldPosition)
        {
            // Background AXIS looks toward the position (subtle)
            if (_backgroundModel != null && IsInstanceValid(_backgroundModel))
            {
                var dir = (worldPosition - _backgroundModel.GlobalPosition).Normalized();
                float yaw = Mathf.RadToDeg(Mathf.Atan2(dir.X, dir.Z));
                var tween = CreateTween();
                tween.TweenProperty(_backgroundModel, "rotation_degrees",
                    new Vector3(0, 180f + yaw * 0.1f, 0), 0.5f)
                    .SetEase(Tween.EaseType.Out);
            }
        }

        public void CommandAssembly()
        {
            _assemblyMode = true;
        }

        /// <summary>
        /// Spiders descend from dome to build rooms.
        /// </summary>
        public void CommandSpiderDescent(List<Vector3> roomPositions)
        {
            Swarm?.CommandDescent(roomPositions);
        }

        /// <summary>
        /// Spiders retreat back to dome after building.
        /// </summary>
        public void CommandSpiderRetreat()
        {
            Swarm?.CommandRetreat();
        }

        public void GoIdle()
        {
            _assemblyMode = false;
        }

        // ═════════════════════════════════════════════════════════
        //  ANIMATION — procedural background AXIS + light pulses
        // ═════════════════════════════════════════════════════════

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _time += dt;

            // ── Animate background AXIS (very slow) ──
            if (_backgroundModel != null && IsInstanceValid(_backgroundModel))
            {
                if (!_debugDisableBones)
                {
                    AnimateBackgroundLegs();
                    AnimateBackgroundBody();
                }
            }

            // ── Pulsing dome core light ──
            if (_coreLight != null && IsInstanceValid(_coreLight))
            {
                float lightPulse = 1f + Mathf.Sin(_time * 2.5f) * 0.3f;
                _coreLight.LightEnergy = 1.5f * lightPulse;
            }
        }

        /// <summary>
        /// Scale a model so its AABB HEIGHT matches targetHeight (not maxDim).
        /// Prevents the wide spider mech legs from shrinking the overall scale.
        /// </summary>
        private static void ScaleModelToHeight(Node3D model, float targetHeight)
        {
            var aabb = CharacterMeshBuilder.GetModelAabb(model);
            float height = aabb.Size.Y;
            if (height <= 0.001f)
            {
                float maxDim = Mathf.Max(aabb.Size.X, Mathf.Max(aabb.Size.Y, aabb.Size.Z));
                if (maxDim <= 0.001f)
                {
                    model.Scale = Vector3.One * 0.01f * targetHeight;
                    return;
                }
                height = maxDim;
            }
            float scale = targetHeight / height;
            model.Scale = Vector3.One * scale;
            GD.Print($"[AXISPresence] ScaleModelToHeight '{model.Name}' AABB.Y={aabb.Size.Y} targetH={targetHeight} scale={scale}");
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

        // ═════════════════════════════════════════════════════════
        //  DEBUG POSE EDITOR — F10 to toggle live bone sliders
        // ═════════════════════════════════════════════════════════

        private bool _poseEditorActive;
        private bool _debugDisableBones = false;
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
                    if (_debugDisableBones && _bgSkeleton != null)
                    {
                        for (int i = 0; i < _bgSkeleton.GetBoneCount(); i++)
                            _bgSkeleton.SetBonePoseRotation(i, Quaternion.Identity);
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
                if (!_bgBones.ContainsKey(boneName)) continue;

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
            if (_bgSkeleton == null) return;
            foreach (var (boneName, euler) in _poseValues)
            {
                if (_bgBones.TryGetValue(boneName, out int idx))
                {
                    var quat = Quaternion.FromEuler(euler * (Mathf.Pi / 180f));
                    _bgSkeleton.SetBonePoseRotation(idx, quat);
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
