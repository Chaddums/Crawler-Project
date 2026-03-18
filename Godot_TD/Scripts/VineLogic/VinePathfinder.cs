using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// A* pathfinder for Vine Logic TD. Respects dynamic gate/switch states.
    /// Recalculates when gates open/close or switches toggle.
    /// </summary>
    public partial class VinePathfinder : Node
    {
        private VineGrid _grid;
        private bool _dirty;

        private readonly Dictionary<Vector2I, List<Vector2I>> _cachedPaths = new();

        private static readonly Vector2I[] Neighbors = {
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1)
        };

        public override void _Ready()
        {
            ServiceLocator.Register(this);
        }

        public void Initialize(VineGrid grid)
        {
            _grid = grid;
            GameEvents.OnVinePathRecalculated += () => _dirty = true;
            RecalculateAllPaths();
        }

        public void RecalculateAllPaths()
        {
            _cachedPaths.Clear();
            _dirty = false;

            // Cache paths from entry points (backward compat)
            foreach (var entry in _grid.EntryPoints)
            {
                var path = FindPath(entry, _grid.ExitPoint);
                if (path != null)
                    _cachedPaths[entry] = path;
                else
                    GD.PushWarning($"[VinePathfinder] No path from entry {entry} to exit {_grid.ExitPoint}");
            }

            // Also cache paths from entry region centers
            foreach (var region in _grid.EntryRegions)
            {
                var center = region.Center;
                if (_cachedPaths.ContainsKey(center)) continue;
                var path = FindPath(center, _grid.ExitPoint);
                if (path != null)
                    _cachedPaths[center] = path;
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            if (_dirty)
                RecalculateAllPaths();
        }

        public List<Vector2I> GetCachedPath(Vector2I entry)
        {
            if (_dirty) RecalculateAllPaths();
            return _cachedPaths.TryGetValue(entry, out var path) ? path : null;
        }

        /// <summary>
        /// Find a path from an arbitrary start position to the exit.
        /// Used for chaotic spawning where enemies start at random cells within entry regions.
        /// </summary>
        public List<Vector2I> FindPathFromPosition(Vector2I start)
        {
            return FindPath(start, _grid.ExitPoint);
        }

        /// <summary>
        /// Check if placing a node at this position would block all paths.
        /// </summary>
        public bool WouldBlockAllPaths(Vector2I pos)
        {
            var oldCell = _grid.GetCell(pos);
            // Temporarily mark as wall
            // (We can't use SetCell since VineGrid doesn't expose silent set — just check walkability)
            // Instead, run A* with this cell excluded
            foreach (var entry in _grid.EntryPoints)
            {
                var path = FindPath(entry, _grid.ExitPoint, excludeCell: pos);
                if (path != null) return false;
            }
            return true;
        }

        /// <summary>
        /// Find path for ghost faction — ignores gate/latch state, only respects walls.
        /// </summary>
        public List<Vector2I> FindGhostPath(Vector2I start, Vector2I end)
        {
            return FindPath(start, end, excludeCell: null, ghostMode: true);
        }

        public List<Vector2I> FindPath(Vector2I start, Vector2I end,
            Vector2I? excludeCell = null, bool ghostMode = false)
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

                    if (excludeCell.HasValue && neighbor == excludeCell.Value) continue;
                    if (closedSet.Contains(neighbor)) continue;

                    // Ghost mode: only walls block. Normal mode: respect walkability.
                    if (ghostMode)
                    {
                        if (_grid.GetCell(neighbor) == VineCellType.Wall) continue;
                    }
                    else
                    {
                        if (!_grid.IsWalkable(neighbor.X, neighbor.Y)) continue;
                    }

                    // Cost: terrain and node modifiers
                    float moveCost = 1f;
                    var neighborCell = _grid.GetCell(neighbor);
                    if (neighborCell == VineCellType.DataStream)
                        moveCost = 0.5f;  // Enemies prefer data streams
                    else if (neighborCell == VineCellType.Channel)
                        moveCost = 0.8f;  // Slight preference for channels

                    if (!ghostMode)
                    {
                        var node = _grid.GetNode(neighbor);
                        if (node != null)
                        {
                            // Slow fields increase path cost (enemies prefer avoiding them)
                            if (node.Data?.Type == VineNodeType.SlowField && node.IsActive)
                                moveCost = 2f;
                        }
                    }

                    float tentativeG = gScore[current] + moveCost;

                    if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeG;
                        openSet.Enqueue(neighbor, tentativeG + Heuristic(neighbor, end));
                    }
                }
            }

            return null;
        }

        private static float Heuristic(Vector2I a, Vector2I b) =>
            Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);

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
            ServiceLocator.Unregister<VinePathfinder>();
        }
    }
}
