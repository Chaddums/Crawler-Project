using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// The enemies' route, drawn on the ground: chevrons from each open entry to the Spire that
    /// march toward it, bright in the build phase and faint during waves. While a tower is being
    /// placed, the route as it would be with that tower in the way shows in the build colour, so
    /// walls and towers visibly bend the path. A label at each entry gives the route's length
    /// and how much the player's maze has added to it.
    /// </summary>
    public partial class PathPreview : Node3D
    {
        private VineGrid _grid;
        private VinePathfinder _pathfinder;
        private MultiMeshInstance3D _route, _preview;
        private ShaderMaterial _routeMat, _previewMat;
        private readonly Dictionary<Vector2I, int> _baseline = new();
        private readonly List<Label3D> _labels = new();
        private int _version = -1;
        private Vector2I? _ghost;
        private int _lastRouteCount;

        /// <summary>Tests: chevrons on the live route, and on the ghost's preview route.</summary>
        internal int RouteMarks => _route?.Multimesh?.VisibleInstanceCount ?? 0;
        internal int PreviewMarks => _preview != null && _preview.Visible ? _preview.Multimesh.VisibleInstanceCount : 0;
        /// <summary>Tests: the cells of the first entry's live route.</summary>
        internal List<Vector2I> FirstRoute { get; private set; } = new();
        internal List<Vector2I> FirstPreview { get; private set; } = new();

        private const string Shader = @"
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, blend_mix;
uniform vec4 tint : source_color = vec4(1.0, 0.35, 0.25, 1.0);
uniform float strength = 1.0;
varying float along;
void vertex() { along = INSTANCE_CUSTOM.x; }
void fragment() {
    float wave = 0.35 + 0.65 * pow(0.5 + 0.5 * sin(along * 0.9 - TIME * 4.0), 3.0);
    ALBEDO = tint.rgb;
    ALPHA = tint.a * wave * strength;
}";

        public override void _Ready()
        {
            Name = "PathPreview";
            ServiceLocator.TryGet(out _grid);
            ServiceLocator.TryGet(out _pathfinder);
            _route = Make(new Color(1f, 0.4f, 0.3f, 0.75f), out _routeMat);
            _preview = Make(new Color(0.45f, 0.95f, 1f, 0.9f), out _previewMat);
            _preview.Visible = false;
        }

        private MultiMeshInstance3D Make(Color tint, out ShaderMaterial mat)
        {
            mat = new ShaderMaterial { Shader = new Shader { Code = Shader } };
            mat.SetShaderParameter("tint", tint);
            var mm = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseCustomData = true,
                Mesh = Chevron(),
                InstanceCount = 1200,
                VisibleInstanceCount = 0,
            };
            var mi = new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            AddChild(mi);
            return mi;
        }

        /// <summary>A flat arrowhead pointing along +Z.</summary>
        private static ArrayMesh Chevron()
        {
            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);
            Vector3 a = new(-0.42f, 0, -0.18f), b = new(0, 0, 0.3f), c = new(0.42f, 0, -0.18f);
            Vector3 a2 = new(-0.42f, 0, -0.42f), b2 = new(0, 0, 0.06f), c2 = new(0.42f, 0, -0.42f);
            // Two strokes of a chevron: (a,b,b2,a2) and (b,c,c2,b2)
            foreach (var (p, q, r) in new[] { (a, b, b2), (a, b2, a2), (b, c, c2), (b, c2, b2) })
            { st.SetNormal(Vector3.Up); st.AddVertex(p); st.AddVertex(q); st.AddVertex(r); }
            return st.Commit();
        }

        public override void _Process(double delta)
        {
            long __pt = FrameProfiler.Start();
            try
            {
                if (_grid == null || _pathfinder == null) return;
                var phase = GameManager.Instance?.CurrentPhase ?? GamePhase.Build;
                bool building = phase is GamePhase.Build or GamePhase.WaveComplete;
                _routeMat.SetShaderParameter("strength", building ? 1f : 0.35f);
                _route.Visible = GameSettings.RoutePreview;

                if (_pathfinder.Version != _version)
                {
                    _version = _pathfinder.Version;
                    Rebuild(_route, live: true, null);
                    _ghost = null; // force the preview to redraw against the new field
                }

                Vector2I? ghost = null;
                if (ServiceLocator.TryGet<VinePlacer>(out var placer)) ghost = placer.GhostCell;
                if (ghost != _ghost)
                {
                    _ghost = ghost;
                    if (ghost == null) _preview.Visible = false;
                    else
                    {
                        Rebuild(_preview, live: false, ghost);
                        _preview.Visible = true;
                    }
                    _routeMat.SetShaderParameter("strength", ghost != null ? 0.3f : building ? 1f : 0.35f);
                }
        
            }
            finally { FrameProfiler.Stop("route", __pt); }
        }

        private void Rebuild(MultiMeshInstance3D target, bool live, Vector2I? blocked)
        {
            var mm = target.Multimesh;
            int n = 0;
            bool first = true;
            foreach (var region in _grid.ActiveEntryRegions)
            {
                var path = live ? _pathfinder.FlowPath(region.Center) : _pathfinder.PreviewPath(region.Center, blocked.Value);
                if (path == null || path.Count < 2) continue;
                if (first) { if (live) FirstRoute = path; else FirstPreview = path; first = false; }
                if (live)
                {
                    if (!_baseline.ContainsKey(region.Center)) _baseline[region.Center] = path.Count;
                    Label(region, path.Count);
                }
                for (int i = 0; i < path.Count - 1 && n < mm.InstanceCount; i++)
                {
                    var a = _grid.GridToWorld(path[i]);
                    var b = _grid.GridToWorld(path[i + 1]);
                    var dir = b - a;
                    dir.Y = 0;
                    if (dir.LengthSquared() < 0.0001f) continue;
                    var mid = (a + b) * 0.5f;
                    mid.Y = _grid.GetWorldHeight(mid.X, mid.Z) + 0.09f;
                    var basis = Basis.LookingAt(-dir.Normalized(), Vector3.Up).Scaled(Vector3.One * 0.9f);
                    mm.SetInstanceTransform(n, new Transform3D(basis, mid));
                    mm.SetInstanceCustomData(n, new Color(i, 0, 0, 0)); // the pulse runs toward the Spire
                    n++;
                }
            }
            mm.VisibleInstanceCount = n;
            if (live) _lastRouteCount = n;
        }

        private void Label(VineEntryRegion region, int cells)
        {
            Label3D label = null;
            foreach (var l in _labels) if (l.HasMeta("entry") && (Vector2I)l.GetMeta("entry") == region.Center) label = l;
            if (label == null)
            {
                label = new Label3D
                {
                    Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                    FontSize = 34,
                    OutlineSize = 8,
                    PixelSize = 0.006f,
                    Modulate = new Color(1f, 0.6f, 0.5f),
                };
                label.SetMeta("entry", region.Center);
                AddChild(label);
                _labels.Add(label);
            }
            var w = _grid.GridToWorld(region.Center);
            label.GlobalPosition = new Vector3(w.X, _grid.GetWorldHeight(w.X, w.Z) + 1.6f, w.Z);
            int extra = cells - _baseline[region.Center];
            label.Text = extra > 0 ? $"ROUTE {cells} cells (+{extra} from your maze)" : $"ROUTE {cells} cells";
        }
    }
}
