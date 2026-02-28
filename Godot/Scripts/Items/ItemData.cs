using Godot;

namespace JunkbotArena
{
    [GlobalClass]
    public partial class ItemData : Resource
    {
        [Export] public string Id { get; set; } = "";
        [Export] public string ItemName { get; set; } = "";
        [Export] public string Description { get; set; } = "";
        [Export] public ItemRarity Rarity { get; set; } = ItemRarity.Common;
        [Export] public ItemType Type { get; set; } = ItemType.Miscellaneous;
        [Export] public int MaxStack { get; set; } = 1;
        [Export] public int BaseValue { get; set; } = 0;
        [Export] public Texture2D Icon { get; set; }

        public ItemData() { }

        public ItemData(string id, string name, ItemRarity rarity, ItemType type)
        {
            Id = id;
            ItemName = name;
            Rarity = rarity;
            Type = type;
        }
    }
}
