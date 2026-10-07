using Godot;
using System.Collections.Generic;
using System.Linq;

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
            // A real attack before a wind-up ("Charge" is the charge-up on the Rig| mechs)
            { AnimState.Attack,  new[] { "Attack", "attack", "ATTACK", "Attack_A", "Shoot", "shoot", "Attack_R", "Attack_L", "Punch", "punch", "Slash", "slash", "1H_Melee_Attack_Slice_Diagonal", "Charge", "charge" } },
            { AnimState.Hit,     new[] { "Hit", "hit", "HIT", "Hurt", "hurt", "Hit_A", "Take_Damage", "No", "no" } },
            // The Rig| mechs have no death; their power-down reads as one
            { AnimState.Death,   new[] { "Death", "death", "DEATH", "Die", "die", "Death_A", "Death_A_Pose", "TurnOff", "Turn_Off", "PowerDown", "Shutdown" } },
            // Not "Hit": Stunned loops, and sharing the clip made every hit reaction loop forever
            { AnimState.Stunned, new[] { "Stunned", "stunned", "Stun", "stun", "Dazed", "Sitting", "sitting" } },
        };

        // States that may borrow a model's only clip. A one-clip model's clip is its locomotion;
        // dying or attacking with it just looked like walking on.
        private static readonly HashSet<AnimState> _loopStates = new() { AnimState.Idle, AnimState.Walk, AnimState.Run, AnimState.Stunned };

        // ── Procedural fallback (models with no usable clips, and deaths with no death clip) ──
        private Node3D _modelRoot;
        private Node3D _pivot;               // child of the model root that procedural motion moves
        private Transform3D _pivotBase;
        private float _modelHeight = 1f;     // world units
        private bool _procedural;            // no usable clips at all: gait/bob drives the model
        private float _phase;
        private float _speed = 1f;
        private Tween _deathTween;
        private Transform3D? _preDeathTransform;

        /// <summary>Stand a procedurally-killed model back up (respawns reuse the model).</summary>
        private void ResetAfterDeath()
        {
            if (_preDeathTransform == null) return;
            _deathTween?.Kill();
            _deathTween = null;
            if (_pivot != null && IsInstanceValid(_pivot)) _pivot.Transform = _preDeathTransform.Value;
            _preDeathTransform = null;
            if (_animPlayer != null && !_animPlayer.IsPlaying()) _animPlayer.Play();
        }
        private Skeleton3D _gaitSkeleton;
        private readonly List<(int bone, float phase, Quaternion rest)> _gaitLegs = new();
        private int _gaitBody = -1;
        private Vector3 _gaitBodyRest;

        /// <summary>How far a procedural death drops the model (hovering models fall to the ground).</summary>
        public float ProceduralDeathDrop { get; set; }

        public AnimState CurrentState => _currentState;
        public bool IsInitialized => _animPlayer != null;
        public AnimationPlayer AnimPlayer => _animPlayer;

        /// <summary>True when states produce visible motion: real clips or the procedural fallback.</summary>
        public bool CanAnimate => _animPlayer != null && _availableAnims.Count > 0 || _procedural;

        /// <summary>True when the model has no usable clips and is moved procedurally.</summary>
        public bool IsProcedural => _procedural;

        /// <summary>True if this state plays a clip of its own (not the fallback or a borrowed one).</summary>
        public bool HasClip(AnimState state)
        {
            string clip = ResolveAnimName(state);
            if (clip == null) return false;
            if (state is AnimState.Death or AnimState.Attack or AnimState.Hit)
                return clip != ResolveAnimName(AnimState.Walk) && clip != ResolveAnimName(AnimState.Idle);
            return true;
        }

        /// <summary>
        /// Find and bind to an AnimationPlayer in the model hierarchy.
        /// </summary>
        public void Initialize(Node3D modelRoot)
        {
            _modelRoot = modelRoot;
            SetupPivot(modelRoot);

            _animPlayer = FindAnimationPlayer(modelRoot);
            _availableAnims.Clear();
            if (_animPlayer != null)
            {
                // Clips with no tracks or no length (Unreal's empty "Unreal Take", Blender PoseLib)
                // play nothing; mapping states to them froze the model mid-slide
                foreach (var animName in _animPlayer.GetAnimationList())
                    if (IsUsable(_animPlayer.GetAnimation(animName)))
                        _availableAnims.Add(animName);
            }

            if (_availableAnims.Count == 0)
            {
                _procedural = true;
                SetupGait(modelRoot);
                GD.Print($"[CharacterAnimator] '{modelRoot.Name}' has no usable clips, procedural " +
                         (_gaitLegs.Count > 0 ? $"gait on {_gaitLegs.Count} legs" : "bob"));
                return;
            }
            _procedural = false;

            GD.Print($"[CharacterAnimator] Initialized with {_availableAnims.Count} anims: {string.Join(", ", _availableAnims)}");

            // Large constant root offsets (a drone authored 160 units up) put the model far from
            // where it was grounded and scaled; keep the motion, drop the offset
            NormalizeRootOffsets();

            // Auto-detect animation name patterns and build fallback mappings
            BuildDynamicMappings();

            // Ensure looping animations are set to loop (FBX imports default to non-looping)
            SetLoopingAnimations();

            // Start with idle
            Play(AnimState.Idle);
        }

        private static bool IsUsable(Animation a)
            => a != null && a.Length > 0.05f && a.GetTrackCount() > 0;

        private void SetupPivot(Node3D modelRoot)
        {
            // The child that carries the visible meshes (FBX roots can hold empty helper nodes)
            _pivot = null;
            int pivots = 0;
            foreach (var child in modelRoot.GetChildren())
            {
                if (child is not Node3D n) continue;
                if (n is MeshInstance3D || n.FindChildren("*", "MeshInstance3D", true, false).Count > 0)
                {
                    _pivot ??= n;
                    pivots++;
                }
            }
            // Several mesh-bearing children: insert nothing, move the root's first one only if
            // it's the sole carrier; otherwise fall back to the root (yaw is reapplied each frame
            // by the owner, so procedural motion there only adds bob and sway)
            if (pivots != 1) _pivot = modelRoot;
            _pivotBase = _pivot.Transform;
            var aabb = AssetLibrary.GetCombinedAABB(modelRoot);
            _modelHeight = Mathf.Max(0.05f, aabb.Size.Y * modelRoot.Scale.Y);
        }

        /// <summary>
        /// Some rigs hold the whole skinned mesh far from where its vertices are authored (the
        /// Swarm drone's root bone sits 160 units up in both rest and clips), so it renders well
        /// above where its mesh bounds grounded it. Measure how far the skin actually displaces
        /// the mesh, at rest and in each clip, and move the root bone back by that much. The
        /// clip edit is shared between instances and idempotent: a shifted clip measures ~0.
        /// </summary>
        private void NormalizeRootOffsets()
        {
            var animRoot = _animPlayer.GetNodeOrNull(_animPlayer.RootNode) ?? _modelRoot;
            foreach (var mi in animRoot.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
            {
                if (mi.Skin == null || mi.Skin.GetBindCount() == 0 || mi.Mesh == null) continue;
                if (mi.GetNodeOrNull(mi.Skeleton) is not Skeleton3D sk) continue;
                int bind = 0;
                string bindName = mi.Skin.GetBindName(bind);
                int bone = !string.IsNullOrEmpty(bindName) ? sk.FindBone(bindName) : mi.Skin.GetBindBone(bind);
                if (bone < 0) continue;
                int rootBone = bone;
                while (sk.GetBoneParent(rootBone) >= 0) rootBone = sk.GetBoneParent(rootBone);
                var ib = mi.Skin.GetBindPose(bind);
                var sz = mi.Mesh.GetAabb().Size;
                float meshSize = Mathf.Max(sz.X, Mathf.Max(sz.Y, sz.Z));

                // Rest: per instance
                var restOffset = (sk.GetBoneGlobalRest(bone) * ib).Origin;
                if (restOffset.Length() > meshSize)
                {
                    var rest = sk.GetBoneRest(rootBone);
                    rest.Origin -= restOffset;
                    sk.SetBoneRest(rootBone, rest);
                    sk.SetBonePosePosition(rootBone, rest.Origin);
                }

                // Clips: shared
                string rootName = sk.GetBoneName(rootBone);
                foreach (var name in _availableAnims)
                {
                    var a = _animPlayer.GetAnimation(name);
                    _animPlayer.Play(name);
                    _animPlayer.Seek(0, true);
                    var offset = (sk.GetBoneGlobalPose(bone) * ib).Origin;
                    if (offset.Length() <= meshSize) continue;
                    for (int t = 0; t < a.GetTrackCount(); t++)
                    {
                        if (a.TrackGetType(t) != Animation.TrackType.Position3D) continue;
                        var path = a.TrackGetPath(t);
                        if (path.GetSubNameCount() == 0 || path.GetSubName(0) != rootName) continue;
                        for (int k = 0; k < a.TrackGetKeyCount(t); k++)
                            a.TrackSetKeyValue(t, k, (Vector3)a.TrackGetKeyValue(t, k) - offset);
                    }
                    GD.Print($"[CharacterAnimator] '{name}': removed {offset.Length():F1}-unit skin offset on '{rootName}'");
                }
                _animPlayer.Stop();
                break; // one skinned mesh decides; the others share the rig
            }
        }

        private static float SkeletonMeshSize(Skeleton3D sk)
        {
            float size = 0f;
            foreach (var child in sk.GetChildren())
                if (child is MeshInstance3D mi && mi.Mesh != null)
                {
                    var s = mi.Mesh.GetAabb().Size;
                    size = Mathf.Max(size, Mathf.Max(s.X, Mathf.Max(s.Y, s.Z)));
                }
            return size > 0f ? size : 1f;
        }

        /// <summary>
        /// Legged models shipped without clips (the Scavenger's four-legged bot) get a trot:
        /// diagonal leg pairs swing in turn and the body bobs.
        /// </summary>
        private void SetupGait(Node3D modelRoot)
        {
            _gaitLegs.Clear();
            _gaitBody = -1;
            _gaitSkeleton = modelRoot.FindChildren("*", "Skeleton3D", true, false).Count > 0
                ? (Skeleton3D)modelRoot.FindChildren("*", "Skeleton3D", true, false)[0] : null;
            if (_gaitSkeleton == null) return;
            for (int b = 0; b < _gaitSkeleton.GetBoneCount(); b++)
            {
                string n = _gaitSkeleton.GetBoneName(b).ToLower();
                if (n.EndsWith("_end")) continue;
                if (n.Contains("leg"))
                {
                    bool front = n.Contains("front"), left = n.Contains("_l") || n.EndsWith("l");
                    // Trot: front-left with back-right, front-right with back-left
                    float phase = (front == left) ? 0f : Mathf.Pi;
                    _gaitLegs.Add((b, phase, _gaitSkeleton.GetBoneRest(b).Basis.GetRotationQuaternion()));
                }
                else if (n == "body" || n == "spine" || n == "torso")
                {
                    _gaitBody = b;
                    _gaitBodyRest = _gaitSkeleton.GetBoneRest(b).Origin;
                }
            }
        }

        public override void _Process(double delta)
        {
            if (!_procedural || _pivot == null || !IsInstanceValid(_pivot)) return;
            if (_currentState == AnimState.Death) return; // the death tween owns the pivot

            bool moving = _currentState is AnimState.Walk or AnimState.Run or AnimState.Attack;
            float rate = moving ? 9f : 2.5f;
            _phase += (float)delta * rate * Mathf.Max(0.2f, _speed);

            if (_gaitSkeleton != null && IsInstanceValid(_gaitSkeleton) && _gaitLegs.Count > 0)
            {
                float swing = moving ? 0.5f : 0.06f; // radians
                foreach (var (bone, phase, rest) in _gaitLegs)
                {
                    float a = Mathf.Sin(_phase + phase) * swing;
                    _gaitSkeleton.SetBonePoseRotation(bone, rest * new Quaternion(Vector3.Right, a));
                }
                if (_gaitBody >= 0)
                {
                    float bob = Mathf.Abs(Mathf.Sin(_phase)) * (moving ? 0.0018f : 0.0006f);
                    _gaitSkeleton.SetBonePosePosition(_gaitBody, _gaitBodyRest + new Vector3(0, bob, 0));
                }
            }

            // Whole-model bob and sway, in the pivot's parent space
            float worldBob = (moving ? 0.06f : 0.02f) * _modelHeight * Mathf.Abs(Mathf.Sin(_phase));
            if (_pivot == _modelRoot)
            {
                // The owner sets the root's yaw every frame; only bob it
                var p = _pivot.Position;
                _pivot.Position = new Vector3(p.X, _pivotBase.Origin.Y + worldBob, p.Z);
                return;
            }
            float sway = (moving ? 4f : 1f) * Mathf.Sin(_phase * 0.5f);
            var t = _pivotBase;
            t.Origin += new Vector3(0, worldBob / PivotParentScale, 0);
            t.Basis = new Basis(Vector3.Forward, Mathf.DegToRad(sway)) * t.Basis;
            _pivot.Transform = t;
        }

        private float PivotParentScale => _pivot == _modelRoot ? 1f : Mathf.Max(0.0001f, _modelRoot?.Scale.Y ?? 1f);

        /// <summary>Test hook: the running procedural death, so a sheet can step through it.</summary>
        internal Tween ProceduralDeathTween => _deathTween;

        /// <summary>Test hook: the node procedural motion moves.</summary>
        internal Node3D ProceduralPivot => _pivot;

        /// <summary>Tip over and sink, for deaths without a death clip.</summary>
        private void PlayProceduralDeath()
        {
            if (_pivot == null || !IsInstanceValid(_pivot)) return;
            _deathTween?.Kill();
            if (_animPlayer != null) _animPlayer.Pause(); // freeze the last pose rather than walk on
            var start = _pivot.Transform;
            _preDeathTransform = start;
            var end = start;
            // Roll about the model's forward axis, then settle into the ground. A child pivot's
            // parent is the model root (Y up, facing forward); the root itself turns in its own frame.
            var roll = new Basis(Vector3.Forward, Mathf.DegToRad(80f));
            end.Basis = _pivot == _modelRoot ? start.Basis * roll : roll * start.Basis;
            end.Origin -= new Vector3(0, (ProceduralDeathDrop + _modelHeight * 0.15f) / PivotParentScale, 0);
            _deathTween = CreateTween();
            _deathTween.TweenMethod(Callable.From<float>(f =>
            {
                if (!IsInstanceValid(_pivot)) return;
                _pivot.Transform = start.InterpolateWith(end, f);
            }), 0f, 1f, 0.45f).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
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
                foreach (var state in _loopStates)
                {
                    _dynamicOverrides[state] = onlyAnim;
                }
                GD.Print($"[CharacterAnimator] Single-anim model: locomotion -> '{onlyAnim}' (death/attack are procedural)");
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
            // States that should loop (a clip a one-shot state also plays is left alone)
            var loopingStates = new[] { AnimState.Idle, AnimState.Walk, AnimState.Run, AnimState.Stunned };

            var oneShot = new HashSet<string>();
            foreach (var st in new[] { AnimState.Attack, AnimState.Hit, AnimState.Death })
            {
                string n = ResolveAnimName(st);
                if (n != null) oneShot.Add(n);
            }

            foreach (var state in loopingStates)
            {
                string animName = ResolveAnimName(state);
                if (animName == null || oneShot.Contains(animName)) continue;

                var anim = _animPlayer.GetAnimation(animName);
                if (anim != null && anim.LoopMode == Animation.LoopModeEnum.None)
                {
                    anim.LoopMode = Animation.LoopModeEnum.Linear;
                }
            }

            // One-shots play once (some packs import Hit as looping)
            foreach (var name in oneShot)
            {
                var anim = _animPlayer.GetAnimation(name);
                if (anim != null && !IsLoopClip(name)) anim.LoopMode = Animation.LoopModeEnum.None;
            }
        }

        private bool IsLoopClip(string clip)
        {
            foreach (var st in _loopStates)
                if (ResolveAnimName(st) == clip) return true;
            return false;
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
            if (state == AnimState.Death && !HasClip(AnimState.Death))
            {
                _currentState = AnimState.Death;
                PlayProceduralDeath();
                return;
            }
            ResetAfterDeath();
            if (_procedural) return; // _Process drives the model
            if (_animPlayer == null) return;

            // Refresh in case clips were added after init (e.g. split animations)
            foreach (var name in _animPlayer.GetAnimationList())
                if (IsUsable(_animPlayer.GetAnimation(name))) _availableAnims.Add(name);

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
            _speed = speed;
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
            if (animationName != "Death") ResetAfterDeath();

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
            ResetAfterDeath();

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
        public static bool SplitMonolithicAnimation(Node3D modelRoot, string[] segmentNames = null, bool stripScale = true, bool stripRootMotion = true)
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

            // Only a timeline holding everything is monolithic. "RobotArmature|Robot_Walking" in a
            // pack of named clips matched "armature" and got chopped up and deleted.
            foreach (var name in animPlayer.GetAnimationList())
            {
                if (name == sourceAnimName || name.ToLower().Contains("poselib")) continue;
                if (IsUsable(animPlayer.GetAnimation(name))) return false;
            }

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

                    // Strip root motion — position tracks on root/center bones cause
                    // characters to walk/roll off screen instead of animating in-place
                    if (stripRootMotion && trackType == Animation.TrackType.Position3D)
                    {
                        string trackPath = sourceAnim.TrackGetPath(t).ToString().ToLower();
                        // Root bones are typically named root, center, hips, or are the first bone
                        if (trackPath.Contains(":root") || trackPath.Contains(":center") ||
                            trackPath.Contains(":hips") || trackPath.EndsWith(":bone_001"))
                            continue;
                    }

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
