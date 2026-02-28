using System.Collections.Generic;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    [CreateAssetMenu(fileName = "NewSkillTree", menuName = "DCC/Skill Tree")]
    public class SkillTreeData : ScriptableObject
    {
        public string TreeName;
        public List<SkillNodeData> Nodes;
    }

    [System.Serializable]
    public class SkillNodeData
    {
        public string NodeId;
        public string NodeName;
        [TextArea] public string Description;
        public Sprite Icon;
        public int PointCost = 1;
        public int RequiredPlayerLevel;
        public string[] PrerequisiteNodeIds;
        public SkillNodeType Type;

        [Header("Passive Bonus")]
        public List<StatModifier> PassiveStatBonuses;

        [Header("Ability")]
        public AbilityData UnlockedAbility;
        public AbilityData UpgradedAbility;

        [Header("UI Layout")]
        public Vector2 TreePosition;
    }
}
