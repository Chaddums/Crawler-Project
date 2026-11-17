using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// A boss fight's boss (Data/bosses.json, <see cref="SiteBossManager"/>): a named enemy in its
    /// own colour that walks the route like any walker and, at the Spire, does not pass through
    /// as a leak. It stays and breaks the Spire, a share of its health every second, until one of
    /// them falls. Its trick (silencing or overloading towers) is the manager's.
    /// </summary>
    public partial class VineEnemy
    {
        /// <summary>Set before Initialize: this enemy is that boss.</summary>
        public SiteBossProfile SiteBoss { get; set; }
        public bool IsSiteBoss => SiteBoss != null;
        /// <summary>Ascendants and site bosses share a look: their own colour, a name over a long bar.</summary>
        public bool IsNamedBoss => IsAscendant || IsSiteBoss;
        /// <summary>The colour a named boss glows in.</summary>
        public Color NamedColor => IsSiteBoss ? SiteBoss.Color : AscendantColor;
        /// <summary>It has reached the Spire and is breaking it.</summary>
        public bool AtSpire { get; private set; }
        /// <summary>What it has taken off the Spire since it got there.</summary>
        public float SpireDamageDone { get; private set; }

        private float _spireHit;
        private Node3D _bossRig;
        private float _stunGuard;

        /// <summary>Seconds it has been held by a stun or a freeze, and stood at a barricade (the fight's log).</summary>
        public float HeldSeconds { get; private set; }
        public float BarricadeSeconds { get; private set; }
        /// <summary>Stuns and freezes it shrugged off.</summary>
        public int StunsRefused { get; private set; }
        /// <summary>A stun or freeze would take hold now.</summary>
        public bool CanBeHeld => !IsSiteBoss || _stunGuard <= 0f;

        /// <summary>
        /// A site boss can be stopped for a moment, not kept stopped: a stun or freeze of
        /// <paramref name="seconds"/> takes hold only when the last one let go stun_guard seconds ago.
        /// (A cluster of stun towers held The Overloader's charge for half of a fight.)
        /// </summary>
        private bool TakeHold(float seconds)
        {
            if (!IsSiteBoss) return true;
            if (_stunGuard > 0f) { StunsRefused++; return false; }
            _stunGuard = seconds + SiteBoss.StunGuard;
            return true;
        }

        private void TickBoss(float dt)
        {
            if (!IsSiteBoss) return;
            if (_stunGuard > 0f) _stunGuard -= dt;
            if (IsStunned || IsFrozen) HeldSeconds += dt;
            if (AtBarricade) BarricadeSeconds += dt;
            TurnBossRig();
        }

        /// <summary>What its blow does to a barricade in its way: a share of the barricade, whatever it is made of.</summary>
        private float BarricadeBlow(VineNode bar, float ordinary)
            => IsSiteBoss ? Mathf.Max(ordinary, bar.NodeMaxHealth * SiteBoss.BarricadeShare) : ordinary;

        /// <summary>Reaching the Spire: a site boss stays. Returns true when it did (it is not a leak).</summary>
        private bool StayAtSpire()
        {
            if (!IsSiteBoss) return false;
            if (!AtSpire)
            {
                AtSpire = true;
                _spireHit = 0.6f;
                GameEvents.OnAnnouncement?.Invoke($"{EnemyName} is at the Spire. Kill it before the Spire falls");
                GD.Print($"[SiteBoss] {EnemyName} reached the Spire with {CurrentHealth:F0} of {MaxHealth:F0} health "
                    + $"(held {HeldSeconds:F0} s by stuns and freezes, {BarricadeSeconds:F0} s at barricades)");
            }
            return true;
        }

        /// <summary>At the Spire: hit it every interval. True while it is there (it walks nowhere).</summary>
        private bool TickAtSpire(float dt)
        {
            if (!AtSpire) return false;
            _stuckCheckTimer = 0f;
            var spire = _grid?.Harvester;
            if (spire != null && !spire.IsDestroyed)
            {
                Face(spire.GlobalPosition, dt);
                if (_stunTimer > 0f) _stunTimer -= dt;
                else if (!IsFrozen && (_spireHit -= dt) <= 0f)
                {
                    _spireHit = SiteBoss.SpireHitInterval;
                    float dmg = spire.MaxHP * SiteBoss.SpireHitShare;
                    spire.TakeDamage(dmg);
                    SpireDamageDone += dmg;
                    RunRecord.BossAtSpire(EnemyName, dmg, spire.IsDestroyed, spire.CurrentHP / Mathf.Max(1f, spire.MaxHP));
                    PlayAttack();
                    VfxFactory.SpawnExplosion(GetTree(), spire.GlobalPosition + Vector3.Up * 1.2f, 1.6f, SiteBoss.Color);
                    if (ServiceLocator.TryGet<TDCamera>(out var cam)) cam.Shake(0.5f, 0.25f);
                }
            }
            CheckPlayerContact(dt);
            UpdateHealthBar();
            return true;
        }

        /// <summary>The boss's rig (a Data/Towers sheet) on its back, turned with it.</summary>
        private void BuildBossRig()
        {
            if (!IsSiteBoss || string.IsNullOrEmpty(SiteBoss.Rig)) return;
            var sheet = TowerSheet.Load(SiteBoss.Rig);
            if (sheet == null) return;
            _bossRig = new Node3D { Name = "BossRig", Position = new Vector3(0, SiteBoss.RigHeight, 0), Scale = Vector3.One * SiteBoss.RigScale };
            _bossRig.AddChild(TowerLook.Build(sheet, SiteBoss.Color, 0f));
            AddChild(_bossRig);
        }

        private void TurnBossRig()
        {
            if (_bossRig != null) _bossRig.Rotation = new Vector3(0, _smoothYaw, 0);
        }

        /// <summary>Tests: the rig on its back.</summary>
        internal Node3D BossRig => _bossRig;
    }
}
