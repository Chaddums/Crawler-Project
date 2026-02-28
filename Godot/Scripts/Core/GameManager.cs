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

        public GameState CurrentState { get; private set; }

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
        }

        public void ChangeState(GameState newState)
        {
            var previousState = CurrentState;
            CurrentState = newState;

            GD.Print($"[GameManager] State changed: {previousState} -> {newState}");
            GameEvents.OnGameStateChanged?.Invoke(newState);
        }

        public void StartNewGame()
        {
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
