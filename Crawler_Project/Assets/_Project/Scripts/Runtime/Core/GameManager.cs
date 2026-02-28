using UnityEngine;

namespace DungeonCrawlerCarl
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] private GameState _initialState = GameState.MainMenu;

        public GameState CurrentState { get; private set; }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeCoreServices();
            ChangeState(_initialState);
        }

        private void InitializeCoreServices()
        {
            ServiceLocator.Register(new SaveSystem());
            ServiceLocator.Register(this);
        }

        public void ChangeState(GameState newState)
        {
            var previousState = CurrentState;
            CurrentState = newState;

            Debug.Log($"[GameManager] State changed: {previousState} -> {newState}");
            GameEvents.OnGameStateChanged?.Invoke(newState);
        }

        public void StartNewGame()
        {
            ChangeState(GameState.CharacterCreation);
            var sceneLoader = ServiceLocator.Get<SceneLoader>();
            sceneLoader?.LoadScene(Constants.SCENE_CHARACTER_CREATION);
        }

        public void ContinueGame()
        {
            var saveSystem = ServiceLocator.Get<SaveSystem>();
            if (saveSystem == null || !saveSystem.HasSave()) return;

            var saveData = saveSystem.Load();
            if (saveData == null) return;

            ChangeState(GameState.InFloor);
            var sceneLoader = ServiceLocator.Get<SceneLoader>();
            sceneLoader?.LoadFloor(saveData.CurrentFloor);
        }

        public void ReturnToMainMenu()
        {
            ChangeState(GameState.MainMenu);
            var sceneLoader = ServiceLocator.Get<SceneLoader>();
            sceneLoader?.LoadScene(Constants.SCENE_MAIN_MENU);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                ServiceLocator.Clear();
                Instance = null;
            }
        }
    }
}
