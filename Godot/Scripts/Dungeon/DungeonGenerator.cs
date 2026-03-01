using System;
using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Procedural dungeon layout generator. Random-walk on grid,
    /// assigns room types, builds geometry, connects with corridors.
    /// </summary>
    public class DungeonGenerator
    {
        public const int GRID_SIZE = 12;
        public const float ROOM_SPACING = 40f;

        private readonly SectorData _sectorData;
        private readonly RandomNumberGenerator _rng = new();

        private readonly Dictionary<Vector2I, RoomType> _roomGrid = new();
        private readonly List<Vector2I> _mainPath = new();

        public IReadOnlyDictionary<Vector2I, RoomType> RoomGrid => _roomGrid;
        public IReadOnlyList<Vector2I> MainPath => _mainPath;

        public DungeonGenerator(SectorData sectorData)
        {
            _sectorData = sectorData;
            _rng.Randomize();
        }

        /// <summary>
        /// Generate the dungeon layout and instantiate all rooms as children of the given parent.
        /// Returns the world position of the entrance room's spawn point.
        /// </summary>
        public Vector3 Generate(Node3D parent)
        {
            GenerateLayout();
            return BuildRooms(parent);
        }

        private void GenerateLayout()
        {
            int targetRooms = _rng.RandiRange(_sectorData.MinRooms, _sectorData.MaxRooms);

            // Random walk with directional momentum to create main path
            var current = new Vector2I(GRID_SIZE / 2, GRID_SIZE / 2);
            _mainPath.Add(current);
            _roomGrid[current] = RoomType.Entrance;

            var directions = new Vector2I[]
            {
                new(0, -1), new(0, 1), new(-1, 0), new(1, 0)
            };

            int lastDirIdx = _rng.RandiRange(0, 3);
            int attempts = 0;
            while (_mainPath.Count < targetRooms && attempts < 500)
            {
                attempts++;

                // 60% chance to continue same direction — creates elongated paths
                int dirIdx;
                if (_rng.Randf() < 0.6f)
                    dirIdx = lastDirIdx;
                else
                    dirIdx = _rng.RandiRange(0, 3);

                var dir = directions[dirIdx];
                var next = current + dir;

                // Bounds check
                if (next.X < 0 || next.X >= GRID_SIZE || next.Y < 0 || next.Y >= GRID_SIZE)
                    continue;

                // Don't revisit
                if (_roomGrid.ContainsKey(next))
                    continue;

                _mainPath.Add(next);
                _roomGrid[next] = RoomType.Combat;
                current = next;
                lastDirIdx = dirIdx;
            }

            // Mark the last room as boss
            if (_mainPath.Count > 2)
            {
                var bossPos = _mainPath[^1];
                _roomGrid[bossPos] = RoomType.Boss;
            }

            // Add branch rooms
            AddBranchRooms(directions);
        }

        private void AddBranchRooms(Vector2I[] directions)
        {
            var branchTypes = new[] { RoomType.Treasure, RoomType.SafeRoom, RoomType.Event, RoomType.Shop };
            int maxBranches = Mathf.Max(3, _mainPath.Count / 3);
            int branchesAdded = 0;

            foreach (var branchType in branchTypes)
            {
                if (branchesAdded >= maxBranches) break;

                for (int retry = 0; retry < 30; retry++)
                {
                    // Pick a random main path room (not entrance or boss)
                    int pathIdx = _rng.RandiRange(1, Mathf.Max(1, _mainPath.Count - 2));
                    var basePos = _mainPath[pathIdx];

                    var dir = directions[_rng.RandiRange(0, 3)];
                    var branchPos = basePos + dir;

                    if (branchPos.X < 0 || branchPos.X >= GRID_SIZE ||
                        branchPos.Y < 0 || branchPos.Y >= GRID_SIZE)
                        continue;

                    if (_roomGrid.ContainsKey(branchPos))
                        continue;

                    _roomGrid[branchPos] = branchType;
                    branchesAdded++;

                    // 50% chance for a 2-deep branch (treasure at end rewards exploration)
                    if (_rng.Randf() < 0.5f && branchesAdded < maxBranches)
                    {
                        var dir2 = directions[_rng.RandiRange(0, 3)];
                        var deepPos = branchPos + dir2;

                        if (deepPos.X >= 0 && deepPos.X < GRID_SIZE &&
                            deepPos.Y >= 0 && deepPos.Y < GRID_SIZE &&
                            !_roomGrid.ContainsKey(deepPos))
                        {
                            // Deep branch gets a reward room type
                            var deepType = branchType == RoomType.Event ? RoomType.Treasure : RoomType.Event;
                            _roomGrid[deepPos] = deepType;
                            branchesAdded++;
                        }
                    }

                    break;
                }
            }
        }

        private Vector3 BuildRooms(Node3D parent)
        {
            Vector3 entranceSpawn = Vector3.Zero;

            var roomControllers = new Dictionary<Vector2I, RoomController>();

            foreach (var (gridPos, roomType) in _roomGrid)
            {
                var worldPos = GridToWorld(gridPos);
                var roomSize = RoomBuilder.GetRoomSize(roomType, gridPos.GetHashCode());

                // Determine door openings
                bool doorN = HasRoom(gridPos + new Vector2I(0, -1));
                bool doorS = HasRoom(gridPos + new Vector2I(0, 1));
                bool doorE = HasRoom(gridPos + new Vector2I(1, 0));
                bool doorW = HasRoom(gridPos + new Vector2I(-1, 0));

                var roomGeometry = RoomBuilder.BuildRoom(worldPos, roomSize, roomType,
                    doorN, doorS, doorE, doorW, _sectorData);
                roomGeometry.Name = $"Room_{gridPos.X}_{gridPos.Y}_{roomType}";

                // Create room controller
                var controller = new RoomController();
                controller.RoomType = roomType;
                controller.GridPosition = gridPos;
                controller.Initialize(_sectorData);

                roomGeometry.AddChild(controller);

                // Visibility culling — hide distant rooms to save GPU
                AddVisibilityCulling(roomGeometry, roomSize);

                parent.AddChild(roomGeometry);
                roomControllers[gridPos] = controller;

                if (roomType == RoomType.Entrance)
                    entranceSpawn = worldPos + new Vector3(0, 0.9f, 0);

                // Add safe room portal for boss room
                if (roomType == RoomType.Boss)
                    AddSafeRoomPortal(roomGeometry);
            }

            // Build corridors between adjacent rooms
            BuildCorridors(parent);

            return entranceSpawn;
        }

        private static void AddVisibilityCulling(Node3D roomNode, Vector2 roomSize)
        {
            var notifier = new VisibleOnScreenNotifier3D();
            // Generous AABB — extend well beyond room bounds so rooms
            // become visible before the player reaches them
            float padW = roomSize.X / 2f + ROOM_SPACING * 0.4f;
            float padH = roomSize.Y / 2f + ROOM_SPACING * 0.4f;
            notifier.Aabb = new Aabb(
                new Vector3(-padW, -2f, -padH),
                new Vector3(padW * 2f, 10f, padH * 2f)
            );
            roomNode.AddChild(notifier);

            // Find the room controller (added before this call)
            RoomController controller = null;
            foreach (var child in roomNode.GetChildren())
            {
                if (child is RoomController rc) { controller = rc; break; }
            }

            notifier.ScreenExited += () =>
            {
                // NEVER cull rooms the player is currently inside
                if (controller != null && controller.IsEntered && !controller.IsCleared)
                    return;

                // Only disable lights and particles — leave meshes/physics alone.
                // Setting roomNode.Visible = false would hide floors, enemies, and
                // the player, causing the invisibility bugs.
                SetLightsAndParticlesEnabled(roomNode, false);
            };
            notifier.ScreenEntered += () =>
            {
                SetLightsAndParticlesEnabled(roomNode, true);
            };
        }

        private static void SetLightsAndParticlesEnabled(Node root, bool enabled)
        {
            foreach (var child in root.GetChildren())
            {
                if (child is OmniLight3D light)
                    light.Visible = enabled;
                else if (child is GpuParticles3D particles)
                    particles.Emitting = enabled;

                // Don't recurse into RoomController — enemies live there
                // and we don't want to touch their lights/particles
                if (child is RoomController)
                    continue;

                if (child is Node node && node.GetChildCount() > 0)
                    SetLightsAndParticlesEnabled(node, enabled);
            }
        }

        private void BuildCorridors(Node3D parent)
        {
            var processed = new HashSet<(Vector2I, Vector2I)>();

            foreach (var gridPos in _roomGrid.Keys)
            {
                var neighbors = new Vector2I[]
                {
                    gridPos + new Vector2I(0, -1),
                    gridPos + new Vector2I(0, 1),
                    gridPos + new Vector2I(1, 0),
                    gridPos + new Vector2I(-1, 0)
                };

                foreach (var neighbor in neighbors)
                {
                    if (!_roomGrid.ContainsKey(neighbor)) continue;

                    var key = gridPos.X < neighbor.X || (gridPos.X == neighbor.X && gridPos.Y < neighbor.Y)
                        ? (gridPos, neighbor) : (neighbor, gridPos);

                    if (processed.Contains(key)) continue;
                    processed.Add(key);

                    var fromWorld = GridToWorld(gridPos);
                    var toWorld = GridToWorld(neighbor);

                    // Calculate room half-extents along the corridor axis
                    var fromSize = RoomBuilder.GetRoomSize(_roomGrid[gridPos]);
                    var toSize = RoomBuilder.GetRoomSize(_roomGrid[neighbor]);
                    bool isXAxis = Mathf.Abs(neighbor.X - gridPos.X) > 0;
                    float fromHalf = isXAxis ? fromSize.X / 2f : fromSize.Y / 2f;
                    float toHalf = isXAxis ? toSize.X / 2f : toSize.Y / 2f;

                    var corridor = RoomBuilder.BuildCorridor(fromWorld, toWorld, fromHalf, toHalf);
                    corridor.Name = $"Corridor_{gridPos}_{neighbor}";
                    parent.AddChild(corridor);
                }
            }
        }

        private void AddSafeRoomPortal(Node3D roomNode)
        {
            var trigger = new Area3D();
            trigger.CollisionLayer = 0;
            trigger.CollisionMask = Constants.MASK_PLAYER;
            trigger.Position = new Vector3(0, 1, 0);
            roomNode.AddChild(trigger);

            var shape = new CollisionShape3D();
            var box = new BoxShape3D();
            box.Size = new Vector3(3, 3, 3);
            shape.Shape = box;
            trigger.AddChild(shape);

            // Visual marker — blue/white portal glow
            var mesh = new MeshInstance3D();
            var cylinder = new CylinderMesh();
            cylinder.TopRadius = 1f;
            cylinder.BottomRadius = 1.5f;
            cylinder.Height = 0.3f;
            mesh.Mesh = cylinder;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.3f, 0.5f, 0.9f);
            mat.EmissionEnabled = true;
            mat.Emission = new Color(0.2f, 0.4f, 0.8f);
            mat.EmissionEnergyMultiplier = 1.5f;
            mesh.MaterialOverride = mat;
            mesh.Position = new Vector3(0, 0, 0);
            trigger.AddChild(mesh);

            // Label above portal
            var label3d = new Label3D();
            label3d.Text = "Safe Room";
            label3d.FontSize = 48;
            label3d.Position = new Vector3(0, 2.5f, 0);
            label3d.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            label3d.Modulate = new Color(0.5f, 0.7f, 1f);
            label3d.OutlineModulate = new Color(0, 0, 0);
            label3d.OutlineSize = 4;
            trigger.AddChild(label3d);

            // Find the room controller
            RoomController controller = null;
            foreach (var child in roomNode.GetChildren())
            {
                if (child is RoomController rc) { controller = rc; break; }
            }

            bool activated = false;

            void TryActivatePortal()
            {
                if (activated) return;
                if (controller == null || !controller.IsCleared) return;

                // Check if player is currently overlapping the trigger
                foreach (var body in trigger.GetOverlappingBodies())
                {
                    if (body.IsInGroup(Constants.GROUP_PLAYER))
                    {
                        activated = true;
                        GD.Print("[DungeonGenerator] Safe room portal activated! Moving to next area.");
                        GameManager.Instance?.CallDeferred(nameof(GameManager.AdvanceArea));
                        return;
                    }
                }
            }

            // Activate when player walks onto portal (if room already cleared)
            trigger.BodyEntered += (body) =>
            {
                if (body.IsInGroup(Constants.GROUP_PLAYER))
                    TryActivatePortal();
            };

            // Also activate when room is cleared (player might already be on the portal)
            GameEvents.OnRoomCleared += (clearedRoom) =>
            {
                if (clearedRoom is RoomController rc && rc == controller)
                {
                    // Defer to next frame so physics overlap state is current
                    var tree = trigger.GetTree();
                    if (tree != null)
                        tree.CreateTimer(0.1f).Timeout += TryActivatePortal;
                }
            };
        }

        private bool HasRoom(Vector2I pos) => _roomGrid.ContainsKey(pos);

        private static Vector3 GridToWorld(Vector2I gridPos)
        {
            return new Vector3(gridPos.X * ROOM_SPACING, 0, gridPos.Y * ROOM_SPACING);
        }
    }
}
