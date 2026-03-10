using Godot;
using System.Collections.Generic;

namespace JunkbotArena
{
    /// <summary>
    /// Shippable-quality procedural mechanical backdrop.
    /// Creates a vast industrial facility interior: dark metal sub-floor with glowing grid lines
    /// fills gaps between rooms, overhead girders with hanging industrial lights illuminate the
    /// space, massive perimeter walls close in the arena, fire/sparks/steam/gears bring life,
    /// and AXIS patrol drones roam the darkness. SSAO + bloom for visual polish.
    /// </summary>
    public partial class DungeonBackdrop : Node3D
    {
        private WorldEnvironment _worldEnv;
        private Godot.Environment _env;
        private bool _appliedToCamera;
        private int _sector;
        private int _ascension;
        private float _danger; // 0-1 normalized
        private float _time;

        // ── Animated elements ──
        private readonly List<MeshInstance3D> _gears = new();
        private readonly List<float> _gearSpeeds = new();
        private struct PistonData { public Node3D Node; public float BaseY, Amplitude, Speed, Phase; }
        private readonly List<PistonData> _pistons = new();
        private struct DroneData { public Node3D Node; public float Radius, Speed, Y, Angle; }
        private readonly List<DroneData> _drones = new();
        private readonly List<OmniLight3D> _fireLights = new();
        private readonly List<float> _fireBaseEnergy = new();

        // AXIS Overseer
        public AXISPresence AXIS { get; private set; }

        // Ascension
        private OmniLight3D _abyssLight;
        private float _baseAmbientEnergy;
        private GpuParticles3D _stormParticles;

        // Dungeon grid: 20×20 at 32-unit spacing → center (320,0,320)
        private const float CX = 320f;
        private const float CZ = 320f;
        private const float RADIUS = 320f;
        private const float GRID_STEP = 32f;
        private const float SUB_FLOOR_Y = -4f;
        private const float OVERHEAD_Y = 22f;

        public void Initialize(SectorData sectorData)
        {
            _sector = sectorData.SectorNumber;
            _ascension = MetaSaveManager.Data.AscensionRank;
            _danger = Mathf.Clamp((_sector - 1) / 4f + _ascension * 0.1f, 0f, 1f);

            Position = new Vector3(CX, 0, CZ);

            BuildEnvironment(sectorData);
            BuildLighting(sectorData);
            BuildSubFloor(sectorData);
            BuildOverheadStructure(sectorData);
            BuildAbyssBelow(sectorData);
            BuildAbyssCracks(sectorData);
            BuildPerimeterWalls(sectorData);
            BuildSupportColumns(sectorData);
            BuildPipeNetwork(sectorData);
            BuildFurnaces(sectorData);
            BuildSparkEmitters(sectorData);
            BuildRotatingGears(sectorData);
            BuildPistons(sectorData);
            BuildHangingCables(sectorData);
            BuildSteamVents(sectorData);
            BuildSubFloorMachinery(sectorData);
            BuildAmbientParticles(sectorData);
            BuildAXISDrones(sectorData);
            BuildAXISPresence(sectorData);

            if (_ascension >= 1)
                BuildAscensionEffects(sectorData);

            GD.Print($"[DungeonBackdrop] Sector {_sector} built: danger={_danger:F2}, ascension={_ascension}, " +
                $"gears={_gears.Count}, pistons={_pistons.Count}, drones={_drones.Count}, fires={_fireLights.Count}");
        }

        // ═════════════════════════════════════════════════════════
        //  ENVIRONMENT — sky, SSAO, bloom, fog, tonemap
        // ═════════════════════════════════════════════════════════

        private void BuildEnvironment(SectorData sectorData)
        {
            var env = new Godot.Environment();

            // Industrial sky — dark overhead, accent-colored haze at horizon
            env.BackgroundMode = Godot.Environment.BGMode.Sky;
            var sky = new Sky();
            var skyMat = new ProceduralSkyMaterial();
            skyMat.SkyTopColor = GetSkyTopColor(sectorData);
            skyMat.SkyHorizonColor = GetHorizonColor(sectorData);
            skyMat.GroundHorizonColor = GetHorizonColor(sectorData);
            skyMat.GroundBottomColor = new Color(0.015f, 0.015f, 0.025f);
            skyMat.SunAngleMax = 0;
            skyMat.SunCurve = 0.01f;
            sky.SkyMaterial = skyMat;
            env.Sky = sky;

            // Ambient — bright enough to see backdrop structures
            env.AmbientLightSource = Godot.Environment.AmbientSource.Sky;
            _baseAmbientEnergy = Mathf.Lerp(0.7f, 0.4f, _danger);
            _baseAmbientEnergy = Mathf.Max(_baseAmbientEnergy, 0.3f);
            env.AmbientLightEnergy = _baseAmbientEnergy;

            // Tonemap — filmic for cinematic look
            env.TonemapMode = Godot.Environment.ToneMapper.Filmic;
            env.TonemapExposure = Mathf.Lerp(1.05f, 0.9f, _danger);
            env.TonemapWhite = 6f;

            // SSAO — adds depth to all the mechanical geometry
            env.SsaoEnabled = true;
            env.SsaoRadius = 2.5f;
            env.SsaoIntensity = 2.5f;

            // Glow/bloom — makes accent lights and fire bloom beautifully
            env.GlowEnabled = true;
            env.GlowIntensity = 0.8f + _danger * 0.5f;
            env.GlowStrength = 0.9f;
            env.GlowBloom = 0.15f + _danger * 0.15f;
            env.GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Softlight;
            env.GlowHdrThreshold = 0.7f;

            // Subtle fog — just enough for depth, not enough to obscure
            env.FogEnabled = true;
            var fogBase = GetHorizonColor(sectorData).Darkened(0.65f);
            env.FogLightColor = new Color(
                Mathf.Max(fogBase.R, 0.04f),
                Mathf.Max(fogBase.G, 0.04f),
                Mathf.Max(fogBase.B, 0.06f));
            env.FogDensity = 0.00015f + _danger * 0.0002f;
            env.FogSkyAffect = 0.05f;

            // Color adjustments — slight boost for visual punch
            env.AdjustmentEnabled = true;
            env.AdjustmentBrightness = 1.05f;
            env.AdjustmentContrast = 1.12f;
            env.AdjustmentSaturation = 1.08f;

            _env = env;

            // Remove stale WorldEnvironments from Root (use GetChildren() for safe iteration)
            foreach (var child in GetTree().Root.GetChildren())
            {
                if (!IsInstanceValid(child)) continue;
                if (child is WorldEnvironment old)
                {
                    old.GetParent()?.RemoveChild(old);
                    old.Free();
                }
            }
            _worldEnv = new WorldEnvironment();
            _worldEnv.Name = "WorldEnvironment";
            AddChild(_worldEnv);
            _worldEnv.Environment = env;
        }

        // ═════════════════════════════════════════════════════════
        //  LIGHTING — directional + hanging industrial fixtures
        // ═════════════════════════════════════════════════════════

        private void BuildLighting(SectorData sectorData)
        {
            // Main directional — overhead angled, warm-tinted
            var main = new DirectionalLight3D();
            main.LightColor = sectorData.TorchTint.Lerp(new Color(0.85f, 0.85f, 1f), 0.4f);
            main.LightEnergy = 0.5f + _danger * 0.15f;
            main.RotationDegrees = new Vector3(-55, -25, 0);
            main.ShadowEnabled = false;
            AddChild(main);

            // Fill light — softer, opposite angle
            var fill = new DirectionalLight3D();
            fill.LightColor = sectorData.AccentColor.Lerp(new Color(0.6f, 0.6f, 0.7f), 0.6f);
            fill.LightEnergy = 0.2f;
            fill.RotationDegrees = new Vector3(-40, 155, 0);
            fill.ShadowEnabled = false;
            AddChild(fill);

            // Hanging industrial light fixtures in a grid over the arena
            var rng = new RandomNumberGenerator();
            rng.Randomize();

            // Place lights in a grid pattern across the arena
            float spacing = GRID_STEP * 3; // every 3 grid cells = every 96 units
            int gridSize = (int)(RADIUS * 2f / spacing);
            float halfSpan = gridSize * spacing * 0.5f;

            for (int gx = 0; gx <= gridSize; gx++)
            {
                for (int gz = 0; gz <= gridSize; gz++)
                {
                    // Skip ~60% for variety
                    if (rng.Randf() < 0.6f) continue;

                    float x = -halfSpan + gx * spacing + rng.RandfRange(-8f, 8f);
                    float z = -halfSpan + gz * spacing + rng.RandfRange(-8f, 8f);
                    float y = OVERHEAD_Y - rng.RandfRange(2f, 6f);

                    // Light housing (rectangular box)
                    var housing = new MeshInstance3D();
                    var hBox = new BoxMesh();
                    hBox.Size = new Vector3(2.5f, 1f, 2.5f);
                    housing.Mesh = hBox;
                    housing.MaterialOverride = MakeMetalMat(
                        sectorData.WallTint.Darkened(0.5f), 0.7f, 0.4f);
                    housing.Position = new Vector3(x, y, z);
                    AddChild(housing);

                    // Glowing underside panel
                    var panel = new MeshInstance3D();
                    var pBox = new BoxMesh();
                    pBox.Size = new Vector3(2f, 0.15f, 2f);
                    panel.Mesh = pBox;
                    panel.Position = new Vector3(x, y - 0.5f, z);
                    panel.MaterialOverride = MakeGlowMat(
                        sectorData.TorchTint, 2f + _danger);
                    AddChild(panel);

                    // Spotlight pointing down
                    var spot = new SpotLight3D();
                    spot.LightColor = sectorData.TorchTint.Lerp(Colors.White, 0.3f);
                    spot.LightEnergy = rng.RandfRange(0.4f, 0.8f);
                    spot.SpotRange = 30f + rng.RandfRange(-5f, 10f);
                    spot.SpotAngle = 35f;
                    spot.RotationDegrees = new Vector3(-90, 0, 0);
                    spot.Position = new Vector3(x, y - 0.6f, z);
                    spot.ShadowEnabled = false;
                    AddChild(spot);
                }
            }
        }

