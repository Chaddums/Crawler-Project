namespace JunkyardTD
{
    /// <summary>
    /// How a shield wall's collapse is triggered.
    /// TimeMilestone is the default — others allow future systems to open walls.
    /// </summary>
    public enum ShieldWallTriggerType
    {
        TimeMilestone,   // Breaks at a game-time threshold
        WorldObject,     // Player claims an object in the world
        UIPrompt,        // Player makes a selection from an event prompt
        Scripted,        // External code calls BreakWall() directly
        Manual           // Debug / API
    }

    /// <summary>
    /// Configuration for a single shield wall. Loaded from JSON level data.
    /// </summary>
    public class ShieldWallConfig
    {
        public CardinalDirection Direction { get; set; }
        public float HP { get; set; } = Constants.SHIELD_WALL_BASE_HP;
        public int EntryRegionIndex { get; set; }
        public ShieldWallTriggerType TriggerType { get; set; } = ShieldWallTriggerType.TimeMilestone;
        public float TriggerTime { get; set; } = Constants.SHIELD_WALL_DEFAULT_BREAK_INTERVAL;
    }
}
