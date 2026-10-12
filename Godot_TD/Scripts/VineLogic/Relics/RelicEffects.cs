using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// The battle side of the tower and Spire relics: dresses every tower in its relic's parts and
    /// takes them off again when the relic is swapped, sizes the dome (Overcharged Dome, and the
    /// Fortified Core keystone), sets the Spire's health share (Siphon Core) and heals the Spire
    /// from deaths inside the dome. BIT's weapon relics swap on <see cref="VinePlayer"/> itself.
    /// </summary>
    public partial class RelicEffects : Node
    {
        /// <summary>Spire heals from Siphon Core this battle (tests).</summary>
        public int Siphons { get; private set; }

        public override void _Ready()
        {
            Name = "RelicEffects";
            ServiceLocator.Register(this);
            RelicRun.Changed += OnChanged;
            GameEvents.OnEnemyKilled += OnEnemyKilled;
            CallDeferred(nameof(ApplyAll));
        }

        public override void _ExitTree()
        {
            RelicRun.Changed -= OnChanged;
            GameEvents.OnEnemyKilled -= OnEnemyKilled;
            if (ServiceLocator.TryGet<RelicEffects>(out var r) && r == this) ServiceLocator.Unregister<RelicEffects>();
        }

        private void ApplyAll()
        {
            ApplySpire();
            foreach (var n in Towers()) if (RelicRun.Towers != null) n.Look?.ShowPerk(RelicRun.Towers, animate: false);
        }

        private IEnumerable<VineNode> Towers() => GetTree().GetNodesInGroup(Constants.GROUP_VINE_NODE).OfType<VineNode>().Where(IsInstanceValid);

        private void OnChanged(string kind, string old, string now)
        {
            if (!IsInsideTree()) return;
            if (kind == "towers")
                foreach (var n in Towers())
                {
                    n.Look?.HidePerk(old);
                    if (now != null) n.Look?.ShowPerk(now, animate: true);
                    if (now != null && n.Look != null && n.Look.PerksShown.Contains(now))
                        VfxFactory.SpawnEnergyBurst(GetTree(), n.GlobalPosition + Vector3.Up * 0.8f, RelicRegistry.Get(now)?.Tint ?? Colors.White, 6);
                }
            if (kind == "spire") ApplySpire();
        }

        private void ApplySpire()
        {
            if (ServiceLocator.TryGet<ConversionDome>(out var dome) && IsInstanceValid(dome))
                dome.SetRadiusMult(MetaRun.DomeRadiusMult * RelicRun.DomeRadiusMult);
            if (ServiceLocator.TryGet<VineGrid>(out var grid) && grid.Harvester != null && IsInstanceValid(grid.Harvester))
                grid.Harvester.SetRelicHpMult(RelicRun.SpireHpMult);
        }

        private void OnEnemyKilled(Node node)
        {
            if (RelicRun.Spire != RelicRun.SiphonCore || node is not VineEnemy e || e.IsAlive || !IsInsideTree()) return;
            if (!ServiceLocator.TryGet<ConversionDome>(out var dome) || !dome.IsInsideDome(e.GlobalPosition)) return;
            if (!ServiceLocator.TryGet<VineGrid>(out var grid) || grid.Harvester == null || grid.Harvester.IsDestroyed) return;
            var h = grid.Harvester;
            h.Heal(h.MaxHP * RelicRun.T(RelicRun.SiphonCore, "healShare", 0.015f));
            Siphons++;
            VfxFactory.SpawnSiphon(GetTree(), e.BodyCentre, h.GlobalPosition + Vector3.Up * h.ModelTop * 0.8f, RelicRegistry.Get(RelicRun.SiphonCore)?.Tint ?? Colors.Red);
        }
    }

    /// <summary>
    /// The perk tree's pick-one keystones that act during a battle: Chain Reaction (tower kills
    /// blow up, a beat apart so a pack goes off in a ripple), Last Stand (the Spire's first fall
    /// below its share stuns the field and doubles every tower's fire rate) and Requisition (a
    /// strike charge for every wave cleared). The rest read <see cref="MetaRun"/> where they apply.
    /// </summary>
    public partial class KeystoneRules : Node
    {
        private readonly Queue<(float at, Vector3 pos, float dmg)> _blasts = new();
        private float _clock;
        private int _requisition;
        public int ChainBlasts { get; private set; }
        public bool LastStandFired { get; private set; }
        public int RequisitionCharges { get; private set; }
        private static readonly string[] StrikeOrder = { "lance", "barrage", "emp" };

        public override void _Ready()
        {
            Name = "KeystoneRules";
            ServiceLocator.Register(this);
            GameEvents.OnEnemyKilled += OnEnemyKilled;
            GameEvents.OnHarvesterHPChanged += OnSpireHp;
            GameEvents.OnWaveCompleted += OnWaveCompleted;
        }

        public override void _ExitTree()
        {
            GameEvents.OnEnemyKilled -= OnEnemyKilled;
            GameEvents.OnHarvesterHPChanged -= OnSpireHp;
            GameEvents.OnWaveCompleted -= OnWaveCompleted;
            if (ServiceLocator.TryGet<KeystoneRules>(out var k) && k == this) ServiceLocator.Unregister<KeystoneRules>();
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _clock += dt;
            if (MetaRun.LastStandLeft > 0f) MetaRun.LastStandLeft = Mathf.Max(0f, MetaRun.LastStandLeft - dt);
            while (_blasts.Count > 0 && _blasts.Peek().at <= _clock)
            {
                var (_, pos, dmg) = _blasts.Dequeue();
                Blast(pos, dmg);
            }
        }

        // ── Chain Reaction ──

        private void OnEnemyKilled(Node node)
        {
            if (MetaRun.ChainShare <= 0f || node is not VineEnemy e || e.IsAlive || e.LastHitBy != "your towers") return;
            _blasts.Enqueue((_clock + 0.08f, e.GlobalPosition, e.MaxHealth * MetaRun.ChainShare));
        }

        private void Blast(Vector3 at, float dmg)
        {
            if (!IsInsideTree()) return;
            ChainBlasts++;
            float r = MetaRun.ChainRadius;
            VfxFactory.SpawnExplosion(GetTree(), at + Vector3.Up * 0.3f, r * 0.7f, new Color(1f, 0.35f, 0.15f));
            VineEnemy.HitSource = "your towers";
            VineEnemy.HitTower = null;
            try
            {
                foreach (var o in Roster.Enemies(GetTree()).ToList())
                {
                    if (!IsInstanceValid(o) || !o.IsAlive) continue;
                    var d = o.GlobalPosition - at; d.Y = 0;
                    if (d.Length() <= r) o.TakeDamage(dmg, DamageKind.Heavy);
                }
            }
            finally { VineEnemy.HitSource = null; }
        }

        // ── Last Stand ──

        private void OnSpireHp(float hp, float max)
        {
            if (MetaRun.LastStandAt <= 0f || MetaRun.LastStandUsed || max <= 0f || hp <= 0f || hp / max >= MetaRun.LastStandAt) return;
            MetaRun.LastStandUsed = true;
            MetaRun.LastStandLeft = MetaRun.LastStandSeconds;
            LastStandFired = true;
            if (!IsInsideTree()) return;
            foreach (var e in Roster.Enemies(GetTree()).ToList())
                if (IsInstanceValid(e) && e.IsAlive) e.ApplyStun(MetaRun.LastStandStun);
            var col = new Color(1f, 0.85f, 0.35f);
            if (ServiceLocator.TryGet<VineGrid>(out var grid) && grid.Harvester != null)
            {
                var at = grid.Harvester.GlobalPosition;
                VfxFactory.SpawnCorruptionPulse(GetTree(), at + Vector3.Up * 0.5f, col);
                VfxFactory.SpawnAbilityRing(GetTree(), at, 18f, col);
            }
            if (ServiceLocator.TryGet<TDCamera>(out var cam)) cam.Shake(1.2f, 0.4f);
            Celebration.Show("Keystone", "LAST STAND", $"Every enemy stunned, every tower firing twice as fast for {MetaRun.LastStandSeconds:0} s", col, 3f);
        }

        // ── Requisition ──

        private void OnWaveCompleted(int wave)
        {
            if (!MetaRun.Requisition || SpireStation.Current == null) return;
            string id = StrikeOrder[_requisition++ % StrikeOrder.Length];
            if (!SpireStation.Current.GrantStrike(id)) return;
            RequisitionCharges++;
            GameEvents.OnAnnouncement?.Invoke($"Requisition: +1 {SpireStation.Current.Strike(id)?.Name ?? id}");
        }
    }
}
