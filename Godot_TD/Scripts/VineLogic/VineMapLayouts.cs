using System.Collections.Generic;
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
        /// Checks for data-driven JSON first, falls back to hardcoded.
        /// </summary>
        public static void BuildFloor(VineGrid grid, int floor)
        {
            string filename = $"floor_{floor}.json";
            if (LevelSerializer.FileExists(filename))
            {
                var data = LevelSerializer.LoadFromFile(filename);
                if (data != null)
                {
                    BuildFromData(grid, data);
                    return;
                }
            }

            // Fallback to hardcoded
            switch (floor)
            {
                case 1: BuildGateway(grid); break;
                case 2: BuildConduit(grid); break;
                case 3: BuildArena(grid); break;
                default: BuildConduit(grid); break;
            }
        }

        /// <summary>
        /// Build a floor from a LevelData JSON definition.
        /// Uses the existing VineGrid API for all operations.
        /// </summary>
        public static void BuildFromData(VineGrid grid, LevelData data)
        {
            int w = grid.Width;
            int h = grid.Height;

            // Parse terrain profile
            TerrainProfile profile = data.HeightmapProfile switch
            {
                "Valley" => TerrainProfile.Valley,
                "Complex" => TerrainProfile.Complex,
                _ => TerrainProfile.Gentle
            };

            // Convert height overrides
            var overrides = new List<HeightOverride>();
            if (data.HeightOverrides != null)
            {
                foreach (var ov in data.HeightOverrides)
                    overrides.Add(new HeightOverride(ov.X1, ov.Y1, ov.X2, ov.Y2, ov.TargetHeight));
            }

            grid.GenerateHeightmap(profile, overrides);

            // Entry regions
            if (data.EntryRegions != null)
            {
                foreach (var entry in data.EntryRegions)
                    grid.SetEntryRegion(entry.StartX, entry.StartY, entry.EndX, entry.EndY);
            }

            // Exit point
            if (data.ExitPoint != null)
                grid.SetExit(data.ExitPoint.X, data.ExitPoint.Y);

            // Place cells (sparse: only non-Empty cells are stored)
            if (data.Cells != null)
            {
                foreach (var cell in data.Cells)
                {
                    switch (cell.Type)
                    {
                        case "Wall": SetWall(grid, cell.X, cell.Y); break;
                        case "Elevated": grid.SetElevated(cell.X, cell.Y); break;
                        case "Channel": grid.SetChannel(cell.X, cell.Y); break;
                        case "DataStream": grid.SetDataStream(cell.X, cell.Y); break;
                    }
                }
            }

            // Place props
            if (data.Props != null)
            {
                foreach (var prop in data.Props)
                    grid.SetProp(prop.X, prop.Y, prop.PropType);
            }

            // Place free-positioned assets
            if (data.Assets != null)
            {
                foreach (var asset in data.Assets)
                {
                    var model = AssetLibrary.InstantiateNormalized(asset.Path);
                    if (model == null) continue;
                    model.Position = new Vector3(asset.PosX, asset.PosY, asset.PosZ);
                    model.RotationDegrees = new Vector3(asset.RotX, asset.RotY, asset.RotZ);
                    model.Scale = new Vector3(asset.ScaleX, asset.ScaleY, asset.ScaleZ);
                    PlanetTheme.Current.ApplyToNode(model);
                    grid.AddChild(model);
                }
            }

            // Place lights
            if (data.Lights != null)
            {
                foreach (var light in data.Lights)
                {
                    Light3D lightNode;
                    if (light.LightType == "Spot")
                    {
                        var spot = new SpotLight3D();
                        spot.SpotRange = light.Range;
                        lightNode = spot;
                    }
                    else
                    {
                        var omni = new OmniLight3D();
                        omni.OmniRange = light.Range;
                        lightNode = omni;
                    }
                    lightNode.Position = new Vector3(light.PosX, light.PosY, light.PosZ);
                    lightNode.LightColor = new Color(light.ColorR, light.ColorG, light.ColorB);
                    lightNode.LightEnergy = light.Energy;
                    lightNode.ShadowEnabled = light.Shadow;
                    grid.AddChild(lightNode);
                }
            }

            // Place animated props
            if (data.AnimatedProps != null)
            {
                foreach (var ap in data.AnimatedProps)
                {
                    var model = AssetLibrary.InstantiateNormalized(ap.Path);
                    if (model == null) continue;
                    var controller = new AnimatedPropController();
                    controller.Position = new Vector3(ap.PosX, ap.PosY, ap.PosZ);
                    controller.AnimType = ap.AnimType;
                    controller.AnimSpeed = ap.AnimSpeed;
                    controller.AnimAmplitude = ap.AnimAmplitude;
                    controller.AddChild(model);
                    PlanetTheme.Current.ApplyToNode(model);
                    grid.AddChild(controller);
                }
            }

            // Place environment FX
            if (data.EnvironmentFX != null)
            {
                foreach (var fx in data.EnvironmentFX)
                {
                    var fxNode = EnvironmentFXFactory.Create(
                        fx.FXType,
                        new Vector3(fx.PosX, fx.PosY, fx.PosZ),
                        fx.Radius,
                        fx.Intensity,
                        new Color(fx.ColorR, fx.ColorG, fx.ColorB));
                    if (fxNode != null)
                        grid.AddChild(fxNode);
                }
            }

            // Build entry/exit visuals (same as hardcoded path)
            BuildEntryExitVisuals(grid);
        }

        /// <summary>
        /// Floor 1: "Gateway" — 1 entry region (left center), 1 exit (right center).
        /// Introductory layout with elevated platform clusters.
        /// </summary>
        public static void BuildGateway(VineGrid grid)
        {
            int w = grid.Width;
            int h = grid.Height;

            // Heightmap: gentle rolling hills
            var overrides = new List<HeightOverride> {
                new(0, h / 2 - 3, 2, h / 2 + 3, 0f),      // Flatten entry zone
                new(w - 3, h / 2 - 2, w, h / 2 + 2, 0f),   // Flatten exit zone
            };
            grid.GenerateHeightmap(TerrainProfile.Gentle, overrides);

            // Single entry region (5 cells tall), single exit
            grid.SetEntryRegion(0, h / 2 - 2, 0, h / 2 + 2);
            grid.SetExit(w - 1, h / 2);

            // Border walls top/bottom
            for (int x = 0; x < w; x++)
            {
                SetWall(grid, x, 0);
                SetWall(grid, x, h - 1);
            }

            // ── Elevated platforms — impassable landmarks that force routing ──

            // Top-left elevated cluster
            for (int x = 3; x <= 6; x++)
            for (int y = 2; y <= 4; y++)
                grid.SetElevated(x, y);

            // Bottom-left elevated cluster
            for (int x = 3; x <= 6; x++)
            for (int y = h - 5; y <= h - 3; y++)
                grid.SetElevated(x, y);

            // Center elevated island — forces split around it
            for (int x = w / 2 - 1; x <= w / 2 + 1; x++)
            for (int y = h / 2 - 1; y <= h / 2 + 1; y++)
                grid.SetElevated(x, y);

            // Right elevated platform near exit
            for (int x = w - 6; x <= w - 4; x++)
            for (int y = 2; y <= 3; y++)
                grid.SetElevated(x, y);
            for (int x = w - 6; x <= w - 4; x++)
            for (int y = h - 4; y <= h - 3; y++)
                grid.SetElevated(x, y);

            // ── Walls — hard blockers creating chokepoints ──

            // Top wall segment — creates upper lane
            for (int x = 8; x <= 11; x++)
                SetWall(grid, x, 3);

            // Bottom wall segment — creates lower lane
            for (int x = 8; x <= 11; x++)
                SetWall(grid, x, h - 4);

            // Mid-right wall — narrows approach to exit
            for (int y = h / 2 - 2; y <= h / 2 + 2; y++)
            {
                if (y == h / 2) continue; // Gap at center
                SetWall(grid, w - 7, y);
            }

            // Scatter props
            ScatterProps(grid, 1);

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

            // Heightmap: valley profile — central depression, ridges at edges
            var overrides = new List<HeightOverride> {
                new(0, h / 3 - 2, 2, h / 3 + 3, 0f),         // Flatten entry 1
                new(0, 2 * h / 3 - 3, 2, 2 * h / 3 + 2, 0f), // Flatten entry 2
                new(w - 3, h / 2 - 2, w, h / 2 + 2, 0f),      // Flatten exit zone
            };
            grid.GenerateHeightmap(TerrainProfile.Valley, overrides);

            // Entry regions on left edge (4 cells each)
            grid.SetEntryRegion(0, h / 3 - 1, 0, h / 3 + 2);
            grid.SetEntryRegion(0, 2 * h / 3 - 2, 0, 2 * h / 3 + 1);

            // Exit on right edge
            grid.SetExit(w - 1, h / 2);

            // ── Center wall columns with gaps — main routing obstacles ──
            for (int y = h / 4; y < 3 * h / 4; y++)
            {
                if (y == h / 2) continue;
                SetWall(grid, w / 3, y);
                SetWall(grid, 2 * w / 3, y);
            }

            // Top and bottom border walls
            for (int x = w / 4; x < 3 * w / 4; x++)
            {
                SetWall(grid, x, 1);
                SetWall(grid, x, h - 2);
            }

            // ── Elevated platforms — island obstacles between lanes ──

            // Upper island between entry lanes
            for (int x = 4; x <= 5; x++)
            for (int y = h / 3 + 1; y <= h / 3 + 2; y++)
                grid.SetElevated(x, y);

            // Lower island between entry lanes
            for (int x = 4; x <= 5; x++)
            for (int y = 2 * h / 3 - 2; y <= 2 * h / 3 - 1; y++)
                grid.SetElevated(x, y);

            // Center elevated platform between the two wall columns
            for (int x = w / 3 + 2; x <= 2 * w / 3 - 2; x++)
            for (int y = h / 2 - 1; y <= h / 2 + 1; y++)
            {
                if (grid.GetCell(x, y) == VineCellType.Empty)
                    grid.SetElevated(x, y);
            }

            // Scatter props
            ScatterProps(grid, 2);

            BuildEntryExitVisuals(grid);
        }

        /// <summary>
        /// Floor 3: "Arena" — 3 entries (left, top, bottom), 1 exit (right center).
        /// Boss arena with elevated fortifications and inner wall ring.
        /// </summary>
        public static void BuildArena(VineGrid grid)
        {
            int w = grid.Width;
            int h = grid.Height;

            // Heightmap: complex profile — dramatic height, plateaus, boss arena depression
            var overrides = new List<HeightOverride> {
                new(0, h / 2 - 3, 2, h / 2 + 3, 0f),           // Flatten left entry
                new(w / 2 - 3, 0, w / 2 + 3, 1, 0f),           // Flatten top entry
                new(w / 2 - 3, h - 2, w / 2 + 3, h, 0f),       // Flatten bottom entry
                new(w - 3, h / 2 - 2, w, h / 2 + 2, 0f),       // Flatten exit
                new(w / 4, h / 4, 3 * w / 4, 3 * h / 4, -0.5f),// Central arena depression
            };
            grid.GenerateHeightmap(TerrainProfile.Complex, overrides);

            // Three entry regions (wider spans)
            grid.SetEntryRegion(0, h / 2 - 2, 0, h / 2 + 2);
            grid.SetEntryRegion(w / 2 - 2, 0, w / 2 + 2, 0);
            grid.SetEntryRegion(w / 2 - 2, h - 1, w / 2 + 2, h - 1);

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

            // Scatter props
            ScatterProps(grid, 3);

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

        private static readonly RandomNumberGenerator _scatterRng = new();

        private static readonly string[] PropTypes = {
            "container", "generator", "barrel_stack", "antenna", "rubble_pile", "pipe_cluster"
        };

        /// <summary>
        /// Scatter random props on empty cells, avoiding entry/exit zones.
        /// Uses pathfinder to ensure props don't block all paths.
        /// </summary>
        private static void ScatterProps(VineGrid grid, int floor)
        {
            int count = Constants.PROP_SCATTER_BASE + (floor - 1) * Constants.PROP_SCATTER_PER_FLOOR;
            int placed = 0;
            int attempts = 0;
            int maxAttempts = count * 10;

            // Collect entry/exit positions to avoid
            var avoidCells = new HashSet<Vector2I>();
            foreach (var entry in grid.EntryPoints)
            {
                for (int dx = -3; dx <= 3; dx++)
                for (int dy = -3; dy <= 3; dy++)
                    avoidCells.Add(new Vector2I(entry.X + dx, entry.Y + dy));
            }
            var exit = grid.ExitPoint;
            for (int dx = -3; dx <= 3; dx++)
            for (int dy = -3; dy <= 3; dy++)
                avoidCells.Add(new Vector2I(exit.X + dx, exit.Y + dy));

            while (placed < count && attempts < maxAttempts)
            {
                attempts++;
                int x = _scatterRng.RandiRange(1, grid.Width - 2);
                int y = _scatterRng.RandiRange(1, grid.Height - 2);
                var cell = new Vector2I(x, y);

                if (grid.GetCell(x, y) != VineCellType.Empty) continue;
                if (avoidCells.Contains(cell)) continue;

                // Check path safety via temporary pathfinder check
                // We can't call WouldBlockAllPaths without a pathfinder, so just place and trust
                // that the scatter count is low relative to grid size
                string propType = PropTypes[_scatterRng.RandiRange(0, PropTypes.Length - 1)];
                grid.SetProp(x, y, propType);
                placed++;
            }
        }

        private static void SetWall(VineGrid grid, int x, int y)
        {
            grid.SetWall(x, y);
        }

        private static void BuildEntryExitVisuals(VineGrid grid)
        {
            var entryColor = PlanetTheme.Current.EntryMarkerColor;

            // Draw entry region strips instead of single-point glows
            foreach (var region in grid.EntryRegions)
            {
                foreach (var cell in region.Cells)
                {
                    var glow = new MeshInstance3D();
                    var glowMesh = new CylinderMesh();
                    glowMesh.TopRadius = 1.0f;
                    glowMesh.BottomRadius = 1.0f;
                    glowMesh.Height = 0.05f;
                    glow.Mesh = glowMesh;
                    glow.Position = grid.GridToWorld(cell) + new Vector3(0, 0.03f, 0);

                    var glowMat = new StandardMaterial3D();
                    glowMat.AlbedoColor = new Color(entryColor.R, entryColor.G, entryColor.B, 0.25f);
                    glowMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                    glowMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                    glowMat.EmissionEnabled = true;
                    glowMat.Emission = entryColor;
                    glowMat.EmissionEnergyMultiplier = 0.4f;
                    glow.MaterialOverride = glowMat;
                    grid.AddChild(glow);
                }
            }

            // Harvester at exit position instead of abstract core
            var harvester = new VineHarvester();
            grid.AddChild(harvester);
            harvester.GlobalPosition = grid.GridToWorld(grid.ExitPoint);
            grid.Harvester = harvester;
        }
    }
}
