using System;
using System.Collections.Generic;
using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Procedural dungeon layout generator. Random-walk on grid,
    /// assigns room types, builds geometry, connects with corridors.
    /// </summary>
    public class DungeonGenerator
    {
        public const int GRID_SIZE = 8;
        public const float ROOM_SPACING = 35f;

        private readonly FloorData _floorData;
        private readonly RandomNumberGenerator _rng = new();

        private readonly Dictionary<Vector2I, RoomType> _roomGrid = new();
        private readonly List<Vector2I> _mainPath = new();

        public IReadOnlyDictionary<Vector2I, RoomType> RoomGrid => _roomGrid;
        public IReadOnlyList<Vector2I> MainPath => _mainPath;

        public DungeonGenerator(FloorData floorData)
        {
            _floorData = floorData;
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
            int targetRooms = _rng.RandiRange(_floorData.MinRooms, _floorData.MaxRooms);

            // Random walk to create main path
            var current = new Vector2I(GRID_SIZE / 2, GRID_SIZE / 2);
            _mainPath.Add(current);
            _roomGrid[current] = RoomType.Entrance;

            var directions = new Vector2I[]
            {
                new(0, -1), new(0, 1), new(-1, 0), new(1, 0)
            };

            int attempts = 0;
            while (_mainPath.Count < targetRooms && attempts < 200)
            {
                attempts++;
                var dir = directions[_rng.RandiRange(0, 3)];
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
            // Try to add treasure and safe rooms branching off the main path
            var branchTypes = new[] { RoomType.Treasure, RoomType.SafeRoom };
            int branchesAdded = 0;

            foreach (var branchType in branchTypes)
            {
                for (int retry = 0; retry < 20 && branchesAdded < 3; retry++)
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
                var roomSize = RoomBuilder.GetRoomSize(roomType);

                // Determine door openings
                bool doorN = HasRoom(gridPos + new Vector2I(0, -1));
                bool doorS = HasRoom(gridPos + new Vector2I(0, 1));
                bool doorE = HasRoom(gridPos + new Vector2I(1, 0));
                bool doorW = HasRoom(gridPos + new Vector2I(-1, 0));

                var roomGeometry = RoomBuilder.BuildRoom(worldPos, roomSize, roomType,
                    doorN, doorS, doorE, doorW);
                roomGeometry.Name = $"Room_{gridPos.X}_{gridPos.Y}_{roomType}";

                // Create room controller
                var controller = new RoomController();
                controller.RoomType = roomType;
                controller.GridPosition = gridPos;
                controller.Initialize(_floorData);

                roomGeometry.AddChild(controller);
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

            trigger.BodyEntered += (body) =>
            {
                if (body.IsInGroup(Constants.GROUP_PLAYER))
                {
                    // Check if boss room is cleared
                    RoomController controller = null;
                    foreach (var child in roomNode.GetChildren())
                    {
                        if (child is RoomController rc) { controller = rc; break; }
                    }
                    if (controller != null && controller.IsCleared)
                    {
                        GD.Print("[DungeonGenerator] Safe room portal activated! Moving to next area.");
                        GameManager.Instance?.AdvanceArea();
                    }
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
