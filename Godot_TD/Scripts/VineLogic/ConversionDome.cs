using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Conversion dome — fog-like radial gradient VFX around the harvester.
    /// Built from many overlapping soft rings with per-vertex alpha, creating a
    /// continuous ethereal glow that builds toward the dome boundary.
    /// No hard edges — every element feathers from 0 to a low peak and back to 0.
    /// </summary>
    public partial class ConversionDome : Node3D
    {
        // ── State ──
        public float MaxRadius { get; private set; } = 10f;
        public float CurrentRadius { get; private set; } = 10f;
        public float BaseFloorRadius { get; private set; } = 10f;
        public bool IsLastStand { get; private set; }
        private int _lastStandHits;
        public const int LAST_STAND_MAX_HITS = 10;
        public int LastStandHitsRemaining => _lastStandHits;

        // ── Visual layers ──
        private const int FOG_LAYER_COUNT = 16;
        private readonly List<MeshInstance3D> _fogLayers = new();
        private MeshInstance3D _shieldSphere;
        private StandardMaterial3D _shieldMat;
        private float _shieldFlashTimer;

        // ── Particles ──
        private readonly List<Particle> _particles = new();
        private readonly List<Particle> _wisps = new();
        private float _particleTimer, _wispTimer;

        private float _time;
        private VineGrid _grid;

        // ── Colors ──
        private Color _domeAccent, _domeAccentDim, _planetColor;
        private bool _pendingAccentChange;
        private bool _isScrapyard;

        // ── BIT takeover floor + terrain conversion ──
        private MeshInstance3D _domeFloor;
        private readonly HashSet<Vector2I> _convertedDecor = new();
        private readonly Dictionary<Vector2I, List<Material>> _originalMaterials = new();
        private float _lastConvertRadius = -1f;
        private Vector3 _lastConvertPosition = Vector3.Zero;

        // ── Geometry cache ──
        // Fog rings + dome floor are static: they only depend on radius, position, colors and the
        // terrain heights underneath. They used to be rebuilt every frame (17 new ArrayMesh +
        // SurfaceTool per frame), which leaked native memory faster than the GC reclaimed it —
        // ~1,000 mesh uploads/s at 60 fps, and an out-of-memory kill within a minute headless.
        private bool _geometryDirty = true;
        private float _builtRadius = float.NaN;
        private Vector3 _builtPosition;
        private Color _builtAccent, _builtAccentDim, _builtPlanetColor;
        private bool _builtWithGrid;
        /// <summary>Number of times fog/floor geometry was rebuilt (diagnostics + tests).</summary>
        public int GeometryRebuildCount { get; private set; }

        private struct Particle
        {
            public MeshInstance3D Mesh;
            public float Life, MaxLife;
            public Vector3 StartPos;
            public float Speed, Spin, Size;
        }

        // ── Shared particle resources ──
        private static SphereMesh _risingParticleMesh;
        // Unit radius, height 2.5 (old per-particle mesh was radius=size, height=2.5*size)
        private static SphereMesh RisingParticleMesh => _risingParticleMesh ??=
            new SphereMesh { Radius = 1f, Height = 2.5f, RadialSegments = 4, Rings = 2 };

        private static readonly Dictionary<Color, ArrayMesh> _wispMeshes = new();
        private static ArrayMesh WispMesh(Color accent)
        {
            if (_wispMeshes.TryGetValue(accent, out var cached)) return cached;
            if (_wispMeshes.Count > 16) _wispMeshes.Clear();
            using var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);
            var cBase = new Color(accent.R, accent.G, accent.B, 0.3f);
            var cTop = new Color(accent.R, accent.G, accent.B, 0f);
            var l = new Vector3(-1, 0, 0); var r = new Vector3(1, 0, 0); var up = Vector3.Up;
            st.SetColor(cBase); st.AddVertex(l);
            st.SetColor(cBase); st.AddVertex(r);
            st.SetColor(cTop); st.AddVertex(r + up);
            st.SetColor(cBase); st.AddVertex(l);
            st.SetColor(cTop); st.AddVertex(r + up);
            st.SetColor(cTop); st.AddVertex(l + up);
            st.GenerateNormals();
            var mesh = st.Commit();
            _wispMeshes[accent] = mesh;
            return mesh;
        }

        private static StandardMaterial3D _wispMaterial;
        private static StandardMaterial3D WispMaterial => _wispMaterial ??= new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            VertexColorUseAsAlbedo = true,
            EmissionEnabled = true,
            Emission = Colors.White,
            EmissionEnergyMultiplier = 0.4f,
        };

        public override void _Ready()
        {
            _grid = ServiceLocator.TryGet<VineGrid>(out var g) ? g : null;

            // Dome edge colour comes from the planet's look (Grid Prime orange, Scrapyard cyan)
            _domeAccent = PlanetTheme.Current.DomeRimColor;
            _domeAccentDim = _domeAccent.Darkened(0.5f);
            _isScrapyard = PlanetTheme.Current is ScrapyardPlanetTheme;
            _planetColor = _isScrapyard
                ? new Color(0.85f, 0.45f, 0.1f)
                : new Color(0.0f, 0.95f, 0.85f);

            // Listen for mining mode and material type changes to tint the dome
            GameEvents.OnMiningModeChanged += OnMiningModeChanged;
            GameEvents.OnMaterialTypeSelected += OnMaterialTypeSelected;

            for (int i = 0; i < FOG_LAYER_COUNT; i++)
            {
                var layer = new MeshInstance3D();
                var mat = new StandardMaterial3D();
                mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                mat.VertexColorUseAsAlbedo = true;
                mat.EmissionEnabled = true;
                mat.Emission = Colors.White;
                mat.EmissionEnergyMultiplier = 0.4f;
                layer.MaterialOverride = mat;
                AddChild(layer);
                _fogLayers.Add(layer);
            }

            BuildShieldSphere();
            BuildDomeFloor();
            GameEvents.OnHarvesterDamaged += OnHarvesterDamaged;
            // Terrain height changes move the ring/floor vertices
            GameEvents.OnTerrainChanged += OnTerrainChangedForGeometry;
            GameEvents.OnTerrainMutated += OnTerrainMutatedForGeometry;
        }

        private void OnTerrainChangedForGeometry(Vector2I _) => _geometryDirty = true;
        private void OnTerrainMutatedForGeometry(Vector2I _, VineCellType __) => _geometryDirty = true;

        private bool GeometryNeedsRebuild()
        {
            if (_geometryDirty) return true;
            return !Mathf.IsEqualApprox(_builtRadius, CurrentRadius)
                || !_builtPosition.IsEqualApprox(GlobalPosition)
                || _builtAccent != _domeAccent
                || _builtAccentDim != _domeAccentDim
                || _builtPlanetColor != _planetColor
                || _builtWithGrid != (_grid != null);
        }

        private void RebuildGeometryIfNeeded()
        {
            if (!GeometryNeedsRebuild()) return;
            RebuildAll();
            UpdateDomeFloor();
            _geometryDirty = false;
            _builtRadius = CurrentRadius;
            _builtPosition = GlobalPosition;
            _builtAccent = _domeAccent;
            _builtAccentDim = _domeAccentDim;
            _builtPlanetColor = _planetColor;
            _builtWithGrid = _grid != null;
            GeometryRebuildCount++;
        }

        /// <summary>Commit into the target's existing ArrayMesh instead of allocating a new one.</summary>
        private static void CommitInto(MeshInstance3D target, SurfaceTool st)
        {
            if (target.Mesh is ArrayMesh existing)
            {
                existing.ClearSurfaces();
                st.Commit(existing);
            }
            else
            {
                target.Mesh = st.Commit();
            }
        }

        // ── Height helpers ──

        private float SampleH(float wx, float wz) => _grid?.GetWorldHeight(wx, wz) ?? 0f;

        private Vector3 LocalPt(float radius, float angle, float yOff)
        {
            float wx = GlobalPosition.X + Mathf.Cos(angle) * radius;
            float wz = GlobalPosition.Z + Mathf.Sin(angle) * radius;
            return new Vector3(wx - GlobalPosition.X,
                SampleH(wx, wz) + yOff - GlobalPosition.Y,
                wz - GlobalPosition.Z);
        }

        private Vector3 WorldPt(float radius, float angle, float yOff)
        {
            float wx = GlobalPosition.X + Mathf.Cos(angle) * radius;
            float wz = GlobalPosition.Z + Mathf.Sin(angle) * radius;
            return new Vector3(wx, SampleH(wx, wz) + yOff, wz);
        }

        // ── Fog ring builder ──

        /// <summary>
        /// Build a single fog ring: a wide band that fades from 0 at innerR
        /// to peakAlpha at centerR, back to 0 at outerR. Pure gradient — no hard core.
        /// </summary>
        private void BuildFogRing(MeshInstance3D target, float innerR, float centerR,
            float outerR, Color color, float peakAlpha, int segments, float yOff)
        {
            peakAlpha *= PlanetTheme.Current.DomeHaloAlpha;
            // Clamp radii to avoid inside-out geometry
            innerR = Mathf.Max(0f, innerR);
            centerR = Mathf.Max(innerR + 0.01f, centerR);
            outerR = Mathf.Max(centerR + 0.01f, outerR);

            using var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);

            var c0 = new Color(color.R, color.G, color.B, 0f);
            var cP = new Color(color.R, color.G, color.B, peakAlpha);

            for (int i = 0; i < segments; i++)
            {
                float a0 = Mathf.Tau * i / segments;
                float a1 = Mathf.Tau * (i + 1) / segments;

                var iA = LocalPt(innerR, a0, yOff);
                var cA = LocalPt(centerR, a0, yOff);
                var oA = LocalPt(outerR, a0, yOff);
                var iB = LocalPt(innerR, a1, yOff);
                var cB = LocalPt(centerR, a1, yOff);
                var oB = LocalPt(outerR, a1, yOff);

                // Inner half: 0 → peak
                st.SetColor(c0); st.AddVertex(iA);
                st.SetColor(cP); st.AddVertex(cA);
                st.SetColor(c0); st.AddVertex(iB);
                st.SetColor(cP); st.AddVertex(cA);
                st.SetColor(cP); st.AddVertex(cB);
                st.SetColor(c0); st.AddVertex(iB);

                // Outer half: peak → 0
                st.SetColor(cP); st.AddVertex(cA);
                st.SetColor(c0); st.AddVertex(oA);
                st.SetColor(cP); st.AddVertex(cB);
                st.SetColor(c0); st.AddVertex(oA);
                st.SetColor(c0); st.AddVertex(oB);
                st.SetColor(cP); st.AddVertex(cB);
            }

            st.GenerateNormals();
            CommitInto(target, st);
        }

        private void BuildShieldSphere()
        {
            _shieldSphere = new MeshInstance3D();
            _shieldSphere.Mesh = new SphereMesh {
                Radius = 1f, Height = 2f, RadialSegments = 16, Rings = 8
            };
            _shieldSphere.Visible = false;
            _shieldMat = new StandardMaterial3D();
            _shieldMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            _shieldMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            _shieldMat.AlbedoColor = new Color(_domeAccent.R, _domeAccent.G, _domeAccent.B, 0f);
            _shieldMat.EmissionEnabled = true;
            _shieldMat.Emission = _domeAccent;
            _shieldMat.EmissionEnergyMultiplier = 0f;
            _shieldMat.CullMode = BaseMaterial3D.CullModeEnum.Back;
            _shieldSphere.MaterialOverride = _shieldMat;
            AddChild(_shieldSphere);
        }

        // ── Rebuild all fog layers ──

        private void RebuildAll()
        {
            float r = CurrentRadius;
            float y = 0.4f; // Well above terrain

            int idx = 0;

            // ── Boundary zone only — no interior fill ──
            // 6 overlapping dome-accent layers spread tightly around the boundary edge.
            // No pulsing — steady state. Material change inside handles the interior.
            for (int i = 0; i < 6 && idx < FOG_LAYER_COUNT; i++, idx++)
            {
                float offset = (i - 2.5f) * r * 0.03f;
                float center = r + offset;
                float width = r * (0.06f + i * 0.02f);
                float alpha = 0.07f + (i < 3 ? i * 0.01f : (5 - i) * 0.01f); // Bell curve
                BuildFogRing(_fogLayers[idx], center - width, center, center + width,
                    _domeAccent, alpha, 32, y + i * 0.01f);
            }

            // 4 conflict layers — planet color just outside boundary
            for (int i = 0; i < 4 && idx < FOG_LAYER_COUNT; i++, idx++)
            {
                float center = r + r * (0.06f + i * 0.05f);
                float width = r * (0.06f + i * 0.025f);
                float alpha = Mathf.Max(0.008f, 0.05f - i * 0.012f);
                BuildFogRing(_fogLayers[idx], center - width, center, center + width,
                    _planetColor, alpha, 28, y + 0.06f + i * 0.01f);
            }

            // 3 outer haze layers — dome accent fading outward
            for (int i = 0; i < 3 && idx < FOG_LAYER_COUNT; i++, idx++)
            {
                float center = r * (1.1f + i * 0.1f);
                float width = r * 0.18f;
                float alpha = Mathf.Max(0.005f, 0.012f - i * 0.003f);
                BuildFogRing(_fogLayers[idx], center - width, center, center + width,
                    _domeAccent, alpha, 20, y + i * 0.01f);
            }

            // 3 inner fade layers — gentle falloff just inside the boundary
            for (int i = 0; i < 3 && idx < FOG_LAYER_COUNT; i++, idx++)
            {
                float center = r * (0.88f - i * 0.06f);
                float width = r * 0.1f;
                float alpha = Mathf.Max(0.005f, 0.03f - i * 0.01f);
                BuildFogRing(_fogLayers[idx], center - width, center, center + width,
                    _domeAccentDim, alpha, 24, y + 0.03f + i * 0.01f);
            }

            // Shield
            _shieldSphere.Scale = Vector3.One * Mathf.Max(0.01f, r);
            _shieldSphere.Position = new Vector3(0, r * 0.3f, 0);
        }

        // ── BIT takeover floor ──

        // ── Takeover structures spawned inside dome ──
        private readonly List<Node3D> _takeoverStructures = new();
        private float _lastStructureRadius = -1f;

        private void BuildDomeFloor()
        {
            // Transparent overlay "painted" on top of terrain — depth_draw_never avoids z-fighting
            _domeFloor = new MeshInstance3D();
            var floorMat = new ShaderMaterial();
            var floorShader = new Shader();
            // A glowing edge ring with an optional faint fill. The old floors were a near-white
            // metal slab (Scrapyard) and a grid annulus (Grid Prime) that hid the terrain.
            floorShader.Code = @"
shader_type spatial;
render_mode depth_draw_never, cull_disabled, shadows_disabled;
uniform vec3 rim_color : source_color = vec3(0.3, 0.85, 1.0);
uniform float rim_strength = 1.6;
uniform vec3 fill_color : source_color = vec3(0.9);
uniform float fill_alpha = 0.0;
uniform vec3 center = vec3(0.0);
uniform float radius = 8.0;
uniform float inner = 5.0;
varying vec3 wp;
void vertex() { wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }
void fragment() {
    float d = length(wp.xz - center.xz);
    float fill = smoothstep(inner - 0.5, inner + 1.5, d) * (1.0 - smoothstep(radius - 2.5, radius, d));
    float rim = exp(-pow((d - (radius - 0.5)) / 0.28, 2.0));
    ALBEDO = mix(fill_color, rim_color, rim);
    ROUGHNESS = 0.6;
    EMISSION = rim_color * rim * rim_strength;
    ALPHA = clamp(fill * fill_alpha + rim * min(rim_strength, 1.0), 0.0, 1.0);
}
";
            GD.Print($"[ConversionDome] Built dome rim ({PlanetTheme.Current.PlanetName})");

            floorMat.Shader = floorShader;
            _domeFloor.MaterialOverride = floorMat;
            AddChild(_domeFloor);
        }

        private void UpdateDomeFloor()
        {
            if (_domeFloor == null || _grid == null) return;
            float r = Mathf.Max(0.5f, CurrentRadius);

            var center = GlobalPosition;
            using var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);

            int segments = 72;
            int rings = 6;
            float heightOffset = 0.15f;
            float innerHole = Mathf.Max(0.5f, r - 3f);
            float outer = r + 0.3f;

            if (_domeFloor.MaterialOverride is ShaderMaterial fm)
            {
                var theme = PlanetTheme.Current;
                fm.SetShaderParameter("rim_color", new Vector3(_domeAccent.R, _domeAccent.G, _domeAccent.B));
                fm.SetShaderParameter("rim_strength", theme.DomeRimStrength);
                var fc = theme.DomeFillColor;
                fm.SetShaderParameter("fill_color", new Vector3(fc.R, fc.G, fc.B));
                fm.SetShaderParameter("fill_alpha", theme.DomeFillAlpha);
                fm.SetShaderParameter("center", center);
                fm.SetShaderParameter("radius", r);
                fm.SetShaderParameter("inner", innerHole);
            }

            for (int ring = 0; ring < rings; ring++)
            {
                float r0 = innerHole + (outer - innerHole) * ring / rings;
                float r1 = innerHole + (outer - innerHole) * (ring + 1) / rings;
                if (r1 <= innerHole) continue;
                if (r0 < innerHole) r0 = innerHole;

                float uv0 = r0 / 8f;
                float uv1 = r1 / 8f;

                for (int seg = 0; seg < segments; seg++)
                {
                    float a0 = seg * Mathf.Tau / segments;
                    float a1 = (seg + 1) * Mathf.Tau / segments;

                    var p00 = new Vector3(Mathf.Cos(a0) * r0, 0, Mathf.Sin(a0) * r0);
                    var p10 = new Vector3(Mathf.Cos(a1) * r0, 0, Mathf.Sin(a1) * r0);
                    var p01 = new Vector3(Mathf.Cos(a0) * r1, 0, Mathf.Sin(a0) * r1);
                    var p11 = new Vector3(Mathf.Cos(a1) * r1, 0, Mathf.Sin(a1) * r1);

                    p00.Y = _grid.GetWorldHeight(center.X + p00.X, center.Z + p00.Z) - center.Y + heightOffset;
                    p10.Y = _grid.GetWorldHeight(center.X + p10.X, center.Z + p10.Z) - center.Y + heightOffset;
                    p01.Y = _grid.GetWorldHeight(center.X + p01.X, center.Z + p01.Z) - center.Y + heightOffset;
                    p11.Y = _grid.GetWorldHeight(center.X + p11.X, center.Z + p11.Z) - center.Y + heightOffset;

                    float uvA0 = (float)seg / segments;
                    float uvA1 = (float)(seg + 1) / segments;

                    st.SetNormal(Vector3.Up);
                    st.SetUV(new Vector2(uvA0 * 3f, uv0)); st.AddVertex(p00);
                    st.SetUV(new Vector2(uvA0 * 3f, uv1)); st.AddVertex(p01);
                    st.SetUV(new Vector2(uvA1 * 3f, uv0)); st.AddVertex(p10);
                    st.SetUV(new Vector2(uvA1 * 3f, uv0)); st.AddVertex(p10);
                    st.SetUV(new Vector2(uvA0 * 3f, uv1)); st.AddVertex(p01);
                    st.SetUV(new Vector2(uvA1 * 3f, uv1)); st.AddVertex(p11);
                }
            }

            CommitInto(_domeFloor, st);
            UpdateTakeoverStructures(r);
        }

        /// <summary>
        /// Spawn BIT infrastructure inside the dome as it grows.
        /// Small structures at close range, larger at wider radius.
        /// </summary>
        private void UpdateTakeoverStructures(float radius)
        {
            if (_grid == null) return;
            // Only add new structures when radius grows by 2+ units
            if (radius - _lastStructureRadius < 2f) return;
            _lastStructureRadius = radius;

            var center = GlobalPosition;
            var rng = new RandomNumberGenerator();
            rng.Seed = (ulong)(radius * 1000); // Deterministic per radius

            // Spawn 2-4 small structures at the new radius ring
            int count = rng.RandiRange(2, 4);
            for (int i = 0; i < count; i++)
            {
                float angle = rng.RandfRange(0, Mathf.Tau);
                float dist = rng.RandfRange(radius * 0.3f, radius * 0.85f);
                float wx = center.X + Mathf.Cos(angle) * dist;
                float wz = center.Z + Mathf.Sin(angle) * dist;
                float wy = _grid.GetWorldHeight(wx, wz);

                // Check we're on the grid
                var gridPos = _grid.WorldToGrid(new Vector3(wx, 0, wz));
                if (!_grid.InBounds(gridPos)) continue;

                // Pick a small structure type
                var structure = new MeshInstance3D();
                int type = rng.RandiRange(0, 3);
                switch (type)
                {
                    case 0: // Small antenna
                        structure.Mesh = new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.1f, Height = 1.2f };
                        break;
                    case 1: // Power node
                        structure.Mesh = new BoxMesh { Size = new Vector3(0.4f, 0.6f, 0.4f) };
                        break;
                    case 2: // Small dome
                        structure.Mesh = new SphereMesh { Radius = 0.3f, Height = 0.4f };
                        break;
                    case 3: // Pylon
                        structure.Mesh = new PrismMesh { Size = new Vector3(0.3f, 0.8f, 0.3f) };
                        break;
                }

                // Tint structures to current dome accent (reflects selected material color)
                structure.MaterialOverride = MakeTakeoverMaterial();

                AddChild(structure);
                structure.GlobalPosition = new Vector3(wx, wy, wz);
                structure.RotateY(rng.RandfRange(0, Mathf.Tau));
                _takeoverStructures.Add(structure);
            }
        }

        /// <summary>
        /// Create a tron-style material for takeover structures: dark body + accent outline.
        /// </summary>
        private StandardMaterial3D MakeTakeoverMaterial()
        {
            // Dark unlit body
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.04f, 0.04f, 0.06f);
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.EmissionEnabled = true;
            mat.Emission = _domeAccent;
            mat.EmissionEnergyMultiplier = 0.1f;

            // Accent-colored outline (tron edge-lit style)
            var outlineShader = new Shader();
            outlineShader.Code = @"
