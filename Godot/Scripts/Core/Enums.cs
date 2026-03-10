namespace JunkbotArena
{
    public enum StatType
    {
        Strength,
        Dexterity,
        Constitution,
        Intelligence,
        Charisma,
        Luck,
        MaxHealth,
        MaxMana,
        Armor,
        CritChance,
        CritDamage,
        AttackSpeed,
        MoveSpeed,
        CooldownReduction
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

    public enum Team
    {
        Player,
        Enemy,
        Neutral
    }

    public enum GameState
    {
        Boot,
        MainMenu,
        CharacterCreation,
        Lift,
        SafeRoom,
        InSector,
        Paused,
        GameOver,
        Loading
    }

    public enum ModifierType
    {
        Flat,
        Percent
    }

    public enum ItemRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary,
        Absurd
    }

    public enum ItemType
    {
        Equipment,
        Consumable,
        QuestItem,
        LootBox,
        Crafting,
        Miscellaneous,
        SalvageCore
    }

    public enum EquipmentSlot
    {
        Head,
        Chest,
        Legs,
        Feet,
        Hands,
        MainHand,
        OffHand,
        Ring1,
        Ring2,
        Amulet,
        Back
    }

    public enum AbilityType
    {
        Melee,
        Projectile,
        AoE,
        Buff,
        Summon,
        Movement
    }

    public enum TargetingType
    {
        Self,
        SingleEnemy,
        Ground,
        Direction,
        AllEnemiesInRange
    }

    public enum EnemyBehavior
    {
        Melee,
        Ranged,
        Flanker,
        Healer,
        Charger,
        Swarm,
        Tank
    }

    public enum EnemyTier
    {
        Normal,
        Elite,
        MiniBoss,
        Boss
    }

    public enum RoomType
    {
        Entrance,
        Combat,
        Treasure,
        Shop,
        Boss,
        SafeRoom,
        Lift,
        Puzzle,
        Event,
        Megabonk
    }

    public enum LootBoxTier
    {
        Junk,
        Bronze,
        Silver,
        Gold,
        Diamond,
        Legendary,
        Celestial
    }

    /// <summary>
    /// Visual growth tiers — robots get larger and gain add-on geometry as they level.
    /// Tier 0 = base model, Tier 4 = full evolution with dramatic silhouette changes.
    /// </summary>
    public enum GrowthTier
    {
        Base,       // Lv 1-4:  Stock model, no additions
        Plated,     // Lv 5-9:  Minor plating — shoulder guards, knee pads, forearm plates
        Armored,    // Lv 10-14: Chest overlay, thicker limbs, head crest/antenna upgrade
        Heavy,      // Lv 15-19: Full pauldrons, back module, leg reinforcement, glowing vents
        Evolved     // Lv 20+:  Dramatic silhouette — energy features, crown/horns, structural additions
    }

    /// <summary>
    /// Where weapons mount on the robot body.
    /// </summary>
    public enum WeaponMountType
    {
        HandHeld,       // Default: weapon in hand/WeaponMount marker
        ShoulderMount,  // Turret on top of shoulder
        BackMount,      // Over-the-shoulder rack
        ArmIntegrated   // Replaces forearm — weapon IS the arm
    }

    public enum CommentaryPriority
    {
        Low,
        Medium,
        High,
        Announcement
    }

    public enum CommentaryCategory
    {
        Announcement,
        SectorIntro,
        RoomReaction,
        CombatReaction,
        LootReaction,
        DeathReaction,
        LevelUpReaction,
        IdleChatter,
        QuestAssignment,
        DarkHumor
    }

    public enum SkillNodeType
    {
        Basic,
        Notable,
        Keystone,
        Pinnacle,
        ClassStart,
        CoreSocket
    }

    public enum BotFrameType
    {
        Scrapheap,
        TinCan,
        SparkPlug,
        RustBucket,
        NoiseBox,
        Clunker
    }

    public enum HazardType
    {
        PoisonPool,
        ElectricPlate,
        LavaCrack
    }

    public enum WeaponType
    {
        None,
        Pistol,
        Rifle,
        Shotgun,
        Launcher,
        Repeater,
        BladeRing,
        FlailChain,    // Swinging ball that hits in a circle
        ShockCoil,     // Electric field that zaps nearby enemies
        FlameThrower   // Continuous cone of fire damage
    }

    public enum RoomShape
    {
        Rectangle,
        LShaped,
        TShaped,
        Partitioned
    }

    public enum AffixType
    {
        Prefix,
        Suffix
    }

    public enum FogState
    {
        Hidden,
        Explored,
        Active
    }

    /// <summary>
    /// Emotional intensity of a celebration. Maps to distinct VFX/audio/screen responses.
    /// Think Vampire Survivors (screen-filling dopamine) vs Balatro (oh-no failure).
    /// </summary>
    public enum CelebrationTier
    {
        /// <summary>Comical failure. Sad trombone, item deflates, AXIS mocks you.</summary>
        Junk,
        /// <summary>Barely worth picking up. Quiet, minimal fanfare.</summary>
        Meh,
        /// <summary>Solid find. Satisfying but brief feedback.</summary>
        Decent,
        /// <summary>Great drop! Screen punch, rarity flash, AXIS/BIT react.</summary>
        Exciting,
        /// <summary>Full dopamine hit. Slow-mo, light pillar, particle storm, screen shake cascade.</summary>
        Legendary,
        /// <summary>Screen goes absolutely nuts. Glitch effects, AXIS loses composure, confetti storm.</summary>
        Absurd
    }
}
