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
        private static readonly Color SWEEP_RED = new(1f, 0.12f, 0.08f);

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
        private readonly List<Vector2I> _bobOrder = new();
        private readonly Dictionary<Vector2I, Node> _celebrationRings = new();
        private readonly HashSet<Vector2I> _landedRooms = new();
        private readonly HashSet<Vector2I> _revealedRooms = new();
        private List<KeyValuePair<Vector2I, RoomController>> _revealOrder = new();
        private RandomNumberGenerator _rng = new();
        private Vector3 _scatterCenter;
        private float _scatterExtent;
        private bool _isBobbing;
        private float _bobTime;
        private Node3D _sweepPlane;
        private float _revealDuration;

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
            PositionAXISAcrossFromCamera();
            ApplyUnknownCoding();
            AddRoomLabels();

            // Build reveal order: shuffle for slot-machine randomness, but put
            // treasure/boss rooms last for dramatic impact
            _revealOrder = _generator.RoomControllers
                .Where(kv => kv.Key != _entranceGrid)
                .OrderBy(_ => _rng.Randf())
                .OrderBy(kv => GetRevealPriority(kv.Value.RoomType))
                .ToList();

            // Calculate sweep duration — smooth continuous scan across the dungeon
            _revealDuration = Mathf.Clamp(_revealOrder.Count * 0.18f, 2.0f, 3.5f);
            float revealDuration = _revealDuration;

            // Create camera and choreograph the full sequence
            CreateIntroCamera(revealDuration);

            // Check if this floor has an AXIS Disciple
            bool hasDisciple = _generator.RoomControllers.Values.Any(c => c.HasAxisDisciple);
            float furyPause = hasDisciple ? 2.5f : 0f;

            // Sequence: zoom out → reveal → (fury warning) → assembly → finish
            var tween = CreateTween();
            tween.TweenInterval(ZOOM_OUT_DURATION);
            tween.TweenCallback(Callable.From(AnimateLaserSweep));
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

            for (int i = _bobOrder.Count - 1; i >= 0; i--)
            {
                var gridPos = _bobOrder[i];
                if (_landedRooms.Contains(gridPos))
                {
                    _bobOrder.RemoveAt(i);
                    continue;
                }

                if (!_scatterPositions.TryGetValue(gridPos, out var scatterPos)) continue;
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
                _bobOrder.Add(gridPos);
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
        /// Move AXIS to the far side of the scattered rooms, directly across from
        /// the intro camera so the player gets a clear view of it during the intro.
        /// AXIS faces back toward the entrance/camera.
        /// </summary>
        private void PositionAXISAcrossFromCamera()
        {
            var backdrop = GetParent()?.GetNodeOrNull<DungeonBackdrop>("DungeonBackdrop");
            if (backdrop?.AXIS == null) return;

            // Place AXIS just beyond the room grid, raised high and doubled in size
            float depth = _scatterExtent * 0.7f + 30f;
            var axisPos = _entrancePos + _forwardDir * depth;
            backdrop.AXIS.GlobalPosition = new Vector3(axisPos.X, 15f, axisPos.Z);

            // Rotate to face back toward the entrance/camera
            backdrop.AXIS.LookAt(new Vector3(_entrancePos.X, 15f, _entrancePos.Z), Vector3.Up);

            // Scale up big — AXIS looms over the dungeon during scan
            backdrop.AXIS.Scale = Vector3.One * 3.0f;

            _axisOriginalScale = Vector3.One; // remember default for restoring later
        }

        private Vector3 _axisOriginalScale = Vector3.One;

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
                platform.Position = new Vector3(0, -1.2f, 0);
                roomNode.AddChild(platform);
                _glowPlatforms[gridPos] = platform;

                // Skip per-room OmniLight3Ds and recursive material tinting —
                // the glow platform provides enough visual indication and these were
                // creating 40+ dynamic lights + hundreds of material copies tanking FPS.
            }
        }

        /// <summary>
        /// Cone laser sweep — an opaque cone of light originates from under AXIS
        /// and sweeps from the camera/entrance area toward directly below AXIS,
        /// revealing rooms as it passes over them.
        /// </summary>
        private void AnimateLaserSweep()
        {
            if (ServiceLocator.TryGet<AudioManager>(out var audio))
                audio.PlaySFXByName("equip");

            var backdrop = GetParent()?.GetNodeOrNull<DungeonBackdrop>("DungeonBackdrop");
            if (backdrop?.AXIS == null) return;

            // AXIS body underside — cone origin point
            // AXIS is scaled 3x during intro, head at HEAD_Y=42 → ~126 local, torso ~90 local
            // GlobalPosition.Y is 15, so torso underside is ~15 + 75 = ~90 world
            Vector3 axisWorldPos = backdrop.AXIS.GlobalPosition;
            float coneOriginY = axisWorldPos.Y + 75f;
            Vector3 coneOrigin = new Vector3(axisWorldPos.X, coneOriginY, axisWorldPos.Z);

            // Compute room bounds for sizing and timing
            var roomForwards = new Dictionary<Vector2I, float>();
            float minFwd = float.MaxValue, maxFwd = float.MinValue;

            foreach (var (gridPos, scatterPos) in _scatterPositions)
            {
                float fwd = (scatterPos - _entrancePos).Dot(_forwardDir);
                roomForwards[gridPos] = fwd;
                minFwd = Mathf.Min(minFwd, fwd);
                maxFwd = Mathf.Max(maxFwd, fwd);
            }

            // Cone length — reach from AXIS to the farthest room
            float distToFarRoom = 0f;
            foreach (var (_, scatterPos) in _scatterPositions)
            {
                float d = (coneOrigin - scatterPos).Length();
                distToFarRoom = Mathf.Max(distToFarRoom, d);
            }
            float coneLength = distToFarRoom + 30f;
            float topRadius = 2f;
            float bottomRadius = coneLength * 0.8f;

            // Build cone pivot at AXIS underside
            _sweepPlane = new Node3D();
            _sweepPlane.Name = "LaserSweepCone";
            AddChild(_sweepPlane);
            _sweepPlane.GlobalPosition = coneOrigin;

            // Cone meshes (narrow end at pivot, extending along -Y)
            BuildConeMesh(_sweepPlane, topRadius, bottomRadius, coneLength,
                SWEEP_RED, 0.25f, 4f);
            BuildConeMesh(_sweepPlane, topRadius * 0.5f, bottomRadius * 0.3f, coneLength,
                new Color(1f, 0.5f, 0.3f), 0.4f, 6f);
            BuildConeMesh(_sweepPlane, topRadius * 1.5f, bottomRadius * 1.15f, coneLength,
                SWEEP_RED, 0.12f, 2f);

            // Quaternion sweep — robust regardless of dungeon orientation.
            // The cone mesh extends along -Y, so we rotate the pivot so -Y
            // points from AXIS toward the entrance (start) then straight down (end).
            Vector3 startDir = (_entrancePos - coneOrigin).Normalized();
            Vector3 endDir = Vector3.Down;

            Quaternion startQuat = RotateDownToward(startDir);
            Quaternion endQuat = RotateDownToward(endDir);
            if (startQuat.Dot(endQuat) < 0) endQuat = -endQuat;

            _sweepPlane.Quaternion = startQuat;

            // Animate cone rotation via quaternion slerp
            var startQ = startQuat;
            var endQ = endQuat;
            var sweepTween = CreateTween();
            sweepTween.TweenMethod(
                Callable.From((float t) =>
                {
                    if (_sweepPlane != null && IsInstanceValid(_sweepPlane))
                        _sweepPlane.Quaternion = startQ.Slerp(endQ, t);
                }),
                0f, 1f, _revealDuration
            ).SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Sine);

            // Schedule room reveals by forward position (near entrance → below AXIS)
            float fwdRange = maxFwd - minFwd;
            if (fwdRange < 1f) fwdRange = 1f;

            var sortedRooms = _revealOrder
                .Where(kv => roomForwards.ContainsKey(kv.Key))
                .OrderBy(kv => roomForwards[kv.Key])
                .ToList();

            for (int i = 0; i < sortedRooms.Count; i++)
            {
                var (gridPos, controller) = sortedRooms[i];
                float fwd = roomForwards[gridPos];
                float t = (fwd - minFwd) / fwdRange;
                float delay = Mathf.Lerp(_revealDuration * 0.05f, _revealDuration * 0.95f, t);

                var capturedGrid = gridPos;
                var capturedType = controller.RoomType;

                var revealTween = CreateTween();
                revealTween.TweenInterval(delay);
                revealTween.TweenCallback(Callable.From(() => RevealRoom(capturedGrid, capturedType)));
            }

            // Clean up cone after sweep completes
            var cleanupTween = CreateTween();
            cleanupTween.TweenInterval(_revealDuration + 1.5f);
            cleanupTween.TweenCallback(Callable.From(() =>
            {
                if (_sweepPlane != null && IsInstanceValid(_sweepPlane))
                    _sweepPlane.QueueFree();
                _sweepPlane = null;
            }));

            // AXIS waves hands right to left in sync
            backdrop.AXIS.CommandSweep(_revealDuration);
        }

        private static void BuildConeMesh(Node3D parent, float topRadius, float bottomRadius,
            float height, Color color, float alpha, float emission)
        {
            var mesh = new MeshInstance3D();
            var cyl = new CylinderMesh();
            cyl.TopRadius = topRadius;
            cyl.BottomRadius = bottomRadius;
            cyl.Height = height;
            cyl.RadialSegments = 16;
            mesh.Mesh = cyl;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(color.R, color.G, color.B, alpha);
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.EmissionEnabled = true;
            mat.Emission = color;
            mat.EmissionEnergyMultiplier = emission;
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
            mesh.MaterialOverride = mat;

            // Narrow end at pivot, extends downward
            mesh.Position = new Vector3(0, -height / 2f, 0);
            parent.AddChild(mesh);
        }

        /// <summary>
        /// Returns a quaternion that rotates the default -Y axis to point along the given direction.
        /// Used to aim the cone (which extends along -Y from its pivot).
        /// </summary>
        private static Quaternion RotateDownToward(Vector3 dir)
        {
            Vector3 from = Vector3.Down;
            Vector3 to = dir.Normalized();
            float dot = from.Dot(to);
            if (dot > 0.9999f) return Quaternion.Identity;
            if (dot < -0.9999f) return new Quaternion(Vector3.Right, Mathf.Pi);
            Vector3 axis = from.Cross(to).Normalized();
            float angle = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f));
            return new Quaternion(axis, angle);
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

            // Platform glow is sufficient — no per-room lights or material retinting needed

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

            // Special celebration for high-value rooms — sprite VFX beam + expanding torus ring
            if (roomType is RoomType.Treasure or RoomType.Megabonk or RoomType.Boss
                or RoomType.Event or RoomType.Shop)
            {
                float scale = roomType == RoomType.Treasure ? 400f : 250f;
                // Room origin is at corner; offset to room center (rooms are 32x32)
                var roomCenter = roomNode.GlobalPosition + new Vector3(16f, 0f, 16f);
                var sprite = SpriteVfxLibrary.SpawnRoomReveal(roomNode, roomCenter, roomType, scale);
                if (sprite != null)
                    _celebrationRings[gridPos] = sprite;

                // Expanding torus ring at room center
                SpawnCelebrationRing(roomNode, roomCenter, typeColor, roomSize);
                if (roomType is RoomType.Treasure or RoomType.Megabonk)
                    SpawnCelebrationRing(roomNode, roomCenter, typeColor.Lightened(0.3f), roomSize, 0.2f);
            }

            // Tick sound for each reveal
            if (ServiceLocator.TryGet<AudioManager>(out var audio))
                audio.PlaySFXByName("pickup");

            // AXIS sweep handles the scanning animation globally
        }

        // Room reveal VFX now handled by SpriteVfxLibrary.SpawnRoomReveal()

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
            // AXIS spreads arms wide to command rooms into position
            var backdrop1 = GetParent()?.GetNodeOrNull<DungeonBackdrop>("DungeonBackdrop");
            backdrop1?.AXIS?.CommandAssembly();

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

            if (_celebrationRings.TryGetValue(gridPos, out var ring))
            {
                if (IsInstanceValid(ring))
                    ring.QueueFree();
                _celebrationRings.Remove(gridPos);
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

            foreach (var (_, ring) in _celebrationRings)
            {
                if (IsInstanceValid(ring))
                    ring.QueueFree();
            }
            _celebrationRings.Clear();

            if (_sweepPlane != null && IsInstanceValid(_sweepPlane))
                _sweepPlane.QueueFree();
            _sweepPlane = null;

            _fogManager.Initialize(_generator);

            // AXIS returns to idle surveillance — restore default transform
            var backdrop2 = GetParent()?.GetNodeOrNull<DungeonBackdrop>("DungeonBackdrop");
            if (backdrop2?.AXIS != null)
            {
                backdrop2.AXIS.Scale = _axisOriginalScale;
                backdrop2.AXIS.Position = Vector3.Zero;
                backdrop2.AXIS.Rotation = Vector3.Zero;
                backdrop2.AXIS.GoIdle();
            }

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

        /// <summary>
        /// Spawn an expanding torus ring that scales out from room center and fades.
        /// Rotated 90° on X so it lies flat, then the ring expands horizontally.
        /// </summary>
        private void SpawnCelebrationRing(Node3D parent, Vector3 worldPos, Color color,
            Vector2 roomSize, float delay = 0f)
        {
            var ring = new MeshInstance3D();
            var torus = new TorusMesh();
            torus.InnerRadius = 0.5f;
            torus.OuterRadius = 1.5f;
            ring.Mesh = torus;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(color.R, color.G, color.B, 0.8f);
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.EmissionEnabled = true;
            mat.Emission = color;
            mat.EmissionEnergyMultiplier = 5f;
            mat.NoDepthTest = true;
            ring.MaterialOverride = mat;

            ring.Scale = Vector3.One * 0.5f;

            parent.AddChild(ring);
            ring.GlobalPosition = worldPos + Vector3.Up * 1.5f;
            // Set global rotation AFTER AddChild so parent rotation doesn't tilt the ring
            ring.GlobalRotationDegrees = new Vector3(90, 0, 0);

            float maxRadius = roomSize.X * 0.6f;

            // Animate: expand + fade out
            var tween = CreateTween();
            if (delay > 0f)
                tween.TweenInterval(delay);
            tween.TweenProperty(ring, "scale", Vector3.One * maxRadius, 0.8f)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
            tween.Parallel().TweenProperty(mat, "albedo_color",
                new Color(color.R, color.G, color.B, 0f), 1.0f)
                .SetEase(Tween.EaseType.In);
            tween.Parallel().TweenProperty(mat, "emission_energy_multiplier", 0f, 1.0f)
                .SetEase(Tween.EaseType.In);
            tween.TweenCallback(Callable.From(() =>
            {
                if (IsInstanceValid(ring)) ring.QueueFree();
            }));
        }

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
