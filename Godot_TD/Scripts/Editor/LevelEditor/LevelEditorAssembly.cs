using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Create, place, and expand assemblies (reusable multi-asset prefabs).
    /// </summary>
    public partial class LevelEditorAssembly : Node3D
    {
        public LevelEditorScene Editor { get; set; }

        private string _selectedAssemblyId;
        private Node3D _ghostPreview;

        public string SelectedAssemblyId
        {
            get => _selectedAssemblyId;
            set
            {
                _selectedAssemblyId = value;
                ClearGhost();
            }
        }

        /// <summary>
        /// Create an assembly from currently selected objects.
        /// </summary>
        public void CreateFromSelection(IReadOnlyList<Node3D> selectedNodes, string name)
        {
            if (selectedNodes == null || selectedNodes.Count < 2) return;

            // Compute centroid
            var centroid = Vector3.Zero;
            foreach (var node in selectedNodes)
                centroid += node.GlobalPosition;
            centroid /= selectedNodes.Count;

            var assembly = new AssemblyData
            {
                Id = name.ToLower().Replace(" ", "_"),
                Name = name
            };

            var tools = Editor?.GetNodeOrNull<LevelEditorTools>("LevelEditorTools");

            foreach (var node in selectedNodes)
            {
                var relPos = node.GlobalPosition - centroid;
                string assetPath = FindAssetPath(node, tools);

                var child = new AssemblyChildData
                {
                    AssetPath = assetPath,
                    RelPosX = relPos.X,
                    RelPosY = relPos.Y,
                    RelPosZ = relPos.Z,
                    RelRotY = node.RotationDegrees.Y,
                    ScaleX = node.Scale.X,
                    ScaleY = node.Scale.Y,
                    ScaleZ = node.Scale.Z
                };

                // Capture material override if present
                var placement = FindAssetPlacement(node);
                if (placement?.MaterialOverride != null)
                    child.MaterialOverride = placement.MaterialOverride;

                assembly.Children.Add(child);
            }

            AssemblySerializer.SaveToFile(assembly, $"{assembly.Id}.json");
            GD.Print($"[Assembly] Created '{name}' with {assembly.Children.Count} children");
        }

        /// <summary>
        /// Place an assembly at a world position.
        /// </summary>
        public void PlaceAssembly(string assemblyId, Vector3 worldPos)
        {
            var data = AssemblySerializer.LoadFromFile($"{assemblyId}.json");
            if (data == null) return;

            var tools = Editor?.GetNodeOrNull<LevelEditorTools>("LevelEditorTools");
            if (tools == null) return;

            int childIdx = 0;
            var childIds = new List<string>();

            foreach (var child in data.Children)
            {
                var model = AssetLibrary.InstantiateNormalized(child.AssetPath);
                if (model == null) continue;

                var pos = worldPos + new Vector3(child.RelPosX, child.RelPosY, child.RelPosZ);
                model.Position = pos;
                model.RotationDegrees = new Vector3(0, child.RelRotY, 0);
                model.Scale = new Vector3(child.ScaleX, child.ScaleY, child.ScaleZ);

                // Apply material
                if (child.MaterialOverride != null)
                    LevelEditorMaterialPainter.Apply(model, child.MaterialOverride);
                else
                    PlanetTheme.Current.ApplyToNode(model);

                Editor.Grid.AddChild(model);
                tools.TrackPlacedAsset(model);

                var assetId = $"asm_{assemblyId}_{childIdx}";
                Editor.CurrentLevel.Assets.Add(new AssetPlacement
                {
                    Id = assetId,
                    Path = child.AssetPath,
                    PosX = pos.X, PosY = pos.Y, PosZ = pos.Z,
                    RotY = child.RelRotY,
                    ScaleX = child.ScaleX, ScaleY = child.ScaleY, ScaleZ = child.ScaleZ,
                    MaterialOverride = child.MaterialOverride
                });

                childIds.Add(assetId);
                childIdx++;
            }

            // Record assembly placement
            Editor.CurrentLevel.Assemblies.Add(new AssemblyPlacement
            {
                Id = $"assembly_{Editor.CurrentLevel.Assemblies.Count + 1}",
                AssemblyId = assemblyId,
                PosX = worldPos.X, PosY = worldPos.Y, PosZ = worldPos.Z
            });

            Editor.PushUndoState();
        }

        /// <summary>
        /// Show ghost preview of assembly at cursor position.
        /// </summary>
        public void UpdateGhostPosition(Vector3 worldPos)
        {
            if (string.IsNullOrEmpty(_selectedAssemblyId)) return;

            if (_ghostPreview == null)
                CreateGhostPreview();

            if (_ghostPreview != null)
                _ghostPreview.Position = worldPos;
        }

        private void CreateGhostPreview()
        {
            ClearGhost();
            if (string.IsNullOrEmpty(_selectedAssemblyId)) return;

            var data = AssemblySerializer.LoadFromFile($"{_selectedAssemblyId}.json");
            if (data == null) return;

            _ghostPreview = new Node3D();

            foreach (var child in data.Children)
            {
                var model = AssetLibrary.InstantiateNormalized(child.AssetPath);
                if (model == null) continue;

                model.Position = new Vector3(child.RelPosX, child.RelPosY, child.RelPosZ);
                model.RotationDegrees = new Vector3(0, child.RelRotY, 0);
                model.Scale = new Vector3(child.ScaleX, child.ScaleY, child.ScaleZ);
                MakeTranslucent(model);
                _ghostPreview.AddChild(model);
            }

            AddChild(_ghostPreview);
        }

        public void ClearGhost()
        {
            if (_ghostPreview != null)
            {
                _ghostPreview.QueueFree();
                _ghostPreview = null;
            }
        }

        /// <summary>
        /// Expand an assembly: remove the AssemblyPlacement record, keeping individual assets.
        /// </summary>
        public void ExpandAssembly(string assemblyPlacementId)
        {
            Editor.CurrentLevel.Assemblies.RemoveAll(a => a.Id == assemblyPlacementId);
            GD.Print($"[Assembly] Expanded {assemblyPlacementId} — children are now individual assets");
        }

        public string[] ListAssemblies()
        {
            var files = AssemblySerializer.ListAssemblyFiles();
            for (int i = 0; i < files.Length; i++)
                files[i] = System.IO.Path.GetFileNameWithoutExtension(files[i]);
            return files;
        }

        private string FindAssetPath(Node3D node, LevelEditorTools tools)
        {
            if (node.HasMeta("asset_path"))
                return node.GetMeta("asset_path").AsString();

            // Try to match from level data
            var placement = FindAssetPlacement(node);
            return placement?.Path ?? "";
        }

        private AssetPlacement FindAssetPlacement(Node3D node)
        {
            if (Editor?.CurrentLevel == null) return null;

            foreach (var asset in Editor.CurrentLevel.Assets)
            {
                if (Mathf.Abs(asset.PosX - node.Position.X) < 0.01f &&
                    Mathf.Abs(asset.PosZ - node.Position.Z) < 0.01f)
                    return asset;
            }
            return null;
        }

        private static void MakeTranslucent(Node3D node)
        {
            foreach (var child in node.GetChildren())
            {
                if (child is MeshInstance3D mesh)
                {
                    var mat = new StandardMaterial3D();
                    mat.AlbedoColor = new Color(0f, 0.85f, 0.95f, 0.25f);
                    mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                    mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                    mesh.MaterialOverride = mat;
                }
                if (child is Node3D child3d)
                    MakeTranslucent(child3d);
            }
        }
    }
}