        // ═════════════════════════════════════════════════════════
        //  SUB-FLOOR — industrial metal floor filling gaps between rooms
        // ═════════════════════════════════════════════════════════

        private void BuildSubFloor(SectorData sectorData)
        {
            // Large dark metal floor plane below room level
            float floorSize = RADIUS * 2.2f;
            var floor = new MeshInstance3D();
            var plane = new PlaneMesh();
            plane.Size = new Vector2(floorSize, floorSize);
            floor.Mesh = plane;
            floor.MaterialOverride = MakeMetalMat(
                new Color(0.08f, 0.08f, 0.10f), 0.75f, 0.55f);
            floor.Position = new Vector3(0, SUB_FLOOR_Y, 0);
            AddChild(floor);

            // Glowing grid lines at room-grid intervals (every 32 or 64 units)
            float gridLineWidth = 0.6f;
            float gridLineGlow = 2.5f + _danger * 1.5f;
            float extent = RADIUS + 20f;
            var accentDim = sectorData.AccentColor.Darkened(0.3f);

            // Major grid lines (every 64 units — every 2 room cells)
            float majorStep = GRID_STEP * 2f;
            var majorGlow = MakeGlowMat(sectorData.AccentColor, gridLineGlow);
            int majorCount = (int)(extent * 2f / majorStep) + 1;
            float majorStart = -((majorCount - 1) * majorStep * 0.5f);

            for (int i = 0; i < majorCount; i++)
            {
                float pos = majorStart + i * majorStep;

                // X-axis line
                var lineX = new MeshInstance3D();
                var bx = new BoxMesh();
                bx.Size = new Vector3(extent * 2f, 0.12f, gridLineWidth);
                lineX.Mesh = bx;
                lineX.MaterialOverride = majorGlow;
                lineX.Position = new Vector3(0, SUB_FLOOR_Y + 0.15f, pos);
                AddChild(lineX);

                // Z-axis line
                var lineZ = new MeshInstance3D();
                var bz = new BoxMesh();
                bz.Size = new Vector3(gridLineWidth, 0.12f, extent * 2f);
                lineZ.Mesh = bz;
                lineZ.MaterialOverride = majorGlow;
                lineZ.Position = new Vector3(pos, SUB_FLOOR_Y + 0.15f, 0);
                AddChild(lineZ);
            }

            // Minor grid lines (every 32 units — every room cell)
            var minorGlow = MakeGlowMat(accentDim, gridLineGlow * 0.4f);
            float minorWidth = 0.3f;
            int minorCount = (int)(extent * 2f / GRID_STEP) + 1;
            float minorStart = -((minorCount - 1) * GRID_STEP * 0.5f);

            for (int i = 0; i < minorCount; i++)
            {
                float pos = minorStart + i * GRID_STEP;

                // Skip positions that overlap major lines
                float majorAligned = (pos - majorStart) % majorStep;
                if (Mathf.Abs(majorAligned) < 1f || Mathf.Abs(majorAligned - majorStep) < 1f)
                    continue;

                var lineX = new MeshInstance3D();
                var bx = new BoxMesh();
                bx.Size = new Vector3(extent * 2f, 0.08f, minorWidth);
                lineX.Mesh = bx;
                lineX.MaterialOverride = minorGlow;
                lineX.Position = new Vector3(0, SUB_FLOOR_Y + 0.10f, pos);
                AddChild(lineX);

                var lineZ = new MeshInstance3D();
                var bz = new BoxMesh();
                bz.Size = new Vector3(minorWidth, 0.08f, extent * 2f);
                lineZ.Mesh = bz;
                lineZ.MaterialOverride = minorGlow;
                lineZ.Position = new Vector3(pos, SUB_FLOOR_Y + 0.10f, 0);
                AddChild(lineZ);
            }

            // Edge trim around the sub-floor (bright accent border)
            float halfSize = floorSize * 0.5f;
            var edgeGlow = MakeGlowMat(sectorData.AccentColor, gridLineGlow * 1.5f);
            float edgeWidth = 1.2f;
            float edgeHeight = 0.2f;

            // 4 edge strips
            AddEdgeStrip(edgeGlow, edgeWidth, edgeHeight, halfSize,
                new Vector3(0, SUB_FLOOR_Y + 0.2f, halfSize), true);
            AddEdgeStrip(edgeGlow, edgeWidth, edgeHeight, halfSize,
                new Vector3(0, SUB_FLOOR_Y + 0.2f, -halfSize), true);
            AddEdgeStrip(edgeGlow, edgeWidth, edgeHeight, halfSize,
                new Vector3(halfSize, SUB_FLOOR_Y + 0.2f, 0), false);
            AddEdgeStrip(edgeGlow, edgeWidth, edgeHeight, halfSize,
                new Vector3(-halfSize, SUB_FLOOR_Y + 0.2f, 0), false);
        }

        private void AddEdgeStrip(StandardMaterial3D mat, float width, float height,
            float halfLen, Vector3 pos, bool alongX)
        {
            var strip = new MeshInstance3D();
            var box = new BoxMesh();
            box.Size = alongX
                ? new Vector3(halfLen * 2f, height, width)
                : new Vector3(width, height, halfLen * 2f);
            strip.Mesh = box;
            strip.MaterialOverride = mat;
            strip.Position = pos;
            AddChild(strip);
        }

        // ═════════════════════════════════════════════════════════
        //  SUB-FLOOR MACHINERY — scattered industrial details between rooms
        // ═════════════════════════════════════════════════════════

