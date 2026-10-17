using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Canyon maps (Data/Canyons/{layout}.json): enemies walk canyon floors cut along set routes
    /// from each entry to the Spire, and towers stand anywhere on the high walls above them. The
    /// floor can't be built on, so there is no mazing: where to build is the cliff edges.
    ///
    /// Each route is a list of cell points joined by straight runs; a brush <c>width</c> cells
    /// square (its top-left corner on the point) cuts the floor along it. The first point is on
    /// the map's edge: the brush's cells there are the entry. A plaza of <c>plaza</c> cells round
    /// the Spire joins the routes. Every other cell is wall: the ones touching the floor (sides
    /// and corners) are its face (Cliff: nothing builds or walks there), the rest its top (Ledge:
    /// build here). The heightmap puts every corner that touches the floor at the floor and the
    /// rest <c>wallHeight</c> up, so the floors are flat and each face drops one cell.
    ///
    /// A route with <c>opensAfter</c> is shut by a shield wall that breaks after that many
    /// seconds. Hazards go on the floor (enemies walk through them); ore goes on the walls, and
    /// takes a Salvage Drill (raiders climb the wall to it).
    /// </summary>
    public static class CanyonMaps
    {
        public class Spec
        {
            public int[] Spire { get; set; }
            public int Plaza { get; set; } = 2;
            public int Width { get; set; } = 2;
            /// <summary>
            /// How high the walls stand over the floor. 3.5 hid a quarter of the walkers from the
            /// play camera and made towers on the tops fire steeply down past the lip (playtest
            /// 2026-10: "lower wall heights, it makes some of the firing look weird"); at 2.0 the
            /// play camera sees 93 to 97% of them (the sightlines suite measures it, and what a
            /// tower on the lip and one cell back sees of the floor).
            /// </summary>
            public float WallHeight { get; set; } = 2.0f;
            /// <summary>How much the wall tops rise and fall (gentle noise).</summary>
            public float TopNoise { get; set; } = 0.26f;
            public int Seed { get; set; }
            public List<Route> Routes { get; set; } = new();
            public List<Hazard> Hazards { get; set; } = new();
            public List<int[]> Ore { get; set; } = new();
            public int Props { get; set; } = 4;
        }

        public class Route
        {
            public string Dir { get; set; } = "West";
            public int Width { get; set; }
            /// <summary>Seconds until its shield wall breaks (0: open from the start).</summary>
            public float OpensAfter { get; set; }
            public List<int[]> Points { get; set; } = new();
        }

        public class Hazard
        {
            public string Type { get; set; } = "Acid";
            public List<int[]> Cells { get; set; } = new();
        }

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        public static string PathFor(string layout) => $"res://Data/Canyons/{layout}.json";

        public static bool Exists(string layout) => !string.IsNullOrEmpty(layout) && Godot.FileAccess.FileExists(PathFor(layout));

        public static Spec Load(string layout)
        {
            if (!Exists(layout)) return null;
            using var f = Godot.FileAccess.Open(PathFor(layout), Godot.FileAccess.ModeFlags.Read);
            Spec spec;
            try { spec = JsonSerializer.Deserialize<Spec>(f.GetAsText(), _json); }
            catch (JsonException e) { GD.PushError($"[Canyon] {layout}: {e.Message}"); return null; }
            // Measuring other heights (the sightlines suite): CANYON_WALL=2.0 builds every map at that height
            if (spec != null && float.TryParse(System.Environment.GetEnvironmentVariable("CANYON_WALL"),
                    System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var wall) && wall > 0.5f)
            {
                spec.TopNoise *= wall / spec.WallHeight;
                spec.WallHeight = wall;
            }
            return spec;
        }

        /// <summary>Build <paramref name="layout"/> as a canyon map; false when it has no canyon spec.</summary>
        public static bool TryBuild(VineGrid grid, string layout, out List<ShieldWallConfig> shieldWalls)
        {
            shieldWalls = null;
            var spec = Load(layout);
            if (spec == null || spec.Spire == null || spec.Routes.Count == 0) return false;
            Build(grid, spec, layout, out shieldWalls);
            return true;
        }

        /// <summary>Which cells are canyon floor, and where each route's entry is.</summary>
        public static bool[,] Carve(Spec spec, int w, int h, List<(Route route, List<Vector2I> cells)> entries = null)
        {
            var floor = new bool[w, h];
            void Brush(int x, int y, int size)
            {
                for (int dx = 0; dx < size; dx++)
                    for (int dy = 0; dy < size; dy++)
                    {
                        int X = x + dx, Y = y + dy;
                        if (X >= 0 && X < w && Y >= 0 && Y < h) floor[X, Y] = true;
                    }
            }
            foreach (var r in spec.Routes)
            {
                int size = r.Width > 0 ? r.Width : spec.Width;
                for (int i = 0; i + 1 < r.Points.Count; i++)
                {
                    var a = r.Points[i];
                    var b = r.Points[i + 1];
                    int n = Math.Max(Math.Abs(b[0] - a[0]), Math.Abs(b[1] - a[1]));
                    for (int k = 0; k <= n; k++)
                    {
                        float t = n == 0 ? 0f : k / (float)n;
                        Brush(Mathf.RoundToInt(a[0] + (b[0] - a[0]) * t), Mathf.RoundToInt(a[1] + (b[1] - a[1]) * t), size);
                    }
                }
                if (entries != null && r.Points.Count > 0)
                {
                    var p = r.Points[0];
                    var cells = new List<Vector2I>();
                    for (int dx = 0; dx < size; dx++)
                        for (int dy = 0; dy < size; dy++)
                        {
                            int X = p[0] + dx, Y = p[1] + dy;
                            bool edge = r.Dir switch
                            {
                                "West" => X == 0,
                                "East" => X == w - 1,
                                "North" => Y == 0,
                                _ => Y == h - 1,
                            };
                            if (edge && X >= 0 && X < w && Y >= 0 && Y < h) cells.Add(new Vector2I(X, Y));
                        }
                    entries.Add((r, cells));
                }
            }
            int sx = spec.Spire[0], sy = spec.Spire[1];
            for (int x = sx - spec.Plaza; x <= sx + spec.Plaza; x++)
                for (int y = sy - spec.Plaza; y <= sy + spec.Plaza; y++)
                    if (x >= 0 && x < w && y >= 0 && y < h) floor[x, y] = true;
            return floor;
        }

        /// <summary>Is a wall cell touching the floor (its sides or corners)? That is the wall's face.</summary>
        public static bool TouchesFloor(bool[,] floor, int x, int y)
        {
            int w = floor.GetLength(0), h = floor.GetLength(1);
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    int X = x + dx, Y = y + dy;
                    if (X >= 0 && X < w && Y >= 0 && Y < h && floor[X, Y]) return true;
                }
            return false;
        }

        private static void Build(VineGrid grid, Spec spec, string layout, out List<ShieldWallConfig> shieldWalls)
        {
            int w = grid.Width, h = grid.Height;
            var entries = new List<(Route route, List<Vector2I> cells)>();
            var floor = Carve(spec, w, h, entries);
            grid.MarkCanyon(spec.WallHeight);

            // Heights: a corner touching the floor is floor, every other corner is wall top.
            // Fixed noise per map, so a canyon looks the same every run.
            int seed = spec.Seed != 0 ? spec.Seed : StableHash(layout);
            var top = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.09f, Seed = seed };
            var low = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.2f, Seed = seed + 7 };
            for (int cx = 0; cx <= w; cx++)
                for (int cy = 0; cy <= h; cy++)
                {
                    bool onFloor = false;
                    for (int dx = -1; dx <= 0 && !onFloor; dx++)
                        for (int dy = -1; dy <= 0 && !onFloor; dy++)
                        {
                            int X = cx + dx, Y = cy + dy;
                            if (X >= 0 && X < w && Y >= 0 && Y < h && floor[X, Y]) onFloor = true;
                        }
                    grid.SetCornerHeight(cx, cy, onFloor
                        ? low.GetNoise2D(cx, cy) * 0.06f
                        : spec.WallHeight + top.GetNoise2D(cx, cy) * spec.TopNoise);
                }

            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    if (floor[x, y]) continue;
                    if (TouchesFloor(floor, x, y)) grid.SetCliff(x, y);
                    else grid.SetLedge(x, y);
                }

            // Entries in the routes' order: shield walls name them by index
            shieldWalls = new List<ShieldWallConfig>();
            float hp = Constants.SHIELD_WALL_BASE_HP;
            foreach (var (route, cells) in entries)
            {
                if (cells.Count == 0)
                {
                    GD.PushError($"[Canyon] {layout}: the {route.Dir} route doesn't start on the {route.Dir} edge");
                    continue;
                }
                int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
                foreach (var c in cells) { x0 = Math.Min(x0, c.X); y0 = Math.Min(y0, c.Y); x1 = Math.Max(x1, c.X); y1 = Math.Max(y1, c.Y); }
                grid.SetEntryRegion(x0, y0, x1, y1);
                var region = grid.EntryRegions[grid.EntryRegions.Count - 1];
                region.Direction = Direction(route.Dir);
                region.Active = route.OpensAfter <= 0f;
                if (route.OpensAfter > 0f)
                {
                    shieldWalls.Add(new ShieldWallConfig
                    {
                        Direction = region.Direction,
                        HP = hp,
                        EntryRegionIndex = region.Index,
                        TriggerType = ShieldWallTriggerType.TimeMilestone,
                        TriggerTime = route.OpensAfter,
                    });
                    hp *= 1.5f;
                }
            }
            grid.SetExit(spec.Spire[0], spec.Spire[1]);

            foreach (var hz in spec.Hazards)
            {
                var type = hz.Type switch { "Lava" => HazardType.Lava, "Electric" => HazardType.Electric, _ => HazardType.Acid };
                foreach (var c in hz.Cells)
                {
                    if (grid.InBounds(c[0], c[1]) && floor[c[0], c[1]] && grid.GetCell(c[0], c[1]) == VineCellType.Empty)
                        grid.SetHazardCell(c[0], c[1], type);
                    else GD.PushWarning($"[Canyon] {layout}: hazard at ({c[0]},{c[1]}) is off the floor");
                }
            }
            foreach (var o in spec.Ore)
            {
                if (grid.InBounds(o[0], o[1]) && grid.GetCell(o[0], o[1]) == VineCellType.Ledge)
                    grid.SetResourceNodeCell(o[0], o[1]);
                else GD.PushWarning($"[Canyon] {layout}: ore at ({o[0]},{o[1]}) is not on a wall top");
            }

            ScatterLedgeProps(grid, floor, spec.Props, seed);
            VineMapLayouts.BuildEntryExitVisuals(grid);
        }

        /// <summary>A few props on the wall tops well back from the edges, where they take no tower's spot.</summary>
        private static void ScatterLedgeProps(VineGrid grid, bool[,] floor, int count, int seed)
        {
            if (count <= 0) return;
            var rng = new RandomNumberGenerator { Seed = (ulong)(uint)seed };
            int w = grid.Width, h = grid.Height, placed = 0;
            for (int attempt = 0; attempt < count * 40 && placed < count; attempt++)
            {
                int x = rng.RandiRange(1, w - 2), y = rng.RandiRange(1, h - 2);
                if (grid.GetCell(x, y) != VineCellType.Ledge || DistanceToFloor(floor, x, y, 4) < 4) continue;
                grid.SetPropOnLedge(x, y, VineMapLayouts.RandomPropType(rng));
                placed++;
            }
        }

        /// <summary>Chebyshev distance from a cell to the nearest floor, up to <paramref name="cap"/>.</summary>
        public static int DistanceToFloor(bool[,] floor, int x, int y, int cap)
        {
            int w = floor.GetLength(0), h = floor.GetLength(1);
            for (int r = 0; r < cap; r++)
                for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        int X = x + dx, Y = y + dy;
                        if (X >= 0 && X < w && Y >= 0 && Y < h && floor[X, Y]) return r;
                    }
            return cap;
        }

        private static CardinalDirection Direction(string dir) => dir switch
        {
            "North" => CardinalDirection.North,
            "East" => CardinalDirection.East,
            "South" => CardinalDirection.South,
            _ => CardinalDirection.West,
        };

        private static int StableHash(string s)
        {
            unchecked
            {
                int hash = 23;
                foreach (char c in s ?? "") hash = hash * 31 + c;
                return hash & 0x7fffffff;
            }
        }
    }
}
