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
        // Per-room-type prop sets
        private static readonly Dictionary<RoomType, string[]> CornerProps = new()
        {
            { RoomType.Combat, new[] { "column_2", "column_3", "column_slim" } },
            { RoomType.Boss, new[] { "column_2", "column_3", "statue", "column_slim" } },
            { RoomType.Shop, new[] { "shelf_tall" } },
            { RoomType.Treasure, new[] { "statue", "vessel_tall" } },
            { RoomType.Event, new[] { "pod", "capsule" } },
            { RoomType.Entrance, new[] { "portal" } },
        };

        private static readonly Dictionary<RoomType, string[]> WallProps = new()
        {
            { RoomType.Combat, new[] { "computer", "computer_small", "pipes", "crate_long" } },
            { RoomType.Boss, new[] { "laser", "computer" } },
            { RoomType.Shop, new[] { "computer_small", "shelf_tall" } },
            { RoomType.Treasure, new[] { "chest", "vessel", "vessel_short" } },
            { RoomType.Event, new[] { "teleporter", "computer_small" } },
            { RoomType.Entrance, new[] { "computer_small" } },
        };

        private static readonly Dictionary<RoomType, string[]> FloorProps = new()
        {
            { RoomType.Combat, new[] { "barrel", "crate" } },
            { RoomType.Boss, new[] { "barrel", "crate" } },
            { RoomType.Shop, new[] { "pedestal" } },
            { RoomType.Treasure, new[] { "vessel", "vessel_short", "chest" } },
            { RoomType.Event, new[] { "capsule" } },
        };

        /// <summary>
        /// Place thematic props in a room based on its type.
        /// </summary>
        public static void DressRoom(Node3D room, Vector2 size, RoomType type,
            bool doorN, bool doorS, bool doorE, bool doorW)
        {
            var rng = new RandomNumberGenerator();
            rng.Randomize();

            float halfW = size.X / 2f;
            float halfH = size.Y / 2f;
            float doorClearance = 3f;
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

                    RoomBuilder.ScaleModelToFitEffective(model, rng.RandfRange(1.5f, 2.5f));
                    model.Position = pos;
                    model.RotateY(rng.RandfRange(0, Mathf.Tau));
                    room.AddChild(model);
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

                    RoomBuilder.ScaleModelToFitEffective(model, rng.RandfRange(0.8f, 1.4f));
                    model.Position = pos;
                    model.RotateY(rng.RandfRange(0, Mathf.Tau));
                    room.AddChild(model);
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

                    RoomBuilder.ScaleModelToFitEffective(model, rng.RandfRange(0.4f, 0.8f));
                    model.Position = pos;
                    model.RotateY(rng.RandfRange(0, Mathf.Tau));
                    room.AddChild(model);
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