        private void BuildSubFloorMachinery(SectorData sectorData)
        {
            var rng = new RandomNumberGenerator();
            rng.Randomize();

            var metalDark = sectorData.WallTint.Darkened(0.5f);
            metalDark.A = 1f;
            int detailCount = 30 + _sector * 5;

            for (int i = 0; i < detailCount; i++)
            {
                float x = rng.RandfRange(-RADIUS * 0.85f, RADIUS * 0.85f);
                float z = rng.RandfRange(-RADIUS * 0.85f, RADIUS * 0.85f);

                int type = rng.RandiRange(0, 3);
                switch (type)
                {
                    case 0: // Small vent grate
                    {
                        var grate = new MeshInstance3D();
                        var box = new BoxMesh();
                        float w = rng.RandfRange(1.5f, 3f);
                        box.Size = new Vector3(w, 0.25f, w);
                        grate.Mesh = box;
                        grate.MaterialOverride = MakeMetalMat(metalDark.Lightened(0.05f), 0.6f, 0.5f);
                        grate.Position = new Vector3(x, SUB_FLOOR_Y + 0.15f, z);
                        AddChild(grate);

                        // Glow inside vent
                        var glow = new MeshInstance3D();
                        var gBox = new BoxMesh();
                        gBox.Size = new Vector3(w * 0.6f, 0.1f, w * 0.6f);
                        glow.Mesh = gBox;
                        glow.MaterialOverride = MakeGlowMat(sectorData.AccentColor.Darkened(0.4f), 1f);
                        glow.Position = new Vector3(x, SUB_FLOOR_Y + 0.05f, z);
                        AddChild(glow);
                        break;
                    }
                    case 1: // Small pipe stub
                    {
                        var stub = new MeshInstance3D();
                        var cyl = new CylinderMesh();
                        cyl.TopRadius = rng.RandfRange(0.3f, 0.8f);
                        cyl.BottomRadius = cyl.TopRadius;
                        cyl.Height = rng.RandfRange(1f, 3f);
                        cyl.RadialSegments = 6;
                        stub.Mesh = cyl;
                        stub.MaterialOverride = MakeMetalMat(metalDark, 0.8f, 0.4f);
                        stub.Position = new Vector3(x, SUB_FLOOR_Y + cyl.Height * 0.5f, z);
                        AddChild(stub);
                        break;
                    }
                    case 2: // Junction box
                    {
                        var jbox = new MeshInstance3D();
                        var box = new BoxMesh();
                        float w = rng.RandfRange(1f, 2.5f);
                        float h = rng.RandfRange(0.8f, 2f);
                        box.Size = new Vector3(w, h, w);
                        jbox.Mesh = box;
                        jbox.MaterialOverride = MakeMetalMat(metalDark.Lightened(0.03f), 0.7f, 0.45f);
                        jbox.Position = new Vector3(x, SUB_FLOOR_Y + h * 0.5f, z);
                        AddChild(jbox);

                        // Small indicator light
                        if (rng.Randf() < 0.5f)
                        {
                            var indicator = new MeshInstance3D();
                            var sphere = new SphereMesh();
                            sphere.Radius = 0.12f;
                            sphere.Height = 0.24f;
                            sphere.RadialSegments = 4;
                            sphere.Rings = 2;
                            indicator.Mesh = sphere;
                            indicator.MaterialOverride = MakeGlowMat(
                                rng.Randf() < 0.5f ? sectorData.AccentColor : new Color(1f, 0.2f, 0.1f), 3f);
                            indicator.Position = new Vector3(x, SUB_FLOOR_Y + h + 0.15f, z);
                            AddChild(indicator);
                        }
                        break;
                    }
                    case 3: // Floor panel (raised section)
                    {
                        var panel = new MeshInstance3D();
                        var box = new BoxMesh();
                        float w = rng.RandfRange(3f, 8f);
                        float d = rng.RandfRange(3f, 8f);
                        box.Size = new Vector3(w, 0.5f, d);
                        panel.Mesh = box;
                        panel.MaterialOverride = MakeMetalMat(
                            new Color(0.1f, 0.1f, 0.12f), 0.7f, 0.5f);
                        panel.Position = new Vector3(x, SUB_FLOOR_Y + 0.25f, z);
                        panel.RotationDegrees = new Vector3(0, rng.RandfRange(0, 90), 0);
                        AddChild(panel);
                        break;
                    }
                }
            }
        }

        // ═════════════════════════════════════════════════════════
        //  OVERHEAD STRUCTURE — industrial girders and framework
        // ═════════════════════════════════════════════════════════

        private void BuildOverheadStructure(SectorData sectorData)
        {
            var rng = new RandomNumberGenerator();
            rng.Randomize();

            var girderColor = sectorData.WallTint.Darkened(0.55f);
            girderColor.A = 1f;
            var girderMat = MakeMetalMat(girderColor, 0.7f, 0.5f);

            float extent = RADIUS * 0.9f;

            // Main cross-beams spanning the arena (X direction)
            float beamSpacing = GRID_STEP * 4; // every 128 units
            int beamCount = (int)(extent * 2f / beamSpacing) + 1;
            float beamStart = -(beamCount - 1) * beamSpacing * 0.5f;

            for (int i = 0; i < beamCount; i++)
            {
                float z = beamStart + i * beamSpacing + rng.RandfRange(-4f, 4f);
                float y = OVERHEAD_Y + rng.RandfRange(-2f, 3f);

                // Main girder beam
                var beam = new MeshInstance3D();
                var box = new BoxMesh();
                box.Size = new Vector3(extent * 2f, 2.5f, 1.8f);
                beam.Mesh = box;
                beam.MaterialOverride = girderMat;
                beam.Position = new Vector3(0, y, z);
                AddChild(beam);

                // Bottom accent flange
                var flange = new MeshInstance3D();
                var fBox = new BoxMesh();
                fBox.Size = new Vector3(extent * 2f, 0.25f, 3f);
                flange.Mesh = fBox;
                flange.MaterialOverride = MakeGlowMat(
                    sectorData.AccentColor.Darkened(0.4f), 0.8f + _danger * 0.5f);
                flange.Position = new Vector3(0, y - 1.25f, z);
                AddChild(flange);
            }

            // Cross-beams in Z direction (fewer, creates grid pattern)
            int crossCount = beamCount - 1;
            float crossStart = beamStart;
            for (int i = 0; i < crossCount; i++)
            {
                if (rng.Randf() < 0.4f) continue; // skip some for variety

                float x = crossStart + (i + 0.5f) * beamSpacing + rng.RandfRange(-4f, 4f);
                float y = OVERHEAD_Y + rng.RandfRange(-1f, 2f);

                var beam = new MeshInstance3D();
                var box = new BoxMesh();
                box.Size = new Vector3(1.5f, 2f, extent * 2f);
                beam.Mesh = box;
                beam.MaterialOverride = girderMat;
                beam.Position = new Vector3(x, y, 0);
                AddChild(beam);
            }

            // Diagonal braces at intersections (adds structural detail)
            for (int i = 0; i < 8; i++)
            {
                float x = rng.RandfRange(-extent * 0.7f, extent * 0.7f);
                float z = rng.RandfRange(-extent * 0.7f, extent * 0.7f);
                float y = OVERHEAD_Y - rng.RandfRange(0f, 4f);

                var brace = new MeshInstance3D();
                var box = new BoxMesh();
                box.Size = new Vector3(0.6f, 0.6f, rng.RandfRange(15f, 35f));
                brace.Mesh = box;
                brace.MaterialOverride = girderMat;
                brace.Position = new Vector3(x, y, z);
                brace.RotationDegrees = new Vector3(
                    rng.RandfRange(-15f, 15f), rng.RandfRange(0, 180), 0);
                AddChild(brace);
            }
        }

        // ═════════════════════════════════════════════════════════
        //  ABYSS — deep void beyond the sub-floor edges
        // ═════════════════════════════════════════════════════════

        private void BuildAbyssBelow(SectorData sectorData)
        {
            // Deep dark plane far below
            var abyssFloor = new MeshInstance3D();
            var plane = new PlaneMesh();
            plane.Size = new Vector2(1600f, 1600f);
            abyssFloor.Mesh = plane;
            abyssFloor.MaterialOverride = MakeMetalMat(
                new Color(0.015f, 0.015f, 0.025f), 0.9f, 0.8f);
            abyssFloor.Position = new Vector3(0, -80f, 0);
            AddChild(abyssFloor);

            // Accent glow from deep below
            _abyssLight = new OmniLight3D();
            _abyssLight.LightColor = sectorData.AccentColor.Darkened(0.4f);
            _abyssLight.LightEnergy = 0.5f + _danger * 0.5f;
            _abyssLight.OmniRange = 300f;
            _abyssLight.OmniAttenuation = 2.5f;
            _abyssLight.Position = new Vector3(0, -40f, 0);
            AddChild(_abyssLight);
        }

        // ═════════════════════════════════════════════════════════
        //  ABYSS CRACKS — glowing energy lines on the deep floor
        // ═════════════════════════════════════════════════════════

        private void BuildAbyssCracks(SectorData sectorData)
        {
            var rng = new RandomNumberGenerator();
            rng.Randomize();

            int crackCount = 15 + _sector * 3;
            var glowMat = MakeGlowMat(sectorData.AccentColor.Darkened(0.2f), 1.5f + _danger);

            for (int i = 0; i < crackCount; i++)
            {
                float x = rng.RandfRange(-RADIUS * 0.7f, RADIUS * 0.7f);
                float z = rng.RandfRange(-RADIUS * 0.7f, RADIUS * 0.7f);
                float length = rng.RandfRange(10f, 50f);
                float angle = rng.RandfRange(0, Mathf.Pi);

                var crack = new MeshInstance3D();
                var box = new BoxMesh();
                box.Size = new Vector3(length, 0.2f, rng.RandfRange(0.3f, 0.8f));
                crack.Mesh = box;
                crack.MaterialOverride = glowMat;
                crack.Position = new Vector3(x, -79.5f, z);
                crack.RotationDegrees = new Vector3(0, Mathf.RadToDeg(angle), 0);
                AddChild(crack);

                if (rng.Randf() < 0.45f)
                {
                    var branch = new MeshInstance3D();
                    var bBox = new BoxMesh();
                    bBox.Size = new Vector3(length * rng.RandfRange(0.3f, 0.6f), 0.2f, 0.4f);
                    branch.Mesh = bBox;
                    branch.MaterialOverride = glowMat;
                    branch.Position = new Vector3(
                        x + rng.RandfRange(-4f, 4f), -79.5f,
                        z + rng.RandfRange(-4f, 4f));
                    branch.RotationDegrees = new Vector3(
                        0, Mathf.RadToDeg(angle) + rng.RandfRange(30, 90), 0);
                    AddChild(branch);
                }
            }
        }

