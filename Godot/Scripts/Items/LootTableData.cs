using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    public class LootEntry
    {
        public ItemData Item;
        public int Weight = 100;
        public int MinCount = 1;
        public int MaxCount = 1;
    }

    [GlobalClass]
    public partial class LootTableData : Resource
    {
        [Export] public string Id { get; set; } = "";
        [Export] public int MinDrops { get; set; } = 1;
        [Export] public int MaxDrops { get; set; } = 2;

        public List<LootEntry> Entries { get; set; } = new();

        public void AddEntry(ItemData item, int weight = 100, int minCount = 1, int maxCount = 1)
        {
            Entries.Add(new LootEntry
            {
                Item = item,
                Weight = weight,
                MinCount = minCount,
                MaxCount = maxCount
            });
        }
    }
}
