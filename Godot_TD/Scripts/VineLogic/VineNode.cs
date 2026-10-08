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
                return false;
            }
        }

        public void Initialize(VineNodeData data)
        {
            Data = data;
            _baseColor = data.TintColor;
            IsOpen = data.Type != VineNodeType.Gate; // Gates start closed

            AddToGroup(Constants.GROUP_VINE_NODE);

            // Effect nodes get health — they can be destroyed by enemy fire
            if (data.Category == VineNodeCategory.Effect)
            {
                _hasHealth = true;
                // Barrier walls get their own HP pool
                NodeMaxHealth = data.Type == VineNodeType.BarrierWall
                    ? Constants.BARRIER_WALL_HP
                    : Constants.VINE_NODE_BASE_HEALTH;

                // Relic: Quantum Splicer — node HP modifier (tradeoff for duplication chance)
                if (ServiceLocator.TryGet<RelicManager>(out var rmInit))
                {
                    float hpMod = rmInit.GetStatMods().NodeHPMult;
                    if (hpMod != 0) NodeMaxHealth *= 1f + hpMod;
                }

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
                && data.Type is not (VineNodeType.SlowField or VineNodeType.BarrierWall))
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

            // Relic: Aether Coil — passive 2 HP/sec regen to all effect nodes
            if (_hasHealth && NodeCurrentHealth < NodeMaxHealth &&
                ServiceLocator.TryGet<RelicManager>(out var rmRegen) && rmRegen.HasAetherCoil)
            {
                NodeCurrentHealth = Mathf.Min(NodeMaxHealth, NodeCurrentHealth + 2f * dt);
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
                        FlashModelRecursive(_modelRoot, false, _baseColor);
                    else if (_mesh?.MaterialOverride is StandardMaterial3D flashMat)
                        flashMat.EmissionEnergyMultiplier = 0.6f;
                }
            }

            // Update tower health bar
            UpdateNodeHealthBar();

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
        public void ReceiveBuff(float strength)
        {
            _buffStrength = Mathf.Max(_buffStrength, strength);
            _buffDecayTimer = 3f;
        }

        // Sensors
        private void UpdateSensor(float dt)
        {
            if (IsJammed) return;
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
                // Fire with SignalPower as strength — each effect node decrements by 1
                float power = Data.SignalPower > 0 ? Data.SignalPower : 3f;

                // Relic: Runic Transistor — routing nodes gain +1 signal power
                if (ServiceLocator.TryGet<RelicManager>(out var rm))
                    power += rm.GetStatMods().BonusSignalPower;

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
            var enemies = GetTree().GetNodesInGroup(Constants.GROUP_VINE_ENEMY);
            VineEnemy closest = null;
            float closestDist = GetEffectiveRange();

            foreach (var enemy in enemies)
            {
                if (enemy is not VineEnemy ve || !CanTarget(ve)) continue;
                float dist = GlobalPosition.DistanceTo(ve.GlobalPosition);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = ve;
                }
            }

            // Turn onto the target first: shots left the side of a barrel still swinging round
            if (closest != null && _look != null && !_look.ReadyToFire(closest)) return;

            if (closest != null)
            {
                // Damage per shot comes from the base interval, so firing faster adds damage
                // (from the boosted interval, a faster tower split the same damage over more shots)
                float dmg = GetEffectiveDamage(DamageTowerBaseInterval);
                closest.TakeDamage(dmg, HitKind);

                // S5: Apply on-hit effects from slotted components
                ApplyOnHitEffects(closest);

                // VFX: muzzle flash at the barrel, projectile to target
                _look?.Track(closest);
                _look?.Fire();
                var muzzlePos = Muzzle();
                var shotColor = _signalBoosted ? new Color(1f, 0.9f, 0.3f) : new Color(1f, 0.7f, 0.2f);
                VfxFactory.SpawnMuzzleFlash(GetTree(), muzzlePos, closest.GlobalPosition - muzzlePos, shotColor);
                VfxFactory.SpawnProjectile(GetTree(), muzzlePos, closest.GlobalPosition,
                    _signalBoosted ? new Color(1f, 0.9f, 0.3f) : new Color(1f, 0.7f, 0.2f));
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
        private void Pierce(VineEnemy target, float dmg, Godot.Collections.Array<Node> enemies)
        {
            var dir = target.GlobalPosition - GlobalPosition;
            dir.Y = 0;
            if (dir.LengthSquared() < 0.0001f) return;
            dir = dir.Normalized();
            VineEnemy first = null, second = null;
            float firstAlong = float.MaxValue, secondAlong = float.MaxValue;
            foreach (var enemy in enemies)
            {
                if (enemy is not VineEnemy ve || ve == target || !CanTarget(ve)) continue;
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
        private float _slowPulseTimer;

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

            var enemies = GetTree().GetNodesInGroup(Constants.GROUP_VINE_ENEMY);
            bool anySlowed = false;
            VineEnemy nearest = null;
            float nearestDist = float.MaxValue;
            foreach (var enemy in enemies)
            {
                if (enemy is not VineEnemy ve || !CanTarget(ve)) continue; // tar can't reach flyers
                float dist = GlobalPosition.DistanceTo(ve.GlobalPosition);
                if (dist <= range)
                {
                    ve.ApplySlow(slowAmount, 0.5f);
                    // Napalm: the tar burns
                    if (napalm) ve.TakeDamage(Constants.NAPALM_DPS * dt * UpgradeDamageMult, DamageKind.Normal);
                    anySlowed = true;
                    if (dist < nearestDist) { nearestDist = dist; nearest = ve; }

                    // Relic: Flux Mandala — slow fields also reduce armor
                    if (ServiceLocator.TryGet<RelicManager>(out var rm2))
                    {
                        float armorReduce = rm2.GetStatMods().SlowFieldArmorReduction;
                        if (armorReduce > 0)
                            ve.ReduceArmor(armorReduce, 0.5f);
                    }
                }
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
            var enemies = GetTree().GetNodesInGroup(Constants.GROUP_VINE_ENEMY);
            VineEnemy target = null;
            float targetDist = range;

            // Find closest enemy as center of AoE (a ground blast: flyers are out of reach)
            foreach (var enemy in enemies)
            {
                if (enemy is not VineEnemy ve || !CanTarget(ve)) continue;
                float dist = GlobalPosition.DistanceTo(ve.GlobalPosition);
                if (dist < targetDist)
                {
                    targetDist = dist;
                    target = ve;
                }
            }

            if (target == null) return;
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
                    hits++;
                }
            }

            // VFX: explosion at target
            _look?.Track(target);
            _look?.Fire();
            var muzzle = Muzzle();
            VfxFactory.SpawnMuzzleFlash(GetTree(), muzzle, target.GlobalPosition - muzzle, new Color(1f, 0.55f, 0.2f), 1.8f);
            VfxFactory.SpawnExplosion(GetTree(), target.GlobalPosition + Vector3.Up * 0.3f, splashRadius, new Color(1f, 0.5f, 0.15f));

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
            var enemies = GetTree().GetNodesInGroup(Constants.GROUP_VINE_ENEMY);

            // Find primary target
            VineEnemy primary = null;
            float primaryDist = range;

            foreach (var enemy in enemies)
            {
                if (enemy is not VineEnemy ve || !CanTarget(ve)) continue;
                float dist = GlobalPosition.DistanceTo(ve.GlobalPosition);
                if (dist < primaryDist)
                {
                    primaryDist = dist;
                    primary = ve;
                }
            }

            if (primary == null) return;

            float dmg = GetEffectiveDamage(Constants.TESLA_COIL_INTERVAL);
            // Overload: a much heavier first strike that stops the target for a moment
            bool overload = HasBranch("overload");
            primary.TakeDamage(dmg * (overload ? Constants.OVERLOAD_MULT : 1f), HitKind);
            if (overload) primary.ApplyStun(Constants.OVERLOAD_STUN);

            // Chain to nearby enemies
            var chainColor = new Color(0.3f, 0.7f, 1f);
            _look?.Track(primary);
            _look?.Fire();
            VfxFactory.SpawnArc(GetTree(), Muzzle(0.5f), primary.GlobalPosition + Vector3.Up * 0.6f, chainColor);

            var hit = new HashSet<VineEnemy> { primary };
            var lastPos = primary.GlobalPosition;
            // Arc Conductor: more arcs, and each keeps more of the damage
            bool arc = Has("arc_conductor");
            int chains = Constants.TESLA_COIL_CHAIN_COUNT + (arc ? Constants.PERK_ARC_EXTRA_CHAINS : 0);

            // Relic: Arc Network synergy adds extra chains
            if (ServiceLocator.TryGet<RelicManager>(out var rm))
                chains += (int)rm.GetStatMods().BonusSignalPower; // Reuse signal power bonus for chains

            for (int c = 0; c < chains; c++)
            {
                VineEnemy nextTarget = null;
                float nextDist = Constants.TESLA_COIL_CHAIN_RANGE;

                foreach (var enemy in enemies)
                {
                    if (enemy is not VineEnemy ve || !CanTarget(ve) || hit.Contains(ve)) continue;
                    float dist = lastPos.DistanceTo(ve.GlobalPosition);
                    if (dist < nextDist)
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

            Rearm(ref _teslaTimer, GetEffectiveFireInterval(Constants.TESLA_COIL_INTERVAL));
            IsActive = true;
        }

        private float _flakTimer;
        private int _flakBurstCount;
        private void UpdateFlakBattery(float dt)
        {
            if (!_autoFireEnabled) return;

            _flakTimer -= dt;
            if (_flakTimer > 0) return;

            float range = GetEffectiveRange();
            var enemies = GetTree().GetNodesInGroup(Constants.GROUP_VINE_ENEMY);

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
            var inRange = new List<(VineEnemy e, float d)>();
            foreach (var enemy in enemies)
                if (enemy is VineEnemy le && CanTarget(le))
                {
                    float d = GlobalPosition.DistanceTo(le.GlobalPosition);
                    if (d <= range) inRange.Add((le, d));
                }
            if (inRange.Count == 0) return;
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
                    VfxFactory.SpawnTracer(GetTree(), Muzzle(), ve.GlobalPosition + Vector3.Up * 0.5f, flakColor, 45f);

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
            float force = Constants.PUSH_PULL_FORCE * (_signalBoosted ? Constants.TOWER_SIGNAL_BOOST : 1f) * UpgradeForceMult;
            bool repulsor = HasBranch("repulsor");
            bool stun = Has("hydraulic_stun"); // Hydraulic Stun
            int pushed = 0;
            VineEnemy nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var enemy in GetTree().GetNodesInGroup(Constants.GROUP_VINE_ENEMY))
            {
                if (enemy is not VineEnemy ve || !CanTarget(ve)) continue; // flyers are out of reach
                var away = ve.GlobalPosition - GlobalPosition;
                away.Y = 0;
                float dist = away.Length();
                if (dist > range || dist < 0.001f) continue;
                if (dist < nearestDist) { nearestDist = dist; nearest = ve; }

                // Full shove next to the ram, fading to nothing at the edge of range
                ve.ApplyKnockback(away / dist * force * (1f - dist / range));
                if (stun) ve.ApplyStun(Constants.PERK_STUN_DURATION);
                if (repulsor) ve.TakeDamage(Constants.REPULSOR_DAMAGE * UpgradeDamageMult, DamageKind.Heavy);
                ApplyOnHitEffects(ve);
                pushed++;
            }

            if (pushed == 0) return; // Hold the charge until something is in range

            _look?.Track(nearest);
            _look?.Fire();
            VfxFactory.SpawnAreaPulse(GetTree(), GlobalPosition, range, new Color(0.3f, 0.5f, 0.9f), 0.5f, 0.45f);
            if (nearest != null) VfxFactory.SpawnShove(GetTree(), Muzzle(0.4f), nearest.GlobalPosition - GlobalPosition);
            Rearm(ref _pushTimer, GetEffectiveFireInterval(Constants.PUSH_PULL_INTERVAL));
            IsActive = true;
        }

        // Overclock Relay — keeps adjacent attack towers buffed. (Only the signal path could
        // deliver buffs before, and signal chains are no longer buildable.)
        private float _buffPulseTimer;
        private void UpdateBuffEmitter(float dt)
        {
            if (!_autoFireEnabled) return;
            _buffPulseTimer -= dt;
            if (_buffPulseTimer > 0) return;
            _buffPulseTimer = Constants.BUFF_EMITTER_PULSE_INTERVAL;

            if (!ServiceLocator.TryGet<VineGrid>(out var grid)) return;
            int buffed = 0;
            // Relay Mesh: two cells out instead of next door
            int reach = Has("relay_mesh") ? Constants.PERK_RELAY_REACH : 1;
            for (int dx = -reach; dx <= reach; dx++)
            {
                for (int dy = -reach; dy <= reach; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var neighbor = grid.GetNode(GridPosition.X + dx, GridPosition.Y + dy);
                    if (neighbor?.Data == null || neighbor.IsDestroyed) continue;
                    if (neighbor.Data.Category != VineNodeCategory.Effect) continue;
                    if (neighbor.Data.Type is VineNodeType.BuffEmitter or VineNodeType.BarrierWall) continue;

                    neighbor.ReceiveBuff(Constants.BUFF_EMITTER_STRENGTH * UpgradeBuffMult);
                    VfxFactory.SpawnBuffMotes(GetTree(), neighbor.GlobalPosition, _baseColor.Lightened(0.3f));
                    buffed++;
                }
            }
            IsActive = buffed > 0;
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
            return range * UpgradeRangeMult;
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
            float mult = AttackRateMultiplier * (1f + _buffStrength * SignalTuningEditor.BuffDamageBonus) * UpgradeRateMult;
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

            // Live tuning / perks ("Overclocked Cores", meta damage perks) scale all towers.
            // SignalTuningEditor.DamageTowerDPS was modified by those perks but never read.
            dmg *= SignalTuningEditor.DamageTowerDPS / Constants.DAMAGE_TOWER_DPS;

            // Signal boost multiplier
            if (_signalBoosted)
                dmg *= Constants.TOWER_SIGNAL_BOOST;

            // Buff multiplier
            dmg *= 1f + _buffStrength * SignalTuningEditor.BuffDamageBonus;

            // Overclock component
            if (_slotSystem != null && _slotSystem.HasComponent(TowerComponentType.Overclock))
                dmg *= 1f + Constants.SLOT_OVERCLOCK_DAMAGE;

            // Adjacency synergies (e.g. Thermal Shock) — computed by TowerSlotSystem, was never applied
            if (_slotSystem != null)
                dmg *= 1f + _slotSystem.GetSynergyDamageBonus();

            // Territory conquest buff ("tower_damage_mult")
            dmg *= GameManager.Instance?.RunTowerDamageMult ?? 1f;

            // Relic: Entropic Lens — base damage modifier (tradeoff for 3x crits)
            if (ServiceLocator.TryGet<RelicManager>(out var rm))
            {
                var mods = rm.GetStatMods();
                if (mods.BaseDamageMult != 0)
                    dmg *= 1f + mods.BaseDamageMult;
            }

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
        public void TakeDamage(float amount)
        {
            if (!_hasHealth || IsDestroyed) return;

            NodeCurrentHealth -= amount;
            _damageFlashTimer = 0.1f;
            if (HasBranch("spiked")) SpikeBack();

            // Flash mesh white on hit
            if (_modelRoot != null)
                FlashModelRecursive(_modelRoot, true, Colors.White);
            else if (_mesh?.MaterialOverride is StandardMaterial3D mat)
            {
                mat.EmissionEnabled = true;
                mat.Emission = Colors.White;
                mat.EmissionEnergyMultiplier = 2f;
            }

            if (NodeCurrentHealth <= 0)
                DestroyNode();
        }

        private float _spikeCooldownUntil;

        /// <summary>Spiked walls: whatever is close enough to have hit it takes damage back.</summary>
        private void SpikeBack()
        {
            float now = Time.GetTicksMsec() / 1000f;
            if (now < _spikeCooldownUntil) return;
            _spikeCooldownUntil = now + 0.4f;
            foreach (var n in GetTree().GetNodesInGroup(Constants.GROUP_VINE_ENEMY))
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
                FlashModelRecursive(_modelRoot, true, _baseColor.Lightened(0.3f), glowingOnly: true);
            }
            else if (_mesh?.MaterialOverride is StandardMaterial3D mat)
            {
                mat.EmissionEnabled = true;
                mat.Emission = _baseColor.Lightened(0.4f);
                mat.EmissionEnergyMultiplier = 1f;
            }
        }

        private static void FlashModelRecursive(Node node, bool flash, Color flashColor, bool glowingOnly = false)
        {
            // Restore exactly on flash-off: resetting only the energy left a tower glowing the
            // last flash colour (white after every hit)
            if (flash) HitFlash.On(node, flashColor, glowingOnly ? 1.8f : 0.5f, glowingOnly);
            else HitFlash.Off(node);
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

        private void UpdateNodeHealthBar()
        {
            if (_nodeHealthBar == null || !_hasHealth) return;
            float pct = NodeMaxHealth > 0 ? Mathf.Clamp(NodeCurrentHealth / NodeMaxHealth, 0f, 1f) : 1f;
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
                    FlashModelRecursive(_modelRoot, false, _baseColor);
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
