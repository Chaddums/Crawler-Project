using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Autoload that plays the game automatically for remote observation.
    /// Smart combat AI with target priority, boss strategy, hazard avoidance,
    /// weapon-aware positioning, ability rotation, inventory management, and
    /// contextual screenshot capture.
    ///
    /// Enable via command line: --autoplay
    /// </summary>
    public partial class AutoPlayer : Node
    {
        private const float ATTACK_RANGE = 12f;
        private const float INTERACT_RANGE = 3f;
        private const float MENU_AUTO_DELAY = 1.5f;
        private const float ROOM_ARRIVAL_THRESHOLD = 6f;
        private const float ABILITY_CHECK_INTERVAL = 1.0f;
        private const float STUCK_THRESHOLD = 2f;
        private const float STUCK_DISTANCE = 1.5f;

        private float _menuDelayTimer;
        private RandomNumberGenerator _rng = new();
        private bool _enabled;

        // State tracking
        private bool _menuHandled;
        private bool _classSelected;

        // Navigation
        private DungeonGenerator _generator;
        private Vector2I? _targetRoom;
        private Vector3 _targetWorldPos;
        private float _abilityTimer;

        // Stuck detection + recovery
        private Vector3 _lastPosition;
        private float _stuckTimer;
        private float _unstuckTimer;
        private Vector2 _unstuckDirection;
        private int _stuckCount;
        private const float UNSTUCK_DURATION = 1.2f;
        private const float WARP_STUCK_COUNT = 3;
        private float _globalStuckTimer;
        private const float GLOBAL_STUCK_RESET = 15f;

        // Portal re-entry
        private float _portalRetryTimer;
        private bool _walkingAwayFromPortal;

        // Bot management timers
        private float _managementTimer;
        private const float MANAGEMENT_INTERVAL = 2f;
        private float _potionTimer;
        private const float POTION_CHECK_INTERVAL = 0.5f;
        private bool _initialBuffApplied;

        // Screenshot & reporting
        private AutoPlayScreenshotter _screenshotter;
        private AutoPlayRunStats _runStats = new();
        private float _runStartTime;
        private bool _reportGenerated;

        // Event re-subscription after GameEvents.ClearAll()
        private int _eventsVersion = -1;

        // Combat state
        private Node3D _currentTarget;
        private float _combatOptimalRange = 6f;
        private Vector3 _combatCheckpointPos;
        private float _combatCheckpointTimer;
        private const float COMBAT_CHECKPOINT_INTERVAL = 3f;
        private const float COMBAT_STUCK_MOVE_THRESHOLD = 2f;
        private bool _combatRepositioning; // Navigate toward enemy instead of circle-strafe
        private Node3D _aimTarget; // Set in _PhysicsProcess, warped in _Process (frame before)
        private float _targetEngageTimer; // How long we've been targeting the same enemy
        private Node3D _lastTargetRef; // Track if target changed

        // Target blacklist — prevents re-targeting enemies we can't hit
        private readonly Dictionary<ulong, float> _blacklistedTargets = new();
        private const float BLACKLIST_DURATION = 12f;
        private float _criticalHpTimer;

        // Room abandonment — skip rooms with unreachable enemies
        private Vector2I _currentRoomGrid = new(-999, -999);
        private float _roomStayTimer;
        private const float ROOM_ABANDON_TIME = 40f; // Leave after 40s if not cleared
        private readonly HashSet<Vector2I> _abandonedRooms = new();

        public static AutoPlayer Instance { get; private set; }

        public override void _Ready()
        {
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

            // Initialize screenshot system
            _screenshotter = new AutoPlayScreenshotter();
            AddChild(_screenshotter);

            _runStartTime = (float)Time.GetTicksMsec() / 1000f;

            GD.Print("[AutoPlayer] ACTIVE — smart combat + contextual screenshots");

            SubscribeEvents();
        }

        /// <summary>
        /// Subscribe to GameEvents. Called on _Ready and re-called whenever
        /// GameEvents.ClearAll() bumps the version (state transitions).
        /// </summary>
        private void SubscribeEvents()
        {
            _eventsVersion = GameEvents.Version;
            GameEvents.OnGameStateChanged += OnGameStateChanged;
            GameEvents.OnRoomCleared += OnRoomClearedAuto;
            GameEvents.OnRoomEntered += OnRoomEnteredAuto;
            GameEvents.OnEnemyKilled += OnEnemyKilledAuto;
            GameEvents.OnPlayerDeath += OnPlayerDeathAuto;
        }

        private void EnsureSubscribed()
        {
            if (_eventsVersion != GameEvents.Version)
            {
                SubscribeEvents();
                // ClearAll() was called — likely a new game. Reset everything.
                _initialBuffApplied = false;
                _menuHandled = false;
                _portalActivated = false;
                _bossHealApplied = false;
                _generator = null;
                _targetRoom = null;
                _currentTarget = null;
                _blacklistedTargets.Clear();
                _abandonedRooms.Clear();
                _runStats = new AutoPlayRunStats();
                _runStartTime = (float)Time.GetTicksMsec() / 1000f;
                _reportGenerated = false;
                GD.Print("[AutoPlayer] Re-subscribed after ClearAll — full state reset");
            }
        }

        public override void _ExitTree()
        {
            GameEvents.OnGameStateChanged -= OnGameStateChanged;
            GameEvents.OnRoomCleared -= OnRoomClearedAuto;
            GameEvents.OnRoomEntered -= OnRoomEnteredAuto;
            GameEvents.OnEnemyKilled -= OnEnemyKilledAuto;
            GameEvents.OnPlayerDeath -= OnPlayerDeathAuto;
            if (Instance == this) Instance = null;
        }

        private void OnGameStateChanged(GameState state)
        {
            _menuHandled = false;
            _menuDelayTimer = MENU_AUTO_DELAY;
            _generator = null;
            _targetRoom = null;
            _globalStuckTimer = 0f;
            _portalRetryTimer = 0f;
            _walkingAwayFromPortal = false;
            _portalActivated = false;
            _portalWaitTimer = 0f;
            _initialBuffApplied = false;
            _bossHealApplied = false;
            _currentTarget = null;
            _blacklistedTargets.Clear();
            _abandonedRooms.Clear();

            // Reset run stats on new game attempt
            if (state == GameState.CharacterCreation)
            {
                _runStats = new AutoPlayRunStats();
                _runStartTime = (float)Time.GetTicksMsec() / 1000f;
                _reportGenerated = false;
            }

            GD.Print($"[AutoPlayer] State changed to {state}");
        }

        private void OnRoomClearedAuto(Node room)
        {
            _globalStuckTimer = 0f;
            _stuckCount = 0;
            _blacklistedTargets.Clear();
            _roomStayTimer = 0f;
            _combatRepositioning = false;

            // Force re-evaluation of navigation target (important for boss portal)
            _targetRoom = null;

            // Restore mana on room clear — bot has no other mana recovery
            var player = PlayerManager.P1;
            if (player?.Stats != null)
                player.Stats.RestoreMana(player.Stats.MaxMana * 0.5f); // 50% mana back
        }

        private void OnRoomEnteredAuto(Node room)
        {
            _runStats.RoomsEntered++;
        }

        private void OnEnemyKilledAuto(Node enemy)
        {
            _runStats.EnemiesKilled++;

            // Small mana restore on kill — keeps abilities flowing
            var player = PlayerManager.P1;
            if (player?.Stats != null)
                player.Stats.RestoreMana(player.Stats.MaxMana * 0.10f); // 10% mana per kill
            _targetEngageTimer = 0f; // Reset since we're making progress
            _globalStuckTimer = 0f; // Actual kill = real progress
        }

        private void OnPlayerDeathAuto(Node player)
        {
            _runStats.Deaths++;
            GenerateReport("Death");
        }

        private void GenerateReport(string outcome)
        {
            if (_reportGenerated || _screenshotter == null) return;
            _reportGenerated = true;

            var player = PlayerManager.P1;
            _runStats.Duration = (float)Time.GetTicksMsec() / 1000f - _runStartTime;
            _runStats.BotFrame = player?.ClassController?.CurrentClass?.ToString() ?? "Unknown";
            _runStats.FinalLevel = player?.Stats?.Level ?? 0;
            _runStats.SectorsCleared = GameManager.Instance?.CurrentSector ?? 0;
            _runStats.Outcome = outcome;

            AutoPlayReport.Generate(_screenshotter, _runStats);
        }

        public override void _Process(double delta)
        {
            if (!_enabled) return;
            float dt = (float)delta;

            // Re-subscribe to events if GameEvents.ClearAll() was called
            EnsureSubscribed();

            // Warp mouse toward aim target HERE in _Process so the position
            // is updated before _PhysicsProcess reads it for basic attacks.
            // This fixes the one-frame-behind cursor issue.
            if (_aimTarget != null && GodotObject.IsInstanceValid(_aimTarget))
            {
                var player = PlayerManager.P1;
                if (player != null)
                    AimAtTarget(player, _aimTarget);
            }

            _menuDelayTimer -= dt;
            if (_menuDelayTimer <= 0f && !_menuHandled)
                HandleMenuNavigation();

            TryDismissDeathScreen();
            TryDismissBlockingUI();
        }

        private void TryDismissDeathScreen()
        {
            var root = GetTree().Root;
            foreach (var child in root.GetChildren())
            {
                if (child is CanvasLayer canvas && canvas.Layer == 100)
                {
                    var tryAgainBtn = FindButton(canvas, "Try Again");
                    if (tryAgainBtn != null)
                    {
                        GD.Print("[AutoPlayer] Clicking 'Try Again' on death screen");
                        tryAgainBtn.EmitSignal("pressed");
                        _reportGenerated = false; // Allow new report for next run
                        return;
                    }
                    var ascensionBtn = FindButton(canvas, "Enter Ascension");
                    ascensionBtn ??= FindButton(canvas, "Continue Ascending");
                    if (ascensionBtn != null)
                    {
                        GenerateReport("Victory");
                        GD.Print("[AutoPlayer] Clicking ascension button on victory screen");
                        ascensionBtn.EmitSignal("pressed");
                        return;
                    }
                }
            }
        }

        private float _uiDismissTimer;
        private void TryDismissBlockingUI()
        {
            _uiDismissTimer -= (float)GetProcessDeltaTime();
            if (_uiDismissTimer > 0f) return;
            _uiDismissTimer = 0.5f;

            var root = GetTree().Root;
            foreach (var child in root.GetChildren())
            {
                if (child is LootBoxCeremonyUI)
                {
                    GD.Print("[AutoPlayer] Auto-collecting loot box ceremony");
                    PushSyntheticClick();
                    return;
                }
                if (child is RelicCacheUI)
                {
                    GD.Print("[AutoPlayer] Auto-collecting relic cache");
                    PushSyntheticClick();
                    return;
                }
            }
        }

        private void PushSyntheticClick()
        {
            var click = new InputEventMouseButton();
            click.ButtonIndex = MouseButton.Left;
            click.Pressed = true;
            click.Position = GetViewport().GetVisibleRect().Size / 2f;
            GetViewport().PushInput(click);
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
                        // Bias toward tankier frames for better survival
                        var classes = new[] {
                            BotFrameType.TinCan, BotFrameType.TinCan,
                            BotFrameType.Scrapheap, BotFrameType.Scrapheap,
                            BotFrameType.Clunker, BotFrameType.Clunker,
                            BotFrameType.SparkPlug, BotFrameType.RustBucket,
                            BotFrameType.NoiseBox
                        };
                        var pick = classes[_rng.RandiRange(0, classes.Length - 1)];
                        GD.Print($"[AutoPlayer] Auto-selecting class: {pick}");
                        gm.StartGameWithClass(pick);
                    }
                    break;

                case GameState.SafeRoom:
                    _menuHandled = true;
                    HandleSafeRoom();
                    break;
            }
        }

        // =================================================================
        // SAFE ROOM — heal, craft, re-equip, then continue
        // =================================================================

        private async void HandleSafeRoom()
        {
            var player = PlayerManager.P1;
            if (player == null)
            {
                GameManager.Instance?.ContinueFromSafeRoom();
                return;
            }

            GD.Print("[AutoPlayer] Safe room — healing, crafting, managing inventory");

            // Full heal + mana restore
            if (player.Health != null && player.Health.IsAlive)
                player.Health.Heal(player.Health.MaxHealth);
            player.Stats?.RestoreMana(player.Stats.MaxMana);

            // Craft if we have components
            TryCraft(player);

            // Re-evaluate equipment after crafting
            AutoEquipItems(player);
            AutoSalvageJunk(player);

            // Screenshot safe room state
            _screenshotter?.Capture("safe_room", "Safe room management");

            // Wait 2s to let everything settle before continuing
            await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);

            GD.Print("[AutoPlayer] Continuing from safe room");
            GameManager.Instance?.ContinueFromSafeRoom();
        }

        private static void TryCraft(PlayerController player)
        {
            var inv = player.Inventory;
            if (inv == null) return;

            CraftingSystem.Initialize();
            int components = CraftingSystem.GetComponentCount(inv);

            if (components < 6)
            {
                GD.Print($"[AutoPlayer] Crafting: only {components} components, skipping");
                return;
            }

            // Prefer highest-tier recipe we can afford
            var recipes = CraftingSystem.Recipes;
            if (recipes == null) return;

            foreach (var recipe in recipes.OrderByDescending(r => r.ComponentCost))
            {
                if (CraftingSystem.GetComponentCount(inv) >= recipe.ComponentCost)
                {
                    var crafted = CraftingSystem.CraftItem(recipe, inv);
                    if (crafted != null)
                        GD.Print($"[AutoPlayer] Crafted: {crafted.GetDisplayName()} ({recipe.DisplayName})");
                }
            }
        }

        // =================================================================
        // GAMEPLAY — main loop
        // =================================================================

        private float _debugTimer;

        private void HandleGameplay(float dt)
        {
            var player = PlayerManager.P1;
            if (player == null)
            {
                _debugTimer -= dt;
                if (_debugTimer <= 0f)
                {
                    GD.Print("[AutoPlayer] No player found via PlayerManager");
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

            // Track room stay time (ticks regardless of combat/navigation mode)
            if (_generator != null)
            {
                var grid = WorldToGrid(player.GlobalPosition);
                if (grid != _currentRoomGrid)
                {
                    _currentRoomGrid = grid;
                    _roomStayTimer = 0f;
                }
                _roomStayTimer += dt;

                // Force abandon and warp if stuck in uncleared room too long
                // NEVER abandon boss rooms — boss must be killed to progress
                if (_roomStayTimer > ROOM_ABANDON_TIME
                    && _generator.RoomControllers.TryGetValue(grid, out var stuckRc)
                    && !stuckRc.IsCleared && stuckRc.IsEntered
                    && stuckRc.RoomType == RoomType.Combat
                    && !_abandonedRooms.Contains(grid))
                {
                    _abandonedRooms.Add(grid);
                    _blacklistedTargets.Clear();
                    _currentTarget = null;
                    _lastTargetRef = null;
                    _targetEngageTimer = 0f;
                    _roomStayTimer = 0f;

                    // Warp to next non-abandoned room
                    var nextRoom = BfsToUncleared(grid);
                    if (nextRoom.HasValue)
                    {
                        player.GlobalPosition = GridToWorld(nextRoom.Value) + Vector3.Up * 1f;
                        ForceEnterRoomAt(nextRoom.Value);
                        _targetRoom = null;
                        GD.Print($"[AutoPlayer] Abandoned room {grid}, warped to {nextRoom.Value} (abandoned: {_abandonedRooms.Count})");
                    }
                    else
                    {
                        // All rooms abandoned or cleared — try boss room
                        var bossPos = _generator.BossPosition;
                        player.GlobalPosition = GridToWorld(bossPos) + Vector3.Up * 1f;
                        _targetRoom = null;
                        GD.Print($"[AutoPlayer] Abandoned room {grid}, warped to boss at {bossPos}");
                    }
                }
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

            // Select combat target with priority scoring
            var target = SelectCombatTarget(player);
            float targetDist = target != null
                ? player.GlobalPosition.DistanceTo(target.GlobalPosition)
                : float.MaxValue;

            // Update weapon-aware optimal range
            UpdateOptimalRange(player);

            // Tick down blacklist timers
            TickBlacklist(dt);

            if (_unstuckTimer > 0f)
            {
                // In unstuck mode — escape only
            }
            else if (target != null && targetDist < ATTACK_RANGE)
            {
                // Don't reset global stuck timer here — it should only reset on
                // actual kills/clears, not just having a target. This prevents
                // infinite loops when targeting unreachable enemies.
                _targetEngageTimer += dt;

                // If stuck on same unreachable target for too long, blacklist and move on
                if (_targetEngageTimer > 6f && FindBossAI(target) == null)
                {
                    var targetId = target.GetInstanceId();
                    _blacklistedTargets[targetId] = BLACKLIST_DURATION;
                    GD.Print($"[AutoPlayer] Blacklisted target {target.Name} (ID={targetId}) — stuck for {_targetEngageTimer:F0}s");
                    _aimTarget = null;
                    _targetEngageTimer = 0f;
                    _currentTarget = null;
                    _lastTargetRef = null;
                    HandleNavigation(player, movement, dt);
                }
                else
                {
                    // Check if this is a boss fight
                    var bossAI = FindBossAI(target);
                    if (bossAI != null)
                    {
                        // Full heal + mana on first boss encounter
                        if (!_bossHealApplied)
                        {
                            _bossHealApplied = true;
                            player.Health.Heal(player.Health.MaxHealth);
                            player.Stats?.RestoreMana(player.Stats.MaxMana);
                            GD.Print("[AutoPlayer] Boss fight — full heal + mana restore");
                        }
                        HandleBossCombat(player, movement, combat, target, bossAI, targetDist, dt);
                    }
                    else
                        HandleCombat(player, movement, combat, target, targetDist, dt);

                    ConsiderDash(player, movement, target, targetDist);
                }
            }
            else
            {
                _aimTarget = null;

                // No enemies — grab nearby loot/interactables before navigating
                var nearestPickup = FindNearestInteractable(player);
                if (nearestPickup != null)
                {
                    float pickupDist = player.GlobalPosition.DistanceTo(nearestPickup.GlobalPosition);
                    if (pickupDist < 15f)
                    {
                        movement.NavigateTo(nearestPickup.GlobalPosition);
                    }
                    else
                    {
                        HandleNavigation(player, movement, dt);
                    }
                }
                else
                {
                    HandleNavigation(player, movement, dt);
                }
            }

            // Periodic debug
            _debugTimer -= dt;
            if (_debugTimer <= 0f)
            {
                _debugTimer = 5f;
                var grid = _generator != null ? WorldToGrid(player.GlobalPosition) : new Vector2I(-1, -1);
                var gm = GameManager.Instance;
                int sector = gm?.CurrentSector ?? 0;
                int area = gm?.CurrentArea ?? 0;
                string targetName = target != null ? target.Name : "none";
                string weaponInfo = "";
                if (player.Inventory?.Equipped?.TryGetValue(EquipmentSlot.MainHand, out var w) == true && w?.BaseData is EquipmentData wd)
                    weaponInfo = $" weapon={wd.WeaponType}";
                GD.Print($"[AutoPlayer] S{sector}-A{area} Lv{player.Stats.Level} HP={player.Health.CurrentHealth:F0}/{player.Health.MaxHealth:F0} pos={player.GlobalPosition:F1} grid={grid} target={_targetRoom} enemy={targetName} kills={_runStats.EnemiesKilled} range={_combatOptimalRange:F0}{weaponInfo}");
            }

            // Smart ability rotation
            _abilityTimer -= dt;
            if (_abilityTimer <= 0f && target != null && targetDist < ATTACK_RANGE)
            {
                _abilityTimer = ABILITY_CHECK_INTERVAL;
                TryUseAbilitySmartly(player, combat, target, targetDist);
            }

            ManageBot(player, dt);
            TryAutoInteract(player);
            DetectStuck(player, movement, dt);
            CheckRoomEntryAtPlayerPos(player);
            TryActivatePortalNearPlayer(player);

            // Timer rush: warp to boss when time is running out (checked every frame)
            if (_generator != null)
            {
                var timerCheck = FindLiftTimer();
                if (timerCheck != null && timerCheck.TimeRemaining < 180f && timerCheck.TimeRemaining > 0f)
                {
                    var bossPos = _generator.BossPosition;
                    var playerGrid = WorldToGrid(player.GlobalPosition);
                    if (playerGrid != bossPos)
                    {
                        player.GlobalPosition = GridToWorld(bossPos) + Vector3.Up * 1f;
                        ForceEnterRoomAt(bossPos);
                        _targetRoom = null;
                        _blacklistedTargets.Clear();
                        _currentTarget = null;
                        GD.Print($"[AutoPlayer] TIMER RUSH ({timerCheck.TimeRemaining:F0}s left) — warped to boss at {bossPos}");
                    }
                }
            }

            // Global stuck failsafe
            _globalStuckTimer += dt;
            if (_globalStuckTimer >= GLOBAL_STUCK_RESET && _generator != null)
            {
                _globalStuckTimer = 0f;
                var grid = WorldToGrid(player.GlobalPosition);

                // If current room is uncleared, warp to center.
                // Boss rooms ALWAYS get this treatment (never leave boss room).
                // Combat rooms only if we haven't been here too long.
                if (_generator.RoomControllers.TryGetValue(grid, out var curRc)
                    && !curRc.IsCleared && curRc.IsEntered
                    && ((curRc.RoomType == RoomType.Boss)
                        || (curRc.RoomType == RoomType.Combat && _roomStayTimer < ROOM_ABANDON_TIME)))
                {
                    var warpPos = GridToWorld(grid) + Vector3.Up * 1f;
                    player.GlobalPosition = warpPos;
                    _targetRoom = null;
                    _stuckCount = 0;
                    _blacklistedTargets.Clear(); // Reset blacklist so we can re-target
                    GD.Print($"[AutoPlayer] Stuck in uncleared room — warped to center of {grid} (boss={curRc.RoomType == RoomType.Boss})");
                }
                else
                {
                    // Room is cleared or not combat — find next uncleared room
                    var nextRoom = BfsToUncleared(grid);
                    if (nextRoom.HasValue)
                    {
                        var warpPos = GridToWorld(nextRoom.Value) + Vector3.Up * 1f;
                        player.GlobalPosition = warpPos;
                        ForceEnterRoomAt(nextRoom.Value);
                        _targetRoom = null;
                        _stuckCount = 0;
                        GD.Print($"[AutoPlayer] Global stuck reset — warped to uncleared room at {nextRoom.Value}");
                    }
                    else
                    {
                        var bossPos = _generator.BossPosition;
                        var warpPos = GridToWorld(bossPos) + Vector3.Up * 1f;
                        player.GlobalPosition = warpPos;
                        _targetRoom = null;
                        _stuckCount = 0;
                        _walkingAwayFromPortal = false;
                        _portalRetryTimer = 0f;
                        GD.Print($"[AutoPlayer] Global stuck reset — all cleared, warped to boss room at {bossPos}");
                    }
                }
            }

            // Critical HP limbo detection — stuck at near-zero HP where enemies can't reach
            if (player.Health.IsAlive && player.Health.HealthPercent < 0.01f)
            {
                _criticalHpTimer += dt;
                if (_criticalHpTimer > 8f)
                {
                    _criticalHpTimer = 0f;
                    // Self-heal to break the deadlock
                    player.Health.Heal(player.Health.MaxHealth * 0.5f);
                    GD.Print("[AutoPlayer] Critical HP limbo — emergency heal applied");
                }
            }
            else
            {
                _criticalHpTimer = 0f;
            }

            // Rescue from fall — must trigger before anomaly threshold (-3)
            if (player.GlobalPosition.Y < -2f)
            {
                var safePos = _generator != null
                    ? GridToWorld(WorldToGrid(_lastPosition)) + Vector3.Up * 1f
                    : new Vector3(320, 1, 320);
                player.GlobalPosition = safePos;
                _targetRoom = null;
                GD.Print($"[AutoPlayer] Rescued player from fall — warped to {safePos}");
            }

            // Rescue from wandering outside dungeon bounds
            var pos = player.GlobalPosition;
            if (pos.X < -32f || pos.X > 672f || pos.Z < -32f || pos.Z > 672f)
            {
                var safePos = _generator != null
                    ? GridToWorld(WorldToGrid(_lastPosition)) + Vector3.Up * 1f
                    : new Vector3(320, 1, 320);
                player.GlobalPosition = safePos;
                _targetRoom = null;
                _blacklistedTargets.Clear();
                GD.Print($"[AutoPlayer] Out of bounds ({pos:F0}) — warped to {safePos}");
            }
        }

        // =================================================================
        // SMART TARGET SELECTION
        // =================================================================

        private Node3D SelectCombatTarget(PlayerController player)
        {
            var enemies = player.GetTree().GetNodesInGroup(Constants.GROUP_ENEMY);
            var playerGrid = _generator != null ? WorldToGrid(player.GlobalPosition) : new Vector2I(-1, -1);

            // Two-pass: first try same-grid-cell enemies, then fall back to any in range
            var bestTarget = ScoreEnemies(player, enemies, playerGrid, requireSameGrid: true);
            if (bestTarget == null)
                bestTarget = ScoreEnemies(player, enemies, playerGrid, requireSameGrid: false);

            // Track target engagement duration
            if (bestTarget != _lastTargetRef)
            {
                _lastTargetRef = bestTarget;
                _targetEngageTimer = 0f;
            }

            _currentTarget = bestTarget;
            return bestTarget;
        }

        private Node3D ScoreEnemies(PlayerController player,
            Godot.Collections.Array<Node> enemies, Vector2I playerGrid, bool requireSameGrid)
        {
            Node3D bestTarget = null;
            float bestScore = float.MinValue;

            foreach (var node in enemies)
            {
                if (node is not Node3D enemy3d) continue;
                if (!GodotObject.IsInstanceValid(enemy3d)) continue;

                var health = enemy3d.GetNodeOrNull<HealthComponent>("HealthComponent");
                if (health != null && !health.IsAlive) continue;

                float dist = player.GlobalPosition.DistanceTo(enemy3d.GlobalPosition);
                if (dist > ATTACK_RANGE * 1.5f) continue;

                // Grid-cell filter (skipped on second pass)
                if (requireSameGrid && _generator != null)
                {
                    var enemyGrid = WorldToGrid(enemy3d.GlobalPosition);
                    if (enemyGrid != playerGrid) continue;
                }

                // Skip blacklisted targets
                if (_blacklistedTargets.ContainsKey(enemy3d.GetInstanceId())) continue;

                float score = 0f;

                // Penalize targets without line of sight
                if (!HasLineOfSight(player, enemy3d))
                    score -= 100f;

                // Score by behavior priority
                var ec = FindEnemyController(enemy3d);
                if (ec != null)
                {
                    if (ec.Data.IsBoss) score += 200;
                    score += ec.Data.Behavior switch
                    {
                        EnemyBehavior.Healer => 100,
                        EnemyBehavior.Ranged => 50,
                        EnemyBehavior.Flanker => 30,
                        EnemyBehavior.Charger => 10,
                        EnemyBehavior.Melee => 0,
                        EnemyBehavior.Swarm => -10,
                        EnemyBehavior.Tank => -20,
                        _ => 0
                    };
                }

                // Execute low-HP targets
                if (health != null && health.HealthPercent < 0.25f)
                    score += 40;

                // Distance penalty
                score -= dist * 2f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = enemy3d;
                }
            }

            return bestTarget;
        }

        // =================================================================
        // WEAPON-AWARE COMBAT RANGES
        // =================================================================

        private void UpdateOptimalRange(PlayerController player)
        {
            var inv = player.Inventory;
            if (inv == null) return;

            if (inv.Equipped.TryGetValue(EquipmentSlot.MainHand, out var weapon) && weapon?.BaseData is EquipmentData eq)
            {
                _combatOptimalRange = eq.WeaponType switch
                {
                    WeaponType.Shotgun => 5f,
                    WeaponType.Rifle => 10f,
                    WeaponType.Launcher => 11f,
                    WeaponType.BladeRing => 3f,
                    WeaponType.FlailChain => 3f,
                    WeaponType.ShockCoil => 3f,
                    WeaponType.FlameThrower => 3f,
                    WeaponType.Repeater => 7f,
                    WeaponType.Pistol => 6f,
                    _ => 6f
                };
            }
            else
            {
                _combatOptimalRange = 6f;
            }
        }

        // =================================================================
        // STANDARD COMBAT — kiting with hazard avoidance
        // =================================================================

        private void HandleCombat(PlayerController player, PlayerMovement movement,
            PlayerCombat combat, Node3D enemy, float dist, float dt)
        {
            var toEnemy = (enemy.GlobalPosition - player.GlobalPosition);
            toEnemy.Y = 0;
            var dir2d = new Vector2(toEnemy.X, toEnemy.Z).Normalized();

            // Combat movement checkpoint — check every 3s if bot actually moved
            _combatCheckpointTimer += dt;
            if (_combatCheckpointTimer >= COMBAT_CHECKPOINT_INTERVAL)
            {
                float movedSinceCheckpoint = player.GlobalPosition.DistanceTo(_combatCheckpointPos);
                if (movedSinceCheckpoint < COMBAT_STUCK_MOVE_THRESHOLD)
                {
                    // Haven't moved meaningfully in 3s — switch to NavigateTo enemy
                    _combatRepositioning = true;
                    GD.Print($"[AutoPlayer] Combat stuck (moved {movedSinceCheckpoint:F1}u in 3s) — repositioning toward enemy");
                }
                else
                {
                    _combatRepositioning = false;
                }
                _combatCheckpointPos = player.GlobalPosition;
                _combatCheckpointTimer = 0f;
            }

            // Always chase the enemy — be aggressive, not passive
            if (_combatRepositioning || dist > 3f)
            {
                // Chase: pathfind toward the enemy (NavigateTo handles obstacles)
                movement.NavigateTo(enemy.GlobalPosition);
            }
            else
            {
                // Close range: circle-strafe while attacking
                var strafeDir = new Vector2(-dir2d.Y, dir2d.X);
                var moveDir = (dir2d * 0.3f + strafeDir * 0.7f).Normalized();

                // Only flee when HP is critically low
                if (player.Health.HealthPercent < 0.25f)
                {
                    int nearbyMelee = CountNearbyEnemies(player, 4f);
                    if (nearbyMelee >= 3)
                    {
                        var centroid = GetEnemyCentroid(player, 6f);
                        var fleeDirVec = player.GlobalPosition - centroid;
                        fleeDirVec.Y = 0;
                        var fleeDir = new Vector2(fleeDirVec.X, fleeDirVec.Z).Normalized();
                        moveDir = (fleeDir * 0.5f + strafeDir * 0.5f).Normalized();
                    }
                }

                // Blend with hazard avoidance
                var hazardVec = GetHazardAvoidanceVector(player);
                if (hazardVec.LengthSquared() > 0.01f)
                    moveDir = (moveDir * 0.6f + hazardVec * 0.4f).Normalized();

                movement.HandleDirectMove(moveDir);
            }

            _aimTarget = enemy; // Warped in _Process (frame before physics)
            combat.HandleBasicAttack();
        }

        // =================================================================
        // BOSS COMBAT — phase-aware strategy
        // =================================================================

        private void HandleBossCombat(PlayerController player, PlayerMovement movement,
            PlayerCombat combat, Node3D enemy, BossAI bossAI, float dist, float dt)
        {
            var toEnemy = (enemy.GlobalPosition - player.GlobalPosition);
            toEnemy.Y = 0;
            var dir2d = new Vector2(toEnemy.X, toEnemy.Z).Normalized();

            // Boss fights: be AGGRESSIVE. Use NavigateTo for pathfinding (avoids arena wall stalling).
            // Only back off during SpecialAttack.
            switch (bossAI.CurrentState)
            {
                case BossAI.BossState.SpecialAttack:
                    // Dodge away from boss — use direct move to escape quickly
                    if (dist < 8f)
                    {
                        var fleeDir = -dir2d;
                        movement.HandleDirectMove(fleeDir);
                    }
                    else
                    {
                        // Safe distance — circle strafe
                        movement.HandleDirectMove(new Vector2(-dir2d.Y, dir2d.X) * 0.5f);
                    }
                    break;

                case BossAI.BossState.Stunned:
                    // Rush in for DPS window — pathfind to boss
                    movement.NavigateTo(enemy.GlobalPosition);
                    break;

                case BossAI.BossState.PhaseTransition:
                    // Brief back-off, then close in
                    if (dist < 6f)
                        movement.HandleDirectMove(-dir2d);
                    else
                        movement.NavigateTo(enemy.GlobalPosition);
                    break;

                default:
                    // Aggressive approach: always chase the boss, circle strafe when close
                    if (dist > 4f)
                    {
                        // Pathfind toward boss (handles arena walls/obstacles)
                        movement.NavigateTo(enemy.GlobalPosition);
                    }
                    else
                    {
                        // Close range — circle strafe while attacking
                        var strafeDir = new Vector2(-dir2d.Y, dir2d.X);
                        movement.HandleDirectMove((dir2d * 0.2f + strafeDir * 0.8f).Normalized());
                    }
                    break;
            }

            _aimTarget = enemy; // Warped in _Process (frame before physics)
            combat.HandleBasicAttack();
        }

        // =================================================================
        // HAZARD AVOIDANCE
        // =================================================================

        private Vector2 GetHazardAvoidanceVector(PlayerController player)
        {
            var spaceState = player.GetWorld3D().DirectSpaceState;
            var shape = new SphereShape3D { Radius = 5f };
            var queryParams = new PhysicsShapeQueryParameters3D
            {
                Shape = shape,
                Transform = new Transform3D(Basis.Identity, player.GlobalPosition),
                CollideWithAreas = true,
                CollideWithBodies = false
            };

            var results = spaceState.IntersectShape(queryParams);
            var repulsion = Vector2.Zero;

            foreach (var result in results)
            {
                var collider = (Node)result["collider"];
                // Walk up tree to find HazardDamager
                var hazard = FindHazardDamager(collider);
                if (hazard == null) continue;

                var hazardPos = (collider as Node3D)?.GlobalPosition ?? player.GlobalPosition;
                var away = player.GlobalPosition - hazardPos;
                away.Y = 0;
                float dist = away.Length();
                if (dist < 0.1f) continue;

                // Stronger repulsion when closer (inverse square falloff)
                float strength = 5f / (dist * dist + 0.5f);
                repulsion += new Vector2(away.X, away.Z).Normalized() * strength;
            }

            return repulsion.LengthSquared() > 0.01f ? repulsion.Normalized() : Vector2.Zero;
        }

        private static HazardDamager FindHazardDamager(Node node)
        {
            var current = node;
            while (current != null)
            {
                if (current is HazardDamager hd) return hd;
                // Check children too (HazardDamager might be a child of Area3D)
                foreach (var child in current.GetChildren())
                    if (child is HazardDamager hdChild) return hdChild;
                current = current.GetParent();
            }
            return null;
        }

        // =================================================================
        // SMART DASH
        // =================================================================

        private void ConsiderDash(PlayerController player, PlayerMovement movement,
            Node3D target, float targetDist)
        {
            if (movement.DashCharges <= 0 || movement.IsDashing) return;

            // Boss doing SpecialAttack — defensive dash away
            var bossAI = FindBossAI(target);
            if (bossAI != null && bossAI.CurrentState == BossAI.BossState.SpecialAttack && targetDist < 5f)
            {
                GD.Print("[AutoPlayer] Defensive dash — boss special attack");
                movement.HandleDash();
                return;
            }

            // 3+ melee enemies within 3u — escape dash
            int nearbyCount = CountNearbyEnemies(player, 3f);
            if (nearbyCount >= 3)
            {
                GD.Print($"[AutoPlayer] Escape dash — {nearbyCount} enemies within 3u");
                movement.HandleDash();
                return;
            }

            // Single enemy very close — reactive dash
            if (targetDist < 2f)
            {
                movement.HandleDash();
                return;
            }
        }

        // =================================================================
        // SMART ABILITY ROTATION
        // =================================================================

        private void TryUseAbilitySmartly(PlayerController player, PlayerCombat combat,
            Node3D target, float dist)
        {
            int nearbyEnemies = CountNearbyEnemies(player, 8f);
            float manaPercent = player.Stats.MaxMana > 0
                ? player.Stats.CurrentMana / player.Stats.MaxMana
                : 1f;
            bool isBossRoom = FindBossAI(target) != null;

            int bestSlot = -1;
            float bestPriority = 0f;

            for (int i = 0; i < Constants.MAX_ABILITY_SLOTS; i++)
            {
                var slot = combat.GetSlot(i);
                if (slot == null || slot.IsEmpty || !slot.IsReady) continue;
                if (dist > slot.Data.Range * 1.3f) continue;

                float priority = 10f; // base priority

                // Damage is king — heavily weight base damage
                priority += slot.Data.BaseDamage * 0.5f;

                // AoE value: scales with nearby enemy count, but only if it does real damage
                if (slot.Data.AoERadius > 0 && slot.Data.BaseDamage >= 15f)
                    priority += nearbyEnemies * 12f;

                // Projectile bonus at range
                if (slot.Data.Type == AbilityType.Projectile && dist > 5f)
                    priority += 15f;

                // Melee bonus when close
                if (slot.Data.Type == AbilityType.Melee && dist < 3f)
                    priority += 20f;

                // Mana conservation: penalize expensive abilities when low
                if (manaPercent < 0.3f && slot.Data.ManaCost > 0)
                    priority -= slot.Data.ManaCost * 2f;

                // Boss rooms: prioritize big abilities
                if (isBossRoom)
                    priority += 20f;

                if (priority > bestPriority)
                {
                    bestPriority = priority;
                    bestSlot = i;
                }
            }

            if (bestSlot >= 0)
            {
                var slot = combat.GetSlot(bestSlot);
                combat.HandleAbilityInput(bestSlot);
                GD.Print($"[AutoPlayer] Ability [{bestSlot}] {slot.Data.AbilityName} (priority={bestPriority:F0}, nearby={nearbyEnemies})");
            }
        }

        // =================================================================
        // NAVIGATION (unchanged core, extracted for clarity)
        // =================================================================

        private void HandleNavigation(PlayerController player, PlayerMovement movement, float dt)
        {
            if (_unstuckTimer > 0f) return;

            if (_generator == null)
            {
                movement.HandleDirectMove(RandomDirection());
                return;
            }

            var playerGrid = WorldToGrid(player.GlobalPosition);

            // If current room is uncleared combat and not abandoned, stay and fight
            if (_generator.RoomControllers.TryGetValue(playerGrid, out var currentRc)
                && !currentRc.IsCleared && currentRc.IsEntered
                && !_abandonedRooms.Contains(playerGrid)
                && (currentRc.RoomType == RoomType.Combat || currentRc.RoomType == RoomType.Boss))
            {
                var roomCenter = GridToWorld(playerGrid);
                movement.NavigateTo(roomCenter);
                return;
            }

            if (!_targetRoom.HasValue || playerGrid == _targetRoom.Value ||
                player.GlobalPosition.FlatDistance(_targetWorldPos) < ROOM_ARRIVAL_THRESHOLD)
            {
                _targetRoom = PickNextRoom(playerGrid);
                if (_targetRoom.HasValue)
                    _targetWorldPos = GridToWorld(_targetRoom.Value);
            }

            if (_targetRoom.HasValue)
            {
                movement.NavigateTo(_targetWorldPos);

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
                var toTarget = new Vector2((float)GD.RandRange(-1, 1), (float)GD.RandRange(-1, 1)).Normalized();
                movement.HandleDirectMove(toTarget);
            }
        }

        private Vector2I? PickNextRoom(Vector2I currentGrid)
        {
            if (_generator == null) return null;

            // After boss is killed, prioritize navigating to the portal
            var bossGrid = _generator.BossPosition;
            if (_generator.RoomControllers.TryGetValue(bossGrid, out var bossCheck)
                && bossCheck.IsCleared)
            {
                if (currentGrid == bossGrid)
                {
                    // Already in boss room — stay at center for portal activation
                    GD.Print("[AutoPlayer] In cleared boss room — staying for portal");
                    return bossGrid; // Navigate to center where portal is
                }
                else
                {
                    GD.Print("[AutoPlayer] Boss room cleared — navigating to portal");
                    return BfsNextStep(currentGrid, bossGrid);
                }
            }

            // Time pressure: rush to boss room when timer is low
            var liftTimer = FindLiftTimer();
            if (liftTimer != null && liftTimer.TimeRemaining < 180f && liftTimer.TimeRemaining > 0f)
            {
                // Less than 3 minutes left — warp directly to boss room
                if (currentGrid != bossGrid)
                {
                    var player = PlayerManager.P1;
                    if (player != null)
                    {
                        player.GlobalPosition = GridToWorld(bossGrid) + Vector3.Up * 1f;
                        ForceEnterRoomAt(bossGrid);
                        GD.Print($"[AutoPlayer] Timer low ({liftTimer.TimeRemaining:F0}s) — WARPED to boss room at {bossGrid}");
                    }
                    return bossGrid;
                }
            }

            var adjacent = GetAdjacentRooms(currentGrid);
            var unclearedCombat = new List<Vector2I>();
            var unclearedOther = new List<Vector2I>();
            var clearedRooms = new List<Vector2I>();

            foreach (var pos in adjacent)
            {
                if (!_generator.RoomControllers.TryGetValue(pos, out var rc)) continue;

                // Skip abandoned rooms (enemies unreachable)
                if (_abandonedRooms.Contains(pos)) { clearedRooms.Add(pos); continue; }

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

            var target = BfsToUncleared(currentGrid);
            if (target.HasValue) return target;

            if (bossGrid != currentGrid)
                return BfsNextStep(currentGrid, bossGrid);

            if (_generator.RoomControllers.TryGetValue(currentGrid, out var bossRc) && bossRc.IsCleared)
            {
                if (!_walkingAwayFromPortal)
                {
                    _portalRetryTimer += 0.02f;
                    if (_portalRetryTimer > 3f)
                    {
                        _walkingAwayFromPortal = true;
                        _portalRetryTimer = 0f;
                        GD.Print("[AutoPlayer] Walking away from portal to re-trigger entry");
                        var offset = new Vector3(8f, 0, 0);
                        _targetWorldPos = GridToWorld(currentGrid) + offset;
                        return currentGrid;
                    }
                }
                else
                {
                    _portalRetryTimer += 0.02f;
                    if (_portalRetryTimer > 1.5f)
                    {
                        _walkingAwayFromPortal = false;
                        _portalRetryTimer = 0f;
                        GD.Print("[AutoPlayer] Walking back onto portal");
                    }
                }

                _targetWorldPos = GridToWorld(currentGrid);
                return currentGrid;
            }

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

                if (_generator.RoomControllers.TryGetValue(pos, out var rc) && !rc.IsCleared
                    && !_abandonedRooms.Contains(pos))
                    return firstStep;

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

        private void ForceEnterRoomAt(Vector2I gridPos)
        {
            if (_generator == null) return;
            if (_generator.RoomControllers.TryGetValue(gridPos, out var rc))
                rc.ForceEnter();
        }

        private void CheckRoomEntryAtPlayerPos(PlayerController player)
        {
            if (_generator == null) return;
            var grid = WorldToGrid(player.GlobalPosition);
            if (_generator.RoomControllers.TryGetValue(grid, out var rc) && !rc.IsEntered)
            {
                var roomCenter = GridToWorld(grid);
                if (player.GlobalPosition.DistanceTo(roomCenter) < ROOM_ARRIVAL_THRESHOLD * 1.5f)
                {
                    rc.ForceEnter();
                    GD.Print($"[AutoPlayer] Force-entered room at {grid}");
                }
            }
        }

        private float _portalCheckTimer;
        private bool _portalActivated;
        private float _portalWaitTimer; // Time spent waiting for portal in boss room
        private bool _bossHealApplied; // One-time full heal when entering boss fight
        private void TryActivatePortalNearPlayer(PlayerController player)
        {
            if (_generator == null || _portalActivated) return;
            _portalCheckTimer -= 0.016f;
            if (_portalCheckTimer > 0f) return;
            _portalCheckTimer = 0.5f; // Check more frequently

            var bossGrid = _generator.BossPosition;
            if (!_generator.RoomControllers.TryGetValue(bossGrid, out var bossRc)) return;
            if (!bossRc.IsCleared) return;

            var playerGrid = WorldToGrid(player.GlobalPosition);
            if (playerGrid != bossGrid) return;

            _portalWaitTimer += 0.5f;

            var bossRoomNode = bossRc.GetParent();
            if (bossRoomNode == null) return;

            // Look for portal Area3D
            foreach (var child in bossRoomNode.GetChildren())
            {
                if (child is not Area3D area3d) continue;
                float dist = player.GlobalPosition.FlatDistance(area3d.GlobalPosition);
                if (dist < 8f) // Increased radius from 5 to 8
                {
                    _portalActivated = true;
                    GD.Print("[AutoPlayer] Manually triggering boss portal");
                    GameManager.Instance?.CallDeferred(nameof(GameManager.AdvanceArea));
                    return;
                }
            }

            // If stuck waiting for portal > 5s, force advance
            if (_portalWaitTimer > 5f)
            {
                _portalActivated = true;
                GD.Print("[AutoPlayer] Force-advancing area — portal not found after 5s in cleared boss room");
                GameManager.Instance?.CallDeferred(nameof(GameManager.AdvanceArea));
            }
        }

        private void DetectStuck(PlayerController player, PlayerMovement movement, float dt)
        {
            if (_unstuckTimer > 0f)
            {
                _unstuckTimer -= dt;
                movement.HandleDirectMove(_unstuckDirection);
                _lastPosition = player.GlobalPosition;
                return;
            }

            float movedDist = player.GlobalPosition.DistanceTo(_lastPosition);
            if (movedDist < STUCK_DISTANCE * dt)
            {
                _stuckTimer += dt;
                if (_stuckTimer > STUCK_THRESHOLD)
                {
                    _stuckCount++;
                    _stuckTimer = 0f;

                    if (_stuckCount >= WARP_STUCK_COUNT && _targetRoom.HasValue)
                    {
                        var warpPos = GridToWorld(_targetRoom.Value) + Vector3.Up * 1f;
                        player.GlobalPosition = warpPos;
                        ForceEnterRoomAt(_targetRoom.Value);
                        _targetRoom = null;
                        _stuckCount = 0;
                        _lastPosition = player.GlobalPosition;
                        GD.Print($"[AutoPlayer] Warped to escape stuck (pos={warpPos})");
                        return;
                    }

                    _targetRoom = null;
                    if (_stuckCount % 2 == 0)
                    {
                        var toTarget = (_targetWorldPos - player.GlobalPosition);
                        toTarget.Y = 0;
                        var perpDir = new Vector2(toTarget.X, toTarget.Z).Normalized();
                        _unstuckDirection = _stuckCount % 4 < 2
                            ? new Vector2(-perpDir.Y, perpDir.X)
                            : new Vector2(perpDir.Y, -perpDir.X);
                    }
                    else
                    {
                        _unstuckDirection = RandomDirection();
                    }

                    _unstuckTimer = UNSTUCK_DURATION;
                    GD.Print($"[AutoPlayer] Stuck! Escape dir={_unstuckDirection} count={_stuckCount}");
                }
            }
            else
            {
                _stuckTimer = 0f;
                if (movedDist > 5f * dt)
                    _stuckCount = 0;
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

        // =================================================================
        // BOT MANAGEMENT — auto-level, skill allocation, potions, equip
        // =================================================================

        private void ManageBot(PlayerController player, float dt)
        {
            if (!_initialBuffApplied)
            {
                _initialBuffApplied = true;
                ApplyAutoBuff(player);
            }

            // Use potions when low
            _potionTimer -= dt;
            if (_potionTimer <= 0f)
            {
                _potionTimer = POTION_CHECK_INTERVAL;
                if (player.Health.IsAlive && player.Health.HealthPercent < 0.75f)
                    player.Inventory.UseHealthQuick();
                if (player.Stats.CurrentMana < player.Stats.MaxMana * 0.3f)
                    player.Inventory.UseManaQuick();
            }

            // Periodic management
            _managementTimer -= dt;
            if (_managementTimer <= 0f)
            {
                _managementTimer = MANAGEMENT_INTERVAL;
                AutoAllocateSkillPoints(player);
                AutoEquipItems(player);
                AutoSalvageJunk(player);
            }
        }

        private void ApplyAutoBuff(PlayerController player)
        {
            var stats = player.Stats.Stats;
            var source = "autoplay_buff";

            // Scale buffs by area (later areas need more power)
            var gm = GameManager.Instance;
            int area = gm?.CurrentArea ?? 1;
            int sector = gm?.CurrentSector ?? 1;
            float areaScale = 1f + (sector - 1) * 0.5f + (area - 1) * 0.2f; // e.g., S1A1=1.0, S1A2=1.2, S2A1=1.5

            // Strong buffs so the bot survives long enough to test content
            stats.AddModifier(new StatModifier(StatType.MaxHealth, ModifierType.Percent, 4.0f * areaScale, source));  // +400%+ HP
            stats.AddModifier(new StatModifier(StatType.MaxHealth, ModifierType.Flat, 300f * areaScale, source));
            stats.AddModifier(new StatModifier(StatType.Armor, ModifierType.Flat, 200f * areaScale, source));
            stats.AddModifier(new StatModifier(StatType.Strength, ModifierType.Flat, 60f * areaScale, source));
            stats.AddModifier(new StatModifier(StatType.Strength, ModifierType.Percent, 1.5f * areaScale, source));
            stats.AddModifier(new StatModifier(StatType.Intelligence, ModifierType.Flat, 60f * areaScale, source));
            stats.AddModifier(new StatModifier(StatType.Intelligence, ModifierType.Percent, 1.5f * areaScale, source));
            stats.AddModifier(new StatModifier(StatType.Dexterity, ModifierType.Flat, 60f * areaScale, source));
            stats.AddModifier(new StatModifier(StatType.Dexterity, ModifierType.Percent, 1.5f * areaScale, source));
            stats.AddModifier(new StatModifier(StatType.CritChance, ModifierType.Flat, 0.40f, source));
            stats.AddModifier(new StatModifier(StatType.CritDamage, ModifierType.Flat, 2.0f, source));
            stats.AddModifier(new StatModifier(StatType.MoveSpeed, ModifierType.Percent, 0.35f, source));
            stats.AddModifier(new StatModifier(StatType.AttackSpeed, ModifierType.Percent, 0.80f, source));
            stats.AddModifier(new StatModifier(StatType.CooldownReduction, ModifierType.Flat, 0.30f, source)); // faster abilities
            stats.AddModifier(new StatModifier(StatType.Constitution, ModifierType.Flat, 40f * areaScale, source)); // more HP via CON
            stats.AddModifier(new StatModifier(StatType.MaxMana, ModifierType.Percent, 2.0f * areaScale, source)); // +200%+ mana pool
            stats.AddModifier(new StatModifier(StatType.MaxMana, ModifierType.Flat, 100f * areaScale, source));

            player.Health.SetMaxHealth(player.Stats.GetStat(StatType.MaxHealth), true);

            // Restore mana to full after buffing the pool
            player.Stats.RestoreMana(player.Stats.MaxMana);

            GD.Print($"[AutoPlayer] Applied stat buffs (area scale {areaScale:F1}x) — HP now {player.Health.MaxHealth:F0}, Mana {player.Stats.CurrentMana:F0}/{player.Stats.MaxMana:F0}");
        }

        private static void AutoAllocateSkillPoints(PlayerController player)
        {
            var classCtrl = player.ClassController;
            if (classCtrl?.PassiveTree == null) return;

            int allocated = 0;
            while (player.Stats.AvailableSkillPoints > 0)
            {
                var frontier = classCtrl.PassiveTree.GetAllocatableNodes(player.Stats.AvailableSkillPoints);
                if (frontier.Count == 0) break;

                var pick = frontier[GD.RandRange(0, frontier.Count - 1)];
                if (!classCtrl.AllocatePassiveNode(pick)) break;
                allocated++;
            }

            if (allocated > 0)
                GD.Print($"[AutoPlayer] Auto-allocated {allocated} passive tree nodes");
        }

        // =================================================================
        // SMART INVENTORY — score-based equipping + salvage
        // =================================================================

        private void AutoEquipItems(PlayerController player)
        {
            var inventory = player.Inventory;
            if (inventory == null) return;

            // Collect items by slot, find best candidates
            var bestBySlot = new Dictionary<EquipmentSlot, (ItemInstance item, float score)>();

            // Score currently equipped items
            foreach (var kvp in inventory.Equipped)
            {
                if (kvp.Value != null)
                    bestBySlot[kvp.Key] = (kvp.Value, ScoreItem(kvp.Value));
            }

            // Check inventory for upgrades
            foreach (var item in inventory.Items)
            {
                if (item?.BaseData is not EquipmentData eq) continue;

                float score = ScoreItem(item);
                var slot = eq.Slot;

                // Handle ring slots — try Ring1 then Ring2
                if (slot == EquipmentSlot.Ring1 || slot == EquipmentSlot.Ring2)
                {
                    foreach (var ringSlot in new[] { EquipmentSlot.Ring1, EquipmentSlot.Ring2 })
                    {
                        if (!bestBySlot.TryGetValue(ringSlot, out var current) || score > current.score * 1.1f)
                        {
                            bestBySlot[ringSlot] = (item, score);
                            break;
                        }
                    }
                    continue;
                }

                if (!bestBySlot.TryGetValue(slot, out var existing))
                {
                    // Empty slot — equip immediately
                    bestBySlot[slot] = (item, score);
                }
                else if (score > existing.score * 1.1f)
                {
                    // 10% better — upgrade
                    bestBySlot[slot] = (item, score);
                }
            }

            // Actually equip the best items
            foreach (var kvp in bestBySlot)
            {
                var currentlyEquipped = inventory.Equipped.TryGetValue(kvp.Key, out var cur) ? cur : null;
                if (kvp.Value.item != currentlyEquipped && inventory.Items.Contains(kvp.Value.item))
                {
                    inventory.Equip(kvp.Value.item, kvp.Key);
                    GD.Print($"[AutoPlayer] Equipped {kvp.Value.item.GetDisplayName()} → {kvp.Key} (score={kvp.Value.score:F0})");
                }
            }
        }

        private static float ScoreItem(ItemInstance item)
        {
            if (item == null) return 0f;

            // Rarity base score
            float score = item.Rarity switch
            {
                ItemRarity.Common => 10,
                ItemRarity.Uncommon => 25,
                ItemRarity.Rare => 50,
                ItemRarity.Epic => 100,
                ItemRarity.Legendary => 200,
                ItemRarity.Absurd => 500,
                _ => 0
            };

            // Sum weighted stat modifiers
            foreach (var mod in item.GetAllModifiers())
            {
                float weight = mod.StatType switch
                {
                    StatType.CritChance => 3f,
                    StatType.CritDamage => 2.5f,
                    StatType.Strength => 2f,
                    StatType.AttackSpeed => 2f,
                    StatType.MaxHealth => 1.5f,
                    StatType.Armor => 1.2f,
                    _ => 1f
                };
                score += Mathf.Abs(mod.Value) * weight;
            }

            // Affix count bonus
            if (item.Affixes != null)
                score += item.Affixes.Count * 15f;

            return score;
        }

        private void AutoSalvageJunk(PlayerController player)
        {
            var inventory = player.Inventory;
            if (inventory == null || inventory.ItemCount <= 15) return;

            CraftingSystem.Initialize();

            // Build reference scores for equipped items by slot
            var equippedScores = new Dictionary<EquipmentSlot, float>();
            foreach (var kvp in inventory.Equipped)
            {
                if (kvp.Value != null)
                    equippedScores[kvp.Key] = ScoreItem(kvp.Value);
            }

            // Find salvage candidates
            var toSalvage = new List<ItemInstance>();
            foreach (var item in inventory.Items)
            {
                if (item?.BaseData is not EquipmentData eq) continue;
                if (item.Rarity > ItemRarity.Uncommon) continue; // Only salvage Common/Uncommon

                if (equippedScores.TryGetValue(eq.Slot, out float equippedScore))
                {
                    float itemScore = ScoreItem(item);
                    if (itemScore < equippedScore * 0.8f)
                        toSalvage.Add(item);
                }
            }

            foreach (var item in toSalvage)
            {
                CraftingSystem.SalvageItem(item, inventory);
                GD.Print($"[AutoPlayer] Salvaged: {item.GetDisplayName()}");
            }
        }

        // =================================================================
        // HELPER METHODS
        // =================================================================

        private static EnemyController FindEnemyController(Node3D node)
        {
            if (node is EnemyController ec) return ec;
            var current = node.GetParent();
            while (current != null)
            {
                if (current is EnemyController parentEc) return parentEc;
                current = current.GetParent();
            }
            // Check children (enemy group node might be on a child)
            foreach (var child in node.GetChildren())
                if (child is EnemyController childEc) return childEc;
            return null;
        }

        private static BossAI FindBossAI(Node3D node)
        {
            var ec = FindEnemyController(node);
            return ec?.BossAI;
        }

        private static int CountNearbyEnemies(PlayerController player, float radius)
        {
            int count = 0;
            var enemies = player.GetTree().GetNodesInGroup(Constants.GROUP_ENEMY);
            foreach (var node in enemies)
            {
                if (node is not Node3D e3d) continue;
                if (!GodotObject.IsInstanceValid(e3d)) continue;
                var health = e3d.GetNodeOrNull<HealthComponent>("HealthComponent");
                if (health != null && !health.IsAlive) continue;
                if (player.GlobalPosition.DistanceTo(e3d.GlobalPosition) <= radius)
                    count++;
            }
            return count;
        }

        private static Vector3 GetEnemyCentroid(PlayerController player, float radius)
        {
            var sum = Vector3.Zero;
            int count = 0;
            var enemies = player.GetTree().GetNodesInGroup(Constants.GROUP_ENEMY);
            foreach (var node in enemies)
            {
                if (node is not Node3D e3d) continue;
                if (!GodotObject.IsInstanceValid(e3d)) continue;
                var health = e3d.GetNodeOrNull<HealthComponent>("HealthComponent");
                if (health != null && !health.IsAlive) continue;
                if (player.GlobalPosition.DistanceTo(e3d.GlobalPosition) <= radius)
                {
                    sum += e3d.GlobalPosition;
                    count++;
                }
            }
            return count > 0 ? sum / count : player.GlobalPosition;
        }

        private static Node3D FindNearestInteractable(PlayerController player)
        {
            Node3D nearest = null;
            float nearestDist = float.MaxValue;
            var interactables = player.GetTree().GetNodesInGroup(Constants.GROUP_INTERACTABLE);
            foreach (var node in interactables)
            {
                if (node is not Node3D n3d) continue;
                if (!GodotObject.IsInstanceValid(n3d)) continue;
                if (n3d is not IInteractable interactable || !interactable.CanInteract) continue;
                float dist = player.GlobalPosition.DistanceTo(n3d.GlobalPosition);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = n3d;
                }
            }
            return nearest;
        }

        private static bool HasLineOfSight(PlayerController player, Node3D target)
        {
            var spaceState = player.GetWorld3D().DirectSpaceState;
            if (spaceState == null) return true;

            var from = player.GlobalPosition + Vector3.Up * 0.8f;
            var to = target.GlobalPosition + Vector3.Up * 0.8f;
            var query = PhysicsRayQueryParameters3D.Create(from, to);
            query.CollisionMask = 1; // Default layer (static geometry only)
            query.Exclude = new Godot.Collections.Array<Rid> { player.GetRid() };

            var result = spaceState.IntersectRay(query);
            if (result == null || result.Count == 0) return true;

            // Hit something — check if it's the target or something between us
            var hitObj = result["collider"].As<GodotObject>();
            return hitObj == target;
        }

        private static void AimAtTarget(PlayerController player, Node3D target)
        {
            var camera = player.GetViewport().GetCamera3D();
            if (camera == null) return;

            var targetPos = target.GlobalPosition + Vector3.Up * 0.8f;
            if (!camera.IsPositionBehind(targetPos))
            {
                var screenPos = camera.UnprojectPosition(targetPos);
                Input.WarpMouse(screenPos);
            }
            else
            {
                // Target behind camera — warp cursor toward screen edge in target direction
                // This helps the hitscan system find nearby enemies even when fleeing
                var viewportSize = player.GetViewport().GetVisibleRect().Size;
                var camToTarget = (targetPos - camera.GlobalPosition).Normalized();
                var camRight = camera.GlobalBasis.X;
                float dot = camToTarget.Dot(camRight);
                // Place cursor on the side of screen nearest to the target
                float x = dot > 0 ? viewportSize.X * 0.9f : viewportSize.X * 0.1f;
                Input.WarpMouse(new Vector2(x, viewportSize.Y * 0.5f));
            }
        }

        private void TickBlacklist(float dt)
        {
            var expired = new List<ulong>();
            foreach (var kvp in _blacklistedTargets)
            {
                if (kvp.Value - dt <= 0f)
                    expired.Add(kvp.Key);
            }
            foreach (var id in expired)
                _blacklistedTargets.Remove(id);
            // Update remaining timers
            var keys = new List<ulong>(_blacklistedTargets.Keys);
            foreach (var key in keys)
                _blacklistedTargets[key] -= dt;
        }

        private Vector2 RandomDirection()
        {
            float angle = _rng.RandfRange(0, Mathf.Tau);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        private static LiftTimer FindLiftTimer()
        {
            return ServiceLocator.TryGet<LiftTimer>(out var timer) ? timer : null;
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
    }
}
