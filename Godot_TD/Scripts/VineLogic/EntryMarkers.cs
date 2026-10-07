using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Entry markers: a strip of chevrons scrolling inward across each active entry's edge cells,
    /// starting a cell outside the field so it reads as "they come in here", plus a lit pylon at
    /// each end of the opening. Replaces faint per-cell discs that disappeared under the terrain
    /// texture. Lives under the grid, so the Conversion Dome leaves it alone.
    /// </summary>
    public static class EntryMarkers
    {
        private static Shader _chevronShader;

        private static Shader ChevronShader => _chevronShader ??= new Shader
        {
            Code = @"
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled;

uniform vec3 color : source_color = vec3(0.2, 1.0, 0.9);
uniform float strength = 1.0;
uniform float speed = 0.6;

// UV.x runs inward (0 = outer end, 1.5 = inner end, one unit per cell); UV.y runs along the opening
void fragment() {
    float across = abs(fract(UV.y) - 0.5);
    float f = UV.x * 1.2 + across * 0.9 - TIME * speed;
    float stripe = 1.0 - smoothstep(0.12, 0.2, fract(f));
    // fade in from the outside and out toward the field, so it marks the edge, not a lane
    float fade = smoothstep(0.0, 0.6, UV.x) * (1.0 - smoothstep(0.9, 1.5, UV.x));
    ALBEDO = color * strength;
    ALPHA = clamp(stripe * fade * 0.6 + fade * 0.05, 0.0, 1.0);
}
"
        };

        public static void Build(VineGrid grid, VineEntryRegion region)
        {
            if (grid == null || region == null || region.Cells.Count == 0) return;
            var color = PlanetTheme.Current?.EntryMarkerColor ?? new Color(0.2f, 1f, 0.9f);
            float cs = Constants.VINE_CELL_SIZE;

            // Inward unit vector for the opening
            Vector3 inward = region.Direction switch
            {
                CardinalDirection.West => new Vector3(1, 0, 0),
                CardinalDirection.East => new Vector3(-1, 0, 0),
                CardinalDirection.North => new Vector3(0, 0, 1),
                _ => new Vector3(0, 0, -1),
            };
            var along = new Vector3(inward.Z, 0, -inward.X);

            var root = new Node3D { Name = $"EntryMarker{region.Index}" };
            grid.AddChild(root);

            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);
            for (int i = 0; i < region.Cells.Count; i++)
            {
                var cell = region.Cells[i];
                var c = grid.GridToWorld(cell);
                float y = c.Y + 0.06f;
                // from a cell outside the field to the middle of the edge cell
                var inner = c;
                var outer = c - inward * (cs * 1.5f);
                var halfA = along * (cs * 0.5f);
                Vector3 V(Vector3 p) => new(p.X, y, p.Z);

                var o0 = V(outer - halfA); var o1 = V(outer + halfA);
                var n0 = V(inner - halfA); var n1 = V(inner + halfA);
                void Add(Vector3 p, float u, float v) { st.SetUV(new Vector2(u, v)); st.AddVertex(p); }
                Add(o0, 0, i); Add(o1, 0, i + 1); Add(n0, 1.5f, i);
                Add(o1, 0, i + 1); Add(n1, 1.5f, i + 1); Add(n0, 1.5f, i);
            }
            var stripMat = new ShaderMaterial { Shader = ChevronShader };
            stripMat.SetShaderParameter("color", new Vector3(color.R, color.G, color.B));
            root.AddChild(new MeshInstance3D
            {
                Name = "Chevrons",
                Mesh = st.Commit(),
                MaterialOverride = stripMat,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });

            // Pylons just outside each end of the opening
            var first = grid.GridToWorld(region.Cells[0]);
            var last = grid.GridToWorld(region.Cells[region.Cells.Count - 1]);
            var dirAlong = (last - first).Length() > 0.01f ? (last - first).Normalized() : along;
            var pylonBody = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.06f, 0.07f, 0.09f),
                Metallic = 0.6f,
                Roughness = 0.4f,
            };
            var pylonCap = new StandardMaterial3D
            {
                AlbedoColor = color,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            };
            foreach (var (end, sign) in new[] { (first, -1f), (last, 1f) })
            {
                var basePos = end + dirAlong * (sign * cs * 0.75f) - inward * (cs * 0.5f);
                var pylon = new MeshInstance3D
                {
                    Name = "Pylon",
                    Mesh = new BoxMesh { Size = new Vector3(0.35f, 2.4f, 0.35f) },
                    MaterialOverride = pylonBody,
                    Position = basePos + new Vector3(0, 1.2f, 0),
                };
                root.AddChild(pylon);
                var cap = new MeshInstance3D
                {
                    Name = "PylonCap",
                    Mesh = new BoxMesh { Size = new Vector3(0.42f, 0.5f, 0.42f) },
                    MaterialOverride = pylonCap,
                    Position = basePos + new Vector3(0, 2.55f, 0),
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                };
                root.AddChild(cap);
                var light = new OmniLight3D
                {
                    LightColor = color,
                    LightEnergy = 0.8f,
                    OmniRange = 5f,
                    ShadowEnabled = false,
                    Position = basePos + new Vector3(0, 2.4f, 0),
                };
                root.AddChild(light);
            }
        }
    }
}
