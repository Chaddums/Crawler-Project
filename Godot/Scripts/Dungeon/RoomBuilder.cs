using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Builds procedural room geometry: floor, walls, door openings.
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

            // Floor
            var floor = new StaticBody3D();
            floor.CollisionLayer = Constants.MASK_GROUND;
            room.AddChild(floor);

            var floorMesh = new MeshInstance3D();
            var planeMesh = new PlaneMesh();
            planeMesh.Size = size;
            floorMesh.Mesh = planeMesh;

            var floorMat = new StandardMaterial3D();
            floorMat.AlbedoColor = GetFloorColor(type);
            floorMesh.MaterialOverride = floorMat;
            floor.AddChild(floorMesh);

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
                BuildWallWithDoor(room, new Vector3(0, wallHeight / 2f, -halfH), size.X, wallHeight, wallThickness, doorWidth, type);

            // South wall (positive Z)
            if (!doorSouth)
                BuildWall(room, new Vector3(0, wallHeight / 2f, halfH), new Vector3(size.X, wallHeight, wallThickness), type);
            else
                BuildWallWithDoor(room, new Vector3(0, wallHeight / 2f, halfH), size.X, wallHeight, wallThickness, doorWidth, type);

            // East wall (positive X)
            if (!doorEast)
                BuildWall(room, new Vector3(halfW, wallHeight / 2f, 0), new Vector3(wallThickness, wallHeight, size.Y), type);
            else
                BuildWallWithDoorZ(room, new Vector3(halfW, wallHeight / 2f, 0), size.Y, wallHeight, wallThickness, doorWidth, type);

            // West wall (negative X)
            if (!doorWest)
                BuildWall(room, new Vector3(-halfW, wallHeight / 2f, 0), new Vector3(wallThickness, wallHeight, size.Y), type);
            else
                BuildWallWithDoorZ(room, new Vector3(-halfW, wallHeight / 2f, 0), size.Y, wallHeight, wallThickness, doorWidth, type);

            // Spawn point marker
            var spawnMarker = new Marker3D();
            spawnMarker.Name = "SpawnPoint";
            spawnMarker.Position = new Vector3(0, 0.9f, 0);
            room.AddChild(spawnMarker);

            return room;
        }

        /// <summary>
        /// Build a corridor connecting two positions.
        /// </summary>
        public static Node3D BuildCorridor(Vector3 from, Vector3 to, float width = 3f)
        {
            var corridor = new Node3D();
            corridor.Position = (from + to) / 2f;

            var dir = to - from;
            float length = dir.Length();

            // Floor
            var floor = new StaticBody3D();
            floor.CollisionLayer = Constants.MASK_GROUND;
            corridor.AddChild(floor);

            var floorMesh = new MeshInstance3D();
            var planeMesh = new PlaneMesh();

            bool isXAxis = Mathf.Abs(dir.X) > Mathf.Abs(dir.Z);
            planeMesh.Size = isXAxis ? new Vector2(length, width) : new Vector2(width, length);
            floorMesh.Mesh = planeMesh;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.18f, 0.16f, 0.14f);
            floorMesh.MaterialOverride = mat;
            floor.AddChild(floorMesh);

            var floorShape = new CollisionShape3D();
            var box = new BoxShape3D();
            box.Size = isXAxis ? new Vector3(length, 0.1f, width) : new Vector3(width, 0.1f, length);
            floorShape.Shape = box;
            floorShape.Position = new Vector3(0, -0.05f, 0);
            floor.AddChild(floorShape);

            return corridor;
        }

        private static void BuildWall(Node3D parent, Vector3 pos, Vector3 size, RoomType type)
        {
            var wall = new StaticBody3D();
            wall.Position = pos;
            wall.CollisionLayer = 1; // Default layer
            parent.AddChild(wall);

            var mesh = new MeshInstance3D();
            var boxMesh = new BoxMesh();
            boxMesh.Size = size;
            mesh.Mesh = boxMesh;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = GetWallColor(type);
            mesh.MaterialOverride = mat;
            wall.AddChild(mesh);

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
                // Left side
                BuildWall(parent, center + new Vector3(-(doorWidth / 2f + sideWidth / 2f), 0, 0),
                    new Vector3(sideWidth, wallHeight, wallThickness), type);
                // Right side
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
