using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Exports hardcoded VineMapLayouts floors to LevelData JSON files.
    /// Run once to bootstrap the data-driven level system.
    /// </summary>
    public static class LevelExporter
    {
        private static readonly string[] FloorNames = { "", "Gateway", "Conduit", "Arena", "Crossroads" };
        private static readonly string[] FloorProfiles = { "", "Gentle", "Valley", "Complex", "Gentle" };

        public static void ExportAll()
        {
            for (int floor = 1; floor <= 3; floor++)
                ExportFromHardcoded(floor);
            GD.Print("[LevelExporter] All 3 floors exported.");
        }

        public static LevelData ExportFromHardcoded(int floor)
        {
            int idx = Mathf.Clamp(floor, 1, FloorNames.Length - 1);
            var data = new LevelData
            {
                Id = $"floor_{floor}",
                Name = FloorNames[idx],
                Floor = floor,
                Width = Constants.VINE_MAP_WIDTH,
                Height = Constants.VINE_MAP_HEIGHT,
                HeightmapProfile = FloorProfiles[idx],
                PlanetTheme = "tron"
            };

            switch (floor)
            {
                case 1: ExportGateway(data); break;
                case 2: ExportConduit(data); break;
                case 3: ExportArena(data); break;
                case 4: ExportCrossroads(data); break;
            }

            LevelSerializer.SaveToFile(data, $"floor_{floor}.json");
            return data;
        }

        private static void ExportGateway(LevelData data)
        {
            int w = data.Width, h = data.Height;

            // Height overrides
            data.HeightOverrides.Add(new HeightOverrideData { X1 = 0, Y1 = h / 2 - 3, X2 = 2, Y2 = h / 2 + 3, TargetHeight = 0f });
            data.HeightOverrides.Add(new HeightOverrideData { X1 = w - 3, Y1 = h / 2 - 2, X2 = w, Y2 = h / 2 + 2, TargetHeight = 0f });

            // Entry/exit
            data.EntryRegions.Add(new EntryRegionData { StartX = 0, StartY = h / 2 - 2, EndX = 0, EndY = h / 2 + 2 });
            data.ExitPoint = new ExitPointData { X = w - 1, Y = h / 2 };

            // Border walls
            for (int x = 0; x < w; x++)
            {
                data.Cells.Add(new CellPlacement { X = x, Y = 0, Type = "Wall" });
                data.Cells.Add(new CellPlacement { X = x, Y = h - 1, Type = "Wall" });
            }

            // Elevated platforms
            AddElevatedRect(data, 3, 2, 6, 4);
            AddElevatedRect(data, 3, h - 5, 6, h - 3);
            AddElevatedRect(data, w / 2 - 1, h / 2 - 1, w / 2 + 1, h / 2 + 1);
            AddElevatedRect(data, w - 6, 2, w - 4, 3);
            AddElevatedRect(data, w - 6, h - 4, w - 4, h - 3);

            // Wall segments
            for (int x = 8; x <= 11; x++)
            {
                data.Cells.Add(new CellPlacement { X = x, Y = 3, Type = "Wall" });
                data.Cells.Add(new CellPlacement { X = x, Y = h - 4, Type = "Wall" });
            }

            // Mid-right wall with gap
            for (int y = h / 2 - 2; y <= h / 2 + 2; y++)
            {
                if (y == h / 2) continue;
                data.Cells.Add(new CellPlacement { X = w - 7, Y = y, Type = "Wall" });
            }
        }

        private static void ExportConduit(LevelData data)
        {
            int w = data.Width, h = data.Height;

            // Height overrides
            data.HeightOverrides.Add(new HeightOverrideData { X1 = 0, Y1 = h / 3 - 2, X2 = 2, Y2 = h / 3 + 3, TargetHeight = 0f });
            data.HeightOverrides.Add(new HeightOverrideData { X1 = 0, Y1 = 2 * h / 3 - 3, X2 = 2, Y2 = 2 * h / 3 + 2, TargetHeight = 0f });
            data.HeightOverrides.Add(new HeightOverrideData { X1 = w - 3, Y1 = h / 2 - 2, X2 = w, Y2 = h / 2 + 2, TargetHeight = 0f });

            // Entry regions
            data.EntryRegions.Add(new EntryRegionData { StartX = 0, StartY = h / 3 - 1, EndX = 0, EndY = h / 3 + 2 });
            data.EntryRegions.Add(new EntryRegionData { StartX = 0, StartY = 2 * h / 3 - 2, EndX = 0, EndY = 2 * h / 3 + 1 });
            data.ExitPoint = new ExitPointData { X = w - 1, Y = h / 2 };

            // Center wall columns with gaps
            for (int y = h / 4; y < 3 * h / 4; y++)
            {
                if (y == h / 2) continue;
                data.Cells.Add(new CellPlacement { X = w / 3, Y = y, Type = "Wall" });
                data.Cells.Add(new CellPlacement { X = 2 * w / 3, Y = y, Type = "Wall" });
            }

            // Top/bottom border walls
            for (int x = w / 4; x < 3 * w / 4; x++)
            {
                data.Cells.Add(new CellPlacement { X = x, Y = 1, Type = "Wall" });
                data.Cells.Add(new CellPlacement { X = x, Y = h - 2, Type = "Wall" });
            }

            // Elevated platforms
            AddElevatedRect(data, 4, h / 3 + 1, 5, h / 3 + 2);
            AddElevatedRect(data, 4, 2 * h / 3 - 2, 5, 2 * h / 3 - 1);

            // Center elevated between columns
            for (int x = w / 3 + 2; x <= 2 * w / 3 - 2; x++)
            for (int y = h / 2 - 1; y <= h / 2 + 1; y++)
                data.Cells.Add(new CellPlacement { X = x, Y = y, Type = "Elevated" });
        }

        private static void ExportArena(LevelData data)
        {
            int w = data.Width, h = data.Height;

            // Height overrides
            data.HeightOverrides.Add(new HeightOverrideData { X1 = 0, Y1 = h / 2 - 3, X2 = 2, Y2 = h / 2 + 3, TargetHeight = 0f });
            data.HeightOverrides.Add(new HeightOverrideData { X1 = w / 2 - 3, Y1 = 0, X2 = w / 2 + 3, Y2 = 1, TargetHeight = 0f });
            data.HeightOverrides.Add(new HeightOverrideData { X1 = w / 2 - 3, Y1 = h - 2, X2 = w / 2 + 3, Y2 = h, TargetHeight = 0f });
            data.HeightOverrides.Add(new HeightOverrideData { X1 = w - 3, Y1 = h / 2 - 2, X2 = w, Y2 = h / 2 + 2, TargetHeight = 0f });
            data.HeightOverrides.Add(new HeightOverrideData { X1 = w / 4, Y1 = h / 4, X2 = 3 * w / 4, Y2 = 3 * h / 4, TargetHeight = -0.5f });

            // Three entry regions
            data.EntryRegions.Add(new EntryRegionData { StartX = 0, StartY = h / 2 - 2, EndX = 0, EndY = h / 2 + 2 });
            data.EntryRegions.Add(new EntryRegionData { StartX = w / 2 - 2, StartY = 0, EndX = w / 2 + 2, EndY = 0 });
            data.EntryRegions.Add(new EntryRegionData { StartX = w / 2 - 2, StartY = h - 1, EndX = w / 2 + 2, EndY = h - 1 });
            data.ExitPoint = new ExitPointData { X = w - 1, Y = h / 2 };

            // Corner fortifications
            AddElevatedRect(data, 1, 1, 3, 3);
            AddElevatedRect(data, 1, h - 4, 3, h - 2);
            AddElevatedRect(data, w - 4, 1, w - 2, 3);
            AddElevatedRect(data, w - 4, h - 4, w - 2, h - 2);

            // Inner wall ring
            int innerL = w / 4, innerR = 3 * w / 4;
            int innerT = h / 4, innerB = 3 * h / 4;

            for (int x = innerL; x <= innerR; x++)
            {
                if (x >= w / 2 - 1 && x <= w / 2 + 1) continue;
                data.Cells.Add(new CellPlacement { X = x, Y = innerT, Type = "Wall" });
                data.Cells.Add(new CellPlacement { X = x, Y = innerB, Type = "Wall" });
            }
            for (int y = innerT; y <= innerB; y++)
            {
                if (y >= h / 2 - 1 && y <= h / 2 + 1) continue;
                data.Cells.Add(new CellPlacement { X = innerL, Y = y, Type = "Wall" });
            }
        }

        private static void ExportCrossroads(LevelData data)
        {
            int w = data.Width, h = data.Height;

            // Four entry points on edges
            data.EntryRegions.Add(new EntryRegionData { StartX = 0, StartY = h / 2, EndX = 0, EndY = h / 2 });
            data.EntryRegions.Add(new EntryRegionData { StartX = w - 1, StartY = h / 2, EndX = w - 1, EndY = h / 2 });
            data.EntryRegions.Add(new EntryRegionData { StartX = w / 2, StartY = 0, EndX = w / 2, EndY = 0 });
            data.EntryRegions.Add(new EntryRegionData { StartX = w / 2, StartY = h - 1, EndX = w / 2, EndY = h - 1 });

            // Exit in center
            data.ExitPoint = new ExitPointData { X = w / 2, Y = h / 2 };

            // Corner walls
            int margin = 3;
            for (int x = 0; x < margin; x++)
            for (int y = 0; y < margin; y++)
            {
                data.Cells.Add(new CellPlacement { X = x, Y = y, Type = "Wall" });
                data.Cells.Add(new CellPlacement { X = w - 1 - x, Y = y, Type = "Wall" });
                data.Cells.Add(new CellPlacement { X = x, Y = h - 1 - y, Type = "Wall" });
                data.Cells.Add(new CellPlacement { X = w - 1 - x, Y = h - 1 - y, Type = "Wall" });
            }
        }

        private static void AddElevatedRect(LevelData data, int x1, int y1, int x2, int y2)
        {
            for (int x = x1; x <= x2; x++)
            for (int y = y1; y <= y2; y++)
                data.Cells.Add(new CellPlacement { X = x, Y = y, Type = "Elevated" });
        }
    }
}
