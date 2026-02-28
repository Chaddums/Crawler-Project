using Godot;
using System.Collections.Generic;

namespace JunkbotArena
{
    /// <summary>
    /// Tween-based limb animation for procedural character bodies.
    /// Finds named body part nodes (Head, Torso, LeftArm, RightArm, LeftLeg, RightLeg, Weapon)
    /// and animates them based on AnimState.
    /// </summary>
    public partial class ProceduralAnimator : Node, IAnimatable
    {
        private Node3D _bodyRoot;
        private AnimState _currentState = AnimState.Idle;
        private float _cycleTimer;
        private Tween _activeTween;
        private bool _initialized;

        // Body part pivots (Node3D wrappers at joint positions)
        private Node3D _head;
        private Node3D _torso;
        private Node3D _leftArm;
        private Node3D _rightArm;
        private Node3D _leftLeg;
        private Node3D _rightLeg;
        private Node3D _weapon;

        // Original transforms for baselines
        private readonly Dictionary<Node3D, Vector3> _basePositions = new();
        private readonly Dictionary<Node3D, Vector3> _baseRotations = new();

        // All discovered parts for batch operations
        private readonly List<Node3D> _allParts = new();

        public AnimState CurrentState => _currentState;

        public void Initialize(Node3D bodyRoot)
        {
            _bodyRoot = bodyRoot;
            if (_bodyRoot == null) return;

            // Find named parts
            _head = FindPart("Head");
            _torso = FindPart("Torso");
            _leftArm = FindPart("LeftArm");
            _rightArm = FindPart("RightArm");
            _leftLeg = FindPart("LeftLeg");
            _rightLeg = FindPart("RightLeg");
            _weapon = FindPart("Weapon");

            // Also check enemy-specific parts
            if (_head == null) _head = FindPart("Body");
            if (_leftArm == null) _leftArm = FindPart("Crossbar");
            if (_rightLeg == null) _rightLeg = FindPart("Tail");

            // Store baselines
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

            _initialized = true;
            _cycleTimer = 0f;
        }

