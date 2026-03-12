using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Static factory creating pre-configured GpuParticles3D nodes for all VFX.
    /// </summary>
    public static class VfxFactory
    {
        private static SphereMesh _sharedDrawPass;

        private static SphereMesh SharedDrawPass
        {
            get
            {
                if (_sharedDrawPass == null)
                {
                    _sharedDrawPass = new SphereMesh();
                    _sharedDrawPass.Radius = 0.06f;
                    _sharedDrawPass.Height = 0.12f;
                    _sharedDrawPass.RadialSegments = 4;
                    _sharedDrawPass.Rings = 2;
                    // Use unshaded emissive material so particles look like glowing sparks, not solid objects
                    var drawMat = new StandardMaterial3D();
                    drawMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                    drawMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                    drawMat.AlbedoColor = Colors.White;
                    drawMat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
                    _sharedDrawPass.Material = drawMat;
                }
                return _sharedDrawPass;
            }
        }

        /// <summary>
        /// Small spark burst on damage hit.
        /// </summary>
        public static GpuParticles3D CreateHitParticles(Color color)
        {
            const string fx = "hit_physical";
            color = VfxConfig.GetColor(fx, color);

            var particles = new GpuParticles3D();
            particles.Amount = VfxConfig.GetInt(fx, "Amount", 12);
            particles.OneShot = true;
            particles.Explosiveness = VfxConfig.GetFloat(fx, "Explosiveness", 0.9f);
            particles.Lifetime = VfxConfig.GetFloat(fx, "Lifetime", 0.3f);
            particles.SpeedScale = VfxConfig.GetFloat(fx, "SpeedScale", 2f);
            particles.DrawPass1 = SharedDrawPass;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 1, 0);
            mat.Spread = VfxConfig.GetFloat(fx, "Spread", 180f);
            mat.InitialVelocityMin = VfxConfig.GetFloat(fx, "InitialVelocityMin", 3f);
            mat.InitialVelocityMax = VfxConfig.GetFloat(fx, "InitialVelocityMax", 6f);
            mat.Gravity = new Vector3(0, VfxConfig.GetFloat(fx, "Gravity", -8f), 0);
            mat.ScaleMin = VfxConfig.GetFloat(fx, "ScaleMin", 0.5f);
            mat.ScaleMax = VfxConfig.GetFloat(fx, "ScaleMax", 1.5f);
            mat.Color = color;

            var colorRamp = new GradientTexture1D();
            var gradient = new Gradient();
            gradient.SetColor(0, color);
            gradient.SetColor(1, new Color(color.R, color.G, color.B, 0));
            colorRamp.Gradient = gradient;
            mat.ColorRamp = colorRamp;

            particles.ProcessMaterial = mat;
            particles.Emitting = true;

            // Self-cleanup
            AutoFree(particles, 0.5f);
            return particles;
        }

        /// <summary>
        /// Larger outward burst on enemy death.
        /// </summary>
        public static GpuParticles3D CreateDeathParticles(Color color)
        {
            const string fx = "death";
            color = VfxConfig.GetColor(fx, color);

            var particles = new GpuParticles3D();
            particles.Amount = VfxConfig.GetInt(fx, "Amount", 24);
            particles.OneShot = true;
            particles.Explosiveness = VfxConfig.GetFloat(fx, "Explosiveness", 0.95f);
            particles.Lifetime = VfxConfig.GetFloat(fx, "Lifetime", 0.6f);
            particles.SpeedScale = VfxConfig.GetFloat(fx, "SpeedScale", 1.5f);
            particles.DrawPass1 = SharedDrawPass;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 1, 0);
            mat.Spread = VfxConfig.GetFloat(fx, "Spread", 180f);
            mat.InitialVelocityMin = VfxConfig.GetFloat(fx, "InitialVelocityMin", 4f);
            mat.InitialVelocityMax = VfxConfig.GetFloat(fx, "InitialVelocityMax", 8f);
            mat.Gravity = new Vector3(0, VfxConfig.GetFloat(fx, "Gravity", -5f), 0);
            mat.ScaleMin = VfxConfig.GetFloat(fx, "ScaleMin", 0.8f);
            mat.ScaleMax = VfxConfig.GetFloat(fx, "ScaleMax", 2f);
            mat.Color = color;

            var colorRamp = new GradientTexture1D();
            var gradient = new Gradient();
            gradient.SetColor(0, new Color(1, 1, 1));
            gradient.SetColor(1, new Color(color.R, color.G, color.B, 0));
            colorRamp.Gradient = gradient;
            mat.ColorRamp = colorRamp;

            particles.ProcessMaterial = mat;
            particles.Emitting = true;

            AutoFree(particles, 1f);
            return particles;
        }

        /// <summary>
        /// Items shooting outward on loot drop.
        /// </summary>
        public static GpuParticles3D CreateLootBurstParticles(Color color)
        {
            const string fx = "loot_burst";

            var particles = new GpuParticles3D();
            particles.Amount = VfxConfig.GetInt(fx, "Amount", 16);
            particles.OneShot = true;
            particles.Explosiveness = VfxConfig.GetFloat(fx, "Explosiveness", 0.85f);
            particles.Lifetime = VfxConfig.GetFloat(fx, "Lifetime", 0.5f);
            particles.SpeedScale = VfxConfig.GetFloat(fx, "SpeedScale", 1.5f);
            particles.DrawPass1 = SharedDrawPass;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 1, 0);
            mat.Spread = VfxConfig.GetFloat(fx, "Spread", 120f);
            mat.InitialVelocityMin = VfxConfig.GetFloat(fx, "InitialVelocityMin", 3f);
            mat.InitialVelocityMax = VfxConfig.GetFloat(fx, "InitialVelocityMax", 5f);
            mat.Gravity = new Vector3(0, VfxConfig.GetFloat(fx, "Gravity", -6f), 0);
            mat.ScaleMin = VfxConfig.GetFloat(fx, "ScaleMin", 0.6f);
            mat.ScaleMax = VfxConfig.GetFloat(fx, "ScaleMax", 1.2f);
            mat.Color = VfxConfig.GetColor(fx, new Color(1f, 0.85f, 0.3f));

            particles.ProcessMaterial = mat;
            particles.Emitting = true;

            AutoFree(particles, 0.8f);
            return particles;
        }

        /// <summary>
        /// Slow floating motes for ambient room atmosphere.
        /// </summary>
        public static GpuParticles3D CreateAmbientParticles(Color color, float radius)
        {
            const string fx = "ambient_particles";
            color = VfxConfig.GetColor(fx, color);

            var particles = new GpuParticles3D();
            particles.Amount = VfxConfig.GetInt(fx, "Amount", 20);
            particles.Lifetime = VfxConfig.GetFloat(fx, "Lifetime", 4f);
            particles.SpeedScale = VfxConfig.GetFloat(fx, "SpeedScale", 0.5f);
            particles.DrawPass1 = SharedDrawPass;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 1, 0);
            mat.Spread = 180f;
            mat.InitialVelocityMin = 0.2f;
            mat.InitialVelocityMax = 0.5f;
            mat.Gravity = new Vector3(0, 0.1f, 0);
            mat.ScaleMin = 0.3f;
            mat.ScaleMax = 0.6f;
            mat.Color = new Color(color.R, color.G, color.B, 0.4f);

            // Emission shape: sphere
            mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
            mat.EmissionSphereRadius = radius;

            var colorRamp = new GradientTexture1D();
            var gradient = new Gradient();
            gradient.SetColor(0, new Color(color.R, color.G, color.B, 0));
            gradient.AddPoint(0.3f, new Color(color.R, color.G, color.B, 0.5f));
            gradient.AddPoint(0.7f, new Color(color.R, color.G, color.B, 0.5f));
            gradient.SetColor(1, new Color(color.R, color.G, color.B, 0));
            colorRamp.Gradient = gradient;
            mat.ColorRamp = colorRamp;

            particles.ProcessMaterial = mat;
            particles.Emitting = true;

            return particles;
        }

        /// <summary>
        /// Flickering orange/yellow upward particles for torches.
        /// </summary>
        public static GpuParticles3D CreateTorchFireParticles()
        {
            const string fx = "torch_fire";

            var particles = new GpuParticles3D();
            particles.Amount = VfxConfig.GetInt(fx, "Amount", 8);
            particles.Lifetime = VfxConfig.GetFloat(fx, "Lifetime", 0.6f);
            particles.SpeedScale = VfxConfig.GetFloat(fx, "SpeedScale", 1f);
            particles.DrawPass1 = SharedDrawPass;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 1, 0);
            mat.Spread = VfxConfig.GetFloat(fx, "Spread", 15f);
            mat.InitialVelocityMin = VfxConfig.GetFloat(fx, "InitialVelocityMin", 1f);
            mat.InitialVelocityMax = VfxConfig.GetFloat(fx, "InitialVelocityMax", 2f);
            mat.Gravity = new Vector3(0, VfxConfig.GetFloat(fx, "Gravity", 0.5f), 0);
            mat.ScaleMin = VfxConfig.GetFloat(fx, "ScaleMin", 0.3f);
            mat.ScaleMax = VfxConfig.GetFloat(fx, "ScaleMax", 0.8f);

            var colorRamp = new GradientTexture1D();
            var gradient = new Gradient();
            gradient.SetColor(0, new Color(1f, 0.9f, 0.3f));
            gradient.AddPoint(0.4f, new Color(1f, 0.5f, 0.1f));
            gradient.SetColor(1, new Color(0.8f, 0.2f, 0.05f, 0));
            colorRamp.Gradient = gradient;
            mat.ColorRamp = colorRamp;

            particles.ProcessMaterial = mat;
            particles.Emitting = true;

            return particles;
        }

        /// <summary>
        /// Rotating swirl for portals.
        /// </summary>
        public static GpuParticles3D CreatePortalParticles(Color color)
        {
            const string fx = "portal_particles";
            color = VfxConfig.GetColor(fx, color);

            var particles = new GpuParticles3D();
            particles.Amount = VfxConfig.GetInt(fx, "Amount", 16);
            particles.Lifetime = VfxConfig.GetFloat(fx, "Lifetime", 2f);
            particles.SpeedScale = VfxConfig.GetFloat(fx, "SpeedScale", 1f);
            particles.DrawPass1 = SharedDrawPass;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 1, 0);
            mat.Spread = 180f;
            mat.InitialVelocityMin = 0.5f;
            mat.InitialVelocityMax = 1.5f;
            mat.Gravity = Vector3.Zero;
            mat.ScaleMin = 0.4f;
            mat.ScaleMax = 0.8f;
            mat.Color = color;
            mat.OrbitVelocityMin = 0.5f;
            mat.OrbitVelocityMax = 1.5f;

            mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
            mat.EmissionSphereRadius = 1.2f;

            var colorRamp = new GradientTexture1D();
            var gradient = new Gradient();
            gradient.SetColor(0, new Color(color.R, color.G, color.B, 0.8f));
            gradient.SetColor(1, new Color(color.R, color.G, color.B, 0));
            colorRamp.Gradient = gradient;
            mat.ColorRamp = colorRamp;

            particles.ProcessMaterial = mat;
            particles.Emitting = true;

            return particles;
        }

        /// <summary>
        /// Persistent glow aura for Rare+ items.
        /// </summary>
        public static GpuParticles3D CreateRarityAura(ItemRarity rarity)
        {
            Color color = rarity switch
            {
                ItemRarity.Rare => new Color(0.2f, 0.4f, 1f),
                ItemRarity.Epic => new Color(0.6f, 0.2f, 0.8f),
                ItemRarity.Legendary => new Color(1f, 0.6f, 0f),
                ItemRarity.Absurd => new Color(1f, 0f, 0.4f),
                _ => new Color(1, 1, 1, 0.3f)
            };

            var particles = new GpuParticles3D();
            particles.Amount = 10;
            particles.Lifetime = 2.0;
            particles.SpeedScale = 0.6f;
            particles.DrawPass1 = SharedDrawPass;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 1, 0);
            mat.Spread = 180f;
            mat.InitialVelocityMin = 0.1f;
            mat.InitialVelocityMax = 0.4f;
            mat.Gravity = new Vector3(0, 0.3f, 0);
            mat.ScaleMin = 0.2f;
            mat.ScaleMax = 0.5f;
            mat.Color = color;
            mat.OrbitVelocityMin = 0.3f;
            mat.OrbitVelocityMax = 0.8f;

            mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
            mat.EmissionSphereRadius = 0.4f;

            var colorRamp = new GradientTexture1D();
            var gradient = new Gradient();
            gradient.SetColor(0, new Color(color.R, color.G, color.B, 0));
            gradient.AddPoint(0.3f, new Color(color.R, color.G, color.B, 0.7f));
            gradient.AddPoint(0.7f, new Color(color.R, color.G, color.B, 0.7f));
            gradient.SetColor(1, new Color(color.R, color.G, color.B, 0));
            colorRamp.Gradient = gradient;
            mat.ColorRamp = colorRamp;

            particles.ProcessMaterial = mat;
            particles.Emitting = true;

            return particles;
        }

        /// <summary>
        /// Upward sparkle burst for room clear celebration.
        /// </summary>
        public static GpuParticles3D CreateCelebrationParticles()
        {
            const string fx = "celebration";

            var particles = new GpuParticles3D();
            particles.Amount = VfxConfig.GetInt(fx, "Amount", 30);
            particles.OneShot = true;
            particles.Explosiveness = VfxConfig.GetFloat(fx, "Explosiveness", 0.8f);
            particles.Lifetime = VfxConfig.GetFloat(fx, "Lifetime", 1.2f);
            particles.SpeedScale = VfxConfig.GetFloat(fx, "SpeedScale", 1.2f);
            particles.DrawPass1 = SharedDrawPass;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 1, 0);
            mat.Spread = VfxConfig.GetFloat(fx, "Spread", 60f);
            mat.InitialVelocityMin = VfxConfig.GetFloat(fx, "InitialVelocityMin", 4f);
            mat.InitialVelocityMax = VfxConfig.GetFloat(fx, "InitialVelocityMax", 8f);
            mat.Gravity = new Vector3(0, VfxConfig.GetFloat(fx, "Gravity", -3f), 0);
            mat.ScaleMin = VfxConfig.GetFloat(fx, "ScaleMin", 0.4f);
            mat.ScaleMax = VfxConfig.GetFloat(fx, "ScaleMax", 1.2f);

            var colorRamp = new GradientTexture1D();
            var gradient = new Gradient();
            gradient.SetColor(0, new Color(1f, 1f, 0.5f));
            gradient.AddPoint(0.3f, new Color(1f, 0.85f, 0.2f));
            gradient.SetColor(1, new Color(1f, 0.4f, 0.1f, 0));
            colorRamp.Gradient = gradient;
            mat.ColorRamp = colorRamp;

            particles.ProcessMaterial = mat;
            particles.Emitting = true;

            AutoFree(particles, 1.5f);
            return particles;
        }

        /// <summary>
        /// Brief trail on item collect.
        /// </summary>
        public static GpuParticles3D CreatePickupTrail(Color color)
        {
            const string fx = "pickup_trail";
            color = VfxConfig.GetColor(fx, color);

            var particles = new GpuParticles3D();
            particles.Amount = VfxConfig.GetInt(fx, "Amount", 8);
            particles.OneShot = true;
            particles.Explosiveness = VfxConfig.GetFloat(fx, "Explosiveness", 0.7f);
            particles.Lifetime = VfxConfig.GetFloat(fx, "Lifetime", 0.4f);
            particles.SpeedScale = VfxConfig.GetFloat(fx, "SpeedScale", 2f);
            particles.DrawPass1 = SharedDrawPass;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 1, 0);
            mat.Spread = VfxConfig.GetFloat(fx, "Spread", 90f);
            mat.InitialVelocityMin = VfxConfig.GetFloat(fx, "InitialVelocityMin", 2f);
            mat.InitialVelocityMax = VfxConfig.GetFloat(fx, "InitialVelocityMax", 4f);
            mat.Gravity = new Vector3(0, VfxConfig.GetFloat(fx, "Gravity", -2f), 0);
            mat.ScaleMin = VfxConfig.GetFloat(fx, "ScaleMin", 0.3f);
            mat.ScaleMax = VfxConfig.GetFloat(fx, "ScaleMax", 0.6f);
            mat.Color = color;

            particles.ProcessMaterial = mat;
            particles.Emitting = true;

            AutoFree(particles, 0.6f);
            return particles;
        }

        /// <summary>
        /// Larger impact burst for projectile hits.
        /// </summary>
        public static GpuParticles3D CreateImpactBurst(Color color)
        {
            const string fx = "impact_burst";
            color = VfxConfig.GetColor(fx, color);

            var particles = new GpuParticles3D();
            particles.Amount = VfxConfig.GetInt(fx, "Amount", 18);
            particles.OneShot = true;
            particles.Explosiveness = VfxConfig.GetFloat(fx, "Explosiveness", 0.95f);
            particles.Lifetime = VfxConfig.GetFloat(fx, "Lifetime", 0.35f);
            particles.SpeedScale = VfxConfig.GetFloat(fx, "SpeedScale", 2f);
            particles.DrawPass1 = SharedDrawPass;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 1, 0);
            mat.Spread = VfxConfig.GetFloat(fx, "Spread", 180f);
            mat.InitialVelocityMin = VfxConfig.GetFloat(fx, "InitialVelocityMin", 4f);
            mat.InitialVelocityMax = VfxConfig.GetFloat(fx, "InitialVelocityMax", 8f);
            mat.Gravity = new Vector3(0, VfxConfig.GetFloat(fx, "Gravity", -6f), 0);
            mat.ScaleMin = VfxConfig.GetFloat(fx, "ScaleMin", 0.6f);
            mat.ScaleMax = VfxConfig.GetFloat(fx, "ScaleMax", 1.8f);
            mat.Color = color;

            var colorRamp = new GradientTexture1D();
            var gradient = new Gradient();
            gradient.SetColor(0, new Color(1, 1, 1));
            gradient.SetColor(1, new Color(color.R, color.G, color.B, 0));
            colorRamp.Gradient = gradient;
            mat.ColorRamp = colorRamp;

            particles.ProcessMaterial = mat;
            particles.Emitting = true;

            AutoFree(particles, 0.6f);
            return particles;
        }

        /// <summary>
        /// Flat rotating arcane circle at feet (for MagicUser cast).
        /// </summary>
        public static Node3D CreateArcaneCircle(Color color)
        {
            var root = new Node3D();
            root.Name = "ArcaneCircle";

            // Create ring mesh (flat torus)
            var meshInst = new MeshInstance3D();
            var torus = new TorusMesh();
            torus.InnerRadius = 0.6f;
            torus.OuterRadius = 0.8f;
            torus.Rings = 20;
            torus.RingSegments = 12;
            meshInst.Mesh = torus;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(color.R, color.G, color.B, 0.6f);
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.EmissionEnabled = true;
            mat.Emission = color;
            mat.EmissionEnergyMultiplier = 2.5f;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
            meshInst.MaterialOverride = mat;
            meshInst.Position = Vector3.Up * 0.05f;
            root.AddChild(meshInst);

            // Animate: spin + fade
            root.TreeEntered += () =>
            {
                if (!GodotObject.IsInstanceValid(root) || !root.IsInsideTree()) return;
                var tween = root.CreateTween();
                if (tween == null) return;
                tween.SetParallel(true);
                tween.TweenProperty(root, "rotation_degrees:y", 180f, 0.6f);
                tween.TweenProperty(mat, "albedo_color:a", 0f, 0.6f)
                    .SetTrans(Tween.TransitionType.Quad)
                    .SetEase(Tween.EaseType.In);
                tween.SetParallel(false);
                tween.TweenCallback(Callable.From(() =>
                {
                    if (GodotObject.IsInstanceValid(root)) root.QueueFree();
                }));
            };

            return root;
        }

        /// <summary>
        /// Expanding ring shockwave effect.
        /// </summary>
        public static Node3D CreateShockwaveRing(Color color)
        {
            var root = new Node3D();
            root.Name = "ShockwaveRing";

            var meshInst = new MeshInstance3D();
            var torus = new TorusMesh();
            torus.InnerRadius = 0.15f;
            torus.OuterRadius = 0.25f;
            torus.Rings = 16;
            torus.RingSegments = 8;
            meshInst.Mesh = torus;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(color.R, color.G, color.B, 0.7f);
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.EmissionEnabled = true;
            mat.Emission = color;
            mat.EmissionEnergyMultiplier = 2f;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
            meshInst.MaterialOverride = mat;
            root.AddChild(meshInst);

            // Animate: expand + fade
            root.TreeEntered += () =>
            {
                if (!GodotObject.IsInstanceValid(root) || !root.IsInsideTree()) return;
                var tween = root.CreateTween();
                if (tween == null) return;
                tween.SetParallel(true);
                tween.TweenProperty(root, "scale", Vector3.One * 5f, 0.3f)
                    .SetTrans(Tween.TransitionType.Quad)
                    .SetEase(Tween.EaseType.Out);
                tween.TweenProperty(mat, "albedo_color:a", 0f, 0.3f)
                    .SetTrans(Tween.TransitionType.Quad)
                    .SetEase(Tween.EaseType.In);
                tween.SetParallel(false);
                tween.TweenCallback(Callable.From(() =>
                {
                    if (GodotObject.IsInstanceValid(root)) root.QueueFree();
                }));
            };

            return root;
        }

        /// <summary>
        /// Small note-shaped particles for NecroBard.
        /// </summary>
        public static GpuParticles3D CreateMusicNotes(Color color)
        {
            const string fx = "music_notes";
            color = VfxConfig.GetColor(fx, color);

            var particles = new GpuParticles3D();
            particles.Amount = VfxConfig.GetInt(fx, "Amount", 8);
            particles.OneShot = true;
            particles.Explosiveness = VfxConfig.GetFloat(fx, "Explosiveness", 0.6f);
            particles.Lifetime = VfxConfig.GetFloat(fx, "Lifetime", 0.8f);
            particles.SpeedScale = VfxConfig.GetFloat(fx, "SpeedScale", 1f);
            particles.DrawPass1 = SharedDrawPass;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 1, 0);
            mat.Spread = VfxConfig.GetFloat(fx, "Spread", 90f);
            mat.InitialVelocityMin = VfxConfig.GetFloat(fx, "InitialVelocityMin", 1f);
            mat.InitialVelocityMax = VfxConfig.GetFloat(fx, "InitialVelocityMax", 3f);
            mat.Gravity = new Vector3(0, VfxConfig.GetFloat(fx, "Gravity", 1f), 0);
            mat.ScaleMin = VfxConfig.GetFloat(fx, "ScaleMin", 0.5f);
            mat.ScaleMax = VfxConfig.GetFloat(fx, "ScaleMax", 1.2f);
            mat.Color = color;

            var colorRamp = new GradientTexture1D();
            var gradient = new Gradient();
            gradient.SetColor(0, new Color(color.R, color.G, color.B, 0.9f));
            gradient.SetColor(1, new Color(color.R, color.G, color.B, 0));
            colorRamp.Gradient = gradient;
            mat.ColorRamp = colorRamp;

            particles.ProcessMaterial = mat;
            particles.Emitting = true;

            AutoFree(particles, 1f);
            return particles;
        }

        /// <summary>
        /// Tall light pillar for Epic+ ground drops. Color by rarity.
        /// </summary>
        public static Node3D CreateLightPillar(ItemRarity rarity)
        {
            Color color = rarity switch
            {
                ItemRarity.Epic => new Color(0.6f, 0.2f, 0.8f),
                ItemRarity.Legendary => new Color(1f, 0.6f, 0f),
                ItemRarity.Absurd => new Color(1f, 0f, 0.4f),
                _ => new Color(0.6f, 0.2f, 0.8f)
            };

            var root = new Node3D();
            root.Name = "LightPillar";

            // Tall emissive cylinder (tapers from 0.4 base to 0.15 top)
            var meshInst = new MeshInstance3D();
            var cylinder = new CylinderMesh();
            cylinder.TopRadius = 0.15f;
            cylinder.BottomRadius = 0.4f;
            cylinder.Height = 12f;
            cylinder.RadialSegments = 8;
            cylinder.Rings = 1;
            meshInst.Mesh = cylinder;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(color.R, color.G, color.B, 0.5f);
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.EmissionEnabled = true;
            mat.Emission = color;
            mat.EmissionEnergyMultiplier = 3f;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
            meshInst.MaterialOverride = mat;
            meshInst.Position = Vector3.Up * 6f; // Center the 12-unit cylinder
            root.AddChild(meshInst);

            // Rising particles alongside
            var particles = new GpuParticles3D();
            particles.Amount = 20;
            particles.Lifetime = 2.0;
            particles.SpeedScale = 1f;
            particles.DrawPass1 = SharedDrawPass;

            var pMat = new ParticleProcessMaterial();
            pMat.Direction = new Vector3(0, 1, 0);
            pMat.Spread = 15f;
            pMat.InitialVelocityMin = 2f;
            pMat.InitialVelocityMax = 5f;
            pMat.Gravity = Vector3.Zero;
            pMat.ScaleMin = 0.3f;
            pMat.ScaleMax = 0.8f;
            pMat.Color = color;
            pMat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
            pMat.EmissionSphereRadius = 0.3f;

            var colorRamp = new GradientTexture1D();
            var gradient = new Gradient();
            gradient.SetColor(0, new Color(color.R, color.G, color.B, 0.8f));
            gradient.SetColor(1, new Color(color.R, color.G, color.B, 0));
            colorRamp.Gradient = gradient;
            pMat.ColorRamp = colorRamp;

            particles.ProcessMaterial = pMat;
            particles.Emitting = true;
            root.AddChild(particles);

            // Lifecycle tween: fade in → hold → fade out → free
            root.TreeEntered += () =>
            {
                if (!GodotObject.IsInstanceValid(root) || !root.IsInsideTree()) return;
                var tween = root.CreateTween();
                if (tween == null) return;
                mat.AlbedoColor = new Color(color.R, color.G, color.B, 0f);
                tween.TweenProperty(mat, "albedo_color:a", 0.5f, 0.2f);
                tween.TweenInterval(2.5f);
                tween.TweenProperty(mat, "albedo_color:a", 0f, 0.8f);
                tween.TweenCallback(Callable.From(() =>
                {
                    if (GodotObject.IsInstanceValid(root)) root.QueueFree();
                }));
            };

            return root;
        }

        /// <summary>
        /// Green rising sparkles for healing.
        /// </summary>
        public static GpuParticles3D CreateHealParticles(Color color = default)
        {
            const string fx = "heal";
            if (color == default) color = new Color(0.2f, 1f, 0.4f);
            color = VfxConfig.GetColor(fx, color);

            var particles = new GpuParticles3D();
            particles.Amount = VfxConfig.GetInt(fx, "Amount", 20);
            particles.OneShot = true;
            particles.Explosiveness = VfxConfig.GetFloat(fx, "Explosiveness", 0.5f);
            particles.Lifetime = VfxConfig.GetFloat(fx, "Lifetime", 0.8f);
            particles.SpeedScale = VfxConfig.GetFloat(fx, "SpeedScale", 1f);
            particles.DrawPass1 = SharedDrawPass;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 1, 0);
            mat.Spread = VfxConfig.GetFloat(fx, "Spread", 30f);
            mat.InitialVelocityMin = VfxConfig.GetFloat(fx, "InitialVelocityMin", 1.5f);
            mat.InitialVelocityMax = VfxConfig.GetFloat(fx, "InitialVelocityMax", 3f);
            mat.Gravity = new Vector3(0, VfxConfig.GetFloat(fx, "Gravity", 0.5f), 0);
            mat.ScaleMin = VfxConfig.GetFloat(fx, "ScaleMin", 0.3f);
            mat.ScaleMax = VfxConfig.GetFloat(fx, "ScaleMax", 0.8f);
            mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
            mat.EmissionSphereRadius = 0.6f;

            var colorRamp = new GradientTexture1D();
            var gradient = new Gradient();
            gradient.SetColor(0, new Color(1, 1, 1, 0.9f));
            gradient.AddPoint(0.3f, color);
            gradient.SetColor(1, new Color(color.R, color.G, color.B, 0));
            colorRamp.Gradient = gradient;
            mat.ColorRamp = colorRamp;

            particles.ProcessMaterial = mat;
            particles.Emitting = true;

            AutoFree(particles, 1.2f);
            return particles;
        }

        /// <summary>
        /// Persistent trail particles for dash/movement abilities.
        /// Emits continuously while active — caller must stop and free.
        /// </summary>
        public static GpuParticles3D CreateDashTrail(Color color)
        {
            const string fx = "dash_trail";
            color = VfxConfig.GetColor(fx, color);

            var particles = new GpuParticles3D();
            particles.Amount = VfxConfig.GetInt(fx, "Amount", 12);
            particles.Lifetime = VfxConfig.GetFloat(fx, "Lifetime", 0.4f);
            particles.SpeedScale = VfxConfig.GetFloat(fx, "SpeedScale", 1f);
            particles.DrawPass1 = SharedDrawPass;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 0.5f, 0);
            mat.Spread = VfxConfig.GetFloat(fx, "Spread", 45f);
            mat.InitialVelocityMin = VfxConfig.GetFloat(fx, "InitialVelocityMin", 0.5f);
            mat.InitialVelocityMax = VfxConfig.GetFloat(fx, "InitialVelocityMax", 1.5f);
            mat.Gravity = new Vector3(0, VfxConfig.GetFloat(fx, "Gravity", -1f), 0);
            mat.ScaleMin = VfxConfig.GetFloat(fx, "ScaleMin", 0.4f);
            mat.ScaleMax = VfxConfig.GetFloat(fx, "ScaleMax", 1.0f);

            var colorRamp = new GradientTexture1D();
            var gradient = new Gradient();
            gradient.SetColor(0, new Color(color.R, color.G, color.B, 0.8f));
            gradient.SetColor(1, new Color(color.R, color.G, color.B, 0));
            colorRamp.Gradient = gradient;
            mat.ColorRamp = colorRamp;

            particles.ProcessMaterial = mat;
            particles.Emitting = true;

            return particles;
        }

        /// <summary>
        /// Lingering poison cloud AoE.
        /// </summary>
        public static GpuParticles3D CreatePoisonCloud(Color color = default, float radius = 2f)
        {
            const string fx = "poison_cloud";
            if (color == default) color = new Color(0.3f, 0.9f, 0.2f, 0.6f);
            color = VfxConfig.GetColor(fx, color);

            var particles = new GpuParticles3D();
            particles.Amount = VfxConfig.GetInt(fx, "Amount", 24);
            particles.Lifetime = VfxConfig.GetFloat(fx, "Lifetime", 2f);
            particles.SpeedScale = VfxConfig.GetFloat(fx, "SpeedScale", 0.6f);
            particles.DrawPass1 = SharedDrawPass;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 0.5f, 0);
            mat.Spread = 180f;
            mat.InitialVelocityMin = 0.2f;
            mat.InitialVelocityMax = 0.8f;
            mat.Gravity = new Vector3(0, 0.2f, 0);
            mat.ScaleMin = 1.0f;
            mat.ScaleMax = 2.5f;
            mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
            mat.EmissionSphereRadius = radius;

            var colorRamp = new GradientTexture1D();
            var gradient = new Gradient();
            gradient.SetColor(0, new Color(color.R, color.G, color.B, 0));
            gradient.AddPoint(0.2f, color);
            gradient.AddPoint(0.7f, color);
            gradient.SetColor(1, new Color(color.R, color.G, color.B, 0));
            colorRamp.Gradient = gradient;
            mat.ColorRamp = colorRamp;

            particles.ProcessMaterial = mat;
            particles.Emitting = true;

            return particles;
        }

        /// <summary>
        /// Sharp ice crystal burst for freeze effects.
        /// </summary>
        public static GpuParticles3D CreateFreezeBurst(Color color = default)
        {
            const string fx = "freeze_burst";
            if (color == default) color = new Color(0.5f, 0.85f, 1f);
            color = VfxConfig.GetColor(fx, color);

            var particles = new GpuParticles3D();
            particles.Amount = VfxConfig.GetInt(fx, "Amount", 16);
            particles.OneShot = true;
            particles.Explosiveness = VfxConfig.GetFloat(fx, "Explosiveness", 0.95f);
            particles.Lifetime = VfxConfig.GetFloat(fx, "Lifetime", 0.5f);
            particles.SpeedScale = VfxConfig.GetFloat(fx, "SpeedScale", 1.5f);
            particles.DrawPass1 = SharedDrawPass;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 1, 0);
            mat.Spread = VfxConfig.GetFloat(fx, "Spread", 180f);
            mat.InitialVelocityMin = VfxConfig.GetFloat(fx, "InitialVelocityMin", 3f);
            mat.InitialVelocityMax = VfxConfig.GetFloat(fx, "InitialVelocityMax", 6f);
            mat.Gravity = new Vector3(0, VfxConfig.GetFloat(fx, "Gravity", -2f), 0);
            mat.ScaleMin = VfxConfig.GetFloat(fx, "ScaleMin", 0.5f);
            mat.ScaleMax = VfxConfig.GetFloat(fx, "ScaleMax", 1.5f);

            var colorRamp = new GradientTexture1D();
            var gradient = new Gradient();
            gradient.SetColor(0, Colors.White);
            gradient.AddPoint(0.3f, color);
            gradient.SetColor(1, new Color(color.R, color.G, color.B, 0));
            colorRamp.Gradient = gradient;
            mat.ColorRamp = colorRamp;

            particles.ProcessMaterial = mat;
            particles.Emitting = true;

            AutoFree(particles, 0.8f);
            return particles;
        }

        /// <summary>
        /// Electric sparks for lightning/stun effects.
        /// </summary>
        public static GpuParticles3D CreateElectricSparks(Color color = default)
        {
            const string fx = "electric_sparks";
            if (color == default) color = new Color(0.7f, 0.85f, 1f);
            color = VfxConfig.GetColor(fx, color);

            var particles = new GpuParticles3D();
            particles.Amount = VfxConfig.GetInt(fx, "Amount", 14);
            particles.OneShot = true;
            particles.Explosiveness = VfxConfig.GetFloat(fx, "Explosiveness", 0.9f);
            particles.Lifetime = VfxConfig.GetFloat(fx, "Lifetime", 0.25f);
            particles.SpeedScale = VfxConfig.GetFloat(fx, "SpeedScale", 3f);
            particles.DrawPass1 = SharedDrawPass;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 1, 0);
            mat.Spread = VfxConfig.GetFloat(fx, "Spread", 180f);
            mat.InitialVelocityMin = VfxConfig.GetFloat(fx, "InitialVelocityMin", 5f);
            mat.InitialVelocityMax = VfxConfig.GetFloat(fx, "InitialVelocityMax", 10f);
            mat.Gravity = Vector3.Zero;
            mat.ScaleMin = VfxConfig.GetFloat(fx, "ScaleMin", 0.2f);
            mat.ScaleMax = VfxConfig.GetFloat(fx, "ScaleMax", 0.6f);
            mat.DampingMin = 6f;
            mat.DampingMax = 10f;

            var colorRamp = new GradientTexture1D();
            var gradient = new Gradient();
            gradient.SetColor(0, Colors.White);
            gradient.SetColor(1, new Color(color.R, color.G, color.B, 0));
            colorRamp.Gradient = gradient;
            mat.ColorRamp = colorRamp;

            particles.ProcessMaterial = mat;
            particles.Emitting = true;

            AutoFree(particles, 0.5f);
            return particles;
        }

        /// <summary>
        /// Muzzle flash burst for gun fire.
        /// </summary>
        public static GpuParticles3D CreateMuzzleFlash(Color color = default)
        {
            const string fx = "muzzle_flash";
            if (color == default) color = new Color(1f, 0.85f, 0.3f);
            color = VfxConfig.GetColor(fx, color);

            var particles = new GpuParticles3D();
            particles.Amount = VfxConfig.GetInt(fx, "Amount", 8);
            particles.OneShot = true;
            particles.Explosiveness = VfxConfig.GetFloat(fx, "Explosiveness", 1.0f);
            particles.Lifetime = VfxConfig.GetFloat(fx, "Lifetime", 0.12f);
            particles.SpeedScale = VfxConfig.GetFloat(fx, "SpeedScale", 3f);
            particles.DrawPass1 = SharedDrawPass;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 0, -1);
            mat.Spread = VfxConfig.GetFloat(fx, "Spread", 25f);
            mat.InitialVelocityMin = VfxConfig.GetFloat(fx, "InitialVelocityMin", 4f);
            mat.InitialVelocityMax = VfxConfig.GetFloat(fx, "InitialVelocityMax", 8f);
            mat.Gravity = Vector3.Zero;
            mat.ScaleMin = VfxConfig.GetFloat(fx, "ScaleMin", 0.5f);
            mat.ScaleMax = VfxConfig.GetFloat(fx, "ScaleMax", 1.5f);

            var colorRamp = new GradientTexture1D();
            var gradient = new Gradient();
            gradient.SetColor(0, Colors.White);
            gradient.SetColor(1, new Color(color.R, color.G, color.B, 0));
            colorRamp.Gradient = gradient;
            mat.ColorRamp = colorRamp;

            particles.ProcessMaterial = mat;
            particles.Emitting = true;

            AutoFree(particles, 0.3f);
            return particles;
        }

        // =================================================================
        //  CELEBRATION-SPECIFIC PARTICLES
        //  Used by CelebrationVfxManager for tiered emotional responses.
        // =================================================================

        /// <summary>
        /// Sad gray puff that falls DOWN. For Junk tier — the anti-celebration.
        /// Particles droop earthward like they're disappointed too.
        /// </summary>
        public static GpuParticles3D CreateSadPuff()
        {
            const string fx = "sad_puff";

            var particles = new GpuParticles3D();
            particles.Amount = VfxConfig.GetInt(fx, "Amount", 8);
            particles.OneShot = true;
            particles.Explosiveness = VfxConfig.GetFloat(fx, "Explosiveness", 0.5f);
            particles.Lifetime = VfxConfig.GetFloat(fx, "Lifetime", 1.0f);
            particles.SpeedScale = VfxConfig.GetFloat(fx, "SpeedScale", 0.7f);
            particles.DrawPass1 = SharedDrawPass;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, -1, 0); // DOWN — the sadness
            mat.Spread = VfxConfig.GetFloat(fx, "Spread", 60f);
            mat.InitialVelocityMin = VfxConfig.GetFloat(fx, "InitialVelocityMin", 0.5f);
            mat.InitialVelocityMax = VfxConfig.GetFloat(fx, "InitialVelocityMax", 1.5f);
            mat.Gravity = new Vector3(0, VfxConfig.GetFloat(fx, "Gravity", -4f), 0);
            mat.ScaleMin = VfxConfig.GetFloat(fx, "ScaleMin", 0.8f);
            mat.ScaleMax = VfxConfig.GetFloat(fx, "ScaleMax", 1.5f);

            var colorRamp = new GradientTexture1D();
            var gradient = new Gradient();
            gradient.SetColor(0, new Color(0.5f, 0.5f, 0.5f, 0.6f));
            gradient.SetColor(1, new Color(0.3f, 0.3f, 0.3f, 0f));
            colorRamp.Gradient = gradient;
            mat.ColorRamp = colorRamp;

            particles.ProcessMaterial = mat;
            particles.Emitting = true;
            AutoFree(particles, 1.5f);
            return particles;
        }

        /// <summary>
        /// Configurable celebration burst. Amount and color scale with tier.
        /// For Decent→Absurd — the satisfying explosion of sparks.
        /// </summary>
        public static GpuParticles3D CreateCelebrationBurst(Color color, int amount)
        {
            const string fx = "celebration_burst";
            color = VfxConfig.GetColor(fx, color);

            var particles = new GpuParticles3D();
            particles.Amount = Mathf.Max(amount, VfxConfig.GetInt(fx, "Amount", amount));
            particles.OneShot = true;
            particles.Explosiveness = VfxConfig.GetFloat(fx, "Explosiveness", 0.85f);
            particles.Lifetime = VfxConfig.GetFloat(fx, "Lifetime", 1.0f) + (amount / 100f);
            particles.SpeedScale = VfxConfig.GetFloat(fx, "SpeedScale", 1.5f);
            particles.DrawPass1 = SharedDrawPass;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 1, 0);
            mat.Spread = 180f;
            mat.InitialVelocityMin = 4f;
            mat.InitialVelocityMax = 10f + (amount / 20f);
            mat.Gravity = new Vector3(0, -4, 0);
            mat.ScaleMin = 0.3f;
            mat.ScaleMax = 1.5f;
            mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
            mat.EmissionSphereRadius = 0.5f;

            var colorRamp = new GradientTexture1D();
            var gradient = new Gradient();
            gradient.SetColor(0, Colors.White);
            gradient.AddPoint(0.15f, color);
            gradient.AddPoint(0.6f, new Color(color.R * 0.8f, color.G * 0.8f, color.B * 0.8f, 0.8f));
            gradient.SetColor(1, new Color(color.R, color.G, color.B, 0f));
            colorRamp.Gradient = gradient;
            mat.ColorRamp = colorRamp;

            particles.ProcessMaterial = mat;
            particles.Emitting = true;
            AutoFree(particles, 2f + (amount / 80f));
            return particles;
        }

        /// <summary>
        /// Confetti storm — particles rain DOWN from above with horizontal spread.
        /// For Legendary/Absurd tiers. Vampire Survivors "screen full of stuff" energy.
        /// Art plug-in: swap SharedDrawPass for a quad mesh with confetti texture.
        /// </summary>
        public static GpuParticles3D CreateConfettiStorm(Color color, int amount)
        {
            const string fx = "confetti_storm";
            color = VfxConfig.GetColor(fx, color);

            var particles = new GpuParticles3D();
            particles.Amount = Mathf.Max(amount, VfxConfig.GetInt(fx, "Amount", amount));
            particles.OneShot = true;
            particles.Explosiveness = VfxConfig.GetFloat(fx, "Explosiveness", 0.3f);
            particles.Lifetime = VfxConfig.GetFloat(fx, "Lifetime", 2.5f);
            particles.SpeedScale = VfxConfig.GetFloat(fx, "SpeedScale", 1f);
            particles.DrawPass1 = SharedDrawPass; // TODO: Replace with confetti quad mesh + texture

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, -1, 0); // Rain down
            mat.Spread = 80f;
            mat.InitialVelocityMin = 1f;
            mat.InitialVelocityMax = 4f;
            mat.Gravity = new Vector3(0, -2f, 0);
            mat.ScaleMin = 0.3f;
            mat.ScaleMax = 1.0f;

            // Wide horizontal spread
            mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
            mat.EmissionSphereRadius = 5f;

            // Tumble effect — angular velocity makes confetti spin
            mat.AngularVelocityMin = -200f;
            mat.AngularVelocityMax = 200f;

            // Color variation: main color with slight randomization
            var colorRamp = new GradientTexture1D();
            var gradient = new Gradient();
            gradient.SetColor(0, new Color(color.R, color.G, color.B, 0.9f));
            gradient.AddPoint(0.7f, new Color(color.R * 0.9f, color.G * 0.9f, color.B * 0.9f, 0.8f));
            gradient.SetColor(1, new Color(color.R, color.G, color.B, 0f));
            colorRamp.Gradient = gradient;
            mat.ColorRamp = colorRamp;

            // Slight hue variation for visual richness
            mat.HueVariationMin = -0.08f;
            mat.HueVariationMax = 0.08f;

            particles.ProcessMaterial = mat;
            particles.Emitting = true;
            AutoFree(particles, 3.5f);
            return particles;
        }

        /// <summary>
        /// Orbiting sparkle ring — persistent glow ring that orbits the drop point.
        /// For Exciting+ tiers. POE2 style "this item is special" indicator.
        /// Art plug-in: Replace sphere mesh with star/diamond texture.
        /// </summary>
        public static GpuParticles3D CreateOrbitingSparkles(Color color, float radius = 0.8f)
        {
            const string fx = "orbiting_sparkles";
            color = VfxConfig.GetColor(fx, color);

            var particles = new GpuParticles3D();
            particles.Amount = VfxConfig.GetInt(fx, "Amount", 12);
            particles.Lifetime = VfxConfig.GetFloat(fx, "Lifetime", 3f);
            particles.SpeedScale = VfxConfig.GetFloat(fx, "SpeedScale", 0.8f);
            particles.DrawPass1 = SharedDrawPass;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 0.5f, 0);
            mat.Spread = 10f;
            mat.InitialVelocityMin = 0.1f;
            mat.InitialVelocityMax = 0.3f;
            mat.Gravity = Vector3.Zero;
            mat.ScaleMin = 0.2f;
            mat.ScaleMax = 0.6f;
            mat.Color = color;
            mat.OrbitVelocityMin = 0.8f;
            mat.OrbitVelocityMax = 1.2f;

            mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
            mat.EmissionSphereRadius = radius;

            var colorRamp = new GradientTexture1D();
            var gradient = new Gradient();
            gradient.SetColor(0, new Color(1f, 1f, 1f, 0f));
            gradient.AddPoint(0.15f, new Color(color.R, color.G, color.B, 0.9f));
            gradient.AddPoint(0.85f, new Color(color.R, color.G, color.B, 0.9f));
            gradient.SetColor(1, new Color(color.R, color.G, color.B, 0f));
            colorRamp.Gradient = gradient;
            mat.ColorRamp = colorRamp;

            particles.ProcessMaterial = mat;
            particles.Emitting = true;
            AutoFree(particles, 4f);
            return particles;
        }

        /// <summary>
        /// Ground-level sparks that scatter outward along the floor.
        /// For Exciting+ tiers. Adds grounded weight to celebrations.
        /// </summary>
        public static GpuParticles3D CreateGroundSparks(Color color, int amount = 20)
        {
            const string fx = "ground_sparks";
            color = VfxConfig.GetColor(fx, color);

            var particles = new GpuParticles3D();
            particles.Amount = Mathf.Max(amount, VfxConfig.GetInt(fx, "Amount", amount));
            particles.OneShot = true;
            particles.Explosiveness = VfxConfig.GetFloat(fx, "Explosiveness", 0.9f);
            particles.Lifetime = VfxConfig.GetFloat(fx, "Lifetime", 0.6f);
            particles.SpeedScale = VfxConfig.GetFloat(fx, "SpeedScale", 2f);
            particles.DrawPass1 = SharedDrawPass;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 0.2f, 0); // Nearly horizontal
            mat.Spread = 180f;
            mat.InitialVelocityMin = 5f;
            mat.InitialVelocityMax = 12f;
            mat.Gravity = new Vector3(0, -15, 0); // Heavy — sparks hit the ground fast
            mat.ScaleMin = 0.2f;
            mat.ScaleMax = 0.5f;
            mat.DampingMin = 3f;
            mat.DampingMax = 6f;

            var colorRamp = new GradientTexture1D();
            var gradient = new Gradient();
            gradient.SetColor(0, Colors.White);
            gradient.AddPoint(0.2f, color);
            gradient.SetColor(1, new Color(color.R * 0.5f, color.G * 0.5f, color.B * 0.5f, 0f));
            colorRamp.Gradient = gradient;
            mat.ColorRamp = colorRamp;

            particles.ProcessMaterial = mat;
            particles.Emitting = true;
            AutoFree(particles, 1f);
            return particles;
        }

        // =================================================================
        //  COMBAT VFX — AoE indicators, arcs, stun, slash, auras
        // =================================================================

        /// <summary>
        /// Flat ground ring showing AoE ability radius. Fades out over duration.
        /// </summary>
        public static Node3D CreateAoEIndicator(Color color, float radius, float duration = 0.6f)
        {
            var root = new Node3D();
            root.Name = "AoEIndicator";

            var meshInst = new MeshInstance3D();
            var torus = new TorusMesh();
            torus.InnerRadius = radius - 0.08f;
            torus.OuterRadius = radius;
            torus.Rings = 32;
            torus.RingSegments = 4;
            meshInst.Mesh = torus;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(color.R, color.G, color.B, 0.7f);
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.EmissionEnabled = true;
            mat.Emission = color;
            mat.EmissionEnergyMultiplier = 2f;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
            meshInst.MaterialOverride = mat;
            meshInst.Position = Vector3.Up * 0.05f;
            root.AddChild(meshInst);

            root.TreeEntered += () =>
            {
                if (!GodotObject.IsInstanceValid(root) || !root.IsInsideTree()) return;
                var tween = root.CreateTween();
                if (tween == null) return;
                tween.SetParallel(true);
                // Expand slightly
                root.Scale = Vector3.One * 0.8f;
                tween.TweenProperty(root, "scale", Vector3.One, duration * 0.3f)
                    .SetTrans(Tween.TransitionType.Quad)
                    .SetEase(Tween.EaseType.Out);
                tween.TweenProperty(mat, "albedo_color:a", 0f, duration)
                    .SetTrans(Tween.TransitionType.Quad)
                    .SetEase(Tween.EaseType.In);
                tween.SetParallel(false);
                tween.TweenCallback(Callable.From(() =>
                {
                    if (GodotObject.IsInstanceValid(root)) root.QueueFree();
                }));
            };

            return root;
        }

        /// <summary>
        /// Lightning arc bolt between two world positions. Tween-based box mesh
        /// that stretches between points and fades out quickly.
        /// </summary>
        public static Node3D CreateLightningArc(Vector3 from, Vector3 to, Color color = default)
        {
            if (color == default) color = new Color(0.7f, 0.9f, 1f);

            var root = new Node3D();
            root.Name = "LightningArc";

            float length = from.DistanceTo(to);
            Vector3 midpoint = (from + to) / 2f;
            Vector3 dir = (to - from).Normalized();

            // Main bolt — jagged segments
            int segments = Mathf.Max(2, (int)(length / 0.8f));
            for (int i = 0; i < segments; i++)
            {
                float t0 = (float)i / segments;
                float t1 = (float)(i + 1) / segments;
                Vector3 p0 = from.Lerp(to, t0);
                Vector3 p1 = from.Lerp(to, t1);

                // Offset midpoints randomly for jagged look (not endpoints)
                if (i > 0)
                {
                    float jitter = 0.3f + length * 0.05f;
                    p0 += new Vector3(
                        (GD.Randf() - 0.5f) * jitter,
                        (GD.Randf() - 0.5f) * jitter * 0.5f,
                        (GD.Randf() - 0.5f) * jitter);
                }

                float segLen = p0.DistanceTo(p1);
                var seg = new MeshInstance3D();
                var box = new BoxMesh { Size = new Vector3(0.06f, 0.06f, segLen) };
                seg.Mesh = box;

                var mat = new StandardMaterial3D();
                mat.AlbedoColor = new Color(color.R, color.G, color.B, 0.9f);
                mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                mat.EmissionEnabled = true;
                mat.Emission = color;
                mat.EmissionEnergyMultiplier = 4f;
                mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                seg.MaterialOverride = mat;

                root.AddChild(seg);

                // Use local position (root will be at midpoint)
                seg.Position = (p0 + p1) / 2f - midpoint;
                var segDir = (p1 - p0).Normalized();
                if (segDir.LengthSquared() > 0.001f)
                {
                    // Manual LookAt: make -Z face segDir (Godot convention)
                    var zAxis = -segDir;
                    var tempUp = Mathf.Abs(zAxis.Dot(Vector3.Up)) > 0.99f ? Vector3.Right : Vector3.Up;
                    var xAxis = tempUp.Cross(zAxis).Normalized();
                    var yAxis = zAxis.Cross(xAxis).Normalized();
                    seg.Basis = new Basis(xAxis, yAxis, zAxis);
                }
            }

            // Glow at endpoints
            var sparkFrom = CreateElectricSparks(color);
            sparkFrom.Position = from - midpoint;
            root.AddChild(sparkFrom);

            var sparkTo = CreateElectricSparks(color);
            sparkTo.Position = to - midpoint;
            root.AddChild(sparkTo);

            root.Position = midpoint;

            // Fade out quickly
            root.TreeEntered += () =>
            {
                if (!GodotObject.IsInstanceValid(root) || !root.IsInsideTree()) return;
                root.GetTree().CreateTimer(0.15f).Timeout += () =>
                {
                    if (GodotObject.IsInstanceValid(root) && root.IsInsideTree())
                        root.QueueFree();
                };
            };

            return root;
        }

        /// <summary>
        /// Stun indicator: orbiting yellow sparks above an entity's head.
        /// Returns a persistent node — caller must free it when stun ends.
        /// </summary>
        public static Node3D CreateStunIndicator()
        {
            var root = new Node3D();
            root.Name = "StunIndicator";
            root.Position = Vector3.Up * 2.2f;

            var particles = new GpuParticles3D();
            particles.Amount = 6;
            particles.Lifetime = 1.5;
            particles.SpeedScale = 1f;
            particles.DrawPass1 = SharedDrawPass;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 0.3f, 0);
            mat.Spread = 10f;
            mat.InitialVelocityMin = 0.1f;
            mat.InitialVelocityMax = 0.2f;
            mat.Gravity = Vector3.Zero;
            mat.ScaleMin = 0.3f;
            mat.ScaleMax = 0.7f;
            mat.OrbitVelocityMin = 1.5f;
            mat.OrbitVelocityMax = 2.0f;
            mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
            mat.EmissionSphereRadius = 0.3f;

            var colorRamp = new GradientTexture1D();
            var gradient = new Gradient();
            gradient.SetColor(0, new Color(1f, 1f, 0.3f, 0.9f));
            gradient.AddPoint(0.5f, new Color(1f, 0.9f, 0.2f, 0.8f));
            gradient.SetColor(1, new Color(1f, 0.8f, 0.1f, 0f));
            colorRamp.Gradient = gradient;
            mat.ColorRamp = colorRamp;

            particles.ProcessMaterial = mat;
            particles.Emitting = true;
            root.AddChild(particles);

            return root;
        }

        /// <summary>
        /// Melee slash arc — a quick sweeping crescent that fades.
        /// Spawned at attacker position, facing attack direction.
        /// </summary>
        public static Node3D CreateMeleeSlashArc(Color color, Vector3 direction)
        {
            var root = new Node3D();
            root.Name = "SlashArc";

            // Use a torus segment as the arc mesh
            var meshInst = new MeshInstance3D();
            var torus = new TorusMesh();
            torus.InnerRadius = 0.8f;
            torus.OuterRadius = 1.2f;
            torus.Rings = 12;
            torus.RingSegments = 4;
            meshInst.Mesh = torus;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(color.R, color.G, color.B, 0.8f);
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.EmissionEnabled = true;
            mat.Emission = color;
            mat.EmissionEnergyMultiplier = 3f;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
            meshInst.MaterialOverride = mat;

            // Rotate the torus to be vertical (like a sword arc)
            meshInst.RotationDegrees = new Vector3(90f, 0f, 0f);
            root.AddChild(meshInst);

            // Face the attack direction (use local rotation — node isn't in tree yet)
            if (direction.LengthSquared() > 0.001f)
            {
                var flatDir = new Vector3(direction.X, 0, direction.Z).Normalized();
                if (flatDir.LengthSquared() > 0.001f)
                {
                    float yRot = Mathf.Atan2(flatDir.X, flatDir.Z);
                    root.Rotation = new Vector3(0, yRot, 0);
                }
            }

            // Offset forward so arc appears in front of attacker
            root.Position += direction.Normalized() * 0.8f + Vector3.Up * 0.8f;

            // Animate: quick scale-up sweep + fade
            root.TreeEntered += () =>
            {
                if (!GodotObject.IsInstanceValid(root) || !root.IsInsideTree()) return;
                var tween = root.CreateTween();
                if (tween == null) return;
                root.Scale = new Vector3(0.3f, 0.3f, 0.3f);
                tween.SetParallel(true);
                tween.TweenProperty(root, "scale", Vector3.One, 0.12f)
                    .SetTrans(Tween.TransitionType.Quad)
                    .SetEase(Tween.EaseType.Out);
                tween.TweenProperty(root, "rotation_degrees:y",
                    root.RotationDegrees.Y + 60f, 0.15f)
                    .SetTrans(Tween.TransitionType.Quad)
                    .SetEase(Tween.EaseType.Out);
                tween.TweenProperty(mat, "albedo_color:a", 0f, 0.2f)
                    .SetDelay(0.05f);
                tween.SetParallel(false);
                tween.TweenCallback(Callable.From(() =>
                {
                    if (GodotObject.IsInstanceValid(root)) root.QueueFree();
                }));
            };

            return root;
        }

        /// <summary>
        /// Persistent aura ring at feet — orbiting particles in a ring.
        /// For pinnacle perks (Arc Reactor, Siege Plating, etc.).
        /// Caller must free when aura deactivates.
        /// </summary>
        public static GpuParticles3D CreateAuraRing(Color color, float radius = 2f)
        {
            var particles = new GpuParticles3D();
            particles.Amount = 16;
            particles.Lifetime = 2.0;
            particles.SpeedScale = 0.8f;
            particles.DrawPass1 = SharedDrawPass;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 0.3f, 0);
            mat.Spread = 15f;
            mat.InitialVelocityMin = 0.1f;
            mat.InitialVelocityMax = 0.3f;
            mat.Gravity = Vector3.Zero;
            mat.ScaleMin = 0.3f;
            mat.ScaleMax = 0.7f;
            mat.Color = color;
            mat.OrbitVelocityMin = 0.6f;
            mat.OrbitVelocityMax = 1.0f;

            // Ring-shaped emission
            mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring;
            mat.EmissionRingRadius = radius;
            mat.EmissionRingInnerRadius = radius - 0.2f;
            mat.EmissionRingHeight = 0.1f;
            mat.EmissionRingAxis = Vector3.Up;

            var colorRamp = new GradientTexture1D();
            var gradient = new Gradient();
            gradient.SetColor(0, new Color(color.R, color.G, color.B, 0f));
            gradient.AddPoint(0.2f, new Color(color.R, color.G, color.B, 0.7f));
            gradient.AddPoint(0.8f, new Color(color.R, color.G, color.B, 0.7f));
            gradient.SetColor(1, new Color(color.R, color.G, color.B, 0f));
            colorRamp.Gradient = gradient;
            mat.ColorRamp = colorRamp;

            particles.ProcessMaterial = mat;
            particles.Position = Vector3.Up * 0.1f;
            particles.Emitting = true;

            return particles;
        }

        private static void AutoFree(GpuParticles3D particles, float delay)
        {
            particles.TreeEntered += () =>
            {
                particles.GetTree().CreateTimer(delay).Timeout += () =>
                {
                    if (GodotObject.IsInstanceValid(particles) && particles.IsInsideTree())
                        particles.QueueFree();
                };
            };
        }
    }
}