        // ═════════════════════════════════════════════════════════
        //  PERIMETER WALLS — massive mechanical walls ringing the arena
        // ═════════════════════════════════════════════════════════

        private void BuildPerimeterWalls(SectorData sectorData)
        {
            var rng = new RandomNumberGenerator();
            rng.Randomize();

            float wallDist = RADIUS + 35f; // closer to the action
            int segmentsPerSide = 10;
            float segmentWidth = (wallDist * 2f) / segmentsPerSide;
            float baseWallHeight = 80f + _danger * 30f;

            var darkMetal = sectorData.WallTint.Darkened(0.5f);
            darkMetal.A = 1f;

            for (int side = 0; side < 4; side++)
            {
                for (int seg = 0; seg < segmentsPerSide; seg++)
                {
                    float t = (seg + 0.5f) / segmentsPerSide - 0.5f;
                    float along = t * wallDist * 2f;

                    float x, z, rotY;
                    switch (side)
                    {
                        case 0: x = along; z = -wallDist; rotY = 0; break;
                        case 1: x = along; z = wallDist; rotY = 180; break;
                        case 2: x = -wallDist; z = along; rotY = 90; break;
                        default: x = wallDist; z = along; rotY = -90; break;
                    }

                    float h = baseWallHeight * rng.RandfRange(0.75f, 1.35f);
                    var wall = CreateWallSegment(h, segmentWidth * 0.96f, darkMetal, sectorData, rng);
                    wall.Position = new Vector3(x, h * 0.5f - 20f, z);
                    wall.RotationDegrees = new Vector3(0, rotY, 0);
                    AddChild(wall);
                }
            }
        }

        private Node3D CreateWallSegment(float height, float width, Color darkColor,
            SectorData sectorData, RandomNumberGenerator rng)
        {
            var root = new Node3D();
            float depth = rng.RandfRange(10f, 22f);

            // Main slab — lit metal
            var slab = new MeshInstance3D();
            var box = new BoxMesh();
            box.Size = new Vector3(width, height, depth);
            slab.Mesh = box;
            slab.MaterialOverride = MakeMetalMat(darkColor, 0.65f, 0.5f);
            root.AddChild(slab);

            // Horizontal accent stripes (2-3)
            int stripeCount = rng.RandiRange(2, 3);
            for (int s = 0; s < stripeCount; s++)
            {
                float stripeY = height * (s * 0.25f - 0.2f) + rng.RandfRange(-2f, 2f);
                var stripe = new MeshInstance3D();
                var sBox = new BoxMesh();
                sBox.Size = new Vector3(width + 0.5f, 0.6f, depth + 0.5f);
                stripe.Mesh = sBox;
                stripe.Position = new Vector3(0, stripeY, 0);
                stripe.MaterialOverride = MakeGlowMat(
                    sectorData.AccentColor, 2f + _danger * 2f + s * 0.3f);
                root.AddChild(stripe);
            }

            // Vertical accent lines (1-3)
            int vLineCount = rng.RandiRange(1, 3);
            for (int v = 0; v < vLineCount; v++)
            {
                var vline = new MeshInstance3D();
                var vbox = new BoxMesh();
                vbox.Size = new Vector3(0.4f, height * 0.9f, depth + 0.3f);
                vline.Mesh = vbox;
                vline.Position = new Vector3(
                    rng.RandfRange(-width * 0.35f, width * 0.35f), 0, 0);
                vline.MaterialOverride = MakeGlowMat(
                    sectorData.AccentColor.Darkened(0.15f), 1.2f + _danger);
                root.AddChild(vline);
            }

            // Protruding mechanical blocks (1-2)
            int blockCount = rng.RandiRange(0, 2);
            for (int b = 0; b < blockCount; b++)
            {
                var block = new MeshInstance3D();
                var bBox = new BoxMesh();
                float bw = rng.RandfRange(4f, width * 0.4f);
                float bh = rng.RandfRange(6f, height * 0.35f);
                bBox.Size = new Vector3(bw, bh, depth + rng.RandfRange(4f, 12f));
                block.Mesh = bBox;
                block.Position = new Vector3(
                    rng.RandfRange(-width * 0.3f, width * 0.3f),
                    rng.RandfRange(-height * 0.2f, height * 0.2f), 0);
                block.MaterialOverride = MakeMetalMat(darkColor.Lightened(0.06f), 0.7f, 0.45f);
                root.AddChild(block);
            }

            // Glowing window/vent opening (40% chance)
            if (rng.Randf() < 0.4f)
            {
                float ww = rng.RandfRange(3f, 6f);
                float wh = rng.RandfRange(2f, 4f);
                var window = new MeshInstance3D();
                var wBox = new BoxMesh();
                wBox.Size = new Vector3(ww, wh, 0.4f);
                window.Mesh = wBox;
                window.Position = new Vector3(
                    rng.RandfRange(-width * 0.2f, width * 0.2f),
                    rng.RandfRange(height * 0.05f, height * 0.35f),
                    depth * 0.5f + 0.25f);
                window.MaterialOverride = MakeGlowMat(
                    sectorData.TorchTint.Darkened(0.15f), 1.5f + _danger);
                root.AddChild(window);
            }

            return root;
        }

        // ═════════════════════════════════════════════════════════
        //  SUPPORT COLUMNS — connecting sub-floor down to abyss
        // ═════════════════════════════════════════════════════════

        private void BuildSupportColumns(SectorData sectorData)
        {
            var rng = new RandomNumberGenerator();
            rng.Randomize();

            var darkMetal = sectorData.WallTint.Darkened(0.5f);
            darkMetal.A = 1f;
            int colCount = 22 + _sector * 4;

            for (int i = 0; i < colCount; i++)
            {
                float x = rng.RandfRange(-RADIUS * 0.85f, RADIUS * 0.85f);
                float z = rng.RandfRange(-RADIUS * 0.85f, RADIUS * 0.85f);
                float colHeight = rng.RandfRange(50f, 100f);
                float colWidth = rng.RandfRange(2.5f, 5.5f);

                var col = new Node3D();

                // Main shaft
                var shaft = new MeshInstance3D();
                var box = new BoxMesh();
                box.Size = new Vector3(colWidth, colHeight, colWidth);
                shaft.Mesh = box;
                shaft.MaterialOverride = MakeMetalMat(darkMetal, 0.7f, 0.5f);
                shaft.Position = new Vector3(0, -colHeight * 0.5f, 0);
                col.AddChild(shaft);

                // Top cap
                var cap = new MeshInstance3D();
                var capBox = new BoxMesh();
                capBox.Size = new Vector3(colWidth + 2.5f, 1.8f, colWidth + 2.5f);
                cap.Mesh = capBox;
                cap.MaterialOverride = MakeMetalMat(darkMetal.Lightened(0.07f), 0.75f, 0.4f);
                col.AddChild(cap);

                // Accent ring near top
                var ring = new MeshInstance3D();
                var ringBox = new BoxMesh();
                ringBox.Size = new Vector3(colWidth + 3f, 0.5f, colWidth + 3f);
                ring.Mesh = ringBox;
                ring.Position = new Vector3(0, -1.5f, 0);
                ring.MaterialOverride = MakeGlowMat(sectorData.AccentColor, 1.8f + _danger);
                col.AddChild(ring);

                // Lower ring (60% chance)
                if (rng.Randf() < 0.6f)
                {
                    var ring2 = new MeshInstance3D();
                    ring2.Mesh = ringBox;
                    ring2.Position = new Vector3(0, -colHeight * rng.RandfRange(0.3f, 0.6f), 0);
                    ring2.MaterialOverride = MakeGlowMat(
                        sectorData.AccentColor.Darkened(0.2f), 1f + _danger * 0.5f);
                    col.AddChild(ring2);
                }

                col.Position = new Vector3(x, SUB_FLOOR_Y, z);
                AddChild(col);
            }
        }

        // ═════════════════════════════════════════════════════════
        //  PIPE NETWORK — conduits connecting structures
        // ═════════════════════════════════════════════════════════

