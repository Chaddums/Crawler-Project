using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Runtime vine node. Handles signal processing, visual state, and type-specific behavior.
    /// All 18 node types are implemented here via type dispatch — keeps the node system
    /// in one place rather than 18 tiny files.
    /// </summary>
    public partial class VineNode : Node3D
    {
        public VineNodeData Data { get; private set; }
        public Vector2I GridPosition { get; set; }

        // Connection tracking
        private readonly List<Vector2I> _connectedCells = new();
        public IReadOnlyList<Vector2I> ConnectedCells => _connectedCells;

        // State
        public bool IsActive { get; private set; }    // General active/signaled state
        public bool IsOpen { get; private set; } = true;  // For gates/latches: whether enemies can pass
        private float _stateTimer;                     // Multi-purpose timer
        private int _activeInputCount;                 // For gates: how many inputs are currently active
        private float _inputWindowTimer;               // For gates: AND-gate timing window
        private int _switchOutputIndex;                // For switches: which output is active (0 or 1)
        private float _buffStrength;                   // For towers receiving buffs
        private float _buffDecayTimer;

        // Sensor state
        private float _sensorCooldown;
        private float _debugLogTimer;
        private const float SENSOR_COOLDOWN = 0.15f;

        // Visual
        private MeshInstance3D _mesh;
        private MeshInstance3D _stateIndicator;        // Shows open/closed, active/inactive
        private Label3D _idleLabel;                     // "NO SIGNAL" for effect nodes
        private Color _baseColor;
        private float _idlePulseTimer;

        /// <summary>
        /// Whether enemies can walk through this node's cell.
        /// ALL nodes block the path — the vine network IS the maze.
        /// Gates/Latches are the exception: walkable when open, blocked when closed.
        /// </summary>
        public bool IsWalkable
        {
            get
            {
                if (Data == null) return true;
                if (Data.Type == VineNodeType.Gate) return IsOpen;
                if (Data.Type == VineNodeType.Latch) return IsOpen;
                return false;
            }
        }

        public void Initialize(VineNodeData data)
        {
            Data = data;
            _baseColor = data.TintColor;
            IsOpen = data.Type != VineNodeType.Gate; // Gates start closed

            AddToGroup(Constants.GROUP_VINE_NODE);
            BuildVisual();

            // Effect nodes show "NO SIGNAL" when idle — the core concept feedback
            if (data.Category == VineNodeCategory.Effect && data.Type != VineNodeType.SlowField)
                BuildIdleLabel();
        }

        public bool HasFreeSlot() => _connectedCells.Count < (Data?.MaxConnections ?? 0);

        public void OnConnected(Vector2I neighbor)
        {
            if (!_connectedCells.Contains(neighbor))
                _connectedCells.Add(neighbor);
        }

        public void OnDisconnected(Vector2I neighbor)
        {
            _connectedCells.Remove(neighbor);
        }

        // ── Signal processing ──

        /// <summary>
        /// Called when a signal arrives at this node from a connected vine.
        /// </summary>
        public void ReceiveSignal(SignalType type, float strength, Vector2I fromCell)
        {
            GameEvents.OnSignalReceived?.Invoke(this, type);

            switch (Data.Type)
            {
                case VineNodeType.Extender:
                    // Pass through to all connections except source
                    PropagateSignal(type, strength, fromCell);
                    break;

                case VineNodeType.Junction:
                    // Split to all outputs
                    PropagateSignal(type, strength, fromCell);
                    break;

                case VineNodeType.Switch:
                    // Only propagate to the active output
                    PropagateToSwitchOutput(type, strength, fromCell);
                    break;

                case VineNodeType.Gate:
                    HandleGateInput(type, strength, fromCell);
                    break;

                case VineNodeType.Inverter:
                    // Flip: if trigger received, send reset. If reset, send trigger.
                    var flipped = type == SignalType.Trigger ? SignalType.Reset : SignalType.Trigger;
                    PropagateSignal(flipped, strength, fromCell);
                    break;

                case VineNodeType.Delay:
                    // Queue signal for later delivery
                    _delayedSignals.Add(new DelayedSignal {
                        Type = type, Strength = strength,
                        FromCell = fromCell, TimeRemaining = Data.Interval
                    });
                    break;

                case VineNodeType.Latch:
                    HandleLatchInput(type, strength, fromCell);
                    break;

                case VineNodeType.DamageTower:
                    ActivateEffect(strength);
                    break;

                case VineNodeType.SlowField:
                    ActivateEffect(strength);
                    break;

                case VineNodeType.PushPull:
                    ActivateEffect(strength);
                    break;

                case VineNodeType.LoopAnchor:
                    ToggleLoopAnchor(type);
                    break;

                case VineNodeType.BuffEmitter:
                    // Send buff signal through vine
                    PropagateSignal(SignalType.Buff, strength, fromCell);
                    break;

                case VineNodeType.SignalCannon:
                    // Receives don't do anything — it's manually triggered
                    break;

                // Sensors don't process incoming signals (they generate them)
                default:
                    break;
            }
        }

        /// <summary>
        /// Fire a signal from this node to all connected vines.
        /// Called by sensors, timers, and manual triggers.
        /// </summary>
        public void FireSignal(SignalType type, float strength = 1f)
        {
            IsActive = true;
            _stateTimer = Constants.SIGNAL_PULSE_DURATION;
            FlashActive();

            GameEvents.OnSignalFired?.Invoke(this, type);

            if (!ServiceLocator.TryGet<VineGrid>(out var grid)) return;

            foreach (var neighbor in _connectedCells)
            {
                var conn = grid.GetConnection(GridPosition, neighbor);
                conn?.InjectSignal(GridPosition, type, strength);
            }
        }

        // ── Per-frame update ──

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;

            // Active flash decay
            if (_stateTimer > 0)
            {
                _stateTimer -= dt;
                if (_stateTimer <= 0)
                    IsActive = false;
            }

            // Buff decay
            if (_buffStrength > 0)
            {
                _buffDecayTimer -= dt;
                if (_buffDecayTimer <= 0)
                    _buffStrength = 0;
            }

            // Gate input window
            if (Data?.Type == VineNodeType.Gate && _inputWindowTimer > 0)
            {
                _inputWindowTimer -= dt;
                if (_inputWindowTimer <= 0)
                {
                    _activeInputCount = 0;
                }
            }

            // Process delayed signals
            ProcessDelays(dt);

            // Sensor cooldown
            if (_sensorCooldown > 0)
                _sensorCooldown -= dt;

            // Type-specific per-frame behavior
            switch (Data?.Type)
            {
                case VineNodeType.Timer:
                    UpdateTimer(dt);
                    break;
                case VineNodeType.Switch:
                    UpdateSwitch(dt);
                    break;
                case VineNodeType.ProximitySensor:
                case VineNodeType.TypeSensor:
                case VineNodeType.HPSensor:
                case VineNodeType.CountSensor:
                    UpdateSensor(dt);
                    break;
                case VineNodeType.DamageTower:
                    UpdateDamageTower(dt);
                    break;
                case VineNodeType.SlowField:
                    UpdateSlowField(dt);
                    break;
            }

            UpdateVisualState();
        }

        // ── Type-specific behaviors ──

        // Timer
        private float _timerAccumulator;

        private void UpdateTimer(float dt)
        {
            float interval = Data.Interval > 0 ? Data.Interval : SignalTuningEditor.TimerInterval;
            _timerAccumulator += dt;
            if (_timerAccumulator >= interval)
            {
                _timerAccumulator -= interval;
                FireSignal(SignalType.Trigger);
            }
        }

        // Switch
        private float _switchTimer;

        private void UpdateSwitch(float dt)
        {
            float interval = Data.Interval > 0 ? Data.Interval : SignalTuningEditor.SwitchToggleTime;
            _switchTimer += dt;
            if (_switchTimer >= interval)
            {
                _switchTimer -= interval;
                _switchOutputIndex = (_switchOutputIndex + 1) % Mathf.Min(_connectedCells.Count, 2);
                GameEvents.OnSwitchToggled?.Invoke(this, _switchOutputIndex);
                GameEvents.OnVinePathRecalculated?.Invoke();
            }
        }

        private void PropagateToSwitchOutput(SignalType type, float strength, Vector2I fromCell)
        {
            if (_connectedCells.Count == 0) return;
            if (!ServiceLocator.TryGet<VineGrid>(out var grid)) return;

            // Find outputs (connected cells that aren't the input)
            var outputs = new List<Vector2I>();
            foreach (var c in _connectedCells)
                if (c != fromCell) outputs.Add(c);

            if (outputs.Count == 0) return;
            int idx = _switchOutputIndex % outputs.Count;
            var target = outputs[idx];
            var conn = grid.GetConnection(GridPosition, target);
            conn?.InjectSignal(GridPosition, type, strength);
        }

        // Gate (AND)
        private void HandleGateInput(SignalType type, float strength, Vector2I fromCell)
        {
            if (type == SignalType.Reset)
            {
                SetGateState(false);
                PropagateSignal(type, strength, fromCell);
                return;
            }

            _activeInputCount++;
            _inputWindowTimer = SignalTuningEditor.GateInputWindow;

            if (_activeInputCount >= Data.RequiredInputs)
            {
                SetGateState(true);
                _activeInputCount = 0;
                PropagateSignal(type, strength, fromCell);
            }
        }

        private void SetGateState(bool open)
        {
            if (IsOpen == open) return;
            IsOpen = open;
            GameEvents.OnGateStateChanged?.Invoke(this, open);
            GameEvents.OnVinePathRecalculated?.Invoke();
        }

        // Latch
        private void HandleLatchInput(SignalType type, float strength, Vector2I fromCell)
        {
            if (type == SignalType.Reset)
            {
                IsOpen = false;
                GameEvents.OnGateStateChanged?.Invoke(this, false);
                GameEvents.OnVinePathRecalculated?.Invoke();
            }
            else
            {
                IsOpen = true;
                GameEvents.OnGateStateChanged?.Invoke(this, true);
                GameEvents.OnVinePathRecalculated?.Invoke();
                PropagateSignal(type, strength, fromCell);
            }
        }

        // Delay queue
        private readonly List<DelayedSignal> _delayedSignals = new();

        private void ProcessDelays(float dt)
        {
            for (int i = _delayedSignals.Count - 1; i >= 0; i--)
            {
                var ds = _delayedSignals[i];
                ds.TimeRemaining -= dt;
                if (ds.TimeRemaining <= 0)
                {
                    PropagateSignal(ds.Type, ds.Strength, ds.FromCell);
                    _delayedSignals.RemoveAt(i);
                }
                else
                {
                    _delayedSignals[i] = ds;
                }
            }
        }

        // Loop Anchor
        private void ToggleLoopAnchor(SignalType type)
        {
            IsActive = type == SignalType.Trigger;
            GameEvents.OnVinePathRecalculated?.Invoke();
        }

        // Effect activation
        private float _effectTimer;
        private float _effectStrength;
        private const float EFFECT_DURATION = 4f;

        private void ActivateEffect(float strength)
        {
            _effectTimer = EFFECT_DURATION;
            _effectStrength = strength;
            IsActive = true;

            // VFX: signal activation burst
            Color burstColor = Data.Category == VineNodeCategory.Effect
                ? new Color(0.9f, 0.5f, 0.2f)
                : _baseColor;
            VfxFactory.SpawnSignalBurst(GetTree(), GlobalPosition, burstColor);
        }

        // Receive buff (for damage towers)
        public void ReceiveBuff(float strength)
        {
            _buffStrength = Mathf.Max(_buffStrength, strength);
            _buffDecayTimer = 3f;
        }

        // Sensors
        private void UpdateSensor(float dt)
        {
            if (_sensorCooldown > 0) return;

            var enemies = GetTree().GetNodesInGroup(Constants.GROUP_VINE_ENEMY);
            bool triggered = false;
            int count = 0;

            foreach (var enemy in enemies)
            {
                if (enemy is not VineEnemy ve || !ve.IsAlive) continue;
                float dist = GlobalPosition.DistanceTo(ve.GlobalPosition);
                if (dist > Data.Range) continue;

                count++;

                switch (Data.Type)
                {
                    case VineNodeType.ProximitySensor:
                        triggered = true;
                        break;
                    case VineNodeType.TypeSensor:
                        // For now, triggers on any type. Config would narrow this.
                        triggered = true;
                        break;
                    case VineNodeType.HPSensor:
                        if (ve.HealthPercent < 0.5f) triggered = true;
                        break;
                    case VineNodeType.CountSensor:
                        if (count >= Data.RequiredInputs) triggered = true;
                        break;
                }

                if (triggered && Data.Type != VineNodeType.CountSensor) break;
            }

            if (triggered)
            {
                FireSignal(SignalType.Trigger);
                _sensorCooldown = SENSOR_COOLDOWN;
            }
        }

        // Damage tower
        private float _fireTimer;
        private const float FIRE_INTERVAL = 0.4f; // Fires discrete shots, not continuous DPS

        private void UpdateDamageTower(float dt)
        {
            if (_effectTimer <= 0) return;
            _effectTimer -= dt;

            _fireTimer -= dt;
            if (_fireTimer > 0)
            {
                if (_effectTimer <= 0) IsActive = false;
                return;
            }

            var enemies = GetTree().GetNodesInGroup(Constants.GROUP_VINE_ENEMY);
            VineEnemy closest = null;
            float closestDist = Data.Range;

            foreach (var enemy in enemies)
            {
                if (enemy is not VineEnemy ve || !ve.IsAlive) continue;
                float dist = GlobalPosition.DistanceTo(ve.GlobalPosition);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = ve;
                }
            }

            if (closest != null)
            {
                float dmg = Data.Damage * FIRE_INTERVAL * (1f + _buffStrength * SignalTuningEditor.BuffDamageBonus);
                closest.TakeDamage(dmg);

                // VFX: muzzle flash at tower, projectile to target
                var muzzlePos = GlobalPosition + new Vector3(0, 0.3f, 0);
                VfxFactory.SpawnMuzzleFlash(GetTree(), muzzlePos, DamageType.Physical);
                VfxFactory.SpawnProjectile(GetTree(), muzzlePos, closest.GlobalPosition,
                    new Color(1f, 0.7f, 0.2f));

                _fireTimer = FIRE_INTERVAL;
            }

            if (_effectTimer <= 0)
                IsActive = false;
        }

        // Slow field
        private float _slowPulseTimer;

        private void UpdateSlowField(float dt)
        {
            if (_effectTimer <= 0) return;
            _effectTimer -= dt;

            var enemies = GetTree().GetNodesInGroup(Constants.GROUP_VINE_ENEMY);
            foreach (var enemy in enemies)
            {
                if (enemy is not VineEnemy ve || !ve.IsAlive) continue;
                float dist = GlobalPosition.DistanceTo(ve.GlobalPosition);
                if (dist <= Data.Range)
                    ve.ApplySlow(Data.SlowAmount, 0.5f);
            }

            // Visual pulse every 0.8s while active
            _slowPulseTimer -= dt;
            if (_slowPulseTimer <= 0)
            {
                _slowPulseTimer = 0.8f;
                VfxFactory.SpawnAreaPulse(GetTree(), GlobalPosition, Data.Range,
                    new Color(0.3f, 0.3f, 0.7f));
            }

            if (_effectTimer <= 0)
                IsActive = false;
        }

        // ── Signal propagation helper ──

        private void PropagateSignal(SignalType type, float strength, Vector2I excludeCell)
        {
            if (!ServiceLocator.TryGet<VineGrid>(out var grid)) return;

            // Buff signals lose strength per hop
            float outStrength = type == SignalType.Buff
                ? strength * (1f - SignalTuningEditor.SignalBuffDecay)
                : strength;

            if (outStrength <= 0.01f) return;

            // If this is a buff signal arriving at a damage tower, apply the buff
            if (type == SignalType.Buff && Data.Type == VineNodeType.DamageTower)
            {
                ReceiveBuff(outStrength);
            }

            foreach (var neighbor in _connectedCells)
            {
                if (neighbor == excludeCell) continue;
                var conn = grid.GetConnection(GridPosition, neighbor);
                conn?.InjectSignal(GridPosition, type, outStrength);
            }
        }

        /// <summary>
        /// Manually trigger this node (for SignalCannon / player interaction).
        /// </summary>
        public void ManualTrigger()
        {
            if (Data?.Type == VineNodeType.SignalCannon)
                FireSignal(SignalType.Trigger);
        }

        // ── Visuals ──

        private void BuildVisual()
        {
            _mesh = new MeshInstance3D();
            var box = new BoxMesh();

            float size = Data.Category switch {
                VineNodeCategory.Sensor => 0.6f,
                VineNodeCategory.Effect => 0.8f,
                _ => 0.5f
            };
            box.Size = new Vector3(size, size, size);
            _mesh.Mesh = box;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = _baseColor;
            mat.Roughness = 0.7f;
            mat.Metallic = 0.4f;
            // Tron blue program glow
            mat.EmissionEnabled = true;
            mat.Emission = _baseColor;
            mat.EmissionEnergyMultiplier = 0.6f;
            _mesh.MaterialOverride = mat;
            AddChild(_mesh);

            // State indicator — small sphere on top
            _stateIndicator = new MeshInstance3D();
            var sphere = new SphereMesh();
            sphere.Radius = 0.12f;
            sphere.Height = 0.24f;
            _stateIndicator.Mesh = sphere;
            _stateIndicator.Position = new Vector3(0, size / 2f + 0.15f, 0);

            var indicatorMat = new StandardMaterial3D();
            indicatorMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            indicatorMat.AlbedoColor = new Color(0.3f, 0.3f, 0.3f);
            _stateIndicator.MaterialOverride = indicatorMat;
            AddChild(_stateIndicator);

            // Range indicator ring for sensors and effect nodes
            if (Data.Range > 0)
            {
                var rangeRing = new MeshInstance3D();
                var torus = new TorusMesh();
                torus.InnerRadius = Data.Range - 0.08f;
                torus.OuterRadius = Data.Range;
                torus.Rings = 24;
                torus.RingSegments = 32;
                rangeRing.Mesh = torus;
                rangeRing.Position = new Vector3(0, -0.45f, 0); // Ground level

                var rangeMat = new StandardMaterial3D();
                Color rangeColor = Data.Category switch {
                    VineNodeCategory.Sensor => new Color(0.1f, 0.5f, 0.7f, 0.15f),
                    VineNodeCategory.Effect => new Color(0.15f, 0.35f, 0.8f, 0.15f),
                    _ => new Color(0.3f, 0.5f, 0.6f, 0.1f)
                };
                rangeMat.AlbedoColor = rangeColor;
                rangeMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                rangeMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                rangeRing.MaterialOverride = rangeMat;
                AddChild(rangeRing);
            }

            // Role label — floating text above the node showing category + name
            var roleLabel = new Label3D();
            string roleTag = Data.Category switch {
                VineNodeCategory.Sensor => "SENSOR",
                VineNodeCategory.Effect => "EFFECT",
                _ => "ROUTE"
            };
            roleLabel.Text = $"{roleTag}: {Data.Name}";
            roleLabel.FontSize = 48;
            roleLabel.OutlineSize = 8;
            roleLabel.Modulate = Data.Category switch {
                VineNodeCategory.Sensor => new Color(0.2f, 0.9f, 0.4f),
                VineNodeCategory.Effect => new Color(0.9f, 0.5f, 0.2f),
                _ => new Color(0.5f, 0.7f, 1.0f)
            };
            roleLabel.Position = new Vector3(0, size / 2f + 0.6f, 0);
            roleLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            AddChild(roleLabel);
        }

        private void FlashActive()
        {
            if (_mesh?.MaterialOverride is StandardMaterial3D mat)
            {
                mat.EmissionEnabled = true;
                mat.Emission = _baseColor.Lightened(0.4f);
                mat.EmissionEnergyMultiplier = 2f;
            }
        }

        private void BuildIdleLabel()
        {
            _idleLabel = new Label3D();
            _idleLabel.Text = "NO SIGNAL";
            _idleLabel.FontSize = 48;
            _idleLabel.OutlineSize = 8;
            _idleLabel.Modulate = new Color(0.9f, 0.3f, 0.2f, 0.8f);
            _idleLabel.Position = new Vector3(0, 1.2f, 0);
            _idleLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            AddChild(_idleLabel);
        }

        private void UpdateVisualState()
        {
            // Main mesh — flash decay
            if (_mesh?.MaterialOverride is StandardMaterial3D meshMat)
            {
                if (!IsActive && meshMat.EmissionEnabled)
                {
                    meshMat.EmissionEnabled = false;
                }
            }

            // State indicator color
            if (_stateIndicator?.MaterialOverride is StandardMaterial3D indMat)
            {
                if (Data?.HasDynamicRouting == true)
                {
                    // Gate/switch/latch — show open/closed state
                    indMat.AlbedoColor = IsOpen ? new Color(0.1f, 0.9f, 0.1f) : new Color(0.9f, 0.1f, 0.1f);
                    indMat.EmissionEnabled = true;
                    indMat.Emission = indMat.AlbedoColor;
                    indMat.EmissionEnergyMultiplier = 1.5f;
                }
                else if (IsActive)
                {
                    indMat.AlbedoColor = new Color(0.9f, 0.8f, 0.2f);
                    indMat.EmissionEnabled = true;
                    indMat.Emission = indMat.AlbedoColor;
                }
                else
                {
                    indMat.AlbedoColor = new Color(0.3f, 0.3f, 0.3f);
                    indMat.EmissionEnabled = false;
                }
            }

            // "NO SIGNAL" label — visible when idle, pulse opacity
            if (_idleLabel != null)
            {
                bool showIdle = !IsActive && _effectTimer <= 0 && _connectedCells.Count == 0;
                bool showDisconnected = _connectedCells.Count == 0;

                _idleLabel.Visible = showIdle || showDisconnected;
                _idleLabel.Text = showDisconnected ? "NO SIGNAL" : "";

                if (_idleLabel.Visible)
                {
                    _idlePulseTimer += 3f * (float)GetProcessDeltaTime();
                    float alpha = 0.4f + 0.4f * Mathf.Sin(_idlePulseTimer);
                    _idleLabel.Modulate = new Color(0.9f, 0.3f, 0.2f, alpha);
                }
            }
        }
    }

    public struct DelayedSignal
    {
        public SignalType Type;
        public float Strength;
        public Vector2I FromCell;
        public float TimeRemaining;
    }
}