        private Node3D FindPart(string name)
        {
            return _bodyRoot?.GetNodeOrNull<Node3D>(name);
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

            // Kill any running tween
            _activeTween?.Kill();
            _activeTween = null;

            // Reset all parts to baseline before starting new state
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
                    // Stunned uses _Process jitter
                    break;
                // Idle, Walk, Run handled in _Process
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

        private void AnimateIdle()
        {
            float t = _cycleTimer * 2f;

            // Gentle torso breathing bob
            if (_torso != null)
            {
                var basePos = _basePositions[_torso];
                _torso.Position = basePos + new Vector3(0, Mathf.Sin(t) * 0.03f, 0);
            }

            // Slight arm sway
            if (_leftArm != null)
            {
                var baseRot = _baseRotations[_leftArm];
                _leftArm.RotationDegrees = baseRot + new Vector3(Mathf.Sin(t * 0.8f) * 3f, 0, 0);
            }
            if (_rightArm != null)
            {
                var baseRot = _baseRotations[_rightArm];
                _rightArm.RotationDegrees = baseRot + new Vector3(Mathf.Sin(t * 0.8f + 0.5f) * 3f, 0, 0);
            }
        }

        private void AnimateLocomotive(float speedMult, float legAngle, float armAngle, float bobAmount)
        {
            float t = _cycleTimer * 6f * speedMult;

            // Alternating leg swing
            if (_leftLeg != null)
            {
                var baseRot = _baseRotations[_leftLeg];
                _leftLeg.RotationDegrees = baseRot + new Vector3(Mathf.Sin(t) * legAngle, 0, 0);
            }
            if (_rightLeg != null)
            {
                var baseRot = _baseRotations[_rightLeg];
                _rightLeg.RotationDegrees = baseRot + new Vector3(Mathf.Sin(t + Mathf.Pi) * legAngle, 0, 0);
            }

            // Opposing arm swing
            if (_leftArm != null)
            {
                var baseRot = _baseRotations[_leftArm];
                _leftArm.RotationDegrees = baseRot + new Vector3(Mathf.Sin(t + Mathf.Pi) * armAngle, 0, 0);
            }
            if (_rightArm != null)
            {
                var baseRot = _baseRotations[_rightArm];
                _rightArm.RotationDegrees = baseRot + new Vector3(Mathf.Sin(t) * armAngle, 0, 0);
            }

            // Weapon follows right arm
            if (_weapon != null)
            {
                var baseRot = _baseRotations[_weapon];
                _weapon.RotationDegrees = baseRot + new Vector3(Mathf.Sin(t) * armAngle * 0.5f, 0, 0);
            }

            // Torso bob
            if (_torso != null)
            {
                var basePos = _basePositions[_torso];
                _torso.Position = basePos + new Vector3(0, Mathf.Abs(Mathf.Sin(t * 2f)) * bobAmount, 0);
            }

            // Head stays relatively steady
            if (_head != null)
            {
                var basePos = _basePositions[_head];
                _head.Position = basePos + new Vector3(0, Mathf.Abs(Mathf.Sin(t * 2f)) * bobAmount * 0.3f, 0);
            }
        }

        private void PlayAttack()
        {
            _activeTween = CreateTween();
            _activeTween.SetParallel(true);

            // Right arm swings forward
            if (_rightArm != null)
            {
                var baseRot = _baseRotations[_rightArm];
                _activeTween.TweenProperty(_rightArm, "rotation_degrees:x", baseRot.X - 60f, 0.1f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            }

            // Weapon follows
            if (_weapon != null)
            {
                var baseRot = _baseRotations[_weapon];
                _activeTween.TweenProperty(_weapon, "rotation_degrees:x", baseRot.X - 45f, 0.1f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            }

            // Torso leans forward slightly
            if (_torso != null)
            {
                var baseRot = _baseRotations[_torso];
                _activeTween.TweenProperty(_torso, "rotation_degrees:x", baseRot.X - 10f, 0.1f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            }

            // Snap back phase
            _activeTween.SetParallel(false);
            _activeTween.TweenInterval(0.1f);
            _activeTween.SetParallel(true);

            if (_rightArm != null)
            {
                var baseRot = _baseRotations[_rightArm];
                _activeTween.TweenProperty(_rightArm, "rotation_degrees:x", baseRot.X, 0.15f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            }
            if (_weapon != null)
            {
                var baseRot = _baseRotations[_weapon];
                _activeTween.TweenProperty(_weapon, "rotation_degrees:x", baseRot.X, 0.15f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            }
            if (_torso != null)
            {
                var baseRot = _baseRotations[_torso];
                _activeTween.TweenProperty(_torso, "rotation_degrees:x", baseRot.X, 0.15f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            }

            // After attack finishes, return to idle
            _activeTween.SetParallel(false);
            _activeTween.TweenCallback(Callable.From(() =>
            {
                if (_currentState == AnimState.Attack)
                    SetState(AnimState.Idle);
            }));
        }

        private void PlayHit()
        {
            _activeTween = CreateTween();
            _activeTween.SetParallel(true);

            // All parts jolt backward
            foreach (var part in _allParts)
            {
                if (part == null || !GodotObject.IsInstanceValid(part)) continue;
                var baseRot = _baseRotations[part];
                _activeTween.TweenProperty(part, "rotation_degrees:x", baseRot.X + 15f, 0.05f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            }

            // Snap back
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

            // After hit anim, return to previous looping state
            _activeTween.SetParallel(false);
            _activeTween.TweenCallback(Callable.From(() =>
            {
                if (_currentState == AnimState.Hit)
                    SetState(AnimState.Idle);
            }));
        }

        private void PlayDeath()
        {
            _activeTween = CreateTween();
            _activeTween.SetParallel(true);

            // Head drops
            if (_head != null)
            {
                _activeTween.TweenProperty(_head, "rotation_degrees:x", 45f, 0.5f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            }

            // Torso tilts forward
            if (_torso != null)
            {
                _activeTween.TweenProperty(_torso, "rotation_degrees:x", -30f, 0.5f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            }

            // Arms go limp
            if (_leftArm != null)
            {
                _activeTween.TweenProperty(_leftArm, "rotation_degrees:x", 60f, 0.4f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
                _activeTween.TweenProperty(_leftArm, "rotation_degrees:z", 15f, 0.4f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            }
            if (_rightArm != null)
            {
                _activeTween.TweenProperty(_rightArm, "rotation_degrees:x", 60f, 0.4f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
                _activeTween.TweenProperty(_rightArm, "rotation_degrees:z", -15f, 0.4f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            }

            // Legs collapse
            if (_leftLeg != null)
            {
                _activeTween.TweenProperty(_leftLeg, "rotation_degrees:x", 40f, 0.4f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            }
            if (_rightLeg != null)
            {
                _activeTween.TweenProperty(_rightLeg, "rotation_degrees:x", 40f, 0.4f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            }

            // Whole body drops to ground
            if (_bodyRoot != null)
            {
                _activeTween.TweenProperty(_bodyRoot, "position:y",
                    _bodyRoot.Position.Y - 0.5f, 0.5f)
                    .SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
            }
        }

        private void AnimateStunned()
        {
            // Random small jitter on all limbs
            foreach (var part in _allParts)
            {
                if (part == null || !GodotObject.IsInstanceValid(part)) continue;
                var baseRot = _baseRotations[part];
                float jitterX = (float)GD.RandRange(-5.0, 5.0);
                float jitterZ = (float)GD.RandRange(-3.0, 3.0);
                part.RotationDegrees = baseRot + new Vector3(jitterX, 0, jitterZ);
            }
        }

        private void ResetToBaseline()
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
