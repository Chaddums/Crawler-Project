using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Orchestrates a Vine Logic TD battle. Sets up the grid, pathfinder,
    /// wave manager, placer, camera, HUD, and lighting.
    /// Root script for VineBattle.tscn.
    /// </summary>
    public partial class VineBattleScene : Node3D
    {
        private VineGrid _grid;
        private VinePathfinder _pathfinder;
        private VineWaveManager _waveManager;
        private VinePlacer _placer;
        private VinePathPreview _pathPreview;
        private TDCamera _camera;
        private VineHUD _hud;
        private AXISCommentary _axisCommentary;

        public override void _Ready()
        {
            // ── Grid ──
            _grid = new VineGrid();
            AddChild(_grid);

            // ── Build map layout ──
            VineMapLayouts.BuildConduit(_grid);

            // ── Pathfinding ──
            _pathfinder = new VinePathfinder();
            AddChild(_pathfinder);
            _pathfinder.Initialize(_grid);

            // ── Wave manager ──
            _waveManager = new VineWaveManager();
            AddChild(_waveManager);

            // ── Placement ──
            _placer = new VinePlacer();
            AddChild(_placer);

            // ── Path preview ──
            _pathPreview = new VinePathPreview();
            AddChild(_pathPreview);

            // ── Camera ──
            _camera = new TDCamera();
            AddChild(_camera);
            _camera.SetMapBounds(
                _grid.Width * Constants.VINE_CELL_SIZE,
                _grid.Height * Constants.VINE_CELL_SIZE);

            // ── Lighting ──
            SetupLighting();
            SetupEnvironment();

            // ── HUD ──
            _hud = new VineHUD();
            AddChild(_hud);

            // ── AXIS Commentary ──
            _axisCommentary = new AXISCommentary();
            AddChild(_axisCommentary);

            // ── Initialize economy ──
            GameManager.Instance?.SetScrap(Constants.VINE_STARTING_GOLD);
            GameManager.Instance?.SetCoreLives(Constants.VINE_CORE_LIVES);

            // ── Economy hooks ──
            // Vine mode uses simplified economy — scrap drops go directly to gold
            GameEvents.OnScrapDropped += OnScrapDropped;
            GameEvents.OnScrapCollected += OnScrapCollected;

            // Start in build phase
            GameManager.Instance?.SetPhase(GamePhase.Build);

            GD.Print("[VineBattleScene] Ready — place nodes and press Space to start!");
        }

        private void SetupLighting()
        {
            // Main light — slightly green-tinted for vine/organic feel
            var dirLight = new DirectionalLight3D();
            dirLight.Position = new Vector3(10, 20, 10);
            dirLight.RotationDegrees = new Vector3(-45, -30, 0);
            dirLight.LightColor = new Color(0.85f, 0.9f, 0.8f);
            dirLight.LightEnergy = 0.7f;
            dirLight.ShadowEnabled = true;
            AddChild(dirLight);

            // Fill light — warm amber
            var fillLight = new DirectionalLight3D();
            fillLight.RotationDegrees = new Vector3(-30, 150, 0);
            fillLight.LightColor = new Color(0.4f, 0.3f, 0.2f);
            fillLight.LightEnergy = 0.25f;
            fillLight.ShadowEnabled = false;
            AddChild(fillLight);
        }

        private void SetupEnvironment()
        {
            var env = new WorldEnvironment();
            var envRes = new Godot.Environment();
            envRes.BackgroundMode = Godot.Environment.BGMode.Color;
            envRes.BackgroundColor = new Color(0.06f, 0.07f, 0.05f);
            envRes.AmbientLightColor = new Color(0.25f, 0.3f, 0.2f);
            envRes.AmbientLightEnergy = 0.35f;
            envRes.TonemapMode = Godot.Environment.ToneMapper.Filmic;
            envRes.GlowEnabled = true;
            envRes.GlowIntensity = 0.4f;
            env.Environment = envRes;
            AddChild(env);
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            // Click on nodes for interaction (sell, manual trigger)
            if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Right)
            {
                // Right-click: sell node or cancel placement
                if (ServiceLocator.TryGet<VinePlacer>(out var placer) && placer.IsPlacing)
                {
                    placer.CancelPlacing();
                    return;
                }

                var worldPos = RaycastGround(mb.Position);
                if (!worldPos.HasValue) return;

                var cell = _grid.WorldToGrid(worldPos.Value);
                var node = _grid.GetNode(cell);
                if (node != null)
                {
                    // Sell: refund based on editor tuning
                    int refund = Mathf.RoundToInt(node.Data.GoldCost * SignalTuningEditor.SellRefund);
                    _grid.RemoveNode(cell);
                    GameManager.Instance?.AddScrap(refund);
                }
            }
            else if (@event is InputEventMouseButton mb2 && mb2.Pressed && mb2.ButtonIndex == MouseButton.Middle)
            {
                // Middle-click: manual trigger (for SignalCannon nodes)
                var worldPos = RaycastGround(mb2.Position);
                if (!worldPos.HasValue) return;

                var cell = _grid.WorldToGrid(worldPos.Value);
                var node = _grid.GetNode(cell);
                node?.ManualTrigger();
            }
        }

        private void OnScrapDropped(Vector3 pos, int amount)
        {
            // In vine mode, scrap goes directly to gold (no decay/pickup mechanic)
            GameManager.Instance?.AddScrap(amount);
        }

        private void OnScrapCollected(int amount)
        {
            GameManager.Instance?.AddScrap(amount);
        }

        private Vector3? RaycastGround(Vector2 screenPos)
        {
            var camera = GetViewport().GetCamera3D();
            if (camera == null) return null;

            var from = camera.ProjectRayOrigin(screenPos);
            var dir = camera.ProjectRayNormal(screenPos);
            if (Mathf.Abs(dir.Y) < 0.001f) return null;
            float t = -from.Y / dir.Y;
            if (t < 0) return null;
            return from + dir * t;
        }

        public override void _ExitTree()
        {
            GameEvents.OnScrapDropped -= OnScrapDropped;
            GameEvents.OnScrapCollected -= OnScrapCollected;
        }
    }
}
