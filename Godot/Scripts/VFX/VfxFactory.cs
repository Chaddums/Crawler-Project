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
            var particles = new GpuParticles3D();
            particles.Amount = 12;
            particles.OneShot = true;
            particles.Explosiveness = 0.9f;
            particles.Lifetime = 0.3;
            particles.SpeedScale = 2f;
            particles.DrawPass1 = SharedDrawPass;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 1, 0);
            mat.Spread = 180f;
            mat.InitialVelocityMin = 3f;
            mat.InitialVelocityMax = 6f;
            mat.Gravity = new Vector3(0, -8, 0);
            mat.ScaleMin = 0.5f;
            mat.ScaleMax = 1.5f;
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
            var particles = new GpuParticles3D();
            particles.Amount = 24;
            particles.OneShot = true;
            particles.Explosiveness = 0.95f;
            particles.Lifetime = 0.6;
            particles.SpeedScale = 1.5f;
            particles.DrawPass1 = SharedDrawPass;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 1, 0);
            mat.Spread = 180f;
            mat.InitialVelocityMin = 4f;
            mat.InitialVelocityMax = 8f;
            mat.Gravity = new Vector3(0, -5, 0);
            mat.ScaleMin = 0.8f;
            mat.ScaleMax = 2f;
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
            var particles = new GpuParticles3D();
            particles.Amount = 16;
            particles.OneShot = true;
            particles.Explosiveness = 0.85f;
            particles.Lifetime = 0.5;
            particles.SpeedScale = 1.5f;
            particles.DrawPass1 = SharedDrawPass;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 1, 0);
            mat.Spread = 120f;
            mat.InitialVelocityMin = 3f;
            mat.InitialVelocityMax = 5f;
            mat.Gravity = new Vector3(0, -6, 0);
            mat.ScaleMin = 0.6f;
            mat.ScaleMax = 1.2f;
            mat.Color = new Color(1f, 0.85f, 0.3f);

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
            var particles = new GpuParticles3D();
            particles.Amount = 20;
            particles.Lifetime = 4.0;
            particles.SpeedScale = 0.5f;
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
            var particles = new GpuParticles3D();
            particles.Amount = 8;
            particles.Lifetime = 0.6;
            particles.SpeedScale = 1f;
            particles.DrawPass1 = SharedDrawPass;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 1, 0);
            mat.Spread = 15f;
            mat.InitialVelocityMin = 1f;
            mat.InitialVelocityMax = 2f;
            mat.Gravity = new Vector3(0, 0.5f, 0);
            mat.ScaleMin = 0.3f;
            mat.ScaleMax = 0.8f;

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
            var particles = new GpuParticles3D();
            particles.Amount = 16;
            particles.Lifetime = 2.0;
            particles.SpeedScale = 1f;
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
            var particles = new GpuParticles3D();
            particles.Amount = 30;
            particles.OneShot = true;
            particles.Explosiveness = 0.8f;
            particles.Lifetime = 1.2;
            particles.SpeedScale = 1.2f;
            particles.DrawPass1 = SharedDrawPass;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 1, 0);
            mat.Spread = 60f;
            mat.InitialVelocityMin = 4f;
            mat.InitialVelocityMax = 8f;
            mat.Gravity = new Vector3(0, -3, 0);
            mat.ScaleMin = 0.4f;
            mat.ScaleMax = 1.2f;

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
            var particles = new GpuParticles3D();
            particles.Amount = 8;
            particles.OneShot = true;
            particles.Explosiveness = 0.7f;
            particles.Lifetime = 0.4;
            particles.SpeedScale = 2f;
            particles.DrawPass1 = SharedDrawPass;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 1, 0);
            mat.Spread = 90f;
            mat.InitialVelocityMin = 2f;
            mat.InitialVelocityMax = 4f;
            mat.Gravity = new Vector3(0, -2, 0);
            mat.ScaleMin = 0.3f;
            mat.ScaleMax = 0.6f;
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
            var particles = new GpuParticles3D();
            particles.Amount = 18;
            particles.OneShot = true;
            particles.Explosiveness = 0.95f;
            particles.Lifetime = 0.35;
            particles.SpeedScale = 2f;
            particles.DrawPass1 = SharedDrawPass;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 1, 0);
            mat.Spread = 180f;
            mat.InitialVelocityMin = 4f;
            mat.InitialVelocityMax = 8f;
            mat.Gravity = new Vector3(0, -6, 0);
            mat.ScaleMin = 0.6f;
            mat.ScaleMax = 1.8f;
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
            var particles = new GpuParticles3D();
            particles.Amount = 8;
            particles.OneShot = true;
            particles.Explosiveness = 0.6f;
            particles.Lifetime = 0.8;
            particles.SpeedScale = 1f;
            particles.DrawPass1 = SharedDrawPass;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 1, 0);
            mat.Spread = 90f;
            mat.InitialVelocityMin = 1f;
            mat.InitialVelocityMax = 3f;
            mat.Gravity = new Vector3(0, 1f, 0);
            mat.ScaleMin = 0.5f;
            mat.ScaleMax = 1.2f;
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
            if (color == default) color = new Color(0.2f, 1f, 0.4f);

            var particles = new GpuParticles3D();
            particles.Amount = 20;
            particles.OneShot = true;
            particles.Explosiveness = 0.5f;
            particles.Lifetime = 0.8;
            particles.SpeedScale = 1f;
            particles.DrawPass1 = SharedDrawPass;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 1, 0);
            mat.Spread = 30f;
            mat.InitialVelocityMin = 1.5f;
            mat.InitialVelocityMax = 3f;
            mat.Gravity = new Vector3(0, 0.5f, 0);
            mat.ScaleMin = 0.3f;
            mat.ScaleMax = 0.8f;
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
            var particles = new GpuParticles3D();
            particles.Amount = 12;
            particles.Lifetime = 0.4;
            particles.SpeedScale = 1f;
            particles.DrawPass1 = SharedDrawPass;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 0.5f, 0);
            mat.Spread = 45f;
            mat.InitialVelocityMin = 0.5f;
            mat.InitialVelocityMax = 1.5f;
            mat.Gravity = new Vector3(0, -1, 0);
            mat.ScaleMin = 0.4f;
            mat.ScaleMax = 1.0f;

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
            if (color == default) color = new Color(0.3f, 0.9f, 0.2f, 0.6f);

            var particles = new GpuParticles3D();
            particles.Amount = 24;
            particles.Lifetime = 2.0;
            particles.SpeedScale = 0.6f;
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
            if (color == default) color = new Color(0.5f, 0.85f, 1f);

            var particles = new GpuParticles3D();
            particles.Amount = 16;
            particles.OneShot = true;
            particles.Explosiveness = 0.95f;
            particles.Lifetime = 0.5;
            particles.SpeedScale = 1.5f;
            particles.DrawPass1 = SharedDrawPass;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 1, 0);
            mat.Spread = 180f;
            mat.InitialVelocityMin = 3f;
            mat.InitialVelocityMax = 6f;
            mat.Gravity = new Vector3(0, -2, 0);
            mat.ScaleMin = 0.5f;
            mat.ScaleMax = 1.5f;

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
            if (color == default) color = new Color(0.7f, 0.85f, 1f);

            var particles = new GpuParticles3D();
            particles.Amount = 14;
            particles.OneShot = true;
            particles.Explosiveness = 0.9f;
            particles.Lifetime = 0.25;
            particles.SpeedScale = 3f;
            particles.DrawPass1 = SharedDrawPass;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 1, 0);
            mat.Spread = 180f;
            mat.InitialVelocityMin = 5f;
            mat.InitialVelocityMax = 10f;
            mat.Gravity = Vector3.Zero;
            mat.ScaleMin = 0.2f;
            mat.ScaleMax = 0.6f;
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
            if (color == default) color = new Color(1f, 0.85f, 0.3f);

            var particles = new GpuParticles3D();
            particles.Amount = 8;
            particles.OneShot = true;
            particles.Explosiveness = 1.0f;
            particles.Lifetime = 0.12;
            particles.SpeedScale = 3f;
            particles.DrawPass1 = SharedDrawPass;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 0, -1);
            mat.Spread = 25f;
            mat.InitialVelocityMin = 4f;
            mat.InitialVelocityMax = 8f;
            mat.Gravity = Vector3.Zero;
            mat.ScaleMin = 0.5f;
            mat.ScaleMax = 1.5f;

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
            var particles = new GpuParticles3D();
            particles.Amount = 8;
            particles.OneShot = true;
            particles.Explosiveness = 0.5f;
            particles.Lifetime = 1.0;
            particles.SpeedScale = 0.7f;
            particles.DrawPass1 = SharedDrawPass;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, -1, 0); // DOWN — the sadness
            mat.Spread = 60f;
            mat.InitialVelocityMin = 0.5f;
            mat.InitialVelocityMax = 1.5f;
            mat.Gravity = new Vector3(0, -4, 0);
            mat.ScaleMin = 0.8f;
            mat.ScaleMax = 1.5f;

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
            var particles = new GpuParticles3D();
            particles.Amount = amount;
            particles.OneShot = true;
            particles.Explosiveness = 0.85f;
            particles.Lifetime = 1.0 + (amount / 100f);
            particles.SpeedScale = 1.5f;
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
            var particles = new GpuParticles3D();
            particles.Amount = amount;
            particles.OneShot = true;
            particles.Explosiveness = 0.3f; // Low explosiveness = staggered rain
            particles.Lifetime = 2.5;
            particles.SpeedScale = 1f;
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
            var particles = new GpuParticles3D();
            particles.Amount = 12;
            particles.Lifetime = 3.0;
            particles.SpeedScale = 0.8f;
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
            var particles = new GpuParticles3D();
            particles.Amount = amount;
            particles.OneShot = true;
            particles.Explosiveness = 0.9f;
            particles.Lifetime = 0.6;
            particles.SpeedScale = 2f;
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
