namespace JunkyardTD
{
    public enum GamePhase
    {
        Boot,
        MainMenu,
        PlanetSelect,  // Stitch UI planet selection screen
        // S1: removed MapSelect, FloorComplete (floors removed)
        Build,       // Between waves — place towers, modify terrain
        Wave,        // Enemies incoming
        WaveComplete,
        Victory,
        Defeat,
        Paused,
        LevelEditor,
        Territory,       // S4: Territory map screen
        SuitInventory,   // S4: Suit management screen
        BossConfirm,     // S4: Boss run confirmation screen
        RelicInventory,  // Relic inventory screen
        MetaHub,         // UX6: Between-runs command center
        Debrief          // UX11: Post-run extraction results
    }

    public enum RunMode
    {
        Harvest,   // "Harvest Resources" — extraction-focused run
        Invasion,  // "Attempt Invasion" — combat-focused run
        BossRun    // S4: High-stakes mode — risk a suit, win = clear section
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

    // S1: Classic TD enums kept for now — other squads may still reference
    public enum TowerType
    {
        Blaster,
        Scatter,
        Zapper,
        Incinerator,
        Freezer,
        Mortar,
        Sniper,
        Recycler
    }

    public enum TowerRarity
    {
        Scrap,
        Salvaged,
        Reinforced,
        Overclocked,
        Prototype
    }

    public enum ModComponentType
    {
        Gyroscope,
        HeatCoil,
        CryoCell,
        ChargeCapacitor,
        SplitPrism,
        ReinforcedPlating,
        OverclockModule,
        SalvageHopper,
        RangeExtender,
        ShockAbsorber
    }

    public enum EnemyType
    {
        ScrapRat,
        WireWorm,
        RustHulk,
        SparkDrone,
        ScrapThief,
        ShieldBearer,
        Bomber,
        Fabricator
    }

    public enum EnemyTier
    {
        Normal,
        Armored,
        Elite,
        Boss
    }

    public enum TerrainType
    {
        Open,
        Blocked,
        Debris,
        TowerSlot,
        Path,
        SpawnPoint,
        Core
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

    // S1: renamed from ScrapType → ResourceType
    public enum ResourceType
    {
        Common,
        Refined,
        Exotic
    }

    public enum CommentaryPriority
    {
        Low,
        Medium,
        High,
        Announcement
    }

    // ── Directional Spawning ──

    public enum CardinalDirection
    {
        West,
        North,
        East,
        South
    }

    // ── Vine Logic TD ──

    public enum VineNodeType
    {
        // Structural / Routing
        Extender,
        Junction,
        Switch,
        Gate,
        Inverter,
        Delay,
        Latch,

        // Sensor / Input
        ProximitySensor,
        TypeSensor,
        HPSensor,
        CountSensor,
        Timer,

        // Obelisk-specific
        Pylon,

        // Arcanist-specific
        Socket,
        Prism,

        // Effect / Output (core towers — auto-fire, slottable)
        DamageTower,
        SlowField,
        PushPull,
        LoopAnchor,     // Deprecated — kept for save compat
        BuffEmitter,
        SignalCannon,   // Deprecated — kept for save compat

        // New core towers (auto-fire, slottable)
        ScatterCannon,  // AoE damage, anti-swarm
        TeslaCoil,      // Chain lightning, anti-cluster
        FlakBattery,    // Fast-fire low-damage volume tower
        BarrierWall,    // Buildable wall with HP, shapes pathing
    }

    public enum VineNodeCategory
    {
        Structural,
        Sensor,
        Effect
    }

    public enum SignalType
    {
        Trigger,
        Buff,
        Reset
    }

    public enum VineEnemyFaction
    {
        Scavenger,
        Brute,
        Ghost,
        Swarm
    }

    public enum VineCellType
    {
        Empty,
        Wall,
        Node,
        Entry,
        Exit,
        Elevated,
        Channel,
        DataStream,
        Prop,

        // Phase5-MapDesign: new terrain types
        Hazard,           // Damages anything standing on it (lava, acid, electric)
        Pit,              // Impassable hole — enemies path around, no tower placement
        DestructibleWall, // Wall enemies can break through if undefended
        ResourceNode      // Bonus resources when tower placed adjacent
    }

    public enum HazardType
    {
        Acid,       // Poison DOT, green
        Lava,       // Fire burst damage, orange
        Electric    // Periodic zap, blue
    }

    public enum TerrainProfile
    {
        Gentle,
        Valley,
        Complex
    }

    /// <summary>
    /// How each spire type handles tower placement.
    /// </summary>
    public enum PlacementMode
    {
        FreeRadius,     // Obelisk: free placement within power radius
        SocketGrid,     // Arcanist: socket-based grid placement
        WireNetwork     // Bruteforge: wire connections back to forge
    }

    /// <summary>
    /// Mining Building resource mode — the core strategic toggle.
    /// S1: renamed Scrap→Resources, Magic→Materials
    /// </summary>
    public enum MiningMode
    {
        Resources,      // Passive resource generation — invest in vine nodes
        Materials       // Passive materials generation — invest in player power
    }

    /// <summary>
    /// AXIS corruption event — dramatic mid-wave chaos that buffs both sides.
    /// </summary>
    public enum CorruptionType
    {
        AxisChaos
    }

    // S5: Tower slot system — modular tower customization
    public enum TowerSlotType
    {
        Barrel,     // Changes projectile behavior
        Core,       // Changes targeting logic
        Frame       // Stat modifiers
    }

    public enum TowerComponentType
    {
        // Barrel components
        ChainArc,           // Hits bounce to nearby enemies
        ScatterShot,        // AoE splash damage
        PiercingRound,      // Projectile passes through enemies
        CryoBolt,           // Applies slow on hit
        IncendiaryRound,    // Burn DoT on hit

        // Core components
        PriorityWeak,       // Target lowest HP
        PriorityFast,       // Target fastest enemy
        PriorityFar,        // Target farthest along path
        FocusFire,          // Lock onto single target until dead

        // Frame components
        ExtendedRange,      // +30% range
        RapidFire,          // +25% attack speed
        HeavyPlating,       // +50% tower HP
        Overclock,          // +15% damage, -10% HP

        // Signal drops (from enemy kills — repurposed old sensors/routing)
        // Barrel
        ProximityCharge,    // +40% damage to enemies within half range (close-range bonus)
        SwarmReactor,       // +10% damage per enemy in range (scales with crowd)

        // Core
        MotionPredictor,    // Leads shots — never misses fast enemies
        DamageRouter,       // Prioritize most-damaged enemy in range
        PhaseInverter,      // Damage type flips to counter-element

        // Frame
        CapacitorBank,      // Every 4th shot deals 3x damage (charge-up)
        RelayAmplifier,     // +15% damage and range to adjacent towers too
        CrowdSurge,         // +50% fire rate when 5+ enemies in range
        TimerOverdrive,     // Cooldowns reduced 20%
        LatchPlating        // First hit each wave is fully absorbed (shield reset per wave)
    }

    /// <summary>
    /// Material type — chosen at mining building placement.
    /// Planet-agnostic: all three available on every planet with equal weight.
    /// S1: renamed from MagicType → MaterialType
    /// </summary>
    public enum MaterialType
    {
        None,
        Chaos,          // Mind (confusion, misdirection) + Corrosive (poison/acid) — entropy
        Power,          // Range extension, ability amplification, signal boost — amplification
        Environment     // Deconstruct/reconstruct terrain, walls↔resources — spatial manipulation
    }
}
