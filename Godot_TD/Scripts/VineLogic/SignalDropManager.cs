using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Manages signal component drops from enemy kills.
    /// Drops are tower components that slot into towers for free.
    /// Listens to OnEnemyKilled, rolls for drops, manages player inventory.
    /// </summary>
    public partial class SignalDropManager : Node
    {
        // Signal drop components — the pool of droppable types
        private static readonly TowerComponentType[] DropPool = new[]
        {
            TowerComponentType.ProximityCharge,
            TowerComponentType.SwarmReactor,
            TowerComponentType.MotionPredictor,
            TowerComponentType.DamageRouter,
            TowerComponentType.PhaseInverter,
            TowerComponentType.CapacitorBank,
            TowerComponentType.RelayAmplifier,
            TowerComponentType.CrowdSurge,
            TowerComponentType.TimerOverdrive,
            TowerComponentType.LatchPlating
        };

        // Player's collected signal drops (not yet slotted)
        private readonly List<TowerComponentType> _inventory = new();
        public IReadOnlyList<TowerComponentType> Inventory => _inventory;

        // Drop chance: base 8%, +2% per wave cleared (caps at 25%)
        private const float BASE_DROP_CHANCE = 0.08f;
        private const float DROP_CHANCE_PER_WAVE = 0.02f;
        private const float MAX_DROP_CHANCE = 0.25f;
        private const int MAX_INVENTORY = 8;

        private RandomNumberGenerator _rng = new();
        private int _eventVersion;

        public override void _Ready()
        {
            _eventVersion = GameEvents.Version;
            GameEvents.OnEnemyKilled += OnEnemyKilled;
            ServiceLocator.Register(this);
            GD.Print($"[SignalDrop] Ready — {DropPool.Length} signal types in pool, max inventory {MAX_INVENTORY}");
        }

        public override void _ExitTree()
        {
            GameEvents.OnEnemyKilled -= OnEnemyKilled;
            ServiceLocator.Unregister<SignalDropManager>();
        }

        private void OnEnemyKilled(Node enemy)
        {
            if (GameEvents.Version != _eventVersion) return;
            if (_inventory.Count >= MAX_INVENTORY) return;

            int wave = GameManager.Instance?.CurrentWave ?? 0;
            float chance = Mathf.Min(BASE_DROP_CHANCE + wave * DROP_CHANCE_PER_WAVE, MAX_DROP_CHANCE);

            if (_rng.Randf() > chance) return;

            var drop = DropPool[_rng.RandiRange(0, DropPool.Length - 1)];
            _inventory.Add(drop);

            var data = TowerComponentRegistry.Get(drop);
            string name = data?.Name ?? drop.ToString();

            GD.Print($"[SignalDrop] Dropped: {name} (inventory: {_inventory.Count}/{MAX_INVENTORY})");

            // Fire event for HUD notification
            GameEvents.OnSignalDropped?.Invoke(drop);

            // Spawn world pickup VFX at enemy position
            if (enemy is Node3D enemy3D && IsInstanceValid(enemy3D))
            {
                var tree = GetTree();
                if (tree?.CurrentScene != null)
                    VfxFactory.SpawnScrapCollectPop(tree, enemy3D.GlobalPosition + Vector3.Up * 0.5f);
            }
        }

        /// <summary>
        /// Slot a signal drop from inventory into a tower.
        /// Returns true if successful.
        /// </summary>
        public bool ApplyDrop(int inventoryIndex, VineNode tower, int slotIndex)
        {
            if (inventoryIndex < 0 || inventoryIndex >= _inventory.Count) return false;

            var component = _inventory[inventoryIndex];
            var slotSystem = tower?.GetSlotSystem();
            if (slotSystem == null) return false;

            if (!slotSystem.SlotComponent(slotIndex, component)) return false;

            _inventory.RemoveAt(inventoryIndex);
            var data = TowerComponentRegistry.Get(component);
            GD.Print($"[SignalDrop] Applied {data?.Name ?? component.ToString()} to tower at ({tower.GridPosition.X},{tower.GridPosition.Y}) slot {slotIndex}");
            return true;
        }

        /// <summary>
        /// Discard a signal drop from inventory.
        /// </summary>
        public void DiscardDrop(int inventoryIndex)
        {
            if (inventoryIndex < 0 || inventoryIndex >= _inventory.Count) return;
            var data = TowerComponentRegistry.Get(_inventory[inventoryIndex]);
            GD.Print($"[SignalDrop] Discarded: {data?.Name}");
            _inventory.RemoveAt(inventoryIndex);
        }
    }
}
