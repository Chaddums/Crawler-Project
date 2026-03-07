using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Procedural backdrop for the dungeon that reflects sector theme and danger level.
    /// Creates sky gradient, distant silhouette structures, volumetric fog, and
    /// ambient particles. Danger escalates visuals: calm industrial -> hellish core.
    /// </summary>
    public partial class DungeonBackdrop : Node3D
    {
        private WorldEnvironment _worldEnv;
        private int _sector;
        private int _ascension;
        private float _danger; // 0-1 normalized danger

        public void Initialize(SectorData sectorData)
        {
            _sector = sectorData.SectorNumber;
            _ascension = MetaSaveManager.Data.AscensionRank;
            _danger = Mathf.Clamp((_sector - 1) / 4f + _ascension * 0.1f, 0f, 1f);

            BuildEnvironment(sectorData);
            BuildDistantStructures(sectorData);
            BuildAmbientParticles(sectorData);

            GD.Print($"[DungeonBackdrop] Sector {_sector} (Ascension {_ascension}), danger={_danger:F2}");
        }

        private void BuildEnvironment(SectorData sectorData)
        {
            // Find existing WorldEnvironment or create one
            _worldEnv = GetTree().Root.FindChild("WorldEnvironment", true, false) as WorldEnvironment;
            if (_worldEnv == null) return;

            var env = _worldEnv.Environment;
            if (env == null) return;

            // Sky colors based on sector theme
            var skyTop = GetSkyTopColor(sectorData);
            var skyBottom = GetSkyBottomColor(sectorData);
            var bgColor = GetBackgroundColor(sectorData);

            // Use procedural sky
            env.BackgroundMode = Godot.Environment.BGMode.Sky;
            var sky = new Sky();
            var skyMat = new ProceduralSkyMaterial();
            skyMat.SkyTopColor = skyTop;
            skyMat.SkyHorizonColor = skyBottom;
            skyMat.GroundBottomColor = bgColor;
            skyMat.GroundHorizonColor = skyBottom;
            skyMat.SunAngleMax = 0; // no sun disk
            skyMat.SunCurve = 0.01f;
            sky.SkyMaterial = skyMat;
            env.Sky = sky;

            // Ambient light from sector theme
            env.AmbientLightSource = Godot.Environment.AmbientSource.Sky;
            env.AmbientLightEnergy = Mathf.Lerp(0.8f, 0.4f, _danger);

            // Tonemap
            env.TonemapMode = Godot.Environment.ToneMapper.Filmic;
            env.TonemapExposure = Mathf.Lerp(1.0f, 0.85f, _danger);

            // Fog — thickens with danger
            env.FogEnabled = true;
            env.FogLightColor = skyBottom.Lerp(sectorData.AccentColor, 0.3f);
            env.FogDensity = Mathf.Lerp(0.002f, 0.008f, _danger);
            env.FogSkyAffect = 0.3f;

            // Glow for high-danger sectors
            if (_danger > 0.4f)
            {
                env.GlowEnabled = true;
                env.GlowIntensity = Mathf.Lerp(0.3f, 0.8f, _danger);
                env.GlowBloom = Mathf.Lerp(0.1f, 0.4f, _danger);
                env.GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Additive;
            }
        }

        private void BuildDistantStructures(SectorData sectorData)
        {
            var rng = new RandomNumberGenerator();
            rng.Randomize();

            float distance = 200f;
            int structCount = 20 + _sector * 5;
            var accentColor = sectorData.AccentColor;
            var darkColor = sectorData.WallTint * 0.3f;
            darkColor.A = 1f;

            for (int i = 0; i < structCount; i++)
            {
                float angle = (Mathf.Tau / structCount) * i + rng.RandfRange(-0.1f, 0.1f);
                float dist = distance + rng.RandfRange(-30f, 30f);
                float x = Mathf.Cos(angle) * dist;
                float z = Mathf.Sin(angle) * dist;

                var structure = CreateSilhouette(rng, sectorData, darkColor, accentColor);
                structure.Position = new Vector3(x, -5f, z);
                // Face toward center
                structure.LookAt(new Vector3(0, structure.Position.Y, 0), Vector3.Up);
                AddChild(structure);
            }

            // Add tall accent structures at cardinal directions
            for (int i = 0; i < 4; i++)
            {
                float angle = (Mathf.Pi / 2f) * i;
                float x = Mathf.Cos(angle) * (distance - 20f);
                float z = Mathf.Sin(angle) * (distance - 20f);

                var tower = CreateAccentTower(rng, sectorData, accentColor);
                tower.Position = new Vector3(x, -5f, z);
                AddChild(tower);
            }
        }

        private Node3D CreateSilhouette(RandomNumberGenerator rng, SectorData sectorData,
            Color darkColor, Color accentColor)
        {
            var root = new Node3D();
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = darkColor;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;

            float height = rng.RandfRange(15f, 60f) * (1f + _danger * 0.5f);
            float width = rng.RandfRange(3f, 12f);

            // Main tower/pillar
            var mesh = new MeshInstance3D();
            var box = new BoxMesh();
            box.Size = new Vector3(width, height, width * 0.6f);
            mesh.Mesh = box;
            mesh.MaterialOverride = mat;
            mesh.Position = new Vector3(0, height / 2f, 0);
            root.AddChild(mesh);

            // Random accent strip (glowing line on the structure)
            if (rng.Randf() < 0.4f + _danger * 0.3f)
            {
                var strip = new MeshInstance3D();
                var stripBox = new BoxMesh();
                stripBox.Size = new Vector3(width + 0.2f, 0.5f, width * 0.6f + 0.2f);
                strip.Mesh = stripBox;

                var glowMat = new StandardMaterial3D();
                glowMat.AlbedoColor = accentColor;
                glowMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                glowMat.EmissionEnabled = true;
                glowMat.Emission = accentColor;
                glowMat.EmissionEnergyMultiplier = 1.5f + _danger * 2f;
                strip.MaterialOverride = glowMat;
                strip.Position = new Vector3(0, height * rng.RandfRange(0.3f, 0.8f), 0);
                root.AddChild(strip);
            }

            // Second stacked element for variety
            if (rng.Randf() < 0.3f)
            {
                var top = new MeshInstance3D();
                var topBox = new BoxMesh();
                float topW = width * rng.RandfRange(0.5f, 0.8f);
                float topH = height * rng.RandfRange(0.3f, 0.6f);
                topBox.Size = new Vector3(topW, topH, topW * 0.5f);
                top.Mesh = topBox;
                top.MaterialOverride = mat;
                top.Position = new Vector3(
                    rng.RandfRange(-width * 0.3f, width * 0.3f),
                    height + topH / 2f,
                    0);
                root.AddChild(top);
            }

            return root;
        }

        private Node3D CreateAccentTower(RandomNumberGenerator rng, SectorData sectorData, Color accent)
        {
            var root = new Node3D();
            float height = 80f + _danger * 40f;

            // Tall dark core
            var darkMat = new StandardMaterial3D();
            darkMat.AlbedoColor = sectorData.WallTint * 0.2f;
            darkMat.AlbedoColor = new Color(darkMat.AlbedoColor.R, darkMat.AlbedoColor.G, darkMat.AlbedoColor.B, 1f);
            darkMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;

            var core = new MeshInstance3D();
            var coreBox = new BoxMesh();
            coreBox.Size = new Vector3(8f, height, 6f);
            core.Mesh = coreBox;
            core.MaterialOverride = darkMat;
            core.Position = new Vector3(0, height / 2f, 0);
            root.AddChild(core);

            // Glowing accent rings
            var glowMat = new StandardMaterial3D();
            glowMat.AlbedoColor = accent;
            glowMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            glowMat.EmissionEnabled = true;
            glowMat.Emission = accent;
            glowMat.EmissionEnergyMultiplier = 2f + _danger * 3f;

            int ringCount = 2 + _sector;
            for (int i = 0; i < ringCount; i++)
            {
                var ring = new MeshInstance3D();
                var ringBox = new BoxMesh();
                ringBox.Size = new Vector3(10f, 1f, 8f);
                ring.Mesh = ringBox;
                ring.MaterialOverride = glowMat;
                ring.Position = new Vector3(0, (height / (ringCount + 1)) * (i + 1), 0);
                root.AddChild(ring);
            }

            // Pulsing top beacon
            var beacon = new OmniLight3D();
            beacon.LightColor = accent;
            beacon.LightEnergy = 2f + _danger * 4f;
            beacon.OmniRange = 30f + _danger * 20f;
            beacon.Position = new Vector3(0, height + 2f, 0);
            root.AddChild(beacon);

            return root;
        }

        private void BuildAmbientParticles(SectorData sectorData)
        {
            // Floating particles around the dungeon — embers, sparks, spores depending on sector
            var particles = new GpuParticles3D();
            particles.Amount = 40 + (int)(_danger * 60);
            particles.Lifetime = 8f;
            particles.Preprocess = 4f;
            particles.VisibilityAabb = new Aabb(new Vector3(-150, -10, -150), new Vector3(300, 40, 300));

            var mat = new ParticleProcessMaterial();
            mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
            mat.EmissionBoxExtents = new Vector3(100, 15, 100);
            mat.Direction = new Vector3(0, 1, 0);
            mat.Spread = 20f;
            mat.InitialVelocityMin = 0.3f;
            mat.InitialVelocityMax = 1.0f;
            mat.Gravity = new Vector3(0, 0.1f, 0);
            mat.ScaleMin = 0.1f;
            mat.ScaleMax = 0.3f + _danger * 0.2f;

            // Color based on sector theme
            var particleColor = GetParticleColor(sectorData);
            mat.Color = particleColor;

            particles.ProcessMaterial = mat;

            var mesh = new SphereMesh();
            mesh.Radius = 0.08f;
            mesh.Height = 0.16f;
            mesh.RadialSegments = 4;
            mesh.Rings = 2;

            var meshMat = new StandardMaterial3D();
            meshMat.AlbedoColor = particleColor;
            meshMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            meshMat.EmissionEnabled = true;
            meshMat.Emission = particleColor;
            meshMat.EmissionEnergyMultiplier = 1f + _danger * 2f;
            mesh.Material = meshMat;

            particles.DrawPass1 = mesh;
            particles.Position = new Vector3(0, 10, 0);
            AddChild(particles);
        }

        // --- Color helpers ---

        private Color GetSkyTopColor(SectorData data)
        {
            // Darkens and shifts toward threat as danger increases
            var baseColor = new Color(0.05f, 0.05f, 0.1f); // deep dark blue
            var dangerColor = data.AccentColor * 0.15f;
            dangerColor.A = 1f;
            return baseColor.Lerp(dangerColor, _danger * 0.5f);
        }

        private Color GetSkyBottomColor(SectorData data)
        {
            // Horizon picks up sector accent color
            var baseHorizon = data.WallTint * 0.4f;
            baseHorizon.A = 1f;
            var dangerHorizon = data.AccentColor * 0.3f;
            dangerHorizon.A = 1f;
            return baseHorizon.Lerp(dangerHorizon, _danger);
        }

        private Color GetBackgroundColor(SectorData data)
        {
            return data.FloorTint * 0.2f;
        }

        private Color GetParticleColor(SectorData data)
        {
            // S1 Industrial: warm embers, S2 Toxic: green spores,
            // S3 Military: red sparks, S4 Lab: blue motes, S5 Core: purple energy
            return _sector switch
            {
                1 => new Color(1f, 0.6f, 0.2f, 0.6f),     // orange embers
                2 => new Color(0.3f, 0.9f, 0.2f, 0.5f),   // green spores
                3 => new Color(0.9f, 0.3f, 0.15f, 0.6f),  // red sparks
                4 => new Color(0.3f, 0.6f, 1f, 0.5f),     // blue motes
                5 => new Color(0.7f, 0.3f, 0.9f, 0.7f),   // purple energy
                _ => data.AccentColor * new Color(1, 1, 1, 0.5f),
            };
        }

    }
}
