using Godot;
using System.Collections.Generic;

namespace JunkbotArena
{
    /// <summary>
    /// Tween/sin-wave limb animation for procedural character bodies.
    /// Discovers named pivots in a nested hierarchy:
    ///   Top-level: Head, Torso, LeftArm, RightArm, LeftLeg, RightLeg, Weapon
    ///   Sub-joints: LeftElbow, RightElbow, LeftHand, RightHand,
    ///               LeftKnee, RightKnee, LeftAnkle, RightAnkle
    /// Nested pivots give natural IK-like motion — rotating a shoulder
    /// automatically carries the elbow, forearm, and hand along with it.
    /// </summary>
    public partial class ProceduralAnimator : Node, IAnimatable
    {
        private Node3D _bodyRoot;
        private AnimState _currentState = AnimState.Idle;
        private float _cycleTimer;
        private Tween _activeTween;
        private bool _initialized;

        // ── Top-level pivots ──
        private Node3D _head;
        private Node3D _torso;
        private Node3D _leftArm;
        private Node3D _rightArm;
        private Node3D _leftLeg;
        private Node3D _rightLeg;
        private Node3D _weapon;

        // ── Sub-joint pivots (nested inside arms/legs) ──
        private Node3D _leftElbow;
        private Node3D _rightElbow;
        private Node3D _leftHand;
        private Node3D _rightHand;
        private Node3D _leftKnee;
        private Node3D _rightKnee;
        private Node3D _leftAnkle;
        private Node3D _rightAnkle;

        // Original transforms for baselines
        private readonly Dictionary<Node3D, Vector3> _basePositions = new();
        private readonly Dictionary<Node3D, Vector3> _baseRotations = new();

        // Track wheel nodes (children named _Wheel*)
        private readonly List<Node3D> _wheels = new();
        private bool _hasWheels;
        private bool _hasBipedLegs; // has nested knee/ankle joints
        private bool _hasArticulatedArms; // has nested elbow/hand joints

        // All discovered parts for batch operations
        private readonly List<Node3D> _allParts = new();

        // Scale factor for animations on larger models (bosses)
        private float _animScale = 1f;

        public AnimState CurrentState => _currentState;

        public void Initialize(Node3D bodyRoot)
        {
            _bodyRoot = bodyRoot;
            if (_bodyRoot == null) return;

            // Find top-level named parts
            _head = FindPart("Head");
            _torso = FindPart("Torso");
            _leftArm = FindPart("LeftArm");
            _rightArm = FindPart("RightArm");
            _leftLeg = FindPart("LeftLeg");
            _rightLeg = FindPart("RightLeg");
            _weapon = FindPart("Weapon");

            // Enemy-specific fallbacks
            if (_head == null) _head = FindPart("Body");
            if (_leftArm == null) _leftArm = FindPart("Crossbar");
            if (_rightLeg == null) _rightLeg = FindPart("Tail");

            // Find sub-joints (nested inside arm/leg pivots)
            _leftElbow = FindPart("LeftElbow");
            _rightElbow = FindPart("RightElbow");
            _leftHand = FindPart("LeftHand");
            _rightHand = FindPart("RightHand");
            _leftKnee = FindPart("LeftKnee");
            _rightKnee = FindPart("RightKnee");
            _leftAnkle = FindPart("LeftAnkle");
            _rightAnkle = FindPart("RightAnkle");

            _hasArticulatedArms = _leftElbow != null || _rightElbow != null;
            _hasBipedLegs = _leftKnee != null || _rightKnee != null;

            // Store baselines for all parts
            _allParts.Clear();
            _basePositions.Clear();
            _baseRotations.Clear();

            StorePart(_head);
            StorePart(_torso);
            StorePart(_leftArm);
            StorePart(_rightArm);
            StorePart(_leftLeg);
            StorePart(_rightLeg);
            StorePart(_weapon);
            StorePart(_leftElbow);
            StorePart(_rightElbow);
            StorePart(_leftHand);
            StorePart(_rightHand);
            StorePart(_leftKnee);
            StorePart(_rightKnee);
            StorePart(_leftAnkle);
            StorePart(_rightAnkle);

            // Detect track wheels on legs
            _wheels.Clear();
            CollectWheels(_leftLeg);
            CollectWheels(_rightLeg);
            _hasWheels = _wheels.Count > 0;

            _initialized = true;
            _cycleTimer = 0f;

            // Scale animation amplitudes for larger models (bosses are 2-3x player size)
            // Measure approximate body height from part positions
            float maxY = 0f;
            foreach (var part in _allParts)
            {
                float y = part.Position.Y;
                if (y > maxY) maxY = y;
            }
            // Player models are ~1.5 units tall; scale up for bigger enemies
            _animScale = Mathf.Max(1f, maxY / 1.5f);

            int partCount = _allParts.Count;
            if (partCount == 0)
                GD.PrintErr($"[ProceduralAnimator] No animatable parts found in '{_bodyRoot.Name}' — animations will not play");
            else
            {
                string jointInfo = _hasArticulatedArms ? " +arms" : "";
                jointInfo += _hasBipedLegs ? " +legs" : "";
                jointInfo += _hasWheels ? " +wheels" : "";
                GD.Print($"[ProceduralAnimator] Initialized with {partCount} parts{jointInfo}");
            }
        }

