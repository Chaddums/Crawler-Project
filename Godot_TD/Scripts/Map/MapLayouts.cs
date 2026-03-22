using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Named map layouts. Each builds onto a MapGrid.
    /// </summary>
    public static class MapLayouts
    {
        public struct MapInfo
        {
            public string Id;
            public string Name;
            public string Description;
            public int Width;
            public int Height;
        }

        public static readonly List<MapInfo> Available = new()
        {
            new MapInfo { Id = "scrapyard", Name = "Scrapyard", Description = "Two lanes, scattered debris. The classic.", Width = 24, Height = 16 },
            new MapInfo { Id = "gauntlet", Name = "The Gauntlet", Description = "Four spawns, narrow corridors, one core.", Width = 28, Height = 20 },
            new MapInfo { Id = "crossroads", Name = "Crossroads", Description = "Central core, enemies from all four edges.", Width = 22, Height = 22 },
        };

        public static void Build(string mapId, MapGrid grid)
        {
            switch (mapId)
            {
                case "gauntlet": BuildGauntlet(grid); break;
                case "crossroads": BuildCrossroads(grid); break;
                default: MapBuilder.BuildDefaultMap(grid); break;
            }
        }

        /// <summary>
        /// The Gauntlet: 4 spawn points on left, narrow corridor maze, core on far right.
        /// Heavy on terrain manipulation potential.
        /// </summary>
        private static void BuildGauntlet(MapGrid grid)
        {
            var rng = new RandomNumberGenerator();
            rng.Seed = 99;

            // 4 spawn points spread along left edge
            grid.SetSpawnPoint(0, 3);
            grid.SetSpawnPoint(0, 8);
            grid.SetSpawnPoint(0, 13);
            grid.SetSpawnPoint(0, 18);

            // Core on far right, centered
            grid.SetCorePosition(grid.Width - 2, grid.Height / 2);
            MapBuilder.BuildCoreVisual(grid, grid.CorePosition);

            // Vertical walls creating corridors
            int[] wallColumns = { 5, 10, 15, 20 };
            foreach (int wx in wallColumns)
            {
                // Each wall has gaps at different heights
                int gapStart = rng.RandiRange(2, grid.Height - 6);
                int gapSize = rng.RandiRange(2, 4);
                for (int y = 1; y < grid.Height - 1; y++)
                {
                    if (y >= gapStart && y < gapStart + gapSize) continue;
                    if (rng.Randf() < 0.6f)
                    {
                        grid.SetCell(wx, y, TerrainType.Blocked);
                        MapBuilder.BuildWallVisual(grid, wx, y);
                    }
                    else
                    {
                        grid.SetCell(wx, y, TerrainType.Debris);
                        MapBuilder.BuildDebrisVisual(grid, wx, y, rng);
                    }
                }
            }

            // Scatter extra debris between corridors
            for (int i = 0; i < 20; i++)
            {
                int x = rng.RandiRange(1, grid.Width - 3);
                int y = rng.RandiRange(1, grid.Height - 2);
                if (grid.GetCell(x, y) == TerrainType.Open)
                {
                    grid.SetCell(x, y, TerrainType.Debris);
                    MapBuilder.BuildDebrisVisual(grid, x, y, rng);
                }
            }

            foreach (var spawn in grid.SpawnPoints)
                MapBuilder.BuildSpawnVisual(grid, spawn);
        }

        /// <summary>
        /// Crossroads: Core in center, enemies from all 4 edges.
        /// Forces radial defense. Square map.
        /// </summary>
        private static void BuildCrossroads(MapGrid grid)
        {
            var rng = new RandomNumberGenerator();
            rng.Seed = 77;

            int cx = grid.Width / 2;
            int cy = grid.Height / 2;

            // Spawns on all 4 edges
            grid.SetSpawnPoint(0, cy);
            grid.SetSpawnPoint(grid.Width - 1, cy);
            grid.SetSpawnPoint(cx, 0);
            grid.SetSpawnPoint(cx, grid.Height - 1);

            // Core dead center
            grid.SetCorePosition(cx, cy);
            MapBuilder.BuildCoreVisual(grid, grid.CorePosition);

            // Ring of debris around center
            for (int x = cx - 4; x <= cx + 4; x++)
            for (int y = cy - 4; y <= cy + 4; y++)
            {
                if (!grid.InBounds(x, y)) continue;
                if (x == cx && y == cy) continue; // Don't block core
                float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (dist > 3.5f && dist < 5f && rng.Randf() < 0.5f)
                {
                    grid.SetCell(x, y, TerrainType.Debris);
                    MapBuilder.BuildDebrisVisual(grid, x, y, rng);
                }
            }

            // Corner blocks
            int[][] corners = { new[]{2,2}, new[]{2,grid.Height-3}, new[]{grid.Width-3,2}, new[]{grid.Width-3,grid.Height-3} };
            foreach (var corner in corners)
            {
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    int bx = corner[0] + dx, by = corner[1] + dy;
                    if (grid.InBounds(bx, by) && grid.GetCell(bx, by) == TerrainType.Open)
                    {
                        grid.SetCell(bx, by, TerrainType.Blocked);
                        MapBuilder.BuildWallVisual(grid, bx, by);
                    }
                }
            }

            // Scatter debris
            for (int i = 0; i < 25; i++)
            {
                int x = rng.RandiRange(1, grid.Width - 2);
                int y = rng.RandiRange(1, grid.Height - 2);
                if (grid.GetCell(x, y) == TerrainType.Open && (x != cx || y != cy))
                {
                    grid.SetCell(x, y, TerrainType.Debris);
                    MapBuilder.BuildDebrisVisual(grid, x, y, rng);
                }
            }

            foreach (var spawn in grid.SpawnPoints)
                MapBuilder.BuildSpawnVisual(grid, spawn);
        }
    }
}
