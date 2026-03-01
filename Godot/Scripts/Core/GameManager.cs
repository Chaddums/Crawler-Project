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
            ServiceLocator.Register(this);

            // Initialize all registries — order matters for loot table references
            BotFrameRegistry.Initialize();
            AbilityRegistry.Initialize();
            CompanionRegistry.Initialize();
            AffixRegistry.Initialize();
            ItemRegistry.Initialize();
            ConsumableRegistry.Initialize();
            BaseItemPool.Initialize();
            LootBoxFactory.Initialize();
            EnemyRegistry.Initialize();
            SectorDataRegistry.Initialize();

            // Build the passive tree (lazy, but ensure it's ready)
            _ = PassiveTreeBuilder.Tree;

            GD.Print("[GameManager] All registries initialized");
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
            ChangeState(GameState.InSector);
            GetTree().ChangeSceneToFile(Constants.SCENE_SECTOR);
        }

        public void AdvanceArea()
        {
            GD.Print($"[GameManager] Area cleared! Heading to Safe Room...");
            ChangeState(GameState.SafeRoom);
            GetTree().ChangeSceneToFile(Constants.SCENE_SAFE_ROOM);
        }

        public void ContinueFromSafeRoom()
        {
            CurrentArea++;

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
            CurrentArea = 1;
            CurrentSector++;
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
            ChangeState(GameState.MainMenu);
            GetTree().ChangeSceneToFile(Constants.SCENE_MAIN_MENU);
        }

        public void QuitGame()
        {
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
