using System.Collections.Generic;

namespace JunkbotArena
{
    /// <summary>
    /// Serializable save data classes for the entire game state.
    /// </summary>
    public class SaveData
    {
        public string Version { get; set; } = "1.0";
        public PlayerSaveData Player { get; set; } = new();
        public int CurrentSector { get; set; } = 1;
        public int CurrentArea { get; set; } = 1;
        public AchievementSaveData Achievements { get; set; } = new();
        public float TimerRemaining { get; set; } = 300f;
    }

    public class AchievementSaveData
    {
        public HashSet<string> Unlocked { get; set; } = new();
        public Dictionary<string, int> Counters { get; set; } = new();
    }

    public class PlayerSaveData
    {
        public BotFrameType ClassName { get; set; } = BotFrameType.TinCan;
        public int Level { get; set; } = 1;
        public int Experience { get; set; }
        public int SkillPoints { get; set; }
        public float CurrentHealth { get; set; }
        public float CurrentMana { get; set; }
        public Dictionary<string, float> BaseStats { get; set; } = new();
        public List<ItemSaveData> InventoryItems { get; set; } = new();
        public Dictionary<string, ItemSaveData> EquippedItems { get; set; } = new();
        public List<string> AllocatedPassiveNodes { get; set; } = new();
        public List<string> AbilityIds { get; set; } = new();
    }

    public class ItemSaveData
    {
        public string BaseDataId { get; set; } = "";
        public string Rarity { get; set; } = "Common";
        public int StackCount { get; set; } = 1;
        public List<AffixSaveData> Affixes { get; set; } = new();
    }

    public class AffixSaveData
    {
        public string AffixId { get; set; } = "";
        public float RolledValue { get; set; }
    }
}
