using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Character Viewer/Builder — browse all 10 characters (3 players, BIT companion,
    /// 6 enemies), preview them in 3D with orbit camera, test animations, attach/configure
    /// weapons, view abilities, and tweak theme/scale.
    /// </summary>
    public partial class CharacterViewerEditor : EditorModule
    {
        public override string ModuleName => "Characters";
        public override Color AccentColor => new(0.95f, 0.65f, 0.15f);

        // ── Character definition ──

        private enum CharacterRole { Player, Companion, Enemy }

        private struct CharacterDef
        {
            public string Name, ModelPath;
            public CharacterRole Role;
            public VineEnemyFaction? Faction;
            public float HP, Speed, AttackRange, AttackDamage, AttackInterval;
        }

        private static readonly CharacterDef[] Characters = {
            // Players
            new() { Name = "Clunker", ModelPath = AssetLibrary.PLAYER_CLUNKER, Role = CharacterRole.Player,
                     HP = Constants.VINE_PLAYER_MAX_HP, Speed = Constants.VINE_PLAYER_MOVE_SPEED,
                     AttackRange = Constants.VINE_PLAYER_ATTACK_RANGE,
                     AttackDamage = Constants.VINE_PLAYER_ATTACK_DAMAGE,
                     AttackInterval = 1f / Constants.VINE_PLAYER_ATTACK_SPEED },
            new() { Name = "Rustbucket", ModelPath = AssetLibrary.PLAYER_RUSTBUCKET, Role = CharacterRole.Player,
                     HP = Constants.VINE_PLAYER_MAX_HP, Speed = Constants.VINE_PLAYER_MOVE_SPEED,
                     AttackRange = Constants.VINE_PLAYER_ATTACK_RANGE,
                     AttackDamage = Constants.VINE_PLAYER_ATTACK_DAMAGE,
                     AttackInterval = 1f / Constants.VINE_PLAYER_ATTACK_SPEED },
            new() { Name = "Sparkplug", ModelPath = AssetLibrary.PLAYER_SPARKPLUG, Role = CharacterRole.Player,
                     HP = Constants.VINE_PLAYER_MAX_HP, Speed = Constants.VINE_PLAYER_MOVE_SPEED,
                     AttackRange = Constants.VINE_PLAYER_ATTACK_RANGE,
                     AttackDamage = Constants.VINE_PLAYER_ATTACK_DAMAGE,
                     AttackInterval = 1f / Constants.VINE_PLAYER_ATTACK_SPEED },
            // BIT — currently the main player character in-game
            new() { Name = "BIT", ModelPath = AssetLibrary.COMPANION_BIT, Role = CharacterRole.Player,
                     HP = Constants.VINE_PLAYER_MAX_HP, Speed = Constants.VINE_PLAYER_MOVE_SPEED,
                     AttackRange = Constants.VINE_PLAYER_ATTACK_RANGE,
                     AttackDamage = Constants.VINE_PLAYER_ATTACK_DAMAGE,
                     AttackInterval = 1f / Constants.VINE_PLAYER_ATTACK_SPEED },
            // Robot 1 — second player character (bit.fbx model)
            new() { Name = "Robot 1", ModelPath = "res://Models/Characters/Companions/bit.fbx", Role = CharacterRole.Player,
                     HP = Constants.VINE_PLAYER_MAX_HP, Speed = Constants.VINE_PLAYER_MOVE_SPEED,
                     AttackRange = Constants.VINE_PLAYER_ATTACK_RANGE,
                     AttackDamage = Constants.VINE_PLAYER_ATTACK_DAMAGE,
                     AttackInterval = 1f / Constants.VINE_PLAYER_ATTACK_SPEED },
            // Enemies — stats from first-wave data + faction defaults
            new() { Name = "Scrap Rat", ModelPath = AssetLibrary.ENEMY_SCRAP_RAT, Role = CharacterRole.Enemy,
                     Faction = VineEnemyFaction.Scavenger,
                     HP = 20, Speed = 2.5f, AttackRange = 5f, AttackDamage = 4f, AttackInterval = 1.5f },
            new() { Name = "Wire Worm", ModelPath = AssetLibrary.ENEMY_WIRE_WORM, Role = CharacterRole.Enemy,
                     Faction = VineEnemyFaction.Scavenger,
                     HP = 25, Speed = 2.5f, AttackRange = 5f, AttackDamage = 4f, AttackInterval = 1.5f },
            new() { Name = "Trilobite", ModelPath = AssetLibrary.ENEMY_TRILOBITE, Role = CharacterRole.Enemy,
                     Faction = VineEnemyFaction.Ghost,
                     HP = 35, Speed = 3f, AttackRange = 6f, AttackDamage = 3f, AttackInterval = 2f },
            new() { Name = "Quad Shell", ModelPath = AssetLibrary.ENEMY_QUAD_SHELL, Role = CharacterRole.Enemy,
                     Faction = VineEnemyFaction.Brute,
                     HP = 80, Speed = 1.5f, AttackRange = 3f, AttackDamage = 10f, AttackInterval = 2.5f },
            new() { Name = "Spark Drone", ModelPath = AssetLibrary.ENEMY_SPARK_DRONE, Role = CharacterRole.Enemy,
                     Faction = VineEnemyFaction.Swarm,
                     HP = 10, Speed = 4f, AttackRange = 4f, AttackDamage = 2f, AttackInterval = 1f },
            new() { Name = "Decoy Unit", ModelPath = AssetLibrary.ENEMY_DECOY, Role = CharacterRole.Enemy,
                     Faction = VineEnemyFaction.Scavenger,
                     HP = 15, Speed = 3f, AttackRange = 5f, AttackDamage = 4f, AttackInterval = 1.5f },
        };

        // ── UI fields ──

        private VBoxContainer _charList;
        private VBoxContainer _inspector;
        private SubViewport _previewViewport;
        private SubViewportContainer _previewContainer;
        private Node3D _previewRoot;
        private Node3D _previewPivot;
        private Node3D _previewModel;
        private Camera3D _previewCamera;
        private DirectionalLight3D _previewLight;
        private CharacterAnimator _animator;
        private Node3D _weaponModel;
        private Label _statusLabel;

        private int _selectedCharIndex = -1;
        private int _selectedFaction;  // 0=player, 1=scavenger, 2=brute, 3=swarm, 4=ghost, 5=original
        private float _previewZoom = 6f;
        private float _orbitAngleX;
        private float _orbitAngleY = -25f;

        // Weapon attachment state
        private int _weaponChoice;  // 0=None
        private Vector3 _weaponPos = new(0.2f, 0.4f, 0.3f);
        private Vector3 _weaponRotDeg = Vector3.Zero;
        private Vector3 _weaponScale = new(0.3f, 0.3f, 0.3f);

        // Character list buttons for highlight
        private readonly List<Button> _charButtons = new();

        // Skeleton bone pose overrides
        private Skeleton3D _previewSkeleton;
        private readonly Dictionary<int, Vector3> _boneOverrides = new(); // boneIdx -> rotation degrees

        // Animation timeline/clipper state
        private AnimationPlayer _rawAnimPlayer;      // Direct reference to model's AnimationPlayer
        private string _rawAnimName;                  // Original monolithic animation name
        private float _rawAnimLength;                 // Total length of raw animation
        private bool _isScrubbingRaw;                 // Whether we're in raw scrub mode
        private bool _scrubPlaying;                   // Play/pause toggle during scrub
        private HSlider _scrubSlider;
        private Label _scrubTimeLabel;
        private Label _scrubTrackInfo;

        // Segment definitions for the clipper
        private struct AnimSegment
        {
            public string Name;
            public float Start;
            public float End;
            public bool Loop;
        }
        private readonly List<AnimSegment> _segments = new();

        private static readonly string[] FactionNames = {
            "Player (Blue)", "Scavenger (Red)", "Brute (Crimson)",
            "Swarm (Orange)", "Ghost (Magenta)", "Original Materials"
        };

        private static readonly string[] WeaponNames = {
            "None", "Weapon A", "Weapon B", "Rocket Launcher", "Plasma Gun"
        };
        private static readonly string[] WeaponPaths = {
            null, AssetLibrary.WEAPON_A, AssetLibrary.WEAPON_B,
            AssetLibrary.ROCKET_LAUNCHER, AssetLibrary.PLASMA_GUN
        };

        // Dynamic animation clip list — rebuilt per character
        private HBoxContainer _animButtonRow;
        private int _currentClipIndex;
        private string[] _currentClipNames = System.Array.Empty<string>();

        // ── Build UI ──

        public override void _Ready()
        {
            BuildUI();
        }

        private void BuildUI()
        {
            var outerHBox = new HBoxContainer();
            outerHBox.SizeFlagsVertical = SizeFlags.ExpandFill;
            outerHBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            outerHBox.AddThemeConstantOverride("separation", 2);
            AddChild(outerHBox);

            // ── Left: Character selector ──
            var leftPanel = new PanelContainer();
            leftPanel.CustomMinimumSize = new Vector2(180, 0);
            leftPanel.AddThemeStyleboxOverride("panel", EditorStyles.MakePanel(EditorStyles.BgPanel));
            outerHBox.AddChild(leftPanel);

            var leftVBox = new VBoxContainer();
            leftVBox.AddThemeConstantOverride("separation", 2);
            leftPanel.AddChild(leftVBox);

            leftVBox.AddChild(EditorStyles.MakeLabel("Characters", 16, AccentColor));
            leftVBox.AddChild(EditorStyles.MakeSeparator());

            var scroll = new ScrollContainer();
            scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            leftVBox.AddChild(scroll);

            _charList = new VBoxContainer();
            _charList.AddThemeConstantOverride("separation", 1);
            scroll.AddChild(_charList);

            PopulateCharacterList();

            // ── Center: 3D preview + animation bar ──
            var centerVBox = new VBoxContainer();
            centerVBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            centerVBox.AddThemeConstantOverride("separation", 4);
            outerHBox.AddChild(centerVBox);

            // SubViewport for 3D preview
            _previewContainer = new SubViewportContainer();
            _previewContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
            _previewContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _previewContainer.Stretch = true;
            centerVBox.AddChild(_previewContainer);

            _previewViewport = new SubViewport();
            _previewViewport.Size = new Vector2I(1024, 768);
            _previewViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
            _previewViewport.OwnWorld3D = true;
            _previewViewport.TransparentBg = false;
            _previewContainer.AddChild(_previewViewport);

            // Preview scene setup
            _previewRoot = new Node3D();
            _previewViewport.AddChild(_previewRoot);

            _previewCamera = new Camera3D();
            _previewCamera.Fov = 50;
            _previewRoot.AddChild(_previewCamera);
            _previewCamera.Current = true;
            UpdatePreviewCamera();

            // Lights
            _previewLight = new DirectionalLight3D();
            _previewLight.RotationDegrees = new Vector3(-40, -30, 0);
            _previewLight.LightColor = new Color(0.9f, 0.9f, 0.9f);
            _previewLight.LightEnergy = 1.2f;
            _previewLight.ShadowEnabled = true;
            _previewRoot.AddChild(_previewLight);

            var fillLight = new DirectionalLight3D();
            fillLight.RotationDegrees = new Vector3(-20, 150, 0);
            fillLight.LightColor = new Color(0.4f, 0.5f, 0.6f);
            fillLight.LightEnergy = 0.5f;
            _previewRoot.AddChild(fillLight);

            // Environment
            var worldEnv = new WorldEnvironment();
            var env = new Godot.Environment();
            env.BackgroundMode = Godot.Environment.BGMode.Color;
            env.BackgroundColor = new Color(0.08f, 0.08f, 0.1f);
            env.AmbientLightColor = new Color(0.3f, 0.3f, 0.35f);
            env.AmbientLightEnergy = 0.8f;
            worldEnv.Environment = env;
            _previewRoot.AddChild(worldEnv);

            // Ground plane
            var ground = new MeshInstance3D();
            var groundMesh = new PlaneMesh();
            groundMesh.Size = new Vector2(20, 20);
            ground.Mesh = groundMesh;
            var groundMat = new StandardMaterial3D();
            groundMat.AlbedoColor = new Color(0.12f, 0.12f, 0.15f);
            groundMat.Roughness = 0.9f;
            ground.MaterialOverride = groundMat;
            _previewRoot.AddChild(ground);

            // Animation controls bar — dynamic clip buttons, rebuilt per character
            var animPanel = new PanelContainer();
            animPanel.CustomMinimumSize = new Vector2(0, 40);
            animPanel.AddThemeStyleboxOverride("panel", EditorStyles.MakePanel(EditorStyles.BgPanel));
            centerVBox.AddChild(animPanel);

            var animOuter = new VBoxContainer();
            animOuter.AddThemeConstantOverride("separation", 2);
            animPanel.AddChild(animOuter);

            // Top row: prev/next + speed
            var navRow = new HBoxContainer();
            navRow.AddThemeConstantOverride("separation", 4);

            var prevBtn = EditorStyles.MakeButton("<< Prev", 12, EditorStyles.TextSecondary);
            prevBtn.CustomMinimumSize = new Vector2(60, 28);
            prevBtn.Pressed += () => CycleClip(-1);
            navRow.AddChild(prevBtn);

            var nextBtn = EditorStyles.MakeButton("Next >>", 12, EditorStyles.TextSecondary);
            nextBtn.CustomMinimumSize = new Vector2(60, 28);
            nextBtn.Pressed += () => CycleClip(1);
            navRow.AddChild(nextBtn);

            navRow.AddChild(EditorStyles.MakeLabel("Speed:", 12));
            var speedSpin = EditorStyles.MakeSpinBox(1f, 0.1f, 3f, 0.1f);
            speedSpin.ValueChanged += (double v) => _animator?.SetSpeed((float)v);
            navRow.AddChild(speedSpin);
            animOuter.AddChild(navRow);

            // Bottom row: per-clip buttons (populated dynamically)
            _animButtonRow = new HBoxContainer();
            _animButtonRow.AddThemeConstantOverride("separation", 2);
            animOuter.AddChild(_animButtonRow);

            // Status label
            _statusLabel = EditorStyles.MakeLabel("Select a character", 12, EditorStyles.TextMuted);
            centerVBox.AddChild(_statusLabel);

            // ── Right: Inspector ──
            var inspPanel = new PanelContainer();
            inspPanel.CustomMinimumSize = new Vector2(280, 0);
            inspPanel.AddThemeStyleboxOverride("panel", EditorStyles.MakePanel(EditorStyles.BgPanel));
            outerHBox.AddChild(inspPanel);

            var inspScroll = new ScrollContainer();
            inspScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            inspPanel.AddChild(inspScroll);

            _inspector = new VBoxContainer();
            _inspector.AddThemeConstantOverride("separation", 4);
            _inspector.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            inspScroll.AddChild(_inspector);

            _inspector.AddChild(EditorStyles.MakeLabel("Select a character to inspect", 14, EditorStyles.TextMuted));
        }

        // ── Character List ──

        private void PopulateCharacterList()
        {
            _charButtons.Clear();
            string lastCategory = null;

            for (int i = 0; i < Characters.Length; i++)
            {
                var def = Characters[i];
                string category = def.Role switch {
                    CharacterRole.Player => "PLAYERS",
                    CharacterRole.Companion => "COMPANION",
                    CharacterRole.Enemy => "ENEMIES",
                    _ => "OTHER"
                };

                if (category != lastCategory)
                {
                    lastCategory = category;
                    var margin = new MarginContainer();
                    margin.AddThemeConstantOverride("margin_top", 8);
                    margin.AddChild(EditorStyles.MakeLabel(category, 12, EditorStyles.TextSecondary));
                    _charList.AddChild(margin);
                }

                int idx = i;
                var btn = new Button();
                btn.Text = def.Name;
                btn.Alignment = HorizontalAlignment.Left;
                btn.CustomMinimumSize = new Vector2(0, 28);
                btn.Pressed += () => SelectCharacter(idx);
                _charList.AddChild(btn);
                _charButtons.Add(btn);
            }
        }

        // ── Select Character ──

        private void SelectCharacter(int index)
        {
            _selectedCharIndex = index;
            var def = Characters[index];

            // Highlight selected button
            for (int i = 0; i < _charButtons.Count; i++)
            {
                _charButtons[i].AddThemeColorOverride("font_color",
                    i == index ? AccentColor : EditorStyles.TextPrimary);
            }

            // Default to original materials so you see the FBX's native palette
            _selectedFaction = 5; // Original Materials

            // Reset weapon and animation state
            _weaponChoice = 0;
            _weaponPos = new Vector3(0.2f, 0.4f, 0.3f);
            _weaponRotDeg = Vector3.Zero;
            _weaponScale = new Vector3(0.3f, 0.3f, 0.3f);
            _isScrubbingRaw = false;
            _scrubPlaying = false;
            _boneOverrides.Clear();
            _previewSkeleton = null;

            // Clear old preview
            if (_previewPivot != null)
            {
                _previewPivot.QueueFree();
                _previewPivot = null;
                _previewModel = null;
                _weaponModel = null;
            }
            if (_animator != null)
            {
                _animator.QueueFree();
                _animator = null;
            }

            // Load model
            var model = AssetLibrary.InstantiateNormalized(def.ModelPath);
            if (model == null)
            {
                SetStatus($"Failed to load: {def.Name}");
                BuildInspectorError(def);
                return;
            }

            _previewPivot = new Node3D();
            _previewRoot.AddChild(_previewPivot);
            _previewPivot.AddChild(model);
            _previewModel = model;

            // Split animations — BIT and Robot 1 use the same skeleton/monolithic animation format
            if (def.Name == "BIT" || def.Name == "Robot 1")
                VinePlayer.SplitBitAnimations(model);
            else if (def.Role == CharacterRole.Enemy)
                CharacterAnimator.SplitMonolithicAnimation(model);

            // Initialize animator
            _animator = new CharacterAnimator();
            _previewRoot.AddChild(_animator);
            _animator.Initialize(model);

            // No theme applied — show original FBX materials by default.
            // User can apply a theme via the faction picker in the inspector.

            // Center/ground the model after one frame
            CallDeferred(nameof(FinalizePreview));

            SetStatus($"Loaded: {def.Name}");
        }

        // ── Animation ──

        /// <summary>
        /// Rebuild the clip button row from the model's actual AnimationPlayer clips.
        /// Called after SelectCharacter and after re-split.
        /// </summary>
        private void RebuildAnimButtons()
        {
            if (_animButtonRow == null) return;
            foreach (var child in _animButtonRow.GetChildren())
                child.QueueFree();

            _currentClipNames = System.Array.Empty<string>();
            _currentClipIndex = 0;

            var ap = _animator?.AnimPlayer;
            if (ap == null) return;

            var names = new List<string>();
            foreach (var n in ap.GetAnimationList())
                names.Add(n);
            names.Sort();
            _currentClipNames = names.ToArray();

            for (int i = 0; i < _currentClipNames.Length; i++)
            {
                int idx = i;
                string clipName = _currentClipNames[idx];
                var btn = EditorStyles.MakeButton(clipName, 11, AccentColor);
                btn.CustomMinimumSize = new Vector2(0, 26);
                btn.Pressed += () => PlayClipByIndex(idx);
                _animButtonRow.AddChild(btn);
            }

            if (_currentClipNames.Length == 0)
                _animButtonRow.AddChild(EditorStyles.MakeLabel("No clips", 11, EditorStyles.TextMuted));
        }

        private void PlayClipByIndex(int index)
        {
            if (_animator == null || index < 0 || index >= _currentClipNames.Length) return;
            _currentClipIndex = index;
            string clipName = _currentClipNames[index];
            _animator.PlayCustom(clipName);
            SetStatus($"Playing: {clipName} ({index + 1}/{_currentClipNames.Length})");
            HighlightActiveClipButton();
        }

        private void CycleClip(int direction)
        {
            if (_currentClipNames.Length == 0) return;
            _currentClipIndex = (_currentClipIndex + direction + _currentClipNames.Length) % _currentClipNames.Length;
            PlayClipByIndex(_currentClipIndex);
        }

        private void HighlightActiveClipButton()
        {
            if (_animButtonRow == null) return;
            int i = 0;
            foreach (var child in _animButtonRow.GetChildren())
            {
                if (child is Button btn)
                {
                    btn.AddThemeColorOverride("font_color",
                        i == _currentClipIndex ? AccentColor : EditorStyles.TextPrimary);
                    i++;
                }
            }
        }

        // ── Theme ──

        private void ApplyThemeToPreview()
        {
            if (_previewModel == null || _selectedCharIndex < 0) return;

            var def = Characters[_selectedCharIndex];
            var oldScale = _previewModel.Scale;
            var oldPos = _previewModel.Position;

            // Reload fresh model to undo previous theme
            _previewModel.QueueFree();

            _previewModel = AssetLibrary.InstantiateNormalized(def.ModelPath);
            if (_previewModel == null) return;

            _previewModel.Scale = oldScale;
            _previewModel.Position = oldPos;
            _previewPivot.AddChild(_previewModel);

            // Re-split animations on the fresh model
            if (def.Name == "BIT" || def.Name == "Robot 1")
                VinePlayer.SplitBitAnimations(_previewModel);
            else if (def.Role == CharacterRole.Enemy)
                CharacterAnimator.SplitMonolithicAnimation(_previewModel);

            // Re-init animator
            if (_animator != null)
            {
                _animator.QueueFree();
                _animator = null;
            }
            _animator = new CharacterAnimator();
            _previewRoot.AddChild(_animator);
            _animator.Initialize(_previewModel);

            // Apply theme
            var theme = PlanetTheme.Current;
            switch (_selectedFaction)
            {
                case 0: theme.ApplyToNode(_previewModel, theme.PlayerPrimary); break;
                case 1: theme.ApplyEnemyTheme(_previewModel, VineEnemyFaction.Scavenger); break;
                case 2: theme.ApplyEnemyTheme(_previewModel, VineEnemyFaction.Brute); break;
                case 3: theme.ApplyEnemyTheme(_previewModel, VineEnemyFaction.Swarm); break;
                case 4: theme.ApplyEnemyTheme(_previewModel, VineEnemyFaction.Ghost); break;
                case 5: break; // Original — no theme
            }

            // Re-attach weapon if one was configured
            if (_weaponChoice > 0)
                AttachWeapon(WeaponPaths[_weaponChoice], _weaponPos, _weaponRotDeg, _weaponScale);

            // Re-discover skeleton and re-apply bone overrides
            _previewSkeleton = FindSkeletonInTree(_previewModel);
            if (_previewSkeleton != null && _boneOverrides.Count > 0)
            {
                foreach (var (boneIdx, rot) in _boneOverrides)
                {
                    if (boneIdx < _previewSkeleton.GetBoneCount())
                    {
                        var q = Quaternion.FromEuler(new Vector3(
                            Mathf.DegToRad(rot.X), Mathf.DegToRad(rot.Y), Mathf.DegToRad(rot.Z)));
                        _previewSkeleton.SetBonePoseRotation(boneIdx, q);
                    }
                }
            }

            // Rebuild clip buttons since model was reloaded
            RebuildAnimButtons();

            EditorManager.Instance?.SetStatus($"Applied {FactionNames[_selectedFaction]} theme");
        }

        // ── Weapon Attachment ──

        private void AttachWeapon(string path, Vector3 pos, Vector3 rotDeg, Vector3 scale)
        {
            if (_previewModel == null) return;

            // Remove old weapon
            if (_weaponModel != null)
            {
                _weaponModel.QueueFree();
                _weaponModel = null;
            }

            if (string.IsNullOrEmpty(path)) return;

            _weaponModel = AssetLibrary.InstantiateNormalized(path);
            if (_weaponModel == null) return;

            _weaponModel.Position = pos;
            _weaponModel.RotationDegrees = rotDeg;
            _weaponModel.Scale = scale;
            _previewModel.AddChild(_weaponModel);

            // Apply current faction theme to weapon
            var theme = PlanetTheme.Current;
            if (_selectedFaction < 5 && _selectedFaction > 0)
            {
                var faction = _selectedFaction switch {
                    1 => VineEnemyFaction.Scavenger,
                    2 => VineEnemyFaction.Brute,
                    3 => VineEnemyFaction.Swarm,
                    4 => VineEnemyFaction.Ghost,
                    _ => VineEnemyFaction.Scavenger
                };
                theme.ApplyEnemyTheme(_weaponModel, faction);
            }
            else if (_selectedFaction == 0)
            {
                theme.ApplyToNode(_weaponModel, theme.PlayerPrimary);
            }
        }

        private void DetachWeapon()
        {
            if (_weaponModel != null)
            {
                _weaponModel.QueueFree();
                _weaponModel = null;
            }
            _weaponChoice = 0;
        }

        // ── Preview Camera / Finalize ──

        private void FinalizePreview()
        {
            if (_previewModel == null || _previewPivot == null) return;

            var globalAABB = new Aabb();
            bool first = true;
            CollectGlobalAABB(_previewModel, ref globalAABB, ref first);

            if (!first && globalAABB.Size.Length() > 0.001f)
            {
                float maxDim = Mathf.Max(globalAABB.Size.X,
                    Mathf.Max(globalAABB.Size.Y, globalAABB.Size.Z));
                if (maxDim > 0.001f)
                {
                    float fitScale = 3f / maxDim;
                    _previewPivot.Scale = Vector3.One * fitScale;
                }

                // Recalculate AABB after scale
                globalAABB = new Aabb();
                first = true;
                CollectGlobalAABB(_previewModel, ref globalAABB, ref first);

                var center = globalAABB.GetCenter();
                var bottom = globalAABB.Position.Y;
                _previewPivot.Position = new Vector3(-center.X, -bottom, -center.Z);
            }

            _previewZoom = 6f;
            UpdatePreviewCamera();

            // Build clip buttons from actual animation clips
            RebuildAnimButtons();

            // Build inspector
            BuildInspector();
        }

        private static void CollectGlobalAABB(Node node, ref Aabb result, ref bool first)
        {
            if (node is MeshInstance3D mesh && mesh.Mesh != null)
            {
                var meshAabb = mesh.GetAabb();
                var globalTransform = mesh.GlobalTransform;
                var corners = new Vector3[8];
                corners[0] = globalTransform * new Vector3(meshAabb.Position.X, meshAabb.Position.Y, meshAabb.Position.Z);
                corners[1] = globalTransform * new Vector3(meshAabb.End.X, meshAabb.Position.Y, meshAabb.Position.Z);
                corners[2] = globalTransform * new Vector3(meshAabb.Position.X, meshAabb.End.Y, meshAabb.Position.Z);
                corners[3] = globalTransform * new Vector3(meshAabb.End.X, meshAabb.End.Y, meshAabb.Position.Z);
                corners[4] = globalTransform * new Vector3(meshAabb.Position.X, meshAabb.Position.Y, meshAabb.End.Z);
                corners[5] = globalTransform * new Vector3(meshAabb.End.X, meshAabb.Position.Y, meshAabb.End.Z);
                corners[6] = globalTransform * new Vector3(meshAabb.Position.X, meshAabb.End.Y, meshAabb.End.Z);
                corners[7] = globalTransform * new Vector3(meshAabb.End.X, meshAabb.End.Y, meshAabb.End.Z);

                foreach (var corner in corners)
                {
                    if (first)
                    {
                        result = new Aabb(corner, Vector3.Zero);
                        first = false;
                    }
                    else
                    {
                        result = result.Expand(corner);
                    }
                }
            }
            foreach (var child in node.GetChildren())
                CollectGlobalAABB(child, ref result, ref first);
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseMotion mm && Input.IsMouseButtonPressed(MouseButton.Left))
            {
                _orbitAngleX += mm.Relative.X * 0.4f;
                _orbitAngleY = Mathf.Clamp(_orbitAngleY + mm.Relative.Y * 0.4f, -80f, 80f);
                UpdatePreviewCamera();
            }
            else if (@event is InputEventMouseButton mb)
            {
                if (mb.ButtonIndex == MouseButton.WheelUp)
                {
                    _previewZoom = Mathf.Max(2f, _previewZoom - 0.5f);
                    UpdatePreviewCamera();
                }
                else if (mb.ButtonIndex == MouseButton.WheelDown)
                {
                    _previewZoom = Mathf.Min(20f, _previewZoom + 0.5f);
                    UpdatePreviewCamera();
                }
            }
        }

        private void UpdatePreviewCamera()
        {
            if (_previewCamera == null) return;
            float yawRad = Mathf.DegToRad(_orbitAngleX);
            float pitchRad = Mathf.DegToRad(_orbitAngleY);

            float x = _previewZoom * Mathf.Cos(pitchRad) * Mathf.Sin(yawRad);
            float y = _previewZoom * Mathf.Sin(-pitchRad) + 1.5f;
            float z = _previewZoom * Mathf.Cos(pitchRad) * Mathf.Cos(yawRad);

            _previewCamera.Position = new Vector3(x, y, z);
            _previewCamera.LookAt(new Vector3(0, 1, 0), Vector3.Up);
        }

        // ── Inspector ──

        private void BuildInspector()
        {
            foreach (var child in _inspector.GetChildren())
                child.QueueFree();

            if (_selectedCharIndex < 0) return;
            var def = Characters[_selectedCharIndex];

            // ── Character Info ──
            _inspector.AddChild(EditorStyles.MakeLabel(def.Name, 18, AccentColor));

            string roleBadge = def.Role switch {
                CharacterRole.Player => "PLAYER",
                CharacterRole.Companion => "COMPANION",
                CharacterRole.Enemy => "ENEMY",
                _ => "UNKNOWN"
            };
            var roleColor = def.Role switch {
                CharacterRole.Player => new Color(0.3f, 0.7f, 1f),
                CharacterRole.Companion => new Color(0.3f, 0.9f, 0.5f),
                CharacterRole.Enemy => new Color(0.9f, 0.3f, 0.3f),
                _ => EditorStyles.TextMuted
            };
            _inspector.AddChild(EditorStyles.MakeLabel(roleBadge, 12, roleColor));
            _inspector.AddChild(EditorStyles.MakeLabel(
                System.IO.Path.GetFileName(def.ModelPath), 11, EditorStyles.TextMuted));

            // AABB size
            if (_previewModel != null)
            {
                var aabb = new Aabb();
                bool first = true;
                CollectGlobalAABB(_previewModel, ref aabb, ref first);
                if (!first)
                {
                    var s = aabb.Size;
                    _inspector.AddChild(EditorStyles.MakeLabel(
                        $"Size: {s.X:F1} x {s.Y:F1} x {s.Z:F1}", 11, EditorStyles.TextSecondary));
                }
            }

            _inspector.AddChild(EditorStyles.MakeSeparator());

            // ── Combat Stats ──
            _inspector.AddChild(EditorStyles.MakeLabel("Combat Stats", 14, EditorStyles.TextPrimary));

            if (def.Role == CharacterRole.Player)
            {
                _inspector.AddChild(MakeStatRow("HP", $"{def.HP:F0}"));
                _inspector.AddChild(MakeStatRow("Mana", $"{Constants.VINE_PLAYER_MAX_MANA:F0}"));
                _inspector.AddChild(MakeStatRow("Move Speed", $"{def.Speed:F1}"));
                _inspector.AddChild(MakeStatRow("Atk Damage", $"{def.AttackDamage:F1}"));
                _inspector.AddChild(MakeStatRow("Atk Speed", $"{Constants.VINE_PLAYER_ATTACK_SPEED:F1}/s"));
                _inspector.AddChild(MakeStatRow("Atk Range", $"{def.AttackRange:F1}"));
            }
            else
            {
                _inspector.AddChild(MakeStatRow("HP", $"{def.HP:F0}"));
                _inspector.AddChild(MakeStatRow("Speed", $"{def.Speed:F1}"));
                _inspector.AddChild(MakeStatRow("Atk Range", $"{def.AttackRange:F1}"));
                _inspector.AddChild(MakeStatRow("Atk Damage", $"{def.AttackDamage:F1}"));
                _inspector.AddChild(MakeStatRow("Atk Interval", $"{def.AttackInterval:F1}s"));
                if (def.Faction.HasValue)
                    _inspector.AddChild(MakeStatRow("Faction", def.Faction.Value.ToString()));
            }

            _inspector.AddChild(EditorStyles.MakeSeparator());

            // ── Theme/Faction Picker ──
            _inspector.AddChild(EditorStyles.MakeLabel("Theme / Faction", 14, EditorStyles.TextPrimary));

            var factionPicker = new OptionButton();
            factionPicker.AddThemeFontSizeOverride("font_size", 13);
            foreach (var fname in FactionNames)
                factionPicker.AddItem(fname);
            factionPicker.Selected = _selectedFaction;
            factionPicker.ItemSelected += (long idx) => {
                _selectedFaction = (int)idx;
            };
            _inspector.AddChild(factionPicker);

            // Planet picker
            var planetPicker = new OptionButton();
            planetPicker.AddThemeFontSizeOverride("font_size", 13);
            planetPicker.AddItem("Grid Prime (Tron)");
            planetPicker.AddItem("Scrapyard (Rust/Metal)");
            planetPicker.Selected = PlanetTheme.Current is TronPlanetTheme ? 0 : 1;
            planetPicker.ItemSelected += (long idx) => {
                PlanetTheme.Current = idx == 0
                    ? new TronPlanetTheme()
                    : new ScrapyardPlanetTheme();
                ApplyThemeToPreview();
                BuildInspector();
            };
            _inspector.AddChild(planetPicker);

            var applyThemeBtn = EditorStyles.MakeButton("Apply Theme", 13, AccentColor);
            applyThemeBtn.Pressed += () => {
                ApplyThemeToPreview();
                BuildInspector();
            };
            _inspector.AddChild(applyThemeBtn);

            _inspector.AddChild(EditorStyles.MakeSeparator());

            // ── Scale / Height ──
            _inspector.AddChild(EditorStyles.MakeLabel("Scale / Height", 14, EditorStyles.TextPrimary));

            var scaleRow = new HBoxContainer();
            scaleRow.AddThemeConstantOverride("separation", 6);
            scaleRow.AddChild(EditorStyles.MakeLabel("Target Height:", 12));
            var heightSpin = EditorStyles.MakeSpinBox(
                _previewPivot?.Scale.X ?? 1f, 0.1f, 5f, 0.1f);
            heightSpin.CustomMinimumSize = new Vector2(80, 0);
            scaleRow.AddChild(heightSpin);
            _inspector.AddChild(scaleRow);

            var rescaleBtn = EditorStyles.MakeButton("Rescale", 12, AccentColor);
            rescaleBtn.Pressed += () => {
                if (_previewPivot != null)
                    _previewPivot.Scale = Vector3.One * (float)heightSpin.Value;
            };
            _inspector.AddChild(rescaleBtn);

            _inspector.AddChild(EditorStyles.MakeSeparator());

            // ── Weapon Attachment ──
            _inspector.AddChild(EditorStyles.MakeLabel("Weapon Attachment", 14, EditorStyles.TextPrimary));

            var weaponPicker = new OptionButton();
            weaponPicker.AddThemeFontSizeOverride("font_size", 13);
            foreach (var wname in WeaponNames)
                weaponPicker.AddItem(wname);
            weaponPicker.Selected = _weaponChoice;
            weaponPicker.ItemSelected += (long idx) => {
                _weaponChoice = (int)idx;
            };
            _inspector.AddChild(weaponPicker);

            // Position XYZ
            _inspector.AddChild(EditorStyles.MakeLabel("Position", 11, EditorStyles.TextSecondary));
            var posRow = BuildVector3Row(_weaponPos, v => _weaponPos = v);
            _inspector.AddChild(posRow);

            // Rotation XYZ
            _inspector.AddChild(EditorStyles.MakeLabel("Rotation", 11, EditorStyles.TextSecondary));
            var rotRow = BuildVector3Row(_weaponRotDeg, v => _weaponRotDeg = v, -180f, 180f, 5f);
            _inspector.AddChild(rotRow);

            // Scale XYZ
            _inspector.AddChild(EditorStyles.MakeLabel("Scale", 11, EditorStyles.TextSecondary));
            var sclRow = BuildVector3Row(_weaponScale, v => _weaponScale = v, 0.01f, 5f, 0.05f);
            _inspector.AddChild(sclRow);

            var weaponBtnRow = new HBoxContainer();
            weaponBtnRow.AddThemeConstantOverride("separation", 4);

            var attachBtn = EditorStyles.MakeButton("Attach", 12, EditorStyles.StatusOk);
            attachBtn.Pressed += () => {
                if (_weaponChoice > 0)
                    AttachWeapon(WeaponPaths[_weaponChoice], _weaponPos, _weaponRotDeg, _weaponScale);
            };
            weaponBtnRow.AddChild(attachBtn);

            var detachBtn = EditorStyles.MakeButton("Detach", 12, EditorStyles.StatusError);
            detachBtn.Pressed += DetachWeapon;
            weaponBtnRow.AddChild(detachBtn);
            _inspector.AddChild(weaponBtnRow);

            var savLoadRow = new HBoxContainer();
            savLoadRow.AddThemeConstantOverride("separation", 4);
            var saveBtn = EditorStyles.MakeButton("Save Config", 12, EditorStyles.StatusOk);
            saveBtn.Pressed += SaveWeaponConfig;
            savLoadRow.AddChild(saveBtn);

            var loadBtn = EditorStyles.MakeButton("Load Config", 12, AccentColor);
            loadBtn.Pressed += LoadWeaponConfig;
            savLoadRow.AddChild(loadBtn);
            _inspector.AddChild(savLoadRow);

            // ── Abilities (player only) ──
            if (def.Role == CharacterRole.Player)
            {
                _inspector.AddChild(EditorStyles.MakeSeparator());
                _inspector.AddChild(EditorStyles.MakeLabel("Abilities", 14, EditorStyles.TextPrimary));

                var abilities = VinePlayer.GetDefaultAbilities();
                string[] keys = { "Q", "E", "R" };
                for (int i = 0; i < abilities.Length && i < keys.Length; i++)
                {
                    var ab = abilities[i];
                    var abRow = new VBoxContainer();
                    abRow.AddThemeConstantOverride("separation", 1);

                    abRow.AddChild(EditorStyles.MakeLabel($"[{keys[i]}] {ab.Name}", 13, ab.IconColor));
                    abRow.AddChild(EditorStyles.MakeLabel(ab.Description, 11, EditorStyles.TextSecondary));
                    abRow.AddChild(EditorStyles.MakeLabel(
                        $"CD: {ab.Cooldown:F0}s  Mana: {ab.ManaCost:F0}  Range: {ab.Range:F0}",
                        10, EditorStyles.TextMuted));

                    _inspector.AddChild(abRow);
                }
            }

            // ── Animation Editor (always shown) ──
            BuildAnimationEditorSection();

            // ── Skeleton / Bone Inspector ──
            BuildSkeletonSection();
        }

        private void BuildInspectorError(CharacterDef def)
        {
            foreach (var child in _inspector.GetChildren())
                child.QueueFree();

            _inspector.AddChild(EditorStyles.MakeLabel($"Failed to load: {def.Name}", 16, EditorStyles.StatusError));
            _inspector.AddChild(EditorStyles.MakeLabel(def.ModelPath, 11, EditorStyles.TextMuted));
        }

        // ── Helper UI builders ──

        private static HBoxContainer MakeStatRow(string label, string value)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            var lbl = EditorStyles.MakeLabel(label + ":", 12, EditorStyles.TextSecondary);
            lbl.CustomMinimumSize = new Vector2(90, 0);
            row.AddChild(lbl);
            row.AddChild(EditorStyles.MakeLabel(value, 12, EditorStyles.TextPrimary));
            return row;
        }

        private static HBoxContainer BuildVector3Row(Vector3 initial,
            System.Action<Vector3> onChange,
            float min = -5f, float max = 5f, float step = 0.05f)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 4);

            var spinX = EditorStyles.MakeSpinBox(initial.X, min, max, step);
            spinX.CustomMinimumSize = new Vector2(70, 0);
            var spinY = EditorStyles.MakeSpinBox(initial.Y, min, max, step);
            spinY.CustomMinimumSize = new Vector2(70, 0);
            var spinZ = EditorStyles.MakeSpinBox(initial.Z, min, max, step);
            spinZ.CustomMinimumSize = new Vector2(70, 0);

            void Update() => onChange(new Vector3(
                (float)spinX.Value, (float)spinY.Value, (float)spinZ.Value));

            spinX.ValueChanged += _ => Update();
            spinY.ValueChanged += _ => Update();
            spinZ.ValueChanged += _ => Update();

            row.AddChild(EditorStyles.MakeLabel("X", 10, new Color(0.9f, 0.3f, 0.3f)));
            row.AddChild(spinX);
            row.AddChild(EditorStyles.MakeLabel("Y", 10, new Color(0.3f, 0.9f, 0.3f)));
            row.AddChild(spinY);
            row.AddChild(EditorStyles.MakeLabel("Z", 10, new Color(0.3f, 0.5f, 0.9f)));
            row.AddChild(spinZ);
            return row;
        }

        // ── Weapon Config Persistence ──

        private const string WEAPON_CONFIG_PATH = "res://Data/weapon_attachments.json";

        private void SaveWeaponConfig()
        {
            if (_selectedCharIndex < 0) return;
            var def = Characters[_selectedCharIndex];

            // Load existing config or create new
            var dict = new Godot.Collections.Dictionary();
            if (FileAccess.FileExists(WEAPON_CONFIG_PATH))
            {
                var file = FileAccess.Open(WEAPON_CONFIG_PATH, FileAccess.ModeFlags.Read);
                if (file != null)
                {
                    var json = new Json();
                    if (json.Parse(file.GetAsText()) == Error.Ok && json.Data.Obj is Godot.Collections.Dictionary existing)
                        dict = existing;
                    file.Close();
                }
            }

            var entry = new Godot.Collections.Dictionary();
            entry["WeaponPath"] = _weaponChoice > 0 ? WeaponPaths[_weaponChoice] : "";
            entry["PosX"] = _weaponPos.X;
            entry["PosY"] = _weaponPos.Y;
            entry["PosZ"] = _weaponPos.Z;
            entry["RotX"] = _weaponRotDeg.X;
            entry["RotY"] = _weaponRotDeg.Y;
            entry["RotZ"] = _weaponRotDeg.Z;
            entry["ScaleX"] = _weaponScale.X;
            entry["ScaleY"] = _weaponScale.Y;
            entry["ScaleZ"] = _weaponScale.Z;
            dict[def.Name] = entry;

            var saveFile = FileAccess.Open(WEAPON_CONFIG_PATH, FileAccess.ModeFlags.Write);
            if (saveFile != null)
            {
                saveFile.StoreString(Json.Stringify(dict, "\t"));
                saveFile.Close();
                SetStatus($"Saved weapon config for {def.Name}");
            }
            else
            {
                SetStatus("Failed to save weapon config");
            }
        }

        private void LoadWeaponConfig()
        {
            if (_selectedCharIndex < 0) return;
            var def = Characters[_selectedCharIndex];

            if (!FileAccess.FileExists(WEAPON_CONFIG_PATH))
            {
                SetStatus("No weapon config file found");
                return;
            }

            var file = FileAccess.Open(WEAPON_CONFIG_PATH, FileAccess.ModeFlags.Read);
            if (file == null) return;

            var json = new Json();
            if (json.Parse(file.GetAsText()) != Error.Ok)
            {
                file.Close();
                SetStatus("Failed to parse weapon config");
                return;
            }
            file.Close();

            if (json.Data.Obj is not Godot.Collections.Dictionary dict) return;
            if (!dict.ContainsKey(def.Name))
            {
                SetStatus($"No config for {def.Name}");
                return;
            }

            if (dict[def.Name].Obj is not Godot.Collections.Dictionary entry) return;

            string weaponPath = entry.ContainsKey("WeaponPath") ? (string)entry["WeaponPath"] : "";
            _weaponPos = new Vector3(
                entry.ContainsKey("PosX") ? (float)(double)entry["PosX"] : 0.2f,
                entry.ContainsKey("PosY") ? (float)(double)entry["PosY"] : 0.4f,
                entry.ContainsKey("PosZ") ? (float)(double)entry["PosZ"] : 0.3f
            );
            _weaponRotDeg = new Vector3(
                entry.ContainsKey("RotX") ? (float)(double)entry["RotX"] : 0f,
                entry.ContainsKey("RotY") ? (float)(double)entry["RotY"] : 0f,
                entry.ContainsKey("RotZ") ? (float)(double)entry["RotZ"] : 0f
            );
            _weaponScale = new Vector3(
                entry.ContainsKey("ScaleX") ? (float)(double)entry["ScaleX"] : 0.3f,
                entry.ContainsKey("ScaleY") ? (float)(double)entry["ScaleY"] : 0.3f,
                entry.ContainsKey("ScaleZ") ? (float)(double)entry["ScaleZ"] : 0.3f
            );

            // Find weapon choice index
            _weaponChoice = 0;
            if (!string.IsNullOrEmpty(weaponPath))
            {
                for (int i = 1; i < WeaponPaths.Length; i++)
                {
                    if (WeaponPaths[i] == weaponPath) { _weaponChoice = i; break; }
                }
            }

            // Apply weapon
            if (_weaponChoice > 0)
                AttachWeapon(WeaponPaths[_weaponChoice], _weaponPos, _weaponRotDeg, _weaponScale);

            // Rebuild inspector to reflect loaded values
            BuildInspector();
            SetStatus($"Loaded weapon config for {def.Name}");
        }

        // ── Skeleton / Bone Inspector ──

        private static Skeleton3D FindSkeletonInTree(Node root)
        {
            if (root is Skeleton3D s) return s;
            foreach (var child in root.GetChildren())
            {
                var found = FindSkeletonInTree(child);
                if (found != null) return found;
            }
            return null;
        }

        private void BuildSkeletonSection()
        {
            // Find skeleton
            _previewSkeleton = _previewModel != null ? FindSkeletonInTree(_previewModel) : null;

            _inspector.AddChild(EditorStyles.MakeSeparator());
            _inspector.AddChild(EditorStyles.MakeLabel("Skeleton / Bones", 14, new Color(0.5f, 0.8f, 1f)));

            if (_previewSkeleton == null)
            {
                _inspector.AddChild(EditorStyles.MakeLabel("No Skeleton3D found", 11, EditorStyles.TextMuted));
                return;
            }

            int boneCount = _previewSkeleton.GetBoneCount();
            _inspector.AddChild(EditorStyles.MakeLabel(
                $"{boneCount} bones in '{_previewSkeleton.Name}'", 11, EditorStyles.TextSecondary));

            // Reset all overrides button
            var resetBtn = EditorStyles.MakeButton("Reset All Bone Poses", 12, EditorStyles.StatusWarn);
            resetBtn.Pressed += () => {
                _boneOverrides.Clear();
                if (_previewSkeleton != null)
                {
                    for (int b = 0; b < _previewSkeleton.GetBoneCount(); b++)
                        _previewSkeleton.ResetBonePose(b);
                }
                SetStatus("All bone overrides cleared");
            };
            _inspector.AddChild(resetBtn);

            // Save/Load bone pose buttons
            var boneIORow = new HBoxContainer();
            boneIORow.AddThemeConstantOverride("separation", 4);
            var saveBoneBtn = EditorStyles.MakeButton("Save Pose", 11, EditorStyles.StatusOk);
            saveBoneBtn.Pressed += SaveBonePose;
            boneIORow.AddChild(saveBoneBtn);
            var loadBoneBtn = EditorStyles.MakeButton("Load Pose", 11, AccentColor);
            loadBoneBtn.Pressed += () => { LoadBonePose(); BuildInspector(); };
            boneIORow.AddChild(loadBoneBtn);
            _inspector.AddChild(boneIORow);

            // Per-bone controls
            for (int b = 0; b < boneCount; b++)
            {
                int boneIdx = b;
                string boneName = _previewSkeleton.GetBoneName(boneIdx);
                int parentIdx = _previewSkeleton.GetBoneParent(boneIdx);
                string parentName = parentIdx >= 0 ? _previewSkeleton.GetBoneName(parentIdx) : "root";

                // Current override (or zero)
                Vector3 currentRot = _boneOverrides.ContainsKey(boneIdx) ? _boneOverrides[boneIdx] : Vector3.Zero;

                // Bone header
                var boneHeader = new HBoxContainer();
                boneHeader.AddThemeConstantOverride("separation", 4);
                boneHeader.AddChild(EditorStyles.MakeLabel(
                    $"[{boneIdx}] {boneName}", 11,
                    _boneOverrides.ContainsKey(boneIdx) ? AccentColor : EditorStyles.TextPrimary));
                boneHeader.AddChild(EditorStyles.MakeLabel(
                    $"(parent: {parentName})", 9, EditorStyles.TextMuted));
                _inspector.AddChild(boneHeader);

                // Rotation override row: X Y Z spinboxes
                var rotRow = new HBoxContainer();
                rotRow.AddThemeConstantOverride("separation", 2);

                var spinX = EditorStyles.MakeSpinBox(currentRot.X, -180f, 180f, 1f);
                spinX.CustomMinimumSize = new Vector2(65, 0);
                var spinY = EditorStyles.MakeSpinBox(currentRot.Y, -180f, 180f, 1f);
                spinY.CustomMinimumSize = new Vector2(65, 0);
                var spinZ = EditorStyles.MakeSpinBox(currentRot.Z, -180f, 180f, 1f);
                spinZ.CustomMinimumSize = new Vector2(65, 0);

                void ApplyOverride()
                {
                    var rot = new Vector3((float)spinX.Value, (float)spinY.Value, (float)spinZ.Value);
                    _boneOverrides[boneIdx] = rot;
                    if (_previewSkeleton != null && boneIdx < _previewSkeleton.GetBoneCount())
                    {
                        var q = Quaternion.FromEuler(new Vector3(
                            Mathf.DegToRad(rot.X),
                            Mathf.DegToRad(rot.Y),
                            Mathf.DegToRad(rot.Z)));
                        _previewSkeleton.SetBonePoseRotation(boneIdx, q);
                    }
                }

                spinX.ValueChanged += _ => ApplyOverride();
                spinY.ValueChanged += _ => ApplyOverride();
                spinZ.ValueChanged += _ => ApplyOverride();

                rotRow.AddChild(EditorStyles.MakeLabel("X", 9, new Color(0.9f, 0.3f, 0.3f)));
                rotRow.AddChild(spinX);
                rotRow.AddChild(EditorStyles.MakeLabel("Y", 9, new Color(0.3f, 0.9f, 0.3f)));
                rotRow.AddChild(spinY);
                rotRow.AddChild(EditorStyles.MakeLabel("Z", 9, new Color(0.3f, 0.5f, 0.9f)));
                rotRow.AddChild(spinZ);

                // Clear button for this bone
                var clearBtn = EditorStyles.MakeButton("X", 9, EditorStyles.StatusError);
                clearBtn.CustomMinimumSize = new Vector2(20, 20);
                clearBtn.Pressed += () => {
                    _boneOverrides.Remove(boneIdx);
                    if (_previewSkeleton != null && boneIdx < _previewSkeleton.GetBoneCount())
                        _previewSkeleton.ResetBonePose(boneIdx);
                    spinX.SetValueNoSignal(0);
                    spinY.SetValueNoSignal(0);
                    spinZ.SetValueNoSignal(0);
                };
                rotRow.AddChild(clearBtn);

                _inspector.AddChild(rotRow);
            }
        }

        // ── Bone Pose Persistence ──

        private const string BONE_POSE_PATH = "res://Data/bone_poses.json";

        private void SaveBonePose()
        {
            if (_selectedCharIndex < 0 || _boneOverrides.Count == 0) return;
            var def = Characters[_selectedCharIndex];

            var dict = new Godot.Collections.Dictionary();
            if (FileAccess.FileExists(BONE_POSE_PATH))
            {
                var file = FileAccess.Open(BONE_POSE_PATH, FileAccess.ModeFlags.Read);
                if (file != null)
                {
                    var json = new Json();
                    if (json.Parse(file.GetAsText()) == Error.Ok && json.Data.Obj is Godot.Collections.Dictionary existing)
                        dict = existing;
                    file.Close();
                }
            }

            var boneArray = new Godot.Collections.Array();
            foreach (var (boneIdx, rot) in _boneOverrides)
            {
                string boneName = _previewSkeleton != null && boneIdx < _previewSkeleton.GetBoneCount()
                    ? _previewSkeleton.GetBoneName(boneIdx) : $"bone_{boneIdx}";
                var entry = new Godot.Collections.Dictionary();
                entry["Index"] = boneIdx;
                entry["Name"] = boneName;
                entry["RotX"] = rot.X;
                entry["RotY"] = rot.Y;
                entry["RotZ"] = rot.Z;
                boneArray.Add(entry);
            }
            dict[def.Name] = boneArray;

            var saveFile = FileAccess.Open(BONE_POSE_PATH, FileAccess.ModeFlags.Write);
            if (saveFile != null)
            {
                saveFile.StoreString(Json.Stringify(dict, "\t"));
                saveFile.Close();
                SetStatus($"Saved {_boneOverrides.Count} bone overrides for {def.Name}");
            }
        }

        private void LoadBonePose()
        {
            if (_selectedCharIndex < 0) return;
            var def = Characters[_selectedCharIndex];

            if (!FileAccess.FileExists(BONE_POSE_PATH)) { SetStatus("No bone pose file"); return; }

            var file = FileAccess.Open(BONE_POSE_PATH, FileAccess.ModeFlags.Read);
            if (file == null) return;

            var json = new Json();
            if (json.Parse(file.GetAsText()) != Error.Ok) { file.Close(); return; }
            file.Close();

            if (json.Data.Obj is not Godot.Collections.Dictionary dict) return;
            if (!dict.ContainsKey(def.Name)) { SetStatus($"No bone pose for {def.Name}"); return; }

            if (dict[def.Name].Obj is not Godot.Collections.Array boneArray) return;

            _boneOverrides.Clear();
            foreach (var item in boneArray)
            {
                if (item.Obj is not Godot.Collections.Dictionary entry) continue;
                int boneIdx = entry.ContainsKey("Index") ? (int)(long)entry["Index"] : -1;
                if (boneIdx < 0) continue;

                var rot = new Vector3(
                    entry.ContainsKey("RotX") ? (float)(double)entry["RotX"] : 0f,
                    entry.ContainsKey("RotY") ? (float)(double)entry["RotY"] : 0f,
                    entry.ContainsKey("RotZ") ? (float)(double)entry["RotZ"] : 0f
                );
                _boneOverrides[boneIdx] = rot;

                // Apply to skeleton
                if (_previewSkeleton != null && boneIdx < _previewSkeleton.GetBoneCount())
                {
                    var q = Quaternion.FromEuler(new Vector3(
                        Mathf.DegToRad(rot.X),
                        Mathf.DegToRad(rot.Y),
                        Mathf.DegToRad(rot.Z)));
                    _previewSkeleton.SetBonePoseRotation(boneIdx, q);
                }
            }

            SetStatus($"Loaded {_boneOverrides.Count} bone overrides for {def.Name}");
        }

        // ── Animation Timeline / Clipper ──

        public override void _Process(double delta)
        {
            if (!_isScrubbingRaw || _rawAnimPlayer == null) return;

            if (_scrubPlaying && _rawAnimPlayer.IsPlaying())
            {
                float pos = (float)_rawAnimPlayer.CurrentAnimationPosition;
                if (_scrubSlider != null)
                    _scrubSlider.SetValueNoSignal(pos);
                if (_scrubTimeLabel != null)
                    _scrubTimeLabel.Text = $"{pos:F3}s / {_rawAnimLength:F2}s";
            }
        }

        /// <summary>
        /// Find the raw AnimationPlayer and its monolithic animation.
        /// Returns true if found, false if animations were already split or no player exists.
        /// </summary>
        private bool FindRawAnimation()
        {
            if (_previewModel == null) return false;

            _rawAnimPlayer = FindAnimPlayerInTree(_previewModel);
            if (_rawAnimPlayer == null) return false;

            // Look for the monolithic animation name
            _rawAnimName = null;
            _rawAnimLength = 0;
            float longestLength = 0;

            foreach (var name in _rawAnimPlayer.GetAnimationList())
            {
                var anim = _rawAnimPlayer.GetAnimation(name);
                if (anim == null) continue;
                float len = (float)anim.Length;

                // The monolithic anim is usually the longest one, or named "Action"/"Armature"
                string lower = name.ToLower();
                if (lower.Contains("action") || lower.Contains("armature"))
                {
                    _rawAnimName = name;
                    _rawAnimLength = len;
                    return true;
                }

                if (len > longestLength)
                {
                    longestLength = len;
                    _rawAnimName = name;
                    _rawAnimLength = len;
                }
            }

            // If all clips are short (already split), offer to play any of them
            // but mark as "no raw monolithic found"
            if (_rawAnimLength < 1f)
            {
                _rawAnimName = null;
                return false;
            }

            return _rawAnimName != null;
        }

        private static AnimationPlayer FindAnimPlayerInTree(Node root)
        {
            if (root is AnimationPlayer ap) return ap;
            foreach (var child in root.GetChildren())
            {
                var found = FindAnimPlayerInTree(child);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// Load a fresh model WITHOUT splitting animations, for raw timeline access.
        /// </summary>
        private void LoadRawForScrubbing()
        {
            if (_selectedCharIndex < 0) return;
            var def = Characters[_selectedCharIndex];

            // Remember pivot transform
            var pivotScale = _previewPivot?.Scale ?? Vector3.One;
            var pivotPos = _previewPivot?.Position ?? Vector3.Zero;

            // Clear old model
            if (_previewPivot != null)
            {
                _previewPivot.QueueFree();
                _previewPivot = null;
                _previewModel = null;
                _weaponModel = null;
            }
            if (_animator != null)
            {
                _animator.QueueFree();
                _animator = null;
            }

            // Load fresh — NO splitting
            var model = AssetLibrary.InstantiateNormalized(def.ModelPath);
            if (model == null) { SetStatus("Failed to load raw model"); return; }

            _previewPivot = new Node3D();
            _previewPivot.Scale = pivotScale;
            _previewPivot.Position = pivotPos;
            _previewRoot.AddChild(_previewPivot);
            _previewPivot.AddChild(model);
            _previewModel = model;

            // Apply theme so it's visible
            if (_selectedFaction < 5)
            {
                var theme = PlanetTheme.Current;
                if (_selectedFaction == 0)
                    theme.ApplyToNode(_previewModel, theme.PlayerPrimary);
                else
                {
                    var faction = _selectedFaction switch {
                        1 => VineEnemyFaction.Scavenger,
                        2 => VineEnemyFaction.Brute,
                        3 => VineEnemyFaction.Swarm,
                        4 => VineEnemyFaction.Ghost,
                        _ => VineEnemyFaction.Scavenger
                    };
                    theme.ApplyEnemyTheme(_previewModel, faction);
                }
            }

            // Find raw animation
            if (!FindRawAnimation())
            {
                SetStatus("No monolithic animation found — already split clips only");
                _isScrubbingRaw = false;

                // List available clips
                _rawAnimPlayer = FindAnimPlayerInTree(_previewModel);
                if (_rawAnimPlayer != null)
                {
                    var clipList = string.Join(", ", _rawAnimPlayer.GetAnimationList());
                    GD.Print($"[CharViewer] Available clips: {clipList}");
                    SetStatus($"Clips: {clipList}");
                }
                return;
            }

            _isScrubbingRaw = true;
            _scrubPlaying = false;

            // Pause at frame 0
            _rawAnimPlayer.Play(_rawAnimName);
            _rawAnimPlayer.Pause();
            _rawAnimPlayer.Seek(0);

            // Auto-detect segments
            AutoDetectSegments();

            // Count tracks
            var sourceAnim = _rawAnimPlayer.GetAnimation(_rawAnimName);
            int trackCount = sourceAnim?.GetTrackCount() ?? 0;
            if (_scrubTrackInfo != null)
                _scrubTrackInfo.Text = $"'{_rawAnimName}': {_rawAnimLength:F2}s, {trackCount} tracks";

            SetStatus($"Raw mode: {_rawAnimName} ({_rawAnimLength:F2}s)");
            GD.Print($"[CharViewer] Raw scrub: {_rawAnimName}, {_rawAnimLength:F2}s, {trackCount} tracks");
        }

        private void AutoDetectSegments()
        {
            _segments.Clear();

            if (_selectedCharIndex >= 0 && (Characters[_selectedCharIndex].Name == "BIT" || Characters[_selectedCharIndex].Name == "Robot 1"))
            {
                // BIT has known hardcoded segments
                _segments.Add(new AnimSegment { Name = "Idle",     Start = 0f,    End = 3.17f, Loop = true });
                _segments.Add(new AnimSegment { Name = "Run",      Start = 3.17f, End = 4.13f, Loop = true });
                _segments.Add(new AnimSegment { Name = "Attack_R", Start = 4.13f, End = 5.07f, Loop = false });
                _segments.Add(new AnimSegment { Name = "Attack_L", Start = 5.07f, End = 5.90f, Loop = false });
                _segments.Add(new AnimSegment { Name = "Attack",   Start = 5.90f, End = 6.73f, Loop = false });
                _segments.Add(new AnimSegment { Name = "Death",    Start = 6.73f, End = _rawAnimLength, Loop = false });
            }
            else
            {
                // Try gap detection (same algorithm as CharacterAnimator.SplitMonolithicAnimation)
                var sourceAnim = _rawAnimPlayer?.GetAnimation(_rawAnimName);
                if (sourceAnim == null) return;

                int trackCount = sourceAnim.GetTrackCount();
                var gapTimes = new List<float>();
                for (int t = 0; t < Mathf.Min(trackCount, 5); t++)
                {
                    int keyCount = sourceAnim.TrackGetKeyCount(t);
                    if (keyCount < 4) continue;
                    float avgDelta = _rawAnimLength / keyCount;
                    float prevTime = (float)sourceAnim.TrackGetKeyTime(t, 0);
                    for (int k = 1; k < keyCount; k++)
                    {
                        float time = (float)sourceAnim.TrackGetKeyTime(t, k);
                        float delta = time - prevTime;
                        if (delta > avgDelta * 3f)
                        {
                            bool dup = false;
                            foreach (float g in gapTimes)
                                if (Mathf.Abs(g - prevTime) < 0.1f) { dup = true; break; }
                            if (!dup) gapTimes.Add(prevTime);
                        }
                        prevTime = time;
                    }
                }
                gapTimes.Sort();

                var boundaries = new List<float> { 0f };
                boundaries.AddRange(gapTimes);
                boundaries.Add(_rawAnimLength);

                var defaultNames = new[] { "Idle", "Walk", "Attack", "Hit", "Death" };
                int segCount = Mathf.Min(boundaries.Count - 1, defaultNames.Length + 5);
                for (int s = 0; s < segCount; s++)
                {
                    string name = s < defaultNames.Length ? defaultNames[s] : $"Clip_{s}";
                    bool loop = name == "Idle" || name == "Walk" || name == "Run";
                    _segments.Add(new AnimSegment {
                        Name = name,
                        Start = boundaries[s],
                        End = boundaries[s + 1],
                        Loop = loop
                    });
                }
            }
        }

        private void SeekRawAnimation(float time)
        {
            if (_rawAnimPlayer == null || string.IsNullOrEmpty(_rawAnimName)) return;

            if (!_rawAnimPlayer.IsPlaying())
            {
                _rawAnimPlayer.Play(_rawAnimName);
                _rawAnimPlayer.Pause();
            }
            _rawAnimPlayer.Seek(time);

            if (_scrubTimeLabel != null)
                _scrubTimeLabel.Text = $"{time:F3}s / {_rawAnimLength:F2}s";
        }

        /// <summary>
        /// Apply the current segment list to re-split the animation.
        /// Creates named clips from the segment boundaries.
        /// </summary>
        private void ApplyResplit()
        {
            if (_rawAnimPlayer == null || string.IsNullOrEmpty(_rawAnimName)) return;

            var sourceAnim = _rawAnimPlayer.GetAnimation(_rawAnimName);
            if (sourceAnim == null) return;

            int trackCount = sourceAnim.GetTrackCount();

            AnimationLibrary lib;
            if (_rawAnimPlayer.HasAnimationLibrary(""))
                lib = _rawAnimPlayer.GetAnimationLibrary("");
            else { lib = new AnimationLibrary(); _rawAnimPlayer.AddAnimationLibrary("", lib); }

            foreach (var seg in _segments)
            {
                var clip = new Animation();
                clip.Length = seg.End - seg.Start;

                for (int t = 0; t < trackCount; t++)
                {
                    var trackType = sourceAnim.TrackGetType(t);
                    if (trackType == Animation.TrackType.Scale3D) continue;
                    int newIdx = clip.AddTrack(trackType);
                    clip.TrackSetPath(newIdx, sourceAnim.TrackGetPath(t));
                    clip.TrackSetInterpolationType(newIdx, sourceAnim.TrackGetInterpolationType(t));
                    int keyCount = sourceAnim.TrackGetKeyCount(t);
                    for (int k = 0; k < keyCount; k++)
                    {
                        float keyTime = (float)sourceAnim.TrackGetKeyTime(t, k);
                        if (keyTime < seg.Start || keyTime >= seg.End - 0.01f) continue;
                        clip.TrackInsertKey(newIdx, keyTime - seg.Start, sourceAnim.TrackGetKeyValue(t, k));
                    }
                }

                if (seg.Loop)
                {
                    clip.LoopMode = Animation.LoopModeEnum.Linear;
                    // Duplicate first keyframe at end for smooth looping
                    for (int ti = 0; ti < clip.GetTrackCount(); ti++)
                    {
                        if (clip.TrackGetKeyCount(ti) > 0)
                        {
                            var firstKey = clip.TrackGetKeyValue(ti, 0);
                            clip.TrackInsertKey(ti, clip.Length, firstKey);
                        }
                    }
                }

                if (lib.HasAnimation(seg.Name)) lib.RemoveAnimation(seg.Name);
                lib.AddAnimation(seg.Name, clip);
                GD.Print($"[CharViewer] Split: {seg.Name} = {seg.Start:F2}s-{seg.End:F2}s ({clip.Length:F2}s) loop={seg.Loop}");
            }

            // Re-init animator with the new clips
            _isScrubbingRaw = false;
            _scrubPlaying = false;

            if (_animator != null) { _animator.QueueFree(); _animator = null; }
            _animator = new CharacterAnimator();
            _previewRoot.AddChild(_animator);
            _animator.Initialize(_previewModel);
            _animator.SetState(AnimState.Idle);

            SetStatus($"Re-split: {_segments.Count} clips applied");
            RebuildAnimButtons();
            BuildInspector();
        }

        /// <summary>
        /// Play just a specific segment of the raw animation (preview one clip).
        /// </summary>
        private void PreviewSegment(int index)
        {
            if (index < 0 || index >= _segments.Count) return;
            if (_rawAnimPlayer == null) return;

            var seg = _segments[index];
            _rawAnimPlayer.Play(_rawAnimName);
            _rawAnimPlayer.Seek(seg.Start);
            _rawAnimPlayer.SpeedScale = 1f;
            _scrubPlaying = true;

            // Stop at end of segment via a timer
            var timer = GetTree().CreateTimer(seg.End - seg.Start);
            timer.Timeout += () => {
                if (_rawAnimPlayer != null && _isScrubbingRaw)
                {
                    if (seg.Loop)
                    {
                        // Loop: restart from segment start
                        _rawAnimPlayer.Seek(seg.Start);
                    }
                    else
                    {
                        _rawAnimPlayer.Pause();
                        _scrubPlaying = false;
                    }
                }
            };

            SetStatus($"Preview: {seg.Name} ({seg.Start:F2}-{seg.End:F2}s)");
        }

        /// <summary>
        /// Build the Animation Editor section in the inspector.
        /// </summary>
        private void BuildAnimationEditorSection()
        {
            _inspector.AddChild(EditorStyles.MakeSeparator());
            _inspector.AddChild(EditorStyles.MakeLabel("Animation Editor", 14, new Color(1f, 0.5f, 0.2f)));

            // Load Raw / Return to Split buttons
            var modeRow = new HBoxContainer();
            modeRow.AddThemeConstantOverride("separation", 4);

            var loadRawBtn = EditorStyles.MakeButton(
                _isScrubbingRaw ? "Return to Split" : "Load Raw Timeline",
                12, _isScrubbingRaw ? EditorStyles.StatusWarn : new Color(1f, 0.5f, 0.2f));
            loadRawBtn.Pressed += () => {
                if (_isScrubbingRaw)
                {
                    // Return to split mode — reload and split
                    _isScrubbingRaw = false;
                    SelectCharacter(_selectedCharIndex);
                }
                else
                {
                    LoadRawForScrubbing();
                    BuildInspector();
                }
            };
            modeRow.AddChild(loadRawBtn);
            _inspector.AddChild(modeRow);

            if (!_isScrubbingRaw)
            {
                // Show available clips
                if (_animator?.AnimPlayer != null)
                {
                    _inspector.AddChild(EditorStyles.MakeLabel("Available Clips:", 11, EditorStyles.TextSecondary));
                    foreach (var clipName in _animator.AnimPlayer.GetAnimationList())
                    {
                        var anim = _animator.AnimPlayer.GetAnimation(clipName);
                        float len = anim != null ? (float)anim.Length : 0f;
                        string loopStr = anim?.LoopMode == Animation.LoopModeEnum.Linear ? " [loop]" : "";
                        var clipRow = new HBoxContainer();
                        clipRow.AddThemeConstantOverride("separation", 4);

                        var playBtn = EditorStyles.MakeButton(">", 11, EditorStyles.StatusOk);
                        playBtn.CustomMinimumSize = new Vector2(24, 20);
                        string cn = clipName; // capture
                        playBtn.Pressed += () => {
                            _animator?.PlayCustom(cn);
                            SetStatus($"Playing: {cn}");
                        };
                        clipRow.AddChild(playBtn);
                        clipRow.AddChild(EditorStyles.MakeLabel(
                            $"{clipName}: {len:F2}s{loopStr}", 11, EditorStyles.TextPrimary));
                        _inspector.AddChild(clipRow);
                    }
                }
                return;
            }

            // ── Raw Timeline Scrubber ──

            // Track info
            _scrubTrackInfo = EditorStyles.MakeLabel("", 11, EditorStyles.TextSecondary);
            if (_rawAnimPlayer != null && !string.IsNullOrEmpty(_rawAnimName))
            {
                var sourceAnim = _rawAnimPlayer.GetAnimation(_rawAnimName);
                int trackCount = sourceAnim?.GetTrackCount() ?? 0;
                _scrubTrackInfo.Text = $"'{_rawAnimName}': {_rawAnimLength:F2}s, {trackCount} tracks";
            }
            _inspector.AddChild(_scrubTrackInfo);

            // Scrub slider
            _scrubSlider = new HSlider();
            _scrubSlider.MinValue = 0;
            _scrubSlider.MaxValue = _rawAnimLength;
            _scrubSlider.Step = 0.001;
            _scrubSlider.Value = 0;
            _scrubSlider.CustomMinimumSize = new Vector2(0, 20);
            _scrubSlider.ValueChanged += (double v) => {
                if (!_scrubPlaying)
                    SeekRawAnimation((float)v);
            };
            _inspector.AddChild(_scrubSlider);

            // Time readout
            _scrubTimeLabel = EditorStyles.MakeLabel("0.000s / 0.00s", 12, AccentColor);
            _scrubTimeLabel.Text = $"0.000s / {_rawAnimLength:F2}s";
            _inspector.AddChild(_scrubTimeLabel);

            // Transport controls
            var transportRow = new HBoxContainer();
            transportRow.AddThemeConstantOverride("separation", 4);

            var playPauseBtn = EditorStyles.MakeButton(_scrubPlaying ? "||" : ">", 14, EditorStyles.StatusOk);
            playPauseBtn.CustomMinimumSize = new Vector2(30, 28);
            playPauseBtn.Pressed += () => {
                _scrubPlaying = !_scrubPlaying;
                if (_scrubPlaying)
                {
                    _rawAnimPlayer?.Play(_rawAnimName);
                    float seekTo = _scrubSlider != null ? (float)_scrubSlider.Value : 0f;
                    _rawAnimPlayer?.Seek(seekTo);
                    playPauseBtn.Text = "||";
                }
                else
                {
                    _rawAnimPlayer?.Pause();
                    playPauseBtn.Text = ">";
                }
            };
            transportRow.AddChild(playPauseBtn);

            var stopBtn = EditorStyles.MakeButton("Stop", 12, EditorStyles.StatusError);
            stopBtn.CustomMinimumSize = new Vector2(40, 28);
            stopBtn.Pressed += () => {
                _scrubPlaying = false;
                _rawAnimPlayer?.Pause();
                SeekRawAnimation(0);
                if (_scrubSlider != null) _scrubSlider.SetValueNoSignal(0);
            };
            transportRow.AddChild(stopBtn);

            // Speed control
            transportRow.AddChild(EditorStyles.MakeLabel("Spd:", 11));
            var scrubSpeed = EditorStyles.MakeSpinBox(1f, 0.1f, 3f, 0.1f);
            scrubSpeed.CustomMinimumSize = new Vector2(60, 0);
            scrubSpeed.ValueChanged += (double v) => {
                if (_rawAnimPlayer != null) _rawAnimPlayer.SpeedScale = (float)v;
            };
            transportRow.AddChild(scrubSpeed);
            _inspector.AddChild(transportRow);

            _inspector.AddChild(EditorStyles.MakeSeparator());

            // ── Segment List ──
            _inspector.AddChild(EditorStyles.MakeLabel("Clip Segments", 13, new Color(1f, 0.5f, 0.2f)));
            _inspector.AddChild(EditorStyles.MakeLabel(
                "Adjust start/end times, then Re-split", 10, EditorStyles.TextMuted));

            for (int i = 0; i < _segments.Count; i++)
            {
                int idx = i;
                var seg = _segments[idx];

                // Segment header row: name + play preview
                var headerRow = new HBoxContainer();
                headerRow.AddThemeConstantOverride("separation", 4);

                var previewBtn = EditorStyles.MakeButton(">", 11, EditorStyles.StatusOk);
                previewBtn.CustomMinimumSize = new Vector2(22, 22);
                previewBtn.Pressed += () => PreviewSegment(idx);
                headerRow.AddChild(previewBtn);

                // Seek to start button
                var seekBtn = EditorStyles.MakeButton("|<", 10, EditorStyles.TextSecondary);
                seekBtn.CustomMinimumSize = new Vector2(22, 22);
                seekBtn.Pressed += () => {
                    _scrubPlaying = false;
                    _rawAnimPlayer?.Pause();
                    SeekRawAnimation(_segments[idx].Start);
                    if (_scrubSlider != null) _scrubSlider.SetValueNoSignal(_segments[idx].Start);
                };
                headerRow.AddChild(seekBtn);

                var nameEdit = EditorStyles.MakeLineEdit(seg.Name, 11);
                nameEdit.Text = seg.Name;
                nameEdit.CustomMinimumSize = new Vector2(70, 22);
                nameEdit.TextChanged += (string text) => {
                    var s = _segments[idx];
                    s.Name = text;
                    _segments[idx] = s;
                };
                headerRow.AddChild(nameEdit);

                // Loop toggle
                var loopBtn = EditorStyles.MakeButton(seg.Loop ? "L" : "-", 10,
                    seg.Loop ? EditorStyles.StatusOk : EditorStyles.TextMuted);
                loopBtn.CustomMinimumSize = new Vector2(22, 22);
                loopBtn.TooltipText = "Toggle looping";
                loopBtn.Pressed += () => {
                    var s = _segments[idx];
                    s.Loop = !s.Loop;
                    _segments[idx] = s;
                    loopBtn.Text = s.Loop ? "L" : "-";
                    loopBtn.AddThemeColorOverride("font_color",
                        s.Loop ? EditorStyles.StatusOk : EditorStyles.TextMuted);
                };
                headerRow.AddChild(loopBtn);

                _inspector.AddChild(headerRow);

                // Time row: start and end spinboxes
                var timeRow = new HBoxContainer();
                timeRow.AddThemeConstantOverride("separation", 4);

                timeRow.AddChild(EditorStyles.MakeLabel("In:", 10, EditorStyles.TextMuted));
                var startSpin = EditorStyles.MakeSpinBox(seg.Start, 0, _rawAnimLength, 0.01f);
                startSpin.CustomMinimumSize = new Vector2(75, 0);
                startSpin.ValueChanged += (double v) => {
                    var s = _segments[idx];
                    s.Start = (float)v;
                    _segments[idx] = s;
                };
                timeRow.AddChild(startSpin);

                timeRow.AddChild(EditorStyles.MakeLabel("Out:", 10, EditorStyles.TextMuted));
                var endSpin = EditorStyles.MakeSpinBox(seg.End, 0, _rawAnimLength, 0.01f);
                endSpin.CustomMinimumSize = new Vector2(75, 0);
                endSpin.ValueChanged += (double v) => {
                    var s = _segments[idx];
                    s.End = (float)v;
                    _segments[idx] = s;
                };
                timeRow.AddChild(endSpin);

                float dur = seg.End - seg.Start;
                timeRow.AddChild(EditorStyles.MakeLabel($"({dur:F2}s)", 10, EditorStyles.TextSecondary));
                _inspector.AddChild(timeRow);
            }

            // Segment management buttons
            var segBtnRow = new HBoxContainer();
            segBtnRow.AddThemeConstantOverride("separation", 4);

            var addSegBtn = EditorStyles.MakeButton("+ Add", 11, EditorStyles.StatusOk);
            addSegBtn.Pressed += () => {
                float start = _segments.Count > 0 ? _segments[^1].End : 0;
                float end = Mathf.Min(start + 1f, _rawAnimLength);
                _segments.Add(new AnimSegment {
                    Name = $"Clip_{_segments.Count}",
                    Start = start,
                    End = end,
                    Loop = false
                });
                BuildInspector();
            };
            segBtnRow.AddChild(addSegBtn);

            var removeSegBtn = EditorStyles.MakeButton("- Remove Last", 11, EditorStyles.StatusError);
            removeSegBtn.Pressed += () => {
                if (_segments.Count > 0)
                {
                    _segments.RemoveAt(_segments.Count - 1);
                    BuildInspector();
                }
            };
            segBtnRow.AddChild(removeSegBtn);

            var setFromScrubBtn = EditorStyles.MakeButton("Set Start @", 11, AccentColor);
            setFromScrubBtn.TooltipText = "Set last segment's start to current scrub position";
            setFromScrubBtn.Pressed += () => {
                if (_segments.Count > 0 && _scrubSlider != null)
                {
                    var s = _segments[^1];
                    s.Start = (float)_scrubSlider.Value;
                    _segments[^1] = s;
                    BuildInspector();
                }
            };
            segBtnRow.AddChild(setFromScrubBtn);
            _inspector.AddChild(segBtnRow);

            _inspector.AddChild(EditorStyles.MakeSeparator());

            // Re-split button
            var resplitBtn = EditorStyles.MakeButton("RE-SPLIT ANIMATION", 14, new Color(1f, 0.5f, 0.2f));
            resplitBtn.CustomMinimumSize = new Vector2(0, 34);
            resplitBtn.Pressed += ApplyResplit;
            _inspector.AddChild(resplitBtn);

            // Save/Load segment config
            var segSaveRow = new HBoxContainer();
            segSaveRow.AddThemeConstantOverride("separation", 4);
            var saveSegBtn = EditorStyles.MakeButton("Save Segments", 11, EditorStyles.StatusOk);
            saveSegBtn.Pressed += SaveSegmentConfig;
            segSaveRow.AddChild(saveSegBtn);

            var loadSegBtn = EditorStyles.MakeButton("Load Segments", 11, AccentColor);
            loadSegBtn.Pressed += () => { LoadSegmentConfig(); BuildInspector(); };
            segSaveRow.AddChild(loadSegBtn);
            _inspector.AddChild(segSaveRow);
        }

        // ── Segment Config Persistence ──

        private const string SEGMENT_CONFIG_PATH = "res://Data/anim_segments.json";

        private void SaveSegmentConfig()
        {
            if (_selectedCharIndex < 0 || _segments.Count == 0) return;
            var def = Characters[_selectedCharIndex];

            var dict = new Godot.Collections.Dictionary();
            if (FileAccess.FileExists(SEGMENT_CONFIG_PATH))
            {
                var file = FileAccess.Open(SEGMENT_CONFIG_PATH, FileAccess.ModeFlags.Read);
                if (file != null)
                {
                    var json = new Json();
                    if (json.Parse(file.GetAsText()) == Error.Ok && json.Data.Obj is Godot.Collections.Dictionary existing)
                        dict = existing;
                    file.Close();
                }
            }

            var segArray = new Godot.Collections.Array();
            foreach (var seg in _segments)
            {
                var entry = new Godot.Collections.Dictionary();
                entry["Name"] = seg.Name;
                entry["Start"] = seg.Start;
                entry["End"] = seg.End;
                entry["Loop"] = seg.Loop;
                segArray.Add(entry);
            }
            dict[def.Name] = segArray;

            var saveFile = FileAccess.Open(SEGMENT_CONFIG_PATH, FileAccess.ModeFlags.Write);
            if (saveFile != null)
            {
                saveFile.StoreString(Json.Stringify(dict, "\t"));
                saveFile.Close();
                SetStatus($"Saved {_segments.Count} segments for {def.Name}");
            }
        }

        private void LoadSegmentConfig()
        {
            if (_selectedCharIndex < 0) return;
            var def = Characters[_selectedCharIndex];

            if (!FileAccess.FileExists(SEGMENT_CONFIG_PATH)) { SetStatus("No segment config"); return; }

            var file = FileAccess.Open(SEGMENT_CONFIG_PATH, FileAccess.ModeFlags.Read);
            if (file == null) return;

            var json = new Json();
            if (json.Parse(file.GetAsText()) != Error.Ok) { file.Close(); return; }
            file.Close();

            if (json.Data.Obj is not Godot.Collections.Dictionary dict) return;
            if (!dict.ContainsKey(def.Name)) { SetStatus($"No segments for {def.Name}"); return; }

            if (dict[def.Name].Obj is not Godot.Collections.Array segArray) return;

            _segments.Clear();
            foreach (var item in segArray)
            {
                if (item.Obj is not Godot.Collections.Dictionary entry) continue;
                _segments.Add(new AnimSegment {
                    Name = entry.ContainsKey("Name") ? (string)entry["Name"] : "Clip",
                    Start = entry.ContainsKey("Start") ? (float)(double)entry["Start"] : 0f,
                    End = entry.ContainsKey("End") ? (float)(double)entry["End"] : 1f,
                    Loop = entry.ContainsKey("Loop") && (bool)entry["Loop"]
                });
            }

            SetStatus($"Loaded {_segments.Count} segments for {def.Name}");
        }

        // ── Helpers ──

        private void SetStatus(string text)
        {
            if (_statusLabel != null)
                _statusLabel.Text = text;
        }

        private static int FactionIndexFromEnum(VineEnemyFaction faction)
        {
            return faction switch {
                VineEnemyFaction.Scavenger => 1,
                VineEnemyFaction.Brute => 2,
                VineEnemyFaction.Swarm => 3,
                VineEnemyFaction.Ghost => 4,
                _ => 5
            };
        }
    }
}
