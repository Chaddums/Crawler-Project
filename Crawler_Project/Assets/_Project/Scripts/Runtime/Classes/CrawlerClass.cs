using System.Collections.Generic;
using System.Linq;

namespace DungeonCrawlerCarl
{
    public class CrawlerClass
    {
        public CrawlerClassData Data { get; private set; }
        public SkillTree SkillTree { get; private set; }
        public List<AbilityData> UnlockedAbilities { get; private set; } = new();

        public CrawlerClass(CrawlerClassData data)
        {
            Data = data;

            if (data.SkillTree != null)
                SkillTree = new SkillTree(data.SkillTree);

            // Add starting abilities
            if (data.StartingAbilities != null)
                UnlockedAbilities.AddRange(data.StartingAbilities);
        }

        public void OnLevelUp(int newLevel)
        {
            if (Data.AbilityUnlocks == null) return;

            var newUnlocks = Data.AbilityUnlocks
                .Where(u => u.RequiredLevel == newLevel && u.Ability != null)
                .ToList();

            foreach (var unlock in newUnlocks)
            {
                if (!UnlockedAbilities.Contains(unlock.Ability))
                {
                    UnlockedAbilities.Add(unlock.Ability);
                    GameEvents.OnAbilityUnlocked?.Invoke(unlock.Ability as UnityEngine.ScriptableObject);
                }
            }

            SkillTree?.AddPoints(1);
        }

        public bool HasAbility(AbilityData ability)
        {
            return UnlockedAbilities.Contains(ability);
        }
    }
}
