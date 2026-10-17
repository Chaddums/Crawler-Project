using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Cells tests build on. On a canyon map the nearest place to build beside a route is the
    /// wall top past the face, two or three cells out, so "the cell next to the route" never takes
    /// a tower there; and it has to be on the lip, where a tower sees the floor below.
    /// </summary>
    public static class TestCells
    {
        /// <summary>
        /// Where a Barricade (BarrierWall) goes: on a canyon map a floor cell (the one thing built
        /// there), the <paramref name="index"/>th of those well away from the Spire; elsewhere any
        /// free cell that leaves a route.
        /// </summary>
        public static Vector2I? WallSpot(VineGrid grid, int index = 0, float awayFromSpire = 10f)
        {
            if (grid == null) return null;
            ServiceLocator.TryGet<VinePathfinder>(out var pf);
            int n = 0;
            for (int y = 1; y < grid.Height - 1; y++)
                for (int x = 1; x < grid.Width - 1; x++)
                {
                    var c = new Vector2I(x, y);
                    if ((c - grid.ExitPoint).Length() < awayFromSpire / Constants.VINE_CELL_SIZE) continue;
                    bool ok = grid.IsCanyon ? grid.BarricadeSpot(c) && grid.GetNode(c) == null
                        : grid.CanPlace(c) && grid.GetNode(c) == null && (pf == null || !pf.WouldBlockAllPaths(c));
                    if (ok && n++ == index) return c;
                }
            return null;
        }

        /// <summary>Free buildable cells beside <paramref name="route"/>, nearest the route first, in route order.</summary>
        public static List<Vector2I> BesideRoute(VineGrid grid, IList<Vector2I> route, int count, int skip = 0)
        {
            var found = new List<Vector2I>();
            if (grid == null || route == null) return found;
            var onRoute = new HashSet<Vector2I>(route);
            int reach = grid.IsCanyon ? 3 : 1;
            for (int i = skip; i < route.Count && found.Count < count; i++)
                for (int r = 1; r <= reach && found.Count < count; r++)
                    foreach (var d in new[] { new Vector2I(r, 0), new Vector2I(-r, 0), new Vector2I(0, r), new Vector2I(0, -r) })
                    {
                        var c = route[i] + d;
                        if (found.Count >= count || onRoute.Contains(c) || found.Contains(c)) continue;
                        if (!grid.CanPlace(c) || grid.GetNode(c) != null) continue;
                        if (ServiceLocator.TryGet<VinePathfinder>(out var pf) && pf.WouldBlockAllPaths(c)) continue;
                        // A tower there has to see the route: one set back from the lip doesn't (Sight)
                        if (grid.IsCanyon && !Sight.CellSeesCell(grid, c, route[i])) continue;
                        found.Add(c);
                    }
            return found;
        }
    }
}