        private void BuildPipeNetwork(SectorData sectorData)
        {
            var rng = new RandomNumberGenerator();
            rng.Randomize();

            var pipeMetal = sectorData.WallTint.Darkened(0.4f);
            pipeMetal.A = 1f;
            int pipeCount = 18 + _sector * 4;

            for (int i = 0; i < pipeCount; i++)
            {
                float x1 = rng.RandfRange(-RADIUS * 0.9f, RADIUS * 0.9f);
                float z1 = rng.RandfRange(-RADIUS * 0.9f, RADIUS * 0.9f);
                float x2 = x1 + rng.RandfRange(-100f, 100f);
                float z2 = z1 + rng.RandfRange(-100f, 100f);
                float y = rng.RandfRange(SUB_FLOOR_Y - 25f, SUB_FLOOR_Y - 2f);

                var from = new Vector3(x1, y, z1);
                var to = new Vector3(x2, y + rng.RandfRange(-8f, 8f), z2);
                float length = from.DistanceTo(to);
                if (length < 15f) continue;

                float thickness = rng.RandfRange(0.5f, 1.8f);

                var pipe = new MeshInstance3D();
                var cyl = new CylinderMesh();
                cyl.TopRadius = thickness;
                cyl.BottomRadius = thickness;
                cyl.Height = length;
                cyl.RadialSegments = 6;
                pipe.Mesh = cyl;
                pipe.MaterialOverride = MakeMetalMat(pipeMetal, 0.8f, 0.4f);

                var mid = (from + to) * 0.5f;
                pipe.Position = mid;
                AddChild(pipe);
                pipe.LookAt(to, Vector3.Up);
                pipe.RotateObjectLocal(Vector3.Right, Mathf.DegToRad(90f));

                // Junction sphere (45% chance)
                if (rng.Randf() < 0.45f)
                {
                    var joint = new MeshInstance3D();
                    var sphere = new SphereMesh();
                    sphere.Radius = thickness * 2.5f;
                    sphere.Height = thickness * 5f;
                    sphere.RadialSegments = 8;
                    sphere.Rings = 4;
                    joint.Mesh = sphere;
                    joint.Position = mid;
                    joint.MaterialOverride = MakeGlowMat(
                        sectorData.AccentColor, 1.2f + _danger * 0.8f);
                    AddChild(joint);
                }
            }
        }

        // ═════════════════════════════════════════════════════════
        //  FURNACES — fire pits along walls with fire particles
        // ═════════════════════════════════════════════════════════

        private void BuildFurnaces(SectorData sectorData)
        {
            var rng = new RandomNumberGenerator();
            rng.Randomize();

            float wallDist = RADIUS + 28f;
            int furnaceCount = 10 + _sector * 2;

            for (int i = 0; i < furnaceCount; i++)
            {
                float angle = (float)i / furnaceCount * Mathf.Tau + rng.RandfRange(-0.12f, 0.12f);
                float x = Mathf.Cos(angle) * wallDist;
                float z = Mathf.Sin(angle) * wallDist;
                float y = rng.RandfRange(4f, 20f);

                // Furnace opening (bright orange-white glow)
                var opening = new MeshInstance3D();
                var oBox = new BoxMesh();
                oBox.Size = new Vector3(4f, 3f, 0.6f);
                opening.Mesh = oBox;
                opening.MaterialOverride = MakeGlowMat(
                    new Color(1f, 0.45f, 0.08f), 3.5f + _danger * 2f);
                opening.Position = new Vector3(x, y, z);
                AddChild(opening);
                opening.LookAt(new Vector3(0, y, 0), Vector3.Up);

                // Fire particles
                var fire = CreateFireParticles();
                fire.Position = new Vector3(x * 0.96f, y, z * 0.96f);
                AddChild(fire);

                // Warm omni light (flickered in _Process)
                var light = new OmniLight3D();
                light.LightColor = new Color(1f, 0.5f, 0.12f);
                float baseE = rng.RandfRange(1.0f, 1.8f);
                light.LightEnergy = baseE;
                light.OmniRange = 30f + _danger * 12f;
                light.OmniAttenuation = 1.3f;
                light.Position = new Vector3(x * 0.94f, y + 1.5f, z * 0.94f);
                light.ShadowEnabled = false;
                AddChild(light);
                _fireLights.Add(light);
                _fireBaseEnergy.Add(baseE);
            }

            // Also scatter a few fire pits on the sub-floor
            int floorFires = 4 + _sector;
            for (int i = 0; i < floorFires; i++)
            {
                float x = rng.RandfRange(-RADIUS * 0.6f, RADIUS * 0.6f);
                float z = rng.RandfRange(-RADIUS * 0.6f, RADIUS * 0.6f);

                // Fire pit basin
                var basin = new MeshInstance3D();
                var bCyl = new CylinderMesh();
                bCyl.TopRadius = 2f;
                bCyl.BottomRadius = 1.5f;
                bCyl.Height = 1f;
                bCyl.RadialSegments = 8;
                basin.Mesh = bCyl;
                basin.MaterialOverride = MakeMetalMat(
                    sectorData.WallTint.Darkened(0.6f), 0.8f, 0.5f);
                basin.Position = new Vector3(x, SUB_FLOOR_Y + 0.5f, z);
                AddChild(basin);

                // Fire inside
                var fire = CreateFireParticles();
                fire.Position = new Vector3(x, SUB_FLOOR_Y + 1.5f, z);
                AddChild(fire);

                // Light
                var light = new OmniLight3D();
                light.LightColor = new Color(1f, 0.5f, 0.12f);
                float baseE = rng.RandfRange(0.8f, 1.2f);
                light.LightEnergy = baseE;
                light.OmniRange = 20f;
                light.OmniAttenuation = 1.5f;
                light.Position = new Vector3(x, SUB_FLOOR_Y + 3f, z);
                light.ShadowEnabled = false;
                AddChild(light);
                _fireLights.Add(light);
                _fireBaseEnergy.Add(baseE);
            }
        }

        private GpuParticles3D CreateFireParticles()
        {
            var fire = new GpuParticles3D();
            fire.Amount = 32;
            fire.Lifetime = 1.0f;
            fire.Preprocess = 0.3f;
            fire.VisibilityAabb = new Aabb(new Vector3(-4, -1, -4), new Vector3(8, 10, 8));

            var pmat = new ParticleProcessMaterial();
            pmat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
            pmat.EmissionBoxExtents = new Vector3(0.8f, 0.2f, 0.8f);
            pmat.Direction = new Vector3(0, 1, 0);
            pmat.Spread = 12f;
            pmat.InitialVelocityMin = 2f;
            pmat.InitialVelocityMax = 5f;
            pmat.Gravity = new Vector3(0, 1.5f, 0);
            pmat.ScaleMin = 0.2f;
            pmat.ScaleMax = 0.6f;

            var colorRamp = new Gradient();
            colorRamp.SetColor(0, new Color(1f, 0.95f, 0.5f, 1f));
            colorRamp.AddPoint(0.2f, new Color(1f, 0.6f, 0.1f, 0.9f));
            colorRamp.AddPoint(0.6f, new Color(0.8f, 0.2f, 0.03f, 0.6f));
            colorRamp.SetColor(1, new Color(0.3f, 0.05f, 0.02f, 0f));
            var gradTex = new GradientTexture1D();
            gradTex.Gradient = colorRamp;
            pmat.ColorRamp = gradTex;

            fire.ProcessMaterial = pmat;

            var mesh = new SphereMesh();
            mesh.Radius = 0.18f;
            mesh.Height = 0.36f;
            mesh.RadialSegments = 4;
            mesh.Rings = 2;
            var mat = new StandardMaterial3D();
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.EmissionEnabled = true;
            mat.Emission = new Color(1f, 0.6f, 0.15f);
            mat.EmissionEnergyMultiplier = 4f;
            mat.VertexColorUseAsAlbedo = true;
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mesh.Material = mat;
            fire.DrawPass1 = mesh;

            return fire;
        }

        // ═════════════════════════════════════════════════════════
        //  SPARK EMITTERS — bright welding sparks
        // ═════════════════════════════════════════════════════════

        private void BuildSparkEmitters(SectorData sectorData)
        {
            var rng = new RandomNumberGenerator();
            rng.Randomize();

            int sparkCount = 12 + _sector * 3;

            for (int i = 0; i < sparkCount; i++)
            {
                // Place sparks near sub-floor level and walls — where they're visible
                float x, z, y;
                if (rng.Randf() < 0.4f)
                {
                    // Near perimeter walls
                    float angle = rng.RandfRange(0, Mathf.Tau);
                    float dist = RADIUS + rng.RandfRange(15f, 30f);
                    x = Mathf.Cos(angle) * dist;
                    z = Mathf.Sin(angle) * dist;
                    y = rng.RandfRange(0f, 15f);
                }
                else
                {
                    // Scattered on sub-floor
                    x = rng.RandfRange(-RADIUS * 0.8f, RADIUS * 0.8f);
                    z = rng.RandfRange(-RADIUS * 0.8f, RADIUS * 0.8f);
                    y = SUB_FLOOR_Y + rng.RandfRange(0.5f, 3f);
                }

                var sparks = new GpuParticles3D();
                sparks.Amount = 16;
                sparks.Lifetime = 0.5f;
                sparks.Preprocess = 0.1f;
                sparks.VisibilityAabb = new Aabb(new Vector3(-6, -6, -6), new Vector3(12, 12, 12));

                var pmat = new ParticleProcessMaterial();
                pmat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
                pmat.EmissionSphereRadius = 0.2f;
                pmat.Direction = new Vector3(0, 1, 0);
                pmat.Spread = 85f;
                pmat.InitialVelocityMin = 4f;
                pmat.InitialVelocityMax = 10f;
                pmat.Gravity = new Vector3(0, -14f, 0);
                pmat.ScaleMin = 0.03f;
                pmat.ScaleMax = 0.09f;

                var grad = new Gradient();
                grad.SetColor(0, new Color(1f, 1f, 0.9f, 1f));
                grad.AddPoint(0.3f, new Color(1f, 0.75f, 0.3f, 0.9f));
                grad.SetColor(1, new Color(0.9f, 0.35f, 0.05f, 0f));
                var tex = new GradientTexture1D();
                tex.Gradient = grad;
                pmat.ColorRamp = tex;

                sparks.ProcessMaterial = pmat;

                var mesh = new SphereMesh();
                mesh.Radius = 0.04f;
                mesh.Height = 0.08f;
                mesh.RadialSegments = 3;
                mesh.Rings = 2;
                var mat = new StandardMaterial3D();
                mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                mat.EmissionEnabled = true;
                mat.Emission = new Color(1f, 0.9f, 0.5f);
                mat.EmissionEnergyMultiplier = 8f;
                mat.VertexColorUseAsAlbedo = true;
                mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                mesh.Material = mat;
                sparks.DrawPass1 = mesh;

                sparks.Position = new Vector3(x, y, z);
                AddChild(sparks);
            }
        }

