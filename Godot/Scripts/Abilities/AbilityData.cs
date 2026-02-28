using Godot;

namespace DungeonCrawlerCarl
{
    [GlobalClass]
    public partial class AbilityData : Resource
    {
        [Export] public string Id { get; set; } = "";
        [Export] public string AbilityName { get; set; } = "";
        [Export] public string Description { get; set; } = "";
        [Export] public AbilityType Type { get; set; } = AbilityType.Melee;
        [Export] public TargetingType Targeting { get; set; } = TargetingType.SingleEnemy;
        [Export] public DamageType DamageType { get; set; } = DamageType.Physical;
        [Export] public float BaseDamage { get; set; } = 10f;
        [Export] public StatType ScalingStat { get; set; } = StatType.Strength;
        [Export] public float ScalingRatio { get; set; } = 0.5f;
        [Export] public float Cooldown { get; set; } = 1f;
        [Export] public float ManaCost { get; set; } = 0f;
        [Export] public float Range { get; set; } = 2f;
        [Export] public float AoERadius { get; set; } = 0f;
        [Export] public float KnockbackForce { get; set; } = 0f;
        [Export] public float StunDuration { get; set; } = 0f;

        public AbilityData() { }

        public AbilityData(string id, string name, AbilityType type, float baseDmg,
            StatType scaling, float ratio, float cooldown, float mana = 0)
        {
            Id = id;
            AbilityName = name;
            Type = type;
            BaseDamage = baseDmg;
            ScalingStat = scaling;
            ScalingRatio = ratio;
            Cooldown = cooldown;
            ManaCost = mana;
        }
    }
}
