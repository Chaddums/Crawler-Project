using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Static data for a vine node type.
    /// </summary>
    public class VineNodeData
    {
        public string Id;
        public string Name;
        public string Description;
        public VineNodeType Type;
        public VineNodeCategory Category;
        public int ResourceCost;
        public int MaxConnections;      // Max vine connections this node supports
        public bool BlocksPath;         // Whether enemies can't walk through this node
        public bool HasDynamicRouting;   // Whether this node affects enemy pathing (gates, switches)
        public Color TintColor;

        // Type-specific defaults
        public float Range;             // For sensors and effect nodes
        public float Damage;            // For damage towers
        public float Interval;          // For timers, delays
        public float SlowAmount;        // For slow fields
        public int RequiredInputs;      // For gates (AND: 2+)
        public int SignalPower;         // How many effect nodes this sensor can activate (0 = passthrough)
    }

    /// <summary>
    /// Registry of all vine node type definitions.
    /// </summary>
    public static class VineNodeRegistry
    {
        private static readonly Dictionary<VineNodeType, VineNodeData> _nodes = new();
        private static bool _initialized;

        public static VineNodeData Get(VineNodeType type)
        {
            EnsureInit();
            return _nodes.TryGetValue(type, out var data) ? data : null;
        }

        public static IEnumerable<VineNodeData> GetAll()
        {
            EnsureInit();
            return _nodes.Values;
        }

        public static IEnumerable<VineNodeData> GetByCategory(VineNodeCategory cat)
        {
            EnsureInit();
            foreach (var data in _nodes.Values)
                if (data.Category == cat) yield return data;
        }

        private static void EnsureInit()
        {
            if (_initialized) return;
            _initialized = true;

            // ── Structural / Routing ──

            Register(new VineNodeData {
                Id = "extender", Name = "Cable Splice",
                Description = "Rusted cable segment. Carries signals, nothing else.",
                Type = VineNodeType.Extender, Category = VineNodeCategory.Structural,
                ResourceCost = 3, MaxConnections = 2, BlocksPath = false,
                TintColor = new Color(0.25f, 0.45f, 0.6f)
            });

            Register(new VineNodeData {
                Id = "junction", Name = "Junction Box",
                Description = "Splits signal to all outputs. More connections, more chaos.",
                Type = VineNodeType.Junction, Category = VineNodeCategory.Structural,
                ResourceCost = 8, MaxConnections = 4, BlocksPath = false,
                TintColor = new Color(0.3f, 0.5f, 0.65f)
            });

            Register(new VineNodeData {
                Id = "switch", Name = "Rail Switch",
                Description = "Toggles enemy route between two outputs. Timer or signal-driven.",
                Type = VineNodeType.Switch, Category = VineNodeCategory.Structural,
                ResourceCost = 12, MaxConnections = 3, BlocksPath = false,
                HasDynamicRouting = true, Interval = Constants.SWITCH_TOGGLE_TIME,
                TintColor = new Color(0.35f, 0.55f, 0.7f)
            });

            Register(new VineNodeData {
                Id = "gate", Name = "Pneumatic Gate",
                Description = "Only opens when 2+ inputs fire simultaneously. Enemies bunch at closed gates.",
                Type = VineNodeType.Gate, Category = VineNodeCategory.Structural,
                ResourceCost = 15, MaxConnections = 4, BlocksPath = false,
                HasDynamicRouting = true, RequiredInputs = 2,
                TintColor = new Color(0.2f, 0.5f, 0.6f)
            });

            Register(new VineNodeData {
                Id = "inverter", Name = "Phase Inverter",
                Description = "Flips signal state. Active becomes inactive, and vice versa.",
                Type = VineNodeType.Inverter, Category = VineNodeCategory.Structural,
                ResourceCost = 6, MaxConnections = 2, BlocksPath = false,
                TintColor = new Color(0.3f, 0.4f, 0.7f)
            });

            Register(new VineNodeData {
                Id = "delay", Name = "Capacitor Bank",
                Description = "Holds a signal for a few seconds before passing it on.",
                Type = VineNodeType.Delay, Category = VineNodeCategory.Structural,
                ResourceCost = 8, MaxConnections = 2, BlocksPath = false,
                Interval = Constants.DELAY_DURATION,
                TintColor = new Color(0.25f, 0.4f, 0.65f)
            });

            Register(new VineNodeData {
                Id = "latch", Name = "Relay Latch",
                Description = "Stays open after first trigger. Only a reset signal closes it.",
                Type = VineNodeType.Latch, Category = VineNodeCategory.Structural,
                ResourceCost = 10, MaxConnections = 3, BlocksPath = false,
                HasDynamicRouting = true,
                TintColor = new Color(0.3f, 0.45f, 0.7f)
            });

            // ── Sensors ──

            Register(new VineNodeData {
                Id = "proximity_sensor", Name = "Motion Detector",
                Description = "Busted motion sensor. Fires when anything moves nearby. Powers up to 3 nodes.",
                Type = VineNodeType.ProximitySensor, Category = VineNodeCategory.Sensor,
                ResourceCost = 5, MaxConnections = 2, BlocksPath = true,
                Range = Constants.SENSOR_RANGE, SignalPower = 3,
                TintColor = new Color(0.1f, 0.55f, 0.65f)
            });

            Register(new VineNodeData {
                Id = "type_sensor", Name = "IFF Scanner",
                Description = "Identifies specific enemy types. Only fires for the configured target. Powers up to 3 nodes.",
                Type = VineNodeType.TypeSensor, Category = VineNodeCategory.Sensor,
                ResourceCost = 8, MaxConnections = 2, BlocksPath = true,
                Range = Constants.SENSOR_RANGE, SignalPower = 3,
                TintColor = new Color(0.1f, 0.5f, 0.7f)
            });

            Register(new VineNodeData {
                Id = "hp_sensor", Name = "Damage Gauge",
                Description = "Fires when a wounded enemy passes. The more hurt, the louder. Powers up to 3 nodes.",
                Type = VineNodeType.HPSensor, Category = VineNodeCategory.Sensor,
                ResourceCost = 7, MaxConnections = 2, BlocksPath = true,
                Range = Constants.SENSOR_RANGE, SignalPower = 3,
                TintColor = new Color(0.15f, 0.45f, 0.7f)
            });

            Register(new VineNodeData {
                Id = "count_sensor", Name = "Crowd Counter",
                Description = "Fires when enough enemies bunch up in range. Patience pays off. Powers up to 4 nodes.",
                Type = VineNodeType.CountSensor, Category = VineNodeCategory.Sensor,
                ResourceCost = 10, MaxConnections = 2, BlocksPath = true,
                Range = Constants.SENSOR_RANGE, RequiredInputs = 5, SignalPower = 4,
                TintColor = new Color(0.1f, 0.6f, 0.6f)
            });

            Register(new VineNodeData {
                Id = "timer", Name = "Crank Timer",
                Description = "Fires on a fixed interval. No input needed — it just ticks. Powers up to 4 nodes.",
                Type = VineNodeType.Timer, Category = VineNodeCategory.Sensor,
                ResourceCost = 6, MaxConnections = 2, BlocksPath = true,
                Interval = Constants.TIMER_INTERVAL, SignalPower = 4,
                TintColor = new Color(0.15f, 0.5f, 0.65f)
            });

            // ── Effect / Output ──

            Register(new VineNodeData {
                Id = "damage_tower", Name = "Junk Turret",
                Description = "Shoots enemies in range — but only when it receives a signal.",
                Type = VineNodeType.DamageTower, Category = VineNodeCategory.Effect,
                ResourceCost = 15, MaxConnections = 2, BlocksPath = true,
                Range = Constants.DAMAGE_TOWER_RANGE, Damage = Constants.DAMAGE_TOWER_DPS,
                TintColor = new Color(0.15f, 0.35f, 0.75f)
            });

            Register(new VineNodeData {
                Id = "slow_field", Name = "Tar Sprayer",
                Description = "Coats the path in gunk. Enemies slog through it.",
                Type = VineNodeType.SlowField, Category = VineNodeCategory.Effect,
                ResourceCost = 10, MaxConnections = 2, BlocksPath = false,
                Range = Constants.SLOW_FIELD_RANGE, SlowAmount = Constants.SLOW_FIELD_AMOUNT,
                TintColor = new Color(0.2f, 0.3f, 0.7f)
            });

            Register(new VineNodeData {
                Id = "push_pull", Name = "Pneumatic Ram",
                Description = "Shoves enemies sideways when signaled. Great for redirects.",
                Type = VineNodeType.PushPull, Category = VineNodeCategory.Effect,
                ResourceCost = 12, MaxConnections = 2, BlocksPath = true,
                Range = Constants.SENSOR_RANGE,
                TintColor = new Color(0.2f, 0.4f, 0.75f)
            });

            Register(new VineNodeData {
                Id = "loop_anchor", Name = "Routing Magnet",
                Description = "Enemies loop through this section until the anchor is deactivated.",
                Type = VineNodeType.LoopAnchor, Category = VineNodeCategory.Effect,
                ResourceCost = 14, MaxConnections = 2, BlocksPath = false,
                HasDynamicRouting = true,
                TintColor = new Color(0.25f, 0.35f, 0.8f)
            });

            Register(new VineNodeData {
                Id = "buff_emitter", Name = "Overclock Relay",
                Description = "Sends a buff pulse through the vine. Connected towers hit harder.",
                Type = VineNodeType.BuffEmitter, Category = VineNodeCategory.Effect,
                ResourceCost = 14, MaxConnections = 3, BlocksPath = true,
                TintColor = new Color(0.1f, 0.45f, 0.7f)
            });

            Register(new VineNodeData {
                Id = "signal_cannon", Name = "Manual Trigger",
                Description = "Press to fire a signal. For when you need a human in the loop.",
                Type = VineNodeType.SignalCannon, Category = VineNodeCategory.Effect,
                ResourceCost = 5, MaxConnections = 2, BlocksPath = true,
                TintColor = new Color(0.2f, 0.5f, 0.65f)
            });
        }

        private static void Register(VineNodeData data)
        {
            _nodes[data.Type] = data;
        }
    }
}
