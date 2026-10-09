using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Manages Ascendant clashes.
    ///
    /// Once a run is deep enough (min_wave, min_extraction), a cleared wave can roll an
    /// Ascendant. It is announced at once ("ASCENDANT INBOUND", and a line on the wave card) and
    /// comes with the next wave as a boss: an enemy <see cref="VineEnemy"/> that walks the maze to
    /// the Spire, which towers, BIT and the Spire's guns can all hit. A few seconds later its
    /// rival, a friendly <see cref="Ascendant"/>, answers and fights it. The friendly is not
    /// summoned by the player and does not care about the player.
    ///
    /// Killed: it drops its reward, the friendly leaves without acknowledgment, and (after enough
    /// runs) the body lies there for BIT to inhabit. Gets through: it takes spire_damage_share of
    /// the Spire's health and leaves. If the friendly falls first, the enemy keeps walking; the
    /// run is not lost unless it reaches a Spire that can't take the hit.
    /// (Before 2026-10 both were untouchable Node3Ds that only fought each other, and a fallen
    /// friendly meant 120 damage a second to the Spire for 5 seconds, which usually ended the run.)
    /// </summary>
    public partial class AscendantManager : Node
    {
        private readonly List<AscendantProfile> _profiles = new();
        private readonly Dictionary<string, string> _rivalries = new();

        // Spawn thresholds
        private int _minWave = 12;
        private int _minExtraction = 200;
        private int _checkIntervalWaves = 3;
        private float _spawnChance = 0.4f;

        // Combat tuning (ascendants.json "combat")
        private float _spireShare = 0.35f;
        private float _towerShare = 0.25f;
        private float _hpPerWave = 0.06f;
        private float _walkSpeed = 1.1f;
        private float _friendlyDelay = 3f;
        private float _friendlyLead = 8f;
        private float _lingerSeconds = 20f;
        private float _enemyHpMult = 1f;

        // State
        private bool _hasSpawnedThisRun;
        private AscendantProfile _pendingEnemy, _pendingFriendly;
        private AscendantProfile _enemyProfile;
        private VineEnemy _enemy;
        private Ascendant _friendly;

        private VineGrid _grid;
        private readonly RandomNumberGenerator _rng = new();

        private const string DataPath = "res://Data/ascendants.json";

        /// <summary>Who landed the killing blow on the last enemy Ascendant (tests, debrief).</summary>
        public string KilledBy { get; private set; }

        /// <summary>Name of the Ascendant coming with the next wave, or null.</summary>
        public string PendingName => _pendingEnemy?.Name;
        /// <summary>The enemy Ascendant on the field, or null.</summary>
        public VineEnemy Enemy => _enemy != null && IsInstanceValid(_enemy) ? _enemy : null;
        /// <summary>The friendly Ascendant on the field, or null.</summary>
        public Ascendant Friendly => _friendly != null && IsInstanceValid(_friendly) ? _friendly : null;
        public IReadOnlyList<AscendantProfile> Profiles => _profiles;

        public override void _Ready()
        {
            LoadData();

            if (ServiceLocator.TryGet<VineGrid>(out var grid))
                _grid = grid;

            GameEvents.OnWaveCompleted += OnWaveCompleted;
            GameEvents.OnWaveStarted += OnWaveStarted;
            GameEvents.OnEnemyKilled += OnEnemyKilled;
            GameEvents.OnEnemyLeaked += OnEnemyLeaked;
            GameEvents.OnAscendantDefeated += OnAscendantDefeated;

            ServiceLocator.Register(this);
        }

        private void LoadData()
        {
            if (!FileAccess.FileExists(DataPath)) return;

            var file = FileAccess.Open(DataPath, FileAccess.ModeFlags.Read);
            if (file == null) return;

            var json = new Json();
            if (json.Parse(file.GetAsText()) != Error.Ok) { file.Close(); return; }
            file.Close();

            if (json.Data.Obj is not Godot.Collections.Dictionary root) return;

            if (root.ContainsKey("spawn_thresholds") &&
                root["spawn_thresholds"].Obj is Godot.Collections.Dictionary thresholds)
            {
                _minWave = GetInt(thresholds, "min_wave", 12);
                _minExtraction = GetInt(thresholds, "min_extraction", 200);
                _checkIntervalWaves = Mathf.Max(1, GetInt(thresholds, "check_interval_waves", 3));
                _spawnChance = GetFloat(thresholds, "spawn_chance_per_check", 0.4f);
            }

            if (root.ContainsKey("combat") && root["combat"].Obj is Godot.Collections.Dictionary combat)
            {
                _spireShare = GetFloat(combat, "spire_damage_share", _spireShare);
                _towerShare = GetFloat(combat, "tower_damage_share", _towerShare);
                _hpPerWave = GetFloat(combat, "hp_per_wave", _hpPerWave);
                _walkSpeed = GetFloat(combat, "walk_speed", _walkSpeed);
                _friendlyDelay = GetFloat(combat, "friendly_delay", _friendlyDelay);
                _friendlyLead = GetFloat(combat, "friendly_lead", _friendlyLead);
                _lingerSeconds = GetFloat(combat, "friendly_linger", _lingerSeconds);
            }

            if (root.ContainsKey("ascendants") &&
                root["ascendants"].Obj is Godot.Collections.Array ascendants)
            {
                foreach (var item in ascendants)
                {
                    if (item.Obj is not Godot.Collections.Dictionary d) continue;
                    var profile = new AscendantProfile
                    {
                        Id = GetStr(d, "id"),
                        Name = GetStr(d, "name"),
                        Faction = GetStr(d, "faction"),
                        Motivation = GetStr(d, "motivation"),
                        CombatStyle = GetStr(d, "combat_style"),
                        HP = GetFloat(d, "hp", 3000),
                        Damage = GetFloat(d, "damage", 80),
                        Speed = GetFloat(d, "speed", 3),
                        AttackRange = GetFloat(d, "attack_range", 5),
                        AttackInterval = GetFloat(d, "attack_interval", 1.5f),
                        ChaosRadius = GetFloat(d, "chaos_radius", 6),
                        ChaosTerrainDamage = GetInt(d, "chaos_damage_to_terrain", 3),
                        ModelScale = GetFloat(d, "model_scale", 3.5f),
                        Model = GetStr(d, "model"),
                        ModelHeight = GetFloat(d, "model_height", 3.5f),
                        Reward = GetInt(d, "reward", 0),
                    };

                    if (d.ContainsKey("color") && d["color"].Obj is Godot.Collections.Array c && c.Count >= 3)
                    {
                        profile.ColorR = (float)c[0].AsDouble();
                        profile.ColorG = (float)c[1].AsDouble();
                        profile.ColorB = (float)c[2].AsDouble();
                    }

                    if (d.ContainsKey("planet_affinity") && d["planet_affinity"].Obj is Godot.Collections.Array pa)
                        profile.PlanetAffinity = pa.Select(p => p.AsInt32()).ToArray();

                    _profiles.Add(profile);
                }
            }

            if (root.ContainsKey("rivalries") &&
                root["rivalries"].Obj is Godot.Collections.Dictionary rivalries)
            {
                foreach (var key in rivalries.Keys)
                    _rivalries[key.AsString()] = rivalries[key].AsString();
            }

            GD.Print($"[AscendantManager] Loaded {_profiles.Count} profiles, {_rivalries.Count} rivalries");
        }

        private void OnWaveCompleted(int wave)
        {
            if (_hasSpawnedThisRun || _pendingEnemy != null) return;
            if (wave < _minWave) return;

            int extracted = GameManager.Instance?.TotalExtracted ?? 0;
            if (extracted < _minExtraction) return;

            if ((wave - _minWave) % _checkIntervalWaves != 0) return;
            if (_rng.Randf() > _spawnChance) return;

            Announce(wave + 1);
        }

        /// <summary>Pick the pair and warn the player: they come with <paramref name="nextWave"/>.</summary>
        private bool Announce(int nextWave, string enemyId = null)
        {
            if (!PickPair(enemyId, out var enemy, out var friendly)) return false;
            _pendingEnemy = enemy;
            _pendingFriendly = friendly;
            GD.Print($"[AscendantManager] {enemy.Name} comes with wave {nextWave} ({friendly?.Name ?? "no rival"} answers)");
            GameEvents.OnAnnouncement?.Invoke($"ASCENDANT INBOUND: {enemy.Name} comes with wave {nextWave}");
            if (ServiceLocator.TryGet<BITCommentary>(out var bit))
                bit.Say("something large is coming with the next wave. it will walk the maze like the rest. it is not like the rest.");
            return true;
        }

        private bool PickPair(string enemyId, out AscendantProfile enemy, out AscendantProfile friendly)
        {
            enemy = null; friendly = null;
            int planet = GameManager.Instance?.CurrentPlanet ?? 1;
            if (enemyId != null)
                enemy = _profiles.FirstOrDefault(p => p.Id == enemyId && p.Faction == "enemy");
            if (enemy == null)
            {
                var candidates = _profiles
                    .Where(p => p.Faction == "enemy" && (p.PlanetAffinity == null || p.PlanetAffinity.Contains(planet)))
                    .ToList();
                if (candidates.Count == 0) candidates = _profiles.Where(p => p.Faction == "enemy").ToList();
                if (candidates.Count == 0) return false;
                enemy = candidates[_rng.RandiRange(0, candidates.Count - 1)];
            }

            if (_rivalries.TryGetValue(enemy.Id, out var rivalId))
                friendly = _profiles.FirstOrDefault(p => p.Id == rivalId);
            friendly ??= _profiles.Where(p => p.Faction == "friendly").OrderBy(_ => _rng.Randf()).FirstOrDefault();
            return true;
        }

        private void OnWaveStarted(int wave)
        {
            if (_pendingEnemy == null) return;
            SpawnClash(wave, _friendlyDelay);
        }

        private void SpawnClash(int wave, float friendlyDelay)
        {
            var enemyProfile = _pendingEnemy;
            var friendlyProfile = _pendingFriendly;
            _pendingEnemy = null;
            _pendingFriendly = null;
            if (!ServiceLocator.TryGet<VineWaveManager>(out var wm)) return;

            float hpMult = 1f + _hpPerWave * Mathf.Max(0, wave - _minWave);
            _enemyHpMult = hpMult;
            _enemy = wm.SpawnAscendant(enemyProfile, hpMult, _towerShare, _walkSpeed, _spireShare);
            if (_enemy == null) return;
            _enemyProfile = enemyProfile;
            _hasSpawnedThisRun = true;

            GD.Print($"[AscendantManager] CLASH: {enemyProfile.Name} ({_enemy.MaxHealth:F0} hp) vs {friendlyProfile?.Name ?? "nobody"}");
            GameEvents.OnAnnouncement?.Invoke($"ASCENDANT: {enemyProfile.Name} is walking your maze to the Spire");

            if (friendlyProfile == null) return;
            if (friendlyDelay <= 0f) { SpawnFriendly(friendlyProfile); return; }
            var timer = GetTree().CreateTimer(friendlyDelay);
            timer.Timeout += () => { if (IsInstanceValid(this)) SpawnFriendly(friendlyProfile); };
        }

        private void SpawnFriendly(AscendantProfile profile)
        {
            var enemy = Enemy;
            if (enemy == null || !enemy.IsAlive || _grid == null) return;

            _friendly = new Ascendant();
            AddChild(_friendly);
            _friendly.Initialize(profile, _grid);
            _friendly.ScaleTo(_enemyHpMult);
            // It drops in just ahead of the enemy, between it and the Spire, so the fight starts
            // where you can see it (it used to walk the whole field from the Spire first)
            var spire = _grid.Harvester != null ? _grid.Harvester.GlobalPosition : _grid.GridToWorld(_grid.ExitPoint);
            var toSpire = new Vector3(spire.X - enemy.GlobalPosition.X, 0, spire.Z - enemy.GlobalPosition.Z);
            float gap = Mathf.Min(_friendlyLead, toSpire.Length() * 0.5f);
            var at = enemy.GlobalPosition + (toSpire.LengthSquared() > 0.01f ? toSpire.Normalized() * gap : new Vector3(gap, 0, 0));
            _friendly.GlobalPosition = new Vector3(at.X, _grid.GetWorldHeight(at.X, at.Z), at.Z);
            _friendly.SetRivalEnemy(enemy);
            enemy.AscendantFoe = _friendly;

            GameEvents.OnAnnouncement?.Invoke($"{profile.Name} answers. It fights {enemy.EnemyName}, not for you.");
            if (ServiceLocator.TryGet<TDCamera>(out var cam))
                cam.Shake(1f, 0.4f);
        }

        private void OnEnemyKilled(Node node)
        {
            if (node == null || node != _enemy) return;
            var enemy = _enemy;
            _enemy = null;
            GD.Print($"[AscendantManager] {enemy.EnemyName} killed");
            GameManager.Instance?.AwardAscendantPoints();
            int pts = MetaPerkRegistry.AscendantKillPoints;
            string pay = enemy.DropValue > 0 ? $" +{enemy.DropValue} Resources" : "";
            if (pts > 0) pay += pay.Length > 0 ? $", +{pts} perk point" : $" +{pts} perk point";
            // Say who made the kill: it looked random when the rival or the towers got it
            string by = enemy.LastHitBy;
            string who = string.IsNullOrEmpty(by) ? $"{enemy.EnemyName} is down"
                : by == _friendly?.AscendantName ? $"{by} finished off {enemy.EnemyName}"
                : $"{char.ToUpperInvariant(by[0])}{by.Substring(1)} brought down {enemy.EnemyName}";
            GameEvents.OnAnnouncement?.Invoke($"{who}.{pay}");
            Celebration.Show("Ascendant down", enemy.EnemyName, $"{who}.{pay}", enemy.AscendantColor, 3.5f);
            KilledBy = by;

            if (Friendly != null && _friendly.IsAlive)
            {
                _friendly.Linger(_lingerSeconds);
                if (ServiceLocator.TryGet<BITCommentary>(out var bit))
                    bit.Say("its rival is down. it is clearing the rest, out of habit. it did not acknowledge us.");
            }

            // After enough runs the body stays for BIT to climb into
            if (_enemyProfile != null && _grid != null && AscendantInhabit.CanInhabit())
            {
                enemy.Visible = false; // the corpse plays the death instead
                var corpse = new Ascendant();
                AddChild(corpse);
                corpse.GlobalPosition = enemy.GlobalPosition;
                corpse.InitializeCorpse(_enemyProfile, _grid, enemy.FacingYaw);
                GameEvents.OnAscendantDefeated?.Invoke(corpse);
            }
        }

        private void OnEnemyLeaked(Node node, Vector3 pos)
        {
            if (node == null || node != _enemy) return;
            _enemy = null;
            GD.Print("[AscendantManager] Enemy Ascendant reached the Spire");
            Friendly?.Linger(_lingerSeconds);
        }

        private void OnAscendantDefeated(Ascendant fallen)
        {
            if (fallen == null || fallen != _friendly) return;
            GD.Print("[AscendantManager] Friendly Ascendant fell");
            _friendly = null;
            if (Enemy != null && _enemy.IsAlive)
            {
                _enemy.AscendantFoe = null;
                GameEvents.OnAnnouncement?.Invoke($"{fallen.AscendantName} fell. {_enemy.EnemyName} is still coming");
                if (ServiceLocator.TryGet<BITCommentary>(out var bit))
                    bit.Say("the ascendant fell. the other one is still walking. it is ours to stop now.");
            }
        }

        // ── Test hooks ──

        /// <summary>Announce an Ascendant for the next wave now (skips the wave and chance rolls).</summary>
        public bool TestAnnounce(string enemyId = null)
        {
            int next = (ServiceLocator.TryGet<VineWaveManager>(out var wm) ? wm.CurrentWave : 0) + 1;
            return Announce(next, enemyId);
        }

        /// <summary>Spawn the announced pair now, the friendly after <paramref name="friendlyDelay"/> s (0 = at once).</summary>
        public VineEnemy TestSpawnNow(int wave, float friendlyDelay = 0f, bool withFriendly = true)
        {
            if (_pendingEnemy == null && !TestAnnounce()) return null;
            if (!withFriendly) _pendingFriendly = null;
            SpawnClash(wave, friendlyDelay);
            return Enemy;
        }

        /// <summary>Forget this run's clash so another can be spawned.</summary>
        public void TestReset()
        {
            _hasSpawnedThisRun = false;
            _pendingEnemy = null;
            _pendingFriendly = null;
            _enemy = null;
            _friendly = null;
        }

        // ── JSON helpers ──
        private static string GetStr(Godot.Collections.Dictionary d, string k)
            => d.ContainsKey(k) ? d[k].AsString() : null;
        private static int GetInt(Godot.Collections.Dictionary d, string k, int fb = 0)
            => d.ContainsKey(k) ? d[k].AsInt32() : fb;
        private static float GetFloat(Godot.Collections.Dictionary d, string k, float fb = 0)
            => d.ContainsKey(k) ? (float)d[k].AsDouble() : fb;

        public override void _ExitTree()
        {
            GameEvents.OnWaveCompleted -= OnWaveCompleted;
            GameEvents.OnWaveStarted -= OnWaveStarted;
            GameEvents.OnEnemyKilled -= OnEnemyKilled;
            GameEvents.OnEnemyLeaked -= OnEnemyLeaked;
            GameEvents.OnAscendantDefeated -= OnAscendantDefeated;
            ServiceLocator.Unregister<AscendantManager>();
        }
    }
}
