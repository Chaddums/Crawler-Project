using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Attaches to a loot box Node3D (from CharacterMeshBuilder.BuildLootBoxModel) and adds
    /// tier-specific idle animations, particle effects, and glow auras.
    ///
    /// Art plug-in points:
    ///   - Replace BuildLootBoxModel() meshes with final 3D models (same Node3D root API)
    ///   - Swap particle textures via res://VFX/Particles/{tier}_leak.tres
    ///   - Swap glow shader via res://Shaders/lootbox_{tier}_glow.gdshader
    ///
    /// Usage:
    ///   var boxModel = CharacterMeshBuilder.BuildLootBoxModel(tier);
    ///   LootBoxPresenter.Attach(boxModel, tier);
    /// </summary>
    public partial class LootBoxPresenter : Node3D
    {
        private LootBoxTier _tier;
        private GpuParticles3D _idleParticles;
        private OmniLight3D _glowLight;
        private Tween _idleTween;

        /// <summary>
        /// Attach idle presentation to an existing loot box model.
        /// Adds wobble, glow light, and particle leak effects scaled by tier.
        /// </summary>
        public static LootBoxPresenter Attach(Node3D boxModel, LootBoxTier tier)
        {
            var presenter = new LootBoxPresenter();
            presenter._tier = tier;
            presenter.Name = "LootBoxPresenter";
            boxModel.AddChild(presenter);
            presenter.Setup(boxModel);
            return presenter;
        }

        private void Setup(Node3D boxModel)
        {
            AddIdleWobble(boxModel);

            if (_tier >= LootBoxTier.Silver)
                AddGlowLight(boxModel);

            if (_tier >= LootBoxTier.Gold)
                AddParticleLeak(boxModel);

            if (_tier >= LootBoxTier.Diamond)
                AddOrbitingSparkles(boxModel);
        }

        /// <summary>
        /// Gentle idle wobble — rotation oscillation. Higher tiers wobble faster and wider.
        /// Bronze: slow gentle sway. Legendary: eager rumble.
        /// </summary>
        private void AddIdleWobble(Node3D target)
        {
            float angle = _tier switch
            {
                LootBoxTier.Bronze => 0.02f,
                LootBoxTier.Silver => 0.03f,
                LootBoxTier.Gold => 0.04f,
                LootBoxTier.Diamond => 0.05f,
                LootBoxTier.Legendary => 0.07f,
                LootBoxTier.Celestial => 0.09f,
                _ => 0.02f
            };
            float speed = _tier switch
            {
                LootBoxTier.Bronze => 2.0f,
                LootBoxTier.Silver => 1.6f,
                LootBoxTier.Gold => 1.3f,
                LootBoxTier.Diamond => 1.0f,
                LootBoxTier.Legendary => 0.6f,
                LootBoxTier.Celestial => 0.4f,
                _ => 2.0f
            };

            _idleTween = CreateTween();
            _idleTween.SetLoops();

            _idleTween.TweenProperty(target, "rotation:z", angle, speed)
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Sine);
            _idleTween.TweenProperty(target, "rotation:z", -angle, speed)
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Sine);

            // Vertical hover for Diamond+
            if (_tier >= LootBoxTier.Diamond)
            {
                var hoverTween = CreateTween();
                hoverTween.SetLoops();
                float hoverHeight = _tier == LootBoxTier.Legendary ? 0.15f : 0.08f;
                float hoverSpeed = _tier == LootBoxTier.Legendary ? 0.8f : 1.2f;
                hoverTween.TweenProperty(target, "position:y", hoverHeight, hoverSpeed)
                    .AsRelative()
                    .SetEase(Tween.EaseType.InOut)
                    .SetTrans(Tween.TransitionType.Sine);
                hoverTween.TweenProperty(target, "position:y", -hoverHeight, hoverSpeed)
                    .AsRelative()
                    .SetEase(Tween.EaseType.InOut)
                    .SetTrans(Tween.TransitionType.Sine);
            }
        }

        /// <summary>
        /// Pulsing omni light in tier color. Art plug-in: replace with spotlight or area light.
        /// </summary>
        private void AddGlowLight(Node3D parent)
        {
            Color glowColor = GetTierGlowColor(_tier);
            float energy = _tier switch
            {
                LootBoxTier.Silver => 0.5f,
                LootBoxTier.Gold => 0.8f,
                LootBoxTier.Diamond => 1.2f,
                LootBoxTier.Legendary => 2.0f,
                LootBoxTier.Celestial => 3.0f,
                _ => 0.5f
            };
            float range = _tier switch
            {
                LootBoxTier.Silver => 1.5f,
                LootBoxTier.Gold => 2.0f,
                LootBoxTier.Diamond => 2.5f,
                LootBoxTier.Legendary => 3.5f,
                LootBoxTier.Celestial => 5.0f,
                _ => 1.5f
            };

            _glowLight = new OmniLight3D();
            _glowLight.LightColor = glowColor;
            _glowLight.LightEnergy = energy;
            _glowLight.OmniRange = range;
            _glowLight.ShadowEnabled = false;
            _glowLight.Position = new Vector3(0, 0.3f, 0);
            parent.AddChild(_glowLight);

            // Pulse the light energy
            float pulseSpeed = _tier >= LootBoxTier.Diamond ? 0.6f : 1.0f;
            float minEnergy = energy * 0.4f;
            var pulseTween = CreateTween();
            pulseTween.SetLoops();
            pulseTween.TweenProperty(_glowLight, "light_energy", minEnergy, pulseSpeed)
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Sine);
            pulseTween.TweenProperty(_glowLight, "light_energy", energy, pulseSpeed)
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Sine);
        }

        /// <summary>
        /// Particle leak from box seams — wisps escaping upward.
        /// Gold: subtle sparks. Diamond: bright trails. Legendary: fire-like.
        /// Art plug-in: swap ParticleProcessMaterial with res://VFX/Particles/{tier}_leak.tres
        /// </summary>
        private void AddParticleLeak(Node3D parent)
        {
            Color color = GetTierGlowColor(_tier);
            int amount = _tier switch
            {
                LootBoxTier.Gold => 6,
                LootBoxTier.Diamond => 12,
                LootBoxTier.Legendary => 20,
                LootBoxTier.Celestial => 30,
                _ => 6
            };

            _idleParticles = new GpuParticles3D();
            _idleParticles.Amount = amount;
            _idleParticles.Lifetime = 1.5f;
            _idleParticles.Emitting = true;
            _idleParticles.Position = new Vector3(0, 0.2f, 0);

            var mat = new ParticleProcessMaterial();
            mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
            mat.EmissionBoxExtents = new Vector3(0.2f, 0.02f, 0.15f);
            mat.Direction = new Vector3(0, 1, 0);
            mat.Spread = 20f;
            mat.InitialVelocityMin = 0.3f;
            mat.InitialVelocityMax = 0.8f;
            mat.Gravity = new Vector3(0, 0.2f, 0);
            mat.ScaleMin = 0.02f;
            mat.ScaleMax = _tier >= LootBoxTier.Diamond ? 0.06f : 0.04f;
            mat.Color = color;

            _idleParticles.ProcessMaterial = mat;

            var drawMat = new StandardMaterial3D();
            drawMat.AlbedoColor = color;
            drawMat.EmissionEnabled = true;
            drawMat.Emission = color;
            drawMat.EmissionEnergyMultiplier = 2f;
            drawMat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
            drawMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            drawMat.NoDepthTest = true;

            var quadMesh = new QuadMesh();
            quadMesh.Size = new Vector2(0.05f, 0.05f);
            quadMesh.Material = drawMat;
            _idleParticles.DrawPass1 = quadMesh;

            parent.AddChild(_idleParticles);
        }

        /// <summary>
        /// Orbiting sparkles for Diamond+ — POE2-style glowing ring around the box.
        /// </summary>
        private void AddOrbitingSparkles(Node3D parent)
        {
            Color color = GetTierGlowColor(_tier);
            float radius = _tier == LootBoxTier.Legendary ? 0.6f : 0.45f;
            var sparkles = VfxFactory.CreateOrbitingSparkles(color, radius);
            sparkles.Position = new Vector3(0, 0.25f, 0);
            parent.AddChild(sparkles);
        }

        private static Color GetTierGlowColor(LootBoxTier tier)
        {
            return tier switch
            {
                LootBoxTier.Bronze => new Color(0.8f, 0.5f, 0.2f),
                LootBoxTier.Silver => new Color(0.7f, 0.75f, 0.85f),
                LootBoxTier.Gold => new Color(1f, 0.84f, 0f),
                LootBoxTier.Diamond => new Color(0.4f, 0.9f, 1f),
                LootBoxTier.Legendary => new Color(0.8f, 0.3f, 1f),
                LootBoxTier.Celestial => new Color(1f, 0.95f, 0.7f),
                _ => Colors.White
            };
        }
    }
}
