using System.Collections.Generic;

namespace DungeonCrawlerCarl
{
    [System.Serializable]
    public class SaveData
    {
        public string PlayerName;
        public string ClassName;
        public string RaceName;
        public int CurrentFloor;
        public int PlayerLevel;
        public int ExperiencePoints;
        public float[] PlayerPosition = new float[3];
        public SerializableStatBlock PlayerStats;
        public List<string> InventoryItemIds = new();
        public List<string> EquippedItemIds = new();
        public List<string> UnlockedAbilityIds = new();
        public CompanionSaveData CompanionData;
        public List<string> CompletedQuestIds = new();
        public List<string> TriggeredCommentaryIds = new();
        public float PlayTime;
    }

    [System.Serializable]
    public class CompanionSaveData
    {
        public string CompanionId;
        public int Level;
        public float CurrentHealth;
        public List<string> UnlockedAbilityIds = new();
    }

    [System.Serializable]
    public class SerializableStatBlock
    {
        public List<SerializableStatEntry> Stats = new();

        [System.Serializable]
        public struct SerializableStatEntry
        {
            public StatType Type;
            public float Value;
        }
    }
}
