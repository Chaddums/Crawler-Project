using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Enemy traits that each need a particular answer:
    /// Armoured (heavy guns: Junk Turret, Scatter Cannon), Flying (anti-air: Flak Battery,
    /// Tesla Coil, BIT, the Spire) and Shielded (Tesla Coil strips shields three times as fast).
    /// Set from the wave data's "traits" after Initialize.
    /// </summary>
    public partial class VineEnemy
    {
        public EnemyTraits Traits { get; private set; }
        public bool IsArmoured => (Traits & EnemyTraits.Armoured) != 0;
        public bool IsFlying => (Traits & EnemyTraits.Flying) != 0;
        public bool IsShielded => (Traits & EnemyTraits.Shielded) != 0;

        /// <summary>Hits of these kinds are scaled this much against armour.</summary>
        public const float ARMOUR_LIGHT = 0.3f, ARMOUR_NORMAL = 0.75f;
        /// <summary>Shield as a share of health; regeneration per second (of the shield) once unhit.</summary>
        public const float SHIELD_SHARE = 0.6f, SHIELD_REGEN = 0.15f, SHIELD_REGEN_DELAY = 2.5f;
        /// <summary>Electric hits count this many times over against a shield.</summary>
        public const float SHIELD_ELECTRIC = 3f;
        /// <summary>Flyers keep this far above the ground.</summary>
        public const float FLY_HEIGHT = 2.4f;

        public float ShieldHP { get; private set; }
        public float ShieldMax { get; private set; }
        private float _shieldRegenWait;
        private float _armourBrokenTimer;
        private MeshInstance3D _shieldBubble, _shieldBar, _shadow, _thruster;
        private StandardMaterial3D _bubbleMat;
        private float _flyBob;

        /// <summary>An EMP takes the shield down at once (it grows back as usual).</summary>
        public void StripShield()
        {
            if (ShieldHP <= 0f) return;
            ShieldHP = 0f;
            _shieldRegenWait = SHIELD_REGEN_DELAY;
            PulseShield(true);
            UpdateShieldBar();
        }

        /// <summary>Armour is off for a moment (Shredder shells).</summary>
        public bool ArmourBroken => _armourBrokenTimer > 0f;
        public void BreakArmour(float seconds) => _armourBrokenTimer = Mathf.Max(_armourBrokenTimer, seconds);

        /// <summary>Give this enemy its traits (after Initialize): shield, plates, flight.</summary>
        public void SetTraits(EnemyTraits traits)
        {
            Traits = traits;
            if (IsShielded)
            {
                ShieldMax = ShieldHP = MaxHealth * SHIELD_SHARE;
                BuildShieldVisual();
            }
            if (IsArmoured) BuildArmourVisual();
            if (IsFlying) BuildFlyingVisual();
            if (IsEmpowerer) BuildEmpowererVisual();
            if (IsFlying)
            {
                // Over everything, straight at the Spire: the maze doesn't apply
                _marchMode = false;
                _usingDirectMovement = true;
                var p = GlobalPosition;
                GlobalPosition = new Vector3(p.X, (_grid?.GetWorldHeight(p.X, p.Z) ?? 0f) + FLY_HEIGHT, p.Z);
            }
        }

        /// <summary>An Empowerer wears a spinning magenta halo, so it can be picked out of a crowd.</summary>
        private void BuildEmpowererVisual()
        {
            var mat = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.35f, 0.95f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                EmissionEnabled = true, Emission = new Color(1f, 0.3f, 0.9f), EmissionEnergyMultiplier = 2.2f };
            var halo = new MeshInstance3D { Name = "EmpowererHalo", Mesh = new TorusMesh { InnerRadius = 0.32f, OuterRadius = 0.42f, Rings = 24, RingSegments = 6 },
                MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Position = new Vector3(0, (IsBoss ? 3.6f : 2.0f), 0) };
            AddChild(halo);
            var tw = halo.CreateTween().SetLoops();
            tw.TweenProperty(halo, "rotation:y", Mathf.Tau, 1.6f).From(0f);
        }

        public static EnemyTraits ParseTraits(string[] names)
        {
            var t = EnemyTraits.None;
            if (names == null) return t;
            foreach (var n in names)
                if (System.Enum.TryParse<EnemyTraits>(n, true, out var one)) t |= one;
            return t;
        }

        /// <summary>A hit of <paramref name="kind"/>: armour scales it, a shield takes it first.</summary>
        public void TakeDamage(float amount, DamageKind kind)
        {
            if (!IsAlive) return;
            if (HitSource != null) { LastHitBy = HitSource; LastTower = HitTower; }
            float healthBefore = CurrentHealth;
            // Flat armour (chaos, commanders) as before: never below 1 or the hit itself
            float hit = Mathf.Max(Mathf.Min(1f, amount), amount - ArmorBonus);
            if (IsArmoured && !ArmourBroken)
                hit *= kind switch
                {
                    DamageKind.Light or DamageKind.Electric => ARMOUR_LIGHT,
                    DamageKind.Normal => ARMOUR_NORMAL,
                    _ => 1f,
                };
            // An Empowerer's tether soaks most of every hit
            hit *= SupportTakenMult;
            if (ShieldHP > 0f)
            {
                _shieldRegenWait = SHIELD_REGEN_DELAY;
                float mult = kind == DamageKind.Electric ? SHIELD_ELECTRIC : 1f;
                float absorbed = Mathf.Min(ShieldHP, hit * mult);
                ShieldHP -= absorbed;
                hit -= absorbed / mult;
                PulseShield(ShieldHP <= 0f);
                DamageNumbers.Enemy(this, absorbed);
                if (hit <= 0.0001f) { UpdateShieldBar(); return; }
            }
            CurrentHealth -= hit;
            string by = HitSource ?? "other";
            float lost = healthBefore - Mathf.Max(0f, CurrentHealth);
            DamageBySource[by] = DamageBySource.GetValueOrDefault(by) + lost;
            DamageNumbers.Enemy(this, lost);
            FlashMesh();
            if (!IsAlive) Die();
        }

        /// <summary>Health taken off this enemy so far, by who hit it ("your towers", "BIT", "the Spire", an Ascendant, "other").</summary>
        public readonly System.Collections.Generic.Dictionary<string, float> DamageBySource = new();

        /// <summary>Per frame: shield regeneration, armour break, flight.</summary>
        private void TickTraits(float dt)
        {
            if (_armourBrokenTimer > 0f) _armourBrokenTimer -= dt;
            if (_armourRig != null)
            {
                _armourRig.Rotation = new Vector3(0, _smoothYaw, 0);
                // Plates hang loose while Shredder shells have the armour off
                _armourRig.Scale = ArmourBroken ? new Vector3(1.15f, 0.85f, 1.15f) : Vector3.One;
            }
            if (IsShielded)
            {
                if (_shieldRegenWait > 0f) _shieldRegenWait -= dt;
                else if (ShieldHP < ShieldMax)
                {
                    bool wasDown = ShieldHP <= 0f;
                    ShieldHP = Mathf.Min(ShieldMax, ShieldHP + ShieldMax * SHIELD_REGEN * dt);
                    if (wasDown && ShieldHP > 0f) PulseShield(false);
                }
                UpdateShieldBar();
                if (_bubbleMat != null)
                {
                    float a = ShieldMax > 0 ? ShieldHP / ShieldMax : 0f;
                    _bubbleMat.AlbedoColor = new Color(0.35f, 0.8f, 1f, 0.12f + 0.16f * a);
                    if (_shieldBubble != null) _shieldBubble.Visible = ShieldHP > 0.01f;
                }
            }
        }

        /// <summary>
        /// Flight: straight at the Spire at FLY_HEIGHT over the drawn ground, bobbing a little.
        /// Slows from tar and shoves can't reach it (the towers that do them can't target it).
        /// Returns true when it handled this frame's movement.
        /// </summary>
        private bool TickFlight(float dt)
        {
            if (!IsFlying || _grid == null) return false;
            float speed = BaseSpeed * SpeedMultiplier;
            if (_stunTimer > 0) { _stunTimer -= dt; speed = 0f; }
            var exit = _grid.GridToWorld(_grid.ExitPoint);
            var to = exit - GlobalPosition;
            to.Y = 0;
            float dist = to.Length();
            if (dist < 0.6f) { ReachExit(); return true; }
            var step = to / dist * Mathf.Min(dist, speed * dt);
            _flyBob += dt;
            var p = GlobalPosition + step;
            float ground = _grid.GetWorldHeight(p.X, p.Z);
            p.Y = Mathf.Lerp(GlobalPosition.Y, ground + FLY_HEIGHT + 0.15f * Mathf.Sin(_flyBob * 3f), Mathf.Min(1f, dt * 4f));
            GlobalPosition = p;
            _smoothYaw = Mathf.LerpAngle(_smoothYaw, Mathf.Atan2(to.X, to.Z), dt * 10f);
            if (_modelRoot != null) _modelRoot.Rotation = new Vector3(0, _smoothYaw + _facingOffset, 0);
            if (_shadow != null)
            {
                _shadow.GlobalPosition = new Vector3(p.X, ground + 0.04f, p.Z);
                float h = p.Y - ground;
                _shadow.Scale = Vector3.One * Mathf.Clamp(1.2f - h * 0.12f, 0.6f, 1.2f);
            }
            UpdateRangedAttack(dt);
            UpdateHealthBar();
            return true;
        }

        // ── Looks ──

        private void BuildShieldVisual()
        {
            float r = Mathf.Max(_bodyRadius * 1.6f, 0.6f) * (IsBoss ? Constants.BOSS_SCALE : 1f);
            _bubbleMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.35f, 0.8f, 1f, 0.28f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                EmissionEnabled = true,
                Emission = new Color(0.3f, 0.75f, 1f),
                EmissionEnergyMultiplier = 0.6f,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                RimEnabled = true,
                Rim = 1f,
            };
            _shieldBubble = new MeshInstance3D
            {
                Name = "ShieldBubble",
                Mesh = new SphereMesh { Radius = r, Height = r * 1.7f, RadialSegments = 20, Rings = 10 },
                MaterialOverride = _bubbleMat,
                Position = new Vector3(0, r * 0.75f, 0),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(_shieldBubble);

            // A cyan bar over the health bar
            float barWidth = IsBoss ? 1.2f : 0.6f;
            _shieldBar = new MeshInstance3D
            {
                Name = "ShieldBar",
                Mesh = new BoxMesh { Size = new Vector3(barWidth, 0.05f, 0.05f) },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.35f, 0.85f, 1f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
                Position = new Vector3(0, (_healthBar?.Position.Y ?? 0.6f) + 0.09f, 0),
            };
            AddChild(_shieldBar);
        }

        private void UpdateShieldBar()
        {
            if (_shieldBar == null) return;
            float pct = ShieldMax > 0 ? Mathf.Clamp(ShieldHP / ShieldMax, 0f, 1f) : 0f;
            float half = IsBoss ? 0.6f : 0.3f;
            _shieldBar.Visible = pct > 0.001f;
            _shieldBar.Scale = new Vector3(Mathf.Max(pct, 0.001f), 1, 1);
            _shieldBar.Position = new Vector3((pct - 1f) * half, _shieldBar.Position.Y, 0);
        }

        private void PulseShield(bool broke)
        {
            if (_bubbleMat == null) return;
            _bubbleMat.EmissionEnergyMultiplier = broke ? 2.5f : 1.4f;
            var tw = CreateTween();
            tw.TweenProperty(_bubbleMat, "emission_energy_multiplier", 0.6f, 0.25f);
            if (broke) VfxFactory.SpawnImpact(GetTree(), GlobalPosition + Vector3.Up * 0.6f, new Color(0.4f, 0.85f, 1f), 1.2f);
        }

        /// <summary>
        /// A steel shell over the body with an orange rim and ribs, so armour reads at a glance from
        /// the play camera. It hangs off its own rig beside the model (not inside it), sized in world
        /// units from the body, so the model's breathing, hit flash and import scale never touch it;
        /// TickTraits turns the rig with the body.
        /// </summary>
        private void BuildArmourVisual()
        {
            float r = _bodyRadius;
            float bottom = 0f, h = 1.0f;
            if (_modelRoot != null)
            {
                var b = _modelRoot.Transform * AssetLibrary.GetCombinedAABB(_modelRoot);
                if (b.Size.Y > 0.05f) { bottom = b.Position.Y; h = Mathf.Clamp(b.Size.Y, 0.4f, 3.2f); }
            }
            ArmourHeight = h;
            _armourRig = new Node3D { Name = "ArmourRig", Position = new Vector3(0, bottom, 0), Rotation = new Vector3(0, _smoothYaw, 0) };
            AddChild(_armourRig);
            // Gunmetal, warm rather than blue so it never reads as a shield
            var steel = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.46f, 0.45f, 0.44f),
                Metallic = 0.5f,
                Roughness = 0.55f,
                // A little light of its own so it reads on Grid Prime's black ground
                EmissionEnabled = true,
                Emission = new Color(0.5f, 0.45f, 0.4f),
                EmissionEnergyMultiplier = 0.12f,
            };
            var stripe = new StandardMaterial3D
            {
                AlbedoColor = new Color(1f, 0.62f, 0.2f),
                EmissionEnabled = true,
                Emission = new Color(1f, 0.55f, 0.15f),
                EmissionEnergyMultiplier = 0.9f,
            };
            float shellR = r * 0.78f;
            float shellH = Mathf.Clamp(h * 0.24f, 0.16f, 0.6f);
            // On the back, its rim just under the top of the body so the whole shell shows
            float rimY = h * 0.84f;
            var shell = new MeshInstance3D
            {
                Name = "ArmourShell",
                // Six facets and two bands: angular plates, not a bubble. A hemisphere spans its
                // whole Height, so Height = Radius makes a half dome that Scale then flattens
                Mesh = new SphereMesh { Radius = shellR, Height = shellR, IsHemisphere = true, RadialSegments = 6, Rings = 2 },
                MaterialOverride = steel,
                Position = new Vector3(0, rimY, -r * 0.08f),
                Scale = new Vector3(1f, shellH / shellR, 1.12f),
            };
            _armourRig.AddChild(shell);
            _armourRig.AddChild(new MeshInstance3D
            {
                Name = "ArmourRim",
                Mesh = new TorusMesh { InnerRadius = shellR * 0.9f, OuterRadius = shellR * 1.04f, Rings = 6, RingSegments = 4 },
                MaterialOverride = stripe,
                Position = shell.Position,
                // Line the torus's six sides up with the shell's facets
                Rotation = new Vector3(0, Mathf.Pi / 6f, 0),
                Scale = new Vector3(1f, 1f, 1.12f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
            // Three ribs over the top, front to back
            float ribW = Mathf.Clamp(r * 0.1f, 0.04f, 0.1f);
            for (int i = -1; i <= 1; i++)
            {
                float x = i * shellR * 0.45f;
                float top = shellH * Mathf.Sqrt(Mathf.Max(0f, 1f - (x * x) / (shellR * shellR)));
                _armourRig.AddChild(new MeshInstance3D
                {
                    Name = "ArmourRib",
                    Mesh = new BoxMesh { Size = new Vector3(ribW, ribW * 0.8f, shellR * 1.6f) },
                    MaterialOverride = stripe,
                    Position = shell.Position + new Vector3(x, top * 0.92f, 0),
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                });
            }
            // The health bar turns steel blue for armoured enemies
            _armourBarTint = true;
        }

        /// <summary>Where shots should land: the middle of the body, not the feet.</summary>
        public Vector3 BodyCentre
        {
            get
            {
                float h = _bodyHeight;
                float hover = Faction == VineEnemyFaction.Swarm ? Constants.SWARM_HOVER_HEIGHT : 0f;
                return GlobalPosition + Vector3.Up * (hover + Mathf.Clamp(h * 0.5f, 0.3f, 1.2f));
            }
        }

        /// <summary>Tests: the body height the shell was sized from.</summary>
        internal float ArmourHeight { get; private set; }

        private Node3D _armourRig;

        /// <summary>Tests: turn the body (and its plates) to face <paramref name="yaw"/> radians from +Z.</summary>
        internal void TestFaceYaw(float yaw)
        {
            _smoothYaw = yaw;
            if (_modelRoot != null) _modelRoot.Rotation = new Vector3(0, yaw + _facingOffset, 0);
            else if (_mesh != null) _mesh.Rotation = new Vector3(0, yaw, 0);
            if (_armourRig != null) _armourRig.Rotation = new Vector3(0, yaw, 0);
        }

        /// <summary>Tests: the plates' rig (null when not armoured).</summary>
        internal Node3D ArmourRig => _armourRig;

        /// <summary>Hide the trait looks when the death animation starts.</summary>
        private void HideTraitLooks()
        {
            foreach (var n in new Node3D[] { _armourRig, _shieldBubble, _shieldBar, _shadow, _thruster })
                if (n != null) n.Visible = false;
        }

        private bool _armourBarTint;

        private void BuildFlyingVisual()
        {
            // A soft shadow on the ground under it, so its height reads from above
            _shadow = new MeshInstance3D
            {
                Name = "FlyShadow",
                Mesh = new CylinderMesh { TopRadius = 0.45f, BottomRadius = 0.45f, Height = 0.01f, RadialSegments = 16 },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0f, 0f, 0f, 0.4f),
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                },
                TopLevel = true,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(_shadow);
            // Thruster glow under the body
            var glow = _thruster = new MeshInstance3D
            {
                Name = "Thruster",
                Mesh = new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.02f, Height = 0.45f, RadialSegments = 8 },
                MaterialOverride = VfxCache.Glow(new Color(1f, 0.6f, 0.3f), new Color(1f, 0.45f, 0.15f), 2f, alpha: false),
                // Just under the body (Swarm drones hover above their origin)
                Position = new Vector3(0, (Faction == VineEnemyFaction.Swarm ? Constants.SWARM_HOVER_HEIGHT : 0f) - 0.12f, 0),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(glow);
        }
    }
}
