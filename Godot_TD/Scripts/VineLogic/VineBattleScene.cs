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
            if (PlanetTheme.Current is ScrapyardPlanetTheme)
            {
                GD.Print("[VineBattle] Using Scrapyard environment...");
                var scrapRoot = new Node3D();
                scrapRoot.Name = "ScrapyardEnvironment";
                AddChild(scrapRoot);
                ScrapyardEnvironment.BuildEnvironment(scrapRoot,
                    _grid.Width * Constants.VINE_CELL_SIZE,
                    _grid.Height * Constants.VINE_CELL_SIZE);
            }
            else
            {
                BuildEnvironmentDressing();
            }
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

            // ── Apply planet theme override ──
            // The grid/environment builds with TronTheme by default.
            // If we're on a different planet, re-theme everything.
            if (PlanetTheme.Current is not TronPlanetTheme)
            {
                GD.Print("[VineBattle] Applying planet theme override...");
                ApplyPlanetThemeOverride();
            }

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
            var theme = PlanetTheme.Current;
            var dirLight = new DirectionalLight3D();
            dirLight.Position = new Vector3(10, 20, 10);
            dirLight.RotationDegrees = new Vector3(-45, -30, 0);
            dirLight.LightColor = theme.MainLightColor;
            dirLight.LightEnergy = theme is ScrapyardPlanetTheme ? 0.8f : 0.6f;
            dirLight.ShadowEnabled = true;
            AddChild(dirLight);

            var fillLight = new DirectionalLight3D();
            fillLight.RotationDegrees = new Vector3(-30, 150, 0);
            fillLight.LightColor = theme.FillLightColor;
            fillLight.LightEnergy = 0.25f;
            fillLight.ShadowEnabled = false;
            AddChild(fillLight);
        }

        private void SetupEnvironment()
        {
            var theme = PlanetTheme.Current;
            var env = new WorldEnvironment();
            var envRes = new Godot.Environment();
            envRes.BackgroundMode = Godot.Environment.BGMode.Color;
            envRes.BackgroundColor = theme.BackgroundColor;
            envRes.AmbientLightColor = theme.AmbientColor;
            envRes.AmbientLightEnergy = theme is ScrapyardPlanetTheme ? 0.5f : 0.35f;
            envRes.TonemapMode = Godot.Environment.ToneMapper.Filmic;
            envRes.GlowEnabled = true;
            envRes.GlowIntensity = theme is ScrapyardPlanetTheme ? 0.4f : 0.7f;

            envRes.FogEnabled = true;
            envRes.FogLightColor = theme.FogColor;
            envRes.FogDensity = theme is ScrapyardPlanetTheme ? 0.012f : 0.008f;
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

            GD.Print("[VineBattle]   Terrain ring...");
            BuildTerrainRing(envRoot, cx, cz);
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
        /// Build a dense continuous terrain ring around the playable grid.
        /// Packed rock formations, mountains, ridges — reads as a rocky Tron planet surface.
        /// Entry points get canyon openings where enemies emerge from.
        /// </summary>
        private void BuildTerrainRing(Node3D parent, float cx, float cz)
        {
            float gridW = _grid.Width * Constants.VINE_CELL_SIZE;
            float gridH = _grid.Height * Constants.VINE_CELL_SIZE;
            float margin = 4f;  // Gap between grid edge and terrain start
            float depth = 40f;  // How deep the terrain ring extends outward

            // Collect entry point world positions for canyon gaps
            var entryPositions = new System.Collections.Generic.List<Vector3>();
            foreach (var entry in _grid.EntryPoints)
                entryPositions.Add(_grid.GridToWorld(entry));
            var exitPos = _grid.GridToWorld(_grid.ExitPoint);
            entryPositions.Add(exitPos);

            // Sweep around the grid perimeter, placing dense terrain
            // Skip areas near entry/exit points to create canyon openings
            float canyonWidth = 6f; // Width of opening at entry points

            // Place terrain in a dense ring pattern
            for (float angle = 0; angle < Mathf.Tau; angle += 0.08f)
            {
                for (float dist = margin; dist < depth; dist += _rng.RandfRange(2f, 4f))
                {
                    // Position relative to grid center
                    float rawX = cx + Mathf.Cos(angle) * (gridW / 2f + dist);
                    float rawZ = cz + Mathf.Sin(angle) * (gridH / 2f + dist);

                    // Check if this position is near an entry/exit — if so, skip (canyon gap)
                    bool nearEntry = false;
                    foreach (var ep in entryPositions)
                    {
                        float d = new Vector2(rawX - ep.X, rawZ - ep.Z).Length();
                        if (d < canyonWidth + dist * 0.1f) // Canyon widens with distance
                        {
                            nearEntry = true;
                            break;
                        }
                    }
                    if (nearEntry) continue;

                    // Scale up with distance — near pieces are small details, far pieces are mountains
                    float scaleFactor = 0.5f + dist * 0.08f;
                    float heightFactor = 0.5f + dist * 0.12f;

                    var piece = new Node3D();
                    piece.Position = new Vector3(rawX, 0, rawZ);
                    parent.AddChild(piece);

                    // Random jitter so it doesn't look gridded
                    piece.Position += new Vector3(
                        _rng.RandfRange(-1.5f, 1.5f), 0,
                        _rng.RandfRange(-1.5f, 1.5f));

                    BuildTerrainPiece(piece, scaleFactor, heightFactor);
                }
            }

            // Fill in canyon walls — dense terrain on both sides of each opening
            foreach (var ep in entryPositions)
            {
                // Direction from center to entry
                float dirX = ep.X - cx;
                float dirZ = ep.Z - cz;
                float len = Mathf.Sqrt(dirX * dirX + dirZ * dirZ);
                if (len < 0.1f) continue;
                dirX /= len;
                dirZ /= len;

                // Perpendicular direction (canyon walls)
                float perpX = -dirZ;
                float perpZ = dirX;

                // Place terrain along both sides of the canyon
                for (float dist = margin; dist < depth * 0.7f; dist += _rng.RandfRange(2f, 3.5f))
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float wallDist = canyonWidth * 0.5f + _rng.RandfRange(0.5f, 1.5f);
                        float px = ep.X + dirX * dist + perpX * side * wallDist;
                        float pz = ep.Z + dirZ * dist + perpZ * side * wallDist;

                        float scaleFactor = 0.6f + dist * 0.06f;
                        float heightFactor = 0.8f + dist * 0.1f;

                        var piece = new Node3D();
                        piece.Position = new Vector3(px, 0, pz);
                        parent.AddChild(piece);
                        BuildTerrainPiece(piece, scaleFactor, heightFactor);
                    }
                }
            }
        }

        /// <summary>
        /// Build a single terrain formation — randomized shape with Tron outline.
        /// Scale and height increase with distance from grid for depth perspective.
        /// </summary>
        private void BuildTerrainPiece(Node3D parent, float scale, float height)
        {
            int variant = _rng.RandiRange(0, 6);
            switch (variant)
            {
                case 0: // Rock cluster — multiple angular pieces packed together
                    int count = _rng.RandiRange(2, 5);
                    for (int i = 0; i < count; i++)
                    {
                        float s = _rng.RandfRange(0.3f, 0.7f) * scale;
                        var rock = new MeshInstance3D();
                        rock.Mesh = new BoxMesh { Size = new Vector3(s, s * _rng.RandfRange(0.5f, 1.2f), s * _rng.RandfRange(0.6f, 1.3f)) };
                        rock.Position = new Vector3(_rng.RandfRange(-0.5f, 0.5f) * scale, s * 0.3f, _rng.RandfRange(-0.5f, 0.5f) * scale);
                        rock.RotationDegrees = new Vector3(_rng.RandfRange(-20, 20), _rng.RandfRange(0, 90), _rng.RandfRange(-20, 20));
                        TronTheme.ApplyTronOutline(rock, TronTheme.MakeWallBodyMaterial());
                        parent.AddChild(rock);
                    }
                    break;

                case 1: // Mountain peak — tall cone
                    float peakH = _rng.RandfRange(2f, 5f) * height;
                    var peak = new MeshInstance3D();
                    peak.Mesh = new CylinderMesh {
                        TopRadius = _rng.RandfRange(0.05f, 0.3f) * scale,
                        BottomRadius = _rng.RandfRange(0.8f, 1.5f) * scale,
                        Height = peakH,
                        RadialSegments = _rng.RandiRange(4, 7) };
                    peak.Position = new Vector3(0, peakH / 2f, 0);
                    TronTheme.ApplyTronOutline(peak, TronTheme.MakeElevatedMaterial());
                    parent.AddChild(peak);
                    break;

                case 2: // Mesa plateau — flat-topped wide cylinder
                    float mesaH = _rng.RandfRange(1f, 3f) * height;
                    var mesa = new MeshInstance3D();
                    mesa.Mesh = new CylinderMesh {
                        TopRadius = _rng.RandfRange(0.8f, 1.5f) * scale,
                        BottomRadius = _rng.RandfRange(1f, 1.8f) * scale,
                        Height = mesaH,
                        RadialSegments = _rng.RandiRange(5, 8) };
                    mesa.Position = new Vector3(0, mesaH / 2f, 0);
                    TronTheme.ApplyTronOutline(mesa, TronTheme.MakeElevatedMaterial());
                    parent.AddChild(mesa);
                    break;

                case 3: // Ridge wall — long narrow box
                    float ridgeH = _rng.RandfRange(1f, 3f) * height;
                    float ridgeL = _rng.RandfRange(2f, 5f) * scale;
                    var ridge = new MeshInstance3D();
                    ridge.Mesh = new BoxMesh { Size = new Vector3(ridgeL, ridgeH, _rng.RandfRange(0.3f, 0.8f) * scale) };
                    ridge.Position = new Vector3(0, ridgeH / 2f, 0);
                    ridge.RotationDegrees = new Vector3(_rng.RandfRange(-5, 5), _rng.RandfRange(-30, 30), _rng.RandfRange(-3, 3));
                    TronTheme.ApplyTronOutline(ridge, TronTheme.MakeWallBodyMaterial());
                    parent.AddChild(ridge);
                    break;

                case 4: // Stepped formation — 2-4 stacked pieces
                    float baseY = 0;
                    for (int i = 0; i < _rng.RandiRange(2, 4); i++)
                    {
                        float stepW = _rng.RandfRange(0.6f, 1.2f) * scale * (1f - i * 0.2f);
                        float stepH = _rng.RandfRange(0.4f, 0.8f) * height;
                        var step = new MeshInstance3D();
                        step.Mesh = new BoxMesh { Size = new Vector3(stepW, stepH, stepW * _rng.RandfRange(0.7f, 1.3f)) };
                        step.Position = new Vector3(0, baseY + stepH / 2f, 0);
                        step.RotationDegrees = new Vector3(0, i * _rng.RandfRange(10, 25), 0);
                        TronTheme.ApplyTronOutline(step, TronTheme.MakeElevatedMaterial());
                        parent.AddChild(step);
                        baseY += stepH;
                    }
                    break;

                case 5: // Hill mound — half-sphere
                    float hillR = _rng.RandfRange(0.8f, 2f) * scale;
                    var hill = new MeshInstance3D();
                    hill.Mesh = new SphereMesh { Radius = hillR, Height = hillR, RadialSegments = _rng.RandiRange(6, 10), Rings = 4 };
                    hill.Position = new Vector3(0, 0, 0); // Half-sphere sits on ground
                    TronTheme.ApplyTronOutline(hill, TronTheme.MakeElevatedMaterial());
                    parent.AddChild(hill);
                    break;

                default: // Tilted slab debris
                    float slabS = _rng.RandfRange(0.5f, 1.5f) * scale;
                    float slabH = _rng.RandfRange(0.1f, 0.3f) * scale;
                    var slab = new MeshInstance3D();
                    slab.Mesh = new BoxMesh { Size = new Vector3(slabS, slabH, slabS * _rng.RandfRange(0.8f, 1.5f)) };
                    slab.Position = new Vector3(0, _rng.RandfRange(0.3f, 1.5f) * height, 0);
                    slab.RotationDegrees = new Vector3(_rng.RandfRange(-30, 30), _rng.RandfRange(0, 90), _rng.RandfRange(-20, 20));
                    TronTheme.ApplyTronOutline(slab, TronTheme.MakeWallBodyMaterial());
                    parent.AddChild(slab);
                    break;
            }
        }

        /// <summary>
        /// Re-skin the entire scene for non-Tron planets.
        /// Walks the scene tree and replaces materials on all MeshInstance3D nodes.
        /// </summary>
        private void ApplyPlanetThemeOverride()
        {
            var theme = PlanetTheme.Current;

            // Re-theme all mesh instances in the scene tree
            ReThemeRecursive(this, theme);
        }

        private static void ReThemeRecursive(Node node, PlanetTheme theme)
        {
            if (node is MeshInstance3D mesh)
            {
                // Check current material to determine what this is
                var currentMat = mesh.MaterialOverride;

                if (currentMat is StandardMaterial3D stdMat)
                {
                    // Replace with planet-appropriate material based on the original color/type
                    var color = stdMat.AlbedoColor;
                    bool isDark = color.R < 0.1f && color.G < 0.1f && color.B < 0.1f;
                    bool isCyan = color.G > color.R * 2f && color.B > color.R * 2f;
                    bool isGreen = color.G > 0.5f && color.R < 0.4f;
                    bool isRed = color.R > 0.5f && color.G < 0.3f;

                    if (isDark)
                    {
                        // Dark body → use planet wall color
                        stdMat.AlbedoColor = theme.WallColor;
                    }
                    else if (isCyan && stdMat.EmissionEnabled)
                    {
                        // Cyan emissive (grid lines, edges) → planet grid color
                        stdMat.AlbedoColor = new Color(theme.GridLineColor.R, theme.GridLineColor.G, theme.GridLineColor.B, stdMat.AlbedoColor.A);
                        stdMat.Emission = theme.GridLineColor;
                    }
                    else if (isGreen)
                    {
                        // Entry marker → planet entry color
                        stdMat.AlbedoColor = new Color(theme.EntryMarkerColor.R, theme.EntryMarkerColor.G, theme.EntryMarkerColor.B, stdMat.AlbedoColor.A);
                        if (stdMat.EmissionEnabled)
                            stdMat.Emission = theme.EntryMarkerColor;
                    }
                    else if (isRed)
                    {
                        // Exit marker → planet exit color
                        stdMat.AlbedoColor = new Color(theme.ExitMarkerColor.R, theme.ExitMarkerColor.G, theme.ExitMarkerColor.B, stdMat.AlbedoColor.A);
                        if (stdMat.EmissionEnabled)
                            stdMat.Emission = theme.ExitMarkerColor;
                    }
                }
                else if (currentMat is ShaderMaterial shaderMat)
                {
                    // Shader materials (outline shaders, fog) → swap outline color
                    if (shaderMat.Shader != null)
                    {
                        // Try to set outline_color if the shader has it
                        shaderMat.SetShaderParameter("outline_color",
                            new Vector3(theme.GridLineColor.R, theme.GridLineColor.G, theme.GridLineColor.B));
                    }
                }

                // Check next_pass too (outline shaders are chained)
                if (currentMat is StandardMaterial3D baseMat && baseMat.NextPass is ShaderMaterial nextShader)
                {
                    nextShader.SetShaderParameter("outline_color",
                        new Vector3(theme.GridLineColor.R, theme.GridLineColor.G, theme.GridLineColor.B));
                }
            }

            // Also re-theme Label3D nodes
            if (node is Label3D label)
            {
                if (label.Modulate.G > label.Modulate.R * 2f) // Cyan/teal text
                    label.Modulate = theme.EntryMarkerColor;
                else if (label.Modulate.R > 0.5f && label.Modulate.G < 0.3f) // Red text
                    label.Modulate = theme.ExitMarkerColor;
            }

            foreach (var child in node.GetChildren())
                ReThemeRecursive(child, theme);
        }

        // Old approach corridors removed — replaced by BuildTerrainRing

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
