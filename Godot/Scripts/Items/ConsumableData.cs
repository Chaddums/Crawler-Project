using Godot;

namespace JunkbotArena
{
    [GlobalClass]
    public partial class ConsumableData : ItemData
    {
        [Export] public float HealAmount { get; set; }
        [Export] public float ManaRestoreAmount { get; set; }
        [Export] public string BuffId { get; set; } = "";
        [Export] public float BuffDuration { get; set; }

        public ConsumableData()
        {
            Type = ItemType.Consumable;
            MaxStack = 10;
        }
    }
}
