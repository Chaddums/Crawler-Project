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
            { AnimState.Attack,  new[] { "Shoot", "shoot", "Charge", "charge", "Attack", "Attack_R", "Attack_L", "attack", "ATTACK", "Attack_A", "Punch", "punch", "Slash", "slash", "1H_Melee_Attack_Slice_Diagonal" } },
            { AnimState.Hit,     new[] { "Hit", "hit", "HIT", "Hurt", "hurt", "Hit_A", "Take_Damage", "No", "no" } },
            { AnimState.Death,   new[] { "Death", "death", "DEATH", "Die", "die", "Death_A", "Death_A_Pose" } },
            { AnimState.Stunned, new[] { "Stunned", "stunned", "Stun", "stun", "Dazed", "Hit", "Sitting", "sitting" } },
        };

        public AnimState CurrentState => _currentState;
        public bool IsInitialized => _animPlayer != null;
        public AnimationPlayer AnimPlayer => _animPlayer;

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
            _dynamicOverrides.Clear();

            // If the model only has one animation, use it for all states
            if (_availableAnims.Count == 1)
            {
                string onlyAnim = null;
                foreach (var a in _availableAnims) { onlyAnim = a; break; }
                foreach (var state in _stateAnimMap.Keys)
                {
                    _dynamicOverrides[state] = onlyAnim;
                }
                GD.Print($"[CharacterAnimator] Single-anim model: mapped all states -> '{onlyAnim}'");
                return;
            }

            foreach (var (state, candidates) in _stateAnimMap)
            {
                // Check if any candidate already matches
                bool hasMatch = false;
                foreach (var c in candidates)
                {
                    if (_availableAnims.Contains(c)) { hasMatch = true; break; }
                }
                if (hasMatch) continue;

                // Try partial matching: iterate candidates in ORDER so earlier
                // (more specific) matches win — e.g. "Walk" before "Run" for Walk state
                foreach (var candidate in candidates)
                {
                    string candLower = candidate.ToLower();
                    foreach (var animName in _availableAnims)
                    {
                        if (animName.ToLower().Contains(candLower))
                        {
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

        /// <summary>
        /// Play a custom animation and keep it looping continuously.
        /// Unlike PlayCustom, this forces LoopMode.Linear on the clip and
        /// seamlessly restarts it (no crossfade) if it somehow finishes.
        /// Use for animations that must never visibly stop (e.g. naruto run).
        /// </summary>
        public void PlayCustomLooping(string animationName)
        {
            if (_animPlayer == null) return;

            // Refresh available anims
            if (!_availableAnims.Contains(animationName))
            {
                foreach (var name in _animPlayer.GetAnimationList())
                    _availableAnims.Add(name);
            }
            if (!_availableAnims.Contains(animationName)) return;

            // Already playing — nothing to do
            if (_animPlayer.CurrentAnimation == animationName && _animPlayer.IsPlaying())
                return;

            // Force loop mode on the clip in case it wasn't set or got lost
            var anim = _animPlayer.GetAnimation(animationName);
            if (anim != null && anim.LoopMode != Animation.LoopModeEnum.Linear)
                anim.LoopMode = Animation.LoopModeEnum.Linear;

            _currentState = AnimState.Custom;
            // No crossfade — instant restart to avoid visible blend to default pose
            _animPlayer.Play(animationName);
        }

        /// <summary>Public wrapper for external callers that need to check if a model has animations.</summary>
        public static AnimationPlayer FindAnimationPlayerPublic(Node root) => FindAnimationPlayer(root);

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

        /// <summary>
        /// Split a monolithic FBX animation (e.g. "ArmatureAction") into named segments
        /// using gap detection on keyframe timings. Works for any character model.
        /// </summary>
        /// <param name="modelRoot">The loaded model root node.</param>
        /// <param name="segmentNames">Ordered names for detected segments. Defaults to Idle, Walk, Attack, Hit, Death.</param>
        /// <param name="stripScale">If true, removes Scale3D tracks to prevent size flickering.</param>
        /// <returns>True if splitting occurred, false if no monolithic animation was found.</returns>
        public static bool SplitMonolithicAnimation(Node3D modelRoot, string[] segmentNames = null, bool stripScale = true)
        {
            segmentNames ??= new[] { "Idle", "Walk", "Attack", "Hit", "Death" };

            var animPlayer = FindAnimationPlayer(modelRoot);
            if (animPlayer == null) return false;

            // Find the monolithic animation — look for "Action" or "Armature" names
            Animation sourceAnim = null;
            string sourceAnimName = null;
            foreach (var name in animPlayer.GetAnimationList())
            {
                string lower = name.ToLower();
                if (lower.Contains("action") || lower.Contains("armature"))
                {
                    sourceAnim = animPlayer.GetAnimation(name);
                    sourceAnimName = name;
                    break;
                }
            }

            if (sourceAnim == null) return false;

            float totalLength = (float)sourceAnim.Length;
            int trackCount = sourceAnim.GetTrackCount();

            // Detect gaps between keyframes to find segment boundaries
            var gapTimes = new List<float>();
            for (int t = 0; t < Mathf.Min(trackCount, 5); t++)
            {
                int keyCount = sourceAnim.TrackGetKeyCount(t);
                if (keyCount < 4) continue;

                float avgDelta = totalLength / keyCount;
                float prevTime = (float)sourceAnim.TrackGetKeyTime(t, 0);
                for (int k = 1; k < keyCount; k++)
                {
                    float time = (float)sourceAnim.TrackGetKeyTime(t, k);
                    float delta = time - prevTime;
                    if (delta > avgDelta * 3f)
                    {
                        // Check if this gap time is already close to an existing one
                        bool duplicate = false;
                        foreach (float g in gapTimes)
                        {
                            if (Mathf.Abs(g - prevTime) < 0.1f) { duplicate = true; break; }
                        }
                        if (!duplicate) gapTimes.Add(prevTime);
                    }
                    prevTime = time;
                }
            }
            gapTimes.Sort();

            if (gapTimes.Count == 0)
            {
                GD.Print($"[CharacterAnimator] No gaps found in '{sourceAnimName}' ({totalLength:F2}s) — cannot split");
                return false;
            }

            // Build segment boundaries from gaps
            var boundaries = new List<float> { 0f };
            boundaries.AddRange(gapTimes);
            boundaries.Add(totalLength);

            // Map segments to names — use as many as we have names for
            int segCount = Mathf.Min(boundaries.Count - 1, segmentNames.Length);

            GD.Print($"[CharacterAnimator] Splitting '{sourceAnimName}' ({totalLength:F2}s, {trackCount} tracks) into {segCount} segments");

            // Get or create library
            AnimationLibrary lib;
            if (animPlayer.HasAnimationLibrary(""))
                lib = animPlayer.GetAnimationLibrary("");
            else
            {
                lib = new AnimationLibrary();
                animPlayer.AddAnimationLibrary("", lib);
            }

            for (int s = 0; s < segCount; s++)
            {
                float start = boundaries[s];
                float end = boundaries[s + 1];
                string clipName = segmentNames[s];

                var clip = new Animation();
                clip.Length = end - start;

                for (int t = 0; t < trackCount; t++)
                {
                    var trackType = sourceAnim.TrackGetType(t);
                    if (stripScale && trackType == Animation.TrackType.Scale3D) continue;

                    int newIdx = clip.AddTrack(trackType);
                    clip.TrackSetPath(newIdx, sourceAnim.TrackGetPath(t));
                    clip.TrackSetInterpolationType(newIdx, sourceAnim.TrackGetInterpolationType(t));

                    int keyCount = sourceAnim.TrackGetKeyCount(t);
                    for (int k = 0; k < keyCount; k++)
                    {
                        float keyTime = (float)sourceAnim.TrackGetKeyTime(t, k);
                        if (keyTime < start || keyTime >= end - 0.01f) continue;
                        clip.TrackInsertKey(newIdx, keyTime - start, sourceAnim.TrackGetKeyValue(t, k));
                    }
                }

                // Loop idle and walk
                if (clipName == "Idle" || clipName == "Walk" || clipName == "Run")
                    clip.LoopMode = Animation.LoopModeEnum.Linear;

                if (lib.HasAnimation(clipName)) lib.RemoveAnimation(clipName);
                lib.AddAnimation(clipName, clip);

                GD.Print($"[CharacterAnimator]   {clipName}: {start:F2}s - {end:F2}s ({clip.Length:F2}s)");
            }

            // Remove original monolithic animation
            string baseName = sourceAnimName.Contains("|") ? sourceAnimName.Split('|')[1] : sourceAnimName;
            if (lib.HasAnimation(sourceAnimName)) lib.RemoveAnimation(sourceAnimName);
            if (lib.HasAnimation(baseName)) lib.RemoveAnimation(baseName);
            foreach (var libName in animPlayer.GetAnimationLibraryList())
            {
                if (libName == "") continue;
                var otherLib = animPlayer.GetAnimationLibrary(libName);
                if (otherLib.HasAnimation(baseName)) otherLib.RemoveAnimation(baseName);
            }

            GD.Print($"[CharacterAnimator] After split: {string.Join(", ", animPlayer.GetAnimationList())}");
            return true;
        }
    }
}
