using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Who can see whom on a canyon map (playtest 2026-10: "enemies and friendlies can't see each
    /// other through walls"). Sight is judged between fixed points: a tower's eye (its muzzle's
    /// height over the tower), an enemy's body centre, BIT's chest, an Ascendant's shoulders. The
    /// same two points are used both ways, so a tower that can't see an enemy can't be seen by it
    /// either, and shots aim at those points, so a shot that was allowed never cuts through rock.
    ///
    /// Lobbed fire (the Mortar Pit, the Slag Chute, siege shells, strikes, the Spire's gunner
    /// mortar) and fields (the Signal Jammer, Gravity Well, Tar Sprayer and Pneumatic Ram) don't
    /// need it. Off canyon maps everything sees everything, as before.
    /// </summary>
    public static class Sight
    {
        private static VineGrid _grid;

        private static VineGrid Grid
        {
            get
            {
                if (_grid != null && GodotObject.IsInstanceValid(_grid) && _grid.IsInsideTree()) return _grid;
                _grid = ServiceLocator.TryGet<VineGrid>(out var g) ? g : null;
                return _grid;
            }
        }

        /// <summary>True when the map has walls that can hide anything (a canyon map).</summary>
        public static bool Matters => Grid?.IsCanyon ?? false;

        /// <summary>Nothing of the ground between the two points.</summary>
        public static bool Clear(Vector3 a, Vector3 b)
        {
            var g = Grid;
            return g == null || g.HasLineOfSight(a, b);
        }

        /// <summary>Where a shot from <paramref name="a"/> toward <paramref name="b"/> meets rock, or b.</summary>
        public static Vector3 End(Vector3 a, Vector3 b)
        {
            var g = Grid;
            return g == null ? b : g.SightEnd(a, b);
        }

        /// <summary>The point that sees and is seen for <paramref name="n"/> (and where shots at it aim).</summary>
        public static Vector3 PointOf(Node3D n) => n switch
        {
            VineNode t => t.EyePoint,
            VineEnemy e => e.BodyCentre,
            VinePlayer p => p.ChestPoint,
            Ascendant a => a.SightPoint,
            null => Vector3.Zero,
            _ => n.GlobalPosition + Vector3.Up * 0.6f,
        };

        /// <summary>A typical tower's eye over its origin (muzzles sit 0.5 to 1.2 up), for cells with no tower yet.</summary>
        public const float TOWER_EYE = 0.8f;
        /// <summary>A walker's body centre over the floor.</summary>
        public const float WALKER_UP = 0.6f;

        private static readonly System.Collections.Generic.Dictionary<VineNodeType, float> _eyes = new();

        /// <summary>A built tower measured its eye (its muzzle over its origin): the build ghost uses it for the next.</summary>
        public static void RememberEye(VineNodeType t, float eyeUp) => _eyes[t] = eyeUp;

        /// <summary>How high a tower of this type sees from, once one has been built (else a typical turret's).</summary>
        public static float EyeUpFor(VineNodeType t) => _eyes.TryGetValue(t, out var e) ? e : TOWER_EYE;

        /// <summary>Lobbed fire and fields don't need to see; every other gun (and the Beacon) does.</summary>
        public static bool TypeNeedsSight(VineNodeType t)
        {
            if (VineNode.TypeIgnoresSight(t)) return false;
            var d = VineNodeRegistry.Get(t);
            return d != null && d.Category == VineNodeCategory.Effect && d.AutoFires && d.Range > 0f
                && t is not (VineNodeType.BuffEmitter or VineNodeType.SalvageDrill or VineNodeType.BarrierWall);
        }

        /// <summary>Where a tower built on <paramref name="cell"/> would see from.</summary>
        public static Vector3 TowerEyeAt(VineGrid g, Vector2I cell, float eyeUp = TOWER_EYE)
        {
            var p = g.GridToWorld(cell);
            p.Y = g.NodeBaseHeight(cell) + Constants.NODE_ORIGIN_HEIGHT + eyeUp;
            return p;
        }

        /// <summary>A walker's body centre standing in the middle of <paramref name="cell"/>.</summary>
        public static Vector3 WalkerAt(VineGrid g, Vector2I cell)
        {
            var p = g.GridToWorld(cell);
            p.Y = g.GetWorldHeight(p.X, p.Z) + WALKER_UP;
            return p;
        }

        /// <summary>Would a tower on <paramref name="tower"/> see a walker in the middle of <paramref name="floor"/>.</summary>
        public static bool CellSeesCell(VineGrid g, Vector2I tower, Vector2I floor) =>
            g == null || g.HasLineOfSight(TowerEyeAt(g, tower), WalkerAt(g, floor));

        /// <summary>Can <paramref name="a"/> and <paramref name="b"/> see each other.</summary>
        public static bool Between(Node3D a, Node3D b)
        {
            if (!Matters) return true;
            return a != null && b != null && Clear(PointOf(a), PointOf(b));
        }
    }
}
