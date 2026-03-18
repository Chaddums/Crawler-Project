using Godot;
using System.Collections.Generic;

namespace JunkyardTD
{
    public enum AnimState
    {
        Idle,
        Walk,
        Run,
        Attack,
        Hit,
        Death,
        Stunned,
        Custom
    }

    /// <summary>
    /// Wraps AnimationPlayer for character/enemy models.
    /// Maps AnimState enum to animation names with crossfade.
    /// Handles missing animations gracefully.
    /// </summary>
    public partial class CharacterAnimator : Node
    {
        private AnimationPlayer _animPlayer;
        private AnimState _currentState = AnimState.Idle;
        private HashSet<string> _availableAnims = new();
        private readonly Dictionary<AnimState, string> _dynamicOverrides = new();
        private float _crossfadeDuration = 0.15f;

        // State → animation name candidates (tried in order)
        // Includes Quaternius robot names (Punch, Jump, Dance, etc.)
        // "ArmatureAction" covers generic FBX exports (e.g. LilRobot)
        private static readonly Dictionary<AnimState, string[]> _stateAnimMap = new()
        {
            { AnimState.Idle,    new[] { "Idle", "idle", "IDLE", "Idle_A", "idle_a", "Standing", "standing" } },
            { AnimState.Walk,    new[] { "Walk", "walk", "WALK", "Walking", "walking", "Walk_A", "Run", "run", "ArmatureAction", "Action" } },
            { AnimState.Run,     new[] { "Run", "run", "RUN", "Running", "running", "Run_A", "Walk", "walk", "ArmatureAction", "Action" } },
            { AnimState.Attack,  new[] { "Attack", "Attack_R", "Attack_L", "attack", "ATTACK", "Attack_A", "Punch", "punch", "Slash", "slash", "1H_Melee_Attack_Slice_Diagonal" } },
            { AnimState.Hit,     new[] { "Hit", "hit", "HIT", "Hurt", "hurt", "Hit_A", "Take_Damage", "No", "no" } },
            { AnimState.Death,   new[] { "Death", "death", "DEATH", "Die", "die", "Death_A", "Death_A_Pose" } },
            { AnimState.Stunned, new[] { "Stunned", "stunned", "Stun", "stun", "Dazed", "Hit", "Sitting", "sitting" } },
        };

        public AnimState CurrentState => _currentState;
        public bool IsInitialized => _animPlayer != null;

        /// <summary>
        /// Find and bind to an AnimationPlayer in the model hierarchy.
        /// </summary>
        public void Initialize(Node3D modelRoot)
        {
            _animPlayer = FindAnimationPlayer(modelRoot);
            if (_animPlayer == null)
            {
                GD.PrintErr($"[CharacterAnimator] No AnimationPlayer found in '{modelRoot.Name}' hierarchy");
                return;
            }

            _availableAnims.Clear();
            foreach (var animName in _animPlayer.GetAnimationList())
                _availableAnims.Add(animName);

            if (_availableAnims.Count == 0)
            {
                GD.PrintErr($"[CharacterAnimator] AnimationPlayer found in '{modelRoot.Name}' but has 0 animations");
                return;
            }

            GD.Print($"[CharacterAnimator] Initialized with {_availableAnims.Count} anims: {string.Join(", ", _availableAnims)}");

            // Auto-detect animation name patterns and build fallback mappings
            BuildDynamicMappings();

            // Ensure looping animations are set to loop (FBX imports default to non-looping)
            SetLoopingAnimations();

            // Start with idle
            Play(AnimState.Idle);
        }

        /// <summary>
        /// If the FBX has animation names that don't match our candidates,
        /// try to auto-map them by checking for partial/substring matches.
        /// e.g. "Armature|Idle" or "mixamo.com|Walk" should still match.
        /// </summary>
        private void BuildDynamicMappings()
        {
            foreach (var (state, candidates) in _stateAnimMap)
            {
                // Check if any candidate already matches
                bool hasMatch = false;
                foreach (var c in candidates)
                {
                    if (_availableAnims.Contains(c)) { hasMatch = true; break; }
                }
                if (hasMatch) continue;

                // Try partial matching: check if any available anim CONTAINS a candidate name
                foreach (var animName in _availableAnims)
                {
                    string lower = animName.ToLower();
                    foreach (var candidate in candidates)
                    {
                        if (lower.Contains(candidate.ToLower()))
                        {
                            // Add the actual FBX animation name as a candidate
                            // by putting it in _availableAnims with the exact casing
                            // We add it to a dynamic override list
                            _dynamicOverrides[state] = animName;
                            GD.Print($"[CharacterAnimator] Auto-mapped {state} -> '{animName}' (partial match for '{candidate}')");
                            goto nextState;
                        }
                    }
                }
                nextState:;
            }
        }

