using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    public class SkillTree
    {
        private SkillTreeData _data;
        private HashSet<string> _unlockedNodes = new();
        private int _availablePoints;

        public SkillTreeData Data => _data;
        public int AvailablePoints => _availablePoints;
        public IReadOnlyCollection<string> UnlockedNodes => _unlockedNodes;

        public SkillTree(SkillTreeData data)
        {
            _data = data;
        }

        public bool CanUnlock(SkillNodeData node)
        {
            if (node == null) return false;
            if (_unlockedNodes.Contains(node.NodeId)) return false;
            if (_availablePoints < node.PointCost) return false;

            if (node.PrerequisiteNodeIds != null)
            {
                foreach (var prereq in node.PrerequisiteNodeIds)
                {
                    if (!_unlockedNodes.Contains(prereq))
                        return false;
                }
            }

            return true;
        }

        public bool UnlockNode(string nodeId, StatBlock stats)
        {
            var node = _data.Nodes.FirstOrDefault(n => n.NodeId == nodeId);
            if (node == null || !CanUnlock(node)) return false;

            _availablePoints -= node.PointCost;
            _unlockedNodes.Add(node.NodeId);

            ApplyNodeEffects(node, stats);
            return true;
        }

        private void ApplyNodeEffects(SkillNodeData node, StatBlock stats)
        {
            switch (node.Type)
            {
                case SkillNodeType.PassiveBonus:
                    if (node.PassiveStatBonuses != null)
                    {
                        foreach (var mod in node.PassiveStatBonuses)
                        {
                            mod.Source = node;
                            stats.AddModifier(mod);
                        }
                    }
                    break;

                case SkillNodeType.NewAbility:
                    if (node.UnlockedAbility != null)
                        GameEvents.OnAbilityUnlocked?.Invoke(node.UnlockedAbility as ScriptableObject);
                    break;

                case SkillNodeType.AbilityUpgrade:
                    if (node.UpgradedAbility != null)
                        GameEvents.OnAbilityUnlocked?.Invoke(node.UpgradedAbility as ScriptableObject);
                    break;

                case SkillNodeType.KeystonePassive:
                    if (node.PassiveStatBonuses != null)
                    {
                        foreach (var mod in node.PassiveStatBonuses)
                        {
                            mod.Source = node;
                            stats.AddModifier(mod);
                        }
                    }
                    if (node.UnlockedAbility != null)
                        GameEvents.OnAbilityUnlocked?.Invoke(node.UnlockedAbility as ScriptableObject);
                    break;
            }
        }

        public void AddPoints(int points)
        {
            _availablePoints += points;
        }

        public bool IsNodeUnlocked(string nodeId)
        {
            return _unlockedNodes.Contains(nodeId);
        }

        public List<SkillNodeData> GetAvailableNodes(int playerLevel)
        {
            return _data.Nodes
                .Where(n => !_unlockedNodes.Contains(n.NodeId) && n.RequiredPlayerLevel <= playerLevel && CanUnlock(n))
                .ToList();
        }

        /// <summary>
        /// Resets the entire skill tree, removing all stat modifiers from unlocked nodes
        /// and refunding all spent points. Returns the total points refunded.
        /// </summary>
        public int Respec(StatBlock stats)
        {
            int refundedPoints = 0;

            foreach (var nodeId in _unlockedNodes)
            {
                var node = FindNode(nodeId);
                if (node == null) continue;

                refundedPoints += node.PointCost;

                // Remove stat modifiers applied by this node
                stats.RemoveModifiersFromSource(node);
            }

            _unlockedNodes.Clear();
            _availablePoints += refundedPoints;

            return refundedPoints;
        }

        private SkillNodeData FindNode(string nodeId)
        {
            if (_data?.Nodes == null) return null;
            for (int i = 0; i < _data.Nodes.Count; i++)
            {
                if (_data.Nodes[i].NodeId == nodeId)
                    return _data.Nodes[i];
            }
            return null;
        }
    }
}
