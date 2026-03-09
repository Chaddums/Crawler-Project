using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JunkbotArena.Editor
{
    /// <summary>
    /// Dungeon Visualizer — 2D grid map + 3D room preview with object editing.
    /// Left: top-down grid showing room types, main path, doors.
    /// Center: interactive 3D viewport — scroll to zoom, drag to orbit, click to select objects.
    /// Right: transform inspector for selected objects + scene tree.
    /// Saves per-object overrides to room_overrides.json.
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
        private Node3D _overlayRoot;
        private Node3D _labelRoot;
        private Node3D _selectionHighlightRoot;
        private Camera3D _camera;
        private Label _previewLabel;
        private bool _showCollision = true;
        private bool _showDebugIds;

        // Camera control (mouse-driven)
        private float _cameraYaw = 0.8f;
        private float _cameraPitch = 1.0f;   // radians from horizontal
        private float _cameraDistance = 30f;
        private Vector3 _cameraTarget = new(0, 2, 0);
        private bool _isDragging;
        private Vector2 _lastMousePos;
        private bool _autoOrbit = true;

        // Object selection
        private readonly List<Node3D> _selectedNodes = new();
        private readonly Dictionary<Node3D, Transform3D> _originalTransforms = new();

        // Transform inspector controls
        private VBoxContainer _inspectorPanel;
        private Label _selectionLabel;
        private SpinBox _posX, _posY, _posZ;
        private SpinBox _rotX, _rotY, _rotZ;
        private SpinBox _scaleX, _scaleY, _scaleZ;
        private bool _updatingInspector; // prevent feedback loops

        // Generated data
        private Dictionary<Vector2I, RoomType> _grid;
        private List<Vector2I> _mainPath;
        private Vector2I? _selectedRoom;
        private SectorData _currentSector;

        // Node tree
        private VBoxContainer _nodeList;
        private ScrollContainer _nodeListScroll;

        // Room overrides persistence
        private Dictionary<string, object> _roomOverrides;

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

        // Selection highlight material
        private static StandardMaterial3D _highlightMat;
        private static StandardMaterial3D HighlightMaterial
        {
            get
            {
                if (_highlightMat == null)
                {
                    _highlightMat = new StandardMaterial3D();
                    _highlightMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                    _highlightMat.AlbedoColor = new Color(1f, 0.8f, 0.2f, 0.25f);
                    _highlightMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                    _highlightMat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
                    _highlightMat.NoDepthTest = true;
                }
                return _highlightMat;
            }
        }

        protected override void BuildUI(VBoxContainer content)
        {
            var topBar = new HBoxContainer();
            topBar.AddThemeConstantOverride("separation", 8);

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

            // Generate
            var genBtn = EditorStyles.MakeButton("Generate", EditorStyles.FontSmall, AccentColor);
            genBtn.Pressed += GenerateDungeon;
            topBar.AddChild(genBtn);

            // Collision toggle
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

            // Debug ID labels
            var idCheck = new CheckBox();
            idCheck.Text = "IDs";
            idCheck.ButtonPressed = false;
            idCheck.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
            idCheck.Toggled += v =>
            {
                _showDebugIds = v;
                if (_labelRoot != null) _labelRoot.Visible = v;
            };
            topBar.AddChild(idCheck);

            // Auto-orbit toggle
            var orbitCheck = new CheckBox();
            orbitCheck.Text = "Auto-Orbit";
            orbitCheck.ButtonPressed = true;
            orbitCheck.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
            orbitCheck.Toggled += v => _autoOrbit = v;
            topBar.AddChild(orbitCheck);

            content.AddChild(topBar);
            content.AddChild(EditorStyles.MakeSeparator());

            // Main split: map | viewport | inspector+tree
            var split = new HBoxContainer();
            split.SizeFlagsVertical = SizeFlags.ExpandFill;
            split.AddThemeConstantOverride("separation", 6);

            // === LEFT: 2D Map ===
            var leftPanel = new VBoxContainer();
            leftPanel.SizeFlagsVertical = SizeFlags.ExpandFill;

            _mapInfo = EditorStyles.MakeLabel("Click Generate to create a dungeon", EditorStyles.FontSmall, EditorStyles.TextSecondary);
            leftPanel.AddChild(_mapInfo);

            _mapPanel = new Control();
            _mapPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            _mapPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _mapPanel.CustomMinimumSize = new Vector2(340, 340);
            _mapPanel.Draw += DrawMap;
            _mapPanel.GuiInput += OnMapClick;
            _mapPanel.MouseFilter = Control.MouseFilterEnum.Stop;
            leftPanel.AddChild(_mapPanel);

            _roomInfo = EditorStyles.MakeLabel("", EditorStyles.FontSmall, EditorStyles.TextMuted);
            leftPanel.AddChild(_roomInfo);

            split.AddChild(leftPanel);

            // === CENTER: 3D Viewport ===
            var centerPanel = new VBoxContainer();
            centerPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            centerPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            centerPanel.CustomMinimumSize = new Vector2(400, 0);

            // Layout cycling bar
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
            _viewportContainer.MouseFilter = Control.MouseFilterEnum.Stop;

            _viewport = new SubViewport();
            _viewport.Size = new Vector2I(640, 480);
            _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
            _viewport.OwnWorld3D = true;
            _viewport.PhysicsObjectPicking = true;

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

            _selectionHighlightRoot = new Node3D();
            _selectionHighlightRoot.Name = "SelectionHighlight";
            _viewport.AddChild(_selectionHighlightRoot);

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

            // Wire up mouse events on the viewport container
            _viewportContainer.GuiInput += OnViewportInput;

            centerPanel.AddChild(_viewportContainer);

            // Controls hint
            var hint = EditorStyles.MakeLabel("Scroll=Zoom  Drag=Orbit  Click=Select  Shift+Click=Multi", EditorStyles.FontTiny, EditorStyles.TextMuted);
            hint.HorizontalAlignment = HorizontalAlignment.Center;
            centerPanel.AddChild(hint);

            // Legend
            var legend = new HBoxContainer();
            legend.AddThemeConstantOverride("separation", 10);
            AddLegendItem(legend, "Combat", new Color(0.5f, 0.2f, 0.2f));
            AddLegendItem(legend, "Boss", new Color(0.8f, 0.1f, 0.1f));
            AddLegendItem(legend, "Treasure", new Color(0.9f, 0.7f, 0.1f));
            AddLegendItem(legend, "Shop", new Color(0.2f, 0.7f, 0.3f));
            AddLegendItem(legend, "Event", new Color(0.3f, 0.5f, 0.8f));
            centerPanel.AddChild(legend);

            split.AddChild(centerPanel);

            // === RIGHT: Inspector + Node Tree ===
            var rightPanel = new VBoxContainer();
            rightPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            rightPanel.CustomMinimumSize = new Vector2(260, 0);

            // --- Transform Inspector ---
            _inspectorPanel = new VBoxContainer();
            _inspectorPanel.AddThemeConstantOverride("separation", 3);

            _selectionLabel = EditorStyles.MakeLabel("No selection", EditorStyles.FontBody, AccentColor);
            _inspectorPanel.AddChild(_selectionLabel);
            _inspectorPanel.AddChild(EditorStyles.MakeSeparator());

            // Position
            _inspectorPanel.AddChild(EditorStyles.MakeLabel("Position:", EditorStyles.FontSmall, EditorStyles.TextSecondary));
            var posRow = new HBoxContainer();
            posRow.AddThemeConstantOverride("separation", 2);
            posRow.AddChild(MakeAxisLabel("X"));
            _posX = MakeTransformSpinBox(-200, 200, 0.5);
            _posX.ValueChanged += _ => ApplyInspectorTransform();
            posRow.AddChild(_posX);
            posRow.AddChild(MakeAxisLabel("Y"));
            _posY = MakeTransformSpinBox(-50, 50, 0.1);
            _posY.ValueChanged += _ => ApplyInspectorTransform();
            posRow.AddChild(_posY);
            posRow.AddChild(MakeAxisLabel("Z"));
            _posZ = MakeTransformSpinBox(-200, 200, 0.5);
            _posZ.ValueChanged += _ => ApplyInspectorTransform();
            posRow.AddChild(_posZ);
            _inspectorPanel.AddChild(posRow);

            // Rotation
            _inspectorPanel.AddChild(EditorStyles.MakeLabel("Rotation:", EditorStyles.FontSmall, EditorStyles.TextSecondary));
            var rotRow = new HBoxContainer();
            rotRow.AddThemeConstantOverride("separation", 2);
            rotRow.AddChild(MakeAxisLabel("X"));
            _rotX = MakeTransformSpinBox(-180, 180, 15);
            _rotX.ValueChanged += _ => ApplyInspectorTransform();
            rotRow.AddChild(_rotX);
            rotRow.AddChild(MakeAxisLabel("Y"));
            _rotY = MakeTransformSpinBox(-180, 180, 15);
            _rotY.ValueChanged += _ => ApplyInspectorTransform();
            rotRow.AddChild(_rotY);
            rotRow.AddChild(MakeAxisLabel("Z"));
            _rotZ = MakeTransformSpinBox(-180, 180, 15);
            _rotZ.ValueChanged += _ => ApplyInspectorTransform();
            rotRow.AddChild(_rotZ);
            _inspectorPanel.AddChild(rotRow);

            // Scale
            _inspectorPanel.AddChild(EditorStyles.MakeLabel("Scale:", EditorStyles.FontSmall, EditorStyles.TextSecondary));
            var scaleRow = new HBoxContainer();
            scaleRow.AddThemeConstantOverride("separation", 2);
            scaleRow.AddChild(MakeAxisLabel("X"));
            _scaleX = MakeTransformSpinBox(0.01, 20, 0.1);
            _scaleX.Value = 1.0;
            _scaleX.ValueChanged += _ => ApplyInspectorTransform();
            scaleRow.AddChild(_scaleX);
            scaleRow.AddChild(MakeAxisLabel("Y"));
            _scaleY = MakeTransformSpinBox(0.01, 20, 0.1);
            _scaleY.Value = 1.0;
            _scaleY.ValueChanged += _ => ApplyInspectorTransform();
            scaleRow.AddChild(_scaleY);
            scaleRow.AddChild(MakeAxisLabel("Z"));
            _scaleZ = MakeTransformSpinBox(0.01, 20, 0.1);
            _scaleZ.Value = 1.0;
            _scaleZ.ValueChanged += _ => ApplyInspectorTransform();
            scaleRow.AddChild(_scaleZ);
            _inspectorPanel.AddChild(scaleRow);

            _inspectorPanel.AddChild(EditorStyles.MakeSeparator());

            // Quick action buttons
            _inspectorPanel.AddChild(EditorStyles.MakeLabel("Quick Actions:", EditorStyles.FontSmall, EditorStyles.TextSecondary));

            var actionRow1 = new HBoxContainer();
            actionRow1.AddThemeConstantOverride("separation", 3);

            var rot90Btn = EditorStyles.MakeButton("Rot 90", EditorStyles.FontTiny, AccentColor);
            rot90Btn.CustomMinimumSize = new Vector2(0, 24);
            rot90Btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            rot90Btn.Pressed += () => QuickRotate(90);
            actionRow1.AddChild(rot90Btn);

            var rot180Btn = EditorStyles.MakeButton("Rot 180", EditorStyles.FontTiny, AccentColor);
            rot180Btn.CustomMinimumSize = new Vector2(0, 24);
            rot180Btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            rot180Btn.Pressed += () => QuickRotate(180);
            actionRow1.AddChild(rot180Btn);

            var rot270Btn = EditorStyles.MakeButton("Rot 270", EditorStyles.FontTiny, AccentColor);
            rot270Btn.CustomMinimumSize = new Vector2(0, 24);
            rot270Btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            rot270Btn.Pressed += () => QuickRotate(270);
            actionRow1.AddChild(rot270Btn);

            _inspectorPanel.AddChild(actionRow1);

            var actionRow2 = new HBoxContainer();
            actionRow2.AddThemeConstantOverride("separation", 3);

            var flipXBtn = EditorStyles.MakeButton("Flip X", EditorStyles.FontTiny);
            flipXBtn.CustomMinimumSize = new Vector2(0, 24);
            flipXBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            flipXBtn.Pressed += () => QuickFlip('X');
            actionRow2.AddChild(flipXBtn);

            var flipZBtn = EditorStyles.MakeButton("Flip Z", EditorStyles.FontTiny);
            flipZBtn.CustomMinimumSize = new Vector2(0, 24);
            flipZBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            flipZBtn.Pressed += () => QuickFlip('Z');
            actionRow2.AddChild(flipZBtn);

            var scaleUpBtn = EditorStyles.MakeButton("Scale+", EditorStyles.FontTiny);
            scaleUpBtn.CustomMinimumSize = new Vector2(0, 24);
            scaleUpBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            scaleUpBtn.Pressed += () => QuickScale(1.25f);
            actionRow2.AddChild(scaleUpBtn);

            var scaleDnBtn = EditorStyles.MakeButton("Scale-", EditorStyles.FontTiny);
            scaleDnBtn.CustomMinimumSize = new Vector2(0, 24);
            scaleDnBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            scaleDnBtn.Pressed += () => QuickScale(0.8f);
            actionRow2.AddChild(scaleDnBtn);

            _inspectorPanel.AddChild(actionRow2);

            var actionRow3 = new HBoxContainer();
            actionRow3.AddThemeConstantOverride("separation", 3);

            var resetBtn = EditorStyles.MakeButton("Reset Transform", EditorStyles.FontTiny, EditorStyles.StatusError);
            resetBtn.CustomMinimumSize = new Vector2(0, 24);
            resetBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            resetBtn.Pressed += ResetSelectedTransforms;
            actionRow3.AddChild(resetBtn);

            var deleteBtn = EditorStyles.MakeButton("Hide Object", EditorStyles.FontTiny, EditorStyles.StatusError);
            deleteBtn.CustomMinimumSize = new Vector2(0, 24);
            deleteBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            deleteBtn.Pressed += HideSelected;
            actionRow3.AddChild(deleteBtn);

            _inspectorPanel.AddChild(actionRow3);

            rightPanel.AddChild(_inspectorPanel);
            rightPanel.AddChild(EditorStyles.MakeSeparator());

            // --- Scene Tree ---
            rightPanel.AddChild(EditorStyles.MakeLabel("Scene Tree", EditorStyles.FontSmall, AccentColor));

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

        // ===== CAMERA & INPUT =====

        public override void _Process(double delta)
        {
            if (_camera == null || !Visible) return;

            if (_autoOrbit && !_isDragging)
                _cameraYaw += (float)delta * 0.3f;

            UpdateCamera();
        }

        private void UpdateCamera()
        {
            float pitch = Mathf.Clamp(_cameraPitch, 0.1f, 1.5f);
            float x = Mathf.Cos(_cameraYaw) * Mathf.Cos(pitch) * _cameraDistance;
            float y = Mathf.Sin(pitch) * _cameraDistance;
            float z = Mathf.Sin(_cameraYaw) * Mathf.Cos(pitch) * _cameraDistance;

            _camera.Position = _cameraTarget + new Vector3(x, y, z);
            _camera.LookAt(_cameraTarget);
        }

        private void OnViewportInput(InputEvent @event)
        {
            // Scroll to zoom
            if (@event is InputEventMouseButton mb)
            {
                if (mb.ButtonIndex == MouseButton.WheelUp)
                {
                    _cameraDistance = Mathf.Max(5f, _cameraDistance - 3f);
                    _viewportContainer.AcceptEvent();
                }
                else if (mb.ButtonIndex == MouseButton.WheelDown)
                {
                    _cameraDistance = Mathf.Min(80f, _cameraDistance + 3f);
                    _viewportContainer.AcceptEvent();
                }
                // Middle mouse drag to orbit
                else if (mb.ButtonIndex == MouseButton.Middle)
                {
                    _isDragging = mb.Pressed;
                    _lastMousePos = mb.Position;
                    _viewportContainer.AcceptEvent();
                }
                // Right mouse drag to orbit too
                else if (mb.ButtonIndex == MouseButton.Right)
                {
                    _isDragging = mb.Pressed;
                    _lastMousePos = mb.Position;
                    _viewportContainer.AcceptEvent();
                }
                // Left click to select
                else if (mb.ButtonIndex == MouseButton.Left && mb.Pressed)
                {
                    bool multiSelect = mb.ShiftPressed;
                    SelectObjectAt(mb.Position, multiSelect);
                    _viewportContainer.AcceptEvent();
                }
            }

            // Mouse motion for orbit dragging
            if (@event is InputEventMouseMotion mm && _isDragging)
            {
                var delta = mm.Position - _lastMousePos;
                _cameraYaw -= delta.X * 0.005f;
                _cameraPitch += delta.Y * 0.005f;
                _cameraPitch = Mathf.Clamp(_cameraPitch, 0.1f, 1.5f);
                _lastMousePos = mm.Position;
                _viewportContainer.AcceptEvent();
            }
        }

        // ===== OBJECT SELECTION =====

        private void SelectObjectAt(Vector2 screenPos, bool multiSelect)
        {
            if (_roomPreviewRoot == null || _roomPreviewRoot.GetChildCount() == 0) return;

            // Raycast from camera through the click point
            var from = _camera.ProjectRayOrigin(screenPos);
            var dir = _camera.ProjectRayNormal(screenPos);

            // Collect ALL hits, then pick the smallest object (not the closest).
            // This prevents the floor/walls from always winning over props/obstacles.
            var hits = new List<(Node3D node, float dist, float volume)>();

            var room = _roomPreviewRoot.GetChildCount() > 0 ? _roomPreviewRoot.GetChild(0) : null;
            if (room is Node3D roomNode)
                CollectAllHits(roomNode, from, dir, hits, 0);

            Node3D bestHit = null;
            if (hits.Count > 0)
            {
                // Sort by volume (smallest first) so we pick the most specific object.
                // Among equal-volume objects, prefer the closest.
                hits.Sort((a, b) =>
                {
                    int volCmp = a.volume.CompareTo(b.volume);
                    return volCmp != 0 ? volCmp : a.dist.CompareTo(b.dist);
                });
                bestHit = hits[0].node;
            }

            if (bestHit != null)
            {
                if (!multiSelect)
                    _selectedNodes.Clear();

                if (_selectedNodes.Contains(bestHit))
                    _selectedNodes.Remove(bestHit); // toggle off
                else
                    _selectedNodes.Add(bestHit);
            }
            else if (!multiSelect)
            {
                _selectedNodes.Clear();
            }

            UpdateSelectionHighlight();
            UpdateInspector();
        }

        private void CollectAllHits(Node node, Vector3 rayOrigin, Vector3 rayDir, List<(Node3D node, float dist, float volume)> hits, int depth)
        {
            if (depth > 5) return;

            if (node is Node3D n3d && IsSelectableNode(n3d))
            {
                var aabb = ComputeNodeAabb(n3d);
                float volume = aabb.Size.X * aabb.Size.Y * aabb.Size.Z;
                if (volume > 0.001f && RayIntersectsAabb(rayOrigin, rayDir, aabb, out float dist))
                {
                    hits.Add((n3d, dist, volume));
                }
            }

            foreach (var child in node.GetChildren())
            {
                if (child is Node n)
                    CollectAllHits(n, rayOrigin, rayDir, hits, depth + 1);
            }
        }

        private static bool IsSelectableNode(Node3D node)
        {
            string name = node.Name.ToString();
            // Skip internal/auto-generated nodes and structural room elements
            if (name.StartsWith("@") || name == "RoomPreview") return false;

            // Skip the room root itself and the floor — these are structural, not editable objects
            if (name.StartsWith("Room_")) return false;
            if (name == "Floor") return false;

            // Skip NavigationRegion3D and SpawnPoints — infrastructure nodes
            if (node is NavigationRegion3D) return false;

            // Select named physics objects, props, meshes, markers
            return node is StaticBody3D || node is Area3D || node is MeshInstance3D || node is Marker3D;
        }

        private static Aabb ComputeNodeAabb(Node3D node)
        {
            // Try collision shape in direct children first
            foreach (var child in node.GetChildren())
            {
                if (child is CollisionShape3D col && col.Shape != null)
                {
                    var shapeAabb = GetShapeAabb(col.Shape);
                    var globalPos = col.GlobalPosition;
                    return new Aabb(globalPos + shapeAabb.Position, shapeAabb.Size);
                }
            }

            // Try this node as a mesh
            if (node is MeshInstance3D mi && mi.Mesh != null)
            {
                var meshAabb = mi.Mesh.GetAabb();
                var globalPos = mi.GlobalPosition;
                return new Aabb(globalPos + meshAabb.Position, meshAabb.Size);
            }

            // Search children recursively for any mesh (e.g. StaticBody3D with MeshInstance3D child)
            var merged = new Aabb();
            bool found = false;
            CollectChildMeshAabbs(node, ref merged, ref found);
            if (found) return merged;

            // Fallback: small sphere around position
            var pos = node.GlobalPosition;
            return new Aabb(pos - new Vector3(0.5f, 0.5f, 0.5f), new Vector3(1, 1, 1));
        }

        private static void CollectChildMeshAabbs(Node node, ref Aabb merged, ref bool found)
        {
            foreach (var child in node.GetChildren())
            {
                if (child is MeshInstance3D childMi && childMi.Mesh != null)
                {
                    var meshAabb = childMi.Mesh.GetAabb();
                    var worldAabb = new Aabb(childMi.GlobalPosition + meshAabb.Position, meshAabb.Size);
                    if (!found) { merged = worldAabb; found = true; }
                    else merged = merged.Merge(worldAabb);
                }
                if (child is Node n)
                    CollectChildMeshAabbs(n, ref merged, ref found);
            }
        }

        private static Aabb GetShapeAabb(Shape3D shape)
        {
            if (shape is BoxShape3D box)
                return new Aabb(-box.Size / 2, box.Size);
            if (shape is CylinderShape3D cyl)
                return new Aabb(new Vector3(-cyl.Radius, -cyl.Height / 2, -cyl.Radius),
                    new Vector3(cyl.Radius * 2, cyl.Height, cyl.Radius * 2));
            if (shape is SphereShape3D sph)
                return new Aabb(new Vector3(-sph.Radius, -sph.Radius, -sph.Radius),
                    new Vector3(sph.Radius * 2, sph.Radius * 2, sph.Radius * 2));
            return new Aabb(Vector3.Zero, Vector3.One);
        }

        private static bool RayIntersectsAabb(Vector3 origin, Vector3 dir, Aabb aabb, out float dist)
        {
            dist = float.MaxValue;
            var min = aabb.Position;
            var max = aabb.Position + aabb.Size;

            float tmin = float.MinValue;
            float tmax = float.MaxValue;

            for (int i = 0; i < 3; i++)
            {
                float o = i == 0 ? origin.X : i == 1 ? origin.Y : origin.Z;
                float d = i == 0 ? dir.X : i == 1 ? dir.Y : dir.Z;
                float mn = i == 0 ? min.X : i == 1 ? min.Y : min.Z;
                float mx = i == 0 ? max.X : i == 1 ? max.Y : max.Z;

                if (Mathf.Abs(d) < 1e-6f)
                {
                    if (o < mn || o > mx) return false;
                }
                else
                {
                    float t1 = (mn - o) / d;
                    float t2 = (mx - o) / d;
                    if (t1 > t2) (t1, t2) = (t2, t1);
                    tmin = Mathf.Max(tmin, t1);
                    tmax = Mathf.Min(tmax, t2);
                    if (tmin > tmax) return false;
                }
            }

            if (tmax < 0) return false;
            dist = tmin > 0 ? tmin : tmax;
            return true;
        }

        // ===== SELECTION HIGHLIGHT =====

        private void UpdateSelectionHighlight()
        {
            // Clear existing highlights
            foreach (var child in _selectionHighlightRoot.GetChildren())
                if (child is Node n) n.QueueFree();

            foreach (var node in _selectedNodes)
            {
                if (!GodotObject.IsInstanceValid(node)) continue;

                var aabb = ComputeNodeAabb(node);
                // Expand slightly so it's visible outside the object
                var size = aabb.Size + new Vector3(0.2f, 0.2f, 0.2f);

                var mi = new MeshInstance3D();
                mi.Mesh = new BoxMesh { Size = size };
                mi.GlobalPosition = aabb.Position + aabb.Size / 2;
                mi.MaterialOverride = HighlightMaterial;
                _selectionHighlightRoot.AddChild(mi);
            }
        }

        // ===== TRANSFORM INSPECTOR =====

        private void UpdateInspector()
        {
            _updatingInspector = true;

            if (_selectedNodes.Count == 0)
            {
                _selectionLabel.Text = "No selection";
                _selectionLabel.AddThemeColorOverride("font_color", EditorStyles.TextMuted);
                _posX.Value = 0; _posY.Value = 0; _posZ.Value = 0;
                _rotX.Value = 0; _rotY.Value = 0; _rotZ.Value = 0;
                _scaleX.Value = 1; _scaleY.Value = 1; _scaleZ.Value = 1;
            }
            else if (_selectedNodes.Count == 1)
            {
                var node = _selectedNodes[0];
                _selectionLabel.Text = node.Name;
                _selectionLabel.AddThemeColorOverride("font_color", AccentColor);

                var pos = node.Position;
                _posX.Value = pos.X;
                _posY.Value = pos.Y;
                _posZ.Value = pos.Z;

                var rot = node.RotationDegrees;
                _rotX.Value = rot.X;
                _rotY.Value = rot.Y;
                _rotZ.Value = rot.Z;

                var scale = node.Scale;
                _scaleX.Value = scale.X;
                _scaleY.Value = scale.Y;
                _scaleZ.Value = scale.Z;

                // Store original if not already stored
                if (!_originalTransforms.ContainsKey(node))
                    _originalTransforms[node] = node.Transform;
            }
            else
            {
                _selectionLabel.Text = $"{_selectedNodes.Count} objects selected";
                _selectionLabel.AddThemeColorOverride("font_color", AccentColor);
                // Show first selected values as reference
                var node = _selectedNodes[0];
                _posX.Value = node.Position.X;
                _posY.Value = node.Position.Y;
                _posZ.Value = node.Position.Z;
                _rotX.Value = node.RotationDegrees.X;
                _rotY.Value = node.RotationDegrees.Y;
                _rotZ.Value = node.RotationDegrees.Z;
                _scaleX.Value = node.Scale.X;
                _scaleY.Value = node.Scale.Y;
                _scaleZ.Value = node.Scale.Z;
            }

            _updatingInspector = false;
        }

        private void ApplyInspectorTransform()
        {
            if (_updatingInspector || _selectedNodes.Count == 0) return;

            if (_selectedNodes.Count == 1)
            {
                var node = _selectedNodes[0];
                if (!GodotObject.IsInstanceValid(node)) return;

                node.Position = new Vector3((float)_posX.Value, (float)_posY.Value, (float)_posZ.Value);
                node.RotationDegrees = new Vector3((float)_rotX.Value, (float)_rotY.Value, (float)_rotZ.Value);
                node.Scale = new Vector3((float)_scaleX.Value, (float)_scaleY.Value, (float)_scaleZ.Value);
            }
            else
            {
                // Multi-select: apply rotation/scale change to all, position is relative offset
                foreach (var node in _selectedNodes)
                {
                    if (!GodotObject.IsInstanceValid(node)) continue;
                    node.RotationDegrees = new Vector3((float)_rotX.Value, (float)_rotY.Value, (float)_rotZ.Value);
                    node.Scale = new Vector3((float)_scaleX.Value, (float)_scaleY.Value, (float)_scaleZ.Value);
                }
            }

            UpdateSelectionHighlight();
            SaveNodeOverrides();
            MarkDirty();
        }

        // ===== QUICK ACTIONS =====

        private void QuickRotate(float degrees)
        {
            if (_selectedNodes.Count == 0) return;

            foreach (var node in _selectedNodes)
            {
                if (!GodotObject.IsInstanceValid(node)) continue;
                if (!_originalTransforms.ContainsKey(node))
                    _originalTransforms[node] = node.Transform;

                var rot = node.RotationDegrees;
                rot.Y = (rot.Y + degrees) % 360;
                node.RotationDegrees = rot;
            }

            UpdateInspector();
            UpdateSelectionHighlight();
            SaveNodeOverrides();
            MarkDirty();
        }

        private void QuickFlip(char axis)
        {
            if (_selectedNodes.Count == 0) return;

            foreach (var node in _selectedNodes)
            {
                if (!GodotObject.IsInstanceValid(node)) continue;
                if (!_originalTransforms.ContainsKey(node))
                    _originalTransforms[node] = node.Transform;

                var scale = node.Scale;
                if (axis == 'X') scale.X = -scale.X;
                else if (axis == 'Z') scale.Z = -scale.Z;
                node.Scale = scale;
            }

            UpdateInspector();
            UpdateSelectionHighlight();
            SaveNodeOverrides();
            MarkDirty();
        }

        private void QuickScale(float factor)
        {
            if (_selectedNodes.Count == 0) return;

            foreach (var node in _selectedNodes)
            {
                if (!GodotObject.IsInstanceValid(node)) continue;
                if (!_originalTransforms.ContainsKey(node))
                    _originalTransforms[node] = node.Transform;

                node.Scale *= factor;
            }

            UpdateInspector();
            UpdateSelectionHighlight();
            SaveNodeOverrides();
            MarkDirty();
        }

        private void ResetSelectedTransforms()
        {
            if (_selectedNodes.Count == 0) return;

            foreach (var node in _selectedNodes)
            {
                if (!GodotObject.IsInstanceValid(node)) continue;
                if (_originalTransforms.TryGetValue(node, out var original))
                    node.Transform = original;
            }

            UpdateInspector();
            UpdateSelectionHighlight();
            SaveNodeOverrides();
            MarkDirty();
        }

        private void HideSelected()
        {
            if (_selectedNodes.Count == 0) return;

            foreach (var node in _selectedNodes)
            {
                if (!GodotObject.IsInstanceValid(node)) continue;
                // Hide the node and all children (ensures meshes inside StaticBody3D disappear)
                SetVisibleRecursive(node, false);
            }

            var names = string.Join(", ", _selectedNodes.Select(n => n.Name.ToString()));
            _selectedNodes.Clear();
            UpdateSelectionHighlight();
            UpdateInspector();
            SaveNodeOverrides();
            MarkDirty();
            SetStatus($"Hidden: {names}", EditorStyles.TextMuted);
        }

        private static void SetVisibleRecursive(Node3D node, bool visible)
        {
            node.Visible = visible;
            foreach (var child in node.GetChildren())
            {
                if (child is Node3D child3d)
                    SetVisibleRecursive(child3d, visible);
            }
        }

        // ===== NODE OVERRIDE PERSISTENCE =====

        private string GetCurrentRoomKey()
        {
            if (!_selectedRoom.HasValue) return null;
            int sector = (int)_sectorPicker.Value;
            var pos = _selectedRoom.Value;
            var type = _grid?.ContainsKey(pos) == true ? _grid[pos] : RoomType.Combat;
            return $"s{sector}_{pos.X}_{pos.Y}_{type}";
        }

        private void SaveNodeOverrides()
        {
            var roomKey = GetCurrentRoomKey();
            if (roomKey == null) return;

            if (_roomOverrides == null)
                _roomOverrides = new Dictionary<string, object>();

            var nodeEdits = new Dictionary<string, object>();
            var room = _roomPreviewRoot.GetChildCount() > 0 ? _roomPreviewRoot.GetChild(0) : null;
            if (room is Node3D roomNode)
                CollectOverrides(roomNode, nodeEdits);

            if (nodeEdits.Count > 0)
                _roomOverrides[roomKey] = nodeEdits;
            else
                _roomOverrides.Remove(roomKey);
        }

        private void CollectOverrides(Node node, Dictionary<string, object> edits)
        {
            if (node is Node3D n3d && IsSelectableNode(n3d))
            {
                string name = n3d.Name.ToString();
                if (_originalTransforms.TryGetValue(n3d, out var original))
                {
                    // Only save if transform actually changed
                    if (!TransformApproxEqual(n3d.Transform, original) || !n3d.Visible)
                    {
                        var edit = new Dictionary<string, object>
                        {
                            ["posX"] = (double)n3d.Position.X,
                            ["posY"] = (double)n3d.Position.Y,
                            ["posZ"] = (double)n3d.Position.Z,
                            ["rotX"] = (double)n3d.RotationDegrees.X,
                            ["rotY"] = (double)n3d.RotationDegrees.Y,
                            ["rotZ"] = (double)n3d.RotationDegrees.Z,
                            ["scaleX"] = (double)n3d.Scale.X,
                            ["scaleY"] = (double)n3d.Scale.Y,
                            ["scaleZ"] = (double)n3d.Scale.Z,
                            ["visible"] = n3d.Visible
                        };
                        edits[name] = edit;
                    }
                }
            }

            foreach (var child in node.GetChildren())
            {
                if (child is Node n)
                    CollectOverrides(n, edits);
            }
        }

        private static bool TransformApproxEqual(Transform3D a, Transform3D b)
        {
            return a.Origin.DistanceTo(b.Origin) < 0.01f
                && a.Basis.X.DistanceTo(b.Basis.X) < 0.01f
                && a.Basis.Y.DistanceTo(b.Basis.Y) < 0.01f
                && a.Basis.Z.DistanceTo(b.Basis.Z) < 0.01f;
        }

        private void ApplyOverridesToRoom(Node room)
        {
            var roomKey = GetCurrentRoomKey();
            if (roomKey == null || _roomOverrides == null) return;
            if (!_roomOverrides.TryGetValue(roomKey, out var editsObj)) return;
            if (editsObj is not Dictionary<string, object> edits) return;

            ApplyOverridesRecursive(room, edits);
        }

        private void ApplyOverridesRecursive(Node node, Dictionary<string, object> edits)
        {
            if (node is Node3D n3d && IsSelectableNode(n3d))
            {
                string name = n3d.Name.ToString();
                if (edits.TryGetValue(name, out var editObj) && editObj is Dictionary<string, object> edit)
                {
                    // Store original before applying override
                    _originalTransforms[n3d] = n3d.Transform;

                    if (edit.TryGetValue("posX", out var px) && edit.TryGetValue("posY", out var py) && edit.TryGetValue("posZ", out var pz))
                        n3d.Position = new Vector3(Convert.ToSingle(px), Convert.ToSingle(py), Convert.ToSingle(pz));
                    if (edit.TryGetValue("rotX", out var rx) && edit.TryGetValue("rotY", out var ry) && edit.TryGetValue("rotZ", out var rz))
                        n3d.RotationDegrees = new Vector3(Convert.ToSingle(rx), Convert.ToSingle(ry), Convert.ToSingle(rz));
                    if (edit.TryGetValue("scaleX", out var sx) && edit.TryGetValue("scaleY", out var sy) && edit.TryGetValue("scaleZ", out var sz))
                        n3d.Scale = new Vector3(Convert.ToSingle(sx), Convert.ToSingle(sy), Convert.ToSingle(sz));
                    if (edit.TryGetValue("visible", out var vis))
                        n3d.Visible = Convert.ToBoolean(vis);
                }
            }

            foreach (var child in node.GetChildren())
            {
                if (child is Node n)
                    ApplyOverridesRecursive(n, edits);
            }
        }

        // ===== DUNGEON GENERATION =====

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
                _mapInfo.Text = $"Sector {sector} | Seed {_seed} | {_grid.Count} rooms ({combat} combat, {special} special)";
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

                var pos2 = kvp.Key;
                var dirs = new (Vector2I dir, Vector2 from, Vector2 to)[]
                {
                    (new Vector2I(1, 0), new Vector2(gx + CELL_SIZE, gy + CELL_SIZE * 0.3f), new Vector2(gx + CELL_SIZE, gy + CELL_SIZE * 0.7f)),
                    (new Vector2I(0, 1), new Vector2(gx + CELL_SIZE * 0.3f, gy + CELL_SIZE), new Vector2(gx + CELL_SIZE * 0.7f, gy + CELL_SIZE)),
                };
                foreach (var (dir, from, to) in dirs)
                {
                    if (_grid.ContainsKey(pos2 + dir))
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

                _roomInfo.Text = $"Room ({gx},{gy}) | {type} | Doors: {string.Join(",", doorDirs)} | {(onPath ? "MAIN PATH" : "Branch")}";
                _mapPanel.QueueRedraw();
                PreviewSelectedRoom();
            }
        }

        // ===== 3D ROOM PREVIEW =====

        private void ClearPreview()
        {
            _selectedNodes.Clear();
            _originalTransforms.Clear();

            foreach (var child in _roomPreviewRoot.GetChildren())
                if (child is Node n) n.QueueFree();
            foreach (var child in _overlayRoot.GetChildren())
                if (child is Node n) n.QueueFree();
            foreach (var child in _labelRoot.GetChildren())
                if (child is Node n) n.QueueFree();
            foreach (var child in _selectionHighlightRoot.GetChildren())
                if (child is Node n) n.QueueFree();

            UpdateInspector();
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

                    // Store original transforms for all selectable nodes
                    StoreOriginalTransforms(room);

                    // Apply saved overrides
                    ApplyOverridesToRoom(room);

                    // Build overlays
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

        private void StoreOriginalTransforms(Node node)
        {
            if (node is Node3D n3d && IsSelectableNode(n3d))
            {
                if (!_originalTransforms.ContainsKey(n3d))
                    _originalTransforms[n3d] = n3d.Transform;
            }

            foreach (var child in node.GetChildren())
            {
                if (child is Node n)
                    StoreOriginalTransforms(n);
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
                    StoreOriginalTransforms(room);
                    BuildCollisionOverlay(room);
                    BuildDebugLabels(room);
                    BuildNodeList(room);
                }

                _previewLabel.Text = $"Layout: {LayoutNames[_layoutIndex]}";
            }
            catch (Exception e)
            {
                _previewLabel.Text = $"Layout error: {e.Message}";
            }
        }

        // ===== COLLISION OVERLAY =====

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

        // ===== DEBUG ID LABELS =====

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

                if (isInteresting || !displayName.StartsWith("@"))
                {
                    var pos3d = n3d.GlobalPosition;
                    string shapeInfo = "";

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
            if (node is Area3D) return new Color(1f, 0.3f, 0.3f);
            if (node is StaticBody3D) return new Color(0.3f, 1f, 0.5f);
            if (node is MeshInstance3D) return new Color(0.5f, 0.7f, 1f);
            if (node is Marker3D) return new Color(1f, 1f, 0.3f);
            return new Color(0.8f, 0.8f, 0.8f);
        }

        // ===== NODE TREE LISTING =====

        private void BuildNodeList(Node room)
        {
            if (_nodeList == null) return;

            foreach (var child in _nodeList.GetChildren())
                if (child is Node n) n.QueueFree();

            CollectNodeListEntries(room, 0);
        }

        private void CollectNodeListEntries(Node node, int depth)
        {
            if (depth > 5) return;

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

                // Make scene tree entries clickable to select
                var entryBtn = new Button();
                entryBtn.Text = $"{indent}{displayName} [{className}]{posStr}";
                entryBtn.Alignment = HorizontalAlignment.Left;
                entryBtn.AddThemeFontSizeOverride("font_size", EditorStyles.FontTiny);
                entryBtn.AddThemeColorOverride("font_color", labelColor);
                entryBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                entryBtn.CustomMinimumSize = new Vector2(0, 16);
                entryBtn.ClipText = true;

                // Flat style
                var flatStyle = EditorStyles.MakeFlat(new Color(0, 0, 0, 0));
                entryBtn.AddThemeStyleboxOverride("normal", flatStyle);
                entryBtn.AddThemeStyleboxOverride("hover", EditorStyles.MakeFlat(EditorStyles.BgHover));
                entryBtn.AddThemeStyleboxOverride("pressed", EditorStyles.MakeFlat(EditorStyles.BgSelected));

                if (node is Node3D selectableNode && IsSelectableNode(selectableNode))
                {
                    var captured = selectableNode;
                    entryBtn.Pressed += () =>
                    {
                        _selectedNodes.Clear();
                        _selectedNodes.Add(captured);
                        UpdateSelectionHighlight();
                        UpdateInspector();
                    };
                }

                _nodeList.AddChild(entryBtn);
            }

            foreach (var child in node.GetChildren())
            {
                if (child is Node n)
                    CollectNodeListEntries(n, depth + 1);
            }
        }

        // ===== HELPERS =====

        private static SpinBox MakeTransformSpinBox(double min, double max, double step)
        {
            var sb = new SpinBox();
            sb.MinValue = min;
            sb.MaxValue = max;
            sb.Step = step;
            sb.Value = 0;
            sb.CustomMinimumSize = new Vector2(55, 0);
            sb.AddThemeFontSizeOverride("font_size", EditorStyles.FontTiny);
            sb.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            return sb;
        }

        private static Label MakeAxisLabel(string axis)
        {
            var label = EditorStyles.MakeLabel(axis, EditorStyles.FontTiny, EditorStyles.TextMuted);
            label.CustomMinimumSize = new Vector2(10, 0);
            return label;
        }

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

            var json = LoadJson("res://Data/room_overrides.json");
            if (json != null)
                _roomOverrides = json;
            else
                _roomOverrides = new Dictionary<string, object>();

            SetStatus($"Ready — {_roomOverrides.Count} room overrides loaded", EditorStyles.StatusSaved);
        }

        protected override void Save()
        {
            if (_roomOverrides == null || _roomOverrides.Count == 0)
            {
                SetStatus("No room overrides to save", EditorStyles.TextMuted);
                return;
            }

            if (SaveJson("res://Data/room_overrides.json", _roomOverrides))
            {
                MarkClean();
                SetStatus($"Saved {_roomOverrides.Count} room overrides  [Remember to git push]", EditorStyles.StatusSaved);
                GD.Print($"[DungeonVisualizer] Saved {_roomOverrides.Count} room overrides");
            }
            else
            {
                SetStatus("Save failed!", EditorStyles.StatusError);
            }
        }

        protected override void RestoreSnapshot(string jsonSnapshot)
        {
            var parsed = MiniJson.Deserialize(jsonSnapshot) as Dictionary<string, object>;
            if (parsed != null)
            {
                _roomOverrides = parsed;
                MarkDirty();
                // Re-render current room with restored overrides
                if (_selectedRoom.HasValue)
                    PreviewSelectedRoom();
            }
        }
    }
}
