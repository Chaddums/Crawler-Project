namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Interface for any entity that can receive items (player, companion, etc.).
    /// Lives in DCC.Items so item-related code can add items without referencing DCC.Player.
    /// </summary>
    public interface IItemReceiver
    {
        bool TryAddItem(ItemInstance item);
        string DisplayName { get; }
    }
}
