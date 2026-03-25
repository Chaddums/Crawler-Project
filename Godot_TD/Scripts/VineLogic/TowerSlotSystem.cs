using System;
using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Component data for a slottable tower component.
    /// Defined in the static registry — not per-instance.
    /// </summary>
    public class TowerComponentData
    {
        public TowerComponentType Type;
        public TowerSlotType SlotType;   // Which slot this goes into
        public string Name;
        public string Description;
        public int ResourceCost;
        public Color TintColor;
    }

    /// <summary>
    /// Per-tower slot system. Manages which components are slotted,
    /// applies stat modifiers, and detects adjacency synergies.
    /// Attached to VineNode via SetSlotSystem().
    /// </summary>
    public class TowerSlotSystem
    {
        private readonly VineNode _owner;
        private readonly TowerSlotType[] _slotTypes;
        private readonly TowerComponentType?[] _slottedComponents;

        // Cached adjacency synergies (recalculated when components change)
        private readonly List<SynergyEffect> _activeSynergies = new();

        public int SlotCount => _slotTypes.Length;
        public IReadOnlyList<SynergyEffect> ActiveSynergies => _activeSynergies;

        public TowerSlotSystem(VineNode owner, TowerSlotType[] slotTypes)
        {
            _owner = owner;
            _slotTypes = slotTypes ?? Array.Empty<TowerSlotType>();
            _slottedComponents = new TowerComponentType?[_slotTypes.Length];
        }

        /// <summary>
        /// Get the slot type at the given index.
        /// </summary>
        public TowerSlotType GetSlotType(int index)
        {
            if (index < 0 || index >= _slotTypes.Length)
                return TowerSlotType.Frame;
            return _slotTypes[index];
        }

        /// <summary>
        /// Get the component in the given slot (null if empty).
        /// </summary>
        public TowerComponentType? GetComponent(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slottedComponents.Length) return null;
            return _slottedComponents[slotIndex];
        }

        /// <summary>
        /// Check if a specific component type is slotted anywhere.
        /// </summary>
        public bool HasComponent(TowerComponentType type)
        {
            for (int i = 0; i < _slottedComponents.Length; i++)
                if (_slottedComponents[i] == type) return true;
            return false;
        }

        /// <summary>
        /// Slot a component. Returns false if slot is wrong type or occupied.
        /// </summary>
        public bool SlotComponent(int slotIndex, TowerComponentType component)
        {
            if (slotIndex < 0 || slotIndex >= _slotTypes.Length) return false;
            if (_slottedComponents[slotIndex] != null) return false;

            var compData = TowerComponentRegistry.Get(component);
            if (compData == null) return false;
            if (compData.SlotType != _slotTypes[slotIndex]) return false;

            _slottedComponents[slotIndex] = component;

            // Apply HP modifier immediately for frame components
            if (component == TowerComponentType.HeavyPlating)
            {
                _owner.NodeMaxHealth *= 1f + Constants.SLOT_HEAVY_PLATING;
                // Heal to new max (placed fresh with component)
            }
            else if (component == TowerComponentType.Overclock)
            {
                _owner.NodeMaxHealth *= 1f - Constants.SLOT_OVERCLOCK_HP_COST;
            }

            GameEvents.OnComponentSlotted?.Invoke(_owner, component, _slotTypes[slotIndex]);
            RecalculateSynergies();
            return true;
        }

        /// <summary>
        /// Remove a component from a slot. Returns the removed component type.
        /// </summary>
        public TowerComponentType? RemoveComponent(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slottedComponents.Length) return null;
            var removed = _slottedComponents[slotIndex];
            if (removed == null) return null;

            _slottedComponents[slotIndex] = null;
            GameEvents.OnComponentRemoved?.Invoke(_owner, removed.Value);
            RecalculateSynergies();
            return removed;
        }

        /// <summary>
        /// Check adjacent towers for synergy combos.
        /// Called when components change on this tower or neighbors.
        /// </summary>
        public void RecalculateSynergies()
        {
            _activeSynergies.Clear();

            if (!ServiceLocator.TryGet<VineGrid>(out var grid)) return;

            // Gather neighbor tower slot systems
            var neighborSystems = new List<TowerSlotSystem>();
            foreach (var neighborPos in _owner.ConnectedCells)
            {
                var neighborNode = grid.GetNode(neighborPos);
                var neighborSlots = neighborNode?.GetSlotSystem();
                if (neighborSlots != null)
                    neighborSystems.Add(neighborSlots);
            }

            // Also check grid-adjacent (not just vine-connected) for proximity synergies
            var dirs = new[] {
                new Vector2I(1, 0), new Vector2I(-1, 0),
                new Vector2I(0, 1), new Vector2I(0, -1)
            };
            foreach (var dir in dirs)
            {
                var adjPos = _owner.GridPosition + dir;
                var adjNode = grid.GetNode(adjPos);
                var adjSlots = adjNode?.GetSlotSystem();
                if (adjSlots != null && !neighborSystems.Contains(adjSlots))
                    neighborSystems.Add(adjSlots);
            }

            // Check each synergy condition
            foreach (var neighbor in neighborSystems)
            {
                // Thermal Shock: this has CryoBolt + neighbor has IncendiaryRound (or vice versa)
                if ((HasComponent(TowerComponentType.CryoBolt) && neighbor.HasComponent(TowerComponentType.IncendiaryRound)) ||
                    (HasComponent(TowerComponentType.IncendiaryRound) && neighbor.HasComponent(TowerComponentType.CryoBolt)))
                {
                    _activeSynergies.Add(new SynergyEffect {
                        Name = "Thermal Shock",
                        Description = $"+{Constants.SYNERGY_THERMAL_SHOCK_BONUS * 100:0}% damage to both towers",
                        DamageBonus = Constants.SYNERGY_THERMAL_SHOCK_BONUS
                    });
                }

                // Arc Network: both have ChainArc
                if (HasComponent(TowerComponentType.ChainArc) && neighbor.HasComponent(TowerComponentType.ChainArc))
                {
                    _activeSynergies.Add(new SynergyEffect {
                        Name = "Arc Network",
                        Description = "Chain arcs jump between towers",
                        ExtraChainBounces = (int)Constants.SYNERGY_ARC_NETWORK_BOUNCES
                    });
                }

                // Suppression Field: both have RapidFire
                if (HasComponent(TowerComponentType.RapidFire) && neighbor.HasComponent(TowerComponentType.RapidFire))
                {
                    _activeSynergies.Add(new SynergyEffect {
                        Name = "Suppression Field",
                        Description = "Enemies in overlap area are slowed",
                        SlowAmount = Constants.SYNERGY_SUPPRESSION_SLOW
                    });
                }
            }

            // Fire synergy events
            if (_activeSynergies.Count > 0)
            {
                foreach (var syn in _activeSynergies)
                    GameEvents.OnSynergyActivated?.Invoke(_owner, null, syn.Name);
            }
        }

        /// <summary>
        /// Get total synergy damage bonus from all active synergies.
        /// </summary>
        public float GetSynergyDamageBonus()
        {
            float bonus = 0f;
            foreach (var syn in _activeSynergies)
                bonus += syn.DamageBonus;
            return bonus;
        }
    }

    /// <summary>
    /// A synergy effect created by adjacent tower component combos.
    /// </summary>
    public struct SynergyEffect
    {
        public string Name;
        public string Description;
        public float DamageBonus;
        public float SlowAmount;
        public int ExtraChainBounces;
    }

    /// <summary>
    /// Registry of all slottable tower components.
    /// </summary>
    public static class TowerComponentRegistry
    {
        private static readonly Dictionary<TowerComponentType, TowerComponentData> _components = new();
        private static bool _initialized;

        public static TowerComponentData Get(TowerComponentType type)
        {
            EnsureInit();
            return _components.TryGetValue(type, out var data) ? data : null;
        }

        public static IEnumerable<TowerComponentData> GetAll()
        {
            EnsureInit();
            return _components.Values;
        }

        public static IEnumerable<TowerComponentData> GetBySlot(TowerSlotType slotType)
        {
            EnsureInit();
            foreach (var data in _components.Values)
                if (data.SlotType == slotType) yield return data;
        }

        private static void EnsureInit()
        {
            if (_initialized) return;
            _initialized = true;

            // ── Barrel components ──

            Register(new TowerComponentData {
                Type = TowerComponentType.ChainArc, SlotType = TowerSlotType.Barrel,
                Name = "Chain Arc", Description = "Shots bounce to nearby enemies.",
                ResourceCost = Constants.TOWER_COMPONENT_COST,
                TintColor = new Color(0.3f, 0.7f, 1f)
            });

            Register(new TowerComponentData {
                Type = TowerComponentType.ScatterShot, SlotType = TowerSlotType.Barrel,
                Name = "Scatter Shot", Description = "Shots deal AoE splash damage.",
                ResourceCost = Constants.TOWER_COMPONENT_COST,
                TintColor = new Color(0.9f, 0.6f, 0.2f)
            });

            Register(new TowerComponentData {
                Type = TowerComponentType.PiercingRound, SlotType = TowerSlotType.Barrel,
                Name = "Piercing Round", Description = "Shots pass through enemies.",
                ResourceCost = Constants.TOWER_COMPONENT_COST,
                TintColor = new Color(0.8f, 0.8f, 0.3f)
            });

            Register(new TowerComponentData {
                Type = TowerComponentType.CryoBolt, SlotType = TowerSlotType.Barrel,
                Name = "Cryo Bolt", Description = "Shots slow enemies on hit.",
                ResourceCost = Constants.TOWER_COMPONENT_COST,
                TintColor = new Color(0.3f, 0.6f, 0.9f)
            });

            Register(new TowerComponentData {
                Type = TowerComponentType.IncendiaryRound, SlotType = TowerSlotType.Barrel,
                Name = "Incendiary Round", Description = "Shots apply burn DoT.",
                ResourceCost = Constants.TOWER_COMPONENT_COST,
                TintColor = new Color(1f, 0.4f, 0.1f)
            });

            // ── Core components ──

            Register(new TowerComponentData {
                Type = TowerComponentType.PriorityWeak, SlotType = TowerSlotType.Core,
                Name = "Weakpoint Scanner", Description = "Target lowest HP enemy first.",
                ResourceCost = Constants.TOWER_COMPONENT_COST,
                TintColor = new Color(0.9f, 0.3f, 0.3f)
            });

            Register(new TowerComponentData {
                Type = TowerComponentType.PriorityFast, SlotType = TowerSlotType.Core,
                Name = "Motion Tracker", Description = "Target fastest enemy first.",
                ResourceCost = Constants.TOWER_COMPONENT_COST,
                TintColor = new Color(0.3f, 0.9f, 0.5f)
            });

            Register(new TowerComponentData {
                Type = TowerComponentType.PriorityFar, SlotType = TowerSlotType.Core,
                Name = "Path Predictor", Description = "Target enemy farthest along path.",
                ResourceCost = Constants.TOWER_COMPONENT_COST,
                TintColor = new Color(0.7f, 0.5f, 0.9f)
            });

            Register(new TowerComponentData {
                Type = TowerComponentType.FocusFire, SlotType = TowerSlotType.Core,
                Name = "Lock-On Module", Description = "Lock onto target until dead.",
                ResourceCost = Constants.TOWER_COMPONENT_COST,
                TintColor = new Color(1f, 0.2f, 0.2f)
            });

            // ── Frame components ──

            Register(new TowerComponentData {
                Type = TowerComponentType.ExtendedRange, SlotType = TowerSlotType.Frame,
                Name = "Signal Booster", Description = "+30% range.",
                ResourceCost = Constants.TOWER_COMPONENT_COST,
                TintColor = new Color(0.4f, 0.8f, 0.4f)
            });

            Register(new TowerComponentData {
                Type = TowerComponentType.RapidFire, SlotType = TowerSlotType.Frame,
                Name = "Overcrank Spring", Description = "+25% attack speed.",
                ResourceCost = Constants.TOWER_COMPONENT_COST,
                TintColor = new Color(0.8f, 0.8f, 0.2f)
            });

            Register(new TowerComponentData {
                Type = TowerComponentType.HeavyPlating, SlotType = TowerSlotType.Frame,
                Name = "Reinforced Plating", Description = "+50% tower HP.",
                ResourceCost = Constants.TOWER_COMPONENT_COST,
                TintColor = new Color(0.5f, 0.5f, 0.6f)
            });

            Register(new TowerComponentData {
                Type = TowerComponentType.Overclock, SlotType = TowerSlotType.Frame,
                Name = "Overclock Module", Description = "+15% damage, -10% HP.",
                ResourceCost = Constants.TOWER_COMPONENT_COST,
                TintColor = new Color(1f, 0.6f, 0.1f)
            });

            // ── Signal drops (from enemy kills) ──

            Register(new TowerComponentData {
                Type = TowerComponentType.ProximityCharge, SlotType = TowerSlotType.Barrel,
                Name = "Proximity Charge", Description = "+40% damage to enemies within half range.",
                ResourceCost = 0, // Drops are free to slot
                TintColor = new Color(0.1f, 0.55f, 0.65f)
            });

            Register(new TowerComponentData {
                Type = TowerComponentType.SwarmReactor, SlotType = TowerSlotType.Barrel,
                Name = "Swarm Reactor", Description = "+10% damage per enemy in range.",
                ResourceCost = 0,
                TintColor = new Color(0.1f, 0.6f, 0.6f)
            });

            Register(new TowerComponentData {
                Type = TowerComponentType.MotionPredictor, SlotType = TowerSlotType.Core,
                Name = "Motion Predictor", Description = "Leads shots — never misses fast enemies.",
                ResourceCost = 0,
                TintColor = new Color(0.3f, 0.9f, 0.5f)
            });

            Register(new TowerComponentData {
                Type = TowerComponentType.DamageRouter, SlotType = TowerSlotType.Core,
                Name = "Damage Router", Description = "Prioritize most-damaged enemy in range.",
                ResourceCost = 0,
                TintColor = new Color(0.9f, 0.3f, 0.3f)
            });

            Register(new TowerComponentData {
                Type = TowerComponentType.PhaseInverter, SlotType = TowerSlotType.Core,
                Name = "Phase Inverter", Description = "Damage type flips to counter-element.",
                ResourceCost = 0,
                TintColor = new Color(0.3f, 0.4f, 0.7f)
            });

            Register(new TowerComponentData {
                Type = TowerComponentType.CapacitorBank, SlotType = TowerSlotType.Frame,
                Name = "Capacitor Bank", Description = "Every 4th shot deals 3x damage.",
                ResourceCost = 0,
                TintColor = new Color(0.25f, 0.4f, 0.65f)
            });

            Register(new TowerComponentData {
                Type = TowerComponentType.RelayAmplifier, SlotType = TowerSlotType.Frame,
                Name = "Relay Amplifier", Description = "+15% damage and range to adjacent towers too.",
                ResourceCost = 0,
                TintColor = new Color(0.4f, 0.8f, 0.4f)
            });

            Register(new TowerComponentData {
                Type = TowerComponentType.CrowdSurge, SlotType = TowerSlotType.Frame,
                Name = "Crowd Surge", Description = "+50% fire rate when 5+ enemies in range.",
                ResourceCost = 0,
                TintColor = new Color(0.1f, 0.6f, 0.6f)
            });

            Register(new TowerComponentData {
                Type = TowerComponentType.TimerOverdrive, SlotType = TowerSlotType.Frame,
                Name = "Timer Overdrive", Description = "All cooldowns reduced 20%.",
                ResourceCost = 0,
                TintColor = new Color(0.15f, 0.5f, 0.65f)
            });

            Register(new TowerComponentData {
                Type = TowerComponentType.LatchPlating, SlotType = TowerSlotType.Frame,
                Name = "Latch Plating", Description = "First hit each wave is fully absorbed.",
                ResourceCost = 0,
                TintColor = new Color(0.3f, 0.45f, 0.7f)
            });
        }

        private static void Register(TowerComponentData data)
        {
            _components[data.Type] = data;
        }
    }
}
