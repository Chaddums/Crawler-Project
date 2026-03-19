using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Manages multi-select state + visual highlight overlays for the level editor.
    /// Supports single-click, shift-add, ctrl-toggle, and box-select.
    /// </summary>
    public partial class LevelEditorSelection : Node3D
    {
        public LevelEditorScene Editor { get; set; }

        private readonly List<Node3D> _selected = new();
        private readonly Dictionary<Node3D, MeshInstance3D> _highlights = new();

        // Box select state
        private bool _boxSelecting;
        private Vector2 _boxStart;
        private ColorRect _boxRect;

        public IReadOnlyList<Node3D> Selected => _selected;
        public int Count => _selected.Count;

        public void SelectSingle(Node3D node)
        {
            ClearAll();
            if (node != null)
                AddInternal(node);
        }

        public void AddToSelection(Node3D node)
        {
            if (node == null || _selected.Contains(node)) return;
            AddInternal(node);
        }

        public void ToggleSelection(Node3D node)
        {
            if (node == null) return;
            if (_selected.Contains(node))
                RemoveInternal(node);
            else
                AddInternal(node);
        }

        public void ClearAll()
        {
            foreach (var kvp in _highlights)
                kvp.Value?.QueueFree();
            _highlights.Clear();
            _selected.Clear();
        }

        public void RemoveNode(Node3D node)
        {
            RemoveInternal(node);
        }

        public bool Contains(Node3D node)
        {
            return _selected.Contains(node);
        }

        private void AddInternal(Node3D node)
        {
            _selected.Add(node);
            AddHighlight(node);
        }

        private void RemoveInternal(Node3D node)
        {
            _selected.Remove(node);
            if (_highlights.TryGetValue(node, out var hl))
            {
                hl.QueueFree();
                _highlights.Remove(node);
            }
        }

        private void AddHighlight(Node3D node)
        {
            if (_highlights.ContainsKey(node)) return;

            var aabb = AssetLibrary.GetCombinedAABB(node);
            if (aabb.Size.LengthSquared() < 0.001f)
                aabb = new Aabb(new Vector3(-0.5f, 0, -0.5f), Vector3.One);

            var mesh = new MeshInstance3D();
            var im = new ImmediateMesh();
            mesh.Mesh = im;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0f, 0.85f, 0.95f, 0.9f);
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.NoDepthTest = true;
            mesh.MaterialOverride = mat;

            DrawWireBox(im, aabb);

            node.AddChild(mesh);
            _highlights[node] = mesh;
        }

        private static void DrawWireBox(ImmediateMesh im, Aabb box)
        {
            var min = box.Position;
            var max = box.End;

            Vector3[] corners = {
                new(min.X, min.Y, min.Z), // 0
                new(max.X, min.Y, min.Z), // 1
                new(max.X, min.Y, max.Z), // 2
                new(min.X, min.Y, max.Z), // 3
                new(min.X, max.Y, min.Z), // 4
                new(max.X, max.Y, min.Z), // 5
                new(max.X, max.Y, max.Z), // 6
                new(min.X, max.Y, max.Z), // 7
            };

            im.SurfaceBegin(Mesh.PrimitiveType.Lines);
            // Bottom
            AddEdge(im, corners[0], corners[1]);
            AddEdge(im, corners[1], corners[2]);
            AddEdge(im, corners[2], corners[3]);
            AddEdge(im, corners[3], corners[0]);
            // Top
            AddEdge(im, corners[4], corners[5]);
            AddEdge(im, corners[5], corners[6]);
            AddEdge(im, corners[6], corners[7]);
            AddEdge(im, corners[7], corners[4]);
            // Vertical
            AddEdge(im, corners[0], corners[4]);
            AddEdge(im, corners[1], corners[5]);
            AddEdge(im, corners[2], corners[6]);
            AddEdge(im, corners[3], corners[7]);
            im.SurfaceEnd();
        }

        private static void AddEdge(ImmediateMesh im, Vector3 a, Vector3 b)
        {
            im.SurfaceAddVertex(a);
            im.SurfaceAddVertex(b);
        }

        // ── Box Select ──

        public void StartBoxSelect(Vector2 screenPos, CanvasLayer uiLayer)
        {
            _boxSelecting = true;
            _boxStart = screenPos;

            _boxRect = new ColorRect();
            _boxRect.Color = new Color(0f, 0.85f, 0.95f, 0.15f);
            _boxRect.ZIndex = 100;
            uiLayer.AddChild(_boxRect);
        }

        public void UpdateBoxSelect(Vector2 screenPos)
        {
            if (!_boxSelecting || _boxRect == null) return;

            var min = new Vector2(
                Mathf.Min(_boxStart.X, screenPos.X),
                Mathf.Min(_boxStart.Y, screenPos.Y));
            var size = new Vector2(
                Mathf.Abs(screenPos.X - _boxStart.X),
                Mathf.Abs(screenPos.Y - _boxStart.Y));

            _boxRect.Position = min;
            _boxRect.Size = size;
        }

        public void EndBoxSelect(Vector2 screenPos, IReadOnlyList<Node3D> candidates)
        {
            if (!_boxSelecting) return;
            _boxSelecting = false;

            if (_boxRect != null)
            {
                _boxRect.QueueFree();
                _boxRect = null;
            }

            var camera = GetViewport().GetCamera3D();
            if (camera == null) return;

            var rectMin = new Vector2(
                Mathf.Min(_boxStart.X, screenPos.X),
                Mathf.Min(_boxStart.Y, screenPos.Y));
            var rectMax = new Vector2(
                Mathf.Max(_boxStart.X, screenPos.X),
                Mathf.Max(_boxStart.Y, screenPos.Y));

            // Skip tiny drags (treat as click)
            if ((rectMax - rectMin).Length() < 5f) return;

            ClearAll();

            foreach (var node in candidates)
            {
                if (!IsInstanceValid(node)) continue;
                var screenPoint = camera.UnprojectPosition(node.GlobalPosition);
                if (screenPoint.X >= rectMin.X && screenPoint.X <= rectMax.X &&
                    screenPoint.Y >= rectMin.Y && screenPoint.Y <= rectMax.Y)
                {
                    AddInternal(node);
                }
            }
        }

        public bool IsBoxSelecting => _boxSelecting;
    }
}
