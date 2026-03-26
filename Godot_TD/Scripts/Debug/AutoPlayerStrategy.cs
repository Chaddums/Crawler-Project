using System;
using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Interface for AutoPlayer placement/combat strategies.
    /// Each strategy plays the game differently to test different systems.
    /// </summary>
    public interface IAutoPlayerStrategy
    {
        string Name { get; }
        void OnBuildPhase(VineGrid grid, int currentResources, int waveNumber);
        void OnWavePhase(float dt); // Called each frame during waves
        void OnMilestone(int wave, string type);
    }

    /// <summary>
    /// Helper for strategies to place nodes via the VineGrid API.
    /// </summary>
    public static class AutoPlaceHelper
    {
        /// <summary>
        /// Place a node of the given type at (x, y). Handles VineNode creation.
        /// Returns true if placed successfully.
        /// </summary>
        public static bool PlaceAt(VineGrid grid, int x, int y, VineNodeType type)
        {
            if (!grid.CanPlace(x, y)) return false;
            var data = VineNodeRegistry.Get(type);
            if (data == null) return false;

            var node = new VineNode();
            node.Initialize(data);
            return grid.PlaceNode(node, new Vector2I(x, y));
        }
    }

    /// <summary>
    /// Registry of all built-in strategies.
    /// </summary>
    public static class StrategyRegistry
    {
        private static readonly Dictionary<string, Func<IAutoPlayerStrategy>> _strategies = new() {
            { "TurretSpam", () => new TurretSpamStrategy() },
            { "MazeBuilder", () => new MazeBuilderStrategy() },
            { "MixedDefense", () => new MixedDefenseStrategy() },
            { "RandomPlacement", () => new RandomPlacementStrategy() },
            { "EconomyFocus", () => new EconomyFocusStrategy() },
            { "RushDefense", () => new RushDefenseStrategy() },
            { "SlotExplorer", () => new SlotExplorerStrategy() },
            { "DoNothing", () => new DoNothingStrategy() },
        };

        public static IAutoPlayerStrategy Create(string name)
        {
            return _strategies.TryGetValue(name, out var factory) ? factory() : new TurretSpamStrategy();
        }

        public static IEnumerable<string> AllNames => _strategies.Keys;
    }

    // ═══════════════════════════════════════════════════════════════
    // Built-in Strategies
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Fill grid with DamageTowers. No routing, no signals. Tests raw tower DPS.
    /// </summary>
    public class TurretSpamStrategy : IAutoPlayerStrategy
    {
        public string Name => "TurretSpam";

        public void OnBuildPhase(VineGrid grid, int currentResources, int waveNumber)
        {
            var turretData = VineNodeRegistry.Get(VineNodeType.DamageTower);
            if (turretData == null) return;

            // Place turrets along the enemy path (west entry → spire), NOT around the spire.
            // Line them along the horizontal midline, spaced apart to avoid blocking the path entirely.
            int placed = 0;
            int cy = grid.Height / 2;

            // Stagger placements: even waves place above path, odd below
            int yOffset = waveNumber % 2 == 0 ? -2 : 2;
            int startX = 3 + (waveNumber * 2) % (grid.Width - 6);

            for (int x = startX; x < grid.Width - 2 && placed < 3 && currentResources >= turretData.ResourceCost; x += 3)
            {
                int y = cy + yOffset;
                if (grid.CanPlace(x, y))
                {
                    if (AutoPlaceHelper.PlaceAt(grid, x, y, VineNodeType.DamageTower))
                    {
                        currentResources -= turretData.ResourceCost;
                        placed++;
                        GD.Print($"[TurretSpam] Placed turret at ({x},{y}) — {placed} this phase");
                    }
                }
            }
        }

        public void OnWavePhase(float dt) { }
        public void OnMilestone(int wave, string type) { }
    }

    /// <summary>
    /// Build walls to create a maze, with towers at chokepoints.
    /// Tests pathfinding with BarrierWalls.
    /// </summary>
    public class MazeBuilderStrategy : IAutoPlayerStrategy
    {
        public string Name => "MazeBuilder";
        private int _buildStep;

        public void OnBuildPhase(VineGrid grid, int currentResources, int waveNumber)
        {
            // Alternate between walls and towers
            var nodeType = _buildStep % 3 == 2 ? VineNodeType.DamageTower : VineNodeType.BarrierWall;
            var data = VineNodeRegistry.Get(nodeType);
            if (data == null) return;

            // Build a zigzag pattern from entries toward center
            int y = (_buildStep * 2) % grid.Height;
            int xStart = _buildStep % 2 == 0 ? 2 : grid.Width - 3;
            int xEnd = _buildStep % 2 == 0 ? grid.Width - 3 : 2;
            int xStep = xStart < xEnd ? 1 : -1;

            int placed = 0;
            for (int x = xStart; x != xEnd && placed < 3 && currentResources >= data.ResourceCost; x += xStep)
            {
                if (grid.CanPlace(x, y))
                {
                    AutoPlaceHelper.PlaceAt(grid, x, y, nodeType);
                    currentResources -= data.ResourceCost;
                    placed++;
                }
            }
            _buildStep++;
        }

        public void OnWavePhase(float dt) { }
        public void OnMilestone(int wave, string type) { }
    }

    /// <summary>
    /// Mixed tower composition: turrets + slow + Tesla + scatter.
    /// Tests tower diversity and synergies.
    /// </summary>
    public class MixedDefenseStrategy : IAutoPlayerStrategy
    {
        public string Name => "MixedDefense";
        private int _placeCount;

        // Cycle through tower types for variety
        private static readonly VineNodeType[] TowerCycle = {
            VineNodeType.DamageTower,
            VineNodeType.SlowField,
            VineNodeType.TeslaCoil,
            VineNodeType.ScatterCannon,
            VineNodeType.FlakBattery,
            VineNodeType.BuffEmitter,
        };

        public void OnBuildPhase(VineGrid grid, int currentResources, int waveNumber)
        {
            var nodeType = TowerCycle[_placeCount % TowerCycle.Length];
            var data = VineNodeRegistry.Get(nodeType);
            if (data == null || currentResources < data.ResourceCost) return;

            // Place near center, expanding outward
            int cx = grid.Width / 2, cy = grid.Height / 2;
            for (int r = 2; r < 12; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dy = -r; dy <= r; dy++)
                    {
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;
                        int x = cx + dx, y = cy + dy;
                        if (grid.CanPlace(x, y))
                        {
                            AutoPlaceHelper.PlaceAt(grid, x, y, nodeType);
                            _placeCount++;
                            return;
                        }
                    }
                }
            }
        }

        public void OnWavePhase(float dt) { }
        public void OnMilestone(int wave, string type) { }
    }

    /// <summary>
    /// Place random valid nodes in random valid cells.
    /// Tests edge cases and crash resistance.
    /// </summary>
    public class RandomPlacementStrategy : IAutoPlayerStrategy
    {
        public string Name => "RandomPlacement";
        private readonly Random _rng = new();

        // Only buildable tower types
        private static readonly VineNodeType[] BuildableTypes = {
            VineNodeType.DamageTower, VineNodeType.SlowField,
            VineNodeType.ScatterCannon, VineNodeType.TeslaCoil,
            VineNodeType.FlakBattery, VineNodeType.BarrierWall,
            VineNodeType.PushPull, VineNodeType.BuffEmitter
        };

        public void OnBuildPhase(VineGrid grid, int currentResources, int waveNumber)
        {
            int attempts = 0;
            int placed = 0;

            while (placed < 3 && attempts < 50)
            {
                attempts++;
                int x = _rng.Next(0, grid.Width);
                int y = _rng.Next(0, grid.Height);
                var type = BuildableTypes[_rng.Next(BuildableTypes.Length)];
                var data = VineNodeRegistry.Get(type);
                if (data == null || currentResources < data.ResourceCost) continue;

                if (grid.CanPlace(x, y))
                {
                    AutoPlaceHelper.PlaceAt(grid, x, y, type);
                    currentResources -= data.ResourceCost;
                    placed++;
                }
            }
        }

        public void OnWavePhase(float dt) { }
        public void OnMilestone(int wave, string type) { }
    }

    /// <summary>
    /// Mine Resources first, switch to Materials at wave 5.
    /// Tests economy balance and mining toggle.
    /// </summary>
    public class EconomyFocusStrategy : IAutoPlayerStrategy
    {
        public string Name => "EconomyFocus";

        public void OnBuildPhase(VineGrid grid, int currentResources, int waveNumber)
        {
            // Only place a few cheap towers
            if (waveNumber <= 2)
            {
                var data = VineNodeRegistry.Get(VineNodeType.DamageTower);
                if (data != null && currentResources >= data.ResourceCost)
                {
                    for (int x = 5; x < grid.Width - 5; x += 4)
                    {
                        if (grid.CanPlace(x, grid.Height / 2))
                        {
                            AutoPlaceHelper.PlaceAt(grid,x, grid.Height / 2, VineNodeType.DamageTower);
                            break;
                        }
                    }
                }
            }
        }

        public void OnWavePhase(float dt) { }

        public void OnMilestone(int wave, string type)
        {
            // Toggle mining mode at wave 5
            if (wave >= 5 && ServiceLocator.TryGet<VineHarvester>(out var harvester))
            {
                if (harvester.CurrentMode == MiningMode.Resources)
                    harvester.ToggleMode();
            }
        }
    }

    /// <summary>
    /// All towers near spawn, no maze. Tests early-wave pressure defense.
    /// </summary>
    public class RushDefenseStrategy : IAutoPlayerStrategy
    {
        public string Name => "RushDefense";

        public void OnBuildPhase(VineGrid grid, int currentResources, int waveNumber)
        {
            var data = VineNodeRegistry.Get(VineNodeType.DamageTower);
            if (data == null) return;

            // Place near left edge (West entry)
            int placed = 0;
            for (int y = 3; y < grid.Height - 3 && placed < 3 && currentResources >= data.ResourceCost; y += 2)
            {
                for (int x = 2; x < 8 && currentResources >= data.ResourceCost; x++)
                {
                    if (grid.CanPlace(x, y))
                    {
                        AutoPlaceHelper.PlaceAt(grid,x, y, VineNodeType.DamageTower);
                        currentResources -= data.ResourceCost;
                        placed++;
                        break;
                    }
                }
            }
        }

        public void OnWavePhase(float dt) { }
        public void OnMilestone(int wave, string type) { }
    }

    /// <summary>
    /// Try every component combination on towers.
    /// Tests the slot system thoroughly.
    /// </summary>
    public class SlotExplorerStrategy : IAutoPlayerStrategy
    {
        public string Name => "SlotExplorer";
        private int _componentIndex;

        public void OnBuildPhase(VineGrid grid, int currentResources, int waveNumber)
        {
            // Place a tower and try slotting the next component
            var data = VineNodeRegistry.Get(VineNodeType.DamageTower);
            if (data == null || currentResources < data.ResourceCost) return;

            // Find a spot for a tower
            for (int x = 3; x < grid.Width - 3; x++)
            {
                for (int y = 3; y < grid.Height - 3; y++)
                {
                    if (grid.CanPlace(x, y))
                    {
                        AutoPlaceHelper.PlaceAt(grid,x, y, VineNodeType.DamageTower);
                        // Component slotting would happen here via TowerSlotSystem
                        _componentIndex++;
                        return;
                    }
                }
            }
        }

        public void OnWavePhase(float dt) { }
        public void OnMilestone(int wave, string type) { }
    }

    /// <summary>
    /// Place Mining Building only, never build anything else.
    /// Baseline: how fast do you lose with zero defense?
    /// </summary>
    public class DoNothingStrategy : IAutoPlayerStrategy
    {
        public string Name => "DoNothing";
        public void OnBuildPhase(VineGrid grid, int currentResources, int waveNumber) { }
        public void OnWavePhase(float dt) { }
        public void OnMilestone(int wave, string type) { }
    }
}