        // ═════════════════════════════════════════════════════════
        //  ROTATING GEARS — embedded in perimeter walls
        // ═════════════════════════════════════════════════════════

        private void BuildRotatingGears(SectorData sectorData)
        {
            var rng = new RandomNumberGenerator();
            rng.Randomize();

            int gearCount = 10 + _sector * 2;
            float wallDist = RADIUS + 30f;
            var metalColor = sectorData.WallTint.Darkened(0.35f);
            metalColor.A = 1f;

            for (int i = 0; i < gearCount; i++)
            {
                float angle = rng.RandfRange(0, Mathf.Tau);
                float x = Mathf.Cos(angle) * wallDist;
                float z = Mathf.Sin(angle) * wallDist;
                float y = rng.RandfRange(-5f, 35f);
                float gearRadius = rng.RandfRange(4f, 10f);

                // Gear disc
                var gear = new MeshInstance3D();
                var cyl = new CylinderMesh();
                cyl.TopRadius = gearRadius;
                cyl.BottomRadius = gearRadius;
                cyl.Height = 1.2f;
                cyl.RadialSegments = 16;
                gear.Mesh = cyl;
                gear.MaterialOverride = MakeMetalMat(metalColor, 0.85f, 0.3f);
                gear.Position = new Vector3(x, y, z);
                AddChild(gear);
                if (gear.IsInsideTree())
                    gear.LookAt(new Vector3(0, y, 0), Vector3.Up);
                else
                    gear.LookAtFromPosition(gear.Position, new Vector3(0, y, 0), Vector3.Up);
                gear.RotateObjectLocal(Vector3.Right, Mathf.DegToRad(90f));
                _gears.Add(gear);
                _gearSpeeds.Add(rng.RandfRange(0.15f, 0.6f) * (rng.Randf() < 0.5f ? 1f : -1f));

                // Hub accent
                var hub = new MeshInstance3D();
                var hubCyl = new CylinderMesh();
                hubCyl.TopRadius = gearRadius * 0.2f;
                hubCyl.BottomRadius = gearRadius * 0.2f;
                hubCyl.Height = 1.8f;
                hubCyl.RadialSegments = 8;
                hub.Mesh = hubCyl;
                hub.MaterialOverride = MakeGlowMat(sectorData.AccentColor, 2f + _danger);
                gear.AddChild(hub);

                // Outer rim accent ring
                var rim = new MeshInstance3D();
                var rimCyl = new CylinderMesh();
                rimCyl.TopRadius = gearRadius * 0.95f;
                rimCyl.BottomRadius = gearRadius * 0.95f;
                rimCyl.Height = 0.3f;
                rimCyl.RadialSegments = 16;
                rim.Mesh = rimCyl;
                rim.Position = new Vector3(0, 0.7f, 0);
                rim.MaterialOverride = MakeGlowMat(
                    sectorData.AccentColor.Darkened(0.3f), 1f + _danger * 0.5f);
                gear.AddChild(rim);
            }
        }

        // ═════════════════════════════════════════════════════════
        //  PISTONS — oscillating mechanical columns
        // ═════════════════════════════════════════════════════════

        private void BuildPistons(SectorData sectorData)
        {
            var rng = new RandomNumberGenerator();
            rng.Randomize();

            int pistonCount = 12 + _sector * 2;
            var metalColor = sectorData.WallTint.Darkened(0.45f);
            metalColor.A = 1f;

            for (int i = 0; i < pistonCount; i++)
            {
                float x = rng.RandfRange(-RADIUS * 0.8f, RADIUS * 0.8f);
                float z = rng.RandfRange(-RADIUS * 0.8f, RADIUS * 0.8f);
                float baseY = SUB_FLOOR_Y + rng.RandfRange(-15f, -3f);
                float pistonHeight = rng.RandfRange(6f, 18f);
                float pistonWidth = rng.RandfRange(1.2f, 3f);

                var piston = new Node3D();

                // Cylinder shaft
                var shaft = new MeshInstance3D();
                var cyl = new CylinderMesh();
                cyl.TopRadius = pistonWidth * 0.5f;
                cyl.BottomRadius = pistonWidth * 0.5f;
                cyl.Height = pistonHeight;
                cyl.RadialSegments = 8;
                shaft.Mesh = cyl;
                shaft.MaterialOverride = MakeMetalMat(metalColor, 0.85f, 0.3f);
                piston.AddChild(shaft);

                // Accent top plate
                var plate = new MeshInstance3D();
                var plateBox = new BoxMesh();
                plateBox.Size = new Vector3(pistonWidth * 2.2f, 0.7f, pistonWidth * 2.2f);
                plate.Mesh = plateBox;
                plate.Position = new Vector3(0, pistonHeight * 0.5f, 0);
                plate.MaterialOverride = MakeGlowMat(sectorData.AccentColor, 1.2f + _danger);
                piston.AddChild(plate);

                // Housing base
                var housing = new MeshInstance3D();
                var hBox = new BoxMesh();
                hBox.Size = new Vector3(pistonWidth * 2.8f, 2.5f, pistonWidth * 2.8f);
                housing.Mesh = hBox;
                housing.Position = new Vector3(0, -pistonHeight * 0.5f - 1.2f, 0);
                housing.MaterialOverride = MakeMetalMat(metalColor.Lightened(0.04f), 0.7f, 0.5f);
                piston.AddChild(housing);

                piston.Position = new Vector3(x, baseY, z);
                AddChild(piston);

                _pistons.Add(new PistonData
                {
                    Node = piston,
                    BaseY = baseY,
                    Amplitude = rng.RandfRange(2f, 6f),
                    Speed = rng.RandfRange(0.4f, 1.3f),
                    Phase = rng.RandfRange(0, Mathf.Tau),
                });
            }
        }

        // ═════════════════════════════════════════════════════════
        //  HANGING CABLES — vertical atmosphere detail
        // ═════════════════════════════════════════════════════════

        private void BuildHangingCables(SectorData sectorData)
        {
            var rng = new RandomNumberGenerator();
            rng.Randomize();

            int cableCount = 25 + _sector * 3;
            var cableColor = sectorData.WallTint.Darkened(0.35f);
            cableColor.A = 1f;

            for (int i = 0; i < cableCount; i++)
            {
                float x = rng.RandfRange(-RADIUS * 0.9f, RADIUS * 0.9f);
                float z = rng.RandfRange(-RADIUS * 0.9f, RADIUS * 0.9f);
                float topY = OVERHEAD_Y + rng.RandfRange(-5f, 0f);
                float cableLen = rng.RandfRange(10f, 35f);
                float thickness = rng.RandfRange(0.06f, 0.25f);

                var cable = new MeshInstance3D();
                var cyl = new CylinderMesh();
                cyl.TopRadius = thickness;
                cyl.BottomRadius = thickness;
                cyl.Height = cableLen;
                cyl.RadialSegments = 4;
                cable.Mesh = cyl;
                cable.MaterialOverride = MakeMetalMat(cableColor, 0.3f, 0.7f);
                cable.Position = new Vector3(x, topY - cableLen * 0.5f, z);
                AddChild(cable);

                // Accent light bulb at cable end (25%)
                if (rng.Randf() < 0.25f)
                {
                    var bulb = new MeshInstance3D();
                    var sphere = new SphereMesh();
                    sphere.Radius = 0.35f;
                    sphere.Height = 0.7f;
                    sphere.RadialSegments = 4;
                    sphere.Rings = 2;
                    bulb.Mesh = sphere;
                    bulb.Position = new Vector3(x, topY - cableLen, z);
                    bulb.MaterialOverride = MakeGlowMat(sectorData.AccentColor, 2f);
                    AddChild(bulb);
                }
            }
        }

