namespace JunkyardTD
{
    public enum GamePhase
    {
        Boot,
        MainMenu,
        // S1: removed MapSelect, FloorComplete (floors removed)
        Build,       // Between waves — place towers, modify terrain
        Wave,        // Enemies incoming
        WaveComplete,
        Victory,
        Defeat,
        Paused,
        LevelEditor
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

        // Effect / Output
        DamageTower,
        SlowField,
        PushPull,
        LoopAnchor,
        BuffEmitter,
        SignalCannon
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
        Prop
    }

    public enum TerrainProfile
    {
        Gentle,
        Valley,
        Complex
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
