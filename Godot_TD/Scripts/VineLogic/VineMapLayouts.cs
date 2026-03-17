using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Predefined map layouts for Vine Logic TD.
    /// Each layout defines entry/exit points and wall placement.
    /// The player builds the vine network — the map just sets the arena shape.
    /// </summary>
    public static class VineMapLayouts
    {
        /// <summary>
        /// Build the "Conduit" layout — two entries on left, exit on right.
        /// Open field with some wall obstacles to create natural chokepoints.
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

            // Some wall obstacles to give the player something to work around
            // Center column
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
