using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DungeonCrawlerCarl
{
    public class SceneLoader : MonoBehaviour
    {
        [SerializeField] private float _minimumLoadTime = 0.5f;

        private bool _isLoading;

        private void Awake()
        {
            ServiceLocator.Register(this);
        }

        public async void LoadScene(string sceneName, LoadSceneMode mode = LoadSceneMode.Single)
        {
            if (_isLoading) return;
            _isLoading = true;

            GameManager.Instance?.ChangeState(GameState.Loading);

            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, mode);
            op.allowSceneActivation = false;

            float startTime = Time.realtimeSinceStartup;

            while (op.progress < 0.9f)
                await Task.Yield();

            // Ensure minimum load time for smooth transitions
            float elapsed = Time.realtimeSinceStartup - startTime;
            if (elapsed < _minimumLoadTime)
            {
                float remaining = _minimumLoadTime - elapsed;
                await Task.Delay((int)(remaining * 1000));
            }

            op.allowSceneActivation = true;
            _isLoading = false;
        }

        public void LoadFloor(int floorNumber)
        {
            string sceneName = $"Floor{floorNumber:D2}";
            LoadScene(sceneName);
            GameEvents.OnFloorEntered?.Invoke(floorNumber);
        }

        public void LoadStairwell()
        {
            LoadScene(Constants.SCENE_STAIRWELL);
            GameManager.Instance?.ChangeState(GameState.Stairwell);
        }

        public void UnloadScene(string sceneName)
        {
            SceneManager.UnloadSceneAsync(sceneName);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<SceneLoader>();
        }
    }
}
