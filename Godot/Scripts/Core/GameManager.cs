using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Autoload singleton — manages game state and scene transitions.
    /// Attached to Main.tscn which is registered as an autoload in project.godot.
    /// </summary>
    public partial class GameManager : Node
    {
        public static GameManager Instance { get; private set; }

        [Export] private GameState _initialState = GameState.MainMenu;

        public const int AREAS_PER_SECTOR = 3;

        public GameState CurrentState { get; private set; }
        public BotFrameType SelectedClass { get; set; } = BotFrameType.TinCan;
        public string ActiveCompanionId { get; set; } = "bit";
        public int CurrentSector { get; set; } = 1;
        public int CurrentArea { get; set; } = 1;
        public bool IsLoadingGame { get; set; }

        public override void _Ready()
        {
            if (Instance != null)
            {
                QueueFree();
                return;
            }

            Instance = this;
            InitializeCoreServices();
            ChangeState(_initialState);
        }

        private void InitializeCoreServices()
        {
            StringLoader.Load();
            IconLoader.Load();
            AudioLoader.Load();
            ServiceLocator.Register(this);

            // Initialize all registries — order matters for loot table references
            PerkRegistry.Initialize();
            BotFrameRegistry.Initialize();
            AbilityRegistry.Initialize();
            CompanionRegistry.Initialize();
            AffixRegistry.Initialize();
            ItemRegistry.Initialize();
            ConsumableRegistry.Initialize();
            BaseItemPool.Initialize();
            LootBoxFactory.Initialize();
            RelicRegistry.Initialize();
            EnemyRegistry.Initialize();
            SectorDataRegistry.Initialize();

            // Build the passive tree (lazy, but ensure it's ready)
            _ = PassiveTreeBuilder.Tree;

            // Load meta-progression (triggers lazy init)
            _ = MetaSaveManager.Data;

            GD.Print($"[GameManager] All registries initialized (Threat Level: {MetaSaveManager.ThreatLevel})");

            WireMetaHooks();
        }

        public int RunKills { get; private set; }
        public System.Collections.Generic.HashSet<string> FoundRelicsThisRun { get; private set; } = new();

        private void OnPlayerDeathMeta(Node playerNode)
        {
            MetaSaveManager.RecordRunEnd(
                SelectedClass, CurrentSector, CurrentArea,
                _playerLevel, RunKills, _runTimer);
            RunKills = 0;
        }

        private int _playerLevel = 1;
        private float _runTimer;

        private void OnItemPickedUpMeta(Resource itemData)
        {
            if (itemData is ItemData item)
                MetaSaveManager.DiscoverGear(item.Id);
        }

        private void OnEnemyKilledMeta(Node enemy)
        {
            RunKills++;
            // Small scrap reward per kill (scales with sector)
            int scrap = CurrentSector + 1;
            MetaSaveManager.AddScrap(scrap);
            GameEvents.OnScrapEarned?.Invoke(scrap);
        }

        private void OnRoomClearedMeta(Node room)
        {
            // Bonus scrap for clearing a room
            int scrap = 5 * CurrentSector;
            MetaSaveManager.AddScrap(scrap);
            GameEvents.OnScrapEarned?.Invoke(scrap);
        }

        private void OnBossDefeatedMeta(Node bossNode)
        {
            // Check if this is the AXIS fight (sector 5, final area)
            if (CurrentSector >= 5 && CurrentArea >= AREAS_PER_SECTOR)
            {
                // Record the victory and bump ascension
                MetaSaveManager.RecordAxisVictory();

                // Big scrap bonus for beating AXIS (scales with ascension)
                int victoryScrap = 500 * MetaSaveManager.Data.AscensionRank;
                MetaSaveManager.AddScrap(victoryScrap);
                GameEvents.OnScrapEarned?.Invoke(victoryScrap);

                // Show victory screen after a short delay
                GetTree().CreateTimer(2.0).Timeout += ShowVictoryScreen;
            }
        }

        private void ShowVictoryScreen()
        {
            var victory = new VictoryScreenUI();
            victory.Name = "VictoryScreen";
            GetTree().Root.AddChild(victory);
            victory.Show(MetaSaveManager.Data.AscensionRank, MetaSaveManager.Data.TimesAxisDefeated,
                _runTimer, RunKills, _playerLevel, SelectedClass);
        }

        private void WireMetaHooks()
        {
            GameEvents.OnPlayerDeath += OnPlayerDeathMeta;
            GameEvents.OnItemPickedUp += OnItemPickedUpMeta;
            GameEvents.OnEnemyKilled += OnEnemyKilledMeta;
            GameEvents.OnRoomCleared += OnRoomClearedMeta;
            GameEvents.OnBossDefeated += OnBossDefeatedMeta;
        }

        public override void _Process(double delta)
        {
            if (CurrentState == GameState.InSector)
                _runTimer += (float)delta;

            // Track player level for meta stats
            var players = GetTree().GetNodesInGroup(Constants.GROUP_PLAYER);
            if (players.Count > 0 && players[0] is PlayerController pc)
                _playerLevel = pc.Stats.Level;
        }

        public void StartGameWithClass(BotFrameType className)
        {
            SelectedClass = className;
            StartNewGame();
        }

        public void ChangeState(GameState newState)
        {
            var previousState = CurrentState;
            CurrentState = newState;

            GD.Print($"[GameManager] State changed: {previousState} -> {newState}");
            GameEvents.OnGameStateChanged?.Invoke(newState);
        }

        public void GoToCharacterCreation()
        {
            ChangeState(GameState.CharacterCreation);
            GetTree().ChangeSceneToFile(Constants.SCENE_CHARACTER_CREATION);
        }

        public void StartNewGame()
        {
            // Clear leaked static event subscriptions from previous run
            GameEvents.ClearAll();
            WireMetaHooks();
            // Delete old run save so it doesn't bleed into the new run
            // (meta save is NEVER deleted)
            SaveManager.DeleteSave();
            IsLoadingGame = false;
            CurrentSector = 1;
            CurrentArea = 1;
            RunKills = 0;
            FoundRelicsThisRun.Clear();
            _runTimer = 0f;
            _playerLevel = 1;
            ChangeState(GameState.InSector);
            GetTree().ChangeSceneToFile(Constants.SCENE_SECTOR);
        }

        /// <summary>
        /// Save the current player's state before a scene transition so
        /// inventory, equipment, level, and progress carry over.
        /// </summary>
        private void SaveBeforeTransition()
        {
            var players = GetTree().GetNodesInGroup(Constants.GROUP_PLAYER);
            if (players.Count > 0 && players[0] is PlayerController player)
            {
                SaveManager.SaveGame(player, CurrentSector);
                GD.Print("[GameManager] Player state saved before transition");
            }

            // Persist meta-progression (codex, scrap, etc.) at every transition
            MetaSaveManager.Save();
        }

        public void AdvanceArea()
        {
            GD.Print($"[GameManager] Area cleared! Heading to Safe Room...");
            SaveBeforeTransition();
            ChangeState(GameState.SafeRoom);
            GetTree().ChangeSceneToFile(Constants.SCENE_SAFE_ROOM);
        }

        public void ContinueFromSafeRoom()
        {
            SaveBeforeTransition();
            CurrentArea++;

            // Clear leaked static event subscriptions from previous area
            GameEvents.ClearAll();
            WireMetaHooks();

            // Every N areas, advance to the next sector
            if (CurrentArea > AREAS_PER_SECTOR)
            {
                CurrentArea = 1;
                CurrentSector++;
                GD.Print($"[GameManager] Advancing to Sector {CurrentSector}!");

                SectorTransitionUI.Show(GetTree().Root, CurrentSector, Callable.From(() =>
                {
                    ChangeState(GameState.InSector);
                    GetTree().ChangeSceneToFile(Constants.SCENE_SECTOR);
                }));
                return;
            }

            GD.Print($"[GameManager] Entering Area {CurrentArea} of Sector {CurrentSector}");
            ChangeState(GameState.InSector);
            GetTree().ChangeSceneToFile(Constants.SCENE_SECTOR);
        }

        public void AdvanceSector()
        {
            SaveBeforeTransition();
            CurrentArea = 1;
            CurrentSector++;
            GameEvents.ClearAll();
            WireMetaHooks();
            GD.Print($"[GameManager] Advancing to sector {CurrentSector}");

            SectorTransitionUI.Show(GetTree().Root, CurrentSector, Callable.From(() =>
            {
                ChangeState(GameState.Lift);
                GetTree().ChangeSceneToFile(Constants.SCENE_SECTOR);
            }));
        }

        public void ContinueGame()
        {
            IsLoadingGame = true;
            ChangeState(GameState.InSector);
            GetTree().ChangeSceneToFile(Constants.SCENE_SECTOR);
        }

        public void ReturnToMainMenu()
        {
            // Persist meta before leaving the run
            MetaSaveManager.Save();
            // Clear leaked static event subscriptions from the run
            GameEvents.ClearAll();
            ChangeState(GameState.MainMenu);
            GetTree().ChangeSceneToFile(Constants.SCENE_MAIN_MENU);
        }

        public void QuitGame()
        {
            // Save meta before quitting
            MetaSaveManager.Save();
            GetTree().Quit();
        }

        public override void _ExitTree()
        {
            if (Instance == this)
            {
                ServiceLocator.Clear();
                Instance = null;
            }
        }
    }
}
