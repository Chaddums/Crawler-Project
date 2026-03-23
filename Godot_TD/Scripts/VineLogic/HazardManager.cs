using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Ticks damage on enemies and towers standing on or near hazard cells.
    /// Manages destructible walls (Brute damage). Tracks resource node capture.
    /// Added to VineBattleScene as a child node.
    /// </summary>
    public partial class HazardManager : Node
    {
        private VineGrid _grid;
        private float _tickTimer;

        // Cache hazard cell positions at map load to avoid scanning every tick
        private readonly List<Vector2I> _hazardCells = new();
        private readonly List<Vector2I> _destructibleWalls = new();

        public override void _Ready()
        {
            if (!ServiceLocator.TryGet<VineGrid>(out _grid))
            {
                GD.PrintErr("[HazardManager] VineGrid not registered in ServiceLocator");
                return;
            }
            CacheTerrainCells();

            // Re-cache when terrain mutates
            GameEvents.OnTerrainMutated += OnTerrainMutated;
            GameEvents.OnDestructibleWallBroken += OnWallBroken;
        }

        public override void _ExitTree()
        {
            GameEvents.OnTerrainMutated -= OnTerrainMutated;
            GameEvents.OnDestructibleWallBroken -= OnWallBroken;
        }

        private void CacheTerrainCells()
        {
            _hazardCells.Clear();
            _destructibleWalls.Clear();

            for (int x = 0; x < _grid.Width; x++)
            {
                for (int y = 0; y < _grid.Height; y++)
                {
                    var cell = _grid.GetCell(x, y);
                    var pos = new Vector2I(x, y);
                    if (cell == VineCellType.Hazard)
                        _hazardCells.Add(pos);
                    else if (cell == VineCellType.DestructibleWall)
                        _destructibleWalls.Add(pos);
                }
            }

            GD.Print($"[HazardManager] Cached {_hazardCells.Count} hazard cells, {_destructibleWalls.Count} destructible walls");
        }

        public override void _Process(double delta)
        {
            if (_grid == null) return;

            _tickTimer -= (float)delta;
            if (_tickTimer > 0) return;
            _tickTimer = Constants.HAZARD_TICK_INTERVAL;

            TickHazardDamage();
            TickResourceNodeCapture();
        }

        private void TickHazardDamage()
        {
            if (_hazardCells.Count == 0) return;

            var enemies = GetTree().GetNodesInGroup(Constants.GROUP_VINE_ENEMY);
            var cellSize = Constants.VINE_CELL_SIZE;

            foreach (var hazardPos in _hazardCells)
            {
                var worldPos = _grid.GridToWorld(hazardPos);
                float dps = GetHazardDPS(_grid.GetHazardType(hazardPos));

                // Damage enemies on this cell
                foreach (var enemy in enemies)
                {
                    if (enemy is not VineEnemy ve || !ve.IsAlive) continue;
                    var enemyGrid = _grid.WorldToGrid(ve.GlobalPosition);
                    if (enemyGrid != hazardPos) continue;

                    ve.TakeDamage(dps * Constants.HAZARD_TICK_INTERVAL);

                    // Electric hazards briefly stun
                    if (_grid.GetHazardType(hazardPos) == HazardType.Electric)
                        ve.ApplySlow(Constants.HAZARD_ELECTRIC_STUN, 1f); // Full slow = stun
                }

                // Chip damage to towers on adjacent cells
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        if (Mathf.Abs(dx) + Mathf.Abs(dy) > (int)Constants.HAZARD_TOWER_CHIP_RANGE) continue;
                        var adj = new Vector2I(hazardPos.X + dx, hazardPos.Y + dy);
                        var node = _grid.GetNode(adj);
                        if (node != null && !node.IsDestroyed)
                            node.TakeDamage(Constants.HAZARD_TOWER_CHIP_DPS * Constants.HAZARD_TICK_INTERVAL);
                    }
                }
            }
        }

        private void TickResourceNodeCapture()
        {
            var resourceNodes = _grid.GetResourceNodes();
            foreach (var pos in resourceNodes)
            {
                bool wasCaptured = _grid.IsResourceNodeCaptured(pos);
                bool hasTower = false;

                // Check 4-directional adjacency for a tower
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (Mathf.Abs(dx) + Mathf.Abs(dy) != 1) continue;
                        var adj = new Vector2I(pos.X + dx, pos.Y + dy);
                        if (_grid.GetNode(adj) != null)
                        {
                            hasTower = true;
                            break;
                        }
                    }
                    if (hasTower) break;
                }

                if (hasTower && !wasCaptured)
                {
                    _grid.SetResourceNodeCaptured(pos, true);
                    GameEvents.OnResourceNodeCaptured?.Invoke(pos);
                    GD.Print($"[HazardManager] Resource node captured at ({pos.X},{pos.Y})");
                }
                else if (!hasTower && wasCaptured)
                {
                    _grid.SetResourceNodeCaptured(pos, false);
                }
            }
        }

        private static float GetHazardDPS(HazardType type) => type switch
        {
            HazardType.Acid => Constants.HAZARD_ACID_DPS,
            HazardType.Lava => Constants.HAZARD_LAVA_DPS,
            HazardType.Electric => Constants.HAZARD_ELECTRIC_DPS,
            _ => Constants.HAZARD_ACID_DPS
        };

        private void OnTerrainMutated(Vector2I pos, VineCellType newType)
        {
            CacheTerrainCells();
        }

        private void OnWallBroken(Vector2I pos)
        {
            _destructibleWalls.Remove(pos);
        }
    }
}
