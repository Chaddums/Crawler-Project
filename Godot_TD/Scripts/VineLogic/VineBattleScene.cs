using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Orchestrates a Vine Logic TD battle. Sets up the grid, pathfinder,
    /// wave manager, placer, camera, HUD, and lighting.
    /// Root script for VineBattle.tscn.
    /// </summary>
    public partial class VineBattleScene : Node3D
    {
        private VineGrid _grid;
        private VinePathfinder _pathfinder;
        private VineWaveManager _waveManager;
        private VinePlacer _placer;
        private VinePathPreview _pathPreview;
        private TDCamera _camera;
        private VineHUD _hud;
        private AXISCommentary _axisCommentary;

        public override void _Ready()
        {
            try
            {
            GD.Print("[VineBattle] _Ready START");

            // ── Grid ──
            GD.Print("[VineBattle] Creating grid...");
            _grid = new VineGrid();
            AddChild(_grid);

            // ── Build map layout ──
            int floor = GameManager.Instance?.CurrentFloor ?? 1;
            GD.Print($"[VineBattle] Building floor {floor} layout...");
            VineMapLayouts.BuildFloor(_grid, floor);

            // ── Pathfinding ──
            GD.Print("[VineBattle] Creating pathfinder...");
            _pathfinder = new VinePathfinder();
            AddChild(_pathfinder);
            _pathfinder.Initialize(_grid);

            // ── Wave manager ──
            GD.Print("[VineBattle] Creating wave manager...");
            _waveManager = new VineWaveManager();
            AddChild(_waveManager);

            // ── Placement ──
            GD.Print("[VineBattle] Creating placer...");
            _placer = new VinePlacer();
            AddChild(_placer);

            // ── Path preview ──
            GD.Print("[VineBattle] Creating path preview...");
            _pathPreview = new VinePathPreview();
            AddChild(_pathPreview);

            // ── Camera ──
            GD.Print("[VineBattle] Creating camera...");
            _camera = new TDCamera();
            AddChild(_camera);
            _camera.SetMapBounds(
                _grid.Width * Constants.VINE_CELL_SIZE,
                _grid.Height * Constants.VINE_CELL_SIZE);

            // ── Lighting + Environment ──
            GD.Print("[VineBattle] Setting up lighting...");
            SetupLighting();
            GD.Print("[VineBattle] Setting up environment...");
            SetupEnvironment();
            GD.Print("[VineBattle] Building environment dressing...");
            BuildEnvironmentDressing();
            GD.Print("[VineBattle] Environment dressing complete.");

            // ── HUD ──
            GD.Print("[VineBattle] Creating HUD...");
            _hud = new VineHUD();
            AddChild(_hud);

            // ── AXIS Commentary ──
            GD.Print("[VineBattle] Creating AXIS commentary...");
            _axisCommentary = new AXISCommentary();
            AddChild(_axisCommentary);

            // ── Initialize economy ──
            if (floor <= 1)
            {
                GameManager.Instance?.SetScrap(Constants.VINE_STARTING_GOLD);
            }
            else
            {
                // Carry over gold from previous floor
                GameManager.Instance?.SetScrap(GameManager.Instance?.GoldCarryover ?? Constants.VINE_STARTING_GOLD);
            }
            GameManager.Instance?.SetCoreLives(Constants.VINE_CORE_LIVES);

            // ── Economy hooks ──
            // Vine mode uses simplified economy — scrap drops go directly to gold
            GameEvents.OnScrapDropped += OnScrapDropped;
            GameEvents.OnScrapCollected += OnScrapCollected;

            // Start in build phase
            GameManager.Instance?.SetPhase(GamePhase.Build);

            GD.Print("[VineBattle] _Ready COMPLETE");
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[VineBattle] CRASH in _Ready: {ex.GetType().Name}: {ex.Message}");
                GD.PrintErr($"[VineBattle] Stack: {ex.StackTrace}");
            }
        }

        private void SetupLighting()
        {
            // Main light — cool blue-white for Tron feel
            var dirLight = new DirectionalLight3D();
            dirLight.Position = new Vector3(10, 20, 10);
            dirLight.RotationDegrees = new Vector3(-45, -30, 0);
            dirLight.LightColor = TronTheme.MainLight;
            dirLight.LightEnergy = 0.6f;
            dirLight.ShadowEnabled = true;
            AddChild(dirLight);

            // Fill light — faint cool blue
            var fillLight = new DirectionalLight3D();
            fillLight.RotationDegrees = new Vector3(-30, 150, 0);
            fillLight.LightColor = TronTheme.FillLight;
            fillLight.LightEnergy = 0.25f;
            fillLight.ShadowEnabled = false;
            AddChild(fillLight);
        }

        private void SetupEnvironment()
        {
            var env = new WorldEnvironment();
            var envRes = new Godot.Environment();
            envRes.BackgroundMode = Godot.Environment.BGMode.Color;
            envRes.BackgroundColor = TronTheme.Background;
            envRes.AmbientLightColor = TronTheme.Ambient;
            envRes.AmbientLightEnergy = 0.35f;
            envRes.TonemapMode = Godot.Environment.ToneMapper.Filmic;
            envRes.GlowEnabled = true;
            envRes.GlowIntensity = 0.7f;

            // Volumetric fog — blue-tinted atmospheric depth
            envRes.FogEnabled = true;
            envRes.FogLightColor = TronTheme.FogColor;
            envRes.FogDensity = 0.008f;
            envRes.FogSkyAffect = 0.3f;

            env.Environment = envRes;
            AddChild(env);
        }

        private static readonly RandomNumberGenerator _rng = new();

        /// <summary>
        /// Build the planet surface environment around the playable battle grid.
        /// Extended ground, outer grid lines, cliffs, structures, pillars, haze planes.
        /// </summary>
        private void BuildEnvironmentDressing()
        {
            float gridW = _grid.Width * Constants.VINE_CELL_SIZE;   // 40
            float gridH = _grid.Height * Constants.VINE_CELL_SIZE;  // 28
            float cx = gridW / 2f;  // 20 — grid center X
            float cz = gridH / 2f;  // 14 — grid center Z
            float extentSize = 240f; // Total extended ground size

            var envRoot = new Node3D();
            envRoot.Name = "EnvironmentDressing";
            AddChild(envRoot);

            // ── Extended ground plane (beneath and around the battle grid) ──
            var extGround = new MeshInstance3D();
            var extPlane = new PlaneMesh();
            extPlane.Size = new Vector2(extentSize, extentSize);
            extGround.Mesh = extPlane;
            extGround.Position = new Vector3(cx, -0.05f, cz); // Slightly below battle ground
            extGround.MaterialOverride = TronTheme.MakeExtendedGroundMaterial();
            envRoot.AddChild(extGround);

            GD.Print("[VineBattle]   Outer grid lines...");
            BuildOuterGridLines(envRoot, cx, cz, extentSize);

            GD.Print("[VineBattle]   Cliff ring...");
            BuildCliffRing(envRoot, cx, cz);

            GD.Print("[VineBattle]   Background structures...");
            BuildBackgroundStructures(envRoot, cx, cz);

            GD.Print("[VineBattle]   Background pillars...");
            BuildBackgroundPillars(envRoot, cx, cz);

            GD.Print("[VineBattle]   Tron fog...");
            BuildTronFog(envRoot, cx, cz);

            GD.Print("[VineBattle]   Horizon silhouettes...");
            BuildHorizonSilhouettes(envRoot, cx, cz);

            GD.Print("[VineBattle]   Approach corridors...");
            BuildApproachCorridors(envRoot);
        }

        private void BuildOuterGridLines(Node3D parent, float cx, float cz, float extent)
        {
            var gridVisual = new MeshInstance3D();
            var im = new ImmediateMesh();
            gridVisual.Mesh = im;
            gridVisual.Position = new Vector3(cx, 0.01f, cz);
            gridVisual.MaterialOverride = TronTheme.MakeOuterGridLineMaterial();

            float half = extent / 2f;
            float spacing = 4f; // Wider spacing than the 2-unit battle grid

            im.SurfaceBegin(Mesh.PrimitiveType.Lines);
            int lines = (int)(extent / spacing) + 1;
            for (int i = 0; i < lines; i++)
            {
                float offset = -half + i * spacing;
                im.SurfaceAddVertex(new Vector3(-half, 0, offset));
                im.SurfaceAddVertex(new Vector3(half, 0, offset));
                im.SurfaceAddVertex(new Vector3(offset, 0, -half));
                im.SurfaceAddVertex(new Vector3(offset, 0, half));
            }
            im.SurfaceEnd();

            parent.AddChild(gridVisual);
        }

        private void BuildCliffRing(Node3D parent, float cx, float cz)
        {
            // Mixed terrain shapes forming an irregular ring around the battle area.
            // Mesas, spires, hills, ridges, peaks, and stepped formations.

            // ── Large mesas (flat-topped cylindrical plateaus) ──
            var mesas = new (Vector3 pos, float radius, float height, float rotY)[] {
                (new Vector3(cx - 36, 0, cz - 63), 12, 10, 5),
                (new Vector3(cx + 5,  0, cz - 70), 15, 14, 0),
                (new Vector3(cx - 58, 0, cz + 12), 10, 12, -6),
                (new Vector3(cx + 60, 0, cz + 18), 11, 16, 5),
                (new Vector3(cx + 38, 0, cz + 60), 14, 9, 2),
            };
            foreach (var (pos, r, h, rotY) in mesas)
            {
                var mesa = TronTheme.MakeMesa(r, h);
                mesa.Position = pos;
                mesa.RotationDegrees = new Vector3(0, rotY, 0);
                parent.AddChild(mesa);
            }

            // ── Spires (tall thin hexagonal columns) ──
            var spires = new (Vector3 pos, float height, float radius)[] {
                (new Vector3(cx + 38, 0, cz - 60), 18, 2.5f),
                (new Vector3(cx - 63, 0, cz - 18), 22, 2f),
                (new Vector3(cx + 63, 0, cz - 12), 16, 3f),
                (new Vector3(cx - 46, 0, cz + 58), 14, 2.2f),
                (new Vector3(cx + 56, 0, cz + 46), 20, 1.8f),
                (new Vector3(cx - 66, 0, cz + 32), 12, 2.5f),
            };
            foreach (var (pos, h, r) in spires)
            {
                var spire = TronTheme.MakeSpire(h, r);
                spire.Position = pos;
                parent.AddChild(spire);
            }

            // ── Rounded hills (sphere mounds) ──
            var hills = new (Vector3 pos, float radius)[] {
                (new Vector3(cx - 50, 0, cz - 48), 8),
                (new Vector3(cx + 50, 0, cz - 50), 6),
                (new Vector3(cx - 43, 0, cz + 50), 7),
                (new Vector3(cx + 28, 0, cz - 56), 5),
                (new Vector3(cx - 15, 0, cz + 63), 9),
            };
            foreach (var (pos, r) in hills)
            {
                var hill = TronTheme.MakeHill(r);
                hill.Position = pos;
                parent.AddChild(hill);
            }

            // ── Ridges (long narrow walls — the one box-shape that makes geological sense) ──
            var ridges = new (Vector3 pos, float length, float height, float depth, float rotY)[] {
                (new Vector3(cx - 20, 0, cz + 66), 30, 6, 3, -5),
                (new Vector3(cx + 56, 0, cz + 5),  4, 10, 22, -8),
                (new Vector3(cx - 60, 0, cz - 5),  3, 8, 18, 10),
            };
            foreach (var (pos, l, h, d, rotY) in ridges)
            {
                var ridge = TronTheme.MakeRidge(l, h, d);
                ridge.Position = pos;
                ridge.RotationDegrees = new Vector3(0, rotY, 0);
                parent.AddChild(ridge);
            }

            // ── Peaked mountains (prism shapes) ──
            var peaks = new (Vector3 pos, float width, float height, float depth, float rotY)[] {
                (new Vector3(cx + 46, 0, cz - 56), 12, 15, 8, -15),
                (new Vector3(cx - 56, 0, cz + 50), 10, 12, 10, 12),
                (new Vector3(cx + 53, 0, cz + 50), 8, 10, 14, -10),
            };
            foreach (var (pos, w, h, d, rotY) in peaks)
            {
                var peak = TronTheme.MakePeak(w, h, d);
                peak.Position = pos;
                peak.RotationDegrees = new Vector3(0, rotY, 0);
                parent.AddChild(peak);
            }

            // ── Stepped mesas (terraced plateaus) ──
            var stepped = new (Vector3 pos, float radius, float height, int steps)[] {
                (new Vector3(cx - 56, 0, cz - 50), 8, 12, 3),
                (new Vector3(cx + 58, 0, cz - 43), 6, 10, 4),
            };
            foreach (var (pos, r, h, s) in stepped)
            {
                var mesa = TronTheme.MakeSteppedMesa(r, h, s);
                mesa.Position = pos;
                parent.AddChild(mesa);
            }
        }

        private void BuildBackgroundStructures(Node3D parent, float cx, float cz)
        {
            // KitBash buildings placed in the mid-ground (30-50 units from center)
            var structures = new (string asset, Vector3 pos, float scale, float rotY)[] {
                // Buildings in the background
                (AssetLibrary.BLDG_OUTPOST,      new Vector3(cx - 43, 0, cz - 38), 0.25f, 15),
                (AssetLibrary.BLDG_FUEL_TANKS,    new Vector3(cx + 46, 0, cz - 33), 0.2f, -20),
                (AssetLibrary.BLDG_BARRACKS,      new Vector3(cx - 38, 0, cz + 43), 0.22f, 40),
                (AssetLibrary.BLDG_WATER_TOWERS,  new Vector3(cx + 40, 0, cz + 38), 0.18f, -30),
                (AssetLibrary.BLDG_TRENCH,        new Vector3(cx + 10, 0, cz - 46), 0.2f, 0),
                (AssetLibrary.BLDG_CHECKPOINT,    new Vector3(cx - 15, 0, cz + 48), 0.2f, 10),

                // Turrets on cliff edges
                (AssetLibrary.TURRET_A, new Vector3(cx - 50, 0, cz - 28), 0.5f, 45),
                (AssetLibrary.TURRET_B, new Vector3(cx + 52, 0, cz + 5), 0.5f, -30),
                (AssetLibrary.TURRET_C, new Vector3(cx - 10, 0, cz - 50), 0.45f, 0),

                // Large props (generators, containers, radar)
                (AssetLibrary.PROP_GENERATOR_A, new Vector3(cx + 38, 0, cz - 43), 1.8f, -10),
                (AssetLibrary.PROP_GENERATOR_B, new Vector3(cx - 46, 0, cz + 15), 1.6f, 25),
                (AssetLibrary.PROP_CONTAINER_A, new Vector3(cx + 33, 0, cz + 46), 2f, 5),
                (AssetLibrary.PROP_CONTAINER_B, new Vector3(cx - 36, 0, cz - 46), 1.8f, -15),
                (AssetLibrary.PROP_RADAR,       new Vector3(cx + 48, 0, cz - 46), 2f, 30),
                (AssetLibrary.PROP_SATELLITE,   new Vector3(cx - 50, 0, cz + 46), 1.5f, -20),
                (AssetLibrary.PROP_ANTENNA_A,   new Vector3(cx - 56, 0, cz - 5), 2f, 0),
                (AssetLibrary.PROP_ANTENNA_B,   new Vector3(cx + 56, 0, cz + 20), 1.8f, 15),
            };

            foreach (var (asset, pos, scale, rotY) in structures)
            {
                var instance = AssetLibrary.Instantiate(asset);
                if (instance == null) continue;
                instance.Position = pos;
                instance.Scale = Vector3.One * scale;
                instance.RotationDegrees = new Vector3(0, rotY, 0);
                TronTheme.TronifyNode(instance);
                parent.AddChild(instance);
            }

            // Scatter small props in the near-field (15-35 units from center)
            for (int i = 0; i < 16; i++)
            {
                var prop = AssetLibrary.Instantiate(
                    AssetLibrary.SmallProps[_rng.RandiRange(0, AssetLibrary.SmallProps.Length - 1)]);
                if (prop == null) continue;

                float angle = _rng.RandfRange(0, Mathf.Tau);
                float dist = _rng.RandfRange(30f, 48f);
                float px = cx + Mathf.Cos(angle) * dist;
                float pz = cz + Mathf.Sin(angle) * dist;

                prop.Position = new Vector3(px, 0, pz);
                prop.Scale = Vector3.One * _rng.RandfRange(1.2f, 2f);
                prop.RotationDegrees = new Vector3(0, _rng.RandfRange(0, 360), 0);
                TronTheme.TronifyNode(prop);
                parent.AddChild(prop);
            }
        }

        private void BuildBackgroundPillars(Node3D parent, float cx, float cz)
        {
            // Data pillars at varying distances — Tron signature element
            for (int i = 0; i < 24; i++)
            {
                float angle = _rng.RandfRange(0, Mathf.Tau);
                float dist = _rng.RandfRange(36f, 78f);
                float px = cx + Mathf.Cos(angle) * dist;
                float pz = cz + Mathf.Sin(angle) * dist;
                float height = _rng.RandfRange(3f, 12f);
                float width = _rng.RandfRange(0.3f, 0.8f);

                // Taller pillars further away for depth
                if (dist > 50f) height *= 1.5f;

                var pillar = TronTheme.MakeDataPillar(height, width);
                pillar.Position = new Vector3(px, 0, pz);
                parent.AddChild(pillar);
            }
        }

        private void BuildTronFog(Node3D parent, float cx, float cz)
        {
            // Tron fog — few massive clouds of hundreds of tiny drifting wireframe cubes.
            // Each cloud is a dense cluster that reads as a single fog mass.

            // Large clouds scattered around the field (6 massive banks, elevated)
            for (int i = 0; i < 6; i++)
            {
                float angle = _rng.RandfRange(0, Mathf.Tau);
                float dist = _rng.RandfRange(38f, 78f);
                float px = cx + Mathf.Cos(angle) * dist;
                float pz = cz + Mathf.Sin(angle) * dist;
                float py = _rng.RandfRange(6f, 14f);
                parent.AddChild(TronTheme.MakeFogBank(
                    _rng, new Vector3(px, py, pz),
                    cubeCount: _rng.RandiRange(800, 1000), spread: 12f, cubeSize: 0.2f));
            }

            // Horizon fog walls — very dense, far out, tall (4 huge banks)
            for (int i = 0; i < 4; i++)
            {
                float angle = _rng.RandfRange(0, Mathf.Tau);
                float dist = _rng.RandfRange(75f, 110f);
                float px = cx + Mathf.Cos(angle) * dist;
                float pz = cz + Mathf.Sin(angle) * dist;
                float py = _rng.RandfRange(8f, 18f);
                parent.AddChild(TronTheme.MakeFogBank(
                    _rng, new Vector3(px, py, pz),
                    cubeCount: _rng.RandiRange(1000, 1400), spread: 18f, cubeSize: 0.25f));
            }
        }

        private void BuildHorizonSilhouettes(Node3D parent, float cx, float cz)
        {
            // Varied distant terrain — mesas, peaks, hills, and spires on the horizon.
            // Mixed shapes prevent the "all boxes" look.

            // ── Massive distant mesas ──
            var distMesas = new (Vector3 pos, float r, float h)[] {
                (new Vector3(cx - 45, 0, cz - 95), 22, 25),
                (new Vector3(cx + 35, 0, cz + 90), 20, 20),
                (new Vector3(cx - 88, 0, cz + 20), 18, 30),
                (new Vector3(cx + 90, 0, cz - 15), 16, 35),
            };
            foreach (var (pos, r, h) in distMesas)
            {
                var mesa = TronTheme.MakeMesa(r, h, 8);
                mesa.Position = pos;
                parent.AddChild(mesa);
            }

            // ── Distant peaked mountains ──
            var distPeaks = new (Vector3 pos, float w, float h, float d, float rotY)[] {
                (new Vector3(cx + 15, 0, cz - 100), 30, 45, 20, 10),
                (new Vector3(cx + 65, 0, cz - 85), 20, 30, 15, -20),
                (new Vector3(cx - 35, 0, cz + 92), 25, 35, 18, 5),
                (new Vector3(cx - 90, 0, cz - 25), 18, 40, 30, 15),
                (new Vector3(cx + 88, 0, cz + 25), 15, 45, 25, -10),
            };
            foreach (var (pos, w, h, d, rotY) in distPeaks)
            {
                var peak = TronTheme.MakePeak(w, h, d);
                peak.Position = pos;
                peak.RotationDegrees = new Vector3(0, rotY, 0);
                parent.AddChild(peak);
            }

            // ── Distant rounded hills ──
            var distHills = new (Vector3 pos, float r)[] {
                (new Vector3(cx - 70, 0, cz - 75), 15),
                (new Vector3(cx + 75, 0, cz + 60), 12),
                (new Vector3(cx + 55, 0, cz - 80), 10),
                (new Vector3(cx - 80, 0, cz + 55), 13),
            };
            foreach (var (pos, r) in distHills)
            {
                var hill = TronTheme.MakeHill(r);
                hill.Position = pos;
                parent.AddChild(hill);
            }

            // ── Distant tall spires (antenna/obelisk silhouettes) ──
            var distSpires = new (Vector3 pos, float h, float r)[] {
                (new Vector3(cx - 60, 0, cz - 88), 35, 3f),
                (new Vector3(cx + 80, 0, cz - 50), 40, 2.5f),
                (new Vector3(cx - 85, 0, cz - 5), 50, 3.5f),
                (new Vector3(cx + 30, 0, cz + 95), 30, 2f),
                (new Vector3(cx - 20, 0, cz - 98), 45, 2.8f),
            };
            foreach (var (pos, h, r) in distSpires)
            {
                var spire = TronTheme.MakeSpire(h, r);
                spire.Position = pos;
                parent.AddChild(spire);
            }

            // ── Distant stepped formations ──
            var distStepped = new (Vector3 pos, float r, float h, int steps)[] {
                (new Vector3(cx + 60, 0, cz + 80), 15, 22, 4),
                (new Vector3(cx - 75, 0, cz + 70), 12, 18, 3),
            };
            foreach (var (pos, r, h, s) in distStepped)
            {
                var stepped = TronTheme.MakeSteppedMesa(r, h, s);
                stepped.Position = pos;
                parent.AddChild(stepped);
            }
        }

        /// <summary>
        /// Build visible approach corridors from off-screen to each entry point.
        /// Enemies travel along these before reaching the grid — makes spawning feel like
        /// they're coming from somewhere, not appearing at the edge.
        /// </summary>
        private void BuildApproachCorridors(Node3D parent)
        {
            float cs = Constants.VINE_CELL_SIZE;

            foreach (var entry in _grid.EntryPoints)
            {
                var entryWorld = _grid.GridToWorld(entry);

                // Determine approach direction based on entry position
                float dirX = 0, dirZ = 0;
                if (entry.X == 0) dirX = -1;                    // Left edge → approach from left
                else if (entry.X == _grid.Width - 1) dirX = 1;  // Right edge → from right
                if (entry.Y == 0) dirZ = -1;                    // Top edge → from top
                else if (entry.Y == _grid.Height - 1) dirZ = 1; // Bottom edge → from bottom

                if (dirX == 0 && dirZ == 0) dirX = -1; // Fallback

                // Build corridor extending 30-60 units off-screen
                float corridorLength = 50f;
                float corridorWidth = cs * 2.5f;

                // DataStream-style floor along the corridor
                for (float dist = 2f; dist < corridorLength; dist += cs)
                {
                    float px = entryWorld.X + dirX * dist;
                    float pz = entryWorld.Z + dirZ * dist;

                    // Corridor floor segment
                    var floor = new MeshInstance3D();
                    var floorMesh = new BoxMesh();
                    if (Mathf.Abs(dirX) > 0)
                        floorMesh.Size = new Vector3(cs, 0.04f, corridorWidth);
                    else
                        floorMesh.Size = new Vector3(corridorWidth, 0.04f, cs);
                    floor.Mesh = floorMesh;
                    floor.Position = new Vector3(px, 0.01f, pz);
                    floor.MaterialOverride = TronTheme.MakeDataStreamMaterial() as Material;
                    parent.AddChild(floor);
                }

                // Terrain formations lining the corridor walls
                for (float dist = 4f; dist < corridorLength; dist += _rng.RandfRange(3f, 6f))
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float offset = corridorWidth / 2f + _rng.RandfRange(0.5f, 2f);
                        float px, pz;
                        if (Mathf.Abs(dirX) > 0)
                        {
                            px = entryWorld.X + dirX * dist;
                            pz = entryWorld.Z + side * offset;
                        }
                        else
                        {
                            px = entryWorld.X + side * offset;
                            pz = entryWorld.Z + dirZ * dist;
                        }

                        // Random terrain piece
                        var piece = new Node3D();
                        piece.Position = new Vector3(px, 0, pz);
                        parent.AddChild(piece);

                        int variant = _rng.RandiRange(0, 4);
                        switch (variant)
                        {
                            case 0: // Rock cluster
                                for (int r = 0; r < _rng.RandiRange(2, 4); r++)
                                {
                                    float s = _rng.RandfRange(0.3f, 0.8f);
                                    var rock = new MeshInstance3D();
                                    rock.Mesh = new BoxMesh { Size = new Vector3(s, s * 0.7f, s * _rng.RandfRange(0.5f, 1.3f)) };
                                    rock.Position = new Vector3(_rng.RandfRange(-0.5f, 0.5f), s * 0.3f, _rng.RandfRange(-0.5f, 0.5f));
                                    rock.RotationDegrees = new Vector3(_rng.RandfRange(-15, 15), _rng.RandfRange(0, 90), _rng.RandfRange(-15, 15));
                                    TronTheme.ApplyTronOutline(rock, TronTheme.MakeWallBodyMaterial());
                                    piece.AddChild(rock);
                                }
                                break;

                            case 1: // Broken pillar
                                float pH = _rng.RandfRange(1f, 3f);
                                var pillar = new MeshInstance3D();
                                pillar.Mesh = new CylinderMesh { TopRadius = _rng.RandfRange(0.15f, 0.3f), BottomRadius = _rng.RandfRange(0.3f, 0.6f), Height = pH };
                                pillar.Position = new Vector3(0, pH / 2f, 0);
                                pillar.RotationDegrees = new Vector3(_rng.RandfRange(-8, 8), 0, _rng.RandfRange(-8, 8));
                                TronTheme.ApplyTronOutline(pillar, TronTheme.MakeWallBodyMaterial());
                                piece.AddChild(pillar);
                                break;

                            case 2: // Angular slab wall
                                var slab = new MeshInstance3D();
                                float slabH = _rng.RandfRange(0.8f, 2f);
                                slab.Mesh = new BoxMesh { Size = new Vector3(_rng.RandfRange(0.3f, 0.8f), slabH, _rng.RandfRange(1f, 2.5f)) };
                                slab.Position = new Vector3(0, slabH / 2f, 0);
                                slab.RotationDegrees = new Vector3(_rng.RandfRange(-10, 10), _rng.RandfRange(-20, 20), _rng.RandfRange(-5, 5));
                                TronTheme.ApplyTronOutline(slab, TronTheme.MakeElevatedMaterial());
                                piece.AddChild(slab);
                                break;

                            case 3: // Mesa
                                float mH = _rng.RandfRange(1f, 2.5f);
                                var mesa = new MeshInstance3D();
                                mesa.Mesh = new CylinderMesh { TopRadius = _rng.RandfRange(0.6f, 1.2f), BottomRadius = _rng.RandfRange(0.8f, 1.5f), Height = mH, RadialSegments = _rng.RandiRange(5, 8) };
                                mesa.Position = new Vector3(0, mH / 2f, 0);
                                TronTheme.ApplyTronOutline(mesa, TronTheme.MakeElevatedMaterial());
                                piece.AddChild(mesa);
                                break;

                            default: // Spire
                                float sH = _rng.RandfRange(2f, 4f);
                                var spire = new MeshInstance3D();
                                spire.Mesh = new CylinderMesh { TopRadius = 0.05f, BottomRadius = _rng.RandfRange(0.3f, 0.6f), Height = sH, RadialSegments = _rng.RandiRange(4, 6) };
                                spire.Position = new Vector3(0, sH / 2f, 0);
                                TronTheme.ApplyTronOutline(spire, TronTheme.MakeElevatedMaterial());
                                piece.AddChild(spire);
                                break;
                        }
                    }
                }
            }
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            // Click on nodes for interaction (sell, manual trigger)
            if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Right)
            {
                // Right-click: sell node or cancel placement
                if (ServiceLocator.TryGet<VinePlacer>(out var placer) && placer.IsPlacing)
                {
                    placer.CancelPlacing();
                    return;
                }

                var worldPos = RaycastGround(mb.Position);
                if (!worldPos.HasValue) return;

                var cell = _grid.WorldToGrid(worldPos.Value);
                var node = _grid.GetNode(cell);
                if (node != null)
                {
                    // Sell: refund based on editor tuning
                    int refund = Mathf.RoundToInt(node.Data.GoldCost * SignalTuningEditor.SellRefund);
                    _grid.RemoveNode(cell);
                    GameManager.Instance?.AddScrap(refund);
                }
            }
            else if (@event is InputEventMouseButton mb2 && mb2.Pressed && mb2.ButtonIndex == MouseButton.Middle)
            {
                // Middle-click: manual trigger (for SignalCannon nodes)
                var worldPos = RaycastGround(mb2.Position);
                if (!worldPos.HasValue) return;

                var cell = _grid.WorldToGrid(worldPos.Value);
                var node = _grid.GetNode(cell);
                node?.ManualTrigger();
            }
        }

        private void OnScrapDropped(Vector3 pos, int amount)
        {
            // In vine mode, scrap goes directly to gold (no decay/pickup mechanic)
            GameManager.Instance?.AddScrap(amount);
        }

        private void OnScrapCollected(int amount)
        {
            GameManager.Instance?.AddScrap(amount);
        }

        private Vector3? RaycastGround(Vector2 screenPos)
        {
            var camera = GetViewport().GetCamera3D();
            if (camera == null) return null;

            var from = camera.ProjectRayOrigin(screenPos);
            var dir = camera.ProjectRayNormal(screenPos);
            if (Mathf.Abs(dir.Y) < 0.001f) return null;
            float t = -from.Y / dir.Y;
            if (t < 0) return null;
            return from + dir * t;
        }

        public override void _ExitTree()
        {
            GameEvents.OnScrapDropped -= OnScrapDropped;
            GameEvents.OnScrapCollected -= OnScrapCollected;
        }
    }
}