        private void CollectWheels(Node3D legPivot)
        {
            if (legPivot == null) return;
            foreach (var child in legPivot.GetChildren())
            {
                if (child is Node3D node && node.Name.ToString().StartsWith("_Wheel"))
                    _wheels.Add(node);
            }
        }

        private Node3D FindPart(string name)
        {
            var direct = _bodyRoot?.GetNodeOrNull<Node3D>(name);
            if (direct != null) return direct;
            return FindPartRecursive(_bodyRoot, name);
        }

        private static Node3D FindPartRecursive(Node parent, string name)
        {
            if (parent == null) return null;
            foreach (var child in parent.GetChildren())
            {
                if (child is Node3D n3d && n3d.Name.ToString() == name)
                    return n3d;
                var found = FindPartRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private void StorePart(Node3D part)
        {
            if (part == null) return;
            _allParts.Add(part);
            _basePositions[part] = part.Position;
            _baseRotations[part] = part.RotationDegrees;
        }

        public void SetState(AnimState state)
        {
            if (!_initialized) return;
            if (state == _currentState) return;

            _currentState = state;
            _cycleTimer = 0f;

            _activeTween?.Kill();
            _activeTween = null;

            ResetToBaseline();

            switch (state)
            {
                case AnimState.Attack:
                    PlayAttack();
                    break;
                case AnimState.Hit:
                    PlayHit();
                    break;
                case AnimState.Death:
                    PlayDeath();
                    break;
                case AnimState.Stunned:
                    break;
            }
        }

        public override void _Process(double delta)
        {
            if (!_initialized || _bodyRoot == null) return;
            if (!GodotObject.IsInstanceValid(_bodyRoot)) return;

            float dt = (float)delta;
            _cycleTimer += dt;

            switch (_currentState)
            {
                case AnimState.Idle:
                    AnimateIdle();
                    break;
                case AnimState.Walk:
                    AnimateLocomotive(1.0f, 25f, 20f, 0.04f);
                    break;
                case AnimState.Run:
                    AnimateLocomotive(1.6f, 35f, 30f, 0.06f);
                    break;
                case AnimState.Stunned:
                    AnimateStunned();
                    break;
            }
        }

        // ── Idle ──

        private void AnimateIdle()
        {
            float t = _cycleTimer * 2f;
            float s = _animScale;

            // Gentle torso breathing bob
            if (_torso != null)
            {
                var basePos = _basePositions[_torso];
                _torso.Position = basePos + new Vector3(0, Mathf.Sin(t) * 0.03f * s, 0);
            }

            // Slight arm sway at shoulder
            AnimatePartRot(_leftArm, t * 0.8f, 3f * s, 0);
            AnimatePartRot(_rightArm, t * 0.8f + 0.5f, 3f * s, 0);

            // Sub-joints: gentle elbow flex in idle
            if (_hasArticulatedArms)
            {
                AnimatePartRot(_leftElbow, t * 0.6f, 2f * s, 0);
                AnimatePartRot(_rightElbow, t * 0.6f + 0.3f, 2f * s, 0);
                // Hands: very subtle wrist rotation
                AnimatePartRot(_leftHand, t * 0.4f, 1.5f * s, 0, zAmp: 1f * s);
                AnimatePartRot(_rightHand, t * 0.4f + 0.2f, 1.5f * s, 0, zAmp: 1f * s);
            }

            // Biped legs: subtle weight shift
            if (_hasBipedLegs)
            {
                AnimatePartRot(_leftKnee, t * 0.5f, 1.5f * s, 0);
                AnimatePartRot(_rightKnee, t * 0.5f + Mathf.Pi, 1.5f * s, 0);
            }

            // Head: slow look-around
            if (_head != null)
            {
                var baseRot = _baseRotations[_head];
                _head.RotationDegrees = baseRot + new Vector3(
                    Mathf.Sin(t * 0.3f) * 2f * s,
                    Mathf.Sin(t * 0.2f) * 3f * s,
                    0);
            }
        }

        // ── Locomotion ──

        private void AnimateLocomotive(float speedMult, float legAngle, float armAngle, float bobAmount)
        {
            float t = _cycleTimer * 6f * speedMult;
            legAngle *= _animScale;
            armAngle *= _animScale;
            bobAmount *= _animScale;

            if (_hasWheels)
            {
                AnimateWheelLocomotion(t, speedMult, armAngle, bobAmount);
            }
            else if (_hasBipedLegs)
            {
                AnimateBipedLocomotion(t, legAngle, armAngle, bobAmount);
            }
            else
            {
                AnimateSimpleLocomotion(t, legAngle, armAngle, bobAmount);
            }

            // Weapon follows right arm with dampened motion
            if (_weapon != null)
            {
                var baseRot = _baseRotations[_weapon];
                float weaponSway = _hasWheels ? 3f : armAngle * 0.5f;
                _weapon.RotationDegrees = baseRot + new Vector3(Mathf.Sin(t) * weaponSway, 0, 0);
            }

            // Torso bob
            if (_torso != null)
            {
                var basePos = _basePositions[_torso];
                _torso.Position = basePos + new Vector3(0, Mathf.Abs(Mathf.Sin(t * 2f)) * bobAmount, 0);
                // Slight torso sway on walk
                var baseRot = _baseRotations[_torso];
                _torso.RotationDegrees = baseRot + new Vector3(0, Mathf.Sin(t) * 1.5f, Mathf.Sin(t * 2f) * 1f);
            }

            // Head stays relatively steady (counter-bob)
            if (_head != null)
            {
                var basePos = _basePositions[_head];
                _head.Position = basePos + new Vector3(0, Mathf.Abs(Mathf.Sin(t * 2f)) * bobAmount * 0.3f, 0);
            }
        }

        private void AnimateWheelLocomotion(float t, float speedMult, float armAngle, float bobAmount)
        {
            // Spin wheels
            float wheelSpeed = 360f * speedMult;
            foreach (var wheel in _wheels)
            {
                if (wheel == null || !GodotObject.IsInstanceValid(wheel)) continue;
                wheel.RotateX(Mathf.DegToRad(wheelSpeed * (float)GetProcessDeltaTime()));
            }

            // Subtle suspension bounce
            AnimatePartRot(_leftLeg, t * 2f, 2.5f, 0);
            AnimatePartRot(_rightLeg, t * 2f + 1f, 2.5f, 0);

            // Arms: shoulder sway
            float armSway = _hasArticulatedArms ? armAngle * 0.3f : 5f;
            AnimatePartRot(_leftArm, t * 0.8f, armSway, 0);
            AnimatePartRot(_rightArm, t * 0.8f + 0.5f, armSway, 0);

            // Articulated arm secondary motion
            if (_hasArticulatedArms)
            {
                AnimatePartRot(_leftElbow, t * 0.8f + 0.3f, armSway * 0.6f, 0);
                AnimatePartRot(_rightElbow, t * 0.8f + 0.8f, armSway * 0.6f, 0);
                AnimatePartRot(_leftHand, t * 0.8f + 0.5f, armSway * 0.3f, 0, zAmp: 1.5f);
                AnimatePartRot(_rightHand, t * 0.8f + 1f, armSway * 0.3f, 0, zAmp: 1.5f);
            }
        }

        private void AnimateBipedLocomotion(float t, float legAngle, float armAngle, float bobAmount)
        {
            // ── Articulated bipedal walk ──

            // Hip swing (top-level leg pivots)
            AnimatePartRot(_leftLeg, t, legAngle * 0.6f, 0);
            AnimatePartRot(_rightLeg, t + Mathf.Pi, legAngle * 0.6f, 0);

            // Knee flex — bends forward when leg swings back (phase offset)
            // Knees bend more at mid-stride for a natural gait
            float kneeAngle = legAngle * 0.8f;
            if (_leftKnee != null)
            {
                var baseRot = _baseRotations[_leftKnee];
                // Knee only bends forward (positive X), using abs+sin to keep it one-directional
                float kFlex = Mathf.Max(0, Mathf.Sin(t + 0.8f)) * kneeAngle;
                _leftKnee.RotationDegrees = baseRot + new Vector3(kFlex, 0, 0);
            }
            if (_rightKnee != null)
            {
                var baseRot = _baseRotations[_rightKnee];
                float kFlex = Mathf.Max(0, Mathf.Sin(t + Mathf.Pi + 0.8f)) * kneeAngle;
                _rightKnee.RotationDegrees = baseRot + new Vector3(kFlex, 0, 0);
            }

            // Ankle — counter-rotates to keep foot flat
            if (_leftAnkle != null)
            {
                var baseRot = _baseRotations[_leftAnkle];
                float aFlex = -Mathf.Sin(t) * legAngle * 0.3f;
                _leftAnkle.RotationDegrees = baseRot + new Vector3(aFlex, 0, 0);
            }
            if (_rightAnkle != null)
            {
                var baseRot = _baseRotations[_rightAnkle];
                float aFlex = -Mathf.Sin(t + Mathf.Pi) * legAngle * 0.3f;
                _rightAnkle.RotationDegrees = baseRot + new Vector3(aFlex, 0, 0);
            }

            // Opposing arm swing at shoulder
            float shoulderSwing = _hasArticulatedArms ? armAngle * 0.7f : armAngle;
            AnimatePartRot(_leftArm, t + Mathf.Pi, shoulderSwing, 0);
            AnimatePartRot(_rightArm, t, shoulderSwing, 0);

            // Elbow flex during arm swing — bends when arm swings back
            if (_hasArticulatedArms)
            {
                float elbowAngle = armAngle * 0.5f;
                if (_leftElbow != null)
                {
                    var baseRot = _baseRotations[_leftElbow];
                    float eFlex = Mathf.Max(0, -Mathf.Sin(t + Mathf.Pi)) * elbowAngle;
                    _leftElbow.RotationDegrees = baseRot + new Vector3(-eFlex, 0, 0);
                }
                if (_rightElbow != null)
                {
                    var baseRot = _baseRotations[_rightElbow];
                    float eFlex = Mathf.Max(0, -Mathf.Sin(t)) * elbowAngle;
                    _rightElbow.RotationDegrees = baseRot + new Vector3(-eFlex, 0, 0);
                }

                // Hands: subtle wrist flex
                AnimatePartRot(_leftHand, t + Mathf.Pi + 0.5f, armAngle * 0.15f, 0, zAmp: 2f);
                AnimatePartRot(_rightHand, t + 0.5f, armAngle * 0.15f, 0, zAmp: 2f);
            }
        }

        private void AnimateSimpleLocomotion(float t, float legAngle, float armAngle, float bobAmount)
        {
            // Simple single-pivot leg swing (enemies, non-articulated)
            AnimatePartRot(_leftLeg, t, legAngle, 0);
            AnimatePartRot(_rightLeg, t + Mathf.Pi, legAngle, 0);

            // Opposing arm swing
            float shoulderSwing = _hasArticulatedArms ? armAngle * 0.7f : armAngle;
            AnimatePartRot(_leftArm, t + Mathf.Pi, shoulderSwing, 0);
            AnimatePartRot(_rightArm, t, shoulderSwing, 0);

            if (_hasArticulatedArms)
            {
                float elbowAngle = armAngle * 0.5f;
                if (_leftElbow != null)
                {
                    var baseRot = _baseRotations[_leftElbow];
                    float eFlex = Mathf.Max(0, -Mathf.Sin(t + Mathf.Pi)) * elbowAngle;
                    _leftElbow.RotationDegrees = baseRot + new Vector3(-eFlex, 0, 0);
                }
                if (_rightElbow != null)
                {
                    var baseRot = _baseRotations[_rightElbow];
                    float eFlex = Mathf.Max(0, -Mathf.Sin(t)) * elbowAngle;
                    _rightElbow.RotationDegrees = baseRot + new Vector3(-eFlex, 0, 0);
                }
                AnimatePartRot(_leftHand, t + Mathf.Pi + 0.5f, armAngle * 0.15f, 0, zAmp: 2f);
                AnimatePartRot(_rightHand, t + 0.5f, armAngle * 0.15f, 0, zAmp: 2f);
            }
        }

        // ── Attack ──

        private void PlayAttack()
        {
            _activeTween = CreateTween();
            _activeTween.SetParallel(true);
            float s = _animScale;
            float swingDur = 0.1f + (_animScale > 1.5f ? 0.1f : 0f); // Bosses get slower, weightier swings
            float returnDur = 0.15f + (_animScale > 1.5f ? 0.1f : 0f);

            // Right arm swings forward at shoulder
            TweenPartRotX(_rightArm, -45f * s, swingDur);

            // Elbow snaps straight on attack
            if (_rightElbow != null)
                TweenPartRotX(_rightElbow, 15f * s, swingDur);

            // Hand flicks
            if (_rightHand != null)
                TweenPartRotX(_rightHand, -20f * s, swingDur * 0.8f);

            // Left arm braces (slight pull back)
            TweenPartRotX(_leftArm, 10f * s, swingDur);
            if (_leftElbow != null)
                TweenPartRotX(_leftElbow, -15f * s, swingDur);

            // Weapon follows
            TweenPartRotX(_weapon, -45f * s, swingDur);

            // Torso leans forward
            TweenPartRotX(_torso, -10f * s, swingDur);

            // Snap back phase
            _activeTween.SetParallel(false);
            _activeTween.TweenInterval(swingDur);
            _activeTween.SetParallel(true);

            TweenPartToBaseRotX(_rightArm, returnDur);
            TweenPartToBaseRotX(_rightElbow, returnDur);
            TweenPartToBaseRotX(_rightHand, returnDur);
            TweenPartToBaseRotX(_leftArm, returnDur);
            TweenPartToBaseRotX(_leftElbow, returnDur);
            TweenPartToBaseRotX(_weapon, returnDur);
            TweenPartToBaseRotX(_torso, returnDur);

            _activeTween.SetParallel(false);
            _activeTween.TweenCallback(Callable.From(() =>
            {
                if (_currentState == AnimState.Attack)
                    SetState(AnimState.Idle);
            }));
        }

        // ── Hit ──

        private void PlayHit()
        {
            _activeTween = CreateTween();
            _activeTween.SetParallel(true);
            float hitAngle = 15f * _animScale;

            // All parts jolt backward
            foreach (var part in _allParts)
            {
                if (part == null || !GodotObject.IsInstanceValid(part)) continue;
                var baseRot = _baseRotations[part];
                _activeTween.TweenProperty(part, "rotation_degrees:x", baseRot.X + hitAngle, 0.05f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            }

            _activeTween.SetParallel(false);
            _activeTween.TweenInterval(0.05f);
            _activeTween.SetParallel(true);

            foreach (var part in _allParts)
            {
                if (part == null || !GodotObject.IsInstanceValid(part)) continue;
                var baseRot = _baseRotations[part];
                _activeTween.TweenProperty(part, "rotation_degrees:x", baseRot.X, 0.1f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            }

            _activeTween.SetParallel(false);
            _activeTween.TweenCallback(Callable.From(() =>
            {
                if (_currentState == AnimState.Hit)
                    SetState(AnimState.Idle);
            }));
        }

        // ── Death ──

        private void PlayDeath()
        {
            _activeTween = CreateTween();
            _activeTween.SetParallel(true);

            if (_hasWheels)
            {
                // Track-based death: eyes droop, torso tips, tracks splay
                TweenPartToRotX(_head, 35f, 0.5f);
                TweenPartToRotX(_torso, -25f, 0.5f);

                if (_leftArm != null)
                {
                    TweenPartToRotX(_leftArm, 40f, 0.4f);
                    TweenPartToRotZ(_leftArm, 20f, 0.4f);
                }
                if (_rightArm != null)
                {
                    TweenPartToRotX(_rightArm, 40f, 0.4f);
                    TweenPartToRotZ(_rightArm, -20f, 0.4f);
                }

                // Elbows go limp
                TweenPartToRotX(_leftElbow, 30f, 0.35f);
                TweenPartToRotX(_rightElbow, 30f, 0.35f);

                TweenPartToRotZ(_leftLeg, 25f, 0.4f);
                TweenPartToRotZ(_rightLeg, -25f, 0.4f);
            }
            else if (_hasBipedLegs)
            {
                // Articulated bipedal death: knees buckle, collapse
                TweenPartToRotX(_head, 45f, 0.5f);
                TweenPartToRotX(_torso, -30f, 0.5f);

                // Arms go limp
                if (_leftArm != null)
                {
                    TweenPartToRotX(_leftArm, 50f, 0.4f);
                    TweenPartToRotZ(_leftArm, 15f, 0.4f);
                }
                if (_rightArm != null)
                {
                    TweenPartToRotX(_rightArm, 50f, 0.4f);
                    TweenPartToRotZ(_rightArm, -15f, 0.4f);
                }
                TweenPartToRotX(_leftElbow, 45f, 0.35f);
                TweenPartToRotX(_rightElbow, 45f, 0.35f);
                TweenPartToRotX(_leftHand, 20f, 0.3f);
                TweenPartToRotX(_rightHand, 20f, 0.3f);

                // Knees buckle forward
                TweenPartToRotX(_leftKnee, 60f, 0.4f);
                TweenPartToRotX(_rightKnee, 60f, 0.4f);
                // Ankles fold
                TweenPartToRotX(_leftAnkle, -30f, 0.35f);
                TweenPartToRotX(_rightAnkle, -30f, 0.35f);

                // Legs splay
                TweenPartToRotX(_leftLeg, 40f, 0.4f);
                TweenPartToRotX(_rightLeg, 40f, 0.4f);
            }
            else
            {
                // Generic humanoid death
                TweenPartToRotX(_head, 45f, 0.5f);
                TweenPartToRotX(_torso, -30f, 0.5f);

                if (_leftArm != null)
                {
                    TweenPartToRotX(_leftArm, 60f, 0.4f);
                    TweenPartToRotZ(_leftArm, 15f, 0.4f);
                }
                if (_rightArm != null)
                {
                    TweenPartToRotX(_rightArm, 60f, 0.4f);
                    TweenPartToRotZ(_rightArm, -15f, 0.4f);
                }
                TweenPartToRotX(_leftElbow, 35f, 0.35f);
                TweenPartToRotX(_rightElbow, 35f, 0.35f);

                TweenPartToRotX(_leftLeg, 40f, 0.4f);
                TweenPartToRotX(_rightLeg, 40f, 0.4f);
            }

            // Whole body drops
            if (_bodyRoot != null)
            {
                _activeTween.TweenProperty(_bodyRoot, "position:y",
                    _bodyRoot.Position.Y - 0.5f, 0.5f)
                    .SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
            }
        }

        // ── Stunned ──

        private void AnimateStunned()
        {
            foreach (var part in _allParts)
            {
                if (part == null || !GodotObject.IsInstanceValid(part)) continue;
                var baseRot = _baseRotations[part];
                float jitterX = (float)GD.RandRange(-5.0, 5.0);
                float jitterZ = (float)GD.RandRange(-3.0, 3.0);
                part.RotationDegrees = baseRot + new Vector3(jitterX, 0, jitterZ);
            }
        }

        // ── Helpers ──

        private void AnimatePartRot(Node3D part, float phase, float xAmp, float yAmp, float zAmp = 0f)
        {
            if (part == null) return;
            var baseRot = _baseRotations[part];
            part.RotationDegrees = baseRot + new Vector3(
                Mathf.Sin(phase) * xAmp,
                Mathf.Sin(phase) * yAmp,
                Mathf.Sin(phase * 1.3f) * zAmp);
        }

        private void TweenPartRotX(Node3D part, float offsetDeg, float duration)
        {
            if (part == null || _activeTween == null) return;
            var baseRot = _baseRotations[part];
            _activeTween.TweenProperty(part, "rotation_degrees:x", baseRot.X + offsetDeg, duration)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        }

        private void TweenPartToBaseRotX(Node3D part, float duration)
        {
            if (part == null || _activeTween == null) return;
            var baseRot = _baseRotations[part];
            _activeTween.TweenProperty(part, "rotation_degrees:x", baseRot.X, duration)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        }

        private void TweenPartToRotX(Node3D part, float targetDeg, float duration)
        {
            if (part == null || _activeTween == null) return;
            _activeTween.TweenProperty(part, "rotation_degrees:x", targetDeg, duration)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        }

        private void TweenPartToRotZ(Node3D part, float targetDeg, float duration)
        {
            if (part == null || _activeTween == null) return;
            _activeTween.TweenProperty(part, "rotation_degrees:z", targetDeg, duration)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        }

        public void ResetToBaseline()
        {
            foreach (var part in _allParts)
            {
                if (part == null || !GodotObject.IsInstanceValid(part)) continue;
                if (_basePositions.TryGetValue(part, out var pos))
                    part.Position = pos;
                if (_baseRotations.TryGetValue(part, out var rot))
                    part.RotationDegrees = rot;
            }
        }
    }
}
