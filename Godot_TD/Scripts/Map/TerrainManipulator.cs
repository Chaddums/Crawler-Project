using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Handles terrain manipulation (Pillar #3).
    /// Toggle terrain mode with T, then click debris to bulldoze or pile.
    /// </summary>
    public partial class TerrainManipulator : Node3D
    {
        private bool _terrainMode;
        private MeshInstance3D _highlight;
        private Label3D _actionLabel;
        private MapGrid _grid;
        private Pathfinder _pathfinder;
        private Vector2I _hoveredCell = new(-1, -1);

        public bool IsActive => _terrainMode;

        public override void _Ready()
        {
            _grid = ServiceLocator.Get<MapGrid>();
            _pathfinder = ServiceLocator.Get<Pathfinder>();

            // Highlight cursor
            _highlight = new MeshInstance3D();
            var box = new BoxMesh();
            box.Size = new Vector3(Constants.CELL_SIZE * 0.9f, 0.05f, Constants.CELL_SIZE * 0.9f);
            _highlight.Mesh = box;
            _highlight.Visible = false;

            var mat = new StandardMaterial3D();
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.AlbedoColor = new Color(0.9f, 0.7f, 0.2f, 0.5f);
            _highlight.MaterialOverride = mat;
            AddChild(_highlight);

            // Action label floating above cursor
            _actionLabel = new Label3D();
            _actionLabel.FontSize = 32;
            _actionLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            _actionLabel.NoDepthTest = true;
            _actionLabel.PixelSize = 0.004f;
            _actionLabel.OutlineSize = 6;
            _actionLabel.Visible = false;
            AddChild(_actionLabel);

            ServiceLocator.Register(this);
        }

        public void ToggleMode()
        {
            _terrainMode = !_terrainMode;
            _highlight.Visible = _terrainMode;
            _actionLabel.Visible = false;
            GD.Print($"[Terrain] Mode: {(_terrainMode ? "ON" : "OFF")}");
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event.IsActionPressed("toggle_terrain"))
            {
                ToggleMode();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (!_terrainMode) return;

            if (@event is InputEventMouseButton mb && mb.Pressed)
            {
                if (mb.ButtonIndex == MouseButton.Left)
                {
                    TryBulldoze();
                    GetViewport().SetInputAsHandled();
                }
                else if (mb.ButtonIndex == MouseButton.Right)
                {
                    TryPile();
                    GetViewport().SetInputAsHandled();
                }
            }
        }

        public override void _Process(double delta)
        {
            if (!_terrainMode) return;

            var camera = GetViewport().GetCamera3D();
            if (camera == null) return;

            var mousePos = GetViewport().GetMousePosition();
            var from = camera.ProjectRayOrigin(mousePos);
            var dir = camera.ProjectRayNormal(mousePos);
            if (Mathf.Abs(dir.Y) < 0.001f) return;
            float t = -from.Y / dir.Y;
            if (t < 0) return;
            var worldPos = from + dir * t;

            _hoveredCell = _grid.WorldToGrid(worldPos);
            var snapped = _grid.GridToWorld(_hoveredCell);
            _highlight.GlobalPosition = snapped + new Vector3(0, 0.05f, 0);

            var cellType = _grid.GetCell(_hoveredCell);
            if (cellType == TerrainType.Debris)
            {
                _highlight.Visible = true;
                _actionLabel.Visible = true;
                _actionLabel.GlobalPosition = snapped + new Vector3(0, 1.2f, 0);
                _actionLabel.Text = $"LMB: Bulldoze ({Constants.BULLDOZE_COST}s)\nRMB: Pile ({Constants.PILE_COST}s)";
                _actionLabel.Modulate = new Color(0.9f, 0.7f, 0.2f);

                var mat = (StandardMaterial3D)_highlight.MaterialOverride;
                mat.AlbedoColor = new Color(0.9f, 0.7f, 0.2f, 0.5f);
            }
            else
            {
                _actionLabel.Visible = false;
                var mat = (StandardMaterial3D)_highlight.MaterialOverride;
                mat.AlbedoColor = new Color(0.5f, 0.5f, 0.5f, 0.2f);
            }
        }

        private void TryBulldoze()
        {
            if (_grid.GetCell(_hoveredCell) != TerrainType.Debris) return;
            if (!ServiceLocator.TryGet<ScrapManager>(out var scrap)) return;
            if (!scrap.TrySpend(Constants.BULLDOZE_COST)) return;

            _grid.BulldozeDebris(_hoveredCell.X, _hoveredCell.Y);
            _pathfinder.RecalculateAllPaths();

            // Remove the debris visual (find child meshes near this position)
            RemoveDebrisVisual(_hoveredCell);

            GD.Print($"[Terrain] Bulldozed debris at {_hoveredCell}");
        }

        private void TryPile()
        {
            if (_grid.GetCell(_hoveredCell) != TerrainType.Debris) return;
            if (!ServiceLocator.TryGet<ScrapManager>(out var scrap)) return;

            // Check if piling would block all paths
            _grid.SetCellSilent(_hoveredCell, TerrainType.Blocked);
            bool blocked = true;
            foreach (var spawn in _grid.SpawnPoints)
            {
                if (_pathfinder.FindPath(spawn, _grid.CorePosition) != null)
                {
                    blocked = false;
                    break;
                }
            }
            _grid.SetCellSilent(_hoveredCell, TerrainType.Debris);

            if (blocked)
            {
                GD.Print("[Terrain] Can't pile — would block all paths!");
                return;
            }

            if (!scrap.TrySpend(Constants.PILE_COST)) return;

            _grid.PileDebris(_hoveredCell.X, _hoveredCell.Y);
            _pathfinder.RecalculateAllPaths();

            // Replace debris visual with wall visual
            RemoveDebrisVisual(_hoveredCell);
            BuildWallVisual(_hoveredCell);

            GD.Print($"[Terrain] Piled debris at {_hoveredCell}");
        }

        private void RemoveDebrisVisual(Vector2I gridPos)
        {
            var worldPos = _grid.GridToWorld(gridPos);
            foreach (var child in _grid.GetChildren())
            {
                if (child is MeshInstance3D mesh && child != _highlight)
                {
                    var diff = mesh.Position - worldPos;
                    diff.Y = 0;
                    if (diff.Length() < Constants.CELL_SIZE * 0.6f &&
                        mesh.Position.Y < 1f && mesh.Position.Y > 0f)
                    {
                        mesh.QueueFree();
                        return;
                    }
                }
            }
        }

        private void BuildWallVisual(Vector2I gridPos)
        {
            var mesh = new MeshInstance3D();
            var box = new BoxMesh();
            box.Size = new Vector3(Constants.CELL_SIZE * 0.9f, 1.5f, Constants.CELL_SIZE * 0.9f);
            mesh.Mesh = box;
            mesh.Position = _grid.GridToWorld(gridPos) + new Vector3(0, 0.75f, 0);

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.35f, 0.3f, 0.25f);
            mat.Roughness = 0.85f;
            mesh.MaterialOverride = mat;
            _grid.AddChild(mesh);
        }

        public override void _ExitTree()
        {
            ServiceLocator.Unregister<TerrainManipulator>();
        }
    }
}
