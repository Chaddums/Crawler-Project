using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// AXIS Chaos event — a single dramatic mid-wave disruption inspired by DCC demon events.
    /// Enemies go berserk and wander off-path, player gets buffed, kills award 3x scrap.
    /// Duration ~60s. Self-contained: trigger logic, wandering AI, VFX, AXIS commentary.
    /// Added as child of VineBattleScene.
    /// </summary>
    public partial class CorruptionManager : Node
    {
        // Public state for HUD
        public bool IsCorruptionActive { get; private set; }
        public float RemainingDuration { get; private set; }

        // Scrap multiplier — read by VineBattleScene.OnResourcesDropped
        public static int ScrapMultiplier { get; private set; } = 1;

        private const float CHAOS_DURATION = 60f;

        // Trigger scheduling
        private float _chaosTriggerTime = -1f;
        private float _waveElapsed;
        private bool _waveRunning;

        // Wandering state per enemy
        private class WanderState
        {
            public Vector3 Target;
            public float RetargetTimer;
            public float OriginalMaxHP;
            public float OriginalArmor;
        }
        private readonly Dictionary<VineEnemy, WanderState> _wanderStates = new();

        // Saved player stats for revert
        private float _savedPlayerMoveSpeed;
        private float _savedPlayerAttackSpeed;

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

        // ── Scheduling ──

        private void OnWaveStarted(int waveNum)
        {
            // S1: floors removed — use wave number for scaling
            _waveElapsed = 0;
            _waveRunning = true;
            _chaosTriggerTime = -1f;

            if (waveNum <= 1) return; // No chaos on first wave

            // One chaos trigger at 15-30s into wave
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

            // 1. Scrap multiplier
            ScrapMultiplier = 3;

            // 2. Enemy buffs: +50% HP (heal to new max), armor proportional to base HP
            var enemies = GetTree().GetNodesInGroup(Constants.GROUP_VINE_ENEMY);
            _wanderStates.Clear();
            foreach (var e in enemies)
            {
                if (e is VineEnemy ve && ve.IsAlive)
                    ApplyChaosToEnemy(ve);
            }

            // 3. Player buffs
            if (ServiceLocator.TryGet<VinePlayer>(out var player))
            {
                _savedPlayerMoveSpeed = player.MoveSpeed;
                _savedPlayerAttackSpeed = player.AttackSpeed;
                player.MoveSpeed *= 1.5f;
                player.AttackSpeed *= 1.5f;
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
            SpawnEnemyLights();
            SpawnInitialGroundArcs();
            SpawnInitialEnemyArcs();

            GameEvents.OnCorruptionStarted?.Invoke(CorruptionType.AxisChaos);
        }

        private void ApplyChaosToEnemy(VineEnemy ve)
        {
            float originalMax = ve.MaxHealth;
            float originalArmor = ve.ArmorBonus;

            // +50% HP, healed to new max
            ve.SetChaosHP(originalMax * 1.5f);
            // Armor bonus proportional to base HP (roughly 10% of max HP)
            ve.ArmorBonus += originalMax * 0.1f;

            // Start wandering
            ve.IsWandering = true;
            _wanderStates[ve] = new WanderState
            {
                Target = PickRandomWalkable(ve.GlobalPosition),
                RetargetTimer = _rng.RandfRange(2f, 3f),
                OriginalMaxHP = originalMax,
                OriginalArmor = originalArmor
            };
        }

        private void RevertChaos()
        {
            // 1. Reset scrap multiplier
            ScrapMultiplier = 1;

            // 2. Revert enemies
            foreach (var (ve, state) in _wanderStates)
            {
                if (!IsInstanceValid(ve) || !ve.IsAlive) continue;
                ve.RevertChaosHP(state.OriginalMaxHP);
                ve.ArmorBonus = state.OriginalArmor;
                ve.IsWandering = false;
                ve.TryRepath();
            }
            _wanderStates.Clear();

            // Also clear wandering flag on any enemies that spawned during chaos
            // but weren't in the dict (edge case: they died and were removed)
            var allEnemies = GetTree().GetNodesInGroup(Constants.GROUP_VINE_ENEMY);
            foreach (var e in allEnemies)
            {
                if (e is VineEnemy enemy && enemy.IsWandering)
                {
                    enemy.IsWandering = false;
                    enemy.TryRepath();
                }
            }

            // 3. Restore player stats
            if (ServiceLocator.TryGet<VinePlayer>(out var player))
            {
                player.MoveSpeed = _savedPlayerMoveSpeed;
                player.AttackSpeed = _savedPlayerAttackSpeed;
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

        // ── Wandering AI ──

        private void UpdateWanderingEnemies(float dt)
        {
            // Handle new spawns: enemies in group but not in _wanderStates
            var allEnemies = GetTree().GetNodesInGroup(Constants.GROUP_VINE_ENEMY);
            foreach (var e in allEnemies)
            {
                if (e is VineEnemy ve && ve.IsAlive && !_wanderStates.ContainsKey(ve))
                    ApplyChaosToEnemy(ve);
            }

            // Move each wandering enemy toward its target
            var dead = new List<VineEnemy>();
            foreach (var (ve, state) in _wanderStates)
            {
                if (!IsInstanceValid(ve) || !ve.IsAlive)
                {
                    dead.Add(ve);
                    continue;
                }

                // Move toward wander target
                var dir = state.Target - ve.GlobalPosition;
                dir.Y = 0;
                float dist = dir.Length();
                float speed = ve.BaseSpeed * ve.SpeedMultiplier;

                if (dist > 0.3f)
                {
                    ve.GlobalPosition += dir.Normalized() * speed * dt;

                    // Smooth facing rotation
                    float targetYaw = Mathf.Atan2(dir.X, dir.Z);
                    var modelRoot = ve.GetChildOrNull<Node3D>(0);
                    if (modelRoot != null)
                    {
                        float currentYaw = modelRoot.Rotation.Y;
                        float newYaw = Mathf.LerpAngle(currentYaw, targetYaw, dt * 8f);
                        modelRoot.Rotation = new Vector3(0, newYaw, 0);
                    }
                }

                // Retarget timer
                state.RetargetTimer -= dt;
                if (state.RetargetTimer <= 0 || dist < 0.5f)
                {
                    state.Target = PickRandomWalkable(ve.GlobalPosition);
                    state.RetargetTimer = _rng.RandfRange(2f, 3f);
                }
            }

            // Clean up dead enemies
            foreach (var d in dead)
                _wanderStates.Remove(d);
        }

        private Vector3 PickRandomWalkable(Vector3 nearPos)
        {
            if (!ServiceLocator.TryGet<VineGrid>(out var grid))
                return nearPos;

            float cs = Constants.VINE_CELL_SIZE;
            // Try random cells near current position
            for (int attempt = 0; attempt < 10; attempt++)
            {
                int dx = _rng.RandiRange(-5, 5);
                int dz = _rng.RandiRange(-5, 5);
                var baseCell = grid.WorldToGrid(nearPos);
                var testCell = new Vector2I(baseCell.X + dx, baseCell.Y + dz);

                if (testCell.X >= 0 && testCell.X < grid.Width &&
                    testCell.Y >= 0 && testCell.Y < grid.Height &&
                    grid.IsWalkable(testCell))
                {
                    return grid.GridToWorld(testCell) + new Vector3(0, 0.3f, 0);
                }
            }
            // Fallback: grid center
            return new Vector3(grid.Width * cs / 2f, 0.3f, grid.Height * cs / 2f);
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
