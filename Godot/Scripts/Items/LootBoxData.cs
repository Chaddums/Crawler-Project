using Godot;

namespace JunkbotArena
{
    [GlobalClass]
    public partial class LootBoxData : ItemData
    {
        [Export] public LootBoxTier Tier { get; set; } = LootBoxTier.Bronze;
        [Export] public int MinItems { get; set; } = 1;
        [Export] public int MaxItems { get; set; } = 2;
        [Export] public float EpicChance { get; set; } = 0.05f;
        [Export] public float LegendaryChance { get; set; } = 0.01f;

        public LootBoxData()
        {
            Type = ItemType.LootBox;
            MaxStack = 1;
        }

        public LootBoxData(string id, string name, LootBoxTier tier, int minItems, int maxItems,
            float epicChance, float legendaryChance)
            : base(id, name, ItemRarity.Common, ItemType.LootBox)
        {
            Tier = tier;
            MinItems = minItems;
            MaxItems = maxItems;
            EpicChance = epicChance;
            LegendaryChance = legendaryChance;
            MaxStack = 1;
        }
    }
}
