using System.Collections.Generic;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    [CreateAssetMenu(fileName = "NewRace", menuName = "DCC/Race")]
    public class RaceData : ScriptableObject
    {
        public string RaceName;
        [TextArea] public string Description;
        public Sprite Portrait;
        public StatBlock BaseStats;
        public List<PassiveAbilityData> RacialPassives;
        public string[] AvailableClassIds;
    }

    [System.Serializable]
    public class PassiveAbilityData
    {
        public string PassiveName;
        [TextArea] public string Description;
        public Sprite Icon;
        public List<StatModifier> StatBonuses;
    }
}
