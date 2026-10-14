using System.Linq;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// "bitAssist" in a balance bot's config: BIT played the way a player plays it beside a tower
    /// build. Each wave it stands on the route a few cells out from the Spire, on the cell with the
    /// most towers in Overclock's reach, fires by itself, and spends its own Materials (which
    /// regenerate) on its abilities: Repair Pulse when the Spire or the towers around BIT are hurt,
    /// Overclock on cooldown while enemies are under fire from enough towers in its reach, Shock Blast on a crowd. Without it the
    /// tower bots leave BIT idle by the Spire, so the Obelisk's kit (cheaper, stronger abilities,
    /// a tougher BIT) and the power types' ability effects (Chaos throws, Power's longer Overclock,
    /// Environment's fortify) were never measured.
    /// </summary>
    public static class BitPilot
    {
        /// <summary>About a tower's reach: an enemy this far past Overclock's edge is under boosted fire.</summary>
        private const float FightRange = 14f;
        private static float _clock;
        private static Vector3? _spot;
        private static int _shock, _repair, _overclock, _wave;

        /// <summary>The build phase before a wave: pick a new spot and log the last wave's casts.</summary>
        public static void NewWave(VineGrid grid, int wave)
        {
            if (_wave > 0 && (_shock + _repair + _overclock) > 0)
                GD.Print($"[BitPilot] W{_wave}: shock {_shock}, repair {_repair}, overclock {_overclock}");
            _shock = _repair = _overclock = 0;
            _wave = wave + 1;
            _spot = PickSpot(grid);
        }

        public static void Tick(VineGrid grid, float dt)
        {
            if ((_clock -= dt) > 0f) return;
            _clock = 0.5f;
            if (grid == null || !ServiceLocator.TryGet<VinePlayer>(out var player)) return;
            if (!player.IsAlive || player.IsDown || player.IsDocked) return;
            var tree = player.GetTree();
            if (tree == null) return;

            // Walk back to the spot (after a respawn or a knock)
            _spot ??= PickSpot(grid);
            if (_spot is Vector3 s && player.GlobalPosition.DistanceTo(s) > 3f) player.GlobalPosition = s;

            var abilities = player.GetAbilities();
            if (abilities == null || abilities.Length < 3) return;
            var at = player.GlobalPosition;
            var spire = grid.Harvester;

            int near = 0, inFight = 0;
            foreach (var e in Roster.Enemies(tree))
            {
                if (e == null || !e.IsAlive) continue;
                float d = e.GlobalPosition.DistanceTo(at);
                if (d <= Constants.ABILITY_SHOCK_RADIUS) near++;
                // In the fight: within shooting range of a tower Overclock would boost
                if (d <= Constants.ABILITY_OVERCLOCK_RADIUS * PowerType.OverclockReachMult + FightRange) inFight++;
            }

            int hurt = 0, boostable = 0;
            float reach = Constants.ABILITY_OVERCLOCK_RADIUS * PowerType.OverclockReachMult;
            foreach (var n in Roster.Nodes(tree))
            {
                if (n == null || n.IsDestroyed || n.Data == null) continue;
                float d = n.GlobalPosition.DistanceTo(at);
                if (d <= Constants.ABILITY_REPAIR_RADIUS && n.NodeMaxHealth > 0 && n.NodeCurrentHealth < n.NodeMaxHealth * 0.7f) hurt++;
                if (d <= reach && Boostable(n)) boostable++;
            }

            // Repair first: the Spire is the run
            float pulse = Constants.ABILITY_REPAIR_SPIRE * player.AbilityPower;
            bool spireHurt = spire != null && spire.CurrentHP < spire.MaxHP - pulse * 0.8f;
            if ((spireHurt || hurt >= 3) && Ready(player, abilities[1]))
            {
                player.UseAbility(1);
                _repair++;
                return;
            }
            // Keep enough back for a repair when the Spire is hurt
            float keep = spireHurt ? abilities[1].MaterialsCost : 0f;
            if (boostable >= 4 && inFight >= 1 && Ready(player, abilities[2], keep))
            {
                player.UseAbility(2);
                _overclock++;
                return;
            }
            // Shock Blast is cheap and quick: only with Overclock's cost to spare
            if (near >= 3 && Ready(player, abilities[0], keep + abilities[2].MaterialsCost))
            {
                player.UseAbility(0);
                _shock++;
            }
        }

        private static bool Ready(VinePlayer player, VinePlayerAbility a, float keep = 0f)
            => a != null && a.CurrentCooldown <= 0f && (MetaRun.BloodRites
                ? player.CurrentHP > a.MaterialsCost + 1f + player.MaxHP * 0.5f
                : player.CurrentMaterials >= a.MaterialsCost + keep);

        /// <summary>What Overclock boosts: a tower that fires (not walls, relays or drills).</summary>
        private static bool Boostable(VineNode n)
            => n.Data.Category == VineNodeCategory.Effect
               && n.Data.Type is not (VineNodeType.BarrierWall or VineNodeType.BuffEmitter or VineNodeType.SalvageDrill);

        /// <summary>The route cell 2 to 8 cells out from the Spire with the most towers in Overclock's reach.</summary>
        private static Vector3? PickSpot(VineGrid grid)
        {
            if (grid == null || !ServiceLocator.TryGet<VinePathfinder>(out var pf)) return null;
            var exit = grid.ExitPoint;
            var tree = grid.GetTree();
            var towers = tree == null ? new System.Collections.Generic.List<Vector3>()
                : Roster.Nodes(tree).Where(n => n != null && !n.IsDestroyed && n.Data != null && Boostable(n)).Select(n => n.GlobalPosition).ToList();
            float reach = Constants.ABILITY_OVERCLOCK_RADIUS * PowerType.OverclockReachMult;
            Vector2I? best = null;
            float bestScore = -1f;
            foreach (var reg in grid.ActiveEntryRegions)
            {
                var path = pf.GetCachedPath(reg.Center);
                if (path == null) continue;
                foreach (var c in path)
                {
                    float d = (c - exit).Length();
                    if (d < 2f || d > 8f) continue;
                    var w = grid.GridToWorld(c);
                    float score = towers.Count(t => new Vector2(t.X - w.X, t.Z - w.Z).Length() <= reach) + 0.1f / (1f + d);
                    if (score > bestScore) { bestScore = score; best = c; }
                }
            }
            if (best == null) return null;
            var p = grid.GridToWorld(best.Value);
            return new Vector3(p.X, grid.GetWorldHeight(p.X, p.Z), p.Z);
        }
    }
}
