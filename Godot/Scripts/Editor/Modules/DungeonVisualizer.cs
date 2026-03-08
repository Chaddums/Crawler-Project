using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JunkbotArena.Editor
{
    /// <summary>
    /// Dungeon Visualizer — 2D grid map of generated dungeon layout + 3D room preview.
    /// Left: top-down grid showing room types, main path, doors, selected room info.
    /// Right: 3D viewport previewing the selected room's layout variant with collision overlay.
    /// Debug ID labels show every node's name, type, and position for bug reporting.
    /// </summary>
    public partial class DungeonVisualizer : EditorPanel
    {
        public override string PanelName => "Dungeon";
        public override Color AccentColor => EditorStyles.AccentRoom;

        // 2D Map
        private Control _mapPanel;
        private Label _mapInfo;
        private Label _roomInfo;
        private SpinBox _sectorPicker;
        private int _seed = 42;

        // 3D Preview
        private SubViewport _viewport;
        private SubViewportContainer _viewportContainer;
        private Node3D _roomPreviewRoot;
        private Node3D _overlayRoot;   // separate container for collision overlays
        private Node3D _labelRoot;     // separate container for debug ID labels
        private Camera3D _camera;
        private Label _previewLabel;
        private bool _showCollision = true;
        private bool _showDebugIds;
        private float _cameraAngle;

        // Generated data
        private Dictionary<Vector2I, RoomType> _grid;
        private List<Vector2I> _mainPath;
        private Vector2I? _selectedRoom;
        private SectorData _currentSector;

        // Node tree info for the side panel
        private VBoxContainer _nodeList;
        private ScrollContainer _nodeListScroll;

        // Layout cycling
        private int _layoutIndex;
        private static readonly string[] LayoutNames =
        {
            "Pillbox", "Trench", "Arena", "Maze", "Sniper", "Bunker",
            "Gauntlet", "Crossroads", "Pillars", "Scrapyard", "FiringRange",
            "CargoBay", "Reactor", "CircuitBoard", "Ambush", "Fortress",
            "Catwalk", "Workshop", "ServerRoom", "JunkPile"
        };

        // Map drawing constants
        private const float CELL_SIZE = 22f;
        private const float MAP_OFFSET_X = 10f;
        private const float MAP_OFFSET_Y = 10f;

        protected override void BuildUI(VBoxContainer content)
        {
            var topBar = new HBoxContainer();
            topBar.AddThemeConstantOverride("separation", 12);

            // Sector picker
            topBar.AddChild(EditorStyles.MakeLabel("Sector:", EditorStyles.FontSmall, EditorStyles.TextSecondary));
            _sectorPicker = new SpinBox();
            _sectorPicker.MinValue = 1;
            _sectorPicker.MaxValue = 8;
            _sectorPicker.Step = 1;
            _sectorPicker.Value = 1;
            _sectorPicker.CustomMinimumSize = new Vector2(60, 0);
            _sectorPicker.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
            topBar.AddChild(_sectorPicker);

            // Seed
            topBar.AddChild(EditorStyles.MakeLabel("Seed:", EditorStyles.FontSmall, EditorStyles.TextSecondary));
            var seedBox = new SpinBox();
            seedBox.MinValue = 0;
            seedBox.MaxValue = 99999;
            seedBox.Step = 1;
            seedBox.Value = _seed;
            seedBox.CustomMinimumSize = new Vector2(80, 0);
            seedBox.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
            seedBox.ValueChanged += v => _seed = (int)v;
            topBar.AddChild(seedBox);

            // Generate button
            var genBtn = EditorStyles.MakeButton("Generate", EditorStyles.FontSmall, AccentColor);
            genBtn.Pressed += GenerateDungeon;
            topBar.AddChild(genBtn);

            // Collision toggle — no longer rebuilds room, just toggles overlay visibility
            var collCheck = new CheckBox();
            collCheck.Text = "Collision";
            collCheck.ButtonPressed = true;
            collCheck.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
            collCheck.Toggled += v =>
            {
                _showCollision = v;
                if (_overlayRoot != null) _overlayRoot.Visible = v;
            };
            topBar.AddChild(collCheck);

            // Debug ID labels toggle
            var idCheck = new CheckBox();
            idCheck.Text = "Show IDs";
            idCheck.ButtonPressed = false;
            idCheck.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
            idCheck.Toggled += v =>
            {
                _showDebugIds = v;
                if (_labelRoot != null) _labelRoot.Visible = v;
            };
            topBar.AddChild(idCheck);

            content.AddChild(topBar);
            content.AddChild(EditorStyles.MakeSeparator());

            // Main split: map | viewport | node tree
            var split = new HBoxContainer();
            split.SizeFlagsVertical = SizeFlags.ExpandFill;
            split.AddThemeConstantOverride("separation", 8);

            // Left: 2D Map
            var leftPanel = new VBoxContainer();
            leftPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            leftPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            _mapInfo = EditorStyles.MakeLabel("Click Generate to create a dungeon", EditorStyles.FontSmall, EditorStyles.TextSecondary);
            leftPanel.AddChild(_mapInfo);

            _mapPanel = new Control();
            _mapPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            _mapPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _mapPanel.CustomMinimumSize = new Vector2(460, 460);
            _mapPanel.Draw += DrawMap;
            _mapPanel.GuiInput += OnMapClick;
            _mapPanel.MouseFilter = Control.MouseFilterEnum.Stop;
            leftPanel.AddChild(_mapPanel);

            _roomInfo = EditorStyles.MakeLabel("", EditorStyles.FontSmall, EditorStyles.TextMuted);
            leftPanel.AddChild(_roomInfo);

            split.AddChild(leftPanel);

            // Center: 3D room preview
            var centerPanel = new VBoxContainer();
            centerPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            centerPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            centerPanel.CustomMinimumSize = new Vector2(400, 0);

            // Layout cycling
            var layoutBar = new HBoxContainer();
            layoutBar.AddThemeConstantOverride("separation", 4);

            var prevLayout = EditorStyles.MakeButton("<", EditorStyles.FontBody);
            prevLayout.CustomMinimumSize = new Vector2(28, 28);
            prevLayout.Pressed += () => CycleLayout(-1);
            layoutBar.AddChild(prevLayout);

            _previewLabel = EditorStyles.MakeLabel("Room Preview", EditorStyles.FontBody, AccentColor);
            _previewLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _previewLabel.HorizontalAlignment = HorizontalAlignment.Center;
            layoutBar.AddChild(_previewLabel);

            var nextLayout = EditorStyles.MakeButton(">", EditorStyles.FontBody);
            nextLayout.CustomMinimumSize = new Vector2(28, 28);
            nextLayout.Pressed += () => CycleLayout(1);
            layoutBar.AddChild(nextLayout);

            centerPanel.AddChild(layoutBar);

            // 3D Viewport
            _viewportContainer = new SubViewportContainer();
            _viewportContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
            _viewportContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _viewportContainer.Stretch = true;

            _viewport = new SubViewport();
            _viewport.Size = new Vector2I(640, 480);
            _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
            _viewport.OwnWorld3D = true;

            _camera = new Camera3D();
            _camera.Position = new Vector3(0, 25, 20);
            _camera.LookAt(Vector3.Zero);
            _viewport.AddChild(_camera);

            _roomPreviewRoot = new Node3D();
            _roomPreviewRoot.Name = "RoomPreview";
            _viewport.AddChild(_roomPreviewRoot);

            _overlayRoot = new Node3D();
            _overlayRoot.Name = "CollisionOverlay";
            _viewport.AddChild(_overlayRoot);

            _labelRoot = new Node3D();
            _labelRoot.Name = "DebugLabels";
            _labelRoot.Visible = false;
            _viewport.AddChild(_labelRoot);

            // Lighting
            var light = new DirectionalLight3D();
            light.Position = new Vector3(10, 20, 10);
            light.LookAt(Vector3.Zero);
            light.LightEnergy = 1.0f;
            _viewport.AddChild(light);

            var fill = new DirectionalLight3D();
            fill.Position = new Vector3(-10, 15, -5);
            fill.LookAt(Vector3.Zero);
            fill.LightEnergy = 0.3f;
            _viewport.AddChild(fill);

            var env = new WorldEnvironment();
            var envRes = new Godot.Environment();
            envRes.BackgroundMode = Godot.Environment.BGMode.Color;
            envRes.BackgroundColor = new Color(0.03f, 0.03f, 0.05f);
            envRes.AmbientLightSource = Godot.Environment.AmbientSource.Color;
            envRes.AmbientLightColor = new Color(0.12f, 0.12f, 0.15f);
            env.Environment = envRes;
            _viewport.AddChild(env);

            _viewportContainer.AddChild(_viewport);
            centerPanel.AddChild(_viewportContainer);

            // Legend
            centerPanel.AddChild(EditorStyles.MakeSeparator());
            var legend = new HBoxContainer();
            legend.AddThemeConstantOverride("separation", 12);
            AddLegendItem(legend, "Combat", new Color(0.5f, 0.2f, 0.2f));
            AddLegendItem(legend, "Boss", new Color(0.8f, 0.1f, 0.1f));
            AddLegendItem(legend, "Treasure", new Color(0.9f, 0.7f, 0.1f));
            AddLegendItem(legend, "Shop", new Color(0.2f, 0.7f, 0.3f));
            AddLegendItem(legend, "Event", new Color(0.3f, 0.5f, 0.8f));
            AddLegendItem(legend, "Entrance", new Color(0.3f, 0.8f, 0.8f));
            centerPanel.AddChild(legend);

            split.AddChild(centerPanel);

            // Right: Node tree listing (debug info)
            var rightPanel = new VBoxContainer();
            rightPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            rightPanel.CustomMinimumSize = new Vector2(240, 0);

            rightPanel.AddChild(EditorStyles.MakeLabel("Scene Tree", EditorStyles.FontHeader, AccentColor));
            rightPanel.AddChild(EditorStyles.MakeSeparator());

            _nodeListScroll = new ScrollContainer();
            _nodeListScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            _nodeListScroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            _nodeList = new VBoxContainer();
            _nodeList.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _nodeList.AddThemeConstantOverride("separation", 1);
            _nodeListScroll.AddChild(_nodeList);
            rightPanel.AddChild(_nodeListScroll);

            split.AddChild(rightPanel);
            content.AddChild(split);
        }

        public override void _Process(double delta)
        {
            if (_roomPreviewRoot != null && Visible)
            {
                _cameraAngle += (float)delta * 0.3f;
                float radius = 30f;
                _camera.Position = new Vector3(
                    Mathf.Cos(_cameraAngle) * radius,
                    25f,
                    Mathf.Sin(_cameraAngle) * radius
                );
                _camera.LookAt(new Vector3(0, 2, 0));
            }
        }

        private void GenerateDungeon()
        {
            int sector = (int)_sectorPicker.Value;
            _currentSector = SectorDataRegistry.GetSector(sector);
            if (_currentSector == null)
            {
                _mapInfo.Text = "Failed to get sector data";
                return;
            }

            try
            {
                var gen = new DungeonGenerator(_currentSector);
                var tempParent = new Node3D();
                gen.Generate(tempParent);

                _grid = new Dictionary<Vector2I, RoomType>();
                foreach (var kvp in gen.RoomGrid)
                    _grid[kvp.Key] = kvp.Value;

                _mainPath = gen.MainPath.ToList();
                tempParent.QueueFree();

                int combat = _grid.Values.Count(t => t == RoomType.Combat || t == RoomType.Megabonk);
                int special = _grid.Count - combat;
                _mapInfo.Text = $"Sector {sector} | Seed {_seed} | {_grid.Count} rooms ({combat} combat, {special} special) | Path length: {_mainPath.Count}";
                _selectedRoom = null;
                _roomInfo.Text = "Click a room on the map to inspect";
                _mapPanel.QueueRedraw();
            }
            catch (Exception e)
            {
                _mapInfo.Text = $"Generation error: {e.Message}";
                GD.PrintErr($"[DungeonVisualizer] {e}");
            }
        }

        // ===== 2D MAP DRAWING =====

        private void DrawMap()
        {
            if (_grid == null || _grid.Count == 0) return;

            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;
            foreach (var pos in _grid.Keys)
            {
                minX = Math.Min(minX, pos.X);
                maxX = Math.Max(maxX, pos.X);
                minY = Math.Min(minY, pos.Y);
                maxY = Math.Max(maxY, pos.Y);
            }

            foreach (var kvp in _grid)
            {
                var gx = (kvp.Key.X - minX) * CELL_SIZE + MAP_OFFSET_X;
                var gy = (kvp.Key.Y - minY) * CELL_SIZE + MAP_OFFSET_Y;
                var rect = new Rect2(gx + 1, gy + 1, CELL_SIZE - 2, CELL_SIZE - 2);
                var color = GetRoomColor(kvp.Value);

                if (_selectedRoom.HasValue && _selectedRoom.Value == kvp.Key)
                    _mapPanel.DrawRect(new Rect2(gx - 1, gy - 1, CELL_SIZE + 2, CELL_SIZE + 2), Colors.White, false, 2f);

                _mapPanel.DrawRect(rect, color);

                if (_mainPath != null && _mainPath.Contains(kvp.Key))
                {
                    var center = new Vector2(gx + CELL_SIZE / 2, gy + CELL_SIZE / 2);
                    _mapPanel.DrawCircle(center, 3f, new Color(1, 1, 1, 0.5f));
                }

                var pos = kvp.Key;
                var dirs = new (Vector2I dir, Vector2 from, Vector2 to)[]
                {
                    (new Vector2I(1, 0), new Vector2(gx + CELL_SIZE, gy + CELL_SIZE * 0.3f), new Vector2(gx + CELL_SIZE, gy + CELL_SIZE * 0.7f)),
                    (new Vector2I(0, 1), new Vector2(gx + CELL_SIZE * 0.3f, gy + CELL_SIZE), new Vector2(gx + CELL_SIZE * 0.7f, gy + CELL_SIZE)),
                };
                foreach (var (dir, from, to) in dirs)
                {
                    if (_grid.ContainsKey(pos + dir))
                        _mapPanel.DrawLine(from, to, new Color(0.6f, 0.6f, 0.7f, 0.6f), 2f);
                }
            }

            // Room type letter labels
            foreach (var kvp in _grid)
            {
                var gx = (kvp.Key.X - minX) * CELL_SIZE + MAP_OFFSET_X;
                var gy = (kvp.Key.Y - minY) * CELL_SIZE + MAP_OFFSET_Y;
                var letter = GetRoomLetter(kvp.Value);
                var font = ThemeDB.FallbackFont;
                if (font != null)
                    _mapPanel.DrawString(font, new Vector2(gx + 5, gy + CELL_SIZE - 5), letter, HorizontalAlignment.Left, -1, 10, new Color(1, 1, 1, 0.8f));
            }
        }

        private void OnMapClick(InputEvent @event)
        {
            if (@event is not InputEventMouseButton mb || !mb.Pressed || mb.ButtonIndex != MouseButton.Left)
                return;
            if (_grid == null) return;

            int minX = int.MaxValue, minY = int.MaxValue;
            foreach (var pos in _grid.Keys)
            {
                minX = Math.Min(minX, pos.X);
                minY = Math.Min(minY, pos.Y);
            }

            var local = mb.Position;
            int gx = (int)((local.X - MAP_OFFSET_X) / CELL_SIZE) + minX;
            int gy = (int)((local.Y - MAP_OFFSET_Y) / CELL_SIZE) + minY;
            var clicked = new Vector2I(gx, gy);

            if (_grid.ContainsKey(clicked))
            {
                _selectedRoom = clicked;
                var type = _grid[clicked];
                bool onPath = _mainPath != null && _mainPath.Contains(clicked);

                var doorDirs = new List<string>();
                if (_grid.ContainsKey(clicked + new Vector2I(0, -1))) doorDirs.Add("N");
                if (_grid.ContainsKey(clicked + new Vector2I(0, 1))) doorDirs.Add("S");
                if (_grid.ContainsKey(clicked + new Vector2I(-1, 0))) doorDirs.Add("W");
                if (_grid.ContainsKey(clicked + new Vector2I(1, 0))) doorDirs.Add("E");

                _roomInfo.Text = $"Room ({gx},{gy}) | Type: {type} | Doors: {string.Join(",", doorDirs)} | {(onPath ? "MAIN PATH" : "Branch")}";
                _mapPanel.QueueRedraw();
                PreviewSelectedRoom();
            }
        }

        // ===== 3D ROOM PREVIEW =====

        private void ClearPreview()
        {
            foreach (var child in _roomPreviewRoot.GetChildren())
                if (child is Node n) n.QueueFree();
            foreach (var child in _overlayRoot.GetChildren())
                if (child is Node n) n.QueueFree();
            foreach (var child in _labelRoot.GetChildren())
                if (child is Node n) n.QueueFree();
        }

        private void PreviewSelectedRoom()
        {
            if (_roomPreviewRoot == null) return;
            ClearPreview();

            if (!_selectedRoom.HasValue || _currentSector == null) return;

            var pos = _selectedRoom.Value;
            var type = _grid[pos];

            bool doorN = _grid.ContainsKey(pos + new Vector2I(0, -1));
            bool doorS = _grid.ContainsKey(pos + new Vector2I(0, 1));
            bool doorE = _grid.ContainsKey(pos + new Vector2I(1, 0));
            bool doorW = _grid.ContainsKey(pos + new Vector2I(-1, 0));

            try
            {
                var room = RoomBuilder.BuildRoom(
                    Vector3.Zero,
                    RoomBuilder.GetRoomSize(type),
                    type,
                    doorN, doorS, doorE, doorW,
                    _currentSector,
                    RoomShape.Rectangle,
                    pos
                );

                if (room != null)
                {
                    _roomPreviewRoot.AddChild(room);
                    // Build overlays into separate root so toggling doesn't rebuild room
                    BuildCollisionOverlay(room);
                    BuildDebugLabels(room);
                    BuildNodeList(room);
                }

                _previewLabel.Text = $"{type} Room ({pos.X},{pos.Y})";
            }
            catch (Exception e)
            {
                _previewLabel.Text = $"Preview error: {e.Message}";
                GD.PrintErr($"[DungeonVisualizer] Room preview error: {e}");
            }
        }

        private void CycleLayout(int dir)
        {
            _layoutIndex = (_layoutIndex + dir + LayoutNames.Length) % LayoutNames.Length;
            _previewLabel.Text = $"Layout: {LayoutNames[_layoutIndex]}";

            if (_roomPreviewRoot == null) return;
            ClearPreview();

            var sector = _currentSector ?? SectorDataRegistry.GetSector(1);
            try
            {
                var room = RoomBuilder.BuildRoom(
                    Vector3.Zero,
                    new Vector2(32, 32),
                    RoomType.Combat,
                    true, true, true, true,
                    sector,
                    RoomShape.Rectangle,
                    new Vector2I(_layoutIndex * 7919, _layoutIndex * 6271)
                );

                if (room != null)
                {
                    _roomPreviewRoot.AddChild(room);
                    BuildCollisionOverlay(room);
                    BuildDebugLabels(room);
                    BuildNodeList(room);
                }

                _previewLabel.Text = $"Layout Preview: {LayoutNames[_layoutIndex]}";
            }
            catch (Exception e)
            {
                _previewLabel.Text = $"Layout error: {e.Message}";
            }
        }

        // ===== COLLISION OVERLAY (into separate root) =====

        private void BuildCollisionOverlay(Node room)
        {
            CollectCollisionShapes(room);
            _overlayRoot.Visible = _showCollision;
        }

        private void CollectCollisionShapes(Node node)
        {
            foreach (var child in node.GetChildren())
            {
                if (child is CollisionShape3D col && col.Shape != null)
                {
                    var overlay = CreateCollisionMesh(col);
                    if (overlay != null)
                        _overlayRoot.AddChild(overlay);
                }
                if (child is Node n)
                    CollectCollisionShapes(n);
            }
        }

        private MeshInstance3D CreateCollisionMesh(CollisionShape3D col)
        {
            Mesh mesh = null;

            if (col.Shape is BoxShape3D box)
                mesh = new BoxMesh { Size = box.Size };
            else if (col.Shape is CylinderShape3D cyl)
                mesh = new CylinderMesh { TopRadius = cyl.Radius, BottomRadius = cyl.Radius, Height = cyl.Height };
            else if (col.Shape is SphereShape3D sphere)
                mesh = new SphereMesh { Radius = sphere.Radius, Height = sphere.Radius * 2 };

            if (mesh == null) return null;

            var mi = new MeshInstance3D();
            mi.Mesh = mesh;
            mi.GlobalTransform = col.GlobalTransform;

            var mat = new StandardMaterial3D();
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.AlbedoColor = new Color(0f, 1f, 0.3f, 0.15f);
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
            mi.MaterialOverride = mat;

            return mi;
        }

        // ===== DEBUG ID LABELS (into separate root) =====

        private void BuildDebugLabels(Node room)
        {
            CollectDebugLabels(room, 0);
            _labelRoot.Visible = _showDebugIds;
        }

        private void CollectDebugLabels(Node node, int depth)
        {
            if (node is Node3D n3d && depth > 0)
            {
                string className = node.GetClass();
                string displayName = node.Name;
                bool isInteresting = node is StaticBody3D || node is Area3D ||
                    node is MeshInstance3D || node is Marker3D ||
                    (node is Node3D && node.GetChildCount() > 0 && depth <= 2);

                // Always label named things and physics objects
                if (isInteresting || !displayName.StartsWith("@"))
                {
                    var pos3d = n3d.GlobalPosition;
                    string shapeInfo = "";

                    // If this has a collision shape child, show its dimensions
                    foreach (var child in node.GetChildren())
                    {
                        if (child is CollisionShape3D col && col.Shape != null)
                        {
                            if (col.Shape is BoxShape3D box)
                                shapeInfo = $"\nBox({box.Size.X:F1},{box.Size.Y:F1},{box.Size.Z:F1})";
                            else if (col.Shape is CylinderShape3D cyl)
                                shapeInfo = $"\nCyl(r={cyl.Radius:F1},h={cyl.Height:F1})";
                            else if (col.Shape is SphereShape3D sph)
                                shapeInfo = $"\nSphere(r={sph.Radius:F1})";
                            break;
                        }
                    }

                    string labelText = $"{displayName}\n{className}{shapeInfo}\n({pos3d.X:F1},{pos3d.Y:F1},{pos3d.Z:F1})";

                    var label = new Label3D();
                    label.Text = labelText;
                    label.FontSize = 24;
                    label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
                    label.NoDepthTest = true;
                    label.PixelSize = 0.01f;
                    label.Modulate = GetLabelColor(node);
                    label.OutlineModulate = new Color(0, 0, 0, 0.8f);
                    label.OutlineSize = 6;
                    label.GlobalPosition = pos3d + Vector3.Up * 0.5f;
                    _labelRoot.AddChild(label);
                }
            }

            foreach (var child in node.GetChildren())
            {
                if (child is Node n)
                    CollectDebugLabels(n, depth + 1);
            }
        }

        private static Color GetLabelColor(Node node)
        {
            if (node is Area3D) return new Color(1f, 0.3f, 0.3f);       // Red for hazards
            if (node is StaticBody3D) return new Color(0.3f, 1f, 0.5f);  // Green for physics
            if (node is MeshInstance3D) return new Color(0.5f, 0.7f, 1f); // Blue for meshes
            if (node is Marker3D) return new Color(1f, 1f, 0.3f);        // Yellow for markers
            return new Color(0.8f, 0.8f, 0.8f);                          // White for other
        }

        // ===== NODE TREE LISTING (2D panel) =====

        private void BuildNodeList(Node room)
        {
            if (_nodeList == null) return;

            // Clear existing
            foreach (var child in _nodeList.GetChildren())
                if (child is Node n) n.QueueFree();

            CollectNodeListEntries(room, 0);
        }

        private void CollectNodeListEntries(Node node, int depth)
        {
            if (depth > 5) return; // limit depth

            string className = node.GetClass();
            string displayName = node.Name;
            bool isInteresting = node is StaticBody3D || node is Area3D ||
                node is MeshInstance3D || node is Marker3D || depth <= 1;

            if (isInteresting || !displayName.StartsWith("@"))
            {
                string indent = new string(' ', depth * 2);
                string posStr = "";
                if (node is Node3D n3d)
                    posStr = $" ({n3d.Position.X:F1},{n3d.Position.Y:F1},{n3d.Position.Z:F1})";

                Color labelColor = depth == 0 ? AccentColor :
                    (node is StaticBody3D ? new Color(0.3f, 1f, 0.5f) :
                     node is Area3D ? new Color(1f, 0.4f, 0.4f) :
                     node is MeshInstance3D ? new Color(0.5f, 0.7f, 1f) :
                     EditorStyles.TextMuted);

                var entry = EditorStyles.MakeLabel(
                    $"{indent}{displayName} [{className}]{posStr}",
                    EditorStyles.FontTiny,
                    labelColor
                );
                entry.AutowrapMode = TextServer.AutowrapMode.Off;
                _nodeList.AddChild(entry);
            }

            foreach (var child in node.GetChildren())
            {
                if (child is Node n)
                    CollectNodeListEntries(n, depth + 1);
            }
        }

        // ===== HELPERS =====

        private static Color GetRoomColor(RoomType type) => type switch
        {
            RoomType.Combat => new Color(0.5f, 0.2f, 0.2f),
            RoomType.Boss => new Color(0.8f, 0.1f, 0.1f),
            RoomType.Treasure => new Color(0.9f, 0.7f, 0.1f),
            RoomType.Shop => new Color(0.2f, 0.7f, 0.3f),
            RoomType.Event => new Color(0.3f, 0.5f, 0.8f),
            RoomType.Entrance => new Color(0.3f, 0.8f, 0.8f),
            RoomType.SafeRoom => new Color(0.4f, 0.8f, 0.4f),
            RoomType.Megabonk => new Color(1f, 0.3f, 0f),
            RoomType.Puzzle => new Color(0.6f, 0.4f, 0.8f),
            _ => new Color(0.3f, 0.3f, 0.3f)
        };

        private static string GetRoomLetter(RoomType type) => type switch
        {
            RoomType.Combat => "C",
            RoomType.Boss => "B",
            RoomType.Treasure => "T",
            RoomType.Shop => "$",
            RoomType.Event => "E",
            RoomType.Entrance => "S",
            RoomType.SafeRoom => "R",
            RoomType.Megabonk => "M",
            RoomType.Puzzle => "P",
            _ => "?"
        };

        private void AddLegendItem(HBoxContainer parent, string label, Color color)
        {
            var swatch = new ColorRect();
            swatch.Color = color;
            swatch.CustomMinimumSize = new Vector2(12, 12);
            parent.AddChild(swatch);
            parent.AddChild(EditorStyles.MakeLabel(label, EditorStyles.FontTiny, EditorStyles.TextMuted));
        }

        // ===== LIFECYCLE =====

        protected override void Reload()
        {
            if (_mapPanel == null) return;
            SetStatus("Ready — click Generate", EditorStyles.StatusSaved);
        }

        protected override void Save()
        {
            SetStatus("Dungeon layouts are procedural — tune via Sectors tab", EditorStyles.TextMuted);
        }

        protected override void RestoreSnapshot(string jsonSnapshot) { }
    }
}
