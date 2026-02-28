using System.Collections.Generic;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    [CreateAssetMenu(fileName = "NewClass", menuName = "DCC/Class")]
    public class CrawlerClassData : ScriptableObject
    {
        public string ClassId;
        public string ClassName;
        [TextArea] public string Description;
        [TextArea] public string AIDescription;
        public Sprite ClassIcon;

        public StatBlock StatGrowthPerLevel;
        public List<AbilityData> StartingAbilities;
        public List<ClassAbilityUnlock> AbilityUnlocks;
        public SkillTreeData SkillTree;
    }

    [System.Serializable]
    public class ClassAbilityUnlock
    {
        public int RequiredLevel;
        public AbilityData Ability;
        [TextArea] public string UnlockDescription;
    }
}
