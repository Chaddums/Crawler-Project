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

        private struct Particle
        {
            public MeshInstance3D Mesh;
            public float Life, MaxLife;
            public Vector3 StartPos;
            public float Speed, Spin;
        }

        public override void _Ready()
        {
            _grid = ServiceLocator.TryGet<VineGrid>(out var g) ? g : null;

            // Dome uses BIT palette — white spaceship aesthetic, same on every planet
            _domeAccent = BitPalette.Accent;
            _domeAccentDim = BitPalette.AccentDim;
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
            // Clamp radii to avoid inside-out geometry
            innerR = Mathf.Max(0f, innerR);
            centerR = Mathf.Max(innerR + 0.01f, centerR);
            outerR = Mathf.Max(centerR + 0.01f, outerR);

            var st = new SurfaceTool();
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
            target.Mesh = st.Commit();
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

            if (_isScrapyard)
            {
                // Scrapyard: solid BIT palette surface — silver-white metallic paint, NO grid lines
                floorShader.Code = @"
shader_type spatial;
render_mode unshaded, depth_draw_never, cull_disabled;
uniform vec3 base_color : source_color = vec3(0.82, 0.84, 0.88);
uniform vec3 accent_color : source_color = vec3(0.9, 0.93, 1.0);
uniform float emission_strength = 0.15;
void fragment() {
    // Subtle procedural noise for panel/plate texture variation
    vec2 scaled = UV * 20.0;
    float n1 = fract(sin(dot(floor(scaled), vec2(12.9898, 78.233))) * 43758.5453);
    float n2 = fract(sin(dot(floor(scaled * 0.5), vec2(39.346, 11.135))) * 28947.123);
    // Panel seams — faint darker lines in a large grid
    vec2 seam = abs(fract(UV * 5.0 - 0.5) - 0.5);
    float seamLine = 1.0 - smoothstep(0.02, 0.04, min(seam.x, seam.y));
    // Mix base with slight noise variation + darken at seams
    vec3 col = mix(base_color, accent_color, n1 * 0.12 + n2 * 0.05);
    col = mix(col, col * 0.6, seamLine * 0.3);
    ALBEDO = col;
    EMISSION = accent_color * emission_strength * (1.0 - seamLine * 0.5);
    ALPHA = 0.92;
}
";
                GD.Print("[ConversionDome] Built Scrapyard dome floor: BIT palette (silver-white, no grid)");
            }
            else
            {
                // Tron: dark surface with glowing grid lines
                floorShader.Code = @"
shader_type spatial;
render_mode unshaded, depth_draw_never, cull_disabled;
uniform vec3 base_color : source_color = vec3(0.04, 0.04, 0.08);
uniform vec3 grid_color : source_color = vec3(0.7, 0.75, 0.85);
uniform float grid_spacing = 2.0;
uniform float grid_width = 0.04;
uniform float grid_emission = 0.4;
void fragment() {
    vec2 world_uv = UV * grid_spacing * 10.0;
    vec2 grid = abs(fract(world_uv - 0.5) - 0.5);
    float line = min(grid.x, grid.y);
    float mask = 1.0 - smoothstep(grid_width, grid_width + 0.02, line);
    ALBEDO = mix(base_color, grid_color, mask);
    EMISSION = grid_color * grid_emission * mask;
    ALPHA = mix(0.88, 1.0, mask);
}
";
                GD.Print("[ConversionDome] Built Tron dome floor: dark + grid lines");
            }

            floorMat.Shader = floorShader;
            _domeFloor.MaterialOverride = floorMat;
            AddChild(_domeFloor);
        }

        private void UpdateDomeFloor()
        {
            if (_domeFloor == null || _grid == null) return;
            float r = Mathf.Max(0.5f, CurrentRadius);

            var center = GlobalPosition;
            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);

            int segments = 24;
            int rings = Mathf.Max(3, (int)(r / 2f));
            float heightOffset = 0.15f;
            float innerHole = 5.0f;

            for (int ring = 0; ring < rings; ring++)
            {
                float r0 = innerHole + (r - innerHole) * ring / rings;
                float r1 = innerHole + (r - innerHole) * (ring + 1) / rings;
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

            _domeFloor.Mesh = st.Commit();
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

            // Converted terrain material — planet-aware
            var bitMat = new StandardMaterial3D();
            if (_isScrapyard)
            {
                // Scrapyard: silver-white BIT palette
                bitMat.AlbedoColor = BitPalette.Body;
                bitMat.Roughness = 0.25f;
                bitMat.Metallic = 0.75f;
                bitMat.EmissionEnabled = true;
                bitMat.Emission = BitPalette.Accent;
                bitMat.EmissionEnergyMultiplier = 0.08f;
            }
            else
            {
                // Tron: dark body with subtle accent emission
                bitMat.AlbedoColor = new Color(0.03f, 0.03f, 0.05f);
                bitMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                bitMat.EmissionEnabled = true;
                bitMat.Emission = _domeAccent;
                bitMat.EmissionEnergyMultiplier = 0.15f;
            }

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
                var worldPos = mesh.GlobalPosition;
                float dist = new Vector2(worldPos.X - center.X, worldPos.Z - center.Z).Length();

                if (dist <= radius && !_convertedSceneMeshes.Contains(id))
                {
                    _originalSceneMaterials[id] = mesh.MaterialOverride;
                    mesh.MaterialOverride = bitMat;
                    _convertedSceneMeshes.Add(id);
                    count++;
                }
                else if (dist > radius && _convertedSceneMeshes.Contains(id))
                {
                    if (_originalSceneMaterials.TryGetValue(id, out var orig))
                        mesh.MaterialOverride = orig;
                    _convertedSceneMeshes.Remove(id);
                    _originalSceneMaterials.Remove(id);
                }
            }

            // Don't recurse into player-owned nodes, the dome, or UI layers
            if (node is ConversionDome || node is VinePlayer || node is VineHarvester
                || node is VineNode || node is VineEnemy || node is CanvasLayer) return;

            foreach (var child in node.GetChildren())
                ConvertSceneChildren(child, center, radius, bitMat, ref count);
        }

        private static void SaveAndReplaceMaterials(Node node, StandardMaterial3D newMat, List<Material> originals)
        {
            if (node is MeshInstance3D mesh)
            {
                originals.Add(mesh.MaterialOverride);
                mesh.MaterialOverride = newMat;
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

            var mesh = new MeshInstance3D();
            float size = GD.Randf() * 0.08f + 0.03f;
            mesh.Mesh = new SphereMesh { Radius = size, Height = size * 2.5f, RadialSegments = 4, Rings = 2 };

            var mat = new StandardMaterial3D();
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.AlbedoColor = new Color(_domeAccent.R, _domeAccent.G, _domeAccent.B, 0.5f);
            mat.EmissionEnabled = true;
            mat.Emission = _domeAccent;
            mat.EmissionEnergyMultiplier = 1f;
            mesh.MaterialOverride = mat;

            GetParent().AddChild(mesh);
            mesh.GlobalPosition = wpos;
            _particles.Add(new Particle {
                Mesh = mesh, Life = 0, MaxLife = GD.Randf() * 1.5f + 1f,
                StartPos = wpos, Speed = GD.Randf() * 1.5f + 0.8f,
                Spin = GD.Randf() * 120f - 60f
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

            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);
            var cBase = new Color(_domeAccent.R, _domeAccent.G, _domeAccent.B, 0.3f);
            var cTop = new Color(_domeAccent.R, _domeAccent.G, _domeAccent.B, 0f);
            var l = tangent * -halfW;
            var rr = tangent * halfW;
            var up = Vector3.Up * height;

            st.SetColor(cBase); st.AddVertex(l);
            st.SetColor(cBase); st.AddVertex(rr);
            st.SetColor(cTop); st.AddVertex(rr + up);
            st.SetColor(cBase); st.AddVertex(l);
            st.SetColor(cTop); st.AddVertex(rr + up);
            st.SetColor(cTop); st.AddVertex(l + up);
            st.GenerateNormals();

            var mesh = new MeshInstance3D();
            mesh.Mesh = st.Commit();
            var mat = new StandardMaterial3D();
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.VertexColorUseAsAlbedo = true;
            mat.EmissionEnabled = true;
            mat.Emission = Colors.White;
            mat.EmissionEnergyMultiplier = 0.4f;
            mesh.MaterialOverride = mat;

            GetParent().AddChild(mesh);
            mesh.GlobalPosition = basePos;
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
                p.Mesh.Scale = Vector3.One * Mathf.Lerp(0.8f, 0.2f, p.Life);
                if (p.Mesh.MaterialOverride is StandardMaterial3D mat)
                { mat.AlbedoColor = new Color(_domeAccent.R, _domeAccent.G, _domeAccent.B, a * 0.4f); mat.EmissionEnergyMultiplier = a * 0.8f; }
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
                if (w.Mesh.MaterialOverride is StandardMaterial3D mat)
                    mat.EmissionEnergyMultiplier = 0.4f * (1f - w.Life);
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

            RebuildAll();
            UpdateDomeFloor();
            UpdateTerrainConversion();
            UpdateParticles(dt);

            // Lazy-acquire grid if _Ready ran before VineGrid registered
            if (_grid == null)
                _grid = ServiceLocator.TryGet<VineGrid>(out var g) ? g : null;

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
                _domeAccent = BitPalette.Accent;
                _domeAccentDim = BitPalette.AccentDim;
            }
            _pendingAccentChange = true;
        }

        private void OnMaterialTypeSelected(MaterialType type)
        {
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
            foreach (var p in _particles) if (GodotObject.IsInstanceValid(p.Mesh)) p.Mesh.QueueFree();
            foreach (var w in _wisps) if (GodotObject.IsInstanceValid(w.Mesh)) w.Mesh.QueueFree();
            _particles.Clear(); _wisps.Clear();
        }
    }
}
