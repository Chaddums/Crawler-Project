using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Handles tower placement input — shows ghost, validates position, places on click.
    /// </summary>
    public partial class TowerPlacer : Node3D
    {
        private TowerType? _selectedType;
        private MeshInstance3D _ghost;
        private MapGrid _grid;
        private Pathfinder _pathfinder;
        private bool _validPlacement;

        public bool IsPlacing => _selectedType.HasValue;

        public override void _Ready()
        {
            _grid = ServiceLocator.Get<MapGrid>();
            _pathfinder = ServiceLocator.Get<Pathfinder>();
            CreateGhost();
        }

        private void CreateGhost()
        {
            _ghost = new MeshInstance3D();
            var box = new BoxMesh();
            box.Size = new Vector3(Constants.CELL_SIZE * 0.8f, 1f, Constants.CELL_SIZE * 0.8f);
            _ghost.Mesh = box;
            _ghost.Visible = false;

            var mat = new StandardMaterial3D();
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.AlbedoColor = new Color(0, 1, 0, 0.4f);
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            _ghost.MaterialOverride = mat;
            AddChild(_ghost);
        }

        public void StartPlacement(TowerType type)
        {
            _selectedType = type;
            _ghost.Visible = true;
        }

        public void CancelPlacement()
        {
            _selectedType = null;
            _ghost.Visible = false;
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (!IsPlacing) return;

            if (@event.IsActionPressed("cancel_placement"))
            {
                CancelPlacement();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (@event.IsActionPressed("place_tower"))
            {
                if (_validPlacement)
                    PlaceTower();
                GetViewport().SetInputAsHandled();
            }
        }

        public override void _Process(double delta)
        {
            if (!IsPlacing) return;

            // Raycast from mouse to ground
            var camera = GetViewport().GetCamera3D();
            if (camera == null) return;

            var mousePos = GetViewport().GetMousePosition();
            var from = camera.ProjectRayOrigin(mousePos);
            var dir = camera.ProjectRayNormal(mousePos);

            // Intersect with Y=0 plane
            if (Mathf.Abs(dir.Y) < 0.001f) return;
            float t = -from.Y / dir.Y;
            if (t < 0) return;
            var worldPos = from + dir * t;

            var gridPos = _grid.WorldToGrid(worldPos);
            var snappedWorld = _grid.GridToWorld(gridPos);
            _ghost.GlobalPosition = snappedWorld + new Vector3(0, 0.5f, 0);

            // Validate
            _validPlacement = _grid.CanBuild(gridPos) && !_pathfinder.WouldBlockAllPaths(gridPos);

            var mat = (StandardMaterial3D)_ghost.MaterialOverride;
            mat.AlbedoColor = _validPlacement
                ? new Color(0, 1, 0, 0.4f)
                : new Color(1, 0, 0, 0.4f);
        }

        private void PlaceTower()
        {
            if (!_selectedType.HasValue) return;

            var data = TowerRegistry.Get(_selectedType.Value);
            if (data == null) return;

            // Check if player can afford it
            if (!ServiceLocator.TryGet<ScrapManager>(out var scrapMgr)) return;
            if (!scrapMgr.TrySpend(data.ScrapCost)) return;

            var camera = GetViewport().GetCamera3D();
            var mousePos = GetViewport().GetMousePosition();
            var from = camera.ProjectRayOrigin(mousePos);
            var dir = camera.ProjectRayNormal(mousePos);
            float t = -from.Y / dir.Y;
            var worldPos = from + dir * t;
            var gridPos = _grid.WorldToGrid(worldPos);

            // Create tower
            var tower = new TowerController();
            GetTree().CurrentScene.AddChild(tower);
            tower.GlobalPosition = _grid.GridToWorld(gridPos);
            tower.Initialize(data, gridPos);

            _grid.PlaceTower(gridPos.X, gridPos.Y, tower);
            _pathfinder.RecalculateAllPaths();
            GameEvents.OnTowerPlaced?.Invoke(tower);

            // Keep placing same type (shift-click style)
            if (!Input.IsKeyPressed(Key.Shift))
                CancelPlacement();
        }
    }
}
