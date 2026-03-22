using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Asset Sandbox — browse all available 3D models, preview them with the
    /// current planet theme applied, and configure faction/role assignments.
    ///
    /// Workflow:
    /// 1. Browse asset library (buildings, turrets, props, enemies, player models)
    /// 2. Click to load a 3D preview
    /// 3. Apply current planet theme with one click (or pick a faction)
    /// 4. Rotate/zoom the preview to inspect
    /// 5. Save the themed config for use in gameplay
    /// </summary>
    public partial class AssetSandboxEditor : EditorModule
    {
        public override string ModuleName => "Asset Sandbox";
        public override Color AccentColor => new(0.0f, 0.85f, 0.95f);

        private VBoxContainer _assetList;
        private VBoxContainer _inspector;
        private SubViewport _previewViewport;
        private SubViewportContainer _previewContainer;
        private Node3D _previewRoot;
        private Node3D _previewModel;
        private Node3D _previewPivot; // Rotation pivot — model is offset inside this
        private Camera3D _previewCamera;
        private DirectionalLight3D _previewLight;

        private string _selectedAssetPath;
        private string _selectedAssetName;
        private float _previewRotation;
        private float _previewZoom = 8f;

        // Faction assignment for preview
        private int _selectedFaction; // 0=player, 1=scavenger, 2=brute, 3=swarm, 4=ghost, 5=original
        private Label _saveStatusLabel;

        private static readonly string[] FactionNames = {
            "Player (Blue)", "Scavenger (Red)", "Brute (Crimson)",
            "Swarm (Orange)", "Ghost (Magenta)", "Original Materials"
        };

        private static readonly string[] OutlineModes = {
            "Per-Mesh Outline", "Silhouette Only", "No Outline (Body Only)"
        };
        private int _outlineMode; // 0=per-mesh, 1=silhouette, 2=none

        public override void _Ready()
        {
            BuildUI();
        }

        private void BuildUI()
        {
            var split = new HSplitContainer();
            split.SizeFlagsVertical = SizeFlags.ExpandFill;
            split.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            AddChild(split);

            // ── Left: Asset browser ──
            var leftPanel = new PanelContainer();
            leftPanel.CustomMinimumSize = new Vector2(250, 0);
            leftPanel.AddThemeStyleboxOverride("panel", EditorStyles.MakePanel(EditorStyles.BgPanel));
            split.AddChild(leftPanel);

            var leftVBox = new VBoxContainer();
            leftVBox.AddThemeConstantOverride("separation", 2);
            leftPanel.AddChild(leftVBox);

            leftVBox.AddChild(EditorStyles.MakeLabel("Asset Library", 16, AccentColor));
            leftVBox.AddChild(EditorStyles.MakeSeparator());

            var scroll = new ScrollContainer();
            scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            leftVBox.AddChild(scroll);

            _assetList = new VBoxContainer();
            _assetList.AddThemeConstantOverride("separation", 1);
            scroll.AddChild(_assetList);

            // Verify buttons
            var verifyBtn = EditorStyles.MakeButton("Verify (Raw)", 12, EditorStyles.StatusWarn);
            verifyBtn.CustomMinimumSize = new Vector2(0, 28);
            verifyBtn.Pressed += () => RunVerifyAll(applyTheme: false);
            leftVBox.AddChild(verifyBtn);

            var verifyThemedBtn = EditorStyles.MakeButton("Verify (Themed)", 12, AccentColor);
            verifyThemedBtn.CustomMinimumSize = new Vector2(0, 28);
            verifyThemedBtn.Pressed += () => RunVerifyAll(applyTheme: true);
            leftVBox.AddChild(verifyThemedBtn);

            leftVBox.AddChild(EditorStyles.MakeSeparator());

            PopulateAssetList();

            // ── Right: Preview + Inspector ──
            var rightVBox = new VBoxContainer();
            rightVBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            rightVBox.AddThemeConstantOverride("separation", 4);
            split.AddChild(rightVBox);

            // 3D Preview viewport — fills most of the right panel
            _previewContainer = new SubViewportContainer();
            _previewContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
            _previewContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _previewContainer.Stretch = true;
            rightVBox.AddChild(_previewContainer);

            _previewViewport = new SubViewport();
            _previewViewport.Size = new Vector2I(1024, 768);
            _previewViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
            _previewViewport.OwnWorld3D = true;
            _previewViewport.TransparentBg = false;
            _previewContainer.AddChild(_previewViewport);

            // Preview scene setup
            _previewRoot = new Node3D();
            _previewViewport.AddChild(_previewRoot);

            _previewCamera = new Camera3D();
            _previewCamera.Fov = 50;
            _previewRoot.AddChild(_previewCamera);
            // Must set current AFTER adding to tree inside SubViewport
            _previewCamera.Current = true;
            UpdatePreviewCamera();

            // Strong main light so models are always visible
            _previewLight = new DirectionalLight3D();
            _previewLight.RotationDegrees = new Vector3(-40, -30, 0);
            _previewLight.LightColor = new Color(0.9f, 0.9f, 0.9f);
            _previewLight.LightEnergy = 1.2f;
            _previewLight.ShadowEnabled = true;
            _previewRoot.AddChild(_previewLight);

            // Fill light from opposite side
            var fillLight = new DirectionalLight3D();
            fillLight.RotationDegrees = new Vector3(-20, 150, 0);
            fillLight.LightColor = new Color(0.4f, 0.5f, 0.6f);
            fillLight.LightEnergy = 0.5f;
            _previewRoot.AddChild(fillLight);

            // Preview environment — bright enough to see models clearly
            var worldEnv = new WorldEnvironment();
            var env = new Godot.Environment();
            env.BackgroundMode = Godot.Environment.BGMode.Color;
            env.BackgroundColor = new Color(0.08f, 0.08f, 0.1f);
            env.AmbientLightColor = new Color(0.3f, 0.3f, 0.35f);
            env.AmbientLightEnergy = 0.8f;
            worldEnv.Environment = env;
            _previewRoot.AddChild(worldEnv);

            // Ground plane — checker pattern so you can see scale
            var ground = new MeshInstance3D();
            var groundMesh = new PlaneMesh();
            groundMesh.Size = new Vector2(20, 20);
            ground.Mesh = groundMesh;
            var groundMat = new StandardMaterial3D();
            groundMat.AlbedoColor = new Color(0.12f, 0.12f, 0.15f);
            groundMat.Roughness = 0.9f;
            ground.MaterialOverride = groundMat;
            _previewRoot.AddChild(ground);

            // Inspector below preview — scrollable, doesn't steal from preview
            var inspectorPanel = new PanelContainer();
            inspectorPanel.CustomMinimumSize = new Vector2(0, 180);
            inspectorPanel.SizeFlagsVertical = SizeFlags.ShrinkEnd;
            inspectorPanel.AddThemeStyleboxOverride("panel", EditorStyles.MakePanel(EditorStyles.BgPanel));
            rightVBox.AddChild(inspectorPanel);

            var inspScroll = new ScrollContainer();
            inspScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            inspectorPanel.AddChild(inspScroll);

            _inspector = new VBoxContainer();
            _inspector.AddThemeConstantOverride("separation", 4);
            _inspector.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            inspScroll.AddChild(_inspector);

            _inspector.AddChild(EditorStyles.MakeLabel("Select an asset to preview", 14, EditorStyles.TextMuted));
        }

        private void PopulateAssetList()
        {
            foreach (var child in _assetList.GetChildren())
                child.QueueFree();

            AddCategory("AXIS");
            AddAssetButton("Repeater Tower", AssetLibrary.AXIS_REPEATER);
            AddAssetButton("Power Mast", AssetLibrary.AXIS_POWER_MAST);
            AddAssetButton("Eye Drone", AssetLibrary.AXIS_EYE_DRONE);

            AddCategory("Buildings");
            AddAssetButton("Outpost", AssetLibrary.BLDG_OUTPOST);
            AddAssetButton("Barracks", AssetLibrary.BLDG_BARRACKS);
            AddAssetButton("Fuel Tanks", AssetLibrary.BLDG_FUEL_TANKS);
            AddAssetButton("Trench", AssetLibrary.BLDG_TRENCH);

            AddCategory("Turrets");
            AddAssetButton("Turret A", AssetLibrary.TURRET_A);
            AddAssetButton("Turret B", AssetLibrary.TURRET_B);
            AddAssetButton("Turret C", AssetLibrary.TURRET_C);
            AddAssetButton("Weapon A", AssetLibrary.WEAPON_A);
            AddAssetButton("Weapon B", AssetLibrary.WEAPON_B);
            AddAssetButton("Rocket Launcher", AssetLibrary.ROCKET_LAUNCHER);
            AddAssetButton("Plasma Gun", AssetLibrary.PLASMA_GUN);

            AddCategory("Props");
            AddAssetButton("Barrier A", AssetLibrary.PROP_BARRIER_A);
            AddAssetButton("Container A", AssetLibrary.PROP_CONTAINER_A);
            AddAssetButton("Generator A", AssetLibrary.PROP_GENERATOR_A);
            AddAssetButton("Radar", AssetLibrary.PROP_RADAR);
            AddAssetButton("Satellite", AssetLibrary.PROP_SATELLITE);
            AddAssetButton("Antenna A", AssetLibrary.PROP_ANTENNA_A);
            AddAssetButton("Barrel", AssetLibrary.PROP_BARREL);
            AddAssetButton("Crate A", AssetLibrary.PROP_CRATE_A);
            AddAssetButton("Lamp Post", AssetLibrary.PROP_LAMP_A);
            AddAssetButton("Sandbags", AssetLibrary.PROP_SANDBAGS);

            AddCategory("Enemies");
            AddAssetButton("Scrap Rat", AssetLibrary.ENEMY_SCRAP_RAT);
            AddAssetButton("Wire Worm", AssetLibrary.ENEMY_WIRE_WORM);
            AddAssetButton("Trilobite", AssetLibrary.ENEMY_TRILOBITE);
            AddAssetButton("Quad Shell", AssetLibrary.ENEMY_QUAD_SHELL);
            AddAssetButton("Spark Drone", AssetLibrary.ENEMY_SPARK_DRONE);
            AddAssetButton("Grunt Mech", AssetLibrary.ENEMY_GRUNT_MECH);

            AddCategory("Player");
            AddAssetButton("Clunker", AssetLibrary.PLAYER_CLUNKER);
            AddAssetButton("Rustbucket", AssetLibrary.PLAYER_RUSTBUCKET);
            AddAssetButton("Sparkplug", AssetLibrary.PLAYER_SPARKPLUG);
        }

        private void AddCategory(string name)
        {
            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_top", 8);
            margin.AddChild(EditorStyles.MakeLabel(name, 12, EditorStyles.TextSecondary));
            _assetList.AddChild(margin);
        }

        private void AddAssetButton(string name, string path)
        {
            var btn = new Button();
            btn.Text = name;
            btn.Alignment = HorizontalAlignment.Left;
            btn.CustomMinimumSize = new Vector2(0, 26);
            btn.Pressed += () => LoadAssetPreview(name, path);
            _assetList.AddChild(btn);
        }

        private void LoadAssetPreview(string name, string path)
        {
            _selectedAssetPath = path;
            _selectedAssetName = name;
            _previewRotation = 0;
            _selectedFaction = 5; // Start with original materials

            // Clear old preview
            if (_previewPivot != null)
            {
                _previewPivot.QueueFree();
                _previewPivot = null;
                _previewModel = null;
            }

            // Load model
            var model = AssetLibrary.Instantiate(path);
            if (model == null)
            {
                BuildInspectorError(name, path);
                return;
            }

            float normScale = AssetLibrary.GetNormalizedScale(path);

            // Simple approach: pivot at origin, model inside it
            _previewPivot = new Node3D();
            _previewRoot.AddChild(_previewPivot);

            model.Scale = Vector3.One * normScale;
            _previewPivot.AddChild(model);
            _previewModel = model;

            // Show original materials first — user applies theme manually
            _selectedFaction = 5; // Original Materials

            // Let Godot process one frame so AABB is valid, then center
            CallDeferred(nameof(FinalizePreview), name, normScale);
        }

        private void BuildInspector(string name, string path, Aabb aabb, float normScale)
        {
            foreach (var child in _inspector.GetChildren())
                child.QueueFree();

            // Header
            _inspector.AddChild(EditorStyles.MakeLabel(name, 18, AccentColor));
            _inspector.AddChild(EditorStyles.MakeLabel(
                System.IO.Path.GetFileName(path), 11, EditorStyles.TextMuted));

            var nativeSize = aabb.Size;
            var gameSize = nativeSize * normScale;
            _inspector.AddChild(EditorStyles.MakeLabel(
                $"Native: {nativeSize.X:F1} x {nativeSize.Y:F1} x {nativeSize.Z:F1}  |  Scale: {normScale}x  |  Game: {gameSize.X:F1} x {gameSize.Y:F1} x {gameSize.Z:F1}",
                11, EditorStyles.TextSecondary));

            // Scale override editor
            var scaleRow = new HBoxContainer();
            scaleRow.AddThemeConstantOverride("separation", 8);
            scaleRow.AddChild(EditorStyles.MakeLabel("Scale Override:", 12));
            var scaleSpin = EditorStyles.MakeSpinBox(normScale, 0.01f, 500f, normScale < 1f ? 0.01f : 1f);
            scaleSpin.ValueChanged += (double v) => {
                if (_previewModel != null)
                    _previewModel.Scale = Vector3.One * (float)v;
            };
            scaleRow.AddChild(scaleSpin);
            _inspector.AddChild(scaleRow);

            _inspector.AddChild(EditorStyles.MakeSeparator());

            // Theme application
            _inspector.AddChild(EditorStyles.MakeLabel("Apply Planet Theme", 14, EditorStyles.TextPrimary));

            var factionPicker = new OptionButton();
            factionPicker.AddThemeFontSizeOverride("font_size", 13);
            foreach (var fname in FactionNames)
                factionPicker.AddItem(fname);
            factionPicker.Selected = _selectedFaction;
            factionPicker.ItemSelected += (long idx) => {
                _selectedFaction = (int)idx;
                ApplyThemeToPreview();
            };
            _inspector.AddChild(factionPicker);

            // Outline mode
            _inspector.AddChild(EditorStyles.MakeLabel("Outline Mode", 12, EditorStyles.TextSecondary));
            var outlinePicker = new OptionButton();
            outlinePicker.AddThemeFontSizeOverride("font_size", 13);
            foreach (var mode in OutlineModes)
                outlinePicker.AddItem(mode);
            outlinePicker.Selected = _outlineMode;
            outlinePicker.ItemSelected += (long idx) => { _outlineMode = (int)idx; };
            _inspector.AddChild(outlinePicker);

            // Apply button
            var applyBtn = EditorStyles.MakeButton("Apply Theme", 14, AccentColor);
            applyBtn.Pressed += ApplyThemeToPreview;
            _inspector.AddChild(applyBtn);

            // Save themed version
            var saveBtn = EditorStyles.MakeButton("Save Themed Version", 14, EditorStyles.StatusOk);
            saveBtn.Pressed += SaveThemedVersion;
            _inspector.AddChild(saveBtn);

            _saveStatusLabel = EditorStyles.MakeLabel("", 11, EditorStyles.TextMuted);
            _inspector.AddChild(_saveStatusLabel);

            // Rotate controls
            _inspector.AddChild(EditorStyles.MakeSeparator());
            _inspector.AddChild(EditorStyles.MakeLabel("Preview Controls", 12, EditorStyles.TextSecondary));
            _inspector.AddChild(EditorStyles.MakeLabel(
                "Drag on preview to rotate. Scroll to zoom.", 11, EditorStyles.TextMuted));

            // Planet selector
            _inspector.AddChild(EditorStyles.MakeSeparator());
            _inspector.AddChild(EditorStyles.MakeLabel("Planet Theme", 14, EditorStyles.TextPrimary));

            var planetPicker = new OptionButton();
            planetPicker.AddThemeFontSizeOverride("font_size", 13);
            planetPicker.AddItem("Grid Prime (Tron)");
            planetPicker.AddItem("Scrapyard (Rust/Metal)");
            planetPicker.Selected = PlanetTheme.Current is TronPlanetTheme ? 0 : 1;
            planetPicker.ItemSelected += (long idx) => {
                PlanetTheme.Current = idx == 0
                    ? new TronPlanetTheme()
                    : new ScrapyardPlanetTheme();
                UpdatePreviewEnvironment();
                ApplyThemeToPreview();
            };
            _inspector.AddChild(planetPicker);

            var theme = PlanetTheme.Current;
            _inspector.AddChild(EditorStyles.MakeLabel($"Current: {theme.PlanetName}", 12, AccentColor));
            _inspector.AddChild(EditorStyles.MakeLabel(theme.PlanetDescription, 11, EditorStyles.TextMuted));
        }

        private void BuildInspectorError(string name, string path)
        {
            foreach (var child in _inspector.GetChildren())
                child.QueueFree();

            _inspector.AddChild(EditorStyles.MakeLabel($"Failed to load: {name}", 16, EditorStyles.StatusError));
            _inspector.AddChild(EditorStyles.MakeLabel(path, 11, EditorStyles.TextMuted));
        }

        private void ApplyThemeToPreview()
        {
            if (_previewModel == null) return;

            // Reload fresh model (undo previous theme) — keep same scale and position
            var path = _selectedAssetPath;
            var oldScale = _previewModel.Scale;
            var oldPos = _previewModel.Position;
            _previewModel.QueueFree();

            _previewModel = AssetLibrary.Instantiate(path);
            if (_previewModel == null) return;

            _previewModel.Scale = oldScale;
            _previewModel.Position = oldPos;
            _previewPivot.AddChild(_previewModel);

            // Apply selected theme with outline mode
            GD.Print($"[Sandbox] Applying theme: faction={_selectedFaction}, outlineMode={_outlineMode}");
            var theme = PlanetTheme.Current;
            if (theme is TronPlanetTheme tron)
                tron.OutlineMode = _outlineMode;
            else
                GD.Print($"[Sandbox] Theme is NOT TronPlanetTheme: {theme.GetType().Name}");

            switch (_selectedFaction)
            {
                case 0: // Player
                    theme.ApplyToNode(_previewModel, theme.PlayerPrimary);
                    break;
                case 1: // Scavenger
                    theme.ApplyEnemyTheme(_previewModel, VineEnemyFaction.Scavenger);
                    break;
                case 2: // Brute
                    theme.ApplyEnemyTheme(_previewModel, VineEnemyFaction.Brute);
                    break;
                case 3: // Swarm
                    theme.ApplyEnemyTheme(_previewModel, VineEnemyFaction.Swarm);
                    break;
                case 4: // Ghost
                    theme.ApplyEnemyTheme(_previewModel, VineEnemyFaction.Ghost);
                    break;
                case 5: // Original — don't apply any theme
                    break;
            }

            EditorManager.Instance?.SetStatus($"Applied {FactionNames[_selectedFaction]} theme to {_selectedAssetName}");
        }

        /// <summary>
        /// Load every asset in the library, verify it has meshes and materials,
        /// measure its size, and report results to the console + inspector.
        /// </summary>
        private void RunVerifyAll(bool applyTheme)
        {
            foreach (var child in _inspector.GetChildren())
                child.QueueFree();

            string mode = applyTheme ? $"Themed ({PlanetTheme.Current.PlanetName})" : "Raw (original materials)";
            _inspector.AddChild(EditorStyles.MakeLabel($"Verification: {mode}", 16, applyTheme ? AccentColor : EditorStyles.StatusWarn));
            _inspector.AddChild(EditorStyles.MakeSeparator());

            GD.Print($"[Verify] Starting verification — mode: {mode}");

            var allAssets = new (string name, string path)[] {
                ("Repeater Tower", AssetLibrary.AXIS_REPEATER),
                ("Power Mast", AssetLibrary.AXIS_POWER_MAST),
                ("Eye Drone", AssetLibrary.AXIS_EYE_DRONE),
                ("Outpost", AssetLibrary.BLDG_OUTPOST),
                ("Barracks", AssetLibrary.BLDG_BARRACKS),
                ("Fuel Tanks", AssetLibrary.BLDG_FUEL_TANKS),
                ("Trench", AssetLibrary.BLDG_TRENCH),
                ("Turret A", AssetLibrary.TURRET_A),
                ("Turret B", AssetLibrary.TURRET_B),
                ("Turret C", AssetLibrary.TURRET_C),
                ("Weapon A", AssetLibrary.WEAPON_A),
                ("Weapon B", AssetLibrary.WEAPON_B),
                ("Rocket Launcher", AssetLibrary.ROCKET_LAUNCHER),
                ("Plasma Gun", AssetLibrary.PLASMA_GUN),
                ("Barrier A", AssetLibrary.PROP_BARRIER_A),
                ("Barrier B", AssetLibrary.PROP_BARRIER_B),
                ("Container A", AssetLibrary.PROP_CONTAINER_A),
                ("Container B", AssetLibrary.PROP_CONTAINER_B),
                ("Crate A", AssetLibrary.PROP_CRATE_A),
                ("Crate B", AssetLibrary.PROP_CRATE_B),
                ("Barrel", AssetLibrary.PROP_BARREL),
                ("Barrels", AssetLibrary.PROP_BARRELS),
                ("Generator A", AssetLibrary.PROP_GENERATOR_A),
                ("Generator B", AssetLibrary.PROP_GENERATOR_B),
                ("Radar", AssetLibrary.PROP_RADAR),
                ("Satellite", AssetLibrary.PROP_SATELLITE),
                ("Antenna A", AssetLibrary.PROP_ANTENNA_A),
                ("Antenna B", AssetLibrary.PROP_ANTENNA_B),
                ("Lamp A", AssetLibrary.PROP_LAMP_A),
                ("Lamp B", AssetLibrary.PROP_LAMP_B),
                ("Sandbags", AssetLibrary.PROP_SANDBAGS),
                ("Hedgehog", AssetLibrary.PROP_HEDGEHOG),
                ("Fence", AssetLibrary.PROP_FENCE),
                ("Scrap Rat", AssetLibrary.ENEMY_SCRAP_RAT),
                ("Wire Worm", AssetLibrary.ENEMY_WIRE_WORM),
                ("Trilobite", AssetLibrary.ENEMY_TRILOBITE),
                ("Quad Shell", AssetLibrary.ENEMY_QUAD_SHELL),
                ("Spark Drone", AssetLibrary.ENEMY_SPARK_DRONE),
                ("Grunt Mech", AssetLibrary.ENEMY_GRUNT_MECH),
                ("Clunker", AssetLibrary.PLAYER_CLUNKER),
                ("Rustbucket", AssetLibrary.PLAYER_RUSTBUCKET),
                ("Sparkplug", AssetLibrary.PLAYER_SPARKPLUG),
            };

            int passed = 0, failed = 0, noMesh = 0;

            foreach (var (name, path) in allAssets)
            {
                var instance = AssetLibrary.Instantiate(path);
                if (instance == null)
                {
                    failed++;
                    _inspector.AddChild(EditorStyles.MakeLabel(
                        $"FAIL: {name} — could not load from {System.IO.Path.GetFileName(path)}", 11, EditorStyles.StatusError));
                    GD.PrintErr($"[Verify] FAIL: {name} ({path}) — load returned null");
                    continue;
                }

                float scale = AssetLibrary.GetNormalizedScale(path);
                instance.Scale = Vector3.One * scale;

                // Temporarily add to tree to measure
                _previewRoot.AddChild(instance);

                // Apply planet theme if requested
                if (applyTheme)
                    PlanetTheme.Current.ApplyToNode(instance);

                var aabb = AssetLibrary.GetCombinedAABB(instance);
                var gameSize = aabb.Size * scale;

                // Deep analysis of meshes and materials
                var report = AnalyzeMeshes(instance);
                instance.QueueFree();

                string sizeStr = $"{gameSize.X:F1}x{gameSize.Y:F1}x{gameSize.Z:F1} @ {scale}x";
                string status;
                Color statusColor;

                if (report.MeshCount == 0 || report.EmptyMeshCount == report.MeshCount)
                {
                    failed++;
                    status = $"FAIL  {name} — no meshes ({sizeStr})";
                    statusColor = EditorStyles.StatusError;
                }
                else if (report.MaterialCount == 0)
                {
                    failed++;
                    status = $"FAIL  {name} — {report.MeshCount} meshes, 0 materials ({sizeStr})";
                    statusColor = EditorStyles.StatusError;
                }
                else if (report.TexturedMaterialCount > 0 && report.PlaceholderCount == 0)
                {
                    passed++;
                    status = $"OK    {name} — {report.TexturedMaterialCount} textured mats ({sizeStr})";
                    statusColor = EditorStyles.StatusOk;
                }
                else if (report.PlaceholderCount > 0 && report.TexturedMaterialCount > 0)
                {
                    noMesh++;
                    status = $"PART  {name} — {report.TexturedMaterialCount}/{report.MaterialCount} textured, {report.PlaceholderCount} need theme ({sizeStr})";
                    statusColor = EditorStyles.StatusWarn;
                }
                else
                {
                    noMesh++;
                    status = $"THEME {name} — {report.MeshCount} meshes, needs planet theme applied ({sizeStr})";
                    statusColor = new Color(0.4f, 0.7f, 1f); // Blue = "theme will fix"
                }

                _inspector.AddChild(EditorStyles.MakeLabel(status, 11, statusColor));
                GD.Print($"[Verify] {status}");

                // Only log detail issues to console, not inspector (keeps it clean)
                if (report.Issues.Count > 0 && report.Issues.Count <= 5)
                {
                    foreach (var issue in report.Issues)
                        GD.Print($"[Verify]   {issue}");
                }
                else if (report.Issues.Count > 5)
                {
                    GD.Print($"[Verify]   ({report.Issues.Count} issues — check console for details)");
                }

                _inspector.AddChild(EditorStyles.MakeLabel(status, 10, statusColor));
            }

            _inspector.AddChild(EditorStyles.MakeSeparator());
            _inspector.AddChild(EditorStyles.MakeLabel(
                $"Results: {passed} OK, {noMesh} warnings, {failed} failed / {allAssets.Length} total",
                14, failed > 0 ? EditorStyles.StatusError : EditorStyles.StatusOk));

            GD.Print($"[Verify] Complete: {passed} OK, {noMesh} warnings, {failed} failed / {allAssets.Length} total");
        }

        public struct MeshReport
        {
            public int MeshCount;
            public int SurfaceCount;
            public int MaterialCount;
            public int TexturedMaterialCount;  // Has albedo texture, not just flat color
            public int PlaceholderCount;       // Default white/grey with no texture
            public int EmptyMeshCount;         // MeshInstance3D with null Mesh
            public List<string> Issues;
        }

        private static MeshReport AnalyzeMeshes(Node root)
        {
            var report = new MeshReport { Issues = new List<string>() };
            AnalyzeMeshesRecursive(root, ref report, 0);
            return report;
        }

        private static void AnalyzeMeshesRecursive(Node node, ref MeshReport report, int depth)
        {
            if (node is MeshInstance3D meshInst)
            {
                report.MeshCount++;

                if (meshInst.Mesh == null)
                {
                    report.EmptyMeshCount++;
                    report.Issues.Add($"Empty mesh on '{meshInst.Name}'");
                    return;
                }

                int surfaces = meshInst.Mesh.GetSurfaceCount();
                report.SurfaceCount += surfaces;

                // Check override material first
                var overrideMat = meshInst.MaterialOverride;
                if (overrideMat != null)
                {
                    report.MaterialCount++;
                    CategorizeMaterial(overrideMat, meshInst.Name, ref report);
                }
                else
                {
                    // Check per-surface materials
                    for (int i = 0; i < surfaces; i++)
                    {
                        var surfMat = meshInst.Mesh.SurfaceGetMaterial(i);
                        if (surfMat == null)
                        {
                            report.PlaceholderCount++;
                            report.Issues.Add($"Surface {i} on '{meshInst.Name}' has NO material");
                        }
                        else
                        {
                            report.MaterialCount++;
                            CategorizeMaterial(surfMat, meshInst.Name, ref report);
                        }
                    }
                }
            }

            foreach (var child in node.GetChildren())
                AnalyzeMeshesRecursive(child, ref report, depth + 1);
        }

        private static void CategorizeMaterial(Material mat, string meshName, ref MeshReport report)
        {
            // ShaderMaterial = custom shader = intentional
            if (mat is ShaderMaterial)
            {
                report.TexturedMaterialCount++;
                return;
            }

            // Handle all BaseMaterial3D subclasses (StandardMaterial3D, ORMMaterial3D, etc.)
            if (mat is BaseMaterial3D baseMat)
            {
                bool hasAlbedoTex = baseMat.AlbedoTexture != null;
                bool hasNormalTex = baseMat.NormalTexture != null;
                bool hasORMTex = baseMat.OrmTexture != null;
                bool hasAnyTexture = hasAlbedoTex || hasNormalTex || hasORMTex;
                bool hasEmission = baseMat.EmissionEnabled && baseMat.EmissionEnergyMultiplier > 0.1f;
                bool hasNonWhiteColor = baseMat.AlbedoColor != Colors.White
                    && baseMat.AlbedoColor.R < 0.95f; // Not near-white

                if (hasAnyTexture)
                {
                    report.TexturedMaterialCount++;
                }
                else if (hasEmission)
                {
                    report.TexturedMaterialCount++;
                }
                else if (hasNonWhiteColor)
                {
                    // Has intentional color but no texture — styled flat material
                    report.TexturedMaterialCount++;
                }
                else
                {
                    var c = baseMat.AlbedoColor;
                    report.PlaceholderCount++;
                    report.Issues.Add($"Default material on '{meshName}': color=({c.R:F2},{c.G:F2},{c.B:F2})");
                }
                return;
            }

            // Unknown material type — assume intentional
            report.TexturedMaterialCount++;
        }

        private void UpdatePreviewEnvironment()
        {
            var theme = PlanetTheme.Current;
            if (_previewLight != null)
                _previewLight.LightColor = theme.MainLightColor;

            // Update ground material
            foreach (var child in _previewRoot.GetChildren())
            {
                if (child is MeshInstance3D mesh && mesh.Mesh is PlaneMesh)
                    mesh.MaterialOverride = theme.MakeGroundMaterial();
                if (child is WorldEnvironment we && we.Environment != null)
                {
                    we.Environment.BackgroundColor = theme.BackgroundColor;
                    we.Environment.AmbientLightColor = theme.AmbientColor;
                }
            }
        }

        // ── Save themed version ──

        private static readonly string[] FactionSuffixes = {
            "Player", "Scavenger", "Brute", "Swarm", "Ghost", "Original"
        };

        private void SaveThemedVersion()
        {
            if (_previewModel == null || string.IsNullOrEmpty(_selectedAssetPath))
            {
                if (_saveStatusLabel != null)
                {
                    _saveStatusLabel.Text = "No model loaded to save.";
                    _saveStatusLabel.AddThemeColorOverride("font_color", EditorStyles.StatusError);
                }
                return;
            }

            var theme = PlanetTheme.Current;
            string planetSlug = theme.PlanetName.Replace(" ", "");
            string factionSlug = FactionSuffixes[_selectedFaction];
            string baseName = _selectedAssetName.Replace(" ", "_");

            // e.g. Turret_A_GridPrime_Scavenger.tscn
            string fileName = $"{baseName}_{planetSlug}_{factionSlug}.tscn";
            string savePath = $"res://Models/Themed/{fileName}";

            try
            {
                // Ensure directory exists
                string globalDir = ProjectSettings.GlobalizePath("res://Models/Themed/");
                if (!System.IO.Directory.Exists(globalDir))
                    System.IO.Directory.CreateDirectory(globalDir);

                // Load a fresh copy, apply theme, and pack as scene
                var freshModel = AssetLibrary.Instantiate(_selectedAssetPath);
                if (freshModel == null)
                {
                    _saveStatusLabel.Text = "Failed to load fresh model.";
                    _saveStatusLabel.AddThemeColorOverride("font_color", EditorStyles.StatusError);
                    return;
                }

                // Apply theme to the fresh copy
                if (_selectedFaction < 5)
                {
                    if (_selectedFaction == 0)
                        theme.ApplyToNode(freshModel, theme.PlayerPrimary);
                    else
                    {
                        var faction = _selectedFaction switch {
                            1 => VineEnemyFaction.Scavenger,
                            2 => VineEnemyFaction.Brute,
                            3 => VineEnemyFaction.Swarm,
                            4 => VineEnemyFaction.Ghost,
                            _ => VineEnemyFaction.Scavenger
                        };
                        theme.ApplyEnemyTheme(freshModel, faction);
                    }
                }

                // We need the model in the tree briefly to pack it
                _previewRoot.AddChild(freshModel);

                var packedScene = new PackedScene();
                packedScene.Pack(freshModel);
                var error = ResourceSaver.Save(packedScene, savePath);

                freshModel.QueueFree();

                if (error == Error.Ok)
                {
                    _saveStatusLabel.Text = $"Saved: {fileName}";
                    _saveStatusLabel.AddThemeColorOverride("font_color", EditorStyles.StatusOk);
                    GD.Print($"[AssetSandbox] Saved themed version: {savePath}");
                }
                else
                {
                    _saveStatusLabel.Text = $"Save failed: {error}";
                    _saveStatusLabel.AddThemeColorOverride("font_color", EditorStyles.StatusError);
                    GD.PrintErr($"[AssetSandbox] Save failed: {error} for {savePath}");
                }
            }
            catch (System.Exception ex)
            {
                _saveStatusLabel.Text = $"Error: {ex.Message}";
                _saveStatusLabel.AddThemeColorOverride("font_color", EditorStyles.StatusError);
                GD.PrintErr($"[AssetSandbox] Save error: {ex}");
            }
        }

        // ── Preview rotation/zoom ──

        private void FinalizePreview(string name, float normScale)
        {
            if (_previewModel == null || _previewPivot == null) return;

            // Now that the model is in the tree, get its VISUAL bounding box
            // by checking all MeshInstance3D children' global AABBs
            var globalAABB = new Aabb();
            bool first = true;
            CollectGlobalAABB(_previewModel, ref globalAABB, ref first);

            if (!first && globalAABB.Size.Length() > 0.001f)
            {
                // Scale to fit ~3 units
                float maxDim = Mathf.Max(globalAABB.Size.X,
                    Mathf.Max(globalAABB.Size.Y, globalAABB.Size.Z));
                if (maxDim > 0.001f)
                {
                    float fitScale = 3f / maxDim;
                    _previewPivot.Scale = Vector3.One * fitScale;
                }

                // Recalculate AABB after scale
                globalAABB = new Aabb();
                first = true;
                CollectGlobalAABB(_previewModel, ref globalAABB, ref first);

                // Move pivot so model center is at world origin, bottom at y=0
                var center = globalAABB.GetCenter();
                var bottom = globalAABB.Position.Y;
                _previewPivot.Position = new Vector3(-center.X, -bottom, -center.Z);
            }

            _previewZoom = 6f;
            UpdatePreviewCamera();

            var gameSize = globalAABB.Size;
            GD.Print($"[Sandbox] {name}: visible size={gameSize.X:F1}x{gameSize.Y:F1}x{gameSize.Z:F1}, normScale={normScale}");

            var aabb = AssetLibrary.GetCombinedAABB(_previewModel);
            BuildInspector(name, _selectedAssetPath, aabb, normScale);
        }

        private static void CollectGlobalAABB(Node node, ref Aabb result, ref bool first)
        {
            if (node is MeshInstance3D mesh && mesh.Mesh != null)
            {
                var meshAabb = mesh.GetAabb();
                // Transform to global space
                var globalTransform = mesh.GlobalTransform;
                var corners = new Vector3[8];
                corners[0] = globalTransform * new Vector3(meshAabb.Position.X, meshAabb.Position.Y, meshAabb.Position.Z);
                corners[1] = globalTransform * new Vector3(meshAabb.End.X, meshAabb.Position.Y, meshAabb.Position.Z);
                corners[2] = globalTransform * new Vector3(meshAabb.Position.X, meshAabb.End.Y, meshAabb.Position.Z);
                corners[3] = globalTransform * new Vector3(meshAabb.End.X, meshAabb.End.Y, meshAabb.Position.Z);
                corners[4] = globalTransform * new Vector3(meshAabb.Position.X, meshAabb.Position.Y, meshAabb.End.Z);
                corners[5] = globalTransform * new Vector3(meshAabb.End.X, meshAabb.Position.Y, meshAabb.End.Z);
                corners[6] = globalTransform * new Vector3(meshAabb.Position.X, meshAabb.End.Y, meshAabb.End.Z);
                corners[7] = globalTransform * new Vector3(meshAabb.End.X, meshAabb.End.Y, meshAabb.End.Z);

                foreach (var corner in corners)
                {
                    if (first)
                    {
                        result = new Aabb(corner, Vector3.Zero);
                        first = false;
                    }
                    else
                    {
                        result = result.Expand(corner);
                    }
                }
            }
            foreach (var child in node.GetChildren())
                CollectGlobalAABB(child, ref result, ref first);
        }

        public override void _Process(double delta)
        {
            // No auto-rotation — user controls the camera
        }

        private float _orbitAngleX;  // Horizontal orbit (yaw)
        private float _orbitAngleY = -25f; // Vertical orbit (pitch) — slight downward look

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseMotion mm && Input.IsMouseButtonPressed(MouseButton.Left))
            {
                _orbitAngleX += mm.Relative.X * 0.4f;
                _orbitAngleY = Mathf.Clamp(_orbitAngleY + mm.Relative.Y * 0.4f, -80f, 80f);
                UpdatePreviewCamera();
            }
            else if (@event is InputEventMouseButton mb)
            {
                if (mb.ButtonIndex == MouseButton.WheelUp)
                {
                    _previewZoom = Mathf.Max(2f, _previewZoom - 0.5f);
                    UpdatePreviewCamera();
                }
                else if (mb.ButtonIndex == MouseButton.WheelDown)
                {
                    _previewZoom = Mathf.Min(20f, _previewZoom + 0.5f);
                    UpdatePreviewCamera();
                }
            }
        }

        private void UpdatePreviewCamera()
        {
            if (_previewCamera == null) return;

            // Orbit camera around origin
            float yawRad = Mathf.DegToRad(_orbitAngleX);
            float pitchRad = Mathf.DegToRad(_orbitAngleY);

            float x = _previewZoom * Mathf.Cos(pitchRad) * Mathf.Sin(yawRad);
            float y = _previewZoom * Mathf.Sin(-pitchRad) + 1.5f; // +1.5 so we look slightly above ground
            float z = _previewZoom * Mathf.Cos(pitchRad) * Mathf.Cos(yawRad);

            _previewCamera.Position = new Vector3(x, y, z);
            _previewCamera.LookAt(new Vector3(0, 1, 0), Vector3.Up);
        }
    }
}