        // ═════════════════════════════════════════════════════════
        //  STEAM VENTS — floor-level steam bursts
        // ═════════════════════════════════════════════════════════

        private void BuildSteamVents(SectorData sectorData)
        {
            var rng = new RandomNumberGenerator();
            rng.Randomize();
            int ventCount = 6 + _sector;

            for (int i = 0; i < ventCount; i++)
            {
                float x = rng.RandfRange(-RADIUS * 0.7f, RADIUS * 0.7f);
                float z = rng.RandfRange(-RADIUS * 0.7f, RADIUS * 0.7f);

                // Vent grate on sub-floor
                var grate = new MeshInstance3D();
                var grateBox = new BoxMesh();
                grateBox.Size = new Vector3(2.5f, 0.35f, 2.5f);
                grate.Mesh = grateBox;
                grate.MaterialOverride = MakeMetalMat(
                    sectorData.WallTint.Darkened(0.25f), 0.6f, 0.5f);
                grate.Position = new Vector3(x, SUB_FLOOR_Y + 0.18f, z);
                AddChild(grate);

                // Steam particles
                var steam = new GpuParticles3D();
                steam.Amount = 20;
                steam.Lifetime = 2.5f;
                steam.Preprocess = 0.5f;
                steam.VisibilityAabb = new Aabb(new Vector3(-4, -1, -4), new Vector3(8, 15, 8));

                var pmat = new ParticleProcessMaterial();
                pmat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
                pmat.EmissionBoxExtents = new Vector3(0.6f, 0.1f, 0.6f);
                pmat.Direction = new Vector3(0, 1, 0);
                pmat.Spread = 18f;
                pmat.InitialVelocityMin = 2.5f;
                pmat.InitialVelocityMax = 6f;
                pmat.Gravity = new Vector3(0, 0.2f, 0);
                pmat.ScaleMin = 0.25f;
                pmat.ScaleMax = 0.7f;

                var grad = new Gradient();
                grad.SetColor(0, new Color(0.85f, 0.9f, 0.95f, 0.5f));
                grad.AddPoint(0.4f, new Color(0.65f, 0.7f, 0.75f, 0.3f));
                grad.SetColor(1, new Color(0.45f, 0.5f, 0.55f, 0f));
                var tex = new GradientTexture1D();
                tex.Gradient = grad;
                pmat.ColorRamp = tex;

                steam.ProcessMaterial = pmat;

                var mesh = new SphereMesh();
                mesh.Radius = 0.25f;
                mesh.Height = 0.5f;
                mesh.RadialSegments = 4;
                mesh.Rings = 2;
                var mat = new StandardMaterial3D();
                mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                mat.VertexColorUseAsAlbedo = true;
                mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                mesh.Material = mat;
                steam.DrawPass1 = mesh;

                steam.Position = new Vector3(x, SUB_FLOOR_Y + 0.5f, z);
                AddChild(steam);
            }
        }

        // ═════════════════════════════════════════════════════════
        //  AMBIENT PARTICLES — floating energy motes
        // ═════════════════════════════════════════════════════════

        private void BuildAmbientParticles(SectorData sectorData)
        {
            var particles = new GpuParticles3D();
            particles.Amount = 100 + (int)(_danger * 120);
            particles.Lifetime = 8f;
            particles.Preprocess = 4f;

            float ext = RADIUS * 0.8f;
            particles.VisibilityAabb = new Aabb(
                new Vector3(-ext, -30, -ext),
                new Vector3(ext * 2, 60, ext * 2));

            var pmat = new ParticleProcessMaterial();
            pmat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
            pmat.EmissionBoxExtents = new Vector3(ext, 25, ext);
            pmat.Direction = new Vector3(0, 1, 0);
            pmat.Spread = 45f;
            pmat.InitialVelocityMin = 0.1f;
            pmat.InitialVelocityMax = 0.4f;
            pmat.Gravity = new Vector3(0, 0.02f, 0);
            pmat.ScaleMin = 0.05f;
            pmat.ScaleMax = 0.18f + _danger * 0.08f;

            var color = GetParticleColor(sectorData);
            pmat.Color = color;
            particles.ProcessMaterial = pmat;

            var mesh = new SphereMesh();
            mesh.Radius = 0.06f;
            mesh.Height = 0.12f;
            mesh.RadialSegments = 4;
            mesh.Rings = 2;
            mesh.Material = MakeGlowMat(color, 2f + _danger);
            particles.DrawPass1 = mesh;

            particles.Position = new Vector3(0, 0, 0);
            AddChild(particles);
        }

        // ═════════════════════════════════════════════════════════
        //  AXIS PATROL DRONES — menacing shapes orbiting the arena
        // ═════════════════════════════════════════════════════════

        private void BuildAXISDrones(SectorData sectorData)
        {
            var rng = new RandomNumberGenerator();
            rng.Randomize();

            int droneCount = 3 + Mathf.Min(_sector, 4);

            for (int i = 0; i < droneCount; i++)
            {
                var drone = new Node3D();

                // Body
                var body = new MeshInstance3D();
                var bodyMesh = new BoxMesh();
                bodyMesh.Size = new Vector3(1.6f, 0.8f, 1.6f);
                body.Mesh = bodyMesh;
                body.MaterialOverride = MakeMetalMat(
                    new Color(0.08f, 0.08f, 0.1f), 0.9f, 0.25f);
                drone.AddChild(body);

                // Accent trim
                var trim = new MeshInstance3D();
                var trimBox = new BoxMesh();
                trimBox.Size = new Vector3(1.7f, 0.15f, 1.7f);
                trim.Mesh = trimBox;
                trim.MaterialOverride = MakeGlowMat(sectorData.AccentColor, 2f);
                drone.AddChild(trim);

                // Red eye
                var eye = new MeshInstance3D();
                var eyeMesh = new SphereMesh();
                eyeMesh.Radius = 0.22f;
                eyeMesh.Height = 0.44f;
                eyeMesh.RadialSegments = 6;
                eyeMesh.Rings = 3;
                eye.Mesh = eyeMesh;
                eye.Position = new Vector3(0, 0.05f, 0.8f);
                eye.MaterialOverride = MakeGlowMat(new Color(1f, 0.1f, 0.08f), 5f);
                drone.AddChild(eye);

                // Searchlight
                var spot = new SpotLight3D();
                spot.LightColor = new Color(1f, 0.3f, 0.15f);
                spot.LightEnergy = 0.8f + _danger * 0.5f;
                spot.SpotRange = 40f;
                spot.SpotAngle = 22f;
                spot.RotationDegrees = new Vector3(-90, 0, 0);
                spot.Position = new Vector3(0, -0.4f, 0);
                spot.ShadowEnabled = false;
                drone.AddChild(spot);

                // Engine glow
                var engine = new MeshInstance3D();
                var engMesh = new SphereMesh();
                engMesh.Radius = 0.35f;
                engMesh.Height = 0.25f;
                engMesh.RadialSegments = 4;
                engMesh.Rings = 2;
                engine.Mesh = engMesh;
                engine.Position = new Vector3(0, -0.45f, 0);
                engine.MaterialOverride = MakeGlowMat(new Color(0.3f, 0.5f, 1f), 3f);
                drone.AddChild(engine);

                float orbitRadius = rng.RandfRange(RADIUS * 0.35f, RADIUS + 20f);
                float orbitY = rng.RandfRange(8f, 25f);
                float orbitSpeed = rng.RandfRange(0.04f, 0.1f) * (rng.Randf() < 0.5f ? 1f : -1f);
                float startAngle = rng.RandfRange(0, Mathf.Tau);

                drone.Position = new Vector3(
                    Mathf.Cos(startAngle) * orbitRadius, orbitY,
                    Mathf.Sin(startAngle) * orbitRadius);

                AddChild(drone);
                _drones.Add(new DroneData
                {
                    Node = drone,
                    Radius = orbitRadius,
                    Speed = orbitSpeed,
                    Y = orbitY,
                    Angle = startAngle,
                });
            }
        }

        // ═════════════════════════════════════════════════════════
        //  AXIS OVERSEER — massive floating construct above arena
        // ═════════════════════════════════════════════════════════

        private void BuildAXISPresence(SectorData sectorData)
        {
            AXIS = new AXISPresence();
            AXIS.Name = "AXISPresence";
            AddChild(AXIS);
            AXIS.Initialize(sectorData.AccentColor, _danger);
        }

        // ═════════════════════════════════════════════════════════
        //  ASCENSION EFFECTS — escalating visual chaos
        // ═════════════════════════════════════════════════════════

