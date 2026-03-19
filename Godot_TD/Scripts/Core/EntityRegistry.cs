using System;
using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// EntityRegistry - Tracks all live entities by type and position.
    /// Uses a pre-allocated flat-array spatial grid for fast proximity queries.
    /// Zero per-frame allocations: cell lists are cleared and refilled in-place.
    ///
    /// Ported from HoldtheLine's autoload. Grid dimensions sized for the vine TD map:
    /// 80x48 world units (VINE_MAP_WIDTH * CELL_SIZE x VINE_MAP_HEIGHT * CELL_SIZE).
    /// </summary>
    public partial class EntityRegistry : Node
    {
        // Entity type constants
        public const string TYPE_TOWER = "tower";
        public const string TYPE_ENEMY = "enemy";
        public const string TYPE_PROJECTILE = "projectile";
        public const string TYPE_PLAYER = "player";

        // --- Spatial Grid ---
        // Covers the vine TD world plus margin. Grid cell size ~16 units.
        private const float CELL_SIZE = 16f;
        // World covers roughly 0 to VINE_MAP_WIDTH * VINE_CELL_SIZE (80) in X
        // and 0 to VINE_MAP_HEIGHT * VINE_CELL_SIZE (48) in Z, plus margin.
        // 7 cells of 16 = 112 covers 0..112 comfortably for an 80x48 world.
        private const int GRID_DIM_X = 7;
        private const int GRID_DIM_Z = 5;
        private const int GRID_TOTAL = GRID_DIM_X * GRID_DIM_Z; // 35

        // Entities stored by type
        private readonly Dictionary<string, List<Node3D>> _entities = new();

        // Per-type spatial grids. Each value is a flat array of GRID_TOTAL lists (pre-allocated once).
        private readonly Dictionary<string, List<Node3D>[]> _typeGrids = new();
        // Frame when each type's grid was last rebuilt.
        private readonly Dictionary<string, long> _typeFrame = new();

        public override void _Ready()
        {
            ServiceLocator.Register(this);
        }

        // =============================================================================
        // Registration
        // =============================================================================

        public void Register(Node3D entity, string entityType)
        {
            if (!_entities.TryGetValue(entityType, out var list))
            {
                list = new List<Node3D>();
                _entities[entityType] = list;
            }
            if (!list.Contains(entity))
                list.Add(entity);
        }

        public void Unregister(Node3D entity, string entityType)
        {
            if (_entities.TryGetValue(entityType, out var list))
                list.Remove(entity);
        }

        public List<Node3D> GetAll(string entityType)
        {
            return _entities.TryGetValue(entityType, out var list) ? list : new List<Node3D>();
        }

        public int GetCount(string entityType)
        {
            return _entities.TryGetValue(entityType, out var list) ? list.Count : 0;
        }

        // =============================================================================
        // Spatial Grid Internals
        // =============================================================================

        private int CellIndexFromPos(Vector3 pos)
        {
            int cx = Mathf.Clamp((int)(pos.X / CELL_SIZE), 0, GRID_DIM_X - 1);
            int cz = Mathf.Clamp((int)(pos.Z / CELL_SIZE), 0, GRID_DIM_Z - 1);
            return cz * GRID_DIM_X + cx;
        }

        private (int cx, int cz) CellCoords(Vector3 pos)
        {
            return (
                Mathf.Clamp((int)(pos.X / CELL_SIZE), 0, GRID_DIM_X - 1),
                Mathf.Clamp((int)(pos.Z / CELL_SIZE), 0, GRID_DIM_Z - 1)
            );
        }

        private List<Node3D>[] EnsureGrid(string entityType)
        {
            long frame = (long)Engine.GetProcessFrames();

            // Allocate the grid the first time this type is seen.
            if (!_typeGrids.ContainsKey(entityType))
            {
                var grid = new List<Node3D>[GRID_TOTAL];
                for (int i = 0; i < GRID_TOTAL; i++)
                    grid[i] = new List<Node3D>();
                _typeGrids[entityType] = grid;
                _typeFrame[entityType] = -1;
            }

            if (_typeFrame[entityType] == frame)
                return _typeGrids[entityType];

            // Rebuild: clear every cell in-place (no new lists), then re-bucket.
            _typeFrame[entityType] = frame;
            var g = _typeGrids[entityType];
            for (int i = 0; i < GRID_TOTAL; i++)
                g[i].Clear();

            if (_entities.TryGetValue(entityType, out var entities))
            {
                foreach (var entity in entities)
                {
                    if (!IsInstanceValid(entity) || !entity.IsInsideTree())
                        continue;
                    g[CellIndexFromPos(entity.GlobalPosition)].Add(entity);
                }
            }

            return g;
        }

        // =============================================================================
        // Spatial Queries
        // =============================================================================

        /// <summary>
        /// Find the nearest entity of a given type within maxRange.
        /// </summary>
        public Node3D GetNearest(Vector3 position, string entityType, float maxRange = float.PositiveInfinity)
        {
            var grid = EnsureGrid(entityType);
            var (centerX, centerZ) = CellCoords(position);

            Node3D nearest = null;
            float nearestDistSq = float.PositiveInfinity;
            float rangeSq = float.IsPositiveInfinity(maxRange) ? float.PositiveInfinity : maxRange * maxRange;

            int cellRange = float.IsPositiveInfinity(maxRange)
                ? Math.Max(GRID_DIM_X, GRID_DIM_Z)
                : (int)MathF.Ceiling(maxRange / CELL_SIZE) + 1;

            int minCx = Math.Max(0, centerX - cellRange);
            int maxCx = Math.Min(GRID_DIM_X - 1, centerX + cellRange);
            int minCz = Math.Max(0, centerZ - cellRange);
            int maxCz = Math.Min(GRID_DIM_Z - 1, centerZ + cellRange);

            for (int gz = minCz; gz <= maxCz; gz++)
            {
                int row = gz * GRID_DIM_X;
                for (int gx = minCx; gx <= maxCx; gx++)
                {
                    var bucket = grid[row + gx];
                    if (bucket.Count == 0) continue;

                    foreach (var entity in bucket)
                    {
                        float distSq = position.DistanceSquaredTo(entity.GlobalPosition);
                        if (distSq < nearestDistSq && distSq <= rangeSq)
                        {
                            nearestDistSq = distSq;
                            nearest = entity;
                        }
                    }
                }
            }

            return nearest;
        }

        /// <summary>
        /// Get all entities of a given type within range.
        /// </summary>
        public List<Node3D> GetInRange(Vector3 position, string entityType, float maxRange)
        {
            var grid = EnsureGrid(entityType);
            var (centerX, centerZ) = CellCoords(position);
            int cellRange = (int)MathF.Ceiling(maxRange / CELL_SIZE) + 1;
            float rangeSq = maxRange * maxRange;
            var result = new List<Node3D>();

            int minCx = Math.Max(0, centerX - cellRange);
            int maxCx = Math.Min(GRID_DIM_X - 1, centerX + cellRange);
            int minCz = Math.Max(0, centerZ - cellRange);
            int maxCz = Math.Min(GRID_DIM_Z - 1, centerZ + cellRange);

            for (int gz = minCz; gz <= maxCz; gz++)
            {
                int row = gz * GRID_DIM_X;
                for (int gx = minCx; gx <= maxCx; gx++)
                {
                    var bucket = grid[row + gx];
                    if (bucket.Count == 0) continue;

                    foreach (var entity in bucket)
                    {
                        if (position.DistanceSquaredTo(entity.GlobalPosition) <= rangeSq)
                            result.Add(entity);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Find the nearest entity matching a filter predicate.
        /// </summary>
        public Node3D GetNearestWithFilter(Vector3 position, string entityType, float maxRange, Func<Node3D, bool> filter)
        {
            var grid = EnsureGrid(entityType);
            var (centerX, centerZ) = CellCoords(position);

            Node3D nearest = null;
            float nearestDistSq = float.PositiveInfinity;
            float rangeSq = float.IsPositiveInfinity(maxRange) ? float.PositiveInfinity : maxRange * maxRange;

            int cellRange = float.IsPositiveInfinity(maxRange)
                ? Math.Max(GRID_DIM_X, GRID_DIM_Z)
                : (int)MathF.Ceiling(maxRange / CELL_SIZE) + 1;

            int minCx = Math.Max(0, centerX - cellRange);
            int maxCx = Math.Min(GRID_DIM_X - 1, centerX + cellRange);
            int minCz = Math.Max(0, centerZ - cellRange);
            int maxCz = Math.Min(GRID_DIM_Z - 1, centerZ + cellRange);

            for (int gz = minCz; gz <= maxCz; gz++)
            {
                int row = gz * GRID_DIM_X;
                for (int gx = minCx; gx <= maxCx; gx++)
                {
                    var bucket = grid[row + gx];
                    if (bucket.Count == 0) continue;

                    foreach (var entity in bucket)
                    {
                        if (!filter(entity)) continue;
                        float distSq = position.DistanceSquaredTo(entity.GlobalPosition);
                        if (distSq < nearestDistSq && distSq <= rangeSq)
                        {
                            nearestDistSq = distSq;
                            nearest = entity;
                        }
                    }
                }
            }

            return nearest;
        }

        /// <summary>
        /// Find the nearest entity across multiple entity types.
        /// </summary>
        public Node3D GetNearestMulti(Vector3 position, string[] entityTypes, float maxRange = float.PositiveInfinity)
        {
            Node3D best = null;
            float bestDistSq = float.PositiveInfinity;

            foreach (var etype in entityTypes)
            {
                var candidate = GetNearest(position, etype, maxRange);
                if (candidate != null)
                {
                    float distSq = position.DistanceSquaredTo(candidate.GlobalPosition);
                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        best = candidate;
                    }
                }
            }

            return best;
        }

        // =============================================================================
        // Utility
        // =============================================================================

        public int GetTotalCount()
        {
            int total = 0;
            foreach (var list in _entities.Values)
                total += list.Count;
            return total;
        }

        public void ClearAll()
        {
            _entities.Clear();
            _typeGrids.Clear();
            _typeFrame.Clear();
        }

        public void ClearType(string entityType)
        {
            if (_entities.ContainsKey(entityType))
                _entities[entityType].Clear();
            _typeGrids.Remove(entityType);
            _typeFrame.Remove(entityType);
        }

        /// <summary>
        /// Remove invalid (freed) entities from all lists.
        /// Call periodically if entities are freed without unregistering.
        /// </summary>
        public void CleanupInvalid()
        {
            foreach (var kvp in _entities)
            {
                kvp.Value.RemoveAll(e => !IsInstanceValid(e) || !e.IsInsideTree());
            }
        }

        public override void _ExitTree()
        {
            ServiceLocator.Unregister<EntityRegistry>();
        }
    }
}
