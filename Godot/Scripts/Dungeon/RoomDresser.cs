using Godot;
using System.Collections.Generic;

namespace JunkbotArena
{

    /// <summary>
    /// Places thematic prop models per room type using ModelLibrary assets.
    /// Called from RoomBuilder.BuildRoom() after existing decoration methods.
    /// </summary>
    public static class RoomDresser
    {
        // Per-room-type prop sets (IDs resolve via ModelLibrary aliases to POLYGON prefabs)
        private static readonly Dictionary<RoomType, string[]> CornerProps = new()
        {
            { RoomType.Combat, new[] { "barrel", "crate", "sm_prop_crate_wood_01", "sm_prop_barrel_large_01", "sm_prop_tech_pipe_02", "weapon_rack" } },
            { RoomType.Boss, new[] { "statue", "sm_env_statue_02", "sm_env_obelisk_01", "sm_prop_brazier_01", "sm_prop_tech_crystal_01", "pedestal" } },
            { RoomType.Shop, new[] { "shelf_tall", "sm_prop_bookcase_02", "sm_prop_bookcase_03", "computer_small" } },
            { RoomType.Treasure, new[] { "statue", "vessel_tall", "sm_prop_chest_02", "sm_prop_chest_03", "sm_prop_vase_05", "pedestal" } },
            { RoomType.Event, new[] { "pod", "capsule", "sm_prop_tech_engine_01", "sm_prop_cauldron_01", "teleporter" } },
            { RoomType.Entrance, new[] { "portal", "sm_prop_tech_turbine_01", "sm_prop_bonfire_01" } },
            { RoomType.SafeRoom, new[] { "shelf_tall", "computer", "charging_cables", "coupling" } },
        };

        private static readonly Dictionary<RoomType, string[]> WallProps = new()
        {
            { RoomType.Combat, new[] { "computer", "pipes", "crate_long", "sm_prop_crate_metal_02", "sm_prop_tech_pole_01", "sm_prop_chain_01", "weapon_rack" } },
            { RoomType.Boss, new[] { "laser", "computer", "sm_prop_wall_banner_01", "sm_prop_wall_banner_02", "sm_prop_chain_05", "statue" } },
            { RoomType.Shop, new[] { "shelf_tall", "computer_small", "sm_prop_bookcase_01", "sm_prop_bookcase_02", "sm_prop_lantern_01" } },
            { RoomType.Treasure, new[] { "chest", "vessel", "vessel_short", "sm_prop_chest_04", "sm_prop_vase_06", "sm_prop_gem_01" } },
            { RoomType.Event, new[] { "teleporter", "computer", "sm_prop_tech_switchboard_01", "sm_prop_tech_lever_01", "sm_prop_tech_pipe_01" } },
            { RoomType.Entrance, new[] { "sm_prop_torchstick_01", "sm_prop_torch_ornate_01", "sm_prop_wall_banner_03" } },
            { RoomType.SafeRoom, new[] { "shelf_tall", "pipes", "charging_cables_2", "sm_prop_lantern_01" } },
        };

        private static readonly Dictionary<RoomType, string[]> FloorProps = new()
        {
            { RoomType.Combat, new[] { "barrel", "crate", "sm_prop_barrel_broken_01", "sm_prop_crate_wood_02", "sm_prop_brick_01" } },
            { RoomType.Boss, new[] { "sm_prop_rug_01", "sm_prop_rug_02", "sm_prop_bonfire_01", "sm_prop_brazier_01" } },
            { RoomType.Shop, new[] { "sm_prop_rug_03", "sm_prop_stool_01", "sm_prop_table_01" } },
            { RoomType.Treasure, new[] { "vessel", "chest", "sm_prop_gem_02", "sm_prop_jewel_01", "sm_prop_vase_03" } },
            { RoomType.Event, new[] { "capsule", "sm_prop_tech_cog_01", "sm_prop_tech_conveyor_01" } },
        };

