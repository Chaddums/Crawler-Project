using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Mining Building — the core objective and resource generator.
    /// Enemies attack it when they reach the exit. Toggles between Scrap and Magic
    /// production. Visual state changes with mode. Foundation for the conversion dome.
    /// </summary>
    public partial class VineHarvester : Node3D
    {
        public float MaxHP { get; private set; }
        public float CurrentHP { get; private set; }
        public bool IsDestroyed => CurrentHP <= 0;

        // ── Mining mode toggle ──
        public MiningMode CurrentMode { get; private set; } = MiningMode.Scrap;
        public MagicType SelectedMagic { get; private set; } = MagicType.None;
        public float MagicAccumulated { get; private set; }

        private MeshInstance3D _healthBar;
        private MeshInstance3D _healthBarBg;
        private float _incomeTimer;

        // Hit flash
        private float _flashTimer;
        private Node3D _modelRoot;

        // Animated parts — spinning extractor
        private MeshInstance3D _groundGlow;
        private StandardMaterial3D _groundGlowMat;
        private MeshInstance3D _energyColumn;
        private StandardMaterial3D _energyColumnMat;
        private MeshInstance3D _coreOrb;
        private StandardMaterial3D _coreOrbMat;
        private MeshInstance3D _topCorona;
        private StandardMaterial3D _topCoronaMat;

        private readonly List<Node3D> _ringAssemblies = new();
        private readonly List<StandardMaterial3D> _ringMats = new();
        private readonly float[] _ringSpeeds = { 1.0f, -1.5f, 0.8f };
        private readonly float[] _ringBaseHeights = { 0.8f, 1.8f, 2.8f };

        private struct DebrisOrbit
        {
            public MeshInstance3D Mesh;
            public StandardMaterial3D Mat;
            public float Radius;
            public float BaseHeight;
            public float Speed;
            public float Phase;
            public float BobSpeed;
            public float BobAmp;
        }
        private readonly List<DebrisOrbit> _debrisList = new();

        // Ground-churn VFX — dirt being extracted
        private struct RisingChunk
        {
            public MeshInstance3D Mesh;
            public StandardMaterial3D Mat;
            public float OrbitRadius;
            public float OrbitSpeed;
            public float Phase;
            public float RiseSpeed;
            public float Progress; // 0 = ground, 1 = top (recycles)
        }
        private readonly List<RisingChunk> _risingChunks = new();

        private struct DustPuff
        {
            public MeshInstance3D Mesh;
            public StandardMaterial3D Mat;
            public float Angle;
            public float Speed;
            public float Progress; // 0 = center, 1 = dissipated (recycles)
            public float RiseRate;
        }
        private readonly List<DustPuff> _dustPuffs = new();

        private Node3D _churnRing; // rotating ring of ground-level debris

        private float _animTimer;

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
            float dt = (float)delta;

            // Resource generation based on mining mode
            _incomeTimer += dt;
            if (_incomeTimer >= Constants.VINE_HARVESTER_INCOME_INTERVAL)
            {
                _incomeTimer -= Constants.VINE_HARVESTER_INCOME_INTERVAL;
                if (CurrentMode == MiningMode.Scrap)
                {
                    GameManager.Instance?.AddScrap(
                        (int)(Constants.VINE_HARVESTER_INCOME * SignalTuningEditor.HarvesterIncomeMult)
                        + SignalTuningEditor.HarvesterIncomeBonus);
                }
                else if (CurrentMode == MiningMode.Magic && SelectedMagic != MagicType.None)
                {
                    // Magic accumulates — not spent like scrap, unlocks shop upgrades
                    float magicRate = Constants.VINE_HARVESTER_INCOME * SignalTuningEditor.HarvesterIncomeMult;
                    MagicAccumulated += magicRate;
                    GameEvents.OnMagicAccumulated?.Invoke(MagicAccumulated, SelectedMagic);
                }
            }

            // Hit flash decay
            if (_flashTimer > 0)
            {
                _flashTimer -= dt;
                if (_flashTimer <= 0 && _modelRoot != null)
                    SetFlash(false);
            }

            _animTimer += dt;

            // --- Ring rotation + vertical bob ---
            for (int i = 0; i < _ringAssemblies.Count; i++)
            {
                var ring = _ringAssemblies[i];
                ring.RotateY(_ringSpeeds[i] * dt);
                float bob = Mathf.Sin(_animTimer * 1.2f + i * 2.1f) * 0.1f;
                ring.Position = new Vector3(0, _ringBaseHeights[i] + bob, 0);

                // Ring emission pulse
                if (i < _ringMats.Count)
                {
                    float pulse = 0.4f + Mathf.Sin(_animTimer * 1.5f + i * 0.8f) * 0.3f;
                    _ringMats[i].EmissionEnergyMultiplier = pulse;
                }
            }

            // --- Energy column pulse (~2 Hz) ---
            if (_energyColumnMat != null)
            {
                float colPulse = 0.6f + Mathf.Sin(_animTimer * 2f * Mathf.Tau) * 0.4f;
                _energyColumnMat.EmissionEnergyMultiplier = colPulse;
            }

            // --- Core orb pulse (~1.5 Hz) ---
            if (_coreOrbMat != null)
            {
                float orbPulse = 1.0f + Mathf.Sin(_animTimer * 1.5f * Mathf.Tau) * 0.6f;
                _coreOrbMat.EmissionEnergyMultiplier = orbPulse;
            }

            // --- Ground glow pulse (~0.5 Hz) ---
            if (_groundGlowMat != null)
            {
                float glowPulse = 0.3f + Mathf.Sin(_animTimer * 0.5f * Mathf.Tau) * 0.15f;
                _groundGlowMat.EmissionEnergyMultiplier = glowPulse;
            }

            // --- Top corona rapid pulse (~3 Hz) ---
            if (_topCoronaMat != null)
            {
                float coronaPulse = 1.2f + Mathf.Sin(_animTimer * 3f * Mathf.Tau) * 0.8f;
                _topCoronaMat.EmissionEnergyMultiplier = coronaPulse;
            }

            // --- Floating debris orbits ---
            for (int i = 0; i < _debrisList.Count; i++)
            {
                var d = _debrisList[i];
                float angle = d.Phase + _animTimer * d.Speed;
                float x = Mathf.Cos(angle) * d.Radius;
                float z = Mathf.Sin(angle) * d.Radius;
                float y = d.BaseHeight + Mathf.Sin(_animTimer * d.BobSpeed + d.Phase) * d.BobAmp;
                d.Mesh.Position = new Vector3(x, y, z);
                d.Mesh.RotateY(dt * 1.5f);
                d.Mesh.RotateX(dt * 0.7f);
            }

            // --- Ground churn: spinning debris ring ---
            if (_churnRing != null)
                _churnRing.RotateY(dt * 0.6f);

            // --- Rising dirt chunks: spiral up column, recycle at top ---
            for (int i = 0; i < _risingChunks.Count; i++)
            {
                var c = _risingChunks[i];
                c.Progress += c.RiseSpeed * dt;
                if (c.Progress >= 1f)
                {
                    c.Progress -= 1f; // Recycle to bottom
                }
                _risingChunks[i] = c;

                float t = c.Progress;
                float height = Mathf.Lerp(0.05f, 3.5f, t);
                // Spiral inward as they rise (pulled into column)
                float radius = Mathf.Lerp(c.OrbitRadius, 0.08f, t * t);
                float orbitAngle = c.Phase + _animTimer * c.OrbitSpeed;
                float cx = Mathf.Cos(orbitAngle) * radius;
                float cz = Mathf.Sin(orbitAngle) * radius;
                c.Mesh.Position = new Vector3(cx, height, cz);
                c.Mesh.RotateY(dt * 2f);
                c.Mesh.RotateX(dt * 1.3f);

                // Fade glow brighter as chunk rises into beam
                c.Mat.EmissionEnergyMultiplier = Mathf.Lerp(0.05f, 0.5f, t);
                // Scale down as absorbed
                float s = Mathf.Lerp(1f, 0.3f, t * t);
                c.Mesh.Scale = new Vector3(s, s, s);
            }

            // --- Dust puffs: burst outward from base, fade and recycle ---
            for (int i = 0; i < _dustPuffs.Count; i++)
            {
                var p = _dustPuffs[i];
                p.Progress += p.Speed * dt;
                if (p.Progress >= 1f)
                {
                    p.Progress -= 1f;
                    // Randomize angle on recycle for variety
                    p.Angle += 1.2f + i * 0.5f;
                }
                _dustPuffs[i] = p;

                float t = p.Progress;
                float dist = Mathf.Lerp(0.5f, 2.8f, t);
                float puffY = Mathf.Lerp(0.05f, 0.4f, t) + Mathf.Sin(t * Mathf.Pi) * 0.15f;
                float px = Mathf.Cos(p.Angle) * dist;
                float pz = Mathf.Sin(p.Angle) * dist;
                p.Mesh.Position = new Vector3(px, puffY, pz);

                // Expand then fade
                float scale = Mathf.Lerp(0.4f, 1.2f, t);
                p.Mesh.Scale = new Vector3(scale, scale * 0.6f, scale);
                // Alpha: appear, peak at 0.3, then fade out
                float alpha = Mathf.Sin(t * Mathf.Pi) * 0.3f;
                p.Mat.AlbedoColor = new Color(p.Mat.AlbedoColor.R, p.Mat.AlbedoColor.G, p.Mat.AlbedoColor.B, alpha);
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

        // ── Mining Mode Toggle ──

        /// <summary>
        /// Toggle between Scrap and Magic production modes.
        /// Only works during Build phase. Magic mode requires a selected magic type.
        /// </summary>
        public void ToggleMode()
        {
            if (IsDestroyed) return;
            var phase = GameManager.Instance?.CurrentPhase ?? GamePhase.Wave;
            if (phase != GamePhase.Build) return;

            if (CurrentMode == MiningMode.Scrap && SelectedMagic != MagicType.None)
            {
                CurrentMode = MiningMode.Magic;
            }
            else
            {
                CurrentMode = MiningMode.Scrap;
            }

            UpdateModeVisuals();
            GameEvents.OnMiningModeChanged?.Invoke(CurrentMode);
            GD.Print($"[MiningBuilding] Mode → {CurrentMode}" +
                (CurrentMode == MiningMode.Magic ? $" ({SelectedMagic})" : ""));
        }

        /// <summary>
        /// Select the magic type for this mining building.
        /// Called on first placement after Floor 1, or when choosing second magic (non-attacker).
        /// </summary>
        public void SelectMagicType(MagicType type)
        {
            if (type == MagicType.None) return;
            SelectedMagic = type;
            GameEvents.OnMagicTypeSelected?.Invoke(type);
            GD.Print($"[MiningBuilding] Magic type selected: {type}");
        }

        /// <summary>
        /// Get the accent color for the current magic type.
        /// </summary>
        public static Color GetMagicColor(MagicType type) => type switch
        {
            MagicType.Chaos => new Color(0.7f, 0.2f, 0.9f),    // Purple — entropy/mind
            MagicType.Power => new Color(1.0f, 0.7f, 0.1f),         // Gold — amplification
            MagicType.Environment => new Color(0.2f, 0.85f, 0.3f),  // Green — nature/terrain
            _ => BitPalette.Accent                                    // Default BIT white
        };

        private void UpdateModeVisuals()
        {
            if (CurrentMode == MiningMode.Scrap)
            {
                // Scrap mode: standard BIT white-silver
                if (_coreOrbMat != null)
                {
                    _coreOrbMat.Emission = BitPalette.AccentBright;
                    _coreOrbMat.EmissionEnergyMultiplier = 1.0f;
                }
                if (_energyColumnMat != null)
                {
                    _energyColumnMat.Emission = BitPalette.AccentBright;
                }
                if (_topCoronaMat != null)
                {
                    _topCoronaMat.Emission = BitPalette.AccentBright;
                }
            }
            else
            {
                // Magic mode: tinted by magic type
                var magicColor = GetMagicColor(SelectedMagic);
                if (_coreOrbMat != null)
                {
                    _coreOrbMat.Emission = magicColor;
                    _coreOrbMat.EmissionEnergyMultiplier = 1.5f;
                }
                if (_energyColumnMat != null)
                {
                    _energyColumnMat.Emission = magicColor;
                }
                if (_topCoronaMat != null)
                {
                    _topCoronaMat.Emission = magicColor;
                }
            }
        }

        private void BuildVisual()
        {
            _modelRoot = new Node3D();
            AddChild(_modelRoot);

            // BIT palette — consistent across all planets (we are the virus)
            var accent = BitPalette.Accent;

            // 1. Ground glow disc
            _groundGlow = new MeshInstance3D();
            _groundGlow.Mesh = new CylinderMesh
            {
                TopRadius = 2.5f, BottomRadius = 2.5f, Height = 0.02f, RadialSegments = 24
            };
            _groundGlow.Position = new Vector3(0, 0.01f, 0);
            _groundGlowMat = BitPalette.MakeGlowMaterial(0.25f, 0.3f);
            _groundGlow.MaterialOverride = _groundGlowMat;
            _modelRoot.AddChild(_groundGlow);

            // 2. Base platform — hexagonal-ish cylinder with struts
            var basePlat = new MeshInstance3D();
            basePlat.Mesh = new CylinderMesh
            {
                TopRadius = 1.8f, BottomRadius = 2.0f, Height = 0.4f, RadialSegments = 6
            };
            basePlat.Position = new Vector3(0, 0.2f, 0);
            basePlat.MaterialOverride = BitPalette.MakeSolidMaterial(0.15f);
            _modelRoot.AddChild(basePlat);

            // 6 support struts radiating outward
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.Tau / 6f;
                var strut = new MeshInstance3D();
                strut.Mesh = new BoxMesh { Size = new Vector3(0.08f, 0.15f, 1.2f) };
                float cx = Mathf.Cos(angle) * 1.4f;
                float cz = Mathf.Sin(angle) * 1.4f;
                strut.Position = new Vector3(cx, 0.12f, cz);
                strut.Rotation = new Vector3(0, -angle, 0);
                strut.MaterialOverride = BitPalette.MakeSolidMaterial(0.1f);
                _modelRoot.AddChild(strut);
            }

            // 3. Central energy column — tall, thin, glowing beam
            _energyColumn = new MeshInstance3D();
            _energyColumn.Mesh = new CylinderMesh
            {
                TopRadius = 0.15f, BottomRadius = 0.15f, Height = 3.5f, RadialSegments = 8
            };
            _energyColumn.Position = new Vector3(0, 1.95f, 0);
            _energyColumnMat = BitPalette.MakeGlowMaterial(0.5f, 0.6f);
            _energyColumn.MaterialOverride = _energyColumnMat;
            _modelRoot.AddChild(_energyColumn);

            // 4. Core orb — at center height
            _coreOrb = new MeshInstance3D();
            _coreOrb.Mesh = new SphereMesh { Radius = 0.35f, Height = 0.7f };
            _coreOrb.Position = new Vector3(0, 1.8f, 0);
            _coreOrbMat = BitPalette.MakeGlowMaterial(1f, 1.0f);
            _coreOrb.MaterialOverride = _coreOrbMat;
            _modelRoot.AddChild(_coreOrb);

            // 5. Three spinning ring assemblies
            _ringAssemblies.Clear();
            _ringMats.Clear();
            int[] armCounts = { 2, 3, 2 };

            for (int i = 0; i < 3; i++)
            {
                var ringPivot = new Node3D();
                ringPivot.Position = new Vector3(0, _ringBaseHeights[i], 0);
                _modelRoot.AddChild(ringPivot);
                _ringAssemblies.Add(ringPivot);

                // Torus ring
                float innerR = 0.8f + i * 0.1f;
                float outerR = innerR + 0.2f;
                var ring = new MeshInstance3D();
                ring.Mesh = new TorusMesh
                {
                    InnerRadius = innerR, OuterRadius = outerR,
                    Rings = 12, RingSegments = 16
                };
                ring.MaterialOverride = BitPalette.MakeSolidMaterial(0.4f);
                ringPivot.AddChild(ring);
                if (ring.MaterialOverride is StandardMaterial3D ringMat)
                    _ringMats.Add(ringMat);

                // Arm blades extending outward
                for (int a = 0; a < armCounts[i]; a++)
                {
                    float armAngle = a * Mathf.Tau / armCounts[i];
                    var arm = new MeshInstance3D();
                    arm.Mesh = new BoxMesh { Size = new Vector3(0.06f, 0.04f, 0.7f) };
                    float armDist = outerR + 0.35f;
                    arm.Position = new Vector3(
                        Mathf.Cos(armAngle) * armDist,
                        0,
                        Mathf.Sin(armAngle) * armDist
                    );
                    arm.Rotation = new Vector3(0, -armAngle, 0);
                    arm.MaterialOverride = BitPalette.MakeSolidMaterial(0.25f);
                    ringPivot.AddChild(arm);
                }
            }

            // 6. Top corona — energy discharge point
            _topCorona = new MeshInstance3D();
            _topCorona.Mesh = new SphereMesh { Radius = 0.2f, Height = 0.4f };
            _topCorona.Position = new Vector3(0, 3.7f, 0);
            _topCoronaMat = BitPalette.MakeGlowMaterial(1f, 1.2f);
            _topCorona.MaterialOverride = _topCoronaMat;
            _modelRoot.AddChild(_topCorona);

            // 7. Ground churn — crater ring, rising dirt, dust puffs
            BuildGroundChurn();

            // 8. Floating debris — small fragments orbiting
            _debrisList.Clear();
            var rng = new RandomNumberGenerator();
            rng.Seed = 42; // Deterministic for consistency
            for (int i = 0; i < 7; i++)
            {
                var debris = new MeshInstance3D();
                float size = rng.RandfRange(0.06f, 0.14f);
                debris.Mesh = new BoxMesh { Size = new Vector3(size, size * 0.6f, size * 1.3f) };
                var dMat = new StandardMaterial3D();
                dMat.AlbedoColor = new Color(
                    BitPalette.BodyDark.R + rng.RandfRange(-0.02f, 0.02f),
                    BitPalette.BodyDark.G + rng.RandfRange(-0.02f, 0.02f),
                    BitPalette.BodyDark.B + rng.RandfRange(-0.02f, 0.02f));
                dMat.Roughness = 0.6f;
                dMat.EmissionEnabled = true;
                dMat.Emission = accent;
                dMat.EmissionEnergyMultiplier = 0.15f;
                debris.MaterialOverride = dMat;
                _modelRoot.AddChild(debris);

                _debrisList.Add(new DebrisOrbit
                {
                    Mesh = debris,
                    Mat = dMat,
                    Radius = rng.RandfRange(1.5f, 2.8f),
                    BaseHeight = rng.RandfRange(0.6f, 3.0f),
                    Speed = rng.RandfRange(0.3f, 0.8f) * (i % 2 == 0 ? 1f : -1f),
                    Phase = rng.RandfRange(0, Mathf.Tau),
                    BobSpeed = rng.RandfRange(0.5f, 1.5f),
                    BobAmp = rng.RandfRange(0.1f, 0.3f)
                });
            }
        }

        private void BuildGroundChurn()
        {
            var rng = new RandomNumberGenerator();
            rng.Seed = 99;

            var dirtColor = BitPalette.DirtColor;

            // Churned crater ring — dark broken ground around drill point
            var crater = new MeshInstance3D();
            crater.Mesh = new TorusMesh
            {
                InnerRadius = 0.3f, OuterRadius = 1.6f,
                Rings = 12, RingSegments = 16
            };
            crater.Position = new Vector3(0, -0.02f, 0);
            crater.Rotation = new Vector3(Mathf.Pi * 0.5f, 0, 0);
            var craterMat = new StandardMaterial3D();
            craterMat.AlbedoColor = BitPalette.CraterColor;
            craterMat.Roughness = 1.0f;
            craterMat.Metallic = 0.0f;
            crater.MaterialOverride = craterMat;
            _modelRoot.AddChild(crater);

            // Spinning churn ring — small rocks/clods rotating at ground level
            _churnRing = new Node3D();
            _churnRing.Position = new Vector3(0, 0.08f, 0);
            _modelRoot.AddChild(_churnRing);

            for (int i = 0; i < 10; i++)
            {
                float angle = i * Mathf.Tau / 10f + rng.RandfRange(-0.2f, 0.2f);
                float radius = rng.RandfRange(0.8f, 1.5f);
                float sz = rng.RandfRange(0.05f, 0.12f);
                var clod = new MeshInstance3D();
                clod.Mesh = new BoxMesh { Size = new Vector3(sz, sz * 0.5f, sz * 0.8f) };
                clod.Position = new Vector3(Mathf.Cos(angle) * radius, rng.RandfRange(-0.02f, 0.06f), Mathf.Sin(angle) * radius);
                clod.Rotation = new Vector3(rng.RandfRange(0, 1f), rng.RandfRange(0, 1f), rng.RandfRange(0, 1f));
                var clodMat = new StandardMaterial3D();
                clodMat.AlbedoColor = new Color(
                    dirtColor.R + rng.RandfRange(-0.05f, 0.05f),
                    dirtColor.G + rng.RandfRange(-0.03f, 0.03f),
                    dirtColor.B + rng.RandfRange(-0.02f, 0.02f));
                clodMat.Roughness = 1.0f;
                clod.MaterialOverride = clodMat;
                _churnRing.AddChild(clod);
            }

            // Rising dirt chunks — spiral upward along the column, recycle at top
            _risingChunks.Clear();
            for (int i = 0; i < 8; i++)
            {
                float sz = rng.RandfRange(0.04f, 0.1f);
                var chunk = new MeshInstance3D();
                chunk.Mesh = new BoxMesh { Size = new Vector3(sz, sz * 0.7f, sz * 0.9f) };
                var cMat = new StandardMaterial3D();
                cMat.AlbedoColor = new Color(
                    dirtColor.R + rng.RandfRange(-0.04f, 0.06f),
                    dirtColor.G + rng.RandfRange(-0.03f, 0.04f),
                    dirtColor.B + rng.RandfRange(-0.02f, 0.03f));
                cMat.Roughness = 0.95f;
                // Slight accent glow as they get pulled into the beam
                cMat.EmissionEnabled = true;
                cMat.Emission = BitPalette.Accent;
                cMat.EmissionEnergyMultiplier = 0.05f;
                chunk.MaterialOverride = cMat;
                _modelRoot.AddChild(chunk);

                _risingChunks.Add(new RisingChunk
                {
                    Mesh = chunk,
                    Mat = cMat,
                    OrbitRadius = rng.RandfRange(0.2f, 0.6f),
                    OrbitSpeed = rng.RandfRange(1.5f, 3.0f) * (i % 2 == 0 ? 1f : -1f),
                    Phase = rng.RandfRange(0, Mathf.Tau),
                    RiseSpeed = rng.RandfRange(0.15f, 0.3f),
                    Progress = rng.RandfRange(0f, 1f) // Stagger start positions
                });
            }

            // Dust puffs — semi-transparent clouds that burst outward from base
            _dustPuffs.Clear();
            for (int i = 0; i < 6; i++)
            {
                float sz = rng.RandfRange(0.15f, 0.3f);
                var puff = new MeshInstance3D();
                puff.Mesh = new SphereMesh { Radius = sz, Height = sz * 2f };
                var pMat = new StandardMaterial3D();
                pMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                pMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                pMat.AlbedoColor = new Color(dirtColor.R, dirtColor.G, dirtColor.B, 0.3f);
                puff.MaterialOverride = pMat;
                _modelRoot.AddChild(puff);

                _dustPuffs.Add(new DustPuff
                {
                    Mesh = puff,
                    Mat = pMat,
                    Angle = rng.RandfRange(0, Mathf.Tau),
                    Speed = rng.RandfRange(0.3f, 0.6f),
                    Progress = rng.RandfRange(0f, 1f), // Stagger
                    RiseRate = rng.RandfRange(0.05f, 0.15f)
                });
            }
        }

        private void BuildHealthBar()
        {
            float barWidth = 2.0f;
            float barY = 3.8f;

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
            label.Text = "MINING STATION";
            label.FontSize = 48;
            label.OutlineSize = 6;
            label.Modulate = BitPalette.Accent;
            label.Position = new Vector3(0, 4.2f, 0);
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
            SetFlashRecursive(_modelRoot, flash);
        }

        private void SetFlashRecursive(Node parent, bool flash)
        {
            foreach (var child in parent.GetChildren())
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
                        mat.Emission = BitPalette.Accent;
                        mat.EmissionEnergyMultiplier = 0.3f;
                    }
                }
                if (child is Node3D)
                    SetFlashRecursive(child, flash);
            }
        }

        public override void _ExitTree()
        {
            ServiceLocator.Unregister<VineHarvester>();
        }
    }
}
