using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Procedural dungeon layout generator. Creates a main spine path from
    /// entrance to boss, then grows branch corridors to fill the target room
    /// count (40 by default). Room types are assigned based on sector config.
    /// </summary>
    public class DungeonGenerator
    {
        public const int GRID_SIZE = 20;
        public const float ROOM_SPACING = 32f;

        private readonly SectorData _sectorData;
        private readonly RandomNumberGenerator _rng = new();

        private readonly Dictionary<Vector2I, RoomType> _roomGrid = new();
        private readonly List<Vector2I> _mainPath = new();
        private readonly List<Vector2I> _allPositions = new();
        private readonly Dictionary<Vector2I, RoomController> _roomControllers = new();
        private Vector2I _bossPosition;

        public IReadOnlyDictionary<Vector2I, RoomType> RoomGrid => _roomGrid;
        public IReadOnlyList<Vector2I> MainPath => _mainPath;
        public IReadOnlyDictionary<Vector2I, RoomController> RoomControllers => _roomControllers;
        public Vector2I BossPosition => _bossPosition;

        private static readonly Vector2I[] Directions =
        {
            new(0, -1), new(0, 1), new(-1, 0), new(1, 0)
        };

        public DungeonGenerator(SectorData sectorData)
        {
            _sectorData = sectorData;
            _rng.Randomize();
        }

        public Vector3 Generate(Node3D parent)
        {
            GenerateLayout();
            var spawn = BuildRooms(parent);
            TryMarkDiscipleRoom();
            return spawn;
        }

        /// <summary>
        /// 5% chance per floor: mark one random combat room for an AXIS Disciple encounter.
        /// </summary>
        private void TryMarkDiscipleRoom()
        {
            if (_rng.Randf() > 0.05f) return;

            var combatRooms = new System.Collections.Generic.List<RoomController>();
            foreach (var (_, controller) in _roomControllers)
            {
                if (controller.RoomType == RoomType.Combat)
                    combatRooms.Add(controller);
            }

            if (combatRooms.Count == 0) return;

            var chosen = combatRooms[_rng.RandiRange(0, combatRooms.Count - 1)];
            chosen.HasAxisDisciple = true;
            GD.Print($"[DungeonGenerator] AXIS Disciple marked in room at {chosen.GridPosition}");
        }

        private void GenerateLayout()
        {
            int totalTarget = _sectorData.TotalRooms;
            int spineLength = Mathf.Clamp(totalTarget / 3, 8, 15);

            // Retry layout generation if boss room is unreachable (up to 5 attempts)
            for (int attempt = 0; attempt < 5; attempt++)
            {
                _roomGrid.Clear();
                _mainPath.Clear();
                _allPositions.Clear();

                // Phase 1: Generate the main spine — entrance to boss
                GenerateSpine(spineLength);

                // Phase 2: Grow branches off the spine and existing rooms until we hit target
                GrowBranches(totalTarget);

                // Validate: entrance must be able to reach boss through adjacent rooms
                var entrance = _mainPath[0];
                if (IsReachable(entrance, _bossPosition))
                {
                    // Phase 3: Assign room types based on sector distribution
                    AssignRoomTypes();

                    GD.Print($"[DungeonGenerator] Layout: {_roomGrid.Count} rooms " +
                        $"(spine={_mainPath.Count}, target={totalTarget}, attempt={attempt + 1})");
                    return;
                }

                GD.PrintErr($"[DungeonGenerator] Boss unreachable on attempt {attempt + 1}, regenerating...");
                _rng.Randomize();
            }

            // Fallback: force a direct path from entrance to boss
            GD.PrintErr("[DungeonGenerator] Could not generate reachable layout, forcing spine path");
            AssignRoomTypes();
        }

        /// <summary>
        /// BFS to check if two grid positions are connected through adjacent rooms.
        /// </summary>
        private bool IsReachable(Vector2I from, Vector2I to)
        {
            var visited = new HashSet<Vector2I>();
            var queue = new Queue<Vector2I>();
            queue.Enqueue(from);
            visited.Add(from);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == to) return true;

                foreach (var dir in Directions)
                {
                    var neighbor = current + dir;
                    if (!visited.Contains(neighbor) && _roomGrid.ContainsKey(neighbor))
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Random walk from center to create the main path. Boss goes at the end.
        /// </summary>
        private void GenerateSpine(int targetLength)
        {
            var center = new Vector2I(GRID_SIZE / 2, GRID_SIZE / 2);
            _mainPath.Add(center);
            _roomGrid[center] = RoomType.Entrance;
            _allPositions.Add(center);

            var current = center;
            int lastDirIdx = _rng.RandiRange(0, 3);
            int attempts = 0;

            while (_mainPath.Count < targetLength && attempts < 1000)
            {
                attempts++;

                // 55% momentum, 45% random — slightly more winding than before
                int dirIdx = _rng.Randf() < 0.55f ? lastDirIdx : _rng.RandiRange(0, 3);
                var next = current + Directions[dirIdx];

                if (!InBounds(next) || _roomGrid.ContainsKey(next))
                    continue;

                _mainPath.Add(next);
                _roomGrid[next] = RoomType.Combat;
                _allPositions.Add(next);
                current = next;
                lastDirIdx = dirIdx;
            }

            // Mark last spine room as boss
            if (_mainPath.Count > 2)
            {
                _bossPosition = _mainPath[^1];
                _roomGrid[_bossPosition] = RoomType.Boss;
            }
        }

        /// <summary>
        /// Grow branch corridors off existing rooms until we reach the target count.
        /// Branches create a tree-like structure — exploration is rewarded by depth.
        /// </summary>
        private void GrowBranches(int totalTarget)
        {
            int attempts = 0;
            int maxAttempts = totalTarget * 20;

            while (_roomGrid.Count < totalTarget && attempts < maxAttempts)
            {
                attempts++;

                // Pick a random existing room to branch from (prefer spine for early branches)
                Vector2I basePos;
                if (_roomGrid.Count < totalTarget * 0.6f && _rng.Randf() < 0.7f)
                    basePos = _mainPath[_rng.RandiRange(1, _mainPath.Count - 2)];
                else
                    basePos = _allPositions[_rng.RandiRange(0, _allPositions.Count - 1)];

                // Skip branching from boss room
                if (basePos == _bossPosition) continue;

                var dir = Directions[_rng.RandiRange(0, 3)];
                var branchPos = basePos + dir;

                if (!InBounds(branchPos) || _roomGrid.ContainsKey(branchPos))
                    continue;

                _roomGrid[branchPos] = RoomType.Combat;
                _allPositions.Add(branchPos);

                // 40% chance to extend the branch 1-3 more rooms deep
                if (_rng.Randf() < 0.4f)
                {
                    var current = branchPos;
                    int branchDepth = _rng.RandiRange(1, 3);
                    int branchDirIdx = _rng.RandiRange(0, 3);

                    for (int d = 0; d < branchDepth && _roomGrid.Count < totalTarget; d++)
                    {
                        // 60% momentum for branch direction
                        int nextDirIdx = _rng.Randf() < 0.6f ? branchDirIdx : _rng.RandiRange(0, 3);
                        var next = current + Directions[nextDirIdx];

                        if (!InBounds(next) || _roomGrid.ContainsKey(next))
                            break;

                        _roomGrid[next] = RoomType.Combat;
                        _allPositions.Add(next);
                        current = next;
                        branchDirIdx = nextDirIdx;
                    }
                }
            }
        }

        /// <summary>
        /// Assign room types to the generated positions. Entrance and Boss are already set.
        /// Special rooms go on branch tips (dead ends) and mid-branches for exploration reward.
        /// </summary>
        private void AssignRoomTypes()
        {
            // Identify candidate rooms — all combat rooms (not entrance/boss)
            var candidates = _allPositions
                .Where(p => _roomGrid[p] == RoomType.Combat)
                .ToList();

            // Shuffle candidates
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = _rng.RandiRange(0, i);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            // Sort so dead ends (fewer neighbors) come first — special rooms reward exploration
            candidates = candidates
                .OrderBy(p => CountNeighbors(p))
                .ThenBy(_ => _rng.Randf())
                .ToList();

            int idx = 0;

            // Assign treasure rooms — probability-based if TreasureRoomChance > 0, otherwise guaranteed count
            if (_sectorData.TreasureRoomChance > 0f)
            {
                int treasurePlaced = 0;
                for (int i = idx; i < candidates.Count && treasurePlaced < _sectorData.MaxTreasureRooms; i++)
                {
                    if (_rng.Randf() < _sectorData.TreasureRoomChance)
                    {
                        _roomGrid[candidates[i]] = RoomType.Treasure;
                        treasurePlaced++;
                    }
                }
            }
            else
            {
                idx = AssignType(candidates, idx, RoomType.Treasure, _sectorData.TreasureRooms);
            }
            idx = AssignType(candidates, idx, RoomType.Event, _sectorData.EventRooms);
            idx = AssignType(candidates, idx, RoomType.Shop, _sectorData.ShopRooms);
            idx = AssignType(candidates, idx, RoomType.Puzzle, _sectorData.PuzzleRooms);

            // Safe room: 10% chance per floor (0 or 1)
            if (_rng.Randf() < _sectorData.SafeRoomChance && idx < candidates.Count)
            {
                _roomGrid[candidates[idx]] = RoomType.SafeRoom;
                idx++;
            }

            // Megabonk rooms: roll per eligible slot, capped
            if (_sectorData.MegabonkChance > 0 && _sectorData.MaxMegabonkRooms > 0)
            {
                int megabonkPlaced = 0;
                for (int i = idx; i < candidates.Count && megabonkPlaced < _sectorData.MaxMegabonkRooms; i++)
                {
                    if (_rng.Randf() < _sectorData.MegabonkChance)
                    {
                        _roomGrid[candidates[i]] = RoomType.Megabonk;
                        megabonkPlaced++;
                    }
                }
            }

            // Count final distribution
            int combatCount = _roomGrid.Values.Count(t => t == RoomType.Combat);
            int specialCount = _roomGrid.Count - combatCount - 2; // minus entrance and boss
            GD.Print($"[DungeonGenerator] Room types: {combatCount} combat, {specialCount} special, " +
                $"1 entrance, 1 boss");
        }

        private int AssignType(List<Vector2I> candidates, int startIdx, RoomType type, int count)
        {
            int placed = 0;
            for (int i = startIdx; i < candidates.Count && placed < count; i++)
            {
                if (_roomGrid[candidates[i]] == RoomType.Combat)
                {
                    _roomGrid[candidates[i]] = type;
                    placed++;
                    // Swap placed item to startIdx region so we advance past it
                    (candidates[startIdx + placed - 1], candidates[i]) = (candidates[i], candidates[startIdx + placed - 1]);
                }
            }
            return startIdx + placed;
        }

        private int CountNeighbors(Vector2I pos)
        {
            int count = 0;
            foreach (var dir in Directions)
            {
                if (_roomGrid.ContainsKey(pos + dir))
                    count++;
            }
            return count;
        }

        private static bool InBounds(Vector2I pos)
        {
            return pos.X >= 0 && pos.X < GRID_SIZE && pos.Y >= 0 && pos.Y < GRID_SIZE;
        }

        private Vector3 BuildRooms(Node3D parent)
        {
            Vector3 entranceSpawn = Vector3.Zero;

            foreach (var (gridPos, roomType) in _roomGrid)
            {
                var worldPos = GridToWorld(gridPos);
                var roomSize = RoomBuilder.GetRoomSize(roomType, gridPos.GetHashCode());

                // Determine door openings
                bool doorN = HasRoom(gridPos + new Vector2I(0, -1));
                bool doorS = HasRoom(gridPos + new Vector2I(0, 1));
                bool doorE = HasRoom(gridPos + new Vector2I(1, 0));
                bool doorW = HasRoom(gridPos + new Vector2I(-1, 0));

                var roomShape = GetRoomShape(roomType, gridPos);
                var roomGeometry = RoomBuilder.BuildRoom(worldPos, roomSize, roomType,
                    doorN, doorS, doorE, doorW, _sectorData, roomShape, gridPos);
                roomGeometry.Name = $"Room_{gridPos.X}_{gridPos.Y}_{roomType}";

                // Create room controller
                var controller = new RoomController();
                controller.RoomType = roomType;
                controller.GridPosition = gridPos;
                controller.Initialize(_sectorData);

                roomGeometry.AddChild(controller);

                // Visibility culling — hide distant rooms to save GPU
                AddVisibilityCulling(roomGeometry, roomSize, roomType);

                parent.AddChild(roomGeometry);
                _roomControllers[gridPos] = controller;

                if (roomType == RoomType.Entrance)
                    entranceSpawn = worldPos + new Vector3(0, 0.9f, 0);

                // Add safe room portal for boss room
                if (roomType == RoomType.Boss)
                    AddSafeRoomPortal(roomGeometry);
            }

            return entranceSpawn;
        }

        private static void AddVisibilityCulling(Node3D roomNode, Vector2 roomSize, RoomType roomType)
        {
            // Never cull the entrance room — player spawns there and needs to see it immediately
            if (roomType == RoomType.Entrance) return;

            var notifier = new VisibleOnScreenNotifier3D();
            // Generous AABB — extend well beyond room bounds so rooms
            // become visible before the player reaches them
            float padW = roomSize.X / 2f + 20f;
            float padH = roomSize.Y / 2f + 20f;
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

        private void AddSafeRoomPortal(Node3D roomNode)
        {
            var trigger = new Area3D();
            trigger.CollisionLayer = 0;
            trigger.CollisionMask = Constants.MASK_PLAYER;
            trigger.Position = new Vector3(0, 1, 0);
            roomNode.AddChild(trigger);

            var shape = new CollisionShape3D();
            var box = new BoxShape3D();
            box.Size = new Vector3(5, 4, 5);
            shape.Shape = box;
            trigger.AddChild(shape);

            // Visual marker — starts dim, lights up on room clear
            var mesh = new MeshInstance3D();
            var cylinder = new CylinderMesh();
            cylinder.TopRadius = 1f;
            cylinder.BottomRadius = 1.5f;
            cylinder.Height = 0.3f;
            mesh.Mesh = cylinder;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.15f, 0.2f, 0.3f);
            mat.EmissionEnabled = true;
            mat.Emission = new Color(0.1f, 0.15f, 0.25f);
            mat.EmissionEnergyMultiplier = 0.3f;
            mesh.MaterialOverride = mat;
            mesh.Position = new Vector3(0, 0, 0);
            trigger.AddChild(mesh);

            // Label above portal — hidden until cleared
            var label3d = new Label3D();
            label3d.Text = "LOCKED";
            label3d.FontSize = 48;
            label3d.Position = new Vector3(0, 2.5f, 0);
            label3d.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            label3d.Modulate = new Color(0.4f, 0.4f, 0.4f);
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
            bool unlocked = false;

            void UnlockPortal()
            {
                if (unlocked) return;
                unlocked = true;

                // Light up the portal
                mat.AlbedoColor = new Color(0.3f, 0.6f, 1f);
                mat.Emission = new Color(0.3f, 0.5f, 1f);
                mat.EmissionEnergyMultiplier = 2.5f;

                // Update label
                label3d.Text = "Safe Room";
                label3d.Modulate = new Color(0.5f, 0.8f, 1f);

                // Add swirl particles
                var particles = VfxFactory.CreatePortalParticles(new Color(0.3f, 0.5f, 1f));
                particles.Position = Vector3.Up * 0.5f;
                trigger.AddChild(particles);

                // Add light
                var light = new OmniLight3D();
                light.LightColor = new Color(0.3f, 0.5f, 1f);
                light.LightEnergy = 2f;
                light.OmniRange = 8f;
                light.Position = new Vector3(0, 2f, 0);
                trigger.AddChild(light);

                // Play sound
                if (ServiceLocator.TryGet<AudioManager>(out var audio))
                    audio.PlaySFXByName("level_up");

                GD.Print("[DungeonGenerator] Safe room portal UNLOCKED!");
            }

            void TryActivatePortal()
            {
                if (activated) return;
                if (!unlocked) return;

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

            // Unlock portal visuals when room is cleared, then check for activation
            GameEvents.OnRoomCleared += (clearedRoom) =>
            {
                if (clearedRoom is RoomController rc && rc == controller)
                {
                    UnlockPortal();
                    // Defer activation check so physics overlap state is current
                    var tree = trigger.GetTree();
                    if (tree != null)
                        tree.CreateTimer(0.2f).Timeout += TryActivatePortal;
                }
            };
        }

        private bool HasRoom(Vector2I pos) => _roomGrid.ContainsKey(pos);

        private static Vector3 GridToWorld(Vector2I gridPos)
        {
            return new Vector3(gridPos.X * ROOM_SPACING, 0, gridPos.Y * ROOM_SPACING);
        }

        /// <summary>
        /// Determine room shape for combat rooms. Non-combat rooms always get Rectangle.
        /// L-shaped and T-shaped wings extend beyond the 32x32 grid boundary and overlap
        /// with adjacent rooms, so only Rectangle and Partitioned are used.
        /// Distribution: 75% Rectangle, 25% Partitioned
        /// </summary>
        private static RoomShape GetRoomShape(RoomType type, Vector2I gridPos)
        {
            if (type != RoomType.Combat && type != RoomType.Megabonk) return RoomShape.Rectangle;

            // Deterministic from grid position
            int hash = gridPos.GetHashCode();
            int roll = ((hash % 100) + 100) % 100;

            if (roll < 75) return RoomShape.Rectangle;
            return RoomShape.Partitioned;
        }
    }
}