        // Sector-specific environmental props (KitBash3D assets) — layered on top of room-type dressing
        private static readonly Dictionary<int, string[]> SectorProps = new()
        {
            { 1, new[] { "kb_generator", "kb_generator_2", "kb_barrel", "kb_barrels", "kb_crate", "kb_crate_2",
                         "kb_crate_stack", "kb_pallet", "kb_pallet_stack", "kb_hvac", "kb_hvac_2",
                         "kb_pipes", "kb_power_mast", "kb_lamp_post", "kb_container" } },
            { 2, new[] { "kb_iso_tank", "kb_iso_tank_2", "kb_air_filter", "kb_air_filter_2",
                         "kb_trash_bag", "kb_trash_bags", "kb_tires", "kb_tires_2",
                         "kb_container", "kb_container_2", "kb_covered_body", "kb_barrel" } },
            { 3, new[] { "kb_barrier", "kb_barrier_2", "kb_barrier_3", "kb_sandbags", "kb_sandbags_2",
                         "kb_sandbags_3", "kb_fence", "kb_fence_2", "kb_hedgehog", "kb_flag_pole",
                         "kb_sign", "kb_sign_2", "kb_crate", "kb_spotlight" } },
            { 4, new[] { "kb_radar", "kb_radar_2", "kb_mobile_radar", "kb_antenna", "kb_antenna_2",
                         "kb_satellite", "kb_security_cam", "kb_security_term", "kb_solar_panel",
                         "kb_surveillance", "kb_table", "kb_table_2" } },
            { 5, new[] { "kb_drop_pod", "kb_drop_pod_2", "kb_armory", "kb_armory_station",
                         "kb_corridor", "kb_platform", "kb_crashed_rocket", "kb_robot_observer",
                         "kb_elevator", "kb_pillar", "kb_bridge", "kb_guard_rail" } },
        };

