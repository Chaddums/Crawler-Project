using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Main level editor scene. Sets up 3D world with VineGrid,
    /// editor camera, UI overlay, and tool state machine.
    /// </summary>
    public partial class LevelEditorScene : Node3D
    {
        private LevelData _levelData;
        private VineGrid _grid;
        private LevelEditorCamera _camera;
        private LevelEditorUI _ui;
        private LevelEditorTools _tools;
        private LevelEditorSelection _selection;
        private LevelEditorAssembly _assembly;
        private LevelEditorGrid2D _minimap;
        private LevelEditorInspector _inspector;
        private UndoStack<string> _undoStack = new(50);
        private Node3D _scaleReferences;
        private bool _scaleRefsVisible;

        public LevelData CurrentLevel => _levelData;
        public VineGrid Grid => _grid;
        public LevelEditorCamera Camera => _camera;
        public UndoStack<string> UndoStack => _undoStack;

        public override void _Ready()
        {
            GameManager.Instance?.SetPhase(GamePhase.LevelEditor);

            // Default to tron theme
            PlanetTheme.Current = new TronPlanetTheme();

            // Create a new empty level or load one
            _levelData = new LevelData
            {
                Id = "untitled",
                Name = "Untitled",
                Floor = 1,
                Width = Constants.VINE_MAP_WIDTH,
                Height = Constants.VINE_MAP_HEIGHT,
                HeightmapProfile = "Gentle",
                PlanetTheme = "tron"
            };

            SetupEnvironment();
            SetupGrid();
            SetupCamera();
            SetupUI();
            SetupSelection();
            SetupAssembly();
            SetupTools();
            SetupScaleReferences();

            // Save initial undo state
            PushUndoState();
        }

        private void SetupEnvironment()
        {
            // World environment
            var env = new WorldEnvironment();
            var environment = new Godot.Environment();
            environment.BackgroundMode = Godot.Environment.BGMode.Color;
            environment.BackgroundColor = TronTheme.Background;
            environment.AmbientLightSource = Godot.Environment.AmbientSource.Color;
            environment.AmbientLightColor = new Color(0.15f, 0.18f, 0.25f);
            environment.AmbientLightEnergy = 0.4f;
            env.Environment = environment;
            AddChild(env);

            // Main directional light
            var light = new DirectionalLight3D();
            light.RotationDegrees = new Vector3(-45, -30, 0);
            light.LightColor = new Color(0.7f, 0.75f, 0.9f);
            light.LightEnergy = 0.8f;
            light.ShadowEnabled = true;
            AddChild(light);

            // Fill light
            var fill = new DirectionalLight3D();
            fill.RotationDegrees = new Vector3(-20, 150, 0);
            fill.LightColor = new Color(0.3f, 0.35f, 0.5f);
            fill.LightEnergy = 0.3f;
            fill.ShadowEnabled = false;
            AddChild(fill);
        }

        private void SetupGrid()
        {
            _grid = new VineGrid();
            _grid.Width = _levelData.Width;
            _grid.Height = _levelData.Height;
            AddChild(_grid);

            // Generate default heightmap
            _grid.GenerateHeightmap(TerrainProfile.Gentle);
            _grid.RebuildTerrainMesh();
            _grid.BuildGridLines();
        }

        private void SetupCamera()
        {
            _camera = new LevelEditorCamera();
            _camera.Current = true;
            AddChild(_camera);
        }

        private void SetupUI()
        {
            _ui = new LevelEditorUI();
            _ui.Name = "LevelEditorUI";
            _ui.Editor = this;
            AddChild(_ui);
        }

        private void SetupSelection()
        {
            _selection = new LevelEditorSelection();
            _selection.Name = "LevelEditorSelection";
            _selection.Editor = this;
            AddChild(_selection);
        }

        private void SetupAssembly()
        {
            _assembly = new LevelEditorAssembly();
            _assembly.Name = "LevelEditorAssembly";
            _assembly.Editor = this;
            AddChild(_assembly);
        }

        private void SetupTools()
        {
            _tools = new LevelEditorTools();
            _tools.Name = "LevelEditorTools";
            _tools.Editor = this;
            _tools.InitSelection(_selection);
            AddChild(_tools);
        }

        // ── Level Operations ──

        public void NewLevel()
        {
            _levelData = new LevelData
            {
                Id = "untitled",
                Name = "Untitled",
                Floor = 1,
                Width = Constants.VINE_MAP_WIDTH,
                Height = Constants.VINE_MAP_HEIGHT,
                HeightmapProfile = "Gentle",
                PlanetTheme = "tron"
            };
            RebuildGrid();
            _undoStack.Clear();
            PushUndoState();
            _ui?.RefreshAll();
        }

        public void LoadLevel(string filename)
        {
            var data = LevelSerializer.LoadFromFile(filename);
            if (data == null) return;
            _levelData = data;
            ApplyPlanetTheme();
            RebuildGrid();
            _undoStack.Clear();
            PushUndoState();
            _ui?.RefreshAll();
        }

        public void SaveLevel()
        {
            SyncGridToData();
            string filename = $"{_levelData.Id}.json";
            LevelSerializer.SaveToFile(_levelData, filename);
        }

        public void SaveLevelAs(string name, string id)
        {
            _levelData.Name = name;
            _levelData.Id = id;
            SaveLevel();
            _ui?.RefreshAll();
        }

        public void ExportHardcodedFloors()
        {
            LevelExporter.ExportAll();
        }

        public void TestPlay()
        {
            SyncGridToData();
            string filename = "_editor_test.json";
            LevelSerializer.SaveToFile(_levelData, filename);

            // S1: floors removed — just start battle
            GameManager.Instance?.StartVineBattle();
        }

        /// <summary>
        /// Load one of the 3 hardcoded floors directly into the editor.
        /// Exports it to LevelData first (no JSON file needed on disk).
        /// </summary>
        public void LoadHardcodedFloor(int floor)
        {
            _levelData = LevelExporter.ExportFromHardcoded(floor);
            ApplyPlanetTheme();
            RebuildGrid();
            _undoStack.Clear();
            PushUndoState();
            _ui?.RefreshAll();
            GD.Print($"[LevelEditor] Loaded hardcoded floor {floor}: {_levelData.Name}");
        }

        // ── Scale References ──

        private void SetupScaleReferences()
        {
            _scaleReferences = new Node3D();
            _scaleReferences.Name = "ScaleReferences";
            AddChild(_scaleReferences);
            _scaleReferences.Visible = false;

            float cs = Constants.VINE_CELL_SIZE;
            var accentMat = new StandardMaterial3D();
            accentMat.AlbedoColor = new Color(0f, 0.85f, 0.95f, 0.6f);
            accentMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            accentMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;

            var yellowMat = new StandardMaterial3D();
            yellowMat.AlbedoColor = new Color(1f, 0.9f, 0.2f, 0.7f);
            yellowMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            yellowMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;

            var whiteMat = new StandardMaterial3D();
            whiteMat.AlbedoColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);
            whiteMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            whiteMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;

            // 1) Human figure silhouette (~1.8m = 1.8 units, standing at grid origin area)
            float humanH = 1.8f;
            var humanGroup = new Node3D();
            humanGroup.Position = new Vector3(2 * cs, 0, 2 * cs);

            // Body (capsule approximated as cylinder + sphere head)
            var body = new MeshInstance3D();
            var bodyCyl = new CylinderMesh();
            bodyCyl.TopRadius = 0.2f;
            bodyCyl.BottomRadius = 0.25f;
            bodyCyl.Height = humanH * 0.65f;
            body.Mesh = bodyCyl;
            body.MaterialOverride = yellowMat;
            body.Position = new Vector3(0, humanH * 0.35f, 0);
            humanGroup.AddChild(body);

            // Head
            var head = new MeshInstance3D();
            var headSphere = new SphereMesh();
            headSphere.Radius = 0.15f;
            headSphere.Height = 0.3f;
            head.Mesh = headSphere;
            head.MaterialOverride = yellowMat;
            head.Position = new Vector3(0, humanH * 0.65f + 0.15f + 0.05f, 0);
            humanGroup.AddChild(head);

            // Legs (two thin cylinders)
            for (int leg = -1; leg <= 1; leg += 2)
            {
                var legMesh = new MeshInstance3D();
                var legCyl = new CylinderMesh();
                legCyl.TopRadius = 0.08f;
                legCyl.BottomRadius = 0.1f;
                legCyl.Height = humanH * 0.45f;
                legMesh.Mesh = legCyl;
                legMesh.MaterialOverride = yellowMat;
                legMesh.Position = new Vector3(leg * 0.12f, humanH * 0.45f * 0.5f - 0.02f, 0);
                humanGroup.AddChild(legMesh);
            }

            // Label post: "1.8m" height marker line
            var heightPost = new MeshInstance3D();
            var postMesh = new CylinderMesh();
            postMesh.TopRadius = 0.015f;
            postMesh.BottomRadius = 0.015f;
            postMesh.Height = humanH;
            heightPost.Mesh = postMesh;
            heightPost.MaterialOverride = accentMat;
            heightPost.Position = new Vector3(-0.5f, humanH * 0.5f, 0);
            humanGroup.AddChild(heightPost);

            _scaleReferences.AddChild(humanGroup);

            // 2) Grid cell marker — a 1-cell-sized wireframe box at origin
            var cellMarker = new Node3D();
            cellMarker.Position = new Vector3(cs * 0.5f, 0, cs * 0.5f);
            TronTheme.AddWireframeEdges(cellMarker, new Vector3(cs, 0.1f, cs));
            _scaleReferences.AddChild(cellMarker);

            // 3) Ruler — 10-unit ruler along X axis near origin
            float rulerLen = 10f;
            var ruler = new MeshInstance3D();
            var rulerBox = new BoxMesh();
            rulerBox.Size = new Vector3(rulerLen, 0.04f, 0.04f);
            ruler.Mesh = rulerBox;
            ruler.MaterialOverride = accentMat;
            ruler.Position = new Vector3(rulerLen * 0.5f, 0.02f, -1f);
            _scaleReferences.AddChild(ruler);

            // Ruler tick marks every 2 units (= 1 cell)
            for (int i = 0; i <= (int)(rulerLen / cs); i++)
            {
                var tick = new MeshInstance3D();
                var tickBox = new BoxMesh();
                tickBox.Size = new Vector3(0.04f, 0.3f, 0.04f);
                tick.Mesh = tickBox;
                tick.MaterialOverride = i % 5 == 0 ? yellowMat : whiteMat;
                tick.Position = new Vector3(i * cs, 0.15f, -1f);
                _scaleReferences.AddChild(tick);
            }

            // 4) Enemy-height reference cylinder (1.2 units = standard enemy)
            var enemyRef = new MeshInstance3D();
            var enemyCyl = new CylinderMesh();
            enemyCyl.TopRadius = 0.3f;
            enemyCyl.BottomRadius = 0.3f;
            enemyCyl.Height = Constants.ENEMY_HEIGHT_STANDARD;
            enemyRef.Mesh = enemyCyl;
            enemyRef.MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.9f, 0.25f, 0.15f, 0.5f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            };
            enemyRef.Position = new Vector3(4 * cs, Constants.ENEMY_HEIGHT_STANDARD * 0.5f, 2 * cs);
            _scaleReferences.AddChild(enemyRef);

            // 5) Node tower height reference (1.5 units)
            var towerRef = new MeshInstance3D();
            var towerCyl = new CylinderMesh();
            towerCyl.TopRadius = 0.25f;
            towerCyl.BottomRadius = 0.35f;
            towerCyl.Height = Constants.NODE_MODEL_HEIGHT;
            towerRef.Mesh = towerCyl;
            towerRef.MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0f, 0.7f, 0.9f, 0.5f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            };
            towerRef.Position = new Vector3(6 * cs, Constants.NODE_MODEL_HEIGHT * 0.5f, 2 * cs);
            _scaleReferences.AddChild(towerRef);
        }

        public void ToggleScaleReferences()
        {
            _scaleRefsVisible = !_scaleRefsVisible;
            if (_scaleReferences != null)
                _scaleReferences.Visible = _scaleRefsVisible;
        }

        public bool ScaleRefsVisible => _scaleRefsVisible;

        public void ReturnToMenu()
        {
            GameManager.Instance?.ReturnToMainMenu();
        }

        // ── Grid Rebuild ──

        public void RebuildGrid()
        {
            // Remove old grid
            if (_grid != null)
            {
                RemoveChild(_grid);
                _grid.QueueFree();
            }

            // Clear selection (old nodes are gone)
            _selection?.ClearAll();
            if (_tools != null) _tools.ClearTrackedObjects();

            _grid = new VineGrid();
            _grid.Width = _levelData.Width;
            _grid.Height = _levelData.Height;
            AddChild(_grid);

            // Apply level data (cells, heights, entry/exit, lights, FX)
            VineMapLayouts.BuildFromData(_grid, _levelData);

            // Apply material overrides to rebuilt assets + store metadata + track assets
            ApplyMaterialOverridesOnRebuild();

            _grid.RebuildTerrainMesh();
            _grid.BuildGridLines();
        }

        private void ApplyMaterialOverridesOnRebuild()
        {
            if (_levelData.Assets == null) return;

            // Walk grid children to find placed assets and apply material overrides
            // Assets are placed by BuildFromData in order matching _levelData.Assets
            int assetIndex = 0;
            foreach (Node child in _grid.GetChildren())
            {
                if (child is not Node3D n3d) continue;
                if (child is Light3D || child is GpuParticles3D) continue;

                // Match to asset placement by index
                if (assetIndex < _levelData.Assets.Count)
                {
                    var placement = _levelData.Assets[assetIndex];

                    // Verify position match
                    if (Mathf.Abs(n3d.Position.X - placement.PosX) < 0.1f &&
                        Mathf.Abs(n3d.Position.Z - placement.PosZ) < 0.1f)
                    {
                        n3d.SetMeta("asset_path", placement.Path);
                        n3d.SetMeta("placement_id", placement.Id);

                        if (placement.MaterialOverride != null)
                            LevelEditorMaterialPainter.Apply(n3d, placement.MaterialOverride);

                        _tools?.TrackPlacedAsset(n3d);
                        assetIndex++;
                    }
                }
            }
        }

        /// <summary>
        /// Rebuild just the terrain mesh (after cell/height changes).
        /// </summary>
        public void RefreshTerrain()
        {
            _grid?.RebuildTerrainMesh();
            _grid?.BuildGridLines();
            _minimap?.QueueRedraw();
        }

        // ── Data Sync ──

        /// <summary>
        /// Walk the grid state and update the LevelData to match.
        /// </summary>
        public void SyncGridToData()
        {
            if (_grid == null) return;

            _levelData.Cells.Clear();
            _levelData.Props.Clear();

            for (int x = 0; x < _grid.Width; x++)
            for (int y = 0; y < _grid.Height; y++)
            {
                var cell = _grid.GetCell(x, y);
                switch (cell)
                {
                    case VineCellType.Wall:
                        _levelData.Cells.Add(new CellPlacement { X = x, Y = y, Type = "Wall" });
                        break;
                    case VineCellType.Elevated:
                        _levelData.Cells.Add(new CellPlacement { X = x, Y = y, Type = "Elevated" });
                        break;
                    case VineCellType.Channel:
                        _levelData.Cells.Add(new CellPlacement { X = x, Y = y, Type = "Channel" });
                        break;
                    case VineCellType.DataStream:
                        _levelData.Cells.Add(new CellPlacement { X = x, Y = y, Type = "DataStream" });
                        break;
                    case VineCellType.Prop:
                        _levelData.Props.Add(new PropPlacement { X = x, Y = y, PropType = "container" });
                        break;
                }
            }

            // Sync entry regions from grid
            _levelData.EntryRegions.Clear();
            foreach (var region in _grid.EntryRegions)
            {
                if (region.Cells.Count == 0) continue;
                int minX = int.MaxValue, minY = int.MaxValue;
                int maxX = int.MinValue, maxY = int.MinValue;
                foreach (var c in region.Cells)
                {
                    if (c.X < minX) minX = c.X;
                    if (c.Y < minY) minY = c.Y;
                    if (c.X > maxX) maxX = c.X;
                    if (c.Y > maxY) maxY = c.Y;
                }
                _levelData.EntryRegions.Add(new EntryRegionData
                {
                    StartX = minX, StartY = minY, EndX = maxX, EndY = maxY
                });
            }

            // Exit
            _levelData.ExitPoint = new ExitPointData { X = _grid.ExitPoint.X, Y = _grid.ExitPoint.Y };
        }

        // ── Undo/Redo ──

        public void PushUndoState()
        {
            SyncGridToData();
            string json = LevelSerializer.Serialize(_levelData);
            _undoStack.Push(json);
        }

        public void Undo()
        {
            string json = _undoStack.Undo();
            if (json == null) return;
            _levelData = LevelSerializer.Deserialize(json);
            RebuildGrid();
            _ui?.RefreshAll();
        }

        public void Redo()
        {
            string json = _undoStack.Redo();
            if (json == null) return;
            _levelData = LevelSerializer.Deserialize(json);
            RebuildGrid();
            _ui?.RefreshAll();
        }

        // ── Theme ──

        public void SetPlanetTheme(string theme)
        {
            _levelData.PlanetTheme = theme;
            ApplyPlanetTheme();
            RebuildGrid();
        }

        private void ApplyPlanetTheme()
        {
            PlanetTheme.Current = _levelData.PlanetTheme switch
            {
                "scrapyard" => new ScrapyardPlanetTheme(),
                _ => new TronPlanetTheme()
            };
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event.IsActionPressed("ui_cancel"))
            {
                ReturnToMenu();
                GetViewport().SetInputAsHandled();
            }

            // Ctrl+Z / Ctrl+Y undo/redo
            if (@event is InputEventKey key && key.Pressed)
            {
                if (key.CtrlPressed && key.Keycode == Key.Z)
                {
                    if (key.ShiftPressed)
                        Redo();
                    else
                        Undo();
                    GetViewport().SetInputAsHandled();
                }
                else if (key.CtrlPressed && key.Keycode == Key.Y)
                {
                    Redo();
                    GetViewport().SetInputAsHandled();
                }
                else if (key.CtrlPressed && key.Keycode == Key.S)
                {
                    SaveLevel();
                    GetViewport().SetInputAsHandled();
                }
            }
        }
    }
}
