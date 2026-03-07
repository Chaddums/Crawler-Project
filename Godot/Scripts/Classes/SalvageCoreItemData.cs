using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// ItemData subclass for salvage cores so they can live in the inventory system.
    /// Links to the SalvageCoreData that defines its effects.
    /// </summary>
    [GlobalClass]
    public partial class SalvageCoreItemData : ItemData
    {
        /// <summary>
        /// The salvage core definition with stats, perks, etc.
        /// </summary>
        public SalvageCoreData CoreData { get; set; }

        public SalvageCoreItemData() { }

        public SalvageCoreItemData(SalvageCoreData core)
        {
            CoreData = core;
            Id = core.Id;
            ItemName = core.CoreName;
            Description = core.Description;
            Type = ItemType.SalvageCore;
            MaxStack = 1;
            Icon = core.Icon;

            Rarity = core.Rarity switch
            {
                SalvageCoreRarity.Rare => ItemRarity.Rare,
                SalvageCoreRarity.Epic => ItemRarity.Epic,
                SalvageCoreRarity.Legendary => ItemRarity.Legendary,
                _ => ItemRarity.Rare
            };
        }
    }
}
