using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Autoload that plays the game automatically for remote observation.
    /// Uses room-to-room navigation via the dungeon grid, fires abilities,
    /// and captures periodic screenshots for remote viewing.
    ///
    /// Enable via command line: --autoplay
    /// </summary>
    public partial class AutoPlayer : Node
    {
        private const float SCREENSHOT_INTERVAL = 2.0f;
        private const float ATTACK_RANGE = 12f;
        private const float INTERACT_RANGE = 3f;
        private const float MENU_AUTO_DELAY = 1.5f;
        private const int MAX_SCREENSHOTS = 50;
        private const float ROOM_ARRIVAL_THRESHOLD = 6f;
        private const float ABILITY_CHECK_INTERVAL = 1.5f;
        private const float STUCK_THRESHOLD = 3f;
        private const float STUCK_DISTANCE = 1.5f;

        private float _screenshotTimer;
        private float _menuDelayTimer;
        private RandomNumberGenerator _rng = new();
        private string _screenshotDir;
        private bool _enabled;
        private int _screenshotIndex;

        // State tracking
        private bool _menuHandled;
        private bool _classSelected;

        // Navigation
        private DungeonGenerator _generator;
        private Vector2I? _targetRoom;
        private Vector3 _targetWorldPos;
        private float _abilityTimer;

        // Stuck detection
        private Vector3 _lastPosition;
        private float _stuckTimer;

        public static AutoPlayer Instance { get; private set; }

        public override void _Ready()
        {
            // Only activate if launched with --autoplay
            foreach (var arg in OS.GetCmdlineArgs())
                if (arg == "--autoplay") { _enabled = true; break; }
            foreach (var arg in OS.GetCmdlineUserArgs())
                if (arg == "--autoplay") { _enabled = true; break; }

            if (!_enabled)
            {
                QueueFree();
                return;
            }

            Instance = this;
            ProcessMode = ProcessModeEnum.Always;

            _screenshotDir = ProjectSettings.GlobalizePath("user://autoplay_screenshots");
            if (!DirAccess.DirExistsAbsolute(_screenshotDir))
                DirAccess.MakeDirAbsolute(_screenshotDir);

            CleanScreenshots();

            GD.Print("[AutoPlayer] ACTIVE — screenshots: " + _screenshotDir);

            GameEvents.OnGameStateChanged += OnGameStateChanged;
            CallDeferred(nameof(AddAutoPlayLight));
        }

        public override void _ExitTree()
        {
            GameEvents.OnGameStateChanged -= OnGameStateChanged;
            if (Instance == this) Instance = null;
        }

        private void OnGameStateChanged(GameState state)
        {
            _menuHandled = false;
            _menuDelayTimer = MENU_AUTO_DELAY;
            _generator = null;
            _targetRoom = null;
            GD.Print($"[AutoPlayer] State changed to {state}");
        }

        public override void _Process(double delta)
        {
            if (!_enabled) return;
            float dt = (float)delta;

            _screenshotTimer -= dt;
            if (_screenshotTimer <= 0f)
            {
                _screenshotTimer = SCREENSHOT_INTERVAL;
                CaptureScreenshot();
            }

            _menuDelayTimer -= dt;
            if (_menuDelayTimer <= 0f && !_menuHandled)
                HandleMenuNavigation();

            TryDismissDeathScreen();
        }

        private void TryDismissDeathScreen()
        {
            var root = GetTree().Root;
            foreach (var child in root.GetChildren())
            {
                if (child is CanvasLayer canvas && canvas.Layer == 100)
                {
                    // Death screen
                    var tryAgainBtn = FindButton(canvas, "Try Again");
                    if (tryAgainBtn != null)
                    {
                        GD.Print("[AutoPlayer] Clicking 'Try Again' on death screen");
                        tryAgainBtn.EmitSignal("pressed");
                        return;
                    }
                    // Victory screen — enter ascension
                    var ascensionBtn = FindButton(canvas, "Enter Ascension");
                    ascensionBtn ??= FindButton(canvas, "Continue Ascending");
                    if (ascensionBtn != null)
                    {
                        GD.Print("[AutoPlayer] Clicking ascension button on victory screen");
                        ascensionBtn.EmitSignal("pressed");
                        return;
                    }
                }
            }
        }

        private static Button FindButton(Node root, string text)
        {
            if (root is Button btn && btn.Text == text) return btn;
            foreach (var child in root.GetChildren())
            {
                var found = FindButton(child, text);
                if (found != null) return found;
            }
            return null;
        }

        public override void _PhysicsProcess(double delta)
        {
            if (!_enabled) return;

            var gm = GameManager.Instance;
            if (gm == null) return;

            if (gm.CurrentState == GameState.InSector)
                HandleGameplay((float)delta);
        }

        private void HandleMenuNavigation()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            switch (gm.CurrentState)
            {
                case GameState.MainMenu:
                    _menuHandled = true;
                    GD.Print("[AutoPlayer] Auto-starting new game");
                    gm.GoToCharacterCreation();
                    break;

                case GameState.CharacterCreation:
                    if (!_classSelected)
                    {
                        _classSelected = true;
                        _menuHandled = true;
                        var classes = new[] {
                            BotFrameType.TinCan, BotFrameType.Scrapheap,
                            BotFrameType.SparkPlug, BotFrameType.RustBucket,
                            BotFrameType.NoiseBox, BotFrameType.Clunker
                        };
                        var pick = classes[_rng.RandiRange(0, classes.Length - 1)];
                        GD.Print($"[AutoPlayer] Auto-selecting class: {pick}");
                        gm.StartGameWithClass(pick);
                    }
                    break;

                case GameState.SafeRoom:
                    _menuHandled = true;
                    GD.Print("[AutoPlayer] Auto-continuing from safe room");
                    gm.ContinueFromSafeRoom();
                    break;
            }
        }

        private float _debugTimer;

        private void HandleGameplay(float dt)
        {
            var player = ServiceLocator.TryGet<PlayerController>(out var pc) ? pc : null;
            if (player == null)
            {
                _debugTimer -= dt;
                if (_debugTimer <= 0f)
                {
                    GD.Print("[AutoPlayer] No player found via ServiceLocator");
                    _debugTimer = 3f;
                }
                return;
            }

            var movement = player.Movement;
            var combat = player.Combat;
            if (movement == null || combat == null) return;

            // Disable human input handler
            var inputHandler = player.GetNodeOrNull<PlayerInputHandler>("PlayerInputHandler");
            if (inputHandler != null && inputHandler.ProcessMode != ProcessModeEnum.Disabled)
            {
                inputHandler.ProcessMode = ProcessModeEnum.Disabled;
                GD.Print("[AutoPlayer] Disabled PlayerInputHandler — bot is in control");
            }

            // Acquire dungeon generator reference
            if (_generator == null)
            {
                var sectorMgr = FindSectorManager(GetTree().Root);
                if (sectorMgr != null)
                {
                    _generator = sectorMgr.Generator;
                    GD.Print($"[AutoPlayer] Found DungeonGenerator with {_generator?.RoomGrid?.Count ?? 0} rooms");
                }
            }

            // Find enemies
            var enemies = player.GetTree().GetNodesInGroup(Constants.GROUP_ENEMY);
            Node3D nearestEnemy = null;
            float nearestDist = float.MaxValue;

            foreach (var node in enemies)
            {
                if (node is not Node3D enemy3d) continue;
                if (!GodotObject.IsInstanceValid(enemy3d)) continue;

                var health = enemy3d.GetNodeOrNull<HealthComponent>("HealthComponent");
                if (health != null && !health.IsAlive) continue;

                float dist = player.GlobalPosition.DistanceTo(enemy3d.GlobalPosition);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestEnemy = enemy3d;
                }
            }

            if (nearestEnemy != null && nearestDist < ATTACK_RANGE)
            {
                HandleCombat(player, movement, combat, nearestEnemy, nearestDist, dt);
            }
            else
            {
                HandleNavigation(player, movement, dt);
            }

            // Periodic debug
            _debugTimer -= dt;
            if (_debugTimer <= 0f)
            {
                _debugTimer = 5f;
                var grid = _generator != null ? WorldToGrid(player.GlobalPosition) : new Vector2I(-1, -1);
                GD.Print($"[AutoPlayer] pos={player.GlobalPosition:F1} grid={grid} target={_targetRoom} enemies={enemies.Count} gen={_generator != null}");
            }

            // Try abilities periodically
            _abilityTimer -= dt;
            if (_abilityTimer <= 0f && nearestEnemy != null && nearestDist < ATTACK_RANGE)
            {
                _abilityTimer = ABILITY_CHECK_INTERVAL;
                TryUseAbility(combat, nearestEnemy, nearestDist);
            }

            TryAutoInteract(player);
            DetectStuck(player, movement, dt);

            // Rescue player if they fall off the map
            if (player.GlobalPosition.Y < -5f)
            {
                var safePos = _generator != null
                    ? GridToWorld(WorldToGrid(_lastPosition)) + Vector3.Up * 1f
                    : new Vector3(320, 1, 320);
                player.GlobalPosition = safePos;
                _targetRoom = null;
                GD.Print($"[AutoPlayer] Rescued player from fall — warped to {safePos}");
            }
        }

        private void HandleCombat(PlayerController player, PlayerMovement movement,
            PlayerCombat combat, Node3D enemy, float dist, float dt)
        {
            var toEnemy = (enemy.GlobalPosition - player.GlobalPosition);
            toEnemy.Y = 0;
            var dir2d = new Vector2(toEnemy.X, toEnemy.Z).Normalized();

            // Kite: back away if too close, approach if too far
            if (dist < 2.5f)
            {
                // Strafe sideways while backing up
                var backDir = -dir2d;
                var strafeDir = new Vector2(-backDir.Y, backDir.X);
                movement.HandleDirectMove((backDir + strafeDir * 0.5f).Normalized());
            }
            else if (dist > 5f)
            {
                movement.HandleDirectMove(dir2d);
            }
            else
            {
                // Optimal range — circle strafe
                var strafeDir = new Vector2(-dir2d.Y, dir2d.X);
                movement.HandleDirectMove(strafeDir * 0.6f);
            }

            // Aim at enemy
            var camera = player.GetViewport().GetCamera3D();
            if (camera != null && !camera.IsPositionBehind(enemy.GlobalPosition))
            {
                var screenPos = camera.UnprojectPosition(
                    enemy.GlobalPosition + Vector3.Up * 0.8f);
                Input.WarpMouse(screenPos);
            }

            combat.HandleBasicAttack();
        }

        private void TryUseAbility(PlayerCombat combat, Node3D enemy, float dist)
        {
            // Try each ability slot, prefer AoE when multiple enemies nearby
            for (int i = 0; i < Constants.MAX_ABILITY_SLOTS; i++)
            {
                var slot = combat.GetSlot(i);
                if (slot == null || slot.IsEmpty || !slot.IsReady) continue;

                // Check if in range
                if (dist <= slot.Data.Range * 1.2f)
                {
                    combat.HandleAbilityInput(i);
                    GD.Print($"[AutoPlayer] Used ability slot {i}: {slot.Data.AbilityName}");
                    break;
                }
            }
        }

        private void HandleNavigation(PlayerController player, PlayerMovement movement, float dt)
        {
            if (_generator == null)
            {
                movement.HandleDirectMove(RandomDirection());
                return;
            }

            var playerGrid = WorldToGrid(player.GlobalPosition);

            if (!_targetRoom.HasValue || playerGrid == _targetRoom.Value ||
                player.GlobalPosition.FlatDistance(_targetWorldPos) < ROOM_ARRIVAL_THRESHOLD)
            {
                _targetRoom = PickNextRoom(playerGrid);
                if (_targetRoom.HasValue)
                    _targetWorldPos = GridToWorld(_targetRoom.Value);
            }

            if (_targetRoom.HasValue)
            {
                // Try nav agent first, fall back to direct move if stuck
                movement.NavigateTo(_targetWorldPos);

                // If barely moving, also push with direct input toward target
                float moved = player.GlobalPosition.FlatDistance(_lastPosition);
                if (moved < 0.3f * dt)
                {
                    var toTarget = (_targetWorldPos - player.GlobalPosition);
                    toTarget.Y = 0;
                    var dir = new Vector2(toTarget.X, toTarget.Z).Normalized();
                    movement.HandleDirectMove(dir);
                }
            }
            else
            {
                // Wander within current room
                var toTarget = new Vector2((float)GD.RandRange(-1, 1), (float)GD.RandRange(-1, 1)).Normalized();
                movement.HandleDirectMove(toTarget);
            }
        }

        private Vector2I? PickNextRoom(Vector2I currentGrid)
        {
            if (_generator == null) return null;

            // Priority 1: Adjacent uncleared combat/boss rooms
            var adjacent = GetAdjacentRooms(currentGrid);
            var unclearedCombat = new List<Vector2I>();
            var unclearedOther = new List<Vector2I>();
            var clearedRooms = new List<Vector2I>();

            foreach (var pos in adjacent)
            {
                if (!_generator.RoomControllers.TryGetValue(pos, out var rc)) continue;

                if (!rc.IsCleared && (rc.RoomType == RoomType.Combat || rc.RoomType == RoomType.Boss))
                    unclearedCombat.Add(pos);
                else if (!rc.IsCleared)
                    unclearedOther.Add(pos);
                else
                    clearedRooms.Add(pos);
            }

            if (unclearedCombat.Count > 0)
                return unclearedCombat[_rng.RandiRange(0, unclearedCombat.Count - 1)];
            if (unclearedOther.Count > 0)
                return unclearedOther[_rng.RandiRange(0, unclearedOther.Count - 1)];

            // Priority 2: BFS to find nearest uncleared room
            var target = BfsToUncleared(currentGrid);
            if (target.HasValue)
                return target;

            // Priority 3: Move toward boss room
            if (_generator.BossPosition != currentGrid)
                return BfsNextStep(currentGrid, _generator.BossPosition);

            // All cleared — wander to a random adjacent room
            if (clearedRooms.Count > 0)
                return clearedRooms[_rng.RandiRange(0, clearedRooms.Count - 1)];

            return null;
        }

        private List<Vector2I> GetAdjacentRooms(Vector2I pos)
        {
            var dirs = new[] {
                new Vector2I(0, -1), new Vector2I(0, 1),
                new Vector2I(-1, 0), new Vector2I(1, 0)
            };

            var result = new List<Vector2I>();
            foreach (var d in dirs)
            {
                var neighbor = pos + d;
                if (_generator.RoomGrid.ContainsKey(neighbor))
                    result.Add(neighbor);
            }
            return result;
        }

        private Vector2I? BfsToUncleared(Vector2I start)
        {
            var visited = new HashSet<Vector2I> { start };
            var queue = new Queue<(Vector2I pos, Vector2I firstStep)>();

            foreach (var adj in GetAdjacentRooms(start))
            {
                visited.Add(adj);
                queue.Enqueue((adj, adj));
            }

            while (queue.Count > 0)
            {
                var (pos, firstStep) = queue.Dequeue();

                if (_generator.RoomControllers.TryGetValue(pos, out var rc) && !rc.IsCleared)
                    return firstStep; // Return the first step toward it

                foreach (var adj in GetAdjacentRooms(pos))
                {
                    if (visited.Add(adj))
                        queue.Enqueue((adj, firstStep));
                }
            }

            return null;
        }

        private Vector2I? BfsNextStep(Vector2I start, Vector2I goal)
        {
            var visited = new HashSet<Vector2I> { start };
            var queue = new Queue<(Vector2I pos, Vector2I firstStep)>();

            foreach (var adj in GetAdjacentRooms(start))
            {
                if (adj == goal) return adj;
                visited.Add(adj);
                queue.Enqueue((adj, adj));
            }

            while (queue.Count > 0)
            {
                var (pos, firstStep) = queue.Dequeue();
                if (pos == goal) return firstStep;

                foreach (var adj in GetAdjacentRooms(pos))
                {
                    if (visited.Add(adj))
                        queue.Enqueue((adj, firstStep));
                }
            }

            return null;
        }

        private void DetectStuck(PlayerController player, PlayerMovement movement, float dt)
        {
            float movedDist = player.GlobalPosition.DistanceTo(_lastPosition);
            if (movedDist < STUCK_DISTANCE * dt)
            {
                _stuckTimer += dt;
                if (_stuckTimer > STUCK_THRESHOLD)
                {
                    // Unstick: pick new random direction and clear target
                    _targetRoom = null;
                    _stuckTimer = 0f;
                    movement.HandleDirectMove(RandomDirection());
                }
            }
            else
            {
                _stuckTimer = 0f;
            }
            _lastPosition = player.GlobalPosition;
        }

        private void TryAutoInteract(PlayerController player)
        {
            var spaceState = player.GetWorld3D().DirectSpaceState;
            var shape = new SphereShape3D { Radius = INTERACT_RANGE };
            var queryParams = new PhysicsShapeQueryParameters3D
            {
                Shape = shape,
                Transform = new Transform3D(Basis.Identity, player.GlobalPosition),
                CollisionMask = Constants.MASK_INTERACTABLE
            };

            var results = spaceState.IntersectShape(queryParams);
            foreach (var result in results)
            {
                var collider = (Node)result["collider"];
                if (collider is IInteractable interactable && interactable.CanInteract)
                {
                    interactable.Interact(player);
                    break;
                }
            }
        }

        private void CaptureScreenshot()
        {
            var viewport = GetViewport();
            if (viewport == null) return;

            var img = viewport.GetTexture().GetImage();
            if (img == null) return;

            _screenshotIndex = (_screenshotIndex % MAX_SCREENSHOTS);
            string path = _screenshotDir + $"/frame_{_screenshotIndex:D4}.png";
            img.SavePng(path);

            string latestPath = _screenshotDir + "/latest.png";
            img.SavePng(latestPath);

            _screenshotIndex++;
        }

        private void CleanScreenshots()
        {
            var dir = DirAccess.Open(_screenshotDir);
            if (dir == null) return;

            dir.ListDirBegin();
            string file = dir.GetNext();
            while (!string.IsNullOrEmpty(file))
            {
                if (file.EndsWith(".png"))
                    dir.Remove(file);
                file = dir.GetNext();
            }
            dir.ListDirEnd();
        }

        private Vector2 RandomDirection()
        {
            float angle = _rng.RandfRange(0, Mathf.Tau);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        private static SectorManager FindSectorManager(Node root)
        {
            if (root is SectorManager sm) return sm;
            foreach (var child in root.GetChildren())
            {
                var found = FindSectorManager(child);
                if (found != null) return found;
            }
            return null;
        }

        private static Vector2I WorldToGrid(Vector3 worldPos)
        {
            return new Vector2I(
                Mathf.RoundToInt(worldPos.X / DungeonGenerator.ROOM_SPACING),
                Mathf.RoundToInt(worldPos.Z / DungeonGenerator.ROOM_SPACING));
        }

        private static Vector3 GridToWorld(Vector2I gridPos)
        {
            return new Vector3(
                gridPos.X * DungeonGenerator.ROOM_SPACING, 0,
                gridPos.Y * DungeonGenerator.ROOM_SPACING);
        }

        private bool _lightAdded;

        private void AddAutoPlayLight()
        {
            if (_lightAdded) return;
            _lightAdded = true;

            var env = new Godot.Environment();
            env.AmbientLightSource = Godot.Environment.AmbientSource.Color;
            env.AmbientLightColor = new Color(0.9f, 0.85f, 0.75f);
            env.AmbientLightEnergy = 0.6f;
            env.BackgroundMode = Godot.Environment.BGMode.Color;
            env.BackgroundColor = new Color(0.1f, 0.1f, 0.15f);
            env.TonemapMode = Godot.Environment.ToneMapper.Filmic;

            var worldEnv = new WorldEnvironment();
            worldEnv.Environment = env;
            worldEnv.Name = "AutoPlayEnvironment";
            GetTree().Root.CallDeferred("add_child", worldEnv);

            GD.Print("[AutoPlayer] Added bright ambient light for visibility");
        }
    }
}