        private void BuildAscensionEffects(SectorData sectorData)
        {
            float ext = RADIUS * 0.8f;

            // Storm particles
            _stormParticles = new GpuParticles3D();
            _stormParticles.Amount = 50 + _ascension * 30;
            _stormParticles.Lifetime = 4f;
            _stormParticles.Preprocess = 2f;
            _stormParticles.VisibilityAabb = new Aabb(
                new Vector3(-ext, -20, -ext), new Vector3(ext * 2, 60, ext * 2));

            var stormMat = new ParticleProcessMaterial();
            stormMat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
            stormMat.EmissionBoxExtents = new Vector3(ext, 20, ext);
            stormMat.Direction = new Vector3(0.3f, 0.5f, 0.2f);
            stormMat.Spread = 65f;
            stormMat.InitialVelocityMin = 1.5f + _ascension * 0.5f;
            stormMat.InitialVelocityMax = 4f + _ascension * 1.2f;
            stormMat.AngularVelocityMin = -200f;
            stormMat.AngularVelocityMax = 200f;
            stormMat.Gravity = new Vector3(0, -0.5f, 0);
            stormMat.ScaleMin = 0.06f;
            stormMat.ScaleMax = 0.2f + _ascension * 0.04f;

            var stormColor = sectorData.AccentColor.Lerp(
                new Color(0.9f, 0.2f, 0.3f), Mathf.Min(_ascension * 0.15f, 0.7f));
            stormColor.A = 0.7f;
            stormMat.Color = stormColor;
            _stormParticles.ProcessMaterial = stormMat;

            var stormMesh = new SphereMesh();
            stormMesh.Radius = 0.1f;
            stormMesh.Height = 0.2f;
            stormMesh.RadialSegments = 4;
            stormMesh.Rings = 2;
            stormMesh.Material = MakeGlowMat(stormColor, 3f + _ascension * 0.5f);
            _stormParticles.DrawPass1 = stormMesh;
            _stormParticles.Position = new Vector3(0, 5, 0);
            AddChild(_stormParticles);

            // Warning lights at ascension 2+
            if (_ascension >= 2)
            {
                float warningDist = RADIUS + 25f;
                int lightCount = 4 + _ascension;
                for (int i = 0; i < lightCount; i++)
                {
                    float angle = (float)i / lightCount * Mathf.Tau;
                    var warning = new OmniLight3D();
                    warning.LightColor = new Color(0.9f, 0.15f, 0.1f);
                    warning.LightEnergy = 0.4f + _ascension * 0.12f;
                    warning.OmniRange = 45f + _ascension * 5f;
                    warning.OmniAttenuation = 1.5f;
                    warning.Position = new Vector3(
                        Mathf.Cos(angle) * warningDist, 8f,
                        Mathf.Sin(angle) * warningDist);
                    warning.ShadowEnabled = false;
                    AddChild(warning);
                }
            }

            if (_ascension >= 4 && _env != null)
            {
                _env.FogDensity += _ascension * 0.00015f;
                _env.TonemapExposure -= _ascension * 0.015f;
            }
        }

        // ═════════════════════════════════════════════════════════
        //  ANIMATION — _Process drives all moving parts
        // ═════════════════════════════════════════════════════════

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _time += dt;

            // Camera environment override
            if (!_appliedToCamera && _env != null)
            {
                if (ServiceLocator.TryGet<IsometricCamera>(out var cam))
                {
                    cam.Environment = _env;
                    _appliedToCamera = true;
                }
            }

            // ── Rotate gears ──
            for (int i = 0; i < _gears.Count; i++)
            {
                if (IsInstanceValid(_gears[i]))
                    _gears[i].RotateObjectLocal(Vector3.Up, _gearSpeeds[i] * dt);
            }

            // ── Oscillate pistons ──
            for (int i = 0; i < _pistons.Count; i++)
            {
                var p = _pistons[i];
                if (!IsInstanceValid(p.Node)) continue;
                float y = p.BaseY + Mathf.Sin(_time * p.Speed + p.Phase) * p.Amplitude;
                var pos = p.Node.Position;
                p.Node.Position = new Vector3(pos.X, y, pos.Z);
            }

            // ── Orbit AXIS drones ──
            for (int i = 0; i < _drones.Count; i++)
            {
                var d = _drones[i];
                if (!IsInstanceValid(d.Node)) continue;

                float newAngle = d.Angle + d.Speed * dt;
                float bob = Mathf.Sin(_time * 0.7f + i * 2.1f) * 1.5f;
                d.Node.Position = new Vector3(
                    Mathf.Cos(newAngle) * d.Radius,
                    d.Y + bob,
                    Mathf.Sin(newAngle) * d.Radius);

                float lookAngle = newAngle + Mathf.Pi * 0.5f * Mathf.Sign(d.Speed);
                d.Node.Rotation = new Vector3(0, lookAngle, 0);

                d.Angle = newAngle;
                _drones[i] = d;
            }

            // ── Flicker fire lights ──
            for (int i = 0; i < _fireLights.Count; i++)
            {
                if (!IsInstanceValid(_fireLights[i])) continue;
                float flicker = Mathf.Sin(_time * 11f + i * 2.7f) * 0.3f
                    + Mathf.Sin(_time * 17f + i * 4.3f) * 0.18f
                    + Mathf.Sin(_time * 29f + i * 0.9f) * 0.1f;
                _fireLights[i].LightEnergy = _fireBaseEnergy[i] + flicker;
            }

            // ── Abyss pulse ──
            if (_abyssLight != null && IsInstanceValid(_abyssLight))
            {
                float pulse = Mathf.Sin(_time * 1.0f) * 0.15f
                    + Mathf.Sin(_time * 0.35f) * 0.08f;
                _abyssLight.LightEnergy = (0.5f + _danger * 0.5f) + pulse;
            }

            // ── Ascension effects ──
            if (_ascension >= 1 && _env != null)
            {
                float fi = Mathf.Min(_ascension * 0.04f, 0.2f);
                float flicker = Mathf.Sin(_time * (8f + _ascension * 3f)) * fi;
                flicker += Mathf.Sin(_time * 23f) * fi * 0.3f;
                _env.AmbientLightEnergy = _baseAmbientEnergy + flicker;
            }

            if (_ascension >= 3 && _env != null && _env.GlowEnabled)
            {
                float spike = Mathf.Max(0, Mathf.Sin(_time * 0.7f) - 0.85f) * 5f;
                _env.GlowIntensity = (0.8f + _danger * 0.5f) + spike * _ascension * 0.1f;
            }
        }

        // ═════════════════════════════════════════════════════════
        //  CLEANUP
        // ═════════════════════════════════════════════════════════

        public override void _ExitTree()
        {
            if (_worldEnv != null && IsInstanceValid(_worldEnv))
            {
                var parent = _worldEnv.GetParent();
                if (parent != null && IsInstanceValid(parent))
                    parent.RemoveChild(_worldEnv);
                _worldEnv.Free();
                _worldEnv = null;
            }
        }

        // ═════════════════════════════════════════════════════════
        //  MATERIAL HELPERS
        // ═════════════════════════════════════════════════════════

        private static StandardMaterial3D MakeMetalMat(Color color, float metallic, float roughness)
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = color;
            mat.Metallic = metallic;
            mat.Roughness = roughness;
            return mat;
        }

        private static StandardMaterial3D MakeGlowMat(Color color, float energy)
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = color;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.EmissionEnabled = true;
            mat.Emission = color;
            mat.EmissionEnergyMultiplier = energy;
            return mat;
        }

        // ═════════════════════════════════════════════════════════
        //  COLOR HELPERS
        // ═════════════════════════════════════════════════════════

        private Color GetSkyTopColor(SectorData data)
        {
            // Brighter than before so the sky contributes visible atmosphere
            var baseColor = new Color(0.03f, 0.04f, 0.1f);
            return baseColor.Lerp(data.AccentColor.Darkened(0.7f), 0.35f + _danger * 0.15f);
        }

        private Color GetHorizonColor(SectorData data)
        {
            // Visible industrial haze at the horizon
            var baseHorizon = new Color(0.08f, 0.06f, 0.12f);
            var accent = data.AccentColor.Darkened(0.35f);
            accent.A = 1f;
            return baseHorizon.Lerp(accent, 0.45f + _danger * 0.25f);
        }

        private Color GetParticleColor(SectorData data)
        {
            return _sector switch
            {
                1 => new Color(1f, 0.6f, 0.2f, 0.5f),
                2 => new Color(0.3f, 0.9f, 0.2f, 0.4f),
                3 => new Color(0.9f, 0.3f, 0.15f, 0.5f),
                4 => new Color(0.3f, 0.6f, 1f, 0.4f),
                5 => new Color(0.7f, 0.3f, 0.9f, 0.6f),
                _ => data.AccentColor * new Color(1, 1, 1, 0.4f),
            };
        }
    }
}
