using System;
using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Predefined map layouts for Vine Logic TD.
    /// Each layout defines entry/exit points, wall placement, and terrain features.
    /// The player builds the vine network — the map just sets the arena shape.
    /// S1: removed floor dispatch — layouts are now named maps, selected by ID.
    /// </summary>
    public static class VineMapLayouts
    {
        /// <summary>
        /// Build a map by layout name. Checks for JSON data file first, falls back to hardcoded.
        /// S1: replaces BuildFloor(grid, floor) — no floor indexing.
        /// </summary>
        /// <summary>
        /// Last shield wall configs parsed from JSON level data.
        /// Null if map was built from hardcoded layout.
        /// </summary>
        public static List<ShieldWallConfig> LastShieldWallConfigs { get; private set; }

        public static void BuildMap(VineGrid grid, string layoutName)
        {
            LastShieldWallConfigs = null;

            string filename = $"{layoutName}.json";
            if (LevelSerializer.FileExists(filename))
            {
                var data = LevelSerializer.LoadFromFile(filename);
                if (data != null)
                {
                    BuildFromData(grid, data);
                    LastShieldWallConfigs = ParseShieldWallConfigs(data);
                    return;
                }
            }

            // Fallback to hardcoded layouts
            switch (layoutName)
            {
                case "gateway":     BuildGateway(grid); break;
                case "conduit":     BuildConduit(grid); break;
                case "arena":       BuildArena(grid); break;
                case "forge":       BuildForge(grid); break;
                case "labyrinth":   BuildLabyrinth(grid); break;
                case "crucible":    BuildCrucible(grid); break;
                case "crossroads":  BuildCrossroads(grid); break;
                default:            BuildGateway(grid); break;
            }
        }

        private static List<ShieldWallConfig> ParseShieldWallConfigs(LevelData data)
        {
            if (data.ShieldWalls == null || data.ShieldWalls.Count == 0) return null;

            var configs = new List<ShieldWallConfig>();
            foreach (var sw in data.ShieldWalls)
            {
                var dir = sw.Direction switch
                {
                    "North" => CardinalDirection.North,
                    "East" => CardinalDirection.East,
                    "South" => CardinalDirection.South,
                    "West" => CardinalDirection.West,
                    _ => CardinalDirection.North
                };
                var trigger = sw.TriggerType switch
                {
                    "WorldObject" => ShieldWallTriggerType.WorldObject,
                    "UIPrompt" => ShieldWallTriggerType.UIPrompt,
                    "Scripted" => ShieldWallTriggerType.Scripted,
                    "Manual" => ShieldWallTriggerType.Manual,
                    _ => ShieldWallTriggerType.TimeMilestone
                };
                configs.Add(new ShieldWallConfig
                {
                    Direction = dir,
                    HP = sw.HP,
                    EntryRegionIndex = sw.EntryRegionIndex,
                    TriggerType = trigger,
                    TriggerTime = sw.TriggerTime
                });
            }
            return configs;
        }

        /// <summary>
        /// Build a map from a LevelData JSON definition.
        /// </summary>
        public static void BuildFromData(VineGrid grid, LevelData data)
        {
            int w = grid.Width;
            int h = grid.Height;

            TerrainProfile profile = data.HeightmapProfile switch
            {
                "Valley" => TerrainProfile.Valley,
                "Complex" => TerrainProfile.Complex,
                _ => TerrainProfile.Gentle
            };

            var overrides = new List<HeightOverride>();
            if (data.HeightOverrides != null)
            {
                foreach (var ov in data.HeightOverrides)
                    overrides.Add(new HeightOverride(ov.X1, ov.Y1, ov.X2, ov.Y2, ov.TargetHeight));
            }

            grid.GenerateHeightmap(profile, overrides);

            if (data.EntryRegions != null)
            {
                foreach (var entry in data.EntryRegions)
                {
                    grid.SetEntryRegion(entry.StartX, entry.StartY, entry.EndX, entry.EndY);

                    // Apply direction and active state from JSON if specified
                    var region = grid.EntryRegions[grid.EntryRegions.Count - 1];
                    if (!string.IsNullOrEmpty(entry.Direction))
                    {
                        region.Direction = entry.Direction switch
                        {
                            "North" => CardinalDirection.North,
                            "East" => CardinalDirection.East,
                            "South" => CardinalDirection.South,
                            "West" => CardinalDirection.West,
                            _ => region.Direction
                        };
                    }
                    region.Active = entry.StartActive;
                }
            }

            if (data.ExitPoint != null)
                grid.SetExit(data.ExitPoint.X, data.ExitPoint.Y);

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
                        // Phase5-MapDesign: new terrain types
                        case "Hazard": grid.SetHazardCell(cell.X, cell.Y, HazardType.Acid); break;
                        case "HazardAcid": grid.SetHazardCell(cell.X, cell.Y, HazardType.Acid); break;
                        case "HazardLava": grid.SetHazardCell(cell.X, cell.Y, HazardType.Lava); break;
                        case "HazardElectric": grid.SetHazardCell(cell.X, cell.Y, HazardType.Electric); break;
                        case "Pit": grid.SetPit(cell.X, cell.Y); break;
                        case "DestructibleWall":
                            grid.SetDestructibleWall(cell.X, cell.Y);
                            grid.SetDestructibleWallVisual(cell.X, cell.Y);
                            break;
                        case "ResourceNode": grid.SetResourceNodeCell(cell.X, cell.Y); break;
                    }
                }
            }

            if (data.Props != null)
            {
                foreach (var prop in data.Props)
                    grid.SetProp(prop.X, prop.Y, prop.PropType);
            }

            if (data.Assets != null)
            {
                foreach (var asset in data.Assets)
                {
                    var model = AssetLibrary.InstantiateNormalized(asset.Path);
                    if (model == null) continue;
                    model.Position = new Vector3(asset.PosX, asset.PosY, asset.PosZ);
                    model.RotationDegrees = new Vector3(asset.RotX, asset.RotY, asset.RotZ);
                    model.Scale = new Vector3(asset.ScaleX, asset.ScaleY, asset.ScaleZ);
                    // Only apply material override if explicitly requested in JSON.
                    // Default (null or "original"): keep the model's original materials.
                    ApplyMaterialOverride(model, asset.MaterialOverride);
                    grid.AddChild(model);
                }
            }

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
                    // Animated props: keep original materials (no override data on this type)
                    grid.AddChild(controller);
                }
            }

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

            // Phase5-MapDesign: Load terrain mutations for milestone-driven map changes
            if (data.TerrainMutations != null && data.TerrainMutations.Count > 0)
            {
                var mutations = new List<TerrainMutation>();
                foreach (var md in data.TerrainMutations)
                {
                    var newType = md.NewType switch
                    {
                        "Empty" => VineCellType.Empty,
                        "Hazard" or "HazardAcid" => VineCellType.Hazard,
                        "HazardLava" => VineCellType.Hazard,
                        "HazardElectric" => VineCellType.Hazard,
                        "Pit" => VineCellType.Pit,
                        "Wall" => VineCellType.Wall,
                        _ => VineCellType.Empty
                    };
                    var hazType = md.NewType switch
                    {
                        "HazardLava" => HazardType.Lava,
                        "HazardElectric" => HazardType.Electric,
                        _ => Enum.TryParse<HazardType>(md.HazardType, out var ht) ? ht : HazardType.Acid
                    };
                    mutations.Add(new TerrainMutation
                    {
                        TriggerWave = md.TriggerWave,
                        Cell = new Vector2I(md.X, md.Y),
                        NewType = newType,
                        HazardType = hazType
                    });
                }
                // Find the TerrainMutationManager and load
                var mutMgr = grid.GetTree()?.Root?.FindChild("TerrainMutationManager", true, false);
                if (mutMgr is TerrainMutationManager tmm)
                    tmm.LoadMutations(mutations);
                else
                    GD.Print($"[VineMapLayouts] {mutations.Count} terrain mutations defined but TerrainMutationManager not found (will load on next run)");
            }

            BuildEntryExitVisuals(grid);
        }

        /// <summary>
        /// "Gateway" — 1 entry region (left center), 1 exit (right center).
        /// Introductory layout with elevated platform clusters.
        /// </summary>
        public static void BuildGateway(VineGrid grid)
        {
            int w = grid.Width;
            int h = grid.Height;

            var overrides = new List<HeightOverride> {
                new(0, 0, 1, h, 0f),                                    // West edge
                new(w / 2 - 3, h / 2 - 3, w / 2 + 3, h / 2 + 3, 0f),  // Center (Spire)
                new(0, 0, w, 1, 0f),                                    // North edge
                new(w - 2, 0, w, h, 0f),                                // East edge
                new(0, h - 2, w, h, 0f),                                // South edge
            };
            grid.GenerateHeightmap(TerrainProfile.Gentle, overrides);

            // Entry 0: West — full edge (active)
            grid.SetEntryRegion(0, 1, 0, h - 2);
            // Entry 1: North — full edge (dormant)
            grid.SetEntryRegion(1, 0, w - 2, 0);
            // Entry 2: East — full edge (dormant)
            grid.SetEntryRegion(w - 1, 1, w - 1, h - 2);
            // Entry 3: South — full edge (dormant)
            grid.SetEntryRegion(1, h - 1, w - 2, h - 1);

            // Spire at center of playspace
            grid.SetExit(w / 2, h / 2);

            for (int x = 3; x <= 6; x++)
            for (int y = 2; y <= 4; y++)
                grid.SetElevated(x, y);

            for (int x = 3; x <= 6; x++)
            for (int y = h - 5; y <= h - 3; y++)
                grid.SetElevated(x, y);

            for (int x = w - 6; x <= w - 4; x++)
            for (int y = 2; y <= 3; y++)
                grid.SetElevated(x, y);
            for (int x = w - 6; x <= w - 4; x++)
            for (int y = h - 4; y <= h - 3; y++)
                grid.SetElevated(x, y);

            for (int x = 8; x <= 11; x++)
                SetWall(grid, x, 3);
            for (int x = 8; x <= 11; x++)
                SetWall(grid, x, h - 4);

            // Phase5-MapDesign: sample hazard, pit, destructible wall, resource node placements
            // Acid pool near center-north — enemies can path through it but take damage
            grid.SetHazardCell(w / 2 - 3, h / 2 - 4, HazardType.Acid);
            grid.SetHazardCell(w / 2 - 2, h / 2 - 4, HazardType.Acid);

            // Lava vent near center-south
            grid.SetHazardCell(w / 2 + 2, h / 2 + 3, HazardType.Lava);

            // Pits creating chokepoints near center corridors
            grid.SetPit(w / 2 - 5, h / 2);
            grid.SetPit(w / 2 + 4, h / 2);

            // Destructible walls — shortcuts enemies can bash through
            grid.SetDestructibleWall(w / 2, h / 2 - 3);
            grid.SetDestructibleWallVisual(w / 2, h / 2 - 3);
            grid.SetDestructibleWall(w / 2, h / 2 + 3);
            grid.SetDestructibleWallVisual(w / 2, h / 2 + 3);

            // Resource nodes — placed away from safe positions to reward expansion
            grid.SetResourceNodeCell(w / 2 + 8, h / 2 - 5);
            grid.SetResourceNodeCell(w / 2 - 8, h / 2 + 4);

            ScatterProps(grid, 1);
            BuildEntryExitVisuals(grid);
        }

        /// <summary>
        /// "Conduit" — two entries on left, exit on right.
        /// Open field with wall obstacles to create natural chokepoints.
        /// </summary>
        public static void BuildConduit(VineGrid grid)
        {
            int w = grid.Width;
            int h = grid.Height;

            var overrides = new List<HeightOverride> {
                new(0, h / 3 - 2, 2, h / 3 + 3, 0f),
                new(0, 2 * h / 3 - 3, 2, 2 * h / 3 + 2, 0f),
                new(w - 3, h / 2 - 2, w, h / 2 + 2, 0f),
            };
            grid.GenerateHeightmap(TerrainProfile.Valley, overrides);

            grid.SetEntryRegion(0, h / 3 - 1, 0, h / 3 + 2);
            grid.SetEntryRegion(0, 2 * h / 3 - 2, 0, 2 * h / 3 + 1);
            grid.SetExit(w - 1, h / 2);

            for (int y = h / 4; y < 3 * h / 4; y++)
            {
                if (y == h / 2) continue;
                SetWall(grid, w / 3, y);
                SetWall(grid, 2 * w / 3, y);
            }

            for (int x = w / 4; x < 3 * w / 4; x++)
            {
                SetWall(grid, x, 1);
                SetWall(grid, x, h - 2);
            }

            for (int x = 4; x <= 5; x++)
            for (int y = h / 3 + 1; y <= h / 3 + 2; y++)
                grid.SetElevated(x, y);

            for (int x = 4; x <= 5; x++)
            for (int y = 2 * h / 3 - 2; y <= 2 * h / 3 - 1; y++)
                grid.SetElevated(x, y);

            for (int x = w / 3 + 2; x <= 2 * w / 3 - 2; x++)
            for (int y = h / 2 - 1; y <= h / 2 + 1; y++)
            {
                if (grid.GetCell(x, y) == VineCellType.Empty)
                    grid.SetElevated(x, y);
            }

            ScatterProps(grid, 2);
            BuildEntryExitVisuals(grid);
        }

        /// <summary>
        /// "Arena" — 3 entries (left, top, bottom), 1 exit (right center).
        /// Boss arena with elevated fortifications and inner wall ring.
        /// </summary>
        public static void BuildArena(VineGrid grid)
        {
            int w = grid.Width;
            int h = grid.Height;

            var overrides = new List<HeightOverride> {
                new(0, h / 2 - 3, 2, h / 2 + 3, 0f),
                new(w / 2 - 3, 0, w / 2 + 3, 1, 0f),
                new(w / 2 - 3, h - 2, w / 2 + 3, h, 0f),
                new(w - 3, h / 2 - 2, w, h / 2 + 2, 0f),
                new(w / 4, h / 4, 3 * w / 4, 3 * h / 4, -0.5f),
            };
            grid.GenerateHeightmap(TerrainProfile.Complex, overrides);

            grid.SetEntryRegion(0, h / 2 - 2, 0, h / 2 + 2);
            grid.SetEntryRegion(w / 2 - 2, 0, w / 2 + 2, 0);
            grid.SetEntryRegion(w / 2 - 2, h - 1, w / 2 + 2, h - 1);
            grid.SetExit(w - 1, h / 2);

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

            int innerL = w / 4;
            int innerR = 3 * w / 4;
            int innerT = h / 4;
            int innerB = 3 * h / 4;

            for (int x = innerL; x <= innerR; x++)
            {
                if (x >= w / 2 - 1 && x <= w / 2 + 1) continue;
                SetWall(grid, x, innerT);
            }
            for (int x = innerL; x <= innerR; x++)
            {
                if (x >= w / 2 - 1 && x <= w / 2 + 1) continue;
                SetWall(grid, x, innerB);
            }
            for (int y = innerT; y <= innerB; y++)
            {
                if (y >= h / 2 - 1 && y <= h / 2 + 1) continue;
                SetWall(grid, innerL, y);
            }

            ScatterProps(grid, 3);
            BuildEntryExitVisuals(grid);
        }

        /// <summary>
        /// "Forge" — 2 entries on opposite sides (left, right), exit bottom center.
        /// Open field with scattered elevated platforms.
        /// </summary>
        public static void BuildForge(VineGrid grid)
        {
            int w = grid.Width;
            int h = grid.Height;

            var overrides = new List<HeightOverride> {
                new(0, h / 2 - 4, 2, h / 2 + 4, 0f),
                new(w - 3, h / 2 - 4, w, h / 2 + 4, 0f),
                new(w / 2 - 3, h - 3, w / 2 + 3, h, 0f),
            };
            grid.GenerateHeightmap(TerrainProfile.Valley, overrides);

            grid.SetEntryRegion(0, h / 2 - 3, 0, h / 2 + 3);
            grid.SetEntryRegion(w - 1, h / 2 - 3, w - 1, h / 2 + 3);
            grid.SetExit(w / 2, h - 1);

            for (int x = 0; x < w; x++)
                SetWall(grid, x, 0);

            int[][] platforms = {
                new[] { w / 4 - 1, h / 4, w / 4 + 1, h / 4 + 2 },
                new[] { 3 * w / 4 - 1, h / 4, 3 * w / 4 + 1, h / 4 + 2 },
                new[] { w / 2 - 2, h / 2 - 1, w / 2 + 2, h / 2 + 1 },
                new[] { w / 4, 3 * h / 4 - 1, w / 4 + 2, 3 * h / 4 + 1 },
                new[] { 3 * w / 4 - 2, 3 * h / 4 - 1, 3 * w / 4, 3 * h / 4 + 1 },
            };
            foreach (var p in platforms)
                for (int x = p[0]; x <= p[2]; x++)
                for (int y = p[1]; y <= p[3]; y++)
                    if (grid.InBounds(x, y))
                        grid.SetElevated(x, y);

            for (int y = h / 3; y <= h / 3 + 3; y++)
                SetWall(grid, w / 3, y);
            for (int y = h / 3; y <= h / 3 + 3; y++)
                SetWall(grid, 2 * w / 3, y);

            ScatterProps(grid, 4);
            BuildEntryExitVisuals(grid);
        }

        /// <summary>
        /// "Labyrinth" — 3 entries (left, top-right, bottom-right), exit center.
        /// Dense wall grid creating a maze with multiple routing options.
        /// </summary>
        public static void BuildLabyrinth(VineGrid grid)
        {
            int w = grid.Width;
            int h = grid.Height;

            var overrides = new List<HeightOverride> {
                new(0, h / 2 - 3, 2, h / 2 + 3, 0f),
                new(3 * w / 4, 0, w, 2, 0f),
                new(3 * w / 4, h - 3, w, h, 0f),
                new(w / 2 - 3, h / 2 - 3, w / 2 + 3, h / 2 + 3, 0f),
            };
            grid.GenerateHeightmap(TerrainProfile.Gentle, overrides);

            grid.SetEntryRegion(0, h / 2 - 3, 0, h / 2 + 3);
            grid.SetEntryRegion(w - 1, 1, w - 1, 4);
            grid.SetEntryRegion(w - 1, h - 5, w - 1, h - 2);
            grid.SetExit(w / 2, h / 2);

            for (int col = 4; col < w - 4; col += 4)
            {
                for (int y = 2; y < h - 2; y++)
                {
                    if (y % 3 == 0) continue;
                    if (Mathf.Abs(y - h / 2) <= 2 && Mathf.Abs(col - w / 2) <= 2) continue;
                    SetWall(grid, col, y);
                }
            }

            for (int row = 3; row < h - 3; row += 5)
            {
                for (int x = 2; x < w - 2; x += 2)
                {
                    if (grid.GetCell(x, row) != VineCellType.Empty) continue;
                    if (Mathf.Abs(x - w / 2) <= 3 && Mathf.Abs(row - h / 2) <= 3) continue;
                    SetWall(grid, x, row);
                }
            }

            for (int x = 1; x < w / 2; x++)
            {
                if (grid.GetCell(x, h / 2) == VineCellType.Wall)
                    grid.ClearCell(x, h / 2);
            }

            ScatterProps(grid, 5);
            BuildEntryExitVisuals(grid);
        }

        /// <summary>
        /// "Crucible" — 4 entries (all cardinal), exit center.
        /// Final boss arena with central depression and radial wall ring.
        /// </summary>
        public static void BuildCrucible(VineGrid grid)
        {
            int w = grid.Width;
            int h = grid.Height;

            var overrides = new List<HeightOverride> {
                new(0, h / 2 - 4, 2, h / 2 + 4, 0f),
                new(w - 3, h / 2 - 4, w, h / 2 + 4, 0f),
                new(w / 2 - 4, 0, w / 2 + 4, 2, 0f),
                new(w / 2 - 4, h - 3, w / 2 + 4, h, 0f),
                new(w / 4, h / 4, 3 * w / 4, 3 * h / 4, -1f),
            };
            grid.GenerateHeightmap(TerrainProfile.Complex, overrides);

            grid.SetEntryRegion(0, h / 2 - 4, 0, h / 2 + 4);
            grid.SetEntryRegion(w - 1, h / 2 - 4, w - 1, h / 2 + 4);
            grid.SetEntryRegion(w / 2 - 4, 0, w / 2 + 4, 0);
            grid.SetEntryRegion(w / 2 - 4, h - 1, w / 2 + 4, h - 1);
            grid.SetExit(w / 2, h / 2);

            int[][] corners = {
                new[] { 2, 2, 5, 5 },
                new[] { w - 6, 2, w - 3, 5 },
                new[] { 2, h - 6, 5, h - 3 },
                new[] { w - 6, h - 6, w - 3, h - 3 },
            };
            foreach (var c in corners)
                for (int x = c[0]; x <= c[2]; x++)
                for (int y = c[1]; y <= c[3]; y++)
                    grid.SetElevated(x, y);

            int ringL = w / 4 + 2;
            int ringR = 3 * w / 4 - 2;
            int ringT = h / 4 + 2;
            int ringB = 3 * h / 4 - 2;

            for (int x = ringL; x <= ringR; x++)
            {
                if (Mathf.Abs(x - w / 2) <= 2) continue;
                SetWall(grid, x, ringT);
            }
            for (int x = ringL; x <= ringR; x++)
            {
                if (Mathf.Abs(x - w / 2) <= 2) continue;
                SetWall(grid, x, ringB);
            }
            for (int y = ringT; y <= ringB; y++)
            {
                if (Mathf.Abs(y - h / 2) <= 2) continue;
                SetWall(grid, ringL, y);
            }
            for (int y = ringT; y <= ringB; y++)
            {
                if (Mathf.Abs(y - h / 2) <= 2) continue;
                SetWall(grid, ringR, y);
            }

            ScatterProps(grid, 6);
            BuildEntryExitVisuals(grid);
        }

        /// <summary>
        /// "Crossroads" — four entries, exit in center.
        /// Tests radial defense patterns.
        /// </summary>
        public static void BuildCrossroads(VineGrid grid)
        {
            int w = grid.Width;
            int h = grid.Height;

            grid.SetEntry(0, h / 2);
            grid.SetEntry(w - 1, h / 2);
            grid.SetEntry(w / 2, 0);
            grid.SetEntry(w / 2, h - 1);
            grid.SetExit(w / 2, h / 2);

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
        /// S1: density param replaces floor index — controls prop count scaling.
        /// </summary>
        private static void ScatterProps(VineGrid grid, int density)
        {
            int count = Constants.PROP_SCATTER_BASE + (density - 1) * Constants.PROP_SCATTER_PER_LEVEL;
            int placed = 0;
            int attempts = 0;
            int maxAttempts = count * 10;

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

            foreach (var region in grid.EntryRegions)
            {
                // Skip inactive regions (gated by shield walls — visuals added when wall breaks)
                if (!region.Active) continue;

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

            var exitGlow = new MeshInstance3D();
            var exitMesh = new CylinderMesh { TopRadius = 1.5f, BottomRadius = 1.5f, Height = 0.05f };
            exitGlow.Mesh = exitMesh;
            exitGlow.Position = grid.GridToWorld(grid.ExitPoint) + new Vector3(0, 0.04f, 0);
            var exitMat = new StandardMaterial3D();
            exitMat.AlbedoColor = new Color(1f, 1f, 1f, 0.15f);
            exitMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            exitMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            exitMat.EmissionEnabled = true;
            exitMat.Emission = BitPalette.Accent;
            exitMat.EmissionEnergyMultiplier = 0.3f;
            exitGlow.MaterialOverride = exitMat;
            exitGlow.AddToGroup("ExitGlow");
            grid.AddChild(exitGlow);
        }

        /// <summary>
        /// Apply material override to a loaded model only if explicitly requested in JSON.
        /// null or "original" = keep the model's original materials/textures intact.
        /// </summary>
        private static void ApplyMaterialOverride(Node3D model, MaterialOverrideData overrideData)
        {
            if (overrideData == null) return;

            string type = overrideData.MaterialType?.ToLowerInvariant() ?? "original";
            switch (type)
            {
                case "theme":
                    PlanetTheme.Current?.ApplyToNode(model);
                    break;
                case "bit":
                    BitPalette.ApplyToNode(model);
                    break;
                case "faction":
                    var faction = (VineEnemyFaction)overrideData.FactionId;
                    PlanetTheme.Current?.ApplyEnemyTheme(model, faction);
                    break;
                case "original":
                default:
                    // Keep original materials — do nothing
                    break;
            }
        }
    }
}
