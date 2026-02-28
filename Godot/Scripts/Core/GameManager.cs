using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Autoload singleton — manages game state and scene transitions.
    /// Attached to Main.tscn which is registered as an autoload in project.godot.
    /// </summary>
    public partial class GameManager : Node
    {
        public static GameManager Instance { get; private set; }

        [Export] private GameState _initialState = GameState.MainMenu;

        public const int AREAS_PER_FLOOR = 3;

        public GameState CurrentState { get; private set; }
        public CrawlerClassName SelectedClass { get; set; } = CrawlerClassName.BoringOlFighter;
        public string ActiveCompanionId { get; set; } = "donut";
        public int CurrentFloor { get; set; } = 1;
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
            ServiceLocator.Register(this);

            // Initialize all registries — order matters for loot table references
            CrawlerClassRegistry.Initialize();
            AbilityRegistry.Initialize();
            CompanionRegistry.Initialize();
            AffixRegistry.Initialize();
            ItemRegistry.Initialize();
            ConsumableRegistry.Initialize();
            BaseItemPool.Initialize();
            LootBoxFactory.Initialize();
            EnemyRegistry.Initialize();
            FloorDataRegistry.Initialize();

            // Build the passive tree (lazy, but ensure it's ready)
            _ = PassiveTreeBuilder.Tree;

            GD.Print("[GameManager] All registries initialized");
        }

        public void StartGameWithClass(CrawlerClassName className)
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
            ChangeState(GameState.InFloor);
            GetTree().ChangeSceneToFile(Constants.SCENE_FLOOR);
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

            // Every N areas, advance to the next floor
            if (CurrentArea > AREAS_PER_FLOOR)
            {
                CurrentArea = 1;
                CurrentFloor++;
                GD.Print($"[GameManager] Descending to Floor {CurrentFloor}!");

                FloorTransitionUI.Show(GetTree().Root, CurrentFloor, Callable.From(() =>
                {
                    ChangeState(GameState.InFloor);
                    GetTree().ChangeSceneToFile(Constants.SCENE_FLOOR);
                }));
                return;
            }

            GD.Print($"[GameManager] Entering Area {CurrentArea} of Floor {CurrentFloor}");
            ChangeState(GameState.InFloor);
            GetTree().ChangeSceneToFile(Constants.SCENE_FLOOR);
        }

        public void AdvanceFloor()
        {
            CurrentArea = 1;
            CurrentFloor++;
            GD.Print($"[GameManager] Advancing to floor {CurrentFloor}");

            FloorTransitionUI.Show(GetTree().Root, CurrentFloor, Callable.From(() =>
            {
                ChangeState(GameState.Stairwell);
                GetTree().ChangeSceneToFile(Constants.SCENE_FLOOR);
            }));
        }

        public void ContinueGame()
        {
            IsLoadingGame = true;
            ChangeState(GameState.InFloor);
            GetTree().ChangeSceneToFile(Constants.SCENE_FLOOR);
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
