using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Loads every registered map layout, validates structural integrity,
    /// pathability, terrain type placement, and expansion zone sanity.
    /// Runs headless — builds grids directly without loading battle scenes.
    /// </summary>
    public class MapValidationSuite : ITestSuite
    {
        public string SuiteName => "map-validation";

        private static readonly string[] AllMaps =
        {
            // Grid Prime
            "gateway", "circuit_lanes", "antenna_field", "grid_maze", "data_nexus",
            // Scrapyard
            "salvage_yard", "rust_pit", "foundry", "smelter",
            // Original layouts
            "conduit", "arena", "forge", "labyrinth", "crucible", "crossroads"
        };

        public async Task Run(TestContext ctx)
        {
            GD.Print("[MapValidation] Starting map validation suite...");

            foreach (var mapName in AllMaps)
            {
                GD.Print($"\n[MapValidation] ═══ Validating: {mapName} ═══");
                ValidateMap(ctx, mapName);
            }

            ValidateCrossMapUniqueness(ctx);
            ValidateTerritoryJsonMaps(ctx);

            GD.Print("\n[MapValidation] Suite complete.");
            await Task.CompletedTask;
        }

        private void ValidateMap(TestContext ctx, string mapName)
        {
            string prefix = $"map/{mapName}";

            // Build a grid directly — no scene loading, no CEF, works headless
            var grid = new VineGrid();
            grid.Width = Constants.VINE_MAP_WIDTH;
            grid.Height = Constants.VINE_MAP_HEIGHT;

            // Add to scene tree temporarily (needed for GetTree() calls inside BuildMap)
            var root = ctx.Tree.CurrentScene;
            root.AddChild(grid);

            try
            {
                // Initialize the grid's internal arrays by calling _Ready
                // VineGrid._Ready sets up _cells and _nodes arrays
                // Since we added it to the tree, _Ready was called automatically

                // Build the map
                VineMapLayouts.BuildMap(grid, mapName);

                // Run all validation checks
                ValidateStructure(ctx, grid, prefix);
                ValidateEntryExit(ctx, grid, prefix);
                ValidatePathability(ctx, grid, prefix);
                ValidateTerrainTypes(ctx, grid, prefix);
                ValidateTerrainVariety(ctx, grid, prefix);
                ValidatePlacementSpace(ctx, grid, prefix);
            }
            catch (Exception e)
            {
                ctx.Assert(false, $"{prefix}/no_crash", $"Map build crashed: {e.Message}");
                GD.PrintErr($"  [{prefix}] EXCEPTION: {e}");
            }
            finally
            {
                grid.QueueFree();
            }
        }

        private void ValidateStructure(TestContext ctx, VineGrid grid, string prefix)
        {
            ctx.Assert(grid.Width == Constants.VINE_MAP_WIDTH,
                $"{prefix}/width", $"Width={grid.Width}, expected {Constants.VINE_MAP_WIDTH}");
            ctx.Assert(grid.Height == Constants.VINE_MAP_HEIGHT,
                $"{prefix}/height", $"Height={grid.Height}, expected {Constants.VINE_MAP_HEIGHT}");

            var counts = new Dictionary<VineCellType, int>();
            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                {
                    var cell = grid.GetCell(x, y);
                    counts.TryGetValue(cell, out int c);
                    counts[cell] = c + 1;
                }

            int totalCells = grid.Width * grid.Height;
            int wallCount = counts.GetValueOrDefault(VineCellType.Wall, 0);
            int emptyCount = counts.GetValueOrDefault(VineCellType.Empty, 0);

            float wallPct = wallCount / (float)totalCells;
            ctx.Assert(wallPct < 0.7f, $"{prefix}/wall_density",
                $"Wall density {wallPct:P0} should be <70% ({wallCount}/{totalCells})");
            ctx.Assert(emptyCount > totalCells * 0.15f, $"{prefix}/buildable_space",
                $"Buildable {emptyCount} should be >15% ({totalCells * 0.15f:F0})");

            string breakdown = string.Join(", ", counts.OrderByDescending(kv => kv.Value)
                .Select(kv => $"{kv.Key}={kv.Value}"));
            GD.Print($"  [{prefix}] Cells: {breakdown}");
        }

        private void ValidateEntryExit(TestContext ctx, VineGrid grid, string prefix)
        {
            var entries = grid.EntryRegions;
            ctx.Assert(entries.Count > 0, $"{prefix}/has_entries",
                $"Need >=1 entry region, got {entries.Count}");

            int active = entries.Count(e => e.Active);
            ctx.Assert(active > 0, $"{prefix}/has_active_entry",
                $"Need >=1 active entry, got {active}/{entries.Count}");

            var exit = grid.ExitPoint;
            ctx.Assert(grid.InBounds(exit), $"{prefix}/exit_in_bounds",
                $"Exit ({exit.X},{exit.Y}) must be in bounds");

            var exitCell = grid.GetCell(exit);
            ctx.Assert(exitCell == VineCellType.Exit, $"{prefix}/exit_cell_type",
                $"Exit cell should be Exit, got {exitCell}");

            for (int i = 0; i < entries.Count; i++)
            {
                ctx.Assert(entries[i].Cells.Count > 0, $"{prefix}/entry_{i}_has_cells",
                    $"Entry {i} has {entries[i].Cells.Count} cells");
            }
        }

        private void ValidatePathability(TestContext ctx, VineGrid grid, string prefix)
        {
            var pf = new VinePathfinder();
            var root = ctx.Tree.CurrentScene;
            root.AddChild(pf);
            pf.Initialize(grid);

            var exit = grid.ExitPoint;
            int activeCount = 0, pathable = 0;

            foreach (var region in grid.EntryRegions)
            {
                if (!region.Active) continue;
                activeCount++;

                var path = pf.FindPath(region.Center, exit);
                bool ok = path != null && path.Count > 0;
                if (ok) pathable++;

                ctx.Assert(ok, $"{prefix}/path_entry_{region.Index}",
                    ok ? $"Entry {region.Index} → exit: {path.Count} steps"
                       : $"Entry {region.Index} at ({region.Center.X},{region.Center.Y}) CANNOT reach exit");

                if (ok)
                {
                    ctx.Assert(path.Count >= 5, $"{prefix}/path_min_{region.Index}",
                        $"Path length {path.Count} should be >=5");
                    int maxPath = grid.Width * grid.Height / 2;
                    ctx.Assert(path.Count < maxPath, $"{prefix}/path_max_{region.Index}",
                        $"Path length {path.Count} should be <{maxPath}");
                }
            }

            ctx.Assert(pathable == activeCount, $"{prefix}/all_pathable",
                $"{pathable}/{activeCount} active entries reach exit");

            pf.QueueFree();
        }

        private void ValidateTerrainTypes(TestContext ctx, VineGrid grid, string prefix)
        {
            int hazards = 0, pits = 0, destructible = 0, resources = 0;
            int elevated = 0, dataStreams = 0, channels = 0;

            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                    switch (grid.GetCell(x, y))
                    {
                        case VineCellType.Hazard: hazards++; break;
                        case VineCellType.Pit: pits++; break;
                        case VineCellType.DestructibleWall: destructible++; break;
                        case VineCellType.ResourceNode: resources++; break;
                        case VineCellType.Elevated: elevated++; break;
                        case VineCellType.DataStream: dataStreams++; break;
                        case VineCellType.Channel: channels++; break;
                    }

            // Verify walkability rules for each type
            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                {
                    var cell = grid.GetCell(x, y);
                    if (cell == VineCellType.Hazard)
                    {
                        ctx.Assert(grid.IsWalkable(x, y), $"{prefix}/hazard_walkable",
                            $"Hazard at ({x},{y}) must be walkable");
                        goto doneWalkCheck; // one check per type is enough
                    }
                }
            doneWalkCheck:

            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                    if (grid.GetCell(x, y) == VineCellType.Pit)
                    {
                        ctx.Assert(!grid.IsWalkable(x, y), $"{prefix}/pit_blocked",
                            $"Pit at ({x},{y}) must NOT be walkable");
                        goto donePitCheck;
                    }
            donePitCheck:

            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                    if (grid.GetCell(x, y) == VineCellType.DestructibleWall)
                    {
                        ctx.Assert(!grid.IsWalkable(x, y), $"{prefix}/dwall_blocked",
                            $"DestructibleWall at ({x},{y}) must NOT be walkable");
                        goto doneDwCheck;
                    }
            doneDwCheck:

            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                    if (grid.GetCell(x, y) == VineCellType.ResourceNode)
                    {
                        ctx.Assert(grid.IsWalkable(x, y), $"{prefix}/resource_walkable",
                            $"ResourceNode at ({x},{y}) must be walkable");
                        goto doneRnCheck;
                    }
            doneRnCheck:

            GD.Print($"  [{prefix}] Terrain: {hazards}haz {pits}pit {destructible}dwall " +
                     $"{resources}res {elevated}elev {dataStreams}ds {channels}ch");
        }

        private void ValidateTerrainVariety(TestContext ctx, VineGrid grid, string prefix)
        {
            var used = new HashSet<VineCellType>();
            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                    used.Add(grid.GetCell(x, y));

            used.Remove(VineCellType.Empty);
            used.Remove(VineCellType.Wall);
            used.Remove(VineCellType.Entry);
            used.Remove(VineCellType.Exit);
            used.Remove(VineCellType.Node);

            ctx.Assert(used.Count >= 1, $"{prefix}/terrain_variety",
                $"Uses {used.Count} special types: {string.Join(", ", used)}");
        }

        private void ValidatePlacementSpace(TestContext ctx, VineGrid grid, string prefix)
        {
            int placeable = 0;
            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                    if (grid.CanPlace(x, y)) placeable++;

            ctx.Assert(placeable >= 50, $"{prefix}/min_placement",
                $"Need >=50 placeable cells, got {placeable}");

            int nearExit = 0;
            var exit = grid.ExitPoint;
            for (int x = exit.X - 5; x <= exit.X + 5; x++)
                for (int y = exit.Y - 5; y <= exit.Y + 5; y++)
                    if (grid.InBounds(x, y) && grid.CanPlace(x, y))
                        nearExit++;

            ctx.Assert(nearExit >= 8, $"{prefix}/placement_near_exit",
                $"Need >=8 near exit, got {nearExit}");

            GD.Print($"  [{prefix}] Placement: {placeable} total, {nearExit} near exit");
        }

        private void ValidateCrossMapUniqueness(TestContext ctx)
        {
            GD.Print("\n[MapValidation] ═══ Cross-map checks ═══");

            ctx.Assert(AllMaps.Length >= 14, "cross/map_count",
                $"Registered {AllMaps.Length} maps, need >=14");

            var unique = new HashSet<string>(AllMaps);
            ctx.Assert(unique.Count == AllMaps.Length, "cross/no_duplicates",
                $"Unique={unique.Count}, total={AllMaps.Length}");
        }

        private void ValidateTerritoryJsonMaps(TestContext ctx)
        {
            GD.Print("\n[MapValidation] ═══ Territory JSON map refs ═══");

            // Load territory data and check all map_layout refs point to known maps
            TerritoryManager.Load();
            var knownMaps = new HashSet<string>(AllMaps);
            int checked_ = 0, missing = 0;

            for (int planetId = 1; planetId <= 3; planetId++)
            {
                var planet = TerritoryManager.GetPlanet(planetId);
                if (planet == null) continue;

                foreach (var region in planet.Regions)
                {
                    foreach (var site in region.Sites)
                    {
                        checked_++;
                        if (!knownMaps.Contains(site.MapLayout))
                        {
                            missing++;
                            ctx.Assert(false, $"territory/{site.Id}/map_exists",
                                $"Site '{site.Name}' refs map '{site.MapLayout}' which has no layout builder");
                        }
                    }
                }
            }

            if (missing == 0)
                ctx.Assert(true, "territory/all_maps_valid",
                    $"All {checked_} territory sites reference valid map layouts");

            GD.Print($"  Checked {checked_} territory sites, {missing} missing maps");
        }
    }
}
