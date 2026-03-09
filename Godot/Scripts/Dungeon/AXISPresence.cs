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

        public void Initialize(Color sectorAccent, float danger)
        {
            _accentColor = sectorAccent;

            BuildHead();
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
        //  HANDS — floating articulated panels with finger extensions
        // ═════════════════════════════════════════════════════════

        private void BuildHand(Node3D hand, bool isLeft)
        {
            hand.Name = isLeft ? "AXISLeftHand" : "AXISRightHand";

            // ── Palm ──
            var palm = MakeMesh(new BoxMesh { Size = new Vector3(3.5f, 0.7f, 3f) },
                MakeMetalMat(AXIS_METAL, 0.9f, 0.25f));
            hand.AddChild(palm);

            // ── Palm accent edges ──
            var palmEdge = MakeMesh(new BoxMesh { Size = new Vector3(3.8f, 0.2f, 3.3f) },
                MakeGlowMat(AXIS_DARK_RED, 1.5f));
            palmEdge.Position = new Vector3(0, 0.3f, 0);
            hand.AddChild(palmEdge);

            // ── Palm underside glow (the "activation" surface) ──
            var glowMat = MakeGlowMat(AXIS_RED.Lerp(_accentColor, 0.2f), 2f);
            if (isLeft) _leftPalmGlowMat = glowMat;
            else _rightPalmGlowMat = glowMat;

            var palmGlow = MakeMesh(new BoxMesh { Size = new Vector3(2.5f, 0.15f, 2f) },
                glowMat);
            palmGlow.Position = new Vector3(0, -0.45f, 0);
            hand.AddChild(palmGlow);

            // ── Fingers (4 extensions hanging below the palm) ──
            float[] fingerX = { -1.1f, -0.37f, 0.37f, 1.1f };
            float[] fingerLen = { 2f, 2.5f, 2.5f, 2f };

            for (int f = 0; f < 4; f++)
            {
                float len = fingerLen[f];

                var finger = MakeMesh(new BoxMesh { Size = new Vector3(0.4f, len, 0.4f) },
                    MakeMetalMat(AXIS_METAL_LIGHT, 0.85f, 0.3f));
                finger.Position = new Vector3(fingerX[f], -0.35f - len * 0.5f, -0.8f);
                hand.AddChild(finger);

                // ── Finger joint accent ──
                var joint = MakeMesh(new BoxMesh { Size = new Vector3(0.5f, 0.2f, 0.5f) },
                    MakeGlowMat(AXIS_DARK_RED, 1f));
                joint.Position = new Vector3(fingerX[f], -0.5f, -0.8f);
                hand.AddChild(joint);

                // ── Fingertip glow ──
                var tip = MakeMesh(
                    new SphereMesh { Radius = 0.18f, Height = 0.36f, RadialSegments = 4, Rings = 2 },
                    MakeGlowMat(AXIS_RED, 3f));
                tip.Position = new Vector3(fingerX[f], -0.35f - len - 0.1f, -0.8f);
                hand.AddChild(tip);
            }

            // ── Thumb (thicker, to the side) ──
            float thumbSide = isLeft ? 1.8f : -1.8f;
            var thumb = MakeMesh(new BoxMesh { Size = new Vector3(0.5f, 1.5f, 0.5f) },
                MakeMetalMat(AXIS_METAL_LIGHT, 0.85f, 0.3f));
            thumb.Position = new Vector3(thumbSide, -0.35f - 0.75f, 0.5f);
            thumb.RotationDegrees = new Vector3(0, 0, isLeft ? -20f : 20f);
            hand.AddChild(thumb);
        }

        // ═════════════════════════════════════════════════════════
        //  ENERGY BEAMS — connecting head to hands
        // ═════════════════════════════════════════════════════════

        private void BuildBeams()
        {
            var beamMat = MakeGlowMat(AXIS_RED.Lerp(_accentColor, 0.3f), 2.5f);

            _leftBeam = MakeBeamMesh(beamMat);
            AddChild(_leftBeam);

            _rightBeam = MakeBeamMesh(beamMat);
            AddChild(_rightBeam);

            // Secondary thinner beams for visual density
            var thinMat = MakeGlowMat(AXIS_DARK_RED, 1.5f);
            var leftThin = MakeBeamMesh(thinMat, 0.04f);
            leftThin.Name = "LeftBeamThin";
            AddChild(leftThin);
            var rightThin = MakeBeamMesh(thinMat, 0.04f);
            rightThin.Name = "RightBeamThin";
            AddChild(rightThin);
        }

        private MeshInstance3D MakeBeamMesh(StandardMaterial3D mat, float radius = 0.08f)
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

            // Palm lights on each hand
            var leftPalmLight = new OmniLight3D();
            leftPalmLight.LightColor = AXIS_RED.Lerp(_accentColor, 0.3f);
            leftPalmLight.LightEnergy = 0.4f;
            leftPalmLight.OmniRange = 15f;
            leftPalmLight.OmniAttenuation = 1.5f;
            leftPalmLight.Position = new Vector3(0, -1.5f, 0);
            leftPalmLight.ShadowEnabled = false;
            _leftHand.AddChild(leftPalmLight);

            var rightPalmLight = new OmniLight3D();
            rightPalmLight.LightColor = AXIS_RED.Lerp(_accentColor, 0.3f);
            rightPalmLight.LightEnergy = 0.4f;
            rightPalmLight.OmniRange = 15f;
            rightPalmLight.OmniAttenuation = 1.5f;
            rightPalmLight.Position = new Vector3(0, -1.5f, 0);
            rightPalmLight.ShadowEnabled = false;
            _rightHand.AddChild(rightPalmLight);
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
        /// Return to idle surveillance mode — called when intro finishes.
        /// </summary>
        public void GoIdle()
        {
            _assemblyMode = false;
            _leftGesturing = false;
            _rightGesturing = false;
            _leftHandTargetPos = _leftHandIdlePos;
            _rightHandTargetPos = _rightHandIdlePos;
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
            UpdateBeams();
            AnimateEyes();
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
            _head.RotationDegrees = new Vector3(
                Mathf.Sin(_time * 0.15f) * 3f, // subtle nod
                scanAngle,
                Mathf.Sin(_time * 0.1f) * 1.5f); // subtle tilt
        }

        private void AnimateHands(float dt)
        {
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

            // Beam endpoints: shoulder positions on head, hand palm positions
            Vector3 leftShoulder = _head.Position + new Vector3(-9f, -2f, 0);
            Vector3 rightShoulder = _head.Position + new Vector3(9f, -2f, 0);

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
            var dir = (to - from).Normalized();
            beam.LookAt(beam.GlobalPosition + dir, Vector3.Up);
            beam.RotateObjectLocal(Vector3.Right, Mathf.DegToRad(90f));
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
