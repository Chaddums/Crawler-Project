using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DungeonCrawlerCarl
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

        public PassiveTree(PassiveTreeData treeData, CrawlerClassName crawlerClass)
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
        /// and is connected to an already-allocated node.
        /// </summary>
        public bool CanAllocate(string nodeId, int availablePoints)
        {
            if (_allocatedNodes.Contains(nodeId)) return false;
            if (availablePoints <= 0) return false;

            var node = _treeData.GetNode(nodeId);
            if (node == null) return false;

            // Must be connected to at least one allocated node
            return node.Connections.Any(c => _allocatedNodes.Contains(c));
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