shader_type spatial;
render_mode unshaded, cull_front;
uniform vec3 outline_color : source_color = vec3(0.7, 0.2, 0.9);
uniform float outline_width : hint_range(0.0, 0.3) = 0.035;
void vertex() { VERTEX += NORMAL * outline_width; }
void fragment() { ALBEDO = outline_color; ALPHA = 0.7; }
";
            var outlineMat = new ShaderMaterial();
            outlineMat.Shader = outlineShader;
            outlineMat.SetShaderParameter("outline_color",
                new Vector3(_domeAccent.R, _domeAccent.G, _domeAccent.B));
            outlineMat.SetShaderParameter("outline_width", 0.035f);
            outlineMat.RenderPriority = -1;
            mat.NextPass = outlineMat;
            return mat;
        }

        /// <summary>
        /// Re-color all existing takeover structures to match current dome accent.
        /// </summary>
        private void RecolorTakeoverStructures()
        {
            var mat = MakeTakeoverMaterial();
            foreach (var s in _takeoverStructures)
                if (GodotObject.IsInstanceValid(s) && s is MeshInstance3D mesh)
                    mesh.MaterialOverride = mat;
        }

        /// <summary>
        /// Re-theme terrain decorations inside dome to BIT white palette.
        /// Only processes newly-entered props to avoid per-frame material churn.
        /// </summary>
        /// <summary>
        /// Force terrain conversion to re-evaluate all objects.
        /// Call after repositioning the dome.
        /// </summary>
        public void ForceConversionUpdate()
        {
            _lastConvertRadius = -1f;
            _lastConvertPosition = Vector3.Zero;
            // Revert all currently converted objects first
            foreach (var (gridPos, originals) in _originalMaterials)
            {
                if (_grid != null && _grid.TerrainDecorNodes.TryGetValue(gridPos, out var node))
                {
                    if (GodotObject.IsInstanceValid(node))
                        RestoreMaterials(node, originals);
                }
            }
            // Scene meshes too: clearing the bookkeeping without restoring left them converted
            // for good, and the next pass recorded the converted material as their original
            foreach (var id in _convertedSceneMeshes)
            {
                if (GodotObject.InstanceFromId(id) is not MeshInstance3D mesh || !GodotObject.IsInstanceValid(mesh)) continue;
                if (_originalSceneMaterials.TryGetValue(id, out var orig)) mesh.MaterialOverride = orig;
                if (mesh.MaterialOverlay == _convertedRim) mesh.MaterialOverlay = null;
            }
            _convertedDecor.Clear();
            _originalMaterials.Clear();
            _convertedSceneMeshes.Clear();
            _originalSceneMaterials.Clear();
        }

        private void UpdateTerrainConversion()
        {
            if (_grid == null) return;
            float r = CurrentRadius;
            var center = GlobalPosition;

            // Skip if nothing changed (radius same AND dome hasn't moved)
            bool radiusChanged = Mathf.Abs(r - _lastConvertRadius) > 0.1f;
            bool positionChanged = center.DistanceTo(_lastConvertPosition) > 0.5f;
            if (!radiusChanged && !positionChanged) return;
            _lastConvertRadius = r;
            _lastConvertPosition = center;

            // Converted terrain material comes from the planet's look; its glow follows the dome tint
            var bitMat = PlanetTheme.Current.MakeConvertedMaterial();
            bitMat.Emission = _domeAccent;
            // Wireframe edges take the accent instead of the body material, so converted decor
            // keeps its outline (Grid Prime's dark converted blocks were edge-less black shapes)
            _convertedLineMat = new StandardMaterial3D
            {
                AlbedoColor = _domeAccent,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            };
            // Converted solids get the accent rim towers have: inside the dome is "yours"
            _convertedRim = BitPalette.GetAccentRimMaterial(_domeAccent, ConvertedRimStrength);

            int totalNodes = _grid.TerrainDecorNodes.Count;
            int convertedCount = 0;

            foreach (var (gridPos, node) in _grid.TerrainDecorNodes)
            {
                if (!GodotObject.IsInstanceValid(node) || !node.IsInsideTree()) continue;
                var nodeWorld = node.GlobalPosition;
                float dist = new Vector2(nodeWorld.X - center.X, nodeWorld.Z - center.Z).Length();
                bool inside = dist <= r;

                if (inside && !_convertedDecor.Contains(gridPos))
                {
                    // Convert to BIT palette — save originals for revert
                    var originals = new List<Material>();
                    SaveAndReplaceMaterials(node, bitMat, originals);
                    _originalMaterials[gridPos] = originals;
                    _convertedDecor.Add(gridPos);
                    convertedCount++;
                }
                else if (!inside && _convertedDecor.Contains(gridPos))
                {
                    // Revert to original planet materials
                    if (_originalMaterials.TryGetValue(gridPos, out var originals))
                        RestoreMaterials(node, originals);
                    _convertedDecor.Remove(gridPos);
                    _originalMaterials.Remove(gridPos);
                }
            }

            // Also convert non-grid scene objects (terrain ring, background structures, props)
            // These are children of the battle scene, not tracked by VineGrid
            var sceneRoot = GetTree().Root;
            ConvertSceneChildren(sceneRoot, center, r, bitMat, ref convertedCount);

            if (convertedCount > 0)
                GD.Print($"[ConversionDome] Converted {convertedCount} objects (grid tracked: {totalNodes}, dome center: {center}, radius: {r:F1})");
        }

        // Track scene-level converted meshes separately (not grid-based)
        private readonly HashSet<ulong> _convertedSceneMeshes = new();
        private readonly Dictionary<ulong, Material> _originalSceneMaterials = new();

        private void ConvertSceneChildren(Node node, Vector3 center, float radius, StandardMaterial3D bitMat, ref int count)
        {
            if (node is MeshInstance3D mesh && mesh.Mesh != null
                && node is not VineHarvester // Don't convert the harvester itself
                && !mesh.IsInGroup("ExitGlow")
                && !mesh.IsInGroup("DomePart")) // Don't convert dome's own parts
            {
                var id = mesh.GetInstanceId();
                bool inside = FitsInsideDome(mesh, center, radius);

                if (inside && !_convertedSceneMeshes.Contains(id) && !IsSeeThrough(mesh))
                {
                    _originalSceneMaterials[id] = mesh.MaterialOverride;
                    ConvertMesh(mesh, bitMat);
                    _convertedSceneMeshes.Add(id);
                    count++;
                }
                else if (!inside && _convertedSceneMeshes.Contains(id))
                {
                    if (_originalSceneMaterials.TryGetValue(id, out var orig))
                        mesh.MaterialOverride = orig;
                    if (mesh.MaterialOverlay == _convertedRim) mesh.MaterialOverlay = null;
                    _convertedSceneMeshes.Remove(id);
                    _originalSceneMaterials.Remove(id);
                }
            }

            // Don't recurse into player-owned nodes, the dome, or UI layers
            // VineConnection: its vine color encodes power status and is recolored in place, so
            // swapping in the dome material would hide it (and pulse dots would write into bitMat).
            // VineGrid: its decor is converted (and restored) by the grid loop above; walking it here
            // too stored the dome material as the "original" and reverted to white.
            if (node is ConversionDome || node is VinePlayer || node is VineHarvester
                || node is VineNode || node is VineEnemy || node is VineConnection || node is CanvasLayer
                || node is VineGrid) return;

            foreach (var child in node.GetChildren())
                ConvertSceneChildren(child, center, radius, bitMat, ref count);
        }

        /// <summary>
        /// A scene mesh is inside the dome only if its whole footprint fits in it. Ground planes,
        /// haze layers and skyline pieces are centered on the grid too, but they are wider than
        /// the dome; recoloring them turned Scrapyard's haze into opaque sheets over the map.
        /// </summary>
        internal static bool FitsInsideDome(MeshInstance3D mesh, Vector3 center, float radius)
        {
            var aabb = mesh.GlobalTransform * mesh.GetAabb();
            if (Mathf.Max(aabb.Size.X, aabb.Size.Z) > radius * 2f) return false;
            var c = aabb.GetCenter();
            return new Vector2(c.X - center.X, c.Z - center.Z).Length() <= radius;
        }

        /// <summary>Haze, glow and fading VFX keep their look; the dome material is opaque.</summary>
        internal static bool IsSeeThrough(MeshInstance3D mesh)
        {
            if (mesh.Transparency > 0f) return true;
            return mesh.MaterialOverride is BaseMaterial3D bm
                && bm.Transparency != BaseMaterial3D.TransparencyEnum.Disabled;
        }

        private StandardMaterial3D _convertedLineMat;
        private ShaderMaterial _convertedRim;
        private static float ConvertedRimStrength => PlanetTheme.Current?.TowerRimStrength * 0.8f ?? 0f;

        private void ConvertMesh(MeshInstance3D mesh, StandardMaterial3D bodyMat)
        {
            bool line = IsLineMesh(mesh);
            mesh.MaterialOverride = line && _convertedLineMat != null ? _convertedLineMat : bodyMat;
            if (!line && _convertedRim != null && ConvertedRimStrength > 0f) mesh.MaterialOverlay = _convertedRim;
        }

        internal static bool IsLineMesh(MeshInstance3D mesh)
            => mesh.Mesh is ImmediateMesh
               || mesh.Mesh is ArrayMesh am && am.GetSurfaceCount() > 0
                  && am.SurfaceGetPrimitiveType(0) is Mesh.PrimitiveType.Lines or Mesh.PrimitiveType.LineStrip;

        private void SaveAndReplaceMaterials(Node node, StandardMaterial3D newMat, List<Material> originals)
        {
            if (node is MeshInstance3D mesh)
            {
                // Record every mesh so RestoreMaterials stays index-aligned, but leave lava pools,
                // glows and other see-through parts as they are
                originals.Add(mesh.MaterialOverride);
                if (!IsSeeThrough(mesh)) ConvertMesh(mesh, newMat);
            }
            foreach (var child in node.GetChildren())
                SaveAndReplaceMaterials(child, newMat, originals);
        }

        private static void RestoreMaterials(Node node, List<Material> originals)
        {
            int idx = 0;
            RestoreMaterialsRecursive(node, originals, ref idx);
        }

        private static void RestoreMaterialsRecursive(Node node, List<Material> originals, ref int idx)
        {
            if (node is MeshInstance3D mesh && idx < originals.Count)
            {
                mesh.MaterialOverride = originals[idx];
                // The conversion rim is the only overlay decor ever gets
                if (mesh.MaterialOverlay is ShaderMaterial sm && sm.Shader != null && sm.HasMeta(BitPalette.MetaAccentRim))
                    mesh.MaterialOverlay = null;
                idx++;
            }
            foreach (var child in node.GetChildren())
                RestoreMaterialsRecursive(child, originals, ref idx);
        }

        // ── Particles ──

        private void SpawnRising()
        {
            if (CurrentRadius < 0.5f) return;
            float angle = GD.Randf() * Mathf.Tau;
            float spawnR = CurrentRadius * (0.9f + GD.Randf() * 0.2f);
            var wpos = WorldPt(spawnR, angle, 0.3f);

            // Shared mesh + material (a new SphereMesh + material per particle, ~5/s, was held by
            // its C# wrapper until GC); per-instance size via Scale, fade via Transparency.
            var mesh = new MeshInstance3D();
            float size = GD.Randf() * 0.08f + 0.03f;
            mesh.Mesh = RisingParticleMesh;
            mesh.MaterialOverride = VfxCache.Glow(new Color(_domeAccent.R, _domeAccent.G, _domeAccent.B, 0.4f),
                _domeAccent, 0.8f, alpha: true);
            mesh.Transparency = 1f;

            GetParent().AddChild(mesh);
            mesh.GlobalPosition = wpos;
            _particles.Add(new Particle {
                Mesh = mesh, Life = 0, MaxLife = GD.Randf() * 1.5f + 1f,
                StartPos = wpos, Speed = GD.Randf() * 1.5f + 0.8f,
                Spin = GD.Randf() * 120f - 60f, Size = size
            });
        }

        private void SpawnWisp()
        {
            if (CurrentRadius < 1f) return;
            float angle = GD.Randf() * Mathf.Tau;
            float spawnR = CurrentRadius * (0.93f + GD.Randf() * 0.14f);
            var basePos = WorldPt(spawnR, angle, 0.1f);

            // Vertical quad with per-vertex alpha: bright base → transparent top
            float height = GD.Randf() * 1.2f + 0.6f;
            float halfW = GD.Randf() * 0.06f + 0.02f;
            var tangent = new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle));

            // Shared unit quad per accent color (was a new SurfaceTool + ArrayMesh + material per
            // wisp). The quad spans local X in [-1, 1] and Y in [0, 1]; scale to width/height and
            // rotate local +X onto the tangent (-sin a, 0, cos a).
            var mesh = new MeshInstance3D();
            mesh.Mesh = WispMesh(_domeAccent);
            mesh.MaterialOverride = WispMaterial;

            GetParent().AddChild(mesh);
            mesh.GlobalPosition = basePos;
            mesh.Basis = new Basis(Vector3.Up, -(angle + Mathf.Pi / 2f)).Scaled(new Vector3(halfW, height, 1f));
            _wisps.Add(new Particle {
                Mesh = mesh, Life = 0, MaxLife = GD.Randf() * 2.5f + 1.5f,
                StartPos = basePos, Speed = 0.3f
            });
        }

        private void UpdateParticles(float dt)
        {
            _particleTimer += dt;
            while (_particleTimer >= 0.18f) { _particleTimer -= 0.18f; SpawnRising(); }

            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                var p = _particles[i];
                p.Life += dt / p.MaxLife;
                if (p.Life >= 1f || !GodotObject.IsInstanceValid(p.Mesh))
                { if (GodotObject.IsInstanceValid(p.Mesh)) p.Mesh.QueueFree(); _particles.RemoveAt(i); continue; }

                p.Mesh.GlobalPosition = p.StartPos + Vector3.Up * (p.Speed * p.Life * p.MaxLife);
                p.Mesh.RotationDegrees += new Vector3(0, p.Spin * dt, 0);
                float a = p.Life < 0.3f ? p.Life / 0.3f : 1f - (p.Life - 0.3f) / 0.7f; // Fade in then out
                p.Mesh.Scale = Vector3.One * (p.Size * Mathf.Lerp(0.8f, 0.2f, p.Life));
                p.Mesh.Transparency = 1f - Mathf.Clamp(a, 0f, 1f);
                _particles[i] = p;
            }

            _wispTimer += dt;
            while (_wispTimer >= 0.4f) { _wispTimer -= 0.4f; SpawnWisp(); }

            for (int i = _wisps.Count - 1; i >= 0; i--)
            {
                var w = _wisps[i];
                w.Life += dt / w.MaxLife;
                if (w.Life >= 1f || !GodotObject.IsInstanceValid(w.Mesh))
                { if (GodotObject.IsInstanceValid(w.Mesh)) w.Mesh.QueueFree(); _wisps.RemoveAt(i); continue; }

                w.Mesh.GlobalPosition = w.StartPos + Vector3.Up * (w.Speed * w.Life * w.MaxLife);
                w.Mesh.Transparency = Mathf.Clamp(w.Life, 0f, 1f);
                _wisps[i] = w;
            }
        }

        // ── Update ──

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _time += dt;

            // Handle deferred accent change: revert + reconvert in the same frame
            if (_pendingAccentChange)
            {
                _pendingAccentChange = false;
                RecolorTakeoverStructures();
                ForceConversionUpdate();
            }

            // Lazy-acquire grid if _Ready ran before VineGrid registered
            if (_grid == null)
                _grid = ServiceLocator.TryGet<VineGrid>(out var g) ? g : null;

            RebuildGeometryIfNeeded();
            UpdateTerrainConversion();
            UpdateParticles(dt);

            // Push dome boundary to ground shader — blends terrain to BIT grid
            if (_grid?.GroundShaderMat != null)
                BitPalette.UpdateGroundDome(_grid.GroundShaderMat, GlobalPosition, CurrentRadius);

            if (_shieldFlashTimer > 0)
            {
                _shieldFlashTimer -= dt;
                float t = Mathf.Clamp(_shieldFlashTimer / 0.5f, 0f, 1f);
                _shieldMat.AlbedoColor = new Color(_domeAccent.R, _domeAccent.G, _domeAccent.B, t * t * 0.3f);
                _shieldMat.EmissionEnergyMultiplier = t * t * 2.5f;
                if (_shieldFlashTimer <= 0) _shieldSphere.Visible = false;
            }
        }

        // ── Dome radius ──

        public void SetFloorRadius(int floor)
        {
            BaseFloorRadius = 8f + (floor - 1) * 3f;
            MaxRadius = BaseFloorRadius;
            CurrentRadius = MaxRadius;
        }

        public void GrowForWave(int waveNumber, int totalWaves)
        {
            float progress = (float)waveNumber / totalWaves;
            CurrentRadius = Mathf.Max(CurrentRadius, BaseFloorRadius * (0.6f + progress * 0.4f));
        }

        public void ShrinkForDamage(float hpPercent)
        {
            if (IsLastStand) return;
            float newR = MaxRadius * hpPercent;
            if (newR <= 0.5f) { EnterLastStand(); return; }
            CurrentRadius = newR;
        }

        public void RestoreDome()
        {
            IsLastStand = false;
            _lastStandHits = 0;
            CurrentRadius = BaseFloorRadius * 0.5f;
        }

        public void ExpandToFullMap(float mapW, float mapH)
            => CurrentRadius = Mathf.Sqrt(mapW * mapW + mapH * mapH);

        // ── Last stand ──

        private void EnterLastStand()
        {
            IsLastStand = true;
            _lastStandHits = LAST_STAND_MAX_HITS;
            CurrentRadius = 0.5f;
            GameEvents.OnDomeCollapsed?.Invoke();
        }

        public bool TakeLastStandHit()
        {
            if (!IsLastStand) return true;
            _lastStandHits--;
            FlashShield();
            return _lastStandHits > 0;
        }

        // ── Hit flash ──

        private void OnHarvesterDamaged(float hp) => FlashShield();

        public void FlashShield()
        {
            _shieldSphere.Visible = true;
            _shieldFlashTimer = 0.5f;
            _shieldMat.AlbedoColor = new Color(_domeAccent.R, _domeAccent.G, _domeAccent.B, 0.3f);
            _shieldMat.EmissionEnergyMultiplier = 2.5f;
        }

        // ── Queries ──

        public bool IsInsideDome(Vector3 worldPos)
        {
            float dist = new Vector2(worldPos.X - GlobalPosition.X, worldPos.Z - GlobalPosition.Z).Length();
            return dist <= CurrentRadius;
        }

        // ── Cleanup ──

        // ── Mining mode visual reaction ──

        private void OnMiningModeChanged(MiningMode mode)
        {
            if (mode == MiningMode.Materials)
            {
                var harvester = ServiceLocator.TryGet<VineHarvester>(out var h) ? h : null;
                var materialColor = VineHarvester.GetMaterialColor(harvester?.SelectedMaterial ?? MaterialType.None);
                _domeAccent = materialColor;
                _domeAccentDim = new Color(materialColor.R * 0.5f, materialColor.G * 0.5f, materialColor.B * 0.5f);
            }
            else
            {
                _domeAccent = PlanetTheme.Current.DomeRimColor;
                _domeAccentDim = _domeAccent.Darkened(0.5f);
            }
            _pendingAccentChange = true;
        }

        private void OnMaterialTypeSelected(MaterialType type)
        {
            // Choosing a material doesn't switch modes — only tint while actually mining Materials
            // (OnMiningModeChanged applies the tint when the player toggles into Materials).
            var harvester = ServiceLocator.TryGet<VineHarvester>(out var h) ? h : null;
            if (harvester != null && harvester.CurrentMode != MiningMode.Materials) return;
            var materialColor = VineHarvester.GetMaterialColor(type);
            _domeAccent = materialColor;
            _domeAccentDim = new Color(materialColor.R * 0.5f, materialColor.G * 0.5f, materialColor.B * 0.5f);
            _pendingAccentChange = true;
        }

        public override void _ExitTree()
        {
            GameEvents.OnHarvesterDamaged -= OnHarvesterDamaged;
            GameEvents.OnMiningModeChanged -= OnMiningModeChanged;
            GameEvents.OnMaterialTypeSelected -= OnMaterialTypeSelected;
            GameEvents.OnTerrainChanged -= OnTerrainChangedForGeometry;
            GameEvents.OnTerrainMutated -= OnTerrainMutatedForGeometry;
            foreach (var p in _particles) if (GodotObject.IsInstanceValid(p.Mesh)) p.Mesh.QueueFree();
            foreach (var w in _wisps) if (GodotObject.IsInstanceValid(w.Mesh)) w.Mesh.QueueFree();
            _particles.Clear(); _wisps.Clear();
        }
    }
}
