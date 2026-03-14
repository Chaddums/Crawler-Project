using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Orchestrates a single battle: sets up the map, systems, camera, and UI.
    /// This is the root script for Battle.tscn.
    /// </summary>
    public partial class BattleScene : Node3D
    {
        private MapGrid _grid;
        private Pathfinder _pathfinder;
        private WaveManager _waveManager;
        private ScrapManager _scrapManager;
        private TowerPlacer _towerPlacer;
        private TDCamera _camera;
        private HUD _hud;
        private AXISCommentary _axisCommentary;
        private FabricationSystem _fabrication;
        private TowerInspector _towerInspector;
        private TerrainManipulator _terrainManipulator;
        private HeroBotController _heroBot;

        public override void _Ready()
        {
            // --- Create systems ---

            // Map grid — use selected map dimensions
            var mapId = GameManager.Instance?.SelectedMapId ?? "scrapyard";
            var mapInfo = MapLayouts.Available.Find(m => m.Id == mapId);

            _grid = new MapGrid();
            if (mapInfo.Width > 0)
            {
                _grid.Width = mapInfo.Width;
                _grid.Height = mapInfo.Height;
            }
            AddChild(_grid);

            // Pathfinding
            _pathfinder = new Pathfinder();
            AddChild(_pathfinder);

            // Build selected map layout
            MapLayouts.Build(mapId, _grid);

            // Initialize pathfinding after map is built
            _pathfinder.Initialize(_grid);

            // Economy
            _scrapManager = new ScrapManager();
            AddChild(_scrapManager);

            // Waves
            _waveManager = new WaveManager();
            AddChild(_waveManager);

            // Tower placement
            _towerPlacer = new TowerPlacer();
            AddChild(_towerPlacer);
            ServiceLocator.Register(_towerPlacer);

            // --- Camera ---
            _camera = new TDCamera();
            AddChild(_camera);
            _camera.SetMapBounds(
                _grid.Width * Constants.CELL_SIZE,
                _grid.Height * Constants.CELL_SIZE
            );

            // --- Lighting ---
            SetupLighting();

            // --- HUD ---
            _hud = new HUD();
            AddChild(_hud);

            // --- AXIS Commentary ---
            _axisCommentary = new AXISCommentary();
            AddChild(_axisCommentary);

            // --- Fabrication ---
            _fabrication = new FabricationSystem();
            AddChild(_fabrication);

            // --- Tower Inspector ---
            _towerInspector = new TowerInspector();
            AddChild(_towerInspector);

            // --- Terrain Manipulator ---
            _terrainManipulator = new TerrainManipulator();
            AddChild(_terrainManipulator);

            // --- Hero Bot ---
            _heroBot = new HeroBotController();
            AddChild(_heroBot);

            // --- Apply difficulty ---
            float difficulty = GameManager.Instance?.DifficultyMultiplier ?? 1f;
            _waveManager.SetDifficulty(difficulty);

            // --- Environment ---
            SetupEnvironment();

            // Start in build phase
            GameManager.Instance?.SetPhase(GamePhase.Build);

            GD.Print("[BattleScene] Ready — place towers and press Space to start!");
        }

        private void SetupLighting()
        {
            // Directional light (sun)
            var dirLight = new DirectionalLight3D();
            dirLight.Position = new Vector3(10, 20, 10);
            dirLight.RotationDegrees = new Vector3(-45, -30, 0);
            dirLight.LightColor = new Color(0.95f, 0.9f, 0.85f);
            dirLight.LightEnergy = 0.8f;
            dirLight.ShadowEnabled = true;
            AddChild(dirLight);

            // Ambient fill — slight orange for junkyard warmth
            var fillLight = new DirectionalLight3D();
            fillLight.RotationDegrees = new Vector3(-30, 150, 0);
            fillLight.LightColor = new Color(0.4f, 0.35f, 0.3f);
            fillLight.LightEnergy = 0.3f;
            fillLight.ShadowEnabled = false;
            AddChild(fillLight);
        }

        private void SetupEnvironment()
        {
            var env = new WorldEnvironment();
            var envRes = new Godot.Environment();
            envRes.BackgroundMode = Godot.Environment.BGMode.Color;
            envRes.BackgroundColor = new Color(0.08f, 0.07f, 0.06f);
            envRes.AmbientLightColor = new Color(0.3f, 0.25f, 0.2f);
            envRes.AmbientLightEnergy = 0.4f;
            envRes.TonemapMode = Godot.Environment.ToneMapper.Filmic;
            envRes.GlowEnabled = true;
            envRes.GlowIntensity = 0.3f;
            env.Environment = envRes;
            AddChild(env);
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            // Click to collect scrap
            if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            {
                if (ServiceLocator.TryGet<TowerPlacer>(out var placer) && placer.IsPlacing)
                    return; // Let TowerPlacer handle it

                var camera = GetViewport().GetCamera3D();
                if (camera == null) return;
                var from = camera.ProjectRayOrigin(mb.Position);
                var dir = camera.ProjectRayNormal(mb.Position);
                if (Mathf.Abs(dir.Y) < 0.001f) return;
                float t = -from.Y / dir.Y;
                if (t < 0) return;
                var worldPos = from + dir * t;

                _scrapManager.TryCollectNear(worldPos, Constants.SCRAP_COLLECT_RADIUS);
            }
        }

        public override void _ExitTree()
        {
            ServiceLocator.Unregister<TowerPlacer>();
        }
    }
}
