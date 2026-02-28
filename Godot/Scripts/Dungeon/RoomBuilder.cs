using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Builds procedural room geometry: tile floors, walls with trim, door frames,
    /// wall torches, and room-type decorations.
    /// </summary>
    public static class RoomBuilder
    {
        /// <summary>
        /// Create a room Node3D with floor and walls at the given position.
        /// </summary>
        public static Node3D BuildRoom(Vector3 position, Vector2 size, RoomType type,
            bool doorNorth = false, bool doorSouth = false, bool doorEast = false, bool doorWest = false)
        {
            var room = new Node3D();
            room.Position = position;

            float halfW = size.X / 2f;
            float halfH = size.Y / 2f;

            // Tile floor
            var floor = new StaticBody3D();
            floor.CollisionLayer = Constants.MASK_GROUND;
            room.AddChild(floor);

            BuildTileFloor(floor, size, type);

            var floorShape = new CollisionShape3D();
            var box = new BoxShape3D();
            box.Size = new Vector3(size.X, 0.1f, size.Y);
            floorShape.Shape = box;
            floorShape.Position = new Vector3(0, -0.05f, 0);
            floor.AddChild(floorShape);

            // Walls
            float wallHeight = 3f;
            float wallThickness = 0.5f;
            float doorWidth = 3f;

            // North wall (negative Z)
            if (!doorNorth)
                BuildWall(room, new Vector3(0, wallHeight / 2f, -halfH), new Vector3(size.X, wallHeight, wallThickness), type);
            else
            {
                BuildWallWithDoor(room, new Vector3(0, wallHeight / 2f, -halfH), size.X, wallHeight, wallThickness, doorWidth, type);
                BuildDoorFrame(room, new Vector3(0, 0, -halfH), wallHeight, doorWidth, wallThickness, type);
            }

            // South wall (positive Z)
            if (!doorSouth)
                BuildWall(room, new Vector3(0, wallHeight / 2f, halfH), new Vector3(size.X, wallHeight, wallThickness), type);
            else
            {
                BuildWallWithDoor(room, new Vector3(0, wallHeight / 2f, halfH), size.X, wallHeight, wallThickness, doorWidth, type);
                BuildDoorFrame(room, new Vector3(0, 0, halfH), wallHeight, doorWidth, wallThickness, type);
            }

            // East wall (positive X)
            if (!doorEast)
                BuildWall(room, new Vector3(halfW, wallHeight / 2f, 0), new Vector3(wallThickness, wallHeight, size.Y), type);
            else
            {
                BuildWallWithDoorZ(room, new Vector3(halfW, wallHeight / 2f, 0), size.Y, wallHeight, wallThickness, doorWidth, type);
                BuildDoorFrameZ(room, new Vector3(halfW, 0, 0), wallHeight, doorWidth, wallThickness, type);
            }

            // West wall (negative X)
            if (!doorWest)
                BuildWall(room, new Vector3(-halfW, wallHeight / 2f, 0), new Vector3(wallThickness, wallHeight, size.Y), type);
            else
            {
                BuildWallWithDoorZ(room, new Vector3(-halfW, wallHeight / 2f, 0), size.Y, wallHeight, wallThickness, doorWidth, type);
                BuildDoorFrameZ(room, new Vector3(-halfW, 0, 0), wallHeight, doorWidth, wallThickness, type);
            }

            // Wall trim (baseboard + crown)
            AddWallTrim(room, size, wallHeight, type);

            // Torches
            AddWallTorches(room, size, wallHeight, type);

            // Room decorations
            AddRoomDecorations(room, size, type);

            // Navigation mesh for pathfinding
            AddNavRegion(room, size);

            // Spawn point marker
            var spawnMarker = new Marker3D();
            spawnMarker.Name = "SpawnPoint";
            spawnMarker.Position = new Vector3(0, 0.9f, 0);
            room.AddChild(spawnMarker);

            return room;
        }

        /// <summary>
        /// Build a corridor connecting two rooms.
        /// </summary>
        public static Node3D BuildCorridor(Vector3 from, Vector3 to,
            float fromHalfExtent, float toHalfExtent, float width = 3f)
        {
            var dir = (to - from).Normalized();
            bool isXAxis = Mathf.Abs(dir.X) > Mathf.Abs(dir.Z);

            var gapStart = from + dir * fromHalfExtent;
            var gapEnd = to - dir * toHalfExtent;
            float gapLength = gapStart.DistanceTo(gapEnd);

            if (gapLength < 0.5f) return new Node3D();

            var corridor = new Node3D();
            corridor.Position = (gapStart + gapEnd) / 2f;

            float wallHeight = 3f;
            float wallThickness = 0.3f;
            float halfW = width / 2f;

            // Floor with center path strip
            var floor = new StaticBody3D();
            floor.CollisionLayer = Constants.MASK_GROUND;
            corridor.AddChild(floor);

            BuildCorridorFloor(floor, gapLength, width, isXAxis);

            var floorShape = new CollisionShape3D();
            var box = new BoxShape3D();
            box.Size = isXAxis ? new Vector3(gapLength, 0.1f, width) : new Vector3(width, 0.1f, gapLength);
            floorShape.Shape = box;
            floorShape.Position = new Vector3(0, -0.05f, 0);
            floor.AddChild(floorShape);

            // Side walls
            if (isXAxis)
            {
                BuildWall(corridor, new Vector3(0, wallHeight / 2f, -halfW),
                    new Vector3(gapLength, wallHeight, wallThickness), RoomType.Combat);
                BuildWall(corridor, new Vector3(0, wallHeight / 2f, halfW),
                    new Vector3(gapLength, wallHeight, wallThickness), RoomType.Combat);
            }
            else
            {
                BuildWall(corridor, new Vector3(-halfW, wallHeight / 2f, 0),
                    new Vector3(wallThickness, wallHeight, gapLength), RoomType.Combat);
                BuildWall(corridor, new Vector3(halfW, wallHeight / 2f, 0),
                    new Vector3(wallThickness, wallHeight, gapLength), RoomType.Combat);
            }

            // Corridor sconces
            AddCorridorSconces(corridor, gapLength, width, wallHeight, isXAxis);

            // Navigation mesh for corridor
            var corridorSize = isXAxis ? new Vector2(gapLength, width) : new Vector2(width, gapLength);
            AddNavRegion(corridor, corridorSize);

            return corridor;
        }

        // ── Tile Floor ──

        private static void BuildTileFloor(Node3D parent, Vector2 size, RoomType type)
        {
            Color baseColor = GetFloorColor(type);
            Color altColor = baseColor.Lightened(0.08f);
            bool hasFloorModel = ModelLibrary.HasModel("floor", "floor_tile");

            float tileSize = 1.8f;
            float gap = 0.2f;
            float step = tileSize + gap;

            int tilesX = Mathf.Max(1, (int)(size.X / step));
            int tilesZ = Mathf.Max(1, (int)(size.Y / step));

            float startX = -(tilesX - 1) * step / 2f;
            float startZ = -(tilesZ - 1) * step / 2f;

            for (int x = 0; x < tilesX; x++)
            {
                for (int z = 0; z < tilesZ; z++)
                {
                    var tilePos = new Vector3(startX + x * step, -0.05f, startZ + z * step);

                    if (hasFloorModel)
                    {
                        var model = ModelLibrary.TryLoad("floor", "floor_tile");
                        if (model != null)
                        {
                            CharacterMeshBuilder.ScaleModelToFit(model, 0.1f);
                            model.Position = tilePos;
                            parent.AddChild(model);
                            continue;
                        }
                    }

                    var tile = new MeshInstance3D();
                    var boxMesh = new BoxMesh();
                    boxMesh.Size = new Vector3(tileSize, 0.1f, tileSize);
                    tile.Mesh = boxMesh;
                    tile.Position = tilePos;

                    bool isAlt = (x + z) % 2 == 0;
                    var mat = new StandardMaterial3D();
                    mat.AlbedoColor = isAlt ? baseColor : altColor;
                    tile.MaterialOverride = mat;

                    parent.AddChild(tile);
                }
            }
        }

        private static void BuildCorridorFloor(Node3D parent, float length, float width, bool isXAxis)
        {
            Color baseColor = new Color(0.18f, 0.16f, 0.14f);
            Color pathColor = baseColor.Lightened(0.1f);

            // Main floor
            var floorMesh = new MeshInstance3D();
            var planeMesh = new PlaneMesh();
            planeMesh.Size = isXAxis ? new Vector2(length, width) : new Vector2(width, length);
            floorMesh.Mesh = planeMesh;
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = baseColor;
            floorMesh.MaterialOverride = mat;
            parent.AddChild(floorMesh);

            // Center path strip
            float stripWidth = 0.8f;
            var strip = new MeshInstance3D();
            var stripMesh = new BoxMesh();
            stripMesh.Size = isXAxis
                ? new Vector3(length * 0.9f, 0.02f, stripWidth)
                : new Vector3(stripWidth, 0.02f, length * 0.9f);
            strip.Mesh = stripMesh;
            strip.Position = new Vector3(0, 0.01f, 0);
            var stripMat = new StandardMaterial3D();
            stripMat.AlbedoColor = pathColor;
            strip.MaterialOverride = stripMat;
            parent.AddChild(strip);
        }

        // ── Walls ──

        private static void BuildWall(Node3D parent, Vector3 pos, Vector3 size, RoomType type)
        {
            var wall = new StaticBody3D();
            wall.Position = pos;
            wall.CollisionLayer = 1;
            parent.AddChild(wall);

            // Try model wall segment
            var model = ModelLibrary.TryLoad("wall", "wall_segment");
            if (model != null)
            {
                CharacterMeshBuilder.ScaleModelToFit(model, size.Y);
                wall.AddChild(model);
            }
            else
            {
                var mesh = new MeshInstance3D();
                var boxMesh = new BoxMesh();
                boxMesh.Size = size;
                mesh.Mesh = boxMesh;

                var mat = new StandardMaterial3D();
                mat.AlbedoColor = GetWallColor(type);
                mesh.MaterialOverride = mat;
                wall.AddChild(mesh);
            }

            var shape = new CollisionShape3D();
            var box = new BoxShape3D();
            box.Size = size;
            shape.Shape = box;
            wall.AddChild(shape);
        }

        private static void BuildWallWithDoor(Node3D parent, Vector3 center, float wallWidth,
            float wallHeight, float wallThickness, float doorWidth, RoomType type)
        {
            float sideWidth = (wallWidth - doorWidth) / 2f;
            if (sideWidth > 0.1f)
            {
                BuildWall(parent, center + new Vector3(-(doorWidth / 2f + sideWidth / 2f), 0, 0),
                    new Vector3(sideWidth, wallHeight, wallThickness), type);
                BuildWall(parent, center + new Vector3(doorWidth / 2f + sideWidth / 2f, 0, 0),
                    new Vector3(sideWidth, wallHeight, wallThickness), type);
            }
        }

        private static void BuildWallWithDoorZ(Node3D parent, Vector3 center, float wallLength,
            float wallHeight, float wallThickness, float doorWidth, RoomType type)
        {
            float sideLength = (wallLength - doorWidth) / 2f;
            if (sideLength > 0.1f)
            {
                BuildWall(parent, center + new Vector3(0, 0, -(doorWidth / 2f + sideLength / 2f)),
                    new Vector3(wallThickness, wallHeight, sideLength), type);
                BuildWall(parent, center + new Vector3(0, 0, doorWidth / 2f + sideLength / 2f),
                    new Vector3(wallThickness, wallHeight, sideLength), type);
            }
        }

        // ── Door Frames ──

        private static void BuildDoorFrame(Node3D parent, Vector3 doorCenter, float wallHeight, float doorWidth, float wallThickness, RoomType type)
        {
            // Try model door frame
            var model = ModelLibrary.TryLoad("door", "door_frame");
            if (model != null)
            {
                CharacterMeshBuilder.ScaleModelToFit(model, wallHeight);
                model.Position = doorCenter + new Vector3(0, wallHeight / 2f, 0);
                parent.AddChild(model);
                return;
            }

            Color frameColor = GetWallColor(type).Lightened(0.15f);
            float pillarSize = 0.25f;

            // Left pillar
            AddDecorMesh(parent, new BoxMesh { Size = new Vector3(pillarSize, wallHeight, pillarSize) },
                frameColor, doorCenter + new Vector3(-doorWidth / 2f, wallHeight / 2f, 0));

            // Right pillar
            AddDecorMesh(parent, new BoxMesh { Size = new Vector3(pillarSize, wallHeight, pillarSize) },
                frameColor, doorCenter + new Vector3(doorWidth / 2f, wallHeight / 2f, 0));

            // Lintel
            AddDecorMesh(parent, new BoxMesh { Size = new Vector3(doorWidth + pillarSize * 2, 0.2f, pillarSize) },
                frameColor.Lightened(0.05f), doorCenter + new Vector3(0, wallHeight, 0));
        }

        private static void BuildDoorFrameZ(Node3D parent, Vector3 doorCenter, float wallHeight, float doorWidth, float wallThickness, RoomType type)
        {
            // Try model door frame (rotated 90 degrees for Z-axis doors)
            var model = ModelLibrary.TryLoad("door", "door_frame");
            if (model != null)
            {
                CharacterMeshBuilder.ScaleModelToFit(model, wallHeight);
                model.Position = doorCenter + new Vector3(0, wallHeight / 2f, 0);
                model.RotateY(Mathf.DegToRad(90));
                parent.AddChild(model);
                return;
            }

            Color frameColor = GetWallColor(type).Lightened(0.15f);
            float pillarSize = 0.25f;

            AddDecorMesh(parent, new BoxMesh { Size = new Vector3(pillarSize, wallHeight, pillarSize) },
                frameColor, doorCenter + new Vector3(0, wallHeight / 2f, -doorWidth / 2f));

            AddDecorMesh(parent, new BoxMesh { Size = new Vector3(pillarSize, wallHeight, pillarSize) },
                frameColor, doorCenter + new Vector3(0, wallHeight / 2f, doorWidth / 2f));

            AddDecorMesh(parent, new BoxMesh { Size = new Vector3(pillarSize, 0.2f, doorWidth + pillarSize * 2) },
                frameColor.Lightened(0.05f), doorCenter + new Vector3(0, wallHeight, 0));
        }

        // ── Wall Trim ──

        private static void AddWallTrim(Node3D parent, Vector2 size, float wallHeight, RoomType type)
        {
            float halfW = size.X / 2f;
            float halfH = size.Y / 2f;
            Color baseboardColor = GetWallColor(type).Darkened(0.2f);
            Color crownColor = GetWallColor(type).Lightened(0.1f);
            float baseH = 0.15f;
            float crownH = 0.1f;

            // Baseboard strips (4 walls)
            AddDecorMesh(parent, new BoxMesh { Size = new Vector3(size.X, baseH, 0.08f) },
                baseboardColor, new Vector3(0, baseH / 2f, -halfH + 0.25f));
            AddDecorMesh(parent, new BoxMesh { Size = new Vector3(size.X, baseH, 0.08f) },
                baseboardColor, new Vector3(0, baseH / 2f, halfH - 0.25f));
            AddDecorMesh(parent, new BoxMesh { Size = new Vector3(0.08f, baseH, size.Y) },
                baseboardColor, new Vector3(-halfW + 0.25f, baseH / 2f, 0));
            AddDecorMesh(parent, new BoxMesh { Size = new Vector3(0.08f, baseH, size.Y) },
                baseboardColor, new Vector3(halfW - 0.25f, baseH / 2f, 0));

            // Crown strips
            AddDecorMesh(parent, new BoxMesh { Size = new Vector3(size.X, crownH, 0.06f) },
                crownColor, new Vector3(0, wallHeight - crownH / 2f, -halfH + 0.25f));
            AddDecorMesh(parent, new BoxMesh { Size = new Vector3(size.X, crownH, 0.06f) },
                crownColor, new Vector3(0, wallHeight - crownH / 2f, halfH - 0.25f));
            AddDecorMesh(parent, new BoxMesh { Size = new Vector3(0.06f, crownH, size.Y) },
                crownColor, new Vector3(-halfW + 0.25f, wallHeight - crownH / 2f, 0));
            AddDecorMesh(parent, new BoxMesh { Size = new Vector3(0.06f, crownH, size.Y) },
                crownColor, new Vector3(halfW - 0.25f, wallHeight - crownH / 2f, 0));
        }

        // ── Wall Torches ──

        private static void AddWallTorches(Node3D parent, Vector2 size, float wallHeight, RoomType type)
        {
            float halfW = size.X / 2f;
            float halfH = size.Y / 2f;
            float spacing = 7f;
            float torchY = wallHeight * 0.65f;
            Color lightColor = GetTorchColor(type);

            // North & South walls
            int countX = Mathf.Max(1, (int)(size.X / spacing));
            float startX = -(countX - 1) * spacing / 2f;
            for (int i = 0; i < countX; i++)
            {
                float x = startX + i * spacing;
                AddTorch(parent, new Vector3(x, torchY, -halfH + 0.35f), lightColor);
                AddTorch(parent, new Vector3(x, torchY, halfH - 0.35f), lightColor);
            }

            // East & West walls
            int countZ = Mathf.Max(1, (int)(size.Y / spacing));
            float startZ = -(countZ - 1) * spacing / 2f;
            for (int i = 0; i < countZ; i++)
            {
                float z = startZ + i * spacing;
                AddTorch(parent, new Vector3(-halfW + 0.35f, torchY, z), lightColor);
                AddTorch(parent, new Vector3(halfW - 0.35f, torchY, z), lightColor);
            }
        }

        private static void AddTorch(Node3D parent, Vector3 position, Color lightColor)
        {
            // Try model torch
            var model = ModelLibrary.TryLoad("prop", "torch");
            if (model != null)
            {
                CharacterMeshBuilder.ScaleModelToFit(model, 0.4f);
                model.Position = position;
                parent.AddChild(model);
            }
            else
            {
                // Bracket
                AddDecorMesh(parent, new BoxMesh { Size = new Vector3(0.1f, 0.06f, 0.1f) },
                    new Color(0.3f, 0.25f, 0.2f), position + new Vector3(0, -0.15f, 0));

                // Torch head
                var torchMat = new StandardMaterial3D();
                torchMat.AlbedoColor = new Color(0.8f, 0.5f, 0.2f);
                torchMat.EmissionEnabled = true;
                torchMat.Emission = new Color(1f, 0.6f, 0.2f);
                torchMat.EmissionEnergyMultiplier = 1.5f;

                var torchMesh = new MeshInstance3D();
                torchMesh.Mesh = new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.06f, Height = 0.15f, RadialSegments = 6 };
                torchMesh.Position = position;
                torchMesh.MaterialOverride = torchMat;
                parent.AddChild(torchMesh);
            }

            // Light — always added regardless of model
            var light = new OmniLight3D();
            light.Position = position + Vector3.Up * 0.2f;
            light.LightColor = lightColor;
            light.LightEnergy = 1.2f;
            light.OmniRange = 6f;
            light.ShadowEnabled = false;
            parent.AddChild(light);

            // Fire particles — always added
            var fire = VfxFactory.CreateTorchFireParticles();
            fire.Position = position + Vector3.Up * 0.1f;
            parent.AddChild(fire);
        }

        private static void AddCorridorSconces(Node3D parent, float length, float width, float wallHeight, bool isXAxis)
        {
            float spacing = 5f;
            float halfW = width / 2f;
            Color lightColor = new Color(0.9f, 0.7f, 0.4f);
            float torchY = wallHeight * 0.6f;
            int count = Mathf.Max(1, (int)(length / spacing));
            float start = -(count - 1) * spacing / 2f;

            for (int i = 0; i < count; i++)
            {
                float pos = start + i * spacing;
                if (isXAxis)
                {
                    AddTorch(parent, new Vector3(pos, torchY, -halfW + 0.2f), lightColor);
                    AddTorch(parent, new Vector3(pos, torchY, halfW - 0.2f), lightColor);
                }
                else
                {
                    AddTorch(parent, new Vector3(-halfW + 0.2f, torchY, pos), lightColor);
                    AddTorch(parent, new Vector3(halfW - 0.2f, torchY, pos), lightColor);
                }
            }
        }

        // ── Room Decorations ──

        private static void AddRoomDecorations(Node3D parent, Vector2 size, RoomType type)
        {
            switch (type)
            {
                case RoomType.Combat:
                    AddCombatDecorations(parent, size);
                    break;
                case RoomType.Treasure:
                    AddTreasureDecorations(parent, size);
                    break;
                case RoomType.Boss:
                    AddBossDecorations(parent, size);
                    break;
                case RoomType.Entrance:
                    AddEntranceDecorations(parent, size);
                    break;
            }
        }

        private static void AddCombatDecorations(Node3D parent, Vector2 size)
        {
            float halfW = size.X / 2f;
            float halfH = size.Y / 2f;
            var rng = new RandomNumberGenerator();
            rng.Randomize();

            // 2-4 debris — try barrel/crate models first
            int debrisCount = rng.RandiRange(2, 4);
            for (int i = 0; i < debrisCount; i++)
            {
                float x = rng.RandfRange(-halfW * 0.6f, halfW * 0.6f);
                float z = rng.RandfRange(-halfH * 0.6f, halfH * 0.6f);

                string propId = i % 2 == 0 ? "barrel" : "crate";
                var model = ModelLibrary.TryLoad("prop", propId);
                if (model != null)
                {
                    CharacterMeshBuilder.ScaleModelToFit(model, rng.RandfRange(0.3f, 0.6f));
                    model.Position = new Vector3(x, 0, z);
                    model.RotateY(rng.RandfRange(0, Mathf.Tau));
                    parent.AddChild(model);
                }
                else
                {
                    float s = rng.RandfRange(0.15f, 0.35f);
                    var debris = AddDecorMesh(parent, new BoxMesh { Size = new Vector3(s, s * 0.7f, s) },
                        new Color(0.25f, 0.23f, 0.2f), new Vector3(x, s * 0.35f, z));
                    debris.RotateY(rng.RandfRange(0, Mathf.Tau));
                    debris.RotateX(rng.RandfRange(-0.2f, 0.2f));
                }
            }

            // Weapon rack on wall — try model first
            var rackModel = ModelLibrary.TryLoad("prop", "weapon_rack");
            if (rackModel != null)
            {
                CharacterMeshBuilder.ScaleModelToFit(rackModel, 1.2f);
                rackModel.Position = new Vector3(halfW * 0.5f, 1.2f, -halfH + 0.5f);
                parent.AddChild(rackModel);
            }
            else
            {
                AddDecorMesh(parent, new BoxMesh { Size = new Vector3(1.2f, 0.08f, 0.15f) },
                    new Color(0.35f, 0.25f, 0.15f), new Vector3(halfW * 0.5f, 1.8f, -halfH + 0.5f));
                // Crossed weapons
                var weapon1 = AddDecorMesh(parent, new BoxMesh { Size = new Vector3(0.04f, 0.6f, 0.02f) },
                    new Color(0.6f, 0.62f, 0.65f), new Vector3(halfW * 0.5f, 1.5f, -halfH + 0.45f));
                weapon1.RotateZ(Mathf.DegToRad(25));
                var weapon2 = AddDecorMesh(parent, new BoxMesh { Size = new Vector3(0.04f, 0.6f, 0.02f) },
                    new Color(0.6f, 0.62f, 0.65f), new Vector3(halfW * 0.5f, 1.5f, -halfH + 0.45f));
                weapon2.RotateZ(Mathf.DegToRad(-25));
            }
        }

        private static void AddTreasureDecorations(Node3D parent, Vector2 size)
        {
            float halfW = size.X / 2f;
            float halfH = size.Y / 2f;

            // Central pedestal — try model first
            var pedestalModel = ModelLibrary.TryLoad("prop", "pedestal");
            if (pedestalModel != null)
            {
                CharacterMeshBuilder.ScaleModelToFit(pedestalModel, 0.5f);
                pedestalModel.Position = new Vector3(0, 0, 0);
                parent.AddChild(pedestalModel);
            }
            else
            {
                var pedestalMat = new StandardMaterial3D();
                pedestalMat.AlbedoColor = new Color(0.6f, 0.55f, 0.4f);
                pedestalMat.EmissionEnabled = true;
                pedestalMat.Emission = new Color(0.4f, 0.35f, 0.15f);
                pedestalMat.EmissionEnergyMultiplier = 0.5f;

                var pedestal = new MeshInstance3D();
                pedestal.Mesh = new CylinderMesh { TopRadius = 0.6f, BottomRadius = 0.8f, Height = 0.5f, RadialSegments = 12 };
                pedestal.Position = new Vector3(0, 0.25f, 0);
                pedestal.MaterialOverride = pedestalMat;
                parent.AddChild(pedestal);
            }

            // Pedestal light
            var pedestalLight = new OmniLight3D();
            pedestalLight.Position = new Vector3(0, 1.5f, 0);
            pedestalLight.LightColor = new Color(1f, 0.9f, 0.5f);
            pedestalLight.LightEnergy = 1.5f;
            pedestalLight.OmniRange = 4f;
            parent.AddChild(pedestalLight);

            // Corner gold piles — try model first
            Vector3[] corners = {
                new(-halfW * 0.6f, 0, -halfH * 0.6f),
                new(halfW * 0.6f, 0, -halfH * 0.6f),
                new(-halfW * 0.6f, 0, halfH * 0.6f),
                new(halfW * 0.6f, 0, halfH * 0.6f)
            };

            foreach (var corner in corners)
            {
                var goldModel = ModelLibrary.TryLoad("prop", "gold_pile");
                if (goldModel != null)
                {
                    CharacterMeshBuilder.ScaleModelToFit(goldModel, 0.3f);
                    goldModel.Position = corner;
                    parent.AddChild(goldModel);
                }
                else
                {
                    AddDecorMesh(parent, new BoxMesh { Size = new Vector3(0.4f, 0.25f, 0.3f) },
                        new Color(0.7f, 0.6f, 0.2f), corner + new Vector3(0, 0.125f, 0));
                    AddDecorMesh(parent, new BoxMesh { Size = new Vector3(0.25f, 0.2f, 0.2f) },
                        new Color(0.75f, 0.65f, 0.25f), corner + new Vector3(0.15f, 0.1f, 0.1f));
                }
            }
        }

        private static void AddBossDecorations(Node3D parent, Vector2 size)
        {
            float halfW = size.X / 2f;
            float halfH = size.Y / 2f;
            float pillarInset = 0.3f;

            // 4 large pillars — try model first
            Vector3[] pillarPositions = {
                new(-halfW * pillarInset, 0, -halfH * pillarInset),
                new(halfW * pillarInset, 0, -halfH * pillarInset),
                new(-halfW * pillarInset, 0, halfH * pillarInset),
                new(halfW * pillarInset, 0, halfH * pillarInset)
            };

            foreach (var pos in pillarPositions)
            {
                var pillarModel = ModelLibrary.TryLoad("prop", "pillar");
                if (pillarModel != null)
                {
                    CharacterMeshBuilder.ScaleModelToFit(pillarModel, 3.5f);
                    pillarModel.Position = pos;
                    parent.AddChild(pillarModel);
                }
                else
                {
                    AddDecorMesh(parent, new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.6f, Height = 3.5f, RadialSegments = 10 },
                        new Color(0.3f, 0.15f, 0.15f), pos + new Vector3(0, 1.75f, 0));
                }
            }

            // Center red light for arena feel
            var bossLight = new OmniLight3D();
            bossLight.Position = new Vector3(0, 3f, 0);
            bossLight.LightColor = new Color(0.8f, 0.15f, 0.1f);
            bossLight.LightEnergy = 2f;
            bossLight.OmniRange = 15f;
            parent.AddChild(bossLight);

            // Ambient particles
            var ambient = VfxFactory.CreateAmbientParticles(new Color(0.8f, 0.2f, 0.1f), halfW * 0.6f);
            ambient.Position = new Vector3(0, 1.5f, 0);
            parent.AddChild(ambient);
        }

        private static void AddEntranceDecorations(Node3D parent, Vector2 size)
        {
            // Try model stairs first
            var stairsModel = ModelLibrary.TryLoad("prop", "stairs");
            if (stairsModel != null)
            {
                CharacterMeshBuilder.ScaleModelToFit(stairsModel, 0.75f);
                stairsModel.Position = new Vector3(0, 0, 0);
                parent.AddChild(stairsModel);
            }
            else
            {
                // Stacked blocks forming stairwell visual
                for (int i = 0; i < 3; i++)
                {
                    float s = 1.2f - i * 0.3f;
                    float y = i * 0.25f;
                    AddDecorMesh(parent, new BoxMesh { Size = new Vector3(s, 0.25f, s) },
                        new Color(0.25f, 0.24f, 0.22f), new Vector3(0, y + 0.125f, 0));
                }
            }

            // Dust particles
            var dust = VfxFactory.CreateAmbientParticles(new Color(0.6f, 0.55f, 0.45f), size.X * 0.3f);
            dust.Position = new Vector3(0, 1f, 0);
            parent.AddChild(dust);
        }

        // ── Helpers ──

        private static MeshInstance3D AddDecorMesh(Node3D parent, Mesh mesh, Color color, Vector3 position)
        {
            var node = new MeshInstance3D();
            node.Mesh = mesh;
            node.Position = position;
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = color;
            node.MaterialOverride = mat;
            parent.AddChild(node);
            return node;
        }

        private static Color GetFloorColor(RoomType type) => type switch
        {
            RoomType.Entrance => new Color(0.22f, 0.22f, 0.20f),
            RoomType.Boss => new Color(0.25f, 0.12f, 0.12f),
            RoomType.Treasure => new Color(0.25f, 0.22f, 0.12f),
            RoomType.Shop => new Color(0.15f, 0.2f, 0.15f),
            RoomType.SafeRoom => new Color(0.15f, 0.18f, 0.22f),
            _ => new Color(0.2f, 0.18f, 0.16f),
        };

        private static Color GetWallColor(RoomType type) => type switch
        {
            RoomType.Boss => new Color(0.35f, 0.15f, 0.15f),
            RoomType.Treasure => new Color(0.35f, 0.3f, 0.15f),
            _ => new Color(0.3f, 0.28f, 0.25f),
        };

        private static Color GetTorchColor(RoomType type) => type switch
        {
            RoomType.Combat => new Color(0.95f, 0.7f, 0.3f),
            RoomType.Boss => new Color(0.9f, 0.2f, 0.15f),
            RoomType.Treasure => new Color(1f, 0.85f, 0.3f),
            RoomType.Entrance => new Color(0.8f, 0.85f, 0.9f),
            RoomType.SafeRoom => new Color(0.4f, 0.6f, 0.9f),
            _ => new Color(0.9f, 0.7f, 0.4f),
        };

        private static void AddNavRegion(Node3D parent, Vector2 size)
        {
            var navRegion = new NavigationRegion3D();
            var navMesh = new NavigationMesh();

            float halfW = size.X / 2f;
            float halfH = size.Y / 2f;

            navMesh.Vertices = new Vector3[]
            {
                new Vector3(-halfW, 0, -halfH),
                new Vector3(halfW, 0, -halfH),
                new Vector3(halfW, 0, halfH),
                new Vector3(-halfW, 0, halfH),
            };
            navMesh.AddPolygon(new int[] { 0, 1, 2, 3 });

            navRegion.NavigationMesh = navMesh;
            parent.AddChild(navRegion);
        }

        public static Vector2 GetRoomSize(RoomType type) => type switch
        {
            RoomType.Boss => new Vector2(30, 30),
            RoomType.Treasure => new Vector2(15, 15),
            RoomType.Shop => new Vector2(18, 18),
            RoomType.SafeRoom => new Vector2(12, 12),
            RoomType.Entrance => new Vector2(16, 16),
            _ => new Vector2(20, 20),
        };
    }
}
