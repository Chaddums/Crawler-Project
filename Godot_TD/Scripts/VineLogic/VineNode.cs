using System.Linq;
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
        public bool IsJammed { get; set; }            // Corruption: sensors disabled when true
        private float _stateTimer;                     // Multi-purpose timer
        private int _activeInputCount;                 // For gates: how many inputs are currently active
        private float _inputWindowTimer;               // For gates: AND-gate timing window
        private int _switchOutputIndex;                // For switches: which output is active (0 or 1)
        private float _buffStrength;                   // For towers receiving buffs
        private float _buffDecayTimer;

        // Buff/debuff multiplier — modified by BuffDebuffComponent
        public float AttackRateMultiplier { get; set; } = 1f;

        // Sensor state
        private float _sensorCooldown;
        private float _debugLogTimer;
        private const float SENSOR_COOLDOWN = 0.15f;

        // Health (effect nodes only — towers can be destroyed by enemies)
        public float NodeMaxHealth { get; internal set; }
        public float NodeCurrentHealth { get; internal set; }

        /// <summary>
        /// Scale max HP and keep current HP at the same fraction (slot components).
        /// Changing only the max meant +HP plating added nothing and -HP costs could
        /// leave current HP above max.
        /// </summary>
        internal void ScaleMaxHealth(float factor)
        {
            if (!_hasHealth || NodeMaxHealth <= 0 || factor <= 0) return;
            float pct = NodeCurrentHealth / NodeMaxHealth;
            NodeMaxHealth *= factor;
            NodeCurrentHealth = Mathf.Clamp(NodeMaxHealth * pct, 0f, NodeMaxHealth);
        }
        public bool IsDestroyed => _hasHealth && NodeCurrentHealth <= 0;
        private bool _hasHealth;
        private MeshInstance3D _nodeHealthBar;
        private float _damageFlashTimer;

        // Visual
        private MeshInstance3D _mesh;
        private Node3D _modelRoot;                     // 3D model (null if procedural fallback)
        private TowerLook _look;                       // built from Data/Towers/{id}.json (aims, recoils)
        /// <summary>What the player sees of this node: the model, or the fallback cube. For tests.</summary>
        internal Node3D VisualRoot => _modelRoot ?? _mesh;
        private MeshInstance3D _footing;                // block under the base on sloped or elevated cells
        internal MeshInstance3D Footing => _footing;
        private float _visualTop = 0.4f;               // top of the visible model, node space

        /// <summary>True when the node shows a model (kit or built-in) rather than the fallback cube.</summary>
        internal bool HasModel => _modelRoot != null;
        internal TowerLook Look => _look;
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
                // A barricade on a canyon floor never changes the route: walkers stop at it instead
                if (OnCanyonFloor) return true;
                return false;
            }
        }

        /// <summary>A Barricade standing on a canyon map's floor (VineEnemy.HoldAtBarricade).</summary>
        public bool OnCanyonFloor { get; internal set; }

        /// <summary>Barricades on the floor now: every walker checks them, so they're kept apart from the rest.</summary>
        public static readonly List<VineNode> FloorBarricades = new();

        // The shared list of nodes (Roster) is rebuilt at most once a step: placing, building into
        // the scene or removing a node makes it look again
        public override void _EnterTree()
        {
            Roster.InvalidateNodes();
            if (OnCanyonFloor && !FloorBarricades.Contains(this)) FloorBarricades.Add(this);
        }

        public override void _ExitTree()
        {
            Roster.InvalidateNodes();
            FloorBarricades.Remove(this);
            if (IsWrecked) _wrecksStanding = Mathf.Max(0, _wrecksStanding - 1);
        }

        public void Initialize(VineNodeData data)
        {
            Data = data;
            _baseColor = data.TintColor;
            IsOpen = data.Type != VineNodeType.Gate; // Gates start closed

            AddToGroup(Constants.GROUP_VINE_NODE);
            Roster.Invalidate();

            // Effect nodes get health — they can be destroyed by enemy fire
            if (data.Category == VineNodeCategory.Effect)
            {
                _hasHealth = true;
                // Barrier walls get their own HP pool
                NodeMaxHealth = data.Type switch
                {
                    // Masonry (perk tree); Environment power type
                    VineNodeType.BarrierWall => Constants.BARRIER_WALL_HP * MetaRun.WallHpMult * PowerType.WallHpMult,
                    VineNodeType.SalvageDrill => Constants.SALVAGE_DRILL_HP * MetaRun.TowerHpMult,
                    // Hardened Plating; Fortress or Swarm Doctrine
                    _ => Constants.VINE_NODE_BASE_HEALTH * MetaRun.TowerHpMult * MetaRun.TowerHpFactor,
                };

                NodeCurrentHealth = NodeMaxHealth;
            }

            // S5: Auto-fire towers work by default without signal chains
            _autoFireEnabled = data.AutoFires;

            // S5: Initialize slot system for towers with component slots
            if (data.SlotCount > 0 && data.SlotTypes != null)
            {
                _slotSystem = new TowerSlotSystem(this, data.SlotTypes);
            }

            BuildVisual();

            // Effect nodes get a status label: "NO SIGNAL" for signal-only nodes with nothing feeding
            // them, "BOOSTED" while a signal amplifies an auto-firing tower. Walls never need a signal.
            if (data.Category == VineNodeCategory.Effect
                && data.Type is not (VineNodeType.SlowField or VineNodeType.BarrierWall or VineNodeType.SalvageDrill))
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
            if (IsWrecked) return;
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
                    // Decrement power — propagate only if juice remains
                    if (strength > 1f) PropagateSignal(type, strength - 1f, fromCell);
                    break;

                case VineNodeType.SlowField:
                    ActivateEffect(strength);
                    if (strength > 1f) PropagateSignal(type, strength - 1f, fromCell);
                    break;

                case VineNodeType.PushPull:
                    ActivateEffect(strength);
                    if (strength > 1f) PropagateSignal(type, strength - 1f, fromCell);
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

        private static readonly Dictionary<VineNodeType, string> _profileKeys = new();
        private static string ProfileKey(VineNodeType? t)
        {
            if (t == null) return "tower.none";
            if (!_profileKeys.TryGetValue(t.Value, out var k)) _profileKeys[t.Value] = k = "tower." + t.Value;
            return k;
        }

        // ── Targeting ──

        /// <summary>
        /// With nothing in reach a tower looks again this much later, not every physics step: a
        /// 300-tower field scanning 150 enemies each step was most of a frame (2026-10).
        /// </summary>
        private const float IdleRescan = 0.1f;

        // The tower's spot: towers don't move, and GlobalPosition is an engine call each time
        private Vector3 _at;
        private bool _atKnown;
        private Vector3 At
        {
            get
            {
                if (!_atKnown && IsInsideTree()) { _at = GlobalPosition; _atKnown = true; }
                return _atKnown ? _at : GlobalPosition;
            }
        }

        // How far over the tower's origin it sees from: its muzzle at rest, found once
        private float _eyeUp = -1f;

        /// <summary>
        /// Where this tower sees from and is seen at (<see cref="Sight"/>): its muzzle's height over
        /// the tower. Enemies aim their shots here too.
        /// </summary>
        public Vector3 EyePoint
        {
            get
            {
                if (_eyeUp < 0f && _look != null && _look.IsInsideTree() && _atKnown)
                {
                    _eyeUp = Mathf.Clamp(_look.MuzzleGlobal.Y - At.Y, 0.35f, 2.4f);
                    if (Data != null) Sight.RememberEye(Data.Type, _eyeUp);
                }
                return At + Vector3.Up * (_eyeUp < 0f ? 0.8f : _eyeUp);
            }
        }

        /// <summary>
        /// Lobbed fire and fields reach what the tower can't see: the Mortar Pit and Slag Chute
        /// arc their shells over the lip, the Jammer, Gravity Well, tar and rams work on an area.
        /// </summary>
        public bool IgnoresSight => Data != null && TypeIgnoresSight(Data.Type);

        /// <summary>The towers that work without seeing their targets (lobbed fire and fields).</summary>
        public static bool TypeIgnoresSight(VineNodeType t) => t is VineNodeType.MortarPit or VineNodeType.SlagChute or VineNodeType.SignalJammer
            or VineNodeType.GravityWell or VineNodeType.SlowField or VineNodeType.PushPull;

        /// <summary>This tower can see <paramref name="ve"/> (always, off canyon maps).</summary>
        public bool Sees(VineEnemy ve) => IgnoresSight || !Sight.Matters || Sees(ve, ve.GlobalPosition, EyePoint);

        /// <summary>
        /// The nearest enemy this tower can shoot within <paramref name="range"/>, from the step's
        /// cached positions. Measured across the ground, like the range ring: from a canyon wall
        /// four units up, a straight-line reach fell short of the ring by a cell for short guns.
        /// On a canyon map it has to see it, too: an enemy behind a wall or under the lip in front
        /// of a tower set back from the edge is out of its sight (<see cref="Sight"/>).
        /// </summary>
        private VineEnemy NearestInRange(float range, out float dist)
        {
            var tree = GetTree();
            var list = Roster.Enemies(tree);
            var pos = Roster.EnemyPositions(tree);
            var at = At;
            bool sight = !IgnoresSight && Sight.Matters;
            var eye = sight ? EyePoint : Vector3.Zero;
            float best2 = range * range;
            VineEnemy best = null;
            for (int i = 0; i < list.Count && i < pos.Count; i++)
            {
                var p = pos[i];
                float dx = p.X - at.X, dz = p.Z - at.Z;
                float d2 = dx * dx + dz * dz;
                if (d2 >= best2) continue;
                var ve = list[i];
                if (!CanTarget(ve)) continue;
                if (sight && !Sees(ve, p, eye)) continue;
                best2 = d2; best = ve;
            }
            dist = best != null ? Mathf.Sqrt(best2) : range;
            return best;
        }

        // ── Per-frame update ──

        public override void _PhysicsProcess(double delta)
        {
            long __pt = FrameProfiler.Start();
            try
            {
                if (_fortifyLeft > 0f) _fortifyLeft -= (float)delta;
                VineEnemy.HitSource = "your towers";
                VineEnemy.HitTower = this; // kills credit this tower (veterancy)
                try { PhysicsTick(delta); }
                finally { VineEnemy.HitSource = null; VineEnemy.HitTower = null; }
        
            }
            finally { FrameProfiler.Stop("towers", __pt); }
        }

        private void PhysicsTick(double delta)
        {
            float dt = (float)delta;
            if (IsWrecked) { TickWreck(dt); return; }

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

            // Crew links, recounted once a second; now and then a tag says what boosts this tower
            if ((_crewTimer -= dt) <= 0f)
            {
                _crewTimer = 1f;
                _atKnown = false; // the spot, refreshed now and then in case something moved it
                long __c = FrameProfiler.Start(); RecountCrew(); FrameProfiler.Stop("tower.crew", __c);
            }
            // The dome mends what stands inside it (not under Fortress Doctrine)
            if (InDome && _hasHealth && !IsDestroyed && NodeCurrentHealth < NodeMaxHealth && !MetaRun.NoTowerRepair)
                NodeCurrentHealth = Mathf.Min(NodeMaxHealth, NodeCurrentHealth + NodeMaxHealth * Constants.DOME_REPAIR_SHARE * dt);
            if ((_tagTimer -= dt) <= 0f)
            {
                _tagTimer = Constants.BUFF_TAG_INTERVAL * (0.8f + 0.4f * (float)GD.Randf());
                long __g = FrameProfiler.Start(); ShowBuffTag(); FrameProfiler.Stop("tower.tag", __g);
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

            // Damage flash decay
            if (_damageFlashTimer > 0)
            {
                _damageFlashTimer -= dt;
                if (_damageFlashTimer <= 0)
                {
                    if (_modelRoot != null)
                        FlashModel(false, _baseColor);
                    else if (_mesh?.MaterialOverride is StandardMaterial3D flashMat)
                        flashMat.EmissionEnergyMultiplier = 0.6f;
                }
            }

            // Update tower health bar
            { long __b = FrameProfiler.Start(); UpdateNodeHealthBar(); FrameProfiler.Stop("tower.bar", __b); }

            // Type-specific per-frame behavior
            long __t = FrameProfiler.Start();
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
                case VineNodeType.ScatterCannon:
                    UpdateScatterCannon(dt);
                    break;
                case VineNodeType.TeslaCoil:
                    UpdateTeslaCoil(dt);
                    break;
                case VineNodeType.FlakBattery:
                    UpdateFlakBattery(dt);
                    break;
                case VineNodeType.PushPull:
                    UpdatePushPull(dt);
                    break;
                case VineNodeType.BuffEmitter:
                    UpdateBuffEmitter(dt);
                    break;
                case VineNodeType.BarrierWall:
                    if (RelicRun.Towers == RelicRun.LivingWalls) UpdateThorns(dt);
                    break;
                default:
                    UpdateClassTower(dt);
                    break;
            }
            if (FrameProfiler.Enabled) FrameProfiler.Stop(ProfileKey(Data?.Type), __t);

            { long __v = FrameProfiler.Start(); UpdateVisualState(); FrameProfiler.Stop("tower.visual", __v); }
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
                float power = Data.SignalPower > 0 ? Data.SignalPower : 4f;
                FireSignal(SignalType.Trigger, power);
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

        /// <summary>
        /// Force-toggle gate/latch state (used by Gate Scramble corruption).
        /// Bypasses the normal signal requirement.
        /// </summary>
        public void ForceToggleGate()
        {
            if (Data?.Type != VineNodeType.Gate && Data?.Type != VineNodeType.Latch) return;
            SetGateState(!IsOpen);
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

        // S5: Auto-fire state — towers fire on their own, signals boost them
        private bool _autoFireEnabled;
        private bool _signalBoosted;   // Currently receiving signal boost

        private void ActivateEffect(float strength)
        {
            _effectTimer = EFFECT_DURATION;
            _effectStrength = strength;
            _signalBoosted = true;  // S5: mark as signal-boosted for damage multiplier
            IsActive = true;

            // VFX: signal activation burst
            Color burstColor = Data.Category == VineNodeCategory.Effect
                ? new Color(0.9f, 0.5f, 0.2f)
                : _baseColor;
            VfxFactory.SpawnSignalBurst(GetTree(), GlobalPosition, burstColor);
        }

        // Receive buff (for damage towers)
        public void ReceiveBuff(float strength) => ReceiveBuff(strength, 3f);

        /// <summary>The boost on this tower now (relays, Overclock); 0 when none.</summary>
        public float BuffStrength => _buffStrength;

        /// <summary>A boost for <paramref name="seconds"/>; the strongest boost on a tower wins (boosts don't stack).</summary>
        public void ReceiveBuff(float strength, float seconds)
        {
            if (strength >= _buffStrength) _buffDecayTimer = Mathf.Max(seconds, strength > _buffStrength ? seconds : _buffDecayTimer);
            _buffStrength = Mathf.Max(_buffStrength, strength);
        }

        /// <summary>Restore up to <paramref name="amount"/> health (Repair Pulse).</summary>
        public void Repair(float amount)
        {
            if (!_hasHealth || IsDestroyed || amount <= 0f || MetaRun.NoTowerRepair) return; // Fortress Doctrine
            NodeCurrentHealth = Mathf.Min(NodeMaxHealth, NodeCurrentHealth + amount);
        }

        // Sensors
        private void UpdateSensor(float dt)
        {
            if (IsJammed) return;
            if (_sensorCooldown > 0) return;

            var enemies = Roster.Enemies(GetTree());
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
                // Fire with SignalPower as strength — each effect node decrements by 1
                float power = Data.SignalPower > 0 ? Data.SignalPower : 3f;

                FireSignal(SignalType.Trigger, power);
                _sensorCooldown = SENSOR_COOLDOWN;
            }
        }

        // Damage tower
        private float _fireTimer;
        private const float FIRE_INTERVAL = 0.4f; // Fires discrete shots, not continuous DPS

        /// <summary>Where this tower's shots leave: the look's barrel tip, or a point over the base.</summary>
        private Vector3 Muzzle(float up = 0.3f) => _look != null && _look.IsInsideTree() ? _look.MuzzleGlobal : GlobalPosition + Vector3.Up * up;

        private void UpdateDamageTower(float dt)
        {
            // S5: Auto-fire towers always run. Signal boost adds damage multiplier.
            bool canFire = _autoFireEnabled || _effectTimer > 0;
            if (!canFire) return;

            if (_effectTimer > 0)
            {
                _effectTimer -= dt;
                if (_effectTimer <= 0)
                    _signalBoosted = false;
            }

            _fireTimer -= dt;
            if (_fireTimer > 0) return;

            // Find target — slot system can override targeting later
            var enemies = Roster.Enemies(GetTree());
            var closest = NearestInRange(GetEffectiveRange(), out _);
            if (closest == null) _fireTimer = Mathf.Max(_fireTimer, IdleRescan);

            // Turn onto the target first: shots left the side of a barrel still swinging round
            if (closest != null && _look != null && !_look.ReadyToFire(closest)) return;

            if (closest != null)
            {
                // Damage per shot comes from the base interval, so firing faster adds damage
                // (from the boosted interval, a faster tower split the same damage over more shots)
                float dmg = GetEffectiveDamage(DamageTowerBaseInterval);
                closest.TakeDamage(dmg, HitKind);
                if (RelicRun.Ignites(Data.Type)) IgniteFrom(closest, dmg, GetEffectiveFireInterval(DamageTowerBaseInterval));

                // S5: Apply on-hit effects from slotted components
                ApplyOnHitEffects(closest);

                // VFX: muzzle flash at the barrel, projectile to target
                _look?.Track(closest);
                _look?.Fire();
                var muzzlePos = Muzzle();
                var shotColor = RelicRun.Ignites(Data.Type) ? VineEnemy.FireColor : _signalBoosted ? new Color(1f, 0.9f, 0.3f) : new Color(1f, 0.7f, 0.2f);
                VfxFactory.SpawnMuzzleFlash(GetTree(), muzzlePos, closest.BodyCentre - muzzlePos, shotColor);
                VfxFactory.SpawnProjectile(GetTree(), muzzlePos, closest.BodyCentre, shotColor, RelicRun.Ignites(Data.Type) ? 16f : 18f,
                    RelicRun.Ignites(Data.Type) ? 1.3f : 1f);
                if (Has("piercing_rail")) Pierce(closest, dmg, enemies);

                Rearm(ref _fireTimer, GetEffectiveFireInterval(DamageTowerBaseInterval));
                IsActive = true;
            }
            if (closest == null && !_signalBoosted)
            {
                IsActive = false;
            }
        }

        /// <summary>
        /// Piercing Rail: the round carries on past <paramref name="target"/> along the line from
        /// the tower and hits the next enemies close to that line.
        /// </summary>
        private void Pierce(VineEnemy target, float dmg, System.Collections.Generic.List<VineEnemy> enemies)
        {
            var dir = target.GlobalPosition - GlobalPosition;
            dir.Y = 0;
            if (dir.LengthSquared() < 0.0001f) return;
            dir = dir.Normalized();
            VineEnemy first = null, second = null;
            float firstAlong = float.MaxValue, secondAlong = float.MaxValue;
            foreach (var enemy in enemies)
            {
                if (enemy is not VineEnemy ve || ve == target || !CanTarget(ve) || !Sees(ve)) continue;
                var rel = ve.GlobalPosition - target.GlobalPosition;
                rel.Y = 0;
                float along = rel.Dot(dir);
                if (along <= 0f || along > Constants.PERK_PIERCE_LENGTH) continue;
                if ((rel - dir * along).Length() > Constants.PERK_PIERCE_WIDTH) continue;
                if (along < firstAlong) { second = first; secondAlong = firstAlong; first = ve; firstAlong = along; }
                else if (along < secondAlong) { second = ve; secondAlong = along; }
            }
            var from = target.GlobalPosition;
            foreach (var ve in new[] { first, second })
            {
                if (ve == null) continue;
                ve.TakeDamage(dmg * Constants.PERK_PIERCE_DAMAGE, HitKind);
                ApplyOnHitEffects(ve);
                VfxFactory.SpawnProjectile(GetTree(), from + Vector3.Up * 0.5f, ve.GlobalPosition, new Color(0.6f, 0.85f, 1f), 40f, 0.7f);
                from = ve.GlobalPosition;
            }
        }

        // Slow field
        private float _slowPulseTimer, _slowScanTimer, _slowScanDt;
        private bool _slowedLast;
        private VineEnemy _slowNearest;

        private void UpdateSlowField(float dt)
        {
            // S5: Auto-fire slow fields always run. Signal boost increases area.
            bool canFire = _autoFireEnabled || _effectTimer > 0;
            if (!canFire) return;

            if (_effectTimer > 0)
            {
                _effectTimer -= dt;
                if (_effectTimer <= 0)
                    _signalBoosted = false;
            }

            float range = GetEffectiveRange();
            // Live tuning / perks ("Viscous Tar") — SlowFieldAmount was never read
            float slowAmount = Mathf.Clamp(
                Data.SlowAmount + SignalTuningEditor.SlowFieldAmount - Constants.SLOW_FIELD_AMOUNT + UpgradeSlowBonus, 0f, 0.9f);
            bool napalm = HasBranch("napalm");
            if (_signalBoosted)
                slowAmount = Mathf.Min(0.9f, slowAmount * Constants.TOWER_SIGNAL_BOOST);
            // Obelisk: slows hit harder; so does the Environment power type
            slowAmount = Mathf.Min(0.9f, slowAmount * RoleRun.SlowMult * PowerType.SlowMult);

            // The tar is laid every IdleRescan, not every physics step (its slow lasts 0.5 s and
            // napalm burns for the time since the last pass)
            _slowScanTimer -= dt;
            _slowScanDt += dt;
            bool anySlowed = _slowedLast;
            VineEnemy nearest = IsInstanceValid(_slowNearest) && _slowNearest.IsAlive ? _slowNearest : null;
            if (_slowScanTimer <= 0f)
            {
                _slowScanTimer = IdleRescan;
                float burnFor = _slowScanDt;
                _slowScanDt = 0f;
                var enemies = Roster.Enemies(GetTree());
                var tarPos = Roster.EnemyPositions(GetTree());
                var tarAt = At;
                anySlowed = false;
                nearest = null;
                float nearestDist = float.MaxValue;
                for (int i = 0; i < enemies.Count && i < tarPos.Count; i++)
                {
                    float tdx = tarAt.X - tarPos[i].X, tdz = tarAt.Z - tarPos[i].Z;
                    float dist = Mathf.Sqrt(tdx * tdx + tdz * tdz); // across the ground, like the ring
                    if (dist > range || enemies[i] is not VineEnemy ve || !CanTarget(ve)) continue; // tar can't reach flyers
                    ve.ApplySlow(slowAmount, 0.5f);
                    // Napalm: the tar burns
                    if (napalm) ve.TakeDamage(Constants.NAPALM_DPS * burnFor * UpgradeDamageMult, DamageKind.Normal);
                    anySlowed = true;
                    // The tar covers its reach; the gob it sprays goes to the nearest it can see
                    if (dist < nearestDist && (!Sight.Matters || Sight.Clear(EyePoint, ve.BodyCentre))) { nearestDist = dist; nearest = ve; }
                }
                _slowedLast = anySlowed;
                _slowNearest = nearest;
            }

            IsActive = anySlowed || _signalBoosted;

            // Visual pulse every 0.8s while active
            _slowPulseTimer -= dt;
            if (nearest != null) _look?.Track(nearest);
            // The gob waits for the nozzle to come round (the slow itself doesn't)
            if (_slowPulseTimer <= 0 && anySlowed && nearest != null && _look != null && !_look.ReadyToFire(nearest))
                _slowPulseTimer = 0.05f;
            if (_slowPulseTimer <= 0 && anySlowed)
            {
                _slowPulseTimer = 0.8f;
                Color pulseColor = _signalBoosted
                    ? new Color(0.4f, 0.4f, 0.9f)  // Brighter when boosted
                    : new Color(0.3f, 0.3f, 0.7f);
                // Faint: the gob and splat show the spraying, the ring only marks the reach
                VfxFactory.SpawnAreaPulse(GetTree(), GlobalPosition, range, pulseColor, 0.6f, 0.35f);
                // A gob of tar at the nearest one, so the sprayer visibly sprays
                if (nearest != null)
                {
                    _look?.Track(nearest);
                    _look?.Fire();
                    var from = Muzzle();
                    VfxFactory.SpawnProjectile(GetTree(), from, nearest.GlobalPosition, new Color(0.25f, 0.18f, 0.4f), 14f, 1.4f, ProjectileImpact.Tar);
                    // Tar Pools: the gob leaves a pool where it lands
                    if (Has("tar_pools"))
                        TarPool.Spawn(GetTree(), nearest.GlobalPosition, from.DistanceTo(nearest.GlobalPosition) / 14f);
                }
            }
        }

        // ── New Tower Types ──

        private float _scatterTimer;
        private void UpdateScatterCannon(float dt)
        {
            if (!_autoFireEnabled) return;

            _scatterTimer -= dt;
            if (_scatterTimer > 0) return;

            float range = GetEffectiveRange();
            var enemies = Roster.Enemies(GetTree());
            // Find closest enemy as center of AoE (a ground blast: flyers are out of reach)
            var target = NearestInRange(range, out float targetDist);

            if (target == null) { _scatterTimer = IdleRescan; return; }
            if (_look != null && !_look.ReadyToFire(target)) return; // turn first, then fire

            // Damage all enemies in splash radius around the target
            float dmg = GetEffectiveDamage(Constants.SCATTER_CANNON_INTERVAL);
            // Cluster Shells: wider blasts that don't weaken toward the edge
            bool cluster = Has("cluster_shells");
            float splashRadius = Constants.SCATTER_CANNON_RADIUS * (cluster ? Constants.PERK_CLUSTER_RADIUS_MULT : 1f);
            int hits = 0;
            bool shred = HasBranch("shredder");

            foreach (var enemy in enemies)
            {
                if (enemy is not VineEnemy ve || !CanTarget(ve)) continue;
                float dist = target.GlobalPosition.DistanceTo(ve.GlobalPosition);
                if (dist <= splashRadius)
                {
                    // Shredder: strip the armour first, so this blast and everything after lands
                    if (shred) ve.BreakArmour(Constants.SHREDDER_SECONDS);
                    // Falloff: full damage at center, half at edge
                    float falloff = cluster ? 1f : 1f - (dist / splashRadius) * 0.5f;
                    ve.TakeDamage(dmg * falloff, HitKind);
                    if (RelicRun.Ignites(Data.Type)) IgniteFrom(ve, dmg * falloff, GetEffectiveFireInterval(Constants.SCATTER_CANNON_INTERVAL));
                    hits++;
                }
            }

            // VFX: explosion at target
            _look?.Track(target);
            _look?.Fire();
            var muzzle = Muzzle();
            VfxFactory.SpawnMuzzleFlash(GetTree(), muzzle, target.GlobalPosition - muzzle, new Color(1f, 0.55f, 0.2f), 1.8f);
            VfxFactory.SpawnExplosion(GetTree(), target.GlobalPosition + Vector3.Up * 0.3f, splashRadius, RelicRun.Ignites(Data.Type) ? VineEnemy.FireColor : new Color(1f, 0.5f, 0.15f));

            Rearm(ref _scatterTimer, GetEffectiveFireInterval(Constants.SCATTER_CANNON_INTERVAL));
            IsActive = true;
        }

        private float _teslaTimer;
        private void UpdateTeslaCoil(float dt)
        {
            if (!_autoFireEnabled) return;

            _teslaTimer -= dt;
            if (_teslaTimer > 0) return;

            float range = GetEffectiveRange();
            var enemies = Roster.Enemies(GetTree());

            // Find primary target
            var primary = NearestInRange(range, out float primaryDist);

            if (primary == null) { _teslaTimer = IdleRescan; return; }

            float dmg = GetEffectiveDamage(Constants.TESLA_COIL_INTERVAL);
            // Overload: a much heavier first strike that stops the target for a moment
            bool overload = HasBranch("overload");
            primary.TakeDamage(dmg * (overload ? Constants.OVERLOAD_MULT : 1f), HitKind);
            if (overload) primary.ApplyStun(Constants.OVERLOAD_STUN);

            // Chain to nearby enemies
            var chainColor = new Color(0.3f, 0.7f, 1f);
            _look?.Track(primary);
            _look?.Fire();
            VfxFactory.SpawnArc(GetTree(), Muzzle(0.5f), primary.BodyCentre, chainColor);

            var hit = new HashSet<VineEnemy> { primary };
            var lastPos = primary.GlobalPosition;
            // Arc Conductor: more arcs, and each keeps more of the damage
            bool arc = Has("arc_conductor");
            int chains = Constants.TESLA_COIL_CHAIN_COUNT + (arc ? Constants.PERK_ARC_EXTRA_CHAINS : 0);

            for (int c = 0; c < chains; c++)
            {
                VineEnemy nextTarget = null;
                float nextDist = Constants.TESLA_COIL_CHAIN_RANGE;

                bool sight = Sight.Matters;
                foreach (var enemy in enemies)
                {
                    if (enemy is not VineEnemy ve || !CanTarget(ve) || hit.Contains(ve)) continue;
                    float dist = lastPos.DistanceTo(ve.GlobalPosition);
                    // The arc jumps through the air, not through a wall to the next canyon
                    if (dist < nextDist && (!sight || Sight.Clear(lastPos + Vector3.Up * 0.6f, ve.BodyCentre)))
                    {
                        nextDist = dist;
                        nextTarget = ve;
                    }
                }

                if (nextTarget == null) break;

                float chainDmg = dmg * (arc ? Constants.PERK_ARC_CHAIN_FACTOR : Constants.TESLA_COIL_CHAIN_FACTOR);
                nextTarget.TakeDamage(chainDmg, HitKind);
                VfxFactory.SpawnArc(GetTree(), lastPos + Vector3.Up * 0.6f, nextTarget.GlobalPosition + Vector3.Up * 0.6f, chainColor);

                hit.Add(nextTarget);
                lastPos = nextTarget.GlobalPosition;
            }

            // Arcanist Prisms near it pass the arc on
            EchoArcs(dmg);

            Rearm(ref _teslaTimer, GetEffectiveFireInterval(Constants.TESLA_COIL_INTERVAL));
            IsActive = true;
        }

        private float _flakTimer;
        private int _flakBurstCount;
        // One buffer for every battery's targets (it was a new list each burst)
        private static readonly List<(VineEnemy e, float d)> _flakBuffer = new(32);

        private void UpdateFlakBattery(float dt)
        {
            if (!_autoFireEnabled) return;

            _flakTimer -= dt;
            if (_flakTimer > 0) return;

            float range = GetEffectiveRange();
            var enemies = Roster.Enemies(GetTree());

            // Hit up to N enemies in range. Damage per hit goes through the same bonuses as every
            // other tower (it used the raw constant, so no perk, relay, slot or relic touched it)
            float dmg = GetEffectiveDamage(Constants.FLAK_BATTERY_INTERVAL);
            // Saturation Fire: more targets per burst
            int maxTargets = Constants.FLAK_BATTERY_MAX_TARGETS
                + (Has("saturation_fire") ? Constants.PERK_FLAK_EXTRA_TARGETS : 0);
            int targetsHit = 0;
            var flakColor = new Color(1f, 0.6f, 0.2f);

            // The battery turns onto the nearest and opens up once it's on it (it tracked
            // whichever enemy came first in the list, and fired before it had turned)
            // Anti-air first: flyers in range come before anything on the ground, nearest first
            var inRange = _flakBuffer;
            inRange.Clear();
            var flakPos = Roster.EnemyPositions(GetTree());
            var flakAt = At;
            bool flakSight = Sight.Matters;
            var flakEye = flakSight ? EyePoint : Vector3.Zero;
            for (int i = 0; i < enemies.Count && i < flakPos.Count; i++)
            {
                float fdx = flakAt.X - flakPos[i].X, fdz = flakAt.Z - flakPos[i].Z;
                float d = Mathf.Sqrt(fdx * fdx + fdz * fdz); // across the ground, like the ring
                if (d <= range && enemies[i] is VineEnemy le && CanTarget(le)
                    && (!flakSight || Sees(le, flakPos[i], flakEye))) inRange.Add((le, d));
            }
            if (inRange.Count == 0) { _flakTimer = IdleRescan; return; }
            inRange.Sort((a, b) => a.e.IsFlying != b.e.IsFlying ? (a.e.IsFlying ? -1 : 1) : a.d.CompareTo(b.d));
            var lead = inRange[0].e;
            if (_look != null && !_look.ReadyToFire(lead, 30f)) return;
            _look?.Fire();
            bool skyguard = HasBranch("skyguard");

            foreach (var (ve, _) in inRange)
            {
                ve.TakeDamage(dmg * (skyguard && ve.IsFlying ? Constants.SKYGUARD_AIR_MULT : 1f), HitKind);
                targetsHit++;

                // VFX: small projectile to each target
                if (targetsHit <= 3) // Limit VFX to prevent spam
                    VfxFactory.SpawnTracer(GetTree(), Muzzle(), ve.BodyCentre, flakColor, 45f);

                if (targetsHit >= maxTargets) break;
            }

            if (targetsHit > 0)
            {
                IsActive = true;
                _flakBurstCount++;

                // Muzzle flash every 3rd burst to avoid VFX overload
                if (_flakBurstCount % 2 == 0)
                {
                    var m = Muzzle();
                    var at = _look?.AimNode != null ? _look.MuzzleGlobal - _look.AimNode.GlobalPosition : Vector3.Zero;
                    at.Y = 0;
                    VfxFactory.SpawnMuzzleFlash(GetTree(), m, at, flakColor, 0.8f);
                }
            }

            Rearm(ref _flakTimer, GetEffectiveFireInterval(Constants.FLAK_BATTERY_INTERVAL));
        }

        // Pneumatic Ram — periodic knockback. (Auto-fire flag was set but nothing ran it,
        // and the signal path only started an effect timer, so the tower never moved anyone.)
        private float _pushTimer;
        private void UpdatePushPull(float dt)
        {
            bool canFire = _autoFireEnabled || _effectTimer > 0;
            if (!canFire) return;
            if (_effectTimer > 0)
            {
                _effectTimer -= dt;
                if (_effectTimer <= 0) _signalBoosted = false;
            }

            _pushTimer -= dt;
            if (_pushTimer > 0) return;

            float range = GetEffectiveRange();
            float force = Constants.PUSH_PULL_FORCE * (_signalBoosted ? Constants.TOWER_SIGNAL_BOOST : 1f) * UpgradeForceMult * RoleRun.ForceMult * PowerType.RamMult;
            bool repulsor = HasBranch("repulsor");
            bool stun = Has("hydraulic_stun"); // Hydraulic Stun
            int pushed = 0;
            VineEnemy nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var enemy in Roster.Enemies(GetTree()))
            {
                if (enemy is not VineEnemy ve || !CanTarget(ve)) continue; // flyers are out of reach
                var away = ve.GlobalPosition - GlobalPosition;
                away.Y = 0;
                float dist = away.Length();
                if (dist > range || dist < 0.001f) continue;
                if (dist < nearestDist) { nearestDist = dist; nearest = ve; }

                if (RelicRun.GravityPull)
                {
                    // Gravity Rams: dragged in toward the ram (not past it) and crushed
                    float pull = Mathf.Max(0f, dist - RelicRun.T(RelicRun.GravityRams, "pullTo", 1.4f)) * RelicRun.T(RelicRun.GravityRams, "pullShare", 0.85f);
                    ve.ApplyKnockback(-away / dist * pull);
                    ve.Crush(RelicRun.T(RelicRun.GravityRams, "crushTaken", 1.3f), RelicRun.T(RelicRun.GravityRams, "crushTime", 3f));
                }
                // Full shove next to the ram, fading to nothing at the edge of range
                else ve.ApplyKnockback(away / dist * force * (1f - dist / range));
                if (stun) ve.ApplyStun(Constants.PERK_STUN_DURATION);
                if (repulsor) ve.TakeDamage(Constants.REPULSOR_DAMAGE * UpgradeDamageMult * PowerType.RamMult, DamageKind.Heavy);
                ApplyOnHitEffects(ve);
                pushed++;
            }

            if (pushed == 0) return; // Hold the charge until something is in range

            _look?.Track(nearest);
            _look?.Fire();
            if (RelicRun.GravityPull) VfxFactory.SpawnImplosion(GetTree(), GlobalPosition, range);
            else
            {
                VfxFactory.SpawnAreaPulse(GetTree(), GlobalPosition, range, new Color(0.3f, 0.5f, 0.9f), 0.5f, 0.45f);
                if (nearest != null) VfxFactory.SpawnShove(GetTree(), Muzzle(0.4f), nearest.GlobalPosition - GlobalPosition);
            }
            Rearm(ref _pushTimer, GetEffectiveFireInterval(Constants.PUSH_PULL_INTERVAL));
            IsActive = true;
        }

        /// <summary>Molten Rounds: a hit of <paramref name="hit"/> (a shot every <paramref name="interval"/> s) sets the target burning.</summary>
        private static void IgniteFrom(VineEnemy e, float hit, float interval)
        {
            if (!IsInstanceValid(e) || !e.IsAlive) return;
            float share = RelicRun.T(RelicRun.MoltenRounds, "burnShare", 0.45f);
            e.Ignite(hit / Mathf.Max(0.05f, interval) * share, RelicRun.T(RelicRun.MoltenRounds, "burnTime", 3f), RelicRun.T(RelicRun.MoltenRounds, "spreadRadius", 3f));
        }

        // Living Walls: thorns cut and slow whatever walks past a wall
        private float _thornTimer, _thornFx;
        /// <summary>Enemies the thorns cut on their last tick (tests).</summary>
        internal int ThornHits { get; private set; }

        private void UpdateThorns(float dt)
        {
            if (IsDestroyed || (_thornTimer -= dt) > 0f) return;
            const float step = 0.25f;
            _thornTimer = step;
            string id = RelicRun.LivingWalls;
            float radius = RelicRun.T(id, "radius", 2.6f);
            int wave = GameManager.Instance?.CurrentWave ?? 0;
            // Grows with the wave and the tower damage the tree and keystones give
            float dps = RelicRun.T(id, "dps", 7f) * (1f + RelicRun.T(id, "dpsPerWave", 0.12f) * wave) * VineWaveLoader.RampAt(wave)
                * (SignalTuningEditor.DamageTowerDPS / Constants.DAMAGE_TOWER_DPS) * MetaRun.TowerDamageFactor;
            bool fx = (_thornFx -= step) <= 0f;
            if (fx) _thornFx = 0.6f;
            int hits = 0;
            foreach (var ve in Roster.Enemies(GetTree()))
            {
                if (!IsInstanceValid(ve) || !ve.IsAlive || ve.IsFlying) continue;
                var d = ve.GlobalPosition - GlobalPosition; d.Y = 0;
                if (d.Length() > radius) continue;
                ve.ApplySlow(RelicRun.T(id, "slow", 0.3f), 0.45f);
                ve.TakeDamage(dps * step, DamageKind.Normal);
                if (fx) VfxFactory.SpawnThorns(GetTree(), ve.GlobalPosition + Vector3.Up * 0.3f);
                hits++;
            }
            ThornHits = hits;
            if (hits > 0) IsActive = true;
        }

        // Overclock Relay — keeps adjacent attack towers buffed. (Only the signal path could
        // deliver buffs before, and signal chains are no longer buildable.)
        private float _buffPulseTimer;
        // ── Veterancy: ranks from this tower's own kills ──
        public int Kills { get; private set; }
        public int Rank { get; private set; }
        private Label3D _chevrons;
        public float VeteranDamage => Rank * Veterancy.DamagePerRank;
        public float VeteranRate => Rank * Veterancy.RatePerRank;

        /// <summary>An enemy this tower hit last died.</summary>
        public void CreditKill()
        {
            if (IsDestroyed) return;
            Kills += MetaRun.VeteranKillMult; // Battle-Hardened (perk tree): kills count double
            int r = Veterancy.RankFor(Kills);
            if (r <= Rank) return;
            Rank = r;
            ShowChevrons();
            if (IsInsideTree())
            {
                DamageNumbers.Tag(GlobalPosition + Vector3.Up * 2.6f,
                    $"{Veterancy.NameOf(Rank).ToUpperInvariant()}: +{VeteranDamage * 100:0}% damage, +{VeteranRate * 100:0}% rate", new Color(1f, 0.85f, 0.35f));
                VfxFactory.SpawnEnergyBurst(GetTree(), GlobalPosition + Vector3.Up * 1.2f, new Color(1f, 0.85f, 0.35f), 10);
            }
        }

        /// <summary>Chevrons over the tower, one a rank (a star at the top rank).</summary>
        private void ShowChevrons()
        {
            if (Rank <= 0) { if (_chevrons != null) _chevrons.Visible = false; return; }
            if (_chevrons == null || !IsInstanceValid(_chevrons))
            {
                _chevrons = new Label3D
                {
                    Name = "Chevrons", Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FixedSize = true,
                    PixelSize = 0.0016f, FontSize = 30, OutlineSize = 8, Modulate = new Color(1f, 0.82f, 0.3f),
                    Position = new Vector3(0, 2.05f, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                };
                AddChild(_chevrons);
            }
            _chevrons.Text = Rank >= Veterancy.Kills.Length ? "★" : string.Concat(System.Linq.Enumerable.Repeat("▲", Rank));
            _chevrons.Visible = true;
        }

        /// <summary>The panel's line about this tower's rank.</summary>
        public string RankLine()
        {
            int next = Veterancy.NextAt(Rank);
            string name = Veterancy.NameOf(Rank);
            string bonus = Rank > 0 ? $" (+{VeteranDamage * 100:0}% damage, +{VeteranRate * 100:0}% rate)" : "";
            return next > 0 ? $"{name}{bonus}: {Kills} kills, next rank at {next}" : $"{name}{bonus}: {Kills} kills";
        }

        // ── Crew links and what boosts a tower ──
        private string _lastTag = "";
        private float _crewTimer, _tagTimer = (float)GD.RandRange(1.0, Constants.BUFF_TAG_INTERVAL);

        /// <summary>Firing towers this one is linked to (counted once a second).</summary>
        public int CrewLinks { get; private set; }

        /// <summary>Does this node take part in crew links (a firing tower, not a relay or a wall)?</summary>
        public bool IsCrew => Data != null && Data.AutoFires && Data.Type != VineNodeType.BuffEmitter && !IsDestroyed;

        /// <summary>Damage and fire-rate share from crew links.</summary>
        public float CrewBonus => IsCrew ? Mathf.Min(CrewLinks, Constants.CREW_MAX_LINKS) * (Constants.CREW_BONUS_PER_LINK + MetaRun.CrewBonusAdd + RoleRun.CrewBonusAdd) : 0f;

        /// <summary>The relay boost on this tower right now (0 if none).</summary>
        public float RelayBoost => _buffStrength * SignalTuningEditor.BuffDamageBonus;

        /// <summary>Towers this relay boosted on its last pulse (0 = it's doing nothing).</summary>
        public int RelayTargets { get; private set; }

        /// <summary>Inside the Spire's dome (checked once a second): faster fire and slow self-repair.</summary>
        public bool InDome { get; private set; }
        public float DomeBonus => InDome && IsCrew ? Constants.DOME_RATE_BONUS + MetaRun.DomeBonusAdd + RelicRun.DomeInsideRate : 0f;
        /// <summary>Overcharged Dome: towers outside the dome fire slower.</summary>
        public float OutsideDomePenalty => !InDome && IsCrew ? RelicRun.DomeOutsideRate : 0f;

        internal void RecountCrew()
        {
            InDome = ServiceLocator.TryGet<ConversionDome>(out var dome) && IsInstanceValid(dome) && dome.IsInsideDome(GlobalPosition);
            if (!IsCrew || !ServiceLocator.TryGet<VineGrid>(out var grid)) { CrewLinks = 0; return; }
            int n = 0;
            foreach (var conn in grid.GetConnectionsFrom(GridPosition))
            {
                var other = grid.GetNode(conn.GetOtherEnd(GridPosition));
                if (other != null && other.IsCrew) n++;
            }
            CrewLinks = n;
        }

        /// <summary>A crew link's share for this run (the role and the perk tree add to it).</summary>
        public static float CrewPerLink => Constants.CREW_BONUS_PER_LINK + MetaRun.CrewBonusAdd + RoleRun.CrewBonusAdd;
        /// <summary>What an Overclock Relay next door adds (damage and fire rate each).</summary>
        public static float RelayShare => Constants.BUFF_EMITTER_STRENGTH * RoleRun.RelayStrengthMult * SignalTuningEditor.BuffDamageBonus;
        /// <summary>What BIT's Overclock adds while it lasts (damage and fire rate each).</summary>
        public static float OverclockShare => Constants.ABILITY_OVERCLOCK_STRENGTH * SignalTuningEditor.BuffDamageBonus;

        /// <summary>
        /// The tower panel's boost breakdown: each source, what it adds now, and its rule (caps,
        /// what doesn't stack). Players couldn't tell what links or Overclock did for a build,
        /// what they boosted, or where they stopped adding up (2026-10).
        /// </summary>
        public string BoostLines()
        {
            if (Data == null || !Data.AutoFires) return "";
            var lines = new System.Collections.Generic.List<string>();
            if (Data.Type == VineNodeType.BuffEmitter)
            {
                int reach = Mathf.Max(Has("relay_mesh") ? Constants.PERK_RELAY_REACH : 1, RoleRun.RelayReach);
                lines.Add($"Boosts towers within {reach} cell{(reach == 1 ? "" : "s")}, diagonals too: +{RelayShare * UpgradeBuffMult * 100:0}% damage and rate ({RelayTargets} now)");
                lines.Add("Doesn't stack: a tower takes only its strongest boost (relay or Overclock)");
                return string.Join("\n", lines);
            }
            int links = Mathf.Min(CrewLinks, Constants.CREW_MAX_LINKS);
            lines.Add($"Crew {links}/{Constants.CREW_MAX_LINKS}: +{CrewBonus * 100:0}%" + (CrewLinks > Constants.CREW_MAX_LINKS ? $" ({CrewLinks} touching, {Constants.CREW_MAX_LINKS} count)" : $" (+{CrewPerLink * 100:0}% per firing tower touching it)"));
            float boost = RelayBoost;
            lines.Add(boost > 0.001f
                ? $"Boost: +{boost * 100:0}% from {(boost > RelayShare * 1.5f ? "Overclock" : "a relay")} (strongest only)"
                : $"Boost: none (relay +{RelayShare * 100:0}%, Overclock +{OverclockShare * 100:0}%, strongest only)");
            if (IsCrew)
                lines.Add(InDome ? $"Dome: +{DomeBonus * 100:0}% rate, mends" : $"Dome: outside (inside +{(Constants.DOME_RATE_BONUS + MetaRun.DomeBonusAdd + RelicRun.DomeInsideRate) * 100:0}% rate)" + (OutsideDomePenalty < 0f ? $", {OutsideDomePenalty * 100:0}% out here" : ""));
            float dmg = (1f + CrewBonus) * (1f + boost) * (1f + VeteranDamage);
            float rate = (1f + CrewBonus) * (1f + boost) * (1f + DomeBonus) * (1f + VeteranRate) * (1f + OutsideDomePenalty);
            lines.Add($"All boosts: x{dmg * rate:0.00} damage a second");
            return string.Join("\n", lines);
        }

        /// <summary>The small tag over a tower: "+10% crew  +25% relay", or a relay with nothing beside it.</summary>
        public string BuffTagText()
        {
            if (Data?.Type == VineNodeType.BuffEmitter)
                return RelayTargets == 0 ? "relay idle: build towers beside it" : $"boosting {RelayTargets} tower{(RelayTargets == 1 ? "" : "s")}";
            var parts = new System.Collections.Generic.List<string>();
            if (CrewBonus > 0.001f) parts.Add($"+{CrewBonus * 100:0}% crew ({Mathf.Min(CrewLinks, Constants.CREW_MAX_LINKS)} linked)");
            if (DomeBonus > 0.001f) parts.Add($"+{DomeBonus * 100:0}% rate in the dome");
            if (RelayBoost > 0.001f) parts.Add($"+{RelayBoost * 100:0}% relay");
            return string.Join("  ", parts);
        }

        private void ShowBuffTag()
        {
            if (IsDestroyed || Data == null || !IsInsideTree()) return;
            var phase = GameManager.Instance?.CurrentPhase ?? GamePhase.Build;
            if (phase is GamePhase.Defeat or GamePhase.Debrief) return;
            string text = BuffTagText();
            if (text.Length == 0) { _lastTag = ""; return; }
            // A tag says something when it changes (a new link, a relay in reach); repeating every
            // tower's tag every few seconds buried a big field under text. Small builds still get
            // the reminder, and the whole field shares a few tags a second.
            bool changed = text != _lastTag;
            if (!changed && Roster.Nodes(GetTree()).Count > Constants.BUFF_TAG_REPEAT_MAX_TOWERS) return;
            if (!DamageNumbers.TakeTagSlot()) return;
            _lastTag = text;
            bool idle = Data.Type == VineNodeType.BuffEmitter && RelayTargets == 0;
            var color = idle ? new Color(0.6f, 0.62f, 0.66f) : RelayBoost > 0.001f ? _baseColor.Lightened(0.35f) : new Color(0.35f, 0.9f, 1f);
            DamageNumbers.Tag(GlobalPosition + Vector3.Up * 2.1f, text, color);
        }

        private void UpdateBuffEmitter(float dt)
        {
            if (!_autoFireEnabled) return;
            _buffPulseTimer -= dt;
            if (_buffPulseTimer > 0) return;
            _buffPulseTimer = Constants.BUFF_EMITTER_PULSE_INTERVAL;

            if (!ServiceLocator.TryGet<VineGrid>(out var grid)) return;
            int buffed = 0;
            // Relay Mesh: two cells out instead of next door
            // Arcanist relays reach as far
            int reach = Mathf.Max(Has("relay_mesh") ? Constants.PERK_RELAY_REACH : 1, RoleRun.RelayReach);
            for (int dx = -reach; dx <= reach; dx++)
            {
                for (int dy = -reach; dy <= reach; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var neighbor = grid.GetNode(GridPosition.X + dx, GridPosition.Y + dy);
                    if (neighbor?.Data == null || neighbor.IsDestroyed) continue;
                    if (neighbor.Data.Category != VineNodeCategory.Effect) continue;
                    if (neighbor.Data.Type is VineNodeType.BuffEmitter or VineNodeType.BarrierWall or VineNodeType.SalvageDrill) continue;

                    neighbor.ReceiveBuff(Constants.BUFF_EMITTER_STRENGTH * UpgradeBuffMult * RoleRun.RelayStrengthMult);
                    VfxFactory.SpawnBuffMotes(GetTree(), neighbor.GlobalPosition, _baseColor.Lightened(0.3f));
                    buffed++;
                }
            }
            IsActive = buffed > 0;
            RelayTargets = buffed;
            if (buffed > 0) _look?.Fire();
        }

        // ── S5: Slot-modified stat helpers ──

        /// <summary>
        /// Effective range, modified by slotted components (ExtendedRange).
        /// </summary>
        private float GetEffectiveRange()
        {
            float range = Data.Range;
            // Live tuning / perks ("Long Barrel", meta range perks) — was never read
            if (range > 0)
            {
                range += Data.Type == VineNodeType.SlowField
                    ? SignalTuningEditor.SlowFieldRange - Constants.SLOW_FIELD_RANGE
                    : SignalTuningEditor.DamageTowerRange - Constants.DAMAGE_TOWER_RANGE;
                range = Mathf.Max(range, Constants.VINE_CELL_SIZE);
            }
            if (_slotSystem != null && _slotSystem.HasComponent(TowerComponentType.ExtendedRange))
                range *= 1f + Constants.SLOT_EXTENDED_RANGE;
            // Elevated terrain bonus
            if (ServiceLocator.TryGet<VineGrid>(out var grid) && grid.IsElevated(GridPosition))
                range *= 1f + Constants.ELEVATED_RANGE_BONUS;
            return range * UpgradeRangeMult * MetaRun.TowerRangeFactor * PowerType.TowerRangeMult; // One Big Gun; Power type
        }

        /// <summary>A new tower's reach before upgrades (the build ghost's sight): live tuning, the tree and the power type.</summary>
        public static float NewTowerRange(VineNodeData data)
        {
            float range = data.Range;
            if (range <= 0f) return 0f;
            range += data.Type == VineNodeType.SlowField
                ? SignalTuningEditor.SlowFieldRange - Constants.SLOW_FIELD_RANGE
                : SignalTuningEditor.DamageTowerRange - Constants.DAMAGE_TOWER_RANGE;
            range = Mathf.Max(range, Constants.VINE_CELL_SIZE);
            return range * MetaRun.TowerRangeFactor * PowerType.TowerRangeMult;
        }

        /// <summary>The Junk Turret's base time between shots.</summary>
        private float DamageTowerBaseInterval => _autoFireEnabled ? Constants.TOWER_AUTO_FIRE_INTERVAL : FIRE_INTERVAL;

        /// <summary>
        /// Time between attacks for a tower whose base is <paramref name="baseInterval"/>, shortened
        /// by fire-rate boosts: the tower's attack-rate multiplier, the Overclock Relay's buff and
        /// the Overcrank Spring slot. Every attacking tower goes through this (the Scatter Cannon,
        /// Tesla Coil and Flak Battery ran on fixed timers, and the slot made towers slower).
        /// </summary>
        private float GetEffectiveFireInterval(float baseInterval)
        {
            // Overclock Relay buff raises fire rate as well as damage
            float mult = AttackRateMultiplier * (1f + _buffStrength * SignalTuningEditor.BuffDamageBonus) * UpgradeRateMult * (1f + CrewBonus) * (1f + DomeBonus) * (1f + VeteranRate)
                * (1f + OutsideDomePenalty) * (MetaRun.LastStandLeft > 0f ? 2f : 1f) // Overcharged Dome; Last Stand
                * PowerType.TowerRateMult; // Power type
            if (_slotSystem != null && _slotSystem.HasComponent(TowerComponentType.RapidFire))
                mult *= 1f + Constants.SLOT_RAPID_FIRE;
            return baseInterval / Mathf.Max(mult, 0.1f);
        }

        /// <summary>
        /// Start the next cooldown, keeping what the last frame overshot by (setting the timer
        /// straight to the interval lost up to a frame per shot: 8% of the Flak's fire rate).
        /// After an idle spell the overshoot is dropped instead of firing a catch-up burst.
        /// </summary>
        private static void Rearm(ref float timer, float interval)
            => timer = timer > -interval ? timer + interval : interval;

        /// <summary>
        /// Effective damage per shot, modified by signal boost, buffs, and slotted components.
        /// </summary>
        private float GetEffectiveDamage(float interval)
        {
            float dmg = Data.Damage * interval * UpgradeDamageMult;
            // The role's own towers hit harder (Bruteforge guns, Arcanist coils)
            dmg *= 1f + RoleRun.TypeDamageOf(Data.Type);

            // Live tuning / perks ("Overclocked Cores", meta damage perks) scale all towers.
            // SignalTuningEditor.DamageTowerDPS was modified by those perks but never read.
            dmg *= SignalTuningEditor.DamageTowerDPS / Constants.DAMAGE_TOWER_DPS;

            // Signal boost multiplier
            if (_signalBoosted)
                dmg *= Constants.TOWER_SIGNAL_BOOST;

            // Buff multiplier
            dmg *= 1f + _buffStrength * SignalTuningEditor.BuffDamageBonus;

            // Crew links to neighbouring firing towers, and the tower's own rank
            dmg *= (1f + CrewBonus) * (1f + VeteranDamage);

            // Overclock component
            if (_slotSystem != null && _slotSystem.HasComponent(TowerComponentType.Overclock))
                dmg *= 1f + Constants.SLOT_OVERCLOCK_DAMAGE;

            // Adjacency synergies (e.g. Thermal Shock) — computed by TowerSlotSystem, was never applied
            if (_slotSystem != null)
                dmg *= 1f + _slotSystem.GetSynergyDamageBonus();

            // Territory conquest buff ("tower_damage_mult")
            dmg *= GameManager.Instance?.RunTowerDamageMult ?? 1f;

            // Relics (Molten Rounds: Tesla and Flak weaker) and keystones (Swarm, One Big Gun, Chain Reaction)
            dmg *= RelicRun.TowerDamageMult(Data.Type) * MetaRun.TowerDamageFactor;

            return dmg;
        }

        /// <summary>
        /// Apply on-hit effects from slotted barrel components.
        /// </summary>
        private void ApplyOnHitEffects(VineEnemy target)
        {
            if (_slotSystem == null) return;

            if (_slotSystem.HasComponent(TowerComponentType.CryoBolt))
                target.ApplySlow(Constants.SLOT_CRYO_SLOW, Constants.SLOT_CRYO_DURATION);

            // IncendiaryRound, ChainArc, ScatterShot, PiercingRound
            // are more complex — stubs for now, full impl in TowerSlotSystem
        }

        // S5: Slot system reference — set by TowerSlotSystem when components are slotted
        private TowerSlotSystem _slotSystem;

        /// <summary>
        /// Attach a slot system to this node for component-modified behavior.
        /// </summary>
        public void SetSlotSystem(TowerSlotSystem system) => _slotSystem = system;

        /// <summary>
        /// Get this tower's slot system (null if no slots).
        /// </summary>
        public TowerSlotSystem GetSlotSystem() => _slotSystem;

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

        // ── Health system (effect nodes only) ──

        /// <summary>
        /// Take damage from enemy ranged attacks. Only effect-category nodes have health.
        /// </summary>
        public void TakeDamage(float amount, TowerLoss by = TowerLoss.Other)
        {
            if (!_hasHealth || IsDestroyed || IsWrecked) return;

            // Fortified by Repair Pulse (Environment power type)
            if (_fortifyLeft > 0f) amount *= PowerType.FortifyTaken;
            // Towers keep pace with the climb's damage: enemy hits grow a share a wave and tower
            // health didn't, so deep waves (chaos above all) flattened 60 to 130 towers at once
            amount *= Climb.TowerTaken(GameManager.Instance?.CurrentWave ?? 1);
            NodeCurrentHealth -= amount;
            Losses.Hit(by, amount);
            _damageFlashTimer = 0.1f;
            if (HasBranch("spiked")) SpikeBack();

            // Flash mesh white on hit
            if (_modelRoot != null)
                FlashModel(true, Colors.White);
            else if (_mesh?.MaterialOverride is StandardMaterial3D mat)
            {
                mat.EmissionEnabled = true;
                mat.Emission = Colors.White;
                mat.EmissionEnergyMultiplier = 2f;
            }

            if (NodeCurrentHealth <= 0)
            {
                Losses.Wrecked(by);
                if (WrecksAtZero) Wreck();
                else DestroyNode();
            }
        }

        // ── Wrecks ──

        /// <summary>
        /// Knocked out: at zero health a tower stands as a wreck (it doesn't fire, nothing targets
        /// it, it still blocks the maze) until it is repaired for a share of what went into it, or
        /// sold. A tower used to vanish with every upgrade on it, and one bad chaos or siege wave
        /// took 50 to 130 towers at once: the run was over a wave later, with no warning. Now a
        /// wave that breaks the defence costs leaks and a repair bill, and you choose what to fix.
        /// </summary>
        public bool IsWrecked { get; private set; }

        /// <summary>Guns, drills and the rest wreck; walls (Brutes break them) are gone at zero. Fortress
        /// Doctrine towers wreck too (they were gone at zero, upgrades and all: with nothing healing
        /// them either, a full tree with the keystone fell 12 waves before the same tree without it).</summary>
        public bool WrecksAtZero => Data != null && Data.Category == VineNodeCategory.Effect
            && Data.Type != VineNodeType.BarrierWall;

        /// <summary>What a repair costs: WRECK_REPAIR_SHARE of what was paid for the tower and its upgrades.</summary>
        public int RepairCost => Mathf.Max(1, Mathf.RoundToInt(Invested * Constants.WRECK_REPAIR_SHARE));

        /// <summary>Wrecks standing now.</summary>
        public static List<VineNode> Wrecks(SceneTree tree)
        {
            var list = new List<VineNode>();
            if (tree == null) return list;
            foreach (var vn in Roster.Nodes(tree))
                if (IsInstanceValid(vn) && vn.IsWrecked && vn.IsInsideTree()) list.Add(vn);
            return list;
        }

        /// <summary>Pay for the repair and bring it back at full health. False: not a wreck, or not enough Resources.</summary>
        public bool TryRepairWreck()
        {
            if (!IsWrecked || GameManager.Instance == null) return false;
            if (!GameManager.Instance.SpendResources(RepairCost)) return false;
            IsWrecked = false;
            NodeCurrentHealth = NodeMaxHealth;
            if (_modelRoot != null)
            {
                _modelRoot.Position = _wreckRestPos;
                _modelRoot.Rotation = _wreckRestRot;
            }
            if (IsInstanceValid(_wreckLabel)) _wreckLabel.Visible = false;
            Scorch(false);
            _wrecksStanding = Mathf.Max(0, _wrecksStanding - 1);
            VfxFactory.SpawnBuffMotes(GetTree(), GlobalPosition + Vector3.Up * 0.6f, new Color(0.5f, 1f, 0.6f));
            VfxFactory.SpawnEnergyBurst(GetTree(), GlobalPosition + Vector3.Up * 0.5f, new Color(0.5f, 1f, 0.6f), 6);
            return true;
        }

        /// <summary>Repair as many wrecks as the Resources cover, cheapest first. Returns how many.</summary>
        public static int RepairAll(SceneTree tree)
        {
            int n = 0;
            foreach (var w in Wrecks(tree).OrderBy(w => w.RepairCost))
                if (w.TryRepairWreck()) n++;
            if (n > 0) GD.Print($"[Wrecks] {n} repaired");
            return n;
        }

        private Vector3 _wreckRestPos, _wreckRestRot;
        private Label3D _wreckLabel;
        private float _wreckSmoke, _wreckSpark, _wreckRing, _wreckClock;
        private static int _wrecksStanding;
        /// <summary>Wrecks on the field (their smoke thins out when there are many).</summary>
        internal static int WrecksStanding => _wrecksStanding;
        /// <summary>Tests: particles a wreck has put up since it fell (smoke, sparks, rings).</summary>
        internal int WreckPuffs { get; private set; }
        private Dictionary<MeshInstance3D, Material> _scorched;
        private static ShaderMaterial _scorchMat;

        /// <summary>
        /// A wreck's model goes dark and scorched (an overlay that multiplies it down, with a hot
        /// glow pulsing in it); back to its own look when repaired.
        /// </summary>
        private void Scorch(bool on)
        {
            if (_modelRoot == null) return;
            if (on)
            {
                _scorchMat ??= new ShaderMaterial
                {
                    Shader = new Shader
                    {
                        Code = @"
shader_type spatial;
render_mode unshaded, blend_mul, depth_draw_never, cull_back, shadows_disabled;
void fragment() {
    float flick = 0.5 + 0.5 * sin(TIME * 7.0 + FRAGCOORD.x * 0.01);
    ALBEDO = vec3(0.32, 0.26, 0.24) + vec3(0.25, 0.08, 0.02) * flick;
}"
                    }
                };
                _scorched ??= new Dictionary<MeshInstance3D, Material>();
                foreach (var mi in _modelRoot.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
                {
                    if (_scorched.ContainsKey(mi)) continue;
                    _scorched[mi] = mi.MaterialOverlay;
                    mi.MaterialOverlay = _scorchMat;
                }
            }
            else if (_scorched != null)
            {
                foreach (var kv in _scorched)
                    if (IsInstanceValid(kv.Key)) kv.Key.MaterialOverlay = kv.Value;
                _scorched.Clear();
            }
        }

        private void Wreck()
        {
            IsWrecked = true;
            NodeCurrentHealth = 0f;
            VfxFactory.SpawnDeathBurst(GetTree(), GlobalPosition, _baseColor, 6);
            GameEvents.OnVineNodeDestroyed?.Invoke(this);
            if (_nodeHealthBar != null) _nodeHealthBar.Visible = false;
            if (_modelRoot != null)
            {
                // Knocked askew and sunk: plainly out of the fight
                _wreckRestPos = _modelRoot.Position;
                _wreckRestRot = _modelRoot.Rotation;
                _modelRoot.Position = _wreckRestPos + Vector3.Down * 0.22f;
                _modelRoot.Rotation = _wreckRestRot + new Vector3(Mathf.DegToRad(16f), 0f, Mathf.DegToRad(-11f));
                HitFlash.Off(_modelRoot);
                _modelFlashed = false;
            }
            if (!IsInstanceValid(_wreckLabel))
            {
                // The same size on screen at any zoom (at a world size it was a smudge from the
                // play camera), pulsing while it waits for a repair
                _wreckLabel = new Label3D
                {
                    Name = "WreckLabel",
                    Text = "WRECKED\nneeds repair", FontSize = 20, OutlineSize = 6, FixedSize = true, PixelSize = 0.0022f,
                    Modulate = new Color(1f, 0.55f, 0.2f, 0.95f), OutlineModulate = new Color(0.1f, 0.02f, 0f, 0.95f),
                    Position = new Vector3(0, Mathf.Max(1.1f, _visualTop + 0.6f), 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                    NoDepthTest = true,
                };
                AddChild(_wreckLabel);
            }
            _wreckLabel.Visible = true;
            _wreckSmoke = 0.05f;
            _wreckSpark = 0.4f;
            _wreckRing = 0.2f;
            _wrecksStanding++;
            Scorch(true);
        }

        /// <summary>
        /// A wreck smokes, sparks and flickers, with a red ring pulsing at its foot, so a broken
        /// tower reads from across the map (the playtest: "need visual VFX for when a tower is
        /// broken needing repair"; it only knocked the model askew and let out an ember every
        /// two seconds). With many wrecks each one puffs less often.
        /// </summary>
        private void TickWreck(float dt)
        {
            _wreckClock += dt;
            if (IsInstanceValid(_wreckLabel))
            {
                float a = 0.6f + 0.4f * Mathf.Sin(_wreckClock * 4f);
                _wreckLabel.Modulate = new Color(1f, 0.55f + 0.15f * a, 0.2f, 0.55f + 0.4f * a);
            }
            var vp = VfxParticles.Get(GetTree());
            if (vp == null) return;
            float thin = Mathf.Max(1f, _wrecksStanding / 25f);
            float top = Mathf.Max(0.5f, _visualTop * 0.85f);
            var at = GlobalPosition + Vector3.Up * top;
            if ((_wreckSmoke -= dt) <= 0f)
            {
                _wreckSmoke = (0.22f + 0.1f * (float)GD.Randf()) * thin;
                vp.Emit(VfxParticles.Kind.Smoke, at + new Vector3(((float)GD.Randf() - 0.5f) * 0.3f, 0f, ((float)GD.Randf() - 0.5f) * 0.3f),
                    new Vector3(((float)GD.Randf() - 0.5f) * 0.3f, 0.9f + 0.5f * (float)GD.Randf(), ((float)GD.Randf() - 0.5f) * 0.3f),
                    new Color(0.1f, 0.09f, 0.09f, 0.6f), 0.35f, 2.2f, 1.6f, 0f, 0.8f);
                if (GD.Randf() < 0.35f)
                    vp.Emit(VfxParticles.Kind.Glow, at, Vector3.Up * 0.6f, new Color(1f, 0.45f, 0.12f), 0.28f, 0.35f, -0.4f);
                WreckPuffs++;
            }
            if ((_wreckSpark -= dt) <= 0f)
            {
                _wreckSpark = (0.6f + 0.6f * (float)GD.Randf()) * thin;
                // A short crackle: shorted wiring
                var spark = new Color(0.75f, 0.9f, 1f);
                for (int i = 0; i < 5; i++)
                    vp.Emit(VfxParticles.Kind.Spark, at, new Vector3((float)GD.Randf() - 0.5f, (float)GD.Randf() * 0.8f + 0.2f, (float)GD.Randf() - 0.5f) * 5f,
                        spark, 0.035f, 0.25f + 0.2f * (float)GD.Randf(), 0f, 9f, 1.5f);
                vp.Emit(VfxParticles.Kind.Glow, at, Vector3.Zero, spark, 0.45f, 0.08f, 0.3f);
                WreckPuffs++;
            }
            if ((_wreckRing -= dt) <= 0f)
            {
                _wreckRing = 1.5f * thin;
                float g = ServiceLocator.TryGet<VineGrid>(out var grid) ? grid.GetWorldHeight(GlobalPosition.X, GlobalPosition.Z) : GlobalPosition.Y;
                vp.Emit(VfxParticles.Kind.Ring, new Vector3(GlobalPosition.X, Mathf.Max(g, GlobalPosition.Y - 0.3f) + 0.08f, GlobalPosition.Z), Vector3.Zero,
                    new Color(1f, 0.25f, 0.12f), 0.6f, 0.9f, 1.2f);
                WreckPuffs++;
            }
        }

        private float _fortifyLeft;
        /// <summary>Fortified (Environment's Repair Pulse): takes PowerType.FortifyTaken of every hit for a while.</summary>
        public bool IsFortified => _fortifyLeft > 0f;
        public void Fortify(float seconds)
        {
            if (seconds <= 0f) return;
            _fortifyLeft = Mathf.Max(_fortifyLeft, seconds);
        }

        private float _spikeCooldownUntil;

        /// <summary>Spiked walls: whatever is close enough to have hit it takes damage back.</summary>
        private void SpikeBack()
        {
            float now = Time.GetTicksMsec() / 1000f;
            if (now < _spikeCooldownUntil) return;
            _spikeCooldownUntil = now + 0.4f;
            foreach (var n in Roster.Enemies(GetTree()))
                if (n is VineEnemy ve && ve.IsAlive && !ve.IsFlying
                    && ve.GlobalPosition.DistanceTo(GlobalPosition) < Constants.VINE_CELL_SIZE * 1.6f)
                    ve.TakeDamage(Constants.SPIKED_DAMAGE, DamageKind.Heavy);
        }

        private void DestroyNode()
        {
            // Death VFX
            VfxFactory.SpawnDeathBurst(GetTree(), GlobalPosition, _baseColor, 8);

            // Fire event
            GameEvents.OnVineNodeDestroyed?.Invoke(this);

            // Remove from grid
            if (ServiceLocator.TryGet<VineGrid>(out var grid))
                grid.RemoveNode(GridPosition);

            QueueFree();
        }

        // ── Visuals ──

        private void BuildVisual()
        {
            float size = Data.Category switch {
                VineNodeCategory.Sensor => 0.6f,
                VineNodeCategory.Effect => 0.8f,
                _ => 0.5f
            };

            // Towers with a sheet are built from it; other nodes use a mapped kit model
            var sheet = TowerSheet.Load(Data.Id);
            string modelPath = sheet == null ? GetModelPathForNodeType(Data.Type) : null;
            bool builtIn = false;
            if (sheet != null)
            {
                _look = TowerLook.Build(sheet, _baseColor);
                _modelRoot = _look;
                builtIn = true;
            }
            else _modelRoot = modelPath != null ? AssetLibrary.InstantiateNormalized(modelPath) : null;
            // Towers without a kit model get a built-in one instead of a bare cube
            if (_modelRoot == null)
            {
                _modelRoot = TowerMeshes.Build(Data.Type, _baseColor);
                builtIn = _modelRoot != null;
            }

            if (_modelRoot != null)
            {
                AddChild(_modelRoot);
                if (!builtIn)
                {
                    _modelRoot.RotationDegrees = new Vector3(0, AssetLibrary.GetModelYaw(modelPath), 0);
                    AssetLibrary.GroundModel(_modelRoot);
                }
                // The node's origin sits NODE_ORIGIN_HEIGHT above the ground (VineGrid.PlaceNode);
                // models grounded to the origin hovered that far over the terrain
                _modelRoot.Position += new Vector3(0, -Constants.NODE_ORIGIN_HEIGHT, 0);

                // Only apply material override if explicitly set in data (JSON-driven).
                // Default: keep the model's original materials/textures intact.
                if (!string.IsNullOrEmpty(Data.MaterialOverride))
                {
                    if (Data.MaterialOverride == "bit")
                    {
                        Color tint = Data.Category switch {
                            VineNodeCategory.Sensor => BitPalette.SensorTint,
                            VineNodeCategory.Effect => BitPalette.EffectTint,
                            _ => BitPalette.RouteTint
                        };
                        BitPalette.ApplyToNode(_modelRoot, tint);
                    }
                    else if (Data.MaterialOverride == "planet")
                    {
                        PlanetTheme.Current?.ApplyToNode(_modelRoot);
                    }
                }

                // Player accent rim so towers read as the player's against the planet
                var theme = PlanetTheme.Current;
                if (theme != null)
                    BitPalette.ApplyAccentRim(_modelRoot, theme.PlayerAccent, theme.TowerRimStrength);

                // Create invisible _mesh for compatibility (flash/emission state tracking)
                _mesh = new MeshInstance3D();
                _mesh.Visible = false;
                AddChild(_mesh);
            }
            else
            {
                // Procedural fallback — same as before
                _mesh = new MeshInstance3D();
                var box = new BoxMesh();
                box.Size = new Vector3(size, size, size);
                _mesh.Mesh = box;

                var mat = new StandardMaterial3D();
                mat.AlbedoColor = _baseColor;
                mat.Roughness = 0.7f;
                mat.Metallic = 0.4f;
                mat.EmissionEnabled = true;
                mat.Emission = _baseColor;
                mat.EmissionEnergyMultiplier = 0.6f;
                _mesh.MaterialOverride = mat;
                AddChild(_mesh);
            }

            // Status pip and HP bar sit just above whatever the tower model is (they were placed
            // for a 0.8 cube and ended up inside taller models)
            float top = size / 2f;
            if (_modelRoot != null)
            {
                var bb = AssetLibrary.GetCombinedAABB(_modelRoot);
                top = Mathf.Max(top, _modelRoot.Position.Y + bb.End.Y * _modelRoot.Scale.Y);
            }
            _visualTop = top;

            // State indicator: a small sphere on top (not on towers with a look: their band lights
            // when they fire, and a grey ball floated over every one of them)
            if (_look == null) BuildStateIndicator(top);

            BuildRangeAndHealth(top);
        }

        private void BuildStateIndicator(float top)
        {
            _stateIndicator = new MeshInstance3D();
            var sphere = new SphereMesh();
            sphere.Radius = 0.12f;
            sphere.Height = 0.24f;
            _stateIndicator.Mesh = sphere;
            _stateIndicator.Position = new Vector3(0, top + 0.15f, 0);

            var indicatorMat = new StandardMaterial3D();
            indicatorMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            indicatorMat.AlbedoColor = new Color(0.3f, 0.3f, 0.3f);
            _stateIndicator.MaterialOverride = indicatorMat;
            AddChild(_stateIndicator);
        }

        private void BuildRangeAndHealth(float top)
        {
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

            // (No floating role label: a "SENSOR: name" tag over every node cluttered the field.
            // The build bar and the selection panel name the node.)

            // Health bar for effect nodes
            if (_hasHealth)
            {
                _nodeHealthBar = new MeshInstance3D();
                var hpBarMesh = new BoxMesh();
                hpBarMesh.Size = new Vector3(0.8f, 0.06f, 0.06f);
                _nodeHealthBar.Mesh = hpBarMesh;
                _nodeHealthBar.Position = new Vector3(0, top + 0.35f, 0);
                var hpMat = new StandardMaterial3D();
                hpMat.AlbedoColor = new Color(0.1f, 0.9f, 0.1f);
                hpMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                _nodeHealthBar.MaterialOverride = hpMat;
                _nodeHealthBar.Visible = false; // shown once the tower takes damage
                AddChild(_nodeHealthBar);
            }
        }

        /// <summary>
        /// Map specific node types to 3D model asset paths.
        /// Returns null for routing/structural nodes that should stay procedural.
        /// </summary>
        /// <summary>
        /// Try to instantiate a normalized 3D model for the given node type.
        /// Returns null if no model is mapped or loading fails.
        /// Used by VinePlacer for ghost preview.
        /// </summary>
        /// <summary>
        /// A block under the tower's base reaching depth below it, so a tower on a slope or an
        /// elevated cell stands on something instead of overhanging the drop.
        /// </summary>
        internal void AddFooting(float depth, Material material, bool wireframe)
        {
            if (depth <= 0.02f || _footing != null) return;
            float cs = Constants.VINE_CELL_SIZE;
            float w = Data?.Type == VineNodeType.BarrierWall ? cs * 0.93f : cs * 0.8f;
            var size = new Vector3(w, depth + 0.02f, w);
            _footing = new MeshInstance3D { Name = "Footing", Mesh = new BoxMesh { Size = size }, MaterialOverride = material };
            // The base is NODE_ORIGIN_HEIGHT below the origin; the top sits just under it
            _footing.Position = new Vector3(0, -Constants.NODE_ORIGIN_HEIGHT + 0.02f - size.Y / 2f, 0);
            AddChild(_footing);
            if (wireframe) TronTheme.AddWireframeEdges(_footing, size);
        }

        public static Node3D TryLoadModelForType(VineNodeType type)
        {
            // Same model, grounding and offset as a placed node, so the ghost sits where the
            // tower will (the ghost is positioned like a node, NODE_ORIGIN_HEIGHT up)
            var sheet = TowerSheet.Load(VineNodeRegistry.Get(type)?.Id);
            string path = sheet == null ? GetModelPathForNodeType(type) : null;
            var model = sheet != null ? TowerLook.Build(sheet, VineNodeRegistry.Get(type).TintColor)
                : path != null ? AssetLibrary.InstantiateNormalized(path) : null;
            bool builtIn = sheet != null;
            if (model == null)
            {
                var data = VineNodeRegistry.Get(type);
                model = data != null ? TowerMeshes.Build(type, data.TintColor) : null;
                builtIn = model != null;
            }
            if (model == null) return null;
            var holder = new Node3D { Name = "GhostModel" };
            holder.AddChild(model);
            if (!builtIn)
            {
                model.RotationDegrees = new Vector3(0, AssetLibrary.GetModelYaw(path), 0);
                AssetLibrary.GroundModel(model);
            }
            model.Position += new Vector3(0, -Constants.NODE_ORIGIN_HEIGHT, 0);
            return holder;
        }

        private static string GetModelPathForNodeType(VineNodeType type)
        {
            return type switch {
                // Effect nodes — turrets and weapons
                VineNodeType.DamageTower => AssetLibrary.TURRET_A,
                VineNodeType.SlowField => AssetLibrary.TURRET_B,
                VineNodeType.PushPull => AssetLibrary.TURRET_C,
                VineNodeType.SignalCannon => AssetLibrary.ROCKET_LAUNCHER,
                VineNodeType.BuffEmitter => AssetLibrary.PLASMA_GUN,
                VineNodeType.LoopAnchor => AssetLibrary.WEAPON_A,

                // Sensor nodes — detection equipment
                VineNodeType.ProximitySensor => AssetLibrary.PROP_SATELLITE,
                VineNodeType.TypeSensor => AssetLibrary.PROP_RADAR,
                VineNodeType.HPSensor => AssetLibrary.PROP_ANTENNA_A,
                VineNodeType.CountSensor => AssetLibrary.PROP_ANTENNA_B,
                VineNodeType.Timer => AssetLibrary.PROP_GENERATOR_A,

                // Structural/routing nodes — infrastructure
                VineNodeType.Extender => AssetLibrary.PROP_LAMP_A,
                VineNodeType.Junction => AssetLibrary.PROP_GENERATOR_B,
                VineNodeType.Switch => AssetLibrary.PROP_BARRIER_A,
                VineNodeType.Gate => AssetLibrary.PROP_HEDGEHOG,
                VineNodeType.Inverter => AssetLibrary.PROP_LAMP_B,
                VineNodeType.Delay => AssetLibrary.PROP_BARRIER_B,
                VineNodeType.Latch => AssetLibrary.PROP_FENCE,
                VineNodeType.Pylon => AssetLibrary.WEAPON_B,

                _ => null
            };
        }

        private void FlashActive()
        {
            if (_modelRoot != null)
            {
                // Firing lights the tower's own glowing parts, not the whole body
                FlashModel(true, _baseColor.Lightened(0.3f), glowingOnly: true);
            }
            else if (_mesh?.MaterialOverride is StandardMaterial3D mat)
            {
                mat.EmissionEnabled = true;
                mat.Emission = _baseColor.Lightened(0.4f);
                mat.EmissionEnergyMultiplier = 1f;
            }
        }

        // Whether the model is lit by a flash now: turning it off walked the whole model every
        // physics step whether it was lit or not, 60 microseconds a tower (a 300-tower field spent
        // 140 ms a frame on it, 2026-10)
        private bool _modelFlashed;

        // The model's meshes, gathered once: walking the model on every shot cost a GetChildren
        // Array per node, the biggest allocation left in a 300-tower field (2026-10)
        private readonly List<MeshInstance3D> _flashParts = new();
        private Node3D _flashPartsOf;
        private int _flashPartsVersion = -1;

        private List<MeshInstance3D> FlashParts()
        {
            int v = _modelRoot is TowerLook tl ? tl.PartsVersion : 0;
            if (!ReferenceEquals(_flashPartsOf, _modelRoot) || v != _flashPartsVersion)
            {
                _flashParts.Clear();
                HitFlash.Collect(_modelRoot, _flashParts);
                _flashPartsOf = _modelRoot;
                _flashPartsVersion = v;
            }
            return _flashParts;
        }

        private void FlashModel(bool on, Color color, bool glowingOnly = false)
        {
            if (_modelRoot == null) return;
            // Restore exactly on flash-off: resetting only the energy left a tower glowing the
            // last flash colour (white after every hit)
            if (on) { HitFlash.On(FlashParts(), color, glowingOnly ? 1.8f : 0.5f, glowingOnly); _modelFlashed = true; }
            else if (_modelFlashed) { HitFlash.Off(FlashParts()); _modelFlashed = false; }
        }

        private void BuildIdleLabel()
        {
            _idleLabel = new Label3D();
            _idleLabel.Text = "NO SIGNAL";
            _idleLabel.FontSize = 48;
            _idleLabel.OutlineSize = 8;
            _idleLabel.Modulate = new Color(0.9f, 0.3f, 0.2f, 0.8f);
            _idleLabel.Position = new Vector3(0, Mathf.Max(1.2f, _visualTop + 0.6f), 0);
            _idleLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            AddChild(_idleLabel);
        }

        private float _barShown = -1f;

        private void UpdateNodeHealthBar()
        {
            if (_nodeHealthBar == null || !_hasHealth) return;
            float pct = NodeMaxHealth > 0 ? Mathf.Clamp(NodeCurrentHealth / NodeMaxHealth, 0f, 1f) : 1f;
            // Only when it changed: every write is a transform and a material update in the engine,
            // every physics step for every tower, most of them at full health
            if (pct == _barShown) return;
            _barShown = pct;
            // A bar over every healthy tower was clutter: show it only while damaged
            _nodeHealthBar.Visible = pct < 0.999f;
            _nodeHealthBar.Scale = new Vector3(pct, 1, 1);
            _nodeHealthBar.Position = new Vector3((pct - 1f) * 0.4f, _nodeHealthBar.Position.Y, 0);

            if (_nodeHealthBar.MaterialOverride is StandardMaterial3D mat)
                mat.AlbedoColor = pct > 0.5f
                    ? new Color(0.1f, 0.9f, 0.1f)
                    : pct > 0.25f
                        ? new Color(0.9f, 0.7f, 0.1f)
                        : new Color(0.9f, 0.1f, 0.1f);
        }

        private void UpdateVisualState()
        {
            // Main mesh — flash decay
            if (_modelRoot != null)
            {
                // Leave a running damage flash alone (it was undone the very next frame)
                if (!IsActive && _damageFlashTimer <= 0)
                    FlashModel(false, _baseColor);
            }
            else if (_mesh?.MaterialOverride is StandardMaterial3D meshMat)
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

            // S5: Status label: "NO SIGNAL" for signal-only, "BOOSTED" when signal-active (auto-fire is the default, unlabelled)
            if (_idleLabel != null)
            {
                if (_autoFireEnabled)
                {
                    if (_signalBoosted)
                    {
                        _idleLabel.Visible = true;
                        _idleLabel.Text = "BOOSTED";
                        _idleLabel.Modulate = new Color(1f, 0.85f, 0.2f, 0.9f);
                    }
                    else
                    {
                        _idleLabel.Visible = false;
                    }
                }
                else
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
    }

    public struct DelayedSignal
    {
        public SignalType Type;
        public float Strength;
        public Vector2I FromCell;
        public float TimeRemaining;
    }
}
