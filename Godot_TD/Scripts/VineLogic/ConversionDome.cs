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

        // ── BIT takeover floor + terrain conversion ──
        private MeshInstance3D _domeFloor;
        private readonly HashSet<Vector2I> _convertedDecor = new();
        private readonly Dictionary<Vector2I, List<Material>> _originalMaterials = new();
        private float _lastConvertRadius = -1f;

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
            bool isScrapyard = PlanetTheme.Current is ScrapyardPlanetTheme;
            _planetColor = isScrapyard
                ? new Color(0.85f, 0.45f, 0.1f)
                : new Color(0.0f, 0.95f, 0.85f);

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

        private void BuildDomeFloor()
        {
            _domeFloor = new MeshInstance3D();
            // Unit-radius disc — we'll scale XZ by dome radius each frame
            _domeFloor.Mesh = new CylinderMesh
            {
                TopRadius = 1f, BottomRadius = 1f, Height = 0.03f, RadialSegments = 32
            };
            var floorMat = new StandardMaterial3D();
            floorMat.AlbedoColor = BitPalette.Body;
            floorMat.Roughness = 0.2f;
            floorMat.Metallic = 0.8f;
            floorMat.EmissionEnabled = true;
            floorMat.Emission = BitPalette.Accent;
            floorMat.EmissionEnergyMultiplier = 0.08f;
            _domeFloor.MaterialOverride = floorMat;
            AddChild(_domeFloor);
        }

        private void UpdateDomeFloor()
        {
            if (_domeFloor == null) return;
            float r = Mathf.Max(0.01f, CurrentRadius);
            _domeFloor.Scale = new Vector3(r, 1f, r);
            _domeFloor.Position = new Vector3(0, 0.05f, 0);
        }

        /// <summary>
        /// Re-theme terrain decorations inside dome to BIT white palette.
        /// Only processes newly-entered props to avoid per-frame material churn.
        /// </summary>
        private void UpdateTerrainConversion()
        {
            if (_grid == null) return;
            float r = CurrentRadius;
            var center = GlobalPosition;

            // Skip if radius hasn't changed meaningfully
            if (Mathf.Abs(r - _lastConvertRadius) < 0.1f) return;
            _lastConvertRadius = r;

            var bitMat = BitPalette.MakeSolidMaterial(0.15f);

            foreach (var (gridPos, node) in _grid.TerrainDecorNodes)
            {
                if (!GodotObject.IsInstanceValid(node)) continue;
                float dist = new Vector2(node.Position.X - center.X, node.Position.Z - center.Z).Length();
                bool inside = dist <= r;

                if (inside && !_convertedDecor.Contains(gridPos))
                {
                    // Convert to BIT palette — save originals for revert
                    var originals = new List<Material>();
                    SaveAndReplaceMaterials(node, bitMat, originals);
                    _originalMaterials[gridPos] = originals;
                    _convertedDecor.Add(gridPos);
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
            RebuildAll();
            UpdateDomeFloor();
            UpdateTerrainConversion();
            UpdateParticles(dt);

            // Push dome boundary to ground shader — blends terrain to BIT white
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

        public override void _ExitTree()
        {
            GameEvents.OnHarvesterDamaged -= OnHarvesterDamaged;
            foreach (var p in _particles) if (GodotObject.IsInstanceValid(p.Mesh)) p.Mesh.QueueFree();
            foreach (var w in _wisps) if (GodotObject.IsInstanceValid(w.Mesh)) w.Mesh.QueueFree();
            _particles.Clear(); _wisps.Clear();
        }
    }
}
