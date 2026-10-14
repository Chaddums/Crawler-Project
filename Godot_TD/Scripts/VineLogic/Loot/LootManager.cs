using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Loot crates in a battle (Data/loot_boxes.json). Commanders, bosses and Ascendants drop
    /// them where they die; a cleared wave can call down a supply drop out in the field. BIT
    /// takes a crate by walking over it (<see cref="LootCrate"/>) and carries it; between waves O
    /// (or the HUD's OPEN) opens what BIT carries in a <see cref="LootCeremony"/>: Scrap and
    /// Supply crates together, each Gold, Prism and Ascendant cache on its own. What a crate
    /// holds is rolled when it opens and applied when the sequence is collected. Crates BIT
    /// still carries when the run ends open by themselves; crates left in the field are lost.
    /// Relics come only from crates while the loot file is loaded.
    /// </summary>
    public partial class LootManager : Node
    {
        private readonly RandomNumberGenerator _rng = new();
        private readonly List<LootTier> _carried = new();
        public IReadOnlyList<LootTier> Carried => _carried;
        /// <summary>Crates dropped, taken and opened this run (tests and the debrief).</summary>
        public int Dropped { get; private set; }
        public int Opened { get; private set; }
        /// <summary>Everything opened this run, in order (tests).</summary>
        public readonly List<LootReward> Gained = new();
        /// <summary>Fires when what BIT carries changes.</summary>
        public event Action Changed;
        /// <summary>The ceremony on screen, if any.</summary>
        public LootCeremony Ceremony { get; private set; }
        private int _lastWaveAnnounced = -1;

        public static int InField(SceneTree tree) => tree?.GetNodesInGroup(LootCrate.GROUP).OfType<LootCrate>().Count(c => !c.Taken) ?? 0;

        public override void _Ready()
        {
            _rng.Randomize();
            ServiceLocator.Register(this);
            GameEvents.OnEnemyKilled += OnEnemyKilled;
            GameEvents.OnWaveCompleted += OnWaveCompleted;
            GameEvents.OnWaveCleared += OnWaveCleared;
        }

        public override void _ExitTree()
        {
            GameEvents.OnEnemyKilled -= OnEnemyKilled;
            GameEvents.OnWaveCompleted -= OnWaveCompleted;
            GameEvents.OnWaveCleared -= OnWaveCleared;
            // The ceremony lives on the root: don't leave it behind with the battle
            if (Ceremony != null && IsInstanceValid(Ceremony)) Ceremony.TakeRest();
            Ceremony = null;
            if (ServiceLocator.TryGet<LootManager>(out var m) && m == this) ServiceLocator.Unregister<LootManager>();
        }

        private static int Wave => GameManager.Instance?.CurrentWave ?? 0;

        // ── Drops ──

        private void OnEnemyKilled(Node node)
        {
            // Stuck enemies are despawned through the same event while still alive: no crate.
            // Commanders are made with the boss flag (for their scaling), so they're checked first.
            if (node is not VineEnemy e || e.IsAlive || !LootTable.Loaded) return;
            string source = e.IsAscendant ? "ascendant" : e.IsCommander ? "commander" : e.IsBoss ? "boss" : null;
            if (source == null) return;
            var tier = LootTable.RollDrop(source, Wave, _rng);
            if (tier != null) DropAt(tier, e.GlobalPosition, fromSky: false);
        }

        // Every wave a clear covers rolls its own supply drop (a stack of waves sent early clears together)
        private void OnWaveCleared(int wave)
        {
            if (LootTable.Loaded) SupplyDrop(wave);
        }

        private void OnWaveCompleted(int wave)
        {
            if (!LootTable.Loaded) return;
            if (_carried.Count > 0 && wave != _lastWaveAnnounced)
            {
                _lastWaveAnnounced = wave;
                GetTree().CreateTimer(1.5).Timeout += () =>
                {
                    if (_carried.Count > 0 && Ceremony == null)
                        GameEvents.OnAnnouncement?.Invoke($"{_carried.Count} crate{(_carried.Count == 1 ? "" : "s")} to open: press O");
                };
            }
        }

        /// <summary>A cleared wave can call down a supply drop out in the field (null: not this time).</summary>
        internal LootCrate SupplyDrop(int wave)
        {
            var tier = LootTable.RollDrop("supply", wave, _rng);
            if (tier == null || SupplySpot() is not Vector3 at) return null;
            var crate = DropAt(tier, at, fromSky: true);
            GameEvents.OnAnnouncement?.Invoke($"Supply drop: a {tier.Name} came down in the field. Send BIT to take it");
            return crate;
        }

        /// <summary>Put a crate in the field near <paramref name="at"/> (on open ground).</summary>
        public LootCrate DropAt(LootTier tier, Vector3 at, bool fromSky)
        {
            if (tier == null) return null;
            var land = LandingSpot(at);
            var crate = LootCrate.Spawn(GetParent() ?? this, tier, at, land, fromSky);
            Dropped++;
            GD.Print($"[Loot] {tier.Name} dropped at ({land.X:F1}, {land.Z:F1}){(fromSky ? " from the sky" : "")}");
            return crate;
        }

        /// <summary>Ground near a point that isn't under a tower, a little way off it.</summary>
        private Vector3 LandingSpot(Vector3 at)
        {
            if (!ServiceLocator.TryGet<VineGrid>(out var grid)) return at;
            var c0 = grid.WorldToGrid(at);
            for (int r = 0; r <= 3; r++)
                for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        var c = c0 + new Vector2I(dx, dy);
                        if (!grid.IsWalkable(c) || grid.GetNode(c) != null) continue;
                        var w = grid.GridToWorld(c);
                        var off = r == 0 ? new Vector3((float)_rng.RandfRange(-0.4f, 0.4f), 0, (float)_rng.RandfRange(-0.4f, 0.4f)) : Vector3.Zero;
                        w += off;
                        return new Vector3(w.X, grid.GetWorldHeight(w.X, w.Z), w.Z);
                    }
            return new Vector3(at.X, grid.GetWorldHeight(at.X, at.Z), at.Z);
        }

        /// <summary>Somewhere out in the field for a supply drop: open ground 10 to 24 from the Spire.</summary>
        private Vector3? SupplySpot()
        {
            if (!ServiceLocator.TryGet<VineGrid>(out var grid)) return null;
            var spire = grid.Harvester != null ? grid.Harvester.GlobalPosition : grid.GridToWorld(grid.ExitPoint);
            for (int i = 0; i < 40; i++)
            {
                float a = _rng.Randf() * Mathf.Tau, d = _rng.RandfRange(10f, 24f);
                var p = spire + new Vector3(Mathf.Cos(a) * d, 0, Mathf.Sin(a) * d);
                var c = grid.WorldToGrid(p);
                if (!grid.InBounds(c) || !grid.IsWalkable(c) || grid.GetNode(c) != null) continue;
                return new Vector3(p.X, grid.GetWorldHeight(p.X, p.Z), p.Z);
            }
            return null;
        }

        // ── Carrying and opening ──

        internal void Collect(LootCrate crate)
        {
            _carried.Add(crate.Tier);
            _carried.Sort((a, b) => a.Rank.CompareTo(b.Rank));
            GD.Print($"[Loot] BIT took: {crate.Tier.Name} ({_carried.Count} carried)");
            Changed?.Invoke();
        }

        /// <summary>Tests: put a crate straight into BIT's hands.</summary>
        internal void GiveForTests(LootTier tier) { _carried.Add(tier); _carried.Sort((a, b) => a.Rank.CompareTo(b.Rank)); Changed?.Invoke(); }

        /// <summary>Why the crates can't be opened now (null: they can).</summary>
        public string CantOpen()
        {
            if (_carried.Count == 0) return "No crates";
            if (Ceremony != null) return "Opening";
            var phase = GameManager.Instance?.CurrentPhase ?? GamePhase.Build;
            if (phase != GamePhase.Build && phase != GamePhase.WaveComplete) return "Open between waves";
            if (GetTree().Paused) return "Paused";
            return null;
        }

        /// <summary>
        /// Open what BIT carries: Scrap and Supply crates in one sequence, then each bigger cache
        /// in its own. The battle pauses until the last one is collected.
        /// </summary>
        public bool OpenAll()
        {
            if (CantOpen() != null) return false;
            var queue = new List<LootCeremony.Opening>();
            // Everything is rolled now and paid as each is collected: one relic can't come out of two crates
            var rolled = new HashSet<string>();
            var small = _carried.Where(t => t.Sequence == "pop").ToList();
            if (small.Count > 0)
                queue.Add(new LootCeremony.Opening(small, small.SelectMany(t => LootTable.RollContents(t, Wave, _rng, rolled)).OrderBy(r => r.RarityRank).ToList(),
                    LootTable.Line("pop", _rng)));
            foreach (var t in _carried.Where(t => t.Sequence != "pop"))
                queue.Add(new LootCeremony.Opening(new List<LootTier> { t }, LootTable.RollContents(t, Wave, _rng, rolled), LootTable.Line(t.Sequence, _rng)));
            _carried.Clear();
            Changed?.Invoke();

            Ceremony = new LootCeremony { Name = "LootCeremony" };
            Ceremony.Collected += opening => { Apply(opening.Rewards); Opened += opening.Tiers.Count; };
            Ceremony.Finished += () => { Ceremony = null; Changed?.Invoke(); };
            Ceremony.EquipRequested += EquipFromCrate;
            GetTree().Root.AddChild(Ceremony);
            Ceremony.Play(queue);
            return true;
        }

        /// <summary>EQUIP on a relic in the reveal: it's BIT's now and goes in its slot at once.</summary>
        internal bool EquipFromCrate(string relicId)
        {
            if (RelicRegistry.Get(relicId) == null) return false;
            if (!RelicInventory.FoundThisRun(relicId))
            {
                if (ServiceLocator.TryGet<RelicManager>(out var rm0)) rm0.GrantRelic(relicId, "loot crate", announce: false);
                else RelicInventory.Acquire(relicId);
            }
            return ServiceLocator.TryGet<RelicManager>(out var rm) ? rm.EquipNow(relicId) : RelicInventory.Equip(relicId) || RelicInventory.IsEquipped(relicId);
        }

        /// <summary>Hand over what a crate gave.</summary>
        public void Apply(IEnumerable<LootReward> rewards)
        {
            var gm = GameManager.Instance;
            foreach (var r in rewards)
            {
                Gained.Add(r);
                switch (r.Kind)
                {
                    case "resources": case "salvage": gm?.AddResources(r.Amount); Income.Add(IncomeSource.Crates, r.Amount); break;
                    case "materials":
                        if (ServiceLocator.TryGet<VineGrid>(out var grid) && grid.Harvester != null) grid.Harvester.AddMaterials(r.Amount);
                        else gm?.AddMaterials(r.Amount);
                        break;
                    case "xp":
                        if (ServiceLocator.TryGet<VinePlayer>(out var bit) && bit.Progression != null) bit.Progression.AddXp(r.Amount);
                        break;
                    case "strike": SpireStation.Current?.GrantStrike(r.StrikeId); break;
                    case "relic":
                        // The ceremony already showed it: no second card in the middle of the screen
                        if (ServiceLocator.TryGet<RelicManager>(out var relics)) relics.GrantRelic(r.RelicId, "loot crate", announce: false);
                        else RelicInventory.Acquire(r.RelicId);
                        break;
                    case "perk_point": gm?.AwardLootPoints(r.Amount); break;
                }
            }
        }

        /// <summary>The run is over: crates BIT carries open by themselves (no sequence).</summary>
        public int SettleAtRunEnd()
        {
            int n = 0;
            // An opening still on screen pays out what it showed
            if (Ceremony != null && IsInstanceValid(Ceremony))
            {
                foreach (var o in Ceremony.TakeRest()) { Apply(o.Rewards); Opened += o.Tiers.Count; n += o.Tiers.Count; }
                Ceremony = null;
            }
            if (_carried.Count == 0) { if (n > 0) Changed?.Invoke(); return n; }
            n += _carried.Count;
            foreach (var t in _carried.ToList())
                Apply(LootTable.RollContents(t, Wave, _rng));
            Opened += _carried.Count;
            GD.Print($"[Loot] {n} carried crate(s) opened at the end of the run; {InField(GetTree())} left in the field");
            _carried.Clear();
            Changed?.Invoke();
            return n;
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey k && k.Pressed && !k.Echo && k.Keycode == Key.O)
            {
                string why = CantOpen();
                if (why == null) { OpenAll(); GetViewport().SetInputAsHandled(); }
                else if (_carried.Count > 0) GameEvents.OnAnnouncement?.Invoke(why == "Open between waves" ? "Crates open between waves" : why);
            }
        }
    }
}