        /// <summary>
        /// FBX animations import as non-looping by default. Force loop mode on
        /// animations that should loop (idle, walk, run, stunned).
        /// </summary>
        private void SetLoopingAnimations()
        {
            // States that should loop
            var loopingStates = new[] { AnimState.Idle, AnimState.Walk, AnimState.Run, AnimState.Stunned };

            foreach (var state in loopingStates)
            {
                string animName = ResolveAnimName(state);
                if (animName == null) continue;

                var anim = _animPlayer.GetAnimation(animName);
                if (anim != null && anim.LoopMode == Animation.LoopModeEnum.None)
                {
                    anim.LoopMode = Animation.LoopModeEnum.Linear;
                }
            }
        }

        /// <summary>
        /// Resolve the actual animation name for a state (checking overrides then candidates).
        /// </summary>
        private string ResolveAnimName(AnimState state)
        {
            if (_dynamicOverrides.TryGetValue(state, out var overrideName))
                return overrideName;

            if (_stateAnimMap.TryGetValue(state, out var candidates))
            {
                foreach (var c in candidates)
                {
                    if (_availableAnims.Contains(c))
                        return c;
                }
            }
            return null;
        }

        /// <summary>
        /// Set the animation state. Only changes if different from current.
        /// </summary>
        public void SetState(AnimState state)
        {
            if (state == _currentState) return;
            _currentState = state;
            Play(state);
        }

        /// <summary>
        /// Play animation for a given state.
        /// </summary>
        public void Play(AnimState state)
        {
            if (_animPlayer == null) return;

            // Refresh in case clips were added after init (e.g. split animations)
            foreach (var name in _animPlayer.GetAnimationList())
                _availableAnims.Add(name);

            // Check dynamic overrides first (auto-mapped from FBX names)
            if (_dynamicOverrides.TryGetValue(state, out var overrideName))
            {
                if (_animPlayer.CurrentAnimation != overrideName)
                    _animPlayer.Play(overrideName, _crossfadeDuration);
                return;
            }

            if (!_stateAnimMap.TryGetValue(state, out var candidates))
                return;

            foreach (var animName in candidates)
            {
                if (_availableAnims.Contains(animName))
                {
                    if (_animPlayer.CurrentAnimation != animName)
                        _animPlayer.Play(animName, _crossfadeDuration);
                    return;
                }
            }

            // Fallback: if requested state not found and not already idle, try idle
            if (state != AnimState.Idle)
                Play(AnimState.Idle);
        }

        /// <summary>
        /// Set the playback speed multiplier for the current animation.
        /// </summary>
        public void SetSpeed(float speed)
        {
            if (_animPlayer != null)
                _animPlayer.SpeedScale = speed;
        }

        /// <summary>
        /// Get current playback position (0 to animation length).
        /// </summary>
        public float GetPlaybackPosition()
        {
            return _animPlayer != null ? (float)_animPlayer.CurrentAnimationPosition : 0f;
        }

        /// <summary>
        /// Get current animation's total length.
        /// </summary>
        public float GetAnimationLength()
        {
            if (_animPlayer == null) return 0f;
            var anim = _animPlayer.GetAnimation(_animPlayer.CurrentAnimation);
            return anim != null ? (float)anim.Length : 0f;
        }

        /// <summary>
        /// Play a custom animation by exact name.
        /// </summary>
        public void PlayCustom(string animationName)
        {
            if (_animPlayer == null) return;

            // Refresh available anims in case clips were added after init
            if (!_availableAnims.Contains(animationName))
            {
                foreach (var name in _animPlayer.GetAnimationList())
                    _availableAnims.Add(name);
            }

            if (!_availableAnims.Contains(animationName))
            {
                GD.PrintErr($"[CharacterAnimator] Animation '{animationName}' not found");
                return;
            }
            // Don't restart if already playing this exact animation
            if (_animPlayer.CurrentAnimation == animationName) return;
            _currentState = AnimState.Custom;
            _animPlayer.Play(animationName, _crossfadeDuration);
        }

        private static AnimationPlayer FindAnimationPlayer(Node root)
        {
            if (root is AnimationPlayer ap) return ap;

            foreach (var child in root.GetChildren())
            {
                if (child is AnimationPlayer found) return found;
                if (child is Node node)
                {
                    var result = FindAnimationPlayer(node);
                    if (result != null) return result;
                }
            }
            return null;
        }
    }
}
