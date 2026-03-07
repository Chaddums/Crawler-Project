using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Cinematic dungeon assembly intro. Player stands in the entrance room
    /// and sees all other rooms floating out ahead like a road of possibilities.
    /// Rooms start as gray unknowns, then reveal their types slot-machine style
    /// with color-coded flashes. Finally rooms fly together into the final layout.
    /// </summary>
    public partial class DungeonAssemblyIntro : Node3D
    {
        private const float GRID_SPACING_X = 60f;
        private const float GRID_SPACING_Z = 55f;
        private const float GRID_JITTER = 8f;
        private const float FLOAT_HEIGHT_MIN = 8f;
        private const float FLOAT_HEIGHT_MAX = 18f;
        private const float ZOOM_OUT_DURATION = 1.8f;
        private const float COMBAT_REVEAL_STAGGER = 0.06f;
        private const float SPECIAL_REVEAL_STAGGER = 0.35f;
        private const float POST_REVEAL_PAUSE = 1.5f;
        private const float ASSEMBLY_DURATION = 2.8f;
        private const float BOB_AMPLITUDE = 0.6f;
        private const float BOB_SPEED = 1.2f;

        private static readonly Color UNKNOWN_COLOR = new(0.35f, 0.35f, 0.4f);

        private DungeonGenerator _generator;
        private FogOfWarManager _fogManager;
        private Camera3D _introCamera;
        private Vector3 _entrancePos;
        private Vector2I _entranceGrid;
        private Vector3 _forwardDir;

        private readonly Dictionary<Vector2I, Vector3> _finalPositions = new();
        private readonly Dictionary<Vector2I, Vector3> _scatterPositions = new();
        private readonly Dictionary<Vector2I, Label3D> _roomLabels = new();
        private readonly Dictionary<Vector2I, MeshInstance3D> _glowPlatforms = new();
        private readonly Dictionary<Vector2I, OmniLight3D> _roomLights = new();
        private readonly Dictionary<Vector2I, MeshInstance3D> _celebrationRings = new();
        private readonly Dictionary<Vector2I, List<(StandardMaterial3D mat, Color originalColor)>> _tintedMaterials = new();
        private readonly HashSet<Vector2I> _landedRooms = new();
        private readonly HashSet<Vector2I> _revealedRooms = new();
        private List<KeyValuePair<Vector2I, RoomController>> _revealOrder = new();
        private RandomNumberGenerator _rng = new();
        private Vector3 _scatterCenter;
        private float _scatterExtent;
        private bool _isBobbing;
        private float _bobTime;

        [Signal]
        public delegate void IntroFinishedEventHandler();

        public void Initialize(DungeonGenerator generator, FogOfWarManager fogManager)
        {
            _generator = generator;
            _fogManager = fogManager;
            _rng.Randomize();

            foreach (var (gridPos, type) in _generator.RoomGrid)
            {
                if (type == RoomType.Entrance)
                {
                    _entranceGrid = gridPos;
                    break;
                }
            }

            if (_generator.RoomControllers.TryGetValue(_entranceGrid, out var entranceCtrl))
            {
                var entranceNode = entranceCtrl.GetParent<Node3D>();
                if (entranceNode != null)
                    _entrancePos = entranceNode.Position;
            }
        }

        public void Play()
        {
            _isBobbing = true;

            StoreAndScatterRooms();
            ApplyUnknownCoding();
            AddRoomLabels();

            // Build reveal order: shuffle for slot-machine randomness, but put
            // treasure/boss rooms last for dramatic impact
            _revealOrder = _generator.RoomControllers
                .Where(kv => kv.Key != _entranceGrid)
                .OrderBy(_ => _rng.Randf())
                .OrderBy(kv => GetRevealPriority(kv.Value.RoomType))
                .ToList();

            // Calculate reveal duration
            float revealDuration = 0.5f;
            foreach (var (_, ctrl) in _revealOrder)
            {
                revealDuration += ctrl.RoomType == RoomType.Combat
                    ? COMBAT_REVEAL_STAGGER : SPECIAL_REVEAL_STAGGER;
            }

            // Create camera and choreograph the full sequence
            CreateIntroCamera(revealDuration);

            // Check if this floor has an AXIS Disciple
            bool hasDisciple = _generator.RoomControllers.Values.Any(c => c.HasAxisDisciple);
            float furyPause = hasDisciple ? 2.5f : 0f;

            // Sequence: zoom out → reveal → (fury warning) → assembly → finish
            var tween = CreateTween();
            tween.TweenInterval(ZOOM_OUT_DURATION);
            tween.TweenCallback(Callable.From(AnimateSlotReveal));
            tween.TweenInterval(revealDuration + POST_REVEAL_PAUSE);
            if (hasDisciple)
                tween.TweenCallback(Callable.From(ShowAxisFuryWarning));
            tween.TweenInterval(furyPause);
            tween.TweenCallback(Callable.From(AnimateAssembly));
            tween.TweenInterval(ASSEMBLY_DURATION + 0.8f);
            tween.TweenCallback(Callable.From(FinishIntro));
        }

        public override void _Process(double delta)
        {
            if (!_isBobbing) return;

            _bobTime += (float)delta * BOB_SPEED;

            foreach (var (gridPos, scatterPos) in _scatterPositions)
            {
                if (_landedRooms.Contains(gridPos)) continue;

                if (!_generator.RoomControllers.TryGetValue(gridPos, out var controller)) continue;
                var roomNode = controller.GetParent<Node3D>();
                if (roomNode == null) continue;

                float phase = gridPos.X * 0.7f + gridPos.Y * 1.3f;
                float bob = Mathf.Sin(_bobTime + phase) * BOB_AMPLITUDE;
                var current = roomNode.Position;
                roomNode.Position = new Vector3(current.X, scatterPos.Y + bob, current.Z);
            }
        }

        private void StoreAndScatterRooms()
        {
            // Store all final positions
            foreach (var (gridPos, controller) in _generator.RoomControllers)
            {
                var roomNode = controller.GetParent<Node3D>();
                if (roomNode == null) continue;
                _finalPositions[gridPos] = roomNode.Position;
            }

            // Compute forward direction from entrance toward dungeon center of mass
            var avgDir = Vector3.Zero;
            foreach (var (gridPos, _) in _generator.RoomControllers)
            {
                if (gridPos == _entranceGrid) continue;
                avgDir += (_finalPositions[gridPos] - _entrancePos).Normalized();
            }
            _forwardDir = avgDir.LengthSquared() > 0.01f
                ? new Vector3(avgDir.X, 0, avgDir.Z).Normalized()
                : new Vector3(0, 0, -1);
            var right = _forwardDir.Cross(Vector3.Up).Normalized();

            // Arrange rooms in a grid formation ahead of the player.
            // Calculate grid dimensions — roughly square arrangement
            var nonEntrance = _generator.RoomControllers
                .Where(kv => kv.Key != _entranceGrid)
                .ToList();

            int count = nonEntrance.Count;
            int cols = Mathf.CeilToInt(Mathf.Sqrt(count * 1.3f)); // slightly wider than tall
            int rows = Mathf.CeilToInt((float)count / cols);

            // Center the grid ahead of the entrance
            float gridWidth = (cols - 1) * GRID_SPACING_X;
            float gridDepth = (rows - 1) * GRID_SPACING_Z;
            float startDepth = 50f; // distance from entrance to first row
            float startLateral = -gridWidth / 2f;

            // Shuffle the list for variety in grid placement
            for (int i = count - 1; i > 0; i--)
            {
                int j = _rng.RandiRange(0, i);
                (nonEntrance[i], nonEntrance[j]) = (nonEntrance[j], nonEntrance[i]);
            }

            for (int i = 0; i < count; i++)
            {
                var (gridPos, controller) = nonEntrance[i];
                var roomNode = controller.GetParent<Node3D>();
                if (roomNode == null) continue;

                int col = i % cols;
                int row = i / cols;

                // Grid position with jitter for organic feel
                float lateral = startLateral + col * GRID_SPACING_X
                    + _rng.RandfRange(-GRID_JITTER, GRID_JITTER);
                float depth = startDepth + row * GRID_SPACING_Z
                    + _rng.RandfRange(-GRID_JITTER, GRID_JITTER);
                float height = _rng.RandfRange(FLOAT_HEIGHT_MIN, FLOAT_HEIGHT_MAX);

                var scatterPos = _entrancePos
                    + _forwardDir * depth
                    + right * lateral
                    + Vector3.Up * height;

                _scatterPositions[gridPos] = scatterPos;
                roomNode.Position = scatterPos;
                roomNode.Visible = true;

                // Slight tilt for floating feel
                roomNode.RotationDegrees = new Vector3(
                    _rng.RandfRange(-2f, 2f),
                    _rng.RandfRange(-5f, 5f),
                    _rng.RandfRange(-2f, 2f)
                );
            }

            // Store scatter center for camera targeting
            _scatterCenter = _entrancePos
                + _forwardDir * (startDepth + gridDepth / 2f)
                + Vector3.Up * ((FLOAT_HEIGHT_MIN + FLOAT_HEIGHT_MAX) / 2f);
            _scatterExtent = Mathf.Max(gridWidth, gridDepth) / 2f + 40f;

            // Entrance stays in place
            if (_generator.RoomControllers.TryGetValue(_entranceGrid, out var entranceCtrl))
            {
                var entranceNode = entranceCtrl.GetParent<Node3D>();
                if (entranceNode != null)
                    entranceNode.Visible = true;
            }
        }

        /// <summary>
        /// Start all rooms as gray unknowns — solid opaque platforms so rooms
        /// aren't see-through from the intro camera angle.
        /// </summary>
        private void ApplyUnknownCoding()
        {
            foreach (var (gridPos, controller) in _generator.RoomControllers)
            {
                if (gridPos == _entranceGrid) continue;

                var roomNode = controller.GetParent<Node3D>();
                if (roomNode == null) continue;

                var roomSize = RoomBuilder.GetRoomSize(controller.RoomType);

                // Solid opaque platform box — covers room bottom so it's not see-through
                var platform = new MeshInstance3D();
                var box = new BoxMesh();
                box.Size = new Vector3(roomSize.X * 0.95f, 1.2f, roomSize.Y * 0.95f);
                platform.Mesh = box;

                var platMat = new StandardMaterial3D();
                platMat.AlbedoColor = UNKNOWN_COLOR;
                platMat.EmissionEnabled = true;
                platMat.Emission = UNKNOWN_COLOR.Lightened(0.15f);
                platMat.EmissionEnergyMultiplier = 0.6f;
                platMat.Roughness = 0.8f;
                platform.MaterialOverride = platMat;
                platform.Position = new Vector3(0, -0.8f, 0);
                roomNode.AddChild(platform);
                _glowPlatforms[gridPos] = platform;

                // Dim light — gray ambient
                var light = new OmniLight3D();
                light.LightColor = UNKNOWN_COLOR.Lightened(0.3f);
                light.LightEnergy = 1.5f;
                light.OmniRange = Mathf.Max(roomSize.X, roomSize.Y) * 0.5f;
                light.Position = new Vector3(0, 5f, 0);
                light.ShadowEnabled = false;
                roomNode.AddChild(light);
                _roomLights[gridPos] = light;

                // Tint room materials gray
                var tinted = new List<(StandardMaterial3D, Color)>();
                TintMeshMaterials(roomNode, UNKNOWN_COLOR, 0.5f, tinted);
                _tintedMaterials[gridPos] = tinted;
            }
        }

        /// <summary>
        /// Slot machine reveal — rooms light up one by one with their type color.
        /// Treasure/boss rooms reveal last for dramatic impact.
        /// </summary>
        private void AnimateSlotReveal()
        {
            if (ServiceLocator.TryGet<AudioManager>(out var audio))
                audio.PlaySFXByName("equip");

            float delay = 0f;
            for (int i = 0; i < _revealOrder.Count; i++)
            {
                var (gridPos, controller) = _revealOrder[i];
                var capturedGrid = gridPos;
                var capturedType = controller.RoomType;

                var tween = CreateTween();
                tween.TweenInterval(delay);
                tween.TweenCallback(Callable.From(() => RevealRoom(capturedGrid, capturedType)));

                // Combat rooms zip by fast; special rooms pause for dramatic effect
                delay += capturedType == RoomType.Combat
                    ? COMBAT_REVEAL_STAGGER : SPECIAL_REVEAL_STAGGER;
            }
        }

        private void RevealRoom(Vector2I gridPos, RoomType roomType)
        {
            _revealedRooms.Add(gridPos);

            if (!_generator.RoomControllers.TryGetValue(gridPos, out var controller)) return;
            var roomNode = controller.GetParent<Node3D>();
            if (roomNode == null) return;

            var typeColor = GetRoomGlowColor(roomType);
            var roomSize = RoomBuilder.GetRoomSize(roomType);

            // Flash the platform to the room's color
            if (_glowPlatforms.TryGetValue(gridPos, out var platform) && IsInstanceValid(platform))
            {
                var platMat = platform.MaterialOverride as StandardMaterial3D;
                if (platMat != null)
                {
                    // Brief white flash then settle to type color
                    platMat.AlbedoColor = new Color(1f, 1f, 1f);
                    platMat.Emission = new Color(1f, 1f, 1f);
                    platMat.EmissionEnergyMultiplier = 6f;

                    var flashTween = CreateTween();
                    flashTween.TweenProperty(platMat, "albedo_color", typeColor, 0.3f)
                        .SetEase(Tween.EaseType.Out);
                    flashTween.Parallel().TweenProperty(platMat, "emission", typeColor, 0.3f)
                        .SetEase(Tween.EaseType.Out);
                    flashTween.Parallel().TweenProperty(platMat, "emission_energy_multiplier", 3f, 0.4f)
                        .SetEase(Tween.EaseType.Out);
                }
            }

            // Swap light color
            if (_roomLights.TryGetValue(gridPos, out var light) && IsInstanceValid(light))
            {
                light.LightColor = typeColor;
                var lightTween = CreateTween();
                // Flash bright then settle
                light.LightEnergy = 8f;
                lightTween.TweenProperty(light, "light_energy", 3.5f, 0.5f)
                    .SetEase(Tween.EaseType.Out);
                lightTween.Parallel().TweenProperty(light, "omni_range",
                    Mathf.Max(roomSize.X, roomSize.Y) * 0.7f, 0.3f);
            }

            // Retint room materials to type color (from gray)
            if (_tintedMaterials.TryGetValue(gridPos, out var tinted))
            {
                foreach (var (mat, _) in tinted)
                {
                    if (mat == null) continue;
                    // Flash white then lerp to tinted color
                    mat.AlbedoColor = new Color(1f, 1f, 1f);
                    mat.Emission = typeColor;
                    mat.EmissionEnergyMultiplier = 2f;
                }

                // Settle the tint over 0.4s
                var matTween = CreateTween();
                matTween.TweenInterval(0.05f);
                matTween.TweenCallback(Callable.From(() =>
                {
                    foreach (var (mat, originalColor) in tinted)
                    {
                        if (mat == null) continue;
                        var targetColor = originalColor.Lerp(typeColor, 0.4f);
                        var settle = CreateTween();
                        settle.TweenProperty(mat, "albedo_color", targetColor, 0.35f)
                            .SetEase(Tween.EaseType.Out);
                        settle.Parallel().TweenProperty(mat, "emission_energy_multiplier", 0.8f, 0.4f);
                    }
                }));
            }

            // Update label — swap from "???" to real name with pop animation
            if (_roomLabels.TryGetValue(gridPos, out var label) && IsInstanceValid(label))
            {
                label.Text = GetRoomDisplayName(roomType);
                label.Modulate = GetRoomLabelColor(roomType);

                // Pop scale effect
                label.Scale = Vector3.One * 0.01f;
                var popTween = CreateTween();
                popTween.TweenProperty(label, "scale", Vector3.One * 1.3f, 0.15f)
                    .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
                popTween.TweenProperty(label, "scale", Vector3.One, 0.1f)
                    .SetEase(Tween.EaseType.InOut);
            }

            // Special celebration for high-value rooms
            if (roomType == RoomType.Treasure)
                SpawnCelebrationRing(gridPos, roomNode, typeColor, roomSize, true);
            else if (roomType == RoomType.Megabonk)
                SpawnCelebrationRing(gridPos, roomNode, typeColor, roomSize, true);
            else if (roomType == RoomType.Boss)
                SpawnCelebrationRing(gridPos, roomNode, typeColor, roomSize, false);
            else if (roomType == RoomType.Event || roomType == RoomType.Shop)
                SpawnCelebrationRing(gridPos, roomNode, typeColor, roomSize, false);

            // Tick sound for each reveal
            if (ServiceLocator.TryGet<AudioManager>(out var audio))
                audio.PlaySFXByName("pickup");
        }

        /// <summary>
        /// Expanding ring effect around high-value rooms on reveal.
        /// Treasure rooms get a double ring + brighter glow.
        /// </summary>
        private void SpawnCelebrationRing(Vector2I gridPos, Node3D roomNode, Color color,
            Vector2 roomSize, bool isTreasure)
        {
            float maxRadius = Mathf.Max(roomSize.X, roomSize.Y) * 0.6f;

            var ring = new MeshInstance3D();
            var torus = new TorusMesh();
            torus.InnerRadius = 0.5f;
            torus.OuterRadius = 1.5f;
            ring.Mesh = torus;

            var ringMat = new StandardMaterial3D();
            ringMat.AlbedoColor = isTreasure ? new Color(1f, 0.9f, 0.3f, 0.9f) : new Color(color.R, color.G, color.B, 0.8f);
            ringMat.EmissionEnabled = true;
            ringMat.Emission = color;
            ringMat.EmissionEnergyMultiplier = isTreasure ? 5f : 3f;
            ringMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            ringMat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Disabled;
            ring.MaterialOverride = ringMat;
            ring.Position = new Vector3(0, 1f, 0);
            ring.Scale = Vector3.One * 0.5f;
            ring.RotationDegrees = new Vector3(90, 0, 0);
            roomNode.AddChild(ring);
            _celebrationRings[gridPos] = ring;

            // Expand and fade
            var expandTween = CreateTween();
            expandTween.TweenProperty(ring, "scale", Vector3.One * maxRadius, isTreasure ? 1.0f : 0.7f)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
            expandTween.Parallel().TweenProperty(ringMat, "albedo_color:a", 0f, isTreasure ? 1.2f : 0.8f)
                .SetEase(Tween.EaseType.In);

            if (isTreasure)
            {
                // Second delayed ring for treasure
                var ring2 = new MeshInstance3D();
                var torus2 = new TorusMesh();
                torus2.InnerRadius = 0.3f;
                torus2.OuterRadius = 1.0f;
                ring2.Mesh = torus2;

                var ring2Mat = new StandardMaterial3D();
                ring2Mat.AlbedoColor = new Color(1f, 1f, 0.5f, 0.7f);
                ring2Mat.EmissionEnabled = true;
                ring2Mat.Emission = new Color(1f, 0.85f, 0.2f);
                ring2Mat.EmissionEnergyMultiplier = 4f;
                ring2Mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                ring2.MaterialOverride = ring2Mat;
                ring2.Position = new Vector3(0, 1f, 0);
                ring2.Scale = Vector3.One * 0.3f;
                ring2.RotationDegrees = new Vector3(90, 0, 0);
                roomNode.AddChild(ring2);

                var expand2 = CreateTween();
                expand2.TweenInterval(0.2f);
                expand2.TweenProperty(ring2, "scale", Vector3.One * maxRadius * 0.8f, 0.9f)
                    .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
                expand2.Parallel().TweenProperty(ring2Mat, "albedo_color:a", 0f, 1.0f)
                    .SetEase(Tween.EaseType.In);
                expand2.TweenCallback(Callable.From(() =>
                {
                    if (IsInstanceValid(ring2)) ring2.QueueFree();
                }));
            }
        }

        private static void TintMeshMaterials(Node root, Color tintColor, float tintStrength,
            List<(StandardMaterial3D mat, Color originalColor)> tracker)
        {
            foreach (var child in root.GetChildren())
            {
                if (child is MeshInstance3D mesh)
                {
                    if (mesh.MaterialOverride is StandardMaterial3D overrideMat)
                    {
                        var original = overrideMat.AlbedoColor;
                        tracker.Add((overrideMat, original));
                        overrideMat.AlbedoColor = original.Lerp(tintColor, tintStrength);
                        overrideMat.EmissionEnabled = true;
                        overrideMat.Emission = tintColor.Darkened(0.3f);
                        overrideMat.EmissionEnergyMultiplier = 0.4f;
                    }
                    else if (mesh.Mesh != null)
                    {
                        for (int s = 0; s < mesh.Mesh.GetSurfaceCount(); s++)
                        {
                            var surfMat = mesh.GetActiveMaterial(s);
                            if (surfMat is StandardMaterial3D stdMat)
                            {
                                var copy = (StandardMaterial3D)stdMat.Duplicate();
                                var original = copy.AlbedoColor;
                                tracker.Add((copy, original));
                                copy.AlbedoColor = original.Lerp(tintColor, tintStrength);
                                copy.EmissionEnabled = true;
                                copy.Emission = tintColor.Darkened(0.3f);
                                copy.EmissionEnergyMultiplier = 0.4f;
                                mesh.SetSurfaceOverrideMaterial(s, copy);
                            }
                        }
                    }
                }

                if (child is RoomController) continue;
                if (child is Node node && node.GetChildCount() > 0)
                    TintMeshMaterials(node, tintColor, tintStrength, tracker);
            }
        }

        /// <summary>
        /// All rooms start with "???" labels — real names get revealed during slot machine phase.
        /// </summary>
        private void AddRoomLabels()
        {
            foreach (var (gridPos, controller) in _generator.RoomControllers)
            {
                if (gridPos == _entranceGrid) continue;

                var roomNode = controller.GetParent<Node3D>();
                if (roomNode == null) continue;

                var label = new Label3D();
                label.Text = "???";
                label.FontSize = 48;
                label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
                label.Modulate = new Color(0.6f, 0.6f, 0.65f);
                label.OutlineModulate = new Color(0, 0, 0);
                label.OutlineSize = 6;
                label.PixelSize = 0.015f;
                label.Position = new Vector3(0, 5f, 0);
                label.NoDepthTest = true;

                roomNode.AddChild(label);
                _roomLabels[gridPos] = label;
            }
        }

        private void CreateIntroCamera(float revealDuration)
        {
            _introCamera = new Camera3D();
            _introCamera.Name = "IntroCam";
            _introCamera.Fov = 50f;
            AddChild(_introCamera);

            // Phase 1 start: close behind the player, looking at the entrance
            var closePos = _entrancePos
                - _forwardDir * 12f
                + Vector3.Up * 10f;

            _introCamera.Position = closePos;
            _introCamera.LookAt(_entrancePos + Vector3.Up * 2f, Vector3.Up);
            _introCamera.MakeCurrent();

            // Phase 1 target: zoomed way out to see all scattered rooms
            float pullDist = _scatterExtent * 0.8f;
            var widePos = _scatterCenter
                - _forwardDir * pullDist
                + Vector3.Up * (pullDist * 0.6f);

            // Phase 1: Zoom out from player to wide shot (rooms become visible)
            var zoomOut = CreateTween();
            zoomOut.TweenProperty(_introCamera, "position", widePos, ZOOM_OUT_DURATION)
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Cubic);
            zoomOut.Parallel().TweenProperty(_introCamera, "fov", 65f, ZOOM_OUT_DURATION)
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Sine);

            // Smoothly rotate to look at the scatter center during zoom
            // We use a tween callback to continuously update LookAt
            var lookTween = CreateTween();
            lookTween.TweenMethod(
                Callable.From((float t) =>
                {
                    if (_introCamera != null && IsInstanceValid(_introCamera))
                    {
                        var lookFrom = _entrancePos + Vector3.Up * 2f;
                        var lookTo = _scatterCenter;
                        var lookTarget = lookFrom.Lerp(lookTo, t);
                        _introCamera.LookAt(lookTarget, Vector3.Up);
                    }
                }),
                0f, 1f, ZOOM_OUT_DURATION
            ).SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Sine);

            // Phase 2: Hold wide shot during reveals (slight drift for life)
            float holdDuration = revealDuration + POST_REVEAL_PAUSE;
            var driftPos = widePos + _forwardDir * 8f + Vector3.Up * 3f;
            var driftTween = CreateTween();
            driftTween.TweenInterval(ZOOM_OUT_DURATION);
            driftTween.TweenProperty(_introCamera, "position", driftPos, holdDuration)
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Sine);

            // Phase 3: During assembly, fly camera back toward the player
            float assemblyStart = ZOOM_OUT_DURATION + holdDuration;
            var returnPos = _entrancePos
                - _forwardDir * 15f
                + Vector3.Up * 12f;

            var returnTween = CreateTween();
            returnTween.TweenInterval(assemblyStart);
            returnTween.TweenProperty(_introCamera, "position", returnPos, ASSEMBLY_DURATION)
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Cubic);
            returnTween.Parallel().TweenProperty(_introCamera, "fov", 50f, ASSEMBLY_DURATION)
                .SetEase(Tween.EaseType.InOut);

            // Return look target to entrance area
            var returnLookTween = CreateTween();
            returnLookTween.TweenInterval(assemblyStart);
            returnLookTween.TweenMethod(
                Callable.From((float t) =>
                {
                    if (_introCamera != null && IsInstanceValid(_introCamera))
                    {
                        var lookFrom = _scatterCenter;
                        var lookTo = _entrancePos + Vector3.Up * 2f;
                        var lookTarget = lookFrom.Lerp(lookTo, t);
                        _introCamera.LookAt(lookTarget, Vector3.Up);
                    }
                }),
                0f, 1f, ASSEMBLY_DURATION
            ).SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Cubic);
        }

        /// <summary>
        /// Floor-wide warning that an AXIS Disciple lurks in one of the rooms.
        /// Shows a dramatic 3D text banner visible from the intro camera.
        /// </summary>
        private void ShowAxisFuryWarning()
        {
            // 3D banner text floating in front of the camera
            var banner = new Label3D();
            banner.Text = "AXIS  FURY  APPLIES  TO  THIS  FLOOR";
            banner.FontSize = 72;
            banner.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            banner.Modulate = new Color(1f, 0.95f, 0.7f, 0f); // start invisible
            banner.OutlineModulate = new Color(0.4f, 0.02f, 0.05f);
            banner.OutlineSize = 8;
            banner.PixelSize = 0.012f;
            banner.NoDepthTest = true;

            // Position between camera and the scatter center
            var bannerPos = _introCamera != null && IsInstanceValid(_introCamera)
                ? _introCamera.GlobalPosition.Lerp(_scatterCenter, 0.35f)
                : _scatterCenter + Vector3.Up * 15f;
            AddChild(banner);
            banner.GlobalPosition = bannerPos;

            // Subtitle with lore flavor
            var subtitle = new Label3D();
            subtitle.Text = "One of AXIS's chosen awaits. Destroy it quickly for divine reward.";
            subtitle.FontSize = 36;
            subtitle.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            subtitle.Modulate = new Color(0.8f, 0.7f, 0.5f, 0f);
            subtitle.OutlineModulate = new Color(0, 0, 0);
            subtitle.OutlineSize = 4;
            subtitle.PixelSize = 0.012f;
            subtitle.NoDepthTest = true;
            subtitle.Position = new Vector3(0, -3f, 0);
            banner.AddChild(subtitle);

            // Animate: flash in, hold, fade out
            banner.Scale = Vector3.One * 0.01f;
            var bannerTween = CreateTween();
            bannerTween.TweenProperty(banner, "modulate:a", 1f, 0.2f);
            bannerTween.Parallel().TweenProperty(banner, "scale", Vector3.One * 1.2f, 0.25f)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
            bannerTween.TweenProperty(banner, "scale", Vector3.One, 0.1f);

            // Subtitle fades in slightly after
            var subTween = CreateTween();
            subTween.TweenInterval(0.3f);
            subTween.TweenProperty(subtitle, "modulate:a", 1f, 0.3f);

            // Hold then fade everything out
            var fadeTween = CreateTween();
            fadeTween.TweenInterval(1.8f);
            fadeTween.TweenProperty(banner, "modulate:a", 0f, 0.5f);
            fadeTween.Parallel().TweenProperty(subtitle, "modulate:a", 0f, 0.5f);
            fadeTween.TweenCallback(Callable.From(() =>
            {
                if (IsInstanceValid(banner)) banner.QueueFree();
            }));

            // AXIS commentary
            if (ServiceLocator.TryGet<CommentaryManager>(out var commentary))
            {
                commentary.QueueLine("AXIS",
                    "I've stationed one of my chosen on this floor. Find them... if you dare.",
                    CommentaryPriority.Announcement, CommentaryCategory.SectorIntro);
            }

            if (ServiceLocator.TryGet<AudioManager>(out var audio))
                audio.PlaySFXByName("equip");

            GD.Print("[DungeonAssemblyIntro] AXIS FURY warning displayed");
        }

        private void AnimateAssembly()
        {
            var sortedRooms = _scatterPositions
                .OrderBy(kv => _finalPositions[kv.Key].DistanceTo(_entrancePos))
                .ToList();

            int count = sortedRooms.Count;
            float maxStagger = ASSEMBLY_DURATION * 0.4f;

            for (int i = 0; i < count; i++)
            {
                var (gridPos, _) = sortedRooms[i];

                if (!_generator.RoomControllers.TryGetValue(gridPos, out var controller)) continue;
                var roomNode = controller.GetParent<Node3D>();
                if (roomNode == null) continue;

                var finalPos = _finalPositions[gridPos];

                float staggerDelay = (i / (float)count) * maxStagger;
                float moveDuration = ASSEMBLY_DURATION - staggerDelay;
                moveDuration = Mathf.Max(moveDuration, 0.6f);

                var tween = CreateTween();
                tween.TweenInterval(staggerDelay);

                tween.TweenProperty(roomNode, "position", finalPos, moveDuration)
                    .SetEase(Tween.EaseType.InOut)
                    .SetTrans(Tween.TransitionType.Cubic);

                tween.Parallel().TweenProperty(roomNode, "rotation_degrees", Vector3.Zero, moveDuration)
                    .SetEase(Tween.EaseType.Out)
                    .SetTrans(Tween.TransitionType.Cubic);

                var capturedGridPos = gridPos;
                tween.TweenCallback(Callable.From(() => OnRoomLanded(capturedGridPos)));
            }

            if (ServiceLocator.TryGet<AudioManager>(out var audio))
                audio.PlaySFXByName("equip");
        }

        private void OnRoomLanded(Vector2I gridPos)
        {
            _landedRooms.Add(gridPos);

            if (_roomLabels.TryGetValue(gridPos, out var label))
            {
                var tween = CreateTween();
                tween.TweenProperty(label, "modulate:a", 0f, 0.3f);
                tween.TweenCallback(Callable.From(() =>
                {
                    if (IsInstanceValid(label))
                        label.QueueFree();
                }));
                _roomLabels.Remove(gridPos);
            }

            if (_glowPlatforms.TryGetValue(gridPos, out var platform))
            {
                if (IsInstanceValid(platform))
                    platform.QueueFree();
                _glowPlatforms.Remove(gridPos);
            }

            if (_roomLights.TryGetValue(gridPos, out var light))
            {
                if (IsInstanceValid(light))
                    light.QueueFree();
                _roomLights.Remove(gridPos);
            }

            if (_celebrationRings.TryGetValue(gridPos, out var ring))
            {
                if (IsInstanceValid(ring))
                    ring.QueueFree();
                _celebrationRings.Remove(gridPos);
            }

            if (_tintedMaterials.TryGetValue(gridPos, out var tinted))
            {
                foreach (var (mat, originalColor) in tinted)
                {
                    if (mat != null)
                    {
                        mat.AlbedoColor = originalColor;
                        mat.EmissionEnabled = false;
                    }
                }
                _tintedMaterials.Remove(gridPos);
            }

            if (_generator.RoomControllers.TryGetValue(gridPos, out var controller))
                controller.SetFogState(FogState.Hidden);
        }


        private void FinishIntro()
        {
            _isBobbing = false;

            if (_introCamera != null && IsInstanceValid(_introCamera))
            {
                _introCamera.QueueFree();
                _introCamera = null;
            }

            foreach (var (_, label) in _roomLabels)
            {
                if (IsInstanceValid(label))
                    label.QueueFree();
            }
            _roomLabels.Clear();

            foreach (var (_, platform) in _glowPlatforms)
            {
                if (IsInstanceValid(platform))
                    platform.QueueFree();
            }
            _glowPlatforms.Clear();

            foreach (var (_, light) in _roomLights)
            {
                if (IsInstanceValid(light))
                    light.QueueFree();
            }
            _roomLights.Clear();

            foreach (var (_, ring) in _celebrationRings)
            {
                if (IsInstanceValid(ring))
                    ring.QueueFree();
            }
            _celebrationRings.Clear();

            foreach (var (_, tinted) in _tintedMaterials)
            {
                foreach (var (mat, originalColor) in tinted)
                {
                    if (mat != null)
                    {
                        mat.AlbedoColor = originalColor;
                        mat.EmissionEnabled = false;
                    }
                }
            }
            _tintedMaterials.Clear();

            _fogManager.Initialize(_generator);

            EmitSignal(SignalName.IntroFinished);
            GD.Print("[DungeonAssemblyIntro] Intro complete — handing off to gameplay");
        }

        /// <summary>
        /// Reveal priority — lower numbers reveal first. Treasure and boss last for drama.
        /// </summary>
        private static int GetRevealPriority(RoomType type) => type switch
        {
            RoomType.Combat => 0,
            RoomType.SafeRoom => 1,
            RoomType.Puzzle => 2,
            RoomType.Shop => 3,
            RoomType.Event => 4,
            RoomType.Treasure => 5,
            RoomType.Megabonk => 6,
            RoomType.Boss => 7,
            _ => 0,
        };

        private static string GetRoomDisplayName(RoomType type) => type switch
        {
            RoomType.Entrance => "ENTRANCE",
            RoomType.Combat => "COMBAT",
            RoomType.Treasure => "TREASURE",
            RoomType.Shop => "SHOP",
            RoomType.Boss => "BOSS",
            RoomType.SafeRoom => "SAFE ROOM",
            RoomType.Event => "EVENT",
            RoomType.Puzzle => "PUZZLE",
            RoomType.Megabonk => "MEGABONK",
            _ => "???",
        };

        private static Color GetRoomGlowColor(RoomType type) => type switch
        {
            RoomType.Entrance => new Color(0.3f, 0.6f, 0.9f),      // Blue — home base
            RoomType.Combat => new Color(0.9f, 0.25f, 0.2f),       // Red — danger
            RoomType.Treasure => new Color(1f, 0.78f, 0.1f),       // Gold — loot
            RoomType.Shop => new Color(0.2f, 0.85f, 0.4f),         // Green — money
            RoomType.Boss => new Color(0.85f, 0.1f, 0.1f),         // Deep red — big danger
            RoomType.SafeRoom => new Color(0.3f, 0.55f, 0.95f),    // Calm blue — safety
            RoomType.Event => new Color(0.7f, 0.35f, 0.95f),       // Purple — mystery/buff
            RoomType.Puzzle => new Color(0.95f, 0.5f, 0.1f),       // Orange — challenge
            RoomType.Megabonk => new Color(1f, 0.15f, 0.6f),       // Hot pink — chaos
            _ => new Color(0.5f, 0.5f, 0.5f),
        };

        private static Color GetRoomLabelColor(RoomType type) => type switch
        {
            RoomType.Entrance => new Color(0.5f, 0.8f, 1f),
            RoomType.Combat => new Color(1f, 0.4f, 0.3f),
            RoomType.Treasure => new Color(1f, 0.85f, 0.2f),
            RoomType.Shop => new Color(0.3f, 1f, 0.5f),
            RoomType.Boss => new Color(1f, 0.2f, 0.2f),
            RoomType.SafeRoom => new Color(0.4f, 0.7f, 1f),
            RoomType.Event => new Color(0.8f, 0.5f, 1f),
            RoomType.Puzzle => new Color(1f, 0.6f, 0.2f),
            RoomType.Megabonk => new Color(1f, 0.3f, 0.7f),
            _ => new Color(0.7f, 0.7f, 0.7f),
        };
    }
}
