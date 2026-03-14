using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// A* pathfinding on the MapGrid. Recalculates when terrain changes.
    /// Enemies query this for their route from spawn to core.
    /// </summary>
    public partial class Pathfinder : Node
    {
        private MapGrid _grid;
        private bool _dirty;

        // Cached paths from each spawn point to the core
        private readonly Dictionary<Vector2I, List<Vector2I>> _cachedPaths = new();

        private static readonly Vector2I[] Neighbors = {
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1)
        };

        public override void _Ready()
        {
            ServiceLocator.Register(this);
        }

        public void Initialize(MapGrid grid)
        {
            _grid = grid;
            GameEvents.OnTerrainChanged += _ => _dirty = true;
            RecalculateAllPaths();
        }

        public void RecalculateAllPaths()
        {
            _cachedPaths.Clear();
            _dirty = false;
            foreach (var spawn in _grid.SpawnPoints)
            {
                var path = FindPath(spawn, _grid.CorePosition);
                if (path != null)
                    _cachedPaths[spawn] = path;
                else
                    GD.PushWarning($"[Pathfinder] No path from spawn {spawn} to core {_grid.CorePosition}");
            }
            GD.Print($"[Pathfinder] Cached {_cachedPaths.Count} paths for {_grid.SpawnPoints.Count} spawn points");
            GameEvents.OnPathRecalculated?.Invoke();
        }

        public override void _PhysicsProcess(double delta)
        {
            if (_dirty)
                RecalculateAllPaths();
        }

        public List<Vector2I> GetCachedPath(Vector2I spawn)
        {
            if (_dirty)
                RecalculateAllPaths();
            return _cachedPaths.TryGetValue(spawn, out var path) ? path : null;
        }

        /// <summary>
        /// Check if placing a tower at (x,y) would block ALL paths.
        /// Used to prevent the player from completely sealing off enemies.
        /// </summary>
        public bool WouldBlockAllPaths(Vector2I pos)
        {
            var originalType = _grid.GetCell(pos);
            _grid.SetCellSilent(pos, TerrainType.TowerSlot);

            bool anyPathExists = false;
            foreach (var spawn in _grid.SpawnPoints)
            {
                var path = FindPath(spawn, _grid.CorePosition);
                if (path != null)
                {
                    anyPathExists = true;
                    break;
                }
            }

            _grid.SetCellSilent(pos, originalType);
            return !anyPathExists;
        }

        /// <summary>
        /// Standard A* on the grid. Returns null if no path exists.
        /// </summary>
        public List<Vector2I> FindPath(Vector2I start, Vector2I end)
        {
            if (!_grid.InBounds(start) || !_grid.InBounds(end)) return null;

            var openSet = new PriorityQueue<Vector2I, float>();
            var cameFrom = new Dictionary<Vector2I, Vector2I>();
            var gScore = new Dictionary<Vector2I, float>();
            var closedSet = new HashSet<Vector2I>();

            gScore[start] = 0;
            openSet.Enqueue(start, Heuristic(start, end));

            while (openSet.Count > 0)
            {
                var current = openSet.Dequeue();

                if (current == end)
                    return ReconstructPath(cameFrom, current);

                if (closedSet.Contains(current)) continue;
                closedSet.Add(current);

                foreach (var dir in Neighbors)
                {
                    var neighbor = current + dir;

                    if (!_grid.IsWalkable(neighbor.X, neighbor.Y)) continue;
                    if (closedSet.Contains(neighbor)) continue;

                    float tentativeG = gScore[current] + 1f;

                    if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeG;
                        float f = tentativeG + Heuristic(neighbor, end);
                        openSet.Enqueue(neighbor, f);
                    }
                }
            }

            return null; // No path found
        }

        private static float Heuristic(Vector2I a, Vector2I b)
        {
            return Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);
        }

        private static List<Vector2I> ReconstructPath(Dictionary<Vector2I, Vector2I> cameFrom, Vector2I current)
        {
            var path = new List<Vector2I> { current };
            while (cameFrom.ContainsKey(current))
            {
                current = cameFrom[current];
                path.Add(current);
            }
            path.Reverse();
            return path;
        }

        public override void _ExitTree()
        {
            ServiceLocator.Unregister<Pathfinder>();
        }
    }
}
