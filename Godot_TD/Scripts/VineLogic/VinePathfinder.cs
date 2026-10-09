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

        // ── Flow field: every walkable cell's cost to reach the Spire ──
        // Enemies walk down it, so the maze the player builds is the route they take (they used
        // to beeline whenever the straight line was clear and only detour when blocked).
        private float[,] _flow;
        /// <summary>Bumped every time the field is rebuilt (a tower placed or sold, terrain changed).</summary>
        public int Version { get; private set; }

        /// <summary>Cost from <paramref name="cell"/> to the Spire along the field (infinity if cut off).</summary>
        public float FlowDistance(Vector2I cell)
        {
            if (_dirty) RecalculateAllPaths();
            return _flow != null && _grid.InBounds(cell) ? _flow[cell.X, cell.Y] : float.PositiveInfinity;
        }

        /// <summary>The route from <paramref name="from"/> to the Spire, cell by cell, down the flow field.</summary>
        public List<Vector2I> FlowPath(Vector2I from, int maxSteps = 600)
        {
            if (_dirty) RecalculateAllPaths();
            if (_flow == null || !_grid.InBounds(from)) return null;
            var path = new List<Vector2I> { from };
            var cur = from;
            // Standing in a solid cell (just built on): step out to the best open neighbour
            for (int i = 0; i < maxSteps && cur != _grid.ExitPoint; i++)
            {
                float best = _flow[cur.X, cur.Y];
                Vector2I next = cur;
                foreach (var d in Neighbors)
                {
                    var n = cur + d;
                    if (!_grid.InBounds(n)) continue;
                    float f = _flow[n.X, n.Y];
                    if (f < best) { best = f; next = n; }
                }
                if (next == cur) break;
                path.Add(next);
                cur = next;
            }
            return path.Count > 1 || from == _grid.ExitPoint ? path : null;
        }

        /// <summary>
        /// The route from <paramref name="from"/> as it would be with <paramref name="blocked"/>
        /// built on (the build ghost's preview), without touching the live field.
        /// </summary>
        public List<Vector2I> PreviewPath(Vector2I from, Vector2I blocked)
        {
            var field = BuildFlow(blocked);
            if (field == null || !_grid.InBounds(from)) return null;
            var path = new List<Vector2I> { from };
            var cur = from;
            for (int i = 0; i < 600 && cur != _grid.ExitPoint; i++)
            {
                float best = field[cur.X, cur.Y];
                Vector2I next = cur;
                foreach (var d in Neighbors)
                {
                    var n = cur + d;
                    if (!_grid.InBounds(n)) continue;
                    if (field[n.X, n.Y] < best) { best = field[n.X, n.Y]; next = n; }
                }
                if (next == cur) break;
                path.Add(next);
                cur = next;
            }
            return path.Count > 1 ? path : null;
        }

        /// <summary>Dijkstra out from the Spire over walkable cells, with A*'s move costs.</summary>
        private float[,] BuildFlow(Vector2I? blocked = null)
        {
            if (_grid == null) return null;
            int w = _grid.Width, h = _grid.Height;
            var dist = new float[w, h];
            for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) dist[x, y] = float.PositiveInfinity;
            var exit = _grid.ExitPoint;
            if (!_grid.InBounds(exit)) return dist;
            var open = new PriorityQueue<Vector2I, float>();
            dist[exit.X, exit.Y] = 0f;
            open.Enqueue(exit, 0f);
            while (open.Count > 0)
            {
                open.TryDequeue(out var cur, out float d);
                if (d > dist[cur.X, cur.Y]) continue;
                foreach (var dir in Neighbors)
                {
                    var n = cur + dir; // n steps onto cur
                    if (!_grid.InBounds(n) || (blocked.HasValue && n == blocked.Value)) continue;
                    if (!_grid.IsWalkable(n.X, n.Y) && n != exit) continue;
                    float nd = d + StepCost(n, cur);
                    if (nd < dist[n.X, n.Y]) { dist[n.X, n.Y] = nd; open.Enqueue(n, nd); }
                }
            }
            return dist;
        }

        /// <summary>Cost of stepping from <paramref name="from"/> onto <paramref name="to"/> (as in FindPath).</summary>
        private float StepCost(Vector2I from, Vector2I to)
        {
            var cell = _grid.GetCell(to);
            float c = cell switch
            {
                VineCellType.DataStream => 0.5f,
                VineCellType.Channel => 0.8f,
                VineCellType.Hazard => 1.5f,
                _ => 1f,
            };
            var node = _grid.GetNode(to);
            if (node?.Data?.Type == VineNodeType.SlowField && node.IsActive) c = 2f;
            float dh = Mathf.Abs(_grid.GetCellHeight(from) - _grid.GetCellHeight(to));
            if (dh > Constants.STEEP_THRESHOLD) c += dh * Constants.SLOPE_COST_FACTOR;
            return c;
        }

        public void RecalculateAllPaths()
        {
            _cachedPaths.Clear();
            _dirty = false;
            _flow = BuildFlow();
            Version++;

            // Cache paths from active entry points only (inactive regions are gated by shield walls)
            foreach (var entry in _grid.EntryPoints)
            {
                // Skip entry points belonging to inactive regions
                bool isActive = false;
                foreach (var region in _grid.EntryRegions)
                {
                    if (region.Active && region.Center == entry) { isActive = true; break; }
                }
                if (!isActive && _grid.ActiveEntryRegions.Count > 0) continue;

                var path = FlowPathNoRecalc(entry) ?? FindPath(entry, _grid.ExitPoint);
                if (path != null)
                    _cachedPaths[entry] = path;
                else
                    GD.PushWarning($"[VinePathfinder] No path from entry {entry} to exit {_grid.ExitPoint}");
            }

            // Also cache paths from active entry region centers
            foreach (var region in _grid.ActiveEntryRegions)
            {
                var center = region.Center;
                if (_cachedPaths.ContainsKey(center)) continue;
                var path = FlowPathNoRecalc(center) ?? FindPath(center, _grid.ExitPoint);
                if (path != null)
                    _cachedPaths[center] = path;
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            long __pt = FrameProfiler.Start();
            try
            {
                if (_dirty)
                    RecalculateAllPaths();
        
            }
            finally { FrameProfiler.Stop("pathfinder", __pt); }
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
            return FlowPath(start) ?? FindPath(start, _grid.ExitPoint);
        }

        // FlowPath without the dirty check (used while rebuilding)
        private List<Vector2I> FlowPathNoRecalc(Vector2I from)
        {
            bool d = _dirty;
            _dirty = false;
            var p = FlowPath(from);
            _dirty = d;
            return p;
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

                    // Ghost mode: only walls and pits block. Normal mode: respect walkability.
                    var neighborCell = _grid.GetCell(neighbor);
                    if (ghostMode)
                    {
                        if (neighborCell == VineCellType.Wall || neighborCell == VineCellType.Pit
                            || neighborCell == VineCellType.DestructibleWall) continue;
                    }
                    else
                    {
                        if (!_grid.IsWalkable(neighbor.X, neighbor.Y)) continue;
                    }

                    // Cost: terrain and node modifiers
                    float moveCost = 1f;
                    if (neighborCell == VineCellType.DataStream)
                        moveCost = 0.5f;  // Enemies prefer data streams
                    else if (neighborCell == VineCellType.Channel)
                        moveCost = 0.8f;  // Slight preference for channels
                    else if (neighborCell == VineCellType.Hazard)
                        moveCost = 1.5f;  // Enemies path through hazards but prefer not to

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

                    // Slope cost — penalize steep terrain
                    float heightDiff = Mathf.Abs(_grid.GetCellHeight(current) - _grid.GetCellHeight(neighbor));
                    if (heightDiff > Constants.STEEP_THRESHOLD)
                        moveCost += heightDiff * Constants.SLOPE_COST_FACTOR;

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
