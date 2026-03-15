using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// POE-style passive tree runtime. Tracks allocated nodes, enforces connectivity,
    /// applies stat bonuses, and supports respec.
    /// </summary>
    public class PassiveTree
    {
        private readonly PassiveTreeData _treeData;
        private readonly HashSet<string> _allocatedNodes = new();
        private readonly string _classStartId;
        private int _pointsSpent;

        public IReadOnlyCollection<string> AllocatedNodes => _allocatedNodes;
        public int PointsSpent => _pointsSpent;

        /// <summary>
        /// Check if a gameplay-changing perk is currently active (allocated).
        /// Other systems call this to modify behavior based on tree choices.
        /// </summary>
        public bool HasPerk(string perkId)
        {
            if (string.IsNullOrEmpty(perkId)) return false;
            foreach (var nodeId in _allocatedNodes)
            {
                var node = _treeData.GetNode(nodeId);
                if (node == null) continue;
                if (node.PerkId == perkId) return true;
                // Also check socketed cores
                if (node.SocketedCore != null)
                {
                    if (node.SocketedCore.PerkId == perkId) return true;
                    if (node.SocketedCore.GrantsPerkId == perkId) return true;
                }
            }
            return false;
        }

        public PassiveTree(PassiveTreeData treeData, BotFrameType crawlerClass)
        {
            _treeData = treeData;

            if (treeData.ClassStartNodes.TryGetValue(crawlerClass, out var startId))
            {
                _classStartId = startId;
                _allocatedNodes.Add(startId);
            }
        }

        /// <summary>
        /// Check if a node can be allocated: has points, not already allocated,
        /// connected to an allocated node, and passes threshold/exclusivity gates.
        /// </summary>
        public bool CanAllocate(string nodeId, int availablePoints)
        {
            if (_allocatedNodes.Contains(nodeId)) return false;
            if (availablePoints <= 0) return false;

            var node = _treeData.GetNode(nodeId);
            if (node == null) return false;

            // Must be connected to at least one allocated node
            if (!node.Connections.Any(c => _allocatedNodes.Contains(c)))
                return false;

            // Threshold gate: require N points spent in a specific branch
            if (node.RequiredPointsInBranch > 0 && !string.IsNullOrEmpty(node.RequiredBranchId))
            {
                if (CountPointsInBranch(node.RequiredBranchId) < node.RequiredPointsInBranch)
                    return false;
            }

            // Keystone mutual exclusivity
            if (!string.IsNullOrEmpty(node.MutuallyExclusiveWith))
            {
                if (_allocatedNodes.Contains(node.MutuallyExclusiveWith))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Count how many allocated nodes belong to a given sub-branch.
        /// </summary>
        public int CountPointsInBranch(string branchId)
        {
            if (string.IsNullOrEmpty(branchId)) return 0;
            int count = 0;
            foreach (var nodeId in _allocatedNodes)
            {
                var node = _treeData.GetNode(nodeId);
                if (node != null && node.SubBranchId == branchId)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Allocate a node, spending a skill point and applying stat bonuses.
        /// </summary>
        public bool AllocateNode(string nodeId, StatBlock stats, int availablePoints)
        {
            if (!CanAllocate(nodeId, availablePoints)) return false;

            var node = _treeData.GetNode(nodeId);
            _allocatedNodes.Add(nodeId);
            _pointsSpent++;

            // Apply stat bonuses as modifiers sourced from this node
            foreach (var bonus in node.StatBonuses)
            {
                var mod = new StatModifier(bonus.StatType, bonus.ModType, bonus.Value, nodeId);
                stats.AddModifier(mod);
            }

            GameEvents.OnPassiveNodeAllocated?.Invoke(nodeId);
            GD.Print($"[PassiveTree] Allocated: {node.NodeName} ({nodeId})");
            return true;
        }

        /// <summary>
        /// Deallocate a node if removing it wouldn't disconnect the tree.
        /// </summary>
        public bool DeallocateNode(string nodeId, StatBlock stats)
        {
            if (!_allocatedNodes.Contains(nodeId)) return false;
            if (nodeId == _classStartId) return false; // Can't remove start

            // Check if removing would disconnect the tree
            if (!CanRemoveWithoutDisconnect(nodeId)) return false;

            _allocatedNodes.Remove(nodeId);
            _pointsSpent--;

            // Remove modifiers sourced from this node
            stats.RemoveModifiersFromSource(nodeId);

            GameEvents.OnPassiveNodeDeallocated?.Invoke(nodeId);
            return true;
        }

        /// <summary>
        /// Full respec: remove all nodes except class start, refund all points.
        /// </summary>
        public int Respec(StatBlock stats)
        {
            int refunded = 0;

            foreach (var nodeId in _allocatedNodes.ToList())
            {
                if (nodeId == _classStartId) continue;
                stats.RemoveModifiersFromSource(nodeId);
                refunded++;
            }

            _allocatedNodes.Clear();
            _allocatedNodes.Add(_classStartId);
            _pointsSpent = 0;

            GameEvents.OnPassiveTreeReset?.Invoke();
            GD.Print($"[PassiveTree] Respec complete. Refunded {refunded} points");
            return refunded;
        }

        /// <summary>
        /// Returns all nodes that could currently be allocated (the frontier).
        /// </summary>
        public List<string> GetAllocatableNodes(int availablePoints)
        {
            if (availablePoints <= 0) return new List<string>();

            var result = new List<string>();

            foreach (var kvp in _treeData.Nodes)
            {
                if (CanAllocate(kvp.Key, availablePoints))
                    result.Add(kvp.Key);
            }

            return result;
        }

        // =================================================================
        // SALVAGE CORE SOCKETING
        // =================================================================

        /// <summary>
        /// Socket a salvage core into an allocated CoreSocket node.
        /// Returns true if successful.
        /// </summary>
        public bool SocketCore(string nodeId, SalvageCoreData core, StatBlock stats)
        {
            if (!_allocatedNodes.Contains(nodeId)) return false;
            var node = _treeData.GetNode(nodeId);
            if (node == null || node.NodeType != SkillNodeType.CoreSocket) return false;

            // Remove existing core first
            if (node.SocketedCore != null)
                UnsocketCore(nodeId, stats);

            node.SocketedCore = core;

            // Apply core stat bonuses
            foreach (var bonus in core.StatBonuses)
            {
                var mod = new StatModifier(bonus.StatType, bonus.ModType, bonus.Value, $"core_{nodeId}");
                stats.AddModifier(mod);
            }

            GD.Print($"[PassiveTree] Socketed {core.CoreName} into {nodeId}");
            return true;
        }

        /// <summary>
        /// Remove a socketed core from a CoreSocket node. Returns the removed core.
        /// </summary>
        public SalvageCoreData UnsocketCore(string nodeId, StatBlock stats)
        {
            var node = _treeData.GetNode(nodeId);
            if (node?.SocketedCore == null) return null;

            var core = node.SocketedCore;
            node.SocketedCore = null;

            // Remove stat modifiers from this core
            stats.RemoveModifiersFromSource($"core_{nodeId}");

            GD.Print($"[PassiveTree] Unsocketed {core.CoreName} from {nodeId}");
            return core;
        }

        /// <summary>
        /// Check if a gameplay-changing perk is granted by any socketed core.
        /// </summary>
        public bool HasCorePerk(string perkId)
        {
            if (string.IsNullOrEmpty(perkId)) return false;
            foreach (var nodeId in _allocatedNodes)
            {
                var node = _treeData.GetNode(nodeId);
                if (node?.SocketedCore == null) continue;
                if (node.SocketedCore.PerkId == perkId) return true;
                if (node.SocketedCore.GrantsPerkId == perkId) return true;
            }
            return false;
        }

        /// <summary>
        /// Get the total ability level bonus from Amplifier Cores for a specific ability.
        /// </summary>
        public int GetAbilityLevelBonus(string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId)) return 0;
            int bonus = 0;
            foreach (var nodeId in _allocatedNodes)
            {
                var node = _treeData.GetNode(nodeId);
                if (node?.SocketedCore == null) continue;
                if (node.SocketedCore.AmplifyAbilityId == abilityId)
                    bonus += 3;
            }
            return bonus;
        }

        /// <summary>
        /// Check if removing a node would disconnect any other allocated nodes from the start.
        /// Uses BFS from the class start, ignoring the candidate node.
        /// </summary>
        private bool CanRemoveWithoutDisconnect(string candidateId)
        {
            var remaining = new HashSet<string>(_allocatedNodes);
            remaining.Remove(candidateId);

            if (remaining.Count <= 1) return true; // Only start left

            // BFS from class start
            var visited = new HashSet<string>();
            var queue = new Queue<string>();
            queue.Enqueue(_classStartId);
            visited.Add(_classStartId);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var node = _treeData.GetNode(current);
                if (node == null) continue;

                foreach (var neighbor in node.Connections)
                {
                    if (remaining.Contains(neighbor) && !visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }

            return visited.Count == remaining.Count;
        }
    }
}
