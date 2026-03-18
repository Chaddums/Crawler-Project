using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// The harvester — a visible, damageable objective that replaces abstract core lives.
    /// Enemies attack it when they reach the exit. Generates passive income.
    /// </summary>
    public partial class VineHarvester : Node3D
    {
        public float MaxHP { get; private set; }
        public float CurrentHP { get; private set; }
        public bool IsDestroyed => CurrentHP <= 0;

        private MeshInstance3D _healthBar;
        private MeshInstance3D _healthBarBg;
        private float _incomeTimer;

        // Hit flash
        private float _flashTimer;
        private Node3D _modelRoot;

        public override void _Ready()
        {
            MaxHP = Constants.VINE_HARVESTER_MAX_HP;
            CurrentHP = MaxHP;

            BuildVisual();
            BuildHealthBar();

            ServiceLocator.Register(this);
            GameEvents.OnHarvesterHPChanged?.Invoke(CurrentHP, MaxHP);
        }

        public override void _Process(double delta)
        {
            if (IsDestroyed) return;

            // Passive income
            _incomeTimer += (float)delta;
            if (_incomeTimer >= Constants.VINE_HARVESTER_INCOME_INTERVAL)
            {
                _incomeTimer -= Constants.VINE_HARVESTER_INCOME_INTERVAL;
                GameManager.Instance?.AddScrap(
                    (int)(Constants.VINE_HARVESTER_INCOME * SignalTuningEditor.HarvesterIncomeMult)
                    + SignalTuningEditor.HarvesterIncomeBonus);
            }

            // Hit flash decay
            if (_flashTimer > 0)
            {
                _flashTimer -= (float)delta;
                if (_flashTimer <= 0 && _modelRoot != null)
                    SetFlash(false);
            }

            UpdateHealthBar();
        }

        public void TakeDamage(float amount)
        {
            if (IsDestroyed) return;
            CurrentHP = Mathf.Max(0, CurrentHP - amount);

            // Flash
            _flashTimer = 0.15f;
            if (_modelRoot != null) SetFlash(true);

            // Screen shake
            if (ServiceLocator.TryGet<TDCamera>(out var cam))
                cam.Shake(0.5f + (1f - CurrentHP / MaxHP) * 0.8f, 0.3f);

            GameEvents.OnHarvesterDamaged?.Invoke(CurrentHP);
            GameEvents.OnHarvesterHPChanged?.Invoke(CurrentHP, MaxHP);

            if (IsDestroyed)
                OnDestroyed();
        }

        public void Heal(float amount)
        {
            if (IsDestroyed) return;
            CurrentHP = Mathf.Min(MaxHP, CurrentHP + amount);
            GameEvents.OnHarvesterHPChanged?.Invoke(CurrentHP, MaxHP);
        }

        private void OnDestroyed()
        {
            // Death VFX
            VfxFactory.SpawnDeathBurst(GetTree(), GlobalPosition, new Color(1f, 0.4f, 0.1f), 12);

            GameManager.Instance?.SetPhase(GamePhase.Defeat);
            GameEvents.OnCoreDestroyed?.Invoke();
        }

        private void BuildVisual()
        {
            _modelRoot = new Node3D();
            AddChild(_modelRoot);

            bool isScrapyard = PlanetTheme.Current is ScrapyardPlanetTheme;
            var accentColor = isScrapyard
                ? new Color(0.85f, 0.55f, 0.15f) // Amber
                : new Color(0.0f, 0.85f, 0.95f);  // Cyan

            // Base platform — wide cylinder
            var basePlat = new MeshInstance3D();
            basePlat.Mesh = new CylinderMesh {
                TopRadius = 1.8f, BottomRadius = 2.0f, Height = 0.3f, RadialSegments = 12
            };
            basePlat.Position = new Vector3(0, 0.15f, 0);
            ApplyHarvesterMaterial(basePlat, accentColor, 0.3f);
            _modelRoot.AddChild(basePlat);

            // Main body — tall cylinder (drill rig / data tower)
            var body = new MeshInstance3D();
            body.Mesh = new CylinderMesh {
                TopRadius = 0.6f, BottomRadius = 1.0f, Height = 2.5f, RadialSegments = 8
            };
            body.Position = new Vector3(0, 1.55f, 0);
            ApplyHarvesterMaterial(body, accentColor, 0.6f);
            _modelRoot.AddChild(body);

            // Ring details
            for (int i = 0; i < 3; i++)
            {
                var ring = new MeshInstance3D();
                ring.Mesh = new TorusMesh {
                    InnerRadius = 0.65f + i * 0.05f,
                    OuterRadius = 0.85f + i * 0.05f,
                    Rings = 8, RingSegments = 12
                };
                ring.Position = new Vector3(0, 0.8f + i * 0.7f, 0);
                ApplyHarvesterMaterial(ring, accentColor, 1.0f);
                _modelRoot.AddChild(ring);
            }

            // Top antenna / beacon
            var antenna = new MeshInstance3D();
            antenna.Mesh = new CylinderMesh {
                TopRadius = 0.05f, BottomRadius = 0.15f, Height = 1.0f, RadialSegments = 6
            };
            antenna.Position = new Vector3(0, 3.3f, 0);
            ApplyHarvesterMaterial(antenna, accentColor, 1.5f);
            _modelRoot.AddChild(antenna);

            // Glow sphere at top
            var beacon = new MeshInstance3D();
            beacon.Mesh = new SphereMesh { Radius = 0.2f, Height = 0.4f };
            beacon.Position = new Vector3(0, 3.9f, 0);
            var beaconMat = new StandardMaterial3D();
            beaconMat.AlbedoColor = accentColor;
            beaconMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            beaconMat.EmissionEnabled = true;
            beaconMat.Emission = accentColor;
            beaconMat.EmissionEnergyMultiplier = 1f;
            beacon.MaterialOverride = beaconMat;
            _modelRoot.AddChild(beacon);
        }

        private static void ApplyHarvesterMaterial(MeshInstance3D mesh, Color accent, float emissionStrength)
        {
            bool isScrapyard = PlanetTheme.Current is ScrapyardPlanetTheme;
            if (isScrapyard)
            {
                var mat = new StandardMaterial3D();
                mat.AlbedoColor = new Color(0.25f, 0.15f, 0.08f);
                mat.Roughness = 0.85f;
                mat.Metallic = 0.6f;
                mat.EmissionEnabled = true;
                mat.Emission = accent;
                mat.EmissionEnergyMultiplier = emissionStrength * 0.3f;
                mesh.MaterialOverride = mat;
            }
            else
            {
                var bodyMat = new StandardMaterial3D();
                bodyMat.AlbedoColor = new Color(0.02f, 0.02f, 0.04f);
                bodyMat.Roughness = 0.7f;
                bodyMat.Metallic = 0.5f;
                bodyMat.EmissionEnabled = true;
                bodyMat.Emission = accent;
                bodyMat.EmissionEnergyMultiplier = emissionStrength * 0.5f;
                TronTheme.ApplyTronOutline(mesh, bodyMat);
            }
        }

        private void BuildHealthBar()
        {
            float barWidth = 2.0f;
            float barY = 4.5f;

            // Background
            _healthBarBg = new MeshInstance3D();
            _healthBarBg.Mesh = new BoxMesh { Size = new Vector3(barWidth, 0.12f, 0.12f) };
            _healthBarBg.Position = new Vector3(0, barY, 0);
            var bgMat = new StandardMaterial3D();
            bgMat.AlbedoColor = new Color(0.15f, 0.15f, 0.15f);
            bgMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            _healthBarBg.MaterialOverride = bgMat;
            AddChild(_healthBarBg);

            // Foreground
            _healthBar = new MeshInstance3D();
            _healthBar.Mesh = new BoxMesh { Size = new Vector3(barWidth, 0.1f, 0.1f) };
            _healthBar.Position = new Vector3(0, barY, 0);
            var barMat = new StandardMaterial3D();
            barMat.AlbedoColor = new Color(0.1f, 0.9f, 0.1f);
            barMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            _healthBar.MaterialOverride = barMat;
            AddChild(_healthBar);

            // Label
            var label = new Label3D();
            label.Text = "HARVESTER";
            label.FontSize = 48;
            label.OutlineSize = 6;
            bool isScrapyard = PlanetTheme.Current is ScrapyardPlanetTheme;
            label.Modulate = isScrapyard ? new Color(0.85f, 0.55f, 0.15f) : new Color(0.0f, 0.85f, 0.95f);
            label.Position = new Vector3(0, 5.0f, 0);
            label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            AddChild(label);
        }

        private void UpdateHealthBar()
        {
            if (_healthBar == null) return;
            float pct = Mathf.Clamp(CurrentHP / MaxHP, 0f, 1f);
            float halfBar = 1.0f;
            _healthBar.Scale = new Vector3(pct, 1, 1);
            _healthBar.Position = new Vector3((pct - 1f) * halfBar, _healthBar.Position.Y, 0);

            if (_healthBar.MaterialOverride is StandardMaterial3D mat)
                mat.AlbedoColor = pct > 0.5f
                    ? new Color(0.1f, 0.9f, 0.1f)
                    : pct > 0.25f
                        ? new Color(0.9f, 0.7f, 0.1f)
                        : new Color(0.9f, 0.1f, 0.1f);
        }

        private void SetFlash(bool flash)
        {
            if (_modelRoot == null) return;
            foreach (var child in _modelRoot.GetChildren())
            {
                if (child is MeshInstance3D mesh && mesh.MaterialOverride is StandardMaterial3D mat)
                {
                    if (flash)
                    {
                        mat.EmissionEnabled = true;
                        mat.Emission = Colors.White;
                        mat.EmissionEnergyMultiplier = 1.2f;
                    }
                    else
                    {
                        bool isScrapyard = PlanetTheme.Current is ScrapyardPlanetTheme;
                        var accent = isScrapyard
                            ? new Color(0.85f, 0.55f, 0.15f)
                            : new Color(0.0f, 0.85f, 0.95f);
                        mat.Emission = accent;
                        mat.EmissionEnergyMultiplier = isScrapyard ? 0.15f : 0.3f;
                    }
                }
            }
        }

        public override void _ExitTree()
        {
            ServiceLocator.Unregister<VineHarvester>();
        }
    }
}
