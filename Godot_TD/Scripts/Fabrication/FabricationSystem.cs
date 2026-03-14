using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Fabrication Combo system (Pillar #2).
    /// Players combine mod components to enhance towers.
    /// Components drop from enemies or are purchased with refined scrap.
    /// </summary>
    public partial class FabricationSystem : Node
    {
        // Component inventory — how many of each the player has
        private readonly Dictionary<ModComponentType, int> _components = new();

        // Combo recipes: combining two components yields a new effect
        private static readonly Dictionary<(ModComponentType, ModComponentType), string> _combos = new()
        {
            { (ModComponentType.Gyroscope, ModComponentType.HeatCoil), "Tracking Incendiary" },
            { (ModComponentType.Gyroscope, ModComponentType.CryoCell), "Homing Frost" },
            { (ModComponentType.HeatCoil, ModComponentType.CryoCell), "Thermal Shock" },
            { (ModComponentType.ChargeCapacitor, ModComponentType.SplitPrism), "Scatter Cannon" },
            { (ModComponentType.OverclockModule, ModComponentType.HeatCoil), "Meltdown" },
            { (ModComponentType.RangeExtender, ModComponentType.Gyroscope), "Eagle Eye" },
            { (ModComponentType.SalvageHopper, ModComponentType.OverclockModule), "Scrap Frenzy" },
        };

        public override void _Ready()
        {
            ServiceLocator.Register(this);

            // Start with nothing — earn through gameplay
            foreach (ModComponentType type in System.Enum.GetValues(typeof(ModComponentType)))
                _components[type] = 0;
        }

        public void AddComponent(ModComponentType type, int count = 1)
        {
            _components[type] = _components.GetValueOrDefault(type) + count;
        }

        public int GetComponentCount(ModComponentType type)
        {
            return _components.GetValueOrDefault(type);
        }

        public bool HasComponent(ModComponentType type)
        {
            return _components.GetValueOrDefault(type) > 0;
        }

        /// <summary>
        /// Attach a mod component to a tower, consuming it from inventory.
        /// </summary>
        public bool TryAttachMod(TowerController tower, ModComponentType mod)
        {
            if (!tower.CanAttachMod()) return false;
            if (!HasComponent(mod)) return false;

            _components[mod]--;
            tower.AttachMod(mod);
            GameEvents.OnFabricationComplete?.Invoke(tower, mod);
            return true;
        }

        /// <summary>
        /// Check what combo name (if any) would result from two components.
        /// </summary>
        public static string GetComboName(ModComponentType a, ModComponentType b)
        {
            if (_combos.TryGetValue((a, b), out var name)) return name;
            if (_combos.TryGetValue((b, a), out name)) return name;
            return null;
        }

        public override void _ExitTree()
        {
            ServiceLocator.Unregister<FabricationSystem>();
        }
    }
}
