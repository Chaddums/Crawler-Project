using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JunkbotArena.Editor
{
    public partial class DungeonVisualizer
    {
        // === Tool modes ===
        enum EditorTool { Select, Place, Erase, Collision }

        private EditorTool _currentTool = EditorTool.Select;
        private Button _toolSelect, _toolPlace, _toolErase, _toolCollision;

        // === Ghost preview ===
        private Node3D _ghostNode;
        private float _ghostRotY;

        // === Placed objects ===
        private Node3D _placedObjectsRoot;
        private readonly List<PlacedObjectData> _placedObjects = new();
        private int _placedIdCounter;

        // === Collision drawing ===
        private bool _isDrawingCollision;
        private Vector3 _collisionStart;
        private Node3D _collisionPreview;

        // === Type-specific inspector ===
        private VBoxContainer _typePropsPanel;

        // ===== PlacedObjectData =====

        private class PlacedObjectData
        {
            public string Id;
            public string Type;       // "model", "vfx", "audio", "trigger", "collision"
            public string Category;   // ModelLibrary category or special
            public string AssetId;    // model id, vfx method, sfx name, trigger type
            public Vector3 Position;
            public Vector3 RotationDeg;
            public Vector3 Scale = Vector3.One;
            public bool Visible = true;
            public Dictionary<string, object> Props = new();
            public Node3D Node;       // runtime reference

            public Dictionary<string, object> ToDict()
            {
                var d = new Dictionary<string, object>
                {
                    ["id"] = Id,
                    ["type"] = Type,
                    ["category"] = Category,
                    ["assetId"] = AssetId,
                    ["posX"] = (double)Position.X,
                    ["posY"] = (double)Position.Y,
                    ["posZ"] = (double)Position.Z,
                    ["rotX"] = (double)RotationDeg.X,
                    ["rotY"] = (double)RotationDeg.Y,
                    ["rotZ"] = (double)RotationDeg.Z,
                    ["scaleX"] = (double)Scale.X,
                    ["scaleY"] = (double)Scale.Y,
                    ["scaleZ"] = (double)Scale.Z,
                };
                if (!Visible) d["visible"] = false;
                if (Props.Count > 0)
                {
                    var propsDict = new Dictionary<string, object>(Props);
                    d["props"] = propsDict;
                }
                return d;
            }

            public static PlacedObjectData FromDict(Dictionary<string, object> d)
            {
                var p = new PlacedObjectData
                {
                    Id = d.TryGetValue("id", out var id) ? id.ToString() : "",
                    Type = d.TryGetValue("type", out var t) ? t.ToString() : "model",
                    Category = d.TryGetValue("category", out var c) ? c.ToString() : "prop",
                    AssetId = d.TryGetValue("assetId", out var a) ? a.ToString() : "",
                    Position = new Vector3(
                        GetFloat(d, "posX"), GetFloat(d, "posY"), GetFloat(d, "posZ")),
                    RotationDeg = new Vector3(
                        GetFloat(d, "rotX"), GetFloat(d, "rotY"), GetFloat(d, "rotZ")),
                    Scale = new Vector3(
                        GetFloat(d, "scaleX", 1f), GetFloat(d, "scaleY", 1f), GetFloat(d, "scaleZ", 1f)),
                    Visible = !d.TryGetValue("visible", out var v) || Convert.ToBoolean(v),
                };
                if (d.TryGetValue("props", out var props) && props is Dictionary<string, object> pd)
                    p.Props = new Dictionary<string, object>(pd);
                return p;
            }

            private static float GetFloat(Dictionary<string, object> d, string key, float def = 0f)
            {
                return d.TryGetValue(key, out var val) ? Convert.ToSingle(val) : def;
            }
        }

        // ===== Tool bar =====

        private void BuildToolBar(VBoxContainer content)
        {
            var toolRow = new HBoxContainer();
            toolRow.AddThemeConstantOverride("separation", 4);

            _toolSelect = EditorStyles.MakeButton("Select (1)", EditorStyles.FontSmall);
            _toolSelect.ToggleMode = true;
            _toolSelect.ButtonPressed = true;
            _toolSelect.Pressed += () => SetEditorTool(EditorTool.Select);
            toolRow.AddChild(_toolSelect);

            _toolPlace = EditorStyles.MakeButton("Place (2)", EditorStyles.FontSmall);
            _toolPlace.ToggleMode = true;
            _toolPlace.Pressed += () => SetEditorTool(EditorTool.Place);
            toolRow.AddChild(_toolPlace);

            _toolErase = EditorStyles.MakeButton("Erase (3)", EditorStyles.FontSmall);
            _toolErase.ToggleMode = true;
            _toolErase.Pressed += () => SetEditorTool(EditorTool.Erase);
            toolRow.AddChild(_toolErase);

            _toolCollision = EditorStyles.MakeButton("Collision (4)", EditorStyles.FontSmall);
            _toolCollision.ToggleMode = true;
            _toolCollision.Pressed += () => SetEditorTool(EditorTool.Collision);
            toolRow.AddChild(_toolCollision);

            toolRow.AddChild(new VSeparator());

            _placingLabel = EditorStyles.MakeLabel("", EditorStyles.FontSmall, EditorStyles.TextMuted);
            _placingLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            toolRow.AddChild(_placingLabel);

            content.AddChild(toolRow);
        }

        private void SetEditorTool(EditorTool tool)
        {
            _currentTool = tool;
            _toolSelect.ButtonPressed = tool == EditorTool.Select;
            _toolPlace.ButtonPressed = tool == EditorTool.Place;
            _toolErase.ButtonPressed = tool == EditorTool.Erase;
            _toolCollision.ButtonPressed = tool == EditorTool.Collision;

            if (tool != EditorTool.Place)
                ClearGhostPreview();

            if (tool == EditorTool.Place && _selectedAssetId != null)
            {
                _placingLabel.Text = $"Placing: {_selectedAssetId}";
                CreateGhostPreview();
            }
            else if (tool == EditorTool.Erase)
                _placingLabel.Text = "Click to erase";
            else if (tool == EditorTool.Collision)
                _placingLabel.Text = "Click+drag to draw collision box";
            else
                _placingLabel.Text = "";
        }

        // ===== Tool key shortcuts (1/2/3/4, R, Delete) =====

        private bool HandleToolKeys(InputEventKey key)
        {
            if (!key.Pressed || key.Echo) return false;

            switch (key.Keycode)
            {
                case Key.Key1:
                    SetEditorTool(EditorTool.Select);
                    return true;
                case Key.Key2:
                    SetEditorTool(EditorTool.Place);
                    return true;
                case Key.Key3:
                    SetEditorTool(EditorTool.Erase);
                    return true;
                case Key.Key4:
                    SetEditorTool(EditorTool.Collision);
                    return true;
                case Key.R:
                    if (_currentTool == EditorTool.Place && _ghostNode != null)
                    {
                        _ghostRotY = (_ghostRotY + 90f) % 360f;
                        _ghostNode.RotationDegrees = new Vector3(0, _ghostRotY, 0);
                    }
                    return true;
                case Key.Delete:
                    DeleteOrHideSelected();
                    return true;
                case Key.Escape:
                    DeselectAll();
                    return true;
                case Key.F:
                    ToggleFreeCam();
                    return true;
            }
            return false;
        }

        private void DeselectAll()
        {
            // Clear palette selection
            _selectedAssetId = null;
            _selectedAssetType = null;
            _selectedAssetCategory = null;
            ClearGhostPreview();

            // Switch back to Select tool
            SetEditorTool(EditorTool.Select);

            // Clear node selection
            _selectedNodes.Clear();
            UpdateSelectionHighlight();
            UpdateInspector();

            // Clear palette toggle states
            if (_paletteList != null)
            {
                foreach (var child in _paletteList.GetChildren())
                    if (child is Button btn) btn.ButtonPressed = false;
            }
        }

        // ===== Ghost preview =====

        private void CreateGhostPreview()
        {
            ClearGhostPreview();
            if (_selectedAssetId == null || _viewport == null) return;

            _ghostNode = InstantiateAsset(_selectedAssetType, _selectedAssetCategory, _selectedAssetId, isGhost: true);
            if (_ghostNode == null) return;

            _ghostNode.Name = "GhostPreview";
            _ghostRotY = 0;
            ApplyGhostMaterial(_ghostNode);
            _viewport.AddChild(_ghostNode);
            _ghostNode.Visible = false; // hidden until mouse enters viewport
        }

        private void ClearGhostPreview()
        {
            if (_ghostNode != null && GodotObject.IsInstanceValid(_ghostNode))
            {
                _ghostNode.QueueFree();
                _ghostNode = null;
            }
        }

        private void ApplyGhostMaterial(Node node)
        {
            if (node is MeshInstance3D mi)
            {
                var mat = new StandardMaterial3D();
                mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                mat.AlbedoColor = new Color(0.5f, 0.8f, 1f, 0.4f);
                mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
                mi.MaterialOverride = mat;
            }
            foreach (var child in node.GetChildren())
                if (child is Node n) ApplyGhostMaterial(n);
        }

        private void UpdateGhostPosition(Vector2 screenPos)
        {
            if (_ghostNode == null || !GodotObject.IsInstanceValid(_ghostNode)) return;

            var containerSize = _viewportContainer.Size;
            var viewportSize = (Vector2)_viewport.Size;
            var vpPos = screenPos * viewportSize / containerSize;

            var plane = new Plane(Vector3.Up, new Vector3(0, 0, 0));
            var hit = DragRayPlaneHit(vpPos, plane);
            if (hit.HasValue)
            {
                var pos = hit.Value;
                if (_gridSnap > 0)
                {
                    pos.X = Mathf.Round(pos.X / _gridSnap) * _gridSnap;
                    pos.Z = Mathf.Round(pos.Z / _gridSnap) * _gridSnap;
                }
                _ghostNode.Position = pos;
                _ghostNode.Visible = true;
            }
        }

        // ===== Click handling per tool =====

        private void HandlePlaceClick(Vector2 screenPos)
        {
            if (_selectedAssetId == null || _selectedAssetType == null) return;

            var containerSize = _viewportContainer.Size;
            var viewportSize = (Vector2)_viewport.Size;
            var vpPos = screenPos * viewportSize / containerSize;
            var plane = new Plane(Vector3.Up, Vector3.Zero);
            var hit = DragRayPlaneHit(vpPos, plane);
            if (!hit.HasValue) return;

            var pos = hit.Value;
            if (_gridSnap > 0)
            {
                pos.X = Mathf.Round(pos.X / _gridSnap) * _gridSnap;
                pos.Z = Mathf.Round(pos.Z / _gridSnap) * _gridSnap;
            }

            PushUndoSnapshot();

            var data = new PlacedObjectData
            {
                Id = $"p_{++_placedIdCounter:D3}",
                Type = _selectedAssetType,
                Category = _selectedAssetCategory,
                AssetId = _selectedAssetId,
                Position = pos,
                RotationDeg = new Vector3(0, _ghostRotY, 0),
                Scale = Vector3.One,
            };

            InstantiatePlacedObject(data);
            _placedObjects.Add(data);
            SaveNodeOverrides();
            MarkDirty();
        }

        private void HandleEraseClick(Vector2 screenPos)
        {
            var containerSize = _viewportContainer.Size;
            var viewportSize = (Vector2)_viewport.Size;
            var vpPos = screenPos * viewportSize / containerSize;

            var from = _camera.ProjectRayOrigin(vpPos);
            var dir = _camera.ProjectRayNormal(vpPos);

            // Check placed objects first
            PlacedObjectData hitPlaced = null;
            float bestDist = float.MaxValue;
            foreach (var po in _placedObjects)
            {
                if (po.Node == null || !GodotObject.IsInstanceValid(po.Node)) continue;
                var aabb = ComputeNodeAabb(po.Node);
                if (RayIntersectsAabb(from, dir, aabb, out float dist) && dist < bestDist)
                {
                    bestDist = dist;
                    hitPlaced = po;
                }
            }

            if (hitPlaced != null)
            {
                PushUndoSnapshot();
                RemovePlacedObject(hitPlaced);
                SaveNodeOverrides();
                MarkDirty();
                return;
            }

            // Otherwise try to hit a generated object and hide it
            var hits = new List<(Node3D node, float dist, float volume)>();
            var room = _roomPreviewRoot.GetChildCount() > 0 ? _roomPreviewRoot.GetChild(0) : null;
            if (room is Node3D roomNode)
                CollectAllHits(roomNode, from, dir, hits, 0);

            if (hits.Count > 0)
            {
                hits.Sort((a, b) => a.volume.CompareTo(b.volume));
                PushUndoSnapshot();
                SetVisibleRecursive(hits[0].node, false);
                SaveNodeOverrides();
                MarkDirty();
            }
        }

        private void HandleCollisionPress(Vector2 screenPos)
        {
            var containerSize = _viewportContainer.Size;
            var viewportSize = (Vector2)_viewport.Size;
            var vpPos = screenPos * viewportSize / containerSize;
            var plane = new Plane(Vector3.Up, Vector3.Zero);
            var hit = DragRayPlaneHit(vpPos, plane);
            if (!hit.HasValue) return;

            _isDrawingCollision = true;
            _collisionStart = hit.Value;
            if (_gridSnap > 0)
            {
                _collisionStart.X = Mathf.Round(_collisionStart.X / _gridSnap) * _gridSnap;
                _collisionStart.Z = Mathf.Round(_collisionStart.Z / _gridSnap) * _gridSnap;
            }

            // Create preview box
            _collisionPreview = CreateWireframeBox(Vector3.One, new Color(0.2f, 1f, 0.3f, 0.5f));
            _collisionPreview.Position = _collisionStart;
            _viewport.AddChild(_collisionPreview);
        }

        private void UpdateCollisionDrag(Vector2 screenPos)
        {
            if (!_isDrawingCollision || _collisionPreview == null) return;

            var containerSize = _viewportContainer.Size;
            var viewportSize = (Vector2)_viewport.Size;
            var vpPos = screenPos * viewportSize / containerSize;
            var plane = new Plane(Vector3.Up, Vector3.Zero);
            var hit = DragRayPlaneHit(vpPos, plane);
            if (!hit.HasValue) return;

            var endPos = hit.Value;
            if (_gridSnap > 0)
            {
                endPos.X = Mathf.Round(endPos.X / _gridSnap) * _gridSnap;
                endPos.Z = Mathf.Round(endPos.Z / _gridSnap) * _gridSnap;
            }

            var sizeX = Mathf.Abs(endPos.X - _collisionStart.X);
            var sizeZ = Mathf.Abs(endPos.Z - _collisionStart.Z);
            sizeX = Mathf.Max(sizeX, 0.5f);
            sizeZ = Mathf.Max(sizeZ, 0.5f);
            float sizeY = 2f; // default height

            var center = (_collisionStart + endPos) / 2f;
            center.Y = sizeY / 2f;

            _collisionPreview.Position = center;
            _collisionPreview.Scale = new Vector3(sizeX, sizeY, sizeZ);
        }

        private void FinishCollisionDrag(Vector2 screenPos)
        {
            if (!_isDrawingCollision) return;
            _isDrawingCollision = false;

            if (_collisionPreview != null && GodotObject.IsInstanceValid(_collisionPreview))
            {
                var pos = _collisionPreview.Position;
                var scale = _collisionPreview.Scale;
                _collisionPreview.QueueFree();
                _collisionPreview = null;

                PushUndoSnapshot();

                var data = new PlacedObjectData
                {
                    Id = $"p_{++_placedIdCounter:D3}",
                    Type = "collision",
                    Category = "collision",
                    AssetId = "box",
                    Position = pos,
                    Scale = scale,
                };

                InstantiatePlacedObject(data);
                _placedObjects.Add(data);
                SaveNodeOverrides();
                MarkDirty();
            }
        }

        // ===== Delete / Hide =====

        private void DeleteOrHideSelected()
        {
            if (_selectedNodes.Count == 0) return;
            PushUndoSnapshot();

            foreach (var node in _selectedNodes.ToList())
            {
                if (!GodotObject.IsInstanceValid(node)) continue;

                // Check if it's a placed object
                var placed = _placedObjects.Find(p => p.Node == node);
                if (placed != null)
                {
                    RemovePlacedObject(placed);
                }
                else
                {
                    // Generated object — hide it
                    SetVisibleRecursive(node, false);
                }
            }

            _selectedNodes.Clear();
            UpdateSelectionHighlight();
            UpdateInspector();
            SaveNodeOverrides();
            MarkDirty();
        }

        private void RemovePlacedObject(PlacedObjectData data)
        {
            if (data.Node != null && GodotObject.IsInstanceValid(data.Node))
                data.Node.QueueFree();
            _placedObjects.Remove(data);
            _selectedNodes.Remove(data.Node);
        }

        // ===== Instantiation =====

        private void EnsurePlacedObjectsRoot()
        {
            if (_placedObjectsRoot == null || !GodotObject.IsInstanceValid(_placedObjectsRoot))
            {
                _placedObjectsRoot = new Node3D();
                _placedObjectsRoot.Name = "PlacedObjects";
                _viewport.AddChild(_placedObjectsRoot);
            }
        }

        private void InstantiatePlacedObject(PlacedObjectData data)
        {
            EnsurePlacedObjectsRoot();

            var node = InstantiateAsset(data.Type, data.Category, data.AssetId, isGhost: false);
            if (node == null)
            {
                // Fallback: colored box
                node = CreateFallbackBox(data.Type);
            }

            node.Name = data.Id;
            node.Position = data.Position;
            node.RotationDegrees = data.RotationDeg;
            node.Scale = data.Scale;
            node.Visible = data.Visible;

            data.Node = node;
            _placedObjectsRoot.AddChild(node);
        }

        private Node3D InstantiateAsset(string type, string category, string assetId, bool isGhost)
        {
            switch (type)
            {
                case "model":
                {
                    var model = ModelLibrary.TryLoad(category, assetId);
                    if (model != null) return model;
                    // Procedural fallback
                    return CreateProceduralBox(new Color(0.5f, 0.5f, 0.5f), new Vector3(1, 1, 1));
                }

                case "vfx":
                {
                    if (isGhost)
                        return CreateGizmoSphere(new Color(1f, 0.5f, 0.2f, 0.4f), 0.3f);

                    var container = new Node3D();
                    container.Name = assetId;
                    var vfx = CreateVfxByName(assetId);
                    if (vfx != null)
                        container.AddChild(vfx);
                    // Add visible gizmo so it's selectable
                    var gizmo = CreateGizmoSphere(new Color(1f, 0.5f, 0.2f, 0.6f), 0.3f);
                    container.AddChild(gizmo);
                    return container;
                }

                case "audio":
                {
                    var container = new Node3D();
                    container.Name = assetId;
                    var marker = new Marker3D();
                    container.AddChild(marker);
                    // Cyan cube gizmo
                    var cube = CreateProceduralBox(new Color(0f, 0.8f, 0.8f, 0.6f), new Vector3(0.4f, 0.4f, 0.4f));
                    container.AddChild(cube);
                    // Label
                    var label = new Label3D();
                    label.Text = assetId;
                    label.FontSize = 16;
                    label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
                    label.PixelSize = 0.008f;
                    label.Position = Vector3.Up * 0.5f;
                    label.Modulate = new Color(0f, 0.9f, 0.9f);
                    container.AddChild(label);
                    return container;
                }

                case "trigger":
                {
                    var area = new Area3D();
                    area.Name = assetId;
                    var shape = new CollisionShape3D();
                    var box = new BoxShape3D();
                    box.Size = new Vector3(4, 3, 4);
                    shape.Shape = box;
                    area.AddChild(shape);
                    // Yellow wireframe
                    var wireframe = CreateWireframeBox(new Vector3(4, 3, 4), new Color(1f, 0.9f, 0.2f, 0.5f));
                    area.AddChild(wireframe);
                    // Label
                    var label = new Label3D();
                    label.Text = $"Trigger: {assetId}";
                    label.FontSize = 16;
                    label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
                    label.PixelSize = 0.008f;
                    label.Position = Vector3.Up * 2f;
                    label.Modulate = new Color(1f, 0.9f, 0.2f);
                    area.AddChild(label);
                    return area;
                }

                case "collision":
                {
                    var body = new StaticBody3D();
                    body.Name = "CollisionBox";
                    var shape = new CollisionShape3D();
                    var box = new BoxShape3D();
                    box.Size = Vector3.One; // scaled by parent
                    shape.Shape = box;
                    body.AddChild(shape);
                    // Green wireframe
                    var wireframe = CreateWireframeBox(Vector3.One, new Color(0.2f, 1f, 0.3f, 0.5f));
                    body.AddChild(wireframe);
                    return body;
                }

                default:
                    return CreateFallbackBox("model");
            }
        }

        private static Node3D CreateVfxByName(string name)
        {
            try
            {
                return name switch
                {
                    "TorchFire" => VfxFactory.CreateTorchFireParticles(),
                    "AmbientParticles" => VfxFactory.CreateAmbientParticles(new Color(0.5f, 0.5f, 1f), 3f),
                    "Portal" => VfxFactory.CreatePortalParticles(new Color(0.3f, 0.5f, 1f)),
                    "HitParticles" => VfxFactory.CreateHitParticles(Colors.White),
                    "DeathParticles" => VfxFactory.CreateDeathParticles(Colors.Red),
                    "LootBurst" => VfxFactory.CreateLootBurstParticles(Colors.Gold),
                    "CelebrationBurst" => VfxFactory.CreateCelebrationBurst(Colors.Gold, 30),
                    "HealParticles" => VfxFactory.CreateHealParticles(),
                    "PoisonCloud" => VfxFactory.CreatePoisonCloud(),
                    "FreezeBurst" => VfxFactory.CreateFreezeBurst(),
                    "ElectricSparks" => VfxFactory.CreateElectricSparks(),
                    "MuzzleFlash" => VfxFactory.CreateMuzzleFlash(),
                    "ArcaneCircle" => VfxFactory.CreateArcaneCircle(new Color(0.5f, 0.3f, 1f)),
                    "ShockwaveRing" => VfxFactory.CreateShockwaveRing(new Color(0.5f, 0.8f, 1f)),
                    "MusicNotes" => VfxFactory.CreateMusicNotes(new Color(0.3f, 0.9f, 0.5f)),
                    "DashTrail" => VfxFactory.CreateDashTrail(Colors.Cyan),
                    "AuraRing" => VfxFactory.CreateAuraRing(Colors.Purple),
                    "StunIndicator" => VfxFactory.CreateStunIndicator(),
                    "GroundSparks" => VfxFactory.CreateGroundSparks(Colors.Orange),
                    "SadPuff" => VfxFactory.CreateSadPuff(),
                    _ => null,
                };
            }
            catch
            {
                return null;
            }
        }

        // ===== Helper geometry =====

        private static Node3D CreateProceduralBox(Color color, Vector3 size)
        {
            var mi = new MeshInstance3D();
            mi.Mesh = new BoxMesh { Size = size };
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = color;
            if (color.A < 1f)
            {
                mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            }
            mi.MaterialOverride = mat;
            return mi;
        }

        private static Node3D CreateGizmoSphere(Color color, float radius)
        {
            var mi = new MeshInstance3D();
            mi.Mesh = new SphereMesh { Radius = radius, Height = radius * 2 };
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = color;
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mi.MaterialOverride = mat;
            return mi;
        }

        private static Node3D CreateWireframeBox(Vector3 size, Color color)
        {
            var half = size / 2f;
            Vector3[] corners =
            {
                new(-half.X, -half.Y, -half.Z), new(half.X, -half.Y, -half.Z),
                new(half.X, -half.Y, half.Z),   new(-half.X, -half.Y, half.Z),
                new(-half.X, half.Y, -half.Z),   new(half.X, half.Y, -half.Z),
                new(half.X, half.Y, half.Z),     new(-half.X, half.Y, half.Z),
            };
            int[] edges = { 0,1, 1,2, 2,3, 3,0, 4,5, 5,6, 6,7, 7,4, 0,4, 1,5, 2,6, 3,7 };

            var im = new ImmediateMesh();
            im.SurfaceBegin(Mesh.PrimitiveType.Lines);
            for (int i = 0; i < edges.Length; i += 2)
            {
                im.SurfaceAddVertex(corners[edges[i]]);
                im.SurfaceAddVertex(corners[edges[i + 1]]);
            }
            im.SurfaceEnd();

            var mi = new MeshInstance3D();
            mi.Mesh = im;
            var mat = new StandardMaterial3D();
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.AlbedoColor = color;
            mat.NoDepthTest = true;
            if (color.A < 1f) mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mi.MaterialOverride = mat;
            return mi;
        }

        private static Node3D CreateFallbackBox(string type)
        {
            var color = type switch
            {
                "vfx" => new Color(1f, 0.5f, 0.2f, 0.6f),
                "audio" => new Color(0f, 0.8f, 0.8f, 0.6f),
                "trigger" => new Color(1f, 0.9f, 0.2f, 0.6f),
                "collision" => new Color(0.2f, 1f, 0.3f, 0.6f),
                _ => new Color(0.5f, 0.5f, 0.5f, 0.8f),
            };
            return CreateProceduralBox(color, new Vector3(0.5f, 0.5f, 0.5f));
        }

        // ===== Placed object persistence (integrates with room_overrides.json) =====

        private void SavePlacedObjectsToOverrides()
        {
            var roomKey = GetCurrentRoomKey();
            if (roomKey == null) return;

            if (_roomOverrides == null)
                _roomOverrides = new Dictionary<string, object>();

            // Get or create room entry
            if (!_roomOverrides.TryGetValue(roomKey, out var roomObj) || roomObj is not Dictionary<string, object> roomDict)
            {
                roomDict = new Dictionary<string, object>();
                _roomOverrides[roomKey] = roomDict;
            }

            // Sync transforms from live nodes
            foreach (var po in _placedObjects)
            {
                if (po.Node != null && GodotObject.IsInstanceValid(po.Node))
                {
                    po.Position = po.Node.Position;
                    po.RotationDeg = po.Node.RotationDegrees;
                    po.Scale = po.Node.Scale;
                    po.Visible = po.Node.Visible;
                }
            }

            if (_placedObjects.Count > 0)
            {
                var placedList = new List<object>();
                foreach (var po in _placedObjects)
                    placedList.Add(po.ToDict());
                roomDict["_placed"] = placedList;
            }
            else
            {
                roomDict.Remove("_placed");
            }

            // Clean up empty room entries
            if (roomDict.Count == 0)
                _roomOverrides.Remove(roomKey);
        }

        private void LoadPlacedObjectsFromOverrides()
        {
            _placedObjects.Clear();
            _placedIdCounter = 0;

            var roomKey = GetCurrentRoomKey();
            if (roomKey == null || _roomOverrides == null) return;
            if (!_roomOverrides.TryGetValue(roomKey, out var roomObj)) return;
            if (roomObj is not Dictionary<string, object> roomDict) return;
            if (!roomDict.TryGetValue("_placed", out var placedObj)) return;
            if (placedObj is not List<object> placedList) return;

            EnsurePlacedObjectsRoot();

            foreach (var item in placedList)
            {
                if (item is not Dictionary<string, object> d) continue;
                var data = PlacedObjectData.FromDict(d);
                InstantiatePlacedObject(data);
                _placedObjects.Add(data);

                // Track max ID counter
                if (data.Id.StartsWith("p_") && int.TryParse(data.Id.Substring(2), out int num))
                    _placedIdCounter = Math.Max(_placedIdCounter, num);
            }
        }

        private void ClearPlacedObjects()
        {
            _placedObjects.Clear();
            if (_placedObjectsRoot != null && GodotObject.IsInstanceValid(_placedObjectsRoot))
            {
                foreach (var child in _placedObjectsRoot.GetChildren())
                    if (child is Node n) n.QueueFree();
            }
        }

        // ===== Type-specific inspector =====

        private void BuildTypePropsInspector(VBoxContainer rightPanel)
        {
            _typePropsPanel = new VBoxContainer();
            _typePropsPanel.AddThemeConstantOverride("separation", 3);
            rightPanel.AddChild(_typePropsPanel);
        }

        private void UpdateTypePropsInspector()
        {
            foreach (var child in _typePropsPanel.GetChildren())
                if (child is Node n) n.QueueFree();

            if (_selectedNodes.Count != 1) return;

            var node = _selectedNodes[0];

            // Show collision shape editor for ANY node with a CollisionShape3D child
            var colShape = FindChildCollisionShape(node);
            if (colShape != null && colShape.Shape is BoxShape3D)
                BuildCollisionShapeEditor(node, colShape);

            // Show mesh edit tools for any node containing meshes
            if (HasChildMeshes(node))
                BuildMeshEditSection(node);

            // Show placed-object-specific props
            var placed = _placedObjects.Find(p => p.Node == node);
            if (placed == null) return;

            _typePropsPanel.AddChild(EditorStyles.MakeSeparator());
            _typePropsPanel.AddChild(EditorStyles.MakeLabel($"Type: {placed.Type}", EditorStyles.FontSmall, AccentColor));

            switch (placed.Type)
            {
                case "vfx":
                    BuildVfxProps(placed);
                    break;
                case "audio":
                    BuildAudioProps(placed);
                    break;
                case "trigger":
                    BuildTriggerProps(placed);
                    break;
                case "collision":
                    BuildCollisionProps(placed);
                    break;
            }
        }

        // ===== Collision Shape Editor (for any node with BoxShape3D) =====

        private static CollisionShape3D FindChildCollisionShape(Node node)
        {
            foreach (var child in node.GetChildren())
            {
                if (child is CollisionShape3D col && col.Shape != null)
                    return col;
                // Check one level deeper (e.g. StaticBody3D > CollisionShape3D)
                if (child is Node n)
                {
                    foreach (var grandchild in n.GetChildren())
                        if (grandchild is CollisionShape3D gcol && gcol.Shape != null)
                            return gcol;
                }
            }
            return null;
        }

        private bool _updatingCollisionInspector;

        private void BuildCollisionShapeEditor(Node3D owner, CollisionShape3D colShape)
        {
            if (colShape.Shape is not BoxShape3D box) return;

            _typePropsPanel.AddChild(EditorStyles.MakeSeparator());
            _typePropsPanel.AddChild(EditorStyles.MakeLabel("Collision Shape", EditorStyles.FontSmall, new Color(0.3f, 1f, 0.5f)));

            // Size
            _typePropsPanel.AddChild(EditorStyles.MakeLabel("Size:", EditorStyles.FontTiny, EditorStyles.TextSecondary));
            var sizeRow = new HBoxContainer();
            sizeRow.AddThemeConstantOverride("separation", 2);

            sizeRow.AddChild(MakeAxisLabel("X"));
            var sizeX = MakeTransformSpinBox(0.1, 200, 0.25);
            sizeX.Value = box.Size.X;
            sizeRow.AddChild(sizeX);
            sizeRow.AddChild(MakeAxisLabel("Y"));
            var sizeY = MakeTransformSpinBox(0.1, 200, 0.25);
            sizeY.Value = box.Size.Y;
            sizeRow.AddChild(sizeY);
            sizeRow.AddChild(MakeAxisLabel("Z"));
            var sizeZ = MakeTransformSpinBox(0.1, 200, 0.25);
            sizeZ.Value = box.Size.Z;
            sizeRow.AddChild(sizeZ);
            _typePropsPanel.AddChild(sizeRow);

            // Offset (CollisionShape3D local position)
            _typePropsPanel.AddChild(EditorStyles.MakeLabel("Offset:", EditorStyles.FontTiny, EditorStyles.TextSecondary));
            var offRow = new HBoxContainer();
            offRow.AddThemeConstantOverride("separation", 2);

            offRow.AddChild(MakeAxisLabel("X"));
            var offX = MakeTransformSpinBox(-100, 100, 0.25);
            offX.Value = colShape.Position.X;
            offRow.AddChild(offX);
            offRow.AddChild(MakeAxisLabel("Y"));
            var offY = MakeTransformSpinBox(-100, 100, 0.25);
            offY.Value = colShape.Position.Y;
            offRow.AddChild(offY);
            offRow.AddChild(MakeAxisLabel("Z"));
            var offZ = MakeTransformSpinBox(-100, 100, 0.25);
            offZ.Value = colShape.Position.Z;
            offRow.AddChild(offZ);
            _typePropsPanel.AddChild(offRow);

            // Wire up value changes
            void ApplyCollisionEdits()
            {
                if (_updatingCollisionInspector) return;
                PushUndoSnapshot();
                box.Size = new Vector3((float)sizeX.Value, (float)sizeY.Value, (float)sizeZ.Value);
                colShape.Position = new Vector3((float)offX.Value, (float)offY.Value, (float)offZ.Value);
                SaveNodeOverrides();
                RebuildCollisionOverlay();
                MarkDirty();
            }

            sizeX.ValueChanged += _ => ApplyCollisionEdits();
            sizeY.ValueChanged += _ => ApplyCollisionEdits();
            sizeZ.ValueChanged += _ => ApplyCollisionEdits();
            offX.ValueChanged += _ => ApplyCollisionEdits();
            offY.ValueChanged += _ => ApplyCollisionEdits();
            offZ.ValueChanged += _ => ApplyCollisionEdits();

            // Action buttons
            var btnRow = new HBoxContainer();
            btnRow.AddThemeConstantOverride("separation", 3);

            var fitBtn = EditorStyles.MakeButton("Fit to Mesh", EditorStyles.FontTiny, new Color(0.3f, 1f, 0.5f));
            fitBtn.CustomMinimumSize = new Vector2(0, 24);
            fitBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            fitBtn.Pressed += () =>
            {
                PushUndoSnapshot();
                FitCollisionToMesh(owner, colShape, box);
                // Refresh spinboxes
                _updatingCollisionInspector = true;
                sizeX.Value = box.Size.X;
                sizeY.Value = box.Size.Y;
                sizeZ.Value = box.Size.Z;
                offX.Value = colShape.Position.X;
                offY.Value = colShape.Position.Y;
                offZ.Value = colShape.Position.Z;
                _updatingCollisionInspector = false;
                SaveNodeOverrides();
                RebuildCollisionOverlay();
                MarkDirty();
            };
            btnRow.AddChild(fitBtn);

            var growBtn = EditorStyles.MakeButton("+0.5", EditorStyles.FontTiny);
            growBtn.CustomMinimumSize = new Vector2(0, 24);
            growBtn.TooltipText = "Grow collision by 0.5 on each side";
            growBtn.Pressed += () =>
            {
                PushUndoSnapshot();
                box.Size += Vector3.One;
                _updatingCollisionInspector = true;
                sizeX.Value = box.Size.X;
                sizeY.Value = box.Size.Y;
                sizeZ.Value = box.Size.Z;
                _updatingCollisionInspector = false;
                SaveNodeOverrides();
                RebuildCollisionOverlay();
                MarkDirty();
            };
            btnRow.AddChild(growBtn);

            var shrinkBtn = EditorStyles.MakeButton("-0.5", EditorStyles.FontTiny);
            shrinkBtn.CustomMinimumSize = new Vector2(0, 24);
            shrinkBtn.TooltipText = "Shrink collision by 0.5 on each side";
            shrinkBtn.Pressed += () =>
            {
                PushUndoSnapshot();
                var s = box.Size - Vector3.One;
                s.X = Mathf.Max(0.1f, s.X);
                s.Y = Mathf.Max(0.1f, s.Y);
                s.Z = Mathf.Max(0.1f, s.Z);
                box.Size = s;
                _updatingCollisionInspector = true;
                sizeX.Value = box.Size.X;
                sizeY.Value = box.Size.Y;
                sizeZ.Value = box.Size.Z;
                _updatingCollisionInspector = false;
                SaveNodeOverrides();
                RebuildCollisionOverlay();
                MarkDirty();
            };
            btnRow.AddChild(shrinkBtn);

            _typePropsPanel.AddChild(btnRow);

            // Per-axis trim buttons
            var trimRow = new HBoxContainer();
            trimRow.AddThemeConstantOverride("separation", 3);

            void AddTrimBtn(string label, int axis, float dir)
            {
                var btn = EditorStyles.MakeButton(label, EditorStyles.FontTiny);
                btn.CustomMinimumSize = new Vector2(0, 22);
                btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                btn.Pressed += () =>
                {
                    PushUndoSnapshot();
                    // Trim: shrink size on one side, shift offset to compensate
                    float trimAmt = _gridSnap > 0 ? _gridSnap : 0.5f;
                    var size = box.Size;
                    var off = colShape.Position;
                    if (axis == 0) { size.X -= trimAmt; off.X += trimAmt * dir * 0.5f; }
                    else if (axis == 1) { size.Y -= trimAmt; off.Y += trimAmt * dir * 0.5f; }
                    else { size.Z -= trimAmt; off.Z += trimAmt * dir * 0.5f; }
                    size.X = Mathf.Max(0.1f, size.X);
                    size.Y = Mathf.Max(0.1f, size.Y);
                    size.Z = Mathf.Max(0.1f, size.Z);
                    box.Size = size;
                    colShape.Position = off;
                    _updatingCollisionInspector = true;
                    sizeX.Value = box.Size.X; sizeY.Value = box.Size.Y; sizeZ.Value = box.Size.Z;
                    offX.Value = off.X; offY.Value = off.Y; offZ.Value = off.Z;
                    _updatingCollisionInspector = false;
                    SaveNodeOverrides();
                    RebuildCollisionOverlay();
                    MarkDirty();
                };
                trimRow.AddChild(btn);
            }

            AddTrimBtn("X-", 0, -1f);
            AddTrimBtn("X+", 0, 1f);
            AddTrimBtn("Z-", 2, -1f);
            AddTrimBtn("Z+", 2, 1f);

            _typePropsPanel.AddChild(EditorStyles.MakeLabel("Trim edge:", EditorStyles.FontTiny, EditorStyles.TextSecondary));
            _typePropsPanel.AddChild(trimRow);

            // Extend row
            var extRow = new HBoxContainer();
            extRow.AddThemeConstantOverride("separation", 3);

            void AddExtBtn(string label, int axis, float dir)
            {
                var btn = EditorStyles.MakeButton(label, EditorStyles.FontTiny);
                btn.CustomMinimumSize = new Vector2(0, 22);
                btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                btn.Pressed += () =>
                {
                    PushUndoSnapshot();
                    float extAmt = _gridSnap > 0 ? _gridSnap : 0.5f;
                    var size = box.Size;
                    var off = colShape.Position;
                    if (axis == 0) { size.X += extAmt; off.X += extAmt * dir * 0.5f; }
                    else if (axis == 1) { size.Y += extAmt; off.Y += extAmt * dir * 0.5f; }
                    else { size.Z += extAmt; off.Z += extAmt * dir * 0.5f; }
                    box.Size = size;
                    colShape.Position = off;
                    _updatingCollisionInspector = true;
                    sizeX.Value = box.Size.X; sizeY.Value = box.Size.Y; sizeZ.Value = box.Size.Z;
                    offX.Value = off.X; offY.Value = off.Y; offZ.Value = off.Z;
                    _updatingCollisionInspector = false;
                    SaveNodeOverrides();
                    RebuildCollisionOverlay();
                    MarkDirty();
                };
                extRow.AddChild(btn);
            }

            AddExtBtn("X-", 0, -1f);
            AddExtBtn("X+", 0, 1f);
            AddExtBtn("Z-", 2, -1f);
            AddExtBtn("Z+", 2, 1f);

            _typePropsPanel.AddChild(EditorStyles.MakeLabel("Extend edge:", EditorStyles.FontTiny, EditorStyles.TextSecondary));
            _typePropsPanel.AddChild(extRow);
        }

        private static void FitCollisionToMesh(Node3D owner, CollisionShape3D colShape, BoxShape3D box)
        {
            // Compute AABB of all child meshes in local space
            var meshAabb = new Aabb();
            bool found = false;
            CollectLocalMeshAabbs(owner, owner.GlobalTransform, ref meshAabb, ref found);

            if (!found) return;

            // Set box size to match mesh AABB
            box.Size = meshAabb.Size;

            // Center the collision shape on the mesh center (in parent-local space)
            colShape.Position = meshAabb.GetCenter();
        }

        private static void CollectLocalMeshAabbs(Node node, Transform3D ownerGlobal, ref Aabb merged, ref bool found)
        {
            if (node is MeshInstance3D mi && mi.Mesh != null)
            {
                var localAabb = mi.Mesh.GetAabb();
                // Transform mesh AABB to owner's local space
                var meshToOwner = ownerGlobal.AffineInverse() * mi.GlobalTransform;
                var corners = new Vector3[8];
                var min = localAabb.Position;
                var max = localAabb.Position + localAabb.Size;
                for (int i = 0; i < 8; i++)
                {
                    corners[i] = meshToOwner * new Vector3(
                        (i & 1) != 0 ? max.X : min.X,
                        (i & 2) != 0 ? max.Y : min.Y,
                        (i & 4) != 0 ? max.Z : min.Z);
                }
                foreach (var c in corners)
                {
                    if (!found) { merged = new Aabb(c, Vector3.Zero); found = true; }
                    else merged = merged.Expand(c);
                }
            }

            foreach (var child in node.GetChildren())
                if (child is Node n)
                    CollectLocalMeshAabbs(n, ownerGlobal, ref merged, ref found);
        }

        private void BuildVfxProps(PlacedObjectData data)
        {
            _typePropsPanel.AddChild(EditorStyles.MakeLabel("Color:", EditorStyles.FontTiny, EditorStyles.TextSecondary));
            var picker = new ColorPickerButton();
            picker.CustomMinimumSize = new Vector2(0, 28);
            float r = data.Props.TryGetValue("colorR", out var cr) ? Convert.ToSingle(cr) : 1f;
            float g = data.Props.TryGetValue("colorG", out var cg) ? Convert.ToSingle(cg) : 1f;
            float b = data.Props.TryGetValue("colorB", out var cb) ? Convert.ToSingle(cb) : 1f;
            picker.Color = new Color(r, g, b);
            picker.ColorChanged += c =>
            {
                data.Props["colorR"] = (double)c.R;
                data.Props["colorG"] = (double)c.G;
                data.Props["colorB"] = (double)c.B;
                SaveNodeOverrides();
                MarkDirty();
            };
            _typePropsPanel.AddChild(picker);

            var rebuildBtn = EditorStyles.MakeButton("Rebuild VFX", EditorStyles.FontTiny, AccentColor);
            rebuildBtn.Pressed += () =>
            {
                if (data.Node != null && GodotObject.IsInstanceValid(data.Node))
                {
                    data.Position = data.Node.Position;
                    data.RotationDeg = data.Node.RotationDegrees;
                    data.Scale = data.Node.Scale;
                    data.Node.QueueFree();
                }
                InstantiatePlacedObject(data);
            };
            _typePropsPanel.AddChild(rebuildBtn);
        }

        private void BuildAudioProps(PlacedObjectData data)
        {
            _typePropsPanel.AddChild(EditorStyles.MakeLabel("Volume:", EditorStyles.FontTiny, EditorStyles.TextSecondary));
            var volSpin = MakeTransformSpinBox(0, 2, 0.1);
            volSpin.Value = data.Props.TryGetValue("volume", out var vol) ? Convert.ToDouble(vol) : 0.8;
            volSpin.ValueChanged += v =>
            {
                data.Props["volume"] = v;
                SaveNodeOverrides();
                MarkDirty();
            };
            _typePropsPanel.AddChild(volSpin);

            _typePropsPanel.AddChild(EditorStyles.MakeLabel("Radius:", EditorStyles.FontTiny, EditorStyles.TextSecondary));
            var radSpin = MakeTransformSpinBox(0.5, 50, 0.5);
            radSpin.Value = data.Props.TryGetValue("radius", out var rad) ? Convert.ToDouble(rad) : 5.0;
            radSpin.ValueChanged += v =>
            {
                data.Props["radius"] = v;
                SaveNodeOverrides();
                MarkDirty();
            };
            _typePropsPanel.AddChild(radSpin);
        }

        private void BuildTriggerProps(PlacedObjectData data)
        {
            _typePropsPanel.AddChild(EditorStyles.MakeLabel("Speaker:", EditorStyles.FontTiny, EditorStyles.TextSecondary));
            var speakerEdit = EditorStyles.MakeLineEdit("AXIS", EditorStyles.FontSmall);
            speakerEdit.Text = data.Props.TryGetValue("speaker", out var sp) ? sp.ToString() : "AXIS";
            speakerEdit.TextChanged += t =>
            {
                data.Props["speaker"] = t;
                SaveNodeOverrides();
                MarkDirty();
            };
            _typePropsPanel.AddChild(speakerEdit);

            _typePropsPanel.AddChild(EditorStyles.MakeLabel("Text:", EditorStyles.FontTiny, EditorStyles.TextSecondary));
            var textEdit = new TextEdit();
            textEdit.CustomMinimumSize = new Vector2(0, 60);
            textEdit.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
            textEdit.Text = data.Props.TryGetValue("text", out var tx) ? tx.ToString() : "";
            textEdit.TextChanged += () =>
            {
                data.Props["text"] = textEdit.Text;
                SaveNodeOverrides();
                MarkDirty();
            };
            _typePropsPanel.AddChild(textEdit);

            _typePropsPanel.AddChild(EditorStyles.MakeLabel("Priority:", EditorStyles.FontTiny, EditorStyles.TextSecondary));
            var priorityPicker = new OptionButton();
            priorityPicker.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
            priorityPicker.AddItem("Low", 0);
            priorityPicker.AddItem("Normal", 1);
            priorityPicker.AddItem("High", 2);
            var currentPri = data.Props.TryGetValue("priority", out var pri) ? pri.ToString() : "Normal";
            priorityPicker.Selected = currentPri == "High" ? 2 : currentPri == "Low" ? 0 : 1;
            priorityPicker.ItemSelected += idx =>
            {
                data.Props["priority"] = idx == 2 ? "High" : idx == 0 ? "Low" : "Normal";
                SaveNodeOverrides();
                MarkDirty();
            };
            _typePropsPanel.AddChild(priorityPicker);
        }

        private void BuildCollisionProps(PlacedObjectData data)
        {
            _typePropsPanel.AddChild(EditorStyles.MakeLabel("Height:", EditorStyles.FontTiny, EditorStyles.TextSecondary));
            var heightSpin = MakeTransformSpinBox(0.5, 20, 0.5);
            heightSpin.Value = data.Scale.Y;
            heightSpin.ValueChanged += v =>
            {
                if (data.Node != null && GodotObject.IsInstanceValid(data.Node))
                {
                    var s = data.Node.Scale;
                    s.Y = (float)v;
                    data.Node.Scale = s;
                    data.Scale = data.Node.Scale;
                    UpdateInspector();
                    SaveNodeOverrides();
                    MarkDirty();
                }
            };
            _typePropsPanel.AddChild(heightSpin);
        }

        // ===== Free camera mode =====
        private bool _freeCam;
        private float _freeCamPitch; // radians, separate from orbit pitch

        private void ToggleFreeCam()
        {
            _freeCam = !_freeCam;
            _autoOrbit = false;
            if (_freeCam)
            {
                // Enter free cam: place camera at current position, use current yaw
                _freeCamPitch = -_cameraPitch; // convert orbit pitch to look-down angle
                _placingLabel.Text = "Free Cam (F to exit, WASD+QE fly, RMB look)";
            }
            else
            {
                // Exit free cam: set orbit target to where camera is looking
                var forward = -_camera.GlobalTransform.Basis.Z;
                _cameraTarget = _camera.GlobalPosition + forward * 15f;
                _cameraDistance = 15f;
                _cameraPitch = 0.8f;
                _placingLabel.Text = "";
            }
        }

        // ===== WASD camera movement =====

        private void ProcessWASD(double delta)
        {
            if (_camera == null || !Visible) return;
            if (!_viewportContainer.HasFocus()) return;

            float speed = 15f * (float)delta;
            if (Input.IsKeyPressed(Key.Shift)) speed *= 2.5f;

            if (_freeCam)
            {
                // Free cam: fly in the direction the camera is looking
                var basis = _camera.GlobalTransform.Basis;
                var forward = -basis.Z;
                var right = basis.X;
                var up = Vector3.Up;

                Vector3 move = Vector3.Zero;
                if (Input.IsKeyPressed(Key.W)) move += forward;
                if (Input.IsKeyPressed(Key.S)) move -= forward;
                if (Input.IsKeyPressed(Key.A)) move -= right;
                if (Input.IsKeyPressed(Key.D)) move += right;
                if (Input.IsKeyPressed(Key.Q)) move -= up;
                if (Input.IsKeyPressed(Key.E)) move += up;

                if (move.LengthSquared() > 0.001f)
                    _camera.GlobalPosition += move.Normalized() * speed;
            }
            else
            {
                // Orbit cam: WASD moves the orbit target on the XZ plane
                float cosY = Mathf.Cos(_cameraYaw);
                float sinY = Mathf.Sin(_cameraYaw);

                Vector3 move = Vector3.Zero;
                if (Input.IsKeyPressed(Key.W)) move += new Vector3(sinY, 0, cosY);
                if (Input.IsKeyPressed(Key.S)) move -= new Vector3(sinY, 0, cosY);
                if (Input.IsKeyPressed(Key.A)) move += new Vector3(cosY, 0, -sinY);
                if (Input.IsKeyPressed(Key.D)) move -= new Vector3(cosY, 0, -sinY);

                if (move.LengthSquared() > 0.001f)
                {
                    _cameraTarget += move.Normalized() * speed;
                    _autoOrbit = false;
                }
            }
        }

        // ===== Selection helpers for placed objects =====

        private bool IsPlacedObject(Node3D node)
        {
            if (_placedObjectsRoot == null || !GodotObject.IsInstanceValid(_placedObjectsRoot))
                return false;

            // Walk up parent chain
            Node current = node;
            while (current != null)
            {
                if (current == _placedObjectsRoot) return true;
                current = current.GetParent();
            }
            return false;
        }

        // ═══════════════════════════════════════════════════════════════
        //  MESH EDITING — Trim / Extend visual geometry per-side
        // ═══════════════════════════════════════════════════════════════

        private static bool HasChildMeshes(Node node)
        {
            if (node is MeshInstance3D mi && mi.Mesh != null) return true;
            foreach (var child in node.GetChildren())
                if (child is Node n && HasChildMeshes(n)) return true;
            return false;
        }

        private void BuildMeshEditSection(Node3D owner)
        {
            _typePropsPanel.AddChild(EditorStyles.MakeSeparator());
            _typePropsPanel.AddChild(EditorStyles.MakeLabel("Mesh Edit", EditorStyles.FontSmall, new Color(0.5f, 0.7f, 1f)));

            // Amount spinner
            var amtRow = new HBoxContainer();
            amtRow.AddThemeConstantOverride("separation", 4);
            amtRow.AddChild(EditorStyles.MakeLabel("Amount:", EditorStyles.FontTiny, EditorStyles.TextSecondary));
            var amtSpin = MakeTransformSpinBox(0.1, 20, 0.25);
            amtSpin.Value = _gridSnap > 0 ? _gridSnap : 0.5;
            amtSpin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            amtRow.AddChild(amtSpin);
            _typePropsPanel.AddChild(amtRow);

            // Trim row
            _typePropsPanel.AddChild(EditorStyles.MakeLabel("Trim side (cut geometry):", EditorStyles.FontTiny, EditorStyles.TextSecondary));
            var trimRow = new HBoxContainer();
            trimRow.AddThemeConstantOverride("separation", 3);

            void AddMeshTrimBtn(string label, int axis, float dir)
            {
                var btn = EditorStyles.MakeButton(label, EditorStyles.FontTiny, new Color(1f, 0.5f, 0.4f));
                btn.CustomMinimumSize = new Vector2(0, 22);
                btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                btn.Pressed += () =>
                {
                    PushUndoSnapshot();
                    float amt = (float)amtSpin.Value;
                    TrimMeshSide(owner, axis, dir, amt);
                    UpdateSelectionHighlight();
                    SaveNodeOverrides();
                    MarkDirty();
                };
                trimRow.AddChild(btn);
            }

            AddMeshTrimBtn("X-", 0, -1f);
            AddMeshTrimBtn("X+", 0, 1f);
            AddMeshTrimBtn("Y+", 1, 1f);
            AddMeshTrimBtn("Z-", 2, -1f);
            AddMeshTrimBtn("Z+", 2, 1f);
            _typePropsPanel.AddChild(trimRow);

            // Extend row
            _typePropsPanel.AddChild(EditorStyles.MakeLabel("Extend side (stretch edge):", EditorStyles.FontTiny, EditorStyles.TextSecondary));
            var extRow = new HBoxContainer();
            extRow.AddThemeConstantOverride("separation", 3);

            void AddMeshExtBtn(string label, int axis, float dir)
            {
                var btn = EditorStyles.MakeButton(label, EditorStyles.FontTiny, new Color(0.4f, 0.8f, 1f));
                btn.CustomMinimumSize = new Vector2(0, 22);
                btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                btn.Pressed += () =>
                {
                    PushUndoSnapshot();
                    float amt = (float)amtSpin.Value;
                    ExtendMeshSide(owner, axis, dir, amt);
                    UpdateSelectionHighlight();
                    SaveNodeOverrides();
                    MarkDirty();
                };
                extRow.AddChild(btn);
            }

            AddMeshExtBtn("X-", 0, -1f);
            AddMeshExtBtn("X+", 0, 1f);
            AddMeshExtBtn("Y+", 1, 1f);
            AddMeshExtBtn("Z-", 2, -1f);
            AddMeshExtBtn("Z+", 2, 1f);
            _typePropsPanel.AddChild(extRow);

            // Fit collision after mesh edit
            var syncBtn = EditorStyles.MakeButton("Sync Collision to Mesh", EditorStyles.FontTiny, new Color(0.3f, 1f, 0.5f));
            syncBtn.CustomMinimumSize = new Vector2(0, 24);
            syncBtn.Pressed += () =>
            {
                var cs = FindChildCollisionShape(owner);
                if (cs != null && cs.Shape is BoxShape3D bx)
                {
                    PushUndoSnapshot();
                    FitCollisionToMesh(owner, cs, bx);
                    RebuildCollisionOverlay();
                    SaveNodeOverrides();
                    MarkDirty();
                    // Refresh the whole inspector to update collision spinboxes
                    UpdateTypePropsInspector();
                }
            };
            _typePropsPanel.AddChild(syncBtn);
        }

        // ===== Trim: clip geometry beyond a plane =====

        private static void TrimMeshSide(Node3D owner, int axis, float dir, float amount)
        {
            // Process all MeshInstance3D children
            var meshes = new List<MeshInstance3D>();
            CollectMeshInstances(owner, meshes);

            foreach (var mi in meshes)
            {
                if (mi.Mesh == null) continue;

                // Compute local-space AABB for this mesh
                var localAabb = mi.Mesh.GetAabb();

                // Clip plane in mesh-local space:
                // dir > 0 means trim the positive side → plane normal points negative, plane at (max - amount)
                // dir < 0 means trim the negative side → plane normal points positive, plane at (min + amount)
                float planePos;
                float planeNormalSign;
                if (dir > 0)
                {
                    float maxVal = GetAxisValue(localAabb.Position + localAabb.Size, axis);
                    planePos = maxVal - amount;
                    planeNormalSign = -1f; // keep everything below planePos
                }
                else
                {
                    float minVal = GetAxisValue(localAabb.Position, axis);
                    planePos = minVal + amount;
                    planeNormalSign = 1f; // keep everything above planePos
                }

                var newMesh = ClipMeshByPlane(mi.Mesh, axis, planePos, planeNormalSign);
                if (newMesh != null)
                    mi.Mesh = newMesh;
            }
        }

        // ===== Extend: push edge vertices outward =====

        private static void ExtendMeshSide(Node3D owner, int axis, float dir, float amount)
        {
            var meshes = new List<MeshInstance3D>();
            CollectMeshInstances(owner, meshes);

            foreach (var mi in meshes)
            {
                if (mi.Mesh == null) continue;

                var localAabb = mi.Mesh.GetAabb();
                float edgeThreshold;
                if (dir > 0)
                {
                    float maxVal = GetAxisValue(localAabb.Position + localAabb.Size, axis);
                    edgeThreshold = maxVal - amount * 0.5f; // vertices near the max edge
                }
                else
                {
                    float minVal = GetAxisValue(localAabb.Position, axis);
                    edgeThreshold = minVal + amount * 0.5f; // vertices near the min edge
                }

                var newMesh = StretchMeshEdge(mi.Mesh, axis, dir, amount, edgeThreshold);
                if (newMesh != null)
                    mi.Mesh = newMesh;
            }
        }

        // ===== Core mesh operations =====

        private static void CollectMeshInstances(Node node, List<MeshInstance3D> list)
        {
            if (node is MeshInstance3D mi && mi.Mesh != null)
                list.Add(mi);
            foreach (var child in node.GetChildren())
                if (child is Node n) CollectMeshInstances(n, list);
        }

        private static float GetAxisValue(Vector3 v, int axis) =>
            axis == 0 ? v.X : axis == 1 ? v.Y : v.Z;

        private static Vector3 SetAxisValue(Vector3 v, int axis, float val)
        {
            if (axis == 0) v.X = val;
            else if (axis == 1) v.Y = val;
            else v.Z = val;
            return v;
        }

        /// <summary>
        /// Clip a mesh by a plane. Keeps geometry on the side indicated by planeNormalSign.
        /// planeNormalSign > 0: keep vertices where axis > planePos
        /// planeNormalSign < 0: keep vertices where axis < planePos
        /// </summary>
        private static ArrayMesh ClipMeshByPlane(Mesh sourceMesh, int axis, float planePos, float planeNormalSign)
        {
            var result = new ArrayMesh();
            int surfCount = sourceMesh.GetSurfaceCount();
            bool anyGeometry = false;

            for (int s = 0; s < surfCount; s++)
            {
                var arrays = sourceMesh.SurfaceGetArrays(s);
                if (arrays == null || arrays.Count == 0) continue;

                var verts = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                if (verts == null || verts.Length == 0) continue;

                var normals = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
                var uvs = arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
                var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();

                bool hasNormals = normals != null && normals.Length == verts.Length;
                bool hasUVs = uvs != null && uvs.Length == verts.Length;
                bool hasIndices = indices != null && indices.Length >= 3;

                // Build triangle list
                var newVerts = new List<Vector3>();
                var newNormals = new List<Vector3>();
                var newUVs = new List<Vector2>();

                int triCount;
                if (hasIndices)
                    triCount = indices.Length / 3;
                else
                    triCount = verts.Length / 3;

                for (int t = 0; t < triCount; t++)
                {
                    int i0, i1, i2;
                    if (hasIndices)
                    {
                        i0 = indices[t * 3];
                        i1 = indices[t * 3 + 1];
                        i2 = indices[t * 3 + 2];
                    }
                    else
                    {
                        i0 = t * 3;
                        i1 = t * 3 + 1;
                        i2 = t * 3 + 2;
                    }

                    if (i0 >= verts.Length || i1 >= verts.Length || i2 >= verts.Length) continue;

                    var v0 = verts[i0]; var v1 = verts[i1]; var v2 = verts[i2];
                    var n0 = hasNormals ? normals[i0] : Vector3.Up;
                    var n1 = hasNormals ? normals[i1] : Vector3.Up;
                    var n2 = hasNormals ? normals[i2] : Vector3.Up;
                    var u0 = hasUVs ? uvs[i0] : Vector2.Zero;
                    var u1 = hasUVs ? uvs[i1] : Vector2.Zero;
                    var u2 = hasUVs ? uvs[i2] : Vector2.Zero;

                    // Classify vertices: inside = on the kept side
                    bool in0 = IsInsidePlane(v0, axis, planePos, planeNormalSign);
                    bool in1 = IsInsidePlane(v1, axis, planePos, planeNormalSign);
                    bool in2 = IsInsidePlane(v2, axis, planePos, planeNormalSign);

                    int insideCount = (in0 ? 1 : 0) + (in1 ? 1 : 0) + (in2 ? 1 : 0);

                    if (insideCount == 3)
                    {
                        // All inside — keep triangle
                        AddTri(newVerts, newNormals, newUVs, v0, v1, v2, n0, n1, n2, u0, u1, u2);
                    }
                    else if (insideCount == 0)
                    {
                        // All outside — discard
                        continue;
                    }
                    else if (insideCount == 2)
                    {
                        // Two inside, one outside — clip to 2 triangles (quad)
                        // Rotate so the outside vertex is v2/n2/u2
                        if (!in0) { Swap(ref v0, ref v2); Swap(ref n0, ref n2); Swap(ref u0, ref u2); Swap(ref v0, ref v1); Swap(ref n0, ref n1); Swap(ref u0, ref u1); }
                        else if (!in1) { Swap(ref v1, ref v2); Swap(ref n1, ref n2); Swap(ref u1, ref u2); Swap(ref v0, ref v1); Swap(ref n0, ref n1); Swap(ref u0, ref u1); }
                        // v0, v1 inside; v2 outside
                        float t02 = PlaneIntersectT(v0, v2, axis, planePos);
                        float t12 = PlaneIntersectT(v1, v2, axis, planePos);
                        var c02 = Lerp(v0, v2, t02); var cn02 = Lerp(n0, n2, t02).Normalized(); var cu02 = Lerp(u0, u2, t02);
                        var c12 = Lerp(v1, v2, t12); var cn12 = Lerp(n1, n2, t12).Normalized(); var cu12 = Lerp(u1, u2, t12);
                        // Snap clipped verts exactly to the plane
                        c02 = SetAxisValue(c02, axis, planePos);
                        c12 = SetAxisValue(c12, axis, planePos);
                        AddTri(newVerts, newNormals, newUVs, v0, v1, c02, n0, n1, cn02, u0, u1, cu02);
                        AddTri(newVerts, newNormals, newUVs, v1, c12, c02, n1, cn12, cn02, u1, cu12, cu02);
                    }
                    else
                    {
                        // One inside, two outside — clip to 1 triangle
                        // Rotate so the inside vertex is v0
                        if (in1) { Swap(ref v0, ref v1); Swap(ref n0, ref n1); Swap(ref u0, ref u1); Swap(ref v1, ref v2); Swap(ref n1, ref n2); Swap(ref u1, ref u2); }
                        else if (in2) { Swap(ref v0, ref v2); Swap(ref n0, ref n2); Swap(ref u0, ref u2); Swap(ref v1, ref v2); Swap(ref n1, ref n2); Swap(ref u1, ref u2); }
                        // v0 inside; v1, v2 outside
                        float t01 = PlaneIntersectT(v0, v1, axis, planePos);
                        float t02 = PlaneIntersectT(v0, v2, axis, planePos);
                        var c01 = Lerp(v0, v1, t01); var cn01 = Lerp(n0, n1, t01).Normalized(); var cu01 = Lerp(u0, u1, t01);
                        var c02 = Lerp(v0, v2, t02); var cn02 = Lerp(n0, n2, t02).Normalized(); var cu02 = Lerp(u0, u2, t02);
                        c01 = SetAxisValue(c01, axis, planePos);
                        c02 = SetAxisValue(c02, axis, planePos);
                        AddTri(newVerts, newNormals, newUVs, v0, c01, c02, n0, cn01, cn02, u0, cu01, cu02);
                    }
                }

                if (newVerts.Count < 3) continue;
                anyGeometry = true;

                // Build new surface arrays
                var newArrays = new Godot.Collections.Array();
                newArrays.Resize((int)Mesh.ArrayType.Max);
                newArrays[(int)Mesh.ArrayType.Vertex] = newVerts.ToArray();
                if (hasNormals) newArrays[(int)Mesh.ArrayType.Normal] = newNormals.ToArray();
                if (hasUVs) newArrays[(int)Mesh.ArrayType.TexUV] = newUVs.ToArray();

                result.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, newArrays);

                // Preserve material
                var mat = sourceMesh.SurfaceGetMaterial(s);
                if (mat != null)
                    result.SurfaceSetMaterial(result.GetSurfaceCount() - 1, mat);
            }

            return anyGeometry ? result : null;
        }

        /// <summary>
        /// Stretch vertices on one edge of the mesh outward.
        /// </summary>
        private static ArrayMesh StretchMeshEdge(Mesh sourceMesh, int axis, float dir, float amount, float edgeThreshold)
        {
            var result = new ArrayMesh();
            int surfCount = sourceMesh.GetSurfaceCount();

            for (int s = 0; s < surfCount; s++)
            {
                var arrays = sourceMesh.SurfaceGetArrays(s);
                if (arrays == null || arrays.Count == 0) continue;

                var verts = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                if (verts == null || verts.Length == 0) continue;

                var modified = new Vector3[verts.Length];
                for (int i = 0; i < verts.Length; i++)
                {
                    var v = verts[i];
                    float axisVal = GetAxisValue(v, axis);

                    // Check if this vertex is at the edge we want to extend
                    bool atEdge;
                    if (dir > 0)
                        atEdge = axisVal >= edgeThreshold;
                    else
                        atEdge = axisVal <= edgeThreshold;

                    if (atEdge)
                        v = SetAxisValue(v, axis, axisVal + amount * dir);

                    modified[i] = v;
                }

                // Clone arrays with modified vertices
                var newArrays = new Godot.Collections.Array();
                newArrays.Resize((int)Mesh.ArrayType.Max);
                for (int a = 0; a < (int)Mesh.ArrayType.Max; a++)
                    newArrays[a] = arrays[a];
                newArrays[(int)Mesh.ArrayType.Vertex] = modified;

                result.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, newArrays);

                var mat = sourceMesh.SurfaceGetMaterial(s);
                if (mat != null)
                    result.SurfaceSetMaterial(result.GetSurfaceCount() - 1, mat);
            }

            return result;
        }

        // ===== Clip helpers =====

        private static bool IsInsidePlane(Vector3 v, int axis, float planePos, float normalSign)
        {
            float val = GetAxisValue(v, axis);
            return normalSign > 0 ? val >= planePos : val <= planePos;
        }

        private static float PlaneIntersectT(Vector3 a, Vector3 b, int axis, float planePos)
        {
            float aVal = GetAxisValue(a, axis);
            float bVal = GetAxisValue(b, axis);
            float denom = bVal - aVal;
            if (Mathf.Abs(denom) < 1e-8f) return 0.5f;
            return (planePos - aVal) / denom;
        }

        private static void AddTri(
            List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs,
            Vector3 v0, Vector3 v1, Vector3 v2,
            Vector3 n0, Vector3 n1, Vector3 n2,
            Vector2 u0, Vector2 u1, Vector2 u2)
        {
            verts.Add(v0); verts.Add(v1); verts.Add(v2);
            normals.Add(n0); normals.Add(n1); normals.Add(n2);
            uvs.Add(u0); uvs.Add(u1); uvs.Add(u2);
        }

        private static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a + (b - a) * t;
        private static Vector2 Lerp(Vector2 a, Vector2 b, float t) => a + (b - a) * t;
        private static void Swap<T>(ref T a, ref T b) { var tmp = a; a = b; b = tmp; }
    }
}
