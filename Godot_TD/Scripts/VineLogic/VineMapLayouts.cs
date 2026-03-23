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
                // Phase5: Grid Prime maps
                case "circuit_lanes":   BuildCircuitLanes(grid); break;
                case "antenna_field":   BuildAntennaField(grid); break;
                case "grid_maze":       BuildGridMaze(grid); break;
                case "data_nexus":      BuildDataNexus(grid); break;
                // Phase5: Scrapyard maps
                case "salvage_yard":    BuildSalvageYard(grid); break;
                case "rust_pit":        BuildRustPit(grid); break;
                case "foundry":         BuildFoundry(grid); break;
                case "smelter":         BuildSmelter(grid); break;
                default:                BuildGateway(grid); break;
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

            // Phase5-MapDesign: Load expansion zones
            if (data.ExpansionZones != null && data.ExpansionZones.Count > 0)
            {
                var zones = new List<ExpansionZone>();
                foreach (var zd in data.ExpansionZones)
                {
                    var zone = new ExpansionZone
                    {
                        TriggerWave = zd.TriggerWave,
                        X1 = zd.X1, Y1 = zd.Y1, X2 = zd.X2, Y2 = zd.Y2,
                        Label = zd.Label
                    };
                    if (zd.Features != null)
                    {
                        foreach (var fd in zd.Features)
                        {
                            zone.Features.Add(new TerrainMutation
                            {
                                Cell = new Vector2I(fd.X, fd.Y),
                                NewType = ParseCellType(fd.NewType),
                                HazardType = ParseHazardType(fd.NewType, fd.HazardType)
                            });
                        }
                    }
                    zones.Add(zone);
                }
                var mutMgr2 = grid.GetTree()?.Root?.FindChild("TerrainMutationManager", true, false);
                if (mutMgr2 is TerrainMutationManager tmm2)
                {
                    tmm2.LoadExpansionZones(zones);
                    // Seal zones at map init
                    foreach (var zone in zones)
                        tmm2.SealExpansionZone(zone);
                }
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
        /// Open map with radial symmetry. Elevated platforms at midpoints
        /// between entries and spire. Hazard ring around center forces
        /// enemies to approach through damage. Resource nodes in corners.
        /// </summary>
        public static void BuildCrossroads(VineGrid grid)
        {
            int w = grid.Width;
            int h = grid.Height;
            int cx = w / 2, cy = h / 2;

            grid.GenerateHeightmap(TerrainProfile.Gentle, new List<HeightOverride> {
                new(cx - 2, cy - 2, cx + 2, cy + 2, 0f),
                new(0, 0, 2, h, 0f), new(w - 3, 0, w, h, 0f),
                new(0, 0, w, 2, 0f), new(0, h - 3, w, h, 0f)
            });

            // 4 entries (all active — immediate all-angle pressure)
            grid.SetEntryRegion(0, cy - 2, 0, cy + 2);
            grid.SetEntryRegion(w - 1, cy - 2, w - 1, cy + 2);
            grid.SetEntryRegion(cx - 2, 0, cx + 2, 0);
            grid.SetEntryRegion(cx - 2, h - 1, cx + 2, h - 1);
            grid.SetExit(cx, cy);

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

            // Elevated sniper platforms at the 4 midpoints (between entries and spire)
            for (int x = cx - 7; x <= cx - 5; x++)
                for (int y = cy - 1; y <= cy + 1; y++)
                    grid.SetElevated(x, y);
            for (int x = cx + 5; x <= cx + 7; x++)
                for (int y = cy - 1; y <= cy + 1; y++)
                    grid.SetElevated(x, y);
            for (int x = cx - 1; x <= cx + 1; x++)
                for (int y = cy - 7; y <= cy - 5; y++)
                    grid.SetElevated(x, y);
            for (int x = cx - 1; x <= cx + 1; x++)
                for (int y = cy + 5; y <= cy + 7; y++)
                    grid.SetElevated(x, y);

            // Acid hazard ring around spire (radius ~3) — enemies take damage on approach
            for (int dx = -3; dx <= 3; dx++)
                for (int dy = -3; dy <= 3; dy++)
                {
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist >= 2.5f && dist <= 3.5f)
                    {
                        // Leave cardinal gaps so enemies can still reach spire
                        if (Mathf.Abs(dx) <= 1 && Mathf.Abs(dy) <= 1) continue;
                        int hx = cx + dx, hy = cy + dy;
                        if (grid.InBounds(hx, hy) && grid.GetCell(hx, hy) == VineCellType.Empty)
                            grid.SetHazardCell(hx, hy, HazardType.Acid);
                    }
                }

            // Destructible walls on the diagonal approaches
            grid.SetDestructibleWall(cx - 4, cy - 4);
            grid.SetDestructibleWallVisual(cx - 4, cy - 4);
            grid.SetDestructibleWall(cx + 4, cy + 4);
            grid.SetDestructibleWallVisual(cx + 4, cy + 4);

            // Resource nodes in the 4 corners (risky — far from spire)
            grid.SetResourceNodeCell(4, 4);
            grid.SetResourceNodeCell(w - 5, 4);
            grid.SetResourceNodeCell(4, h - 5);
            grid.SetResourceNodeCell(w - 5, h - 5);

            ScatterProps(grid, 2);
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

        private static VineCellType ParseCellType(string type) => type switch
        {
            "Empty" => VineCellType.Empty,
            "Wall" => VineCellType.Wall,
            "Elevated" => VineCellType.Elevated,
            "Channel" => VineCellType.Channel,
            "DataStream" => VineCellType.DataStream,
            "Hazard" or "HazardAcid" or "HazardLava" or "HazardElectric" => VineCellType.Hazard,
            "Pit" => VineCellType.Pit,
            "DestructibleWall" => VineCellType.DestructibleWall,
            "ResourceNode" => VineCellType.ResourceNode,
            _ => VineCellType.Empty
        };

        private static HazardType ParseHazardType(string cellType, string hazardType) => cellType switch
        {
            "HazardLava" => HazardType.Lava,
            "HazardElectric" => HazardType.Electric,
            _ => Enum.TryParse<HazardType>(hazardType ?? "", out var ht2) ? ht2 : HazardType.Acid
        };

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

        // ════════════════════════════════════════════════════════════════════
        // Phase5: Grid Prime maps (clean geometry, teaches mechanics)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// "Circuit Lanes" — three parallel DataStream corridors running east-west
        /// with elevated sniper platforms between them. Tight lanes force players
        /// to specialize: AoE for lanes, single-target for platforms.
        /// </summary>
        public static void BuildCircuitLanes(VineGrid grid)
        {
            int w = grid.Width, h = grid.Height;
            grid.GenerateHeightmap(TerrainProfile.Gentle, new List<HeightOverride> {
                new(0, 0, 1, h, 0f), new(w - 2, 0, w, h, 0f),
                new(w / 2 - 2, h / 2 - 2, w / 2 + 2, h / 2 + 2, 0f)
            });

            // Entry: West (active), North + South (dormant shield walls)
            grid.SetEntryRegion(0, 3, 0, h - 4);
            grid.SetEntryRegion(3, 0, w - 4, 0);    // dormant
            grid.SetEntryRegion(3, h - 1, w - 4, h - 1); // dormant
            grid.SetExit(w - 4, h / 2);

            // Three DataStream corridors (north, center, south)
            int[] lanes = { h / 4, h / 2, 3 * h / 4 };
            foreach (int ly in lanes)
                for (int x = 3; x < w - 4; x++)
                    grid.SetDataStream(x, ly);

            // Walls between lanes
            for (int x = 5; x < w - 5; x += 2)
            {
                if (x % 6 == 0) continue; // gaps every 3rd pair for cross-lane movement
                SetWall(grid, x, lanes[0] + 2);
                SetWall(grid, x, lanes[2] - 2);
            }

            // Elevated sniper platforms between lanes
            for (int x = 8; x <= 12; x++)
            {
                grid.SetElevated(x, lanes[0] + 4);
                grid.SetElevated(x, lanes[1] - 3);
            }
            for (int x = w - 13; x <= w - 9; x++)
            {
                grid.SetElevated(x, lanes[1] + 3);
                grid.SetElevated(x, lanes[2] - 4);
            }

            // Hazards at lane intersections — electric zaps in the DataStream
            grid.SetHazardCell(w / 2, lanes[0], HazardType.Electric);
            grid.SetHazardCell(w / 2, lanes[2], HazardType.Electric);

            // Resource nodes at far ends of outer lanes
            grid.SetResourceNodeCell(w - 8, lanes[0] - 2);
            grid.SetResourceNodeCell(w - 8, lanes[2] + 2);

            // Destructible walls blocking shortcuts between lanes
            grid.SetDestructibleWall(w / 3, lanes[0] + 2);
            grid.SetDestructibleWallVisual(w / 3, lanes[0] + 2);
            grid.SetDestructibleWall(2 * w / 3, lanes[2] - 2);
            grid.SetDestructibleWallVisual(2 * w / 3, lanes[2] - 2);

            ScatterProps(grid, 2);
            BuildEntryExitVisuals(grid);
        }

        /// <summary>
        /// "Antenna Field" — wide open center with scattered elevated antenna
        /// platforms. Resource nodes at the extremes reward expansion.
        /// Few walls — positioning is everything.
        /// </summary>
        public static void BuildAntennaField(VineGrid grid)
        {
            int w = grid.Width, h = grid.Height;
            grid.GenerateHeightmap(TerrainProfile.Complex, new List<HeightOverride> {
                new(0, 0, 2, h, 0f), new(w - 3, 0, w, h, 0f),
                new(w / 2 - 3, h / 2 - 3, w / 2 + 3, h / 2 + 3, 0f)
            });

            // 2 entries: West (active), East (dormant)
            grid.SetEntryRegion(0, h / 4, 0, 3 * h / 4);
            grid.SetEntryRegion(w - 1, h / 4, w - 1, 3 * h / 4); // dormant
            grid.SetExit(w / 2, h / 2);

            // Scattered elevated antenna platforms (irregular placement)
            int[][] platforms = {
                new[]{6, 4, 8, 5}, new[]{14, 2, 16, 3}, new[]{24, 3, 26, 5},
                new[]{10, h - 6, 12, h - 5}, new[]{20, h - 4, 22, h - 3},
                new[]{30, h / 2 - 1, 32, h / 2 + 1}, new[]{8, h / 2 + 3, 10, h / 2 + 4}
            };
            foreach (var p in platforms)
                for (int x = p[0]; x <= p[2]; x++)
                    for (int y = p[1]; y <= p[3]; y++)
                        if (grid.InBounds(x, y)) grid.SetElevated(x, y);

            // Minimal walls — just corner barriers
            for (int i = 0; i < 3; i++)
            {
                SetWall(grid, 3 + i, 1);
                SetWall(grid, 3 + i, h - 2);
                SetWall(grid, w - 4 - i, 1);
                SetWall(grid, w - 4 - i, h - 2);
            }

            // Resource nodes at extreme positions (far from spire = risky to capture)
            grid.SetResourceNodeCell(3, 2);
            grid.SetResourceNodeCell(w - 4, 2);
            grid.SetResourceNodeCell(3, h - 3);
            grid.SetResourceNodeCell(w - 4, h - 3);

            // Pits creating diagonal hazard lines
            for (int i = 0; i < 4; i++)
            {
                grid.SetPit(w / 2 - 6 + i, h / 2 - 6 + i);
                grid.SetPit(w / 2 + 5 - i, h / 2 - 6 + i);
            }

            ScatterProps(grid, 1);
            BuildEntryExitVisuals(grid);
        }

        /// <summary>
        /// "Grid Maze" — dense L-shaped walls creating a winding maze.
        /// Destructible wall shortcuts let Brutes create new paths.
        /// Channels guide enemies through the intended route.
        /// </summary>
        public static void BuildGridMaze(VineGrid grid)
        {
            int w = grid.Width, h = grid.Height;
            grid.GenerateHeightmap(TerrainProfile.Gentle, new List<HeightOverride> {
                new(0, 0, 2, h, 0f), new(w / 2 - 2, h / 2 - 2, w / 2 + 2, h / 2 + 2, 0f)
            });

            // 3 entries: West (active), North + East (dormant)
            grid.SetEntryRegion(0, h / 3, 0, 2 * h / 3);
            grid.SetEntryRegion(w / 4, 0, 3 * w / 4, 0);       // dormant
            grid.SetEntryRegion(w - 1, h / 3, w - 1, 2 * h / 3); // dormant
            grid.SetExit(w / 2, h / 2);

            // Dense maze walls — vertical barriers with alternating gaps
            for (int col = 5; col < w - 5; col += 4)
            {
                bool gapTop = (col / 4) % 2 == 0;
                for (int y = 2; y < h - 2; y++)
                {
                    if (gapTop && y < 5) continue;
                    if (!gapTop && y > h - 6) continue;
                    if (Mathf.Abs(y - h / 2) <= 2 && Mathf.Abs(col - w / 2) <= 3) continue; // spire clear
                    SetWall(grid, col, y);
                }
            }

            // L-shaped extensions from some walls
            for (int col = 7; col < w - 7; col += 8)
            {
                for (int x = col; x < col + 3 && x < w - 3; x++)
                    SetWall(grid, x, h / 3);
                for (int x = col + 1; x < col + 4 && x < w - 3; x++)
                    SetWall(grid, x, 2 * h / 3);
            }

            // Channels through the intended maze path
            for (int x = 3; x < w - 3; x += 4)
                for (int y = h / 2 - 1; y <= h / 2 + 1; y++)
                    if (grid.GetCell(x, y) == VineCellType.Empty)
                        grid.SetChannel(x, y);

            // Destructible wall shortcuts — 6 of them throughout the maze
            int[][] shortcuts = {
                new[]{7, h / 4}, new[]{15, h / 4}, new[]{23, h / 4},
                new[]{11, 3 * h / 4}, new[]{19, 3 * h / 4}, new[]{27, 3 * h / 4}
            };
            foreach (var s in shortcuts)
            {
                if (grid.InBounds(s[0], s[1]) && grid.GetCell(s[0], s[1]) == VineCellType.Wall)
                {
                    grid.ClearCell(s[0], s[1]);
                    grid.SetDestructibleWall(s[0], s[1]);
                    grid.SetDestructibleWallVisual(s[0], s[1]);
                }
            }

            // Resource node deep in the maze (hard to reach = big payoff positioning)
            grid.SetResourceNodeCell(w - 6, h / 2);

            ScatterProps(grid, 3);
            BuildEntryExitVisuals(grid);
        }

        /// <summary>
        /// "Data Nexus" — central pit ring around the spire forces enemies to path
        /// around it. DataStream highways connect entry points. Expansion zones
        /// reveal the eastern half at wave 8.
        /// </summary>
        public static void BuildDataNexus(VineGrid grid)
        {
            int w = grid.Width, h = grid.Height;
            int cx = w / 2, cy = h / 2;
            grid.GenerateHeightmap(TerrainProfile.Valley, new List<HeightOverride> {
                new(0, 0, 2, h, 0f), new(cx - 2, cy - 2, cx + 2, cy + 2, 0f)
            });

            // 4 entries: West (active), N/E/S dormant
            grid.SetEntryRegion(0, cy - 3, 0, cy + 3);
            grid.SetEntryRegion(cx - 4, 0, cx + 4, 0);
            grid.SetEntryRegion(w - 1, cy - 3, w - 1, cy + 3);
            grid.SetEntryRegion(cx - 4, h - 1, cx + 4, h - 1);
            grid.SetExit(cx, cy);

            // Pit ring around spire (radius 4-5)
            for (int x = cx - 5; x <= cx + 5; x++)
            {
                for (int y = cy - 5; y <= cy + 5; y++)
                {
                    if (!grid.InBounds(x, y)) continue;
                    float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if (dist >= 3.5f && dist <= 5f)
                    {
                        // Leave 4 bridge gaps (cardinal directions)
                        if ((Mathf.Abs(x - cx) <= 1 && y < cy) || // North bridge
                            (Mathf.Abs(x - cx) <= 1 && y > cy) || // South bridge
                            (x < cx && Mathf.Abs(y - cy) <= 1) || // West bridge
                            (x > cx && Mathf.Abs(y - cy) <= 1))   // East bridge
                            continue;
                        grid.SetPit(x, y);
                    }
                }
            }

            // DataStream highways from entries to the bridges
            for (int x = 2; x < cx - 5; x++)
                grid.SetDataStream(x, cy);
            for (int x = cx + 6; x < w - 2; x++)
                grid.SetDataStream(x, cy);
            for (int y = 2; y < cy - 5; y++)
                grid.SetDataStream(cx, y);
            for (int y = cy + 6; y < h - 2; y++)
                grid.SetDataStream(cx, y);

            // Elevated platforms at the four quadrant corners
            for (int x = 3; x <= 5; x++) for (int y = 2; y <= 4; y++) grid.SetElevated(x, y);
            for (int x = 3; x <= 5; x++) for (int y = h - 5; y <= h - 3; y++) grid.SetElevated(x, y);
            for (int x = w - 6; x <= w - 4; x++) for (int y = 2; y <= 4; y++) grid.SetElevated(x, y);
            for (int x = w - 6; x <= w - 4; x++) for (int y = h - 5; y <= h - 3; y++) grid.SetElevated(x, y);

            // Electric hazards on the bridges (risk/reward — fast path but dangerous)
            grid.SetHazardCell(cx, cy - 4, HazardType.Electric);
            grid.SetHazardCell(cx, cy + 4, HazardType.Electric);

            // Resource nodes in the far corners
            grid.SetResourceNodeCell(2, 2);
            grid.SetResourceNodeCell(w - 3, h - 3);

            // Scattered walls near entries for initial cover
            for (int i = 0; i < 3; i++)
            {
                SetWall(grid, 4, cy - 5 + i);
                SetWall(grid, 4, cy + 3 + i);
            }

            ScatterProps(grid, 2);
            BuildEntryExitVisuals(grid);
        }

        // ════════════════════════════════════════════════════════════════════
        // Phase5: Scrapyard maps (wide, chaotic, multi-angle pressure)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// "Salvage Yard" — wide open industrial field. Scattered debris walls
        /// create organic cover. Lava hazards in the center. Entries from 3 sides.
        /// No safe position — every angle is exposed.
        /// </summary>
        public static void BuildSalvageYard(VineGrid grid)
        {
            int w = grid.Width, h = grid.Height;
            grid.GenerateHeightmap(TerrainProfile.Complex, new List<HeightOverride> {
                new(0, 0, 2, h, 0f), new(w - 3, 0, w, h, 0f),
                new(0, 0, w, 2, 0f), new(0, h - 3, w, h, 0f),
                new(w / 2 - 2, h / 2 - 2, w / 2 + 2, h / 2 + 2, 0f)
            });

            // 3 entries: West (active), North + South (dormant)
            grid.SetEntryRegion(0, 2, 0, h - 3);
            grid.SetEntryRegion(4, 0, w - 5, 0);
            grid.SetEntryRegion(4, h - 1, w - 5, h - 1);
            grid.SetExit(w / 2, h / 2);

            // Scattered debris walls — irregular organic clusters
            var rng = new RandomNumberGenerator();
            rng.Seed = 42; // Deterministic so every player sees the same map
            for (int cluster = 0; cluster < 12; cluster++)
            {
                int cx = rng.RandiRange(5, w - 6);
                int cy = rng.RandiRange(4, h - 5);
                if (Mathf.Abs(cx - w / 2) < 4 && Mathf.Abs(cy - h / 2) < 4) continue; // skip spire area
                int size = rng.RandiRange(1, 3);
                for (int dx = 0; dx < size; dx++)
                    for (int dy = 0; dy < size; dy++)
                        if (grid.InBounds(cx + dx, cy + dy) && grid.GetCell(cx + dx, cy + dy) == VineCellType.Empty)
                            SetWall(grid, cx + dx, cy + dy);
            }

            // Lava hazard zone in center (avoid the spire itself)
            for (int x = w / 2 - 4; x <= w / 2 + 4; x++)
                for (int y = h / 2 - 1; y <= h / 2 + 1; y++)
                    if (Mathf.Abs(x - w / 2) > 2 || Mathf.Abs(y - h / 2) > 1)
                        if (grid.GetCell(x, y) == VineCellType.Empty)
                            grid.SetHazardCell(x, y, HazardType.Lava);

            // Acid pools near edges
            grid.SetHazardCell(5, 5, HazardType.Acid);
            grid.SetHazardCell(6, 5, HazardType.Acid);
            grid.SetHazardCell(w - 7, h - 6, HazardType.Acid);
            grid.SetHazardCell(w - 6, h - 6, HazardType.Acid);

            // Resource nodes at risky positions (far from center, near entries)
            grid.SetResourceNodeCell(w - 5, 3);
            grid.SetResourceNodeCell(5, h - 4);
            grid.SetResourceNodeCell(w / 2, 3);

            // Destructible wall across center creating a breakable barrier
            for (int y = h / 2 - 3; y <= h / 2 - 2; y++)
            {
                grid.SetDestructibleWall(w / 2 - 6, y);
                grid.SetDestructibleWallVisual(w / 2 - 6, y);
                grid.SetDestructibleWall(w / 2 + 6, y);
                grid.SetDestructibleWallVisual(w / 2 + 6, y);
            }

            ScatterProps(grid, 3);
            BuildEntryExitVisuals(grid);
        }

        /// <summary>
        /// "Rust Pit" — massive central pit divides the map into north and south halves.
        /// Two narrow bridges are the only crossing points — ultimate chokepoints.
        /// Entries from both sides force split defense.
        /// </summary>
        public static void BuildRustPit(VineGrid grid)
        {
            int w = grid.Width, h = grid.Height;
            int pitY = h / 2;
            grid.GenerateHeightmap(TerrainProfile.Valley, new List<HeightOverride> {
                new(0, 0, 2, h, 0f), new(w - 3, 0, w, h, 0f),
                new(w / 2 - 2, 3, w / 2 + 2, 5, 0f), // north spire area
            });

            // Entry: West-North + West-South (active), East (dormant)
            grid.SetEntryRegion(0, 2, 0, pitY - 3);
            grid.SetEntryRegion(0, pitY + 3, 0, h - 3);
            grid.SetEntryRegion(w - 1, 2, w - 1, h - 3); // dormant
            grid.SetExit(w / 2, 4); // Spire in the north — south half must cross bridges

            // Massive pit — full width, 4 cells tall
            for (int x = 2; x < w - 2; x++)
                for (int y = pitY - 1; y <= pitY + 2; y++)
                    grid.SetPit(x, y);

            // Two bridges (clear pits at bridge locations)
            int bridge1 = w / 4, bridge2 = 3 * w / 4;
            for (int y = pitY - 1; y <= pitY + 2; y++)
            {
                for (int bx = bridge1 - 1; bx <= bridge1 + 1; bx++)
                    grid.ClearCell(bx, y);
                for (int bx = bridge2 - 1; bx <= bridge2 + 1; bx++)
                    grid.ClearCell(bx, y);
            }

            // DataStreams on bridges (enemies rush across)
            for (int y = pitY - 1; y <= pitY + 2; y++)
            {
                grid.SetDataStream(bridge1, y);
                grid.SetDataStream(bridge2, y);
            }

            // Elevated platforms overlooking bridges (premium tower spots)
            for (int x = bridge1 - 3; x <= bridge1 - 1; x++) grid.SetElevated(x, pitY - 3);
            for (int x = bridge1 + 1; x <= bridge1 + 3; x++) grid.SetElevated(x, pitY - 3);
            for (int x = bridge2 - 3; x <= bridge2 - 1; x++) grid.SetElevated(x, pitY + 4);
            for (int x = bridge2 + 1; x <= bridge2 + 3; x++) grid.SetElevated(x, pitY + 4);

            // Walls creating corridors in each half
            for (int x = 5; x <= 8; x++) SetWall(grid, x, 3);
            for (int x = 5; x <= 8; x++) SetWall(grid, x, h - 4);
            for (int x = w - 9; x <= w - 6; x++) SetWall(grid, x, 3);
            for (int x = w - 9; x <= w - 6; x++) SetWall(grid, x, h - 4);

            // Lava hazards at bridge approaches
            grid.SetHazardCell(bridge1 - 2, pitY - 2, HazardType.Lava);
            grid.SetHazardCell(bridge1 + 2, pitY + 3, HazardType.Lava);
            grid.SetHazardCell(bridge2 - 2, pitY + 3, HazardType.Lava);
            grid.SetHazardCell(bridge2 + 2, pitY - 2, HazardType.Lava);

            // Resource nodes in south half (must cross to benefit)
            grid.SetResourceNodeCell(w / 2, h - 4);
            grid.SetResourceNodeCell(w / 4, h - 5);

            ScatterProps(grid, 2);
            BuildEntryExitVisuals(grid);
        }

        /// <summary>
        /// "Foundry" — acid pools everywhere creating narrow safe corridors.
        /// Resource nodes sit IN the hazard zones — capturing them means building
        /// where your towers take chip damage. Risk vs reward.
        /// </summary>
        public static void BuildFoundry(VineGrid grid)
        {
            int w = grid.Width, h = grid.Height;
            grid.GenerateHeightmap(TerrainProfile.Gentle, new List<HeightOverride> {
                new(0, 0, 2, h, 0f), new(w / 2 - 3, h / 2 - 3, w / 2 + 3, h / 2 + 3, 0f)
            });

            // 2 entries: West (active), East (dormant)
            grid.SetEntryRegion(0, h / 3, 0, 2 * h / 3);
            grid.SetEntryRegion(w - 1, h / 3, w - 1, 2 * h / 3);
            grid.SetExit(w / 2, h / 2);

            // Large acid pools — 4 pools creating corridors between them
            int[][] acidPools = {
                new[]{6, 2, 12, 6},          // NW pool
                new[]{6, h - 7, 12, h - 3},  // SW pool
                new[]{w - 13, 3, w - 7, 7},  // NE pool
                new[]{w - 13, h - 8, w - 7, h - 4}, // SE pool
                new[]{w / 2 - 1, 2, w / 2 + 1, 5},  // North center
                new[]{w / 2 - 1, h - 6, w / 2 + 1, h - 3}, // South center
            };
            foreach (var pool in acidPools)
                for (int x = pool[0]; x <= pool[2]; x++)
                    for (int y = pool[1]; y <= pool[3]; y++)
                        if (grid.InBounds(x, y) && grid.GetCell(x, y) == VineCellType.Empty)
                            grid.SetHazardCell(x, y, HazardType.Acid);

            // Narrow safe corridors between pools — place channels for visual guidance
            for (int x = 3; x < w - 3; x++)
                if (grid.GetCell(x, h / 2) == VineCellType.Empty)
                    grid.SetChannel(x, h / 2);

            // Elevated safe havens above the acid
            grid.SetElevated(9, h / 2 - 2); grid.SetElevated(10, h / 2 - 2);
            grid.SetElevated(9, h / 2 + 2); grid.SetElevated(10, h / 2 + 2);
            grid.SetElevated(w - 11, h / 2 - 2); grid.SetElevated(w - 10, h / 2 - 2);
            grid.SetElevated(w - 11, h / 2 + 2); grid.SetElevated(w - 10, h / 2 + 2);

            // Resource nodes IN the hazard zones (near acid but not on acid)
            // Player must build adjacent to acid to capture these — towers take chip damage
            grid.SetResourceNodeCell(9, 4);      // Inside NW acid zone
            grid.SetResourceNodeCell(w - 10, 5);  // Inside NE acid zone
            grid.SetResourceNodeCell(9, h - 5);   // Inside SW acid zone
            grid.SetResourceNodeCell(w - 10, h - 6); // Inside SE acid zone

            // Walls along map edges
            for (int y = 0; y < 2; y++) for (int x = 2; x < w - 2; x++) SetWall(grid, x, y);
            for (int y = h - 2; y < h; y++) for (int x = 2; x < w - 2; x++) SetWall(grid, x, y);

            // Destructible walls blocking a shortcut through center
            grid.SetDestructibleWall(w / 2 - 4, h / 2);
            grid.SetDestructibleWallVisual(w / 2 - 4, h / 2);
            grid.SetDestructibleWall(w / 2 + 4, h / 2);
            grid.SetDestructibleWallVisual(w / 2 + 4, h / 2);

            ScatterProps(grid, 2);
            BuildEntryExitVisuals(grid);
        }

        /// <summary>
        /// "Smelter" — asymmetric map. West half is dense walls and corridors (safe).
        /// East half starts walled off as an expansion zone — revealed at wave 8
        /// as a wide open danger zone with lava hazards and multiple entries.
        /// Forces players to rebuild their defense when the map doubles in size.
        /// </summary>
        public static void BuildSmelter(VineGrid grid)
        {
            int w = grid.Width, h = grid.Height;
            int splitX = w / 2 + 2; // Expansion boundary
            grid.GenerateHeightmap(TerrainProfile.Complex, new List<HeightOverride> {
                new(0, 0, 2, h, 0f), new(splitX - 3, h / 2 - 2, splitX, h / 2 + 2, 0f)
            });

            // Entry: West (active), East-North + East-South inside expansion (dormant)
            grid.SetEntryRegion(0, h / 3, 0, 2 * h / 3);
            grid.SetEntryRegion(w - 1, 2, w - 1, h / 3);       // dormant — in expansion zone
            grid.SetEntryRegion(w - 1, 2 * h / 3, w - 1, h - 3); // dormant — in expansion zone
            grid.SetExit(splitX - 5, h / 2); // Spire in the west half

            // West half: dense corridors
            for (int y = 3; y < h - 3; y += 3)
                for (int x = 4; x < splitX - 5; x += 2)
                    if (grid.GetCell(x, y) == VineCellType.Empty && !(Mathf.Abs(x - (splitX - 5)) < 3 && Mathf.Abs(y - h / 2) < 3))
                        SetWall(grid, x, y);

            // Channels through west half corridors
            for (int x = 3; x < splitX - 3; x++)
                if (grid.GetCell(x, h / 2) == VineCellType.Empty)
                    grid.SetChannel(x, h / 2);

            // Elevated positions in west half
            for (int x = 3; x <= 5; x++) for (int y = 3; y <= 4; y++) grid.SetElevated(x, y);
            for (int x = 3; x <= 5; x++) for (int y = h - 5; y <= h - 4; y++) grid.SetElevated(x, y);

            // East half: wall off entirely (expansion zone handles this)
            // The expansion zone will clear all walls from splitX to w-2
            // when it reveals, leaving an open danger zone.
            for (int x = splitX; x < w - 1; x++)
                for (int y = 1; y < h - 1; y++)
                    if (grid.GetCell(x, y) == VineCellType.Empty)
                        SetWall(grid, x, y);

            // Pre-place features that will appear when expansion reveals:
            // (These are walls right now, they'll be cleared by the expansion zone,
            //  then the features get placed on top)
            // We handle this through the ExpansionZone.Features mechanism in JSON maps.
            // For hardcoded maps, we manually place hazards after the walls — they'll
            // be overwritten by SetWall above, so we need a different approach.
            // Instead, mark the boundary with lava as a warning.
            for (int y = 2; y < h - 2; y++)
                if (y % 3 == 0)
                    grid.SetHazardCell(splitX - 1, y, HazardType.Lava);

            // Resource node in west half (safe income)
            grid.SetResourceNodeCell(splitX - 8, h / 2 - 4);

            // Destructible wall in the partition — Brutes can breach early
            grid.SetDestructibleWall(splitX - 1, h / 2);
            grid.SetDestructibleWallVisual(splitX - 1, h / 2);
            grid.SetDestructibleWall(splitX - 1, h / 2 - 1);
            grid.SetDestructibleWallVisual(splitX - 1, h / 2 - 1);

            ScatterProps(grid, 2);
            BuildEntryExitVisuals(grid);
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
