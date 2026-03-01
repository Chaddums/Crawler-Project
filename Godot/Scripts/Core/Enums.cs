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
        Miscellaneous
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
        Event
    }

    public enum LootBoxTier
    {
        Bronze,
        Silver,
        Gold,
        Diamond,
        Legendary
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
        ClassStart,
        JewelSocket
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
        Gun,
        BladeRing
    }

    public enum AffixType
    {
        Prefix,
        Suffix
    }
}
