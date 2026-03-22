using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Manages the Arcanist's socket grid. Sockets are buildable grid cells
    /// that extend outward from the spire. Towers are placed into sockets.
    /// Magic can be infused into sockets, affecting towers in adjacent sockets.
    /// </summary>
    public partial class ArcanistSocketGrid : Node3D
    {
        public class SocketCell
        {
            public Vector2I GridPos;
            public VineNode OccupyingTower;
            public MaterialType? Infusion;
            public bool IsPrism;
            public MeshInstance3D Visual;
            public MeshInstance3D BorderVisual;
        }

        private readonly Dictionary<Vector2I, SocketCell> _sockets = new();
        private Vector2I _spireCell;
        private VineGrid _grid;

        private static readonly Vector2I[] CardinalDirs = {
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1)
        };

        public void Initialize(Vector2I spireCell)
        {
            _spireCell = spireCell;
            _grid = ServiceLocator.Get<VineGrid>();

            // The spire cell itself is the root socket
            var rootSocket = new SocketCell { GridPos = spireCell };
            _sockets[spireCell] = rootSocket;
            BuildSocketVisual(rootSocket);

            // Auto-build sockets in 4 cardinal neighbors
            foreach (var dir in CardinalDirs)
            {
                var neighbor = spireCell + dir;
                if (_grid.InBounds(neighbor.X, neighbor.Y) && _grid.CanPlace(neighbor))
                    BuildSocket(neighbor);
            }

            // Show/hide based on phase
            GameEvents.OnPhaseChanged += phase => SetVisualsVisible(phase == GamePhase.Build);

            ServiceLocator.Register(this);
            GD.Print($"[ArcanistSocket] Initialized at ({spireCell.X}, {spireCell.Y}) with {_sockets.Count} sockets");
        }

        /// <summary>
        /// Can a new socket be built at this cell?
        /// Must be empty in VineGrid and adjacent to an existing socket.
        /// </summary>
        public bool CanBuildSocket(Vector2I cell)
        {
            if (_sockets.ContainsKey(cell)) return false;
            if (!_grid.InBounds(cell.X, cell.Y)) return false;
            if (!_grid.CanPlace(cell)) return false;

            // Must be adjacent to at least one existing socket
            foreach (var dir in CardinalDirs)
            {
                if (_sockets.ContainsKey(cell + dir))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Build a new socket at the given cell.
        /// </summary>
        public bool BuildSocket(Vector2I cell)
        {
            if (!CanBuildSocket(cell)) return false;

            var socket = new SocketCell { GridPos = cell };
            _sockets[cell] = socket;

            // Mark as wall in VineGrid for pathfinding
            _grid.SetWall(cell.X, cell.Y);

            BuildSocketVisual(socket);
            return true;
        }

        /// <summary>
        /// Can a tower be placed into this socket?
        /// </summary>
        public bool CanPlaceTower(Vector2I cell)
        {
            if (!_sockets.TryGetValue(cell, out var socket)) return false;
            if (cell == _spireCell) return false; // Can't place tower on top of spire
            return socket.OccupyingTower == null;
        }

        /// <summary>
        /// Place a tower into a socket.
        /// </summary>
        public bool PlaceTower(VineNode tower, Vector2I cell)
        {
            if (!CanPlaceTower(cell)) return false;

            var socket = _sockets[cell];
            socket.OccupyingTower = tower;
            tower.GlobalPosition = _grid.GridToWorld(cell);
            UpdateSocketVisual(socket);
            return true;
        }

        /// <summary>
        /// Remove a tower from its socket (sell/refund).
        /// </summary>
        public void RemoveTower(Vector2I cell)
        {
            if (!_sockets.TryGetValue(cell, out var socket)) return;
            socket.OccupyingTower = null;
            UpdateSocketVisual(socket);
        }

        /// <summary>
        /// Check if a cell is a socket.
        /// </summary>
        public bool IsSocket(Vector2I cell) => _sockets.ContainsKey(cell);

        /// <summary>
        /// Get the socket at a cell, or null.
        /// </summary>
        public SocketCell GetSocket(Vector2I cell)
            => _sockets.TryGetValue(cell, out var s) ? s : null;

        /// <summary>
        /// Infuse a socket with magic. Affects towers in adjacent sockets.
        /// </summary>
        public void InfuseSocket(Vector2I cell, MaterialType magic)
        {
            if (!_sockets.TryGetValue(cell, out var socket)) return;
            socket.Infusion = magic;
            UpdateSocketVisual(socket);
            GD.Print($"[ArcanistSocket] Infused ({cell.X},{cell.Y}) with {magic}");
        }

        /// <summary>
        /// Get all magic infusions affecting a tower at a given cell.
        /// Checks adjacent sockets for infusions, and follows Prisms for relay.
        /// </summary>
        public List<MaterialType> GetInfusionsAffecting(Vector2I towerCell)
        {
            var result = new List<MaterialType>();
            var visited = new HashSet<Vector2I>();
            CollectInfusions(towerCell, result, visited);
            return result;
        }

        private void CollectInfusions(Vector2I cell, List<MaterialType> result, HashSet<Vector2I> visited)
        {
            if (!visited.Add(cell)) return;

            foreach (var dir in CardinalDirs)
            {
                var neighbor = cell + dir;
                if (!_sockets.TryGetValue(neighbor, out var socket)) continue;

                if (socket.Infusion.HasValue && !result.Contains(socket.Infusion.Value))
                    result.Add(socket.Infusion.Value);

                // Prisms relay: follow through to their neighbors
                if (socket.IsPrism)
                    CollectInfusions(neighbor, result, visited);
            }
        }

        /// <summary>
        /// Get all sockets for iteration.
        /// </summary>
        public IEnumerable<SocketCell> AllSockets => _sockets.Values;

        public void SetVisualsVisible(bool visible)
        {
            foreach (var socket in _sockets.Values)
            {
                if (socket.Visual != null) socket.Visual.Visible = visible;
                if (socket.BorderVisual != null) socket.BorderVisual.Visible = visible;
            }
        }

        private void BuildSocketVisual(SocketCell socket)
        {
            var worldPos = _grid.GridToWorld(socket.GridPos);
            float cellSize = Constants.VINE_CELL_SIZE;

            // Flat platform
            var platform = new MeshInstance3D();
            platform.Mesh = new BoxMesh
            {
                Size = new Vector3(cellSize * 0.9f, 0.08f, cellSize * 0.9f)
            };
            platform.GlobalPosition = worldPos + new Vector3(0, 0.04f, 0);

            var platMat = new StandardMaterial3D();
            platMat.AlbedoColor = new Color(0.1f, 0.2f, 0.15f, 0.6f);
            platMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            platMat.EmissionEnabled = true;
            platMat.Emission = new Color(0.2f, 0.9f, 0.4f);
            platMat.EmissionEnergyMultiplier = 0.15f;
            platform.MaterialOverride = platMat;
            AddChild(platform);
            socket.Visual = platform;

            // Border ring
            var border = new MeshInstance3D();
            var torus = new TorusMesh();
            torus.InnerRadius = cellSize * 0.4f;
            torus.OuterRadius = cellSize * 0.45f;
            torus.Rings = 16;
            torus.RingSegments = 8;
            border.Mesh = torus;
            border.GlobalPosition = worldPos + new Vector3(0, 0.06f, 0);
            border.Rotation = new Vector3(Mathf.Pi * 0.5f, 0, 0);

            var borderMat = new StandardMaterial3D();
            borderMat.AlbedoColor = new Color(0.2f, 0.9f, 0.4f, 0.3f);
            borderMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            borderMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            borderMat.EmissionEnabled = true;
            borderMat.Emission = new Color(0.2f, 0.9f, 0.4f);
            borderMat.EmissionEnergyMultiplier = 0.2f;
            border.MaterialOverride = borderMat;
            AddChild(border);
            socket.BorderVisual = border;
        }

        private void UpdateSocketVisual(SocketCell socket)
        {
            if (socket.Visual?.MaterialOverride is not StandardMaterial3D mat) return;

            if (socket.Infusion.HasValue)
            {
                var magicColor = VineHarvester.GetMaterialColor(socket.Infusion.Value);
                mat.Emission = magicColor;
                mat.EmissionEnergyMultiplier = 0.4f;
                mat.AlbedoColor = new Color(magicColor.R, magicColor.G, magicColor.B, 0.5f);
            }
            else if (socket.OccupyingTower != null)
            {
                mat.Emission = new Color(0.2f, 0.9f, 0.4f);
                mat.EmissionEnergyMultiplier = 0.3f;
                mat.AlbedoColor = new Color(0.15f, 0.3f, 0.2f, 0.7f);
            }
            else
            {
                mat.Emission = new Color(0.2f, 0.9f, 0.4f);
                mat.EmissionEnergyMultiplier = 0.15f;
                mat.AlbedoColor = new Color(0.1f, 0.2f, 0.15f, 0.6f);
            }
        }

        public override void _ExitTree()
        {
            ServiceLocator.Unregister<ArcanistSocketGrid>();
        }
    }
}
