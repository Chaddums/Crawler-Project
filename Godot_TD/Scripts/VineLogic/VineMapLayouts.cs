using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Predefined map layouts for Vine Logic TD.
    /// Each layout defines entry/exit points, wall placement, and terrain features.
    /// The player builds the vine network — the map just sets the arena shape.
    /// </summary>
    public static class VineMapLayouts
    {
        /// <summary>
        /// Dispatch to the correct floor layout.
        /// </summary>
        public static void BuildFloor(VineGrid grid, int floor)
        {
            switch (floor)
            {
                case 1: BuildGateway(grid); break;
                case 2: BuildConduit(grid); break;
                case 3: BuildArena(grid); break;
                default: BuildConduit(grid); break;
            }
        }

        /// <summary>
        /// Floor 1: "Gateway" — 1 entry (left center), 1 exit (right center).
        /// Introductory layout with elevated platform clusters and a channel corridor.
        /// </summary>
        public static void BuildGateway(VineGrid grid)
        {
            int w = grid.Width;
            int h = grid.Height;

            // Single entry, single exit
            grid.SetEntry(0, h / 2);
            grid.SetExit(w - 1, h / 2);

            // Border walls top/bottom
            for (int x = 0; x < w; x++)
            {
                SetWall(grid, x, 0);
                SetWall(grid, x, h - 1);
            }

            // Elevated platform cluster — top-left corner
            for (int x = 2; x <= 5; x++)
            for (int y = 2; y <= 4; y++)
                grid.SetElevated(x, y);

            // Elevated platform cluster — bottom-right corner
            for (int x = w - 6; x <= w - 3; x++)
            for (int y = h - 5; y <= h - 3; y++)
                grid.SetElevated(x, y);

            // Channel corridor through the middle
            for (int x = w / 4; x < 3 * w / 4; x++)
                grid.SetChannel(x, h / 2);

            BuildEntryExitVisuals(grid);
        }

        /// <summary>
        /// Floor 2: "Conduit" — two entries on left, exit on right.
        /// Open field with wall obstacles to create natural chokepoints.
        /// </summary>
        public static void BuildConduit(VineGrid grid)
        {
            int w = grid.Width;
            int h = grid.Height;

            // Entries on left edge
            grid.SetEntry(0, h / 3);
            grid.SetEntry(0, 2 * h / 3);

            // Exit on right edge
            grid.SetExit(w - 1, h / 2);

            // Center columns
            for (int y = h / 4; y < 3 * h / 4; y++)
            {
                if (y == h / 2) continue; // Gap in the middle
                SetWall(grid, w / 3, y);
                SetWall(grid, 2 * w / 3, y);
            }

            // Top and bottom walls to funnel
            for (int x = w / 4; x < 3 * w / 4; x++)
            {
                SetWall(grid, x, 1);
                SetWall(grid, x, h - 2);
            }

            BuildEntryExitVisuals(grid);
        }

        /// <summary>
        /// Floor 3: "Arena" — 3 entries (left, top, bottom), 1 exit (right center).
        /// Boss arena with DataStream fast lanes, elevated fortifications, and channel chokepoints.
        /// </summary>
        public static void BuildArena(VineGrid grid)
        {
            int w = grid.Width;
            int h = grid.Height;

            // Three entries
            grid.SetEntry(0, h / 2);
            grid.SetEntry(w / 2, 0);
            grid.SetEntry(w / 2, h - 1);

            // Exit on right
            grid.SetExit(w - 1, h / 2);

            // Elevated platform fortifications in corners
            for (int x = 1; x <= 3; x++)
            for (int y = 1; y <= 3; y++)
                grid.SetElevated(x, y);

            for (int x = 1; x <= 3; x++)
            for (int y = h - 4; y <= h - 2; y++)
                grid.SetElevated(x, y);

            for (int x = w - 4; x <= w - 2; x++)
            for (int y = 1; y <= 3; y++)
                grid.SetElevated(x, y);

            for (int x = w - 4; x <= w - 2; x++)
            for (int y = h - 4; y <= h - 2; y++)
                grid.SetElevated(x, y);

            // Two DataStream lanes across the width (at h/3 and 2h/3)
            int lane1 = h / 3;
            int lane2 = 2 * h / 3;
            for (int x = 2; x < w - 2; x++)
            {
                if (grid.GetCell(x, lane1) == VineCellType.Empty)
                    grid.SetDataStream(x, lane1);
                if (grid.GetCell(x, lane2) == VineCellType.Empty)
                    grid.SetDataStream(x, lane2);
            }

            // Inner wall ring with gaps defining the arena
            int innerL = w / 4;
            int innerR = 3 * w / 4;
            int innerT = h / 4;
            int innerB = 3 * h / 4;

            // Top wall segment with gap at center
            for (int x = innerL; x <= innerR; x++)
            {
                if (x >= w / 2 - 1 && x <= w / 2 + 1) continue; // Gap
                SetWall(grid, x, innerT);
            }
            // Bottom wall segment with gap at center
            for (int x = innerL; x <= innerR; x++)
            {
                if (x >= w / 2 - 1 && x <= w / 2 + 1) continue; // Gap
                SetWall(grid, x, innerB);
            }
            // Left wall segment with gap at center
            for (int y = innerT; y <= innerB; y++)
            {
                if (y >= h / 2 - 1 && y <= h / 2 + 1) continue; // Gap
                SetWall(grid, innerL, y);
            }

            // Channels near entries for chokepoints
            for (int y = h / 2 - 2; y <= h / 2 + 2; y++)
            {
                if (grid.GetCell(1, y) == VineCellType.Empty)
                    grid.SetChannel(1, y);
            }
            for (int x = w / 2 - 2; x <= w / 2 + 2; x++)
            {
                if (grid.GetCell(x, 1) == VineCellType.Empty)
                    grid.SetChannel(x, 1);
                if (grid.GetCell(x, h - 2) == VineCellType.Empty)
                    grid.SetChannel(x, h - 2);
            }

            BuildEntryExitVisuals(grid);
        }

        /// <summary>
        /// Build the "Crossroads" layout — four entries, exit in center.
        /// Tests radial defense patterns.
        /// </summary>
        public static void BuildCrossroads(VineGrid grid)
        {
            int w = grid.Width;
            int h = grid.Height;

            // Four entries on edges
            grid.SetEntry(0, h / 2);
            grid.SetEntry(w - 1, h / 2);
            grid.SetEntry(w / 2, 0);
            grid.SetEntry(w / 2, h - 1);

            // Exit in center
            grid.SetExit(w / 2, h / 2);

            // Corner walls
            int margin = 3;
            for (int x = 0; x < margin; x++)
            for (int y = 0; y < margin; y++)
            {
                SetWall(grid, x, y);
                SetWall(grid, w - 1 - x, y);
                SetWall(grid, x, h - 1 - y);
                SetWall(grid, w - 1 - x, h - 1 - y);
            }

            BuildEntryExitVisuals(grid);
        }

        private static void SetWall(VineGrid grid, int x, int y)
        {
            grid.SetWall(x, y);
        }

        private static void BuildEntryExitVisuals(VineGrid grid)
        {
            // Entry markers — teal pillars with labels
            int entryNum = 1;
            foreach (var entry in grid.EntryPoints)
            {
                var marker = new MeshInstance3D();
                var cyl = new CylinderMesh();
                cyl.TopRadius = 0.3f;
                cyl.BottomRadius = 0.4f;
                cyl.Height = 1.5f;
                marker.Mesh = cyl;
                marker.Position = grid.GridToWorld(entry) + new Vector3(0, 0.75f, 0);
                marker.MaterialOverride = TronTheme.MakeEntryMarkerMaterial();
                grid.AddChild(marker);

                // Floating label
                var label = new Label3D();
                label.Text = $"ENTRY {entryNum}";
                label.FontSize = 72;
                label.OutlineSize = 10;
                label.Modulate = TronTheme.EntryTeal;
                label.Position = grid.GridToWorld(entry) + new Vector3(0, 2.2f, 0);
                label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
                grid.AddChild(label);
                entryNum++;
            }

            // Exit marker — red pillar with label (danger contrast against cyan)
            var exitMarker = new MeshInstance3D();
            var exitCyl = new CylinderMesh();
            exitCyl.TopRadius = 0.4f;
            exitCyl.BottomRadius = 0.5f;
            exitCyl.Height = 2f;
            exitMarker.Mesh = exitCyl;
            exitMarker.Position = grid.GridToWorld(grid.ExitPoint) + new Vector3(0, 1f, 0);
            exitMarker.MaterialOverride = TronTheme.MakeExitMarkerMaterial();
            grid.AddChild(exitMarker);

            var exitLabel = new Label3D();
            exitLabel.Text = "CORE";
            exitLabel.FontSize = 96;
            exitLabel.OutlineSize = 12;
            exitLabel.Modulate = TronTheme.ExitRed;
            exitLabel.Position = grid.GridToWorld(grid.ExitPoint) + new Vector3(0, 2.8f, 0);
            exitLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            grid.AddChild(exitLabel);
        }
    }
}
