using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    public enum EditorToolMode
    {
        Select,
        CellPaint,
        HeightPaint,
        AssetPlace,
        LightPlace,
        FXPlace,
        Erase,
        TexturePaint,
        MaterialPaint
    }

    /// <summary>
    /// Handles all tool modes for the level editor:
    /// Select, CellPaint, HeightPaint, AssetPlace, LightPlace, FXPlace, Erase.
    /// Manages ghost preview, raycasting, and placement logic.
    /// </summary>
    public partial class LevelEditorTools : Node3D
    {
        public LevelEditorScene Editor { get; set; }

        public EditorToolMode CurrentMode { get; private set; } = EditorToolMode.Select;
        public string PaintCellType { get; set; } = "Wall";
        public float HeightPaintValue { get; set; } = 0.5f;
        public string PlaceAssetPath { get; set; } = "";
        public string PlaceLightType { get; set; } = "Omni";
        public string PlaceFXType { get; set; } = "Fire";
        public float PlaceRotationY { get; set; }
        public bool GridSnap { get; set; } = true;
        public string PaintTextureId { get; set; } = "ground";
        public int TextureBrushSize { get; set; } = 1;
        public MaterialOverrideData PaintMaterialOverride { get; set; }
        public string PlaceAssemblyId { get; set; } = "";

        // Selection
        public Node3D SelectedNode { get; private set; }
        private LevelEditorSelection _selection;

        // Ghost preview
        private Node3D _ghostPreview;
        private Vector2I _lastGridPos = new(-1, -1);
        private bool _painting;
        private Vector2? _boxSelectStart;

        // Placed objects tracking
        private readonly List<Node3D> _placedAssets = new();
        private readonly List<Light3D> _placedLights = new();
        private readonly List<Node3D> _placedFX = new();

        public IReadOnlyList<Node3D> PlacedAssets => _placedAssets;
        public IReadOnlyList<Light3D> PlacedLights => _placedLights;
        public IReadOnlyList<Node3D> PlacedFX => _placedFX;
        public LevelEditorSelection Selection => _selection;

        public void InitSelection(LevelEditorSelection selection)
        {
            _selection = selection;
        }

        public void TrackPlacedAsset(Node3D node)
        {
            _placedAssets.Add(node);
        }

        public void ClearTrackedObjects()
        {
            _placedAssets.Clear();
            _placedLights.Clear();
            _placedFX.Clear();
            _textureOverlays.Clear();
        }

        public void SetMode(EditorToolMode mode)
        {
            CurrentMode = mode;
            ClearGhost();
            ClearSelection();
            // Clear assembly ghost
            Editor?.GetNodeOrNull<LevelEditorAssembly>("LevelEditorAssembly")?.ClearGhost();
            GD.Print($"[Editor] Tool mode: {mode}");
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (Editor?.Grid == null) return;

            // Tool hotkeys
            if (@event is InputEventKey key && key.Pressed)
            {
                // Ctrl+D: duplicate selected
                if (key.CtrlPressed && key.Keycode == Key.D)
                {
                    DuplicateSelected();
                    GetViewport().SetInputAsHandled();
                    return;
                }

                if (!key.CtrlPressed)
                {
                    switch (key.Keycode)
                    {
                        case Key.Q: SetMode(EditorToolMode.Select); GetViewport().SetInputAsHandled(); return;
                        case Key.W when CurrentMode != EditorToolMode.CellPaint:
                            SetMode(EditorToolMode.CellPaint); GetViewport().SetInputAsHandled(); return;
                        case Key.E: SetMode(EditorToolMode.HeightPaint); GetViewport().SetInputAsHandled(); return;
                        case Key.R when CurrentMode == EditorToolMode.AssetPlace:
                            PlaceRotationY += 45f;
                            if (PlaceRotationY >= 360f) PlaceRotationY -= 360f;
                            UpdateGhostRotation();
                            GetViewport().SetInputAsHandled();
                            return;
                        case Key.R when CurrentMode != EditorToolMode.AssetPlace:
                            SetMode(EditorToolMode.AssetPlace); GetViewport().SetInputAsHandled(); return;
                        case Key.T: SetMode(EditorToolMode.LightPlace); GetViewport().SetInputAsHandled(); return;
                        case Key.Y: SetMode(EditorToolMode.FXPlace); GetViewport().SetInputAsHandled(); return;
                        case Key.X: SetMode(EditorToolMode.Erase); GetViewport().SetInputAsHandled(); return;
                        case Key.C: SetMode(EditorToolMode.TexturePaint); GetViewport().SetInputAsHandled(); return;
                        case Key.V: SetMode(EditorToolMode.MaterialPaint); GetViewport().SetInputAsHandled(); return;
                        case Key.G:
                            GridSnap = !GridSnap;
                            GetViewport().SetInputAsHandled();
                            return;
                        case Key.Delete:
                            DeleteSelected();
                            GetViewport().SetInputAsHandled();
                            return;
                    }
                }
            }

            // Mouse input
            if (@event is InputEventMouseButton mb)
            {
                if (mb.ButtonIndex == MouseButton.Left)
                {
                    if (mb.Pressed)
                    {
                        // Start box select in Select mode when no modifier held
                        if (CurrentMode == EditorToolMode.Select && _selection != null &&
                            !Input.IsKeyPressed(Key.Shift) && !Input.IsKeyPressed(Key.Ctrl))
                        {
                            _boxSelectStart = mb.Position;
                        }

                        _painting = true;
                        HandleClick(mb.Position);
                    }
                    else
                    {
                        // End box select if drag was large enough
                        if (CurrentMode == EditorToolMode.Select && _selection != null && _boxSelectStart.HasValue)
                        {
                            if ((mb.Position - _boxSelectStart.Value).Length() > 10f)
                            {
                                _selection.EndBoxSelect(mb.Position, _placedAssets);
                                SelectedNode = _selection.Count == 1 ? _selection.Selected[0] : null;
                            }
                            _boxSelectStart = null;
                        }

                        if (_painting)
                        {
                            _painting = false;
                            if (CurrentMode == EditorToolMode.CellPaint || CurrentMode == EditorToolMode.HeightPaint
                                || CurrentMode == EditorToolMode.TexturePaint)
                                Editor.PushUndoState();
                        }
                    }
                    GetViewport().SetInputAsHandled();
                }
                else if (mb.ButtonIndex == MouseButton.Right && mb.Pressed)
                {
                    // Right-click erases in paint modes
                    if (CurrentMode == EditorToolMode.CellPaint)
                    {
                        var gridPos = RaycastToGrid(mb.Position);
                        if (gridPos.HasValue)
                            EraseCellAt(gridPos.Value);
                    }
                    else if (CurrentMode == EditorToolMode.HeightPaint)
                    {
                        var gridPos = RaycastToGrid(mb.Position);
                        if (gridPos.HasValue)
                            LowerHeightAt(gridPos.Value);
                    }
                    else if (CurrentMode == EditorToolMode.TexturePaint)
                    {
                        var gridPos = RaycastToGrid(mb.Position);
                        if (gridPos.HasValue)
                            ClearTextureAt(gridPos.Value);
                    }
                    GetViewport().SetInputAsHandled();
                }
            }
            else if (@event is InputEventMouseMotion mm)
            {
                // Box select drag
                if (CurrentMode == EditorToolMode.Select && _selection != null &&
                    _boxSelectStart.HasValue && _painting)
                {
                    if (!_selection.IsBoxSelecting && (mm.Position - _boxSelectStart.Value).Length() > 10f)
                    {
                        var ui = Editor?.GetNodeOrNull<LevelEditorUI>("LevelEditorUI");
                        if (ui != null)
                            _selection.StartBoxSelect(_boxSelectStart.Value, ui);
                    }
                    if (_selection.IsBoxSelecting)
                        _selection.UpdateBoxSelect(mm.Position);
                }

                var gridPos = RaycastToGrid(mm.Position);
                if (gridPos.HasValue && gridPos.Value != _lastGridPos)
                {
                    _lastGridPos = gridPos.Value;
                    UpdateGhostPosition(gridPos.Value);

                    if (_painting)
                        HandlePaintStroke(gridPos.Value);
                }
            }
        }

        private void HandleClick(Vector2 screenPos)
        {
            var gridPos = RaycastToGrid(screenPos);

            switch (CurrentMode)
            {
                case EditorToolMode.Select:
                    SelectAtScreenPos(screenPos);
                    break;
                case EditorToolMode.CellPaint:
                    if (gridPos.HasValue) PaintCellAt(gridPos.Value);
                    break;
                case EditorToolMode.HeightPaint:
                    if (gridPos.HasValue) RaiseHeightAt(gridPos.Value);
                    break;
                case EditorToolMode.AssetPlace:
                    if (gridPos.HasValue) PlaceAssetAt(gridPos.Value);
                    break;
                case EditorToolMode.LightPlace:
                    if (gridPos.HasValue) PlaceLightAt(gridPos.Value);
                    break;
                case EditorToolMode.FXPlace:
                    if (gridPos.HasValue) PlaceFXAt(gridPos.Value);
                    break;
                case EditorToolMode.Erase:
                    EraseAtScreenPos(screenPos);
                    break;
                case EditorToolMode.TexturePaint:
                    if (gridPos.HasValue) PaintTextureAt(gridPos.Value);
                    break;
                case EditorToolMode.MaterialPaint:
                    MaterialPaintAtScreenPos(screenPos);
                    break;
            }
        }

        private void HandlePaintStroke(Vector2I gridPos)
        {
            if (CurrentMode == EditorToolMode.CellPaint)
                PaintCellAt(gridPos);
            else if (CurrentMode == EditorToolMode.HeightPaint)
                RaiseHeightAt(gridPos);
            else if (CurrentMode == EditorToolMode.TexturePaint)
                PaintTextureAt(gridPos);
        }

        // ── Cell Paint ──

        private void PaintCellAt(Vector2I pos)
        {
            var grid = Editor.Grid;
            if (!grid.InBounds(pos)) return;

            // Don't overwrite entry/exit
            var existing = grid.GetCell(pos);
            if (existing == VineCellType.Entry || existing == VineCellType.Exit) return;

            switch (PaintCellType)
            {
                case "Wall": grid.SetWall(pos.X, pos.Y); break;
                case "Elevated": grid.SetElevated(pos.X, pos.Y); break;
                case "Channel": grid.SetChannel(pos.X, pos.Y); break;
                case "DataStream": grid.SetDataStream(pos.X, pos.Y); break;
                case "Entry":
                    grid.SetEntryRegion(pos.X, pos.Y, pos.X, pos.Y);
                    break;
                case "Exit":
                    grid.SetExit(pos.X, pos.Y);
                    break;
            }

            Editor.RefreshTerrain();
        }

        private void EraseCellAt(Vector2I pos)
        {
            var grid = Editor.Grid;
            if (!grid.InBounds(pos)) return;

            var existing = grid.GetCell(pos);
            if (existing == VineCellType.Entry || existing == VineCellType.Exit) return;
            if (existing == VineCellType.Empty) return;

            grid.ClearCell(pos.X, pos.Y);
            Editor.RefreshTerrain();
        }

        // ── Height Paint ──

        private void RaiseHeightAt(Vector2I pos)
        {
            var grid = Editor.Grid;
            if (!grid.InBounds(pos)) return;
            grid.AdjustHeight(pos.X, pos.Y, HeightPaintValue);
            Editor.RefreshTerrain();
        }

        private void LowerHeightAt(Vector2I pos)
        {
            var grid = Editor.Grid;
            if (!grid.InBounds(pos)) return;
            grid.AdjustHeight(pos.X, pos.Y, -HeightPaintValue);
            Editor.RefreshTerrain();
        }

        // ── Asset Place ──

        private void PlaceAssetAt(Vector2I gridPos)
        {
            // Check if we're placing an assembly instead
            if (!string.IsNullOrEmpty(PlaceAssemblyId))
            {
                var assembly = Editor?.GetNodeOrNull<LevelEditorAssembly>("LevelEditorAssembly");
                if (assembly != null)
                {
                    var worldPos = Editor.Grid.GridToWorld(gridPos);
                    assembly.PlaceAssembly(PlaceAssemblyId, worldPos);
                }
                return;
            }

            if (string.IsNullOrEmpty(PlaceAssetPath)) return;

            var model = AssetLibrary.InstantiateNormalized(PlaceAssetPath);
            if (model == null) return;

            var wPos = Editor.Grid.GridToWorld(gridPos);
            model.Position = wPos;
            model.RotationDegrees = new Vector3(0, PlaceRotationY, 0);

            // Apply material override if one is set, otherwise default theme
            if (PaintMaterialOverride != null)
                LevelEditorMaterialPainter.Apply(model, PaintMaterialOverride);
            else
                PlanetTheme.Current.ApplyToNode(model);

            model.SetMeta("asset_path", PlaceAssetPath);
            Editor.Grid.AddChild(model);
            _placedAssets.Add(model);

            // Record in level data
            Editor.CurrentLevel.Assets.Add(new AssetPlacement
            {
                Id = $"asset_{_placedAssets.Count}",
                Path = PlaceAssetPath,
                PosX = wPos.X, PosY = wPos.Y, PosZ = wPos.Z,
                RotY = PlaceRotationY,
                ScaleX = model.Scale.X, ScaleY = model.Scale.Y, ScaleZ = model.Scale.Z,
                MaterialOverride = PaintMaterialOverride
            });

            Editor.PushUndoState();
        }

        // ── Light Place ──

        private void PlaceLightAt(Vector2I gridPos)
        {
            var worldPos = Editor.Grid.GridToWorld(gridPos) + new Vector3(0, 3f, 0);

            Light3D light;
            if (PlaceLightType == "Spot")
            {
                var spot = new SpotLight3D();
                spot.SpotRange = 10f;
                spot.RotationDegrees = new Vector3(-90, 0, 0);
                light = spot;
            }
            else
            {
                var omni = new OmniLight3D();
                omni.OmniRange = 10f;
                light = omni;
            }

            light.Position = worldPos;
            light.LightColor = new Color(1f, 0.9f, 0.7f);
            light.LightEnergy = 1.5f;
            Editor.Grid.AddChild(light);
            _placedLights.Add(light);

            // Add gizmo sphere for visibility
            var gizmo = new MeshInstance3D();
            var sphere = new SphereMesh();
            sphere.Radius = 0.3f;
            sphere.Height = 0.6f;
            gizmo.Mesh = sphere;
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = light.LightColor;
            mat.EmissionEnabled = true;
            mat.Emission = light.LightColor;
            mat.EmissionEnergyMultiplier = 2f;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            gizmo.MaterialOverride = mat;
            light.AddChild(gizmo);

            // Record
            Editor.CurrentLevel.Lights.Add(new LightPlacement
            {
                Id = $"light_{_placedLights.Count}",
                LightType = PlaceLightType,
                PosX = worldPos.X, PosY = worldPos.Y, PosZ = worldPos.Z,
                ColorR = 1f, ColorG = 0.9f, ColorB = 0.7f,
                Energy = 1.5f, Range = 10f
            });

            Editor.PushUndoState();
        }

        // ── FX Place ──

        private void PlaceFXAt(Vector2I gridPos)
        {
            var worldPos = Editor.Grid.GridToWorld(gridPos) + new Vector3(0, 0.5f, 0);

            var fxNode = EnvironmentFXFactory.Create(PlaceFXType, worldPos, 2f, 1f,
                new Color(1f, 0.5f, 0.1f));
            if (fxNode == null) return;

            Editor.Grid.AddChild(fxNode);
            _placedFX.Add(fxNode);

            Editor.CurrentLevel.EnvironmentFX.Add(new EnvironmentFXData
            {
                Id = $"fx_{_placedFX.Count}",
                FXType = PlaceFXType,
                PosX = worldPos.X, PosY = worldPos.Y, PosZ = worldPos.Z,
                Radius = 2f, Intensity = 1f,
                ColorR = 1f, ColorG = 0.5f, ColorB = 0.1f
            });

            Editor.PushUndoState();
        }

        // ── Texture Paint ──

        // Stores per-cell texture overlay meshes for visual feedback
        private readonly Dictionary<Vector2I, MeshInstance3D> _textureOverlays = new();

        private void PaintTextureAt(Vector2I center)
        {
            var grid = Editor.Grid;
            int half = TextureBrushSize / 2;

            for (int dx = -half; dx <= half; dx++)
            for (int dz = -half; dz <= half; dz++)
            {
                var pos = new Vector2I(center.X + dx, center.Y + dz);
                if (!grid.InBounds(pos)) continue;

                ApplyTextureOverlay(pos, PaintTextureId);

                // Store in level data
                Editor.CurrentLevel.SetCellTexture(pos.X, pos.Y, PaintTextureId);
            }
        }

        private void ClearTextureAt(Vector2I center)
        {
            var grid = Editor.Grid;
            int half = TextureBrushSize / 2;

            for (int dx = -half; dx <= half; dx++)
            for (int dz = -half; dz <= half; dz++)
            {
                var pos = new Vector2I(center.X + dx, center.Y + dz);
                if (!grid.InBounds(pos)) continue;

                RemoveTextureOverlay(pos);
                Editor.CurrentLevel.SetCellTexture(pos.X, pos.Y, null);
            }
        }

        private void ApplyTextureOverlay(Vector2I pos, string textureId)
        {
            RemoveTextureOverlay(pos);

            var color = GetTextureColor(textureId);
            bool emissive = textureId.StartsWith("emit_");

            float cs = Constants.VINE_CELL_SIZE;
            var mesh = new MeshInstance3D();
            var box = new BoxMesh();
            box.Size = new Vector3(cs * 0.95f, 0.06f, cs * 0.95f);
            mesh.Mesh = box;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(color.R, color.G, color.B, emissive ? 0.7f : 0.5f);
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            if (emissive)
            {
                mat.EmissionEnabled = true;
                mat.Emission = color;
                mat.EmissionEnergyMultiplier = 1.5f;
            }
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mesh.MaterialOverride = mat;

            var worldPos = Editor.Grid.GridToWorld(pos);
            mesh.Position = worldPos + new Vector3(0, 0.04f, 0);
            Editor.Grid.AddChild(mesh);
            _textureOverlays[pos] = mesh;
        }

        private void RemoveTextureOverlay(Vector2I pos)
        {
            if (_textureOverlays.TryGetValue(pos, out var existing))
            {
                existing.QueueFree();
                _textureOverlays.Remove(pos);
            }
        }

        private static Color GetTextureColor(string id)
        {
            return id switch
            {
                "ground" => new Color(0.02f, 0.02f, 0.04f),
                "grid_cyan" => TronTheme.GridCyan,
                "dark_metal" => new Color(0.04f, 0.04f, 0.06f),
                "rust" => new Color(0.35f, 0.18f, 0.08f),
                "concrete" => new Color(0.25f, 0.24f, 0.22f),
                "sand" => new Color(0.45f, 0.38f, 0.25f),
                "grime" => new Color(0.12f, 0.1f, 0.06f),
                "acid" => new Color(0.2f, 0.6f, 0.1f),
                "lava" => new Color(0.8f, 0.25f, 0.02f),
                "ice" => new Color(0.4f, 0.65f, 0.85f),
                "toxic" => new Color(0.4f, 0.1f, 0.5f),
                "holo" => new Color(0f, 0.85f, 0.95f),
                "emit_cyan" => new Color(0f, 0.85f, 0.95f),
                "emit_red" => new Color(0.9f, 0.15f, 0.1f),
                "emit_green" => new Color(0.1f, 0.9f, 0.2f),
                "emit_gold" => new Color(0.9f, 0.7f, 0.2f),
                "emit_magenta" => new Color(0.8f, 0.1f, 0.6f),
                _ => new Color(0.3f, 0.3f, 0.3f)
            };
        }

        // ── Selection ──

        private void SelectAtScreenPos(Vector2 screenPos)
        {
            // Raycast for 3D objects
            var camera = GetViewport().GetCamera3D();
            if (camera == null) return;

            var from = camera.ProjectRayOrigin(screenPos);
            var to = from + camera.ProjectRayNormal(screenPos) * 100f;

            var spaceState = GetWorld3D().DirectSpaceState;
            var query = PhysicsRayQueryParameters3D.Create(from, to);
            var result = spaceState.IntersectRay(query);

            Node3D hit = null;
            if (result.Count > 0)
            {
                var collider = result["collider"].As<Node>();
                hit = FindPlacedParent(collider);
            }

            if (_selection != null)
            {
                if (Input.IsKeyPressed(Key.Shift))
                    _selection.AddToSelection(hit);
                else if (Input.IsKeyPressed(Key.Ctrl))
                    _selection.ToggleSelection(hit);
                else
                    _selection.SelectSingle(hit);

                // Keep SelectedNode synced to last single selection
                SelectedNode = _selection.Count == 1 ? _selection.Selected[0] : hit;
            }
            else
            {
                ClearSelection();
                SelectedNode = hit;
            }
        }

        private Node3D FindPlacedParent(Node node)
        {
            while (node != null)
            {
                if (node is Node3D n3d)
                {
                    if (_placedAssets.Contains(n3d)) return n3d;
                    if (node is Light3D l && _placedLights.Contains(l)) return n3d;
                    if (_placedFX.Contains(n3d)) return n3d;
                }
                node = node.GetParent();
            }
            return null;
        }

        public void DeleteSelected()
        {
            if (_selection != null && _selection.Count > 0)
            {
                // Delete all selected nodes
                foreach (var node in new List<Node3D>(_selection.Selected))
                {
                    RemoveTrackedNode(node);
                    node.QueueFree();
                }
                _selection.ClearAll();
                SelectedNode = null;
                Editor.PushUndoState();
                return;
            }

            if (SelectedNode == null) return;

            RemoveTrackedNode(SelectedNode);
            SelectedNode.QueueFree();
            SelectedNode = null;
            Editor.PushUndoState();
        }

        private void RemoveTrackedNode(Node3D node)
        {
            if (node is Light3D light)
                _placedLights.Remove(light);
            else if (_placedFX.Contains(node))
                _placedFX.Remove(node);
            else
                _placedAssets.Remove(node);

            // Remove from level data
            RemoveAssetPlacement(node);
        }

        private void RemoveAssetPlacement(Node3D node)
        {
            if (Editor?.CurrentLevel == null) return;
            Editor.CurrentLevel.Assets.RemoveAll(a =>
                Mathf.Abs(a.PosX - node.Position.X) < 0.01f &&
                Mathf.Abs(a.PosY - node.Position.Y) < 0.01f &&
                Mathf.Abs(a.PosZ - node.Position.Z) < 0.01f);
        }

        private void ClearSelection()
        {
            SelectedNode = null;
            _selection?.ClearAll();
        }

        // ── Material Paint ──

        private void MaterialPaintAtScreenPos(Vector2 screenPos)
        {
            if (PaintMaterialOverride == null) return;

            // Raycast to find target
            var camera = GetViewport().GetCamera3D();
            if (camera == null) return;

            var from = camera.ProjectRayOrigin(screenPos);
            var to = from + camera.ProjectRayNormal(screenPos) * 100f;

            var spaceState = GetWorld3D().DirectSpaceState;
            var query = PhysicsRayQueryParameters3D.Create(from, to);
            var result = spaceState.IntersectRay(query);

            if (result.Count == 0) return;

            var collider = result["collider"].As<Node>();
            var node = FindPlacedParent(collider);
            if (node == null) return;

            // Apply to hit node and all selected
            var targets = new List<Node3D>();
            if (_selection != null && _selection.Contains(node))
            {
                targets.AddRange(_selection.Selected);
            }
            else
            {
                targets.Add(node);
            }

            foreach (var target in targets)
            {
                LevelEditorMaterialPainter.Apply(target, PaintMaterialOverride);
                UpdateAssetPlacementMaterial(target, PaintMaterialOverride);
            }

            Editor.PushUndoState();
        }

        private void UpdateAssetPlacementMaterial(Node3D node, MaterialOverrideData data)
        {
            if (Editor?.CurrentLevel == null) return;
            foreach (var asset in Editor.CurrentLevel.Assets)
            {
                if (Mathf.Abs(asset.PosX - node.Position.X) < 0.01f &&
                    Mathf.Abs(asset.PosZ - node.Position.Z) < 0.01f)
                {
                    asset.MaterialOverride = data;
                    return;
                }
            }
        }

        // ── Duplicate ──

        private void DuplicateSelected()
        {
            var targets = new List<Node3D>();
            if (_selection != null && _selection.Count > 0)
                targets.AddRange(_selection.Selected);
            else if (SelectedNode != null)
                targets.Add(SelectedNode);

            if (targets.Count == 0) return;

            var offset = new Vector3(Constants.VINE_CELL_SIZE, 0, 0);

            foreach (var original in targets)
            {
                // Find asset placement data
                AssetPlacement srcPlacement = null;
                foreach (var a in Editor.CurrentLevel.Assets)
                {
                    if (Mathf.Abs(a.PosX - original.Position.X) < 0.01f &&
                        Mathf.Abs(a.PosZ - original.Position.Z) < 0.01f)
                    {
                        srcPlacement = a;
                        break;
                    }
                }
                if (srcPlacement == null) continue;

                var model = AssetLibrary.InstantiateNormalized(srcPlacement.Path);
                if (model == null) continue;

                var newPos = original.Position + offset;
                model.Position = newPos;
                model.RotationDegrees = original.RotationDegrees;
                model.Scale = original.Scale;

                if (srcPlacement.MaterialOverride != null)
                    LevelEditorMaterialPainter.Apply(model, srcPlacement.MaterialOverride);
                else
                    PlanetTheme.Current.ApplyToNode(model);

                Editor.Grid.AddChild(model);
                _placedAssets.Add(model);

                Editor.CurrentLevel.Assets.Add(new AssetPlacement
                {
                    Id = $"asset_{_placedAssets.Count}",
                    Path = srcPlacement.Path,
                    PosX = newPos.X, PosY = newPos.Y, PosZ = newPos.Z,
                    RotX = original.RotationDegrees.X,
                    RotY = original.RotationDegrees.Y,
                    RotZ = original.RotationDegrees.Z,
                    ScaleX = model.Scale.X, ScaleY = model.Scale.Y, ScaleZ = model.Scale.Z,
                    MaterialOverride = srcPlacement.MaterialOverride
                });

                model.SetMeta("asset_path", srcPlacement.Path);
            }

            Editor.PushUndoState();
        }

        // ── Erase ──

        private void EraseAtScreenPos(Vector2 screenPos)
        {
            // Try grid cell first
            var gridPos = RaycastToGrid(screenPos);
            if (gridPos.HasValue)
            {
                EraseCellAt(gridPos.Value);
                Editor.PushUndoState();
                return;
            }

            // Then try 3D objects
            SelectAtScreenPos(screenPos);
            if (SelectedNode != null)
                DeleteSelected();
        }

        // ── Ghost Preview ──

        private void UpdateGhostPosition(Vector2I gridPos)
        {
            if (CurrentMode == EditorToolMode.CellPaint ||
                CurrentMode == EditorToolMode.HeightPaint ||
                CurrentMode == EditorToolMode.Erase ||
                CurrentMode == EditorToolMode.TexturePaint)
            {
                EnsureCellGhost();
                if (_ghostPreview != null)
                {
                    var worldPos = Editor.Grid.GridToWorld(gridPos);
                    _ghostPreview.Position = worldPos + new Vector3(0, 0.05f, 0);
                }
            }
            else if (CurrentMode == EditorToolMode.AssetPlace)
            {
                if (!string.IsNullOrEmpty(PlaceAssemblyId))
                {
                    // Assembly ghost preview
                    var assembly = Editor?.GetNodeOrNull<LevelEditorAssembly>("LevelEditorAssembly");
                    if (assembly != null)
                    {
                        var worldPos = Editor.Grid.GridToWorld(gridPos);
                        assembly.UpdateGhostPosition(worldPos);
                    }
                }
                else if (!string.IsNullOrEmpty(PlaceAssetPath))
                {
                    EnsureAssetGhost();
                    if (_ghostPreview != null)
                    {
                        var worldPos = Editor.Grid.GridToWorld(gridPos);
                        _ghostPreview.Position = worldPos;
                    }
                }
            }
        }

        private void EnsureCellGhost()
        {
            if (_ghostPreview != null) return;
            var mesh = new MeshInstance3D();
            var box = new BoxMesh();
            box.Size = new Vector3(Constants.VINE_CELL_SIZE * 0.9f, 0.1f, Constants.VINE_CELL_SIZE * 0.9f);
            mesh.Mesh = box;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0f, 0.85f, 0.95f, 0.3f);
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mesh.MaterialOverride = mat;

            _ghostPreview = mesh;
            AddChild(_ghostPreview);
        }

        private void EnsureAssetGhost()
        {
            ClearGhost();
            if (string.IsNullOrEmpty(PlaceAssetPath)) return;

            var model = AssetLibrary.InstantiateNormalized(PlaceAssetPath);
            if (model == null) return;

            // Make translucent
            MakeTranslucent(model);
            model.RotationDegrees = new Vector3(0, PlaceRotationY, 0);
            _ghostPreview = model;
            AddChild(_ghostPreview);
        }

        private void UpdateGhostRotation()
        {
            if (_ghostPreview != null && CurrentMode == EditorToolMode.AssetPlace)
                _ghostPreview.RotationDegrees = new Vector3(0, PlaceRotationY, 0);
        }

        private void ClearGhost()
        {
            if (_ghostPreview != null)
            {
                _ghostPreview.QueueFree();
                _ghostPreview = null;
            }
        }

        private static void MakeTranslucent(Node3D node)
        {
            foreach (var child in node.GetChildren())
            {
                if (child is MeshInstance3D mesh)
                {
                    var mat = new StandardMaterial3D();
                    mat.AlbedoColor = new Color(0f, 0.85f, 0.95f, 0.35f);
                    mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                    mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                    mesh.MaterialOverride = mat;
                }
                if (child is Node3D child3d)
                    MakeTranslucent(child3d);
            }
        }

        // ── Raycasting ──

        private Vector2I? RaycastToGrid(Vector2 screenPos)
        {
            var camera = GetViewport().GetCamera3D();
            if (camera == null) return null;

            var from = camera.ProjectRayOrigin(screenPos);
            var dir = camera.ProjectRayNormal(screenPos);

            // Intersect with Y=0 plane (approximate ground)
            if (Mathf.Abs(dir.Y) < 0.001f) return null;
            float t = -from.Y / dir.Y;
            if (t < 0) return null;

            var hitPoint = from + dir * t;
            var gridPos = Editor.Grid.WorldToGrid(hitPoint);

            if (!Editor.Grid.InBounds(gridPos)) return null;
            return gridPos;
        }
    }
}
