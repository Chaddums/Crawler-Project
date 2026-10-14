using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Raiders: from "start_wave" on, while the player holds resource nodes or has Salvage
    /// Drills standing, a few enemies of each wave leave the path to wreck them (Data/
    /// difficulty_scaling.json "raids"). Income out on the map has to be defended, not just built.
    ///
    /// A raider wears a gold marker over its head, walks the maze to its target (Ghosts go
    /// straight), shoots it if it has a gun and hits it if not. When its target is gone it picks
    /// another; with nothing left to raid it goes back to the path. AXIS chaos takes raiders
    /// over like any other enemy.
    /// </summary>
    public partial class RaidManager : Node
    {
        private int _startWave = 3;
        private int _base = 1;
        private int _wavesPerExtra = 6;
        private int _max = 6;
        private float _meleeMult = 1.5f;

        private class Raid
        {
            public VineNode Target;
            public List<Vector2I> Path;
            public int PathIndex;
            public float RepathTimer, MeleeTimer;
            public Label3D Marker;
        }

        private readonly Dictionary<VineEnemy, Raid> _raiders = new();
        private readonly RandomNumberGenerator _rng = new();
        // Raiders still to draft for each wave sent
        private readonly Dictionary<int, int> _quota = new();
        private bool _announced;

        /// <summary>Raiders out now (tests, HUD).</summary>
        public int Active => _raiders.Count;
        /// <summary>Raiders drafted this run (tests).</summary>
        public int Drafted { get; private set; }

        public override void _Ready()
        {
            _rng.Randomize();
            Load();
            ServiceLocator.Register(this);
            GameEvents.OnWaveStarted += OnWaveStarted;
            GameEvents.OnWaveCompleted += OnWaveCompleted;
        }

        public override void _ExitTree()
        {
            GameEvents.OnWaveStarted -= OnWaveStarted;
            GameEvents.OnWaveCompleted -= OnWaveCompleted;
            if (ServiceLocator.TryGet<RaidManager>(out var m) && m == this) ServiceLocator.Unregister<RaidManager>();
        }

        /// <summary>How many raiders a wave sends while there is something to raid.</summary>
        public int RaidersFor(int wave)
            => wave < _startWave ? 0 : Mathf.Min(_max, _base + (wave - _startWave) / Mathf.Max(1, _wavesPerExtra));

        private void OnWaveStarted(int wave)
        {
            int n = RaidersFor(wave);
            if (n > 0) _quota[wave] = n;
        }

        private void OnWaveCompleted(int wave)
        {
            _quota.Clear();
            _announced = false;
        }

        /// <summary>What raiders go for: Salvage Drills and the towers holding resource nodes.</summary>
        public static List<VineNode> Targets(VineGrid grid)
        {
            var list = new List<VineNode>();
            if (grid == null) return list;
            list.AddRange(SalvageDrill.All());
            foreach (var rn in grid.GetResourceNodes())
            {
                if (!grid.IsResourceNodeCaptured(rn)) continue;
                foreach (var d in new[] { Vector2I.Up, Vector2I.Down, Vector2I.Left, Vector2I.Right })
                {
                    var n = grid.GetNode(rn + d);
                    if (n != null && !n.IsDestroyed && n.NodeMaxHealth > 0f && !list.Contains(n)) list.Add(n);
                }
            }
            return list;
        }

        public override void _Process(double delta)
        {
            long __pt = FrameProfiler.Start();
            try
            {
                float dt = (float)delta;
                if (!ServiceLocator.TryGet<VineGrid>(out var grid)) return;
                bool chaos = ServiceLocator.TryGet<CorruptionManager>(out var cm) && cm.IsCorruptionActive;
                if (chaos) { ReleaseAll(); return; }
                if (_quota.Count > 0) Draft(grid);
                if (_raiders.Count == 0) return;

                var done = new List<VineEnemy>();
                foreach (var (ve, raid) in _raiders)
                {
                    if (!IsInstanceValid(ve) || !ve.IsAlive) { done.Add(ve); continue; }
                    raid.RepathTimer -= dt;
                    bool gone = raid.Target == null || !IsInstanceValid(raid.Target) || raid.Target.IsDestroyed;
                    if (gone || raid.RepathTimer <= 0f)
                    {
                        if (!Retarget(ve, raid, grid)) { done.Add(ve); continue; }
                    }
                    var at = raid.Target.GlobalPosition;
                    float dist = Flat(ve.GlobalPosition, at);
                    float reach = ve.AttackRange > 0f ? Mathf.Min(ve.AttackRange * 0.8f, 5f) : 1.9f;
                    if (dist <= reach)
                    {
                        ve.Face(at, dt);
                        ve.ChaosStep(ve.GlobalPosition, 1f, dt); // ticks slows and stuns
                        if (ve.AttackRange <= 0f) Melee(ve, raid, dt);
                        continue;
                    }
                    Vector3 step;
                    if (ve.Faction == VineEnemyFaction.Ghost || raid.Path == null || raid.PathIndex >= raid.Path.Count)
                        step = at;
                    else
                    {
                        step = grid.GridToWorld(raid.Path[raid.PathIndex]);
                        if (Flat(ve.GlobalPosition, step) < 0.25f)
                        {
                            raid.PathIndex++;
                            step = raid.PathIndex < raid.Path.Count ? grid.GridToWorld(raid.Path[raid.PathIndex]) : at;
                        }
                    }
                    if (!ve.ChaosStep(step, 1f, dt)) raid.RepathTimer = 0f;
                }
                foreach (var ve in done) Release(ve);
            }
            finally { FrameProfiler.Stop("raids", __pt); }
        }

        /// <summary>Pick raiders from enemies that have just come onto the grid.</summary>
        private void Draft(VineGrid grid)
        {
            var targets = Targets(grid);
            if (targets.Count == 0) return;
            foreach (var ve in Roster.Enemies(GetTree()))
            {
                if (_quota.Count == 0) break;
                if (!IsInstanceValid(ve) || !ve.IsAlive || ve.IsWandering || ve.IsRaider || !ve.OnGrid) continue;
                if (ve.IsBoss || ve.IsCommander || ve.IsAscendant || ve.IsFlying) continue;
                if (!_quota.TryGetValue(ve.Wave, out int left) || left <= 0) continue;
                if (left <= 1) _quota.Remove(ve.Wave); else _quota[ve.Wave] = left - 1;

                var raid = new Raid();
                _raiders[ve] = raid;
                ve.IsRaider = true;
                ve.IsWandering = true;
                raid.Marker = new Label3D
                {
                    Name = "RaidMarker",
                    Text = "RAID",
                    FontSize = 40,
                    OutlineSize = 10,
                    Modulate = new Color(1f, 0.78f, 0.25f),
                    Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                    NoDepthTest = true,
                    Position = new Vector3(0, (ve.IsBoss ? 3.8f : 2.3f), 0),
                    PixelSize = 0.006f,
                };
                ve.AddChild(raid.Marker);
                Drafted++;
                if (!Retarget(ve, raid, grid)) { Release(ve); continue; }
                if (!_announced)
                {
                    _announced = true;
                    string what = raid.Target.Data?.Type == VineNodeType.SalvageDrill ? "your Salvage Drills" : "the towers holding your resource nodes";
                    GameEvents.OnAnnouncement?.Invoke($"Raiders are going for {what}");
                }
            }
        }

        /// <summary>The nearest raid target, and a path to it. False: nothing left to raid.</summary>
        private bool Retarget(VineEnemy ve, Raid raid, VineGrid grid)
        {
            raid.RepathTimer = _rng.RandfRange(1.5f, 2.2f);
            VineNode best = null;
            float bestD = float.MaxValue;
            foreach (var t in Targets(grid))
            {
                float d = Flat(ve.GlobalPosition, t.GlobalPosition);
                if (d < bestD) { bestD = d; best = t; }
            }
            raid.Target = best;
            if (best == null) return false;
            if (ve.Faction == VineEnemyFaction.Ghost || !ServiceLocator.TryGet<VinePathfinder>(out var pf)) { raid.Path = null; return true; }
            var from = grid.WorldToGrid(ve.GlobalPosition);
            var to = NearestWalkable(grid, best.GridPosition, from);
            raid.Path = to.HasValue ? pf.FindPath(from, to.Value) : null;
            raid.PathIndex = raid.Path != null && raid.Path.Count > 1 ? 1 : 0;
            return true;
        }

        private void Melee(VineEnemy ve, Raid raid, float dt)
        {
            raid.MeleeTimer -= dt;
            if (raid.MeleeTimer > 0f) return;
            raid.MeleeTimer = 1.2f;
            float dmg = Mathf.Max(4f, ve.AttackDamage) * _meleeMult * ve.DamageMultiplier;
            if (ve.Faction == VineEnemyFaction.Brute) dmg *= 1.6f;
            raid.Target.TakeDamage(dmg, TowerLoss.Raid);
            ve.PlayAttack();
        }

        /// <summary>Back to the path to the Spire.</summary>
        private void Release(VineEnemy ve)
        {
            if (_raiders.TryGetValue(ve, out var raid) && raid.Marker != null && IsInstanceValid(raid.Marker)) raid.Marker.QueueFree();
            _raiders.Remove(ve);
            if (!IsInstanceValid(ve)) return;
            ve.IsRaider = false;
            if (!ve.IsAlive) return;
            ve.IsWandering = false;
            ve.RepathToSpire();
        }

        private void ReleaseAll()
        {
            if (_raiders.Count == 0) return;
            foreach (var ve in new List<VineEnemy>(_raiders.Keys))
            {
                if (_raiders.TryGetValue(ve, out var raid) && raid.Marker != null && IsInstanceValid(raid.Marker)) raid.Marker.QueueFree();
                if (IsInstanceValid(ve)) ve.IsRaider = false; // chaos steers it now
            }
            _raiders.Clear();
        }

        private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

        private static Vector2I? NearestWalkable(VineGrid grid, Vector2I cell, Vector2I from)
        {
            if (grid.InBounds(cell) && grid.IsWalkable(cell)) return cell;
            Vector2I? best = null;
            float bestD = float.MaxValue;
            for (int r = 1; r <= 3 && best == null; r++)
                for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        var c = cell + new Vector2I(dx, dy);
                        if (!grid.InBounds(c) || !grid.IsWalkable(c)) continue;
                        float d = (c - from).LengthSquared();
                        if (d < bestD) { bestD = d; best = c; }
                    }
            return best;
        }

        private void Load()
        {
            const string path = "res://Data/difficulty_scaling.json";
            if (!Godot.FileAccess.FileExists(path)) return;
            using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
            var parsed = Json.ParseString(file.GetAsText());
            if (parsed.VariantType != Variant.Type.Dictionary) return;
            var d = parsed.AsGodotDictionary();
            if (!d.ContainsKey("raids") || d["raids"].VariantType != Variant.Type.Dictionary) return;
            var r = d["raids"].AsGodotDictionary();
            float F(string k, float v) => r.ContainsKey(k) ? (float)r[k].AsDouble() : v;
            _startWave = (int)F("start_wave", _startWave);
            _base = (int)F("raiders", _base);
            _wavesPerExtra = (int)F("waves_per_extra", _wavesPerExtra);
            _max = (int)F("max", _max);
            _meleeMult = F("melee_mult", _meleeMult);
        }
    }
}
