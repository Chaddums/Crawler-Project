using System;
using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// The core grid that underlies everything: tower placement, enemy pathing, terrain.
    /// Each cell is CELL_SIZE world units. Grid origin is (0,0) at top-left.
    /// </summary>
    public partial class MapGrid : Node3D
    {
        [Export] public int Width { get; set; } = Constants.DEFAULT_MAP_WIDTH;
        [Export] public int Height { get; set; } = Constants.DEFAULT_MAP_HEIGHT;

        private TerrainType[,] _grid;
        private Node3D[,] _towers;         // Tower reference per cell (null if empty)
        private MeshInstance3D _groundMesh;

        public override void _Ready()
        {
            _grid = new TerrainType[Width, Height];
            _towers = new Node3D[Width, Height];

            InitializeGrid();
            BuildGroundPlane();

            ServiceLocator.Register(this);
        }

        private void InitializeGrid()
        {
            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                _grid[x, y] = TerrainType.Open;
        }

        private void BuildGroundPlane()
        {
            _groundMesh = new MeshInstance3D();
            var planeMesh = new PlaneMesh();
            planeMesh.Size = new Vector2(Width * Constants.CELL_SIZE, Height * Constants.CELL_SIZE);
            _groundMesh.Mesh = planeMesh;
            _groundMesh.Position = new Vector3(
                Width * Constants.CELL_SIZE / 2f,
                0f,
                Height * Constants.CELL_SIZE / 2f
            );

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.15f, 0.12f, 0.1f); // Dark scrapyard ground
            mat.Roughness = 0.9f;
            _groundMesh.MaterialOverride = mat;

            // Static body for raycasting tower placement
            var body = new StaticBody3D();
            body.CollisionLayer = (uint)(1 << (Constants.LAYER_GROUND - 1));
            var shape = new CollisionShape3D();
            var boxShape = new BoxShape3D();
            boxShape.Size = new Vector3(Width * Constants.CELL_SIZE, 0.1f, Height * Constants.CELL_SIZE);
            shape.Shape = boxShape;
            body.AddChild(shape);
            _groundMesh.AddChild(body);

            AddChild(_groundMesh);
            BuildGridLines();
        }

        private void BuildGridLines()
        {
            var gridVisual = new MeshInstance3D();
            var im = new ImmediateMesh();
            gridVisual.Mesh = im;
            gridVisual.Position = new Vector3(0f, 0.02f, 0f);

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.3f, 0.25f, 0.2f, 0.3f);
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            gridVisual.MaterialOverride = mat;

            im.SurfaceBegin(Mesh.PrimitiveType.Lines);
            for (int x = 0; x <= Width; x++)
            {
                im.SurfaceAddVertex(new Vector3(x * Constants.CELL_SIZE, 0, 0));
                im.SurfaceAddVertex(new Vector3(x * Constants.CELL_SIZE, 0, Height * Constants.CELL_SIZE));
            }
            for (int y = 0; y <= Height; y++)
            {
                im.SurfaceAddVertex(new Vector3(0, 0, y * Constants.CELL_SIZE));
                im.SurfaceAddVertex(new Vector3(Width * Constants.CELL_SIZE, 0, y * Constants.CELL_SIZE));
            }
            im.SurfaceEnd();

            AddChild(gridVisual);
        }

        // --- Grid queries ---

        public TerrainType GetCell(int x, int y)
        {
            if (!InBounds(x, y)) return TerrainType.Blocked;
            return _grid[x, y];
        }

        public TerrainType GetCell(Vector2I pos) => GetCell(pos.X, pos.Y);

        public bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;
        public bool InBounds(Vector2I pos) => InBounds(pos.X, pos.Y);

        public bool CanBuild(int x, int y)
        {
            if (!InBounds(x, y)) return false;
            return _grid[x, y] == TerrainType.Open && _towers[x, y] == null;
        }

        public bool CanBuild(Vector2I pos) => CanBuild(pos.X, pos.Y);

        public bool IsWalkable(int x, int y)
        {
            if (!InBounds(x, y)) return false;
            var t = _grid[x, y];
            return t == TerrainType.Open || t == TerrainType.Path || t == TerrainType.SpawnPoint || t == TerrainType.Core;
        }

        public bool IsWalkable(Vector2I pos) => IsWalkable(pos.X, pos.Y);

        // --- Grid mutations ---

        public void SetCell(int x, int y, TerrainType type)
        {
            if (!InBounds(x, y)) return;
            _grid[x, y] = type;
            GameEvents.OnTerrainChanged?.Invoke(new Vector2I(x, y));
        }

        public void SetCell(Vector2I pos, TerrainType type) => SetCell(pos.X, pos.Y, type);

        /// <summary>
        /// Set cell without firing events. Used by Pathfinder for speculative checks.
        /// </summary>
        public void SetCellSilent(int x, int y, TerrainType type)
        {
            if (!InBounds(x, y)) return;
            _grid[x, y] = type;
        }

        public void SetCellSilent(Vector2I pos, TerrainType type) => SetCellSilent(pos.X, pos.Y, type);

        public void PlaceTower(int x, int y, Node3D tower)
        {
            _grid[x, y] = TerrainType.TowerSlot;
            _towers[x, y] = tower;
            GameEvents.OnTerrainChanged?.Invoke(new Vector2I(x, y));
        }

        public void RemoveTower(int x, int y)
        {
            _grid[x, y] = TerrainType.Open;
            _towers[x, y] = null;
            GameEvents.OnTerrainChanged?.Invoke(new Vector2I(x, y));
        }

        public Node3D GetTower(int x, int y) => InBounds(x, y) ? _towers[x, y] : null;

        // --- Coordinate conversion ---

        public Vector3 GridToWorld(int x, int y)
        {
            return new Vector3(
                (x + 0.5f) * Constants.CELL_SIZE,
                0f,
                (y + 0.5f) * Constants.CELL_SIZE
            );
        }

        public Vector3 GridToWorld(Vector2I pos) => GridToWorld(pos.X, pos.Y);

        public Vector2I WorldToGrid(Vector3 worldPos)
        {
            return new Vector2I(
                Mathf.FloorToInt(worldPos.X / Constants.CELL_SIZE),
                Mathf.FloorToInt(worldPos.Z / Constants.CELL_SIZE)
            );
        }

        // --- Spawn / Core positions ---

        private readonly List<Vector2I> _spawnPoints = new();
        private Vector2I _corePosition;

        public IReadOnlyList<Vector2I> SpawnPoints => _spawnPoints;
        public Vector2I CorePosition => _corePosition;

        public void SetSpawnPoint(int x, int y)
        {
            SetCell(x, y, TerrainType.SpawnPoint);
            _spawnPoints.Add(new Vector2I(x, y));
        }

        public void SetCorePosition(int x, int y)
        {
            SetCell(x, y, TerrainType.Core);
            _corePosition = new Vector2I(x, y);
        }

        // --- Terrain manipulation (Design Pillar #3) ---

        public bool BulldozeDebris(int x, int y)
        {
            if (!InBounds(x, y) || _grid[x, y] != TerrainType.Debris) return false;
            SetCell(x, y, TerrainType.Open);
            return true;
        }

        public bool PileDebris(int x, int y)
        {
            if (!InBounds(x, y) || _grid[x, y] != TerrainType.Debris) return false;
            SetCell(x, y, TerrainType.Blocked);
            return true;
        }

        public override void _ExitTree()
        {
            ServiceLocator.Unregister<MapGrid>();
        }
    }
}
