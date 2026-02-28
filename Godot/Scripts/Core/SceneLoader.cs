using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Helper for scene transitions. In Godot, scene loading is straightforward
    /// via GetTree().ChangeSceneToFile(), but this provides a central place
    /// for loading logic (fade transitions, loading screens, etc.).
    /// </summary>
    public partial class SceneLoader : Node
    {
        public static SceneLoader Instance { get; private set; }

        public override void _Ready()
        {
            Instance = this;
            ServiceLocator.Register(this);
        }

        public void LoadScene(string scenePath)
        {
            GD.Print($"[SceneLoader] Loading scene: {scenePath}");
            GetTree().ChangeSceneToFile(scenePath);
        }

        public void LoadSector(int sectorNumber)
        {
            GD.Print($"[SceneLoader] Loading sector {sectorNumber}");
            GetTree().ChangeSceneToFile(Constants.SCENE_SECTOR);
        }

        public override void _ExitTree()
        {
            if (Instance == this)
            {
                ServiceLocator.Unregister<SceneLoader>();
                Instance = null;
            }
        }
    }
}
