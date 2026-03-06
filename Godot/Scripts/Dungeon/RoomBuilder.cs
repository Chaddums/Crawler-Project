using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Builds procedural room geometry: tile floors, walls with trim, door frames,
    /// wall torches, and room-type decorations.
    /// </summary>
    public static class RoomBuilder
    {
        // Current sector data — set during BuildRoom scope for color methods
        private static SectorData _currentSector;

        /// <summary>
        /// Create a room Node3D with floor and walls at the given position.
        /// </summary>
        public static Node3D BuildRoom(Vector3 position, Vector2 size, RoomType type,
            bool doorNorth = false, bool doorSouth = false, bool doorEast = false, bool doorWest = false,
            SectorData sectorData = null, RoomShape shape = RoomShape.Rectangle)
        {
            _currentSector = sectorData;
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
            float wallHeight = 5f;
            float wallThickness = 0.5f;
            float doorWidth = 5.5f;

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
            if (type == RoomType.Combat || type == RoomType.Boss || type == RoomType.Megabonk)
            {
                bool isArena = size.X >= 42 || type == RoomType.Boss || type == RoomType.Megabonk;
                AddObstacles(room, size, isArena);
                if (sectorData?.AllowedHazards?.Count > 0)
                    AddHazards(room, size, sectorData);
                // Raised platforms disabled — ramps don't reliably work with CharacterBody3D
                // if (isArena)
                //     AddRaisedPlatform(room, size, type);

                // Wall detail panels from asset pack
                AddWallDetails(room, size, wallHeight, doorNorth, doorSouth, doorEast, doorWest);
            }

            // Room shape features
            if (shape == RoomShape.Partitioned)
                AddPartitionWall(room, size);
            else if (shape == RoomShape.LShaped)
                AddLShapedWing(room, size, type);
            else if (shape == RoomShape.TShaped)
                AddTShapedWing(room, size, type);

            // Navigation mesh for pathfinding
            AddNavRegion(room, size);

            // Thematic prop dressing
            RoomDresser.DressRoom(room, size, type, doorNorth, doorSouth, doorEast, doorWest);

            // Spawn point marker
            var spawnMarker = new Marker3D();
            spawnMarker.Name = "SpawnPoint";
            spawnMarker.Position = new Vector3(0, 0.9f, 0);
            room.AddChild(spawnMarker);

            _currentSector = null;
            return room;
        }

        /// <summary>
        /// Build a wide hallway connecting two rooms (replaces old narrow corridors).
        /// Width: 10 units, wall height: 5 units.
        /// </summary>
        public static Node3D BuildWideHallway(Vector3 from, Vector3 to,
            float fromHalfExtent, float toHalfExtent)
        {
            var dir = (to - from).Normalized();
            bool isXAxis = Mathf.Abs(dir.X) > Mathf.Abs(dir.Z);

            var gapStart = from + dir * fromHalfExtent;
            var gapEnd = to - dir * toHalfExtent;
            float gapLength = gapStart.DistanceTo(gapEnd);

            if (gapLength < 0.5f) return new Node3D();

            float width = 10f;
            var hallway = new Node3D();
            hallway.Position = (gapStart + gapEnd) / 2f;

            float wallHeight = 5f;
            float wallThickness = 0.3f;
            float halfW = width / 2f;

            // Floor with center path strip
            var floor = new StaticBody3D();
            floor.CollisionLayer = Constants.MASK_GROUND;
            hallway.AddChild(floor);

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
                BuildWall(hallway, new Vector3(0, wallHeight / 2f, -halfW),
                    new Vector3(gapLength, wallHeight, wallThickness), RoomType.Combat);
                BuildWall(hallway, new Vector3(0, wallHeight / 2f, halfW),
                    new Vector3(gapLength, wallHeight, wallThickness), RoomType.Combat);
            }
            else
            {
                BuildWall(hallway, new Vector3(-halfW, wallHeight / 2f, 0),
                    new Vector3(wallThickness, wallHeight, gapLength), RoomType.Combat);
                BuildWall(hallway, new Vector3(halfW, wallHeight / 2f, 0),
                    new Vector3(wallThickness, wallHeight, gapLength), RoomType.Combat);
            }

            // Hallway sconces
            AddCorridorSconces(hallway, gapLength, width, wallHeight, isXAxis);

            // Navigation mesh for hallway
            var hallwaySize = isXAxis ? new Vector2(gapLength, width) : new Vector2(width, gapLength);
            AddNavRegion(hallway, hallwaySize);

            return hallway;
        }

        /// <summary>
        /// Add a small navigation bridge for rooms with minimal gap (less than 4 units).
        /// Ensures enemy pathfinding works across room boundaries without a visible hallway.
        /// </summary>
        public static void BuildNavBridge(Node3D parent, Vector3 gapStart, Vector3 gapEnd, bool isXAxis)
        {
            float gapLength = gapStart.DistanceTo(gapEnd);
            if (gapLength < 0.1f) return;

            var bridge = new Node3D();
            bridge.Position = (gapStart + gapEnd) / 2f;
            bridge.Name = "NavBridge";

            // Small floor collision so entities don't fall
            var floor = new StaticBody3D();
            floor.CollisionLayer = Constants.MASK_GROUND;
            bridge.AddChild(floor);

            float bridgeWidth = 5.5f; // match door width
            var floorShape = new CollisionShape3D();
            var box = new BoxShape3D();
            box.Size = isXAxis ? new Vector3(gapLength, 0.1f, bridgeWidth) : new Vector3(bridgeWidth, 0.1f, gapLength);
            floorShape.Shape = box;
            floorShape.Position = new Vector3(0, -0.05f, 0);
            floor.AddChild(floorShape);

            // Floor visual
            var floorMesh = new MeshInstance3D();
            var planeMesh = new PlaneMesh();
            planeMesh.Size = isXAxis ? new Vector2(gapLength, bridgeWidth) : new Vector2(bridgeWidth, gapLength);
            floorMesh.Mesh = planeMesh;
            floorMesh.Position = new Vector3(0, -0.05f, 0);
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.18f, 0.16f, 0.14f);
            mat.Metallic = 0.5f;
            mat.Roughness = 0.65f;
            floorMesh.MaterialOverride = mat;
            floor.AddChild(floorMesh);

            // Nav mesh
            var navSize = isXAxis ? new Vector2(gapLength, bridgeWidth) : new Vector2(bridgeWidth, gapLength);
            AddNavRegion(bridge, navSize);

            parent.AddChild(bridge);
        }

        // ── Tile Floor ──

        private static readonly Shader _floorShader = CreateFloorShader();
        private static readonly Shader _wallShader = CreateWallShader();

        private static Shader CreateFloorShader()
        {
            var shader = new Shader();
            shader.Code = @"
shader_type spatial;
uniform vec3 color_a : source_color;
uniform vec3 color_b : source_color;
uniform float tile_scale = 5.0;

// Hash function for per-tile variation
float hash21(vec2 p) {
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}

void fragment() {
    vec2 tile_coord = UV * tile_scale;
    vec2 tile_id = floor(tile_coord);
    vec2 tile_uv = fract(tile_coord);

    // Checkerboard base
    float check = mod(tile_id.x + tile_id.y, 2.0);
    vec3 base_color = mix(color_a, color_b, check);

    // Per-tile roughness/shade variation
    float tile_hash = hash21(tile_id);
    base_color *= 0.92 + tile_hash * 0.16;

    // Panel gap lines (dark edges between tiles)
    float gap_width = 0.03;
    float gap = step(tile_uv.x, gap_width) + step(1.0 - gap_width, tile_uv.x)
              + step(tile_uv.y, gap_width) + step(1.0 - gap_width, tile_uv.y);
    gap = clamp(gap, 0.0, 1.0);
    base_color = mix(base_color, base_color * 0.3, gap);

    // Worn edges — slightly shinier at tile borders
    float edge_dist = min(min(tile_uv.x, 1.0 - tile_uv.x), min(tile_uv.y, 1.0 - tile_uv.y));
    float edge_wear = smoothstep(0.08, 0.0, edge_dist);

    ALBEDO = base_color;
    METALLIC = 0.5;
    ROUGHNESS = mix(0.65 + tile_hash * 0.15, 0.35, edge_wear);
    SPECULAR = 0.4;
}
";
            return shader;
        }

        private static Shader CreateWallShader()
        {
            var shader = new Shader();
            shader.Code = @"
shader_type spatial;
uniform vec3 wall_color : source_color;
uniform vec3 accent_color : source_color = vec3(0.85, 0.55, 0.15);
uniform float panel_count_x = 4.0;
uniform float panel_count_y = 3.0;

float hash21(vec2 p) {
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}

void fragment() {
    vec2 panel_coord = UV * vec2(panel_count_x, panel_count_y);
    vec2 panel_id = floor(panel_coord);
    vec2 panel_uv = fract(panel_coord);

    // Per-panel color variation
    float panel_hash = hash21(panel_id);
    vec3 base = wall_color * (0.9 + panel_hash * 0.2);

    // Panel grooves (vertical/horizontal lines)
    float groove_width = 0.025;
    float groove = step(panel_uv.x, groove_width) + step(1.0 - groove_width, panel_uv.x)
                 + step(panel_uv.y, groove_width) + step(1.0 - groove_width, panel_uv.y);
    groove = clamp(groove, 0.0, 1.0);
    base = mix(base, base * 0.25, groove);

    // Corner rivets — bright dots at panel corners
    float rivet_size = 0.06;
    float d_tl = length(panel_uv - vec2(rivet_size, rivet_size));
    float d_tr = length(panel_uv - vec2(1.0 - rivet_size, rivet_size));
    float d_bl = length(panel_uv - vec2(rivet_size, 1.0 - rivet_size));
    float d_br = length(panel_uv - vec2(1.0 - rivet_size, 1.0 - rivet_size));
    float rivet = smoothstep(rivet_size, rivet_size * 0.5, min(min(d_tl, d_tr), min(d_bl, d_br)));
    base = mix(base, vec3(0.7, 0.7, 0.75), rivet);

    // Accent stripe — horizontal band at ~30% height from bottom
    float stripe_center = 0.3;
    float stripe_width = 0.04;
    float stripe_y = UV.y * panel_count_y; // use global UV for consistent stripe
    float stripe = smoothstep(stripe_center - stripe_width, stripe_center, fract(stripe_y / panel_count_y * 1.0))
                 * smoothstep(stripe_center + stripe_width, stripe_center, fract(stripe_y / panel_count_y * 1.0));
    // Only apply stripe once across the wall (not per panel)
    float global_stripe = smoothstep(stripe_center - stripe_width, stripe_center, UV.y)
                        * smoothstep(stripe_center + stripe_width, stripe_center, UV.y);
    base = mix(base, accent_color, global_stripe * 0.8);

    ALBEDO = base;
    METALLIC = mix(0.55, 0.8, rivet);
    ROUGHNESS = mix(0.55 + panel_hash * 0.1, 0.3, rivet);
    SPECULAR = 0.45;
}
";
            return shader;
        }

        private static void BuildTileFloor(Node3D parent, Vector2 size, RoomType type)
        {
            Color baseColor = GetFloorColor(type, _currentSector);
            Color altColor = baseColor.Lightened(0.08f);

            // Single plane for the entire floor — replaces 100+ individual tile nodes
            var floor = new MeshInstance3D();
            var planeMesh = new PlaneMesh();
            planeMesh.Size = new Vector2(size.X, size.Y);
            floor.Mesh = planeMesh;
            floor.Position = new Vector3(0, -0.05f, 0);

            var mat = new ShaderMaterial();
            mat.Shader = _floorShader;
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
            Color baseColor = new Color(0.32f, 0.30f, 0.27f);
            Color altColor = baseColor.Lightened(0.08f);

            // Main floor — use floor shader for metallic panels
            var floorMesh = new MeshInstance3D();
            var planeMesh = new PlaneMesh();
            planeMesh.Size = isXAxis ? new Vector2(length, width) : new Vector2(width, length);
            floorMesh.Mesh = planeMesh;
            var mat = new ShaderMaterial();
            mat.Shader = _floorShader;
            mat.SetShaderParameter("color_a", baseColor);
            mat.SetShaderParameter("color_b", altColor);
            mat.SetShaderParameter("tile_scale", Mathf.Max(length, width) / 2f);
            floorMesh.MaterialOverride = mat;
            parent.AddChild(floorMesh);

            // Center path strip — emissive accent
            float stripWidth = 0.8f;
            var strip = new MeshInstance3D();
            var stripMesh = new BoxMesh();
            stripMesh.Size = isXAxis
                ? new Vector3(length * 0.9f, 0.02f, stripWidth)
                : new Vector3(stripWidth, 0.02f, length * 0.9f);
            strip.Mesh = stripMesh;
            strip.Position = new Vector3(0, 0.01f, 0);
            var stripMat = new StandardMaterial3D();
            stripMat.AlbedoColor = new Color(0.7f, 0.45f, 0.1f);
            stripMat.Metallic = 0.6f;
            stripMat.Roughness = 0.4f;
            stripMat.EmissionEnabled = true;
            stripMat.Emission = new Color(0.85f, 0.55f, 0.15f);
            stripMat.EmissionEnergyMultiplier = 0.4f;
            strip.MaterialOverride = stripMat;
            parent.AddChild(strip);
        }

        // ── Walls ──

        private static readonly string[] _wallModelIds =
            { "wall_1", "wall_2", "wall_3", "wall_4", "wall_5", "wall_empty" };

        private static void BuildWall(Node3D parent, Vector3 pos, Vector3 size, RoomType type)
        {
            var wall = new StaticBody3D();
            wall.Position = pos;
            wall.CollisionLayer = 1;
            parent.AddChild(wall);

            if (TryBuildTiledWall(wall, size))
            {
                // Cap on top of the wall so it has visible thickness from top-down camera
                float capH = 0.2f;
                var cap = new MeshInstance3D();
                var capBox = new BoxMesh();
                capBox.Size = new Vector3(size.X, capH, size.Z);
                cap.Mesh = capBox;
                cap.Position = new Vector3(0, size.Y / 2f, 0);
                var capMat = new StandardMaterial3D();
                // Match Quaternius model color (light gray) instead of shader wall color
                capMat.AlbedoColor = new Color(0.75f, 0.72f, 0.68f);
                capMat.Roughness = 0.7f;
                capMat.Metallic = 0.1f;
                cap.MaterialOverride = capMat;
                wall.AddChild(cap);
            }
            else
            {
                // Procedural fallback
                var mesh = new MeshInstance3D();
                var boxMesh = new BoxMesh();
                boxMesh.Size = size;
                mesh.Mesh = boxMesh;

                var mat = new ShaderMaterial();
                mat.Shader = _wallShader;
                mat.SetShaderParameter("wall_color", GetWallColor(type, _currentSector));
                mat.SetShaderParameter("accent_color", GetAccentColor(_currentSector));
                float wallSpan = Mathf.Max(size.X, size.Z);
                mat.SetShaderParameter("panel_count_x", Mathf.Max(2f, Mathf.Round(wallSpan / 2f)));
                mat.SetShaderParameter("panel_count_y", Mathf.Max(2f, Mathf.Round(size.Y / 1.5f)));
                mesh.MaterialOverride = mat;
                wall.AddChild(mesh);
            }

            var shape = new CollisionShape3D();
            var box = new BoxShape3D();
            box.Size = size;
            shape.Shape = box;
            wall.AddChild(shape);
        }

        /// <summary>
        /// Tile wall models along the wall span. Returns false if no wall models available.
        /// </summary>
        private static bool TryBuildTiledWall(Node3D wallBody, Vector3 size)
        {
            var probe = ModelLibrary.TryLoad("wall", "wall_1");
            if (probe == null)
            {
                GD.Print("[RoomBuilder] TryBuildTiledWall: wall_1 model not found, using procedural fallback");
                return false;
            }

            float wallHeight = size.Y;
            bool xAxis = size.X > size.Z;
            float wallSpan = xAxis ? size.X : size.Z;

            // Use transform-aware AABB (accounts for intermediate FBX scale/rotation nodes)
            var aabb = GetEffectiveAabb(probe);
            if (aabb.Size.Y < 0.001f)
            {
                probe.QueueFree();
                return false;
            }

            // Scale root so effective visual height matches wall height
            float scale = wallHeight / aabb.Size.Y;
            probe.Scale = Vector3.One * scale;

            // Determine tile width (model's wider horizontal axis after scaling)
            float scaledWidthX = aabb.Size.X * scale;
            float scaledWidthZ = aabb.Size.Z * scale;
            bool modelWideAlongX = scaledWidthX >= scaledWidthZ;
            float tileWidth = Mathf.Max(scaledWidthX, scaledWidthZ);

            if (tileWidth < 0.1f)
            {
                probe.QueueFree();
                return false;
            }

            // Y offset so model base sits at ground level
            float tileY = -wallHeight / 2f - aabb.Position.Y * scale;

            // Rotate model's wide axis to match wall span direction
            bool needRotation = (xAxis && !modelWideAlongX) || (!xAxis && modelWideAlongX);

            // Tile to fill wall span
            int tileCount = Mathf.Max(1, Mathf.RoundToInt(wallSpan / tileWidth));
            float tileSpacing = wallSpan / tileCount;
            float scaleFix = tileSpacing / tileWidth;
            float startOffset = -wallSpan / 2f + tileSpacing / 2f;

            var rng = new RandomNumberGenerator();
            rng.Randomize();

            for (int i = 0; i < tileCount; i++)
            {
                Node3D tile;
                if (i == 0)
                {
                    tile = probe;
                }
                else
                {
                    string id = _wallModelIds[rng.RandiRange(0, _wallModelIds.Length - 1)];
                    tile = ModelLibrary.TryLoad("wall", id) ?? ModelLibrary.TryLoad("wall", "wall_1");
                    if (tile == null) continue;
                    tile.Scale = Vector3.One * scale;
                }

                // Stretch/shrink slightly so tiles fill the span exactly
                // Extra 2% overlap eliminates floating-point seam gaps between tiles
                tile.Scale *= scaleFix * 1.02f;

                float offset = startOffset + i * tileSpacing;
                tile.Position = xAxis
                    ? new Vector3(offset, tileY, 0)
                    : new Vector3(0, tileY, offset);

                if (needRotation)
                    tile.RotateY(Mathf.Pi / 2f);

                wallBody.AddChild(tile);
            }

            return true;
        }

        /// <summary>
        /// Compute the effective AABB of a model by walking the scene tree and
        /// accumulating intermediate transforms (handles FBX scale/rotation nodes).
        /// Includes root rotation (for FBX Z-up→Y-up conversion) but excludes root
        /// scale since we override root.Scale.
        /// </summary>
        private static Aabb GetEffectiveAabb(Node3D root)
        {
            Aabb combined = new();
            bool first = true;

            // Include root's rotation (handles FBX coordinate system conversion)
            // but strip its scale (we'll override root.Scale)
            var rootRotation = new Transform3D(root.Basis.Orthonormalized(), Vector3.Zero);

            foreach (var child in root.GetChildren())
                CollectTransformedAabbs(child, rootRotation, ref combined, ref first);

            // If root itself is a mesh with no children
            if (first && root is MeshInstance3D mi && mi.Mesh != null)
                combined = mi.Mesh.GetAabb();

            return combined;
        }

        private static void CollectTransformedAabbs(Node node, Transform3D accumulated,
            ref Aabb combined, ref bool first)
        {
            Transform3D current = accumulated;
            if (node is Node3D n3d)
                current = accumulated * n3d.Transform;

            if (node is MeshInstance3D mi && mi.Mesh != null)
            {
                var meshAabb = mi.Mesh.GetAabb();
                var pos = meshAabb.Position;
                var end = meshAabb.End;

                // Transform all 8 AABB corners to get the true extent
                var c0 = current * new Vector3(pos.X, pos.Y, pos.Z);
                var minV = c0;
                var maxV = c0;
                Vector3[] corners =
                {
                    current * new Vector3(end.X, pos.Y, pos.Z),
                    current * new Vector3(pos.X, end.Y, pos.Z),
                    current * new Vector3(end.X, end.Y, pos.Z),
                    current * new Vector3(pos.X, pos.Y, end.Z),
                    current * new Vector3(end.X, pos.Y, end.Z),
                    current * new Vector3(pos.X, end.Y, end.Z),
                    current * new Vector3(end.X, end.Y, end.Z),
                };
                foreach (var c in corners)
                {
                    minV = new Vector3(Mathf.Min(minV.X, c.X), Mathf.Min(minV.Y, c.Y), Mathf.Min(minV.Z, c.Z));
                    maxV = new Vector3(Mathf.Max(maxV.X, c.X), Mathf.Max(maxV.Y, c.Y), Mathf.Max(maxV.Z, c.Z));
                }

                var transformedAabb = new Aabb(minV, maxV - minV);
                if (first) { combined = transformedAabb; first = false; }
                else combined = combined.Merge(transformedAabb);
            }

            foreach (var child in node.GetChildren())
                CollectTransformedAabbs(child, current, ref combined, ref first);
        }

        /// <summary>
        /// Scale a model to fit a target height using transform-aware AABB measurement.
        /// Unlike CharacterMeshBuilder.ScaleModelToFit, this accounts for intermediate
        /// FBX scale/rotation nodes in the model hierarchy.
        /// </summary>
        internal static void ScaleModelToFitEffective(Node3D model, float targetHeight)
        {
            var aabb = GetEffectiveAabb(model);
            if (aabb.Size.Y < 0.001f)
            {
                model.Scale = Vector3.One * 0.01f * targetHeight;
                return;
            }
            float scale = targetHeight / aabb.Size.Y;
            model.Scale = Vector3.One * scale;
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
                ScaleModelToFitEffective(model, wallHeight);
                model.Position = doorCenter + new Vector3(0, wallHeight / 2f, 0);
                parent.AddChild(model);
                return;
            }

            Color frameColor = GetWallColor(type, _currentSector).Lightened(0.15f);
            float pillarSize = 0.25f;

            // Left pillar
            AddMetalDecorMesh(parent, new BoxMesh { Size = new Vector3(pillarSize, wallHeight, pillarSize) },
                frameColor, doorCenter + new Vector3(-doorWidth / 2f, wallHeight / 2f, 0), 0.6f, 0.5f);

            // Right pillar
            AddMetalDecorMesh(parent, new BoxMesh { Size = new Vector3(pillarSize, wallHeight, pillarSize) },
                frameColor, doorCenter + new Vector3(doorWidth / 2f, wallHeight / 2f, 0), 0.6f, 0.5f);

            // Lintel
            AddMetalDecorMesh(parent, new BoxMesh { Size = new Vector3(doorWidth + pillarSize * 2, 0.2f, pillarSize) },
                frameColor.Lightened(0.05f), doorCenter + new Vector3(0, wallHeight, 0), 0.6f, 0.5f);
        }

        private static void BuildDoorFrameZ(Node3D parent, Vector3 doorCenter, float wallHeight, float doorWidth, float wallThickness, RoomType type)
        {
            // Try model door frame (rotated 90 degrees for Z-axis doors)
            var model = ModelLibrary.TryLoad("door", "door_frame");
            if (model != null)
            {
                ScaleModelToFitEffective(model, wallHeight);
                model.Position = doorCenter + new Vector3(0, wallHeight / 2f, 0);
                model.RotateY(Mathf.DegToRad(90));
                parent.AddChild(model);
                return;
            }

            Color frameColor = GetWallColor(type, _currentSector).Lightened(0.15f);
            float pillarSize = 0.25f;

            AddMetalDecorMesh(parent, new BoxMesh { Size = new Vector3(pillarSize, wallHeight, pillarSize) },
                frameColor, doorCenter + new Vector3(0, wallHeight / 2f, -doorWidth / 2f), 0.6f, 0.5f);

            AddMetalDecorMesh(parent, new BoxMesh { Size = new Vector3(pillarSize, wallHeight, pillarSize) },
                frameColor, doorCenter + new Vector3(0, wallHeight / 2f, doorWidth / 2f), 0.6f, 0.5f);

            AddMetalDecorMesh(parent, new BoxMesh { Size = new Vector3(pillarSize, 0.2f, doorWidth + pillarSize * 2) },
                frameColor.Lightened(0.05f), doorCenter + new Vector3(0, wallHeight, 0), 0.6f, 0.5f);
        }

        // ── Wall Trim ──

        private static void AddWallTrim(Node3D parent, Vector2 size, float wallHeight, RoomType type)
        {
            float halfW = size.X / 2f;
            float halfH = size.Y / 2f;
            Color accentColor = GetAccentColor(_currentSector);
            Color crownColor = GetWallColor(type, _currentSector).Lightened(0.1f);
            float baseH = 0.2f;
            float crownH = 0.1f;

            // Baseboard strips (4 walls) — emissive accent trim so room boundaries are visible
            AddEmissiveTrim(parent, new Vector3(size.X, baseH, 0.1f),
                accentColor, new Vector3(0, baseH / 2f, -halfH + 0.25f));
            AddEmissiveTrim(parent, new Vector3(size.X, baseH, 0.1f),
                accentColor, new Vector3(0, baseH / 2f, halfH - 0.25f));
            AddEmissiveTrim(parent, new Vector3(0.1f, baseH, size.Y),
                accentColor, new Vector3(-halfW + 0.25f, baseH / 2f, 0));
            AddEmissiveTrim(parent, new Vector3(0.1f, baseH, size.Y),
                accentColor, new Vector3(halfW - 0.25f, baseH / 2f, 0));

            // Crown strips — polished metal trim
            AddMetalDecorMesh(parent, new BoxMesh { Size = new Vector3(size.X, crownH, 0.06f) },
                crownColor, new Vector3(0, wallHeight - crownH / 2f, -halfH + 0.25f), 0.7f, 0.4f);
            AddMetalDecorMesh(parent, new BoxMesh { Size = new Vector3(size.X, crownH, 0.06f) },
                crownColor, new Vector3(0, wallHeight - crownH / 2f, halfH - 0.25f), 0.7f, 0.4f);
            AddMetalDecorMesh(parent, new BoxMesh { Size = new Vector3(0.06f, crownH, size.Y) },
                crownColor, new Vector3(-halfW + 0.25f, wallHeight - crownH / 2f, 0), 0.7f, 0.4f);
            AddMetalDecorMesh(parent, new BoxMesh { Size = new Vector3(0.06f, crownH, size.Y) },
                crownColor, new Vector3(halfW - 0.25f, wallHeight - crownH / 2f, 0), 0.7f, 0.4f);
        }

        private static void AddEmissiveTrim(Node3D parent, Vector3 size, Color color, Vector3 position)
        {
            var mesh = new MeshInstance3D();
            mesh.Mesh = new BoxMesh { Size = size };
            mesh.Position = position;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = color;
            mat.EmissionEnabled = true;
            mat.Emission = color;
            mat.EmissionEnergyMultiplier = 1.2f;
            mat.Metallic = 0.8f;
            mat.Roughness = 0.3f;
            mesh.MaterialOverride = mat;
            parent.AddChild(mesh);
        }

        // ── Wall Torches ──

        private static int _torchIndex;

        private static void AddWallTorches(Node3D parent, Vector2 size, float wallHeight, RoomType type)
        {
            float halfW = size.X / 2f;
            float halfH = size.Y / 2f;
            float spacing = 18f;
            float torchY = wallHeight * 0.65f;
            Color lightColor = GetTorchColor(type, _currentSector);
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
                ScaleModelToFitEffective(model, 0.4f);
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

            // Every torch gets a light — rooms are big enough to need them all
            {
                var light = new OmniLight3D();
                light.Position = position + Vector3.Up * 0.2f;
                light.LightColor = lightColor;
                light.LightEnergy = 1.2f;
                light.OmniRange = 14f;
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
                case RoomType.Megabonk:
                    AddCombatDecorations(parent, size);
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
                    ScaleModelToFitEffective(model, rng.RandfRange(0.3f, 0.6f));
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
                ScaleModelToFitEffective(rackModel, 1.2f);
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
                ScaleModelToFitEffective(pedestalModel, 0.5f);
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

            // Corner treasure props — use vessel/chest models
            Vector3[] corners = {
                new(-halfW * 0.5f, 0, -halfH * 0.5f),
                new(halfW * 0.5f, 0, -halfH * 0.5f),
                new(-halfW * 0.5f, 0, halfH * 0.5f),
                new(halfW * 0.5f, 0, halfH * 0.5f)
            };

            string[] treasureIds = { "chest", "vessel", "vessel_short", "vessel_tall" };
            for (int i = 0; i < corners.Length; i++)
            {
                var corner = corners[i];
                string propId = treasureIds[i % treasureIds.Length];
                var treasureModel = ModelLibrary.TryLoad("prop", propId);
                if (treasureModel != null)
                {
                    ScaleModelToFitEffective(treasureModel, 0.8f);
                    treasureModel.Position = corner;
                    parent.AddChild(treasureModel);
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

            // 4 large pillars — use column models
            Vector3[] pillarPositions = {
                new(-halfW * pillarInset, 0, -halfH * pillarInset),
                new(halfW * pillarInset, 0, -halfH * pillarInset),
                new(-halfW * pillarInset, 0, halfH * pillarInset),
                new(halfW * pillarInset, 0, halfH * pillarInset)
            };

            string[] columnIds = { "column_2", "column_3", "column_2", "column_3" };
            for (int i = 0; i < pillarPositions.Length; i++)
            {
                var pos = pillarPositions[i];
                var pillarModel = ModelLibrary.TryLoad("prop", columnIds[i])
                    ?? ModelLibrary.TryLoad("prop", "pillar");
                if (pillarModel != null)
                {
                    ScaleModelToFitEffective(pillarModel, 5f);
                    pillarModel.Position = pos;
                    parent.AddChild(pillarModel);
                }
                else
                {
                    AddDecorMesh(parent, new CylinderMesh { TopRadius = 0.6f, BottomRadius = 0.7f, Height = 5f, RadialSegments = 10 },
                        new Color(0.3f, 0.15f, 0.15f), pos + new Vector3(0, 2.5f, 0));
                }
            }

            // Decorative laser turrets at compass points
            Vector3[] laserPositions = {
                new(0, 0, -halfH * 0.5f),
                new(0, 0, halfH * 0.5f),
                new(-halfW * 0.5f, 0, 0),
                new(halfW * 0.5f, 0, 0),
            };
            foreach (var lp in laserPositions)
            {
                var laser = ModelLibrary.TryLoad("prop", "laser");
                if (laser != null)
                {
                    ScaleModelToFitEffective(laser, 1.5f);
                    laser.Position = lp;
                    parent.AddChild(laser);
                }
            }

            // Center red light for arena feel
            var bossLight = new OmniLight3D();
            bossLight.Position = new Vector3(0, 4f, 0);
            bossLight.LightColor = new Color(0.8f, 0.15f, 0.1f);
            bossLight.LightEnergy = 2.5f;
            bossLight.OmniRange = 25f;
            parent.AddChild(bossLight);

            // Ambient particles
            var ambient = VfxFactory.CreateAmbientParticles(new Color(0.8f, 0.2f, 0.1f), halfW * 0.6f);
            ambient.Position = new Vector3(0, 2f, 0);
            parent.AddChild(ambient);
        }

        private static void AddEntranceDecorations(Node3D parent, Vector2 size)
        {
            // Try model stairs first
            var stairsModel = ModelLibrary.TryLoad("prop", "stairs");
            if (stairsModel != null)
            {
                ScaleModelToFitEffective(stairsModel, 0.75f);
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
            eventLight.OmniRange = 14f;
            parent.AddChild(eventLight);

            // Arcane circle around brazier
            var particles = VfxFactory.CreateAmbientParticles(new Color(0.6f, 0.3f, 0.9f), 2f);
            particles.Position = new Vector3(0, 1.5f, 0);
            parent.AddChild(particles);

            // Corner pods/capsules for sci-fi lab feel
            float halfW = size.X / 2f;
            float halfH = size.Y / 2f;
            Vector3[] cornerPositions = {
                new(-halfW * 0.5f, 0, -halfH * 0.5f),
                new(halfW * 0.5f, 0, -halfH * 0.5f),
                new(-halfW * 0.5f, 0, halfH * 0.5f),
                new(halfW * 0.5f, 0, halfH * 0.5f),
            };

            string[] podIds = { "pod", "capsule", "pod", "capsule" };
            for (int i = 0; i < cornerPositions.Length; i++)
            {
                var pos = cornerPositions[i];
                var podModel = ModelLibrary.TryLoad("prop", podIds[i]);
                if (podModel != null)
                {
                    ScaleModelToFitEffective(podModel, 1.8f);
                    podModel.Position = pos;
                    parent.AddChild(podModel);
                }
                else
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
        }

        private static void AddShopDecorations(Node3D parent, Vector2 size)
        {
            // Counter — try shelf_tall model first
            var counter = ModelLibrary.TryLoad("prop", "shelf_tall");
            if (counter != null)
            {
                ScaleModelToFitEffective(counter, 1.2f);
                counter.Position = new Vector3(0, 0, -3f);
                parent.AddChild(counter);
            }
            else
            {
                AddDecorMesh(parent, new BoxMesh { Size = new Vector3(4f, 1f, 1.2f) },
                    new Color(0.35f, 0.25f, 0.15f), new Vector3(0, 0.5f, -3f));
            }

            // Shop terminal — computer_small behind counter
            var terminal = ModelLibrary.TryLoad("prop", "computer_small");
            if (terminal != null)
            {
                ScaleModelToFitEffective(terminal, 0.8f);
                terminal.Position = new Vector3(1.5f, 1.2f, -3.5f);
                parent.AddChild(terminal);
            }

            // Display pedestals (3 across)
            for (int i = -1; i <= 1; i++)
            {
                float x = i * 4f;

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

        // ── Wall Details ──

        private static void AddWallDetails(Node3D parent, Vector2 size, float wallHeight,
            bool doorN, bool doorS, bool doorE, bool doorW)
        {
            var detailIds = ModelLibrary.GetCategoryIds("detail");
            if (detailIds.Length == 0) return;

            var rng = new RandomNumberGenerator();
            rng.Randomize();

            float halfW = size.X / 2f;
            float halfH = size.Y / 2f;
            float doorClearance = 2.5f;
            int count = rng.RandiRange(3, 5);

            for (int i = 0; i < count; i++)
            {
                string id = detailIds[rng.RandiRange(0, detailIds.Length - 1)];
                var model = ModelLibrary.TryLoad("detail", id);
                if (model == null) continue;

                ScaleModelToFitEffective(model, rng.RandfRange(0.6f, 1.2f));

                // Pick a wall (0=N, 1=S, 2=E, 3=W), skip walls with doors
                int wallIdx;
                int attempts = 0;
                do
                {
                    wallIdx = rng.RandiRange(0, 3);
                    attempts++;
                } while (attempts < 20 && (
                    (wallIdx == 0 && doorN) || (wallIdx == 1 && doorS) ||
                    (wallIdx == 2 && doorE) || (wallIdx == 3 && doorW)));

                if (attempts >= 20) continue;

                float y = rng.RandfRange(1.0f, 2.5f);
                float wallOffset = 0.05f; // slightly proud of wall surface

                switch (wallIdx)
                {
                    case 0: // North wall (-Z)
                        float nx = rng.RandfRange(-halfW * 0.7f, halfW * 0.7f);
                        if (doorN && Mathf.Abs(nx) < doorClearance) continue;
                        model.Position = new Vector3(nx, y, -halfH + wallOffset);
                        model.RotationDegrees = new Vector3(0, 180, 0);
                        break;
                    case 1: // South wall (+Z)
                        float sx = rng.RandfRange(-halfW * 0.7f, halfW * 0.7f);
                        if (doorS && Mathf.Abs(sx) < doorClearance) continue;
                        model.Position = new Vector3(sx, y, halfH - wallOffset);
                        // Default rotation faces +Z (outward), no rotation needed
                        break;
                    case 2: // East wall (+X)
                        float ez = rng.RandfRange(-halfH * 0.7f, halfH * 0.7f);
                        if (doorE && Mathf.Abs(ez) < doorClearance) continue;
                        model.Position = new Vector3(halfW - wallOffset, y, ez);
                        model.RotationDegrees = new Vector3(0, -90, 0);
                        break;
                    case 3: // West wall (-X)
                        float wz = rng.RandfRange(-halfH * 0.7f, halfH * 0.7f);
                        if (doorW && Mathf.Abs(wz) < doorClearance) continue;
                        model.Position = new Vector3(-halfW + wallOffset, y, wz);
                        model.RotationDegrees = new Vector3(0, 90, 0);
                        break;
                }

                parent.AddChild(model);
            }
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

        private static MeshInstance3D AddMetalDecorMesh(Node3D parent, Mesh mesh, Color color, Vector3 position,
            float metallic = 0.5f, float roughness = 0.6f)
        {
            var node = new MeshInstance3D();
            node.Mesh = mesh;
            node.Position = position;
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = color;
            mat.Metallic = metallic;
            mat.Roughness = roughness;
            node.MaterialOverride = mat;
            parent.AddChild(node);
            return node;
        }

        private static Color GetFloorColor(RoomType type, SectorData sector = null)
        {
            Color baseColor = type switch
            {
                RoomType.Entrance => new Color(0.38f, 0.38f, 0.35f),
                RoomType.Boss => new Color(0.38f, 0.22f, 0.22f),
                RoomType.Treasure => new Color(0.38f, 0.35f, 0.22f),
                RoomType.Shop => new Color(0.28f, 0.35f, 0.28f),
                RoomType.SafeRoom => new Color(0.28f, 0.32f, 0.38f),
                _ => new Color(0.35f, 0.32f, 0.28f),
            };
            if (sector != null)
                return baseColor.Lerp(sector.FloorTint, 0.3f);
            return baseColor;
        }

        private static Color GetWallColor(RoomType type, SectorData sector = null)
        {
            Color baseColor = type switch
            {
                RoomType.Boss => new Color(0.45f, 0.25f, 0.25f),
                RoomType.Treasure => new Color(0.45f, 0.4f, 0.25f),
                _ => new Color(0.42f, 0.4f, 0.36f),
            };
            if (sector != null)
                return baseColor.Lerp(sector.WallTint, 0.3f);
            return baseColor;
        }

        private static Color GetTorchColor(RoomType type, SectorData sector = null)
        {
            Color baseColor = type switch
            {
                RoomType.Combat => new Color(0.95f, 0.7f, 0.3f),
                RoomType.Boss => new Color(0.9f, 0.2f, 0.15f),
                RoomType.Treasure => new Color(1f, 0.85f, 0.3f),
                RoomType.Entrance => new Color(0.8f, 0.85f, 0.9f),
                RoomType.SafeRoom => new Color(0.4f, 0.6f, 0.9f),
                _ => new Color(0.9f, 0.7f, 0.4f),
            };
            if (sector != null)
                return baseColor.Lerp(sector.TorchTint, 0.3f);
            return baseColor;
        }

        private static Color GetAccentColor(SectorData sector = null)
        {
            return sector?.AccentColor ?? new Color(0.85f, 0.55f, 0.15f);
        }

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
            float wallInset = 3f;
            float centerClearance = 5f;
            float minSpacing = 3.5f;

            int count = isArena ? rng.RandiRange(7, 12) : rng.RandiRange(5, 8);
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
                    case 0: // Metal Pillar
                    {
                        string[] columnIds = { "column_1", "column_2", "column_3", "column_slim" };
                        string colId = columnIds[rng.RandiRange(0, columnIds.Length - 1)];
                        var colModel = ModelLibrary.TryLoad("prop", colId);
                        if (colModel != null)
                        {
                            ScaleModelToFitEffective(colModel, 3f);
                            AddStaticObstacleWithModel(parent, pos, colModel,
                                new CylinderShape3D { Radius = 0.6f, Height = 3f },
                                new Vector3(0, 1.5f, 0));
                        }
                        else
                        {
                            AddStaticObstacle(parent, pos,
                                new CylinderMesh { TopRadius = 0.6f, BottomRadius = 0.6f, Height = 3f, RadialSegments = 8 },
                                new CylinderShape3D { Radius = 0.6f, Height = 3f },
                                new Vector3(0, 1.5f, 0),
                                new Color(0.35f, 0.33f, 0.3f), 0.5f, 0.6f);
                        }
                        break;
                    }
                    case 1: // Crate Stack (low cover)
                    {
                        var crateModel = ModelLibrary.TryLoad("prop", "crate");
                        if (crateModel != null)
                        {
                            ScaleModelToFitEffective(crateModel, 0.5f);
                            AddStaticObstacleWithModel(parent, pos, crateModel,
                                new BoxShape3D { Size = new Vector3(1f, 0.5f, 1f) },
                                new Vector3(0, 0.25f, 0));
                        }
                        else
                        {
                            AddStaticObstacle(parent, pos,
                                new BoxMesh { Size = new Vector3(1f, 0.5f, 1f) },
                                new BoxShape3D { Size = new Vector3(1f, 0.5f, 1f) },
                                new Vector3(0, 0.25f, 0),
                                new Color(0.4f, 0.3f, 0.18f), 0.3f, 0.7f);
                        }
                        break;
                    }
                    case 2: // Low Wall (low cover)
                    {
                        float wallRot = rng.Randf() > 0.5f ? 0 : Mathf.Pi / 2f;
                        var lwModel = ModelLibrary.TryLoad("prop", "crate_long");
                        if (lwModel != null)
                        {
                            ScaleModelToFitEffective(lwModel, 0.5f);
                            var lwNode = AddStaticObstacleWithModel(parent, pos, lwModel,
                                new BoxShape3D { Size = new Vector3(2f, 0.5f, 0.5f) },
                                new Vector3(0, 0.25f, 0));
                            lwNode.RotateY(wallRot);
                        }
                        else
                        {
                            var lwNode = AddStaticObstacle(parent, pos,
                                new BoxMesh { Size = new Vector3(2f, 0.5f, 0.5f) },
                                new BoxShape3D { Size = new Vector3(2f, 0.5f, 0.5f) },
                                new Vector3(0, 0.25f, 0),
                                new Color(0.32f, 0.3f, 0.28f), 0.4f, 0.65f);
                            lwNode.RotateY(wallRot);
                        }
                        break;
                    }
                }
            }
        }

        private static StaticBody3D AddStaticObstacle(Node3D parent, Vector3 floorPos,
            Mesh mesh, Shape3D shape, Vector3 meshOffset, Color color,
            float metallic = 0f, float roughness = 1f)
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
            mat.Metallic = metallic;
            mat.Roughness = roughness;
            meshNode.MaterialOverride = mat;
            body.AddChild(meshNode);

            var col = new CollisionShape3D();
            col.Shape = shape;
            col.Position = meshOffset;
            body.AddChild(col);

            return body;
        }

        private static StaticBody3D AddStaticObstacleWithModel(Node3D parent, Vector3 floorPos,
            Node3D model, Shape3D shape, Vector3 collisionOffset)
        {
            var body = new StaticBody3D();
            body.Position = floorPos;
            body.CollisionLayer = 1;
            parent.AddChild(body);

            body.AddChild(model);

            var col = new CollisionShape3D();
            col.Shape = shape;
            col.Position = collisionOffset;
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
                        AddSunkenHazardArea(parent, new Vector3(x, 0, z), new Vector2(3.5f, 3.5f));
                        AddPoisonPool(parent, new Vector3(x, 0.02f, z));
                        break;
                    case HazardType.ElectricPlate:
                        AddElectricPlate(parent, new Vector3(x, 0.02f, z));
                        break;
                    case HazardType.LavaCrack:
                        AddSunkenHazardArea(parent, new Vector3(x, 0, z), new Vector2(1f, 6.5f));
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
            if (type == RoomType.Boss)
            {
                // Boss multi-level arena:
                // - 4 large corner platforms with ramps
                // - Connecting catwalks along walls
                // - Visual sunken center
                float cornerSize = 10f;
                float cornerHeight = 1.2f;
                float catwalkWidth = 3f;
                float catwalkHeight = 0.8f;
                float halfW = size.X / 2f;
                float halfH = size.Y / 2f;
                float inset = 3f;

                Color platformColor = new Color(0.28f, 0.14f, 0.14f);

                // 4 corner platforms
                Vector3[] corners = {
                    new(-halfW + inset + cornerSize / 2f, 0, -halfH + inset + cornerSize / 2f),
                    new(halfW - inset - cornerSize / 2f, 0, -halfH + inset + cornerSize / 2f),
                    new(-halfW + inset + cornerSize / 2f, 0, halfH - inset - cornerSize / 2f),
                    new(halfW - inset - cornerSize / 2f, 0, halfH - inset - cornerSize / 2f),
                };

                foreach (var corner in corners)
                {
                    AddStaticObstacle(parent, corner,
                        new BoxMesh { Size = new Vector3(cornerSize, cornerHeight, cornerSize) },
                        new BoxShape3D { Size = new Vector3(cornerSize, cornerHeight, cornerSize) },
                        new Vector3(0, cornerHeight / 2f, 0),
                        platformColor, 0.5f, 0.6f);

                    // Ramp facing center from each corner
                    var toCenter = (Vector3.Zero - corner).Normalized();
                    var rampPos = corner + toCenter * (cornerSize / 2f + 1.5f);
                    AddRamp(parent, rampPos, cornerHeight, 5f, toCenter);
                }

                // Catwalks connecting corners along walls
                float catwalkLen = halfW * 2f - 2 * inset - 2 * cornerSize;
                if (catwalkLen > 2f)
                {
                    float northZ = -halfH + inset + cornerSize / 2f;
                    float southZ = halfH - inset - cornerSize / 2f;

                    // North catwalk
                    AddStaticObstacle(parent, new Vector3(0, 0, northZ),
                        new BoxMesh { Size = new Vector3(catwalkLen, catwalkHeight, catwalkWidth) },
                        new BoxShape3D { Size = new Vector3(catwalkLen, catwalkHeight, catwalkWidth) },
                        new Vector3(0, catwalkHeight / 2f, 0),
                        platformColor.Lightened(0.05f), 0.5f, 0.6f);
                    // North catwalk ramp (from center side)
                    AddRamp(parent, new Vector3(0, 0, northZ + catwalkWidth / 2f + 1.5f),
                        catwalkHeight, 4f, Vector3.Back);

                    // South catwalk
                    AddStaticObstacle(parent, new Vector3(0, 0, southZ),
                        new BoxMesh { Size = new Vector3(catwalkLen, catwalkHeight, catwalkWidth) },
                        new BoxShape3D { Size = new Vector3(catwalkLen, catwalkHeight, catwalkWidth) },
                        new Vector3(0, catwalkHeight / 2f, 0),
                        platformColor.Lightened(0.05f), 0.5f, 0.6f);
                    // South catwalk ramp (from center side)
                    AddRamp(parent, new Vector3(0, 0, southZ - catwalkWidth / 2f - 1.5f),
                        catwalkHeight, 4f, Vector3.Forward);
                }

                // Visual sunken center (darkened floor area, same collision height)
                var sunkenMesh = new MeshInstance3D();
                sunkenMesh.Mesh = new PlaneMesh { Size = new Vector2(halfW, halfH) };
                sunkenMesh.Position = new Vector3(0, -0.03f, 0);
                var sunkenMat = new StandardMaterial3D();
                sunkenMat.AlbedoColor = new Color(0.1f, 0.06f, 0.06f);
                sunkenMat.Metallic = 0.6f;
                sunkenMat.Roughness = 0.5f;
                sunkenMesh.MaterialOverride = sunkenMat;
                parent.AddChild(sunkenMesh);
            }
            else
            {
                // Arena combat: larger center platform with 2 opposing ramps
                float platformHeight = 1.0f;
                float platSize = 14f;
                AddStaticObstacle(parent, Vector3.Zero,
                    new BoxMesh { Size = new Vector3(platSize, platformHeight, platSize) },
                    new BoxShape3D { Size = new Vector3(platSize, platformHeight, platSize) },
                    new Vector3(0, platformHeight / 2f, 0),
                    new Color(0.3f, 0.28f, 0.25f), 0.5f, 0.6f);

                // North ramp
                AddRamp(parent, new Vector3(0, 0, -(platSize / 2f + 1.5f)), platformHeight, 5f, Vector3.Forward);

                // South ramp
                AddRamp(parent, new Vector3(0, 0, platSize / 2f + 1.5f), platformHeight, 5f, Vector3.Back);
            }
        }

        /// <summary>
        /// Add a ramp at the given position facing toward center.
        /// </summary>
        private static void AddRamp(Node3D parent, Vector3 position, float height, float width, Vector3 direction)
        {
            var ramp = new StaticBody3D();
            ramp.Position = position;
            parent.AddChild(ramp);

            float rampLength = 2.5f;
            var rampMesh = new MeshInstance3D();
            rampMesh.Mesh = new BoxMesh { Size = new Vector3(width, height, rampLength) };
            rampMesh.Position = new Vector3(0, height / 2f, 0);

            // Calculate tilt based on direction
            float tiltAngle = -18f;
            if (direction.Z < -0.5f) tiltAngle = 18f; // heading north, tilt up
            else if (direction.Z > 0.5f) tiltAngle = -18f; // heading south
            else if (direction.X > 0.5f) rampMesh.RotationDegrees = new Vector3(0, 90, 0);
            else if (direction.X < -0.5f) rampMesh.RotationDegrees = new Vector3(0, -90, 0);

            if (Mathf.Abs(direction.Z) > 0.5f)
                rampMesh.RotationDegrees = new Vector3(tiltAngle, 0, 0);

            var rampMat = new StandardMaterial3D();
            rampMat.AlbedoColor = new Color(0.32f, 0.3f, 0.27f);
            rampMat.Metallic = 0.4f;
            rampMat.Roughness = 0.7f;
            rampMesh.MaterialOverride = rampMat;
            ramp.AddChild(rampMesh);

            var rampCol = new CollisionShape3D();
            rampCol.Shape = new BoxShape3D { Size = new Vector3(width, height, rampLength) };
            rampCol.Position = rampMesh.Position;
            rampCol.RotationDegrees = rampMesh.RotationDegrees;
            ramp.AddChild(rampCol);
        }

        /// <summary>
        /// Add a visually sunken area for hazards (floor dropped 0.3 units, darker color).
        /// Collision stays flat for simplicity.
        /// </summary>
        private static void AddSunkenHazardArea(Node3D parent, Vector3 position, Vector2 areaSize)
        {
            var sunkenMesh = new MeshInstance3D();
            sunkenMesh.Mesh = new PlaneMesh { Size = areaSize };
            sunkenMesh.Position = position + new Vector3(0, -0.3f, 0);
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.08f, 0.06f, 0.05f);
            mat.Metallic = 0.5f;
            mat.Roughness = 0.7f;
            sunkenMesh.MaterialOverride = mat;
            parent.AddChild(sunkenMesh);
        }

        // ── Room Shape Features ──

        /// <summary>
        /// Partitioned room: internal half-height wall covering 40% of room width.
        /// Creates a tactical divider for cover-based combat.
        /// </summary>
        private static void AddPartitionWall(Node3D room, Vector2 size)
        {
            float halfW = size.X / 2f;
            float partitionWidth = size.X * 0.4f;
            float partitionHeight = 2.5f;
            float thickness = 0.5f;

            var wall = new StaticBody3D();
            wall.CollisionLayer = 1;
            room.AddChild(wall);

            var mesh = new MeshInstance3D();
            mesh.Mesh = new BoxMesh { Size = new Vector3(partitionWidth, partitionHeight, thickness) };
            mesh.Position = new Vector3(0, partitionHeight / 2f, 0);
            var mat = new ShaderMaterial();
            mat.Shader = _wallShader;
            mat.SetShaderParameter("wall_color", GetWallColor(RoomType.Combat, _currentSector));
            mat.SetShaderParameter("accent_color", GetAccentColor(_currentSector));
            mat.SetShaderParameter("panel_count_x", Mathf.Max(2f, Mathf.Round(partitionWidth / 2f)));
            mat.SetShaderParameter("panel_count_y", Mathf.Max(2f, Mathf.Round(partitionHeight / 1.5f)));
            mesh.MaterialOverride = mat;
            wall.AddChild(mesh);

            var col = new CollisionShape3D();
            col.Shape = new BoxShape3D { Size = new Vector3(partitionWidth, partitionHeight, thickness) };
            col.Position = new Vector3(0, partitionHeight / 2f, 0);
            wall.AddChild(col);
        }

        /// <summary>
        /// L-shaped room: adds a side wing (50% width x 40% depth) offset to form the L.
        /// Includes floor, walls, and nav mesh for the extension.
        /// </summary>
        private static void AddLShapedWing(Node3D room, Vector2 size, RoomType type)
        {
            float mainHalfW = size.X / 2f;
            float mainHalfH = size.Y / 2f;

            float wingW = size.X * 0.5f;
            float wingH = size.Y * 0.4f;
            float wallHeight = 5f;
            float wallThickness = 0.5f;

            // Wing offset: extends from the east side, south portion
            float wingCenterX = mainHalfW + wingW / 2f;
            float wingCenterZ = mainHalfH - wingH / 2f;

            // Wing floor
            var wingFloor = new StaticBody3D();
            wingFloor.CollisionLayer = Constants.MASK_GROUND;
            wingFloor.Position = new Vector3(wingCenterX, 0, wingCenterZ);
            room.AddChild(wingFloor);

            var wingFloorMesh = new MeshInstance3D();
            var planeMesh = new PlaneMesh();
            planeMesh.Size = new Vector2(wingW, wingH);
            wingFloorMesh.Mesh = planeMesh;
            wingFloorMesh.Position = new Vector3(0, -0.05f, 0);
            var floorColor = GetFloorColor(type, _currentSector);
            var floorMat = new ShaderMaterial();
            floorMat.Shader = _floorShader;
            floorMat.SetShaderParameter("color_a", floorColor);
            floorMat.SetShaderParameter("color_b", floorColor.Lightened(0.08f));
            floorMat.SetShaderParameter("tile_scale", Mathf.Max(wingW, wingH) / 2f);
            wingFloorMesh.MaterialOverride = floorMat;
            wingFloor.AddChild(wingFloorMesh);

            var wingFloorCol = new CollisionShape3D();
            wingFloorCol.Shape = new BoxShape3D { Size = new Vector3(wingW, 0.1f, wingH) };
            wingFloorCol.Position = new Vector3(0, -0.05f, 0);
            wingFloor.AddChild(wingFloorCol);

            // Wing walls — east, north (partial), south
            float wingHalfW = wingW / 2f;
            float wingHalfH = wingH / 2f;
            var wingOffset = new Vector3(wingCenterX, 0, wingCenterZ);

            // East wall of wing
            BuildWall(room, wingOffset + new Vector3(wingHalfW, wallHeight / 2f, 0),
                new Vector3(wallThickness, wallHeight, wingH), type);
            // North wall of wing
            BuildWall(room, wingOffset + new Vector3(0, wallHeight / 2f, -wingHalfH),
                new Vector3(wingW, wallHeight, wallThickness), type);
            // South wall of wing
            BuildWall(room, wingOffset + new Vector3(0, wallHeight / 2f, wingHalfH),
                new Vector3(wingW, wallHeight, wallThickness), type);

            // Nav mesh for wing
            AddNavRegion(room, new Vector2(wingW, wingH));
        }

        /// <summary>
        /// T-shaped room: adds a centered extension on one side.
        /// </summary>
        private static void AddTShapedWing(Node3D room, Vector2 size, RoomType type)
        {
            float mainHalfW = size.X / 2f;
            float mainHalfH = size.Y / 2f;

            float wingW = size.X * 0.4f;
            float wingH = size.Y * 0.35f;
            float wallHeight = 5f;
            float wallThickness = 0.5f;

            // Wing extends from the north side, centered
            float wingCenterX = 0;
            float wingCenterZ = -(mainHalfH + wingH / 2f);

            // Wing floor
            var wingFloor = new StaticBody3D();
            wingFloor.CollisionLayer = Constants.MASK_GROUND;
            wingFloor.Position = new Vector3(wingCenterX, 0, wingCenterZ);
            room.AddChild(wingFloor);

            var wingFloorMesh = new MeshInstance3D();
            var planeMesh = new PlaneMesh();
            planeMesh.Size = new Vector2(wingW, wingH);
            wingFloorMesh.Mesh = planeMesh;
            wingFloorMesh.Position = new Vector3(0, -0.05f, 0);
            var floorColor = GetFloorColor(type, _currentSector);
            var floorMat = new ShaderMaterial();
            floorMat.Shader = _floorShader;
            floorMat.SetShaderParameter("color_a", floorColor);
            floorMat.SetShaderParameter("color_b", floorColor.Lightened(0.08f));
            floorMat.SetShaderParameter("tile_scale", Mathf.Max(wingW, wingH) / 2f);
            wingFloorMesh.MaterialOverride = floorMat;
            wingFloor.AddChild(wingFloorMesh);

            var wingFloorCol = new CollisionShape3D();
            wingFloorCol.Shape = new BoxShape3D { Size = new Vector3(wingW, 0.1f, wingH) };
            wingFloorCol.Position = new Vector3(0, -0.05f, 0);
            wingFloor.AddChild(wingFloorCol);

            // Wing walls — north, east, west
            float wingHalfW = wingW / 2f;
            float wingHalfH = wingH / 2f;
            var wingOffset = new Vector3(wingCenterX, 0, wingCenterZ);

            // North wall of wing
            BuildWall(room, wingOffset + new Vector3(0, wallHeight / 2f, -wingHalfH),
                new Vector3(wingW, wallHeight, wallThickness), type);
            // East wall of wing
            BuildWall(room, wingOffset + new Vector3(wingHalfW, wallHeight / 2f, 0),
                new Vector3(wallThickness, wallHeight, wingH), type);
            // West wall of wing
            BuildWall(room, wingOffset + new Vector3(-wingHalfW, wallHeight / 2f, 0),
                new Vector3(wallThickness, wallHeight, wingH), type);

            // Nav mesh for wing
            AddNavRegion(room, new Vector2(wingW, wingH));
        }

        // Combat room size variants — picked deterministically per room
        private static readonly Vector2[] CombatSizes = new[]
        {
            new Vector2(28, 28),  // Small
            new Vector2(32, 32),  // Standard
            new Vector2(32, 32),  // Standard (weighted)
            new Vector2(36, 38),  // Large — open arena
            new Vector2(42, 42),  // Arena — with obstacles, more enemies
        };

        public static Vector2 GetRoomSize(RoomType type, int seed = 0) => type switch
        {
            RoomType.Boss => new Vector2(50, 50),
            RoomType.Megabonk => new Vector2(46, 46),
            RoomType.Treasure => new Vector2(22, 22),
            RoomType.Shop => new Vector2(26, 26),
            RoomType.SafeRoom => new Vector2(18, 18),
            RoomType.Entrance => new Vector2(24, 24),
            RoomType.Event => new Vector2(26, 26),
            RoomType.Puzzle => new Vector2(24, 24),
            RoomType.Combat => CombatSizes[((seed % CombatSizes.Length) + CombatSizes.Length) % CombatSizes.Length],
            _ => new Vector2(32, 32),
        };
    }
}
