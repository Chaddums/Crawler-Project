using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Siege tuning (Data/difficulty_scaling.json "siege"). A siege enemy walks the maze until a
    /// tower is within <see cref="Range"/>, beds down and lobs shells at the thickest knot of
    /// towers it can reach: every tower within <see cref="Splash"/> of the impact takes the hit.
    /// After <see cref="Hold"/> seconds it packs up and walks on. Every route ends at the Spire, so
    /// packing every tower around it was the best layout by a wide margin (autoplay, 2026-10):
    /// siege fire is what packing costs. Its reach is past a plain turret's: long guns (Mortar,
    /// upgraded turrets), BIT, strikes or towers further out deal with it.
    /// </summary>
    public static class Siege
    {
        private static bool _loaded;
        private static float _range = 19f, _splash = 2.6f, _damage = 40f, _interval = 3f, _hold = 16f;
        private static float _flight = 1.1f, _cooldown = 6f, _bitShare = 0.5f;
        private static int _maxPerSurge = 4, _minCrowd = 4;

        public static float Range { get { Load(); return _range; } }
        public static float Splash { get { Load(); return _splash; } }
        /// <summary>A shell's hit at the centre, before the climb's damage (half at the edge).</summary>
        public static float Damage { get { Load(); return _damage; } }
        public static float Interval { get { Load(); return _interval; } }
        public static float Hold { get { Load(); return _hold; } }
        public static float Flight { get { Load(); return _flight; } }
        public static float Cooldown { get { Load(); return _cooldown; } }
        /// <summary>Share of a shell BIT takes when it stands under one.</summary>
        public static float BitShare { get { Load(); return _bitShare; } }
        /// <summary>Procedural waves grow counts: a siege surge stops at this many (the rest goes into health).</summary>
        public static int MaxPerSurge { get { Load(); return _maxPerSurge; } }
        /// <summary>
        /// A shell is only worth it on a knot: this many towers (the target counts) within the splash.
        /// A line of towers along the route is safe; a block of them is not.
        /// </summary>
        public static int MinCrowd { get { Load(); return _minCrowd; } }

        public static void Reload() { _loaded = false; Load(); }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            const string path = "res://Data/difficulty_scaling.json";
            if (!Godot.FileAccess.FileExists(path)) return;
            using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
            var parsed = Json.ParseString(file.GetAsText());
            if (parsed.VariantType != Variant.Type.Dictionary) return;
            var d = parsed.AsGodotDictionary();
            if (!d.ContainsKey("siege") || d["siege"].VariantType != Variant.Type.Dictionary) return;
            var s = d["siege"].AsGodotDictionary();
            float F(string k, float v) => s.ContainsKey(k) ? (float)s[k].AsDouble() : v;
            _range = F("range", _range);
            _splash = F("splash", _splash);
            _damage = F("damage", _damage);
            _interval = F("interval", _interval);
            _hold = F("hold", _hold);
            _flight = F("flight", _flight);
            _cooldown = F("cooldown", _cooldown);
            _bitShare = F("bit_share", _bitShare);
            _maxPerSurge = (int)F("max_per_surge", _maxPerSurge);
            _minCrowd = (int)F("min_crowd", _minCrowd);
        }

        /// <summary>
        /// Where a shell from <paramref name="from"/> does the most harm: the tower within reach
        /// with the most towers within the splash of it (ties: the nearer). Null: no knot of
        /// <see cref="MinCrowd"/> towers in reach.
        /// </summary>
        public static VineNode BestTarget(Vector3 from, IReadOnlyList<VineNode> nodes, IReadOnlyList<Vector3> pos, out int crowd)
        {
            crowd = 0;
            float r2 = Range * Range, s2 = Splash * Splash;
            VineNode best = null;
            float bestD = float.MaxValue;
            var near = new List<int>();
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (!GodotObject.IsInstanceValid(n) || n.IsDestroyed || n.Data?.Category != VineNodeCategory.Effect) continue;
                if (Flat2(pos[i], from) <= r2) near.Add(i);
            }
            foreach (int i in near)
            {
                int c = 0;
                for (int j = 0; j < nodes.Count; j++)
                {
                    var m = nodes[j];
                    if (!GodotObject.IsInstanceValid(m) || m.IsDestroyed || m.Data?.Category != VineNodeCategory.Effect) continue;
                    if (Flat2(pos[i], pos[j]) <= s2) c++;
                }
                float d = Flat2(pos[i], from);
                if (c > crowd || (c == crowd && d < bestD)) { crowd = c; bestD = d; best = nodes[i]; }
            }
            return crowd >= MinCrowd ? best : null;
        }

        private static float Flat2(Vector3 a, Vector3 b) { float dx = a.X - b.X, dz = a.Z - b.Z; return dx * dx + dz * dz; }
    }

    public partial class VineEnemy
    {
        public bool IsSiege => (Traits & EnemyTraits.Siege) != 0;
        /// <summary>Bedded down and shelling (tests, the stuck check).</summary>
        public bool IsSieging { get; private set; }
        /// <summary>Shells fired (tests).</summary>
        public int ShellsFired { get; private set; }
        private float _siegeHold, _siegeShot, _siegeCooldown, _siegeLook;
        private static int _siegeAnnouncedWave = -1;

        /// <summary>
        /// The siege enemy's turn: true while it is bedded down (it doesn't walk). It looks for a
        /// tower in reach, beds down, shells the thickest knot for <see cref="Siege.Hold"/> s, then
        /// walks on and won't bed down again for <see cref="Siege.Cooldown"/> s.
        /// </summary>
        private bool TickSiege(float dt)
        {
            if (!IsSiege || IsWandering || IsFlying || _grid == null) { IsSieging = false; return false; }
            if (_siegeCooldown > 0f) _siegeCooldown -= dt;
            var tree = GetTree();
            if (!IsSieging)
            {
                if (_siegeCooldown > 0f || (_siegeLook -= dt) > 0f) return false;
                _siegeLook = 0.4f;
                if (Siege.BestTarget(GlobalPosition, Roster.Nodes(tree), Roster.NodePositions(tree), out _) == null) return false;
                IsSieging = true;
                _siegeHold = Siege.Hold;
                _siegeShot = 0.8f;
                _animator?.SetState(AnimState.Idle);
                if (_siegeAnnouncedWave != Wave)
                {
                    _siegeAnnouncedWave = Wave;
                    GameEvents.OnAnnouncement?.Invoke("Siege: shells are falling on your tightest knot of towers. Reach it with long guns, BIT or a strike");
                }
            }
            _siegeHold -= dt;
            if (_stunTimer > 0f) { _stunTimer -= dt; return true; } // stunned or frozen: no shelling
            _siegeShot -= dt;
            if (_siegeShot <= 0f)
            {
                var target = Siege.BestTarget(GlobalPosition, Roster.Nodes(tree), Roster.NodePositions(tree), out _);
                if (target == null) { PackUp(); return false; }
                FireShell(target.GlobalPosition);
                _siegeShot = Siege.Interval;
            }
            if (_siegeHold <= 0f) { PackUp(); return false; }
            CheckPlayerContact(dt);
            UpdateHealthBar();
            return true;
        }

        private void PackUp()
        {
            IsSieging = false;
            _siegeCooldown = Siege.Cooldown;
            _lastPosition = GlobalPosition;
            _stuckCheckTimer = 0f;
            _animator?.SetState(AnimState.Walk);
        }

        private void FireShell(Vector3 at)
        {
            var tree = GetTree();
            ShellsFired++;
            Face(at, 1f);
            _animator?.SetState(AnimState.Attack);
            _attackAnimTimer = 0.5f;
            var from = BodyCentre + Vector3.Up * 0.4f;
            var hit = new Vector3(at.X, _grid.GetWorldHeight(at.X, at.Z) + 0.2f, at.Z);
            float flight = Siege.Flight;
            VfxFactory.SpawnShell(tree, from, hit, _baseColor, flight);
            float dmg = Siege.Damage * Climb.Damage(Wave) * DamageMultiplier * StatusDamageMult;
            float splash = Siege.Splash;
            var self = this;
            tree.CreateTimer(flight, processAlways: false).Timeout += () =>
            {
                if (!GodotObject.IsInstanceValid(self) || !self.IsInsideTree()) return;
                VfxFactory.SpawnExplosion(tree, hit, splash, self._baseColor.Lerp(new Color(1f, 0.6f, 0.2f), 0.5f));
                foreach (var n in Roster.Nodes(tree).ToArray())
                {
                    if (!GodotObject.IsInstanceValid(n) || n.IsDestroyed || n.Data?.Category != VineNodeCategory.Effect) continue;
                    var p = n.GlobalPosition;
                    float d = new Vector2(p.X - hit.X, p.Z - hit.Z).Length();
                    if (d > splash) continue;
                    n.TakeDamage(dmg * (1f - 0.5f * d / splash), TowerLoss.Siege);
                }
                if (ServiceLocator.TryGet<VinePlayer>(out var bit) && bit.IsAlive)
                {
                    var b = bit.GlobalPosition;
                    if (new Vector2(b.X - hit.X, b.Z - hit.Z).Length() <= splash) bit.TakeDamage(dmg * Siege.BitShare);
                }
                if (ServiceLocator.TryGet<TDCamera>(out var cam)) cam.Shake(0.25f, 0.15f);
            };
        }
    }
}
