using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Draws faint lines showing enemy paths from each entry to the core.
    /// Updates when nodes are placed/removed or gates change state.
    /// </summary>
    public partial class VinePathPreview : Node3D
    {
        private VineGrid _grid;
        private VinePathfinder _pathfinder;
        private readonly List<MeshInstance3D> _pathLines = new();

        private static readonly Color[] TronEntryColors = {
            new(0.2f, 0.9f, 0.3f, 0.35f),  // Entry 1: green
            new(0.3f, 0.7f, 0.9f, 0.35f),  // Entry 2: blue
            new(0.9f, 0.7f, 0.2f, 0.35f),  // Entry 3: orange
            new(0.9f, 0.3f, 0.7f, 0.35f),  // Entry 4: pink
        };

        private static readonly Color[] ScrapyardEntryColors = {
            new(0.8f, 0.5f, 0.2f, 0.4f),   // Entry 1: warm amber
            new(0.7f, 0.4f, 0.15f, 0.4f),  // Entry 2: copper
            new(0.6f, 0.3f, 0.1f, 0.4f),   // Entry 3: rust
            new(0.7f, 0.5f, 0.1f, 0.4f),   // Entry 4: ochre
        };

        private static Color[] EntryColors =>
            PlanetTheme.Current is ScrapyardPlanetTheme ? ScrapyardEntryColors : TronEntryColors;

        public override void _Ready()
        {
            _grid = ServiceLocator.Get<VineGrid>();
            _pathfinder = ServiceLocator.Get<VinePathfinder>();

            GameEvents.OnVinePathRecalculated += RebuildPreview;
            GameEvents.OnVineNodePlaced += _ => RebuildDelayed();
            GameEvents.OnVineNodeSold += _ => RebuildDelayed();

            // Initial build after pathfinder has cached paths
            CallDeferred(nameof(RebuildPreview));
        }

        private void RebuildDelayed()
        {
            // Defer so pathfinder recalculates first
            CallDeferred(nameof(RebuildPreview));
        }

        private void RebuildPreview()
        {
            // Clear old lines
            foreach (var line in _pathLines)
                line?.QueueFree();
            _pathLines.Clear();

            if (_grid == null || _pathfinder == null) return;

            for (int i = 0; i < _grid.EntryPoints.Count; i++)
            {
                var entry = _grid.EntryPoints[i];
                var path = _pathfinder.GetCachedPath(entry);
                if (path == null || path.Count < 2) continue;

                Color color = i < EntryColors.Length ? EntryColors[i] : EntryColors[0];
                DrawPath(path, color, i);
            }
        }

        private void DrawPath(List<Vector2I> path, Color color, int entryIndex)
        {
            float yOffset = 0.08f + entryIndex * 0.02f; // Slight vertical offset so paths don't z-fight

            for (int i = 0; i < path.Count - 1; i++)
            {
                var from = _grid.GridToWorld(path[i]) + new Vector3(0, yOffset, 0);
                var to = _grid.GridToWorld(path[i + 1]) + new Vector3(0, yOffset, 0);

                var line = new MeshInstance3D();
                var direction = to - from;

                var box = new BoxMesh();
                float thickness = 0.12f;
                if (Mathf.Abs(direction.X) > Mathf.Abs(direction.Z))
                    box.Size = new Vector3(direction.Length(), thickness * 0.5f, thickness);
                else
                    box.Size = new Vector3(thickness, thickness * 0.5f, direction.Length());

                line.Mesh = box;
                line.Position = from.Lerp(to, 0.5f);

                var mat = new StandardMaterial3D();
                mat.AlbedoColor = color;
                mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                mat.EmissionEnabled = true;
                mat.Emission = new Color(color.R, color.G, color.B);
                mat.EmissionEnergyMultiplier = PlanetTheme.Current is ScrapyardPlanetTheme ? 0.2f : 0.5f;
                line.MaterialOverride = mat;

                AddChild(line);
                _pathLines.Add(line);
            }

            // Arrow at the end pointing toward core
            if (path.Count >= 2)
            {
                var lastPos = _grid.GridToWorld(path[^1]) + new Vector3(0, yOffset + 0.2f, 0);
                var arrow = new MeshInstance3D();
                var sphere = new SphereMesh();
                sphere.Radius = 0.18f;
                sphere.Height = 0.36f;
                arrow.Mesh = sphere;
                arrow.Position = lastPos;

                var arrowMat = new StandardMaterial3D();
                arrowMat.AlbedoColor = new Color(color.R, color.G, color.B, 0.6f);
                arrowMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                arrowMat.EmissionEnabled = true;
                arrowMat.Emission = new Color(color.R, color.G, color.B);
                arrow.MaterialOverride = arrowMat;

                AddChild(arrow);
                _pathLines.Add(arrow);
            }
        }

        public override void _ExitTree()
        {
            GameEvents.OnVinePathRecalculated -= RebuildPreview;
        }
    }
}
