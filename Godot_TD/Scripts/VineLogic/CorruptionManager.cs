using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// AXIS Chaos event: AXIS takes the leash off mid-wave. Every enemy on the field goes
    /// berserk (tougher, faster, firing faster) and leaves the path to hunt: some go for BIT, some
    /// wreck the nearest tower, the rest rush the Spire. AXIS also drops squads of tougher
    /// reinforcements onto the field, away from the Spire. BIT is buffed and kills pay triple.
    /// Self-contained: trigger logic, hunting AI, VFX, AXIS commentary. Child of VineBattleScene.
    /// </summary>
    public partial class CorruptionManager : Node
    {
        // Public state for HUD
        public bool IsCorruptionActive { get; private set; }
        public float RemainingDuration { get; private set; }

        // Resource multiplier — read by VineBattleScene.OnResourcesDropped
        public static int ResourceMultiplier { get; private set; } = 1;

        private const float CHAOS_DURATION = 45f;
        /// <summary>Berserk enemies: health, speed, damage and fire-rate multipliers.</summary>
        public const float CHAOS_HP_MULT = 1.3f, CHAOS_SPEED_MULT = 1.2f, CHAOS_DAMAGE_MULT = 1.3f, CHAOS_FIRE_RATE_MULT = 1.4f;
        /// <summary>Reinforcement drops: how many, how far apart, how much tougher than the wave.</summary>
        public const int CHAOS_DROPS = 3;
        public const float CHAOS_DROP_INTERVAL = 12f, CHAOS_DROP_HP_MULT = 1.3f;

        // Trigger scheduling
        private float _chaosTriggerTime = -1f;
        private float _waveElapsed;
        private bool _waveRunning;

        /// <summary>What a berserk enemy is after.</summary>
        public enum Hunt { Bit, Tower, Spire }

        // Chaos state per enemy
        private class WanderState
        {
            public Hunt Role;
            public Node3D Target;
            public List<Vector2I> Path;
            public int PathIndex;
            public float RepathTimer;
            public float MeleeTimer;
            public float OriginalMaxHP;
            public float OriginalArmor;
            public float OriginalSpeedMult;
            public float OriginalDamageMult;
            public float OriginalInterval;
            public float OriginalRange;
        }
        private readonly Dictionary<VineEnemy, WanderState> _wanderStates = new();
        private int _roleCounter;
        private int _dropsLeft;
        private float _dropTimer;

        /// <summary>Enemies AXIS dropped this chaos (for tests and the HUD).</summary>
        public int ReinforcementsDropped { get; private set; }
        /// <summary>The role a berserk enemy was given (tests); null if it isn't berserk.</summary>
        public Hunt? RoleOf(VineEnemy e) => _wanderStates.TryGetValue(e, out var s) ? s.Role : null;

        // Saved player stats for revert
        private bool _playerBoosted;

        // ── Corruption visuals: grid shader swap ──
        private MeshInstance3D _gridMeshNode;
        private Material _gridOriginalMaterial;
        private ShaderMaterial _corruptionGridShader;
        private Vector3 _waveOrigin;

        // ── Corruption visuals: enemy red lights ──
        private readonly List<OmniLight3D> _enemyLights = new();

        // ── Lightning arcs ──
        private readonly List<LightningArc> _groundArcs = new();
        private readonly List<LightningArc> _enemyArcs = new();
        private float _groundArcTimer;
        private float _enemyArcTimer;
        private int _arcFrameCounter;

        // Corruption red color
        private static readonly Color CorruptionRed = new(0.95f, 0.1f, 0.05f);

        // Animation timer for pulse
        private float _corruptionPulseTime;

        // RNG
        private static readonly RandomNumberGenerator _rng = new();

        // AXIS start lines
        private static readonly string[] ChaosStartLines = {
            "I'm bored. Let's see what happens when I take the leash off.",
            "AXIS PROTOCOL OVERRIDE. All units... do whatever you want.",
            "You wanted a challenge? Here. Everyone's invited to the party."
        };

        // AXIS end lines
        private static readonly string[] ChaosEndLines = {
            "Fine, playtime's over. Back in your cages.",
            "That was entertaining. For me, anyway.",
            "Order restored. Try not to look so relieved."
        };

        public override void _Ready()
        {
            GameEvents.OnPhaseChanged += OnPhaseChanged;
            GameEvents.OnWaveStarted += OnWaveStarted;
            GameEvents.OnWaveCompleted += OnWaveCompleted;
        }

        public override void _Process(double delta)
        {
            long __pt = FrameProfiler.Start();
            try
            {
                if (!_waveRunning) return;
                float dt = (float)delta;
                _waveElapsed += dt;

                // Check pending trigger
                if (_chaosTriggerTime > 0 && _waveElapsed >= _chaosTriggerTime && !IsCorruptionActive)
                {
                    _chaosTriggerTime = -1f;
                    TriggerChaos();
                }

                // Tick active chaos
                if (IsCorruptionActive)
                {
                    RemainingDuration -= dt;
                    _corruptionPulseTime += dt;
                    UpdateWanderingEnemies(dt);
                    UpdateGridCorruptionShader();
                    UpdateEnemyCorruptionPulse();
                    UpdateGroundArcs(dt);
                    UpdateEnemyArcs(dt);
                    _arcFrameCounter++;
                    if (RemainingDuration <= 0)
                        RevertChaos();
                }
        
            }
            finally { FrameProfiler.Stop("chaos", __pt); }
        }

        // ── Scheduling ──

        private void OnWaveStarted(int waveNum)
        {
            // S1: floors removed — use wave number for scaling
            _waveElapsed = 0;
            _waveRunning = true;
            _chaosTriggerTime = -1f;

            if (!IsChaosWave(waveNum)) return;

            // One chaos trigger at 15-30s into the wave
            _chaosTriggerTime = _rng.RandfRange(15f, 30f);
        }

        private void OnWaveCompleted(int waveNum)
        {
            _waveRunning = false;
            if (IsCorruptionActive)
                RevertChaos();
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase != GamePhase.Wave && IsCorruptionActive)
                RevertChaos();
            if (phase != GamePhase.Wave)
                _waveRunning = false;
        }

        // ── Trigger ──

        private void TriggerChaos()
        {
            IsCorruptionActive = true;
            RemainingDuration = CHAOS_DURATION;
            _corruptionPulseTime = 0;
            _arcFrameCounter = 0;

            // 1. Resource multiplier
            ResourceMultiplier = 3;

            // 2. Every enemy on the field goes berserk and starts hunting; AXIS drops its first
            // squad of reinforcements straight away and more every few seconds
            var enemies = GetTree().GetNodesInGroup(Constants.GROUP_VINE_ENEMY);
            _wanderStates.Clear();
            _roleCounter = 0;
            ReinforcementsDropped = 0;
            foreach (var e in enemies)
            {
                if (e is VineEnemy ve && ve.IsAlive && ve.OnGrid)
                    ApplyChaosToEnemy(ve);
            }
            _dropsLeft = CHAOS_DROPS;
            _dropTimer = 0f;

            // 3. Player buffs
            if (ServiceLocator.TryGet<VinePlayer>(out var player))
            {
                // Scale and unscale rather than save and restore: a level-up during chaos used to
                // be wiped when the old speed was put back, so BIT got slower as runs went on
                if (!_playerBoosted)
                {
                    _playerBoosted = true;
                    player.MoveSpeed *= 1.5f;
                    player.AttackSpeed *= 1.5f;
                }
                player.ChaosAbilityCooldownMult = 1.5f;
            }

            // 4. Screen shake
            if (ServiceLocator.TryGet<TDCamera>(out var cam))
                cam.Shake(1.5f, 1.0f);

            // 5. AXIS commentary
            string line = ChaosStartLines[_rng.RandiRange(0, ChaosStartLines.Length - 1)];
            GameEvents.OnCommentary?.Invoke("AXIS", line);

            // 6. VFX pulse at grid center
            if (ServiceLocator.TryGet<VineGrid>(out var grid))
            {
                float cx = grid.Width * Constants.VINE_CELL_SIZE / 2f;
                float cz = grid.Height * Constants.VINE_CELL_SIZE / 2f;
                _waveOrigin = new Vector3(cx, 0, cz);
                VfxFactory.SpawnCorruptionPulse(GetTree(), _waveOrigin, CorruptionRed);
            }

            // 7. Start visuals
            SwapGridToCorruptionShader();
            // (Berserk enemies got their red light in ApplyChaosToEnemy)
            SpawnInitialGroundArcs();
            SpawnInitialEnemyArcs();

            GameEvents.OnCorruptionStarted?.Invoke(CorruptionType.AxisChaos);
        }

        private void ApplyChaosToEnemy(VineEnemy ve, bool reinforcement = false)
        {
            var state = new WanderState
            {
                OriginalMaxHP = ve.MaxHealth,
                OriginalArmor = ve.ArmorBonus,
                OriginalSpeedMult = ve.SpeedMultiplier,
                OriginalDamageMult = ve.DamageMultiplier,
                OriginalInterval = ve.AttackInterval,
                OriginalRange = ve.AttackRange,
            };
            // Berserk: tougher (healed to the new max), faster, harder hitting, faster firing,
            // and a gun reaches further
            ve.SetChaosHP(state.OriginalMaxHP * CHAOS_HP_MULT);
            ve.ArmorBonus += state.OriginalMaxHP * 0.1f;
            ve.SpeedMultiplier = state.OriginalSpeedMult * CHAOS_SPEED_MULT; // rushers lose it below
            ve.DamageMultiplier = state.OriginalDamageMult * CHAOS_DAMAGE_MULT;
            if (state.OriginalInterval > 0f) ve.AttackInterval = state.OriginalInterval / CHAOS_FIRE_RATE_MULT;
            if (state.OriginalRange > 0f) ve.AttackRange = state.OriginalRange + 2f;

            // Roles in turn so every chaos has all three: of every five, two hunt BIT, two wreck
            // towers and one rushes the Spire. Reinforcements only fight: a drop is a brawl to
            // win, not a stream of leaks.
            state.Role = (_roleCounter++ % 5) switch { 0 or 3 => Hunt.Bit, 1 or 4 => Hunt.Tower, _ => Hunt.Spire };
            if (reinforcement && state.Role == Hunt.Spire) state.Role = Hunt.Bit;
            if (ve.IsBoss || ve.IsFlying) state.Role = Hunt.Spire; // bosses and flyers keep coming, just angrier
            ve.IsWandering = state.Role != Hunt.Spire;
            // Rushers keep their own pace: they walk the path, and faster rushers only meant leaks
            if (state.Role == Hunt.Spire) ve.SpeedMultiplier = state.OriginalSpeedMult;
            _wanderStates[ve] = state;
            if (ServiceLocator.TryGet<VineGrid>(out var grid) && ve.IsWandering)
                Retarget(ve, state, grid);
            if (IsCorruptionActive) AttachCorruptionLight(ve);
        }

        private void RevertChaos()
        {
            // 1. Reset scrap multiplier
            ResourceMultiplier = 1;

            // 2. Revert enemies: back to their own stats, and back on a path to the Spire
            foreach (var (ve, state) in _wanderStates)
            {
                if (!IsInstanceValid(ve) || !ve.IsAlive) continue;
                ve.RevertChaosHP(state.OriginalMaxHP);
                ve.ArmorBonus = state.OriginalArmor;
                ve.SpeedMultiplier = state.OriginalSpeedMult;
                ve.DamageMultiplier = state.OriginalDamageMult;
                ve.AttackInterval = state.OriginalInterval;
                ve.AttackRange = state.OriginalRange;
                bool was = ve.IsWandering;
                ve.IsWandering = false;
                if (was) ve.RepathToSpire();
            }
            _wanderStates.Clear();
            _dropsLeft = 0;

            var allEnemies = GetTree().GetNodesInGroup(Constants.GROUP_VINE_ENEMY);
            foreach (var e in allEnemies)
            {
                if (e is VineEnemy enemy && enemy.IsWandering)
                {
                    enemy.IsWandering = false;
                    enemy.RepathToSpire();
                }
            }

            // 3. Restore player stats
            if (ServiceLocator.TryGet<VinePlayer>(out var player))
            {
                if (_playerBoosted)
                {
                    _playerBoosted = false;
                    player.MoveSpeed /= 1.5f;
                    player.AttackSpeed /= 1.5f;
                }
                player.ChaosAbilityCooldownMult = 1f;
            }

            // 4. Clean up VFX
            RestoreGridMaterial();
            RemoveEnemyLights();
            RemoveAllArcs();

            IsCorruptionActive = false;
            RemainingDuration = 0;

            // 5. AXIS end commentary
            string line = ChaosEndLines[_rng.RandiRange(0, ChaosEndLines.Length - 1)];
            GameEvents.OnCommentary?.Invoke("AXIS", line);

            GameEvents.OnCorruptionEnded?.Invoke(CorruptionType.AxisChaos);
        }

        // ── Hunting AI ──

        private void UpdateWanderingEnemies(float dt)
        {
            if (!ServiceLocator.TryGet<VineGrid>(out var grid)) return;

            // Reinforcement drops
            if (_dropsLeft > 0 && (_dropTimer -= dt) <= 0f)
            {
                _dropTimer = CHAOS_DROP_INTERVAL;
                _dropsLeft--;
                DropReinforcements(grid);
            }

            // New arrivals (spawned during chaos, or just walked onto the grid) go berserk too
            var allEnemies = GetTree().GetNodesInGroup(Constants.GROUP_VINE_ENEMY);
            foreach (var e in allEnemies)
            {
                if (e is VineEnemy ve && ve.IsAlive && !_wanderStates.ContainsKey(ve) && ve.OnGrid)
                    ApplyChaosToEnemy(ve);
            }

            var dead = new List<VineEnemy>();
            foreach (var (ve, state) in _wanderStates)
            {
                if (!IsInstanceValid(ve) || !ve.IsAlive) { dead.Add(ve); continue; }
                if (!ve.IsWandering) continue; // Spire rushers walk their own path, buffed

                state.RepathTimer -= dt;
                bool targetGone = state.Target == null || !IsInstanceValid(state.Target)
                    || (state.Target is VineNode n && n.IsDestroyed)
                    || (state.Target is VinePlayer p && !p.IsAlive);
                if (targetGone || state.RepathTimer <= 0f) Retarget(ve, state, grid);
                if (state.Target == null) continue;

                var targetPos = state.Target.GlobalPosition;
                float dist = Flat(ve.GlobalPosition, targetPos);
                // Close enough: stand and fight (guns fire through UpdateRangedAttack; the rest hit)
                float reach = ve.AttackRange > 0f ? Mathf.Min(ve.AttackRange * 0.8f, 7f) : 1.2f;
                if (state.Target is VineNode) reach = ve.AttackRange > 0f ? Mathf.Min(ve.AttackRange * 0.8f, 5f) : 1.9f;
                if (dist <= reach)
                {
                    ve.Face(targetPos, dt);
                    ve.ChaosStep(ve.GlobalPosition, CHAOS_SPEED_MULT, dt); // ticks slows/stuns
                    if (ve.AttackRange <= 0f || state.Target is VineNode) Melee(ve, state, dt);
                    continue;
                }

                // Walk the path to the target (Ghosts go straight)
                Vector3 step;
                if (ve.Faction == VineEnemyFaction.Ghost || state.Path == null || state.PathIndex >= state.Path.Count)
                    step = targetPos;
                else
                {
                    step = grid.GridToWorld(state.Path[state.PathIndex]);
                    if (Flat(ve.GlobalPosition, step) < 0.25f)
                    {
                        state.PathIndex++;
                        if (state.PathIndex < state.Path.Count) step = grid.GridToWorld(state.Path[state.PathIndex]);
                        else step = targetPos;
                    }
                }
                if (!ve.ChaosStep(step, 1f, dt)) state.RepathTimer = 0f;
            }
            foreach (var d in dead) _wanderStates.Remove(d);
        }

        private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

        /// <summary>Wreckers and gunless hunters hit what they reach.</summary>
        private static void Melee(VineEnemy ve, WanderState state, float dt)
        {
            state.MeleeTimer -= dt;
            if (state.MeleeTimer > 0f) return;
            state.MeleeTimer = 1.2f;
            float dmg = (4f + state.OriginalMaxHP * 0.04f) * ve.DamageMultiplier;
            if (ve.Faction == VineEnemyFaction.Brute) dmg *= 1.6f;
            switch (state.Target)
            {
                case VineNode node when !node.IsDestroyed: node.TakeDamage(dmg); break;
                case VinePlayer player when player.IsAlive: return; // contact damage handles BIT
                default: return;
            }
            ve.PlayAttack();
        }

        /// <summary>Pick (or refresh) the hunted target and the path to it.</summary>
        private void Retarget(VineEnemy ve, WanderState state, VineGrid grid)
        {
            state.RepathTimer = _rng.RandfRange(1.2f, 1.8f);
            Node3D target = null;
            if (state.Role == Hunt.Bit && ServiceLocator.TryGet<VinePlayer>(out var player) && player.IsAlive && !player.IsDocked)
                target = player;
            if (target == null)
            {
                // Nearest standing tower (walls count: AXIS likes breaking things)
                float best = float.MaxValue;
                foreach (var n in GetTree().GetNodesInGroup(Constants.GROUP_VINE_NODE))
                {
                    if (n is not VineNode vn || vn.IsDestroyed) continue;
                    float d = Flat(ve.GlobalPosition, vn.GlobalPosition);
                    if (d < best) { best = d; target = vn; }
                }
            }
            if (target == null)
            {
                // Nothing to hunt: rush the Spire on the normal path
                state.Role = Hunt.Spire;
                ve.IsWandering = false;
                ve.RepathToSpire();
                state.Target = null;
                return;
            }
            state.Target = target;
            if (ve.Faction == VineEnemyFaction.Ghost || !ServiceLocator.TryGet<VinePathfinder>(out var pf)) { state.Path = null; return; }
            var from = grid.WorldToGrid(ve.GlobalPosition);
            var to = NearestWalkable(grid, grid.WorldToGrid(target.GlobalPosition), from);
            state.Path = to.HasValue ? pf.FindPath(from, to.Value) : null;
            state.PathIndex = state.Path != null && state.Path.Count > 1 ? 1 : 0;
        }

        /// <summary>The cell itself if an enemy can stand there, else the closest open cell around it.</summary>
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

        /// <summary>
        /// A squad of tougher enemies from this wave's roster, dropped onto open ground on the
        /// far side of the field from the Spire, with a red flash where each lands.
        /// </summary>
        private void DropReinforcements(VineGrid grid)
        {
            if (!ServiceLocator.TryGet<VineWaveManager>(out var waves)) return;
            int wave = waves.CurrentWave;
            int count = Mathf.Clamp(1 + wave / 4, 2, 6);
            var spire = grid.ExitPoint;
            // Candidate cells: open, not next to the Spire, in the half of the map away from it
            var cells = new List<Vector2I>();
            float half = Mathf.Max(grid.Width, grid.Height) * 0.35f;
            for (int x = 1; x < grid.Width - 1; x++)
                for (int y = 1; y < grid.Height - 1; y++)
                {
                    var c = new Vector2I(x, y);
                    if (!grid.IsWalkable(c) || grid.GetCell(c) != VineCellType.Empty) continue;
                    if ((c - spire).Length() < half) continue;
                    cells.Add(c);
                }
            if (cells.Count == 0) return;
            // One landing zone per drop, the squad around it
            var zone = cells[_rng.RandiRange(0, cells.Count - 1)];
            int dropped = 0;
            for (int i = 0; i < count * 10 && dropped < count; i++)
            {
                var c = zone + new Vector2I(_rng.RandiRange(-2, 2), _rng.RandiRange(-2, 2));
                if (!grid.InBounds(c) || !grid.IsWalkable(c)) continue;
                var e = waves.SpawnReinforcement(c, CHAOS_DROP_HP_MULT);
                if (e == null) continue;
                dropped++;
                ReinforcementsDropped++;
                var at = e.GlobalPosition;
                VfxFactory.SpawnCorruptionPulse(GetTree(), at, CorruptionRed);
                ApplyChaosToEnemy(e, reinforcement: true);
            }
            if (dropped > 0)
            {
                GD.Print($"[AXIS] Chaos drop: {dropped} reinforcements at ({zone.X},{zone.Y})");
                if (ServiceLocator.TryGet<TDCamera>(out var cam)) cam.Shake(0.6f, 0.4f);
                string[] lines = { "Reinforcements. You're welcome.", "More friends for you.", "Special delivery." };
                GameEvents.OnCommentary?.Invoke("AXIS", lines[_rng.RandiRange(0, lines.Length - 1)]);
            }
        }

        // ══════════════════════════════════════════════════════
        // ── Corruption Visuals: Grid Wave Shader ──
        // ══════════════════════════════════════════════════════

        private void SwapGridToCorruptionShader()
        {
            // Scrapyard has no prominent grid lines — skip shader swap
            if (PlanetTheme.Current is ScrapyardPlanetTheme) return;

            if (!ServiceLocator.TryGet<VineGrid>(out var grid)) return;

            _gridMeshNode = grid.GetNodeOrNull<MeshInstance3D>("GridLines");
            if (_gridMeshNode == null) return;

            _gridOriginalMaterial = _gridMeshNode.MaterialOverride;

            var baseColor = new Vector3(TronTheme.GridCyan.R, TronTheme.GridCyan.G, TronTheme.GridCyan.B);
            _corruptionGridShader = TronTheme.MakeCorruptionGridShader(baseColor, 0.6f, 0.8f);
            _corruptionGridShader.SetShaderParameter("wave_origin", _waveOrigin);
            _corruptionGridShader.SetShaderParameter("wave_time", 0.0f);
            _corruptionGridShader.SetShaderParameter("corruption_mix", 0.0f);
            _gridMeshNode.MaterialOverride = _corruptionGridShader;
        }

        private void UpdateGridCorruptionShader()
        {
            if (_corruptionGridShader == null) return;
            float ramp = Mathf.Clamp(_corruptionPulseTime / 1.5f, 0f, 1f);
            _corruptionGridShader.SetShaderParameter("wave_time", _corruptionPulseTime);
            _corruptionGridShader.SetShaderParameter("corruption_mix", ramp);
        }

        private void RestoreGridMaterial()
        {
            if (_gridMeshNode != null && IsInstanceValid(_gridMeshNode) && _gridOriginalMaterial != null)
                _gridMeshNode.MaterialOverride = _gridOriginalMaterial;
            _gridMeshNode = null;
            _gridOriginalMaterial = null;
            _corruptionGridShader = null;
        }

        // ══════════════════════════════════════════════════════
        // ── Corruption Visuals: Enemy Red Lighting ──
        // ══════════════════════════════════════════════════════

        private void SpawnEnemyLights()
        {
            var enemies = GetTree().GetNodesInGroup(Constants.GROUP_VINE_ENEMY);
            foreach (var e in enemies)
            {
                if (e is VineEnemy ve && ve.IsAlive)
                    AttachCorruptionLight(ve);
            }
        }

        private void AttachCorruptionLight(Node3D target)
        {
            var light = new OmniLight3D();
            light.Name = "CorruptionLight";
            light.LightColor = CorruptionRed;
            light.LightEnergy = 2f;
            light.OmniRange = 4f;
            light.OmniAttenuation = 1.5f;
            light.ShadowEnabled = false;
            light.Position = new Vector3(0, 0.5f, 0);
            target.AddChild(light);
            _enemyLights.Add(light);
        }

        private void UpdateEnemyCorruptionPulse()
        {
            float t = 0.5f + 0.5f * Mathf.Sin(_corruptionPulseTime * 5f);
            float lightEnergy = Mathf.Lerp(1f, 3.5f, t);

            for (int i = _enemyLights.Count - 1; i >= 0; i--)
            {
                if (!IsInstanceValid(_enemyLights[i]))
                {
                    _enemyLights.RemoveAt(i);
                    continue;
                }
                _enemyLights[i].LightEnergy = lightEnergy;
            }

            var enemies = GetTree().GetNodesInGroup(Constants.GROUP_VINE_ENEMY);
            foreach (var e in enemies)
            {
                if (e is VineEnemy ve && ve.IsAlive)
                    PulseNodeEmissionRed(ve, t);
            }
        }

        private static void PulseNodeEmissionRed(Node node, float t)
        {
            if (node is MeshInstance3D mesh && mesh.MaterialOverride is StandardMaterial3D mat
                && mat.EmissionEnabled)
            {
                float energy = Mathf.Lerp(mat.EmissionEnergyMultiplier, 2.5f, t * 0.6f);
                mat.EmissionEnergyMultiplier = energy;
                mat.Emission = mat.Emission.Lerp(CorruptionRed, t * 0.5f);
            }
            foreach (var child in node.GetChildren())
                PulseNodeEmissionRed(child, t);
        }

        private void RemoveEnemyLights()
        {
            foreach (var light in _enemyLights)
            {
                if (IsInstanceValid(light))
                    light.QueueFree();
            }
            _enemyLights.Clear();

            var enemies = GetTree().GetNodesInGroup(Constants.GROUP_VINE_ENEMY);
            foreach (var e in enemies)
            {
                if (e is VineEnemy ve && IsInstanceValid(ve) && ve.IsAlive)
                    ResetEnemyEmission(ve);
            }
        }

        private static void ResetEnemyEmission(Node node)
        {
            bool isScrapyard = PlanetTheme.Current is ScrapyardPlanetTheme;
            float baseEnergy = isScrapyard ? 0.15f : 0.4f;

            if (node is MeshInstance3D mesh && mesh.MaterialOverride is StandardMaterial3D mat
                && mat.EmissionEnabled)
            {
                mat.EmissionEnergyMultiplier = baseEnergy;
            }
            foreach (var child in node.GetChildren())
                ResetEnemyEmission(child);
        }

        // ══════════════════════════════════════════════════════
        // ── Lightning Arcs ──
        // ══════════════════════════════════════════════════════

        private class LightningArc
        {
            public MeshInstance3D MeshNode;
            public float Lifetime;
            public float Age;
            public Vector3 From;
            public Vector3 To;
            public int Segments;
            private int _rebuildCounter;

            public LightningArc(Node parent, Vector3 from, Vector3 to, float lifetime, int segments = 6)
            {
                From = from;
                To = to;
                Lifetime = lifetime;
                Age = 0;
                Segments = segments;

                MeshNode = new MeshInstance3D();
                MeshNode.MaterialOverride = TronTheme.MakeLightningArcMaterial();
                parent.AddChild(MeshNode);
                Rebuild();
            }

            public void Rebuild()
            {
                var im = new ImmediateMesh();
                im.SurfaceBegin(Mesh.PrimitiveType.Lines);

                Vector3 dir = To - From;
                Vector3 perp = dir.Cross(Vector3.Up).Normalized();
                if (perp.LengthSquared() < 0.001f)
                    perp = dir.Cross(Vector3.Right).Normalized();

                Vector3 prev = From;
                for (int i = 1; i <= Segments; i++)
                {
                    Vector3 next;
                    if (i == Segments)
                    {
                        next = To;
                    }
                    else
                    {
                        float frac = (float)i / Segments;
                        next = From + dir * frac;
                        float jitterXZ = _rng.RandfRange(-0.3f, 0.3f);
                        float jitterY = _rng.RandfRange(-0.1f, 0.1f);
                        next += perp * jitterXZ + Vector3.Up * jitterY;
                    }
                    im.SurfaceAddVertex(prev);
                    im.SurfaceAddVertex(next);
                    prev = next;
                }

                im.SurfaceEnd();
                MeshNode.Mesh = im;
            }

            public bool Tick(float dt)
            {
                Age += dt;
                if (Age >= Lifetime) return false;
                _rebuildCounter++;
                if (_rebuildCounter % 3 == 0)
                    Rebuild();
                return true;
            }

            public void Destroy()
            {
                if (Node.IsInstanceValid(MeshNode))
                    MeshNode.QueueFree();
            }
        }

        // ── Ground Arcs ──

        private void SpawnInitialGroundArcs()
        {
            if (!ServiceLocator.TryGet<VineGrid>(out var grid)) return;
            _groundArcTimer = 0;

            float cs = Constants.VINE_CELL_SIZE;
            float maxX = grid.Width * cs;
            float maxZ = grid.Height * cs;

            for (int i = 0; i < 8; i++)
                SpawnOneGroundArc(maxX, maxZ);
        }

        private void SpawnOneGroundArc(float maxX, float maxZ)
        {
            float cs = Constants.VINE_CELL_SIZE;
            float x1 = Mathf.Floor(_rng.RandfRange(0, maxX / cs)) * cs;
            float z1 = Mathf.Floor(_rng.RandfRange(0, maxZ / cs)) * cs;
            float dx = _rng.RandiRange(-5, 5) * cs;
            float dz = _rng.RandiRange(-5, 5) * cs;
            if (Mathf.Abs(dx) < cs && Mathf.Abs(dz) < cs) dx = 2 * cs;
            float x2 = Mathf.Clamp(x1 + dx, 0, maxX);
            float z2 = Mathf.Clamp(z1 + dz, 0, maxZ);

            var from = new Vector3(x1, 0.08f, z1);
            var to = new Vector3(x2, 0.08f, z2);

            var arc = new LightningArc(this, from, to,
                _rng.RandfRange(0.3f, 0.6f), _rng.RandiRange(5, 8));
            _groundArcs.Add(arc);
        }

        private void UpdateGroundArcs(float dt)
        {
            if (!ServiceLocator.TryGet<VineGrid>(out var grid)) return;
            float maxX = grid.Width * Constants.VINE_CELL_SIZE;
            float maxZ = grid.Height * Constants.VINE_CELL_SIZE;

            for (int i = _groundArcs.Count - 1; i >= 0; i--)
            {
                if (!_groundArcs[i].Tick(dt))
                {
                    _groundArcs[i].Destroy();
                    _groundArcs.RemoveAt(i);
                }
            }

            _groundArcTimer += dt;
            if (_groundArcTimer >= 0.15f)
            {
                _groundArcTimer = 0;
                if (_groundArcs.Count < 10)
                    SpawnOneGroundArc(maxX, maxZ);
            }
        }

        // ── Enemy Arcs ──

        private void SpawnInitialEnemyArcs()
        {
            _enemyArcTimer = 0;
            var enemies = GetTree().GetNodesInGroup(Constants.GROUP_VINE_ENEMY);
            foreach (var e in enemies)
            {
                if (e is VineEnemy ve && ve.IsAlive)
                    SpawnEnemyArc(ve);
            }
        }

        private void SpawnEnemyArc(Node3D enemy)
        {
            var pos = enemy.GlobalPosition;
            float dist = _rng.RandfRange(1f, 2f);
            float angle = _rng.RandfRange(0, Mathf.Tau);
            var end = pos + new Vector3(Mathf.Cos(angle) * dist, 0, Mathf.Sin(angle) * dist);
            pos.Y = 0.1f;
            end.Y = _rng.RandfRange(0.05f, 0.3f);

            var arc = new LightningArc(this, pos, end,
                _rng.RandfRange(0.2f, 0.4f), _rng.RandiRange(4, 6));
            _enemyArcs.Add(arc);
        }

        private void UpdateEnemyArcs(float dt)
        {
            for (int i = _enemyArcs.Count - 1; i >= 0; i--)
            {
                if (!_enemyArcs[i].Tick(dt))
                {
                    _enemyArcs[i].Destroy();
                    _enemyArcs.RemoveAt(i);
                }
            }

            _enemyArcTimer += dt;
            if (_enemyArcTimer >= 0.2f)
            {
                _enemyArcTimer = 0;
                var enemies = GetTree().GetNodesInGroup(Constants.GROUP_VINE_ENEMY);
                foreach (var e in enemies)
                {
                    if (e is VineEnemy ve && ve.IsAlive && _rng.Randf() < 0.4f)
                        SpawnEnemyArc(ve);
                }
            }
        }

        private void RemoveAllArcs()
        {
            foreach (var arc in _groundArcs) arc.Destroy();
            _groundArcs.Clear();
            foreach (var arc in _enemyArcs) arc.Destroy();
            _enemyArcs.Clear();
        }

        // ── Helpers ──

        public static Color GetCorruptionColor() => CorruptionRed;

        /// <summary>
        /// AXIS takes over on waves 5, 9, 13 and so on (the wave card warns ahead). It fired on
        /// every wave from 2 when chaos only made enemies mill about, which made every wave
        /// easier: they stopped walking at the Spire. Now that it hunts and drops
        /// reinforcements, every wave would be too much, and a known one can be prepared for.
        /// </summary>
        public static bool IsChaosWave(int wave) => wave >= 5 && (wave - 5) % 4 == 0;

        public static string GetCorruptionName() => "AXIS CHAOS";

        /// <summary>
        /// Debug entry point — force-trigger chaos. Reverts any active chaos first.
        /// </summary>
        public void DebugTriggerChaos()
        {
            if (IsCorruptionActive)
                RevertChaos();
            TriggerChaos();
        }

        /// <summary>Debug/test: end the chaos now.</summary>
        public void DebugEndChaos()
        {
            if (IsCorruptionActive) RevertChaos();
        }

        public override void _ExitTree()
        {
            GameEvents.OnPhaseChanged -= OnPhaseChanged;
            GameEvents.OnWaveStarted -= OnWaveStarted;
            GameEvents.OnWaveCompleted -= OnWaveCompleted;

            if (IsCorruptionActive)
                RevertChaos();
        }
    }
}
