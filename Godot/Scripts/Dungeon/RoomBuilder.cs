using Godot;

namespace JunkbotArena
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
            bool doorNorth = false, bool doorSouth = false, bool doorEast = false, bool doorWest = false,
            SectorData sectorData = null)
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

            // Obstacles and hazards for combat rooms
            if (type == RoomType.Combat || type == RoomType.Boss)
            {
                bool isArena = size.X >= 25 || type == RoomType.Boss;
                AddObstacles(room, size, isArena);
                if (sectorData?.AllowedHazards?.Count > 0)
                    AddHazards(room, size, sectorData);
                if (isArena)
                    AddRaisedPlatform(room, size, type);
            }

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

        private static readonly Shader _checkerboardShader = CreateCheckerboardShader();

        private static Shader CreateCheckerboardShader()
        {
            var shader = new Shader();
            shader.Code = @"
shader_type spatial;
uniform vec3 color_a : source_color;
uniform vec3 color_b : source_color;
uniform float tile_scale = 5.0;
void fragment() {
    vec2 tile = floor(UV * tile_scale);
    float check = mod(tile.x + tile.y, 2.0);
    ALBEDO = mix(color_a, color_b, check);
}
";
            return shader;
        }

        private static void BuildTileFloor(Node3D parent, Vector2 size, RoomType type)
        {
            Color baseColor = GetFloorColor(type);
            Color altColor = baseColor.Lightened(0.08f);

            // Single plane for the entire floor — replaces 100+ individual tile nodes
            var floor = new MeshInstance3D();
            var planeMesh = new PlaneMesh();
            planeMesh.Size = new Vector2(size.X, size.Y);
            floor.Mesh = planeMesh;
            floor.Position = new Vector3(0, -0.05f, 0);

            var mat = new ShaderMaterial();
            mat.Shader = _checkerboardShader;
            mat.SetShaderParameter("color_a", baseColor);
            mat.SetShaderParameter("color_b", altColor);
            // Scale tiles so each is ~2 units — matches old 1.8 tile + 0.2 gap
            float tileScale = Mathf.Max(size.X, size.Y) / 2f;
            mat.SetShaderParameter("tile_scale", tileScale);

            floor.MaterialOverride = mat;
            parent.AddChild(floor);
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

        private static int _torchIndex;

        private static void AddWallTorches(Node3D parent, Vector2 size, float wallHeight, RoomType type)
        {
            float halfW = size.X / 2f;
            float halfH = size.Y / 2f;
            float spacing = 14f;
            float torchY = wallHeight * 0.65f;
            Color lightColor = GetTorchColor(type);
            _torchIndex = 0;

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
            int idx = _torchIndex++;

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

            // Only every other torch gets an OmniLight3D — halves active light count
            if (idx % 2 == 0)
            {
                var light = new OmniLight3D();
                light.Position = position + Vector3.Up * 0.2f;
                light.LightColor = lightColor;
                light.LightEnergy = 1.4f;
                light.OmniRange = 12f;
                light.ShadowEnabled = false;
                parent.AddChild(light);
            }

            // Fire particles — always added for visual consistency
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
                case RoomType.Event:
                    AddEventDecorations(parent, size);
                    break;
                case RoomType.Shop:
                    AddShopDecorations(parent, size);
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

        private static void AddEventDecorations(Node3D parent, Vector2 size)
        {
            // Central brazier / terminal
            var brazierMat = new StandardMaterial3D();
            brazierMat.AlbedoColor = new Color(0.4f, 0.3f, 0.5f);
            brazierMat.EmissionEnabled = true;
            brazierMat.Emission = new Color(0.5f, 0.3f, 0.8f);
            brazierMat.EmissionEnergyMultiplier = 1.2f;

            var brazier = new MeshInstance3D();
            brazier.Mesh = new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.7f, Height = 1.2f, RadialSegments = 8 };
            brazier.Position = new Vector3(0, 0.6f, 0);
            brazier.MaterialOverride = brazierMat;
            parent.AddChild(brazier);

            // Purple/blue ambient light
            var eventLight = new OmniLight3D();
            eventLight.Position = new Vector3(0, 2.5f, 0);
            eventLight.LightColor = new Color(0.5f, 0.3f, 0.9f);
            eventLight.LightEnergy = 1.8f;
            eventLight.OmniRange = 10f;
            parent.AddChild(eventLight);

            // Arcane circle around brazier
            var particles = VfxFactory.CreateAmbientParticles(new Color(0.6f, 0.3f, 0.9f), 2f);
            particles.Position = new Vector3(0, 1.5f, 0);
            parent.AddChild(particles);

            // Corner rune stones
            float halfW = size.X / 2f;
            float halfH = size.Y / 2f;
            Vector3[] runePositions = {
                new(-halfW * 0.5f, 0, -halfH * 0.5f),
                new(halfW * 0.5f, 0, -halfH * 0.5f),
                new(-halfW * 0.5f, 0, halfH * 0.5f),
                new(halfW * 0.5f, 0, halfH * 0.5f),
            };

            foreach (var pos in runePositions)
            {
                var runeMat = new StandardMaterial3D();
                runeMat.AlbedoColor = new Color(0.35f, 0.3f, 0.4f);
                runeMat.EmissionEnabled = true;
                runeMat.Emission = new Color(0.4f, 0.2f, 0.6f);
                runeMat.EmissionEnergyMultiplier = 0.6f;

                var rune = new MeshInstance3D();
                rune.Mesh = new BoxMesh { Size = new Vector3(0.6f, 1f, 0.6f) };
                rune.Position = pos + new Vector3(0, 0.5f, 0);
                rune.MaterialOverride = runeMat;
                parent.AddChild(rune);
            }
        }

        private static void AddShopDecorations(Node3D parent, Vector2 size)
        {
            // Counter / table
            AddDecorMesh(parent, new BoxMesh { Size = new Vector3(4f, 1f, 1.2f) },
                new Color(0.35f, 0.25f, 0.15f), new Vector3(0, 0.5f, -2f));

            // Display pedestals (3 across)
            for (int i = -1; i <= 1; i++)
            {
                float x = i * 3f;

                // Pedestal
                var pedestalMat = new StandardMaterial3D();
                pedestalMat.AlbedoColor = new Color(0.5f, 0.45f, 0.35f);
                pedestalMat.EmissionEnabled = true;
                pedestalMat.Emission = new Color(0.3f, 0.4f, 0.2f);
                pedestalMat.EmissionEnergyMultiplier = 0.4f;

                var pedestal = new MeshInstance3D();
                pedestal.Mesh = new CylinderMesh { TopRadius = 0.4f, BottomRadius = 0.5f, Height = 0.8f, RadialSegments = 8 };
                pedestal.Position = new Vector3(x, 0.4f, 2f);
                pedestal.MaterialOverride = pedestalMat;
                parent.AddChild(pedestal);

                // Floating item preview (small spinning cube placeholder)
                var itemPreview = new MeshInstance3D();
                itemPreview.Mesh = new BoxMesh { Size = new Vector3(0.4f, 0.4f, 0.4f) };
                itemPreview.Position = new Vector3(x, 1.3f, 2f);
                var itemMat = new StandardMaterial3D();
                itemMat.AlbedoColor = new Color(0.6f, 0.7f, 0.3f);
                itemMat.EmissionEnabled = true;
                itemMat.Emission = new Color(0.5f, 0.6f, 0.2f);
                itemMat.EmissionEnergyMultiplier = 0.8f;
                itemPreview.MaterialOverride = itemMat;
                parent.AddChild(itemPreview);

                // Pedestal light
                var light = new OmniLight3D();
                light.Position = new Vector3(x, 2f, 2f);
                light.LightColor = new Color(0.8f, 0.9f, 0.5f);
                light.LightEnergy = 0.8f;
                light.OmniRange = 3f;
                parent.AddChild(light);
            }

            // NPC placeholder — simple procedural mesh robot shopkeeper
            var npcBody = new MeshInstance3D();
            npcBody.Mesh = new CylinderMesh { TopRadius = 0.3f, BottomRadius = 0.4f, Height = 1.4f, RadialSegments = 6 };
            npcBody.Position = new Vector3(0, 0.7f + 1f, -2.5f);
            var npcMat = new StandardMaterial3D();
            npcMat.AlbedoColor = new Color(0.4f, 0.5f, 0.4f);
            npcBody.MaterialOverride = npcMat;
            parent.AddChild(npcBody);

            // NPC head
            var npcHead = new MeshInstance3D();
            npcHead.Mesh = new BoxMesh { Size = new Vector3(0.5f, 0.5f, 0.5f) };
            npcHead.Position = new Vector3(0, 0.7f + 1.4f + 0.35f, -2.5f);
            var headMat = new StandardMaterial3D();
            headMat.AlbedoColor = new Color(0.5f, 0.55f, 0.45f);
            headMat.EmissionEnabled = true;
            headMat.Emission = new Color(0.3f, 0.6f, 0.3f);
            headMat.EmissionEnergyMultiplier = 0.5f;
            npcHead.MaterialOverride = headMat;
            parent.AddChild(npcHead);

            // Shop sign
            var sign = new Label3D();
            sign.Text = "SHOP";
            sign.FontSize = 48;
            sign.Position = new Vector3(0, 2.8f, -2.5f);
            sign.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            sign.Modulate = new Color(0.3f, 0.8f, 0.3f);
            sign.OutlineModulate = new Color(0, 0, 0);
            sign.OutlineSize = 4;
            parent.AddChild(sign);
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
                new Vector3(-halfW, 0.05f, -halfH),
                new Vector3(halfW, 0.05f, -halfH),
                new Vector3(halfW, 0.05f, halfH),
                new Vector3(-halfW, 0.05f, halfH),
            };
            navMesh.AddPolygon(new int[] { 0, 1, 2, 3 });

            navRegion.NavigationMesh = navMesh;
            parent.AddChild(navRegion);
        }

        // ── Obstacles ──

        private static void AddObstacles(Node3D parent, Vector2 size, bool isArena)
        {
            var rng = new RandomNumberGenerator();
            rng.Randomize();

            float halfW = size.X / 2f;
            float halfH = size.Y / 2f;
            float wallInset = 2f;
            float centerClearance = 3f;
            float minSpacing = 2.5f;

            int count = isArena ? rng.RandiRange(5, 7) : rng.RandiRange(3, 5);
            var placed = new System.Collections.Generic.List<Vector3>();

            for (int attempt = 0; attempt < count * 10 && placed.Count < count; attempt++)
            {
                float x = rng.RandfRange(-halfW + wallInset, halfW - wallInset);
                float z = rng.RandfRange(-halfH + wallInset, halfH - wallInset);

                // Keep center clear for spawns
                if (Mathf.Abs(x) < centerClearance && Mathf.Abs(z) < centerClearance)
                    continue;

                // Keep door openings clear (±1.5 units from each edge center)
                if ((Mathf.Abs(x) < 2f && Mathf.Abs(z) > halfH - 3f) ||
                    (Mathf.Abs(z) < 2f && Mathf.Abs(x) > halfW - 3f))
                    continue;

                // Min spacing from other obstacles
                var pos = new Vector3(x, 0, z);
                bool tooClose = false;
                foreach (var p in placed)
                {
                    if (pos.DistanceTo(p) < minSpacing) { tooClose = true; break; }
                }
                if (tooClose) continue;

                placed.Add(pos);

                // Pick obstacle type
                int obstacleType = rng.RandiRange(0, 2);
                switch (obstacleType)
                {
                    case 0: // Stone Pillar
                        AddStaticObstacle(parent, pos,
                            new CylinderMesh { TopRadius = 0.6f, BottomRadius = 0.6f, Height = 3f, RadialSegments = 8 },
                            new CylinderShape3D { Radius = 0.6f, Height = 3f },
                            new Vector3(0, 1.5f, 0),
                            new Color(0.35f, 0.33f, 0.3f));
                        break;
                    case 1: // Crate Stack
                        AddStaticObstacle(parent, pos,
                            new BoxMesh { Size = new Vector3(1f, 1.2f, 1f) },
                            new BoxShape3D { Size = new Vector3(1f, 1.2f, 1f) },
                            new Vector3(0, 0.6f, 0),
                            new Color(0.4f, 0.3f, 0.18f));
                        break;
                    case 2: // Low Wall
                        float wallRot = rng.Randf() > 0.5f ? 0 : Mathf.Pi / 2f;
                        var lwNode = AddStaticObstacle(parent, pos,
                            new BoxMesh { Size = new Vector3(2f, 1f, 0.5f) },
                            new BoxShape3D { Size = new Vector3(2f, 1f, 0.5f) },
                            new Vector3(0, 0.5f, 0),
                            new Color(0.32f, 0.3f, 0.28f));
                        lwNode.RotateY(wallRot);
                        break;
                }
            }
        }

        private static StaticBody3D AddStaticObstacle(Node3D parent, Vector3 floorPos,
            Mesh mesh, Shape3D shape, Vector3 meshOffset, Color color)
        {
            var body = new StaticBody3D();
            body.Position = floorPos;
            body.CollisionLayer = 1; // default layer — blocks movement
            parent.AddChild(body);

            var meshNode = new MeshInstance3D();
            meshNode.Mesh = mesh;
            meshNode.Position = meshOffset;
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = color;
            meshNode.MaterialOverride = mat;
            body.AddChild(meshNode);

            var col = new CollisionShape3D();
            col.Shape = shape;
            col.Position = meshOffset;
            body.AddChild(col);

            return body;
        }

        // ── Hazards ──

        private static void AddHazards(Node3D parent, Vector2 size, SectorData sectorData)
        {
            var rng = new RandomNumberGenerator();
            rng.Randomize();

            float halfW = size.X / 2f;
            float halfH = size.Y / 2f;

            foreach (var hazardType in sectorData.AllowedHazards)
            {
                // 50% chance per hazard type per room
                if (rng.Randf() > 0.5f) continue;

                float x = rng.RandfRange(-halfW * 0.5f, halfW * 0.5f);
                float z = rng.RandfRange(-halfH * 0.5f, halfH * 0.5f);

                // Keep clear of center spawn
                if (Mathf.Abs(x) < 2.5f && Mathf.Abs(z) < 2.5f)
                {
                    x += x >= 0 ? 3f : -3f;
                }

                switch (hazardType)
                {
                    case HazardType.PoisonPool:
                        AddPoisonPool(parent, new Vector3(x, 0.02f, z));
                        break;
                    case HazardType.ElectricPlate:
                        AddElectricPlate(parent, new Vector3(x, 0.02f, z));
                        break;
                    case HazardType.LavaCrack:
                        AddLavaCrack(parent, new Vector3(x, 0.02f, z));
                        break;
                }
            }
        }

        private static void AddPoisonPool(Node3D parent, Vector3 pos)
        {
            var area = new Area3D();
            area.Position = pos;
            area.CollisionLayer = 0;
            area.CollisionMask = Constants.MASK_PLAYER | Constants.MASK_ENEMY;
            parent.AddChild(area);

            var col = new CollisionShape3D();
            col.Shape = new BoxShape3D { Size = new Vector3(3f, 1f, 3f) };
            col.Position = new Vector3(0, 0.5f, 0);
            area.AddChild(col);

            // Green emissive surface
            var mesh = new MeshInstance3D();
            mesh.Mesh = new PlaneMesh { Size = new Vector2(3f, 3f) };
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.15f, 0.5f, 0.1f, 0.7f);
            mat.EmissionEnabled = true;
            mat.Emission = new Color(0.1f, 0.6f, 0.05f);
            mat.EmissionEnergyMultiplier = 0.8f;
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mesh.MaterialOverride = mat;
            area.AddChild(mesh);

            // Bubble particles
            var particles = VfxFactory.CreateAmbientParticles(new Color(0.2f, 0.7f, 0.1f), 1.2f);
            particles.Position = new Vector3(0, 0.2f, 0);
            area.AddChild(particles);

            // Damage via HazardDamager component
            var damager = new HazardDamager();
            damager.DamagePerSecond = 2f;
            damager.DamageType = DamageType.Poison;
            area.AddChild(damager);
        }

        private static void AddElectricPlate(Node3D parent, Vector3 pos)
        {
            var area = new Area3D();
            area.Position = pos;
            area.CollisionLayer = 0;
            area.CollisionMask = Constants.MASK_PLAYER | Constants.MASK_ENEMY;
            parent.AddChild(area);

            var col = new CollisionShape3D();
            col.Shape = new BoxShape3D { Size = new Vector3(2f, 1f, 2f) };
            col.Position = new Vector3(0, 0.5f, 0);
            area.AddChild(col);

            // Blue metal plate
            var mesh = new MeshInstance3D();
            mesh.Mesh = new BoxMesh { Size = new Vector3(2f, 0.05f, 2f) };
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.3f, 0.35f, 0.5f);
            mat.EmissionEnabled = true;
            mat.Emission = new Color(0.2f, 0.4f, 0.9f);
            mat.EmissionEnergyMultiplier = 0.5f;
            mesh.MaterialOverride = mat;
            area.AddChild(mesh);

            var damager = new HazardDamager();
            damager.DamagePerSecond = 5f;
            damager.DamageType = DamageType.Lightning;
            damager.StunDuration = 0.3f;
            damager.ToggleInterval = 3f;
            area.AddChild(damager);
        }

        private static void AddLavaCrack(Node3D parent, Vector3 pos)
        {
            var area = new Area3D();
            area.Position = pos;
            area.CollisionLayer = 0;
            area.CollisionMask = Constants.MASK_PLAYER | Constants.MASK_ENEMY;
            parent.AddChild(area);

            var col = new CollisionShape3D();
            col.Shape = new BoxShape3D { Size = new Vector3(0.5f, 1f, 6f) };
            col.Position = new Vector3(0, 0.5f, 0);
            area.AddChild(col);

            // Thin red/orange strip
            var mesh = new MeshInstance3D();
            mesh.Mesh = new BoxMesh { Size = new Vector3(0.5f, 0.05f, 6f) };
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.8f, 0.25f, 0.05f);
            mat.EmissionEnabled = true;
            mat.Emission = new Color(0.9f, 0.3f, 0.05f);
            mat.EmissionEnergyMultiplier = 1.5f;
            mesh.MaterialOverride = mat;
            area.AddChild(mesh);

            // Fire particles along crack
            var fire = VfxFactory.CreateTorchFireParticles();
            fire.Position = new Vector3(0, 0.1f, 0);
            area.AddChild(fire);

            var damager = new HazardDamager();
            damager.DamagePerSecond = 3f;
            damager.DamageType = DamageType.Fire;
            area.AddChild(damager);
        }

        // ── Raised Platforms ──

        private static void AddRaisedPlatform(Node3D parent, Vector2 size, RoomType type)
        {
            float platformHeight = 0.8f;

            if (type == RoomType.Boss)
            {
                // 4 stepped corner ledges for boss rooms
                float ledgeH = 0.4f;
                float ledgeSize = 4f;
                float halfW = size.X / 2f;
                float halfH = size.Y / 2f;
                float inset = 2f;
                Vector3[] corners = {
                    new(-halfW + inset + ledgeSize / 2f, 0, -halfH + inset + ledgeSize / 2f),
                    new(halfW - inset - ledgeSize / 2f, 0, -halfH + inset + ledgeSize / 2f),
                    new(-halfW + inset + ledgeSize / 2f, 0, halfH - inset - ledgeSize / 2f),
                    new(halfW - inset - ledgeSize / 2f, 0, halfH - inset - ledgeSize / 2f),
                };

                foreach (var corner in corners)
                {
                    AddStaticObstacle(parent, corner,
                        new BoxMesh { Size = new Vector3(ledgeSize, ledgeH, ledgeSize) },
                        new BoxShape3D { Size = new Vector3(ledgeSize, ledgeH, ledgeSize) },
                        new Vector3(0, ledgeH / 2f, 0),
                        new Color(0.28f, 0.14f, 0.14f));
                }
            }
            else
            {
                // Raised center platform with ramp
                float platSize = 8f;
                AddStaticObstacle(parent, Vector3.Zero,
                    new BoxMesh { Size = new Vector3(platSize, platformHeight, platSize) },
                    new BoxShape3D { Size = new Vector3(platSize, platformHeight, platSize) },
                    new Vector3(0, platformHeight / 2f, 0),
                    new Color(0.3f, 0.28f, 0.25f));

                // Ramp on south side
                var ramp = new StaticBody3D();
                ramp.Position = new Vector3(0, 0, platSize / 2f + 1f);
                parent.AddChild(ramp);

                var rampMesh = new MeshInstance3D();
                rampMesh.Mesh = new BoxMesh { Size = new Vector3(3f, platformHeight, 2.5f) };
                rampMesh.Position = new Vector3(0, platformHeight / 2f, 0);
                // Tilt the ramp mesh for visual slope
                rampMesh.RotationDegrees = new Vector3(-18f, 0, 0);
                var rampMat = new StandardMaterial3D();
                rampMat.AlbedoColor = new Color(0.32f, 0.3f, 0.27f);
                rampMesh.MaterialOverride = rampMat;
                ramp.AddChild(rampMesh);

                var rampCol = new CollisionShape3D();
                rampCol.Shape = new BoxShape3D { Size = new Vector3(3f, platformHeight, 2.5f) };
                rampCol.Position = new Vector3(0, platformHeight / 2f, 0);
                rampCol.RotationDegrees = new Vector3(-18f, 0, 0);
                ramp.AddChild(rampCol);
            }
        }

        // Combat room size variants — picked deterministically per room
        private static readonly Vector2[] CombatSizes = new[]
        {
            new Vector2(18, 18),  // Small — tight, fast fight
            new Vector2(20, 20),  // Standard
            new Vector2(20, 20),  // Standard (weighted)
            new Vector2(22, 24),  // Large — open arena
            new Vector2(25, 25),  // Arena — with obstacles, more enemies
        };

        public static Vector2 GetRoomSize(RoomType type, int seed = 0) => type switch
        {
            RoomType.Boss => new Vector2(30, 30),
            RoomType.Treasure => new Vector2(15, 15),
            RoomType.Shop => new Vector2(18, 18),
            RoomType.SafeRoom => new Vector2(12, 12),
            RoomType.Entrance => new Vector2(16, 16),
            RoomType.Event => new Vector2(18, 18),
            RoomType.Combat => CombatSizes[((seed % CombatSizes.Length) + CombatSizes.Length) % CombatSizes.Length],
            _ => new Vector2(20, 20),
        };
    }
}
