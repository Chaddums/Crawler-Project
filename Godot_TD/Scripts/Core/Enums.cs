namespace JunkyardTD
{
    public enum GamePhase
    {
        Boot,
        MainMenu,
        MapSelect,
        Build,       // Between waves — place towers, modify terrain
        Wave,        // Enemies incoming
        WaveComplete,
        Victory,
        Defeat,
        Paused
    }

    public enum Team
    {
        Player,
        Enemy,
        Neutral
    }

    public enum DamageType
    {
        Physical,
        Fire,
        Ice,
        Lightning,
        Poison,
        Dark,
        Holy
    }

    public enum TowerType
    {
        Blaster,      // Basic single-target, fast fire rate
        Scatter,      // Short-range shotgun burst, AoE
        Zapper,       // Chain lightning between nearby enemies
        Incinerator,  // Flame cone, DoT
        Freezer,      // Slows enemies in radius
        Mortar,       // Long-range AoE, slow fire
        Sniper,       // Very long range, single target, high damage
        Recycler      // Auto-collects scrap in radius
    }

    public enum TowerRarity
    {
        Scrap,        // Base tower, no mods
        Salvaged,     // 1 mod slot
        Reinforced,   // 2 mod slots
        Overclocked,  // 3 mod slots
        Prototype     // 3 mod slots + unique passive
    }

    public enum ModComponentType
    {
        // Barrel mods — affect projectile behavior
        Gyroscope,      // +tracking accuracy
        HeatCoil,       // +burn damage
        CryoCell,       // +slow effect
        ChargeCapacitor,// +damage per shot, -fire rate
        SplitPrism,     // Projectile splits on hit

        // Frame mods — affect tower stats
        ReinforcedPlating, // +tower HP
        OverclockModule,   // +fire rate, tower takes DoT
        SalvageHopper,     // +scrap from kills in range
        RangeExtender,     // +range
        ShockAbsorber      // -self damage from recoil effects
    }

    public enum EnemyType
    {
        ScrapRat,     // Fast, weak, swarm
        WireWorm,     // Medium speed, burrows (ignores some terrain)
        RustHulk,     // Slow, tanky, armored
        SparkDrone,   // Flying, ignores maze
        ScrapThief,   // Steals scrap piles on the ground
        ShieldBearer, // Provides armor aura to nearby enemies
        Bomber,       // Explodes on death, damages nearby towers
        Fabricator    // Upgrades nearby enemies with scrap armor
    }

    public enum EnemyTier
    {
        Normal,
        Armored,      // Has scrap-armor from pillar #5
        Elite,
        Boss
    }

    public enum TerrainType
    {
        Open,         // Walkable by enemies, buildable
        Blocked,      // Wall/obstacle — enemies path around
        Debris,       // Can be bulldozed to Open or piled to Blocked
        TowerSlot,    // Has a tower on it
        Path,         // Designated enemy lane (cannot build)
        SpawnPoint,   // Enemy entry
        Core          // Defend this — player base
    }

    public enum StatType
    {
        MaxHealth,
        Armor,
        MoveSpeed,
        AttackDamage,
        AttackSpeed,
        Range,
        CritChance,
        CritDamage,
        SlowPotency,
        BurnDamage,
        SplashRadius
    }

    public enum ModifierType
    {
        Flat,
        Percent
    }

    public enum ScrapType
    {
        Common,       // Basic currency
        Refined,      // For upgrades
        Exotic        // For prototype towers
    }

    public enum CommentaryPriority
    {
        Low,
        Medium,
        High,
        Announcement
    }

    // ── Vine Logic TD ──

    public enum VineNodeType
    {
        // Structural / Routing
        Extender,       // Wire — passes signals through, no effect
        Junction,       // Splitter — signal to all outputs
        Switch,         // Railroad switch — toggles enemy route L/R
        Gate,           // AND gate — opens when 2+ inputs active
        Inverter,       // NOT gate — flips signal state
        Delay,          // Buffer — holds signal N seconds
        Latch,          // Flip-flop — stays open until reset

        // Sensor / Input
        ProximitySensor,// Enemy within range
        TypeSensor,     // Specific enemy type detected
        HPSensor,       // Enemy below HP threshold
        CountSensor,    // N+ enemies in zone
        Timer,          // Fires on interval

        // Effect / Output
        DamageTower,    // Shoots enemies when signaled
        SlowField,      // Debuffs enemies passing through
        PushPull,       // Redirects enemy movement
        LoopAnchor,     // Creates circular route section
        BuffEmitter,    // Sends buff through vine to connected towers
        SignalCannon    // Player-triggered signal
    }

    public enum VineNodeCategory
    {
        Structural,
        Sensor,
        Effect
    }

    public enum SignalType
    {
        Trigger,        // Standard on/off signal
        Buff,           // Damage/speed buff that propagates
        Reset           // Resets latches and stateful nodes
    }

    public enum VineEnemyFaction
    {
        Scavenger,      // Follow signals, confused by flickering gates
        Brute,          // Bulldoze switches, break logic state
        Ghost,          // Ignore gate routing, phase through walls
        Swarm           // Tiny, trigger count sensors early
    }

    public enum VineCellType
    {
        Empty,          // Walkable, buildable
        Wall,           // Impassable
        Node,           // Occupied by a vine node
        Entry,          // Enemy spawn
        Exit            // Core / goal
    }
}