        /// <summary>
        /// Place thematic props in a room based on its type.
        /// </summary>
        public static void DressRoom(Node3D room, Vector2 size, RoomType type,
            bool doorN, bool doorS, bool doorE, bool doorW, int sectorNumber = 0)
        {
            var rng = new RandomNumberGenerator();
            rng.Randomize();

            float halfW = size.X / 2f;
            float halfH = size.Y / 2f;
            float doorClearance = 6f;
            float minSpacing = 3f;

            var placedPositions = new List<Vector3>();

            // Corner props (4 positions)
            if (CornerProps.TryGetValue(type, out var corners))
            {
                float inset = 2.5f;
                Vector3[] cornerPositions =
                {
                    new(-halfW + inset, 0, -halfH + inset),
                    new(halfW - inset, 0, -halfH + inset),
                    new(-halfW + inset, 0, halfH - inset),
                    new(halfW - inset, 0, halfH - inset),
                };

                foreach (var pos in cornerPositions)
                {
                    if (rng.Randf() > 0.6f) continue; // 60% chance per corner

                    string propId = corners[rng.RandiRange(0, corners.Length - 1)];
                    var model = ModelLibrary.TryLoad("prop", propId);
                    if (model == null) continue;

                    model.Name = $"Prop_Corner_{propId}";
                    RoomBuilder.ScaleModelToFitEffective(model, rng.RandfRange(0.8f, 1.2f));
                    model.Position = pos;
                    model.RotateY(rng.RandfRange(0, Mathf.Tau));
                    room.AddChild(model);
                    RoomBuilder.GroundModel(model);
                    placedPositions.Add(pos);
                }
            }

            // Wall-adjacent props (along perimeter, 2-unit inset)
            if (WallProps.TryGetValue(type, out var wallPropIds))
            {
                float wallInset = 2f;
                int wallPropCount = rng.RandiRange(2, 4);

                for (int i = 0; i < wallPropCount; i++)
                {
                    // Pick a random wall position
                    int wall = rng.RandiRange(0, 3);
                    float x, z;

                    switch (wall)
                    {
                        case 0: // North
                            if (doorN) continue;
                            x = rng.RandfRange(-halfW * 0.7f, halfW * 0.7f);
                            z = -halfH + wallInset;
                            break;
                        case 1: // South
                            if (doorS) continue;
                            x = rng.RandfRange(-halfW * 0.7f, halfW * 0.7f);
                            z = halfH - wallInset;
                            break;
                        case 2: // East
                            if (doorE) continue;
                            x = halfW - wallInset;
                            z = rng.RandfRange(-halfH * 0.7f, halfH * 0.7f);
                            break;
                        default: // West
                            if (doorW) continue;
                            x = -halfW + wallInset;
                            z = rng.RandfRange(-halfH * 0.7f, halfH * 0.7f);
                            break;
                    }

                    var pos = new Vector3(x, 0, z);

                    // Check door zone clearance
                    if (IsNearDoor(pos, halfW, halfH, doorClearance, doorN, doorS, doorE, doorW))
                        continue;

                    // Min spacing check
                    if (IsTooClose(pos, placedPositions, minSpacing))
                        continue;

                    string propId = wallPropIds[rng.RandiRange(0, wallPropIds.Length - 1)];
                    var model = ModelLibrary.TryLoad("prop", propId);
                    if (model == null) continue;

                    model.Name = $"Prop_Wall_{propId}";
                    RoomBuilder.ScaleModelToFitEffective(model, rng.RandfRange(0.8f, 1.4f));
                    model.Position = pos;
                    model.RotateY(rng.RandfRange(0, Mathf.Tau));
                    room.AddChild(model);
                    RoomBuilder.GroundModel(model);
                    placedPositions.Add(pos);
                }
            }

            // Floor scatter props (inner area, avoiding center + doors)
            if (FloorProps.TryGetValue(type, out var floorPropIds))
            {
                int floorPropCount = rng.RandiRange(1, 3);

                for (int i = 0; i < floorPropCount; i++)
                {
                    float x = rng.RandfRange(-halfW * 0.5f, halfW * 0.5f);
                    float z = rng.RandfRange(-halfH * 0.5f, halfH * 0.5f);

                    // Keep center clear
                    if (Mathf.Abs(x) < 3f && Mathf.Abs(z) < 3f)
                        continue;

                    var pos = new Vector3(x, 0, z);

                    if (IsNearDoor(pos, halfW, halfH, doorClearance, doorN, doorS, doorE, doorW))
                        continue;

                    if (IsTooClose(pos, placedPositions, minSpacing))
                        continue;

                    string propId = floorPropIds[rng.RandiRange(0, floorPropIds.Length - 1)];
                    var model = ModelLibrary.TryLoad("prop", propId);
                    if (model == null) continue;

                    model.Name = $"Prop_Floor_{propId}";
                    RoomBuilder.ScaleModelToFitEffective(model, rng.RandfRange(0.4f, 0.8f));
                    model.Position = pos;
                    model.RotateY(rng.RandfRange(0, Mathf.Tau));
                    room.AddChild(model);
                    RoomBuilder.GroundModel(model);
                    placedPositions.Add(pos);
                }
            }

            // Sector-specific environmental dressing
            if (sectorNumber > 0 && SectorProps.TryGetValue(sectorNumber, out var sectorPropIds))
            {
                int sectorPropCount = rng.RandiRange(2, 4);
                for (int i = 0; i < sectorPropCount; i++)
                {
                    // Place along walls and corners (mix of wall-adjacent and corner positions)
                    float x, z;
                    if (rng.Randf() > 0.5f)
                    {
                        // Wall-adjacent
                        int wall = rng.RandiRange(0, 3);
                        float wallInset = 2.5f;
                        switch (wall)
                        {
                            case 0: x = rng.RandfRange(-halfW * 0.7f, halfW * 0.7f); z = -halfH + wallInset; break;
                            case 1: x = rng.RandfRange(-halfW * 0.7f, halfW * 0.7f); z = halfH - wallInset; break;
                            case 2: x = halfW - wallInset; z = rng.RandfRange(-halfH * 0.7f, halfH * 0.7f); break;
                            default: x = -halfW + wallInset; z = rng.RandfRange(-halfH * 0.7f, halfH * 0.7f); break;
                        }
                    }
                    else
                    {
                        // Scattered in room
                        x = rng.RandfRange(-halfW * 0.6f, halfW * 0.6f);
                        z = rng.RandfRange(-halfH * 0.6f, halfH * 0.6f);
                    }

                    var pos = new Vector3(x, 0, z);
                    if (Mathf.Abs(x) < 3f && Mathf.Abs(z) < 3f) continue;
                    if (IsNearDoor(pos, halfW, halfH, doorClearance, doorN, doorS, doorE, doorW)) continue;
                    if (IsTooClose(pos, placedPositions, minSpacing)) continue;

                    string propId = sectorPropIds[rng.RandiRange(0, sectorPropIds.Length - 1)];
                    var model = ModelLibrary.TryLoad("prop", propId);
                    if (model == null) continue;

                    model.Name = $"Prop_Sector_{propId}";
                    RoomBuilder.ScaleModelToFitEffective(model, rng.RandfRange(0.6f, 1.0f));
                    model.Position = pos;
                    model.RotateY(rng.RandfRange(0, Mathf.Tau));
                    room.AddChild(model);
                    RoomBuilder.GroundModel(model);
                    placedPositions.Add(pos);
                }
            }
        }

        private static bool IsNearDoor(Vector3 pos, float halfW, float halfH,
            float clearance, bool doorN, bool doorS, bool doorE, bool doorW)
        {
            // Check if position is within clearance of any door opening
            if (doorN && Mathf.Abs(pos.X) < clearance && pos.Z < -halfH + clearance)
                return true;
            if (doorS && Mathf.Abs(pos.X) < clearance && pos.Z > halfH - clearance)
                return true;
            if (doorE && pos.X > halfW - clearance && Mathf.Abs(pos.Z) < clearance)
                return true;
            if (doorW && pos.X < -halfW + clearance && Mathf.Abs(pos.Z) < clearance)
                return true;
            return false;
        }

        private static bool IsTooClose(Vector3 pos, List<Vector3> placed, float minDist)
        {
            foreach (var p in placed)
            {
                if (pos.DistanceTo(p) < minDist) return true;
            }
            return false;
        }
    }
}
