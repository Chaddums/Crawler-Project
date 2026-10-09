using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// A friendly Ascendant on the battlefield: a massively overpowered AI that answers when its
    /// rival shows up. It ignores the player, the towers and the Spire, walks to the enemy
    /// Ascendant (a boss <see cref="VineEnemy"/> walking the maze) and fights it until one falls.
    /// The clash cracks the terrain around it and catches nodes in the crossfire.
    ///
    /// A fallen Ascendant (either side) lies on the field as a corpse for
    /// <see cref="AscendantInhabit.CORPSE_WINDOW"/> seconds, long enough for BIT to climb in.
    /// </summary>
    public partial class Ascendant : Node3D
    {
        public string AscendantId { get; private set; }
        public string AscendantName { get; private set; }
        public string Faction { get; private set; }  // "enemy" or "friendly"
        public string Motivation { get; private set; }
        public string CombatStyle { get; private set; }

        public float MaxHP { get; private set; }
        public float CurrentHP { get; private set; }
        public float Damage { get; private set; }
        public float Speed { get; private set; }
        public float AttackRange { get; private set; }
        public float AttackInterval { get; private set; }
        public float ChaosRadius { get; private set; }
        public int ChaosTerrainDamage { get; private set; }

        public bool IsAlive => CurrentHP > 0;
        public bool IsEnemy => Faction == "enemy";
        /// <summary>BIT is inside it: the corpse timer leaves it alone.</summary>
        public bool Claimed { get; set; }
        /// <summary>Walking off the field after the fight.</summary>
        public bool IsDeparting => _departing;
        public VineEnemy RivalEnemy => _rivalEnemy;
        /// <summary>Total damage dealt to its rival (tests read this).</summary>
        public float DamageDealt { get; private set; }

        // Combat state
        private VineEnemy _rivalEnemy;
        private Ascendant _rival;
        private float _attackTimer;
        private float _chaosTimer;
        private const float CHAOS_TICK_INTERVAL = 2f;
        private bool _departing;
        private Vector3 _departTarget;
        private readonly RandomNumberGenerator _rng = new();

        // Visual
        private Node3D _modelRoot;
        private MeshInstance3D _fallbackMesh;
        private CharacterAnimator _animator;
        private float _facingOffset;
        private float _yaw;
        private MeshInstance3D _healthBar;
        private Label3D _nameLabel;
        private Color _color;
        private float _height = 3.5f;

        private VineGrid _grid;

        public void Initialize(AscendantProfile profile, VineGrid grid)
        {
            AscendantId = profile.Id;
            AscendantName = profile.Name;
            Faction = profile.Faction;
            Motivation = profile.Motivation;
            CombatStyle = profile.CombatStyle;
            MaxHP = profile.HP;
            CurrentHP = MaxHP;
            Damage = profile.Damage;
            Speed = profile.Speed;
            AttackRange = profile.AttackRange;
            AttackInterval = profile.AttackInterval;
            ChaosRadius = profile.ChaosRadius;
            ChaosTerrainDamage = profile.ChaosTerrainDamage;
            _color = new Color(profile.ColorR, profile.ColorG, profile.ColorB);
            _height = profile.ModelHeight > 0 ? profile.ModelHeight : 3.5f;
            _grid = grid;
            _attackTimer = 0.5f;

            BuildVisual(profile.Model);
            GD.Print($"[Ascendant] {AscendantName} spawned ({Faction}). HP: {MaxHP}, DMG: {Damage}");
        }

        /// <summary>
        /// A body left where an Ascendant fell (the enemy one is a <see cref="VineEnemy"/>, so its
        /// corpse is made here for BIT to inhabit). Plays its death and lies there.
        /// </summary>
        public void InitializeCorpse(AscendantProfile profile, VineGrid grid, float yaw)
        {
            Initialize(profile, grid);
            _yaw = yaw;
            ApplyFacing();
            CurrentHP = 0;
            LieDown();
        }

        public void SetRival(Ascendant rival)
        {
            _rival = rival;
            GD.Print($"[Ascendant] {AscendantName} targeting rival: {rival?.AscendantName ?? "none"}");
        }

        /// <summary>Fight this enemy Ascendant: walk to it and hit it until it falls.</summary>
        public void SetRivalEnemy(VineEnemy rival)
        {
            _rivalEnemy = rival;
            GD.Print($"[Ascendant] {AscendantName} hunting {rival?.EnemyName ?? "nothing"}");
        }

        /// <summary>The fight is over: walk off the nearest edge of the field and go.</summary>
        public void Depart()
        {
            if (!IsAlive || _departing) return;
            _departing = true;
            _lingering = false;
            _rivalEnemy = null;
            _rival = null;
            _departTarget = NearestEdgeOutside();
        }

        /// <summary>
        /// Its rival is gone: stay <paramref name="seconds"/> s, swatting whatever enemies are
        /// near, then leave (it used to walk off at once, which read as doing nothing).
        /// </summary>
        public void Linger(float seconds)
        {
            if (!IsAlive || _departing) return;
            _rivalEnemy = null;
            _rival = null;
            _lingering = true;
            _lingerTimer = seconds;
        }

        /// <summary>Health and hits scaled with the enemy it answers (deep waves).</summary>
        public void ScaleTo(float mult)
        {
            if (mult <= 1f) return;
            MaxHP *= mult;
            CurrentHP = MaxHP;
            Damage *= mult;
        }

        public bool IsLingering => _lingering;
        private bool _lingering;
        private float _lingerTimer;
        private VineEnemy _swat;

        public override void _PhysicsProcess(double delta)
        {
            if (!IsAlive) return;
            float dt = (float)delta;

            if (_departing)
            {
                if (MoveToward(_departTarget, dt, 0.5f))
                    QueueFree();
                UpdateHealthBar();
                return;
            }

            Vector3? target = null;
            if (_rivalEnemy != null && IsInstanceValid(_rivalEnemy) && _rivalEnemy.IsAlive)
                target = _rivalEnemy.BodyCentre;
            else if (_rival != null && IsInstanceValid(_rival) && _rival.IsAlive)
                target = _rival.GlobalPosition;
            else if (_lingering)
            {
                _lingerTimer -= dt;
                if (_lingerTimer <= 0f) { Depart(); return; }
                if (_swat == null || !IsInstanceValid(_swat) || !_swat.IsAlive) _swat = NearestEnemy(18f);
                if (_swat != null) target = _swat.BodyCentre;
            }

            if (target == null)
            {
                _animator?.SetState(AnimState.Idle);
                UpdateHealthBar();
                return;
            }

            var flat = new Vector3(target.Value.X - GlobalPosition.X, 0, target.Value.Z - GlobalPosition.Z);
            float reach = CombatStyle == "ranged_artillery" ? AttackRange : Mathf.Max(2f, AttackRange * 0.6f);
            if (flat.Length() > reach)
            {
                MoveToward(target.Value, dt, reach);
            }
            else
            {
                Face(flat);
                SnapToGround();
                _attackTimer -= dt;
                if (_attackTimer <= 0)
                {
                    _attackTimer = AttackInterval;
                    AttackRival(target.Value);
                }

                // The clash cracks the ground around it
                _chaosTimer -= dt;
                if (_chaosTimer <= 0)
                {
                    _chaosTimer = CHAOS_TICK_INTERVAL;
                    ApplyChaos();
                }
            }

            UpdateHealthBar();
        }

        // Returns true when within `stop` of the target
        private bool MoveToward(Vector3 target, float dt, float stop)
        {
            var flat = new Vector3(target.X - GlobalPosition.X, 0, target.Z - GlobalPosition.Z);
            float dist = flat.Length();
            if (dist <= stop) return true;
            var dir = flat / dist;
            GlobalPosition += dir * Mathf.Min(Speed * dt, dist - stop);
            Face(dir);
            SnapToGround();
            _animator?.SetState(AnimState.Walk);
            return false;
        }

        private void Face(Vector3 dir)
        {
            if (dir.LengthSquared() < 0.0001f) return;
            float targetYaw = Mathf.Atan2(dir.X, dir.Z);
            _yaw = Mathf.LerpAngle(_yaw, targetYaw, 0.2f);
            ApplyFacing();
        }

        private void ApplyFacing()
        {
            if (_modelRoot != null) _modelRoot.Rotation = new Vector3(0, _yaw + _facingOffset, 0);
            else if (_fallbackMesh != null) _fallbackMesh.Rotation = new Vector3(0, _yaw, 0);
        }

        private void SnapToGround()
        {
            if (_grid == null) return;
            var p = GlobalPosition;
            GlobalPosition = new Vector3(p.X, _grid.GetWorldHeight(p.X, p.Z), p.Z);
        }

        private VineEnemy NearestEnemy(float within)
        {
            VineEnemy best = null;
            float bestD = within;
            foreach (var n in Roster.Enemies(GetTree()))
            {
                if (n is not VineEnemy e || !e.IsAlive) continue;
                float d = e.GlobalPosition.DistanceTo(GlobalPosition);
                if (d < bestD) { bestD = d; best = e; }
            }
            return best;
        }

        private void AttackRival(Vector3 at)
        {
            VineEnemy.HitSource = AscendantName;
            try { AttackRivalInner(at); }
            finally { VineEnemy.HitSource = null; }
        }

        private void AttackRivalInner(Vector3 at)
        {
            _animator?.SetState(AnimState.Attack);
            var victim = _rivalEnemy != null && IsInstanceValid(_rivalEnemy) && _rivalEnemy.IsAlive ? _rivalEnemy
                : _lingering && _swat != null && IsInstanceValid(_swat) && _swat.IsAlive ? _swat : null;
            if (victim != null)
            {
                // Smaller fry take the hit in full; it splashes around them too
                victim.TakeDamage(Damage, DamageKind.Heavy);
                DamageDealt += Damage;
                if (_lingering)
                    foreach (var n in Roster.Enemies(GetTree()))
                        if (n is VineEnemy e && e != victim && e.IsAlive && e.GlobalPosition.DistanceTo(victim.GlobalPosition) < 2.5f)
                            e.TakeDamage(Damage * 0.4f, DamageKind.Heavy);
            }
            else if (_rival != null && _rival.IsAlive)
            {
                _rival.TakeDamage(Damage);
                DamageDealt += Damage;
            }

            // Big, readable hits: a muzzle flash, a fat round and a blast where it lands
            var muzzle = GlobalPosition + Vector3.Up * (_height * 0.6f);
            if (CombatStyle == "ranged_artillery")
            {
                VfxFactory.SpawnMuzzleFlash(GetTree(), muzzle, at - muzzle, _color, 2.2f);
                VfxFactory.SpawnProjectile(GetTree(), muzzle, at, _color, 24f, 2.6f, ProjectileImpact.Sparks);
                var tree = GetTree();
                float travel = muzzle.DistanceTo(at) / 24f;
                tree.CreateTimer(travel).Timeout += () => VfxFactory.SpawnExplosion(tree, at, 1.8f, _color);
            }
            else
            {
                VfxFactory.SpawnExplosion(GetTree(), at, Mathf.Max(1.4f, AttackRange * 0.35f), _color);
                VfxFactory.SpawnAreaPulse(GetTree(), at, AttackRange * 0.5f, _color);
            }

            if (ServiceLocator.TryGet<TDCamera>(out var cam))
                cam.Shake(0.25f, 0.15f);
        }

        public void TakeDamage(float amount)
        {
            if (!IsAlive) return;
            CurrentHP = Mathf.Max(0, CurrentHP - amount);
            if (CurrentHP <= 0)
            {
                GD.Print($"[Ascendant] {AscendantName} has fallen.");
                OnDeath();
            }
        }

        private void OnDeath()
        {
            VfxFactory.SpawnBossDeathBurst(GetTree(), GlobalPosition, _color);
            if (ServiceLocator.TryGet<TDCamera>(out var cam))
                cam.Shake(1.5f, 0.5f);
            ApplyChaos(radiusMult: 2f);
            LieDown();
            GameEvents.OnAscendantDefeated?.Invoke(this);
        }

        // Plays the death, hides the bars and frees the body once nobody can climb in
        private void LieDown()
        {
            _animator?.SetState(AnimState.Death);
            if (_healthBar != null) _healthBar.Visible = false;
            if (_nameLabel != null) _nameLabel.Visible = false;
            var timer = GetTree().CreateTimer(AscendantInhabit.CORPSE_WINDOW + 2f);
            timer.Timeout += () => { if (IsInstanceValid(this) && !Claimed) QueueFree(); };
        }

        /// <summary>BIT climbed in: stand the body back up.</summary>
        public void StandUp()
        {
            Claimed = true;
            _animator?.SetState(AnimState.Idle);
            if (_modelRoot != null) _modelRoot.Rotation = new Vector3(0, _facingOffset, 0);
        }

        /// <summary>
        /// Chaos around the clash: knocks down some terrain walls and catches nodes in it.
        /// </summary>
        private void ApplyChaos(float radiusMult = 1f)
        {
            if (_grid == null) return;
            float radius = ChaosRadius * radiusMult;
            Vector2I gridPos = _grid.WorldToGrid(GlobalPosition);
            int cellRadius = Mathf.CeilToInt(radius / Constants.VINE_CELL_SIZE);
            int destroyed = 0;

            for (int dx = -cellRadius; dx <= cellRadius; dx++)
            for (int dy = -cellRadius; dy <= cellRadius; dy++)
            {
                int x = gridPos.X + dx, y = gridPos.Y + dy;
                if (!_grid.InBounds(x, y)) continue;
                if (new Vector2(dx, dy).Length() > cellRadius) continue;
                if (_rng.Randf() > 0.15f * radiusMult) continue;

                if (_grid.GetCell(x, y) == VineCellType.Wall)
                {
                    _grid.ClearCell(x, y);
                    destroyed++;
                }
                // Only an enemy Ascendant's clash catches your towers: the friendly one cracked
                // them while it stood beside them, with nothing visibly hitting them
                var node = IsEnemy ? _grid.GetNode(x, y) : null;
                if (node != null && !node.IsDestroyed)
                    node.TakeDamage(ChaosTerrainDamage * 10f);
            }

            if (destroyed > 0)
            {
                GD.Print($"[Ascendant] {AscendantName} chaos: destroyed {destroyed} walls");
                GameEvents.OnTerrainChanged?.Invoke(gridPos);
            }
        }

        private Vector3 NearestEdgeOutside()
        {
            if (_grid == null) return GlobalPosition + new Vector3(60, 0, 0);
            float cs = Constants.VINE_CELL_SIZE;
            float w = _grid.Width * cs, h = _grid.Height * cs;
            var p = GlobalPosition;
            float left = p.X, right = w - p.X, top = p.Z, bottom = h - p.Z;
            float m = Mathf.Min(Mathf.Min(left, right), Mathf.Min(top, bottom));
            const float OUT = 14f;
            if (m == left) return new Vector3(-OUT, 0, p.Z);
            if (m == right) return new Vector3(w + OUT, 0, p.Z);
            if (m == top) return new Vector3(p.X, 0, -OUT);
            return new Vector3(p.X, 0, h + OUT);
        }

        private void BuildVisual(string modelPath)
        {
            _modelRoot = string.IsNullOrEmpty(modelPath) ? null : AssetLibrary.InstantiateToHeight(modelPath, _height);
            if (_modelRoot != null)
            {
                AddChild(_modelRoot);
                AssetLibrary.GroundModel(_modelRoot);
                AssetLibrary.ApplyPlayerTexture(_modelRoot, modelPath);
                BitPalette.ApplyAccentRim(_modelRoot, _color, 1.4f);
                _facingOffset = AssetLibrary.GetFacingYawOffset(modelPath);
                CharacterAnimator.SplitMonolithicAnimation(_modelRoot);
                _animator = new CharacterAnimator();
                AddChild(_animator);
                _animator.Initialize(_modelRoot);
                _animator.SetState(AnimState.Idle);
            }
            else
            {
                // No model: a glowing capsule so it still reads
                _fallbackMesh = new MeshInstance3D
                {
                    Mesh = new CapsuleMesh { Height = _height, Radius = _height * 0.22f },
                    Position = new Vector3(0, _height * 0.5f, 0),
                    MaterialOverride = new StandardMaterial3D
                    {
                        AlbedoColor = _color * 0.3f, EmissionEnabled = true, Emission = _color,
                        EmissionEnergyMultiplier = 2f,
                    },
                };
                AddChild(_fallbackMesh);
            }

            // Sits a little higher than an enemy Ascendant's, so the two names don't overlap in a clash
            float barY = _height + (IsEnemy ? 0.5f : 1.3f);
            _nameLabel = new Label3D
            {
                Name = "AscendantName",
                Text = AscendantName.ToUpperInvariant(),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                FontSize = 40, OutlineSize = 10, PixelSize = 0.008f,
                Modulate = _color.Lightened(0.35f),
                Position = new Vector3(0, barY + 0.45f, 0),
                NoDepthTest = true,
            };
            AddChild(_nameLabel);

            _healthBar = new MeshInstance3D
            {
                Mesh = new QuadMesh { Size = new Vector2(2.6f, 0.12f) },
                Position = new Vector3(0, barY, 0),
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = IsEnemy ? new Color(0.9f, 0.2f, 0.15f) : new Color(0.2f, 0.8f, 0.4f),
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
                },
            };
            AddChild(_healthBar);

            AddChild(new OmniLight3D
            {
                LightColor = _color, LightEnergy = 2f, OmniRange = Mathf.Max(4f, ChaosRadius),
                ShadowEnabled = false, Position = new Vector3(0, _height * 0.6f, 0),
            });
        }

        private void UpdateHealthBar()
        {
            if (_healthBar?.Mesh is QuadMesh quad)
                quad.Size = new Vector2(2.6f * Mathf.Clamp(CurrentHP / MaxHP, 0f, 1f), 0.12f);
        }
    }

    /// <summary>
    /// Parsed Ascendant profile from JSON.
    /// </summary>
    public class AscendantProfile
    {
        public string Id;
        public string Name;
        public string Faction;
        public string Motivation;
        public string CombatStyle;
        public float HP;
        public float Damage;
        public float Speed;
        public float AttackRange;
        public float AttackInterval;
        public float ChaosRadius;
        public int ChaosTerrainDamage;
        public float ModelScale;
        public float ColorR, ColorG, ColorB;
        public int[] PlanetAffinity;
        /// <summary>The model it walks in, fitted to <see cref="ModelHeight"/>.</summary>
        public string Model;
        public float ModelHeight = 3.5f;
        /// <summary>Resources dropped when an enemy Ascendant is killed.</summary>
        public int Reward;
    }
}
