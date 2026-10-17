using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// The Obelisk's Barricade (the Barrier Wall on a canyon map) stands on the floor and never
    /// changes a route: a walker whose way runs through its cell stops at it and hits it until it
    /// breaks (Brutes hardest). Flyers and Ghosts go on. Holding at one isn't being stuck.
    /// </summary>
    public partial class VineEnemy
    {
        private bool _atBarricade;
        private float _barricadeHit;

        /// <summary>Standing at a barricade, breaking it (tests).</summary>
        public bool AtBarricade => _atBarricade;

        private bool HoldAtBarricade(float dt)
        {
            _atBarricade = false;
            var list = VineNode.FloorBarricades;
            if (list.Count == 0 || !FollowsMaze || IsRaider) return false;
            VineNode bar = null;
            float reach = _bodyRadius + Constants.VINE_CELL_SIZE * 0.6f, best = reach * reach;
            var me = GlobalPosition;
            for (int i = 0; i < list.Count; i++)
            {
                var n = list[i];
                if (!IsInstanceValid(n) || n.IsDestroyed) continue;
                var p = n.GlobalPosition;
                float dx = p.X - me.X, dz = p.Z - me.Z, d2 = dx * dx + dz * dz;
                if (d2 < best && OnMyWay(n.GridPosition)) { best = d2; bar = n; }
            }
            if (bar == null) return false;
            _atBarricade = true;
            _stuckCheckTimer = 0f;
            Face(bar.GlobalPosition, dt);
            if ((_barricadeHit -= dt) <= 0f)
            {
                _barricadeHit = 1f;
                float dmg = Mathf.Max(4f, _attackDamage) * 1.5f * (Faction == VineEnemyFaction.Brute ? 2f : 1f) * DamageMultiplier * StatusDamageMult;
                bar.TakeDamage(dmg, TowerLoss.Shot);
                PlayAttack();
            }
            return true;
        }

        /// <summary>The barricade's cell is where this walker is going next (or where it stands).</summary>
        private bool OnMyWay(Vector2I cell)
        {
            if (_grid.WorldToGrid(GlobalPosition) == cell) return true;
            if (_path == null) return true;
            for (int i = _pathIndex; i < _path.Count && i < _pathIndex + 3; i++)
                if (_path[i] == cell) return true;
            return false;
        }
    }
}
