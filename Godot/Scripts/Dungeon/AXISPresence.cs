using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// The AXIS Overseer — a massive floating mechanical construct hovering above the arena.
    /// Angular dark-metal head with a red visor/eyes, two detached floating hands connected
    /// by energy beams. During the dungeon intro, AXIS reaches toward rooms to "reveal" them
    /// from the fog of war. After the intro, AXIS watches and scans from above.
    /// </summary>
    public partial class AXISPresence : Node3D
    {
        // ── Core assemblies ──
        private Node3D _head;
        private Node3D _leftHand;
        private Node3D _rightHand;
        private MeshInstance3D _leftBeam;
        private MeshInstance3D _rightBeam;

        // ── Eye materials (for pulsing) ──
        private StandardMaterial3D _leftEyeMat;
        private StandardMaterial3D _rightEyeMat;
        private StandardMaterial3D _visorMat;

        // ── Palm glow materials (for gesture brightening) ──
        private StandardMaterial3D _leftPalmGlowMat;
        private StandardMaterial3D _rightPalmGlowMat;

        // ── Gesture particles ──
        private GpuParticles3D _leftBurst;
        private GpuParticles3D _rightBurst;

        // ── Animation state ──
        private float _time;
        private float _headBaseY;
        private Vector3 _leftHandIdlePos;
        private Vector3 _rightHandIdlePos;
        private Vector3 _leftHandTargetPos;
        private Vector3 _rightHandTargetPos;
        private bool _leftGesturing;
        private bool _rightGesturing;
        private float _leftGestureTimer;
        private float _rightGestureTimer;
        private bool _nextGestureIsLeft = true;
        private bool _assemblyMode;
        private bool _sweepMode;
        private float _sweepTimer;
        private float _sweepDuration;

        // ── Tuning ──
        private const float GESTURE_DURATION = 1.4f;
        private const float HAND_LERP_SPEED = 2.5f;
        private const float HAND_GESTURE_Y = 15f; // Y level hands reach down to
        private const float HEAD_Y = 42f;
        private const float HAND_IDLE_Y = 26f;
        private const float HAND_IDLE_SPREAD = 20f;

        // ── AXIS signature colors ──
        private static readonly Color AXIS_RED = new(1f, 0.12f, 0.08f);
        private static readonly Color AXIS_DARK_RED = new(0.6f, 0.06f, 0.04f);
        private static readonly Color AXIS_METAL = new(0.05f, 0.05f, 0.065f);
        private static readonly Color AXIS_METAL_LIGHT = new(0.08f, 0.08f, 0.1f);

        private Color _accentColor; // sector accent for secondary effects

        // Tracks whether we are using the FBX model instead of procedural parts
        private bool _usingFbxModel;
        private Node3D _fbxModelNode;

        public void Initialize(Color sectorAccent, float danger)
        {
            _accentColor = sectorAccent;

            // ── Try PolygonMech FBX for the distant AXIS presence ──
            var mechModel = ModelLibrary.TryLoad("boss", "axis_mech");
            if (mechModel != null && HasAnyMesh(mechModel))
            {
                _usingFbxModel = true;
                _fbxModelNode = mechModel;
                _fbxModelNode.Name = "AXISMechModel";

                // Synty modular mech has ALL variant parts visible — hide duplicates,
                // keep only the base set (un-numbered or _01 variants)
                int hidden = StripVariantMeshes(_fbxModelNode);

                CharacterMeshBuilder.ScaleModelToFit(_fbxModelNode, 30f);
                _fbxModelNode.Position = new Vector3(0, 0, 0);
                _fbxModelNode.RotationDegrees = new Vector3(0, 180f, 0);

                ApplyAXISIntroMaterials(_fbxModelNode);

                // Add to tree first — AnimationPlayer needs scene tree for bone paths
                AddChild(_fbxModelNode);

                // Defer animation playback to next frame so skeleton is fully ready
                CallDeferred(nameof(DeferredPlayMechAnimation));

                _head = _fbxModelNode;
                _headBaseY = 0f;

                BuildLighting();

                GD.Print($"[AXISPresence] FBX mech assembled (hidden {hidden} variant meshes)");
                return;
            }

            if (mechModel == null)
                GD.Print("[AXISPresence] PolygonMech FBX not available — using procedural AXIS presence");
            else
            {
                mechModel.QueueFree();
                GD.Print("[AXISPresence] PolygonMech FBX has no mesh content — using procedural AXIS presence");
            }

            // ── Procedural fallback ──
            BuildHead();
            BuildTorso();
            BuildHand(_leftHand = new Node3D(), true);
            BuildHand(_rightHand = new Node3D(), false);
            BuildBeams();
            BuildGestureParticles();
            BuildLighting();

            // Position head
            _headBaseY = HEAD_Y;
            _head.Position = new Vector3(0, _headBaseY, 0);

            // Idle hand positions (floating to each side below the head)
            _leftHandIdlePos = new Vector3(-HAND_IDLE_SPREAD, HAND_IDLE_Y, -5f);
            _rightHandIdlePos = new Vector3(HAND_IDLE_SPREAD, HAND_IDLE_Y, -5f);
            _leftHandTargetPos = _leftHandIdlePos;
            _rightHandTargetPos = _rightHandIdlePos;
            _leftHand.Position = _leftHandIdlePos;
            _rightHand.Position = _rightHandIdlePos;

            AddChild(_leftHand);
            AddChild(_rightHand);

            GD.Print("[AXISPresence] AXIS Overseer constructed");
        }

        /// <summary>
        /// Synty modular mechs export ALL variants visible. This hides duplicate parts,
        /// keeping only the base frame (un-numbered or _01) for each body group.
        /// E.g. keeps geo_c_head_01, hides geo_c_head_02 through _08.
        /// Also hides "Empty" suffix variants and launcher duplicates.
        /// </summary>
        private static int StripVariantMeshes(Node root)
        {
            int hidden = 0;
            StripVariantMeshesRecursive(root, ref hidden);
            return hidden;
        }

        private static void StripVariantMeshesRecursive(Node node, ref int hidden)
        {
            if (node is MeshInstance3D mi)
            {
                string name = mi.Name.ToString().ToLower();

                // Hide "Empty" suffix variants (empty launchers, etc.)
                if (name.Contains("empty"))
                {
                    mi.Visible = false;
                    hidden++;
                }
                // For numbered variants (_02, _03, etc.), hide all but _01
                // Match pattern: ends with _0N or _N where N > 1
                else if (IsHigherVariant(name))
                {
                    mi.Visible = false;
                    hidden++;
                }
            }

            foreach (Node child in node.GetChildren())
                StripVariantMeshesRecursive(child, ref hidden);
        }

        /// <summary>
        /// Returns true if a mesh name represents a higher variant (not the base _01).
        /// Checks the LAST numeric suffix in the name — e.g. "geo_c_head_03" → true,
        /// "geo_c_head_01" → false, "geo_c_chest" → false.
        /// Body-part groups share a common prefix before the final number.
        /// </summary>
        private static bool IsHigherVariant(string name)
        {
            // Find the last underscore followed by digits at end of name
            int lastUnderscore = name.LastIndexOf('_');
            if (lastUnderscore < 0 || lastUnderscore >= name.Length - 1) return false;

            string suffix = name.Substring(lastUnderscore + 1);
            if (!int.TryParse(suffix, out int num)) return false;

            // _01 is the base variant; _02+ are alternates
            // But skip structural parts like "geo_l_index_01" (finger segment, not variant)
            // Finger/thumb segments: _01/_02/_03 are segments, not variants
            string prefix = name.Substring(0, lastUnderscore);
            if (prefix.Contains("index") || prefix.Contains("mid") || prefix.Contains("thumb") ||
                prefix.Contains("ball"))
                return false;

            return num > 1;
        }

        /// <summary>
        /// Apply dark silhouette materials — unified AXIS look using mesh name categories.
        /// </summary>
        private static void ApplyAXISIntroMaterials(Node node)
        {
            int count = 0;
            ApplyAXISIntroMaterialsRecursive(node, ref count);
            GD.Print($"[AXISPresence] Applied AXIS materials to {count} meshes");
        }

        private static void ApplyAXISIntroMaterialsRecursive(Node node, ref int count)
        {
            if (node is MeshInstance3D mi && mi.Mesh != null)
            {
                string name = mi.Name.ToString().ToLower();
                StandardMaterial3D mat;

                if (name.Contains("head") || name.Contains("cockpit"))
                {
                    mat = MakeIntroMat(AXIS_METAL, 0.95f, 0.15f);
                    mat.EmissionEnabled = true;
                    mat.Emission = AXIS_RED;
                    mat.EmissionEnergyMultiplier = 0.6f;
                }
                else if (name.Contains("weapon") || name.Contains("launcher"))
                {
                    mat = MakeIntroMat(new Color(0.04f, 0.03f, 0.06f), 0.9f, 0.2f);
                    mat.EmissionEnabled = true;
                    mat.Emission = new Color(0.35f, 0.15f, 0.55f);
                    mat.EmissionEnergyMultiplier = 0.5f;
                }
                else if (name.Contains("exhaust") || name.Contains("jetpack") || name.Contains("intake"))
                {
                    mat = MakeIntroMat(new Color(0.06f, 0.04f, 0.03f), 0.8f, 0.3f);
                    mat.EmissionEnabled = true;
                    mat.Emission = new Color(0.8f, 0.3f, 0.1f);
                    mat.EmissionEnergyMultiplier = 0.8f;
                }
                else if (name.Contains("armor") || name.Contains("shield"))
                {
                    mat = MakeIntroMat(AXIS_METAL, 0.92f, 0.18f);
                    mat.EmissionEnabled = true;
                    mat.Emission = AXIS_DARK_RED;
                    mat.EmissionEnergyMultiplier = 0.15f;
                }
                else
                {
                    mat = MakeIntroMat(AXIS_METAL_LIGHT, 0.9f, 0.2f);
                    mat.EmissionEnabled = true;
                    mat.Emission = AXIS_DARK_RED;
                    mat.EmissionEnergyMultiplier = 0.1f;
                }

                mi.MaterialOverride = mat;
                // Also override each surface directly in case MaterialOverride
                // doesn't take effect on skinned meshes
                for (int i = 0; i < mi.GetSurfaceOverrideMaterialCount(); i++)
                    mi.SetSurfaceOverrideMaterial(i, mat);
                count++;
            }

            foreach (Node child in node.GetChildren())
                ApplyAXISIntroMaterialsRecursive(child, ref count);
        }

        private static StandardMaterial3D MakeIntroMat(Color color, float metallic, float roughness)
        {
            return new StandardMaterial3D
            {
                AlbedoColor = color,
                Metallic = metallic,
                Roughness = roughness
            };
        }

        /// <summary>
        /// Deferred animation playback — called next frame after AddChild so skeleton is ready.
        /// </summary>
        private void DeferredPlayMechAnimation()
        {
            if (_fbxModelNode == null) return;
            PoseMechSkeleton(_fbxModelNode);
        }

        /// <summary>
        /// Manually pose the mech skeleton into a menacing standing pose.
        /// The Synty POLYGON Mech has no real animation — "Take 001" is the bind pose (T-pose).
        /// We rotate bones directly to lower the arms and create a combat-ready stance.
        /// </summary>
        private static void PoseMechSkeleton(Node3D model)
        {
            var skel = FindNodeOfType<Skeleton3D>(model);
            if (skel == null)
            {
                GD.PrintErr("[AXISPresence] No Skeleton3D — can't pose mech");
                return;
            }

            int boneCount = skel.GetBoneCount();

            // Build bone name → index lookup
            var boneMap = new System.Collections.Generic.Dictionary<string, int>();
            for (int i = 0; i < boneCount; i++)
                boneMap[skel.GetBoneName(i).ToLower()] = i;

            // Arms down at sides (rotate shoulders ~70° around Z)
            PoseBone(skel, boneMap, "l_shoulder", new Vector3(0, 0, -70));
            PoseBone(skel, boneMap, "r_shoulder", new Vector3(0, 0, 70));

            // Elbows bent slightly forward
            PoseBone(skel, boneMap, "l_elbow", new Vector3(-30, 0, 0));
            PoseBone(skel, boneMap, "r_elbow", new Vector3(-30, 0, 0));

            // Hands angled slightly inward
            PoseBone(skel, boneMap, "l_hand", new Vector3(0, 0, -10));
            PoseBone(skel, boneMap, "r_hand", new Vector3(0, 0, 10));

            // Head tilted down — looking at the arena menacingly
            PoseBone(skel, boneMap, "head", new Vector3(-10, 0, 0));

            GD.Print($"[AXISPresence] Mech posed ({boneCount} bones)");
        }

        private static void PoseBone(Skeleton3D skel,
            System.Collections.Generic.Dictionary<string, int> boneMap,
            string boneName, Vector3 eulerDeg)
        {
            if (!boneMap.TryGetValue(boneName, out int idx)) return;
            var quat = Quaternion.FromEuler(eulerDeg * (Mathf.Pi / 180f));
            skel.SetBonePoseRotation(idx, quat);
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

        /// <summary>
        /// Returns true if the node or any descendant has a MeshInstance3D with a mesh assigned.
        /// </summary>
        private static bool HasAnyMesh(Node node)
        {
            if (node is MeshInstance3D mi && mi.Mesh != null)
                return true;
            // Some FBX importers create GeometryInstance3D subtypes that aren't MeshInstance3D
            if (node is GeometryInstance3D)
                return true;
            foreach (Node child in node.GetChildren())
                if (HasAnyMesh(child)) return true;
            return false;
        }


        // ═════════════════════════════════════════════════════════
        //  HEAD — angular display unit with visor and eyes
        // ═════════════════════════════════════════════════════════

        private void BuildHead()
        {
            _head = new Node3D();
            _head.Name = "AXISHead";

            // ── Main body: angular box ──
            var body = MakeMesh(new BoxMesh { Size = new Vector3(14f, 9f, 10f) },
                MakeMetalMat(AXIS_METAL, 0.9f, 0.25f));
            _head.AddChild(body);

            // ── Tapered top (crown ridge) ──
            var crown = MakeMesh(new BoxMesh { Size = new Vector3(10f, 3f, 7f) },
                MakeMetalMat(AXIS_METAL_LIGHT, 0.85f, 0.3f));
            crown.Position = new Vector3(0, 5.5f, 0);
            _head.AddChild(crown);

            // ── Central antenna spike ──
            var spike = MakeMesh(new BoxMesh { Size = new Vector3(0.6f, 6f, 0.6f) },
                MakeMetalMat(AXIS_METAL_LIGHT, 0.8f, 0.35f));
            spike.Position = new Vector3(0, 9f, 0);
            _head.AddChild(spike);

            var spikeTip = MakeMesh(
                new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 6, Rings = 3 },
                MakeGlowMat(AXIS_RED, 4f));
            spikeTip.Position = new Vector3(0, 12.5f, 0);
            _head.AddChild(spikeTip);

            // ── Side antenna pylons ──
            for (int side = -1; side <= 1; side += 2)
            {
                var pylon = MakeMesh(new BoxMesh { Size = new Vector3(0.8f, 4.5f, 0.8f) },
                    MakeMetalMat(AXIS_METAL, 0.85f, 0.3f));
                pylon.Position = new Vector3(side * 6f, 6.5f, 0);
                pylon.RotationDegrees = new Vector3(0, 0, -side * 12f);
                _head.AddChild(pylon);

                var tip = MakeMesh(
                    new SphereMesh { Radius = 0.35f, Height = 0.7f, RadialSegments = 4, Rings = 2 },
                    MakeGlowMat(AXIS_RED, 3f));
                tip.Position = new Vector3(side * 6.8f, 9f, 0);
                _head.AddChild(tip);
            }

            // ── Face plate (front panel) ──
            var faceplate = MakeMesh(new BoxMesh { Size = new Vector3(12f, 7f, 0.5f) },
                MakeMetalMat(new Color(0.03f, 0.03f, 0.04f), 0.95f, 0.2f));
            faceplate.Position = new Vector3(0, 0, -5.3f);
            _head.AddChild(faceplate);

            // ── Visor (wrapping horizontal band — the signature AXIS look) ──
            _visorMat = MakeGlowMat(AXIS_RED, 3f);
            var visor = MakeMesh(new BoxMesh { Size = new Vector3(14.5f, 1.8f, 10.5f) },
                _visorMat);
            visor.Position = new Vector3(0, 1f, 0);
            _head.AddChild(visor);

            // ── Eyes (two bright horizontal slits on the face) ──
            _leftEyeMat = MakeGlowMat(AXIS_RED, 6f);
            _rightEyeMat = MakeGlowMat(AXIS_RED, 6f);

            var leftEye = MakeMesh(new BoxMesh { Size = new Vector3(3.5f, 1.2f, 0.3f) },
                _leftEyeMat);
            leftEye.Position = new Vector3(-2.5f, 1f, -5.55f);
            _head.AddChild(leftEye);

            var rightEye = MakeMesh(new BoxMesh { Size = new Vector3(3.5f, 1.2f, 0.3f) },
                _rightEyeMat);
            rightEye.Position = new Vector3(2.5f, 1f, -5.55f);
            _head.AddChild(rightEye);

            // ── Jaw structure (angular lower section) ──
            var jaw = MakeMesh(new BoxMesh { Size = new Vector3(10f, 3f, 7f) },
                MakeMetalMat(AXIS_METAL, 0.85f, 0.3f));
            jaw.Position = new Vector3(0, -5f, -1f);
            jaw.RotationDegrees = new Vector3(10, 0, 0);
            _head.AddChild(jaw);

            // ── Jaw accent line ──
            var jawLine = MakeMesh(new BoxMesh { Size = new Vector3(9f, 0.35f, 7.5f) },
                MakeGlowMat(AXIS_DARK_RED, 1.5f));
            jawLine.Position = new Vector3(0, -3.8f, -1f);
            _head.AddChild(jawLine);

            // ── Underside glow panel (looking down at the arena) ──
            var underGlow = MakeMesh(new BoxMesh { Size = new Vector3(8f, 0.3f, 6f) },
                MakeGlowMat(AXIS_RED.Lerp(_accentColor, 0.3f), 2f));
            underGlow.Position = new Vector3(0, -4.5f, 0);
            _head.AddChild(underGlow);

            // ── Shoulder shelves (where arms conceptually attach) ──
            for (int side = -1; side <= 1; side += 2)
            {
                var shoulder = MakeMesh(new BoxMesh { Size = new Vector3(4f, 2f, 6f) },
                    MakeMetalMat(AXIS_METAL_LIGHT, 0.8f, 0.35f));
                shoulder.Position = new Vector3(side * 9f, -2f, 0);
                _head.AddChild(shoulder);

                var shoulderAccent = MakeMesh(new BoxMesh { Size = new Vector3(4.5f, 0.3f, 6.5f) },
                    MakeGlowMat(AXIS_DARK_RED, 1.2f));
                shoulderAccent.Position = new Vector3(side * 9f, -1f, 0);
                _head.AddChild(shoulderAccent);
            }

            AddChild(_head);
        }

        // ═════════════════════════════════════════════════════════
        //  TORSO — angular connecting structure between head and hands
        // ═════════════════════════════════════════════════════════

        private Node3D _torso;

        private void BuildTorso()
        {
            _torso = new Node3D();
            _torso.Name = "AXISTorso";
            _torso.Position = new Vector3(0, HEAD_Y - 12f, 0); // below head

            // Main chest — wide angular slab
            var chest = MakeMesh(new BoxMesh { Size = new Vector3(16f, 8f, 8f) },
                MakeMetalMat(AXIS_METAL, 0.9f, 0.25f));
            _torso.AddChild(chest);

            // Central core glow
            var coreGlow = MakeMesh(new BoxMesh { Size = new Vector3(4f, 4f, 0.5f) },
                MakeGlowMat(AXIS_RED, 4f));
            coreGlow.Position = new Vector3(0, 0, -4.3f);
            _torso.AddChild(coreGlow);

            // Shoulder blocks — where beams visually connect
            for (int side = -1; side <= 1; side += 2)
            {
                var shoulder = MakeMesh(new BoxMesh { Size = new Vector3(5f, 5f, 6f) },
                    MakeMetalMat(AXIS_METAL_LIGHT, 0.85f, 0.3f));
                shoulder.Position = new Vector3(side * 10f, 2f, 0);
                _torso.AddChild(shoulder);

                // Shoulder glow accent
                var shoulderGlow = MakeMesh(new BoxMesh { Size = new Vector3(5.5f, 0.4f, 6.5f) },
                    MakeGlowMat(AXIS_DARK_RED, 2f));
                shoulderGlow.Position = new Vector3(side * 10f, 4.5f, 0);
                _torso.AddChild(shoulderGlow);
            }

            // Spine glow strip (vertical line down the front)
            var spine = MakeMesh(new BoxMesh { Size = new Vector3(0.6f, 8f, 0.3f) },
                MakeGlowMat(AXIS_RED, 2.5f));
            spine.Position = new Vector3(0, 0, -4.2f);
            _torso.AddChild(spine);

            // Lower trim
            var lowerTrim = MakeMesh(new BoxMesh { Size = new Vector3(14f, 0.4f, 8.5f) },
                MakeGlowMat(AXIS_DARK_RED, 1.5f));
            lowerTrim.Position = new Vector3(0, -4.2f, 0);
            _torso.AddChild(lowerTrim);

            AddChild(_torso);
        }

        // ═════════════════════════════════════════════════════════
        //  HANDS — floating articulated panels with finger extensions
        // ═════════════════════════════════════════════════════════

        private void BuildHand(Node3D hand, bool isLeft)
        {
            hand.Name = isLeft ? "AXISLeftHand" : "AXISRightHand";

            // ── Forearm connector (visible structural piece linking beam to palm) ──
            var forearm = MakeMesh(new BoxMesh { Size = new Vector3(1.5f, 5f, 1.5f) },
                MakeMetalMat(AXIS_METAL_LIGHT, 0.85f, 0.3f));
            forearm.Position = new Vector3(0, 3f, 0);
            hand.AddChild(forearm);

            var forearmGlow = MakeMesh(new BoxMesh { Size = new Vector3(1.8f, 0.3f, 1.8f) },
                MakeGlowMat(AXIS_DARK_RED, 2f));
            forearmGlow.Position = new Vector3(0, 5.5f, 0);
            hand.AddChild(forearmGlow);

            // ── Palm — larger, with emissive border ──
            var palm = MakeMesh(new BoxMesh { Size = new Vector3(5f, 1f, 4f) },
                MakeMetalMat(AXIS_METAL, 0.9f, 0.25f));
            hand.AddChild(palm);

            // ── Palm emissive border (makes hand shape visible at distance) ──
            var palmBorderTop = MakeMesh(new BoxMesh { Size = new Vector3(5.3f, 0.25f, 4.3f) },
                MakeGlowMat(AXIS_RED, 2.5f));
            palmBorderTop.Position = new Vector3(0, 0.5f, 0);
            hand.AddChild(palmBorderTop);

            var palmBorderBot = MakeMesh(new BoxMesh { Size = new Vector3(5.3f, 0.25f, 4.3f) },
                MakeGlowMat(AXIS_DARK_RED, 2f));
            palmBorderBot.Position = new Vector3(0, -0.5f, 0);
            hand.AddChild(palmBorderBot);

            // ── Palm underside glow (the "activation" surface) ──
            var glowMat = MakeGlowMat(AXIS_RED.Lerp(_accentColor, 0.2f), 3f);
            if (isLeft) _leftPalmGlowMat = glowMat;
            else _rightPalmGlowMat = glowMat;

            var palmGlow = MakeMesh(new BoxMesh { Size = new Vector3(3.5f, 0.2f, 2.5f) },
                glowMat);
            palmGlow.Position = new Vector3(0, -0.6f, 0);
            hand.AddChild(palmGlow);

            // ── Fingers (4 extensions hanging below the palm) ──
            float[] fingerX = { -1.5f, -0.5f, 0.5f, 1.5f };
            float[] fingerLen = { 2.5f, 3f, 3f, 2.5f };

            for (int f = 0; f < 4; f++)
            {
                float len = fingerLen[f];

                var finger = MakeMesh(new BoxMesh { Size = new Vector3(0.6f, len, 0.6f) },
                    MakeMetalMat(AXIS_METAL_LIGHT, 0.85f, 0.3f));
                finger.Position = new Vector3(fingerX[f], -0.5f - len * 0.5f, -1f);
                hand.AddChild(finger);

                // ── Finger joint accent ──
                var joint = MakeMesh(new BoxMesh { Size = new Vector3(0.7f, 0.25f, 0.7f) },
                    MakeGlowMat(AXIS_DARK_RED, 1.5f));
                joint.Position = new Vector3(fingerX[f], -0.6f, -1f);
                hand.AddChild(joint);

                // ── Fingertip glow ──
                var tip = MakeMesh(
                    new SphereMesh { Radius = 0.25f, Height = 0.5f, RadialSegments = 6, Rings = 3 },
                    MakeGlowMat(AXIS_RED, 4f));
                tip.Position = new Vector3(fingerX[f], -0.5f - len - 0.15f, -1f);
                hand.AddChild(tip);
            }

            // ── Thumb (thicker, to the side) ──
            float thumbSide = isLeft ? 2.6f : -2.6f;
            var thumb = MakeMesh(new BoxMesh { Size = new Vector3(0.7f, 2f, 0.7f) },
                MakeMetalMat(AXIS_METAL_LIGHT, 0.85f, 0.3f));
            thumb.Position = new Vector3(thumbSide, -0.5f - 1f, 0.5f);
            thumb.RotationDegrees = new Vector3(0, 0, isLeft ? -20f : 20f);
            hand.AddChild(thumb);
        }

        // ═════════════════════════════════════════════════════════
        //  ENERGY BEAMS — connecting head to hands
        // ═════════════════════════════════════════════════════════

        private void BuildBeams()
        {
            var beamMat = MakeGlowMat(AXIS_RED.Lerp(_accentColor, 0.3f), 3f);

            _leftBeam = MakeBeamMesh(beamMat);
            AddChild(_leftBeam);

            _rightBeam = MakeBeamMesh(beamMat);
            AddChild(_rightBeam);

            // Secondary beams for visual density
            var thinMat = MakeGlowMat(AXIS_DARK_RED, 2f);
            var leftThin = MakeBeamMesh(thinMat, 0.2f);
            leftThin.Name = "LeftBeamThin";
            AddChild(leftThin);
            var rightThin = MakeBeamMesh(thinMat, 0.2f);
            rightThin.Name = "RightBeamThin";
            AddChild(rightThin);
        }

        private MeshInstance3D MakeBeamMesh(StandardMaterial3D mat, float radius = 0.4f)
        {
            var mesh = new MeshInstance3D();
            var cyl = new CylinderMesh();
            cyl.TopRadius = radius;
            cyl.BottomRadius = radius;
            cyl.Height = 1f; // Scaled dynamically in _Process
            cyl.RadialSegments = 4;
            mesh.Mesh = cyl;
            mesh.MaterialOverride = mat;
            return mesh;
        }

        // ═════════════════════════════════════════════════════════
        //  GESTURE PARTICLES — downward burst when hand activates
        // ═════════════════════════════════════════════════════════

        private void BuildGestureParticles()
        {
            _leftBurst = CreateBurstParticles();
            _leftHand.AddChild(_leftBurst);

            _rightBurst = CreateBurstParticles();
            _rightHand.AddChild(_rightBurst);
        }

        private GpuParticles3D CreateBurstParticles()
        {
            var burst = new GpuParticles3D();
            burst.Amount = 24;
            burst.Lifetime = 0.6f;
            burst.OneShot = true;
            burst.Emitting = false;
            burst.Explosiveness = 0.9f;
            burst.VisibilityAabb = new Aabb(new Vector3(-5, -8, -5), new Vector3(10, 10, 10));

            var pmat = new ParticleProcessMaterial();
            pmat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
            pmat.EmissionSphereRadius = 0.5f;
            pmat.Direction = new Vector3(0, -1, 0);
            pmat.Spread = 25f;
            pmat.InitialVelocityMin = 5f;
            pmat.InitialVelocityMax = 12f;
            pmat.Gravity = new Vector3(0, -8f, 0);
            pmat.ScaleMin = 0.05f;
            pmat.ScaleMax = 0.15f;

            var grad = new Gradient();
            grad.SetColor(0, new Color(AXIS_RED.R, AXIS_RED.G, AXIS_RED.B, 1f));
            grad.AddPoint(0.4f, new Color(1f, 0.3f, 0.1f, 0.7f));
            grad.SetColor(1, new Color(0.5f, 0.1f, 0.05f, 0f));
            var tex = new GradientTexture1D();
            tex.Gradient = grad;
            pmat.ColorRamp = tex;

            burst.ProcessMaterial = pmat;

            var mesh = new SphereMesh();
            mesh.Radius = 0.06f;
            mesh.Height = 0.12f;
            mesh.RadialSegments = 3;
            mesh.Rings = 2;
            mesh.Material = MakeGlowMat(AXIS_RED, 6f);
            burst.DrawPass1 = mesh;
            burst.Position = new Vector3(0, -2f, 0); // Below the palm

            return burst;
        }

        // ═════════════════════════════════════════════════════════
        //  LIGHTING — AXIS's own atmospheric lights
        // ═════════════════════════════════════════════════════════

        private void BuildLighting()
        {
            // Head glow (red omni)
            var headLight = new OmniLight3D();
            headLight.LightColor = AXIS_RED;
            headLight.LightEnergy = 0.8f;
            headLight.OmniRange = 30f;
            headLight.OmniAttenuation = 1.5f;
            headLight.Position = new Vector3(0, 0, -4f);
            headLight.ShadowEnabled = false;
            _head.AddChild(headLight);

            // Eye spotlights pointing down at the arena
            for (int side = -1; side <= 1; side += 2)
            {
                var eyeSpot = new SpotLight3D();
                eyeSpot.LightColor = AXIS_RED;
                eyeSpot.LightEnergy = 0.5f;
                eyeSpot.SpotRange = 50f;
                eyeSpot.SpotAngle = 18f;
                eyeSpot.RotationDegrees = new Vector3(-75, 0, 0); // Mostly downward
                eyeSpot.Position = new Vector3(side * 2.5f, -1f, -5.5f);
                eyeSpot.ShadowEnabled = false;
                _head.AddChild(eyeSpot);
            }

            // Palm lights on each hand (only if procedural hands exist)
            if (_leftHand != null)
            {
                var leftPalmLight = new OmniLight3D();
                leftPalmLight.LightColor = AXIS_RED.Lerp(_accentColor, 0.3f);
                leftPalmLight.LightEnergy = 0.4f;
                leftPalmLight.OmniRange = 15f;
                leftPalmLight.OmniAttenuation = 1.5f;
                leftPalmLight.Position = new Vector3(0, -1.5f, 0);
                leftPalmLight.ShadowEnabled = false;
                _leftHand.AddChild(leftPalmLight);
            }

            if (_rightHand != null)
            {
                var rightPalmLight = new OmniLight3D();
                rightPalmLight.LightColor = AXIS_RED.Lerp(_accentColor, 0.3f);
                rightPalmLight.LightEnergy = 0.4f;
                rightPalmLight.OmniRange = 15f;
                rightPalmLight.OmniAttenuation = 1.5f;
                rightPalmLight.Position = new Vector3(0, -1.5f, 0);
                rightPalmLight.ShadowEnabled = false;
                _rightHand.AddChild(rightPalmLight);
            }
        }

        // ═════════════════════════════════════════════════════════
        //  PUBLIC API — called by DungeonAssemblyIntro
        // ═════════════════════════════════════════════════════════

        /// <summary>
        /// AXIS reaches one hand toward a world position (alternates left/right).
        /// Called during the intro room reveal sequence.
        /// </summary>
        public void GestureToward(Vector3 worldPosition)
        {
            // Convert world → local (AXISPresence is child of DungeonBackdrop)
            var localPos = worldPosition - GlobalPosition;
            var target = new Vector3(localPos.X, HAND_GESTURE_Y, localPos.Z);

            if (_nextGestureIsLeft)
            {
                _leftHandTargetPos = target;
                _leftGesturing = true;
                _leftGestureTimer = GESTURE_DURATION;
            }
            else
            {
                _rightHandTargetPos = target;
                _rightGesturing = true;
                _rightGestureTimer = GESTURE_DURATION;
            }
            _nextGestureIsLeft = !_nextGestureIsLeft;
        }

        /// <summary>
        /// Both hands spread wide — called when rooms fly to final positions.
        /// </summary>
        public void CommandAssembly()
        {
            _assemblyMode = true;
            _leftHandTargetPos = new Vector3(-50f, 20f, 0);
            _rightHandTargetPos = new Vector3(50f, 20f, 0);
            _leftGesturing = true;
            _rightGesturing = true;
            _leftGestureTimer = 4f;
            _rightGestureTimer = 4f;
        }

        /// <summary>
        /// Hands raised and waving right to left — called during laser cone sweep.
        /// </summary>
        public void CommandSweep(float duration)
        {
            _sweepMode = true;
            _sweepTimer = 0f;
            _sweepDuration = duration;
            _assemblyMode = false;
            _leftGesturing = true;
            _rightGesturing = true;
            _leftGestureTimer = duration + 2f;
            _rightGestureTimer = duration + 2f;

            // Fire initial palm bursts
            if (_leftBurst != null) _leftBurst.Restart();
            if (_rightBurst != null) _rightBurst.Restart();
        }

        /// <summary>
        /// Return to idle surveillance mode — called when intro finishes.
        /// </summary>
        public void GoIdle()
        {
            _assemblyMode = false;
            _sweepMode = false;
            _leftGesturing = false;
            _rightGesturing = false;
            _leftHandTargetPos = _leftHandIdlePos;
            _rightHandTargetPos = _rightHandIdlePos;

            // Elevate the mech/head to surveillance height now that the intro is done.
            // During the intro, _headBaseY stays at 0 so the intro's Scale/Position
            // control works correctly. After intro, AXIS resets to (0,0,0) local and
            // we raise the head to loom overhead.
            _headBaseY = HEAD_Y;
        }

        // ═════════════════════════════════════════════════════════
        //  ANIMATION
        // ═════════════════════════════════════════════════════════

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _time += dt;

            AnimateHead(dt);
            AnimateHands(dt);
            if (!_usingFbxModel) UpdateBeams();
            if (!_usingFbxModel) AnimateEyes();
            AnimatePalmGlow();
            CheckGestureBursts();
        }

        private void AnimateHead(float dt)
        {
            if (_head == null || !IsInstanceValid(_head)) return;

            // Gentle bob
            float bobY = _headBaseY + Mathf.Sin(_time * 0.25f) * 0.6f;

            // Slow scanning rotation (±12 degrees)
            float scanAngle = Mathf.Sin(_time * 0.04f * Mathf.Tau) * 12f;

            _head.Position = new Vector3(0, bobY, 0);

            // FBX model faces -Z via 180° Y rotation — preserve that base
            float baseYaw = _usingFbxModel ? 180f : 0f;
            _head.RotationDegrees = new Vector3(
                Mathf.Sin(_time * 0.15f) * 3f, // subtle nod
                baseYaw + scanAngle,
                Mathf.Sin(_time * 0.1f) * 1.5f); // subtle tilt

            // Torso follows head bob (slightly dampened)
            if (_torso != null && IsInstanceValid(_torso))
            {
                float torsoY = HEAD_Y - 12f + Mathf.Sin(_time * 0.25f) * 0.3f;
                _torso.Position = new Vector3(0, torsoY, 0);
                _torso.RotationDegrees = new Vector3(0, scanAngle * 0.5f, 0);
            }
        }

        private void AnimateHands(float dt)
        {
            // During sweep, hands stay spread apart and wave while raised
            if (_sweepMode)
            {
                _sweepTimer += dt;
                float t = Mathf.Clamp(_sweepTimer / _sweepDuration, 0f, 1f);

                // Hands raised high, each on their own side
                float sweepY = HAND_IDLE_Y + 10f;
                float fwd = -8f;

                // Gentle right-to-left drift on both hands (commanding the scan)
                float drift = Mathf.Lerp(6f, -6f, t);

                // Independent waving — each hand oscillates up/down on its own phase
                float leftWave = Mathf.Sin(_sweepTimer * 3.5f) * 4f;
                float rightWave = Mathf.Sin(_sweepTimer * 3.5f + 2.0f) * 4f;

                // Small forward/back sway for organic feel
                float leftSway = Mathf.Sin(_sweepTimer * 2.2f) * 2f;
                float rightSway = Mathf.Sin(_sweepTimer * 2.2f + 1.3f) * 2f;

                if (_leftHand != null && IsInstanceValid(_leftHand))
                    _leftHand.Position = new Vector3(
                        -HAND_IDLE_SPREAD + drift,
                        sweepY + leftWave,
                        fwd + leftSway);
                if (_rightHand != null && IsInstanceValid(_rightHand))
                    _rightHand.Position = new Vector3(
                        HAND_IDLE_SPREAD + drift,
                        sweepY + rightWave,
                        fwd + rightSway);

                return;
            }

            // Countdown gesture timers
            if (_leftGesturing)
            {
                _leftGestureTimer -= dt;
                if (_leftGestureTimer <= 0)
                {
                    _leftGesturing = false;
                    _leftHandTargetPos = _leftHandIdlePos;
                }
            }

            if (_rightGesturing)
            {
                _rightGestureTimer -= dt;
                if (_rightGestureTimer <= 0)
                {
                    _rightGesturing = false;
                    _rightHandTargetPos = _rightHandIdlePos;
                }
            }

            // Idle hand motion (gentle figure-8)
            Vector3 leftTarget = _leftGesturing ? _leftHandTargetPos : _leftHandIdlePos
                + new Vector3(
                    Mathf.Sin(_time * 0.3f) * 2f,
                    Mathf.Sin(_time * 0.4f) * 1.5f,
                    Mathf.Cos(_time * 0.25f) * 2f);

            Vector3 rightTarget = _rightGesturing ? _rightHandTargetPos : _rightHandIdlePos
                + new Vector3(
                    Mathf.Sin(_time * 0.3f + 1.5f) * 2f,
                    Mathf.Sin(_time * 0.4f + 1f) * 1.5f,
                    Mathf.Cos(_time * 0.25f + 2f) * 2f);

            // Smooth interpolation
            float speed = HAND_LERP_SPEED * dt;
            if (_leftHand != null && IsInstanceValid(_leftHand))
                _leftHand.Position = _leftHand.Position.Lerp(leftTarget, speed);

            if (_rightHand != null && IsInstanceValid(_rightHand))
                _rightHand.Position = _rightHand.Position.Lerp(rightTarget, speed);
        }

        private void UpdateBeams()
        {
            if (_head == null) return;

            // Beam endpoints: shoulder positions on torso, hand palm positions
            Vector3 leftShoulder = _torso != null
                ? _torso.Position + new Vector3(-10f, 2f, 0)
                : _head.Position + new Vector3(-9f, -2f, 0);
            Vector3 rightShoulder = _torso != null
                ? _torso.Position + new Vector3(10f, 2f, 0)
                : _head.Position + new Vector3(9f, -2f, 0);

            PositionBeam(_leftBeam, leftShoulder, _leftHand?.Position ?? _leftHandIdlePos);
            PositionBeam(_rightBeam, rightShoulder, _rightHand?.Position ?? _rightHandIdlePos);

            // Thin secondary beams (offset slightly)
            var leftThin = GetNodeOrNull<MeshInstance3D>("LeftBeamThin");
            var rightThin = GetNodeOrNull<MeshInstance3D>("RightBeamThin");
            if (leftThin != null)
                PositionBeam(leftThin, leftShoulder + new Vector3(-0.5f, 0.3f, 0),
                    (_leftHand?.Position ?? _leftHandIdlePos) + new Vector3(-0.3f, 0.2f, 0));
            if (rightThin != null)
                PositionBeam(rightThin, rightShoulder + new Vector3(0.5f, 0.3f, 0),
                    (_rightHand?.Position ?? _rightHandIdlePos) + new Vector3(0.3f, 0.2f, 0));
        }

        private void PositionBeam(MeshInstance3D beam, Vector3 from, Vector3 to)
        {
            if (beam == null || !IsInstanceValid(beam)) return;

            float dist = from.DistanceTo(to);
            if (dist < 1f) { beam.Visible = false; return; }
            beam.Visible = true;

            beam.Position = (from + to) * 0.5f;

            // Build orientation in local space — cylinder points along Y by default,
            // so we need to rotate it to align with the from→to direction.
            var dir = (to - from).Normalized();
            beam.Basis = Basis.Identity;
            // Use cross products to build a basis that aligns local Y with dir
            var up = dir;
            var right = up.Cross(Vector3.Forward).Normalized();
            if (right.LengthSquared() < 0.001f)
                right = up.Cross(Vector3.Right).Normalized();
            var forward = right.Cross(up).Normalized();
            beam.Basis = new Basis(right, up, forward);
            beam.Scale = new Vector3(1f, dist, 1f);
        }

        private void AnimateEyes()
        {
            // Pulsing red glow
            float pulse = 1f + Mathf.Sin(_time * 2.5f) * 0.25f
                + Mathf.Sin(_time * 5.7f) * 0.1f;

            if (_leftEyeMat != null) _leftEyeMat.EmissionEnergyMultiplier = 6f * pulse;
            if (_rightEyeMat != null) _rightEyeMat.EmissionEnergyMultiplier = 6f * pulse;

            // Visor pulses more slowly
            float visorPulse = 1f + Mathf.Sin(_time * 1.5f) * 0.15f;
            if (_visorMat != null) _visorMat.EmissionEnergyMultiplier = 3f * visorPulse;
        }

        private void AnimatePalmGlow()
        {
            // Palms glow brighter when actively gesturing
            float leftGlow = _leftGesturing ? 5f : 2f;
            float rightGlow = _rightGesturing ? 5f : 2f;

            // Smooth interpolation via sin blend
            float leftActual = Mathf.Lerp(2f, leftGlow, _leftGesturing ? 1f : 0f);
            float rightActual = Mathf.Lerp(2f, rightGlow, _rightGesturing ? 1f : 0f);

            if (_leftPalmGlowMat != null) _leftPalmGlowMat.EmissionEnergyMultiplier = leftActual;
            if (_rightPalmGlowMat != null) _rightPalmGlowMat.EmissionEnergyMultiplier = rightActual;
        }

        private void CheckGestureBursts()
        {
            // Fire a particle burst when a hand reaches near its target
            if (_leftGesturing && _leftHand != null && IsInstanceValid(_leftHand))
            {
                float dist = _leftHand.Position.DistanceTo(_leftHandTargetPos);
                if (dist < 3f && _leftGestureTimer > GESTURE_DURATION * 0.5f)
                {
                    if (_leftBurst != null && !_leftBurst.Emitting)
                        _leftBurst.Restart();
                }
            }

            if (_rightGesturing && _rightHand != null && IsInstanceValid(_rightHand))
            {
                float dist = _rightHand.Position.DistanceTo(_rightHandTargetPos);
                if (dist < 3f && _rightGestureTimer > GESTURE_DURATION * 0.5f)
                {
                    if (_rightBurst != null && !_rightBurst.Emitting)
                        _rightBurst.Restart();
                }
            }
        }

        // ═════════════════════════════════════════════════════════
        //  MATERIAL HELPERS
        // ═════════════════════════════════════════════════════════

        private static StandardMaterial3D MakeMetalMat(Color color, float metallic, float roughness)
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = color;
            mat.Metallic = metallic;
            mat.Roughness = roughness;
            return mat;
        }

        private static StandardMaterial3D MakeGlowMat(Color color, float energy)
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = color;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.EmissionEnabled = true;
            mat.Emission = color;
            mat.EmissionEnergyMultiplier = energy;
            return mat;
        }

        private static MeshInstance3D MakeMesh(Mesh mesh, StandardMaterial3D mat)
        {
            var mi = new MeshInstance3D();
            mi.Mesh = mesh;
            mi.MaterialOverride = mat;
            return mi;
        }
    }
}
