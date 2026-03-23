using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Manages Ascendant spawning, rivalries, and aftermath.
    ///
    /// Ascendants appear when the player has held long enough and extracted enough
    /// to make the battlefield interesting. Enemy Ascendant arrives first.
    /// Friendly Ascendant responds 1-2 seconds later — not summoned by the player,
    /// but because their rival showed up and they won't let that stand.
    ///
    /// If friendly wins: leaves without acknowledgment. Player was never the point.
    /// If friendly loses: enemy turns to Spire for ~5 seconds of massive damage before departing.
    /// </summary>
    public partial class AscendantManager : Node
    {
        private List<AscendantProfile> _profiles = new();
        private Dictionary<string, string> _rivalries = new();

        // Spawn thresholds
        private int _minWave = 12;
        private int _minExtraction = 200;
        private int _checkIntervalWaves = 3;
        private float _spawnChance = 0.4f;

        // State
        private bool _hasSpawnedThisRun;
        private Ascendant _enemyAscendant;
        private Ascendant _friendlyAscendant;
        private float _spireAttackTimer;
        private bool _enemyTargetingSpire;

        private VineGrid _grid;
        private RandomNumberGenerator _rng = new();

        private const string DataPath = "res://Data/ascendants.json";

        public override void _Ready()
        {
            LoadData();

            if (ServiceLocator.TryGet<VineGrid>(out var grid))
                _grid = grid;

            GameEvents.OnWaveMilestone += OnWaveMilestone;
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

            // Spawn thresholds
            if (root.ContainsKey("spawn_thresholds") &&
                root["spawn_thresholds"].Obj is Godot.Collections.Dictionary thresholds)
            {
                _minWave = GetInt(thresholds, "min_wave", 12);
                _minExtraction = GetInt(thresholds, "min_extraction", 200);
                _checkIntervalWaves = GetInt(thresholds, "check_interval_waves", 3);
                _spawnChance = GetFloat(thresholds, "spawn_chance_per_check", 0.4f);
            }

            // Ascendant profiles
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
                        ModelScale = GetFloat(d, "model_scale", 3.5f)
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

            // Rivalries
            if (root.ContainsKey("rivalries") &&
                root["rivalries"].Obj is Godot.Collections.Dictionary rivalries)
            {
                foreach (var key in rivalries.Keys)
                    _rivalries[key.AsString()] = rivalries[key].AsString();
            }

            GD.Print($"[AscendantManager] Loaded {_profiles.Count} profiles, {_rivalries.Count} rivalries");
        }

        private void OnWaveMilestone(int wave, string type)
        {
            if (_hasSpawnedThisRun) return;
            if (wave < _minWave) return;

            int extracted = GameManager.Instance?.TotalExtracted ?? 0;
            if (extracted < _minExtraction) return;

            // Check at intervals
            if ((wave - _minWave) % _checkIntervalWaves != 0) return;

            // Roll for spawn
            if (_rng.Randf() > _spawnChance) return;

            SpawnAscendantClash();
        }

        private void SpawnAscendantClash()
        {
            _hasSpawnedThisRun = true;
            int planet = GameManager.Instance?.CurrentPlanet ?? 1;

            // Pick enemy Ascendant (prefer planet affinity)
            var enemyCandidates = _profiles
                .Where(p => p.Faction == "enemy" &&
                       (p.PlanetAffinity == null || p.PlanetAffinity.Contains(planet)))
                .ToList();
            if (enemyCandidates.Count == 0)
                enemyCandidates = _profiles.Where(p => p.Faction == "enemy").ToList();
            if (enemyCandidates.Count == 0) return;

            var enemyProfile = enemyCandidates[_rng.RandiRange(0, enemyCandidates.Count - 1)];

            // Find rival
            AscendantProfile friendlyProfile = null;
            if (_rivalries.TryGetValue(enemyProfile.Id, out var rivalId))
                friendlyProfile = _profiles.FirstOrDefault(p => p.Id == rivalId);

            // Fallback: any friendly
            friendlyProfile ??= _profiles
                .Where(p => p.Faction == "friendly")
                .OrderBy(_ => _rng.Randf())
                .FirstOrDefault();

            if (friendlyProfile == null) return;

            GD.Print($"[AscendantManager] CLASH: {enemyProfile.Name} vs {friendlyProfile.Name}");

            // Spawn enemy on map edge
            _enemyAscendant = new Ascendant();
            AddChild(_enemyAscendant);
            _enemyAscendant.GlobalPosition = GetEdgeSpawnPosition(0);  // Left edge
            _enemyAscendant.Initialize(enemyProfile, _grid);

            // Commentary
            GameEvents.OnBossSpawned?.Invoke();

            // Spawn friendly 1.5 seconds later on opposite edge
            var timer = GetTree().CreateTimer(1.5);
            timer.Timeout += () =>
            {
                _friendlyAscendant = new Ascendant();
                AddChild(_friendlyAscendant);
                _friendlyAscendant.GlobalPosition = GetEdgeSpawnPosition(2);  // Right edge
                _friendlyAscendant.Initialize(friendlyProfile, _grid);

                // Set rivals — they only fight each other
                _enemyAscendant.SetRival(_friendlyAscendant);
                _friendlyAscendant.SetRival(_enemyAscendant);

                // Screen shake — something massive just arrived
                if (ServiceLocator.TryGet<TDCamera>(out var cam))
                    cam.Shake(1f, 0.4f);
            };
        }

        private void OnAscendantDefeated(Ascendant fallen)
        {
            if (fallen == _friendlyAscendant)
            {
                // Friendly lost — enemy turns to Spire briefly
                GD.Print($"[AscendantManager] Friendly Ascendant fell. Enemy targeting Spire for 5 seconds.");
                _enemyTargetingSpire = true;
                _spireAttackTimer = 5f;

                // Commentary — BIT acknowledges
                if (ServiceLocator.TryGet<BITCommentary>(out var bit))
                    bit.Say("the ascendant fell. the other one is turning toward the spire. this is not good.");
            }
            else if (fallen == _enemyAscendant)
            {
                // Enemy fell — friendly leaves without acknowledgment
                GD.Print($"[AscendantManager] Enemy Ascendant fell. Friendly departing.");

                if (ServiceLocator.TryGet<BITCommentary>(out var bit))
                    bit.Say("the ascendant is leaving. it did not acknowledge us. we were never the point.");

                // Friendly walks off the map edge
                if (_friendlyAscendant != null && _friendlyAscendant.IsAlive)
                {
                    var departureLoc = GetEdgeSpawnPosition(2) + new Vector3(50, 0, 0);
                    // Simple departure — just move off screen
                    var tween = CreateTween();
                    tween.TweenProperty(_friendlyAscendant, "global_position", departureLoc, 4f);
                    tween.TweenCallback(Callable.From(() => _friendlyAscendant?.QueueFree()));
                }
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            if (!_enemyTargetingSpire || _enemyAscendant == null || !_enemyAscendant.IsAlive) return;

            _spireAttackTimer -= (float)delta;

            // Enemy attacks Spire
            if (ServiceLocator.TryGet<VineHarvester>(out var harvester))
            {
                harvester.TakeDamage(_enemyAscendant.Damage * (float)delta);
            }

            if (_spireAttackTimer <= 0)
            {
                // Enemy departs
                GD.Print($"[AscendantManager] Enemy Ascendant departing after Spire attack.");
                _enemyTargetingSpire = false;

                var departureLoc = GetEdgeSpawnPosition(0) + new Vector3(-50, 0, 0);
                var tween = CreateTween();
                tween.TweenProperty(_enemyAscendant, "global_position", departureLoc, 3f);
                tween.TweenCallback(Callable.From(() => _enemyAscendant?.QueueFree()));
            }
        }

        private Vector3 GetEdgeSpawnPosition(int side)
        {
            if (_grid == null) return Vector3.Zero;

            // 0=left, 1=top, 2=right, 3=bottom
            return side switch
            {
                0 => _grid.GridToWorld(0, _grid.Height / 2) + new Vector3(-8, 0, 0),
                1 => _grid.GridToWorld(_grid.Width / 2, 0) + new Vector3(0, 0, -8),
                2 => _grid.GridToWorld(_grid.Width, _grid.Height / 2) + new Vector3(8, 0, 0),
                3 => _grid.GridToWorld(_grid.Width / 2, _grid.Height) + new Vector3(0, 0, 8),
                _ => _grid.GridToWorld(_grid.Width / 2, _grid.Height / 2)
            };
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
            GameEvents.OnWaveMilestone -= OnWaveMilestone;
            ServiceLocator.Unregister<AscendantManager>();
        }
    }
}
